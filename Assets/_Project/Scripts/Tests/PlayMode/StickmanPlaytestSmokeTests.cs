using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// Assets/_Project/Scenes/Main.unity(Assets/Editor/SceneBootstrapper.cs가 생성)를 실제로 Play
    /// 모드에서 일정 시간 구동해 "컴파일된다"를 넘어 "실제로 걷고, 무한 낙하하지 않고, 예외 없이
    /// 돌아간다"를 검증하는 스모크 테스트다. 씬/프리팹이 없던 Phase 0~6 코드 레이어를 처음으로 실제
    /// Update/FixedUpdate 루프에 태워보는 테스트이므로, 개별 로직 단위 테스트(EditMode)가 아니라
    /// 전체 배선이 실제로 동작하는지를 확인하는 것이 목적이다.
    ///
    /// 검증 항목:
    /// (a) Y좌표가 발산하지 않고 정착하는지 — NullPlatformWindowService의 더미 발판 스냅이 실제로 동작.
    /// (b) X좌표가 유의미하게 변하는지 — AutoWanderController 자율 배회가 실제로 Rigidbody2D를 움직임.
    /// (c) 예외/에러 로그가 없는지 — Unity Test Framework 기본 동작상 테스트 도중 Debug.LogError/
    ///     LogException이 한 번이라도 발생하면 이 테스트는 자동으로 실패한다(LogAssert.Expect로 미리
    ///     기대하지 않는 한). 별도의 예외 감지 코드를 추가할 필요가 없다.
    ///
    /// 각 샘플은 Debug.Log(...)로도 남겨(LogPrefix) -logFile 결과물에서 grep으로 실측 로그를 바로
    /// 확인할 수 있게 한다(리더 보고용 "아마 될 것"이 아니라 "실측 증거" 요구사항 대응).
    /// </summary>
    public sealed class StickmanPlaytestSmokeTests
    {
        private const string LogPrefix = "[PLAYTEST]";
        private const float SampleInterval = 0.5f;
        private const float TotalDuration = 15f;
        private const float SettleWindowStart = 8f; // 이 시점 이후 샘플들로 "정착" 여부 판정.
        private const float MaxSettledYRange = 0.05f; // 정착 구간 Y 변동 허용 오차(월드 유닛).
        private const float MinXMovementRange = 0.3f; // 배회 이동 판정 최소 X 변동폭(월드 유닛).
        private const float MaxAllowedFallDistance = 50f; // 무한 낙하 조기 실패 판정(안전망).

        [UnityTest]
        public IEnumerator StickmanFallsSettlesAndWanders()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null; // Awake/Start가 완전히 실행되도록 한 프레임 더 대기.

            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(agent, "씬에서 StickmanAgent를 찾지 못했습니다 — Main.unity 배선을 확인하세요.");

            var samples = new List<(float t, Vector3 pos)>();
            float elapsed = 0f;
            float initialY = agent.transform.position.y;

            Debug.Log($"{LogPrefix} 시작 — initialY={initialY:F3}, duration={TotalDuration}s, interval={SampleInterval}s");
            Debug.Log($"{LogPrefix} DIAG Screen={Screen.width}x{Screen.height}, cam.orthoSize={Camera.main?.orthographicSize}, cam.y={Camera.main?.transform.position.y}");

            while (elapsed < TotalDuration)
            {
                yield return new WaitForSeconds(SampleInterval);
                elapsed += SampleInterval;
                Vector3 pos = agent.transform.position;
                samples.Add((elapsed, pos));
                Debug.Log($"{LogPrefix} t={elapsed:F1}s x={pos.x:F3} y={pos.y:F3}");

                Assert.Less(initialY - pos.y, MaxAllowedFallDistance,
                    $"{LogPrefix} 무한 낙하 의심 — t={elapsed:F1}s에 Y가 시작점보다 {initialY - pos.y:F1} 유닛 아래로 발산했습니다.");
            }

            // (a) 정착 판정: SettleWindowStart 이후 샘플들의 Y 범위가 충분히 작아야 한다(무한 낙하/진동 없음).
            float settledMinY = float.MaxValue, settledMaxY = float.MinValue;
            foreach (var s in samples)
            {
                if (s.t < SettleWindowStart) continue;
                settledMinY = Mathf.Min(settledMinY, s.pos.y);
                settledMaxY = Mathf.Max(settledMaxY, s.pos.y);
            }
            float settledRange = settledMaxY - settledMinY;
            Debug.Log($"{LogPrefix} 정착 구간(t>={SettleWindowStart}s) Y 범위: {settledRange:F4} (min={settledMinY:F3}, max={settledMaxY:F3})");
            Assert.Less(settledRange, MaxSettledYRange,
                $"{LogPrefix} 캐릭터가 정착하지 못했습니다 — 정착 구간 Y 변동폭 {settledRange:F4}가 허용치 {MaxSettledYRange}를 초과했습니다.");

            // (b) 배회 판정: 전체 구간 X 범위가 충분히 커야 한다(자율 배회 AI가 실제로 걸었다는 증거).
            float minX = float.MaxValue, maxX = float.MinValue;
            foreach (var s in samples)
            {
                minX = Mathf.Min(minX, s.pos.x);
                maxX = Mathf.Max(maxX, s.pos.x);
            }
            float xRange = maxX - minX;
            Debug.Log($"{LogPrefix} 전체 구간 X 범위: {xRange:F4} (min={minX:F3}, max={maxX:F3})");
            Assert.Greater(xRange, MinXMovementRange,
                $"{LogPrefix} 자율 배회 이동을 감지하지 못했습니다 — X 변동폭 {xRange:F4}가 최소 기준 {MinXMovementRange}에 못 미칩니다.");

            Debug.Log($"{LogPrefix} 완료 — 정착 Y범위={settledRange:F4}(<{MaxSettledYRange}), X범위={xRange:F4}(>{MinXMovementRange})");
        }
    }
}
