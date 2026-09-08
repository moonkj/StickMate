using System;
using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// LFVS 키포즈 <b>한 장</b>. 정본은 <c>docs/UX_MOTION_COSTUME_FOCUS.md</c> 5절 각도표.
    ///
    /// ============================================================================
    /// 왜 배열이 아니라 <b>이름 붙은 각도</b>인가
    /// ============================================================================
    /// 배열은 마디 열거 순서에 의존한다. 그 순서가 바뀌는 날 <b>모든 코스튬의 팔이 한 칸씩 밀리고</b>,
    /// 에셋을 열어봐도 안 보인다. <c>CharacterSaveStore</c>가 창 위치를 <c>Vector2[3]</c>이 아니라
    /// 이름 붙은 9필드로 둔 것과 <b>같은 판단</b>이다.
    ///
    /// ============================================================================
    /// ★ 다리 각도가 <b>여기 없는 것은 누락이 아니다</b>
    /// ============================================================================
    /// 계약서 5-4절의 초안 구조체에는 다리 4각이 있었지만 <b>넣지 않았다</b>.
    /// <c>UX_MOTION_COSTUME_FOCUS</c> 1-3절이 그 이유를 적어 뒀다 — 무릎/고관절을 흔들면
    /// <b>발끝이 지면에서 떨어진다</b>. 접지를 지키려면 무릎 굽힘에 몸통 하강을 짝지어야 하고
    /// (<c>LandingCrouch</c>가 그 기구를 갖고 있다) 그 검증은 이번 범위 밖이다.
    /// 그래서 4종 전부 <b>선 자세</b>로 설계됐고 프롭 높이가 거기 맞춰져 있다.
    ///
    /// <para><b>필드를 두고 무시하면 더 나쁘다</b>: 작성자가 값을 적어 넣고 «왜 안 움직이지»를 겪거나,
    /// 언젠가 누가 배선해 접지 계약(고관절 ±12° / 무릎 −4° 고정)을 조용히 깬다.
    /// <b>표현할 수 없게 만드는 것</b>이 그 사고를 원천에서 없앤다.</para>
    ///
    /// <para>앉는 자세가 필요해지면 그건 별도 라운드이고 <c>LandingCrouch</c>의 하강 기구를
    /// 재사용해야 한다 — <b>미설계</b>다.</para>
    ///
    /// ============================================================================
    /// 각도 규약 (<c>StickmanPoseAnimator</c> 클래스 문서 그대로)
    /// ============================================================================
    /// <c>θu</c> = 어깨/상완, <b>0 = 팔이 바로 아래</b>, <b>+ = 끝이 진행 방향(앞)</b>.
    /// <c>θl</c> = 팔꿈치 굽힘(항상 ≥ 0). 전완 절대각 = <c>θu + θl</c>.
    /// 각도는 <b>facing 중립 공간</b>이고 미러링은 포즈 층이 한다.
    ///
    /// <para><b>이름과 설계 문서의 대응</b>(갈라지면 팔이 통째로 바뀌므로 여기 한 번만 적는다):
    /// <c>right*</c> = <c>Limb.NeutralSign &gt; 0</c> = 설계 문서의 <b>A(앞팔)</b>, Idle 중립 +40°.
    /// <c>left*</c> = <c>NeutralSign &lt; 0</c> = <b>B(뒷팔)</b>, 중립 −40°.</para>
    /// </summary>
    [Serializable]
    public struct CostumeKeypose
    {
        [Tooltip("B(뒷팔) 어깨 각(도). 0 = 팔이 바로 아래, + = 앞쪽.")]
        public float leftUpperArm;

        [Tooltip("B(뒷팔) 팔꿈치 굽힘(도, ≥ 0).")]
        public float leftLowerArm;

        [Tooltip("A(앞팔) 어깨 각(도). 0 = 팔이 바로 아래, + = 앞쪽.")]
        public float rightUpperArm;

        [Tooltip("A(앞팔) 팔꿈치 굽힘(도, ≥ 0).")]
        public float rightLowerArm;

        [Tooltip("상체 기울임(도, + = 앞). 포즈 층의 StickmanPoseAnimator.MaxBodyLeanDegrees(30)로 " +
                 "클램프된다 — 7.60°는 이 필드와 무관한 다른 상수(유휴 미세 흔들림 상한)다. " +
                 "2026-09-08 design-motion 재검산으로 정정: 옛 툴팁이 7.60을 잘못 적어 놓았었다.")]
        public float leanDegrees;

        [Tooltip("몸통 상하 오프셋 — 신장 H 배수(월드 유닛이 아니다). 「내려앉기」용.\n" +
                 "설계 대역: |0.017| ~ |0.032| H. 하한 0.017은 배율 0.75에서 1.0pt(그 아래는 안 보인다), " +
                 "상한 0.032는 머리 시각반경의 1/3(그 위는 「몸이 뛴다」로 읽힌다).")]
        public float bodyOffsetY;

        [Tooltip("프롭의 몇 번째 미리 구운 변형인가(0 = 변형 없음). ★ 2026-09-08 game-architect 정정 " +
                 "— 이전 문구는 「프롭 렌더러가 enabled 토글만 한다」고 현재형으로 적어 거짓 주석이었다. " +
                 "실제로는 CostumePropRenderer가 이 필드를 전혀 읽지 않는다(값을 선언해도 프레임 " +
                 "전환이 화면에 안 나타난다 — 광부 K2의 타격 섬광이 그 사례). v3 스키마 승격 여부는 " +
                 "출시 이후 폴리싱 라운드로 미뤄졌다(docs/GAME_ARCHITECTURE_REVIEW.md §18-1). " +
                 "되살릴 조건: 이 필드를 쓰는 코스튬이 2종 이상이고 그중 하나라도 프레임 수가 2를 " +
                 "넘을 때. TestClaimExpiryAuditTests.cs의 Ignore 등재가 승격 시점을 자동 감지한다 " +
                 "— 그 역방향 장치는 건드리지 마라.")]
        public byte propFrame;
    }

    /// <summary>
    /// 코스튬 하나의 <b>전용 모션 표</b>(LFVS = 저프레임 벡터 스텝).
    /// 키포즈를 <b>벽시계 기준으로 즉시 전환</b>하고 그 사이를 <b>보간하지 않는다</b> —
    /// 그 「보간 없음」이 LFVS의 정의이자 절감의 전부다.
    ///
    /// ============================================================================
    /// ★ 스텝레이트가 3과 5뿐인 이유 — 취향이 아니라 약수다
    /// ============================================================================
    /// 절감 등급별 제출률은 Active/Calm <b>30/초</b>, Still/Away <b>15/초</b>다.
    /// 스텝 하나가 <b>모든 등급에서 정수 개의 제출</b>을 받아야 박자가 안 흔들리므로
    /// 스텝레이트는 <b>15의 약수</b>여야 한다: <c>{1, 3, 5, 15}</c>.
    /// 1은 슬라이드쇼고 15는 Still과 같아 절감이 0이다 ⇒ <b>3과 5뿐이다</b>.
    ///
    /// <para><b>4를 쓰지 마라.</b> 15 ÷ 4 = 3.75라 스텝마다 제출이 3장·4장으로 갈리고,
    /// 그 지연이 0·3·2·1로 <b>순환</b>한다. 그 불균일은 「평균 fps」로는 안 보이고 <b>눈에는 보인다</b>
    /// — 「저프레임 연출」과 「끊김」을 가르는 유일한 선이다.
    /// <c>design-motion</c>과 <c>game-architect</c>가 <b>서로 못 본 채 같은 결론</b>에 도달했다(교차검증 1건).</para>
    ///
    /// <para>그래서 <see cref="stepsPerSecond"/>는 <see cref="IsLegalStepRate"/>로 잠근다 —
    /// 불법 값이면 이 표를 <b>싣지 않는다</b>. 반쯤 실어서 끊긴 그림을 내보내지 않는다.</para>
    ///
    /// ============================================================================
    /// 표에 <b>없는</b> 것
    /// ============================================================================
    /// 듀티 사이클·소구간 배분·주기는 <see cref="CostumeFocusRhythm"/>의 <b>코드 상수</b>다.
    /// 인스펙터에 열면 그 순간 위 약수 조건과 「정수 루프」 조건이 조용히 깨진다 —
    /// <b>바꿀 수 있게 만들면 안 되는 값</b>이라 자료가 아니라 코드에 둔다.
    /// </summary>
    [CreateAssetMenu(fileName = "CostumeKeyposeTable", menuName = "StickMate/Costume Keypose Table", order = 4)]
    public sealed class CostumeKeyposeTableSO : ScriptableObject
    {
        /// <summary>키포즈 개수의 하한/상한. 2장 미만은 「모션」이 아니고, 8장을 넘으면 도달 원·규칙 B
        /// 검증 대상이 코스튬당 급증한다(설계가 3~4장으로 잡은 이유).</summary>
        public const int MinKeyposeCount = 2;
        public const int MaxKeyposeCount = 8;

        [Header("박자 — ★ 3 또는 5만 합법이다(15의 약수). 4는 눈에 보이게 끊긴다")]
        [Tooltip("이 코스튬의 «쉬는» 스텝레이트(초당 키포즈 수). 몰입기 진입/이완 소구간이 이 값으로 돌고, " +
                 "가운데 절정 소구간은 언제나 빠른 합법값(5)으로 돈다 — 그 대비가 사용자 요구 " +
                 "「세션 내 3단계 실시간 미세변화」의 실체다.\n" +
                 "★ 3 또는 5가 아니면 이 표는 실리지 않는다(반쯤 실어 끊긴 그림을 내보내지 않는다).")]
        public int stepsPerSecond = 3;

        [Header("키포즈 — 0번이 「정지 키포즈」다(정지 구간에 이 자세를 유지한다)")]
        [Tooltip("순환 재생된다. 2~8장.")]
        public CostumeKeypose[] keyposes;

        [Header("단계별 창(선택) — 비면 전 단계가 같은 표를 쓴다")]
        [Tooltip("단계 s가 쓰는 구간의 시작 인덱스. 구간의 끝은 다음 항목의 시작(마지막은 배열 끝)이다. " +
                 "비어 있으면 전 단계가 keyposes 전체를 쓴다 — 그 자체가 정상 상태다" +
                 "(진화는 주로 프롭 조형으로 표현된다).")]
        public int[] stageKeyposeStart;

        /// <summary>15의 약수 중 실제로 쓸 수 있는 둘. <b>이 판정은 여기 한 곳뿐이다.</b></summary>
        public static bool IsLegalStepRate(int stepsPerSecond)
            => stepsPerSecond == CostumeFocusRhythm.RestingStepsPerSecond
            || stepsPerSecond == CostumeFocusRhythm.PeakStepsPerSecond;

        /// <summary>
        /// 이 표를 실을 수 있는가. <b>결함은 고치지 않고 신고한다</b> —
        /// 조용히 기본값으로 때우면 작성자가 영영 모른다(<c>CostumeCatalog</c>와 같은 방침).
        /// </summary>
        public bool IsUsable(out string error)
        {
            error = null;
            if (!IsLegalStepRate(stepsPerSecond))
            {
                error = $"stepsPerSecond={stepsPerSecond}는 15의 약수가 아닙니다 — " +
                    $"{CostumeFocusRhythm.RestingStepsPerSecond} 또는 {CostumeFocusRhythm.PeakStepsPerSecond}만 " +
                    "쓸 수 있습니다(그 밖의 값은 제출 프레임과의 어긋남이 순환해 「끊김」으로 보입니다).";
                return false;
            }
            int count = keyposes != null ? keyposes.Length : 0;
            if (count < MinKeyposeCount || count > MaxKeyposeCount)
            {
                error = $"키포즈가 {count}장입니다 — {MinKeyposeCount}~{MaxKeyposeCount}장이어야 합니다.";
                return false;
            }
            if (stageKeyposeStart != null)
            {
                for (int i = 0; i < stageKeyposeStart.Length; i++)
                {
                    if (stageKeyposeStart[i] < 0 || stageKeyposeStart[i] >= count)
                    {
                        error = $"stageKeyposeStart[{i}]={stageKeyposeStart[i]}가 키포즈 범위(0~{count - 1}) 밖입니다.";
                        return false;
                    }
                    if (i > 0 && stageKeyposeStart[i] <= stageKeyposeStart[i - 1])
                    {
                        error = $"stageKeyposeStart가 오름차순이 아닙니다({i - 1}번 {stageKeyposeStart[i - 1]} " +
                            $"→ {i}번 {stageKeyposeStart[i]}). 구간의 끝을 다음 항목이 정하므로 " +
                            "오름차순이 아니면 빈 구간이 생깁니다.";
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// 단계 <paramref name="stage"/>가 쓰는 키포즈 구간. <see cref="stageKeyposeStart"/>가 비어 있거나
        /// 그 단계를 안 적었으면 <b>표 전체</b>다.
        /// </summary>
        public void StageWindow(int stage, out int start, out int count)
        {
            int total = keyposes != null ? keyposes.Length : 0;
            start = 0;
            count = total;
            if (total == 0 || stageKeyposeStart == null || stage < 0 || stage >= stageKeyposeStart.Length) return;

            start = Mathf.Clamp(stageKeyposeStart[stage], 0, total - 1);
            int end = stage + 1 < stageKeyposeStart.Length
                ? Mathf.Clamp(stageKeyposeStart[stage + 1], start + 1, total)
                : total;
            count = Mathf.Max(1, end - start);
        }
    }

    /// <summary>
    /// ★ 몰입기 <b>내부 3소구간</b>과 듀티 사이클 — 순수 함수만 있다(<c>MonoBehaviour</c> 없이 EditMode에서 잰다).
    /// 정본은 <c>docs/UX_MOTION_COSTUME_FOCUS.md</c> 3-3절.
    ///
    /// ============================================================================
    /// 왜 「몰입기 안」을 다시 3등분하는가
    /// ============================================================================
    /// 사용자 요구는 <i>「25분 세션 내 3단계 실시간 미세변화」</i>인데, 채택안에서 <b>코스튬은
    /// 몰입기에만 보인다</b>. 적응기·한계에 스텝레이트를 넣어 봐야 <b>그릴 대상이 없다</b>.
    /// 「보이는 구간에 변화를 넣는다」가 그 요구를 만족시키는 유일한 해석이다.
    ///
    /// <para>읽히는 이야기: <b>천천히 시작 → 가장 바쁘게 몰입 → 힘이 빠진다.</b>
    /// 스텝레이트와 듀티 <b>두 축이 같은 방향</b>을 가리켜 신호가 서로를 보강한다.</para>
    ///
    /// ============================================================================
    /// 루프는 <b>언제나 정수 개</b>다
    /// ============================================================================
    /// 반쯤 진행된 동작이 정지 구간에 수 초간 얼어 있으면 <b>결함으로 읽힌다</b>.
    /// 그래서 작업 구간 W는 «루프 길이 × 정수»이고, 루프 수는 상수가 아니라
    /// <see cref="LoopCount"/>로 <b>유도</b>한다 — 키 수가 다른 코스튬(오피스 3키)에 4키용 상수를 쓰면
    /// W가 정수 루프가 아니게 되어 동작이 중간에 얼어붙는다.
    /// </summary>
    public static class CostumeFocusRhythm
    {
        /// <summary>몰입기를 몇 등분하는가(진입 / 절정 / 이완).</summary>
        public const int SubPhaseCount = 3;

        /// <summary>«쉬는» 스텝레이트. 표가 고르는 기본값이고 진입·이완 소구간이 쓴다.</summary>
        public const int RestingStepsPerSecond = 3;

        /// <summary>«절정» 스텝레이트. 가운데 소구간이 언제나 이 값으로 돈다.</summary>
        public const int PeakStepsPerSecond = 5;

        /// <summary>듀티 주기 하한(초) — 한 소구간에 주기가 <b>3번</b>은 들어가야 「리듬」으로 읽힌다.</summary>
        public const float MinPeriodSeconds = 4f;

        /// <summary>듀티 주기 상한(초) — 그 위로는 정지 구간이 길어져 「멈춘 캐릭터」로 되돌아간다.
        /// <c>StickConfig.costumeFocusDutyCycleMaxSeconds</c>가 이 값을 덮을 수 있다.</summary>
        public const float MaxPeriodSeconds = 8f;

        /// <summary>소구간별 목표 듀티(작업 구간이 주기에서 차지하는 비율).</summary>
        public const float EnterDuty = 1f / 3f;
        public const float PeakDuty = 1f / 2f;
        public const float RelaxDuty = 1f / 6f;

        /// <summary>이완 소구간의 정지 자세에 더하는 앞쪽 기울임(도) — <b>힘이 빠진다</b>.
        /// ★ 새 키포즈를 만들지 않는다: 표가 늘면 도달 원·규칙 B 검증 대상이 코스튬당 25% 늘어난다.
        /// ★★ 2026-09-08 — design-motion 재검산(코스튬 3종 키포즈)이 옛 값 1.5°가 어깨를 0.57pt만
        /// 움직여 설계 자신의 가시 하한(1.0pt)의 57%뿐임을 계산으로 확인했다. 2.6~3.0° 구간이면
        /// 하한을 넘는다는 권고를 받아 중간값 2.8°로 정한다(어깨 이동량이 하한을 넉넉히 넘으면서
        /// 기울임 상한(MaxBodyLeanDegrees=30, CostumeKeypose.leanDegrees 문서 참고)과는 여전히
        /// 거리가 먼 값). 이완 구간의 실제 주 신호는 여전히 정지 6.67초이고, 이 각도는 보조 신호다.</summary>
        public const float RelaxLeanBiasDegrees = 2.8f;

        /// <summary>몰입기 안의 소구간 번호(0 진입 / 1 절정 / 2 이완).</summary>
        public static int SubPhaseOf(float immersionSeconds, float immersionElapsedSeconds)
        {
            if (!(immersionSeconds > 0f)) return 0;
            float sub = immersionSeconds / SubPhaseCount;
            if (!(sub > 0f)) return 0;
            return Mathf.Clamp(Mathf.FloorToInt(immersionElapsedSeconds / sub), 0, SubPhaseCount - 1);
        }

        /// <summary>그 소구간의 스텝레이트. 가운데만 <see cref="PeakStepsPerSecond"/>다.</summary>
        public static int StepsPerSecondOf(int subPhase, int restingStepsPerSecond)
            => subPhase == 1 ? PeakStepsPerSecond : restingStepsPerSecond;

        public static float DutyOf(int subPhase)
            => subPhase == 0 ? EnterDuty : subPhase == 1 ? PeakDuty : RelaxDuty;

        /// <summary>듀티 주기(초). 소구간에 주기가 3번 들어가는 것을 목표로 하고 4~8초로 문다.</summary>
        public static float PeriodSeconds(float immersionSeconds, float maxPeriodSeconds)
        {
            float max = maxPeriodSeconds > MinPeriodSeconds ? maxPeriodSeconds : MaxPeriodSeconds;
            if (!(immersionSeconds > 0f)) return MinPeriodSeconds;
            return Mathf.Clamp(immersionSeconds / SubPhaseCount / SubPhaseCount, MinPeriodSeconds, max);
        }

        /// <summary>
        /// 작업 구간에 넣을 <b>정수</b> 루프 수(최소 1).
        ///
        /// <para>★ <b>반올림은 「0.5에서 올림」이다</b> — <c>Mathf.RoundToInt</c>(짝수 반올림)를 쓰면 안 된다.
        /// 최단 세션(60초) 절정 소구간에서 값이 정확히 2.5가 되는데, 짝수 반올림은 2(듀티 40%)를,
        /// 설계 문서의 검산표는 <b>3(듀티 60%)</b>을 적고 있다. <b>표가 골든이다.</b></para>
        /// </summary>
        public static int LoopCount(float periodSeconds, float loopSeconds, float duty)
        {
            if (!(loopSeconds > 0f)) return 1;
            return Mathf.Max(1, Mathf.FloorToInt(duty * periodSeconds / loopSeconds + 0.5f));
        }

        /// <summary>작업 구간 W(초) = 루프 길이 × 정수 루프 수. 주기를 넘지 않는다.</summary>
        public static float WorkSeconds(float periodSeconds, float loopSeconds, float duty)
        {
            if (!(loopSeconds > 0f)) return 0f;
            float w = LoopCount(periodSeconds, loopSeconds, duty) * loopSeconds;
            return Mathf.Min(w, periodSeconds);
        }
    }
}
