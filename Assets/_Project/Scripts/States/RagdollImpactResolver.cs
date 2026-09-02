using UnityEngine;
using StickMate.Core;

namespace StickMate.States
{
    /// <summary>
    /// RAGDOLL 강제 전이 판정("충격량 &gt;= ragdollForceThreshold면 Ragdoll로, 아니면 그대로")을 한
    /// 곳에 모은 순수 함수 헬퍼(부작용은 blackboard.Machine.ChangeState 호출 하나뿐).
    ///
    /// 왜 필요한가(Phase 3): Core/StickmanAgent.ReportExternalImpact()가 원래 이 판정의 유일한
    /// 진입점이었지만(Phase 2, "단일 진입점" 설계), 그 메서드는 MonoBehaviour 인스턴스 메서드라 블랙보드만
    /// 가진 순수 상태/컨트롤러 클래스(States/DragThrowState.cs — 던진 속도로부터 계산한 충격량,
    /// States/RodeoCursorState.cs — 거친 흔들기로 튕겨 떨어질 때)에서 직접 호출할 수 없다
    /// (참조 대상이 다름). 이 정적 유틸로 로직을 분리해
    /// 세 곳 이상에서 같은 판정식이 어긋나지 않게 한다 — StickmanAgent.ReportExternalImpact()도 내부적으로
    /// 이 메서드를 호출하도록 리팩터했다(공개 시그니처는 그대로, 내부 구현만 위임).
    ///
    /// ════════════════════════════════════════════════════════════════════════════════════
    /// ★ 2026-09-02 — RAGDOLL 진입 경로의 <b>정본 목록</b>(전부 실측. CLAUDE.md "캐릭터 무빙 방식" 절과 짝)
    /// ════════════════════════════════════════════════════════════════════════════════════
    /// 이 목록이 실재와 갈라진 채로 여러 파일에 복사돼 있었고, 그 때문에 <b>"랙돌이 고장났다"는 오진이
    /// 반복해서 올라왔다.</b> 그래서 목록은 <b>여기 한 곳에만</b> 두고 다른 파일은 이 절을 가리키기만 한다.
    /// <list type="bullet">
    /// <item><b>커서로 거칠게 털어내기 — 살아 있다.</b> States/RodeoCursorState가 임계값 x
    ///   rodeoShakeImpactMultiplier(1.25)를 <b>강제</b>하므로 구조적으로 항상 전이한다. 배포 기본값에서
    ///   확실히 도는 사실상 유일한 경로다.</item>
    /// <item><b>「던짐」 — 폐지됐다.</b> 사용자 요청 2026-08-29(원문은 States/DragThrowState.cs의
    ///   "★★ 던진 뒤 무엇이 되는가" 절이 인용한다). throwTumbleEnabled=1이라 깨끗한 던지기는
    ///   ThrowTumble(공중 회전 -> 무릎앉아)로 가고, 랙돌 분기는 그 스위치를 꺼야만 닿는다.
    ///   <b>되살리지 마라 — 사용자가 닫은 문이다.</b></item>
    /// <item><b>「추락 충격」 — 끊겨 있다.</b> landingImpactRagdollShield=1이 아래
    ///   <see cref="IsOwnLandingContact"/>로 자기 착지를 걸러낸다.</item>
    /// <item><b>루트의 물리 충돌 — 원리상 살아 있다.</b> 루트 질량 1.00이라 필요 상대속도가 곧 임계값
    ///   8.0이고 dragThrowMaxSpeed 12.0 안에 든다. 다만 차단막이 <b>발밑에서 올라온 접촉</b>을 걸러내므로,
    ///   남는 것은 그 예외를 벗어난 접촉(옆/윗면, Dynamic 상대)뿐이다.</item>
    /// <item><b>긴 망토 자락 밟기 — 잠재.</b> Interaction/LongCapeTripDirector가 임계값 x1.02를 넣지만
    ///   longCapeTripMeanSeconds=0(2026-08-31 사용자 요청으로 기본 OFF)이라 배포 기본값에서는 돌지 않는다.
    ///   켜면 즉시 살아나므로 <b>"경로가 없다"고 적으면 안 된다.</b></item>
    /// <item><b>팔다리 8개의 물리 충돌 — 도달 불가</b>(2026-09-02 실측, <b>이번 라운드 미수정</b>).
    ///   Core/RagdollLimbImpactRelay가 <c>relativeVelocity.magnitude * _body.mass</c>를 넘기는데 그
    ///   <c>_body</c>는 팔다리다(프리팹 실측 질량 0.06 x4 / 0.09 x4, 루트만 1.00). 임계값 8.0에 닿으려면
    ///   상대속도 88.9~133.3 유닛/초가 필요한데 dragThrowMaxSpeed는 12.0이다.
    ///   아래 <see cref="LogCollisionImpact"/>의 <c>보고바디</c> 항목이 이것을 로그로 가르려고 생겼다 —
    ///   <b>가르기 전에 임계값이나 릴레이 질량을 만지지 마라</b>(어느 경로를 고쳤는지 모르게 된다).</item>
    /// </list>
    /// </summary>
    public static class RagdollImpactResolver
    {
        /// <summary>
        /// "발밑에서 올라온 충돌"로 볼 접촉점의 높이 상한 — 발 높이 + 신장의 이 비율까지. 루트 원점이
        /// 곧 발바닥이라는 이 프로젝트의 규약(StickmanBlackboard.SenseGround 문서)을 그대로 쓴다.
        /// 0이 아니라 여유를 두는 이유: 캡슐 콜라이더의 접촉점은 정확히 발바닥 선에 찍히지 않고,
        /// 빠른 낙하에서는 한 물리 스텝 안에 몇 센티 파고든 뒤 접촉이 생성되기 때문이다. 반대로 이
        /// 값이 크면 옆에서 들어온 타격까지 착지로 오인하므로 신장의 20%(무릎 높이 언저리)로 묶는다.
        /// </summary>
        private const float LandingContactHeightRatio = 0.2f;

        /// <summary>
        /// ★ 충돌 콜백 전용 진입점(2026-08-29, "무릎앉아 착지" 라운드). 충격량 판정 자체는 아래
        /// <see cref="TryApplyImpact"/>와 완전히 같고, 그 앞에 **"이건 외력이 아니라 내 착지다"** 라는
        /// 한 가지 예외만 둔다.
        ///
        /// 왜 필요한가: 씬에는 눈에 보이지 않는 물리 바닥이 있고(Editor/SceneBootstrapper의 PhysicsGround),
        /// 캐릭터 루트는 능동 상태에서도 Dynamic이라 낙하 중 그 바닥에 부딪히면 OnCollisionEnter2D가
        /// 발생한다. 충격량은 relativeVelocity * mass(=1)이고 ragdollForceThreshold가 8, gravityScale이
        /// 3이므로 계산상 v = sqrt(2*9.81*3*h) = 8, 즉 **1.09유닛만 떨어져도** 임계값을 넘는다.
        ///
        /// ★ 다만 실측 결과는 계산보다 좁았다(정직하게 기록 — 2026-08-29 PlayMode 실측 로그 [착지충격]).
        /// **논리 발판이 있는 정상 착지에서는 이 충격량이 0.00이다.** FallState의 스윕 교차 판정이
        /// Update에서 먼저 착지를 확정하면서 몸을 발판 상단으로 스냅하고 하강 속도를 지우기 때문에,
        /// 그 다음 물리 스텝에서 생기는 접촉은 이미 정지 상태의 안착 접촉이다. 그래서 이 예외가 실제로
        /// 겨냥하는 것은 **물리 바닥은 있는데 논리 발판은 없는 구간**이다 —
        /// Editor/SceneBootstrapper.CreateGroundCollider가 명시한 그 상황(화면 최하단 안전망은 Dock 가로
        /// 구간에 구멍이 있는 반면 PhysicsGround는 전체 폭이라, 그 구간에서 캐릭터는 "물리적으로는
        /// 떠받쳐지지만 논리적으로는 접지하지 않는다"). 그리로 떨어지면 착지가 확정되지 않아 스냅도
        /// 없고, 몸이 전속력 그대로 바닥에 부딪혀 즉시 랙돌이 된다. 사용자가 실제 데스크톱에서 캐릭터를
        /// Dock 구간으로 던지거나 떨어뜨렸을 때 밟게 되는 경로가 그것이다.
        /// Tests/PlayMode/LandingCrouchTests가 그 시나리오로 이 스위치의 on/off 대조를 실측한다.
        ///
        /// 판정 근거는 아키텍처 0절이다 — RAGDOLL이 배정된 대상은 **외력**이고, 자기가
        /// 떨어져서 땅에 닿는 것은 외력이 아니라 착지다(★ 2026-09-02 정정: 원래 «피격/던져짐 같은
        /// 외력»이었는데 「던져짐」은 2026-08-29에 폐지됐다 — 실재 경로는 이 클래스 문서의 정본 목록 참고). 그래서 다음 두 조건을 **동시에** 만족할 때만
        /// 무시한다(★ 2026-08-30 갱신 — (1)이 상태 허용목록에서 "부딪힌 대상"으로 바뀌었다.
        /// 근거는 <see cref="IsOwnLandingContact"/> 본문 주석):
        ///   (1) 부딪힌 상대가 Dynamic 바디가 아니다(= 정적 지면/바닥이지 움직이는 물체가 아니다),
        ///   (2) 접촉점이 발 높이 근처 이하다(= 발밑에서 올라온 면).
        /// 그래서 옆에서 날아온 물체나 던져져 벽에 부딪히는 충돌은 그대로 랙돌이 되고,
        /// 직접 호출 경로(DragThrowState의 던진 속도 / RodeoCursorState의
        /// 거친 흔들기)는 애초에 이 메서드를 거치지 않으므로 전혀 영향을 받지 않는다.
        /// StickConfig.landingImpactRagdollShield를 끄면 이 예외 전체가 사라져 예전 거동으로 되돌아간다
        /// (Tests/PlayMode/LandingCrouchTests.cs의 네거티브 컨트롤이 그 스위치를 쓴다).
        /// </summary>
        /// <returns>Ragdoll로 전이시켰으면 true.</returns>
        public static bool TryApplyCollisionImpact(StickmanBlackboard blackboard, Collision2D collision, float impulseMagnitude)
        {
            bool shielded = IsOwnLandingContact(blackboard, collision);
            LogCollisionImpact(blackboard, collision, impulseMagnitude, shielded);
            if (shielded) return false;
            return TryApplyImpact(blackboard, impulseMagnitude, ResolveContactPushDirection(collision));
        }

        /// <summary>
        /// 랙돌 임계값 <b>미만</b> 타격의 시각 리액션(2026-09-01) — 상체를 밀린 방향으로 짧게 기울였다
        /// 되돌린다(States/StickmanPoseAnimator.AddHitLean). 순수 시각 트윈이라 물리에 아무 것도 더하지
        /// 않으며, 스스로 감쇠하므로 취소 배관도 없다.
        ///
        /// <para>세기는 <b>임계값 대비 비</b>로 정규화한다 — 충격량의 절대 단위(질량 x 속도)는 배율/설정에
        /// 따라 움직이지만 "임계값의 몇 %인가"는 무차원이라 어떤 설정에서도 같은 그림이 나온다.</para>
        ///
        /// <para>방향을 모르면(무방향 호출 경로) <b>뒤로 젖힌다</b>: 방향을 모른다고 앞으로 숙이면
        /// "맞았는데 달려드는" 그림이 되지만, 뒤로 젖히는 것은 어느 방향에서 맞았든 '움찔'로 읽힌다.</para>
        /// </summary>
        private static void ApplyHitLean(StickmanBlackboard blackboard, float impulseMagnitude, Vector2 hitDirection)
        {
            float maxDegrees = blackboard.HitBodyLeanDegrees;
            if (maxDegrees <= 0f) return;

            StickmanPoseAnimator pose = blackboard.GetPoseAnimator();
            if (pose == null) return;

            float threshold = Mathf.Max(0.0001f, blackboard.Config.ragdollForceThreshold);
            float strength = Mathf.Clamp01(impulseMagnitude / threshold);
            float forward = hitDirection.sqrMagnitude > 0.000001f
                ? Mathf.Clamp(hitDirection.normalized.x * pose.FacingSign, -1f, 1f)
                : -1f;

            pose.AddHitLean(maxDegrees * strength * forward, blackboard.HitBodyLeanRecoverRate);
        }

        /// <summary>
        /// ★ 2026-09-01 (P9-b) 충돌에서 "어느 쪽으로 밀려나는가"를 뽑는다 — 접촉 법선의 평균.
        ///
        /// 왜 <see cref="Collision2D.relativeVelocity"/>가 아니라 법선인가: relativeVelocity는 "두 물체의
        /// 상대 속도"라 부호 규약이 문서만으로 확정되지 않고(어느 쪽에서 뺀 것인지), 우리 쪽이 정지해
        /// 있고 상대가 날아온 경우와 그 반대에서 서로 다른 해석이 필요하다. 반면 <c>ContactPoint2D.normal</c>은
        /// "상대 콜라이더에서 나(이 충돌 콜백을 받은 쪽)를 향하는" 표면 법선이라, 그것이 곧 <b>내가 밀리는
        /// 방향</b>이다(이 프로젝트가 이미 접지 판정에서 같은 규약을 쓰고 있다 — 바닥에 떨어지면 법선이 +y).
        ///
        /// 접촉이 여러 개면 평균한다. 양쪽에서 동시에 끼인(법선이 서로 상쇄되는) 이론상의 경우에는
        /// 결과가 0이 되고, 그러면 RagdollRig가 충격량 경로를 건너뛴다 — <b>추정한 방향으로 때리는 것보다
        /// 안 때리는 쪽이 정직하다</b>(방향을 모르면 P9-a 이전 거동 그대로).
        /// </summary>
        private static Vector2 ResolveContactPushDirection(Collision2D collision)
        {
            if (collision == null) return Vector2.zero;
            int count = collision.contactCount;
            if (count <= 0) return Vector2.zero;

            Vector2 sum = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                sum += collision.GetContact(i).normal;
            }
            return sum;
        }

        /// <summary>
        /// 충돌 충격 통지의 진단 로그. 리더가 화면을 볼 수 없으므로 "착지 충격이 어디로 갔는가"는 로그가
        /// 유일한 판별 수단이다 — 그런데 24시간 상주 앱이라 매 충돌마다 남기면 로그가 무너진다. 그래서
        /// [눈추적]/[발판리포트]와 동일한 컨벤션을 쓴다: 기본적으로 시작 직후 CollisionLogSampleCount회까지만,
        /// 그 뒤로는 StickConfig.verboseDiagnosticsLogging을 켰을 때 임계값 이상만.
        ///
        /// ★★ 2026-08-30 (리더 지시 2항, 디버거 지적) — **RAGDOLL로 실제로 이어지는 충돌은 표본 제한과
        /// 무관하게 항상 남긴다.** 직전 라운드에 이 로그가 "시작 직후 6건만 남기고 침묵"해서, 정작 문제의
        /// RAGDOLL 5건은 **원인 줄이 하나도 안 남았다**(가장 중요한 사건의 원인만 안 찍히는 구조였다).
        /// 로그 예산은 원래 "약한 충돌이 홍수를 이루는 것"을 막으려는 것이지 사건 자체를 감추려는 것이
        /// 아니다. RAGDOLL 전이는 이산적이고 드문 사건이며(정상 동작에서는 0회 — 회귀 테스트가
        /// RAGDOLL 0회를 절대 조건으로 잠근다) 자기 착지는 차단막이 먼저 걸러내므로, 이 완화가 로그를
        /// 무너뜨릴 수 있는 유일한 경우는 "RAGDOLL이 폭주하는 상황" 뿐이다 — 그건 정확히 로그가 필요한
        /// 상황이다. 반대로 **표본 예산은 소비하지 않는다**: 그 예산의 목적은 "충돌이 아예 안 나는 것"과
        /// "나는데 약한 것"을 구분하는 초기 표본이므로, RAGDOLL 사건이 그 자리를 빼앗으면 안 된다.
        ///
        /// ★★★ 2026-09-02 (진단 전용 — 판정 거동은 한 줄도 바뀌지 않았다) — <b>누가 보고했는지</b>를
        /// 이 줄이 말하게 했다. 그 전까지 이 줄은 충격량·상태·접촉수·발y만 적고 <b>보고한 바디를
        /// 적지 않았다</b>. 충돌 통지는 루트(Core/StickmanAgent.OnCollisionEnter2D)와 비루트 파츠 8개
        /// (Core/RagdollLimbImpactRelay)가 <b>같은 진입점</b>으로 모이는데, 두 경로는 임계값
        /// (StickConfig.ragdollForceThreshold)을 <b>같은 숫자</b>로 비교하면서 곱하는 질량이 다르다
        /// (프리팹 실측: 루트 1.00 / 팔다리 0.06·0.09). 그래서 관측된 랙돌 전이가 어느 쪽에서 왔는지
        /// <b>로그만으로는 가를 수 없었고</b>, 그 상태로 임계값을 만지면 "고쳤는데 안 고쳐진 것"을 못 본다.
        ///
        /// <para>보고 바디는 <c>Collision2D.otherRigidbody</c>(= 이 콜백을 받은 쪽, 상대는
        /// <c>Collision2D.rigidbody</c> — <see cref="IsOwnLandingContact"/>가 이미 쓰는 그 규약)에서
        /// 뽑는다. 호출부를 한 줄도 건드리지 않아도 되기 때문이다.</para>
        ///
        /// <para>★ 다만 "그것이 정말 충격량을 만든 바디인가"는 <b>별개의 주장</b>이라 같은 줄 안에서
        /// 되잰다. 두 호출부 모두 충격량 = <c>relativeVelocity.magnitude * mass</c>이므로
        /// <c>역산질량 = 충격량 / 상대속도</c>가 <c>질량</c>과 어긋나면 이 줄의 보고 바디를 믿으면 안 된다.
        /// 이 저장소가 반복해서 당한 형태가 <b>"죽은 프로브의 출력이 성공한 프로브와 똑같이 생긴 것"</b>이라,
        /// 진단용 줄일수록 자기 자신을 반증할 수 있어야 한다.</para>
        /// </summary>
        private const int CollisionLogSampleCount = 6;

        // ★ 2026-09-02 — 표본 예산을 **루트/비루트로 갈랐다**(하나였을 때의 결함은 위 문서 참고).
        // 예산이 하나면 시작 직후 루트의 낙하 충돌이 6건을 전부 먹고, 팔다리가 보고를 하는지조차
        // 로그에 한 줄도 안 남는다 — 그러면 "팔다리 경로가 죽었다"는 가설을 반증도 입증도 못 한다.
        // 상주 비용은 시작 직후 최대 6+6줄로 여전히 상수다.
        private static int _rootCollisionLogSamplesLeft = CollisionLogSampleCount;
        private static int _limbCollisionLogSamplesLeft = CollisionLogSampleCount;

        /// <summary>표본 예산 한 칸을 쓰고 "이번 건을 적어도 되는가"를 답한다. 규칙은 예산이 하나였을
        /// 때와 글자 그대로 같고, 버킷만 둘로 갈렸다. 문자열을 전혀 만들지 않는다(24시간 상주 앱).</summary>
        private static bool ConsumeCollisionLogSample(ref int samplesLeft, bool verbose,
            float impulseMagnitude, float threshold)
        {
            if (samplesLeft > 0)
            {
                samplesLeft--;
                return true;
            }
            return verbose && impulseMagnitude >= threshold;
        }

        private static void LogCollisionImpact(StickmanBlackboard blackboard, Collision2D collision,
            float impulseMagnitude, bool shielded)
        {
            if (blackboard == null || blackboard.Config == null) return;

            // ★ 2026-08-29 — "외력으로 판정 -> RAGDOLL 전이"를 shielded==false일 때 무조건 적었던 것을
            // 고쳤다. 이 로그는 TryApplyImpact()가 실제로 임계값과 비교하기 *전에* 찍히므로, shielded가
            // false라도 impulseMagnitude가 임계값 미만이면 RAGDOLL 전이는 일어나지 않는다 — 그런데도
            // "전이"라고 단정해 로그만 보고 오판하게 만들었다(디버거가 실사용 조사 중 발견).
            bool willRagdoll = !shielded && impulseMagnitude >= blackboard.Config.ragdollForceThreshold;

            // 보고 바디 식별은 게이트보다 **앞**에서 끝낸다(버킷을 고르는 데 필요하다). 여기까지는
            // 참조 비교와 네이티브 조회뿐이라 할당이 없다 — UnityEngine.Object.name은 호출마다 새
            // string을 만들므로 게이트를 통과한 뒤에만 읽는다.
            Rigidbody2D reporter = collision != null ? collision.otherRigidbody : null;
            bool reporterIsRoot = reporter != null && ReferenceEquals(reporter, blackboard.Body);

            // ★★ 2026-08-30 — RAGDOLL로 이어지는 충돌은 표본 예산을 **소비하지도, 확인하지도 않는다**
            // (위 메서드 문서의 근거 참고). 그 외의 약한 충돌만 예전 규칙(초기 표본 6건 + verbose 토글)을 탄다.
            if (!willRagdoll)
            {
                // 충돌 진입은 이산 이벤트(매 프레임이 아니다)라 시작 직후 몇 건은 세기와 무관하게 전부 남긴다 —
                // "충돌이 아예 발생하지 않는 것"과 "발생했는데 약한 것"을 구분하지 못하면 진단이 불가능하기 때문이다.
                // 그 표본을 다 쓰면 임계값 이상만, 그것도 verboseDiagnosticsLogging이 켜져 있을 때만 남긴다.
                //
                // ★ 보고 바디가 불명(otherRigidbody 없음)이면 **비루트 버킷**으로 보낸다 — 루트 예산을
                //   잠식하지 않게 하려는 것뿐이고, 아래 로그는 그런 건을 "루트"라고 주장하지 않는다.
                bool verbose = blackboard.Config.verboseDiagnosticsLogging;
                float threshold = blackboard.Config.ragdollForceThreshold;
                bool allowed = reporterIsRoot
                    ? ConsumeCollisionLogSample(ref _rootCollisionLogSamplesLeft, verbose, impulseMagnitude, threshold)
                    : ConsumeCollisionLogSample(ref _limbCollisionLogSamplesLeft, verbose, impulseMagnitude, threshold);
                if (!allowed) return;
            }

            float footY = blackboard.Body != null ? blackboard.Body.position.y : float.NaN;
            float lowestContactY = float.NaN;
            int count = collision != null ? collision.contactCount : 0;
            for (int i = 0; i < count; i++)
            {
                float y = collision.GetContact(i).point.y;
                if (float.IsNaN(lowestContactY) || y < lowestContactY) lowestContactY = y;
            }

            string verdict = shielded ? "착지로 판정해 무시"
                : willRagdoll ? "외력으로 판정, 임계값 초과 -> RAGDOLL 전이"
                : "외력으로 판정했으나 임계값 미만 -> 전이 없음";

            // 보고 바디의 자기검증(위 문서 참고): 두 호출부 모두 충격량 = 상대속도 x 질량이므로
            // 역산질량과 질량이 같아야 한다. 상대속도가 0이면(스냅 뒤 안착 접촉) 역산이 불가능해 NaN이고,
            // 그것은 자기검증을 못 했다는 뜻이지 불일치가 아니다 — "재지 못함"과 "맞음"을 같게 적지 않는다.
            float reporterMass = reporter != null ? reporter.mass : float.NaN;
            float relativeSpeed = collision != null ? collision.relativeVelocity.magnitude : float.NaN;
            float impliedMass = relativeSpeed > 0.0001f ? impulseMagnitude / relativeSpeed : float.NaN;
            string reporterRole = reporter == null ? "불명" : reporterIsRoot ? "루트" : "비루트";

            Debug.Log($"[착지충격] 보고바디={(reporter != null ? reporter.name : "?")}" +
                $"({reporterRole}, 질량={reporterMass:F3}, 역산질량={impliedMass:F3}, " +
                $"상대속도={relativeSpeed:F2}), 충돌 충격량={impulseMagnitude:F2}(랙돌 임계 " +
                $"{blackboard.Config.ragdollForceThreshold:F1}), 상태=" +
                $"{(blackboard.Machine != null ? blackboard.Machine.CurrentStateId.ToString() : "?")}, " +
                $"접촉 {count}개(최저 y={lowestContactY:F3}), 발 y={footY:F3}, " +
                $"차단스위치={blackboard.Config.landingImpactRagdollShield} -> {verdict}.");
        }

        /// <summary>위 문서의 (1)+(2) 판정. 판단에 필요한 것이 하나라도 없으면 안전한 쪽(= 예외 아님,
        /// 즉 기존 거동)으로 false를 돌려준다.</summary>
        public static bool IsOwnLandingContact(StickmanBlackboard blackboard, Collision2D collision)
        {
            if (blackboard == null || blackboard.Machine == null || blackboard.Body == null) return false;
            if (blackboard.Config != null && !blackboard.Config.landingImpactRagdollShield) return false;

            // ★★ 2026-08-30 (디버거) — **상태 허용목록을 없앴다.**
            // 예전에는 Fall/Jump/LandingCrouch/ThrowTumble 넷일 때만 차단했다. 그런데 이 프로젝트의
            // 발판(Dock/창 상단)은 논리 발판일 뿐 물리 콜라이더가 없어서, **접지 스냅을 부르지 않는
            // 어떤 상태든** 그 위에서 자유낙하해 물리 바닥에 전속력으로 부딪힌다. 그때 상태가 저 넷에
            // 없으면(Attack/Getup/… — 당시엔 BattleMinigame도) 자기 착지가 외력으로 오판되어 RAGDOLL이 됐다 —
            // 실제 앱 로그의 "[착지충격] 충돌 충격량=10.01 ... 상태=BattleMinigame ... -> RAGDOLL 전이"가
            // 그 증거이고, 사용자 신고 "갑자기 독 아래로 떨어지면서 관절이 이상하게 꺾임"의 그림이다.
            // 목록을 늘리는 것은 같은 실패를 다음 상태에 미루는 일이라, 판정 기준 자체를 상태가 아니라
            // **부딪힌 대상**으로 바꾼다:
            //   · 정적(또는 비-Dynamic) 콜라이더가 발밑에서 올라온 것  = 지면/바닥 = 내 착지  -> 차단
            //   · Dynamic 바디와의 충돌                                = 외력            -> 그대로 랙돌
            // 이 규칙은 상태 목록에 의존하지 않으므로 새 상태가 생겨도 자동으로 옳다.
            // 직접 호출 경로(DragThrowState의 던진 속도 /
            // RodeoCursorState의 거친 흔들기)는 애초에 이 메서드를 거치지 않아 전혀 영향이 없다.
            if (collision == null) return false;
            Rigidbody2D otherBody = collision.rigidbody;
            if (otherBody != null && otherBody.bodyType == RigidbodyType2D.Dynamic) return false;
            float footY = blackboard.Body.position.y;
            float ceiling = footY + blackboard.CharacterHeightWorld * LandingContactHeightRatio;
            int count = collision.contactCount;
            for (int i = 0; i < count; i++)
            {
                if (collision.GetContact(i).point.y <= ceiling) return true;
            }
            return false;
        }

        /// <summary>
        /// 방향을 모르는 호출 경로(원인 불명의 강제 랙돌, 크기만 아는 통지)용. 방향 0 = 진입 충격량
        /// 없음이므로 <b>P9-a 이전과 비트 단위로 같은 거동</b>이다.
        /// </summary>
        /// <returns>임계값 이상이라 Ragdoll로 전이시켰으면 true, 미만이라 아무 것도 하지 않았으면 false.</returns>
        public static bool TryApplyImpact(StickmanBlackboard blackboard, float impulseMagnitude)
            => TryApplyImpact(blackboard, impulseMagnitude, Vector2.zero);

        /// <summary>
        /// ★ 2026-09-01 (P9-b) 방향까지 아는 호출 경로용. 크기/방향 두 스냅샷을 함께 남기고 전이시킨다 —
        /// RagdollState.Enter()가 그 둘에서 진입 충격량 벡터를 만들어 RagdollRig에 넘긴다.
        /// </summary>
        /// <param name="hitDirection">캐릭터가 <b>밀려나는</b> 방향(월드, 정규화 불필요). 0이면 위 무방향 경로와 같다.</param>
        public static bool TryApplyImpact(StickmanBlackboard blackboard, float impulseMagnitude, Vector2 hitDirection)
        {
            if (blackboard == null || blackboard.Machine == null || blackboard.Config == null) return false;
            if (impulseMagnitude < blackboard.Config.ragdollForceThreshold)
            {
                // ★ 2026-09-01 — 임계값에 못 미치는 타격은 지금까지 **아무 일도 일어나지 않았다**
                // (판정만 하고 조용히 false). 그 구간에 시각 리액션(상체가 밀린 쪽으로 짧게 기울었다
                // 복귀)만 얹는다. 랙돌로 가는 경로는 아래 한 줄도 건드리지 않는다 — 이 분기는 정의상
                // "랙돌로 가지 않는" 쪽이라 진입 각속도/댐핑 튜닝과 교집합이 없다.
                ApplyHitLean(blackboard, impulseMagnitude, hitDirection);
                return false;
            }

            // UX_FLOW.md 31-2 #2 대비 스냅샷 — RagdollState.Enter()가 이 값을 IHasDialogueParams로
            // 노출해 "윽.../으악!/으아아아악?!" 같은 충격 강도별 대사를 파생시킨다(31-1 원칙).
            blackboard.LastImpactMagnitude = impulseMagnitude;
            // 방향은 소비형이다(StickmanBlackboard.LastImpactDirection 문서) — 여기서 덮어쓰고,
            // RagdollState.Enter()가 읽는 즉시 지운다.
            blackboard.LastImpactDirection = hitDirection;
            blackboard.Machine.ChangeState(StickmanStateId.Ragdoll, isForcedInterrupt: true);
            return true;
        }

        // ════════════════════════════════════════════════════════════════════════════════════
        // ★ 2026-09-01 (P9-b) 판정 단위(N·s) -> 연출 단위(도/초) 환산
        // ════════════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 랙돌 <b>판정</b>에 쓰인 원본 충격량을, 랙돌 <b>진입 연출</b>에 실을 충격량(N·s)으로 환산한다.
        /// <see cref="RagdollRig.EnterRagdoll(UnityEngine.Vector2,float)"/>에 넘길 유일한 값의 생산자다.
        ///
        /// ────────────────────────────────────────────────────────────────────────────────────
        /// 왜 원본을 그대로 넘기면 안 되는가 (실측 근거)
        /// ────────────────────────────────────────────────────────────────────────────────────
        /// 실측 감도는 <b>1N·s당 약 42.8도/초</b>다
        /// (<see cref="StickConfig.ragdollEntryAngularSensitivityPerImpulse"/>).
        /// 기존 호출부들이 넘기는 원본 충격량은 <c>ragdollForceThreshold</c>의 1.02배(긴 망토)~5배
        /// (테스트/강한 타격)이고, 5배 = 40N·s를 그대로 넣으면 <b>약 1712도/초 = 초당 4.8바퀴</b>다.
        /// 팽이가 되는 것이지 얻어맞아 넘어지는 그림이 아니다.
        ///
        /// ────────────────────────────────────────────────────────────────────────────────────
        /// 환산식 — 선형 + 상한 클램프. 계수를 직접 적지 않고 <b>역산</b>한다
        /// ────────────────────────────────────────────────────────────────────────────────────
        /// <code>
        ///   scale  = 목표각속도(임계값에서) / 감도 / 임계값     [N·s per 원본 단위]
        ///   capRaw = 임계값 x (상한각속도 / 목표각속도)         [원본 단위]
        ///   결과   = min(원본, capRaw) x scale
        /// </code>
        /// 기본값(임계 8, 목표 100도/초, 상한 400도/초, 감도 42.8)에서:
        /// <list type="bullet">
        /// <item>1.00배(8N·s)  -> 2.34N·s -> <b>100도/초</b>  — 은은하게 픽 넘어진다(긴 망토/최약 피격).</item>
        /// <item>1.50배(12N·s) -> 3.50N·s -> <b>150도/초</b></item>
        /// <item>2.00배(16N·s) -> 4.67N·s -> <b>200도/초</b>  — 대사가 "으악!"으로 바뀌는 지점.</item>
        /// <item>4.00배(32N·s) -> 9.35N·s -> <b>400도/초</b>  — 포화. 대사가 "으아아아악?!"이 되는 지점과 일치.</item>
        /// <item>5.00배(40N·s) -> 9.35N·s -> <b>400도/초</b>  — 클램프가 1712도/초를 막는다.</item>
        /// </list>
        /// 상한을 대사 3구간의 마지막 경계(4배)에 맞춘 것은 우연이 아니다 — 그 위는 이미 "말로 표현되는
        /// 가장 센 충격"이라 물리적으로 더 크게 만들 이유가 없고, 대신 <b>어디서 포화하는지가 대사와
        /// 같은 눈금 위에 있게</b> 된다(원칙 1: 행동과 텍스트가 같은 파라미터에서 파생).
        ///
        /// 상한을 "안 넘어가게 나누기"가 아니라 <c>Mathf.Min</c>으로 둔 이유: 부드러운 포화 곡선
        /// (tanh 등)을 쓰면 임계값 근처의 기울기까지 함께 바뀌어 "약한 충격은 은은하게"가 흐려진다.
        /// 1~4배 구간이 정확히 선형이고 그 밖은 딱 잘리는 편이 튜닝도 검증도 단순하다.
        /// </summary>
        /// <param name="config">null이면 아래 폴백 상수를 쓴다(에디터/손조립 리그).</param>
        /// <param name="rawImpactMagnitude">랙돌 판정에 쓴 그 값(= <see cref="StickmanBlackboard.LastImpactMagnitude"/>).</param>
        /// <returns>진입에 실을 충격량(N·s). 0이면 아무 힘도 가하지 않는다.</returns>
        public static float ResolveEntryImpulse(StickConfig config, float rawImpactMagnitude)
        {
            if (!(rawImpactMagnitude > 0f)) return 0f;   // NaN도 여기서 함께 걸러진다.

            float threshold = config != null ? config.ragdollForceThreshold : 8f;
            float target = config != null ? config.ragdollEntryAngularVelocityAtThreshold : 100f;
            float cap = config != null ? config.ragdollEntryAngularVelocityCap : 400f;
            float sensitivity = config != null ? config.ragdollEntryAngularSensitivityPerImpulse : 42.8f;

            // 셋 중 하나라도 유효하지 않으면 조용히 꺼진다(= P9-a 이전 거동). target <= 0은 이 기능의
            // 공식 OFF 스위치이기도 하다 — StickConfig의 그 필드 툴팁에 적어 두었다.
            if (!(threshold > 0f) || !(target > 0f) || !(sensitivity > 0f)) return 0f;

            // 상한이 목표보다 낮게 설정되면(오설정) 상한을 목표까지 끌어올린다 — 그렇지 않으면
            // capRaw가 임계값보다 작아져 "세게 맞을수록 약해지는" 구간이 생긴다.
            if (cap < target) cap = target;

            float scale = target / sensitivity / threshold;
            float capRaw = threshold * (cap / target);
            return Mathf.Min(rawImpactMagnitude, capRaw) * scale;
        }
    }
}
