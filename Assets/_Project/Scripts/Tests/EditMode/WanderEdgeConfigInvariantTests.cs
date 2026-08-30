using NUnit.Framework;
using UnityEditor;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 설정값 사이의 **불변식**을 잠근다 (2026-08-29, 사용자 신고 "독위로 가끔 올라오긴 하지만 바로
    /// 다시 내려감" / "이 끝쪽에서만 계속 왔다갔다만함" 대응).
    ///
    /// 이번 버그의 재발 조건은 코드가 아니라 **두 숫자의 대소 관계**였다. 실측:
    ///   · 등반이 끝난 캐릭터는 붙잡은 턱의 모서리에서 parkourMantleInset만큼 안쪽에 선다(맨틀 X=13.326,
    ///     Dock 오른쪽 모서리 X=13.576 -> 남은 거리 0.250).
    ///   · 배회 AI는 진행 방향 앞쪽 잔여 거리가 wanderEdgeStopDistance(0.300) 이하면 "경계"로 본다.
    ///   · 그래서 inset(0.25) &lt; stopDistance(0.30)인 동안에는 **올라선 그 자리가 이미 경계**였고,
    ///     진행 방향이 바깥으로 한 번 뒤집히기만 하면 다음 프레임에 곧바로 뛰어내리기 추첨이 돌았다.
    ///
    /// 값을 다시 만지는 사람이 이 관계를 모른 채 inset을 줄이거나 stopDistance를 키우면 증상이 그대로
    /// 되살아나므로, 관계 자체를 여기서 단언한다. 이 테스트는 **원본 자산을 읽기만 한다**(불변 원칙 3).
    ///
    /// 주의: 이 관계는 필요조건일 뿐 충분조건이 아니다 — 실제 원인 수정은
    /// StickConfig.postClimbDescendCooldown(되올라간 직후 되내려가기 유예)이고, 그 동작 자체는
    /// Tests/PlayMode/EdgeHopDownTests.cs가 네거티브 컨트롤과 함께 실측으로 잠근다.
    /// </summary>
    public class WanderEdgeConfigInvariantTests
    {
        private const string DefaultConfigPath = "Assets/_Project/Data/DefaultStickConfig.asset";

        private static StickConfig LoadDefaultConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<StickConfig>(DefaultConfigPath);
            Assert.IsNotNull(config, $"기본 설정 자산을 찾지 못했습니다: {DefaultConfigPath}");
            return config;
        }

        /// <summary>
        /// ★ 2026-08-30 R3-M1로 비교 대상이 바뀌었다 — 상대는 <b>설정값이 아니라 유도값</b>이다.
        /// 경계 판정 거리는 이제 max(설정값, 몸의 물리 반폭 + 0.10)이라(Core/DockGeometry.
        /// ResolveEdgeStopDistance) 배포 배율 0.75에서 0.300이 아니라 <b>0.405</b>다.
        /// 예전처럼 설정값(0.300)과만 비교하면 이 불변식은 "지키고 있다"고 초록불을 내면서 실제로는
        /// 깨져 있을 수 있다 — 그게 정확히 이 프로젝트가 계속 당해 온 실패 유형이다.
        ///
        /// 여유를 그냥 &gt;0이 아니라 <see cref="MinInsetMarginUnits"/> 이상 요구하는 이유:
        /// 유휴 "주위 살피기"가 머리를 최대 (키 x idleAmbientLookHeadShiftRatio) 만큼 옆으로 밀고,
        /// 좌표 왕복 오차도 0.02유닛까지 허용된다. 소수점 셋째 자리로 붙어 있는 두 상수는
        /// "우연히 안 깨지고 있는" 상태이며 R3-M1이 정확히 그렇게 터졌다.
        /// </summary>
        private const float MinInsetMarginUnits = 0.05f;

        [Test]
        public void 맨틀_인셋은_유도된_경계_판정_거리보다_충분히_커야_한다()
        {
            StickConfig c = LoadDefaultConfig();
            float halfWidth = StickConfig.BaselineBodyPhysicsHalfWidth * c.ResolveCharacterScale();
            float resolvedStop = DockGeometry.ResolveEdgeStopDistance(c.wanderEdgeStopDistance, halfWidth);
            float margin = c.parkourMantleInset - resolvedStop;

            UnityEngine.Debug.Log($"[WANDER-EDGE] 배율 {c.ResolveCharacterScale():F3} → 물리 반폭 {halfWidth:F3}, " +
                $"경계 판정 거리 설정값 {c.wanderEdgeStopDistance:F3} → 유도값 {resolvedStop:F3}, " +
                $"맨틀 인셋 {c.parkourMantleInset:F3} (여유 {margin:F3}, 요구 {MinInsetMarginUnits:F3} 이상)");

            Assert.Greater(margin, MinInsetMarginUnits,
                $"parkourMantleInset({c.parkourMantleInset:F3})이 **유도된** 경계 판정 거리" +
                $"({resolvedStop:F3} = max(설정 {c.wanderEdgeStopDistance:F3}, 물리 반폭 {halfWidth:F3} + " +
                $"{DockGeometry.EdgeStopWallStandoffMarginUnits:F2}))보다 {MinInsetMarginUnits:F3} 넘게 크지 " +
                "않습니다 — 턱 위에 올라선 그 자리가 이미 '발판 경계'로 판정되어, 방향이 한 번 바깥으로 " +
                "뒤집히면 곧바로 다시 뛰어내립니다(2026-08-29 사용자 신고의 필요조건).");
        }

        /// <summary>
        /// ★ R3-M1의 본체 — 경계 판정 밴드가 **몸이 벽에 붙어 설 수 있는 이격보다 넓은가.**
        /// 좁으면 Dock 물리 계단 옆면에 막혀 선 캐릭터가 밴드에 물리적으로 들어가지 못해
        /// 되올라가기 판정을 평가할 기회조차 없다(사용자가 세 번 신고한 증상).
        ///
        /// 네거티브 컨트롤: AutoWanderController가 유도값 대신 설정값(0.300)을 그대로 쓰던 예전
        /// 코드로 되돌리면 여유가 0.305 → 0.300으로 **음수**가 되어 이 단언이 즉시 실패한다.
        /// </summary>
        [Test]
        public void 경계_판정_밴드는_벽_이격보다_넓어야_한다()
        {
            StickConfig c = LoadDefaultConfig();
            float scale = c.ResolveCharacterScale();
            float halfWidth = StickConfig.BaselineBodyPhysicsHalfWidth * scale;
            // Box2D 접촉 이격(ProjectSettings의 defaultContactOffset 0.01 → 정착 시 약 절반).
            const float ContactSeparation = 0.005f;
            float standoff = halfWidth + ContactSeparation;
            float resolvedStop = DockGeometry.ResolveEdgeStopDistance(c.wanderEdgeStopDistance, halfWidth);

            UnityEngine.Debug.Log($"[WANDER-EDGE] 벽 이격 {standoff:F3}(반폭 {halfWidth:F3} + 접촉 {ContactSeparation:F3}) " +
                $"vs 유도 판정 거리 {resolvedStop:F3} → 여유 {(resolvedStop - standoff):F3}");

            Assert.GreaterOrEqual(resolvedStop - standoff, MinInsetMarginUnits,
                $"경계 판정 거리({resolvedStop:F3})가 벽 이격({standoff:F3})보다 " +
                $"{MinInsetMarginUnits:F3} 넘게 크지 않습니다 — 캐릭터가 Dock 물리 계단 옆면에 붙어 서면 " +
                "경계 밴드에 영영 들어가지 못해 되올라가기를 평가조차 못 합니다(2026-08-30 R3-M1).");

            // ★ 설정 절대값 단독으로는 못 덮는다는 사실 자체를 박제한다(유도가 사라지면 빨간불).
            Assert.Less(c.wanderEdgeStopDistance, standoff,
                $"설정 절대값({c.wanderEdgeStopDistance:F3})이 벽 이격({standoff:F3})을 이미 덮고 있습니다 — " +
                "이 테스트의 전제(R3-M1의 근거)가 바뀌었습니다. 설정값을 올려 덮은 것이라면 배율을 " +
                "키웠을 때 다시 깨지므로, 유도(DockGeometry.ResolveEdgeStopDistance) 쪽을 유지하세요.");
        }

        [Test]
        public void 되올라간_직후_되내려가기_유예가_켜져_있어야_한다()
        {
            StickConfig c = LoadDefaultConfig();
            Assert.Greater(c.postClimbDescendCooldown, 0f,
                $"postClimbDescendCooldown({c.postClimbDescendCooldown:F2})이 0 이하입니다 — 이 값이 0이면 " +
                "수정 전 거동(되올라간 직후 같은 모서리로 즉시 되내려감)이 그대로 복원됩니다. " +
                "0은 Tests/PlayMode/EdgeHopDownTests의 네거티브 컨트롤 전용 값입니다.");
        }

        [Test]
        public void 유예_시간은_한_배회_사이클보다_길어야_한다()
        {
            StickConfig c = LoadDefaultConfig();
            // 유예가 "서기 1회 + 걷기 1회"보다 짧으면, 올라선 뒤 안쪽으로 걸어 들어갔다가 되돌아오는
            // 첫 왕복에서 곧바로 다시 내려갈 수 있다(체감상 여전히 "금방 내려감").
            float oneCycle = c.wanderIdleDurationMax + c.wanderWalkDurationMin;
            Assert.GreaterOrEqual(c.postClimbDescendCooldown, oneCycle,
                $"postClimbDescendCooldown({c.postClimbDescendCooldown:F2}초)이 배회 한 사이클" +
                $"(서기 최대 {c.wanderIdleDurationMax:F1} + 걷기 최소 {c.wanderWalkDurationMin:F1} = {oneCycle:F1}초)보다 " +
                "짧습니다 — 올라오자마자 되돌아와 다시 내려가는 왕복이 유예 안에서 끝나버립니다.");
        }
    }
}
