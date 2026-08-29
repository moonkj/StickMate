using System.Collections.Generic;

namespace StickMate.Platform
{
    /// <summary>
    /// 가려짐(오클루전) 필터를 <b>거치기 전</b>의 원본 창 목록을 읽기 전용으로 추가 노출하는 선택적 채널.
    /// ICursorPositionService/IDockMetricsService와 같은 관례로, 구현하지 않는 플랫폼에서는 소비 측이
    /// <c>as IRawWindowRectSource</c> 캐스팅 실패(null)를 그대로 폴백 신호로 쓴다.
    ///
    /// 왜 별도 채널인가 — <see cref="IPlatformWindowService.EnumerateFootholds"/>가 돌려주는 목록은
    /// "상단 테두리가 앞에서 <b>실제로 보이는</b> 창"만 담는다(그게 발판의 정의다: 눈에 보이지 않는 창을
    /// 딛고 서 있으면 허공을 걷는 것처럼 보인다 — 2026-08-28 사용자 신고 대응). 하지만 <b>딛는 것이
    /// 아니라 미는 연출</b>인 창 도둑(docs/UX_FLOW.md 27-1)은 그 조건이 필요 없다. 오히려 작은 창은
    /// 대개 큰 창 뒤에 가려져 있어서, 발판 목록을 후보로 쓰면 폭 판정에 도달하기도 전에 후보가 0개가
    /// 된다(2026-08-29 실측: 계산기를 띄워둬도 Cursor 창 뒤에 있으면 "완전히 가려짐"으로 탈락).
    /// 그래서 발판 열거/가려짐 계산 로직은 <b>한 줄도 건드리지 않고</b>, 같은 열거 패스가 이미 만들어 둔
    /// 원본 목록만 읽기 전용으로 하나 더 내보낸다.
    ///
    /// 절대 불변 원칙 3: 이 채널도 조회 전용이다. 여기서 얻은 핸들/사각형으로 창을 이동·활성화·종료하는
    /// API는 어디서도 호출하지 않는다(Interaction/WindowTheftDirector.cs는 좌표를 <b>복사본 고스트</b>를
    /// 그리는 데만 쓴다).
    /// </summary>
    public interface IRawWindowRectSource
    {
        /// <summary>
        /// 마지막 <see cref="IPlatformWindowService.EnumerateFootholds"/> 패스가 채택한 원본 창 목록
        /// (z-order 앞->뒤). 가려짐 여부와 무관하며, 사각형은 조각으로 잘리지 않은 <b>창 전체</b>다.
        /// 구현체는 매 호출마다 새 컬렉션을 만들지 말고 재사용 버퍼의 읽기 전용 뷰를 돌려줘야 한다
        /// (24시간 상주 앱 GC 압박 방지 컨벤션).
        /// </summary>
        IReadOnlyList<PlatformFoothold> RawWindows { get; }
    }
}
