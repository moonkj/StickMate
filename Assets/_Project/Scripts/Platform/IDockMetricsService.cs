namespace StickMate.Platform
{
    /// <summary>
    /// ★ 2026-08-29 — 사용자 신고 "지금도 독이랑 계속 겹쳐" -> "지금도 제대로 바닥과 독을 제대로
    /// 인식 못하는거 같음"(2차)의 수정을 위해 신설/전면 개정.
    ///
    /// ============================================================================
    /// 1. 결론부터 — Dock의 진짜 사각형은 공개 API로 얻을 수 없다 (전수 조사로 확정)
    /// ============================================================================
    /// 리더 지시는 "Dock 프로세스는 창을 여러 개 가질 가능성이 높으니 kCGWindowOwnerName == "Dock"인
    /// 창을 **전부** 열거해서 진짜 Dock 막대와 일치하는 창이 있는지 확인하라"였다. 그대로 했다
    /// (2026-08-29, CGWindowListCopyWindowInfo 전수 덤프 — kCGWindowListOptionOnScreenOnly를 **뺀**
    /// optionAll까지 포함해 숨은 창 143개 전부):
    ///
    ///     owner='Dock'  name='Dock'        layer=20            alpha=1.0  bounds=(0,0,1512,982)
    ///     owner='Dock'  name='Wallpaper-'  layer=-2147483624   alpha=1.0  bounds=(0,0,1512,982)
    ///
    /// **Dock 프로세스가 소유한 창은 이 둘뿐이고, 둘 다 화면 전체 크기다.** 나아가 시스템 전체 143개
    /// 창 중 "Dock 막대 모양"(화면 하단 + 폭 300pt 이상 + 두께 40~160pt)인 창은 **소유자를 불문하고
    /// 단 하나도 없었다.** 즉 Dock 막대는 어떤 창의 bounds로도 노출되지 않는다 — macOS의 Dock은 화면
    /// 전체를 덮는 레이어 하나에 막대를 그릴 뿐이다. 이 경로는 영구히 막혔다(추정이 아니라 실측 결론).
    ///
    /// 남은 정확한 경로 두 개도 전부 채택 불가다:
    ///   · CGWindowListCreateImage로 Dock 창만 캡처해 알파 경계 측정 -> **화면 기록 권한** 필요. 금지.
    ///   · Dock의 AXUIElement에서 AXDockItem들의 AXPosition/AXSize 조회 -> **접근성 권한** 필요. 금지.
    /// (CLAUDE.md 비침해 원칙 + 리더 지시: 필요하다는 결론이 나와도 쓰지 말고 보고할 것.)
    ///
    /// ============================================================================
    /// 2. 그래서 무엇을 하는가 — "추정"을 없앤 게 아니라 **추정의 유일한 미지수를 없앴다**
    /// ============================================================================
    /// Dock 폭은 타일 개수 N에 정확히 선형이다. 지금까지 틀린 이유는 공식이 아니라 **N을 몰랐다는 것**
    /// 하나였다(직전 라운드는 "실행 중이지만 고정돼 있지 않은 앱"의 타일 수를 셀 방법이 없다고 보고
    /// StickConfig.dockExtraRunningAppTileEstimate = 6으로 때려박았고, 그 결과 좌우 각 77pt씩 Dock을
    /// 넓게 잡아 "Dock 없는 자리에서 Dock 위에 선 것처럼 부양"하는 이번 신고가 나왔다).
    ///
    /// N은 셀 수 있다. **NSWorkspace.runningApplications 중 activationPolicy == NSApplicationActivation
    /// PolicyRegular인 앱이 곧 "Dock에 타일이 생기는 앱"의 정의 그 자체**다(권한 불필요, 공개 API).
    /// 이것과 com.apple.dock 설정을 합치면:
    ///
    ///     N = 1(Finder, 항상 있고 persistent-apps에 없다)
    ///       + persistent-apps
    ///       + persistent-others
    ///       + | recent-apps ∪ (실행 중 .regular 앱 - persistent-apps - Finder) |   ← 두 집합은 겹친다
    ///       + 1(휴지통)
    ///
    /// 실측 검증(2026-08-29, 이 개발 머신 1512x982 / tilesize=49): 위 식이 준 N=20과 스크린샷에서
    /// **육안으로 하나씩 센 타일 20개**가 정확히 일치했다(Finder / 고정 13개 / 최근+실행중 5개 / 휴지통).
    ///
    /// ============================================================================
    /// 3. 폭 공식 — 6개 표본 최소제곱 검증, 최대 오차 1.0pt
    /// ============================================================================
    ///     폭 = N x (tilesize + dockTilePitchPaddingPoints)
    ///        + dockPanelFixedPaddingPoints
    ///        + 구분선수 x dockSeparatorWidthPoints
    ///     좌 = 화면중앙 - 폭/2   (가운데 정렬 — 아래 표본 전부에서 중심이 정확히 756.00pt였다)
    ///
    /// 표본(앱을 하나씩 켜서 타일을 1개씩만 늘리고, 매번 스크린샷에서 패널 좌우 테두리를 재측정):
    ///     N=20 -> 1123.50pt   N=21 -> 1175.00   N=22 -> 1229.00
    ///     N=23 -> 1281.00     N=24 -> 1335.00   N=25 -> 1387.00
    /// 위 계수(피치=tilesize+4, 고정분 15, 구분선 24 x 2개)를 넣으면 예측값은
    ///     1123 / 1176 / 1229 / 1282 / 1335 / 1388  -> **최대 오차 1.0pt**.
    /// (측정 방법: screencapture PNG에서 패널 상/하 테두리 사이 5개 행의 휘도를 평균해 좌우 끝의
    ///  1px 밝은 테두리 능선을 찾았다. 두 끝의 중점이 매번 화면 정중앙 756.0pt와 일치하는 것으로
    ///  측정 자체를 교차 검증했다. 앱 실행/종료 외에 사용자의 Dock 설정은 전혀 건드리지 않았다 —
    ///  절대 불변 원칙 3.)
    ///
    /// 직전 라운드의 공식(피치 51.5 = tilesize+2.5, 고정분 76.5)은 표본이 2개뿐이라 피치가 1.3pt
    /// 작게 잡혔고, 그 오차가 N에 곱해져 누적됐다. 이번엔 표본 6개로 레버암을 6배 늘려 다시 폈다.
    ///
    /// 구분선 개수는 세어서 쓴다(고정 2개가 아니다): Dock은 [Finder+고정앱] | [최근/실행중] |
    /// [기타스택+휴지통] 순서이고, 가운데 구획이 비면(show-recents 끄고 실행 중 비고정 앱도 없을 때)
    /// 구분선이 1개로 줄어든다. 그 경우까지 같은 식으로 덮으려고 고정분과 구분선분을 분리했다.
    ///
    /// ============================================================================
    /// 4. 두께(Dock 상단 Y)도 더 이상 하드코딩 75가 아니다
    /// ============================================================================
    /// 리더 지적대로 두께는 tilesize에 따라 변한다. 실측: tilesize=49일 때 Dock 두께가 정확히 75.00pt
    /// (NSScreen.visibleFrame의 하단 인셋 = 75.00, 화면 982 - 작업영역 874 - 메뉴바 33 = 75와 일치,
    /// 스크린샷에서 잰 패널 상단 테두리 y=907 = 982-75와도 일치). 따라서
    ///     두께 = tilesize + dockThicknessTilePaddingPoints(26)
    /// 로 tilesize에 따라오게 했다. 정직한 한계: 이 관계식의 보정점은 tilesize=49 **한 점뿐**이다
    /// (두 번째 점을 얻으려면 사용자의 Dock 크기 설정을 바꿔야 하는데 그건 절대 불변 원칙 3 위반이라
    /// 하지 않았다). 그래서 이 값이 틀리면 StickConfig에서 바로 고칠 수 있게 상수로 빼뒀다.
    ///
    /// 이 인터페이스는 위 계산의 **입력**을 OS에서 읽어오는 창구다. 화면 기록 권한도, 접근성 권한도
    /// 필요 없다 — com.apple.dock 설정을 CFPreferences로 **읽기만** 하고(쓰지 않는다, 절대 불변 원칙 3),
    /// 실행 중 앱 목록은 NSWorkspace로 **조회만** 한다.
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
    /// Dock 발판 기하를 결정하는 데 필요한 OS 조회 결과 묶음. 전부 읽기 전용 조회다.
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

        /// <summary>Dock에 실제로 놓여 있는 타일 총 개수(Finder/휴지통 포함).
        /// <see cref="IsTileCountExact"/>가 true면 이 값은 추정이 아니라 셈이다.</summary>
        public readonly int TileCount;

        /// <summary>타일 사이에 그려지는 구분선의 개수(보통 2, 가운데 구획이 비면 1).
        /// 구분선도 가로 폭을 차지하므로 폭 공식에 개수만큼 곱해 더한다.</summary>
        public readonly int SeparatorCount;

        /// <summary>
        /// <see cref="TileCount"/>가 **정확히 셈한 값**인지. NSWorkspace 조회에 성공하면 true다.
        ///
        /// false는 "실행 중이지만 Dock에 고정되지 않은 앱"을 세지 못했다는 뜻 — 즉 TileCount가
        /// 반드시 **실제보다 작다**(= Dock을 실제보다 좁게 본다). 호출부는 그때만
        /// StickConfig.dockExtraRunningAppTileEstimate로 모자란 만큼을 더한다. 성공했을 때 그 보정을
        /// 또 더하면 이번 신고("부양")가 그대로 재발하므로, 이 플래그로 두 경우를 반드시 구분해야 한다.
        /// </summary>
        public readonly bool IsTileCountExact;

        public DockMetrics(bool isBottomOriented, bool isAutoHidden, float tileSizePoints,
            int tileCount, int separatorCount, bool isTileCountExact)
        {
            IsBottomOriented = isBottomOriented;
            IsAutoHidden = isAutoHidden;
            TileSizePoints = tileSizePoints;
            TileCount = tileCount;
            SeparatorCount = separatorCount;
            IsTileCountExact = isTileCountExact;
        }
    }
}
