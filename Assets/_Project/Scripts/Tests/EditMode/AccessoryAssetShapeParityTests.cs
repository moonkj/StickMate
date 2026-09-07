using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Interaction;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ============================================================================
    /// ★ 두 경로가 <b>같은 답</b>을 내는가 — 코드 표 ↔ 에셋 스트림 (2026-09-07 P6)
    /// ============================================================================
    /// 리더 지시: <i>"코드 switch 경로를 당장 지우지 마라. 에셋 경로가 같은 답을 낸다는 것이
    /// 증명되기 전에 지우면, 틀렸을 때 되돌릴 기준이 사라진다. 두 경로가 같은 답을 내는지 대조하는
    /// 테스트를 먼저 세우고, 그 다음에 전환 여부를 리더에게 물어라."</i>
    ///
    /// 이 파일이 그 대조다. 대상은 <b>인계본 16종</b>(HEAD 4 · EYES 4 · BACK 4) — 지금 화면에 실제로
    /// 그려지는 좌표를 그대로 쓴다. 합성 도형으로 재면 "우리가 만든 쉬운 도형에서만 맞는" 결과가 된다.
    ///
    /// ============================================================================
    /// 어떻게 순환을 피했는가 — 이 문단이 이 파일의 핵심이다
    /// ============================================================================
    /// 기대값을 프로덕션 함수로 만들면 그 함수가 틀어질 때 기대값도 함께 틀어져 <b>아무것도 못 잰다</b>
    /// (TEAM.md 「생성기와 검사기가 같이 틀린다」). 그래서 <b>계수를 계산하지 않고 읽어 낸다</b>:
    ///
    /// <list type="number">
    ///  <item><b>R = 1 · 머리중심 = 0 · 정면</b>인 릭으로 코드 표를 한 번 돌린다. 그 릭에서
    ///    <c>HandoffPiece</c>의 식 <c>rig.F(c·r, hc + c·r)</c>은 <c>(c·1·1, 0 + c·1)</c>이라
    ///    <b>나눗셈 없이</b> 원본 R 배수 <c>c</c>가 그대로 나온다(부동소수 손실 0).</item>
    ///  <item>그 <c>c</c>로 <b>스트림</b>을 적는다 — <c>x = [R × c]</c>, <c>y = [HC] + [R × c]</c>.
    ///    이것이 팩 작성자가 <c>.asset</c>에 적을 바로 그 형태다.</item>
    ///  <item><b>다른 릭</b>(출하 치수 · 좌우 양방향)에서 두 경로를 각각 돌려 <b>비트로</b> 대조한다.
    ///    한쪽은 <c>HandoffPiece</c>의 곱셈, 다른 쪽은 <c>AccessoryWornShapeReader</c>의 항 누적이라
    ///    <b>산술 경로가 서로 다르다</b> — 같이 틀릴 수 없다.</item>
    /// </list>
    ///
    /// <para><b>왜 이것이 「16종을 에셋으로 옮겨도 된다」의 증명인가</b>: 생성 파일의
    /// <c>AppendHandoffHead/Eyes/Shoulders</c>는 <c>HandoffPiece</c> 호출의 <b>나열일 뿐</b>이다.
    /// 조각 하나가 비트까지 같으면 아이템 한 벌도 같다.</para>
    ///
    /// ============================================================================
    /// 양성 대조 (이 둘이 없으면 위 초록은 아무것도 증명하지 않는다)
    /// ============================================================================
    ///  · <b>0개를 돌고 초록</b>이 아닌가 — 비교한 아이템·조각·점 수를 세어 하한을 건다.
    ///  · <b>움직임을 실제로 감지</b>하는가 — 계수 한 자리를 1 ULP 밀면 반드시 빨개진다.
    /// </summary>
    public sealed class AccessoryAssetShapeParityTests
    {
        /// <summary>코드 표가 좌표를 갖는 자리. <b>여기 이름을 적지 않는다</b> —
        /// <see cref="AccessoryShapeBuilder.IsHandoffCode"/>에게 묻는다(표가 늘면 이 검사도 함께 는다).</summary>
        private static readonly EquipmentSlot[] CodeSlots =
        {
            EquipmentSlot.Head, EquipmentSlot.Eyes, EquipmentSlot.Shoulders,
        };

        // ====================================================================
        // 릭 — 뽑아내는 릭(R=1, HC=0)과 재는 릭(출하 치수)은 <b>달라야</b> 한다.
        //      같으면 "배율에 무관한가"를 구조적으로 못 본다.
        // ====================================================================

        private static AccessoryShapeBuilder.Rig UnitRig()
            => new AccessoryShapeBuilder.Rig(1f, 0f,
                AccessoryShapeBuilder.BaselineShoulderLocalY,
                AccessoryShapeBuilder.BaselineHipLocalY, +1f);

        private static AccessoryShapeBuilder.Rig ShippingRig(float scale, float facing)
        {
            float r = AccessoryShapeBuilder.BaselineHeadVisualRadius * scale;
            float hc = (StickConfig.BaselineCharacterTotalHeight * scale) - r;
            return new AccessoryShapeBuilder.Rig(r, hc,
                AccessoryShapeBuilder.BaselineShoulderLocalY * scale,
                AccessoryShapeBuilder.BaselineHipLocalY * scale, facing);
        }

        /// <summary>배율은 <b>서로 다르다</b>는 것에만 뜻이 있다(정수배·상용·어중간을 섞는다) —
        /// <c>WornShapeDataGoldenTests</c>의 릭 10벌과 같은 사고방식이다.</summary>
        private static readonly float[] Scales = { 1f, 0.75f, 1.37f, 0.4123f };

        // ====================================================================
        // 스트림 인코더 — 「팩 작성자가 .asset 에 적을 형태」를 그대로 만든다
        // ====================================================================

        /// <summary>R 배수 점열 -> 항 스트림. 문법은 <see cref="AccessoryWornShapeData"/> 문단 그대로다.
        /// <para>계수를 <b>미리 곱하지 않는다</b> — 곱하면 마지막 비트가 갈린다(그 함정이 이 계약의
        /// 존재 이유다). 여기서 하는 일은 <b>배치</b>뿐이고 산술은 판독기가 한다.</para></summary>
        private static float[] Encode(Vector3[] pointsInR)
        {
            var terms = new List<float> { pointsInR.Length };
            foreach (Vector3 p in pointsInR)
            {
                // x = 머리 반경 × cx
                terms.Add(1f);
                terms.Add((float)AccessoryWornBasis.HeadRadius);
                terms.Add((float)AccessoryWornGate.Always);
                terms.Add((float)AccessoryWornTrig.None);
                terms.Add(1f);
                terms.Add(p.x);

                // y = 머리 중심선 + 머리 반경 × cy
                terms.Add(2f);
                terms.Add((float)AccessoryWornBasis.HeadCenterLine);
                terms.Add((float)AccessoryWornGate.Always);
                terms.Add((float)AccessoryWornTrig.None);
                terms.Add(0f);
                terms.Add((float)AccessoryWornBasis.HeadRadius);
                terms.Add((float)AccessoryWornGate.Always);
                terms.Add((float)AccessoryWornTrig.None);
                terms.Add(1f);
                terms.Add(p.y);
            }
            return terms.ToArray();
        }

        /// <summary>코드 표가 낸 조각 하나 -> 에셋이 적었을 형태(<see cref="AccessoryWornShapeData"/>).
        /// <b>계약 v2 필드를 한 칸도 빠뜨리지 않는다</b> — 빠뜨리면 그 축이 「둘 다 0」이라 조용히 통과한다.</summary>
        private static AccessoryWornShapeData ToData(in AccessoryShapeBuilder.Shape s, Vector3[] pointsInR)
            => new AccessoryWornShapeData
            {
                name = s.Name,
                loop = s.Loop,
                filled = s.Filled,
                tone = s.Tone,
                swayStart = s.SwayStart,
                swayCount = s.SwayCount,
                swingDegrees = 0f,
                surfaces = s.Surfaces,
                strokeMult = s.StrokeMult,
                strokeInR = s.StrokeInR,
                noStroke = s.NoStroke,
                alpha = s.Alpha,
                lineAlpha = s.LineAlpha,
                underBack = s.UnderBack,
                layer = s.Layer,
                bodyFixed = s.BodyFixed,
                terms = Encode(pointsInR),
            };

        // ====================================================================
        // 1. 본 대조
        // ====================================================================

        [Test]
        public void 코드_표가_가진_전부는_에셋_스트림으로_적어도_비트까지_같다()
        {
            int items = 0, shapes = 0, points = 0;
            var failures = new StringBuilder();

            foreach (EquipmentSlot slot in CodeSlots)
            {
                Assert.IsTrue(AccessoryShapeBuilder.TryWornAssetSlotOrder(slot, out int slotOrder),
                    $"{slot}이 에셋 조형을 받는 자리가 아닙니다 — 이 라운드가 넓힌 자리 목록에서 빠졌습니다.");

                int count = ItemCatalog.ItemCountIn(slot);
                Assert.Greater(count, 0, $"{slot} 카테고리가 비었습니다 — 아래 순회가 공허합니다.");

                for (int item = 0; item < count; item++)
                {
                    if (!AccessoryShapeBuilder.IsHandoffCode(slot, item)) continue;
                    items++;

                    // (1) R=1 · HC=0 에서 원본 R 배수를 손실 없이 뽑는다(카드 요청 = 아이템 변형 없음).
                    var raw = new List<AccessoryShapeBuilder.Shape>();
                    Assert.IsTrue(
                        AccessoryShapeBuilder.AppendCodeShapes(raw, slot, item, UnitRig(), AccessorySurface.Card),
                        $"{slot} {item}번을 코드 표가 안 그렸습니다 — IsHandoffCode 와 실제 표가 갈라졌습니다.");
                    Assert.Greater(raw.Count, 0, $"{slot} {item}번의 코드 표 조각이 0개입니다.");

                    // (2) 에셋이 적었을 형태로 옮긴다.
                    var data = new AccessoryWornShapeData[raw.Count];
                    for (int i = 0; i < raw.Count; i++) data[i] = ToData(raw[i], raw[i].Points);

                    AccessoryWornTransform xf = AccessoryShapeBuilder.WornTransformCode(slot, item);

                    // (3) 다른 릭 · 두 표면 · 양방향에서 대조한다.
                    foreach (float scale in Scales)
                    {
                        foreach (float facing in new[] { +1f, -1f })
                        {
                            AccessoryShapeBuilder.Rig rig = ShippingRig(scale, facing);
                            foreach (AccessorySurface surface in new[] { AccessorySurface.Body, AccessorySurface.Card })
                            {
                                var code = new List<AccessoryShapeBuilder.Shape>();
                                AccessoryShapeBuilder.AppendCodeShapes(code, slot, item, rig, surface);

                                var asset = new List<AccessoryShapeBuilder.Shape>();
                                AccessoryShapeBuilder.AppendWornData(asset, data, xf, slot, item, rig,
                                    slotOrder, false, surface);

                                string where = $"{slot} {item}번 · 배율 {scale} · 방향 {facing} · {surface}";
                                if (code.Count != asset.Count)
                                {
                                    failures.AppendLine($"{where}: 조각 수 {code.Count} vs {asset.Count}");
                                    continue;
                                }

                                for (int i = 0; i < code.Count; i++)
                                {
                                    shapes++;
                                    points += Compare(code[i], asset[i], $"{where} · 조각 {i}", failures);
                                }
                            }
                        }
                    }
                }
            }

            // ★ 양성 대조 (가) — 0개를 돌고 초록이 아닌가, 그리고 <b>빠뜨린 자리가 없는가</b>.
            //   숫자를 베끼지 않는다: 코드 표가 좌표를 갖는 아이템을 <b>7자리 전수</b>로 다시 세어
            //   위 순회가 그 전부를 소진했는지 본다. CodeSlots 에서 한 자리가 빠지면 여기서 걸린다.
            //   (문서의 「인계본 16종」은 NECK 4를 포함한 수다 — 그 넷은 이미 에셋이라 코드 표에 없다.)
            int codeTableItems = 0;
            for (int s = 0; s < EquipmentModel.SlotCount; s++)
            {
                var slot = (EquipmentSlot)s;
                int count = ItemCatalog.ItemCountIn(slot);
                for (int item = 0; item < count; item++)
                {
                    if (AccessoryShapeBuilder.IsHandoffCode(slot, item)) codeTableItems++;
                }
            }
            Assert.Greater(codeTableItems, 0,
                "코드 표가 좌표를 갖는 아이템이 하나도 없습니다 — 대조할 대상이 없다면 이 검사는 껍데기입니다.");
            Assert.AreEqual(codeTableItems, items,
                $"코드 표는 {codeTableItems}종을 갖는데 {items}종만 대조했습니다 — " +
                "CodeSlots 목록이 실제 표보다 좁습니다(빠진 자리의 아이템은 아무도 안 잽니다).");
            Assert.Greater(shapes, 0, "조각을 하나도 대조하지 않았습니다 — 이 초록은 아무것도 증명하지 않습니다.");
            Assert.Greater(points, 0, "점을 하나도 대조하지 않았습니다 — 이 초록은 아무것도 증명하지 않습니다.");

            if (failures.Length > 0)
            {
                Assert.Fail($"코드 표와 에셋 스트림이 갈라졌습니다 " +
                    $"(대조 {items}종 / 조각 {shapes} / 점 {points}):\n{failures}");
            }
        }

        /// <summary>조각 하나를 <b>전 필드</b> 대조하고 대조한 점 수를 돌려준다.
        /// <para>좌표는 비트로 본다 — "거의 같다"는 이 이사에서 아무 뜻이 없다
        /// (<c>WornShapeDataGoldenTests</c> 문단의 실측: 계수를 미리 곱하면 일부 배율에서만 갈린다).</para></summary>
        private static int Compare(in AccessoryShapeBuilder.Shape a, in AccessoryShapeBuilder.Shape b,
            string where, StringBuilder failures)
        {
            void Eq(object x, object y, string field)
            {
                if (!Equals(x, y)) failures.AppendLine($"{where}: {field} {x} vs {y}");
            }

            Eq(a.Name, b.Name, "이름");
            Eq(a.Loop, b.Loop, "고리");
            Eq(a.Filled, b.Filled, "채움");
            Eq(a.Tone, b.Tone, "역할");
            Eq(a.SortingOrder, b.SortingOrder, "정렬번호");
            Eq(a.SwayStart, b.SwayStart, "흔들시작");
            Eq(a.SwayCount, b.SwayCount, "흔들개수");
            Eq(a.Surfaces, b.Surfaces, "표면");
            Eq(Bits(a.StrokeMult), Bits(b.StrokeMult), "획배수");
            Eq(Bits(a.StrokeInR), Bits(b.StrokeInR), "획명목폭");
            Eq(a.NoStroke, b.NoStroke, "선없음");
            Eq(Bits(a.Alpha), Bits(b.Alpha), "채움알파");
            Eq(Bits(a.LineAlpha), Bits(b.LineAlpha), "선알파");
            Eq(a.UnderBack, b.UnderBack, "밑색거리");
            Eq(a.Layer, b.Layer, "층");
            Eq(a.BodyFixed, b.BodyFixed, "몸고정");

            if (a.Points == null || b.Points == null || a.Points.Length != b.Points.Length)
            {
                failures.AppendLine($"{where}: 점 수 {a.Points?.Length} vs {b.Points?.Length}");
                return 0;
            }

            for (int i = 0; i < a.Points.Length; i++)
            {
                if (Bits(a.Points[i].x) != Bits(b.Points[i].x) || Bits(a.Points[i].y) != Bits(b.Points[i].y)
                    || Bits(a.Points[i].z) != Bits(b.Points[i].z))
                {
                    // ★★ 2026-09-08 — <b>비트와 ULP를 함께 찍는다.</b> 그 전에는 float 를 기본 서식으로
                    //   인쇄해서, 1 ULP 차이가 <b>두 값이 똑같아 보이는</b> 실패 메시지를 냈다
                    //   (실제 사고: <c>2.383196 vs 2.383196</c> — 받은 사람이 원인을 볼 방법이 없었다).
                    //   이 대조의 기준이 애초에 <b>비트</b>이므로, 사람에게도 비트를 보여 주는 것이 옳다.
                    failures.AppendLine($"{where}: 점 {i}" +
                        Axis("x", a.Points[i].x, b.Points[i].x) +
                        Axis("y", a.Points[i].y, b.Points[i].y) +
                        Axis("z", a.Points[i].z, b.Points[i].z));
                }
            }
            return a.Points.Length;
        }

        /// <summary>축 하나의 차이를 <b>비트 · ULP · 십진</b>으로. 같으면 빈 문자열이다 —
        /// 안 갈린 축까지 찍으면 갈린 축이 묻힌다.</summary>
        private static string Axis(string name, float x, float y)
            => Bits(x) == Bits(y)
                ? string.Empty
                : $"  {name}: {Bits(x):X8} vs {Bits(y):X8} (Δ{Ulps(x, y)} ULP) [{x:R} vs {y:R}]";

        /// <summary>float 의 <b>비트</b>. <c>WornShapeDataGoldenTests</c>와 같은 방식을 쓴다 —
        /// 이 어셈블리에서 이미 도는 것이 확인된 API 다.</summary>
        private static uint Bits(float v) => System.BitConverter.ToUInt32(System.BitConverter.GetBytes(v), 0);

        /// <summary>두 float 사이의 ULP 거리(부호 포함). 「얼마나 다른가」를 십진 자릿수가 아니라
        /// <b>표현 가능한 값 몇 칸</b>으로 말한다 — 1이면 이웃한 float 이고, 그건 산술 경로 차이의
        /// 지문이다(계수 오기입은 대개 훨씬 크다).</summary>
        private static int Ulps(float x, float y)
        {
            int ix = System.BitConverter.ToInt32(System.BitConverter.GetBytes(x), 0);
            int iy = System.BitConverter.ToInt32(System.BitConverter.GetBytes(y), 0);
            if (ix < 0) ix = int.MinValue - ix;
            if (iy < 0) iy = int.MinValue - iy;
            return ix - iy;
        }

        // ====================================================================
        // 2. ★ 양성 대조 (나) — 이 대조가 <b>움직임을 실제로 감지</b>하는가
        // ====================================================================

        /// <summary>계수 한 자리를 <b>1 ULP</b> 밀면 위 대조가 반드시 빨개진다.
        ///
        /// <para>★ <b>첫 판이 조용히 통과했다</b>(2026-09-07 오프라인 예행에서 자기 발견). 첫 계수를
        /// 1 ULP 밀었더니 <b>좌표가 안 움직였고</b>, 그래서 대조도 안 물었는데 그 출력은 「대조가 죽었다」와
        /// 똑같이 생겼다. 이유는 산술이다: <c>x = R × c</c>에서 <c>c</c>의 1 ULP(≈5.96e−8)에 <c>R</c>(≈0.22)이
        /// 곱해지면 곱의 1 ULP보다 <b>작아질 수 있어</b> 같은 float 로 반올림된다.</para>
        ///
        /// <para>그래서 「첫 계수」가 아니라 <b>「좌표를 실제로 움직이는 첫 계수」</b>를 찾는다. 초록을 찾을
        /// 때까지 뒤지는 것이 아니라, <b>질문이 성립하는 자리</b>를 찾는 것이다 — 움직이지 않은 계수에 대고
        /// "왜 안 잡았냐"고 묻는 것은 애초에 틀린 질문이다. 그 구분을 <c>moved</c>/<c>detectable</c> 두 수로
        /// 함께 찍어 <b>탐색이 몇 칸에서 성립했는지</b>를 남긴다.</para></summary>
        [Test]
        public void 좌표를_움직이는_계수를_1ULP_밀면_대조가_빨개진다()
        {
            const EquipmentSlot slot = EquipmentSlot.Head;
            int item = FirstHandoffItem(slot);

            var raw = new List<AccessoryShapeBuilder.Shape>();
            Assert.IsTrue(AccessoryShapeBuilder.AppendCodeShapes(raw, slot, item, UnitRig(), AccessorySurface.Card));
            Assert.Greater(raw.Count, 0);

            AccessoryShapeBuilder.Rig rig = ShippingRig(1f, +1f);
            AccessoryWornTransform xf = AccessoryShapeBuilder.WornTransformCode(slot, item);
            Assert.IsTrue(AccessoryShapeBuilder.TryWornAssetSlotOrder(slot, out int slotOrder));

            var code = new List<AccessoryShapeBuilder.Shape>();
            AccessoryShapeBuilder.AppendCodeShapes(code, slot, item, rig, AccessorySurface.Body);
            Assert.Greater(code.Count, 0);

            float[] clean = ToData(raw[0], raw[0].Points).terms;
            int scanned = 0, moved = -1, compared = 0;
            var failures = new StringBuilder();

            for (int i = 0; i < clean.Length && moved < 0; i++)
            {
                // 개수 자리(정수)를 건드리면 문법이 깨져 <b>다른 이유로</b> 실패한다.
                if (clean[i] == Mathf.Round(clean[i])) continue;
                scanned++;

                var data = new AccessoryWornShapeData[raw.Count];
                for (int k = 0; k < raw.Count; k++) data[k] = ToData(raw[k], raw[k].Points);
                data[0].terms[i] = NextUp(data[0].terms[i]);

                var nudged = new List<AccessoryShapeBuilder.Shape>();
                AccessoryShapeBuilder.AppendWornData(nudged, data, xf, slot, item, rig, slotOrder, false,
                    AccessorySurface.Body);
                Assert.AreEqual(code.Count, nudged.Count);

                // 좌표가 <b>실제로</b> 움직였는가. 안 움직였으면 이 계수는 질문 자체가 성립하지 않는다.
                bool changed = false;
                for (int p = 0; p < nudged[0].Points.Length && !changed; p++)
                {
                    changed = Bits(code[0].Points[p].x) != Bits(nudged[0].Points[p].x)
                           || Bits(code[0].Points[p].y) != Bits(nudged[0].Points[p].y);
                }
                if (!changed) continue;

                moved = i;
                failures.Clear();
                compared = 0;
                for (int k = 0; k < code.Count; k++)
                {
                    compared += Compare(code[k], nudged[k], $"조각 {k}", failures);
                }
            }

            Assert.Greater(scanned, 0, "계수 자리를 하나도 못 찾았습니다 — 스트림이 정수뿐일 수 없습니다.");
            Assert.GreaterOrEqual(moved, 0,
                $"계수 {scanned}개를 전부 1 ULP 밀었는데 좌표가 한 번도 안 움직였습니다 — " +
                "스트림이 계수를 안 읽고 있을 수 있습니다(그러면 위 본 대조도 뜻이 없습니다).");
            Assert.Greater(compared, 0, "점을 하나도 대조하지 않았습니다 — 이 양성 대조가 공허합니다.");
            Assert.Greater(failures.Length, 0,
                $"계수 {moved}번을 1 ULP 밀어 좌표가 실제로 움직였는데 대조가 아무것도 못 잡았습니다 — " +
                "본 대조의 초록은 아무 뜻이 없습니다.");
        }

        /// <summary>비트를 하나 올린 값 = <b>1 ULP</b> 차이(부호와 무관하게 「크기 증가」쪽).
        /// <c>Mathf</c>에 없는 연산이라 비트로 만든다.</summary>
        private static float NextUp(float v)
        {
            uint bits = Bits(v);
            return System.BitConverter.ToSingle(System.BitConverter.GetBytes(bits + 1u), 0);
        }

        private static int FirstHandoffItem(EquipmentSlot slot)
        {
            int count = ItemCatalog.ItemCountIn(slot);
            for (int i = 0; i < count; i++)
            {
                if (AccessoryShapeBuilder.IsHandoffCode(slot, i)) return i;
            }
            Assert.Fail($"{slot}에 코드 표 아이템이 하나도 없습니다.");
            return -1;
        }

        // ====================================================================
        // 3. 뽑아내는 릭이 <b>정말로</b> 손실 없는가 — 위 (1)단계의 전제 자체를 잰다
        // ====================================================================

        /// <summary>R=1 · HC=0 릭에서 <c>HandoffPiece</c>의 출력이 원본 R 배수와 <b>비트까지</b> 같은가.
        /// <para>이 전제가 깨지면 위 대조는 「원본」이 아니라 「한 번 어긋난 값」끼리 비교하는 것이 되고,
        /// 그래도 <b>양쪽이 같이 어긋나므로 초록이 된다</b> — 이 저장소가 열 번째로 당한 형태 그대로다.
        /// 그래서 전제를 따로 잰다: 릭 배율만 바꿔 두 번 뽑아 <b>비례 관계</b>가 정확히 성립하는지 본다.</para></summary>
        [Test]
        public void 단위릭에서_뽑은_계수는_배율릭_좌표를_정확히_재현한다()
        {
            const EquipmentSlot slot = EquipmentSlot.Eyes;
            int item = FirstHandoffItem(slot);

            var unit = new List<AccessoryShapeBuilder.Shape>();
            Assert.IsTrue(AccessoryShapeBuilder.AppendCodeShapes(unit, slot, item, UnitRig(), AccessorySurface.Card));

            AccessoryShapeBuilder.Rig rig = ShippingRig(0.75f, +1f);
            var scaled = new List<AccessoryShapeBuilder.Shape>();
            AccessoryShapeBuilder.AppendCodeShapes(scaled, slot, item, rig, AccessorySurface.Card);

            Assert.AreEqual(unit.Count, scaled.Count);
            int checkedPoints = 0;
            for (int i = 0; i < unit.Count; i++)
            {
                Vector3[] u = unit[i].Points;
                Vector3[] s = scaled[i].Points;
                Assert.AreEqual(u.Length, s.Length);
                for (int k = 0; k < u.Length; k++)
                {
                    // ★★ 2026-09-08 — 기대식의 <b>곱을 지역 float 에 접는다</b>(AccessoryShapeBuilder.
                    //   HandoffPiece 의 같은 날짜 주석과 <b>한 쌍</b>이다). 허용치를 두는 것이 아니라
                    //   <b>반올림 횟수를 프로덕션과 같게</b> 맞추는 것이고, 비교는 여전히 비트 단위다.
                    //
                    //   왜 필요한가: 프로덕션은 에셋 판독기(AccessoryWornShapeReader.ReadSum, `v *= coef`
                    //   후 덧셈 = <b>반올림 2회</b>)와 비트까지 같아야 한다는 계약 때문에 곱을 지역 float 에
                    //   접었다. 그런데 이 기대식은 `HC + R * u.y`라는 <b>한 식</b>이라 Mono JIT 이 융합
                    //   곱셈-덧셈(FMA)으로 계약할 수 있어 <b>반올림 1회</b>였다 — 같은 수식인데 다른 비트가
                    //   나온다. 실측: 선글라스 조각 0 점 14에서 1070226377 vs 1070226378(1 ULP)로 갈렸고,
                    //   오프라인 CoreCLR 하니스에서는 재현되지 않았다(런타임 의존).
                    //
                    //   ★ x 는 접을 것이 없다 — `R * u.x` 는 곱 하나(반올림 1회)이고 프로덕션의
                    //     `px = xyInR[..] * r` 과 연산 수가 이미 같다(rig.F 의 `* Facing` 은 여기서 +1f 라 정확).
                    //   ★ <b>「불필요한 임시 변수」로 보고 인라인하지 마라</b> — 인라인하는 순간 계약이 다시
                    //     깨지고, 증상은 「같아 보이는 두 값」의 빨간불이다. 1 ULP 허용치를 두는 쪽은
                    //     해가 아니다: 그 허용치가 이 파일의 양성 대조(«계수를 1 ULP 밀면 빨개진다»)를 죽인다.
                    float expectedY = rig.HeadRadius * u[k].y;
                    Assert.AreEqual(Bits(rig.HeadRadius * u[k].x), Bits(s[k].x),
                        $"조각 {i} 점 {k}의 x가 R 배수 관계를 벗어납니다 — 단위릭 추출이 손실 없다는 전제가 깨졌습니다.");
                    Assert.AreEqual(Bits(rig.HeadCenterY + expectedY), Bits(s[k].y),
                        $"조각 {i} 점 {k}의 y가 (HC + R 배수) 관계를 벗어납니다.");
                    checkedPoints++;
                }
            }
            Assert.Greater(checkedPoints, 0, "점을 하나도 확인하지 않았습니다.");
        }
    }
}
