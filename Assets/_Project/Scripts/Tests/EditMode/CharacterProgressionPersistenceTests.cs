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
    ///   (1) 저장 -> 초기화 -> 로드 왕복에서 레벨/XP/이름과 <b>카테고리별로 고른 아이템</b>이 그대로 복원된다.
    ///   (2) <b>파일을 지우면 기본값(Lv.1 / 기본 차림)으로 시작한다</b>(리더 지시의 명시 항목).
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

            // 가장 늦게 열리는 아이템(커서 친구, req24)까지 실제로 착용해 보려면 그 요구 레벨을 넘겨야
            // 한다 — 잠긴 아이템은 TryWear가 거부하므로(그게 정상), 여기서 레벨을 충분히 올려 둔다.
            int topUnlock = 0;
            for (int s = 0; s < EquipmentModel.SlotCount; s++)
            {
                var slot = (EquipmentSlot)s;
                for (int i = 0; i < EquipmentModel.ItemCount(slot); i++)
                {
                    topUnlock = Mathf.Max(topUnlock, EquipmentModel.RequiredLevel(slot, i));
                }
            }
            int targetLevel = topUnlock + 1;

            // 연속 레벨업 이월(한 번의 AddXp로 여러 레벨)도 함께 검증한다.
            float bulk = 0f;
            for (int lv = 1; lv < targetLevel; lv++) bulk += CharacterProgressionModel.XpToNextLevel(lv, config);
            CharacterProgressionModel.AddXp(bulk + 37f, config);
            CharacterProgressionModel.SetCharacterName("책상동료");

            // 기본 아이템이 아닌 것을 골라야 "인덱스가 그대로 복원되는가"가 실제로 검증된다 —
            // 0번만 걸치면 옛 bool 저장으로도 통과해 버린다.
            Assert.IsTrue(EquipmentModel.TryWear(EquipmentSlot.Head, 3, config), "왕관을 걸치지 못했습니다.");
            Assert.IsTrue(EquipmentModel.TryWear(EquipmentSlot.Pet, 3, config), "커서 친구를 걸치지 못했습니다.");
            Assert.IsTrue(EquipmentModel.TryWear(EquipmentSlot.Eyes, EquipmentModel.NotWorn, config),
                "기본으로 걸치고 있던 선글라스를 벗지 못했습니다.");

            int level = CharacterProgressionModel.Level;
            float xp = CharacterProgressionModel.CurrentXp;
            float total = CharacterProgressionModel.TotalXpEarned;

            Assert.AreEqual(targetLevel, level,
                $"{targetLevel - 1}레벨 분량 + 여분을 한 번에 넣었으므로 Lv.{targetLevel}이어야 합니다(연속 레벨업 이월).");
            // 허용 오차 0.01 -> 0.1: 32종 확장으로 목표 레벨이 9에서 25로 올라가며 누적 XP가 4천대에서
            // 4만대가 됐다. float32의 눈금이 그 크기에서 약 0.004라 24번의 덧셈/뺄셈이 순서만 달라도
            // 0.01을 넘길 수 있다(실측 잔차 약 0.002). 검증하려는 것은 "37이 버려지지 않았다"이지
            // 부동소수점 재현이 아니다.
            Assert.AreEqual(37f, xp, 0.1f, "레벨업 후 남은 XP가 이월되지 않고 버려졌습니다.");
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
            Assert.AreEqual(3, EquipmentModel.WornIndex(EquipmentSlot.Head),
                "모자 카테고리에서 고른 아이템(왕관)이 복원되지 않았습니다 — 착용 여부만 남고 '무엇을' 골랐는지가 사라졌습니다.");
            Assert.AreEqual(3, EquipmentModel.WornIndex(EquipmentSlot.Pet), "펫(커서 친구)이 복원되지 않았습니다.");
            Assert.AreEqual(EquipmentModel.NotWorn, EquipmentModel.WornIndex(EquipmentSlot.Eyes),
                "벗어 둔 안경이 착용 상태로 복원됐습니다.");
            Assert.IsFalse(EquipmentModel.IsEquipped(EquipmentSlot.Neck), "착용하지 않은 넥타이가 착용 상태로 복원됐습니다.");
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
            // ★ 2026-08-30 32종 확장 — "파일이 없으면 아무것도 안 걸친다"에서 "핸드오프가 정한
            //   기본 차림(모자=천모자, 안경=선글라스, 나머지 6종 미착용)으로 시작한다"로 바뀌었다.
            //   검증하려는 의도는 그대로다: 파일이 없을 때의 차림은 <b>정해진 하나</b>여야 하고,
            //   직전 세션의 잔재가 남아서는 안 된다.
            Assert.AreEqual(0, EquipmentModel.WornIndex(EquipmentSlot.Head), "새 캐릭터는 천모자를 쓰고 시작합니다.");
            Assert.AreEqual(0, EquipmentModel.WornIndex(EquipmentSlot.Eyes), "새 캐릭터는 선글라스를 쓰고 시작합니다.");
            for (int i = (int)EquipmentSlot.Neck; i < EquipmentModel.SlotCount; i++)
            {
                Assert.AreEqual(EquipmentModel.NotWorn, EquipmentModel.WornIndex((EquipmentSlot)i),
                    $"파일이 없는데 [{EquipmentModel.SlotName((EquipmentSlot)i)}]에 뭔가 걸쳐져 있습니다.");
            }
        }

        [Test]
        public void 손상된_저장_파일은_기본값으로_떨어지고_크래시하지_않는다()
        {
            // ★ 2026-09-02 — 이 테스트가 재는 것은 "복구원이 하나도 없을 때 기본값으로 떨어지는가"다.
            //   직전 세대(stickmate_character.prev.json)가 남아 있으면 새 복구 경로가 그것을 집으므로,
            //   전제를 파일로 만들어 둔다(EditMode 임시 폴더는 전 픽스처가 공유한다).
            if (File.Exists(CharacterSaveStore.PreviousGenerationPath))
                File.Delete(CharacterSaveStore.PreviousGenerationPath);
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
        public void 잠긴_아이템은_착용되지_않는다()
        {
            StickConfig config = LoadDefaultConfig();
            Assert.AreEqual(1, CharacterProgressionModel.Level, "전제: Lv.1에서 시작.");

            // ★ 2026-08-30 32종 확장 — 잠금의 단위가 카테고리에서 <b>아이템</b>으로 내려왔다.
            //   각 카테고리의 0번은 처음부터 보유이고(그래야 Lv.1 사용자에게 빈 칸이 없다),
            //   요구 레벨이 붙은 아이템은 여전히 레벨만이 유일한 해제 경로다.
            for (int s = 0; s < EquipmentModel.SlotCount; s++)
            {
                var slot = (EquipmentSlot)s;
                for (int i = 0; i < EquipmentModel.ItemCount(slot); i++)
                {
                    if (EquipmentModel.RequiredLevel(slot, i) <= 1) continue;

                    Assert.IsFalse(EquipmentModel.TryWear(slot, i, config),
                        $"Lv.1인데 [{EquipmentModel.ItemName(slot, i)}](Lv.{EquipmentModel.RequiredLevel(slot, i)} 필요)가 " +
                        "착용됐습니다 — 잠금이 뚫렸습니다.");
                    Assert.IsFalse(EquipmentModel.IsEquipped(slot, i),
                        $"[{EquipmentModel.ItemName(slot, i)}]가 착용 상태입니다.");
                }
            }
        }

        [Test]
        public void 요구_레벨은_카테고리_안에서_점점_높아지고_전체적으로_퍼져_있다()
        {
            // "며칠에 걸쳐 하나씩 열린다"는 리듬은 이제 카테고리 순서가 아니라 <b>아이템 요구 레벨의
            // 분포</b>가 만든다. 한 레벨에 여러 개가 몰려 열리면 그 뒤로는 며칠 동안 아무 일도 없다.
            var opened = new System.Collections.Generic.Dictionary<int, int>();
            int maxRequired = 0;

            for (int s = 0; s < EquipmentModel.SlotCount; s++)
            {
                var slot = (EquipmentSlot)s;
                int prev = 0;
                for (int i = 0; i < EquipmentModel.ItemCount(slot); i++)
                {
                    int need = EquipmentModel.RequiredLevel(slot, i);
                    Assert.GreaterOrEqual(need, prev,
                        $"[{EquipmentModel.SlotName(slot)}]의 요구 레벨이 자리 순서대로 오르지 않습니다.");
                    prev = need;
                    maxRequired = Mathf.Max(maxRequired, need);

                    if (need <= 1) continue;
                    opened.TryGetValue(need, out int n);
                    opened[need] = n + 1;
                }
            }

            foreach (var pair in opened)
            {
                Assert.LessOrEqual(pair.Value, 2,
                    $"Lv.{pair.Key}에 아이템 {pair.Value}개가 한꺼번에 열립니다 — 그 앞뒤 레벨이 텅 빕니다.");
            }
            Assert.GreaterOrEqual(opened.Count, 12,
                "잠긴 아이템이 열리는 레벨이 12종류보다 적습니다 — 성장 구간이 듬성듬성해집니다.");
            // 2026-09-01 카테고리당 +2종 — 마지막 해금이 Lv.24(커서 친구) -> Lv.30(달팽이)로 밀렸다.
            Assert.AreEqual(30, maxRequired,
                "가장 늦게 열리는 아이템의 요구 레벨이 30(달팽이)이 아닙니다 — 콘텐츠 표와 어긋납니다.");
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

            // 처음으로 <b>열리는</b> 아이템(털모자/단정한머리 = Lv.5)까지 걸리는 시간 —
            // "며칠 안에 하나씩"의 첫 단추. 0번 아이템들은 처음부터 보유라 기다림이 없다.
            int headUnlock = EquipmentModel.RequiredLevel(EquipmentSlot.Head, 1);
            float cumulative = 0f;
            for (int lv = 1; lv < headUnlock; lv++) cumulative += CharacterProgressionModel.XpToNextLevel(lv, config);
            float hoursToHat = cumulative / perHour;
            Assert.Less(hoursToHat, 24f,
                $"처음 열리는 아이템(Lv.{headUnlock})까지 패시브만으로 {hoursToHat:F1}시간입니다 — " +
                "하루 8시간 사용 기준으로 며칠씩 걸리면 '며칠 안에 하나씩'이 성립하지 않습니다.");
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
