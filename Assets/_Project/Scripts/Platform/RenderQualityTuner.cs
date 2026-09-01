using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// 안티에일리어싱(MSAA) 배수를 <b>런타임에서 한 곳으로</b> 모으는 클래스.
    ///
    /// <para>왜 필요한가 — 이 값은 지금까지 <c>Assets/Editor/BuildStandalone.cs</c>의
    /// <c>ConfigureAntiAliasing()</c>이 빌드 시점에 4x로 굳혀 넣는 것이 유일한 경로였다. 그래서
    /// "MSAA를 바꾸면 부하가 얼마나 주는가"를 재려면 매번 다시 빌드해야 했고, 조건마다 다른
    /// 바이너리를 비교하게 되어 측정 자체가 오염된다. 이 클래스는 <b>같은 바이너리로</b> MSAA만
    /// 바꿔 A/B를 돌릴 수 있는 진입점 하나를 만든다(2026-08-31 "60fps 유지 + 부하 감소" 라운드,
    /// 결과는 docs/ARCHITECTURE.md 6-16절).</para>
    ///
    /// <para><b>계측용 환경변수</b> <c>STICKMATE_FORCE_MSAA</c> (0 | 2 | 4 | 8).
    /// 지정하지 않으면 제품 동작에 영향 0(프로젝트 설정값 그대로). <see cref="FramePacing"/>의
    /// <c>STICKMATE_FORCE_TIER</c>와 같은 규약이며, 둘을 함께 주면 <b>present는 60fps에 고정한 채
    /// MSAA만</b> 바꾸는 실험이 된다.</para>
    ///
    /// <para><b>macOS 실행 예</b> (앱 번들에 환경변수를 주려면 <c>open --env</c>가 필요하다.
    /// 터미널에서 <c>export</c>만 하면 <c>open</c>이 새 프로세스를 launchd로 띄우므로 전달되지 않는다):
    /// <code>
    /// open -n -a StickMate.app --env STICKMATE_FORCE_TIER=Active --env STICKMATE_FORCE_MSAA=0
    /// </code></para>
    ///
    /// <para><b>Windows 실행 예</b> — cmd.exe:
    /// <code>
    /// set STICKMATE_FORCE_TIER=Active
    /// set STICKMATE_FORCE_MSAA=0
    /// "C:\Program Files\StickMate\StickMate.exe"
    /// </code>
    /// PowerShell(<c>set</c>은 PowerShell에서 다른 명령이다 — 반드시 <c>$env:</c>를 쓸 것):
    /// <code>
    /// $env:STICKMATE_FORCE_TIER = "Active"
    /// $env:STICKMATE_FORCE_MSAA = "0"
    /// &amp; "C:\Program Files\StickMate\StickMate.exe"
    /// </code>
    /// <b>Windows에서 흔히 틀리는 두 가지</b>:
    /// (1) <b>탐색기에서 더블클릭하면 환경변수가 전달되지 않는다</b> — 반드시 위 변수를 설정한
    ///     <i>그 콘솔 창에서</i> 실행해야 한다. 콘솔을 새로 열면 변수는 사라진다.
    /// (2) <c>setx</c>는 쓰지 마라. 사용자 환경에 <b>영구 등록</b>되어 나중에 평범하게 실행한 앱까지
    ///     계측 모드로 돌게 된다(계측이 제품 동작을 오염시킨다).</para>
    ///
    /// <para><b>★ 적용 시점이 전부다 — 시작 전에만 먹는다.</b>
    /// <see cref="RuntimeInitializeLoadType.BeforeSceneLoad"/>에서 거는 것은 실측으로 유효함이
    /// 확인됐다(그래픽 메모리가 실제로 갈린다). 그러나 <b>그 뒤에 바꾸면 아무 일도 일어나지 않는다</b> —
    /// 아래 "닫힌 길" 주석 참고. 런타임 설정 UI를 만든다면 "재시작 필요"로 표시해야 한다.</para>
    ///
    /// <para><b>그래서 A/B는 반드시 "콜드 스타트"로만 한다.</b> 앱을 <b>완전히 종료</b>한 뒤 다른 값으로
    /// 다시 켜는 것 외에 유효한 방법이 없다. 실행 중에 값을 바꾸고 <c>Screen.msaaSamples</c>가 바뀐 것을
    /// 확인해도 <b>그건 거짓 보고다</b>(백버퍼는 그대로다). Windows에서는 종료 후 작업 관리자에
    /// <c>StickMate.exe</c>가 남아 있지 않은지 확인하고 다음 회차를 시작할 것 — 트레이/백그라운드
    /// 잔류 프로세스가 있으면 두 회차가 같은 백버퍼를 공유한다.</para>
    ///
    /// <para><b>검증은 이 클래스가 아니라 로그가 한다.</b> <see cref="RenderDiagnostics"/>가 시작 후
    /// 60초에 남기는 <c>[렌더진단] ★A/B 요약</c> 한 줄에 (a) 이번 실행의 MSAA 상태와
    /// (b) <b>GPU 프레임 시간</b>이 함께 들어 있다. 두 회차의 그 줄을 나란히 놓는 것이 이 실험의
    /// 전부다. <see cref="MutatedAfterStartup"/>가 true로 찍혔다면 그 회차는 폐기한다.</para>
    /// </summary>
    public static class RenderQualityTuner
    {
        /// <summary>계측/디버그용 MSAA 강제 지정 환경변수 이름.</summary>
        public const string MsaaEnvironmentVariableName = "STICKMATE_FORCE_MSAA";

        // ====================================================================
        // 플랫폼별 기본 MSAA — 2026-09-01 "윈도우만 렉" 라운드에서 갈라졌다
        // ====================================================================
        //
        // ★ 왜 플랫폼마다 다른 값인가 — 같은 설정의 비용이 GPU 구조에 따라 정반대다(둘 다 실측).
        //
        //   macOS(Apple GPU, TBDR)  : MSAA resolve가 **타일 메모리 안에서** 끝난다. 4x vs 0x를
        //                             콜드 스타트 페어드 6쌍으로 재도 CPU/GPU 절감이 **검출되지
        //                             않았다**(부호조차 일정하지 않음). 비용은 메모리 93MB뿐이고
        //                             화질 이득은 확실하다 -> **4x 유지가 순이득.**
        //   Windows(즉시 모드 IMR)   : resolve가 **실제 메모리 대역폭**이다. 게다가 이 앱은
        //                             화면 전체 크기 투명 오버레이 + 레거시 BitBlt 제출이라
        //                             그 위에 표면 복사가 한 번 더 얹힌다.
        //                             -> 사용자 실기 A/B에서 **0x가 체감 렉을 줄였다**(2026-09-01).
        //
        // 즉 이건 "한쪽을 깎는" 것이 아니라 **같은 값이 두 하드웨어에서 다른 물건**이라는 사실을
        // 코드에 반영하는 것이다. macOS를 Windows에 맞춰 내리면 아무 이득 없이 화질만 잃는다.
        //
        // ★ 이 값을 바꿀 때 읽을 것: Assets/Editor/BuildStandalone.cs의 ConfigureAntiAliasing()
        //   문서(8x 함정 — Apple GPU가 8을 조용히 4로 낮추면서 Screen.msaaSamples는 8을 계속
        //   보고한다). **8은 어느 플랫폼에도 주지 마라.**

        /// <summary>
        /// Windows 데스크톱 빌드의 기본 MSAA 배수.
        ///
        /// <para><b>실기 확정 사실(2026-09-01, 사용자 콜드 스타트 A/B)</b>:
        /// <list type="bullet">
        ///   <item><c>4x</c> — 화질 좋음 / <b>렉 있음</b></item>
        ///   <item><c>0x</c> — 렉 줄어듦 / <b>"지저분함"</b>(사용자 표현, 명백한 화질 회귀)</item>
        /// </list>
        /// 양 끝이 모두 기각됐으므로 중간값을 기본으로 둔다. <b>다만 "2x면 비용도 절반"은 추측이며,
        /// 이 라운드는 그것을 사실로 가정하지 않는다</b> — MSAA 비용에는 배수에 비례하지 않는
        /// <b>고정 성분</b>(별도 멀티샘플 서피스 + 화면 전체 resolve 패스 자체)이 있고, 이 앱은
        /// 화면의 99% 이상이 "지워지기만 하고 아무것도 안 그려지는" 픽셀이라 배수 비례 성분이
        /// 압축으로 상당 부분 사라진다. 그래서 <c>2x</c>의 실제 절감은 <b>절반보다 작을 것</b>으로
        /// 예측한다. 검증 방법은 아래 문단에 있다.</para>
        ///
        /// <para><b>이 예측을 확정하는 법(사용자가 3분에 끝낼 수 있다)</b>: <c>STICKMATE_FORCE_MSAA</c>를
        /// 0 / 2 / 4로 주며 <b>콜드 스타트 3회</b>. 각 회차의 <c>[렌더진단] ★A/B 요약</c> 줄에 있는
        /// <b>GPU 프레임시간</b>을 비교한다. 2x가 4x에 가까우면 고정 성분이 지배하는 것이고(=2x는
        /// 나쁜 절충, 셰이더 AA로 가야 한다), 중간에 있으면 배수 성분이 지배하는 것이다.</para>
        /// </summary>
        /// <para><b>★ 2026-09-01 실기 측정으로 위 예측이 확정됐고, 그 결과 이 값은 4로 되돌아왔다.</b>
        /// 사용자 Windows 실기에서 MSAA <b>4x / 2x / 0x 세 경우 모두 StickMate GPU 사용률이 약 30%로
        /// 동일</b>했다("GPU 비슷함", "줄여도 해결이 안되는거 같음"). 즉 <b>MSAA는 이 앱의 GPU 비용과
        /// 무관하다</b> — 고정 성분이 지배할 것이라는 예측을 넘어, 배수 성분과 고정 성분 어느 쪽도
        /// 유의미하지 않았다.
        /// 비용이 같다면 <b>화질이 가장 좋은 값을 쓰는 것이 옳다</b>. 사용자 판정은
        /// 4x=좋음 / 2x="봐줄만함" / 0x="지저분함"이었으므로 4로 되돌린다(= macOS와 동일, 분기 없음).
        /// 이에 따라 "MSAA를 끄고 셰이더 AA로 대체한다"는 후속 라운드도 전제가 무너져 중단됐다.
        /// 진짜 병목은 다른 곳이다(제출 횟수 비례 비용 조사 중).</para>
        public const int WindowsDefaultSamples = 4;

        /// <summary>이번 실행에서 실제로 요청한 배수(환경변수가 없으면 플랫폼 기본값/프로젝트 설정값).</summary>
        public static int RequestedSamples { get; private set; }

        /// <summary>이번 실행의 배수가 <see cref="MsaaEnvironmentVariableName"/>으로 강제된 것인가
        /// (= 제품 기본값이 아니라 계측용 값인가).</summary>
        public static bool ForcedByEnvironment { get; private set; }

        /// <summary>
        /// <b>이 라운드의 핵심 신뢰 지표.</b> 시작(<c>BeforeSceneLoad</c>) <b>이후에</b>
        /// <see cref="Apply"/>가 불렸는가.
        ///
        /// <para>왜 이게 "실제 MSAA"를 아는 유일한 방법인가: 백버퍼는 시작할 때 한 번 만들어지고 그
        /// 뒤로 바뀌지 않는데, <c>Screen.msaaSamples</c>는 <b>그 사실을 감추고 요청값을 되돌려준다</b>
        /// (실측: 런타임에 4->0으로 바꾸면 즉시 0을 보고하지만 그래픽 메모리는 1바이트도 안 움직였다).
        /// 그래서 "지금 몇 x인가?"를 API에 묻는 대신 <b>"언제 정해졌는가?"</b>라는 인과적 사실을
        /// 기록한다. 이 값이 false면 <see cref="RequestedSamples"/>는 백버퍼의 진실이고,
        /// true면 그 실행의 MSAA 측정은 무효다.</para>
        /// </summary>
        public static bool MutatedAfterStartup { get; private set; }

        /// <summary>시작 초기화가 끝났는지(= 이후의 <see cref="Apply"/>는 백버퍼에 안 먹는다).</summary>
        private static bool _startupComplete;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyFromEnvironment()
        {
            RequestedSamples = QualitySettings.antiAliasing;
            ForcedByEnvironment = false;
            MutatedAfterStartup = false;

            int? forced = ReadEnvSamples(MsaaEnvironmentVariableName);
            if (forced.HasValue)
            {
                // 계측이 제품 기본값을 이긴다 — A/B를 하려면 반드시 그래야 한다.
                Apply(forced.Value);
                ForcedByEnvironment = true;
                Debug.Log($"[렌더품질] ★ {MsaaEnvironmentVariableName}={forced.Value} 강제 지정됨(계측용). " +
                    $"QualitySettings.antiAliasing={QualitySettings.antiAliasing} 적용. " +
                    "이 값이 백버퍼에 실제로 걸렸는지는 잠시 뒤의 '[렌더진단] 콜드스타트' 줄로 확인할 것 " +
                    "— Screen.msaaSamples는 거짓말을 한 전례가 있다.");
            }
            else if (TryGetPlatformDefault(out int platformDefault) &&
                     platformDefault != QualitySettings.antiAliasing)
            {
                int before = QualitySettings.antiAliasing;
                Apply(platformDefault);
                Debug.Log($"[렌더품질] 플랫폼 기본 MSAA 적용 — {before}x -> {platformDefault}x. " +
                    "(프로젝트 설정 4x는 macOS 기준값이다. Windows는 즉시 모드 GPU라 화면 전체 " +
                    "resolve가 실제 대역폭 비용이므로 여기서 낮춘다 — 근거는 이 클래스의 " +
                    "WindowsDefaultSamples 문서.)");
            }

            _startupComplete = true;
        }

        /// <summary>
        /// 이 플랫폼이 프로젝트 설정값을 덮어써야 하는가.
        ///
        /// <para><b>macOS/에디터/모바일은 false를 돌려준다 — 즉 <see cref="Apply"/>가 아예 불리지
        /// 않는다.</b> "같은 값을 다시 대입"하는 것도 아니고 호출 자체가 없다. macOS 경로가 이 라운드로
        /// 한 바이트도 바뀌지 않는다는 것을 이 한 줄로 눈으로 확인할 수 있게 하려는 구조다
        /// (CLAUDE.md의 플랫폼 동시 검토 규칙).</para>
        ///
        /// <para><c>!UNITY_EDITOR</c>를 함께 거는 이유: 에디터에서 Windows 타깃을 잡아두고 플레이하면
        /// <c>UNITY_STANDALONE_WIN</c>이 켜지는데, 그때 프로젝트의 Quality Settings를 실제로 덮어쓰면
        /// <b>에셋이 더러워진 채 커밋될 수 있다</b>. 계측/제품 설정이 에디터 상태를 오염시키지 않게 한다.</para>
        /// </summary>
        private static bool TryGetPlatformDefault(out int samples)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            samples = WindowsDefaultSamples;
            return true;
#else
            samples = 0;
            return false;
#endif
        }

        /// <summary>
        /// 진단 로그용 — "이번 실행의 MSAA는 몇 x이고, 그 값을 믿어도 되는가"를 한 문장으로.
        /// <see cref="RenderDiagnostics"/>의 두 로그가 모두 이걸 쓴다(같은 문장을 두 번 쓰지 않는다).
        /// </summary>
        public static string DescribeState()
        {
            string source = ForcedByEnvironment
                ? $"강제 {MsaaEnvironmentVariableName}"
                : "기본값";
            string trust = MutatedAfterStartup
                ? "★신뢰 불가: 시작 이후 런타임에 변경됨 — 백버퍼에는 반영되지 않았다. 이 실행의 MSAA 비교는 폐기할 것"
                : "신뢰 가능: BeforeSceneLoad에서 확정(백버퍼가 만들어지기 전)";
            return $"요청={RequestedSamples}x ({source}, {trust})";
        }

        // ====================================================================
        // 【실측으로 닫힌 길】 런타임 MSAA 토글은 백버퍼에 반영되지 않는다 (2026-08-31)
        // ====================================================================
        // 한때 이 파일에는 파일 감시로 MSAA를 3초마다 교차시키는 계측 모드가 있었다(6-2의 E1~E6이
        // 쓴 3초 교차 페어드 설계를 MSAA에 그대로 적용하려는 시도). **무효로 판명돼 삭제했다.**
        //   · 런타임에 QualitySettings.antiAliasing을 4 -> 0으로 바꾸면 Screen.msaaSamples는
        //     **즉시 0을 보고한다**. 그런데 vmmap의 `owned unmapped (graphics)`는 4x/2x/0x를
        //     22초 간격으로 돌려도 **99.5MB에서 1바이트도 움직이지 않았다**.
        //   · 반면 같은 값을 **콜드 스타트**로 주면 그 영역이 98.3MB <-> 5.3MB로 정확히 갈린다
        //     (6쌍 전부 같은 방향, 표준편차 0.43MB).
        //   -> 백버퍼는 시작할 때 한 번 만들어지고 그 뒤 바뀌지 않으며,
        //      **Screen.msaaSamples는 그 사실을 감춘다**(이 프로젝트가 8x에서 이미 당한 함정과
        //      같은 부류 — 커밋 39ab690, BuildStandalone.ConfigureAntiAliasing 주석).
        // 교훈: MSAA A/B는 반드시 앱을 껐다 켜서 해야 하고, 유효성 검증은 API가 아니라
        //       **그래픽 메모리 실측**으로 해야 한다.

        /// <summary>
        /// 모든 품질 레벨의 MSAA 배수를 바꾼다. 허용값은 0(끔)/2/4/8이며 그 외는 가장 가까운
        /// 아래 값으로 내린다 — Unity는 지원하지 않는 배수를 조용히 다른 값으로 바꾸므로
        /// 여기서 미리 정규화해 "요청값 로그"와 실제 요청이 어긋나지 않게 한다.
        /// (Apple GPU는 8x를 조용히 4x로 낮춘다 — 8은 주지 말 것.)
        /// </summary>
        public static void Apply(int samples)
        {
            int normalized = Normalize(samples);
            int originalLevel = QualitySettings.GetQualityLevel();
            string[] names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
            {
                // false = 레벨 전환 시 무거운 리소스 재적용을 건너뛴다(BuildStandalone과 같은 규약).
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.antiAliasing = normalized;
            }
            QualitySettings.SetQualityLevel(originalLevel, false);
            RequestedSamples = normalized;

            // 시작이 끝난 뒤의 호출은 **백버퍼에 반영되지 않는다**(위 "닫힌 길" 참고). 조용히 넘어가면
            // 나중에 그 실행의 측정값을 진짜인 줄 알고 쓰게 되므로, 사실을 플래그로 남겨 진단 로그가
            // 스스로 "이 회차는 무효"라고 말하게 한다.
            if (_startupComplete) MutatedAfterStartup = true;
        }

        private static int Normalize(int samples)
        {
            if (samples >= 8) return 8;
            if (samples >= 4) return 4;
            if (samples >= 2) return 2;
            return 0;
        }

        private static int? ReadEnvSamples(string name)
        {
            try
            {
                string v = System.Environment.GetEnvironmentVariable(name);
                if (string.IsNullOrEmpty(v)) return null;
                return int.TryParse(v.Trim(), out int parsed) ? Normalize(parsed) : (int?)null;
            }
            catch
            {
                // 샌드박스/플랫폼에 따라 환경변수 조회가 막힐 수 있다. 계측용 기능이므로 조용히 포기한다.
                return null;
            }
        }
    }
}
