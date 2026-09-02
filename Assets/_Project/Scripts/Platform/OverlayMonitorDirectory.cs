using System;
using System.Collections.Generic;
using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// ============================================================================
    /// 표시 모니터 — <b>설정 UI가 붙을 API 표면</b> (2026-09-02)
    /// ============================================================================
    /// 이 라운드의 역할 분담상 <b>설정창 배선은 다른 담당자</b>가 한다(리더 배정).
    /// 그래서 이 클래스는 <b>배선이 한 줄이 되도록</b>만 만들어 둔다. 실제로 필요한 것은 넷뿐이다:
    /// <list type="number">
    ///   <item><see cref="IsMultiMonitor"/> — 행을 살릴지(<c>SettingsRowGate.SetEnabled</c>)</item>
    ///   <item><see cref="Monitors"/> — 고를 항목들(사각형 + 주 모니터 여부). <b>문구는 담지 않는다</b></item>
    ///   <item><see cref="PreferredIndex"/> / <see cref="SelectMonitor"/> — 지금 무엇이 골라져 있고, 고르면 저장</item>
    ///   <item><see cref="Changed"/> — 실행 중 모니터가 붙거나 빠졌다. 설정창의 <c>RefreshAll()</c>이 여기 물린다</item>
    /// </list>
    ///
    /// <para>★ <b>사용자에게 보이는 문구를 이 파일에 넣지 않는다.</b> 행의 모양·문구·비활성 사유는
    /// <c>ux-designer</c>가 <c>docs/UX_FLOW.md</c>에 쓰고 있고, 플랫폼 담당이 문구를 지어내면
    /// 두 벌이 갈라진다. 여기서 나가는 문자열은 <b>로그 전용</b>이다.</para>
    ///
    /// <para><b>왜 정적 클래스인가</b>: 소비자가 셋(양 플랫폼 Enforcer, 설정창, 저장 모델)인데 서로를
    /// 참조할 경로가 없다. <c>ScreenCoordinateConverter</c>/<c>AppSettingsModel</c>이 이미 같은 형태로
    /// 이 문제를 풀고 있어 관례를 따른다. 상태는 "지금 OS가 보고한 목록" 하나뿐이다.</para>
    /// </summary>
    public static class OverlayMonitorDirectory
    {
        private static readonly List<OsMonitorFact> Live = new List<OsMonitorFact>(8);
        private static readonly List<OsMonitorFact> Empty = new List<OsMonitorFact>(0);

        /// <summary>지금 OS가 보고한 모니터 전수(읽기 전용). 아직 보고 전이면 비어 있다.</summary>
        public static IReadOnlyList<OsMonitorFact> Monitors => Live.Count > 0 ? Live : (IReadOnlyList<OsMonitorFact>)Empty;

        /// <summary>인식된 모니터 수.</summary>
        public static int MonitorCount => Live.Count;

        /// <summary>모니터를 2대 이상 인식했는가(사용자 확정 조건의 절반).</summary>
        public static bool IsMultiMonitor => OverlayMonitorChoicePolicy.IsMultiMonitor(Live.Count);

        /// <summary>배치의 축 — UI가 <c>왼쪽/오른쪽</c>과 <c>위쪽/아래쪽</c> 중 무엇을 쓸지 정한다.</summary>
        public static MonitorArrangementAxis Axis => OverlayMonitorChoicePolicy.ResolveAxis(Live);

        /// <summary>★ <b>설정 행 게이트의 입력</b>(ux-designer §49 요구). 2대 이상 <b>그리고</b> 축이 서야 한다 —
        /// 미러링에서 행이 살아 있으면 "고를 것이 둘인데 화면은 하나"가 된다.</summary>
        public static bool CanChoose => OverlayMonitorChoicePolicy.CanChoose(Live);

        /// <summary>모니터 구성이 바뀌었다(개수 또는 사각형). 설정창이 <c>RefreshAll()</c>을 여기 문다.
        /// <b>바뀔 때만</b> 발행한다 — 24시간 상주 앱이라 매 폴링 발행은 그 자체로 결함이다.</summary>
        public static event Action Changed;

        /// <summary>
        /// 플랫폼 서비스가 이번 관측을 보고한다. <b>내용이 실제로 달라졌을 때만</b> <see cref="Changed"/>를 쏜다.
        /// </summary>
        public static void Publish(IReadOnlyList<OsMonitorFact> monitors)
        {
            if (!DiffersFromLive(monitors))
            {
                return;
            }

            Live.Clear();
            if (monitors != null)
            {
                for (int i = 0; i < monitors.Count; i++) Live.Add(monitors[i]);
            }
            Changed?.Invoke();
        }

        private static bool DiffersFromLive(IReadOnlyList<OsMonitorFact> monitors)
        {
            int count = monitors?.Count ?? 0;
            if (count != Live.Count) return true;
            for (int i = 0; i < count; i++)
            {
                if (monitors[i].FullOsRect != Live[i].FullOsRect) return true;
                if (monitors[i].IsPrimary != Live[i].IsPrimary) return true;
            }
            return false;
        }

        /// <summary>사용자가 고른 <b>자리</b>. 고른 적이 없으면 <c>Start</c>(기본값)를 돌려준다 —
        /// UI는 이 값으로 어느 칩이 선택 상태인지 그리면 된다.</summary>
        public static OverlayMonitorSlot PreferredSlot
            => OverlayMonitorChoicePolicy.TryParseSlot(
                   Core.AppSettingsModel.PreferredOverlayMonitorKey, out OverlayMonitorSlot slot)
               ? slot : OverlayMonitorSlot.Start;

        /// <summary>그 자리에 해당하는 지금 목록의 인덱스(정할 수 없으면 -1).</summary>
        public static int IndexOfSlot(OverlayMonitorSlot slot)
            => OverlayMonitorChoicePolicy.IndexOfSlot(Live, Axis, slot);

        /// <summary>OS가 <b>주 모니터</b>라고 말한 인덱스(없으면 -1). 기본값 판정에는 쓰지 않는다
        /// (기본값은 "가장 왼쪽"이다) — 계측/진단과, macOS의 "화면 전체 덮기" 경로 판정에 쓴다.</summary>
        public static int PrimaryIndex => OverlayMonitorChoicePolicy.IndexOfPrimary(Live);

        /// <summary><b>기본 자리</b>(축의 시작)의 인덱스(없으면 -1).</summary>
        public static int LeftmostIndex => OverlayMonitorChoicePolicy.IndexOfLeftmost(Live);

        /// <summary>
        /// 지금 적용해야 할 최종 선택. Enforcer와 UI가 <b>같은 함수</b>를 본다 —
        /// 둘이 다른 계산을 하면 "설정에는 2번인데 창은 1번에 뜬다"가 된다.
        /// </summary>
        public static OverlayMonitorChoice Resolve()
            => OverlayMonitorChoicePolicy.Resolve(Live, Core.AppSettingsModel.PreferredOverlayMonitorKey);

        /// <summary>
        /// 설정 UI가 부르는 유일한 쓰기 통로. <paramref name="index"/>가 범위 밖이면
        /// <b>선택 해제</b>(= 기본값 주 모니터로 복귀)로 다룬다.
        /// </summary>
        public static void SelectSlot(OverlayMonitorSlot slot)
            => Core.AppSettingsModel.SetPreferredOverlayMonitor(
                   OverlayMonitorChoicePolicy.SlotSaveName(slot));

        /// <summary>인덱스로 고르는 편의 통로 — 지금 배치에서 그 인덱스가 어느 자리인지 역산한다.
        /// 양 끝이 아니면(3대 이상의 가운데) <b>아무것도 하지 않는다</b>: UI가 고를 수 없는 것을
        /// 저장하면 다음 실행에서 조용히 기본값으로 떨어진다.</summary>
        public static bool SelectMonitor(int index)
        {
            if (index < 0 || index >= Live.Count) { ClearSelection(); return true; }
            if (index == IndexOfSlot(OverlayMonitorSlot.Start)) { SelectSlot(OverlayMonitorSlot.Start); return true; }
            if (index == IndexOfSlot(OverlayMonitorSlot.End)) { SelectSlot(OverlayMonitorSlot.End); return true; }
            return false;
        }

        /// <summary>
        /// ★★ <b>오버레이가 실제로 덮는 화면의 OS 사각형</b>(2026-09-02 회귀 수정).
        ///
        /// <para><b>이 함수가 존재하는 이유</b>: "기본은 왼쪽"을 넣으면서 <b>우리가 만든 회귀 위험</b>이
        /// 있다. 발판 열거가 자르는 사각형과 오버레이가 덮는 사각형이 <b>서로 다른 화면</b>이 될 수 있고,
        /// 그러면 실제 창 발판이 <b>0개</b>가 되어 합성 안전망만 남는다 — 캐릭터가 남의 창 위에
        /// 서지 못한다는 뜻이고, 그건 이 앱의 정체성 그 자체다.</para>
        ///
        /// <para>지금까지 두 값이 맞았던 것은 <b>우연</b>이다(오버레이도 사실상 주 디스플레이에 떴다).
        /// 그래서 양 플랫폼이 <b>이 함수 하나</b>를 발판 클리핑 사각형으로 쓴다 — 우연이 아니라
        /// 구조로 같게 만든다.</para>
        ///
        /// <para>아직 보고 전이면 false. 호출자는 <b>기존 폴백</b>(macOS 주 디스플레이 / Windows 가상 화면)을
        /// 그대로 쓴다 — 조회 실패를 이유로 멀쩡한 창을 발판에서 지우지 않는다는 기존 계약을 깨지 않는다.</para>
        /// </summary>
        public static bool TryGetOverlayScreenOsRect(out Rect osRect)
        {
            osRect = default;
            OverlayMonitorChoice choice = Resolve();
            if (!choice.HasIndex || choice.Index >= Live.Count) return false;
            osRect = Live[choice.Index].FullOsRect;
            return osRect.width > 0f && osRect.height > 0f;
        }

        /// <summary>
        /// <b>데스크톱 전체</b>(모든 모니터의 외접 사각형)의 OS 사각형.
        ///
        /// <para>쓰임은 <b>딱 하나</b>: <c>ScreenCoordinateConverter</c>의 <b>원점 위생 검사</b>다.
        /// 그 검사는 "우리 창이 명백히 화면 밖인가"를 묻는 것이라 <b>반드시 전체 데스크톱</b>이어야 한다.
        /// 위 <see cref="TryGetOverlayScreenOsRect"/>(한 화면)와 <b>절대 섞으면 안 된다</b> —
        /// 섞으면 오버레이가 보조 화면에 있을 때 우리가 <b>우리 자신의 원점을 거부</b>한다.</para>
        ///
        /// <para>★ 이것도 이번 라운드에 드러난 회귀다: macOS는 이 자리에 <c>CGDisplayBounds(주 디스플레이)</c>를
        /// 넘기고 있었다(그때는 그것밖에 없었다). 오버레이가 가장 왼쪽(주 화면이 아닐 수 있다)으로
        /// 가면 <b>우리 창의 0%만 "데스크톱 안"</b>이 되어 원점 보고가 연속 거부된다.
        /// Windows는 원래 <c>SM_*VIRTUALSCREEN</c>이라 이 문제가 없었다 — <b>macOS가 뒤처진 쪽</b>이다.</para>
        /// </summary>
        public static bool TryGetDesktopUnionOsRect(out Rect osRect)
        {
            osRect = default;
            bool any = false;
            for (int i = 0; i < Live.Count; i++)
            {
                Rect r = Live[i].FullOsRect;
                if (r.width <= 0f || r.height <= 0f) continue;
                if (!any) { osRect = r; any = true; continue; }

                float xMin = Mathf.Min(osRect.xMin, r.xMin);
                float yMin = Mathf.Min(osRect.yMin, r.yMin);
                float xMax = Mathf.Max(osRect.xMax, r.xMax);
                float yMax = Mathf.Max(osRect.yMax, r.yMax);
                osRect = new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
            }
            return any;
        }

        /// <summary>선택 해제 — 기본값(주 모니터)으로 되돌린다.</summary>
        public static void ClearSelection() => Core.AppSettingsModel.SetPreferredOverlayMonitor(null);

        /// <summary>테스트 전용 초기화(정적 상태가 테스트 사이에 새지 않게 —
        /// <c>AppSettingsModel.ResetForTesting</c>과 같은 관례).</summary>
        public static void ResetForTesting()
        {
            Live.Clear();
            Changed = null;
        }
    }
}
