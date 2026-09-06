using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ============================================================================
    /// 인용 감사 #7 — <b>「소스에서 뽑아 서로 다르고 문서 값과 같은지를 대신 잠근다」던 그 기계</b>
    /// (test-engineer, 2026-09-06)
    /// ============================================================================
    /// <c>Platform/Windows/WindowsSystemAudioActivityProbe.cs</c>의 COM 식별자 블록 주석 원문:
    ///
    /// <para><i>"COM 식별자. 1차 출처는 Microsoft Learn / mmdeviceapi.h · endpointvolume.h 다.
    /// 한 글자만 틀려도 개체 생성이나 QueryInterface가 <b>조용히</b> 실패한다.
    /// <b>이 머신에서는 실행으로 확인할 수 없으므로</b> <c>SystemAudioActivityProbeContractTests</c>가
    /// 소스에서 뽑아 «서로 다르고 문서 값과 같은지»를 대신 잠근다."</i></para>
    ///
    /// <b>그 테스트는 2026-09-06까지 존재하지 않았다.</b> 그리고 이 건은 특히 나빴다 —
    /// 같은 주석이 <b>「실행으로 확인 불가」</b>라고 적어 이 테스트를 <b>유일한 검증 수단</b>으로
    /// 세워 뒀기 때문이다. 즉 «다른 방법으로 재면 된다»는 도피처가 문장 안에서 이미 닫혀 있었다.
    ///
    /// ============================================================================
    /// ★ 기대값을 <b>우리 소스에서 만들지 않는다</b> (TEAM.md 열 번째 거짓 통과 형태)
    /// ============================================================================
    /// <i>"생성기와 검사기가 코드를 공유하면 둘 다 같은 방향으로 틀린다."</i>
    /// GUID를 <b>우리 상수에서 읽어 우리 상수와 비교</b>하면 그 테스트는 «값이 무엇이든» 초록이다.
    /// 오타를 잡겠다는 목적에 정확히 반대다.
    ///
    /// <para>그래서 <see cref="WindowsComGolden"/>은 <b>Windows SDK 헤더의 공표값</b>을 옮겨 적은
    /// <b>외부 골든</b>이다. CLAUDE.md의 «테스트에 프로덕션 상수를 베끼지 마라»와 충돌하지 않는다 —
    /// 그 규칙이 막는 것은 <b>우리가 정한 값</b>(상한·스키마 버전 등, 정당하게 움직인다)을 두 곳에
    /// 두는 것이고, 여기 값은 <b>우리가 정하지 않았고 앞으로도 절대 바뀌지 않는 외부 ABI</b>다.
    /// 이 경우엔 오히려 <b>베끼지 않으면 아무것도 잴 수 없다.</b></para>
    ///
    /// <para>★ <b>골든의 출처를 정직하게 적는다</b>: 2026-09-06에 네 값 모두 저장소 <b>바깥</b>에서
    /// 재확인했다(Microsoft Learn의 MMDevice API 문서, ReactOS <c>mmdeviceapi.idl</c>,
    /// mingw <c>endpointvolume.h</c>, NAudio/coreaudio-dotnet 등 서로 독립인 공개 구현들).
    /// 의심되면 <b>우리 소스가 아니라 그 헤더들과</b> 대조해라 — 우리 소스와 대조하는 것은
    /// 이 테스트가 하려는 일 그 자체이므로 순환이다.</para>
    ///
    /// ============================================================================
    /// ★ 왜 리플렉션이 아니라 소스 텍스트인가
    /// ============================================================================
    /// 그 파일 전체가 <c>#if UNITY_STANDALONE_WIN</c> 안이다. <b>macOS 타깃에서는 타입이 없다.</b>
    /// 리플렉션 감사는 없는 타입을 셀 수 없고 그 0건은 «깨끗함»과 구분되지 않는다
    /// (CLAUDE.md 활성 빌드 타깃 규칙). 이 감사는 <b>어느 타깃에서 돌려도 같은 것</b>을 잰다.
    ///
    /// ============================================================================
    /// 무엇을 재고 무엇을 <b>안</b> 재는가
    /// ============================================================================
    /// <list type="bullet">
    ///  <item><b>잰다</b>: 소스에 적힌 COM GUID가 (ㄱ) 서로 다른가 (ㄴ) 헤더 공표값과 같은가
    ///    (ㄷ) <b>선언 자리에만</b> 있는가(같은 값이 두 곳에 있으면 갈라진다).</item>
    ///  <item><b>안 잰다</b>: 실제로 <c>CoCreateInstance</c>가 성공하는가. 그건 Windows 실기에서만
    ///    알 수 있고, 그래서 원 주석이 «이 머신에서는 실행으로 확인할 수 없다»고 적은 것이다.
    ///    <b>못 재는 것을 재는 척하지 않는다.</b></item>
    /// </list>
    /// </summary>
    public sealed class SystemAudioActivityProbeContractTests
    {
        private const string LogPrefix = "[오디오COM계약감사]";

        /// <summary>존재 단언용 니들 — 이름이 바뀌면 <b>시끄럽게</b> 빨개진다.</summary>
        private const string ProbeTypeName = "WindowsSystemAudioActivityProbe";

        /// <summary>
        /// ★ <b>외부 골든.</b> 키는 <b>Windows SDK가 정한 심볼 이름</b>이지 우리 상수 이름이 아니다 —
        /// 그래야 우리가 <c>DeviceEnumeratorClsid</c>를 뭐라고 부르든 이 표가 낡지 않는다.
        /// <para>값은 <c>mmdeviceapi.h</c> / <c>endpointvolume.h</c>의 공표값이다.</para>
        /// </summary>
        private static readonly (string Symbol, string Guid, string Header, string Why)[] WindowsComGolden =
        {
            ("CLSID_MMDeviceEnumerator", "BCDE0395-E52F-467C-8E3D-C4579291692E", "mmdeviceapi.h",
                "장치 열거자 개체의 CLSID. 틀리면 Type.GetTypeFromCLSID가 null을 주고 " +
                "이 앱은 «오디오 감지 사용 불가»로 조용히 떨어진다."),
            ("IID_IMMDeviceEnumerator", "A95664D2-9614-4F35-A746-DE8DB63617E6", "mmdeviceapi.h",
                "열거자 인터페이스 IID. [Guid] 특성으로 마샬러가 vtable을 잡는 근거다."),
            ("IID_IMMDevice", "D666063F-1587-4E43-81F1-B948E807363F", "mmdeviceapi.h",
                "엔드포인트 장치 인터페이스 IID."),
            ("IID_IAudioMeterInformation", "C02216F6-8C67-4B5B-9D00-D008E73E0064", "endpointvolume.h",
                "피크 미터 인터페이스 IID. ★ 미터 «정보»다 — 스트림이 아니다. " +
                "이걸 스트림 쪽 IID로 잘못 적으면 Activate가 실패하고 «항상 무음»이 된다."),
        };

        /// <summary>SDK 심볼의 접두. COM 식별자는 이 둘 중 하나로 시작한다.</summary>
        private static readonly string[] SdkSymbolPrefixes = { "CLSID_", "IID_" };

        /// <summary>상수 선언 위로 몇 줄까지 거슬러 올라가 SDK 심볼을 찾을 것인가.
        /// 이 파일의 관례는 <b>바로 윗줄 한 줄짜리 <c>&lt;summary&gt;</c></b>지만,
        /// 두 줄로 늘어나도 따라가도록 여유를 둔다.</summary>
        private const int SymbolLookBackLines = 6;

        // ====================================================================
        // 스캐너 — 순수 함수. 네거티브 컨트롤이 <b>같은 함수</b>에 가짜 소스를 흘린다.
        // ====================================================================

        internal readonly struct GuidConst
        {
            public readonly string ConstName;
            public readonly string Value;
            public readonly string SdkSymbol;   // 못 찾으면 null
            public readonly int Line;

            public GuidConst(string constName, string value, string sdkSymbol, int line)
            {
                ConstName = constName;
                Value = value;
                SdkSymbol = sdkSymbol;
                Line = line;
            }

            public override string ToString() => $"{SdkSymbol ?? "(심볼 미상)"} = {Value} ({ConstName}, :{Line})";
        }

        private static bool IsIdentifierChar(char c)
            => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';

        /// <summary>
        /// <c>"XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX"</c> 꼴인가. <c>Guid.TryParseExact</c>의 <c>"D"</c>는
        /// 하이픈 5조각 정규형만 받는다 — 중괄호판(<c>"B"</c>)이나 하이픈 없는 판(<c>"N"</c>)은 <b>거절</b>한다.
        /// <para>그게 의도다: 이 파일의 관례는 하이픈 정규형이고, 표기가 섞이면
        /// <c>new Guid(...)</c>는 통과하지만 사람 눈의 대조가 어려워진다.</para>
        /// </summary>
        internal static bool LooksLikeGuid(string literal)
            => !string.IsNullOrEmpty(literal) && Guid.TryParseExact(literal, "D", out _);

        /// <summary>
        /// 그 줄에서 <b>코드 부분만</b> 남긴다(주석은 버린다).
        /// <para>★ 왜 필요한가: <see cref="ExtractGuidConsts"/>는 라벨을 읽어야 해서 <b>원본</b> 줄을
        /// 훑는다. 그러면 <c>// const string OldIid = "…";</c>처럼 주석에 남겨 둔 옛 값이 선언으로
        /// 세어지고, 그 유령이 골든 대조에 끌려 들어온다.</para>
        /// </summary>
        internal static string CodeOnly(string line)
        {
            if (line == null) return null;
            if (line.TrimStart().StartsWith("*", StringComparison.Ordinal)) return string.Empty;
            int at = line.IndexOf("//", StringComparison.Ordinal);
            return at >= 0 ? line.Substring(0, at) : line;
        }

        /// <summary>한 줄에서 <c>const string 이름 = "값";</c>을 뽑는다. 아니면 <c>null</c>.</summary>
        internal static (string Name, string Value)? ParseConstString(string rawLine)
        {
            string line = CodeOnly(rawLine);
            if (string.IsNullOrEmpty(line)) return null;
            const string marker = "const string ";
            int at = line.IndexOf(marker, StringComparison.Ordinal);
            if (at < 0) return null;

            int p = at + marker.Length;
            while (p < line.Length && (line[p] == ' ' || line[p] == '\t')) p++;
            int start = p;
            while (p < line.Length && IsIdentifierChar(line[p])) p++;
            if (p == start) return null;
            string name = line.Substring(start, p - start);

            while (p < line.Length && (line[p] == ' ' || line[p] == '\t')) p++;
            if (p >= line.Length || line[p] != '=') return null;
            p++;
            while (p < line.Length && (line[p] == ' ' || line[p] == '\t')) p++;
            if (p >= line.Length || line[p] != '"') return null;
            p++;
            int valueStart = p;
            while (p < line.Length && line[p] != '"') p++;
            if (p >= line.Length) return null;

            return (name, line.Substring(valueStart, p - valueStart));
        }

        /// <summary>한 줄에서 <c>CLSID_*</c> / <c>IID_*</c> 심볼을 뽑는다. 없으면 <c>null</c>.</summary>
        internal static string ParseSdkSymbol(string line)
        {
            if (line == null) return null;
            foreach (string prefix in SdkSymbolPrefixes)
            {
                int at = line.IndexOf(prefix, StringComparison.Ordinal);
                while (at >= 0)
                {
                    bool leftOk = at == 0 || !IsIdentifierChar(line[at - 1]);
                    if (leftOk)
                    {
                        int p = at;
                        while (p < line.Length && IsIdentifierChar(line[p])) p++;
                        if (p - at > prefix.Length) return line.Substring(at, p - at);
                    }
                    at = line.IndexOf(prefix, at + 1, StringComparison.Ordinal);
                }
            }
            return null;
        }

        /// <summary>
        /// 소스 <b>원본</b>(주석 포함)에서 GUID 상수를 전부 뽑고, 각각 <b>바로 위 주석</b>의
        /// SDK 심볼과 짝지어 준다.
        /// <para>★ 주석이 필요하므로 여기서는 <b>주석을 걷어내지 않는다</b>. 이 파일에서 주석은
        /// «장식»이 아니라 <b>무엇과 대조해야 하는가를 알려 주는 라벨</b>이다.</para>
        /// </summary>
        internal static List<GuidConst> ExtractGuidConsts(string rawSource)
        {
            var found = new List<GuidConst>();
            if (string.IsNullOrEmpty(rawSource)) return found;

            string[] lines = rawSource.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                (string Name, string Value)? parsed = ParseConstString(lines[i]);
                if (parsed == null) continue;
                if (!LooksLikeGuid(parsed.Value.Value)) continue;   // LogPrefix 같은 평범한 const는 여기서 빠진다

                string symbol = null;
                for (int j = i - 1; j >= 0 && j >= i - SymbolLookBackLines; j--)
                {
                    symbol = ParseSdkSymbol(lines[j]);
                    if (symbol != null) break;
                }
                found.Add(new GuidConst(parsed.Value.Name, parsed.Value.Value, symbol, i + 1));
            }
            return found;
        }

        /// <summary>소스 전체에서 <b>GUID 꼴 문자열 리터럴</b>이 몇 번 나오는가(선언·인라인 무관).</summary>
        internal static List<string> AllGuidStringLiterals(string source)
        {
            var found = new List<string>();
            if (string.IsNullOrEmpty(source)) return found;

            int i = 0;
            while (i < source.Length)
            {
                if (source[i] != '"') { i++; continue; }
                int start = ++i;
                while (i < source.Length && source[i] != '"') i++;
                if (i >= source.Length) break;
                string literal = source.Substring(start, i - start);
                if (LooksLikeGuid(literal)) found.Add(literal);
                i++;
            }
            return found;
        }

        // ====================================================================
        // 대상 파일 — 경로가 아니라 타입 선언으로 찾는다
        // ====================================================================

        private static (string Path, string Raw, string Stripped) FindProbeSource()
        {
            string[] all = EntitlementAuditSource.ProductionSourceFiles();
            Assert.GreaterOrEqual(all.Length, EntitlementAuditSource.MinProductionFileCount,
                $"{LogPrefix} 프로덕션 .cs를 {all.Length}개밖에 읽지 못했습니다 — " +
                "이 상태의 '위반 0건'은 측정이 아닙니다.");

            var matches = new List<(string, string, string)>();
            foreach (string path in all)
            {
                string raw = File.ReadAllText(path);
                string stripped = EntitlementAuditSource.StripComments(raw);
                if (!EntitlementAuditSource.DeclaresType(stripped, ProbeTypeName)) continue;
                matches.Add((path, raw, stripped));
            }

            Assert.AreEqual(1, matches.Count,
                $"{LogPrefix} '{ProbeTypeName}'을 선언하는 파일이 {matches.Count}개입니다(기대 1). " +
                "이름이 바뀌었거나 파일이 사라졌습니다 — 그대로 두면 아래 모든 대조가 " +
                "'대상이 없어서 위반 0건'이 되어 조용히 초록이 됩니다.");

            return matches[0];
        }

        private static string Rel(string path)
        {
            string root = Directory.GetParent(Application.dataPath)!.FullName;
            return path.Length > root.Length ? path.Substring(root.Length + 1) : path;
        }

        // ====================================================================
        // 1. ★ 본론 — 「문서 값과 같은가」 (양방향 집합 대조)
        // ====================================================================

        [Test]
        public void COM_식별자가_SDK_헤더_공표값과_같다()
        {
            (string Path, string Raw, string Stripped) target = FindProbeSource();
            List<GuidConst> consts = ExtractGuidConsts(target.Raw);

            // ── 존재 쪽 먼저: 하나도 못 뽑았으면 아래 «불일치 0건»은 «없다»가 아니라 «못 봤다»다.
            Assert.GreaterOrEqual(consts.Count, WindowsComGolden.Length,
                $"{LogPrefix} {Rel(target.Path)}에서 GUID 상수를 {consts.Count}개만 뽑았습니다" +
                $"(골든 {WindowsComGolden.Length}개). 상수 표기가 바뀌었거나(예: " +
                "'const string' -> 'static readonly Guid') 파서가 죽었습니다. " +
                "이 상태에서 아래 대조는 아무것도 재지 않습니다.");

            var bySymbol = new Dictionary<string, GuidConst>(StringComparer.Ordinal);
            var unlabeled = new List<string>();
            foreach (GuidConst c in consts)
            {
                if (c.SdkSymbol == null) { unlabeled.Add($"{c.ConstName}(:{c.Line}) = {c.Value}"); continue; }
                if (bySymbol.TryGetValue(c.SdkSymbol, out GuidConst earlier))
                {
                    Assert.Fail($"{LogPrefix} SDK 심볼 '{c.SdkSymbol}'이 두 상수에 붙어 있습니다 " +
                                $"({earlier.ConstName}:{earlier.Line} · {c.ConstName}:{c.Line}). " +
                                "라벨이 중복되면 어느 쪽을 골든과 대조해야 하는지 알 수 없고, " +
                                "뒤의 것이 앞의 것을 덮어써 <b>앞의 값은 아무도 검사하지 않게</b> 됩니다.");
                }
                bySymbol[c.SdkSymbol] = c;
            }

            Assert.IsEmpty(unlabeled,
                $"{LogPrefix} 어떤 SDK 심볼도 붙어 있지 않은 GUID 상수가 있습니다:\n  " +
                string.Join("\n  ", unlabeled) + "\n" +
                "이 파일의 관례는 상수 <b>바로 위 한 줄 주석</b>에 헤더의 심볼 이름을 적는 것입니다" +
                "(<c>CLSID_MMDeviceEnumerator</c> 등). 그 라벨이 없으면 <b>무엇과 대조해야 하는지</b>를 " +
                "사람도 기계도 알 수 없고, 그 순간 이 감사는 그 상수를 통째로 건너뜁니다.");

            var mismatched = new List<string>();
            var missing = new List<string>();
            foreach ((string symbol, string golden, string header, string why) in WindowsComGolden)
            {
                if (!bySymbol.TryGetValue(symbol, out GuidConst actual))
                {
                    missing.Add($"{symbol} ({header}) — 소스에 없음. {why}");
                    continue;
                }
                // Guid 값으로 비교한다 — 대소문자 표기 차이는 COM에서 의미가 없다.
                if (Guid.Parse(actual.Value) != Guid.Parse(golden))
                    mismatched.Add($"{symbol} ({header})\n      소스 {actual.Value} (:{actual.Line}, {actual.ConstName})" +
                                   $"\n      헤더 {golden}\n      {why}");
            }

            var unknown = new List<string>();
            foreach (KeyValuePair<string, GuidConst> pair in bySymbol)
            {
                bool known = false;
                foreach ((string symbol, string _, string __, string ___) in WindowsComGolden)
                    if (string.Equals(symbol, pair.Key, StringComparison.Ordinal)) { known = true; break; }
                if (!known) unknown.Add(pair.Value.ToString());
            }

            Debug.Log($"{LogPrefix} {Rel(target.Path)} — GUID 상수 {consts.Count}개 / 골든 대조 " +
                      $"{WindowsComGolden.Length}개\n  " + string.Join("\n  ", consts.ConvertAll(c => c.ToString())));

            Assert.IsEmpty(mismatched,
                $"{LogPrefix} COM 식별자가 SDK 헤더 공표값과 <b>다릅니다</b>:\n  " +
                string.Join("\n  ", mismatched) + "\n" +
                "한 글자만 틀려도 개체 생성이나 QueryInterface가 <b>조용히</b> 실패하고, 이 앱은 " +
                "«오디오 감지 사용 불가»로 떨어져 음악에 반응하는 춤이 영원히 안 나옵니다 " +
                "(그리고 그 실패는 예외가 아니라 HRESULT라 로그도 조용합니다). " +
                "값을 바꾸려면 헤더를 먼저 확인하세요 — 이 표가 틀렸다면 표를 고치되 <b>출처를 함께</b> 적으세요.");

            Assert.IsEmpty(missing,
                $"{LogPrefix} 골든에 있는 심볼이 소스에서 사라졌습니다:\n  " + string.Join("\n  ", missing) + "\n" +
                "구현이 정말 바뀌었다면(예: WASAPI를 버리고 다른 API로 이전) 이 골든 항목도 " +
                "지우세요. 남겨 두면 «대조했다»는 착시만 남습니다.");

            Assert.IsEmpty(unknown,
                $"{LogPrefix} 골든이 모르는 COM 식별자가 생겼습니다:\n  " + string.Join("\n  ", unknown) + "\n" +
                "새 COM 인터페이스를 쓰기 시작했다면 그 IID도 <b>헤더 값과 대조돼야</b> 합니다 — " +
                $"{nameof(WindowsComGolden)}에 심볼·값·헤더·사유를 한 줄 적으세요. 이 단언이 없으면 " +
                "새로 들어온 GUID는 <b>아무도 확인하지 않는 값</b>이 됩니다.");
        }

        // ====================================================================
        // 2. ★ 본론 — 「서로 다른가」
        // ====================================================================

        [Test]
        public void COM_식별자는_서로_다르다()
        {
            (string Path, string Raw, string Stripped) target = FindProbeSource();
            List<GuidConst> consts = ExtractGuidConsts(target.Raw);

            Assert.GreaterOrEqual(consts.Count, 2,
                $"{LogPrefix} GUID 상수를 {consts.Count}개만 뽑았습니다 — 2개 미만이면 " +
                "«서로 다르다»가 자동으로 참이라 아무것도 재지 못합니다.");

            var seen = new Dictionary<string, GuidConst>(StringComparer.OrdinalIgnoreCase);
            var duplicates = new List<string>();
            foreach (GuidConst c in consts)
            {
                string key = Guid.Parse(c.Value).ToString("D");
                if (seen.TryGetValue(key, out GuidConst first))
                    duplicates.Add($"{first.ConstName}(:{first.Line}) == {c.ConstName}(:{c.Line}) = {c.Value}");
                else
                    seen[key] = c;
            }

            Assert.IsEmpty(duplicates,
                $"{LogPrefix} 서로 달라야 할 COM 식별자가 같습니다:\n  " + string.Join("\n  ", duplicates) + "\n" +
                "복사·붙여넣기로 <b>한 줄만 고치는 것을 잊은</b> 가장 흔한 형태입니다. " +
                "CLSID와 IID가 같아지면 CoCreateInstance는 성공한 것처럼 보이고 QueryInterface에서 " +
                "E_NOINTERFACE가 나며, 이 앱은 그것을 «오디오 감지 사용 불가»로만 보고합니다.");
        }

        // ====================================================================
        // 3. ★ 사본이 두 개면 갈라진다 — GUID 리터럴은 선언 자리에만
        // ====================================================================

        /// <summary>
        /// GUID 문자열 리터럴 개수 == GUID 상수 선언 개수.
        /// <para>이 파일은 <c>[Guid(DeviceEnumeratorIid)]</c>처럼 <b>상수를 참조</b>해 특성을 붙인다.
        /// 누군가 <c>[Guid("A95664D2-…")]</c>로 <b>리터럴을 인라인</b>하면 같은 값이 두 곳이 되고,
        /// 그 순간 한쪽만 고치는 사고가 열린다 — 이 저장소가 «세 번째 사본»으로 반복해 당한 형태다
        /// (<see cref="ConfigFallbackLiteralDriftTests"/> 참조).</para>
        /// </summary>
        [Test]
        public void GUID_리터럴은_상수_선언_자리에만_있다()
        {
            (string Path, string Raw, string Stripped) target = FindProbeSource();

            List<string> literals = AllGuidStringLiterals(target.Stripped);
            List<GuidConst> consts = ExtractGuidConsts(target.Raw);

            Assert.IsNotEmpty(literals,
                $"{LogPrefix} GUID 리터럴을 하나도 못 찾았습니다 — 리터럴 스캐너가 죽었습니다.");

            Assert.AreEqual(consts.Count, literals.Count,
                $"{LogPrefix} GUID 리터럴 {literals.Count}개 vs 상수 선언 {consts.Count}개.\n" +
                "리터럴이 더 많으면 <b>선언 밖 어딘가에 사본</b>이 있습니다([Guid(\"…\")] 인라인 등). " +
                "사본이 둘이면 언젠가 한쪽만 고쳐지고, 그 드리프트는 <b>Windows 실기에서만</b> " +
                "드러납니다(이 머신에서는 실행으로 확인할 수 없다 — 원 주석 그대로입니다). " +
                "상수를 참조하세요: [Guid(DeviceEnumeratorIid)].");
        }

        // ====================================================================
        // 4. 양성 대조 — 「0건」이 능력을 증명한 뒤에만 값을 갖는다
        // ====================================================================

        [Test]
        public void 양성대조_스캐너가_실제_파일에서_라벨_붙은_GUID를_찾아낸다()
        {
            (string Path, string Raw, string Stripped) target = FindProbeSource();
            List<GuidConst> consts = ExtractGuidConsts(target.Raw);

            Assert.IsNotEmpty(consts, $"{LogPrefix} GUID 상수를 하나도 못 뽑았습니다.");

            int labeled = 0;
            foreach (GuidConst c in consts) if (c.SdkSymbol != null) labeled++;
            Assert.AreEqual(consts.Count, labeled,
                $"{LogPrefix} {consts.Count}개 중 {labeled}개만 SDK 심볼 라벨을 찾았습니다 — " +
                "주석 파서가 절반만 보고 있습니다.");

            // ★ 평범한 const string(LogPrefix 등)은 GUID로 세지 않는다는 것도 실제 파일에서 확인한다.
            //   이 파일에는 그런 상수가 실제로 있다 — 없다면 그 대조가 성립하지 않는다.
            int allConstStrings = 0;
            foreach (string line in target.Raw.Replace("\r\n", "\n").Split('\n'))
                if (ParseConstString(line) != null) allConstStrings++;

            Assert.Greater(allConstStrings, consts.Count,
                $"{LogPrefix} const string이 {allConstStrings}개인데 그중 GUID가 {consts.Count}개입니다. " +
                "GUID가 아닌 const string이 하나도 없으면 «GUID만 골라낸다»는 성질이 이 파일에서 " +
                "증명되지 않습니다(그 성질이 깨지면 LogPrefix 같은 상수가 골든 대조에 끌려 들어와 " +
                "거짓 빨강이 납니다).");
        }

        [Test]
        public void 골든표는_비어_있지_않고_전부_출처와_사유를_단다()
        {
            Assert.IsNotEmpty(WindowsComGolden, $"{LogPrefix} 골든표가 비었습니다 — " +
                                                "빈 표는 foreach가 아무것도 안 돌고 초록이 됩니다(거짓 통과 #5).");

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach ((string symbol, string guid, string header, string why) in WindowsComGolden)
            {
                Assert.IsNotEmpty(header, $"{LogPrefix} '{symbol}'의 출처 헤더가 비었습니다 — " +
                                          "출처를 못 대는 골든은 «누군가 소스에서 베낀 값»과 구분되지 않습니다.");
                Assert.IsNotEmpty(why, $"{LogPrefix} '{symbol}'의 사유가 비었습니다.");
                Assert.IsTrue(LooksLikeGuid(guid),
                    $"{LogPrefix} 골든 '{symbol}'의 값이 GUID 정규형이 아닙니다: {guid}");
                Assert.IsTrue(seen.Add(Guid.Parse(guid).ToString("D")),
                    $"{LogPrefix} 골든표 안에서 '{symbol}'의 값이 다른 항목과 중복됩니다 — " +
                    "골든이 스스로 모순이면 소스와의 대조 결과를 믿을 수 없습니다.");
            }
        }

        // ====================================================================
        // 5. 네거티브 컨트롤 — 가짜 소스를 <b>같은 함수</b>에 흘린다
        // ====================================================================

        /// <summary>한 글자 오타 — 이 감사가 존재하는 이유 그 자체다.</summary>
        [Test]
        public void NegativeControl_한_글자_틀린_GUID를_잡는다()
        {
            // 실제 값의 마지막 문자만 바꾼다(BCDE0395-…-692E -> …-692F).
            const string fake =
                "internal sealed class Fake\n" +
                "{\n" +
                "    /// <summary><c>CLSID_MMDeviceEnumerator</c>.</summary>\n" +
                "    internal const string DeviceEnumeratorClsid = \"BCDE0395-E52F-467C-8E3D-C4579291692F\";\n" +
                "}\n";

            List<GuidConst> consts = ExtractGuidConsts(fake);
            Assert.AreEqual(1, consts.Count, $"{LogPrefix} 파서가 상수를 못 뽑았습니다.");
            Assert.AreEqual("CLSID_MMDeviceEnumerator", consts[0].SdkSymbol,
                $"{LogPrefix} 바로 윗줄 주석의 SDK 심볼을 못 읽었습니다 — 그러면 골든 대조가 " +
                "통째로 건너뛰어집니다.");

            string golden = null;
            foreach ((string symbol, string value, string _, string __) in WindowsComGolden)
                if (symbol == consts[0].SdkSymbol) golden = value;

            Assert.IsNotNull(golden, $"{LogPrefix} 골든에서 그 심볼을 못 찾았습니다.");
            Assert.AreNotEqual(Guid.Parse(golden), Guid.Parse(consts[0].Value),
                $"{LogPrefix} 마지막 한 글자가 다른데도 같다고 판정했습니다 — 이 감사의 존재 이유가 " +
                "«한 글자만 틀려도 조용히 실패한다»입니다.");
        }

        /// <summary>두 상수가 같은 값이 되는 형태(복사 후 한 줄 고치기를 잊음).</summary>
        [Test]
        public void NegativeControl_같은_GUID가_두_상수에_들어가면_잡는다()
        {
            const string fake =
                "internal sealed class Fake\n" +
                "{\n" +
                "    /// <summary><c>CLSID_MMDeviceEnumerator</c>.</summary>\n" +
                "    internal const string A = \"BCDE0395-E52F-467C-8E3D-C4579291692E\";\n" +
                "    /// <summary><c>IID_IMMDeviceEnumerator</c>.</summary>\n" +
                "    internal const string B = \"BCDE0395-E52F-467C-8E3D-C4579291692E\";\n" +
                "}\n";

            List<GuidConst> consts = ExtractGuidConsts(fake);
            Assert.AreEqual(2, consts.Count, $"{LogPrefix} 상수 두 개를 못 뽑았습니다.");
            Assert.AreEqual(Guid.Parse(consts[0].Value), Guid.Parse(consts[1].Value),
                $"{LogPrefix} 같은 값을 다르다고 읽었습니다 — 중복 검사가 죽습니다.");
            Assert.AreNotEqual(consts[0].SdkSymbol, consts[1].SdkSymbol,
                $"{LogPrefix} 라벨 두 개를 구분하지 못했습니다(둘 다 같은 심볼로 읽었습니다). " +
                "여러 줄을 거슬러 올라가다 <b>앞 상수의 라벨</b>을 물었을 가능성이 큽니다.");
        }

        /// <summary>
        /// ★★ <b>이 감사를 첫날부터 거짓 빨강으로 만들 수 있었던 함정.</b>
        /// 대상 파일에는 <c>private const string LogPrefix = "[오디오감지]";</c>가 있다.
        /// GUID 꼴 검사를 안 하면 그 상수가 골든 대조에 끌려 들어가 «골든이 모르는 식별자»로 잡힌다.
        /// </summary>
        [Test]
        public void NegativeControl_GUID가_아닌_const_string은_세지_않는다()
        {
            const string fake =
                "internal sealed class Fake\n" +
                "{\n" +
                "    private const string LogPrefix = \"[오디오감지]\";\n" +
                "    private const string Tag = \"Windows/WASAPI(IAudioMeterInformation)\";\n" +
                "    /// <summary><c>IID_IMMDevice</c>.</summary>\n" +
                "    internal const string DeviceIid = \"D666063F-1587-4E43-81F1-B948E807363F\";\n" +
                "}\n";

            List<GuidConst> consts = ExtractGuidConsts(fake);
            Assert.AreEqual(1, consts.Count,
                $"{LogPrefix} GUID가 아닌 const string까지 셌습니다({consts.Count}개). " +
                "그러면 LogPrefix가 «골든이 모르는 COM 식별자»로 잡혀 이 감사는 첫 실행부터 " +
                "빨간 채로 방치되고, 방치된 감사는 없는 감사입니다.");
            Assert.AreEqual("IID_IMMDevice", consts[0].SdkSymbol);
        }

        /// <summary>주석에 남겨 둔 <b>옛 값</b>이 선언으로 세어지지 않는가.</summary>
        [Test]
        public void NegativeControl_주석_속_옛_상수는_선언이_아니다()
        {
            const string fake =
                "internal sealed class Fake\n" +
                "{\n" +
                "    // 옛 값: const string DeviceIid = \"00000000-0000-0000-0000-000000000001\";\n" +
                "    /// <summary><c>IID_IMMDevice</c>.</summary>\n" +
                "    internal const string DeviceIid = \"D666063F-1587-4E43-81F1-B948E807363F\";\n" +
                "}\n";

            List<GuidConst> consts = ExtractGuidConsts(fake);
            Assert.AreEqual(1, consts.Count,
                $"{LogPrefix} 주석 속 옛 상수를 선언으로 셌습니다({consts.Count}개). " +
                "그 유령은 «골든이 모르는 식별자»로 잡혀 거짓 빨강을 내고, 사람들은 " +
                "그것을 없애려고 <b>정직한 이력 주석</b>을 지웁니다.");
            Assert.AreEqual("D666063F-1587-4E43-81F1-B948E807363F", consts[0].Value);
        }

        /// <summary>선언 밖 인라인 리터럴을 잡는가.</summary>
        [Test]
        public void NegativeControl_인라인된_GUID_리터럴을_잡는다()
        {
            const string fake =
                "internal sealed class Fake\n" +
                "{\n" +
                "    /// <summary><c>IID_IMMDevice</c>.</summary>\n" +
                "    internal const string DeviceIid = \"D666063F-1587-4E43-81F1-B948E807363F\";\n" +
                "    [Guid(\"D666063F-1587-4E43-81F1-B948E807363F\")]\n" +
                "    private interface IMMDevice { }\n" +
                "}\n";

            List<string> literals = AllGuidStringLiterals(fake);
            List<GuidConst> consts = ExtractGuidConsts(fake);

            Assert.AreEqual(2, literals.Count,
                $"{LogPrefix} 리터럴을 {literals.Count}개로 셌습니다(기대 2) — 인라인 사본을 못 봅니다.");
            Assert.AreEqual(1, consts.Count);
            Assert.AreNotEqual(consts.Count, literals.Count,
                $"{LogPrefix} 사본이 하나 더 있는데 개수가 같다고 판정했습니다 — 드리프트 검사가 죽습니다.");
        }

        /// <summary>중괄호판·하이픈 없는 판을 정규형으로 인정하지 않는가(표기 혼입 방지).</summary>
        [Test]
        public void NegativeControl_정규형이_아닌_GUID_표기를_구분한다()
        {
            Assert.IsTrue(LooksLikeGuid("D666063F-1587-4E43-81F1-B948E807363F"));
            Assert.IsTrue(LooksLikeGuid("d666063f-1587-4e43-81f1-b948e807363f"),
                $"{LogPrefix} 소문자 정규형을 거절했습니다 — COM에서 대소문자는 의미가 없습니다.");
            Assert.IsFalse(LooksLikeGuid("{D666063F-1587-4E43-81F1-B948E807363F}"),
                $"{LogPrefix} 중괄호판을 정규형으로 인정했습니다.");
            Assert.IsFalse(LooksLikeGuid("D666063F15874E4381F1B948E807363F"),
                $"{LogPrefix} 하이픈 없는 판을 정규형으로 인정했습니다.");
            Assert.IsFalse(LooksLikeGuid("[오디오감지]"));
            Assert.IsFalse(LooksLikeGuid(""));
        }
    }
}
