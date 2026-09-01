using System;
using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// "지금 화면 구성이 앱 기동 시점과 달라졌는가"를 판정하는 **플랫폼 공용 순수 로직**
    /// (2026-08-31 perf-doc 정적 분석 지적 대응).
    ///
    /// ============================================================================
    /// 왜 필요한가 — 오버레이 창이 최초 기동 해상도에 영원히 박제되던 결함
    /// ============================================================================
    /// 양 플랫폼 Enforcer의 <c>TickFullScreenBounds()</c>는 오차 1px 이내로 맞으면
    /// <c>_fullScreenBoundsApplied = true</c>를 걸고 그 뒤로는 아무 일도 하지 않았다. 그 플래그를 다시
    /// false로 되돌리는 경로가 **코드 어디에도 없었다.** 그래서 앱이 켜진 채 사용자가 디스플레이
    /// 해상도를 바꾸거나 모니터를 붙였다 떼면, 오버레이 창은 옛 해상도 크기 그대로 남고
    /// ScreenCoordinateConverter의 y 반전 기준까지 통째로 어긋난 채 재시작 전까지 방치됐다.
    ///
    /// ============================================================================
    /// ★ 디바운스가 이 클래스의 존재 이유다 (감지보다 이쪽이 어렵다)
    /// ============================================================================
    /// "다르면 즉시 재적합"은 고치는 것이 아니라 <b>더 큰 히치를 직접 만드는 것</b>이다. 해상도 모드
    /// 전환 한 번에 OS는 중간 상태를 여러 번 노출한다(모니터 사라짐 -> 임시 해상도 -> 최종 해상도).
    /// 그 중간 상태마다 <c>Screen.SetResolution</c>을 부르면 백버퍼 재할당이 연달아 일어나 사용자가
    /// 체감하는 멈춤이 오히려 길어진다. 그래서 이 클래스는 <b>마지막 변화 이후 값이 그대로 유지된</b>
    /// 상태가 <see cref="DefaultSettleSeconds"/>만큼 이어졌을 때 **딱 한 번만** 신호를 낸다.
    ///
    /// 되돌아온 변화는 신호를 내지 않는다: 안정 판정 시점의 값이 기준값과 같으면(잠깐 흔들렸다가 원래
    /// 구성으로 복귀) 아무 일도 없었던 것으로 처리한다 — 필요 없는 재적합은 곧 필요 없는 히치다.
    ///
    /// ============================================================================
    /// 시그니처에 무엇을 넣고 무엇을 <b>절대</b> 넣지 않는가 (되먹임 방지)
    /// ============================================================================
    /// 넣는 것: OS가 직접 주는 값만 — 모니터 개수, 대상 모니터 사각형, 화면 전체 크기,
    ///          OS가 보고한 UI 배율. 이들은 우리가 창에 무엇을 하든 변하지 않는다.
    /// 넣지 않는 것: 우리 창의 크기/위치, 그리고 그로부터 유도되는 값
    ///          (<c>ScreenCoordinateConverter.AutoDpiScale</c> = 창 폭 / Screen.width).
    ///          재적합이 그 값을 바꾸므로 시그니처에 넣으면 "재적합 -> 시그니처 변화 -> 재적합"의
    ///          자기 되먹임 루프가 되어 앱이 영원히 해상도를 다시 잡는다.
    ///
    /// 순수 로직인 이유: 플랫폼 API(<c>#if UNITY_STANDALONE_*</c>) 밖에 두어야 EditMode 테스트가
    /// 모니터 사각형 변화를 손으로 만들어 넣고 재무장/디바운스를 검증할 수 있다.
    /// 관측은 호출자(각 플랫폼 Enforcer)가 하고, 판단은 전부 여기 한 곳에 있다.
    /// </summary>
    public sealed class DisplayTopologyWatcher
    {
        /// <summary>
        /// 마지막 변화 감지 후 이만큼 값이 그대로면 "안정됐다"고 보고 재적합 신호를 1회 낸다.
        /// 짧으면 전환 중간 상태에서 조기 발화하고, 길면 사용자가 어긋난 창을 그만큼 오래 본다.
        /// 0.75초는 지시 범위(0.5~1.0초)의 가운데 값이며, 재적합 루프 간격(0.5초)보다 길어
        /// 재적합 자체와 위상이 겹치지 않는다.
        /// </summary>
        public const float DefaultSettleSeconds = 0.75f;

        private readonly float _settleSeconds;

        private DisplayTopologySignature _baseline;
        private bool _hasBaseline;

        private DisplayTopologySignature _pending;
        private bool _isPending;
        private float _settleTimer;

        public DisplayTopologyWatcher() : this(DefaultSettleSeconds) { }

        public DisplayTopologyWatcher(float settleSeconds)
        {
            _settleSeconds = Mathf.Max(0f, settleSeconds);
        }

        /// <summary>현재 기준값(마지막으로 "정상"이라고 합의한 화면 구성).</summary>
        public DisplayTopologySignature Baseline => _baseline;

        public bool HasBaseline => _hasBaseline;

        /// <summary>변화를 감지하고 안정되기를 기다리는 중인가(디바운스 창 안).</summary>
        public bool IsSettling => _isPending;

        /// <summary>디바운스 창에서 지금까지 값이 유지된 시간(초) — 진단/테스트용.</summary>
        public float SettledSeconds => _settleTimer;

        /// <summary>지금까지 낸 재적합 신호 횟수 — "한 번의 변화에 한 번만"을 테스트가 확인하는 통로.</summary>
        public int TriggerCount { get; private set; }

        /// <summary>
        /// 기준값을 현재 관측으로 강제 교체하고 대기 중인 디바운스를 버린다.
        /// 재적합이 끝난 직후에 호출한다 — 재적합 과정에서 우리 스스로 만든 변화(창 크기/Unity 해상도)가
        /// 다음 관측에서 "새 변화"로 오인되는 것을 막는 자리다.
        /// </summary>
        public void ResetBaseline(in DisplayTopologySignature current)
        {
            _baseline = current;
            _hasBaseline = current.Valid;
            _isPending = false;
            _settleTimer = 0f;
        }

        /// <summary>
        /// 매 틱 1회 호출. 반환값 true는 "지금 재적합을 시작해라"이며, 한 번의 화면 구성 변화당
        /// 정확히 한 번만 true가 된다.
        /// </summary>
        /// <param name="current">이번 틱의 관측. Valid=false(조회 실패)면 상태를 건드리지 않는다 —
        /// 모니터가 잠깐 조회되지 않는 전환 순간에 잘못된 판단을 내리지 않기 위함이다.</param>
        /// <param name="unscaledDeltaTime">Time.unscaledDeltaTime(테스트는 손으로 넣는다).</param>
        public bool Observe(in DisplayTopologySignature current, float unscaledDeltaTime)
        {
            if (!current.Valid) return false;

            if (!_hasBaseline)
            {
                _baseline = current;
                _hasBaseline = true;
                return false;
            }

            if (!_isPending)
            {
                if (current.Equals(_baseline)) return false;
                _isPending = true;
                _pending = current;
                _settleTimer = 0f;
                return false;
            }

            // 디바운스 창 안에서 값이 또 바뀌면 타이머를 처음부터 다시 센다 — 연속 변화 한 묶음을
            // 하나의 사건으로 접는 지점이다.
            if (!current.Equals(_pending))
            {
                _pending = current;
                _settleTimer = 0f;
                return false;
            }

            _settleTimer += Mathf.Max(0f, unscaledDeltaTime);
            if (_settleTimer < _settleSeconds) return false;

            _isPending = false;
            _settleTimer = 0f;

            // 흔들렸다가 원래 구성으로 되돌아온 경우 — 재적합할 이유가 없다.
            if (_pending.Equals(_baseline)) return false;

            _baseline = _pending;
            TriggerCount++;
            return true;
        }
    }

    /// <summary>
    /// 화면 구성의 지문. 모든 필드가 정수로 양자화돼 있다 — 부동소수 미세 흔들림(0.0001pt)이
    /// 재적합을 유발하면 그것 자체가 상시 히치가 되기 때문이다.
    /// </summary>
    public readonly struct DisplayTopologySignature : IEquatable<DisplayTopologySignature>
    {
        /// <summary>false면 "이번 틱은 관측하지 못했다"(모니터 조회 실패). 판단 근거로 쓰지 않는다.</summary>
        public readonly bool Valid;

        public readonly int MonitorCount;
        public readonly int MonitorX;
        public readonly int MonitorY;
        public readonly int MonitorWidth;
        public readonly int MonitorHeight;

        /// <summary>화면 전체 크기(Windows: Screen.currentResolution, macOS: CGDisplayBounds).</summary>
        public readonly int DesktopWidth;
        public readonly int DesktopHeight;

        /// <summary>OS가 직접 보고한 UI 배율 x1000(미보고면 0). 해상도가 그대로인 DPI 전용 변경을 잡는다.</summary>
        public readonly int UiDensityMilli;

        private DisplayTopologySignature(int monitorCount, int mx, int my, int mw, int mh,
            int desktopW, int desktopH, int uiDensityMilli)
        {
            Valid = true;
            MonitorCount = monitorCount;
            MonitorX = mx;
            MonitorY = my;
            MonitorWidth = mw;
            MonitorHeight = mh;
            DesktopWidth = desktopW;
            DesktopHeight = desktopH;
            UiDensityMilli = uiDensityMilli;
        }

        /// <summary>관측 실패를 뜻하는 값(= default).</summary>
        public static DisplayTopologySignature Invalid => default;

        public static DisplayTopologySignature Create(int monitorCount, Rect monitorRect,
            Vector2 desktopSize, float uiDensityScale)
        {
            if (monitorCount <= 0) return Invalid;
            if (monitorRect.width <= 0f || monitorRect.height <= 0f) return Invalid;

            int density = uiDensityScale > 0f && !float.IsNaN(uiDensityScale) && !float.IsInfinity(uiDensityScale)
                ? Mathf.RoundToInt(uiDensityScale * 1000f)
                : 0;

            return new DisplayTopologySignature(monitorCount,
                Mathf.RoundToInt(monitorRect.x), Mathf.RoundToInt(monitorRect.y),
                Mathf.RoundToInt(monitorRect.width), Mathf.RoundToInt(monitorRect.height),
                Mathf.RoundToInt(Mathf.Max(0f, desktopSize.x)), Mathf.RoundToInt(Mathf.Max(0f, desktopSize.y)),
                density);
        }

        public bool Equals(DisplayTopologySignature other)
        {
            return Valid == other.Valid
                && MonitorCount == other.MonitorCount
                && MonitorX == other.MonitorX && MonitorY == other.MonitorY
                && MonitorWidth == other.MonitorWidth && MonitorHeight == other.MonitorHeight
                && DesktopWidth == other.DesktopWidth && DesktopHeight == other.DesktopHeight
                && UiDensityMilli == other.UiDensityMilli;
        }

        public override bool Equals(object obj) => obj is DisplayTopologySignature other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = Valid ? 17 : 3;
                h = h * 31 + MonitorCount;
                h = h * 31 + MonitorX;
                h = h * 31 + MonitorY;
                h = h * 31 + MonitorWidth;
                h = h * 31 + MonitorHeight;
                h = h * 31 + DesktopWidth;
                h = h * 31 + DesktopHeight;
                h = h * 31 + UiDensityMilli;
                return h;
            }
        }

        /// <summary>진단 로그 전용(매 프레임 경로에서 부르지 말 것 — 문자열을 만든다).</summary>
        public override string ToString()
        {
            if (!Valid) return "topology(관측실패)";
            return $"topology(모니터{MonitorCount}개, 대상=({MonitorX},{MonitorY},{MonitorWidth}x{MonitorHeight}), " +
                $"화면={DesktopWidth}x{DesktopHeight}, UI배율={(UiDensityMilli > 0 ? (UiDensityMilli / 1000f).ToString("F3") : "미보고")})";
        }
    }
}
