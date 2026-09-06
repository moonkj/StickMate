using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ============================================================================
    /// 인용 감사 #6 — <b>「각 상수를 문자 4개로부터 다시 계산해 대조한다」던 그 기계</b>
    /// (test-engineer, 2026-09-06)
    /// ============================================================================
    /// <c>Platform/MacOS/MacSystemAudioActivityProbe.cs</c>의 FourCC 블록 주석 원문:
    ///
    /// <para><i>"---- FourCC 셀렉터. <b>값을 손으로 바꾸지 마라 — 틀려도 컴파일은 되고 status만
    /// 조용히 어긋난다.</b> <c>MacSystemAudioSelectorTests</c>가 각 상수를 문자 4개로부터 다시 계산해
    /// 대조한다."</i></para>
    ///
    /// <b>그 테스트는 2026-09-06까지 존재하지 않았다.</b> 바로 윗줄이 «틀려도 컴파일은 된다»고
    /// 경고하면서 이 테스트를 <b>그 위험의 유일한 안전망</b>으로 제시해 두었으므로, 이 건은
    /// 「없는 안전망을 있다고 적어 둔」 형태다.
    ///
    /// ============================================================================
    /// FourCC란 무엇이고 왜 기계가 필요한가
    /// ============================================================================
    /// CoreAudio의 셀렉터는 <b>ASCII 4글자를 빅엔디언으로 이어 붙인 32비트 정수</b>다.
    /// <c>'dOut'</c> = <c>0x64 0x4F 0x75 0x74</c> = <c>0x644F7574</c>.
    /// 사람 눈에 <c>0x644F7574</c>와 <c>0x644F7475</c>는 <b>구분되지 않는다</b>(<c>ut</c> ↔ <c>tu</c>).
    /// 그리고 틀리면 컴파일은 통과하고 <c>AudioObjectGetPropertyData</c>가 <b>다른 프로퍼티</b>를
    /// 조회해 status만 조용히 어긋난다 — 이 저장소가 반복해 당한
    /// <i>"실패한 측정이 성공한 측정과 똑같이 생겼다"</i>의 하드웨어판이다.
    ///
    /// ============================================================================
    /// ★ 두 다리로 선다 — 인용된 것 + 그보다 한 칸 바깥
    /// ============================================================================
    /// <list type="number">
    ///  <item><b>인용된 것</b>: 소스의 16진 값이 <b>바로 위 주석에 적힌 4글자</b>와 맞는가.
    ///    («각 상수를 문자 4개로부터 다시 계산해 대조한다» 그대로다.)</item>
    ///  <item><b>한 칸 바깥</b>: 그 <b>4글자 자체</b>가 Apple 헤더의 공표값과 맞는가.
    ///    ①만 있으면 <c>'gone'</c>을 <c>'glob'</c>으로 <b>주석까지 함께</b> 잘못 적은 경우를
    ///    구조적으로 못 본다(둘이 같이 틀리면 대조가 성립한다 — TEAM.md 열 번째 거짓 통과 형태).
    ///    그래서 <see cref="CoreAudioGolden"/>은 <b>우리 소스가 아니라 SDK 헤더</b>에서 온다.</item>
    /// </list>
    ///
    /// <para>★ 골든 출처(2026-09-06 저장소 <b>바깥</b>에서 재확인):
    /// <c>CoreAudio.framework/Headers/AudioHardware.h</c>(<c>'dOut'</c>·<c>'gone'</c>) ·
    /// <c>AudioHardwareBase.h</c>(<c>'glob'</c>). 의심되면 <b>그 헤더와</b> 대조해라 —
    /// 우리 소스와 대조하는 것은 이 테스트가 하려는 일 그 자체라 순환이다.</para>
    ///
    /// ============================================================================
    /// ★ 계산기는 <b>알려진 값으로 먼저 교정</b>한다 (TEAM.md 공통 처방)
    /// ============================================================================
    /// <see cref="NegativeControl_FourCC_계산기를_ASCII_표로_교정한다"/>가 <c>"AAAA"</c>·<c>"abcd"</c>처럼
    /// <b>CoreAudio와 무관한</b> 값으로 계산기를 먼저 맞춘다. 교정이 깨지면 그 뒤 숫자는 전부 폐기다.
    ///
    /// <para>리플렉션은 한 줄도 쓰지 않는다 — 그 파일은 <c>#if UNITY_STANDALONE_OSX</c> 안이라
    /// Windows 타깃에서는 <b>타입이 존재하지 않는다</b>(CLAUDE.md 활성 빌드 타깃 규칙).
    /// 이 감사는 어느 타깃에서 돌려도 같은 것을 잰다.</para>
    /// </summary>
    public sealed class MacSystemAudioSelectorTests
    {
        private const string LogPrefix = "[오디오셀렉터감사]";

        /// <summary>존재 단언용 니들 — 이름이 바뀌면 <b>시끄럽게</b> 빨개진다.</summary>
        private const string ProbeTypeName = "MacSystemAudioActivityProbe";

        /// <summary>CoreAudio 심볼의 접두. 이 파일의 주석 라벨은 전부 이걸로 시작한다.</summary>
        private const string CoreAudioSymbolPrefix = "kAudio";

        /// <summary>상수 선언 위로 몇 줄까지 거슬러 올라가 라벨을 찾는가.</summary>
        private const int LabelLookBackLines = 6;

        /// <summary>
        /// ★ <b>외부 골든.</b> 키는 <b>Apple이 정한 심볼 이름</b>이지 우리 상수 이름이 아니다 —
        /// 우리가 <c>SelectorDefaultOutputDevice</c>를 뭐라고 부르든 이 표는 낡지 않는다.
        /// </summary>
        private static readonly (string Symbol, string FourCc, string Header, string Why)[] CoreAudioGolden =
        {
            ("kAudioHardwarePropertyDefaultOutputDevice", "dOut", "AudioHardware.h",
                "기본 출력 장치 ID를 묻는 셀렉터. 틀리면 엉뚱한 프로퍼티를 읽어 장치 ID가 " +
                "쓰레기가 되고, 그 뒤 모든 조회가 조용히 실패한다."),
            ("kAudioDevicePropertyDeviceIsRunningSomewhere", "gone", "AudioHardware.h",
                "★ 이것이 이 파일의 전부다 — 0=무음 / 1=누군가 재생 중. 틀리면 «항상 무음»이 되어 " +
                "음악 반응 춤이 영원히 안 나오고, 아무 에러도 안 난다."),
            ("kAudioObjectPropertyScopeGlobal", "glob", "AudioHardwareBase.h",
                "★ Input이 아니다. 이 한 글자 차이가 마이크 동의창의 유무를 가른다 — " +
                "잘못 적으면 상주 앱이 사용자에게 마이크 권한을 묻는다(비침해 원칙 정면 위반)."),
        };

        // ====================================================================
        // 계산기 — 순수 함수. 아래 교정 테스트가 알려진 값으로 먼저 맞춘다.
        // ====================================================================

        /// <summary>
        /// ASCII 4글자 → 32비트. <b>빅엔디언</b>이다(첫 글자가 최상위 바이트).
        /// <para>비ASCII·길이≠4는 <c>null</c>. 조용히 0을 돌려주지 않는다 —
        /// 0은 «계산 실패»와 «값이 0»을 구분하지 못한다.</para>
        /// </summary>
        internal static uint? FourCcToUInt(string fourCc)
        {
            if (fourCc == null || fourCc.Length != 4) return null;
            uint value = 0;
            foreach (char c in fourCc)
            {
                if (c < 0x20 || c > 0x7E) return null;   // 표시 가능한 ASCII만
                value = (value << 8) | c;
            }
            return value;
        }

        private static bool IsIdentifierChar(char c)
            => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';

        internal readonly struct UintConst
        {
            public readonly string Name;
            public readonly uint Value;
            public readonly string Literal;
            public readonly string FourCc;      // 주석에서 읽은 4글자. 없으면 null
            public readonly string Symbol;      // 주석에서 읽은 kAudio* 심볼. 없으면 null
            public readonly int Line;

            public UintConst(string name, uint value, string literal, string fourCc, string symbol, int line)
            {
                Name = name;
                Value = value;
                Literal = literal;
                FourCc = fourCc;
                Symbol = symbol;
                Line = line;
            }

            public override string ToString()
                => $"{Symbol ?? "(심볼 없음)"} = '{FourCc ?? "----"}' / {Literal} ({Name}, :{Line})";
        }

        /// <summary>
        /// 그 줄에서 <b>코드 부분만</b> 남긴다(주석은 버린다).
        /// <para>★ 왜 필요한가: <see cref="ExtractUintConsts"/>는 라벨을 읽어야 해서 <b>원본</b>
        /// 줄을 훑는다. 그러면 <c>// const uint Old = 0x1234;</c>처럼 <b>주석에 남겨 둔 옛 값</b>이
        /// 선언으로 세어지고, 그 유령이 대조에 끌려 들어온다.</para>
        /// </summary>
        internal static string CodeOnly(string line)
        {
            if (line == null) return null;
            if (line.TrimStart().StartsWith("*", StringComparison.Ordinal)) return string.Empty;
            int at = line.IndexOf("//", StringComparison.Ordinal);
            return at >= 0 ? line.Substring(0, at) : line;
        }

        /// <summary>한 줄에서 <c>const uint 이름 = 값;</c>을 뽑는다. 아니면 <c>null</c>.</summary>
        internal static (string Name, uint Value, string Literal)? ParseUintConst(string rawLine)
        {
            string line = CodeOnly(rawLine);
            if (string.IsNullOrEmpty(line)) return null;
            const string marker = "const uint ";
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

            int valueStart = p;
            while (p < line.Length && (IsIdentifierChar(line[p]) || line[p] == 'x' || line[p] == 'X')) p++;
            if (p == valueStart) return null;
            string literal = line.Substring(valueStart, p - valueStart);

            uint value;
            if (literal.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (!uint.TryParse(literal.Substring(2), NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture, out value))
                {
                    return null;
                }
            }
            else if (!uint.TryParse(literal, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return null;
            }
            return (name, value, literal);
        }

        /// <summary>한 줄에서 <c>'xxxx'</c>(작은따옴표 안 정확히 4글자)를 뽑는다. 없으면 <c>null</c>.</summary>
        internal static string ParseFourCcLabel(string line)
        {
            if (line == null) return null;
            for (int i = 0; i + 5 < line.Length; i++)
            {
                if (line[i] != '\'') continue;
                if (line[i + 5] != '\'') continue;
                string candidate = line.Substring(i + 1, 4);
                if (FourCcToUInt(candidate) == null) continue;
                return candidate;
            }
            return null;
        }

        /// <summary>한 줄에서 <c>kAudio*</c> 심볼을 뽑는다. 없으면 <c>null</c>.</summary>
        internal static string ParseCoreAudioSymbol(string line)
        {
            if (line == null) return null;
            int at = line.IndexOf(CoreAudioSymbolPrefix, StringComparison.Ordinal);
            while (at >= 0)
            {
                bool leftOk = at == 0 || !IsIdentifierChar(line[at - 1]);
                if (leftOk)
                {
                    int p = at;
                    while (p < line.Length && IsIdentifierChar(line[p])) p++;
                    if (p - at > CoreAudioSymbolPrefix.Length) return line.Substring(at, p - at);
                }
                at = line.IndexOf(CoreAudioSymbolPrefix, at + 1, StringComparison.Ordinal);
            }
            return null;
        }

        /// <summary>
        /// 소스 <b>원본</b>에서 <c>const uint</c>를 전부 뽑고 <b>바로 위 주석</b>의 라벨(4글자·심볼)과 짝짓는다.
        /// <para>★ 주석이 곧 «무엇과 대조해야 하는가»의 라벨이므로 여기서는 주석을 걷어내지 않는다.</para>
        /// </summary>
        internal static List<UintConst> ExtractUintConsts(string rawSource)
        {
            var found = new List<UintConst>();
            if (string.IsNullOrEmpty(rawSource)) return found;

            string[] lines = rawSource.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                (string Name, uint Value, string Literal)? parsed = ParseUintConst(lines[i]);
                if (parsed == null) continue;

                string fourCc = null;
                string symbol = null;
                for (int j = i - 1; j >= 0 && j >= i - LabelLookBackLines; j--)
                {
                    if (fourCc == null) fourCc = ParseFourCcLabel(lines[j]);
                    if (symbol == null) symbol = ParseCoreAudioSymbol(lines[j]);
                    if (fourCc != null && symbol != null) break;
                    // 라벨이 아예 없는 상수(SystemObject 등)에서 위쪽 다른 상수의 라벨을 물지 않도록
                    // <b>다른 const 선언</b>을 만나면 즉시 멈춘다.
                    if (ParseUintConst(lines[j]) != null) break;
                }
                found.Add(new UintConst(parsed.Value.Name, parsed.Value.Value, parsed.Value.Literal,
                    fourCc, symbol, i + 1));
            }
            return found;
        }

        // ====================================================================
        // 대상 파일 — 경로가 아니라 타입 선언으로 찾는다
        // ====================================================================

        private static (string Path, string Raw) FindProbeSource()
        {
            string[] all = EntitlementAuditSource.ProductionSourceFiles();
            Assert.GreaterOrEqual(all.Length, EntitlementAuditSource.MinProductionFileCount,
                $"{LogPrefix} 프로덕션 .cs를 {all.Length}개밖에 읽지 못했습니다 — " +
                "이 상태의 '위반 0건'은 측정이 아닙니다.");

            var matches = new List<(string, string)>();
            foreach (string path in all)
            {
                string raw = File.ReadAllText(path);
                if (!EntitlementAuditSource.DeclaresType(
                        EntitlementAuditSource.StripComments(raw), ProbeTypeName))
                {
                    continue;
                }
                matches.Add((path, raw));
            }

            Assert.AreEqual(1, matches.Count,
                $"{LogPrefix} '{ProbeTypeName}'을 선언하는 파일이 {matches.Count}개입니다(기대 1). " +
                "이름이 바뀌었거나 파일이 사라졌습니다 — 그대로 두면 아래 대조가 " +
                "'대상이 없어서 위반 0건'이 되어 조용히 초록이 됩니다.");

            return matches[0];
        }

        private static string Rel(string path)
        {
            string root = Directory.GetParent(Application.dataPath)!.FullName;
            return path.Length > root.Length ? path.Substring(root.Length + 1) : path;
        }

        // ====================================================================
        // 1. ★ 본론 — 인용 원문 그대로: 「문자 4개로부터 다시 계산해 대조한다」
        // ====================================================================

        [Test]
        public void FourCC_상수는_주석의_문자_4개에서_다시_계산한_값과_같다()
        {
            (string Path, string Raw) target = FindProbeSource();
            List<UintConst> consts = ExtractUintConsts(target.Raw);

            var labeled = new List<UintConst>();
            foreach (UintConst c in consts) if (c.FourCc != null) labeled.Add(c);

            Debug.Log($"{LogPrefix} {Rel(target.Path)} — const uint {consts.Count}개 / FourCC 라벨 " +
                      $"{labeled.Count}개\n  " + string.Join("\n  ", consts.ConvertAll(c => c.ToString())));

            // ── 존재 쪽 먼저: 라벨 붙은 상수를 못 찾으면 아래 «불일치 0건»은 «못 봤다»다.
            Assert.GreaterOrEqual(labeled.Count, CoreAudioGolden.Length,
                $"{LogPrefix} FourCC 라벨이 붙은 const uint를 {labeled.Count}개만 찾았습니다" +
                $"(골든 {CoreAudioGolden.Length}개). 주석 관례(<c>'dOut'</c>)가 바뀌었거나 파서가 " +
                "죽었습니다. 이 상태에서 아래 대조는 아무것도 재지 않습니다 — " +
                "그리고 원 주석은 «값을 손으로 바꾸지 마라, 틀려도 컴파일은 된다»고 경고하면서 " +
                "이 대조를 유일한 안전망으로 세워 두었습니다.");

            var mismatched = new List<string>();
            foreach (UintConst c in labeled)
            {
                uint? recomputed = FourCcToUInt(c.FourCc);
                Assert.IsNotNull(recomputed,
                    $"{LogPrefix} '{c.FourCc}'를 32비트로 환산하지 못했습니다({c.Name}:{c.Line}). " +
                    "계산기가 죽었거나 라벨이 4글자 ASCII가 아닙니다.");

                if (recomputed.Value != c.Value)
                {
                    mismatched.Add(
                        $"{c.Symbol ?? c.Name} (:{c.Line})\n" +
                        $"      소스   {c.Literal} (= 0x{c.Value:X8})\n" +
                        $"      '{c.FourCc}' 재계산  0x{recomputed.Value:X8}");
                }
            }

            Assert.IsEmpty(mismatched,
                $"{LogPrefix} FourCC 상수가 <b>주석에 적힌 4글자와 다릅니다</b>:\n  " +
                string.Join("\n  ", mismatched) + "\n" +
                "이건 컴파일러가 절대 잡아 주지 않는 종류의 오류입니다 — 값이 틀려도 빌드는 되고 " +
                "AudioObjectGetPropertyData가 <b>다른 프로퍼티</b>를 조회해 status만 조용히 어긋납니다. " +
                "손으로 16진수를 고쳤다면 되돌리세요. 정말 다른 셀렉터로 바꾼 것이라면 " +
                "<b>주석의 4글자와 심볼 이름을 먼저</b> 고치세요(그게 이 대조의 기준입니다).");
        }

        // ====================================================================
        // 2. ★ 한 칸 바깥 — 그 4글자 자체가 Apple 헤더와 같은가
        // ====================================================================

        /// <summary>
        /// ①만으로는 «주석과 값이 함께 틀린 경우»를 구조적으로 못 본다. 그 형태가 이 저장소가
        /// 정본에 적어 둔 <b>열 번째 거짓 통과</b>(생성기와 검사기가 같은 잘못을 공유)다.
        /// </summary>
        [Test]
        public void FourCC_문자_4개가_Apple_헤더_공표값과_같다()
        {
            (string Path, string Raw) target = FindProbeSource();
            List<UintConst> consts = ExtractUintConsts(target.Raw);

            var bySymbol = new Dictionary<string, UintConst>(StringComparer.Ordinal);
            foreach (UintConst c in consts)
            {
                if (c.Symbol == null || c.FourCc == null) continue;
                bySymbol[c.Symbol] = c;
            }

            var missing = new List<string>();
            var mismatched = new List<string>();
            foreach ((string symbol, string fourCc, string header, string why) in CoreAudioGolden)
            {
                if (!bySymbol.TryGetValue(symbol, out UintConst actual))
                {
                    missing.Add($"{symbol} ({header}) — 소스에서 «심볼 + 4글자»가 붙은 상수를 못 찾음. {why}");
                    continue;
                }
                if (!string.Equals(actual.FourCc, fourCc, StringComparison.Ordinal))
                {
                    mismatched.Add($"{symbol} ({header})\n      소스 '{actual.FourCc}' (:{actual.Line})" +
                                   $"\n      헤더 '{fourCc}'\n      {why}");
                }
            }

            Assert.IsEmpty(missing,
                $"{LogPrefix} 골든에 있는 CoreAudio 심볼을 소스에서 찾지 못했습니다:\n  " +
                string.Join("\n  ", missing) + "\n" +
                "심볼 라벨(<c>kAudio…</c>)이나 4글자 라벨이 주석에서 사라졌다면, 그 순간부터 " +
                "이 감사는 그 상수를 <b>통째로 건너뜁니다</b>(부재 단언이 조용히 초록이 되는 형태). " +
                "구현이 정말 바뀌었다면 골든에서도 지우세요.");

            Assert.IsEmpty(mismatched,
                $"{LogPrefix} 주석의 4글자가 <b>Apple 헤더 공표값과 다릅니다</b>:\n  " +
                string.Join("\n  ", mismatched) + "\n" +
                "이 경우 위의 «값 ↔ 4글자» 대조는 <b>통과합니다</b> — 둘이 같이 틀렸기 때문입니다. " +
                "그래서 이 두 번째 다리가 필요합니다(TEAM.md: 생성기와 검사기가 코드를 공유하면 " +
                "둘 다 같은 방향으로 틀린다).");
        }

        // ====================================================================
        // 3. 양성 대조 — 「0건」이 능력을 증명한 뒤에만 값을 갖는다
        // ====================================================================

        [Test]
        public void 양성대조_스캐너가_실제_파일에서_라벨과_비라벨을_구분한다()
        {
            (string Path, string Raw) target = FindProbeSource();
            List<UintConst> consts = ExtractUintConsts(target.Raw);

            Assert.IsNotEmpty(consts, $"{LogPrefix} const uint를 하나도 못 뽑았습니다 — 파서가 죽었습니다.");

            int labeled = 0;
            foreach (UintConst c in consts) if (c.FourCc != null) labeled++;

            Assert.Greater(labeled, 0,
                $"{LogPrefix} FourCC 라벨이 붙은 상수가 0개입니다 — 주석 파서가 눈이 멀었습니다.");
            Assert.Less(labeled, consts.Count,
                $"{LogPrefix} const uint {consts.Count}개가 <b>전부</b> FourCC 라벨을 가졌습니다. " +
                "이 파일에는 FourCC가 아닌 const uint가 실제로 있습니다" +
                "(kAudioObjectSystemObject=1, kAudioObjectUnknown=0, ElementMain=0). " +
                "그것까지 FourCC로 세고 있다면 라벨 파서가 <b>위쪽 다른 상수의 라벨</b>을 " +
                "물고 있다는 뜻이고, 그 상태로는 «값 1이 'dOut'과 다르다»는 거짓 빨강이 납니다.");
        }

        [Test]
        public void 골든표는_비어_있지_않고_전부_출처와_사유를_단다()
        {
            Assert.IsNotEmpty(CoreAudioGolden, $"{LogPrefix} 골든표가 비었습니다 — " +
                                               "빈 표는 foreach가 아무것도 안 돌고 초록이 됩니다(거짓 통과 #5).");

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach ((string symbol, string fourCc, string header, string why) in CoreAudioGolden)
            {
                Assert.IsNotEmpty(header, $"{LogPrefix} '{symbol}'의 출처 헤더가 비었습니다 — " +
                                          "출처 없는 골든은 «소스에서 베낀 값»과 구분되지 않습니다.");
                Assert.IsNotEmpty(why, $"{LogPrefix} '{symbol}'의 사유가 비었습니다.");
                Assert.IsNotNull(FourCcToUInt(fourCc),
                    $"{LogPrefix} 골든 '{symbol}'의 4글자가 ASCII 4글자가 아닙니다: '{fourCc}'");
                Assert.IsTrue(seen.Add(fourCc),
                    $"{LogPrefix} 골든표 안에서 '{fourCc}'가 중복됩니다 — 골든이 스스로 모순이면 " +
                    "소스와의 대조 결과를 믿을 수 없습니다.");
            }
        }

        // ====================================================================
        // 4. 계산기 교정 — 알려진 값으로 먼저 맞춘다 (TEAM.md 공통 처방)
        // ====================================================================

        /// <summary>
        /// ★ CoreAudio와 <b>무관한</b> 값으로 교정한다. 여기가 깨지면 이 파일의 모든 숫자를 폐기한다.
        /// <para>기준은 ASCII 표다: <c>'A'</c>=0x41, <c>'a'</c>=0x61, <c>'b'</c>=0x62 …</para>
        /// </summary>
        [Test]
        public void NegativeControl_FourCC_계산기를_ASCII_표로_교정한다()
        {
            Assert.AreEqual(0x41414141u, FourCcToUInt("AAAA"),
                $"{LogPrefix} 'AAAA'가 0x41414141이 아닙니다 — ASCII 교정 실패. " +
                "이 계산기가 낸 다른 모든 숫자를 폐기하세요.");
            Assert.AreEqual(0x61626364u, FourCcToUInt("abcd"),
                $"{LogPrefix} 'abcd'가 0x61626364이 아닙니다 — 바이트 순서가 뒤집혔을 수 있습니다. " +
                "FourCC는 <b>빅엔디언</b>입니다(첫 글자가 최상위 바이트).");
            Assert.AreEqual(0x20202020u, FourCcToUInt("    "),
                $"{LogPrefix} 공백 4개가 0x20202020이 아닙니다.");

            // 뒤집힘을 실제로 잡는지 — 'abcd'와 'dcba'는 반드시 달라야 한다.
            Assert.AreNotEqual(FourCcToUInt("abcd"), FourCcToUInt("dcba"),
                $"{LogPrefix} 순서를 바꿔도 같은 값이 나옵니다 — 이 계산기는 " +
                "<c>ut</c> ↔ <c>tu</c> 오타를 <b>구조적으로</b> 못 잡습니다.");

            // 실패는 조용한 0이 아니라 null이어야 한다.
            Assert.IsNull(FourCcToUInt("abc"), $"{LogPrefix} 3글자를 받아들였습니다.");
            Assert.IsNull(FourCcToUInt("abcde"), $"{LogPrefix} 5글자를 받아들였습니다.");
            Assert.IsNull(FourCcToUInt(null), $"{LogPrefix} null을 받아들였습니다.");
            Assert.IsNull(FourCcToUInt("한글자넷"),
                $"{LogPrefix} 비ASCII를 받아들였습니다 — 이 파일의 주석에는 한글이 가득하므로 " +
                "여기가 뚫리면 «'조용한'» 같은 한글 인용부호 조각이 FourCC로 잡힙니다.");
        }

        // ====================================================================
        // 5. 네거티브 컨트롤 — 가짜 소스를 <b>같은 함수</b>에 흘린다
        // ====================================================================

        /// <summary>16진수만 손으로 고친 형태 — 원 주석이 경고한 바로 그 사고다.</summary>
        [Test]
        public void NegativeControl_16진수만_손으로_바꾸면_잡는다()
        {
            // 'dOut' = 0x644F7574. 마지막 두 바이트를 뒤집었다(ut -> tu). 사람 눈에는 거의 같다.
            const string fake =
                "internal sealed class Fake\n" +
                "{\n" +
                "    /// <summary><c>kAudioHardwarePropertyDefaultOutputDevice</c> = <c>'dOut'</c>.</summary>\n" +
                "    internal const uint SelectorDefaultOutputDevice = 0x644F7475;\n" +
                "}\n";

            List<UintConst> consts = ExtractUintConsts(fake);
            Assert.AreEqual(1, consts.Count, $"{LogPrefix} 상수를 못 뽑았습니다.");
            Assert.AreEqual("dOut", consts[0].FourCc,
                $"{LogPrefix} 주석의 4글자 라벨을 못 읽었습니다 — 그러면 대조가 통째로 건너뛰어집니다.");
            Assert.AreEqual("kAudioHardwarePropertyDefaultOutputDevice", consts[0].Symbol,
                $"{LogPrefix} 주석의 CoreAudio 심볼을 못 읽었습니다.");
            Assert.AreNotEqual(FourCcToUInt(consts[0].FourCc), consts[0].Value,
                $"{LogPrefix} 두 바이트가 뒤집혔는데 같다고 판정했습니다 — 이 감사의 존재 이유가 " +
                "«사람 눈에 0x644F7574와 0x644F7475는 구분되지 않는다»입니다.");
        }

        /// <summary>주석과 값이 <b>함께</b> 틀린 형태 — 첫 번째 다리는 통과하고 두 번째가 잡아야 한다.</summary>
        [Test]
        public void NegativeControl_주석과_값이_함께_틀리면_골든이_잡는다()
        {
            // 'gone'이어야 할 자리에 'glob'을 값까지 함께 적었다(0x676C6F62).
            const string fake =
                "internal sealed class Fake\n" +
                "{\n" +
                "    /// <summary><c>kAudioDevicePropertyDeviceIsRunningSomewhere</c> = <c>'glob'</c>.</summary>\n" +
                "    internal const uint SelectorDeviceIsRunningSomewhere = 0x676C6F62;\n" +
                "}\n";

            List<UintConst> consts = ExtractUintConsts(fake);
            Assert.AreEqual(1, consts.Count);

            // 첫 번째 다리(값 ↔ 4글자)는 <b>통과한다</b> — 둘이 같이 틀렸기 때문이다.
            Assert.AreEqual(FourCcToUInt(consts[0].FourCc), consts[0].Value,
                $"{LogPrefix} 이 표본은 «값과 주석이 서로 일치하지만 둘 다 틀린» 경우여야 합니다. " +
                "여기서 이미 불일치가 나면 아래 대조가 무엇을 증명하는지 알 수 없습니다.");

            // 두 번째 다리(4글자 ↔ 헤더)는 반드시 잡는다.
            string golden = null;
            foreach ((string symbol, string fourCc, string _, string __) in CoreAudioGolden)
                if (symbol == consts[0].Symbol) golden = fourCc;

            Assert.IsNotNull(golden, $"{LogPrefix} 골든에서 그 심볼을 못 찾았습니다.");
            Assert.AreNotEqual(golden, consts[0].FourCc,
                $"{LogPrefix} 주석까지 함께 틀린 경우를 골든이 못 잡았습니다 — " +
                "그러면 이 감사는 «스스로와 대조»하는 것이라 아무것도 증명하지 못합니다.");
        }

        /// <summary>
        /// ★★ <b>이 감사를 첫날부터 거짓 빨강으로 만들 수 있었던 함정.</b>
        /// 대상 파일에는 FourCC가 <b>아닌</b> <c>const uint</c>가 셋 있다
        /// (<c>SystemObject=1</c>·<c>UnknownObject=0</c>·<c>ElementMain=0</c>).
        /// 라벨 탐색이 위쪽 다른 상수의 <c>'xxxx'</c>를 물면 «1이 'dOut'과 다르다»는 거짓 빨강이 난다.
        /// </summary>
        [Test]
        public void NegativeControl_FourCC가_아닌_상수는_라벨을_빌려_오지_않는다()
        {
            const string fake =
                "internal sealed class Fake\n" +
                "{\n" +
                "    /// <summary><c>kAudioObjectPropertyScopeGlobal</c> = <c>'glob'</c>.</summary>\n" +
                "    internal const uint ScopeGlobal = 0x676C6F62;\n" +
                "\n" +
                "    /// <summary><c>kAudioObjectPropertyElementMain</c>.</summary>\n" +
                "    private const uint ElementMain = 0;\n" +
                "}\n";

            List<UintConst> consts = ExtractUintConsts(fake);
            Assert.AreEqual(2, consts.Count, $"{LogPrefix} 상수 두 개를 못 뽑았습니다.");

            UintConst scope = consts[0].Name == "ScopeGlobal" ? consts[0] : consts[1];
            UintConst element = consts[0].Name == "ElementMain" ? consts[0] : consts[1];

            Assert.AreEqual("glob", scope.FourCc, $"{LogPrefix} 라벨 있는 쪽을 못 읽었습니다.");
            Assert.IsNull(element.FourCc,
                $"{LogPrefix} FourCC가 아닌 상수가 <b>위쪽 상수의 라벨</b>('{element.FourCc}')을 " +
                "빌려 왔습니다. 그러면 «0이 'glob'과 다르다»는 거짓 빨강이 나고, 이 감사는 " +
                "첫 실행부터 빨간 채로 방치됩니다 — 방치된 감사는 없는 감사입니다.");
            Assert.AreEqual("kAudioObjectPropertyElementMain", element.Symbol,
                $"{LogPrefix} 심볼 라벨은 자기 것을 읽어야 합니다.");
        }

        /// <summary>10진 리터럴과 16진 리터럴을 둘 다 읽는가(파서가 절반만 보면 조용히 건너뛴다).</summary>
        [Test]
        public void NegativeControl_10진과_16진_리터럴을_모두_읽는다()
        {
            (string Name, uint Value, string Literal)? hex =
                ParseUintConst("        internal const uint A = 0x644F7574;");
            Assert.IsNotNull(hex, $"{LogPrefix} 16진 리터럴을 못 읽었습니다.");
            Assert.AreEqual(0x644F7574u, hex.Value.Value);

            (string Name, uint Value, string Literal)? dec =
                ParseUintConst("        private const uint B = 1;");
            Assert.IsNotNull(dec, $"{LogPrefix} 10진 리터럴을 못 읽었습니다 — 그러면 라벨 없는 상수를 " +
                                  "아예 못 보게 되어 위 «라벨/비라벨 구분» 대조가 성립하지 않습니다.");
            Assert.AreEqual(1u, dec.Value.Value);

            Assert.IsNull(ParseUintConst("        private const string C = \"x\";"),
                $"{LogPrefix} const string을 const uint로 읽었습니다.");
            Assert.IsNull(ParseUintConst("        // const uint D = 0x1; 라고 적힌 주석"),
                $"{LogPrefix} 주석 줄의 상수 선언을 읽었습니다 — 주석 속 «옛 값»이 대조에 " +
                "끌려 들어옵니다.");
        }
    }
}
