using NUnit.Framework;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 격파 미니게임 배치 회귀 테스트 (사용자 실측 신고 2026-08-29 대응)
    ///
    /// 신고 원문: "게이지가 케릭터랑겹치고".
    /// 원인: 기 모으기 게이지가 컨테이너 로컬 x=0 **중심**으로 그려져, 캐릭터 중심에서 겨우
    /// +0.27유닛(판자 더미의 +0.55유닛보다 안쪽)까지 파고들어 다리/몸통 위에 얹혔다.
    /// 수정: 게이지를 판자 더미와 같은 근접 모서리에서 시작시키고 캐릭터 반대 방향으로만 뻗게 했다.
    ///
    /// 왜 유닛 테스트인가(실측 한계의 정직한 기록): 겹침 자체는 실물 앱 스크린샷으로 전/후를 확인했다.
    /// 하지만 리더가 함께 요구한 "캐릭터가 화면 가장자리에 있을 때도 게이지가 보이는가"는 실물에서
    /// 재현을 기다리기 어렵다 — 캐릭터가 화면 끝에 서 있는 **동안** 자동 발동이 겹쳐야 하는데,
    /// 트리거 확률을 100%로 올린 검증 빌드에서도 연속 17회 소환이 전부 화면 중앙(OS x 520~970)에서
    /// 일어났다. 그래서 경계 동작만 여기서 결정론적으로 잠근다.
    ///
    /// ★ 크기 대응(리더 지시 2026-08-29): 사용자가 "캐릭터 사이즈가 지금의 절반 정도 + 추후 조정 가능"을
    /// 요구했으므로, 모든 케이스를 <b>현재 크기(2.27)와 절반 크기(1.135) 둘 다</b>에서 돌린다. 배치가
    /// 절대 유닛으로 회귀하면(비율 파생을 깨면) 절반 크기 케이스가 즉시 빨간불이 된다.
    ///
    /// 화면 기준값은 실행 환경 실측(1512x982 포인트, 카메라 orthographicSize=12, 종횡비 1.54)에서
    /// 온 것이다 -> 가시 가로 반폭 약 18.48유닛.
    /// </summary>
    public class BattleMinigamePlacementTests
    {
        private const float VisibleHalfWidth = 18.48f;
        private const float VisibleMin = -VisibleHalfWidth;
        private const float VisibleMax = VisibleHalfWidth;

        /// <summary>현재 프리팹 실측 전신 높이와, 사용자가 요구한 "절반 크기".</summary>
        private const float FullHeight = BattleMinigameRenderer.FallbackHeight; // 2.27
        private const float HalfHeight = FullHeight * 0.5f;

        /// <summary>화면 하드 클램프가 캐릭터를 화면 끝에서 붙잡아 세우는 거리(실측 약 58pt ≈ 1.42유닛).</summary>
        private const float ClampInset = 1.42f;

        private static void AssertContract(
            BattleMinigameRenderer.Placement p, float anchorX, float height, string because)
        {
            float near = p.ContainerX - p.Facing * BattleMinigameRenderer.NearEdgeLocalX(height);
            float far = p.ContainerX + p.Facing * BattleMinigameRenderer.FarEdgeLocalX(height);

            float clearance = (near - anchorX) * p.Facing;
            Assert.GreaterOrEqual(clearance, BattleMinigameRenderer.MinCharacterClearance(height) - 0.001f,
                $"{because}: 연출의 근접 모서리가 캐릭터에 너무 가깝습니다(여유 {clearance:F3}유닛). " +
                "이게 깨지면 사용자가 신고한 '게이지가 캐릭터와 겹친다'가 재발합니다.");

            float left = System.Math.Min(near, far);
            float right = System.Math.Max(near, far);
            Assert.GreaterOrEqual(left, VisibleMin - 0.001f, $"{because}: 연출 왼쪽이 화면 밖입니다.");
            Assert.LessOrEqual(right, VisibleMax + 0.001f, $"{because}: 연출 오른쪽이 화면 밖입니다.");
        }

        [Test]
        public void 게이지의_근접_모서리는_판자_더미와_정확히_같은_선이다(
            [Values(FullHeight, HalfHeight)] float height)
        {
            // 이 등식이 수정의 본체다. 게이지가 판자의 근접 모서리에서 시작해 반대 방향으로만 뻗으면
            // 연출 전체의 폭(근접+먼)은 정확히 게이지 폭과 같아진다. 게이지가 다시 캐릭터 쪽으로
            // 튀어나오면(예: 중앙 정렬로 되돌리면) 이 등식이 즉시 깨진다.
            Assert.AreEqual(
                height * BattleMinigameRenderer.GaugeWidthRatio,
                BattleMinigameRenderer.NearEdgeLocalX(height) + BattleMinigameRenderer.FarEdgeLocalX(height),
                0.0001f,
                "게이지의 근접 모서리가 판자 더미의 근접 모서리와 어긋났습니다 — " +
                "사용자가 신고한 '게이지가 캐릭터와 겹친다'가 재발합니다.");

            Assert.AreEqual(height * BattleMinigameRenderer.TileWidthRatio * 0.5f,
                BattleMinigameRenderer.NearEdgeLocalX(height), 0.0001f,
                "근접 모서리는 판자 반폭이어야 합니다.");
        }

        [Test]
        public void 게이지_폭은_읽히는_크기_아래로_줄어들지_않는다()
        {
            // 리더 지시: 이전에 "너무 얇아서 안 읽힌다"고 한 번 상향한 이력이 있으니 되돌리지 말 것.
            // 겹침을 게이지 축소로 '해결'하려는 시도를 여기서 막는다(이번 수정은 폭을 1유닛도 줄이지
            // 않고 시작점만 옮겼다). 비율로 검사하므로 캐릭터 크기가 바뀌어도 의미가 유지된다.
            Assert.GreaterOrEqual(BattleMinigameRenderer.GaugeWidthRatio, 0.687f,
                "게이지 폭 비율이 줄었습니다 — 겹침은 폭이 아니라 시작 위치로 푸는 것이 이 수정의 전제입니다.");
            Assert.Greater(BattleMinigameRenderer.GaugeWidthRatio, BattleMinigameRenderer.TileWidthRatio,
                "게이지는 판자 더미보다 길어야 채움 진행이 읽힙니다.");
        }

        [Test]
        public void 배치는_캐릭터_크기에_정비례한다()
        {
            // 크기를 절반으로 줄이면 모든 오프셋도 정확히 절반이어야 한다. 절대 유닛 상수가 하나라도
            // 남아 있으면 이 비례가 깨진다(리더 지시의 핵심 계약).
            var full = BattleMinigameRenderer.ComputePlacement(0f, 1f, VisibleMin, VisibleMax, FullHeight);
            var half = BattleMinigameRenderer.ComputePlacement(0f, 1f, VisibleMin, VisibleMax, HalfHeight);

            Assert.AreEqual(full.ContainerX * 0.5f, half.ContainerX, 0.0001f,
                "컨테이너 위치가 캐릭터 크기에 비례하지 않습니다 — 절대 유닛 상수가 남아 있습니다.");
            Assert.AreEqual(BattleMinigameRenderer.FarEdgeLocalX(FullHeight) * 0.5f,
                BattleMinigameRenderer.FarEdgeLocalX(HalfHeight), 0.0001f);
            Assert.AreEqual(BattleMinigameRenderer.TopEdgeLocalY(FullHeight) * 0.5f,
                BattleMinigameRenderer.TopEdgeLocalY(HalfHeight), 0.0001f);
        }

        [Test]
        public void 화면_중앙에서는_기본_배치를_그대로_쓴다(
            [Values(-1f, 1f)] float facing, [Values(FullHeight, HalfHeight)] float height)
        {
            var p = BattleMinigameRenderer.ComputePlacement(0f, facing, VisibleMin, VisibleMax, height);

            Assert.IsFalse(p.Mirrored, "화면 중앙에서는 미러링이 일어나면 안 됩니다.");
            Assert.AreEqual(facing, p.Facing, "화면 중앙에서 정면이 바뀌면 안 됩니다.");
            AssertContract(p, 0f, height, $"화면 중앙(높이 {height:F2})");
        }

        [Test]
        public void 화면_끝에서_바깥을_보면_반대편으로_미러링해_전부_보이게_한다(
            [Values(-1f, 1f)] float side)
        {
            // 캐릭터가 그 방향 화면 끝에 바짝 붙어(하드 클램프 한계), 정면도 바깥을 향한 최악의 배치.
            float anchorX = side * (VisibleHalfWidth - ClampInset);

            var p = BattleMinigameRenderer.ComputePlacement(anchorX, side, VisibleMin, VisibleMax, FullHeight);

            Assert.IsTrue(p.Mirrored, "화면 끝에서 바깥을 볼 때는 반대편으로 미러링해야 합니다.");
            Assert.AreEqual(-side, p.Facing, "미러링했으면 배치 방향도 뒤집혀야 합니다.");
            AssertContract(p, anchorX, FullHeight, "화면 끝 + 바깥 정면");
        }

        [Test]
        public void 화면_어디에_서_있어도_겹치지_않고_화면_안에_남는다(
            [Values(FullHeight, HalfHeight)] float height)
        {
            // 하드 클램프가 허용하는 전 구간을 0.25유닛 간격으로 훑는다.
            for (float anchorX = VisibleMin + ClampInset; anchorX <= VisibleMax - ClampInset; anchorX += 0.25f)
            {
                foreach (float facing in new[] { -1f, 1f })
                {
                    var p = BattleMinigameRenderer.ComputePlacement(
                        anchorX, facing, VisibleMin, VisibleMax, height);
                    AssertContract(p, anchorX, height, $"anchorX={anchorX:F2}, facing={facing}, 높이={height:F2}");
                }
            }
        }

        [Test]
        public void 캐릭터가_화면_맨_위_창_테두리에_서_있어도_연출이_화면_안에_들어온다(
            [Values(FullHeight, HalfHeight)] float height)
        {
            // 실측 재현: 화면 거의 전체를 덮는 창의 상단 테두리(OS y=33)에 캐릭터가 서면, 판자 더미가
            // 통째로 화면 위로 사라졌다(연속 8회 소환 전부). 세로 클램프가 그걸 되돌린다.
            const float visTop = 12f;     // orthographicSize=12, 카메라 y=0
            const float visBottom = -12f;
            float anchorY = visTop - 0.2f; // 화면 맨 위에 선 캐릭터

            float containerY = BattleMinigameRenderer.ComputeContainerY(anchorY, visTop, visBottom, height);

            Assert.LessOrEqual(containerY + BattleMinigameRenderer.TopEdgeLocalY(height), visTop + 0.001f,
                "판자 더미 윗변이 화면 위로 잘렸습니다.");
            Assert.GreaterOrEqual(containerY + BattleMinigameRenderer.BottomEdgeLocalY(height), visBottom - 0.001f,
                "게이지 아랫변이 화면 아래로 잘렸습니다.");
            Assert.Less(containerY, anchorY,
                "화면 맨 위에서는 연출이 아래로 내려와야 합니다(그대로 두면 보이지 않습니다).");
        }

        [Test]
        public void 화면_한가운데_높이에서는_세로_클램프가_작동하지_않는다()
        {
            const float visTop = 12f;
            const float visBottom = -12f;

            Assert.AreEqual(0f,
                BattleMinigameRenderer.ComputeContainerY(0f, visTop, visBottom, FullHeight), 0.0001f,
                "세로 클램프가 필요 없는 위치에서 연출 높이를 건드리면 '주먹 높이' 구도가 깨집니다.");
        }
    }
}
