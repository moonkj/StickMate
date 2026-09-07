using System;
using System.Globalization;
using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// ★ 2026-09-07 — <b>계측용</b> UI 밀도(캔버스 배율) 강제 지정.
    /// <c>RenderQualityTuner</c>의 <c>STICKMATE_FORCE_MSAA</c>와 <b>똑같은 관례</b>다:
    /// 환경변수를 안 주면 <b>이 클래스는 아무 일도 하지 않는다</b>(제품 동작에 영향 0).
    ///
    /// ============================================================================
    /// 왜 필요한가 — <b>이 개발 머신에서는 배율 1.5를 만들 수 없다</b>
    /// ============================================================================
    /// 사용자 신고는 <b>Windows 디스플레이 125% / 150%</b>에서 나온다. 그런데:
    /// <list type="bullet">
    ///   <item>이 저장소에 Windows 머신이 없다.</item>
    ///   <item>macOS는 앱에게 <c>backingScaleFactor</c>를 <b>2.0(또는 1.0)</b>으로만 준다.
    ///     "더 넓게/더 크게" 스케일 모드는 <b>2배로 그린 뒤 OS가 다운샘플</b>하는 것이라
    ///     앱이 보는 배율은 여전히 2.0이다. <b>디스플레이 설정으로는 1.5를 낼 수 없다.</b></item>
    /// </list>
    /// 그래서 <b>비정수 배율에서만 나타나는 결함</b>(<see cref="GlyphPixelSnapPolicy"/>의 위상 축,
    /// <see cref="UiGlyphScalePolicy"/>의 크기 축)은 이 머신에서 <b>원리적으로 재현할 수 없었다</b>.
    /// 실측 근거: 배치 좌표 154개 중 격자 밖 비율이 <b>배율 1.25에서 71.4% / 1.5에서 14.3%</b>인데
    /// <b>2.0에서는 1.3%</b>다 — macOS에서 아무리 봐도 안 보이는 것이 당연했다.
    ///
    /// ============================================================================
    /// ★ 왜 <c>StickConfig.desktopDpiScale</c>을 쓰지 않는가 — 그것은 <b>좌표까지</b> 바꾼다
    /// ============================================================================
    /// <c>ScreenCoordinateConverter.ResolveCanvasScaleFactor</c>는 <c>desktopDpiScale</c>을 1순위로
    /// 보지만, <b>같은 필드를 <c>ResolveDpiScale</c>(좌표 변환)도 본다</b>(같은 클래스의 그 메서드).
    /// 그 값을 건드리면 캔버스 배율과 함께 <b>OS↔Unity 좌표 변환까지</b> 틀어져 캐릭터와 발판이
    /// 어긋난다 — 글자 선명도를 재려다 화면 배치를 망가뜨리는 셈이다.
    ///
    /// <para>이 클래스는 대신 <see cref="ScreenCoordinateConverter.ReportUiDensityScale"/>만 부른다.
    /// 그것이 채우는 <c>AutoUiDensityScale</c>은 <b>UI 밀도 전용</b>이고 좌표 쪽
    /// <c>_autoDpiScale</c>과 완전히 분리돼 있다(같은 파일 "두 개념을 이름부터 분리한다" 블록).
    /// ⇒ <b>좌표는 정상인 채로 캔버스 배율만</b> Windows 150%와 동일하게 만든다.</para>
    ///
    /// ============================================================================
    /// 쓰는 법 (macOS)
    /// ============================================================================
    /// <code>
    /// open -n -a StickMate.app --env STICKMATE_FORCE_UI_DENSITY=1.5    # Windows 150% 재현
    /// open -n -a StickMate.app --env STICKMATE_FORCE_UI_DENSITY=1.25   # Windows 125% 재현(최악)
    /// </code>
    /// Windows(cmd.exe)에서는 <c>set STICKMATE_FORCE_UI_DENSITY=1.5</c> 뒤 같은 콘솔에서 실행.
    /// <c>setx</c>는 쓰지 마라 — 사용자 환경에 영구 등록되어 평범하게 실행한 앱까지 계측 모드가 된다.
    ///
    /// <para><b>주의</b>: 이건 <b>계측 도구지 설정이 아니다</b>. 배율을 낮추면 UI의 물리적 크기가
    /// 그만큼 작아진다(2.0 → 1.5면 75%). 가독성 판단에 쓰지 말고 <b>선명도(위상·크기 잔차)</b>를
    /// 재는 데만 써라.</para>
    ///
    /// <para><b>플랫폼</b>: <c>#if</c> 분기가 없다. macOS/Windows/모바일이 같은 코드를 돌고,
    /// 환경변수가 없으면 <b>세 곳 모두 한 바이트도 바뀌지 않는다</b>.</para>
    /// </summary>
    public static class UiDensityOverride
    {
        /// <summary>계측용 UI 밀도 강제 지정 환경변수 이름.
        /// <para>테스트는 이 상수를 <b>참조</b>해야 하며 문자열을 베끼면 안 된다(CLAUDE.md).</para></summary>
        public const string EnvironmentVariableName = "STICKMATE_FORCE_UI_DENSITY";

        /// <summary>받아들이는 배율의 하한/상한. 실재하는 디스플레이 배율은 1.0~4.0 사이이고,
        /// 그 밖의 값은 오타(예: <c>150</c>을 넣는 것)일 가능성이 훨씬 높다 — 조용히 먹으면
        /// UI가 화면 밖으로 날아가고 원인을 못 찾는다.</summary>
        public const float MinAcceptedDensity = 0.5f;
        public const float MaxAcceptedDensity = 4f;

        /// <summary>이번 실행에서 환경변수로 강제됐는가(진단·테스트용).</summary>
        public static bool ForcedByEnvironment { get; private set; }

        /// <summary>강제된 값(강제되지 않았으면 0).</summary>
        public static float ForcedDensity { get; private set; }

        /// <summary>
        /// 문자열을 배율로 해석한다. <b>순수 함수</b>라 EditMode 테스트가 프로세스 환경을 건드리지
        /// 않고 전수 검증할 수 있다(파싱은 이 저장소가 여러 번 틀린 자리다 — 로캘의 소수점 기호가
        /// 쉼표인 환경에서 <c>"1.5"</c>가 <b>조용히</b> 15로 읽히거나 파싱에 실패한다.
        /// 그래서 <see cref="CultureInfo.InvariantCulture"/>를 못박는다).
        /// </summary>
        public static bool TryParseDensity(string raw, out float density)
        {
            density = 0f;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            if (!float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                return false;
            if (float.IsNaN(v) || float.IsInfinity(v)) return false;
            if (v < MinAcceptedDensity || v > MaxAcceptedDensity) return false;
            density = v;
            return true;
        }

        /// <summary>
        /// ★ <b>적용 시점이 전부다.</b> <c>BeforeSceneLoad</c>에서 걸어야 캔버스를 만드는 코드
        /// (<c>CharacterInfoWindow.EnsureCanvas</c> 등)가 <b>처음부터</b> 이 값을 본다.
        /// 나중에 바꾸면 이미 만들어진 캔버스는 다음 <c>ApplyCanvasScaleFactor()</c>까지 옛 값을 쓴다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyFromEnvironment()
        {
            ForcedByEnvironment = false;
            ForcedDensity = 0f;

            string raw;
            try { raw = Environment.GetEnvironmentVariable(EnvironmentVariableName); }
            catch (Exception) { return; }        // 샌드박스에서 환경변수 조회가 막힐 수 있다.

            if (string.IsNullOrEmpty(raw)) return;   // ← 평상시 경로. 여기서 끝난다.

            if (!TryParseDensity(raw, out float density))
            {
                Debug.LogWarning($"[UI밀도] {EnvironmentVariableName}=\"{raw}\"를 해석하지 못했습니다 " +
                    $"(허용 범위 {MinAcceptedDensity}~{MaxAcceptedDensity}, 소수점은 마침표). " +
                    "무시하고 자동 산출값을 그대로 씁니다.");
                return;
            }

            ScreenCoordinateConverter.ReportUiDensityScale(density);
            ForcedByEnvironment = true;
            ForcedDensity = density;

            Debug.Log($"[UI밀도] ★ {EnvironmentVariableName}={density:F3} 강제 지정됨(계측용). " +
                $"캔버스 배율이 이 값이 되고 <좌표 변환은 건드리지 않습니다> — " +
                $"이 배율에서 정수 픽셀이 되는 pt 간격은 " +
                $"{UiGlyphScalePolicy.ExactPointStep(density)}입니다" +
                $"(1이면 모든 정수 pt 안전, 2면 짝수만, 4면 4의 배수만). " +
                "★ 제품 판단(가독성·크기)에 이 실행을 쓰지 마세요. 선명도 계측 전용입니다.");
        }
    }
}
