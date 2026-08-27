using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// docs/UX_FLOW.md 19절 스트레스 게이지 — 값(0~1)만 보관하는 정적 상태 저장소.
    /// SpectacleEventLock/TodoListModel과 동일한 이유(24시간 상주 앱, 씬 생명주기와 무관한 단일
    /// 프로세스 전역 상태)로 정적 클래스로 구현한다.
    ///
    /// 이 클래스 자신은 언제/왜 값이 바뀌는지 전혀 모른다 — 트리거 판정(격파훈련 과다/장시간 방치/
    /// 긴급정지 반복 사용/시간당 자연 감소)은 전부 Interaction/StressGaugeDirector.cs의 책임이고,
    /// 이 클래스는 오직 "현재 값 보관 + 변경 시 StickmanEventBus.StressLevelChanged 통지"만 한다
    /// (StickmanBlackboard.LastImpactMagnitude처럼 "값의 의미"와 "값을 읽어 반응하는 로직"을 분리하는
    /// 기존 컨벤션과 동일).
    ///
    /// 3단 노출(19절 — 상시 표정 암시/트레이 색점/설정창 상세)은 지금 렌더링 레이어가 없어 구현하지
    /// 않는다 — StressLevelChanged 이벤트만 지금 확정해두고, 아무도 구독하지 않아도 무해하다
    /// (WanderAmbientMotionRequested류 기존 패턴과 동일, 이번 라운드 지시사항의 명시적 스코프 축소).
    /// </summary>
    public static class StressGauge
    {
        public static float CurrentLevel { get; private set; }

        /// <summary>델타를 더하고(음수면 감소) 0~1로 clamp한다. 값이 실제로 바뀌었을 때만
        /// StickmanEventBus.StressLevelChanged를 발행한다(무의미한 재통지 방지).</summary>
        public static void Add(float delta)
        {
            if (delta == 0f) return;
            SetLevel(CurrentLevel + delta);
        }

        public static void SetLevel(float level)
        {
            float clamped = Mathf.Clamp01(level);
            if (Mathf.Approximately(clamped, CurrentLevel)) return;
            CurrentLevel = clamped;
            StickmanEventBus.RaiseStressLevelChanged(CurrentLevel);
        }

        /// <summary>간식으로 달래졌을 때(20절) 사용 — "상당량 감소, 완전 리셋은 아님"을 만족하려면
        /// 호출자가 항상 음수 delta(예: -runawaySnackStressRelief)로 Add()를 호출하면 된다. 이 메서드는
        /// 테스트/디버그 등에서 완전 초기화가 필요할 때만 쓴다(정상 게임플레이 경로에서는 호출되지 않음).</summary>
        public static void ResetForTesting()
        {
            CurrentLevel = 0f;
        }
    }
}
