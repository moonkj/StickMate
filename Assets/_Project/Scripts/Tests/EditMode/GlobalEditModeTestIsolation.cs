using NUnit.Framework;
using StickMate.Core;
using StickMate.Platform;
using UnityEngine;

/// <summary>
/// ★ EditMode 스위트 전체의 저장 파일 격리 + <b>임시 QA 스위치 차단</b> (2026-08-31).
/// 근거와 설계 판단은 Tests/PlayMode/GlobalPlayModeTestIsolation.cs 문서와 동일하다 —
/// 어셈블리가 둘이라 <c>[SetUpFixture]</c>도 어셈블리마다 하나씩 필요할 뿐이다.
///
/// <para>EditMode 쪽은 프리팹을 띄우지 않는 테스트가 대부분이지만, 지속성 테스트 6종이
/// <see cref="CharacterSaveStore.FilePath"/>에 <b>직접 쓰고 읽는다</b>. 지금까지 그것들은
/// "SetUp에서 개발자 파일을 백업하고 TearDown에서 되돌린다"는 관례로 안전을 지켜 왔는데,
/// 그 관례는 테스트가 중간에 크래시하면 그대로 깨진다. 경로 자체를 임시 폴더로 옮기면
/// 개발자 파일이 그 시나리오에서도 손상될 수 없다(관례가 아니라 구조로 보장한다).</para>
/// </summary>
[SetUpFixture]
public sealed class GlobalEditModeTestIsolation
{
    [OneTimeSetUp]
    public void RedirectSaveFile()
    {
        // ★ 2026-08-31 — QA 해금 스위치(EquipmentDebugUnlock)를 <b>스위트 전체에서 끈다</b>.
        // 그 스위치는 사용자가 장비 전종을 직접 눌러 보려고 켠 것이지 사양 변경이 아니다. 켜진 채로
        // 테스트를 돌리면 "요구 레벨이 실제로 잠그는가"를 검증하던 단언들이 스위치를 검증하게 되고,
        // 스위치를 끄는 날 그 회귀를 아무도 못 잡는다. 테스트는 언제나 <b>제품 규칙</b>을 본다.
        EquipmentDebugUnlock.SetTestOverride(false);

        // ★ 2026-09-02 — 작업표시줄 자동 숨김 원복 흔적도 스위트 전체에서 임시 폴더로 옮긴다.
        // ReservedBarRevealPolicyTests가 테스트마다 다시 옮기지만, 그 앞뒤로 도는 다른 테스트가
        // 흔적 API를 스치더라도 개발자의 실제 파일이 열리지 않게 바닥을 깔아 둔다.
        ReservedBarRestoreLedger.RedirectToTemporaryDirectoryForTesting("editmode");

        string dir = CharacterSaveStore.RedirectToTemporaryDirectoryForTesting("editmode");

        // ★ 옮기기만 하면 격리가 아니다 — 앞선 실행이 남긴 파일이 그대로 읽힌다. 비우고 시작한다.
        //   (근거와 가드 설계는 아래 PurgeIsolatedDirectories 문단 참고.)
        int purged = PurgeIsolatedDirectories();

        Debug.Log($"[테스트격리] EditMode 저장 경로를 임시 폴더로 옮기고 이월분 {purged}개를 비웠습니다 — {dir} " +
                  $"(개발자 실제 저장 파일은 이번 실행에서 열리지 않습니다). " +
                  $"리디렉션={CharacterSaveStore.IsRedirectedForTesting}, 파일={CharacterSaveStore.FilePath}");
    }

    [OneTimeTearDown]
    public void RestoreSaveFilePath()
    {
        EquipmentDebugUnlock.SetTestOverride(null);
        CharacterSaveStore.ResetForTesting();
        ReservedBarRestoreLedger.ResetForTesting();
        Debug.Log($"[테스트격리] EditMode 저장 경로를 원래대로 되돌렸습니다 — 리디렉션={CharacterSaveStore.IsRedirectedForTesting}.");
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
