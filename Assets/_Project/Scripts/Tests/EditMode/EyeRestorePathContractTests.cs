using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.States;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-01 — <b>눈맞춤 기능의 "되살리기 경로"가 실제로 남아 있는가</b>를 잠근다
    /// (docs/UX_FLOW.md 38-5).
    ///
    /// ============================================================================
    /// 왜 이런 테스트가 필요한가
    /// ============================================================================
    /// 사용자 지시는 <b>"기능은 삭제하되 코드는 남겨 나중에 복원 가능하게"</b>였다. 즉 요구사항의 절반이
    /// <b>"지우지 않았다"</b>이고, 그것은 <b>일반 테스트로는 절대 검증되지 않는다</b> — 기능이 꺼져 있으므로
    /// 실행 경로가 아무것도 밟지 않기 때문이다. 누군가 반년 뒤 "안 쓰는 코드"라며
    /// <c>EyeController.cs</c>를 지우거나, 좌표 상수를 정리하거나, 게이트를 <c>if</c>가 아니라
    /// <b>삭제</b>로 바꿔도 <b>모든 테스트가 초록불로 통과한다</b>. 그러면 사용자가 "눈 다시 살려줘"라고
    /// 했을 때 되돌릴 것이 남아 있지 않다.
    ///
    /// 그래서 이 파일은 <b>소스 자체</b>를 관측 대상으로 삼는다(선례: <c>OfflineFirstNetworkAuditTests</c>).
    /// 재는 것은 "코드가 예쁜가"가 아니라 <b>복원 절차의 각 단계가 가리키는 대상이 존재하는가</b>다:
    ///
    /// <code>
    ///   1) Editor/SceneBootstrapper.BakeEyes = false            ← 상수가 있고, false이고, 눈 생성을 감싼다
    ///   2) Interaction/CharacterPortraitStage.DrawEyes = false   ← 동상
    ///   3) StickConfig.eyeTrackingEnabled = false + 튜닝 4개 보존 ← 되살릴 때 그때의 값이 남아 있어야 한다
    ///   4) States/EyeController.cs 가 그대로 있고 null 가드가 살아 있다
    /// </code>
    ///
    /// <para><b>네거티브 컨트롤</b>: 각 검사는 "찾지 못하면 통과"가 될 수 없게, 먼저 파일을 실제로 읽었고
    /// 내용이 비어 있지 않음을 단언한다. 그러지 않으면 경로 계산이 틀렸을 때 이 파일 전체가
    /// <b>아무것도 보지 않고 조용히 통과</b>한다.</para>
    /// </summary>
    public sealed class EyeRestorePathContractTests
    {
        private const string LogPrefix = "[눈복원경로-TEST]";

        private static string Read(string relativeToAssets)
        {
            string path = Path.Combine(Application.dataPath, relativeToAssets.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(path),
                $"{LogPrefix} 되살리기 절차가 가리키는 파일이 없습니다: {relativeToAssets}\n" +
                "눈맞춤 기능은 '삭제'가 아니라 '게이트'여야 합니다(사용자 지시: 코드는 남길 것).");
            string text = File.ReadAllText(path);
            Assert.Greater(text.Length, 200,
                $"{LogPrefix} {relativeToAssets}의 내용이 {text.Length}자뿐입니다 — 파일이 비었거나 경로가 잘못됐습니다.");
            return text;
        }

        // ============================================================================
        // 1) 프리팹 쪽 게이트 — 상수가 있고, false이고, 눈 생성 코드를 감싸고 있다
        // ============================================================================
        [Test]
        public void BootstrapperKeepsEyeBakingCodeBehindAConstantGate()
        {
            string src = Read("Editor/SceneBootstrapper.cs");

            Assert.IsTrue(Regex.IsMatch(src, @"const\s+bool\s+BakeEyes\s*=\s*false\s*;"),
                $"{LogPrefix} SceneBootstrapper에 `const bool BakeEyes = false;`가 없습니다 — " +
                "되살리기 절차 1단계가 가리키는 스위치가 사라졌습니다.");

            Assert.IsTrue(Regex.IsMatch(src, @"if\s*\(\s*BakeEyes\s*\)"),
                $"{LogPrefix} BakeEyes를 실제로 분기에 쓰는 곳이 없습니다 — 상수만 남고 눈 생성 코드는 " +
                "지워졌을 가능성이 큽니다(그러면 true로 되돌려도 눈이 돌아오지 않습니다).");

            // 눈을 실제로 만드는 코드가 통째로 남아 있어야 한다.
            foreach (string needle in new[] { "\"LeftEye\"", "\"RightEye\"", "CreateFilledDot(" })
            {
                Assert.IsTrue(src.Contains(needle),
                    $"{LogPrefix} SceneBootstrapper에서 {needle} 가 사라졌습니다 — 되살릴 코드가 없습니다.");
            }

            // 좌표/크기 상수 3개도 보존돼야 한다(되살릴 때 '어디에 얼마나' 그릴지가 여기 있다).
            foreach (string c in new[] { "BaselineEyePupilRadius", "BaselineEyeOffsetX", "BaselineEyeOffsetY" })
            {
                Assert.IsTrue(Regex.IsMatch(src, @"const\s+float\s+" + c + @"\s*=\s*[0-9.]+f\s*;"),
                    $"{LogPrefix} 눈 좌표 상수 {c}가 사라졌습니다 — 되살려도 위치를 다시 정해야 합니다.");
            }

            Debug.Log($"{LogPrefix} 프리팹 게이트 확인 — BakeEyes=false + if(BakeEyes) 분기 + 눈 생성 코드/좌표 상수 3개 보존.");
        }

        // ============================================================================
        // 2) 초상화 쪽 게이트 — 실제 캐릭터와 같은 상태여야 한다
        // ============================================================================
        [Test]
        public void PortraitKeepsEyeDrawingCodeBehindAConstantGate()
        {
            string src = Read("_Project/Scripts/Interaction/CharacterPortraitStage.cs");

            Assert.IsTrue(Regex.IsMatch(src, @"const\s+bool\s+DrawEyes\s*=\s*false\s*;"),
                $"{LogPrefix} CharacterPortraitStage에 `const bool DrawEyes = false;`가 없습니다.");
            Assert.IsTrue(Regex.IsMatch(src, @"if\s*\(\s*!\s*DrawEyes\s*\)"),
                $"{LogPrefix} DrawEyes를 실제로 분기에 쓰는 곳이 없습니다.");

            foreach (string needle in new[] { "\"EyeBack\"", "\"EyeFront\"", "EyeRadiusRatio" })
            {
                Assert.IsTrue(src.Contains(needle),
                    $"{LogPrefix} 초상화에서 {needle} 가 사라졌습니다 — 되살릴 코드가 없습니다.");
            }

            // ★ 두 게이트가 **같은 상태**여야 한다. 한쪽만 켜면 "몸에는 눈이 없는데 초상화에만 있다"는
            //   이중 정의가 그대로 재발한다(2026-08-31에 실제로 고쳤던 결함의 거울상).
            string boot = Read("Editor/SceneBootstrapper.cs");
            bool bake = Regex.IsMatch(boot, @"const\s+bool\s+BakeEyes\s*=\s*true\s*;");
            bool draw = Regex.IsMatch(src, @"const\s+bool\s+DrawEyes\s*=\s*true\s*;");
            Assert.AreEqual(bake, draw,
                $"{LogPrefix} 실제 캐릭터(BakeEyes={bake})와 초상화(DrawEyes={draw})의 눈 게이트가 어긋났습니다 — " +
                "둘은 반드시 같이 켜고 같이 꺼야 합니다.");

            Debug.Log($"{LogPrefix} 초상화 게이트 확인 — DrawEyes=false, 실제 캐릭터 게이트와 일치.");
        }

        // ============================================================================
        // 3) StickConfig — 스위치는 꺼졌고 튜닝값 4개는 보존됐다
        // ============================================================================
        [Test]
        public void ConfigKeepsEyeTuningFieldsWhileTrackingIsOff()
        {
            var probe = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                Assert.IsFalse(probe.eyeTrackingEnabled,
                    $"{LogPrefix} StickConfig.eyeTrackingEnabled의 기본값이 true입니다 — " +
                    "눈이 없는 캐릭터에 추적을 켜 두면 의도가 기록되지 않습니다(38-5).");

                // 되살릴 때 그때의 튜닝값이 남아 있어야 한다 = 0이 아닌 실제 값이어야 한다.
                Assert.Greater(probe.eyeMaxPupilOffset, 0f, $"{LogPrefix} eyeMaxPupilOffset이 보존되지 않았습니다.");
                Assert.Greater(probe.eyeTrackingFollowRate, 0f, $"{LogPrefix} eyeTrackingFollowRate가 보존되지 않았습니다.");
                Assert.Greater(probe.eyeTrackingNeutralRadiusWorld, 0f, $"{LogPrefix} eyeTrackingNeutralRadiusWorld가 보존되지 않았습니다.");
                Assert.Greater(probe.eyeTrackingFullRangeWorld, probe.eyeTrackingNeutralRadiusWorld,
                    $"{LogPrefix} eyeTrackingFullRangeWorld가 보존되지 않았습니다(중립 반경보다 커야 합니다).");

                Debug.Log($"{LogPrefix} StickConfig 확인 — eyeTrackingEnabled={probe.eyeTrackingEnabled}, " +
                    $"보존된 튜닝값 maxOffset={probe.eyeMaxPupilOffset:F3} / follow={probe.eyeTrackingFollowRate:F2} / " +
                    $"neutral={probe.eyeTrackingNeutralRadiusWorld:F2} / full={probe.eyeTrackingFullRangeWorld:F2}.");
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        // ============================================================================
        // 3') 배포 에셋도 같은 상태여야 한다 — 코드 기본값만 바꾸고 에셋을 잊는 실패 방지
        // ============================================================================
        [Test]
        public void DeployedConfigAssetHasEyeTrackingOff()
        {
            string path = Path.Combine(Application.dataPath, "_Project", "Data", "DefaultStickConfig.asset");
            Assert.IsTrue(File.Exists(path), $"{LogPrefix} 배포 설정 에셋을 찾지 못했습니다: {path}");
            string text = File.ReadAllText(path);

            var m = Regex.Match(text, @"eyeTrackingEnabled:\s*([01])");
            Assert.IsTrue(m.Success,
                $"{LogPrefix} DefaultStickConfig.asset에 eyeTrackingEnabled 필드가 없습니다 — " +
                "필드가 삭제됐다면 되살리기 경로가 끊긴 것입니다.");
            Assert.AreEqual("0", m.Groups[1].Value,
                $"{LogPrefix} 배포 에셋의 eyeTrackingEnabled가 1입니다 — 코드 기본값만 끄고 에셋을 잊었습니다. " +
                "직렬화된 에셋 값이 코드 기본값을 덮어쓰므로 실제 실행에서는 켜진 채로 나갑니다.");

            Debug.Log($"{LogPrefix} 배포 에셋 확인 — eyeTrackingEnabled: 0.");
        }

        // ============================================================================
        // 4) EyeController — 파일이 그대로 있고, 눈이 없을 때 스스로 조용해진다
        // ============================================================================
        [Test]
        public void EyeControllerSurvivesAndIsHarmlessWithoutEyes()
        {
            string src = Read("_Project/Scripts/States/EyeController.cs");
            foreach (string needle in new[] { "HasEyes", "TickLookAt", "SetLookDirection", "SetFacing", "LookForward" })
            {
                Assert.IsTrue(src.Contains(needle),
                    $"{LogPrefix} EyeController에서 {needle} 가 사라졌습니다 — 되살리기 경로가 끊깁니다.");
            }

            // 소스에 남아 있는 것만으로는 부족하다 — **실제로 만들어 호출해** 무해함을 확인한다.
            var root = new GameObject("눈없는리그");
            try
            {
                var head = new GameObject("Head");
                head.transform.SetParent(root.transform, false);

                var eyes = new EyeController(root.transform);
                Assert.IsFalse(eyes.HasEyes, $"{LogPrefix} 눈이 없는 리그인데 HasEyes가 참입니다.");

                Assert.DoesNotThrow(() =>
                {
                    eyes.TickLookAt(true, new Vector2(5f, 1f), 0.016f, EyeController.EyeTrackingSettings.Default);
                    eyes.SetLookDirection(new Vector2(1f, 0f));
                    eyes.SetFacing(-1f);
                    eyes.LookForward();
                }, $"{LogPrefix} 눈이 없을 때 EyeController가 예외를 던졌습니다 — 무해하지 않습니다.");

                // 네거티브 컨트롤 — 눈을 붙이면 같은 클래스가 실제로 눈을 잡는다(= 위 통과가 공허하지 않다).
                var left = new GameObject("LeftEye");
                left.transform.SetParent(head.transform, false);
                left.transform.localPosition = new Vector3(-0.075f, 0.02f, 0f);
                var right = new GameObject("RightEye");
                right.transform.SetParent(head.transform, false);
                right.transform.localPosition = new Vector3(0.075f, 0.02f, 0f);

                var revived = new EyeController(root.transform);
                Assert.IsTrue(revived.HasEyes,
                    $"{LogPrefix} 눈 오브젝트를 붙였는데도 EyeController가 찾지 못했습니다 — " +
                    "되살리기 경로가 실제로는 동작하지 않습니다(위의 '무해함' 통과가 공허했습니다).");

                Debug.Log($"{LogPrefix} EyeController 확인 — 눈 없으면 HasEyes=false + 예외 없음, " +
                    "눈을 붙이면 HasEyes=true(되살리기 경로 실동작 확인).");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
