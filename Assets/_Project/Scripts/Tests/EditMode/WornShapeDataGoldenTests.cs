using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Interaction;
using UnityEngine;
using UnityEngine.TestTools;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ DLC 이행 B-2 파일럿의 <b>회귀 잠금</b> (2026-09-02).
    ///
    /// ============================================================================
    /// 무엇을 잠그는가
    /// ============================================================================
    /// NECK 6종의 몸 도형은 이 라운드에 <c>AccessoryShapeBuilder.AppendNeck</c>의 switch에서
    /// <c>Resources/Items/equip_neck_*.asset</c>의 <c>wornShapes</c>로 내려갔다.
    /// 그 이사에서 <b>좌표가 비트 하나라도 흔들리지 않았다</b>는 것이 성공 조건이었고,
    /// 이 검사가 그 조건을 영구히 붙잡는다.
    ///
    /// ============================================================================
    /// 왜 <b>16진 비트</b>로 비교하는가
    /// ============================================================================
    /// "거의 같다"는 이 이사에서 아무 뜻이 없다. 계수를 미리 곱해 눕히면(예:
    /// <c>0.98 × -0.878</c>을 <c>-0.86044</c> 한 칸으로) 대부분의 배율에서는 같은 값이 나오고
    /// <b>일부 배율에서만</b> 마지막 비트가 갈린다 — 실제로 이 라운드에 그것을 실험으로 확인했다
    /// (릭 10벌 중 8벌에서 갈렸다). 허용오차를 두면 그 함정을 그대로 통과시킨다.
    ///
    /// ============================================================================
    /// 릭을 <b>10벌</b> 쓰는 이유
    /// ============================================================================
    /// 배율 하나에서만 맞는 것은 우연일 수 있다. 이 파일의 액세서리 좌표에는 월드유닛 절대
    /// 상수가 하나도 없어야 하므로(배율이 바뀌어도 액세서리만 뒤에 남지 않는다), 배율 5종 ×
    /// 방향 2종을 함께 굽는다. 방향은 <c>Rig.F</c>가 x에만 부호를 곱한다는 규약까지 함께 잠근다.
    /// </summary>
    public sealed class WornShapeDataGoldenTests
    {
        private const string GoldenPath =
            "Assets/_Project/Scripts/Tests/EditMode/Golden/NeckWornShapeGolden.txt";

        /// <summary>골든을 구울 때 쓴 배율. 값 자체에 뜻은 없고 <b>서로 다르다</b>는 것에 뜻이 있다
        /// (정수배·상용 배율·어중간한 배율을 섞는다).</summary>
        private static readonly float[] Scales = { 1f, 0.75f, 0.6f, 1.37f, 0.4123f };

        /// <summary>파일럿 자리. 여기 한 줄을 늘리는 것이 곧 "다음 자리를 옮겼다"는 선언이다.</summary>
        private const EquipmentSlot PilotSlot = EquipmentSlot.Neck;

        /// <summary>파일럿 자리의 종 수. <b>표에서 센다</b> — 여기 6을 적으면 종이 늘어난 날
        /// 골든이 조용히 앞부분만 재게 된다.</summary>
        private static int ItemsPerSlot => ItemCatalog.ItemCountIn(PilotSlot);

        // ============================================================================
        // 1. 골든 대조 — 데이터로 내려간 뒤에도 좌표가 그대로인가
        // ============================================================================

        [Test]
        public void 목_형상은_데이터화_전후로_비트까지_같다()
        {
            int v1Items = 0;
            for (int item = 0; item < ItemsPerSlot; item++) if (!IsHandoffItem(item)) v1Items++;
            Assert.Greater(v1Items, 0, "v1 파일럿 아이템이 하나도 안 남았습니다 — 이 골든 대조는 공허합니다(전부 CardShapeGolden 으로 옮겨졌다면 이 파일을 정리하세요).");

            string expected = ReadGolden();
            string actual = Dump();

            if (expected == actual) Assert.Pass();

            Assert.Fail("NECK 몸 도형이 골든과 다릅니다 — 형상이 <b>움직였습니다</b>.\n" +
                        FirstDifference(expected, actual) +
                        $"\n골든: {GoldenPath}\n" +
                        "값을 바꾸는 것이 의도였다면 에셋(Resources/Items/equip_neck_*.asset)을 고친 뒤 " +
                        "이 골든을 다시 굽고, <b>왜 움직였는지</b>를 함께 남기세요.");
        }

        /// <summary>★ 양성 대조 — 좌표 한 자리를 흔들면 위 검사가 <b>실제로</b> 빨개지는가.
        /// <para>에셋을 건드리지 않고 메모리에서만 흔든다. 데이터가 어디서 오든 상관없이
        /// "비교가 살아 있다"만 확인하는 자리다.</para></summary>
        [Test]
        public void 좌표_한_자리를_흔들면_골든이_빨개진다()
        {
            AccessoryWornShapeData[] real = ItemCatalog.WornShapes(PilotSlot, 0);
            Assert.IsNotNull(real, "파일럿 자리 0번에 형상 데이터가 없습니다 — 아래 대조가 공허합니다.");
            Assert.Greater(real.Length, 0);

            AccessoryWornShapeData shifted = real[0];
            shifted.terms = (float[])real[0].terms.Clone();

            int moved = -1;
            for (int i = 0; i < shifted.terms.Length; i++)
            {
                // 계수 자리(정수가 아닌 값)만 흔든다 — 개수 자리를 흔들면 문법이 깨져 다른 이유로 실패한다.
                if (shifted.terms[i] == Mathf.Round(shifted.terms[i])) continue;
                shifted.terms[i] += 0.001f;
                moved = i;
                break;
            }
            Assert.GreaterOrEqual(moved, 0, "흔들 계수를 못 찾았습니다 — 스트림이 정수뿐일 수 없습니다.");

            AccessoryWornFrame frame = AccessoryShapeBuilder.Frame(Rig(1f, +1f));
            Assert.IsTrue(AccessoryWornShapeReader.TryBuild(real[0], frame, false, out Vector3[] before, out _));
            Assert.IsTrue(AccessoryWornShapeReader.TryBuild(shifted, frame, false, out Vector3[] after, out _));

            bool differs = false;
            for (int i = 0; i < before.Length && !differs; i++)
            {
                differs = Bits(before[i].x) != Bits(after[i].x) || Bits(before[i].y) != Bits(after[i].y);
            }
            Assert.IsTrue(differs,
                $"계수 {moved}번을 0.001 옮겼는데 좌표가 하나도 안 움직였습니다 — " +
                "이 검사(와 위 골든 대조)는 아무것도 잡지 못합니다.");
        }

        // ============================================================================
        // 2. 이사가 <b>실제로 일어났는가</b> — 코드가 그 자리를 모르는가
        // ============================================================================

        /// <summary>파일럿 자리의 6종이 <b>전부</b> 에셋에서 온다. 하나라도 코드로 남아 있으면
        /// "옮겼다"는 보고가 절반만 참이다.</summary>
        [Test]
        public void 파일럿_자리_여섯_종이_전부_에셋에서_온다()
        {
            for (int item = 0; item < ItemsPerSlot; item++)
            {
                AccessoryWornShapeData[] data = ItemCatalog.WornShapes(PilotSlot, item);
                Assert.IsNotNull(data,
                    $"{PilotSlot} {item}번의 형상 데이터가 없습니다 — 에셋의 wornShapes가 비었거나 " +
                    "스트림 검사에서 버려졌습니다(콘솔의 [ItemCatalog] 에러를 보세요).");
                Assert.Greater(data.Length, 0, $"{PilotSlot} {item}번의 형상이 0개입니다.");
            }
        }

        /// <summary>데이터가 <b>없으면</b> 그 자리는 '빠진 도형' 표식만 남는다 — 즉 지금 그려지는
        /// 것이 데이터에서 온다.
        /// <para>★ 이 검사는 <b>표식이 실제로 그려지는 구성</b>(개발 게이트 ON)에서만 뜻이 있다.
        /// 게이트가 닫혀 있으면 <c>sink</c>가 비고, 빈 목록을 도는 <c>foreach</c>는 아무것도 재지 않은 채
        /// 초록이 된다 — 이 저장소가 이미 겪은 거짓 통과 유형 그대로다.</para></summary>
        [Test]
        public void 데이터가_없으면_그_자리는_표식만_남는다()
        {
            ShapeCoverageGuard.ResetForTests();
            StickMateDevTools.SetTestOverride(true);
            try
            {
                var withData = new List<AccessoryShapeBuilder.Shape>();
                AccessoryShapeBuilder.Append(withData, PilotSlot, 0, Rig(1f, +1f));
                Assert.Greater(withData.Count, 0, "정상 경로에서 도형이 하나도 안 나왔습니다.");
                foreach (AccessoryShapeBuilder.Shape s in withData)
                {
                    StringAssert.DoesNotStartWith("Missing", s.Name,
                        "정상 아이템이 표식을 그렸습니다 — 아래 대조가 뜻을 잃습니다.");
                }

                LogAssert.Expect(LogType.Error, new Regex(@"\[도형\]"));

                var empty = new List<AccessoryShapeBuilder.Shape>();
                AccessoryShapeBuilder.AppendWorn(empty, PilotSlot, UnknownItemIndex(), Rig(1f, +1f),
                    AccessoryShapeBuilder.SortNeck, false);

                Assert.Greater(empty.Count, 0,
                    "데이터가 없는 자리에서 표식조차 안 나왔습니다 — 개발 게이트를 열었는데도 비었다면 " +
                    "이 검사는 빈 목록을 도는 껍데기입니다.");
                foreach (AccessoryShapeBuilder.Shape s in empty)
                {
                    StringAssert.StartsWith("Missing", s.Name,
                        "데이터가 없는 자리에서 '빠진 도형' 표식이 아닌 것이 나왔습니다 — " +
                        "코드가 아직 그 자리의 좌표를 알고 있다는 뜻입니다.");
                }
            }
            finally
            {
                StickMateDevTools.SetTestOverride(null);
                ShapeCoverageGuard.ResetForTests();
            }
        }

        // ============================================================================
        // 2-b. ★ 2026-09-05 계약 v2(R16/R17) — 「월요일_회전은_옛_산술과_비트까지_같다」·「방울_10각형은_옛_산술과_비트까지_같다」를 지웠다.
        //   두 검사는 2026-09-02 이전 AppendNeck 의 산술(TieBlade 회전 · 방울 10각형)을 박제한 것인데, 그 도형은 인계본 착용 기하
        //   (EQUIPMENT_HANDOFF_PORT_SPEC §14-0 #1)로 대체돼 더는 존재하지 않는다. 잃는 것: 삼각함수 좌표의 엔진 내 비트 대조 —
        //   인계본 조각은 스트림에 삼각 항이 없어(계수만) 그 축 자체가 사라졌다. 좌표 잠금은 CardShapeContractTests 가 골든으로 한다.
        // ============================================================================

        // ============================================================================
        // 3. 스트림 문법 — 망가진 팩이 <b>조용히</b> 통과하지 않는가
        // ============================================================================

        [Test]
        public void 빈_스트림은_거부된다()
        {
            var shape = new AccessoryWornShapeData { name = "빈것", terms = new float[0] };
            Assert.IsFalse(AccessoryWornShapeReader.Validate(shape, out string error));
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void 남는_칸이_있으면_거부된다()
        {
            AccessoryWornShapeData[] real = ItemCatalog.WornShapes(PilotSlot, 0);
            var padded = real[0];
            padded.terms = new float[real[0].terms.Length + 1];
            Array.Copy(real[0].terms, padded.terms, real[0].terms.Length);

            Assert.IsFalse(AccessoryWornShapeReader.Validate(padded, out string error),
                "스트림 끝에 값이 하나 남았는데 통과했습니다 — 점 하나가 통째로 빠져도 못 잡습니다.");
            StringAssert.Contains("칸", error);
        }

        [Test]
        public void 모르는_기저_번호는_거부된다()
        {
            var shape = new AccessoryWornShapeData
            {
                name = "이상한기저",
                // 점 1개 / x: 항 1개(기저 99, 게이트 0, 삼각 0, 계수 0개) / y: 항 0개
                terms = new float[] { 1f, 1f, 99f, 0f, 0f, 0f, 0f },
            };
            Assert.IsFalse(AccessoryWornShapeReader.Validate(shape, out string error));
            StringAssert.Contains("99", error);
        }

        /// <summary>기저 번호 8개가 <b>전부</b> 리그의 실제 치수로 이어지는가.
        /// <para>쓰이지 않는 번호를 열어 두면 그 자리는 아무도 안 본 채로 남는다 — 지금 NECK이
        /// 쓰는 것은 셋(머리 반경/몸통/목선)뿐이지만 나머지도 여기서 값으로 확인한다.</para></summary>
        [Test]
        public void 기저_번호는_전부_리그의_치수로_이어진다()
        {
            AccessoryShapeBuilder.Rig rig = Rig(0.83f, +1f);
            AccessoryWornFrame frame = AccessoryShapeBuilder.Frame(rig);

            var expected = new (AccessoryWornBasis basis, float value, string label)[]
            {
                (AccessoryWornBasis.HeadRadius, rig.HeadRadius, "머리 반경"),
                (AccessoryWornBasis.TorsoLength, rig.TorsoLength, "몸통 길이"),
                (AccessoryWornBasis.NeckLine, AccessoryShapeBuilder.NeckLocalY(rig), "목선"),
                (AccessoryWornBasis.ShoulderLine, rig.ShoulderY, "어깨선"),
                (AccessoryWornBasis.HeadCenterLine, rig.HeadCenterY, "머리 중심선"),
                (AccessoryWornBasis.HipLine, rig.HipY, "고관절선"),
            };

            foreach ((AccessoryWornBasis basis, float value, string label) in expected)
            {
                // 점 1개 / x: 기저 그대로 1항 / y: 0항
                var probe = new AccessoryWornShapeData
                {
                    name = label,
                    terms = new float[] { 1f, 1f, (float)basis, 0f, 0f, 0f, 0f },
                };
                Assert.IsTrue(AccessoryWornShapeReader.TryBuild(probe, frame, false,
                    out Vector3[] points, out string error), $"{label}: {error}");
                Assert.AreEqual(Bits(value), Bits(points[0].x),
                    $"{label}({basis})이 리그 값과 다릅니다.");
            }
        }

        /// <summary>게이트가 <b>양쪽으로</b> 작동하는가. 한쪽만 맞아도 초록이 되는 검사는 반쪽이다.</summary>
        [Test]
        public void 상태_게이트는_켤_때와_끌_때가_다르다()
        {
            AccessoryWornFrame frame = AccessoryShapeBuilder.Frame(Rig(1f, +1f));
            var shape = new AccessoryWornShapeData
            {
                name = "게이트",
                // 점 1개 / x: 항 2개 — 항상 더하는 R×1 + 상태일 때만 더하는 R×10 / y: 0항
                terms = new float[]
                {
                    1f,
                    2f,
                    (float)AccessoryWornBasis.HeadRadius, (float)AccessoryWornGate.Always,
                        (float)AccessoryWornTrig.None, 1f, 1f,
                    (float)AccessoryWornBasis.HeadRadius, (float)AccessoryWornGate.WhenStateOn,
                        (float)AccessoryWornTrig.None, 1f, 10f,
                    0f,
                },
            };

            Assert.IsTrue(AccessoryWornShapeReader.TryBuild(shape, frame, false, out Vector3[] off, out _));
            Assert.IsTrue(AccessoryWornShapeReader.TryBuild(shape, frame, true, out Vector3[] on, out _));
            Assert.AreEqual(frame.HeadRadius, off[0].x, 1e-6f, "상태가 꺼졌는데 게이트 항이 더해졌습니다.");
            Assert.AreEqual(frame.HeadRadius * 11f, on[0].x, 1e-5f, "상태가 켜졌는데 게이트 항이 빠졌습니다.");
        }

        // ============================================================================
        // 덤프 — 골든과 <b>같은 코드</b>가 굽는다
        // ============================================================================

        private static string Dump()
        {
            var sb = new StringBuilder(64 * 1024);
            var sink = new List<AccessoryShapeBuilder.Shape>();

            foreach (float scale in Scales)
            {
                foreach (float facing in new[] { 1f, -1f })
                {
                    AccessoryShapeBuilder.Rig rig = Rig(scale, facing);
                    string tag = $"s{scale.ToString("R", CultureInfo.InvariantCulture)}f{facing}";

                    for (int item = 0; item < ItemsPerSlot; item++)
                    {
                        // ★ 계약 v2 — 인계본 조각(strokeInR > 0)이 있는 아이템은 CardShapeGolden 이 잠근다. 여기 골든은 v1 파일럿만.
                        if (IsHandoffItem(item)) continue;
                        foreach (bool stateOn in new[] { false, true })
                        {
                            // ★ 2026-09-05 R20 N-1 — 이 골든은 「스트림 데이터의 비트 동일」을 잠근다. 아이템 단위 몸 파라미터(펜던트·반다나 착용선 dy)는
                            //   이제 v1 조각에도 걸리므로 Append 로 굽으면 그 오프셋이 섞인다. 그래서 스트림을 <b>읽기 그 자체</b>로 굽는다 —
                            //   오프셋이 실제로 걸리는지는 v1_조각에도_아이템_세로_오프셋이_걸린다 가 따로 잠근다.
                            sink.Clear();
                            AppendFromStreams(sink, item, rig, stateOn);

                            foreach (AccessoryShapeBuilder.Shape s in sink)
                            {
                                sb.Append(tag).Append('\t').Append(PilotSlot).Append('\t')
                                  .Append(item).Append('\t').Append(stateOn ? 1 : 0).Append('\t')
                                  .Append(s.Name).Append('\t').Append(s.Loop ? 1 : 0).Append('\t')
                                  .Append(s.Filled ? 1 : 0).Append('\t').Append(s.Tone).Append('\t')
                                  .Append(s.SortingOrder).Append('\t').Append(s.SwayStart).Append('\t')
                                  .Append(s.SwayCount).Append('\t').Append(s.Points.Length);
                                foreach (Vector3 p in s.Points)
                                {
                                    sb.Append('\t').Append(Bits(p.x)).Append(',').Append(Bits(p.y));
                                }
                                sb.Append('\n');
                            }
                        }
                    }
                }
            }
            return sb.ToString();
        }

        /// <summary>에셋 스트림을 리그로 읽어 도형 목록에 넣는다 — <c>AppendWorn</c>과 같은 순서·같은 필드, <b>아이템 몸 변형만 없이</b>.</summary>
        private static void AppendFromStreams(List<AccessoryShapeBuilder.Shape> sink, int item, in AccessoryShapeBuilder.Rig rig, bool stateOn)
        {
            AccessoryWornShapeData[] data = ItemCatalog.WornShapes(PilotSlot, item);
            Assert.IsNotNull(data, $"{PilotSlot} {item}번의 에셋 형상이 없습니다.");
            AccessoryWornFrame frame = AccessoryShapeBuilder.Frame(rig);
            for (int i = 0; i < data.Length; i++)
            {
                Assert.IsTrue(AccessoryWornShapeReader.TryBuild(data[i], frame, stateOn, out Vector3[] points, out _), $"{item}번 {data[i].name} 스트림을 못 읽었습니다.");
                sink.Add(new AccessoryShapeBuilder.Shape(data[i].name, points, data[i].loop,
                    AccessoryShapeBuilder.LayerOrder((byte)data[i].layer, AccessoryShapeBuilder.SortNeck),
                    data[i].swayStart, data[i].swayCount, (byte)data[i].tone, data[i].filled));
            }
        }

        /// <summary>
        /// ★ R20 N-1(2026-09-05) — 아이템 단위 세로 오프셋(<c>wornOffsetYInR</c>)은 v1 조각에도 걸린다: 펜던트·반다나의 착용선 dy.
        /// <para>Append(몸) 결과 = 스트림 읽기 결과 + dy·R (x 는 그대로, 상태 켜짐/꺼짐 둘 다). 오프셋이 0 인 v1 아이템은 두 결과가 비트까지 같다
        /// (음성 대조). 존재 대조: 오프셋이 0 이 아닌 v1 아이템이 실제로 있고(펜던트·반다나) 그 값은 아래(음수)다 — 값 자체는 에셋이 정본이고
        /// 모델과의 일치는 Tools/CardShapeGen/verify_card_shapes.py 가 잰다.</para>
        /// </summary>
        [Test]
        public void v1_조각에도_아이템_세로_오프셋이_걸린다()
        {
            int shifted = 0, unshifted = 0;
            var viaAppend = new List<AccessoryShapeBuilder.Shape>();
            var viaStream = new List<AccessoryShapeBuilder.Shape>();
            foreach (float scale in Scales)
            {
                AccessoryShapeBuilder.Rig rig = Rig(scale, 1f);
                for (int item = 0; item < ItemsPerSlot; item++)
                {
                    if (IsHandoffItem(item)) continue;
                    AccessoryWornTransform xf = ItemCatalog.WornTransform(PilotSlot, item);
                    Assert.AreEqual(0f, xf.Scale); Assert.AreEqual(0f, xf.ScaleY); Assert.IsFalse(xf.MirrorX);
                    float dy = xf.OffsetYInR * rig.HeadRadius;
                    foreach (bool stateOn in new[] { false, true })
                    {
                        viaAppend.Clear(); viaStream.Clear();
                        AccessoryShapeBuilder.Append(viaAppend, PilotSlot, item, rig, float.PositiveInfinity, 0f, stateOn);
                        AppendFromStreams(viaStream, item, rig, stateOn);
                        Assert.AreEqual(viaStream.Count, viaAppend.Count, $"{item}번: 조각 수가 다릅니다.");
                        for (int k = 0; k < viaStream.Count; k++)
                        {
                            Vector3[] a = viaAppend[k].Points, b = viaStream[k].Points;
                            Assert.AreEqual(b.Length, a.Length);
                            for (int i = 0; i < a.Length; i++)
                            {
                                if (xf.OffsetYInR == 0f)
                                {
                                    Assert.AreEqual(b[i], a[i], $"{item}번 {viaStream[k].Name} {i}번 점: 오프셋 0 인데 Append 가 점을 옮겼습니다.");
                                }
                                else
                                {
                                    Assert.AreEqual(b[i].x, a[i].x, 1e-6f, $"{item}번 {viaStream[k].Name} {i}번 점 x 가 움직였습니다(세로 오프셋뿐이어야 한다).");
                                    Assert.AreEqual(b[i].y + dy, a[i].y, 1e-5f, $"{item}번 {viaStream[k].Name} {i}번 점 y: 오프셋 {xf.OffsetYInR:F4} R 이 안 걸렸습니다.");
                                }
                            }
                        }
                    }
                    if (xf.OffsetYInR != 0f) { shifted++; Assert.Less(xf.OffsetYInR, 0f, $"{item}번: N-1 착용선 오프셋은 아래(음수)여야 합니다."); }
                    else unshifted++;
                }
            }
            Assert.Greater(shifted, 0, "세로 오프셋을 가진 v1 아이템이 없습니다 — R20 N-1(펜던트·반다나) 전사가 사라졌습니다.");
            Assert.Greater(unshifted + shifted, 0);
        }

        /// <summary>인계본 조각(계약 v2)으로 바뀐 자리인가 — 에셋 데이터에 명목 획이 적혀 있으면 그렇다.</summary>
        private static bool IsHandoffItem(int item)
        {
            AccessoryWornShapeData[] data = ItemCatalog.WornShapes(PilotSlot, item);
            if (data == null) return false;
            for (int i = 0; i < data.Length; i++)
            {
                if (AccessoryStroke.IsHandoff(data[i].strokeInR)) return true;
            }
            return false;
        }

        /// <summary>골든을 구운 릭. <b>프로덕션 기준 상수에 배율을 곱한다</b> — 여기에 숫자를 직접
        /// 적으면 기준이 움직였을 때 이 검사만 옛 세상에 남는다.</summary>
        private static AccessoryShapeBuilder.Rig Rig(float scale, float facing)
        {
            const float r1 = AccessoryShapeBuilder.BaselineHeadVisualRadius;
            const float hc1 = StickConfig.BaselineCharacterTotalHeight - r1;
            return new AccessoryShapeBuilder.Rig(r1 * scale, hc1 * scale,
                AccessoryShapeBuilder.BaselineShoulderLocalY * scale,
                AccessoryShapeBuilder.BaselineHipLocalY * scale, facing);
        }

        private static string Bits(float v)
            => BitConverter.ToUInt32(BitConverter.GetBytes(v), 0).ToString("X8", CultureInfo.InvariantCulture);

        /// <summary>그 자리에 <b>아이템이 없는</b> 번호. <b>숫자를 베끼지 않는다</b> — 표가 7종으로
        /// 늘면 이 값도 따라 올라가야 "존재하지 않는 번호"로 남는다.</summary>
        private static int UnknownItemIndex() => ItemCatalog.ItemCountIn(PilotSlot) + 1;

        private static string ReadGolden()
        {
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, GoldenPath);
            Assert.IsTrue(File.Exists(path), $"골든 스냅샷이 없습니다: {GoldenPath}");

            var sb = new StringBuilder(64 * 1024);
            foreach (string line in File.ReadAllText(path).Replace("\r\n", "\n").Split('\n'))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                sb.Append(line).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>다른 줄을 <b>전부 세고</b> 앞의 셋을 보여 준다.
        /// <para>★ 2026-09-02 — 처음에는 첫 줄 하나만 보여 줬다. 그 탓에 실제로는 14줄이 다른데
        /// 1줄만 보였고, 원인을 좁히는 데 Unity 배치모드 실행이 한 번 더 들었다. 실패 메시지는
        /// <b>범위</b>를 먼저 말해야 한다 — 한 점이 흔들린 것과 카테고리가 통째로 흔들린 것은
        /// 원인이 전혀 다르다.</para></summary>
        private static string FirstDifference(string expected, string actual)
        {
            string[] e = expected.Split('\n');
            string[] a = actual.Split('\n');
            var sb = new StringBuilder();
            int diff = 0;
            for (int i = 0; i < Mathf.Max(e.Length, a.Length); i++)
            {
                string le = i < e.Length ? e[i] : "(줄 없음)";
                string la = i < a.Length ? a[i] : "(줄 없음)";
                if (le == la) continue;
                diff++;
                if (diff <= 3) sb.Append($"\n  [{i + 1}]\n    골든: {le}\n    지금: {la}");
            }
            if (diff == 0) return "줄 내용은 같은데 전체 문자열이 다릅니다(끝 개행?).";
            return $"다른 줄 {diff}건 / 전체 {e.Length - 1}줄." + sb;
        }
    }
}
