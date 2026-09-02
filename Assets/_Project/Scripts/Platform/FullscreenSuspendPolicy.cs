using System;
using System.Collections.Generic;

namespace StickMate.Platform
{
    /// <summary>
    /// "전체화면 앱이 떴으니 캐릭터를 숨긴다"는 판정에서 **네이티브 조회와 무관한 순수 규칙**만 떼어낸
    /// 부분. 여기에는 P/Invoke가 한 줄도 없어야 한다 — 그래야 EditMode 테스트가 macOS 네이티브 상태에
    /// 의존하지 않고 규칙 자체를 검증할 수 있다(MacWindowService는 이 규칙에 입력값만 공급한다).
    ///
    /// ============================================================================
    /// 왜 카테고리 필터가 필요한가 (2026-08-31, 디버거 실측 -> 리더 결정)
    /// ============================================================================
    /// CLAUDE.md 절대 불변 원칙 2의 문구는 "전체화면 <b>게임</b> 감지 시 자동 숨김"인데, 기존
    /// EvaluateFullscreen()은 "전경 창의 bounds == 메인 디스플레이 bounds"만 보고 판정했다. 그래서 엑셀,
    /// 키노트, 브라우저를 전체화면으로 쓰는 동안에도 캐릭터가 사라졌다(사용자 신고 "타 앱 전체화면
    /// 클릭 시 캐릭터가 사라짐"의 한 축). 원칙 문구대로 **게임일 때만** 숨기도록 좁힌다.
    ///
    /// ★ 2026-09-02 범위 정정 — 위 문장의 "숨긴다"는 이제 <b>캐릭터에 한정</b>된다. 게임이 아닌
    /// 전체화면 앱에서도 <b>창·패널·팝오버·부채꼴과 그 클릭 차단막</b>은 물러난다
    /// (<see cref="ForeignFullscreenTier.PanelsOnly"/>). 이 문단만 읽고 "게임이 아니면 아무것도 안 한다"로
    /// 요약하면 <b>거짓</b>이다 — 실제로 그 요약 때문에 홍보 문구 한 줄이 사실과 어긋난 채 준비될
    /// 뻔했다(marketing 라운드, 같은 날).
    ///
    /// 판별 근거는 앱 번들 Info.plist의 <c>LSApplicationCategoryType</c>이다. 이건 App Store 카테고리를
    /// 앱이 스스로 선언해두는 메타데이터일 뿐이라, 읽어도 유저 자산에 어떤 영향도 없다(원칙 3 안전).
    ///
    /// ============================================================================
    /// 왜 이 파일이 Platform/MacOS/가 아니라 Platform/에 있는가 (2026-09-01 Windows 패리티 감사)
    /// ============================================================================
    /// 원래 이 파일은 <c>Platform/MacOS/</c>에 있었고 네임스페이스도 <c>StickMate.Platform.MacOS</c>였다.
    /// 그 자리에 두면 <b>Windows 구현이 같은 규칙을 부를 수 없다</b> — 실제로 그 하루 동안
    /// <see cref="FullscreenVerdictDebouncer"/>는 macOS에만 걸려 있었고, Windows 사용자는 메뉴/작업표시줄
    /// 자동 숨김 등으로 전경 창 사각형이 순간적으로 바뀔 때마다 캐릭터가 깜빡이는 상태로 남아 있었다.
    /// 이 프로젝트가 <c>VisibleTopEdgeSolver</c>/<c>WindowsFootholdFilter</c>에서 이미 두 번 겪은
    /// "한쪽 플랫폼 폴더에 갇힌 정책" 실패와 같은 구조라, 규칙 자체는 플랫폼 중립 폴더로 올린다.
    /// (네임스페이스 <c>StickMate.Platform</c>은 <c>StickMate.Platform.MacOS</c>/<c>.Windows</c>의
    ///  바깥 범위라, 두 플랫폼 파일 모두 using 추가 없이 그대로 참조된다.)
    /// </summary>
    public static class FullscreenGameCategory
    {
        /// <summary>Apple이 정한 카테고리 UTI의 공통 접두사.</summary>
        private const string CategoryPrefix = "public.app-category.";

        /// <summary>게임 대분류. 세부 장르는 "...-games"로 끝난다(action-games, puzzle-games 등).</summary>
        private const string GamesCategory = "public.app-category.games";

        /// <summary>세부 장르 판별용 접미사.</summary>
        private const string GamesSuffix = "-games";

        /// <summary>
        /// <c>LSApplicationCategoryType</c> 문자열이 게임 계열인지.
        ///
        /// 대분류 하나만 비교하지 않는 이유: 실제 App Store 게임 대부분은 세부 장르
        /// (<c>public.app-category.action-games</c> 등)를 선언하고 대분류를 쓰지 않는다. 접두사 + 접미사
        /// 조합으로 판정하면 Apple이 장르를 추가해도 코드 수정 없이 따라간다.
        ///
        /// 미선언(null/빈 문자열)은 <b>게임이 아님</b>으로 본다 — 원칙 2가 "게임"이라고 명시한 이상
        /// 모르는 앱을 게임으로 추정해 숨는 쪽이 아니라, 숨지 않는 쪽이 보수적이다(선언 안 한 게임에서
        /// 캐릭터가 계속 보이는 것은 사소한 불편이지만, 선언 안 한 업무 앱에서 캐릭터가 사라지는 것은
        /// 지금 신고된 바로 그 버그다).
        /// </summary>
        public static bool IsGameCategory(string categoryType)
        {
            if (string.IsNullOrEmpty(categoryType)) return false;

            // 문자열 비교는 반드시 Ordinal — 이건 사람이 읽는 텍스트가 아니라 UTI 식별자다.
            if (string.Equals(categoryType, GamesCategory, StringComparison.Ordinal)) return true;

            return categoryType.StartsWith(CategoryPrefix, StringComparison.Ordinal)
                && categoryType.EndsWith(GamesSuffix, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// **"이 창이 디스플레이를 통째로 덮고 있는가"의 순수 기하 규칙.** P/Invoke가 한 줄도 없으므로
    /// EditMode에서 네이티브 없이 검증된다 — <b>이 파일이 존재하는 이유가 그것이다</b>.
    ///
    /// ============================================================================
    /// 왜 "정확히 일치"만으로는 부족한가 (2026-09-02, 실기 통제 실험)
    /// ============================================================================
    /// macOS **네이티브 전체화면**(초록 버튼 / <c>toggleFullScreen:</c>)에 들어간 창의 실측 사각형은
    /// 디스플레이 사각형과 <b>같지 않다</b>:
    /// <code>
    ///   CGDisplayBounds  = (0,  0, 1512, 982)
    ///   실제 전체화면 창 = (0, 33, 1512, 949)   &lt;- 상단 33pt가 시스템 스트립에 남는다
    /// </code>
    /// 그래서 기존 "정확히 일치" 판정은 네이티브 전체화면 게임에서 <b>영원히 false</b>였고,
    /// 절대 불변 원칙 2("전체화면 게임 감지 시 자동 숨김")가 그 경로에서 통째로 죽어 있었다.
    ///
    /// <para><b>★ 반증된 처방들</b>(같은 라운드에서 실측으로 각각 기각됐다. 다시 시도하지 말 것):
    /// <list type="bullet">
    /// <item><c>safeAreaInsets.top</c>(=32)을 상한으로 쓰기 — 실측 여백은 <b>33</b>이고 epsilon은
    ///   0.5라 <b>0/24 샘플</b>에서 실패했다. "원인을 고쳤다"는 서사만 남고 동작은 하나도 안 바뀐다.</item>
    /// <item><c>statusThick</c>(=22) / <c>auxiliaryTopLeftArea</c>(=32) — 어느 것도 33이 아니다.</item>
    /// <item>"기하 불일치면 다음 창을 계속 본다"(<c>continue</c>) — 그러면 z-order 아무 데나 화면
    ///   크기의 게임 창이 있기만 하면 숨는다. 전체화면 게임 위에 작은 창을 띄우고 작업 중일 때
    ///   캐릭터가 사라져 <b>원칙 2의 반대편</b>을 새로 깬다.</item>
    /// </list></para>
    ///
    /// ============================================================================
    /// 채택한 규칙과 그 검산
    /// ============================================================================
    /// <code>
    ///   정확일치  OR  ( 가로 전폭 AND 하단 밀착 AND 0 &lt;= 상단 여백 &lt;= 디스플레이 높이 * 5% )
    /// </code>
    /// · <b>상단 여백 상한을 비율로</b> 잡는 이유: 노치/메뉴바 두께는 기기와 스케일링에 따라 달라지므로
    ///   상수 하나를 박으면 다음 기기에서 또 0/24가 된다. 실측 33 / 982 = <b>3.36%</b>이고 상한 5% =
    ///   49.1pt라 <b>여유 1.49배</b>다.
    /// · <b>하단 밀착이 음성 대조의 핵심 방벽이다.</b> Dock이 보이는 "줌(최대화)" 창은
    ///   <c>(0,33,1512,874)</c>여서 하단이 <c>907 != 982</c>로 어긋나 탈락한다. 이 조건이 없으면
    ///   최대화한 업무 창이 전부 전체화면으로 오판된다.
    /// · 좌우는 <b>전폭</b>을 요구한다 — 반쪽 화면(Split View)은 폭이 절반이라 탈락한다.
    ///
    /// ============================================================================
    /// ★ Windows는 <see cref="MatchesExactly"/>만 쓴다 (의도적 분기, 2026-09-02)
    /// ============================================================================
    /// Windows에는 "화면 상단에 OS가 항상 남겨두는 띠"라는 개념이 없다. 오히려 상단 도킹 작업표시줄이
    /// 흔해서, 상단 여백 허용을 그대로 켜면 <b>상단 작업표시줄 환경에서 최대화한 창이 전부 전체화면으로
    /// 오판</b>된다(= 원칙 2의 반대편을 깨는, macOS에서 방금 피한 것과 똑같은 사고). 그래서 규칙은 이
    /// 중립 파일에 함께 두되 <b>Windows는 관용 없는 쪽을 명시적으로 호출</b>한다.
    /// 실기 검증 없이 관용을 켜지 않는다 — 이 분기는 갭이 아니라 결정이다.
    /// </summary>
    public static class FullscreenGeometry
    {
        /// <summary>부동소수/서브픽셀 오차 허용치(OS 포인트). 두 플랫폼이 같은 값을 쓴다.</summary>
        public const double Epsilon = 0.5;

        /// <summary>
        /// 상단에 남아도 되는 시스템 스트립(메뉴바/노치)의 최대 비율. 실측 33/982 = 3.36%에 대해
        /// 1.49배 여유. 절대값(32/22/33...)을 박으면 기기·스케일링이 바뀔 때마다 다시 깨진다.
        /// </summary>
        public const double MenuBarStripFraction = 0.05;

        /// <summary>창 사각형이 디스플레이 사각형과 정확히 같은가(보더리스 전체화면 / Windows 경로).</summary>
        public static bool MatchesExactly(
            double winX, double winY, double winWidth, double winHeight,
            double dispX, double dispY, double dispWidth, double dispHeight,
            double epsilon)
        {
            return Math.Abs(winX - dispX) < epsilon
                && Math.Abs(winY - dispY) < epsilon
                && Math.Abs(winWidth - dispWidth) < epsilon
                && Math.Abs(winHeight - dispHeight) < epsilon;
        }

        /// <summary>
        /// 창이 디스플레이를 덮고 있는가 — 정확일치, 또는 상단 시스템 스트립만 남긴 네이티브 전체화면.
        /// 클래스 문서의 검산 표와 반증 목록을 함께 읽을 것.
        /// </summary>
        public static bool CoversDisplay(
            double winX, double winY, double winWidth, double winHeight,
            double dispX, double dispY, double dispWidth, double dispHeight,
            double epsilon)
        {
            // 퇴화 사각형은 어떤 경우에도 "덮는다"가 아니다(0x0짜리 보조 창이 여기까지 오면 사고다).
            if (winWidth <= 0.0 || winHeight <= 0.0) return false;
            if (dispWidth <= 0.0 || dispHeight <= 0.0) return false;

            if (MatchesExactly(winX, winY, winWidth, winHeight,
                    dispX, dispY, dispWidth, dispHeight, epsilon)) return true;

            // 가로 전폭 — Split View(반쪽 화면)를 여기서 떨군다.
            if (Math.Abs(winX - dispX) >= epsilon) return false;
            if (Math.Abs(winWidth - dispWidth) >= epsilon) return false;

            // ★ 하단 밀착 — Dock이 보이는 "줌(최대화)" 창을 떨구는 핵심 방벽.
            if (Math.Abs((winY + winHeight) - (dispY + dispHeight)) >= epsilon) return false;

            double topInset = winY - dispY;
            if (topInset < -epsilon) return false;                       // 화면 위로 삐져나온 창.
            return topInset <= dispHeight * MenuBarStripFraction + epsilon;
        }
    }

    /// <summary>
    /// <b>남의 전체화면 앱이 떴을 때 우리가 물러나는 정도</b>. 값이 클수록 더 많이 걷는다.
    ///
    /// ============================================================================
    /// 왜 등급이 필요한가 (2026-09-02, 페르소나 `재현` 실기 재현 — 출시 Blocker)
    /// ============================================================================
    /// 카테고리를 선언하지 않은 앱(Zoom/Teams/Keynote 부류)을 네이티브 전체화면으로 올리면
    /// <b>자동 숨김이 0%</b>였다. 정보창이 그 위에 그대로 그려지고, <b>패널 안 클릭을 전체화면 앱이
    /// 받지 못한다</b>(우리 차단막이 먹는다). 실측: 정보창 877x853pt / 화면 1512x982pt =
    /// <b>면적 50.38% · 세로 86.86%</b>.
    ///
    /// <para><b>결정적 대조</b>: 같은 창에 게임 카테고리만 붙이니 숨김 5회/해제 5회로 <b>전부 정상
    /// 동작</b>했다. 즉 <b>숨김 기계는 끝까지 배선돼 있고, 없는 것은 트리거 하나뿐</b>이다.</para>
    ///
    /// ============================================================================
    /// 두 개의 반증된 처방 — 다시 시도하지 말 것
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><b>게임 카테고리 목록을 넓힌다</b>(<c>productivity</c> 등 추가) — <b>무효</b>.
    ///     재현이 잡은 창은 카테고리를 <b>선언하지 않았다</b>. 20종을 추가해도 미선언은 영원히 안 걸린다.</item>
    ///   <item><b>게임 조건을 없애고 기하만 쓴다</b> — <b>금지</b>. 2026-08-31 사용자 신고
    ///     <i>"엑셀같은 프로그램 전체화면에서 엑셀 클릭하면 캐릭터가 없어져버림"</i>의 <b>완전한 회귀</b>다.
    ///     <see cref="FullscreenGameCategory"/>와 <see cref="WindowsGameExecutablePolicy"/>의 클래스
    ///     문서가 둘 다 이 처방을 명시적으로 반증해 뒀다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// 채택 — <b>판정을 없애거나 넓히지 않고, 결과를 두 등급으로 가른다</b>
    /// ============================================================================
    /// 기하와 게임 여부는 <b>이미 따로 계산되고 있었다</b>(양 플랫폼 <c>EvaluateFullscreen</c>).
    /// 그 두 사실을 하나의 <c>bool</c>로 뭉개던 것을 풀어 등급으로 만든다. 새 네이티브 0줄, 새 권한 0개.
    /// </summary>
    public enum ForeignFullscreenTier
    {
        /// <summary>남의 전체화면 앱 없음. 아무것도 걷지 않는다.</summary>
        None = 0,

        /// <summary>
        /// <b>등급 1 — 패널 회수.</b> 기하만 일치(게임 아님).
        /// 화면에 <b>고정된 표면</b>(창·패널·팝오버·부채꼴·포스트잇·화면 오버레이)과 그 <b>클릭 차단막</b>을
        /// 걷는다. <b>캐릭터는 그대로 남는다</b> — 그것이 2026-08-31 신고를 회귀시키지 않는 유일한 선이다.
        /// </summary>
        PanelsOnly = 1,

        /// <summary>
        /// <b>등급 2 — 전면 숨김.</b> 기하 일치 <b>그리고</b> 게임.
        /// 지금까지의 동작 그대로(캐릭터 렌더러까지 끈다). <b>이 등급의 조건은 한 글자도 바뀌지 않았다.</b>
        /// </summary>
        Full = 2
    }

    /// <summary>
    /// <see cref="ForeignFullscreenTier"/>의 <b>순수 합성 규칙</b>. P/Invoke도 UnityEngine 의존도 없다 —
    /// 그래야 EditMode가 네이티브 없이 4분기를 전부 실행해 검증한다
    /// (<see cref="FullscreenGeometry"/>/<see cref="OverlayBoundsFitPolicy"/>와 같은 설계).
    ///
    /// <para><b>플랫폼 중립 위치에 있는 이유</b>: 이 파일의 <see cref="FullscreenGameCategory"/> 문서가
    /// 적어 둔 실제 사고 — 정책이 <c>Platform/MacOS/</c> 안에 있어서 <b>Windows가 물리적으로 부를 수
    /// 없었다</b> — 를 반복하지 않기 위해서다. 플랫폼 코드는 "기하가 맞는가"와 "게임인가"라는
    /// <b>두 사실만</b> 올려 보내고, 그 뜻은 전부 여기서 정해진다.</para>
    /// </summary>
    public static class ForeignFullscreenTierPolicy
    {
        /// <summary>
        /// 두 사실을 등급으로 합성한다.
        ///
        /// <para><b>진리표</b>(EditMode가 4분기를 전부 실행한다):</para>
        /// <list type="table">
        ///   <item><term>덮음=false, 게임=false</term><description><see cref="ForeignFullscreenTier.None"/></description></item>
        ///   <item><term>덮음=false, 게임=true </term><description><see cref="ForeignFullscreenTier.None"/> —
        ///     <b>게임이어도 전체화면이 아니면 아무 일도 없다.</b> 창 모드 게임을 켰다고 우리가 물러날 이유가 없다</description></item>
        ///   <item><term>덮음=true,  게임=false</term><description><see cref="ForeignFullscreenTier.PanelsOnly"/></description></item>
        ///   <item><term>덮음=true,  게임=true </term><description><see cref="ForeignFullscreenTier.Full"/></description></item>
        /// </list>
        /// </summary>
        public static ForeignFullscreenTier Resolve(bool coversDisplay, bool isGame)
        {
            if (!coversDisplay) return ForeignFullscreenTier.None;
            return isGame ? ForeignFullscreenTier.Full : ForeignFullscreenTier.PanelsOnly;
        }

        /// <summary>
        /// 이 등급에서 <b>캐릭터 본체</b>를 숨기는가. <see cref="ForeignFullscreenTier.Full"/>에서만 참이다.
        ///
        /// <para>★ <b>이 함수가 이번 변경의 안전판이다.</b> 기존 <c>IsFullscreenAppActive()</c>의 의미가
        /// 정확히 이것이며, 값이 바뀌는 입력 조합은 <b>하나도 없다</b> — 그래서 2026-08-31 신고
        /// ("엑셀 전체화면에서 캐릭터가 사라진다")가 회귀할 경로가 구조적으로 없다.</para>
        /// </summary>
        public static bool SuspendsCharacter(ForeignFullscreenTier tier)
            => tier == ForeignFullscreenTier.Full;

        /// <summary>
        /// 이 등급에서 <b>화면 고정 표면과 클릭 차단막</b>을 걷는가.
        /// <see cref="ForeignFullscreenTier.PanelsOnly"/>와 <see cref="ForeignFullscreenTier.Full"/> 모두 참이다 —
        /// 등급 2는 등급 1을 <b>포함</b>한다(등급이 올라갈수록 더 걷는다는 불변식).
        /// </summary>
        public static bool RetreatsPanels(ForeignFullscreenTier tier)
            => tier != ForeignFullscreenTier.None;

        /// <summary>로그용 한 줄 설명(전이 순간에만 조립 — 폴링 경로에서 문자열을 만들지 않는다).</summary>
        public static string Describe(ForeignFullscreenTier tier)
        {
            switch (tier)
            {
                case ForeignFullscreenTier.Full:
                    return "등급 2(전면 숨김) — 전체화면 **게임**이라 캐릭터까지 숨깁니다";
                case ForeignFullscreenTier.PanelsOnly:
                    return "등급 1(패널 회수) — 전체화면 앱이지만 게임이 아니라 창·패널·클릭 차단막만 " +
                        "걷고 **캐릭터는 남깁니다**(2026-08-31 신고 회귀 방지)";
                default:
                    return "등급 0 — 남의 전체화면 앱 없음";
            }
        }
    }

    /// <summary>
    /// Windows판 "이 전경 앱이 게임인가" 판정의 **순수 규칙**. macOS의
    /// <see cref="FullscreenGameCategory"/>와 같은 자리에 있고, 같은 계약을 지킨다:
    /// <b>모르면 게임이 아니다</b>.
    ///
    /// ============================================================================
    /// 왜 Windows에는 별도 규칙이 필요한가 (2026-09-01, 사용자 신고 재발 방지)
    /// ============================================================================
    /// 사용자 신고(2026-08-31): "엑셀같은 프로그램 전체화면에서 엑셀 클릭하면 캐릭터가 없어져버림."
    /// macOS는 그날 <c>LSApplicationCategoryType</c> 필터로 고쳤지만, <b>정작 신고 대상인 Windows는
    /// 기하 판정("전경 창 사각형 == 모니터 사각형")만 남아 있었다</b>(2026-09-01 패리티 감사에서 발각).
    /// 그래서 전체화면 Excel/PowerPoint 슬라이드쇼/브라우저 F11/동영상 전체화면에서 캐릭터가 계속
    /// 사라졌다. CLAUDE.md 절대 불변 원칙 2의 문구는 "전체화면 <b>게임</b> 감지 시 자동 숨김"이다.
    ///
    /// ============================================================================
    /// 판별 근거를 무엇으로 잡았고, 무엇을 <b>버렸는가</b>
    /// ============================================================================
    /// Windows에는 macOS의 <c>LSApplicationCategoryType</c>처럼 앱이 스스로 선언하는 "카테고리"가
    /// 없다. 감사 라운드가 후보를 실측/조사해 아래 순서로 좁혔다.
    ///
    /// <list type="bullet">
    /// <item><b>기각 — <c>SHQueryUserNotificationState</c></b>: 테두리 없는 전체화면 게임과 전체화면
    ///   엑셀이 <b>둘 다 <c>QUNS_BUSY</c></b>를 보고한다. 이것만으로는 이번 버그의 두 당사자를 구분
    ///   조차 못 하므로 단독 근거로 쓸 수 없다.</item>
    /// <item><b>기각 — 창 스타일 휴리스틱</b>(<c>WS_POPUP</c> + topmost + 캡션 없음): PowerPoint
    ///   슬라이드쇼와 브라우저 F11도 정확히 같은 모양이다. 기하 판정과 같은 오탐이 되풀이된다.</item>
    /// <item><b>기각 — 로드된 모듈 검사</b>(d3d11.dll/dxgi.dll): 엑셀·브라우저도 하드웨어 가속으로
    ///   같은 DLL을 올린다. 게다가 타 프로세스 모듈 열거는 원칙 3의 표면적만 넓힌다.</item>
    /// <item><b>채택 — 게임 바(Xbox Game Bar)의 게임 목록</b>: Windows 자신이
    ///   <c>HKCU\System\GameConfigStore\Children\*\MatchedExeFullPath</c>에 "이 실행 파일은
    ///   게임"이라고 기록해 둔다(사용자가 Win+G에서 확인해 준 것 포함). <b>읽기 전용</b> 조회만으로
    ///   충분하며 어떤 값도 쓰지 않는다(원칙 3 안전). 즉 "게임인가"의 판단을 우리가 추측하지 않고
    ///   OS/사용자가 이미 내려 둔 선언을 그대로 인용한다 — macOS에서 Info.plist를 인용하는 것과
    ///   정확히 같은 성격이다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// 알려진 한계 (전부 "안 숨는" 안전한 방향으로만 틀린다)
    /// ============================================================================
    /// 게임 바에 한 번도 잡히지 않은 게임, 목록 등록 후 경로가 바뀐 게임(런처가 버전 폴더를 갈아끼우는
    /// 경우), 게임 바 자체를 끈 환경에서는 <see cref="IsRegisteredGameExecutable"/>가 false가 되어
    /// <b>게임 위에서도 캐릭터가 남는다</b>. 리더 결정에 따라 이 방향을 택한다: 게임 중에 캐릭터가
    /// 안 숨는 것은 사소한 거슬림이지만, 업무 중 전체화면 문서에서 캐릭터가 사라지는 것은 지금
    /// 신고된 바로 그 버그다.
    ///
    /// (파일 경로만 비교하고 <b>파일 이름만으로는 절대 비교하지 않는다</b>. 이름 대조는 "경로가 바뀐
    ///  게임"을 구제해 주지만 <c>launcher.exe</c> 같은 흔한 이름에서 업무 앱을 게임으로 오인할 수
    ///  있다 — 그건 위험한 방향의 오탐이라 일부러 넣지 않았다.)
    /// </summary>
    public static class WindowsGameExecutablePolicy
    {
        /// <summary>
        /// 전경 프로세스의 실행 파일 경로가 "게임으로 등록된 실행 파일" 목록 안에 있는가.
        ///
        /// 조회 실패는 전부 <c>null</c>/빈 목록으로 들어오고, 그때 결과는 <b>false = 게임 아님 =
        /// 숨기지 않음</b>이다(macOS의 "카테고리 미선언 -> 게임 아님"과 같은 보수적 기본값).
        /// </summary>
        public static bool IsRegisteredGameExecutable(
            string foregroundExecutablePath, IReadOnlyList<string> registeredGameExecutablePaths)
        {
            if (registeredGameExecutablePaths == null) return false;
            if (string.IsNullOrEmpty(foregroundExecutablePath)) return false;

            // 인덱서 순회 — foreach의 열거자 할당조차 만들지 않는다(24시간 상주 앱).
            for (int i = 0; i < registeredGameExecutablePaths.Count; i++)
            {
                if (PathEquals(foregroundExecutablePath, registeredGameExecutablePaths[i])) return true;
            }
            return false;
        }

        /// <summary>
        /// Windows 실행 파일 경로 두 개가 같은 파일을 가리키는가. <b>할당이 한 바이트도 없다</b>
        /// (ToLower/Trim/Replace를 쓰면 폴링마다 문자열이 쌓인다 — 이 앱은 하루 종일 켜져 있다).
        ///
        /// 정규화 규칙과 그 근거:
        /// <list type="bullet">
        /// <item>대소문자 무시 — NTFS 경로는 대소문자를 구분하지 않는다. 레지스트리에는 게임 바가
        ///   본 그대로(<c>C:\Program Files\...</c>)가, <c>QueryFullProcessImageName</c>에서는 커널이
        ///   보관한 표기가 나와 철자 케이스가 어긋나는 일이 흔하다.</item>
        /// <item><c>/</c> == <c>\</c> — 두 구분자는 Win32에서 동등하다.</item>
        /// <item>양 끝의 따옴표/공백/NUL 무시 — 레지스트리 REG_SZ 값에는 종단 NUL이 딸려오고, 경로를
        ///   따옴표로 감싸 저장해 둔 항목도 있다.</item>
        /// </list>
        /// 그 외에는 아무것도 하지 않는다(중복 구분자 축약, 8.3 단축 경로 해석, 심볼릭 링크 추적 등은
        /// 하지 않는다) — 대조에 실패하면 "게임 아님"으로 떨어지므로 실패는 항상 안전한 방향이다.
        /// </summary>
        public static bool PathEquals(string a, string b)
        {
            if (a == null || b == null) return false;

            int aStart = 0, aEnd = a.Length;
            int bStart = 0, bEnd = b.Length;
            TrimBounds(a, ref aStart, ref aEnd);
            TrimBounds(b, ref bStart, ref bEnd);

            int length = aEnd - aStart;
            if (length <= 0) return false;               // 빈 경로끼리는 "같다"고 하지 않는다.
            if (length != bEnd - bStart) return false;

            for (int i = 0; i < length; i++)
            {
                if (Fold(a[aStart + i]) != Fold(b[bStart + i])) return false;
            }
            return true;
        }

        private static void TrimBounds(string s, ref int start, ref int end)
        {
            while (start < end && IsTrimmable(s[start])) start++;
            while (end > start && IsTrimmable(s[end - 1])) end--;
        }

        private static bool IsTrimmable(char c)
            => c == '"' || c == '\'' || c == '\0' || char.IsWhiteSpace(c);

        private static char Fold(char c)
            => c == '/' ? '\\' : char.ToUpperInvariant(c);
    }

    /// <summary>
    /// 전체화면 판정의 깜빡임(flapping)을 없애는 디바운스. 값 타입이라 힙 할당이 없다(24시간 상주 앱).
    ///
    /// ============================================================================
    /// 왜 필요한가 (2026-08-31, 디버거 실측)
    /// ============================================================================
    /// 같은 전체화면 창인데도 사용자가 커서를 화면 상단에 올려 메뉴바를 부르면 CGWindow bounds가
    /// <c>(0,33 ...)</c> 과 <c>(0,0 ...)</c> 사이를 오간다. 기하 일치 판정이 그때마다 뒤집혀
    /// Resume/Suspend가 반복되고, 캐릭터가 깜빡이며 프레임 등급도 요동친다.
    ///
    /// 그래서 "원시 판정이 바뀌었다"는 사실만으로 즉시 전환하지 않고, <b>바뀐 값이 연속으로 일정 시간
    /// 유지될 때만</b> 확정한다. 되돌아오면 후보를 버리고 기존 확정값을 유지한다.
    /// </summary>
    public struct FullscreenVerdictDebouncer
    {
        private bool _stable;          // 바깥에 보고 중인 확정값.
        private bool _candidate;       // 지금 관찰 중인 후보값.
        private double _candidateSince; // 후보가 처음 관찰된 시각(초).
        private bool _initialized;

        /// <summary>현재 확정값(아직 한 번도 갱신되지 않았으면 false = 숨기지 않음).</summary>
        public bool Stable => _stable;

        /// <summary>
        /// 원시 판정을 넣고 확정값을 받는다.
        /// </summary>
        /// <param name="rawVerdict">이번 폴링의 즉시 판정.</param>
        /// <param name="now">단조 증가 시각(초). 호출자가 Time.realtimeSinceStartupAsDouble 등을 넘긴다.</param>
        /// <param name="holdSeconds">확정에 필요한 연속 유지 시간. 0 이하면 디바운스 없이 즉시 반영.</param>
        public bool Update(bool rawVerdict, double now, double holdSeconds)
        {
            if (!_initialized)
            {
                // 최초 관측은 그대로 확정한다 — 앱 시작 시점에 이미 전체화면 게임이 떠 있었다면
                // holdSeconds만큼 캐릭터가 보였다 사라지는 것이 오히려 어색하다.
                _initialized = true;
                _stable = rawVerdict;
                _candidate = rawVerdict;
                _candidateSince = now;
                return _stable;
            }

            if (rawVerdict == _stable)
            {
                // 확정값으로 되돌아왔다 = 진행 중이던 후보를 폐기(깜빡임의 절반은 여기서 흡수된다).
                _candidate = rawVerdict;
                _candidateSince = now;
                return _stable;
            }

            if (rawVerdict != _candidate)
            {
                _candidate = rawVerdict;
                _candidateSince = now;
            }

            if (holdSeconds <= 0.0 || now - _candidateSince >= holdSeconds)
            {
                _stable = _candidate;
            }
            return _stable;
        }
    }
}
