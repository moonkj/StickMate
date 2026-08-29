namespace StickMate.Platform
{
    /// <summary>
    /// ★ 2026-08-29 — 사용자 신고 "지금도 독이랑 계속 겹쳐"의 수정을 위해 신설.
    ///
    /// ============================================================================
    /// 왜 이 인터페이스가 필요했는가 — "고정 비율 추정"의 원리적 한계
    /// ============================================================================
    /// 직전까지 Dock의 가로 구간은 StickConfig.dockFootholdWidthFraction(= 화면 폭의 65%, 가운데 정렬)
    /// 이라는 **고정 비율 추정**이었다. 그런데 Dock 폭은 타일 개수에 정비례하고, 타일 개수는
    ///   (a) 사용자가 Dock에 고정해둔 앱 수,
    ///   (b) 최근 사용 앱 표시 여부와 그 개수,
    ///   (c) **지금 실행 중이지만 고정돼 있지 않은 앱 수**(= 시시각각 변한다)
    /// 로 정해진다. 즉 어떤 고정 비율을 넣어도 사용자마다/시점마다 틀린다 — 고정값을 다른 고정값으로
    /// 바꾸는 것은 해결이 아니다(리더 지시).
    ///
    /// 실측(2026-08-29, 이 개발 머신 1512x982, tilesize=49):
    ///   · 타일 21개일 때 Dock 패널 = OS x 176.5 ~ 1334.5 (폭 1158.0, 화면의 76.6%)
    ///   · 타일 20개일 때 Dock 패널 = OS x 202.5 ~ 1309.0 (폭 1106.5, 화면의 73.2%)
    ///   · 차이 51.5 = **타일 1개의 피치**, 좌우로 정확히 25.75씩 대칭으로 줄었다(= 가운데 정렬 확인).
    ///   · 두 표본 모두 (폭 - 타일수 x 51.5) = 76.5로 **일치** -> 폭 = 타일수 x (tilesize + 2.5) + 76.5.
    /// (측정 방법: screencapture로 받은 PNG에서 Dock 패널 상단 안쪽 가로줄의 휘도 계단을 찾아 좌우 끝을
    ///  구했고, 두 번째 표본은 앱 하나를 실행/종료해 타일을 정확히 1개만 바꿔 얻었다. 앱 실행/종료 외에
    ///  사용자의 Dock 설정은 전혀 건드리지 않았다 — 절대 불변 원칙 3.)
    ///
    /// 이 인터페이스는 그 식의 **입력**(tilesize / 타일 수 / 방향 / 자동숨김)을 OS 설정에서 읽어오는
    /// 창구다. 화면 기록 권한도, 접근성 권한도 필요 없다 — com.apple.dock의 사용자 기본 설정을
    /// CFPreferences로 **읽기만** 한다(쓰지 않는다, 절대 불변 원칙 3).
    ///
    /// 구현하지 않아도 되는 선택적 캐퍼빌리티다. FallbackPlatformWindowService가 `as` 캐스팅으로
    /// 확인하고, 없으면 예전처럼 StickConfig.dockFootholdWidthFraction 고정 추정으로 되돌아간다
    /// (ILocalClickCaptureService/IDesktopIconLayoutService와 동일한 관례).
    /// </summary>
    public interface IDockMetricsService
    {
        /// <summary>
        /// 지금 이 순간의 Dock 실측 파라미터를 읽는다.
        /// </summary>
        /// <param name="metrics">읽기에 성공했을 때의 값. 실패하면 의미 없음.</param>
        /// <returns>OS 설정을 읽지 못했으면 false(호출부는 고정 비율 폴백을 쓴다).</returns>
        bool TryGetDockMetrics(out DockMetrics metrics);
    }

    /// <summary>
    /// Dock 발판 기하를 결정하는 데 필요한 OS 설정 묶음. 전부 읽기 전용 조회 결과다.
    /// </summary>
    public readonly struct DockMetrics
    {
        /// <summary>Dock이 화면 **아래쪽**에 있는지. false면(좌/우 세로 Dock) Dock 발판 개념 자체가
        /// 성립하지 않으므로 호출부는 Dock 발판을 만들지 않아야 한다.</summary>
        public readonly bool IsBottomOriented;

        /// <summary>자동 숨김이 켜져 있는지. true면 Dock은 평소 화면에 없으므로 발판을 만들면 안 된다
        /// (커서를 가져다 댈 때만 잠깐 나타나는 것을 발판으로 삼으면 캐릭터가 허공에 서게 된다).</summary>
        public readonly bool IsAutoHidden;

        /// <summary>com.apple.dock의 tilesize(아이콘 한 변, OS 포인트). 기본 미설정 시 macOS 기본값 48.</summary>
        public readonly float TileSizePoints;

        /// <summary>지금 Dock에 실제로 놓여 있을 것으로 추정되는 타일 총 개수(휴지통 포함).</summary>
        public readonly int TileCount;

        public DockMetrics(bool isBottomOriented, bool isAutoHidden, float tileSizePoints, int tileCount)
        {
            IsBottomOriented = isBottomOriented;
            IsAutoHidden = isAutoHidden;
            TileSizePoints = tileSizePoints;
            TileCount = tileCount;
        }
    }
}
