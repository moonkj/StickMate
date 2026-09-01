using UnityEngine;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 반짝임 FX의 <b>발동 리듬</b> — 2026-09-01 (docs/UX_FLOW.md 37-3 (F)(3) / 로드맵 P4).
    ///
    /// ============================================================================
    /// 고치는 것은 값이 아니라 <b>구조</b>다
    /// ============================================================================
    /// 옛 <see cref="CharacterFxRenderer"/>는 이렇게 잡혀 있었다:
    /// 무장 3초 + 재발동 대기 6~10초. 그런데 자율 배회의 Idle 유지 시간은
    /// <see cref="StickConfig.wanderIdleDurationMax"/> = <b>6초</b>이고, Idle을 벗어나는 순간
    /// 두 타이머가 <b>0으로 리셋</b>된다. 즉 무장에 3초를 쓰고 나면 남는 Idle이 최대 3초인데
    /// 다음 발동까지 6~10초를 더 기다려야 한다 — <b>2회차 반짝임은 원리적으로 절대 오지 않는다.</b>
    /// (측정 노출률 약 8%. 사용자의 "반짝임효과도 잘적용안됨"은 정확한 관찰이었다.)
    ///
    /// 진짜 결함은 숫자가 아니라 <b>두 시스템이 서로의 최대값을 모른다</b>는 것이다.
    /// 그래서 값을 고치지 않는다 — 값을 고치면 배회 시간을 조정하는 순간 같은 버그가 재발한다.
    /// 대신 <b>불변식을 코드로 강제</b>한다:
    ///
    /// <code>
    ///     무장시간 + 재발동 대기 최대값 + 수명  ≤  배회 Idle 최대 지속시간
    /// </code>
    ///
    /// 이 부등식이 성립하면 <b>한 Idle 구간 안에서 반짝임이 최소 2회 완결</b>된다.
    /// 배회 시간을 어떻게 바꾸든(에셋에서든 테스트에서든) 이 관계는 <see cref="Resolve"/>가
    /// 매번 다시 유도하므로 깨질 수 없다. 회귀 잠금:
    /// Tests/EditMode/SparkleCadenceInvariantTests.
    ///
    /// ============================================================================
    /// "설정값을 존중하되 상한을 강제한다"
    /// ============================================================================
    /// 배회 창이 넉넉하면(예: Idle 최대 30초) 아래 <c>Configured*</c> 값이 <b>그대로</b> 쓰인다.
    /// 창이 좁을 때만 창에서 유도한 상한으로 끌어내린다. 오늘 하루 <c>DockGeometry</c>·
    /// <c>GroundSensor</c>에서 반복 적용한 <c>max(설정값, 필요값)</c> 패턴의 <b>반대 방향</b>이다
    /// (그쪽은 하한, 여기는 상한).
    /// </summary>
    internal static class SparkleCadence
    {
        // ---- 설계값(33-5절이 못박은 리듬). 창이 넉넉하면 이 값이 그대로 나간다.
        internal const float ConfiguredArmSeconds = 3.0f;
        internal const float ConfiguredIntervalMinSeconds = 6f;
        internal const float ConfiguredIntervalMaxSeconds = 10f;
        internal const float ConfiguredLifeSeconds = 1.2f;

        /// <summary>무장이 Idle 창에서 차지할 수 있는 최대 지분. 창의 1/4을 넘으면 "가만히 있어야
        /// 나오는 연출"이 아니라 "거의 안 나오는 연출"이 된다.</summary>
        internal const float ArmShareOfIdleWindow = 0.25f;

        /// <summary>수명의 지분. 무장(0.25)과 합쳐 0.45이므로 재발동 대기에 <b>항상</b>
        /// 창의 55%가 남는다 — 불변식의 우변이 음수가 될 수 없다는 것이 식으로 보장된다.</summary>
        internal const float LifeShareOfIdleWindow = 0.20f;

        /// <summary>설정이 없는 리그(테스트 스텁/사본)에서 쓸 Idle 창.
        /// <see cref="States.AutoWanderController"/>가 쓰는 폴백과 <b>같은 값</b>이어야 한다 —
        /// 두 폴백이 다르면 설정이 빠진 씬에서만 리듬이 어긋난다.</summary>
        internal const float FallbackIdleWindowSeconds = 6f;

        /// <summary>이 캐릭터의 배회 Idle 최대 지속시간(초).</summary>
        internal static float IdleWindowSeconds(StickConfig config)
        {
            float configured = config != null ? config.wanderIdleDurationMax : 0f;
            return configured > 0.01f ? configured : FallbackIdleWindowSeconds;
        }

        /// <summary>지금 설정에서 쓸 무장/대기/수명. 위 불변식을 <b>언제나</b> 만족한다.</summary>
        internal static void Resolve(StickConfig config, out float armSeconds, out float lifeSeconds,
            out float intervalMinSeconds, out float intervalMaxSeconds)
            => Resolve(IdleWindowSeconds(config), out armSeconds, out lifeSeconds,
                out intervalMinSeconds, out intervalMaxSeconds);

        /// <summary>설정 오브젝트 없이 창 길이만으로 유도한다(테스트가 여러 창 길이를 훑을 수 있게).</summary>
        internal static void Resolve(float idleWindowSeconds, out float armSeconds, out float lifeSeconds,
            out float intervalMinSeconds, out float intervalMaxSeconds)
        {
            float window = Mathf.Max(0.01f, idleWindowSeconds);

            armSeconds = Mathf.Min(ConfiguredArmSeconds, window * ArmShareOfIdleWindow);
            lifeSeconds = Mathf.Min(ConfiguredLifeSeconds, window * LifeShareOfIdleWindow);

            // ★ 불변식이 나오는 곳은 여기 한 줄이다. 남은 창 = 창 − 무장 − 수명(항상 > 0).
            float remaining = window - armSeconds - lifeSeconds;
            intervalMaxSeconds = Mathf.Min(ConfiguredIntervalMaxSeconds, remaining);

            // 설계된 6:10 비율을 유지한 채 함께 줄인다 — 상한만 깎으면 무작위성이 사라져
            // 반짝임이 기계처럼 규칙적으로 뜬다.
            const float designedRatio = ConfiguredIntervalMinSeconds / ConfiguredIntervalMaxSeconds;
            intervalMinSeconds = Mathf.Min(ConfiguredIntervalMinSeconds, intervalMaxSeconds * designedRatio);
            if (intervalMinSeconds > intervalMaxSeconds) intervalMinSeconds = intervalMaxSeconds;
        }
    }
}
