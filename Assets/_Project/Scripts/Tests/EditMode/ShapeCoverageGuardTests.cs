using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ "새 장비를 넣으면 조용히 안 그려진다"의 <b>네거티브 컨트롤</b> — 2026-09-02.
    ///
    /// ============================================================================
    /// 이 파일이 없으면 방어가 작동한다는 증거가 없다
    /// ============================================================================
    /// 결함: 도형을 고르는 <c>switch (itemIndex)</c> 5개에 <c>default:</c>가 하나도 없었다.
    /// 7번째 모자를 <c>.asset</c>으로 넣으면 카드는 뜨는데 몸에는 아무것도 안 그려지고,
    /// <b>에러도 로그도 없었다</b>. 게다가 <c>HatCoverLocalY</c>의 <c>default: +∞</c>가 그 모자를
    /// <b>조용히 왕관 취급</b>(= 아무것도 안 가림)해서 머리카락 클리핑까지 함께 틀어졌다.
    ///
    /// 그래서 이 파일은 <b>존재하지 않는 번호를 실제로 넣어 본다</b>. "default를 추가했다"는 코드
    /// 리뷰로는 그 default가 <b>도달 가능한지</b>도, 로그가 <b>정말 나오는지</b>도 확인되지 않는다.
    ///
    /// ============================================================================
    /// 교정(calibration) — 모든 "없음" 판정에 양성 대조
    /// ============================================================================
    /// <see cref="지금_표에_있는_아이템은_전부_도형이_있다"/>가 <b>알려진 값</b>으로 먼저 교정한다:
    /// 지금 표의 42종을 전부 통과시켰을 때 신고가 <b>0건</b>이어야 한다. 이 교정이 깨지면
    /// (= 정상 아이템도 신고된다) 아래 네거티브 컨트롤의 "잡혔다"는 전부 무의미해진다.
    /// </summary>
    public sealed class ShapeCoverageGuardTests
    {
        /// <summary>표에 없는 자리 번호. <b>숫자를 베끼지 않는다</b> — 표가 7종으로 늘면 이 값도
        /// 자동으로 따라 올라가야 네거티브 컨트롤이 계속 "존재하지 않는 번호"로 남는다.</summary>
        private static int UnknownIndexFor(EquipmentSlot slot) => ItemCatalog.ItemCountIn(slot) + 1;

        /// <summary>몸 도형을 갖는 자리(FX/PET은 원래 없다).</summary>
        private static readonly EquipmentSlot[] Drawable =
        {
            EquipmentSlot.Head, EquipmentSlot.Eyes, EquipmentSlot.Neck,
            EquipmentSlot.Shoulders, EquipmentSlot.Hair,
        };

        private static AccessoryShapeBuilder.Rig Rig(float facing = 1f)
        {
            const float H = StickConfig.BaselineCharacterTotalHeight;
            const float R = AccessoryShapeBuilder.BaselineHeadVisualRadius;
            return new AccessoryShapeBuilder.Rig(R, H - R,
                AccessoryShapeBuilder.BaselineShoulderLocalY, AccessoryShapeBuilder.BaselineHipLocalY, facing);
        }

        private List<AccessoryShapeBuilder.Shape> _sink;

        [SetUp]
        public void SetUp()
        {
            // 중복 억제가 걸려 있으므로 케이스마다 초기화하지 않으면 두 번째 테스트부터 로그가 안 난다.
            ShapeCoverageGuard.ResetForTests();
            StickMateDevTools.SetTestOverride(null);
            _sink = new List<AccessoryShapeBuilder.Shape>(8);
        }

        [TearDown]
        public void TearDown()
        {
            StickMateDevTools.SetTestOverride(null);
            ShapeCoverageGuard.ResetForTests();
        }

        // ============================================================================
        // 0. 교정 — 알려진 값(지금 표 42종)에서는 신고가 0건이어야 한다
        // ============================================================================

        [Test]
        public void 지금_표에_있는_아이템은_전부_도형이_있다()
        {
            for (int s = 0; s < Drawable.Length; s++)
            {
                int count = ItemCatalog.ItemCountIn(Drawable[s]);
                Assert.Greater(count, 0, $"{Drawable[s]} 카테고리가 비어 있으면 이 교정이 공허해집니다.");

                for (int i = 0; i < count; i++)
                {
                    _sink.Clear();
                    AccessoryShapeBuilder.Append(_sink, Drawable[s], i, Rig());
                    Assert.Greater(_sink.Count, 0, $"{Drawable[s]} {i}번이 선을 하나도 만들지 않습니다.");
                    Assert.IsFalse(HasMarker(_sink),
                        $"{Drawable[s]} {i}번이 <b>빠짐 표식</b>을 그렸습니다 — 표에는 있는데 도형이 없습니다.");
                }
            }

            Assert.AreEqual(0, ShapeCoverageGuard.HitCount,
                "지금 표의 아이템만 그렸는데 신고가 났습니다. 이 교정이 깨지면 아래 네거티브 컨트롤의 " +
                $"'잡혔다'는 전부 무효입니다(마지막 신고: {ShapeCoverageGuard.LastMessage}).");
        }

        // ============================================================================
        // 1. 네거티브 컨트롤 — 존재하지 않는 번호를 실제로 넣는다
        // ============================================================================

        [Test]
        public void 알_수_없는_아이템_번호는_로그와_표식_둘_다로_잡힌다(
            [ValueSource(nameof(Drawable))] EquipmentSlot slot)
        {
            int unknown = UnknownIndexFor(slot);
            LogAssert.Expect(LogType.Error, new Regex(@"\[도형\].*몸 도형이"));

            AccessoryShapeBuilder.Append(_sink, slot, unknown, Rig());

            Assert.AreEqual(1, ShapeCoverageGuard.LoggedCount,
                $"{slot} {unknown}번(표에 없는 번호)을 넣었는데 신고가 없습니다 — default:가 도달 " +
                "불가능하거나 조용히 삼켜지고 있습니다.");
            Assert.IsTrue(ShapeCoverageGuard.LastMessage.Contains(unknown.ToString()),
                $"신고 문구에 문제의 번호({unknown})가 없습니다 — 로그만 보고는 어느 아이템인지 모릅니다.");
            Assert.IsTrue(HasMarker(_sink),
                "화면 표식이 없습니다 — 이 결함의 첫 증상이 '화면에서 안 보인다'라 로그만으로는 부족합니다.");
        }

        [Test]
        public void 표식은_배율에_비례한다()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[도형\]"));
            AccessoryShapeBuilder.Append(_sink, EquipmentSlot.Head, UnknownIndexFor(EquipmentSlot.Head), Rig());
            float small = MarkerWidth(_sink);

            ShapeCoverageGuard.ResetForTests();
            _sink.Clear();
            LogAssert.Expect(LogType.Error, new Regex(@"\[도형\]"));
            var big = new AccessoryShapeBuilder.Rig(
                AccessoryShapeBuilder.BaselineHeadVisualRadius * 2f,
                (StickConfig.BaselineCharacterTotalHeight - AccessoryShapeBuilder.BaselineHeadVisualRadius) * 2f,
                AccessoryShapeBuilder.BaselineShoulderLocalY * 2f,
                AccessoryShapeBuilder.BaselineHipLocalY * 2f, 1f);
            AccessoryShapeBuilder.Append(_sink, EquipmentSlot.Head, UnknownIndexFor(EquipmentSlot.Head), big);

            Assert.AreEqual(small * 2f, MarkerWidth(_sink), small * 1e-3f,
                "표식이 월드 유닛 상수로 그려졌습니다 — 이 파일의 규약은 '절대 상수 없음'입니다.");
        }

        [Test]
        public void 같은_자리는_한_번만_찍는다()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[도형\]"));

            for (int i = 0; i < 5; i++)
            {
                _sink.Clear();
                AccessoryShapeBuilder.Append(_sink, EquipmentSlot.Head,
                    UnknownIndexFor(EquipmentSlot.Head), Rig());
            }

            Assert.AreEqual(5, ShapeCoverageGuard.HitCount, "다섯 번 다 적발되어야 합니다.");
            Assert.AreEqual(1, ShapeCoverageGuard.LoggedCount,
                "24시간 상주 앱에서 같은 사고를 매 재구성마다 찍으면 로그가 자기 자신으로 덮입니다.");
        }

        [Test]
        public void 출하_구성에서는_표식을_그리지_않되_로그는_남긴다()
        {
            StickMateDevTools.SetTestOverride(false);
            LogAssert.Expect(LogType.Error, new Regex(@"\[도형\]"));

            AccessoryShapeBuilder.Append(_sink, EquipmentSlot.Neck,
                UnknownIndexFor(EquipmentSlot.Neck), Rig());

            Assert.AreEqual(1, ShapeCoverageGuard.LoggedCount,
                "릴리스에서도 Player.log에는 남아야 합니다 — 이 팀의 확인 절차가 릴리스 빌드 + Player.log입니다.");
            Assert.AreEqual(0, _sink.Count,
                "출하된 앱의 사용자 캐릭터에 '빠짐 표식'이 24시간 붙어 있으면 결함보다 나쁩니다.");
        }

        // ============================================================================
        // 2. 자리(슬롯) 분배 — FX/PET은 <b>정상</b>이고 모르는 자리만 결함이다
        // ============================================================================

        [TestCase(EquipmentSlot.Fx)]
        [TestCase(EquipmentSlot.Pet)]
        public void 몸_도형이_없는_자리는_신고하지_않는다(EquipmentSlot slot)
        {
            AccessoryShapeBuilder.Append(_sink, slot, 0, Rig());

            Assert.AreEqual(0, _sink.Count, $"{slot}은 몸에 붙는 도형이 없습니다(AppearanceShapeBuilder 소관).");
            Assert.AreEqual(0, ShapeCoverageGuard.HitCount,
                $"{slot}은 <b>정상 경로</b>입니다 — 여기서 신고가 나면 착용자 전원이 매 재구성마다 " +
                "거짓 경보를 찍어 진짜 사고를 가립니다.");
        }

        [Test]
        public void 알_수_없는_자리는_신고된다()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[도형\].*Append가 모릅니다"));

            AccessoryShapeBuilder.Append(_sink, (EquipmentSlot)(EquipmentModel.SlotCount + 3), 0, Rig());

            Assert.AreEqual(1, ShapeCoverageGuard.LoggedCount,
                "EquipmentSlot에 값이 늘었는데 분배 switch가 안 따라오면 그 카테고리 전체가 조용히 사라집니다.");
        }

        // ============================================================================
        // 3. 모자 커버선 — 왕관/미착용(의도된 +∞)과 알 수 없는 번호를 <b>구분</b>한다
        // ============================================================================

        [Test]
        public void 왕관과_미착용은_아무것도_가리지_않지만_신고되지_않는다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();

            Assert.AreEqual(AccessoryShapeBuilder.NothingCovered,
                AccessoryShapeBuilder.HatCoverLocalY(AccessoryShapeBuilder.HeadCrown, rig),
                "왕관은 얹는 물건이라 밑이 뚫려 있습니다 — 이 면제는 if가 아니라 <b>이 표의 값</b>입니다.");
            Assert.AreEqual(AccessoryShapeBuilder.NothingCovered,
                AccessoryShapeBuilder.HatCoverLocalY(EquipmentModel.NotWorn, rig),
                "모자를 안 썼으면 가릴 것도 없습니다.");

            Assert.AreEqual(0, ShapeCoverageGuard.HitCount,
                "왕관과 미착용은 <b>의도된</b> +∞입니다 — 이들이 신고되면 정상 사용이 매번 빨간불이 됩니다.");
        }

        [Test]
        public void 알_수_없는_모자는_왕관과_구분되어_신고된다()
        {
            int unknown = UnknownIndexFor(EquipmentSlot.Head);
            LogAssert.Expect(LogType.Error, new Regex(@"\[도형\].*커버선"));

            float cover = AccessoryShapeBuilder.HatCoverLocalY(unknown, Rig());

            Assert.AreEqual(AccessoryShapeBuilder.NothingCovered, cover,
                "모르는 모자 밑에서 머리카락을 자르면 머리카락까지 함께 사라져 원인이 두 겹이 됩니다 — " +
                "값 자체는 +∞가 맞습니다.");
            Assert.AreEqual(1, ShapeCoverageGuard.LoggedCount,
                "옛 코드는 왕관·미착용·알 수 없는 번호 셋을 <c>default: +∞</c> 하나로 뭉뚱그려서, " +
                "7번째 모자가 조용히 왕관 취급을 받았습니다(머리카락 클리핑이 함께 틀어짐).");
        }

        // ============================================================================
        // 4. 카드 폴백 — 도형이 없어도 카드가 비지 않는다는 기존 계약이 살아 있는가
        // ============================================================================

        [Test]
        public void 알_수_없는_아이템이어도_카탈로그_표는_그대로다()
        {
            // 이 결함의 증상 절반이 "카드는 뜨는데 몸에만 안 나온다"였다. 카드 쪽 폴백은 <b>고치는
            // 대상이 아니라 지켜야 할 계약</b>이라, 도형 신고가 카탈로그를 건드리지 않는지 확인한다.
            LogAssert.Expect(LogType.Error, new Regex(@"\[도형\]"));
            int before = ItemCatalog.ItemCountIn(EquipmentSlot.Head);

            AccessoryShapeBuilder.Append(_sink, EquipmentSlot.Head,
                UnknownIndexFor(EquipmentSlot.Head), Rig());

            Assert.AreEqual(before, ItemCatalog.ItemCountIn(EquipmentSlot.Head));
            Assert.IsNull(ItemCatalog.Item(EquipmentSlot.Head, UnknownIndexFor(EquipmentSlot.Head)),
                "표에 없는 번호가 표에서 조회되면 이 테스트의 전제가 무너집니다.");
        }

        // ==================== 도우미 ====================

        private static bool HasMarker(List<AccessoryShapeBuilder.Shape> sink)
        {
            for (int i = 0; i < sink.Count; i++)
            {
                if (sink[i].Name == "MissingBox") return true;
            }
            return false;
        }

        private static float MarkerWidth(List<AccessoryShapeBuilder.Shape> sink)
        {
            for (int i = 0; i < sink.Count; i++)
            {
                if (sink[i].Name != "MissingBox") continue;
                Vector3[] p = sink[i].Points;
                float min = float.MaxValue, max = float.MinValue;
                for (int k = 0; k < p.Length; k++)
                {
                    min = Mathf.Min(min, p[k].x);
                    max = Mathf.Max(max, p[k].x);
                }
                return max - min;
            }
            Assert.Fail("표식이 없어 폭을 잴 수 없습니다.");
            return 0f;
        }
    }
}
