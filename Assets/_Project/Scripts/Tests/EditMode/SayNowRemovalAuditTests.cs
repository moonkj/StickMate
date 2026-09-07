using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★★ <b>「말 걸기」 폐지가 유지되는가</b> — 2026-09-07 사용자 지시.
    ///
    /// ============================================================================
    /// 무엇이 사라졌고 왜 이 파일이 필요한가
    /// ============================================================================
    /// 사용자 지시: <b>부채꼴 ④[행동] 메뉴에서 「말 걸기」 항목을 삭제하고, 전역 단축키 B도 함께
    /// 완전히 해제</b>. 사라진 것은 <b>강제 발화 경로</b>이고, 「혼잣말」 기능 자체
    /// (<c>Dialogue/AmbientChatter</c>의 유휴/보행 확률 발화)는 <b>그대로 출하된다</b>.
    ///
    /// <para>이 저장소에는 같은 삭제를 <b>반쯤만</b> 해서 생긴 사고가 이미 두 종류 있다:</para>
    /// <list type="number">
    ///   <item><b>바인딩만 지우고 표기를 남긴다</b> → 유령 단축키. 2026-09-05에 F/J/H로 실제로 났고,
    ///     릴리스에서 눌러도 아무 일이 없는 조합을 카드가 계속 가르쳤다.</item>
    ///   <item><b>표기만 지우고 바인딩을 남긴다</b> → 발견 불가능한 채로 살아 있는 명령. 그리고
    ///     Windows에서는 <c>Ctrl+Alt+Win+&lt;글자&gt;</c>가 OS 조합과 겹칠 위험을 계속 진다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 부재 단언에는 반드시 존재 대조를 붙인다 (CLAUDE.md)
    /// ============================================================================
    /// 이 파일의 단언은 대부분 <b>「없다」</b>인데, 그 종류는 <b>썩으면 조용히 초록</b>이 된다 —
    /// 니들이 오타가 되거나 프로덕션 표현이 바뀌면 «찾지 못했다»와 «없다»가 <b>출력상 똑같다</b>.
    /// 그래서 모든 부재 단언은 <b>같은 테스트 안에서</b> 살아 있는 형제(활쏘기 A / 그라피티 G)로
    /// 니들이 아직 무언가를 찾아낸다는 것을 먼저 보인다.
    ///
    /// ============================================================================
    /// ★ 되살리려면
    /// ============================================================================
    /// <b>사용자에게 다시 물어라.</b> 이건 사용자가 닫은 문이다. 코드에서 되살리는 순서는
    /// <c>Interaction/AppControlDirector.cs</c>의 「말 걸기 폐지」 절에 적혀 있다.
    /// </summary>
    public sealed class SayNowRemovalAuditTests
    {
        private const string LogPrefix = "[말걸기폐지]";

        private static string ScriptsRoot => Path.Combine(Application.dataPath, "_Project", "Scripts");

        private static string ReadScript(params string[] relative)
        {
            string path = Path.Combine(ScriptsRoot, Path.Combine(relative));
            Assert.IsTrue(File.Exists(path), $"{LogPrefix} 소스를 찾지 못했습니다: {path}");
            return File.ReadAllText(path).Replace("\r\n", "\n");
        }

        /// <summary>줄 전체가 <c>//</c>로 시작하는 주석만 걷는다. 이 라운드가 남긴 «왜 지웠는가»
        /// 설명들이 전부 그 형태이고, 그것들이 니들에 걸리면 <b>삭제했는데 빨간</b> 거짓 실패가 난다.
        /// <para>문자열 리터럴 안의 <c>//</c>를 건드리지 않으려고 <b>줄 끝 주석은 일부러 남긴다</b> —
        /// 이 파일이 겨누는 니들은 줄 끝 주석에 나타나지 않는다.</para></summary>
        private static string StripFullLineComments(string source)
        {
            string[] lines = source.Split('\n');
            var kept = new System.Text.StringBuilder(source.Length);
            foreach (string line in lines)
            {
                if (line.TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;
                kept.Append(line).Append('\n');
            }
            return kept.ToString();
        }

        private static float ReadFloatConst(string source, string name)
        {
            Match m = Regex.Match(source, @"const\s+float\s+" + Regex.Escape(name) + @"\s*=\s*(-?[\d.]+)f");
            Assert.IsTrue(m.Success,
                $"{LogPrefix} const float {name} 을(를) 찾지 못했습니다 — 이름이 바뀌었다면 이 감사도 " +
                "함께 고치십시오(못 찾은 것을 통과로 넘기면 아래 계산이 통째로 무의미해집니다).");
            return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        // ====================================================================
        // ① 전역 단축키 B가 어디에서도 폴링되지 않는다
        // ====================================================================

        [Test]
        public void 전역_단축키_B는_더_이상_조회되지_않는다()
        {
            string director = StripFullLineComments(ReadScript("Interaction", "AppControlDirector.cs"));

            // ---- 존재 대조: 니들 모양이 아직 무언가를 찾아내는가 ----
            // 그라피티(G)는 출하 중인 사용자 단축키다. 이 줄이 깨지면 아래 "B가 없다"는 측정이 아니다.
            string live = "IsKeyDown(" + nameof(StickMate.Platform.GlobalKey) + "." + nameof(StickMate.Platform.GlobalKey.G) + ")";
            StringAssert.Contains(live, director,
                $"{LogPrefix} 존재 대조 실패 — '{live}'를 찾지 못했습니다. 니들이 죽었으므로 " +
                "아래 'B는 조회되지 않는다'는 아무것도 증명하지 않습니다.");

            // ---- 부재 단언 ----
            string dead = "IsKeyDown(" + nameof(StickMate.Platform.GlobalKey) + "." + nameof(StickMate.Platform.GlobalKey.B) + ")";
            Assert.AreEqual(-1, director.IndexOf(dead, StringComparison.Ordinal),
                $"{LogPrefix} AppControlDirector가 아직 '{dead}'를 조회합니다 — 2026-09-07 사용자 지시는 " +
                "「전역 단축키 B도 함께 완전히 해제」였습니다. 되살리려면 사용자에게 다시 물으십시오.");

            Assert.AreEqual(-1, director.IndexOf("_prevB", StringComparison.Ordinal),
                $"{LogPrefix} B의 엣지 판정 필드(_prevB)가 남아 있습니다 — 조회가 없는데 엣지 상태만 " +
                "남으면 다음 사람이 «배선이 있다»로 읽습니다.");

            Debug.Log($"{LogPrefix} ① 통과 — G 조회는 살아 있고(존재 대조) B 조회는 0건이다.");
        }

        // ====================================================================
        // ② 실행부가 사라졌다 (판정 + 강제 발화)
        // ====================================================================

        /// <summary>
        /// 회색 처리와 실행이 <b>같은 판정 하나</b>를 쓴다는 36-7 규칙 때문에, 이 기능은 함수가
        /// <b>둘</b>이었다(<c>GetSayNowAvailability</c> / <c>ForceSayNow</c>). 한쪽만 지우면
        /// 나머지가 «죽은 진입점»으로 남아 다음 사람이 배선을 되살리는 재료가 된다.
        /// </summary>
        [Test]
        public void 말_걸기_실행부가_프로덕션에_남아_있지_않다()
        {
            string[] production =
            {
                Path.Combine("Interaction", "AppControlDirector.cs"),
                Path.Combine("Interaction", "ActionCommandPopover.cs"),
            };

            // ---- 존재 대조: 살아 있는 형제 진입점은 그대로 있는가 ----
            string popover = StripFullLineComments(ReadScript("Interaction", "ActionCommandPopover.cs"));
            StringAssert.Contains("ForceTriggerNow", popover,
                $"{LogPrefix} 존재 대조 실패 — 행동 명령창에서 'ForceTriggerNow'를 찾지 못했습니다. " +
                "이 창이 명령을 실행하는 방식 자체가 바뀌었거나 니들이 죽었습니다.");

            foreach (string relative in production)
            {
                string[] parts = relative.Split(Path.DirectorySeparatorChar);
                string src = StripFullLineComments(ReadScript(parts));
                foreach (string dead in new[] { "ForceSayNow", "GetSayNowAvailability" })
                {
                    Assert.AreEqual(-1, src.IndexOf(dead, StringComparison.Ordinal),
                        $"{LogPrefix} {relative}에 '{dead}'가 남아 있습니다 — 말 걸기는 2026-09-07 " +
                        "사용자 지시로 폐지됐습니다(부채꼴 타일 + 단축키 B 동시 해제).");
                }
            }

            Debug.Log($"{LogPrefix} ② 통과 — ForceTriggerNow는 살아 있고(존재 대조) SayNow 실행부는 0건이다.");
        }

        // ====================================================================
        // ③ 부채꼴 ④[행동]의 칸에서 사라졌다 — enum에서 직접 확인한다
        // ====================================================================

        /// <summary>
        /// 소스 텍스트가 아니라 <b>enum 자체</b>를 본다. 창은 <c>_tiles</c> 배열 길이를
        /// <c>CommandCount</c>에서 얻으므로, enum이 곧 화면에 뜨는 칸의 정본이다.
        /// </summary>
        [Test]
        public void 행동_명령창에_말_걸기_칸이_없다()
        {
            string[] names = Enum.GetNames(typeof(ActionCommandPopover.Command));

            // ---- 존재 대조: 살아 있는 칸이 실제로 보이는가 ----
            CollectionAssert.Contains(names, nameof(ActionCommandPopover.Command.Archery),
                $"{LogPrefix} 존재 대조 실패 — [활쏘기] 칸을 찾지 못했습니다. 이 창의 명령 목록을 " +
                "읽는 방식이 바뀌었으므로 아래 부재 단언은 아무것도 재지 않습니다.");

            // ---- 부재 단언: 이름이 바뀐 채로 되살아나는 것까지 막는다 ----
            foreach (string name in names)
            {
                foreach (string banned in new[] { "Say", "Chatter", "Talk", "Speak" })
                {
                    Assert.IsFalse(name.IndexOf(banned, StringComparison.OrdinalIgnoreCase) >= 0,
                        $"{LogPrefix} 명령 [{name}]이 말 걸기 계열로 보입니다 — 2026-09-07 사용자 지시로 " +
                        "이 창에서 삭제된 항목입니다. 다른 이름으로 되살리는 것도 되살리는 것입니다.");
                }
            }

            Debug.Log($"{LogPrefix} ③ 통과 — 명령 {names.Length}칸({string.Join(", ", names)}) 중 " +
                      "말 걸기 계열 0칸.");
        }

        // ====================================================================
        // ④ 항목이 빠진 만큼 창이 실제로 줄었다 (빈 띠를 남기지 않았다)
        // ====================================================================

        /// <summary>
        /// ★ 「지웠는데 패널만 그대로」를 잡는다 — 2026-09-06 집중 모드 「지켜보기」 삭제에서 같은
        /// 그물을 썼다. 행이 하나 빠졌는데 <see cref="ActionCommandPopover"/>의 높이를 그대로 두면
        /// 푸터 아래에 <b>정확히 한 행(52pt)의 빈 띠</b>가 남는다. 그 띠는 아무것도 말하지 않으면서
        /// 창을 아래로 무겁게 만든다(UX_FLOW 17절 "빈 상태를 굳이 보여주지 않는다").
        ///
        /// <para><b>행 수를 상수로 적지 않는다</b>: <c>BuildTile(group1, ...)</c>/<c>(group2, ...)</c>
        /// 호출을 세어 <b>실제로 만들어지는 행</b>에서 유도하고, 그 합이 <c>CommandCount</c>와 같은지도
        /// 함께 본다. 이 두 값이 갈라지면 «enum에서는 뺐는데 타일은 남았다»(또는 그 반대)이고,
        /// 그 상태에서는 <c>_tiles</c>에 null이 남아 클릭이 조용히 죽는다.</para>
        ///
        /// <para>세로 배치식(간격 8/4/12, 크롬 22)은 프로덕션과 <b>같은 식을 다시 적는다</b> —
        /// 그 식이 바뀌면 이 테스트도 함께 고쳐야 한다. 여기서 사는 것은 «식이 옳은가»가 아니라
        /// <b>«식에 넣는 행 수가 바뀌었을 때 높이가 따라왔는가»</b>다.</para>
        /// </summary>
        [Test]
        public void 타일_한_행이_빠진_만큼_창_높이도_줄었다()
        {
            string src = ReadScript("Interaction", "ActionCommandPopover.cs");
            string code = StripFullLineComments(src);

            int rows1 = Regex.Matches(code, @"BuildTile\(group1,").Count;
            int rows2 = Regex.Matches(code, @"BuildTile\(group2,").Count;
            Assert.Greater(rows1, 0, $"{LogPrefix} 그룹1에 타일이 하나도 없습니다 — 니들이 죽었습니다.");
            Assert.Greater(rows2, 0, $"{LogPrefix} 그룹2에 타일이 하나도 없습니다 — 니들이 죽었습니다.");

            Assert.AreEqual(ActionCommandPopover.CommandCount, rows1 + rows2,
                $"{LogPrefix} 만들어지는 타일이 {rows1 + rows2}개인데 CommandCount는 " +
                $"{ActionCommandPopover.CommandCount}입니다 — enum과 조립이 갈라졌습니다. " +
                "_tiles에 null이 남아 그 칸의 클릭이 조용히 죽습니다(36-7 조용한 실패 금지).");

            // ★ 주석을 걷은 <c>code</c>에서 읽는다 — 이 파일의 «왜 508 → 456인가» 검산 주석에는
            //   같은 숫자들이 그대로 적혀 있어서, 원본에서 읽으면 주석이 코드를 검증하게 된다.
            float cardPadding = ReadFloatConst(code, "CardPadding");
            float groupTitle = ReadFloatConst(code, "GroupTitleHeight");
            float rowHeight = ReadFloatConst(code, "RowHeight");
            float statusRow = ReadFloatConst(code, "StatusRowHeight");
            float caption2 = ReadFloatConst(code, "Group2CaptionHeight");
            float footerRow = ReadFloatConst(code, "QuitButtonHeight");
            float height = ReadFloatConst(code, "Height");

            // 프로덕션과 같은 식(간격 8/4/12는 그 파일의 리터럴이다).
            float group1H = cardPadding * 2f + groupTitle + 4f + rowHeight * rows1;
            float group2H = cardPadding * 2f + groupTitle + 4f + rowHeight * rows2 + 4f + caption2;
            float group1Y = -(statusRow + 8f);
            float group2Y = group1Y - group1H - 12f;
            float footerY = group2Y - group2H - 12f;
            float contentBottom = Mathf.Abs(footerY) + footerRow;

            // 크롬이 먹는 세로 — PopoverPanel.BuildChrome의 식(제목 줄 22는 그 파일의 리터럴).
            const float TitleRowHeight = 22f;
            float contentHeight = height - (UiChrome.Space3 + TitleRowHeight + UiChrome.Space2) - UiChrome.Space4;

            Assert.AreEqual(contentBottom, contentHeight, 0.001f,
                $"{LogPrefix} 콘텐츠 높이({contentHeight:F0}pt)와 푸터 바닥({contentBottom:F0}pt)이 " +
                $"다릅니다 — 타일이 {rows1}+{rows2}행인데 창 높이는 {height:F0}pt입니다. " +
                $"차이 {contentHeight - contentBottom:F0}pt는 " +
                $"{(contentHeight > contentBottom ? "푸터 아래에 남은 빈 띠" : "패널 밖으로 삐져나간 내용")}입니다. " +
                "행을 지웠으면 Height도 같은 만큼 줄이십시오(2026-09-07 말 걸기 타일 삭제: 508 → 456).");

            Debug.Log($"{LogPrefix} ④ 통과 — 그룹1 {rows1}행 / 그룹2 {rows2}행, 창 {height:F0}pt, " +
                      $"콘텐츠 {contentHeight:F0}pt = 푸터 바닥 {contentBottom:F0}pt (빈 띠 0).");
        }

        // ====================================================================
        // ⑤ 보관함 카드가 없는 조합키를 가르치지 않는다 (유령 단축키)
        // ====================================================================

        /// <summary>
        /// <see cref="ItemCatalog"/>의 「혼잣말」 카드는 <b>남는다</b> — 유휴/보행 확률 발화는 그대로
        /// 출하되므로 지우면 <b>있는 기능을 없다고</b> 말하게 된다. 지워야 하는 것은 <b>표기</b>다.
        /// <para>여기서는 카드 하나가 아니라 <b>행동 카드 전수</b>를 훑는다 — 다음에 또 어떤 바인딩이
        /// 사라져도 이 그물에 걸리게.</para>
        /// </summary>
        [Test]
        public void 어떤_행동_카드도_B_조합키를_광고하지_않는다()
        {
            string bChord = ShortcutLabel.Chord("B");
            string aChord = ShortcutLabel.Chord("A");

            bool sawLiveChord = false;
            int actions = 0;
            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                ItemCatalogEntry entry = ItemCatalog.At(i);
                if (entry.Category != ItemCategory.Action) continue;
                actions++;

                if (entry.ActionStatus == aChord) sawLiveChord = true;

                Assert.AreNotEqual(bChord, entry.ActionStatus,
                    $"{LogPrefix} 「{entry.DisplayName}」 카드가 '{bChord}'를 광고합니다 — 그 바인딩은 " +
                    "2026-09-07 사용자 지시로 삭제됐습니다. 눌러도 아무 일도 일어나지 않는 조합을 " +
                    "화면이 계속 가르치는 것이 2026-09-05 F/J/H 결함의 정의 그 자체입니다.");
            }

            Assert.Greater(actions, 0, $"{LogPrefix} 행동 카드를 하나도 찾지 못했습니다 — 스캔이 깨졌습니다.");

            // ---- 존재 대조 ----
            Assert.IsTrue(sawLiveChord,
                $"{LogPrefix} 존재 대조 실패 — 살아 있는 조합키('{aChord}', 활쏘기)를 어떤 카드에서도 " +
                "찾지 못했습니다. 표기 방식이 바뀌었으므로 위 'B를 광고하지 않는다'는 측정이 아닙니다.");

            Debug.Log($"{LogPrefix} ⑤ 통과 — 행동 카드 {actions}장 중 '{bChord}' 0장, " +
                      $"'{aChord}'는 살아 있다(존재 대조).");
        }
    }
}
