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
    }
}
