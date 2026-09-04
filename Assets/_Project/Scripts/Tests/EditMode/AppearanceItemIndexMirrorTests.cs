using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StickMate.Interaction;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-02 qa-regression — <b>PlayMode 테스트가 베껴 적은 아이템 번호</b>가 프로덕션과
    /// 갈라지지 않게 잠근다.
    ///
    /// <para><b>왜 이런 이상한 검사가 필요한가.</b>
    /// <c>Tests/PlayMode/AppearanceNewItemsRenderTests.cs</c>는 <c>FxBubble = 4</c> 같은 값을
    /// 손으로 적어 두고, 주석에 "어긋나면 착용 단언이 즉시 빨개진다"고 <b>거짓 안전 주장</b>을
    /// 달고 있었다. 실제로는 빨개지지 않는다 — 그 단언은
    /// <c>Assert.IsTrue(Wear(slot, index))</c>이고 <c>Wear</c>는 <b>범위 밖 번호</b>에만 false를
    /// 준다. 프로덕션이 번호를 재배치해 4번이 다른 아이템이 되면 <c>Wear</c>는 여전히 true이고,
    /// 그 PlayMode 테스트는 <b>엉뚱한 아이템을 착용한 채 초록</b>이 된다. 사본이 어긋나는 바로 그
    /// 경우가 그 단언의 사각지대다. (<c>Wear</c>는 <b>거부</b> — 범위 밖·미해금 — 은 잡는다.
    /// 못 잡는 것은 <b>번호가 다른 아이템을 가리키게 된 것</b>이다.)</para>
    ///
    /// <para><b>왜 사본을 그냥 없애지 않았나.</b> 없앨 수 없다. <c>Scripts/AssemblyInfo.cs</c>의
    /// <c>InternalsVisibleTo</c>는 <b><c>StickMate.Tests.EditMode</c> 하나뿐</b>이고,
    /// <c>AppearanceShapeBuilder</c>의 번호 상수는 전부 <c>internal</c>이다. 즉 PlayMode 어셈블리는
    /// 그 상수를 <b>물리적으로 참조할 수 없다</b>. (프로덕션 <c>.cs</c>를 고쳐
    /// <c>InternalsVisibleTo("StickMate.Tests.PlayMode")</c>를 추가하면 사본 자체가 사라지지만,
    /// 그것은 프로덕션 변경이라 리더 배정이 필요하다 — 그때까지의 다리가 이 파일이다.)</para>
    ///
    /// <para>그래서 EditMode(= internal이 보이는 쪽)에서 <b>그 파일의 소스 텍스트를 읽어</b>
    /// 프로덕션 상수와 대조한다. 어긋나면 여기가 빨개진다.</para>
    ///
    /// ============================================================================
    /// ★★ 2026-09-03 (개선 R2) — <b>거울 대장을 문자열까지 넓혔다</b>
    /// ============================================================================
    /// <para>이 대장은 <c>const int</c>만 대조했다. 그런데 같은 사본 파일에
    /// <c>private const string PetContainerName = "CharacterPet";</c>가 <b>거울 잠금 없이</b>
    /// 앉아 있었다 — <c>test-engineer</c> 자기 신고 6번.</para>
    ///
    /// <para>CLAUDE.md 협업 프로토콜은 2026-09-02에 이 조항을 <i>"상수를 <b>숫자로</b>"</i>에서
    /// <i>"상수·식별자를 <b>문자열로</b>"</i>로 넓혔다. 넓힌 이유가 실제 사고였다:
    /// <c>UiInteractionFramePacingHoldTests.cs</c>가 <c>"_agent.IsSuspended"</c>를 니들로
    /// 하드코딩했는데 프로덕션이 <c>ArePanelsSuppressed</c>로 넓어지자 <c>IndexOf = -1</c>이 되어
    /// <b>거짓 빨강</b>을 냈다. <b>규칙만 넓히고 대장을 안 넓히면 조항이 도는 곳이 없다.</b></para>
    ///
    /// <para><b>이 문자열은 어느 쪽 위험인가.</b> <c>PetContainerName</c>은 <b>존재 단언</b>에 쓰인다
    /// (<c>Assert.AreEqual(1, namedPetRoots)</c>). 그래서 이름이 갈라지면 0개로 <b>시끄럽게</b>
    /// 빨개지긴 한다 — 조용히 초록이 되는 부재 단언보다는 낫다. 문제는 <b>그 빨강을 읽는 사람이
    /// 무엇이 깨졌는지 모른다</b>는 것이다. 펫이 안 생긴 것인지, 이름이 바뀐 것인지가 구분되지
    /// 않는다. 이 거울은 <b>후자를 EditMode에서 먼저, 정확한 문장으로</b> 잡는다.</para>
    ///
    /// <para><b>기대값의 출처(TEAM.md "거짓 통과 신형").</b> 기대값을 여기에 <c>"CharacterPet"</c>이라고
    /// 또 적으면 사본이 <b>세 벌</b>이 될 뿐이다. 그래서 기대값을 <b>프로덕션 소스에서 뽑는다</b> —
    /// <c>Interaction/CharacterPetRenderer.cs</c>의 <c>new GameObject("…")</c> 리터럴.
    /// 대조의 양쪽이 <b>서로 다른 파일 · 서로 다른 추출식</b>이라 한쪽이 움직이면 반드시 갈라진다.
    /// (프로덕션에 <c>const</c>가 없어 소스 텍스트로 가는 것이지, 그것이 이상적이라서가 아니다 —
    /// <c>Interaction/</c>은 이 라운드의 편집 금지 대상이다.)</para>
    ///
    /// <para><b>양성 대조 필수.</b> 이 검사의 결론은 "어긋난 것이 없다"라는 <b>부재 판정</b>이고,
    /// 이 저장소가 부재 판정으로 당한 거짓 통과가 한둘이 아니다(정규식이 아무것도 못 찾으면
    /// <c>foreach</c>가 0바퀴 돌고 초록이 된다). 그래서 대장이 비지 않았는지,
    /// 파서가 실제로 값을 뽑았는지, 그리고 <b>일부러 틀린 값</b>이 실제로 걸리는지를
    /// 같은 파일에서 증명한다.</para>
    /// </summary>
    public sealed class AppearanceItemIndexMirrorTests
    {
        private const string LogPrefix = "[번호거울]";

        private static string MirrorSourcePath => Path.Combine(
            Application.dataPath, "_Project", "Scripts", "Tests", "PlayMode",
            "AppearanceNewItemsRenderTests.cs");

        /// <summary>PlayMode 테스트가 베낀 이름 → 프로덕션의 진짜 값.
        /// <b>비면 안 된다</b> — 비는 순간 아래 <c>foreach</c>가 아무것도 재지 않는다.</summary>
        private static IReadOnlyDictionary<string, int> MirroredIndices => new Dictionary<string, int>
        {
            { "FxNone", AppearanceShapeBuilder.FxNone },
            { "FxBubble", AppearanceShapeBuilder.FxBubble },
            { "FxLeaf", AppearanceShapeBuilder.FxLeaf },
            { "PetBalloon", AppearanceShapeBuilder.PetBalloon },
            { "PetSnail", AppearanceShapeBuilder.PetSnail },
        };

        /// <summary><c>private const int NAME = N;</c>에서 N을 뽑는다. 못 찾으면 false —
        /// 못 찾은 것을 "일치"로 세지 않는 것이 이 파서의 유일한 임무다.</summary>
        private static bool TryReadIntConst(string source, string name, out int value)
        {
            Match m = Regex.Match(source,
                @"const\s+int\s+" + Regex.Escape(name) + @"\s*=\s*(-?\d+)\s*;");
            if (!m.Success) { value = 0; return false; }
            return int.TryParse(m.Groups[1].Value, out value);
        }

        [Test]
        public void PlayMode가_베낀_아이템_번호가_프로덕션과_같다()
        {
            Assert.IsTrue(File.Exists(MirrorSourcePath),
                $"{LogPrefix} 거울 대상 소스를 찾지 못했습니다: {MirrorSourcePath}. " +
                "파일이 옮겨졌다면 이 검사의 경로를 함께 옮기세요 — 경로가 썩으면 이 검사는 " +
                "아무것도 재지 않으면서 초록이 될 수 있습니다.");

            string src = File.ReadAllText(MirrorSourcePath);
            IReadOnlyDictionary<string, int> expected = MirroredIndices;

            // 대장이 비면 아래 루프가 0바퀴 돈다. 빈 대장을 기대값으로 명시한다(거짓 통과 유형 5).
            Assert.Greater(expected.Count, 0,
                $"{LogPrefix} 거울 대장이 비었습니다 — 검사가 아무것도 재지 않습니다.");

            var mismatched = new List<string>();
            var missing = new List<string>();
            int compared = 0;

            foreach (KeyValuePair<string, int> kv in expected)
            {
                if (!TryReadIntConst(src, kv.Key, out int mirrored))
                {
                    missing.Add(kv.Key);
                    continue;
                }

                compared++;
                if (mirrored != kv.Value) mismatched.Add($"{kv.Key}: 사본={mirrored} 프로덕션={kv.Value}");
            }

            Assert.IsEmpty(missing,
                $"{LogPrefix} 사본에서 상수를 찾지 못했습니다: {string.Join(", ", missing)}. " +
                "이름이 바뀌었거나 지워졌습니다. 지워졌다면(= InternalsVisibleTo로 직접 참조하게 " +
                "바뀌었다면) 이 검사의 대장에서도 그 항목을 빼세요.");

            Assert.AreEqual(expected.Count, compared,
                $"{LogPrefix} 대장 {expected.Count}건 중 {compared}건만 실제로 비교됐습니다.");

            Assert.IsEmpty(mismatched,
                $"{LogPrefix} PlayMode 테스트가 베낀 번호가 프로덕션과 갈라졌습니다:\n  " +
                string.Join("\n  ", mismatched) + "\n" +
                "그 파일의 착용 단언(Assert.IsTrue(Wear(...)))은 이 어긋남을 <b>잡지 못합니다</b> — " +
                "Wear는 범위 밖 번호에만 false를 주므로, 재배치된 번호로는 <b>엉뚱한 아이템을 " +
                "착용한 채 초록</b>이 됩니다. 사본 값을 프로덕션에 맞추세요.");
        }

        /// <summary>★ 양성 대조 — 파서가 실제로 값을 뽑는가, 그리고 <b>틀린 값을 틀렸다고 하는가</b>.
        /// 이것이 빨간불이면 위 검사의 초록은 "일치"가 아니라 "아무것도 못 읽음"이다.</summary>
        [Test]
        public void 양성_대조_파서가_값을_읽고_틀린_값을_잡아낸다()
        {
            string src = File.ReadAllText(MirrorSourcePath);

            Assert.IsTrue(TryReadIntConst(src, "FxBubble", out int bubble),
                $"{LogPrefix} 파서가 FxBubble을 읽지 못했습니다 — 위 검사는 비교를 한 적이 없습니다.");
            Assert.AreEqual(AppearanceShapeBuilder.FxBubble, bubble,
                $"{LogPrefix} 양성 대조 자체가 어긋났습니다.");

            // 음성 대조 — 존재하지 않는 이름은 "일치"가 아니라 "못 찾음"으로 떨어져야 한다.
            Assert.IsFalse(TryReadIntConst(src, "존재하지않는상수이름12345", out _),
                $"{LogPrefix} 없는 상수를 찾았다고 보고했습니다 — 파서가 아무 숫자나 집어오고 있습니다.");

            // 음성 대조 — 대조 로직이 실제로 불일치를 구분하는가(같은 자를 뒤집어 확인).
            Assert.AreNotEqual(AppearanceShapeBuilder.FxBubble, AppearanceShapeBuilder.FxLeaf,
                $"{LogPrefix} 두 아이템 번호가 같습니다 — 번호로 아이템을 가르는 전제가 무너집니다.");
        }

        // ============================================================================
        // ★★ 문자열 거울 (2026-09-03, 개선 R2 항목 6)
        // ============================================================================

        /// <summary>프로덕션 쪽 원본이 사는 파일. <b>이 파일을 옮기면 아래 검사가 실패한다</b> —
        /// 경로가 썩은 채 초록이 되지 않게 <c>File.Exists</c>를 먼저 단언한다.</summary>
        private static string PetRendererSourcePath => Path.Combine(
            Application.dataPath, "_Project", "Scripts", "Interaction", "CharacterPetRenderer.cs");

        /// <summary>
        /// 문자열 거울 대장. <b>키</b> = 사본 파일의 <c>const string</c> 이름,
        /// <b>값</b> = 그 기대값을 <b>프로덕션 소스에서 뽑는 함수</b>.
        ///
        /// <para>★ 값 자리에 문자열 리터럴을 적지 마라 — 그러면 사본이 한 벌 더 늘 뿐이고,
        /// 프로덕션이 바뀌어도 이 대장과 사본이 <b>사이좋게 같이 틀린다</b>
        /// (docs/TEAM.md: 생성기와 검사기가 같은 함정에 같이 빠진다).</para>
        /// </summary>
        private static IReadOnlyDictionary<string, Func<string>> MirroredStrings =>
            new Dictionary<string, Func<string>>
            {
                { "PetContainerName", ReadPetContainerNameFromProduction },
            };

        /// <summary>
        /// <b>컨테이너 필드에 대입되는</b> <c>new GameObject("…")</c> 리터럴을 프로덕션 소스에서 뽑는다.
        ///
        /// <para>★ 처음에는 <c>new GameObject("…")</c> 전체를 훑었는데 <b>실측 2건</b>이 나왔다 —
        /// <c>"CharacterPet"</c>(컨테이너)와 <c>"Body"</c>(자식). 둘 중 아무거나 고르면 그것이 곧
        /// 거짓 통과이므로, <c>_container =</c> 대입까지 포함해 <b>자리를 특정</b>한다.</para>
        ///
        /// <para><b>정확히 1건</b>이라야 한다. 0건이면 프로덕션이 그 형태를 버린 것이고(필드명 변경 ·
        /// 팩토리로 이동 등), 그때 이 검사는 <b>조용히 통과하는 대신 시끄럽게 실패</b>한다 —
        /// 이것이 이 저장소가 반복해 온 "죽은 니들이 0건을 깨끗으로 읽는" 형태를 막는 자리다.</para>
        /// </summary>
        private static string ReadPetContainerNameFromProduction()
        {
            Assert.IsTrue(File.Exists(PetRendererSourcePath),
                $"{LogPrefix} 프로덕션 소스를 찾지 못했습니다: {PetRendererSourcePath}. " +
                "파일이 옮겨졌다면 이 경로를 함께 옮기세요.");

            string src = File.ReadAllText(PetRendererSourcePath);
            MatchCollection ms = Regex.Matches(src,
                @"_container\s*=\s*new\s+GameObject\s*\(\s*""([^""]+)""\s*\)");

            Assert.AreEqual(1, ms.Count,
                $"{LogPrefix} CharacterPetRenderer.cs에서 `_container = new GameObject(\"…\")` 형태가 " +
                $"{ms.Count}개입니다 — 정확히 1개여야 이 거울이 '어느 이름이 펫 컨테이너인가'를 " +
                "판정할 수 있습니다.\n" +
                "  · 0개: 프로덕션이 그 형태를 버렸습니다(필드명 변경 / 팩토리로 이동). 추출식을 " +
                "그 자리에 맞추세요 — 여기서 멈추는 것이 맞습니다.\n" +
                "  · 2개 이상: 어느 것이 컨테이너인지 이 검사가 고를 수 없습니다. 아무거나 고르면 " +
                "그게 거짓 통과입니다.");

            string name = ms[0].Groups[1].Value;
            Assert.IsNotEmpty(name,
                $"{LogPrefix} 프로덕션에서 뽑은 컨테이너 이름이 빈 문자열입니다 — 추출이 실패한 것을 " +
                "'일치'로 세면 안 됩니다(빈 값끼리 비교해 초록이 되는 형태).");
            return name;
        }

        /// <summary><c>const string NAME = "…";</c>에서 리터럴을 뽑는다. 못 찾으면 false —
        /// <see cref="TryReadIntConst"/>와 같은 계약이다(못 찾은 것을 "일치"로 세지 않는다).</summary>
        private static bool TryReadStringConst(string source, string name, out string value)
        {
            Match m = Regex.Match(source,
                @"const\s+string\s+" + Regex.Escape(name) + @"\s*=\s*""([^""]*)""\s*;");
            if (!m.Success) { value = null; return false; }
            value = m.Groups[1].Value;
            return true;
        }

        [Test]
        public void PlayMode가_베낀_식별자_문자열이_프로덕션과_같다()
        {
            Assert.IsTrue(File.Exists(MirrorSourcePath),
                $"{LogPrefix} 거울 대상 소스를 찾지 못했습니다: {MirrorSourcePath}.");

            string src = File.ReadAllText(MirrorSourcePath);
            IReadOnlyDictionary<string, Func<string>> expected = MirroredStrings;

            Assert.Greater(expected.Count, 0,
                $"{LogPrefix} 문자열 거울 대장이 비었습니다 — 검사가 아무것도 재지 않습니다(거짓 통과 유형 5).");

            var mismatched = new List<string>();
            var missing = new List<string>();
            int compared = 0;

            foreach (KeyValuePair<string, Func<string>> kv in expected)
            {
                if (!TryReadStringConst(src, kv.Key, out string mirrored))
                {
                    missing.Add(kv.Key);
                    continue;
                }

                compared++;
                string production = kv.Value();
                if (!string.Equals(mirrored, production, StringComparison.Ordinal))
                {
                    mismatched.Add($"{kv.Key}: 사본=\"{mirrored}\" 프로덕션=\"{production}\"");
                }
            }

            Assert.IsEmpty(missing,
                $"{LogPrefix} 사본에서 문자열 상수를 찾지 못했습니다: {string.Join(", ", missing)}. " +
                "이름이 바뀌었거나 지워졌습니다. 지워졌다면 이 대장에서도 그 항목을 빼세요.");

            Assert.AreEqual(expected.Count, compared,
                $"{LogPrefix} 문자열 대장 {expected.Count}건 중 {compared}건만 실제로 비교됐습니다.");

            Assert.IsEmpty(mismatched,
                $"{LogPrefix} PlayMode 테스트가 베낀 <b>식별자 문자열</b>이 프로덕션과 갈라졌습니다:\n  " +
                string.Join("\n  ", mismatched) + "\n" +
                "그 파일의 존재 단언(Assert.AreEqual(1, namedPetRoots))은 이 어긋남을 <b>0개</b>로 " +
                "빨갛게 만들긴 하지만, 읽는 사람에게 '펫이 안 생겼다'와 '이름이 바뀌었다'를 " +
                "구분해 주지 못합니다. 사본 값을 프로덕션에 맞추세요.");

            Debug.Log($"{LogPrefix} 문자열 거울 {compared}건 대조 완료 — 어긋남 0건.");
        }

        /// <summary>★ 양성/음성 대조 — 문자열 파서와 프로덕션 추출기가 <b>실제로 값을 뽑는가</b>,
        /// 그리고 <b>틀린 값을 틀렸다고 하는가</b>. 이것이 빨간불이면 위 검사의 초록은
        /// "일치"가 아니라 "아무것도 못 읽음"이다.</summary>
        [Test]
        public void 양성_대조_문자열_파서가_값을_읽고_틀린_값을_잡아낸다()
        {
            string src = File.ReadAllText(MirrorSourcePath);

            // (a) 양성 — 사본 쪽 파서가 실제로 값을 뽑는가.
            Assert.IsTrue(TryReadStringConst(src, "PetContainerName", out string mirrored),
                $"{LogPrefix} 파서가 PetContainerName을 읽지 못했습니다 — 위 검사는 비교를 한 적이 없습니다.");
            Assert.IsNotEmpty(mirrored, $"{LogPrefix} 사본에서 뽑은 값이 빈 문자열입니다.");

            // (b) 양성 — 프로덕션 쪽 추출기가 실제로 값을 뽑는가.
            string production = ReadPetContainerNameFromProduction();
            Assert.IsNotEmpty(production, $"{LogPrefix} 프로덕션에서 뽑은 값이 빈 문자열입니다.");

            // (c) 음성 — 존재하지 않는 이름은 "일치"가 아니라 "못 찾음"으로 떨어져야 한다.
            Assert.IsFalse(TryReadStringConst(src, "존재하지않는문자열상수12345", out _),
                $"{LogPrefix} 없는 상수를 찾았다고 보고했습니다 — 파서가 아무 리터럴이나 집어오고 있습니다.");

            // (d) 음성 — 대조 로직이 실제로 불일치를 구분하는가(같은 자를 뒤집어 확인).
            //     프로덕션 값에 한 글자를 더한 가짜는 <b>반드시</b> 불일치여야 한다.
            Assert.AreNotEqual(production, production + "X",
                $"{LogPrefix} 문자열 비교가 불일치를 구분하지 못합니다 — 이 대조 전체가 무의미합니다.");

            // (e) 음성 — <c>const int</c> 파서가 <c>const string</c>을 집어오지 않는가(자가 섞이지 않게).
            Assert.IsFalse(TryReadIntConst(src, "PetContainerName", out _),
                $"{LogPrefix} 정수 파서가 문자열 상수를 읽었습니다 — 두 자가 섞였습니다.");

            // (f) ★★ 주입 대조 — <b>드리프트한 사본을 실제로 잡는가</b>를 끝까지 흉내 낸다.
            //     (c)·(d)는 파서와 비교 연산을 <b>따로</b> 확인할 뿐, "본안이 실제로 빨개지는가"는
            //     확인하지 않는다. 이 저장소가 반복해 온 형태가 바로 그것이다 —
            //     <b>부품은 다 살아 있는데 조립된 검사는 아무것도 못 잡는다.</b>
            //     디스크는 건드리지 않고 소스 텍스트만 메모리에서 어긋나게 만든 뒤 같은 파서로 읽는다.
            string needle = $"const string PetContainerName = \"{mirrored}\";";
            Assert.IsTrue(src.Contains(needle),
                $"{LogPrefix} 주입 니들이 사본에 없습니다({needle}) — 그 줄의 형태가 바뀌었습니다. " +
                "본안 정규식도 함께 확인하십시오(여기서 멈추는 것이 맞습니다).");

            string drifted = src.Replace(needle,
                $"const string PetContainerName = \"{mirrored}_드리프트\";");
            Assert.AreNotEqual(src, drifted,
                $"{LogPrefix} 주입이 아무것도 바꾸지 못했습니다 — 이 대조는 무효입니다.");

            Assert.IsTrue(TryReadStringConst(drifted, "PetContainerName", out string driftedValue),
                $"{LogPrefix} 드리프트한 사본에서 값을 읽지 못했습니다 — 본안은 그 경우를 " +
                "'못 찾음'으로 떨어뜨립니다(실패하긴 하지만 사유가 엉뚱해집니다).");
            Assert.AreNotEqual(production, driftedValue,
                $"{LogPrefix} ★ 일부러 어긋낸 사본을 프로덕션과 <b>같다</b>고 읽었습니다 — " +
                "본안 검사는 드리프트를 <b>구조적으로 잡지 못합니다</b>. 이 파일의 모든 초록을 폐기하십시오.");

            Debug.Log($"{LogPrefix} 문자열 대조 — 사본=\"{mirrored}\" 프로덕션=\"{production}\" " +
                      $"({Path.GetFileName(PetRendererSourcePath)}에서 추출).");
        }

        /// <summary>번호 상수들이 <b>서로 다른가</b>. 프로덕션이 재배치하다 둘을 같은 값으로 만들면
        /// 위 거울 검사는 통과하면서(사본도 같이 고치면) 두 PlayMode 테스트가 같은 아이템을 잰다.</summary>
        [Test]
        public void 신규_4종의_번호가_카테고리_안에서_서로_다르다()
        {
            Assert.AreNotEqual(AppearanceShapeBuilder.FxBubble, AppearanceShapeBuilder.FxLeaf,
                $"{LogPrefix} 물방울과 나뭇잎의 FX 번호가 같습니다.");
            Assert.AreNotEqual(AppearanceShapeBuilder.PetBalloon, AppearanceShapeBuilder.PetSnail,
                $"{LogPrefix} 풍선과 달팽이의 PET 번호가 같습니다.");
            Assert.AreNotEqual(AppearanceShapeBuilder.FxNone, AppearanceShapeBuilder.FxBubble,
                $"{LogPrefix} FX '없음'과 물방울의 번호가 같습니다 — " +
                "그러면 네거티브 컨트롤(FX 없음 = 조각 0개)이 물방울을 재게 됩니다.");
        }
    }
}
