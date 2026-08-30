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
    /// macOS 히트테스트가 커서 아래 픽셀 알파를 보므로 <b>보이는 데 그치지 않고 클릭까지 먹는다</b>.
    ///
    /// ============================================================================
    /// 절대 조건으로 잠그는 것 (상대 비교/플래그 확인 금지)
    /// ============================================================================
    ///  ① 숨기기 <b>전에</b> 다섯 표면이 실제로 켜져 있다 — 이 단계가 없으면 "원래 꺼져 있어서 통과"가 된다.
    ///  ② 감지 후: 톱니 그림 / 부채꼴 / 팝오버 / 캐릭터 창이 전부 꺼지고,
    ///     <b>클릭 차단막 3종(톱니·팝오버·창)도 함께 꺼진다</b> — 안 꺼지면 "안 보이는데 클릭만 먹는"
    ///     최악의 형태가 된다. 플래그가 아니라 GameObject/Collider의 <b>실제 상태</b>를 읽는다.
    ///  ③ 복귀하면 톱니는 다시 나타난다. 반대로 <b>메뉴/창/팝오버는 다시 열리지 않는다</b> —
    ///     사용자가 부르지도 않은 창이 게임을 끄자마자 튀어나오면 그 자체가 방해다(확정 설계).
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
        private StickmanAgent _agent;

        private StickConfig _config;
        private float _savedPollInterval;

        private string _backup;
        private bool _hadFile;

        private static readonly FieldInfo SuspendedField =
            typeof(StickmanAgent).GetField("_isSuspended", BindingFlags.Instance | BindingFlags.NonPublic);

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
            _agent = null;
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
            _agent = _gear.GetComponent<StickmanAgent>();

            Assert.IsNotNull(_window, $"{LogPrefix} CharacterInfoWindow가 없습니다.");
            Assert.IsNotNull(_menu, $"{LogPrefix} GearRadialMenuWidget이 없습니다.");
            Assert.IsNotNull(_todo, $"{LogPrefix} TodoBoardPopover가 없습니다.");
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
            for (int i = 0; i < SettleFrames; i++) yield return null;
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

            Debug.Log($"{LogPrefix} 준비 완료 — 톱니/부채꼴/팝오버/캐릭터 창 + 차단막 3종이 모두 켜진 상태에서 " +
                "전체화면 감지를 주입합니다.");

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

            Debug.Log($"{LogPrefix} 감지 중 — 다섯 표면과 차단막 3종이 전부 내려갔습니다. 이제 복귀를 확인합니다.");

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

            Debug.Log($"{LogPrefix} 복귀 확인 — 톱니만 되살아나고 메뉴/창/팝오버는 닫힌 채 유지됩니다.");
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

            Debug.Log($"{LogPrefix} 네거티브 컨트롤 통과 — 감지가 없으면 다섯 표면이 그대로 유지됩니다.");
        }
    }
}
