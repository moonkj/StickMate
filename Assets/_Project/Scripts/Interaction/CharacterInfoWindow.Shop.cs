using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// <b>준비 중 페이지</b> — <see cref="CharacterInfoWindow.TabPage.Placeholder"/>인 탭의 본문.
    /// 2026-09-02 현재 그런 탭은 <c>[상점]</c> 하나뿐이다.
    ///
    /// <para>★ <b>왜 빈 껍데기인가</b>: 상점의 화면 설계는 <c>docs/UX_SHOP_AND_CURRENCY.md</c>에 있지만
    /// 그 수치가 <c>design/systems/ECONOMY_SPEC.md</c>와 <b>7건 충돌</b>한 채 미해결이다
    /// (임계 6/12/18 vs 10/20/32, 가격 30~330 vs 600~3,200 등 — 총괄 검토 §1-A).
    /// 지금 가격표를 그리면 <b>화면이 아직 정해지지 않은 값을 주장</b>하게 되고, 그건 이 저장소가
    /// 원칙 1로 금지한 것이다. 그래서 이 라운드는 <b>탭의 자리</b>만 만든다.</para>
    ///
    /// <para>이 페이지는 상점 전용이 아니다 — 표에서 <c>Notice</c>를 받아 그대로 적는다.
    /// 다음에 또 "자리는 있고 내용은 다음 라운드"인 탭이 생기면 표에 한 줄만 더한다.</para>
    /// </summary>
    public sealed partial class CharacterInfoWindow
    {
        private GameObject _placeholderPage;
        private Text _placeholderNotice;

        /// <summary>준비 중 문구가 앉는 상자의 높이 — 카드 페이지가 섹션에 쓰는 세로 예산과 같다.
        /// 문구는 그 안에서 가운데 정렬이라, 탭을 오갈 때 문구가 위아래로 튀지 않는다.</summary>
        private static float PlaceholderNoticeHeight => SectionCount * SectionStep;

        private void BuildPlaceholderPage(RectTransform right)
        {
            var pageGo = new GameObject("PlaceholderPage", typeof(RectTransform));
            pageGo.transform.SetParent(right, false);
            var page = pageGo.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(page, 0f, 0f, RightWidth, BodyHeight);
            _placeholderPage = pageGo;

            _placeholderNotice = Label(page, "Notice", UiChrome.FontTitle, TextAnchor.MiddleCenter,
                UiChrome.InkMeta, RightPadX, SectionsTopY, RightContentWidth, PlaceholderNoticeHeight,
                string.Empty);
            _placeholderNotice.raycastTarget = false;
        }

        /// <summary>문구는 <b>탭이 정해진 뒤 그 탭에서 파생</b>한다 — 페이지를 켜면서 문구를 따로
        /// 기억해 두면 둘이 어긋날 수 있다(행동-텍스트 싱크와 같은 이유).</summary>
        private void ApplyPlaceholderPage(bool visible)
        {
            if (_placeholderPage == null) return;
            if (_placeholderPage.activeSelf != visible) _placeholderPage.SetActive(visible);
            if (!visible || _placeholderNotice == null) return;

            string notice = Def(_tab).Notice ?? string.Empty;
            if (!string.Equals(_placeholderNotice.text, notice, System.StringComparison.Ordinal))
            {
                _placeholderNotice.text = notice;
            }
        }

        /// <summary>준비 중 페이지가 지금 말하고 있는 문구(진단/테스트 전용). 페이지가 꺼져 있으면 빈 문자열.</summary>
        public string PlaceholderNoticeForTests
            => _placeholderPage != null && _placeholderPage.activeSelf && _placeholderNotice != null
                ? _placeholderNotice.text
                : string.Empty;
    }
}
