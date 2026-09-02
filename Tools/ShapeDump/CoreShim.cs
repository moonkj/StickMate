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
