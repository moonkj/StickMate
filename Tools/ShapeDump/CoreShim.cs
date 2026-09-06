// ★ StickMate.Core 흉내 — <b>여기 있는 것이 적을수록</b> 이 하니스가 재는 것이 프로덕션에 가깝다.
//
// 규약(2026-09-02): 흉내낸 타입은 전부 shimdrift.py 의 SHIMMED 표에 이유와 함께 등록되고,
//  · enum 은 프로덕션과 이름·값이 <b>완전히</b> 같아야 하며(빠진 값은 컴파일도 되고 조용히 틀린다),
//  · const 는 값이 같아야 하고,
//  · 메서드는 <b>계산을 흉내내지 않는다</b>(계산이 필요하면 프로덕션 파일을 컴파일 목록에 넣는다).
// SlotName/SlotCode 만 예외적으로 표를 베끼는데, 그 문자열은 좌표에도 등급에도 들어가지 않고
// 로그 문구에만 쓰인다(shimdrift.py 가 프로덕션 switch 와 대조한다).
namespace StickMate.Core
{
    public enum EquipmentSlot { Head = 0, Eyes = 1, Neck = 2, Shoulders = 3, Hair = 4, Fx = 5, Pet = 6 }

    public sealed class StickConfig
    {
        public const float BaselineCharacterTotalHeight = 2.2746944f;
        public const float MinStrokeScreenPoints = 2f;
        public const float MinFillOutlineScreenPoints = 1f;
        public const float ReferencePointsPerWorldUnitApprox = 846f / (2f * 12f);
    }

    /// <summary>카테고리 단위 사실만. 착용 상태(_worn)는 이 하니스가 쓰지 않는다 —
    /// 도형 덤프도 등급 파생도 "무엇을 걸쳤는가"와 무관하다.</summary>
    public static class EquipmentModel
    {
        public const int SlotCount = 7;
        public const int NotWorn = -1;

        public static string SlotName(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Head: return "모자";
                case EquipmentSlot.Eyes: return "안경";
                case EquipmentSlot.Neck: return "넥타이";
                case EquipmentSlot.Shoulders: return "망토";
                case EquipmentSlot.Hair: return "머리";
                case EquipmentSlot.Fx: return "이펙트";
                case EquipmentSlot.Pet: return "펫";
                default: return "?";
            }
        }

        public static string SlotCode(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Head: return "HEAD";
                case EquipmentSlot.Eyes: return "EYES";
                case EquipmentSlot.Neck: return "NECK";
                case EquipmentSlot.Shoulders: return "BACK";
                case EquipmentSlot.Hair: return "HAIR";
                case EquipmentSlot.Fx: return "FX";
                case EquipmentSlot.Pet: return "PET";
                default: return "?";
            }
        }

        public static bool IsAppearanceSlot(EquipmentSlot slot) => (int)slot >= (int)EquipmentSlot.Hair;

        /// <summary>★ <b>답을 베끼지 않는다 — 터진다.</b> (2026-09-06 [머리] 은퇴로 프로덕션에 생긴 술어)
        ///
        /// <para>이 하니스는 «무엇이 은퇴했는가»를 쓰지 않는다. 부르는 자리는 <c>ItemCatalog.IsListed</c> ·
        /// <c>ListedEquipmentCount</c> 뿐이고 <c>Dump.cs</c> 는 둘 다 안 부른다 — 좌표도 등급도
        /// 표시 모집단과 무관하다. 그래서 <b>컴파일만</b> 되면 된다.</para>
        ///
        /// <para>여기에 <c>slot == EquipmentSlot.Hair</c> 를 베껴 두면 그 순간 «무엇이 은퇴했는가»가
        /// 두 곳이 되고, 다음에 슬롯이 하나 더 은퇴하는 날 <b>이 하니스만 조용히 옛 답</b>을 준다.
        /// 이 저장소가 이중 정의로 반복해 당한 형태 그대로다. 터지는 쪽을 택했다 —
        /// 덤프가 이 술어를 쓰기 시작하면 <b>rc≠0 으로 시끄럽게</b> 알려야 하고, 그때 사람이
        /// "그럼 EquipmentModel.cs 를 목록에 넣을 때가 됐다"를 판단하면 된다.</para>
        ///
        /// <para>★ <b>이 줄은 프로덕션의 <c>EquipmentModel.IsRetiredSlot</c> 과 한 몸이다.</b>
        /// [머리] 은퇴가 되돌려져 프로덕션에서 그 술어가 사라지면 <c>shimdrift.py</c> 가
        /// "유령 멤버"로 <b>빨간불</b>을 낸다 — 그때는 이 메서드를 지우면 된다(그 빨간불이
        /// 목적이다. 조용히 남아 있으면 하니스가 없는 것을 재게 된다).</para></summary>
        public static bool IsRetiredSlot(EquipmentSlot slot)
            => throw new System.NotSupportedException(
                "ShapeDump 하니스는 은퇴 여부를 흉내내지 않는다(CoreShim.cs). " +
                "덤프가 이것을 부르기 시작했다면 Core/EquipmentModel.cs 를 build.sh 목록에 넣어라.");

        /// <summary>이 하니스에서는 <b>아무것도 걸치지 않았다</b>. 등급·좌표 어느 쪽에도 들어가지 않는다.</summary>
        public static int WornIndex(EquipmentSlot slot) => NotWorn;
    }

    /// <summary>QA 해금 스위치. 오프라인 덤프는 <b>릴리스와 같은 상태</b>(닫힘)로 둔다.</summary>
    public static class EquipmentDebugUnlock
    {
        public static bool UnlockAll => false;
    }

    /// <summary>레벨은 보유 판정에만 쓰이고 등급 파생에는 안 쓰인다(등급은 requiredLevel 순위다).</summary>
    public static class CharacterProgressionModel
    {
        public static int Level => 1;
    }
}
