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
    /// <b>2026-09-07부터 실제로 두 플랫폼이 함께 읽는다</b> — Windows는 PE export 값을,
    /// macOS는 <c>Info.plist</c> 키를 이 파일의 판정으로 정한다. "그날"이 왔다.</para>
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
    ///
    /// <para><b>macOS 쪽도 같은 경계에 있다</b>(2026-09-07): 이 개발 머신은 Apple Silicon이라
    /// GPU가 하나뿐이고 <b>내장/외장이라는 개념 자체가 없다</b>. 그러므로 이 머신에서 증명 가능한
    /// 전부는 <b>"빌드된 <c>Info.plist</c>에 키가 올바르게 들어갔고 서명이 유효하다"</b>까지다.
    /// 그 키가 실제로 듀얼 GPU Mac에서 내장 선택으로 귀결되는지는 <b>사용자 실기에서만</b> 갈린다.</para>
    ///
    /// <para><b>★ 그리고 이 판정은 "GPU 사용률"과 다른 축이다 — 뭉치지 마라.</b> 같은 신고에
    /// "GPU가 90%까지도 올라감"이 함께 왔다(2026-09-07). 그것은 <b>어느 GPU를 쓰는가</b>가 아니라
    /// <b>얼마나 그리는가</b>의 문제이고, 이 파일은 후자에 대해 아무 말도 하지 않는다. 오히려
    /// 부하가 그대로면 이 판정은 <b>그 부하를 내장 GPU로 옮길 뿐</b>이라 발열·배터리가 나빠질 수도
    /// 있다. 렌더 부하는 별도 라운드의 일이다.</para>
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
        // macOS — 대응하는 지렛대 (2026-09-07 배선 완료)
        // --------------------------------------------------------------------

        /// <summary>
        /// macOS에서 같은 성격의 지렛대. 이 키가 <b>없는</b> 앱이 Metal 컨텍스트를 만들면
        /// 듀얼 GPU Intel Mac(예: 2019 MacBook Pro 16")에서 macOS가 자동으로 디스크리트 GPU로
        /// 전환한다 — Windows의 <see cref="NvidiaExportSymbol"/>과 증상(발열·팬·배터리)이 같은 계열이다.
        ///
        /// <para><b>★ 2026-09-07 갱신 — 배선됐다.</b> 이전 판 주석은 "출하 중인 <c>Info.plist</c>에
        /// 이 키가 없다(미제어)"였고 그 사유는 "개발 머신이 Apple Silicon이라 재현·검증 불가"였다.
        /// <b>사용자가 듀얼 GPU Mac 실기에서 직접 신고했다</b>("내장 그래픽을 강제사용하도록
        /// 자동설정이 되어야 하는데 외장 그래픽을 사용함"). 기다리던 실기 근거가 생겼으므로 이번
        /// 라운드에 적용기를 붙였다: <c>Editor/MacHybridGpuInfoPlistPostprocessor.cs</c>가 빌드된
        /// <c>.app/Contents/Info.plist</c>에 이 키를 <c>true</c>로 보장한다(멱등).</para>
        ///
        /// <para><b>실측으로 확인된 전제 둘</b>(추측이 아니다):
        /// <list type="number">
        ///   <item>Unity 6000.0.82f1에는 이 키를 다루는 <c>PlayerSettings</c> API가 <b>없다</b>.
        ///     에디터 설치 전체를 문자열 탐색해 0건이었다(양성 대조 <c>NSPrincipalClass</c>는 4건).
        ///     플레이어 템플릿 <c>Info.plist</c>에도 이 키가 없다 ⇒ 빌드 후처리 말고 길이 없다.</item>
        ///   <item>빌드된 <c>.app</c>은 <b>애드혹 서명</b>돼 있고 <c>Info.plist</c>가 서명에 봉인된다
        ///     (<c>codesign -dv</c>가 <c>Info.plist entries=17</c>을 보고한다). 즉 후처리로 plist를
        ///     고치면 <b>서명이 깨진다</b> — 실측으로 확인했다(<c>invalid Info.plist (plist or
        ///     signature have been modified)</c>). 그래서 적용기는 고친 뒤 <b>반드시 재서명</b>한다.</item>
        /// </list></para>
        ///
        /// <para><b>★ 이 키의 뜻을 과장하지 마라.</b> Windows의 값 0이 "내장 강제"가 아니라 "요청 철회"인
        /// 것과 똑같이, 이 키도 <b>"내장 강제"가 아니라 "우리는 GPU 전환을 감당할 수 있다"는 선언</b>이다.
        /// macOS는 그 선언을 보고 <b>내장에서 시작</b>하고, 정말 필요할 때만 디스크리트로 올린다.
        /// 그러므로 <b>렌더 부하 자체가 높으면 이 키는 그 부하를 내장 GPU로 옮길 뿐이다.</b></para>
        /// </summary>
        public const string MacAutomaticGraphicsSwitchingKey = "NSSupportsAutomaticGraphicsSwitching";

        /// <summary>이 앱이 원하는 값. 24시간 상주 장식 앱이므로 <b>내장에서 시작</b>하기를 원한다.</summary>
        public const bool MacDesiredAutomaticGraphicsSwitching = true;

        /// <summary>plist에서 <see cref="MacDesiredAutomaticGraphicsSwitching"/>가 갖는 요소 이름.
        /// <c>&lt;true/&gt;</c>는 값이 아니라 <b>요소 이름 자체</b>가 값인 형식이다.</summary>
        public const string MacTrueElementName = "true";

        /// <summary>반대 값의 요소 이름. "누군가 일부러 꺼 두었다"를 알아보기 위해 필요하다.</summary>
        public const string MacFalseElementName = "false";

        /// <summary>후처리가 plist에서 읽은 현재 상태에 대해 무엇을 해야 하는가.
        /// <see cref="Verdict"/>(Windows)와 같은 세 갈래다 — <b>"모르면 넘어간다"는 여기에도 없다.</b></summary>
        public enum PlistVerdict
        {
            /// <summary>이미 원하는 값으로 선언돼 있다. 한 글자도 쓰지 않는다(멱등).</summary>
            AlreadyDeclared,

            /// <summary>키가 아예 없다. 루트 dict 끝에 삽입한다.</summary>
            NeedsDeclaration,

            /// <summary>키는 있는데 값이 기대 밖이다(<c>false</c>이거나 boolean이 아니다).
            /// <b>덮어쓰지 않고 빌드를 세운다</b> — 사람이 일부러 꺼 둔 것일 수 있고,
            /// 그 결정을 빌드 스크립트가 조용히 되돌리면 안 된다.</summary>
            Unexpected,
        }

        /// <summary>
        /// macOS <c>Info.plist</c>의 현재 상태 하나에 대한 판정.
        /// <b>사실 조회는 호출자(플랫폼 전용 후처리)가 하고, 판정은 여기서만 한다.</b>
        /// </summary>
        /// <param name="keyPresent">루트 dict에 <see cref="MacAutomaticGraphicsSwitchingKey"/>가 있는가.</param>
        /// <param name="valueElementName">있다면 그 값 요소의 이름(<c>true</c>/<c>false</c>/<c>string</c>…).
        /// 없으면 <c>null</c>.</param>
        /// <param name="reason">사람이 읽을 사유(로그·실패 메시지용). 항상 채워진다.</param>
        public static PlistVerdict ClassifyMacPlistEntry(bool keyPresent, string valueElementName, out string reason)
        {
            string desiredElement = MacDesiredAutomaticGraphicsSwitching ? MacTrueElementName : MacFalseElementName;

            if (!keyPresent)
            {
                reason = $"'{MacAutomaticGraphicsSwitchingKey}' 키가 루트 dict에 없다 — " +
                         $"<{desiredElement}/>로 선언한다.";
                return PlistVerdict.NeedsDeclaration;
            }

            if (string.Equals(valueElementName, desiredElement, System.StringComparison.Ordinal))
            {
                reason = $"이미 '{MacAutomaticGraphicsSwitchingKey}'가 <{desiredElement}/>로 선언돼 있다 — " +
                         "쓰지 않는다(멱등).";
                return PlistVerdict.AlreadyDeclared;
            }

            reason = $"'{MacAutomaticGraphicsSwitchingKey}'가 <{valueElementName ?? "(값 없음)"}/>로 " +
                     $"선언돼 있다 — 기대한 것은 <{desiredElement}/>뿐이다. " +
                     "사람이 일부러 꺼 두었거나 다른 도구가 이 plist를 이미 만졌다. " +
                     "그 결정을 빌드 후처리가 조용히 덮어쓰지 않는다.";
            return PlistVerdict.Unexpected;
        }

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
