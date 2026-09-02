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
/// </summary>
[SetUpFixture]
public sealed class GlobalPlayModeTestIsolation
{
    [OneTimeSetUp]
    public void RedirectSaveFile()
    {
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
        CharacterSaveStore.ResetForTesting();
        ReservedBarRestoreLedger.ResetForTesting();
        Debug.Log($"[테스트격리] PlayMode 저장 경로를 원래대로 되돌렸습니다 — 리디렉션={CharacterSaveStore.IsRedirectedForTesting}.");
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
