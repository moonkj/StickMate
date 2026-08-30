using System;
using System.IO;
using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// ★ 성장/장비 상태의 영속화 — <c>Application.persistentDataPath</c>에 JSON 한 파일.
    ///
    /// ============================================================================
    /// 불변 원칙 3(유저 자산 불변)과의 관계 — <b>충돌하지 않는다</b>
    /// ============================================================================
    /// CLAUDE.md 원칙 3이 금지하는 것은 "유저의 실제 파일/아이콘/타 윈도우"를 이동·삭제·수정하는
    /// 행위다. 여기서 쓰는 <c>Application.persistentDataPath</c>는 OS가 <b>이 앱에게 배정한 자기 자신의
    /// 데이터 디렉터리</b>(macOS: ~/Library/Application Support/&lt;company&gt;/&lt;product&gt;)이고, 그 안에
    /// 이 앱이 만든 파일만 읽고 쓴다. 유저의 문서/바탕화면/다른 앱의 파일은 열거조차 하지 않는다.
    /// 이 클래스는 경로를 직접 조립하지 않고 항상 persistentDataPath 아래에 고정 파일명 하나만 쓴다 —
    /// 상대 경로 조작으로 바깥으로 나갈 여지 자체를 없앤다.
    ///
    /// ============================================================================
    /// 실패는 조용히 삼킨다 (의도적)
    /// ============================================================================
    /// 저장/로드 실패(디스크 가득참, 권한, 파일 손상)는 <b>치명적 오류로 만들지 않는다</b>. 이 앱은
    /// 하루 종일 켜져 있는 관찰형 데스크톱 펫이고, 레벨이 몇인지는 앱이 계속 돌아가는 것보다 덜
    /// 중요하다. 실패하면 경고 로그 한 줄만 남기고 메모리 값 그대로 계속 진행한다(다음 주기 저장이
    /// 다시 시도한다).
    ///
    /// 저장 시점: 레벨업 / 장비 변경 / 이름 변경 직후 + <c>progressionAutoSaveIntervalSeconds</c>
    /// 주기(기본 60초, 값이 바뀌었을 때만) + 종료 시(OnApplicationQuit). 전부
    /// Interaction/CharacterProgressionDirector.cs가 호출한다.
    /// </summary>
    public static class CharacterSaveStore
    {
        private const string FileName = "stickmate_character.json";

        /// <summary>스키마 버전. 2 = 2026-08-30 정보창 리디자인 라운드에서 기록 7종
        /// (격파/대결/활쏘기 2종/누적 시간/넘어진 횟수/첫 만남 시각)이 추가된 버전. <b>버전 1 파일도 그대로 읽힌다</b> —
        /// 새 필드는 JsonUtility가 0으로 채우고, 그 0은 "아직 기록이 없다"는 정확한 사실이다.
        /// 3 = 2026-08-30 톱니 아이콘 길게 눌러 옮기기 라운드에서 <b>사용자가 옮긴 톱니 위치</b>
        /// (Core/UiLayoutModel.cs)가 추가된 버전. 여기서도 하위 호환은 같은 방식으로 성립한다 —
        /// v1/v2 파일에는 <c>gearPositionSaved</c>가 없어 JsonUtility가 false로 채우고, 그 false는
        /// "아직 옮긴 적 없다 = 기본 위치(우상단)를 쓴다"는 정확한 사실이다. 좌표 0,0을 "값 없음"으로
        /// 해석하지 않는 이유는 (0,0)이 실제로 도달 가능한 좌표라서다(별도 플래그가 필요한 이유).</summary>
        private const int CurrentVersion = 3;

        /// <summary>
        /// 직렬화 스키마. JsonUtility는 프로퍼티를 직렬화하지 않으므로 public 필드로만 구성한다.
        /// <c>version</c>을 맨 앞에 둔 이유: 훗날 스키마가 바뀌어도 옛 파일을 읽어 마이그레이션할 수
        /// 있어야 하고, 알 수 없는 버전이면 "기본값으로 시작"이라는 안전한 쪽으로 떨어뜨리기 위해서다.
        /// </summary>
        [Serializable]
        private sealed class SaveData
        {
            public int version;
            public int level;
            public float currentXp;
            public float totalXpEarned;
            public string characterName;
            public bool equippedHead;
            public bool equippedEyes;
            public bool equippedNeck;
            public bool equippedShoulders;

            // ---- v2: 정보창 하단 스탯 블록의 기록(Core/CharacterStatsModel.cs) ----
            public int battleWins;
            public int rivalWins;
            public int archeryShots;
            public int archeryBullseyes;
            public float companionSeconds;
            public int ragdollFalls;

            /// <summary>"근속"의 기준점(Unix 초, UTC). 0이면 아직 기록이 없다는 뜻이고, 로드 직후
            /// CharacterStatsModel.EnsureFirstRunInitialized()가 지금 시각으로 채운다.</summary>
            public long firstRunUnixSeconds;

            // ---- v3: 사용자가 옮긴 화면 UI 위치(Core/UiLayoutModel.cs) ----

            /// <summary>사용자가 톱니를 한 번이라도 옮겼는가. false면 아래 좌표는 무시하고 기본 위치를 쓴다.</summary>
            public bool gearPositionSaved;

            /// <summary>큰 기어 중심(창 좌상단 원점, OS 포인트). 단위 근거는 UiLayoutModel 문서 참고.</summary>
            public float gearCenterXPoints;
            public float gearCenterYPoints;
        }

        /// <summary>저장 파일의 절대 경로. 진단 로그/테스트에서만 쓴다.</summary>
        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>마지막 로드가 실제 파일에서 값을 읽었는가(false면 파일이 없어 기본값으로 시작).
        /// 진단 로그 전용.</summary>
        public static bool LoadedFromFile { get; private set; }

        /// <summary>
        /// 앱 시작 시 1회. 파일이 없거나 깨졌으면 <b>아무것도 하지 않는다</b> — 정적 모델의 초기값
        /// (Lv.1 / XP 0 / 기본 이름 / 전부 미착용)이 그대로 "새 캐릭터"가 된다.
        /// </summary>
        public static void Load()
        {
            LoadedFromFile = false;
            try
            {
                string path = FilePath;
                if (!File.Exists(path)) return;

                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return;

                var data = JsonUtility.FromJson<SaveData>(json);
                if (data == null || data.version <= 0 || data.version > CurrentVersion) return;

                CharacterProgressionModel.RestoreFromSave(data.level, data.currentXp, data.totalXpEarned, data.characterName);
                EquipmentModel.RestoreFromSave(EquipmentSlot.Head, data.equippedHead);
                EquipmentModel.RestoreFromSave(EquipmentSlot.Eyes, data.equippedEyes);
                EquipmentModel.RestoreFromSave(EquipmentSlot.Neck, data.equippedNeck);
                EquipmentModel.RestoreFromSave(EquipmentSlot.Shoulders, data.equippedShoulders);
                CharacterStatsModel.RestoreFromSave(data.battleWins, data.rivalWins,
                    data.archeryShots, data.archeryBullseyes, data.companionSeconds,
                    data.ragdollFalls, data.firstRunUnixSeconds);
                UiLayoutModel.RestoreFromSave(data.gearPositionSaved, data.gearCenterXPoints, data.gearCenterYPoints);
                LoadedFromFile = true;

                // 복원이 끝난 뒤 한 번만 통지한다(중간 상태를 UI가 그리지 않게 — RestoreFromSave가
                // 각자 이벤트를 쏘지 않는 이유).
                StickmanEventBus.RaiseCharacterProgressionChanged();
                StickmanEventBus.RaiseCharacterEquipmentChanged();
            }
            catch (Exception e)
            {
                // 손상된 파일을 지우지 않는다 — 사용자가 나중에 들여다볼 수 있게 남겨두고,
                // 다음 저장이 정상 내용으로 덮어쓴다.
                Debug.LogWarning($"[성장] 저장 파일을 읽지 못했습니다({e.GetType().Name}: {e.Message}). " +
                    "기본값(Lv.1)으로 시작합니다 — 다음 저장이 정상 내용으로 덮어씁니다.");
            }
        }

        /// <summary>성공하면 true. 실패해도 예외를 밖으로 던지지 않는다(클래스 문서 참고).</summary>
        public static bool Save()
        {
            try
            {
                var data = new SaveData
                {
                    version = CurrentVersion,
                    level = CharacterProgressionModel.Level,
                    currentXp = CharacterProgressionModel.CurrentXp,
                    totalXpEarned = CharacterProgressionModel.TotalXpEarned,
                    characterName = CharacterProgressionModel.CharacterName,
                    equippedHead = EquipmentModel.IsEquipped(EquipmentSlot.Head),
                    equippedEyes = EquipmentModel.IsEquipped(EquipmentSlot.Eyes),
                    equippedNeck = EquipmentModel.IsEquipped(EquipmentSlot.Neck),
                    equippedShoulders = EquipmentModel.IsEquipped(EquipmentSlot.Shoulders),
                    battleWins = CharacterStatsModel.BattleWins,
                    rivalWins = CharacterStatsModel.RivalWins,
                    archeryShots = CharacterStatsModel.ArcheryShots,
                    archeryBullseyes = CharacterStatsModel.ArcheryBullseyes,
                    companionSeconds = CharacterStatsModel.TotalCompanionSeconds,
                    ragdollFalls = CharacterStatsModel.RagdollFalls,
                    firstRunUnixSeconds = CharacterStatsModel.FirstRunUnixSeconds,
                    gearPositionSaved = UiLayoutModel.HasGearCenter,
                    gearCenterXPoints = UiLayoutModel.GearCenterPoints.x,
                    gearCenterYPoints = UiLayoutModel.GearCenterPoints.y,
                };

                string dir = Application.persistentDataPath;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
                CharacterProgressionModel.MarkSaved();
                CharacterStatsModel.MarkSaved();
                UiLayoutModel.MarkSaved();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[성장] 저장에 실패했습니다({e.GetType().Name}: {e.Message}) — " +
                    "메모리 값 그대로 계속 진행합니다(다음 주기에 다시 시도).");
                return false;
            }
        }

        /// <summary>
        /// ★ 이 클래스에는 <b>파일을 지우는 코드가 존재하지 않는다</b>(의도적).
        ///
        /// 처음에는 "테스트 전용 저장 파일 삭제" 헬퍼를 여기 두었는데, 원칙 3(유저 자산 불변) 정적 감사
        /// (Tests/EditMode/UserAssetImmutabilityAuditTests.cs)가 프로덕션 소스에서 파일 삭제 API를
        /// 예외 없이 금지하고 있어 그 자리에서 빨개졌다(그 감사는 주석까지 포함한 <b>텍스트 스캔</b>이라
        /// 이 문단조차 그 API 이름을 그대로 적을 수 없다 — 일부러 풀어 썼다). 화이트리스트를 늘리는 대신 <b>헬퍼 자체를
        /// 없애는</b> 쪽을 택했다 — "이 앱의 프로덕션 코드에는 파일을 지우는 능력이 아예 없다"가
        /// 그 감사가 지키려는 바로 그 불변식이고, 화이트리스트를 한 번 열면 다음 사람이 그 틈으로
        /// 진짜 위반을 들여올 수 있기 때문이다.
        ///
        /// "파일이 없을 때 기본값으로 시작하는가"는 여전히 검증한다 — 테스트가 자기 손으로 파일을
        /// 지우고 <see cref="Load"/>를 부른다(Tests/ 폴더는 그 감사의 스캔 대상에서 제외되어 있고,
        /// 대상은 언제나 <see cref="FilePath"/> 하나뿐이다).
        /// </summary>
        internal static void MarkNotLoadedForTesting() => LoadedFromFile = false;
    }
}
