namespace StickMate.Core
{
    public enum EquipmentSlot { Head = 0, Eyes = 1, Neck = 2, Shoulders = 3, Hair = 4, Fx = 5, Pet = 6 }

    public static class StickConfig
    {
        public const float BaselineCharacterTotalHeight = 2.2746944f;
        public const float MinStrokeScreenPoints = 2f;
        public const float ReferencePointsPerWorldUnitApprox = 846f / (2f * 12f);
    }
}
