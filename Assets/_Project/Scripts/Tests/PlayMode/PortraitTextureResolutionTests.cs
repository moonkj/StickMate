using System.Collections;
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
    /// ★ 2026-08-30 회귀 잠금 — 사용자 신고 "캐릭터 창에서 보이는 캐릭터도… 픽셀이 다 깨져보임".
    ///
    /// ============================================================================
    /// 무엇이 잘못돼 있었나
    /// ============================================================================
    /// 초상화 RenderTexture 크기를 정할 때 <see cref="ScreenCoordinateConverter.ResolveDpiScale"/>를 썼다.
    /// 그 값의 단위는 <b>OS 포인트 / Unity 픽셀</b>이라 Retina에서 <b>0.5</b>다(1512pt / 3024px).
    /// 필요한 것은 그 <b>역수</b>인 "캔버스 유닛 -> Unity 픽셀"(= <c>CanvasScaler.scaleFactor</c>, Retina에서 2)이었다.
    /// 두 값은 서로 역수라 <b>잘못 넘겨도 컴파일되고 그림도 나온다</b> — 다만 Retina에서 텍스처가
    /// 표시 물리 픽셀의 <b>1/2</b>(면적 1/4)로 만들어져 확대 표시된다. 그게 "픽셀이 깨져 보임"이었다.
    ///
    /// ============================================================================
    /// 이 테스트가 잠그는 것
    /// ============================================================================
    ///  ① 두 배율이 실제로 <b>서로 역수</b>다(둘을 헷갈릴 수 있다는 사실 자체를 코드로 남긴다).
    ///  ② Retina(배율 0.5)를 흉내 낸 상태에서 초상화 RT가 <b>표시 물리 픽셀의 슈퍼샘플 배수</b>가 된다.
    ///     즉 폭이 표시 캔버스 유닛보다 <b>커야</b> 한다 — 옛 코드에서는 정확히 <b>절반</b>이었다.
    /// </summary>
    public sealed class PortraitTextureResolutionTests
    {
        private const string LogPrefix = "[초상화해상도-TEST]";
        private const int Supersample = 2;   // CharacterPortraitStage.Supersample과 같은 값(private이라 복사)

        private CharacterInfoWindow _window;
        private float _savedAutoDpi;

        [SetUp]
        public void SaveDpi() => _savedAutoDpi = ScreenCoordinateConverter.AutoDpiScale;

        [UnityTearDown]
        public IEnumerator Down()
        {
            ScreenCoordinateConverter.AutoDpiScale = _savedAutoDpi;
            if (_window != null && _window.IsOpen) _window.Close("테스트 정리");
            _window = null;
            yield return null;
        }

        [Test]
        public void TheTwoScaleFactorsAreReciprocalsSoSwappingThemIsSilent()
        {
            ScreenCoordinateConverter.AutoDpiScale = 0.5f;   // Retina 2x
            float dpi = ScreenCoordinateConverter.ResolveDpiScale(null);
            float canvas = ScreenCoordinateConverter.ResolveCanvasScaleFactor(null);

            Assert.AreEqual(0.5f, dpi, 1e-4f, $"{LogPrefix} Retina에서 ResolveDpiScale은 0.5여야 합니다(포인트/픽셀).");
            Assert.AreEqual(2f, canvas, 1e-4f, $"{LogPrefix} Retina에서 캔버스 배율은 2여야 합니다(픽셀/유닛).");
            Assert.AreEqual(1f, dpi * canvas, 1e-4f,
                $"{LogPrefix} 두 값은 서로 역수입니다 — 그래서 뒤바꿔 써도 컴파일되고 그림도 나옵니다. " +
                "초상화 RT 크기에는 반드시 <b>캔버스 배율</b>을 씁니다.");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator PortraitTextureIsSupersampledAgainstPhysicalPixelsOnRetina()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Assert.Ignore("헤드리스(-nographics)에서는 오프스크린 카메라를 켜지 않습니다.");
                yield break;
            }

            ScreenCoordinateConverter.AutoDpiScale = 0.5f;   // Retina 2x인 척

            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _window = Object.FindFirstObjectByType<CharacterInfoWindow>();
            Assert.IsNotNull(_window, $"{LogPrefix} CharacterInfoWindow 없음");
            _window.Open("초상화 해상도 테스트");
            for (int i = 0; i < 6; i++) yield return null;

            var stage = Object.FindFirstObjectByType<CharacterPortraitStage>();
            Assert.IsNotNull(stage, $"{LogPrefix} CharacterPortraitStage 없음");
            Assert.IsNotNull(stage.Texture, $"{LogPrefix} 초상화 RT가 만들어지지 않았습니다.");

            float designWidth = CharacterInfoWindow.PortraitContentSize.x;    // 캔버스 유닛
            float canvasScale = ScreenCoordinateConverter.ResolveCanvasScaleFactor(null);
            int expected = Mathf.RoundToInt(designWidth * canvasScale) * Supersample;

            Debug.Log($"{LogPrefix} 표시 {designWidth:F0}유닛 × 캔버스배율 {canvasScale:F1} = " +
                      $"{designWidth * canvasScale:F0} 물리픽셀, 기대 RT 폭 {expected}, 실측 {stage.Texture.width}");

            Assert.AreEqual(expected, stage.Texture.width,
                $"{LogPrefix} RT 폭이 기대와 다릅니다. 옛 버그에서는 {Mathf.RoundToInt(designWidth * 0.5f) * Supersample}" +
                "(=표시 물리 픽셀의 절반)이 나왔습니다.");

            // 절대 조건: RT는 표시 물리 픽셀보다 반드시 커야 한다(축소 표시 = 슈퍼샘플).
            Assert.Greater(stage.Texture.width, designWidth * canvasScale,
                $"{LogPrefix} RT가 표시 물리 픽셀보다 작거나 같습니다 — 확대 표시되어 계단이 보입니다.");
        }
    }
}
