using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// 치수 — 캔버스 배율, 화면 클램프, 탭별 창 높이 애니메이션, 도달성/차단막 동기화.
    /// <para>2026-09-02 <see cref="CharacterInfoWindow"/> 3,556줄을 <c>partial</c>로 나눈 조각이다.
    /// <b>분할은 줄 단위로 그대로 옮겼다</b>(옮기기 전후로 코드 줄 집합이 동일함을 확인).
    /// 그 뒤 같은 라운드에서 탭 판정만 <see cref="CharacterInfoWindow.TabTable"/> 기반으로 바꿨다.</para>
    /// </summary>
    public sealed partial class CharacterInfoWindow
    {
        private void ApplyCanvasScaleFactor()
        {
            if (_scaler == null) return;
            float target = ScreenCoordinateConverter.ResolveCanvasScaleFactor(_config);
            if (!Mathf.Approximately(_scaler.scaleFactor, target)) _scaler.scaleFactor = target;
            ClampPanelToScreen(target);
            EnsurePortraitTexture(force: false);
        }

        /// <summary>이보다 더 줄이면 컬럼 1(306)조차 담지 못한다 — 세로 하한과 같은 값으로 맞췄다.</summary>
        private const float MinPanelWidth = 320f;
        private const float MinPanelHeight = 320f;

        /// <summary>작은 화면에서 창이 화면 밖으로 나가지 않게 <b>가로·세로 모두</b> 줄인다.
        /// 예전에는 세로만 줄이고 폭은 항상 880이라 640폭 화면에서 좌우로 각각 120pt씩 흘러나갔다
        /// (2026-08-30 디버거 실측). 잘리는 것은 본문 오른쪽/아래쪽이고 <see cref="RectMask2D"/>가
        /// 패널 밖으로 삐져나오는 그림을 막는다(타이틀바의 [✕]/구분선은 패널 폭을 따라가게 앵커를
        /// 오른쪽/양끝에 걸어 뒀다 — 안 그러면 그 둘만 창 밖에 떠 있게 된다).
        /// 33-7-9가 적어 둔 "[▲][▼] 2섹션 페이지 모드" 폴백은 아직 없다.
        /// 크기를 줄인 뒤에는 드래그로 옮겨 둔 자리도 다시 화면 안으로 끌어들인다.</summary>
        private void ClampPanelToScreen(float scaleFactor)
        {
            if (_panel == null || scaleFactor <= 0f) return;
            float height = Mathf.Min(PanelHeight, Mathf.Max(MinPanelHeight, Screen.height / scaleFactor - ScreenMargin * 2f));
            float width = Mathf.Min(PanelWidth, Mathf.Max(MinPanelWidth, Screen.width / scaleFactor - ScreenMargin * 2f));
            if (!Mathf.Approximately(_panel.sizeDelta.x, width) || !Mathf.Approximately(_panel.sizeDelta.y, height))
            {
                _panel.sizeDelta = new Vector2(width, height);
                SyncColumnLayout(width);
                SyncActionReachability();
            }

            Vector2 clamped = ClampPanelPosition(_panel.anchoredPosition, scaleFactor);
            if (clamped != _panel.anchoredPosition) _panel.anchoredPosition = clamped;
        }

        // ★ L-8 — 여기 있던 TickPanelHeight()/ApplyTabDetailPlacement()를 <b>지웠다</b>.
        //   창 높이가 탭마다 달라지던 시절의 장치였고(861 ↔ 705), 3컬럼에서는 인계본 규칙대로
        //   "창 크기와 헤더는 탭에 따라 변하지 않는다"가 된다. 세로 애니메이션도 함께 사라졌다 —
        //   움직이지 않는 값을 부드럽게 옮길 이유가 없다.
        //
        //   ★ 세로가 728pt 미만인 화면의 강등 사다리(컬럼 1 세로 스크롤)는 <b>아직 없다</b>.
        //     지금은 Body의 RectMask2D가 아래를 자르고, 상세 카드가 컬럼 1 바닥에 붙어 있어
        //     그 화면에서는 상세 카드가 먼저 잘린다. 착용 경로는 컬럼 3(항상 스크롤 가능)의 카드
        //     버튼이 전담하므로 도달성은 유지된다(문서 §3-6).

        /// <summary>창 중심이 화면 밖으로 나가지 않는 범위로 자른다 — 드래그와 화면 크기 변화가
        /// <b>같은 규칙</b>을 쓴다. 좌표계는 화면 중앙 원점이고, 창이 화면만큼 커지면 이동량은 0이 된다.</summary>
        private Vector2 ClampPanelPosition(Vector2 desired, float scaleFactor)
        {
            if (_panel == null || scaleFactor <= 0f) return desired;
            float sf = scaleFactor;
            Vector2 size = _panel.sizeDelta;
            float maxX = Mathf.Max(0f, (Screen.width / sf - size.x) * 0.5f - ScreenMargin);

            // ★ 2026-09-02 (41-1) — 세로는 <b>대칭이 아니다</b>. 옛 코드의 대칭 클램프는 이 창을 위로
            //   44.5pt 끌어올리게 허용했고, 그러면 창 위쪽이 OS y=16pt에 앉아 macOS 메뉴바(0~33)를
            //   17pt 덮는다(팝오버와 같은 결함, 같은 원인).
            //
            // ★★ 2026-09-05 (M-7, 리더 판정) — 아래쪽 한계는 <b>플랫폼마다 다르다</b>.
            //   macOS: 0 — Dock을 덮는 것은 macOS의 모든 앱이 하는 표준 동작이고, 이 앱은 그 위를
            //          발판으로도 쓴다(예전과 비트 단위로 동일하다).
            //   Windows: 작업표시줄 두께만큼 좁아진다 — 드래그로 창을 작업표시줄 위에 겹쳐 둘 수 없다.
            float topInset = ReservedTopBarProbe.TopInsetPoints(_agent != null ? _agent.PlatformService : null);
            float bottomInset =
                ReservedEdgeProbe.EnforcedBottomInsetPoints(_agent != null ? _agent.PlatformService : null);
            float y = SurfaceSafeAreaPolicy.ClampCenterOriginOffsetY(
                desired.y, size.y, Screen.height / sf, topInset, bottomInset, ScreenMargin);
            return new Vector2(Mathf.Clamp(desired.x, -maxX, maxX), y);
        }

        /// <summary>
        /// ★ 가로 강등 사다리 — 창이 설계 폭 1042보다 좁아지면 <b>컬럼 2 → 컬럼 1 순서로 접고</b>
        /// 컬럼 3(카드)에 남은 폭을 전부 준다. 근거는 <see cref="_gridX"/> 문서에 있다.
        ///
        /// <para>헤더 오른쪽 칩들도 같은 이유로 함께 접힌다 — 오른쪽 앵커라 창이 좁아지면 왼쪽 앵커인
        /// 탭 스트립 위로 <b>올라탄다</b>. 그러면 탭을 눌렀는데 [설정]이 열리는 오배선이 생긴다.
        /// <b>[✕]는 절대 접지 않는다</b> — 이 창의 유일한 탈출구다.</para>
        ///
        /// <para>창 크기가 <b>바뀔 때만</b> 불린다(<see cref="ClampPanelToScreen"/>).</para>
        /// </summary>
        private void SyncColumnLayout(float panelWidth)
        {
            // 접는 순서: 컬럼 2 → 컬럼 1 → (그래도 모자라면) 카드 2열 → 1열.
            _showCol1 = panelWidth >= Col1Width + MinGridColumnWidth;                 // 750
            _showCol2 = _showCol1 && panelWidth >= Col3X + MinGridColumnWidth;        // 1042

            _gridX = _showCol2 ? Col3X : _showCol1 ? Col1Width : 0f;
            _gridWidth = Mathf.Max(MinSingleColumnGridWidth, panelWidth - _gridX);
            _gridContentWidth = _gridWidth - Col3PadX * 2f - Col3ScrollbarInset;
            _gridColumns = _gridContentWidth >= CardWidth * 2f + CardGap ? 2 : 1;

            if (_sectionPage != null)
            {
                UiChrome.PlaceTopLeft(_sectionPage.GetComponent<RectTransform>(), _gridX, 0f,
                    _gridWidth, BodyHeight);
            }

            ApplyColumnVisibility();
            SyncHeaderChips(panelWidth);
            RefreshCards();   // 열 수·헤더 폭이 바뀌면 좌표를 다시 잡아야 한다.
        }

        /// <summary>컬럼 1·2가 지금 보이는가 — 탭 종류(카드 탭인가)와 폭(<see cref="SyncColumnLayout"/>)
        /// 두 조건의 <b>곱</b>이다. 두 곳에서 각각 <c>SetActive</c>를 부르면 나중 호출이 앞을 덮는다.</summary>
        private void ApplyColumnVisibility()
        {
            bool cards = Def(_tab).Page == TabPage.Cards;
            if (_col1Root != null) _col1Root.SetActive(cards && _showCol1);
            if (_col2Root != null) _col2Root.SetActive(cards && _showCol2);
        }

        /// <summary>헤더 오른쪽 칩이 <b>탭 스트립을 침범하면 그 칩을 끈다</b>.
        /// <para>칩은 오른쪽 앵커, 탭은 왼쪽 앵커라 창이 좁아지면 서로 올라탄다. 그러면 탭을 눌렀는데
        /// [설정]이 열리는 오배선이 생긴다(폴링 히트테스트는 겹친 사각형 중 <b>먼저 검사한 것</b>을
        /// 집는다). <b>[✕]는 목록에 없다 — 이 창의 유일한 탈출구라 접지 않는다.</b></para>
        /// <para>인셋은 <see cref="BuildHeader"/>가 칩을 놓을 때 쓴 값과 <b>같은 식</b>이어야 한다 —
        /// 칩을 꺼도 나머지가 자리를 옮기지는 않으므로, 각 칩의 고정 자리로 판정한다.</para></summary>
        private void SyncHeaderChips(float panelWidth)
        {
            float limit = _tabStripRightEdge + UiChrome.Space4;
            SetChipVisible(_settingsRect, panelWidth - HeaderSettingsChipInset - HeaderSettingsChipWidth >= limit);
            SetChipVisible(_coinChipRect, panelWidth - HeaderCoinChipInset - HeaderCoinChipWidth >= limit);
            SetChipVisible(_ownedChipRect, panelWidth - HeaderOwnedChipInset - HeaderOwnedChipWidth >= limit);
        }

        private static void SetChipVisible(RectTransform chip, bool visible)
        {
            if (chip != null && chip.gameObject.activeSelf != visible) chip.gameObject.SetActive(visible);
        }

        /// <summary>
        /// 화면이 낮아 [착용]/[해제] 버튼이 <b>하나도 남김없이</b> 잘리면 한 번만 경고한다. 클릭은 이미
        /// <see cref="ContainsScreenPoint"/>가 막으므로 "안 보이는데 눌린다"는 없어졌지만, 그 화면에서는
        /// 아이템을 갈아입을 수단 자체가 사라진다는 사실은 조용히 넘길 일이 아니다(33-7-9 페이지 폴백 미구현).
        ///
        /// <para>★ 2026-09-01 — 감시 대상을 상세 패널 버튼에서 <b>카드 하단 버튼들</b>로 옮겼다. 상세 패널의
        /// 중복 버튼을 걷어내면서 착용 경로가 카드 버튼뿐이 됐기 때문이다. <b>하나라도</b> 보이면 아직
        /// 갈아입을 수 있으므로 경고하지 않는다.</para>
        ///
        /// <para>창 크기가 <b>바뀔 때만</b> 불린다(<see cref="ClampPanelToScreen"/>) — 카드 수만큼 도는
        /// 이 루프를 매 프레임 경로에 두면 상주 앱 규약을 어긴다.</para>
        /// </summary>
        private void SyncActionReachability()
        {
            bool anyActive = false;
            bool anyReachable = false;
            for (int i = 0; i < _cards.Length; i++)
            {
                ItemCard card = _cards[i];
                if (card == null || card.ActionRect == null) continue;
                if (!card.ActionRect.gameObject.activeInHierarchy) continue;
                anyActive = true;
                if (CardEquipButtonVisibleFraction(i) > 0f) { anyReachable = true; break; }
            }

            bool unreachable = anyActive && !anyReachable;
            if (unreachable == _actionUnreachable) return;
            _actionUnreachable = unreachable;
            if (!unreachable) return;

            Debug.LogWarning("[정보창] 화면 세로가 짧아 카드의 [착용] 버튼이 전부 가려졌습니다 — " +
                             "그 자리를 눌러도 반응하지 않습니다(보이지 않는 것은 눌리지 않는다). " +
                             "33-7-9의 [▲][▼] 페이지 폴백이 들어오기 전까지는 창을 띄울 세로 공간이 더 필요합니다.");
        }

        /// <summary>창이 보이는 동안만 창 사각형을 덮는 히트테스트용 콜라이더를 켠다(TodoPostItWidget과
        /// 같은 관례 — isTrigger라 캐릭터 물리에는 전혀 관여하지 않는다).</summary>
        private void SyncClickBlocker()
        {
            if (_clickBlocker == null || _panel == null) return;
            Camera cam = _agent != null && _agent.Blackboard != null ? _agent.Blackboard.MainCamera : Camera.main;
            if (cam == null) { _clickBlocker.enabled = false; return; }

            _panel.GetWorldCorners(_corners);
            float depth = Mathf.Abs(cam.transform.position.z);
            Vector3 bl = cam.ScreenToWorldPoint(new Vector3(_corners[0].x, _corners[0].y, depth));
            Vector3 tr = cam.ScreenToWorldPoint(new Vector3(_corners[2].x, _corners[2].y, depth));

            _clickBlocker.enabled = true;
            _clickBlocker.transform.position = new Vector3((bl.x + tr.x) * 0.5f, (bl.y + tr.y) * 0.5f, 0f);
            _clickBlocker.size = new Vector2(Mathf.Abs(tr.x - bl.x), Mathf.Abs(tr.y - bl.y));
        }
    }
}
