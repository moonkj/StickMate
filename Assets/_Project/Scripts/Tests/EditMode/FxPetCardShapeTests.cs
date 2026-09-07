using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ (다)군 FX/PET 12종의 <b>카드 아이콘</b>이 설계 좌표 그대로 화면에 놓이는가
    /// (docs/EQUIPMENT_HANDOFF_PORT_SPEC.md §14-12-3 · §14-12-5 #23/#26/#27, 2026-09-07).
    ///
    /// ============================================================================
    /// 왜 이 검사가 필요한가
    /// ============================================================================
    /// 사용자 신고 <i>"펫들도 아직 업데이트가 안되어 있는데"</i>. 42종 중 이 12종만 옛 40u 폴백에
    /// 남아 <b>채움 0개 · 잉크 윤곽 없음 · 하이라이트 없음 · 획 +23.6%</b>였다. 이 라운드가 §14-12-3 의
    /// 64u 아이콘을 굽고 카드 프레임을 열었는데, 이 계통에는 <b>인계본 슬롯 박스가 없다</b> —
    /// 즉 R 로 되돌리는 배율을 코드가 스스로 정한다. 그 배율이 조용히 틀어지면 카드는 <b>여전히 그려지므로</b>
    /// 아무도 신고하지 않는다(이 저장소가 반복해 당한 형태). 그래서 대조하는 것은 좌표만이 아니라
    /// <b>프레이밍 그 자체</b>다: 골든은 <b>64u 아이콘 단위</b>로 적혀 있고, 이 검사는 프로덕션의 R 좌표를
    /// <see cref="AccessoryCardIcon.Frame.TryGetCardFrame"/>으로 되돌려 거기에 맞춘다.
    ///
    /// ============================================================================
    /// 월드(착용 모습)와의 계약
    /// ============================================================================
    /// §14-12-5 #23 은 "몸 효과·펫은 현행 <c>AppearanceShapeBuilder</c> 그대로 — 카드만 갈라진다"이다.
    /// 그래서 이 파일은 <b>몸에 한 조각도 새지 않는다</b>는 것을 먼저 잠근다. 다만 2026-09-06 R22 가
    /// 월드에서 확정한 <b>색·위상</b> 두 건(반짝임 오목비 · 커서친구 머리/꼬리 2색)은 카드가 따라가야
    /// 하고, 그 둘은 <b>월드 상수·월드 도형에서 직접 재서</b> 대조한다 — 숫자를 여기 베끼지 않는다.
    /// </summary>
    public sealed class FxPetCardShapeTests
    {
        private const string GoldenPath =
            "Assets/_Project/Scripts/Tests/EditMode/Golden/FxPetCardGolden.txt";

        /// <summary>골든을 만든 명령. 실패 메시지에 그대로 실어 다음 사람이 헤매지 않게 한다.</summary>
        private const string Regen = "python3 Tools/CardShapeGen/gen_fxpet_card_shapes.py";

        private sealed class GoldenPiece
        {
            public EquipmentSlot Slot;
            public int Item, Index, Tone, UnderBack;
            public string Name;
            public bool Loop, Filled, NoStroke;
            public float StrokeMult, LineAlpha;
            public Vector2[] Points;      // 64u 아이콘 단위 · y 아래
        }

        private static List<GoldenPiece> ReadGolden(out int totalItems)
        {
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, GoldenPath);
            Assert.IsTrue(File.Exists(path), $"골든이 없습니다: {GoldenPath} ({Regen})");
            var inv = CultureInfo.InvariantCulture;
            var pieces = new List<GoldenPiece>();
            int total = -1;
            totalItems = -1;
            foreach (string raw in File.ReadAllText(path).Replace("\r\n", "\n").Split('\n'))
            {
                if (raw.Length == 0 || raw[0] == '#') continue;
                string[] c = raw.Split('\t');
                switch (c[0])
                {
                    case "PIECE":
                    {
                        var p = new GoldenPiece
                        {
                            Slot = (EquipmentSlot)int.Parse(c[1], inv), Item = int.Parse(c[2], inv),
                            Index = int.Parse(c[3], inv), Name = c[4], Loop = c[5] == "1", Filled = c[6] == "1",
                            Tone = int.Parse(c[7], inv), StrokeMult = float.Parse(c[8], inv), NoStroke = c[9] == "1",
                            LineAlpha = float.Parse(c[10], inv), UnderBack = int.Parse(c[11], inv),
                        };
                        int n = int.Parse(c[12], inv);
                        Assert.AreEqual(n, c.Length - 13, $"골든 {p.Name}: 점 수 {n}인데 좌표 칸이 {c.Length - 13}개입니다.");
                        p.Points = new Vector2[n];
                        for (int i = 0; i < n; i++)
                        {
                            string[] xy = c[13 + i].Split(',');
                            p.Points[i] = new Vector2(float.Parse(xy[0], inv), float.Parse(xy[1], inv));
                        }
                        pieces.Add(p);
                        break;
                    }
                    case "TOTAL":
                        total = int.Parse(c[1], inv);
                        totalItems = int.Parse(c[2], inv);
                        break;
                    // 조용히 건너뛰면 골든의 절반이 안 읽힌 채 초록이 된다.
                    default:
                        Assert.Fail($"골든에 모르는 줄 종류 '{c[0]}' — 생성기가 새 줄을 냈으면 판독기도 함께 갱신하십시오.");
                        break;
                }
            }
            Assert.Greater(pieces.Count, 0, "골든에 PIECE 줄이 없습니다 — 아래 대조가 공허합니다.");
            Assert.AreEqual(total, pieces.Count, "골든의 TOTAL 과 PIECE 줄 수가 다릅니다.");
            return pieces;
        }

        private static List<AccessoryShapeBuilder.Shape> Build(EquipmentSlot slot, int item, AccessorySurface surface)
        {
            var sink = new List<AccessoryShapeBuilder.Shape>();
            AccessoryShapeBuilder.Append(sink, slot, item, AccessoryCardIcon.CardRig(),
                float.PositiveInfinity, 0f, false, surface);
            return sink;
        }

        /// <summary>조각의 로컬 점 하나 → 64u 아이콘 좌표(y 아래). <b>프로덕션 프레임</b>으로 되돌린다 —
        /// 그래야 이 대조가 좌표뿐 아니라 프레이밍까지 잡는다.</summary>
        private static Vector2 ToIcon(Vector3 local, in AccessoryShapeBuilder.Rig rig,
            float unitsPerR, float centerYInR)
        {
            float xR = local.x / rig.HeadRadius;
            float yR = (local.y - rig.HeadCenterY) / rig.HeadRadius;
            // AccessoryCardIcon.BuildFramed 가 화면에 놓는 식의 역함수(상자 중심 = 32, 32).
            return new Vector2(xR * unitsPerR + AccessoryCardIcon.Frame.IconViewBox * 0.5f,
                AccessoryCardIcon.Frame.IconViewBox * 0.5f - (yR - centerYInR) * unitsPerR);
        }

        private static readonly EquipmentSlot[] FxPetSlots = { EquipmentSlot.Fx, EquipmentSlot.Pet };

        // ============================================================================
        // 1. 좌표 · 프레이밍
        // ============================================================================

        [Test]
        public void 이펙트_펫_카드_조각이_설계_좌표_그대로다()
        {
            List<GoldenPiece> golden = ReadGolden(out int items);
            var seen = new HashSet<(EquipmentSlot, int)>();
            var failures = new List<string>();
            AccessoryShapeBuilder.Rig rig = AccessoryCardIcon.CardRig();

            var byItem = new Dictionary<(EquipmentSlot, int), List<GoldenPiece>>();
            foreach (GoldenPiece p in golden)
            {
                if (!byItem.TryGetValue((p.Slot, p.Item), out List<GoldenPiece> list))
                {
                    list = new List<GoldenPiece>();
                    byItem[(p.Slot, p.Item)] = list;
                }
                list.Add(p);
            }
            Assert.AreEqual(items, byItem.Count, "골든의 TOTAL 아이템 수와 실제 아이템 수가 다릅니다.");

            foreach (KeyValuePair<(EquipmentSlot, int), List<GoldenPiece>> kv in byItem)
            {
                (EquipmentSlot slot, int item) = kv.Key;
                seen.Add(kv.Key);
                Assert.IsTrue(AccessoryCardIcon.Frame.TryGetCardFrame(slot, out float unitsPerR, out float centerYInR),
                    $"{slot} {item}: 카드 프레임이 없습니다 — 그 자리는 옛 40u 폴백으로 되돌아갑니다.");

                List<AccessoryShapeBuilder.Shape> card = Build(slot, item, AccessorySurface.Card);
                Assert.AreEqual(kv.Value.Count, card.Count,
                    $"{slot} {item}: 카드 조각 수 {card.Count} ≠ 골든 {kv.Value.Count} ({Regen}).");

                for (int i = 0; i < card.Count; i++)
                {
                    GoldenPiece want = kv.Value[i];
                    AccessoryShapeBuilder.Shape got = card[i];
                    string where = $"{slot} {item} [{i}] {want.Name}";
                    if (got.Name != want.Name) { failures.Add($"{where}: 이름이 '{got.Name}'"); continue; }
                    if (got.Loop != want.Loop) failures.Add($"{where}: loop {got.Loop}");
                    if (got.Filled != want.Filled) failures.Add($"{where}: filled {got.Filled}");
                    if (got.Tone != want.Tone) failures.Add($"{where}: tone {got.Tone} ≠ {want.Tone}");
                    if (got.NoStroke != want.NoStroke) failures.Add($"{where}: noStroke {got.NoStroke}");
                    if (got.UnderBack != want.UnderBack) failures.Add($"{where}: underBack {got.UnderBack} ≠ {want.UnderBack}");
                    if (Mathf.Abs(got.StrokeMult - want.StrokeMult) > 1e-4f)
                        failures.Add($"{where}: strokeMult {got.StrokeMult} ≠ {want.StrokeMult}");
                    if (Mathf.Abs(got.LineAlpha - want.LineAlpha) > 1e-4f)
                        failures.Add($"{where}: lineAlpha {got.LineAlpha} ≠ {want.LineAlpha}");
                    if (got.Points.Length != want.Points.Length)
                    {
                        failures.Add($"{where}: 점 수 {got.Points.Length} ≠ {want.Points.Length}");
                        continue;
                    }
                    for (int k = 0; k < got.Points.Length; k++)
                    {
                        Vector2 icon = ToIcon(got.Points[k], rig, unitsPerR, centerYInR);
                        if (Vector2.Distance(icon, want.Points[k]) > 0.01f)
                        {
                            failures.Add($"{where}: {k}번 점이 ({icon.x:F3}, {icon.y:F3}) — 설계는 " +
                                $"({want.Points[k].x:F3}, {want.Points[k].y:F3})");
                            break;
                        }
                    }
                }
            }

            // 골든이 12종을 전부 덮는가(한 종이 빠져도 위 루프는 초록이다).
            for (int s = 0; s < FxPetSlots.Length; s++)
            {
                for (int i = 0; i < ItemCatalog.ItemCountIn(FxPetSlots[s]); i++)
                {
                    Assert.IsTrue(seen.Contains((FxPetSlots[s], i)),
                        $"{FxPetSlots[s]} {i}번이 골든에 없습니다 — 그 카드만 조용히 검사 밖입니다 ({Regen}).");
                }
            }
            Assert.IsEmpty(failures,
                "이펙트/펫 카드가 설계 좌표(§14-12-3)와 다릅니다:\n  " + string.Join("\n  ", failures));
        }

        /// <summary>슬롯 박스가 없는 두 자리가 <b>전체 상자 프레이밍</b>을 쓴다 — 그리고 그 사실이
        /// 「인계본이 선언한 슬롯 박스」와 <b>섞이지 않는다</b>(<c>CardShapeContractTests</c>가 반대편을 잠근다).</summary>
        [Test]
        public void 이펙트_펫은_슬롯_박스가_없고_전체_상자_프레이밍을_쓴다()
        {
            for (int s = 0; s < FxPetSlots.Length; s++)
            {
                EquipmentSlot slot = FxPetSlots[s];
                Assert.IsFalse(AccessoryCardIcon.Frame.TryGet(slot, out _, out _),
                    $"{slot}: 인계본은 이 자리의 슬롯 박스를 선언한 적이 없습니다(§14-12-3 「64u 상자 전체」).");
                Assert.IsTrue(AccessoryCardIcon.Frame.TryGetCardFrame(slot, out float upr, out float cy));
                Assert.AreEqual(AccessoryCardIcon.Frame.FullBoxUnitsPerR, upr, 1e-5f,
                    $"{slot}: 1 R 당 아이콘 단위가 전체 상자 선언값이 아닙니다.");
                Assert.AreEqual(
                    (AccessoryCardIcon.Frame.FullBoxHeadCenterY - AccessoryCardIcon.Frame.IconViewBox * 0.5f)
                        / AccessoryCardIcon.Frame.FullBoxUnitsPerR, cy, 1e-5f,
                    $"{slot}: 상자 중심의 y 가 선언된 머리 중심에서 유도되지 않았습니다.");
            }
        }

        // ============================================================================
        // 2. 몸(월드)에 새지 않는다
        // ============================================================================

        /// <summary>§14-12-5 #23 「몸 효과·펫은 현행 그대로 — 카드만 갈라진다」.
        /// 카드 조각이 몸 표면으로 한 개라도 새면 캐릭터 위에 <b>정면 아이콘이 통째로 뜬다</b>.</summary>
        [Test]
        public void 이펙트_펫_카드_조각은_몸_표면으로_새지_않는다()
        {
            int cardPieces = 0;
            for (int s = 0; s < FxPetSlots.Length; s++)
            {
                EquipmentSlot slot = FxPetSlots[s];
                for (int i = 0; i < ItemCatalog.ItemCountIn(slot); i++)
                {
                    List<AccessoryShapeBuilder.Shape> body = Build(slot, i, AccessorySurface.Body);
                    Assert.AreEqual(0, body.Count,
                        $"{slot} {i}번이 몸 표면에 조각 {body.Count}개를 냈습니다 — 이펙트·펫의 몸 도형은 " +
                        "Interaction/AppearanceShapeBuilder.cs 소관입니다.");

                    List<AccessoryShapeBuilder.Shape> card = Build(slot, i, AccessorySurface.Card);
                    Assert.Greater(card.Count, 0, $"{slot} {i}번의 카드 조각이 없습니다.");
                    foreach (AccessoryShapeBuilder.Shape p in card)
                    {
                        Assert.IsTrue(AccessorySurfaces.IsExplicit(p.Surfaces, AccessorySurface.Card),
                            $"{slot} {i} '{p.Name}': 카드 전용(명시)이어야 합니다.");
                        Assert.IsFalse(p.IsOn(AccessorySurface.Body),
                            $"{slot} {i} '{p.Name}': 카드 조각이 몸에도 켜져 있습니다.");
                        Assert.IsTrue(p.IsHandoff,
                            $"{slot} {i} '{p.Name}': 계약 v2 조각이 아닙니다(strokeInR 0) — " +
                            "그러면 잉크 윤곽·재질 채움·하이라이트 문법을 못 탑니다.");
                        cardPieces++;
                    }
                }
            }
            Debug.Log($"[FX/PET 카드] 조각 {cardPieces}개 · 몸 누출 0.");
        }

        // ============================================================================
        // 3. 월드 정렬 2건 — 숫자를 베끼지 않고 <b>월드에서 직접 잰다</b>
        // ============================================================================

        /// <summary>반짝임 카드의 별과 착용 별이 <b>같은 허리</b>를 갖는가.
        /// <para>기준은 <c>AppearanceShapeBuilder.SparkleConcaveRatio</c> 상수 자체다 — 그 상수 문서가
        /// "카드가 이미 쓰고 있는 값이라 카드와 착용 모습이 같은 그림이 된다"를 근거로 삼고 있어서,
        /// 카드가 다른 비율을 쓰는 순간 그 문장이 조용히 거짓이 된다.</para></summary>
        [Test]
        public void 반짝임_카드_별의_오목비가_월드_상수와_같다()
        {
            List<AccessoryShapeBuilder.Shape> card = Build(EquipmentSlot.Fx, AppearanceShapeBuilder.FxSparkle,
                AccessorySurface.Card);
            int stars = 0;
            foreach (AccessoryShapeBuilder.Shape p in card)
            {
                if (p.Points.Length != 8) continue;
                Vector2 c = Center(p.Points);
                float outer = 0f, inner = float.MaxValue;
                for (int i = 0; i < p.Points.Length; i++)
                {
                    float r = Vector2.Distance(new Vector2(p.Points[i].x, p.Points[i].y), c);
                    if (i % 2 == 0) outer = Mathf.Max(outer, r); else inner = Mathf.Min(inner, r);
                }
                Assert.Greater(outer, 0f, $"'{p.Name}': 별의 바깥 반지름이 0입니다.");
                Assert.AreEqual(AppearanceShapeBuilder.SparkleConcaveRatio, inner / outer, 1e-3f,
                    $"'{p.Name}'의 오목비가 월드 별과 다릅니다 — 카드와 착용 모습의 허리가 갈라집니다.");
                stars++;
            }
            Assert.AreEqual(2, stars, "반짝임 카드의 별이 2개(큰 별 + 작은 별)가 아닙니다 — 대조가 공허합니다.");
        }

        /// <summary>커서친구 카드가 월드와 <b>같은 위상</b>인가 — 머리(주색) + 꼬리(보조색)이고
        /// 두 조각이 <b>점 2개를 공유</b>한다(그 공유가 곧 부착이다).
        /// <para>기대값은 월드 도형(<c>AppearanceShapeBuilder.CursorHead/CursorTail</c>)에서 <b>세어서</b> 얻는다 —
        /// 숫자를 여기 적어 두면 월드가 다시 바뀌는 날 이 검사만 옛 사실로 남는다.</para></summary>
        [Test]
        public void 커서친구_카드가_월드와_같은_머리_꼬리_분할이다()
        {
            Vector3[] worldHead = AppearanceShapeBuilder.CursorHead(1f);
            Vector3[] worldTail = AppearanceShapeBuilder.CursorTail(1f);
            int worldShared = SharedPointCount(worldHead, worldTail, 1e-4f);
            Assert.Greater(worldShared, 0, "월드 커서친구의 머리/꼬리가 점을 공유하지 않습니다 — 기준이 죽었습니다.");

            List<AccessoryShapeBuilder.Shape> card = Build(EquipmentSlot.Pet, AppearanceShapeBuilder.PetCursor,
                AccessorySurface.Card);
            AccessoryShapeBuilder.Shape? head = null, tail = null;
            foreach (AccessoryShapeBuilder.Shape p in card)
            {
                if (!p.Filled) continue;
                if (p.Tone == AccessoryTone.Primary) head = p;
                else if (p.Tone == AccessoryTone.Accent) tail = p;
            }
            Assert.IsTrue(head.HasValue, "커서친구 카드에 주색 채움(머리)이 없습니다.");
            Assert.IsTrue(tail.HasValue, "커서친구 카드에 보조색 채움(꼬리)이 없습니다 — " +
                "월드는 꼬리를 보조색으로 그립니다(카드에 없는 색이 착용하면 나타나는 형태의 결함).");
            Assert.AreEqual(worldHead.Length, head.Value.Points.Length,
                "카드 머리의 점 수가 월드 머리와 다릅니다.");
            Assert.AreEqual(worldTail.Length, tail.Value.Points.Length,
                "카드 꼬리의 점 수가 월드 꼬리와 다릅니다.");

            // 카드는 64u 아이콘 좌표를 R 로 되돌린 것이라 배율·원점이 월드와 다르다 —
            // 그래서 「몇 점을 공유하는가」라는 <b>위상</b>만 잰다(좌표 비교가 아니다).
            Assert.AreEqual(worldShared, SharedPointCount(ToArray(head.Value), ToArray(tail.Value), 1e-4f),
                "카드 머리/꼬리가 월드와 같은 수의 점을 공유하지 않습니다 — 두 조각이 벌어지거나 겹칩니다.");
        }

        // ============================================================================
        // 4. 상자를 넘지 않는다
        // ============================================================================

        /// <summary>설계가 스스로 "64u 상자 전체"라고 말한 이상 <b>넘침 보정이 항등</b>이어야 한다.
        /// 항등이 아니면 그 아이템만 슬쩍 작아져 12칸의 크기 관계가 흐트러진다.</summary>
        [Test]
        public void 이펙트_펫_카드는_넘침_보정이_항등이다()
        {
            for (int s = 0; s < FxPetSlots.Length; s++)
            {
                EquipmentSlot slot = FxPetSlots[s];
                for (int i = 0; i < ItemCatalog.ItemCountIn(slot); i++)
                {
                    Assert.IsTrue(AccessoryCardIcon.TryGetBoxFit(slot, i, out AccessoryCardIcon.BoxFit fit),
                        $"{slot} {i}번의 프레이밍을 잴 수 없습니다.");
                    Assert.IsTrue(fit.IsIdentity,
                        $"{slot} {i}번이 상자를 넘어 보정됐습니다(축소 {fit.Shrink:F4} · " +
                        $"밀기 {fit.OffsetXInUnits:F2}, {fit.OffsetYInUnits:F2}) — 설계는 64u 상자 안입니다.");
                }
            }
        }

        // ============================================================================
        // 5. 채움이 실제로 도형을 덮는가
        // ============================================================================

        /// <summary>
        /// 채운 조각의 <b>삼각분할이 도형을 그대로 덮는가</b>. (다)군에는 점 112개짜리 오목 다각형
        /// (발자국 밑창)이 있고, 그것이 42종 통틀어 가장 점이 많은 채움면이다.
        ///
        /// <para>왜 삼각형 <b>수</b>로는 안 되는가: <c>AccessoryShapeBuilder.Triangulate</c>는 귀를 못 찾으면
        /// 남은 것을 <b>부채꼴로 덮고</b> 끝낸다. 그래서 삼각형 수는 어떤 경우에도 n−2 로 같고, 실패해도
        /// 개수로는 표가 안 난다 — 대신 <b>면적</b>이 어긋난다(부채꼴이 도형 밖을 덮는다).
        /// 화면에서는 카드 채움이 <b>삐져나오거나 구멍이 뚫린</b> 모습인데, 44px 썸네일이라
        /// "원래 그런 아이콘"으로 읽혀 아무도 신고하지 않는다.</para>
        /// </summary>
        [Test]
        public void 이펙트_펫_카드의_채움이_도형_면적을_그대로_덮는다()
        {
            int filled = 0, worstPoints = 0;
            var failures = new List<string>();
            for (int s = 0; s < FxPetSlots.Length; s++)
            {
                EquipmentSlot slot = FxPetSlots[s];
                for (int item = 0; item < ItemCatalog.ItemCountIn(slot); item++)
                {
                    foreach (AccessoryShapeBuilder.Shape p in Build(slot, item, AccessorySurface.Card))
                    {
                        if (!p.Filled) continue;
                        filled++;
                        worstPoints = Mathf.Max(worstPoints, p.Points.Length);

                        int[] tris = AccessoryShapeBuilder.Triangulate(p.Points);
                        float covered = 0f;
                        for (int t = 0; t + 2 < tris.Length; t += 3)
                        {
                            covered += Mathf.Abs(TriangleArea(p.Points[tris[t]], p.Points[tris[t + 1]],
                                p.Points[tris[t + 2]]));
                        }
                        float area = Mathf.Abs(SignedArea(p.Points));
                        Assert.Greater(area, 0f, $"{slot} {item} '{p.Name}': 채움 면적이 0입니다.");
                        if (Mathf.Abs(covered - area) / area > 0.01f)
                        {
                            failures.Add($"{slot} {item} '{p.Name}'({p.Points.Length}점): " +
                                $"삼각형 합 {covered:F5} vs 다각형 {area:F5}");
                        }
                    }
                }
            }
            Assert.Greater(filled, 0, "채운 조각이 하나도 없습니다 — 대조가 공허합니다.");
            Assert.IsEmpty(failures, "카드 채움이 도형과 다른 면적을 덮습니다:\n  " + string.Join("\n  ", failures));
            Debug.Log($"[FX/PET 카드] 채움 {filled}면 · 최대 점 수 {worstPoints}" +
                $"(카드 버퍼 {AccessoryCardIcon.CardPointBudget}칸).");
        }

        private static float TriangleArea(Vector3 a, Vector3 b, Vector3 c)
            => ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) * 0.5f;

        private static float SignedArea(Vector3[] p)
        {
            float a = 0f;
            for (int i = 0; i < p.Length; i++)
            {
                Vector3 c = p[i], d = p[(i + 1) % p.Length];
                a += c.x * d.y - d.x * c.y;
            }
            return a * 0.5f;
        }

        /// <summary>가장 점이 많은 조각이 카드 그리기 버퍼 안에 드는가. 넘치면
        /// <c>AccessoryCardIcon</c>이 <b>말없이 앞쪽만</b> 그리고, 닫힌 도형은 엉뚱한 자리에서 닫힌다.</summary>
        [Test]
        public void 이펙트_펫_카드_조각이_카드_그리기_버퍼_안에_든다()
        {
            for (int s = 0; s < FxPetSlots.Length; s++)
            {
                EquipmentSlot slot = FxPetSlots[s];
                for (int item = 0; item < ItemCatalog.ItemCountIn(slot); item++)
                {
                    foreach (AccessoryShapeBuilder.Shape p in Build(slot, item, AccessorySurface.Card))
                    {
                        // 고리는 그릴 때 첫 점을 한 번 더 쓴다(AccessoryCardIcon.AddStrokes).
                        int need = p.Points.Length + (p.Loop ? 1 : 0);
                        Assert.LessOrEqual(need, AccessoryCardIcon.CardPointBudget,
                            $"{slot} {item} '{p.Name}': 점 {need}개가 카드 버퍼 " +
                            $"{AccessoryCardIcon.CardPointBudget}칸을 넘습니다.");
                    }
                }
            }
        }

        // ---- 잔심부름 --------------------------------------------------------------

        private static Vector2 Center(Vector3[] pts)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < pts.Length; i++)
            {
                minX = Mathf.Min(minX, pts[i].x); maxX = Mathf.Max(maxX, pts[i].x);
                minY = Mathf.Min(minY, pts[i].y); maxY = Mathf.Max(maxY, pts[i].y);
            }
            return new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        }

        private static Vector3[] ToArray(in AccessoryShapeBuilder.Shape s) => s.Points;

        private static int SharedPointCount(Vector3[] a, Vector3[] b, float epsilon)
        {
            int shared = 0;
            for (int i = 0; i < a.Length; i++)
            {
                for (int j = 0; j < b.Length; j++)
                {
                    if (Vector3.Distance(a[i], b[j]) <= epsilon) { shared++; break; }
                }
            }
            return shared;
        }
    }
}
