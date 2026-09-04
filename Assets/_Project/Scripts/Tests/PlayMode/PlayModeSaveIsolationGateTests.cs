using System.IO;
using NUnit.Framework;
using StickMate.Core;
using UnityEngine;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★ <see cref="PlayModeSaveIsolationGate"/>가 <b>실제로 살아 있는지</b>를 이번 실행 안에서 증명한다
    /// (2026-09-02 <c>test-engineer</c>, BUG-2 동반 조치).
    ///
    /// ============================================================================
    /// 왜 카운터로 증명하지 않는가
    /// ============================================================================
    /// <c>LeafTestsStarted &gt; 0</c> 같은 단언은 <b>앞선 실행의 정적 값</b>으로도 참이 된다 —
    /// 에디터 도메인은 실행 사이에 살아남는다. 이 저장소가 아홉 번 당한 형태
    /// (<i>"실패한 측정과 성공한 측정이 똑같이 생겼다"</i>)가 그대로 재현될 자리다.
    ///
    /// ============================================================================
    /// 그래서 <b>이번 실행에 직접 심은 파일</b>로 잰다
    /// ============================================================================
    /// <list type="number">
    ///   <item><c>[TearDown]</c>이 격리 폴더에 표식 파일을 <b>심고</b>,
    ///         심어졌는지 <b>그 자리에서 확인</b>한다(양성 대조 — 쓰기 자체가 죽으면 여기서 걸린다).</item>
    ///   <item>게이트가 다음 테스트 <b>직전</b>에 그 폴더를 비운다.</item>
    ///   <item><c>[SetUp]</c>이 표식이 <b>사라졌는지</b> 확인한다. 게이트가 죽었으면 파일이 남아 있고,
    ///         이 검사는 <b>시끄럽게 빨개진다</b>.</item>
    /// </list>
    /// 순서에 의존하지 않는다 — 어느 테스트가 먼저 돌든 <b>두 번째</b>가 경계를 검사한다.
    /// 그리고 필터로 <b>한 건만</b> 돌아 경계가 아예 없었던 경우를 "검사했다"고 착각하지 않도록
    /// <c>[OneTimeTearDown]</c>이 그 사실을 구분해 기록한다.
    ///
    /// <para><b>부재 단언의 짝</b>: 아래 <c>File.Exists(...) == false</c>들은 썩으면 조용히 초록이 되는
    /// 종류다(CLAUDE.md). 그래서 <b>같은 픽스처 안</b>에 "심었더니 실제로 있다"는 존재 단언을
    /// 짝으로 둔다 — 둘 중 하나만 살아 있는 상태가 성립하지 않게.</para>
    ///
    /// ============================================================================
    /// ★★ 2026-09-03 (개선 R2) — <b>왜 테스트가 셋인가</b>
    /// ============================================================================
    /// <para>자기 반려 2번 항목: <i>"경계 프로브가 591건 실행에서 경계를 딱 1회만 건넌다."</i>
    /// 테스트가 둘이면 <c>[TearDown]→[SetUp]</c> 경계는 <b>한 번</b>만 생긴다. 그 1회는
    /// <b>메커니즘이 살아 있다</b>는 증명은 되지만, 나머지 590개 경계는
    /// <see cref="PlayModeSaveIsolationGate.PurgeAttempts"/> <b>카운터에만</b> 의존한다 —
    /// 그리고 이 파일의 클래스 문서가 스스로 말하듯 <b>카운터는 증명이 아니다</b>.</para>
    ///
    /// <para>그래서 셋으로 만든다: 리프 3개 → 실제로 <b>파일이 지워지는 것을 눈으로 본</b> 경계가
    /// <b>2회</b>가 된다. 회계는 정확히 <c>경계 검사 = 테스트 수 − 1</c>이고,
    /// <see cref="경계가_실제로_두_번_이상_검사됐는지_못박는다"/>가 그 등식을 <b>정확히</b> 단언한다
    /// (≥가 아니라 =). 하나가 조용히 안 돌면 등식이 깨져 시끄럽게 빨개진다.</para>
    ///
    /// <para>★ 그리고 세 번째 테스트는 머릿수 채우기가 아니다 — <b>삭제의 사정거리</b>(절대 불변
    /// 원칙 3)를 잰다. 이 게이트는 이번 실행에서 <b>591번 파일을 지운다</b>. 그 삽이 어디를
    /// 파는지가 이 픽스처에서 가장 위험한 사실이고, 지금까지 그것을 PlayMode 쪽에서 확인하는
    /// 테스트가 <b>한 건도 없었다</b>(EditMode의 <c>SaveIsolationPurgeTests</c>만 있었다).</para>
    /// </summary>
    public sealed class PlayModeSaveIsolationGateTests
    {
        private const string LogPrefix = "[세이브게이트-TEST]";

        /// <summary>격리 폴더에 심는 표식. 저장 파일과 <b>다른 이름</b>이라 저장 로직과 섞이지 않는다.</summary>
        private const string SentinelFileName = "gate-boundary-probe.tmp";

        /// <summary>앞 테스트가 이번 실행에서 심어 둔 표식 경로(없으면 null).</summary>
        private static string s_armedSentinel;

        /// <summary>실제로 <b>경계를 건너며</b> 확인한 횟수.</summary>
        private static int s_boundaryChecks;

        /// <summary>이 픽스처에서 돈 테스트 수 — 경계가 존재할 수 있었는지를 가른다.</summary>
        private static int s_testsRun;

        private static string SaveDirectory => Path.GetDirectoryName(CharacterSaveStore.FilePath);

        private static string SentinelPath => Path.Combine(SaveDirectory, SentinelFileName);

        // ============================================================================
        // 경계 프로브
        // ============================================================================

        [SetUp]
        public void 앞_테스트가_남긴_표식이_사라졌는지_본다()
        {
            s_testsRun++;

            Assert.IsTrue(CharacterSaveStore.IsRedirectedForTesting,
                $"{LogPrefix} 저장 경로가 격리되지 않았습니다 — GlobalPlayModeTestIsolation이 돌지 " +
                "않았습니다. 이 상태에서는 게이트가 (정당하게) 아무것도 하지 않으므로 이 픽스처의 " +
                "판정도 성립하지 않습니다.");

            if (s_armedSentinel == null)
            {
                Debug.Log($"{LogPrefix} 이번 실행에서 아직 표식을 심지 않았습니다 — 이 테스트는 " +
                          "경계를 건너지 않았으므로 검사하지 않습니다(다음 테스트가 검사합니다).");
                return;
            }

            string armed = s_armedSentinel;
            s_armedSentinel = null;              // 한 번의 심기는 한 번만 센다.
            Assert.IsFalse(File.Exists(armed),
                $"{LogPrefix} ★ 앞 테스트가 격리 폴더에 심어 둔 표식이 <b>그대로 남아 있습니다</b> — " +
                $"테스트 사이에 정리가 돌지 않았습니다(BUG-2가 되살아났습니다).\n" +
                $"  표식 = {armed}\n" +
                $"  게이트 진단 = 리프 {PlayModeSaveIsolationGate.LeafTestsStarted}건 / " +
                $"정리 시도 {PlayModeSaveIsolationGate.PurgeAttempts}회 / " +
                $"삭제 {PlayModeSaveIsolationGate.FilesRemoved}개 / " +
                $"건너뜀 {PlayModeSaveIsolationGate.Skipped}회({PlayModeSaveIsolationGate.LastSkipReason}) / " +
                $"실패 {PlayModeSaveIsolationGate.Failures}건({PlayModeSaveIsolationGate.LastFailure})\n" +
                "  ※ Unity Test Framework가 바뀌어 어셈블리 수준 ITestRunCallback이 더는 안 붙는 것일 " +
                "수도 있습니다. 그 경우에도 <b>여기서 멈추는 것</b>이 맞습니다 — 조용히 통과하면 " +
                "오염이 다시 스위트를 타고 흐릅니다.");

            s_boundaryChecks++;
            Debug.Log($"{LogPrefix} 경계 검사 {s_boundaryChecks}회째 — 앞 테스트의 표식이 " +
                      "이 테스트 시작 전에 사라졌습니다(게이트가 살아 있습니다).");
        }

        [TearDown]
        public void 다음_테스트가_볼_수_있게_표식을_심는다()
        {
            if (!CharacterSaveStore.IsRedirectedForTesting) return;   // 실제 폴더에는 아무것도 심지 않는다.

            Directory.CreateDirectory(SaveDirectory);
            string path = SentinelPath;
            File.WriteAllText(path, "PlayModeSaveIsolationGateTests 경계 프로브 — 지워져야 정상입니다.");

            // ★ 양성 대조. 아래 [SetUp]의 "없다"는 부재 단언이라 썩으면 조용히 초록이 된다.
            //   그 짝으로 "심으면 실제로 생긴다"를 같은 픽스처 안에서 못박는다.
            Assert.IsTrue(File.Exists(path),
                $"{LogPrefix} 표식을 심지 못했습니다 — 이 프로브가 죽으면 다음 테스트의 " +
                "\"사라졌다\"는 <b>아무 의미가 없습니다</b>(0건 = 깨끗이 아니라 0건 = 안 쟀다).");

            s_armedSentinel = path;
        }

        /// <summary>
        /// ★ 2026-09-03 (개선 R2) — <b>≥1에서 =(테스트−1)로 조인다.</b>
        ///
        /// <para>옛 판정은 <c>checks &gt;= 1</c>이었다. 테스트가 셋으로 늘어도 그 단언은
        /// <b>1회만 돌아도 통과</b>한다 — 즉 늘린 만큼 조여지지 않는다. 이 저장소가 반복해 온
        /// "느슨한 하한이 실제로는 아무것도 못 잰다"(G4 하한 노후와 같은 병)의 자리다.</para>
        ///
        /// <para>정확한 회계는 <b><c>경계 검사 = 테스트 수 − 1</c></b>이다: 첫 테스트는 앞이 없어
        /// 검사하지 않고, 이후 테스트는 <b>반드시</b> 앞 테스트의 표식을 검사한다.
        /// <c>Assert.AreEqual</c>로 <b>정확히</b> 못박아
        /// 어느 한 경계가 조용히 빠지면 등식이 깨지게 한다.</para>
        ///
        /// <para>★ 필터로 1건만 도는 실행은 경계가 <b>구조적으로 없다</b> — 그 경우를
        /// "검사했다"로 착각하지 않도록 등식은 그대로 성립시키되(0 = 1−1) 로그로 <b>증명 실패</b>를
        /// 분명히 남긴다. 전량 실행에서만 이 픽스처는 게이트 생존을 증명한다.</para>
        /// </summary>
        [OneTimeTearDown]
        public void 경계가_실제로_두_번_이상_검사됐는지_못박는다()
        {
            // 남은 표식은 치운다 — 다음 픽스처에 파일을 넘기지 않는다.
            if (CharacterSaveStore.IsRedirectedForTesting && File.Exists(SentinelPath))
            {
                File.Delete(SentinelPath);
            }

            int tests = s_testsRun, checks = s_boundaryChecks;
            s_armedSentinel = null;
            s_boundaryChecks = 0;
            s_testsRun = 0;

            if (tests <= 1)
            {
                Debug.Log($"{LogPrefix} 테스트가 {tests}건만 돌아(필터 실행) 경계가 존재하지 않았습니다 — " +
                          "이번 실행은 게이트 생존을 <b>증명하지 못했습니다</b>. 전량 실행에서 확인하십시오.");
                return;
            }

            Assert.AreEqual(tests - 1, checks,
                $"{LogPrefix} ★ 경계 회계가 맞지 않습니다 — 테스트 {tests}건이면 경계 검사는 정확히 " +
                $"{tests - 1}회여야 하는데 {checks}회였습니다.\n" +
                "  · 적으면: [TearDown]이 표식을 못 심었거나 [SetUp]이 검사를 건너뛰었습니다 " +
                "(= 게이트 생존 근거가 그만큼 비었습니다).\n" +
                "  · 많으면: 표식 하나를 두 번 셌습니다(s_armedSentinel 해제 로직이 깨졌습니다).\n" +
                $"  게이트 진단 = 리프 {PlayModeSaveIsolationGate.LeafTestsStarted}건 / " +
                $"정리 {PlayModeSaveIsolationGate.PurgeAttempts}회 / " +
                $"건너뜀 {PlayModeSaveIsolationGate.Skipped}회 / " +
                $"실패 {PlayModeSaveIsolationGate.Failures}건");

            Assert.GreaterOrEqual(checks, 2,
                $"{LogPrefix} ★ 전량 실행인데 경계를 {checks}회만 건넜습니다 — 이 픽스처는 " +
                "경계를 <b>최소 2회</b> 건너도록 테스트를 셋 두고 있습니다(개선 R2 항목 2). " +
                "테스트가 지워졌다면 그 사실을 보고에 적고, 아니라면 프로브가 죽은 것입니다.");

            Debug.Log($"{LogPrefix} 테스트 {tests}건 · 경계 검사 {checks}회(= {tests}−1) — " +
                      "게이트 생존을 실제 파일로 두 번 이상 확인했습니다.");
        }

        // ============================================================================
        // 본체
        // ============================================================================

        /// <summary>이 테스트가 시작될 때 격리 폴더가 실제로 비어 있었는가.
        /// 필터로 한 건만 돌아도 성립하는 검사다(경계 프로브와 사정거리가 다르다).</summary>
        [Test]
        public void 이_테스트가_시작될_때_격리_저장_파일이_없다()
        {
            Assert.IsFalse(File.Exists(CharacterSaveStore.FilePath),
                $"{LogPrefix} ★ 테스트 시작 시점에 격리 저장 파일이 이미 있습니다 — 앞 테스트가 " +
                $"박제한 전역 모델(레벨·착용 장비·펫)을 이 테스트의 씬 로드가 그대로 읽게 됩니다.\n" +
                $"  파일 = {CharacterSaveStore.FilePath}\n" +
                $"  게이트 = 정리 시도 {PlayModeSaveIsolationGate.PurgeAttempts}회 / " +
                $"삭제 {PlayModeSaveIsolationGate.FilesRemoved}개 / " +
                $"실패 {PlayModeSaveIsolationGate.Failures}건");

            int left = Directory.Exists(SaveDirectory) ? Directory.GetFiles(SaveDirectory).Length : -1;
            Debug.Log($"{LogPrefix} 시작 시점 격리 폴더 파일 수 = {left}개 ({SaveDirectory}) " +
                      "— -1은 폴더 자체가 없다는 뜻입니다(그것도 정상: 아직 아무도 저장하지 않았다).");
        }

        /// <summary>게이트가 <b>모든 리프 테스트</b>에서 돌았고, 조용히 건너뛰거나 예외를 삼키지 않았는가.
        /// <para>회계 항등식으로 본다 — <c>리프 = 정리 시도 + 건너뜀</c>. 어느 한쪽이 새면 합이 어긋난다.</para></summary>
        [Test]
        public void 게이트는_리프마다_돌고_조용히_건너뛰지_않는다()
        {
            Assert.AreEqual(0, PlayModeSaveIsolationGate.Failures,
                $"{LogPrefix} ★ 게이트가 정리 중 예외를 {PlayModeSaveIsolationGate.Failures}건 삼켰습니다 " +
                $"— 마지막 사유: {PlayModeSaveIsolationGate.LastFailure}");

            Assert.AreEqual(0, PlayModeSaveIsolationGate.Skipped,
                $"{LogPrefix} ★ PlayMode 실행인데 게이트가 " +
                $"{PlayModeSaveIsolationGate.Skipped}회 건너뛰었습니다 — 사유: " +
                $"{PlayModeSaveIsolationGate.LastSkipReason}. 건너뛴 그 테스트는 앞 테스트의 " +
                "저장 파일을 물려받은 채 돌았습니다.");

            Assert.AreEqual(PlayModeSaveIsolationGate.LeafTestsStarted,
                PlayModeSaveIsolationGate.PurgeAttempts + PlayModeSaveIsolationGate.Skipped,
                $"{LogPrefix} 회계가 맞지 않습니다 — 리프 {PlayModeSaveIsolationGate.LeafTestsStarted}건 ≠ " +
                $"정리 {PlayModeSaveIsolationGate.PurgeAttempts} + 건너뜀 {PlayModeSaveIsolationGate.Skipped}. " +
                "게이트가 어떤 경로로 조용히 빠져나가고 있습니다.");

            Assert.IsNotNull(PlayModeSaveIsolationGate.LastPurgedTestFullName,
                $"{LogPrefix} 게이트가 이번 실행에서 정리를 한 번도 기록하지 않았습니다.");
            StringAssert.Contains(nameof(게이트는_리프마다_돌고_조용히_건너뛰지_않는다),
                PlayModeSaveIsolationGate.LastPurgedTestFullName,
                $"{LogPrefix} 마지막으로 정리가 돈 테스트가 이 테스트가 아닙니다 " +
                $"({PlayModeSaveIsolationGate.LastPurgedTestFullName}) — 게이트가 <b>이 테스트 직전</b>에 " +
                "돌지 않았다는 뜻입니다.");

            Debug.Log($"{LogPrefix} 리프 {PlayModeSaveIsolationGate.LeafTestsStarted}건 / " +
                      $"정리 {PlayModeSaveIsolationGate.PurgeAttempts}회 / " +
                      $"삭제 {PlayModeSaveIsolationGate.FilesRemoved}개 / 실패 0건.");

            // ★ 여기서 읽는 값은 <b>이 테스트가 도는 시점</b>의 중간 회계다(실측 424/591).
            //   <b>최종</b> 회계(591/591/0/0)는 RunFinished의 정규형 로그에만 남고, 그것은
            //   docs/verify/regress.sh의 <b>G10</b>이 러너 xml과 대조해 게이트로 건다.
            //   이 주석이 그 분업의 유일한 접점이다 — 한쪽만 보고 "게이트가 살아 있다"고 말하지 마라.
        }

        // ============================================================================
        // ★ 삭제의 사정거리 (2026-09-03, 개선 R2 항목 2 — 세 번째 리프)
        // ============================================================================

        /// <summary>
        /// 게이트의 삽이 <b>어디를 파는가</b>. 이번 실행에서 이 게이트는 파일을 <b>수백 번</b> 지운다 —
        /// 그 대상이 격리 폴더가 아니라면 그것은 <b>절대 불변 원칙 3 위반</b>이고, 테스트가 개발자의
        /// 진짜 저장 파일을 지우는 사고다.
        ///
        /// <para><b>왜 여기 있어야 하나.</b> 같은 성격의 검사가 EditMode에는 있다
        /// (<c>Tests/EditMode/SaveIsolationPurgeTests</c>의 가드 2종). 그런데 <b>실제로 591번
        /// 삭제가 도는 쪽은 PlayMode</b>이고, 이쪽에는 <b>한 건도 없었다</b>. 위험이 큰 쪽이
        /// 비어 있었다.</para>
        ///
        /// <para><b>양성/음성 대조.</b> "임시 캐시 안이다"만 단언하면 두 뿌리가 우연히 같을 때
        /// 공허하게 참이 된다. 그래서 <b>두 뿌리가 실제로 다른가</b>를 먼저 단언하고,
        /// <b>개발자 폴더 아래가 아니다</b>를 짝으로 둔다.</para>
        ///
        /// <para>필터로 한 건만 돌아도 성립한다 — 경계 프로브와 사정거리가 다르다.</para>
        /// </summary>
        [Test]
        public void 게이트가_지우는_폴더는_임시_캐시_안이고_개발자_폴더가_아니다()
        {
            Assert.IsTrue(CharacterSaveStore.IsRedirectedForTesting,
                $"{LogPrefix} 저장 경로가 격리되지 않았습니다 — 이 상태에서 게이트가 돌면 개발자의 " +
                "진짜 파일을 지웁니다(지금은 가드가 막고 있지만, 그 가드에 의존하지 않고 여기서도 확인합니다).");

            string temp = Path.GetFullPath(Application.temporaryCachePath)
                .TrimEnd(Path.DirectorySeparatorChar);
            string persistent = Path.GetFullPath(Application.persistentDataPath)
                .TrimEnd(Path.DirectorySeparatorChar);
            string dir = Path.GetFullPath(SaveDirectory);

            // 전제(양성 대조) — 두 뿌리가 같으면 아래 두 단언이 서로를 부정하며 공허해진다.
            Assert.AreNotEqual(temp, persistent,
                $"{LogPrefix} 임시 캐시와 영구 저장 뿌리가 같은 경로입니다({temp}) — " +
                "이 플랫폼에서는 이 검사가 아무것도 가르지 못합니다. 판정을 신뢰하지 마십시오.");

            Assert.IsTrue(dir.StartsWith(temp + Path.DirectorySeparatorChar, System.StringComparison.Ordinal),
                $"{LogPrefix} ★ 게이트가 지우는 폴더가 임시 캐시 <b>밖</b>입니다.\n" +
                $"  대상 = {dir}\n  임시 캐시 = {temp}\n" +
                "이 상태로 스위트가 돌면 격리가 아니라 <b>삭제 사고</b>입니다.");

            Assert.IsFalse(dir.StartsWith(persistent + Path.DirectorySeparatorChar, System.StringComparison.Ordinal),
                $"{LogPrefix} ★ 게이트가 지우는 폴더가 <b>개발자의 영구 저장 폴더</b> 아래입니다.\n" +
                $"  대상 = {dir}\n  영구 저장 = {persistent}\n" +
                "절대 불변 원칙 3 위반입니다 — 리디렉션이 이름만 바뀌고 자리는 그대로인 상태입니다.");

            Debug.Log($"{LogPrefix} 삭제 사정거리 확인 — 대상 {dir} 은(는) 임시 캐시({temp}) 안이고 " +
                      $"영구 저장({persistent}) 밖입니다. 이번 실행 누적 삭제 " +
                      $"{PlayModeSaveIsolationGate.FilesRemoved}개.");
        }
    }
}
