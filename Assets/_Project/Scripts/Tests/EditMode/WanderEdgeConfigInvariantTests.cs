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

        [Test]
        public void 맨틀_인셋은_경계_판정_거리보다_커야_한다()
        {
            StickConfig c = LoadDefaultConfig();
            Assert.Greater(c.parkourMantleInset, c.wanderEdgeStopDistance,
                $"parkourMantleInset({c.parkourMantleInset:F3})이 wanderEdgeStopDistance({c.wanderEdgeStopDistance:F3}) " +
                "이하입니다 — 턱 위에 올라선 그 자리가 이미 '발판 경계'로 판정되어, 방향이 한 번 바깥으로 " +
                "뒤집히면 곧바로 다시 뛰어내립니다(2026-08-29 사용자 신고의 필요조건).");
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
