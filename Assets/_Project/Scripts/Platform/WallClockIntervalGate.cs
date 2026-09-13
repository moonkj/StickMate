using System;

namespace StickMate.Platform
{
    /// <summary>
    /// ★ 2026-09-14 (debugger 확정 결함 D1) — "실제 시간으로 N초마다 한 번"을 <b>호출 빈도와 무관하게</b> 지키는 문.
    ///
    /// <para><b>무엇이 틀렸었나.</b> 두 플랫폼 Enforcer의 OS 모니터 목록 갱신이
    /// <c>timer += Time.unscaledDeltaTime; if (timer &gt;= 1초) …</c>였다. 그런데 그 함수는 매 프레임이 아니라
    /// <b>토폴로지 표본(0.25초)·재적합 시도(0.5초) 게이트 뒤</b>에서만 불린다. 한 번 불릴 때 더해지는 것은
    /// <b>그 한 프레임의 dt</b>(약 1/60초)뿐이므로, "1초마다"는 실제로 <b>15~30초마다</b>였다.
    /// 모니터를 떼면 OS 목록이 그만큼 낡은 채로 남아 선택 매핑이 실패하고 폴백 로그가 반복됐다.</para>
    ///
    /// <para><b>고친 형태.</b> 누적하지 않는다. 다음 갱신 시각을 기억하고 지금 시각과 비교한다 —
    /// 호출이 드물어도 늦게 한 번 부르면 곧바로 참이 된다. 첫 호출은 즉시 참이다(예전 초기값
    /// <c>PositiveInfinity</c>와 같은 의미).</para>
    ///
    /// <para>순수 로직이라 EditMode가 "0.25초 간격으로 불러도 1초마다 열린다"를 실행으로 검증한다
    /// (<c>WallClockIntervalGateTests</c>).</para>
    /// </summary>
    public struct WallClockIntervalGate
    {
        private double _nextDueSeconds;
        private bool _armed;

        /// <summary>한 번이라도 열린 적이 있는가.</summary>
        public bool IsArmed => _armed;

        /// <summary>
        /// 지금 열려야 하면 true를 돌려주고 다음 갱신 시각을 <paramref name="nowSeconds"/> + 간격으로 옮긴다.
        /// </summary>
        /// <param name="nowSeconds">단조 증가하는 실시간(<c>Time.unscaledTime</c> 등).</param>
        /// <param name="intervalSeconds">간격(초). 0 이하면 매번 열린다.</param>
        public bool TryConsume(double nowSeconds, double intervalSeconds)
        {
            if (_armed && nowSeconds < _nextDueSeconds) return false;
            _armed = true;
            _nextDueSeconds = nowSeconds + Math.Max(0.0, intervalSeconds);
            return true;
        }
    }
}
