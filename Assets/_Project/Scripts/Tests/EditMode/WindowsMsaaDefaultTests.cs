using System.IO;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// **플랫폼별 MSAA 기본값 회귀 잠금** (2026-09-01 "윈도우만 렉" 라운드).
    ///
    /// <para><b>무엇을 지키는가.</b> 사용자 실기 콜드 스타트 A/B에서 <b>Windows만</b> MSAA 배수에 따라
    /// 체감이 갈렸다:
    /// <list type="bullet">
    ///   <item>4x — 화질 좋음 / <b>렉 심함</b></item>
    ///   <item>2x — 화질 "봐줄만함"</item>
    ///   <item>0x — 렉 줄어듦 / 화질 <b>"지저분함"</b></item>
    /// </list>
    /// macOS는 같은 값을 6쌍 페어드로 재도 CPU/GPU 절감이 <b>검출되지 않았고</b>(Apple GPU는 TBDR이라
    /// resolve가 타일 메모리에서 끝난다) 화질 이득만 있었다. 그래서 <b>두 플랫폼이 서로 다른 기본값을
    /// 갖는 것이 의도된 상태</b>다. 이 비대칭은 설명 없이 보면 "실수로 갈린 값"처럼 보여서
    /// <b>선의로 통일당하기 쉽다</b> — 그걸 막는 것이 이 파일의 목적이다.</para>
    ///
    /// <para><b>왜 테스트가 꼭 필요한가.</b> 이 설정은 <c>BeforeSceneLoad</c>에서만 효력이 있고
    /// 에디터/테스트에서는 그 경로가 돌지 않는다. 즉 <b>누가 값을 되돌려도 기존 테스트는 전부
    /// 초록불</b>이고, 잘못은 사용자 PC에서 렉으로만 드러난다(이 프로젝트가 디스플레이 절전
    /// 회귀에서 이미 한 번 겪은 실패 모드 — <see cref="DisplaySleepPolicyTests"/> 문서 참고).</para>
    ///
    /// <para><b>이 테스트의 정직한 한계</b>: "값이 의도대로 코드에 있는가"와 "그 값이 유효한 시점에
    /// 걸리도록 배선돼 있는가"까지만 잠근다. 그 값이 실제로 백버퍼에 반영되는지, 그래서 렉이 실제로
    /// 줄어드는지는 <b>Windows 실기 콜드 스타트</b>로만 확인할 수 있다(개발 머신이 macOS다).
    /// 실기 확인 수단은 <c>[렌더진단] ★A/B 요약</c> 로그 한 줄이다.</para>
    /// </summary>
    public class WindowsMsaaDefaultTests
    {
        /// <summary>프로젝트 설정 에셋의 MSAA 기준값 — <b>macOS/에디터</b>가 쓰는 값이다.</summary>
        private const int MacOsBaselineSamples = 4;

        private static string RepoRoot =>
            Directory.GetParent(Application.dataPath).FullName;

        private static string TunerSourcePath => Path.Combine(
            Application.dataPath, "_Project", "Scripts", "Platform", "RenderQualityTuner.cs");

        private static string ReadSource(string path)
        {
            Assert.IsTrue(File.Exists(path), $"소스를 찾지 못했습니다: {path}");
            return File.ReadAllText(path);
        }

        [Test]
        public void Windows_기본_MSAA는_macOS와_같은_4x다()
        {
            // 2026-09-01 사용자 실기 측정으로 확정: MSAA는 이 앱의 GPU 비용과 **무관**하다.
            // 4x / 2x / 0x 세 경우 모두 작업 관리자 GPU 사용률이 약 30%로 동일했고, 그 뒤 도착한
            // [렌더진단] 로그가 이유를 밝혔다 — GPU 프레임시간이 평균 0.01~0.71ms로 GPU는 사실상
            // 놀고 있었다(작업 관리자 %는 GPU 다운클럭 상태의 착시였다). 렉의 실체는 CPU였다.
            //
            // 비용이 같다면 화질이 가장 좋은 값을 쓰는 것이 옳다. 사용자 판정은
            // 4x=좋음 / 2x="봐줄만함" / 0x="지저분함"이었으므로 4로 되돌렸다(= macOS와 동일, 분기 없음).
            //
            // 다시 내리려는 사람에게: **작업 관리자 GPU %를 근거로 쓰지 마라.** 반드시 Windows 실기에서
            // STICKMATE_FORCE_MSAA로 콜드 스타트 A/B를 하고 [렌더진단] ★A/B 요약의
            // **GPU 프레임시간(ms)**을 근거로 남겨라. 오늘 이 함정으로 세 번 헛수고했다.
            Assert.AreEqual(4, RenderQualityTuner.WindowsDefaultSamples,
                "Windows 기본 MSAA가 바뀌었습니다. 2026-09-01 실기 측정에서 MSAA는 이 앱의 GPU 비용과 " +
                "무관함이 확인됐고(4x/2x/0x 전부 동일, GPU 프레임시간 0.01~0.71ms), 비용이 같으므로 " +
                "화질이 가장 좋은 4x를 씁니다. 내리기 전에 반드시 GPU 프레임시간(ms) 근거를 남기세요 — " +
                "작업 관리자 GPU %는 다운클럭 때문에 착시가 납니다. " +
                "8은 어느 플랫폼에도 주지 마세요(Apple GPU가 조용히 4로 낮추면서 Screen.msaaSamples는 " +
                "8을 계속 보고하는 함정 — BuildStandalone.ConfigureAntiAliasing 문서).");
        }

        [Test]
        public void 프로젝트_품질설정은_macOS_기준값_4x를_유지한다()
        {
            // Windows는 런타임에 덮어쓰므로, 공유 에셋인 QualitySettings.asset은 macOS 값으로 남아야 한다.
            // 여기가 흔들리면 macOS 화질이 조용히 회귀한다(그쪽은 MSAA가 사실상 공짜라 순손실이다).
            string path = Path.Combine(RepoRoot, "ProjectSettings", "QualitySettings.asset");
            Assert.IsTrue(File.Exists(path), $"QualitySettings.asset을 찾지 못했습니다: {path}");

            string[] lines = File.ReadAllLines(path);
            int levelCount = 0;
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (!trimmed.StartsWith("antiAliasing:")) continue;
                levelCount++;
                Assert.AreEqual($"antiAliasing: {MacOsBaselineSamples}", trimmed,
                    "QualitySettings.asset의 품질 레벨 하나가 macOS 기준값 4x에서 벗어났습니다. " +
                    "Windows 성능 때문에 여기를 내리지 마세요 — 이 에셋은 두 플랫폼이 공유하므로 " +
                    "macOS 화질만 잃고 Windows에는 아무 영향이 없습니다(Windows는 " +
                    "RenderQualityTuner.WindowsDefaultSamples가 런타임에 덮어씁니다).");
            }

            Assert.Greater(levelCount, 0,
                "QualitySettings.asset에서 antiAliasing 항목을 한 개도 찾지 못했습니다 — " +
                "에셋 형식이 바뀌었다면 이 테스트의 파싱을 먼저 고치세요(조용히 통과시키지 말 것).");
        }

        [Test]
        public void 에디터에서는_플랫폼_기본값이_적용되지_않는다()
        {
            // macOS 무영향 보증의 런타임 측 근거. 에디터에서 Windows 타깃을 잡아도
            // (UNITY_STANDALONE_WIN이 켜져도) !UNITY_EDITOR 조건이 덮어쓰기를 막아야 한다 —
            // 안 그러면 계측/제품 설정이 공유 에셋을 더럽힌 채 커밋될 수 있다.
            Assert.AreEqual(MacOsBaselineSamples, QualitySettings.antiAliasing,
                "에디터의 QualitySettings.antiAliasing이 4x가 아닙니다. 플랫폼 기본값 적용이 " +
                "에디터까지 새어 들어왔거나(=RenderQualityTuner의 !UNITY_EDITOR 가드가 사라졌거나), " +
                "누군가 Quality Settings UI에서 값을 바꾼 뒤 커밋한 것입니다.");
        }

        [Test]
        public void 플랫폼_덮어쓰기는_Windows_플레이어에만_걸린다()
        {
            string src = ReadSource(TunerSourcePath);

            StringAssert.Contains("UNITY_STANDALONE_WIN && !UNITY_EDITOR", src,
                "플랫폼 분기 가드가 사라졌거나 조건이 바뀌었습니다. macOS/에디터/모바일에서는 " +
                "덮어쓰기가 **호출조차 되지 않아야** 합니다(CLAUDE.md 플랫폼 동시 검토 규칙).");

            // 유효 시점 잠금: 이 값은 백버퍼가 만들어지기 전에만 먹는다. 다른 시점으로 옮기면
            // 코드는 멀쩡히 돌지만 아무 효과가 없고, Screen.msaaSamples는 걸린 척 거짓말을 한다.
            StringAssert.Contains("RuntimeInitializeLoadType.BeforeSceneLoad", src,
                "MSAA 적용 시점이 BeforeSceneLoad가 아닙니다. 그 뒤에 바꾸면 백버퍼에 반영되지 " +
                "않으면서 Screen.msaaSamples만 바뀐 척합니다(실측으로 닫힌 길 — " +
                "RenderQualityTuner의 해당 주석 참고).");
        }

        [Test]
        public void 계측용_환경변수는_계속_살아있다()
        {
            // 기본값이 정해졌다고 A/B 손잡이를 치우면, 다음에 렉 신고가 왔을 때 다시 빌드부터
            // 해야 한다. 이 라운드가 짧게 끝난 이유가 바로 이 손잡이가 있었기 때문이다.
            Assert.AreEqual("STICKMATE_FORCE_MSAA", RenderQualityTuner.MsaaEnvironmentVariableName,
                "계측용 MSAA 환경변수 이름이 바뀌었습니다. 사용자 안내 문서(RenderQualityTuner XML " +
                "주석의 macOS/Windows 실행 예)와 함께 고치세요.");

            string src = ReadSource(TunerSourcePath);
            StringAssert.Contains("$env:STICKMATE_FORCE_MSAA", src,
                "Windows(PowerShell) 실행 예시가 사라졌습니다. macOS의 `open --env` 예시만 남으면 " +
                "정작 문제가 재현되는 플랫폼에서 A/B를 못 합니다.");
        }

        [Test]
        public void GPU_프레임시간_계측이_빌드_설정으로_켜진다()
        {
            // 이 설정이 꺼지면 [렌더진단]의 GPU 프레임시간이 "측정 불가"가 되고, 60fps 상한에 가려
            // 렌더 비용을 볼 방법이 사라진다(CPU 프레임시간은 4x든 0x든 똑같이 16.7ms다).
            string path = Path.Combine(RepoRoot, "ProjectSettings", "ProjectSettings.asset");
            Assert.IsTrue(File.Exists(path), $"ProjectSettings.asset을 찾지 못했습니다: {path}");

            StringAssert.Contains("enableFrameTimingStats: 1", File.ReadAllText(path),
                "enableFrameTimingStats가 꺼져 있습니다. 이 값이 0이면 Windows 렌더 비용을 원격으로 " +
                "측정할 수단이 없어집니다(개발 머신이 macOS라 실기 프로파일러를 붙일 수 없습니다). " +
                "빌드 시 BuildStandalone.ConfigureRenderDiagnostics()가 켜므로, 0이라면 그 호출이 " +
                "빠졌거나 설정이 되돌려진 것입니다.");
        }
    }
}
