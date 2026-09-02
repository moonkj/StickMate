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
        Debug.Log($"[테스트격리] EditMode 저장 경로를 임시 폴더로 옮겼습니다 — {dir} " +
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
}
