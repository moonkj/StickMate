namespace StickMate.Platform
{
    /// <summary>
    /// 창 열거 구현체가 <b>자기 열거 규모</b>를 보고하는 선택적 창구(2026-09-01 스파이크 라운드).
    ///
    /// <para><b>왜 필요한가.</b> <see cref="FootholdPoller"/>는 <c>EnumerateFootholds()</c>가 걸린
    /// <b>시간</b>은 스톱워치로 직접 잴 수 있지만, 그 시간이 "창이 많아서"인지 "창 하나가 느려서"인지는
    /// 구별할 수 없다. 리더가 지정한 세 측정값 중 <b>"전체 열거 개수"</b>가 정확히 그 구별을 준다 —
    /// z-order 라운드 실측에서 이 사용자 환경의 창 수는 16 -> 54 -> 57 -> 60 -> <b>818</b>까지 갔고,
    /// 818 x 초당 3.3회면 초당 2,700회의 관리↔네이티브 전환이다.</para>
    ///
    /// <para><b>선택적</b>이다: 구현하지 않는 플랫폼(mac/모바일/Null)에서는 폴러가 -1을 기록하고
    /// 로그에 그대로 -1이 찍힌다. "모르는 값"을 0으로 위장하지 않는다 — 0개 열거와 미지원은 완전히
    /// 다른 사실이고, 원격 진단에서 그 둘을 섞으면 잘못된 결론이 나온다.</para>
    ///
    /// <para>같은 파일에 두는 <see cref="IRawWindowRectSource"/>와 역할이 다르다: 그쪽은 <b>연출용
    /// 데이터</b>(창 도둑이 밀 창 목록)이고, 이쪽은 <b>계측용 메타데이터</b>다. 섞지 않는다.</para>
    /// </summary>
    public interface IWindowEnumerationCostSource
    {
        /// <summary>
        /// 마지막 열거에서 OS가 콜백한 최상위 창의 <b>총 개수</b>(필터 이전). 아직 한 번도 열거하지
        /// 않았으면 0.
        /// </summary>
        int LastEnumeratedWindowCount { get; }

        /// <summary>
        /// 마지막 열거에서 <b>비싼 크로스 프로세스 조회</b>(Windows에서는 <c>DwmGetWindowAttribute</c>)가
        /// 몇 번 일어났는가. 값싼 필터를 통과한 창만 이 비용을 낸다.
        /// 지원하지 않으면 -1(모르는 값을 0으로 위장하지 않는다 — 위 문단 참고).
        /// </summary>
        int LastDwmProbeCount { get; }
    }
}
