using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Core;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ============================================================================
    /// ★ 조각 계약에 <b>신장 H 기저</b>를 연 라운드의 회귀 잠금 (2026-09-07, P6 선결)
    /// ============================================================================
    /// 왜 열었나: <c>design-motion</c>이 코스튬 프롭 좌표를 <b>전부 H 배수</b>로 적는데
    /// <see cref="AccessoryWornBasis"/>에는 <c>HeadRadius</c>/<c>TorsoLength</c>/… 뿐이고 H가 없었다.
    /// 매니페스트를 쓰는 사람이 손으로 R 배수로 환산해야 했고, <b>그 환산이 한 번 어긋나면
    /// 조형이 통째로 밀린다</b>.
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 것 — 넷이고, 셋은 <b>대조</b>가 붙어 있다
    /// ============================================================================
    /// <list type="number">
    ///  <item><b>기존 기저 번호가 한 칸도 안 밀렸다.</b> 이 번호는 <c>.asset</c> 스트림에 <b>숫자
    ///    그대로</b> 눕는 <b>배선 값</b>이라, 중간에 끼워 넣으면 출하된 모든 도형의 기저가 밀린다.
    ///    그 사고는 <b>화면이 그려지므로</b> 눈에 안 띈다(좌표가 「다른 값」일 뿐이다).</item>
    ///  <item>★ <b>출하 도형이 한 점도 안 움직인다.</b> 신장만 다른 두 프레임으로 <b>실제 판독기를
    ///    돌려</b> 비트 단위로 대조한다 — 스트림 문법을 두 번째로 적지 않으려는 것이 이 방식의 이유다
    ///    (문법을 두 벌로 적으면 검사기와 실행기가 갈라진다).</item>
    ///  <item><b>검사기가 새 기저를 거부하지 않는다.</b> 모르는 번호는 <b>여전히</b> 거부한다(대조).</item>
    ///  <item><b>신장을 안 실어 주면 조용히 0이 되지 않고 사유와 함께 실패한다.</b>
    ///    0으로 떨어뜨리면 도형이 원점으로 무너지는데 그건 「안 그려짐」과 화면상 같다.</item>
    /// </list>
    /// </summary>
    public sealed class AccessoryWornBasisHeightTests
    {
        // ====================================================================
        // 합성 스트림 — 문법: pointCount, (sumX, sumY) * n / sum := termCount, term* /
        //               term := basis, gate, trig, coefCount, coef*
        // ====================================================================

        /// <summary>점 1개짜리 도형. x는 <paramref name="xBasis"/> × <paramref name="xCoef"/>,
        /// y는 머리 반경 × 1. y를 <b>일부러 다른 기저</b>로 두는 이유: x만 바뀌는지 y까지 흔들리는지를
        /// 같은 도형 안에서 가른다.</summary>
        private static AccessoryWornShapeData OnePoint(int xBasis, float xCoef)
        {
            return new AccessoryWornShapeData
            {
                name = "probe",
                terms = new float[]
                {
                    1f,                                                             // 점 1개
                    1f, xBasis, 0f, 0f, 1f, xCoef,                                  // x = basis * coef
                    1f, (float)AccessoryWornBasis.HeadRadius, 0f, 0f, 1f, 1f,       // y = R * 1
                },
            };
        }

        /// <summary>치수는 서로 다른 값으로 준다 — 전부 1이면 기저를 바꿔도 값이 같아
        /// <b>어느 기저를 읽었는지</b>를 구별할 수 없다.</summary>
        private static AccessoryWornFrame FrameWithHeight(float height)
            => new AccessoryWornFrame(0.22f, 0.83f, 1.74f, 1.76f, 2.05f, 0.93f, +1f, height);

        private static AccessoryWornFrame FrameWithoutHeight()
            => new AccessoryWornFrame(0.22f, 0.83f, 1.74f, 1.76f, 2.05f, 0.93f, +1f);

        private static int Bits(float v) => System.BitConverter.SingleToInt32Bits(v);

        // ====================================================================
        // 1. 배선 값 — 기존 번호가 한 칸도 안 밀렸는가
        // ====================================================================

        /// <summary>
        /// ★ <b>숫자를 일부러 적는다.</b> 보통 이 저장소는 프로덕션 상수를 테스트에 베끼는 것을
        /// 금지하지만, 여기서는 <b>숫자 자체가 계약</b>이다 — 이 값들은 <c>.asset</c> 파일에 그대로
        /// 눕고, 출하된 뒤에는 <b>바꿀 수 없다</b>. 상수를 참조하면 «누가 값을 바꿔도 테스트가
        /// 따라 움직여» 아무것도 못 잰다(<c>StickmanStateId</c>·세이브 필드 이름과 같은 종류).
        /// </summary>
        [Test]
        public void 기존_기저_번호는_한_칸도_안_밀렸다()
        {
            Assert.AreEqual(0, (int)AccessoryWornBasis.HeadRadius, "기저 번호가 밀렸습니다.");
            Assert.AreEqual(1, (int)AccessoryWornBasis.TorsoLength, "기저 번호가 밀렸습니다.");
            Assert.AreEqual(2, (int)AccessoryWornBasis.NeckLine, "기저 번호가 밀렸습니다.");
            Assert.AreEqual(3, (int)AccessoryWornBasis.ShoulderLine, "기저 번호가 밀렸습니다.");
            Assert.AreEqual(4, (int)AccessoryWornBasis.HeadCenterLine, "기저 번호가 밀렸습니다.");
            Assert.AreEqual(5, (int)AccessoryWornBasis.HipLine, "기저 번호가 밀렸습니다.");
            Assert.AreEqual(6, (int)AccessoryWornBasis.SwungX, "기저 번호가 밀렸습니다.");
            Assert.AreEqual(7, (int)AccessoryWornBasis.SwungY, "기저 번호가 밀렸습니다.");
        }

        /// <summary>신장은 <b>맨 뒤</b>에 붙었다 — 즉 기존 번호 위에 앉지 않았다.
        /// 위 테스트가 「값」을 잠근다면 이것은 「배치 규칙」을 잠근다: 다음에 기저를 하나 더 여는
        /// 사람이 <b>중간에 끼워 넣으면</b> 여기서 걸린다.</summary>
        [Test]
        public void 신장_기저는_기존_기저_전부보다_뒤에_붙었다()
        {
            int height = (int)AccessoryWornBasis.Height;

            var previous = new List<AccessoryWornBasis>
            {
                AccessoryWornBasis.HeadRadius, AccessoryWornBasis.TorsoLength,
                AccessoryWornBasis.NeckLine, AccessoryWornBasis.ShoulderLine,
                AccessoryWornBasis.HeadCenterLine, AccessoryWornBasis.HipLine,
                AccessoryWornBasis.SwungX, AccessoryWornBasis.SwungY,
            };

            foreach (AccessoryWornBasis basis in previous)
            {
                Assert.Less((int)basis, height,
                    $"신장 기저({height})가 기존 기저 {basis}({(int)basis})보다 앞이거나 같습니다 — " +
                    "출하된 모든 도형의 기저가 한 칸씩 밀립니다. 그 사고는 화면이 그려지므로 " +
                    "눈에 띄지 않습니다.");
            }

            // ★ 음성 대조 — 「전부 뒤에 있다」가 목록이 비어서 통과한 것이 아님을 못박는다.
            Assert.AreEqual(8, previous.Count,
                "기존 기저 목록이 8개가 아닙니다 — 위 foreach가 실제로 8번 돌았는지가 이 단언에 달려 있습니다.");
        }

        // ====================================================================
        // 2. ★ 출하 도형 무영향 — 이 파일의 본론
        // ====================================================================

        /// <summary>
        /// ★★ <b>신장을 바꿔도 출하 도형은 한 점도 안 움직인다.</b>
        ///
        /// <para>같은 도형을 <b>신장만 다른</b> 두 프레임으로 판독해 좌표를 <b>비트 단위로</b> 대조한다.
        /// 스트림을 내가 다시 파싱하지 않고 <b>실제 판독기</b>를 두 번 돌리는 이유:
        /// 문법을 두 벌로 적으면 검사기와 실행기가 갈라지고, 그때 «검사는 통과했는데 화면은 깨진»
        /// 상태가 만들어진다.</para>
        ///
        /// <para>★ <b>양성 대조 둘</b>이 붙어 있다 — 하나라도 깨지면 이 「안 움직였다」는 통째로 무효다:
        /// (가) 실제로 <b>도형을 하나라도 돌렸는가</b>(0개를 돌고 초록이 되는 것이 이 저장소 사고 #5),
        /// (나) 이 대조가 <b>움직임을 실제로 감지하는가</b>(H 기저 도형은 반드시 갈려야 한다).</para>
        /// </summary>
        [Test]
        public void 출하_도형은_신장을_바꿔도_한_점도_안_움직인다()
        {
            AccessoryWornFrame withoutH = FrameWithHeight(AccessoryWornFrame.HeightUnavailable);
            AccessoryWornFrame withH = FrameWithHeight(3.14159f);

            int comparedShapes = 0;
            int comparedPoints = 0;

            for (int s = 0; s < ItemCatalog.SlotCount; s++)
            {
                var slot = (EquipmentSlot)s;
                int items = ItemCatalog.ItemCountIn(slot);
                for (int item = 0; item < items; item++)
                {
                    AccessoryWornShapeData[] shapes = ItemCatalog.WornShapes(slot, item);
                    if (shapes == null) continue;

                    for (int i = 0; i < shapes.Length; i++)
                    {
                        foreach (bool stateOn in new[] { false, true })
                        {
                            Assert.IsTrue(
                                AccessoryWornShapeReader.TryBuild(shapes[i], withoutH, stateOn,
                                    out Vector3[] a, out string errorA),
                                $"{slot} {item}번 '{shapes[i].name}'가 신장 없는 프레임에서 판독에 " +
                                $"실패했습니다({errorA}) — 새 기저를 여는 변경이 <b>기존 경로</b>를 " +
                                "깼다는 뜻입니다.");
                            Assert.IsTrue(
                                AccessoryWornShapeReader.TryBuild(shapes[i], withH, stateOn,
                                    out Vector3[] b, out string errorB),
                                $"{slot} {item}번 '{shapes[i].name}'가 신장 있는 프레임에서 판독에 " +
                                $"실패했습니다({errorB}).");

                            Assert.AreEqual(a.Length, b.Length,
                                $"{slot} {item}번 '{shapes[i].name}'의 점 개수가 신장에 따라 달라졌습니다.");

                            for (int k = 0; k < a.Length; k++)
                            {
                                Assert.AreEqual(Bits(a[k].x), Bits(b[k].x),
                                    $"{slot} {item}번 '{shapes[i].name}' {k}번 점의 x가 신장에 반응했습니다 — " +
                                    "출하 42종은 H 기저를 하나도 쓰지 않으므로 한 비트도 달라지면 안 됩니다.");
                                Assert.AreEqual(Bits(a[k].y), Bits(b[k].y),
                                    $"{slot} {item}번 '{shapes[i].name}' {k}번 점의 y가 신장에 반응했습니다.");
                                comparedPoints++;
                            }
                            comparedShapes++;
                        }
                    }
                }
            }

            // ---- (가) 양성 대조: 실제로 뭔가를 돌았는가 ----
            Assert.Greater(comparedShapes, 0,
                "출하 도형을 하나도 못 읽었습니다 — 위 '안 움직였다'는 <b>측정 0건</b>입니다. " +
                "Resources/Items 경로나 ItemCatalog.WornShapes가 바뀌었는지 보세요.");
            Assert.Greater(comparedPoints, 0, "점을 하나도 비교하지 못했습니다(측정 0건).");

            // ---- (나) 양성 대조: 이 비교가 움직임을 감지하는가 ----
            AccessoryWornShapeData heightProbe = OnePoint((int)AccessoryWornBasis.Height, 2f);
            Assert.IsTrue(AccessoryWornShapeReader.TryBuild(heightProbe, withH, false,
                out Vector3[] probeWithH, out _), "신장 프로브가 판독되지 않았습니다.");
            AccessoryWornFrame otherH = FrameWithHeight(9.87f);
            Assert.IsTrue(AccessoryWornShapeReader.TryBuild(heightProbe, otherH, false,
                out Vector3[] probeOtherH, out _), "신장 프로브가 판독되지 않았습니다.");

            Assert.AreNotEqual(Bits(probeWithH[0].x), Bits(probeOtherH[0].x),
                "신장을 바꿨는데 H 기저 도형의 x가 안 움직였습니다 — 위 대조는 아무것도 잡지 못합니다.");
            Assert.AreEqual(Bits(probeWithH[0].y), Bits(probeOtherH[0].y),
                "H와 무관한 y(머리 반경 기저)까지 움직였습니다 — 기저 해석이 서로 샙니다.");
        }

        // ====================================================================
        // 3. 검사기가 새 기저를 거부하지 않는가 (+ 모르는 번호는 여전히 거부하는가)
        // ====================================================================

        /// <summary>★ <see cref="AccessoryWornShapeReader.Validate"/>는 <c>Unit</c> 프레임을 쓴다.
        /// 거기에 신장을 안 넣어 두면 H 기저 도형이 <b>문법은 옳은데</b> 로드에서 거부되고,
        /// 매니페스트를 쓰는 사람은 그 이유를 영원히 못 찾는다(코스튬 프롭이 통째로 안 뜬다).</summary>
        [Test]
        public void 신장_기저_도형은_검사기를_통과하고_모르는_번호는_여전히_거부된다()
        {
            AccessoryWornShapeData heightShape = OnePoint((int)AccessoryWornBasis.Height, 0.25f);
            Assert.IsTrue(AccessoryWornShapeReader.Validate(heightShape, out string error),
                $"신장 기저 도형이 검사기에서 거부됐습니다({error}) — 코스튬 프롭이 통째로 안 실립니다.");

            // ★ 음성 대조 — 검사기가 「전부 통과」로 망가진 것이 아님을 같은 테스트에서 못박는다.
            const int unknownBasis = 99;
            Assert.IsFalse(AccessoryWornShapeReader.Validate(OnePoint(unknownBasis, 1f), out string unknownError),
                "모르는 기저 번호가 검사기를 통과했습니다 — 검사기가 통째로 열렸습니다.");
            StringAssert.Contains(unknownBasis.ToString(), unknownError,
                "모르는 기저를 거부하면서 그 번호를 사유에 남기지 않았습니다.");
        }

        // ====================================================================
        // 4. 신장 미공급 — 조용히 0이 되지 않는가
        // ====================================================================

        /// <summary>
        /// ★ 신장을 안 실어 준 프레임에서 H 기저 도형은 <b>실패</b>해야 한다.
        /// 0으로 떨어뜨리면 그 항이 통째로 사라져 도형이 <b>원점으로 무너지는데</b>,
        /// 그 화면은 「안 그려짐」과 구별되지 않는다.
        /// <para><b>존재 대조를 같은 테스트에 넣는다</b>: <u>같은 프레임</u>에서 기존 기저 도형은
        /// 여전히 성공해야 한다. 안 그러면 이 '실패했다'가 «프레임이 통째로 망가져서»와 구별되지 않는다.</para>
        /// </summary>
        [Test]
        public void 신장을_안_실어_주면_조용히_0이_되지_않고_사유와_함께_실패한다()
        {
            AccessoryWornFrame noHeight = FrameWithoutHeight();
            Assert.AreEqual(AccessoryWornFrame.HeightUnavailable, noHeight.Height,
                "7인자 생성자가 신장을 '없음'으로 두지 않았습니다(전제 붕괴).");

            Assert.IsFalse(
                AccessoryWornShapeReader.TryBuild(OnePoint((int)AccessoryWornBasis.Height, 2f),
                    noHeight, false, out Vector3[] collapsed, out string error),
                "신장이 없는데 H 기저 도형이 조용히 판독됐습니다 — 그 도형은 원점으로 무너지고, " +
                "그 화면은 '안 그려짐'과 구별되지 않습니다.");
            Assert.IsNull(collapsed, "실패했는데 점 배열이 나왔습니다.");
            StringAssert.Contains("신장", error,
                "실패 사유가 신장을 지목하지 않습니다 — 고치는 사람이 어디를 봐야 하는지 모릅니다.");

            // ---- 존재 대조 — 같은 프레임에서 기존 기저는 여전히 된다 ----
            Assert.IsTrue(
                AccessoryWornShapeReader.TryBuild(OnePoint((int)AccessoryWornBasis.TorsoLength, 2f),
                    noHeight, false, out Vector3[] fine, out string fineError),
                $"신장 없는 프레임에서 기존 기저 도형까지 실패했습니다({fineError}) — " +
                "위 '실패했다'가 '프레임이 통째로 망가져서'와 구별되지 않습니다.");
            Assert.AreEqual(1, fine.Length, "기존 기저 도형의 점 개수가 1이 아닙니다.");
        }
    }
}
