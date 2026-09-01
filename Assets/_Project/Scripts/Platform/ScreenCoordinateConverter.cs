using UnityEngine;
using StickMate.Core;

namespace StickMate.Platform
{
    /// <summary>
    /// Unity 씬 좌표계와 OS 데스크톱 좌표계 사이의 변환을 한 곳에 모아두는 유틸리티.
    /// (Debugger BUG-M5 대응: "각 상태가 개별 구현하면 좌표계 혼용 버그 위험" — 모든 소비자는
    /// 반드시 이 유틸만 거쳐야 하며 직접 Screen.height/DPI 계산식을 다시 쓰지 않는다.)
    ///
    /// 왜 변환이 필요한가 — 좌표계가 3중으로 다르다:
    /// 1) Unity 월드 좌표: 게임플레이가 쓰는 좌표(유닛), 카메라/오브젝트 배치 기준.
    /// 2) Unity 스크린 좌표: Camera.WorldToScreenPoint()의 결과. 원점이 "좌하단"이고 y가 위로 갈수록
    ///    증가하며, 단위는 Unity가 보고하는 픽셀("points" — 고DPI 화면에서 실제 백킹 픽셀과
    ///    다를 수 있음, 아래 DPI 설명 참고).
    /// 3) OS 데스크톱 좌표: PlatformFoothold.ScreenRect가 쓰는 좌표계(Platform/IPlatformWindowService.cs
    ///    주석 참고). 원점이 "좌상단"이고 y가 아래로 갈수록 증가하며, Win32 GetWindowRect 등 OS API가
    ///    보고하는 실제 화면 픽셀 단위.
    ///
    /// 변환 절차 (Unity 월드 -> OS 데스크톱):
    ///   a. Camera.WorldToScreenPoint로 Unity 스크린 좌표(좌하단 원점) 획득.
    ///   b. y를 Screen.height 기준으로 뒤집어 좌상단 원점으로 전환: osY = Screen.height - unityY.
    ///   c. DPI 배율(<see cref="ResolveDpiScale"/>)을 곱해 "Unity가 보고하는 픽셀 단위" ↔ "OS가 보고하는
    ///      실제 데스크톱 포인트 단위" 배율 차이를 보정한다. 예: macOS Retina에서 Unity가 백킹 스토어
    ///      픽셀(3024x1964)을 보고하지만 CGWindowListCopyWindowInfo/CGEventGetLocation은 포인트
    ///      (1512x982)를 반환하는 경우, 또는 Windows에서 프로세스 DPI 인식 설정에 따라 GetWindowRect가
    ///      물리/논리 픽셀 중 무엇을 반환하는지 달라지는 경우.
    ///
    /// 왕복 정밀도: 카메라가 직교(2D) 투영이더라도 Camera.ScreenToWorldPoint는 세 번째 인자를
    /// "카메라로부터의 거리"로 해석한다. WorldToOsScreen이 반환하는 cameraDepth를 OsScreenToWorld
    /// 호출 시 그대로 재사용해야 같은 z 평면으로 정확히 역변환된다(임의의 world z 값을 넣지 말 것).
    ///
    /// Phase 1 알려진 한계 (문서화 목적 — 임의 확장 금지, Tasklist.md 교차 레이어 로그 참고):
    /// - 오버레이가 OS 가상 데스크톱의 (0,0)에서 시작해 화면 전체를 덮는다고 가정한다. 실제 멀티모니터
    ///   배치(오프셋/다른 해상도)에 따른 보정은 IPlatformWindowService가 모니터 경계를 노출해야
    ///   가능한데, 이는 Phase 0 교차 레이어 로그 9절-5 항목으로 아직 미반영 상태다.
    /// - DPI 배율은 화면 전체에 대해 단일 값이다. 모니터마다 DPI가 다른 환경은 Phase 4 정교화 대상
    ///   (단, 오버레이 창이 실제로 놓인 화면 기준으로 매 폴링마다 재측정되므로 모니터를 옮기면 자동 추종한다).
    /// </summary>
    public static class ScreenCoordinateConverter
    {
        // ============================================================================
        // ★★ DPI 배율의 **단일 소스** (2026-08-29 Retina 대응 라운드, 리더 지시 2항)
        // ============================================================================
        // 배경: ProjectSettings의 `macRetinaSupport`가 0에서 1로 바뀌면서 Unity의 Screen.width/height와
        // WorldToScreenPoint가 **물리 백킹 픽셀**(3024x1964)을 보고하게 됐다. 반면 이 앱이 상대하는 OS
        // 좌표(CGWindowListCopyWindowInfo의 창 사각형 / CGEventGetLocation의 커서 / CGDisplayBounds)는
        // 전부 **AppKit 포인트**(1512x982)다. 두 단위 사이의 배율이 곧 아래 DpiScale이다.
        //
        //     OS 포인트 = Unity 픽셀 x DpiScale        (Retina 2x -> 0.5, 비Retina -> 1.0)
        //
        // 왜 하드코딩(0.5)하면 안 되는가: 외장 모니터(비Retina)를 물리면 그 화면에서는 1.0이고, 사용자가
        // 창을 모니터 사이로 옮기면 실행 중에 바뀐다. 그래서 **실측**한다 — 우리 창의 OS 포인트 폭을
        // 같은 순간의 Screen.width(Unity 픽셀)로 나눈 값이 정확히 그 배율이다(창 크기와 무관하게 성립하는
        // 비율이며, 창이 실제로 놓인 화면의 배율을 자동으로 반영한다).
        //
        // 왜 여기(이 클래스)인가: BUG-M5 컨벤션("좌표 변환식은 이 유틸에만 존재한다")의 연장이다. 예전에는
        // 소비자들이 각자 `config.desktopDpiScale`을 직접 읽어 `Screen.width * dpi` 식을 다시 썼고, 그
        // 필드는 "실측된 적 없는 근사치 1"이었다. 이제 값의 생산(ReportOverlayWindowOsRect)과 해석
        // (ResolveDpiScale)이 모두 이 클래스에 있고, 소비자는 결과만 받아 쓴다.

        /// <summary>
        /// 플랫폼 계층이 실측해 보고한 자동 배율. <see cref="ReportOverlayWindowOsRect"/>가 갱신한다.
        /// 아무도 보고하지 않은 환경(에디터/헤드리스/Windows 스텁)에서는 1(배율 차이 없음)로 남아
        /// 예전과 완전히 동일하게 동작한다.
        ///
        /// setter가 public인 이유: 테스트가 "배율 2인 척"을 만들 유일한 수단이고(Tests/PlayMode/
        /// RetinaDpiCoordinateTests.cs), 플랫폼 계층이 창 사각형 없이 디스플레이 배율만 아는 폴백
        /// 경로에서도 대입해야 하기 때문이다. 0 이하/NaN/무한대는 조용히 무시한다 — 잘못된 배율을
        /// 받아들이는 것보다 직전 값을 유지하는 편이 안전하다(0을 받아들이면 나눗셈이 폭발한다).
        /// </summary>
        public static float AutoDpiScale
        {
            get { return _autoDpiScale; }
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f) return;
                _autoDpiScale = value;
            }
        }
        private static float _autoDpiScale = 1f;

        /// <summary>
        /// 실제로 쓸 배율을 결정하는 **유일한** 함수. 모든 소비자는 `config.desktopDpiScale`을 직접 읽지
        /// 말고 이 함수를 부른다.
        ///
        /// 규칙(StickConfig.desktopDpiScale의 툴팁과 동일):
        ///   · config.desktopDpiScale &gt; 0  -> 사람이 지정한 **수동 오버라이드**. 그 값을 그대로 쓴다.
        ///   · 그 외(0 이하 / config 없음)   -> <see cref="AutoDpiScale"/>(실측 자동 산출값).
        /// </summary>
        public static float ResolveDpiScale(StickConfig config)
        {
            if (config != null && config.desktopDpiScale > 0f) return config.desktopDpiScale;
            return _autoDpiScale;
        }

        /// <summary>
        /// 플랫폼 계층이 "우리 오버레이 창의 OS 사각형(포인트)"을 보고하는 단일 진입점.
        /// <see cref="OverlayOriginOsScreen"/>(원점)과 <see cref="AutoDpiScale"/>(배율)을 **같은 순간의
        /// 한 관측**에서 함께 유도하므로 둘이 서로 다른 시점의 값으로 어긋날 수 없다.
        ///
        /// 배율을 여기서 스냅샷하는 이유(중요): Screen.width는 실행 중에 바뀔 수 있다
        /// (MacOverlayStateEnforcer.TickFullScreenBounds()가 Screen.SetResolution으로 창을 화면 전체로
        /// 넓힌다). 폭과 Screen.width를 각각 다른 시점에 읽어 나중에 나누면 그 전환 프레임에서 배율이
        /// 순간적으로 2배 틀린 값이 되어 캐릭터가 화면 밖으로 튄다. 그래서 비율은 **관측 순간에** 계산해
        /// 저장한다.
        /// </summary>
        public static void ReportOverlayWindowOsRect(Rect overlayRectOsPoints)
        {
            ReportOverlayWindowOsRect(overlayRectOsPoints, default, false);
        }

        /// <summary>
        /// 위와 같되 <b>이번 관측과 같은 순간의 데스크톱 경계</b>를 함께 받는 오버로드.
        /// 경계를 알면 아래 위생 검사(<see cref="IsOverlayRectPlausible"/>)가 "명백히 화면 밖" 보고를
        /// 걸러낼 수 있다. macOS는 CGDisplayBounds(주 디스플레이), Windows는 SM_*VIRTUALSCREEN(모든
        /// 모니터의 외접 사각형)을 넘긴다 — 둘 다 이미 같은 열거 패스에서 조회하던 값이라
        /// <b>추가 시스템 호출이 0건</b>이다.
        /// </summary>
        public static void ReportOverlayWindowOsRect(Rect overlayRectOsPoints, Rect desktopBoundsOsPoints)
        {
            ReportOverlayWindowOsRect(overlayRectOsPoints, desktopBoundsOsPoints, true);
        }

        private static void ReportOverlayWindowOsRect(Rect overlayRectOsPoints, Rect desktopBoundsOsPoints, bool hasDesktopBounds)
        {
            if (!IsOverlayRectPlausible(overlayRectOsPoints, desktopBoundsOsPoints, hasDesktopBounds, out string reason))
            {
                RejectedOverlayRectCount++;
                LastRejectedOverlayRectReason = reason;
                // 직전 유효값을 그대로 유지한다(원점도 배율도 건드리지 않는다).
                return;
            }

            OverlayOriginOsScreen = overlayRectOsPoints.position;
            if (overlayRectOsPoints.width > 0f && Screen.width > 0)
            {
                AutoDpiScale = overlayRectOsPoints.width / Screen.width;
            }
        }

        // ============================================================================
        // ★★ 오버레이 원점 위생 검사 (2026-09-01 — 신고 "창에서 가끔 갑자기 떨어짐"의 근본 원인 3)
        // ============================================================================
        // 실측 증거(디버거, Player.log.prevround): 원점이
        //     (0,0) -> (0,-805) -> (0,-936) -> (0,-937) -> (0,-78) -> (0,0)
        // 으로 한 차례 요동친 직후 [발판상실]이 발생했다. 화면 높이는 982pt이므로 -936은 창의
        // 95%가 화면 위로 빠져나간 값이다 — 실제로 그런 창은 존재할 수 없고, 창 애니메이션 도중의
        // 일시적 오독이다. 원점이 틀리면 WorldToOsScreen이 통째로 틀어져 "발 OS y"와 "발판 상단 y"의
        // 비교가 무너진다 = 창은 그대로인데 접지가 풀린다.
        //
        // ★ 이 검사만으로 위 시퀀스 전부를 잡지는 못한다(정직한 한계):
        //   -805(18% 남음) / -936(4.7% 남음)은 걸리지만 -78(92% 남음)은 "명백히 밖"이 아니라 통과한다.
        //   -78처럼 완만한 한 번의 튐은 **근본 원인 2의 처방**이 흡수한다 — 유예(폴링 간격 x 1.5 = 0.45초)가
        //   나쁜 원점의 수명(다음 폴링까지 최대 0.3초)보다 길고, 그 유예 동안 몸이 중력 억제로 **제자리에
        //   붙잡혀 있어서**(StickmanBlackboard.GroundedTick의 _graceHoldFrame) 튐이 지나간 뒤 그대로 다시
        //   접지된다. 유예만 늘리고 몸을 놔두면 0.3초 자유낙하가 1.32유닛이라 허용오차(0.489)도 스냅
        //   상한(0.6)도 넘어 되돌아올 수 없다 — 두 처방은 함께여야 작동한다.
        //   세 처방은 서로 다른 층에서 같은 증상을 막는다.
        //
        // ★ 왜 "무조건 거부"가 아니라 "연속 확인"인가 — 영구 고착이 더 위험하기 때문이다.
        //   macOS가 넘겨주는 것은 **주 디스플레이** 경계라, 사용자가 앱을 보조 모니터로 옮기면
        //   정상 창인데도 이 검사에 걸린다. 그래서 거부는 **잠정적**이다: 같은 사각형이
        //   OffDesktopConfirmReports회 연속으로 보고되면 실제 이동으로 인정하고 받아들인다.
        //   창 애니메이션 중의 오독은 매 표본이 다른 값이라 이 카운터를 채우지 못한다(= 계속 거부).

        /// <summary>창 넓이의 이만큼이 데스크톱 안에 남아 있어야 "그럴듯한 보고"로 본다.
        /// 절반은 "명백히 화면 밖"의 보수적 해석이다 — 실측 오독(4.7%/18% 잔존)은 걸러내고,
        /// 창을 화면 밖으로 절반쯤 끌어다 놓는 정상 사용은 통과시킨다.</summary>
        private const float MinOnDesktopAreaFraction = 0.5f;

        /// <summary>위 문단의 "연속 확인" 횟수. 폴링 주기가 0.3초이므로 2회 = 약 0.6초 안에 스스로 풀린다.</summary>
        private const int OffDesktopConfirmReports = 2;

        private static Vector4 _lastOffDesktopRect;
        private static int _offDesktopRepeatCount;

        /// <summary>위생 검사로 버린 보고의 누적 횟수(진단/테스트용).</summary>
        public static int RejectedOverlayRectCount { get; private set; }

        /// <summary>마지막으로 버린 보고의 사유(진단/테스트용). 버린 적이 없으면 빈 문자열.</summary>
        public static string LastRejectedOverlayRectReason { get; private set; } = string.Empty;

        /// <summary>
        /// 플랫폼 계층이 매 폴링 직전에 스위치 상태를 밀어 넣는 통로(Platform/FootholdPoller).
        /// StickConfig를 여기서 직접 읽지 않는 이유는 이 클래스가 순수 static 유틸이기 때문이다 —
        /// 설정 의존을 늘리는 대신, 이미 설정을 들고 있고 보고 직전에 반드시 도는 한 곳에서 밀어준다.
        /// </summary>
        public static bool OverlayOriginSanityCheckEnabled { get; set; } = true;

        /// <summary>테스트가 누적 카운터/연속 확인 상태를 초기화하는 통로(플랫폼 계층은 쓰지 않는다).</summary>
        public static void ResetOverlayRectSanityState()
        {
            RejectedOverlayRectCount = 0;
            LastRejectedOverlayRectReason = string.Empty;
            _offDesktopRepeatCount = 0;
            _lastOffDesktopRect = Vector4.zero;
        }

        /// <summary>
        /// 이 오버레이 사각형 보고를 받아들여도 되는가(위 섹션 문서 참고). 순수 판정 + 연속 확인 부기만
        /// 하고 좌표계는 건드리지 않는다.
        /// </summary>
        public static bool IsOverlayRectPlausible(Rect rect, Rect desktopBounds, bool hasDesktopBounds, out string reason)
        {
            reason = string.Empty;

            // (0) 숫자 자체가 망가진 보고. 스위치와 무관하게 언제나 거부한다 — NaN이 좌표계에 들어가면
            //     그 뒤 모든 변환이 NaN이 되어 캐릭터가 영원히 사라진다(복구 경로 없음).
            if (float.IsNaN(rect.x) || float.IsNaN(rect.y) || float.IsNaN(rect.width) || float.IsNaN(rect.height)
                || float.IsInfinity(rect.x) || float.IsInfinity(rect.y)
                || float.IsInfinity(rect.width) || float.IsInfinity(rect.height))
            {
                reason = $"좌표에 NaN/무한대가 섞였습니다 — rect={rect}";
                return false;
            }
            if (rect.width <= 0f || rect.height <= 0f)
            {
                reason = $"창 크기가 0 이하입니다 — rect={rect}";
                return false;
            }

            if (!OverlayOriginSanityCheckEnabled) return true;
            if (!hasDesktopBounds || desktopBounds.width <= 0f || desktopBounds.height <= 0f) return true;

            float overlapW = Mathf.Min(rect.xMax, desktopBounds.xMax) - Mathf.Max(rect.xMin, desktopBounds.xMin);
            float overlapH = Mathf.Min(rect.yMax, desktopBounds.yMax) - Mathf.Max(rect.yMin, desktopBounds.yMin);
            float onDesktopArea = Mathf.Max(0f, overlapW) * Mathf.Max(0f, overlapH);
            float rectArea = rect.width * rect.height;
            float onDesktopFraction = rectArea > 0f ? onDesktopArea / rectArea : 0f;

            if (onDesktopFraction >= MinOnDesktopAreaFraction)
            {
                _offDesktopRepeatCount = 0;
                return true;
            }

            // 같은 사각형이 연속으로 다시 오면 실제 이동으로 인정한다(영구 고착 방지, 위 문서 참고).
            var key = new Vector4(rect.x, rect.y, rect.width, rect.height);
            _offDesktopRepeatCount = key == _lastOffDesktopRect ? _offDesktopRepeatCount + 1 : 1;
            _lastOffDesktopRect = key;
            if (_offDesktopRepeatCount >= OffDesktopConfirmReports)
            {
                _offDesktopRepeatCount = 0;
                return true;
            }

            reason = $"창의 {(onDesktopFraction * 100f):F1}%만 데스크톱 안에 있습니다" +
                $"(최소 {(MinOnDesktopAreaFraction * 100f):F0}%) — rect={rect}, 데스크톱={desktopBounds}. " +
                $"직전 유효 원점 {OverlayOriginOsScreen}을 유지합니다. " +
                $"같은 값이 {OffDesktopConfirmReports}회 연속으로 오면 실제 이동으로 인정합니다" +
                $"(현재 {_offDesktopRepeatCount}회).";
            return false;
        }

        // ============================================================================
        // ★★ UI 밀도(캔버스 배율)를 좌표 배율에서 **분리**한 이유 — 2026-08-31 Windows 신고
        //    "캐릭터창 해상도도 엄청 낮아서 글씨도 잘 안보임"
        // ============================================================================
        // 위 AutoDpiScale은 "창 사각형(OS 단위) / Screen.width(Unity 픽셀)"이다. 이 비에 디스플레이
        // 배율이 실려 오는 것은 **macOS에서만** 참이다(창 사각형이 AppKit 포인트, Screen이 백킹 픽셀).
        // Windows에서는 GetWindowRect도 Screen.width도 둘 다 물리 픽셀이라 이 비가 **항상 1.0**이고,
        // 디스플레이 배율(125%/150%)이 어디에도 실리지 않는다 -> 캔버스 배율 1 -> 논리 포인트로 맞춰 둔
        // 모든 UI 상수가 물리 픽셀 크기로 그려져 실제보다 1/1.25~1/1.5로 쪼그라든다.
        //
        // 그래서 두 개념을 이름부터 분리한다:
        //   · AutoDpiScale       — **좌표** 변환용. "OS 좌표 1 = Unity 픽셀 몇 개인가"의 역수.
        //                          Windows에서 1.0인 것이 **맞다**(창 좌표와 커서 좌표가 같은 단위다).
        //   · AutoUiDensityScale — **UI 크기** 전용. "논리 포인트 1개 = 물리 픽셀 몇 개인가".
        //                          Windows는 GetDpiForWindow/96으로 OS에서 직접 읽어 보고한다.
        //
        // 아무도 보고하지 않으면(macOS/에디터/헤드리스/모바일) 예전 정의 `1 / AutoDpiScale`이 그대로
        // 쓰인다 — 즉 **기존 플랫폼의 동작은 한 글자도 바뀌지 않는다**.

        /// <summary>
        /// 플랫폼 계층이 보고한 UI 밀도(논리 포인트 1개당 물리 픽셀 수). 0 이하면 "미보고"이며
        /// <see cref="ResolveCanvasScaleFactor"/>가 예전 정의(<c>1 / AutoDpiScale</c>)로 되돌아간다.
        /// </summary>
        public static float AutoUiDensityScale { get; private set; } = 0f;

        /// <summary>
        /// 플랫폼 계층이 UI 밀도를 보고하는 단일 진입점(<see cref="ReportOverlayWindowOsRect"/>와 같은 관례).
        /// 0 이하/NaN/무한대는 조용히 무시한다 — 잘못된 배율을 받아들이는 것보다 직전 값을 유지하는
        /// 편이 안전하다(UI 전체가 한 프레임 만에 화면 밖으로 날아가는 사고를 막는다).
        /// </summary>
        public static void ReportUiDensityScale(float physicalPixelsPerPoint)
        {
            if (float.IsNaN(physicalPixelsPerPoint) || float.IsInfinity(physicalPixelsPerPoint)) return;
            if (physicalPixelsPerPoint <= 0f) return;
            AutoUiDensityScale = physicalPixelsPerPoint;
        }

        /// <summary>테스트가 "밀도 미보고 상태"로 되돌리기 위한 통로(플랫폼 계층은 쓰지 않는다).</summary>
        public static void ClearReportedUiDensity() => AutoUiDensityScale = 0f;

        /// <summary>
        /// ScreenSpaceOverlay 캔버스(<c>CanvasScaler.scaleFactor</c>)에 넣을 값 = <b>Unity 픽셀 / 논리 포인트</b>.
        /// Retina 2x면 2, 비Retina면 1, Windows 디스플레이 배율 150%면 1.5다.
        ///
        /// 우선순위:
        ///   1. <c>config.desktopDpiScale &gt; 0</c> — 사람이 지정한 수동 오버라이드(예전과 동일하게 그 역수).
        ///   2. <see cref="AutoUiDensityScale"/> — 플랫폼이 OS에서 직접 읽어 보고한 값(Windows).
        ///   3. <c>1 / AutoDpiScale</c> — 창 사각형 대 Screen 비에서 유도한 예전 값(macOS 등).
        ///
        /// 왜 이 값인가 — 이 프로젝트의 UI 상수(말풍선 폰트 크기/여백, 앱제어 메뉴 행 높이, 투두 카드 폭)는
        /// 전부 **macOS 포인트 기준으로 눈으로 맞춰진 값**이다(Dialogue/DialogueBubbleRenderer.cs 상단
        /// "값은 전부 Unity 스크린 픽셀(= macOS 포인트, Screen.height≈846 기준)" 참고). scaleFactor를 이
        /// 배율로 두면 **캔버스 1유닛 == OS 포인트 1**이 되어, Retina를 켜기 전과 UI의 물리적 크기가
        /// 정확히 같으면서 렌더 해상도만 2배가 된다(= 같은 크기, 더 선명). scaleFactor를 1로 방치하면
        /// 캔버스 1유닛 == 물리 픽셀 1이 되어 모든 UI가 물리적으로 절반 크기로 쪼그라든다 — 이것이
        /// 리더 지시 5항이 경고한 함정이다. "가독성 하한" 걱정은 이 정의에서는 발생하지 않는다:
        /// 글자의 물리적 크기가 변하지 않기 때문이다.
        /// </summary>
        public static float ResolveCanvasScaleFactor(StickConfig config)
        {
            if (config != null && config.desktopDpiScale > 0f) return 1f / config.desktopDpiScale;
            if (AutoUiDensityScale > 0f) return AutoUiDensityScale;
            return _autoDpiScale > 0f ? 1f / _autoDpiScale : 1f;
        }

        /// <summary>
        /// Unity 스크린 픽셀 좌표(WorldToScreenPoint / Screen.width 등) -> ScreenSpaceOverlay 캔버스 유닛.
        /// <see cref="ResolveCanvasScaleFactor"/>로 스케일된 캔버스에 <c>anchoredPosition</c>을 대입하는
        /// 코드는 반드시 이 변환을 거쳐야 한다(안 거치면 Retina에서 UI가 화면 우상단 밖으로 날아간다).
        ///
        /// 주의 — 반대 방향은 필요 없는 경우가 많다: ScreenSpaceOverlay 캔버스에서 RectTransform의
        /// <c>GetWorldCorners</c>는 캔버스 루트의 localScale(=scaleFactor)이 이미 곱해진 **스크린 픽셀**을
        /// 돌려준다. 그래서 히트테스트(AppControlDirector.HitTestMenuRow / TodoPostItWidget.ContainsScreenPoint)와
        /// 클릭관통 차단막(Camera.ScreenToWorldPoint)은 scaleFactor와 무관하게 예전 코드 그대로 정확하다.
        ///
        /// <para>★ 2026-08-31: 식을 <see cref="ResolveCanvasScaleFactor"/>로 다시 표현했다. 배율이 예전
        /// 정의(<c>1 / ResolveDpiScale</c>)일 때는 <c>v * dpi == v / (1/dpi)</c>로 <b>완전히 같은 값</b>이고,
        /// UI 밀도가 따로 보고된 환경(Windows)에서만 캔버스 배율을 따라간다 — 이 함수의 정의가
        /// "캔버스 배율의 역"이어야 배치와 크기가 갈라지지 않기 때문이다.</para>
        /// </summary>
        public static float UnityScreenToCanvas(float unityScreenValue, StickConfig config)
        {
            float scale = ResolveCanvasScaleFactor(config);
            return scale > 0f ? unityScreenValue / scale : unityScreenValue;
        }

        /// <summary>캔버스 유닛 -> Unity 스크린 픽셀(<see cref="UnityScreenToCanvas"/>의 역).</summary>
        public static float CanvasToUnityScreen(float canvasValue, StickConfig config)
        {
            return canvasValue * ResolveCanvasScaleFactor(config);
        }

        /// <summary>
        /// 오버레이 창(= 우리 Unity Player 창)의 좌상단이 OS 데스크톱 좌표계에서 어디에 있는지.
        /// 기본값 (0,0)은 "창이 화면 좌상단에서 시작한다"는 이 클래스의 원래 가정이며, 그 가정이
        /// 맞는 환경(에디터/헤드리스/Windows 스텁)에서는 아래 두 변환식이 예전과 완전히 동일하게 동작한다.
        ///
        /// ============================================================================
        /// 왜 필요한가 — 드래그&던지기 배선 라운드(2026-08-28)에 실측 로그로 드러난 좌표 어긋남
        /// ============================================================================
        /// 직전 라운드 실측: `windowSize=(1512, 846)`, `windowPosition=(0, 75)`. 즉 우리 창은 화면 폭은
        /// 전부 덮지만 **세로로는 메뉴바/Dock을 뺀 가운데 846pt 구간에만** 존재한다. 그런데 이 클래스의
        /// 기존 식은 `osY = (Screen.height - unityY) * dpi`로 "창 좌상단 = 화면 좌상단"을 가정하므로,
        /// 커서(CGEventGetLocation, 화면 전역 좌표)를 월드로 되돌릴 때 창 오프셋만큼 통째로 틀어진다.
        /// 실측값 기준 세로 오차는 약 60~75 OS-pt = 월드 약 2유닛으로, 캐릭터 전신 높이(2.27유닛)에
        /// 맞먹는다 — 드래그하면 캐릭터가 커서에서 한 몸 길이만큼 벗어난 채 따라다니게 된다.
        ///
        /// 그래서 "OS 데스크톱 좌표 ↔ 창 클라이언트 좌표"의 원점 차이를 이 한 값으로 흡수한다. 실제
        /// 갱신은 Platform/MacOS/MacWindowService.EnumerateFootholds()가 이미 돌고 있는 창 열거
        /// 루프에서 자기 창(IsSelfWindow)의 kCGWindowBounds를 집어 <see cref="ReportOverlayWindowOsRect"/>로
        /// 넘기는 것이다 — 추가 시스템 호출이 전혀 없고, 커서 좌표(CGEventGetLocation)와 **완전히 같은
        /// Quartz 좌표계**라 좌표계 혼용 위험도 없다(MacWindowService의 ICursorPositionService 주석 참고).
        /// 그 한 번의 보고에서 <see cref="AutoDpiScale"/>도 함께 유도된다(원점과 배율이 항상 같은 관측에서 나온다).
        ///
        /// static 가변 상태인 이유: 이 클래스는 순수 static 유틸이고 소비자(States/Interaction 전역)가
        /// 인스턴스를 들고 다니지 않는다. 프로젝트에 이미 같은 성격의 static이 있다(Core/SpectacleEventLock,
        /// Core/StressGauge). 기본값이 (0,0)이라 세팅하지 않는 플랫폼/테스트는 기존 동작 그대로다.
        /// </summary>
        public static Vector2 OverlayOriginOsScreen { get; set; } = Vector2.zero;

        /// <summary>
        /// Unity 월드 좌표 -> OS 데스크톱 좌표(좌상단 원점, 픽셀).
        /// </summary>
        /// <param name="cameraDepth">
        /// 왕복 변환을 위해 함께 반환하는 "카메라로부터의 거리". 같은 호출 세트 안에서
        /// OsScreenToWorld로 되돌릴 때 이 값을 그대로 넘겨야 정밀도가 보존된다.
        /// </param>
        public static Vector2 WorldToOsScreen(Camera cam, Vector3 worldPos, StickConfig config, out float cameraDepth)
        {
            Vector3 unityScreen = cam.WorldToScreenPoint(worldPos); // 좌하단 원점, Unity 픽셀
            cameraDepth = unityScreen.z;

            float dpi = Mathf.Max(0.0001f, ResolveDpiScale(config));
            Vector2 origin = OverlayOriginOsScreen;
            float osX = unityScreen.x * dpi + origin.x;
            float osY = (Screen.height - unityScreen.y) * dpi + origin.y; // 좌상단 원점으로 y 반전 + 창 오프셋
            return new Vector2(osX, osY);
        }

        /// <summary>
        /// OS 데스크톱 좌표(좌상단 원점) -> Unity 스크린 좌표(좌하단 원점, Unity 픽셀).
        /// 카메라/월드를 전혀 거치지 않는 순수 화면 좌표 변환이라 ScreenSpaceOverlay UI의 히트테스트에
        /// 그대로 쓸 수 있다(Interaction/AppControlDirector.cs의 우클릭 메뉴 — 클릭관통 오버레이에서는
        /// uGUI EventSystem을 신뢰할 수 없어 전역 커서 좌표로 직접 판정해야 한다).
        ///
        /// 변환식은 OsScreenToWorld()의 앞부분과 **완전히 동일한 한 벌**이다 — 좌표 변환은 오직 이
        /// 클래스만 담당한다는 컨벤션(BUG-M5)을 지키기 위해 소비자 쪽에서 같은 식을 다시 쓰지 않고
        /// 여기에 이름을 붙여 노출한다.
        /// </summary>
        public static Vector2 OsScreenToUnityScreen(Vector2 osScreenPoint, StickConfig config)
        {
            float dpi = Mathf.Max(0.0001f, ResolveDpiScale(config));
            Vector2 origin = OverlayOriginOsScreen;
            float unityX = (osScreenPoint.x - origin.x) / dpi;
            float unityY = Screen.height - ((osScreenPoint.y - origin.y) / dpi);
            return new Vector2(unityX, unityY);
        }

        /// <summary>OS 데스크톱 좌표 -> Unity 월드 좌표. cameraDepth는 WorldToOsScreen에서 얻은 값을 그대로 넘길 것.</summary>
        public static Vector3 OsScreenToWorld(Camera cam, Vector2 osScreenPoint, float cameraDepth, StickConfig config)
        {
            float dpi = Mathf.Max(0.0001f, ResolveDpiScale(config));
            Vector2 origin = OverlayOriginOsScreen;
            float unityX = (osScreenPoint.x - origin.x) / dpi;
            float unityY = Screen.height - ((osScreenPoint.y - origin.y) / dpi); // 좌하단 원점으로 y 재반전
            return cam.ScreenToWorldPoint(new Vector3(unityX, unityY, cameraDepth));
        }
    }
}
