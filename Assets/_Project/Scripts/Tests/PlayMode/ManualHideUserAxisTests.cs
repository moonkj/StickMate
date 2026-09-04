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
    /// ★★★ <b>2026-09-03 — 범위가 「캐릭터만」으로 좁아졌다</b>(사용자 확정). 위 문단의
    /// <i>"열린 창과 그 클릭 차단막"</i>까지 걷는 계약은 <b>폐기됐다</b> — 그 계약이 사용자를 가뒀다:
    /// <i>"설정에서 숨기기버튼 누르니까 전부 다 없어져버려서 다시 나오게 할 방법이 없어"</i> →
    /// <i>"메뉴버튼은 보여야지"</i> → <i>"캐릭만 가리고"</i>. 지금 계약은
    /// <b>캐릭터(와 말풍선·이펙트·장비·펫)만 가리고 톱니·열린 창·부채꼴은 남긴다</b>이다.
    ///
    /// 그래서 이 스위트는 네 가지를 <b>서로 다른 방법으로</b> 잰다:
    /// <list type="number">
    ///   <item><b>캐릭터는 사라지고 표면은 남는가</b> — 캐릭터는 <b>렌더러 실물</b>, 표면은
    ///         <b>씬의 클릭 차단막 전수</b>로 잰다. 한쪽만 재면 둘 다 놓친다(옛 계약도, 아무 일도
    ///         안 한 구현도 통과한다).</item>
    ///   <item><b>★ 네거티브 컨트롤 — 전체화면 왕복에도 되살아나지 않는가</b>. 이 라운드의 핵심이다.
    ///         ①<b>실측 폴링</b>(에이전트 자신의 <c>TickFullscreenSuspend</c>를 벽시계로 여러 주기
    ///         돌린다. 에디터의 <c>NullPlatformWindowService.IsFullscreenAppActive()</c>는 항상
    ///         false라, <b>옛 코드였다면 첫 폴링에서 Resume()</b>이 났다)와
    ///         ②<b>축 1 주입</b>(전체화면 감지의 true→false 반쪽까지 재현)을 <b>둘 다</b> 쓴다.</item>
    ///   <item><b>두 축이 하나로 묶이지 않았는가</b> — 설정창의 "전체화면 자동 숨김"을 꺼도
    ///         사용자 숨김은 유지되어야 한다. 이 둘을 <c>||</c> 한 줄로 합치면 여기서 깨진다.</item>
    ///   <item><b>★ 축 분리(2026-09-03)</b> — <b>같은 관측(톱니)</b>으로 세 조합을 잰다:
    ///         축 2 단독=남는다 / 축 1 단독=사라진다(원칙 2) / 둘 다=사라진다(원칙 2가 이긴다).
    ///         셋째 칸이 없으면 "표면 채널 = <c>!_userHidden</c>" 같은 구현이 통과한다.</item>
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

        /// <summary>
        /// ★★★ <b>축 1 주입을 관측하는 동안 폴링을 얼린다</b>(초). 2026-09-03 실측으로 발견한 함정이다.
        ///
        /// <para>이 픽스처의 기본 주기는 <b>0.1초</b>인데(②의 «실측 폴링» 케이스가 여러 주기를 돌려야
        /// 해서), 에디터의 <c>NullPlatformWindowService</c>는 등급이 항상 <c>None</c>이라
        /// <c>TickFullscreenSuspend</c>가 <b>주입한 <c>_fullscreenAutoHide</c>를 0.1초 만에 false로
        /// 되돌린다</b>. 그래서 «주입 → 0.2초 대기 → 관측»으로 짜면 관측 시점에 축 1은 이미 꺼져 있고,
        /// 테스트는 <b>프로덕션이 멀쩡한데 빨간불</b>을 낸다(실제로 첫 실행이 그랬다).</para>
        ///
        /// <para>②가 <b>대기 없이</b> 주입 직후 단언하는 것도 같은 이유다. 이 케이스는 톱니의
        /// <c>LateUpdate</c>가 돌아야 결과가 보이므로 대기가 필요하고, 그래서 대기 대신
        /// <b>폴링을 얼린다</b>(<c>FullscreenPanelRetreatTests</c>가 축 3에 쓰는 것과 같은 처방).
        /// <c>UnityTearDown</c>이 배포 에셋 값을 원복한다.</para></summary>
        private const float FreezePollInterval = 9999f;

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
        /// ★★★ <b>2026-09-03 — 이 케이스가 정확히 뒤집혔다</b>(사용자 확정).
        ///
        /// <para>옛 이름은 <c>사용자숨김은_열린_창과_클릭차단막까지_함께_걷는다</c>였고, 그 계약이
        /// 바로 사용자를 가둔 원인이다 — <i>"설정에서 숨기기버튼 누르니까 전부 다 없어져버려서
        /// 다시 나오게 할 방법이 없어"</i> → <i>"메뉴버튼은 보여야지"</i> → <i>"캐릭만 가리고"</i>.</para>
        ///
        /// <para>그래서 지금 재는 것은 <b>「캐릭터는 사라졌고, 되돌릴 표면은 남았다」</b>는 두 사실이
        /// <b>동시에</b> 참인가이다. 한쪽만 재면 둘 다 놓친다: 캐릭터만 재면 옛 계약도 통과하고,
        /// 표면만 재면 <b>아무 일도 안 일어난 것</b>도 통과한다.</para>
        ///
        /// <para><b>측정은 플래그가 아니라 실물로 한다</b> — 캐릭터는 <b>렌더러 실제 enabled</b>,
        /// 표면은 <b>씬의 클릭 차단막 전수</b>다(옛 파일의 <c>CountEnabledClickBlockers</c>를 그대로 쓴다).</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 사용자숨김은_캐릭터만_가리고_열린_창과_차단막은_남긴다()
        {
            yield return LoadScene();

            Assert.IsNotNull(_settings, $"{LogPrefix} 씬에 SettingsWindow가 없습니다.");
            // ★★ 2026-09-02 — 여기 있던 `_info.Open("테스트 준비")` 한 줄을 걷어냈다. 되살리지 마라.
            //    CharacterInfoWindow와 SettingsWindow는 둘 다 IExclusiveSurface라 정보창을 여는 순간
            //    설정창이 반드시 닫힌다(ExclusiveSurfaces). 그 줄이 있던 동안 이 테스트는
            //    프로덕션 버그가 아니라 **자기 준비 단계** 때문에 태어날 때부터 빨갰다.
            _settings.Open("테스트 준비");
            yield return Wait(SettleSeconds);

            Assert.IsTrue(_settings.IsOpen && _settings.IsCanvasActive && _settings.IsClickBlockerEnabled,
                $"{LogPrefix} 준비 단계에서 설정창이 열리지 않았습니다 — 이 단언이 없으면 아래 " +
                "'그대로 남았다'가 '애초에 아무것도 안 떠 있었다'로도 통과합니다.");
            Assert.IsTrue(_info == null || !_info.IsOpen,
                $"{LogPrefix} 준비 단계에서 정보창이 열려 있습니다 — 배타 규칙(ExclusiveSurfaces)상 " +
                "그 순간 설정창은 닫힙니다.");

            int blockersBefore = CountEnabledClickBlockers(out string beforeNames);
            Assert.GreaterOrEqual(blockersBefore, 1,
                $"{LogPrefix} 켜진 차단막이 {blockersBefore}개입니다({beforeNames}) — 이름 규약이 바뀌었거나 " +
                "표면이 안 떠 있습니다.");
            Renderer[] inkSnapshot = SnapshotEnabledCharacterRenderers();
            int inkBefore = inkSnapshot.Length;
            Assert.Greater(inkBefore, 0,
                $"{LogPrefix} 숨기기 전에 캐릭터 렌더러가 이미 0개입니다 — 전제 불성립입니다. " +
                "이 단언이 없으면 아래 '캐릭터가 사라졌다'가 항상 참이 됩니다.");
            Debug.Log($"{LogPrefix} 준비 완료 — 차단막 {blockersBefore}개({beforeNames}) / " +
                $"캐릭터 렌더러 {inkBefore}개에서 사용자 숨김을 겁니다.");

            _agent.SetUserHidden(true, "테스트");
            yield return Wait(SettleSeconds);

            // ① 캐릭터는 사라졌는가 — 플래그와 실물 둘 다.
            Assert.IsTrue(_agent.IsSuspended,
                $"{LogPrefix} 사용자 숨김을 걸었는데 IsSuspended가 false입니다 — Suspend() 경로를 타지 " +
                "않았다는 뜻이고, 그러면 물리도 상태도 그대로 돕니다.");
            Assert.AreEqual(0, CountStillEnabled(inkSnapshot),
                $"{LogPrefix} 숨겼는데 아까 켜져 있던 캐릭터 렌더러 {inkBefore}개 중 일부가 아직 " +
                "켜져 있습니다 — 화면공유에 그대로 찍힙니다.");

            // ② 되돌릴 표면은 남았는가 — 이 라운드가 고친 바로 그것.
            Assert.IsFalse(_agent.HidesScreenSurfaces,
                $"{LogPrefix} 사용자 명시 숨김 단독인데 표면 채널이 참입니다 — 축이 다시 합쳐졌습니다.");
            Assert.IsFalse(_agent.ArePanelsSuppressed,
                $"{LogPrefix} 사용자 명시 숨김인데 등급 1 채널이 참입니다 — 열린 창이 전부 걷힙니다.");
            Assert.IsTrue(_settings.IsOpen,
                $"{LogPrefix} ★ 설정창이 닫혔습니다 — [숨기기] 바로 옆의 [보이기]가 사라졌다는 뜻이고, " +
                "그것이 2026-09-03 신고(\"다시 나오게 할 방법이 없어\") 그 자체입니다.");
            Assert.IsTrue(_settings.IsCanvasActive, $"{LogPrefix} 설정창 캔버스가 꺼졌습니다.");
            Assert.IsTrue(_settings.IsClickBlockerEnabled,
                $"{LogPrefix} 설정창 차단막이 꺼졌습니다 — 창은 보이는데 [보이기] 버튼이 클릭을 " +
                "받지 못하는 상태이고, 그건 없는 것과 같습니다.");

            int blockersDuring = CountEnabledClickBlockers(out string duringNames);
            Assert.AreEqual(blockersBefore, blockersDuring,
                $"{LogPrefix} 사용자 숨김 전후로 켜진 차단막 개수가 달라졌습니다: " +
                $"{blockersBefore}개({beforeNames}) → {blockersDuring}개({duringNames}). " +
                "이 축은 표면을 한 개도 걷지 않아야 합니다(사용자 확정 \"캐릭만 가리고\").");

            Debug.Log($"{LogPrefix} 확인 — 캐릭터 렌더러 {inkBefore}→0, 차단막 {blockersBefore}개 유지.");
        }

        /// <summary>지금 <b>켜져 있는</b> 캐릭터 렌더러들(에이전트 자식 전체 = 몸 + 액세서리 + 펫 + FX).
        ///
        /// <para>개수가 아니라 <b>인스턴스 목록</b>을 뜨는 이유는 <c>FullscreenPanelRetreatTests</c>와 같다:
        /// 관측 사이에 이펙트/펫이 새로 생기거나 사라지면 개수 비교는 그 무관한 변화에 흔들려
        /// <b>실패가 실패를 뜻하지 않게</b> 된다. 우리가 재려는 것은 "그때 켜져 있던 <b>바로 그
        /// 렌더러들</b>이 지금도 켜져 있는가"다.</para></summary>
        private Renderer[] SnapshotEnabledCharacterRenderers()
        {
            var all = _agent.GetComponentsInChildren<Renderer>(true);
            var list = new System.Collections.Generic.List<Renderer>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].enabled) list.Add(all[i]);
            }
            return list.ToArray();
        }

        /// <summary>스냅샷의 렌더러 중 <b>아직도</b> 켜져 있는 개수.</summary>
        private static int CountStillEnabled(Renderer[] snapshot)
        {
            int n = 0;
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (snapshot[i] != null && snapshot[i].enabled) n++;
            }
            return n;
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
        /// ★ <b>탈출구</b>. 숨김은 <b>토글</b>이라 같은 조작이 그대로 복귀 경로다.
        ///
        /// <para>★★★ <b>2026-09-03 — 톱니에 대한 기대가 뒤집혔다</b>. 옛 단언은
        /// <i>"숨겼는데 톱니가 남아 있습니다 — 화면공유에 그대로 찍힙니다"</i>였다. 사용자가 그 대가를
        /// <b>알고</b> 반대쪽을 골랐다: <i>"메뉴버튼은 보여야지"</i>. 톱니는 숨기는 동안에도 남는다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 같은_토글을_다시_누르면_캐릭터가_돌아오고_톱니는_내내_남는다()
        {
            yield return LoadScene();

            var gear = Object.FindFirstObjectByType<InfoGearIconWidget>(FindObjectsInactive.Include);
            Assert.IsNotNull(gear, $"{LogPrefix} 씬에 InfoGearIconWidget이 없습니다 — 톱니는 숨김 중에도 " +
                "남아야 하는 마우스 진입점이라 이 케이스의 관심사입니다.");
            yield return Wait(SettleSeconds);
            Assert.IsTrue(gear.IsIconVisible, $"{LogPrefix} 준비 단계에서 톱니가 이미 꺼져 있습니다.");

            bool hidden = _agent.ToggleUserHidden("테스트 1회차");
            Assert.IsTrue(hidden, $"{LogPrefix} 첫 토글이 숨김으로 가지 않았습니다.");
            yield return Wait(SettleSeconds);
            Assert.IsTrue(_agent.IsSuspended, $"{LogPrefix} 첫 토글 후 숨지 않았습니다.");
            Assert.IsTrue(gear.IsIconVisible,
                $"{LogPrefix} ★ 숨겼더니 톱니까지 사라졌습니다 — 2026-09-03 사용자 확정 " +
                "(\"메뉴버튼은 보여야지\")의 회귀이고, 이 상태가 바로 사용자를 가둔 그 화면입니다.");

            bool shown = _agent.ToggleUserHidden("테스트 2회차");
            Assert.IsFalse(shown, $"{LogPrefix} 두 번째 토글이 숨김을 풀지 않았습니다 — 탈출구가 없습니다.");
            yield return Wait(SettleSeconds);

            Assert.IsFalse(_agent.IsSuspended,
                $"{LogPrefix} 같은 토글을 다시 눌렀는데 계속 숨어 있습니다.");
            Assert.IsTrue(gear.IsIconVisible,
                $"{LogPrefix} 숨김을 풀었는데 톱니가 없습니다 — 마우스 진입점이 영구 실종됩니다.");

            Debug.Log($"{LogPrefix} 탈출구 확인 — 토글 2회로 캐릭터가 왕복하는 동안 톱니는 내내 남았습니다.");
        }

        // ==================== ⑤ ★★★ 축 분리 — 이 라운드의 핵심 산출물 ====================

        /// <summary>
        /// ★★★ <b>두 축이 같은 코드로 무너지지 않는가</b>(리더 지시 2026-09-03).
        ///
        /// ============================================================================
        /// 무엇을 재는가 — <b>같은 관측(톱니)으로 세 조합</b>
        /// ============================================================================
        /// <list type="table">
        ///   <item><term>축 2 단독</term><description>사용자 명시 숨김 → 톱니 <b>남는다</b>
        ///     (사용자 확정 "캐릭만 가리고").</description></item>
        ///   <item><term>축 1 단독</term><description>전체화면 <b>게임</b> 감지 → 톱니 <b>사라진다</b>
        ///     (절대 불변 원칙 2. 게임 위에 톱니가 남으면 그 자체가 침해다).</description></item>
        ///   <item><term>둘 다</term><description>원칙 2가 이긴다 → 톱니 <b>사라진다</b>. 이 칸이 없으면
        ///     "표면 채널 = <c>!_userHidden</c>"처럼 <b>둘 다일 때 톱니가 게임 위에 남는</b> 구현도
        ///     앞의 두 칸만으로 통과한다.</description></item>
        /// </list>
        ///
        /// <para><b>같은 척도로 세 번 잰다</b>: 축마다 다른 것을 재면 "이쪽은 되고 저쪽은 안 된다"가
        /// 측정 차이인지 동작 차이인지 구분되지 않는다. 여기서는 셋 다 <c>gear.IsIconVisible</c> 하나다.</para>
        ///
        /// <para>축 1 주입은 이 픽스처가 이미 쓰는 경로 그대로다(<c>_fullscreenAutoHide</c> +
        /// <c>ApplySuspendDecision()</c>) — 위 ②의 <b>양성 대조</b>가 그 주입이 실제로 Suspend를
        /// 움직인다는 것을 같은 픽스처 안에서 증명한다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 축분리_톱니는_사용자숨김에서_남고_전체화면_게임에서는_사라진다()
        {
            yield return LoadScene();

            var gear = Object.FindFirstObjectByType<InfoGearIconWidget>(FindObjectsInactive.Include);
            Assert.IsNotNull(gear, $"{LogPrefix} 씬에 InfoGearIconWidget이 없습니다.");

            // ★ 폴링을 얼린다(FreezePollInterval 문서 참고) — 얼리지 않으면 아래 축 1 주입이
            //   0.1초 만에 에디터의 등급 None으로 덮여, 이 테스트가 프로덕션과 무관하게 빨개진다.
            _config.fullscreenPollInterval = FreezePollInterval;

            yield return Wait(SettleSeconds);
            Assert.IsTrue(gear.IsIconVisible, $"{LogPrefix} 준비 단계에서 톱니가 이미 꺼져 있습니다 — " +
                "이 단언이 없으면 아래 '사라졌다'가 '원래 없었다'로도 통과합니다.");

            // ── 축 2 단독 ────────────────────────────────────────────────
            _agent.SetUserHidden(true, "축 분리 — 축 2 단독");
            yield return Wait(SettleSeconds);
            Assert.IsTrue(_agent.IsSuspended, $"{LogPrefix} 축 2가 Suspend로 이어지지 않았습니다.");
            Assert.IsFalse(_agent.HidesScreenSurfaces,
                $"{LogPrefix} 축 2 단독인데 표면 채널이 참입니다 — 두 축이 합쳐졌습니다.");
            Assert.IsTrue(gear.IsIconVisible,
                $"{LogPrefix} ★ 축 2에서 톱니가 사라졌습니다 — 사용자 확정 \"메뉴버튼은 보여야지\"의 회귀.");

            // ── 둘 다 ────────────────────────────────────────────────────
            //    순서에 의미가 있다: 축 2가 켜진 <b>위에</b> 축 1을 얹는다. 원칙 2가 이겨야 한다.
            FullscreenAxisField.SetValue(_agent, true);
            ApplyDecisionMethod.Invoke(_agent, null);
            yield return Wait(SettleSeconds);
            Assert.IsTrue((bool)FullscreenAxisField.GetValue(_agent),
                $"{LogPrefix} 축 1 주입이 관측 전에 폴링에 덮였습니다 — 폴링을 얼리는 줄이 사라졌거나 " +
                "값이 낮아졌습니다. 이 단언이 없으면 아래 판정이 '주입이 아무 일도 안 했다'와 " +
                "구별되지 않습니다(2026-09-03 실제로 이 형태로 한 번 빨개졌다).");
            Assert.IsTrue(_agent.HidesScreenSurfaces,
                $"{LogPrefix} 축 1이 켜졌는데 표면 채널이 거짓입니다 — 사용자 숨김이 원칙 2를 " +
                "덮어썼습니다(실패 비용이 가장 큰 방향입니다).");
            Assert.IsFalse(gear.IsIconVisible,
                $"{LogPrefix} ★ 전체화면 게임 위에 톱니가 남았습니다 — 절대 불변 원칙 2 위반입니다. " +
                "사용자가 직접 숨긴 상태에서 게임이 켜졌을 때가 이 결함이 나는 조합입니다.");

            // ── 축 1 단독 ────────────────────────────────────────────────
            _agent.SetUserHidden(false, "축 분리 — 축 1 단독");
            yield return Wait(SettleSeconds);
            Assert.IsTrue(_agent.IsSuspended,
                $"{LogPrefix} 축 2를 풀었더니 축 1까지 함께 풀렸습니다 — 두 축이 묶여 있습니다.");
            Assert.IsTrue(_agent.HidesScreenSurfaces, $"{LogPrefix} 축 1 단독인데 표면 채널이 거짓입니다.");
            Assert.IsFalse(gear.IsIconVisible,
                $"{LogPrefix} ★ 축 1(전체화면 게임 감지) 단독에서 톱니가 남았습니다 — 이 라운드는 " +
                "그 축을 한 비트도 바꾸지 않기로 한 변경입니다(원칙 2).");

            // ── 원상복귀(네거티브 컨트롤) ────────────────────────────────
            FullscreenAxisField.SetValue(_agent, false);
            ApplyDecisionMethod.Invoke(_agent, null);
            yield return Wait(SettleSeconds);
            Assert.IsFalse(_agent.IsSuspended, $"{LogPrefix} 두 축을 다 껐는데 계속 숨어 있습니다.");
            Assert.IsTrue(gear.IsIconVisible,
                $"{LogPrefix} 두 축을 다 껐는데 톱니가 돌아오지 않았습니다 — 위의 '사라졌다' 관측이 " +
                "되돌릴 수 없는 파괴였다는 뜻이라, 앞선 단언들의 의미가 달라집니다.");

            Debug.Log($"{LogPrefix} 축 분리 확인 — 톱니: 축2 단독=보임 / 둘 다=숨김 / 축1 단독=숨김 / 없음=보임.");
        }

        // ==================== ⑥ ★★★ 숨은 동안 캐릭터 연출은 못 시킨다 ====================

        /// <summary>
        /// ★★★ <b>2026-09-03 — 이 라운드가 만든 회귀를 막는 자리.</b>
        ///
        /// <para>사용자 명시 숨김이 «캐릭터만» 가리도록 바뀌면서 행동 명령창이 <b>남았다</b>. 그건
        /// 사용자 확정이지만, 그 결과로 <b>보이지 않는 캐릭터에게 연출을 시키는 마우스 경로</b>가
        /// 열렸다. <c>coder</c> 배치모드 프로브가 실측했다 — 숨김 상태에서 그라피티가
        /// <c>started=True</c>로 발동했고 상태가 <c>Graffiti</c>로 전이했다.</para>
        ///
        /// <para><b>소스가 아니라 동작으로 잰다</b>: 배선 여부는 EditMode
        /// (<c>HiddenCharacterCommandGateAuditTests</c>)가 보고, 여기서는 <b>씬에 조립된 실물</b>이
        /// 실제로 막히는지를 «상태가 전이하지 않았다»로 확인한다. 프로브가 잡은 것과 <b>같은 관측</b>이다.</para>
        ///
        /// <para><b>탈출구는 열려 있어야 한다</b>는 반대쪽 절반을 같은 케이스에 넣는다 —
        /// 전부 막아 버리는 구현도 앞 절반만으로는 통과하고, 그건 이 라운드가 고친 «갇힘»의 재발이다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 숨은_동안_캐릭터_연출은_막히고_탈출구는_열려_있다()
        {
            yield return LoadScene();

            var popover = Object.FindFirstObjectByType<ActionCommandPopover>(FindObjectsInactive.Include);
            var graffiti = Object.FindFirstObjectByType<GraffitiDirector>(FindObjectsInactive.Include);
            var runaway = Object.FindFirstObjectByType<RunawayDirector>(FindObjectsInactive.Include);
            Assert.IsNotNull(popover, $"{LogPrefix} 씬에 ActionCommandPopover가 없습니다.");
            Assert.IsNotNull(graffiti, $"{LogPrefix} 씬에 GraffitiDirector가 없습니다.");
            Assert.IsNotNull(runaway, $"{LogPrefix} 씬에 RunawayDirector가 없습니다.");
            yield return Wait(SettleSeconds);

            // ── 양성 대조 먼저: 숨기기 전에는 실제로 «가능»한 명령이 있는가 ──────────
            //    이게 없으면 아래 "전부 막혔다"가 "원래 다 막혀 있었다"로도 통과한다.
            Assert.IsTrue(graffiti.GetAvailability().IsReady,
                $"{LogPrefix} 숨기기 전에 그라피티가 이미 불가입니다({graffiti.GetAvailability().Reason}) — " +
                "이 케이스의 전제가 성립하지 않습니다. 프로브 실측에서는 이 상태가 Ready였습니다.");

            _agent.SetUserHidden(true, "연출 차단 검사");
            yield return Wait(SettleSeconds);

            // ── ① 행동 명령창 5칸이 전부 «숨어 있어요»로 회색이 되는가 ────────────
            //    사유 문자열은 <b>베끼지 않고</b> 프로덕션 상수를 참조한다(design-narrative가 글자를
            //    바꾸는 날 이 테스트가 조용히 초록으로 남지 않게).
            for (int i = 0; i < ActionCommandPopover.CommandCount; i++)
            {
                var command = (ActionCommandPopover.Command)i;
                CommandAvailability availability = popover.GetAvailability(command);
                Assert.IsFalse(availability.IsReady,
                    $"{LogPrefix} 숨은 상태인데 [{command}]가 실행 가능입니다 — 보이지 않는 캐릭터가 " +
                    "연출을 합니다(원칙 1).");
                Assert.AreEqual(HiddenCharacterCommandGate.HiddenReason, availability.Reason,
                    $"{LogPrefix} [{command}]의 불가 사유가 숨김이 아니라 \"{availability.Reason}\"입니다 — " +
                    "다른 이유로 우연히 막힌 것이라면 그 이유가 사라지는 순간 다시 뚫립니다.");
            }

            // ── ② 실제 발동 경로도 막히는가(프로브가 잡은 그 관측) ────────────────
            StickmanStateId before = _agent.Blackboard.Machine.CurrentStateId;
            Assert.IsFalse(graffiti.ForceTriggerNow("숨김 중 강제 시도"),
                $"{LogPrefix} ★ 숨은 상태에서 그라피티가 발동했습니다 — 2026-09-03 프로브가 실측한 " +
                "결함 그대로입니다(started=True, 상태 Graffiti).");
            runaway.ForceTriggerNow("숨김 중 강제 시도");
            yield return Wait(SettleSeconds);
            Assert.AreEqual(before, _agent.Blackboard.Machine.CurrentStateId,
                $"{LogPrefix} ★ 숨은 상태에서 상태가 {before} → {_agent.Blackboard.Machine.CurrentStateId}로 " +
                "전이했습니다 — 화면에 없는 캐릭터가 무언가를 하고 있습니다.");

            // ── ③ 탈출구는 열려 있는가 ──────────────────────────────────────────
            Assert.IsNotNull(_settings, $"{LogPrefix} 씬에 SettingsWindow가 없습니다.");
            _settings.Open("숨김 중 탈출구 검사");
            yield return Wait(SettleSeconds);
            Assert.IsTrue(_settings.IsOpen,
                $"{LogPrefix} ★ 숨은 상태에서 설정창이 열리지 않습니다 — [보이기] 버튼에 도달할 수 " +
                "없다는 뜻이고, 그게 이 라운드가 고친 «갇힘» 그 자체입니다.");

            bool shown = _agent.ToggleUserHidden("숨김 중 탈출구 검사");
            Assert.IsFalse(shown,
                $"{LogPrefix} ★ 숨김 해제 토글이 막혔습니다 — 절대 게이트를 붙이면 안 되는 경로입니다.");
            yield return Wait(SettleSeconds);

            // ── ④ 네거티브 대조: 풀면 다시 가능해지는가 ─────────────────────────
            Assert.IsFalse(_agent.IsSuspended, $"{LogPrefix} 숨김이 풀리지 않았습니다.");
            Assert.IsTrue(graffiti.GetAvailability().IsReady,
                $"{LogPrefix} 숨김을 풀었는데 그라피티가 계속 불가입니다" +
                $"({graffiti.GetAvailability().Reason}) — 게이트가 숨김이 아니라 <b>영구히</b> 막고 " +
                "있다는 뜻이라, 위 '막혔다' 단언들의 의미가 달라집니다.");

            Debug.Log($"{LogPrefix} 연출 차단 확인 — 명령 {ActionCommandPopover.CommandCount}칸 전부 " +
                $"\"{HiddenCharacterCommandGate.HiddenReason}\"로 회색, 강제 경로도 상태 전이 0건, " +
                "설정창·숨김 해제는 그대로 열려 있습니다.");
        }

    }
}
