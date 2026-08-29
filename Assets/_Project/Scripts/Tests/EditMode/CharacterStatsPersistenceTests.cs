using System;
using System.IO;
using NUnit.Framework;
using UnityEngine.TestTools;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 기록(근속/함께한 시간/격파/대결/활쏘기/넘어짐)의 <b>영속화</b> 회귀 테스트 —
    /// 2026-08-30 정보창 리디자인 라운드.
    ///
    /// ============================================================================
    /// 무엇을 잡으려는가
    /// ============================================================================
    ///  (1) 저장 -> 초기화 -> 로드 왕복에서 <b>여섯 값이 전부</b> 그대로 돌아온다(리더 지시의 명시 항목:
    ///      "카운터 영속화 테스트(재시작 후 유지) 추가").
    ///  (2) <b>구버전(v1) 저장 파일</b>도 그대로 읽힌다 — 이 라운드에서 스키마 버전을 올렸으므로,
    ///      이미 며칠 키운 사용자의 레벨이 날아가면 그게 최악의 회귀다. 새 필드는 0으로 시작하고
    ///      그 0은 "아직 기록이 없다"는 정확한 사실이다.
    ///  (3) 활쏘기 0발은 <b>0%가 아니라 "기록 없음"</b>이다(0%는 "쏴봤는데 다 빗나갔다"는 다른 뜻).
    ///  (4) 손상된 값(명중 > 발사, 음수, 미래 시각)에서도 표시가 깨지지 않는다.
    ///
    /// 실행 중인 앱의 진짜 저장 파일과 같은 경로를 쓰므로(개발 머신) OneTimeSetUp에서 통째로 백업하고
    /// OneTimeTearDown에서 되돌린다 — CharacterProgressionPersistenceTests와 같은 관례.
    /// </summary>
    public class CharacterStatsPersistenceTests
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
        }

        [SetUp]
        public void ResetModels()
        {
            CharacterProgressionModel.ResetForTesting();
            EquipmentModel.ResetForTesting();
            CharacterStatsModel.ResetForTesting();
        }

        [Test]
        public void 기록_여섯_값이_저장하고_다시_불러온_뒤에도_같다()
        {
            CharacterStatsModel.AddBattleWin();
            CharacterStatsModel.AddBattleWin();
            CharacterStatsModel.AddRivalWin();
            CharacterStatsModel.AddArcheryShot(bullseye: true);
            CharacterStatsModel.AddArcheryShot(bullseye: false);
            CharacterStatsModel.AddArcheryShot(bullseye: false);
            CharacterStatsModel.AddRagdollFall();
            CharacterStatsModel.AddCompanionSeconds(3 * 3600f + 12 * 60f);
            CharacterStatsModel.EnsureFirstRunInitialized();

            long firstRun = CharacterStatsModel.FirstRunUnixSeconds;
            Assert.Greater(firstRun, 0L, "첫 만남 시각이 기록되지 않았습니다 — 근속을 셀 기준점이 없습니다.");
            Assert.IsTrue(CharacterStatsModel.IsDirty, "값이 바뀌었는데 저장 대상으로 표시되지 않았습니다.");
            Assert.IsTrue(CharacterSaveStore.Save(), "저장에 실패했습니다.");
            Assert.IsFalse(CharacterStatsModel.IsDirty, "저장 후에도 더티 플래그가 남아 있습니다.");

            // 프로세스를 새로 켠 것과 같은 상태로 만든 뒤 로드.
            CharacterStatsModel.ResetForTesting();
            Assert.AreEqual(0, CharacterStatsModel.BattleWins, "초기화가 되지 않아 아래 검증이 무의미해집니다.");

            CharacterSaveStore.Load();

            Assert.AreEqual(2, CharacterStatsModel.BattleWins, "격파 성공 횟수가 복원되지 않았습니다.");
            Assert.AreEqual(1, CharacterStatsModel.RivalWins, "대결 승리 횟수가 복원되지 않았습니다.");
            Assert.AreEqual(3, CharacterStatsModel.ArcheryShots, "활쏘기 발사 수가 복원되지 않았습니다.");
            Assert.AreEqual(1, CharacterStatsModel.ArcheryBullseyes, "활쏘기 명중 수가 복원되지 않았습니다.");
            Assert.AreEqual(1, CharacterStatsModel.RagdollFalls, "넘어진 횟수가 복원되지 않았습니다.");
            Assert.AreEqual(3 * 3600f + 12 * 60f, CharacterStatsModel.TotalCompanionSeconds, 1f,
                "함께한 시간이 복원되지 않았습니다.");
            Assert.AreEqual(firstRun, CharacterStatsModel.FirstRunUnixSeconds, "첫 만남 시각이 복원되지 않았습니다.");
            Assert.AreEqual("3시간 12분", CharacterStatsModel.FormatCompanionTime());
        }

        [Test]
        public void 구버전_v1_저장_파일도_그대로_읽히고_기록은_0에서_시작한다()
        {
            // 이번 라운드 이전(v1) 파일에는 기록 필드가 아예 없다.
            const string V1Json =
                "{\n" +
                "    \"version\": 1,\n" +
                "    \"level\": 5,\n" +
                "    \"currentXp\": 123.5,\n" +
                "    \"totalXpEarned\": 900.0,\n" +
                "    \"characterName\": \"옛동료\",\n" +
                "    \"equippedHead\": true,\n" +
                "    \"equippedEyes\": false,\n" +
                "    \"equippedNeck\": false,\n" +
                "    \"equippedShoulders\": false\n" +
                "}";
            File.WriteAllText(CharacterSaveStore.FilePath, V1Json);

            CharacterSaveStore.Load();

            Assert.IsTrue(CharacterSaveStore.LoadedFromFile, "구버전 파일을 읽지 못했습니다 — 사용자의 진행도가 날아갑니다.");
            Assert.AreEqual(5, CharacterProgressionModel.Level, "구버전 파일의 레벨이 복원되지 않았습니다.");
            Assert.AreEqual("옛동료", CharacterProgressionModel.CharacterName);
            Assert.IsTrue(EquipmentModel.IsEquipped(EquipmentSlot.Head), "구버전 파일의 장비가 복원되지 않았습니다.");

            Assert.AreEqual(0, CharacterStatsModel.BattleWins, "없던 기록은 0이어야 합니다.");
            Assert.AreEqual(0, CharacterStatsModel.RagdollFalls);
            Assert.AreEqual(0L, CharacterStatsModel.FirstRunUnixSeconds,
                "구버전 파일에는 첫 만남 시각이 없으므로 0이어야 합니다(그 뒤 EnsureFirstRunInitialized가 채운다).");

            CharacterStatsModel.EnsureFirstRunInitialized();
            Assert.Greater(CharacterStatsModel.FirstRunUnixSeconds, 0L,
                "구버전 파일에서 올라온 사용자는 '오늘'을 첫 만남으로 삼아야 합니다.");
            Assert.AreEqual(1, CharacterStatsModel.DaysTogether, "방금 시작했으면 1일차입니다.");
        }

        [Test]
        public void 활쏘기를_한_발도_안_쐈으면_명중률이_아니라_기록_없음이다()
        {
            Assert.IsFalse(CharacterStatsModel.TryGetArcheryAccuracy01(out float acc),
                "발사 0회인데 명중률을 계산했습니다 — 0%는 '쏴봤는데 다 빗나갔다'는 다른 사실입니다.");
            Assert.AreEqual(0f, acc);

            CharacterStatsModel.AddArcheryShot(bullseye: true);
            Assert.IsTrue(CharacterStatsModel.TryGetArcheryAccuracy01(out acc));
            Assert.AreEqual(1f, acc, 0.0001f);
        }

        [Test]
        public void 근속은_첫_만남으로부터_지난_날수_더하기_하루다()
        {
            long threeDaysAgo = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 3 * 86400L - 60L;
            CharacterStatsModel.RestoreFromSave(0, 0, 0, 0, 0f, 0, threeDaysAgo);

            Assert.AreEqual(4, CharacterStatsModel.DaysTogether,
                "3일 전에 처음 만났으면 오늘은 4일차입니다(첫날이 1일차).");
        }

        [Test]
        public void 손상된_값이_들어와도_표시가_깨지지_않는다()
        {
            // 명중 수 > 발사 수 / 음수 / 미래 시각 — 전부 파일 손상이나 손편집으로 들어올 수 있다.
            long future = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 5 * 86400L;
            CharacterStatsModel.RestoreFromSave(-3, -1, 2, 99, -50f, -7, future);

            Assert.AreEqual(0, CharacterStatsModel.BattleWins, "음수 기록이 그대로 들어왔습니다.");
            Assert.AreEqual(0, CharacterStatsModel.RivalWins);
            Assert.AreEqual(0, CharacterStatsModel.RagdollFalls);
            Assert.AreEqual(0f, CharacterStatsModel.TotalCompanionSeconds, 0.001f);
            Assert.AreEqual(2, CharacterStatsModel.ArcheryShots);
            Assert.AreEqual(2, CharacterStatsModel.ArcheryBullseyes,
                "명중 수가 발사 수보다 크면 명중률이 100%를 넘습니다 — 발사 수로 clamp되어야 합니다.");

            Assert.IsTrue(CharacterStatsModel.TryGetArcheryAccuracy01(out float acc));
            Assert.LessOrEqual(acc, 1f);
            Assert.AreEqual(1, CharacterStatsModel.DaysTogether,
                "시계가 과거로 돌아간 경우에도 근속이 음수가 되면 안 됩니다.");
        }

        [Test]
        public void 손상된_JSON에서도_기록이_기본값으로_떨어지고_크래시하지_않는다()
        {
            File.WriteAllText(CharacterSaveStore.FilePath, "{ broken json ]]]");

            LogAssert.ignoreFailingMessages = true;   // 이 경로는 경고 로그를 내는 것이 정상 동작이다.
            CharacterSaveStore.Load();
            LogAssert.ignoreFailingMessages = false;

            Assert.AreEqual(0, CharacterStatsModel.BattleWins);
            Assert.AreEqual(1, CharacterProgressionModel.Level);
        }
    }
}
