using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Core;

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
        /// <summary>이 앱이 안내하는 동작키 전부(문자 10개 + 쉼표). 표기 규칙은 키 종류와 무관해야 한다.</summary>
        private static readonly string[] Keys =
        {
            "A", "B", "C", "D", "F", "G", "H", "I", "J", "K", "N", "Q", "R", "S", "T", "V", "X", ",",
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
    }
}
