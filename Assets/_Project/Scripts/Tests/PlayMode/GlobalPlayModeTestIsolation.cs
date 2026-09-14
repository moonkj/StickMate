using NUnit.Framework;
using StickMate.Core;
using StickMate.Platform;
using UnityEngine;

/// <summary>
/// ★ PlayMode 스위트 전체의 저장 파일 격리 (2026-08-31, R3 Blocker 2 동반 조치).
///
/// <para><b>왜 필요한가.</b> PlayMode 테스트는 Stickman 프리팹을 통째로 띄운다. 그 프리팹에는
/// <c>CharacterProgressionDirector</c>가 붙어 있고 그것이 <b><c>Start()</c></b>에서
/// <see cref="CharacterSaveStore.Load"/>를 부른다
/// (<c>Scripts/Interaction/CharacterProgressionDirector.cs</c>의 <c>Start()</c> 안 — ★ 2026-09-02 정정:
/// 이 문단은 오래도록 <i>"Awake에서"</i>라고 적고 있었다. 훅 이름이 틀리면 격리가 도는 시점을 잘못
/// 계산하게 되고, 실제로 <c>[OneTimeSetUp]</c>과 <c>Awake</c> 사이만 막으면 된다는 오해를 낳는다).
/// 그래서 지금까지 모든 PlayMode 테스트가 <b>테스트를 돌리는 사람의 개인 저장 파일</b>을 읽고
/// 있었다. 실제로 개발자 파일의 <c>characterScale 0.35</c>가 매 씬 로드마다 복원되면서 하루치
/// PlayMode 전체가 0.35배 캐릭터로 돌았고(로그 146회), 네 명이 같은 실패를 보고도 원인을 프리팹으로
/// 오인했다 — "내 변경을 되돌려도 실패가 그대로다"라는 네거티브 컨트롤이 <b>참이지만 무의미</b>했기
/// 때문이다(전원이 같은 오염원을 읽고 있었으므로).</para>
///
/// <para><b>왜 [SetUpFixture]인가.</b> 네임스페이스 없는 <c>[SetUpFixture]</c>는 NUnit이 이 어셈블리의
/// <b>모든 테스트보다 먼저</b> 딱 한 번 실행한다. 테스트 100여 개에 같은 한 줄을 붙이는 것보다
/// 빠뜨릴 여지가 없다. 실제로 실행됐는지는 아래 로그 한 줄로 확인할 수 있다(가정으로 두지 않는다).</para>
///
/// <para><b>왜 억제가 아니라 경로 재지정인가.</b> 로드를 막아 버리면 실제 디스크 왕복을 검증하는
/// 지속성 테스트가 통째로 죽는다. 경로만 임시 폴더로 옮기면 그 테스트들은 한 글자도 안 고친 채
/// 그대로 돌고, 프리팹이 자동으로 부르는 Load()는 빈 폴더를 만나 "새 캐릭터"로 출발한다.
/// 개발자의 실제 파일은 읽지도 쓰지도, 지우지도 않는다(CLAUDE.md 절대 불변 원칙 3).</para>
///
/// <para>★ 2026-09-14 — <b>설정 저장소(PlayerPrefs)의 한 비트도 같은 병이었다.</b> 톱니 부채꼴 「최초 1회 안내 봤음」
/// 한 비트를 스위트 전체에서 메모리 저장소로 돌리고, 끝에서 그 비트의 실제 저장소 접근 0과 주입 누수 0을 단언한다
/// (아래 <see cref="RedirectSaveFile"/>의 2026-09-14 절). <b>설정 저장소 전체를 격리하는 것은 아니다</b> — Unity 엔진이
/// 에디터 설정 파일에 스스로 쓰는 키는 이 계수 밖이다.</para>
/// </summary>
[SetUpFixture]
public sealed class GlobalPlayModeTestIsolation
{
    // 계수는 정적이라 같은 에디터 도메인에서 앞선 실행(EditMode 계수 자기 검증 등)이 올린 값이 남아 있을 수 있다.
    // 그래서 절댓값이 아니라 이 스위트 동안의 <b>증가분</b>으로 잰다.
    private int _realOnboardingReadsAtStart;
    private int _realOnboardingWritesAtStart;

    // 스위트가 넣은 그 인스턴스 — 끝에서 「아직 이것이 쓰이고 있는가」를 같은 인스턴스로 확인한다.
    private StickMate.Interaction.IGearMenuOnboardingSeenStore _suiteOnboardingStore;

    [OneTimeSetUp]
    public void RedirectSaveFile()
    {
        // ★★ 2026-09-03 (dev-platform 신고 → test-engineer 개선 R2) — <b>가장 먼저</b> 프로브를 걷는다.
        // 이 파일은 저장 파일만 격리하고 <b>정적 프로브 오버라이드는 하나도 걷지 않았다</b>. 그런데
        // 톱니(예약 띠 위 배치)·부채꼴이 이제 ReservedEdgeProbe를 읽으므로, 네 방향 오버라이드가
        // 새면 그 둘의 위치가 흔들린다. 지금은 유일한 사용처(TodoPostItReservedTopBarTests)가
        // 스스로 되돌려 안전하지만, "관례로 지키는 안전"은 이 저장소에서 이미 여러 번 깨졌다.
        //
        // ★ 순서 — 반드시 <b>ReservedEdgeProbe</b>의 것을 부른다(계약: ReservedEdgeProbe.cs 클래스 문서).
        //   ReservedEdgeProbe.ResetForTests()는 <b>상단 프로브도 함께</b> 걷지만, 반대로
        //   ReservedTopBarProbe.ResetForTests()만 부르면 <b>네 방향 오버라이드는 안 걷힌다</b>.
        //
        // ★ 그리고 <b>세우기 전에도</b> 부르는 이유는 저장 폴더를 비우는 이유와 똑같다 —
        //   에디터 도메인은 실행 사이에 살아남으므로 앞선 실행의 정적 오버라이드가 그대로 남아 있을
        //   수 있다. "옮기기만 하면 격리가 아니다"가 정적 상태에도 그대로 적용된다.
        ReservedEdgeProbe.ResetForTests();

        // ============================================================================
        // ★★ 2026-09-05 — 「대기 톱니」 게이트 우회 (coder-ui 라운드)
        // ============================================================================
        // 이날 톱니는 <b>평상시에 뜨지 않게</b> 됐다. 평소 진입점은 캐릭터 우클릭이고, 톱니는
        // 「캐릭터가 화면에서 사라진 동안」(사용자 명시 숨김 / 가출)에만 되돌아올 문으로 나타난다.
        //
        // 그런데 이 어셈블리의 톱니 픽스처들(드래그 · 부채꼴 진입 · 호버 이름표 · 포스트잇 회피 ·
        // 위치 소유권 · 배타 모달)은 전부 <b>「톱니가 보인다」를 전제로</b> 쓰였고, 그 전제는
        // <b>그 테스트들의 주제가 아니다</b>. 여기서 게이트를 우회하지 않으면 60여 건이
        // 「톱니를 못 찾음」이라는 <b>같은 한 가지 이유</b>로 빨개지고, 그 빨강은 각 테스트가
        // 원래 지키던 계약에 대해 <b>아무것도 말해 주지 않는다</b>.
        //
        // ★★ <b>그 대신 게이트 자체는 다른 자리에서 잠긴다</b> —
        //   Tests/EditMode/RightClickFanGateTests.대기_톱니는_사용자숨김과_가출_두_상태를_본다 가
        //   조건식(사용자 숨김 OR 가출, 그리고 설정 토글 <b>부재</b>)을 소스에서 못박는다.
        //   ⇒ 여기서 우회되는 것은 <b>「보이는가」의 런타임 관측 하나뿐</b>이다.
        //
        // ★ 게이트의 <b>런타임 거동</b>을 재는 테스트를 새로 쓴다면 그 픽스처의 SetUp에서
        //   <c>SetStandbyGateBypassedForTests(false)</c>로 <b>스스로 되돌리고</b>, TearDown에서
        //   다시 true로 놓아라. 그러지 않으면 그 테스트는 우회된 세상을 재게 된다.
        StickMate.Interaction.InfoGearIconWidget.SetStandbyGateBypassedForTests(true);
        Debug.Log("[테스트격리] 「대기 톱니」 게이트를 우회합니다 — 이 어셈블리의 톱니 픽스처들은 " +
            "톱니가 상시로 보이던 시절에 쓰였고 그 전제는 그 테스트들의 주제가 아닙니다. " +
            "게이트 조건식 자체는 EditMode(RightClickFanGateTests)가 소스로 잠급니다.");

        // ============================================================================
        // ★★ 2026-09-14 — 톱니 부채꼴 「최초 1회 안내 봤음」을 스위트 전체에서 메모리 저장소로 (coder)
        // ============================================================================
        // 이 한 비트는 프로덕션 GearRadialMenuWidget.Expand() 한가운데서 읽고 쓴다. 그래서 부채꼴을
        // 여는 픽스처가 돌기만 해도 개발자 기계의 실제 설정 저장소(macOS 에디터 plist / Windows
        // 레지스트리)를 읽고 1을 적었다. GearMenuOnboardingHintTests의 옛 「기억했다 되돌리기」는 그 픽스처
        // 하나만 감싸서, 먼저 돈 다른 픽스처가 오염시킨 값을 「원래 값」으로 기억해 되돌렸다.
        //
        // ★ 초기값을 「봤음」으로 두는 이유 — 안내가 떠 있으면 호버 이름표가 물러나고 자동 접힘이 멈춘다.
        //   그건 다른 픽스처들의 주제가 아니고, 「안 봤음」으로 두면 스위트에서 <b>처음으로</b> 부채꼴을 여는
        //   픽스처만 다른 세상을 재게 된다(실행 순서 의존). 개발자 기계의 실제 값도 1이었으므로
        //   이 픽스처들의 기존 초록은 「봤음」 세상에서 나온 것이다. 안내 자체는
        //   GearMenuOnboardingHintTests가 테스트마다 자기 저장소를 넣어 잰다.
        //
        // ★ 주입 누락·누수는 조용히 넘어가지 않는다 — 끝에서 (1) 실제 저장소 구현의 읽기·쓰기 계수 증가분 0,
        //   (2) 지금 쓰이는 저장소가 여기서 넣은 <b>같은 인스턴스</b>인지를 단언한다(RestoreSaveFilePath).
        _realOnboardingReadsAtStart = StickMate.Interaction.PlayerPrefsGearMenuOnboardingSeenStore.ReadCount;
        _realOnboardingWritesAtStart = StickMate.Interaction.PlayerPrefsGearMenuOnboardingSeenStore.WriteCount;
        bool leftover = StickMate.Interaction.GearMenuOnboardingSeenStore.IsOverriddenForTesting;
        _suiteOnboardingStore = new StickMate.Interaction.InMemoryGearMenuOnboardingSeenStore(seen: true);
        StickMate.Interaction.GearMenuOnboardingSeenStore.UseForTesting(_suiteOnboardingStore);
        Debug.Log("[테스트격리] 톱니 부채꼴 「최초 1회 안내 봤음」 한 비트를 메모리 저장소(초기값 봤음)로 바꿨습니다 — " +
                  "이번 실행에서 이 한 비트는 실제 설정 저장소를 읽지도 쓰지도 않습니다" +
                  "(Unity 엔진이 에디터 설정 파일에 스스로 쓰는 키는 이 격리 밖입니다). " +
                  $"이월된 주입={(leftover ? "있었음(덮어씀)" : "없음")}, " +
                  $"실제 저장소 계수 기준선 읽기={_realOnboardingReadsAtStart} 쓰기={_realOnboardingWritesAtStart}.");

        // ★ 2026-09-02 — 작업표시줄 자동 숨김 원복 흔적도 함께 옮긴다. PlayMode는 실제로 씬을
        // 띄우므로 ReservedBarRevealDirector의 BeforeSceneLoad 훅이 돈다. 그 훅은 기본 경로
        // (Application.persistentDataPath)의 흔적 파일을 읽고, 상황에 따라 쓴다 — 테스트가
        // 개발자의 실제 원복 흔적을 건드리면 그 사람의 작업표시줄 복구가 조용히 망가진다.
        string barDir = ReservedBarRestoreLedger.RedirectToTemporaryDirectoryForTesting("playmode");
        Debug.Log($"[테스트격리] PlayMode 작업표시줄 원복 흔적 경로를 임시 폴더로 옮겼습니다 — {barDir}");

        string dir = CharacterSaveStore.RedirectToTemporaryDirectoryForTesting("playmode");

        // ★ 옮기기만 하면 격리가 아니다 — 앞선 실행이 남긴 파일이 그대로 읽힌다. 비우고 시작한다.
        //   (근거와 가드 설계는 아래 PurgeIsolatedDirectories 문단 참고.)
        int purged = PurgeIsolatedDirectories();

        Debug.Log($"[테스트격리] PlayMode 저장 경로를 임시 폴더로 옮기고 이월분 {purged}개를 비웠습니다 — {dir} " +
                  $"(개발자 실제 저장 파일은 이번 실행에서 열리지 않습니다). " +
                  $"리디렉션={CharacterSaveStore.IsRedirectedForTesting}, 파일={CharacterSaveStore.FilePath}");
    }

    [OneTimeTearDown]
    public void RestoreSaveFilePath()
    {
        // ★ 2026-09-14 — 관측은 <b>맨 먼저</b>(정리가 주입을 걷기 전에), 단언은 <b>맨 끝</b>에 한 번에.
        //   여기서 먼저 던지면 아래 정리(프로브·게이트·저장 경로 원복)가 안 돌아 다음 실행에 오염을 물려주고,
        //   문제를 하나씩 던지면 앞의 실패가 뒤의 실패를 가린다.
        int realOnboardingReads =
            StickMate.Interaction.PlayerPrefsGearMenuOnboardingSeenStore.ReadCount - _realOnboardingReadsAtStart;
        int realOnboardingWrites =
            StickMate.Interaction.PlayerPrefsGearMenuOnboardingSeenStore.WriteCount - _realOnboardingWritesAtStart;
        // 주입이 걷혀 있으면 Current를 부르지 않는다(기본 저장소를 만들 이유가 없다).
        bool suiteStoreStillInPlace =
            StickMate.Interaction.GearMenuOnboardingSeenStore.IsOverriddenForTesting &&
            ReferenceEquals(StickMate.Interaction.GearMenuOnboardingSeenStore.Current, _suiteOnboardingStore);

        // ★ 먼저 걷는다(위 RedirectSaveFile의 순서 주석과 같은 계약) — 다음 어셈블리/다음 실행에
        //   네 방향 오버라이드를 물려주지 않는다.
        ReservedEdgeProbe.ResetForTests();

        // ★ 정적 우회는 도메인을 넘어 살아남는다 — 다음 어셈블리/다음 실행에 물려주지 않는다.
        StickMate.Interaction.InfoGearIconWidget.SetStandbyGateBypassedForTests(false);

        CharacterSaveStore.ResetForTesting();
        ReservedBarRestoreLedger.ResetForTesting();
        StickMate.Interaction.GearMenuOnboardingSeenStore.ResetForTesting();
        Debug.Log($"[테스트격리] PlayMode 저장 경로를 원래대로 되돌렸습니다 — 리디렉션={CharacterSaveStore.IsRedirectedForTesting}. " +
                  $"예약 띠 프로브 오버라이드도 걷었습니다(지금 관측치={ReservedEdgeProbe.LastInsets}). " +
                  $"「봤음」 저장소 주입도 걷었습니다(주입={StickMate.Interaction.GearMenuOnboardingSeenStore.IsOverriddenForTesting}).");

        // ★ 계수 자기 검증 — 「한 번도 오를 수 없는 계수의 0」은 아무것도 증명하지 않는다. 증가분을 먼저 떠
        //   두었으므로 이 검증이 올리는 값은 판정에 섞이지 않는다.
        string probeFailure = ProbeRealOnboardingStoreCounters();
        Debug.Log($"[테스트격리] 부채꼴 안내 봤음 한 비트의 실제 설정 저장소 접근 — 스위트 동안 읽기={realOnboardingReads} " +
                  $"쓰기={realOnboardingWrites} · 스위트 저장소 유지={(suiteStoreStillInPlace ? "예" : "아니오")} · " +
                  $"계수 자기 검증={(probeFailure == null ? "살아 있음(안 봤음·봤음 두 경로)" : "죽음: " + probeFailure)}");

        // ★ 실측(2026-09-14 변이 M5p): 이 TearDown의 실패는 결과 xml의 failed= 개수에 들어가지 않고 Unity 종료코드도 0이다.
        //   test-run result=Failed(Child) 와 이 SetUpFixture 스위트의 site=TearDown 으로만 드러난다 — 판정은 그 둘로 하라.
        var problems = new System.Collections.Generic.List<string>();
        if (probeFailure != null)
            problems.Add("★ 측정 무효 — 실제 설정 저장소 구현의 계수가 실제 읽기·쓰기 경로에서 오르지 않습니다(" + probeFailure +
                         "). 이 실행의 「실제 저장소 접근 0」은 아무것도 증명하지 않습니다.");
        if (realOnboardingReads != 0 || realOnboardingWrites != 0)
            problems.Add($"스위트가 부채꼴 「최초 1회 안내 봤음」 한 비트의 실제 설정 저장소에 닿았습니다 — 읽기 {realOnboardingReads}회 · " +
                         $"쓰기 {realOnboardingWrites}회. 어떤 픽스처가 주입을 걷었거나 이 파일의 주입이 빠졌습니다. " +
                         "쓰기가 0이고 읽기만 있어도 누락입니다 — 이미 1이 적힌 기계에서는 누락이 쓰기로 드러나지 않습니다.");
        if (!suiteStoreStillInPlace)
            problems.Add("스위트가 넣은 「봤음」 저장소가 끝에 쓰이고 있지 않습니다 — 어떤 픽스처가 자기 저장소를 넣고 " +
                         "RestoreForTesting으로 되돌리지 않았거나 주입을 걷었습니다. 그 뒤로 부채꼴을 여는 픽스처가 없어서 " +
                         "계수에는 안 잡혔을 뿐, 순서가 바뀌면 다음 픽스처가 남의 저장소나 실제 저장소를 씁니다.");
        Assert.IsEmpty(problems, "[테스트격리] " + string.Join("\n[테스트격리] ", problems));

        // ★ 모든 단언이 통과해야만 닿는 줄(docs/TEAM.md 픽스처 정리 단언 규칙 4, PlayModeSaveIsolationGateTests 선례).
        //   이 TearDown의 실패는 xml failed=·Unity 종료코드에 안 나오고, NUnit 「TearDown :」 접두어는 Unity 로그에 안 찍힌다.
        //   그래서 이 줄의 부재가 로그만 남았을 때의 유일한 흔적이다 — 문구를 바꾸면 판정 스크립트의 니들도 함께 바꿔라.
        Debug.Log("[테스트격리] ★ 부채꼴 안내 봤음 격리 단언 통과 — 스위트 동안 실제 설정 저장소 읽기 0 · 쓰기 0 · " +
                  "스위트 저장소 인스턴스 유지 · 계수 자기 검증 두 경로(안 봤음·봤음) 살아 있음.");
    }

    /// <summary>
    /// 실제 설정 저장소 구현(<see cref="StickMate.Interaction.PlayerPrefsGearMenuOnboardingSeenStore"/>)의 계수가
    /// 읽기·쓰기 경로에서 오르는지 <b>안 봤음·봤음 두 경로 모두</b> 확인한다. 살아 있으면 <c>null</c>, 아니면 사유.
    ///
    /// <para><b>왜 두 경로인가</b> (verify-change B7): 싱크가 늘 기본값을 돌려주면 「안 봤음」 경로만 잰다. 그러면
    /// 「봤음이면 계수 전에 반환」 형태가 이 검증을 통과하고, 이미 1이 적힌 기계에서 주입 누락이 읽기 0으로 숨는다.</para>
    ///
    /// <para><b>실제 저장소를 건드리지 않는 방법</b>: 이 클래스의 코드를 그대로 태우되 맨 끝 한 줄(설정 저장소 호출)만
    /// 기록용 싱크로 바꾼다. 「쓰고 곧바로 원복」은 기각했다 — 원래 키가 없던 기계에서는 원복이 <b>지우기</b>이고,
    /// 쓰기와 원복 사이에 러너가 죽으면 개발자 값이 바뀐 채 남으며, 원복 자체가 보호받지 않는 쓰기다
    /// (docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md G-1 「기록해 두고 되돌리기로 대신하지 않는다」).
    /// 바꾼 한 줄이 정말 설정 저장소 호출이고 계수를 거치지 않는 다른 길이 없다는 것은
    /// Tests/EditMode/GearMenuOnboardingSeenStoreTests 의 소스 감사가 잠근다.</para>
    /// </summary>
    public static string ProbeRealOnboardingStoreCounters()
    {
        const string unseenPath = "안 봤음 경로";
        const string seenPath = "봤음 경로";

        string failure = ProbeOnePath(unseenPath, storedValue: null, expectSeen: false);
        if (failure != null) return failure;
        return ProbeOnePath(seenPath, StickMate.Interaction.PlayerPrefsGearMenuOnboardingSeenStore.SeenValue, expectSeen: true);
    }

    private static string ProbeOnePath(string label, int? storedValue, bool expectSeen)
    {
        var sink = new RecordingIntPreferenceSink(storedValue);
        var store = new StickMate.Interaction.PlayerPrefsGearMenuOnboardingSeenStore(sink);

        int reads0 = StickMate.Interaction.PlayerPrefsGearMenuOnboardingSeenStore.ReadCount;
        int writes0 = StickMate.Interaction.PlayerPrefsGearMenuOnboardingSeenStore.WriteCount;

        bool seen = store.IsSeen;
        int readDelta = StickMate.Interaction.PlayerPrefsGearMenuOnboardingSeenStore.ReadCount - reads0;
        store.MarkSeen();
        int writeDelta = StickMate.Interaction.PlayerPrefsGearMenuOnboardingSeenStore.WriteCount - writes0;

        if (seen != expectSeen)
            return $"{label}: 준비물 무효 — 싱크가 돌려준 값에서 봤음={seen}(기대 {expectSeen})로 읽혀 이 경로를 태우지 못했습니다";
        if (readDelta != 1 || writeDelta != 1)
            return $"{label}: 계수 증가 읽기 {readDelta}·쓰기 {writeDelta}(기대 1·1)";
        if (sink.GetCalls != 1 || sink.SetCalls != 1 || sink.SaveCalls != 1)
            return $"{label}: 싱크 도달 읽기 {sink.GetCalls}·쓰기 {sink.SetCalls}·저장 {sink.SaveCalls}(기대 1·1·1) — " +
                   "계수는 올랐는데 그 경로가 설정 저장소 호출까지 가지 않습니다";
        if (sink.LastSetKey != StickMate.Interaction.PlayerPrefsGearMenuOnboardingSeenStore.Key)
            return $"{label}: 쓰기 경로가 선언된 키가 아닌 다른 키로 씁니다";
        return null;
    }

    private sealed class RecordingIntPreferenceSink
        : StickMate.Interaction.PlayerPrefsGearMenuOnboardingSeenStore.IIntPreferenceSink
    {
        private readonly int? _storedValue;

        public RecordingIntPreferenceSink(int? storedValue)
        {
            _storedValue = storedValue;
        }

        public int GetCalls;
        public int SetCalls;
        public int SaveCalls;
        public string LastSetKey;

        public int GetInt(string key, int defaultValue)
        {
            GetCalls++;
            return _storedValue ?? defaultValue;
        }

        public void SetInt(string key, int value)
        {
            SetCalls++;
            LastSetKey = key;
        }

        public void Save() => SaveCalls++;
    }

    // ============================================================================
    // ★★ 2026-09-02 — 리디렉션만으로는 격리가 아니다. <b>비우고 시작해야</b> 격리다.
    // ============================================================================
    // qa-regression의 A/B 통제 실험이 거짓 빨강 5건의 진범으로 이 자리를 지목했다.
    //
    // 무엇이 문제였나(실측):
    //   CharacterSaveStore.RedirectToTemporaryDirectoryForTesting()은 폴더가 없으면 만들 뿐
    //   <b>비우지 않는다</b>. 그 폴더는 macOS의 /var/folders/.../T 아래라 <b>재부팅에서만</b>
    //   지워진다. 즉 앞선 실행이 남긴 저장 파일이 다음 실행의 첫 씬 로드에서 그대로 읽힌다
    //   (프리팹의 CharacterProgressionDirector가 **Start()**에서 Load를 부른다 —
    //    Interaction/CharacterProgressionDirector.cs. 2026-09-02 정정: Awake가 아니다).
    //
    // ★ 그래서 "지금 초록"은 고쳐진 증거가 아니다 — 재부팅 직후라 폴더가 우연히 비었을 뿐일 수 있다.
    //   2026-09-02 실측: 재부팅 뒤 19:29에 만들어진 폴더가 20:10에 이미 파일 3개로 다시 차 있었다.
    //
    // 삭제의 사정거리는 <b>두 겹의 가드</b>로 묶는다. 하나라도 거짓이면 <b>지우지 않고 실패</b>한다:
    //   (1) 그 저장소가 실제로 테스트용으로 리디렉션돼 있을 것(IsRedirectedForTesting)
    //   (2) 대상 폴더가 Application.temporaryCachePath <b>아래</b>일 것
    // 개발자의 실제 저장 파일(persistentDataPath)은 (2)에서 구조적으로 걸린다.
    // 하위 폴더로 내려가지 않고 <b>바로 아래 파일만</b> 지운다(사정거리 최소화).
    //
    // 프로덕션이 아니라 테스트 코드에 두는 이유: 이 앱의 프로덕션 코드에는 파일 삭제 능력이
    // 0건이라는 불변식을 Tests/EditMode/UserAssetImmutabilityAuditTests가 잠근다. 그 불변식을
    // 테스트 편의로 깨지 않는다.
    public static int PurgeIsolatedDirectories()
    {
        // 저장 파일은 이 격리의 <b>본체</b>다 — 리디렉션돼 있지 않으면 그 자체가 사고이므로 단언한다.
        int removed = PurgeGuarded(StickMate.Core.CharacterSaveStore.IsRedirectedForTesting,
            StickMate.Core.CharacterSaveStore.FilePath, "저장 파일");

        // ★ 2026-09-02 러너 실측으로 고침 — 원복 흔적은 <b>남의 소유</b>다.
        //   Tests/EditMode/ReservedBarRevealPolicyTests가 테스트마다 자기 경로로 다시 옮기고
        //   끝나면 되돌린다(ResetForTesting). 그래서 스위트 <b>도중</b>에는 리디렉션이 꺼져 있는
        //   순간이 정상적으로 존재한다. 옛 코드는 그 순간에도 단언을 걸어, 스위트 중간에 이 정리기를
        //   부르는 검사가 통째로 빨개졌다(러너 1회차 실패 1건이 정확히 그것이다).
        //   그때 할 일은 "실패"가 아니라 <b>건드리지 않는 것</b>이다 — 리디렉션이 아니면 그 경로는
        //   남의 것이거나 개발자의 실제 경로다. 다만 <b>조용히 건너뛰지는 않는다</b>(로그로 남긴다).
        if (StickMate.Platform.ReservedBarRestoreLedger.IsRedirectedForTesting)
        {
            removed += PurgeGuarded(true,
                StickMate.Platform.ReservedBarRestoreLedger.FilePath, "작업표시줄 원복 흔적");
        }
        else
        {
            UnityEngine.Debug.Log("[테스트격리] 작업표시줄 원복 흔적 경로가 지금 리디렉션돼 있지 않아 " +
                "건너뜁니다 — 남의(또는 개발자의) 경로를 건드리지 않습니다. " +
                "OneTimeSetUp에서는 방금 옮긴 직후라 이 분기를 타지 않습니다.");
        }
        return removed;
    }

    /// <summary>가드 두 겹을 통과한 폴더의 <b>바로 아래 파일만</b> 지운다. 지운 개수를 돌려준다.
    /// <para><c>public</c>인 이유: 가드가 <b>실제로 무는지</b>를 전역 상태를 건드리지 않고 확인할 수
    /// 있어야 한다. Tests/EditMode/SaveIsolationPurgeTests가 합성 인자로 직접 호출해
    /// "임시 캐시 밖이면 지우지 않는다"를 <b>실제 파일로</b> 확인한다.</para></summary>
    public static int PurgeGuarded(bool isRedirected, string filePath, string label)
    {
        NUnit.Framework.Assert.IsTrue(isRedirected,
            $"[테스트격리] {label} 경로가 리디렉션되지 않았습니다 — 지우지 않고 멈춥니다. " +
            "이 상태로 진행하면 개발자의 실제 파일을 지울 수 있습니다.");

        string dir = System.IO.Path.GetFullPath(System.IO.Path.GetDirectoryName(filePath));
        string temp = System.IO.Path.GetFullPath(UnityEngine.Application.temporaryCachePath)
            .TrimEnd(System.IO.Path.DirectorySeparatorChar);

        NUnit.Framework.Assert.IsTrue(
            dir.StartsWith(temp + System.IO.Path.DirectorySeparatorChar, System.StringComparison.Ordinal),
            $"[테스트격리] {label} 폴더가 임시 캐시 밖입니다 — 지우지 않고 멈춥니다.\n" +
            $"  대상 = {dir}\n  임시 캐시 = {temp}");

        if (!System.IO.Directory.Exists(dir)) return 0;

        int removed = 0;
        foreach (string f in System.IO.Directory.GetFiles(dir))
        {
            System.IO.File.Delete(f);
            removed++;
        }

        // "0건 = 깨끗"과 "아무것도 안 봤다"를 구분한다 — 실제로 비었는지 다시 센다.
        int left = System.IO.Directory.GetFiles(dir).Length;
        NUnit.Framework.Assert.AreEqual(0, left,
            $"[테스트격리] {label} 폴더를 비우지 못했습니다 — {left}개가 남았습니다({dir}).");

        UnityEngine.Debug.Log($"[테스트격리] {label} 폴더를 비웠습니다 — 삭제 {removed}개, 경로 {dir}");
        return removed;
    }
}
