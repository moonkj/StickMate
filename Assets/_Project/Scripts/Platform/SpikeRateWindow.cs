namespace StickMate.Platform
{
    /// <summary>
    /// **최근 발생률을 재는 슬라이딩 창.** 값 타입이고 시계를 읽지 않는다(호출자가 dt를 준다).
    ///
    /// ============================================================================
    /// 왜 텀블링이 아니라 슬라이딩인가 (2026-09-02 검증 R2-5)
    /// ============================================================================
    /// 처음 구현은 텀블링(창을 통째로 굴리기)이었다. 허수 방지 가드는 잘 동작했지만
    /// (최소 창 62초 확보, "0.3초에 1회 = 분당 200회" 같은 값 0건) <b>모양이 사건을 못 따라갔다</b>:
    /// <code>
    ///   실제 발생률 216회/분(일정)  ->  로그: 64.9 -> 95.3 ... 단조 상승만
    ///                                   창이 굴린 62초 시점: 53.3 -> 129.4 (2.4배 점프)
    /// </code>
    /// 창이 자라는 동안 분모만 커지니 값이 <b>단조 상승</b>하고, 굴리는 순간 분모가 리셋되어
    /// <b>점프</b>한다. 두 줄을 나란히 읽는 사람은 그 점프를 "갑자기 나빠졌다"로 읽는다 —
    /// 계기가 사건이 아니라 <b>자기 구조</b>를 보고한 셈이다.
    ///
    /// <para>버킷을 하나씩 은퇴시키면 한 번에 바뀌는 몫이 최대 1/<see cref="BucketCount"/>이라
    /// 값이 실제 발생률을 따라가고, 사건이 멎으면 <b>내려간다</b>(텀블링은 창이 끝날 때까지 못 내려갔다).</para>
    /// </summary>
    public struct SpikeRateWindow
    {
        /// <summary>창 전체 길이(초).</summary>
        public const float WindowSeconds = 300f;

        /// <summary>버킷 수. 한 번에 은퇴하는 몫이 1/12이라 값이 계단이 아니라 곡선으로 움직인다.</summary>
        public const int BucketCount = 12;

        /// <summary>버킷 하나의 길이(초).</summary>
        public const float BucketSeconds = WindowSeconds / BucketCount;

        /// <summary>관측 구간이 이보다 짧으면 발생률에 "관측 짧음"을 붙인다.</summary>
        public const float MinSpanSeconds = 60f;

        // 값 타입이라 배열(참조형)을 두지 않는다 — 구조체 복사 시 뒤에서 공유되는 사고를 원천 차단한다.
        private int _b0, _b1, _b2, _b3, _b4, _b5, _b6, _b7, _b8, _b9, _b10, _b11;
        private float _elapsed;
        private int _head;
        private int _filled;

        /// <summary>창이 덮고 있는 시간(초).</summary>
        public float SpanSeconds => _filled * BucketSeconds + _elapsed;

        /// <summary>창 안의 총 건수.</summary>
        public int Count => _b0 + _b1 + _b2 + _b3 + _b4 + _b5 + _b6 + _b7 + _b8 + _b9 + _b10 + _b11;

        /// <summary>분당 발생률. 관측 구간이 0이면 0.</summary>
        public float PerMinute
        {
            get
            {
                float span = SpanSeconds;
                return span > 0.001f ? Count * 60f / span : 0f;
            }
        }

        /// <summary>관측 구간이 아직 짧아 값이 흔들릴 수 있는가(로그에 그대로 적는다).</summary>
        public bool SpanTooShort => SpanSeconds < MinSpanSeconds;

        /// <summary>매 프레임 호출.</summary>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;
            _elapsed += deltaSeconds;

            // 아주 긴 공백(디스플레이 절전 복귀 등)은 창 전체를 무의미하게 만든다 — 통째로 비운다.
            if (_elapsed >= WindowSeconds)
            {
                Reset();
                return;
            }

            while (_elapsed >= BucketSeconds)
            {
                _elapsed -= BucketSeconds;
                _head = (_head + 1) % BucketCount;
                SetBucket(_head, 0);
                if (_filled < BucketCount - 1) _filled++;
            }
        }

        /// <summary>사건 한 건을 현재 버킷에 센다.</summary>
        public void Count1() => SetBucket(_head, GetBucket(_head) + 1);

        public void Reset()
        {
            _b0 = _b1 = _b2 = _b3 = _b4 = _b5 = 0;
            _b6 = _b7 = _b8 = _b9 = _b10 = _b11 = 0;
            _elapsed = 0f; _head = 0; _filled = 0;
        }

        private int GetBucket(int i)
        {
            switch (i)
            {
                case 0: return _b0; case 1: return _b1; case 2: return _b2; case 3: return _b3;
                case 4: return _b4; case 5: return _b5; case 6: return _b6; case 7: return _b7;
                case 8: return _b8; case 9: return _b9; case 10: return _b10; default: return _b11;
            }
        }

        private void SetBucket(int i, int v)
        {
            switch (i)
            {
                case 0: _b0 = v; break; case 1: _b1 = v; break; case 2: _b2 = v; break;
                case 3: _b3 = v; break; case 4: _b4 = v; break; case 5: _b5 = v; break;
                case 6: _b6 = v; break; case 7: _b7 = v; break; case 8: _b8 = v; break;
                case 9: _b9 = v; break; case 10: _b10 = v; break; default: _b11 = v; break;
            }
        }
    }
}
