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
    /// MSAA만</b> 바꾸는 실험이 된다. 사용 예:
    /// <c>open -n -a StickMate.app --env STICKMATE_FORCE_TIER=Active --env STICKMATE_FORCE_MSAA=0</c></para>
    ///
    /// <para><b>★ 적용 시점이 전부다 — 시작 전에만 먹는다.</b>
    /// <see cref="RuntimeInitializeLoadType.BeforeSceneLoad"/>에서 거는 것은 실측으로 유효함이
    /// 확인됐다(그래픽 메모리가 실제로 갈린다). 그러나 <b>그 뒤에 바꾸면 아무 일도 일어나지 않는다</b> —
    /// 아래 "닫힌 길" 주석 참고. 런타임 설정 UI를 만든다면 "재시작 필요"로 표시해야 한다.</para>
    /// </summary>
    public static class RenderQualityTuner
    {
        /// <summary>계측/디버그용 MSAA 강제 지정 환경변수 이름.</summary>
        public const string MsaaEnvironmentVariableName = "STICKMATE_FORCE_MSAA";

        /// <summary>이번 실행에서 실제로 요청한 배수(환경변수가 없으면 프로젝트 설정값).</summary>
        public static int RequestedSamples { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyFromEnvironment()
        {
            RequestedSamples = QualitySettings.antiAliasing;

            int? forced = ReadEnvSamples(MsaaEnvironmentVariableName);
            if (forced.HasValue)
            {
                Apply(forced.Value);
                Debug.Log($"[렌더품질] ★ {MsaaEnvironmentVariableName}={forced.Value} 강제 지정됨(계측용). " +
                    $"QualitySettings.antiAliasing={QualitySettings.antiAliasing} 적용. " +
                    "실제로 걸렸는지는 잠시 뒤의 'MSAA 실측=' 로그(Screen.msaaSamples)와 " +
                    "`vmmap --summary <pid>`의 'owned unmapped (graphics)'로 함께 확인할 것 " +
                    "— 전자는 거짓말을 한 전례가 있다.");
            }
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
