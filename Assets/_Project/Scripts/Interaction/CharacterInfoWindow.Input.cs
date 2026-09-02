using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// 입력 — 전역 폴링, 캐러셀/타이틀바 드래그, 클릭 라우팅, hover, 마스크 히트테스트.
    /// <para>2026-09-02 <see cref="CharacterInfoWindow"/> 3,556줄을 <c>partial</c>로 나눈 조각이다.
    /// <b>분할은 줄 단위로 그대로 옮겼다</b>(옮기기 전후로 코드 줄 집합이 동일함을 확인).
    /// 그 뒤 같은 라운드에서 탭 판정만 <see cref="CharacterInfoWindow.TabTable"/> 기반으로 바꿨다.</para>
    /// </summary>
    public sealed partial class CharacterInfoWindow
    {
        // ==================== 클릭 경로 3: 전역 폴링 ====================

        private void TickGlobalPointer()
        {
            if (_buttonService == null || _panel == null) return;

            // 홀드 판정도 이 가드 뒤에 있다 — 전역 포인터 서비스가 없으면 커서를 관측할 수단이
            // 자체가 없다. 그 환경(에디터/Null 서비스)에서는 적응형 페이싱도 함께 꺼져 있으므로
            // 홀드가 없어서 생기는 손해가 없다.

            // 드래그 중에만 폴링 간격을 없앤다 — 20Hz로 창을 끌면 커서에서 창이 뚝뚝 끊겨 떨어진다.
            // 평소에는 예전 그대로 ClickPollInterval(0.05초)로 눌러 둔다(하루 종일 켜져 있는 앱이다).
            if (!_draggingPanel && _carouselSection < 0)
            {
                _clickPollTimer += Time.unscaledDeltaTime;
                if (_clickPollTimer < ClickPollInterval) return;
                _clickPollTimer = 0f;
            }

            Vector2 osScreen = Vector2.zero;
            bool hasCursor = _agent != null && _agent.TryGetCursorPosition(out osScreen);
            Vector2 cursor = hasCursor
                ? ScreenCoordinateConverter.OsScreenToUnityScreen(osScreen, _config)
                : Vector2.zero;
            TickFramePacingHold(hasCursor, cursor);

            // 끄는 중에는 카드를 다시 칠하지 않는다(패널 이동도, 캐러셀 밀기도 마찬가지다).
            if (hasCursor && !_draggingPanel && !_carouselMoved) UpdateHover(cursor);

            if (!_buttonService.TryGetPrimaryButtonPressed(out bool left)) return;
            ProcessPointer(left, cursor, hasCursor);
        }

        /// <summary>
        /// "지금 이 창을 <b>조작 중</b>인가"를 프레임 페이싱에 알린다. 판정은 플랫폼 중립 한 곳
        /// (<see cref="FramePacingPolicy.ShouldHoldForSurface"/>)이고 여기서는 사실만 모은다.
        ///
        /// <para><b>★ 2026-09-01 — 원래 이 홀드는 <c>Update()</c>에서 무조건 걸려 있었고, 그것이
        /// 적응형 절전을 통째로 죽였다.</b> 사용자 로그에서 정보창이 <b>125분</b> 열려 있는 동안
        /// 등급 전이가 0회 / 활성 등급 체류 100%였고, 창을 닫은 직후 전이가 재개되며 GPU 점유 추정이
        /// 약 2.5배 떨어졌다. "정보창은 수명이 짧다"는 전제가 실측으로 반증된 것이다. 자리비움(3분
        /// 무입력)이 이 홀드를 이기게 돼 있지만, 사용자가 <b>다른 앱에서 계속 타이핑</b>하면 그
        /// 시계는 3분에 닿지 않는다 — 홀드를 깨는 경로가 실질적으로 없었다.</para>
        ///
        /// <para>반응성을 해치지 않는 근거(왜 이 경계인가)는 정책 함수 문서에 있다. 요약하면:
        /// 절감 등급은 게임 루프가 아니라 렌더 간격만 바꾸므로 <b>입력 처리 주기는 그대로</b>이고,
        /// 커서가 창에 닿는 순간의 복귀 지연이 최대 0.07초(폴링 0.05초 + 1프레임)다.</para>
        /// </summary>
        private void TickFramePacingHold(bool hasCursor, Vector2 cursor)
        {
            // 커서가 창 밖으로 나가도 계속되는 조작들 — 이것들은 사각형 판정으로 잡을 수 없다.
            bool manipulating = _draggingPanel || _carouselSection >= 0 || _editingName;
            bool cursorOver = hasCursor && RectContainsScreenPoint(_panel, cursor);
            if (manipulating || cursorOver) _lastSurfaceTouchTime = Time.unscaledTime;

            if (FramePacingPolicy.ShouldHoldForSurface(cursorOver, manipulating,
                    Time.unscaledTime - _lastSurfaceTouchTime))
            {
                FramePacing.HoldActiveForInteraction();
            }
        }

        /// <summary>
        /// 실제 입력과 테스트가 <b>공유하는</b> 포인터 처리(InfoGearIconWidget.ProcessPointer와 같은 관례).
        /// 누름 = 타이틀바 드래그 시작 또는 클릭 처리, 누른 채 이동 = 창 이동, 뗌 = 드래그 종료.
        /// </summary>
        private void ProcessPointer(bool buttonDown, Vector2 cursor, bool hasCursor)
        {
            bool prev = _leftPrev;
            if (!_leftInitialized)
            {
                // 창을 여는 그 클릭이 곧바로 카드 클릭/드래그로 오인되지 않게 첫 표본은 버린다.
                _leftInitialized = true;
                _leftPrev = buttonDown;
                return;
            }
            _leftPrev = buttonDown;

            if (buttonDown && !prev)
            {
                if (!hasCursor) return;
                if (TryBeginPanelDrag(cursor)) return;   // 타이틀바를 잡았으면 클릭 처리로 넘기지 않는다.

                // 캐러셀은 <b>잡아만 둔다</b> — 누름을 삼키지 않는다. 삼키면 카드를 한 번 눌러
                // 고르는 것 자체가 불가능해진다(대부분의 누름은 드래그가 아니라 클릭이다).
                ArmCarouselDrag(cursor);
                FeedClick(cursor);
                return;
            }
            if (buttonDown)
            {
                if (!hasCursor) return;
                if (_draggingPanel) DragPanelTo(cursor);
                else DragCarouselTo(cursor);
                return;
            }
            if (!buttonDown && prev)
            {
                ResolvePendingEquip(cursor, hasCursor);
                EndCarouselDrag();
                EndPanelDrag();
            }
        }

        // ==================== 가로 카드 캐러셀 (2026-09-01) ====================
        //
        // 배치·클램프·휠은 uGUI <see cref="ScrollRect"/>가 한다. 그런데 이 창의 <b>실제</b> 클릭 경로는
        // 전역 폴링이다(uGUI 이벤트는 앱이 활성화된 뒤에만 도착한다 — 타이틀바 드래그를 폴링으로 짠
        // 것과 같은 사정). 그래서 드래그도 한 벌 더 있다.
        //
        // 두 경로가 <b>싸우지 않는</b> 이유: 아래는 "잡은 순간의 content.x + 커서 이동량"이라는
        // <b>절대값</b> 공식이다. ScrollRect의 드래그도 같은 형태(시작 위치 + 이동량)이고 클램프도
        // 같으므로, 둘이 동시에 돌아도 계산 결과가 같다(더해지지 않는다). 그래서 관성(inertia)을
        // 끄고 MovementType을 Clamped로 둔다 — 탄성/감속이 붙는 순간 그 등식이 깨진다.

        private void ArmCarouselDrag(Vector2 cursor)
        {
            _carouselSection = -1;
            _carouselMoved = false;
            if (Def(_tab).Page != TabPage.Cards) return;   // 캐러셀은 카드 페이지에만 있다.

            int visible = SectionCountForTab(_tab);
            for (int s = 0; s < visible; s++)
            {
                SectionView view = _sections[s];
                if (view == null || view.Row == null || view.Content == null) continue;
                if (!ContainsScreenPoint(view.RowRect, cursor)) continue;

                _carouselSection = s;
                _carouselGrabScreenX = cursor.x;
                _carouselStartContentX = view.Content.anchoredPosition.x;
                return;
            }
        }

        private void DragCarouselTo(Vector2 cursor)
        {
            if (_carouselSection < 0) return;
            SectionView view = _sections[_carouselSection];
            if (view == null || view.Content == null || view.Row == null) return;

            float delta = (cursor.x - _carouselGrabScreenX) / CanvasScale();
            if (!_carouselMoved && Mathf.Abs(delta) < CarouselDragThresholdPoints) return;
            _carouselMoved = true;
            _lastCarouselMoveTime = Time.unscaledTime;

            Vector2 p = view.Content.anchoredPosition;
            p.x = Mathf.Clamp(_carouselStartContentX + delta, -MaxCarouselScroll(view), 0f);
            view.Content.anchoredPosition = p;
        }

        private void EndCarouselDrag()
        {
            _carouselSection = -1;
            _carouselMoved = false;
        }

        /// <summary>content가 왼쪽으로 밀려날 수 있는 최대치(양수). 카드가 뷰포트를 넘지 않으면 0이다.</summary>
        private static float MaxCarouselScroll(SectionView view)
        {
            if (view == null || view.Content == null || view.Row == null || view.Row.viewport == null) return 0f;
            return Mathf.Max(0f, view.Content.rect.width - view.Row.viewport.rect.width);
        }

        /// <summary>누름 때 보류해 둔 카드 착용을 <b>뗄 때</b> 확정한다. 미는 동안 손가락 아래로 지나간
        /// 카드가 눌리지 않도록, 밀었으면 취소하고 커서가 그 버튼 위에 남아 있을 때만 실행한다.</summary>
        private void ResolvePendingEquip(Vector2 cursor, bool hasCursor)
        {
            int pending = _pendingEquipCard;
            _pendingEquipCard = -1;
            if (pending < 0 || _carouselMoved || !hasCursor) return;

            ItemCard card = CardAt(pending);
            if (card == null || !card.Rect.gameObject.activeInHierarchy) return;
            if (!ContainsScreenPoint(card.ActionRect, cursor)) return;
            if (TryClaimAction("equip" + pending)) OnCardEquipClicked(pending);
        }

        /// <summary>방금 캐러셀을 민 직후인가 — uGUI <see cref="Button.onClick"/>(뗄 때 발동)이
        /// 스크롤의 마지막 손짓을 클릭으로 오인하지 않게 하는 유일한 관문.</summary>
        private bool SuppressedByCarousel()
            => Time.unscaledTime - _lastCarouselMoveTime < CarouselClickSuppressSeconds;

        // ==================== 타이틀바 드래그 (2026-08-30 — 33-7-7 결정의 일부 번복) ====================
        //
        // 33-7-7/34-7은 "화면 중앙 고정 모달"로 확정했고 드래그 코드는 처음부터 <b>없었다</b>(버그가
        // 아니라 미구현이었다). 사용자가 "끌면 옮겨져야 하는데 고정돼 있다"고 해서 리더가 뒤집었다 —
        // <b>열릴 때는 여전히 화면 중앙</b>에서 시작하고, 타이틀바를 잡은 동안만 옮길 수 있다.
        // 옮긴 자리는 기억하지 않는다(다음에 열면 다시 중앙 — "열면 중앙" 규칙을 그대로 지킨다).
        // 클릭 경로가 전역 폴링인 것과 같은 이유로 드래그도 전역 폴링을 쓴다(uGUI 이벤트는 앱이
        // 활성화된 뒤에만 도착한다 — 이 앱은 그 전제를 둘 수 없다).

        private bool TryBeginPanelDrag(Vector2 cursor)
        {
            if (_titleBarRect == null || _panel == null) return false;
            if (!RectContainsScreenPoint(_titleBarRect, cursor)) return false;
            if (RectContainsScreenPoint(_closeRect, cursor)) return false;   // [✕]는 버튼이지 손잡이가 아니다.
            if (RectContainsScreenPoint(_settingsRect, cursor)) return false; // [설정]도 마찬가지.

            // 잡은 지점과 창 중심의 차이를 기억한다 — 드래그가 시작될 때 창이 커서로 순간이동하지 않게.
            _dragGrabOffsetPoints = _panel.anchoredPosition - ScreenToPanelPoints(cursor, CanvasScale());
            _dragStartOffsetPoints = _panel.anchoredPosition;
            _draggingPanel = true;
            return true;
        }

        private void DragPanelTo(Vector2 cursor)
        {
            if (_panel == null) return;
            float sf = CanvasScale();
            _panel.anchoredPosition = ClampPanelPosition(ScreenToPanelPoints(cursor, sf) + _dragGrabOffsetPoints, sf);
        }

        private void EndPanelDrag()
        {
            if (!_draggingPanel) return;
            _draggingPanel = false;
            Vector2 p = _panel != null ? _panel.anchoredPosition : Vector2.zero;
            if ((p - _dragStartOffsetPoints).sqrMagnitude < 0.25f) return;   // 제자리 클릭은 이동이 아니다.
            Debug.Log($"[정보창] 이동 완료 — 화면 중앙에서 ({p.x:F0}, {p.y:F0})pt 옮긴 자리입니다. " +
                "다시 열면 중앙에서 시작합니다.");
        }

        /// <summary>화면 중앙을 원점으로 하는 캔버스 좌표(패널 anchoredPosition과 <b>같은 계</b>).</summary>
        private static Vector2 ScreenToPanelPoints(Vector2 cursorUnityScreen, float scaleFactor)
            => new Vector2((cursorUnityScreen.x - Screen.width * 0.5f) / scaleFactor,
                           (cursorUnityScreen.y - Screen.height * 0.5f) / scaleFactor);

        private float CanvasScale()
        {
            float sf = _scaler != null ? _scaler.scaleFactor : 1f;
            return sf > 0f ? sf : 1f;
        }

        private void ResetPanelToCenter()
        {
            _draggingPanel = false;
            if (_panel != null) _panel.anchoredPosition = Vector2.zero;
        }

        private static Rect RawScreenRectOf(RectTransform rt)
        {
            if (rt == null || rt.gameObject == null || !rt.gameObject.activeInHierarchy) return new Rect();
            rt.GetWorldCorners(_corners);
            return Rect.MinMaxRect(_corners[0].x, _corners[0].y, _corners[2].x, _corners[2].y);
        }

        private void FeedClick(Vector2 cursor)
        {
            if (ContainsScreenPoint(_settingsRect, cursor))
            {
                if (TryClaimAction("settings")) OpenSettings("정보창 헤더 [설정]");
                return;
            }

            if (ContainsScreenPoint(_closeRect, cursor))
            {
                if (TryClaimAction("close")) Close("[✕] 클릭");
                return;
            }

            if (ContainsScreenPoint(_nameRect, cursor) && !_editingName)
            {
                if (TryClaimAction("nameEdit")) BeginNameEdit();
                return;
            }
            for (int i = 0; i < _inkRects.Length; i++)
            {
                if (!ContainsScreenPoint(_inkRects[i], cursor)) continue;
                if (TryClaimAction("ink" + i)) OnInkSwatchClicked(i == 1);
                return;
            }

            for (int i = 0; i < _tabRects.Length; i++)
            {
                if (!ContainsScreenPoint(_tabRects[i], cursor)) continue;
                if (TryClaimAction("tab" + i)) OnTabClicked((Tab)i);
                return;
            }

            TabPage page = Def(_tab).Page;

            if (page == TabPage.Inventory)
            {
                // ★ 클릭 경로가 <b>둘</b>이다(Button.onClick + 이 폴링). 한쪽만 가드하면 다른 쪽이
                //   그대로 뚫린다 — 두 경로가 같은 CanScrollInventory를 본다(45-9-b ④).
                if (ContainsScreenPoint(_pageUpRect, cursor))
                {
                    if (CanScrollInventory(-1) && TryClaimAction("pageUp")) ScrollInventory(-1);
                    return;
                }
                if (ContainsScreenPoint(_pageDownRect, cursor))
                {
                    if (CanScrollInventory(+1) && TryClaimAction("pageDown")) ScrollInventory(+1);
                    return;
                }
                for (int i = 0; i < _inventoryViews.Length; i++)
                {
                    InventoryRowView view = _inventoryViews[i];
                    if (view == null || view.BoundCatalogIndex < 0) continue;
                    if (!ContainsScreenPoint(view.Rect, cursor)) continue;
                    if (TryClaimAction("inv" + i)) OnInventoryRowClicked(view.BoundCatalogIndex);
                    return;
                }
                return;
            }

            // 카드가 없는 탭([상점] 같은 준비 중 페이지)은 여기서 끝이다. 아래 카드 루프는
            // 숨겨진 카드를 activeInHierarchy로 걸러 내지만, 그건 <b>우연한 방어</b>다 —
            // 어떤 탭이 카드를 갖는지는 표가 정한다.
            if (page != TabPage.Cards) return;

            for (int i = 0; i < _cards.Length; i++)
            {
                ItemCard card = _cards[i];
                if (card == null || !card.Rect.gameObject.activeInHierarchy) continue;
                if (ContainsScreenPoint(card.ActionRect, cursor))
                {
                    // 누른 순간에는 아무 일도 하지 않는다 — 착용은 <b>뗄 때</b> 확정한다
                    // (그 사이에 카드를 밀었다면 그건 스크롤이다. _pendingEquipCard 문서 참고).
                    _pendingEquipCard = i;
                    return;
                }
                if (!ContainsScreenPoint(card.Rect, cursor)) continue;
                if (TryClaimAction("card" + i)) OnCardClicked(i);
                return;
            }

            // ★ 2026-09-02 사용자 지시 — 여기까지 왔다는 것은 어떤 컨트롤에도 맞지 않았다는 뜻이고,
            //   <b>패널 안이든 밖이든 아무 일도 하지 않는다</b>. 2026-08-30에 신설했던 "창 밖 클릭"
            //   탈출구(33-7-9 ③)를 사용자 신고로 걷어냈다: "캐릭터창이나 다른 메뉴창들이 떠있을때
            //   바탕화면을 클릭하면 꺼지는데 안꺼지고 사용자가 닫기전에는 안꺼져야함".
            //   근거와 그 대가는 <see cref="UiChrome"/>의 "창을 닫는 법" 절 한 곳에 모아 뒀다.
            //
            //   ★ 그 클릭을 <b>먹지는 않는다</b>: 차단막(<see cref="_clickBlocker"/>)은 패널 사각형만
            //     덮으므로 창 밖 좌표에는 콜라이더가 없고, 히트테스트(hitTestType=Raycast)가 그대로
            //     밑의 앱에 넘긴다. "안 닫히는 것"과 "클릭을 뺏는 것"은 다른 문제이고, 후자면 원칙 2
            //     위반이다(Tests/PlayMode/SurfaceOutsideClickTests가 그 경계를 픽셀로 잠근다).
        }

        /// <summary>
        /// 카드 hover(33-7-3). <b>있으면 좋은 것이지 필수가 아니다</b> — 이 앱의 uGUI 입력은 창을 클릭해
        /// 앱이 활성화된 뒤에만 정상 도착하고, 전역 커서 조회도 플랫폼에 따라 없을 수 있다.
        /// hover가 한 프레임도 오지 않아도 선택/착용은 클릭만으로 온전히 동작한다.
        /// 바뀐 프레임에만 테두리 두 장을 다시 칠한다(문자열/할당 없음).
        /// </summary>
        private void UpdateHover(Vector2 cursor)
        {
            // 카드가 없는 탭. 남아 있던 hover는 지우기만 하면 된다 — 카드가 숨겨져 있어
            // 다시 칠할 필요가 없고, 탭을 되돌아오면 RefreshCards가 -1 상태로 전부 다시 칠한다.
            if (Def(_tab).Page != TabPage.Cards)
            {
                _hoveredCard = -1;
                return;
            }

            int found = -1;
            for (int i = 0; i < _cards.Length; i++)
            {
                ItemCard card = _cards[i];
                if (card == null || !card.Rect.gameObject.activeInHierarchy) continue;
                if (!ContainsScreenPoint(card.Rect, cursor)) continue;
                found = i;
                break;
            }
            if (found == _hoveredCard) return;

            int previous = _hoveredCard;
            _hoveredCard = found;
            RestyleCard(previous);
            RestyleCard(found);
        }

        private void RestyleCard(int index)
        {
            ItemCard card = CardAt(index);
            if (card == null || !card.Rect.gameObject.activeSelf) return;
            if (Def(_tab).Page != TabPage.Cards) return;   // SectionSlot이 물을 수 없는 탭이다.
            ApplyCardStyle(card, SectionSlot(_tab, card.Section), card.Item, IconSetForTab(_tab));
        }

        private bool TryClaimAction(string key)
        {
            if (_lastActionKey == key && Time.unscaledTime - _lastActionTime < ActionDedupSeconds) return false;
            _lastActionKey = key;
            _lastActionTime = Time.unscaledTime;
            return true;
        }

        /// <summary>
        /// ScreenSpaceOverlay 캔버스에서는 RectTransform의 월드 좌표가 곧 스크린 픽셀 좌표다
        /// (TodoPostItWidget.ContainsScreenPoint와 같은 전제).
        /// <para>★ 2026-09-03 정정 — 여기 함께 적혀 있던 <c>AppControlDirector.HitTestMenuRow</c>는
        /// <b>존재하지 않는다.</b> 우클릭 제어 메뉴 UI가 통째로 삭제되면서 같이 없어졌다
        /// (docs/UX_FLOW.md 36-13 #6). 전제를 확인하러 그 이름을 찾아가면 빈손으로 돌아온다.</para>
        ///
        /// ★ 2026-08-30(R2 M3): <b>마스크에 잘린 자리는 눌리지 않는다.</b> 세로가 짧은 화면에서
        /// <see cref="ClampPanelToScreen"/>이 패널을 줄이면 본문 아래쪽([착용] 버튼 포함)이
        /// <see cref="RectMask2D"/>에 잘려 <b>화면에서 사라진다</b>. 그런데 이 전역 폴링 경로는
        /// 마스크를 모르는 순수 사각형 판정이라, 예전에는 보이지도 않는 버튼이 그대로 눌렸다 —
        /// 이 프로젝트가 "최악의 형태"라고 부르는 패턴이다(안 보이는데 클릭은 먹는 UI).
        /// uGUI 배선 쪽은 <see cref="RectMask2D"/>가 <c>ICanvasRaycastFilter</c>라 원래부터 막혀 있었고,
        /// 이 함수만 빠져 있었다. 부분적으로 잘린 컨트롤은 <b>보이는 부분만</b> 계속 눌린다.
        /// </summary>
        private bool ContainsScreenPoint(RectTransform rt, Vector2 screenPoint)
        {
            if (!RectContainsScreenPoint(rt, screenPoint)) return false;
            return IsUnclipped(rt, screenPoint);
        }

        /// <summary>마스크를 <b>보지 않는</b> 날 사각형 판정(마스크 사각형 자신을 잴 때 쓴다).</summary>
        private static bool RectContainsScreenPoint(RectTransform rt, Vector2 screenPoint)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return false;
            rt.GetWorldCorners(_corners);
            return screenPoint.x >= _corners[0].x && screenPoint.x <= _corners[2].x &&
                   screenPoint.y >= _corners[0].y && screenPoint.y <= _corners[2].y;
        }

        /// <summary>이 지점이 조상 마스크 <b>전부</b>의 안쪽인가. 마스크 목록은 빌드 때 한 번만
        /// 모으고(폴링 경로 할당 0), 조상 여부는 <see cref="Transform.IsChildOf"/>로 확인한다.</summary>
        private bool IsUnclipped(RectTransform rt, Vector2 screenPoint)
        {
            if (_masks == null || rt == null) return true;
            for (int i = 0; i < _masks.Length; i++)
            {
                RectMask2D mask = _masks[i];
                if (mask == null || !mask.isActiveAndEnabled) continue;
                RectTransform maskRect = mask.rectTransform;
                if (maskRect == null || maskRect == rt || !rt.IsChildOf(maskRect)) continue;
                if (!RectContainsScreenPoint(maskRect, screenPoint)) return false;
            }
            return true;
        }

        /// <summary>이 부품이 마스크에 잘리고 <b>남은</b> 화면 사각형(전부 잘리면 넓이 0).
        /// 진단/테스트 전용 — "보이는 만큼만 눌린다"를 숫자로 확인하는 창구다.</summary>
        public Rect VisibleScreenRectOf(RectTransform rt)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return new Rect();
            rt.GetWorldCorners(_corners);
            float xMin = _corners[0].x, yMin = _corners[0].y, xMax = _corners[2].x, yMax = _corners[2].y;

            if (_masks != null)
            {
                for (int i = 0; i < _masks.Length; i++)
                {
                    RectMask2D mask = _masks[i];
                    if (mask == null || !mask.isActiveAndEnabled) continue;
                    RectTransform maskRect = mask.rectTransform;
                    if (maskRect == null || maskRect == rt || !rt.IsChildOf(maskRect)) continue;

                    maskRect.GetWorldCorners(_corners);
                    xMin = Mathf.Max(xMin, _corners[0].x);
                    yMin = Mathf.Max(yMin, _corners[0].y);
                    xMax = Mathf.Min(xMax, _corners[2].x);
                    yMax = Mathf.Min(yMax, _corners[2].y);
                }
            }
            if (xMax <= xMin || yMax <= yMin) return new Rect();
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }
    }
}
