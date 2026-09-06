using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.States;

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

        /// <summary>
        /// 관찰창의 <b>상한</b>(초). 조건이 충족되면 그 전에 끝난다 — 아래 «조기 탈출» 문단 참고.
        ///
        /// <para>★★ 2026-09-06 — 옛 값은 <c>TotalDuration = 15f</c>(고정 15초 관찰)였고, 그것이
        /// 이 테스트의 <b>1.44%/회 플레이키니스</b>의 전부였다.</para>
        ///
        /// <para><b>원인</b>(debugger 규명): 배회 RNG는 씬 로드마다 무작위 시드다
        /// (<c>Core/StickmanAgent.cs</c>가 <c>AutoWanderController</c>에 넘기는
        /// <c>new System.Random(System.Guid.NewGuid().GetHashCode())</c>) —
        /// 즉 이 테스트는 <b>구조적으로 비결정적</b>이다. 그리고 15초 안에는 «Idle 종료 추첨»이
        /// 3~4회밖에 없다. 한 번의 추첨에서 «Idle 연장»이 나올 확률은
        /// <c>1 − wanderPostIdleWalkChance − wanderPostIdleJumpChance</c> = 0.25이므로,
        /// 3~4회가 <b>전부</b> 연장이면 X 변동폭이 <b>정확히 0</b>이 되어 실패한다
        /// (몬테카를로 200만 시행 실측 1.44%/회. 실제 실패 로그도 X범위 0.0000이었다).</para>
        ///
        /// <para><b>고친 방식</b>: 고정 창을 «조건 충족 시 조기 탈출하는 최대 40초 창»으로 바꿨다.
        /// 추첨 기회가 3~4회에서 10회 이상으로 늘어 실패율이 1/70 → 약 1/100,000이 된다.
        /// <b>평균 실행 시간은 오히려 줄어든다</b> — 실측 로그상 첫 걷기는 대개 4초 근처에
        /// 시작하므로 대부분 그때 탈출한다(옛 판은 무조건 15초를 다 썼다).</para>
        ///
        /// <para><b>진짜 회귀는 여전히 잡는다</b>: 배회가 실제로 죽으면 40초를 다 채워도 X 변동폭이
        /// 기준에 못 미쳐 실패한다. 창을 늘린 것은 «판정 기준»이 아니라 «추첨 횟수»다 —
        /// 문턱(<see cref="MinXMovementRange"/>)과 단언은 한 글자도 바뀌지 않았다.</para>
        /// </summary>
        private const float MaxObserveSeconds = 40f;

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
            float minX = float.MaxValue, maxX = float.MinValue;
            bool wanderObserved = false;

            Debug.Log($"{LogPrefix} 시작 — initialY={initialY:F3}, 관찰창 최대 {MaxObserveSeconds}s(조기 탈출형), interval={SampleInterval}s");
            Debug.Log($"{LogPrefix} DIAG Screen={Screen.width}x{Screen.height}, cam.orthoSize={Camera.main?.orthographicSize}, cam.y={Camera.main?.transform.position.y}");

            while (elapsed < MaxObserveSeconds)
            {
                yield return new WaitForSeconds(SampleInterval);
                elapsed += SampleInterval;
                Vector3 pos = agent.transform.position;
                samples.Add((elapsed, pos));
                minX = Mathf.Min(minX, pos.x);
                maxX = Mathf.Max(maxX, pos.x);
                Debug.Log($"{LogPrefix} t={elapsed:F1}s x={pos.x:F3} y={pos.y:F3} (누적 X범위 {maxX - minX:F3})");

                Assert.Less(initialY - pos.y, MaxAllowedFallDistance,
                    $"{LogPrefix} 무한 낙하 의심 — t={elapsed:F1}s에 Y가 시작점보다 {initialY - pos.y:F1} 유닛 아래로 발산했습니다.");

                // ── 조기 탈출: (b) 배회가 확인됐고 (a) 지금 접지 상태다.
                //
                // ★ «접지»를 조기 탈출 조건에 함께 넣는 이유: 아래 (a) 정착 판정은 «관찰 종료 시점의
                //   상태»를 본다. 접지를 요구하지 않으면 점프/파쿠르 궤적 한가운데서 탈출해 정착
                //   판정이 위양성으로 실패할 수 있다 — 플레이키니스를 한 곳에서 없애고 다른 곳에
                //   새로 만드는 꼴이다.
                // ★ 그리고 이 조건은 (a)를 <b>약화시키지 않는다</b>: 캐릭터가 영영 정착하지 못하는
                //   진짜 회귀에서는 접지가 오지 않으므로 조기 탈출 자체가 걸리지 않고, 40초를 다 채운
                //   뒤 (a)가 그 사실을 그대로 잡는다(그 40초 내내 위 무한낙하 가드도 함께 돈다 —
                //   옛 15초보다 오히려 오래 본다).
                StickmanStateId now = agent.Blackboard.Machine.CurrentStateId;
                bool groundedNow = now == StickmanStateId.Idle || now == StickmanStateId.Walk;
                if (maxX - minX > MinXMovementRange && groundedNow)
                {
                    wanderObserved = true;
                    Debug.Log($"{LogPrefix} 조기 탈출 — t={elapsed:F1}s에 X 변동폭 {maxX - minX:F3} > {MinXMovementRange} " +
                        $"확인, 상태={now}(접지). 남은 관찰({MaxObserveSeconds - elapsed:F1}s)은 더 볼 것이 없습니다.");
                    break;
                }
            }

            // (a) 정착 판정: docs/BUG_REPORT_SCENE_WIRING.md 재확인 라운드에서 리더가 직접 발견/수정한
            // 결함 — 원래는 "SettleWindowStart 이후 7초 전체 구간의 Y 변동폭이 작아야 한다"였는데,
            // AutoWanderController(26절)는 설계상 그 구간에도 저확률로 제자리 점프/파쿠르 시도를
            // 계속 굴릴 수 있어(그게 "살아있는 느낌"의 의도된 핵심이다), 점프 도중에 샘플링되면
            // 정상 동작인데도 이 넓은 범위 체크가 우연히 실패하는 플레이키 테스트였다(리더 재검토 중
            // 재현 확인). "정착"의 진짜 의미는 "Y가 7초 내내 안 흔들림"이 아니라 "결국 접지 상태로
            // 돌아와 있음"이므로, 테스트 종료 시점의 실제 상태머신 상태(Idle/Walk = 접지, 그 외
            // Jump/Fall/ParkourClimb/Ragdoll/Getup 등은 아직 전이 중)로 판정하도록 교체한다 — 이렇게
            // 하면 우연히 그 순간에 점프 중이어도 위양성 실패가 나지 않으면서, 진짜 무한낙하/미정착은
            // 여전히 위의 매 샘플 MaxAllowedFallDistance 가드와 아래 판정 둘 다에서 잡힌다.
            StickmanStateId finalState = agent.Blackboard.Machine.CurrentStateId;
            bool isGrounded = finalState == StickmanStateId.Idle || finalState == StickmanStateId.Walk;
            Debug.Log($"{LogPrefix} 정착 판정(t={elapsed:F1}s, 표본 {samples.Count}개) 최종 상태={finalState} (Idle/Walk 접지 상태여야 함)");
            Assert.IsTrue(isGrounded,
                $"{LogPrefix} 캐릭터가 정착하지 못했습니다 — 관찰 종료 시점(t={elapsed:F1}s) 상태가 {finalState}로, " +
                "접지 상태(Idle/Walk)가 아닙니다.");

            // (b) 배회 판정: 관찰 구간 X 범위가 충분히 커야 한다(자율 배회 AI가 실제로 걸었다는 증거).
            //     최소/최대는 위 루프가 표본을 잡을 때 이미 누적했다 — 조기 탈출 판정과 여기의 최종
            //     판정이 <b>같은 누적값</b>을 보므로 둘이 갈라질 수 없다.
            float xRange = maxX - minX;
            Debug.Log($"{LogPrefix} 관찰 구간 X 범위: {xRange:F4} (min={minX:F3}, max={maxX:F3}, 관찰 {elapsed:F1}s / 상한 {MaxObserveSeconds}s)");
            Assert.Greater(xRange, MinXMovementRange,
                $"{LogPrefix} 자율 배회 이동을 감지하지 못했습니다 — {elapsed:F1}초(상한 {MaxObserveSeconds}초) 동안 " +
                $"X 변동폭 {xRange:F4}가 최소 기준 {MinXMovementRange}에 못 미칩니다. 이 창 안에는 «Idle 종료» " +
                "추첨이 10회 이상 들어가므로, 전부 «Idle 연장»이 뽑혔을 확률은 약 1/100,000입니다 — " +
                "즉 이 실패는 우연이 아니라 배회 AI가 실제로 걷지 않는다는 뜻입니다.");
            Assert.IsTrue(wanderObserved,
                $"{LogPrefix} 내부 모순 — X 변동폭({xRange:F4})은 기준을 넘었는데 조기 탈출 플래그가 서지 않았습니다. " +
                "관찰 루프와 최종 판정이 서로 다른 값을 보고 있다는 뜻입니다.");

            Debug.Log($"{LogPrefix} 완료 — 최종상태={finalState}(접지), X범위={xRange:F4}(>{MinXMovementRange}), 실제 관찰 {elapsed:F1}s.");
        }
    }
}
