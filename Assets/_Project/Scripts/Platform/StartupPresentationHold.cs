using System;

namespace StickMate.Platform
{
    /// <summary>기동 표시 보류의 단계.</summary>
    public enum StartupPresentationHoldPhase
    {
        /// <summary>아직 시작하지 않았다(또는 끄기 스위치로 영원히 시작하지 않는다).</summary>
        Inactive = 0,
        /// <summary>어두운 보류색이 걸려 있다 — 부착·투명이 성립하기를 기다리는 구간.</summary>
        Holding,
        /// <summary>투명 교정이 실제로 걸려 배경이 검정(알파 보존)으로 넘어갔다. 보류는 할 일이 없다.</summary>
        HandedOver,
        /// <summary>상한 도달 또는 부착 실패로 <b>근백색을 되돌렸다</b>. 되돌린 뒤에는 다시 걸지 않는다.</summary>
        Restored,
    }

    /// <summary>보류가 왜 끝났나(로그에 그대로 적는다).</summary>
    public enum StartupPresentationHoldRelease
    {
        None = 0,
        /// <summary>정상 경로 — 투명 교정이 걸려 넘겼다.</summary>
        HandedOverToTransparency,
        /// <summary>벽시계 상한 도달 — 부착을 더 기다리지 않고 근백색으로 되돌렸다.</summary>
        TimeoutRestored,
        /// <summary>부착 실패 보고와 <b>같은 순간</b>에 되돌렸다(캡션 복귀와 한 사건).</summary>
        AttachFailureRestored,
    }

    /// <summary>
    /// ★ 2026-09-28 — <b>기동 표시 보류</b>의 상수·판정. 플랫폼 중립, 순수 함수.
    /// UnityEngine 의존도 OS 호출도 한 줄 없다(<see cref="OverlayBoundsFitPolicy"/>·
    /// <see cref="DisplayChangeHoldPolicy"/>와 같은 설계 — EditMode가 규칙 자체를 실행해 검증한다).
    ///
    /// ============================================================================
    /// 무슨 신고를 고치는가 — 「시작할때 전체 흰화면이 계속 켜져있다가 꺼짐」
    /// ============================================================================
    /// 사용자 실기 신고(2026-09-28, Windows). 흰 배경의 정체는 <b>씬 카메라의 클리어 색 근백색
    /// 0.94</b>이고, 그것이 보이는 이유는 <b>OS 레이어드(투명)가 걸리기 전에는 알파가 무시</b>되기
    /// 때문이다(2026-09-07 실기 확정, <c>Tasklist.md</c> 「Windows 실행시 흰화면 깜박임 — 원인 확정」).
    /// 창은 시작부터 화면 전체 크기라 그 근백색이 <b>화면 전체</b>를 덮는다.
    ///
    /// <para><b>이 장치가 하는 일</b>: 부착·투명이 성립하기 <b>전</b> 프레임의 클리어 색을
    /// <b>스플래시 배경과 같은 어두운 값</b>으로 유지한다. 새 화면을 만들지 않는다 — 사용자에게는
    /// 스플래시가 잠시 더 이어지는 것처럼 보이고, 그 뒤 창이 투명해진다.</para>
    ///
    /// ============================================================================
    /// ★ 왜 「화면 변경 유예」(<see cref="DisplayChangeRenderHold"/>)를 재사용하지 않았는가
    /// ============================================================================
    /// 그 장치가 억제하는 것은 <b>제출률</b>이다(<see cref="DisplayChangeHoldPolicy.SuppressedRenderFrameInterval"/>
    /// = 30 → 60Hz 루프에서 초당 약 2장). <b>초당 2장이어도 근백색은 화면에 그대로 있다</b> — 그림이 덜
    /// 갱신될 뿐이다. 기동 문제는 「무엇이 보이는가」이므로 그 기전으로는 원리상 해결되지 않는다.
    ///
    /// <para>그래서 <b>기전은 새로 쓰고 형태는 그대로 빌렸다</b>: 벽시계 상한 + 해제 사유 열거,
    /// 끄기 스위치 관례(비었거나 <c>0</c>이면 기본 동작), 부작용을 주입 훅으로 빼서 EditMode가 가짜
    /// 싱크로 전이를 실행하는 구조.</para>
    ///
    /// ============================================================================
    /// ★ 상한이 <b>반드시</b> 근백색을 되돌리는 이유 — 검정-on-검정
    /// ============================================================================
    /// 이 보류는 「투명이 곧 온다」는 전제에 서 있다. 전제가 깨지면(부착 영구 실패 · 투명 교정 미적용)
    /// 어두운 배경이 그대로 남는데, <b>잉크색을 검정으로 쓰는 사용자</b>에게 그것은 「아무것도 안 보이는
    /// 창」이다. 그 실패는 2026-08 라운드가 <b>명시적으로 거부</b>한 것이고(근백색 폴백이 존재하는
    /// 이유가 정확히 그것이다), 그래서 상한에 닿으면 <b>보류를 걷는다</b>.
    ///
    /// <para><b>되돌리는 색은 지어내지 않는다</b> — <see cref="StartupPresentationHold.Begin"/>에 넘어온
    /// 폴백 RGB(<c>StickConfig.backgroundFallbackColor</c>)를 <b>그대로</b> 되돌린다.</para>
    ///
    /// ============================================================================
    /// 정직한 한계
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><b>흰 구간의 길이를 줄이지 않는다.</b> 그 구간이 무슨 색인지만 바꾼다. 길이는 부착까지의
    ///     시간이 지배하고, 그것은 실기 로그(<c>창 부착 감지 … 경과 X.XX초</c>)로만 갈린다.</item>
    ///   <item><b>스플래시 자체는 건드리지 않는다</b>(라이선스 축 — 끌 수 있는지 미확인).</item>
    ///   <item><b>보류색은 런타임에 유도할 수 없다.</b> 스플래시 배경색은 에디터 전용 설정이라 런타임
    ///     API가 없어 아래 상수로 <b>복사</b>해 둔다. 두 벌이 갈라지지 않게
    ///     <c>Tests/EditMode/StartupPresentationHoldTests</c>가 <c>ProjectSettings.asset</c>의 실제 값과
    ///     이 상수를 대조한다(관계 단언).</item>
    /// </list>
    /// </summary>
    public static class StartupPresentationHoldPolicy
    {
        /// <summary>
        /// 끄기 스위치. <c>1</c>(또는 <c>0</c>이 아닌 아무 값)이면 보류가 <b>한 번도 시작되지 않는다</b>
        /// (= 이전 동작: 부착 전 근백색 노출). 기존 <c>STICKMATE_DISABLE_DISPLAY_CHANGE_HOLD</c> ·
        /// <c>STICKMATE_KEEP_LAYERED</c>와 같은 관례다.
        /// </summary>
        public const string DisableEnvironmentVariable = "STICKMATE_DISABLE_STARTUP_HOLD";

        /// <summary>보류색 R — <b>스플래시 배경색과 같은 값</b>
        /// (<c>ProjectSettings.asset</c>의 <c>m_SplashScreenBackgroundColor</c> 실측 2026-09-28).</summary>
        public const float HoldRed = 0.12156863f;

        /// <summary>보류색 G — 위와 같은 출처.</summary>
        public const float HoldGreen = 0.12156863f;

        /// <summary>보류색 B — 위와 같은 출처.</summary>
        public const float HoldBlue = 0.1254902f;

        /// <summary>환경 변수 값 → 꺼짐 여부(비었거나 <c>0</c>이면 켜짐 = 기본 동작).</summary>
        public static bool IsDisabledByEnvironmentValue(string raw) => !string.IsNullOrEmpty(raw) && raw != "0";

        /// <summary>실제 환경에서 끄기 스위치를 읽는다. 읽기 실패는 "켜짐"(기본)으로 본다.</summary>
        public static bool ReadDisabledFromEnvironment()
        {
            try { return IsDisabledByEnvironmentValue(Environment.GetEnvironmentVariable(DisableEnvironmentVariable)); }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// 지금 <b>상한 도달로 근백색을 되돌려야</b> 하는가. 보류 중일 때만, 그리고 예산이 실제로 있을 때만 참이다
        /// (예산이 0 이하면 상한 개념이 없다 — 그 경우 보류를 시작하지 않는 쪽이 옳으므로 여기서도 거짓).
        /// </summary>
        public static bool ShouldRestoreOnTimeout(StartupPresentationHoldPhase phase,
            double startedAt, double now, double budgetSeconds)
        {
            if (phase != StartupPresentationHoldPhase.Holding) return false;
            if (!(budgetSeconds > 0.0)) return false;   // NaN도 여기서 걸린다.
            return now - startedAt >= budgetSeconds;
        }
    }

    /// <summary>
    /// 기동 표시 보류 상태기계. <b>메인 스레드 전용</b>, 시계는 호출자가 넘긴다(테스트가 시간을 손으로 흘린다).
    /// 색을 실제로 쓰는 것은 주입된 훅 하나뿐이라, EditMode가 가짜 싱크로 <b>전이와 색을 그대로 실행</b>해
    /// 검증한다(<see cref="DisplayChangeRenderHold"/>와 같은 구조).
    ///
    /// <para><b>보증</b>:
    /// <list type="number">
    ///   <item><see cref="Begin"/>은 <b>한 번만</b> 먹는다. 끄기 스위치가 켜져 있으면 한 번도 먹지 않는다.</item>
    ///   <item>보류 중 상한에 닿으면 <see cref="Tick"/>이 <b>반드시</b> 폴백 색을 되돌린다.</item>
    ///   <item><see cref="NoteTransparentCorrectionApplied"/>는 <b>색을 쓰지 않는다</b> — 그 시점의 배경은
    ///     이미 투명 교정이 검정(알파 보존)으로 바꿔 놓았다. 여기서 또 쓰면 그 교정을 덮는다.</item>
    ///   <item>한 번 <see cref="StartupPresentationHoldPhase.Restored"/>/<see cref="StartupPresentationHoldPhase.HandedOver"/>가
    ///     되면 어떤 호출도 색을 다시 쓰지 않는다(멱등).</item>
    /// </list></para>
    /// </summary>
    public sealed class StartupPresentationHold
    {
        /// <summary>카메라 배경 RGB를 적용한다. <b>알파는 호출자가 보존</b>한다(알파는 투명 합성의 입력이다).</summary>
        public delegate void ApplyBackgroundRgb(float r, float g, float b);

        private readonly bool _disabled;
        private readonly double _budgetSeconds;
        private readonly ApplyBackgroundRgb _apply;

        private StartupPresentationHoldPhase _phase = StartupPresentationHoldPhase.Inactive;
        private StartupPresentationHoldRelease _release = StartupPresentationHoldRelease.None;
        private double _startedAt;
        private float _restoreR, _restoreG, _restoreB;

        /// <param name="disabled">끄기 스위치 — 참이면 영원히 시작하지 않는다.</param>
        /// <param name="budgetSeconds">상한(초). 부착 제한 시간과 <b>같은 예산</b>을 받는다
        /// (<see cref="OverlayStateReapplyPolicy.AttachTimeoutSeconds"/>).</param>
        /// <param name="apply">색 적용 훅. null이면 상태 전이만 하고 아무것도 그리지 않는다.</param>
        public StartupPresentationHold(bool disabled, double budgetSeconds, ApplyBackgroundRgb apply)
        {
            _disabled = disabled;
            _budgetSeconds = budgetSeconds;
            _apply = apply;
        }

        public bool IsDisabled => _disabled;
        public StartupPresentationHoldPhase Phase => _phase;
        public StartupPresentationHoldRelease LastRelease => _release;
        public bool IsHolding => _phase == StartupPresentationHoldPhase.Holding;
        public double StartedAt => _startedAt;
        public double BudgetSeconds => _budgetSeconds;

        /// <summary>상한에서 되돌릴 색(진단·테스트용). <see cref="Begin"/> 전에는 0이다.</summary>
        public float RestoreRed => _restoreR;
        public float RestoreGreen => _restoreG;
        public float RestoreBlue => _restoreB;

        /// <summary>
        /// 부착 전 첫 기회에 부른다. 폴백 RGB(<c>StickConfig.backgroundFallbackColor</c>)를 받아
        /// <b>그대로 기억</b>하고, 보류색을 적용한다.
        /// </summary>
        /// <returns>이번 호출이 보류를 시작했으면 참(로그는 호출자가 남긴다).</returns>
        public bool Begin(double now, float fallbackR, float fallbackG, float fallbackB)
        {
            if (_disabled) return false;
            if (_phase != StartupPresentationHoldPhase.Inactive) return false;
            if (!(_budgetSeconds > 0.0)) return false;   // 상한이 없으면 되돌릴 보장이 없다 → 시작하지 않는다.

            _restoreR = fallbackR;
            _restoreG = fallbackG;
            _restoreB = fallbackB;
            _startedAt = now;
            _phase = StartupPresentationHoldPhase.Holding;

            Invoke(StartupPresentationHoldPolicy.HoldRed,
                StartupPresentationHoldPolicy.HoldGreen,
                StartupPresentationHoldPolicy.HoldBlue);
            return true;
        }

        /// <summary>매 프레임. 상한에 닿으면 폴백 색을 되돌리고 참을 돌려준다.</summary>
        public bool Tick(double now)
        {
            if (!StartupPresentationHoldPolicy.ShouldRestoreOnTimeout(_phase, _startedAt, now, _budgetSeconds))
            {
                return false;
            }
            return Restore(StartupPresentationHoldRelease.TimeoutRestored);
        }

        /// <summary>
        /// 투명 교정이 <b>실제로 걸렸을 때</b> 부른다. 색을 쓰지 않는다 — 배경은 이미 검정(알파 보존)이다.
        /// </summary>
        public bool NoteTransparentCorrectionApplied()
        {
            if (_phase != StartupPresentationHoldPhase.Holding) return false;
            _phase = StartupPresentationHoldPhase.HandedOver;
            _release = StartupPresentationHoldRelease.HandedOverToTransparency;
            return true;
        }

        /// <summary>부착 실패 보고와 같은 순간의 강제 복원(멱등). 보류 중이 아니면 아무 일도 하지 않는다.</summary>
        public bool RestoreNow(StartupPresentationHoldRelease reason) => Restore(reason);

        private bool Restore(StartupPresentationHoldRelease reason)
        {
            if (_phase != StartupPresentationHoldPhase.Holding) return false;
            _phase = StartupPresentationHoldPhase.Restored;
            _release = reason;
            Invoke(_restoreR, _restoreG, _restoreB);
            return true;
        }

        private void Invoke(float r, float g, float b)
        {
            if (_apply == null) return;
            // 색 적용 실패(카메라가 사라진 순간 등)가 상태기계를 망가뜨리지 않게 한다 —
            // DisplayChangeHoldDriver가 목록 갱신 실패를 삼키는 것과 같은 이유다.
            try { _apply(r, g, b); }
            catch (Exception) { }
        }
    }
}
