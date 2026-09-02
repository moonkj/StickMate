using StickMate.Core;

namespace StickMate.Dialogue
{
    /// <summary>
    /// 붙잡힘(<see cref="StickmanStateId.Dragged"/>) 반응 대사 풀 — 2026-09-02 사용자 요청
    /// <b>"마우스로 잡고 있을때 놔줘 놔줘 라는 멘트 같은거 넣는것도 좋아보임"</b>.
    /// 설계 원본: <c>design/narrative/2026-09-02_R7_붙잡힘대사_회전침묵_재확인.md</c> §2.
    ///
    /// ============================================================================
    /// 왜 전부 <see cref="DialogueKind.Reaction"/>인가 — 이 선택이 이 파일의 전부다
    /// ============================================================================
    /// <c>StickmanBlackboard.PlannedDwellRemainingSecondsFor(Dragged)</c>는 <b>정의상 NaN</b>이다
    /// (붙잡힌 시간은 사용자가 정하므로 우리가 알 수 없다). 그런데 <c>DialogueBudget.IsEligible</c>은
    /// NaN을 <b>통과시킨다</b>(침묵보다 안전한 쪽). 즉 <b>발화 자격 게이트(규칙 8)가 이 상태에서는
    /// 아무것도 막지 못한다.</b>
    /// <list type="bullet">
    ///   <item><see cref="DialogueKind.Narrative"/>였다면: 0.1초짜리 클릭에서 상태 종료 즉시 컷(규칙 4-c ③)
    ///     → <b>0.1초 번쩍임</b>. 규칙 8이 없애기로 한 그 결함이 게이트를 우회해 그대로 나온다.</item>
    ///   <item><see cref="DialogueKind.Reaction"/>이면: 상태가 끝나도 최소 노출을 채우고 사라진다. 번쩍임 0.</item>
    /// </list>
    /// 문안도 그 종류에 맞춘다 — "방금 유저가 나를 붙잡았다"는 <b>놓은 뒤에도 참</b>이다.
    /// 명령형(<c>놔</c>)은 명제가 아니라 발화 행위라 참/거짓 대상이 아니며, <c>Ragdoll</c>의 <c>으악!</c>과
    /// 같은 자리다("방금 그렇게 외쳤다"로 참).
    ///
    /// ============================================================================
    /// 변주 축은 <b>하나뿐</b>이다 — 유저가 몸의 어디를 잡았는가
    /// ============================================================================
    /// 한 번 잡기 = <b>대사 1줄</b>이다. <see cref="DialogueIntent"/>는 <c>StateTransitionContext</c>를
    /// 요구하고 그 컨텍스트는 1회용 토큰이라, <b>상태 도중에 새 대사를 만드는 경로가 프로덕션에 없다</b>
    /// (그게 원칙 1의 구조적 방어선이다). 그래서 "짧게 집기 vs 오래 들기", "흔드는 중 vs 가만히"는
    /// <b>대사로 구분할 수 없다</b> — <c>Enter()</c> 시점에 커서 속도 표본이 0개이므로 지어내면 그게
    /// 원칙 1 위반이다. 흔들기에 대한 반응은 이미 <b>몸</b>이 하고 있다
    /// (<c>DragThrowState.TickStruggle</c>의 커서 속도 부스트).
    ///
    /// <para>남는 축은 <b>잡힌 자리</b> 하나이고, 그건 우리가 고르는 값이 아니라
    /// <b>유저가 마우스로 정한 물리적 사실</b>이다(<see cref="GrabZone"/>).</para>
    ///
    /// ============================================================================
    /// ★★ <see cref="GrabParams.HasGrabPoint"/>가 <b>별도 필드</b>인 이유
    /// ============================================================================
    /// <c>_grabOffset == Vector2.zero</c>는 <b>「발끝을 정확히 잡았다」와 「커서를 못 읽었다」가
    /// 완전히 같은 값</b>이다. 이 저장소가 반복해서 당한 형태 — <b>실패한 측정과 성공한 측정이
    /// 똑같이 생겼다</b>. 오프셋만 보고 구역을 고르면 <b>좌표 조회 실패가 전부 「다리 놔」로 위장한다.</b>
    /// 그래서 확신이 없으면 <b>부위를 주장하지 않는다</b>(<see cref="GrabZone.Unknown"/> → 폴백 1줄).
    ///
    /// <para>★ 폴백을 <b>일부러 1줄</b>로 둔다. 좌표 프로브가 조용히 죽으면 캐릭터가 항상
    /// <c>잡혔다</c>만 말하게 되어 <b>증상이 화면에 드러난다</b>. 여기에 줄을 더 넣으면 프로브 사망이
    /// 다양성 뒤에 숨는다 — 늘리지 마라.</para>
    ///
    /// ============================================================================
    /// 지어내지 않은 것 (설계 §2-4에서 명시적으로 배제됨)
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><c>거꾸로잖아</c>류 — 발을 잡혀도 <b>루트는 계속 직립</b>이다(발버둥 비틀림 ±9°가 전부).</item>
    ///   <item><c>발 떴잖아</c> — 지면 소프트 클램프 때문에 바닥을 따라 끌릴 수 있다.</item>
    ///   <item><c>손 치워</c> — 화면에 손이 없다. 커서다.</item>
    ///   <item>몸통 전용 줄 — <c>몸통 놔</c>/<c>배 놔</c>는 어색하고, 어색함을 무릅쓰면
    ///     <b>말이 부자연스러운 대가로 정보가 0</b>이다. 자연스러운 낱말이 있는 두 구역에서만 부위를 말한다.</item>
    /// </list>
    ///
    /// <para><b>어조</b>: 사용자 예시는 <c>놔줘 놔줘</c>였는데 실측 33/33이 전부 반말이고 그중
    /// <c>나 안 해!</c>·<c>흥... 그럼 한 입만이다</c>처럼 <b>대드는 결</b>이라, <c>design-narrative</c>가
    /// 요청·애원을 반말 명령 <c>놔, 놔</c>로 바꿨다(반복 리듬은 <b>한 말풍선 안에</b> 넣어 유지).
    /// ★ <b>사용자의 낱말을 바꾼 것이라 리더 판정 대기 중이다</b> — 되돌린다면 이 파일
    /// <see cref="AnyLines"/>의 그 한 줄만 교체하면 된다. 두 벌을 함께 두지 않는다.</para>
    ///
    /// <para><b>플랫폼</b>: 완전 중립. <c>#if</c>가 없고 문자열과 비율뿐이라 macOS/Windows/iOS 동일.</para>
    /// </summary>
    public static class GrabReactionLines
    {
        /// <summary>
        /// 잡힌 자리 구역. 경계는 <b>고른 값이 아니라</b> <c>Core/StickmanMetrics</c>가 프리팹에서
        /// 실측한 랜드마크다(머리 링 아랫끝 / 고관절) — <see cref="Classify"/> 참고.
        /// </summary>
        public enum GrabZone
        {
            /// <summary>구역을 주장할 수 없음(커서 조회 실패 / 오프셋 clamp 발동 / 히트박스 밖 좌표).
            /// <b>실패를 0과 구분하기 위해 0번 자리에 둔다</b> — 기본값이 곧 "모름"이어야 안전하다.</summary>
            Unknown = 0,

            /// <summary>머리 링의 실제 세로 범위 안. 어깨를 경계로 쓰면 그 사이(=목)를 "머리"라고 부르게 된다.</summary>
            Head,

            /// <summary>고관절 ~ 머리 링 아랫끝. <b>부위를 주장하지 않는다</b>(공통 줄만).</summary>
            Torso,

            /// <summary>고관절 아래. 명명이 모호하지 않다.</summary>
            Leg,
        }

        /// <summary>
        /// 상태가 <see cref="IHasDialogueParams"/>로 노출하는 스냅샷. 난수는 <c>Enter()</c>에서 한 번
        /// 소진되고 그 결과만 여기 남는다(<c>AmbientChatter.ChatterParams</c>와 동일 관례) —
        /// 그래야 <see cref="Resolve"/>가 순수 함수로 남는다.
        /// </summary>
        public sealed class GrabParams
        {
            /// <summary>잡힌 높이(신장비, 발끝 0 = 정수리 1). <b>진단·로그용</b>이며 구역 판정의 원재료다.
            /// 판정 결과는 <see cref="Zone"/> 하나만 믿는다(두 진실원을 만들지 않는다).
            /// 확신이 없으면 <see cref="float.NaN"/>.</summary>
            public float GrabHeightRatio;

            /// <summary>★ 부위를 주장해도 되는가. 클래스 문서 참고 — <b>절대 <see cref="GrabHeightRatio"/>가
            /// 0인지로 대신 판정하지 마라.</b> 그건 발끝을 잡은 경우와 구분되지 않는다.</summary>
            public bool HasGrabPoint;

            /// <summary><c>Enter()</c>에서 확정된 구역.</summary>
            public GrabZone Zone;

            /// <summary><c>Enter()</c>에서 소진한 난수 스냅샷.</summary>
            public int LineIndex;
        }

        // ================================================================================
        // 대사표 — 설계 §2-5 확정 풀 9줄
        // ================================================================================

        /// <summary>위(머리) 전용.</summary>
        private static readonly string[] HeadLines =
        {
            "머리 놔",
            "거긴 머리야",
        };

        /// <summary>아래(다리) 전용.</summary>
        private static readonly string[] LegLines =
        {
            "다리 놔",
            "거긴 다리야",
        };

        /// <summary>세 구역 어디서나 참인 공통 줄 — 부위를 주장하지 않는다.</summary>
        private static readonly string[] AnyLines =
        {
            "야!",
            // ★ 사용자 예시 "놔줘 놔줘"의 반복 리듬을 한 말풍선 안에 넣은 형태(리더 판정 대기 — 클래스 문서).
            "놔, 놔",
            "안 놔?",
            "어딜 잡아",
        };

        /// <summary>★ 부위 불명. <b>일부러 1줄</b>이다(클래스 문서의 카나리아 설계) — 늘리지 마라.</summary>
        private static readonly string[] FallbackLines =
        {
            "잡혔다",
        };

        // 구역별 균등 추첨을 위한 합본. 정적 초기화 1회라 발화 경로에서는 할당이 없다
        // (24시간 상주 앱 — 대사 파생은 하루 수만 번 돈다).
        private static readonly string[] HeadPool = Combine(HeadLines, AnyLines);
        private static readonly string[] LegPool = Combine(LegLines, AnyLines);

        private static string[] Combine(string[] zoneLines, string[] commonLines)
        {
            var merged = new string[zoneLines.Length + commonLines.Length];
            zoneLines.CopyTo(merged, 0);
            commonLines.CopyTo(merged, zoneLines.Length);
            return merged;
        }

        // ================================================================================
        // 구역 판정 — 순수 함수(경계를 인자로 받는다)
        // ================================================================================

        /// <summary>
        /// 잡힌 높이를 구역으로 나눈다. <b>경계를 상수로 갖지 않고 인자로 받는 이유</b>: 두 랜드마크는
        /// <c>StickmanMetrics</c>가 프리팹 계층에서 <b>실측</b>하는 값이라(배율/조형이 바뀌면 함께 바뀐다)
        /// 이 파일에 숫자로 베끼면 그 순간 사본이 갈라진다.
        /// </summary>
        /// <param name="heightRatio">잡힌 높이(신장비). 발끝 0, 정수리 1.</param>
        /// <param name="headRingBottomRatio">머리 링 <b>아랫끝</b>의 신장비
        /// (<c>(HeadCenterLocalY - HeadRadius) / TotalHeight</c>).</param>
        /// <param name="hipRatio">고관절의 신장비(<c>HipLocalY / TotalHeight</c>).</param>
        /// <returns>경계 자체가 말이 안 되면(NaN·역순·비양수) <see cref="GrabZone.Unknown"/> —
        /// <b>모르면 부위를 주장하지 않는다</b>.</returns>
        public static GrabZone Classify(float heightRatio, float headRingBottomRatio, float hipRatio)
        {
            if (float.IsNaN(heightRatio) || float.IsInfinity(heightRatio)) return GrabZone.Unknown;
            if (float.IsNaN(headRingBottomRatio) || float.IsNaN(hipRatio)) return GrabZone.Unknown;
            if (hipRatio <= 0f || headRingBottomRatio <= hipRatio) return GrabZone.Unknown;

            if (heightRatio >= headRingBottomRatio) return GrabZone.Head;
            if (heightRatio < hipRatio) return GrabZone.Leg;
            return GrabZone.Torso;
        }

        // ================================================================================
        // 대사 파생
        // ================================================================================

        /// <summary>이 구역에서 뽑을 수 있는 줄 수. <c>Enter()</c>의 난수 상한이며, 이 값과
        /// <see cref="Resolve"/>가 <b>같은 표</b>를 보게 하려고 한 곳에서 유도한다.</summary>
        public static int PoolSizeFor(GrabZone zone, bool hasGrabPoint) => TableFor(zone, hasGrabPoint).Length;

        /// <summary>
        /// 텍스트 매핑 함수(<b>순수</b>). 난수/시간/전역 상태를 읽지 않는다 — 그래야 "이 텍스트가 어느
        /// <c>Enter()</c>의 어느 스냅샷에서 나왔는지"가 역추적된다(UX_FLOW.md 31-3).
        ///
        /// <para><paramref name="stateId"/>는 계약상 항상 <see cref="StickmanStateId.Dragged"/>다
        /// (이 풀을 쓰는 상태가 하나뿐이다). 분기하지 않는 이유는 문장이 <b>어느 상태에서 읽어도
        /// 같은 사실</b>("방금 붙잡혔다")을 말하기 때문이다.</para>
        /// </summary>
        public static DialogueLine Resolve(StickmanStateId stateId, object dialogueParams)
        {
            var p = dialogueParams as GrabParams;
            string[] table = p != null
                ? TableFor(p.Zone, p.HasGrabPoint)
                : FallbackLines;   // 파라미터가 없다 = 아무것도 모른다 → 부위를 주장하지 않는다.

            if (table.Length == 0) return DialogueLine.React(string.Empty);
            int index = p != null ? p.LineIndex : 0;
            return DialogueLine.React(table[((index % table.Length) + table.Length) % table.Length]);
        }

        /// <summary>
        /// ★ 잠금이 <b>둘</b>이다: <paramref name="hasGrabPoint"/>가 false거나 구역이
        /// <see cref="GrabZone.Unknown"/>이면 폴백. 둘 중 하나만 두면 나중에 한쪽을 세팅하지 않는
        /// 호출부가 생겼을 때 <b>조용히 틀린 부위를 말하게 된다</b>.
        /// </summary>
        private static string[] TableFor(GrabZone zone, bool hasGrabPoint)
        {
            if (!hasGrabPoint) return FallbackLines;
            switch (zone)
            {
                case GrabZone.Head: return HeadPool;
                case GrabZone.Leg: return LegPool;
                case GrabZone.Torso: return AnyLines;
                default: return FallbackLines;
            }
        }
    }
}
