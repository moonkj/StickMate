namespace StickMate.Platform
{
    /// <summary>
    /// **스파이크를 "누가 낸 긴 프레임인가"로 갈라 세는 장부.** 값 타입이라 힙 할당이 없고,
    /// 네이티브/Unity 상태를 만지지 않으므로 EditMode에서 그대로 검증된다.
    ///
    /// ============================================================================
    /// 왜 세 칸인가 (2026-09-02 검증 R2-2)
    /// ============================================================================
    /// 처음에는 두 칸이었다 — <c>실사용</c>(Active/Calm/Still) / <c>절감</c>(Away/Suspended/DisplayOff).
    /// 정상 구간의 분리는 실측으로 동작했다(85초 숨김 내내 실사용 247 고정, 절감만 +309).
    ///
    /// <para><b>그런데 전환 순간이 실사용 칸을 오염시켰다.</b> 전체화면 게임에 들어가고 나올 때
    /// macOS의 Space 슬라이드가 루프를 100~300ms 멈추는데, <b>그 순간의 등급은 아직
    /// Active/Still</b>이라(전체화면 판정은 폴링 1.5초 + 디바운스 1.0초 뒤에야 확정된다)
    /// 전부 실사용으로 계상됐다. 실측 <b>왕복 1회당 +13.5회</b> — 저녁에 게임을 20번 들락거리면
    /// "실사용 +270"이 쌓이는데 <b>전부 OS 애니메이션</b>이다. 그러면 "먼저 볼 것은 실사용 수"라는
    /// 판독 규칙이 게임 왕복 횟수에 비례해 무너진다.</para>
    ///
    /// ============================================================================
    /// ★ 소급 재분류 — 스톨이 전환보다 **먼저** 오기 때문에 필요하다
    /// ============================================================================
    /// 앞을 보는 유예(전환 이후 N초)만으로는 절반밖에 못 잡는다. 스톨은 등급이 바뀌기 <b>전에</b>
    /// 일어나고 등급 전환은 그 뒤에 따라온다. 그래서 최근 몇 초 동안의 실사용 계상을 1초 슬롯으로
    /// 들고 있다가, 전환이 실제로 오면 <b>그 슬롯들을 전환 칸으로 옮긴다.</b>
    ///
    /// <para>슬롯이 넘치면(짧은 시간에 아주 많은 스파이크) 일부는 실사용에 남는다 —
    /// 그 방향의 오차는 보정이 부족한 쪽이라 안전하다.</para>
    ///
    /// ============================================================================
    /// ★★ 2026-09-02 신빌드 실측이 반증한 것 — "반대 방향 오차는 없다"는 틀렸다
    /// ============================================================================
    /// 이 문서에는 원래 <i>"전환 칸을 부풀려 진짜 히치를 감추는 반대 방향의 오차는 구조적으로
    /// 생기지 않는다"</i>고 적혀 있었다. <b>실기에서 정면으로 반증됐다.</b>
    /// <code>
    ///   SIGSTOP으로 유도한 192ms 히치(등급=Active) -> 전환 칸
    ///   SIGSTOP으로 유도한 434ms 히치(등급=Calm)   -> 전환 칸
    ///   6회 왕복 188초 구간: 실사용 +0            &lt;- 성공이 아니라 **실명**이었다
    /// </code>
    /// 원인: 유예를 <b>모든 등급 전환</b>에 걸었는데, Active↔Calm↔Still 미세 전환은 캐릭터가
    /// 서고 걷기만 해도 <b>수 초마다</b> 일어난다. 그래서 3초 유예가 타임라인의 상당 부분을 덮어
    /// 진짜 히치까지 삼켰다.
    ///
    /// <para><b>그래서 유예는 <see cref="IsThrottledTier"/> 경계를 넘는 전환에만 건다.</b>
    /// 100~300ms를 실제로 먹는 것은 Space 슬라이드(가려짐 진입/해제)이고 그것만이 이 경계를 넘는다.
    /// Calm↔Still 같은 미세 전환은 아무것도 멈추게 하지 않으므로 유예 사유가 될 수 없다.
    /// 판정은 <see cref="CrossesThrottleBoundary"/>가 한다 — 두 계기가 같은 규칙을 쓰도록.</para>
    /// </summary>
    public struct SpikeTierLedger
    {
        /// <summary>전환 전후 이 시간 안의 긴 프레임은 <b>전환 비용</b>으로 본다.
        /// 근거: Space 슬라이드 100~300ms + 전체화면 폴링 1.5초 + 판정 디바운스 1.0초.</summary>
        public const float TransitionGraceSeconds = 3f;

        /// <summary>소급 슬롯 수(1초 슬롯). <see cref="TransitionGraceSeconds"/>를 덮고 하나 더.</summary>
        private const int SlotCount = 4;

        private const float SlotSeconds = 1f;

        /// <summary>Active/Calm/Still에서 난 긴 프레임 — <b>사용자가 렉으로 느끼는 바로 그것.</b></summary>
        public int Actionable { get; private set; }

        /// <summary>등급 전환 전후의 긴 프레임 — Space 슬라이드 등 <b>OS 애니메이션 비용</b>.</summary>
        public int Transitional { get; private set; }

        /// <summary>Away/Suspended/DisplayOff에서 난 긴 프레임 — <b>설계된 절감</b>이지 히치가 아니다.</summary>
        public int Throttled { get; private set; }

        private float _sinceTransition;
        private float _slotElapsed;
        private int _slotHead;
        private int _s0, _s1, _s2, _s3;   // 배열 대신 필드 4개 — 값 타입이라 참조형 필드를 두지 않는다.

        /// <summary>이 등급의 긴 프레임은 설계된 절감이다.</summary>
        public static bool IsThrottledTier(FramePacingTier tier) => tier >= FramePacingTier.Away;

        /// <summary>
        /// 이 전환이 <b>유예를 걸 만한 전환</b>인가 = 절감 경계를 넘는가.
        ///
        /// <para>Active↔Calm↔Still은 아무것도 멈추게 하지 않는다(실측: 캐릭터가 서고 걷기만 해도
        /// 수 초마다 일어난다). Space 슬라이드처럼 100~300ms를 실제로 먹는 전환만이 이 경계를 넘는다.
        /// 이 구분이 없으면 유예가 타임라인 대부분을 덮어 <b>진짜 히치까지 전환 칸으로 삼킨다</b>.</para>
        /// </summary>
        public static bool CrossesThrottleBoundary(FramePacingTier before, FramePacingTier after)
            => IsThrottledTier(before) != IsThrottledTier(after);

        /// <summary>스파이크 한 건이 어느 칸으로 갈지. 로그 억제 정책도 이 값을 쓴다.</summary>
        public enum SpikeClass
        {
            /// <summary>사용자가 렉으로 느끼는 바로 그것 — <b>유일하게 긴급한 종류</b>.</summary>
            Actionable = 0,
            Transitional = 1,
            Throttled = 2,
        }

        /// <summary>지금 스파이크가 나면 어느 칸인가(상태 의존 — 전환 유예를 반영한다).</summary>
        public SpikeClass Classify(FramePacingTier tier)
        {
            if (IsThrottledTier(tier)) return SpikeClass.Throttled;
            return _sinceTransition <= TransitionGraceSeconds ? SpikeClass.Transitional : SpikeClass.Actionable;
        }

        /// <summary>
        /// 매 프레임 호출. 벽시계를 읽지 않고 호출자가 이미 갖고 있는 dt만 받는다
        /// (24시간 상주 앱 — 프레임당 float 덧셈 몇 개가 전부다).
        /// </summary>
        /// <param name="deltaSeconds">이번 프레임의 실제 경과(초).</param>
        /// <param name="tierTransitioned">이번 프레임에 <b>절감 경계를 넘는</b> 전환이 있었는가
        /// (<see cref="CrossesThrottleBoundary"/>). 미세 전환을 넣으면 유예가 진짜 히치를 삼킨다.</param>
        public void Tick(float deltaSeconds, bool tierTransitioned)
        {
            if (deltaSeconds > 0f)
            {
                _sinceTransition += deltaSeconds;
                _slotElapsed += deltaSeconds;

                // dt가 아주 클 수 있다(디스플레이 절전 복귀 등). 슬롯을 한 바퀴 이상 돌릴 필요는
                // 없으므로 그 경우 전부 비운다 — 오래된 계상은 어차피 소급 대상이 아니다.
                if (_slotElapsed >= SlotSeconds * SlotCount)
                {
                    _slotElapsed = 0f;
                    _s0 = _s1 = _s2 = _s3 = 0;
                }
                else
                {
                    while (_slotElapsed >= SlotSeconds)
                    {
                        _slotElapsed -= SlotSeconds;
                        _slotHead = (_slotHead + 1) & 3;
                        SetSlot(_slotHead, 0);
                    }
                }
            }

            if (!tierTransitioned) return;

            // ★ 소급 재분류 — 전환 직전 몇 초의 실사용 계상은 사실 전환 비용이었다.
            int retro = _s0 + _s1 + _s2 + _s3;
            if (retro > 0)
            {
                Actionable -= retro;
                Transitional += retro;
                _s0 = _s1 = _s2 = _s3 = 0;
            }
            _sinceTransition = 0f;
        }

        /// <summary>스파이크 한 건을 등급에 따라 센다.</summary>
        public void Count(FramePacingTier tier)
        {
            switch (Classify(tier))
            {
                case SpikeClass.Throttled: Throttled++; return;
                case SpikeClass.Transitional: Transitional++; return;   // 앞을 보는 절반.
            }

            Actionable++;
            SetSlot(_slotHead, GetSlot(_slotHead) + 1);                 // 뒤를 보는 절반(소급 대기).
        }

        public void Reset()
        {
            Actionable = 0; Transitional = 0; Throttled = 0;
            _sinceTransition = 0f; _slotElapsed = 0f; _slotHead = 0;
            _s0 = _s1 = _s2 = _s3 = 0;
        }

        /// <summary>총합. 세 칸을 합치면 예전의 단일 누적값과 같다.</summary>
        public int Total => Actionable + Transitional + Throttled;

        /// <summary>로그 한 조각. <b>실사용을 맨 앞에 둔다</b> — 먼저 봐야 할 숫자이기 때문이다.</summary>
        public override string ToString()
            => $"실사용 {Actionable}회 + 전환 {Transitional}회 + 절감 {Throttled}회";

        private int GetSlot(int i) => i == 0 ? _s0 : i == 1 ? _s1 : i == 2 ? _s2 : _s3;

        private void SetSlot(int i, int v)
        {
            switch (i)
            {
                case 0: _s0 = v; break;
                case 1: _s1 = v; break;
                case 2: _s2 = v; break;
                default: _s3 = v; break;
            }
        }
    }
}
