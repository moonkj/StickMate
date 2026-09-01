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
    /// <b>표시가 갈라질 수 있는 경로</b>가 열린다: 설정창에서 한 눈금 옮기고 구석 패널을 열었을 때
    /// 다이얼이 옛 값을 가리키면, "켜진 눈금 = 표시 숫자 = 실제 값"(34-3-4)이라는 구조적 보증이 깨진다.
    ///
    ///   (1) 슬라이더로 바꾸면 <b>다이얼/저장 모델/실제 캐릭터</b>가 전부 같은 값이 된다.
    ///   (2) 반대 방향(다이얼 → 슬라이더)도 같은 이벤트 하나로 흐른다.
    ///   (3) 적용 게이트(랙돌 중 유예 + 최대 3초 후 강제)는 <b>한 곳에만</b> 있고, 유예 중에도
    ///       두 UI의 <b>표시</b>는 사용자가 고른 값으로 즉시 같아진다(유예는 몸이 늦는 것이지
    ///       선택이 취소된 것이 아니다).
    ///   (4) 슬라이더는 컨트롤러와 <b>같은 격자</b>(CharacterScaleController.ValueStep)에 스냅된다 —
    ///       격자가 두 벌이면 같은 값이 두 UI에서 한 눈금씩 어긋나 보인다.
    ///
    /// ============================================================================
    /// 왜 <see cref="SettingsWindow.FeedClickForTests"/>로 누르는가
    /// ============================================================================
    /// PlayMode는 진짜 전역 클릭을 만들 수 없다(이 앱의 클릭 경로는 OS 커서 폴링이다).
    /// 그래서 <b>실제 입력과 완전히 같은 처리 경로</b>의 입구에 좌표를 먹인다 —
    /// <c>InfoGearIconWidget.FeedPointerForTests</c>가 확립한 관례다. 좌표는 손으로 적지 않고 <b>실제 부품의 화면 사각형</b>에서 얻는다
    /// (레이아웃이 바뀌면 엉뚱한 곳을 누르는 대신 테스트가 실패해야 한다).
    /// </summary>
    public sealed class SettingsCharacterScaleSingleSourceTests
    {
        private const string LogPrefix = "[크기단일소스-TEST]";

        private SettingsWindow _window;
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
            // 저장 복원은 CharacterProgressionDirector.Start가 CharacterSaveStore.Load() 직후에
            // 한 번만 한다(2026-09-01 구석 패널 삭제로 이사). 씬 로드 두 프레임 뒤면 이미 끝나 있다.
            yield return new WaitForSeconds(0.3f);
        }

        /// <summary>
        /// ★ 정적 상태를 <b>반드시</b> 되돌린다. <see cref="UiLayoutModel"/>/<see cref="CharacterScaleController"/>는
        /// 정적이라 씬을 다시 로드해도 살아남고, 그러면 <b>뒤에 오는 다른 스위트</b>가 이 테스트가 고른
        /// 배율로 캐릭터를 띄운 채 돌게 된다 — 2026-08-31에 하루치 PlayMode가 0.35배로 돌았던
        /// 그 사고의 전파 경로다.
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

        // ============================================================================
        // 목표 배율은 <b>숫자로 적지 않는다</b> — 상한에서 파생시킨다
        // ============================================================================

        /// <summary>
        /// 상한보다 <paramref name="steps"/> 눈금 아래의 배율. <b>값을 손으로 적지 않는 것이 요점이다.</b>
        ///
        /// <para>★ 2026-09-02 사고: 이 파일에 <c>1.20</c>/<c>1.35</c>가 박혀 있었고, 사용자 지시
        /// <i>"사이즈 max를 1배로 변경"</i>으로 <see cref="StickConfig.MaxCharacterScale"/>이 1.5 → 1.0이 된
        /// 순간 두 테스트가 <b>도달할 수 없는 값</b>을 눌렀다. 그때 로그는
        /// <c>설정창=1.00 / 저장모델=1.00 / 실캐릭터=1.000 / 컨트롤러=1.00</c>이었다 — <b>네 곳이 완벽히
        /// 일치</b>했고, 즉 이 파일이 잠그려던 사실(단일 소스)은 처음부터 멀쩡했다. <b>테스트만 낡았다.</b></para>
        ///
        /// <para>왜 하필 "상한보다 아래"인가: 목표가 상한에 <b>붙어</b> 있으면 "슬라이더가 누른 자리를
        /// 따라왔다"와 "clamp가 걸려서 우연히 같아졌다"를 구분할 수 없다 — 그 상태의 초록은 초록이
        /// 아니다. 그래서 아래에서 상한 미만임을 <b>테스트 자신이 확인</b>한다.</para>
        ///
        /// <para>★ 여기서 상수를 참조하는 것과, (5)에서 <c>1.0</c>을 직접 적는 것은 <b>모순이 아니다</b> —
        /// 잠그는 대상이 다르다. 근거는 그 테스트의 주석에 있다.</para>
        /// </summary>
        private static float TargetStepsBelowMax(int steps)
        {
            float target = CharacterScaleController.Snap(
                StickConfig.MaxCharacterScale - steps * CharacterScaleController.ValueStep);

            Assert.Less(target, StickConfig.MaxCharacterScale,
                $"{LogPrefix} 목표 배율 {target:F2}이 상한 {StickConfig.MaxCharacterScale:F2}에 붙어 있습니다 — " +
                "그러면 '슬라이더가 따라왔다'와 'clamp가 걸렸다'를 구분할 수 없어 이 테스트가 아무것도 " +
                "잠그지 못합니다. 눈금 수를 늘리거나 상한/눈금 간격을 다시 보세요.");
            Assert.GreaterOrEqual(target, StickConfig.MinCharacterScale,
                $"{LogPrefix} 목표 배율 {target:F2}이 하한 {StickConfig.MinCharacterScale:F2} 아래입니다 — " +
                $"상한({StickConfig.MaxCharacterScale:F2})과 눈금({CharacterScaleController.ValueStep:F2})으로는 " +
                $"{steps}눈금 아래를 만들 수 없습니다.");
            return target;
        }

        /// <summary>
        /// 목표가 <b>지금 값과 실제로 다른지</b> 확인한다. 같으면 트랙을 눌러도 값이 안 바뀌고
        /// (<c>SettingsSlider.SetValueFromUser</c>가 같은 값이면 조기 반환한다) 모든 단언이 통과해
        /// <b>거짓 초록</b>이 된다 — "아무 일도 안 일어났는데 전부 일치"는 이 스위트의 최악 실패 모드다.
        /// </summary>
        private static void AssertTargetIsRealChange(float target, float current)
        {
            Assert.Greater(Mathf.Abs(target - current), CharacterScaleController.ValueStep * 0.5f,
                $"{LogPrefix} 목표 {target:F2}이 지금 값 {current:F2}과 같은 눈금입니다 — 슬라이더를 눌러도 " +
                "아무 일이 일어나지 않으므로 이 테스트는 통과해도 아무것도 증명하지 않습니다. " +
                "기본 배율(StickConfig.characterScale)이 목표와 겹쳤는지 확인하세요.");
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
        // (1) 설정창 → 컨트롤러 / 저장 모델 / 실제 캐릭터
        // ============================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 설정창_슬라이더로_바꾸면_컨트롤러와_저장모델과_실캐릭터가_같은_값이_된다()
        {
            yield return LoadScene();
            yield return OpenCharacterTab();

            float Target = TargetStepsBelowMax(1);   // 상한 바로 아래 눈금 — 숫자를 적지 않는다.
            AssertTargetIsRealChange(Target, CharacterScaleController.Value);
            ClickTrackFor(Target);
            yield return null;

            Debug.Log($"{LogPrefix} 슬라이더 클릭 후 — 설정창={_window.DisplayedCharacterScale:F2}, " +
                $"저장모델={UiLayoutModel.CharacterScale:F2}, " +
                $"실캐릭터={_agent.CurrentCharacterScale:F3}, " +
                $"컨트롤러={CharacterScaleController.Value:F2}.");

            Assert.AreEqual(Target, _window.DisplayedCharacterScale, 1e-3f,
                $"{LogPrefix} 슬라이더가 누른 자리의 값을 가리키지 않습니다.");
            Assert.AreEqual(Target, CharacterScaleController.Value, 1e-3f,
                $"{LogPrefix} ★ 단일 소스가 옛 값({CharacterScaleController.Value:F2}×)을 들고 있습니다 — " +
                "슬라이더가 컨트롤러를 지나지 않고 어딘가에 직접 썼다는 뜻이고, 그 순간 '표시 숫자와 " +
                "실제 값이 다른' 화면이 가능해집니다(원칙 1).");
            Assert.AreEqual(Target, UiLayoutModel.CharacterScale, 1e-3f,
                $"{LogPrefix} 저장 모델이 따라오지 않았습니다 — 앱을 껐다 켜면 옛 크기로 돌아갑니다.");
            Assert.AreEqual(Target, _agent.CurrentCharacterScale, 1e-2f,
                $"{LogPrefix} 실제 캐릭터가 커지지 않았습니다(표시만 바뀌었습니다).");
        }

        // ============================================================================
        // (2) 반대 방향 — 설정창 <b>밖</b>에서 바꿔도 슬라이더가 따라온다
        // ============================================================================

        /// <summary>
        /// 2026-09-01 구석 다이얼이 삭제되어 "다른 UI"는 지금 없다. 그래도 이 방향을 계속 잠그는 이유:
        /// 설정창 밖에서 배율을 바꾸는 경로는 <b>지금도 있다</b>(저장 복원, 우클릭/단축키, 미래의 두 번째
        /// UI). 설정창이 구독을 놓치면 열어 둔 창의 숫자가 실제 캐릭터와 갈라진다(원칙 1) — 옛 다이얼이
        /// 있을 때와 <b>정확히 같은 결함</b>이다.
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 설정창_밖에서_바꾸면_슬라이더가_같은_프레임에_따라온다()
        {
            yield return LoadScene();
            yield return OpenCharacterTab();

            // 목표는 상한에서 파생시킨다 — 상한이 내려가면 손으로 적은 값은 조용히 사거리를 벗어난다.
            float target = TargetStepsBelowMax(3);
            AssertTargetIsRealChange(target, CharacterScaleController.Value);

            // 모든 UI가 지나는 그 문을 직접 두드린다(제스처를 흉내 내지 않는다).
            CharacterScaleController.Request(target, "설정창 밖 경로(테스트)");
            yield return null;

            Debug.Log($"{LogPrefix} 외부 경로 후 — 목표={target:F2}, 설정창={_window.DisplayedCharacterScale:F2}, " +
                $"컨트롤러={CharacterScaleController.Value:F2}, 실캐릭터={_agent.CurrentCharacterScale:F3}.");

            Assert.AreEqual(target, _window.DisplayedCharacterScale, 1e-3f,
                $"{LogPrefix} ★ 설정창 슬라이더가 외부 변경을 따라오지 않았습니다 — " +
                "설정창을 열어 둔 채 값이 바뀌면 두 숫자가 갈라집니다(원칙 1).");
            Assert.AreEqual(target, CharacterScaleController.Value, 1e-3f,
                $"{LogPrefix} 컨트롤러가 요청한 값을 반영하지 않았습니다.");
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

            float Target = TargetStepsBelowMax(2);
            AssertTargetIsRealChange(Target, before);
            ClickTrackFor(Target);
            yield return null;

            Debug.Log($"{LogPrefix} 랙돌 중 요청 — 유예={CharacterScaleController.HasPendingApply}, " +
                $"남은 시간={CharacterScaleController.PendingSecondsRemaining:F2}초, " +
                $"설정창={_window.DisplayedCharacterScale:F2}, " +
                $"실캐릭터={_agent.CurrentCharacterScale:F3}(요청 전 {before:F3}).");

            Assert.IsTrue(CharacterScaleController.HasPendingApply,
                $"{LogPrefix} 랙돌 중인데 유예가 걸리지 않았습니다 — 게이트가 사라졌습니다(34-3-6).");
            Assert.AreEqual(Target, _window.DisplayedCharacterScale, 1e-3f,
                $"{LogPrefix} 유예 중이라고 슬라이더가 되돌아가면 안 됩니다(선택은 취소되지 않았습니다).");
            Assert.AreEqual(Target, CharacterScaleController.Value, 1e-3f,
                $"{LogPrefix} 유예 중에도 '사용자가 고른 값'은 목표값이어야 합니다 — 유예는 몸이 늦는 " +
                "것이지 선택이 취소된 것이 아닙니다.");
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
        // (4) 슬라이더가 컨트롤러와 <b>같은 격자</b>에 스냅된다
        // ============================================================================

        /// <summary>
        /// 격자를 UI가 따로 들고 있으면 같은 값이 두 UI에서 한 눈금씩 어긋나 보인다(원칙 1).
        /// 옛 구석 다이얼과의 대조는 그 위젯이 삭제되면서 사라졌으므로, 이제는 <b>설정창 슬라이더가
        /// 자기 격자를 발명하지 않고 컨트롤러 상수를 그대로 참조하는가</b>를 잠근다.
        /// </summary>
        [Test]
        public void 슬라이더는_자기_격자를_발명하지_않고_컨트롤러_상수를_쓴다()
        {
            string settings = ReadProductionSource("SettingsWindow.cs");
            StringAssert.Contains("CharacterScaleController.ValueStep", settings,
                "[크기단일소스-TEST] 설정창 슬라이더가 컨트롤러의 스냅 간격을 참조하지 않습니다 — " +
                "격자가 두 벌이 되면 같은 값이 서로 다른 숫자로 보입니다(원칙 1).");

            // 컨트롤러의 스냅이 실제로 그 격자 위에 떨어지는지도 함께 본다(상수만 맞고 구현이
            // 어긋나는 경우를 막는다).
            float[] probes = { 0.34f, 0.371f, 0.774f, 1.126f, 1.49f, 1.73f };
            for (int i = 0; i < probes.Length; i++)
            {
                float snapped = CharacterScaleController.Snap(probes[i]);
                float steps = (snapped - StickConfig.MinCharacterScale) / CharacterScaleController.ValueStep;
                Assert.AreEqual(Mathf.Round(steps), steps, 1e-3f,
                    $"[크기단일소스-TEST] {probes[i]:F3}을 스냅한 {snapped:F3}이 " +
                    $"{CharacterScaleController.ValueStep:F2} 격자 위에 있지 않습니다.");
            }
        }

        /// <summary>프로덕션 소스를 문자열로 읽는다(구조 잠금 전용).</summary>
        private static string ReadProductionSource(string fileName)
        {
            string path = System.IO.Path.Combine(Application.dataPath, "_Project", "Scripts",
                "Interaction", fileName);
            Assert.IsTrue(System.IO.File.Exists(path),
                $"[크기단일소스-TEST] 프로덕션 파일을 찾지 못했습니다: {path}");
            return System.IO.File.ReadAllText(path);
        }

        // ============================================================================
        // (5) 상한은 StickConfig 하나에서만 온다 (2026-08-31 2.0 → 1.5 지시가 새지 않게)
        // ============================================================================

        [Test]
        public void 슬라이더_상한은_StickConfig_MaxCharacterScale을_그대로_따른다()
        {
            // ★ 이 단언은 CLAUDE.md "테스트에 프로덕션 상수를 숫자로 베끼지 않는다"의 **의도된 예외**다.
            //   여기서 잠그는 것은 "구현이 상수와 일치하는가"(그건 아래 두 단언이 한다)가 아니라
            //   **"사용자가 고른 값이 조용히 되돌려지지 않았는가"**다. 그래서 값을 직접 적는 것이
            //   목적 그 자체이며, 상수를 참조하면 이 단언은 항상 참이 되어 아무것도 잠그지 못한다.
            //   값을 바꾸려면 사용자 지시가 있어야 하고, 그 지시를 아래 메시지에 함께 남긴다.
            Assert.AreEqual(1.0f, StickConfig.MaxCharacterScale, 1e-4f,
                "[크기단일소스-TEST] 상한이 1.0이 아닙니다 — 2026-09-01 사용자 지시 '사이즈 max를 " +
                "1배로 변경'이 되돌려졌습니다. (이력: 2026-08-31 'max를 1.5까지만'으로 2.0 → 1.5, " +
                "2026-09-01 1.5 → 1.0.) 이 값을 올리면 Dock 등반 결함 구간(배율 > 1.125)이 다시 " +
                "사거리에 들어옵니다 — StickConfig.MaxCharacterScale 문서 참고.");
            Assert.AreEqual(StickConfig.MaxCharacterScale, CharacterScaleController.Snap(99f), 1e-4f,
                "[크기단일소스-TEST] 컨트롤러가 상한 위의 값을 잘라 내지 않습니다.");
            Assert.AreEqual(StickConfig.MinCharacterScale, CharacterScaleController.Snap(-1f), 1e-4f,
                "[크기단일소스-TEST] 컨트롤러가 하한 아래의 값을 잘라 내지 않습니다.");
        }
    }
}
