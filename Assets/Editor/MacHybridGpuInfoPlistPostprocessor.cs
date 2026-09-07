using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using StickMate.Platform;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using Debug = UnityEngine.Debug;

namespace StickMate.EditorTools
{
    /// <summary>
    /// macOS 빌드 후처리 — 산출된 <c>.app</c>의 <c>Contents/Info.plist</c>에
    /// <b>자동 그래픽 전환 선언</b>(<c>NSSupportsAutomaticGraphicsSwitching</c>)을 보장한다.
    /// Windows 쪽 <see cref="WindowsHybridGpuExportPostprocessor"/>와 <b>같은 판정
    /// (<see cref="HybridGpuPreferencePolicy"/>)을 읽는 대칭 훅</b>이다.
    ///
    /// ========================================================================
    /// 왜 (사용자 실기 신고, 2026-09-07)
    /// ========================================================================
    /// 사용자가 듀얼 GPU Mac에서 직접 신고했다: <i>"내장 그래픽을 강제사용하도록 자동설정이 되어야
    /// 하는데 외장 그래픽을 사용함."</i> 이 키가 <b>없는</b> 앱이 Metal 컨텍스트를 만들면 macOS는
    /// 보수적으로 디스크리트 GPU를 깨운다. 24시간 상주하는 이 앱에서 그것은 발열·팬·배터리로 직결된다.
    /// Windows에서 PE export 값을 1 → 0으로 되돌린 것과 <b>같은 축의 결정</b>이다(상주 앱의 자원 예절).
    ///
    /// ========================================================================
    /// ★ 왜 빌드 후처리인가 — 다른 길이 없다는 것을 실측으로 확인했다
    /// ========================================================================
    /// <list type="number">
    ///   <item>Unity 6000.0.82f1 에디터 설치 <b>전체</b>를 문자열 탐색했을 때
    ///     <c>NSSupportsAutomaticGraphicsSwitching</c>은 <b>0건</b>이다(양성 대조로 같은 방법이
    ///     <c>NSPrincipalClass</c>는 4건을 찾는다). 즉 <c>PlayerSettings</c> API도, 체크박스도 없다.</item>
    ///   <item>플레이어 템플릿
    ///     (<c>PlaybackEngines/MacStandaloneSupport/Source/…/MacPlayerEntryPoint/Info.plist</c>)에도
    ///     이 키가 없고, 실제 출하된 <c>Info.plist</c>에도 없다(둘 다 직접 열어 확인).</item>
    /// </list>
    /// ⇒ 남는 길은 <b>빌드된 plist를 후처리로 고치는 것</b> 하나뿐이다.
    ///
    /// ========================================================================
    /// ★★ 서명 — 이 훅에서 가장 위험한 지점이다 (실측으로 확인했다)
    /// ========================================================================
    /// 빌드된 <c>.app</c>은 <b>애드혹 서명</b>돼 있고 <c>Info.plist</c>는 그 서명에 <b>봉인</b>된다
    /// (<c>codesign -dv</c>가 <c>Info.plist entries=17</c>을 보고한다). 그래서 plist를 한 글자만 고쳐도
    /// 서명이 깨진다 — 실측:
    /// <code>
    ///   codesign --verify --deep --strict StickMate.app
    ///   → invalid Info.plist (plist or signature have been modified)   (rc=1)
    /// </code>
    /// <b>Apple Silicon에서 서명이 깨진 앱은 아예 실행되지 않는다.</b> 즉 "GPU를 아끼려다 앱을 못 켜게
    /// 만드는" 사고가 바로 여기에 있다. 그래서 이 훅은 고친 뒤 <b>반드시 재서명하고 다시 검증한다</b>:
    /// <code>
    ///   codesign --force --sign - StickMate.app      (최상위 번들만 — 중첩 서명은 건드리지 않는다)
    ///   codesign --verify --deep --strict StickMate.app   → rc=0, Info.plist entries=18
    /// </code>
    ///
    /// <para><b>애드혹이 아닌 서명은 절대 덮지 않는다.</b> 진짜 인증서로 서명된 앱을 우리가 애드혹으로
    /// 재서명하면 그건 <b>서명 등급을 몰래 낮추는 것</b>이다. 그런 상태를 만나면 아무것도 쓰지 않고
    /// 빌드를 세운다.</para>
    ///
    /// ========================================================================
    /// ★ 이 훅이 지키는 안전 규칙 (Windows 훅과 같은 다섯)
    /// ========================================================================
    /// <list type="number">
    ///   <item><b>구조를 추측하지 않는다.</b> plist를 진짜 XML 파서로 읽어
    ///     <see cref="PropertyListRootReader"/>가 <b>루트 dict의 직계 항목만</b> 뽑는다.
    ///     중첩 dict 안의 같은 이름은 "있다"로 세지 않는다.</item>
    ///   <item><b>쓰기 전에 판정한다.</b> 없으면 삽입, 이미 <c>true</c>면 멱등 통과,
    ///     <b>그 밖(예: 누가 <c>false</c>로 꺼 둠)이면 한 글자도 쓰지 않고 빌드를 세운다.</b></item>
    ///   <item><b>쓴 뒤 디스크에서 다시 읽어 처음부터 다시 파싱한다.</b></item>
    ///   <item><b>바뀐 것이 삽입 한 덩어리뿐인지 대조한다.</b> 기존 키의 값이 하나라도 달라졌거나
    ///     항목 수가 정확히 +1이 아니면 실패다.</item>
    ///   <item><b>실패는 요란해야 한다.</b> <see cref="BuildFailedException"/>을 던지고 산출물 <b>옆</b>에
    ///     영수증을 남긴다(번들 <b>안</b>에 쓰면 그 자체가 봉인을 깬다).</item>
    /// </list>
    ///
    /// ========================================================================
    /// ★ 확인됨 / 미확인 — 흐리지 마라
    /// ========================================================================
    /// <b>확인 가능한 것</b>(이 머신): 키가 plist에 올바르게 들어가고, 다른 키가 그대로이며,
    /// 서명이 다시 유효해진다. <b>미확인</b>: 그 키가 실제로 <b>내장 GPU 선택</b>으로 귀결되는가.
    /// 이 개발 머신은 Apple Silicon이라 <b>내장/외장이라는 개념 자체가 없다</b> — 재현이 불가능하다.
    /// 사용자의 듀얼 GPU Mac에서 확인해야 확정된다. <b>"고쳤다"가 아니라 "이렇게 동작할 것으로
    /// 판단한다, 실기 미확인"이다.</b>
    ///
    /// <para><b>그리고 이 훅은 GPU 사용률을 낮추지 않는다.</b> 같은 신고에 "GPU가 90%까지도 올라감"이
    /// 함께 왔다. 이 키는 <b>어느 GPU가 그리는가</b>만 바꾸고 <b>얼마나 그리는가</b>는 손대지 않는다 —
    /// 부하가 그대로면 그 부하가 내장 GPU로 옮겨갈 뿐이다. 렌더 부하는 별도 라운드의 일이다.</para>
    ///
    /// <para>독립 검증(이 훅과 코드를 한 줄도 공유하지 않는 별도 계기):
    /// <c>python3 Tools/BuildVerify/check_mac_gpu_switching.py Builds/macOS/StickMate.app</c></para>
    /// </summary>
    public sealed class MacHybridGpuInfoPlistPostprocessor : IPostprocessBuildWithReport
    {
        /// <summary>빌드 로그에서 이 훅의 흔적을 찾는 <b>단일 토큰</b>. ASCII만 쓴다.</summary>
        public const string LogTag = "[hybridGPU-plist]";

        /// <summary>산출물 <b>옆</b>(번들 밖)에 남기는 영수증 파일 이름. 매 빌드마다 덮어쓴다.</summary>
        public const string ReceiptFileName = "mac-gpu-switching-plist.txt";

        /// <summary>번들 안에서 plist가 있는 상대 경로.</summary>
        public const string InfoPlistRelativePath = "Contents/Info.plist";

        /// <summary>서명이 존재하는지 판별하는 <b>디스크 사실</b>. <c>codesign</c> 없이도 볼 수 있다.</summary>
        public const string CodeSignatureRelativePath = "Contents/_CodeSignature";

        private const string CodesignToolPath = "/usr/bin/codesign";
        private const string AdhocSignatureMarker = "Signature=adhoc";
        private const int ToolTimeoutMilliseconds = 120000;

        /// <summary>다른 후처리보다 <b>뒤에</b> 돌게 크게 잡는다(Windows 훅과 같은 값).</summary>
        public int callbackOrder => 10000;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report == null)
            {
                Debug.LogWarning($"{LogTag} RESULT=SKIP — BuildReport가 null이다.");
                return;
            }

            BuildSummary summary = report.summary;

            // ★ macOS가 아니면 즉시 나간다. Windows 산출물에는 한 바이트도 닿지 않는다.
            if (summary.platform != BuildTarget.StandaloneOSX)
            {
                Debug.Log($"{LogTag} RESULT=SKIP — 대상 플랫폼이 {summary.platform}이라 " +
                          "아무 파일도 열지 않는다(이 후처리는 StandaloneOSX 전용이다).");
                return;
            }

            if (summary.result == BuildResult.Failed || summary.result == BuildResult.Cancelled)
            {
                Debug.Log($"{LogTag} RESULT=SKIP — 빌드 결과가 {summary.result}라 산출물을 만지지 않는다.");
                return;
            }

            string appPath = summary.outputPath;
            var log = new StringBuilder();
            log.Append("StickMate — macOS 자동 그래픽 전환 선언 후처리 영수증\n")
               .Append("시각: ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append('\n')
               .Append("대상: ").Append(appPath).Append('\n')
               .Append("키: ").Append(HybridGpuPreferencePolicy.MacAutomaticGraphicsSwitchingKey)
               .Append(" = <").Append(HybridGpuPreferencePolicy.MacDesiredAutomaticGraphicsSwitching
                   ? HybridGpuPreferencePolicy.MacTrueElementName
                   : HybridGpuPreferencePolicy.MacFalseElementName).Append("/>\n");

            try
            {
                Apply(appPath, log);
            }
            catch (BuildFailedException)
            {
                throw;   // Fail()이 이미 로그와 영수증을 남겼다.
            }
            catch (Exception e)
            {
                Fail(appPath, log, $"예상치 못한 예외 — {e.GetType().Name}: {e.Message}");
            }
        }

        // ====================================================================
        // 본체
        // ====================================================================

        private void Apply(string appPath, StringBuilder log)
        {
            if (string.IsNullOrEmpty(appPath))
            {
                Fail(appPath, log, "산출물 경로가 비어 있다.");
                return;
            }

            if (!appPath.EndsWith(".app", StringComparison.Ordinal))
            {
                Fail(appPath, log,
                    $"산출물이 .app 번들이 아니다 — '{appPath}'. " +
                    "Xcode 프로젝트로 내보내는 빌드 모드는 이 훅이 다루지 않는다. " +
                    "조용히 넘어가지 않는 이유: 그러면 키 없는 앱이 '통과'한 것처럼 출하되고, " +
                    "실패한 빌드와 성공한 빌드가 산출물에서 구분되지 않는다.");
                return;
            }

            if (!Directory.Exists(appPath))
            {
                Fail(appPath, log, $"산출물 번들이 없다 — '{appPath}'.");
                return;
            }

            string plistPath = Path.Combine(appPath, ToLocalPath(InfoPlistRelativePath));
            if (!File.Exists(plistPath))
            {
                Fail(appPath, log, $"번들 안에 {InfoPlistRelativePath}가 없다 — '{plistPath}'. " +
                     "번들 구조가 바뀌었거나 빌드가 완결되지 않았다.");
                return;
            }

            // ★ 텍스트가 아니라 **바이트로** 읽는다. 그래야 (a) BOM 유무를 그대로 되돌려줄 수 있고,
            //   (b) 마지막에 "달라진 바이트가 삽입 한 덩어리뿐인가"를 Windows 훅과 같은 등급으로
            //   대조할 수 있다. ReadAllText는 BOM을 조용히 먹어치워, 없던 차이를 만든다.
            byte[] originalBytes = File.ReadAllBytes(plistPath);
            bool hasBom = originalBytes.Length >= 3 &&
                          originalBytes[0] == 0xEF && originalBytes[1] == 0xBB && originalBytes[2] == 0xBF;
            int bomLength = hasBom ? 3 : 0;
            string original = new UTF8Encoding(false)
                .GetString(originalBytes, bomLength, originalBytes.Length - bomLength);

            log.Append("plist: ").Append(plistPath)
               .Append(" (").Append(originalBytes.Length).Append(" bytes, BOM ")
               .Append(hasBom ? "있음" : "없음").Append(")\n");

            if (!PropertyListRootReader.TryReadRootEntries(original, out List<PlistRootEntry> before, out string parseError))
            {
                Fail(appPath, log, "Info.plist 파싱 실패 — " + parseError);
                return;
            }

            log.Append("루트 항목 ").Append(before.Count).Append("개: ")
               .Append(PropertyListRootReader.Describe(before)).Append('\n');

            // ---- 1단계: 사실 조회 → 중립 판정 ----
            bool present = PropertyListRootReader.TryFind(before,
                HybridGpuPreferencePolicy.MacAutomaticGraphicsSwitchingKey, out PlistRootEntry existing);

            HybridGpuPreferencePolicy.PlistVerdict verdict = HybridGpuPreferencePolicy.ClassifyMacPlistEntry(
                present, present ? existing.ValueElementName : null, out string reason);

            log.Append("판정: ").Append(verdict).Append(" — ").Append(reason).Append('\n');

            if (verdict == HybridGpuPreferencePolicy.PlistVerdict.Unexpected)
            {
                Fail(appPath, log, reason);
                return;
            }

            if (verdict == HybridGpuPreferencePolicy.PlistVerdict.AlreadyDeclared)
            {
                log.Append("쓰기 없음 — 이미 선언돼 있다(멱등 통과). 서명도 건드리지 않는다.\n")
                   .Append("RESULT=PASS\n");
                WriteReceipt(appPath, log);
                Debug.Log($"{LogTag} RESULT=PASS — {reason} " +
                          $"(루트 항목 {before.Count}개 그대로, 서명 미변경). 영수증: {ReceiptFileName}");
                return;
            }

            // ---- 2단계: 순수 함수로 새 텍스트를 만든다(아직 디스크에 안 쓴다) ----
            if (!PropertyListRootReader.TryInsertBooleanEntry(original,
                    HybridGpuPreferencePolicy.MacAutomaticGraphicsSwitchingKey,
                    HybridGpuPreferencePolicy.MacDesiredAutomaticGraphicsSwitching,
                    out string patched, out string insertError))
            {
                Fail(appPath, log, "삽입 텍스트를 만들지 못했다(디스크는 건드리지 않았다) — " + insertError);
                return;
            }

            // ---- 3단계: 서명 상태를 먼저 확인한다. 되돌릴 수 없게 되기 전에. ----
            bool wasSigned = Directory.Exists(Path.Combine(appPath, ToLocalPath(CodeSignatureRelativePath)));
            bool codesignAvailable = File.Exists(CodesignToolPath);
            log.Append("서명 흔적(").Append(CodeSignatureRelativePath).Append("): ")
               .Append(wasSigned ? "있음" : "없음")
               .Append(" / codesign 도구: ").Append(codesignAvailable ? "있음" : "없음").Append('\n');

            if (wasSigned)
            {
                if (!codesignAvailable)
                {
                    Fail(appPath, log,
                        $"번들이 서명돼 있는데 이 호스트에 {CodesignToolPath}가 없다. " +
                        "plist를 고치면 서명이 깨지고(실측 확인) Apple Silicon에서는 앱이 아예 실행되지 " +
                        "않는데, 재서명할 수단이 없다. 그래서 한 글자도 쓰지 않는다.");
                    return;
                }

                if (!TryDescribeSignature(appPath, out string signatureDescription, out string describeError))
                {
                    Fail(appPath, log, "서명을 읽지 못했다 — " + describeError +
                         " (재서명이 필요한데 현재 서명 종류를 모르는 상태에서는 쓰지 않는다.)");
                    return;
                }

                string oneLine = signatureDescription.Replace("\n", " | ");
                log.Append("현재 서명: ").Append(oneLine).Append('\n');

                if (signatureDescription.IndexOf(AdhocSignatureMarker, StringComparison.Ordinal) < 0)
                {
                    Fail(appPath, log,
                        $"번들이 애드혹({AdhocSignatureMarker})이 아닌 서명을 갖고 있다. " +
                        "plist를 고치면 그 서명이 깨지는데, 우리가 애드혹으로 재서명하면 " +
                        "서명 등급을 몰래 낮추는 것이 된다. 아무것도 쓰지 않는다.\n" +
                        "  선택지: (1) 서명 전 단계에서 이 키를 넣도록 파이프라인을 바꾸거나, " +
                        "(2) 이 훅 뒤에 같은 인증서로 재서명하는 단계를 붙인다.\n" +
                        "  현재 서명: " + oneLine);
                    return;
                }
            }

            // ---- 4단계: 쓴다 (BOM이 있었으면 그대로 되돌려준다) ----
            byte[] body = new UTF8Encoding(false).GetBytes(patched);
            var patchedBytes = new byte[bomLength + body.Length];
            Array.Copy(originalBytes, 0, patchedBytes, 0, bomLength);
            Array.Copy(body, 0, patchedBytes, bomLength, body.Length);
            File.WriteAllBytes(plistPath, patchedBytes);

            log.Append("쓰기: 루트 dict 끝에 키 1개 삽입 (")
               .Append(originalBytes.Length).Append(" -> ").Append(patchedBytes.Length).Append(" bytes)\n");

            // ---- 5단계: 서명이 있었으면 즉시 재서명한다 ----
            if (wasSigned)
            {
                if (!TryRunCodesign(new[] { "--force", "--sign", "-", appPath }, out string signOut, out string signError))
                {
                    Fail(appPath, log, "재서명 실패 — " + signError +
                         "\n  ★ plist는 이미 고쳐졌고 서명은 깨진 상태다. 이 번들을 실행하지 마라 — " +
                         "다시 빌드하라.");
                    return;
                }
                log.Append("재서명: codesign --force --sign - (최상위 번들만)\n")
                   .Append(Indent(signOut));

                if (!TryRunCodesign(new[] { "--verify", "--deep", "--strict", appPath }, out string verifyOut, out string verifyError))
                {
                    Fail(appPath, log, "재서명 후 검증 실패 — " + verifyError +
                         "\n  ★ 이 번들을 배포하지 마라. 다시 빌드하라.");
                    return;
                }
                log.Append("서명 검증: 통과\n").Append(Indent(verifyOut));
            }

            // ---- 6단계: 디스크에서 다시 읽어 처음부터 다시 파싱한다 ----
            byte[] afterBytes = File.ReadAllBytes(plistPath);
            string after = new UTF8Encoding(false)
                .GetString(afterBytes, bomLength, afterBytes.Length - bomLength);

            // ★ Windows 훅과 같은 등급의 대조 — 달라진 바이트가 **연속된 한 덩어리의 삽입**인가.
            //   앞뒤가 원본과 완전히 같지 않으면, 파싱이 통과하더라도 우리가 무엇을 덮어썼는지
            //   설명할 수 없다. 설명할 수 없는 변경은 통과시키지 않는다.
            if (!TryDescribeContiguousInsertion(originalBytes, afterBytes, out int insertedBytes, out string diffError))
            {
                Fail(appPath, log, "쓰기 후 바이트 대조 실패 — " + diffError +
                     " (plist가 손상됐다. 다시 빌드하라.)");
                return;
            }
            log.Append("바이트 대조: 삽입 ").Append(insertedBytes)
               .Append("바이트 한 덩어리, 그 밖의 변경 0바이트\n");

            if (!PropertyListRootReader.TryReadRootEntries(after, out List<PlistRootEntry> verify, out string verifyParseError))
            {
                Fail(appPath, log, "쓰기 후 재파싱 실패 — " + verifyParseError +
                     " (plist가 손상됐다. 다시 빌드하라.)");
                return;
            }

            if (verify.Count != before.Count + 1)
            {
                Fail(appPath, log, $"쓰기 후 루트 항목이 {before.Count} -> {verify.Count}개다. " +
                     "정확히 1개만 늘어야 한다.");
                return;
            }

            for (int i = 0; i < before.Count; i++)
            {
                if (string.Equals(before[i].Key, verify[i].Key, StringComparison.Ordinal) &&
                    string.Equals(before[i].ValueElementName, verify[i].ValueElementName, StringComparison.Ordinal) &&
                    string.Equals(before[i].ValueText, verify[i].ValueText, StringComparison.Ordinal))
                {
                    continue;
                }
                Fail(appPath, log, $"기존 항목 {i}번이 바뀌었다({before[i]} -> {verify[i]}) — " +
                     "이 후처리는 키 하나를 더할 뿐 아무것도 고치지 않는다. 다시 빌드하라.");
                return;
            }

            if (!PropertyListRootReader.TryFind(verify,
                    HybridGpuPreferencePolicy.MacAutomaticGraphicsSwitchingKey, out PlistRootEntry added))
            {
                Fail(appPath, log, "쓰기 후 그 키를 다시 찾지 못했다.");
                return;
            }

            HybridGpuPreferencePolicy.PlistVerdict finalVerdict = HybridGpuPreferencePolicy.ClassifyMacPlistEntry(
                true, added.ValueElementName, out string finalReason);
            if (finalVerdict != HybridGpuPreferencePolicy.PlistVerdict.AlreadyDeclared)
            {
                Fail(appPath, log, "되읽기 판정이 '이미 선언됨'이 아니다 — " + finalReason);
                return;
            }

            log.Append("되읽기 검증 통과: ").Append(added).Append('\n')
               .Append("RESULT=PASS\n");
            WriteReceipt(appPath, log);

            Debug.Log($"{LogTag} RESULT=PASS — {added} 삽입 완료 " +
                      $"(루트 항목 {before.Count} -> {verify.Count}, 기존 항목 전부 동일" +
                      (wasSigned ? ", 애드혹 재서명 + 검증 통과" : ", 서명 없음") + $"). 영수증: {ReceiptFileName}. " +
                      "★ 실기 미확인: 이 키가 실제로 내장 GPU 선택으로 귀결되는지는 듀얼 GPU Mac에서만 " +
                      "확인된다(이 개발 머신은 Apple Silicon이라 내장/외장 개념이 없다) — " +
                      "python3 Tools/BuildVerify/check_mac_gpu_switching.py <app>");
        }

        // ====================================================================
        // codesign 호출 — 사실 조회와 재서명
        // ====================================================================

        /// <summary>현재 서명을 사람이 읽을 형태로 가져온다(<c>codesign -dv</c>는 stderr로 쓴다).</summary>
        private static bool TryDescribeSignature(string appPath, out string description, out string error)
        {
            description = null;
            if (!TryRunTool(CodesignToolPath, new[] { "-dv", "--verbose=2", appPath },
                    out string stdout, out string stderr, out int exitCode, out error))
            {
                return false;
            }
            if (exitCode != 0)
            {
                error = $"codesign -dv가 종료코드 {exitCode}로 끝났다: {Trim(stderr)}";
                return false;
            }
            description = Trim(stdout + "\n" + stderr);
            if (description.Length == 0)
            {
                error = "codesign -dv가 아무것도 출력하지 않았다 — 서명 종류를 판정할 수 없다.";
                return false;
            }
            return true;
        }

        private static bool TryRunCodesign(string[] arguments, out string output, out string error)
        {
            output = null;
            if (!TryRunTool(CodesignToolPath, arguments,
                    out string stdout, out string stderr, out int exitCode, out error))
            {
                return false;
            }
            output = Trim(stdout + "\n" + stderr);
            if (exitCode != 0)
            {
                error = $"codesign {string.Join(" ", arguments)} 가 종료코드 {exitCode}로 끝났다: {Trim(stderr)}";
                return false;
            }
            return true;
        }

        /// <summary>외부 도구 하나를 돌린다. <b>실패는 전부 사유와 함께 돌려준다</b>(조용한 실패 금지).</summary>
        private static bool TryRunTool(string toolPath, string[] arguments,
            out string stdout, out string stderr, out int exitCode, out string error)
        {
            stdout = string.Empty;
            stderr = string.Empty;
            exitCode = -1;
            error = null;

            foreach (string argument in arguments)
            {
                if (argument != null && argument.IndexOf('"') >= 0)
                {
                    error = $"인자에 따옴표가 있어 안전하게 넘길 수 없다: {argument}";
                    return false;
                }
            }

            var quoted = new StringBuilder();
            for (int i = 0; i < arguments.Length; i++)
            {
                if (i > 0) quoted.Append(' ');
                quoted.Append('"').Append(arguments[i]).Append('"');
            }

            try
            {
                var info = new ProcessStartInfo(toolPath, quoted.ToString())
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                using (var process = new Process { StartInfo = info })
                {
                    process.Start();

                    // ★ 두 파이프를 **동시에** 비운다. 순서대로 ReadToEnd 하면, 자식이 우리가 아직
                    //   읽지 않는 쪽 파이프 버퍼를 채웠을 때 서로를 기다리며 멈춘다(교착).
                    //   codesign의 출력은 짧지만, 실패 시 --verbose 출력이 길어지는 경로가 있다 —
                    //   "평소엔 안 걸리고 실패할 때만 멈추는" 종류의 결함은 최악이다.
                    System.Threading.Tasks.Task<string> outTask = process.StandardOutput.ReadToEndAsync();
                    System.Threading.Tasks.Task<string> errTask = process.StandardError.ReadToEndAsync();

                    if (!process.WaitForExit(ToolTimeoutMilliseconds))
                    {
                        error = $"{toolPath}가 {ToolTimeoutMilliseconds}ms 안에 끝나지 않았다.";
                        return false;
                    }

                    stdout = outTask.Result ?? string.Empty;
                    stderr = errTask.Result ?? string.Empty;
                    exitCode = process.ExitCode;
                }
            }
            catch (Exception e)
            {
                error = $"{toolPath} 실행 실패 — {e.GetType().Name}: {e.Message}";
                return false;
            }

            return true;
        }

        private static string Trim(string text) => (text ?? string.Empty).Trim();

        /// <summary>
        /// <paramref name="after"/>가 <paramref name="original"/>에 <b>연속된 한 덩어리를 끼워 넣은 것</b>인지
        /// 바이트 단위로 확인한다. 앞에서부터 같은 만큼과 뒤에서부터 같은 만큼을 합쳐 원본 길이가
        /// 나오면 그 사이가 순수한 삽입이고, 그 밖에 바뀐 바이트는 하나도 없다.
        /// </summary>
        private static bool TryDescribeContiguousInsertion(byte[] original, byte[] after,
            out int insertedBytes, out string error)
        {
            insertedBytes = 0;

            if (after.Length <= original.Length)
            {
                error = $"파일이 늘지 않았다({original.Length} -> {after.Length} bytes). 삽입이라면 반드시 늘어야 한다.";
                return false;
            }

            int head = 0;
            while (head < original.Length && original[head] == after[head]) head++;

            int tail = 0;
            while (tail < original.Length - head &&
                   original[original.Length - 1 - tail] == after[after.Length - 1 - tail])
            {
                tail++;
            }

            if (head + tail != original.Length)
            {
                error = $"원본이 '앞 {head}바이트 + 뒤 {tail}바이트'로 남지 않았다(원본 {original.Length}바이트). " +
                        "삽입 한 덩어리가 아니라 여러 곳이 바뀌었다는 뜻이다.";
                return false;
            }

            insertedBytes = after.Length - original.Length;
            error = null;
            return true;
        }

        /// <summary>번들 내부 상대 경로(<c>/</c> 구분)를 이 호스트의 구분자로 옮긴다.
        /// 상대 경로 상수를 한 곳에만 두기 위한 변환이다 — 같은 경로를 두 번 적으면 언젠가 갈라진다.</summary>
        private static string ToLocalPath(string relativePath) =>
            relativePath.Replace('/', Path.DirectorySeparatorChar);

        private static string Indent(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var sb = new StringBuilder();
            foreach (string line in text.Split('\n'))
            {
                if (line.Trim().Length == 0) continue;
                sb.Append("  ").Append(line.TrimEnd()).Append('\n');
            }
            return sb.ToString();
        }

        // ====================================================================
        // 실패 — 조용히 넘어가지 않는다
        // ====================================================================

        private void Fail(string appPath, StringBuilder log, string reason)
        {
            log.Append("RESULT=FAIL\n사유: ").Append(reason).Append('\n');
            WriteReceipt(appPath, log);

            string message =
                $"{LogTag} RESULT=FAIL — {reason}\n" +
                "이 실패는 의도된 것이다. 선언이 빠진 .app이 조용히 출하되는 것보다 빌드가 서는 편이 낫다.\n" +
                "확인 순서: (1) 산출물 옆 " + ReceiptFileName + " 를 읽는다 " +
                "(2) python3 Tools/BuildVerify/check_mac_gpu_switching.py <app> 로 현재 상태를 독립 확인한다 " +
                "(3) 템플릿이 바뀐 것이라면 Platform/HybridGpuPreferencePolicy.cs 의 기대값을 다시 판정한다.";

            Debug.LogError(message);
            throw new BuildFailedException(message);
        }

        /// <summary>
        /// 영수증은 <b>번들 밖</b>(.app 옆)에 쓴다. 번들 <b>안</b>에 쓰면 그 파일 자체가 봉인 대상이 되어
        /// 서명이 다시 깨진다 — Windows 훅과 다르게 이 훅에서만 조심해야 하는 지점이다.
        /// </summary>
        private static void WriteReceipt(string appPath, StringBuilder log)
        {
            try
            {
                string dir = string.IsNullOrEmpty(appPath) ? null : Path.GetDirectoryName(appPath);
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
