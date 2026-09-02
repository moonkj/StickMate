using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// 라이브러리 좌표계의 y 규약. <b>추측이 아니라 네이티브 원문에서 확인한 사실</b>이며,
    /// 이 열거형이 존재하는 이유 자체가 "두 플랫폼이 같은 API 이름으로 서로 다른 값을 준다"는 것이다.
    /// </summary>
    public enum LibraryMonitorYConvention
    {
        /// <summary>Windows(<c>libuniwinc.cpp</c>). <c>GetMonitorRectangle</c>/<c>GetPosition</c>이
        /// <c>y = nPrimaryMonitorHeight_ - rect.bottom</c>으로 <b>주 모니터 하단을 원점으로 하는
        /// 상향 y</b>를 만든다. 즉 Win32의 하향 y를 뒤집은 값이다.</summary>
        FlippedFromPrimaryBottom,

        /// <summary>macOS(<c>LibUniWinC.swift</c>). <c>NSScreen.visibleFrame</c>을 그대로 넘기므로
        /// <b>이미 Cocoa 좌하단 원점</b>이고 뒤집기가 없다. 대신 사각형이 <b>작업영역</b>이라
        /// 메뉴바/Dock 띠가 빠져 있다(Windows는 모니터 전체다).</summary>
        CocoaBottomLeft
    }

    /// <summary>
    /// OS가 직접 말하는 모니터 사실 하나. <b>플랫폼 전용 코드는 이 구조체를 채우기만 한다</b>
    /// (사실 조회). 무엇을 뜻하는지 계산하고 사람이 읽을 줄로 만드는 일은 전부
    /// <see cref="MonitorTopologyReport"/>에 있다 — <c>FullscreenSuspendPolicy</c>가
    /// <c>Platform/MacOS/</c> 안에 있어서 Windows가 물리적으로 부를 수 없었던 사고의 재발 방지 형태다.
    /// </summary>
    public readonly struct OsMonitorFact
    {
        /// <summary>모니터 전체 사각형. <b>OS 고유의 하향 y 좌표계</b>로 담는다
        /// (Windows <c>MONITORINFO.rcMonitor</c> / macOS <c>CGDisplayBounds</c>).
        /// <c>x=left, y=top, width, height</c>이므로 <c>yMax</c>가 곧 bottom이다.</summary>
        public readonly Rect FullOsRect;

        /// <summary>작업 영역(예약 막대 제외). 조회할 수 없으면 <see cref="FullOsRect"/>와 같게 둔다.
        /// Windows는 <c>rcWork</c>, macOS는 지금 이 경로로는 알 수 없다.</summary>
        public readonly Rect WorkOsRect;

        /// <summary>OS가 <b>주 모니터라고 말했는가</b>. 추론이 아니라 플래그다
        /// (Windows <c>MONITORINFOF_PRIMARY</c> / macOS <c>CGMainDisplayID</c> 일치).</summary>
        public readonly bool IsPrimary;

        /// <summary>사람이 읽을 식별자(핸들/디스플레이 ID 등). 없으면 빈 문자열.</summary>
        public readonly string Label;

        public OsMonitorFact(Rect fullOsRect, Rect workOsRect, bool isPrimary, string label)
        {
            FullOsRect = fullOsRect;
            WorkOsRect = workOsRect;
            IsPrimary = isPrimary;
            Label = label ?? string.Empty;
        }
    }

    /// <summary>OS 모니터 전수 열거. 성공하면 true. 플랫폼 서비스가 구현하고 Enforcer에 주입한다
    /// (<c>OverlayRectReporter</c>와 같은 배선 형태).</summary>
    public delegate bool OsMonitorEnumerator(List<OsMonitorFact> into);

    /// <summary>
    /// ============================================================================
    /// Phase 0 — 모니터 지형 계측 한 줄 (2026-09-02, 동작 변경 0)
    /// ============================================================================
    /// "우리 오버레이가 어느 모니터에 뜨는가"를 정하는 정책(B/C/E)을 넣기 <b>전에</b>, 지금 추측으로
    /// 남아 있는 것을 전부 확정하기 위한 <b>순수 계측</b>이다. 기동 시 딱 한 번, 한 줄.
    ///
    /// <para>이 줄이 확정하는 것:</para>
    /// <list type="number">
    ///   <item><b>라이브러리 인덱스 ↔ OS 모니터 매핑</b> — 지금까지 어디에도 실측이 없었다.</item>
    ///   <item><b>인덱스 0이 주 모니터인가</b> — <see cref="ClaimIndexZeroIsPrimary"/> 참고.
    ///         <c>MacOverlayStateEnforcer.TryGetTargetMonitorRect</c>가 <c>i == 0</c>을
    ///         "주 모니터"로 단정하고 있어서, 이 답이 아니오면 그 코드는 틀린 것이다.</item>
    ///   <item><b>y 반전 규약</b> — <see cref="YFlipResidual"/>가 0이 아니면 우리가 아는 항등식이 깨진 것이다.</item>
    ///   <item><b>우리가 실제로 어디 있는가</b> — 라이브러리 좌표와 OS 실측을 나란히 찍는다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 네이티브 원문에서 이미 확정된 것 (실측 이전에 코드로 답이 나온 항목)
    /// ============================================================================
    /// 이 계측을 설계하면서 두 네이티브 원문을 실제로 읽었고, 다음 두 가지는 <b>실기 없이 확정</b>됐다.
    /// 계측은 이것을 <b>반증할 기회</b>로 남겨 둔다(맞다고 가정하고 넘어가지 않는다).
    /// <list type="bullet">
    ///   <item><b>인덱스 0은 "가장 왼쪽" 모니터이지 주 모니터가 아니다.</b> 양쪽 다 정렬한다 —
    ///     Windows <c>updateMonitorRectangles()</c>는 <c>left</c> 오름차순 버블 정렬,
    ///     macOS <c>_updateScreenInfo()</c>는 <c>minX</c> 오름차순 정렬. 주 모니터가 원점(0,0)에
    ///     있으므로 <b>왼쪽에 보조 모니터를 둔 사용자</b>에게는 0번이 주 모니터가 아니다.</item>
    ///   <item><b>y 규약이 플랫폼마다 다르다</b> — <see cref="LibraryMonitorYConvention"/>.</item>
    /// </list>
    ///
    /// <para><b>정직한 한계 1 — 라이브러리의 <c>GetCurrentMonitor()</c>는 부를 수 없다.</b>
    /// <c>UniWinCore</c>가 <c>internal</c>이고 <c>LibUniWinC</c>가 <c>protected</c>라
    /// 우리 어셈블리에서 도달 경로가 없다(<c>UniWindowController</c>는 이 함수를 노출하지 않는다).
    /// 그래서 <see cref="ResolveCurrentMonitorIndex"/>가 <b>네이티브와 같은 규칙</b>(창 중심을 포함하는
    /// 첫 모니터, 없으면 원점 모니터)을 우리 쪽에서 재현하고, 로그에는 <b>"파생값"</b>이라고 명시한다.
    /// 라이브러리가 실제로 어떻게 답하는지는 이 줄로는 알 수 없다.</para>
    ///
    /// <para><b>정직한 한계 2 — 이 개발 머신은 디스플레이 1대다.</b> 다중 모니터 매핑은 이 줄이
    /// 사용자 실기에서 나와야 확정된다. 1대에서 확인할 수 있는 것은 <b>계측 자체가 죽어 있지 않다</b>는
    /// 양성 대조뿐이다.</para>
    /// </summary>
    public static class MonitorTopologyReport
    {
        public const string LogTag = "[모니터지형]";

        /// <summary>프로세스당 한 번만 낸다. 24시간 상주 앱이라 반복 로그는 그 자체로 결함이다.</summary>
        public static bool Emitted { get; private set; }

        /// <summary>테스트 전용(같은 프로세스에서 여러 번 조립을 검증하기 위한 통로).</summary>
        public static void ResetEmittedForTests() => Emitted = false;

        // ====================================================================
        // 순수 판정 — 여기부터는 UnityEngine.Rect 외에 아무 의존도 없다(EditMode 실행 가능).
        // ====================================================================

        /// <summary>
        /// 창 중심이 속한 라이브러리 모니터 인덱스. <b>네이티브 <c>GetCurrentMonitor()</c>와 같은 규칙</b>이다
        /// (원문: 창 중심을 포함하는 첫 모니터, 없으면 원점에 있는 모니터, 그것도 없으면 0).
        /// </summary>
        /// <param name="containedCenter">중심 포함으로 답이 나왔는가. false면 폴백을 쓴 것이다 —
        /// 로그에서 이 둘을 반드시 구분한다(폴백은 "모른다"에 가깝다).</param>
        public static int ResolveCurrentMonitorIndex(IReadOnlyList<Rect> libraryRects,
            Vector2 windowCenterLibrary, out bool containedCenter)
        {
            containedCenter = false;
            if (libraryRects == null || libraryRects.Count == 0) return -1;

            for (int i = 0; i < libraryRects.Count; i++)
            {
                Rect r = libraryRects[i];
                if (r.width <= 0f || r.height <= 0f) continue;
                if (r.Contains(windowCenterLibrary))
                {
                    containedCenter = true;
                    return i;
                }
            }

            for (int i = 0; i < libraryRects.Count; i++)
            {
                Rect r = libraryRects[i];
                if (r.width <= 0f || r.height <= 0f) continue;
                if (Mathf.Approximately(r.x, 0f) && Mathf.Approximately(r.y, 0f)) return i;
            }
            return 0;
        }

        /// <summary>OS가 주 모니터라고 말한 항목의 인덱스(없으면 -1).</summary>
        public static int FindOsPrimaryIndex(IReadOnlyList<OsMonitorFact> osMonitors)
        {
            if (osMonitors == null) return -1;
            for (int i = 0; i < osMonitors.Count; i++)
            {
                if (osMonitors[i].IsPrimary) return i;
            }
            return -1;
        }

        /// <summary>
        /// 라이브러리 사각형 하나가 어느 OS 모니터의 것인지 찾는다(없으면 -1).
        ///
        /// <para>x(=left)는 두 좌표계에서 <b>변환 없이 같다</b>(양쪽 네이티브 모두 x를 손대지 않는다).
        /// 그래서 x가 일치하고 폭이 OS 모니터 폭을 넘지 않는 항목을 고른다 — macOS는 라이브러리가
        /// <c>visibleFrame</c>을 주므로 <b>폭이 같지 않을 수 있고</b>(Dock이 좌/우에 있을 때),
        /// 그래서 "같다"가 아니라 "안에 들어간다"로 묻는다.</para>
        /// </summary>
        public static int MatchLibraryRectToOsMonitor(Rect libraryRect,
            IReadOnlyList<OsMonitorFact> osMonitors, float tolerancePixels = 1f)
        {
            if (osMonitors == null) return -1;
            for (int i = 0; i < osMonitors.Count; i++)
            {
                Rect os = osMonitors[i].FullOsRect;
                if (os.width <= 0f || os.height <= 0f) continue;
                if (libraryRect.x + tolerancePixels < os.x) continue;
                if (libraryRect.xMax - tolerancePixels > os.xMax) continue;
                if (libraryRect.width > os.width + tolerancePixels) continue;
                return i;
            }
            return -1;
        }

        /// <summary>
        /// y 반전 항등식의 <b>잔차</b>. 0이면 우리가 아는 규약이 맞는 것이고, 0이 아니면
        /// <b>그 순간 이후의 모든 y 계산이 틀렸다</b>는 뜻이다.
        ///
        /// <para>Windows: <c>libY == primaryOsBottom − osFullRect.bottom</c>.</para>
        /// <para>macOS: 뒤집지 않으므로 이 항등식이 성립할 이유가 없다 — 그래서
        /// <see cref="LibraryMonitorYConvention.CocoaBottomLeft"/>에서는 <see cref="float.NaN"/>을
        /// 돌려주고 로그에 "해당 없음"으로 찍는다(0을 돌려주면 "검증 통과"로 오독된다).</para>
        /// </summary>
        public static float YFlipResidual(Rect libraryRect, Rect osFullRect,
            float primaryOsBottom, LibraryMonitorYConvention convention)
        {
            if (convention != LibraryMonitorYConvention.FlippedFromPrimaryBottom) return float.NaN;
            if (float.IsNaN(primaryOsBottom)) return float.NaN;
            return libraryRect.y - (primaryOsBottom - osFullRect.yMax);
        }

        /// <summary>
        /// <b>라이브러리 0번이 주 모니터인가.</b> 네이티브 정렬이 "가장 왼쪽 먼저"이므로 이 답은
        /// 사용자 배치에 따라 달라진다. 알 수 없으면 null(모른다와 아니다를 구분한다).
        /// </summary>
        public static bool? ClaimIndexZeroIsPrimary(IReadOnlyList<Rect> libraryRects,
            IReadOnlyList<OsMonitorFact> osMonitors)
        {
            if (libraryRects == null || libraryRects.Count == 0) return null;
            if (osMonitors == null || osMonitors.Count == 0) return null;
            int matched = MatchLibraryRectToOsMonitor(libraryRects[0], osMonitors);
            if (matched < 0) return null;
            return osMonitors[matched].IsPrimary;
        }

        /// <summary>주 모니터의 하단 y(OS 좌표). 없으면 NaN.</summary>
        public static float ResolvePrimaryOsBottom(IReadOnlyList<OsMonitorFact> osMonitors)
        {
            int primary = FindOsPrimaryIndex(osMonitors);
            return primary < 0 ? float.NaN : osMonitors[primary].FullOsRect.yMax;
        }

        /// <summary>
        /// 계측 한 줄을 조립한다. <b>부수 효과 0</b> — 그래서 EditMode가 이 함수를 실제로 실행해
        /// "줄이 비어 있지 않다 / 항목 수가 맞는다"를 검증할 수 있다.
        /// </summary>
        public static string Compose(
            string platformLabel,
            LibraryMonitorYConvention convention,
            IReadOnlyList<Rect> libraryRects,
            Rect overlayLibraryRect,
            IReadOnlyList<OsMonitorFact> osMonitors,
            bool osEnumerationOk,
            IReadOnlyList<Rect> unityDisplayRects,
            Rect overlayOsRect,
            bool overlayOsRectKnown)
        {
            var sb = new StringBuilder(768);
            int libCount = libraryRects?.Count ?? 0;
            int osCount = osMonitors?.Count ?? 0;
            int unityCount = unityDisplayRects?.Count ?? 0;

            sb.Append(LogTag).Append(' ').Append(platformLabel)
              .Append(" — 라이브러리 ").Append(libCount).Append("개 / OS ")
              .Append(osEnumerationOk ? osCount.ToString() : "조회실패")
              .Append("개 / Unity 표시 ").Append(unityCount).Append("개. ")
              .Append("y규약=").Append(convention == LibraryMonitorYConvention.FlippedFromPrimaryBottom
                  ? "주모니터하단기준 상향(Win32 하향 y를 뒤집음)"
                  : "Cocoa 좌하단 그대로(뒤집기 없음, 사각형은 visibleFrame)");

            float primaryBottom = ResolvePrimaryOsBottom(osMonitors);

            sb.Append("\n  라이브러리: ");
            for (int i = 0; i < libCount; i++)
            {
                Rect r = libraryRects[i];
                if (i > 0) sb.Append("  ");
                sb.Append('L').Append(i).Append('=').Append(Describe(r));
                int matched = MatchLibraryRectToOsMonitor(r, osMonitors);
                sb.Append(matched >= 0 ? $"→OS{matched}" : "→OS?");
                float residual = matched >= 0
                    ? YFlipResidual(r, osMonitors[matched].FullOsRect, primaryBottom, convention)
                    : float.NaN;
                sb.Append(float.IsNaN(residual) ? "(y잔차 해당없음)" : $"(y잔차 {residual:F1})");
            }

            sb.Append("\n  OS: ");
            if (!osEnumerationOk)
            {
                sb.Append("전수 열거 실패 — 아래 대조는 전부 판정 불가입니다(0으로 위장하지 않습니다).");
            }
            else
            {
                for (int i = 0; i < osCount; i++)
                {
                    OsMonitorFact f = osMonitors[i];
                    if (i > 0) sb.Append("  ");
                    sb.Append("OS").Append(i).Append("=전체").Append(Describe(f.FullOsRect))
                      .Append("작업").Append(Describe(f.WorkOsRect))
                      .Append(f.IsPrimary ? "★주" : "부");
                    if (f.Label.Length > 0) sb.Append('[').Append(f.Label).Append(']');
                }
            }

            sb.Append("\n  Unity(작업영역): ");
            if (unityCount == 0) sb.Append("Screen.GetDisplayLayout이 0건(또는 조회 불가)");
            for (int i = 0; i < unityCount; i++)
            {
                if (i > 0) sb.Append("  ");
                sb.Append('U').Append(i).Append('=').Append(Describe(unityDisplayRects[i]));
            }

            Vector2 center = overlayLibraryRect.position + overlayLibraryRect.size * 0.5f;
            int derived = ResolveCurrentMonitorIndex(libraryRects, center, out bool contained);
            bool? zeroIsPrimary = ClaimIndexZeroIsPrimary(libraryRects, osMonitors);

            sb.Append("\n  우리 창: 라이브러리=").Append(Describe(overlayLibraryRect))
              // ★ 이 값은 "OS 실측"이 아니라 <b>좌표계가 지금 들고 있는 원점</b>이다
              //   (ScreenCoordinateConverter.OverlayOriginOsScreen). 기동 직후에는 아직 아무도
              //   보고하지 않아 (0,0)일 수 있고, 그 (0,0)은 "주 모니터 원점"과 구분되지 않는다 —
              //   그래서 OS라고 부르지 않는다. 진짜 OS 사각형은 위 OS 구획이다.
              .Append(" 좌표계 원점=").Append(overlayOsRectKnown ? Describe(overlayOsRect) : "(미확보) ")
              .Append("· 파생 현재 모니터=").Append(derived)
              .Append(contained ? "(창중심 포함)" : "(★폴백 — 어느 모니터에도 안 들어감)")
              .Append(" · 라이브러리 GetCurrentMonitor()는 호출 불가(UniWinCore internal)");

            sb.Append("\n  판정: 라이브러리 0번 = 주 모니터? ")
              .Append(zeroIsPrimary.HasValue ? (zeroIsPrimary.Value ? "예" : "★아니오") : "알 수 없음")
              .Append(" · 주 모니터 하단 y(OS)=")
              .Append(float.IsNaN(primaryBottom) ? "알 수 없음" : primaryBottom.ToString("F0"))
              .Append(" · 네이티브 정렬 규칙은 '가장 왼쪽 먼저'이므로 이 답은 배치에 따라 달라집니다.");

            return sb.ToString();
        }

        private static string Describe(Rect r)
            => $"({r.x:F0},{r.y:F0} {r.width:F0}x{r.height:F0}) ";

        // ====================================================================
        // 실행 진입점 — 여기만 UnityEngine 런타임 API를 만진다.
        // ====================================================================

        private static readonly List<OsMonitorFact> OsBuffer = new List<OsMonitorFact>(8);
        private static readonly List<Rect> LibBuffer = new List<Rect>(8);
        private static readonly List<Rect> UnityBuffer = new List<Rect>(8);
        private static readonly List<DisplayInfo> DisplayBuffer = new List<DisplayInfo>(8);

        /// <summary>
        /// 기동 시 한 번만 계측 줄을 낸다. <b>어떤 창도 건드리지 않는다</b>(조회 + 로그).
        /// 진단이 앱을 죽이면 안 되므로 전부 <c>try</c>로 감싼다 — 이 줄이 없어도 앱은 돌아야 한다.
        /// </summary>
        public static void EmitOnce(
            string platformLabel,
            LibraryMonitorYConvention convention,
            int libraryMonitorCount,
            System.Func<int, Rect> libraryRectAt,
            Rect overlayLibraryRect,
            OsMonitorEnumerator osEnumerator,
            Rect overlayOsRect,
            bool overlayOsRectKnown)
        {
            if (Emitted) return;
            Emitted = true;

            try
            {
                LibBuffer.Clear();
                if (libraryRectAt != null)
                {
                    for (int i = 0; i < libraryMonitorCount; i++) LibBuffer.Add(libraryRectAt(i));
                }

                OsBuffer.Clear();
                bool osOk = false;
                if (osEnumerator != null)
                {
                    try { osOk = osEnumerator(OsBuffer); }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"{LogTag} OS 모니터 열거가 예외로 실패했습니다 — " +
                            $"{e.GetType().Name}: {e.Message}. 아래 줄의 OS 항목은 판정 불가입니다.");
                        osOk = false;
                    }
                }

                UnityBuffer.Clear();
                try
                {
                    DisplayBuffer.Clear();
                    Screen.GetDisplayLayout(DisplayBuffer);
                    for (int i = 0; i < DisplayBuffer.Count; i++)
                    {
                        DisplayInfo d = DisplayBuffer[i];
                        // workArea 하나만 쓴다(위치와 크기를 다른 출처에서 섞지 않는다).
                        // Windows에서 이 값은 작업표시줄을 뺀 영역이라 OS 구획의 rcWork와 짝이 맞고,
                        // 그 차이가 곧 예약 막대 두께로 로그에서 그대로 읽힌다.
                        UnityBuffer.Add(new Rect(d.workArea.x, d.workArea.y,
                            d.workArea.width, d.workArea.height));
                    }
                }
                catch (System.Exception)
                {
                    UnityBuffer.Clear();   // 조회 불가 — 0건으로 남기고 로그가 그렇게 말한다.
                }

                Debug.Log(Compose(platformLabel, convention, LibBuffer, overlayLibraryRect,
                    OsBuffer, osOk, UnityBuffer, overlayOsRect, overlayOsRectKnown));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{LogTag} 계측 조립 자체가 실패했습니다 — {e.GetType().Name}: {e.Message}. " +
                    "이 줄은 진단 전용이므로 앱 동작에는 영향이 없습니다.");
            }
        }
    }
}
