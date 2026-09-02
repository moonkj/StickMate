using UnityEngine;

namespace StickMate.Dialogue
{
    /// <summary>
    /// 대사의 **종류 축**(docs/UX_FLOW.md 5절 규칙 4-a, 2026-09-01 신설 / docs/MOTION_SPEC.md 3-3).
    ///
    /// 왜 이 축이 필요한가: 구판은 "최소 노출 시간"이라는 가독성 규칙이 "상태가 끝나면 문장이 거짓이
    /// 된다"는 진실 규칙을 일방적으로 이겼다. 그래서 걷기 시작한 캐릭터 위에 "잠깐 쉬는 중"이 계속
    /// 떠 있었다(실측 34건 중 9건). 두 규칙을 충돌 없이 공존시키는 축은 이것 하나뿐이다 —
    /// <see cref="Narrative"/>는 상태가 끝나는 순간 **문장 자체가 거짓이 되고**,
    /// <see cref="Reaction"/>은 점(point) 사건에 대한 서술이라 상태가 끝나도 **여전히 참**이다
    /// ("방금 맞았다"는 랙돌이 끝나도 참).
    ///
    /// ★ 종류는 텍스트 문자열에서 역추론하지 않는다. 매핑 함수가 <see cref="DialogueLine"/>으로
    ///   (텍스트, 종류)를 **함께** 돌려준다 — 같은 상태가 상황에 따라 두 종류를 갈라 쓸 수 있어야
    ///   하기 때문이다.
    ///   <para>★ 2026-09-02 — 이 자리의 예시는 "BattleMinigame: 개시 = 서술 / 판정 = 반응"이었고,
    ///   격파 놀이 삭제로 <b>지금 두 종류를 실제로 갈라 쓰는 상태는 하나도 없다</b>(전수 확인:
    ///   서술만 = ParkourClimb/LedgeHang/AmbientChatter, 반응만 = Ragdoll/Attack/WindowTheft/
    ///   Runaway/TimedSpectacle). 설계 능력은 그대로 두되 <b>없는 예시를 있는 척 적지 않는다</b> —
    ///   다음에 그런 상태가 생기면 여기에 실명으로 적어라.</para>
    /// </summary>
    public enum DialogueKind
    {
        /// <summary>진행 서술 — "나는 **지금** X하고 있다". 상태가 끝나면 가독예산을 무시하고 즉시 컷.</summary>
        Narrative,

        /// <summary>순간 반응 — "**방금** X가 일어났다". 상태가 끝나도 가독예산을 채우고 나서 사라진다.</summary>
        Reaction,
    }

    /// <summary>
    /// 텍스트 매핑 함수의 반환형 — (텍스트, 종류). UX_FLOW.md 31-2 표의 "이 함수의 반환형은 string이
    /// 아니라 (텍스트, DialogueKind)다"를 타입으로 고정한 것이다.
    ///
    /// readonly struct인 이유: 대사 매핑은 상태 전이마다 호출되는 경로라 할당을 만들지 않는다
    /// (CLAUDE.md "Update()에서 매 프레임 할당 금지"의 같은 정신 — 전이는 매 프레임은 아니지만
    /// 24시간 상주 앱에서 하루 수만 번 돈다).
    /// </summary>
    public readonly struct DialogueLine
    {
        public readonly string Text;
        public readonly DialogueKind Kind;

        public DialogueLine(string text, DialogueKind kind)
        {
            Text = text;
            Kind = kind;
        }

        /// <summary>진행 서술("지금 X하고 있다").</summary>
        public static DialogueLine Say(string text) => new DialogueLine(text, DialogueKind.Narrative);

        /// <summary>순간 반응("방금 X가 일어났다").</summary>
        public static DialogueLine React(string text) => new DialogueLine(text, DialogueKind.Reaction);
    }

    /// <summary>
    /// 가독예산과 **발화 자격 게이트**(docs/UX_FLOW.md 5절 규칙 4-b / 규칙 8).
    ///
    /// ============================================================================
    /// 왜 고정 하한이 아니라 글자수의 함수인가
    /// ============================================================================
    /// 구판 하한 0.7초는 글자수를 전혀 보지 않았다 — "음..."(4자)과 "창 위는 미끄러워"(9자)가 정확히
    /// 같은 시간 떠 있었다. 가독성 하한이라면서 실제로는 가독성을 보장하지 못한 값이다.
    ///
    /// ============================================================================
    /// 왜 "제거 시점"이 아니라 "발화 시점"에 막는가
    /// ============================================================================
    /// 규칙 4-c ③(Narrative 즉시 컷)만 넣으면 "0.08초 번쩍이고 사라지는 글자"라는 **새 노이즈**가
    /// 생긴다. 그래서 말할 시간이 없으면 애초에 말하지 않는다 — <b>침묵은 거짓말이 아니다.</b>
    ///
    /// 상수는 전부 **초(무차원 축 ④)** 라 캐릭터 배율/플랫폼과 무관하다(UX_FLOW.md 31-4-1 C1).
    /// </summary>
    public static class DialogueBudget
    {
        /// <summary>글자수와 무관한 기본 인지 시간(초) — 눈이 글자 블록을 찾아가는 데 드는 몫.</summary>
        private const float BaseSeconds = 0.28f;

        /// <summary>한 글자당 추가 가독 시간(초).</summary>
        private const float PerGlyphSeconds = 0.075f;

        /// <summary>아주 짧은 감탄사에도 보장되는 하한(초).</summary>
        public const float MinSeconds = 0.62f;

        /// <summary>아무리 긴 대사여도 넘지 않는 <b>가독예산</b>의 상한(초). 화면 노출 상한은 이 값이
        /// 아니라 <see cref="MaxVisibleSecondsFor"/>가 정한다.</summary>
        public const float MaxSeconds = 2.20f;

        /// <summary>
        /// 이 텍스트를 읽는 데 필요한 시간(초) — <c>clamp(0.28 + 글자수 × 0.075, 0.62, 2.20)</c>.
        /// 순수 함수다(시간/난수/전역 상태를 읽지 않는다).
        /// </summary>
        public static float ReadingSeconds(string text)
        {
            int glyphs = string.IsNullOrEmpty(text) ? 0 : text.Length;
            return Mathf.Clamp(BaseSeconds + glyphs * PerGlyphSeconds, MinSeconds, MaxSeconds);
        }

        /// <summary>
        /// ★★ 2026-09-01 — 이 텍스트가 화면에 떠 있어도 되는 <b>상한</b>(초). 규칙 4-b 개정의
        /// 나머지 절반이다.
        ///
        /// ============================================================================
        /// 왜 필요했나 — 하한만 글자수 함수로 만들자 결과가 정확히 뒤집혔다
        /// ============================================================================
        /// 하한은 글자수 비례가 됐는데 상한은 글자수 무관 고정 4초로 남아, 실측이 이렇게 나왔다:
        /// <list type="table">
        ///   <item><term>하암...(5자)</term><description>가독예산 0.66초 → <b>실제 노출 4.14초</b></description></item>
        ///   <item><term>심심하다(4자)</term><description>0.62초 → <b>4.10초</b></description></item>
        ///   <item><term>오늘 뭐 하지(7자)</term><description>0.81초 → <b>4.12초</b></description></item>
        ///   <item><term>창 위는 미끄러워(9자)</term><description>0.96초 → <b>1.45초</b></description></item>
        /// </list>
        /// <b>가장 짧은 대사가 가장 오래, 가장 긴 대사가 가장 짧게 떠 있었다.</b> 개편의 취지를
        /// 화면 결과가 그대로 뒤집은 것이다. 고정 상한은 상태가 오래 지속될수록 무조건 이기므로,
        /// 상태 길이가 글자수와 무관한 이상 <b>역전이 구조적으로 보장</b>돼 있었다.
        ///
        /// ============================================================================
        /// 왜 이 식인가 — <b>배수 k를 고르지 않았다</b>
        /// ============================================================================
        /// "가독예산 × k"로 두면 k가 또 하나의 근거 없는 숫자가 된다. 대신 <b>화면에 떠 있는 시간이
        /// 실제로 무엇으로 채워지는지</b>를 그대로 적는다:
        /// <code>상한 = 등장(팝인) + 가독예산 × 2 + 소멸(페이드아웃)</code>
        /// <list type="bullet">
        ///   <item><b>등장/소멸</b>은 이 파일의 <see cref="DialogueTiming"/>이 이미 소유한 값이다.
        ///         읽을 수 없는 구간이므로 읽기 예산과 별도로 얹는다.</item>
        ///   <item><b>×2 = "두 번 읽을 수 있다"</b>. 이 앱의 주인공은 말풍선이 아니라 캐릭터다 —
        ///         유저는 글자를 읽고, 캐릭터를 보고, 한 번 더 돌아올 수 있어야 한다. 돌아오면
        ///         <see cref="BaseSeconds"/>("눈이 글자 블록을 찾아가는 몫")를 <b>다시 지불</b>하므로
        ///         재독 비용은 정확히 <see cref="ReadingSeconds"/> 한 번분이다. 세 번째 읽기는
        ///         예산에 넣지 않는다 — 4~9자짜리 같은 문장을 세 번 읽을 이유가 없다.</item>
        /// </list>
        /// 결과값(기본 상수): 심심하다 1.54초 / 하암... 1.61초 / 오늘 뭐 하지 1.91초 /
        /// 창 위는… 2.21초. <b>글자수에 대해 단조 비감소</b>라 역전이 정의상 불가능하다.
        ///
        /// <para>이 값은 항상 <see cref="ReadingSeconds"/>보다 크므로 하한(규칙 4-b)과 절대 싸우지
        /// 않는다. 사용자 설정 <c>dialogueMaxVisibleSeconds</c>는 <b>또 하나의 상한</b>이며, 소비자는
        /// 둘 중 짧은 쪽을 쓴다(상한 둘의 교집합 — 더 일찍 사라지는 방향이라 계약이 막는 실패
        /// 모드의 반대편이다).</para>
        /// </summary>
        /// <param name="popInSeconds">등장 연출 길이. 상수 이중 정의를 막기 위해 인자로 받는다
        /// (<see cref="DialogueTiming.PopInSeconds"/>).</param>
        /// <param name="fadeOutSeconds">소멸 연출 길이(<see cref="DialogueTiming.FadeOutSeconds"/>).</param>
        public static float MaxVisibleSecondsFor(string text, float popInSeconds, float fadeOutSeconds)
            => MaxVisibleSecondsFor(text, popInSeconds, fadeOutSeconds, MinVisibleScale);

        /// <summary>
        /// ★★ 2026-09-02 — 위 식에 <b>사용자 배율</b>을 태운 형태(docs/UX_FLOW.md 42-5 확정안 B).
        /// <c>상한 = 팝인 + 2 × m × 가독예산 + 페이드아웃</c>.
        ///
        /// <para><b>배율이 곱해지는 곳은 여기(화면 노출)와 <see cref="MinVisibleSecondsFor"/>뿐이다.</b>
        /// <see cref="RequiredDwellSeconds"/>/<see cref="IsEligible"/>(발화 자격 게이트)에는 절대
        /// 곱하지 않는다 — 이유는 그 함수들의 문서에 있다.</para>
        ///
        /// <para><b>m = <see cref="MinVisibleScale"/>(100%)에서 이 식은 배율 없는 식과 한 톨도
        /// 다르지 않다.</b> 그래서 이 오버로드는 2026-09-01에 착륙한 거동을 되돌릴 수 없다.</para>
        /// </summary>
        /// <param name="visibleScale">사용자가 고른 노출 배율. <see cref="ClampVisibleScale"/>로 잘린다.</param>
        public static float MaxVisibleSecondsFor(string text, float popInSeconds, float fadeOutSeconds,
            float visibleScale)
            => Mathf.Max(0f, popInSeconds)
             + ReadsBeforeStale * ClampVisibleScale(visibleScale) * ReadingSeconds(text)
             + Mathf.Max(0f, fadeOutSeconds);

        /// <summary>
        /// 이 텍스트의 <b>화면 최소 노출</b>(초) = <c>m × 가독예산</c>. 반응(Reaction) 대사가 상태보다
        /// 오래 살아남을 때 채워야 하는 바닥이다.
        /// <para>m = <see cref="MinVisibleScale"/>이면 <see cref="ReadingSeconds"/>와 정확히 같다.</para>
        /// </summary>
        public static float MinVisibleSecondsFor(string text, float visibleScale)
            => ClampVisibleScale(visibleScale) * ReadingSeconds(text);

        /// <summary>
        /// ★ 노출 배율의 <b>하한</b>(= 100%). <b>고른 값이 아니라 이미 비준된 규칙이 강제한 값이다</b> —
        /// UX_FLOW.md 5절 규칙 6의 개정 기준 "완전 불투명 구간 / 총 노출 ≥ 77%"는 배율을 태우면
        /// <c>m·R / (m·R + 팝인)</c>이 되고 이는 m에 대해 단조 증가한다. 최단 대사(R =
        /// <see cref="MinSeconds"/>)의 기본값이 정확히 77.5%로 <b>경계선 위에 서 있어서</b>,
        /// m = 0.9면 75.6%가 되어 <b>이미 승인된 규칙을 위반</b>한다.
        /// <para>즉 "짧게"라는 선택지가 없는 것은 취향이 아니라 규칙이다.</para>
        /// </summary>
        public const float MinVisibleScale = 1f;

        /// <summary>
        /// ★ 노출 배율의 <b>상한</b>(= 200%). <b>포화가 막 시작되는 문턱</b>이다 — 배율을 올리면
        /// 언젠가 상한이 상태 지속시간을 넘고, 그 지점부터 서술 대사는 규칙 4-c ③(상태 종료 시 즉시 컷)에
        /// 잡혀 더 이상 길어지지 않는다. 앰비언트 13줄 실측에서 포화는 100%/150%에 0줄, <b>200%에 2줄</b>
        /// (9자 Walk 대사, 상한 4.12초 &gt; Walk 최장 4.00초)이다.
        /// <para>더 올리면 <b>"칸을 옮겼는데 화면이 안 바뀐다"가 위쪽에서 재발</b>한다 — 42절이
        /// 고치는 병 그 자체다. 그래서 범위의 끝은 여기다.</para>
        /// </summary>
        public const float MaxVisibleScale = 2f;

        /// <summary>배율을 유효 범위로 자른다. NaN은 하한으로 떨어뜨린다(침묵보다 안전).</summary>
        public static float ClampVisibleScale(float visibleScale)
            => float.IsNaN(visibleScale)
                ? MinVisibleScale
                : Mathf.Clamp(visibleScale, MinVisibleScale, MaxVisibleScale);

        /// <summary>상한이 보장하는 읽기 횟수. 위 <see cref="MaxVisibleSecondsFor"/>의 근거 참고 —
        /// "읽고, 캐릭터를 보고, 한 번 더 읽는다".</summary>
        private const float ReadsBeforeStale = 2f;

        /// <summary>
        /// 이 텍스트가 화면에서 제 몫을 하려면 상태가 최소 얼마나 더 지속돼야 하는가(초).
        /// = 페이드인 + 가독예산. 페이드인 값은 렌더러가 소유하므로 인자로 받는다(상수 이중 정의 금지).
        ///
        /// <para>★★ <b>사용자 노출 배율(m)을 곱하지 않는다</b>(2026-09-02, UX_FLOW.md 42-5 확정안 B).
        /// 근거 셋:</para>
        /// <list type="number">
        ///   <item>규칙 8의 목적은 <b>"번쩍임 노이즈 제거"</b>이지 "이 사용자가 완독 가능한가"가 아니다.
        ///     노이즈인지 아닌지는 <b>화면의 사실</b>이지 개인 취향이 아니다.</item>
        ///   <item>곱하면 <b>"더 오래 보고 싶다"는 입력이 "덜 본다"는 출력</b>을 낳는다. 실측: m=2.0을
        ///     태우면 "구경 중이야"의 필요체류가 0.79 → 1.52초가 되어 두리번 모션 0.9초를 못 채워
        ///     <b>영구 침묵</b>하고, 9자 Walk 대사는 Walk 구간의 19%에서 침묵한다.
        ///     <b>접근성 손잡이를 끝까지 밀었더니 대사가 사라지는 화면</b>이다.</item>
        ///   <item>고정하면 <b>컨트롤 하나 = 효과 하나</b>가 된다. 어떤 배율에서도 말하는 집합은
        ///     동일하고, 바뀌는 것은 얼마나 오래 보이는가뿐이다.</item>
        /// </list>
        /// <para>이 성질은 <c>DialogueVisibleScaleContractTests</c>가 회귀로 잠근다.</para>
        /// </summary>
        public static float RequiredDwellSeconds(string text, float fadeInSeconds)
            => Mathf.Max(0f, fadeInSeconds) + ReadingSeconds(text);

        /// <summary>
        /// 발화 자격 게이트(규칙 8). <paramref name="plannedDwellSeconds"/>는 "지금 확정된 <b>상태</b>의
        /// 계획 잔여 체류 시간"이며, 지어내는 값이 아니라 이미 확정된 사실에서만 나온다
        /// (ParkourClimb = 등반 길이, LedgeHang = 잡기+매달림).
        ///
        /// ★ Idle/Walk는 <b>배회 페이즈 잔여를 그대로 쓰지 않는다</b>(2026-09-01) — 그것은 "배회
        /// 페이즈의 잔여"이지 "이 상태의 잔여"가 아니어서, 기상/착지/등반 복귀처럼 배회가 걷는
        /// 한복판인데 Idle로 들어오는 경로에서 실제 체류 1프레임을 2.8초로 답했다. 지금은
        /// <c>StickmanBlackboard.PlannedDwellRemainingSecondsFor</c>가 상태의 탈출 조건과 대조한 값을
        /// 준다.
        ///
        /// <see cref="DialogueKind.Reaction"/>은 **언제나 통과한다** — 점 사건 서술이라 상태가 끝나도
        /// 참이므로 체류 시간과 무관하다.
        ///
        /// <para>★ 사용자 노출 배율을 <b>인자로 받지 않는다</b>. 받을 자리를 만들어 두면 언젠가 누가
        /// 채운다 — 그 순간 "길게를 골랐더니 대사가 사라진다"가 된다
        /// (<see cref="RequiredDwellSeconds"/> 문서 참고).</para>
        /// </summary>
        public static bool IsEligible(in DialogueLine line, float plannedDwellSeconds, float fadeInSeconds)
        {
            if (line.Kind == DialogueKind.Reaction) return true;
            if (float.IsNaN(plannedDwellSeconds)) return true; // 계획을 알 수 없으면 막지 않는다(침묵보다 안전).
            return plannedDwellSeconds >= RequiredDwellSeconds(line.Text, fadeInSeconds);
        }

        /// <summary>
        /// ★★ 2026-09-02 — <b>교체 경로</b>의 발화 자격(규칙 8의 확장). 실기 로그에서 잡힌 결함:
        /// <code>
        ///   frame=11110 교체 — 이전 "어... 힘이 다 샜다"(반응) 노출 3.38초 → 새 "어... 힘이 다 샜다"(반응)
        ///   frame=11111 교체 — 이전 "어... 힘이 다 샜다"(반응) 노출 0.02초 → 새 "여기 좋네"(Idle, 서술)
        ///   frame=11112 즉시 컷 (Idle) "여기 좋네" — 노출 0.02초
        /// </code>
        /// <b>0.02초 번쩍임이 두 번 연속</b>이었다. 그 빌드는 규칙 8 게이트를 이미 갖고 있었고
        /// (같은 로그에 `발화 보류` 31건) <b>그런데도</b> 통과했다 — 게이트는 "상태의 계획 잔여"만 보고
        /// <b>지금 화면에 무엇이 떠 있는지</b>는 한 번도 보지 않았기 때문이다.
        ///
        /// <para><b>근본 원인</b>: 최소 노출 보호는 <i>만료</i> 경로에만 있었고 <i>교체</i> 경로에는
        /// 한 줄도 없었다. 그래서 방금 뜬 글자가 다음 프레임에 지워질 수 있었다.</para>
        ///
        /// <para><b>왜 큐잉이 아닌가</b>(규칙 5와 부딪히지 않는 이유): 새 대사를 <b>줄 세우지 않는다</b>.
        /// 발화 자격을 부정할 뿐이고, 그 결과는 <b>침묵</b>이다 — 규칙 8이 이미 쓰는 어법 그대로다.
        /// "0.08초 번쩍이고 사라지는 글자라는 새 노이즈"를 없애려던 규칙 8의 목적을, 교체 경로에서도
        /// 같은 방식으로 지키는 것뿐이다.</para>
        ///
        /// <para>★ <b>기준이 팝인인 이유</b>: 이 구간은 글자가 <b>아직 다 커지지도 않은</b> 시간이다
        /// (<see cref="DialogueTiming.PopInSeconds"/>는 스케일 바운스 길이). 그 안에 지워지면 사용자가
        /// 본 것은 문장이 아니라 <b>깜빡임</b>이다. 가독예산을 기준으로 삼지 않는 것은 그러면 교체가
        /// 사실상 큐잉이 되어 규칙 5와 정면으로 부딪히기 때문이다.</para>
        ///
        /// <para>★ <b>사용자 노출 배율(m)을 곱하지 않는다.</b> 여기는 "이 사용자가 완독 가능한가"가
        /// 아니라 <b>"화면에 글자가 나타나기는 했는가"</b>를 재는 자리다 — 화면의 사실이지 취향이 아니다
        /// (<see cref="RequiredDwellSeconds"/>와 완전히 같은 이유). 그래서 배율을 어느 칸에 두든 이
        /// 보호의 길이는 동일하다.</para>
        /// </summary>
        /// <param name="activeVisibleSeconds">지금 떠 있는 대사가 화면에 있었던 시간(초).</param>
        /// <param name="popInSeconds"><see cref="DialogueTiming.PopInSeconds"/>. 상수 이중 정의를 막기
        /// 위해 인자로 받는다.</param>
        /// <param name="replacesItself">새 대사가 <b>지금 떠 있는 것과 같은 글자</b>인가. 그러면 노출
        /// 시계와 팝인이 리셋돼 화면상 같은 글자가 다시 튀어오른다 — 사용자에게는 렌더 글리치로 읽힌다
        /// (위 frame=11110 건).</param>
        /// <param name="visibleWillBeCutAnyway">
        /// ★★ 2026-09-02 <b>보호 범위 정정</b>(리더 실측: <i>"막았으면 이전 것이 계속 떠 있어야 하는데
        /// 그것도 컷된다 — 그러면 막은 의미가 없다"</i>, 2회 관측).
        ///
        /// <para><b>관측</b>: 팝인 가드가 새 대사를 버렸는데(노출 0.17초 &lt; 팝인 0.18초)
        /// <b>이전 대사도 같은 프레임 뒤에 상태 종료로 컷됐다.</b> 결과는 <b>순손해</b>다 —
        /// 사용자는 0.17초 번쩍임을 <b>그대로 보고</b>, 그 대가로 새 대사까지 잃는다.
        /// 가드는 그 번쩍임을 애초에 막은 적이 없다(이전 대사의 수명은 가드가 정하지 않는다).</para>
        ///
        /// <para><b>두 갈래 중 무엇이 맞는가</b> — 리더가 판단을 요구한 지점이다.
        /// <list type="bullet">
        ///   <item>(가) 막았으면 <b>이전 대사의 만료도 함께 미룬다</b>(팝인이 끝날 때까지).
        ///         → <b>기각한다.</b> 서술(Narrative)은 자기 상태가 끝나는 순간 <b>문장 자체가 거짓</b>이
        ///         된다(규칙 4-c ③). 팝인 애니메이션을 지키려고 거짓 문장을 0.18초 더 띄우는 것은
        ///         <b>불변 원칙 1(행동-텍스트 싱크)</b>을 렌더 글리치 완화 규칙에 양보하는 것이다.
        ///         두 규칙의 등급이 다르다.</item>
        ///   <item>(나) 상태 종료 컷이 정당하다 → <b>그러면 그 경우에는 막는 것 자체가 무의미하다.</b>
        ///         → <b>채택한다.</b> 다만 가드를 없애지는 않는다 — 가드는 <b>이전 대사가 실제로
        ///         살아남을 때</b>만 값이 있고, 그때는 여전히 유효하다(frame=11110의 반응 대사 건).</item>
        /// </list></para>
        ///
        /// <para><b>이 사실을 렌더러가 어떻게 아는가</b>: 대사는 상태의 <c>Enter()</c>에서만 만들어지므로,
        /// <b>새 대사의 상태 ID가 지금 떠 있는 대사의 상태 ID와 다르다</b>는 것이 곧 "그 서술의 상태가
        /// 끝났다"는 뜻이다. 추정이 아니라 대사 생성 경로의 성질이다. <b>반응(Reaction)에는 적용하지
        /// 않는다</b> — 반응은 상태가 끝나도 참이라 가독예산을 채우고 나가므로(규칙 4-c ④)
        /// <b>죽지 않는다</b>.</para>
        /// </param>
        public static bool CanReplaceVisible(float activeVisibleSeconds, float popInSeconds, bool replacesItself,
            bool visibleWillBeCutAnyway = false)
        {
            if (replacesItself) return false;   // 같은 글자 재점화는 어떤 경우에도 렌더 글리치다.
            // ★ 이미 죽은 서술을 지키느라 새 대사를 버리지 않는다 — 지켜지지 않는 보호는 손해다.
            if (visibleWillBeCutAnyway) return true;
            if (float.IsNaN(activeVisibleSeconds)) return true; // 알 수 없으면 막지 않는다(침묵보다 안전).
            return activeVisibleSeconds >= Mathf.Max(0f, popInSeconds);
        }

        /// <summary>
        /// 지금 떠 있는 대사가 <b>새 대사가 도착한 그 사실만으로 이미 죽었는가</b>(순수 함수).
        /// <see cref="CanReplaceVisible"/>의 <c>visibleWillBeCutAnyway</c> 인자를 만드는 유일한 곳이다.
        ///
        /// <para>근거는 <b>대사 생성 경로의 성질</b>이다: 대사는 상태의 <c>Enter()</c>에서만 만들어지므로,
        /// 새 대사의 상태 ID가 다르면 <b>다른 상태가 들어왔다</b> = 떠 있던 대사의 상태는 끝났다.
        /// 그리고 <b>서술</b>은 자기 상태가 끝나는 순간 문장이 거짓이 되어 그 프레임에 컷된다(규칙 4-c ③).
        /// <b>반응</b>은 상태가 끝나도 참이라 가독예산을 채우고 나가므로(규칙 4-c ④) 죽지 않는다 —
        /// 그래서 종류 축이 조건에 반드시 들어간다.</para>
        ///
        /// <para>상태 ID를 <c>int</c>로 받는 이유: 이 어셈블리의 다른 순수 판정들과 같이
        /// <b>상태 열거형에 의존하지 않기 위해서</b>다(대사 레이어가 상태 목록을 알 필요가 없다).
        /// 호출부가 <c>(int)stateId</c>로 넘긴다.</para>
        /// </summary>
        public static bool VisibleIsDoomedByIncoming(DialogueKind visibleKind, int visibleStateId, int incomingStateId)
            => visibleKind == DialogueKind.Narrative && visibleStateId != incomingStateId;
    }

    /// <summary>
    /// 말풍선 표시 타이밍 상수(docs/UX_FLOW.md 5절 규칙 6 표, 2026-09-01 개정).
    ///
    /// ★ 왜 렌더러 안이 아니라 여기인가: 발화 자격 게이트(규칙 8)의 "필요체류 = 페이드인 + 가독예산"이
    ///   이 값을 읽어야 하는데, 게이트는 <see cref="DialogueIntent"/>(생성 경로)에 있고 페이드인은
    ///   렌더러(표시 경로)에 있었다. 양쪽이 각자 상수를 들면 그 순간 이중 정의가 되고, 한쪽만 바뀌면
    ///   "말할 시간이 있다고 판정해 놓고 실제로는 잘리는" 조용한 불일치가 생긴다. 단일 소스로 둔다.
    /// </summary>
    public static class DialogueTiming
    {
        /// <summary>알파 페이드인(초). 규칙 6 개정: **60ms**. 만화 레터링은 페이드로 등장하지 않는다 —
        /// 등장감은 아래 <see cref="PopInSeconds"/>(스케일 바운스)가 만든다.</summary>
        public const float FadeInSeconds = 0.06f;

        /// <summary>소멸 페이드아웃(초). 규칙 6 "소멸 100~150ms" 범위 안 — 개정에서 유지된 유일한 값.</summary>
        public const float FadeOutSeconds = 0.12f;

        /// <summary>팝인(툭 튀어나오는 등장) 스케일 바운스 길이(초). 규칙 6 개정: **180ms, 알파와 분리된
        /// 독립 상수**. 예전에는 <c>PopInSeconds = FadeInSeconds</c>로 묶여 있어 알파를 줄이면 바운스도
        /// 함께 짧아졌다 — 그 커플링이 둘 다 어중간하게 만든 원인이다(교차 레이어 로그 L3).</summary>
        public const float PopInSeconds = 0.18f;
    }
}
