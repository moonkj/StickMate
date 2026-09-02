using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.Platform;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ <b>등급 1(ForeignFullscreenTier.PanelsOnly) 실측 잠금</b> — 2026-09-02 출시 Blocker.
    ///
    /// ============================================================================
    /// 무엇이 터졌었나 (페르소나 `재현` 실기 재현)
    /// ============================================================================
    /// 카테고리를 선언하지 않은 앱(Zoom/Teams/Keynote 부류)을 네이티브 전체화면으로 올리면
    /// <b>자동 숨김이 0%</b>였다. 정보창이 그 위에 그대로 그려지고, 패널 안 클릭을 전체화면 앱이
    /// 받지 못했다(우리 차단막이 먹는다). 실측: 정보창 877x853pt / 화면 1512x982pt =
    /// <b>면적 50.38% · 세로 86.86%</b>.
    ///
    /// <para><b>결정적 대조</b>: 같은 창에 게임 카테고리만 붙이니 숨김 5회/해제 5회로 전부 정상
    /// 동작했다 — 숨김 기계는 끝까지 배선돼 있었고 없는 것은 트리거 하나뿐이었다.</para>
    ///
    /// ============================================================================
    /// 이 파일이 재는 것 — <b>두 방향을 동시에</b>
    /// ============================================================================
    /// 이 기능은 <b>한쪽으로만 잘못될 수 있는 기능이 아니다.</b> 그래서 두 방향을 같은 파일에서 잰다:
    /// <list type="number">
    ///   <item><b>덜 걷으면</b> 발표 화면 위에 창이 남고 클릭을 먹는다 = 지금 이 Blocker.</item>
    ///   <item><b>더 걷으면</b> 2026-08-31 사용자 신고
    ///     <i>"엑셀같은 프로그램 전체화면에서 엑셀 클릭하면 캐릭터가 없어져버림"</i>의 완전한 회귀다.</item>
    /// </list>
    /// <c>FullscreenSuspendUiHidingTests</c>(등급 2)는 ①만 잰다. 이 파일이 없으면 ②는 아무도 재지 않는다.
    ///
    /// ============================================================================
    /// 왜 리플렉션으로 축 3을 세우는가 — 그리고 <b>양성 대조</b>를 어떻게 얻었는가
    /// ============================================================================
    /// 실제 경로는 <c>IForeignFullscreenTierSource.GetForeignFullscreenTier()</c>인데, 그 구현체는
    /// <c>StickmanAgent.Awake()</c>가 스스로 만들어 private 필드에 넣으므로 <b>주입 지점이 없다</b>
    /// (에디터에서는 항상 <c>NullPlatformWindowService</c> = 등급 None). 그래서 이 프로젝트의 다른
    /// PlayMode 테스트들이 쓰는 리플렉션 관례를 그대로 따라 축 3 필드를 세운다.
    ///
    /// <para><b>여기에는 함정이 하나 있다</b>: 리플렉션 주입은 <c>Suspend()</c>를 부르지 않으므로
    /// "캐릭터 렌더러가 켜져 있다"는 단언이 <b>항상 참</b>일 수 있다 — 그러면 ②를 재는 척만 하는
    /// 초록불이 된다. 그래서 같은 테스트 안에서 <b>공개 API</b> <c>SetUserHidden(true)</c>로
    /// 진짜 <c>Suspend()</c>를 한 번 태워 <b>같은 측정 방법이 실제로 0을 볼 수 있음</b>을 보인다.
    /// 그 양성 대조가 깨지면 이 파일의 모든 "렌더러가 남아 있다" 판정을 폐기해야 한다.</para>
    /// </summary>
    public sealed class FullscreenPanelRetreatTests
    {
        private const string LogPrefix = "[등급1-TEST]";

        /// <summary>관측 중 에이전트의 자체 폴링이 주입한 축 3을 덮어쓰지 못하게 하는 값(초).
        /// 에디터의 <c>NullPlatformWindowService</c>는 등급 None이라, 폴링이 한 번이라도 돌면
        /// <c>_fullscreenPanelRetreat</c>가 false로 돌아가 이 테스트가 통째로 무의미해진다.</summary>
        private const float ObservePollInterval = 9999f;

        /// <summary>Update/LateUpdate가 한 바퀴 다 돌 여유(톱니는 LateUpdate, 창/팝오버는 Update).</summary>
        private const int SettleFrames = 5;

        private InfoGearIconWidget _gear;
        private CharacterInfoWindow _window;
        private GearRadialMenuWidget _menu;
        private TodoBoardPopover _todo;
        private TodoPostItWidget _postIt;
        private StickmanAgent _agent;

        private StickConfig _config;
        private float _savedPollInterval;


        /// <summary>축 3 — 등급 1의 원시 사실. 이름이 바뀌면 아래 단언이 먼저 실패한다.</summary>
        private static readonly FieldInfo PanelRetreatField =
            typeof(StickmanAgent).GetField("_fullscreenPanelRetreat", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// ★★ 2026-09-02 — 여기 있던 <b>백업/복원</b>은 <b>오염 보존기</b>였다. 걷어냈다. 되살리지 마라.
        ///
        /// <para><b>원래 근거가 사라졌다.</b> 옛 코드는 <c>OneTimeSetUp</c>에서 저장 파일을 통째로 읽어
        /// 두고 <c>OneTimeTearDown</c>에서 <b>그대로 다시 썼다</b>. 그 정당화는 이 클래스가 적어 둔
        /// <i>"저장 파일은 실행 중인 실제 앱의 것과 같은 경로"</i>였는데, 그 전제는 2026-08-31에
        /// <c>GlobalPlayModeTestIsolation</c>이 경로를 임시 폴더로 옮기면서 <b>거짓이 됐다</b>.
        /// 주석은 갱신되지 않았고 코드는 <b>목적 없이</b> 살아남았다.</para>
        ///
        /// <para><b>그리고 뜻이 정반대로 뒤집혔다.</b> 격리된 폴더에서 <c>_hadFile == true</c>는
        /// "개발자 파일이 있다"가 아니라 <b>"앞선 픽스처나 앞선 실행이 남긴 오염이 있다"</b>는 뜻이다.
        /// 옛 TearDown은 그 오염을 <b>다시 써서 되살렸다</b> — 뒤따르는 어떤 정리도 무효화하는 형태였고,
        /// 픽스처마다 같은 코드가 있어 오염이 스위트 전체를 타고 <b>세탁</b>됐다.</para>
        ///
        /// <para>실행 사이의 이월은 별도 원인이었다 — 리디렉션 폴더를 아무도 비우지 않았다. 그쪽은
        /// <c>GlobalPlayModeTestIsolation.PurgeIsolatedDirectories</c>가 막는다.</para>
        ///
        /// <para><b>대신 가드를 남긴다.</b> 격리가 꺼진 채로 이 픽스처가 돌면 씬 로드가 개발자의 실제
        /// 저장 파일을 읽고 쓰게 된다. 그때는 조용히 진행하지 않고 <b>즉시 실패</b>한다 —
        /// 백업/복원이 하던 안전 역할은 이 한 줄이 <b>더 정직하게</b> 대신한다.</para>
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
            UiLayoutModel.ResetForTesting();
        }

        [SetUp]
        public void ResetLayout()
        {
            UiLayoutModel.ResetForTesting();
            TodoListModel.ResetForTesting();
            CharacterSaveStore.Save();
        }

        [TearDown]
        public void RestoreAgent()
        {
            // 순서가 중요하다: 먼저 축 3과 사용자 숨김을 풀고(다음 테스트가 물려받지 않게)
            // 그 다음 폴링 주기를 되돌린다. config는 <b>배포 에셋</b>이라 반드시 원복해야 한다.
            SetPanelRetreat(false);
            if (_agent != null && _agent.IsUserHidden) _agent.SetUserHidden(false, "테스트 정리");
            if (_config != null) _config.fullscreenPollInterval = _savedPollInterval;
            _config = null;
            _gear = null;
            _window = null;
            _menu = null;
            _todo = null;
            _postIt = null;
            _agent = null;
            TodoListModel.ResetForTesting();
        }

        private void SetPanelRetreat(bool on)
        {
            if (_agent == null || PanelRetreatField == null) return;
            PanelRetreatField.SetValue(_agent, on);
        }

        private IEnumerator LoadSceneAndResolve()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var found = Object.FindObjectsByType<InfoGearIconWidget>(FindObjectsSortMode.None);
            Assert.AreEqual(1, found.Length, $"{LogPrefix} 씬의 InfoGearIconWidget 개수가 {found.Length}개입니다.");
            _gear = found[0];
            _window = _gear.GetComponent<CharacterInfoWindow>();
            _menu = _gear.GetComponent<GearRadialMenuWidget>();
            _todo = _gear.GetComponent<TodoBoardPopover>();
            _postIt = Object.FindFirstObjectByType<TodoPostItWidget>(FindObjectsInactive.Include);
            _agent = _gear.GetComponent<StickmanAgent>();

            Assert.IsNotNull(_window, $"{LogPrefix} CharacterInfoWindow가 없습니다.");
            Assert.IsNotNull(_menu, $"{LogPrefix} GearRadialMenuWidget이 없습니다.");
            Assert.IsNotNull(_todo, $"{LogPrefix} TodoBoardPopover가 없습니다.");
            Assert.IsNotNull(_agent, $"{LogPrefix} StickmanAgent가 없습니다.");
            Assert.IsNotNull(PanelRetreatField,
                $"{LogPrefix} StickmanAgent._fullscreenPanelRetreat 필드를 찾지 못했습니다 — 축 3의 이름이 " +
                "바뀌었다면 이 테스트의 주입 경로를 함께 고쳐야 합니다(이 단언이 없으면 주입이 아무 " +
                "일도 하지 않은 채 모든 단언이 통과합니다).");

            _config = _agent.Config;
            Assert.IsNotNull(_config, $"{LogPrefix} StickConfig가 없습니다.");
            _savedPollInterval = _config.fullscreenPollInterval;
            _config.fullscreenPollInterval = ObservePollInterval;

            yield return null;
        }

        /// <summary>창 · 팝오버 · 부채꼴을 동시에 띄운다(포스트잇은 건드리지 않는다 — 아래 별도 테스트).</summary>
        private IEnumerator OpenSurfaces()
        {
            Vector2 center = _gear.IconScreenCenter;
            _gear.FeedPointerForTests(true, center);
            _gear.FeedPointerForTests(false, center);
            yield return new WaitForSecondsRealtime(InfoGearIconWidget.MenuReadySeconds + 0.25f);

            _window.Open("등급 1 테스트 준비");
            yield return null;

            _menu.Expand(center);
            _todo.Open(_gear.IconScreenRect, "등급 1 테스트 준비");
            for (int i = 0; i < SettleFrames; i++) yield return null;
        }

        private IEnumerator WaitFrames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        /// <summary>★ <b>벽시계</b>로 기다린다(CLAUDE.md — 배치모드 PlayMode는 2,000fps 이상으로 돌아
        /// 프레임 수 기반 예산이 실제로는 0.0N초밖에 안 되는 사고가 있었다). 임대(초 단위)를 재는
        /// 아래 도달성 테스트는 <b>반드시</b> 이쪽을 쓴다.</summary>
        private static IEnumerator WaitWallClock(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        /// <summary>
        /// 지금 <b>켜져 있는</b> 캐릭터 렌더러들의 스냅샷.
        ///
        /// <para>개수가 아니라 <b>인스턴스 목록</b>을 뜨는 이유: 관측 사이에 이펙트/펫이 새로 생기거나
        /// 사라지면 개수 비교는 그 무관한 변화에 흔들린다(그러면 실패가 실패를 뜻하지 않게 된다).
        /// "그때 켜져 있던 <b>바로 그 렌더러들</b>이 지금도 켜져 있는가"가 우리가 재려는 것이다.</para>
        /// </summary>
        private static Renderer[] SnapshotEnabledCharacterRenderers(StickmanAgent agent)
        {
            var all = agent.GetComponentsInChildren<Renderer>(true);
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

        /// <summary>이름이 "Blocker"로 끝나는 <b>차단막</b> 콜라이더 중 지금 켜진 것.
        /// 톱니의 <c>InfoGearClickTarget</c>(아이콘 크기 히트타깃)은 여기 세지 않는다 —
        /// 톱니는 리더 판정으로 <b>등급 2</b>에 남았기 때문이다("복구는 톱니 1클릭"이 등급 1의 안전판인데
        /// 톱니를 등급 1에 넣으면 그 안전판이 자기 자신을 지운다).
        ///
        /// <para>★★★ <b>2026-09-03 — 이 면제의 근거가 이 라운드 전까지 <i>거짓</i>이었다.</b>
        /// 위 문장("복구는 톱니 1클릭")은 이 파일 세 곳과 <c>InfoGearIconWidget</c> 한 곳이
        /// <b>주장</b>했지만, 그것이 참인지 재는 테스트는 <b>0건</b>이었다. 실제로는 톱니를 눌러도
        /// 부채꼴이 같은 프레임에 회수되어 <b>아무 일도 일어나지 않았다</b> — 즉 면제는 존재하는데
        /// 면제가 지키려던 탈출구는 없었다(거짓 통과 10번째 형태: 기준과 대상이 같이 틀린다).</para>
        ///
        /// <para>그래서 이 면제를 남기되 <b>그 전제를 실제로 재는 테스트</b>를 같은 파일에 신설했다 —
        /// <c>등급1에서_톱니를_누르면_설정창까지_도달한다()</c>. 그 테스트가 빨개지면 이 면제는
        /// 근거를 잃는다. <b>둘은 함께 산다</b>.</para></summary>
        private static int CountEnabledFullRectBlockers(out string names)
        {
            var all = Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var sb = new System.Text.StringBuilder();
            int n = 0;
            for (int i = 0; i < all.Length; i++)
            {
                Collider2D c = all[i];
                if (c == null) continue;
                if (!c.gameObject.name.EndsWith("Blocker", System.StringComparison.Ordinal)) continue;
                if (!c.enabled || !c.gameObject.activeInHierarchy) continue;
                n++;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(c.gameObject.name);
            }
            names = sb.ToString();
            return n;
        }

        // ==================== ① 등급 1 = 표면만 걷고 캐릭터는 남는다 ====================

        [UnityTest]
        public IEnumerator 등급1은_창과_팝오버와_부채꼴을_걷고_캐릭터와_톱니는_남긴다()
        {
            yield return LoadSceneAndResolve();
            yield return OpenSurfaces();

            // ① 준비 확인 — 이 단계가 없으면 "원래 꺼져 있어서 통과"가 된다.
            Assert.IsTrue(_window.IsOpen && _window.IsClickBlockerEnabled,
                $"{LogPrefix} 준비 단계에서 캐릭터 창이 열리지 않았습니다.");
            Assert.IsTrue(_todo.IsOpen && _todo.IsClickBlockerEnabled,
                $"{LogPrefix} 준비 단계에서 [오늘 할일] 팝오버가 열리지 않았습니다.");
            Assert.IsTrue(_menu.IsVisible, $"{LogPrefix} 준비 단계에서 부채꼴이 펼쳐지지 않았습니다.");
            Assert.IsTrue(_gear.IsIconVisible, $"{LogPrefix} 준비 단계에서 톱니가 이미 꺼져 있습니다.");

            Renderer[] litRenderers = SnapshotEnabledCharacterRenderers(_agent);
            int renderersBefore = litRenderers.Length;
            Assert.Greater(renderersBefore, 0,
                $"{LogPrefix} 준비 단계에서 켜진 캐릭터 렌더러가 0개입니다 — 아래 '캐릭터가 남는다'가 " +
                "그냥 항상 참인 단언이 됩니다.");

            // ② 등급 1 주입.
            SetPanelRetreat(true);
            yield return WaitFrames(SettleFrames);

            // ★★ 이 두 줄이 이 라운드 전체의 계약이다.
            Assert.IsTrue(_agent.ArePanelsSuppressed,
                $"{LogPrefix} 축 3을 세웠는데 ArePanelsSuppressed가 false입니다 — 등급 1이 소비자에게 " +
                "도달하지 않습니다.");
            Assert.IsFalse(_agent.IsSuspended,
                $"{LogPrefix} 등급 1인데 IsSuspended가 true가 됐습니다 — 이것이 2026-08-31 신고" +
                "(\"엑셀 전체화면에서 캐릭터가 사라진다\")의 완전한 회귀입니다.");

            // ③ 표면은 걷힌다.
            Assert.IsFalse(_window.IsOpen, $"{LogPrefix} 등급 1인데 캐릭터 창이 닫히지 않았습니다.");
            Assert.IsFalse(_window.IsCanvasActive, $"{LogPrefix} 캐릭터 창 캔버스가 켜진 채 남아 있습니다.");
            Assert.IsFalse(_window.IsClickBlockerEnabled,
                $"{LogPrefix} 캐릭터 창 차단막이 살아 있습니다 — 발표·화상회의 화면 위에서 그 사각형의 " +
                "클릭을 우리가 먹습니다(면적 50.38%가 이번 Blocker의 실측입니다).");
            Assert.IsFalse(_todo.IsOpen, $"{LogPrefix} 등급 1인데 팝오버가 닫히지 않았습니다.");
            Assert.IsFalse(_todo.IsClickBlockerEnabled, $"{LogPrefix} 팝오버 차단막이 살아 있습니다.");
            Assert.IsFalse(_menu.IsVisible, $"{LogPrefix} 등급 1인데 부채꼴이 남아 있습니다.");

            // ④ 캐릭터와 톱니는 남는다 — 등급 1의 정의 그 자체.
            Assert.AreEqual(renderersBefore, CountStillEnabled(litRenderers),
                $"{LogPrefix} 등급 1에서 캐릭터 렌더러가 꺼졌습니다 — 게임이 아닌 전체화면 앱에서 " +
                "캐릭터가 사라지는 것이 바로 2026-08-31 신고입니다.");
            // ★★★ 2026-09-03 — 이 두 줄은 <b>톱니가 남아 있다</b>까지만 잰다. 「그 톱니를 눌러
            //   설정창까지 갈 수 있는가」는 <b>여기서 재지 않는다</b> — 그것을 재는 것이
            //   등급1에서_톱니를_누르면_설정창까지_도달한다()이고, 그 테스트가 없던 동안 이 두 줄이
            //   "탈출구가 있다"를 <b>주장만</b> 하며 초록으로 떠 있었다.
            Assert.IsTrue(_gear.IsIconVisible,
                $"{LogPrefix} 등급 1에서 톱니가 사라졌습니다 — 등급 1 탈출구의 <b>첫 홉</b>이 없어졌습니다. " +
                "톱니는 등급 2로 남긴다는 것이 리더 판정입니다(도달성은 별도 테스트가 잽니다).");
            Assert.IsTrue(_gear.IsClickBlockerEnabled,
                $"{LogPrefix} 톱니는 보이는데 히트타깃이 꺼졌습니다 — 보이지만 눌리지 않는 톱니는 " +
                "탈출구가 아닙니다. ★ 눌리는 것과 <b>눌러서 무언가 열리는 것</b>도 다릅니다 — " +
                "후자는 등급1에서_톱니를_누르면_설정창까지_도달한다()가 잽니다.");

            Debug.Log($"{LogPrefix} 등급 1 확인 — 창/팝오버/부채꼴은 걷혔고 캐릭터 렌더러 " +
                $"{renderersBefore}개와 톱니는 그대로 남았습니다.");

            // ⑤ ★ 양성 대조 — 같은 측정 방법이 실제로 "0"을 볼 수 있는가.
            //    이것이 없으면 위 ④는 "리플렉션 주입이 Suspend()를 안 부르니 당연히 안 꺼진다"는
            //    사실만으로 영원히 통과한다(이 저장소가 아홉 번 당한 거짓 통과의 형태).
            _agent.SetUserHidden(true, "등급1 테스트의 양성 대조");
            yield return WaitFrames(SettleFrames);
            Assert.AreEqual(0, CountStillEnabled(litRenderers),
                $"{LogPrefix} 양성 대조 실패 — 진짜 Suspend()를 태웠는데도 렌더러가 꺼지지 않았습니다. " +
                "이 측정 방법으로는 '캐릭터가 사라진다'를 감지할 수 없다는 뜻이므로, 위 ④의 판정도 " +
                "함께 폐기해야 합니다.");

            _agent.SetUserHidden(false, "등급1 테스트의 양성 대조 해제");
            yield return WaitFrames(SettleFrames);
            // ★ 여기만 정확한 개수가 아니라 ">0"으로 잰다. Resume()은 Awake 시점에 캐시한 배열만
            //   되켜고, 런타임에 생긴 액세서리/펫/FX는 각자의 소유자가 자기 주기에 되켜기 때문이다
            //   (StickmanAgent.SetRenderersEnabled의 조기 return). 그 비대칭은 이 테스트의 관심사가
            //   아니므로 "캐릭터가 다시 보인다"만 잠근다.
            Assert.Greater(CountStillEnabled(litRenderers), 0,
                $"{LogPrefix} 양성 대조 해제 후 캐릭터 렌더러가 하나도 돌아오지 않았습니다.");

            // 사용자 숨김을 풀어도 축 3은 그대로라 표면은 계속 걷혀 있어야 한다(두 축은 독립이다).
            Assert.IsTrue(_agent.ArePanelsSuppressed,
                $"{LogPrefix} 사용자 숨김을 푸는 것이 축 3까지 함께 껐습니다 — 축이 섞였습니다.");

            Debug.Log($"{LogPrefix} 양성 대조 통과 — 같은 측정으로 렌더러 0개를 실제로 관측했습니다.");
        }

        // ==================== ② 등급 1에서 차단막 전수 ====================

        [UnityTest]
        public IEnumerator 등급1에서_클릭_차단막이_전수_0개다()
        {
            yield return LoadSceneAndResolve();
            yield return OpenSurfaces();

            // ★ 포스트잇을 <b>일부러</b> 띄운다. 띄우지 않으면 이 테스트는 "포스트잇이 안 떠 있어서
            //   0개"라는 이유로 초록이 되고, 정작 배선이 빠진 표면을 영영 못 본다(거짓 통과 #5의 형태).
            TodoListModel.Add("등급 1 차단막 전수 조사용 할 일", PostItSoftCap);
            for (int i = 0; i < SettleFrames; i++) yield return null;

            int before = CountEnabledFullRectBlockers(out string beforeNames);
            Assert.GreaterOrEqual(before, 3,
                $"{LogPrefix} 켜진 차단막이 {before}개뿐입니다({beforeNames}) — 이름 규약이 바뀌었거나 " +
                "표면이 안 떠 있습니다. 이 최소 개수 단언이 없으면 아래 '전부 꺼졌다'가 " +
                "'애초에 하나도 못 찾았다'로도 통과합니다.");

            // ★ 개수만으로는 부족하다 — 이 테스트가 마지막으로 배선한 표면이 <b>포스트잇</b>이다.
            //   포스트잇 차단막이 준비 단계에 안 켜져 있으면(할 일 0건·기능 꺼짐·카드 미표시) 아래
            //   "전수 0개"는 다른 3개만 보고 초록이 되고, 정작 새로 배선한 표면은 한 번도 재지 않는다.
            //   이름을 못박아 그 구멍을 막는다(거짓 통과 #5: 재지 않은 채 통과하는 형태).
            StringAssert.Contains(PostItBlockerName, beforeNames,
                $"{LogPrefix} 준비 단계에 포스트잇 차단막({PostItBlockerName})이 켜져 있지 않습니다({beforeNames}). " +
                "TodoListModel.Add로 카드를 띄웠는데도 안 잡혔다면 카드가 안 떴거나 차단막 이름이 " +
                "바뀐 것입니다 — 이 단언이 없으면 아래 전수 0개가 포스트잇을 재지 않고도 통과합니다.");

            SetPanelRetreat(true);
            yield return WaitFrames(SettleFrames);

            int during = CountEnabledFullRectBlockers(out string duringNames);

            // ★ 2026-09-02 — 여기 있던 Assert.Ignore 탈출구가 사라졌다. 포스트잇이
            //   IsSuspended -> ArePanelsSuppressed로 배선되면서 예외가 성립하지 않는다.
            //   등급 1에서 켜진 차단막은 이제 <b>한 개도 허용되지 않는다</b>.
            Assert.AreEqual(0, during,
                $"{LogPrefix} 등급 1인데 클릭 차단막 {during}개가 아직 켜져 있습니다: {duringNames}. " +
                "이 목록에 있는 표면이 ArePanelsSuppressed를 폴링하지 않는 것입니다(원칙 2 위반) — " +
                "IsSuspended는 등급 1에서 false이므로 그것만 읽는 표면은 여기서 잡힙니다.");

            Debug.Log($"{LogPrefix} 등급 1에서 차단막 전수 0개 — 준비 시 {before}개({beforeNames})가 " +
                "전부 걷혔습니다(포스트잇 포함).");
        }

        /// <summary>포스트잇 차단막 GameObject 이름(<c>TodoPostItWidget.cs</c>의 <c>new GameObject(...)</c>와 동일).
        /// 준비 단계 양성 대조에 쓴다 — 이 이름이 바뀌면 위 단언이 먼저 걸려서 알려 준다.</summary>
        private const string PostItBlockerName = "TodoPostItClickBlocker";

        /// <summary>포스트잇을 띄우기 위한 할 일 1건의 소프트 캡. 값 자체는 관심사가 아니다.</summary>
        private const int PostItSoftCap = 99;

        // ==================== ③ 네거티브 컨트롤 ====================

        /// <summary>
        /// 등급이 <b>None</b>일 때는 아무것도 사라지지 않는다 — 위 테스트들이 "그냥 항상 걷힌다"를 보고
        /// 통과하는 것이 아님을 증명한다. 같은 대기 프레임 수, 같은 관측 지점을 쓴다.
        /// </summary>
        [UnityTest]
        public IEnumerator 등급이_없으면_표면도_캐릭터도_그대로다()
        {
            yield return LoadSceneAndResolve();
            yield return OpenSurfaces();

            SetPanelRetreat(false);
            yield return WaitFrames(SettleFrames);

            Assert.IsFalse(_agent.ArePanelsSuppressed,
                $"{LogPrefix} 등급이 없는데 ArePanelsSuppressed가 true입니다 — 표면이 이유 없이 걷힙니다.");
            Assert.IsFalse(_agent.IsSuspended, $"{LogPrefix} 등급이 없는데 IsSuspended가 true입니다.");
            Assert.IsTrue(_window.IsOpen, $"{LogPrefix} 등급이 없는데 캐릭터 창이 닫혔습니다.");
            Assert.IsTrue(_todo.IsOpen, $"{LogPrefix} 등급이 없는데 팝오버가 닫혔습니다.");
            Assert.IsTrue(_menu.IsVisible, $"{LogPrefix} 등급이 없는데 부채꼴이 사라졌습니다.");
            Assert.IsTrue(_gear.IsIconVisible, $"{LogPrefix} 등급이 없는데 톱니가 사라졌습니다.");
            Assert.Greater(SnapshotEnabledCharacterRenderers(_agent).Length, 0,
                $"{LogPrefix} 등급이 없는데 캐릭터 렌더러가 전부 꺼졌습니다.");

            int enabled = CountEnabledFullRectBlockers(out string names);
            Assert.Greater(enabled, 0,
                $"{LogPrefix} 등급이 없는데 켜진 차단막이 0개입니다 — 위 테스트의 '전부 꺼졌다'가 " +
                "그냥 항상 참이라는 뜻이 됩니다.");

            Debug.Log($"{LogPrefix} 네거티브 컨트롤 통과 — 등급 None에서는 표면 {enabled}개({names})와 " +
                "캐릭터가 그대로 유지됩니다.");
        }

        // ==================== ④ 등급 2는 등급 1을 포함한다 ====================

        /// <summary>
        /// 포함관계의 <b>실측</b>. 순수 규칙 쪽은 EditMode
        /// (<c>FullscreenGameSuspendPolicyTests.등급이_올라갈수록_더_걷는다_포함관계</c>)가 잠그고,
        /// 여기서는 <b>씬에 조립된 실제 소비자들</b>이 그 포함관계를 지키는지 본다.
        ///
        /// <para>이 단언이 깨지는 형태가 이 앱에서 가장 나쁜 상태다 — 캐릭터는 숨었는데 차단막이 남으면
        /// <b>안 보이는데 클릭만 먹는다</b>.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator 캐릭터가_숨으면_표면도_반드시_함께_걷힌다()
        {
            yield return LoadSceneAndResolve();
            yield return OpenSurfaces();

            // 축 3은 건드리지 않고 축 2(사용자 명시 숨김)만 켠다 = 캐릭터가 숨는 진짜 경로.
            _agent.SetUserHidden(true, "포함관계 실측");
            yield return WaitFrames(SettleFrames);

            Assert.IsTrue(_agent.IsSuspended, $"{LogPrefix} 사용자 숨김이 Suspend로 이어지지 않았습니다.");
            Assert.IsTrue(_agent.ArePanelsSuppressed,
                $"{LogPrefix} IsSuspended=true인데 ArePanelsSuppressed=false입니다 — 포함관계가 깨졌습니다. " +
                "캐릭터는 사라졌는데 창과 차단막이 남아 '안 보이는데 클릭만 먹는' 상태입니다.");
            Assert.IsFalse(_window.IsOpen, $"{LogPrefix} 캐릭터가 숨었는데 창이 남아 있습니다.");
            Assert.IsFalse(_todo.IsOpen, $"{LogPrefix} 캐릭터가 숨었는데 팝오버가 남아 있습니다.");
            Assert.IsFalse(_menu.IsVisible, $"{LogPrefix} 캐릭터가 숨었는데 부채꼴이 남아 있습니다.");

            Debug.Log($"{LogPrefix} 포함관계 실측 통과 — 캐릭터가 숨은 경로에서도 표면이 전부 함께 걷혔습니다.");
        }

        // ==================== ⑤ ★★★ R1-I 도달성 — 이 라운드의 핵심 산출물 ====================

        /// <summary>
        /// ★★★ <b>등급 1이 켜져 있는 동안 등급 1을 끄는 통제에 도달할 수 있는가</b>(불변식 R1-I).
        ///
        /// ============================================================================
        /// 왜 이 테스트가 <b>없어서</b> 사고가 났는가 — 이 파일의 자백
        /// ============================================================================
        /// 이 파일 두 곳(<c>CountEnabledFullRectBlockers</c>의 톱니 면제 · 등급 1 테스트의 톱니 단언)과
        /// <c>InfoGearIconWidget</c> 한 곳이 전부 <b>같은 전제</b>를 주장했다 —
        /// <i>"복구는 톱니 1클릭"</i>. 그런데 <b>그 전제가 참인지 재는 테스트는 0건이었다.</b>
        /// 그리고 실제로는 <b>거짓</b>이었다: 톱니는 보이고 눌렸지만, 눌러서 펼쳐진 부채꼴이
        /// <b>같은 프레임에 회수</b>되어 화면에서는 아무 일도 일어나지 않았다.
        ///
        /// <para>루프의 정체: 등급 1을 끄는 유일한 스위치(설정창 [일반] "전체화면 게임 감지 시 자동
        /// 숨김")가 <b>등급 1 때문에 닫히는 창 안에</b> 있었다. 경로 4개가 전부 막혀 있었고
        /// (톱니 → 부채꼴 → 정보창 → 설정 / 전역 단축키 / 자동 복귀 예약 / 사용자 숨김 단축키),
        /// 즉 <b>앱 안에 탈출구가 하나도 없었다</b>.</para>
        ///
        /// ============================================================================
        /// 이 테스트가 재는 것 — <b>주장이 아니라 도달</b>
        /// ============================================================================
        /// 톱니 클릭에서 시작해 <b>설정창이 실제로 떠서 머무를 때까지</b>를 한 번에 걷는다.
        /// 중간 홉을 건너뛰지 않는다 — 건너뛰면 "부채꼴은 살아남는데 정보창에서 끊긴다" 같은
        /// 부분 회귀를 그대로 놓친다.
        ///
        /// <para>★ <b>양성 대조가 뒤에 붙어 있다</b>: 사용자가 표면을 전부 닫으면 허가(임대)가 만료되어
        /// <b>같은 측정으로</b> 회수가 돌아오는 것을 관측한다. 그게 없으면 이 테스트는
        /// "등급 1이 애초에 아무것도 안 걷는다"로도 초록이 되고, 그때 이 장치는 원칙 2의 구멍이다.</para>
        ///
        /// <para>★ 시간 예산은 전부 <b>벽시계</b>다(<see cref="WaitWallClock"/>) — 임대는 초 단위 계약이고,
        /// 배치모드 PlayMode의 프레임 수는 시간과 무관하다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 등급1에서_톱니를_누르면_설정창까지_도달한다()
        {
            yield return LoadSceneAndResolve();

            var settings = _gear.GetComponent<SettingsWindow>();
            Assert.IsNotNull(settings,
                $"{LogPrefix} SettingsWindow를 찾지 못했습니다 — 등급 1을 끄는 <b>유일한 스위치</b>가 " +
                "그 창 안에 있으므로, 창이 없으면 이 테스트가 재려는 도달성 자체가 성립하지 않습니다.");

            // ---------- ① 아무것도 열려 있지 않은 상태에서 등급 1로 <b>진입</b>한다 ----------
            SetPanelRetreat(true);
            yield return WaitFrames(SettleFrames);

            Assert.IsTrue(_agent.ArePanelsSuppressed,
                $"{LogPrefix} 등급 1을 세웠는데 회수가 켜지지 않았습니다 — 이 테스트의 전제가 없습니다.");
            Assert.IsFalse(_agent.IsSuspended,
                $"{LogPrefix} 등급 1인데 IsSuspended가 참입니다(2026-08-31 신고 회귀).");
            Assert.IsFalse(_agent.IsUserSummonGrantActive,
                $"{LogPrefix} 등급 1 <b>진입 직후</b>인데 사용자 허가가 이미 살아 있습니다 — " +
                "「등급 1 진입 시 전부 회수」가 깨졌습니다. 이 상태로는 아래 단언들이 " +
                "'원래 허가가 있어서 통과'가 되어 아무것도 재지 못합니다.");
            Assert.IsFalse(_menu.IsVisible, $"{LogPrefix} 준비 단계에서 부채꼴이 이미 펼쳐져 있습니다.");
            Assert.IsFalse(_window.IsOpen, $"{LogPrefix} 준비 단계에서 정보창이 이미 열려 있습니다.");
            Assert.IsFalse(settings.IsOpen, $"{LogPrefix} 준비 단계에서 설정창이 이미 열려 있습니다.");

            // 톱니는 등급 2에만 걷히므로 여기서는 살아 있어야 한다 — <b>탈출구의 첫 홉</b>.
            Assert.IsTrue(_gear.IsIconVisible && _gear.IsClickBlockerEnabled,
                $"{LogPrefix} 등급 1에서 톱니가 보이지 않거나 눌리지 않습니다 — 첫 홉이 없으면 " +
                "나머지 경로를 잴 필요도 없습니다.");

            // ---------- ② 톱니 클릭(실제 입력과 <b>같은</b> 처리 경로) ----------
            Vector2 center = _gear.IconScreenCenter;
            _gear.FeedPointerForTests(true, center);
            _gear.FeedPointerForTests(false, center);
            yield return WaitFrames(SettleFrames);

            // ★★★ 이 세 줄이 이 라운드의 계약이다.
            Assert.IsTrue(_agent.IsUserSummonGrantActive,
                $"{LogPrefix} 톱니를 눌렀는데 사용자 허가가 나지 않았습니다 — 등급 1 탈출구가 " +
                "열리는 지점이 여기입니다(InfoGearIconWidget.ActivateClick).");
            Assert.IsFalse(_agent.ArePanelsSuppressed,
                $"{LogPrefix} 허가는 났는데 회수가 여전히 켜져 있습니다 — 허가가 소비자 창구" +
                "(ArePanelsSuppressed)에 도달하지 않습니다.");
            Assert.IsTrue(_menu.IsVisible,
                $"{LogPrefix} ★ <b>톱니를 눌렀는데 부채꼴이 회수됐습니다.</b> 이것이 2026-09-03 " +
                "이전의 실제 증상입니다 — 톱니는 보이고 눌리는데 화면에서는 아무 일도 일어나지 않아, " +
                "사용자에게는 '톱니가 고장났다'로 보입니다. 그리고 이 경로가 막히면 등급 1을 끄는 " +
                "방법이 앱 안에 하나도 없습니다(경로 4개 전수 확인).");

            // ---------- ③ 임대가 <b>끊기지 않는다</b>(다음 클릭이 먹을 때까지 벽시계로 기다린다) ----------
            yield return WaitWallClock(InfoGearIconWidget.MenuReadySeconds + 0.25f);
            Assert.IsTrue(_menu.IsVisible,
                $"{LogPrefix} 부채꼴이 {InfoGearIconWidget.MenuReadySeconds:F2}초 뒤에 사라졌습니다 — " +
                $"허가 임대({UserSurfaceSummonPolicy.LeaseSeconds:F2}초)를 아무도 갱신하지 않았다는 뜻입니다. " +
                "회전 게이트가 끝나기도 전에 만료되면 사용자는 다음 홉을 누를 수조차 없습니다.");

            // ---------- ④ 두 번째 홉 — 부채꼴 [캐릭터] → 정보창 ----------
            // 인덱스를 숫자로 베끼지 않는다(CLAUDE.md) — 프로덕션 열거형을 그대로 쓴다.
            _menu.Activate((int)GearMenuButton.Character);
            yield return WaitFrames(SettleFrames);
            Assert.IsTrue(_window.IsOpen,
                $"{LogPrefix} [{GearRadialMenuWidget.NameOf((int)GearMenuButton.Character)}]를 눌렀는데 " +
                "정보창이 살아남지 못했습니다 — 두 번째 홉에서 끊깁니다.");

            // ---------- ⑤ 마지막 홉 — 정보창 [설정] 칩과 <b>같은 호출</b> ----------
            // CharacterInfoWindow.OpenSettings()가 하는 일이 정확히 이 한 줄이다(그 함수는 private이라
            // 여기서 직접 부른다 — 문자열 사유만 다르고 경로는 같다).
            settings.Open("등급 1 도달성 테스트 — 정보창 [설정] 칩과 같은 호출");
            yield return WaitFrames(SettleFrames);
            Assert.IsTrue(settings.IsOpen,
                $"{LogPrefix} ★ <b>설정창이 열리자마자 닫혔습니다.</b> 등급 1을 끄는 유일한 스위치가 " +
                "이 창 안에 있으므로, 이것이 곧 '등급 1을 끌 방법이 없다'입니다(불변식 R1-I 위반).");
            Assert.IsTrue(settings.IsClickBlockerEnabled,
                $"{LogPrefix} 설정창은 떴는데 차단막이 꺼졌습니다 — 그리면서 클릭은 안 받는 창입니다.");

            // ---------- ⑥ <b>머문다</b> — 임대 수명의 4배를 벽시계로 버틴다 ----------
            yield return WaitWallClock(UserSurfaceSummonPolicy.LeaseSeconds * 4f);
            Assert.IsTrue(settings.IsOpen,
                $"{LogPrefix} 설정창이 {UserSurfaceSummonPolicy.LeaseSeconds * 4f:F2}초 뒤에 사라졌습니다 — " +
                "설정을 읽는 도중에 창이 없어지는 것은 고치려던 증상 그 자체입니다. " +
                "이 창이 자기 Update에서 임대를 갱신하는지 확인하십시오.");

            // 축 3은 <b>여전히 켜져 있어야</b> 한다 — 꺼져 있었다면 위 단언들은 등급 1을 한 번도
            // 마주치지 않은 채 통과한 것이다(거짓 통과 #5의 형태).
            Assert.IsTrue((bool)PanelRetreatField.GetValue(_agent),
                $"{LogPrefix} 축 3이 저절로 꺼졌습니다 — 위 단언들은 '등급 1이 아니어서' 통과했을 뿐이고 " +
                "이 테스트는 아무것도 재지 않았습니다.");

            Debug.Log($"{LogPrefix} R1-I 도달성 통과 — 등급 1 중 톱니 → 부채꼴 → 정보창 → 설정창까지 " +
                $"네 홉이 전부 살아남았고, 설정창이 {UserSurfaceSummonPolicy.LeaseSeconds * 4f:F2}초를 버텼습니다.");

            // ================================================================
            // ⑦ ★ 양성 대조 — 허가는 <b>백지수표가 아니다</b>
            // ================================================================
            // 이 절이 없으면 위 전체가 "등급 1이 애초에 아무것도 안 걷는다"로도 초록이 된다.
            // 그 경우 이 라운드의 장치는 탈출구가 아니라 <b>원칙 2의 구멍</b>이다.
            settings.Close("양성 대조 — 사용자가 [✕]를 눌렀다");
            yield return WaitFrames(SettleFrames);

            // 설정창은 자기가 밀어낸 정보창을 되돌린다(M8 시트 복귀). 그것도 사용자 표면이므로
            // 임대가 이어진다 — 만료를 보려면 그것까지 닫아야 한다. 이 한 줄이 없으면 아래 대조가
            // "왜 안 만료되지"로 흐려진다.
            if (_window.IsOpen) _window.Close("양성 대조 — 사용자가 정보창도 닫았다");
            yield return WaitFrames(SettleFrames);
            Assert.IsFalse(_menu.IsVisible,
                $"{LogPrefix} 양성 대조 준비 — 부채꼴이 아직 떠 있어 임대를 계속 갱신합니다.");

            // 이제 갱신자가 하나도 없다. 임대는 스스로 만료되어야 한다.
            yield return WaitWallClock(UserSurfaceSummonPolicy.LeaseSeconds * 3f);
            Assert.IsFalse(_agent.IsUserSummonGrantActive,
                $"{LogPrefix} ★ 양성 대조 실패 — 사용자가 표면을 전부 닫았는데도 허가가 " +
                $"{UserSurfaceSummonPolicy.LeaseSeconds * 3f:F2}초 뒤까지 살아 있습니다. 허가가 " +
                "'한 번 켜면 안 꺼지는' 형태라면 이 전체화면 세션 내내 사용자가 부르지도 않은 표면" +
                "(할 일 리마인더 · 크래시 오버레이 · 포스트잇)이 발표 화면으로 돌아옵니다.");
            Assert.IsTrue(_agent.ArePanelsSuppressed,
                $"{LogPrefix} ★ 양성 대조 실패 — 허가가 만료됐는데도 회수가 돌아오지 않았습니다. " +
                "같은 측정(ArePanelsSuppressed)으로 '회수 켜짐'을 관측하지 못한다는 뜻이므로 " +
                "위 ②~⑥의 판정도 함께 폐기해야 합니다.");

            // 그리고 허가 없이 연 표면은 <b>즉시</b> 걷힌다 — 갱신은 죽은 임대를 되살리지 않는다.
            _window.Open("양성 대조 — 허가 없이 연 창은 살아남지 못한다");
            yield return WaitFrames(SettleFrames);
            Assert.IsFalse(_window.IsOpen,
                $"{LogPrefix} ★ 양성 대조 실패 — 허가 없이 등급 1에서 연 창이 살아남았습니다. " +
                "갱신(RenewUserSummonGrant)이 만료된 임대를 되살리고 있다는 뜻이고, 그러면 " +
                "「등급 1 진입 시 전부 회수」가 구조적으로 깨집니다.");

            Debug.Log($"{LogPrefix} 양성 대조 통과 — 허가는 사용자가 부른 표면이 살아 있는 동안만 " +
                $"유지되고({UserSurfaceSummonPolicy.LeaseSeconds:F2}초 임대), 만료 뒤에는 같은 측정으로 " +
                "회수가 돌아오는 것을 실제로 관측했습니다.");
        }

        // ==================== ⑥ 아직 막혀 있는 경로 — 러너에 계속 보이게 ====================

        /// <summary>
        /// ★ <b>미해결 갭 · 배정 대기</b> — 등급 1 중 <c>정보창 단축키</c>로 정보창을 여는 경로는
        /// <b>여전히 열자마자 닫힌다</b>.
        ///
        /// <para><b>왜 남았나(파일 소유 문제이지 설계 문제가 아니다)</b>: 이 라운드의 허가는
        /// <b>명시적 사용자 진입점</b>에서만 난다. 그 진입점 중 두 개는 이 라운드가 배정받은 파일 안에
        /// 있어 닫혔다 — 톱니 클릭(<c>InfoGearIconWidget.ActivateClick</c>)과
        /// 설정창 열기(<c>SettingsWindow.Open</c>, 전역 단축키 경로 포함). 세 번째인
        /// <c>CharacterInfoWindow.Open</c>은 <b>이 라운드의 배정 파일이 아니다</b>(동시 진행 라운드가
        /// 같은 폴더를 잡고 있어 리더가 파일을 갈랐다 — 2026-09-02에 겹쳐 돌다 두 건의 사고가 났다).</para>
        ///
        /// <para><b>영향은 「불편」이지 「막힘」이 아니다</b>: 등급 1을 <b>끄는</b> 통제(설정창)에는
        /// 두 경로로 도달한다 — 톱니 4홉과 설정창 전역 단축키. 불변식 R1-I는 그 둘로 닫혔고,
        /// 여기 남은 것은 정보창 단축키라는 <b>보조 경로</b> 하나다.</para>
        ///
        /// <para><b>고치는 법(한 줄)</b>: <c>CharacterInfoWindow.Open(string)</c>이
        /// <c>SettingsWindow.Open</c>과 <b>같은 형태로</b> <c>TryGrantUserSummon</c>을 부르면 된다.
        /// 그때 이 테스트를 <c>Assert.Ignore</c>에서 <b>실측으로 승격</b>하라 — 위
        /// <c>등급1에서_톱니를_누르면_설정창까지_도달한다</c>가 그대로 본이 된다.</para>
        ///
        /// <para>★ <c>Assert.Fail</c>이 아니라 <c>Assert.Ignore</c>인 이유(CLAUDE.md): 빨간불로 두면
        /// 다른 진짜 실패를 가리고, 조용히 통과시키면 잊힌다. <b>건너뜀으로 러너에 계속 떠 있어야</b>
        /// 다음 라운드가 본다.</para>
        /// </summary>
        [Test]
        public void 미해결_등급1에서_정보창_단축키_경로는_아직_허가를_받지_못한다()
        {
            // ★ 갭이 <b>아직 실재하는지</b>를 소스로 확인한다 — 고쳐졌는데 Ignore만 남으면 이 항목은
            //   러너에서 영원히 "건너뜀"으로 굳어 아무 뜻도 없어진다(이 저장소의 명부 노화 사고).
            string info = File.ReadAllText(Path.Combine(
                Application.dataPath, "_Project", "Scripts", "Interaction", "CharacterInfoWindow.cs"));

            // 니들은 프로덕션 멤버 이름에서 가져온다(문자열 하드코딩 금지 — 이름이 바뀌면 컴파일이 깨진다).
            string grantNeedle = nameof(StickmanAgent.TryGrantUserSummon);

            // ★ 부재 단언의 자격 검증: 같은 스캐너가 <b>실재하는 것</b>을 실제로 찾아내는지 먼저 보인다.
            //   이 대조가 없으면 "0건"이 '고쳐졌다'인지 '스캐너가 죽었다'인지 구분되지 않는다.
            StringAssert.Contains(nameof(StickmanAgent.ArePanelsSuppressed), info,
                "양성 대조 실패 — CharacterInfoWindow.cs에서 이미 있는 이름조차 못 찾았습니다. " +
                "경로/인코딩이 깨졌다는 뜻이므로 아래 '없음' 판정은 무효입니다.");

            if (info.IndexOf(grantNeedle, System.StringComparison.Ordinal) >= 0)
            {
                Assert.Fail($"CharacterInfoWindow.cs가 이미 {grantNeedle}을 부릅니다 — 갭이 닫혔습니다. " +
                    "이 Assert.Ignore를 지우고 실측 테스트로 승격하십시오(위 도달성 테스트가 본입니다). " +
                    "남겨 두면 러너에서 영원히 '건너뜀'으로 굳어 아무것도 말하지 않습니다.");
            }

            Assert.Ignore("【미해결 갭 · 배정 대기】 신설 2026-09-03 (dev-platform)\n" +
                "등급 1 체류 중 <정보창 전역 단축키>로 정보창을 열면 그 프레임에 다시 닫힌다 — " +
                $"CharacterInfoWindow.Open이 {grantNeedle}을 부르지 않기 때문이다.\n" +
                "· 왜 이 라운드가 못 고쳤나: CharacterInfoWindow.cs가 이 라운드의 배정 파일이 아니다" +
                "(동시 진행 라운드와 파일이 겹치면 사고가 난다 — 리더가 파일을 가른다).\n" +
                "· 심각도: 보조 경로 1개. 등급 1을 끄는 통제(설정창)에는 톱니 4홉과 설정창 단축키 " +
                "두 경로로 여전히 도달한다(불변식 R1-I는 닫혔다).\n" +
                "· 처방: SettingsWindow.Open과 같은 형태로 한 줄. 사용자가 부르지 않은 복귀 경로에서는 " +
                "부르지 말 것(그 구분이 이 장치가 원칙 2의 구멍이 되지 않는 유일한 이유다).");
        }
    }
}
