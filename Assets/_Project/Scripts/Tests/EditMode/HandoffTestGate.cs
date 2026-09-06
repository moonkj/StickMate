using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 인계본 교체(계약 v2, 2026-09-05) — <b>v1 몸 도형을 이름·규칙으로 잠그던 게이트</b>가 그 아이템에서 뜻을 잃었을 때
    /// <b>조용히 지우지 않고</b> 건너뛰기로 남기는 한 곳.
    ///
    /// <para>왜 삭제가 아니라 건너뛰기인가: CLAUDE.md 「아직 못 고친 갭은 Assert.Fail 이 아니라 Assert.Ignore(사유 포함)로 남겨
    /// 러너에 '건너뜀'으로 계속 보이게 한다 — 잊히지 않게」. 인계본 16종은 카드·몸 좌표가 설계 모델에서 기계로 내려오고
    /// (<c>CardShapeContractTests</c> 골든), 획 규칙은 그 배율의 1pt 실폭으로 오프라인에서 잰다
    /// (EQUIPMENT_HANDOFF_PORT_SPEC §14-6 #14 — 정원 2~4 · 보조색 1 · 감쌈 · 낱선 1.5획 자는 이 표면에 안 맞는다).
    /// 그 아이템이 v1 로 되돌아오면(인계본 조각이 사라지면) 이 게이트는 스스로 다시 열린다 — 판정이 이름이 아니라
    /// <see cref="AccessoryShapeBuilder.Shape.IsHandoff"/>(명목 획 &gt; 0) 이기 때문이다.</para>
    ///
    /// <para><b>잃는 것을 적는다</b> — 각 호출부가 사유 문자열에 잃는 규칙을 명시한다. 러너의 건너뜀 메시지가 곧 손실 목록이다.</para>
    /// </summary>
    internal static class HandoffTestGate
    {
        internal static bool IsHandoffItem(EquipmentSlot slot, int item)
        {
            List<AccessoryShapeBuilder.Shape> body = AccessorySilhouetteMetrics.Build(AccessorySilhouetteMetrics.Rig(), slot, item);
            for (int i = 0; i < body.Count; i++)
            {
                if (body[i].IsHandoff) return true;
            }
            return false;
        }

        /// <summary>인계본 아이템이면 <see cref="Assert.Ignore(string)"/> — 사유에 <paramref name="lostRule"/>(잃는 규칙)을 남긴다.</summary>
        internal static void SkipIfHandoff(EquipmentSlot slot, int item, string lostRule)
        {
            if (!IsHandoffItem(slot, item)) return;
            Assert.Ignore($"★ 인계본 교체(계약 v2, 2026-09-05) — {slot} {item}번({ItemCatalog.Item(slot, item).DisplayName}): " +
                $"이 검사가 잠그던 v1 규칙은 인계본 표면에 안 맞는다(§14-6 #14). 잃는 것: {lostRule}. " +
                "대체 자: CardShapeContractTests(설계 골든 · 좌표/역할/흔들 구간) + design/equipment/verify/r16_model.survival(1pt 실폭). " +
                "아이템이 v1 로 되돌아오면 이 게이트는 스스로 다시 열린다.");
        }

        /// <summary>
        /// ★ R25 재저작(2026-09-06) — 베레모·밀짚모자가 <b>R21 재저작본</b>으로 바뀌면서 위와 <b>같은 일</b>이
        /// 두 v1 아이템에서 일어났다. 두 종은 여전히 v1 계약(<c>strokeInR 0</c>)이라
        /// <see cref="SkipIfHandoff"/>가 못 잡는다 — 그래서 게이트가 하나 더 필요하다.
        ///
        /// <para><b>무엇이 뜻을 잃었나</b>: 2026-09-01~09-03의 v1 모자 교리 두 가지다.
        /// (가) 「얹지 말고 감싼다」(커버선 ≤ 0.10 R · |x| ≥ 0.85 R 이면서 y ≤ 0.05 R 인 잉크) —
        /// R25 는 H-2 착용선을 <b>대역</b>으로 잡고 모자를 올렸다(사용자 신고 「안경을 너무 가린다」).
        /// (나) 「올린 띠」 규약(아랫변 + 그것을 <see cref="AccessoryShapeBuilder.AccentBandThicknessRatio"/>만큼
        /// 올린 역순 윗변) — R21 띠는 자기 두께를 가진 아이콘 조각이고 관 밑변에서 유도되지 않는다.</para>
        ///
        /// <para>★★ <b>2026-09-06 갱신 — (나)는 해소됐다. 호출부가 5 → 3으로 줄었다</b>(test-engineer 재검토).
        /// 같은 날 R25d 가 두 띠 좌표를 <b>몸/관 밑변에서 유도</b>하도록 다시 써서(<c>V1Hat_BeretBand</c> ←
        /// <c>V1Hat_BeretBody</c>의 닫힘변 · <c>V1Hat_StrawBand</c> ← <c>V1Hat_StrawCrown</c>의 닫힘변,
        /// 윗변은 각각 그것을 0.46 R 올린 것) 「올린 띠」 규약이 <b>다시 성립한다</b>. 재측정(배율 0.75):
        /// 이음매 2.5e-16 R · 두께 2.11획(베레모) / 2.08획(밀짚모자) · y 법칙 오차 4.2e-17 유닛.
        /// 그래서 그 두 검사의 게이트를 걷었다.</para>
        ///
        /// <para>★★ <b>(가)도 같은 날 정리됐다 — 걷은 것이 아니라 「삭제」다. 호출부 3 → 2.</b>
        /// 리더 판정(2026-09-06): 커버선 ≤ +0.10 R 은 같은 날 정본화된 H-2 착용선 대역 [+0.28, +0.45] R 과
        /// <b>수학적으로 양립 불가</b>다(실측 커버선 베레모 +0.3602 · 밀짚모자 +0.4162 = 옛 상한의 3.6~4.2배,
        /// 감쌈 잉크는 둘 다 없음). 즉 「아직 못 고친 갭」이 아니라 <b>오늘 밤의 확정 수정이 대체해 버린 교리</b>라
        /// 건너뛰기로 남길 대상이 아니었다. 그 검사는 <c>AccessoryStrokeBudgetTests</c> 에서 <b>묘비 주석과 함께
        /// 삭제</b>됐고, 그 자리는 <c>AccessoryHatWearLineBandTests</c>(+ 짝 파일 CoverLine)가 더 정확한
        /// 기준으로 이미 지키고 있다.</para>
        ///
        /// <para><b>남은 2곳이 왜 아직 여기 있는가</b>(각 호출부에 숫자와 함께 적혀 있다): 음성 대조 2건은
        /// <b>재저작</b>이 필요하다 — 재구성 좌표의 기준선이 커버선으로 바뀌어 이름·기대값(0.26 / 0.47획)이
        /// 실측(0.0064 / 0.32획)과 갈라졌다. 걷으면 «사실이 아닌 문장에 감싸인 초록»이 남는다.</para>
        ///
        /// <para>★ <b>건너뛰기가 revert 를 넘어 살아남지 못하게</b> 한다. 그냥 «이 두 번호는 건너뛴다»로
        /// 두면 누군가 옛 도형으로 되돌려도 러너는 계속 「건너뜀」만 찍는다 — 부재 단언이 조용히
        /// 초록이 되는 CLAUDE.md 의 그 형태다. 그래서 건너뛰기 전에 <b>R25 도형에만 있는 조각</b>이
        /// 실재하는지 먼저 단언한다(베레모 꼭지 · 밀짚모자 2층 챙의 먼 쪽). 되돌리면 여기서
        /// <b>빨갛게</b> 멈춘다.</para>
        /// </summary>
        internal static void SkipIfR25Hat(int item, string lostRule)
        {
            string marker;
            if (item == AccessoryShapeBuilder.HeadBeret) marker = "BeretStem";
            else if (item == AccessoryShapeBuilder.HeadStraw) marker = "StrawBrimFar";
            else return;

            List<AccessoryShapeBuilder.Shape> body =
                AccessorySilhouetteMetrics.Build(AccessorySilhouetteMetrics.Rig(), EquipmentSlot.Head, item);
            bool found = false;
            for (int i = 0; i < body.Count; i++)
            {
                if (body[i].Name == marker) found = true;
            }
            Assert.IsTrue(found,
                $"★ {ItemCatalog.Item(EquipmentSlot.Head, item).DisplayName}에 R25 표식 조각 '{marker}'이 없습니다 — " +
                "이 아이템이 옛 v1 도형으로 되돌아갔다는 뜻이고, 그렇다면 이 검사는 건너뛸 것이 아니라 " +
                "<b>다시 돌아야</b> 합니다. 건너뛰기가 되돌림보다 오래 사는 것을 여기서 막습니다.");

            Assert.Ignore($"★ R25 재저작(2026-09-06) — HEAD {item}번({ItemCatalog.Item(EquipmentSlot.Head, item).DisplayName}): " +
                $"이 검사가 잠그던 v1 규칙은 R21 재저작본에 안 맞는다. 잃는 것: {lostRule}. " +
                "대체 자: design/equipment/verify/r24_hats.py(프로덕션 소스 직접 파싱 — H-2 착용선 대역 · 안경 가려짐). " +
                $"'{marker}'이 사라지면 이 게이트는 위 단언에서 스스로 다시 열린다.");
        }
    }
}
