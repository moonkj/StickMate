using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 성장(레벨/XP/이름) + 장비 상태의 <b>영속화</b> 회귀 테스트 — 2026-08-29 성장/장비 라운드.
    ///
    /// ============================================================================
    /// 무엇을 잡으려는가
    /// ============================================================================
    /// "며칠 켜두면 레벨이 오른다"는 기능은 <b>저장이 실제로 되는지</b>가 전부다. 저장이 조용히 실패하면
    /// 사용자는 매번 Lv.1로 돌아오고, 그 사실을 알아차릴 때쯤에는 이미 며칠을 잃은 뒤다. 그런데 이 실패는
    /// 예외도 경고도 없이 일어난다(파일이 없으면 그냥 기본값으로 시작하는 것이 정상 동작이기 때문에).
    /// 그래서 세 가지를 못박는다:
    ///   (1) 저장 -> 초기화 -> 로드 왕복에서 레벨/XP/이름/장비 4종이 그대로 복원된다.
    ///   (2) <b>파일을 지우면 기본값(Lv.1 / 미착용)으로 시작한다</b>(리더 지시의 명시 항목).
    ///   (3) XP 곡선과 패시브 적립률이 설계 목표("초반 레벨업 1~3시간") 안에 있다.
    ///
    /// ============================================================================
    /// 이 테스트가 건드리는 파일 — 불변 원칙 3과 무관하다
    /// ============================================================================
    /// 쓰는 대상은 <c>Application.persistentDataPath</c> 아래의 이 앱 자신의 저장 파일 하나뿐이다
    /// (Core/CharacterSaveStore.cs 클래스 문서 참고). 다만 개발 중인 이 머신에서는 <b>실행 중인 앱의
    /// 진짜 저장 파일</b>과 같은 경로이므로, 테스트가 사용자의 실제 진행도를 날려버리지 않도록
    /// OneTimeSetUp에서 내용을 통째로 백업하고 OneTimeTearDown에서 되돌린다.
    /// </summary>
    public class CharacterProgressionPersistenceTests
    {
        private const string DefaultConfigPath = "Assets/_Project/Data/DefaultStickConfig.asset";

        private string _backup;
        private bool _hadFile;

        private static StickConfig LoadDefaultConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<StickConfig>(DefaultConfigPath);
            Assert.IsNotNull(config, $"기본 설정 자산을 찾지 못했습니다: {DefaultConfigPath}");
            return config;
        }

        [OneTimeSetUp]
        public void BackupRealSaveFile()
        {
            string path = CharacterSaveStore.FilePath;
            _hadFile = File.Exists(path);
            _backup = _hadFile ? File.ReadAllText(path) : null;
        }

        /// <summary>이 앱 자신의 저장 파일(<see cref="CharacterSaveStore.FilePath"/>)만 지운다 —
        /// 경로가 persistentDataPath + 고정 파일명으로 못박혀 있어 다른 것을 지울 수 없다.</summary>
        private static void DeleteSaveFile()
        {
            string path = CharacterSaveStore.FilePath;
            if (File.Exists(path)) File.Delete(path);
        }

        [OneTimeTearDown]
        public void RestoreRealSaveFile()
        {
            if (_hadFile) File.WriteAllText(CharacterSaveStore.FilePath, _backup);
            else DeleteSaveFile();

            CharacterProgressionModel.ResetForTesting();
            EquipmentModel.ResetForTesting();
        }

        [SetUp]
        public void ResetModels()
        {
            CharacterProgressionModel.ResetForTesting();
            EquipmentModel.ResetForTesting();
        }

        // ============================================================================
        // (1) 저장 -> 로드 왕복
        // ============================================================================

        [Test]
        public void 저장한_레벨_XP_이름_장비가_다시_불러온_뒤에도_같다()
        {
            StickConfig config = LoadDefaultConfig();

            // 가장 늦게 열리는 슬롯(망토)까지 실제로 착용해 보려면 그 해제 레벨을 넘겨야 한다 —
            // 잠긴 슬롯은 TryToggle이 거부하므로(그게 정상), 여기서 레벨을 충분히 올려 둔다.
            int topUnlock = 0;
            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                topUnlock = Mathf.Max(topUnlock, EquipmentModel.UnlockLevel((EquipmentSlot)i, config));
            }
            int targetLevel = topUnlock + 1;

            // 연속 레벨업 이월(한 번의 AddXp로 여러 레벨)도 함께 검증한다.
            float bulk = 0f;
            for (int lv = 1; lv < targetLevel; lv++) bulk += CharacterProgressionModel.XpToNextLevel(lv, config);
            CharacterProgressionModel.AddXp(bulk + 37f, config);
            CharacterProgressionModel.SetCharacterName("책상동료");
            EquipmentModel.TryToggle(EquipmentSlot.Head, config);
            EquipmentModel.TryToggle(EquipmentSlot.Shoulders, config);

            int level = CharacterProgressionModel.Level;
            float xp = CharacterProgressionModel.CurrentXp;
            float total = CharacterProgressionModel.TotalXpEarned;

            Assert.AreEqual(targetLevel, level,
                $"{targetLevel - 1}레벨 분량 + 여분을 한 번에 넣었으므로 Lv.{targetLevel}이어야 합니다(연속 레벨업 이월).");
            Assert.AreEqual(37f, xp, 0.01f, "레벨업 후 남은 XP가 이월되지 않고 버려졌습니다.");
            Assert.IsTrue(CharacterSaveStore.Save(), "저장에 실패했습니다.");

            // 프로세스를 새로 켠 것과 같은 상태로 만든 뒤 로드.
            CharacterProgressionModel.ResetForTesting();
            EquipmentModel.ResetForTesting();
            Assert.AreEqual(1, CharacterProgressionModel.Level, "초기화가 되지 않았습니다 — 아래 로드 검증이 무의미해집니다.");

            CharacterSaveStore.Load();

            Assert.IsTrue(CharacterSaveStore.LoadedFromFile, "저장 파일에서 값을 읽지 못했습니다.");
            Assert.AreEqual(level, CharacterProgressionModel.Level, "레벨이 복원되지 않았습니다.");
            Assert.AreEqual(xp, CharacterProgressionModel.CurrentXp, 0.01f, "현재 XP가 복원되지 않았습니다.");
            Assert.AreEqual(total, CharacterProgressionModel.TotalXpEarned, 0.01f, "누적 XP가 복원되지 않았습니다.");
            Assert.AreEqual("책상동료", CharacterProgressionModel.CharacterName, "이름이 복원되지 않았습니다.");
            Assert.IsTrue(EquipmentModel.IsEquipped(EquipmentSlot.Head), "모자 착용 상태가 복원되지 않았습니다.");
            Assert.IsTrue(EquipmentModel.IsEquipped(EquipmentSlot.Shoulders), "망토 착용 상태가 복원되지 않았습니다.");
            Assert.IsFalse(EquipmentModel.IsEquipped(EquipmentSlot.Eyes), "착용하지 않은 선글라스가 착용 상태로 복원됐습니다.");
            Assert.IsFalse(EquipmentModel.IsEquipped(EquipmentSlot.Neck), "착용하지 않은 나비넥타이가 착용 상태로 복원됐습니다.");
        }

        // ============================================================================
        // (2) 파일 삭제 -> 기본값으로 시작 (리더 지시 명시 항목)
        // ============================================================================

        [Test]
        public void 저장_파일을_지우면_기본값으로_시작한다()
        {
            StickConfig config = LoadDefaultConfig();
            CharacterProgressionModel.AddXp(5000f, config);
            CharacterProgressionModel.SetCharacterName("지워질이름");
            EquipmentModel.TryToggle(EquipmentSlot.Head, config);
            CharacterSaveStore.Save();

            // ★ 파일 삭제는 **테스트가 직접** 한다 — 프로덕션 코드(Core/CharacterSaveStore.cs)에는
            //   파일을 지우는 코드가 한 줄도 없어야 하기 때문이다(원칙 3 정적 감사
            //   UserAssetImmutabilityAuditTests가 File.Delete를 프로덕션 소스에서 전면 금지한다).
            //   대상은 이 앱 자신의 저장 파일 하나뿐이고, 그 내용은 OneTimeSetUp이 백업해 두었다.
            DeleteSaveFile();
            Assert.IsFalse(File.Exists(CharacterSaveStore.FilePath), "저장 파일이 실제로 지워지지 않았습니다.");
            CharacterSaveStore.MarkNotLoadedForTesting();

            CharacterProgressionModel.ResetForTesting();
            EquipmentModel.ResetForTesting();
            CharacterSaveStore.Load();

            Assert.IsFalse(CharacterSaveStore.LoadedFromFile, "파일이 없는데 '불러왔다'고 보고했습니다.");
            Assert.AreEqual(1, CharacterProgressionModel.Level, "파일이 없으면 Lv.1로 시작해야 합니다.");
            Assert.AreEqual(0f, CharacterProgressionModel.CurrentXp, 0.001f, "파일이 없으면 XP 0으로 시작해야 합니다.");
            Assert.AreEqual(CharacterProgressionModel.DefaultCharacterName, CharacterProgressionModel.CharacterName,
                "파일이 없으면 기본 이름으로 시작해야 합니다.");
            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                Assert.IsFalse(EquipmentModel.IsEquipped((EquipmentSlot)i),
                    $"파일이 없는데 {EquipmentModel.ItemName((EquipmentSlot)i)}가 착용 상태입니다.");
            }
        }

        [Test]
        public void 손상된_저장_파일은_기본값으로_떨어지고_크래시하지_않는다()
        {
            File.WriteAllText(CharacterSaveStore.FilePath, "{ this is not valid json ]]]");

            CharacterProgressionModel.ResetForTesting();
            EquipmentModel.ResetForTesting();
            LogAssert.ignoreFailingMessages = true;   // 이 경로는 경고 로그를 내는 것이 정상 동작이다.
            CharacterSaveStore.Load();
            LogAssert.ignoreFailingMessages = false;

            Assert.AreEqual(1, CharacterProgressionModel.Level,
                "손상된 파일을 만나면 기본값(Lv.1)으로 시작해야 합니다 — 예외가 밖으로 던져지면 앱이 죽습니다.");
        }

        // ============================================================================
        // (3) 잠금 규칙
        // ============================================================================

        [Test]
        public void 잠긴_슬롯은_착용되지_않는다()
        {
            StickConfig config = LoadDefaultConfig();
            Assert.AreEqual(1, CharacterProgressionModel.Level, "전제: Lv.1에서 시작.");

            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                var slot = (EquipmentSlot)i;
                Assert.Greater(EquipmentModel.UnlockLevel(slot, config), 1,
                    $"{EquipmentModel.ItemName(slot)}의 해제 레벨이 1이면 '레벨업으로 열린다'는 설계가 성립하지 않습니다.");
                Assert.IsFalse(EquipmentModel.TryToggle(slot, config),
                    $"Lv.1인데 {EquipmentModel.ItemName(slot)}가 착용됐습니다 — 잠금이 뚫렸습니다.");
                Assert.IsFalse(EquipmentModel.IsEquipped(slot), $"{EquipmentModel.ItemName(slot)}가 착용 상태입니다.");
            }
        }

        [Test]
        public void 해제_레벨은_슬롯_순서대로_점점_높아진다()
        {
            StickConfig config = LoadDefaultConfig();
            int prev = 0;
            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                int need = EquipmentModel.UnlockLevel((EquipmentSlot)i, config);
                Assert.Greater(need, prev,
                    $"{EquipmentModel.ItemName((EquipmentSlot)i)}의 해제 레벨({need})이 앞 슬롯({prev}) 이하입니다 — " +
                    "'며칠에 걸쳐 하나씩 열린다'는 리듬이 깨집니다.");
                prev = need;
            }
        }

        // ============================================================================
        // (4) XP 곡선이 설계 목표 안에 있는가
        // ============================================================================

        [Test]
        public void XP_곡선은_레벨이_오를수록_단조증가한다()
        {
            StickConfig config = LoadDefaultConfig();
            float prev = 0f;
            for (int lv = 1; lv <= 20; lv++)
            {
                float need = CharacterProgressionModel.XpToNextLevel(lv, config);
                Assert.Greater(need, prev, $"Lv.{lv}의 필요 XP({need:F1})가 이전 레벨({prev:F1}) 이하입니다.");
                prev = need;
            }
        }

        [Test]
        public void 패시브만으로_초반_레벨업이_1시간에서_3시간_사이다()
        {
            StickConfig config = LoadDefaultConfig();
            float perHour = config.progressionPassiveXpPerMinute * 60f;
            Assert.Greater(perHour, 0f, "패시브 XP가 0이면 '아무것도 안 해도 자란다'는 철학이 성립하지 않습니다.");

            float hoursTo2 = CharacterProgressionModel.XpToNextLevel(1, config) / perHour;
            Assert.IsTrue(hoursTo2 >= 0.8f && hoursTo2 <= 3f,
                $"Lv.1 -> Lv.2가 패시브만으로 {hoursTo2:F2}시간입니다 — 리더 목표는 1~3시간입니다.");

            float hoursTo3 = CharacterProgressionModel.XpToNextLevel(2, config) / perHour;
            Assert.Greater(hoursTo3, hoursTo2,
                $"Lv.2 -> Lv.3({hoursTo3:F2}시간)이 Lv.1 -> Lv.2({hoursTo2:F2}시간)보다 빠릅니다 — 곡선이 뒤집혔습니다.");

            // 첫 장비(모자)까지 걸리는 시간 — "며칠 안에 하나씩"의 첫 단추.
            int headUnlock = EquipmentModel.UnlockLevel(EquipmentSlot.Head, config);
            float cumulative = 0f;
            for (int lv = 1; lv < headUnlock; lv++) cumulative += CharacterProgressionModel.XpToNextLevel(lv, config);
            float hoursToHat = cumulative / perHour;
            Assert.Less(hoursToHat, 8f,
                $"첫 장비(모자, Lv.{headUnlock})까지 패시브만으로 {hoursToHat:F1}시간입니다 — 하루 안에는 열려야 합니다.");
        }

        [Test]
        public void 이름은_공백으로_비울_수_없고_길이_상한이_있다()
        {
            CharacterProgressionModel.SetCharacterName("   ");
            Assert.AreEqual(CharacterProgressionModel.DefaultCharacterName, CharacterProgressionModel.CharacterName,
                "공백만 입력하면 기본 이름으로 되돌아가야 합니다(이름이 사라진 상태를 만들지 않는다).");

            CharacterProgressionModel.SetCharacterName(new string('가', CharacterProgressionModel.MaxNameLength + 20));
            Assert.AreEqual(CharacterProgressionModel.MaxNameLength, CharacterProgressionModel.CharacterName.Length,
                "이름 길이 상한이 적용되지 않아 창 밖으로 넘칠 수 있습니다.");
        }
    }
}
