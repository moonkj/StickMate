using System;

namespace StickMate.Platform
{
    /// <summary>
    /// ★ 2026-09-14 (debugger 확정 결함 D1) — "실제 시간으로 N초마다 한 번"을 <b>호출 빈도와 무관하게</b> 지키는 문.
    ///
    /// <para><b>무엇이 틀렸었나.</b> 두 플랫폼 Enforcer의 OS 모니터 목록 갱신이
    /// <c>timer += Time.unscaledDeltaTime; if (timer &gt;= 1초) …</c>였다. 그런데 그 함수는 매 프레임이 아니라
    /// <b>토폴로지 표본·재적합 시도 게이트 뒤</b>에서만 불린다. 한 번 불릴 때 더해지는 것은 <b>그 한 프레임의
    /// dt</b>(루프 60Hz에서 약 1/60초)뿐이다. 그래서 "1초마다"가 실제로는 — Windows(표본 0.25초) 약 15초,
    /// macOS(표본 0.1초) 약 6초, 재적합 진행 중(표본이 멈추고 0.5초 게이트만 남음) 두 플랫폼 모두 약 30초마다였다.
    /// 모니터를 떼면 OS 목록이 그만큼 낡은 채로 남아 선택 매핑이 실패하고 폴백 로그가 반복됐다.</para>
    ///
    /// <para><b>고친 형태.</b> 누적하지 않는다. 다음 갱신 시각을 기억하고 지금 시각과 비교한다 —
    /// 호출이 드물어도 늦게 한 번 부르면 곧바로 참이 된다. 첫 호출은 즉시 참이다.</para>
    ///
    /// <para>★ <b>왜 class인가(verify-change 변이 V1, 2026-09-14).</b> 처음에는 struct였다. 그런데 필드에
    /// <c>readonly</c> 한 단어만 붙이면 C#이 <b>호출마다 복사본</b>에 <see cref="TryConsume"/>를 불러
    /// 상태가 저장되지 않고, 문이 <b>매 호출 열린다</b>(= 매번 모니터 열거). 컴파일 경고도 테스트 실패도
    /// 없었다. 잠금으로 막는 대신 <b>함정 자체를 없앤다</b> — 참조 형식이면 readonly 필드여도 같은 객체를
    /// 부른다. <c>WallClockIntervalGateTests</c>가 "readonly 필드에 담아도 닫힌다"를 실행으로 잠근다.</para>
    /// </summary>
    public sealed class WallClockIntervalGate
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
