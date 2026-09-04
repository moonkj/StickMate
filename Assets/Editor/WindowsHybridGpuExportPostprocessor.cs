using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using StickMate.Platform;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace StickMate.EditorTools
{
    /// <summary>
    /// Windows 빌드 후처리 — 산출된 exe의 <b>하이브리드 GPU export 값 두 개</b>를 중립값으로 바꾼다.
    ///
    /// ========================================================================
    /// 무엇을 왜 (근거 전량: docs/verify/WINDOWS_DGPU_REPORT.md)
    /// ========================================================================
    /// Unity의 Windows 플레이어 템플릿은 <c>NvOptimusEnablement</c>와
    /// <c>AmdPowerXpressRequestHighPerformance</c>를 <b>값 1</b>(= 외장 GPU를 써라)로 하드코딩해
    /// 출하한다. 이 값은 <b>드라이버가 프로세스 시작 전에</b> 읽으므로 런타임 C#으로는 어떤 방법으로도
    /// 바꿀 수 없다. 24시간 상주하는 이 앱에서 그것은 배터리·팬 소음 문제로 직결된다.
    /// 그래서 빌드 산출물의 <b>값 DWORD 2개, 총 8바이트 중 2바이트</b>만 0으로 바꾼다.
    ///
    /// <para><b>심볼 이름은 건드리지 않는다.</b> export 이름 테이블은 사전순 오름차순이어야 하고
    /// (<c>GetProcAddress</c>가 이진 탐색을 한다), 이름을 망가뜨리면 같은 테이블의
    /// <c>D3D12SDKVersion</c>/<c>D3D12SDKPath</c> 조회까지 깨진다. 그 둘은 D3D12 Agility SDK 로더가
    /// 실제로 읽는 심볼이다. 값 패치는 export 테이블·섹션 헤더·재배치·크기·정렬을 하나도 바꾸지 않는다.</para>
    ///
    /// ========================================================================
    /// ★ 이 훅이 지키는 안전 규칙 다섯
    /// ========================================================================
    /// <list type="number">
    ///   <item><b>하드코딩 오프셋을 쓰지 않는다.</b> PE 헤더 → 데이터 디렉터리[0] → export 디렉터리를
    ///     파싱해 <b>심볼 이름으로</b> RVA를 찾고, 섹션 헤더를 경유해 파일 오프셋으로 옮긴다.
    ///     오프셋 상수를 박아 두면 Unity 마이너 버전이 올라 템플릿이 밀리는 순간 <b>엉뚱한 바이트를
    ///     덮어쓴다</b> — 이 접근의 진짜 사고 지점이다.</item>
    ///   <item><b>쓰기 전에 값을 확인한다.</b> 현재 값이 1이면 쓰고, 이미 0이면 쓰지 않고 통과(멱등),
    ///     <b>그 밖의 값이면 한 바이트도 쓰지 않고 빌드를 세운다.</b> 템플릿이 바뀐 상태에서 쓰는 것은
    ///     추측으로 바이너리를 덮는 것이다.</item>
    ///   <item><b>쓴 뒤 디스크에서 다시 읽어 처음부터 다시 파싱해</b> 두 값이 0인지 확인한다.
    ///     쓰기만 하고 안 읽으면 조용한 실패다.</item>
    ///   <item><b>바뀐 바이트 집합을 대조한다.</b> 원본과 결과를 전량 비교해 달라진 바이트가 두 값
    ///     DWORD 안에만 있는지 본다. 하나라도 밖에 있으면 실패다.</item>
    ///   <item><b>실패는 요란해야 한다.</b> 어떤 실패든 <see cref="BuildFailedException"/>을 던지고
    ///     산출물 옆에 영수증 파일을 남긴다. 이 저장소가 반복해 당한 사고는
    ///     「패치가 안 먹었는데 exe는 나온다」이다.</item>
    /// </list>
    ///
    /// ========================================================================
    /// ★ 확인됨 / 미확인
    /// ========================================================================
    /// <b>확인됨</b>(이 머신에서 정적 실측): 파싱·패치·되읽기가 실제 출하 exe에서 동작하고, 바뀌는
    /// 바이트는 정확히 2개다. <b>미확인</b>: 값 0이 사용자 실기에서 실제로 내장 GPU로 귀결되는가.
    /// 이 머신에는 Windows가 없다. <b>"고쳤다"가 아니라 "이렇게 동작할 것으로 판단한다, 실기 미확인"이다.</b>
    ///
    /// <para>독립 검증(같은 코드를 재사용하지 않는 별도 계기):
    /// <c>python3 Tools/BuildVerify/check_dgpu_exports.py Builds/Windows/StickMate.exe --expect 0 --template</c></para>
    ///
    /// <para><b>남은 미확인 하나</b>: Unity가 이 콜백 <b>뒤에</b> exe를 다시 만지는 단계가 있는지는
    /// 확인되지 않았다. 그래서 <see cref="callbackOrder"/>를 크게 잡아 마지막에 돌고, 최종 판정은
    /// 위 파이썬 검증기로 <b>빌드가 끝난 뒤</b> 하도록 설계했다.</para>
    /// </summary>
    public sealed class WindowsHybridGpuExportPostprocessor : IPostprocessBuildWithReport
    {
        /// <summary>빌드 로그에서 이 훅의 흔적을 찾는 <b>단일 토큰</b>. ASCII만 쓴다 —
        /// 어떤 셸/로그 뷰어에서도 <c>grep</c>이 그대로 먹게 하기 위해서다.</summary>
        public const string LogTag = "[dGPU-export]";

        /// <summary>산출물 옆에 남기는 영수증 파일 이름. 매 빌드마다 덮어쓴다.</summary>
        public const string ReceiptFileName = "dgpu-export-patch.txt";

        /// <summary>다른 후처리보다 <b>뒤에</b> 돌게 크게 잡는다.</summary>
        public int callbackOrder => 10000;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report == null)
            {
                Debug.LogWarning($"{LogTag} RESULT=SKIP — BuildReport가 null이다.");
                return;
            }

            BuildSummary summary = report.summary;

            // ★ Windows(x64)가 아니면 즉시 나간다. macOS 산출물에는 한 바이트도 닿지 않는다.
            if (summary.platform != BuildTarget.StandaloneWindows64)
            {
                Debug.Log($"{LogTag} RESULT=SKIP — 대상 플랫폼이 {summary.platform}이라 " +
                          "아무 파일도 열지 않는다(이 후처리는 StandaloneWindows64 전용이다).");
                return;
            }

            if (summary.result == BuildResult.Failed || summary.result == BuildResult.Cancelled)
            {
                Debug.Log($"{LogTag} RESULT=SKIP — 빌드 결과가 {summary.result}라 산출물을 만지지 않는다.");
                return;
            }

            string exePath = summary.outputPath;
            var log = new StringBuilder();
            log.Append("StickMate — Windows dGPU export 패치 영수증\n")
               .Append("시각: ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append('\n')
               .Append("대상: ").Append(exePath).Append('\n');

            try
            {
                Apply(exePath, log);
            }
            catch (BuildFailedException)
            {
                throw;   // Fail()이 이미 로그와 영수증을 남겼다.
            }
            catch (Exception e)
            {
                Fail(exePath, log, $"예상치 못한 예외 — {e.GetType().Name}: {e.Message}");
            }
        }

        // ====================================================================
        // 본체
        // ====================================================================

        private void Apply(string exePath, StringBuilder log)
        {
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                Fail(exePath, log, $"산출물이 없다 — '{exePath}'. 빌드가 exe를 만들지 않았거나 경로가 바뀌었다.");
                return;
            }

            byte[] original = File.ReadAllBytes(exePath);
            log.Append("크기: ").Append(original.Length).Append(" bytes\n");

            if (!PortableExecutableExportReader.TryReadExports(original, out List<PeExportedSymbol> before, out string parseError))
            {
                Fail(exePath, log, "PE export 파싱 실패 — " + parseError);
                return;
            }

            log.Append("export ").Append(before.Count).Append("개: ");
            for (int i = 0; i < before.Count; i++)
            {
                if (i > 0) log.Append(", ");
                log.Append(before[i].Name);
            }
            log.Append('\n');

            if (!PortableExecutableExportReader.AreExportNamesAscending(before))
            {
                Fail(exePath, log, "export 이름 테이블이 사전순 오름차순이 아니다 — 우리가 만지기 전부터 " +
                     "GetProcAddress의 이진 탐색이 깨져 있다. 이 상태에서 바이트를 쓰지 않는다.");
                return;
            }

            // ---- 1단계: 두 심볼 모두 찾고, 두 값 모두 판정한다(하나라도 이상하면 쓰지 않는다) ----
            var targets = new List<(string Name, int Offset, uint Current, HybridGpuPreferencePolicy.Verdict Verdict)>();
            var problems = new List<string>();

            foreach (string symbolName in HybridGpuPreferencePolicy.WindowsExportSymbols)
            {
                if (!PortableExecutableExportReader.TryFind(before, symbolName, out PeExportedSymbol symbol))
                {
                    problems.Add($"심볼 '{symbolName}'이 export 테이블에 없다. 플레이어 템플릿이 바뀌었을 수 있다.");
                    continue;
                }
                if (symbol.IsForwarder || symbol.FileOffset < 0)
                {
                    problems.Add($"심볼 '{symbolName}'이 데이터가 아니다(forwarder이거나 파일에 실체가 없다).");
                    continue;
                }
                if (!PortableExecutableExportReader.TryReadUInt32(original, symbol.FileOffset, out uint current, out string readError))
                {
                    problems.Add($"심볼 '{symbolName}'의 값을 읽을 수 없다 — {readError}");
                    continue;
                }

                HybridGpuPreferencePolicy.Verdict verdict = HybridGpuPreferencePolicy.Classify(current, out string reason);
                log.Append("  ").Append(symbolName)
                   .Append("  RVA 0x").Append(symbol.Rva.ToString("x", CultureInfo.InvariantCulture))
                   .Append("  섹션 ").Append(symbol.SectionName)
                   .Append("  파일오프셋 0x").Append(symbol.FileOffset.ToString("x", CultureInfo.InvariantCulture))
                   .Append("  현재값 ").Append(current)
                   .Append("  판정 ").Append(verdict).Append(" — ").Append(reason).Append('\n');

                if (verdict == HybridGpuPreferencePolicy.Verdict.Unexpected)
                {
                    problems.Add($"'{symbolName}' {reason}");
                    continue;
                }
                targets.Add((symbolName, symbol.FileOffset, current, verdict));
            }

            if (problems.Count > 0)
            {
                Fail(exePath, log, "값을 쓰기 전 검사에서 걸렸다(한 바이트도 쓰지 않았다):\n  · " +
                     string.Join("\n  · ", problems));
                return;
            }

            // ---- 2단계: 필요한 것만 메모리에서 바꾼다 ----
            byte[] patched = (byte[])original.Clone();
            var expectedChangedOffsets = new HashSet<int>();
            int written = 0;

            foreach ((string name, int offset, uint current, HybridGpuPreferencePolicy.Verdict verdict) in targets)
            {
                for (int i = 0; i < 4; i++) expectedChangedOffsets.Add(offset + i);
                if (verdict == HybridGpuPreferencePolicy.Verdict.AlreadyNeutral) continue;

                uint desired = HybridGpuPreferencePolicy.DesiredValue;
                patched[offset + 0] = (byte)(desired & 0xFF);
                patched[offset + 1] = (byte)((desired >> 8) & 0xFF);
                patched[offset + 2] = (byte)((desired >> 16) & 0xFF);
                patched[offset + 3] = (byte)((desired >> 24) & 0xFF);
                written++;
                log.Append("  쓰기: ").Append(name).Append(' ').Append(current).Append(" -> ")
                   .Append(desired).Append(" (파일오프셋 0x")
                   .Append(offset.ToString("x", CultureInfo.InvariantCulture)).Append(")\n");
            }

            if (written == 0)
            {
                log.Append("  쓰기 없음 — 두 값이 이미 중립값이다(멱등 통과).\n");
            }
            else
            {
                File.WriteAllBytes(exePath, patched);
            }

            // ---- 3단계: 디스크에서 다시 읽어 처음부터 다시 파싱한다 ----
            byte[] after = File.ReadAllBytes(exePath);

            if (after.Length != original.Length)
            {
                Fail(exePath, log, $"쓰기 후 파일 크기가 바뀌었다({original.Length} -> {after.Length}). " +
                     "값 패치는 길이를 바꾸지 않는다 — 무언가 잘못됐다.");
                return;
            }

            var strayOffsets = new List<int>();
            int changedBytes = 0;
            for (int i = 0; i < after.Length; i++)
            {
                if (original[i] == after[i]) continue;
                changedBytes++;
                if (!expectedChangedOffsets.Contains(i)) strayOffsets.Add(i);
            }
            log.Append("달라진 바이트: ").Append(changedBytes).Append("개 (값 DWORD 밖: ")
               .Append(strayOffsets.Count).Append("개)\n");
            if (strayOffsets.Count > 0)
            {
                Fail(exePath, log, $"값 DWORD 밖의 바이트가 {strayOffsets.Count}개 바뀌었다(첫 오프셋 0x" +
                     strayOffsets[0].ToString("x", CultureInfo.InvariantCulture) + ") — 오프셋 계산을 의심하라.");
                return;
            }

            if (!PortableExecutableExportReader.TryReadExports(after, out List<PeExportedSymbol> verify, out string verifyParseError))
            {
                Fail(exePath, log, "쓰기 후 PE 재파싱 실패 — " + verifyParseError +
                     " (파일이 손상됐을 수 있다. 다시 빌드하라.)");
                return;
            }

            if (!PortableExecutableExportReader.AreExportNamesAscending(verify))
            {
                Fail(exePath, log, "쓰기 후 export 이름 테이블 순서가 깨졌다 — 값만 바꾸는 패치에서 " +
                     "일어날 수 없는 일이다.");
                return;
            }

            var verified = new List<string>();
            foreach (string symbolName in HybridGpuPreferencePolicy.WindowsExportSymbols)
            {
                if (!PortableExecutableExportReader.TryFind(verify, symbolName, out PeExportedSymbol symbol))
                {
                    Fail(exePath, log, $"쓰기 후 심볼 '{symbolName}'이 사라졌다.");
                    return;
                }
                if (!PortableExecutableExportReader.TryReadUInt32(after, symbol.FileOffset, out uint value, out string readError))
                {
                    Fail(exePath, log, $"쓰기 후 '{symbolName}'의 값을 읽을 수 없다 — {readError}");
                    return;
                }
                if (value != HybridGpuPreferencePolicy.DesiredValue)
                {
                    Fail(exePath, log, $"되읽기 검증 실패 — '{symbolName}'이 {value}다. " +
                         $"기대값은 {HybridGpuPreferencePolicy.DesiredValue}였다.");
                    return;
                }
                verified.Add($"{symbolName}={value}");
            }

            log.Append("RESULT=PASS\n");
            WriteReceipt(exePath, log);

            Debug.Log($"{LogTag} RESULT=PASS — 되읽기 검증 통과: {string.Join(", ", verified)} " +
                      $"(쓰기 {written}건, 달라진 바이트 {changedBytes}개, 값 DWORD 밖 0개). " +
                      $"영수증: {ReceiptFileName}. " +
                      "★ 실기 미확인: 값 0이 실제로 내장 GPU로 귀결되는지는 Windows 실기에서 확인해야 한다 — " +
                      "python3 Tools/BuildVerify/check_dgpu_exports.py <exe> --expect 0 --template");
        }

        // ====================================================================
        // 실패 — 조용히 넘어가지 않는다
        // ====================================================================

        private void Fail(string exePath, StringBuilder log, string reason)
        {
            log.Append("RESULT=FAIL\n사유: ").Append(reason).Append('\n');
            WriteReceipt(exePath, log);

            string message =
                $"{LogTag} RESULT=FAIL — {reason}\n" +
                "이 실패는 의도된 것이다. 패치되지 않은 exe가 조용히 출하되는 것보다 빌드가 서는 편이 낫다.\n" +
                "확인 순서: (1) 산출물 옆 " + ReceiptFileName + " 를 읽는다 " +
                "(2) python3 Tools/BuildVerify/check_dgpu_exports.py <exe> --template 로 현재 값을 독립 확인한다 " +
                "(3) 템플릿이 바뀐 것이라면 docs/verify/WINDOWS_DGPU_REPORT.md 를 갱신하고 " +
                "Platform/HybridGpuPreferencePolicy.cs 의 기대값을 다시 판정한다.";

            Debug.LogError(message);
            throw new BuildFailedException(message);
        }

        private static void WriteReceipt(string exePath, StringBuilder log)
        {
            try
            {
                string dir = string.IsNullOrEmpty(exePath) ? null : Path.GetDirectoryName(exePath);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
                File.WriteAllText(Path.Combine(dir, ReceiptFileName), log.ToString(), Encoding.UTF8);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogTag} 영수증 파일을 쓰지 못했다 — {e.Message} " +
                                 "(판정 자체는 위 로그가 정본이다).");
            }
        }
    }
}
