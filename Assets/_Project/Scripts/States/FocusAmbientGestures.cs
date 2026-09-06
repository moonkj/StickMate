using StickMate.Core;

namespace StickMate.States
{
    /// <summary>
    /// ★ 집중 세션 앰비언트 제스처 4종의 <b>어휘 정본</b>(2026-09-06,
    /// docs/UX_MOTION_FOCUS_SESSION.md 4-3). 아이디 · 지속 시간 · 가중치가 여기 한 곳에만 있다.
    ///
    /// <para><b>여기에 타이머도 확률 게이트도 없다.</b> "언제 한 번 뽑을지"는 전적으로 발행자
    /// (<see cref="AutoWanderController"/>의 기존 1회 추첨 + 최소 간격)가 정하고, 이 클래스는
    /// <b>뽑을 때 무엇이 나오는가</b>만 답한다. 그래서 세션 중에도 <b>빈도는 평소와 같은 계통</b>이고
    /// 바뀌는 것은 어휘와 쿨다운 값 하나뿐이다 — 2026-08-31 사용자 신고 "너무 자주함"의 체감 하한을
    /// 다시 넘지 않기 위한 구조적 장치다.</para>
    ///
    /// <para><b>왜 StickConfig가 아닌가</b>: 네 값의 <b>합이 1</b>이어야 하고 지속 시간은
    /// <see cref="StickmanPoseAnimator"/>의 박자표와 짝이다. 인스펙터에서 하나만 만지면 그 순간
    /// 분포가 깨지거나 박자가 어긋난다(자세 각도를 StickConfig에 넣지 않는 것과 같은 판단).
    /// 대신 <see cref="WeightSum"/>이 그 불변식을 테스트가 잠글 수 있게 노출한다.</para>
    ///
    /// <para><b>G3 폴백</b>: 커서를 못 읽는 환경(플랫폼 미지원/원격 데스크톱/권한 없음)에서는
    /// <see cref="Draw"/>가 G3를 <b>추첨에서 통째로 제외</b>하고 그 가중치를 G1에 합친다.
    /// 없는 대상을 향해 돌아보는 그림은 절대 불변 원칙 1(사실과 행동의 싱크) 위반이다.</para>
    /// </summary>
    public static class FocusAmbientGestures
    {
        // ── 지속 시간(초) — StickmanPoseAnimator의 박자표와 짝이다 ──────────────────────
        // 진행도 0~1이 이 시간에 매핑되므로, 여기를 바꾸면 박자표의 «평평한 구간»(= 한 장의 그림이
        // 되는 자리)의 실제 길이가 함께 바뀐다.

        /// <summary>G1 자세 고쳐 잡기. 풀기 0.36초 + 유지 0.10초 + 다시 접기 0.55초 + 안착.</summary>
        public const float RecrossSeconds = 1.30f;

        /// <summary>G2 발밑 링 확인. 유지 구간 0.28초가 "봤다"의 최소 체류다.</summary>
        public const float RingCheckSeconds = 1.00f;

        /// <summary>G3 화면 쪽 돌아보기. 4종 중 가장 짧다 — 시선 이동은 원래 빠르다.</summary>
        public const float ScreenGlanceSeconds = 0.90f;

        /// <summary>G4 관망 자세 바꾸기. 중립을 반드시 통과해야 해서 가장 길다.</summary>
        public const float StanceSwapSeconds = 1.60f;

        // ── 가중치 — 합 1.00 ────────────────────────────────────────────────────────────
        // 25분 44회 기준 노출: G1 15.0회 / G2 11.5회 / G3 10.6회 / G4 7.0회.
        // G4가 가장 낮은 이유: 관망 자세 토글이 평균 3.5분마다여야 "가끔 자세를 바꾼다"로 읽힌다.
        // 더 잦으면 안절부절못하는 그림이 되고, 더 드물면 25분에 몇 번 안 나와 P2가 없는 것과 같다.

        public const float RecrossWeight = 0.34f;
        public const float RingCheckWeight = 0.26f;
        public const float ScreenGlanceWeight = 0.24f;
        public const float StanceSwapWeight = 0.16f;

        /// <summary>가중치 합. 테스트가 1.00을 잠근다(숫자를 베끼지 않고 이 값을 읽는다).</summary>
        public const float WeightSum = RecrossWeight + RingCheckWeight + ScreenGlanceWeight + StanceSwapWeight;

        /// <summary>이 신호가 집중 세션 전용 어휘인가. 평소 어휘(LookAround/SitAndYawn)와 섞이면
        /// 관망 자세 위에 손차양/만세가 얹혀 "지켜보는 그림"이 통째로 깨진다 — 그래서
        /// <see cref="StickmanBlackboard.BeginIdleAmbientMotion"/>이 이 판정으로 양방향을 막는다.</summary>
        public static bool IsFocusGesture(WanderAmbientMotion motion)
        {
            switch (motion)
            {
                case WanderAmbientMotion.FocusRecross:
                case WanderAmbientMotion.FocusRingCheck:
                case WanderAmbientMotion.FocusScreenGlance:
                case WanderAmbientMotion.FocusStanceSwap:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>이 어휘의 지속 시간(초). 집중 어휘가 아니면 0(호출부가 그것을 "재생 안 함"으로 읽는다).</summary>
        public static float DurationSeconds(WanderAmbientMotion motion)
        {
            switch (motion)
            {
                case WanderAmbientMotion.FocusRecross: return RecrossSeconds;
                case WanderAmbientMotion.FocusRingCheck: return RingCheckSeconds;
                case WanderAmbientMotion.FocusScreenGlance: return ScreenGlanceSeconds;
                case WanderAmbientMotion.FocusStanceSwap: return StanceSwapSeconds;
                default: return 0f;
            }
        }

        /// <summary>
        /// 가중 추첨. <paramref name="roll01"/>은 <b>발행자가 이미 갖고 있는 난수 하나</b>(개체별 독립
        /// RNG)를 그대로 넘긴 것이다 — 여기서 새 난수를 뽑지 않는다.
        /// </summary>
        /// <param name="cursorAvailable">커서 좌표를 실제로 읽을 수 있는가. false면 G3를 제외한다.</param>
        public static WanderAmbientMotion Draw(double roll01, bool cursorAvailable)
        {
            // G3를 못 쓰면 그 몫을 G1에 합친다(가중치 합은 언제나 1이라 분포가 조용히 찌그러지지 않는다).
            float recross = cursorAvailable ? RecrossWeight : RecrossWeight + ScreenGlanceWeight;

            double r = roll01;
            if (r < recross) return WanderAmbientMotion.FocusRecross;
            r -= recross;
            if (r < RingCheckWeight) return WanderAmbientMotion.FocusRingCheck;
            r -= RingCheckWeight;
            if (cursorAvailable)
            {
                if (r < ScreenGlanceWeight) return WanderAmbientMotion.FocusScreenGlance;
                r -= ScreenGlanceWeight;
            }
            // 남은 구간은 G4다. roll01이 1에 아주 가까울 때 부동소수 잔차로 아무 갈래에도 안 걸리는
            // 경우를 없애기 위해 마지막 갈래는 비교 없이 받는다(가중치 합이 정확히 1이라도 안전하게).
            return WanderAmbientMotion.FocusStanceSwap;
        }
    }
}
