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

        /// <summary>이보다 더 줄이면 좌측 컬럼(244)조차 담지 못한다 — 세로 하한과 같은 값으로 맞췄다.</summary>
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
            TickPanelHeight();
            float height = Mathf.Min(_panelHeightPoints, Mathf.Max(MinPanelHeight, Screen.height / scaleFactor - ScreenMargin * 2f));
            float width = Mathf.Min(PanelWidth, Mathf.Max(MinPanelWidth, Screen.width / scaleFactor - ScreenMargin * 2f));
            if (!Mathf.Approximately(_panel.sizeDelta.x, width) || !Mathf.Approximately(_panel.sizeDelta.y, height))
            {
                _panel.sizeDelta = new Vector2(width, height);
                SyncActionReachability();
            }

            Vector2 clamped = ClampPanelPosition(_panel.anchoredPosition, scaleFactor);
            if (clamped != _panel.anchoredPosition) _panel.anchoredPosition = clamped;
        }

        /// <summary>
        /// ★ P0-1 — 탭이 요구하는 높이로 창을 <b>부드럽게</b> 옮긴다. 창은 화면 중앙 고정(피벗 0.5)이라
        /// 위아래로 균등하게 줄어든다.
        /// <para>새 문자열/객체를 만들지 않는다 — 상주 앱의 Update 경로다.</para>
        /// </summary>
        private void TickPanelHeight()
        {
            float target = PanelHeightForTab(_tab);
            if (_panelHeightPoints <= 0f) { _panelHeightPoints = target; return; }   // 첫 프레임은 즉시.
            if (Mathf.Approximately(_panelHeightPoints, target)) { _panelHeightPoints = target; return; }

            // 0.12초에 <b>가장 큰 단(SectionStep)</b>을 지나가는 속도. 단이 작으면 그만큼 빨리 끝난다.
            float speed = SectionStep / PanelHeightAnimateSeconds;
            _panelHeightPoints = Mathf.MoveTowards(_panelHeightPoints, target,
                speed * Mathf.Max(0f, Time.unscaledDeltaTime));
        }

        /// <summary>탭이 요구하는 자리로 상세 패널을 옮긴다. 창 높이는 <see cref="TickPanelHeight"/>가
        /// 뒤따라 줄어들지만 상세 패널은 <b>즉시</b> 올라가야 한다 — 늦으면 그 프레임에 본문 마스크
        /// 밖으로 나가 패널이 잠깐 사라진다.</summary>
        private void ApplyTabDetailPlacement()
        {
            if (_sectionDetailRect == null) return;
            Vector2 p = _sectionDetailRect.anchoredPosition;
            // 카드가 없는 탭에서는 상세 패널이 꺼져 있지만 자리는 잡아 둔다 — 되돌아왔을 때
            // 한 프레임 동안 옛 자리에 떠 있는 것을 막는다.
            float y = DetailYForTab(Def(_tab).Page == TabPage.Cards ? _tab : Tab.Equipment);
            if (Mathf.Approximately(p.y, y)) return;
            p.y = y;
            _sectionDetailRect.anchoredPosition = p;
        }

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
            //   17pt 덮는다(팝오버와 같은 결함, 같은 원인). 아래쪽 한계는 건드리지 않는다 —
            //   Dock을 덮는 것은 macOS의 모든 앱이 하는 표준 동작이고, 이 앱은 그 위를 발판으로도 쓴다.
            float topInset = ReservedTopBarProbe.TopInsetPoints(_agent != null ? _agent.PlatformService : null);
            float y = SurfaceSafeAreaPolicy.ClampCenterOriginOffsetY(
                desired.y, size.y, Screen.height / sf, topInset, ScreenMargin);
            return new Vector2(Mathf.Clamp(desired.x, -maxX, maxX), y);
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
