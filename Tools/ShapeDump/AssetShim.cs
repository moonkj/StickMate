// =============================================================================
// ★ Unity 직렬화기 대역 — `.asset`(YAML) -> ScriptableObject
//
// 왜 이것이 shim 중 유일하게 "코드가 있는" 조각인가
// -----------------------------------------------------------------------------
// 2026-09-02 B-2 파일럿으로 NECK 6종의 <b>몸에 붙는 좌표가 코드에서 에셋으로 내려갔다</b>.
// 그래서 형상을 스텁으로 흉내내면(WornShapes -> null) 덤프의 NECK 6종이 통째로 사라지고,
// mirrordrift/prodverify 는 "설계 4도형 ≠ 프로덕션 0도형"만 잔뜩 뱉는 <b>거짓 빨간불</b>이 된다.
// 반대로 좌표를 손으로 베껴 두면 그것이 곧 세 번째 진실이다.
//
// 그래서 흉내내는 것을 <b>로직이 아니라 로더</b> 한 겹으로 좁혔다:
//  · 스트림 문법을 읽는 것은 프로덕션 AccessoryWornShapeReader 그대로,
//  · 표를 만드는 것은 프로덕션 ItemCatalog 그대로,
//  · 여기서는 디스크의 YAML 을 필드 이름으로 <b>리플렉션 바인딩</b>만 한다.
//
// ★ 리플렉션인 이유: 필드를 손으로 매핑하면 AccessoryDefSO 에 필드가 하나 생길 때마다
//   이 파일이 조용히 뒤처진다(오늘 고치는 사고와 같은 형태다). 이름이 곧 YAML 키라
//   리플렉션이 Unity 의 규칙을 그대로 따른다 — <b>키가 없으면 필드는 기본값 그대로</b>인 것까지.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace UnityEngine
{
    public static class Resources
    {
        /// <summary>에셋 뿌리. build.sh 가 STICKMATE_RESOURCES 로 넘긴다.</summary>
        public static string Root =>
            Environment.GetEnvironmentVariable("STICKMATE_RESOURCES")
            ?? "/Users/kjmoon/App/StickMate/Assets/_Project/Resources";

        public static T[] LoadAll<T>(string folder) where T : Object
        {
            string dir = Path.Combine(Root, folder);
            if (!Directory.Exists(dir))
            {
                Debug.LogError($"[Resources 대역] 폴더가 없습니다: {dir}");
                return Array.Empty<T>();
            }

            var loaded = new List<T>();
            // 파일 이름 순 — 순서가 결과에 영향을 주면 안 되지만, 재현 가능해야 diff 가 뜻을 갖는다.
            foreach (string path in Directory.GetFiles(dir, "*.asset").OrderBy(p => p, StringComparer.Ordinal))
            {
                object o = AssetYaml.Load(path, typeof(T));
                if (o is T typed) loaded.Add(typed);
            }
            return loaded.ToArray();
        }
    }

    internal static class AssetYaml
    {
        public static object Load(string path, Type type)
        {
            object instance = Activator.CreateInstance(type);   // 필드 초기화자가 여기서 돈다
            if (instance is Object uo) uo.name = Path.GetFileNameWithoutExtension(path);

            Node root = Parse(File.ReadAllLines(path));
            if (root?.Map != null && root.Map.TryGetValue("MonoBehaviour", out Node body) && body?.Map != null)
            {
                Bind(instance, type, body.Map);
            }
            return instance;
        }

        // ---------------------------------------------------------------- 파서

        internal sealed class Node
        {
            public string Scalar;
            public Dictionary<string, Node> Map;
            public List<Node> List;
        }

        private static readonly Regex KeyLine = new Regex(@"^([A-Za-z_][A-Za-z0-9_]*):\s?(.*)$");

        private struct Line { public int Indent; public string Text; }

        private static Node Parse(string[] raw)
        {
            var lines = new List<Line>();
            foreach (string r in raw)
            {
                if (r.Length == 0) continue;
                if (r[0] == '%' || r.StartsWith("---")) continue;

                int indent = 0;
                while (indent < r.Length && r[indent] == ' ') indent++;
                string text = r.Substring(indent).TrimEnd();
                if (text.Length == 0 || text[0] == '#') continue;

                if (text == "-" || text.StartsWith("- "))
                {
                    string rest = text.Length > 1 ? text.Substring(2) : string.Empty;
                    if (KeyLine.IsMatch(rest))
                    {
                        // `- kind: 4` 는 "리스트 항목 시작" + "그 항목의 첫 키"로 나눈다.
                        lines.Add(new Line { Indent = indent, Text = "-" });
                        lines.Add(new Line { Indent = indent + 2, Text = rest });
                    }
                    else
                    {
                        lines.Add(new Line { Indent = indent, Text = "- " + rest });
                    }
                }
                else
                {
                    lines.Add(new Line { Indent = indent, Text = text });
                }
            }

            int i = 0;
            return lines.Count == 0 ? null : ParseValue(lines, ref i, lines[0].Indent);
        }

        private static Node ParseValue(List<Line> lines, ref int i, int indent)
            => i < lines.Count && lines[i].Text.StartsWith("-")
                ? ParseList(lines, ref i, indent)
                : ParseMap(lines, ref i, indent);

        private static Node ParseMap(List<Line> lines, ref int i, int indent)
        {
            var map = new Dictionary<string, Node>(StringComparer.Ordinal);
            while (i < lines.Count && lines[i].Indent == indent)
            {
                Match m = KeyLine.Match(lines[i].Text);
                if (!m.Success) break;

                string key = m.Groups[1].Value;
                string rest = m.Groups[2].Value.Trim();
                i++;
                if (rest.Length > 0)
                {
                    // ★ 접힌 큰따옴표 스칼라를 이어 붙인다 (2026-09-02 양성 대조가 잡은 결함).
                    //   Unity 는 긴 문자열을 열 너비에서 접고 다음 줄을 <b>더 깊게</b> 들여쓴다:
                    //       description: "장식 없는 천 모자. 챙은 항상
                    //         가는 쪽을 향한다."
                    //   이걸 안 이으면 그 깊은 줄에서 아래 while 이 그냥 빠져나가
                    //   <b>그 뒤 필드가 통째로 사라진다</b>(requiredLevel·hidesHair·icon·cohortId…).
                    //   실측: 42종 중 3종이 이 모양이고, 셋 다 requiredLevel 이 1(= 필드 초기화자와
                    //   같은 값)이라 <b>사라진 것과 제대로 읽은 것이 똑같이 생겼다</b>.
                    //   equip_head_cap 의 hidesHair(1)와 icon(3조각)이 조용히 죽어 있었다.
                    //   YAML 큰따옴표 스칼라의 접힘 규칙대로 개행+들여쓰기를 공백 하나로 되돌린다.
                    while (rest.Length > 0 && rest[0] == '"' && !ClosesQuoted(rest)
                           && i < lines.Count && lines[i].Indent > indent)
                    {
                        rest += " " + lines[i].Text;
                        i++;
                    }
                    map[key] = new Node { Scalar = rest };
                }
                else if (i < lines.Count && lines[i].Indent > indent)
                {
                    map[key] = ParseValue(lines, ref i, lines[i].Indent);
                }
                else if (i < lines.Count && lines[i].Indent == indent && lines[i].Text.StartsWith("-"))
                {
                    // ★ Unity 는 시퀀스를 <b>부모 키와 같은 들여쓰기</b>로 적는다
                    //   (`terms:` 아래 `- 4`가 둘 다 4칸). 이 한 줄이 없으면 wornShapes/terms 가
                    //   통째로 빈 배열이 되고 NECK 6종이 조용히 사라진다 — 실제로 그랬다.
                    map[key] = ParseList(lines, ref i, indent);
                }
                else
                {
                    map[key] = new Node { List = new List<Node>() };   // `key:` 뒤에 아무것도 없음
                }
            }

            // ★ 여기서 <b>더 깊은</b> 줄에 걸려 빠져나왔다면 그건 "맵이 끝났다"가 아니라
            //   "이 파서가 못 읽는 문법을 만났다"이고, 그 뒤 필드는 전부 유실된다.
            //   조용히 지나가면 유실된 값이 필드 초기화자와 같을 때 <b>정상 로드와 구분되지 않는다</b>.
            //   그래서 큰 소리로 신고한다 — @LOG 가 0이 아니면 그 실행의 모든 숫자가 무효다.
            if (i < lines.Count && lines[i].Indent > indent)
            {
                Debug.LogError($"[AssetShim] YAML 파서가 못 읽는 줄에서 멈췄습니다(들여쓰기 {lines[i].Indent} > {indent}): " +
                    $"'{lines[i].Text}'. 이 줄 뒤의 필드는 전부 읽히지 않았습니다.");
            }
            return new Node { Map = map };
        }

        private static Node ParseList(List<Line> lines, ref int i, int indent)
        {
            var list = new List<Node>();
            while (i < lines.Count && lines[i].Indent == indent && lines[i].Text.StartsWith("-"))
            {
                if (lines[i].Text == "-")
                {
                    i++;
                    list.Add(i < lines.Count && lines[i].Indent > indent
                        ? ParseValue(lines, ref i, lines[i].Indent)
                        : new Node { Map = new Dictionary<string, Node>(StringComparer.Ordinal) });
                }
                else
                {
                    list.Add(new Node { Scalar = lines[i].Text.Substring(2).Trim() });
                    i++;
                }
            }
            return new Node { List = list };
        }

        // ---------------------------------------------------------------- 바인딩

        private static void Bind(object target, Type type, Dictionary<string, Node> map)
        {
            foreach (FieldInfo f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                // ★ 키가 없으면 <b>건드리지 않는다</b> — Unity 가 구버전 에셋을 읽을 때와 같다
                //   (필드 초기화자 또는 default 가 그대로 남는다).
                if (!map.TryGetValue(f.Name, out Node n)) continue;
                if (TryConvert(n, f.FieldType, out object v)) f.SetValue(target, v);
            }
        }

        /// <summary>큰따옴표 스칼라가 이 줄에서 닫혔는가. 닫는 따옴표 앞의 역슬래시 수가 짝수여야
        /// 진짜 닫는 따옴표다(<c>\"</c> 는 문자, <c>\\"</c> 는 닫힘).</summary>
        private static bool ClosesQuoted(string s)
        {
            if (s.Length < 2 || s[s.Length - 1] != '"') return false;
            int back = 0;
            for (int k = s.Length - 2; k >= 0 && s[k] == '\\'; k--) back++;
            return back % 2 == 0;
        }

        private static readonly Regex Inline = new Regex(@"([A-Za-z_][A-Za-z0-9_]*):\s*([^,}]+)");

        private static bool TryConvert(Node n, Type t, out object value)
        {
            value = null;
            if (n == null) return false;

            if (t.IsArray)
            {
                List<Node> items = n.List ?? new List<Node>();
                Type et = t.GetElementType();
                Array arr = Array.CreateInstance(et, items.Count);
                for (int k = 0; k < items.Count; k++)
                {
                    if (TryConvert(items[k], et, out object ev)) arr.SetValue(ev, k);
                }
                value = arr;
                return true;
            }

            if (t == typeof(Color))
            {
                var c = new Color(0f, 0f, 0f, 1f);
                string s = n.Scalar ?? string.Empty;
                foreach (Match m in Inline.Matches(s))
                {
                    float f = ParseFloat(m.Groups[2].Value);
                    switch (m.Groups[1].Value)
                    {
                        case "r": c.r = f; break;
                        case "g": c.g = f; break;
                        case "b": c.b = f; break;
                        case "a": c.a = f; break;
                    }
                }
                value = c;
                return true;
            }

            if (n.Map != null)                     // 중첩 구조체
            {
                object nested = Activator.CreateInstance(t);
                Bind(nested, t, n.Map);
                value = nested;
                return true;
            }

            string raw = n.Scalar;
            if (raw == null) return false;

            if (t == typeof(string)) { value = Unescape(raw); return true; }
            if (t == typeof(bool)) { value = raw != "0"; return true; }
            if (t == typeof(int)) { value = (int)ParseFloat(raw); return true; }
            if (t == typeof(byte)) { value = (byte)ParseFloat(raw); return true; }
            if (t == typeof(float)) { value = ParseFloat(raw); return true; }
            if (t.IsEnum) { value = Enum.ToObject(t, (int)ParseFloat(raw)); return true; }
            return false;
        }

        private static float ParseFloat(string s)
            => float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : 0f;

        /// <summary>★ Unity 는 비ASCII 를 <c>"\uXXXX"</c> 로 적는다 — 이 저장소가 여러 번 속은 자리다
        /// (`.asset` 에 한글을 grep 하면 영원히 0건이다).</summary>
        private static string Unescape(string s)
        {
            s = s.Trim();
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"') s = s.Substring(1, s.Length - 2);

            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 5 < s.Length + 1 && i + 1 < s.Length && s[i + 1] == 'u' && i + 5 < s.Length + 1
                    && i + 6 <= s.Length
                    && int.TryParse(s.Substring(i + 2, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int cp))
                {
                    sb.Append((char)cp);
                    i += 5;
                }
                else
                {
                    sb.Append(s[i]);
                }
            }
            return sb.ToString();
        }
    }
}
