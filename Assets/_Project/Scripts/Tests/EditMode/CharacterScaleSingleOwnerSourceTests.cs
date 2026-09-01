using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <b>배율의 문은 하나다</b> — 소스 정적 스캔(docs/UX_FLOW.md 35-1-3 ①).
    ///
    /// ============================================================================
    /// 런타임 테스트만으로는 부족한 이유
    /// ============================================================================
    /// <c>SettingsCharacterScaleSingleSourceTests</c>는 "지금 두 UI가 같은 값을 가리킨다"를 확인한다.
    /// 그런데 이 계약이 실제로 깨지는 방식은 <b>세 번째 UI가 생기는 날</b>이다 — 그 UI가 게이트를
    /// 다시 구현하거나 <c>StickmanAgent.ApplyCharacterScale</c>을 직접 부르면, 그 경로에서만
    /// 랙돌 유예가 없고 알림 이벤트도 없다. 런타임 테스트는 <b>그 새 UI를 모르므로</b> 잡지 못한다.
    ///
    /// 그래서 <c>DeployedConfigAssetImmutabilityTests</c>의 <c>.inkColor =</c> 정적 스캔과 <b>같은
    /// 방식</b>으로 패턴 자체를 막는다. 주석 줄은 건너뛴다 — 이 라운드의 문서가 옛 호출부를 그대로
    /// 인용하고 있기 때문이다.
    /// </summary>
    public sealed class CharacterScaleSingleOwnerSourceTests
    {
        private const string LogPrefix = "[크기단일소유-TEST]";

        private static string ScriptsRoot => Path.Combine(Application.dataPath, "_Project", "Scripts");

        private static List<string> ProductionFiles()
        {
            string testsRoot = (Path.Combine(ScriptsRoot, "Tests") + Path.DirectorySeparatorChar).Replace('\\', '/');
            var files = new List<string>(Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories));
            files.RemoveAll(p => p.Replace('\\', '/').StartsWith(testsRoot, System.StringComparison.Ordinal));
            Assert.GreaterOrEqual(files.Count, 40,
                $"{LogPrefix} 스캔 대상 파일이 비정상적으로 적습니다({files.Count}) — 경로 계산 오류로 허위 통과할 위험.");
            return files;
        }

        private static bool IsCommentLine(string line)
        {
            string t = line.TrimStart();
            return t.StartsWith("//") || t.StartsWith("///") || t.StartsWith("*") || t.StartsWith("/*");
        }

        private static List<string> FindCallSites(string needle, params string[] allowedFileNames)
        {
            var allowed = new HashSet<string>(allowedFileNames);
            var hits = new List<string>();
            foreach (string file in ProductionFiles())
            {
                string name = Path.GetFileName(file);
                if (allowed.Contains(name)) continue;
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (IsCommentLine(lines[i])) continue;
                    if (lines[i].IndexOf(needle, System.StringComparison.Ordinal) < 0) continue;
                    hits.Add($"{name}:{i + 1}: {lines[i].Trim()}");
                }
            }
            return hits;
        }

        private static string ReadProductionFile(string fileName)
        {
            foreach (string file in ProductionFiles())
            {
                if (Path.GetFileName(file) == fileName) return File.ReadAllText(file);
            }
            Assert.Fail($"{LogPrefix} {fileName}을(를) 찾지 못했습니다.");
            return string.Empty;
        }

        // ============================================================================
        // (1) 적용은 컨트롤러만 부른다
        // ============================================================================

        [Test]
        public void 실캐릭터에_배율을_넣는_호출은_CharacterScaleController에만_있다()
        {
            // StickmanAgent.cs는 이 메서드를 <b>선언</b>하는 파일이라 제외한다(호출부가 아니다).
            List<string> hits = FindCallSites(".ApplyCharacterScale(",
                "CharacterScaleController.cs", "StickmanAgent.cs");

            Debug.Log($"{LogPrefix} ApplyCharacterScale 외부 호출 {hits.Count}건.");

            Assert.IsTrue(hits.Count == 0,
                $"{LogPrefix} 배율의 단일 소스를 우회하는 호출이 발견됐습니다. 이 경로에는 랙돌/스펙터클 " +
                "적용 게이트(34-3-6)도, 다른 UI에 알리는 이벤트도 없습니다 — 그 순간 '표시 숫자와 실제 " +
                "값이 다른' 화면이 가능해집니다(원칙 1). CharacterScaleController.Request(v, reason)를 " +
                "부르세요.\n\n" + string.Join("\n", hits));
        }

        // ============================================================================
        // (2) 두 UI가 <b>같은 문</b>을 지나고, 둘 다 구독자다
        // ============================================================================

        [Test]
        public void 구석_다이얼과_설정창_슬라이더는_둘_다_컨트롤러를_지나고_둘_다_구독한다()
        {
            string corner = ReadProductionFile("CornerHoverPanel.cs");
            string settings = ReadProductionFile("SettingsWindow.cs");

            StringAssert.Contains("CharacterScaleController.Request(", corner,
                $"{LogPrefix} 구석 다이얼이 단일 소스를 지나지 않습니다.");
            StringAssert.Contains("CharacterScaleController.Request(", settings,
                $"{LogPrefix} 설정창 슬라이더가 단일 소스를 지나지 않습니다.");

            StringAssert.Contains("StickmanEventBus.CharacterScaleChanged +=", corner,
                $"{LogPrefix} 구석 다이얼이 배율 변경 이벤트를 구독하지 않습니다 — 설정창에서 바꾼 값을 " +
                "따라오지 못합니다(원칙 1).");
            StringAssert.Contains("StickmanEventBus.CharacterScaleChanged +=", settings,
                $"{LogPrefix} 설정창이 배율 변경 이벤트를 구독하지 않습니다 — 다이얼에서 바꾼 값을 " +
                "따라오지 못합니다(원칙 1).");

            // 구독은 <b>반드시</b> 해제된다 — 정적 이벤트가 파괴된 MonoBehaviour를 붙들면 누수다
            // (StickmanEventBus 클래스 문서 3항).
            StringAssert.Contains("StickmanEventBus.CharacterScaleChanged -=", corner,
                $"{LogPrefix} 구석 다이얼이 구독을 해제하지 않습니다(정적 이벤트 누수).");
            StringAssert.Contains("StickmanEventBus.CharacterScaleChanged -=", settings,
                $"{LogPrefix} 설정창이 구독을 해제하지 않습니다(정적 이벤트 누수).");
        }

        // ============================================================================
        // (3) 게이트가 복제되지 않았다
        // ============================================================================

        [Test]
        public void 랙돌_적용_게이트의_이름은_CharacterScaleController에만_있다()
        {
            // 왜 "상태 목록"이 아니라 <b>게이트의 이름</b>을 스캔하는가: 상태 이름(ThrowTumble/RodeoCursor)은
            // 구석 패널의 캡션 표(<c>ResolveCaption</c>)에도 정당하게 등장한다 — 그것까지 잡으면 오탐이다.
            // 진짜 복제는 판정 메서드를 통째로 베낄 때 일어나고, 그때 이름이 함께 따라온다.
            //
            // 이 검사가 놓치는 경우(이름을 바꿔 베끼는 것)는 위 (1)이 막는다: 게이트를 아무리 잘 베껴도
            // ApplyCharacterScale을 부를 수 없으면 그 게이트는 아무 일도 하지 못한다. 두 검사가 겹쳐
            // "복제 후 우회"의 두 경로를 모두 덮는다.
            List<string> hits = FindCallSites("CanApplyNow", "CharacterScaleController.cs");

            Debug.Log($"{LogPrefix} 게이트 이름 외부 사용 {hits.Count}건.");

            Assert.IsTrue(hits.Count == 0,
                $"{LogPrefix} 적용 게이트가 복제된 것으로 보입니다. 규칙이 두 벌이 되면 유예 시간을 바꿀 때 " +
                "한쪽만 고쳐지고, 그때부터 '어디서 바꿨느냐에 따라 반응이 다른 앱'이 됩니다. " +
                "판정은 CharacterScaleController.CanApplyNow 하나뿐이어야 합니다.\n\n" +
                string.Join("\n", hits));
        }

        // ============================================================================
        // (4) 설정창은 배포 에셋/저장 모델을 직접 만지지 않는다
        // ============================================================================

        [Test]
        public void 설정창은_배율을_저장모델이나_에셋에_직접_쓰지_않는다()
        {
            string settings = ReadProductionFile("SettingsWindow.cs");

            StringAssert.DoesNotContain("UiLayoutModel.SetCharacterScale(", settings,
                $"{LogPrefix} 설정창이 저장 모델에 직접 씁니다 — 그러면 게이트를 건너뛴 값이 저장되고, " +
                "다음 실행에서 실캐릭터와 저장값이 갈라질 수 있습니다. 컨트롤러가 이미 대신 씁니다.");
            StringAssert.DoesNotContain("SetRuntimeCharacterScale(", settings,
                $"{LogPrefix} 설정창이 StickConfig의 런타임 배율을 직접 씁니다 — 그러면 지오메트리/획 두께/" +
                "보행 속도를 함께 갱신하는 5단계 원자 처리를 건너뜁니다(StickmanAgent.ApplyCharacterScale).");
        }
    }
}
