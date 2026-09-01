#if UNITY_EDITOR
using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// ★ 카탈로그 <b>골든 마스터 다이제스트</b> — DLC 이행 A단계(에셋화)의 회귀 잠금 장치.
    ///
    /// ============================================================================
    /// 왜 필요한가
    /// ============================================================================
    /// 28종의 이름/설명/레벨/아이콘 좌표 수천 개를 코드에서 에셋으로 옮기는 작업은, 값 하나가
    /// 조용히 어긋나도 컴파일도 되고 테스트도 대부분 초록이다(왕관 좌표 한 점이 틀린 것을 잡는
    /// 단언은 아무도 안 썼다). 그래서 전환 <b>직전</b>의 카탈로그 전체를 텍스트로 굳혀 두고,
    /// 전환 <b>이후</b> 같은 함수가 만든 텍스트와 <b>완전 일치</b>하는지만 본다.
    ///
    /// ============================================================================
    /// 형식 규칙
    /// ============================================================================
    ///  · 부동소수는 <b>F5 고정소수 + InvariantCulture</b>. 이유는 두 가지다 —
    ///    (1) YAML 왕복에서 마지막 비트가 흔들려도 40×40 좌표에서 1e-5는 의미가 없다.
    ///    (2) "R"(왕복 포맷)을 쓰면 0.90980393 vs 0.9098039 같은 <b>표기 차이</b>가 곧바로
    ///        가짜 실패가 된다. 잡고 싶은 것은 표기가 아니라 값이다.
    ///  · 줄 순서는 표 순서(슬롯 오름차순 → 자리 오름차순 → 행동)로 고정한다. 순서가 곧 사실이다
    ///    (<c>AccessoryShapeBuilder</c>의 switch가 자리 번호로 도형을 고른다).
    ///
    /// 에디터 전용이다 — 출하 빌드에는 한 바이트도 들어가지 않는다.
    /// </summary>
    public static class ItemCatalogDigest
    {
        /// <summary>골든 파일 경로(프로젝트 루트 기준). 테스트와 마이그레이션 도구가 같은 값을 본다.</summary>
        public const string GoldenAssetPath =
            "Assets/_Project/Scripts/Tests/EditMode/Golden/ItemCatalogGolden.txt";

        private static string F(float v) => v.ToString("F5", CultureInfo.InvariantCulture);

        private static string C(Color c) => $"({F(c.r)},{F(c.g)},{F(c.b)},{F(c.a)})";

        /// <summary>지금 <see cref="ItemCatalog"/>가 말하는 전부를 한 덩어리 텍스트로.</summary>
        public static string Build()
        {
            var sb = new StringBuilder(64 * 1024);
            sb.Append("# StickMate ItemCatalog golden digest\n");
            sb.Append("# 형식: F5 고정소수 / InvariantCulture / 표 순서 고정. 손으로 고치지 말 것.\n");
            sb.Append("slots=").Append(ItemCatalog.SlotCount).Append('\n');
            sb.Append("equipment=").Append(ItemCatalog.EquipmentCount).Append('\n');
            sb.Append("actions=").Append(ItemCatalog.ActionCount).Append('\n');

            for (int s = 0; s < ItemCatalog.SlotCount; s++)
            {
                var slot = (EquipmentSlot)s;
                sb.Append("[slot ").Append(s).Append(' ').Append(slot).Append(" count=")
                  .Append(ItemCatalog.ItemCountIn(slot)).Append("]\n");

                for (int i = 0; i < ItemCatalog.ItemCountIn(slot); i++)
                {
                    AppendEntry(sb, ItemCatalog.Item(slot, i));
                }
            }

            sb.Append("[actions]\n");
            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                ItemCatalogEntry entry = ItemCatalog.At(i);
                if (entry.Category == ItemCategory.Action) AppendEntry(sb, entry);
            }
            return sb.ToString();
        }

        private static void AppendEntry(StringBuilder sb, ItemCatalogEntry e)
        {
            if (e == null)
            {
                sb.Append("  <null entry>\n");
                return;
            }

            sb.Append("  item id=").Append(e.Id)
              .Append(" cat=").Append(e.Category)
              .Append(" slot=").Append(e.Slot.HasValue ? e.Slot.Value.ToString() : "-")
              .Append(" idx=").Append(e.ItemIndex)
              .Append(" lv=").Append(e.RequiredLevel.HasValue ? e.RequiredLevel.Value.ToString(CultureInfo.InvariantCulture) : "-")
              .Append(" invocable=").Append(e.IsDirectlyInvocable ? 1 : 0)
              .Append('\n');
            sb.Append("    name=").Append(e.DisplayName).Append('\n');
            sb.Append("    desc=").Append(e.Description).Append('\n');
            sb.Append("    status=").Append(NeutralStatus(e.ActionStatus)).Append('\n');
            sb.Append("    primary=").Append(C(e.PrimaryColor))
              .Append(" secondary=").Append(C(e.SecondaryColor)).Append('\n');

            if (e.Icon == null)
            {
                sb.Append("    icon=none\n");
                return;
            }

            sb.Append("    icon=").Append(e.Icon.Length).Append('\n');
            for (int p = 0; p < e.Icon.Length; p++)
            {
                ItemIconPart part = e.Icon[p];
                sb.Append("      p").Append(p)
                  .Append(" kind=").Append(part.Kind)
                  .Append(" tone=").Append(part.Tone)
                  .Append(" color=").Append(C(part.Color))
                  .Append(" n=").Append(part.Values != null ? part.Values.Length : -1)
                  .Append(" v=");
                if (part.Values != null)
                {
                    for (int v = 0; v < part.Values.Length; v++)
                    {
                        if (v > 0) sb.Append(',');
                        sb.Append(F(part.Values[v]));
                    }
                }
                sb.Append('\n');
            }
        }

        /// <summary>
        /// ★ 2026-09-01 — 상태 슬롯의 <b>조합키 접두사</b>를 중립 토큰으로 바꿔 적는다.
        ///
        /// <para>이날 단축키 표기가 플랫폼별로 갈렸다(<see cref="ShortcutLabel"/>: macOS <c>⌃⌥⌘A</c> /
        /// Windows <c>Ctrl+Alt+Win+A</c>). 골든을 그대로 두면 <b>같은 코드가 Windows 머신에서만
        /// 11줄 어긋난다</b> — 이 프로젝트가 반복해서 겪은 "한 플랫폼에서만 조용히 빨개지는" 실패다.</para>
        ///
        /// <para>골든이 잡으려는 것은 "<b>어느 행동에 단축키가 있는가</b>"이지 "이 머신이 무엇을
        /// 표시하는가"가 아니다. 표기 자체의 정확성은
        /// <c>Tests/EditMode/ShortcutLabelParityTests</c>가 <b>두 플랫폼 표를 다 계산해</b> 따로 잠근다.
        /// 그래서 여기서는 접두사만 <c>&lt;chord&gt;</c>로 접고 <b>동작키는 그대로 둔다</b> —
        /// 어느 행동의 키가 바뀌면 골든은 여전히 빨개진다.</para>
        /// </summary>
        private static string NeutralStatus(string status)
        {
            if (status == null) return "-";
            if (status.StartsWith(ShortcutLabel.MacModifiers, StringComparison.Ordinal))
            {
                return "<chord>" + status.Substring(ShortcutLabel.MacModifiers.Length);
            }
            if (status.StartsWith(ShortcutLabel.WindowsModifiers, StringComparison.Ordinal))
            {
                return "<chord>" + status.Substring(ShortcutLabel.WindowsModifiers.Length);
            }
            return status;
        }

        /// <summary>두 다이제스트의 <b>첫 번째로 다른 줄</b>을 사람이 읽을 수 있게. 같으면 null.
        /// 6만 자짜리 문자열 두 개를 통째로 화면에 뱉으면 아무도 원인을 못 찾는다.</summary>
        public static string FirstDifference(string expected, string actual)
        {
            if (expected == actual) return null;

            string[] a = expected.Replace("\r\n", "\n").Split('\n');
            string[] b = actual.Replace("\r\n", "\n").Split('\n');
            int n = a.Length < b.Length ? a.Length : b.Length;

            for (int i = 0; i < n; i++)
            {
                if (a[i] == b[i]) continue;
                return $"{i + 1}번째 줄이 다릅니다.\n  골든 : {a[i]}\n  현재 : {b[i]}";
            }
            return $"줄 수가 다릅니다(골든 {a.Length}줄 / 현재 {b.Length}줄). " +
                   (a.Length > b.Length ? $"골든에만 있는 다음 줄: {a[n]}" : $"현재에만 있는 다음 줄: {b[n]}");
        }
    }
}
#endif
