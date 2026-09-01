using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 단축키 표기의 플랫폼 패리티 — 2026-09-01 (Windows 패리티 감사 C3 해결).
    ///
    /// ============================================================================
    /// 이 파일이 잡으려는 실패
    /// ============================================================================
    /// 보관함 카드의 상태 슬롯과 정보창/설정창 안내가 <c>"⌃⌥⌘A"</c>로 하드코딩돼 있었다.
    /// Windows에서 이 앱의 조합은 <c>Ctrl+Alt+Win+A</c>이므로(<c>Win32WindowService</c>가
    /// <c>GlobalKey.Command</c>를 Windows 키로 읽는다), Windows 사용자는 <b>존재하지 않는 조합</b>을
    /// 안내받고 있었다. 게다가 <c>ItemCatalogTests</c>가 그 리터럴을 단언으로 <b>잠그고</b> 있어서,
    /// 고치려면 테스트부터 손봐야 하는 구조였다.
    ///
    /// ============================================================================
    /// ★ 그래서 이 파일은 <b>문자열을 다시 적지 않는다</b>
    /// ============================================================================
    /// 테스트가 프로덕션 문자열을 베끼면 단일 정의처가 둘이 된다 — 표기를 바꾸는 날 두 곳을 고쳐야 하고,
    /// 그 순간 테스트는 "옳은가"가 아니라 "같은가"만 확인하는 사본이 된다(이 저장소가 골든 파일에서
    /// 의도적으로 감수하는 비용이지만, 값이 <b>계산 가능한</b> 여기서는 감수할 이유가 없다).
    /// 대신 <b>성질</b>을 단언한다:
    /// <list type="bullet">
    ///   <item>Windows 표기에는 macOS 글리프가 <b>한 글자도</b> 없다(이번 결함의 정의 그 자체).</item>
    ///   <item>macOS 표기에는 라틴 문자가 <b>한 글자도</b> 없다(글리프만으로 조합키를 말한다).</item>
    ///   <item>두 표기 다 동작키로 끝나고, 조합키 접두사로 시작한다.</item>
    ///   <item>지금 빌드가 고른 표기는 <b>호스트 플랫폼과 일치</b>한다.</item>
    /// </list>
    /// 역사적 사실 하나만 <b>네거티브 컨트롤</b>로 박제한다(아래 참고).
    /// </summary>
    public sealed class ShortcutLabelParityTests
    {
        /// <summary>이 앱이 안내하는 동작키 전부. 표기 규칙은 키 종류와 무관해야 한다.
        /// <para><c>V</c>는 아직 배선 대기(설정창 [일반] 숨기기/보이기)라 <see cref="GlobalKey"/>에
        /// 없지만 화면에는 안내되므로 여기 있다. <b>2026-09-01 <c>","</c>가 <c>"P"</c>로 바뀌었다</b> —
        /// 아래 "OS가 예약한 조합" 절 참고.</para></summary>
        private static readonly string[] Keys =
        {
            "A", "B", "C", "D", "F", "G", "H", "I", "J", "K", "N", "P", "Q", "R", "S", "T", "V", "X",
        };

        private static bool IsLatinLetter(char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');

        // ============================================================================
        // 1. 두 표기가 각자 자기 세계의 말을 한다
        // ============================================================================

        [Test]
        public void Windows_표기에는_macOS_글리프가_하나도_없다()
        {
            foreach (string key in Keys)
            {
                string label = ShortcutLabel.WindowsChord(key);
                foreach (char c in label)
                {
                    Assert.Less(c, (char)0x2300,
                        $"Windows 표기 '{label}'에 기호 문자 U+{(int)c:X4}가 섞여 있습니다 — " +
                        "Windows 키보드에는 ⌃⌥⌘ 각인이 없으므로 사용자가 무엇을 눌러야 하는지 알 수 없습니다. " +
                        "이것이 C3 결함의 정의 그 자체입니다.");
                }
            }
        }

        [Test]
        public void macOS_표기의_조합키는_글리프로만_되어_있다()
        {
            foreach (char c in ShortcutLabel.MacModifiers)
            {
                Assert.IsFalse(IsLatinLetter(c),
                    $"macOS 조합키 표기에 라틴 문자 '{c}'가 있습니다 — Apple HIG 관례상 조합키는 " +
                    "글리프로 적습니다. 여기가 무너지면 macOS 사용자에게 보이던 문구가 바뀝니다.");
            }
            Assert.AreEqual(3, ShortcutLabel.MacModifiers.Length,
                "macOS 조합키가 3개(Control·Option·Command)가 아닙니다 — 이 앱은 조합키 3개를 강제합니다.");
        }

        [Test]
        public void Windows_표기는_세_조합키를_이름으로_말하고_더하기로_잇는다()
        {
            string mods = ShortcutLabel.WindowsModifiers;
            Assert.IsTrue(mods.EndsWith("+"),
                $"Windows 조합키 표기 '{mods}'가 '+'로 끝나지 않습니다 — 동작키가 바로 뒤에 붙습니다.");

            string[] parts = mods.TrimEnd('+').Split('+');
            Assert.AreEqual(3, parts.Length,
                $"Windows 조합키가 {parts.Length}개입니다 — macOS의 ⌃⌥⌘ 3개와 하나씩 대응해야 합니다.");
            foreach (string part in parts)
            {
                Assert.IsNotEmpty(part, $"Windows 조합키 표기 '{mods}'에 빈 항목이 있습니다.");
            }
        }

        // ============================================================================
        // 2. 조립 규칙 — 어느 표기든 "조합키 + 동작키"다
        // ============================================================================

        [Test]
        public void 두_표기_모두_조합키로_시작하고_동작키로_끝난다()
        {
            foreach (string key in Keys)
            {
                string mac = ShortcutLabel.MacChord(key);
                string win = ShortcutLabel.WindowsChord(key);

                StringAssert.StartsWith(ShortcutLabel.MacModifiers, mac, $"macOS 표기: {mac}");
                StringAssert.StartsWith(ShortcutLabel.WindowsModifiers, win, $"Windows 표기: {win}");
                StringAssert.EndsWith(key, mac, $"macOS 표기가 동작키로 끝나지 않습니다: {mac}");
                StringAssert.EndsWith(key, win, $"Windows 표기가 동작키로 끝나지 않습니다: {win}");
            }
        }

        [Test]
        public void 서로_다른_동작키는_서로_다른_표기를_낳는다()
        {
            var seen = new HashSet<string>();
            foreach (string key in Keys)
            {
                Assert.IsTrue(seen.Add(ShortcutLabel.Chord(key)),
                    $"동작키 '{key}'가 앞의 어떤 키와 같은 표기를 냅니다 — 보관함에서 두 행동이 " +
                    "같은 단축키를 안내하게 됩니다.");
            }
            Assert.AreEqual(Keys.Length, seen.Count);
        }

        // ============================================================================
        // 3. 지금 빌드가 고른 표기가 호스트와 맞는가
        // ============================================================================

        [Test]
        public void 지금_빌드는_호스트_플랫폼의_표기를_고른다()
        {
            foreach (string key in Keys)
            {
                string expected = ShortcutLabel.HostUsesWindowsNotation
                    ? ShortcutLabel.WindowsChord(key)
                    : ShortcutLabel.MacChord(key);

                Assert.AreEqual(expected, ShortcutLabel.Chord(key),
                    "Chord()가 호스트 플랫폼과 다른 표를 골랐습니다 — 컴파일 분기가 어긋났습니다.");
            }

            // 이 검사가 "양쪽 다 같은 값"이라 공허해지는 것을 막는다.
            Assert.AreNotEqual(ShortcutLabel.MacChord("A"), ShortcutLabel.WindowsChord("A"),
                "두 플랫폼 표기가 같습니다 — 그렇다면 위 분기는 아무것도 고르고 있지 않습니다.");
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — 옛 하드코딩은 <b>macOS에서는 옳고 Windows에서만 틀렸다</b>.
        ///
        /// <para>박제하는 것은 둘이다: (a) 2026-09-01 이전 <c>ItemCatalog.cs</c>에 실제로 있던 리터럴,
        /// (b) 그것이 <b>macOS 표기였다</b>는 사실. 둘 다 얼려야 이 컨트롤이 뜻을 갖는다 —
        /// 하나만 얼리면 훗날 macOS 표기가 바뀌었을 때 "역사상 존재한 적 없는 쌍"을 재게 된다
        /// (2026-09-01 펜던트 컨트롤이 실제로 겪은 실패).</para>
        ///
        /// <para>그래서 이 컨트롤은 두 가지를 동시에 증명한다:
        /// <b>고침이 macOS 사용자가 보던 문구를 한 글자도 바꾸지 않았다</b>(회귀 없음)는 것과,
        /// <b>같은 문구가 Windows에서는 틀린 조합이었다</b>(결함이 실재했다)는 것.</para>
        /// </summary>
        [Test]
        public void 컨트롤_옛_하드코딩은_macOS에서만_옳았다()
        {
            const string oldHardcodedArcheryLabel = "⌃⌥⌘A";

            Assert.AreEqual(oldHardcodedArcheryLabel, ShortcutLabel.MacChord("A"),
                "macOS 표기가 옛 하드코딩과 달라졌습니다 — 이 고침은 Windows 표기를 <b>추가</b>하는 것이지 " +
                "macOS 사용자가 보던 문구를 바꾸는 것이 아닙니다. 바뀌었다면 회귀입니다.");

            Assert.AreNotEqual(oldHardcodedArcheryLabel, ShortcutLabel.WindowsChord("A"),
                "Windows 표기가 옛 하드코딩과 같습니다 — 그렇다면 C3 결함이 그대로 살아 있습니다.");
        }

        // ============================================================================
        // ★ 4. OS가 이미 가져간 조합을 우리가 안내하고 있지는 않은가 (2026-09-01 신설)
        // ============================================================================

        /// <summary>
        /// <b>이 파일에 뚫려 있던 구멍.</b> 위 검사들은 "표기가 플랫폼마다 옳은 말을 하는가"만 본다.
        /// <b>그 조합을 눌러도 되는가</b>는 아무도 보지 않았고, 정확히 그 틈으로 결함이 들어왔다.
        ///
        /// <para><b>사고</b>: 설정창 단축키가 <c>⌃⌥⌘,</c>였다. 그건 macOS 접근성 시스템 단축키
        /// <b>"대비 줄이기"</b>(symbolic hotkey <b>26</b>)다. 키 한 번에 두 가지 일이 일어났고,
        /// 창을 열고 닫는 왕복 2회 누름마다 <c>com.apple.universalaccess</c>의 <c>contrast</c>가
        /// 0.10씩 <b>실제로</b> 내려갔다 — 불변 원칙 2(비침해)·3(유저 자산 불변) 위반이며, 하필
        /// <b>대비를 조절해 쓰는 접근성 사용자만</b> 골라서 맞는다. 2026-09-01 <c>P</c>로 옮겼다.</para>
        ///
        /// <para><b>재현 절차</b>(이 단언이 무엇을 막는지 손으로 확인할 때):
        /// <code>
        /// defaults read com.apple.universalaccess contrast   # 키 없음(=0)
        /// # ⌃⌥⌘. 주입  -> contrast = 0.05   (OS 훅이 살아 있다는 증명)
        /// # ⌃⌥⌘, 주입  -> contrast = 0      AND 설정창도 토글된다  ← 두 가지 일이 함께 일어난다
        /// # ⌃⌥⌘P 주입  -> contrast 불변     AND 설정창만 토글된다  ← 고침 후 기대값
        /// </code></para>
        ///
        /// <para><b>금지 목록을 여기에 숫자로 베끼지 않는다</b>(CLAUDE.md). 목록의 단일 정의처는
        /// 프로덕션의 <see cref="ShortcutLabel.MacReservedActionKeys"/> /
        /// <see cref="ShortcutLabel.WindowsReservedActionKeys"/>이고, 근거(symbolic hotkey ID
        /// 21/25/26)와 "왜 이 세 글자뿐인가"도 거기 적혀 있다. 새 예약이 발견되면 그쪽에 한 줄
        /// 추가하는 것만으로 이 검사가 곧바로 적용된다.</para>
        /// </summary>
        [Test]
        public void 안내하는_동작키는_OS가_예약한_조합이_아니다()
        {
            AssertNoneReserved(Keys, "안내 문구가 쓰는 동작키");
        }

        /// <summary>
        /// ★ 위 검사의 <b>공허함 방지</b> — 표기 목록(<see cref="Keys"/>)은 테스트가 들고 있는
        /// 사본이라, 실제 배선이 예약 조합으로 돌아가도 위 검사만으로는 초록일 수 있다. 그래서
        /// <b>실제로 폴링되는 키</b>(<see cref="GlobalKey"/> 열거형 그 자체)도 같은 잣대로 본다.
        ///
        /// <para>조합키 3개(<c>Command</c>/<c>Option</c>/<c>Control</c>)는 동작키가 아니므로 뺀다.
        /// 나머지는 전부 한 글자 이름이며, <b>그 이름이 곧 사용자가 누르는 글자</b>다 — 이름이
        /// 여러 글자인 항목이 생기면(옛 <c>Comma</c>가 그랬다) 그 글자를 알 수 없으므로 여기서
        /// 실패시킨다. 명시적 매핑 없이 조용히 검사 밖으로 빠져나가는 항목을 만들지 않기 위해서다.</para>
        /// </summary>
        [Test]
        public void 실제_배선된_전역키도_OS가_예약한_조합이_아니다()
        {
            var actionKeys = new List<string>();
            foreach (GlobalKey key in (GlobalKey[])System.Enum.GetValues(typeof(GlobalKey)))
            {
                if (key == GlobalKey.Command || key == GlobalKey.Option || key == GlobalKey.Control) continue;

                string name = key.ToString();
                Assert.AreEqual(1, name.Length,
                    $"GlobalKey.{name}은 이름이 한 글자가 아닙니다 — 사용자가 실제로 누르는 글자를 " +
                    "이 검사가 알 수 없어 예약 조합 판정에서 조용히 빠져나갑니다. 옛 GlobalKey.Comma가 " +
                    "정확히 그런 항목이었고, 그 사각지대에서 ⌃⌥⌘,(macOS 대비 줄이기) 충돌이 살아남았습니다. " +
                    "여러 글자 이름이 필요하면 ShortcutLabel에 이름→글자 매핑을 두고 이 검사에 물리세요.");
                actionKeys.Add(name);
            }

            Assert.IsNotEmpty(actionKeys, "GlobalKey에 동작키가 하나도 없습니다 — 검사가 공허합니다.");
            AssertNoneReserved(actionKeys, "실제 배선된 GlobalKey");
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — <b>이 검사가 실제로 결함을 잡을 수 있는가.</b>
        /// 금지 목록이 비거나 비교가 헛돌면 위 두 검사는 아무것도 안 하면서 초록이다. 그래서
        /// <b>2026-09-01 이전에 실제로 출하돼 있던 동작키</b>(<c>","</c>)를 넣어 보고 잡히는지 본다.
        /// </summary>
        [Test]
        public void 컨트롤_옛_설정창_단축키는_이_검사에_걸린다()
        {
            const string shippedActionKeyBeforeFix = ",";

            CollectionAssert.Contains(ShortcutLabel.MacReservedActionKeys, shippedActionKeyBeforeFix,
                "금지 목록에서 쉼표가 빠졌습니다 — 그렇다면 위 검사들은 이 저장소가 실제로 겪은 결함조차 " +
                "잡지 못합니다(⌃⌥⌘, = macOS 접근성 '대비 줄이기', symbolic hotkey 26).");

            CollectionAssert.DoesNotContain(Keys, shippedActionKeyBeforeFix,
                "안내 문구가 아직 쉼표를 동작키로 쓰고 있습니다 — 고침이 되돌아갔습니다.");

            // 이 검사의 잣대가 "무엇이든 다 금지"가 아님을 함께 보인다(과잉 금지 방지).
            CollectionAssert.DoesNotContain(ShortcutLabel.MacReservedActionKeys, "P",
                "P가 금지 목록에 들어가 있습니다 — 지금 설정창이 쓰는 글자입니다. 실측상 ⌃⌥⌘ 마스크에 " +
                "macOS가 예약한 것은 8 / , / . 셋뿐입니다.");
        }

        /// <summary>플랫폼 양쪽 금지 목록을 <b>같은 코드 경로로</b> 검사한다 — 한쪽만 도는 감사는
        /// 반대쪽 갭이 조용히 쌓이는 이 저장소의 단골 실패다(CLAUDE.md 플랫폼 동시 검토).</summary>
        private static void AssertNoneReserved(IEnumerable<string> actionKeys, string what)
        {
            var hits = new List<string>();
            foreach (string key in actionKeys)
            {
                foreach (string bad in ShortcutLabel.MacReservedActionKeys)
                {
                    if (key == bad) hits.Add($"  · macOS — {ShortcutLabel.MacChord(key)} ({what})");
                }
                foreach (string bad in ShortcutLabel.WindowsReservedActionKeys)
                {
                    if (key == bad) hits.Add($"  · Windows — {ShortcutLabel.WindowsChord(key)} ({what})");
                }
            }

            Assert.IsEmpty(hits,
                "OS가 이미 예약한 조합을 이 앱의 단축키로 쓰고 있습니다 — 사용자가 한 번 누르면 " +
                "두 가지 일이 일어나고, 그중 하나는 우리가 통제하지 못하는 OS 설정 변경입니다 " +
                "(불변 원칙 2 비침해 / 3 유저 자산 불변):\n" + string.Join("\n", hits) +
                "\n금지 목록과 근거는 Core/ShortcutLabel.MacReservedActionKeys 문서를 보세요.");
        }
    }
}
