using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 2026-09-01 P0-2 — <b>포스트잇이 앱에서 유일하게 디자인 시스템 밖에 있었다.</b>
    /// 근거: docs/UI_SURFACE_SPEC.md §4.
    ///
    /// ============================================================================
    /// 무엇이 결함이었나
    /// ============================================================================
    /// 표면이 <c>new Color(1, 0.95, 0.6, <b>0.92</b>)</c>였다. 2026-08-31에 정보창·설정창·팝오버가
    /// 전부 α=1로 바뀐 그 라운드에서 <b>이 카드만 빠졌고</b>, 그래서 폐기된 "알파 유리" 규약이
    /// 여기 하나에 살아남았다.
    ///
    /// 이 앱에서 α&lt;1은 단순히 "반투명해 보인다"가 아니다 — 창 뒤에 <b>유저의 진짜 데스크톱</b>이
    /// 있으므로 <b>카드 색 자체가 배경에 따라 변한다</b>:
    /// <code>
    ///   진한 파랑(#3b4fd8) 위 → #efe59e
    ///   흰 문서(#ffffff) 위   → #fff3a1      ΔL = 11.1%p
    /// </code>
    ///
    /// ============================================================================
    /// 측정 방식 — <see cref="WindowAlphaProbe"/>를 그대로 재사용한다
    /// ============================================================================
    /// 색 상수를 다시 읽지 않고 <b>실제로 만들어진 계층</b>을 그리기 순서대로 걸으며 실측한다
    /// (<see cref="PopoverAndHoverPanelOpacityTests"/>가 팝오버 3종에 쓰는 것과 같은 프로브).
    /// "코드에 0.92가 없다"가 아니라 "화면 알파가 1이다"를 봐야 그림자/보더처럼 <b>나중에 얹히는 겹</b>이
    /// 다시 알파를 끌어내리는 경로까지 잡힌다.
    /// </summary>
    public sealed class TodoPostItChromeTests
    {
        private const string LogPrefix = "[포스트잇크롬-TEST]";

        /// <summary>소프트캡 — 이 테스트는 캡 경고를 보지 않으므로 넉넉히 잡는다.</summary>
        private const int SoftCap = 99;

        private TodoPostItWidget _widget;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            TodoListModel.ResetForTesting();
            _widget = null;
            yield return null;
        }

        private IEnumerator ShowCardWithOneTodo()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _widget = Object.FindFirstObjectByType<TodoPostItWidget>();
            Assert.IsNotNull(_widget, $"{LogPrefix} 씬에 TodoPostItWidget이 없습니다.");

            // 카드는 미완료가 0건이면 통째로 숨는다(17절 "빈 상태 예외" — 그 동작은 옳다).
            // 관측하려면 항목이 하나 필요하다.
            TodoListModel.ResetForTesting();
            // Add의 반환값은 "성공했는가"가 아니라 "소프트캡을 넘었는가"다 — 개수로 확인한다.
            TodoListModel.Add("포스트잇 크롬 회귀 확인용", SoftCap);
            Assert.AreEqual(1, TodoListModel.UncompletedCount,
                $"{LogPrefix} 테스트용 할 일이 목록에 들어가지 않았습니다.");
            yield return null;
            yield return null;

            Assert.IsTrue(_widget.IsCardVisible,
                $"{LogPrefix} 할 일이 1건인데 카드가 보이지 않습니다 — 관측 전제가 성립하지 않습니다.");
        }

        /// <summary>포스트잇 패널의 <b>사각형 전체를 덮는</b> 그래픽 계층을 그리기 순서로 훑어
        /// 최종 창 알파를 구한다(<see cref="WindowAlphaProbe"/>의 계산식과 같다).</summary>
        private static RectTransform FindPanel(TodoPostItWidget widget)
        {
            var all = widget.GetComponentsInChildren<RectTransform>(true);
            foreach (RectTransform rt in all)
            {
                if (rt.name == "PostItPanel") return rt;
            }
            return null;
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator PostItSurfaceIsFullyOpaqueLikeEveryOtherWindow()
        {
            yield return ShowCardWithOneTodo();

            RectTransform panel = FindPanel(_widget);
            Assert.IsNotNull(panel, $"{LogPrefix} PostItPanel을 찾지 못했습니다 — 이름이 바뀌었습니다.");

            var layers = new List<(Graphic g, float alpha)>();
            WindowAlphaProbe.CollectDrawOrder(panel, WindowAlphaProbe.InheritedGroupAlpha(panel), layers);
            Assert.Greater(layers.Count, 0, $"{LogPrefix} 패널 아래에 그래픽이 하나도 없습니다.");

            // 패널 한가운데를 덮는 겹만 누적한다 — 띠/칩처럼 일부만 덮는 것은 여기 대상이 아니다.
            Vector3[] corners = new Vector3[4];
            panel.GetWorldCorners(corners);
            Vector2 center = (corners[0] + corners[2]) * 0.5f;

            float dst = 0f;
            Graphic last = null;
            foreach ((Graphic g, float groupAlpha) in layers)
            {
                if (!(g is Image) && !(g is Text)) continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint(g.rectTransform, center, null)) continue;
                if (!CoversWholeRect(g.rectTransform, corners)) continue;

                float srcA = Mathf.Clamp01(g.color.a) * Mathf.Clamp01(groupAlpha);
                // UiChrome '알파 채널의 법칙': dstA' = srcA² + dstA(1 − srcA)
                dst = srcA * srcA + dst * (1f - srcA);
                last = g;
            }

            Assert.GreaterOrEqual(dst, WindowAlphaProbe.RequiredWindowAlpha,
                $"{LogPrefix} 포스트잇의 창 알파가 {dst:F3}입니다(마지막 겹 '{(last != null ? last.name : "-")}') — " +
                $"{(1f - dst) * 100f:F1}%만큼 유저의 데스크톱이 카드를 통과해 비칩니다. " +
                "이 앱에서 α<1은 '반투명'이 아니라 '카드 색이 배경마다 달라진다'는 뜻입니다.");
        }

        /// <summary>이 그래픽이 패널 사각형을 <b>완전히</b> 덮는가(모서리 네 곳을 다 포함하는가).</summary>
        private static bool CoversWholeRect(RectTransform rt, Vector3[] panelCorners)
        {
            var own = new Vector3[4];
            rt.GetWorldCorners(own);
            const float Epsilon = 0.5f;
            return own[0].x <= panelCorners[0].x + Epsilon && own[0].y <= panelCorners[0].y + Epsilon
                && own[2].x >= panelCorners[2].x - Epsilon && own[2].y >= panelCorners[2].y - Epsilon;
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — 옛 구조(반투명 단색 판 한 장)를 그대로 재현하면 위 단언이
        /// <b>실제로 깨지는가</b>. 이게 없으면 위 테스트가 "무엇이든 통과하는 초록"일 수 있다.
        /// </summary>
        [Test]
        public void OldTranslucentPostItWouldFailTheOpacityRule_NegativeControl()
        {
            const float OldAlpha = 0.92f;   // 옛 코드의 값. 여기서만 <b>일부러</b> 적는다(재현 대상이므로).
            float dst = OldAlpha * OldAlpha;   // 빈 화면(dstA 0) 위에 한 장.
            Assert.Less(dst, WindowAlphaProbe.RequiredWindowAlpha,
                $"{LogPrefix} 옛 알파 {OldAlpha}로도 창 알파가 {dst:F3}로 통과해 버립니다 — " +
                "그렇다면 위 테스트는 이 결함을 잡을 수 없습니다.");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator PostItUsesChromeTokensInsteadOfRawLiterals()
        {
            yield return ShowCardWithOneTodo();

            RectTransform panel = FindPanel(_widget);
            Assert.IsNotNull(panel, $"{LogPrefix} PostItPanel을 찾지 못했습니다.");

            // (1) 본체는 다른 창과 같은 PanelSurface여야 한다.
            Image body = null;
            Image stripe = null;
            foreach (Image img in panel.GetComponentsInChildren<Image>(true))
            {
                if (img.name.EndsWith("Body")) body = img;
                if (img.name == "AccentStripe") stripe = img;
            }
            Assert.IsNotNull(body, $"{LogPrefix} 본체(Body) 이미지를 찾지 못했습니다 — AddOpaquePanel 구조가 아닙니다.");
            Assert.AreEqual(UiChrome.PanelSurface, body.color,
                $"{LogPrefix} 본체 색이 PanelSurface가 아닙니다({body.color}).");

            // (2) 정체성(노란 포스트잇)은 표면이 아니라 왼쪽 띠가 진다.
            Assert.IsNotNull(stripe, $"{LogPrefix} 좌측 강조 띠가 없습니다 — '이건 메모다'라는 단서가 사라집니다.");
            Assert.AreEqual(1f, stripe.color.a, 0.001f, $"{LogPrefix} 강조 띠까지 반투명이면 같은 결함이 남습니다.");

            // (3) ★ 2026-09-02 뒤집힘 — 사용자 지시 "그림자들이 있는데 다 없애줘 깔끔하게".
            //   <b>이름으로 세지 않는다</b>: 이름만 바꾼 잔재를 못 잡기 때문이다. 그림자를 그 <b>생김새</b>
            //   (거의 검은 반투명 겹)로 판정한다 — 실제 그림자는 예외 없이 이 모양이다.
            var shadowLike = new List<string>();
            foreach (Image img in panel.GetComponentsInChildren<Image>(true))
            {
                Color c = img.color;
                if (c.a <= 0.02f || c.a >= 0.999f) continue;       // 투명 히트영역/불투명 면은 그림자가 아니다
                if (UiChrome.RelativeLuminance(c) > 0.05f) continue; // 어두운 겹만
                shadowLike.Add($"{img.name}(α={c.a:F2})");
            }
            Assert.IsEmpty(shadowLike,
                $"{LogPrefix} 그림자로 보이는 어두운 반투명 겹이 남아 있습니다: {string.Join(", ", shadowLike)}");

            // (4) 글자 크기는 전부 UiChrome 계단 위에 있어야 한다(생 14/12 리터럴 금지).
            var ladder = new HashSet<int>
            {
                UiChrome.FontDisplay, UiChrome.FontTitle, UiChrome.FontBody,
                UiChrome.FontLabel, UiChrome.FontCaption,
            };
            foreach (Text t in panel.GetComponentsInChildren<Text>(true))
            {
                Assert.IsTrue(ladder.Contains(t.fontSize),
                    $"{LogPrefix} '{t.name}'의 글자 크기가 {t.fontSize}pt입니다 — UiChrome 계단 밖의 값입니다.");
                Assert.AreEqual(1f, t.color.a, 0.001f,
                    $"{LogPrefix} '{t.name}'의 글자 알파가 {t.color.a:F2}입니다 — 글자를 알파로 흐리면 " +
                    "그 글자 위에서만 창 알파가 내려가 데스크톱이 비칩니다(위계는 색과 크기가 져야 합니다).");
            }

            // (5) 버튼 라벨에 대괄호를 쓰지 않는다(앱의 다른 어떤 버튼도 안 쓴다).
            foreach (Text t in panel.GetComponentsInChildren<Text>(true))
            {
                StringAssert.DoesNotContain("[", t.text,
                    $"{LogPrefix} '{t.name}'의 문구 \"{t.text}\"에 대괄호가 남아 있습니다.");
            }
        }
    }
}
