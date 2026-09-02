using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>어느 근거로 그 모니터가 뽑혔는가. <b>로그와 진단이 이 값을 그대로 찍는다</b> —
    /// "조용히 엉뚱한 데 뜬다"가 이 기능의 유일한 실패 모드라, 근거를 값으로 남긴다.</summary>
    public enum OverlayMonitorChoiceSource
    {
        /// <summary>모니터 목록이 비어 있다. 호출자는 <b>현재 동작을 유지</b>해야 한다.</summary>
        NoMonitors = 0,

        /// <summary><b>기본값 — 시작 자리</b>(가로 배치면 가장 왼쪽, 세로 배치면 가장 위).
        /// 사용자 확정 2026-09-02: "그럼 왼쪽 오른쪽 선택할수 있게 기본은 왼쪽".
        /// 주 모니터 플래그가 아니라 <b>축 방향 최솟값</b>으로 정한다.</summary>
        StartSlotDefault = 1,

        /// <summary>사용자가 고른 화면을 목록에서 찾았다.</summary>
        UserPreferred = 2,

        /// <summary>사용자가 골랐는데 <b>지금 배치에서 그 자리를 정할 수 없다</b>
        /// (미러링/겹침 배치라 축이 서지 않는다) → 기본 자리로 폴백.
        /// <b>반드시 로그에 남긴다</b> — 조용히 폴백하면 사용자는 "설정이 안 먹는다"고 신고한다.</summary>
        UserPreferredMissing = 3,

        /// <summary>사용자가 고른 화면을 못 찾았고 <b>목록도 비어</b> 기본값조차 정할 수 없다.
        /// 0번으로 위장하지 않는다 — 호출자는 <b>현재 동작을 유지</b>하고 로그를 남긴다.</summary>
        Indeterminate = 4,
    }

    /// <summary>
    /// 모니터 배치의 <b>축</b>. UI가 <c>왼쪽/오른쪽</c>과 <c>위쪽/아래쪽</c> 중 무엇을 쓸지,
    /// 그리고 <b>고를 수 있는 배치인지</b>를 이 값 하나가 정한다.
    ///
    /// <para>★ <c>bool</c>이 아니라 3값인 이유(<c>ux-designer</c> §49 지적, 채택):
    /// <b>미러링</b>은 "가로가 아니다"이지 "세로다"가 아니다. 두 화면이 같은 픽셀을 가리키는데
    /// 칩 두 개가 서로 다른 척하면 <b>절대 불변 원칙 1(행동-텍스트 싱크)</b> 위반이다.</para>
    /// </summary>
    public enum MonitorArrangementAxis
    {
        /// <summary>축을 세울 수 없다 — 미러링, 완전 겹침, 화면 1대. <b>고를 수 없다.</b></summary>
        Indistinct = 0,

        /// <summary>좌우로 늘어서 있다 — <c>왼쪽 / 오른쪽</c>.</summary>
        Horizontal = 1,

        /// <summary>위아래로 쌓여 있다 — <c>위쪽 / 아래쪽</c>.</summary>
        Vertical = 2,
    }

    /// <summary>
    /// 사용자가 고를 수 있는 <b>자리</b>. 칩은 영원히 둘이다(<c>ux-designer</c> §49 확정) —
    /// 축이 가로면 <c>왼쪽/오른쪽</c>, 세로면 <c>위쪽/아래쪽</c>으로 <b>글자만</b> 바뀐다.
    ///
    /// <para>★ 이 열거형이 <b>저장 단위</b>이기도 하다(아래 <see cref="OverlayMonitorChoicePolicy"/>의
    /// "왜 좌표를 버렸는가" 절). 파일에는 숫자가 아니라 <b>이름 문자열</b>로 적힌다 —
    /// 잉크색/대사 표시 시간이 쓰는 그 관례이며, 칸이 끼어들어도 파일이 밀리지 않는다.</para>
    /// </summary>
    public enum OverlayMonitorSlot
    {
        /// <summary>축의 시작 — 가로면 <b>가장 왼쪽</b>, 세로면 <b>가장 위</b>. <b>기본값.</b></summary>
        Start = 0,

        /// <summary>축의 끝 — 가로면 <b>가장 오른쪽</b>, 세로면 <b>가장 아래</b>.</summary>
        End = 1,
    }

    /// <summary>선택 결과. <see cref="HasIndex"/>가 false면 호출자는 아무것도 바꾸지 않는다.</summary>
    public readonly struct OverlayMonitorChoice
    {
        public readonly int Index;
        public readonly OverlayMonitorChoiceSource Source;

        public OverlayMonitorChoice(int index, OverlayMonitorChoiceSource source)
        {
            Index = index;
            Source = source;
        }

        /// <summary>쓸 수 있는 인덱스가 나왔는가.</summary>
        public bool HasIndex => Index >= 0;
    }

    /// <summary>
    /// ============================================================================
    /// "오버레이를 어느 모니터에 띄울 것인가" — <b>플랫폼 중립 판정</b> (2026-09-02 사용자 확정)
    /// ============================================================================
    /// 사용자 원문: <i>"멀티모니터일때 무조건 주모니터에서 실행하도록"</i> →
    /// <i>"이게 기본이고 사용자가 설정할수있게 기능 넣어줘 다만 멀티모니터 인식이 됐을때만 활성화"</i>
    ///
    /// <para>★ <b>2026-09-02 재확정</b>: 사용자가 기본값을 직접 바꿨다 —
    /// <i>"그럼 왼쪽 오른쪽 선택할수 있게 기본은 왼쪽"</i>.
    /// 즉 규칙은 두 줄이다: <b>기본은 가장 왼쪽 모니터</b>, <b>사용자가 고르면 그 화면</b>.
    /// 그리고 <b>고르는 UI는 모니터 2대 이상일 때만</b> 산다(<see cref="IsMultiMonitor"/>).</para>
    ///
    /// <para><b>기본값이 "가장 왼쪽"이 되면서 판정이 오히려 단순해졌다</b>: 그것은 두 네이티브
    /// 라이브러리의 <b>정렬 규칙 그 자체</b>라, 라이브러리 목록만 있으면 인덱스 0이 곧 답이다.
    /// 그래도 주 모니터 플래그 조회는 <b>버리지 않는다</b> — (1) 사용자 실기에서
    /// "가장 왼쪽 == 주 모니터인가"를 계측으로 남겨야 하고, (2) macOS의 "화면 전체(메뉴바/Dock 포함)
    /// 덮기" 경로는 <b>진짜 주 디스플레이</b>일 때만 성립하기 때문이다.</para>
    ///
    /// ============================================================================
    /// ★ 이 파일이 존재하는 이유 — 인덱스 0은 주 모니터가 아니다
    /// ============================================================================
    /// Phase 0 계측 라운드가 <b>양쪽 네이티브 원문에서</b> 확정한 사실:
    /// <list type="bullet">
    ///   <item>Windows <c>libuniwinc.cpp updateMonitorRectangles()</c> — <c>left</c> 오름차순 버블 정렬</item>
    ///   <item>macOS <c>LibUniWinC.swift _updateScreenInfo()</c> — <c>minX</c> 오름차순 정렬</item>
    /// </list>
    /// 즉 라이브러리 <b>0번은 "가장 왼쪽" 모니터</b>다. 주 모니터는 원점(0,0)에 있으므로,
    /// <b>주 화면 왼쪽에 보조 모니터를 둔 사용자에게 0번은 주 모니터가 아니다.</b>
    /// 그래서 이 정책은 인덱스를 절대 믿지 않고 <b>OS 플래그</b>(<see cref="OsMonitorFact.IsPrimary"/>)만 본다.
    ///
    /// ============================================================================
    /// ★ 무엇을 저장하는가 — <b>좌표를 버리고 "자리"를 저장한다</b> (2026-09-02 재판단)
    /// ============================================================================
    /// 처음에는 원점 <c>(x,y)</c>를 키로 썼다. <c>ux-designer</c> §49가 그 설계의 구멍을 지적했고,
    /// <b>그 지적을 채택한다</b>:
    ///
    /// <blockquote>주 화면을 바꾸면 <b>모든 원점이 재계산</b>된다(macOS는 주 화면이 언제나 (0,0)이라
    /// 나머지가 음수로 밀린다). 저장한 <c>"0,0"</c>이 <c>"-1920,0"</c>이 되어 <b>선택이 증발</b>한다.</blockquote>
    ///
    /// <para>내 원래 근거는 <i>"원점은 사용자가 OS 설정에서 직접 끌어다 정한 값"</i>이었다.
    /// 그것은 <b>한 배치 안에서는 맞지만</b>, 주 화면 변경은 좌표계 자체를 옮기므로 전제가 깨진다.</para>
    ///
    /// <para><b>채택 근거 — 저장값이 UI보다 더 구체적이면 안 된다.</b> 칩은 영원히 둘이다
    /// (<see cref="OverlayMonitorSlot"/>). 좌표를 저장하면 사용자가 <b>고른 적 없는 "그 패널의 정체성"</b>을
    /// 우리가 대신 기록하는 셈이고, 그 정체성이 흔들릴 때마다 선택이 증발한다.
    /// <c>Start</c>/<c>End</c>는 <b>사용자가 실제로 표현할 수 있는 것과 정확히 같은 해상도</b>이며,
    /// 주 화면 변경·해상도 변경·재원점화에 <b>전부 불변</b>이다.</para>
    ///
    /// <para>추가 이득: <b>축에 무관하다.</b> 같은 두 값이 가로에서는 왼쪽/오른쪽, 세로에서는
    /// 위쪽/아래쪽으로 읽힌다 — ux-designer의 "글자만 교체" 설계가 이 저장 형태를 그대로 요구한다.
    /// 좌표 키였다면 축이 바뀔 때 해석을 다시 해야 했다.</para>
    ///
    /// <para><b>잃는 것(정직하게)</b>: 3대 이상에서 <b>가운데 화면을 기억할 수 없다</b>.
    /// 다만 UI가 애초에 양 끝 2칩만 제공하므로(ux-designer 확정) <b>고를 수 없는 것을 기억할 이유가
    /// 없다</b>. 이 한계는 UI 고지 문구가 사용자에게 직접 말한다.</para>
    ///
    /// <para><b>이행</b>: 저장 스키마를 또 올리지 않는다. 필드는 v10의
    /// <c>preferredMonitorKey</c>(string) 그대로이고, 중간 빌드가 남긴 옛 좌표 문자열은
    /// <see cref="TryParseSlot"/>에서 파싱에 실패해 <b>조용히 기본값</b>이 된다 —
    /// "모르는 이름은 고른 적 없음"이라는 잉크색/대사 표시 시간과 같은 관례다.</para>
    ///
    /// <para><b>폴백이 깨끗해진 것이 이 설계의 전제다</b>: 못 찾으면 <b>주 모니터</b>다. 예전 설계
    /// (커서 위치/저장값 우선순위)에서는 폴백이 "지금 창이 있는 자리"라 실패가 조용했지만, 지금은
    /// 실패해도 <b>사용자가 확정한 기본값</b>으로 간다. 그래서 식별자에 비싼 값을 치를 이유가 없다.</para>
    /// </summary>
    public static class OverlayMonitorChoicePolicy
    {
        /// <summary>원점 좌표 비교 허용 오차(픽셀). 정수 좌표라 1이면 충분하고,
        /// 넓히면 인접 모니터를 같은 것으로 볼 위험이 생긴다.</summary>
        public const float OriginMatchTolerance = 1f;

        /// <summary>키의 원점 부분과 진단 부분을 가르는 문자.</summary>
        private const char DiagnosticSeparator = '@';

        /// <summary>
        /// 설정 UI가 살아 있어야 하는가 — <b>모니터를 2대 이상 인식했을 때만</b>(사용자 확정 조건).
        /// 1대면 고를 것이 없고, 행이 살아 있으면 "골랐는데 아무 일도 안 일어난다"가 된다.
        /// </summary>
        public static bool IsMultiMonitor(int monitorCount) => monitorCount >= 2;

        /// <summary>
        /// ★ <b>2026-09-02 — 더는 저장에 쓰지 않는다</b>(<see cref="OverlayMonitorSlot"/>이 저장 단위다).
        /// 계측/로그에서 "그 화면이 어디였는지"를 사람이 읽기 위한 <b>진단 문자열</b>로만 남긴다.
        /// 형식 <c>"x,y@wxh"</c> —
        /// <b>매칭은 <c>x,y</c>만 본다</b>. 뒤의 크기는 사람이 로그를 읽을 때를 위한 것이고,
        /// 해상도가 바뀌어도 선택이 살아 있어야 하므로 <b>판정에 넣지 않는다</b>.
        /// </summary>
        public static string MakeKey(Rect fullOsRect)
        {
            return string.Concat(
                Mathf.RoundToInt(fullOsRect.x).ToString(CultureInfo.InvariantCulture), ",",
                Mathf.RoundToInt(fullOsRect.y).ToString(CultureInfo.InvariantCulture),
                DiagnosticSeparator.ToString(),
                Mathf.RoundToInt(fullOsRect.width).ToString(CultureInfo.InvariantCulture), "x",
                Mathf.RoundToInt(fullOsRect.height).ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>키에서 원점만 뽑는다. 형식이 깨졌으면 false — <b>0,0으로 위장하지 않는다</b>
        /// (0,0은 주 모니터의 실제 좌표라, 파싱 실패를 그 값으로 만들면 "주 모니터를 골랐다"는
        /// 거짓 성공이 된다).</summary>
        public static bool TryParseOrigin(string key, out Vector2 origin)
        {
            origin = default;
            if (string.IsNullOrEmpty(key)) return false;

            int at = key.IndexOf(DiagnosticSeparator);
            string head = at >= 0 ? key.Substring(0, at) : key;

            int comma = head.IndexOf(',');
            if (comma <= 0 || comma >= head.Length - 1) return false;

            if (!float.TryParse(head.Substring(0, comma), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float x)) return false;
            if (!float.TryParse(head.Substring(comma + 1), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float y)) return false;

            origin = new Vector2(x, y);
            return true;
        }

        /// <summary>저장된 키에 해당하는 모니터 인덱스(없으면 -1).</summary>
        public static int IndexOfKey(IReadOnlyList<OsMonitorFact> monitors, string key)
        {
            if (monitors == null || monitors.Count == 0) return -1;
            if (!TryParseOrigin(key, out Vector2 origin)) return -1;

            for (int i = 0; i < monitors.Count; i++)
            {
                Rect r = monitors[i].FullOsRect;
                if (r.width <= 0f || r.height <= 0f) continue;
                if (Mathf.Abs(r.x - origin.x) <= OriginMatchTolerance
                    && Mathf.Abs(r.y - origin.y) <= OriginMatchTolerance)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>OS가 주 모니터라고 <b>플래그로</b> 말한 인덱스(없으면 -1).
        /// <b>좌표가 (0,0)인 것을 주 모니터로 추정하지 않는다</b> — 그 추정은 네이티브 라이브러리들이
        /// 쓰는 것이고, 우리는 플래그를 직접 읽을 수 있으므로 추정할 이유가 없다.</summary>
        public static int IndexOfPrimary(IReadOnlyList<OsMonitorFact> monitors)
            => MonitorTopologyReport.FindOsPrimaryIndex(monitors);

        /// <summary>
        /// <b>가장 왼쪽 모니터</b>의 인덱스(없으면 -1) — 지금의 <b>기본값</b>.
        ///
        /// <para><b>OS 목록의 순서를 믿지 않는다.</b> <c>EnumDisplayMonitors</c>/<c>CGGetActiveDisplayList</c>는
        /// 정렬을 보장하지 않는다(정렬하는 것은 <b>라이브러리</b> 쪽이다). 그래서 여기서 <c>x</c> 최솟값을
        /// 직접 고른다 — "0번이 왼쪽"이라는 사실은 라이브러리 목록에만 해당한다.</para>
        ///
        /// <para><b>동점 처리</b>: 위아래로 쌓은 배치는 <c>x</c>가 같다. 그때는 <c>y</c>가 작은 쪽
        /// (OS 좌표계에서 <b>위쪽</b>)을 고른다 — 임의로 고르면 실행마다 답이 흔들려서
        /// <see cref="DisplayTopologyWatcher"/>가 구성 변경으로 오인한다. 이 배치에서 "왼쪽/오른쪽"이라는
        /// 말 자체가 성립하지 않는다는 사실은 <see cref="IsHorizontalArrangement"/>가 따로 알려 준다.</para>
        /// </summary>
        public static int IndexOfLeftmost(IReadOnlyList<OsMonitorFact> monitors)
        {
            if (monitors == null) return -1;
            int best = -1;
            for (int i = 0; i < monitors.Count; i++)
            {
                Rect r = monitors[i].FullOsRect;
                if (r.width <= 0f || r.height <= 0f) continue;
                if (best < 0) { best = i; continue; }

                Rect b = monitors[best].FullOsRect;
                if (r.x < b.x - OriginMatchTolerance) { best = i; continue; }
                if (Mathf.Abs(r.x - b.x) <= OriginMatchTolerance && r.y < b.y - OriginMatchTolerance) best = i;
            }
            return best;
        }

        /// <summary>
        /// <b>배치의 축</b> — <c>왼쪽/오른쪽</c>이 사실인지, <c>위쪽/아래쪽</c>이 사실인지,
        /// 아니면 <b>둘 다 거짓</b>인지.
        ///
        /// ============================================================================
        /// ★ 중심 일치 검사가 <b>첫 줄</b>이다 (ux-designer가 자기 초안의 함정을 자백했다)
        /// ============================================================================
        /// 미러링(두 화면이 같은 픽셀)에서 두 사각형은 <b>완전히 겹친다</b>. 겹침 비교를 먼저 하면
        /// <c>overlapX &gt; overlapY</c>가 되어 <b>"세로 배치"로 떨어진다</b> — 그러면 UI가
        /// <c>위쪽/아래쪽</c> 칩 두 개를 그리는데 두 화면은 같은 화면이다(원칙 1 위반).
        /// 그래서 <b>중심이 겹치는 쌍이 하나라도 있으면 즉시 <see cref="MonitorArrangementAxis.Indistinct"/></b>다.
        ///
        /// <para><b>실측 수단이 따로 있다</b>: macOS <c>CGDisplayIsInMirrorSet</c>은
        /// <c>docs/SCREEN_SHARE_DETECTION.md</c> A2에서 "화면 공유 감지" 후보로는 기각됐지만
        /// <b>미러링 판정에는 정확히 맞는 용도</b>다. 다만 그것은 <b>새 P/Invoke</b>이고 Windows에는
        /// 대응물이 사실상 없다(같은 문서 W5: 미러 드라이버는 레거시). 기하만으로 같은 결론에
        /// 도달할 수 있으므로 <b>이번에는 기하로 판정한다</b> — 실기에서 이 판정이 빗나가는 사례가
        /// 나오면 그때 macOS 쪽만 그 API로 보강하면 된다(플랫폼 비대칭이 되므로 그때 갭으로 등재).</para>
        ///
        /// <para><b>축 판정은 반드시 <see cref="OsMonitorFact.FullOsRect"/>를 쓴다</b>
        /// (ux-designer 지적, 채택). <c>WorkOsRect</c>는 Dock 위치에 따라 macOS에서만
        /// 세로 겹침이 달라져서 <b>같은 물리 배치가 OS마다 다른 축</b>으로 판정된다.</para>
        ///
        /// <para><b>대각선 배치</b>(가로로도 세로로도 분리)는 순서가 두 가지로 다 성립한다.
        /// 그때는 <see cref="MonitorArrangementAxis.Horizontal"/>을 고른다 — 사용자의 말이
        /// "왼쪽 오른쪽"이었고, 그것이 압도적으로 흔한 배치다. 임의로 고르지 않고 규칙으로 고정해야
        /// 실행마다 답이 흔들리지 않는다.</para>
        /// </summary>
        public static MonitorArrangementAxis ResolveAxis(IReadOnlyList<OsMonitorFact> monitors)
        {
            if (monitors == null || monitors.Count <= 1) return MonitorArrangementAxis.Indistinct;

            // ---- (1) 중심 일치 = 미러링/완전 겹침. 반드시 먼저. ----
            for (int i = 0; i < monitors.Count; i++)
            {
                Rect a = monitors[i].FullOsRect;
                if (a.width <= 0f || a.height <= 0f) continue;
                for (int j = i + 1; j < monitors.Count; j++)
                {
                    Rect b = monitors[j].FullOsRect;
                    if (b.width <= 0f || b.height <= 0f) continue;
                    if (Mathf.Abs(a.center.x - b.center.x) <= OriginMatchTolerance
                        && Mathf.Abs(a.center.y - b.center.y) <= OriginMatchTolerance)
                    {
                        return MonitorArrangementAxis.Indistinct;
                    }
                }
            }

            // ---- (2) 축별로 "이웃한 두 화면이 그 축에서 분리되는가" ----
            bool horizontal = AxisSeparates(monitors, horizontalAxis: true);
            bool vertical = AxisSeparates(monitors, horizontalAxis: false);

            if (horizontal) return MonitorArrangementAxis.Horizontal;   // 대각선이면 여기서 가로로 확정.
            if (vertical) return MonitorArrangementAxis.Vertical;
            return MonitorArrangementAxis.Indistinct;
        }

        /// <summary>그 축으로 정렬했을 때 <b>이웃한 모든 쌍</b>이 겹치지 않는가.</summary>
        private static bool AxisSeparates(IReadOnlyList<OsMonitorFact> monitors, bool horizontalAxis)
        {
            int n = monitors.Count;
            var order = SortedIndices(monitors, horizontalAxis);
            for (int i = 1; i < n; i++)
            {
                Rect prev = monitors[order[i - 1]].FullOsRect;
                Rect cur = monitors[order[i]].FullOsRect;
                float prevMax = horizontalAxis ? prev.xMax : prev.yMax;
                float curMin = horizontalAxis ? cur.x : cur.y;
                if (curMin < prevMax - OriginMatchTolerance) return false;
            }
            return true;
        }

        /// <summary>축 방향 오름차순 인덱스(개수가 한 자리라 삽입 정렬로 충분하다).</summary>
        private static int[] SortedIndices(IReadOnlyList<OsMonitorFact> monitors, bool horizontalAxis)
        {
            int n = monitors.Count;
            var order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            for (int i = 1; i < n; i++)
            {
                int cur = order[i];
                float key = AxisKey(monitors[cur].FullOsRect, horizontalAxis);
                int j = i - 1;
                while (j >= 0 && AxisKey(monitors[order[j]].FullOsRect, horizontalAxis) > key)
                {
                    order[j + 1] = order[j];
                    j--;
                }
                order[j + 1] = cur;
            }
            return order;
        }

        private static float AxisKey(Rect r, bool horizontalAxis) => horizontalAxis ? r.x : r.y;

        /// <summary>
        /// <b>설정 행을 살려도 되는가</b> — <c>ux-designer</c> §49가 게이트 입력으로 요구한 값.
        /// 모니터 2대 이상 <b>그리고</b> 축이 서야 한다. 미러링에서 행이 살아 있으면
        /// "고를 것이 둘인데 화면은 하나"가 된다.
        /// </summary>
        public static bool CanChoose(IReadOnlyList<OsMonitorFact> monitors)
            => IsMultiMonitor(monitors?.Count ?? 0)
               && ResolveAxis(monitors) != MonitorArrangementAxis.Indistinct;

        /// <summary>
        /// 그 자리에 해당하는 모니터 인덱스(정할 수 없으면 -1).
        ///
        /// <para>★ <b>축에 따라 정렬 키가 달라진다</b>(ux-designer 지적, 채택). 네이티브 라이브러리는
        /// <c>x</c>만 보고 정렬하므로 <b>세로 배치에서는 순서를 정하지 못한다</b> — 그 목록을 그대로
        /// 믿으면 위/아래가 뒤바뀐다. 그래서 여기서 축에 맞는 키로 다시 고른다.</para>
        /// </summary>
        public static int IndexOfSlot(IReadOnlyList<OsMonitorFact> monitors,
            MonitorArrangementAxis axis, OverlayMonitorSlot slot)
        {
            if (monitors == null || monitors.Count == 0) return -1;
            if (axis == MonitorArrangementAxis.Indistinct) return -1;

            bool horizontal = axis == MonitorArrangementAxis.Horizontal;
            int best = -1;
            for (int i = 0; i < monitors.Count; i++)
            {
                Rect r = monitors[i].FullOsRect;
                if (r.width <= 0f || r.height <= 0f) continue;
                if (best < 0) { best = i; continue; }

                float cur = AxisKey(r, horizontal);
                float bestKey = AxisKey(monitors[best].FullOsRect, horizontal);
                bool better = slot == OverlayMonitorSlot.Start
                    ? cur < bestKey - OriginMatchTolerance
                    : cur > bestKey + OriginMatchTolerance;
                if (better) best = i;
            }
            return best;
        }

        /// <summary>저장 파일에 적히는 자리 이름. 고른 적이 없으면 빈 문자열(잉크색과 같은 관례).</summary>
        public static string SlotSaveName(OverlayMonitorSlot slot) => slot.ToString();

        /// <summary>
        /// 저장된 이름을 자리로 되돌린다. <b>모르는 이름은 "고른 적 없음"</b>으로 떨어뜨린다 —
        /// 죽은 값을 사용자의 선택으로 오해하는 것보다 기본값으로 돌아가는 쪽이 언제나 안전하다.
        ///
        /// <para>★ 이 관용성이 <b>이행(migration) 장치</b>이기도 하다: 중간 빌드가 남긴 옛 좌표 키
        /// (<c>"1920,0@2560x1440"</c>)는 여기서 파싱에 실패해 <b>조용히 기본값</b>이 된다.
        /// 저장 스키마 버전을 또 올리지 않아도 되는 이유다.</para>
        /// </summary>
        public static bool TryParseSlot(string name, out OverlayMonitorSlot slot)
        {
            slot = OverlayMonitorSlot.Start;
            if (string.IsNullOrEmpty(name)) return false;
            if (!System.Enum.TryParse(name, out OverlayMonitorSlot parsed)) return false;
            if (parsed != OverlayMonitorSlot.Start && parsed != OverlayMonitorSlot.End) return false;
            slot = parsed;
            return true;
        }

        /// <summary>
        /// 최종 선택. 우선순위는        /// <summary>
        /// 최종 선택. 우선순위는 <b>사용자 선택 → 주 모니터</b> 둘뿐이다(예전 B/C/E 설계는 폐기됐다).
        /// </summary>
        /// <param name="monitors">지금 OS가 보고한 모니터 전수.</param>
        /// <param name="preferredKey">사용자가 고른 화면의 키(고른 적 없으면 null/빈 문자열).</param>
        public static OverlayMonitorChoice Resolve(IReadOnlyList<OsMonitorFact> monitors, string preferredSlotName)
        {
            if (monitors == null || monitors.Count == 0)
            {
                return new OverlayMonitorChoice(-1, OverlayMonitorChoiceSource.NoMonitors);
            }

            MonitorArrangementAxis axis = ResolveAxis(monitors);

            // 축이 서지 않으면(미러링/1대) 자리 개념이 없다. 그래도 창은 어딘가에 떠야 하므로
            // <b>결정적인</b> 답을 준다 — x 최솟값. 흔들리면 DisplayTopologyWatcher가 구성 변경으로 오인한다.
            if (axis == MonitorArrangementAxis.Indistinct)
            {
                int fallback = IndexOfLeftmost(monitors);
                if (fallback < 0) return new OverlayMonitorChoice(-1, OverlayMonitorChoiceSource.Indeterminate);
                // 사용자가 골라 둔 상태에서 배치가 미러링으로 바뀌었다면 그 사실을 값으로 남긴다.
                return TryParseSlot(preferredSlotName, out _)
                    ? new OverlayMonitorChoice(fallback, OverlayMonitorChoiceSource.UserPreferredMissing)
                    : new OverlayMonitorChoice(fallback, OverlayMonitorChoiceSource.StartSlotDefault);
            }

            if (TryParseSlot(preferredSlotName, out OverlayMonitorSlot chosen))
            {
                int at = IndexOfSlot(monitors, axis, chosen);
                if (at >= 0) return new OverlayMonitorChoice(at, OverlayMonitorChoiceSource.UserPreferred);
            }

            int start = IndexOfSlot(monitors, axis, OverlayMonitorSlot.Start);
            return start >= 0
                ? new OverlayMonitorChoice(start, OverlayMonitorChoiceSource.StartSlotDefault)
                : new OverlayMonitorChoice(-1, OverlayMonitorChoiceSource.Indeterminate);
        }

        /// <summary>
        /// <b>OS 모니터 인덱스 → 라이브러리 모니터 인덱스</b> 변환(없으면 -1).
        ///
        /// <para>두 목록은 <b>순서도 좌표계도 다르다</b>. 라이브러리는 자기 정렬(왼쪽부터)을 하고
        /// y를 뒤집으며(Windows) 사각형이 작업영역일 수도 있다(macOS <c>visibleFrame</c>).
        /// 그래서 인덱스를 그대로 쓰면 안 되고, <b>x가 두 좌표계에서 변환 없이 같다</b>는 사실
        /// (양쪽 네이티브 모두 x를 손대지 않는다 — Phase 0에서 원문으로 확인)을 이용해 맞춘다.
        /// 그 매칭 자체는 Phase 0 계측이 이미 갖고 있으므로 재구현하지 않고 그대로 쓴다.</para>
        /// </summary>
        public static int LibraryIndexForOsMonitor(
            IReadOnlyList<Rect> libraryRects, IReadOnlyList<OsMonitorFact> osMonitors, int osIndex)
        {
            if (libraryRects == null || osMonitors == null) return -1;
            if (osIndex < 0 || osIndex >= osMonitors.Count) return -1;

            for (int i = 0; i < libraryRects.Count; i++)
            {
                Rect r = libraryRects[i];
                if (r.width <= 0f || r.height <= 0f) continue;
                if (MonitorTopologyReport.MatchLibraryRectToOsMonitor(r, osMonitors) == osIndex) return i;
            }
            return -1;
        }

        /// <summary>사람이 읽는 한 줄(전이 순간에만 조립 — 폴링 경로에서 문자열을 만들지 않는다).</summary>
        public static string Describe(OverlayMonitorChoice choice, string preferredKey)
        {
            switch (choice.Source)
            {
                case OverlayMonitorChoiceSource.UserPreferred:
                    return $"사용자가 고른 자리({preferredKey})를 확정했습니다 -> {choice.Index}번";
                case OverlayMonitorChoiceSource.UserPreferredMissing:
                    return $"★ 사용자가 고른 자리({preferredKey})를 <b>지금 배치에서 정할 수 없습니다</b> — " +
                        "미러링이거나 화면이 겹쳐 있어 축이 서지 않습니다. " +
                        $"결정적인 기본값({choice.Index}번)으로 갑니다.";
                case OverlayMonitorChoiceSource.StartSlotDefault:
                    return $"기본값 — 축의 시작 자리 {choice.Index}번(가로면 가장 왼쪽, 세로면 가장 위. " +
                        "주 모니터 플래그와는 다른 개념이며, 둘이 갈리는지는 [모니터지형] 줄이 실측으로 말해 줍니다)";
                case OverlayMonitorChoiceSource.Indeterminate:
                    return "★ 표시 모니터를 정하지 못했습니다(모니터 목록이 비었거나 전부 퇴화 사각형). " +
                        "0번으로 위장하지 않고 <b>현재 동작을 그대로 유지</b>합니다.";
                default:
                    return "모니터 목록이 비어 있습니다 — 현재 동작을 유지합니다.";
            }
        }
    }
}
