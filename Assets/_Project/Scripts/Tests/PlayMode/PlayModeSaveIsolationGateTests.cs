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

        [OneTimeTearDown]
        public void 경계가_실제로_한_번은_검사됐는지_기록한다()
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

            if (tests >= 2)
            {
                Assert.GreaterOrEqual(checks, 1,
                    $"{LogPrefix} 이 픽스처에서 테스트가 {tests}건 돌았는데 <b>경계 검사가 한 번도</b> " +
                    "일어나지 않았습니다 — [SetUp]/[TearDown] 프로브가 죽었습니다. " +
                    "게이트가 살아 있다는 근거가 이번 실행에 하나도 없습니다.");
                Debug.Log($"{LogPrefix} 테스트 {tests}건 · 경계 검사 {checks}회 — 게이트 생존 확인.");
            }
            else
            {
                Debug.Log($"{LogPrefix} 테스트가 {tests}건만 돌아(필터 실행) 경계가 존재하지 않았습니다 — " +
                          "이번 실행은 게이트 생존을 <b>증명하지 못했습니다</b>. 전량 실행에서 확인하십시오.");
            }
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
        }
    }
}
