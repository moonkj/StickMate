using System;
using System.Collections.Generic;

namespace StickMate.Platform
{
    /// <summary>
    /// "OS가 창 변화를 <b>통보</b>해 주는 창구". 폴링(<c>EnumWindows</c>를 초당 3.3회)을 대체하는
    /// 이벤트 방식의 플랫폼 중립 인터페이스다.
    ///
    /// ============================================================================
    /// 계약 (구현체는 전부 지켜야 한다)
    /// ============================================================================
    /// <list type="number">
    /// <item><b>읽기 전용 관찰만 한다.</b> 통보를 받는 것 외에 다른 프로세스의 창을 이동/변경/종료하는
    ///   API는 <b>선언조차 하지 않는다</b>(절대 불변 원칙 3). 이 계약은
    ///   <c>Tests/EditMode/UserAssetImmutabilityAuditTests</c>가 소스 정적 스캔으로 잠근다.</item>
    /// <item><b>콜백은 메인 스레드가 아닐 수 있다.</b> 그래서 이 인터페이스에는 "이벤트를 전달하는"
    ///   메서드가 없다 — 구현체는 플래그만 세우고, 메인 루프가
    ///   <see cref="ConsumeChangeSignals"/>로 <b>합쳐서 한 번에</b> 가져간다. Unity API를 콜백에서
    ///   부르는 경로가 구조적으로 생길 수 없게 만드는 것이 이 모양의 목적이다.</item>
    /// <item><b>합치기(coalescing)는 구현체 책임이다.</b> Windows <c>EVENT_OBJECT_LOCATIONCHANGE</c>는
    ///   창을 드래그하는 동안 초당 수백 번 온다. 그것을 그대로 스캔으로 바꾸면 폴링보다 나빠진다.</item>
    /// <item><b>핸들 수명.</b> <see cref="IDisposable.Dispose"/>에서 네이티브 훅을 반드시 해제한다.</item>
    /// </list>
    /// </summary>
    public interface IWindowChangeNotifier : IDisposable
    {
        /// <summary>훅이 실제로 등록돼 살아 있는가. false면 호출자는 <b>옛 주기 폴링</b>으로 폴백한다.</summary>
        bool IsActive { get; }

        /// <summary>로그 한 줄에 그대로 넣을 사람이 읽는 상태 설명(등록 성공/실패 사유 등).</summary>
        string StatusDescription { get; }

        /// <summary>
        /// 지난 호출 이후 쌓인 통보를 <b>소비</b>한다(플래그를 내린다). 한 프레임에 한 번만 부른다.
        /// </summary>
        /// <param name="watchedChanged">감시 목록에 있는 창이 바뀌었다.</param>
        /// <param name="globalChanged">그 밖의 창이 바뀌었다.</param>
        /// <returns>둘 중 하나라도 true면 true.</returns>
        bool ConsumeChangeSignals(out bool watchedChanged, out bool globalChanged);

        /// <summary>
        /// 좁은 감시 대상(창 핸들)을 갈아 끼운다. 목록은 즉시 복사되므로 호출자는 자기 버퍼를
        /// 계속 재사용해도 된다. 빈 목록 = "감시할 창이 없다"(예: 작업표시줄/Dock 위에 서 있음).
        /// </summary>
        void SetWatchedWindows(IReadOnlyList<long> handles);

        /// <summary>콜백이 실제로 불린 총 횟수(진단). 필터에 걸려 버려진 것도 포함한다.</summary>
        long RawCallbackCount { get; }

        /// <summary>필터를 통과해 플래그를 세운 횟수(진단).</summary>
        long AcceptedEventCount { get; }
    }

    /// <summary>
    /// 통보 창구가 없는 플랫폼(macOS/모바일/에디터)에서 쓰는 무해한 구현.
    /// <see cref="IsActive"/>가 항상 false라, 호출자는 자동으로 <b>이 라운드 이전의 주기 폴링</b>으로
    /// 되돌아간다 — 컨벤션의 <c>NullPlatformWindowService</c> 폴백과 같은 취지다.
    /// </summary>
    public sealed class NullWindowChangeNotifier : IWindowChangeNotifier
    {
        public static readonly NullWindowChangeNotifier Instance = new NullWindowChangeNotifier();

        private readonly string _reason;

        public NullWindowChangeNotifier(string reason = "이 플랫폼에는 창 변화 통보 창구가 없습니다(주기 폴링 유지).")
        {
            _reason = reason;
        }

        public bool IsActive => false;
        public string StatusDescription => _reason;
        public long RawCallbackCount => 0;
        public long AcceptedEventCount => 0;

        public bool ConsumeChangeSignals(out bool watchedChanged, out bool globalChanged)
        {
            watchedChanged = false;
            globalChanged = false;
            return false;
        }

        public void SetWatchedWindows(IReadOnlyList<long> handles) { }

        public void Dispose() { }
    }
}
