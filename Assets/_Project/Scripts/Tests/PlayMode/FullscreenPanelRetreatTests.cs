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

        private string _backup;
        private bool _hadFile;

        /// <summary>축 3 — 등급 1의 원시 사실. 이름이 바뀌면 아래 단언이 먼저 실패한다.</summary>
        private static readonly FieldInfo PanelRetreatField =
            typeof(StickmanAgent).GetField("_fullscreenPanelRetreat", BindingFlags.Instance | BindingFlags.NonPublic);

        [OneTimeSetUp]
        public void BackupRealSaveFile()
        {
            string path = CharacterSaveStore.FilePath;
            _hadFile = File.Exists(path);
            _backup = _hadFile ? File.ReadAllText(path) : null;
        }

        [OneTimeTearDown]
        public void RestoreRealSaveFile()
        {
            string path = CharacterSaveStore.FilePath;
            if (_hadFile) File.WriteAllText(path, _backup);
            else if (File.Exists(path)) File.Delete(path);
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
        /// 톱니를 등급 1에 넣으면 그 안전판이 자기 자신을 지운다).</summary>
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
            Assert.IsTrue(_gear.IsIconVisible,
                $"{LogPrefix} 등급 1에서 톱니가 사라졌습니다 — 등급 1의 안전판(\"복구는 톱니 1클릭\")이 " +
                "자기 자신을 지웠습니다. 톱니는 등급 2로 남긴다는 것이 리더 판정입니다.");
            Assert.IsTrue(_gear.IsClickBlockerEnabled,
                $"{LogPrefix} 톱니는 보이는데 히트타깃이 꺼졌습니다 — 보이지만 눌리지 않는 톱니는 " +
                "탈출구가 아닙니다.");

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
    }
}
