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
    /// ★★ 사용자 신고 잠금(2026-09-01): <b>"껏다가 재실행하면 처음과 똑같다가 점점 느려짐"</b> /
    /// <b>"뭔가 데이터가 계속계속 누적되는거 같은 느낌이야"</b>
    ///
    /// <c>FootholdLifecycleLeakTests</c>가 <b>발판 파이프라인 한 곳</b>을 잠근다면, 이 파일은
    /// <b>앱 전체</b>에 같은 성질의 그물을 친다: 실제 Main 씬을 띄워 놓고 <b>살아 있는 씬 오브젝트 수가
    /// 시간에 따라 단조 증가하지 않는지</b>를 본다. 어느 서브시스템이 "만들고 안 지우는" 코드를
    /// 새로 들여와도 여기서 걸린다.
    ///
    /// ============================================================================
    /// 왜 "골(trough) 비교"인가 — 최대치를 보면 반드시 흔들린다
    /// ============================================================================
    /// 이 앱은 먼지/잉크/화살 같은 <b>일시적 연출 오브젝트</b>를 수시로 만들고 지운다. 그래서 순간
    /// 최대치는 그때 무슨 연출이 돌았느냐에 따라 크게 출렁이고, 최대치를 비교하면 테스트가 불안정해진다.
    /// 반대로 <b>구간 최소치</b>는 "연출이 하나도 안 떠 있는 순간의 상주 오브젝트 수"에 수렴한다 —
    /// 진짜 누수는 이 바닥선을 밀어 올리고, 정상적인 연출은 밀어 올리지 못한다.
    ///
    /// <para>★ <b>2026-09-01 — 후반부는 "구간 전체"가 아니라 <see cref="TrailingFloorSamples"/>개짜리
    /// <b>꼬리 구간</b>의 최소치로 잰다.</b> 이 파일의 첫 실행에서 네거티브 컨트롤(G1n)이 실패해 드러난
    /// 결함이다: 일부러 240개를 샜는데 바닥선이 <c>1992 -> 1992(차이 0)</c>으로 <b>전혀 움직이지
    /// 않았다</b>. 후반부 표본은 <c>[1992, 2002, 2012, ... , 2222]</c>로 명백히 새고 있었는데도 그랬다.
    ///
    /// <para>원인은 <b>최소치를 후반부 <i>전체</i>에서 뽑은 것</b>이다. 누수가 후반부 <b>시작점</b>부터
    /// 자라면 첫 표본이 곧 누수 이전 값이고, 그 값이 구간 최소치를 그대로 붙들어 맨다. 이건 네거티브
    /// 컨트롤만의 사정이 아니다 — 실제 앱에서 누수가 관측 구간 도중에 시작해도 <b>G1이 똑같이 못 본다</b>.
    /// 즉 G1은 "항상 참인 단언"이었고, 네거티브 컨트롤이 정확히 그 사실을 잡아냈다.</para>
    ///
    /// <para>고친 방식: 기준선은 전반부 <b>전체</b> 최소치(조용한 바닥선), 비교 대상은 후반부 <b>마지막
    /// 구간</b>의 최소치. 단조 증가하는 누수는 구간 어디서 시작하든 꼬리를 밀어 올리므로 반드시 걸린다.
    /// 반대로 일시적 연출은 꼬리 안에서도 반드시 한 번은 사라지므로(가장 긴 연출이 1초 미만, 꼬리는 그보다
    /// 훨씬 길다) 바닥선을 밀어 올리지 못한다 — 원래 의도였던 "골 비교"의 성질은 그대로다.
    /// <b>허용 오차(<see cref="GrowthToleranceObjects"/>)는 건드리지 않았다</b>: 문제는 문턱이 아니라
    /// 무엇을 재느냐였고, 문턱을 만졌다면 증상만 가리고 맹점은 그대로 남았을 것이다.</para></para>
    ///
    /// ============================================================================
    /// 규칙 준수 메모 (CLAUDE.md)
    /// ============================================================================
    /// · <b>벽시계 예산</b>: 모든 대기/샘플링은 <c>Time.realtimeSinceStartup</c> 기준 초다. 프레임 수 예산
    ///   금지 — 이 저장소의 배치모드 PlayMode는 8,200~13,200fps로 돌아 "N프레임"이 수 밀리초가 된다.
    /// · <b>비활성 오브젝트 포함</b>: "Destroy하지 않고 SetActive(false)만" 하는 형태의 누적이 이 검사의
    ///   1순위 표적이므로 <c>FindObjectsInactive.Include</c>로 센다.
    /// · <b>원칙 3</b>: 이 테스트는 읽기만 한다 — 창을 열거하는 것은 앱 자신이고, 테스트는 아무것도 건드리지 않는다.
    ///
    /// ============================================================================
    /// 플랫폼
    /// ============================================================================
    /// 잠그는 성질(씬 오브젝트 누적)이 플랫폼 중립이라 <b>macOS/Windows 공통</b>이다. 어느 플랫폼 서비스가
    /// 오브젝트를 쌓기 시작해도 같은 단언에서 걸린다.
    /// </summary>
    public sealed class LiveObjectGrowthGuardTests
    {
        private const string LogPrefix = "[오브젝트누적감시]";

        /// <summary>씬 로드 후 지연 초기화/첫 연출이 끝날 때까지 기다리는 시간(초, 벽시계).</summary>
        private const float SettleSeconds = 2.0f;

        /// <summary>전반부/후반부 각각의 관측 시간(초, 벽시계).</summary>
        private const float HalfWindowSeconds = 6.0f;

        /// <summary>샘플 간격(초, 벽시계). 전/후반부 각각 약 24개의 샘플이 나온다.</summary>
        private const float SampleIntervalSeconds = 0.25f;

        /// <summary>
        /// 허용 오차(개). 지연 생성되는 상주 위젯(풀 워밍업 등)이 후반부에 처음 만들어질 수 있어 0은 무리다.
        /// 반대로 <b>진짜 누수</b>는 이보다 훨씬 큰 규모로 나타난다 — 예컨대 "발판 하나당 오브젝트 1개"가
        /// 새면 폴링 주기 0.3초 x 창 6개 기준 후반부 6초 동안 약 120개가 쌓인다. 즉 이 값은 잡음 위,
        /// 검출 대상 훨씬 아래에 있다.
        /// </summary>
        private const int GrowthToleranceObjects = 32;

        /// <summary>
        /// 후반부 <b>바닥선</b>을 재는 꼬리 구간의 표본 수. 12개 x <see cref="SampleIntervalSeconds"/>
        /// 0.25초 = <b>마지막 3초</b>다(후반부 6초의 절반).
        ///
        /// <para>왜 3초인가 — 이 앱에서 가장 오래 떠 있는 일시적 연출이 말풍선(약 0.8초)이고 착지 먼지는
        /// 0.4초다. 꼬리가 그 몇 배는 되어야 "연출이 하나도 안 떠 있는 순간"이 꼬리 안에 반드시 들어와
        /// 바닥선이 제 값을 찾는다. 반대로 너무 길게 잡으면(예: 후반부 전체) 누수 시작 이전 표본이 다시
        /// 섞여 들어와 이 라운드에 고친 그 맹점으로 되돌아간다.</para>
        /// </summary>
        private const int TrailingFloorSamples = 12;

        private static int CountLiveObjects()
            => Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        private static int CountLiveColliders()
            => Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        private IEnumerator LoadMainAndSettle()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다 — Main.unity 배선 확인.");

            yield return WaitWallClock(SettleSeconds);
        }

        /// <summary>★ 벽시계 대기. <c>WaitForSeconds</c>는 timeScale에 묶이므로 쓰지 않는다.</summary>
        private static IEnumerator WaitWallClock(float seconds)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds) yield return null;
        }

        /// <summary>벽시계 <paramref name="seconds"/>초 동안 <paramref name="interval"/>초마다 센서스를 담는다.</summary>
        private static IEnumerator SampleFor(float seconds, float interval,
            List<int> objectSamples, List<int> colliderSamples, System.Action onSample)
        {
            float start = Time.realtimeSinceStartup;
            float nextAt = 0f;
            while (Time.realtimeSinceStartup - start < seconds)
            {
                float t = Time.realtimeSinceStartup - start;
                if (t >= nextAt)
                {
                    objectSamples.Add(CountLiveObjects());
                    colliderSamples.Add(CountLiveColliders());
                    onSample?.Invoke();
                    nextAt += interval;
                }
                yield return null;
            }
        }

        private static int Min(List<int> v)
        {
            int m = int.MaxValue;
            for (int i = 0; i < v.Count; i++) if (v[i] < m) m = v[i];
            return m;
        }

        /// <summary>마지막 <paramref name="count"/>개 표본의 최소치 — 후반부 바닥선의 정의.
        /// 표본이 그보다 적으면 있는 만큼만 본다(관측이 성립했는지는 호출부가 따로 단언한다).</summary>
        private static int TrailingMin(List<int> v, int count)
        {
            int from = v.Count > count ? v.Count - count : 0;
            int m = int.MaxValue;
            for (int i = from; i < v.Count; i++) if (v[i] < m) m = v[i];
            return m;
        }

        private static int Max(List<int> v)
        {
            int m = int.MinValue;
            for (int i = 0; i < v.Count; i++) if (v[i] > m) m = v[i];
            return m;
        }

        private static string Describe(List<int> v) => $"최소 {Min(v)} / 최대 {Max(v)} / 표본 {v.Count}개";

        // ====================================================================================
        // G1 — 실제 앱이 도는 동안 상주 오브젝트 바닥선이 밀려 올라가지 않는다
        // ====================================================================================

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator G1_앱이_도는_동안_살아있는_오브젝트_바닥선이_올라가지_않는다()
        {
            yield return LoadMainAndSettle();

            var firstObj = new List<int>(64);
            var firstCol = new List<int>(64);
            yield return SampleFor(HalfWindowSeconds, SampleIntervalSeconds, firstObj, firstCol, null);

            var secondObj = new List<int>(64);
            var secondCol = new List<int>(64);
            yield return SampleFor(HalfWindowSeconds, SampleIntervalSeconds, secondObj, secondCol, null);

            // 무의미 통과 방지 — 표본이 실제로 모였는지 먼저 확인한다.
            Assert.Greater(firstObj.Count, 3, $"{LogPrefix} 전반부 표본이 {firstObj.Count}개뿐입니다 — 관측이 성립하지 않았습니다.");
            // ★ 꼬리 구간이 후반부 <b>전체</b>와 같아지면 2026-09-01에 고친 맹점이 그대로 되돌아온다
            //   (첫 표본 = 누수 이전 값이 최소치를 붙들어 맨다). 그래서 표본 수 하한을 숫자로 적지 않고
            //   꼬리 상수에 묶는다 — 상수를 키우면 이 하한도 자동으로 따라온다.
            Assert.Greater(secondObj.Count, TrailingFloorSamples,
                $"{LogPrefix} 후반부 표본이 {secondObj.Count}개로 꼬리 구간({TrailingFloorSamples}개)보다 " +
                "많지 않습니다 — 꼬리가 곧 구간 전체가 되어 바닥선 비교가 다시 눈이 멉니다.");

            // 기준선은 전반부 전체의 골, 비교 대상은 후반부 **꼬리 구간**의 골이다(위 클래스 문서의
            // 2026-09-01 문단 참고 — 후반부 전체로 재면 누수가 구간 시작부터 자랄 때 첫 표본이 최소치를
            // 붙들어 매 G1이 '항상 참인 단언'이 된다).
            int troughA = Min(firstObj);
            int troughB = TrailingMin(secondObj, TrailingFloorSamples);
            int colTroughA = Min(firstCol);
            int colTroughB = TrailingMin(secondCol, TrailingFloorSamples);

            Debug.Log($"{LogPrefix} G1 관측 — 전반부 오브젝트 {Describe(firstObj)} / 후반부 {Describe(secondObj)} " +
                $"| 전반부 Collider2D {Describe(firstCol)} / 후반부 {Describe(secondCol)} " +
                $"(각 {HalfWindowSeconds:F0}초, {SampleIntervalSeconds:F2}초 간격, 벽시계)");

            Assert.LessOrEqual(troughB, troughA + GrowthToleranceObjects,
                $"{LogPrefix} 상주 오브젝트 바닥선이 {troughA} -> {troughB}개로 {troughB - troughA}개 올라갔습니다 " +
                $"(허용 {GrowthToleranceObjects}개). 연출이 아니라 **지워지지 않는 오브젝트**가 쌓이고 있다는 뜻이며, " +
                "이것이 곧 사용자가 신고한 '쓸수록 느려지고 재시작하면 회복'의 형태입니다. " +
                $"전반부 표본: [{string.Join(",", firstObj)}] / 후반부 표본: [{string.Join(",", secondObj)}]");

            Assert.LessOrEqual(colTroughB, colTroughA + GrowthToleranceObjects,
                $"{LogPrefix} 상주 Collider2D 바닥선이 {colTroughA} -> {colTroughB}개로 올라갔습니다. " +
                "물리 오브젝트가 쌓이면 매 FixedUpdate 비용이 직접 커집니다.");
        }

        // ====================================================================================
        // G1n — 네거티브 컨트롤: 일부러 새게 하면 G1의 방식이 반드시 잡아낸다
        // ====================================================================================

        /// <summary>샘플 한 번마다 몇 개를 새게 할 것인가. 후반부 총 누수량이 허용 오차를 확실히 넘도록 잡는다.</summary>
        private const int LeakPerSample = 10;

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator G1n_네거티브_일부러_새게_하면_바닥선_비교가_잡아낸다()
        {
            yield return LoadMainAndSettle();

            var firstObj = new List<int>(64);
            var firstCol = new List<int>(64);
            yield return SampleFor(HalfWindowSeconds, SampleIntervalSeconds, firstObj, firstCol, null);

            var leaked = new List<GameObject>(512);
            var secondObj = new List<int>(64);
            var secondCol = new List<int>(64);
            yield return SampleFor(HalfWindowSeconds, SampleIntervalSeconds, secondObj, secondCol, () =>
            {
                for (int i = 0; i < LeakPerSample; i++)
                {
                    // hideFlags를 걸지 않는다 — FindObjectsByType는 HideFlags.DontSave가 붙은 오브젝트를
                    // 돌려주지 않으므로, 숨기면 네거티브 컨트롤이 스스로를 못 보게 된다.
                    var go = new GameObject("GrowthGuardLeakProbe");
                    go.AddComponent<BoxCollider2D>();
                    go.SetActive(false); // ★ "Destroy 안 하고 비활성화만" — 검사가 이것까지 세는지 확인.
                    leaked.Add(go);
                }
            });

            // 기준선은 전반부 전체의 골, 비교 대상은 후반부 **꼬리 구간**의 골이다(위 클래스 문서의
            // 2026-09-01 문단 참고 — 후반부 전체로 재면 누수가 구간 시작부터 자랄 때 첫 표본이 최소치를
            // 붙들어 매 G1이 '항상 참인 단언'이 된다).
            int troughA = Min(firstObj);
            int troughB = TrailingMin(secondObj, TrailingFloorSamples);
            int colTroughA = Min(firstCol);
            int colTroughB = TrailingMin(secondCol, TrailingFloorSamples);
            int madeTotal = leaked.Count;

            // 정리를 먼저 한다 — 아래 단언이 실패해도 씬에 쓰레기를 남기지 않는다.
            for (int i = 0; i < leaked.Count; i++) if (leaked[i] != null) Object.DestroyImmediate(leaked[i]);
            leaked.Clear();

            Assert.Greater(madeTotal, GrowthToleranceObjects,
                $"{LogPrefix} 네거티브 컨트롤이 만든 누수가 {madeTotal}개로 허용 오차({GrowthToleranceObjects}개)를 " +
                "넘지 못했습니다 — 이러면 '잡아낸다'를 증명할 수 없습니다.");

            Assert.Greater(troughB - troughA, GrowthToleranceObjects,
                $"{LogPrefix} 일부러 {madeTotal}개를 샜는데 바닥선이 {troughA} -> {troughB}(차이 {troughB - troughA})밖에 " +
                "안 올랐습니다. G1의 비교 방식이 실제 누수를 감지하지 못한다는 뜻이므로, G1은 '항상 참인 단언'입니다. " +
                $"전반부 표본: [{string.Join(",", firstObj)}] / 후반부 표본: [{string.Join(",", secondObj)}]");

            Assert.Greater(colTroughB - colTroughA, GrowthToleranceObjects,
                $"{LogPrefix} Collider2D 바닥선이 {colTroughA} -> {colTroughB}로만 올랐습니다 — 비활성 오브젝트를 " +
                "세지 않으면 여기서 어긋납니다(FindObjectsInactive.Include 확인).");

            Debug.Log($"{LogPrefix} G1n 통과 — 일부러 {madeTotal}개를 새게 하자 바닥선이 오브젝트 " +
                $"{troughA}->{troughB}, Collider2D {colTroughA}->{colTroughB}로 올라 정확히 감지됐고, 전부 회수했습니다.");
        }
    }
}
