using NUnit.Framework;
using StickMate.Core;
using UnityEngine;

/// <summary>
/// ★ PlayMode 스위트 전체의 저장 파일 격리 (2026-08-31, R3 Blocker 2 동반 조치).
///
/// <para><b>왜 필요한가.</b> PlayMode 테스트는 Stickman 프리팹을 통째로 띄운다. 그 프리팹에는
/// <c>CharacterProgressionDirector</c>가 붙어 있고 그것이 Awake에서 <see cref="CharacterSaveStore.Load"/>를
/// 부른다. 그래서 지금까지 모든 PlayMode 테스트가 <b>테스트를 돌리는 사람의 개인 저장 파일</b>을 읽고
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
        string dir = CharacterSaveStore.RedirectToTemporaryDirectoryForTesting("playmode");
        Debug.Log($"[테스트격리] PlayMode 저장 경로를 임시 폴더로 옮겼습니다 — {dir} " +
                  $"(개발자 실제 저장 파일은 이번 실행에서 열리지 않습니다). " +
                  $"리디렉션={CharacterSaveStore.IsRedirectedForTesting}, 파일={CharacterSaveStore.FilePath}");
    }

    [OneTimeTearDown]
    public void RestoreSaveFilePath()
    {
        CharacterSaveStore.ResetForTesting();
        Debug.Log($"[테스트격리] PlayMode 저장 경로를 원래대로 되돌렸습니다 — 리디렉션={CharacterSaveStore.IsRedirectedForTesting}.");
    }
}
