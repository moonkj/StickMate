namespace StickMate.Platform
{
    /// <summary>
    /// "오버레이 창을 지금 다시 크기/위치/해상도 맞춤 해야 하는가"를 결정하는 <b>순수 규칙</b>.
    /// UnityEngine 의존도 P/Invoke도 한 줄 없다 — 그래야 Windows 실기가 없는 개발 머신에서
    /// 규칙 자체를 실행해 검증할 수 있다(<see cref="TopmostRestorePolicy"/>와 같은 설계).
    ///
    /// ============================================================================
    /// 왜 생겼는가 (2026-09-01, 사용자 신고 "계속 실행해 놓을수록 렉이 심해지는거 같음")
    /// ============================================================================
    /// 실기 로그: 프레임 시간 꼬리가 세션이 갈수록 나빠지고(p99 150ms, 최대 <b>407ms</b>) GPU는
    /// 0.01ms로 무관했다. 같은 로그에 창 크기가 재적용마다 1px씩 줄어드는 흔적이 있었다:
    /// <c>windowSize=(3840) -> (3839) -> (3838) -> ... -> (3831)</c>.
    ///
    /// 원인은 <b>래칫(ratchet)</b>이었다. 창 기하 판정이 "목표와 정확히 같은가"(<c>!=</c>)였기 때문에,
    /// 되읽기가 대입값보다 1px 작게 돌아오는 <b>상수 오차</b>가 영원히 "불일치"로 읽혔다. 그래서
    ///   · <c>Screen.SetResolution</c> 재호출과
    ///   · 창 크기 재대입
    /// 이 계속 실행됐고, <b>둘 다 클라이언트 영역을 바꾸므로 D3D 스왑체인과 DWM 리디렉션 표면이
    /// 재생성된다</b> — 그것이 수백 ms짜리 정지의 정체다. 게다가 재적용마다 1px씩 더 줄어들어
    /// 오차가 커지므로 <b>시간이 갈수록 나빠진다</b>. 사용자가 말한 그대로다.
    ///
    /// 이 저장소는 이 인과를 이미 알고 있었다 — <see cref="DisplayTopologyWatcher"/> 클래스 문서가
    /// "중간 상태마다 <c>Screen.SetResolution</c>을 부르면 백버퍼 재할당이 연달아 일어나 사용자가
    /// 체감하는 멈춤이 오히려 길어진다"고 적어 두었다. 판정 조건만 그 경고를 위반하고 있었다.
    ///
    /// ============================================================================
    /// 처방이 "불감대"인 이유 — 증상을 덮는 것이 아니다
    /// ============================================================================
    /// 1px 오차 자체는 우리가 없앨 수 없다. 후보 원인 두 가지 모두 우리 코드 밖에 있다:
    ///   (a) <c>Screen.SetResolution</c>은 프레임 끝에 지연 적용되며 클라이언트 사각형 기준이다.
    ///   (b) 라이브러리의 <c>SetSize</c>(SetWindowPos)와 <c>GetSize</c>(GetWindowRect)가 레이어드+DWM
    ///       확장 프레임에서 서로 다른 사각형을 볼 수 있다.
    /// 그리고 1px이 남아도 <b>기능적 손실이 없다</b>: 좌표 변환기는 "창 폭 == 모니터 폭"을 가정하지
    /// 않고 실측 창 사각형에서 배율/원점을 유도한다. 반대로 스왑체인 재생성은 수백 ms 정지다.
    /// 그러므로 옳은 처방은 "1px을 없애기"가 아니라 <b>1px이 재적용을 유발하지 못하게 막기</b>다.
    ///
    /// 불감대가 진짜 어긋남까지 덮지 않도록 값은 <see cref="DefaultEpsilonPixels"/>로 좁게 잡고,
    /// 호출자는 실측 오차와 재생성 누적 횟수를 항상 로그에 함께 남긴다.
    /// </summary>
    public static class OverlayBoundsFitPolicy
    {
        /// <summary>
        /// 기본 불감대(픽셀). 관측된 오차는 1px 하나뿐이므로 2면 그 상수 오차를 흡수하면서
        /// 사람이 인지할 수 있는 어긋남(수 px 이상)은 그대로 잡는다.
        /// <b>늘리지 말 것</b> — 늘려야 할 실측 근거가 생기면 그 로그를 여기 함께 남긴다.
        /// </summary>
        public const float DefaultEpsilonPixels = 2f;

        /// <summary>
        /// 프로세스 수명 전체에서 <c>Screen.SetResolution</c>을 부를 수 있는 기본 상한.
        ///
        /// <para>정상 경로는 기동 시 1회다. 디스플레이 구성 변경(모니터 착탈/해상도 변경)이 세션당
        /// 몇 번 일어나도 감당하면서, 판정이 진동하면 즉시 멈춘다. 24시간 상주 앱에서 이 호출이
        /// 무제한이면 사용자는 몇 초마다 수백 ms씩 얼어붙는 앱을 보게 된다.</para>
        ///
        /// <para><b>주의</b>: <c>Platform/Windows/WindowsOverlayStateEnforcer</c>는 아직 같은 값을
        /// 자기 파일의 <c>private const int MaxSetResolutionCalls = 4</c>로 들고 있다(이번 라운드는
        /// 그 파일을 읽기 전용으로 다뤘다). 두 값이 갈라지지 않도록
        /// <c>Tests/EditMode/OverlayResizeRatchetTests</c>가 Windows 소스의 리터럴을 실제로 읽어
        /// 이 상수와 대조한다 — 한쪽만 바꾸면 테스트가 깨진다.</para>
        /// </summary>
        public const int DefaultMaxSetResolutionCalls = 4;

        /// <summary>
        /// 프로세스 수명 전체에서 <b>창 크기를 재대입</b>할 수 있는 기본 상한.
        ///
        /// ============================================================================
        /// 왜 생겼는가 (2026-09-01, 병행 라운드의 대칭성 지적)
        /// ============================================================================
        /// <see cref="DefaultMaxSetResolutionCalls"/>는 수명 상한이 있는데 <b>창 크기 재대입에는
        /// 상한이 아예 없었다</b>. 두 호출은 성질이 같다 — 둘 다 클라이언트 영역을 바꾸므로
        /// <b>OS 표면(스왑체인/백버퍼/리디렉션 표면)이 재생성</b>되고, 그것이 수백 ms짜리 정지다.
        /// 한쪽만 막아 두면 나머지 한쪽으로 같은 사고가 그대로 재발한다.
        ///
        /// <para><b>지금 터지는 버그가 아니다</b>(정직하게): 같은 라운드 실측에서 2px 불감대가 관측
        /// 오차를 흡수하고 있고, 에피소드당 <c>MaxFullScreenApplyAttempts</c> 하드 상한도 있으며,
        /// macOS 92분 세션에서 전체화면 확장 시도는 1회, <c>windowSize</c> 11회 관측이 전부 동일해
        /// <b>드리프트 0</b>이었다. 이것은 <b>불감대를 넘는 오차를 가진 환경에서 다시 열릴 문</b>을
        /// 미리 닫는 하드닝이다.</para>
        ///
        /// <para>값이 <see cref="DefaultMaxSetResolutionCalls"/>와 같은 이유: 두 호출은 같은 함수의
        /// 같은 에피소드에서 짝으로 일어난다. 정상 경로는 기동 시 1회이고, 디스플레이 구성 변경이
        /// 세션당 몇 번 일어나도 감당하면서 진동은 즉시 멈춘다.</para>
        /// </summary>
        public const int DefaultMaxWindowResizeCalls = DefaultMaxSetResolutionCalls;

        /// <summary>
        /// 지금 창 크기를 다시 대입해도 되는가 — <see cref="ShouldResize"/>에 <b>수명 상한</b>을 얹은 것.
        /// 호출자는 이 함수만 쓰면 되고, 상한에 닿았는지는 <paramref name="callsSoFar"/>로 판단한다.
        /// </summary>
        public static bool ShouldResizeWithinBudget(float currentW, float currentH,
            float targetW, float targetH, float epsilonPixels, int callsSoFar, int maxCalls)
        {
            if (callsSoFar >= maxCalls) return false;
            return ShouldResize(currentW, currentH, targetW, targetH, epsilonPixels);
        }

        /// <summary>
        /// 유효하다고 인정하는 최대 백킹 배율(OS 포인트 1 = Unity 픽셀 몇 개). 4x를 넘는 디스플레이는
        /// 존재하지 않으므로, 그보다 큰 값이 들어오면 <b>배율 측정이 깨진 것</b>으로 보고 불감대를
        /// 넓히지 않는다 — 깨진 측정값으로 불감대를 키우면 진짜 어긋남까지 덮게 된다.
        /// </summary>
        public const float MaxDeviceScale = 4f;

        /// <summary>
        /// 목표 픽셀값의 <b>양자화 여유</b>(픽셀). 호출자는 목표를 <c>RoundToInt(포인트 / 배율)</c>로
        /// 만들므로 목표 자체에 최대 0.5px의 반올림 오차가 들어 있다.
        ///
        /// <para>이 항이 없으면 불감대가 <b>칼날 위</b>에 선다: 창 기하가 허용 오차의 정확히 끝(2pt)에
        /// 있을 때 해상도 차이가 불감대와 소수점 셋째 자리에서 갈린다(실측 계산:
        /// 창 1514pt에서 차이 4.000 vs 불감대 3.995 -> 재적용). 그러면 <b>기하 판정은 "맞았다"고 하는데
        /// 해상도 판정만 홀로 "틀렸다"</b>고 해서 <c>Screen.SetResolution</c>이 다시 불린다 —
        /// 이 파일이 없애려는 바로 그 재적용이다.</para>
        ///
        /// <para>0.5를 더해도 불감대가 진짜 어긋남을 덮지 않는다: 같은 계산에서 창이 4pt 어긋나면
        /// 차이 8.0 vs 불감대 4.5로 <b>여전히 재적용이 걸린다</b>.</para>
        /// </summary>
        public const float TargetRoundingSlackPixels = 0.5f;

        /// <summary>
        /// <b>해상도</b> 판정에 쓸 불감대를 <c>Screen.width</c>와 같은 단위(Unity 픽셀)로 유도한다.
        ///
        /// ============================================================================
        /// 왜 상수 2px을 그대로 쓰면 안 되는가 (2026-09-01 macOS 확장 라운드)
        /// ============================================================================
        /// 이 규칙 안에서 두 판정은 <b>서로 다른 좌표계</b>를 본다:
        /// <list type="bullet">
        ///   <item><see cref="ShouldResize"/>/<see cref="ShouldMove"/> — 창 사각형. 단위는 <b>OS 포인트</b>
        ///         (macOS 실측 1512x982).</item>
        ///   <item><see cref="ShouldSetResolution"/> — <c>Screen.width/height</c>. 단위는 <b>Unity 픽셀</b>
        ///         (같은 화면에서 3024x1964).</item>
        /// </list>
        /// Windows는 배율이 1이라 두 단위가 같아서 이 구분이 필요 없었다. macOS Retina는 배율이 2라
        /// <b>포인트 1 = 픽셀 2</b>이고, 그래서 2px 상수를 해상도 판정에 그대로 쓰면 실효 불감대가
        /// 1포인트로 <b>절반</b>이 된다 — 창 기하가 1포인트 어긋나는 순간 해상도 판정만 홀로 "불일치"가
        /// 되어 <c>Screen.SetResolution</c>이 다시 불린다. 그것이 바로 이 파일이 없애려는 래칫이다.
        ///
        /// 그래서 불감대의 <b>정의 단위는 OS 포인트</b>(사람이 보는 크기)로 두고, 픽셀 단위 판정에는
        /// 배율을 곱해 <b>유도</b>한다. 숫자를 플랫폼마다 흩뿌리지 않는 이유다.
        ///
        /// <para><b>지켜야 할 불변식</b>: 창 기하가 <see cref="ShouldResize"/>의 불감대 안에 있으면
        /// <see cref="ShouldSetResolution"/>도 반드시 조용해야 한다. 두 판정이 갈리는 순간 한쪽이
        /// 다른 쪽을 영원히 되살리는 래칫이 된다. <c>OverlayResizeRatchetTests</c>가 이 불변식을
        /// Retina 배율에서 실제로 계산해 잠근다.</para>
        /// </summary>
        /// <param name="osPointsPerUnityPixel">
        /// <c>ScreenCoordinateConverter.ResolveDpiScale</c>의 값 — "OS 포인트 / Unity 픽셀"이다
        /// (Windows 1.0 · macOS Retina 0.5). 값이 이상하면(0 이하/NaN/무한대) 배율을 모르는 것이므로
        /// <b>넓히지 않고</b> <see cref="DefaultEpsilonPixels"/>를 그대로 돌려준다.
        /// </param>
        public static float ResolutionEpsilonPixels(float osPointsPerUnityPixel)
        {
            if (float.IsNaN(osPointsPerUnityPixel) || float.IsInfinity(osPointsPerUnityPixel)
                || osPointsPerUnityPixel <= 0f)
            {
                return DefaultEpsilonPixels;
            }

            float deviceScale = 1f / osPointsPerUnityPixel;
            if (deviceScale < 1f) deviceScale = 1f;                       // 픽셀이 포인트보다 성기면 넓힐 이유가 없다.
            if (deviceScale > MaxDeviceScale) deviceScale = MaxDeviceScale;
            return DefaultEpsilonPixels * deviceScale + TargetRoundingSlackPixels;
        }

        /// <summary>두 값이 불감대 안에 있는가(둘 다 만족해야 한다).</summary>
        public static bool Within(float aX, float aY, float bX, float bY, float epsilonPixels)
        {
            return Abs(aX - bX) <= epsilonPixels && Abs(aY - bY) <= epsilonPixels;
        }

        /// <summary>
        /// 지금 창 크기를 다시 대입해야 하는가. <b>대입 한 번이 곧 OS 리사이즈 한 번이고,
        /// 그것이 백버퍼 재할당 한 번</b>이므로 "이미 충분히 맞았으면 손대지 않는다"가 규칙이다.
        /// </summary>
        public static bool ShouldResize(float currentW, float currentH,
            float targetW, float targetH, float epsilonPixels)
            => !Within(currentW, currentH, targetW, targetH, epsilonPixels);

        /// <summary>지금 창 위치를 다시 대입해야 하는가(<see cref="ShouldResize"/>와 같은 이유).</summary>
        public static bool ShouldMove(float currentX, float currentY,
            float targetX, float targetY, float epsilonPixels)
            => !Within(currentX, currentY, targetX, targetY, epsilonPixels);

        /// <summary>
        /// <b>"적합 완료"를 확정해도 되는가.</b> 확정은 되돌릴 수 없다 — 그 플래그가 서면 재적합 루프가
        /// 통째로 멈추고, 다시 무장하는 유일한 경로는 디스플레이 구성 변경뿐이다.
        ///
        /// ============================================================================
        /// 왜 "허용 오차 안"만으로는 부족한가 (2026-09-02, 오프셋 (11,−45) 수렴 실패)
        /// ============================================================================
        /// 두 Enforcer는 <b>같은 틱 안에서</b> 이렇게 했다:
        /// <list type="number">
        ///   <item><c>Screen.SetResolution(...)</c> 호출 — Unity 문서상 <b>프레임 끝에 적용</b>된다.</item>
        ///   <item>창 크기/위치 대입.</item>
        ///   <item>곧바로 되읽어 <c>Within(...)</c>이면 <b>완료로 확정</b>.</item>
        /// </list>
        /// 3번의 측정은 1번이 요청한 변화가 <b>아직 일어나지 않은</b> 상태에서 이뤄진다. 즉
        /// <b>스스로 만든 변화를 보기도 전에 "다 맞았다"고 선언</b>하는 구조다. 그 뒤 프레임 끝에
        /// 해상도/창 스타일이 바뀌어 창이 다시 어긋나도 되돌릴 주체가 없다 —
        /// Windows에서 창이 모니터 원점 + (11,45)에 눌러앉은 실기 상태가 정확히 이 모양이다
        /// (네이티브 <c>SetBorderless</c>가 프레임→보더리스 전환에서 창을 <b>옛 클라이언트 원점으로
        /// 옮기기</b> 때문이며, 그 이동이 우리 확정 이후에 일어나면 영구히 남는다).
        ///
        /// <para><b>처방</b>: 확정은 <b>우리가 아무것도 쓰지 않은 틱</b>에서만 한다. "안 건드렸는데도
        /// 맞더라"가 수렴의 정직한 정의다. 정상 경로의 비용은 <b>관측 틱 한 번</b>이고 그 틱은 정의상
        /// 쓰기가 0이므로 <b>OS 표면 재생성이 늘지 않는다</b>(불감대 안이라 대입 자체를 하지 않는다).</para>
        ///
        /// <para><b>불감대를 넓히는 것과 정반대의 처방이라는 점이 중요하다.</b> 불감대를 넓히면
        /// 45px 어긋남이 "맞았다"로 은폐된다. 이 규칙은 불감대를 그대로 두고 <b>확정 시점만</b>
        /// 늦춘다 — 어긋남은 여전히 어긋남으로 읽히고, 다음 틱이 그것을 고친다.</para>
        /// </summary>
        /// <param name="withinTolerance">되읽은 창 기하가 목표 불감대 안인가.</param>
        /// <param name="wroteThisTick">이번 틱에 <c>Screen.SetResolution</c>/크기/위치 중 하나라도
        /// 대입했는가. 하나라도 했다면 지금 읽은 값은 <b>아직 정착하지 않은 값</b>일 수 있다.</param>
        public static bool ShouldLatchFitApplied(bool withinTolerance, bool wroteThisTick)
            => withinTolerance && !wroteThisTick;

        /// <summary>
        /// 지금 <c>Screen.SetResolution</c>을 불러야 하는가.
        /// </summary>
        /// <param name="screenW">현재 <c>Screen.width</c>.</param>
        /// <param name="screenH">현재 <c>Screen.height</c>.</param>
        /// <param name="targetW">목표 픽셀 폭.</param>
        /// <param name="targetH">목표 픽셀 높이.</param>
        /// <param name="fullScreenModeIsWindowed">지금 창 모드인가.
        /// <b>false면 해상도가 맞아도 반드시 불러야 한다</b> — 전체화면 계열 모드로 남으면 Unity가
        /// 포커스를 잃을 때 창을 z-order 뒤로 보낸다(2026-09-01 신고 "창 뒤로 넘어감"의 원인 중 하나).</param>
        /// <param name="callsSoFar">이 프로세스에서 지금까지 부른 횟수.</param>
        /// <param name="maxCalls">프로세스 수명 상한. 24시간 상주 앱에서 이 호출은 <b>절대 무제한이면
        /// 안 된다</b> — 판정이 진동하면 사용자는 몇 초마다 수백 ms씩 얼어붙는 앱을 보게 된다.</param>
        public static bool ShouldSetResolution(int screenW, int screenH, int targetW, int targetH,
            bool fullScreenModeIsWindowed, float epsilonPixels, int callsSoFar, int maxCalls)
        {
            if (callsSoFar >= maxCalls) return false;
            if (!fullScreenModeIsWindowed) return true;
            return !Within(screenW, screenH, targetW, targetH, epsilonPixels);
        }

        private static float Abs(float v) => v < 0f ? -v : v;
    }
}
