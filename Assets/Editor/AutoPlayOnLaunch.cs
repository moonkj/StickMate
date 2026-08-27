using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace StickMate.EditorTools
{
    /// <summary>
    /// -executeMethod로 호출: Main.unity를 열고 즉시 Play 모드로 진입한다.
    /// 사용자에게 실제 동작 화면을 스크린샷으로 보여주기 위한 일회성 검증 도구.
    /// </summary>
    public static class AutoPlayOnLaunch
    {
        public static void OpenAndPlay()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Main.unity");
            EditorApplication.isPlaying = true;
        }
    }
}
