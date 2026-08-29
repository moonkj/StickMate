using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 정보창 초상화 촬영장(Interaction/CharacterPortraitStage.cs) 회귀 테스트 —
    /// 2026-08-30 정보창 리디자인 라운드.
    ///
    /// ============================================================================
    /// 왜 Main.unity를 실제로 로드하는가
    /// ============================================================================
    /// 이 프로젝트가 여러 번 반복한 실패 모드는 "로직은 완성됐는데 씬에 배치가 안 돼 화면에 한 픽셀도
    /// 안 나온다"이다. 그래서 컴포넌트를 테스트 안에서 새로 만들지 않고 <b>실제 씬</b>을 로드한다.
    ///
    /// ============================================================================
    /// 절대 조건으로 단언하는 것
    /// ============================================================================
    ///  ① 정보창/기록 디렉터가 <b>정확히 1개씩</b>(0=배치 누락, 2=라이벌 복제본에서 미제거).
    ///  ② 촬영장은 메인 카메라가 <b>절대 볼 수 없는 곳</b>에 있다 — 이 구현이 메인 카메라의
    ///     cullingMask를 건드리지 않고 격리를 얻는 유일한 근거이므로, 거리로 직접 단언한다.
    ///  ③ 촬영장에 <b>Collider가 0개</b>다(관전 전용 = 콜라이더 0개 규칙, 물리 비개입).
    ///  ④ 카메라는 <b>창이 닫혀 있는 동안 꺼져 있고</b> 열면 켜진다(상시 렌더 비용 0).
    ///  ⑤ RT는 표시 크기보다 <b>큰 픽셀 수</b>로 만들어진다(Retina에서 뭉개지지 않는다 — 2026-08-29
    ///     "선 화질 조사" 라운드의 교훈).
    ///  ⑥ 포즈가 상태에서 파생되고, 가출(Hidden) 포즈에서는 <b>선이 하나도 그려지지 않는다</b>
    ///     (없는 사람을 그리지 않는다).
    /// </summary>
    public sealed class CharacterPortraitStageTests
    {
        private CharacterInfoWindow _window;

        [UnityTearDown]
        public IEnumerator CloseWindow()
        {
            if (_window != null) _window.Close("테스트 정리");
            _window = null;
            yield return null;
        }

        private IEnumerator LoadSceneAndResolve()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _window = ExactlyOne<CharacterInfoWindow>();
        }

        private static T ExactlyOne<T>() where T : Object
        {
            var found = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            Assert.AreEqual(1, found.Length,
                $"씬의 {typeof(T).Name} 개수가 {found.Length}개입니다 — 1개여야 합니다. " +
                "0개면 SceneBootstrapper 배치 누락, 2개 이상이면 라이벌 복제본에서 제거되지 않은 것입니다.");
            return found[0];
        }

        [UnityTest]
        public IEnumerator WindowAndStatsDirectorArePlacedExactlyOnce()
        {
            yield return LoadSceneAndResolve();

            var stats = ExactlyOne<CharacterStatsDirector>();
            var progression = ExactlyOne<CharacterProgressionDirector>();
            Assert.IsNotNull(stats);
            Assert.IsNotNull(progression);

            // 기록 디렉터가 두 개면 격파/대결/활쏘기 기록이 두 배로 쌓인다(라이벌 복제 사고의 재현).
            Debug.Log("[초상화테스트] 배선 검증 통과 — 정보창/기록 디렉터/성장 디렉터가 각각 1개.");
        }

        [UnityTest]
        public IEnumerator StageSitsFarOutsideMainCameraViewAndHasNoColliders()
        {
            yield return LoadSceneAndResolve();

            _window.Open("테스트");
            yield return null;
            yield return null;

            var stage = ExactlyOne<CharacterPortraitStage>();

            Camera main = Camera.main;
            Assert.IsNotNull(main, "메인 카메라가 없습니다.");
            float halfWidth = main.orthographicSize * main.aspect;
            float distance = Mathf.Abs(stage.transform.position.x - main.transform.position.x);
            Assert.Greater(distance, halfWidth * 4f,
                $"촬영장이 메인 카메라 가시 범위(반폭 {halfWidth:F1}유닛)에 너무 가깝습니다({distance:F1}유닛) — " +
                "미니 피규어가 바탕화면에 그대로 보이게 됩니다. 이 구현은 거리로 격리를 얻습니다.");

            Assert.AreEqual(0, stage.GetComponentsInChildren<Collider2D>(true).Length,
                "촬영장에 Collider2D가 있습니다 — 관전 전용 오브젝트는 콜라이더가 0개여야 합니다(물리 비개입).");
            Assert.AreEqual(0, stage.GetComponentsInChildren<Collider>(true).Length,
                "촬영장에 3D Collider가 있습니다.");
            Assert.AreEqual(0, stage.GetComponentsInChildren<Rigidbody2D>(true).Length,
                "촬영장에 Rigidbody2D가 있습니다 — 포즈는 전부 정적 좌표여야 합니다.");
        }

        [UnityTest]
        public IEnumerator PortraitCameraRunsOnlyWhileWindowIsOpen()
        {
            yield return LoadSceneAndResolve();

            _window.Open("테스트");
            yield return null;
            yield return null;

            var stage = ExactlyOne<CharacterPortraitStage>();
            Camera portraitCamera = stage.GetComponentInChildren<Camera>(true);
            Assert.IsNotNull(portraitCamera, "촬영장 카메라가 없습니다.");

            // ★ 헤드리스(-batchmode -nographics)에서는 RT를 만들지 않는 것이 <b>정상</b>이다:
            //   오프스크린 카메라가 선을 그리려다 프로세스가 통째로 죽는다(실측 EXIT=139,
            //   네이티브 스택 RenderManager::RenderOffscreenCameras). 그래서 두 갈래로 단언한다.
            bool headless = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
            if (headless)
            {
                Assert.IsFalse(stage.HasTexture, "헤드리스에서는 RT를 만들지 않아야 합니다(배치 모드 크래시 방지).");
                Assert.IsFalse(portraitCamera.enabled, "헤드리스에서는 초상화 카메라가 꺼져 있어야 합니다.");
            }
            else
            {
                Assert.IsTrue(portraitCamera.enabled, "창이 열려 있는데 초상화 카메라가 꺼져 있습니다.");
                Assert.IsTrue(stage.HasTexture, "RenderTexture가 만들어지지 않았습니다.");
                Assert.AreSame(stage.Texture, portraitCamera.targetTexture,
                    "카메라가 다른 타깃을 그리고 있습니다 — 초상화가 화면에 직접 그려질 위험이 있습니다.");

                // ⑤ Retina 대응: 표시 크기(약 176x214pt)보다 픽셀 수가 커야 뭉개지지 않는다.
                Assert.Greater(stage.Texture.width, 176,
                    "RT 가로 픽셀이 표시 크기보다 작습니다 — Retina에서 선이 뭉갭니다.");
                Assert.Greater(stage.Texture.height, 214,
                    "RT 세로 픽셀이 표시 크기보다 작습니다.");
            }

            _window.Close("테스트");
            yield return null;
            Assert.IsFalse(portraitCamera.enabled,
                "창을 닫았는데 초상화 카메라가 계속 돌고 있습니다 — 24시간 상주 앱에서 상시 렌더 비용이 됩니다.");
        }

        [UnityTest]
        public IEnumerator HiddenPoseDrawsNothingAndStandingPoseDrawsLines()
        {
            yield return LoadSceneAndResolve();

            _window.Open("테스트");
            yield return null;
            yield return null;

            var stage = ExactlyOne<CharacterPortraitStage>();

            stage.SetPose(PortraitPose.Standing);
            yield return null;
            int standingLines = stage.GetComponentsInChildren<LineRenderer>(true).Length;
            Assert.Greater(standingLines, 4,
                "서 있는 포즈인데 그려진 선이 너무 적습니다(머리/몸통/팔다리가 나와야 합니다).");

            stage.SetPose(PortraitPose.Hidden);
            yield return null;
            Assert.AreEqual(0, stage.GetComponentsInChildren<LineRenderer>(true).Length,
                "가출(Hidden) 포즈인데 선이 남아 있습니다 — 없는 사람을 그리면 프레즌스 문구와 어긋납니다.");

            stage.SetPose(PortraitPose.Standing);
            yield return null;
            Assert.AreEqual(standingLines, stage.GetComponentsInChildren<LineRenderer>(true).Length,
                "같은 포즈로 되돌아왔는데 선 개수가 달라졌습니다 — 재구성이 누적/누락되고 있습니다.");
        }

        [Test]
        public void PoseIsDerivedFromStateSoPictureAndPresenceLineCannotDisagree()
        {
            Assert.AreEqual(PortraitPose.Hidden, CharacterPortraitStage.PoseForState(StickmanStateId.Runaway),
                "가출 중에는 액자를 비워야 합니다.");
            Assert.AreEqual(PortraitPose.Fallen, CharacterPortraitStage.PoseForState(StickmanStateId.Ragdoll),
                "'넘어져 있는 중'인데 초상화가 서 있으면 그림과 문구가 어긋납니다.");
            Assert.AreEqual(PortraitPose.Fallen, CharacterPortraitStage.PoseForState(StickmanStateId.ThrowTumble));
            Assert.AreEqual(PortraitPose.Busy, CharacterPortraitStage.PoseForState(StickmanStateId.Archery));
            Assert.AreEqual(PortraitPose.Busy, CharacterPortraitStage.PoseForState(StickmanStateId.BattleMinigame));
            Assert.AreEqual(PortraitPose.Standing, CharacterPortraitStage.PoseForState(StickmanStateId.Idle));
            Assert.AreEqual(PortraitPose.Standing, CharacterPortraitStage.PoseForState(StickmanStateId.Walk));
        }

        [Test]
        public void BackdropFlipsWithInkColorSoTheLineIsAlwaysVisible()
        {
            var config = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                config.inkColor = StickmanInkColor.Black;
                Color paper = CharacterPortraitStage.ResolveBackdropColor(config);
                config.inkColor = StickmanInkColor.White;
                Color charcoal = CharacterPortraitStage.ResolveBackdropColor(config);

                float paperLuma = paper.r * 0.299f + paper.g * 0.587f + paper.b * 0.114f;
                float charcoalLuma = charcoal.r * 0.299f + charcoal.g * 0.587f + charcoal.b * 0.114f;

                Assert.Greater(paperLuma, 0.7f, "검정 잉크의 액자 바탕이 밝지 않습니다 — 검은 선이 안 보입니다.");
                Assert.Less(charcoalLuma, 0.3f, "흰 잉크의 액자 바탕이 어둡지 않습니다 — 흰 선이 안 보입니다.");
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void RankTitleClimbsWithLevelAndNeverGoesBlank()
        {
            string previous = null;
            int changes = 0;
            for (int level = 1; level <= 20; level++)
            {
                string title = CharacterInfoWindow.RankTitleFor(level);
                Assert.IsFalse(string.IsNullOrWhiteSpace(title), $"Lv.{level}의 칭호가 비어 있습니다.");
                if (previous != null && title != previous) changes++;
                previous = title;
            }
            Assert.AreEqual(5, changes, "칭호가 레벨 구간마다 정확히 5번 바뀌어야 합니다(6단계).");
        }
    }
}
