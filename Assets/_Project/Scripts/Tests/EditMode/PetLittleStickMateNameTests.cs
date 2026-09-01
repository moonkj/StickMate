using System.IO;
using NUnit.Framework;
using StickMate.Core;
using UnityEditor;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 사용자 명시 요청(2026-08-31): PET 자리 2번의 표시 이름을 <b>"작은졸라맨" → "리틀스틱메이트"</b>로.
    ///
    /// ============================================================================
    /// 왜 별도 테스트가 필요한가 — 골든 대조만으로는 부족하다
    /// ============================================================================
    /// <see cref="ItemCatalogAssetParityTests"/>의 골든 대조는 "지금 값과 굳혀 둔 값이 같다"만 본다.
    /// 즉 <b>둘 다 틀린</b> 경우(이름을 안 바꾼 채 골든만 다시 뽑는 사고)를 잡지 못한다 —
    /// 골든 갱신 도구(<c>StickMate/DLC 이행 A/1</c>)가 회귀 잠금을 <b>스스로 풀 수 있는</b> 구조라
    /// (AccessoryDefMigration 클래스 문서가 그 위험을 명시한다), 사용자가 말로 지정한 이름 하나는
    /// 골든과 <b>독립된</b> 단언으로 따로 못 박아 둔다.
    ///
    /// 잠그는 것:
    ///  N1  런타임 카탈로그가 내주는 표시 이름이 "리틀스틱메이트"다(화면/정보창이 읽는 바로 그 값).
    ///  N2  그 값의 출처인 에셋 파일 자신도 같은 값이다(카탈로그가 어딘가에서 이름을 덧씌우지 않았다).
    ///  N3  옛 이름이 프로젝트 어디에도 <b>표시 문자열로</b> 남아 있지 않다(골든 포함).
    ///  N4  <b>아이디는 그대로다</b> — AccessoryDefSO 문서의 경고 그대로, 아이디를 함께 바꾸면
    ///      사용자의 저장된 차림이 사라진다. 이름 변경이 아이디까지 건드리지 않았음을 못 박는다.
    /// </summary>
    public sealed class PetLittleStickMateNameTests
    {
        private const string ExpectedName = "리틀스틱메이트";
        private const string LegacyName = "작은졸라맨";
        private const string ItemId = "look.pet.mini";
        private const string AssetPath = "Assets/_Project/Resources/Items/look_pet_mini.asset";

        /// <summary>PET 카테고리의 "리틀스틱메이트" 자리(AppearanceShapeBuilder.PetMini와 같은 값).</summary>
        private const int PetMiniIndex = 2;

        [Test]
        public void N1_카탈로그가_내주는_표시이름이_리틀스틱메이트다()
        {
            ItemCatalogEntry entry = ItemCatalog.Item(EquipmentSlot.Pet, PetMiniIndex);
            Assert.IsNotNull(entry, $"PET 자리 {PetMiniIndex}번 항목을 카탈로그에서 찾지 못했습니다.");
            Assert.AreEqual(ExpectedName, entry.DisplayName,
                $"★ 사용자가 지정한 이름이 아닙니다(지금 \"{entry.DisplayName}\"). " +
                $"{AssetPath}의 displayName을 확인하세요.");
        }

        [Test]
        public void N2_에셋_파일_자신도_같은_이름을_들고_있다()
        {
            var def = AssetDatabase.LoadAssetAtPath<AccessoryDefSO>(AssetPath);
            Assert.IsNotNull(def, $"아이템 에셋을 찾지 못했습니다: {AssetPath}");
            Assert.AreEqual(ExpectedName, def.displayName,
                "카탈로그와 에셋이 서로 다른 이름을 들고 있습니다 — 둘 중 하나가 값을 덧씌우고 있습니다.");
        }

        [Test]
        public void N3_옛_이름이_골든에도_에셋에도_남아_있지_않다()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;

            string goldenPath = Path.Combine(root, ItemCatalogDigest.GoldenAssetPath);
            Assert.IsTrue(File.Exists(goldenPath), $"골든 스냅샷이 없습니다: {ItemCatalogDigest.GoldenAssetPath}");
            string golden = File.ReadAllText(goldenPath);

            Assert.IsTrue(golden.Contains($"name={ExpectedName}"),
                "골든 스냅샷이 새 이름을 담고 있지 않습니다 — 이름을 바꾼 뒤 " +
                "메뉴 StickMate/DLC 이행 A/1로 골든을 갱신했는지 확인하세요(의도된 변경입니다).");
            Assert.IsFalse(golden.Contains($"name={LegacyName}"),
                $"골든 스냅샷에 옛 이름(\"{LegacyName}\")이 아직 남아 있습니다.");

            // 카탈로그 전체를 훑어 옛 이름이 다른 자리로 새어 들어가지 않았는지도 함께 본다.
            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                ItemCatalogEntry e = ItemCatalog.At(i);
                if (e == null) continue;
                Assert.AreNotEqual(LegacyName, e.DisplayName,
                    $"카탈로그 {i}번({e.Id})이 아직 옛 이름을 쓰고 있습니다.");
            }
        }

        [Test]
        public void N4_이름만_바뀌고_아이디_슬롯_자리_레벨은_그대로다()
        {
            ItemCatalogEntry entry = ItemCatalog.Item(EquipmentSlot.Pet, PetMiniIndex);
            Assert.IsNotNull(entry);

            // 아이디를 함께 바꾸면 저장된 차림이 사라진다(AccessoryDefSO 클래스 문서의 경고).
            Assert.AreEqual(ItemId, entry.Id,
                "★ 표시 이름만 바꿔야 하는데 아이템 아이디가 바뀌었습니다 — 사용자의 저장된 차림이 사라집니다.");
            Assert.AreEqual(EquipmentSlot.Pet, entry.Slot);
            Assert.AreEqual(PetMiniIndex, entry.ItemIndex);
            Assert.AreEqual(19, entry.RequiredLevel,
                "요구 레벨까지 함께 바뀌었습니다 — 이번 라운드는 이름 1건만 바꾸는 변경입니다.");
            Assert.AreEqual("똑같이 생겼다.", entry.Description,
                "설명까지 함께 바뀌었습니다 — 이번 라운드는 이름 1건만 바꾸는 변경입니다.");
        }
    }
}
