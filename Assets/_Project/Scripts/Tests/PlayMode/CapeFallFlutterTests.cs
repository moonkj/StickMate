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
    /// ★ 2026-08-31 사용자 신고 회귀 잠금 — "떨어지거나 할때 망토도 펄럭여야하는데 고정되어있음".
    ///
    /// ============================================================================
    /// 원인 (추측이 아니라 코드로 확정된 두 겹)
    /// ============================================================================
    ///  ① <b>구동원이 하나뿐</b>이었다. 밑단 흔들림은
    ///     <c>CharacterAccessoryRenderer.ResolveWalkSpeed01()</c> = <c>|velocity.x| / 보행속도</c>로만
    ///     구동됐고, 그 값이 0이면 <c>SetPositions</c> 호출 자체를 건너뛴다. 수직 낙하는 x 속도가 0이라
    ///     <b>"낙하 중에는 한 점도 안 움직인다"가 설계상 보장</b>돼 있었다.
    ///  ② <b>채움 면이 갱신되지 않았다</b>. 밑단을 옮기는 곳이 LineRenderer뿐이라, 화면에서 실제로
    ///     "천"으로 보이는 채움 메시는 재구성 전까지 정적이었다. 즉 <b>걷는 중에도</b> 천은 고정이고
    ///     테두리만 미끄러졌다(그 공백은 AccessoryFillRenderingTests가 "m4 공백"으로 계측만 해 둔 상태였다).
    ///
    /// ============================================================================
    /// 이 파일이 EditMode 짝(Tests/EditMode/CapeAirFlutterTests.cs)과 나누는 역할
    /// ============================================================================
    /// EditMode는 <b>식</b>(HemAirOffset)의 성질을 잠근다. 여기서는 <b>그 식이 실제로 불리는가</b>를
    /// 실행으로 확인한다 — 이 프로젝트가 여섯 번 반복한 "로직은 있는데 아무도 안 부른다" 실패는
    /// 순수 함수 테스트만으로는 절대 잡히지 않는다.
    ///
    /// ============================================================================
    /// 상태를 어떻게 유지하는가 (배치 실행에서도 결정론적이어야 한다)
    /// ============================================================================
    /// FallState.Tick()은 접지를 감지하면 곧바로 Idle/LandingCrouch로 빠져나가므로 몇 프레임을 그냥
    /// 흘려보내면 상태가 유지되지 않는다. 그래서 <b>매 프레임 코루틴 재개 시점</b>(Update 뒤,
    /// LateUpdate 앞)에 상태와 속도를 다시 못 박는다 — 렌더러는 LateUpdate에서 읽으므로 같은 프레임에
    /// 그 값을 본다. 실제 중력에 맡기면 배치 실행의 발판 구성(합성 안전망/Dock)에 따라 착지 시점이
    /// 달라져 테스트가 흔들린다.
    /// </summary>
    public sealed class CapeFallFlutterTests
    {
        private const string LogPrefix = "[망토펄럭]";
        private const int Cape = 0;         // 짧은망토(Shoulders 0번)
        private const int HemStart = 2;     // CapeOutline의 밑단 시작 인덱스
        private const int HemEnd = 6;       // 〃 끝 인덱스(포함)

        // ============================================================================
        // 표본 창은 <b>초</b>다 (2026-09-01 — 프레임수=시간 함정 전수 점검의 잔여 2건)
        // ============================================================================
        // 이 저장소의 배치모드 PlayMode는 실측 <b>8,200~13,200fps</b>(0.076~0.122ms/프레임)로 돈다
        // (앞 라운드 8,200~9,700 + 이 파일에서 6회 실측한 9,300~13,200). 그래서 "N프레임 예산"은
        // 실제로는 밀리초다 — 20프레임 = <b>0.0015~0.002초</b>. 시간에 걸쳐 일어나는 것을 프레임 수로
        // 재면 그 구간을 <b>한 번도 보지 못한 채</b> 초록이 된다(거짓 통과).
        // 예산 코드는 전부 Tests/PlayMode/TestClock.cs에 모여 있다.

        /// <summary>
        /// "정적이다"를 주장하는 표본 창(초, 벽시계).
        ///
        /// <para><b>왜 초인가.</b> 이 단언이 막으려는 회귀는 "가만히 서 있는데 천이 움직인다"인데,
        /// 그 움직임이 <b>느리게 새는</b> 종류일 수 있다. 문턱 1e-4유닛을 넘기려면 누출 속도가
        /// <c>1e-4 / 표본시간</c>보다 커야 하므로, 표본 창이 곧 <b>이 테스트의 감도</b>다:</para>
        /// <code>
        ///   옛 예산 20프레임 = 0.002초  ->  0.05 유닛/초보다 느린 누출은 전부 통과(사실상 무검증)
        ///   지금  2.0초              ->  0.00005 유닛/초까지 잡는다 (감도 1,000배)
        /// </code>
        /// <para>2초를 고른 이유: 이 문턱으로 사람이 알아볼 만한 누출(≈0.001유닛/초 = 획 두께의
        /// 3%가 1초에 밀리는 정도)을 20배 여유로 덮으면서, 테스트 한 건의 벽시계 비용을 2초 안에
        /// 묶어 둔다.</para>
        /// </summary>
        private const float StaticWindowSeconds = 2f;

        /// <summary>
        /// 기류 <b>세기</b>가 목표에 도달하기를 기다리는 예산(초, 벽시계).
        ///
        /// <para>지금 프로덕션은 <c>CharacterAccessoryRenderer.TickAirFlowInertia</c>가 "붙잡을 때는
        /// 즉시(목표가 지금보다 세면 스냅), 놓을 때만 <c>AirFlowSettleSeconds</c>(=0.62초)에 걸쳐"라
        /// 상승은 한 프레임이다. 그래서 옛 6프레임 예산으로도 우연히 맞았다. 하지만 <b>상승도 시간에
        /// 걸리도록 바뀌는 순간</b> 6프레임(0.0007초)은 기류를 0.1%도 못 올린 채 재게 된다.</para>
        /// <para>0.9초 = 그 잦아듦 상수 0.62초의 1.45배. 프로덕션의 유일한 기류 시간 상수가 그것이므로,
        /// 상승이 시간에 걸리게 되더라도 같은 크기의 상수를 쓸 것이라고 보고 잡은 예산이다.
        /// (모자라게 되면 <c>shiftAtZero</c> 단언이 <b>빨갛게</b> 실패한다 — 조용히 통과하지 않는다.)</para>
        /// </summary>
        private const float AirFlowRiseSeconds = 0.9f;

        /// <summary>
        /// 루트 <b>회전</b>이 그림에 반영되기를 기다리는 예산(초, 벽시계). ★ 이 값은 위와 달리
        /// <b>일부러 짧다</b> — 길게 잡으면 회전 단언이 스스로 약해지기 때문이다.
        ///
        /// <para><b>왜 길면 안 되는가(실측 근거).</b> 밑단 오프셋에는 4.5Hz 물결
        /// (<c>AccessoryShapeBuilder.HemAirRippleRatio</c> 0.34·R)이 섞여 있다. 두 표본 사이에 시간이
        /// 흐르면 <b>회전과 무관하게</b> 물결 위상만으로 최대 <c>2·0.34·R ≈ 0.0857유닛</c>(실측 R≈0.126)
        /// = 문턱(획 두께 0.036유닛)의 <b>2.4배</b>가 벌어진다. 즉 표본 간격이 물결 주기(0.22초)만큼만
        /// 돼도 "기류를 로컬로 내리지 않는" 회귀가 있어도 통과할 수 있다.</para>
        /// <para>0.01초에서는 위상이 0.28라디안만 돌아 물결이 만드는 차이가 최대 0.012유닛(문턱의 33%)이라
        /// 회전 효과가 0이면 단언이 정상적으로 실패한다. 그리고 여기서 기다리는 것은 <b>구조적</b>이다 —
        /// 회전은 <c>Force()</c>가 루트 transform에 직접 대입하고 렌더러는 같은 루트에 붙어 있어
        /// (Editor/SceneBootstrapper: <c>root.AddComponent&lt;CharacterAccessoryRenderer&gt;()</c>)
        /// 같은 프레임 LateUpdate에서 반영된다. 세기는 첫 홀드에서 이미 최대에 올라가 있고 속력이
        /// 그대로라 다시 올릴 것이 없다.</para>
        /// </summary>
        private const float RootRotationSeconds = 0.01f;

        [UnityTearDown]
        public IEnumerator TearDownAll()
        {
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            yield return null;
        }

        // ============================================================================
        // (1) 낙하 중 밑단 정점이 실제로 움직인다 — 신고의 핵심
        // ============================================================================

        [UnityTest]
        public IEnumerator 낙하하면_망토_밑단이_정적_상태와_다른_자리로_간다()
        {
            yield return LoadSceneAndPinIdle();
            var rig = TestRig.Build();

            // --- 기준: 정지(Idle). 이 프레임의 점이 "구워진 원본"이다.
            Vector3[] still = rig.HemPoints();
            Debug.Log($"{LogPrefix} 정지 상태 밑단 첫 점 = {still[0]}, 획 두께 = {rig.Stroke:F5}유닛.");

            // --- 빠른 수직 낙하(x 속도 0 — 옛 구동원이라면 진폭이 정확히 0이 되는 조건).
            float maxShift = 0f;
            for (int f = 0; f < 30; f++)
            {
                yield return rig.HoldFall(new Vector2(0f, -20f));
                maxShift = Mathf.Max(maxShift, MaxDistance(still, rig.HemPoints()));
            }

            Debug.Log($"{LogPrefix} 수직 낙하(20유닛/초) 중 밑단 최대 이동 = {maxShift:F5}유닛 " +
                $"(획 두께의 {maxShift / rig.Stroke:P0}).");
            Assert.Greater(maxShift, rig.Stroke * 1.5f,
                $"{LogPrefix} 수직 낙하 중 밑단이 {maxShift:F5}유닛(획의 {maxShift / rig.Stroke:P0})밖에 " +
                "움직이지 않았습니다 — 사용자가 신고한 '고정'이 그대로입니다.");
        }

        // ============================================================================
        // (2) 경계 — 낙하 속도가 0에 가까우면 펄럭임도 거의 없다
        // ============================================================================

        [UnityTest]
        public IEnumerator 낙하_속도가_0에_가까우면_펄럭임도_거의_없다()
        {
            yield return LoadSceneAndPinIdle();
            var rig = TestRig.Build();

            Vector3[] still = rig.HemPoints();

            float maxShift = 0f;
            for (int f = 0; f < 20; f++)
            {
                yield return rig.HoldFall(new Vector2(0f, -0.05f));
                maxShift = Mathf.Max(maxShift, MaxDistance(still, rig.HemPoints()));
            }

            Debug.Log($"{LogPrefix} 거의 멈춘 낙하(0.05유닛/초) 중 밑단 최대 이동 = {maxShift:F6}유닛 " +
                $"(획 두께의 {maxShift / rig.Stroke:P1}).");
            Assert.Less(maxShift, rig.Stroke * 0.1f,
                $"{LogPrefix} 사실상 정지한 낙하에서 밑단이 {maxShift:F5}유닛 움직였습니다 — " +
                "포물선 정점에서 천이 딸깍거립니다.");
        }

        // ============================================================================
        // (3) 채움 면(=화면에서 '천'으로 보이는 것)도 함께 움직인다 — 신고의 나머지 절반
        // ============================================================================

        [UnityTest]
        public IEnumerator 낙하_중_채움_면이_윤곽선을_따라온다()
        {
            yield return LoadSceneAndPinIdle();
            var rig = TestRig.Build();

            Vector3[] stillFill = rig.FillPoints();

            float maxGap = 0f, maxFillShift = 0f;
            for (int f = 0; f < 30; f++)
            {
                yield return rig.HoldFall(new Vector2(0f, -20f));
                Vector3[] line = rig.HemPoints();
                Vector3[] fill = rig.FillPoints();
                for (int k = HemStart; k <= HemEnd; k++)
                {
                    maxGap = Mathf.Max(maxGap, Vector3.Distance(line[k - HemStart], fill[k]));
                    maxFillShift = Mathf.Max(maxFillShift, Vector3.Distance(stillFill[k], fill[k]));
                }
            }

            Debug.Log($"{LogPrefix} 낙하 중 채움 면 최대 이동 = {maxFillShift:F5}유닛, " +
                $"윤곽선과의 최대 어긋남 = {maxGap:F6}유닛(획의 {maxGap / rig.Stroke:P1}).");

            Assert.Greater(maxFillShift, rig.Stroke * 1.5f,
                $"{LogPrefix} 윤곽선은 움직였는데 채움 면이 {maxFillShift:F5}유닛만 움직였습니다 — " +
                "화면에서 '천'으로 보이는 면이 여전히 고정입니다(신고의 나머지 절반).");
            Assert.Less(maxGap, rig.Stroke * 0.05f,
                $"{LogPrefix} 채움 면이 윤곽선에서 {maxGap:F5}유닛 어긋났습니다 — 면이 선 밖으로 삐져나옵니다.");
        }

        // ============================================================================
        // (4) 던져져 회전하는 동안에도 펄럭인다 — 그리고 기류가 <b>몸과 함께</b> 돈다
        // ============================================================================

        /// <summary>
        /// ThrowTumble은 루트를 통째로 회전시킨다. 액세서리 점은 루트 로컬이므로, 월드 기류 방향을
        /// 그대로 쓰면 몸이 도는 동안 기류만 고정돼 망토가 몸을 가로질러 도는 그림이 된다.
        /// 그래서 렌더러는 기류를 <c>InverseTransformDirection</c>으로 로컬에 내린다. 여기서는
        /// <b>루트 회전각이 달라지면 밑단이 가는 자리도 달라진다</b>는 것으로 그 변환을 확인한다.
        ///
        /// <para>★ <b>알려진 부작용(2026-09-01 실측, 이 라운드에서 드러남)</b> — 이 리그는 땅에 서 있는
        /// 캐릭터에게 던지기 회전을 <b>못 박아</b> 흉내 낸다. 그런데 <c>ThrowTumbleState</c>는 진입하자마자
        /// "착지까지 0.00초 — 회전할 시간이 부족합니다"로 Fall에 넘겨 버리고, 다음 프레임에 리그가 다시
        /// 못 박는다. 즉 <b>두 프레임에 한 번꼴로 상태가 재진입</b>한다(첫 홀드 0.9초에 약 4,000회,
        /// 4회 실행 4,025~4,123회). 측정에는 영향이 없다 — 렌더러가 보는 것은 매 LateUpdate의
        /// "공중 상태 + 속도 + 회전각"이고 Fall도 기류 대상(<c>IsAirborne</c>)이라 기류가 끊기지 않는다.
        /// 다만 진입 로그가 그만큼 쌓인다(이 표본 한 건이 결과 XML을 약 1.4MB 불린다). 예산이 6프레임일
        /// 때도 <b>같은 비율</b>로 일어나던 일이고(홀드당 약 3회), 창을 넓히면서 눈에 보이게 된 것뿐이다.
        /// 근본 해소는 "공중에 실제로 띄운 채 붙잡기"(위치까지 못 박기)인데, 그것은 표본 창 교체를 넘는
        /// 시나리오 변경이라 <b>리더 판단 대기</b>로 남긴다.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator 던져져_회전하는_동안에도_펄럭이고_기류가_몸을_따라_돈다()
        {
            yield return LoadSceneAndPinIdle();
            var rig = TestRig.Build();

            Vector3[] still = rig.HemPoints();

            // 회전각 0도에서의 밑단 — 기류는 로컬 기준으로도 위쪽이다.
            // 첫 홀드만 <b>기류 상승 예산</b>(0.9초)을 준다. 지금은 상승이 즉시라 남는 시간이지만,
            // 상승이 시간에 걸리게 바뀌어도 이 표본이 조용히 무의미해지지 않게 하는 자리다.
            yield return rig.HoldTumble(new Vector2(0f, -18f), 0f, AirFlowRiseSeconds);
            Vector3[] atZero = rig.HemPoints();
            float shiftAtZero = MaxDistance(still, atZero);

            // 같은 속도, 루트만 90도 돌린 상태 — 로컬 기류는 옆으로 바뀌어야 한다.
            // ★ 여기는 <b>짧아야 한다</b>. 두 표본 사이에 시간이 흐르면 4.5Hz 물결의 위상 차만으로
            //   최대 0.0857유닛(문턱 0.036의 2.4배)이 벌어져, 회전 효과가 0이어도 통과할 수 있다
            //   (RootRotationSeconds 문서의 실측 근거).
            yield return rig.HoldTumble(new Vector2(0f, -18f), 90f, RootRotationSeconds);
            Vector3[] atNinety = rig.HemPoints();

            float rotationEffect = MaxDistance(atZero, atNinety);
            Debug.Log($"{LogPrefix} 던지기 회전 — 0도에서 밑단 이동 {shiftAtZero:F5}유닛, " +
                $"0도 대비 90도의 밑단 차이 {rotationEffect:F5}유닛(획 {rig.Stroke:F5}).");

            Assert.Greater(shiftAtZero, rig.Stroke * 1.5f,
                $"{LogPrefix} 던져져 회전하는 동안 망토가 {shiftAtZero:F5}유닛밖에 안 움직였습니다.");
            Assert.Greater(rotationEffect, rig.Stroke,
                $"{LogPrefix} 루트를 90도 돌렸는데 밑단이 {rotationEffect:F5}유닛밖에 안 달라졌습니다 — " +
                "기류를 로컬로 내리지 않아 회전하는 몸 위에서 기류가 고정된 것으로 보입니다.");
        }

        // ============================================================================
        // (5) 회귀 — 땅에 서 있으면 예전 그대로 정적이다
        // ============================================================================

        /// <summary>
        /// 기류 펄럭임이 상태와 무관하게 켜지면 <b>가만히 서 있는데 망토가 날리는</b> 그림이
        /// 된다(원칙 1 위반). 접지 상태에서는 구워진 원본에서 한 점도 벗어나지 않아야 한다.
        ///
        /// <para>★ 2026-09-01 — 표본 창을 <b>20프레임(=0.002초)에서 2초</b>로 넓혔다. 옛 예산으로는
        /// 0.05유닛/초보다 느린 누출이 전부 문턱 아래에 숨어 <b>이 테스트가 아무것도 막지 못했다</b>
        /// (문턱 1e-4는 그대로다 — 바꾼 것은 표본 창뿐이고, 그래서 감도가 1,000배 좋아졌다).
        /// 근거는 <see cref="StaticWindowSeconds"/> 문서.</para>
        ///
        /// <para><b>왜 Idle 표본 시간을 따로 세어 단언하는가.</b> 이 테스트는 Idle이 아닌 프레임을
        /// 건너뛴다. 그러면 상태가 <b>한 번도</b> Idle이 아니어도 <c>maxShift</c>가 0인 채로 초록이
        /// 된다 — 표본이 텅 비었다는 사실 자체가 보이지 않는 거짓 통과다. 그래서 "표본 창의 거의
        /// 전부가 Idle이었다"를 먼저 단언한다.</para>
        ///
        /// <para><b>왜 Idle 이탈을 허용하지 않는가.</b> 착지 뒤에는 기류가 0.62초에 걸쳐 잦아드는데
        /// (<c>TickAirFlowInertia</c>) 그 구간은 이미 Idle이다. 즉 표본 창 안에서 한 번이라도
        /// 공중 상태를 다녀오면 그 뒤 Idle 프레임의 움직임은 "가만히 서 있는데 움직인다"가 아니라
        /// <b>직전 낙하의 잔여 운동</b>이다. 섞이면 이 표본은 무효이므로, 조용히 재지 않고
        /// 이탈 사실을 그대로 드러낸다.</para>
        ///
        /// <para><b>네거티브 컨트롤은 어디에 있나.</b> "0이 나왔다"가 의미를 가지려면 같은 계측 경로가
        /// 0이 아닌 값도 낼 수 있어야 한다. 그 증명은 이 파일의 (1)/(3)이 맡는다 — 같은
        /// <c>HemPoints()</c>/<c>FillPoints()</c>로 낙하 중 <b>0.150유닛</b>(획의 418%)을 잰다.
        /// 그래서 여기서 별도의 대조군을 만들지 않는다(같은 것을 두 벌로 적는 대신 참조한다).</para>
        /// </summary>
        [UnityTest]
        public IEnumerator 가만히_서_있으면_망토는_정적이다()
        {
            yield return LoadSceneAndPinIdle();
            var rig = TestRig.Build();

            Vector3[] still = rig.HemPoints();
            float maxShift = 0f;
            float idleSeconds = 0f, offIdleSeconds = 0f, previous = 0f;
            int idleFrames = 0, offIdleFrames = 0;

            yield return TestClock.SampleForSeconds(StaticWindowSeconds, elapsed =>
            {
                float dt = elapsed - previous;
                previous = elapsed;

                if (rig.Agent.Blackboard.Machine.CurrentStateId != StickmanStateId.Idle)
                {
                    offIdleSeconds += dt;
                    offIdleFrames++;
                    return;
                }
                idleSeconds += dt;
                idleFrames++;
                maxShift = Mathf.Max(maxShift, MaxDistance(still, rig.HemPoints()));
            });

            Debug.Log($"{LogPrefix} Idle {idleSeconds:F3}초({idleFrames}프레임, 이탈 " +
                $"{offIdleSeconds:F3}초/{offIdleFrames}프레임) 동안 밑단 최대 이동 = {maxShift:F6}유닛 " +
                $"(획 두께 {rig.Stroke:F5}의 {maxShift / rig.Stroke:P3}). " +
                $"이 표본 창이 잡아낼 수 있는 최소 누출 속도 = {1e-4f / StaticWindowSeconds:F6}유닛/초.");

            // ① 진단 먼저 — 표본이 실제로 Idle이었는가(위 클래스 주석 "왜 따로 세는가").
            Assert.AreEqual(0, offIdleFrames,
                $"{LogPrefix} 표본 {StaticWindowSeconds:F1}초 중 {offIdleSeconds:F3}초" +
                $"({offIdleFrames}프레임)를 Idle이 아닌 상태로 보냈습니다 — 착지 뒤 기류가 잦아드는 " +
                "0.62초가 Idle 프레임에 섞여 들어오므로 이 표본은 무효입니다. " +
                "자율 상태 전이가 생겼는지(발판 상실/안전망 로그) 먼저 확인하세요.");
            Assert.Greater(idleSeconds, StaticWindowSeconds * 0.9f,
                $"{LogPrefix} Idle 표본이 {idleSeconds:F3}초뿐입니다(예산 {StaticWindowSeconds:F1}초) — " +
                "표본이 비어 있으면 아래 단언은 아무것도 재지 않고 통과합니다.");

            // ② 본 단언 — 문턱은 2026-08-31 원본 그대로 1e-4유닛이다.
            Assert.Less(maxShift, 1e-4f,
                $"{LogPrefix} 가만히 서 있는데 망토가 {maxShift:F6}유닛 움직였습니다 — 원칙 1 위반입니다. " +
                $"(표본 {idleSeconds:F2}초이므로 누출 속도로는 약 {maxShift / Mathf.Max(idleSeconds, 0.0001f):F6}유닛/초)");
        }

        // ==================== 유틸 ====================

        private static float MaxDistance(Vector3[] a, Vector3[] b)
        {
            float max = 0f;
            int n = Mathf.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++) max = Mathf.Max(max, Vector3.Distance(a[i], b[i]));
            return max;
        }

        /// <summary>씬에서 찾아낸 검사 대상 묶음 + 상태를 프레임마다 못 박는 헬퍼.</summary>
        private sealed class TestRig
        {
            public StickmanAgent Agent;
            public CharacterAccessoryRenderer Renderer;
            public LineRenderer CapeLine;
            public Mesh CapeFill;
            public float Stroke;

            public static TestRig Build()
            {
                var agent = Object.FindFirstObjectByType<StickmanAgent>();
                Assert.IsNotNull(agent, "StickmanAgent가 없습니다.");
                var renderer = Object.FindFirstObjectByType<CharacterAccessoryRenderer>();
                Assert.IsNotNull(renderer, "CharacterAccessoryRenderer가 없습니다.");

                var rig = new TestRig { Agent = agent, Renderer = renderer, Stroke = renderer.StrokeWidth };
                rig.EnsureRefs();
                return rig;
            }

            /// <summary>★ 매번 다시 찾는다. 액세서리 컨테이너는 서명(방향/색/배율/착용)이 바뀌면 통째로
            /// Destroy되고 다시 구워지므로, 캐시한 참조를 그대로 들고 있으면 재구성이 한 번만 일어나도
            /// 테스트가 <b>파괴된 객체</b>를 읽어 엉뚱한 실패를 낸다.</summary>
            private void EnsureRefs()
            {
                if (CapeLine == null)
                {
                    foreach (var lr in Renderer.GetComponentsInChildren<LineRenderer>(true))
                        if (lr.name == "CapeOutline") CapeLine = lr;
                    Assert.IsNotNull(CapeLine, "CapeOutline 선을 찾지 못했습니다 — 망토가 그려지지 않았습니다.");
                }
                if (CapeFill == null)
                {
                    foreach (var mf in Renderer.GetComponentsInChildren<MeshFilter>(true))
                        if (mf.name == "CapeOutlineFill") CapeFill = mf.sharedMesh;
                    Assert.IsNotNull(CapeFill, "CapeOutlineFill 메시를 찾지 못했습니다.");
                }
            }

            /// <summary>지금 그려지고 있는 밑단 점(인덱스 2~6)만.</summary>
            public Vector3[] HemPoints()
            {
                EnsureRefs();
                var all = new Vector3[CapeLine.positionCount];
                CapeLine.GetPositions(all);
                var hem = new Vector3[HemEnd - HemStart + 1];
                for (int i = 0; i < hem.Length; i++) hem[i] = all[HemStart + i];
                return hem;
            }

            /// <summary>채움 메시의 <b>전체</b> 정점(인덱스가 선과 1:1이라 비교에 그대로 쓴다).</summary>
            public Vector3[] FillPoints()
            {
                EnsureRefs();
                return CapeFill.vertices;
            }

            /// <summary>한 프레임 진행하고, 다음 LateUpdate가 보게 될 상태/속도를 못 박는다.</summary>
            public IEnumerator HoldFall(Vector2 velocity)
            {
                yield return null;
                Force(StickmanStateId.Fall, velocity, 0f);
            }

            /// <summary>
            /// 같은 것을 던지기 회전으로. <paramref name="rootAngle"/>는 루트 시각 회전각(도),
            /// <paramref name="seconds"/>는 그 상황을 붙잡고 있을 <b>벽시계</b> 예산이다.
            ///
            /// <para>★ 2026-09-01 — 옛 구현은 "6프레임"이었다. 배치모드 8,200~9,700fps에서 그것은
            /// <b>0.0007초</b>다. 기류 세기 상승이 지금은 즉시라 우연히 맞았을 뿐, 상승이 시간에 걸리게
            /// 바뀌면 아무것도 못 재는 예산이 된다. 그래서 예산의 단위를 초로 바꿨다 —
            /// 얼마를 줄지는 <b>부르는 쪽</b>이 그 표본에서 무엇을 기다리는지(세기 상승이냐 회전
            /// 반영이냐) 알고 정한다. <see cref="AirFlowRiseSeconds"/> / <see cref="RootRotationSeconds"/>
            /// 문서에 각각의 근거가 있다.</para>
            /// </summary>
            public IEnumerator HoldTumble(Vector2 velocity, float rootAngle, float seconds)
            {
                yield return TestClock.SampleForSeconds(seconds, _ =>
                {
                    Agent.Blackboard.LastThrowVelocity = velocity;
                    Force(StickmanStateId.ThrowTumble, velocity, rootAngle);
                });
                yield return null; // 마지막으로 못 박은 값이 LateUpdate에 반영된 프레임을 하나 더 지난다.
            }

            private void Force(StickmanStateId id, Vector2 velocity, float rootAngle)
            {
                StickmanBlackboard bb = Agent.Blackboard;
                if (bb.Machine.CurrentStateId != id) bb.Machine.ChangeState(id, isForcedInterrupt: true);
                if (bb.Body != null)
                {
                    bb.Body.linearVelocity = velocity;
                    bb.Body.rotation = rootAngle;
                }
                Agent.transform.rotation = Quaternion.Euler(0f, 0f, rootAngle);
            }
        }

        private IEnumerator LoadSceneAndPinIdle()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(agent, $"{LogPrefix} StickmanAgent가 없습니다.");
            Assert.IsNotNull(agent.Blackboard, $"{LogPrefix} 블랙보드가 없습니다.");
            agent.Blackboard.IntentSource = new StillIntentSource();

            StickConfig config = agent.Config;
            for (int guard = 0; guard < 4096 && CharacterProgressionModel.Level < 4; guard++)
                CharacterProgressionModel.AddXp(CharacterProgressionModel.XpToNextLevel(config) + 1f, config);

            // 실제 저장 파일의 차림이 관측을 오염시키지 않게 전부 벗기고 망토만 걸친다.
            for (int i = 0; i < EquipmentModel.SlotCount; i++)
                EquipmentModel.TryWear((EquipmentSlot)i, EquipmentModel.NotWorn, config);
            EquipmentModel.TryWear(EquipmentSlot.Shoulders, Cape, config);
            Assert.AreEqual(Cape, EquipmentModel.WornIndex(EquipmentSlot.Shoulders),
                $"{LogPrefix} 짧은망토를 걸치지 못했습니다.");

            // ★ 2026-09-01 — 손으로 짠 대기 루프를 공용 도구(TestClock)로 바꿨다. 홀드는 0.5초에서
            //   <b>0.7초</b>로 늘렸다: 앱 시작 낙하로 켜진 기류는 착지 뒤 AirFlowSettleSeconds(0.62초)에
            //   걸쳐 잦아들고 그 구간은 이미 Idle이다. 0.5초 홀드로는 "아직 잦아드는 중"인 천을 기준
            //   원본으로 찍을 수 있었다(그 뒤 표본이 2초로 넓어지면서 실제로 위험해진 자리다).
            yield return TestClock.WaitForState(
                agent.Blackboard, StickmanStateId.Idle, timeoutSeconds: 15f, holdSeconds: 0.7f);

            // 액세서리 재구성이 끝날 때까지(장비 변경 -> 다음 LateUpdate) 넉넉히 기다린다.
            for (int i = 0; i < 8; i++) yield return null;
        }

        private sealed class StillIntentSource : IMovementIntentSource
        {
            public float MoveInputX => 0f;
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }
    }
}
