using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 32종 도형 계층의 <b>데이터 계약</b> 회귀 — 2026-08-30 외부 핸드오프 이식 라운드.
    ///
    /// ============================================================================
    /// 이 파일이 잡으려는 실패
    /// ============================================================================
    /// (a) <b>아이템 자리 어긋남</b>. <c>AccessoryShapeBuilder</c>는 "3번은 왕관"처럼 <b>정수 자리</b>로
    ///     도형을 고르는데, 그 순서의 진짜 주인은 <c>Core/ItemCatalog.cs</c>의 표다. 누가 표 중간에
    ///     아이템을 하나 끼워 넣으면 <b>예외도 경고도 없이</b> 왕관 자리에 중절모가 그려진다.
    ///     그래서 자리 상수 전부를 <b>아이디 문자열</b>과 맞대어 잠근다.
    /// (b) <b>모자 커버선의 하드코딩화</b>. 33-4-1은 "왕관만 예외로 머리가 보인다"를 <b>if 분기가 아니라
    ///     데이터</b>(<c>HatCoverLocalY = +∞</c>)로 표현하라고 명시했다. 그 데이터가 실제로 그런지 본다.
    /// (c) <b>레이어 순서 뒤집힘</b>. 안경이 눈동자보다 아래로 가면 선글라스 설명문
    ///     ("표정이 잘 안 보인다")이 거짓말이 된다.
    ///
    /// PlayMode가 아니라 EditMode인 이유: 전부 <b>순수 계산</b>이라 씬도 물리도 필요 없고,
    /// <c>InternalsVisibleTo(StickMate.Tests.EditMode)</c> 덕분에 internal 상수를 직접 볼 수 있다.
    /// </summary>
    public sealed class AccessoryShapeCatalogTests
    {
        /// <summary>배율 1.0 프리팹 실측 리그(도형 계산에만 쓴다 — GameObject를 만들지 않는다).</summary>
        private static AccessoryShapeBuilder.Rig Rig(float facing = 1f)
        {
            const float H = StickConfig.BaselineCharacterTotalHeight;
            const float R = 0.22f;
            return new AccessoryShapeBuilder.Rig(R, H - R, 1.7646944f, 0.9346944f, facing);
        }

        // ============================================================================
        // (a) 도형이 고르는 "자리"가 카탈로그 표의 자리와 같다
        // ============================================================================

        [TestCase(EquipmentSlot.Head, AccessoryShapeBuilder.HeadCap, "equip.head.cap")]
        [TestCase(EquipmentSlot.Head, AccessoryShapeBuilder.HeadBeanie, "equip.head.fur")]
        [TestCase(EquipmentSlot.Head, AccessoryShapeBuilder.HeadFedora, "equip.head.fedora")]
        [TestCase(EquipmentSlot.Head, AccessoryShapeBuilder.HeadCrown, "equip.head.crown")]
        [TestCase(EquipmentSlot.Eyes, AccessoryShapeBuilder.EyesSunglasses, "equip.eyes.sunglasses")]
        [TestCase(EquipmentSlot.Eyes, AccessoryShapeBuilder.EyesRound, "equip.eyes.round")]
        [TestCase(EquipmentSlot.Eyes, AccessoryShapeBuilder.EyesGoggles, "equip.eyes.goggles")]
        [TestCase(EquipmentSlot.Eyes, AccessoryShapeBuilder.EyesMonocle, "equip.eyes.monocle")]
        [TestCase(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBowTie, "equip.neck.bowtie")]
        [TestCase(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckStriped, "equip.neck.striped")]
        [TestCase(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckScarf, "equip.neck.scarf")]
        [TestCase(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell, "equip.neck.bell")]
        [TestCase(EquipmentSlot.Shoulders, AccessoryShapeBuilder.BackCape, "equip.shoulders.cape")]
        [TestCase(EquipmentSlot.Shoulders, AccessoryShapeBuilder.BackLongCape, "equip.shoulders.long_cape")]
        [TestCase(EquipmentSlot.Shoulders, AccessoryShapeBuilder.BackWings, "equip.shoulders.wings")]
        [TestCase(EquipmentSlot.Shoulders, AccessoryShapeBuilder.BackBackpack, "equip.shoulders.backpack")]
        [TestCase(EquipmentSlot.Hair, AccessoryShapeBuilder.HairCowlick, "look.hair.cowlick")]
        [TestCase(EquipmentSlot.Hair, AccessoryShapeBuilder.HairNeat, "look.hair.neat")]
        [TestCase(EquipmentSlot.Hair, AccessoryShapeBuilder.HairCurly, "look.hair.curly")]
        [TestCase(EquipmentSlot.Hair, AccessoryShapeBuilder.HairBald, "look.hair.bald")]
        // ---- 2026-09-01 카테고리당 +2종(임시 플레이스홀더) ----
        [TestCase(EquipmentSlot.Head, AccessoryShapeBuilder.HeadBeret, "equip.head.beret")]
        [TestCase(EquipmentSlot.Head, AccessoryShapeBuilder.HeadStraw, "equip.head.straw")]
        [TestCase(EquipmentSlot.Eyes, AccessoryShapeBuilder.EyesBrowline, "equip.eyes.browline")]
        [TestCase(EquipmentSlot.Eyes, AccessoryShapeBuilder.EyesPatch, "equip.eyes.patch")]
        [TestCase(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckPendant, "equip.neck.pendant")]
        [TestCase(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBandana, "equip.neck.bandana")]
        [TestCase(EquipmentSlot.Shoulders, AccessoryShapeBuilder.BackPoncho, "equip.shoulders.poncho")]
        [TestCase(EquipmentSlot.Shoulders, AccessoryShapeBuilder.BackFairyWings, "equip.shoulders.fairy_wings")]
        [TestCase(EquipmentSlot.Hair, AccessoryShapeBuilder.HairBowl, "look.hair.bowl")]
        [TestCase(EquipmentSlot.Hair, AccessoryShapeBuilder.HairPonytail, "look.hair.ponytail")]
        public void 도형이_고르는_자리가_카탈로그_표와_같다(EquipmentSlot slot, int itemIndex, string expectedId)
        {
            ItemCatalogEntry entry = ItemCatalog.Item(slot, itemIndex);
            Assert.IsNotNull(entry, $"{slot} {itemIndex}번 자리가 카탈로그 표에 없습니다.");
            Assert.AreEqual(expectedId, entry.Id,
                $"{slot} {itemIndex}번이 '{entry.Id}'입니다 — 도형은 '{expectedId}'를 그릴 생각이었습니다. " +
                "표 중간에 아이템이 끼어들었거나 순서가 바뀌었습니다(예외 없이 엉뚱한 그림이 나오는 유형).");
        }

        /// <summary>카테고리당 아이템 수. 7×4=28 -> <b>7×6=42</b>(2026-09-01 카테고리당 +2종).</summary>
        private const int ItemsPerSlot = 6;

        [Test]
        public void 그릴_수_있는_카테고리는_아이템_전부가_도형을_갖는다()
        {
            var drawable = new[]
            {
                EquipmentSlot.Head, EquipmentSlot.Eyes, EquipmentSlot.Neck,
                EquipmentSlot.Shoulders, EquipmentSlot.Hair,
            };
            var sink = new System.Collections.Generic.List<AccessoryShapeBuilder.Shape>();

            for (int s = 0; s < drawable.Length; s++)
            {
                int count = ItemCatalog.ItemCountIn(drawable[s]);
                Assert.AreEqual(ItemsPerSlot, count,
                    $"{drawable[s]} 카테고리의 아이템 수가 {ItemsPerSlot}이 아닙니다 — 표(에셋)와 도형 switch 중 " +
                    "한쪽만 늘어나면 늘어난 쪽이 예외 없이 빈 카드/빈 몸으로 나옵니다.");
                for (int i = 0; i < count; i++)
                {
                    sink.Clear();
                    AccessoryShapeBuilder.Append(sink, drawable[s], i, Rig());
                    Assert.Greater(sink.Count, 0,
                        $"{drawable[s]} {i}번({ItemCatalog.Item(drawable[s], i).DisplayName})이 선을 하나도 만들지 않습니다 — " +
                        "착용했는데 화면이 그대로면 그건 착용이 아닙니다(33-4 #4의 민머리 규칙).");
                    for (int k = 0; k < sink.Count; k++)
                    {
                        Assert.GreaterOrEqual(sink[k].Points.Length, 2,
                            $"{drawable[s]} {i}번의 '{sink[k].Name}' 선에 점이 {sink[k].Points.Length}개뿐입니다.");
                    }
                }
            }
        }

        /// <summary>머리카락을 <b>실제로 덮는다</b>고 스스로 선언한 모자 전부(= 커버선이 유한한 것).
        /// 배열을 손으로 적으면 새 모자가 들어올 때마다 이 파일이 조용히 뒤처진다 — 데이터에게 묻는다.</summary>
        private static int[] CoveringHats()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            var hats = new System.Collections.Generic.List<int>(ItemsPerSlot);
            for (int i = 0; i < ItemCatalog.ItemCountIn(EquipmentSlot.Head); i++)
            {
                if (!float.IsPositiveInfinity(AccessoryShapeBuilder.HatCoverLocalY(i, rig))) hats.Add(i);
            }
            Assert.Greater(hats.Count, 0, "머리카락을 덮는 모자가 하나도 없습니다 — 커버 규칙 검증이 공허해집니다.");
            return hats.ToArray();
        }

        // ============================================================================
        // (b) 모자 커버선은 데이터다 — 왕관만 +∞
        // ============================================================================

        [Test]
        public void 모자_커버선이_데이터로_표현되고_왕관만_아무것도_가리지_않는다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float hc = rig.HeadCenterY;
            float r = rig.HeadRadius;

            // ★ 2026-09-01(3차) — 숫자를 베끼지 않는다. 커버선은 <b>모자가 스스로 선언한 상수</b>에서
            //   나와야 하고, 그 상수가 곧 도형의 관/챙 경계이기도 하다(규칙 4-a).
            //   옛 본문은 0.62 / 0.42 / 0.58을 손으로 적어 두어, 커버선이 내려간 라운드에
            //   "설계가 바뀐 것"이 아니라 "테스트가 낡은 것"으로 빨간불이 났다.
            Assert.AreEqual(AccessoryShapeBuilder.HatBrimLocalY(rig),
                AccessoryShapeBuilder.HatCoverLocalY(AccessoryShapeBuilder.HeadCap, rig), 1e-5f,
                "천 모자의 커버선은 <b>챙 선 그 자체</b>여야 합니다 — 두 값이 갈라지면 챙 밑으로 " +
                "머리카락이 삐져나오거나 챙 위가 통째로 잘립니다.");
            Assert.AreEqual(hc + r * AccessoryShapeBuilder.BeanieBandTopRatio,
                AccessoryShapeBuilder.HatCoverLocalY(AccessoryShapeBuilder.HeadBeanie, rig), 1e-5f,
                "털모자의 커버선은 접힌 단의 윗변(= 관 밑변)이어야 합니다.");
            Assert.AreEqual(hc + r * AccessoryShapeBuilder.FedoraBrimLineRatio,
                AccessoryShapeBuilder.HatCoverLocalY(AccessoryShapeBuilder.HeadFedora, rig), 1e-5f);

            // 이 카테고리에서 <b>가장 깊이 눌러쓰는 모자</b>가 털모자라는 것은 도형의 정체다.
            for (int i = 0; i < ItemCatalog.ItemCountIn(EquipmentSlot.Head); i++)
            {
                if (i == AccessoryShapeBuilder.HeadBeanie) continue;
                float cover = AccessoryShapeBuilder.HatCoverLocalY(i, rig);
                if (float.IsPositiveInfinity(cover)) continue;
                Assert.Greater(cover,
                    AccessoryShapeBuilder.HatCoverLocalY(AccessoryShapeBuilder.HeadBeanie, rig),
                    $"모자 {i}번의 커버선이 털모자보다 낮습니다 — 털모자가 가장 깊이 눌러쓰는 " +
                    "모자라는 것이 그 아이템의 정체입니다.");
            }

            Assert.IsTrue(float.IsPositiveInfinity(
                    AccessoryShapeBuilder.HatCoverLocalY(AccessoryShapeBuilder.HeadCrown, rig)),
                "왕관의 커버선이 +∞가 아닙니다 — 왕관은 씌우는 것이 아니라 얹는 것이라 밑이 뚫려 있고, " +
                "그 예외는 if 분기가 아니라 **이 값**으로 표현돼야 합니다(33-4-1).");
            Assert.IsTrue(float.IsPositiveInfinity(AccessoryShapeBuilder.HatCoverLocalY(-1, rig)),
                "모자 미착용(-1)도 아무것도 가리지 않아야 합니다.");
        }

        /// <summary>
        /// ★ 2026-09-01 커버 규칙 변경 — <b>"선 통째로 생략" -> "커버선에서 자르기(clip)"</b>.
        ///
        /// <para>옛 규칙은 커버선 위로 올라가는 점이 하나라도 있으면 그 선을 통째로 버렸다. 머리카락이
        /// 선 1개짜리 호였을 때는 그것이 "모자 속에 감춘다"와 같았지만, P0에서 머리카락이 <b>닫힌 채움
        /// 도형</b>이 되면서 같은 규칙이 정확히 반대로 작동한다 — 실루엣 하나가 통째로 버려지므로
        /// <b>모자를 쓰면 머리카락이 전부 사라진다</b>(ux-designer가 37-7 #1로 보고한 자리).</para>
        ///
        /// <para>그래서 이제 조합표가 요구하는 것은 "0개"가 아니라 <b>"커버선 위에 잉크가 없다"</b>이다.
        /// 모자 밑으로 삐져나온 옆머리는 남는 것이 옳다(실제로 모자를 써도 귀 옆 머리는 보인다).</para>
        /// </summary>
        [Test]
        public void 모자를_쓰면_머리카락이_커버선_위로_한_점도_올라가지_않는다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float strokeHalf = StickConfig.BaselineCharacterTotalHeight
                * AccessoryShapeBuilder.StrokeWidthRatio * 0.5f;

            int[] hats = CoveringHats();
            var sink = new System.Collections.Generic.List<AccessoryShapeBuilder.Shape>();

            for (int h = 0; h < hats.Length; h++)
            {
                float cover = AccessoryShapeBuilder.HatCoverLocalY(hats[h], rig);
                for (int hair = 0; hair < ItemsPerSlot; hair++)
                {
                    sink.Clear();
                    AccessoryShapeBuilder.Append(sink, EquipmentSlot.Hair, hair, rig, cover, strokeHalf);
                    for (int i = 0; i < sink.Count; i++)
                    {
                        Vector3[] pts = sink[i].Points;
                        for (int k = 0; k < pts.Length; k++)
                        {
                            Assert.LessOrEqual(pts[k].y, cover + 1e-4f,
                                $"모자 {hats[h]}번을 썼는데 머리 {hair}번 '{sink[i].Name}'의 점이 " +
                                $"커버선({cover:F4}) 위 {pts[k].y:F4}에 남았습니다 — 모자를 뚫고 나온 머리카락입니다.");
                        }
                    }
                }
            }
        }

        /// <summary>왕관은 밑이 뚫려 있으므로 <b>한 점도 잘리지 않는다</b>(커버선 +∞).</summary>
        [Test]
        public void 왕관은_머리카락을_한_점도_자르지_않는다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float strokeHalf = StickConfig.BaselineCharacterTotalHeight
                * AccessoryShapeBuilder.StrokeWidthRatio * 0.5f;
            float crownCover = AccessoryShapeBuilder.HatCoverLocalY(AccessoryShapeBuilder.HeadCrown, rig);

            var bare = new System.Collections.Generic.List<AccessoryShapeBuilder.Shape>();
            var under = new System.Collections.Generic.List<AccessoryShapeBuilder.Shape>();
            for (int hair = 0; hair < ItemsPerSlot; hair++)
            {
                bare.Clear();
                under.Clear();
                AccessoryShapeBuilder.Append(bare, EquipmentSlot.Hair, hair, rig);
                AccessoryShapeBuilder.Append(under, EquipmentSlot.Hair, hair, rig, crownCover, strokeHalf);

                Assert.AreEqual(bare.Count, under.Count,
                    $"왕관을 썼더니 머리 {hair}번의 도형 수가 달라졌습니다.");
                for (int i = 0; i < bare.Count; i++)
                {
                    Assert.AreEqual(bare[i].Points.Length, under[i].Points.Length,
                        $"왕관을 썼더니 머리 {hair}번 '{bare[i].Name}'의 점 수가 달라졌습니다.");
                }
            }
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — 자르기가 <b>실제로 일을 하고 있는가</b>.
        /// 모자를 안 썼을 때의 머리카락은 커버선 위로 올라가는 점을 갖고 있어야 한다.
        /// 그러지 않는다면 위 테스트는 "애초에 자를 것이 없어서" 통과한 것뿐이다.
        /// </summary>
        [Test]
        public void 자르기가_없었다면_머리카락이_모자를_뚫고_나온다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            var sink = new System.Collections.Generic.List<AccessoryShapeBuilder.Shape>();

            int[] hats = CoveringHats();
            for (int h = 0; h < hats.Length; h++)
            {
                float cover = AccessoryShapeBuilder.HatCoverLocalY(hats[h], rig);
                for (int hair = 0; hair < ItemsPerSlot; hair++)
                {
                    sink.Clear();
                    AccessoryShapeBuilder.Append(sink, EquipmentSlot.Hair, hair, rig);   // 커버선 +∞

                    bool anyAbove = false;
                    for (int i = 0; i < sink.Count && !anyAbove; i++)
                    {
                        Vector3[] pts = sink[i].Points;
                        for (int k = 0; k < pts.Length; k++)
                        {
                            if (pts[k].y > cover) { anyAbove = true; break; }
                        }
                    }
                    Assert.IsTrue(anyAbove,
                        $"모자 {hats[h]}번의 커버선 위로 올라가는 점이 머리 {hair}번에 하나도 없습니다 — " +
                        "자를 것이 없다면 자르기 규칙을 검증하는 위 테스트가 공허하게 통과합니다.");
                }
            }
        }

        /// <summary>
        /// 자르기가 <b>실루엣을 통째로 지우지 않는다</b>(이번 API 변경의 존재 이유).
        /// 옛 "선 통째로 생략"이었다면 채움 도형 하나가 사라져 머리카락이 전부 없어졌을 것이다.
        /// </summary>
        [Test]
        public void 모자를_써도_옆머리는_남는다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            float strokeHalf = StickConfig.BaselineCharacterTotalHeight
                * AccessoryShapeBuilder.StrokeWidthRatio * 0.5f;
            var sink = new System.Collections.Generic.List<AccessoryShapeBuilder.Shape>();

            // 천 모자(커버선 R·0.62) — 세 모자 중 가장 얕게 쓴다.
            float cover = AccessoryShapeBuilder.HatCoverLocalY(AccessoryShapeBuilder.HeadCap, rig);
            for (int hair = 0; hair < ItemsPerSlot; hair++)
            {
                sink.Clear();
                AccessoryShapeBuilder.Append(sink, EquipmentSlot.Hair, hair, rig, cover, strokeHalf);
                Assert.Greater(sink.Count, 0,
                    $"천 모자를 썼더니 머리 {hair}번이 통째로 사라졌습니다 — " +
                    "이것이 채움 도형에 옛 '선 통째로 생략' 규칙을 적용했을 때 나는 그림입니다(37-7 #1).");
            }
        }

        /// <summary>잘라 낸 조각이 <b>획 하나보다 작으면</b> 버린다 — 커버선 위에 점 하나만 남는 것을 막는다.</summary>
        [Test]
        public void 획보다_작은_조각은_버린다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            var sink = new System.Collections.Generic.List<AccessoryShapeBuilder.Shape>();

            // 커버선 바로 아래에 아주 얇게 걸치는 삼각형 하나.
            float cover = rig.HeadCenterY;
            float sliver = rig.HeadRadius * 0.02f;
            var shape = new AccessoryShapeBuilder.Shape("Sliver", new[]
            {
                rig.F(-sliver, cover - sliver),
                rig.F(sliver, cover - sliver),
                rig.F(0f, cover + sliver * 4f),
            }, true, AccessoryShapeBuilder.SortHair, filled: true);

            AccessoryShapeBuilder.AppendClippedBelowCover(sink, shape, cover, rig.HeadRadius * 0.2f);
            Assert.AreEqual(0, sink.Count,
                "획(0.4R)보다 작은 조각이 살아남았습니다 — 커버선 위에 얹힌 점 하나로 보입니다.");

            sink.Clear();
            AccessoryShapeBuilder.AppendClippedBelowCover(sink, shape, cover, 0f);
            Assert.AreEqual(1, sink.Count, "획 반폭 0에서는 같은 조각이 살아남아야 합니다(네거티브 컨트롤).");
        }

        // ============================================================================
        // (c) 레이어 순서 — 겹침 관계가 설명문과 맞아야 한다
        // ============================================================================

        [Test]
        public void 레이어_순서가_겹침_규칙을_만족한다()
        {
            // 프리팹 실측: 뒤쪽 팔다리 0 / 몸통 1 / 앞쪽 팔다리 2 / 머리 링 4 / 눈동자 5.
            const int CharacterMinStroke = 0;
            const int PupilOrder = 5;

            Assert.Less(AccessoryShapeBuilder.SortBack, CharacterMinStroke,
                "망토/날개/배낭이 캐릭터 획보다 앞에 있습니다 — 33-2-0이 요구한 '몸통 선 뒤'가 성립하지 않습니다.");
            Assert.Greater(AccessoryShapeBuilder.SortEyes, PupilOrder,
                "안경이 눈동자(5)보다 아래입니다 — 렌즈가 눈동자 뒤로 숨어 선글라스 설명문 " +
                "'표정이 잘 안 보인다'가 거짓이 됩니다.");
            Assert.Greater(AccessoryShapeBuilder.SortHead, AccessoryShapeBuilder.SortHair,
                "모자가 머리 모양보다 아래입니다.");
            Assert.Greater(AccessoryShapeBuilder.SortHair, PupilOrder,
                "머리 모양이 눈동자보다 아래입니다 — 정수리 위 도형이 머리 링에 가립니다.");
            Assert.AreEqual(6, AccessoryShapeBuilder.SortDefault,
                "AddLine의 기본 레이어가 6에서 바뀌면 이 인자를 넘기지 않는 옛 호출부의 그림이 조용히 달라집니다.");
        }

        [Test]
        public void 각_아이템의_선이_선언한_레이어로_나온다()
        {
            var expected = new (EquipmentSlot Slot, int Order)[]
            {
                (EquipmentSlot.Head, AccessoryShapeBuilder.SortHead),
                (EquipmentSlot.Eyes, AccessoryShapeBuilder.SortEyes),
                (EquipmentSlot.Neck, AccessoryShapeBuilder.SortNeck),
                (EquipmentSlot.Shoulders, AccessoryShapeBuilder.SortBack),
                (EquipmentSlot.Hair, AccessoryShapeBuilder.SortHair),
            };
            var sink = new System.Collections.Generic.List<AccessoryShapeBuilder.Shape>();

            for (int e = 0; e < expected.Length; e++)
            {
                for (int i = 0; i < ItemCatalog.ItemCountIn(expected[e].Slot); i++)
                {
                    sink.Clear();
                    AccessoryShapeBuilder.Append(sink, expected[e].Slot, i, Rig());
                    for (int k = 0; k < sink.Count; k++)
                    {
                        Assert.AreEqual(expected[e].Order, sink[k].SortingOrder,
                            $"{expected[e].Slot} {i}번의 '{sink[k].Name}'이 레이어 {sink[k].SortingOrder}로 나왔습니다.");
                    }
                }
            }
        }

        // ============================================================================
        // 원칙 1 — 흔들린다고 적힌 아이템은 실제로 흔들 점을 갖고 있다
        // ============================================================================

        /// <summary>
        /// 목도리 "끝자락이 걸을 때마다 흔들린다" / 짧은 망토 "늘 가는 방향의 반대쪽으로 날린다" /
        /// 방울 목걸이(리더 승인으로 문구가 '흔들린다'류로 교체 예정) — 셋 다 <b>흔들 점 구간</b>을
        /// 선언하고 있어야 한다. 선언이 없으면 렌더러는 영원히 정적이고, 그건 문구가 없는 동작을
        /// 주장하는 것 = 원칙 1 위반이다.
        /// </summary>
        [TestCase(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckScarf)]
        [TestCase(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBell)]
        [TestCase(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckStriped)]
        [TestCase(EquipmentSlot.Shoulders, AccessoryShapeBuilder.BackCape)]
        [TestCase(EquipmentSlot.Shoulders, AccessoryShapeBuilder.BackLongCape)]
        public void 흔들린다고_적힌_아이템은_흔들_점_구간을_선언한다(EquipmentSlot slot, int item)
        {
            var sink = new System.Collections.Generic.List<AccessoryShapeBuilder.Shape>();
            AccessoryShapeBuilder.Append(sink, slot, item, Rig());

            bool any = false;
            for (int i = 0; i < sink.Count; i++)
            {
                if (!sink[i].HasSway) continue;
                any = true;
                Assert.LessOrEqual(sink[i].SwayStart + sink[i].SwayCount, sink[i].Points.Length,
                    $"'{sink[i].Name}'의 흔들 구간이 점 배열 밖을 가리킵니다(런타임 IndexOutOfRange).");
            }
            Assert.IsTrue(any,
                $"{ItemCatalog.Item(slot, item).DisplayName}에 흔들 점이 하나도 없습니다 — " +
                $"설명문(\"{ItemCatalog.Item(slot, item).Description}\")이 주장하는 동작이 코드에 없습니다.");
        }

        /// <summary>날개는 "뜨지는 않지만 폼은 난다" — 흔들 점이 <b>없어야</b> 한다(천이 아니다).</summary>
        [Test]
        public void 날개와_배낭은_흔들리지_않는다()
        {
            var sink = new System.Collections.Generic.List<AccessoryShapeBuilder.Shape>();
            foreach (int item in new[] { AccessoryShapeBuilder.BackWings, AccessoryShapeBuilder.BackBackpack })
            {
                sink.Clear();
                AccessoryShapeBuilder.Append(sink, EquipmentSlot.Shoulders, item, Rig());
                for (int i = 0; i < sink.Count; i++)
                {
                    Assert.IsFalse(sink[i].HasSway,
                        $"'{sink[i].Name}'이 흔들립니다 — 날개/배낭은 천이 아니라 흔들리면 안 됩니다.");
                }
            }
        }

        // ============================================================================
        // 33-2-5 (D) 줄무늬 타이의 월요일
        // ============================================================================

        [Test]
        public void 줄무늬_타이는_월요일에만_느슨해진다()
        {
            var normal = new System.Collections.Generic.List<AccessoryShapeBuilder.Shape>();
            var monday = new System.Collections.Generic.List<AccessoryShapeBuilder.Shape>();
            AccessoryShapeBuilder.Append(normal, EquipmentSlot.Neck, AccessoryShapeBuilder.NeckStriped, Rig(),
                mondayLoosened: false);
            AccessoryShapeBuilder.Append(monday, EquipmentSlot.Neck, AccessoryShapeBuilder.NeckStriped, Rig(),
                mondayLoosened: true);

            Assert.AreEqual(normal.Count, monday.Count, "월요일이라고 선 개수가 달라지면 안 됩니다.");
            float normalKnotTop = normal[0].Points[0].y;
            float mondayKnotTop = monday[0].Points[0].y;
            Assert.Less(mondayKnotTop, normalKnotTop,
                "월요일인데 매듭이 내려가지 않았습니다 — 설명문 '월요일마다 조금 느슨해진다'가 성립하지 않습니다.");
            // ★ 2026-09-02 — 느슨해지는 <b>양</b>의 정본은 이제 에셋이다(B-2 파일럿으로 목 형상이
            //   equip_neck_striped.asset의 wornShapes로 내려갔고 TieMondayLoosenDropRatio는 사라졌다).
            //   그 값을 여기 숫자로 다시 적으면 "에셋만 고친 날 조용히 통과"하는 사본이 생긴다.
            //   그래서 이 검사는 <b>양이 배율에 비례한다</b>(= 절대 상수가 아니다)와
            //   <b>"조금"이라는 설명문을 지킬 만큼 작다</b>만 잠근다.
            //   정확한 값은 Tests/EditMode/Golden/NeckWornShapeGolden.txt가 비트 단위로 잠근다.
            float drop = normalKnotTop - mondayKnotTop;

            var halfNormal = new System.Collections.Generic.List<AccessoryShapeBuilder.Shape>();
            var halfMonday = new System.Collections.Generic.List<AccessoryShapeBuilder.Shape>();
            AccessoryShapeBuilder.Rig half = Scaled(0.5f);
            AccessoryShapeBuilder.Append(halfNormal, EquipmentSlot.Neck, AccessoryShapeBuilder.NeckStriped,
                half, mondayLoosened: false);
            AccessoryShapeBuilder.Append(halfMonday, EquipmentSlot.Neck, AccessoryShapeBuilder.NeckStriped,
                half, mondayLoosened: true);
            float halfDrop = halfNormal[0].Points[0].y - halfMonday[0].Points[0].y;

            Assert.AreEqual(drop * 0.5f, halfDrop, 1e-6f,
                "배율 0.5에서 느슨해지는 양이 절반이 아닙니다 — 이 양이 R 배수가 아니라 " +
                "월드유닛 절대 상수로 굳었다는 뜻입니다.");
            Assert.That(drop / Rig().HeadRadius, Is.GreaterThan(0.02f).And.LessThan(0.30f),
                $"느슨해지는 양이 R의 {drop / Rig().HeadRadius:F3}배입니다 — " +
                "설명문 '월요일마다 조금 느슨해진다'가 읽히는 범위(0.02R~0.30R)를 벗어났습니다.");
        }

        /// <summary>같은 비율의 <b>다른 배율</b> 리그. 값이 R 배수인지 절대 상수인지를 가른다.</summary>
        private static AccessoryShapeBuilder.Rig Scaled(float scale)
        {
            AccessoryShapeBuilder.Rig one = Rig();
            return new AccessoryShapeBuilder.Rig(one.HeadRadius * scale, one.HeadCenterY * scale,
                one.ShoulderY * scale, one.HipY * scale, one.Facing);
        }

        /// <summary>다른 넥타이는 요일에 반응하지 않는다(월요일 처리가 카테고리 전체로 새지 않았는가).</summary>
        [TestCase(AccessoryShapeBuilder.NeckBowTie)]
        [TestCase(AccessoryShapeBuilder.NeckScarf)]
        [TestCase(AccessoryShapeBuilder.NeckBell)]
        public void 줄무늬_타이_외에는_요일에_반응하지_않는다(int item)
        {
            var normal = new System.Collections.Generic.List<AccessoryShapeBuilder.Shape>();
            var monday = new System.Collections.Generic.List<AccessoryShapeBuilder.Shape>();
            AccessoryShapeBuilder.Append(normal, EquipmentSlot.Neck, item, Rig(), mondayLoosened: false);
            AccessoryShapeBuilder.Append(monday, EquipmentSlot.Neck, item, Rig(), mondayLoosened: true);

            Assert.AreEqual(normal.Count, monday.Count);
            for (int i = 0; i < normal.Count; i++)
            {
                for (int p = 0; p < normal[i].Points.Length; p++)
                {
                    Assert.AreEqual(normal[i].Points[p], monday[i].Points[p],
                        $"'{normal[i].Name}'이 요일에 따라 움직입니다 — 월요일 처리는 줄무늬 타이 전용입니다.");
                }
            }
        }

        // ============================================================================
        // 좌우 반전 — 비대칭 요소가 진행 방향을 따라간다
        // ============================================================================

        /// <summary>
        /// ★ 2026-09-01(2차) <b>뜻이 바뀐 검사</b>. 옛 고글 스트랩은 "뒤통수를 도는 반원"이라
        /// 33-2-2 #3이 그것을 facing 반전 회귀의 대상으로 지목했고, 이 검사는 <b>모든 점이 x&lt;0</b>임을
        /// 요구했다.
        ///
        /// <para>그 요구가 결함의 원인이었다. 카드 아이콘(<c>AccessoryCardIcon</c>)은 <b>머리 없이</b>
        /// 도형만 그리므로, "머리를 도는 끈"은 맥락이 사라지면 <b>한쪽에만 붙은 고리</b>가 된다 —
        /// 리더 육안 검증이 "곡선이 왼쪽에만 있어 한쪽으로 쏠린 기형 도형"으로 판정한 그 상태다
        /// (Tasklist V3). 그래서 스트랩을 <b>좌우 대칭</b>으로 다시 그렸고, 이 검사도 그 성질을 잠근다.</para>
        ///
        /// <para>반전 회귀의 대상은 사라지지 않았다 — EYES에는 여전히 <b>앞쪽 눈에만</b> 있는
        /// 외알안경·안대가 있고, <c>EyesVisorOpacityTests.좌우를_반전해도_같은_판이_거울로_선다</c>가
        /// 6종 전부의 x 부호 반전을 점별로 검사한다.</para>
        /// </summary>
        [Test]
        public void 고글_스트랩이_좌우로_똑같이_뻗는다()
        {
            var right = new System.Collections.Generic.List<AccessoryShapeBuilder.Shape>();
            AccessoryShapeBuilder.Append(right, EquipmentSlot.Eyes, AccessoryShapeBuilder.EyesGoggles, Rig(+1f));
            Vector3[] r = Find(right, "GoggleStrap");

            float back = 0f, front = 0f;
            for (int i = 0; i < r.Length; i++)
            {
                back = Mathf.Max(back, -r[i].x);
                front = Mathf.Max(front, r[i].x);
            }
            Assert.AreEqual(back, front, 1e-5f,
                $"스트랩이 뒤로 {back:F4} / 앞으로 {front:F4}만큼 뻗어 한쪽으로 쏠렸습니다 — " +
                "카드에는 머리가 없으므로 비대칭인 끈은 '기형 도형'으로 읽힙니다(리더 육안 검증 V3).");

            float reach = AccessorySilhouetteMetrics.Rig().HeadRadius * AccessoryShapeBuilder.GoggleStrapReachRatio;
            Assert.AreEqual(reach, front, 1e-5f, "스트랩 끝이 선언한 뻗음 상수와 어긋났습니다.");
        }

        /// <summary>왕관은 좌우 대칭이라 방향이 바뀌어도 같은 그림이어야 한다(33-2-1 #4가 명시한 정상 동작).</summary>
        [Test]
        public void 왕관은_좌우_대칭이라_반전해도_같은_그림이다()
        {
            var right = new System.Collections.Generic.List<AccessoryShapeBuilder.Shape>();
            var left = new System.Collections.Generic.List<AccessoryShapeBuilder.Shape>();
            AccessoryShapeBuilder.Append(right, EquipmentSlot.Head, AccessoryShapeBuilder.HeadCrown, Rig(+1f));
            AccessoryShapeBuilder.Append(left, EquipmentSlot.Head, AccessoryShapeBuilder.HeadCrown, Rig(-1f));

            Vector3[] a = Find(right, "CrownBody");
            Vector3[] b = Find(left, "CrownBody");
            for (int i = 0; i < a.Length; i++)
            {
                // 대칭 도형이므로 반전하면 점 순서만 뒤집힌 같은 집합이 된다.
                Assert.AreEqual(a[i].x, -b[i].x, 1e-5f);
                Assert.AreEqual(a[i].y, b[i].y, 1e-5f);
            }
        }

        private static Vector3[] Find(System.Collections.Generic.List<AccessoryShapeBuilder.Shape> shapes, string name)
        {
            for (int i = 0; i < shapes.Count; i++)
            {
                if (shapes[i].Name == name) return shapes[i].Points;
            }
            Assert.Fail($"'{name}' 선을 찾지 못했습니다.");
            return null;
        }
    }
}
