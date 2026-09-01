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
