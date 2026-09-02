using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 2026-09-01 사용자 신고 회귀 — <b>"장비카드가 어설픈데서 절반 짤려있어서 더 이상함.
    /// 좀더 오른쪽까지 채워져야함"</b>.
    ///
    /// ============================================================================
    /// 무엇이 틀렸었나 — 걸침은 옳았고 <b>걸리는 자리</b>가 틀렸다
    /// ============================================================================
    /// 같은 날 오전에 캐러셀 뷰포트를 섹션보다 좁게(520.5) 만들어 마지막 카드를 반쯤 걸치게 했다.
    /// 의도는 "오른쪽에 더 있다"였다. 그런데 자르는 선이 오른쪽 열의 <b>어떤 모서리도 아닌 허공</b>
    /// 이었다: 바로 위 헤더의 "n / 6" 카운터는 열 끝까지 가는데 카드줄만 71.5pt 앞에서 멈췄다.
    /// 모서리에 걸리지 않은 절단면은 "계속된다"가 아니라 <b>"깨졌다"</b>로 읽힌다.
    ///
    /// ============================================================================
    /// 왜 이 단언인가 — 숫자가 아니라 <b>두 요소의 관계</b>
    /// ============================================================================
    /// 592·520.5·161 같은 값을 여기 적으면 다음 라운드가 레이아웃을 고칠 때 이 파일이 프로덕션이
    /// 아니라 옛 숫자를 지키게 된다(CLAUDE.md). 그래서 <b>카드줄의 오른쪽 끝</b>과 <b>바로 위 헤더의
    /// 오른쪽 끝</b>이 같은 선인지만 묻는다. 카드 폭·간격·개수를 어떻게 바꾸든 이 관계는 유지돼야 한다.
    ///
    /// <para>두 사각형 모두 <b>잘리기 전</b>(raw) 값이라 배치모드의 좁은 화면에서
    /// <c>ClampPanelToScreen</c>이 창을 줄여도 이 단언은 흔들리지 않는다 — 그 클램프는 마스크가
    /// 만드는 잘림이지 레이아웃이 만드는 잘림이 아니다.</para>
    /// </summary>
    public sealed class InfoWindowCardRowEdgeTests
    {
        private const string LogPrefix = "[카드줄끝선-TEST]";

        /// <summary>같은 선으로 인정하는 오차(pt). 서브픽셀 반올림만 허용한다.</summary>
        private const float EdgeTolerancePoints = 1f;

        private CharacterInfoWindow _window;

        [UnityTearDown]
        public IEnumerator CloseWindow()
        {
            if (_window != null && _window.IsOpen) _window.Close("테스트 정리");
            _window = null;
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            yield return null;
        }

        private IEnumerator OpenWindow()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _window = Object.FindFirstObjectByType<CharacterInfoWindow>();
            Assert.IsNotNull(_window, $"{LogPrefix} 씬에 CharacterInfoWindow가 없습니다.");

            _window.Toggle("테스트");
            Assert.IsTrue(_window.IsOpen, $"{LogPrefix} 창이 열리지 않았습니다.");
            yield return null;
            yield return null;   // HorizontalLayoutGroup/ContentSizeFitter가 한 번 돌 기회를 준다.
        }

        /// <summary>★ 카드줄은 <b>자기 헤더와 같은 선</b>에서 끝나야 한다 — 이 신고의 본체.</summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator CardRowEndsOnTheSameRightEdgeAsItsHeader()
        {
            yield return OpenWindow();

            for (int s = 0; s < 2; s++)   // [장비] 탭에서 눈에 먼저 들어오는 두 줄이면 충분하다.
            {
                Rect row = _window.CarouselRowScreenRect(s);
                Rect header = _window.SectionCountScreenRect(s);
                Assert.Greater(row.width, 1f, $"{LogPrefix} {s}번 카드줄의 사각형이 비었습니다.");
                Assert.Greater(header.width, 1f, $"{LogPrefix} {s}번 헤더 카운터의 사각형이 비었습니다.");

                // ★ 2026-09-02 — 이 메시지가 <b>읽는 사람을 반대로 몰았다.</b> 예전 문장은 부호와 무관하게
                //   "카드줄이 헤더보다 …안쪽에서 끝납니다"라고 단정했는데, 실제로 터진 것은 그 반대
                //   (헤더가 592, 카드줄이 754)였다. 그래서 검증 담당이 "카드줄이 폭을 못 받았다"로 읽고
                //   엉뚱한 곳을 파게 됐다. 이제 <b>어느 쪽이 짧은지</b>를 말로 밝히고 두 xMax를 함께 찍는다 —
                //   부호를 거꾸로 읽을 여지를 남기지 않는다.
                float dead = header.xMax - row.xMax;
                string which = dead < 0f
                    ? "헤더('n / 6')가 카드줄보다 왼쪽에서 먼저 끝납니다(헤더가 짧다)"
                    : "카드줄이 헤더('n / 6')보다 왼쪽에서 먼저 끝납니다(카드줄이 짧다)";
                Assert.LessOrEqual(Mathf.Abs(dead), EdgeTolerancePoints,
                    $"{LogPrefix} {s}번 줄의 오른쪽 끝선이 {Mathf.Abs(dead):F1}pt 어긋납니다 — {which}. " +
                    $"[카드줄 xMax={row.xMax:F1} / 헤더 xMax={header.xMax:F1}] " +
                    "잘린 카드가 아무 모서리에도 걸리지 않으면 \"오른쪽에 더 있다\"가 아니라 " +
                    "\"카드가 깨졌다\"로 읽힙니다(2026-09-01 사용자 신고: \"어설픈데서 절반 짤려있어서 더 이상함\"). " +
                    "둘 다 RightContentWidth에서 파생돼야 합니다 — 한쪽만 숫자로 박혀 있으면 창 폭이 바뀔 때 " +
                    "정확히 이 증상이 납니다(SectionCountX / CarouselViewportWidth 문서 참고).");
            }
        }

        /// <summary>★ 그 끝선에는 <b>반쯤 걸친 카드가 하나</b> 있어야 한다 — 온전히 딱 맞아떨어지면
        /// 다시 "이게 전부"가 된다(페르소나 M1). 위 테스트와 <b>짝</b>이다: 이 둘을 같이 통과해야만
        /// "모서리에 걸린 걸침"이 성립한다.</summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ThatEdgeCutsExactlyOneCardInHalf()
        {
            yield return OpenWindow();

            Rect row = _window.CarouselRowScreenRect(0);
            int full = 0, part = 0, hidden = 0;
            for (int i = 0; i < _window.CardCountForTests; i++)
            {
                if (_window.CardSectionForTests(i) != 0 || !_window.IsCardVisibleForTests(i)) continue;
                Rect card = _window.CardRawScreenRect(i);
                float overlap = Mathf.Min(card.xMax, row.xMax) - Mathf.Max(card.xMin, row.xMin);
                float ratio = card.width > 0f ? Mathf.Clamp01(overlap / card.width) : 0f;
                if (ratio >= 0.98f) full++;
                else if (ratio > 0.02f) part++;
                else hidden++;
            }

            Assert.AreEqual(1, part,
                $"{LogPrefix} 끝선에 걸친 카드가 {part}장입니다(온전 {full} / 걸침 {part} / 밖 {hidden}) — " +
                "0장이면 \"이게 전부\"로 읽히고, 2장 이상이면 뷰포트와 카드 리듬이 어긋난 것입니다.");
            Assert.Greater(full, 0, $"{LogPrefix} 온전히 보이는 카드가 하나도 없습니다.");
            Assert.Greater(hidden, 0,
                $"{LogPrefix} 줄 밖으로 나간 카드가 0장입니다 — 밀 것이 없으면 걸침이 지킬 것도 없습니다.");
        }
    }
}
