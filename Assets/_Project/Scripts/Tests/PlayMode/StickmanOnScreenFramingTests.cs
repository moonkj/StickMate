using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// BUG-P1-R4-B1 재발 방지 테스트(2026-08-28, Architect 진단) — 사용자가 GUI 에디터에서 Main.unity를
    /// 직접 Play시켜 육안으로 "화면 제일 상단에서 뭔가 왔다갔다하고 안 보인다"고 보고했던 버그(더미 발판이
    /// Platform/NullPlatformWindowService.cs에서 화면 "맨 위"에 잘못 놓여, 캐릭터가 카메라 뷰포트
    /// 최상단 가장자리에 걸쳐 정착)를 잡아내지 못했던 기존 테스트 공백을 메운다. 기존
    /// StickmanPlaytestSmokeTests.cs는 transform.position.y가 "발산하지 않는지"(무한낙하 여부)만
    /// 확인했을 뿐, 그 Y가 실제로 카메라 뷰포트 안(=화면에 보이는 범위) 안인지는 전혀 검증하지 않아 이
    /// 버그가 사용자가 직접 눈으로 발견할 때까지 아무 자동 테스트에도 걸리지 않은 채 남아 있었다.
    ///
    /// 방법: 캐릭터의 모든 SpriteRenderer.bounds를 합쳐 월드 바운딩박스(발끝~머리끝)를 구하고,
    /// Camera.main.WorldToScreenPoint로 스크린 좌표로 변환해 화면 세로 범위
    /// ([여백, Screen.height-여백]) 안에 있는지를 정착 후 5초/10초/15초 시점에서 확인한다. 여백은
    /// Architect 지시(0.5~1유닛)의 하한인 0.5월드유닛을 카메라의 px/유닛 환산비로 픽셀 단위 환산해 쓴다
    /// (해상도에 무관하게 항상 "적어도 0.5유닛의 여백"을 강제하도록).
    ///
    /// 가로(X) 방향은 이 테스트에서 화면 폭 안 포함 여부를 의도적으로 강제하지 않는다 — 이는 이번
    /// 버그와 무관한, 사전에 문서화된 별개의 설계 특성이다: Platform/NullPlatformWindowService.cs의
    /// 더미 발판은 BUG-SW-M2(Tasklist.md "씬/프리팹 배선" 절, 2026-08-28) 대응으로 카메라 뷰포트보다
    /// DummyFootholdWidthMultiplier(4)배 넓게 설계되어 있고, AutoWanderController가 그 넓은 범위 전체를
    /// 자유롭게 배회하는 것이 명시적으로 의도된 동작이다(그 상수 선언부 주석: "배회 AI가 카메라
    /// 뷰포트보다 훨씬 넓은 범위를 돌아다닐 수 있다"). 실제로 walkSpeed(2.5)×최대 Walk 지속시간(지터
    /// 포함 최대 약 4.7초)만으로 단일 Walk 페이즈가 카메라 뷰포트 반폭(orthoSize=5 기준 약 6.67유닛)을
    /// 넘는 편도 거리(~11.75유닛)를 낼 수 있어, 15초 관찰 구간에서 캐릭터가 카메라 뷰포트 가로 범위를
    /// 벗어나는 것은 정상이다 — 수직 방향과 달리 수평 방향은 "항상 카메라 프레임 안"이 이 씬의 설계
    /// 불변식이 아니다(지면 Y와 달리 발판의 X 범위 자체가 뷰포트보다 넓게 잡혀 있기 때문). 이 테스트는
    /// 대신 X가 유한(발산하지 않음)한지만 로그로 남긴다.
    /// </summary>
    public sealed class StickmanOnScreenFramingTests
    {
        private const string LogPrefix = "[FRAMING-TEST]";
        private static readonly float[] SampleTimes = { 5f, 10f, 15f };
        private const float MinWorldMarginUnits = 0.5f; // Architect 지시 요구치(0.5~1유닛)의 하한.

        [UnityTest]
        public IEnumerator StickmanStaysWithinVerticalViewportMargin()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null; // Awake/Start가 완전히 실행되도록 한 프레임 더 대기.

            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(agent, "씬에서 StickmanAgent를 찾지 못했습니다 — Main.unity 배선을 확인하세요.");

            Camera cam = Camera.main;
            Assert.IsNotNull(cam, "Camera.main을 찾지 못했습니다.");
            Assert.IsTrue(cam.orthographic, $"{LogPrefix} 이 테스트는 orthographic 카메라 배치를 가정합니다.");

            SpriteRenderer[] renderers = agent.GetComponentsInChildren<SpriteRenderer>(true);
            Assert.IsTrue(renderers.Length > 0, "캐릭터에서 SpriteRenderer를 하나도 찾지 못했습니다.");

            // px/월드유닛 환산비 — orthographic 카메라는 X/Y 모두 동일 스케일(왜곡 없음)이므로
            // Screen.height/(2*orthographicSize) 하나로 충분하다(Platform/ScreenCoordinateConverter.cs와
            // 동일한 변환 전제).
            float pxPerWorldUnit = Screen.height / (2f * cam.orthographicSize);
            float marginPx = MinWorldMarginUnits * pxPerWorldUnit;

            Debug.Log($"{LogPrefix} 시작 — Screen={Screen.width}x{Screen.height}, cam.orthoSize={cam.orthographicSize}, " +
                $"cam.y={cam.transform.position.y:F3}, marginPx={marginPx:F1}(={MinWorldMarginUnits}유닛)");

            float elapsed = 0f;
            foreach (float sampleTime in SampleTimes)
            {
                yield return new WaitForSeconds(sampleTime - elapsed);
                elapsed = sampleTime;

                Bounds bounds = ComputeCombinedBounds(renderers);
                Vector3 bottomWorld = new Vector3(bounds.min.x, bounds.min.y, bounds.center.z);
                Vector3 topWorld = new Vector3(bounds.max.x, bounds.max.y, bounds.center.z);
                Vector3 bottomScreen = cam.WorldToScreenPoint(bottomWorld);
                Vector3 topScreen = cam.WorldToScreenPoint(topWorld);

                Debug.Log($"{LogPrefix} t={elapsed:F1}s bounds.min={bounds.min} bounds.max={bounds.max} " +
                    $"bottomScreen=({bottomScreen.x:F1},{bottomScreen.y:F1}) topScreen=({topScreen.x:F1},{topScreen.y:F1})");

                bool xFinite = !float.IsNaN(bottomScreen.x) && !float.IsInfinity(bottomScreen.x)
                    && !float.IsNaN(topScreen.x) && !float.IsInfinity(topScreen.x);
                Assert.IsTrue(xFinite, $"{LogPrefix} t={elapsed:F1}s 캐릭터 X 좌표가 발산했습니다(NaN/Infinity).");

                Assert.GreaterOrEqual(bottomScreen.y, marginPx,
                    $"{LogPrefix} t={elapsed:F1}s 캐릭터 발이 화면 하단 여백 밖입니다(screenY={bottomScreen.y:F1} < {marginPx:F1}) " +
                    "— 화면 아래로 잘려 보일 수 있습니다(BUG-P1-R4-B1 재발 의심).");
                Assert.LessOrEqual(topScreen.y, Screen.height - marginPx,
                    $"{LogPrefix} t={elapsed:F1}s 캐릭터 머리가 화면 상단 여백 밖입니다(screenY={topScreen.y:F1} > {Screen.height - marginPx:F1}) " +
                    "— 화면 위로 잘려 보일 수 있습니다(BUG-P1-R4-B1 재발 의심 — 사용자가 실제로 목격했던 원래 증상).");
            }

            Debug.Log($"{LogPrefix} 완료 — 모든 샘플 시점({string.Join(",", SampleTimes)}초)에서 캐릭터 전신(발~머리)이 " +
                "화면 세로 범위 안에 최소 여백을 두고 들어와 있었습니다.");
        }

        private static Bounds ComputeCombinedBounds(SpriteRenderer[] renderers)
        {
            Bounds combined = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                combined.Encapsulate(renderers[i].bounds);
            }
            return combined;
        }
    }
}
