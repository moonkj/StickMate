using System.IO;
using System.Text;
using StickMate.Core;
using UnityEditor;
using UnityEngine;

namespace StickMate.EditorTools
{
    /// <summary>
    /// ★ DLC 이행 <b>A단계</b> 도구 — 하드코딩 28종을 <see cref="AccessoryDefSO"/> 에셋 28개로 굳힌다.
    /// (근거: docs/ARCHITECTURE.md 5-3-3)
    ///
    /// ============================================================================
    /// 실행 순서(한 번만 해도 되는 작업이다)
    /// ============================================================================
    ///  1. <see cref="ExportGolden"/> — <b>전환 전</b> 카탈로그 전체를 골든 텍스트로 굳힌다.
    ///  2. <see cref="GenerateAssets"/> — 카탈로그가 말하는 값을 그대로 에셋에 눕힌다.
    ///  3. <c>ItemCatalog</c>를 에셋 로드로 갈아탄 뒤, EditMode 테스트가 골든과 대조한다.
    ///
    /// ============================================================================
    /// 갈아탄 뒤에 이 도구를 다시 돌리면?
    /// ============================================================================
    /// <see cref="GenerateAssets"/>는 이제 "에셋 → 카탈로그 → 에셋"이라 <b>항등 재출력</b>이다(무해).
    /// <see cref="ExportGolden"/>은 골든을 <b>덮어쓴다</b> — 즉 회귀 잠금을 스스로 풀 수 있다.
    /// 그래서 골든 갱신은 값이 <b>의도적으로</b> 바뀐 라운드에서만, 리뷰에서 diff를 보고 한다.
    /// </summary>
    public static class AccessoryDefMigration
    {
        private const string AssetFolder = "Assets/_Project/Resources/Items";

        [MenuItem("StickMate/DLC 이행 A/1. 골든 스냅샷 내보내기")]
        public static void ExportGolden()
        {
            string path = Path.Combine(ProjectRoot(), ItemCatalogDigest.GoldenAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            // 개행을 LF로 고정한다 — 플랫폼마다 다른 개행이 들어가면 골든 비교가 OS 의존이 된다.
            File.WriteAllText(path, ItemCatalogDigest.Build().Replace("\r\n", "\n"), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log($"[A단계] 골든 스냅샷 기록: {ItemCatalogDigest.GoldenAssetPath}");
        }

        [MenuItem("StickMate/DLC 이행 A/2. 아이템 에셋 생성")]
        public static void GenerateAssets()
        {
            EnsureFolder(AssetFolder);

            int written = 0;
            for (int s = 0; s < ItemCatalog.SlotCount; s++)
            {
                var slot = (EquipmentSlot)s;
                for (int i = 0; i < ItemCatalog.ItemCountIn(slot); i++)
                {
                    ItemCatalogEntry entry = ItemCatalog.Item(slot, i);
                    if (entry == null) continue;

                    string assetPath = $"{AssetFolder}/{entry.Id.Replace('.', '_')}.asset";
                    var def = AssetDatabase.LoadAssetAtPath<AccessoryDefSO>(assetPath);
                    bool isNew = def == null;
                    if (isNew) def = ScriptableObject.CreateInstance<AccessoryDefSO>();

                    Fill(def, entry, slot, i);

                    if (isNew) AssetDatabase.CreateAsset(def, assetPath);
                    else EditorUtility.SetDirty(def);
                    written++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[A단계] 아이템 에셋 {written}개 기록 완료: {AssetFolder}");
        }

        /// <summary>배치 실행용(-executeMethod). 골든 먼저, 에셋 나중 — 순서가 뒤집히면
        /// "에셋에서 나온 값"을 골든이라고 부르게 되어 대조가 자기 자신과의 비교로 무의미해진다.</summary>
        public static void RunAll()
        {
            ExportGolden();
            GenerateAssets();
        }

        private static void Fill(AccessoryDefSO def, ItemCatalogEntry entry, EquipmentSlot slot, int index)
        {
            def.itemId = entry.Id;
            def.slot = slot;
            def.itemIndex = index;
            def.displayName = entry.DisplayName;
            def.description = entry.Description;
            def.requiredLevel = entry.RequiredLevel ?? 1;
            def.hidesHair = HidesHair(slot, index);

            ItemIconPart[] icon = entry.Icon;
            def.icon = new AccessoryIconPartData[icon != null ? icon.Length : 0];
            for (int p = 0; p < def.icon.Length; p++)
            {
                float[] src = icon[p].Values;
                var values = new float[src != null ? src.Length : 0];
                if (src != null) System.Array.Copy(src, values, src.Length);

                def.icon[p] = new AccessoryIconPartData
                {
                    kind = icon[p].Kind,
                    values = values,
                    color = icon[p].Color,
                    tone = icon[p].Tone,
                };
            }
            def.name = entry.Id;
        }

        /// <summary>
        /// 지금 렌더러가 <b>실제로</b> 하는 일을 그대로 옮겨 적는다 — 새 판단을 하지 않는다.
        /// 근거: <c>Interaction/AccessoryShapeBuilder.HatCoverLocalY</c> — 천모자/털모자/중절모는
        /// 유한한 커버선을 선언하고(= 머리카락을 덮는다), <b>왕관은 +∞</b>(= 덮지 않는다. 씌우는 것이
        /// 아니라 얹는 것이라 밑이 뚫려 있다). 모자 외의 카테고리는 머리카락에 관여하지 않는다.
        /// <para>이 값이 렌더러와 어긋나지 않는지는 EditMode 테스트가 <c>HatCoverLocalY</c>를 직접
        /// 불러 확인한다(이 어셈블리에서는 internal이라 부를 수 없다).</para>
        /// </summary>
        private static bool HidesHair(EquipmentSlot slot, int index)
            => slot == EquipmentSlot.Head && index != 3;   // 3 = 왕관

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string[] parts = path.Split('/');
            string acc = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{acc}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(acc, parts[i]);
                acc = next;
            }
        }

        private static string ProjectRoot() => Directory.GetParent(Application.dataPath).FullName;
    }
}
