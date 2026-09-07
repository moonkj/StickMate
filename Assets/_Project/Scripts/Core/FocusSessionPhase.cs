using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// 집중 세션(뽀모도로) 한 판을 시간축으로 나눈 <b>3구간</b>.
    /// 정본은 <c>docs/DESIGN_COSTUME_FOCUS_ARCHITECTURE.md</c> 8절이다.
    ///
    /// <para>★ <b>이것은 <see cref="StickmanStateId"/>가 아니다.</b> 구간은 상태머신의 단일 상태
    /// 슬롯을 <b>한 번도 건드리지 않는다</b> — 코스튬 연출은 <c>Idle</c>의 <b>포즈 층</b>에서만 산다
    /// (규칙 C-4). 새 상태 ID를 만들면 <c>Platform/FramePacing</c>의 절감 등급 입력
    /// (<c>CurrentStateId == Idle</c>)이 세션 내내 거짓이 되어 <c>Still</c> 등급이 도달 불가가 되고,
    /// 제출률이 15/초 → 30/초로 <b>정확히 2배</b>가 된다(계약서 4-1절 실측).</para>
    /// </summary>
    public enum FocusSessionPhase
    {
        /// <summary>세션이 돌고 있지 않다. <see cref="Interaction.FocusWatchDirector.CurrentPhase"/>는
        /// 세션이 없는 동안 항상 이 값이다 — 「구간」은 세션 안에만 존재한다.</summary>
        None = 0,

        /// <summary>적응기 — 세션 앞 가장자리. 아직 자리를 잡는 중이라 코스튬/프롭이 <b>안 나온다</b>.</summary>
        Adapt = 1,

        /// <summary>몰입기 — 가운데. 코스튬 LFVS와 프롭이 사는 유일한 구간이고, 배회 확률이 0이 된다.</summary>
        Immersion = 2,

        /// <summary>한계 — 세션 뒤 가장자리. 프롭을 철거하고 기존 관망 자세로 돌아간다(«지친다»의 표현).</summary>
        Limit = 3,
    }

    /// <summary>
    /// 구간 경계를 계산하는 <b>순수 함수</b>. MonoBehaviour도 <c>Time</c>도 참조하지 않으므로
    /// EditMode에서 그대로 잰다(계약서 8-1절이 요구한 형태).
    ///
    /// ============================================================================
    /// 왜 「비율」도 「절대값」도 아니고 셋을 겹쳐 무는가
    /// ============================================================================
    /// 이 저장소는 <b>뽀모도로 자유시간 1~60분을 이미 출하했다</b>
    /// (<c>Interaction/FocusSessionPopover.MinimumSessionMinutes</c>=1 /
    /// <c>MaximumSessionMinutes</c>=60). <b>25분 고정으로 짜면 나머지 59개 값에서 구간이 깨진다.</b>
    /// <list type="bullet">
    ///   <item>순수 비율(20/60/20)만 쓰면 60분 세션의 적응기가 <b>12분</b>이 된다 — 자리 잡는 데
    ///         12분은 연출로도 사실로도 거짓이다.</item>
    ///   <item>절대값(5분 고정)만 쓰면 <b>1분 세션에서 적응기가 세션보다 길다</b>.</item>
    /// </list>
    /// 그래서 <see cref="NominalFraction"/> · <see cref="MinEdgeSeconds"/> ·
    /// <see cref="MaxEdgeSeconds"/> · <see cref="MaxEdgeFraction"/> 넷을 겹쳐 문다.
    /// 결과 곡선은 <c>0.4D → 60 → 0.2D → 300</c>이고 <b>경계에서 연속·단조 비감소</b>다
    /// (D=150에서 0.4×150=60, D=300에서 0.2×300=60, D=1500에서 0.2×1500=300 — 꺾이지만 안 튄다).
    ///
    /// <para>★ <b>몰입기는 어떤 세션 길이에서도 0이 아니다.</b> 가장자리 합이 항상
    /// <c>2 × 0.40D = 0.8D</c> 이하라 몰입기 폭이 최소 <c>0.2D</c>다(최단 60초 세션에서도 12초).
    /// 이 성질이 계약서 7-4절의 「세션 전체를 누적 대상으로 센다」를 성립시킨다 — 코스튬이 화면에
    /// 한 번도 안 뜨는 세션이 있으면 «일어나지 않은 일을 센다»가 되어 절대 불변 원칙 1을 깬다.</para>
    ///
    /// <para>★ <b>닫힌 갈림 1건(2026-09-07)</b> — <c>docs/UX_MOTION_COSTUME_FOCUS.md</c> 3-1절이
    /// 한때 같은 경계를 하한 20초로 적어 <b>D &lt; 300초(다이얼 1~4분·데모 90초)에서만</b> 갈렸다.
    /// 리더 판정으로 <b>이 파일의 값이 정본</b>이고 그쪽 문서가 정정됐다. <b>되돌리지 마라.</b></para>
    /// </summary>
    public static class FocusSessionPhases
    {
        /// <summary>가장자리의 명목 비율 — 25분에서 5분(사용자 요구 「0~5 / 5~20 / 20~25분」).</summary>
        public const float NominalFraction = 0.20f;

        /// <summary>가장자리 하한(초) — 짧은 세션에서 구간이 사라지지 않게.</summary>
        public const float MinEdgeSeconds = 60f;

        /// <summary>가장자리 상한(초) — 긴 세션에서 적응기가 늘어지지 않게(60분 세션의 12분 적응기 방지).</summary>
        public const float MaxEdgeSeconds = 300f;

        /// <summary>가장자리가 세션을 다 먹지 못하게 하는 비율 상한 — 두 가장자리 합이 <c>0.8D</c>를 못 넘는다.</summary>
        public const float MaxEdgeFraction = 0.40f;

        /// <summary>가장자리(적응기 = 한계 구간) 길이(초).</summary>
        public static float EdgeSeconds(float durationSeconds)
            => Mathf.Min(
                 Mathf.Clamp(NominalFraction * durationSeconds, MinEdgeSeconds, MaxEdgeSeconds),
                 MaxEdgeFraction * durationSeconds);

        /// <summary>가운데(몰입기) 길이(초) = <c>D − 2×가장자리</c>. 항상 <c>0.2D</c> 이상이다.
        /// <para>표시·검산용 편의 함수이고 <see cref="Of"/>는 이 값을 쓰지 않는다 — 판정은
        /// 경계 하나(<see cref="EdgeSeconds"/>)에서만 파생돼야 두 계산이 어긋날 여지가 없다.</para></summary>
        public static float ImmersionSeconds(float durationSeconds)
            => durationSeconds > 0f ? durationSeconds - 2f * EdgeSeconds(durationSeconds) : 0f;

        /// <summary>
        /// 경과 시점이 어느 구간인가. <paramref name="elapsedSeconds"/>는
        /// <c>SessionDurationSeconds − RemainingSeconds</c>다 — <b>새 타이머를 만들지 마라</b>
        /// (두 곳에서 같은 시간을 세면 반드시 어긋난다).
        ///
        /// <para>경계는 <b>반열린 구간</b>이다: 적응기 <c>[0, E)</c> · 몰입기 <c>[E, D−E)</c> ·
        /// 한계 <c>[D−E, ∞)</c>. 경과가 세션 길이를 넘어도(마지막 프레임의 <c>dt</c>만큼 넘친다)
        /// 한계로 남는다 — 완주 직전 프레임에 구간이 튀지 않는다.</para>
        /// </summary>
        public static FocusSessionPhase Of(float durationSeconds, float elapsedSeconds)
        {
            // NaN/0/음수 길이는 「세션이 아니다」로 읽는다(부정 비교라 NaN도 여기서 걸린다).
            if (!(durationSeconds > 0f)) return FocusSessionPhase.None;

            float edge = EdgeSeconds(durationSeconds);

            // 부정 비교 — 음수·NaN 경과를 전부 세션 초입으로 보낸다(프롭이 안 뜨는 안전한 방향).
            if (!(elapsedSeconds >= edge)) return FocusSessionPhase.Adapt;
            if (elapsedSeconds < durationSeconds - edge) return FocusSessionPhase.Immersion;
            return FocusSessionPhase.Limit;
        }
    }
}
