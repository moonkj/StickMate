using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Interaction;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ <b>이 앱 최저 대비 1.28 : 1</b>의 회귀 잠금 — 2026-09-02.
    ///
    /// ============================================================================
    /// 무엇이 있었나 (실측)
    /// ============================================================================
    /// [설정] &gt; [캐릭터] &gt; <c>말투</c> 행이 <b>비활성</b>일 때 <c>[반말]</c> 칩:
    /// <code>
    ///   면   #5DA1F5  (강조색 — <b>활성과 똑같다</b>)
    ///   글자 #AEB4BF  (어두운 바탕용 잉크)
    ///   대비 1.28 : 1
    /// </code>
    /// 페르소나가 <i>"화면에 한 글자도 없다"</i>고 적게 만든 값이 <b>2.35</b>였다. <b>이건 그보다 낮다.</b>
    /// 같은 병이 한 곳 더 있었다 — <c>말풍선 표시</c>를 끄는 순간 <c>대사 표시 시간</c>의 <c>[기본]</c> 칩.
    ///
    /// ============================================================================
    /// 왜 <b>기존 사다리 검사가 못 잡았나</b> — 그게 진짜 결함이다
    /// ============================================================================
    /// <see cref="UiChrome.TextBackdrops"/>에 담긴 넷이 <b>전부 어두운 면</b>이었다. 그래서
    /// <i>"이 잉크가 <b>밝은</b> 면 위에 놓이면?"</i>이라는 질문이 검사에 <b>존재하지 않았다</b>.
    /// 검사는 초록이었고 화면은 지워져 있었다 — 검사가 통과한 것이 아니라 <b>보지 않은 것</b>이다.
    ///
    /// <para>그래서 이 파일은 잉크가 아니라 <b>바탕</b>을 검사한다: 선언된 모든 바탕 × 모든 역할 ×
    /// 활성/비활성을 <see cref="UiChrome.InkOnSurface"/>라는 <b>단 하나의 문</b>에 통과시킨다.
    /// 새 표면을 만드는 사람은 목록에 한 줄만 넣으면 그 순간부터 이 검사가 지켜 준다.</para>
    ///
    /// ============================================================================
    /// 네거티브 컨트롤이 이 파일의 절반이다
    /// ============================================================================
    /// "고친 뒤 통과"만으로는 아무것도 증명하지 못한다(이 저장소가 하룻밤에 <b>거짓 초록 일곱 건</b>을
    /// 낸 이유다). 그래서 <b>날것의 사다리 잉크가 그 바탕들에서 실제로 무너진다</b>는 것을 같은 파일에서
    /// 단언한다 — 무너지지 않는다면 <see cref="UiChrome.InkOnSurface"/>의 교체는 장식이고,
    /// 위쪽 초록은 아무 조건도 아니다.
    /// </summary>
    public sealed class InkOnSurfaceTests
    {
        private const string LogPrefix = "[면잉크-TEST]";

        private static readonly UiChrome.InkRole[] Roles =
        {
            UiChrome.InkRole.Title, UiChrome.InkRole.Body, UiChrome.InkRole.Meta,
        };

        private static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        private static IEnumerable<Color> AllDeclaredBackdrops()
        {
            foreach (Color c in UiChrome.TextBackdrops) yield return c;
            foreach (Color c in UiChrome.RaisedTextBackdrops) yield return c;
            foreach (Color c in UiChrome.BrightTextBackdrops) yield return c;
        }

        // ==================== ① 문 하나로 전부 통과하는가 ====================

        [Test]
        public void 선언된_모든_바탕에서_InkOnSurface가_AA를_넘는다()
        {
            foreach (Color bg in AllDeclaredBackdrops())
            {
                foreach (UiChrome.InkRole role in Roles)
                {
                    foreach (bool enabled in new[] { true, false })
                    {
                        Color ink = UiChrome.InkOnSurface(bg, role, enabled);
                        float ratio = UiChrome.ContrastRatio(ink, bg);
                        Assert.GreaterOrEqual(ratio, UiChrome.MinTextContrast,
                            $"{LogPrefix} 바탕 {Hex(bg)} 위 {role}({(enabled ? "활성" : "비활성")})가 " +
                            $"{Hex(ink)} = {ratio:F2}:1입니다. 하한 {UiChrome.MinTextContrast:F1}:1 미만이면 " +
                            "그건 '흐린 글자'가 아니라 <b>없는 글자</b>입니다.");
                    }
                }
            }
        }

        /// <summary>어두운 큰 면에서는 <b>사다리를 바꾸지 않는다</b> — 위계는 거기서 살아 있어야 한다.
        /// 문이 아무 데서나 잉크를 갈아치우면 3단이 전부 한 색으로 접혀 위계가 사라진다.</summary>
        [Test]
        public void 어두운_면에서는_사다리를_그대로_돌려준다_위계_보존()
        {
            foreach (Color bg in UiChrome.TextBackdrops)
            {
                foreach (UiChrome.InkRole role in Roles)
                {
                    foreach (bool enabled in new[] { true, false })
                    {
                        Assert.AreEqual(UiChrome.Ink(role, enabled), UiChrome.InkOnSurface(bg, role, enabled),
                            $"{LogPrefix} 바탕 {Hex(bg)}에서 문이 사다리를 갈아치웠습니다 — 그 면은 " +
                            "사다리 3단이 전부 통과하는 면이므로 위계를 그대로 유지해야 합니다.");
                    }
                }
            }
        }

        // ==================== ② 네거티브 컨트롤 — 바탕이 실제로 적대적인가 ====================

        /// <summary>★ 사고 당시의 <b>정확한 짝</b>. 이 값이 재현되지 않으면 위쪽 초록은 무의미하다.</summary>
        [Test]
        public void 네거티브_컨트롤_사고_당시_짝은_실제로_1점3대1이었다()
        {
            Color face = SettingsControls.AccentSolid;              // 비활성인데도 남아 있던 강조색 면
            Color oldInk = UiChrome.Ink(UiChrome.InkRole.Title, false);   // 옛 코드의 InkTitle(false)
            float ratio = UiChrome.ContrastRatio(oldInk, face);

            Assert.Less(ratio, 1.5f,
                $"{LogPrefix} 옛 짝({Hex(oldInk)} on {Hex(face)})이 {ratio:F2}:1로 나왔습니다 — " +
                "1.28:1이 재현되지 않으면 이 파일이 지키는 대상이 실재하지 않는다는 뜻입니다.");
            Assert.Less(ratio, UiChrome.MinTextContrast,
                $"{LogPrefix} 옛 짝이 AA를 넘었습니다 — 전제가 깨졌습니다.");

            // 그리고 새 문은 같은 면에서 반드시 다른 답을 낸다.
            Color fixedInk = UiChrome.InkOnSurface(face, UiChrome.InkRole.Title, false);
            Assert.AreNotEqual(oldInk, fixedInk,
                $"{LogPrefix} 문이 강조색 면에서도 옛 잉크를 그대로 돌려줬습니다 — 교체가 일어나지 " +
                "않으면 이 수정은 이름만 바뀐 같은 코드입니다.");
            Assert.GreaterOrEqual(UiChrome.ContrastRatio(fixedInk, face), UiChrome.MinTextContrast,
                $"{LogPrefix} 교체한 잉크도 AA를 못 넘습니다.");
        }

        [Test]
        public void 네거티브_컨트롤_강조색_면_위에서는_사다리_3단이_전부_무너진다()
        {
            foreach (Color bright in UiChrome.BrightTextBackdrops)
            {
                foreach (UiChrome.InkRole role in Roles)
                {
                    foreach (bool enabled in new[] { true, false })
                    {
                        Color ladder = UiChrome.Ink(role, enabled);
                        float ratio = UiChrome.ContrastRatio(ladder, bright);
                        Assert.Less(ratio, UiChrome.MinTextContrast,
                            $"{LogPrefix} 밝은 바탕 {Hex(bright)} 위에서 사다리 {role}" +
                            $"({(enabled ? "활성" : "비활성")}) = {ratio:F2}:1로 <b>AA를 넘었습니다</b>. " +
                            "이 목록은 '사다리가 통하지 않는 면'을 모으는 자리입니다 — 통하는 면이 " +
                            "들어오면 UiChrome.TextBackdrops로 옮기십시오(그러면 위계도 되살아납니다).");
                    }
                }
            }
        }

        /// <summary>★ 한 단 들뜬 중성 면 — 여기선 <b>Meta만</b> 무너진다(3.94 / 4.38). 그 사실이
        /// 목록의 존재 이유이고, 무너지지 않게 되면 목록을 합쳐야 한다.</summary>
        [Test]
        public void 네거티브_컨트롤_들뜬_면_위에서는_Meta가_무너진다()
        {
            foreach (Color raised in UiChrome.RaisedTextBackdrops)
            {
                float meta = UiChrome.ContrastRatio(UiChrome.Ink(UiChrome.InkRole.Meta, true), raised);
                Assert.Less(meta, UiChrome.MinTextContrast,
                    $"{LogPrefix} 들뜬 바탕 {Hex(raised)} 위에서 Meta = {meta:F2}:1로 AA를 넘었습니다 — " +
                    "그렇다면 이 목록을 따로 둘 이유가 없습니다(UiChrome.TextBackdrops로 합치십시오).");

                // 반대로 Title은 통해야 한다 — 지금 이 면에 실제로 놓이는 글자가 그 단이다.
                float title = UiChrome.ContrastRatio(UiChrome.Ink(UiChrome.InkRole.Title, false), raised);
                Assert.GreaterOrEqual(title, UiChrome.MinTextContrast,
                    $"{LogPrefix} 들뜬 바탕 {Hex(raised)} 위에서 Title(비활성) = {title:F2}:1입니다 — " +
                    "버튼 라벨이 실제로 그 단으로 그려집니다.");
            }
        }

        // ==================== ③ 목록 자체의 위생 ====================

        [Test]
        public void 세_바탕_목록은_전부_불투명하고_서로_겹치지_않는다()
        {
            var seen = new List<Color>();
            foreach (Color bg in AllDeclaredBackdrops())
            {
                Assert.AreEqual(1f, bg.a, 1e-4f,
                    $"{LogPrefix} 바탕 {Hex(bg)}의 알파가 {bg.a:F2}입니다 — 반투명 바탕 위의 대비 계산은 " +
                    "거짓말입니다. UiChrome.Flatten으로 미리 합성해서 넣으십시오.");

                foreach (Color other in seen)
                {
                    Assert.IsFalse(Same(bg, other),
                        $"{LogPrefix} 바탕 {Hex(bg)}이(가) 두 목록에 동시에 있습니다 — 어느 규칙을 " +
                        "따라야 하는지가 값에서 갈리지 않으면 다음 사람이 반드시 틀린 쪽을 고릅니다.");
                }
                seen.Add(bg);
            }
        }

        /// <summary>★ 선언과 실제 도색이 <b>갈라지지 않게</b> 못 박는다. 설정창이 쓰는 토큰이 목록에
        /// 없으면 그 면은 다시 '보이지 않는 바탕'이 된다 — 이번 사고가 정확히 그것이었다.</summary>
        [Test]
        public void 설정창이_실제로_쓰는_면들이_전부_어느_목록엔가_선언돼_있다()
        {
            AssertDeclared(SettingsControls.AccentSolid, "선택된 세그먼트 칩 / 켜진 스위치 트랙");
            AssertDeclared(SettingsControls.ButtonSurfaceOnCard, "카드 위 버튼 · 비활성 세그먼트 칩");
            AssertDeclared(SettingsControls.ButtonSurfaceOnPanel, "푸터 [지금 종료] 버튼");
            AssertDeclared(UiChrome.CardSurface, "카드 바탕");
            AssertDeclared(UiChrome.CardSurfaceMuted, "레일 칩 / 잠긴 카드");
            AssertDeclared(UiChrome.PanelSurface, "창 바탕");
        }

        private static void AssertDeclared(Color face, string where)
        {
            foreach (Color bg in AllDeclaredBackdrops())
            {
                if (Same(bg, face)) return;
            }
            Assert.Fail($"{LogPrefix} {where}에 쓰이는 면 {Hex(face)}이(가) 어느 바탕 목록에도 " +
                "없습니다. 선언되지 않은 바탕은 대비 검사에서 <b>보이지 않고</b>, 보이지 않는 바탕이 " +
                "1.28:1을 만들었습니다.");
        }

        private static bool Same(Color a, Color b)
            => Mathf.Abs(a.r - b.r) < 1e-3f && Mathf.Abs(a.g - b.g) < 1e-3f
            && Mathf.Abs(a.b - b.b) < 1e-3f && Mathf.Abs(a.a - b.a) < 1e-3f;

        // ==================== ④ 세그먼트가 실제로 그 문을 쓰는가 ====================

        /// <summary>
        /// 값 수준 회귀 잠금 — 세그먼트의 <b>네 가지 상태</b>가 내는 (면, 글자) 짝을 프로덕션과
        /// <b>같은 계산</b>으로 재현해 AA를 확인한다.
        /// <para>좌표·색을 손으로 베끼지 않는다: 면은 <see cref="SettingsControls"/> 토큰에서,
        /// 잉크는 <see cref="UiChrome.InkOnSurface"/>에서 그대로 온다.</para>
        /// </summary>
        [Test]
        public void 세그먼트_네_상태의_면과_글자가_모두_AA를_넘는다()
        {
            foreach (bool interactable in new[] { true, false })
            {
                foreach (bool active in new[] { true, false })
                {
                    Color face = interactable
                        ? (active ? SettingsControls.AccentSolid : UiChrome.CardSurface)
                        : (active ? SettingsControls.ButtonSurfaceOnCard : UiChrome.CardSurface);
                    Color ink = UiChrome.InkOnSurface(face, UiChrome.InkRole.Body, enabled: true);
                    float ratio = UiChrome.ContrastRatio(ink, face);

                    Assert.GreaterOrEqual(ratio, UiChrome.MinTextContrast,
                        $"{LogPrefix} 세그먼트(누를 수 있음={interactable}, 골라짐={active})가 " +
                        $"{Hex(ink)} on {Hex(face)} = {ratio:F2}:1입니다.");

                    // ★ 비활성인데 강조색 면이 남아 있으면 그게 이번 사고 그 자체다.
                    if (!interactable)
                    {
                        Assert.IsFalse(Same(face, SettingsControls.AccentSolid),
                            $"{LogPrefix} 비활성 세그먼트에 강조색 면이 남았습니다 — 컨트롤이 " +
                            "'눌러도 된다'고 거짓말하고, 그 위의 글자가 지워집니다.");
                    }
                }
            }
        }
    }
}
