using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
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
        /// (격파/대결/활쏘기 2종/누적 시간/넘어진 횟수/첫 만남 시각)이 추가된 버전
        /// (격파·대결은 그 뒤 기능이 삭제됐지만 <b>스키마는 그대로다</b> — v2의 사실 기술이다). <b>버전 1 파일도 그대로 읽힌다</b> —
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
        /// 7 = 2026-08-31 잉크색 오염 수정 라운드에서 <b>사용자가 고른 잉크색</b>
        /// (Core/CharacterAppearanceModel.cs)이 추가된 버전. 하위 호환은 v3(톱니 위치)/v6(캐릭터 크기)와
        /// <b>같은 방식</b>으로 성립한다 — v1~v6 파일에는 <c>inkColorSaved</c>가 없어 JsonUtility가
        /// false로 채우고, 그 false는 "아직 색을 고른 적 없다 = 배포 기본 잉크색을 쓴다"는 정확한 사실이다.
        /// 값 자체는 숫자가 아니라 <b>이름 문자열</b>("Black"/"White")로 적는다 — 근거는
        /// CharacterAppearanceModel.RestoreFromSave 문서(열거형 순서가 바뀌어도 파일이 안 밀린다).
        /// 8 = 2026-09-01 설정창 라운드에서 <b>설정창이 만지는 값</b>(Core/AppSettingsModel.cs)이 추가된
        /// 버전. 말풍선 4종은 v6의 <c>characterScaleSaved</c>와 같은 "고른 적 있는가 + 값" 두 벌이라
        /// 하위 호환이 저절로 성립하지만, <c>autoHideOnFullscreen</c>/<c>gearIconVisible</c>는
        /// <b>기본이 true인 값</b>이라 v7 이하 파일에서 읽으면 뜻이 뒤집힌다(구석 패널이 겪은 그 함정) —
        /// 아래 Load가 버전을 보고 기본값(켜짐)을 넘긴다.
        ///
        /// <para>★ <b>internal인 이유</b>(2026-09-01): 지속성 테스트가 "저장하면 최신 버전으로 올라간다"를
        /// 확인할 때 기대값을 <c>"version": 7</c>처럼 <b>숫자로 베껴 적고</b> 있었다. 그러면 스키마가
        /// 올라갈 때마다 마이그레이션과 무관한 테스트가 함께 빨개지고(v8 라운드에서 실제로 2건 터졌다),
        /// 고치는 사람은 "숫자만 맞추면 되는 잡음"으로 학습해 <b>진짜 데이터 손실</b>을 같은 손놀림으로
        /// 넘길 위험이 생긴다. 그래서 이 상수를 테스트 어셈블리에 열어 두고, 테스트는 숫자가 아니라
        /// 이 상수를 참조한다(InternalsVisibleTo: Scripts/AssemblyInfo.cs).</para>
        ///
        /// <para>버전을 올릴 때 실제로 잠가야 하는 것은 "숫자가 바뀌었는가"가 아니라 <b>새 필드가 없는
        /// 옛 파일이 여전히 옳게 읽히는가</b>다. 그 잠금은 버전마다 하나씩 있는 하위 호환 테스트가 맡는다
        /// (v5→구석 패널, v6→잉크색, v7→설정창 2종 — Tests/EditMode/EquipmentMigrationTests.cs).</para>
        /// 9 = 2026-09-02 <b>대사 표시 시간</b> 컨트롤 재설계(docs/UX_FLOW.md 42절). 초 슬라이더
        /// (1.5~6.0초)가 폐기되고 3단 세그먼트(<c>DialogueVisibleLength</c>)로 바뀌면서 저장 필드도
        /// <c>dialogueVisibleSeconds</c>(float) → <c>dialogueVisibleLengthName</c>(문자열)로 교체됐다.
        /// <para>★ <b>옛 값은 의도적으로 버린다</b> — 마이그레이션 매핑은 "저장된 값 전부 → 기본(100%)"이다.
        /// 근거: 그 슬라이더는 2.5초를 넘는 구간에서 <b>화면을 한 톨도 바꾸지 못했고</b>(35줄 전수 실측
        /// 0/35), 그래서 옛 값의 대부분은 "사용자가 고른 뜻"이 아니라 <b>아무 일도 일어나지 않던
        /// 숫자</b>다. 그 숫자를 억지로 배율로 환산하면 <b>겪어본 적 없는 화면</b>을 사용자에게
        /// 새로 만들어 주게 된다. 1.5~2.5초를 고른 소수에게도 새 기본값과의 차이는 최대 1.16초다.</para>
        /// <para>하위 호환은 <c>dialogueVisibleLengthSaved</c>가 v8 이하 파일에서 false로 채워지는
        /// 것으로 저절로 성립한다(v6 <c>characterScaleSaved</c>와 같은 구조). 검증은
        /// Tests/EditMode/EquipmentMigrationTests.cs의 v8 하위 호환 테스트가 한다.</para>
        internal const int CurrentVersion = 9;

        /// <summary>설정창 값이 처음 들어간 버전. 이 값보다 낮은 파일에는 <c>autoHideOnFullscreen</c>/
        /// <c>gearIconVisible</c> 키가 없으므로 읽으면 안 된다(false = 꺼짐으로 오해된다 —
        /// <see cref="FirstVersionWithCornerPanel"/>과 완전히 같은 종류의 필드다).</summary>
        private const int FirstVersionWithAppSettings = 8;

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
            // ★ battleWins는 2026-09-02 격파 놀이 삭제 뒤에도 <b>남긴다</b>(리더 판정). 아래 rivalWins와
            //   정반대 처리인데, 이유가 다르다: rivalWins는 값이 실제로 <b>없던</b> 필드였고, battleWins는
            //   사용자 세이브(v9)에 3회가 실제로 들어 있다. 필드를 빼면 CurrentVersion을 올려야 하고
            //   그 라운드는 v8 하위 호환 테스트를 의무로 달아야 한다(CLAUDE.md) — 죽은 필드 하나를
            //   지우려고 마이그레이션 위험을 사지 않는다. 읽고 그대로 다시 쓴다. 화면에는 안 나온다.
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

            // ---- v7: 사용자가 고른 잉크색(Core/CharacterAppearanceModel.cs) ----

            /// <summary>사용자가 잉크색을 한 번이라도 골랐는가. false면 아래 이름을 무시하고 배포
            /// 기본값을 쓴다(Black도 실제로 고를 수 있는 값이라 별도 플래그가 필요하다 —
            /// characterScaleSaved / gearPositionSaved와 같은 이유).</summary>
            public bool inkColorSaved;

            /// <summary>고른 색의 <b>이름</b>("Black"/"White"). 숫자가 아닌 이유는
            /// CharacterAppearanceModel.RestoreFromSave 문서 참고.</summary>
            public string inkColorName;

            // ---- v8: 설정창(Core/AppSettingsModel.cs) ----

            /// <summary>전체화면 자동 숨김 / 톱니 아이콘. <b>둘 다 기본이 true</b>라 v7 이하 파일에서는
            /// 읽으면 안 된다(위 <see cref="FirstVersionWithAppSettings"/> 문서).</summary>
            public bool autoHideOnFullscreen;
            public bool gearIconVisible;

            /// <summary>말풍선 설정 4종. 전부 "고른 적 있는가 + 값" 두 벌이라 옛 파일에서 false로 채워지는
            /// 것이 곧 "배포 기본값을 쓴다"는 정확한 사실이다(characterScaleSaved와 같은 구조).</summary>
            public bool dialogueFontSizeSaved;
            public int dialogueFontSize;
            /// <summary>★ v9 — 3단 세그먼트. 값은 숫자가 아니라 <b>이름 문자열</b>("Default"/"Long"/
            /// "VeryLong")이다: 열거형에 칸이 끼어들어도 파일이 밀리지 않는다(<c>inkColorName</c>과
            /// 같은 관례). v8 이하 파일에는 <c>dialogueVisibleSeconds</c>(float)가 있었지만 <b>읽지
            /// 않는다</b> — JsonUtility는 모르는 키를 조용히 버린다(wornFace가 겪은 그 경로).</summary>
            public bool dialogueVisibleLengthSaved;
            public string dialogueVisibleLengthName;
            public bool chatterPercentSaved;
            public int chatterPercent;
            public bool dialogueBubbleEnabledSaved;
            public bool dialogueBubbleEnabled;

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
            LoadedFromPreviousGeneration = false;
            NewerVersionFileDetected = false;
            NewerVersionBackupPath = null;
            SaveSuspended = false;
            LastSaveWasAtomic = false;
            LastSaveKeptPreviousGeneration = false;
            ConsecutiveAtomicCommitFailures = 0;
            s_forcedAtomicCommitFailures = 0;
            s_deathAfterBytesForTesting = -1;
        }

        /// <summary>저장 파일의 절대 경로. 진단 로그/테스트에서만 쓴다.</summary>
        public static string FilePath => Path.Combine(SaveDirectory, FileName);

        // ============================================================================
        // ★ 읽기는 남의 교체를 막지 않는 공유 모드로 연다 (2026-09-02)
        // ============================================================================
        // File.ReadAllText는 대상을 FileShare.Read로 연다 = "내가 읽는 동안 아무도 쓰거나 지우거나
        // 이름을 바꿀 수 없다". Windows에서 File.Replace의 첫 단계는 대상 파일을 <b>치우는 것</b>
        // (rename 또는 delete)이라, 그 핸들이 하나라도 살아 있으면 커널이 ERROR_UNABLE_TO_REMOVE_REPLACED
        // (1175, "바꿀 파일을 제거할 수 없습니다")로 거절한다. 그런데 이 저장 파일은 <b>설계상 여러
        // 인스턴스가 공유한다</b>(.claude/skills/run-stickmate/SKILL.md) — 즉 우리가 저장 직전에 하는
        // 버전 재확인 읽기와 남의 기동 시 Load()가, 서로의 원자적 교체를 막는 구조였다.
        // FileShare.Delete를 얹으면 "읽는 동안 치워도 된다"가 되어 그 자충수가 구조적으로 사라진다.
        // (POSIX에는 이 개념 자체가 없어 macOS/Linux에서는 무해한 no-op이다.)
        private static string ReadAllTextShared(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                       FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, new UTF8Encoding(false), true))
            {
                return reader.ReadToEnd();
            }
        }

        // ============================================================================
        // ★ 직전 세대 (2026-09-02 — "원자성이 실패하는 환경에서도 직전 저장으로 되돌아간다")
        // ============================================================================
        // 원자적 교체가 성공하는 한 손상은 일어나지 않는다. 문제는 <b>교체 자체가 거절되는 환경이
        // 실기에 존재한다</b>는 것이고(아래 WriteAtomically 문서), 그때 예전 코드는 대상 파일에 직접
        // 써서 손상 창을 스스로 열었다. 지금은 그 마지막 경로에서도 <b>덮어쓰기 전에 지금 내용을 이
        // 파일로 대피</b>시킨다. 그래서 어느 순간에 죽어도 {본체, 직전 세대} 중 최소 하나는 온전하고,
        // Load()가 본체를 못 읽으면 이쪽을 집는다. 잃는 것은 최대 한 주기(60초)다.
        //
        // 이름이 다운그레이드 백업(character_save.v{N}.backup.json)과 다른 이유: 역할이 다르다.
        // 저쪽은 "해석할 수 없는 신버전 원본을 딱 한 번 보존"이고, 이쪽은 "매 저장마다 갱신되는
        // 바로 앞 세대"다. 한 파일에 두 역할을 맡기면 둘 중 하나가 반드시 틀린 시점에 덮인다.
        private const string PreviousFileName = "stickmate_character.prev.json";

        /// <summary>직전 세대 파일의 절대 경로. 진단/테스트에서만 쓴다.</summary>
        public static string PreviousGenerationPath => Path.Combine(SaveDirectory, PreviousFileName);

        /// <summary>마지막 로드가 본체를 못 읽어 <b>직전 세대로 되돌아갔는가</b>. 진단/테스트용.</summary>
        public static bool LoadedFromPreviousGeneration { get; private set; }

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
        //   (2) 이번 실행에서는 **저장을 보류**한다(Save()가 false를 돌려준다).
        //       "구버전 앱에서 놀던 것을 못 저장한다"는 불편은 되돌릴 수 있지만, 덮어쓴 데이터는 못 되돌린다.
        //
        // ★ 2026-09-01 정책 변경 (페르소나 재현 J1 실측 A): (2)는 원래 **백업에 실패했을 때만**이었고,
        // 백업에 성공하면 신버전 파일을 그대로 덮어썼다("원본이 안전하니 구버전 앱도 평소처럼 쓰라").
        // 그 판단은 "구버전 앱이 나중에 켜진다"는 <b>직렬</b> 시나리오만 가정한 것이다. 실제로는 세이브
        // 파일 하나를 여러 인스턴스가 공유하고(.claude/skills/run-stickmate/SKILL.md), 신버전 인스턴스가
        // **아직 돌고 있는 채로** 구버전이 파일을 되돌리는 일이 일어난다. 그때 백업은 손실을 막지 못한다 —
        // 백업은 딱 한 번(가장 처음) 찍히므로, 그 뒤 신버전이 만든 변경은 어느 사본에도 없다.
        // 실측(11:05:58 v8 → 11:06:03 v7)에서 설정창 키 10개가 그렇게 사라졌다. 그래서 이제 이 분기는
        // 백업 성공 여부와 무관하게 저장을 보류한다.
        //
        // 이 방어는 Load()에서만 도는 **기동 시 1회**짜리라, "이미 켜져 있는 구버전 인스턴스의 발밑에서
        // 파일이 신버전으로 바뀌는" 경로는 여전히 못 본다. 그쪽은 저장 직전 버전 재확인(아래
        // WriteAtomically의 (2)번 단계)이 맡는다 — 두 장치는 같은 원칙의 서로 다른 시점 담당이다.

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
            LoadedFromPreviousGeneration = false;
            NewerVersionFileDetected = false;
            NewerVersionBackupPath = null;
            SaveSuspended = false;
            try
            {
                string path = FilePath;

                // ★ 파일이 <b>아예 없으면</b> 직전 세대를 뒤지지 않는다(의도적). 없음은 이 클래스가
                //   처음부터 "첫 실행"으로 정의한 신호이고, 사용자가 캐릭터를 초기화하려고 파일을 치운
                //   경우도 같은 모양이다. 여기서 세대를 되살리면 "지웠는데 돌아온다"가 된다.
                //   되살리는 대상은 <b>있는데 못 읽는</b> 파일 하나뿐이다 — 그것만이 손상이다.
                if (!File.Exists(path)) return;

                if (!TryReadSaveData(path, out SaveData data, out string failure))
                {
                    if (!TryReadSaveData(PreviousGenerationPath, out data, out _))
                    {
                        // 손상된 파일을 지우지 않는다 — 사용자가 나중에 들여다볼 수 있게 남겨두고,
                        // 다음 저장이 정상 내용으로 덮어쓴다.
                        Debug.LogWarning($"[성장] 저장 파일을 읽지 못했습니다({failure}). " +
                            "기본값(Lv.1)으로 시작합니다 — 다음 저장이 정상 내용으로 덮어씁니다.");
                        return;
                    }

                    path = PreviousGenerationPath;
                    LoadedFromPreviousGeneration = true;
                    Debug.LogWarning($"[성장] 저장 파일을 읽지 못해({failure}) " +
                        $"직전 저장으로 되돌렸습니다: {path}. " +
                        "마지막 한 주기(최대 60초) 분량만 사라지고 나머지는 그대로입니다 — " +
                        "손상된 본체는 지우지 않고 남겨 둡니다(다음 저장이 정상 내용으로 덮어씁니다).");
                }

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
                // v6 이하에는 잉크색 키가 없다 — inkColorSaved가 false로 채워지고, 그 false가
                // "고른 적 없다 = 배포 기본 잉크색"이라는 정확한 사실이다(별도 버전 분기가 필요 없다).
                CharacterAppearanceModel.RestoreFromSave(data.inkColorSaved, data.inkColorName);
                // v7 이하에는 설정창 키가 없다 — 기본이 true인 두 값만 버전으로 갈라 준다(나머지는
                // "고른 적 있는가" 플래그가 false로 채워져 저절로 배포 기본값이 된다).
                // ★ v8 이하의 dialogueVisibleSeconds(초)는 여기서 <b>읽지 않는다</b> — v9 마이그레이션
                //   매핑이 "저장된 값 전부 → 기본(100%)"이기 때문이다(CurrentVersion 문서의 9 항목).
                //   그 결과 dialogueVisibleLengthSaved가 false로 채워지고, 그 false가 정확한 사실이 된다.
                bool hasAppSettings = data.version >= FirstVersionWithAppSettings;
                AppSettingsModel.RestoreFromSave(
                    hasAppSettings ? data.autoHideOnFullscreen : true,
                    hasAppSettings ? data.gearIconVisible : true,
                    data.dialogueFontSizeSaved, data.dialogueFontSize,
                    data.dialogueVisibleLengthSaved, data.dialogueVisibleLengthName,
                    data.chatterPercentSaved, data.chatterPercent,
                    data.dialogueBubbleEnabledSaved, data.dialogueBubbleEnabled);
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
        /// 저장 파일 하나를 읽어 해석해 본다. <b>던지지 않는다</b> — 못 읽는 이유를
        /// <paramref name="failure"/>에 사람이 읽을 문장으로 담고 false를 돌려준다.
        /// "없음 / 빈 파일 / 해석 실패 / version 0 이하"를 전부 <b>같은 등급</b>(못 쓰는 파일)으로 본다:
        /// 그 넷은 사용자에게 똑같이 "캐릭터가 초기화됐다"로 보이고, 넷 다 직전 세대가 구해 줄 수 있다.
        /// </summary>
        private static bool TryReadSaveData(string path, out SaveData data, out string failure)
        {
            data = null;
            try
            {
                if (!File.Exists(path)) { failure = "파일 없음"; return false; }

                string json = ReadAllTextShared(path);
                if (string.IsNullOrWhiteSpace(json)) { failure = "빈 파일"; return false; }

                var parsed = JsonUtility.FromJson<SaveData>(json);
                if (parsed == null) { failure = "JSON 해석 실패"; return false; }
                if (parsed.version <= 0) { failure = $"version {parsed.version}(손상/미완성)"; return false; }

                data = parsed;
                failure = null;
                return true;
            }
            catch (Exception e)
            {
                failure = $"{e.GetType().Name}: {e.Message}";
                return false;
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

            // ★ 2026-09-01 — 백업 성공/실패와 **무관하게** 이번 실행의 저장을 보류한다(정책 변경).
            // 예전에는 "백업에 성공했으면 저장은 정상 진행"이었다. 그 판단은 "구버전 앱이 나중에 켜진다"는
            // **직렬** 시나리오만 가정한 것이었는데, 실제 워크플로는 세이브 파일 하나를 여러 인스턴스가
            // 공유한다(.claude/skills/run-stickmate/SKILL.md). 신버전 인스턴스가 **아직 돌고 있는** 상태에서
            // 구버전이 파일을 되돌리면, 백업이 찍힌 뒤에 신버전이 만든 변경은 어느 백업에도 없다 —
            // 즉 백업은 그 손실을 막아 주지 못한다(페르소나 재현 J1 실측 A: 11:06:03에 설정창 키 10개 소실).
            // 그래서 이 클래스가 이미 채택한 저울("못 저장하는 불편은 되돌릴 수 있지만 덮어쓴 데이터는
            // 못 되돌린다")을 이 분기에도 똑같이 적용한다.
            SaveSuspended = true;

            if (TryBackupOnce(path, fileVersion, out string backupPath, out string failure))
            {
                NewerVersionBackupPath = backupPath;
                Debug.LogWarning($"[성장] 저장 파일이 이 앱보다 새로운 버전입니다(파일 v{fileVersion} > 앱 v{CurrentVersion}). " +
                    $"내용을 해석할 수 없어 기본값으로 시작하고, 원본을 덮어쓰지 않도록 이번 실행에서는 " +
                    $"**저장을 보류**합니다(이 실행에서 얻은 성장/할일은 저장되지 않습니다). " +
                    $"원본은 그대로 있고 사본도 남겼습니다: {backupPath}\n" +
                    "최신 버전 앱으로 돌아가면 지금까지의 데이터를 그대로 이어서 쓸 수 있습니다.");
                return;
            }

            // 백업조차 실패 — 보류는 이미 걸려 있으므로 사실만 남긴다.
            NewerVersionBackupPath = null;
            Debug.LogWarning($"[성장] 저장 파일이 이 앱보다 새로운 버전인데({fileVersion} > {CurrentVersion}) " +
                $"백업에 실패했습니다({failure}). " +
                "원본을 덮어쓰지 않도록 이번 실행에서는 **저장을 보류**합니다 — " +
                "이 실행에서 얻은 성장/할일은 저장되지 않습니다.");
        }

        /// <summary>
        /// 신버전 원본의 사본을 <b>딱 한 번만</b> 남긴다. 이미 사본이 있으면 손대지 않는다 —
        /// 가장 처음 백업이 가장 값지다(그 뒤의 내용은 구버전이 오염시켰을 수 있다).
        /// 복사만 한다: 원본을 지우거나 옮기는 API는 이 프로젝트에 존재하지 않는다(원칙 3 정적 감사).
        /// </summary>
        /// <returns>사본이 (지금 만들어졌거나 이미) 존재하면 true.</returns>
        private static bool TryBackupOnce(string path, int fileVersion, out string backupPath, out string failure)
        {
            backupPath = Path.Combine(SaveDirectory, BackupFileName(fileVersion));
            failure = null;
            try
            {
                if (!File.Exists(backupPath)) File.Copy(path, backupPath);
                return true;
            }
            catch (Exception e)
            {
                failure = $"{e.GetType().Name}: {e.Message}";
                backupPath = null;
                return false;
            }
        }

        // ============================================================================
        // ★ 원자적 쓰기 (2026-08-31 R5 — "저장 파일 비원자적 쓰기" 수정)
        // ============================================================================
        // 무엇이 문제였나: 저장이 `File.WriteAllText(FilePath, json)` 한 줄이었다. 이 호출은 대상
        // 파일을 **먼저 0바이트로 자르고** 내용을 쓴다. 그 사이(수 ms지만 하루 종일 켜져 있고 60초마다
        // 저장하는 앱이다)에 크래시/강제종료/전원 차단이 나면 파일은 **반쯤 쓰인 JSON**으로 남고,
        // 다음 실행의 Load()는 그것을 파싱하지 못해 "기본값(Lv.1 / 빈 할일)으로 시작"으로 떨어진다 —
        // 레벨·장비·기록·<b>사용자가 적은 오늘 할일</b>이 통째로 사라진다. 다운그레이드 방어(m6)가
        // 막으려던 것과 똑같은 종류의 조용한 전손인데, 이쪽만 무방비였다.
        //
        // 고친 방식(표준 패턴): **임시 파일에 전부 쓰고 → 디스크에 확정(fsync) → 원자적 교체**.
        //   · 교체에 File.Replace를 쓴다. 커널 수준에서 rename 한 번이라 "반쯤 교체된 상태"가
        //     존재하지 않는다 — 어느 순간에 죽어도 대상 경로에는 **옛 파일 아니면 새 파일**만 있다.
        //   · File.Move/File.Delete는 쓰지 않는다(쓸 수도 없다) — 절대 불변 원칙 3 정적 감사
        //     (Tests/EditMode/UserAssetImmutabilityAuditTests)가 그 두 API를 예외 없이 금지한다.
        //     File.Replace는 "우리 파일의 내용을 바꾸는" 행위라 그 금지의 취지에도 어긋나지 않는다.
        //   · fs.Flush(true)로 OS 버퍼까지 내려쓴 뒤에 교체한다. 이게 없으면 rename만 먼저 반영되고
        //     내용은 아직 캐시에 있는 상태에서 전원이 끊길 수 있다(빈 파일이 남는 고전적 실패).
        //   · 첫 저장(대상 파일이 아직 없을 때)에는 File.Replace가 성립하지 않으므로 **빈 파일을 먼저
        //     만들고** 교체한다. 거기서 죽어도 잃을 것은 없고(그 전에 저장된 내용이 없다), 남는 것은
        //     빈 파일이라 Load()가 IsNullOrWhiteSpace 가드로 조용히 "새 캐릭터"로 시작한다 —
        //     반쯤 쓰인 JSON이 남아 경고 로그를 내는 것보다 정확한 상태다.
        //
        // ============================================================================
        // ★ 2026-09-02 — 그 폴백이 실기에서 실제로 밟혔다. 폴백이 손상 창을 스스로 열고 있었다.
        // ============================================================================
        // 사용자 Windows 로그:
        //   "[성장] 저장 파일을 원자적으로 교체하지 못해 직접 쓰기로 물러섰습니다
        //    (IOException: 바꿀 파일을 제거할 수 없습니다)."
        //
        // ---- 무엇이 실패했는지부터 좁힌다(추측 금지) ----
        // 그 문장은 우리가 지어낸 것이 아니라 <b>OS 메시지 테이블</b>에서 온 것이다
        // (.NET은 매핑되지 않은 Win32 오류를 FormatMessage 문구 그대로 IOException에 담는다).
        // 한국어 "바꿀 파일을 제거할 수 없습니다" = ERROR_UNABLE_TO_REMOVE_REPLACED(1175).
        // ReplaceFile은 [대상 치우기] → [임시를 대상 이름으로] 두 걸음인데, 1175는 <b>첫 걸음</b>이
        // 거절됐다는 뜻이다. 이 한 줄이 후보를 실제로 잘라낸다:
        //   · "임시와 대상이 다른 볼륨"(옛 주석이 유일한 근거로 적어 둔 가설) → <b>반증</b>.
        //     그 경우 오류는 ERROR_NOT_SAME_DEVICE(17)이고, 무엇보다 두 경로는 코드 구조상
        //     같은 SaveDirectory에서 Path.Combine으로 만들어져 다른 볼륨이 될 수가 없다.
        //   · 읽기 전용 속성 / 권한 없음 → ERROR_ACCESS_DENIED(5). 게다가 <b>같은 순간 직접 쓰기는
        //     성공했다</b> — 즉 그 파일은 "쓸 수는 있는데 치울 수는 없는" 상태였다.
        // 남는 것은 <b>대상 파일에 DELETE(치우기)를 허용하지 않는 핸들이 열려 있었다</b> 하나다.
        // 그런 핸들을 여는 것은 (a) 실시간 검사/색인기/백업 도구 같은 필터 드라이버, 그리고
        // (b) <b>우리 자신</b> — File.ReadAllText는 FileShare.Read로 열고, 이 저장 파일은 설계상
        // 여러 인스턴스가 공유한다. 즉 남의 Load()나 저장 직전 버전 재확인이 내 교체를 막을 수 있었다.
        // ★ (a)와 (b) 중 실기에서 어느 쪽이었는지는 이 머신에 Windows가 없어 <b>확정하지 못했다</b>.
        //   다만 둘 다 "잠깐"이라는 성질이 같고, 처방(공유 모드 + 재시도)도 같다.
        //
        // ---- 고친 방식: 물러서더라도 원자성은 마지막에 놓는다 ----
        //   (0) 읽기를 FileShare.Delete로 연다        → 우리가 우리 교체를 막던 (b)를 구조적으로 제거
        //   (a) File.Replace(임시, 본체, 직전세대)     → 성공하면 세대 보존까지 공짜(커널 한 덩어리)
        //   (b) 짧은 재시도 3회(4/12/32ms)            → 1175는 대개 밀리초 안에 풀린다
        //   (c) File.Replace(임시, 본체, null)        → 세대 보존만 포기하고 원자성은 지킨다
        //   (d) 그림자 커밋: 본체를 직전 세대로 복사한 <b>뒤</b> 직접 쓰기
        //       → 원자적이지는 않지만 <b>손상돼도 직전 저장이 남는다</b>. 최대 손실 한 주기(60초).
        //
        // (d)를 아예 없애고 "저장하지 않고 다음 주기에 다시"로 갈지 따졌고 <b>기각</b>했다:
        // 1175를 만드는 조건 중에는 지속형(ACL이 DELETE만 막는 등)도 있어서, 그 환경에서는
        // "영원히 한 번도 저장되지 않는" 앱이 된다 — 종료 시 저장까지 포함해서. 손상 위험보다
        // 확정 전손이 나쁘다. 대신 (d)의 손상 창은 직전 세대가 받아 낸다.

        // ============================================================================
        // ★ 임시 파일 이름은 인스턴스마다 다르다 (2026-09-01, 페르소나 재현 J2)
        // ============================================================================
        // 이 저장 파일은 **여러 인스턴스가 공유한다** — 유저가 종일 켜 두는 인스턴스와 팀이 방금 낸
        // 새 빌드가 같은 파일을 본다(.claude/skills/run-stickmate/SKILL.md에 명시된 설계).
        // 그런데 임시 파일 이름이 `stickmate_character.json.writing` **하나로 고정**이라, 두 인스턴스의
        // 저장이 겹치면 서로의 임시 파일을 밟았다:
        //   (a) 겹치는 순간 FileShare.None 때문에 늦은 쪽이 IOException → 그 주기의 저장을 통째로 놓친다.
        //   (b) 더 나쁜 쪽: A가 임시 파일을 닫은 **직후** B가 FileMode.Create로 같은 경로를 0바이트로
        //       자르면, A의 File.Replace가 **B의 내용(또는 빈 파일)** 을 본체로 승격시킨다. 빈 파일이
        //       본체가 되면 다음 Load()가 IsNullOrWhiteSpace 가드로 조용히 "새 캐릭터"로 떨어진다 —
        //       이 클래스가 "가장 나쁜 실패"라고 부르는 그 결과이고, 원자적 쓰기가 막으려던 바로 그 사고다.
        // 이름 가운데에 프로세스 아이디를 넣으면 그 두 경로가 **구조적으로** 사라진다(비용 0).
        //
        // 남는 것 하나(의도적으로 받아들인다): 쓰는 도중에 죽으면 그 PID의 임시 파일이 남는다.
        // 이 앱에는 파일을 지우는 능력이 아예 없으므로(아래 MarkNotLoadedForTesting 문서) 청소하지 않는다.
        // 대신 같은 PID의 다음 저장이 그 파일을 그대로 다시 쓰고(FileMode.Create), OS가 PID를 재사용하므로
        // 무한히 늘어나지도 않는다. 이름이 `.writing`으로 **끝나는** 것은 그대로 유지한다 — 저장 파일이나
        // 백업 명명 규칙(character_save.v{N}.backup.json)과 겹치지 않게 하는 것이 원래의 이유였다.

        /// <summary>임시 파일 이름의 꼬리. 이것으로 끝나는 파일은 "쓰다 만 것"이라는 뜻이다.</summary>
        private const string TempFileSuffix = ".writing";

        private static string s_instanceTag;

        /// <summary>이 인스턴스만의 짧은 꼬리표(프로세스 아이디). 프로세스 아이디를 얻을 수 없는
        /// 플랫폼(예: 샌드박스가 막는 모바일 IL2CPP)에서는 실행마다 다른 난수로 물러선다 —
        /// 여기서 필요한 성질은 "다른 인스턴스와 겹치지 않는다" 하나뿐이라 그 폴백으로 충분하다.</summary>
        private static string InstanceTag
        {
            get
            {
                if (s_instanceTag != null) return s_instanceTag;
                try
                {
                    using (var self = System.Diagnostics.Process.GetCurrentProcess())
                    {
                        s_instanceTag = self.Id.ToString(CultureInfo.InvariantCulture);
                    }
                }
                catch (Exception)
                {
                    s_instanceTag = "r" + Guid.NewGuid().ToString("N").Substring(0, 8);
                }
                return s_instanceTag;
            }
        }

        /// <summary>쓰는 중인 임시 파일의 이름 — <c>stickmate_character.json.&lt;인스턴스&gt;.writing</c>.
        /// 저장 파일명과 <b>다른 확장자</b>로 끝나 저장 파일로 오인되거나 백업 명명 규칙과 겹치지 않고,
        /// 가운데 꼬리표가 인스턴스끼리의 충돌을 막는다(위 문단).</summary>
        private static string TempFileName => FileName + "." + InstanceTag + TempFileSuffix;

        /// <summary>쓰는 중인 임시 파일의 절대 경로. 진단/테스트에서만 쓴다.</summary>
        public static string TempFilePath => Path.Combine(SaveDirectory, TempFileName);

        /// <summary>마지막 저장이 원자적 교체 경로로 끝났는가(false = 그림자 커밋으로 물러섰다). 진단/테스트용.</summary>
        public static bool LastSaveWasAtomic { get; private set; }

        /// <summary>마지막 저장이 <b>직전 세대</b>를 남겼는가. 첫 저장에는 남길 것이 없어 false다.
        /// 진단/테스트용.</summary>
        public static bool LastSaveKeptPreviousGeneration { get; private set; }

        /// <summary>원자적 교체가 연속으로 실패한 횟수(성공하면 0). 로그 도배를 막는 데도 쓴다.</summary>
        public static int ConsecutiveAtomicCommitFailures { get; private set; }

        // ============================================================================
        // ★ 원자적 교체 실패를 테스트가 강제로 만든다 (2026-09-02)
        // ============================================================================
        // 이 사다리의 아래쪽 단은 <b>Windows에서만, 그것도 가끔</b> 밟힌다. 이 개발 머신(macOS)에서
        // File.Replace는 그냥 성공하므로, 주입구가 없으면 폴백 경로는 <b>영원히 한 줄도 실행되지 않고</b>
        // 검증도 불가능하다 — 사용자 실기에서 처음 밟히는 코드가 되어 버린다. 그래서 "몇 번 실패한
        // 것으로 칠 것인가"만 테스트가 정한다. 프로덕션에서는 0이라 분기 하나 값이 비용의 전부다.
        private static int s_forcedAtomicCommitFailures;

        /// <summary>원자적 교체 사다리가 <b>전부</b> 실패하려면 몇 번을 막아야 하는가.
        /// 테스트가 숫자를 베껴 적지 않도록 프로덕션 상수에서 파생시킨다(CLAUDE.md 협업 프로토콜).</summary>
        internal static int AtomicCommitAttemptBudget => ReplaceRetryBackoffMilliseconds.Length + 2;

        /// <summary>테스트 전용 — 다음 <paramref name="count"/>번의 교체 시도를 실패한 것으로 만든다.
        /// <see cref="AtomicCommitAttemptBudget"/>을 주면 "이 환경에서는 교체가 아예 안 된다"가 된다.</summary>
        internal static void ForceAtomicCommitFailuresForTesting(int count) => s_forcedAtomicCommitFailures = count;

        // ★ 그림자 커밋의 값어치는 "깨지는 순간"에만 있다. 그래서 <b>깨지는 순간을 실제로 만든다</b> —
        //   테스트가 밖에서 파일을 잘라 상태를 흉내내는 것이 아니라, 프로덕션 순서(대피 → 덮어쓰기)를
        //   그대로 밟다가 덮어쓰기 <b>도중</b>에 멈춘다. 그때 디스크에 남는 모양은 프로세스가 그 자리에서
        //   사라졌을 때와 같고(앞부분만 쓰인 본체), 이어지는 예외는 "그 뒤로 아무 일도 일어나지 않는다"를
        //   재현한다(Save()가 false를 돌려주고 모델은 더티로 남는다 = 저장되지 않았다).
        private static int s_deathAfterBytesForTesting = -1;

        /// <summary>테스트 전용 — 그림자 커밋의 덮어쓰기를 <paramref name="afterBytes"/>바이트에서
        /// 끊고 프로세스가 사라진 것처럼 만든다. 음수면 꺼진다(1회용).</summary>
        internal static void SimulateDeathDuringOverwriteForTesting(int afterBytes) =>
            s_deathAfterBytesForTesting = afterBytes;

        /// <summary>본체 덮어쓰기 한 곳. 테스트 주입이 걸려 있으면 앞부분만 쓰고 그대로 멈춘다.</summary>
        private static void OverwriteDestination(string path, string json)
        {
            if (s_deathAfterBytesForTesting >= 0)
            {
                int cut = Math.Min(s_deathAfterBytesForTesting, json.Length);
                s_deathAfterBytesForTesting = -1;
                File.WriteAllText(path, json.Substring(0, cut));
                throw new IOException("[테스트 주입] 덮어쓰기 도중 프로세스가 사라진 상황을 흉내냅니다.");
            }

            File.WriteAllText(path, json);
        }

        // ============================================================================
        // ★ 저장 직전 버전 재확인 — "내 것보다 새로운 저장을 조용히 덮어쓰지 않는다"
        //    (2026-09-01, 페르소나 재현 J1 실측 B)
        // ============================================================================
        // 다운그레이드 방어(m6)는 <b>Load() 안에만</b> 있었고 Load()는 기동 시 1회뿐이다. 그래서
        // "구버전 앱이 나중에 켜진다"는 직렬 시나리오만 막혔고, **이미 켜져 있는 구버전 인스턴스의
        // 발밑에서 파일이 신버전으로 바뀌는** 경로는 완전한 사각지대였다 — 실측으로 15초 만에
        // 구버전이 v8 파일을 v7로 되돌렸고, 새 백업도 경고 로그도 0줄이었다.
        //
        // 막는 방법: 대상 파일을 갈아끼우기 **직전에** 디스크에 있는 파일의 version 한 필드만 다시 읽어,
        // 그게 내가 쓰려는 버전보다 높으면 내 쓰기를 포기한다. 비용은 read 1회(주기 60초)다.
        //
        // 이것은 <b>락이 아니다</b>(최선 노력). 확인과 File.Replace 사이의 마이크로초 창은 남는다 —
        // 그래서 확인을 파일을 만지기 **가장 가까운 자리**에 둔다. 진짜 배타 제어가 필요해지면
        // 단일 인스턴스 락이 답이고(현재 프로젝트에 0건), 그건 이 클래스 밖의 결정이다.
        // 같은 버전끼리의 갱신 손실(두 v8 인스턴스가 서로의 값을 덮는 것)도 이 가드의 범위가 아니다 —
        // 여기서 지키는 것은 "스키마가 더 새로운 파일을 옛 스키마로 되돌리지 않는다" 하나다.

        /// <summary>저장 직전 확인 전용 — 파일의 <c>version</c> 한 필드만 읽기 위한 최소 스키마.
        /// <see cref="SaveData"/>로 읽지 않는 이유: 신버전 파일에는 우리가 모르는 필드가 들어 있고,
        /// 여기서 알고 싶은 것은 오직 "이 파일이 나보다 새로운가" 하나다.</summary>
        [Serializable]
        private sealed class VersionProbe
        {
            public int version;
        }

        /// <summary>디스크에 있는 저장 파일의 <c>version</c>만 다시 읽는다.
        /// 읽을 수 없으면(파일 없음/빈 파일/손상/권한/파싱 실패) <b>false</b>를 돌려주고, 그때는
        /// 저장을 막지 <b>않는다</b>. 손상된 파일 하나가 저장 기능을 영구히 잠그는 쪽이 더 나쁘고,
        /// 그 경우의 계약은 원래부터 "다음 저장이 정상 내용으로 덮어쓴다"였다(Load의 catch 참고).</summary>
        private static bool TryReadDiskVersion(string path, out int version)
        {
            version = 0;
            try
            {
                if (!File.Exists(path)) return false;

                string json = ReadAllTextShared(path);
                if (string.IsNullOrWhiteSpace(json)) return false;

                var probe = JsonUtility.FromJson<VersionProbe>(json);
                if (probe == null || probe.version <= 0) return false;

                version = probe.version;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 저장 직전에 발견한 "나보다 새로운 파일"을 <b>그대로 두고</b> 물러선다.
        /// 원본은 한 바이트도 건드리지 않고, 사본을 한 번 남기고, 이번 실행의 저장을 보류한다
        /// (보류가 있어야 60초마다 같은 경고가 반복되지 않고, 종료 시 저장도 원본을 건드리지 않는다).
        /// </summary>
        private static void AbandonWriteToNewerFile(string path, int diskVersion)
        {
            NewerVersionFileDetected = true;
            SaveSuspended = true;

            bool backedUp = TryBackupOnce(path, diskVersion, out string backupPath, out string failure);
            NewerVersionBackupPath = backedUp ? backupPath : null;

            Debug.LogWarning($"[성장] 저장을 취소했습니다 — 저장 파일이 그 사이 이 앱보다 새로운 버전이 " +
                $"되었습니다(디스크 v{diskVersion} > 앱 v{CurrentVersion}). 다른 인스턴스(새 빌드)가 같은 " +
                $"파일에 이미 저장했다는 뜻이라, 여기서 덮어쓰면 그쪽 데이터가 사라집니다. " +
                (backedUp
                    ? $"원본은 그대로 두었고 사본도 남겼습니다: {backupPath}. "
                    : $"사본 만들기는 실패했지만({failure}) 원본은 손대지 않았습니다. ") +
                "이번 실행에서 얻은 성장/할일은 저장되지 않습니다 — 되돌릴 수 없는 덮어쓰기 대신 " +
                "되돌릴 수 있는 불편을 택했습니다(이 클래스의 다운그레이드 방어와 같은 저울).");
        }

        // ============================================================================
        // ★ 교체 재시도 — 1175는 "지금은 안 된다"이지 "여기서는 안 된다"가 아니다 (2026-09-02)
        // ============================================================================
        // 실기 로그가 남긴 결정적 단서: 교체가 거절된 <b>바로 그 순간</b>에 이어진 직접 쓰기는
        // 성공했다. 즉 그 시점의 대상 파일은 <b>쓸 수는 있는데 치울 수는 없는</b> 상태였고,
        // 그 상태는 대개 밀리초 단위로 사라진다(검사 필터/색인기/백업 도구가 손을 뗀다).
        // 그래서 첫 대응은 "물러서기"가 아니라 "조금 기다렸다 다시"다. 즉시 재시도는 같은 실패를
        // 반복할 뿐이라 아주 짧게 재운다 — <b>실패했을 때만</b> 최대 48ms(4+12+32).
        // 이 대기는 Save()의 [스톨구간] 안에서 일어나므로 스톨 귀인에 정직하게 잡힌다.
        private static readonly int[] ReplaceRetryBackoffMilliseconds = { 4, 12, 32 };

        /// <summary>연속 실패가 이어질 때 경고를 몇 번에 한 번 남길 것인가. 60초 주기 저장 기준
        /// 약 한 시간에 한 줄이다 — 하루 종일 켜 두는 앱이라 매 분 같은 줄을 남기면 로그가 죽는다.</summary>
        private const int AtomicCommitFailureLogEvery = 60;

        /// <summary>교체 1회. 테스트 주입이 걸려 있으면 실제로 만지지 않고 실패한 것으로 친다
        /// (임시 파일이 소모되지 않으므로 다음 단이 그대로 이어서 시도할 수 있다 — 실기와 같은 모양).</summary>
        private static void ReplaceOnce(string temp, string path, string backup)
        {
            if (s_forcedAtomicCommitFailures > 0)
            {
                s_forcedAtomicCommitFailures--;
                throw new IOException("[테스트 주입] 원자적 교체 실패를 흉내냅니다 " +
                    "(Windows ERROR_UNABLE_TO_REMOVE_REPLACED 재현).");
            }

            File.Replace(temp, path, backup);
        }

        /// <summary>원자성을 잃지 않는 범위에서 할 수 있는 것을 전부 해 본다.
        /// 성공하면 null, 전부 실패하면 마지막 예외를 돌려준다.</summary>
        private static Exception TryCommitAtomically(string temp, string path, string previous)
        {
            Exception last = null;

            // (a) 직전 세대를 남기는 교체. 커널이 "대상→직전 세대 / 임시→대상"을 한 덩어리로 처리하므로
            //     우리 쪽 추가 IO는 0이고, 성공하면 언제나 바로 앞 세대가 디스크에 남는다.
            for (int attempt = 0; attempt <= ReplaceRetryBackoffMilliseconds.Length; attempt++)
            {
                if (attempt > 0) System.Threading.Thread.Sleep(ReplaceRetryBackoffMilliseconds[attempt - 1]);

                try
                {
                    // 첫 저장이면 교체 대상이 있어야 하므로 빈 파일을 만든다. 그 빈 파일을 세대로
                    // 남길 이유는 없으므로(잃을 것이 없다) 이때만 백업 인자를 비운다.
                    bool firstSave = !File.Exists(path);
                    if (firstSave) File.WriteAllText(path, string.Empty);

                    ReplaceOnce(temp, path, firstSave ? null : previous);
                    LastSaveKeptPreviousGeneration = !firstSave;
                    return null;
                }
                catch (Exception e) { last = e; }
            }

            // (b) 마지막 원자적 시도 — 세대 보존을 <b>포기</b>한다. 세대를 남기는 형태는 대상 파일을
            //     지우는 대신 이름을 바꾸므로 실패 지점이 하나 더 있다. 그 하나 때문에 원자성을
            //     통째로 잃는 것은 손해다(직전 세대는 앞선 성공이 남긴 것이 그대로 남아 있다).
            try
            {
                if (!File.Exists(path)) File.WriteAllText(path, string.Empty);
                ReplaceOnce(temp, path, null);
                LastSaveKeptPreviousGeneration = false;
                return null;
            }
            catch (Exception e) { return e; }
        }

        /// <summary>지금 디스크에 있는 <b>온전한</b> 내용을 직전 세대로 대피시킨다.
        /// 온전하지 <b>않으면</b> 손대지 않는다 — 그때 기존 세대가 유일한 복구원이고,
        /// 깨진 본체로 그것을 덮으면 마지막 안전망까지 함께 잃는다.</summary>
        private static bool TryShelterCurrentGeneration(string path, string previous)
        {
            try
            {
                if (!File.Exists(path)) return false;            // 첫 저장 — 대피시킬 것이 없다
                if (!TryReadDiskVersion(path, out _)) return false;  // 이미 못 쓰는 내용이다

                File.Copy(path, previous, true);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <returns>대상 저장 파일을 실제로 갱신했으면 true. 위 가드에 걸려 <b>쓰지 않고 물러섰으면</b>
        /// false — 그때 호출자는 모델을 "저장됨"으로 표시하면 안 된다.</returns>
        private static bool WriteAtomically(string json)
        {
            string path = FilePath;
            string temp = TempFilePath;
            string previous = PreviousGenerationPath;

            // (1) 전량을 임시 파일에 쓰고 디스크에 확정한다. 여기서 죽으면 대상 파일은 손도 대지 않은
            //     옛 내용 그대로다(이 수정의 핵심).
            using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(fs, new UTF8Encoding(false)))
            {
                writer.Write(json);
                writer.Flush();
                fs.Flush(true);   // OS 버퍼 → 디스크. 전원 차단까지 견디게 하는 한 줄이다.
            }

            // (2) ★ 교체 **직전** 확인 — 그 사이 다른 인스턴스가 더 새로운 버전으로 저장했는가.
            //     일부러 아래 사다리 밖에 둔다: 이 확인이 예외를 내면 아래로 떨어져
            //     막으려던 덮어쓰기를 스스로 저지르게 된다. TryReadDiskVersion은 던지지 않는다.
            if (TryReadDiskVersion(path, out int diskVersion) && diskVersion > CurrentVersion)
            {
                // 임시 파일은 남지만 다음 저장이 같은 이름을 다시 쓴다(위 임시 파일 문단).
                AbandonWriteToNewerFile(path, diskVersion);
                return false;
            }

            // (3) 원자적 커밋 사다리.
            Exception failure = TryCommitAtomically(temp, path, previous);
            if (failure == null)
            {
                ConsecutiveAtomicCommitFailures = 0;
                LastSaveWasAtomic = true;
                return true;
            }

            // (4) 그림자 커밋 — 원자성을 잃는 유일한 경로다. 그래서 <b>잃을 것을 먼저 대피시킨다</b>.
            //     순서가 전부다: 대피(복사) → 덮어쓰기. 어느 순간에 죽어도 {본체, 직전 세대} 중
            //     최소 하나는 온전하고, 다음 Load()가 그 하나를 집는다.
            //       · 대피 중 사망      → 본체 온전, 세대 깨짐 → 본체를 읽는다
            //       · 덮어쓰기 중 사망  → 본체 깨짐, 세대 온전 → 세대를 읽는다(최대 한 주기 손실)
            ConsecutiveAtomicCommitFailures++;
            LastSaveWasAtomic = false;
            bool sheltered = TryShelterCurrentGeneration(path, previous);
            LastSaveKeptPreviousGeneration = sheltered;
            OverwriteDestination(path, json);
            LogAtomicCommitFallback(failure, sheltered, previous);
            return true;
        }

        private static void LogAtomicCommitFallback(Exception failure, bool sheltered, string previous)
        {
            int n = ConsecutiveAtomicCommitFailures;
            if (n != 1 && n % AtomicCommitFailureLogEvery != 0) return;

            Debug.LogWarning($"[성장] 저장 파일을 원자적으로 교체하지 못했습니다" +
                $"({failure.GetType().Name}: {failure.Message}) — 재시도 {AtomicCommitAttemptBudget}회 전부 실패" +
                (n > 1 ? $", 연속 {n}회째" : string.Empty) + ". " +
                (sheltered
                    ? $"덮어쓰기 전에 직전 저장을 {previous} 로 대피시켰습니다 — 이번 쓰기 도중 강제 " +
                      "종료되더라도 다음 실행이 직전 저장으로 자동 복구합니다(최대 한 주기 손실)."
                    : "대피시킬 온전한 직전 내용이 없어(첫 저장이거나 본체가 이미 손상) 직전 세대를 " +
                      "갱신하지 못했습니다 — 이번 쓰기 도중 강제 종료되면 이번 분량을 잃을 수 있습니다."));
        }

        /// <summary>미착용을 <c>null</c>이 아니라 빈 문자열로 적는다 — JsonUtility는 null 문자열을
        /// <c>""</c>로 직렬화하므로, 읽는 쪽이 둘을 구분하려 들면 없는 차이를 다루게 된다.</summary>
        private static string WornId(EquipmentSlot slot) => EquipmentModel.WornItemId(slot) ?? string.Empty;

        /// <summary>성공하면 true. 실패해도 예외를 밖으로 던지지 않는다(클래스 문서 참고).</summary>
        public static bool Save()
        {
            // ★ [스톨구간] 계측 (2026-09-01 2차 스파이크 라운드).
            // 이 메서드는 <b>동기 파일 IO 3연타</b>다: (1) 임시 파일에 쓰고 fs.Flush(true)로 fsync,
            // (2) 대상 파일을 통째로 다시 읽어 version 확인, (3) File.Replace.
            // Windows에서 이 셋은 각각 커널/필터 드라이버(실시간 검사)를 통과하므로 수십~수백 ms를
            // 막을 수 있고, <b>Debug.Log가 아니라 [스톨귀인]의 "로그" 항목에는 절대 잡히지 않는다.</b>
            // 그리고 이 경로는 Update 안에서 동기로 불린다(CharacterProgressionDirector.Update의
            // 60초 자동 저장 + 정보창/설정창/할일의 즉시 저장) = 곧 "기타로직"의 유력 후보다.
            // 여기 이름표를 붙여 두면 다음 실기 로그가 추측 없이 답한다.
            using var __stall = global::StickMate.Platform.StallAttribution.Section(
                global::StickMate.Platform.StallSection.Save);

            // ★ 저장 보류 — 이번 실행이 "나보다 새로운 저장 파일"을 이미 만났다면 두 번 다시 쓰지 않는다.
            //   거는 자리는 둘: 기동 시 Load()의 다운그레이드 방어(m6), 그리고 저장 직전 버전 재확인
            //   (AbandonWriteToNewerFile). 여기서 일찍 끊어야 60초마다 같은 경고가 반복되지 않고,
            //   종료 시 저장(OnApplicationQuit)도 신버전 파일을 건드리지 않는다.
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
                    inkColorSaved = CharacterAppearanceModel.HasInkColor,
                    inkColorName = CharacterAppearanceModel.InkColorSaveName(),
                    autoHideOnFullscreen = AppSettingsModel.AutoHideOnFullscreen,
                    gearIconVisible = AppSettingsModel.GearIconVisible,
                    dialogueFontSizeSaved = AppSettingsModel.HasDialogueFontSize,
                    dialogueFontSize = AppSettingsModel.DialogueFontSize,
                    dialogueVisibleLengthSaved = AppSettingsModel.HasDialogueVisibleLength,
                    dialogueVisibleLengthName = AppSettingsModel.DialogueVisibleLengthSaveName(),
                    chatterPercentSaved = AppSettingsModel.HasChatterPercent,
                    chatterPercent = AppSettingsModel.ChatterPercent,
                    dialogueBubbleEnabledSaved = AppSettingsModel.HasDialogueBubbleEnabled,
                    dialogueBubbleEnabled = AppSettingsModel.DialogueBubbleEnabled,
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
                // ★ 쓰지 않고 물러섰으면(디스크가 더 새로운 버전) 여기서 끝낸다 — MarkSaved를 부르면
                //   모델이 "디스크에 반영됨"이라고 거짓말을 하게 되고, 그 거짓말은 나중에 진짜 저장
                //   기회를 잡아먹는다(변경분이 있어야만 주기 저장이 돈다).
                if (!WriteAtomically(JsonUtility.ToJson(data, true))) return false;

                CharacterProgressionModel.MarkSaved();
                CharacterStatsModel.MarkSaved();
                UiLayoutModel.MarkSaved();
                CharacterAppearanceModel.MarkSaved();
                AppSettingsModel.MarkSaved();
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
