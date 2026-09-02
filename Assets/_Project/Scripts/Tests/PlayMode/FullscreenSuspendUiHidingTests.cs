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
    /// ★ 절대 불변 원칙 2("비침해: 클릭 관통 기본 ON, <b>전체화면 게임 감지 시 자동 숨김</b>")의 실측 잠금 —
    /// 상시/임시 UI 표면 전부에 대해. 2026-08-30 횡단 리뷰 M2 + R3 m3.
    ///
    /// ============================================================================
    /// 왜 이 테스트가 필요한가 (리뷰 2회 연속 지적, 테스트 0건)
    /// ============================================================================
    /// <see cref="StickmanAgent"/>.Suspend()가 끄는 것은 <b>Awake에서 캐시한 캐릭터 렌더러 배열</b>뿐이다.
    /// 그런데 톱니/부채꼴/팝오버 2종/캐릭터 창은 전부 <b>씬 루트</b>의 별도 Canvas·Collider라 그 배열에
    /// 들어 있지 않다 — 액세서리가 겪었던 "몸이 사라진 자리에 모자만 남는다"와 정확히 같은 구조다.
    /// 게다가 StickmanAgent가 SetAlwaysOnTop(true)를 켜므로 전체화면 게임 <b>위에</b> 그대로 뜨고,
    /// 히트테스트가 <b>커서 아래 Collider2D</b>를 보므로(UniWindowController <c>hitTestType=Raycast</c> —
    /// <c>InfoGearIconWidget.cs</c> "클릭 판정" 절) 그 표면들이 깔아 둔 차단막 사각형은
    /// <b>보이는 데 그치지 않고 클릭까지 먹는다</b>.
    /// <para>★ 2026-09-02 정정 — 여기 원래 "macOS 히트테스트가 커서 아래 <b>픽셀 알파</b>를 보므로"라고
    /// 적혀 있었다. <b>거짓이다.</b> 이 앱은 픽셀 알파가 아니라 Raycast 히트테스트를 쓰고, 그래서
    /// "안 보이는데 클릭만 먹는" 위험의 실체는 <b>남아 있는 콜라이더</b>다(그림을 지우는 것만으로는
    /// 부족하다는 뜻이라, 아래 ②의 <c>CountEnabledClickBlockers</c>가 이 파일의 핵심이 된다).</para>
    ///
    /// ============================================================================
    /// 절대 조건으로 잠그는 것 (상대 비교/플래그 확인 금지)
    /// ============================================================================
    ///  ① 숨기기 <b>전에</b> 모든 표면이 실제로 켜져 있다 — 이 단계가 없으면 "원래 꺼져 있어서 통과"가 된다.
    ///  ② 감지 후: 톱니 그림 / 부채꼴 / 팝오버 / 캐릭터 창 / 포스트잇이 전부 꺼지고,
    ///     <b>씬의 클릭 차단막이 하나도 남김없이 꺼진다</b> — 안 꺼지면 "안 보이는데 클릭만 먹는"
    ///     최악의 형태가 된다. 플래그가 아니라 GameObject/Collider의 <b>실제 상태</b>를 읽는다.
    ///  ③ 복귀하면 톱니와 포스트잇(상시 HUD)은 다시 나타난다. 반대로 <b>메뉴/창/팝오버는 다시 열리지
    ///     않는다</b> — 사용자가 부르지도 않은 창이 게임을 끄자마자 튀어나오면 그 자체가 방해다(확정 설계).
    ///
    /// ============================================================================
    /// ★ 2026-09-01 — 이 테스트가 놓쳤던 것(포스트잇)과, 그래서 바꾼 것
    /// ============================================================================
    /// 원래 이 테스트가 검사한 것은 톱니 / 부채꼴 / <b>TodoBoardPopover</b> / 캐릭터 창 4종이었고,
    /// <c>TodoPostItWidget</c>은 대상에 없었다. 이름이 비슷한 <c>TodoBoardPopover</c>가 명부에 있어서
    /// "할 일 쪽은 검증됨"으로 보인 것이 함정이었다. 실제로 포스트잇은 <c>IsSuspended</c> 참조가
    /// <b>0건</b>이라 전체화면 게임 위에 카드가 그대로 뜨고 그 사각형의 클릭까지 먹고 있었다.
    ///
    /// 그래서 표면을 하나 더 적는 데서 멈추지 않고, <b>씬의 클릭 차단막을 전수로 훑는 단언</b>을 넣었다
    /// (<c>CountEnabledClickBlockers</c>). 다음에 새 표면이 생겨도 이 파일을 고치지 않고 잡힌다.
    /// "명부에 한 줄 추가"에 의존하는 검사는 이미 한 번 샜다.
    ///
    /// ============================================================================
    /// 왜 리플렉션으로 _isSuspended를 세우는가
    /// ============================================================================
    /// 실제 경로는 <c>IPlatformWindowService.IsFullscreenAppActive()</c>인데, 그 구현체는
    /// <c>StickmanAgent.Awake()</c>가 스스로 만들어 private 필드에 넣으므로 <b>주입 지점이 없다</b>.
    /// 여기서 <see cref="StickmanAgent"/>에 테스트 전용 훅을 새로 뚫는 것은 지금 그 파일을 병행
    /// 수정 중인 다른 작업과 충돌하므로, 이미 이 프로젝트의 다른 테스트들이 쓰는 리플렉션 관례를 따른다
    /// (Phase5VisualLayerTests / HardwareReactionRendererTests / DialogueComicTextPlacementTests).
    /// <b>소비자 코드가 읽는 값은 정확히 <c>IsSuspended</c> 하나</b>이므로 이 주입은 실제 경로와 등가다.
    /// 관측 중에 에이전트가 스스로 Resume()해버리지 않도록 폴링 주기를 잠시 크게 잡는다(TearDown 복구).
    /// </summary>
    public sealed class FullscreenSuspendUiHidingTests
    {
        private const string LogPrefix = "[전체화면숨김-TEST]";

        /// <summary>관측 중 에이전트의 자체 폴링이 Resume()을 부르지 못하게 하는 값(초).</summary>
        private const float ObservePollInterval = 9999f;

        /// <summary>Update/LateUpdate가 한 바퀴 다 돌 여유. 톱니(LateUpdate)와 팝오버/창(Update)이
        /// 서로 다른 단계라 한 프레임으로는 부족할 수 있다.</summary>
        private const int SettleFrames = 5;

        private InfoGearIconWidget _gear;
        private CharacterInfoWindow _window;
        private GearRadialMenuWidget _menu;
        private TodoBoardPopover _todo;
        private TodoPostItWidget _postIt;
        private StickmanAgent _agent;

        private StickConfig _config;
        private float _savedPollInterval;


        private static readonly FieldInfo SuspendedField =
            typeof(StickmanAgent).GetField("_isSuspended", BindingFlags.Instance | BindingFlags.NonPublic);

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
            UiLayoutModel.ResetForTesting();
        }

        [SetUp]
        public void ResetLayout()
        {
            UiLayoutModel.ResetForTesting();
            // 정적 모델이라 씬을 다시 로드해도 목록이 살아남는다 — 앞선 테스트가 남긴 할 일이 있으면
            // 포스트잇 준비 단언이 "몇 건인지"에 따라 흔들린다.
            TodoListModel.ResetForTesting();
            CharacterSaveStore.Save();
        }

        [TearDown]
        public void RestoreAgent()
        {
            // 순서가 중요하다: 먼저 감지를 풀고(다른 테스트가 Suspended 상태를 물려받지 않게)
            // 그 다음 폴링 주기를 되돌린다. config는 <b>배포 에셋</b>이라 반드시 원복해야 한다.
            SetSuspended(false);
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

        private void SetSuspended(bool on)
        {
            if (_agent == null || SuspendedField == null) return;
            SuspendedField.SetValue(_agent, on);
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
            // ★ 이름이 비슷한 TodoBoardPopover가 이미 있어서 "할 일 쪽은 검증됨"으로 보였던 것이
            //   이 사각지대의 정체다. 둘은 완전히 다른 표면이다(팝오버 = 부채꼴에서 부르는 임시 창,
            //   포스트잇 = 할 일이 있으면 하루 종일 떠 있는 상시 HUD).
            _postIt = Object.FindFirstObjectByType<TodoPostItWidget>(FindObjectsInactive.Include);
            _agent = _gear.GetComponent<StickmanAgent>();

            Assert.IsNotNull(_window, $"{LogPrefix} CharacterInfoWindow가 없습니다.");
            Assert.IsNotNull(_menu, $"{LogPrefix} GearRadialMenuWidget이 없습니다.");
            Assert.IsNotNull(_todo, $"{LogPrefix} TodoBoardPopover가 없습니다.");
            Assert.IsNotNull(_postIt, $"{LogPrefix} TodoPostItWidget이 없습니다 — 씬 조립이 바뀌었다면 " +
                "이 테스트의 해석 경로를 함께 고쳐야 합니다.");
            Assert.IsNotNull(_agent, $"{LogPrefix} StickmanAgent가 없습니다.");
            Assert.IsNotNull(SuspendedField, $"{LogPrefix} StickmanAgent._isSuspended 필드를 찾지 못했습니다 " +
                "— 필드 이름이 바뀌었다면 이 테스트의 주입 경로를 함께 고쳐야 합니다.");

            _config = _agent.Config;
            Assert.IsNotNull(_config, $"{LogPrefix} StickConfig가 없습니다.");
            _savedPollInterval = _config.fullscreenPollInterval;
            _config.fullscreenPollInterval = ObservePollInterval;

            yield return null;
        }

        /// <summary>
        /// 네 표면을 <b>동시에</b> 띄운다 — "가장 많이 떠 있는 상태"에서 감지가 걸리는 것이 최악의 경우다.
        ///
        /// ★ 2026-08-30 변경: 캐릭터 창이 <b>배타적 모달</b>이 되면서(창을 열면 부채꼴/팝오버가 접히고,
        /// 창이 열린 채 톱니를 누르면 부채꼴 대신 창이 닫힌다) 이 조합은 <b>실제 사용자 경로로는 더 이상
        /// 만들 수 없다</b>. 그래도 이 테스트는 남긴다 — Suspend()가 "어떻게 떠 있게 됐든 전부 거둔다"를
        /// 재는 것이 목적이고, 앞으로 새 진입점이 배타 규칙을 새게 하더라도 이 안전망은 살아 있어야 한다.
        /// 그래서 조합은 위젯 API로 <b>의도적으로</b> 만든다(순서 중요 — 창을 먼저 연다).
        /// </summary>
        private IEnumerator OpenEverything()
        {
            // ① 톱니는 실제 경로 그대로 한 번 눌러 본다(회전/차단막 배선이 살아 있는지 확인하는 의미).
            Vector2 center = _gear.IconScreenCenter;
            _gear.FeedPointerForTests(true, center);
            _gear.FeedPointerForTests(false, center);
            yield return new WaitForSecondsRealtime(InfoGearIconWidget.MenuReadySeconds + 0.25f);

            // ② 창을 먼저 연다 — 창이 열리는 순간 부채꼴/팝오버를 거두므로 순서를 뒤집으면 셋이 못 모인다.
            _window.Open("테스트 준비");
            yield return null;

            // ③ 그 위에 부채꼴/팝오버를 강제로 되살려 최악의 조합을 만든다.
            _menu.Expand(center);
            _todo.Open(_gear.IconScreenRect, "테스트 준비");

            // ④ 포스트잇은 "열기" API가 없다 — 미완료 할 일이 1건 이상이면 스스로 뜨는 상시 HUD다.
            //    검증 세션에 미완료가 0건이라 로그로 재현되지 않았던 것이 이 결함이 오래 산 이유다.
            TodoListModel.Add("전체화면 숨김 회귀 테스트용 할 일", PostItSoftCap);
            for (int i = 0; i < SettleFrames; i++) yield return null;
        }

        /// <summary>포스트잇을 띄우기 위한 할 일 1건의 소프트 캡. 값 자체는 이 테스트의 관심사가
        /// 아니다(1건만 넣으므로 어떤 상한이든 통과한다).</summary>
        private const int PostItSoftCap = 99;

        // ==================== 차단막 전수 조사 ====================
        //
        // ★ 개별 표면을 하나씩 적는 방식이 이 사고를 놓쳤다 — 다섯 표면을 적어 두고
        //   TodoPostItWidget 하나가 빠져 있었고, 이름이 비슷한 TodoBoardPopover 때문에
        //   "할 일 쪽은 검증됨"으로 보였다. 그래서 이제 <b>씬에 있는 클릭 차단막 전부</b>를 훑는다.
        //   새 표면이 생겨도 이 테스트를 고칠 필요 없이 자동으로 잡힌다.
        //
        // 식별 방법: 이 프로젝트의 차단막/히트타깃은 전부 씬 루트에 만들어지고 이름이
        // "...Blocker" 또는 "...ClickTarget"이다(CharacterInfoClickBlocker / SettingsClickBlocker /
        // TodoPostItClickBlocker / <팝오버이름>Blocker / InfoGearClickTarget).
        // 이름 규약이 깨지면 아래 최소 개수 단언이 먼저 실패한다.

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

        private IEnumerator WaitFrames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        // ==================== ①②③ ====================

        [UnityTest]
        public IEnumerator FullscreenDetectionHidesEveryUiSurfaceAndItsClickBlocker()
        {
            yield return LoadSceneAndResolve();
            yield return OpenEverything();

            // ① 숨기기 전에 실제로 켜져 있는가 — 이 단언이 없으면 "원래 꺼져 있어서 통과"가 된다.
            Assert.IsTrue(_gear.IsIconVisible, $"{LogPrefix} 준비 단계에서 톱니 그림이 이미 꺼져 있습니다.");
            Assert.IsTrue(_gear.IsClickBlockerEnabled, $"{LogPrefix} 준비 단계에서 톱니 차단막이 이미 꺼져 있습니다.");
            Assert.IsTrue(_menu.IsVisible, $"{LogPrefix} 준비 단계에서 부채꼴이 펼쳐지지 않았습니다.");
            Assert.IsTrue(_todo.IsOpen && _todo.IsCanvasActive && _todo.IsClickBlockerEnabled,
                $"{LogPrefix} 준비 단계에서 [오늘 할일] 팝오버가 열리지 않았습니다.");
            Assert.IsTrue(_window.IsOpen && _window.IsCanvasActive && _window.IsClickBlockerEnabled,
                $"{LogPrefix} 준비 단계에서 캐릭터 창이 열리지 않았습니다.");
            Assert.IsTrue(_postIt.IsCardVisible && _postIt.IsClickBlockerEnabled,
                $"{LogPrefix} 준비 단계에서 포스트잇 카드/차단막이 켜지지 않았습니다 — 미완료 할 일이 " +
                $"{TodoListModel.UncompletedCount}건입니다(0이면 17절 빈 상태 예외로 카드가 안 뜹니다).");

            int before = CountEnabledClickBlockers(out string beforeNames);
            Assert.GreaterOrEqual(before, 4,
                $"{LogPrefix} 켜진 차단막이 {before}개뿐입니다({beforeNames}) — 이름 규약이 바뀌었거나 " +
                "표면이 안 떠 있습니다. 이 최소 개수 단언이 없으면 아래 '전부 꺼졌다'가 " +
                "'애초에 하나도 못 찾았다'로도 통과합니다.");

            Debug.Log($"{LogPrefix} 준비 완료 — 톱니/부채꼴/팝오버/캐릭터 창/포스트잇 + 켜진 차단막 " +
                $"{before}개({beforeNames})가 모두 켜진 상태에서 전체화면 감지를 주입합니다.");

            // ② 전체화면 감지.
            SetSuspended(true);
            yield return WaitFrames(SettleFrames);

            Assert.IsFalse(_gear.IsIconVisible,
                $"{LogPrefix} 전체화면 감지 후에도 톱니 그림이 남아 있습니다(원칙 2 위반).");
            Assert.IsFalse(_gear.IsClickBlockerEnabled,
                $"{LogPrefix} 톱니 차단막이 살아 있습니다 — 안 보이는데 클릭만 먹는 최악의 형태입니다.");
            Assert.IsFalse(_menu.IsVisible,
                $"{LogPrefix} 전체화면 감지 후에도 부채꼴이 남아 있습니다.");
            Assert.IsFalse(_todo.IsOpen, $"{LogPrefix} 팝오버가 닫히지 않았습니다.");
            Assert.IsFalse(_todo.IsCanvasActive, $"{LogPrefix} 팝오버 캔버스가 켜진 채 남아 있습니다.");
            Assert.IsFalse(_todo.IsClickBlockerEnabled, $"{LogPrefix} 팝오버 차단막이 살아 있습니다(원칙 2 위반).");
            Assert.IsFalse(_window.IsOpen, $"{LogPrefix} 캐릭터 창이 닫히지 않았습니다.");
            Assert.IsFalse(_window.IsCanvasActive, $"{LogPrefix} 캐릭터 창 캔버스가 켜진 채 남아 있습니다.");
            Assert.IsFalse(_window.IsClickBlockerEnabled, $"{LogPrefix} 캐릭터 창 차단막이 살아 있습니다(원칙 2 위반).");
            Assert.IsFalse(_postIt.IsCardVisible,
                $"{LogPrefix} 전체화면 감지 후에도 포스트잇 카드가 게임 위에 남아 있습니다(원칙 2 위반).");
            Assert.IsFalse(_postIt.IsClickBlockerEnabled,
                $"{LogPrefix} 포스트잇 차단막이 살아 있습니다 — 안 보이는데 그 사각형의 클릭만 먹습니다.");

            // ★ 표면별 단언을 하나씩 적는 방식이 바로 이 결함을 놓쳤다(포스트잇만 명부에서 빠져
            //   있었고, 이름이 비슷한 TodoBoardPopover 때문에 검증된 것처럼 보였다). 그래서
            //   마지막에는 씬의 차단막을 <b>전수</b>로 훑는다 — 새 표면이 생겨도 자동으로 잡힌다.
            int during = CountEnabledClickBlockers(out string duringNames);
            Assert.AreEqual(0, during,
                $"{LogPrefix} 전체화면 감지 중인데 클릭 차단막 {during}개가 아직 켜져 있습니다: {duringNames}. " +
                "이 목록에 있는 표면이 IsSuspended를 폴링하지 않는 것입니다(원칙 2 위반).");

            Debug.Log($"{LogPrefix} 감지 중 — 모든 표면과 차단막이 전부 내려갔습니다. 이제 복귀를 확인합니다.");

            // ③ 복귀 — 톱니만 돌아온다.
            SetSuspended(false);
            yield return WaitFrames(SettleFrames);

            Assert.IsTrue(_gear.IsIconVisible,
                $"{LogPrefix} 전체화면이 끝났는데 톱니가 돌아오지 않았습니다(영구 실종).");
            Assert.IsTrue(_gear.IsClickBlockerEnabled,
                $"{LogPrefix} 톱니는 보이는데 차단막이 안 돌아왔습니다 — 클릭이 밑의 앱으로 샙니다.");
            Assert.IsFalse(_menu.IsVisible,
                $"{LogPrefix} 복귀와 동시에 부채꼴이 저절로 다시 펼쳐졌습니다 — 사용자가 부른 적이 없습니다.");
            Assert.IsFalse(_window.IsOpen,
                $"{LogPrefix} 복귀와 동시에 캐릭터 창이 저절로 다시 열렸습니다 — 그 자체가 방해입니다.");
            Assert.IsFalse(_todo.IsOpen,
                $"{LogPrefix} 복귀와 동시에 팝오버가 저절로 다시 열렸습니다.");

            // ★ 포스트잇은 톱니와 같은 편이다 — "사용자가 연 창"이 아니라 할 일이 있는 동안 늘
            //   떠 있는 상시 HUD라서, 게임이 끝나면 돌아오지 않으면 그것이 실종이다.
            Assert.IsTrue(_postIt.IsCardVisible,
                $"{LogPrefix} 전체화면이 끝났는데 포스트잇이 돌아오지 않았습니다(영구 실종). " +
                $"미완료 {TodoListModel.UncompletedCount}건.");
            Assert.IsTrue(_postIt.IsClickBlockerEnabled,
                $"{LogPrefix} 포스트잇은 보이는데 차단막이 안 돌아왔습니다 — 체크박스 클릭이 밑의 앱으로 샙니다.");

            Debug.Log($"{LogPrefix} 복귀 확인 — 톱니/포스트잇만 되살아나고 메뉴/창/팝오버는 닫힌 채 유지됩니다.");
        }

        // ==================== 네거티브 컨트롤 ====================

        /// <summary>
        /// 감지가 <b>걸리지 않았을 때</b>는 아무것도 사라지지 않는다 — 위 테스트가 "그냥 항상 꺼진다"를
        /// 보고 통과하는 것이 아님을 증명한다. 같은 대기 프레임 수, 같은 관측 지점을 쓴다.
        /// </summary>
        [UnityTest]
        public IEnumerator WithoutFullscreenDetectionNothingIsHidden()
        {
            yield return LoadSceneAndResolve();
            yield return OpenEverything();

            SetSuspended(false);
            yield return WaitFrames(SettleFrames);

            Assert.IsTrue(_gear.IsIconVisible, $"{LogPrefix} 감지가 없는데 톱니가 사라졌습니다.");
            Assert.IsTrue(_gear.IsClickBlockerEnabled, $"{LogPrefix} 감지가 없는데 톱니 차단막이 꺼졌습니다.");
            Assert.IsTrue(_menu.IsVisible, $"{LogPrefix} 감지가 없는데 부채꼴이 사라졌습니다.");
            Assert.IsTrue(_todo.IsOpen, $"{LogPrefix} 감지가 없는데 팝오버가 닫혔습니다.");
            Assert.IsTrue(_window.IsOpen, $"{LogPrefix} 감지가 없는데 캐릭터 창이 닫혔습니다.");
            Assert.IsTrue(_postIt.IsCardVisible, $"{LogPrefix} 감지가 없는데 포스트잇이 사라졌습니다.");
            Assert.IsTrue(_postIt.IsClickBlockerEnabled, $"{LogPrefix} 감지가 없는데 포스트잇 차단막이 꺼졌습니다.");

            int enabled = CountEnabledClickBlockers(out string names);
            Assert.Greater(enabled, 0,
                $"{LogPrefix} 감지가 없는데 켜진 차단막이 0개입니다 — 위 테스트의 '전부 꺼졌다'가 " +
                "그냥 항상 참이라는 뜻이 됩니다.");

            Debug.Log($"{LogPrefix} 네거티브 컨트롤 통과 — 감지가 없으면 모든 표면과 차단막 " +
                $"{enabled}개({names})가 그대로 유지됩니다.");
        }
    }
}
