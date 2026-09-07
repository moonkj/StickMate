using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★★ <b>창을 손으로 옮기는 기구 — 한 벌</b>. 2026-09-07 사용자 요청(PART1-1):
    /// <i>"모든 창(집중모드 타이머, 캐릭터 정보창, 설정창)이 마우스로 끌어도 움직이지 않음 —
    /// 전부 드래그 이동 가능해야 함"</i> · <i>"이동한 위치는 창별로 저장되어 재시작 후에도 유지"</i>.
    ///
    /// ============================================================================
    /// 왜 창마다 따로 짜지 않는가
    /// ============================================================================
    /// 이 라운드 전까지 <b>정보창에만</b> 헤더 드래그가 있었고(설정창·팝오버에는 코드가 아예 없었다),
    /// 그 한 벌조차 「옮긴 자리를 기억하지 않는다」였다. 여기서 각 창이 자기 판으로 다시 짜면
    /// <b>임계값 3개 · 클램프 3개 · 저장 경로 3개</b>가 생기고, 그중 하나가 낡는 날 «어느 창은 되고
    /// 어느 창은 안 되는» 상태가 조용히 만들어진다. 이 저장소가 반복해 당한 형태다.
    /// 그래서 <b>판정(임계) · 좌표 변환 · 클램프 · 저장</b> 네 가지를 여기 한 곳에 둔다.
    /// 창이 각자 아는 것은 «어디가 손잡이인가»뿐이다(그건 창마다 실제로 다르다).
    ///
    /// ============================================================================
    /// ★ 좌표계 — <b>화면 중앙 원점, OS 포인트</b>
    /// ============================================================================
    /// 이 클래스가 다루는 모든 좌표는 «창 중심이 화면 중앙에서 얼마나 떨어졌는가»다.
    ///  · 정보창/설정창의 <c>RectTransform.anchoredPosition</c>(anchor·pivot 0.5)과 <b>비트가 같다</b>.
    ///  · 팝오버는 anchor가 좌하단이라 <c>PopoverPanel.UpdatePlacement</c>가 픽셀로 환산해 쓴다.
    ///  · 저장 스키마(<see cref="UiLayoutModel.SetWindowOffset"/>)도 같은 계다 — 창은 전부
    ///    화면 중앙에서 열리는 표면이라 해상도가 바뀌어도 «중앙에서 얼마나»의 뜻이 보존된다.
    ///
    /// ============================================================================
    /// ★ 「끌었다」의 판정 — 톱니의 <b>거리 조건만</b> 가져온다 (근거를 적는다)
    /// ============================================================================
    /// 톱니(<see cref="InfoGearIconWidget.ShouldBeginDrag"/>)는 <b>시간 AND 거리</b>다:
    /// <c>0.4초 이상 누른 «그리고» 6pt 이상 이동</c>. 창은 그중 <b>거리 조건만</b> 쓴다.
    ///
    /// <para><b>시간 조건을 안 가져오는 이유</b>는 취향이 아니라 두 표면의 구조 차이다.
    /// 톱니는 <b>같은 픽셀</b>이 두 가지 뜻을 갖는다 — 짧게 누르면 부채꼴이 열리고, 길게 끌면 옮긴다.
    /// 시간 조건은 그 <b>둘을 가르기 위해</b> 존재한다. 창의 손잡이는 정반대다: 헤더의 빈 자리와
    /// 팝오버의 컨트롤 아닌 바탕은 <b>누를 때 일어나는 일이 아무것도 없다</b>(버튼은 아래
    /// <see cref="AnyContains"/>로 손잡이에서 빠진다). 가를 것이 없는 자리에 0.4초를 요구하면
    /// 「끌었는데 0.4초 동안 안 움직인다」가 되고, 그건 이 라운드가 고치려는 신고 문장 그 자체다.</para>
    ///
    /// <para><b>거리 조건은 반드시 가져온다</b>: 창 위치가 이제 <b>세이브에 내려가기</b> 때문이다.
    /// 톱니가 2026-09-02에 당한 사고(<c>0.02초 / 16.5pt</c>가 「길게 누름」으로 판정되어 스치듯
    /// 지나간 클릭 하나가 아이콘을 영구히 옮긴 것)의 절반은 <b>영속화</b>가 만든 피해였다.
    /// 창에도 같은 위험이 방금 생겼고, 그것을 닫는 것이 거리 임계다. 값은 베끼지 않고
    /// <see cref="InfoGearIconWidget.DragMoveThreshold"/>를 <b>참조</b>한다 — 두 표면의 손 감각이
    /// 갈라지지 않고, 그 상수가 한 번 바뀔 때 기준과 대상이 함께 움직인다(CLAUDE.md).</para>
    ///
    /// ============================================================================
    /// ★ 클램프 — <b>창 전체</b>가 화면 안에 남는다 (「일부라도」가 아니다)
    /// ============================================================================
    /// 요구는 "화면 밖으로 완전히 나가지 않도록"이었지만 여기서 강제하는 것은 그보다 <b>세다</b>:
    /// 창 사각형 전체가 안전 영역 안이다. 이유는 <b>탈출구</b>다 — 2026-09-02 사용자 지시로
    /// <b>창 바깥 클릭이 창을 닫지 않게</b> 되면서 이 앱에서 창을 닫는 마우스 경로가 <c>[✕]</c>
    /// 하나뿐이 됐다(UiChrome "창을 닫는 법"). «일부라도 남으면 된다»로 풀면 [✕]가 화면 밖으로
    /// 나간 창이 만들어지고, 그 순간 사용자는 그 창을 <b>영영 닫을 수 없다</b>.
    ///
    /// <para>세로는 대칭이 아니다 — 위쪽은 OS 예약 띠(macOS 메뉴바 / 상단 도킹 작업표시줄)만큼,
    /// 아래쪽은 Windows에서만 작업표시줄 두께만큼 좁아진다. 그 갈림은 이 파일이 아니라
    /// <see cref="SurfaceSafeAreaPolicy"/> 한 곳에 있고 여기엔 <c>#if</c>가 없다.</para>
    /// </summary>
    public sealed class UiWindowDrag
    {
        /// <summary>「끌었다」로 판정하는 최소 이동 거리(OS 포인트). <b>숫자를 적지 않는다</b> —
        /// 톱니와 같은 손 감각을 쓴다(클래스 문서 "「끌었다」의 판정" 절).</summary>
        public static float MoveThresholdPoints => InfoGearIconWidget.DragMoveThreshold;

        /// <summary>
        /// ★ 창이 화면 가장자리에서 남기는 여백(OS 포인트) — <b>세 창의 단일 출처</b>.
        ///
        /// <para>이 값은 원래 <c>CharacterInfoWindow.ScreenMargin</c> 한 곳에만 있었고, 설정창에는
        /// 클램프 자체가 없었다. 창 셋이 같은 규칙을 쓰게 되면서 <b>같은 16을 세 번 적는</b> 형태가
        /// 될 뻔했다 — 그러면 다음 라운드에 반드시 한 벌만 고쳐진다(이 저장소의 상수 하드코딩 사고).
        /// 그래서 여기 한 번만 적고 두 창이 이 값을 <b>참조</b>한다.</para>
        ///
        /// <para><b>public인 이유</b>: 클램프를 검증하는 테스트가 숫자를 베끼지 않고 이 상수를
        /// 참조해야 한다(CLAUDE.md — 프로덕션 상수 하드코딩 금지).</para>
        /// </summary>
        public const float ScreenMarginPoints = 16f;

        private readonly UiWindowId _id;

        private bool _grabbed;
        private bool _moved;
        private Vector2 _grabCursorPoints;
        private Vector2 _grabCenterPoints;
        private Vector2 _liveCenterPoints;

        public UiWindowDrag(UiWindowId id) => _id = id;

        /// <summary>이 기구가 위치를 저장하는 창.</summary>
        public UiWindowId Id => _id;

        /// <summary>지금 손잡이를 잡고 있는가(아직 문턱을 넘지 않았어도 true).
        /// <para>「조작 중」의 정의가 이것이다 — 프레임 페이싱 홀드와 폴링 간격 해제가 이 값을 본다.
        /// 잡고만 있고 안 움직이는 순간에도 사용자는 이 창을 만지고 있다.</para></summary>
        public bool IsGrabbed => _grabbed;

        /// <summary>이번 잡기에서 문턱(<see cref="MoveThresholdPoints"/>)을 넘어 실제로 움직였는가.
        /// <b>세이브에 내려가는 조건</b>이다.</summary>
        public bool HasMoved => _moved;

        /// <summary>저장된(= 사용자가 옮겨 둔) 창 중심. 없으면 false이고 창은 기본 자리를 쓴다.</summary>
        public bool TryGetSavedCenter(out Vector2 centerPoints)
        {
            if (!UiLayoutModel.HasWindowOffset(_id)) { centerPoints = Vector2.zero; return false; }
            centerPoints = UiLayoutModel.WindowOffsetPoints(_id);
            return true;
        }

        /// <summary>손잡이를 잡았다. <paramref name="panelCenterPoints"/>는 <b>지금</b> 창 중심이고,
        /// 잡은 지점과의 차이를 기억해 두어야 드래그 첫 프레임에 창이 커서로 순간이동하지 않는다.</summary>
        public void Grab(Vector2 cursorPoints, Vector2 panelCenterPoints)
        {
            _grabbed = true;
            _moved = false;
            _grabCursorPoints = cursorPoints;
            _grabCenterPoints = panelCenterPoints;
            _liveCenterPoints = panelCenterPoints;
        }

        /// <summary>
        /// 커서가 움직였다 — 이번 프레임에 창을 놓아야 할 자리를 돌려준다.
        /// <b>문턱을 아직 안 넘었으면 false</b>(창은 한 픽셀도 움직이지 않는다).
        ///
        /// <para>식은 <c>잡은 순간의 중심 + (지금 커서 − 잡은 커서)</c>라는 <b>절대값</b>이다.
        /// 프레임마다 델타를 누적하지 않으므로, 호출부가 결과를 클램프해서 다르게 적용해도
        /// 오차가 쌓이지 않는다(정보창 캐러셀이 같은 이유로 같은 형태를 쓴다).</para>
        /// </summary>
        public bool TryResolveCenter(Vector2 cursorPoints, out Vector2 desiredCenterPoints)
        {
            desiredCenterPoints = _liveCenterPoints;
            if (!_grabbed) return false;

            Vector2 delta = cursorPoints - _grabCursorPoints;
            if (!_moved)
            {
                if (delta.sqrMagnitude < MoveThresholdPoints * MoveThresholdPoints) return false;
                _moved = true;
            }

            desiredCenterPoints = _grabCenterPoints + delta;
            return true;
        }

        /// <summary>호출부가 <b>실제로 적용한</b>(= 클램프를 지난) 중심을 되돌려 준다.
        /// 세이브에 내려가는 것은 이 값이지 <see cref="TryResolveCenter"/>가 준 희망값이 아니다 —
        /// 그렇지 않으면 화면 밖으로 끌어낸 좌표가 파일에 앉는다.</summary>
        public void NoteAppliedCenter(Vector2 clampedCenterPoints) => _liveCenterPoints = clampedCenterPoints;

        /// <summary>
        /// 손을 뗐다. <b>실제로 움직였을 때만</b> 모델에 확정하고 즉시 디스크에 쓴다.
        ///
        /// <para><b>왜 즉시 저장인가</b>: 주기 저장(기본 60초)만 믿으면 창을 옮긴 직후 앱을 끈
        /// 사용자에게는 그 이동이 없던 일이 된다. 톱니의 <c>CommitDragPosition</c>이 같은 이유로
        /// 같은 선택을 했다. 반대로 <b>안 움직였으면 아무것도 하지 않는다</b> — 헤더를 눌렀다 뗀
        /// 것만으로 디스크를 두드리면 하루 종일 켜져 있는 앱에서 그게 곧 상시 쓰기다.</para>
        /// </summary>
        /// <returns>이번 잡기가 실제 이동으로 확정됐는가(로그를 남길지 판단하는 데 쓴다).</returns>
        public bool Release()
        {
            bool moved = _grabbed && _moved;
            _grabbed = false;
            _moved = false;
            if (!moved) return false;

            UiLayoutModel.SetWindowOffset(_id, _liveCenterPoints);
            CharacterSaveStore.Save();
            return true;
        }

        /// <summary>창이 닫히거나 전체화면 감지로 거둬질 때 — <b>확정하지 않고</b> 손을 놓는다.
        /// (닫히는 창의 마지막 좌표를 저장하면 «내가 옮긴 적 없는데 자리가 바뀌었다»가 된다.)</summary>
        public void Cancel()
        {
            _grabbed = false;
            _moved = false;
        }

        // ==================== 좌표 / 클램프 / 히트테스트 ====================

        /// <summary>Unity 스크린 픽셀(좌하단 원점) → <b>화면 중앙 원점 캔버스 포인트</b>.
        /// 정보창이 쓰던 <c>ScreenToPanelPoints</c>와 같은 식이고, 이제 세 창이 이 한 벌을 쓴다.</summary>
        public static Vector2 ScreenToCenterOriginPoints(Vector2 cursorUnityScreen, float scaleFactor)
        {
            float sf = scaleFactor > 0f ? scaleFactor : 1f;
            return new Vector2((cursorUnityScreen.x - Screen.width * 0.5f) / sf,
                               (cursorUnityScreen.y - Screen.height * 0.5f) / sf);
        }

        /// <summary>
        /// ★ <b>창이 화면(과 OS 예약 띠) 밖으로 나가지 않는 자리</b>로 자른다 — 드래그와 화면 크기
        /// 변화가 <b>같은 규칙</b>을 쓴다. 근거는 클래스 문서 "클램프" 절.
        ///
        /// <para>가로는 <c>Mathf.Max(0f, …)</c> 형태를 그대로 유지한다 — 창이 안전 영역보다 넓어지는
        /// 화면(배치모드 640×480 등)에서 <b>가운데에 두는</b> 것이 지금까지의 동작이고, 이 라운드는
        /// 그 동작을 바꾸지 않는다(정보창 회귀 테스트가 그 자리를 잰다).</para>
        /// </summary>
        /// <param name="desiredCenterPoints">희망 위치(화면 중앙 원점, OS 포인트).</param>
        /// <param name="sizePoints">창 크기(OS 포인트).</param>
        /// <param name="screenSizePoints">화면 크기(OS 포인트 = Unity 픽셀 / 캔버스 배율).</param>
        /// <param name="topInsetPoints">위쪽 OS 예약 띠 두께(못 쟀으면 0).</param>
        /// <param name="bottomInsetPoints">아래쪽 예약 띠(플랫폼 판정을 지난 값 — Windows에서만 0이 아니다).</param>
        /// <param name="marginPoints">화면 가장자리 여백.</param>
        public static Vector2 ClampCenterPoints(Vector2 desiredCenterPoints, Vector2 sizePoints,
            Vector2 screenSizePoints, float topInsetPoints, float bottomInsetPoints, float marginPoints)
        {
            if (screenSizePoints.x <= 0f || screenSizePoints.y <= 0f) return desiredCenterPoints;

            float maxX = Mathf.Max(0f, (screenSizePoints.x - sizePoints.x) * 0.5f - marginPoints);
            float y = SurfaceSafeAreaPolicy.ClampCenterOriginOffsetY(
                desiredCenterPoints.y, sizePoints.y, screenSizePoints.y,
                topInsetPoints, bottomInsetPoints, marginPoints);
            return new Vector2(Mathf.Clamp(desiredCenterPoints.x, -maxX, maxX), y);
        }

        /// <summary>위 클램프가 OS에게 물어보는 두 인셋을 한 번에 가져온다 — 세 창이 각자
        /// <c>ReservedTopBarProbe</c>/<c>ReservedEdgeProbe</c>를 다시 부르지 않게 한다.
        /// <b>못 쟀으면 0</b>이고, 0이면 좌표가 예전과 비트 동일하다(띠가 없는 사용자의 화면은
        /// 한 픽셀도 안 움직인다 — 톱니가 같은 라운드에 세운 규약).</summary>
        public static void ResolveReservedInsets(StickmanAgent agent,
            out float topInsetPoints, out float bottomInsetPoints)
        {
            IPlatformWindowService service = agent != null ? agent.PlatformService : null;
            topInsetPoints = ReservedTopBarProbe.TopInsetPoints(service);
            bottomInsetPoints = ReservedEdgeProbe.EnforcedBottomInsetPoints(service);
        }

        /// <summary>ScreenSpaceOverlay 캔버스에서는 RectTransform의 월드 좌표가 곧 스크린 픽셀이다
        /// (세 창이 이미 각자 같은 함수를 갖고 있고, 손잡이 판정만 이 한 벌을 쓴다).</summary>
        public static bool RectContainsScreenPoint(RectTransform rt, Vector2 screenPoint)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return false;
            rt.GetWorldCorners(CornerBuffer);
            return screenPoint.x >= CornerBuffer[0].x && screenPoint.x <= CornerBuffer[2].x &&
                   screenPoint.y >= CornerBuffer[0].y && screenPoint.y <= CornerBuffer[2].y;
        }

        /// <summary>
        /// ★ <b>손잡이에서 빼야 할 사각형들</b> 중 하나라도 커서를 물고 있는가.
        ///
        /// <para>목록을 <b>손으로 적지 않고 배열을 도는</b> 형태로 두는 것이 요점이다 — 헤더에
        /// 컨트롤을 하나 더 넣는 사람이 여기를 잊으면 그 컨트롤을 누를 때마다 창이 끌려간다.
        /// 팝오버는 아예 <c>GetComponentsInChildren&lt;Button&gt;()</c>이 만든 배열을 넘기므로
        /// «잊을 자리»가 구조적으로 없다(<see cref="PopoverPanel"/>).</para>
        /// </summary>
        public static bool AnyContains(RectTransform[] rects, Vector2 screenPoint)
        {
            if (rects == null) return false;
            for (int i = 0; i < rects.Length; i++)
            {
                if (RectContainsScreenPoint(rects[i], screenPoint)) return true;
            }
            return false;
        }

        /// <summary><see cref="RectTransform.GetWorldCorners"/>가 채워 주는 4칸 버퍼 — 호출마다
        /// <c>new Vector3[4]</c>를 만들지 않는다(하루 종일 켜져 있는 앱. PopoverPanel.CornerBuffer와
        /// 같은 관례이고 안전 근거도 같다: 값을 즉시 읽고 버리며 메인 스레드 전용이다).</summary>
        private static readonly Vector3[] CornerBuffer = new Vector3[4];
    }
}
