using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// ★ 2026-09-03 — <b>「화면 전체」와 「작업 영역」 두 사각형에서 네 변 예약 띠를 뽑는 뺄셈</b>.
    /// 순수 산술이고 플랫폼 분기가 없다.
    ///
    /// ============================================================================
    /// 왜 <c>Platform/MacOS/</c>에서 여기로 나왔나 (2026-09-03 debugger 라운드)
    /// ============================================================================
    /// 이 뺄셈은 <see cref="MacOS.MacReservedScreenEdgeService"/> 안에 있었고, 그 클래스는 생성에
    /// <c>MacWindowService</c>(네이티브 P/Invoke 덩어리)를 요구한다. 그래서 <b>이 산술을 값으로
    /// 검증하는 테스트를 쓸 방법이 물리적으로 없었다</b> — 실제로 이 저장소의 예약 띠 테스트는
    /// 전부 <b>소스 텍스트 감사</b>이거나 <b>스텁 주입</b>이었고, <i>"화면 (0,0,1512,982)와
    /// 작업영역 (0,75,1512,874)를 넣으면 상단 33이 나오는가"</i>를 재는 것은 <b>0건</b>이었다.
    /// 그 공백에서 <b>「메뉴 막대를 고쳤다」는 판정이 근거 없이 두 라운드를 살아남았다.</b>
    ///
    /// <para>CLAUDE.md의 규칙 그대로다 — <i>"정책 판정 로직은 플랫폼 중립 위치에 두고, 플랫폼
    /// 전용 코드는 사실 조회만 담당한다"</i>. 지금 <c>MacReservedScreenEdgeService</c>에 남은 것은
    /// 두 번의 조회와 이 함수 호출 한 줄뿐이다.</para>
    ///
    /// ============================================================================
    /// 뺄셈 (전부 OS 포인트)
    /// ============================================================================
    /// <code>
    ///   상 = display.height − (visible.y + visible.height)
    ///   하 = visible.y
    ///   좌 = visible.x
    ///   우 = display.width  − (visible.x + visible.width)
    /// </code>
    ///
    /// ============================================================================
    /// ★★ 전제 — 두 사각형이 <b>같은 화면</b>을 봐야 뺄셈이 뜻을 갖는다
    /// ============================================================================
    /// 이 전제는 지금까지 <b>산문으로만</b> 있었고 아무것도 그것을 재지 않았다. 그래서
    /// <see cref="IsSameDisplayPair"/>가 그것을 <b>실행되는 가드</b>로 바꾼다.
    ///
    /// <para><b>왜 필요한가 — 멀티모니터에서 조용히 거짓 사실이 나온다.</b> macOS 네이티브의 모니터
    /// 정렬 규칙은 <i>"가장 왼쪽 먼저"</i>라 <c>인덱스 0이 주 디스플레이가 아닐 수 있다</c>.
    /// 주 화면 1512×982의 <b>왼쪽</b>에 1280×800 외장을 붙이면 인덱스 0이 그 외장이 되고,
    /// 짝이 어긋난 채로 위 뺄셈이 돈다:</para>
    /// <code>
    ///   display = (0,0 1512x982)          ← 주 화면(CGDisplayBounds)
    ///   visible = (-1280,0 1280x800)      ← 외장의 visibleFrame(Cocoa 전역)
    ///
    ///   상 = 982 − (0+800) = 182 pt   ← 25% 상식 클램프(245.5)를 <b>통과한다</b>
    ///   하 = 0                        ← "Dock 없음을 확인했다"는 거짓 사실(실제 Dock은 75pt)
    /// </code>
    /// 즉 <b>존재하지 않는 182pt 메뉴 막대</b>가 「측정됨」으로 보고되어 모든 UI 표면이 182pt
    /// 아래로 밀린다. 상식 클램프는 이런 어긋남을 <b>못 잡는다</b> — 값이 상식 범위 안이기 때문이다.
    /// 그래서 값이 아니라 <b>짝 자체</b>를 먼저 검사한다.
    ///
    /// <para><b>가드가 통과시키는 것은 하나뿐이다</b>: <c>visible</c>이 원점 기준
    /// <c>display.width × display.height</c> 상자 안에 들어 있는 경우. Cocoa는 주 화면의
    /// <c>visibleFrame ⊆ frame</c>을 보장하고 주 화면의 원점은 Cocoa·Quartz 두 계에서 모두
    /// <c>(0,0)</c>이므로, 이 포함 관계가 곧 <b>"둘이 같은 화면을 보고 있다"</b>는 뜻이 된다.</para>
    ///
    /// ============================================================================
    /// 「모름」으로 접는 자리 — 0으로 위장하지 않는다
    /// ============================================================================
    /// <list type="bullet">
    ///  <item><b>둘 중 하나가 비었다</b>(폭·높이 ≤ 0, NaN 포함) → 전부 모름.
    ///        ★ macOS에서 이것이 <b>기동 직후 2초 남짓 동안 실제로 일어나는 상태</b>다 —
    ///        근거는 <see cref="MacOS.MacReservedScreenEdgeService"/> 클래스 문서.</item>
    ///  <item><b>짝이 어긋났다</b>(위 포함 관계 실패) → 전부 모름. 한 변만 접지 않는다.
    ///        어긋난 짝에서는 <b>우연히 상식 범위에 든 변</b>도 관측이 아니다.</item>
    ///  <item><b>상식 범위 밖</b>(그 변이 놓인 축 길이의 <see cref="SanityMaxInsetFraction"/> 초과)
    ///        → <b>그 변만</b> 모름.</item>
    ///  <item><b>음수</b> → <see cref="ReservedEdgeInsets.Observed"/>가 그 변만 모름으로 접는다.
    ///        ★ 여기 <c>Plausible</c>은 <b>위쪽만 자른다</b>(음수를 통과시킨다) — 접는 일은
    ///        전적으로 <c>Observed</c>가 한다. 포함 가드가 생긴 뒤로 음수는 도달 불가능하지만,
    ///        <b>두 겹 중 하나가 사라져도 조용해지지 않도록</b> 양쪽 다 남긴다.</item>
    /// </list>
    /// </summary>
    public static class ReservedEdgeGeometry
    {
        /// <summary>이보다 두꺼우면 예약 띠가 아니라 조회가 어긋난 것으로 본다(그 변이 놓인 축 길이 대비 비율).</summary>
        public const float SanityMaxInsetFraction = 0.25f;

        /// <summary>포함 판정의 여유(OS 포인트). 서브픽셀·반올림용이며, 어긋난 짝은 통상 수백 pt 단위로
        /// 벗어나므로 이 여유가 그것을 통과시키지 않는다.</summary>
        public const float SameDisplaySlackPoints = 1f;

        /// <summary>
        /// 화면 전체 사각형과 작업 영역 사각형에서 네 변 예약 띠를 뽑는다(둘 다 OS 포인트).
        /// </summary>
        /// <param name="displayPoints">화면 전체. macOS <c>CGDisplayBounds(주 디스플레이)</c>.</param>
        /// <param name="visiblePoints">작업 영역. macOS <c>NSScreen.visibleFrame</c>(Cocoa 좌하단 원점).</param>
        /// <param name="insets">성공 시 네 변 두께. 실패하면 <see cref="ReservedEdgeInsets.Unknown"/>.</param>
        /// <returns>한 변도 재지 못했으면 false.</returns>
        public static bool TryFromDisplayAndVisibleFrame(Rect displayPoints, Rect visiblePoints,
            out ReservedEdgeInsets insets)
        {
            insets = ReservedEdgeInsets.Unknown;
            if (!IsUsableRect(displayPoints)) return false;
            if (!IsUsableRect(visiblePoints)) return false;
            if (!IsSameDisplayPair(displayPoints, visiblePoints)) return false;

            insets = ReservedEdgeInsets.Observed(
                Plausible(displayPoints.height - (visiblePoints.y + visiblePoints.height), displayPoints.height),
                Plausible(visiblePoints.y, displayPoints.height),
                Plausible(visiblePoints.x, displayPoints.width),
                Plausible(displayPoints.width - (visiblePoints.x + visiblePoints.width), displayPoints.width));

            return insets.MeasuredEdges != ReservedEdge.None;
        }

        /// <summary>
        /// 두 사각형이 <b>같은 화면</b>을 보고 있는가 — <c>visible</c>이 원점 기준
        /// <c>display.width × display.height</c> 상자 안에 있는가. 자세한 근거는 클래스 문서.
        ///
        /// <para>★ <b>보조 모니터로 확장할 때 이 가드가 「고장 난 것처럼」 보일 것이다 — 아니다.</b>
        /// 이 검사는 두 사각형이 <b>원점이 (0,0)인 같은 좌표계</b>로 표현돼 있다고 전제한다.
        /// 주 디스플레이는 Cocoa·Quartz 두 계에서 모두 원점이 (0,0)이라 그 전제가 공짜로 성립한다.
        /// <c>GetMonitorRect(1)</c> + 그 모니터의 <c>CGDisplayBounds</c>를 <b>전역 좌표 그대로</b>
        /// 넣으면 여기서 거부되는 것이 <b>정상</b>이다(두 전역 좌표계는 y 방향이 반대라 그대로 빼면
        /// 틀린 값이 나온다). 확장하려면 <b>넣기 전에 각 사각형을 그 디스플레이 로컬 좌표로
        /// 정규화</b>해라 — 가드를 푸는 것이 아니라 입력을 맞추는 것이 옳은 순서다.</para>
        /// </summary>
        public static bool IsSameDisplayPair(Rect displayPoints, Rect visiblePoints)
        {
            if (!IsUsableRect(displayPoints) || !IsUsableRect(visiblePoints)) return false;

            float slack = SameDisplaySlackPoints;
            if (visiblePoints.x < -slack) return false;
            if (visiblePoints.y < -slack) return false;
            if (visiblePoints.x + visiblePoints.width > displayPoints.width + slack) return false;
            if (visiblePoints.y + visiblePoints.height > displayPoints.height + slack) return false;
            return true;
        }

        /// <summary>폭·높이가 양수이고 네 성분이 전부 유한한가. NaN 폭은 <c>&gt; 0f</c>가 이미 거르지만,
        /// NaN <c>x</c>/<c>y</c>는 <b>모든 부등호를 조용히 false로 만들어</b> 포함 검사를 통과해 버리므로
        /// 여기서 따로 막는다.</summary>
        private static bool IsUsableRect(Rect r)
            => r.width > 0f && r.height > 0f && IsFinite(r.x) && IsFinite(r.y)
               && IsFinite(r.width) && IsFinite(r.height);

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        /// <summary>상식 범위를 벗어나면 <see cref="float.NaN"/>을 돌려준다 —
        /// <see cref="ReservedEdgeInsets.Observed"/>가 그것을 <b>미측정</b>으로 접는다(0으로 위장하지 않는다).
        /// <b>위쪽만 자른다</b>; 음수 처리는 <c>Observed</c>의 몫이다(클래스 문서 참고).</summary>
        private static float Plausible(float raw, float axisExtent)
            => raw > axisExtent * SanityMaxInsetFraction ? float.NaN : raw;
    }
}
