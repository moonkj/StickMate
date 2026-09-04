using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using StickMate.Core;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ============================================================================
    /// C층 계약이 <b>코드로</b> 성립하는가 (docs/security/ENTITLEMENT_CONTRACT.md §E-1 ~ §E-4)
    /// ============================================================================
    /// <c>EntitlementFailOpenAuditTests</c> 는 <b>소스 텍스트</b>로 형태를 잰다(3상태가 있는가,
    /// <c>bool</c> 반환이 없는가). 이 파일은 같은 계약을 <b>실행</b>으로 잰다 —
    /// 형태가 맞아도 동작이 틀릴 수 있고, 그 둘은 서로를 대신하지 못한다.
    ///
    /// ============================================================================
    /// 이 계약이 막으려는 것은 무단 사용이 아니라 <b>정반대</b>다
    /// ============================================================================
    /// 스팀 클라이언트가 안 떠 있으면 조회가 실패하고, 소박한 <c>bool</c> 구현은 그것을 <b>false</b>로
    /// 읽는다 — <b>정상 결제한 사용자 전원이 잠긴다.</b> 이 앱은 부팅 자동 실행 + 종일 상주가 정상
    /// 사용 형태라 <b>매일 아침 그 구간을 지난다.</b>
    ///
    /// <para>정의서 원칙: 정당한 유저를 한 명이라도 잠그는 조치는 무단 사용 열 건보다 비싸다.</para>
    /// </summary>
    public sealed class PackEntitlementContractTests
    {
        private const string LogPrefix = "[C층계약]";

        /// <summary>
        /// 시키는 대로 답하는 가짜 스토어. <b>테스트 어셈블리에만</b> 있다 —
        /// 프로덕션에 「전부 보유」 출처를 두면 그것이 출하 빌드의 기본 경로가 될 수 있다(§E-6-c).
        ///
        /// <para>★ <b>대본(큐)이 아니라 「지금 답」을 들고 있다.</b> 큐로 만들면 단조 래치가
        /// 스토어 호출을 <b>건너뛸 때</b> 대본이 안 넘어가서, 몇 번째 답이 나올지가
        /// <b>래치의 동작에 좌우된다</b> — 재려는 것이 곧 그 래치인데 자가 함께 움직이는 셈이다.
        /// <see cref="Calls"/> 로 호출 횟수를 따로 세어 그 두 사실을 분리한다.</para>
        /// </summary>
        private sealed class MutableSource : IPackEntitlementSource
        {
            internal PackEntitlementState Answer = PackEntitlementState.Unknown;
            internal int Calls;

            internal MutableSource(PackEntitlementState answer) { Answer = answer; }

            public PackEntitlementState Query(string packId)
            {
                Calls++;
                return Answer;
            }
        }


        // ====================================================================
        // 1. §E-1 — 상태는 셋이다
        // ====================================================================

        [Test]
        public void 상태는_둘이_아니라_셋이다()
        {
            Array values = Enum.GetValues(typeof(PackEntitlementState));
            Assert.AreEqual(3, values.Length,
                $"{LogPrefix} 상태가 {values.Length}개입니다. §E-1의 뿌리는 <b>2개가 아니라 3개</b>라는 " +
                "것입니다 — 하나가 사라지면 조회 실패가 미보유로 붕괴하고, 하나가 늘면 " +
                "세 갈래를 명시한 모든 switch가 조용히 새 값을 흘립니다.");

            // 세 값이 서로 다른가(같은 정수로 겹치면 switch가 통째로 무너진다).
            Assert.AreNotEqual((int)PackEntitlementState.Owned, (int)PackEntitlementState.NotOwned);
            Assert.AreNotEqual((int)PackEntitlementState.Owned, (int)PackEntitlementState.Unknown);
            Assert.AreNotEqual((int)PackEntitlementState.NotOwned, (int)PackEntitlementState.Unknown);
        }

        [Test]
        public void 스토어가_없는_빌드는_미보유가_아니라_미확인이다()
        {
            PackEntitlements.ClearTestOverride();
            Assert.AreEqual(PackEntitlementState.Unknown, PackEntitlements.StateOf("packfixture.any"),
                $"{LogPrefix} 스토어 SDK가 0줄인 빌드가 <b>미보유</b>를 답했습니다. " +
                "물어볼 창구가 없는 것은 '안 샀다'를 확인한 것이 아닙니다 — " +
                "기본 구현이 먼저 그 붕괴를 저지르면 계약 전체가 무의미합니다.");

            Assert.AreEqual(PackEntitlementState.Unknown, NullPackEntitlementSource.Instance.Query("packfixture.any"));
        }

        [Test]
        public void 빈_아이디는_미보유가_아니라_미확인이다()
        {
            var source = new MutableSource(PackEntitlementState.NotOwned);
            PackEntitlements.SetTestOverride(source);
            Assert.AreEqual(PackEntitlementState.Unknown, PackEntitlements.StateOf(null));
            Assert.AreEqual(PackEntitlementState.Unknown, PackEntitlements.StateOf(""));
            Assert.AreEqual(0, source.Calls,
                $"{LogPrefix} 빈 아이디로 스토어를 물었습니다 — 물을 대상이 없는데 조회한 것입니다.");
        }

        // ====================================================================
        // 2. §E-3 — 회수하지 않는다 / 실패를 붙들지 않는다
        // ====================================================================

        [Test]
        public void 한번_확인된_보유는_조회가_실패해도_회수되지_않는다()
        {
            var source = new MutableSource(PackEntitlementState.Owned);
            PackEntitlements.SetTestOverride(source);

            Assert.AreEqual(PackEntitlementState.Owned, PackEntitlements.StateOf("packfixture.p1"));

            source.Answer = PackEntitlementState.Unknown;
            Assert.AreEqual(PackEntitlementState.Owned, PackEntitlements.StateOf("packfixture.p1"),
                $"{LogPrefix} 스토어가 <b>미확인</b>을 답하자 보유가 사라졌습니다(§E-3-b). " +
                "종일 상주 중 스팀이 잠깐 죽으면 산 사람이 입고 있던 것을 벗게 됩니다.");

            source.Answer = PackEntitlementState.NotOwned;
            Assert.AreEqual(PackEntitlementState.Owned, PackEntitlements.StateOf("packfixture.p1"),
                $"{LogPrefix} 스토어가 <b>미보유</b>를 답하자 보유가 회수됐습니다(§E-3-c). " +
                "상주 중 회수는 오탐 비용이 회수 이익보다 큽니다 — 다음 실행에서 정리됩니다.");

            // 래치는 <b>그 팩에만</b> 걸린다. 하나가 보유면 전부 보유가 되는 구조는 결제 경계를 지운다.
            Assert.AreEqual(PackEntitlementState.NotOwned, PackEntitlements.StateOf("packfixture.p2"),
                $"{LogPrefix} 단조 래치가 <b>다른 팩</b>에까지 걸렸습니다.");

            // ★ 위 세 번의 p1 조회 중 스토어를 부른 것은 <b>첫 번째뿐</b>이어야 한다(래치가 실제로 short-circuit).
            //   p2 조회 1회를 더하면 총 2회다. 이 수가 어긋나면 위 초록은 「회수 안 함」이 아니라
            //   「스토어가 계속 Owned를 답했음」일 수 있다 — 두 사실이 같은 초록으로 보인다.
            Assert.AreEqual(2, source.Calls,
                $"{LogPrefix} 스토어 조회가 {source.Calls}회입니다(기대 2회: p1 첫 조회 + p2). " +
                "래치가 short-circuit 하지 않으면 위 단언들이 무엇을 증명했는지 알 수 없습니다.");
        }

        [Test]
        public void 미확인과_미보유는_기억되지_않아_다음_질문이_새로_답을_받는다()
        {
            // 09:00 스팀 미기동 → Unknown / 09:01 기동 완료 → Owned.
            // 부정 캐시가 있으면 "다시 켜세요"가 답이 되는데, 상주 앱에서 그건 답이 아니다(§E-3-a).
            var source = new MutableSource(PackEntitlementState.Unknown);
            PackEntitlements.SetTestOverride(source);

            Assert.AreEqual(PackEntitlementState.Unknown, PackEntitlements.StateOf("packfixture.p1"));

            source.Answer = PackEntitlementState.Owned;
            Assert.AreEqual(PackEntitlementState.Owned, PackEntitlements.StateOf("packfixture.p1"),
                $"{LogPrefix} 미확인이 기억됐습니다 — 스팀이 뜬 뒤에도 유저가 산 팩을 못 씁니다.");
            Assert.AreEqual(2, source.Calls,
                $"{LogPrefix} 조회 횟수가 {source.Calls}회입니다. 미확인/미보유를 캐시하면 " +
                "재시도 장치가 따로 필요해지고, 그 장치가 없으면 하루 종일 잠긴 채로 있게 됩니다.");

            // 보유가 확정된 뒤에는 <b>더 묻지 않는다</b>(래치). 조회는 로컬 IPC지만 상주 앱이라 공짜가 아니다.
            int before = source.Calls;
            PackEntitlements.StateOf("packfixture.p1");
            Assert.AreEqual(before, source.Calls,
                $"{LogPrefix} 보유 확정 뒤에도 스토어를 다시 물었습니다.");
        }

        [Test]
        public void 출처를_바꾸면_단조_래치도_함께_비워진다()
        {
            PackEntitlements.SetTestOverride(new MutableSource(PackEntitlementState.Owned));
            Assert.AreEqual(PackEntitlementState.Owned, PackEntitlements.StateOf("packfixture.p1"));

            PackEntitlements.SetTestOverride(new MutableSource(PackEntitlementState.NotOwned));
            Assert.AreEqual(PackEntitlementState.NotOwned, PackEntitlements.StateOf("packfixture.p1"),
                $"{LogPrefix} 앞 테스트가 확인한 보유가 다음 테스트로 샜습니다 — " +
                "정적 상태가 새면 초록/빨강이 <b>실행 순서</b>에 달리게 됩니다.");
        }

        // ====================================================================
        // 3. §E-2-a — 미보유와 미확인은 <b>다른 말</b>을 해야 한다
        // ====================================================================

        [Test]
        public void 세_상태의_사유가_서로_다르고_전부_원문이_아니라_키다()
        {
            var keys = new List<string>();
            foreach (PackEntitlementState s in Enum.GetValues(typeof(PackEntitlementState)))
            {
                string key = PackEntitlements.ReasonKey(s);
                Assert.IsTrue(PackManifestKeys.IsWellFormed(key),
                    $"{LogPrefix} 사유가 키 모양이 아닙니다({s} → \"{key}\"). " +
                    "Core에 한글 원문을 두면 C층이 첫날부터 번역 부채를 만듭니다.");
                Assert.IsFalse(keys.Contains(key),
                    $"{LogPrefix} 두 상태가 같은 사유를 냅니다(\"{key}\"). " +
                    "§E-2-a: 같은 문구를 쓰면 오프라인 유저에게 '당신은 안 샀습니다'라고 말하게 됩니다.");
                keys.Add(key);
            }
            Assert.AreEqual(3, keys.Count);
            Debug.Log($"{LogPrefix} 사유 키 3종: {string.Join(" / ", keys)}");
        }

        // ====================================================================
        // 4. §E-4 / §E-8 — 세이브는 팩을 모른다
        // ====================================================================

        private string _savedBackup;
        private bool _hadSave;
        private string _prevBackup;
        private bool _hadPrev;

        /// <summary>
        /// ★★ <b>저장 경로를 이 스위트가 직접 옮기지 않는다.</b> 2026-09-03 러너 실측으로 배운 것이다.
        ///
        /// <para>처음에는 <c>RedirectToTemporaryDirectoryForTesting</c> 으로 자기 폴더를 잡고
        /// <c>TearDown</c> 에서 <c>ResetForTesting()</c> 으로 되돌렸다. 그런데 그 되돌림이
        /// <b>스위트 전체 격리</b>(<c>GlobalEditModeTestIsolation</c>)까지 함께 껐다 —
        /// 뒤이어 도는 저장 관련 스위트 <b>3종 14건</b>이 <i>"저장 경로가 임시 폴더로
        /// 리디렉션되어 있지 않습니다"</i> 로 무너졌다. 내 테스트는 <b>전부 초록</b>이었고,
        /// 깨진 것은 남이었다.</para>
        ///
        /// <para>그래서 전역 리디렉션을 <b>그대로 쓰고</b>, 파일만 앞뒤로 백업·복원한다
        /// (<c>EquipmentMigrationTests</c> 가 쓰는 관례 그대로). 그리고 격리가 실제로 걸려 있는지
        /// <b>먼저 단언</b>한다 — 안 걸려 있으면 개발자의 진짜 저장 파일을 건드리게 된다.</para>
        /// </summary>
        [OneTimeSetUp]
        public void BackupSaveFile()
        {
            Assert.IsTrue(CharacterSaveStore.IsRedirectedForTesting,
                $"{LogPrefix} 저장 경로가 임시 폴더로 리디렉션되어 있지 않습니다" +
                "(GlobalEditModeTestIsolation 확인). 이 스위트는 파일을 실제로 쓰므로 그 상태에서는 " +
                "개발자의 진짜 저장 파일을 건드릴 수 있습니다.");

            _hadSave = File.Exists(CharacterSaveStore.FilePath);
            _savedBackup = _hadSave ? File.ReadAllText(CharacterSaveStore.FilePath) : null;
            _hadPrev = File.Exists(CharacterSaveStore.PreviousGenerationPath);
            _prevBackup = _hadPrev ? File.ReadAllText(CharacterSaveStore.PreviousGenerationPath) : null;
        }

        [OneTimeTearDown]
        public void RestoreSaveFile()
        {
            if (_hadSave) File.WriteAllText(CharacterSaveStore.FilePath, _savedBackup);
            else if (File.Exists(CharacterSaveStore.FilePath)) File.Delete(CharacterSaveStore.FilePath);

            if (_hadPrev) File.WriteAllText(CharacterSaveStore.PreviousGenerationPath, _prevBackup);
            else if (File.Exists(CharacterSaveStore.PreviousGenerationPath))
                File.Delete(CharacterSaveStore.PreviousGenerationPath);
        }

        /// <summary>★ <c>SetUp</c>/<c>TearDown</c> 을 <b>각각 하나씩만</b> 둔다.
        /// NUnit은 한 클래스에 여럿 있을 때 실행 순서를 보장하지 않는다 — 정적 상태를 다루는
        /// 이 파일에서 순서가 흔들리면 초록/빨강이 실행 순서에 달리게 된다.</summary>
        [SetUp]
        public void ResetModels()
        {
            PackEntitlements.ClearTestOverride();
            CharacterProgressionModel.ResetForTesting();
            EquipmentModel.ResetForTesting();
            CharacterStatsModel.ResetForTesting();
            UiLayoutModel.ResetForTesting();
            TodoListModel.ResetForTesting();
            CharacterAppearanceModel.ResetForTesting();
            AppSettingsModel.ResetForTesting();
        }

        [TearDown]
        public void RestoreDefaultSource() => PackEntitlements.ClearTestOverride();

        /// <summary>
        /// ★ <b>보유 상태가 무엇이든 세이브 바이트가 같다.</b>
        ///
        /// <para>부재 단언("파일에 팩 아이디가 없다")만 두면 썩었을 때 <b>조용히 초록</b>이 된다.
        /// 그래서 같은 프로브로 <b>존재</b>도 함께 확인한다 — 착용 중인 아이템 아이디는
        /// 실제로 파일에 들어 있어야 하고, 그 사실이 프로브가 살아 있음을 증명한다.</para>
        /// </summary>
        [Test]
        public void 팩_보유는_세이브에_한_바이트도_남기지_않는다()
        {
            // ★ 앞선 스위트가 남긴 진단 플래그(SaveSuspended 등)를 확정적으로 지운다.
            //   ResetForTesting()은 <b>전역 리디렉션까지</b> 끄므로 쓰면 안 된다 —
            //   Load()는 그 플래그들만 초기화한다(파일이 없으면 즉시 반환).
            if (File.Exists(CharacterSaveStore.FilePath)) File.Delete(CharacterSaveStore.FilePath);
            CharacterSaveStore.Load();
            Assert.IsFalse(CharacterSaveStore.SaveSuspended,
                $"{LogPrefix} 저장 보류 상태입니다 — Save()가 조용히 false를 돌려주고, " +
                "그 false는 '저장 안 함'과 똑같이 생겼습니다.");

            ItemCatalogEntry head = ItemCatalog.Item(EquipmentSlot.Head, 0);
            Assert.IsNotNull(head, $"{LogPrefix} 기본 아이템을 못 읽었습니다 — 이 검사의 존재 대조가 성립하지 않습니다.");
            EquipmentModel.RestoreFromSave(EquipmentSlot.Head, head.Id);

            const string packId = "packfixture.wallet";

            PackEntitlements.SetTestOverride(new MutableSource(PackEntitlementState.NotOwned));
            Assert.AreEqual(PackEntitlementState.NotOwned, PackEntitlements.StateOf(packId));
            Assert.IsTrue(CharacterSaveStore.Save(), $"{LogPrefix} 저장에 실패했습니다({CharacterSaveStore.FilePath}).");
            string notOwnedText = File.ReadAllText(CharacterSaveStore.FilePath);

            PackEntitlements.SetTestOverride(new MutableSource(PackEntitlementState.Owned));
            Assert.AreEqual(PackEntitlementState.Owned, PackEntitlements.StateOf(packId));
            Assert.IsTrue(CharacterSaveStore.Save());
            string ownedText = File.ReadAllText(CharacterSaveStore.FilePath);

            // (가) 존재 대조 — 프로브가 실제로 문자열을 찾아낸다.
            StringAssert.Contains(head.Id, ownedText,
                $"{LogPrefix} 착용 아이템 아이디가 파일에 없습니다 — 그러면 아래 '팩 아이디 없음'은 " +
                "'없다'가 아니라 '못 본다'입니다.");

            // (나) 부재 — 팩 아이디는 없다.
            Assert.IsFalse(ownedText.Contains(packId),
                $"{LogPrefix} 세이브에 팩 아이디가 적혔습니다(§E-4-a · §E-8-b). " +
                "C층을 세이브에서 뺀 목적은 방어 추가가 아니라 <b>표적 제거</b>였습니다 — " +
                "적는 순간 표적이 되돌아오고, 세이브 스키마 버전도 함께 올려야 합니다.");

            // (다) 더 강한 형태 — 보유 상태를 바꿔도 파일이 <b>글자 하나</b> 안 바뀐다.
            Assert.AreEqual(notOwnedText, ownedText,
                $"{LogPrefix} 보유 상태에 따라 세이브 내용이 달라집니다. 그러면 세이브가 C층을 " +
                "간접적으로 담고 있는 것이고, 이 라운드가 세이브 스키마를 올리지 않았다는 주장이 거짓이 됩니다.");
        }

        /// <summary>
        /// 팩 아이템을 입은 채 저장한 뒤 <b>그 팩이 없는 기기</b>에서 여는 경우
        /// (환불 · 다른 PC · DLC 미설치). 이미 있는 경로이지만, 팩이 생기면 <b>처음으로 실제로
        /// 밟히는</b> 경로이므로 여기서 잠근다.
        /// </summary>
        [Test]
        public void 없는_팩_아이템을_입은_세이브는_미착용으로_떨어지고_나머지는_보존된다()
        {
            ItemCatalogEntry eyes = ItemCatalog.Item(EquipmentSlot.Eyes, 0);
            Assert.IsNotNull(eyes);

            string json =
                "{\n" +
                $"    \"version\": {CharacterSaveStore.CurrentVersion},\n" +
                "    \"level\": 5,\n" +
                "    \"currentXp\": 1.0,\n" +
                "    \"totalXpEarned\": 100.0,\n" +
                "    \"characterName\": \"packfixture\",\n" +
                "    \"wornHead\": \"packfixture.ghost.fedora\",\n" +
                $"    \"wornEyes\": \"{eyes.Id}\"\n" +
                "}";
            File.WriteAllText(CharacterSaveStore.FilePath, json);

            CharacterSaveStore.Load();

            Assert.IsFalse(EquipmentModel.IsEquipped(EquipmentSlot.Head),
                $"{LogPrefix} 없는 팩 아이템이 무언가로 <b>바꿔치기</b>됐습니다. " +
                "사용자가 고르지 않은 차림이 되는 것은 없는 것보다 나쁩니다.");
            Assert.IsTrue(EquipmentModel.IsEquipped(EquipmentSlot.Eyes),
                $"{LogPrefix} 모르는 아이디 하나 때문에 <b>다른 자리</b>까지 잃었습니다.");
            Assert.AreEqual(eyes.Id, EquipmentModel.WornItemId(EquipmentSlot.Eyes));
        }
    }
}
