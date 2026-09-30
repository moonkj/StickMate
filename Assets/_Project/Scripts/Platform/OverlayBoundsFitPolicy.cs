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

        /// <summary>
        /// ★★★ 2026-09-30 — <b>재적합 ↔ 보더리스 재적용 «핑퐁»을 끊는 규칙 ①</b>
        /// (이번 회차의 <c>Screen.SetResolution</c>을 부를 것인가).
        ///
        /// ============================================================================
        /// 확정된 기구 — 추측이 아니라 사용자가 보낸 Windows <c>Player.log</c>로 관측됐다
        /// ============================================================================
        /// 실기 로그에서 두 루프가 서로를 먹이고 있었다(오른쪽이 관측된 증거):
        /// <list type="number">
        ///   <item><c>전체화면 확장 시도</c>가 <c>Screen.SetResolution(..., Windowed)</c>을 부른다.</item>
        ///   <item>그 직후 <c>재적용</c> 틱의 OS 실측이 <c>GWL_STYLE</c>을
        ///         <b><c>0x94000000</c>(보더리스=True) → <c>0x14CA0000</c>(보더리스=False)</b>로 읽는다 —
        ///         <c>Screen.SetResolution</c>이 창 스타일을 되살린다. 이 인과는
        ///         <c>Platform/Windows/WindowsOverlayStateEnforcer</c>의 확정 블록 주석이
        ///         <b>이미 예견해 두었고</b>(「그 전환이 창 스타일을 되살리고 … 네이티브 SetBorderless가
        ///         다시 실행된다」) 이제 로그로 실증됐다.</item>
        ///   <item>재적용이 <see cref="TransparencyReapply.ReassignStyleMismatch"/>로 <b>비싼 경로</b>
        ///         (<c>SetBorderless</c> → <c>SetWindowPos</c> 4회 = 표면 재생성 4회)를 탄다.</item>
        ///   <item>그 <c>SetBorderless</c>가 창을 옛 클라이언트 원점으로 <b>옮긴다</b>
        ///         (실기 150% 배율에서 +(11,45)). 되돌릴 주체가 필요하므로 재적합이 <b>다시 무장</b>된다.</item>
        ///   <item>재무장된 재적합이 <c>Screen.SetResolution</c>을 또 부른다 → <b>1번으로 되돌아간다.</b></item>
        /// </list>
        /// 실측 로그에서 이 고리가 <b>재적용 5회 중 3회</b>(2·4·5번째)를 비싼 경로로 떨어뜨렸고,
        /// 기동 이후 <c>SetClickThrough(True)</c> 시점에 <b>한 번 더</b> 같은 형태가 났다.
        /// 사용자 실측은 「8초정도 흰화면깜박이 한 5번정도함」이다.
        ///
        /// ============================================================================
        /// 무엇을 끊는가 — <b>5번 화살표 하나만</b> 끊는다
        /// ============================================================================
        /// 고리의 다섯 화살표 중 우리가 소유한 것은 5번(재무장된 재적합이 다시
        /// <c>Screen.SetResolution</c>을 부르는 것)이다. 그리고 그 회차에서 <c>SetResolution</c>을 부를
        /// 이유는 <b>없다</b>: 디스플레이 구성은 바뀌지 않았고, 모니터도 그대로이고, 바뀐 것은
        /// <b>우리가 방금 부른 <c>SetBorderless</c>가 옮긴 창 사각형</b> 하나다. 창 사각형은
        /// <b>위치 대입</b>(<c>SetWindowPos</c> + <c>SWP_NOSIZE</c>, 클라이언트 영역 불변 = 표면 재생성 0회)
        /// 으로 되돌리면 되고, 그것은 창 스타일을 건드리지 않으므로 2번 화살표가 다시 서지 않는다.
        ///
        /// <para><b>양보하지 않는 것 — 창 모드 강제.</b> <paramref name="fullScreenModeIsWindowed"/>가
        /// 거짓이면 이 함수는 <b>반드시 참</b>을 돌려준다. 전체화면 계열 모드로 남으면 Unity가 포커스를
        /// 잃을 때 창을 z-order 뒤로 보내고, 그것이 2026-09-01 신고(엑셀 클릭 시 캐릭터가 창 뒤로 넘어감)
        /// 그 자체다. 즉 <b>억제되는 사유는 「해상도 불일치」 하나뿐</b>이다.</para>
        ///
        /// <para><b>상한을 늘리거나 없애지 않는다.</b> 판정의 첫 관문은 여전히
        /// <see cref="ShouldSetResolution"/>이고 <see cref="DefaultMaxSetResolutionCalls"/>가 그대로
        /// 최종 권한을 가진다 — 이 함수는 상한 <b>아래</b>에서 한 번 더 좁히기만 한다.</para>
        ///
        /// <para><b>정직한 한계 — 코드를 읽고 확인한 범위까지만 적는다</b>: 자기유발 회차에서 해상도
        /// 불일치를 고치지 않으므로, <c>SetBorderless</c>가 <b>클라이언트 영역 크기까지</b> 바꿔 놓은
        /// 환경에서는 <c>Screen.width/height</c>가 모니터와 어긋난 채 남을 수 있다. 그 상태가 <b>지속되면
        /// 좌표 배율이 그 비만큼 치우친다</b> — <c>ScreenCoordinateConverter.AutoDpiScale</c>은
        /// <c>창 사각형 폭 ÷ Screen.width</c>이므로, 모니터 3840에서 프레임 두께만큼(약 22px) 어긋나면
        /// 배율이 1.000 대신 약 1.006이 된다. 즉 「창 폭 == 모니터 폭」을 <b>가정하지는</b> 않지만
        /// <b>영향을 받지 않는 것도 아니다</b>(원래 이 자리에 「영향 없음」처럼 읽히는 문장을 적었고,
        /// 변환기 코드를 직접 읽어 정정했다).</para>
        ///
        /// <para>그럼에도 이쪽을 택한 근거는 <b>지속되지 않는다</b>는 것이다: 그 어긋남을 만든 프레임
        /// 스타일은 같은 고리의 <c>SetBorderless</c>가 바로 없애고, 그러면 클라이언트 영역이 창 사각형과
        /// 다시 같아진다. 반대쪽 거래는 <b>표면 재생성 4회 × 수백 ms를 매 회차</b> 지불하는 것이다.
        /// 그리고 다음 디스플레이 구성 변경 한 번이 자기유발 표시를 지우고 완전한 권한을 되돌려 준다.
        /// ★ 이 배율 치우침이 실기에서 실제로 관측된 적은 <b>없다</b> — 기구상 가능하다는 것까지가
        /// 확인된 범위다.</para>
        /// </summary>
        /// <param name="selfInducedRefit">
        /// 이번 재적합 에피소드가 <b>우리 자신의 <c>SetBorderless</c>가 옮긴 창</b>을 되돌리려고 무장된
        /// 것인가. 외부 사건(디스플레이 구성 변경 · 표시 모니터 변경 · 기동 첫 적합)으로 무장된
        /// 에피소드는 거짓이어야 한다 — 그때는 해상도가 진짜로 바뀌었을 수 있다.
        /// </param>
        public static bool ShouldSetResolutionForFitAttempt(
            int screenW, int screenH, int targetW, int targetH,
            bool fullScreenModeIsWindowed, float epsilonPixels, int callsSoFar, int maxCalls,
            bool selfInducedRefit)
        {
            // 상한·불감대·모드 판정은 한 곳(ShouldSetResolution)에만 있다. 여기서 다시 쓰지 않는다.
            if (!ShouldSetResolution(screenW, screenH, targetW, targetH,
                    fullScreenModeIsWindowed, epsilonPixels, callsSoFar, maxCalls))
            {
                return false;
            }

            // 창 모드 강등 복구는 절대 양보하지 않는다(2026-09-01 "창 뒤로 넘어감").
            if (!fullScreenModeIsWindowed) return true;

            // 남은 사유는 「해상도 불일치」 하나뿐이고, 자기유발 재적합에서는 그것이 핑퐁의 되먹임 고리다.
            return !selfInducedRefit;
        }

        /// <summary>
        /// ★★★ 2026-09-30 — <b>핑퐁을 끊는 규칙 ②</b>(우리 <c>SetBorderless</c> 뒤에 재적합을 무장할 것인가).
        ///
        /// <para>재무장의 <b>유일한 목적</b>은 네이티브 <c>SetBorderless</c>가 창을 옮긴 것을 되돌리는
        /// 것이다(위 규칙 ① 4번 화살표). 그러므로 <b>창이 이미 목표 사각형 안에 있으면 되돌릴 것이
        /// 없고, 무장은 순수한 낭비 이상이다</b> — 무장 한 번이 재적합 에피소드 한 번이고, 그 에피소드가
        /// 다시 스타일을 되살리면 비싼 경로 4회 재생성이 또 붙는다.</para>
        ///
        /// <para>이 규칙이 실제로 잡는 회차: <c>SetBorderless</c>가 <b>이미 보더리스인 창</b>에 다시
        /// 불리는 경우다(스타일 실측 실패 · 유리 전용 경로 사용 불가 — <see cref="TransparencyReapply"/>의
        /// <c>ReassignStyleUnreadable</c>·<c>ReassignGlassPathUnavailable</c>). 그때 네이티브가 하는 일은
        /// 폭 ±1 흔들기뿐이고 그 1px은 <see cref="DefaultEpsilonPixels"/> 안이므로 창은 목표 안에 남는다.
        /// 지금까지는 그 회차도 무조건 재무장해 전체 재적합 에피소드를 한 번 더 돌렸다.</para>
        ///
        /// <para><b>진짜 이동은 여전히 무장한다</b>(프레임→보더리스 전환의 +(11,45)는 불감대 2px를 한참
        /// 넘는다). 그리고 <b>목표를 모르면 무장한다</b>(<paramref name="latchedTargetKnown"/> 거짓) —
        /// 「모를 때는 고치는 쪽」이 이 파일과 <c>OverlayStateReapplyPolicy</c>가 일관되게 택해 온 태도이고,
        /// 그 경우 동작은 이 규칙이 생기기 전과 <b>글자 그대로 같다</b>.</para>
        /// </summary>
        /// <param name="latchedTargetKnown">적합이 한 번이라도 확정돼 목표 사각형을 알고 있는가.</param>
        /// <param name="geometryWithinLatchedTarget">
        /// <c>SetBorderless</c> 직후 <b>OS에서 되읽은</b> 창 위치·크기가 그 목표 사각형의 불감대 안인가.
        /// </param>
        public static bool ShouldReArmFitAfterSelfInducedStyleWrite(
            bool latchedTargetKnown, bool geometryWithinLatchedTarget)
        {
            if (!latchedTargetKnown) return true;
            return !geometryWithinLatchedTarget;
        }

        /// <summary>
        /// ★ 2026-09-30 — <b>수명 상한에 닿았다는 사실을 사용자에게 한 번 알릴 것인가.</b>
        ///
        /// ============================================================================
        /// 왜 생겼나 — 상한은 정당한데 «닿은 뒤»에 복구 경로가 없다
        /// ============================================================================
        /// <see cref="DefaultMaxSetResolutionCalls"/>는 진동 루프를 막는 정당한 안전장치이고
        /// <b>여기서 올리거나 없애지 않는다</b>. 그런데 이 카운터는 <b>프로세스 수명 동안 한 번도
        /// 초기화되지 않는다</b>(재무장은 시도 횟수만 되돌리고 이 값은 절대 되돌리지 않는다 — 두 Enforcer의
        /// 재무장 메서드 문서가 그렇게 못박고 있다). 모니터를 한 번 빼고 다시 꽂으면 이미 2~3회를 쓰고,
        /// 다 쓰면 <b>그 세션 안에서는 창을 다시 맞출 수 없으며 재시작 말고 복구 경로가 없다.</b>
        ///
        /// <para>그때까지 남는 것은 <c>Player.log</c> 한 줄(「★상한 도달」)뿐이었다 — 사용자는 로그를 읽지
        /// 않으므로 <b>「창이 화면에 안 맞는데 이유를 모르는 상태」</b>가 된다. 그래서 상한에 닿는 순간
        /// <b>알림 한 번</b>을 낸다. 이것은 <b>복구 시도가 아니다</b> — 창 위치·크기·스타일·스왑체인을
        /// 한 비트도 건드리지 않고, 사용자에게 상황과 해법(재시작)만 알린다.</para>
        ///
        /// <para><b>왜 정책이 여기 있는가</b>: 판정을 <c>Platform/Windows/</c> 안에 두면 macOS가 물리적으로
        /// 호출할 수 없다(<c>FullscreenSuspendPolicy</c> 사고와 같은 형태). macOS Enforcer도 같은 상한을
        /// 같은 방식으로 쓰므로 알림 배선이 그 플랫폼으로 확장될 때 <b>이 함수를 그대로</b> 부른다.</para>
        ///
        /// <para><b>한 번만</b>이 계약이다(<paramref name="alreadyNoticed"/>). 24시간 상주 앱에서 같은 알림을
        /// 반복하면 그 자체가 방해다(원칙 2).</para>
        /// </summary>
        /// <param name="callsSoFar">지금까지 부른 횟수(증가 <b>뒤</b>의 값).</param>
        /// <param name="maxCalls">수명 상한. 0 이하면 상한 개념이 없는 것으로 보고 알리지 않는다.</param>
        /// <param name="alreadyNoticed">이 프로세스에서 이미 알렸는가.</param>
        public static bool ShouldNoticeSetResolutionCapReached(int callsSoFar, int maxCalls, bool alreadyNoticed)
        {
            if (alreadyNoticed) return false;
            if (maxCalls <= 0) return false;
            return callsSoFar >= maxCalls;
        }

        private static float Abs(float v) => v < 0f ? -v : v;
    }

    /// <summary>
    /// ★ 2026-09-14 (verify-change 2차 X2c) — 전체화면 적합 <b>확정 판정과 확정 통지를 한 몸으로</b> 묶는다.
    ///
    /// <para><b>왜 생겼나.</b> 화면 변경 유예(<see cref="DisplayChangeRenderHold"/>)는 첫 전체화면 적합이 확정된 <b>뒤</b>에만
    /// 무장해야 한다 — UniWinC는 창을 붙잡는 순간에도 <c>OnMonitorChanged</c>를 스스로 올리고
    /// (<c>UniWindowController.UpdateTargetWindow</c>), 기동 적합을 늦추면 기동 흰 배경 구간이 길어진다. 2차 구현은 두
    /// Enforcer가 확정 블록 안에서 <c>Arm()</c>을 부르게 했는데, 그 한 줄을 매 프레임 틱으로 옮겨도(=기동 첫 프레임부터
    /// 무장) 두 플랫폼 전량이 초록이었다 — 감사가 호출 문자열의 <b>존재</b>만 봤기 때문이다.</para>
    ///
    /// <para><b>구조로 잠근다.</b> 이제 <c>Arm()</c>은 어디에도 없다. 무장의 유일한 원천은 이 객체의 <see cref="Latched"/>이고,
    /// 그것을 올리는 유일한 길은 <see cref="Evaluate"/>에 <b>되읽은 기하 판정</b>과 <b>이번 틱 쓰기 여부</b>를 넘겨
    /// <see cref="OverlayBoundsFitPolicy.ShouldLatchFitApplied"/>가 참을 내는 것이다. 두 값은 적합 틱의 지역 변수라 유예 틱
    /// 자리에는 존재하지 않는다 — 무장을 옮기려면 확정 판정 자체를 옮겨야 하고, 그러면 적합 확정도 함께 틀어진다.</para>
    ///
    /// <para>메인 스레드 전용. UnityEngine 의존 없음(EditMode가 실행해 잠근다 — <c>DisplayChangeRenderHoldTests</c>).</para>
    /// </summary>
    public sealed class FullScreenFitLatchSignal
    {
        /// <summary>확정이 한 번이라도 섰는가. 디스플레이 변경으로 재적합이 다시 열려도 참으로 남는다.</summary>
        public bool HasLatchedOnce { get; private set; }

        /// <summary>확정이 선 횟수(진단용).</summary>
        public int LatchCount { get; private set; }

        /// <summary>확정이 설 때마다 올린다. 구독자가 던져도 확정 판정은 그대로 돌려준다(무장 실패가 적합을 되돌리지 않는다).</summary>
        public event System.Action Latched;

        /// <summary>
        /// 이번 틱의 확정 판정. 규칙은 <see cref="OverlayBoundsFitPolicy.ShouldLatchFitApplied"/> 그대로이고,
        /// 참이면 <see cref="Latched"/>를 올린 뒤 참을 돌려준다.
        /// </summary>
        public bool Evaluate(bool withinTolerance, bool wroteThisTick)
        {
            if (!OverlayBoundsFitPolicy.ShouldLatchFitApplied(withinTolerance, wroteThisTick)) return false;
            HasLatchedOnce = true;
            LatchCount++;
            System.Action handlers = Latched;
            if (handlers != null)
            {
                try { handlers(); }
                catch (System.Exception) { /* 무장 실패가 적합 확정을 되돌리지 않는다. */ }
            }
            return true;
        }
    }
}
