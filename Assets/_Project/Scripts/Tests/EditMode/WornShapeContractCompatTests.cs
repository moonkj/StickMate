using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Interaction;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 형상 데이터 계약 v2의 <b>하위 호환</b> + 역할(Tone) 도메인 단일화 + 인계본 색 규칙 (2026-09-05).
    ///
    /// <para><see cref="StickPackManifestSO.SchemaVersion"/>이 2가 되면서 걸리는 의무(CLAUDE.md): v1 데이터(새 키 없음)를
    /// 읽었을 때 신규 필드가 <b>안전한 기본값</b>으로 채워지고 그 기본값의 <b>뜻이 v1과 같은가</b>.
    /// 여기서 「안전」은 「0」이 아니라 「예전처럼 몸과 카드 양쪽에, 옛 획 규칙으로 그려진다」다.</para>
    ///
    /// <para>그리고 §15-2 #3의 병 — <c>Shape.Tone</c>(0/1/2)과 <c>ItemIconPart.Tone</c>(0/1)이 갈라져 한쪽만 늘리면
    /// 폴백이 <b>조용히</b> 틀린 색을 칠하는 것 — 을 <see cref="AccessoryTone.Resolve"/> 한 표로 닫았는지 본다.</para>
    /// </summary>
    public sealed class WornShapeContractCompatTests
    {
        // ============================================================================
        // 1. v1 데이터 — 키가 없으면 기본값이고, 그 뜻은 v1 그대로
        // ============================================================================

        [Test]
        public void v1_스트림에_새_키가_없으면_기본값이_들어오고_그_뜻은_v1_그대로다()
        {
            // JsonUtility 는 Unity 직렬화기와 같은 규칙(키가 없으면 C# 기본값)으로 채운다.
            const string v1 = "{\"name\":\"Old\",\"loop\":true,\"filled\":true,\"tone\":1,\"swayStart\":-1,\"swayCount\":0," +
                              "\"swingDegrees\":0,\"terms\":[1,1,0,0,0,1,0.5,1,4,0,0,0]}";
            AccessoryWornShapeData d = JsonUtility.FromJson<AccessoryWornShapeData>(v1);

            Assert.AreEqual("Old", d.name);
            Assert.AreEqual(AccessorySurfaces.Unset, d.surfaces, "v1 데이터의 표면은 미설정(0)이어야 합니다.");
            Assert.AreEqual(0f, d.strokeMult); Assert.AreEqual(0f, d.strokeInR); Assert.IsFalse(d.noStroke);
            Assert.AreEqual(0f, d.alpha); Assert.AreEqual(0f, d.lineAlpha);
            Assert.AreEqual(0, d.underBack); Assert.AreEqual((int)AccessoryPieceLayer.Slot, d.layer, "v1 조각의 층은 슬롯 기본이어야 합니다.");

            Assert.IsTrue(AccessoryWornShapeReader.Validate(d, out string error), error);
            Assert.AreEqual(AccessorySurfaces.Default, AccessorySurfaces.Effective((byte)d.surfaces),
                "미설정(0)은 Body|Card 로 읽혀야 합니다 — 「어디에도 안 나옴」이면 구 에셋 전부가 화면에서 사라집니다(§15-6 #1).");
            Assert.IsFalse(AccessoryStroke.IsHandoff(d.strokeInR), "v1 조각은 인계본 규칙(1pt 하한·등급색)을 타면 안 됩니다.");
            Assert.AreEqual(1f, AccessoryStroke.Multiplier(d.strokeMult), "획 배수 미설정은 ×1 입니다.");
        }

        [Test]
        public void v1_에셋_펜던트와_반다나는_몸과_카드에_같은_조각으로_나온다()
        {
            foreach (int item in new[] { AccessoryShapeBuilder.NeckPendant, AccessoryShapeBuilder.NeckBandana })
            {
                AccessoryWornShapeData[] data = ItemCatalog.WornShapes(EquipmentSlot.Neck, item);
                Assert.IsNotNull(data, $"NECK {item}: 형상 데이터가 없습니다.");
                foreach (AccessoryWornShapeData d in data)
                {
                    Assert.AreEqual(AccessorySurfaces.Unset, d.surfaces, $"NECK {item} '{d.name}': v1 에셋인데 surfaces 가 {d.surfaces}입니다.");
                    Assert.AreEqual(0f, d.strokeInR, $"NECK {item} '{d.name}': v1 에셋인데 명목 폭이 있습니다.");
                }
                // ★ R20 N-1(2026-09-05) — v1 에셋도 아이템 단위 <b>세로 오프셋</b>(착용선 dy)은 가질 수 있다. 배율·세로 압축·반전은 여전히 없다.
                AccessoryWornTransform xf = ItemCatalog.WornTransform(EquipmentSlot.Neck, item);
                Assert.AreEqual(0f, xf.Scale, $"NECK {item}: v1 에셋에 배율이 있습니다.");
                Assert.AreEqual(0f, xf.ScaleY, $"NECK {item}: v1 에셋에 세로 압축이 있습니다.");
                Assert.IsFalse(xf.MirrorX, $"NECK {item}: v1 에셋에 반전이 있습니다.");

                AccessoryShapeBuilder.Rig rig = AccessoryCardIcon.CardRig();
                var body = new List<AccessoryShapeBuilder.Shape>();
                var card = new List<AccessoryShapeBuilder.Shape>();
                AccessoryShapeBuilder.Append(body, EquipmentSlot.Neck, item, rig);
                AccessoryShapeBuilder.Append(card, EquipmentSlot.Neck, item, rig, float.PositiveInfinity, 0f, false, AccessorySurface.Card);
                Assert.AreEqual(data.Length, body.Count, $"NECK {item}: 몸 조각 수가 에셋과 다릅니다.");
                Assert.AreEqual(body.Count, card.Count, $"NECK {item}: v1 에셋은 카드에도 같은 조각이 나와야 합니다.");
                float dy = xf.OffsetYInR * rig.HeadRadius;   // 카드는 착용선 오프셋을 모른다 — 몸만 그만큼 내려간다
                for (int i = 0; i < body.Count; i++)
                {
                    Assert.AreEqual(body[i].Name, card[i].Name);
                    Assert.AreEqual(card[i].Points.Length, body[i].Points.Length);
                    for (int k = 0; k < body[i].Points.Length; k++)
                    {
                        Assert.AreEqual(card[i].Points[k].x, body[i].Points[k].x, 1e-6f, $"NECK {item} '{body[i].Name}' {k}번 점 x 가 표면에 따라 다릅니다.");
                        Assert.AreEqual(card[i].Points[k].y + dy, body[i].Points[k].y, 1e-5f, $"NECK {item} '{body[i].Name}' {k}번 점 y: 몸 = 카드 + 착용선 오프셋이어야 합니다.");
                    }
                }
            }
        }

        [Test]
        public void v2_에셋_나비넥타이는_전부_인계본_조각이고_한_벌이다()
        {
            AccessoryWornShapeData[] data = ItemCatalog.WornShapes(EquipmentSlot.Neck, AccessoryShapeBuilder.NeckBowTie);
            Assert.IsNotNull(data);
            Assert.Greater(data.Length, 0);
            foreach (AccessoryWornShapeData d in data)
            {
                Assert.IsTrue(AccessoryStroke.IsHandoff(d.strokeInR), $"'{d.name}': 인계본 조각이 아닙니다.");
                Assert.AreEqual(AccessorySurfaces.Unset, d.surfaces, $"'{d.name}': 한 벌 조각은 미설정(0)이어야 합니다.");
                Assert.Greater(d.strokeMult, 0f);
            }
        }

        // ============================================================================
        // 2. 역할 도메인 — Shape · ItemIconPart · 렌더러가 같은 표를 본다
        // ============================================================================

        [Test]
        public void 폴백_아이콘의_역할_색은_몸의_v1_역할_색과_같은_표에서_나온다()
        {
            var primary = new Color(0.2f, 0.4f, 0.6f, 1f);
            var secondary = new Color(0.9f, 0.5f, 0.1f, 1f);
            for (byte tone = 0; tone < AccessoryTone.Count; tone++)
            {
                var part = new ItemIconPart(ItemIconPartKind.Polyline, new float[] { 0f, 0f, 1f, 1f }, Color.white, tone);
                Color viaIcon = part.WithPalette(primary, secondary).Color;
                var shapes = new List<AccessoryShapeBuilder.Shape>
                {
                    new AccessoryShapeBuilder.Shape("t", new[] { Vector3.zero, Vector3.one }, false, 0, tone: tone),
                };
                Color viaBody = AccessoryShapeBuilder.ResolveToneColor(shapes, 0, 0, primary, secondary);
                AssertColor(viaBody, viaIcon, $"역할 {tone}");
            }

            // 갈라져 있던 그 자리: 그늘(2)은 보조색이 <b>아니다</b>.
            var shade = new ItemIconPart(ItemIconPartKind.Polyline, new float[] { 0f, 0f, 1f, 1f }, Color.white, AccessoryTone.Shade);
            AssertColor(AccessoryTone.Shaded(primary), shade.WithPalette(primary, secondary).Color, "그늘");
            Assert.AreNotEqual(secondary, shade.WithPalette(primary, secondary).Color, "그늘 조각이 보조색으로 칠해졌습니다(옛 0/1 도메인의 병).");
        }

        [Test]
        public void v1_하이라이트는_밑_조각의_역할_위에_합성된다()
        {
            var primary = new Color(0.2f, 0.4f, 0.6f, 1f);
            var secondary = new Color(0.9f, 0.5f, 0.1f, 1f);
            Vector3[] pts = { Vector3.zero, Vector3.one };
            var shapes = new List<AccessoryShapeBuilder.Shape>
            {
                new AccessoryShapeBuilder.Shape("wrap", pts, true, 0, tone: AccessoryTone.Accent, filled: true),
                new AccessoryShapeBuilder.Shape("hl-on-accent", pts, false, 0, tone: AccessoryTone.Highlight, underBack: 1),
                new AccessoryShapeBuilder.Shape("hl-on-nothing", pts, false, 0, tone: AccessoryTone.Highlight, underBack: 0),
            };
            AssertColor(AccessoryTone.Highlighted(secondary), AccessoryShapeBuilder.ResolveToneColor(shapes, 1, 0, primary, secondary), "보조색 위");
            AssertColor(AccessoryTone.Highlighted(primary), AccessoryShapeBuilder.ResolveToneColor(shapes, 2, 0, primary, secondary), "밑 없음");
            AssertColor(AccessoryTone.Highlighted(primary), AccessoryShapeBuilder.ResolveToneColor(shapes, 1, 1, primary, secondary), "아이템 경계 밖");
        }

        // ============================================================================
        // 3. 인계본 조각의 색·알파 규칙 (§14-5 · §14-6)
        // ============================================================================

        /// <summary>재질 팔레트 §2-3(2026-09-05): 채움 0/1/2 = M/M2/그늘 불투명 · 채움 있는 조각의 윤곽 = 잉크 · 채움 없는 선 0/1 = 재질선 ·
        /// 잉크 역할 선 = 잉크 · 하이라이트 = 흰 · 그룹 알파는 곱해진다(기본 16종은 1.0). 고정색 역할은 없다(I-23 제거 — 임의색 통로 차단).</summary>
        [Test]
        public void 인계본_조각의_몸_색은_재질색_불투명_잉크_윤곽_재질선_흰이고_그룹알파가_곱해진다()
        {
            var ink = new Color(0.07f, 0.07f, 0.07f, 1f);
            var m = new Color(0.59f, 0.51f, 0.31f, 1f);
            var m2 = new Color(0.80f, 0.33f, 0.07f, 1f);
            var palette = new AccessoryShapeBuilder.HandoffPalette(ink, 0.4f, ink, Color.white, m, m2);
            Vector3[] pts = { Vector3.zero, Vector3.one, Vector3.right };

            var fillM = new AccessoryShapeBuilder.Shape("fill", pts, true, 0, tone: AccessoryTone.Primary, filled: true,
                strokeInR: 0.12f, alpha: 1f, lineAlpha: 1f);
            AccessoryShapeBuilder.ResolveHandoffBody(fillM, palette, out Color f, out bool hasF, out Color l, out bool hasL);
            Assert.IsTrue(hasF); Assert.IsTrue(hasL);
            AssertRgb(m, f, "채움 0 = 재질색 M"); Assert.AreEqual(1f * 0.4f, f.a, 1e-6f, "채움 알파 × 그룹 알파");
            AssertRgb(ink, l, "채움 있는 조각의 윤곽 = 잉크(브라스 아님)"); Assert.AreEqual(1f * 0.4f, l.a, 1e-6f, "선 알파 × 그룹 알파");

            var fillM2 = new AccessoryShapeBuilder.Shape("f2", pts, true, 0, tone: AccessoryTone.Accent, filled: true, strokeInR: 0.12f, alpha: 1f);
            AssertRgb(m2, AccessoryShapeBuilder.HandoffFillBase(fillM2, palette), "채움 1 = 보조 재질색 M2");
            var shade = new AccessoryShapeBuilder.Shape("sh", pts, true, 0, tone: AccessoryTone.Shade, filled: true, strokeInR: 0.12f, alpha: 1f, noStroke: true);
            AssertRgb(AccessoryTone.Shaded(m), AccessoryShapeBuilder.HandoffFillBase(shade, palette), "채움 2 = 그늘(M × 0.28)");

            var lineM = new AccessoryShapeBuilder.Shape("s", pts, false, 0, tone: AccessoryTone.Primary, strokeInR: 0.09f);
            var lineM2 = new AccessoryShapeBuilder.Shape("s2", pts, false, 0, tone: AccessoryTone.Accent, strokeInR: 0.09f);
            var lineInk = new AccessoryShapeBuilder.Shape("s3", pts, false, 0, tone: AccessoryTone.HeadInk, strokeInR: 0.09f, lineAlpha: 0.55f);
            AssertRgb(m, AccessoryShapeBuilder.HandoffLineBase(lineM, palette), "채움 없는 선 0 = 재질선 M(안경 다리·목줄)");
            AssertRgb(m2, AccessoryShapeBuilder.HandoffLineBase(lineM2, palette), "채움 없는 선 1 = 재질선 M2(술·코다리)");
            AccessoryShapeBuilder.ResolveHandoffBody(lineInk, palette, out _, out hasF, out l, out hasL);
            Assert.IsFalse(hasF); Assert.IsTrue(hasL);
            AssertRgb(ink, l, "잉크 역할 선 = 잉크(채움 위 결·주름)"); Assert.AreEqual(0.55f * 0.4f, l.a, 1e-6f, "인계본 선 알파 유지(R-4)");

            // 카드 전용 워시(투명 렌즈)의 알파는 표면에 상관없이 조각 값 그대로다 — 표면별 덮어쓰기 필드는 없다(2026-09-05 제거).
            var wash = new AccessoryShapeBuilder.Shape("wash", pts, true, 0, tone: AccessoryTone.Accent, filled: true,
                surfaces: (byte)AccessorySurface.Card, strokeInR: 0.12f, alpha: 0.2f, noStroke: true);
            Assert.AreEqual(0.2f, AccessoryShapeBuilder.HandoffFillAlpha(wash, body: false), 1e-6f, "카드 워시 알파");
            Assert.AreEqual(0.2f, AccessoryShapeBuilder.HandoffFillAlpha(wash, body: true), 1e-6f, "표면별 덮어쓰기는 없다 — 같은 값");

            var hl = new AccessoryShapeBuilder.Shape("hl", pts, false, 0, tone: AccessoryTone.Highlight, strokeInR: 0.09f, lineAlpha: 0.42f);
            AccessoryShapeBuilder.ResolveHandoffBody(hl, palette, out _, out hasF, out l, out hasL);
            Assert.IsFalse(hasF); Assert.IsTrue(hasL);
            AssertRgb(Color.white, l, "하이라이트 = 흰(틴트 우회)"); Assert.AreEqual(0.42f * 0.4f, l.a, 1e-6f);

            var noStroke = new AccessoryShapeBuilder.Shape("cf", pts, true, 0, tone: AccessoryTone.Accent, filled: true,
                strokeInR: 0.12f, alpha: 1f, noStroke: true);
            AccessoryShapeBuilder.ResolveHandoffBody(noStroke, palette, out _, out _, out _, out hasL);
            Assert.IsFalse(hasL, "F/CF 는 윤곽이 없습니다.");
        }

        /// <summary>
        /// ★ 재질색의 <b>원천은 하나</b>(사용자 확정 「카드와 몸은 같은 hex」, 팔레트 §2): 카드 팔레트와 몸 팔레트가 같은 아이템에서
        /// 같은 M/M2 를 받고, 그 값은 카탈로그 주색/보조색(.asset icon tone 0/1)이다. 몸은 <c>WornColor</c> 틴트를 타지 않는다
        /// — 재질색 25색은 대역 안이라 항등이기도 하지만(팔레트 §6-(5)), 여기서는 <b>틴트가 있었다면 갈라졌을</b> 색(잉크 표식)으로 그 우회를 잰다.
        /// </summary>
        [Test]
        public void 재질색은_카드와_몸이_카탈로그_한_원천에서_같은_값을_받고_몸은_틴트를_타지_않는다()
        {
            var ink = new Color(0.07f, 0.07f, 0.07f, 1f);
            int checkedItems = 0;
            foreach (EquipmentSlot slot in new[] { EquipmentSlot.Head, EquipmentSlot.Eyes, EquipmentSlot.Neck, EquipmentSlot.Shoulders })
            {
                for (int i = 0; i < ItemCatalog.ItemCountIn(slot); i++)
                {
                    ItemCatalogEntry e = ItemCatalog.Item(slot, i);
                    AccessoryShapeBuilder.HandoffPalette body = AccessoryHandoffPalette.Body(slot, i, ink);
                    AccessoryShapeBuilder.HandoffPalette card = AccessoryHandoffPalette.Card(slot, i, e.PrimaryColor, e.SecondaryColor);
                    AssertRgb(e.PrimaryColor, body.Material, $"{e.Id} 몸 M = 카탈로그 주색");
                    AssertRgb(e.SecondaryColor, body.Material2, $"{e.Id} 몸 M2 = 카탈로그 보조색");
                    AssertRgb(body.Material, card.Material, $"{e.Id} 카드 M = 몸 M");
                    AssertRgb(body.Material2, card.Material2, $"{e.Id} 카드 M2 = 몸 M2");
                    AssertRgb(ink, body.Ink, $"{e.Id} 몸 잉크 = 유저 잉크");
                    checkedItems++;
                }
            }
            Assert.Greater(checkedItems, 0);

            // 틴트 우회 — WornColor 가 바꾸는 색(잉크 표식 InkTone)을 재질색 자리에 넣어도 팔레트는 그대로 낸다.
            Color mark = ItemCatalog.InkTone;
            Assert.IsFalse(Same(mark, ItemCatalog.WornColor(mark, ink)), "이 대조는 WornColor 가 실제로 바꾸는 색이어야 뜻이 있습니다.");
            var direct = new AccessoryShapeBuilder.HandoffPalette(ink, 0f, ink, Color.white, mark, mark);
            AssertRgb(mark, direct.Material, "재질색은 틴트 없이 그대로");
        }

        private static bool Same(Color a, Color b)
            => Mathf.Abs(a.r - b.r) < 1e-3f && Mathf.Abs(a.g - b.g) < 1e-3f && Mathf.Abs(a.b - b.b) < 1e-3f;

        /// <summary>R20 유리 판 — 바탕(몸 잉크 / 카드 바탕) 위 M2 를 조각 alpha 로 미리 합성한 <b>불투명</b> 채움. 카드의 옛 워시(바탕 위 M2 α 사전 합성)와
        /// 보이는 값이 같고, 몸에서는 머리가 균일 잉크라 「투명 워시」와 같은 그림이면서 가림 판정에는 채움으로 잡힌다.</summary>
        [Test]
        public void 유리_판은_바탕_위_M2를_alpha로_합성한_불투명_채움이다()
        {
            var ink = new Color(0.07f, 0.07f, 0.07f, 1f);
            var m = new Color(0.59f, 0.51f, 0.31f, 1f);
            var m2 = new Color(0.35f, 0.45f, 0.60f, 1f);
            var bg = new Color(0.08f, 0.09f, 0.12f, 1f);
            Vector3[] pts = { Vector3.zero, Vector3.one, Vector3.right };
            var glass = new AccessoryShapeBuilder.Shape("g", pts, true, 0, tone: AccessoryTone.Glass, filled: true, strokeInR: 0.12f, alpha: 0.2f, noStroke: true);

            var body = new AccessoryShapeBuilder.HandoffPalette(ink, 0f, ink, Color.white, m, m2);
            var card = new AccessoryShapeBuilder.HandoffPalette(Color.white, 0f, bg, Color.white, m, m2);
            AssertRgb(Color.Lerp(ink, m2, 0.2f), AccessoryShapeBuilder.HandoffFillBase(glass, body), "몸: 잉크 위 M2 α");
            AssertRgb(Color.Lerp(bg, m2, 0.2f), AccessoryShapeBuilder.HandoffFillBase(glass, card), "카드: 바탕 위 M2 α(옛 워시와 같은 값)");
            Assert.AreEqual(1f, AccessoryShapeBuilder.HandoffFillAlpha(glass, body: true), 1e-6f, "유리 판은 불투명이다");
            Assert.AreEqual(1f, AccessoryShapeBuilder.HandoffFillAlpha(glass, body: false), 1e-6f, "카드에서도 불투명(사전 합성 결과)");
            AccessoryShapeBuilder.ResolveHandoffBody(glass, body, out Color f, out bool hasF, out _, out bool hasL);
            Assert.IsTrue(hasF); Assert.IsFalse(hasL, "유리 판은 선이 없다 — 테 조각이 따로 그린다");
            Assert.AreEqual(1f, f.a, 1e-6f);
        }

        [Test]
        public void 미설정_채움_알파는_그라디언트_평균이고_선_알파는_1이다()
        {
            Vector3[] pts = { Vector3.zero, Vector3.one, Vector3.right };
            var s = new AccessoryShapeBuilder.Shape("x", pts, true, 0, tone: AccessoryTone.Accent, filled: true, strokeInR: 0.1f);
            Assert.AreEqual(AccessoryCardWash.GradientMeanAlpha, AccessoryShapeBuilder.HandoffFillAlpha(s, body: true), 1e-6f);
            Assert.AreEqual(1f, AccessoryShapeBuilder.HandoffLineAlpha(s), 1e-6f);
        }

        private static void AssertColor(Color expected, Color actual, string where)
        {
            Assert.AreEqual(expected.r, actual.r, 1e-5f, $"{where} R");
            Assert.AreEqual(expected.g, actual.g, 1e-5f, $"{where} G");
            Assert.AreEqual(expected.b, actual.b, 1e-5f, $"{where} B");
            Assert.AreEqual(expected.a, actual.a, 1e-5f, $"{where} A");
        }

        private static void AssertRgb(Color expected, Color actual, string where)
        {
            Assert.AreEqual(expected.r, actual.r, 1e-5f, $"{where} R");
            Assert.AreEqual(expected.g, actual.g, 1e-5f, $"{where} G");
            Assert.AreEqual(expected.b, actual.b, 1e-5f, $"{where} B");
        }
    }
}
