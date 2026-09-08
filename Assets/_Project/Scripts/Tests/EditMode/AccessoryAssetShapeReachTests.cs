using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Interaction;
using UnityEngine;
using UnityEngine.TestTools;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ============================================================================
    /// ★ 에셋 조형 경로가 <b>어디까지 닿는가</b> — P6 통로 (2026-09-07)
    /// ============================================================================
    /// <c>CLAUDE.md</c> 절대 불변 원칙 4(<i>"신규 모션/이펙트(DLC)는 기본 로직 무수정으로
    /// ScriptableObject 매니페스트를 통해 추가"</i>)의 <b>실증 사례가 지금까지 0건</b>이었다.
    /// 이유는 <c>game-architect</c>(B-3)와 <c>design-equipment</c>(J-7)가 서로 모른 채 같은 곳을 짚었다 —
    /// <b>몸 도형이 에셋으로 내려온 자리가 NECK 6종뿐</b>이라 신규 팩의 HEAD/EYES/BACK을
    /// <c>.cs</c> 0줄로 만들 수 없었다.
    ///
    /// 이 파일이 재는 것은 <b>통로</b>다(조형이 아니다 — 12종을 굽는 것은 별도 라운드).
    ///
    /// ============================================================================
    /// 이 파일이 답하는 네 가지
    /// ============================================================================
    /// <list type="number">
    ///  <item><b>어느 자리가 에셋 조형을 받는가</b>, 그리고 그 자리의 층이 코드 표가 쓰는 층과 같은가.</item>
    ///  <item><b>출하 42종에는 아무 일도 안 일어나는가</b> — 넓힌 자리 6종의 에셋에 <c>wornShapes</c>가
    ///    0개라는 사실을 <b>데이터에서 직접 센다</b>. NECK 6종이 데이터를 갖는다는 반대 사실을 같이 세어
    ///    "0을 세고 초록"이 아님을 매 실행 보인다.</item>
    ///  <item><b>합성 팩 아이템이 실제로 그려지는가</b> — 프로덕션 <c>.cs</c> 한 줄도 안 고친 채
    ///    에셋 하나만으로 HEAD 6번 자리에 도형이 나오는가.</item>
    ///  <item><b>데이터가 없으면 여전히 크게 실패하는가</b> — 반쪽 팩이 조용히 지나가면 안 된다.</item>
    /// </list>
    ///
    /// ★ <b>선후관계는 아직 코드가 이긴다</b>(리더 판정 대기). 인계본 16종은 코드 표가 그린다.
    /// 두 경로가 같은 답을 낸다는 증명은 <c>AccessoryAssetShapeParityTests</c>에 있다.
    /// </summary>
    public sealed class AccessoryAssetShapeReachTests
    {
        private static AccessoryShapeBuilder.Rig Rig(float facing = +1f)
        {
            float r = AccessoryShapeBuilder.BaselineHeadVisualRadius;
            return new AccessoryShapeBuilder.Rig(r, StickConfig.BaselineCharacterTotalHeight - r,
                AccessoryShapeBuilder.BaselineShoulderLocalY,
                AccessoryShapeBuilder.BaselineHipLocalY, facing);
        }

        // ====================================================================
        // 1. 어느 자리가 받는가 — 그리고 층이 코드 표와 같은가
        // ====================================================================

        /// <summary>에셋 조형을 받는 네 자리와, <b>구조적 사유로</b> 빠진 세 자리.
        /// <para>빠진 자리를 「아직 안 했다」로 두면 다음 사람이 그냥 열어 버린다. 사유를 여기 적어
        /// <b>검사로</b> 굳힌다 — 열려면 이 파일이 먼저 빨개진다.</para></summary>
        [Test]
        public void 에셋_조형을_받는_자리와_빠진_자리가_사유대로다()
        {
            var accepted = new[]
            {
                EquipmentSlot.Head, EquipmentSlot.Eyes, EquipmentSlot.Neck, EquipmentSlot.Shoulders,
            };
            var excluded = new (EquipmentSlot slot, string reason)[]
            {
                (EquipmentSlot.Hair, "머리 도형은 모자 커버선으로 잘린다(AppendClippedBelowCover) — 에셋 조각은 " +
                                     "그 자르기를 안 거치므로 열면 '모자를 썼는데 머리카락이 뚫고 나오는' 결함을 " +
                                     "데이터로 만들 수 있다. 게다가 이 자리는 2026-09-06 사용자 지시로 은퇴했다."),
                (EquipmentSlot.Fx, "몸 도형의 주인이 AppearanceShapeBuilder 다 — 카드만 열면 '카드는 있는데 " +
                                   "착용하면 아무것도 안 나오는' 반쪽 아이템을 팩이 만들 수 있다."),
                (EquipmentSlot.Pet, "위와 같다(AppearanceShapeBuilder 소관)."),
            };

            Assert.AreEqual(EquipmentModel.SlotCount, accepted.Length + excluded.Length,
                "자리 수가 안 맞습니다 — 카테고리가 늘었는데 이 검사가 그 자리를 안 봤습니다. " +
                "새 자리가 에셋 조형을 받는지/왜 안 받는지를 여기 적으세요.");

            foreach (EquipmentSlot slot in accepted)
            {
                Assert.IsTrue(AccessoryShapeBuilder.TryWornAssetSlotOrder(slot, out _),
                    $"{slot}이 에셋 조형을 못 받습니다 — 이 자리의 팩 아이템은 .cs 를 고쳐야 만들 수 있습니다.");
            }

            foreach ((EquipmentSlot slot, string reason) in excluded)
            {
                Assert.IsFalse(AccessoryShapeBuilder.TryWornAssetSlotOrder(slot, out _),
                    $"{slot}이 에셋 조형을 받고 있습니다. 여는 것이 의도였다면 아래 사유를 먼저 해소하세요: {reason}");
            }
        }

        /// <summary>넓힌 자리의 <b>층 번호</b>가 코드 표가 실제로 쓰는 값과 같은가.
        /// <para>여기 숫자를 적지 않는다 — 코드 표를 <b>돌려서</b> 그 조각의 정렬 번호를 읽는다.
        /// 상수를 베끼면 한쪽이 바뀌는 날 <b>둘이 갈라져도 아무도 모른다</b>(CLAUDE.md 협업 프로토콜).</para></summary>
        [Test]
        public void 에셋_자리의_층은_코드_표가_쓰는_층과_같다()
        {
            var slots = new[] { EquipmentSlot.Head, EquipmentSlot.Eyes, EquipmentSlot.Shoulders };
            int checkedSlots = 0;

            foreach (EquipmentSlot slot in slots)
            {
                Assert.IsTrue(AccessoryShapeBuilder.TryWornAssetSlotOrder(slot, out int assetOrder));

                int count = ItemCatalog.ItemCountIn(slot);
                for (int item = 0; item < count; item++)
                {
                    if (!AccessoryShapeBuilder.IsHandoffCode(slot, item)) continue;

                    var code = new List<AccessoryShapeBuilder.Shape>();
                    Assert.IsTrue(AccessoryShapeBuilder.AppendCodeShapes(code, slot, item, Rig(),
                        AccessorySurface.Card));
                    Assert.Greater(code.Count, 0);

                    // 슬롯 기본 층(AccessoryPieceLayer.Slot)인 조각만 비교한다 —
                    // 층을 명시한 조각(망토 뒤판 등)은 LayerOrder 가 다른 번호로 옮긴다.
                    bool sawSlotLayer = false;
                    foreach (AccessoryShapeBuilder.Shape s in code)
                    {
                        if (s.Layer != (byte)AccessoryPieceLayer.Slot) continue;
                        Assert.AreEqual(s.SortingOrder, assetOrder,
                            $"{slot} {item}번 '{s.Name}': 코드 표는 {s.SortingOrder}, 에셋 통로는 {assetOrder}. " +
                            "같은 자리의 겹침 규칙이 두 값으로 갈라졌습니다 — 팩 아이템이 남의 아이템 " +
                            "위/아래로 잘못 들어갑니다.");
                        sawSlotLayer = true;
                    }
                    Assert.IsTrue(sawSlotLayer,
                        $"{slot} {item}번에 슬롯 기본 층 조각이 하나도 없습니다 — 이 비교가 공허합니다.");
                    checkedSlots++;
                    break;   // 자리마다 한 종이면 충분하다(층은 아이템이 아니라 자리의 성질이다).
                }
            }

            Assert.AreEqual(slots.Length, checkedSlots, "층을 확인하지 못한 자리가 있습니다.");
        }

        // ====================================================================
        // 2. 출하 42종에는 아무 일도 일어나지 않는다 — 데이터에서 직접 센다
        // ====================================================================

        /// <summary>넓힌 자리(HEAD/EYES/BACK)의 출하 에셋에는 <c>wornShapes</c>가 <b>0개</b>다.
        /// 그래서 이 라운드의 확장은 그 18종에 대해 <b>구조적으로</b> 아무 일도 하지 않는다.
        ///
        /// <para>★ 이 검사가 「0을 세고 초록」이 아님을 같은 실행 안에서 못박는다 — NECK 6종은
        /// <b>반드시</b> 데이터를 갖는다. 한쪽만 있으면 카탈로그가 통째로 안 실렸을 때도 초록이 된다.</para>
        ///
        /// <para>★ 여기가 빨개지는 날은 「출하 아이템 하나가 에셋 조형으로 넘어갔다」는 뜻이다.
        /// 그것은 <b>리더 판정 사항</b>이고(코드 switch 폐기), 넘길 때는 이 파일과 오프라인 비트 대조
        /// (<c>Tools/ShapeDump</c> 계열)를 함께 다시 돌려야 한다.</para></summary>
        [Test]
        public void 출하_아이템은_아직_아무도_에셋_조형으로_넘어가지_않았다()
        {
            // ★★ 2026-09-08 — 「출하」의 뜻을 <b>기본 코호트</b>로 다시 썼다(리더 판정).
            //   종전에는 자리 번호로 카탈로그 전량을 돌면서 그것을 「출하 아이템」이라고 불렀다.
            //   첫 유료 팩이 HEAD/EYES/BACK 6번에 실리자 이 검사가 «출하 도형이 바뀌었을 수 있다»라고
            //   신고했지만, 팩 아이템이 에셋 조형으로 흐르는 것은 <b>이 기능의 목적 그 자체</b>다
            //   (§18-3 판정 3 · 불변 원칙 4). 즉 결함이 아니라 이름이 낡은 것이었다.
            //   ★ 뺀 자리는 아래 «팩은 반대로 반드시 에셋 조형으로 흐른다»가 다시 잰다.
            var stillCode = new[] { EquipmentSlot.Head, EquipmentSlot.Eyes, EquipmentSlot.Shoulders };
            int codeSideItems = 0;

            foreach (EquipmentSlot slot in stillCode)
            {
                int count = BaseCohortScope.CountIn(slot);
                Assert.Greater(count, 0, $"{slot}의 기본 코호트가 비었습니다 — 아래 순회가 공허합니다.");
                for (int item = 0; item < count; item++)
                {
                    AccessoryWornShapeData[] data = ItemCatalog.WornShapes(slot, item);

                    // ★★ 2026-09-08 — <b>메시지가 null 을 역참조해 테스트가 통과 조건에서 죽었다.</b>
                    //   C#은 인자를 먼저 계산하므로 <c>Assert.IsTrue(cond, $"…{data.Length}…")</c>는
                    //   <b>cond 가 참이어도</b> 보간을 실행한다. 출하 HEAD/EYES/BACK은 데이터가 없어
                    //   <c>data == null</c>이 <b>정상</b>인데, 바로 그 정상 경로가 NullReferenceException 을 냈다.
                    //   ⇒ 이 검사는 첫 아이템에서 죽어 <b>단 한 종도 실제로 재지 못했다</b>
                    //   (러너 기록: 2026-09-07 part2-final_edit.xml, 이 파일 이 줄).
                    //   길이는 <b>조건 밖에서</b> 미리 구한다 — 메시지가 상태를 만들지 않게.
                    int shapeCount = data == null ? 0 : data.Length;
                    Assert.AreEqual(0, shapeCount,
                        $"{slot} {item}번의 에셋에 몸 도형이 {shapeCount}개 들어 있습니다. " +
                        "출하 아이템을 에셋 조형으로 넘기는 것은 리더 판정 사항입니다 — " +
                        "넘겼다면 오프라인 비트 대조를 함께 돌리고 이 검사를 갱신하세요.");
                    codeSideItems++;
                }
            }

            // ★ 양성 대조 — 「데이터가 있는 자리」가 실재해야 위 0건이 뜻을 갖는다.
            int assetSideItems = 0, assetSideShapes = 0;
            int neckCount = BaseCohortScope.CountIn(EquipmentSlot.Neck);
            for (int item = 0; item < neckCount; item++)
            {
                AccessoryWornShapeData[] data = ItemCatalog.WornShapes(EquipmentSlot.Neck, item);
                Assert.IsNotNull(data, $"NECK {item}번의 형상 데이터가 없습니다 — 카탈로그가 반쯤 실렸습니다.");
                Assert.Greater(data.Length, 0);
                assetSideItems++;
                assetSideShapes += data.Length;
            }

            // ★★ 좁히기의 짝 — <b>팩 코호트는 반대로 반드시 에셋 조형을 갖는다</b>.
            //   빼기만 하면 «팩이 조형을 잃어도 조용히 초록»이 된다(부재 단언이 썩는 그 형태).
            int packItems = 0, packShapes = 0;
            for (int s = 0; s < ItemCatalog.SlotCount; s++)
            {
                var slot = (EquipmentSlot)s;
                if (!AccessoryShapeBuilder.TryWornAssetSlotOrder(slot, out _)) continue;
                int count = ItemCatalog.ItemCountIn(slot);
                for (int item = BaseCohortScope.CountIn(slot); item < count; item++)
                {
                    packItems++;
                    AccessoryWornShapeData[] data = ItemCatalog.WornShapes(slot, item);
                    int n = data == null ? 0 : data.Length;
                    Assert.Greater(n, 0,
                        $"{slot} {item}번(팩)의 에셋에 몸 도형이 0개입니다 — 프로덕션 .cs 를 고치지 않고는 " +
                        "이 아이템이 몸에 아무것도 안 그립니다(반쪽 상품). 원칙 4가 약속한 통로가 그 자리에서 끊겼습니다.");
                    packShapes += n;
                }
            }

            Assert.Greater(codeSideItems, 0, "코드 쪽 아이템을 하나도 안 셌습니다.");
            Assert.AreEqual(neckCount, assetSideItems);
            Assert.Greater(assetSideShapes, 0,
                "에셋 쪽 도형이 0개입니다 — 위 '0개' 판정이 「아무것도 안 실렸다」와 구분되지 않습니다.");
            Debug.Log($"[에셋조형] 출하(기본 코호트) {codeSideItems}종은 몸 도형 0개 · " +
                      $"NECK {assetSideItems}종은 {assetSideShapes}개 · " +
                      $"팩 {packItems}종은 {packShapes}개. {BaseCohortScope.Describe()}");
        }

        /// <summary>넓힌 자리에서 <see cref="AccessoryShapeBuilder.AppendWornAsset"/>가 지금은
        /// <b>전부 false</b>이고(= 옛 코드 경로가 그대로 돈다), NECK 에서는 <b>true</b>다.
        /// 위 검사가 데이터를 봤다면 이쪽은 <b>분기 자체</b>를 본다.</summary>
        [Test]
        public void 넓힌_자리는_아직_옛_경로로_흐르고_목만_에셋으로_흐른다()
        {
            var sink = new List<AccessoryShapeBuilder.Shape>();

            // ★ 2026-09-08 — 「출하」 = 기본 코호트(위 검사와 같은 정정). 팩 자리는 <b>true 여야</b> 하고,
            //   그것을 같은 순회에서 함께 잰다 — 그래야 이 검사가 «분기가 살아 있는가»를 계속 본다.
            int packDrew = 0, packItems = 0;
            foreach (EquipmentSlot slot in new[] { EquipmentSlot.Head, EquipmentSlot.Eyes, EquipmentSlot.Shoulders })
            {
                int baseCount = BaseCohortScope.CountIn(slot);
                for (int item = 0; item < baseCount; item++)
                {
                    sink.Clear();
                    Assert.IsFalse(
                        AccessoryShapeBuilder.AppendWornAsset(sink, slot, item, Rig(), false, AccessorySurface.Body),
                        $"{slot} {item}번(출하)이 에셋 경로로 흘렀습니다 — 출하 도형이 바뀌었을 수 있습니다.");
                    Assert.AreEqual(0, sink.Count, $"{slot} {item}번: false 인데 조각을 넣었습니다.");
                }

                for (int item = baseCount; item < ItemCatalog.ItemCountIn(slot); item++)
                {
                    packItems++;
                    sink.Clear();
                    Assert.IsTrue(
                        AccessoryShapeBuilder.AppendWornAsset(sink, slot, item, Rig(), false, AccessorySurface.Body),
                        $"{slot} {item}번(팩)이 에셋 경로로 <b>안</b> 흘렀습니다 — .cs 0줄로 팩을 만든다는 " +
                        "원칙 4의 약속이 이 자리에서 끊겼습니다.");
                    Assert.Greater(sink.Count, 0, $"{slot} {item}번: true 인데 조각이 0개입니다.");
                    packDrew += sink.Count;
                }
            }

            int neck = ItemCatalog.ItemCountIn(EquipmentSlot.Neck);
            int drew = 0;
            for (int item = 0; item < neck; item++)
            {
                sink.Clear();
                Assert.IsTrue(
                    AccessoryShapeBuilder.AppendWornAsset(sink, EquipmentSlot.Neck, item, Rig(), false,
                        AccessorySurface.Body),
                    $"NECK {item}번이 에셋 경로로 안 흘렀습니다 — B-2 파일럿이 깨졌습니다.");
                Assert.Greater(sink.Count, 0, $"NECK {item}번: true 인데 조각이 0개입니다.");
                drew += sink.Count;
            }
            Assert.Greater(drew, 0, "목 조각을 하나도 안 그렸습니다 — 이 대조가 공허합니다.");
            Debug.Log($"[에셋조형] 출하 자리는 전부 옛 경로 · NECK {neck}종 {drew}조각 · " +
                      $"팩 {packItems}종 {packDrew}조각이 에셋 경로로 흐른다.");
        }

        // ====================================================================
        // 2-B. ★ 아직 못 메운 갭 — 러너에 「건너뜀」으로 계속 보이게 등재한다
        // ====================================================================

        /// <summary>
        /// ★★ <b>등재된 갭</b>(2026-09-08, 첫 유료 팩 착지 라운드) — <b>조형 게이트 셋이 팩을 아직 안 본다.</b>
        ///
        /// <para>팩 4종이 실리면서 「HEAD 카탈로그 전부를 덮는가」 류의 커버리지 게이트가 빨개졌고,
        /// 이 라운드는 그 게이트들의 <b>모집단을 기본 코호트로 좁혀</b> 초록으로 돌렸다. 좁히기는
        /// 옳지만(그 게이트들이 잠그는 값은 R25 재저작이 <b>출하 6종</b>에 대해 수렴시킨 값이다)
        /// <b>좁힌 사실을 조용히 두면</b> 팩 조형은 영영 아무 자도 안 탄다 — 그것이 정확히
        /// <c>AccessoryRuleOneCoverageTests</c> 클래스 문서가 세 번째 실패로 적어 둔 형태다.</para>
        ///
        /// <para><b>지금 팩을 안 보는 게이트 셋</b>:
        /// <list type="number">
        ///   <item><c>AccessoryHatBandAndBellTests</c> — 보조색 <b>순 색면</b>(경계 획이 먹고 남는 색면 ≥ 1.5펜).</item>
        ///   <item><c>AccessoryHatWearLineBandTests</c> — H-2 <b>착용선 대역</b> [+0.28, +0.45] R · H-2b 앞층 밑단.</item>
        ///   <item><c>EyesVisorOpacityTests</c> — EYES <b>불투명 바이저</b> 규약(채움 실루엣 · 삼각형 덮임).</item>
        /// </list>
        /// 셋 다 <b>숫자가 출하 6종의 재저작에서 나왔다</b>. 팩에 그 숫자를 그대로 들이대는 것은
        /// 검사가 아니라 추측이므로, <b>design-equipment 가 「팩 조형이 지킬 대역」을 먼저 정해야 한다</b>.</para>
        ///
        /// <para>★ <b>자동 만료</b>: 팩 코호트가 0종이 되면 이 등재는 스스로 사라진다(초록). 그리고
        /// 등재 전에 <b>갭이 실재하는지</b>를 먼저 단언한다 — 팩 아이템이 실제로 있고, 그 조각이 실제로
        /// 계약 v2(에셋 조형)라는 것. 그 전제가 무너지면 여기서 <b>빨갛게</b> 멈춘다.</para>
        /// </summary>
        [Test]
        public void 팩_코호트_조형_게이트_셋이_아직_팩을_안_본다()
        {
            int packItems = BaseCohortScope.PackEquipmentCount;
            if (packItems == 0)
            {
                Debug.Log("[에셋조형] 팩 코호트 0종 — 조형 게이트 갭이 오늘은 없다(등재 자동 만료).");
                return;
            }

            // 전제 ① — 팩이 실제로 몸 자리에 있다(게이트가 볼 수 있는 자리인가).
            var uncovered = new List<string>();
            for (int s = 0; s < ItemCatalog.SlotCount; s++)
            {
                var slot = (EquipmentSlot)s;
                if (slot != EquipmentSlot.Head && slot != EquipmentSlot.Eyes) continue;
                for (int item = BaseCohortScope.CountIn(slot); item < ItemCatalog.ItemCountIn(slot); item++)
                {
                    uncovered.Add($"{slot} {item}번({ItemCatalog.Item(slot, item).DisplayName})");
                }
            }
            Assert.IsNotEmpty(uncovered,
                "HEAD/EYES 에 팩 아이템이 없는데 팩 코호트는 " + packItems + "종입니다 — " +
                "이 등재가 가리키는 갭이 실재하지 않습니다. 사유를 다시 쓰거나 이 검사를 지우십시오.");

            // 전제 ② — 그 조각이 실제로 계약 v2(에셋 조형)라 위 세 게이트의 v1 자를 못 댄다.
            var rig = Rig();
            var sink = new List<AccessoryShapeBuilder.Shape>();
            foreach (EquipmentSlot slot in new[] { EquipmentSlot.Head, EquipmentSlot.Eyes })
            {
                for (int item = BaseCohortScope.CountIn(slot); item < ItemCatalog.ItemCountIn(slot); item++)
                {
                    sink.Clear();
                    AccessoryShapeBuilder.Append(sink, slot, item, rig);
                    Assert.Greater(sink.Count, 0, $"{slot} {item}번이 도형을 하나도 안 만듭니다.");
                    for (int k = 0; k < sink.Count; k++)
                    {
                        Assert.IsTrue(sink[k].IsHandoff,
                            $"{slot} {item}번 '{sink[k].Name}'이 v1 조각입니다 — 그렇다면 위 세 게이트의 자를 " +
                            "<b>그대로 댈 수 있고</b>, 이 등재는 더 이상 유효하지 않습니다. " +
                            "게이트 셋의 좁히기를 걷고 이 검사를 지우십시오.");
                    }
                }
            }

            Assert.Ignore("★ 등재된 갭 — 팩 코호트 " + packItems + "종(" + string.Join(" · ", uncovered) + " 등)이 " +
                "조형 게이트 셋 밖에 있다: (1) 보조색 순 색면(AccessoryHatBandAndBellTests) · " +
                "(2) H-2 착용선 대역(AccessoryHatWearLineBandTests) · (3) EYES 불투명 바이저(EyesVisorOpacityTests). " +
                "세 게이트의 숫자는 출하 6종의 R25 재저작에서 나온 값이라 팩에 그대로 댈 수 없다 — " +
                "«팩 조형이 지킬 대역»은 design-equipment 판정 사항이다. " +
                "팩이 v1 조각으로 돌아오거나 팩 코호트가 비면 이 등재는 스스로 만료된다.");
        }

        // ====================================================================
        // 3. ★ 합성 팩 아이템 — 프로덕션 .cs 0줄로 HEAD 자리에 도형이 나오는가
        // ====================================================================

        /// <summary>팩 자리 번호. 기본 42종이 자리 0..5를 쓰므로 팩은 그 뒤다
        /// (<c>StickPackManifestSO.itemIndexBase</c> 문단). <b>숫자를 베끼지 않는다</b> — 표에서 센다.</summary>
        private static int PackItemIndex(EquipmentSlot slot) => ItemCatalog.ItemCountIn(slot);

        /// <summary>합성 팩 모자의 조형 — <b>R 배수 3점</b>. 값 자체에 뜻은 없고
        /// "코드에 없는 새 도형"이라는 데 뜻이 있다.</summary>
        private static readonly float[] PackHatInR =
        {
            -0.80f, 0.40f,
            +0.00f, 1.60f,
            +0.80f, 0.40f,
        };

        /// <summary>R 배수 점열 -> 항 스트림(<see cref="AccessoryWornShapeData"/> 문법).</summary>
        private static float[] Encode(float[] xyInR)
        {
            var terms = new List<float> { xyInR.Length / 2 };
            for (int i = 0; i < xyInR.Length; i += 2)
            {
                terms.AddRange(new[]
                {
                    1f, (float)AccessoryWornBasis.HeadRadius, (float)AccessoryWornGate.Always,
                    (float)AccessoryWornTrig.None, 1f, xyInR[i],
                });
                terms.AddRange(new[]
                {
                    2f,
                    (float)AccessoryWornBasis.HeadCenterLine, (float)AccessoryWornGate.Always,
                    (float)AccessoryWornTrig.None, 0f,
                    (float)AccessoryWornBasis.HeadRadius, (float)AccessoryWornGate.Always,
                    (float)AccessoryWornTrig.None, 1f, xyInR[i + 1],
                });
            }
            return terms.ToArray();
        }

        /// <summary>합성 팩 아이템 <b>에셋</b> 하나. <c>ScriptableObject.CreateInstance</c>로 만드는 것은
        /// <c>PackManifestCorridorTests</c>의 픽스처와 같은 방식이다 — 실제 변환 경로를 태우려는 것이다.</summary>
        private static AccessoryDefSO MakePackHat(int itemIndex)
        {
            var def = ScriptableObject.CreateInstance<AccessoryDefSO>();
            def.name = "fixture_pack_hat";
            def.itemId = "packfixture.head.probe";
            def.slot = EquipmentSlot.Head;
            def.itemIndex = itemIndex;
            def.displayName = "probe";
            def.requiredLevel = ItemCatalog.PackRequiredLevel;
            def.cohortId = ItemCatalog.BaseCohortId + 1;
            def.declaredRarity = ItemCatalog.MaxDeclaredRarityForPack;
            def.wornShapes = new[]
            {
                new AccessoryWornShapeData
                {
                    name = "PackHatShell",
                    loop = true,
                    filled = true,
                    tone = AccessoryTone.Primary,
                    swayStart = -1,
                    swayCount = 0,
                    surfaces = (int)AccessorySurfaces.Default,
                    strokeMult = 1f,
                    strokeInR = 0.13845f,
                    alpha = 1f,
                    lineAlpha = 1f,
                    terms = Encode(PackHatInR),
                },
            };
            return def;
        }

        [Test]
        public void 합성_팩_모자는_에셋만으로_몸에_그려진다()
        {
            AccessoryDefSO def = MakePackHat(PackItemIndex(EquipmentSlot.Head));
            try
            {
                // (1) 카탈로그가 받아들이는 스트림인가 — ItemCatalog.AcceptWornShapes 가 쓰는 그 판정.
                foreach (AccessoryWornShapeData shape in def.wornShapes)
                {
                    Assert.IsTrue(AccessoryWornShapeReader.Validate(shape, out string error),
                        $"합성 팩 조형이 문법에 안 맞습니다: {error}");
                }

                // (2) 이 자리가 에셋 조형을 받는가.
                Assert.IsTrue(AccessoryShapeBuilder.TryWornAssetSlotOrder(def.slot, out int order));

                // (3) 실제로 그린다.
                AccessoryShapeBuilder.Rig rig = Rig();
                var sink = new List<AccessoryShapeBuilder.Shape>();
                var xf = new AccessoryWornTransform(def.wornGroupAlpha, def.wornScale, def.wornScaleY,
                    def.wornOffsetYInR, def.wornMirrorX);
                AccessoryShapeBuilder.AppendWornData(sink, def.wornShapes, xf, def.slot, def.itemIndex,
                    rig, order, false, AccessorySurface.Body);

                Assert.AreEqual(def.wornShapes.Length, sink.Count,
                    "합성 팩 모자의 조각 수가 데이터와 다릅니다.");
                AccessoryShapeBuilder.Shape s = sink[0];
                Assert.AreEqual("PackHatShell", s.Name);
                Assert.AreEqual(order, s.SortingOrder,
                    "팩 조각이 이 자리의 층에 안 들어갔습니다 — 남의 아이템 위/아래로 갑니다.");
                Assert.IsTrue(s.Filled);
                Assert.AreEqual(PackHatInR.Length / 2, s.Points.Length);

                // (4) 좌표가 설계값(R 배수)과 <b>비트까지</b> 같은가.
                for (int i = 0; i < s.Points.Length; i++)
                {
                    Assert.AreEqual(Bits(rig.HeadRadius * PackHatInR[i * 2] * rig.Facing), Bits(s.Points[i].x),
                        $"점 {i}의 x가 설계 R 배수와 다릅니다.");
                    Assert.AreEqual(Bits(rig.HeadCenterY + rig.HeadRadius * PackHatInR[i * 2 + 1]),
                        Bits(s.Points[i].y), $"점 {i}의 y가 설계 R 배수와 다릅니다.");
                }
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }

        // ====================================================================
        // 4. 데이터가 없으면 여전히 크게 실패한다
        // ====================================================================

        /// <summary>에셋도 없고 코드도 모르는 자리 번호는 <b>조용히 사라지지 않는다</b>.
        /// <para>반쪽 팩(매니페스트는 있는데 아이템 에셋이 빠진 경우)의 증상이 "그냥 안 보임"이면
        /// 아무도 원인을 못 찾는다 — 이 저장소가 「조용한 실패」로 부르는 형태다.</para></summary>
        [Test]
        public void 에셋도_코드도_없는_자리는_표식으로_크게_실패한다()
        {
            ShapeCoverageGuard.ResetForTests();
            StickMateDevTools.SetTestOverride(true);
            try
            {
                int unknown = PackItemIndex(EquipmentSlot.Head);
                Assert.IsNull(ItemCatalog.WornShapes(EquipmentSlot.Head, unknown),
                    "그 번호에 이미 형상이 있습니다 — 아래 대조가 뜻을 잃습니다.");

                LogAssert.Expect(LogType.Error, new Regex(@"\[도형\]"));

                var sink = new List<AccessoryShapeBuilder.Shape>();
                AccessoryShapeBuilder.Append(sink, EquipmentSlot.Head, unknown, Rig());

                Assert.Greater(sink.Count, 0,
                    "표식조차 안 나왔습니다 — 개발 게이트를 열었는데 비었다면 이 검사는 껍데기입니다.");
                foreach (AccessoryShapeBuilder.Shape s in sink)
                {
                    StringAssert.StartsWith("Missing", s.Name,
                        "에셋도 코드도 없는 번호에서 표식이 아닌 것이 나왔습니다.");
                }
            }
            finally
            {
                StickMateDevTools.SetTestOverride(null);
                ShapeCoverageGuard.ResetForTests();
            }
        }

        private static uint Bits(float v) => System.BitConverter.ToUInt32(System.BitConverter.GetBytes(v), 0);
    }
}
