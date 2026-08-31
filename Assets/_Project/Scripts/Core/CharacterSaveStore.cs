using System;
using System.Collections.Generic;
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
        /// 해석하지 않는 이유는 (0,0)이 실제로 도달 가능한 좌표라서다(별도 플래그가 필요한 이유).
        /// 4 = 2026-08-30 부채꼴 메뉴 라운드에서 <b>할일 목록</b>(Core/TodoListModel.cs)이 추가된 버전.
        /// 이 라운드에 [오늘 할일] 패널이 생기면서 사용자가 <b>자기 진짜 일정을 처음 적는 입구</b>가
        /// 됐다 — 앱을 끄면 조용히 사라지는 할일 목록은 기능 실패다(리더 결정). v1~v3 파일에는
        /// <c>todos</c>가 없어 JsonUtility가 null로 채우고, 그 null은 "적어둔 할일이 없다"는 정확한
        /// 사실이라 하위 호환이 앞선 버전들과 같은 방식으로 성립한다.
        /// 5 = 2026-08-30 캐릭터 정보창 재설계 라운드에서 착용 상태가 <b>bool 4개 → 카테고리 여러 개 ×
        /// 아이템 아이디</b>로 바뀐 버전(Core/EquipmentModel.cs). 여기서는 하위 호환이 앞선 버전들처럼
        /// "없으면 기본값"으로 저절로 성립하지 <b>않는다</b> — v1~v4의 <c>equippedHead</c> 4개는 새 필드와
        /// 자리가 다르기 때문이다. 그래서 이 버전만 <b>명시적 마이그레이션</b>을 한다(아래 Load의 분기):
        /// 기존 4카테고리는 "착용 중이었다 = 그 카테고리의 기본 아이템(0번)"으로 승격하고, 신규 3카테고리
        /// (머리/이펙트/펫)는 옛 파일에 존재한 적이 없으므로 전부 미착용에서 시작한다. 옛 사용자의
        /// 캐릭터 생김새가 업데이트만으로 달라지지 않는다는 것이 이 마이그레이션의 유일한 목표다
        /// (회귀 테스트: Tests/EditMode/EquipmentMigrationTests.cs).</summary>
        /// 6 = 2026-08-31 구석 호버 패널 라운드에서 <b>사용자가 고른 캐릭터 크기 + 구석 패널 on/off</b>
        /// (Core/UiLayoutModel.cs)가 추가된 버전. 하위 호환은 v3(톱니 위치) 때와 <b>같은 방식</b>으로
        /// 성립한다 — v1~v5 파일에는 <c>characterScaleSaved</c>가 없어 JsonUtility가 false로 채우고,
        /// 그 false는 "아직 크기를 고른 적 없다 = 배포 기본 배율을 쓴다"는 정확한 사실이다.
        /// <c>cornerPanelEnabled</c>만은 <b>기본이 true인 값</b>이라 없으면 false로 채워져 뜻이 뒤집힌다.
        /// 그래서 옛 파일에는 그 키를 읽지 않고 기본값(켜짐)을 그대로 쓴다(아래 Load의 분기).
        private const int CurrentVersion = 6;

        /// <summary>구석 호버 패널 설정이 처음 들어간 버전. 이 값보다 낮은 파일에는
        /// <c>cornerPanelEnabled</c> 키 자체가 없으므로 그 필드를 읽으면 안 된다(false = 꺼짐으로
        /// 오해된다 — "없으면 기본값"이 저절로 성립하지 <b>않는</b> 유일한 종류의 필드다).</summary>
        private const int FirstVersionWithCornerPanel = 6;

        /// <summary>착용 상태가 아이템 아이디로 바뀐 첫 버전. 이 값보다 낮은 파일은 bool 4개를 읽는다.</summary>
        private const int FirstVersionWithWornItemIds = 5;

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

            // ---- v1~v4: 카테고리당 아이템이 하나뿐이던 시절의 착용 여부 ----
            // v5부터는 아래 wornXxx가 진짜 상태다. 이 4개는 계속 <b>정확한 값으로</b> 기록한다 —
            // 파일 안에 서로 어긋나는 두 문장이 남으면 나중에 이 파일을 들여다볼 사람이 속는다.
            public bool equippedHead;
            public bool equippedEyes;
            public bool equippedNeck;
            public bool equippedShoulders;

            // ---- v2: 정보창 하단 스탯 블록의 기록(Core/CharacterStatsModel.cs) ----
            public int battleWins;
            // ※ v2에는 rivalWins가 있었다(라이벌 대결 승리). 라이벌 기능 전체 삭제(2026-08-30)로
            //   필드를 없앴다 — JsonUtility는 모르는 키를 조용히 무시하므로 옛 저장 파일도 그대로 읽힌다
            //   (Tests/EditMode/EquipmentMigrationTests의 v2 픽스처가 그 키를 계속 담고 있어 회귀를 잡는다).
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

            // ---- v4: 할일 목록(Core/TodoListModel.cs) ----

            /// <summary>미완료/유예 중인 활성 목록. v1~v3 파일에는 없어 null이 되고, null은 "없음"이다.</summary>
            public TodoRecord[] todos;

            /// <summary>완료함(17절 데이터 보존 원칙 — 지우지 않고 모아둔다).</summary>
            public TodoRecord[] todoArchive;

            // ---- v5: 카테고리별 "지금 걸친 아이템 아이디"(빈 문자열 = 미착용) ----
            //
            // 배열(string[8]) 대신 이름 붙은 필드 8개인 이유: 배열은 <b>enum 순서에 의존</b>한다.
            // 누군가 EquipmentSlot에 값을 끼워 넣는 순간 모든 사용자의 차림이 한 칸씩 밀리고, 그 사고는
            // 저장 파일을 열어봐도 눈에 띄지 않는다(그냥 "다른 아이템"이 적혀 있을 뿐이다).
            // 이름이 붙어 있으면 순서를 바꿔도, 나중에 카테고리를 지워도 파일이 스스로를 설명한다.
            //
            // 아이디를 적는 이유는 Core/EquipmentModel.cs의 "인덱스 vs 문자열 아이디" 문단 참고 —
            // 숫자를 적으면 표 중간에 아이템을 하나 끼워 넣는 날 전원의 착용물이 밀린다.
            // ---- v6: 구석 호버 패널(Core/UiLayoutModel.cs) ----

            /// <summary>사용자가 크기를 한 번이라도 정했는가. false면 아래 배율을 무시하고 배포 기본값을 쓴다.
            /// 톱니 위치의 gearPositionSaved와 정확히 같은 이유로 별도 플래그다(0.75는 실제로 도달
            /// 가능한 값이라 "값이 0.75면 설정 안 됨"으로 해석할 수 없다).</summary>
            public bool characterScaleSaved;
            public float characterScale;

            /// <summary>구석 호버 패널을 쓸 것인가. <b>기본이 true</b>라 v5 이하 파일에서는 읽으면 안 된다
            /// (위 <see cref="FirstVersionWithCornerPanel"/> 문서).</summary>
            public bool cornerPanelEnabled;

            public string wornHead;
            public string wornEyes;
            public string wornNeck;
            public string wornShoulders;
            // ★ 2026-08-30 표정(FACE) 삭제 — 여기 있던 wornFace 필드를 지웠다. 이미 저장된 v5 파일에는
            //   "wornFace" 키가 남아 있지만 JsonUtility는 <b>모르는 키를 조용히 버린다</b>. 즉 옛 파일도
            //   그대로 읽히고(다른 값은 전부 보존), 다음 저장에서 그 키만 사라진다. 버전을 6으로 올리지
            //   않은 이유는 스키마가 <b>줄어들기만</b> 했기 때문이다 — 새 필드를 못 읽는 구버전이 없다.
            public string wornHair;
            public string wornFx;
            public string wornPet;
        }

        /// <summary>
        /// 할일 1건의 직렬화 표현. <see cref="TodoItem"/>을 그대로 쓸 수 없는 이유: 그쪽 Id/Text가
        /// <c>readonly</c>이고 JsonUtility는 readonly 필드를 채우지 못한다. 완료 시각은 <b>일부러
        /// 저장하지 않는다</b> — <c>Time.unscaledTime</c> 기준이라 다음 실행에서는 의미가 없는 값이다.
        /// </summary>
        [Serializable]
        private sealed class TodoRecord
        {
            public int id;
            public string text;
            public bool completed;
        }

        private static TodoRecord[] ToRecords(IReadOnlyList<TodoItem> items)
        {
            var records = new TodoRecord[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                records[i] = new TodoRecord { id = items[i].Id, text = items[i].Text, completed = items[i].Completed };
            }
            return records;
        }

        private static TodoItem[] ToItems(TodoRecord[] records)
        {
            if (records == null) return null;
            var items = new TodoItem[records.Length];
            for (int i = 0; i < records.Length; i++)
            {
                TodoRecord r = records[i];
                if (r == null) continue;
                items[i] = new TodoItem(r.id, r.text) { Completed = r.completed };
            }
            return items;
        }

        // ============================================================================
        // ★ 테스트 격리 — 테스트는 개발자의 진짜 저장 파일을 절대 읽지도 쓰지도 않는다
        //    (2026-08-31, R3 Blocker 2 동반 조치)
        // ============================================================================
        // 무슨 일이 있었나: PlayMode 테스트는 Stickman 프리팹을 그대로 띄우고, 그 프리팹에는
        // CharacterProgressionDirector가 붙어 있어 Awake에서 Load()를 부른다. 그래서 스위트 전체가
        // **그 머신에서 앱을 실제로 가지고 논 사람의 저장 파일**을 읽고 있었다. 개발자 파일에
        // characterScale 0.35가 들어 있던 하루 동안 모든 PlayMode가 0.35배 캐릭터로 돌았고, 네 명이
        // 같은 실패를 보고도 원인을 프리팹으로 오인했다 — "내 변경을 되돌려도 그대로다"라는 네거티브
        // 컨트롤이 **참이지만 무의미**했기 때문이다(모두 같은 오염원을 읽었으므로).
        //
        // 고친 방식: 저장 경로의 **단일 조회 지점**(FilePath)에 테스트 전용 리디렉션을 둔다.
        //   · 억제(로드 건너뛰기)가 아니라 **경로 재지정**을 택한 이유: 지속성 테스트 6종은 실제
        //     디스크 왕복을 검증하는 것이 존재 이유다. 로드를 막으면 그 테스트들이 전부 죽는다.
        //     경로만 옮기면 그 테스트들은 한 글자도 안 고친 채 임시 폴더에서 그대로 돌고,
        //     프리팹이 자동으로 부르는 Load()는 **빈 폴더**를 만나 "새 캐릭터"로 출발한다.
        //   · 개발자의 실제 파일은 읽지도 쓰지도 않는다 — 지우거나 값을 바꾸지도 않는다(원칙 3).
        //     이 클래스에는 파일 삭제 API 자체가 없다(아래 MarkNotLoadedForTesting 문서 참고).
        //
        // 켜는 곳: 각 테스트 어셈블리의 전역 [SetUpFixture](Tests/*/GlobalTestIsolation.cs).
        // 프로덕션 실행에서는 이 값이 null이라 예전과 정확히 같은 경로를 쓴다.
        private static string s_testingDirectoryOverride;

        /// <summary>저장 파일이 놓이는 디렉터리. 테스트 리디렉션이 걸려 있으면 그쪽을 쓴다.</summary>
        private static string SaveDirectory => s_testingDirectoryOverride ?? Application.persistentDataPath;

        /// <summary>테스트가 개발자의 실제 저장 파일 대신 임시 폴더를 쓰고 있는가. 진단/단언용.</summary>
        public static bool IsRedirectedForTesting => s_testingDirectoryOverride != null;

        /// <summary>
        /// 테스트 전용 — 저장 경로를 이 실행에만 쓰이는 임시 폴더로 옮기고 그 경로를 돌려준다.
        /// <see cref="Application.temporaryCachePath"/> 아래에만 만든다(OS가 이 앱에 배정한 자리).
        /// <see cref="ResetForTesting"/>로 되돌린다. 프로덕션 코드에서는 절대 부르지 않는다.
        /// </summary>
        public static string RedirectToTemporaryDirectoryForTesting(string label)
        {
            string dir = Path.Combine(Application.temporaryCachePath, "StickMateTestSaves",
                string.IsNullOrEmpty(label) ? "default" : label);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            s_testingDirectoryOverride = dir;
            return dir;
        }

        /// <summary>테스트 전용 완전 초기화 — 경로 리디렉션과 진단 플래그를 원래대로 돌린다
        /// (CharacterProgressionModel.ResetForTesting / EquipmentModel.ResetForTesting과 같은 관례).
        /// 임시 폴더는 지우지 않는다(이 앱의 프로덕션 코드에는 파일 삭제 능력이 없다는 불변식 유지).</summary>
        public static void ResetForTesting()
        {
            s_testingDirectoryOverride = null;
            LoadedFromFile = false;
            NewerVersionFileDetected = false;
            NewerVersionBackupPath = null;
            SaveSuspended = false;
        }

        /// <summary>저장 파일의 절대 경로. 진단 로그/테스트에서만 쓴다.</summary>
        public static string FilePath => Path.Combine(SaveDirectory, FileName);

        /// <summary>마지막 로드가 실제 파일에서 값을 읽었는가(false면 파일이 없어 기본값으로 시작).
        /// 진단 로그 전용.</summary>
        public static bool LoadedFromFile { get; private set; }

        // ============================================================================
        // ★ 다운그레이드 방어 (2026-08-30 횡단 리뷰 m6 — 데이터 소실 경로)
        // ============================================================================
        // 발견된 사실: `data.version > CurrentVersion`(= 사용자가 신버전으로 놀다가 구버전 앱을 실행)
        // 분기가 **조용히 return**하고 있었다. 그러면 모델은 기본값(Lv.1 / 빈 할일)으로 시작하고,
        // 다음 자동 저장이 그 기본값을 신버전 파일 **위에 덮어써** 사용자의 성장/할일이 통째로 사라진다.
        // 테스트도 0건이었다. 조용한 전손은 이 앱에서 가장 나쁜 실패다(할일 목록은 사용자의 진짜 일정이다).
        //
        // 방어(둘 다 한다):
        //   (1) 원본을 **백업 사본**으로 남긴다. 원본을 지우거나 옮기지 않고 복사만 한다 — 파일 삭제/이동
        //       API는 절대 불변 원칙 3 정적 감사가 금지한다(Tests/EditMode/UserAssetImmutabilityAuditTests).
        //       백업이 이미 있으면 **덮어쓰지 않는다**(가장 처음 백업이 가장 값지다).
        //   (2) 백업에 실패했으면 이번 실행에서는 **저장을 보류**한다(Save()가 false를 돌려준다).
        //       "구버전 앱에서 놀던 것을 못 저장한다"는 불편은 되돌릴 수 있지만, 덮어쓴 데이터는 못 되돌린다.
        //
        // 백업에 성공했다면 저장은 정상 진행한다 — 원본이 안전하므로 구버전 앱도 평소처럼 쓸 수 있고,
        // 사용자는 신버전으로 돌아갈 때 백업 파일을 되돌려 놓으면 된다(경로를 경고 로그에 남긴다).

        /// <summary>이번 실행이 <b>자기보다 새로운 버전</b>의 저장 파일을 만났는가. 진단/테스트용.</summary>
        public static bool NewerVersionFileDetected { get; private set; }

        /// <summary>다운그레이드로 판단해 남긴 백업 사본의 경로(없으면 null). 진단/테스트용.</summary>
        public static string NewerVersionBackupPath { get; private set; }

        /// <summary>백업까지 실패해 이번 실행의 저장을 보류하는가. 진단/테스트용.</summary>
        public static bool SaveSuspended { get; private set; }

        /// <summary>신버전 파일 백업의 파일명. 버전 번호를 넣어 여러 신버전을 만나도 서로 덮지 않는다.</summary>
        private static string BackupFileName(int version) => $"character_save.v{version}.backup.json";

        /// <summary>
        /// 앱 시작 시 1회. 파일이 없거나 깨졌으면 <b>아무것도 하지 않는다</b> — 정적 모델의 초기값
        /// (Lv.1 / XP 0 / 기본 이름 / 전부 미착용)이 그대로 "새 캐릭터"가 된다.
        /// </summary>
        public static void Load()
        {
            LoadedFromFile = false;
            NewerVersionFileDetected = false;
            NewerVersionBackupPath = null;
            SaveSuspended = false;
            try
            {
                string path = FilePath;
                if (!File.Exists(path)) return;

                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return;

                var data = JsonUtility.FromJson<SaveData>(json);
                if (data == null || data.version <= 0) return;

                // ★ 다운그레이드 — 위 "다운그레이드 방어" 문단 참고. 스키마를 모르므로 읽지는 않되,
                // 원본이 다음 저장에 덮여 사라지는 것만은 막는다.
                if (data.version > CurrentVersion)
                {
                    HandleNewerVersionFile(path, data.version);
                    return;
                }

                CharacterProgressionModel.RestoreFromSave(data.level, data.currentXp, data.totalXpEarned, data.characterName);
                RestoreEquipment(data);
                CharacterStatsModel.RestoreFromSave(data.battleWins,
                    data.archeryShots, data.archeryBullseyes, data.companionSeconds,
                    data.ragdollFalls, data.firstRunUnixSeconds);
                UiLayoutModel.RestoreFromSave(data.gearPositionSaved, data.gearCenterXPoints, data.gearCenterYPoints);
                // v5 이하에는 cornerPanelEnabled 키가 없다 — 읽으면 false(꺼짐)로 오해되므로 기본값(켜짐)을 쓴다.
                UiLayoutModel.RestoreCornerPanelFromSave(data.characterScaleSaved, data.characterScale,
                    data.version >= FirstVersionWithCornerPanel ? data.cornerPanelEnabled : true);
                TodoListModel.RestoreFromSave(ToItems(data.todos), ToItems(data.todoArchive));
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

        /// <summary>
        /// 착용 상태 복원 — <b>v5 마이그레이션이 사는 유일한 자리</b>(위 <c>CurrentVersion</c> 문서 참고).
        ///
        /// 두 경로 모두 <b>카테고리를 빠짐없이</b> 지정한다. "옛 파일에 없는 카테고리는 건드리지 않는다"로
        /// 두면 그 자리에 <b>직전 상태</b>(새 캐릭터 기본 차림 또는 앞선 로드의 잔재)가 남아, 파일이
        /// 말하지 않은 것을 화면이 보여주게 된다.
        /// </summary>
        private static void RestoreEquipment(SaveData data)
        {
            if (data.version >= FirstVersionWithWornItemIds)
            {
                EquipmentModel.RestoreFromSave(EquipmentSlot.Head, data.wornHead);
                EquipmentModel.RestoreFromSave(EquipmentSlot.Eyes, data.wornEyes);
                EquipmentModel.RestoreFromSave(EquipmentSlot.Neck, data.wornNeck);
                EquipmentModel.RestoreFromSave(EquipmentSlot.Shoulders, data.wornShoulders);
                EquipmentModel.RestoreFromSave(EquipmentSlot.Hair, data.wornHair);
                EquipmentModel.RestoreFromSave(EquipmentSlot.Fx, data.wornFx);
                EquipmentModel.RestoreFromSave(EquipmentSlot.Pet, data.wornPet);
                return;
            }

            // v1~v4 — 카테고리당 하나뿐이던 아이템은 그 카테고리의 기본 아이템(0번)이 됐다.
            EquipmentModel.RestoreFromSave(EquipmentSlot.Head, data.equippedHead);
            EquipmentModel.RestoreFromSave(EquipmentSlot.Eyes, data.equippedEyes);
            EquipmentModel.RestoreFromSave(EquipmentSlot.Neck, data.equippedNeck);
            EquipmentModel.RestoreFromSave(EquipmentSlot.Shoulders, data.equippedShoulders);

            // 신규 3카테고리는 옛 파일에 존재한 적이 없다 → 미착용. 머리를 여기서 "기본값이니까"
            // 하고 걸쳐 주면, 업데이트만 했는데 캐릭터 얼굴이 달라진다.
            EquipmentModel.RestoreFromSave(EquipmentSlot.Hair, false);
            EquipmentModel.RestoreFromSave(EquipmentSlot.Fx, false);
            EquipmentModel.RestoreFromSave(EquipmentSlot.Pet, false);
        }

        /// <summary>
        /// 자기보다 새로운 버전의 저장 파일을 만났을 때의 처리(위 "다운그레이드 방어" 문단이 유일한 근거).
        /// 원본은 **읽기만** 하고 복사본을 하나 더 만든다 — 지우지도, 옮기지도, 고치지도 않는다.
        /// </summary>
        private static void HandleNewerVersionFile(string path, int fileVersion)
        {
            NewerVersionFileDetected = true;

            string backupPath = Path.Combine(SaveDirectory, BackupFileName(fileVersion));
            try
            {
                // 이미 백업이 있으면 그대로 둔다(첫 백업이 가장 값지다 — 두 번째 실행이 덮으면
                // 그 사이 구버전이 만든 내용으로 백업이 오염될 수 있다).
                if (!File.Exists(backupPath)) File.Copy(path, backupPath);
                NewerVersionBackupPath = backupPath;
                SaveSuspended = false;

                Debug.LogWarning($"[성장] 저장 파일이 이 앱보다 새로운 버전입니다(파일 v{fileVersion} > 앱 v{CurrentVersion}). " +
                    $"내용을 해석할 수 없어 기본값으로 시작하지만, 원본을 백업해 두었으므로 데이터는 " +
                    $"사라지지 않습니다: {backupPath}\n" +
                    "최신 버전 앱으로 돌아가려면 이 백업 파일의 이름을 원래 저장 파일명으로 되돌리세요.");
            }
            catch (Exception e)
            {
                // 백업조차 실패 — 이번 실행에서는 저장을 보류해 원본을 지킨다.
                NewerVersionBackupPath = null;
                SaveSuspended = true;
                Debug.LogWarning($"[성장] 저장 파일이 이 앱보다 새로운 버전인데({fileVersion} > {CurrentVersion}) " +
                    $"백업에 실패했습니다({e.GetType().Name}: {e.Message}). " +
                    "원본을 덮어쓰지 않도록 이번 실행에서는 **저장을 보류**합니다 — " +
                    "이 실행에서 얻은 성장/할일은 저장되지 않습니다.");
            }
        }

        /// <summary>미착용을 <c>null</c>이 아니라 빈 문자열로 적는다 — JsonUtility는 null 문자열을
        /// <c>""</c>로 직렬화하므로, 읽는 쪽이 둘을 구분하려 들면 없는 차이를 다루게 된다.</summary>
        private static string WornId(EquipmentSlot slot) => EquipmentModel.WornItemId(slot) ?? string.Empty;

        /// <summary>성공하면 true. 실패해도 예외를 밖으로 던지지 않는다(클래스 문서 참고).</summary>
        public static bool Save()
        {
            // ★ 다운그레이드 보류(m6) — 신버전 파일을 백업조차 못 한 상태에서는 절대 덮어쓰지 않는다.
            if (SaveSuspended) return false;

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
                    archeryShots = CharacterStatsModel.ArcheryShots,
                    archeryBullseyes = CharacterStatsModel.ArcheryBullseyes,
                    companionSeconds = CharacterStatsModel.TotalCompanionSeconds,
                    ragdollFalls = CharacterStatsModel.RagdollFalls,
                    firstRunUnixSeconds = CharacterStatsModel.FirstRunUnixSeconds,
                    gearPositionSaved = UiLayoutModel.HasGearCenter,
                    gearCenterXPoints = UiLayoutModel.GearCenterPoints.x,
                    gearCenterYPoints = UiLayoutModel.GearCenterPoints.y,
                    characterScaleSaved = UiLayoutModel.HasCharacterScale,
                    characterScale = UiLayoutModel.CharacterScale,
                    cornerPanelEnabled = UiLayoutModel.CornerPanelEnabled,
                    todos = ToRecords(TodoListModel.ActiveItems),
                    todoArchive = ToRecords(TodoListModel.CompletedArchive),
                    wornHead = WornId(EquipmentSlot.Head),
                    wornEyes = WornId(EquipmentSlot.Eyes),
                    wornNeck = WornId(EquipmentSlot.Neck),
                    wornShoulders = WornId(EquipmentSlot.Shoulders),
                    wornHair = WornId(EquipmentSlot.Hair),
                    wornFx = WornId(EquipmentSlot.Fx),
                    wornPet = WornId(EquipmentSlot.Pet),
                };

                string dir = SaveDirectory;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
                CharacterProgressionModel.MarkSaved();
                CharacterStatsModel.MarkSaved();
                UiLayoutModel.MarkSaved();
                TodoListModel.MarkSaved();
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
