using UnityEngine;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// 인계본 조각(계약 v2)의 색 — 잉크 / 재질색 M·M2 / 머리 채움 / 잉크 대비색 — 를 <b>어디서 가져오는가</b>
    /// (재질 팔레트 <c>docs/EQUIPMENT_PALETTE.md</c> §2 · §2-3, 2026-09-05 사용자 지시 "각각 아이템들의 색상을 채워 넣어야함").
    /// <list type="bullet">
    ///   <item>잉크(채움 있는 조각의 윤곽 · 채움 위 낱선): 카드 = <see cref="UiChrome.CardIconInk"/> · 몸 = 유저 잉크(<c>ResolveInkColor</c>).
    ///     ★ 몸의 브라스 선(R16/R17)은 밝은 바탕 2.16·흰 잉크 2.41 로 미달이라 잉크로 바뀌었다(팔레트 §3).</item>
    ///   <item>재질색 M/M2: <b>아이템 정의 한 원천</b> — <c>entry.PrimaryColor</c>/<c>SecondaryColor</c>(.asset icon tone 0/1). 카드·몸 같은 hex.
    ///     <c>WornColor</c> 틴트를 타지 않는다(사용자 확정 「카드와 몸은 같은 hex」 · 재질색은 대역 안이라 항등이기도 하다).</item>
    ///   <item>머리 채움: 캐릭터 잉크 그대로(역할 HeadInk 의 채움 — 지금 16종은 안 쓴다. H-1 바탕은 부모의 M/M2 로 굽는다).</item>
    ///   <item>잉크 대비색: 잉크 휘도가 어두우면 흰, 밝으면 목탄 — 반대쪽 눈(R17 E-1, 리더 판정 #3).</item>
    ///   <item>★ <b>등급색은 없다</b>(리더 판정 L-1). 등급은 카드 프레임·리본·낱말(<c>CharacterInfoWindow.Cards</c>)만의 것이다.</item>
    /// </list>
    /// <para>이 파일이 <c>AccessoryShapeBuilder</c> 안에 있지 않은 이유: Tools/ShapeDump 가 그 파일을 UI 없이 컴파일한다.</para>
    /// </summary>
    internal static class AccessoryHandoffPalette
    {
        /// <summary>
        /// 흰 눈과 목탄 눈이 잉크 위에서 <b>같은 대비</b>를 갖는 잉크 휘도 — (1+0.05)/(L+0.05) = (L+0.05)/(L_c+0.05)에서
        /// L_c(목탄 <see cref="UiChrome.InkContrastCharcoal"/>) ≈ 0.020 을 넣으면 L ≈ 0.22. 그 아래(검은 잉크 기본값)는 흰, 위는 목탄.
        /// </summary>
        internal const float InkContrastLuminanceThreshold = 0.22f;

        /// <summary>잉크의 대비색 — 반대쪽 눈. 잉크가 어두우면 흰, 밝으면 목탄.</summary>
        internal static Color ContrastTo(Color ink)
            => UiChrome.RelativeLuminance(ink) < InkContrastLuminanceThreshold ? Color.white : UiChrome.InkContrastCharcoal;

        /// <param name="ink">캐릭터 잉크(머리 채움색). <c>CharacterAccessoryRenderer.ResolveInkColor</c>가 준다.</param>
        internal static AccessoryShapeBuilder.HandoffPalette Body(EquipmentSlot slot, int item, Color ink)
        {
            ItemCatalogEntry entry = ItemCatalog.Item(slot, item);
            return new AccessoryShapeBuilder.HandoffPalette(ink, AccessoryShapeBuilder.WornTransformOf(slot, item).GroupAlpha,
                ink, ContrastTo(ink), MaterialOf(entry, ink), Material2Of(entry, ink));
        }

        /// <summary>카드. <paramref name="primary"/>/<paramref name="secondary"/>는 부르는 쪽(정보창)이 카탈로그에서 준 그 아이템의 M/M2 다 —
        /// 몸과 같은 원천이고, 시트 대조(<c>CardIconProbe</c> brass 변종)가 입력색을 바꿔 넣을 수 있는 자리다.</summary>
        internal static AccessoryShapeBuilder.HandoffPalette Card(EquipmentSlot slot, int item, Color primary, Color secondary)
            => new AccessoryShapeBuilder.HandoffPalette(UiChrome.CardIconInk, 1f, UiChrome.CardSurfaceMuted, UiChrome.CardIconInk,
                primary, secondary);

        // 카탈로그가 비어 있으면(테스트 더미·팩 오류) 잉크로 접는다 — 조용히 투명해지는 것보다 잉크 덩어리가 낫다(빠진 도형 표식과 같은 철학).
        private static Color MaterialOf(ItemCatalogEntry entry, Color fallback) => entry != null ? entry.PrimaryColor : fallback;
        private static Color Material2Of(ItemCatalogEntry entry, Color fallback) => entry != null ? entry.SecondaryColor : fallback;
    }
}
