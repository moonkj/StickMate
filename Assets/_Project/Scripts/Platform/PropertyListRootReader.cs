using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace StickMate.Platform
{
    /// <summary>macOS <c>Info.plist</c> <b>루트 dict</b>의 항목 하나. 중첩 dict/array 안은 보지 않는다.</summary>
    public readonly struct PlistRootEntry
    {
        /// <summary><c>&lt;key&gt;</c>의 텍스트.</summary>
        public readonly string Key;

        /// <summary>값 요소의 이름(<c>string</c> / <c>true</c> / <c>false</c> / <c>dict</c> / <c>array</c> / …).
        /// plist에서 boolean은 <b>요소 이름 자체가 값</b>이라 이 필드가 곧 값이다.</summary>
        public readonly string ValueElementName;

        /// <summary>단순 값의 텍스트. 컨테이너(<c>dict</c>/<c>array</c>)와 boolean은 빈 문자열이다.</summary>
        public readonly string ValueText;

        public PlistRootEntry(string key, string valueElementName, string valueText)
        {
            Key = key;
            ValueElementName = valueElementName;
            ValueText = valueText;
        }

        public override string ToString() =>
            ValueText.Length > 0 ? $"{Key}=<{ValueElementName}>{ValueText}" : $"{Key}=<{ValueElementName}/>";
    }

    /// <summary>
    /// macOS <c>Info.plist</c>(XML property list)의 <b>루트 dict 읽기</b>와, 그 텍스트에 boolean 키
    /// 하나를 <b>끼워 넣은 새 텍스트를 만드는</b> 순수 함수.
    ///
    /// ========================================================================
    /// 왜 여기(플랫폼 중립 <c>Platform/</c>)에 있는가
    /// ========================================================================
    /// Windows 쪽의 <see cref="PortableExecutableExportReader"/>와 <b>정확히 같은 배치</b>다:
    /// <b>읽기·계산은 여기, 디스크에 바이트를 쓰는 일은 에디터 빌드 후처리</b>. 그렇게 갈라 두면
    /// EditMode 테스트가 <b>아무 산출물 없이도</b> 파싱·삽입 로직 전량을 먹여 볼 수 있다
    /// (테스트가 <c>Builds/</c>에 기대면 "빌드가 없어서 초록"이라는 가장 조용한 거짓 통과가 생긴다).
    /// 이 타입은 디스크 API를 <b>하나도</b> 갖지 않는다 — 그 사실을 감사가 소스에서 다시 잰다.
    ///
    /// ========================================================================
    /// ★ 삽입을 "XML 재직렬화"가 아니라 "텍스트 끼워넣기"로 한 이유
    /// ========================================================================
    /// <see cref="XDocument"/>로 읽어 고치고 다시 쓰면 <b>문서 전체가 재포맷된다</b> — 들여쓰기,
    /// 빈 요소 표기(<c>&lt;true /&gt;</c> vs <c>&lt;true/&gt;</c>), DOCTYPE 선언, 줄 끝 문자가 전부
    /// 새로 만들어진다. 그러면 "우리가 바꾼 것은 정확히 무엇인가"를 아무도 대조할 수 없다.
    /// Windows 후처리가 <b>달라진 바이트가 값 DWORD 안에만 있는지</b>를 전량 비교로 확인하는 것과
    /// 같은 규율을 여기서도 지키려면, 바뀐 자리가 <b>연속된 한 덩어리의 삽입</b>이어야 한다.
    ///
    /// <para>그래서 이 클래스는 <b>파싱은 진짜 XML 파서로</b> 하되(구조를 추측하지 않는다),
    /// <b>쓰기는 최소 삽입으로</b> 한 뒤 <b>결과를 다시 파싱해</b> "원래 항목 전부가 순서까지 그대로이고
    /// 새 항목 하나만 늘었는가"를 확인한다. 세 가지가 다 통과해야 결과를 돌려준다.</para>
    ///
    /// <para><b>DTD는 절대 해석하지 않는다.</b> plist의 DOCTYPE은 <c>http://www.apple.com/DTDs/</c>를
    /// 가리킨다. 기본 설정의 XML 파서는 그것을 <b>가져오려고 네트워크를 친다</b> — 빌드 후처리가
    /// 네트워크에 의존하는 것은 그 자체로 결함이다(오프라인에서 조용히 느려지거나 실패한다).
    /// <see cref="XmlReaderSettings.DtdProcessing"/>을 <see cref="DtdProcessing.Ignore"/>로,
    /// <see cref="XmlReaderSettings.XmlResolver"/>를 <c>null</c>로 못 박는다.</para>
    /// </summary>
    public static class PropertyListRootReader
    {
        /// <summary>바이너리 plist의 매직. 이 형식은 <b>다루지 않는다</b> — 텍스트 삽입이 성립하지 않는다.</summary>
        public const string BinaryPlistMagic = "bplist";

        private const string PlistRootElementName = "plist";
        private const string DictElementName = "dict";
        private const string KeyElementName = "key";

        /// <summary>컨테이너 요소(값 텍스트를 읽지 않는다).</summary>
        private static readonly string[] ContainerElements = { "dict", "array" };

        /// <summary>값 텍스트가 없고 <b>요소 이름 자체가 값</b>인 요소.</summary>
        private static readonly string[] BooleanElements = { "true", "false" };

        // ====================================================================
        // 읽기
        // ====================================================================

        /// <summary>
        /// plist 텍스트에서 <b>루트 dict의 직계 항목</b>을 문서 순서대로 읽는다.
        /// 중첩 dict/array 안으로는 들어가지 않는다 — <b>의도적</b>이다. 중첩된 곳에 같은 이름의 키가
        /// 있어도 그것은 루트 선언이 아니며, 그것을 "있다"고 세면 후처리가 <b>필요한 삽입을 건너뛴다</b>.
        /// </summary>
        public static bool TryReadRootEntries(string text, out List<PlistRootEntry> entries, out string error)
        {
            entries = null;

            if (string.IsNullOrEmpty(text))
            {
                error = "plist 텍스트가 비어 있다.";
                return false;
            }

            if (text.StartsWith(BinaryPlistMagic, StringComparison.Ordinal))
            {
                error = $"바이너리 plist({BinaryPlistMagic})다. 이 도구는 XML plist만 다룬다 — " +
                        "텍스트 삽입이 성립하지 않으므로 한 글자도 쓰지 않는다.";
                return false;
            }

            XDocument document;
            try
            {
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Ignore,   // Apple DTD를 가지러 네트워크를 치지 않는다.
                    XmlResolver = null,
                    IgnoreComments = false,
                    IgnoreWhitespace = true,
                };
                using (var stringReader = new StringReader(text))
                using (XmlReader reader = XmlReader.Create(stringReader, settings))
                {
                    document = XDocument.Load(reader);
                }
            }
            catch (Exception e)
            {
                error = $"XML 파싱 실패 — {e.GetType().Name}: {e.Message}";
                return false;
            }

            XElement root = document.Root;
            if (root == null || !string.Equals(root.Name.LocalName, PlistRootElementName, StringComparison.Ordinal))
            {
                error = $"루트 요소가 <{PlistRootElementName}>이 아니다(실제: " +
                        $"<{(root == null ? "(없음)" : root.Name.LocalName)}>).";
                return false;
            }

            XElement rootDict = null;
            foreach (XElement child in root.Elements())
            {
                if (!string.Equals(child.Name.LocalName, DictElementName, StringComparison.Ordinal)) continue;
                rootDict = child;
                break;
            }
            if (rootDict == null)
            {
                error = $"<{PlistRootElementName}> 안에 루트 <{DictElementName}>이 없다.";
                return false;
            }

            var result = new List<PlistRootEntry>();
            string pendingKey = null;
            foreach (XElement child in rootDict.Elements())
            {
                string name = child.Name.LocalName;
                if (string.Equals(name, KeyElementName, StringComparison.Ordinal))
                {
                    if (pendingKey != null)
                    {
                        error = $"<{KeyElementName}>가 연달아 두 번 나왔다('{pendingKey}' 다음 '{child.Value}') — " +
                                "루트 dict의 key/value 짝이 깨져 있다.";
                        return false;
                    }
                    pendingKey = child.Value;
                    continue;
                }

                if (pendingKey == null)
                {
                    error = $"<{KeyElementName}> 없이 값 <{name}>이 먼저 나왔다 — " +
                            "루트 dict의 key/value 짝이 깨져 있다.";
                    return false;
                }

                result.Add(new PlistRootEntry(pendingKey, name, ReadValueText(child)));
                pendingKey = null;
            }

            if (pendingKey != null)
            {
                error = $"마지막 <{KeyElementName}>('{pendingKey}')에 짝이 되는 값이 없다.";
                return false;
            }

            entries = result;
            error = null;
            return true;
        }

        private static string ReadValueText(XElement value)
        {
            string name = value.Name.LocalName;
            foreach (string container in ContainerElements)
            {
                if (string.Equals(name, container, StringComparison.Ordinal)) return string.Empty;
            }
            foreach (string boolean in BooleanElements)
            {
                if (string.Equals(name, boolean, StringComparison.Ordinal)) return string.Empty;
            }
            return value.Value;
        }

        /// <summary>루트 항목 목록에서 키 하나를 찾는다(정확한 대소문자 일치).</summary>
        public static bool TryFind(IReadOnlyList<PlistRootEntry> entries, string key, out PlistRootEntry entry)
        {
            entry = default;
            if (entries == null || string.IsNullOrEmpty(key)) return false;

            for (int i = 0; i < entries.Count; i++)
            {
                if (!string.Equals(entries[i].Key, key, StringComparison.Ordinal)) continue;
                entry = entries[i];
                return true;
            }
            return false;
        }

        // ====================================================================
        // 삽입 (순수 함수 — 디스크에 닿지 않는다)
        // ====================================================================

        /// <summary>
        /// 루트 dict의 <b>끝</b>에 <c>&lt;key&gt;…&lt;/key&gt;</c> + <c>&lt;true/&gt;</c>(또는
        /// <c>&lt;false/&gt;</c>)를 끼워 넣은 <b>새 텍스트</b>를 만든다. 원본 문자열은 바뀌지 않는다.
        ///
        /// <para><b>이미 있으면 실패로 돌려준다</b>(덮어쓰지도, 조용히 넘어가지도 않는다). "이미 있을 때
        /// 무엇을 할 것인가"는 <b>정책</b>이고 그 판정은
        /// <see cref="HybridGpuPreferencePolicy.ClassifyMacPlistEntry"/>가 한다. 여기서 임의로 넘어가면
        /// 판정이 두 곳으로 갈라진다.</para>
        ///
        /// <para>들여쓰기·줄끝·빈 요소 표기는 <b>원본에서 관찰해 그대로 따른다</b>. 새 두 줄이 나머지와
        /// 다르게 생기면 사람이 디프에서 "무엇이 원래 있던 것인지"를 잘못 읽는다.</para>
        /// </summary>
        public static bool TryInsertBooleanEntry(string text, string key, bool value,
            out string result, out string error)
        {
            result = null;

            if (string.IsNullOrEmpty(key))
            {
                error = "삽입할 키 이름이 비어 있다.";
                return false;
            }
            if (key.IndexOf('<') >= 0 || key.IndexOf('>') >= 0 || key.IndexOf('&') >= 0)
            {
                error = $"키 이름에 XML 특수문자가 있다('{key}') — 이스케이프를 추측하지 않고 거부한다.";
                return false;
            }

            if (!TryReadRootEntries(text, out List<PlistRootEntry> before, out error)) return false;

            if (TryFind(before, key, out PlistRootEntry existing))
            {
                error = $"'{key}'가 루트 dict에 이미 있다(<{existing.ValueElementName}/>). " +
                        "삽입은 '없을 때'만 하는 동작이다 — 판정을 먼저 물어라.";
                return false;
            }

            // ---- 루트 dict의 닫는 태그 자리 ----
            // 루트 dict는 모든 중첩 dict를 감싸므로 그 닫는 태그가 문서에서 **마지막** </dict>다.
            // (XML에서 문자열 값 안의 '<'는 반드시 이스케이프되므로 값 안에 </dict>가 문자 그대로
            //  나타날 수 없다. 그래도 마지막에 재파싱 대조로 다시 확인한다.)
            const string dictClose = "</" + DictElementName + ">";
            int close = text.LastIndexOf(dictClose, StringComparison.Ordinal);
            if (close < 0)
            {
                error = $"루트 dict의 닫는 태그({dictClose})를 찾지 못했다 — " +
                        "빈 dict가 자기닫힘(<dict/>)으로 적혀 있으면 이 삽입기는 다루지 않는다.";
                return false;
            }

            string newline = text.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n";
            int lineStart = text.LastIndexOf('\n', close) + 1;
            string closeIndent = IndentOf(text, lineStart, close);
            string childIndent = LastKeyIndent(text, close) ?? (closeIndent + "  ");
            string emptyElementSuffix = text.IndexOf("<" + HybridGpuPreferencePolicy.MacTrueElementName + " />",
                StringComparison.Ordinal) >= 0 ? " />" : "/>";

            string element = value ? HybridGpuPreferencePolicy.MacTrueElementName
                                   : HybridGpuPreferencePolicy.MacFalseElementName;
            string insertion = childIndent + "<" + KeyElementName + ">" + key + "</" + KeyElementName + ">" + newline +
                               childIndent + "<" + element + emptyElementSuffix + newline;

            string candidate = text.Substring(0, lineStart) + insertion + text.Substring(lineStart);

            // ---- 되읽기 대조: 원래 항목 전부가 순서까지 그대로이고, 새 항목 하나만 늘었는가 ----
            if (!TryReadRootEntries(candidate, out List<PlistRootEntry> after, out string reparseError))
            {
                error = "삽입 결과가 다시 파싱되지 않는다 — " + reparseError +
                        " (원본을 건드리지 않았으므로 잃은 것은 없다.)";
                return false;
            }

            if (after.Count != before.Count + 1)
            {
                error = $"삽입 후 루트 항목이 {before.Count} -> {after.Count}개다. 정확히 1개만 늘어야 한다.";
                return false;
            }

            for (int i = 0; i < before.Count; i++)
            {
                if (string.Equals(before[i].Key, after[i].Key, StringComparison.Ordinal) &&
                    string.Equals(before[i].ValueElementName, after[i].ValueElementName, StringComparison.Ordinal) &&
                    string.Equals(before[i].ValueText, after[i].ValueText, StringComparison.Ordinal))
                {
                    continue;
                }
                error = $"삽입이 기존 항목 {i}번을 바꿨다({before[i]} -> {after[i]}). " +
                        "한 글자도 돌려주지 않는다.";
                return false;
            }

            PlistRootEntry added = after[after.Count - 1];
            if (!string.Equals(added.Key, key, StringComparison.Ordinal) ||
                !string.Equals(added.ValueElementName, element, StringComparison.Ordinal))
            {
                error = $"삽입된 항목이 기대와 다르다(기대 {key}=<{element}/>, 실제 {added}).";
                return false;
            }

            // 텍스트 자체도 '연속된 한 덩어리의 삽입'이어야 한다(앞뒤가 원본과 완전히 같은가).
            if (candidate.Length != text.Length + insertion.Length ||
                !string.Equals(candidate.Substring(0, lineStart), text.Substring(0, lineStart), StringComparison.Ordinal) ||
                !string.Equals(candidate.Substring(lineStart + insertion.Length), text.Substring(lineStart), StringComparison.Ordinal))
            {
                error = "삽입 결과가 '원본 + 연속된 한 덩어리'가 아니다 — 문자열 조작을 의심하라.";
                return false;
            }

            result = candidate;
            error = null;
            return true;
        }

        /// <summary><paramref name="lineStart"/>부터 <paramref name="close"/>까지가 전부 공백이면 그 공백,
        /// 아니면 빈 문자열. (같은 줄에 다른 내용이 있으면 들여쓰기를 흉내 내지 않는다.)</summary>
        private static string IndentOf(string text, int lineStart, int close)
        {
            for (int i = lineStart; i < close; i++)
            {
                if (text[i] != ' ' && text[i] != '\t') return string.Empty;
            }
            return text.Substring(lineStart, close - lineStart);
        }

        /// <summary>닫는 태그 앞의 <b>마지막 <c>&lt;key&gt;</c> 줄</b>이 쓰는 들여쓰기.
        /// 원본이 실제로 쓰는 들여쓰기를 그대로 따라가기 위한 관찰이다.</summary>
        private static string LastKeyIndent(string text, int before)
        {
            const string keyOpen = "<" + KeyElementName + ">";
            int at = text.LastIndexOf(keyOpen, Math.Max(0, before - 1), StringComparison.Ordinal);
            if (at < 0) return null;

            int lineStart = text.LastIndexOf('\n', at) + 1;
            string indent = IndentOf(text, lineStart, at);
            return indent.Length > 0 ? indent : null;
        }

        /// <summary>사람이 읽을 요약(영수증·로그용).</summary>
        public static string Describe(IReadOnlyList<PlistRootEntry> entries)
        {
            if (entries == null) return "(없음)";
            var sb = new StringBuilder();
            for (int i = 0; i < entries.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(entries[i].Key);
            }
            return sb.ToString();
        }
    }
}
