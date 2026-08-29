using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// 장비 슬롯 4종. 슬롯마다 아이템이 하나씩만 있고 서로 독립이라(동시에 4개 다 착용 가능) 슬롯과
    /// 아이템을 굳이 분리하지 않는다 — 아이템이 슬롯당 여러 개가 되는 날에 그때 나누면 되고, 지금
    /// 나눠 두면 쓰이지 않는 추상화만 늘어난다.
    /// </summary>
    public enum EquipmentSlot
    {
        /// <summary>머리 — 모자(캡). 챙이 진행 방향으로 뻗는 <b>비대칭</b> 아이템이라 좌우 반전 검증 대상.</summary>
        Head = 0,

        /// <summary>눈 — 선글라스. 렌즈 2개는 대칭이지만 안경다리가 진행 반대쪽으로 뻗어 비대칭이다.</summary>
        Eyes = 1,

        /// <summary>목 — 나비넥타이. 좌우 대칭.</summary>
        Neck = 2,

        /// <summary>어깨 — 망토. 진행 반대쪽으로 흘러내리는 <b>가장 비대칭</b>인 아이템.</summary>
        Shoulders = 3,
    }

    /// <summary>
    /// ★ 장비 착용 상태 — 2026-08-29 사용자 요청("캐릭터 장비 착용").
    ///
    /// ============================================================================
    /// 원안(docs/UX_FLOW.md 7절 "스킨/DLC 탭")을 왜 그대로 쓰지 않았는가 — <b>구매 → 레벨업 해제</b>
    /// ============================================================================
    /// 7절 와이어프레임은 잠긴 스킨을 "미리보기 → 구매 → 적용"으로 풀도록 그려져 있고, 8절 P3에도
    /// "DLC/스킨 탐색·구매(수익화 행동)"가 적혀 있다. 이번 라운드에서는 그 축을 <b>채택하지 않는다</b>:
    ///   · 결제 백엔드가 없다(스토어/영수증 검증/복원 어느 것도 이 프로젝트에 존재하지 않는다).
    ///   · 외부 아트 에셋이 없다 — 이 앱의 모든 시각 요소는 LineRenderer 프로시저럴 선화이므로
    ///     "미리보기 이미지 로드 실패"(7절 예외 상태) 같은 개념 자체가 성립하지 않는다.
    ///   · 결제 UI를 흉내만 내는 것은 사용자에게 거짓 약속이 된다.
    /// 그래서 해제 조건을 <b>레벨</b>로 치환했다. 관찰형 앱 철학("아무것도 안 해도 자란다",
    /// Core/CharacterProgressionModel.cs 참고)과도 맞는다 — 지갑이 아니라 함께 보낸 시간이 보상을 연다.
    /// docs/UX_FLOW.md는 설계 문서라 이 라운드에서 고치지 않았다(리더 지시).
    ///
    /// TodoListModel/StressGauge와 같은 이유로 정적 클래스이며, 저장/로드는 Core/CharacterSaveStore.cs가
    /// 전담한다.
    /// </summary>
    public static class EquipmentModel
    {
        public const int SlotCount = 4;

        /// <summary>config가 없는 경로(테스트 리그)에서 쓰는 해제 레벨. StickConfig의 기본값과 같아야 한다.</summary>
        private static readonly int[] FallbackUnlockLevels = { 2, 4, 6, 8 };

        private static readonly bool[] _equipped = new bool[SlotCount];

        /// <summary>이 슬롯의 아이템 이름(정보창 라벨 + 로그).</summary>
        public static string ItemName(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Head: return "모자";
                case EquipmentSlot.Eyes: return "선글라스";
                case EquipmentSlot.Neck: return "나비넥타이";
                case EquipmentSlot.Shoulders: return "망토";
                default: return "?";
            }
        }

        /// <summary>슬롯 이름(정보창 그리드의 부제).</summary>
        public static string SlotName(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Head: return "머리";
                case EquipmentSlot.Eyes: return "눈";
                case EquipmentSlot.Neck: return "목";
                case EquipmentSlot.Shoulders: return "어깨";
                default: return "?";
            }
        }

        /// <summary>이 슬롯이 열리는 레벨. config가 null이면 기본값(2/4/6/8).</summary>
        public static int UnlockLevel(EquipmentSlot slot, StickConfig config)
        {
            int i = (int)slot;
            if (i < 0 || i >= SlotCount) return int.MaxValue;
            if (config == null) return FallbackUnlockLevels[i];

            switch (slot)
            {
                case EquipmentSlot.Head: return Mathf.Max(1, config.equipmentUnlockLevelHead);
                case EquipmentSlot.Eyes: return Mathf.Max(1, config.equipmentUnlockLevelEyes);
                case EquipmentSlot.Neck: return Mathf.Max(1, config.equipmentUnlockLevelNeck);
                case EquipmentSlot.Shoulders: return Mathf.Max(1, config.equipmentUnlockLevelShoulders);
                default: return int.MaxValue;
            }
        }

        public static bool IsUnlocked(EquipmentSlot slot, StickConfig config)
            => CharacterProgressionModel.Level >= UnlockLevel(slot, config);

        public static bool IsEquipped(EquipmentSlot slot)
        {
            int i = (int)slot;
            return i >= 0 && i < SlotCount && _equipped[i];
        }

        /// <summary>지금 하나라도 착용 중인가 — 렌더러가 "그릴 것이 아무것도 없으면 통째로 쉰다"에 쓴다.</summary>
        public static bool AnyEquipped()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (_equipped[i]) return true;
            }
            return false;
        }

        /// <summary>
        /// 착용/해제 토글(정보창 [장비] 탭의 클릭). 잠긴 슬롯이면 아무 일도 하지 않고 false —
        /// "잠금 해제"를 여기서 대신 해주지 않는다(레벨만이 유일한 해제 경로라는 규칙을 코드로 강제).
        /// </summary>
        public static bool TryToggle(EquipmentSlot slot, StickConfig config)
        {
            int i = (int)slot;
            if (i < 0 || i >= SlotCount) return false;
            if (!IsUnlocked(slot, config)) return false;

            _equipped[i] = !_equipped[i];
            StickmanEventBus.RaiseCharacterEquipmentChanged();
            return true;
        }

        /// <summary>저장 파일 복원 전용(Core/CharacterSaveStore.cs). 잠금 여부를 검사하지 <b>않는다</b> —
        /// 검사하면 저장 시점보다 레벨이 낮게 복원되는 순간(파일 손상 등) 장비가 조용히 사라진다.
        /// 대신 렌더러/UI가 그릴 때 잠금 상태를 함께 본다.</summary>
        internal static void RestoreFromSave(EquipmentSlot slot, bool equipped)
        {
            int i = (int)slot;
            if (i < 0 || i >= SlotCount) return;
            _equipped[i] = equipped;
        }

        /// <summary>테스트/디버그 전용 완전 초기화(TodoListModel.ResetForTesting과 같은 이유).</summary>
        public static void ResetForTesting()
        {
            for (int i = 0; i < SlotCount; i++) _equipped[i] = false;
        }
    }
}
