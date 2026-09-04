using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace StickMate.Platform
{
    /// <summary>
    /// 하이브리드 GPU(내장 + 외장이 함께 있는 노트북)에서 <b>이 앱이 어느 쪽을 원하는가</b>에 대한
    /// <b>플랫폼 중립 판정</b>이다. 실제로 바이트를 쓰는 일은 여기서 하지 않는다 — 여기는 "무엇이
    /// 옳은가"만 정하고, "어떻게 적용하는가"는 플랫폼별 적용기가 맡는다.
    ///
    /// <para><b>왜 <c>Platform/Windows/</c> 가 아니라 여기인가</b>: 이 저장소는 정책을
    /// <c>Platform/MacOS/</c> 안에 두는 바람에 Windows가 물리적으로 호출할 수 없었던 사고를 이미
    /// 겪었다(<c>Platform/FullscreenSuspendPolicy.cs</c> 가 지금 중립 위치에 있는 이유다).
    /// 이 판정은 지금은 Windows만 쓰지만 <b>macOS에도 정확히 대응하는 현상이 있다</b>(아래).
    /// 그날 같은 판정을 다시 쓰려고 만들어 둔 자리다.</para>
    ///
    /// ========================================================================
    /// 무엇이 결정됐는가 (2026-09-03, 사용자 승인)
    /// ========================================================================
    /// Unity의 Windows 플레이어 템플릿은 PE export 두 개를 <b>값 1로 하드코딩</b>해 출하한다.
    /// 이 값은 우리 프로세스도 Unity 런타임도 아니라 <b>그래픽 드라이버</b>가 프로세스 시작 전에
    /// 읽는다. 즉 런타임 C#으로는 어떤 방법으로도 바꿀 수 없다(근거 전량:
    /// docs/verify/WINDOWS_DGPU_REPORT.md 0절).
    ///
    /// <list type="bullet">
    ///   <item><b>1</b> = "High Performance Graphics로 렌더링하라" — 즉 <b>외장 GPU를 깨워라</b>.</item>
    ///   <item><b>0</b> = "이 방법(힌트)을 무시하라" — NVIDIA 문서가 정의한 두 값 중 하나이며,
    ///         <b>"내장 강제"가 아니라 "우리 요청의 철회"</b>다.</item>
    /// </list>
    ///
    /// <para>이 앱은 24시간 상주하는 장식 앱이다. 켜 두는 것이 제품 컨셉이므로, 외장 GPU를 종일
    /// 깨워 두는 것은 배터리·팬 소음에 그대로 꽂힌다. 그래서 <b>요청을 철회한다(0)</b>.
    /// 이것은 성능 튜닝이 아니라 <b>상주 앱의 자원 예절</b> 축의 결정이다.</para>
    ///
    /// <para><b>심볼 이름은 절대 건드리지 않는다.</b> PE export 이름 테이블은 사전순 오름차순이어야
    /// 하고(<c>GetProcAddress</c>가 이진 탐색을 한다), 이름을 고치면 순서가 깨져 같은 테이블의
    /// <c>D3D12SDKVersion</c>/<c>D3D12SDKPath</c> 조회까지 망가진다. 바꾸는 것은 <b>값 DWORD 두 개뿐</b>이다.</para>
    ///
    /// ========================================================================
    /// ★ 확인/미확인의 경계 — 흐리지 마라
    /// ========================================================================
    /// 값이 0이면 NVIDIA 우선순위에서 <b>전역 프로필</b>로 내려가고, Optimus 설계 의도상 프로필이
    /// 없는 앱은 내장으로 간다 — <b>그러므로 내장으로 갈 가능성이 높다</b>. 그러나 이 개발 머신에는
    /// Windows가 없다. <b>실기 미확인이다.</b> 사용자 실기에서 GPU 이름이 내장으로 뜨는 것을 확인하기
    /// 전에는 "고쳤다"고 쓰지 않는다.
    /// </summary>
    public static class HybridGpuPreferencePolicy
    {
        // --------------------------------------------------------------------
        // Windows — PE export 심볼과 값
        // --------------------------------------------------------------------

        /// <summary>NVIDIA Optimus가 읽는 export 심볼 이름.</summary>
        public const string NvidiaExportSymbol = "NvOptimusEnablement";

        /// <summary>AMD PowerXpress(Enduro)가 읽는 export 심볼 이름.</summary>
        public const string AmdExportSymbol = "AmdPowerXpressRequestHighPerformance";

        /// <summary>"고성능(외장) GPU를 써라". Unity 플레이어 템플릿이 하드코딩해 출하하는 값이다.</summary>
        public const uint HighPerformanceValue = 1u;

        /// <summary>"이 힌트를 무시하라". 우리가 원하는 값.</summary>
        public const uint IgnoreHintValue = 0u;

        /// <summary>후처리가 만들어 내야 하는 최종 값.</summary>
        public const uint DesiredValue = IgnoreHintValue;

        /// <summary>후처리가 <b>고치기 전에 보게 될 것으로 기대하는</b> 값. 이것이 아니면 템플릿이
        /// 바뀐 것이므로 아무것도 쓰지 않고 빌드를 세운다.</summary>
        public const uint UnityTemplateValue = HighPerformanceValue;

        private static readonly ReadOnlyCollection<string> WindowsSymbols =
            new ReadOnlyCollection<string>(new[] { NvidiaExportSymbol, AmdExportSymbol });

        /// <summary>Windows exe에서 값을 중립화해야 하는 export 심볼 전량.</summary>
        public static IReadOnlyList<string> WindowsExportSymbols => WindowsSymbols;

        // --------------------------------------------------------------------
        // macOS — 대응하는 지렛대 (아직 배선되지 않았다)
        // --------------------------------------------------------------------

        /// <summary>
        /// macOS에서 같은 성격의 지렛대. 이 키가 <b>없는</b> 앱이 Metal 컨텍스트를 만들면
        /// 듀얼 GPU Intel Mac(예: 2019 MacBook Pro 16")에서 macOS가 자동으로 디스크리트 GPU로
        /// 전환한다 — Windows의 <see cref="NvidiaExportSymbol"/>과 증상(발열·팬·배터리)이 같은 계열이다.
        ///
        /// <para><b>현재 상태: 출하 중인 <c>Info.plist</c>에 이 키가 없다(미제어).</b>
        /// 개발/사용 머신이 Apple Silicon이라 GPU가 하나뿐이고 재현·검증이 불가능해 이번 라운드에서
        /// 켜지 않았다. 이 갭은 <c>Tests/EditMode/PlatformParityAuditTests.cs</c>의
        /// 미해결 항목으로 러너에 계속 뜬다.</para>
        /// </summary>
        public const string MacAutomaticGraphicsSwitchingKey = "NSSupportsAutomaticGraphicsSwitching";

        // --------------------------------------------------------------------
        // 판정
        // --------------------------------------------------------------------

        /// <summary>후처리가 읽은 현재 값에 대해 무엇을 해야 하는가.</summary>
        public enum Verdict
        {
            /// <summary>이미 원하는 값이다. 쓰지 않는다(멱등).</summary>
            AlreadyNeutral,

            /// <summary>템플릿 기본값이다. 중립값으로 바꾼다.</summary>
            NeedsNeutralize,

            /// <summary>기대한 두 값 중 어느 것도 아니다. <b>아무것도 쓰지 않고 빌드를 세운다.</b></summary>
            Unexpected,
        }

        /// <summary>
        /// 현재 값 하나에 대한 판정. <b>여기서 "모르면 넘어간다"는 선택지는 없다</b> —
        /// 이 저장소가 반복해 당한 사고가 「실패한 측정과 성공한 측정이 똑같이 생겼다」이기 때문이다.
        /// 기대 밖의 값은 조용히 통과시키지 말고 <see cref="Verdict.Unexpected"/>로 되돌려
        /// 호출자가 요란하게 실패하게 한다.
        /// </summary>
        /// <param name="currentValue">exe에서 읽은 현재 DWORD.</param>
        /// <param name="reason">사람이 읽을 사유(로그·실패 메시지용). 항상 채워진다.</param>
        public static Verdict Classify(uint currentValue, out string reason)
        {
            if (currentValue == DesiredValue)
            {
                reason = $"이미 {DesiredValue}(힌트 무시)이다 — 쓰지 않는다(멱등).";
                return Verdict.AlreadyNeutral;
            }

            if (currentValue == UnityTemplateValue)
            {
                reason = $"Unity 템플릿 기본값 {UnityTemplateValue}(고성능 GPU 요청)이다 — " +
                         $"{DesiredValue}(힌트 무시)로 바꾼다.";
                return Verdict.NeedsNeutralize;
            }

            reason = $"기대 밖의 값 {currentValue}(0x{currentValue:x8})이다 — " +
                     $"기대한 값은 {UnityTemplateValue}(미패치) 또는 {DesiredValue}(패치됨)뿐이다. " +
                     "플레이어 템플릿이 바뀌었거나 다른 도구가 이 exe를 이미 만졌다. " +
                     "이 상태에서 바이트를 쓰면 무엇을 덮어쓰는지 알 수 없으므로 아무것도 쓰지 않는다.";
            return Verdict.Unexpected;
        }
    }
}
