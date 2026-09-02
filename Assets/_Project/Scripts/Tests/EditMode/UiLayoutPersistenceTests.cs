using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 톱니 아이콘 위치의 영속화(Core/UiLayoutModel.cs + Core/CharacterSaveStore.cs 스키마 v3) 회귀 테스트
    /// — 2026-08-30 사용자 요청("캐릭터 설정 기어들도 길게 클릭해서 위치 옮길 수 있게 해줘")의 "재시작해도
    /// 유지된다" 절반을 잠근다(나머지 절반 — 실제 드래그 조작 — 은 Tests/PlayMode/InfoGearDragTests.cs).
    ///
    /// 이 파일이 지키는 절대 조건은 두 가지다:
    ///  ① 옮긴 좌표가 저장 -> 로드 왕복에서 살아남는다.
    ///  ② <b>구버전(v1/v2) 저장 파일이 그대로 읽힌다</b> — 새 필드가 없으면 "아직 옮긴 적 없음"이 되어
    ///     기본 위치(우상단)로 뜬다. 스키마를 올릴 때마다 사용자의 레벨/기록이 날아가지 않게 하는
    ///     이 프로젝트의 관례(CharacterStatsPersistenceTests와 같은 정신)를 이어서 확인한다.
    ///
    /// 파일 취급도 그 관례 그대로다: 실행 중인 실제 앱의 저장 파일을 건드리므로 전후로 백업/복원하고,
    /// 대상은 언제나 <see cref="CharacterSaveStore.FilePath"/> 하나뿐이다.
    /// </summary>
    public sealed class UiLayoutPersistenceTests
    {
        private string _backup;
        private bool _hadFile;

        [OneTimeSetUp]
        public void BackupRealSaveFile()
        {
            string path = CharacterSaveStore.FilePath;
            _hadFile = File.Exists(path);
            _backup = _hadFile ? File.ReadAllText(path) : null;
        }

        [OneTimeTearDown]
        public void RestoreRealSaveFile()
        {
            string path = CharacterSaveStore.FilePath;
            if (_hadFile) File.WriteAllText(path, _backup);
            else if (File.Exists(path)) File.Delete(path);

            CharacterProgressionModel.ResetForTesting();
            EquipmentModel.ResetForTesting();
            CharacterStatsModel.ResetForTesting();
            UiLayoutModel.ResetForTesting();
        }

        [SetUp]
        public void ResetModels()
        {
            CharacterProgressionModel.ResetForTesting();
            EquipmentModel.ResetForTesting();
            CharacterStatsModel.ResetForTesting();
            UiLayoutModel.ResetForTesting();
        }

        [Test]
        public void 옮긴_톱니_위치가_저장하고_다시_불러온_뒤에도_같다()
        {
            Assert.IsFalse(UiLayoutModel.HasGearCenter, "초기 상태는 '옮긴 적 없음'이어야 합니다.");

            UiLayoutModel.SetGearCenter(new Vector2(412.5f, 733.25f));
            Assert.IsTrue(UiLayoutModel.IsDirty, "위치가 바뀌었는데 저장 대상으로 표시되지 않았습니다.");
            Assert.IsTrue(CharacterSaveStore.Save(), "저장에 실패했습니다.");
            Assert.IsFalse(UiLayoutModel.IsDirty, "저장했는데 여전히 저장 대상으로 남아 있습니다.");

            UiLayoutModel.ResetForTesting();
            CharacterSaveStore.Load();

            Assert.IsTrue(UiLayoutModel.HasGearCenter, "옮긴 위치가 복원되지 않았습니다 — 재시작하면 우상단으로 돌아가 버립니다.");
            Assert.AreEqual(412.5f, UiLayoutModel.GearCenterPoints.x, 0.01f);
            Assert.AreEqual(733.25f, UiLayoutModel.GearCenterPoints.y, 0.01f);
            Assert.IsFalse(UiLayoutModel.IsDirty, "복원 직후는 저장 대상이 아니어야 합니다(복원은 변화가 아니다).");
        }

        [Test]
        public void 구버전_v2_저장_파일은_옮긴_적_없음으로_읽힌다()
        {
            // v2에는 톱니 위치 필드가 아예 없다 — JsonUtility가 false/0으로 채우고, 그 false가
            // "아직 옮긴 적 없다"는 정확한 사실이다(좌표 0,0으로 튀면 안 된다).
            const string V2Json =
                "{\n" +
                "    \"version\": 2,\n" +
                "    \"level\": 7,\n" +
                "    \"currentXp\": 10.0,\n" +
                "    \"totalXpEarned\": 700.0,\n" +
                "    \"characterName\": \"옛동료\",\n" +
                "    \"equippedHead\": true,\n" +
                "    \"equippedEyes\": false,\n" +
                "    \"equippedNeck\": false,\n" +
                "    \"equippedShoulders\": false,\n" +
                "    \"battleWins\": 3,\n" +
                "    \"rivalWins\": 1,\n" +
                "    \"archeryShots\": 5,\n" +
                "    \"archeryBullseyes\": 2,\n" +
                "    \"companionSeconds\": 120.0,\n" +
                "    \"ragdollFalls\": 4,\n" +
                "    \"firstRunUnixSeconds\": 1788038056\n" +
                "}";
            File.WriteAllText(CharacterSaveStore.FilePath, V2Json);

            CharacterSaveStore.Load();

            Assert.IsTrue(CharacterSaveStore.LoadedFromFile, "구버전 파일을 읽지 못했습니다 — 사용자의 진행도가 날아갑니다.");
            Assert.AreEqual(7, CharacterProgressionModel.Level, "구버전 파일의 레벨이 복원되지 않았습니다.");
            Assert.AreEqual(3, CharacterStatsModel.BattleWins, "구버전 파일의 기록이 복원되지 않았습니다.");
            Assert.IsFalse(UiLayoutModel.HasGearCenter,
                "구버전 파일에는 톱니 위치가 없으므로 '옮긴 적 없음'이어야 합니다 — 0,0(화면 좌상단 구석)으로 튀면 안 됩니다.");
        }

        [Test]
        public void 구버전_v1_저장_파일도_옮긴_적_없음으로_읽힌다()
        {
            // ★ 2026-08-30 횡단 리뷰 m5 — 위 v2 경로만 단언돼 있고 **v1 경로는 미검증**이었다.
            // v1은 기록 필드(battleWins 등)와 톱니 위치 필드가 **둘 다** 없는 가장 오래된 파일이라,
            // JsonUtility가 두 그룹을 동시에 기본값으로 채우는 유일한 경로다. v2와 같은 코드가 도는
            // 것처럼 보이지만 "며칠 키운 v1 사용자"는 실제로 존재하는 집합이고, 이 경로가 깨지면
            // 그 사용자만 레벨이 날아간다 — 가장 오래된 사용자가 가장 크게 잃는 형태의 회귀다.
            const string V1Json =
                "{\n" +
                "    \"version\": 1,\n" +
                "    \"level\": 9,\n" +
                "    \"currentXp\": 42.0,\n" +
                "    \"totalXpEarned\": 1500.0,\n" +
                "    \"characterName\": \"최초동료\",\n" +
                "    \"equippedHead\": true,\n" +
                "    \"equippedEyes\": true,\n" +
                "    \"equippedNeck\": false,\n" +
                "    \"equippedShoulders\": false\n" +
                "}";
            File.WriteAllText(CharacterSaveStore.FilePath, V1Json);

            CharacterSaveStore.Load();

            Assert.IsTrue(CharacterSaveStore.LoadedFromFile,
                "가장 오래된(v1) 파일을 읽지 못했습니다 — 며칠 키운 사용자의 진행도가 날아갑니다.");
            Assert.AreEqual(9, CharacterProgressionModel.Level, "v1 파일의 레벨이 복원되지 않았습니다.");
            Assert.AreEqual("최초동료", CharacterProgressionModel.CharacterName);
            Assert.IsTrue(EquipmentModel.IsEquipped(EquipmentSlot.Eyes), "v1 파일의 장비가 복원되지 않았습니다.");
            Assert.AreEqual(0, CharacterStatsModel.BattleWins, "v1 파일에 없던 기록은 0이어야 합니다.");

            // ★ m5의 핵심 단언 — v2와 **같은 결론**이 v1에서도 나와야 한다.
            Assert.IsFalse(UiLayoutModel.HasGearCenter,
                "v1 파일에는 톱니 위치가 없으므로 '옮긴 적 없음'이어야 합니다 — 0,0(화면 좌상단 구석)으로 " +
                "튀면 톱니가 메뉴바 뒤에 숨어 사용자가 다시는 찾지 못합니다.");
            Assert.AreEqual(0f, UiLayoutModel.GearCenterPoints.x, 0.001f,
                "'옮긴 적 없음'인데 좌표가 채워져 있습니다 — 플래그와 좌표가 어긋났습니다.");
        }

        [Test]
        public void 같은_자리로_다시_세팅하면_저장_대상이_되지_않는다()
        {
            // 위젯이 매 프레임 클램프 결과를 되돌려 주므로(화면 경계 보정), 같은 값 재세팅이 IsDirty를
            // 세우면 주기 저장이 60초마다 무의미하게 디스크를 두드리게 된다.
            UiLayoutModel.SetGearCenter(new Vector2(100f, 200f));
            UiLayoutModel.MarkSaved(); // internal — EditMode 어셈블리는 InternalsVisibleTo로 접근 가능.

            UiLayoutModel.SetGearCenter(new Vector2(100f, 200f));
            Assert.IsFalse(UiLayoutModel.IsDirty, "같은 위치를 다시 세팅했는데 저장 대상이 되었습니다.");

            UiLayoutModel.SetGearCenter(new Vector2(100f, 205f));
            Assert.IsTrue(UiLayoutModel.IsDirty, "실제로 옮겼는데 저장 대상이 되지 않았습니다.");
        }

        // ==================== 되돌리기(2026-09-02 P0, docs/UX_FLOW.md 41-8) ====================

        /// <summary>
        /// ★ <b>되돌린 사실이 메모리가 아니라 디스크까지 내려가는가.</b>
        ///
        /// <para>이 테스트가 없으면 <c>ClearGearCenter()</c>가 메모리만 지우고 다음 실행에 옛 자리가
        /// 되살아나는 실패가 <b>초록으로 보인다</b>(설정창에서 눌렀을 때 톱니는 실제로 우상단으로
        /// 돌아가므로 눈으로는 성공처럼 보인다 — 재시작해야 드러난다).</para>
        ///
        /// <para>★ 앞부분은 <b>양성 대조</b>다: 같은 저장/복원 경로로 <c>true</c>가 왕복하는 것을 먼저
        /// 보인다. 그게 없으면 뒤의 <c>IsFalse</c>는 "되돌리기가 동작했다"와 "저장 자체가 죽었다"를
        /// 구분하지 못한다.</para>
        /// </summary>
        [Test]
        public void 톱니_위치_되돌리기가_저장_파일까지_내려간다()
        {
            // ---- 양성 대조: 옮긴 자리가 실제로 왕복한다 ----
            UiLayoutModel.SetGearCenter(new Vector2(412.5f, 733.25f));
            Assert.IsTrue(CharacterSaveStore.Save(), "준비 단계 저장에 실패했습니다.");
            UiLayoutModel.ResetForTesting();
            CharacterSaveStore.Load();
            Assert.IsTrue(UiLayoutModel.HasGearCenter,
                "양성 대조 실패 — 옮긴 자리조차 왕복하지 않습니다. 이 상태에서는 아래 '되돌아갔다'는 " +
                "판정이 아무것도 증명하지 못합니다(프로브가 죽은 것과 구분되지 않습니다).");

            // ---- 본 검증 ----
            Assert.IsTrue(UiLayoutModel.ClearGearCenter(),
                "되돌릴 것이 있는데 ClearGearCenter가 '할 일 없음'(false)을 돌려줬습니다.");
            Assert.IsTrue(UiLayoutModel.IsDirty,
                "되돌렸는데 저장 대상으로 표시되지 않았습니다 — 주기 저장이 이 변화를 흘려보냅니다.");
            Assert.IsTrue(CharacterSaveStore.Save(), "되돌린 뒤 저장에 실패했습니다.");
            Assert.IsFalse(UiLayoutModel.IsDirty, "저장했는데 여전히 저장 대상으로 남아 있습니다.");

            UiLayoutModel.ResetForTesting();
            CharacterSaveStore.Load();

            Assert.IsTrue(CharacterSaveStore.LoadedFromFile, "되돌린 뒤의 저장 파일을 읽지 못했습니다.");
            Assert.IsFalse(UiLayoutModel.HasGearCenter,
                "되돌렸는데 재시작하면 옛 자리가 되살아납니다 — 되돌리기가 메모리에만 남았습니다.");
            Assert.AreEqual(0f, UiLayoutModel.GearCenterPoints.x, 0.001f,
                "'옮긴 적 없음'인데 좌표가 남아 있습니다 — 플래그와 좌표가 어긋났습니다.");
            Assert.AreEqual(0f, UiLayoutModel.GearCenterPoints.y, 0.001f);
        }

        /// <summary>
        /// ★ <b>디스크에 실제로 무엇이 쓰였는가</b> — 위 테스트와 <b>다른 방법</b>으로 다시 잰다.
        /// 위 테스트는 <c>Load()</c>라는 <b>우리 코드</b>를 통해 봤다. 여기서는 JSON 원문을 직접 읽는다
        /// (같은 함정에 같이 빠지지 않게 — 예: Save가 안 써도 Load가 메모리를 안 건드리면 초록이 된다).
        ///
        /// <para>그리고 <b>스키마 버전이 안 올라갔다</b>는 것도 여기서 잠근다: 되돌리기는 새 필드가
        /// 아니라 <c>gearPositionSaved</c>의 값 변경일 뿐이므로 파일의 <c>version</c>은
        /// <see cref="CharacterSaveStore.CurrentVersion"/> 그대로여야 한다(숫자를 베끼지 않고 상수를 참조한다).</para>
        /// </summary>
        [Test]
        public void 되돌리기가_기존_필드의_값만_바꾼다_버전은_그대로()
        {
            // ---- 양성 대조: 같은 방법(원문 스캔)이 true도 실제로 잡아내는가 ----
            UiLayoutModel.SetGearCenter(new Vector2(300f, 120f));
            Assert.IsTrue(CharacterSaveStore.Save());
            string movedJson = File.ReadAllText(CharacterSaveStore.FilePath);
            Assert.IsTrue(GearSavedFlagInJson(movedJson, out bool movedFlag),
                "저장 파일에서 gearPositionSaved 키를 찾지 못했습니다 — 스캐너가 죽었습니다(이 파일의 '없음' 판정 전부 무효).");
            Assert.IsTrue(movedFlag,
                "옮긴 직후인데 파일의 gearPositionSaved가 false입니다 — 양성 대조 실패.");

            // ---- 본 검증 ----
            UiLayoutModel.ClearGearCenter();
            Assert.IsTrue(CharacterSaveStore.Save());
            string clearedJson = File.ReadAllText(CharacterSaveStore.FilePath);

            Assert.IsTrue(GearSavedFlagInJson(clearedJson, out bool clearedFlag),
                "되돌린 뒤 저장 파일에서 gearPositionSaved 키가 사라졌습니다 — 스키마가 깨졌습니다.");
            Assert.IsFalse(clearedFlag,
                "되돌렸는데 파일에는 여전히 gearPositionSaved=true가 적혀 있습니다.");

            Assert.IsTrue(VersionInJson(clearedJson, out int version),
                "저장 파일에서 version 키를 찾지 못했습니다.");
            Assert.AreEqual(CharacterSaveStore.CurrentVersion, version,
                "되돌리기가 스키마 버전을 건드렸습니다 — 이건 새 필드가 아니라 기존 필드의 값 변경이라 " +
                "버전을 올릴 이유가 없습니다(CharacterSaveStore.CurrentVersion 주석의 규칙).");
        }

        /// <summary>이미 기본 위치면 아무것도 하지 않는다 — 하루 종일 켜져 있는 앱에서 "할 일 없는 저장"이
        /// 디스크를 두드리지 않게 하는 계약이다(SetGearCenter의 MeaningfulMovePoints와 같은 정신).</summary>
        [Test]
        public void 이미_기본_위치면_되돌리기가_아무것도_하지_않는다()
        {
            Assert.IsFalse(UiLayoutModel.HasGearCenter, "이 테스트는 '옮긴 적 없음'에서 시작해야 합니다.");
            UiLayoutModel.MarkSaved();

            Assert.IsFalse(UiLayoutModel.ClearGearCenter(),
                "되돌릴 것이 없는데 '되돌렸다'(true)를 돌려줬습니다 — 호출부가 무의미한 저장을 합니다.");
            Assert.IsFalse(UiLayoutModel.IsDirty,
                "아무것도 안 바뀌었는데 저장 대상이 됐습니다.");

            // 양성 대조 — 같은 메서드가 되돌릴 것이 있을 때는 true를 돌려준다(위 IsFalse가 '항상 false'가 아니다).
            UiLayoutModel.SetGearCenter(new Vector2(77f, 88f));
            Assert.IsTrue(UiLayoutModel.ClearGearCenter(),
                "양성 대조 실패 — ClearGearCenter가 어떤 경우에도 false를 돌려줍니다.");
        }

        /// <summary><c>"gearPositionSaved": true</c> 를 공백/줄바꿈에 상관없이 읽는다. JsonUtility의
        /// 들여쓰기 형식에 테스트가 묶이지 않게 정규식으로 푼다.</summary>
        private static bool GearSavedFlagInJson(string json, out bool value)
        {
            value = false;
            Match m = Regex.Match(json, @"""gearPositionSaved""\s*:\s*(true|false)");
            if (!m.Success) return false;
            value = m.Groups[1].Value == "true";
            return true;
        }

        private static bool VersionInJson(string json, out int value)
        {
            value = 0;
            Match m = Regex.Match(json, @"""version""\s*:\s*(-?\d+)");
            return m.Success && int.TryParse(m.Groups[1].Value, out value);
        }

        [Test]
        public void NaN_좌표는_무시된다()
        {
            UiLayoutModel.SetGearCenter(new Vector2(50f, 60f));
            UiLayoutModel.SetGearCenter(new Vector2(float.NaN, 60f));

            Assert.AreEqual(50f, UiLayoutModel.GearCenterPoints.x, 0.001f,
                "NaN 좌표가 들어와 저장값을 오염시켰습니다 — 한 번 NaN이 되면 아이콘이 영영 사라집니다.");
        }
    }
}
