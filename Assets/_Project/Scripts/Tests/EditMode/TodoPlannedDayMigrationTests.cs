using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 저장 스키마 <b>v12 → v13</b> 하위 호환 — 2026-09-08 [오늘 할일] 날짜 축
    /// (docs/UX_WIDGETS.md R6-6 · 리더 판정 R6-13 ③).
    ///
    /// CLAUDE.md 협업 프로토콜: <i>"저장 스키마 <c>CurrentVersion</c>을 올리는 라운드는 <c>vN-1</c>
    /// 구버전 파일을 읽었을 때 신규 필드가 안전한 기본값으로 채워지는지 검증하는 하위 호환 테스트
    /// 1건을 반드시 동반한다."</i>
    ///
    /// ============================================================================
    /// R6-6이 요구한 단언은 <b>두 줄</b>이다 — 한 줄만 쓰면 조용히 썩는다
    /// ============================================================================
    /// <list type="letter">
    ///  <item><b>(a)</b> v12 파일을 로드하면 기존 항목의 <c>PlannedDayIndex</c>가 <b>0</b>이다.</item>
    ///  <item><b>(b)</b> <c>0</c>인 항목은 <b>날짜 라벨을 안 그리고 달력 카운트에도 안 들어간다</b>.</item>
    /// </list>
    /// R6-6 원문: <i>"(b)가 빠지면 기존 사용자의 모든 항목이 1970-01-01 칸에 쌓이고
    /// <b>테스트는 초록이다</b>."</i> — (a)만으로는 «0이 들어왔다»만 확인할 뿐, 그 0을 코드가
    /// <b>날짜로 취급하는지</b>를 한 글자도 재지 못한다. 그래서 이 파일은 둘을 함께 잠근다.
    /// (b)의 "날짜 라벨"은 이 라운드에 UI가 없으므로 <b>모델이 노출하는 계약</b>
    /// (<see cref="TodoItem.IsPlannedDayKnown"/>)으로 잰다 — 화면은 그 값을 보고 그린다.
    ///
    /// ============================================================================
    /// 네거티브 컨트롤
    /// ============================================================================
    ///  · <c>CharacterSaveStore.ToItems</c>가 <c>plannedDayIndex</c>를 안 읽게 되돌리면
    ///    <c>v13_왕복은…</c>이 실패한다(적어 둔 내일이 사라진다).
    ///  · <c>CurrentVersion</c>만 올리고 필드를 안 넣으면 <c>날짜_필드는_v13_번호로_기록된다</c>가 실패한다.
    ///  · 같은 파일의 v12 값(글자·완료 여부·레벨·동전)이 살아남는지를 <b>음성 대조</b>로 함께 잰다 —
    ///    없으면 위 단언들이 "파일을 통째로 버려서" 통과한 것과 구별되지 않는다.
    /// </summary>
    public sealed class TodoPlannedDayMigrationTests
    {
        private string _backup;
        private bool _hadFile;

        private readonly List<TodoItem> _buffer = new List<TodoItem>(8);

        // 「오늘」 — CurrencyRules.LocalDayIndex와 같은 계(에폭 이후 일수). 벽시계를 읽지 않는다.
        private const int Today = 20_701;
        private const int Tomorrow = 20_702;

        [OneTimeSetUp]
        public void BackupRealSaveFile()
        {
            // ★ EditMode 스위트는 GlobalEditModeTestIsolation이 이미 저장 경로를 임시 폴더로 옮겨 두지만,
            //   그 격리가 어떤 이유로 빠져도 개발자 파일이 손상되지 않도록 한 겹 더 덮는다
            //   (EquipmentMigrationTests와 같은 관례).
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
            ResetModels();
        }

        [SetUp]
        public void ResetModels()
        {
            CharacterProgressionModel.ResetForTesting();
            EquipmentModel.ResetForTesting();
            CharacterStatsModel.ResetForTesting();
            UiLayoutModel.ResetForTesting();
            TodoListModel.ResetForTesting();
            CharacterAppearanceModel.ResetForTesting();
            AppSettingsModel.ResetForTesting();
            CurrencyModel.ResetForTesting();
            CostumeProgressModel.ResetForTesting();
            _buffer.Clear();
        }

        [TearDown]
        public void ResetModelsAfter() => ResetModels();

        /// <summary>v12 파일. <b>v12가 실제로 담고 있던 필드만</b> 적는다 — 할일 레코드에
        /// <c>plannedDayIndex</c>가 <b>없다</b>는 것이 이 테스트의 전제다.</summary>
        private const string V12Json =
            "{\n" +
            "    \"version\": 12,\n" +
            "    \"level\": 17,\n" +
            "    \"currentXp\": 12.0,\n" +
            "    \"totalXpEarned\": 9000.0,\n" +
            "    \"characterName\": \"열두동료\",\n" +
            "    \"coinBalance\": 2200,\n" +
            "    \"dayIndex\": 91,\n" +
            "    \"todos\": [\n" +
            "        { \"id\": 3, \"text\": \"우유 사기\", \"completed\": false },\n" +
            "        { \"id\": 4, \"text\": \"체크만 해둔 것\", \"completed\": true }\n" +
            "    ],\n" +
            "    \"todoArchive\": [\n" +
            "        { \"id\": 1, \"text\": \"지난주에 끝낸 것\", \"completed\": true }\n" +
            "    ],\n" +
            "    \"costumeFocusMinutesToday\": 40\n" +
            "}";

        /// <summary>
        /// ★★ (a) + (b) — v12 파일의 항목은 전부 «날짜 미상»이고, 그 미상은 <b>어느 달력 칸에도
        /// 세지 않고 날짜 라벨을 그리지 않는다</b>.
        /// </summary>
        [Test]
        public void v12_파일을_읽으면_모든_할일이_날짜_미상이_된다()
        {
            File.WriteAllText(CharacterSaveStore.FilePath, V12Json);
            CharacterSaveStore.Load();

            Assert.IsTrue(CharacterSaveStore.LoadedFromFile, "v12 파일을 통째로 버렸습니다.");
            Assert.IsFalse(CharacterSaveStore.SaveSuspended,
                "v12 파일을 읽었을 뿐인데 저장이 보류됐습니다 — 그러면 이 사용자는 다시는 저장되지 않습니다.");

            // ---- 전제(양성 대조): 할일 자체는 살아 있는가 ----
            Assert.AreEqual(2, TodoListModel.ActiveItems.Count, "v12 파일의 활성 할일이 사라졌습니다(전제 붕괴).");
            Assert.AreEqual(1, TodoListModel.CompletedArchive.Count, "v12 파일의 완료함이 사라졌습니다(전제 붕괴).");

            // ---- (a) 신규 필드가 안전한 기본값(0 = 미상)인가 ----
            foreach (TodoItem item in TodoListModel.ActiveItems)
            {
                Assert.AreEqual(TodoItem.UnknownPlannedDay, item.PlannedDayIndex,
                    $"v12 파일에 없던 날짜가 «{item.Text}»에 생겼습니다. v12 사용자에게 참인 사실은 " +
                    "«이 항목엔 날짜가 없다»뿐입니다.");
                Assert.IsFalse(item.IsPlannedDayKnown,
                    $"«{item.Text}»가 날짜를 안다고 말합니다 — 화면이 없는 날짜 라벨을 그리게 됩니다.");
            }
            foreach (TodoItem item in TodoListModel.CompletedArchive)
            {
                Assert.IsFalse(item.IsPlannedDayKnown,
                    "완료함 항목이 날짜를 안다고 말합니다 — [완료함] 탭이 없는 날짜를 적게 됩니다 " +
                    "(R6-6: 모르는 것은 안 적는다).");
            }

            // ---- (b) 그 0이 <b>달력 어느 칸에도</b> 안 들어가는가 ----
            //     0을 1970-01-01로 읽는 코드가 있으면 여기가 빨개진다. 이 단언이 R6-6이 요구한
            //     "두 번째 줄"이고, 없으면 위 (a)는 조용히 썩는다.
            Assert.AreEqual(0, TodoListModel.TallyForDay(TodoItem.UnknownPlannedDay).Total,
                "«0일»을 물었더니 v12 항목들이 잡혔습니다 — 기존 사용자의 할일 전부가 1970-01-01 칸에 " +
                "쌓입니다(UW-6-4: 이 설계는 그 순간 전부 무효).");
            for (int day = Today - 400; day <= Today + 40; day += 7)
            {
                Assert.AreEqual(0, TodoListModel.TallyForDay(day).Total,
                    $"{day}일 달력 칸에 «날짜 미상» 항목이 세어졌습니다.");
            }
            Assert.AreEqual(0, TodoListModel.AppendItemsForDay(Today, _buffer),
                "오늘 날짜 목록에 «날짜 미상» 항목이 실렸습니다.");
            Assert.AreEqual(0, TodoListModel.AppendOverdueItems(Today, _buffer),
                "날짜를 모르는 항목이 «밀린 것»으로 분류됐습니다 — 언제 하기로 했는지 모르는 것을 " +
                "«늦었다»고 말할 수 없습니다.");
            Assert.IsFalse(TodoListModel.TryGetEarliestPlannedDay(out _),
                "미상 항목만 있는데 탐색 하한이 생겼습니다 — 달력이 1970년까지 열립니다.");

            // ---- (b)의 짝: 그래도 화면에서 사라지지는 않는다(미상 그룹으로 나온다) ----
            Assert.AreEqual(1, TodoListModel.AppendUnknownPlannedDayItems(_buffer),
                "v12 사용자의 미완료 할일이 어느 화면에서도 보이지 않게 됐습니다 " +
                "(R6-6: «오늘 페이지 최상단 그룹»으로 남아야 한다).");
            Assert.AreEqual("우유 사기", _buffer[0].Text, "미상 그룹에 엉뚱한 항목이 나왔습니다.");

            // ---- 음성 대조 — 같은 파일의 v12 값들이 살아남는가 ----
            Assert.AreEqual("우유 사기", TodoListModel.ActiveItems[0].Text, "v12 파일의 할일 글자가 바뀌었습니다.");
            Assert.IsTrue(TodoListModel.ActiveItems[1].Completed, "v12 파일의 완료 여부가 사라졌습니다.");
            Assert.AreEqual(17, CharacterProgressionModel.Level, "v12 파일의 레벨이 사라졌습니다(전제 붕괴).");
            Assert.AreEqual("열두동료", CharacterProgressionModel.CharacterName, "v12 파일의 이름이 사라졌습니다.");
            Assert.AreEqual(2200, CurrencyModel.CoinBalance, "v12 파일의 동전 잔액이 사라졌습니다.");
        }

        /// <summary>★ v13 <b>왕복</b> — 위 테스트가 "없을 때"를 잠그므로 이것이 "있을 때"를 잠근다.
        /// 둘 중 하나만 있으면 반대쪽이 조용히 죽는다.
        /// <para><b>서로 다른 날짜 둘 + 미상 하나</b>를 쓰는 이유: 한 줄짜리로는 «레코드가 날짜를
        /// 자기 줄에 들고 있는지»를 잴 수 없다(어긋나도 눈에 안 띈다).</para></summary>
        [Test]
        public void v13_왕복은_항목마다의_날짜를_보존한다()
        {
            TodoListModel.Add("오늘 것", softCap: 15, plannedDayIndex: Today);
            TodoListModel.Add("내일 것", softCap: 15, plannedDayIndex: Tomorrow);
            TodoListModel.Add("날짜 모르는 것", softCap: 15);

            Assert.IsTrue(CharacterSaveStore.Save(), "저장에 실패했습니다.");

            ResetModels();
            Assert.AreEqual(0, TodoListModel.ActiveItems.Count, "리셋 전제가 바뀌었습니다.");

            CharacterSaveStore.Load();

            Assert.AreEqual(3, TodoListModel.ActiveItems.Count, "재시작하면 할일이 사라집니다.");
            Assert.AreEqual(Today, TodoListModel.ActiveItems[0].PlannedDayIndex,
                "재시작하면 오늘 몫으로 적어 둔 날짜가 사라집니다.");
            Assert.AreEqual(Tomorrow, TodoListModel.ActiveItems[1].PlannedDayIndex,
                "두 번째 항목의 날짜가 사라졌거나 첫 번째 것과 섞였습니다(레코드가 날짜를 자기 줄에 " +
                "들고 있지 않다는 뜻입니다).");
            Assert.AreEqual(TodoItem.UnknownPlannedDay, TodoListModel.ActiveItems[2].PlannedDayIndex,
                "날짜 없이 적은 항목에 날짜가 생겼습니다.");

            Assert.AreEqual(1, TodoListModel.TallyForDay(Tomorrow).Total,
                "왕복 뒤 내일 칸의 집계가 틀립니다 — 달력이 다른 그림을 그립니다.");
        }

        /// <summary>저장 파일이 <b>정확히 v13</b>으로 기록되고, 새 필드가 실제로 그 안에 있다.
        /// 숫자를 베끼지 않고 상수를 참조한다(CLAUDE.md 2026-09-01 확정).
        /// <para>★ 이 단언이 지키는 것은 v10~v12와 같은 이유 — <b>다운그레이드 방어</b>다.
        /// 날짜 필드를 v12 번호로 앉히면 v12 시절 빌드가 그 파일을 «자기 버전»으로 읽어
        /// <c>SaveSuspended</c>가 침묵하고, 60초 뒤 자동 저장이 사용자가 적어 둔 날짜를 전부 0으로 지운다
        /// (그 빌드의 <c>ToRecords</c>에는 그 필드가 없다).</para></summary>
        [Test]
        public void 날짜_필드는_v13_번호로_기록된다()
        {
            TodoListModel.Add("내일 것", softCap: 15, plannedDayIndex: Tomorrow);
            Assert.IsTrue(CharacterSaveStore.Save(), "저장에 실패했습니다.");

            string json = File.ReadAllText(CharacterSaveStore.FilePath);
            StringAssert.Contains($"\"version\": {CharacterSaveStore.CurrentVersion}", json,
                "저장 파일의 버전 번호가 CurrentVersion과 다릅니다.");
            Assert.GreaterOrEqual(CharacterSaveStore.CurrentVersion,
                CharacterSaveStore.FirstVersionWithPlannedTodoDay,
                "할일 날짜 필드가 들어 있는데 스키마 버전이 그보다 낮습니다. 그러면 v12 시절 빌드가 " +
                "이 파일을 '자기 버전'으로 읽어 다운그레이드 방어가 통째로 침묵합니다.");
            StringAssert.Contains("\"plannedDayIndex\"", json,
                "v13을 선언했는데 할일 날짜 필드가 파일에 없습니다 — 버전만 올라가고 스키마가 " +
                "안 따라왔습니다.");
            StringAssert.Contains($"\"plannedDayIndex\": {Tomorrow}", json,
                "적어 둔 날짜가 파일에 그 값으로 적히지 않았습니다.");
        }

        /// <summary>손상된 날짜(음수)는 <b>미상으로 접힌다</b> — 디스크에서 올라온 값에 대해서도
        /// 같은 정규화가 도는지 보려면 모델을 거치지 않고 파일을 직접 써야 한다.
        /// <para>양성 대조를 같은 테스트 안에 둔다: 같은 파일의 <b>정상 날짜</b>는 살아남아야 한다.
        /// 없으면 이 부재 단언은 "날짜를 통째로 안 읽어서" 통과한 것과 구별되지 않는다.</para></summary>
        [Test]
        public void 손상된_음수_날짜는_미상으로_접히고_정상_날짜는_살아남는다()
        {
            string json =
                "{\n" +
                "    \"version\": " + CharacterSaveStore.CurrentVersion + ",\n" +
                "    \"level\": 5,\n" +
                "    \"characterName\": \"열셋동료\",\n" +
                "    \"todos\": [\n" +
                "        { \"id\": 1, \"text\": \"손상\", \"completed\": false, \"plannedDayIndex\": -9 },\n" +
                "        { \"id\": 2, \"text\": \"정상\", \"completed\": false, \"plannedDayIndex\": " + Today + " }\n" +
                "    ]\n" +
                "}";
            File.WriteAllText(CharacterSaveStore.FilePath, json);
            CharacterSaveStore.Load();

            Assert.AreEqual(2, TodoListModel.ActiveItems.Count, "전제 — 두 항목 모두 읽혀야 한다.");

            // 양성 대조(먼저) — 정상 날짜가 살아 있는가.
            Assert.AreEqual(Today, TodoListModel.ActiveItems[1].PlannedDayIndex,
                "정상 날짜까지 사라졌습니다 — 아래 '접혔다' 단언이 '전부 버려져서' 통과한 것이 됩니다(측정 무효).");

            // 부재 단언.
            Assert.AreEqual(TodoItem.UnknownPlannedDay, TodoListModel.ActiveItems[0].PlannedDayIndex,
                "음수 날짜가 그대로 들어왔습니다 — 달력이 음수 칸을 그리게 됩니다.");
            Assert.AreEqual(1, TodoListModel.TallyForDay(Today).Total,
                "정상 날짜의 집계가 틀립니다(손상된 항목이 섞였거나 정상 항목이 빠졌습니다).");
        }
    }
}
