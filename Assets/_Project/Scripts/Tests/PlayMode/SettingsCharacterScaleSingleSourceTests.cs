using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ <b>캐릭터 크기의 진실은 하나다</b> — docs/UX_FLOW.md 35-1-3 ①, 2026-09-01 설정창 라운드.
    ///
    /// ============================================================================
    /// 무엇을 잠그는가 (절대 불변 원칙 1의 이 라운드판)
    /// ============================================================================
    /// 어제까지 배율을 바꾸는 UI는 구석 호버 다이얼 하나뿐이었다. 설정창 슬라이더가 생기는 순간
    /// <b>표시가 갈라질 수 있는 경로</b>가 열린다: 설정창에서 1.20×로 바꾸고 구석 패널을 열었을 때
    /// 다이얼이 옛 값을 가리키면, "켜진 눈금 = 표시 숫자 = 실제 값"(34-3-4)이라는 구조적 보증이 깨진다.
    ///
    ///   (1) 슬라이더로 바꾸면 <b>다이얼/저장 모델/실제 캐릭터</b>가 전부 같은 값이 된다.
    ///   (2) 반대 방향(다이얼 → 슬라이더)도 같은 이벤트 하나로 흐른다.
    ///   (3) 적용 게이트(랙돌 중 유예 + 최대 3초 후 강제)는 <b>한 곳에만</b> 있고, 유예 중에도
    ///       두 UI의 <b>표시</b>는 사용자가 고른 값으로 즉시 같아진다(유예는 몸이 늦는 것이지
    ///       선택이 취소된 것이 아니다).
    ///   (4) 슬라이더는 다이얼과 <b>같은 0.05 격자</b>에 스냅된다 — 격자가 두 벌이면 같은 값이
    ///       한쪽에서 1.15, 다른 쪽에서 1.20으로 보인다.
    ///
    /// ============================================================================
    /// 왜 <see cref="SettingsWindow.FeedClickForTests"/>로 누르는가
    /// ============================================================================
    /// PlayMode는 진짜 전역 클릭을 만들 수 없다(이 앱의 클릭 경로는 OS 커서 폴링이다).
    /// 그래서 <b>실제 입력과 완전히 같은 처리 경로</b>의 입구에 좌표를 먹인다 —
    /// <c>InfoGearIconWidget.FeedPointerForTests</c> / <c>CornerHoverPanel.FeedPointerForTests</c>가
    /// 확립한 관례다. 좌표는 손으로 적지 않고 <b>실제 부품의 화면 사각형</b>에서 얻는다
    /// (레이아웃이 바뀌면 엉뚱한 곳을 누르는 대신 테스트가 실패해야 한다).
    /// </summary>
    public sealed class SettingsCharacterScaleSingleSourceTests
    {
        private const string LogPrefix = "[크기단일소스-TEST]";

        private SettingsWindow _window;
        private CornerHoverPanel _panel;
        private StickmanAgent _agent;

        private IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다.");
            _window = Object.FindFirstObjectByType<SettingsWindow>();
            Assert.IsNotNull(_window, $"{LogPrefix} 씬에서 SettingsWindow를 찾지 못했습니다 — " +
                "Assets/Editor/SceneBootstrapper.cs의 EnsurePrefabComponents가 프리팹에 이 컴포넌트를 " +
                "붙였는지 확인하세요(33-9 #10 / 34-9 #10과 같은 함정).");
            _panel = Object.FindFirstObjectByType<CornerHoverPanel>();
            Assert.IsNotNull(_panel, $"{LogPrefix} 씬에서 CornerHoverPanel을 찾지 못했습니다.");

            yield return new WaitForSeconds(0.3f);   // 저장 복원(RestoreSavedScale)이 끝날 시간을 준다.
        }

        /// <summary>
        /// ★ 정적 상태를 <b>반드시</b> 되돌린다. <see cref="UiLayoutModel"/>/<see cref="CharacterScaleController"/>는
        /// 정적이라 씬을 다시 로드해도 살아남고, 그러면 <b>뒤에 오는 다른 스위트</b>가 이 테스트가 고른
        /// 배율(1.35× 등)로 캐릭터를 띄운 채 돌게 된다 — 2026-08-31에 하루치 PlayMode가 0.35배로 돌았던
        /// 그 사고의 전파 경로다(<c>CornerHoverPanelTests</c>가 같은 이유로 같은 정리를 한다).
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_window != null) _window.Close("테스트 정리");
            if (_agent != null && _agent.Config != null) _agent.Config.ClearRuntimeCharacterScale();
            CharacterScaleController.ResetForTesting();
            UiLayoutModel.ResetForTesting();
            // ★ 저장 파일까지 되돌린다. 이 테스트들은 <b>제품 경로</b>로 값을 바꾸므로 설정창이 실제로
            //   CharacterSaveStore.Save()를 부른다. PlayMode 저장 경로는 임시 폴더로 리디렉션돼 있지만
            //   그 파일은 <b>실행과 실행 사이에 남는다</b> — 그대로 두면 다음 실행의 씬 로드가 이 값을
            //   복원해 "씬 로드 직후인데 런타임 배율이 이미 설정돼 있다"로 다른 스위트를 깨뜨린다
            //   (실제로 DeployedConfigAssetImmutabilityTests가 그렇게 실패했다).
            //   모델을 비운 <b>뒤에</b> 한 번 더 저장해 파일을 기본값 상태로 되돌린다.
            CharacterSaveStore.Save();
            _window = null;
            _panel = null;
            _agent = null;
        }

        /// <summary>[캐릭터] 탭으로 전환하고 슬라이더가 화면에 나올 때까지 기다린다.</summary>
        private IEnumerator OpenCharacterTab()
        {
            _window.Open("테스트");
            yield return null;
            _window.FeedClickForTests(_window.TabScreenRect(SettingsWindow.Tab.Character).center);
            yield return null;
            Assert.AreEqual(SettingsWindow.Tab.Character, _window.ActiveTab,
                $"{LogPrefix} [캐릭터] 탭으로 전환되지 않았습니다 — 탭 클릭 경로가 죽었습니다.");
        }

        /// <summary>트랙 위에서 <paramref name="value"/>에 해당하는 지점을 누른다(실제 클릭 경로).</summary>
        private void ClickTrackFor(float value)
        {
            Rect track = _window.CharacterScaleTrackScreenRect;
            Assert.Greater(track.width, 1f, $"{LogPrefix} 슬라이더 트랙의 화면 사각형이 비어 있습니다 " +
                "([캐릭터] 탭이 활성인지, 마스크에 잘리지 않았는지 확인).");
            float t = Mathf.Clamp01((value - StickConfig.MinCharacterScale) /
                (StickConfig.MaxCharacterScale - StickConfig.MinCharacterScale));
            _window.FeedClickForTests(new Vector2(track.xMin + track.width * t, track.center.y));
        }

        // ============================================================================
        // (1) 설정창 → 다이얼 / 저장 모델 / 실제 캐릭터
        // ============================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 설정창_슬라이더로_바꾸면_구석_다이얼과_저장모델과_실캐릭터가_같은_값이_된다()
        {
            yield return LoadScene();
            yield return OpenCharacterTab();

            const float Target = 1.20f;   // 0.05 격자 위의 값이면서 기본값(0.75)과 확실히 다르다.
            ClickTrackFor(Target);
            yield return null;

            Debug.Log($"{LogPrefix} 슬라이더 클릭 후 — 설정창={_window.DisplayedCharacterScale:F2}, " +
                $"구석 다이얼={_panel.DialValue:F2}, 저장모델={UiLayoutModel.CharacterScale:F2}, " +
                $"실캐릭터={_agent.CurrentCharacterScale:F3}, " +
                $"컨트롤러={CharacterScaleController.Value:F2}.");

            Assert.AreEqual(Target, _window.DisplayedCharacterScale, 1e-3f,
                $"{LogPrefix} 슬라이더가 누른 자리의 값을 가리키지 않습니다.");
            Assert.AreEqual(Target, _panel.DialValue, 1e-3f,
                $"{LogPrefix} ★ 구석 다이얼이 옛 값({_panel.DialValue:F2}×)을 가리키고 있습니다 — " +
                "설정창에서 바꾼 뒤 구석 패널을 열면 '표시 숫자와 실제 값이 다른' 화면이 됩니다. " +
                "이것이 35-1-3 ①이 경고한 원칙 1 위반이며, StickmanEventBus.CharacterScaleChanged " +
                "구독이 끊겼을 때 정확히 이렇게 실패합니다.");
            Assert.AreEqual(Target, UiLayoutModel.CharacterScale, 1e-3f,
                $"{LogPrefix} 저장 모델이 따라오지 않았습니다 — 앱을 껐다 켜면 옛 크기로 돌아갑니다.");
            Assert.AreEqual(Target, _agent.CurrentCharacterScale, 1e-2f,
                $"{LogPrefix} 실제 캐릭터가 커지지 않았습니다(표시만 바뀌었습니다).");
        }

        // ============================================================================
        // (2) 반대 방향 — 다이얼이 바꾸면 설정창이 따라온다
        // ============================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 구석_다이얼_경로로_바꾸면_설정창_슬라이더가_같은_프레임에_따라온다()
        {
            yield return LoadScene();
            yield return OpenCharacterTab();

            // 다이얼이 손을 뗄 때 실제로 부르는 것과 <b>같은 한 줄</b>이다
            // (CornerHoverPanel.OnDialValueChanged). 드래그 제스처를 흉내 내는 대신 그 문을 직접 두드린다.
            CharacterScaleController.Request(0.60f, "구석 다이얼(테스트)");
            yield return null;

            Debug.Log($"{LogPrefix} 다이얼 경로 후 — 설정창={_window.DisplayedCharacterScale:F2}, " +
                $"구석 다이얼={_panel.DialValue:F2}, 실캐릭터={_agent.CurrentCharacterScale:F3}.");

            Assert.AreEqual(0.60f, _window.DisplayedCharacterScale, 1e-3f,
                $"{LogPrefix} ★ 설정창 슬라이더가 다이얼의 변경을 따라오지 않았습니다 — " +
                "설정창을 열어 둔 채 다이얼을 돌리면 두 숫자가 갈라집니다(원칙 1).");
            Assert.AreEqual(0.60f, _panel.DialValue, 1e-3f,
                $"{LogPrefix} 다이얼 자신이 요청한 값을 반영하지 않았습니다.");
        }

        // ============================================================================
        // (3) 적용 게이트는 한 곳에만 있다 — 유예 중에도 표시는 즉시 같아진다
        // ============================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 랙돌_중_요청은_적용이_유예되지만_두_UI의_표시는_즉시_같아진다()
        {
            yield return LoadScene();
            yield return OpenCharacterTab();

            float before = _agent.CurrentCharacterScale;
            StickmanStateMachine machine = _agent.Blackboard != null ? _agent.Blackboard.Machine : null;
            Assert.IsNotNull(machine, $"{LogPrefix} 상태 머신이 없습니다.");
            machine.ChangeState(StickmanStateId.Ragdoll, isForcedInterrupt: true);
            yield return null;

            const float Target = 1.35f;
            ClickTrackFor(Target);
            yield return null;

            Debug.Log($"{LogPrefix} 랙돌 중 요청 — 유예={CharacterScaleController.HasPendingApply}, " +
                $"남은 시간={CharacterScaleController.PendingSecondsRemaining:F2}초, " +
                $"설정창={_window.DisplayedCharacterScale:F2}, 다이얼={_panel.DialValue:F2}, " +
                $"실캐릭터={_agent.CurrentCharacterScale:F3}(요청 전 {before:F3}).");

            Assert.IsTrue(CharacterScaleController.HasPendingApply,
                $"{LogPrefix} 랙돌 중인데 유예가 걸리지 않았습니다 — 게이트가 사라졌습니다(34-3-6).");
            Assert.AreEqual(Target, _window.DisplayedCharacterScale, 1e-3f,
                $"{LogPrefix} 유예 중이라고 슬라이더가 되돌아가면 안 됩니다(선택은 취소되지 않았습니다).");
            Assert.AreEqual(Target, _panel.DialValue, 1e-3f,
                $"{LogPrefix} 유예 중에도 두 UI의 표시는 같아야 합니다.");
            Assert.AreEqual(before, _agent.CurrentCharacterScale, 1e-2f,
                $"{LogPrefix} 랙돌 중인데 실캐릭터가 즉시 커졌습니다 — 게이트가 통과됐습니다.");

            // 최대 3초 뒤에는 상태와 무관하게 들어간다(연출 유예이지 안전 문제가 아니라는 실측 결론).
            yield return new WaitForSecondsRealtime(CharacterScaleController.PendingForceSeconds + 0.4f);

            Debug.Log($"{LogPrefix} 강제 적용 후 — 유예={CharacterScaleController.HasPendingApply}, " +
                $"실캐릭터={_agent.CurrentCharacterScale:F3}.");

            Assert.IsFalse(CharacterScaleController.HasPendingApply,
                $"{LogPrefix} {CharacterScaleController.PendingForceSeconds:F0}초가 지났는데 유예가 남아 있습니다 — " +
                "강제 적용이 죽었습니다(그 경우 랙돌이 길어지면 크기 변경이 영원히 반영되지 않습니다).");
            Assert.AreEqual(Target, _agent.CurrentCharacterScale, 1e-2f,
                $"{LogPrefix} 강제 적용 뒤에도 실캐릭터가 목표 배율이 아닙니다.");
        }

        // ============================================================================
        // (4) 두 UI가 <b>같은 격자</b>에 스냅된다
        // ============================================================================

        [Test]
        public void 슬라이더와_다이얼은_같은_0_05_격자를_쓴다()
        {
            Assert.AreEqual(CharacterScaleController.ValueStep, SizeDialWidget.ValueStep, 1e-6f,
                "[크기단일소스-TEST] 다이얼과 컨트롤러의 스냅 간격이 다릅니다 — 같은 값이 한쪽에서 " +
                "1.15, 다른 쪽에서 1.20으로 보이게 됩니다(원칙 1).");

            // 격자 밖의 값을 두 경로에 각각 넣어 <b>같은 답</b>이 나오는지 본다.
            float[] probes = { 0.34f, 0.371f, 0.774f, 1.126f, 1.49f, 1.73f };
            for (int i = 0; i < probes.Length; i++)
            {
                float viaController = CharacterScaleController.Snap(probes[i]);
                float viaDial = SizeDialWidget.IndexToValue(SizeDialWidget.ValueToIndex(probes[i]));
                Assert.AreEqual(viaDial, viaController, 1e-4f,
                    $"[크기단일소스-TEST] {probes[i]:F3}을 스냅한 결과가 갈립니다 " +
                    $"(다이얼 {viaDial:F2} vs 컨트롤러 {viaController:F2}).");
            }
        }

        // ============================================================================
        // (5) 상한은 StickConfig 하나에서만 온다 (2026-08-31 2.0 → 1.5 지시가 새지 않게)
        // ============================================================================

        [Test]
        public void 슬라이더_상한은_StickConfig_MaxCharacterScale을_그대로_따른다()
        {
            Assert.AreEqual(1.5f, StickConfig.MaxCharacterScale, 1e-4f,
                "[크기단일소스-TEST] 상한이 1.5가 아닙니다 — 2026-08-31 사용자 지시 '캐릭터 사이즈는 " +
                "max를 1.5까지만'이 되돌려졌습니다.");
            Assert.AreEqual(StickConfig.MaxCharacterScale, CharacterScaleController.Snap(99f), 1e-4f,
                "[크기단일소스-TEST] 컨트롤러가 상한 위의 값을 잘라 내지 않습니다.");
            Assert.AreEqual(StickConfig.MinCharacterScale, CharacterScaleController.Snap(-1f), 1e-4f,
                "[크기단일소스-TEST] 컨트롤러가 하한 아래의 값을 잘라 내지 않습니다.");
        }
    }
}
