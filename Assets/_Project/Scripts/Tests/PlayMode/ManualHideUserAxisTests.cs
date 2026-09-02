using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ <b>사용자 명시 숨김</b>(⌃⌥⌘K / 설정창 [일반] "지금 즉시") — 2026-09-02 신설.
    ///
    /// ============================================================================
    /// 이 파일이 지키는 사실
    /// ============================================================================
    /// 예전 구현(<c>SettingsWindow.SetCharacterVisibleNow</c>)은 <b>렌더러만</b> 껐고, 자기 XML 문서가
    /// 그 한계를 자백하고 있었다 — <i>"전체화면 감지가 왕복하면 StickmanAgent.Resume()이 렌더러를
    /// 되살린다"</i>. 화면공유 중에 되살아나는 숨김은 숨김이 아니고, 무엇보다 <b>열린 창과 그 클릭
    /// 차단막은 애초에 걷히지도 않았다</b>(발표 화면에 캐릭터 대신 설정창이 찍혔다).
    ///
    /// 그래서 이 스위트는 세 가지를 <b>서로 다른 방법으로</b> 잰다:
    /// <list type="number">
    ///   <item><b>Suspend 경로를 실제로 타는가</b> — 렌더러가 아니라 <b>씬의 클릭 차단막 전수</b>가
    ///         0개가 되는지로 판정한다(렌더러만 끄는 옛 구현은 여기서 반드시 실패한다).</item>
    ///   <item><b>★ 네거티브 컨트롤 — 전체화면 왕복에도 되살아나지 않는가</b>. 이 라운드의 핵심이다.
    ///         ①<b>실측 폴링</b>(에이전트 자신의 <c>TickFullscreenSuspend</c>를 벽시계로 여러 주기
    ///         돌린다. 에디터의 <c>NullPlatformWindowService.IsFullscreenAppActive()</c>는 항상
    ///         false라, <b>옛 코드였다면 첫 폴링에서 Resume()</b>이 났다)와
    ///         ②<b>축 1 주입</b>(전체화면 감지의 true→false 반쪽까지 재현)을 <b>둘 다</b> 쓴다.</item>
    ///   <item><b>두 축이 하나로 묶이지 않았는가</b> — 설정창의 "전체화면 자동 숨김"을 꺼도
    ///         사용자 숨김은 유지되어야 한다. 이 둘을 <c>||</c> 한 줄로 합치면 여기서 깨진다.</item>
    /// </list>
    ///
    /// <para><b>양성 대조</b>: 축 1 주입 경로가 <b>정말로 살아 있는지</b>를 먼저 확인한다
    /// (사용자 숨김이 꺼진 상태에서 축 1만으로 숨었다가 풀리는지). 그 대조가 깨지면 그 뒤의
    /// "안 되살아났다"는 <b>주입이 아무 일도 안 했다</b>와 구별되지 않는다.</para>
    ///
    /// <para><b>시간 예산은 벽시계다</b>(CLAUDE.md) — 이 저장소의 배치모드 PlayMode는 2,000fps 이상으로
    /// 돌아서 프레임 수 기반 대기는 실제로 0.0x초밖에 안 될 수 있다.</para>
    /// </summary>
    public sealed class ManualHideUserAxisTests
    {
        private const string LogPrefix = "[사용자숨김-TEST]";

        /// <summary>관측용 폴링 주기(초). 짧게 잡아 <b>여러 주기</b>가 벽시계 예산 안에 들어가게 한다.</summary>
        private const float ObservePollInterval = 0.1f;

        /// <summary>실측 폴링을 지켜보는 벽시계 예산(초). <see cref="ObservePollInterval"/>의 10배 —
        /// 옛 코드였다면 첫 주기(0.1초)에 이미 Resume()이 났다.</summary>
        private const float RoundTripObserveSeconds = 1.0f;

        /// <summary>Update/LateUpdate가 한 바퀴 다 도는 데 필요한 여유(초). 표면마다 단계가 달라
        /// 한 프레임으로는 부족하다(FullscreenSuspendUiHidingTests와 같은 사정).</summary>
        private const float SettleSeconds = 0.2f;

        private StickmanAgent _agent;
        private StickConfig _config;
        private SettingsWindow _settings;
        private CharacterInfoWindow _info;

        private float _savedPollInterval;

        private static readonly FieldInfo FullscreenAxisField =
            typeof(StickmanAgent).GetField("_fullscreenAutoHide", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo ApplyDecisionMethod =
            typeof(StickmanAgent).GetMethod("ApplySuspendDecision", BindingFlags.Instance | BindingFlags.NonPublic);

        // ==================== 준비 / 정리 ====================

        /// <summary>
        /// ★★ 2026-09-02 <c>test-engineer</c> — 여기 있던 <b>백업/복원</b>은 <b>오염 보존기</b>였다.
        /// 걷어냈다. 되살리지 마라. (<c>FullscreenPanelRetreatTests</c>가 같은 날 먼저 걷어낸 것과
        /// <b>같은 코드</b>가 8개 픽스처에 남아 있었다.)
        ///
        /// <para><b>원래 근거가 사라졌다.</b> 옛 코드는 <c>OneTimeSetUp</c>에서 저장 파일을 통째로 읽어
        /// 두고 <c>OneTimeTearDown</c>에서 <b>그대로 다시 썼다</b>. 정당화는 <i>"저장 파일이 실제 앱의
        /// 것과 같은 경로"</i>였는데, 그 전제는 2026-08-31에 <c>GlobalPlayModeTestIsolation</c>이
        /// 경로를 임시 폴더로 옮기면서 <b>거짓이 됐다</b>.</para>
        ///
        /// <para><b>그리고 뜻이 정반대로 뒤집혔다.</b> 격리된 폴더에서 <c>_hadFile == true</c>는
        /// "개발자 파일이 있다"가 아니라 <b>"앞선 픽스처가 남긴 오염이 있다"</b>는 뜻이다. 옛 TearDown은
        /// 그 오염을 <b>다시 써서 되살렸고</b>, 같은 코드가 여러 픽스처에 있었으므로 오염이 스위트
        /// 전체를 타고 <b>세탁</b>됐다 — 어떤 정리도 그 다음 픽스처의 복원 한 줄에 무효화됐다.
        /// 2026-09-02 실측이 그 결과다: <c>c1-play</c>가 씬 로드 430회 중 "없음 161 → 불러옴 278"로
        /// 도중에 뒤집혔고 <c>스틱메이트 Lv.127</c>이 로그에 505회 찍혔다.</para>
        ///
        /// <para><b>대신 가드를 남긴다.</b> 격리가 꺼진 채로 이 픽스처가 돌면 씬 로드가 개발자의 실제
        /// 저장 파일을 읽고 쓴다. 그때는 조용히 진행하지 않고 <b>즉시 실패</b>한다.</para>
        /// </summary>
        [OneTimeSetUp]
        public void RequireIsolatedSaveFileAndStartClean()
        {
            Assert.IsTrue(CharacterSaveStore.IsRedirectedForTesting,
                "저장 경로가 격리되지 않았습니다 — GlobalPlayModeTestIsolation이 돌지 않았습니다. " +
                "이대로 진행하면 개발자의 실제 저장 파일을 읽고 씁니다(절대 불변 원칙 3).");
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
        }

        /// <summary>격리 폴더를 다음 픽스처에 <b>넘기지 않는다</b> — 이 픽스처가 만든 저장 파일을 지운다.
        /// 옛 <c>RestoreRealSaveFile</c>이 하던 "다시 쓰기"의 정확한 반대다(위 문단 참고).</summary>
        [OneTimeTearDown]
        public void ClearIsolatedSaveFile()
        {
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            // 순서가 중요하다: 사용자 숨김을 먼저 풀어야(다음 케이스가 숨은 상태를 물려받지 않게)
            // 그 다음 폴링 주기를 되돌릴 수 있다. config는 <b>배포 에셋</b>이라 반드시 원복한다.
            if (_agent != null) _agent.SetUserHidden(false, "테스트 정리");
            if (FullscreenAxisField != null && _agent != null) FullscreenAxisField.SetValue(_agent, false);
            if (_agent != null) ApplyDecisionMethod?.Invoke(_agent, null);
            if (_config != null) _config.fullscreenPollInterval = _savedPollInterval;

            _agent = null;
            _config = null;
            _settings = null;
            _info = null;
            AppSettingsModel.ResetForTesting();
            yield return null;
        }

        private IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에 StickmanAgent가 없습니다.");
            _settings = Object.FindFirstObjectByType<SettingsWindow>();
            _info = Object.FindFirstObjectByType<CharacterInfoWindow>();

            Assert.IsNotNull(FullscreenAxisField,
                $"{LogPrefix} StickmanAgent._fullscreenAutoHide 필드를 찾지 못했습니다 — 축 1의 이름이 " +
                "바뀌었다면 이 테스트의 주입 경로도 함께 고쳐야 합니다(조용히 0건이 되지 않게 여기서 멈춥니다).");
            Assert.IsNotNull(ApplyDecisionMethod,
                $"{LogPrefix} StickmanAgent.ApplySuspendDecision()을 찾지 못했습니다 — 두 축의 합성 지점이 " +
                "사라졌거나 이름이 바뀌었습니다.");

            _config = _agent.Config;
            Assert.IsNotNull(_config, $"{LogPrefix} StickConfig가 없습니다.");
            _savedPollInterval = _config.fullscreenPollInterval;
            _config.fullscreenPollInterval = ObservePollInterval;

            Assert.IsFalse(_agent.IsUserHidden,
                $"{LogPrefix} 새 씬인데 이미 사용자 숨김 상태입니다 — 이 상태는 <b>저장되지 않아야</b> " +
                "합니다(숨긴 채 껐다 켜면 톱니조차 숨어 마우스 진입점이 0이 됩니다).");
            Assert.IsFalse(_agent.IsSuspended, $"{LogPrefix} 새 씬인데 이미 Suspended 상태입니다.");

            yield return null;
        }

        private static IEnumerator Wait(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline) yield return null;
        }

        // ==================== 클릭 차단막 전수 조사 ====================
        //
        // 이름 규약은 FullscreenSuspendUiHidingTests와 <b>같은 것</b>을 쓴다(씬 루트의
        // "...Blocker" / "...ClickTarget"). 개별 표면을 손으로 적는 방식이 예전에 포스트잇 하나를
        // 통째로 놓쳤기 때문에, 여기서도 전수로 훑는다.

        private static bool IsClickBlockerName(string name) =>
            name.EndsWith("Blocker", System.StringComparison.Ordinal)
            || name.EndsWith("ClickTarget", System.StringComparison.Ordinal);

        private static int CountEnabledClickBlockers(out string names)
        {
            var all = Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var sb = new System.Text.StringBuilder();
            int n = 0;
            for (int i = 0; i < all.Length; i++)
            {
                Collider2D c = all[i];
                if (c == null || !IsClickBlockerName(c.gameObject.name)) continue;
                if (!c.enabled || !c.gameObject.activeInHierarchy) continue;
                n++;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(c.gameObject.name);
            }
            names = sb.ToString();
            return n;
        }

        // ==================== ① Suspend 경로를 실제로 타는가 ====================

        /// <summary>
        /// ★ <b>렌더러만 끄면 실패한다</b>. 옛 <c>SetCharacterVisibleNow</c>는 이 단언을 통과할 수 없다 —
        /// 그쪽은 창도 차단막도 건드리지 않으므로 <c>during</c>이 그대로 남는다.
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다()
        {
            yield return LoadScene();

            Assert.IsNotNull(_settings, $"{LogPrefix} 씬에 SettingsWindow가 없습니다.");
            // ★★ 2026-09-02 — 여기 있던 `_info.Open("테스트 준비")` 한 줄을 걷어냈다. 되살리지 마라.
            //
            // 그 줄은 **프로덕션 배타 규칙과 정면으로 모순**이었다. CharacterInfoWindow와
            // SettingsWindow는 둘 다 IExclusiveSurface이고, 양쪽 Open()이
            // ExclusiveSurfaces.CloseAllExcept(this, reason)를 부른다 — 즉 **정보창을 여는 순간
            // 설정창은 반드시 닫힌다**. 그런데 바로 다음 줄이 `_settings.IsOpen`을 요구했다.
            // 이 테스트는 프로덕션 버그를 잡은 것이 아니라 **태어날 때부터 빨갰다**.
            // (그 배타 동작은 의도된 것이고 Tests/PlayMode/InfoWindowExclusiveModalTests가 잠근다.)
            //
            // 그래서 배타 표면은 **하나만** 연다. 이 검사에 필요한 것은 "열린 창 + 켜진 차단막이
            // 최소 하나 있는 상태"이고 설정창 하나로 충분하다(아래 CountEnabledClickBlockers가
            // 개별 표면을 손으로 적지 않고 전수로 훑으므로, 표면을 더 띄워야 할 이유도 없다).
            _settings.Open("테스트 준비");
            yield return Wait(SettleSeconds);

            Assert.IsTrue(_settings.IsOpen && _settings.IsCanvasActive && _settings.IsClickBlockerEnabled,
                $"{LogPrefix} 준비 단계에서 설정창이 열리지 않았습니다 — 이 단언이 없으면 아래 " +
                "'전부 걷혔다'가 '애초에 아무것도 안 떠 있었다'로도 통과합니다.");

            // ★ 같은 실수가 다시 들어오면 여기서 먼저 잡는다 — 정보창이 열려 있으면 위 단언은
            //   배타 규칙 때문에 반드시 깨진다. 그때 "설정창이 안 열렸다"가 아니라 **진짜 이유**를 말한다.
            Assert.IsTrue(_info == null || !_info.IsOpen,
                $"{LogPrefix} 준비 단계에서 정보창이 열려 있습니다 — 배타 규칙(ExclusiveSurfaces)상 " +
                "그 순간 설정창은 닫힙니다. 배타 표면을 둘 이상 띄우는 준비는 프로덕션에서 " +
                "재현 불가능한 상태이며, 그렇게 짜면 이 테스트는 태어날 때부터 빨갛습니다.");

            int before = CountEnabledClickBlockers(out string beforeNames);
            Assert.GreaterOrEqual(before, 1,
                $"{LogPrefix} 켜진 차단막이 {before}개입니다({beforeNames}) — 이름 규약이 바뀌었거나 " +
                "표면이 안 떠 있습니다.");
            Debug.Log($"{LogPrefix} 준비 완료 — 켜진 차단막 {before}개({beforeNames})에서 사용자 숨김을 겁니다.");

            _agent.SetUserHidden(true, "테스트");
            yield return Wait(SettleSeconds);

            Assert.IsTrue(_agent.IsSuspended,
                $"{LogPrefix} 사용자 숨김을 걸었는데 IsSuspended가 false입니다 — Suspend() 경로를 타지 " +
                "않았다는 뜻이고, 그러면 창도 차단막도 그대로 남습니다.");
            Assert.IsFalse(_settings.IsOpen, $"{LogPrefix} 설정창이 닫히지 않았습니다.");
            Assert.IsFalse(_settings.IsCanvasActive, $"{LogPrefix} 설정창 캔버스가 켜진 채 남아 있습니다.");
            Assert.IsFalse(_settings.IsClickBlockerEnabled,
                $"{LogPrefix} 설정창 720×560 차단막이 살아 있습니다 — 안 보이는데 그 사각형의 클릭만 " +
                "먹는 최악의 형태이고, 발표 화면에 '클릭이 안 되는 구멍'이 남습니다.");

            int during = CountEnabledClickBlockers(out string duringNames);
            Assert.AreEqual(0, during,
                $"{LogPrefix} 사용자 숨김 중인데 클릭 차단막 {during}개가 아직 켜져 있습니다: {duringNames}. " +
                "렌더러만 끄는 옛 경로(SetCharacterVisibleNow)로 되돌아간 것입니다.");

            Debug.Log($"{LogPrefix} 숨김 확인 — 차단막 {before}개가 전부 걷혔습니다.");
        }

        // ==================== ② ★ 네거티브 컨트롤 ====================

        /// <summary>
        /// ★★ <b>이 라운드의 핵심</b>. 숨긴 뒤 전체화면 앱을 왕복해도 되살아나면 안 된다.
        /// 옛 코드는 정확히 여기서 되살아났다.
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 네거티브컨트롤_사용자숨김_중_전체화면이_왕복해도_되살아나지_않는다()
        {
            yield return LoadScene();

            // ── 양성 대조 먼저 ──────────────────────────────────────────────
            // 축 1 주입이 <b>정말로 살아 있는가</b>. 이게 깨지면 아래 "안 되살아났다"는
            // "주입이 아무 일도 안 했다"와 구별되지 않는다.
            FullscreenAxisField.SetValue(_agent, true);
            ApplyDecisionMethod.Invoke(_agent, null);
            Assert.IsTrue(_agent.IsSuspended,
                $"{LogPrefix} 양성 대조 실패 — 축 1만 켰는데 숨지 않았습니다. 주입 경로가 죽어 있으므로 " +
                "이 케이스의 이후 판정은 전부 무효입니다.");

            FullscreenAxisField.SetValue(_agent, false);
            ApplyDecisionMethod.Invoke(_agent, null);
            Assert.IsFalse(_agent.IsSuspended,
                $"{LogPrefix} 양성 대조 실패 — 축 1을 껐는데 계속 숨어 있습니다(사용자 숨김은 아직 꺼져 " +
                "있으므로 반드시 풀려야 합니다).");
            Debug.Log($"{LogPrefix} 양성 대조 통과 — 축 1 주입이 실제로 Suspend/Resume을 움직입니다.");

            // ── 본 검사 ────────────────────────────────────────────────────
            _agent.SetUserHidden(true, "테스트");
            yield return null;
            Assert.IsTrue(_agent.IsSuspended, $"{LogPrefix} 사용자 숨김이 걸리지 않았습니다.");

            // (A) 실측 폴링 — 에이전트 자신의 TickFullscreenSuspend를 여러 주기 돌린다.
            //     에디터의 IsFullscreenAppActive()는 항상 false라, 옛 판정식
            //     `if (!fullscreenActive && _isSuspended) Resume();`이 <b>첫 주기</b>에 발동했다.
            yield return Wait(RoundTripObserveSeconds);
            Assert.IsTrue(_agent.IsSuspended,
                $"{LogPrefix} 실측 폴링 {RoundTripObserveSeconds:F1}초(주기 {ObservePollInterval:F1}초 " +
                $"= 약 {RoundTripObserveSeconds / ObservePollInterval:F0}회) 만에 캐릭터가 되살아났습니다. " +
                "전체화면 판정 한 줄이 사용자 숨김까지 좌우하고 있습니다.");
            Assert.IsTrue(_agent.IsUserHidden,
                $"{LogPrefix} 폴링이 사용자 숨김 축 자체를 꺼 버렸습니다 — 두 축이 섞여 있습니다.");

            // (B) 전체화면 "켜졌다 꺼졌다"의 나머지 반쪽까지 재현.
            FullscreenAxisField.SetValue(_agent, true);
            ApplyDecisionMethod.Invoke(_agent, null);
            Assert.IsTrue(_agent.IsSuspended, $"{LogPrefix} 왕복 중(전체화면 ON)에 숨김이 풀렸습니다.");

            FullscreenAxisField.SetValue(_agent, false);
            ApplyDecisionMethod.Invoke(_agent, null);
            yield return Wait(SettleSeconds);
            Assert.IsTrue(_agent.IsSuspended,
                $"{LogPrefix} ★ 전체화면이 지나가자 캐릭터가 되살아났습니다 — 사용자는 아직 " +
                "'숨겨 둬'라고 말한 상태입니다. 이것이 이 라운드가 고치려던 결함 그 자체입니다.");

            Debug.Log($"{LogPrefix} 네거티브 컨트롤 통과 — 전체화면 왕복(실측 폴링 + 주입 양쪽)에도 " +
                "사용자 숨김이 유지됩니다.");
        }

        // ==================== ③ 두 축의 독립 ====================

        /// <summary>
        /// ★ 설정창 [일반]의 "전체화면 게임 감지 시 자동 숨김"을 <b>끄는</b> 순간 사용자 숨김까지 함께
        /// 풀리면 안 된다. 두 축을 <c>(fullscreen || userHidden) &amp;&amp; AutoHideOnFullscreen</c>처럼
        /// 한 조건식에 얹으면 정확히 그렇게 된다 — 화면공유 중에 토글 하나가 캐릭터를 발표 화면으로
        /// 되돌리는 경로다.
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 전체화면_자동숨김_토글을_꺼도_사용자숨김은_유지된다()
        {
            yield return LoadScene();

            Assert.IsTrue(AppSettingsModel.AutoHideOnFullscreen,
                $"{LogPrefix} 자동 숨김이 처음부터 꺼져 있습니다 — 이 케이스는 '켜져 있던 것을 끄는' " +
                "전이를 재는 것이라 시작 상태가 켬이어야 합니다.");

            _agent.SetUserHidden(true, "테스트");
            yield return null;
            Assert.IsTrue(_agent.IsSuspended, $"{LogPrefix} 사용자 숨김이 걸리지 않았습니다.");

            AppSettingsModel.SetAutoHideOnFullscreen(false);
            yield return Wait(RoundTripObserveSeconds);   // 폴링이 여러 번 돌 시간.

            Assert.IsTrue(_agent.IsSuspended,
                $"{LogPrefix} 자동 숨김 토글을 끄자 사용자 숨김까지 풀렸습니다 — 두 축이 한 조건식에 " +
                "묶여 있습니다. 실패 비용의 방향이 반대라(자동 숨김은 오탐이 크고, 사용자 숨김은 " +
                "본인이 눌렀으니 오탐이 0) 묶으면 안 됩니다.");
            Assert.IsTrue(_agent.IsUserHidden, $"{LogPrefix} 사용자 숨김 축 자체가 꺼졌습니다.");

            Debug.Log($"{LogPrefix} 축 독립 확인 — 자동 숨김을 꺼도 사용자 숨김은 그대로입니다.");
        }

        // ==================== ④ 탈출구(같은 키로 되돌아온다) ====================

        /// <summary>
        /// ★ <b>탈출구</b>. 숨김은 <b>토글</b>이라 같은 조작이 그대로 복귀 경로다. 이것이 유일한
        /// 탈출구이므로(숨는 동안 톱니·부채꼴·창이 전부 사라진다) 여기가 깨지면 사용자는 강제 종료
        /// 외에는 캐릭터를 되찾을 수 없다.
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 같은_토글을_다시_누르면_캐릭터와_톱니가_돌아온다()
        {
            yield return LoadScene();

            var gear = Object.FindFirstObjectByType<InfoGearIconWidget>(FindObjectsInactive.Include);
            Assert.IsNotNull(gear, $"{LogPrefix} 씬에 InfoGearIconWidget이 없습니다 — 톱니는 숨김이 풀렸을 때 " +
                "사용자가 처음 되찾는 마우스 진입점이라 이 케이스의 관심사입니다.");
            yield return Wait(SettleSeconds);
            Assert.IsTrue(gear.IsIconVisible, $"{LogPrefix} 준비 단계에서 톱니가 이미 꺼져 있습니다.");

            bool hidden = _agent.ToggleUserHidden("테스트 1회차");
            Assert.IsTrue(hidden, $"{LogPrefix} 첫 토글이 숨김으로 가지 않았습니다.");
            yield return Wait(SettleSeconds);
            Assert.IsTrue(_agent.IsSuspended, $"{LogPrefix} 첫 토글 후 숨지 않았습니다.");
            Assert.IsFalse(gear.IsIconVisible,
                $"{LogPrefix} 숨겼는데 톱니가 남아 있습니다 — 화면공유에 그대로 찍힙니다.");

            bool shown = _agent.ToggleUserHidden("테스트 2회차");
            Assert.IsFalse(shown, $"{LogPrefix} 두 번째 토글이 숨김을 풀지 않았습니다 — 탈출구가 없습니다.");
            yield return Wait(SettleSeconds);

            Assert.IsFalse(_agent.IsSuspended,
                $"{LogPrefix} 같은 토글을 다시 눌렀는데 계속 숨어 있습니다 — 이 상태에서 사용자에게 남는 " +
                "수단은 강제 종료뿐입니다.");
            Assert.IsTrue(gear.IsIconVisible,
                $"{LogPrefix} 숨김을 풀었는데 톱니가 돌아오지 않았습니다 — 마우스 진입점이 영구 실종됩니다.");

            Debug.Log($"{LogPrefix} 탈출구 확인 — 같은 토글 2회로 숨김/복귀가 대칭입니다.");
        }
    }
}
