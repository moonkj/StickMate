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
    /// ★ "보이지 않는 것은 눌리지 않는다" — 2026-08-30 R2 M3 회귀.
    ///
    /// ============================================================================
    /// 무엇이 문제였나
    /// ============================================================================
    /// 세로가 짧은 화면(1366x768 노트북)에서는 <c>CharacterInfoWindow.ClampPanelToScreen</c>이 창 높이를
    /// 줄이고, 본문 아래쪽([착용]/[해제] 버튼이 있는 상세 패널)이 <see cref="UnityEngine.UI.RectMask2D"/>에
    /// 잘려 <b>화면에서 사라진다</b>. 그런데 전역 폴링 히트테스트는 마스크를 모르는 순수 사각형 판정이라
    /// 그 자리가 계속 눌렸다 — 이 프로젝트가 <b>최악의 형태</b>라고 부르는 패턴(안 보이는데 클릭은 먹는 UI)이며,
    /// 전체화면 감지 때 차단막을 함께 끄는 원칙과 정확히 같은 규칙이다.
    ///
    /// ============================================================================
    /// 절대 조건으로 잠그는 것
    /// ============================================================================
    ///  ① <b>양성 대조</b> — 넉넉한 높이에서는 [착용] 버튼이 100% 보이고 실제로 눌린다(눌러서 착용이 바뀐다).
    ///  ② 화면이 낮아 버튼이 <b>0% 보이게</b> 되면 같은 좌표를 눌러도 <b>아무 일도 일어나지 않는다</b>
    ///     (착용 상태 서명이 그대로다). 플래그가 아니라 실제 클릭 경로(FeedClickForTests)로 확인한다.
    ///  ③ 높이를 되돌리면 다시 눌린다(기능을 죽인 것이 아니라 가려진 동안만 막는다).
    ///
    /// 화면 높이를 배치 실행에서 바꿀 수단이 없어, 실제 클램프 함수에 <b>스케일 팩터</b>를 주입해
    /// 같은 계산 경로로 창을 줄인다(available = Screen.height / scaleFactor − 여백). 리플렉션을 쓰는 이유는
    /// FullscreenSuspendUiHidingTests와 같다 — 실경로에 주입 지점이 없고, 소비자가 읽는 값은 동일하다.
    /// </summary>
    public sealed class InfoWindowClippedHitTestTests
    {
        private const string LogPrefix = "[가려진클릭-TEST]";

        private static readonly MethodInfo ClampMethod = typeof(CharacterInfoWindow).GetMethod(
            "ClampPanelToScreen", BindingFlags.Instance | BindingFlags.NonPublic);

        private CharacterInfoWindow _window;
        private string _backup;
        private bool _hadFile;

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

        [UnityTearDown]
        public IEnumerator TearDownAll()
        {
            if (_window != null && _window.IsOpen) _window.Close("테스트 정리");
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            _window = null;
            yield return null;
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ClippedActionButtonIsNotClickableAndComesBackWhenVisibleAgain()
        {
            yield return LoadSceneAndOpenWindow();

            Assert.IsNotNull(ClampMethod, $"{LogPrefix} ClampPanelToScreen을 찾지 못했습니다 — 이름이 바뀌었습니다.");

            // ★ 프레임을 넘기지 않고 측정한다 — Update가 매 프레임 실제 화면 크기로 다시 클램프하므로
            //   주입한 크기는 그 프레임 안에서만 유효하다(레이아웃 그룹이 없어 코너는 즉시 갱신된다).

            // ---- ① 양성 대조: 넉넉한 세로에서는 100% 보이고 실제로 눌린다 ----
            Clamp(ScaleFactorForTallScreen());

            Assert.Greater(_window.ActionButtonVisibleFraction, 0.99f,
                $"{LogPrefix} 창을 키웠는데도 [착용] 버튼이 잘려 있습니다 — 관측 전제가 성립하지 않습니다.");

            Vector2 visibleCenter = _window.ActionButtonRawScreenRect.center;
            Assert.IsTrue(_window.IsActionButtonHittableAt(visibleCenter),
                $"{LogPrefix} 보이는데도 히트테스트가 거부했습니다.");

            int before = EquipmentModel.WornStateSignature;
            _window.FeedClickForTests(visibleCenter);
            Assert.AreNotEqual(before, EquipmentModel.WornStateSignature,
                $"{LogPrefix} 보이는 [착용] 버튼을 눌렀는데 착용 상태가 그대로입니다 — " +
                "이 양성 대조가 없으면 아래 '안 눌린다'는 기능이 죽어도 통과합니다.");

            // 클릭 중복 억제(0.35초)를 확실히 지나 보낸다 — 아래 '안 눌린다'가 중복 억제 덕분에
            // 통과하는 가짜 초록이 되지 않게.
            yield return new WaitForSecondsRealtime(0.6f);

            // ---- ② 세로가 짧아 통째로 잘리면 같은 자리를 눌러도 아무 일도 없다 ----
            Clamp(ScaleFactorForShortScreen());

            Assert.AreEqual(0f, _window.ActionButtonVisibleFraction, 1e-4f,
                $"{LogPrefix} 창을 최소 높이로 줄였는데 [착용] 버튼이 아직 보입니다 — 이 화면에서는 관측할 수 없습니다.");

            Vector2 hiddenCenter = _window.ActionButtonRawScreenRect.center;
            Assert.IsFalse(_window.IsActionButtonHittableAt(hiddenCenter),
                $"{LogPrefix} 마스크에 완전히 잘린 버튼이 여전히 히트테스트를 통과합니다 — " +
                "안 보이는데 눌리는 최악의 형태입니다.");

            int clippedBefore = EquipmentModel.WornStateSignature;
            _window.FeedClickForTests(hiddenCenter);
            Assert.AreEqual(clippedBefore, EquipmentModel.WornStateSignature,
                $"{LogPrefix} 보이지 않는 [착용] 버튼 자리를 눌렀는데 착용 상태가 바뀌었습니다.");

            // ★ 2026-08-30: 그 자리는 줄어든 창 <b>바깥</b>이기도 하다 — 33-7-9의 "창 밖 클릭" 탈출구가
            //   생기면서 이 클릭은 창을 닫는다(보이는 창 사각형이 판단 기준이다). 착용이 바뀌지 않았다는
            //   위 단언은 그대로 유효하고, ③을 이어가려면 창을 다시 열어야 한다.
            Assert.IsFalse(_window.IsOpen,
                $"{LogPrefix} 줄어든 창 바깥을 눌렀는데 창이 닫히지 않았습니다 — 33-7-9의 창 밖 클릭 탈출구가 없습니다.");
            _window.Open("가려진 클릭 테스트 — 재개");
            yield return null;

            // ---- ③ 되돌리면 다시 눌린다(가려진 동안만 막는 것이지 기능을 죽인 것이 아니다) ----
            Clamp(ScaleFactorForTallScreen());

            Assert.Greater(_window.ActionButtonVisibleFraction, 0.99f,
                $"{LogPrefix} 높이를 되돌렸는데 버튼이 여전히 잘려 있습니다.");
            Assert.IsTrue(_window.IsActionButtonHittableAt(_window.ActionButtonRawScreenRect.center),
                $"{LogPrefix} 높이를 되돌렸는데 히트테스트가 계속 거부합니다 — 기능을 죽였습니다.");

            Debug.Log($"{LogPrefix} 통과 — 보이면 눌리고, 가려지면 안 눌리고, 되돌리면 다시 눌립니다.");
            yield return null;
        }

        private void Clamp(float scaleFactor) => ClampMethod.Invoke(_window, new object[] { scaleFactor });

        /// <summary>창이 설계 높이(861)를 다 쓸 만큼 <b>세로가 넉넉한</b> 화면을 흉내내는 스케일 팩터.
        /// 배치 실행의 실제 화면은 작을 수 있어 상수로 적으면 이 테스트가 조용히 무의미해진다.</summary>
        private static float ScaleFactorForTallScreen() => Mathf.Max(0.01f, Screen.height / 1200f);

        /// <summary>상세 패널이 통째로 잘리도록 창을 클램프 하한(320)까지 줄이는 스케일 팩터.</summary>
        private static float ScaleFactorForShortScreen() => Mathf.Max(0.01f, Screen.height / 300f);

        private IEnumerator LoadSceneAndOpenWindow()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _window = Object.FindFirstObjectByType<CharacterInfoWindow>();
            Assert.IsNotNull(_window, $"{LogPrefix} 씬에서 CharacterInfoWindow를 찾지 못했습니다.");

            _window.Open("가려진 클릭 테스트");
            yield return null;
            yield return null;
            Assert.IsTrue(_window.IsOpen, $"{LogPrefix} 창이 열리지 않았습니다.");
        }
    }
}
