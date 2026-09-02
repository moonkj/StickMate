using System;
using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(PlayModeSaveIsolationGate))]

/// <summary>
/// ★★ 2026-09-02 <c>test-engineer</c> — <b>테스트마다</b> 격리 저장 폴더를 비운다 (BUG-2).
///
/// ============================================================================
/// 이 파일이 잡는 실패 — "실행 <b>도중</b>의 축적"
/// ============================================================================
/// <see cref="GlobalPlayModeTestIsolation"/>는 저장 경로를 임시 폴더로 옮기고 <b>딱 한 번</b>
/// (<c>[OneTimeSetUp]</c>) 비운다. 그래서 <b>실행 간 이월</b>은 막혔지만 <b>실행 도중 축적</b>은
/// 그대로였다:
/// <list type="number">
///   <item>PlayMode 픽스처 10개가 <c>CharacterSaveStore.Save()</c>를 직접 부른다.
///         그 한 번이 <b>그 순간의 전역 모델 전체</b>를 파일에 박제한다.</item>
///   <item>다음 씬 로드에서 프리팹의 <c>CharacterProgressionDirector</c>가 <b><c>Start()</c></b>에서
///         <c>Load()</c>를 부르고(<c>Interaction/CharacterProgressionDirector.cs</c> — 2026-09-02 정정:
///         오래도록 "Awake"라고 적혀 있었다), 박제된 값(레벨·착용 장비·펫)이 <b>다른 픽스처</b>로 흘러든다.</item>
/// </list>
/// <b>실측(debugger, 2026-09-02)</b>: <c>c1-play</c>가 씬 로드 430회 중
/// "없음 161 → 불러옴 278"로 <b>도중에 뒤집혔고</b>, 전이 지점이 <c>FullscreenPanelRetreatTests:123</c>,
/// 그 결과 <c>스틱메이트 Lv.127</c>이 로그에 <b>505회</b> 찍혔다.
///
/// ============================================================================
/// 왜 <c>[SetUpFixture]</c>가 아니라 어셈블리 콜백인가
/// ============================================================================
/// NUnit의 <c>[SetUpFixture]</c>는 <c>[OneTimeSetUp]</c>/<c>[OneTimeTearDown]</c>만 가진다 —
/// 구조적으로 "테스트마다"를 표현할 수 없다. 픽스처 40여 개에 같은 줄을 손으로 붙이는 방법은
/// <b>빠뜨리는 것이 정상</b>이므로 쓰지 않는다.
///
/// <para>★ <b>어셈블리 수준 <c>ITestAction</c>은 쓸 수 없다</b>(실측으로 확인). Unity Test Framework
/// 1.6의 <c>BeforeAfterTestCommandBase.GetTestActions</c>는 <c>test.Parent</c>를 타고 올라가며
/// <c>parent.TypeInfo</c>가 <b>있는</b> 노드의 특성만 모은다. 어셈블리 노드는 <c>TypeInfo</c>가
/// null이라 <c>[assembly: ...]</c> 액션은 <b>영원히 안 붙는다</b> — 붙은 것처럼 조용히 초록이 된다.
/// 그래서 <c>ITestRunCallback</c>(<c>TestRunCallbackListener</c>가 <b>모든 어셈블리</b>의 특성을
/// 긁어 간다)을 쓴다. 이건 EditMode/PlayMode 공통 태스크 리스트
/// (<c>RegisterTestRunCallbackEventsTask</c>)에 등록돼 있다.</para>
///
/// ============================================================================
/// 절대 지키는 것
/// ============================================================================
/// <list type="bullet">
///   <item><b>절대 예외를 던지지 않는다.</b> <c>TestRunCallbackListener.InvokeAllCallbacks</c>는
///     예외를 <c>LogException</c>한 뒤 <b>다시 던진다</b> — 여기서 던지면 러너 자체가 무너진다.
///     대신 <see cref="Failures"/>에 적고, <c>PlayModeSaveIsolationGateTests</c>가 그것을 단언한다.</item>
///   <item><b>PlayMode가 아니면 아무것도 하지 않는다.</b> 이 어셈블리는 EditMode 실행에서도
///     도메인에 로드되므로 콜백이 함께 불린다. EditMode의 저장 파일 수명은 <b>이 게이트의 소관이
///     아니다</b>(그쪽 지속성 테스트는 파일이 남아 있어야 하는 것이 있다).</item>
///   <item><b>리디렉션돼 있지 않으면 손대지 않는다.</b> 개발자의 실제 저장 파일은
///     절대 불변 원칙 3의 대상이다. 판정과 삭제의 사정거리는
///     <see cref="GlobalPlayModeTestIsolation.PurgeGuarded"/> 한 곳에만 있다 —
///     규칙을 두 벌로 만들지 않는다.</item>
/// </list>
///
/// ============================================================================
/// ★ 이 게이트가 <b>살아 있는지</b>는 카운터로 증명하지 않는다
/// ============================================================================
/// 정적 카운터는 에디터 도메인에 남아 <b>앞선 실행의 값</b>일 수 있다 — "0보다 크다"는
/// 조용히 참이 되는 종류의 단언이다. 실제 증명은 <c>PlayModeSaveIsolationGateTests</c>가
/// <b>이번 실행에 심은 표식 파일</b>이 다음 테스트 시작 전에 사라지는지로 한다.
/// 아래 카운터는 <b>진단용</b>이며 실패 메시지를 읽을 수 있게 만드는 것이 목적이다.
/// </summary>
public sealed class PlayModeSaveIsolationGate : ITestRunCallback
{
    private const string LogPrefix = "[세이브게이트]";

    /// <summary>리프 테스트(스위트가 아닌 실제 테스트) 시작 횟수.</summary>
    public static int LeafTestsStarted { get; private set; }

    /// <summary>가드를 통과해 실제로 정리를 시도한 횟수.</summary>
    public static int PurgeAttempts { get; private set; }

    /// <summary>지운 파일 누계.</summary>
    public static int FilesRemoved { get; private set; }

    /// <summary>PlayMode가 아니거나 리디렉션이 꺼져 있어 <b>일부러</b> 건너뛴 횟수.</summary>
    public static int Skipped { get; private set; }

    /// <summary>정리 중 튀어나온 예외 건수. 0이 아니면 게이트가 제 일을 못 하고 있다.</summary>
    public static int Failures { get; private set; }

    /// <summary>마지막 실패 사유(있으면). 실패 메시지에 그대로 실어 보낸다.</summary>
    public static string LastFailure { get; private set; }

    /// <summary>마지막으로 정리를 돌린 테스트의 전체 이름.</summary>
    public static string LastPurgedTestFullName { get; private set; }

    /// <summary>마지막으로 건너뛴 이유(진단용).</summary>
    public static string LastSkipReason { get; private set; }

    public void RunStarted(ITest testsToRun)
    {
        LeafTestsStarted = 0;
        PurgeAttempts = 0;
        FilesRemoved = 0;
        Skipped = 0;
        Failures = 0;
        LastFailure = null;
        LastPurgedTestFullName = null;
        LastSkipReason = null;
        Debug.Log($"{LogPrefix} 어셈블리 콜백이 붙었습니다 — 이번 실행의 모든 리프 테스트 <b>직전</b>에 " +
                  "격리 저장 폴더를 비웁니다(BUG-2: 실행 도중 축적).");
    }

    public void TestStarted(ITest test)
    {
        if (test == null || test.IsSuite) return;   // 어셈블리/클래스 노드는 경계가 아니다.
        LeafTestsStarted++;
        PurgeBefore(test.FullName);
    }

    public void TestFinished(ITestResult result) { }

    public void RunFinished(ITestResult testResults)
    {
        Debug.Log($"{LogPrefix} 실행 종료 — 리프 테스트 {LeafTestsStarted}건, 정리 시도 {PurgeAttempts}회, " +
                  $"삭제 {FilesRemoved}개, 건너뜀 {Skipped}회(마지막 사유: {LastSkipReason ?? "없음"}), " +
                  $"실패 {Failures}건{(LastFailure == null ? string.Empty : " — " + LastFailure)}.");
    }

    /// <summary>★ 여기서 던지면 러너가 무너진다. 모든 경로에서 예외를 삼키고 기록만 한다.</summary>
    private static void PurgeBefore(string testFullName)
    {
        try
        {
            if (!Application.isPlaying)
            {
                Skipped++;
                LastSkipReason = "PlayMode 실행이 아닙니다(EditMode 실행에서도 이 어셈블리가 로드됩니다).";
                return;
            }
            if (!StickMate.Core.CharacterSaveStore.IsRedirectedForTesting)
            {
                Skipped++;
                LastSkipReason = "저장 경로가 리디렉션돼 있지 않습니다 — 개발자의 실제 파일일 수 있어 " +
                                 "손대지 않습니다(절대 불변 원칙 3).";
                return;
            }

            PurgeAttempts++;
            FilesRemoved += GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
            LastPurgedTestFullName = testFullName;
        }
        catch (Exception e)
        {
            Failures++;
            LastFailure = $"{testFullName} 직전 정리에서 {e.GetType().Name}: {e.Message}";
            // ★ Debug.LogError를 쓰지 않는다 — 러너가 그것을 <b>임의의 테스트</b>의 실패로 붙여
            //   진짜 원인을 가린다. 대신 카운터로 남기고 전용 테스트가 단언한다.
            Debug.Log($"{LogPrefix} ★ 정리 실패 {Failures}건째 — {LastFailure}");
        }
    }
}
