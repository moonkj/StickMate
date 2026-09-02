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

        // ========================================================================
        // ★ 2026-09-02 — 이 파일이 <b>눈이 먼</b> 채로 빨개졌다. 그 사고와 처방
        // ========================================================================
        // CharacterInfoWindow가 partial 7개로 쪼개지면서 홀드 배선이
        // CharacterInfoWindow.Input.cs로 이사했다. 이 파일은 <c>CharacterInfoWindow.cs</c>
        // <b>한 파일만</b> 읽고 있었으므로 "배선이 없다"고 단언했다 — <b>프로덕션은 멀쩡했다.</b>
        //
        // 처방으로 파일명 하나를 다른 파일명으로 바꾸지 <b>않는다</b>. 또 쪼개지면 또 깨진다.
        // 대신 두 가지를 바꿨다:
        //   (1) 표면 하나 = <c>X.cs</c> + <c>X.*.cs</c> 조각 <b>전부</b>(ReadSurfaceSource).
        //   (2) 위치 비교(A가 B보다 앞인가)를 <b>메서드 본문 범위</b> 비교로 바꿨다(MethodBody).
        //       파일이 이어 붙는 순서에 따라 "Update()가 뒤에 온다"는 전제가 뒤집히기 때문이다.
        // 그리고 이 매처가 <b>진짜 결함을 잡는지</b>를 같은 테스트 안에서 양성 대조로 확인한다.

        /// <summary>한 <b>표면</b>의 소스 전체 — <c>X.cs</c>와 그 partial 조각 <c>X.*.cs</c>를 모두 읽어 잇는다.
        /// 쪼개지지 않은 표면(설정창/톱니/포스트잇)에서는 예전과 <b>바이트가 같다</b>.
        /// <para>구현은 <see cref="SourceConstantReader.ReadSurfaceText"/> <b>한 벌</b>이다 — 같은 규칙의
        /// 사본을 파일마다 두면 그것이 곧 다음 드리프트다(이 라운드에 눈먼 매처가 넷이었다).</para></summary>
        private static string ReadSurfaceSource(string folder, string file)
            => SourceConstantReader.ReadSurfaceText(
                Path.Combine(Application.dataPath, "_Project", "Scripts", folder, file));

        /// <summary>이름으로 메서드 <b>본문</b>({...} 포함)을 떼어 온다. 없으면 null.</summary>
        private static string TryMethodBody(string source, string signature)
        {
            int at = source.IndexOf(signature, StringComparison.Ordinal);
            if (at < 0) return null;
            int open = source.IndexOf('{', at);
            if (open < 0) return null;

            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0) return source.Substring(open, i - open + 1);
            }
            return null;
        }

        private static string MethodBody(string source, string signature, string what)
        {
            string body = TryMethodBody(source, signature);
            Assert.IsNotNull(body, what);
            return body;
        }

        /// <summary>
        /// 표면 하나의 홀드 배선을 <b>한 벌로</b> 판정한다. 성립하면 null, 아니면 깨진 이유.
        /// <para>진짜 소스와 <b>배선을 지운 사본</b>에 같은 함수를 돌려 양성 대조를 만든다 —
        /// 판정과 대조가 같은 코드를 쓰지 않으면 "대조만 통과하는 대조"가 된다.</para>
        /// </summary>
        private static string DiagnoseHoldWiring(string source, string pollSignature)
        {
            int holds = CountOf(source, HoldCall);
            if (holds != 1) return $"홀드 호출이 {holds}개다 — 하나여야 어느 조건이 진짜인지 알 수 있다";

            string tick = TryMethodBody(source, "private void TickFramePacingHold(");
            if (tick == null) return "TickFramePacingHold()가 없다";
            int hold = tick.IndexOf(HoldCall, StringComparison.Ordinal);
            if (hold < 0) return "홀드가 TickFramePacingHold() 밖에 있다(= 조건 없는 홀드와 같다)";
            int policy = tick.IndexOf("FramePacingPolicy.ShouldHoldForSurface(", StringComparison.Ordinal);
            if (policy < 0) return "플랫폼 중립 판정 함수(FramePacingPolicy.ShouldHoldForSurface)를 쓰지 않는다";
            if (policy > hold) return "홀드가 판정보다 앞이다 — 조건 없는 홀드와 같다";

            string poll = TryMethodBody(source, pollSignature);
            if (poll == null) return $"{pollSignature}가 없다";
            if (!poll.Contains("TickFramePacingHold("))
                return $"{pollSignature}가 TickFramePacingHold()를 부르지 않는다 — 정의만 있고 아무도 안 부른다";
            return null;
        }

        /// <summary>양성 대조용 사본 — <paramref name="signature"/> 본문 안의 <paramref name="call"/>만 지운다
        /// (중괄호 짝은 그대로 둔다).</summary>
        private static string WithCallRemovedInside(string source, string signature, string call)
        {
            string body = MethodBody(source, signature, $"양성 대조를 만들 수 없다 — {signature} 본문이 없다.");
            Assert.IsTrue(body.Contains(call), $"양성 대조 전제 실패 — {signature} 안에 {call}가 없다.");
            int at = source.IndexOf(body, StringComparison.Ordinal);
            return source.Substring(0, at) + body.Replace(call, "RemovedForPositiveControl(")
                   + source.Substring(at + body.Length);
        }

        // ========================================================================
        // ★ 2026-09-01 정정 — "열려 있는 동안 무조건 홀드"가 절전을 통째로 죽였다
        // ========================================================================
        // 원래 이 자리에는 "정보창 Update()가 <b>무조건</b> 홀드를 갱신한다"를 잠그는 테스트가
        // 있었다. 그 테스트는 정확히 <b>버그를 잠그고</b> 있었다.
        //
        // 실측(사용자 로그, 추측 아님):
        //   stickmate.log:127   [정보창] 열림   ...  발판 리포트 심장박동 125회 = 125분  ...
        //   stickmate.log:3125  [정보창] 닫힘
        //   그 125분간 [FramePacing/적응형] 등급 전이 0회, 활성 등급 체류 100%(정적/정지 0%).
        //   창을 닫은 직후 전이 재개(구간별 72 -> 114 -> 89 -> 58회), GPU 점유 추정 12~21% ->
        //   4.3~8.6%(약 2.5배 감소).
        //
        // Away(3분 무입력 + 캐릭터 Idle)가 이 홀드를 이기게 돼 있지만, 사용자가 <b>다른 앱에서
        // 계속 타이핑</b>하면 SecondsSinceUserInput이 3분에 닿지 않는다 — 홀드를 깨는 경로가
        // 실질적으로 없었다.
        //
        // 그래서 조건이 "보인다"에서 "조작 중이다"로 바뀌었고, 이 테스트도 그 반대 방향을 잠근다:
        // <b>무조건 홀드가 다시 들어오면 실패</b>한다.

        [Test]
        public void 정보창_홀드는_열려있음이_아니라_조작중일때만_걸린다()
        {
            // ★ 이 창은 partial 7개다 — 한 조각만 읽으면 배선이 이사한 라운드에 <b>거짓 빨강</b>이 난다
            //   (2026-09-02에 실제로 그렇게 빨개졌다. 위 ReadSurfaceSource 문단 참고).
            const string poll = "private void TickGlobalPointer()";
            string source = ReadSurfaceSource("Interaction", "CharacterInfoWindow.cs");

            string broken = DiagnoseHoldWiring(source, poll);
            Assert.IsNull(broken, $"정보창의 프레임 페이싱 홀드 배선이 깨졌다 — {broken}.");

            // ★ 되돌림 방지 — Update() 본문에 홀드가 직접 있으면 안 된다(125분 실측 사고 그 자체).
            string update = MethodBody(source, "private void Update()",
                "CharacterInfoWindow.Update()가 사라졌다 — 이 테스트를 갱신하라.");
            Assert.IsFalse(update.Contains(HoldCall),
                "홀드가 Update() 본문에 있다 — '창이 열려 있는 동안 무조건 홀드'가 되살아났다. " +
                "사용자 로그에서 정보창 125분 동안 등급 전이 0회 / 활성 100%였던 그 사고다.");

            // ================= 양성 대조 — 이 매처가 진짜 결함을 잡는가 =================
            // 없으면 위 세 단언은 "언제나 참"과 구별되지 않는다. 두 가지 결함을 각각 심어 본다.
            Assert.IsNotNull(DiagnoseHoldWiring(source.Replace(HoldCall, "NoHold("), poll),
                "양성 대조 실패 — 홀드 호출을 통째로 지웠는데도 이 매처가 초록이다. 눈이 멀었다.");
            Assert.IsNotNull(
                DiagnoseHoldWiring(WithCallRemovedInside(source, poll, "TickFramePacingHold("), poll),
                "양성 대조 실패 — 폴링 경로의 호출을 지웠는데도 초록이다. '정의만 있고 아무도 안 부른다'를 " +
                "못 잡는다는 뜻이고, 그것이 이 파일이 막으려는 DisplaySleepPolicyTests의 실패 양식이다.");
        }

        [Test]
        public void 설정창_홀드도_정보창과_같은_조건으로_걸린다()
        {
            // 정보창만 고치면 설정창이 같은 배선으로 남는다 — 실제로 그렇게 남아 있었다.
            string source = ReadScript("Interaction", "SettingsWindow.cs");

            Assert.AreEqual(1, CountOf(source, HoldCall), "설정창의 홀드가 없거나 둘 이상이다.");
            int tick = IndexOfOrFail(source, "private void TickFramePacingHold(", 0,
                "SettingsWindow.TickFramePacingHold()가 사라졌다 — 이 테스트를 갱신하라.");
            int policy = IndexOfOrFail(source, "FramePacingPolicy.ShouldHoldForSurface(", tick,
                "설정창이 플랫폼 중립 판정 함수를 쓰지 않는다.");
            int call = IndexOfOrFail(source, HoldCall, tick, "설정창의 홀드가 TickFramePacingHold() 안에 없다.");
            Assert.Less(policy, call, "홀드가 판정보다 앞이다.");

            // 슬라이더 드래그는 커서가 창 밖으로 나가도 이어진다 — 사각형 판정만으로는 못 잡는다.
            Assert.IsTrue(source.Contains("_dragIndex >= 0"),
                "설정창이 '슬라이더를 끄는 중'을 조작으로 세지 않는다 — 커서가 패널 밖으로 나가는 " +
                "순간 홀드가 풀려 손잡이가 계단식으로 따라온다.");
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

        // ========================================================================
        // ✝ 삭제된 테스트 2건 — 구석 호버 패널 / 크기 다이얼 (2026-09-01)
        // ========================================================================
        // `구석호버패널은_보이는_동안에만_홀드를_갱신한다`와
        // `크기다이얼은_소유자인_구석패널의_홀드에_덮인다_중복배선금지`가 여기 있었다. 둘 다
        // `Interaction/CornerHoverPanel.cs` / `Interaction/SizeDialWidget.cs`를 소스 스캔했는데,
        // 같은 날 다른 작업이 그 두 컴포넌트를 <b>통째로 삭제</b>했다(사용자 요청, git staged
        // 삭제 + 전용 테스트 2건도 함께 제거됨). 없는 파일을 읽는 테스트는 통과할 수도 실패할
        // 수도 없는 소음이라 함께 걷었다.
        //
        // ★ 되돌리려면: `git show HEAD:Assets/_Project/Scripts/Tests/EditMode/UiInteractionFramePacingHoldTests.cs`
        //   에 원문이 그대로 있다. 구석 패널이 부활하면 그 두 테스트도 같이 되살려야 한다 —
        //   그 패널의 Update()는 <b>숨어 있을 때도 매 프레임 돌기</b> 때문에 조건 없는 홀드가
        //   곧 "24시간 Active"였다는 것이 그 테스트의 요지였다.


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
                new[] { "Interaction", "SettingsWindow.cs" },
                new[] { "Interaction", "GearRadialMenuWidget.cs" },
                new[] { "Interaction", "TodoPostItWidget.cs" },
            };

            foreach (string[] surface in surfaces)
            {
                // ★ 표면 = 파일 하나가 아니다. partial로 쪼개진 표면도 <b>통째로</b> 읽는다 —
                //   이 줄이 ReadScript였을 때 정보창 분할 라운드에서 거짓 빨강이 났다.
                string source = ReadSurfaceSource(surface[0], surface[1]);
                Assert.GreaterOrEqual(CountOf(source, HoldCall), 1,
                    $"{surface[1]}에 프레임 페이싱 홀드 배선이 없다.");

                // 양성 대조 — 명부가 <b>실제로 세고 있는가</b>. 지웠는데도 0이 안 나오면 이 루프는
                // 아무 것도 안 보고 있는 것이다(거짓 통과 #5의 형태: 빈 목록이 조용히 초록).
                Assert.AreEqual(0, CountOf(source.Replace(HoldCall, "NoHold("), HoldCall),
                    $"{surface[1]}: 양성 대조 실패 — 홀드를 지운 사본에서도 배선이 세어진다.");
            }

            Assert.AreEqual(4, surfaces.Length,
                "명부가 비었거나 줄었다 — 빈 명부는 foreach가 아무것도 안 재고 초록이 된다(거짓 통과 #5).");
        }

        // ========================================================================
        // 표면 홀드 판정 자체 (2026-09-01 — 125분 실측 사고)
        // ========================================================================

        [Test]
        public void 커서가_창_위에_있으면_홀드한다()
        {
            Assert.IsTrue(FramePacingPolicy.ShouldHoldForSurface(
                cursorOverSurface: true, manipulating: false, secondsSinceLastTouch: 0f),
                "커서가 창 위에 있다 = 클릭/드래그 직전이다. 여기서 절감하면 첫 조작이 굼떠 보인다.");
        }

        [Test]
        public void 커서가_창_밖으로_나가도_조작중이면_홀드한다()
        {
            // 창 드래그/슬라이더 드래그는 커서가 사각형 밖으로 나가도 계속된다 — 사각형 판정만
            // 쓰면 빠르게 끄는 순간 홀드가 풀려 손잡이가 계단식으로 따라온다.
            Assert.IsTrue(FramePacingPolicy.ShouldHoldForSurface(
                cursorOverSurface: false, manipulating: true,
                secondsSinceLastTouch: 99f));
        }

        [Test]
        public void 조작_직후_짧은_여유_동안은_홀드가_유지된다()
        {
            // 커서를 20Hz로 훑기 때문에 스치듯 들락거리면 판정이 그 주기로 깜빡인다.
            float inside = FramePacingPolicy.SurfaceHoldLingerSeconds * 0.5f;
            Assert.IsTrue(FramePacingPolicy.ShouldHoldForSurface(false, false, inside));

            float outside = FramePacingPolicy.SurfaceHoldLingerSeconds + 0.01f;
            Assert.IsFalse(FramePacingPolicy.ShouldHoldForSurface(false, false, outside),
                "여유가 끝났는데도 홀드가 남으면 그게 곧 '열려 있는 동안 무조건'이다.");
        }

        [Test]
        public void 창이_그냥_떠있기만_하면_홀드하지_않는다_125분_회귀()
        {
            // ★ 이것이 실측 사고의 회귀 잠금이다: 창은 열려 있고, 커서는 다른 앱에 있고,
            //   마지막 조작으로부터 한참 지났다(사용자는 다른 앱에서 타이핑 중이라 무입력 시계도
            //   흐르지 않는다 = Away가 성립하지 않는다).
            bool held = FramePacingPolicy.ShouldHoldForSurface(
                cursorOverSurface: false, manipulating: false,
                secondsSinceLastTouch: 125f * 60f);
            Assert.IsFalse(held, "창이 떠 있다는 이유만으로 60fps를 붙잡으면 적응형 절전이 통째로 죽는다.");

            // 그리고 그 결과로 등급이 실제로 내려가야 한다 — 판정만 고치고 등급이 그대로면
            // 아무것도 고치지 못한 것이다. 캐릭터가 오래 서 있는 상황을 쓴다.
            ViewerPresenceSnapshot typingInAnotherApp = Presence(idleSeconds: 0.3f);
            FramePacingTier tier = FramePacingPolicy.DecideTier(typingInAnotherApp,
                suspendedForFullscreen: false, characterIdle: true, uiInteractionActive: held,
                characterStill: true);
            Assert.AreEqual(FramePacingTier.Still, tier,
                "창을 열어 둔 채 다른 앱에서 일하는 동안에는 정지 등급까지 내려가야 한다.");

            // 네거티브 컨트롤 — 옛 배선(무조건 홀드)이면 같은 관측에서 활성 등급에 묶인다.
            Assert.AreEqual(FramePacingTier.Active,
                FramePacingPolicy.DecideTier(typingInAnotherApp, false, true,
                    uiInteractionActive: true, characterStill: true),
                "대조군 전제 실패 — 무조건 홀드가 활성 등급을 붙잡는 것이 이 사고의 인과였다.");
        }

        [Test]
        public void 표면_홀드_여유는_하강_제동보다_길지_않다()
        {
            // 여유가 하강 제동(FramePacing이 깊어지는 전이를 막는 시간)보다 길면 그 차이만큼
            // 절감이 그냥 사라진다. 숫자를 베끼지 않고 두 상수를 비교한다.
            Assert.LessOrEqual(FramePacingPolicy.SurfaceHoldLingerSeconds,
                FramePacing.TierDescendCooldownSeconds,
                "표면 홀드 여유가 등급 하강 제동보다 길다 — 그만큼은 절감이 불가능한 시간이다.");
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
