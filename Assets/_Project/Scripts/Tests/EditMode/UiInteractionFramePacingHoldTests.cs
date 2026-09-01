using System;
using System.IO;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// 회귀 잠금 — <b>"사용자가 이 앱의 창을 붙잡고 있는 동안에는 프레임을 깎지 않는다."</b>
    ///
    /// ============================================================================
    /// 무엇이 터졌었나 (2026-08-31, 사용자 신고 원문 그대로)
    /// ============================================================================
    /// "윈도우에서는 아직도 렉이 있는거 같음" -> "심지어 기어 설정창조차 클릭하면 약간 렉걸린듯이 움직임"
    ///
    /// 확정된 인과(코드 검증 — 추측 아님):
    /// <list type="number">
    /// <item>사용자가 캐릭터 정보창을 열고 <b>읽는다</b>. 마우스를 안 움직인다 -> 무입력 2초 경과
    ///       (<see cref="FramePacingPolicy.RecentInputSeconds"/>).</item>
    /// <item>그 사이 캐릭터가 자율 배회의 Idle 구간(실측 2~6초)에 들어간다 -> 등급 <b>Calm</b>.</item>
    /// <item>Windows는 baseVSyncCount=0이라 Calm이 <c>Application.targetFrameRate</c>를 60->30으로
    ///       나눈다 — <b>게임 루프 자체가 30Hz</b>가 된다(macOS는 renderFrameInterval만 바꿔 루프는
    ///       60Hz 유지. 사용자가 "윈도우에서는"이라고 한 플랫폼 비대칭이 여기서 나온다).</item>
    /// <item><c>CharacterInfoWindow</c>의 타이틀바 드래그는 <c>Update()</c>마다 OS 커서를 한 번
    ///       폴링해 패널을 옮긴다 -> 커서 표본 주기도 30Hz -> 창이 커서를 <b>계단식</b>으로 따라온다.</item>
    /// <item>다시 마우스를 움직여도 등급 복귀는 다음 관측 폴링(최대 0.2초)에나 일어난다 ->
    ///       <b>모든 상호작용의 첫 0.2초</b>가 절반 프레임레이트로 시작한다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// 이 테스트가 지키는 4개의 불변식
    /// ============================================================================
    /// <list type="number">
    /// <item>홀드가 걸리면 <b>Calm으로 내려가지 않는다</b>(신고된 증상 그 자체).</item>
    /// <item>홀드는 <b>DisplayOff / Suspended / Away를 이기지 못한다</b> — 각각 관측된 사실,
    ///       비침해 원칙 2, 24시간 상주 절감의 안전장치다. 이 순서가 뒤집히면 "창을 열어 둔 채
    ///       자리를 비우면 밤새 60fps"가 된다.</item>
    /// <item>홀드는 <b>저전력 감쇄도 함께</b> 막는다 — 등급만 Active로 올리고 이걸 빼먹으면
    ///       배터리 세이버가 켜진 노트북에서 증상이 그대로 남는다(별개의 경로다).</item>
    /// <item>홀드가 <b>실제로 배선돼</b> 있다 — 정책만 고치고 호출부가 없으면 전부 초록불인 채로
    ///       버그가 살아남는다(<c>DisplaySleepPolicyTests</c>가 겪은 그 실패 양식).</item>
    /// </list>
    /// 각 불변식에 <b>네거티브 컨트롤</b>을 붙인다 — 규칙을 되돌리면 실제로 실패하는지가 확인돼야
    /// "항상 참인 단언"이 아니다(이 프로젝트 표준).
    /// </summary>
    public sealed class UiInteractionFramePacingHoldTests
    {
        // 두 플랫폼의 기준값(AdaptiveFramePacingPolicyTests와 같은 상수).
        private const int MacBaseVSync = 2;
        private const int MacBaseTarget = -1;
        private const int WinBaseVSync = 0;
        private const int WinBaseTarget = 60;

        private static ViewerPresenceSnapshot Presence(bool asleep = false, float idleSeconds = 0f,
            bool lowPower = false, bool onBattery = false)
            => new ViewerPresenceSnapshot(asleep, idleSeconds, lowPower, onBattery);

        /// <summary>창을 읽는 동안의 관측: 사람은 있고(Valid) 화면도 켜져 있지만 한동안 입력이 없다.</summary>
        private static ViewerPresenceSnapshot ReadingTheWindow() => Presence(idleSeconds: 5f);

        [SetUp]
        public void ResetHoldBefore() => FramePacing.ResetForTests();

        [TearDown]
        public void ResetHoldAfter() => FramePacing.ResetForTests();

        // ========================================================================
        // 불변식 1 — 홀드가 걸리면 Calm으로 내려가지 않는다
        // ========================================================================

        [Test]
        public void UI홀드가_걸리면_창을_읽는_동안에도_활성등급을_유지한다()
        {
            FramePacingTier tier = FramePacingPolicy.DecideTier(
                ReadingTheWindow(), suspendedForFullscreen: false, characterIdle: true,
                uiInteractionActive: true);

            Assert.AreEqual(FramePacingTier.Active, tier,
                "정보창이 열려 있다는 것은 '사용자가 보고 있다'는 관측된 사실이다 — 절감하면 안 된다.");
        }

        [Test]
        public void 네거티브컨트롤_홀드가_없으면_같은_관측에서_정적등급으로_내려간다()
        {
            // 위 단언이 "항상 참"이 아님을 보이는 대조군 = 신고된 버그의 재현이다.
            FramePacingTier tier = FramePacingPolicy.DecideTier(
                ReadingTheWindow(), suspendedForFullscreen: false, characterIdle: true,
                uiInteractionActive: false);

            Assert.AreEqual(FramePacingTier.Calm, tier,
                "대조군 전제 실패 — 이 관측이 원래 Calm으로 내려가야 위 테스트가 의미를 가진다.");
        }

        [Test]
        public void 기존_3인자_판정은_홀드없음과_완전히_같다()
        {
            // 구 시그니처를 남긴 이유는 호출부/테스트 호환이다. 두 경로가 갈리면 "어느 쪽이 진짜지?"가
            // 생기므로 같은 답임을 못박는다.
            foreach (bool idle in new[] { true, false })
            {
                foreach (float sec in new[] { 0f, 1f, 5f, FramePacingPolicy.AwaySeconds + 1f })
                {
                    ViewerPresenceSnapshot p = Presence(idleSeconds: sec);
                    Assert.AreEqual(
                        FramePacingPolicy.DecideTier(p, false, idle),
                        FramePacingPolicy.DecideTier(p, false, idle, uiInteractionActive: false),
                        $"idle={idle}, 무입력={sec}초");
                }
            }
        }

        // ========================================================================
        // 불변식 2 — 홀드가 이겨서는 안 되는 세 등급
        // ========================================================================

        [Test]
        public void UI홀드는_화면꺼짐을_이기지_못한다()
        {
            // 화면이 꺼진 것은 관측된 사실이다 — 창이 열려 있어도 볼 사람이 물리적으로 없다.
            FramePacingTier tier = FramePacingPolicy.DecideTier(
                Presence(asleep: true, idleSeconds: 0f), suspendedForFullscreen: false,
                characterIdle: true, uiInteractionActive: true);

            Assert.AreEqual(FramePacingTier.DisplayOff, tier);
        }

        [Test]
        public void UI홀드는_전체화면_숨김을_이기지_못한다()
        {
            // CLAUDE.md 절대 불변 원칙 2(비침해) — 전체화면 게임의 프레임을 갉아먹으면 안 된다.
            FramePacingTier tier = FramePacingPolicy.DecideTier(
                Presence(idleSeconds: 0f), suspendedForFullscreen: true,
                characterIdle: true, uiInteractionActive: true);

            Assert.AreEqual(FramePacingTier.Suspended, tier);
        }

        [Test]
        public void UI홀드는_자리비움을_이기지_못한다()
        {
            // ★ 24시간 상주 앱의 안전장치: 창을 열어 둔 채 사용자가 자리를 비우면(3분 무입력)
            //   홀드가 60fps를 영구히 붙잡아 밤새 OS 컴포지터를 돌리게 된다. Away가 이겨야 한다.
            FramePacingTier tier = FramePacingPolicy.DecideTier(
                Presence(idleSeconds: FramePacingPolicy.AwaySeconds + 1f), suspendedForFullscreen: false,
                characterIdle: true, uiInteractionActive: true);

            Assert.AreEqual(FramePacingTier.Away, tier,
                "홀드가 Away를 이기면 '잊고 열어 둔 창' 하나가 절감을 통째로 무력화한다.");
        }

        [Test]
        public void 자리비움에_져도_신고된_증상은_되살아나지_않는다()
        {
            // 위 테스트가 신고를 되살리지 않는 이유를 코드로 못박는다: 창을 **끌고 있는 동안에는
            // 정의상 입력이 계속** 들어오므로 Away 조건(180초 무입력)이 성립할 수 없다.
            FramePacingTier tier = FramePacingPolicy.DecideTier(
                Presence(idleSeconds: 0.05f), suspendedForFullscreen: false,
                characterIdle: true, uiInteractionActive: true);

            Assert.AreEqual(FramePacingTier.Active, tier);
        }

        // ========================================================================
        // 불변식 3 — 저전력 감쇄도 함께 막는다(등급과는 별개의 경로)
        // ========================================================================

        [Test]
        public void UI홀드가_걸리면_저전력_감쇄를_적용하지_않는다()
        {
            Assert.IsFalse(
                FramePacingPolicy.ShouldApplyLowPowerDownshift(Presence(lowPower: true), uiInteractionActive: true),
                "창을 끌고 있는 몇 초까지 반값으로 그릴 이유는 없다(그 몇 초의 전력은 무시할 수 있다).");
        }

        [Test]
        public void 네거티브컨트롤_홀드가_없으면_저전력_감쇄가_실제로_적용된다()
        {
            Assert.IsTrue(
                FramePacingPolicy.ShouldApplyLowPowerDownshift(Presence(lowPower: true), uiInteractionActive: false),
                "대조군 전제 실패 — 저전력 감쇄 자체가 사라지면 위 테스트가 무의미해진다.");

            Assert.IsFalse(
                FramePacingPolicy.ShouldApplyLowPowerDownshift(Presence(lowPower: false), uiInteractionActive: false),
                "저전력이 아니면 홀드와 무관하게 감쇄가 없다.");

            Assert.IsFalse(
                FramePacingPolicy.ShouldApplyLowPowerDownshift(default, uiInteractionActive: false),
                "관측 실패(Valid=false)면 감쇄하지 않는다 — '모르면 내려가지 않는다'.");
        }

        [Test]
        public void 저전력_노트북에서_홀드는_두_플랫폼_모두에서_기준값을_되돌린다()
        {
            // 등급만 Active로 올리고 저전력 감쇄를 빼먹으면 배터리 세이버가 켜진 기기에서 증상이
            // 그대로 남는다. 손잡이 값까지 확인한다.
            ViewerPresenceSnapshot p = Presence(lowPower: true, onBattery: true);

            FramePacingPlan win = FramePacingPolicy.BuildPlan(FramePacingTier.Active,
                WinBaseVSync, WinBaseTarget, FramePacingPolicy.ShouldApplyLowPowerDownshift(p, true));
            Assert.AreEqual(WinBaseTarget, win.EffectiveTargetFps, "Windows: 60fps 그대로여야 한다.");
            Assert.AreEqual(1, win.RenderFrameInterval);

            FramePacingPlan mac = FramePacingPolicy.BuildPlan(FramePacingTier.Active,
                MacBaseVSync, MacBaseTarget, FramePacingPolicy.ShouldApplyLowPowerDownshift(p, true));
            Assert.AreEqual(1, mac.RenderFrameInterval, "macOS: 매 프레임 렌더여야 한다.");
            Assert.AreEqual(MacBaseVSync, mac.VSyncCount);

            // 네거티브 컨트롤 — 홀드가 없으면 같은 관측에서 실제로 반값이 된다.
            // (2026-09-01부터 Windows도 "반값"을 renderFrameInterval로 표현한다 — 루프는 60Hz 유지.
            //  그래서 TargetFrameRate가 아니라 EffectiveTargetFps를 본다.)
            FramePacingPlan winIdle = FramePacingPolicy.BuildPlan(FramePacingTier.Active,
                WinBaseVSync, WinBaseTarget, FramePacingPolicy.ShouldApplyLowPowerDownshift(p, false));
            Assert.AreEqual(30, winIdle.EffectiveTargetFps);
        }

        // ========================================================================
        // 홀드 수명 — "해제 책임이 존재하지 않는다"
        // ========================================================================

        [Test]
        public void 홀드는_한번_부르면_걸리고_만료시간이_지나면_스스로_풀린다()
        {
            Assert.IsFalse(FramePacing.IsInteractionHeld, "초기 상태는 홀드 없음이어야 한다.");

            FramePacing.HoldActiveForInteraction();
            Assert.IsTrue(FramePacing.IsInteractionHeld);

            // 만료를 시계 대기 없이 검증한다: 길이 0의 홀드는 "지금 이 순간 이미 만료"다.
            // (구현이 `Time.unscaledTime < 만료시각`이므로 시계의 절대값과 무관하게 결정적이다.)
            FramePacing.ResetForTests();
            FramePacing.HoldActiveForInteraction(0f);
            Assert.IsFalse(FramePacing.IsInteractionHeld,
                "길이 0의 홀드가 걸린 채로 남으면 '만료 시각' 방식이 아니라 켜고 끄는 플래그라는 뜻이다 — "
                + "그러면 호출부가 죽었을 때 60fps가 영원히 붙잡힌다.");
        }

        [Test]
        public void 홀드_기본길이는_관측_폴링_최대주기보다_짧지_않다()
        {
            // 홀드가 폴링 간격보다 짧으면 폴링 사이에서 깜빡여 등급이 진동한다(Active<->Calm 왕복은
            // 그 자체가 손잡이 대입 + 로그 문자열 할당이다).
            Assert.GreaterOrEqual(FramePacing.InteractionHoldSeconds, 0.5f);
        }

        // ========================================================================
        // 불변식 4 — 배선(정적 스캔). 정책만 고치고 호출부가 없으면 전부 초록불인 채 버그가 산다.
        // ========================================================================

        private static string ReadScript(params string[] relative)
        {
            string path = Path.Combine(Application.dataPath, "_Project", "Scripts");
            foreach (string part in relative) path = Path.Combine(path, part);
            Assert.IsTrue(File.Exists(path), $"소스를 찾지 못했다: {path}");
            return File.ReadAllText(path);
        }

        [Test]
        public void 정보창은_열려있는_동안_매프레임_홀드를_갱신한다()
        {
            string source = ReadScript("Interaction", "CharacterInfoWindow.cs");

            int update = source.IndexOf("private void Update()", StringComparison.Ordinal);
            Assert.Greater(update, 0, "CharacterInfoWindow.Update()가 사라졌다 — 이 테스트를 갱신하라.");

            int call = source.IndexOf("FramePacing.HoldActiveForInteraction(", update, StringComparison.Ordinal);
            Assert.Greater(call, update,
                "정보창 Update()가 프레임 페이싱 홀드를 갱신하지 않는다 — 창을 읽는 동안 Calm으로 " +
                "내려갔다가 끌기 시작하는 첫 0.2초가 절반 프레임레이트가 되는 신고 버그가 되살아난다.");

            // 홀드는 '창이 열려 있을 때만'이어야 한다 — 닫힌 창이 60fps를 붙잡으면 안 된다.
            int guard = source.IndexOf("if (!_open) return;", update, StringComparison.Ordinal);
            Assert.Greater(guard, update, "Update()의 '닫혀 있으면 즉시 반환' 가드가 사라졌다.");
            Assert.Less(guard, call,
                "홀드 갱신이 '창이 열려 있는가' 가드보다 앞에 있다 — 닫힌 창이 프레임을 붙잡게 된다.");
        }

        // ========================================================================
        // 불변식 4-b — 나머지 상호작용 표면들도 같은 배선을 갖는다 (2026-08-31 2차 라운드)
        //
        // 정보창 하나만 고치면 "톱니 부채꼴을 클릭하면 렉", "구석 패널 다이얼을 돌리면 계단식"이
        // 그대로 남는다. 원인이 등급 정책이 아니라 **호출부의 존재 여부**이므로, 표면이 늘어날
        // 때마다 이 파일에 한 줄씩 늘어나는 것이 정상이다.
        //
        // ★ 다만 "열려 있으면 무조건 홀드"가 아니다 — 상시 표면(하루 종일 떠 있는 HUD)에 그렇게
        //   걸면 Calm 등급이 영영 성립하지 않아 적응형 절감이 통째로 죽는다. 아래 각 테스트가
        //   표면별로 **어떤 조건**에 걸려 있어야 하는지까지 못박는 이유다.
        // ========================================================================

        private static int IndexOfOrFail(string source, string needle, int from, string what)
        {
            int i = source.IndexOf(needle, from, StringComparison.Ordinal);
            Assert.Greater(i, 0, what);
            return i;
        }

        private static int CountOf(string source, string needle)
        {
            int n = 0, i = 0;
            while ((i = source.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }

        private const string HoldCall = "FramePacing.HoldActiveForInteraction(";

        [Test]
        public void 부채꼴메뉴는_펼쳐져있는_동안_매프레임_홀드를_갱신한다()
        {
            string source = ReadScript("Interaction", "GearRadialMenuWidget.cs");

            int loop = IndexOfOrFail(source, "private void LateUpdate()", 0,
                "GearRadialMenuWidget.LateUpdate()가 사라졌다 — 이 테스트를 갱신하라.");
            int hiddenGuard = IndexOfOrFail(source, "if (_phase == Phase.Hidden) return;", loop,
                "'접혀 있으면 즉시 반환' 가드가 사라졌다.");
            int suspend = IndexOfOrFail(source, "_agent.IsSuspended", loop,
                "전체화면 숨김(비침해 원칙 2) 가드가 사라졌다.");
            int call = IndexOfOrFail(source, HoldCall, loop,
                "부채꼴 메뉴가 프레임 페이싱 홀드를 갱신하지 않는다 — 메뉴를 띄워 놓고 겨누는 동안 " +
                "Calm으로 내려가 버튼 클릭/호버 반응이 절반 프레임레이트로 시작한다.");

            Assert.Less(hiddenGuard, call,
                "홀드가 '접혀 있는가' 가드보다 앞이다 — 접힌 메뉴가 하루 종일 60fps를 붙잡는다.");
            Assert.Less(suspend, call,
                "홀드가 전체화면 숨김 가드보다 앞이다 — 원칙 2(비침해)보다 먼저 프레임을 붙잡게 된다.");
        }

        [Test]
        public void 구석호버패널은_보이는_동안에만_홀드를_갱신한다()
        {
            string source = ReadScript("Interaction", "CornerHoverPanel.cs");

            int loop = IndexOfOrFail(source, "private void Update()", 0,
                "CornerHoverPanel.Update()가 사라졌다 — 이 테스트를 갱신하라.");

            // ★ 이 패널의 Update()는 **숨어 있을 때도 매 프레임 돈다**(구석 감지 폴링). 그래서
            //   조건 없는 홀드는 곧 "24시간 Active"다. 조건까지 한 문자열로 못박는다.
            Assert.Greater(source.IndexOf("if (IsVisible) " + HoldCall, loop, StringComparison.Ordinal), loop,
                "구석 패널의 홀드가 'IsVisible'로 묶여 있지 않다 — 이 Update()는 숨어 있을 때도 돌기 " +
                "때문에, 조건이 빠지면 상시 폴링이 그대로 상시 60fps가 되어 적응형 절감이 죽는다.");
        }

        [Test]
        public void 크기다이얼은_소유자인_구석패널의_홀드에_덮인다_중복배선금지()
        {
            // SizeDialWidget은 MonoBehaviour가 아니라 CornerHoverPanel이 직접 들고 굴리는 평범한
            // 클래스다(그 파일의 "왜 MonoBehaviour가 아닌가" 절). 매 프레임 진입점이 없으므로
            // 배선할 자리 자체가 없고, 다이얼 드래그는 소유자의 TickPointer()가 굴린다.
            // 여기에 따로 홀드를 넣으면 같은 사실을 두 곳이 주장하게 된다(진실이 둘).
            string dial = ReadScript("Interaction", "SizeDialWidget.cs");

            // (파일 안 "왜 MonoBehaviour가 아닌가" 설명 문단에도 그 단어가 나오므로 선언부로 본다.)
            Assert.IsFalse(dial.Contains("class SizeDialWidget : MonoBehaviour"),
                "SizeDialWidget이 컴포넌트가 됐다면 자기 Update()가 생겼다는 뜻이다 — 그때는 이 " +
                "테스트가 아니라 '보이는 동안 홀드' 배선을 추가하고 이 주석을 갱신하라.");
            Assert.AreEqual(0, CountOf(dial, "void Update()") + CountOf(dial, "void LateUpdate()"),
                "다이얼에 매 프레임 진입점이 생겼다 — 위와 같다.");
            Assert.AreEqual(0, CountOf(dial, HoldCall),
                "다이얼에 홀드가 중복 배선됐다 — 홀드의 소유자는 CornerHoverPanel 하나여야 한다.");

            // 소유자 쪽 배선이 실제로 살아 있어야 이 '없음'이 안전하다.
            string owner = ReadScript("Interaction", "CornerHoverPanel.cs");
            Assert.AreEqual(1, CountOf(owner, HoldCall),
                "소유자(CornerHoverPanel)의 홀드가 없거나 둘 이상이다 — 없으면 다이얼 드래그가 " +
                "30Hz로 끊기고, 둘 이상이면 어느 쪽이 진짜인지 다음 사람이 알 수 없다.");
            Assert.Greater(owner.IndexOf("_dial", StringComparison.Ordinal), 0,
                "구석 패널이 더 이상 다이얼을 소유하지 않는다 — 배선 책임을 다시 정하라.");
        }

        [Test]
        public void 할일포스트잇은_상시표면이라_클릭중에만_홀드를_갱신한다()
        {
            string source = ReadScript("Interaction", "TodoPostItWidget.cs");

            int poll = IndexOfOrFail(source, "private void TickGlobalClickPolling()", 0,
                "TodoPostItWidget.TickGlobalClickPolling()이 사라졌다 — 이 테스트를 갱신하라.");
            Assert.Greater(source.IndexOf("if (left) " + HoldCall, poll, StringComparison.Ordinal), poll,
                "포스트잇의 홀드가 '버튼이 눌려 있는가'에 묶여 있지 않다.");

            // ★ 이 위젯만 조건이 다른 이유를 테스트로 남긴다: 할 일이 있으면 **하루 종일** 떠 있는
            //   상시 HUD라, 다른 표면처럼 '보이는 동안' 걸면 Calm이 영영 성립하지 않는다.
            //   (정보창/부채꼴/구석패널은 수명이 짧거나 커서가 붙어 있어야 유지되는 표면이다.)
            Assert.AreEqual(1, CountOf(source, HoldCall),
                "포스트잇에 홀드가 2개 이상이다 — '보이는 동안' 걸린 것이 섞여 들어오면 상시 HUD가 " +
                "24시간 60fps를 붙잡아 적응형 절감이 통째로 무력화된다.");
            Assert.IsFalse(source.Contains("activeSelf) " + HoldCall),
                "'패널이 보이면 홀드'가 들어왔다 — 위와 같은 이유로 이 위젯에서는 금지다.");
        }

        [Test]
        public void 상호작용_표면_명부가_빠짐없이_배선돼_있다()
        {
            // 표면이 하나 늘 때마다 여기 한 줄을 추가한다. "정책은 고쳤는데 호출부가 없다"는
            // 실패 양식(DisplaySleepPolicyTests)을 명부로 막는다.
            string[][] surfaces =
            {
                new[] { "Interaction", "CharacterInfoWindow.cs" },
                new[] { "Interaction", "GearRadialMenuWidget.cs" },
                new[] { "Interaction", "CornerHoverPanel.cs" },
                new[] { "Interaction", "TodoPostItWidget.cs" },
            };

            foreach (string[] surface in surfaces)
            {
                string source = ReadScript(surface);
                Assert.GreaterOrEqual(CountOf(source, HoldCall), 1,
                    $"{surface[surface.Length - 1]}에 프레임 페이싱 홀드 배선이 없다.");
            }
        }

        [Test]
        public void 등급판정과_저전력감쇄_양쪽_모두에_홀드가_배선돼_있다()
        {
            string source = ReadScript("Platform", "FramePacing.cs");

            int evaluate = source.IndexOf("static void EvaluateAdaptiveTier(", StringComparison.Ordinal);
            Assert.Greater(evaluate, 0, "EvaluateAdaptiveTier가 사라졌거나 이름이 바뀌었다.");

            Assert.Greater(source.IndexOf("IsInteractionHeld", evaluate, StringComparison.Ordinal), evaluate,
                "등급 판정 경로에서 홀드를 읽지 않는다.");
            Assert.Greater(source.IndexOf("ShouldApplyLowPowerDownshift", evaluate, StringComparison.Ordinal), evaluate,
                "저전력 감쇄 경로에 홀드가 빠졌다 — 배터리 세이버 기기에서 증상이 그대로 남는다.");
        }
    }
}
