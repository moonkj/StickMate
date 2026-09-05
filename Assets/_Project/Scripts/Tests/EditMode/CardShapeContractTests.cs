using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Interaction;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 인계본 조각 — 계약 v2 회귀 잠금 (2026-09-05, 인계본 「보이는 그대로」 이식 · R16 착용 재설계).
    ///
    /// ============================================================================
    /// 무엇을 잠그는가
    /// ============================================================================
    /// 인계본 16종 — 아이콘 91조각(카드 = 몸 한 벌) + 망토 무대 6조각(몸 전용) — 이
    /// <see cref="AccessoryShapeBuilder.Append"/>에서 <b>좌표·역할·표면·획 배수·명목 폭·알파·밑조각·레이어·흔들 구간까지</b>
    /// 그대로 나오는가. 기대값은 프로덕션 함수가 아니라 <c>Golden/CardShapeGolden.txt</c>다 — 그 골든은
    /// <c>Tools/CardShapeGen/gen_card_shapes.py</c>가 설계 모델(<c>design/equipment/verify/r16_model.py</c>, 인계본 HTML
    /// 직접 파싱 · 원문 의미)에서 굽는다. 생성기와 이 검사는 코드를 공유하지 않는다(TEAM.md 「생성기와 검사기」 규칙 1·2).
    ///
    /// ============================================================================
    /// 표면 규칙 — 여기서만 잠근다 (EQUIPMENT_HANDOFF_PORT_SPEC §14-6 #1)
    /// ============================================================================
    ///  · 13종: 몸과 카드가 <b>같은 목록</b>(surfaces 0 = Body|Card). 3/4 재저작 도형은 이 표면에서 폐기됐다.
    ///  · 망토 2종: 몸 = 무대 뒤판(뒤, 밑단 흔들림) + 칼라·걸쇠(몸통 앞 <see cref="AccessoryShapeBuilder.SortCapeFront"/>),
    ///    카드 = 아이콘. 두 목록은 겹치지 않는다.
    ///  · 인계본에 없는 14종(장비 8 + 머리 6): v1 규칙 그대로(<c>IsHandoff</c> false).
    ///  · <c>underBack</c>은 데이터지만 <b>기하로 다시 유도</b>해 맞춘다(앞 조각 안에 점 90% 이상 = 모델 규칙의 독립 구현).
    ///  · 옛 정원(2~4)·보조색(정확히 1) 게이트는 <b>리더 결정으로 폐지</b>됐다(R16 (h)) — 여기서 되살리지 않는다.
    /// </summary>
    public sealed class CardShapeContractTests
    {
        private const string GoldenPath = "Assets/_Project/Scripts/Tests/EditMode/Golden/CardShapeGolden.txt";

        /// <summary>골든 좌표는 소수 5자리(R 단위). 리그 왕복의 float 오차를 더해도 1e-4 안이다.</summary>
        private const float CoordTolerance = 1e-4f;

        private static readonly EquipmentSlot[] BodySlots =
        {
            EquipmentSlot.Head, EquipmentSlot.Eyes, EquipmentSlot.Neck, EquipmentSlot.Shoulders, EquipmentSlot.Hair,
        };

        private sealed class GoldenPiece
        {
            public EquipmentSlot Slot;
            public string ItemConst;
            public int Index;
            public string Name;
            public bool Loop, Filled, NoStroke, BodyFixed;
            public int Tone, Surfaces, UnderBack, SwayStart, SwayCount, Layer;
            public float StrokeMult, StrokeInR, Alpha, LineAlpha;
            public Vector2[] Points;
        }

        private sealed class GoldenItem
        {
            public EquipmentSlot Slot;
            public string ItemConst;
            public float GroupAlpha, Scale, ScaleY, OffsetY;
            public bool MirrorX, Pending;
        }

        private sealed class GoldenBody
        {
            public EquipmentSlot Slot;
            public string ItemConst;
            public int Index;
            public string Name;
            public Vector2[] Points;
        }

        private sealed class Golden
        {
            public readonly List<GoldenPiece> Pieces = new List<GoldenPiece>();
            public readonly List<GoldenItem> Items = new List<GoldenItem>();
            public readonly List<GoldenBody> Bodies = new List<GoldenBody>();
            public readonly Dictionary<EquipmentSlot, (float UnitsPerR, float CenterYInR)> Frames =
                new Dictionary<EquipmentSlot, (float, float)>();
            /// <summary>STATE 줄 — 상태 켜짐(월요일)일 때 몸 조각 전부의 y 가 내려가는 양(R). 지금은 줄무늬타이 하나.</summary>
            public readonly Dictionary<(EquipmentSlot Slot, string ItemConst), float> StateDrops =
                new Dictionary<(EquipmentSlot, string), float>();
            public int TotalPieces, TotalBody, TotalCard, TotalHighlights;
        }

        private static int ItemIndexOf(string constName)
        {
            System.Reflection.FieldInfo f = typeof(AccessoryShapeBuilder).GetField(constName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(f, $"골든이 가리키는 자리 상수 AccessoryShapeBuilder.{constName}이 없습니다.");
            return (int)f.GetRawConstantValue();
        }

        private static Golden ReadGolden()
        {
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, GoldenPath);
            Assert.IsTrue(File.Exists(path), $"골든이 없습니다: {GoldenPath} (python3 Tools/CardShapeGen/gen_card_shapes.py)");
            var g = new Golden();
            var inv = CultureInfo.InvariantCulture;
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
                            Slot = (EquipmentSlot)int.Parse(c[1], inv), ItemConst = c[2], Index = int.Parse(c[3], inv), Name = c[4],
                            Loop = c[5] == "1", Filled = c[6] == "1", Tone = int.Parse(c[7], inv), Surfaces = int.Parse(c[8], inv),
                            StrokeMult = float.Parse(c[9], inv), StrokeInR = float.Parse(c[10], inv), NoStroke = c[11] == "1",
                            Alpha = float.Parse(c[12], inv), LineAlpha = float.Parse(c[13], inv),
                            UnderBack = int.Parse(c[14], inv), Layer = int.Parse(c[15], inv),
                            SwayStart = int.Parse(c[16], inv), SwayCount = int.Parse(c[17], inv), BodyFixed = c[18] == "1",
                        };
                        int n = int.Parse(c[19], inv);
                        Assert.AreEqual(n, c.Length - 20, $"골든 {p.Name}: 점 수 {n}인데 좌표 칸이 {c.Length - 20}개입니다.");
                        p.Points = new Vector2[n];
                        for (int i = 0; i < n; i++)
                        {
                            string[] xy = c[20 + i].Split(',');
                            p.Points[i] = new Vector2(float.Parse(xy[0], inv), float.Parse(xy[1], inv));
                        }
                        g.Pieces.Add(p);
                        break;
                    }
                    case "ITEM":
                        g.Items.Add(new GoldenItem
                        {
                            Slot = (EquipmentSlot)int.Parse(c[1], inv), ItemConst = c[2], GroupAlpha = float.Parse(c[3], inv),
                            Scale = float.Parse(c[4], inv), ScaleY = float.Parse(c[5], inv), OffsetY = float.Parse(c[6], inv),
                            MirrorX = c[7] == "1", Pending = c[8] == "1",
                        });
                        break;
                    case "BODY":
                    {
                        var b = new GoldenBody
                        {
                            Slot = (EquipmentSlot)int.Parse(c[1], inv), ItemConst = c[2], Index = int.Parse(c[3], inv), Name = c[4],
                        };
                        int n = int.Parse(c[5], inv);
                        Assert.AreEqual(n, c.Length - 6, $"골든 BODY {b.Name}: 점 수 {n}인데 좌표 칸이 {c.Length - 6}개입니다.");
                        b.Points = new Vector2[n];
                        for (int i = 0; i < n; i++)
                        {
                            string[] xy = c[6 + i].Split(',');
                            b.Points[i] = new Vector2(float.Parse(xy[0], inv), float.Parse(xy[1], inv));
                        }
                        g.Bodies.Add(b);
                        break;
                    }
                    case "FRAME":
                        g.Frames[(EquipmentSlot)int.Parse(c[1], inv)] = (float.Parse(c[2], inv), float.Parse(c[3], inv));
                        break;
                    case "STATE":
                        g.StateDrops[((EquipmentSlot)int.Parse(c[1], inv), c[2])] = float.Parse(c[3], inv);
                        break;
                    case "TOTAL":
                        g.TotalPieces = int.Parse(c[1], inv); g.TotalBody = int.Parse(c[2], inv);
                        g.TotalCard = int.Parse(c[3], inv); g.TotalHighlights = int.Parse(c[4], inv);
                        break;
                    default:
                        Assert.Fail($"골든에 모르는 줄 종류 '{c[0]}' — 생성기가 새 줄을 냈으면 이 판독기도 함께 갱신하십시오(조용히 건너뛰면 골든의 절반이 안 읽힌다).");
                        break;
                }
            }
            Assert.Greater(g.Pieces.Count, 0, "골든에 PIECE 줄이 없습니다 — 아래 대조가 공허합니다.");
            Assert.AreEqual(g.TotalPieces, g.Pieces.Count, "골든의 TOTAL 과 PIECE 줄 수가 다릅니다.");
            Assert.AreEqual(16, g.Items.Count, "골든 ITEM 줄이 16개가 아닙니다.");
            return g;
        }

        private static AccessoryShapeBuilder.Rig Rig() => AccessoryCardIcon.CardRig();

        private static List<AccessoryShapeBuilder.Shape> Build(EquipmentSlot slot, int item, AccessorySurface surface)
        {
            var sink = new List<AccessoryShapeBuilder.Shape>();
            AccessoryShapeBuilder.Append(sink, slot, item, Rig(), float.PositiveInfinity, 0f, false, surface);
            return sink;
        }

        private static Vector2 ToR(Vector3 local, in AccessoryShapeBuilder.Rig rig)
            => new Vector2(local.x / rig.HeadRadius, (local.y - rig.HeadCenterY) / rig.HeadRadius);

        /// <summary>골든 한 조각 ↔ 실제 한 조각. 다르면 사람이 읽는 설명, 같으면 null(양성 대조가 같은 함수를 반대 방향으로 쓴다).</summary>
        private static string Describe(GoldenPiece want, in AccessoryShapeBuilder.Shape got, in AccessoryShapeBuilder.Rig rig,
            bool comparePoints = true)
        {
            string where = $"{want.Slot} {want.ItemConst} [{want.Index}] {want.Name}";
            if (got.Name != want.Name) return $"{where}: 이름이 '{got.Name}'";
            if (got.Loop != want.Loop) return $"{where}: loop {got.Loop}";
            if (got.Filled != want.Filled) return $"{where}: filled {got.Filled}";
            if (got.Tone != want.Tone) return $"{where}: tone {got.Tone} ≠ {want.Tone}";
            if (got.Surfaces != want.Surfaces) return $"{where}: surfaces {got.Surfaces} ≠ {want.Surfaces}";
            if (Mathf.Abs(got.StrokeMult - want.StrokeMult) > 1e-6f) return $"{where}: strokeMult {got.StrokeMult} ≠ {want.StrokeMult}";
            if (Mathf.Abs(got.StrokeInR - want.StrokeInR) > 1e-6f) return $"{where}: strokeInR {got.StrokeInR} ≠ {want.StrokeInR}";
            if (got.NoStroke != want.NoStroke) return $"{where}: noStroke {got.NoStroke}";
            if (Mathf.Abs(got.Alpha - want.Alpha) > 1e-6f) return $"{where}: alpha {got.Alpha} ≠ {want.Alpha}";
            if (Mathf.Abs(got.LineAlpha - want.LineAlpha) > 1e-6f) return $"{where}: lineAlpha {got.LineAlpha} ≠ {want.LineAlpha}";
            if (got.UnderBack != want.UnderBack) return $"{where}: underBack {got.UnderBack} ≠ {want.UnderBack}";
            if (got.Layer != want.Layer) return $"{where}: layer {got.Layer} ≠ {want.Layer}";
            if (got.SwayStart != want.SwayStart || got.SwayCount != want.SwayCount) return $"{where}: sway {got.SwayStart}/{got.SwayCount} ≠ {want.SwayStart}/{want.SwayCount}";
            if (got.BodyFixed != want.BodyFixed) return $"{where}: bodyFixed {got.BodyFixed}";
            if (!comparePoints) return null;
            if (got.Points == null || got.Points.Length != want.Points.Length)
                return $"{where}: 점 수 {(got.Points?.Length ?? 0)} ≠ {want.Points.Length}";
            for (int i = 0; i < want.Points.Length; i++)
            {
                Vector2 r = ToR(got.Points[i], rig);
                float d = Mathf.Max(Mathf.Abs(r.x - want.Points[i].x), Mathf.Abs(r.y - want.Points[i].y));
                if (d > CoordTolerance) return $"{where}: {i}번 점 ({r.x:F5},{r.y:F5}) ≠ 골든 ({want.Points[i].x:F5},{want.Points[i].y:F5}), 차 {d:E2} R";
            }
            return null;
        }

        private static List<GoldenBody> BodiesOf(Golden g, EquipmentSlot slot, string itemConst)
        {
            var list = new List<GoldenBody>();
            foreach (GoldenBody b in g.Bodies) if (b.Slot == slot && b.ItemConst == itemConst) list.Add(b);
            list.Sort((a, b) => a.Index.CompareTo(b.Index));
            return list;
        }

        /// <summary>몸 표면 좌표 ↔ 골든 BODY 줄(r17 좌표). 변환은 프로덕션(ApplyBodyTransform)이 걸고, 기대값은 모델이 준다.</summary>
        private static string DescribeBody(GoldenBody want, in AccessoryShapeBuilder.Shape got, in AccessoryShapeBuilder.Rig rig)
        {
            string where = $"{want.Slot} {want.ItemConst} 몸[{want.Index}] {want.Name}";
            if (got.Name != want.Name) return $"{where}: 이름이 '{got.Name}'";
            if (got.Points == null || got.Points.Length != want.Points.Length)
                return $"{where}: 점 수 {(got.Points?.Length ?? 0)} ≠ {want.Points.Length}";
            for (int i = 0; i < want.Points.Length; i++)
            {
                Vector2 r = ToR(got.Points[i], rig);
                float d = Mathf.Max(Mathf.Abs(r.x - want.Points[i].x), Mathf.Abs(r.y - want.Points[i].y));
                if (d > CoordTolerance) return $"{where}: {i}번 점 ({r.x:F5},{r.y:F5}) ≠ r17 ({want.Points[i].x:F5},{want.Points[i].y:F5}), 차 {d:E2} R";
            }
            return null;
        }

        private static Dictionary<(EquipmentSlot, int), List<GoldenPiece>> ByItem(Golden g)
        {
            var byItem = new Dictionary<(EquipmentSlot, int), List<GoldenPiece>>();
            foreach (GoldenPiece p in g.Pieces)
            {
                var key = (p.Slot, ItemIndexOf(p.ItemConst));
                if (!byItem.TryGetValue(key, out List<GoldenPiece> list)) byItem[key] = list = new List<GoldenPiece>();
                list.Add(p);
            }
            Assert.AreEqual(16, byItem.Count, "골든이 가리키는 아이템이 16종이 아닙니다.");
            return byItem;
        }

        private static List<GoldenPiece> OnSurface(List<GoldenPiece> pieces, AccessorySurface surface)
        {
            var list = new List<GoldenPiece>();
            foreach (GoldenPiece p in pieces)
            {
                if (AccessorySurfaces.IsOn((byte)p.Surfaces, surface)) list.Add(p);
            }
            return list;
        }

        // ============================================================================
        // 1. 골든 대조 — 두 표면 각각
        // ============================================================================

        [Test]
        public void 인계본_조각이_두_표면에서_골든과_같다()
        {
            Golden g = ReadGolden();
            AccessoryShapeBuilder.Rig rig = Rig();
            var failures = new List<string>();
            int checkedBody = 0, checkedCard = 0, highlights = 0;
            foreach (GoldenPiece p in g.Pieces) if (p.Tone == AccessoryTone.Highlight) highlights++;
            Assert.AreEqual(g.TotalHighlights, highlights, "골든의 하이라이트 수가 TOTAL 줄과 다릅니다.");

            foreach (KeyValuePair<(EquipmentSlot, int), List<GoldenPiece>> kv in ByItem(g))
            {
                foreach (AccessorySurface surface in new[] { AccessorySurface.Body, AccessorySurface.Card })
                {
                    List<GoldenPiece> want = OnSurface(kv.Value, surface);
                    List<AccessoryShapeBuilder.Shape> got = Build(kv.Key.Item1, kv.Key.Item2, surface);
                    if (got.Count != want.Count)
                    {
                        failures.Add($"{kv.Key.Item1} {kv.Value[0].ItemConst} [{surface}]: 조각 {got.Count}개 ≠ 골든 {want.Count}개");
                        continue;
                    }
                    bool body = surface == AccessorySurface.Body;
                    List<GoldenBody> bodies = body ? BodiesOf(g, kv.Key.Item1, kv.Value[0].ItemConst) : null;
                    if (body && bodies.Count != got.Count)
                    {
                        failures.Add($"{kv.Key.Item1} {kv.Value[0].ItemConst} [Body]: BODY 줄 {bodies.Count}개 ≠ 조각 {got.Count}개");
                        continue;
                    }
                    for (int i = 0; i < got.Count; i++)
                    {
                        // 카드: 카드 프레임 좌표 그대로. 몸: 아이템 변환(모자 u·ky·dy, 반전, 오프셋)이 걸린 r17 좌표(BODY 줄).
                        string d = Describe(want[i], got[i], rig, comparePoints: !body);
                        if (d == null && body) d = DescribeBody(bodies[i], got[i], rig);
                        if (d != null) failures.Add($"[{surface}] {d}");
                    }
                    if (body) checkedBody += got.Count; else checkedCard += got.Count;
                }
            }
            Assert.AreEqual(g.TotalBody, checkedBody, "몸 표면에서 대조한 조각 수가 골든 TOTAL 과 다릅니다.");
            Assert.AreEqual(g.TotalCard, checkedCard, "카드 표면에서 대조한 조각 수가 골든 TOTAL 과 다릅니다.");
            Assert.IsEmpty(failures,
                $"인계본 조각이 골든과 {failures.Count}건 다릅니다:\n  " + string.Join("\n  ", failures) +
                "\n좌표·속성을 바꾸는 것이 의도였다면 모델(r16_model.py)을 고치고 gen_card_shapes.py 로 다시 구우세요 — 손으로 옮기지 마세요.");
            Debug.Log($"[인계본계약] 몸 {checkedBody} · 카드 {checkedCard} 조각 전부 골든과 일치 (하이라이트 {highlights}).");
        }

        [Test]
        public void 골든_비교기가_실제로_차이를_잡는다()
        {
            Golden g = ReadGolden();
            AccessoryShapeBuilder.Rig rig = Rig();
            GoldenPiece want = g.Pieces[0];
            List<AccessoryShapeBuilder.Shape> got = Build(want.Slot, ItemIndexOf(want.ItemConst), AccessorySurface.Card);
            Assert.IsNull(Describe(want, got[0], rig), "대조군(원본)이 이미 다릅니다 — 양성 대조가 뜻을 잃습니다.");

            var shifted = (Vector2[])want.Points.Clone();
            shifted[2] += new Vector2(CoordTolerance * 5f, 0f);
            var probe = Clone(want); probe.Points = shifted;
            Assert.IsNotNull(Describe(probe, got[0], rig), "좌표를 흔들었는데 비교기가 침묵했습니다.");

            var wrongMult = Clone(want); wrongMult.StrokeMult += 0.05f;
            Assert.IsNotNull(Describe(wrongMult, got[0], rig), "strokeMult 를 흔들었는데 비교기가 침묵했습니다.");

            var wrongAlpha = Clone(want); wrongAlpha.Alpha += 0.01f;
            Assert.IsNotNull(Describe(wrongAlpha, got[0], rig), "alpha 를 흔들었는데 비교기가 침묵했습니다.");
        }

        private static GoldenPiece Clone(GoldenPiece p) => new GoldenPiece
        {
            Slot = p.Slot, ItemConst = p.ItemConst, Index = p.Index, Name = p.Name, Loop = p.Loop, Filled = p.Filled,
            NoStroke = p.NoStroke, Layer = p.Layer, Tone = p.Tone, Surfaces = p.Surfaces, UnderBack = p.UnderBack,
            SwayStart = p.SwayStart, SwayCount = p.SwayCount, StrokeMult = p.StrokeMult, StrokeInR = p.StrokeInR, BodyFixed = p.BodyFixed,
            Alpha = p.Alpha, LineAlpha = p.LineAlpha,
            Points = p.Points,
        };

        // ============================================================================
        // 2. 표면 규칙
        // ============================================================================

        /// <summary>
        /// 표면 규칙(§14-6 #1 · R19 §14-10-8). 아이템은 <b>한 벌</b>(카드 = 몸 좌표 + 아이템 변환, 조각 surfaces 0)이거나
        /// <b>두 벌</b>(카드 조각 전부 Card 명시 · 몸 조각 전부 Body 명시 + bodyFixed — 망토 무대, 모자 2층 분할/뒷벽, 외알안경 거울·눈,
        /// 배낭 착용면)이다. 어느 쪽인지는 데이터가 말한다(카드 조각이 전부 Card 명시인가). 두 벌은 표면이 섞이지 않고,
        /// 몸 뒤층(<see cref="AccessoryPieceLayer.Back"/>)은 <see cref="AccessoryShapeBuilder.SortBack"/>, 몸통 앞은 <see cref="AccessoryShapeBuilder.SortCapeFront"/>다.
        /// 한 벌의 유일한 예외는 투명 렌즈의 카드 전용 유리 워시(팔레트 L-4, Card 명시 · 채움만)다.
        /// </summary>
        [Test]
        public void 아이템은_한_벌이거나_두_벌이고_두_벌은_표면과_층이_명시된다()
        {
            int oneSet = 0, twoList = 0;
            foreach (KeyValuePair<(EquipmentSlot, int), List<GoldenPiece>> kv in ByItem(ReadGolden()))
            {
                (EquipmentSlot slot, int item) = kv.Key;
                List<AccessoryShapeBuilder.Shape> body = Build(slot, item, AccessorySurface.Body);
                List<AccessoryShapeBuilder.Shape> card = Build(slot, item, AccessorySurface.Card);
                Assert.Greater(body.Count, 0, $"{slot} {item}: 몸 조각이 없습니다.");
                Assert.Greater(card.Count, 0, $"{slot} {item}: 카드 조각이 없습니다.");
                foreach (AccessoryShapeBuilder.Shape s in body) Assert.IsTrue(s.IsHandoff, $"{slot} {item} '{s.Name}': 인계본 조각이 아닙니다(strokeInR 0).");
                foreach (AccessoryShapeBuilder.Shape s in card) Assert.IsTrue(s.IsHandoff, $"{slot} {item} '{s.Name}': 인계본 조각이 아닙니다(strokeInR 0).");

                bool allCardExplicit = true;
                foreach (AccessoryShapeBuilder.Shape s in card) allCardExplicit &= AccessorySurfaces.IsExplicit(s.Surfaces, AccessorySurface.Card);

                if (allCardExplicit)
                {
                    twoList++;
                    foreach (AccessoryShapeBuilder.Shape s in card)
                        Assert.IsFalse(s.IsOn(AccessorySurface.Body), $"{slot} {item} '{s.Name}': 두 벌 아이템의 카드 조각이 몸에도 켜져 있습니다.");
                    int back = 0, front = 0;
                    foreach (AccessoryShapeBuilder.Shape s in body)
                    {
                        Assert.IsTrue(AccessorySurfaces.IsExplicit(s.Surfaces, AccessorySurface.Body), $"{slot} {item} '{s.Name}': 몸 조각은 Body 명시여야 합니다.");
                        Assert.IsFalse(s.IsOn(AccessorySurface.Card), $"{slot} {item} '{s.Name}': 몸 조각이 카드에도 켜져 있습니다.");
                        Assert.IsTrue(s.BodyFixed, $"{slot} {item} '{s.Name}': 두 벌 아이템의 몸 조각은 몸 좌표 그대로(bodyFixed)여야 합니다.");
                        Assert.AreEqual(AccessoryShapeBuilder.LayerOrder(s.Layer, SlotOrder(slot)), s.SortingOrder,
                            $"{slot} {item} '{s.Name}': 층 {s.Layer}과 정렬 번호 {s.SortingOrder}가 어긋납니다.");
                        if (s.Layer == (byte)AccessoryPieceLayer.Back) { back++; Assert.AreEqual(AccessoryShapeBuilder.SortBack, s.SortingOrder, $"{slot} {item} '{s.Name}': 뒤층은 몸 뒤여야 합니다."); }
                        if (s.IsFrontLayer) { front++; Assert.AreEqual(AccessoryShapeBuilder.SortCapeFront, s.SortingOrder, $"{slot} {item} '{s.Name}': 몸통 앞 층이 SortCapeFront 가 아닙니다."); }
                    }
                    if (slot == EquipmentSlot.Shoulders && (item == AccessoryShapeBuilder.BackCape || item == AccessoryShapeBuilder.BackLongCape))
                    {
                        int swaying = 0;
                        foreach (AccessoryShapeBuilder.Shape s in body) if (s.HasSway) swaying++;
                        Assert.AreEqual(2, front, $"{slot} {item}: 망토 앞 레이어 조각(칼라·걸쇠)이 2개여야 합니다.");
                        Assert.AreEqual(1, swaying, $"{slot} {item}: 흔들리는 뒤판이 하나여야 합니다.");
                    }
                    if (slot == EquipmentSlot.Head)
                        Assert.Greater(back, 0, $"{slot} {item}: 모자 2층(R19 H-1) — 머리 뒤층 조각(챙 먼 쪽·왕관 뒷벽)이 하나도 없습니다.");
                    if (slot == EquipmentSlot.Shoulders && item == AccessoryShapeBuilder.BackBackpack)
                        Assert.AreEqual(2, front, $"{slot} {item}: 배낭 어깨끈(몸통 앞) 2개여야 합니다(R19 B-4).");
                    continue;
                }

                oneSet++;
                var shared = new List<AccessoryShapeBuilder.Shape>();
                foreach (AccessoryShapeBuilder.Shape s in card)
                {
                    if (AccessorySurfaces.IsExplicit(s.Surfaces, AccessorySurface.Card) && !s.IsOn(AccessorySurface.Body))
                    {
                        Assert.IsTrue(s.Filled && s.NoStroke, $"{slot} {item} '{s.Name}': 카드 전용 조각은 유리 워시(채움만)여야 합니다.");
                        Assert.Less(s.Alpha, 1f, $"{slot} {item} '{s.Name}': 유리 워시는 반투명(α<1)이어야 합니다.");
                        continue;
                    }
                    shared.Add(s);
                }
                int ci = 0;
                foreach (AccessoryShapeBuilder.Shape s in body)
                {
                    Assert.IsFalse(AccessorySurfaces.IsExplicit(s.Surfaces, AccessorySurface.Body), $"{slot} {item} '{s.Name}': 한 벌 아이템에 몸 전용 조각이 있습니다(R19 H-1 이후 바탕 조각은 없다).");
                    Assert.Less(ci, shared.Count, $"{slot} {item}: 몸의 한 벌 조각이 카드보다 많습니다.");
                    Assert.AreEqual(shared[ci].Name, s.Name, $"{slot} {item}: 카드/몸 조각 순서가 다릅니다.");
                    Assert.AreEqual(AccessorySurfaces.Unset, s.Surfaces, $"{slot} {item} '{s.Name}': 한 벌 조각은 미설정(0 = Body|Card)이어야 합니다.");
                    Assert.IsFalse(s.BodyFixed, $"{slot} {item} '{s.Name}': 한 벌 조각은 아이템 변환을 탄다(bodyFixed 아님).");
                    ci++;
                }
                Assert.AreEqual(shared.Count, ci, $"{slot} {item}: 카드 조각이 몸에 전부 있지 않습니다.");
            }
            Assert.Greater(oneSet, 0, "한 벌 아이템이 하나도 없습니다 — 판정이 죽었습니다.");
            Assert.Greater(twoList, 0, "두 벌 아이템이 하나도 없습니다 — 판정이 죽었습니다.");
            Debug.Log($"[표면] 한 벌 {oneSet}종 · 두 벌 {twoList}종.");
        }

        [Test]
        public void 인계본에_없는_열네_종은_v1_규칙_그대로다()
        {
            var handoff = new HashSet<(EquipmentSlot, int)>(ByItem(ReadGolden()).Keys);
            int v1Items = 0;
            foreach (EquipmentSlot slot in BodySlots)
            {
                for (int item = 0; item < ItemCatalog.ItemCountIn(slot); item++)
                {
                    if (handoff.Contains((slot, item))) continue;
                    v1Items++;
                    Assert.IsFalse(AccessoryShapeBuilder.IsHandoffCode(slot, item), $"{slot} {item}: 골든에 없는데 IsHandoffCode 가 참입니다.");
                    List<AccessoryShapeBuilder.Shape> body = Build(slot, item, AccessorySurface.Body);
                    List<AccessoryShapeBuilder.Shape> card = Build(slot, item, AccessorySurface.Card);
                    Assert.AreEqual(body.Count, card.Count, $"{slot} {item}: v1 아이템은 몸/카드 조각 수가 같아야 합니다.");
                    for (int i = 0; i < body.Count; i++)
                    {
                        Assert.IsFalse(body[i].IsHandoff, $"{slot} {item} '{body[i].Name}': v1 아이템에 인계본 조각이 섞였습니다.");
                        Assert.AreEqual(AccessorySurfaces.Unset, body[i].Surfaces, $"{slot} {item} '{body[i].Name}': v1 조각은 미설정이어야 합니다.");
                    }
                    Assert.IsFalse(AccessoryCardIcon.HasDesignedCard(card), $"{slot} {item}: v1 인데 카드 경로가 「설계됨」으로 봅니다.");
                }
            }
            // 30종(몸 자리 5 × 6) − 인계본 16종 = 인계본에 없는 장비 8종 + 머리 6종.
            Assert.AreEqual(14, v1Items, "v1 아이템 수가 14(인계본 없는 장비 8종 + 머리 6종)와 다릅니다.");
        }

        // ============================================================================
        // 3. 데이터의 의미
        // ============================================================================

        /// <summary>★ underBack 은 <b>카드 사전 합성</b>의 밑색 참조다(§14-6 #4 「몸에서는 불필요」). 그래서 카드 표면 목록으로만 되짚는다 —
        /// 투명 렌즈의 카드 전용 워시 조각(팔레트 L-4)이 몸 목록에는 없어 몸에서 되짚으면 인덱스가 하나 어긋난다(몸은 그 값을 읽지 않는다).</summary>
        [Test]
        public void underBack은_기하로_되짚어도_같은_조각을_가리킨다()
        {
            var failures = new List<string>();
            int checkedPieces = 0;
            foreach (KeyValuePair<(EquipmentSlot, int), List<GoldenPiece>> kv in ByItem(ReadGolden()))
            {
                foreach (AccessorySurface surface in new[] { AccessorySurface.Card })
                {
                    List<AccessoryShapeBuilder.Shape> list = Build(kv.Key.Item1, kv.Key.Item2, surface);
                    for (int i = 0; i < list.Count; i++)
                    {
                        int derived = 0;
                        for (int j = 0; j < i; j++)
                        {
                            if (!list[j].Filled) continue;
                            int inside = 0;
                            for (int k = 0; k < list[i].Points.Length; k++) if (Contains(list[j].Points, list[i].Points[k])) inside++;
                            if (inside / (float)list[i].Points.Length >= 0.9f) derived = i - j;
                        }
                        checkedPieces++;
                        if (derived != list[i].UnderBack)
                            failures.Add($"{kv.Key.Item1} {kv.Key.Item2} [{surface}] '{list[i].Name}': 데이터 {list[i].UnderBack} / 기하 {derived}");
                    }
                }
            }
            Assert.Greater(checkedPieces, 0);
            Assert.IsEmpty(failures, "underBack 이 기하와 다릅니다 — 카드 사전 합성이 엉뚱한 밑색 위에 놓입니다:\n  " + string.Join("\n  ", failures));
        }

        [Test]
        public void 역할과_획_알파의_조합이_계약대로다()
        {
            foreach (GoldenPiece p in ReadGolden().Pieces)
            {
                string where = $"{p.Slot} {p.ItemConst} {p.Name}";
                Assert.IsTrue(AccessoryTone.IsKnown((byte)p.Tone), $"{where}: 모르는 역할 {p.Tone}");
                Assert.Greater(p.StrokeInR, 0f, $"{where}: 인계본 조각인데 명목 폭이 0 입니다.");
                Assert.Greater(p.StrokeMult, 0f, $"{where}: 획 배수가 0 입니다.");
                if (p.Tone == AccessoryTone.Highlight)
                {
                    Assert.IsFalse(p.Filled, $"{where}: 하이라이트는 채움이 아니라 선입니다(인계본 H).");
                    Assert.AreEqual(AccessoryTone.HighlightWhiteAlpha, p.LineAlpha, 1e-6f, $"{where}: 하이라이트 알파는 0.42 입니다.");
                }
                if (p.NoStroke) Assert.IsTrue(p.Filled, $"{where}: 선 없음은 채움에만 뜻이 있습니다(F/CF).");
                if (p.Filled) Assert.That(p.Alpha, Is.InRange(0.01f, 1f), $"{where}: 채움 알파 {p.Alpha}");
                if (!p.NoStroke) Assert.That(p.LineAlpha, Is.InRange(0.01f, 1f), $"{where}: 선 알파 {p.LineAlpha}");
                Assert.LessOrEqual(p.Points.Length, 128, $"{where}: 점 {p.Points.Length}개 — AccessoryCardIcon 점 버퍼(128)를 넘습니다.");
            }
        }

        [Test]
        public void 아이템_단위_몸_파라미터는_골든과_같고_날개_배낭만_그룹_알파를_갖는다()
        {
            foreach (GoldenItem it in ReadGolden().Items)
            {
                int item = ItemIndexOf(it.ItemConst);
                AccessoryWornTransform xf = AccessoryShapeBuilder.WornTransformOf(it.Slot, item);
                Assert.AreEqual(it.GroupAlpha, xf.GroupAlpha, 1e-6f, $"{it.Slot} {it.ItemConst}: 그룹 알파");
                Assert.AreEqual(it.Scale, xf.Scale, 1e-6f, $"{it.Slot} {it.ItemConst}: 배율");
                Assert.AreEqual(it.ScaleY, xf.ScaleY, 1e-6f, $"{it.Slot} {it.ItemConst}: 세로 압축");
                Assert.AreEqual(it.OffsetY, xf.OffsetYInR, 1e-6f, $"{it.Slot} {it.ItemConst}: 세로 오프셋");
                Assert.AreEqual(it.MirrorX, xf.MirrorX, $"{it.Slot} {it.ItemConst}: 반전");
                // ★ 팔레트 L-3(2026-09-05) — 「장비 전부 불투명」: 등 아이콘(날개·배낭)의 인계본 그룹 α0.40 은 1.0(= 0 없음)이 됐다.
                Assert.AreEqual(0f, xf.GroupAlpha, 1e-6f, $"{it.ItemConst}: 그룹 알파가 남아 있습니다 — 장비는 전부 불투명입니다(팔레트 L-3).");
                if (it.Slot == EquipmentSlot.Head)
                {
                    // R17 H-2 모자 맞춤(u > u_card)은 한 벌 모자(털모자)에서는 아이템 변환으로, 두 벌 모자(R19 2층: 챙 분할·왕관 뒷벽)에서는
                    // 몸 좌표(bodyFixed) 자체에 들어 있다. 어느 쪽이든 「몸이 카드보다 크다」는 실제 조각의 폭으로 잰다.
                    List<AccessoryShapeBuilder.Shape> body = Build(it.Slot, item, AccessorySurface.Body);
                    List<AccessoryShapeBuilder.Shape> card = Build(it.Slot, item, AccessorySurface.Card);
                    bool twoList = true;
                    foreach (AccessoryShapeBuilder.Shape s in body) twoList &= s.BodyFixed;
                    if (twoList)
                    {
                        Assert.IsFalse(xf.IsSet, $"{it.ItemConst}: 두 벌 모자(몸 좌표 그대로)에 아이템 변형이 남아 있습니다.");
                        Assert.Greater(WidthInR(body), WidthInR(card) * 1.05f, $"{it.ItemConst}: 몸의 모자가 카드보다 크지 않습니다(R17 H-2 u > u_card).");
                    }
                    else
                    {
                        Assert.Greater(xf.Scale, 1f, $"{it.ItemConst}: 모자는 머리에 맞춰 카드보다 커야 합니다(R17 H-2 u > u_card).");
                        Assert.IsTrue(xf.IsSet, $"{it.ItemConst}: 모자 맞춤 변형이 비어 있습니다.");
                    }
                }
            }
        }

        /// <summary>R17 능력 선반영 — 몸 표면 변형(배율·오프셋·반전)이 점열에 정확히 걸리고 카드는 파일 그대로인가.</summary>
        [Test]
        public void 몸_표면_변형은_머리_중심_기준이고_카드에는_걸리지_않는다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            var pts = new[] { rig.F(rig.HeadRadius, rig.HeadCenterY + rig.HeadRadius), rig.F(-0.5f * rig.HeadRadius, rig.HeadCenterY) };
            var xf = new AccessoryWornTransform(0f, 2f, 0.5f, 0.5f, true);
            Assert.IsTrue(xf.IsSet);
            AccessoryShapeBuilder.ApplyBodyTransform(pts, rig, xf);
            // (1R, +1R) → 반전 x -1R, 배율 2 → x -2R · y 배율 2×0.5 = 1 → +1R, 오프셋 +0.5R → y +1.5R
            Assert.AreEqual(-2f * rig.HeadRadius, pts[0].x, 1e-6f);
            Assert.AreEqual(rig.HeadCenterY + 1.5f * rig.HeadRadius, pts[0].y, 1e-6f);
            Assert.AreEqual(1f * rig.HeadRadius, pts[1].x, 1e-6f);
            Assert.AreEqual(rig.HeadCenterY + 0.5f * rig.HeadRadius, pts[1].y, 1e-6f);

            Assert.IsFalse(AccessoryWornTransform.None.IsSet);
            Assert.IsFalse(new AccessoryWornTransform(0.4f, 0f, 0f, 0f, false).IsSet, "그룹 알파만 있으면 점열은 안 움직입니다.");
        }

        // ============================================================================
        // 4. 카드 프레임
        // ============================================================================

        [Test]
        public void 카드_프레임은_인계본_선언값에서_유도된다()
        {
            Golden g = ReadGolden();
            Assert.AreEqual(4, g.Frames.Count, "골든 FRAME 줄이 4개(HEAD/EYES/NECK/BACK)가 아닙니다.");
            foreach (KeyValuePair<EquipmentSlot, (float UnitsPerR, float CenterYInR)> kv in g.Frames)
            {
                Assert.IsTrue(AccessoryCardIcon.Frame.TryGet(kv.Key, out float upr, out float cy), $"{kv.Key}: 프레임이 없습니다.");
                Assert.AreEqual(kv.Value.UnitsPerR, upr, 1e-4f, $"{kv.Key}: R당 아이콘 단위");
                Assert.AreEqual(kv.Value.CenterYInR, cy, 1e-4f, $"{kv.Key}: 아이콘 중심 y(R)");
            }
            Assert.IsFalse(AccessoryCardIcon.Frame.TryGet(EquipmentSlot.Hair, out _, out _), "머리에는 인계본 슬롯 박스가 없습니다.");
            Assert.IsFalse(AccessoryCardIcon.Frame.TryGet(EquipmentSlot.Fx, out _, out _));
            Assert.IsFalse(AccessoryCardIcon.Frame.TryGet(EquipmentSlot.Pet, out _, out _));
        }

        /// <summary>카드 표면 조각(아이콘)은 인계본 64 viewBox 안에 있어야 슬롯 고정 배율에서 카드 상자를 넘지 않는다.</summary>
        [Test]
        public void 카드_조각은_슬롯_프레임_안에_든다()
        {
            foreach (GoldenPiece p in ReadGolden().Pieces)
            {
                if (!AccessorySurfaces.IsOn((byte)p.Surfaces, AccessorySurface.Card)) continue;
                Assert.IsTrue(AccessoryCardIcon.Frame.TryGet(p.Slot, out float upr, out float cy));
                float half = AccessoryCardIcon.Frame.IconViewBox * 0.5f;
                for (int i = 0; i < p.Points.Length; i++)
                {
                    float xi = p.Points[i].x * upr;
                    float yi = (p.Points[i].y - cy) * upr;
                    Assert.LessOrEqual(Mathf.Abs(xi), half + 1e-3f, $"{p.Slot} {p.ItemConst} {p.Name} {i}번 점 x={xi:F2}u");
                    Assert.LessOrEqual(Mathf.Abs(yi), half + 1e-3f, $"{p.Slot} {p.ItemConst} {p.Name} {i}번 점 y={yi:F2}u");
                }
            }
        }

        // ============================================================================
        // 원칙 1 — 설명문이 주장하는 동작이 데이터에 있다: 월요일 느슨함 · 흔들 구간
        // ============================================================================

        /// <summary>
        /// 줄무늬타이 "월요일마다 조금 느슨해진다" — 상태 켜짐에서 <b>모든 몸 조각의 모든 점</b>이 골든 STATE 만큼 내려가고 x 는 그대로다.
        /// 나머지 15종은 상태에 반응하지 않는다. 카드는 상태를 모른다(언제나 켜짐 아님).
        /// </summary>
        [Test]
        public void 줄무늬타이는_상태_켜짐에서_골든_STATE_만큼_통째로_내려가고_다른_아이템은_안_움직인다()
        {
            Golden g = ReadGolden();
            Assert.AreEqual(1, g.StateDrops.Count, "골든 STATE 줄이 하나(줄무늬타이)가 아닙니다.");
            AccessoryShapeBuilder.Rig rig = Rig();

            foreach (GoldenItem it in g.Items)
            {
                int item = ItemIndexOf(it.ItemConst);
                var off = new List<AccessoryShapeBuilder.Shape>();
                var on = new List<AccessoryShapeBuilder.Shape>();
                AccessoryShapeBuilder.Append(off, it.Slot, item, rig, float.PositiveInfinity, 0f, mondayLoosened: false);
                AccessoryShapeBuilder.Append(on, it.Slot, item, rig, float.PositiveInfinity, 0f, mondayLoosened: true);
                Assert.AreEqual(off.Count, on.Count, $"{it.ItemConst}: 상태에 따라 조각 수가 다릅니다.");

                float wantDrop = g.StateDrops.TryGetValue((it.Slot, it.ItemConst), out float d) ? -d * rig.HeadRadius : 0f;
                for (int s = 0; s < off.Count; s++)
                {
                    Assert.AreEqual(off[s].Points.Length, on[s].Points.Length);
                    for (int i = 0; i < off[s].Points.Length; i++)
                    {
                        Assert.AreEqual(off[s].Points[i].x, on[s].Points[i].x, 1e-6f, $"{it.ItemConst} {off[s].Name} {i}번 점 x 가 상태에 따라 움직입니다.");
                        Assert.AreEqual(off[s].Points[i].y + wantDrop, on[s].Points[i].y, 1e-5f,
                            $"{it.ItemConst} {off[s].Name} {i}번 점 y: 상태 켜짐 차 {on[s].Points[i].y - off[s].Points[i].y:F5} ≠ 골든 {wantDrop:F5}");
                    }
                }
            }

            // 양은 R 배수다 — 반 배율 리그에서 반이 된다(절대 상수로 굳지 않았다).
            AccessoryShapeBuilder.Rig half = new AccessoryShapeBuilder.Rig(rig.HeadRadius * 0.5f, rig.HeadCenterY * 0.5f,
                rig.ShoulderY * 0.5f, rig.HipY * 0.5f, rig.Facing);
            var hOff = new List<AccessoryShapeBuilder.Shape>();
            var hOn = new List<AccessoryShapeBuilder.Shape>();
            AccessoryShapeBuilder.Append(hOff, EquipmentSlot.Neck, AccessoryShapeBuilder.NeckStriped, half, float.PositiveInfinity, 0f, mondayLoosened: false);
            AccessoryShapeBuilder.Append(hOn, EquipmentSlot.Neck, AccessoryShapeBuilder.NeckStriped, half, float.PositiveInfinity, 0f, mondayLoosened: true);
            float halfDrop = hOn[0].Points[0].y - hOff[0].Points[0].y;
            Assert.AreEqual(-g.StateDrops[(EquipmentSlot.Neck, "NeckStriped")] * half.HeadRadius, halfDrop, 1e-5f, "느슨해지는 양이 R 배수가 아닙니다.");
        }

        private static int SlotOrder(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Head: return AccessoryShapeBuilder.SortHead;
                case EquipmentSlot.Eyes: return AccessoryShapeBuilder.SortEyes;
                case EquipmentSlot.Neck: return AccessoryShapeBuilder.SortNeck;
                case EquipmentSlot.Shoulders: return AccessoryShapeBuilder.SortBack;
                default: return AccessoryShapeBuilder.SortDefault;
            }
        }

        private static float WidthInR(List<AccessoryShapeBuilder.Shape> shapes)
        {
            float min = float.MaxValue, max = float.MinValue;
            foreach (AccessoryShapeBuilder.Shape s in shapes)
                foreach (Vector3 p in s.Points) { min = Mathf.Min(min, p.x); max = Mathf.Max(max, p.x); }
            return (max - min) / Rig().HeadRadius;
        }

        /// <summary>
        /// R17c 단일 출처 — 몸 외알안경의 알(CB0) 중심 x 는 <b>반대쪽 눈(ExposedEye) 중심 x 의 부호 반전</b>이다
        /// (모델 <c>MONOCLE_LENS_CX_R = −EYE_X</c>). C# 에는 그 숫자가 없고 좌표 배열만 있으므로 관계를 여기서 잰다.
        /// 카드는 반전 없이 원문(알이 보는 사람 왼쪽)이다.
        /// </summary>
        [Test]
        public void 외알안경_몸의_알_중심은_반대쪽_눈_중심의_거울이고_카드는_원문_그대로다()
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            List<AccessoryShapeBuilder.Shape> body = Build(EquipmentSlot.Eyes, AccessoryShapeBuilder.EyesMonocle, AccessorySurface.Body);
            List<AccessoryShapeBuilder.Shape> card = Build(EquipmentSlot.Eyes, AccessoryShapeBuilder.EyesMonocle, AccessorySurface.Card);

            float CenterX(List<AccessoryShapeBuilder.Shape> list, System.Predicate<AccessoryShapeBuilder.Shape> pick, string what)
            {
                int hits = 0; float min = float.MaxValue, max = float.MinValue;
                foreach (AccessoryShapeBuilder.Shape s in list)
                {
                    if (!pick(s)) continue;
                    hits++;
                    foreach (Vector3 p in s.Points) { min = Mathf.Min(min, p.x); max = Mathf.Max(max, p.x); }
                }
                Assert.AreEqual(1, hits, $"{what} 조각이 하나가 아닙니다({hits}).");
                return (min + max) * 0.5f / rig.HeadRadius;
            }

            // 알(CB0)은 팔레트 L-4 로 「테」(선만, 양면)와 「워시」(카드 전용 채움)로 갈렸다 — 중심은 테로 잰다.
            float eyeX = CenterX(body, s => s.Tone == AccessoryTone.InkContrast, "반대쪽 눈");
            float lensBodyX = CenterX(body, s => s.Name == "Piece_CB0" && !s.Filled, "몸 알 테");
            float lensCardX = CenterX(card, s => s.Name == "Piece_CB0" && !s.Filled, "카드 알 테");
            // R20 — 유리 판(Glass): 몸의 렌즈 채움은 잉크 위 M2 α 사전 합성 불투명 판이고 선이 없다(테가 따로 그린다).
            int glass = 0;
            foreach (AccessoryShapeBuilder.Shape s in body)
            {
                if (s.Tone != AccessoryTone.Glass) continue;
                glass++;
                Assert.IsTrue(s.Filled && s.NoStroke, $"'{s.Name}': 유리 판은 채움만(선 없음)이어야 합니다.");
                Assert.That(s.Alpha, Is.InRange(0.01f, 0.99f), $"'{s.Name}': 유리 판의 합성 비율이 (0,1) 밖입니다.");
            }
            Assert.AreEqual(1, glass, "몸 외알안경의 유리 판이 정확히 하나여야 합니다(R20).");

            Assert.Less(eyeX, 0f, "반대쪽 눈이 보는 사람 왼쪽(−x)이 아닙니다(E-1).");
            Assert.AreEqual(-eyeX, lensBodyX, 1e-3f, $"몸 알 중심 x {lensBodyX:F4} R 이 눈 중심 x {eyeX:F4} R 의 부호 반전이 아닙니다.");
            Assert.Less(lensCardX, 0f, "카드 알은 원문(보는 사람 왼쪽)이어야 합니다 — 카드는 반전하지 않는다.");
            foreach (AccessoryShapeBuilder.Shape s in card) Assert.AreNotEqual(AccessoryTone.InkContrast, s.Tone, "카드에 반대쪽 눈이 나왔습니다(몸 전용 조각).");
        }

        private static bool Contains(Vector3[] poly, Vector3 p)
        {
            bool inside = false;
            int n = poly.Length;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = poly[i], b = poly[(i + 1) % n];
                if ((a.y > p.y) != (b.y > p.y))
                {
                    float x = a.x + (p.y - a.y) * (b.x - a.x) / (b.y - a.y);
                    if (p.x < x) inside = !inside;
                }
            }
            return inside;
        }
    }
}
