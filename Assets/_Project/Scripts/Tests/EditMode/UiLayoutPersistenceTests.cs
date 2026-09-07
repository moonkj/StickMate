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

        // ====================================================================================
        // ★★ 창 3종의 자리 (2026-09-07 사용자 요청 PART1-1)
        // ====================================================================================
        //
        // 사용자 원문: "모든 창(집중모드 타이머, 캐릭터 정보창, 설정창)이 마우스로 끌어도 움직이지
        // 않음 — 전부 드래그 이동 가능해야 함" · "이동한 위치는 창별로 저장되어 재시작 후에도 유지".
        //
        // 이 절이 지키는 것은 톱니와 <b>같은 두 조건</b>이다(위 파일 머리):
        //   ① 옮긴 좌표가 저장 → 로드 왕복에서 살아남는다.
        //   ② 구버전 파일이 그대로 읽히고, 새 필드가 없으면 «옮긴 적 없음»이 된다.
        // 그리고 창은 <b>셋</b>이라 하나가 더 붙는다:
        //   ③ ★ <b>서로 섞이지 않는다</b> — 세 창에 서로 다른 값을 넣고 각각을 확인한다.
        //      저장 스키마가 이름 붙은 필드 9개라 <b>복붙 한 줄이 어긋나면</b> 정보창 자리가 설정창
        //      필드에 앉는다. 그 사고는 «저장은 되는데 엉뚱한 창이 움직인다»로 나타나고,
        //      한 창만 검사하는 테스트는 그것을 <b>구조적으로 못 본다</b>.

        /// <summary>enum을 <b>배열 리터럴로 다시 적지 않는다</b> — 창이 하나 늘면 이 목록도 함께 는다.
        /// (손으로 적으면 «새 창만 검사에서 빠진다»가 조용히 생긴다.)</summary>
        private static UiWindowId[] AllWindows()
            => (UiWindowId[])System.Enum.GetValues(typeof(UiWindowId));

        [Test]
        public void 창_개수_상수가_enum과_같다()
        {
            // 이 단언이 깨지면 아래 모든 루프가 «일부만 검사»가 된다 — 그러면서도 초록이다.
            Assert.AreEqual(AllWindows().Length, UiLayoutModel.WindowCount,
                "UiWindowId의 값 개수와 UiLayoutModel.WindowCount가 갈라졌습니다 — " +
                "모델의 배열이 새 창을 담지 못하거나, 아래 회귀 테스트들이 그 창을 건너뜁니다.");
        }

        [Test]
        public void 옮긴_창_위치가_저장하고_다시_불러온_뒤에도_같다_그리고_서로_섞이지_않는다()
        {
            UiWindowId[] windows = AllWindows();
            Assert.Greater(windows.Length, 1,
                "창이 하나뿐이면 '서로 섞이지 않는다'를 잴 수 없습니다 — 이 테스트는 공허해집니다.");

            var expected = new Vector2[windows.Length];
            for (int i = 0; i < windows.Length; i++)
            {
                Assert.IsFalse(UiLayoutModel.HasWindowOffset(windows[i]),
                    $"초기 상태는 '옮긴 적 없음'이어야 합니다({windows[i]}).");
                // 창마다 <b>다른</b> 값 — 필드가 서로 뒤바뀌면 아래 비교에서 반드시 갈린다.
                expected[i] = new Vector2(101f + i * 37f, -53f - i * 29f);
                UiLayoutModel.SetWindowOffset(windows[i], expected[i]);
            }

            Assert.IsTrue(UiLayoutModel.IsDirty, "창을 옮겼는데 저장 대상으로 표시되지 않았습니다.");
            Assert.IsTrue(CharacterSaveStore.Save(), "저장에 실패했습니다.");
            Assert.IsFalse(UiLayoutModel.IsDirty, "저장했는데 여전히 저장 대상으로 남아 있습니다.");

            UiLayoutModel.ResetForTesting();
            CharacterSaveStore.Load();

            for (int i = 0; i < windows.Length; i++)
            {
                Assert.IsTrue(UiLayoutModel.HasWindowOffset(windows[i]),
                    $"{windows[i]} 창의 자리가 복원되지 않았습니다 — 재시작하면 기본 자리로 돌아가 버립니다.");
                Vector2 got = UiLayoutModel.WindowOffsetPoints(windows[i]);
                Assert.AreEqual(expected[i].x, got.x, 0.01f,
                    $"{windows[i]} 창의 x가 어긋났습니다(기대 {expected[i]}, 실제 {got}) — " +
                    "저장 필드가 다른 창의 것과 뒤바뀌었을 수 있습니다.");
                Assert.AreEqual(expected[i].y, got.y, 0.01f,
                    $"{windows[i]} 창의 y가 어긋났습니다(기대 {expected[i]}, 실제 {got}).");
            }
            Assert.IsFalse(UiLayoutModel.IsDirty, "복원 직후는 저장 대상이 아니어야 합니다(복원은 변화가 아니다).");
        }

        [Test]
        public void 구버전_v2_저장_파일은_창을_옮긴_적_없음으로_읽힌다()
        {
            // v2에는 창 위치 필드가 아예 없다 — JsonUtility가 false/0으로 채우고, 그 false가
            // "아직 옮긴 적 없다 = 화면 중앙에서 연다"는 정확한 사실이다(0,0으로 튀는 것과 결과가
            // 같아 보이지만 뜻이 다르다 — 플래그가 false라야 «앵커 배치»를 쓰는 팝오버가 옳게 뜬다).
            const string V2Json =
                "{\n" +
                "    \"version\": 2,\n" +
                "    \"level\": 5,\n" +
                "    \"currentXp\": 1.0,\n" +
                "    \"totalXpEarned\": 100.0,\n" +
                "    \"characterName\": \"창옛동료\"\n" +
                "}";
            File.WriteAllText(CharacterSaveStore.FilePath, V2Json);

            CharacterSaveStore.Load();

            Assert.IsTrue(CharacterSaveStore.LoadedFromFile, "구버전 파일을 읽지 못했습니다.");
            foreach (UiWindowId id in AllWindows())
            {
                Assert.IsFalse(UiLayoutModel.HasWindowOffset(id),
                    $"구버전 파일에는 {id} 창의 자리가 없으므로 '옮긴 적 없음'이어야 합니다.");
                Assert.AreEqual(Vector2.zero, UiLayoutModel.WindowOffsetPoints(id),
                    $"'옮긴 적 없음'인데 {id}의 좌표가 채워져 있습니다 — 플래그와 좌표가 어긋났습니다.");
            }
        }

        /// <summary>
        /// ★ <b>디스크에 실제로 무엇이 쓰였는가</b> — 위 왕복 테스트와 <b>다른 방법</b>으로 다시 잰다
        /// (그쪽은 <c>Load()</c>라는 우리 코드를 통해 봤다. 여기서는 JSON 원문을 직접 읽는다).
        ///
        /// <para>그리고 <b>스키마 버전이 안 올라갔다</b>는 것을 여기서 잠근다. 창 위치 9필드는
        /// <c>CharacterSaveStore.CurrentVersion</c> 주석의 규칙 — <i>"필드의 「없음」이 그 필드의
        /// 0값과 다른 뜻일 때만 버전을 강제한다"</i> — 에서 «같은 뜻»(없음 = false = 안 옮김)이라
        /// 버전을 올릴 이유가 없다. 숫자를 베끼지 않고 상수를 참조한다.</para>
        /// </summary>
        [Test]
        public void 창_위치는_새_필드지만_스키마_버전을_올리지_않는다()
        {
            // ---- 양성 대조: 같은 방법(원문 스캔)이 true도 실제로 잡아내는가 ----
            UiLayoutModel.SetWindowOffset(UiWindowId.Settings, new Vector2(88f, -12f));
            Assert.IsTrue(CharacterSaveStore.Save());
            string json = File.ReadAllText(CharacterSaveStore.FilePath);

            Assert.IsTrue(BoolInJson(json, "settingsWindowPositionSaved", out bool movedFlag),
                "저장 파일에서 settingsWindowPositionSaved 키를 찾지 못했습니다 — 스캐너가 죽었습니다" +
                "(이 파일의 '없음' 판정 전부 무효).");
            Assert.IsTrue(movedFlag, "옮긴 직후인데 파일의 settingsWindowPositionSaved가 false입니다 — 양성 대조 실패.");

            // ---- 본 검증 ① 세 창의 키가 전부 파일에 있다(하나만 있어도 위 양성 대조는 통과한다) ----
            foreach (string key in new[]
                     { "infoWindowPositionSaved", "settingsWindowPositionSaved", "focusPopoverPositionSaved" })
            {
                Assert.IsTrue(BoolInJson(json, key, out _), $"저장 파일에 {key} 키가 없습니다 — 그 창은 자리를 못 남깁니다.");
            }

            // ---- 본 검증 ② 버전은 그대로 ----
            Assert.IsTrue(VersionInJson(json, out int version), "저장 파일에서 version 키를 찾지 못했습니다.");
            Assert.AreEqual(CharacterSaveStore.CurrentVersion, version,
                "창 위치 필드가 스키마 버전을 올렸습니다 — 이 필드들의 「없음」은 0값과 <b>같은 뜻</b>이라" +
                "(없음 = false = 아직 안 옮김) 버전을 강제하지 않습니다" +
                "(CharacterSaveStore.CurrentVersion 주석의 규칙). 정말 올려야 한다면 v(N-1) 하위 호환 " +
                "테스트를 동반해야 합니다(CLAUDE.md).");
        }

        [Test]
        public void 창을_같은_자리로_다시_세팅하면_저장_대상이_되지_않는다()
        {
            // 창들은 매 프레임 클램프 결과를 모델로 되돌려 주므로(화면 경계 보정), 같은 값 재세팅이
            // IsDirty를 세우면 주기 저장이 무의미하게 디스크를 두드린다(톱니와 같은 사정).
            UiLayoutModel.SetWindowOffset(UiWindowId.FocusSession, new Vector2(30f, 40f));
            UiLayoutModel.MarkSaved();

            UiLayoutModel.SetWindowOffset(UiWindowId.FocusSession, new Vector2(30f, 40f));
            Assert.IsFalse(UiLayoutModel.IsDirty, "같은 자리를 다시 세팅했는데 저장 대상이 되었습니다.");

            UiLayoutModel.SetWindowOffset(UiWindowId.FocusSession, new Vector2(30f, 45f));
            Assert.IsTrue(UiLayoutModel.IsDirty, "실제로 옮겼는데 저장 대상이 되지 않았습니다.");
        }

        [Test]
        public void 창_위치의_NaN은_무시된다()
        {
            UiLayoutModel.SetWindowOffset(UiWindowId.CharacterInfo, new Vector2(50f, 60f));
            UiLayoutModel.SetWindowOffset(UiWindowId.CharacterInfo, new Vector2(float.NaN, 60f));

            Assert.AreEqual(50f, UiLayoutModel.WindowOffsetPoints(UiWindowId.CharacterInfo).x, 0.001f,
                "NaN 좌표가 들어와 저장값을 오염시켰습니다 — 한 번 NaN이 되면 창이 영영 사라집니다.");
        }

        [Test]
        public void 창_되돌리기는_되돌릴_것이_있을_때만_참을_돌려준다()
        {
            Assert.IsFalse(UiLayoutModel.ClearWindowOffset(UiWindowId.Settings),
                "되돌릴 것이 없는데 '되돌렸다'(true)를 돌려줬습니다 — 호출부가 무의미한 저장을 합니다.");
            Assert.IsFalse(UiLayoutModel.IsDirty, "아무것도 안 바뀌었는데 저장 대상이 됐습니다.");

            // 양성 대조 — 같은 메서드가 되돌릴 것이 있을 때는 true다(위 IsFalse가 '항상 false'가 아니다).
            UiLayoutModel.SetWindowOffset(UiWindowId.Settings, new Vector2(11f, 22f));
            Assert.IsTrue(UiLayoutModel.ClearWindowOffset(UiWindowId.Settings),
                "양성 대조 실패 — ClearWindowOffset이 어떤 경우에도 false를 돌려줍니다.");
            Assert.IsFalse(UiLayoutModel.HasWindowOffset(UiWindowId.Settings));
            Assert.AreEqual(Vector2.zero, UiLayoutModel.WindowOffsetPoints(UiWindowId.Settings),
                "플래그만 내리고 좌표가 남아 있습니다 — 다음 사람이 '값이 있으니 쓰겠다'고 읽습니다.");
        }

        [Test]
        public void 한_창을_되돌려도_다른_창의_자리는_남는다()
        {
            UiLayoutModel.SetWindowOffset(UiWindowId.CharacterInfo, new Vector2(60f, 70f));
            UiLayoutModel.SetWindowOffset(UiWindowId.Settings, new Vector2(-40f, 15f));

            UiLayoutModel.ClearWindowOffset(UiWindowId.Settings);

            Assert.IsTrue(UiLayoutModel.HasWindowOffset(UiWindowId.CharacterInfo),
                "설정창 하나를 되돌렸는데 정보창의 자리까지 사라졌습니다 — 배열 색인이 어긋났습니다.");
            Assert.AreEqual(new Vector2(60f, 70f), UiLayoutModel.WindowOffsetPoints(UiWindowId.CharacterInfo));
        }

        /// <summary><c>"키": true</c>를 공백/줄바꿈에 상관없이 읽는다(위 GearSavedFlagInJson과 같은 방법).</summary>
        private static bool BoolInJson(string json, string key, out bool value)
        {
            value = false;
            Match m = Regex.Match(json, "\"" + Regex.Escape(key) + @"""\s*:\s*(true|false)");
            if (!m.Success) return false;
            value = m.Groups[1].Value == "true";
            return true;
        }
    }
}
