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
///     도메인에 로드되므로 콜백이 함께 불린다.
///
///     <para>★★ 2026-09-03 (개선 R2) — <b>이 근거를 실측으로 교체했다.</b> 옛 문장은
///     <i>"그쪽 지속성 테스트는 파일이 남아 있어야 하는 것이 있다"</i>였는데, 그것은
///     <b>아무도 재 본 적 없는 짐작</b>이었다. 근거 없는 예외는 이 저장소가 반복해 온
///     "조용한 초록"의 자리다 — 그래서 <b>세는 것</b>으로 바꾼다.</para>
///
///     <para><b>실측(2026-09-03)</b>: BUG-2가 타는 축적 경로는
///     <c>Interaction/CharacterProgressionDirector.Start()</c>의 <c>Load()</c> 하나다. 그 훅은
///     자기 로그에 <c>"저장 파일="</c>을 찍는다. 그 니들을 러너 로그에서 세면:
///     <list type="bullet">
///       <item><b>EditMode 로그 4종 전부 0건</b> — <c>coder-r8_edit</c> · <c>qa-r7_edit</c> ·
///             <c>coder-grabline_edit</c> · <c>coder-grabline-poscontrol-EXPECTED-RED_edit</c></item>
///       <item><b>PlayMode 로그 4종은 전부 0이 아니다</b> — 31 / 458 / 440 / 440
///             (<c>coder-r8_play</c> · <c>qa-r7_play</c> · <c>te-r2_play</c> · <c>qa-r5_play</c>)
///             ← 같은 니들의 <b>양성 대조</b>. 이게 없으면 위 0건은 "깨끗"이 아니라 "니들이 죽었다"와
///             구분되지 않는다.</item>
///     </list>
///     즉 EditMode는 프리팹/씬을 띄우지 않아 그 <c>Start()</c>가 <b>한 번도 돌지 않는다</b>.
///     "리프마다 비워야 하는" 축적 경로가 <b>구조적으로 없다</b> — 그래서 건너뛴다.
///     (양성 대조가 없으면 이 0건은 "깨끗"이 아니라 "니들이 죽었다"와 구분되지 않는다.)</para>
///
///     <para><b>그리고 EditMode에서 켜는 것은 공짜가 아니다.</b>
///     <see cref="GlobalPlayModeTestIsolation.PurgeGuarded"/>는 폴더 <b>바로 아래 파일을 전부</b>
///     지우는데, EditMode의 저장 폴더에는 <c>CharacterSaveStore.TempFilePath</c> ·
///     <c>PreviousGenerationPath</c> · <c>NewerVersionBackupPath</c> 같은 <b>형제 파일</b>이 함께 산다
///     (<c>SavePreviousGenerationTests</c> · <c>SaveDowngradeGuardTests</c>가 만든다).
///     리프마다 지우면 그 파일들의 수명이 바뀐다.
///     ★ 실제로 깨지는 EditMode 테스트가 있는지는 <b>미확인</b>이다 — 켤 이유가 생기면
///     그것부터 재라. 지금은 <b>켤 이유 자체가 위 0건으로 없다</b>.</para></item>
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

    /// <summary>
    /// ★★ 2026-09-03 (개선 R2) — <b>이 줄이 게이트의 유일한 종점 회계다.</b>
    ///
    /// <para><b>무엇이 뚫려 있었나.</b> 게이트 생존은 <c>PlayModeSaveIsolationGateTests</c>가
    /// 단언하는데, 그 테스트는 <b>스위트 도중 한 시점</b>에 돈다(실측: 리프 424/591 지점).
    /// 최종 회계 591/591/0/0은 <b>이 <c>Debug.Log</c>에만</b> 있었고 <b>어떤 테스트도 읽지 않았다</b> —
    /// 즉 <b>게이트가 500번째 리프에서 죽어도 스위트는 초록</b>이었다.
    /// 러너가 끝난 뒤에 도는 단언은 NUnit 안에 둘 자리가 없다(콜백은 <b>예외를 던지면 안 된다</b> —
    /// 위 "절대 지키는 것" 참고). 그래서 <b>바깥</b>에서 읽게 만든다.</para>
    ///
    /// <para><b>정규형으로 못박는 이유.</b> 산문 줄은 사람이 읽으라고 남기지만, 기계가 파싱하기에는
    /// 썩기 쉽다(문구 한 글자만 바뀌어도 <c>grep</c>이 0건이 되고 — 그 0건은 "게이트가 죽었다"와
    /// <b>똑같이 생겼다</b>). docs/TEAM.md 거짓 통과 13번째 형태가 정확히 그것이었다:
    /// <i>"형태만 세지 말고 값이 채워졌는가까지 봐라. 정규형으로 못박아라."</i>
    /// 그래서 <c>회계 leaf=&lt;정수&gt; purge=&lt;정수&gt; skip=&lt;정수&gt; removed=&lt;정수&gt; fail=&lt;정수&gt;</c>
    /// 형태를 <b>한 줄로</b> 낸다. <c>docs/verify/regress.sh</c>의 <b>G10</b>이 이 줄을 파싱해
    /// <list type="bullet">
    ///   <item>회계 항등식 <c>leaf == purge + skip</c></item>
    ///   <item><c>fail == 0</c></item>
    ///   <item>플랫폼별 기대(PlayMode: <c>skip == 0</c> / EditMode: <c>purge == 0</c>)</item>
    ///   <item>★ <b>러너 xml의 <c>testcasecount</c>/<c>total</c>과 독립적으로 일치</b></item>
    /// </list>
    /// 를 게이트로 건다. 마지막 항목이 이 장치의 값어치다 — <b>같은 실행을 두 개의 다른 자로</b>
    /// 잰다(하나는 Unity 로그, 하나는 NUnit xml). 둘이 갈라지면 그 자체가 사건이다.</para>
    ///
    /// <para>★ 줄을 <b>지우거나 형식을 바꾸면</b> G10이 "줄이 없다"로 <b>실패</b>한다(조용히 초록이
    /// 되지 않는다). 형식을 바꿔야 하면 <c>regress.sh</c>의 정규식을 같은 커밋에서 함께 고쳐라.</para>
    /// </summary>
    public void RunFinished(ITestResult testResults)
    {
        Debug.Log($"{LogPrefix} 실행 종료 — 리프 테스트 {LeafTestsStarted}건, 정리 시도 {PurgeAttempts}회, " +
                  $"삭제 {FilesRemoved}개, 건너뜀 {Skipped}회(마지막 사유: {LastSkipReason ?? "없음"}), " +
                  $"실패 {Failures}건{(LastFailure == null ? string.Empty : " — " + LastFailure)}.");

        // ★ 기계가 읽는 정규형. 위 산문 줄과 <b>같은 필드에서</b> 나오지만, 읽는 쪽(regress.sh)은
        //   이것을 러너 xml이라는 <b>다른 자</b>와 대조한다 — 그래서 "생성기와 검사기가 같이 틀린다"에
        //   걸리지 않는다(docs/TEAM.md 거짓 통과 신형).
        Debug.Log($"{LogPrefix} 회계 leaf={LeafTestsStarted} purge={PurgeAttempts} " +
                  $"skip={Skipped} removed={FilesRemoved} fail={Failures}");
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
