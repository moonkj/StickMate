using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Platform;

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
    /// 방법: 캐릭터의 모든 Renderer.bounds(SpriteRenderer뿐 아니라 LineRenderer 등 렌더러 종류를
    /// 가리지 않는 공통 베이스 타입 — 2026-08-28 "고전적 졸라맨" 시각 교체로 몸통/팔다리/머리가
    /// SpriteRenderer에서 LineRenderer로 바뀌었으므로 특정 렌더러 타입에 고정하지 않는다)를 합쳐
    /// 월드 바운딩박스(발끝~머리끝)를 구하고,
    /// Camera.main.WorldToScreenPoint로 스크린 좌표로 변환해 화면 세로 범위
    /// ([여백, Screen.height-여백]) 안에 있는지를 정착 후 5초/10초/15초 시점에서 확인한다. 여백은
    /// Architect 지시(0.5~1유닛)의 하한인 0.5월드유닛을 카메라의 px/유닛 환산비로 픽셀 단위 환산해 쓴다
    /// (해상도에 무관하게 항상 "적어도 0.5유닛의 여백"을 강제하도록).
    ///
    /// 가로(X) 방향도 2026-08-28부터 함께 검증한다(리더 지시, 사용자 피드백 "캐릭터가 화면 벗어나서
    /// 잘 안 보임"). 예전에는 의도적으로 검증하지 않았는데, 그 전제는 더미/안전망 발판이 카메라
    /// 뷰포트보다 DummyFootholdWidthMultiplier(당시 4)배 넓다는 것이었다 — 그래서 캐릭터가 화면 가로
    /// 범위를 벗어나는 것이 "정상"이었다. 이제 그 배율이 1(=화면 폭과 일치)로 되돌아갔으므로
    /// (Platform/NullPlatformWindowService.cs 선언부의 "되돌림" 문단 참고) **캐릭터가 항상 화면 안에
    /// 보인다**가 씬의 설계 불변식이 되었고, 이 테스트가 그 회귀를 자동으로 잡는다.
    ///
    /// ★ 2026-08-28 신설 — "발판이 없으면 화면 최하단(Dock 위)에 선다" 불변식 검증(사용자 신고
    /// "지금도 떠있는것처럼보임"): 위의 여백 검증은 "캐릭터가 화면 안에 보인다"만 보장할 뿐, 캐릭터가
    /// 화면 **한가운데**에 떠 있어도 전부 통과한다 — 실제로 안전망 발판이 화면 80% 지점(OS y=785.6)에
    /// 있던 동안 이 테스트는 계속 초록불이었고, 사용자가 육안으로 두 라운드에 걸쳐 신고해야만 했다.
    /// 그래서 접지 상태(Idle/Walk) 샘플마다 "발 높이가 안전망 발판 상수가 말하는 그 높이와 일치하는가"를
    /// 함께 확인한다. 이 검사는 두 가지 회귀를 동시에 잡는다:
    ///   (1) 안전망 높이가 다시 화면 한가운데로 올라가는 회귀(이번 라운드에서 고친 증상 자체).
    ///   (2) **씬에 구운 지면 Y와 발판 상수가 서로 어긋나는 회귀** — 이 프로젝트가 반복해서 겪은 버그
    ///       계열(BUG-P1-R4-B1, BUG-P1-R5-B2)이며, 지금까지 어떤 자동 테스트도 잡지 못했다. 씬은
    ///       Editor/SceneBootstrapper.cs가 빌드 시점에 구운 값이고 발판은 런타임 계산값이라, 둘을
    ///       실행 중에 대조하는 이 검사만이 어긋남을 드러낼 수 있다.
    ///
    /// 단 X는 세로와 달리 "전신 바운딩박스 전체"가 아니라 **몸의 가로 중심**을 기준으로 본다:
    /// AutoWanderController는 발판 경계 wanderEdgeStopDistance(0.3유닛) 앞에서 멈춰 돌아서므로 가장자리
    /// 에서는 팔/다리 끝이 화면 밖으로 조금 나갈 수 있고(획 폭까지 포함하면 더), 그것까지 실패로 보면
    /// 정상 동작을 오탐한다. "캐릭터가 화면 안에 보인다"의 실질적 판정은 몸통이 화면 안에 있는가이다.
    /// </summary>
    public sealed class StickmanOnScreenFramingTests
    {
        private const string LogPrefix = "[FRAMING-TEST]";
        private static readonly float[] SampleTimes = { 5f, 10f, 15f };
        private const float MinWorldMarginUnits = 0.5f; // Architect 지시 요구치(0.5~1유닛)의 하한.
        // 가로 여백은 세로보다 느슨하게 잡는다(위 클래스 문서 "단 X는..." 참고) — 몸 중심이 화면
        // 가장자리에서 이만큼 안쪽에 있으면 캐릭터는 확실히 화면에 보인다.
        private const float MinHorizontalWorldMarginUnits = 0.1f;

        /// <summary>
        /// 접지 중인 발 높이가 "안전망 발판 상수가 말하는 높이"에서 벗어나도 되는 최대 오차(월드 유닛).
        /// 접지 시 StickmanBlackboard.SnapToGround()가 발 Y를 발판 상단에 정확히 대입하므로 실측 오차는
        /// 사실상 0이지만, 걷기 자세/물리 스텝 경계의 미세한 흔들림까지 오탐하지 않도록 1유닛을 준다.
        /// 이 값이 잡아내야 할 회귀(안전망이 화면 80% 지점으로 돌아가는 것)의 오차는 약 3유닛이라
        /// 1유닛 허용치로도 확실히 걸린다.
        /// </summary>
        private const float MaxFootWorldYErrorUnits = 1f;

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

            Renderer[] renderers = agent.GetComponentsInChildren<Renderer>(true);
            Assert.IsTrue(renderers.Length > 0, "캐릭터에서 Renderer(SpriteRenderer/LineRenderer 등)를 하나도 찾지 못했습니다.");

            // px/월드유닛 환산비 — orthographic 카메라는 X/Y 모두 동일 스케일(왜곡 없음)이므로
            // Screen.height/(2*orthographicSize) 하나로 충분하다(Platform/ScreenCoordinateConverter.cs와
            // 동일한 변환 전제).
            float pxPerWorldUnit = Screen.height / (2f * cam.orthographicSize);
            float marginPx = MinWorldMarginUnits * pxPerWorldUnit;

            Debug.Log($"{LogPrefix} 시작 — Screen={Screen.width}x{Screen.height}, cam.orthoSize={cam.orthographicSize}, " +
                $"cam.y={cam.transform.position.y:F3}, marginPx={marginPx:F1}(={MinWorldMarginUnits}유닛)");

            float elapsed = 0f;
            int groundedSamples = 0;
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

                // 가로 검증(2026-08-28 신설): 몸 중심이 화면 가로 범위 안에 여백을 두고 있어야 한다.
                Vector3 centerScreen = cam.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.center.y, bounds.center.z));
                float marginXPx = MinHorizontalWorldMarginUnits * pxPerWorldUnit;
                Assert.GreaterOrEqual(centerScreen.x, marginXPx,
                    $"{LogPrefix} t={elapsed:F1}s 캐릭터가 화면 왼쪽 밖으로 나갔습니다(screenX={centerScreen.x:F1} < {marginXPx:F1}) " +
                    "— 더미/안전망 발판 폭이 화면보다 넓어져 배회 경계 판정이 화면 밖에서 걸리는 회귀 의심 " +
                    "(Platform/NullPlatformWindowService.DummyFootholdWidthMultiplier 확인).");
                Assert.LessOrEqual(centerScreen.x, Screen.width - marginXPx,
                    $"{LogPrefix} t={elapsed:F1}s 캐릭터가 화면 오른쪽 밖으로 나갔습니다(screenX={centerScreen.x:F1} > {Screen.width - marginXPx:F1}) " +
                    "— 위와 동일한 회귀 의심.");

                Assert.GreaterOrEqual(bottomScreen.y, marginPx,
                    $"{LogPrefix} t={elapsed:F1}s 캐릭터 발이 화면 하단 여백 밖입니다(screenY={bottomScreen.y:F1} < {marginPx:F1}) " +
                    "— 화면 아래로 잘려 보일 수 있습니다(BUG-P1-R4-B1 재발 의심).");
                Assert.LessOrEqual(topScreen.y, Screen.height - marginPx,
                    $"{LogPrefix} t={elapsed:F1}s 캐릭터 머리가 화면 상단 여백 밖입니다(screenY={topScreen.y:F1} > {Screen.height - marginPx:F1}) " +
                    "— 화면 위로 잘려 보일 수 있습니다(BUG-P1-R4-B1 재발 의심 — 사용자가 실제로 목격했던 원래 증상).");

                // ★ "화면 최하단에 서 있는가" 검증(위 클래스 문서 참고). 점프/낙하 중에는 발이 지면에서
                // 떨어져 있는 것이 정상이므로 접지 상태(Idle/Walk) 샘플에서만 판정한다.
                StickmanStateId state = agent.Blackboard.Machine.CurrentStateId;
                if (state == StickmanStateId.Idle || state == StickmanStateId.Walk)
                {
                    groundedSamples++;
                    // 발판 상수 -> 기대 발 높이(월드). Editor/SceneBootstrapper.ComputeGroundTopWorldY()와
                    // **같은 상수에서 독립적으로 재유도**한다(그 헬퍼를 호출하는 게 아니라 — 에디터 전용
                    // 코드라 런타임에서 부를 수 없고, 부를 수 있더라도 같은 코드를 재사용하면 "씬에 구운
                    // 값이 그 계산과 실제로 일치하는가"를 검증할 수 없다).
                    float f = NullPlatformWindowService.DummyFootholdHeightFraction;
                    float expectedFootWorldY = cam.transform.position.y - cam.orthographicSize * (1f - 2f * f);
                    float actualFootWorldY = agent.transform.position.y;
                    float error = Mathf.Abs(actualFootWorldY - expectedFootWorldY);

                    Debug.Log($"{LogPrefix} t={elapsed:F1}s 접지({state}) 발 Y 실측={actualFootWorldY:F3} " +
                        $"기대={expectedFootWorldY:F3}(f={f:F4}) 오차={error:F3}유닛 " +
                        $"— 화면 하단에서 위로 {(actualFootWorldY - (cam.transform.position.y - cam.orthographicSize)) * pxPerWorldUnit:F1}px");

                    Assert.LessOrEqual(error, MaxFootWorldYErrorUnits,
                        $"{LogPrefix} t={elapsed:F1}s 접지 중인 캐릭터의 발 높이({actualFootWorldY:F3})가 안전망 발판 상수가 " +
                        $"말하는 높이({expectedFootWorldY:F3})와 {error:F3}유닛 어긋났습니다. 둘 중 하나입니다 — " +
                        "(a) 안전망 발판(Platform/NullPlatformWindowService.DummyFootholdHeightFraction)이 다시 화면 " +
                        "한가운데로 올라가 캐릭터가 '허공에 떠 있는 것처럼' 보이는 회귀, 또는 (b) 씬에 구운 지면/스폰 Y" +
                        "(Editor/SceneBootstrapper.cs)와 그 상수가 어긋난 회귀 — 후자라면 --force로 씬을 재생성해야 합니다.");
                }
            }

            Assert.Greater(groundedSamples, 0,
                $"{LogPrefix} 세 샘플 시점 모두에서 캐릭터가 접지 상태(Idle/Walk)가 아니었습니다 — 발 높이 검증이 " +
                "한 번도 실행되지 않았으므로 통과로 볼 수 없습니다(무한낙하/미정착 의심).");

            Debug.Log($"{LogPrefix} 완료 — 모든 샘플 시점({string.Join(",", SampleTimes)}초)에서 캐릭터 전신(발~머리)이 " +
                "화면 세로 범위 안에 최소 여백을 두고 들어와 있었고, 몸 중심이 화면 가로 범위 안에 있었습니다.");
        }

        private static Bounds ComputeCombinedBounds(Renderer[] renderers)
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
