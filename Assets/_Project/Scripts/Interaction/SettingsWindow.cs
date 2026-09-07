using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ <b>StickMate 설정창</b> 720×560 — docs/UX_FLOW.md 35-1(탭 구조/와이어프레임/역할 규정),
    /// 2026-09-01 사용자 승인 시안 그대로.
    ///
    /// ============================================================================
    /// 이 창이 존재하는 이유 (35-1-7 "목표")
    /// ============================================================================
    /// 이 앱이 삭제되는 유일한 진짜 이유는 <b>"방해받았다"</b>이다(UX_FLOW 8절 P1). 그 감정이 났을 때
    /// 사용자가 찾는 것은 "설정 어딘가"가 아니라 <b>"이 짓 그만하게 하는 스위치"</b> 하나이고, 그것이
    /// 도달 가능한 곳에 없으면 남는 선택지는 삭제뿐이다. 그래서 이 창의 첫 탭은 꾸미기가 아니라
    /// <b>[일반] — 이 앱이 화면에 있는 방식</b>이다.
    ///
    /// ============================================================================
    /// 탭 5개 (35-1-4) — 이번 라운드는 [일반][캐릭터]만 채운다
    /// ============================================================================
    /// 일반 / 캐릭터 / 이벤트 / 접근성·성능 / 데이터. 나머지 셋은 <b>비워 두되 숨기지 않는다</b> —
    /// 35-1-7의 "미구현은 회색 + 사유. 없는 척하는 것보다 낫다(로드맵이 곧 기대치 관리다)".
    ///
    /// ============================================================================
    /// 배타 규칙 / 탈출구 (35-1-7)
    /// ============================================================================
    ///  · 열면 정보창·부채꼴·팝오버를 <b>여는 쪽에서 한 번에</b> 거둔다(CharacterInfoWindow가
    ///    확립한 관례 — 진입점마다 정리 코드를 흩뿌리면 네 번째 진입점에서 반드시 샌다).
    ///  · 탈출구: [✕] / 진입점 재선택. <b>ESC는 쓰지 않는다</b> — 이미 클릭관통 긴급 해제에 묶여 있다.
    ///  · ★ <b>창 밖 클릭은 닫지 않는다</b>(2026-09-02 사용자 지시). 근거와 그 대가는
    ///    <see cref="UiChrome"/>의 "창을 닫는 법" 절 한 곳에 모아 뒀다 — 세 표면이 같은 규칙을 쓴다.
    ///  · 전체화면 감지 시 창·차단막 즉시 정리, 복귀해도 자동으로 다시 열지 않는다(원칙 2).
    ///
    /// ============================================================================
    /// ★ 값은 <b>배포 에셋에 쓰지 않는다</b>
    /// ============================================================================
    /// 이 창이 만지는 모든 값은 <see cref="AppSettingsModel"/> / <see cref="UiLayoutModel"/> /
    /// <see cref="CharacterAppearanceModel"/> / <see cref="CharacterScaleController"/>를 지난다.
    /// <c>StickConfig</c>의 직렬화 필드에 직접 쓰면 그 순간 출하 기본값이 오염된다(2026-08-31에 두 번
    /// 겪은 실패 모드 — <see cref="AppSettingsModel"/> 클래스 문서 참고).
    /// </summary>
    public sealed class SettingsWindow : MonoBehaviour, IExclusiveSurface
    {
        // ==================== 치수 (35-1-5 와이어프레임) ====================

        public const float PanelWidth = 720f;
        public const float PanelHeight = 560f;
        public const float HeaderHeight = 48f;
        public const float TabBarHeight = 40f;
        /// <summary>
        /// ★ 2026-09-02 (41-2 결정 2) — 34 → <b>46</b>. 푸터가 2단이 되면서 <c>[지금 종료]</c>가
        /// <b>스크롤 영역 밖</b>(창 크롬)으로 올라왔다.
        ///
        /// <para><b>왜 카드를 재배치하지 않았나 — 산술로 기각됐다.</b> [일반] 페이지는 이미 92pt
        /// 넘치고 넘친 92pt 안에 <c>[지금 종료]</c>가 통째로 들어 있었는데, 같은 라운드가 그 탭에
        /// <b>더해야 하는</b> 것만 +112pt다(톱니 설명 16 + 경고 2줄 18 + 톱니 위치 행 60 + 안내 캡션 18).
        /// 넘침을 0으로 만들어도 다시 112pt 넘친다. <b>[일반] 탭은 앞으로도 항상 넘친다</b> —
        /// 그러므로 탈출구를 "안 넘치는 자리"에 두는 것 말고는 답이 없다. 취향이 아니라 산술이다.</para>
        ///
        /// <para>덤: 종료가 크롬으로 올라가면 <b>5개 탭 전부</b>에서 보인다. 죽은 탭에 들어가 있어도
        /// 끌 수 있다(예전에는 [일반] 탭에서만 존재했다).</para>
        /// </summary>
        public const float FooterHeight = 46f;

        /// <summary>푸터 윗줄(안내 2줄) 높이 — 아랫줄은 <c>FooterHeight - FooterTopRowHeight</c>다.</summary>
        public const float FooterTopRowHeight = 18f;

        public const float ContentHeight = PanelHeight - HeaderHeight - TabBarHeight - FooterHeight; // 426
        public const float ContentPadX = 20f;
        public const float ContentPadTop = 16f;

        /// <summary>내용이 잘리는 위/아래 끝에서 이만큼(pt) 서서히 사라진다. 한글 대문자 높이(약 10pt)보다
        /// 작으면 "반쯤 잘린 글자"가 그대로 남고, 크면 멀쩡한 행까지 흐려진다. 8pt = 캡션 한 줄(14pt)의
        /// 절반을 조금 넘는 값이라 잘린 글자는 확실히 녹고 온전한 행은 건드리지 않는다.</summary>
        public const int ClipFadePoints = 8;

        /// <summary>정보창(31900) 바로 위, 앱 제어 메뉴(32760) 아래. 설정창과 정보창은 상호 배타라
        /// 실제로 겹치지 않지만, 한 프레임의 전환 구간에서 설정창이 뒤로 숨지 않게 한다.</summary>
        private const int SortingOrderTopMost = 31950;

        private const float ClickPollInterval = 0.05f;
        /// <summary>같은 컨트롤의 연타를 한 번으로 접는 시간(초). ★ <b>public</b>인 이유: 같은 토글을
        /// 껐다 켜는 것을 검증하는 PlayMode 테스트가 이 창의 실제 대기 시간을 <b>숫자로 베끼지 않고</b>
        /// 참조해야 한다(CLAUDE.md — 프로덕션 상수 하드코딩 금지). 이 파일의 다른 테스트 관측점
        /// (<c>FeedClickForTests</c>/<c>*ScreenRect</c>)과 같은 사정이다 — PlayMode 어셈블리는
        /// <c>InternalsVisibleTo</c> 대상이 아니다.</summary>
        public const float ActionDedupSeconds = 0.35f;

        /// <summary>[지금 종료]의 2단 확인 시간 — <see cref="ActionCommandPopover"/>와 같은 값, 같은 이유
        /// (한 번의 오조준으로 앱이 꺼지면 안 된다).</summary>
        private const float QuitConfirmSeconds = 3f;

        /// <summary>
        /// ★★ 2026-09-02 — 전체화면이 <b>지나간</b> 뒤 이 창을 돌려놓는 유예(초).
        ///
        /// <para><b>고친 증상</b>: 설정을 만지던 중 전체화면이 감지되면 창이 <b>예고 없이</b> 닫히고,
        /// 게임을 끄고 돌아와도 복구되지 않고, <b>아무 흔적도 없었다</b>. 검증 중 3번 발생했고
        /// 읽히는 뜻은 하나다 — <i>"설정창이 자꾸 혼자 꺼진다 = 고장"</i>.</para>
        ///
        /// <para><b>왜 '되살린다'로 판정했나</b>: 우리는 그 창을 <b>닫은 게 아니라 빼앗았다</b>.
        /// 빼앗은 것을 돌려주는 것은 "부르지 않은 창을 띄우는 것"이 아니라 <b>되돌리기</b>다.
        /// 이 앱은 이미 같은 판단을 두 번 했다 — 톱니는 전체화면이 끝나면 스스로 돌아오고,
        /// 설정창은 자기가 밀어낸 정보창을 닫힐 때 돌려놓는다(M8 시트 복귀). 게다가 이 창의
        /// 마우스 재진입 경로는 <b>3홉</b>(톱니 → 부채꼴 [캐릭터] → 정보창 [설정])이라, 되돌려주지
        /// 않으면 대가가 가장 크다.</para>
        ///
        /// <para><b>왜 무제한이 아닌가</b>: 예전 주석의 우려(<i>"게임을 끄자마자 창이 튀어나오면
        /// 그 자체가 방해다"</i>)는 <b>긴</b> 전체화면에 대해서는 옳다. 30초 영상이 지나간 것과
        /// 두 시간 게임을 한 것은 다른 사건이다. 그래서 <b>짧게 지나간 경우에만</b> 되돌린다 —
        /// 그 사이 사용자가 하던 일을 기억하고 있을 시간이다.</para>
        ///
        /// <para><b>원칙 2는 구조적으로 안 깨진다</b>: 복귀 조건이 <c>!ArePanelsSuppressed</c>이므로
        /// 게임이 아직 전체화면이면 <b>실행될 수 없다</b>(문자열 사유가 아니라 상태를 본다 —
        /// <see cref="RestoreInfoWindowIfNeeded"/>와 같은 관례).</para>
        ///
        /// <para>★★★ <b>2026-09-03 — 무장 조건이 좁아졌다</b>: 이제 <c>ArePanelsSuppressed</c>가 아니라
        /// <see cref="StickmanAgent.IsSuspended"/>(등급 2)일 때만 무장한다. 이 장치의 존재 이유는
        /// 위 문단의 <i>"마우스 재진입 경로가 3홉이라 되돌려주지 않으면 대가가 가장 크다"</i>인데,
        /// 등급 1에서는 그 3홉이 <b>실제로 걸을 수 있게 됐다</b>(톱니 클릭이 사용자 허가를 낸다).
        /// 걸을 수 있는데도 예약을 걸면, 사용자가 톱니를 눌러 부채꼴을 부른 그 프레임에
        /// <b>부르지도 않은 설정창</b>이 그 위로 튀어나온다. 등급 2에서는 톱니가 아예 없으므로
        /// 이 안전망이 그대로 필요하다.
        ///   ★★★ <b>2026-09-03 한 번 더 좁아졌다</b>: <c>IsSuspended</c>가 아니라
        ///   <see cref="StickmanAgent.HidesScreenSurfaces"/>를 본다. 사용자 명시 숨김 단독은 이 창을
        ///   <b>빼앗지 않으므로</b>(창이 그대로 열려 있다) 되돌려 줄 것도 없다 — 그 상태에서
        ///   무장하면 나중에 <b>사용자가 닫아 둔</b> 창이 혼자 되살아난다.</para>
        /// </summary>
        public const float DefaultReopenAfterSuspendGraceSeconds = 20f;

        private static float _reopenGraceSeconds = DefaultReopenAfterSuspendGraceSeconds;

        /// <summary>지금 쓰이는 유예(초). 제품은 기본값을 그대로 쓰고 <b>테스트만</b> 낮춘다 —
        /// 20초를 진짜로 기다리는 테스트는 만들지 않는다(<see cref="PopoverPanel.IdleAutoCloseSeconds"/>와
        /// 같은 관례).</summary>
        public static float ReopenAfterSuspendGraceSeconds => _reopenGraceSeconds;

        public static void SetReopenGraceForTests(float seconds)
            => _reopenGraceSeconds = Mathf.Max(0f, seconds);

        public static void ResetReopenGraceForTests()
            => _reopenGraceSeconds = DefaultReopenAfterSuspendGraceSeconds;

        private bool _reopenArmed;
        private float _suspendClosedAt;

        /// <summary>지금 "전체화면이 지나가면 돌아온다" 예약이 걸려 있는가(진단/테스트 창구).</summary>
        public bool IsReopenAfterSuspendArmed => _reopenArmed;

        private void ArmReopenAfterSuspend()
        {
            _reopenArmed = true;
            _suspendClosedAt = Time.unscaledTime;
        }

        /// <summary>사용자가 직접 이 창을 열거나 닫았다 — 예약은 그 순간 뜻을 잃는다.</summary>
        private void DisarmReopenAfterSuspend() => _reopenArmed = false;

        private void TickReopenAfterSuspend()
        {
            if (!_reopenArmed) return;
            if (_open) { _reopenArmed = false; return; }               // 사용자가 이미 다시 열었다.
            if (_agent == null) { _reopenArmed = false; return; }
            // 등급 1(게임이 아닌 전체화면 앱)에서도 되돌리지 않는다 — 우리가 빼앗은 이유가 아직
            // 그대로 있는데 돌려주면 같은 프레임에 다시 빼앗게 된다(무한 왕복).
            if (_agent.ArePanelsSuppressed) return;                    // 아직 전체화면 — 원칙 2.

            _reopenArmed = false;
            float away = Time.unscaledTime - _suspendClosedAt;
            if (away > ReopenAfterSuspendGraceSeconds)
            {
                Debug.Log($"[설정창] 전체화면이 {away:F0}초 이어져 자동 복귀를 포기합니다" +
                    $"(유예 {ReopenAfterSuspendGraceSeconds:F0}초). 그만큼 지났으면 사용자는 다른 일을 " +
                    "하고 있고, 부르지 않은 창이 뒤늦게 튀어나오는 것이 더 방해입니다.");
                return;
            }

            Open($"전체화면이 {away:F0}초 만에 지나가 자동으로 닫혔던 창을 돌려놓습니다 " +
                "(우리가 닫은 게 아니라 빼앗은 것이므로 되돌립니다)",
                userInitiated: false);
        }

        /// <summary>[▲][▼] 한 번에 넘기는 양. 화면 높이에서 한 행쯤 겹쳐 남겨 맥락이 끊기지 않게 한다.</summary>
        private const float PageStep = ContentHeight - SettingsControls.RowHeight;

        // ==================== 탭 ====================

        public enum Tab { General = 0, Character = 1, Event = 2, Accessibility = 3, Data = 4 }

        private const int TabCount = 5;

        private static readonly string[] TabNames = { "일반", "캐릭터", "이벤트", "접근성 · 성능", "데이터" };

        // ==================== 탭바 배지 (docs/UI_SURFACE_SPEC.md 12) ====================

        // ★★ 2026-09-03 — 여기 있던 <c>TabLabelCharWidth = 11f</c>(탭 라벨 한 글자 폭 근사)를 <b>지웠다</b>.
        //
        //    그 11f는 <b>한글에서만</b> 맞는 수였다. 라틴 자폭은 절반 이하인데 같은 11f를 청구하니
        //    상자가 글리프의 두 배 가까이 부풀었고, 탭 5개가 왼쪽에서 오른쪽으로 쌓이는 이 줄에서는
        //    그 부풀음이 그대로 누적되어 <b>창 밖으로 밀려났다</b>. 즉 넘침의 원인은 «영어 단어가
        //    길어서»가 아니라 <b>모형이 청구한 가짜 폭</b>이었다.
        //
        //    ※ 같은 11f가 «접근성 · 성능»처럼 <b>공백·가운뎃점이 섞인 한국어</b>에서도 이미 틀리고
        //      있었다 — 공백 한 칸에 한글 한 글자와 같은 11pt를 물렸다.
        //
        //    이제 라벨 상자와 배지 상자는 둘 다 <see cref="SettingsControls.MeasuredWidth"/>
        //    (= <c>Text.preferredWidth</c>, 폰트가 실제로 잰 값)로 잡는다. 여백 상수
        //    (<see cref="TabPadX"/> / <see cref="TabBadgeGap"/>)는 <b>한 글자도 바뀌지 않았다</b>.

        /// <summary>탭 안쪽 좌우 여백. 배지가 붙어도 <b>양쪽 10pt 대칭</b>이 유지된다.</summary>
        private const float TabPadX = 10f;

        /// <summary>라벨 상자 높이(본문 12pt의 행 상자).</summary>
        private const float TabLabelHeight = 16f;

        /// <summary>
        /// 미구현 탭 라벨 오른쪽에 붙는 <b>보조 어절</b> — "기능이 없는 건가, 내가 못 찾은 건가"에
        /// <b>누르기 전에</b> 답하는 유일한 자리다(docs/UX_FLOW.md 43-3).
        ///
        /// <para>왜 기호가 아니라 글자인가: 이 앱의 자물쇠는 이미 <b>"놀면 열린다"</b>(장비 카드의
        /// <c>Lv.n에 열림</c>)라 미구현 탭에 붙이면 거짓 약속이 되고, 탭바의 도트는 관례상 <b>"새 것"</b>
        /// 이라 뜻이 뒤집히며, 밑줄은 이 앱에서 <b>"지금 여기"</b>다. 남는 것은 글자이고, 글자는
        /// 해독이 필요 없고 스크린리더가 읽는다.</para>
        ///
        /// <para>★ 캡션 접두사와 <b>같은 단어</b>다(<see cref="SettingsControls.NotBuiltWord"/>) —
        /// 탭에서 읽은 어휘가 행 캡션으로 그대로 이어진다. <b>public</b>인 이유: 회귀 테스트가 이
        /// 문자열을 <b>베끼지 않고</b> 참조해야 한다(CLAUDE.md).</para>
        /// </summary>
        public const string TabBadgeText = SettingsControls.NotBuiltWord;

        /// <summary>라벨과 배지 사이. <see cref="UiChrome.Space1"/>(4)은 글자 크기가 다른 두 덩어리를
        /// 붙여 놓기에 좁다 — 한 어구로 읽히되 두 덩어리인 것은 보여야 한다.</summary>
        private const float TabBadgeGap = UiChrome.Space2;

        // ★ 2026-09-03 — 배지 폭 상수(<c>TabBadgeText.Length × UiChrome.FontCaption</c>)도 지웠다.
        //   배지는 탭마다 <b>같은 글자</b>라 굽는 동안 한 번만 재고 나머지 탭이 그 값을 쓴다
        //   (BuildTabBar의 지역변수). 정적 필드로 두면 폰트를 재려고 Text를 만들 자리가 없다.

        /// <summary>배지 상자 높이(캡션 10pt의 행 상자 — <c>SettingsControls.BeginRow</c>와 같은 값).</summary>
        private const float TabBadgeHeight = 14f;

        /// <summary>탭마다 "이 탭을 여는 순간"을 한 줄로 — 35-1-4의 정의를 그대로 화면에 쓴다.</summary>
        private static readonly string[] TabEyebrows =
        {
            "이 앱이 화면에 있는 방식",
            "이 캐릭터가 보이고 말하는 방식",
            "이 캐릭터가 알아서 하는 일의 범위",
            "내 눈과 내 컴퓨터에 맞추기",
            "내 것을 확인하고 지우기",
        };

        // ==================== 배선 ====================

        [SerializeField] private StickConfig _config;

        private StickmanAgent _agent;

        // ==================== 사용자 명시 숨김(2026-09-02) ====================

        /// <summary>
        /// ★ 이 행이 <b>무엇을 가리는지</b> 한 줄로. 누르기 전에 읽히는 유일한 자리다.
        ///
        /// ============================================================================
        /// ★★★ 2026-09-03 — 옛 문장은 <b>두 군데가 거짓</b>이 됐다
        /// ============================================================================
        /// 옛 문장: <i>"캐릭터도 열린 창도 함께 사라져요. 다시 부르려면 ⌃⌥⌘K — 이 방법뿐입니다."</i>
        /// <list type="number">
        ///   <item><b>"열린 창도 함께 사라져요"</b> — 이제 사라지지 않는다. 사용자 명시 숨김은
        ///     캐릭터만 가린다(<c>StickmanAgent.HidesScreenSurfaces</c>, 사용자 확정 <i>"캐릭만 가리고"</i>).</item>
        ///   <item><b>"이 방법뿐입니다"</b> — 톱니도 이 창도 남으므로 마우스 경로가 살아 있다.</item>
        /// </list>
        /// <b>유일성 주장을 다시 쓰지 마라.</b> 그 형태가 이번 신고에서 실제로 무너졌다 —
        /// 사용자는 <i>"전부 다 없어져버려서 다시 나오게 할 방법이 없어"</i>라고 신고했고, 경고는
        /// <b>누르기 직전</b>에만 읽히는데 필요한 순간은 <b>누른 직후</b>였다. 그때 화면엔 아무것도 없었다.
        /// 이제 그 순간에도 <b>이 창과 [보이기] 버튼이 그대로 남는다</b> — 문장이 아니라 구조가 고쳤다.
        ///
        /// <para><b>단축키는 여기 적지 않는다</b>: 같은 행의 <c>hotkey:</c> 칩이 이미 보여 준다
        /// (한 정보를 두 번 적으면 옮기는 날 한쪽만 낡는다).</para>
        /// </summary>
        private const string HideEscapeCaption =
            "캐릭터만 사라져요. 이 창도 톱니도 남으니 옆의 [보이기]로 되돌립니다.";

        // ★ 2026-09-03 — 여기 있던 <c>_manualHideToggle</c>(숨김 상태를 <b>표시</b>하던 토글)을 지웠다.
        //   같은 상태를 조작하는 컨트롤이 둘이면 사용자가 <b>둘의 관계</b>부터 풀어야 하고, 그 오독이
        //   실제로 발생했다(BuildGeneralTab의 「두 행을 하나로 합쳤다」 절). 지금 숨김 상태는
        //   화면에 <b>캐릭터가 있느냐 없느냐</b>로 직접 보이고, 되돌리는 법은 캡션과 단축키 칩에 있다.

        // ★★★ 2026-09-03 — 여기 있던 <c>_manualHideGate</c>(전역 단축키를 쓸 수 없는 환경에서 [숨기기]
        //   행 자체를 잠그던 게이트)를 <b>지웠다</b>. 그 게이트의 사유 문구는
        //   <i>"숨기면 되돌릴 방법이 없어서 잠가 두었습니다"</i>였고, 그 전제가 통째로 사라졌다 —
        //   이제 숨겨도 이 창과 톱니가 남아 [보이기]가 손에 닿는다(HidesScreenSurfaces).
        //   전제가 거짓이 된 잠금을 남기면 <b>화면이 거짓말을 하면서 기능까지 막는다</b>.
        //   되살리지 마라 — 되살리려면 먼저 "표면도 함께 걷는다"로 되돌아가야 한다.
        private IGlobalPointerButtonService _buttonService;

        private Canvas _canvas;
        private CanvasScaler _scaler;
        private RectTransform _panel;
        private BoxCollider2D _clickBlocker;
        private RectMask2D[] _masks = System.Array.Empty<RectMask2D>();

        private RectTransform _closeRect;

        /// <summary>드래그 손잡이 — <b>헤더 48pt</b>에서 <see cref="_closeRect"/>를 뺀 나머지.
        /// 판정은 <see cref="TryBeginWindowDrag"/> 한 곳이고, 기구는 세 창이 공유한다
        /// (<see cref="UiWindowDrag"/>). 2026-09-07 사용자 요청 PART1-1.</summary>
        private RectTransform _headerRect;

        /// <summary>손잡이에서 빼는 사각형들 — <b>배열을 도는</b> 형태로 둔다(헤더에 컨트롤을
        /// 하나 더 넣는 사람이 여기를 잊으면 그 컨트롤을 누를 때마다 창이 끌려간다).
        /// 지금 헤더에 있는 것은 제목(raycast 대상이 아니다)과 [✕]뿐이다.</summary>
        private RectTransform[] _headerNonDragRects = System.Array.Empty<RectTransform>();

        /// <summary>창을 손으로 옮기는 기구(정보창·집중 팝오버와 <b>같은 한 벌</b>).</summary>
        private readonly UiWindowDrag _windowDrag = new UiWindowDrag(UiWindowId.Settings);

        private readonly RectTransform[] _tabRects = new RectTransform[TabCount];
        private readonly Text[] _tabLabels = new Text[TabCount];
        private readonly Image[] _tabUnderlines = new Image[TabCount];
        private readonly RectTransform[] _pages = new RectTransform[TabCount];
        private readonly float[] _pageHeights = new float[TabCount];
        private readonly float[] _pageScroll = new float[TabCount];
        private RectTransform _viewport;
        private RectTransform _pageUpRect;
        private RectTransform _pageDownRect;

        private readonly SettingsControlHost _host = new SettingsControlHost();

        // 값이 바뀌면 화면을 다시 칠해야 하는 부품들(다른 UI가 같은 값을 바꿀 수 있다).
        /// <summary>[캐릭터] 탭의 이름 칸(2026-09-06). 값은 <b>들고 있지 않다</b> —
        /// <see cref="CharacterProgressionModel.CharacterName"/>이 유일한 소스이고
        /// <see cref="SyncNameField"/>가 그것을 이 칸에 옮겨 적는다.</summary>
        private SettingsTextField _nameField;
        private SettingsSlider _scaleSlider;
        private SettingsSwatchRow _inkSwatches;
        // ★ 2026-09-05 — _gearIconToggle / _gearWarnCaption 을 지웠다(BuildGeneralPage의 「화면 위 UI」
        //   카드 주석에 사유 전문이 있다). 필드만 남기면 다음 사람이 «어디서 세우던 값인가»를 다시 찾는다.
        private SettingsToggle _autoHideToggle;

        /// <summary>「음악이 나오면 춤추기」(2026-09-06). ★ <b>세이브에 내려가지 않는</b> 유일한 토글이라,
        /// <see cref="RefreshAll"/>도 저장 값이 아니라
        /// <see cref="AudioReactiveDanceGate.MutedForThisSession"/>를 <b>부정</b>해서 읽는다.</summary>
        private SettingsToggle _danceToggle;

        private SettingsToggle _bubbleToggle;
        private SettingsSlider _fontSizeSlider;
        private SettingsSegment _visibleLengthSegment;
        private SettingsSlider _chatterSlider;

        /// <summary>말풍선을 끄면 함께 무효가 되는 세 행(42-11 판정 G).</summary>
        private SettingsRowGate _speechGate;

        /// <summary>[톱니 위치] 행의 게이트 — 아직 한 번도 옮긴 적이 없으면 되돌릴 것이 없다.
        /// <b>사유 한 줄</b>(docs/UX_FLOW.md C16)이 그 자리에 뜬다(35-1-7 "회색 + 사유").</summary>
        private SettingsRowGate _gearHomeGate;

        /// <summary>[처음 자리로] 버튼의 면 — 테스트가 그 자리를 좌표로 물을 수 있게 붙잡아 둔다.</summary>
        private Image _gearHomeButton;
        private Image _quitSurface;
        private RectTransform _quitRect;
        private Text _quitLabel;
        private Text _footerLeft;

        /// <summary>종료 버튼의 평상시 문구. 만드는 곳(<c>AddButtons</c>)과 되돌리는 곳
        /// (<c>ApplyQuitStyle</c>) 두 군데가 <b>같은 문자열</b>을 각자 적고 있었다 — 한쪽만 고치면
        /// "정말 종료?"에서 되돌아올 때 문구가 달라진다. 단축키 표기가 플랫폼별로 갈리면서
        /// 그 위험이 실제가 되므로 한 곳으로 합친다.</summary>
        private static string QuitLabelText => $"지금 종료 ({ShortcutLabel.Chord("Q")})";

        /// <summary>2단 확인 중의 문구. ★ 2026-09-03 — 같은 이유로 상수화했다: 이 문자열이
        /// <see cref="ApplyQuitStyle"/>에만 있으면 <b>칩 폭을 재는 쪽이 볼 수 없어서</b> 폭 계산이
        /// 평상시 문구만 담게 된다(문안이 길어지는 날 조용히 넘친다).</summary>
        private const string QuitConfirmText = "정말 종료?";

        private bool _open;
        private Tab _tab = Tab.General;
        private float _clickPollTimer;
        private bool _leftPrev;
        private bool _leftInitialized;
        private int _dragIndex = -1;

        /// <summary>커서가 이 창 위에 있었거나 조작이 있었던 마지막 시각(프레임 페이싱 홀드용).
        /// <see cref="TickFramePacingHold"/> 참고.</summary>
        private float _lastSurfaceTouchTime = float.NegativeInfinity;

        private string _lastActionKey;
        private float _lastActionTime;
        private bool _quitArmed;
        private float _quitArmedAt;
        private bool _saveRequested;

        // 부채꼴 참조는 더 이상 이 창이 들고 있지 않다 — 배타 규칙의 집행이 ExclusiveSurfaces로
        // 옮겨가면서 여기서 부채꼴을 직접 아는 이유가 사라졌다(닫을 대상을 손으로 적지 않는다).
        private CharacterInfoWindow _infoWindow;

        /// <summary>설정창을 열 때 <b>내가 닫은</b> 정보창이 있었는가 — 닫을 때 그 자리로 돌려보내기 위해.
        /// 자세한 이유는 <see cref="RestoreInfoWindowIfNeeded"/>.</summary>
        private bool _restoreInfoWindowOnClose;

        private static readonly Vector3[] _corners = new Vector3[4];

        // ==================== 진단/테스트용 공개 상태 ====================

        public bool IsOpen => _open;

        // ★ 배타 표면 등록(2026-09-01) — 정보창과 같은 배선. 이 한 줄이 없어서 <c>I</c>를 눌러도
        //   설정창이 정보창 위에 남아 있었다(사용자 신고 "케릭터창도 겹쳐서보이는 문제있고").
        bool IExclusiveSurface.IsSurfaceOpen => _open;
        void IExclusiveSurface.CloseSurface(string reason) => Close(reason);
        public bool IsCanvasActive => _canvas != null && _canvas.gameObject.activeSelf;
        public bool IsClickBlockerEnabled => _clickBlocker != null && _clickBlocker.enabled;

        /// <summary>차단막(BoxCollider2D)이 실제로 덮고 있는 월드 영역 — <b>비침해(원칙 2) 실측 창구</b>.
        /// <para>★ 2026-09-02부터 창 밖 클릭이 창을 닫지 않으므로 이 사각형은 <b>사용자가 [✕]를 누를
        /// 때까지</b> 남는다. 그래서 "패널 사각형에서 한 픽셀도 넓지 않다"가 예전보다 훨씬 중요해졌다 —
        /// 테스트가 이 값을 패널 화면 사각형과 직접 대조한다.</para></summary>
        public Bounds ClickBlockerWorldBounds
            => _clickBlocker != null && _clickBlocker.enabled ? _clickBlocker.bounds : default;

        public Tab ActiveTab => _tab;
        public Vector2 PanelSizePoints => _panel != null ? _panel.sizeDelta : Vector2.zero;

        // ==================== 창 이동 진단/테스트 창구 (2026-09-07) ====================

        /// <summary>창의 현재 위치(화면 중앙 원점, 캔버스 포인트) — 정보창의 같은 이름 창구와 같은 계다.</summary>
        public Vector2 PanelOffsetPoints => _panel != null ? _panel.anchoredPosition : Vector2.zero;

        /// <summary>드래그 손잡이(<b>헤더</b>)의 화면 사각형. 실제로 끌리는 자리는 여기서 [✕]를 뺀
        /// 나머지이고, 그 판정은 <see cref="TryBeginWindowDrag"/> 한 곳에 있다.</summary>
        public Rect HeaderScreenRect => SettingsControlHost.ScreenRectOf(_headerRect);

        /// <summary>헤더 안에서 <b>드래그가 시작되지 않는</b> 자식들의 사각형 — 테스트가 "빈 자리"를
        /// 고를 때 이 목록을 피한다(좌표를 손으로 적으면 헤더에 컨트롤이 늘 때 엉뚱한 곳을 누른다).</summary>
        public Rect[] HeaderNonDragRectsForTests()
        {
            var rects = new Rect[_headerNonDragRects.Length];
            for (int i = 0; i < rects.Length; i++)
                rects[i] = SettingsControlHost.ScreenRectOf(_headerNonDragRects[i]);
            return rects;
        }

        /// <summary>지금 헤더를 잡고 있는가(문턱을 넘기 전에도 true — "조작 중"의 정의다).</summary>
        public bool IsDraggingWindow => _windowDrag.IsGrabbed;

        /// <summary>테스트 전용 — 버튼 상태와 커서를 <b>실제 입력과 같은 처리 경로</b>에 먹인다
        /// (드래그는 누름/이동/뗌의 연속이라 단발 클릭 진입점으로는 재현할 수 없다).</summary>
        public void FeedPointerForTests(bool buttonDown, Vector2 cursorUnityScreen)
        {
            if (_open) ProcessPointer(buttonDown, cursorUnityScreen, hasCursor: true);
        }

        /// <summary>설정창이 지금 보여주고 있는 캐릭터 배율 — 테스트가 "두 UI가 같은 값을 가리키는가"를
        /// 확인하는 창구다(원칙 1).</summary>
        public float DisplayedCharacterScale => _scaleSlider != null ? _scaleSlider.Value : 0f;

        public Rect CharacterScaleTrackScreenRect => _scaleSlider != null
            ? SettingsControlHost.ScreenRectOf(_scaleSlider.TrackHitRect)
            : new Rect();

        public Rect TabScreenRect(Tab tab) => SettingsControlHost.ScreenRectOf(_tabRects[(int)tab]);

        /// <summary>탭 <b>버튼</b>의 지금 글자색 — "준비 중인 탭은 누르기 전에도 흐리다"(M7)를
        /// 회귀 테스트가 색 상수를 다시 적지 않고 확인하는 창구다.</summary>
        public Color TabLabelColor(Tab tab)
        {
            Text label = _tabLabels[(int)tab];
            return label != null ? label.color : Color.clear;
        }

        /// <summary>이 탭에 내용이 있는가(진단/테스트용 공개 — 판정은 <see cref="IsTabReady"/> 하나뿐이다).</summary>
        public static bool IsTabImplemented(Tab tab) => IsTabReady(tab);

        public Rect CloseButtonScreenRect => SettingsControlHost.ScreenRectOf(_closeRect);

        /// <summary>창 전체의 화면 사각형 — "창 밖"이 화면 안에 실제로 존재하는지 테스트가 확인하는 창구다
        /// (배치모드의 좁은 화면에서는 720×560 패널이 화면을 넘어 <b>바깥이 없을 수</b> 있다).</summary>
        public Rect PanelScreenRect => SettingsControlHost.ScreenRectOf(_panel);

        /// <summary>[▲]/[▼] 페이지 칩과 내용 영역의 화면 사각형 — "칩이 내용 위에 앉지 않는다"(소은 #7-b)를
        /// 회귀 테스트가 좌표로 확인하는 창구다. 칩이 꺼져 있어도 사각형은 나온다(그 자리를 재는 것이 목적).</summary>
        /// <summary>푸터 [지금 종료] 버튼의 화면 사각형 — <b>탈출구가 지금 화면에 있는가</b>를 재는
        /// 유일한 창구(원칙 4). 좌표를 손으로 적어 두면 푸터가 한 번 움직일 때 조용히 엉뚱한 곳을 잰다.</summary>
        public Rect QuitButtonScreenRect => SettingsControlHost.ScreenRectOf(_quitRect);

        public Rect PageUpScreenRect => SettingsControlHost.ScreenRectOf(_pageUpRect);

        public Rect PageDownScreenRect => SettingsControlHost.ScreenRectOf(_pageDownRect);

        public Rect ContentViewportScreenRect => SettingsControlHost.ScreenRectOf(_viewport);

        /// <summary>
        /// 그 탭 페이지가 뷰포트를 <b>몇 pt 넘치는가</b>(0이면 스크롤할 것이 없다).
        ///
        /// <para>★ 2026-09-03 신설. <c>SettingsDisabledSurfaceTests</c>가 "[일반] 탭은 내용이 넘친다"를
        /// <b>전제</b>로 깔고 [▲][▼]의 죽은/산 잉크를 검사하는데, 그 전제가 참인지는 지금까지
        /// <b>아무도 숫자로 못 봤다</b>. 넘침이 0이 되는 날 그 테스트는 실패하지 않고 <b>공허해진다</b> —
        /// 이 저장소가 가장 자주 당한 형태다. 여기서 여유를 pt로 내주면 그 전제를 <b>단언</b>할 수 있다.</para>
        /// </summary>
        public float PageOverflowPointsForTests(Tab tab)
            => Mathf.Max(0f, _pageHeights[(int)tab] - ContentHeight);

        /// <summary>[톱니 위치] &gt; [처음 자리로] 버튼의 화면 사각형 — 회귀 테스트가 <b>실제 클릭 경로</b>로
        /// 이 행을 누르는 창구. 좌표를 손으로 적으면 카드가 한 줄 늘어날 때 조용히 엉뚱한 곳을 누른다.</summary>
        public Rect GearHomeButtonScreenRect => _gearHomeButton != null
            ? SettingsControlHost.ScreenRectOf(_gearHomeButton.rectTransform)
            : new Rect();

        /// <summary>[톱니 위치] 행이 지금 눌리는 상태인가(테스트/진단). 옮긴 적이 없으면 false다.</summary>
        public bool GearHomeRowEnabledForTests => _gearHomeGate != null && _gearHomeGate.Enabled;

        /// <summary>잉크 스와치의 화면 사각형(0=검정, 1=흰색). 테스트가 "배포 에셋을 건드리지 않는가"를
        /// <b>실제 클릭 경로로</b> 확인하기 위해 필요하다 — 좌표를 손으로 적으면 레이아웃이 바뀔 때
        /// 조용히 엉뚱한 곳을 누른다.</summary>
        public Rect InkSwatchScreenRect(int index)
            => _inkSwatches != null && _inkSwatches.Rects != null && index >= 0 && index < _inkSwatches.Rects.Length
                ? SettingsControlHost.ScreenRectOf(_inkSwatches.Rects[index])
                : new Rect();

        /// <summary>말풍선 글자 크기 슬라이더의 [+] 버튼 사각형(테스트 전용).</summary>
        public Rect DialogueFontSizePlusScreenRect => _fontSizeSlider != null
            ? SettingsControlHost.ScreenRectOf(_fontSizeSlider.PlusRect)
            : new Rect();

        /// <summary>`대사 표시 시간` 세그먼트 i번 칸의 사각형(테스트 전용).</summary>
        public Rect DialogueVisibleLengthSegmentScreenRect(int index)
            => _visibleLengthSegment != null && _visibleLengthSegment.Rects != null
               && index >= 0 && index < _visibleLengthSegment.Rects.Length
                ? SettingsControlHost.ScreenRectOf(_visibleLengthSegment.Rects[index])
                : new Rect();

        /// <summary>`말풍선 표시` 토글의 사각형(테스트 전용).</summary>
        public Rect DialogueBubbleToggleScreenRect => _bubbleToggle != null
            ? SettingsControlHost.ScreenRectOf(_bubbleToggle.HitRect)
            : new Rect();

        /// <summary>말풍선을 끄면 함께 무효가 되는 세 행이 지금 활성인가(테스트 전용).</summary>
        public bool SpeechRowsEnabledForTests => _speechGate == null || _speechGate.Enabled;

        /// <summary>테스트 전용 — 실제 입력과 <b>완전히 같은</b> 처리 경로에 커서를 먹인다
        /// (PlayMode는 진짜 전역 클릭을 만들 수 없다. InfoGearIconWidget.FeedPointerForTests와 같은 사정).</summary>
        public void FeedClickForTests(Vector2 cursorUnityScreen) => FeedClick(cursorUnityScreen);

        // ==================== 수명 주기 ====================

        private void Awake()
        {
            _agent = GetComponent<StickmanAgent>();
            if (_config == null && _agent != null) _config = _agent.Config;
            BuildUi();
        }

        private void Start()
        {
            _buttonService = _agent != null ? _agent.PlatformService as IGlobalPointerButtonService : null;
            CharacterScaleController.Bind(_agent);
            RefreshAll();

            Debug.Log($"[설정창] 준비 완료({PanelWidth:F0}×{PanelHeight:F0}, 탭 5개: " +
                $"{string.Join("/", TabNames)}) — 여는 방법: (1) 정보창 헤더의 작은 톱니, " +
                $"(2) 전역 단축키 **{ShortcutLabel.Chord("P")}**. 이번 라운드는 [일반][캐릭터]만 내용이 있습니다. " +
                $"전역 폴링 경로={(_buttonService != null ? "사용 가능" : "미지원 — uGUI 경로만")}.");
            LogRoadmapNotes();
        }

        /// <summary>
        /// ★ 2026-09-01 — 미구현 행의 <b>내부 사정</b>은 여기(로그)에만 적는다.
        ///
        /// <para>화면의 사유 캡션이 "GlobalKey에 V가 없어 다음 라운드에 배선합니다" / "35-1-9 P3"처럼
        /// <b>개발자끼리 쓰는 말</b>을 그대로 렌더하고 있었다(페르소나 M6). 사용자는 GlobalKey도
        /// 35-1-9도 모르고, 읽히는 것은 "이 앱 미완성이구나" 하나다. 그렇다고 그 정보를 지우면 팀이
        /// 로드맵을 잃으므로, <b>독자를 나눈다</b> — 화면에는 사용자 문장, 로그에는 내부 식별자.</para>
        ///
        /// <para>이 규칙의 집행은 <c>SettingsUserFacingCopyTests</c>가 한다 — 다섯 탭의 <b>렌더된
        /// 문자열 전부</b>를 훑어 라틴 식별자/이슈번호/개발 어휘를 찾는다(비활성 탭 포함). 런타임에
        /// 문자열을 검사해 몰래 걸러내는 방식은 쓰지 않았다 — "⌃⌥⌘I" 같은 정당한 사용자 문구까지
        /// 오탐으로 망가뜨릴 수 있고, 조용히 고쳐 주는 방어는 다음 사람이 규칙을 배우지 못하게 한다.</para>
        /// </summary>
        private static void LogRoadmapNotes()
        {
            // ★ 2026-09-02 — "[일반] 숨기기/보이기 단축키 ⌃⌥⌘V = 배선 대기" 줄을 지웠다. 그 행은 이제
            //   실제로 동작한다(GlobalKey.K). 잠긴 행 목록에 남겨두면 이 로그 자체가 거짓이 된다.
            Debug.Log("[설정창/로드맵] 지금 회색으로 잠긴 행들의 내부 사정(사용자 화면에는 나오지 않습니다): " +
                "[일반] 로그인 자동 실행 = 네이티브 로그인 항목 등록(35-1-9 P3) / " +
                "[캐릭터] 포인트 컬러 팔레트 = 회의록 6 소관 / " +
                "[캐릭터] 말투(반말·존댓말) = 대사 두 벌 작성(35-3-3) / " +
                "[이벤트][접근성·성능][데이터] 3탭 = 35-1-9 P1/P2.");
        }

        private void OnEnable()
        {
            StickmanEventBus.CharacterScaleChanged += OnCharacterScaleChanged;
            // 이름은 이 창 밖(정보창 인라인 편집)에서도 바뀐다 — 그때 이 칸이 낡은 값을 들고 있으면
            // 두 창이 같은 캐릭터를 다른 이름으로 부른다.
            StickmanEventBus.CharacterProgressionChanged += OnProgressionChanged;
        }

        private void OnDisable()
        {
            StickmanEventBus.CharacterScaleChanged -= OnCharacterScaleChanged;
            StickmanEventBus.CharacterProgressionChanged -= OnProgressionChanged;
            // 창이 꺼진 채 차단막만 남으면 그 화면 영역이 이유 없이 클릭관통 해제로 남는다(비침해).
            if (_clickBlocker != null) _clickBlocker.enabled = false;
        }

        private void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
            if (_clickBlocker != null) Destroy(_clickBlocker.gameObject);
        }

        // ==================== 공개 진입점 ====================

        public void Toggle(string source)
        {
            if (_open) Close(source);
            else Open(source);
        }

        public void Open(string source) => Open(source, userInitiated: true);

        /// <summary>
        /// ★★★ 2026-09-03 — <c>userInitiated</c>가 갈라 놓는 것은 <b>등급 1 탈출구의 허가</b> 하나다.
        ///
        /// <para><b>true(사용자가 열었다)</b>: 전역 단축키, 정보창 [설정] 칩, 부채꼴 경로.
        /// 등급 1 체류 중이면 <see cref="StickmanAgent.TryGrantUserSummon"/>으로 허가를 받아
        /// <b>이 창이 열리자마자 자기 Update에 닫히는 루프</b>를 연다. 이 창이 등급 1을 끄는
        /// <b>유일한 스위치</b>를 들고 있으므로, 이 한 줄이 없으면 등급 1은 끌 수 없는 상태로 남는다.</para>
        ///
        /// <para><b>false(우리가 되돌린다)</b>: <see cref="TickReopenAfterSuspend"/>의 자동 복귀.
        /// 사용자의 <b>새</b> 행위가 아니므로 허가를 내지 않는다. 애초에 그 경로는
        /// <c>!ArePanelsSuppressed</c>일 때만 실행되므로 허가가 필요하지도 않다 — 여기서 허가를 내면
        /// "우리가 스스로에게 발급하는 면제"가 되어 원칙 2의 구멍이 열린다.</para>
        /// </summary>
        private void Open(string source, bool userInitiated)
        {
            if (_open) return;

            // ★ 열기 <b>전에</b> 허가를 받는다. 이 창의 Update는 첫 프레임에 ArePanelsSuppressed를
            //   보고 닫으므로, 허가가 늦으면 "열렸다가 같은 프레임에 닫힌" 로그 두 줄만 남는다.
            if (userInitiated && _agent != null) _agent.TryGrantUserSummon($"설정창 열기({source})");

            _open = true;
            DisarmReopenAfterSuspend();
            _leftInitialized = false;   // 창을 여는 그 클릭이 곧바로 행 클릭으로 오인되지 않게.
            // 여는 그 순간은 정의상 조작 중이다 — 첫 커서 폴링(최대 0.05초)까지의 공백을 메운다.
            _lastSurfaceTouchTime = Time.unscaledTime;
            _dragIndex = -1;
            // ★ 2026-09-07 — 옮긴 적이 있으면 그 자리에서, 없으면 예전 그대로 화면 중앙에서 연다.
            RestorePanelPosition();
            DisarmQuit();
            CloseOverlappingSurfaces($"설정창 열림({source})");
            if (_canvas != null) _canvas.gameObject.SetActive(true);
            if (_clickBlocker != null) _clickBlocker.enabled = true;
            RefreshAll();
            Debug.Log($"[설정창] 열림({source}) — 탭=[{TabNames[(int)_tab]}]. " +
                "[✕]로 닫힙니다(창 밖 클릭·ESC는 닫지 않습니다).");
        }

        public void Close(string source)
        {
            if (!_open) return;
            _open = false;
            _dragIndex = -1;
            // ★ 확정하지 않고 놓는다 — 닫히는 창의 마지막 좌표를 저장하면 "옮긴 적 없는데 자리가
            //   바뀌었다"가 된다(전체화면 자동 숨김도 이 경로로 들어온다).
            _windowDrag.Cancel();
            // 사용자가 직접 닫았으면 "전체화면이 지나가면 돌려놓는다" 예약은 뜻을 잃는다.
            // (전체화면 경로는 이 호출 <b>뒤에</b> 다시 무장한다.)
            DisarmReopenAfterSuspend();
            DisarmQuit();
            FlushPendingSave();
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            if (_clickBlocker != null) _clickBlocker.enabled = false;
            Debug.Log($"[설정창] 닫힘({source}).");
            RestoreInfoWindowIfNeeded(source);
        }

        /// <summary>
        /// ★ 2026-09-01 — 설정창을 닫으면 <b>내가 밀어낸</b> 정보창을 그 자리로 돌려보낸다(페르소나 M8).
        ///
        /// <para>배타 규칙(<see cref="CloseOverlappingSurfaces"/>) 자체는 옳다. 문제는 <b>돌아갈 문이
        /// 없었다</b>는 것이다: 장비를 구경하다 [설정]을 누르면 정보창이 사라지고, [✕]를 누르면 빈
        /// 바탕화면만 남아 톱니 → [캐릭터]를 처음부터 다시 밟아야 했다. 진입 경로가 정보창 헤더의
        /// [설정] 칩 하나뿐인 창에서 이건 막다른 길이다.</para>
        ///
        /// <para>그래서 <b>시트(sheet)</b>처럼 행동하게 한다 — 얹힐 때 가려진 부모 창은 걷힐 때 돌아온다.
        /// 어떤 경로로 닫혔는지(=[✕]/단축키 재입력)는 구분하지 않는다. 문자열 <c>source</c>로
        /// 분기하면 새 진입점이 생길 때마다 조용히 어긋나고, 사용자가 배우는 규칙도 하나여야 한다.</para>
        ///
        /// <para><b>단 하나의 예외는 전체화면 감지</b>다(원칙 2). 그 경로에서 정보창을 되살리면 게임 위에
        /// 방금 치운 창을 다시 얹는 셈이라 자동 숨김의 목적 자체가 뒤집힌다. <see cref="StickmanAgent.ArePanelsSuppressed"/>를
        /// 직접 보므로 호출부가 그 사실을 잊어도 안전하다(문자열 사유에 기대지 않는다).</para>
        /// </summary>
        private void RestoreInfoWindowIfNeeded(string source)
        {
            bool restore = _restoreInfoWindowOnClose;
            _restoreInfoWindowOnClose = false;
            if (!restore) return;

            if (_agent != null && _agent.ArePanelsSuppressed)
            {
                Debug.Log("[설정창] 정보창 복귀를 건너뜁니다 — 전체화면이 감지된 상태입니다(원칙 2). " +
                    "사용자가 부르지 않은 창이 게임 위로 돌아오는 것이 자동 숨김보다 나쁩니다.");
                return;
            }

            if (_infoWindow == null) _infoWindow = GetComponent<CharacterInfoWindow>();
            if (_infoWindow == null || _infoWindow.IsOpen) return;
            _infoWindow.Open($"설정창 닫힘({source}) — 열기 전에 보던 창으로 복귀");
        }

        /// <summary>
        /// 배타적 모달 — 이 창이 뜨면 <b>다른 모든 배타 표면</b>을 거둔다.
        /// <see cref="CharacterInfoWindow.CloseOverlappingSurfaces"/>와 <b>같은 규약</b>이며,
        /// 이제는 같은 규약이 아니라 <b>같은 코드</b>다(<see cref="ExclusiveSurfaces.CloseAllExcept"/>) —
        /// "규약"으로만 묶여 있던 동안 한쪽에만 상대가 등록돼 있어서 <c>I</c> 방향만 새고 있었다.
        ///
        /// <para>여기 남는 유일한 고유 로직은 <b>시트 복귀 예약</b>(M8)이다. 이건 배타 규칙이 아니라
        /// 이 창만의 성격이라 공통 집행 지점에 올리지 않는다.</para>
        /// </summary>
        private void CloseOverlappingSurfaces(string reason)
        {
            if (_infoWindow == null) _infoWindow = GetComponent<CharacterInfoWindow>();
            // 닫기 <b>전에</b> 기억한다 — 이 한 줄이 없으면 되돌아갈 자리가 사라진다(M8).
            _restoreInfoWindowOnClose = _infoWindow != null && _infoWindow.IsOpen;

            ExclusiveSurfaces.CloseAllExcept(this, reason);
        }

        // ==================== 루프 ====================

        private void Update()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.UiWindows);   // [스톨구간] 계측

            // ★ 닫혀 있을 때도 <b>이 한 줄</b>은 돈다 — 무장돼 있지 않으면 즉시 빠진다(float 비교 1회).
            TickReopenAfterSuspend();

            if (!_open) return;

            // ★★★ 2026-09-03 — 등급 1 탈출구의 <b>임대 갱신</b>. 반드시 아래 회수 가드 <b>앞</b>이다.
            //   이 창은 톱니 경로의 <b>마지막 홉</b>이라, 여기까지 오면 부채꼴도 정보창도 이미 닫혀
            //   앞 홉의 갱신자가 사라진다. 갱신이 끊기면 임대는
            //   UserSurfaceSummonPolicy.LeaseSeconds 안에 만료되어 이 창이 스스로 닫힌다 —
            //   즉 <b>사용자가 설정을 만지는 도중에</b> 창이 사라진다(고치려던 바로 그 증상).
            //   ★ 갱신은 만료된 임대를 되살리지 않으므로(정책 문서) 이 줄이 스스로에게 면제를
            //     발급하는 경로는 아니다 — 허가는 Open(userInitiated: true)에서만 난다.
            if (_agent != null) _agent.RenewUserSummonGrant();

            // ★★ 원칙 2 — 전체화면 게임이 감지되면 창과 차단막을 그 프레임에 거둔다. 복귀 시 자동으로
            //    다시 열지 않는다(정보창/팝오버와 같은 판단 — 사용자가 부르지 않은 창이 게임을 끄자마자
            //    튀어나오면 그 자체가 방해다).
            // ★★★ 2026-09-02 — <c>ArePanelsSuppressed</c>(등급 1 포함). 설정창은 720x560 차단막을
            //    소유하므로 게임이 아닌 전체화면 앱 위에서도 그 사각형의 클릭을 먹었다.
            if (_agent != null && _agent.ArePanelsSuppressed)
            {
                // 복귀 예약을 여기서 명시적으로 지운다 — RestoreInfoWindowIfNeeded의 같은 가드와
                // 이중이지만, 둘 중 하나가 사라져도 전체화면 앱 위에 창이 되살아나지 않는다(원칙 2).
                _restoreInfoWindowOnClose = false;
                Close("전체화면 감지 — 자동 숨김(비침해 원칙 2)");
                // ★ 무장은 Close <b>뒤에</b> — Close()가 "사용자가 닫았다"로 보고 예약을 지운다.
                //   순서를 뒤집으면 이 기능은 <b>영원히 발동하지 않으면서 컴파일도 테스트도 통과</b>한다.
                //
                // ★★★ 2026-09-03 — 무장은 <b>등급 2일 때만</b> 한다(<c>IsSuspended</c>).
                //   등급 1은 이제 <b>톱니 1클릭으로 되돌아올 수 있다</b>(사용자 허가). 그 상태에서까지
                //   예약을 걸면 사용자가 톱니를 눌러 부채꼴을 부른 그 순간, 허가로 회수가 풀리면서
                //   <b>부르지도 않은 설정창이 부채꼴 위로 튀어나온다</b> — 자동 복귀는 "마우스 진입점이
                //   0인 동안"의 안전망이었고, 등급 1에서는 그 전제가 사라졌다.
                //   등급 2(전체화면 게임 / 사용자 명시 숨김)에서는 톱니 자체가 없으므로 그대로 남긴다.
                if (_agent.HidesScreenSurfaces) ArmReopenAfterSuspend();
                return;
            }

            // ★★ 프레임 페이싱 홀드는 TickGlobalPointer() 안에 있다 — "창이 열려 있는 동안"이
            //    아니라 <b>"지금 이 창을 조작 중일 때"</b>만 걸어야 한다(근거: TickFramePacingHold
            //    문서. 정보창이 125분 열린 채 절전을 통째로 죽인 실측 사고와 같은 배선이었다).

            // 배율 적용 유예(랙돌/스펙터클 중) 풀기. ★ 이 창은 <b>구동자가 아니다</b> — 위 Update는
            // `if (!_open) return;`으로 시작하므로 창을 닫으면 여기가 안 돈다. 상시 구동자는
            // CharacterProgressionDirector이고(2026-09-01 구석 패널 삭제로 그쪽이 물려받았다),
            // 여기 한 줄은 "창이 열려 있는 동안 반응이 한 프레임도 늦지 않게" 하는 보조다.
            // 경과 시간 기반이라 두 곳에서 불려도 결과가 같다(멱등).
            CharacterScaleController.Tick();

            ApplyCanvasScaleFactor();
            SyncClickBlocker();
            TickQuitConfirm();
            SyncGearHomeGate();
            TickGlobalPointer();
        }

        private void TickQuitConfirm()
        {
            if (!_quitArmed) return;
            if (Time.unscaledTime - _quitArmedAt < QuitConfirmSeconds) return;
            DisarmQuit();
        }

        private void TickGlobalPointer()
        {
            if (_buttonService == null || _panel == null) return;

            // 홀드 판정도 이 가드 뒤에 있다 — 전역 포인터 서비스가 없으면 커서를 관측할 수단이
            // 자체가 없다. 그 환경(에디터/Null 서비스)에서는 적응형 페이싱도 함께 꺼져 있으므로
            // 홀드가 없어서 생기는 손해가 없다.

            // 드래그(슬라이더 / 창 이동) 중에는 폴링 간격을 없앤다 — 20Hz로 끌면 손잡이도 창도
            // 커서에서 뚝뚝 떨어진다(정보창이 같은 이유로 같은 가드를 쓴다).
            if (_dragIndex < 0 && !_windowDrag.IsGrabbed)
            {
                _clickPollTimer += Time.unscaledDeltaTime;
                if (_clickPollTimer < ClickPollInterval) return;
                _clickPollTimer = 0f;
            }

            Vector2 osScreen = Vector2.zero;
            bool hasCursor = _agent != null && _agent.TryGetCursorPosition(out osScreen);
            Vector2 cursor = hasCursor
                ? ScreenCoordinateConverter.OsScreenToUnityScreen(osScreen, _config)
                : Vector2.zero;

            TickFramePacingHold(hasCursor, cursor);

            if (!_buttonService.TryGetPrimaryButtonPressed(out bool left)) return;
            ProcessPointer(left, cursor, hasCursor);
        }

        /// <summary>
        /// "지금 이 창을 <b>조작 중</b>인가"를 프레임 페이싱에 알린다 —
        /// <see cref="CharacterInfoWindow"/>와 <b>같은 배선</b>이고 판정도 같은 플랫폼 중립 함수
        /// (<see cref="FramePacingPolicy.ShouldHoldForSurface"/>)를 쓴다. 두 창이 서로 다른 규칙을
        /// 갖게 되면 다음 사람이 어느 쪽이 진짜인지 알 수 없다.
        ///
        /// <para><b>왜 바꿨나</b>: 원래 <c>Update()</c>에서 무조건 걸려 있었다. 정보창의 실측
        /// (125분 열림 = 등급 전이 0회, 활성 등급 체류 100%)이 그 배선이 적응형 절전을 통째로
        /// 무력화한다는 것을 확정했고, 이 창은 <b>같은 패턴</b>이었다. 슬라이더 드래그가 끊기지
        /// 않는 이유는 <c>_dragIndex</c>가 그 자체로 "조작 중"이기 때문이다 — 커서가 창 밖으로
        /// 나가도 홀드가 유지된다.</para>
        /// </summary>
        private void TickFramePacingHold(bool hasCursor, Vector2 cursor)
        {
            // _quitArmed는 "정말 종료?"가 떠 있는 몇 초 — 그 순간의 클릭이 굼뜨면 안 된다.
            // ★ 창을 잡고 끄는 중도 조작이다(2026-09-07) — 커서가 창 밖으로 나가도 이어지므로
            //   아래 사각형 판정만으로는 못 잡는다. 슬라이더 드래그와 정확히 같은 사정이다.
            bool manipulating = _dragIndex >= 0 || _quitArmed || _windowDrag.IsGrabbed;
            bool cursorOver = hasCursor && RectContainsScreenPoint(_panel, cursor);
            if (manipulating || cursorOver) _lastSurfaceTouchTime = Time.unscaledTime;

            if (FramePacingPolicy.ShouldHoldForSurface(cursorOver, manipulating,
                    Time.unscaledTime - _lastSurfaceTouchTime))
            {
                FramePacing.HoldActiveForInteraction();
            }
        }

        /// <summary>실제 입력과 테스트가 <b>공유하는</b> 포인터 처리(정보창과 같은 관례).</summary>
        private void ProcessPointer(bool buttonDown, Vector2 cursor, bool hasCursor)
        {
            bool prev = _leftPrev;
            if (!_leftInitialized)
            {
                _leftInitialized = true;
                _leftPrev = buttonDown;
                return;
            }
            _leftPrev = buttonDown;

            if (buttonDown && !prev)
            {
                if (!hasCursor) return;
                // ★ 헤더의 빈 자리를 잡았으면 클릭 처리로 넘기지 않는다(정보창과 같은 순서).
                //   손잡이는 «누를 때 아무 일도 일어나지 않는 자리»라 클릭을 삼켜도 잃는 것이 없다.
                if (TryBeginWindowDrag(cursor)) return;
                FeedClick(cursor);
                return;
            }
            if (buttonDown && _windowDrag.IsGrabbed)
            {
                if (hasCursor) DragWindowTo(cursor);
                return;
            }
            if (buttonDown && _dragIndex >= 0)
            {
                if (hasCursor) _host.DragTo(_dragIndex, cursor);
                return;
            }
            if (!buttonDown && prev)
            {
                EndWindowDrag();
                if (_dragIndex >= 0)
                {
                    _dragIndex = -1;
                    FlushPendingSave();   // 드래그가 끝난 시점에 한 번만 디스크를 두드린다.
                }
            }
        }

        // ==================== 창 이동 (2026-09-07 사용자 요청 PART1-1) ====================
        //
        // 사용자 원문: "모든 창(집중모드 타이머, 캐릭터 정보창, 설정창)이 마우스로 끌어도 움직이지
        // 않음 — 전부 드래그 이동 가능해야 함" · "이동한 위치는 창별로 저장되어 재시작 후에도 유지".
        // 이 창에는 <b>드래그 코드가 아예 없었다</b>(정보창에만 있었다). 판정·클램프·저장은 전부
        // <see cref="UiWindowDrag"/> 한 벌이고, 여기 남는 것은 «어디가 손잡이인가»뿐이다.

        /// <summary>헤더 48pt에서 <see cref="_headerNonDragRects"/>를 뺀 자리를 잡았는가.</summary>
        private bool TryBeginWindowDrag(Vector2 cursor)
        {
            if (_headerRect == null || _panel == null) return false;
            if (!RectContainsScreenPoint(_headerRect, cursor)) return false;
            if (UiWindowDrag.AnyContains(_headerNonDragRects, cursor)) return false;

            _windowDrag.Grab(UiWindowDrag.ScreenToCenterOriginPoints(cursor, CanvasScale()),
                _panel.anchoredPosition);
            return true;
        }

        private void DragWindowTo(Vector2 cursor)
        {
            if (_panel == null) return;
            // 문턱(UiWindowDrag.MoveThresholdPoints)을 넘기 전에는 창이 한 픽셀도 움직이지 않는다.
            if (!_windowDrag.TryResolveCenter(
                    UiWindowDrag.ScreenToCenterOriginPoints(cursor, CanvasScale()), out Vector2 desired)) return;

            Vector2 applied = ClampPanelPosition(desired);
            _panel.anchoredPosition = applied;
            _windowDrag.NoteAppliedCenter(applied);   // 저장되는 것은 <b>클램프를 지난</b> 값이다.
        }

        private void EndWindowDrag()
        {
            if (!_windowDrag.IsGrabbed) return;
            bool moved = _windowDrag.Release();
            if (!moved) return;

            Vector2 p = _panel != null ? _panel.anchoredPosition : Vector2.zero;
            Debug.Log($"[설정창] 이동 완료 — 화면 중앙에서 ({p.x:F0}, {p.y:F0})pt 옮긴 자리입니다. " +
                "재시작해도 이 자리에서 열립니다.");
        }

        /// <summary>
        /// ★ 창을 열 때의 자리 — <b>옮긴 적이 있으면 그 자리, 없으면 화면 중앙</b>.
        /// <para>옮긴 적이 없는 사용자에게는 <c>anchoredPosition = Vector2.zero</c>가 되어
        /// <b>예전과 결과가 같다</b>(이 창은 지금까지 빌드 시점의 0에서 한 번도 움직이지 않았다).</para>
        /// <para>클램프를 함께 하는 이유는 정보창과 같다 — 저장된 자리는 <b>다른 화면 크기에서 만든
        /// 값</b>일 수 있고, 창 밖 클릭이 닫지 않는 이 앱에서 화면 밖 창은 <b>닫을 수 없는 창</b>이다.</para>
        /// </summary>
        private void RestorePanelPosition()
        {
            _windowDrag.Cancel();
            if (_panel == null) return;

            // 옮긴 적이 없으면 <b>클램프도 태우지 않는다</b> — 위 ApplyCanvasScaleFactor의 가드와
            // 같은 이유다(720×560 고정 창이라 좁은 화면에서 클램프가 이동을 만든다).
            if (!_windowDrag.TryGetSavedCenter(out Vector2 saved))
            {
                _panel.anchoredPosition = Vector2.zero;   // 예전과 비트 동일.
                return;
            }
            _panel.anchoredPosition = ClampPanelPosition(saved);
        }

        /// <summary>창 전체가 화면(과 OS 예약 띠) 안에 남는 자리로 자른다 — 규칙은 정보창·팝오버와
        /// <b>같은 코드</b>(<see cref="UiWindowDrag.ClampCenterPoints"/>)다.
        /// <para>이 창은 크기가 720×560 고정이라 <b>줄이지 않는다</b>(정보창의 <c>ClampPanelToScreen</c>과
        /// 다른 점). 화면이 그보다 좁으면 가로는 가운데, 세로는 예약 띠를 피해 위쪽에 붙는다 —
        /// 그건 이 라운드가 만든 동작이 아니라 <c>SurfaceSafeAreaPolicy</c>가 이미 정한 규칙이다.</para></summary>
        private Vector2 ClampPanelPosition(Vector2 desired)
        {
            if (_panel == null) return desired;
            float sf = CanvasScale();
            UiWindowDrag.ResolveReservedInsets(_agent, out float topInset, out float bottomInset);
            return UiWindowDrag.ClampCenterPoints(desired, _panel.sizeDelta,
                new Vector2(Screen.width / sf, Screen.height / sf), topInset, bottomInset, ScreenMarginPoints);
        }

        private float CanvasScale()
        {
            float sf = _scaler != null ? _scaler.scaleFactor : 1f;
            return sf > 0f ? sf : 1f;
        }

        /// <summary>화면 가장자리 여백 — <b>숫자를 다시 적지 않는다</b>. 세 창의 단일 출처는
        /// <see cref="UiWindowDrag.ScreenMarginPoints"/>이고 정보창도 같은 값을 참조한다.</summary>
        public const float ScreenMarginPoints = UiWindowDrag.ScreenMarginPoints;

        private void FeedClick(Vector2 cursor)
        {
            if (!_open) return;

            if (!RectContainsScreenPoint(_panel, cursor))
            {
                // ★ 2026-09-02 사용자 지시 — 창 밖 클릭으로는 닫지 않는다(UiChrome "창을 닫는 법").
                //   그 클릭을 <b>먹지도 않는다</b>: 차단막은 패널 사각형만 덮으므로 이 좌표에는
                //   콜라이더가 없고 히트테스트가 그대로 밑의 앱에 넘긴다(원칙 2).
                return;
            }

            if (ContainsScreenPoint(_closeRect, cursor))
            {
                if (TryClaimAction("close")) Close("[✕] 클릭");
                return;
            }

            for (int i = 0; i < TabCount; i++)
            {
                if (!ContainsScreenPoint(_tabRects[i], cursor)) continue;
                if (TryClaimAction("tab" + i)) SetTab((Tab)i, "탭 클릭");
                return;
            }

            // 푸터 [지금 종료]는 카드 행이 아니라 <b>창 크롬</b>이라 _host가 모른다 — [✕]/탭과 같은
            // 층에서 직접 검사한다. 이게 빠지면 비활성 앱의 첫 클릭 경로에서만 종료가 안 먹는다.
            if (ContainsScreenPoint(_quitRect, cursor))
            {
                if (TryClaimAction("quit")) OnQuitClicked();
                return;
            }

            if (ContainsScreenPoint(_pageUpRect, cursor))
            {
                if (CanScroll(-1) && TryClaimAction("pageUp")) ScrollPage(-1);
                return;
            }
            if (ContainsScreenPoint(_pageDownRect, cursor))
            {
                if (CanScroll(+1) && TryClaimAction("pageDown")) ScrollPage(+1);
                return;
            }

            _host.TryClick(cursor, out _dragIndex);
        }

        private bool TryClaimAction(string key)
        {
            if (_lastActionKey == key && Time.unscaledTime - _lastActionTime < ActionDedupSeconds) return false;
            _lastActionKey = key;
            _lastActionTime = Time.unscaledTime;
            return true;
        }

        // ==================== 값 동기화 ====================

        /// <summary>
        /// ★ 35-1-3 ①의 핵심 — 이 창 <b>밖</b>에서 배율이 바뀌면 슬라이더가 <b>같은 프레임에</b>
        /// 따라온다(저장 복원, 유예 해제 후 강제 적용 등). 반대 방향도 같은 이벤트로 흐른다.
        /// 값을 만지는 쪽들이 서로를 모른 채 같은 숫자를 가리키는 것이 이 구조의 목적이다.
        /// </summary>
        private void OnCharacterScaleChanged(CharacterScaleChangeEvent e)
        {
            if (_scaleSlider == null) return;
            _scaleSlider.SetValueSilently(e.Value);
        }

        /// <summary>
        /// ★ 이름 칸을 <b>모델에서</b> 다시 읽는다 — 이 창이 이름을 따로 기억하지 않는 이유다.
        ///
        /// <para>부르는 자리는 셋이고 전부 같은 사실을 말한다: 창을 열 때(<see cref="RefreshAll"/>),
        /// 이 창에서 고쳤을 때(정규화 결과 반영), <b>다른 창에서 고쳤을 때</b>
        /// (<see cref="OnProgressionChanged"/> — 정보창의 인라인 편집·레벨업 복원 경로).</para>
        ///
        /// <para><b>타이핑 중에는 손대지 않는다.</b> 통지가 날아왔다고 칸을 갈아치우면 지금 치고 있는
        /// 글자가 사라진다 — 그리고 그 통지의 출처가 <b>이 칸 자신</b>일 수도 있다.</para>
        /// </summary>
        private void SyncNameField()
        {
            if (_nameField == null || _nameField.IsFocused) return;
            _nameField.SetTextSilently(CharacterProgressionModel.CharacterName);
        }

        /// <summary>이름이 <b>다른 곳</b>에서 바뀌었다(정보창 인라인 편집, 저장 복원). 이 창이 열려
        /// 있으면 즉시 따라간다 — 두 창이 서로 다른 이름을 보여주는 순간을 만들지 않는다.</summary>
        private void OnProgressionChanged() => SyncNameField();

        private void RefreshAll()
        {
            SyncNameField();
            if (_scaleSlider != null) _scaleSlider.SetValueSilently(CharacterScaleController.Value);
            if (_inkSwatches != null) _inkSwatches.SetIndexSilently(_config != null && _config.IsWhiteInk() ? 1 : 0);
            if (_autoHideToggle != null) _autoHideToggle.SetOn(AppSettingsModel.AutoHideOnFullscreen);
            // ★ 저장 값이 아니라 «지금 이 세션의 정적 상태»를 부정해서 읽는다(그 필드 문서 참고).
            //   창을 닫았다 다시 열어도 방금 끈 상태가 그대로 보여야 한다.
            if (_danceToggle != null) _danceToggle.SetOn(!AudioReactiveDanceGate.MutedForThisSession);
            if (_bubbleToggle != null) _bubbleToggle.SetOn(AppSettingsModel.ResolveDialogueBubbleEnabled(_config));
            if (_fontSizeSlider != null) _fontSizeSlider.SetValueSilently(AppSettingsModel.ResolveDialogueFontSize(_config));
            if (_visibleLengthSegment != null)
                _visibleLengthSegment.SetIndexSilently((int)AppSettingsModel.DialogueVisibleLength);
            if (_chatterSlider != null) _chatterSlider.SetValueSilently(AppSettingsModel.ChatterPercent);
            SyncSpeechGate();
            SyncGearHomeGate();
            ApplyTabVisibility();
        }

        // ★★★ 2026-09-03 — 여기 있던 SyncManualHide()를 지웠다. 하는 일이 «전역 단축키를 쓸 수
        //   있는 환경인가»로 [숨기기] 행을 잠그는 것 하나뿐이었는데, 그 잠금의 사유가 거짓이 됐다
        //   (위 HideEscapeCaption 절). 남길 상태가 0개라 함수께 지운다 — 빈 함수를 남기면
        //   다음 사람이 «무엇을 동기화하던 자리인가»를 다시 조사해야 한다.


        /// <summary>★ 42-11 G — <c>말풍선 표시</c>가 꺼져 있으면 그 아래 세 행은 만져도 화면이 바뀌지
        /// 않는다. 활성인 채로 두면 "컨트롤이 움직이는데 화면이 약속과 다르다"가 된다.</summary>
        private void SyncSpeechGate()
        {
            if (_speechGate == null) return;
            _speechGate.SetEnabled(AppSettingsModel.ResolveDialogueBubbleEnabled(_config));
        }

        // ★ 2026-09-05 — 여기 있던 SyncGearWarning()을 지웠다. 하는 일이 «톱니 아이콘 토글이 꺼져
        //   있으면 경고 캡션을 켠다» 하나뿐이었는데 그 토글과 캡션이 함께 사라졌다(사유는 BuildGeneralPage).
        //   빈 함수를 남기지 않는 이유는 위 SyncManualHide 삭제 때와 같다 — 다음 사람이 «무엇을
        //   동기화하던 자리인가»를 다시 조사하게 된다.

        /// <summary>슬라이더 드래그처럼 <b>연속으로 값이 바뀌는</b> 조작은 매 스텝 디스크를 두드리지 않는다
        /// (24시간 상주 앱). 토글/스와치는 즉시 저장하고, 슬라이더는 손을 뗄 때/창을 닫을 때 흘려보낸다.</summary>
        private void RequestSave() => _saveRequested = true;

        private void FlushPendingSave()
        {
            if (!_saveRequested) return;
            _saveRequested = false;
            CharacterSaveStore.Save();
        }

        // ==================== UI 구성 ====================

        private void BuildUi()
        {
            EnsureEventSystem();

            var canvasGo = new GameObject("SettingsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            // 씬 루트에 둔다 — 캐릭터 자손으로 두면 이 캔버스 안의 UI 이름이 "이름으로 캐릭터 파츠를
            // 찾는 코드"에 걸린다(2026-08-30에 부채꼴의 "Head"로 실제로 터진 사고).
            canvasGo.transform.SetParent(null, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrderTopMost;
            _scaler = canvasGo.GetComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            ApplyCanvasScaleFactor();

            // 그림 없는 컨테이너 + [본체(α1) → 보더] 형제 배치. 컨테이너에 Graphic을 붙이면 본체가
            // 그 위에 그려져 겹 순서가 뒤집힌다(InfoWindowPanelOpacityTests가 잠근 규칙).
            // ★ 2026-09-02 — 그림자 겹은 전부 삭제됐다(사용자 지시). 창 둘레는 보더 1px뿐이다.
            _panel = UiChrome.AddOpaquePanel(canvasGo.transform, "SettingsPanel", UiChrome.RadiusPanel,
                out Image panelImage);
            _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.anchoredPosition = Vector2.zero;
            _panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            panelImage.raycastTarget = true;   // 창 바탕을 눌러도 뒤(데스크톱)로 새지 않게.

            _host.Claim = TryClaimAction;
            _host.HitTest = ContainsScreenPoint;

            BuildHeader();
            BuildTabBar();
            BuildContent();
            BuildFooter();

            _masks = _panel.GetComponentsInChildren<RectMask2D>(true);

            // 클릭관통 차단막 — 씬 루트에 둔다(캐릭터 자식이면 캐릭터가 걷거나 구를 때 함께 돌아
            // 창의 화면 사각형과 어긋난다). isTrigger라 캐릭터 물리에는 관여하지 않는다.
            var blockerGo = new GameObject("SettingsClickBlocker");
            _clickBlocker = blockerGo.AddComponent<BoxCollider2D>();
            _clickBlocker.isTrigger = true;
            _clickBlocker.enabled = false;

            canvasGo.SetActive(false);
        }

        private void BuildHeader()
        {
            var barGo = new GameObject("Header", typeof(RectTransform));
            barGo.transform.SetParent(_panel, false);
            var bar = barGo.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(bar, 0f, 0f, PanelWidth, HeaderHeight);
            _headerRect = bar;   // 드래그 손잡이 — 여기를 잡은 동안만 창이 움직인다(2026-09-07).

            Text title = UiChrome.AddText(bar, "Title", UiChrome.FontTitle, TextAnchor.MiddleLeft,
                UiChrome.TextPrimary, bold: true);
            UiChrome.PlaceTopLeft(title.rectTransform, ContentPadX, -(HeaderHeight - 20f) * 0.5f, 320f, 20f);
            title.text = "StickMate 설정";

            // ★ 2026-09-02 — 세 표면(정보창/설정창/팝오버)이 <b>같은 세 줄</b>을 쓴다: 면은
            //   ChromeButtonSurface, 잉크는 InkOnSurface가 고르고, 테두리는 없다. 종전에는 세 표면이
            //   면 2종 · 잉크 2종 · 테두리 합성 여부 2종으로 갈려 있었고(3-way 분기, 아무도 의도한 적
            //   없다) 그래서 같은 결함이 세 번 따로 샜다. 근거는 UiChrome "창을 닫는 법" 절.
            Image close = UiChrome.AddSurface(bar, "Close", UiChrome.ChromeButtonSurface, UiChrome.RadiusChip);
            _closeRect = close.rectTransform;
            // 폭 인자만 24 → 44(WCAG 2.2 SC 2.5.8). 오른쪽 정렬이라 x/y 식은 한 글자도 안 바뀌고
            // 칩이 왼쪽으로 20pt 자란다. 제목 상자는 x20 w320(=20~340)이라 316pt 여유. 충돌 없음.
            SettingsControls.PlaceTopRight(_closeRect, ContentPadX, -(HeaderHeight - 24f) * 0.5f, 44f, 24f);
            Text closeLabel = UiChrome.AddText(_closeRect, "Label", UiChrome.FontBody, TextAnchor.MiddleCenter,
                UiChrome.InkOnSurface(UiChrome.ChromeButtonSurface, UiChrome.InkRole.Title, enabled: true));
            UiChrome.Stretch(closeLabel.rectTransform);
            closeLabel.text = "✕";
            var closeButton = close.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = close;
            closeButton.transition = Selectable.Transition.None;   // ColorTint pressed(×0.7843) 함정 — UiChrome 절 참고.
            closeButton.onClick.AddListener(() => { if (TryClaimAction("close")) Close("[✕] 클릭"); });

            // 손잡이에서 빼는 목록 — 여기 한 줄이 «[✕]를 누르면 창이 끌려간다»를 막는다.
            _headerNonDragRects = new[] { _closeRect };

            // ★ 2026-09-02 — 헤더의 닫기 힌트("창 밖을 클릭해도 닫혀요")는 <b>같은 날 걷어냈다</b>.
            //   바깥 클릭이 더 이상 닫지 않으므로 그 문장은 거짓이 됐다. 닫는 법은 푸터 아랫줄이
            //   계속 말한다(그 자리는 바로 윗줄의 "여는 방법"과 짝을 이루는 자리다).

            AddHorizontalDivider(_panel, -HeaderHeight);
        }

        private void BuildTabBar()
        {
            var barGo = new GameObject("TabBar", typeof(RectTransform));
            barGo.transform.SetParent(_panel, false);
            var bar = barGo.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(bar, 0f, -HeaderHeight, PanelWidth, TabBarHeight);

            float x = ContentPadX;
            for (int i = 0; i < TabCount; i++)
            {
                // ★ 배지 유무 · 탭 폭 · (아래 ApplyTabVisibility의) 밑줄색이 전부 IsTabReady <b>하나</b>에서
                //   나온다. 그래서 탭이 채워지는 날 <b>아무도 아무것도 지우지 않아도</b> 배지가 사라진다.
                //   판정을 두 벌로 두면 반드시 한쪽만 갱신된다 — M7이 정확히 그 사고였다.
                bool ready = IsTabReady((Tab)i);

                var tabGo = new GameObject("Tab" + i, typeof(RectTransform));
                tabGo.transform.SetParent(bar, false);
                var rt = tabGo.GetComponent<RectTransform>();
                _tabRects[i] = rt;

                // 탭 전체가 클릭 타깃이어야 한다(글자만 누르면 오조준이 잦다).
                // Stretch라 부모 폭이 <b>나중에</b> 정해져도 그대로 따라온다.
                Image hit = SettingsControls.AddHitArea(rt, "Hit");
                UiChrome.Stretch(hit.rectTransform);

                Text label = UiChrome.AddText(rt, "Label", UiChrome.FontBody, TextAnchor.MiddleCenter,
                    UiChrome.InkTab(selected: false));
                // ★ 2026-09-03 — 라벨 상자 폭을 <b>폰트에게 묻는다</b>. 상자를 먼저 놓고 글자를 넣던
                //   순서를 뒤집었다: 재고 나서 놓는다.
                float labelWidth = SettingsControls.MeasuredWidth(label, TabNames[i]);
                _tabLabels[i] = label;

                Text badge = null;
                float badgeWidth = 0f;
                if (!ready)
                {
                    // 잉크는 <b>전 상태 상수</b>(InkMeta)다 — ApplyTabVisibility에 코드를 더하지 않고,
                    //   생성 시 1회 도색으로 끝난다(하루 종일 켜져 있는 앱: 프레임 비용 0).
                    badge = UiChrome.AddText(rt, "Badge", UiChrome.FontCaption, TextAnchor.MiddleLeft,
                        UiChrome.InkMeta);
                    badgeWidth = SettingsControls.MeasuredWidth(badge, TabBadgeText);
                }

                float width = TabPadX * 2f + labelWidth + (ready ? 0f : TabBadgeGap + badgeWidth);
                UiChrome.PlaceTopLeft(rt, x, 0f, width, TabBarHeight);

                // ★ Stretch가 아니라 <b>라벨 폭만큼의 상자</b>다. 이 상자는 탭 상자의 <b>왼쪽 여백
                //   바로 다음</b>에 놓이고 폭이 곧 글리프 폭이라, MiddleCenter로 그려도 글자가
                //   상자를 정확히 채운다(배지가 붙어 부모가 넓어져도 마찬가지다 — 그래서 준비된 탭도
                //   같은 길로 보낸다).
                UiChrome.PlaceTopLeft(label.rectTransform, TabPadX,
                    -(TabBarHeight - TabLabelHeight) * 0.5f, labelWidth, TabLabelHeight);

                if (badge != null)
                {
                    UiChrome.PlaceTopLeft(badge.rectTransform, TabPadX + labelWidth + TabBadgeGap,
                        -(TabBarHeight - TabBadgeHeight) * 0.5f, badgeWidth, TabBadgeHeight);
                }

                // 활성 탭 밑줄 2pt(35-1-5). 비활성은 색만 투명하게 둔다 — 껐다 켜면 배치가 흔들린다.
                Image underline = UiChrome.AddSurface(rt, "Underline", UiChrome.Accent, 2);
                UiChrome.PlaceTopLeft(underline.rectTransform, 0f, -(TabBarHeight - 2f), width, 2f);
                underline.raycastTarget = false;
                _tabUnderlines[i] = underline;

                int captured = i;
                var button = hit.gameObject.AddComponent<Button>();
                button.targetGraphic = hit;
                button.onClick.AddListener(() =>
                {
                    if (TryClaimAction("tab" + captured)) SetTab((Tab)captured, "탭 클릭");
                });

                x += width + UiChrome.Space1;
            }

            // ★ 2026-09-03 신설 — 탭바에는 넘침 검사가 <b>없었다</b>(정보창 탭바에는 예전부터 있다).
            //   탭 줄은 마스크 밖이라 넘치면 잘리지도 않고 <b>창 밖에 글자가 떠 있는</b> 상태가 되고,
            //   그 증상은 "왜 저기 글씨가 있지"라 원인 추적이 비싸다. 문안이 길어지는 그 라운드에
            //   즉시 알려 준다.
            //
            //   ※ <b>정상값에서는 절대 찍히지 않는다</b>: 이 검사는 라벨을 실측으로 잰 뒤 남는 자리를
            //     보는 것이라, 지금 한국어 배치(끝 500pt 미만)에서 한 번도 참이 될 수 없다.
            //     정상 사용자에게 찍히는 경보는 경보가 아니라 소음이다(FX 0번 사고).
            float tabsEnd = x - UiChrome.Space1;
            if (tabsEnd > PanelWidth)
            {
                Debug.LogError($"[설정창] 탭 {TabCount}개가 {tabsEnd:F0}pt에서 끝나 창 폭({PanelWidth:F0}pt)을 " +
                               $"{tabsEnd - PanelWidth:F0}pt 넘겼습니다 — 마지막 탭이 창 밖으로 나갑니다. " +
                               "탭 이름이나 '준비 중' 배지 문안을 줄이십시오.");
            }

            AddHorizontalDivider(_panel, -(HeaderHeight + TabBarHeight));
        }

        private void BuildContent()
        {
            var viewGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewGo.transform.SetParent(_panel, false);
            _viewport = viewGo.GetComponent<RectTransform>();

            // ★★ 2026-09-02 — 자르는 선이 <b>글자 한가운데</b>를 지나고 있었다.
            //   [캐릭터] 탭 첫 화면 맨 아래에서 `대사 표시 시간`이 한글 높이의 약 60% 지점에서 잘려
            //   <b>"밑에 더 있다"가 아니라 "글꼴이 깨졌다"</b>로 읽혔다. 정보창 캐러셀이 이미 같은
            //   교훈을 가로 방향에서 배웠다(CharacterInfoWindow: "자르는 선의 위치가 틀렸다").
            //
            //   ★ 왜 "행 사이 빈 틈에 맞춘다"가 아니라 페이드인가: 이 창은 <b>페이지가 아니라 연속
            //     스크롤</b>이라 자르는 선이 어느 행에 걸릴지 미리 알 수 없다. 행 높이(44/60)와
            //     스크롤량(PageStep 382)이 서로 배수가 아니므로 "빈 틈에 맞추기"는 스크롤 위치마다
            //     다시 어긋난다. 반면 마지막 8pt 페이드는 <b>어디서 잘리든</b> 참이다.
            //   ★ RectMask2D.softness는 uGUI가 마스크 셰이더에서 직접 처리한다 — 새 그래픽도,
            //     매 프레임 비용도 없다(하루 종일 켜져 있는 앱).
            var mask = viewGo.GetComponent<RectMask2D>();
            mask.softness = new Vector2Int(0, ClipFadePoints);
            // ★ 2026-09-02 (41-2 결정 3) — 뷰포트 폭이 <b>카드 폭과 같다</b>(680 → 656).
            //   예전에는 뷰포트(x 20~700)를 카드가 꽉 채워 스크롤 레일이 앉을 자리가 0pt였다.
            //   656으로 줄이면 좌 20 / 우 20 대칭이 되고 그 사이 x 680~700이 레일 자리가 된다.
            //   카드 안쪽 폭 656−28 = 628 ≥ 캡션 상자 480 / 라벨 상자 420 → 잘리는 글자 없음.
            UiChrome.PlaceTopLeft(_viewport, ContentPadX, -(HeaderHeight + TabBarHeight),
                SettingsControls.CardWidth, ContentHeight);

            for (int i = 0; i < TabCount; i++)
            {
                var pageGo = new GameObject("Page_" + TabNames[i], typeof(RectTransform));
                pageGo.transform.SetParent(_viewport, false);
                var page = pageGo.GetComponent<RectTransform>();
                UiChrome.PlaceTopLeft(page, 0f, 0f, SettingsControls.CardWidth, ContentHeight);
                _pages[i] = page;

                float y = -ContentPadTop;
                y = AddEyebrow(page, TabEyebrows[i], y);

                if (!IsTabReady((Tab)i)) y = BuildPlaceholderTab(page, y, (Tab)i);
                else if ((Tab)i == Tab.General) y = BuildGeneralTab(page, y);
                else y = BuildCharacterTab(page, y);

                _pageHeights[i] = -y + ContentPadTop;
            }

            BuildPageButtons();
            ApplyTabVisibility();
        }

        private static float AddEyebrow(RectTransform page, string text, float y)
        {
            Text eyebrow = UiChrome.AddText(page, "Eyebrow", UiChrome.FontLabel, TextAnchor.MiddleLeft,
                UiChrome.InkMeta);
            UiChrome.PlaceTopLeft(eyebrow.rectTransform, 2f, y, SettingsControls.CardWidth, 14f);
            eyebrow.text = text;
            return y - 20f;
        }

        // -------------------- [일반] --------------------

        private float BuildGeneralTab(RectTransform page, float y)
        {
            var display = new SettingsCardBuilder(page, "표시", y, _host);
            _autoHideToggle = display.AddToggle("general.autoHide", "전체화면 게임 감지 시 자동 숨김",
                AppSettingsModel.AutoHideOnFullscreen,
                on =>
                {
                    AppSettingsModel.SetAutoHideOnFullscreen(on);
                    CharacterSaveStore.Save();
                    Debug.Log($"[설정창] 전체화면 자동 숨김 {(on ? "켬" : "끔")} — 이 스위치는 캐릭터만이 " +
                        "아니라 StickmanAgent의 <b>두 창구를 함께</b> 좌우합니다: IsSuspended(등급 2, " +
                        "전체화면 게임 → 캐릭터까지)와 ArePanelsSuppressed(등급 1, 게임이 아닌 전체화면 앱 → " +
                        "창·팝오버·부채꼴과 그 차단막만). 끄면 전체화면 앱 위에 이 창들과 그 클릭관통 " +
                        "차단막(BoxCollider2D)까지 남습니다 — 절대 불변 원칙 2의 사용자 예외이므로 " +
                        "그 대가를 캡션에 적어 두었습니다.");
                },
                // ★ 2026-09-01(페르소나 J3) — 예전 캡션은 "게임 · 영상이 전체화면이 되면 즉시
                //   사라집니다."로 <b>캐릭터 얘기만</b> 했다. 그런데 이 스위치를 끄면 IsSuspended가
                //   영원히 false가 되어 창과 <b>720×560 클릭 차단막</b>도 게임 위에 남는다. 사용자는
                //   캐릭터가 남는 데 동의한 것이지 "클릭이 안 먹는 구멍"에 동의한 적이 없다.
                caption: "켜면 캐릭터도 열린 창도 함께 사라집니다. 끄면 창이 막는 클릭까지 그대로 남아요.");

            // ★★★ 2026-09-06 리더 판정(L-5 확정) — <b>「이번 세션만 끄기」</b>.
            //
            //   왜 여기인가: 이 카드(「표시」)는 이미 <b>«지금 이 순간 화면에서 무엇을 물릴 것인가»</b>를
            //   모아 둔 자리다(전체화면 자동 숨김 / 지금 즉시 숨기기·보이기). 「회의 중이라 자동 연출을
            //   끈다」는 정확히 같은 종류의 요구이고, 실제로 그 상황의 사용자는 <b>바로 위 두 행을
            //   찾으러</b> 이 카드를 연다. 새 카드도, 새 탭도, 새 창도 만들지 않는다.
            //
            //   ★ <b>저장하지 않는다.</b> 그래서 <c>CharacterSaveStore.Save()</c>를 부르지 않는다 —
            //     바로 위 자동 숨김 토글과 다른 유일한 점이고, 그 사실을 캡션이 사용자에게 말한다.
            //     영속시키면 «반년 전에 끈 것을 잊고 고장났다고 신고하는» 경로가 생긴다(41-8과 같은 형태).
            //
            //   ★ 값의 방향에 주의: 이 토글은 <b>«켜기»</b>이고 게이트는 <b>«끄기»</b>다. 그래서 양쪽에
            //     <c>!</c>가 붙는다. 표시 이름을 부정형("춤 끄기")으로 두면 «켜면 꺼진다»가 되어
            //     사용자가 반드시 한 번 헷갈린다.
            _danceToggle = display.AddToggle("general.musicDance", "음악이 나오면 춤추기",
                !AudioReactiveDanceGate.MutedForThisSession,
                on => AudioReactiveDanceGate.SetMutedForThisSession(!on, "설정창 [일반] 표시"),
                caption: "회의나 발표 중에는 꺼 두세요. 저장하지 않으니 앱을 다시 켜면 저절로 켜집니다.");

            // ★★ 2026-09-02 — 이 행은 <b>하나의 상태</b>(StickmanAgent.IsUserHidden)를 본다.
            //   예전에는 [숨기기]가 렌더러만 끄는 1회성이라 캡션이 "전체화면 앱을 오갔다 오면 다시
            //   나타나요"라고 <b>자기 한계를 자백</b>하고 있었다. 화면공유 중에 되살아나는 숨김은
            //   숨김이 아니고, 무엇보다 그때 <b>열린 창과 클릭 차단막은 애초에 걷히지도 않았다</b> —
            //   캐릭터만 사라지고 설정창이 발표 화면에 남는 쪽이 더 이상하다.
            //   이제 이 행은 전체화면 감지와 같은 Suspend 경로를 탄다.
            //
            // ★★★ 2026-09-03 사용자 확정 — <b>범위가 「캐릭터만」으로 좁아졌다</b>(<i>"캐릭만 가리고"</i>).
            //   위 문단의 «설정창이 발표 화면에 남는 쪽이 더 이상하다»는 <b>화면공유</b>를 전제한
            //   판단이었고, 사용자가 실제로 겪은 것은 <b>갇힘</b>이었다 —
            //   <i>"전부 다 없어져버려서 다시 나오게 할 방법이 없어"</i>.
            //   이제 이 버튼은 캐릭터(와 말풍선·이펙트·장비·펫)만 가리고 <b>이 창은 열린 채로 남는다</b>.
            //   그래서 바로 옆 [보이기]가 계속 손에 닿는다 — 탈출구가 안내문이 아니라 <b>구조</b>가 된다.
            //   전체화면 게임 감지(축 1)는 <b>한 비트도 바뀌지 않았다</b>(원칙 2).
            // ★★★ 2026-09-03 — <b>두 행을 하나로 합쳤다</b>(리더 확정, 페르소나 실측 후속).
            //
            //   무엇이 있었나: 바로 아래에 <c>"숨기기 / 보이기 단축키"</c>라는 <b>토글</b> 행이 하나 더
            //   있었고, 그 행의 값은 이 버튼 행과 <b>같은 상태</b>(IsUserHidden)였다. 코드는 이 결함을
            //   <b>미리 예측했고</b>(옛 주석: <i>"「단축키」라는 라벨 때문에 「이 단축키를 켜고 끄는
            //   스위치」로 읽히고, 그렇게 읽은 사용자가 켜는 순간 캐릭터가 사라져 놀란다"</i>) 캡션
            //   한 줄로 막으려 했다. <b>페르소나가 정확히 예측된 그대로 읽었다</b> — 완화가 현실에서
            //   반증됐다. ⇒ 고칠 것은 컨트롤 종류가 아니라 <b>라벨의 낱말 하나(「단축키」)</b>였다.
            //
            //   처방 세 가지가 전부 여기 한 줄에 들어 있다:
            //     (1) 토글 삭제 — 같은 상태를 조작하는 컨트롤이 둘이면 사용자는 <b>둘의 관계</b>를
            //         먼저 풀어야 한다. 카드가 60pt(RowHeightWithCaption) 줄어드는 것은 덤이다.
            //     (2) 칩 이동 — <c>hotkey:</c>는 "이 행의 동작에 전역 단축키가 있다"는 <b>표시</b>이지
            //         스위치가 아니다. 그것이 원래 이 정보가 있어야 할 자리다.
            //     (3) 라벨에서 「단축키」 제거 — 남은 라벨은 <c>지금 즉시</c>다.
            //
            //   ★★ <b>버튼 라벨(["숨기기","보이기"])은 한 글자도 바꾸지 마라.</b> 폭 식이
            //      <c>26 + 글자수 × 9</c>라(SettingsControls.AddButtons) 글자 수가 바뀌면 <b>옆 버튼이
            //      통째로 미끄러진다</b>. 지금 두 버튼은 각각 53pt이고 합계 114pt다.
            display.AddButtons("general.hideNow", "지금 즉시", new[] { "숨기기", "보이기" },
                index => SetUserHiddenFromSettings(index == 0),
                caption: HideEscapeCaption,
                // ★ 리더 판정 2026-09-02 — V가 아니라 <b>K</b>. V는 GlobalKey에 없어 양 플랫폼 파일을
                //   고쳐야 했고, K는 두 플랫폼 모두 이미 키코드가 매핑돼 있으면서 바인딩만 비어 있었다
                //   (Platform/IGlobalKeyStateService.cs의 GlobalKey.K 문서가 이 자리를 예약해 뒀다).
                //   그래서 플랫폼 파일은 이 기능 때문에 <b>한 줄도</b> 바뀌지 않았다.
                hotkey: ShortcutLabel.Chord(StickmanAgent.UserHideHotkeyLetter));
            y = display.Finish(y);

            var screenUi = new SettingsCardBuilder(page, "화면 위 UI", y, _host);
            // ★ 2026-09-01 — 여기 있던 "구석 크기 패널 (왼쪽 아래 모서리)" 토글을 <b>지웠다</b>.
            //   사용자가 세 번 요청한 것은 "끌 수 있게"가 아니라 <b>삭제</b>였고, 그 패널
            //   (CornerHoverPanel/SizeDialWidget)은 이 라운드에 통째로 제거됐다. 크기 조정은
            //   아래 [캐릭터] 탭의 "캐릭터 크기" 슬라이더 하나로 일원화된다.

            // ★★★ 2026-09-05 — 여기 있던 <b>「톱니 아이콘」 토글과 그 경고 캡션을 지웠다</b>
            //   (ux-designer W-4 재발행 / 리더 판정 L-10). 지운 이유는 «정리»가 아니라 <b>위험</b>이다:
            //
            //   같은 날 톱니가 「평상시 숨김 · 캐릭터가 화면에서 사라진 동안에만 표시」로 바뀌면서
            //   (Interaction/InfoGearIconWidget.StandbyGearPolicy) 이 토글은 <b>끌 대상을 잃었다</b>.
            //   그런데 남겨 두면 더 나빠진다 — 그 값은 세이브에 내려가 재시작해도 유지되고, 다시
            //   켜는 문(이 설정창)에 닿는 마우스 경로가 바로 그 톱니이기 때문이다.
            //   ⇒ <b>사용자가 스스로를 영구히 잠글 수 있는 스위치</b>가 된다.
            //   2026-09-03 사용자 신고 <i>"설정에서 숨기기버튼 누르니까 전부 다 없어져버려서 다시
            //   나오게 할 방법이 없어"</i>의 정확한 재발이라, 되돌리는 문이 없는 저장 항목을 금지한
            //   41-8과 정면으로 충돌한다.
            //
            //   ★ 그리고 지우기 전 이 자리는 <b>거짓말을 하고 있었다</b>: 토글을 꺼도 아무 일도
            //     일어나지 않는데(게이트가 이 값을 더 이상 읽지 않는다) 로그와 캡션은
            //     "끄면 마우스 진입점이 사라진다"고 말했다.
            //
            //   ★ <b>설정 값과 세이브 필드(<c>gearIconVisible</c>)는 지우지 않았다</b> — 소비만 끊었다.
            //     스키마를 한 비트도 건드리지 않으므로 기존 세이브가 그대로 읽히고
            //     <c>AppSettingsModelContractTests</c>/<c>EquipmentMigrationTests</c>도 그대로 초록이다.
            //     (필드를 지우려면 v10 → v11 + 마이그레이션 + 하위 호환 테스트가 필요하고, 그건
            //      「톱니 완전 제거」 라운드의 몫이다.)
            //
            //   ★ 아래 [톱니 위치] 행은 <b>남긴다</b>(ux-designer W-5 철회). 톱니는 여전히 드래그로
            //     옮길 수 있고 그 자리는 세이브에 영구히 앉으므로, 되돌리는 문도 남아야 한다(41-8).

            // ★★ 2026-09-02 P0 (docs/UX_FLOW.md 41-8 2겹) — <b>영구히 저장되는 것에는 되돌리는 문이 있다.</b>
            //   톱니를 끌어다 놓은 자리는 세이브에 앉아 재시작해도 유지되는데, 그것을 되돌리는 UI가
            //   이 앱에 <b>하나도 없었다</b>(Core/UiLayoutModel은 세우는 문만 있고 내리는 문이
            //   ResetForTesting뿐이었다). 실수로 화면 구석에 놓으면 영구히 구석이었다.
            //
            //   ★ 왜 [데이터] 탭이 아니라 여기인가: 이 카드는 이미 <b>같은 톱니</b>의 on/off를 들고 있고,
            //     "톱니가 이상해졌다"고 느낀 사람이 여는 곳이 정확히 여기다. [데이터] 탭은 지금 0%(죽은 탭)
            //     이라 살아 있는 행 하나를 그 안에 넣으면 "안 켜지는 탭"이라는 그 탭의 유일한 약속이 깨진다.
            //   ★ 새 컨트롤은 0개다 — 위 [숨기기][보이기] 행과 <b>완전히 같은 부품</b>(AddButtons)이다.
            _gearHomeGate = new SettingsRowGate("아직 옮긴 적이 없어요.");
            _gearHomeButton = screenUi.AddButtons("general.gearHome", "톱니 위치", new[] { "처음 자리로" },
                _ => OnGearHomeClicked(),
                caption: "드래그해서 옮긴 자리를 화면 오른쪽 위로 되돌립니다.", gate: _gearHomeGate)[0];

            y = screenUi.Finish(y);

            // ★ 2026-09-02 (41-2 / C9) — 카드 이름이 <c>시작 / 종료</c>에서 <b><c>시작할 때</c></b>로
            //   바뀌었다. 종료 버튼이 푸터로 올라갔으므로 이름이 절반만 참이 됐기 때문이다.
            //   같은 창에 종료 버튼이 두 개면 그게 더 나쁘다 — 여기서는 <b>뺀다</b>.
            var startStop = new SettingsCardBuilder(page, "시작할 때", y, _host);
            startStop.AddToggle("general.autoLaunch", "로그인할 때 자동 실행", false, null,
                enabled: false,
                disabledNote: DisabledReason.NotBuilt("이 기능은 다음 업데이트에 들어옵니다."));

            // C8. 버튼을 옮겼으면 <b>옮겼다고 말한다</b>. 안 그러면 예전 자리를 아는 사용자가
            //     "없어졌다"로 읽는다(이 라운드가 고치려는 바로 그 증상의 거울상이다).
            startStop.AddCaptionLine("QuitMoved", "앱을 끄는 버튼은 이 창 맨 아래에 있어요.", UiChrome.InkMeta);
            y = startStop.Finish(y);

            return y;
        }

        /// <summary>
        /// ★★ 2026-09-02 — "지금 즉시" 숨기기/보이기의 <b>실제 배선</b>.
        ///
        /// <para><b>무엇이 바뀌었나</b>: 예전 구현(<c>SetCharacterVisibleNow</c>)은
        /// <c>StickmanBlackboard.SetCharacterVisible</c>로 <b>렌더러만</b> 껐다. 그래서
        /// (1) 전체화면 감지가 한 번만 왕복하면 <c>StickmanAgent.Resume()</c>이 렌더러를 되살렸고,
        /// (2) <b>열린 창과 그 클릭 차단막은 애초에 걷히지도 않았다</b> — 발표 화면에 캐릭터 대신
        /// 이 설정창이 그대로 찍혔다. 이제는 <see cref="StickmanAgent.SetUserHidden"/>을 통해
        /// 전체화면 감지와 <b>같은 Suspend 경로</b>를 탄다.</para>
        ///
        /// <para>★★★ <b>2026-09-03 — 이 창은 이제 닫히지 않는다</b>(사용자 확정 <i>"캐릭만 가리고"</i>).
        /// 여기 있던 옛 문장은 <i>"이 창이 스스로 닫히는 것은 결함이 아니라 요구사항이다"</i>였다.
        /// 그 요구사항이 뒤집혔다: 사용자 명시 숨김 단독에서는
        /// <see cref="StickmanAgent.HidesScreenSurfaces"/>가 false라 <see cref="Update"/>의
        /// <c>ArePanelsSuppressed</c> 가드가 걸리지 않고, 창도 720×560 차단막도 그대로 남는다.
        /// <b>그래서 바로 옆 [보이기]가 손에 닿는다</b> — 이 라운드가 고친 것이 정확히 이 한 가지다.</para>
        /// </summary>
        private void SetUserHiddenFromSettings(bool hidden)
        {
            if (_agent == null)
            {
                Debug.LogWarning("[설정창] 지금 즉시 숨기기/보이기 실패 — 씬에 StickmanAgent가 없습니다.");
                return;
            }

            bool applied = _agent.SetUserHidden(hidden, "설정창 [일반] 지금 즉시");
            Debug.Log($"[설정창] 캐릭터 {(applied ? "숨김" : "다시 보이기")} — " +
                (applied
                    ? "캐릭터와 거기 붙은 것(말풍선·이펙트·장비·펫)만 가렸습니다. <b>이 창은 열린 채로 " +
                      "남습니다</b> — 바로 옆 [보이기]로 되돌리세요(" +
                      ShortcutLabel.Chord(StickmanAgent.UserHideHotkeyLetter) + " 도 같은 동작입니다). " +
                      "톱니와 부채꼴도 그대로입니다."
                    : "다시 보이게 했습니다."));
        }

        /// <summary>
        /// ★ [톱니 위치] &gt; [처음 자리로]. <b>확인 대화상자를 띄우지 않는다</b> — 대신 결과가
        /// 화면에 바로 보이고(톱니가 우상단으로 돌아간다) 이 행이 <b>그 자리에서 회색이 되며</b>
        /// 캡션이 <c>아직 옮긴 적이 없어요.</c>로 바뀐다. 그것이 이 조작의 확인이다.
        ///
        /// <para><b>왜 2단 확인(=[지금 종료] 방식)을 붙이지 않았나</b>: 그 장치는 <b>되돌릴 수 없는</b>
        /// 조작을 위한 것이다. 이건 잘못 눌러도 다시 끌어다 놓으면 끝이고, 되돌리는 비용이 이 버튼을
        /// 한 번 더 누르는 비용과 같은 수준이다. 확인을 붙이면 <b>고치러 온 사람에게 관문을 하나 더</b>
        /// 세우는 셈이라 이 행의 목적과 반대로 간다(docs/UX_FLOW.md 41-8 ②는 확인 단계를 두지 않는다).</para>
        ///
        /// <para>저장은 <see cref="InfoGearIconWidget.ReturnToDefaultPosition"/>이 <b>즉시</b> 한다 —
        /// 드래그 확정과 같은 이유다(되돌린 직후 앱을 끄면 옛 자리가 되살아난다). 그래서 여기서는
        /// <see cref="RequestSave"/>를 부르지 않는다(같은 값을 두 번 쓰지 않는다).</para>
        /// </summary>
        private void OnGearHomeClicked()
        {
            InfoGearIconWidget gear = ResolveGearWidget();
            if (gear != null)
            {
                gear.ReturnToDefaultPosition("설정창 [일반] 톱니 위치 [처음 자리로]");
            }
            else
            {
                // 씬에 톱니 위젯이 없어도(비정상 씬/테스트) 저장된 값은 지울 수 있어야 한다 —
                // 이 행이 고치려는 대상은 화면의 아이콘이 아니라 <b>세이브에 앉은 좌표</b>다.
                if (UiLayoutModel.ClearGearCenter()) CharacterSaveStore.Save();
                Debug.LogWarning("[설정창] 톱니 위젯을 찾지 못해 저장값만 되돌렸습니다 — " +
                    "화면의 아이콘은 다음 실행에 기본 위치로 뜹니다.");
            }

            SyncGearHomeGate();
        }

        /// <summary>톱니 위젯은 이 컴포넌트와 <b>같은 GameObject</b>에 있다(씬 조립 관례).
        /// 찾은 결과를 캐시하지 않는 이유: 이 행은 사용자가 누를 때만 도는 <b>1회성</b>이라
        /// 캐시가 벌어 주는 것이 없고, 캐시가 죽은 참조를 들고 있는 위험만 남는다.</summary>
        private InfoGearIconWidget ResolveGearWidget() => GetComponent<InfoGearIconWidget>();

        /// <summary>되돌릴 것이 있는가에 맞춰 [톱니 위치] 행을 켜고 끈다.
        /// <para><see cref="SettingsRowGate.SetEnabled"/>는 값이 <b>바뀐 때만</b> 다시 칠하므로
        /// (bool 비교 한 번, 할당 0) 이 창이 열려 있는 동안 매 프레임 불러도 된다. 매 프레임 부르는
        /// 이유는 실제로 그 사이에 바뀔 수 있기 때문이다 — 이 창은 <b>창 밖 클릭으로 닫히지 않으므로</b>
        /// 설정창을 열어 둔 채 톱니를 끌어 옮기는 것이 가능하다.</para></summary>
        private void SyncGearHomeGate()
        {
            _gearHomeGate?.SetEnabled(UiLayoutModel.HasGearCenter);
        }

        private void OnQuitClicked()
        {
            if (!_quitArmed)
            {
                _quitArmed = true;
                _quitArmedAt = Time.unscaledTime;
                ApplyQuitStyle();
                Debug.Log($"[설정창] [지금 종료] 1차 클릭 — {QuitConfirmSeconds:F0}초 안에 다시 누르면 종료합니다.");
                return;
            }

            Debug.Log("[설정창] [지금 종료] 확정 — Application.Quit()을 호출합니다. 저장은 " +
                "CharacterProgressionDirector.OnApplicationQuit()이 담당하므로 데이터 손실이 없습니다.");
            FlushPendingSave();
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void DisarmQuit()
        {
            if (!_quitArmed) return;
            _quitArmed = false;
            ApplyQuitStyle();
        }

        private void ApplyQuitStyle()
        {
            if (_quitLabel == null || _quitSurface == null) return;
            _quitLabel.text = _quitArmed ? QuitConfirmText : QuitLabelText;
            // 푸터는 창 바탕(PanelSurface) 위다 — 카드용 합성값을 쓰면 한 단 어둡게 앉는다.
            // ★ F1(2026-09-06) — 평상시 면이 ControlFaceOnPanel(4.88:1)로 올라갔다. <b>이 갱신 경로를
            //   같이 안 고치면 첫 DisarmQuit()에서 생성값이 도로 어두워진다</b>(정보창 카드가 이미
            //   당한 형태: 옳게 만든 색을 매 갱신이 덮어썼다).
            Color face = _quitArmed
                ? UiChrome.Flatten(UiChrome.AccentSurface, UiChrome.PanelSurface)
                : SettingsControls.ControlFaceOnPanel;
            _quitSurface.color = face;
            // 무장 라벨은 <b>경고색</b>이 지고(어두운 면 위), 평상시 라벨은 그 밝은 면에서 파생된다.
            _quitLabel.color = _quitArmed
                ? UiChrome.WarmAccent
                : UiChrome.InkOnSurface(face, UiChrome.InkRole.Title, true);
        }

        // -------------------- [캐릭터] --------------------

        private float BuildCharacterTab(RectTransform page, float y)
        {
            var look = new SettingsCardBuilder(page, "모양", y, _host);

            // ★ 2026-09-06 사용자 신고 — <i>"캐릭터설정창에서 캐릭터 이름도 설정할수 있어야 하는데 안됨"</i>.
            //   이름은 이 카드의 <b>첫 줄</b>이다: 이 캐릭터가 무엇인지가 크기·색보다 먼저다.
            //
            //   ★ 로직을 복제하지 않는다. 정규화(빈 값·줄바꿈·길이·이모지 반쪽)는 전부
            //     CharacterProgressionModel.SetCharacterName 하나가 하고, 여기서는 <b>친 글자를 넘기고</b>
            //     <b>모델이 정한 결과를 되돌려 적을</b> 뿐이다. 정보창의 인라인 편집도 같은 함수를 부른다 —
            //     두 창이 같은 사실을 각자 계산하면 언젠가 갈라진다.
            _nameField = look.AddTextField("character.name", "이름",
                CharacterProgressionModel.CharacterName, CharacterProgressionModel.DefaultCharacterName,
                CharacterProgressionModel.MaxNameLength,
                typed =>
                {
                    CharacterProgressionModel.SetCharacterName(typed);
                    CharacterSaveStore.Save();
                    // 친 것과 저장된 것이 다를 수 있다(공백만 → 기본값, 상한 초과 → 잘림).
                    // 그 차이는 <b>지금</b> 보여야 한다 — 다음에 창을 열었을 때 알게 되면
                    // "내가 쓴 이름이 아닌데?"가 된다.
                    SyncNameField();
                    Debug.Log($"[설정창] 이름 변경 -> \"{CharacterProgressionModel.CharacterName}\" (즉시 저장). " +
                        "정보창 헤더/「표시」 블록도 같은 값을 따라옵니다(단일 소스: CharacterProgressionModel).");
                });

            // ★ 크기는 반드시 단일 소스를 지난다(35-1-3 ①). 여기서 UiLayoutModel/Agent를 직접 부르면
            //   적용 게이트(랙돌 중 유예)가 이 경로에만 없는 상태가 되어 규칙이 두 벌이 된다.
            _scaleSlider = look.AddSlider("character.scale", "캐릭터 크기",
                StickConfig.MinCharacterScale, StickConfig.MaxCharacterScale, CharacterScaleController.ValueStep,
                CharacterScaleController.Value, v => v.ToString("0.00") + "×",
                v =>
                {
                    CharacterScaleController.Request(v, "설정창 슬라이더");
                    RequestSave();
                });

            var inkColors = new Color[2];
            inkColors[0] = _config != null ? Opaque(_config.primaryOutlineColor) : Color.black;
            inkColors[1] = _config != null ? Opaque(_config.whiteInkColor) : Color.white;
            _inkSwatches = look.AddSwatches("character.ink", "잉크색", inkColors,
                _config != null && _config.IsWhiteInk() ? 1 : 0, OnInkSwatchClicked);

            // 포인트 컬러는 회의록 6 소관 — 자리만 잡고 비활성(35-1-7 "없는 척하지 않는다").
            //
            // ★ 2026-09-01(페르소나 소은 #7-d) — 견본이 셋이었는데 <b>둘이 같은 색</b>으로 보였다:
            //   UiChrome.WarmAccent는 Accent와 <b>값이 완전히 같다</b>("두 번째 강조색을 발명하지
            //   않는다"는 팔레트 문서의 의도된 결정). 의도가 옳아도, 고를 수 있는 색을 나란히 늘어놓는
            //   자리에서 같은 색 두 개는 사용자에게 <b>표시 버그</b>로 읽힌다. 그래서 새 색을 만들지도
            //   말고 같은 색을 두 번 그리지도 말고, <b>지금 이 앱에 실제로 있는 두 색</b>만 놓는다.
            look.AddSwatches("character.point", "포인트 컬러 (눈 · 윤곽선)",
                new[] { UiChrome.Accent, UiChrome.TextPrimary }, -1, null,
                enabled: false,
                disabledNote: DisabledReason.NotBuilt("포인트 컬러 팔레트는 다음 업데이트에 들어옵니다."));
            y = look.Finish(y);

            var speech = new SettingsCardBuilder(page, "말과 행동", y, _host);

            // ★ 42-11 판정 G — <b>원인이 결과보다 먼저</b>. 이 토글이 카드 맨 아래에 있던 동안, 아래
            //   세 행이 왜 비활성인지 알려면 그 이유를 찾아 <b>아래로 내려가</b> 읽어야 했다.
            _bubbleToggle = speech.AddToggle("character.bubble", "말풍선 표시",
                AppSettingsModel.ResolveDialogueBubbleEnabled(_config),
                on =>
                {
                    AppSettingsModel.SetDialogueBubbleEnabled(on);
                    SyncSpeechGate();
                    CharacterSaveStore.Save();
                    Debug.Log($"[설정창] 말풍선 표시 {(on ? "켬" : "끔")} — 대사 생성 파이프라인은 그대로 돕니다" +
                        "(원칙 1의 행동-텍스트 싱크는 설정으로 끌 수 있는 물건이 아닙니다). 그리지 않을 뿐입니다." +
                        $" 아래 세 행은 {(on ? "다시 조절할 수 있습니다" : "지금 만져도 화면이 바뀌지 않으므로 함께 비활성이 됩니다")}.");
                });

            speech.AddSegment("character.tone", "말투", new[] { "반말", "존댓말" }, 0, null,
                enabled: false,
                disabledNote: DisabledReason.NotBuilt("말투 고르기는 다음 업데이트에 들어옵니다."));

            // 아래 세 행은 말풍선을 그리지 않으면 전부 무효다 — 한 손잡이로 함께 내린다(42-11 G).
            _speechGate = new SettingsRowGate("말풍선 표시를 켜면 조절할 수 있어요.");

            _fontSizeSlider = speech.AddSlider("character.fontSize", "말풍선 글자 크기",
                AppSettingsModel.MinDialogueFontSize, AppSettingsModel.MaxDialogueFontSize, 1f,
                AppSettingsModel.ResolveDialogueFontSize(_config), v => Mathf.RoundToInt(v) + "pt",
                v => { AppSettingsModel.SetDialogueFontSize(Mathf.RoundToInt(v)); RequestSave(); },
                gate: _speechGate);

            // ★ 2026-09-02(42-4) — 초 슬라이더 폐기. 값 라벨이 없는 것은 빠뜨린 게 아니라 판정이다:
            //   보여줄 정직한 숫자가 없다(효과가 대사마다 다르고 눈으로 0.3초를 잴 수 없다).
            _visibleLengthSegment = speech.AddSegment("character.visibleLength", "대사 표시 시간",
                DialogueVisibleLengthOptions, (int)AppSettingsModel.DialogueVisibleLength,
                i =>
                {
                    AppSettingsModel.SetDialogueVisibleLength((DialogueVisibleLength)i);
                    RequestSave();
                },
                caption: "대사가 떠 있는 시간은 글자 수에 맞춰 정해집니다. 천천히 읽는 편이면 늘려 두세요.",
                gate: _speechGate);

            _chatterSlider = speech.AddSlider("character.chatter", "잡담 빈도",
                0f, AppSettingsModel.MaxChatterPercent, 10f,
                AppSettingsModel.ChatterPercent, v => Mathf.RoundToInt(v) + "%",
                v => { AppSettingsModel.SetChatterPercent(Mathf.RoundToInt(v)); RequestSave(); },
                caption: "100%가 기본값입니다. 0%로 두면 혼잣말을 하지 않아요.",
                gate: _speechGate);

            SyncSpeechGate();
            y = speech.Finish(y);

            return y;
        }

        /// <summary>
        /// `대사 표시 시간` 세그먼트 문구(docs/UX_FLOW.md 42-7 확정형). <b>순서가 곧
        /// <see cref="DialogueVisibleLength"/>의 순서</b>다 — 둘이 어긋나면 사용자가 고른 칸과 저장되는
        /// 값이 갈린다.
        ///
        /// <para>`기본`이라는 낱말이 "배포 기본값이 어느 것인지"를 캡션 한 문장 없이 알려 준다.
        /// 폭 검산(42-7): 42 + 42 + 69 + 간격 8 = <b>161pt</b>, 라벨 상자(420pt)와 여유 71pt.</para>
        /// </summary>
        private static readonly string[] DialogueVisibleLengthOptions = { "기본", "길게", "아주 길게" };

        /// <summary>스와치 색은 <b>반드시 불투명</b>이어야 한다 — 반투명 판이 하나라도 얹히면 그 자리의
        /// 창 알파가 내려간다(SettingsControls 클래스 문서의 알파 규칙).</summary>
        private static Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);

        /// <summary>
        /// 잉크색 전환 — 정보창 스와치/단축키 ⌃⌥⌘C와 <b>완전히 같은 경로</b>다.
        /// ★ <c>_config.inkColor</c>(직렬화 필드)에 절대 쓰지 않는다: 그 에셋은 프리팹 16개 컴포넌트에
        /// 배선된 배포 기본값이고, 에디터는 플레이 모드 중의 변경을 되돌리지 않는다(2026-08-31 R5).
        /// </summary>
        private void OnInkSwatchClicked(int index)
        {
            if (_config == null) return;
            StickmanInkColor next = index == 1 ? StickmanInkColor.White : StickmanInkColor.Black;
            if (_config.ResolveInkPreset() == next) return;

            _config.SetRuntimeInkColor(next);
            CharacterAppearanceModel.SetInkColor(next);
            if (_agent != null) _agent.ApplyInkColorFromConfig();
            CharacterSaveStore.Save();
            Debug.Log($"[설정창] 잉크색 전환 -> {next} (캐릭터/액세서리에 즉시 반영, 즉시 저장). " +
                "배포 에셋의 직렬화 필드는 건드리지 않습니다.");
        }

        // -------------------- 아직 비어 있는 탭 --------------------

        private float BuildPlaceholderTab(RectTransform page, float y, Tab tab)
        {
            var card = new SettingsCardBuilder(page, TabNames[(int)tab], y, _host);
            // ★ 문구는 <b>세 탭 공통</b>이라 세 탭 전부에서 참이어야 한다(43-1 ③). "이 스위치들"은
            //   [데이터] 탭의 `저장 파일 위치`가 스위치가 아니라서 거짓이 된다 — "여기 적힌 항목들"은
            //   바로 윗줄 라벨을 가리키고 컨트롤 종류를 약속하지 않는다.
            card.AddToggle("placeholder." + (int)tab, PlaceholderLabel(tab), false, null,
                enabled: false,
                disabledNote: DisabledReason.NotBuilt("여기 적힌 항목들은 다음 업데이트에 들어옵니다."));
            return card.Finish(y);
        }

        private static string PlaceholderLabel(Tab tab) => tab switch
        {
            Tab.Event => "방해 강도 프리셋 + 자동 발동 개별 토글",
            Tab.Accessibility => "윤곽선 강조 · 애니메이션 줄이기 · 저전력 렌더링",
            _ => "저장 파일 위치 · 초기화 · 네트워크(전부 기본 꺼짐)",
        };

        // -------------------- 푸터 / 페이지 --------------------

        /// <summary>
        /// ★ 2026-09-02 (41-2) — <b>2단 푸터</b>. 윗줄은 예전 안내 두 개 그대로, 아랫줄에
        /// "닫는 법"(C4)과 <c>[지금 종료]</c>가 온다.
        ///
        /// <para><b>왜 종료가 여기로 왔나</b>: 예전에는 [일반] 탭 <c>시작 / 종료</c> 카드 안에 있었고,
        /// 그 카드는 넘친 92pt 구간(뷰포트 하단보다 24~48pt 아래)에 앉아 <b>첫 화면에서 안 보였다</b>.
        /// 반면 잘 보이는 종료 버튼은 [행동 명령] 창 푸터에 있었는데 그 창은 스스로
        /// <i>"캐릭터에게 지금 시킬 수 있어요"</i>라고 선언한다 — 종료 경로가 둘로 갈라져 있고
        /// <b>제자리에 있는 쪽이 안 보였다</b>. 탈출구는 스크롤·호버·기억에 의존하면 안 된다(원칙 4).</para>
        /// </summary>
        private void BuildFooter()
        {
            AddHorizontalDivider(_panel, -(PanelHeight - FooterHeight));

            var footGo = new GameObject("Footer", typeof(RectTransform));
            footGo.transform.SetParent(_panel, false);
            var foot = footGo.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(foot, 0f, -(PanelHeight - FooterHeight), PanelWidth, FooterHeight);

            _footerLeft = UiChrome.AddText(foot, "SaveHint", UiChrome.FontCaption, TextAnchor.MiddleLeft,
                UiChrome.InkMeta);
            UiChrome.PlaceTopLeft(_footerLeft.rectTransform, ContentPadX, 0f, 300f, FooterTopRowHeight);
            _footerLeft.text = "변경은 즉시 저장됩니다.";

            Text right = UiChrome.AddText(foot, "HowToOpen", UiChrome.FontCaption, TextAnchor.MiddleRight,
                UiChrome.InkMeta);
            SettingsControls.PlaceTopRight(right.rectTransform, ContentPadX, 0f, 380f, FooterTopRowHeight);
            // 시안의 문구는 "톱니 아이콘 클릭 · ⌃⌥⌘,"였지만, <b>실제로 존재하는 경로</b>만 적는다 —
            // 없는 문을 알려 주는 것은 이 프로젝트가 원칙 1로 금지한 "표시와 실제의 불일치"다.
            // ★ 그 시안의 쉼표도 지금은 죽은 표기다 — 2026-09-01에 P로 옮겼다(⌃⌥⌘,는 macOS 접근성
            //   "대비 줄이기" 예약 조합이라 우리가 누를 때마다 사용자 OS 설정이 바뀌었다).
            // 부채꼴 5번째 버튼은 36-11이 "만들지 않는다"로 결론지어 두었으므로 리더 판단 사항으로 남겼다.
            right.text = $"이 창을 여는 방법: 캐릭터 정보창 [설정] · {ShortcutLabel.Chord("P")}";

            // ---- 아랫줄 ----
            // C4. 이 창은 머무는 시간이 가장 긴 창이고, 바로 윗줄이 이미 "여는 방법"을 적고 있어
            //     <b>여는 법 ↔ 닫는 법</b>이 한 자리에서 짝을 이룬다.
            Text closeHint = UiChrome.AddText(foot, "CloseHint", UiChrome.FontCaption,
                TextAnchor.MiddleLeft, UiChrome.InkMeta);
            closeHint.raycastTarget = false;
            UiChrome.PlaceTopLeft(closeHint.rectTransform, ContentPadX, -(FooterTopRowHeight + 6f), 536f, 14f);
            // ★ 2026-09-02 — "창 밖 아무 곳이나 클릭하면"이 거짓이 됐다. 문장을 지우지 않고
            //   <b>거짓인 절반만</b> 도려낸다: 이 줄이 사라지면 윗줄의 "여는 방법"이 짝을 잃는다.
            closeHint.text = "[✕]를 누르면 닫혀요.";

            // 2단 확인은 <b>그대로</b>다 — 창 크롬으로 올라갔다고 위험도가 내려가지 않는다.
            // ★ F1(2026-09-06) — 면이 곧 어포던스다. 창 바탕 대비 1.32 → <b>4.88 : 1</b>.
            //   생성값과 갱신값이 갈라지지 않게 <b>ApplyQuitStyle()과 같은 토큰</b>을 쓴다.
            _quitSurface = UiChrome.AddSurface(foot, "Quit", SettingsControls.ControlFaceOnPanel,
                UiChrome.RadiusChip);
            _quitRect = _quitSurface.rectTransform;
            UiChrome.AddOutline(_quitRect, "Outline", SettingsControls.OutlineOnPanel, UiChrome.RadiusChip);
            // 색은 사다리를 경유한다(직접 고르지 않는다) — 탈출구는 이 창에서 가장 높은 단이다.
            // ★ 밝은 면 위라 사다리가 통하지 않는다: 잉크는 <b>면에서</b> 뽑는다(면만 바꾸면 글자가 지워진다).
            _quitLabel = UiChrome.AddText(_quitRect, "Label", UiChrome.FontBody, TextAnchor.MiddleCenter,
                UiChrome.InkOnSurface(SettingsControls.ControlFaceOnPanel, UiChrome.InkRole.Title, true),
                bold: true);
            UiChrome.Stretch(_quitLabel.rectTransform);

            // ★★ 2026-09-03 — 이 칸이 <b>이 창에서 가장 위험한 칸</b>이다. 라벨은 플랫폼마다
            //   물리적으로 다르다: macOS <c>지금 종료 (⌃⌥⌘Q)</c> vs Windows
            //   <c>지금 종료 (Ctrl+Alt+Win+Q)</c> — 조합키 표기만 10자 길다(Core/ShortcutLabel).
            //   그런데 폭은 132pt 상수 하나였고, 이 라벨은 <c>HorizontalWrapMode.Overflow</c>라
            //   넘쳐도 <b>잘리지 않고 칩 밖으로 흘러</b> "테두리를 뚫고 나온 글자"가 된다.
            //   ⇒ 상수는 <b>하한</b>으로만 남기고, 실제 폭은 두 문안 중 <b>넓은 쪽</b>을 실측해 정한다.
            //     (2단 확인 중에는 라벨이 "정말 종료?"로 바뀌므로 그 문안도 같이 잰다 — 폭은 굽는
            //      시점에 한 번만 정해지니 둘 다 담을 수 있어야 한다.)
            float quitInk = Mathf.Max(SettingsControls.MeasuredWidth(_quitLabel, QuitLabelText),
                                      SettingsControls.MeasuredWidth(_quitLabel, QuitConfirmText));
            float quitWidth = Mathf.Max(QuitButtonWidth, quitInk + SettingsControls.ButtonPadX * 2f);
            SettingsControls.PlaceTopRight(_quitSurface.rectTransform, ContentPadX,
                -(FooterTopRowHeight + 2f), quitWidth, SettingsControls.ButtonHeight);

            _quitLabel.text = QuitLabelText;
            var quitButton = _quitSurface.gameObject.AddComponent<Button>();
            quitButton.targetGraphic = _quitSurface;
            quitButton.onClick.AddListener(() => { if (TryClaimAction("quit")) OnQuitClicked(); });
        }

        /// <summary>
        /// 푸터 [지금 종료] 버튼 폭의 <b>하한</b>(pt).
        ///
        /// <para>★ 2026-09-03 — 예전 주석은 <i>"Windows에서 <c>Ctrl+Alt+Win+Q</c>로 늘어나도 담기는
        /// 값"</i>이라고 적고 있었는데 <b>거짓이었다</b>. 라틴 평균 자폭을 5.5pt로만 잡아도
        /// <c>지금 종료 (Ctrl+Alt+Win+Q)</c>는 132pt를 넘고, 6.5pt면 162pt다 —
        /// <b>어떤 라틴 자폭 가정에서도</b> 초과한다. 게다가 이 개발 머신에는 Windows 폰트 스택이
        /// 없어 그 주석은 <b>한 번도 검증된 적이 없었다</b>.</para>
        ///
        /// <para>지금은 실제 폭을 <see cref="SettingsControls.MeasuredWidth"/>로 재고 이 상수는
        /// <b>하한</b>으로만 쓴다(승인된 시안의 최소 칩 크기). 하한을 남기는 이유는 문안이
        /// 짧아졌을 때 탈출구 칩이 눌러야 할 만큼도 안 되게 쪼그라들지 않게 하기 위해서다.</para>
        ///
        /// <para>★★ 2026-09-05 (M-4 종결) — <b>「영어 × Windows」 칸까지 러너로 실측했다.</b>
        /// 활성 빌드 타깃이 <c>UNITY_STANDALONE_WIN</c>인 상태라 라벨이 실제로
        /// <c>지금 종료 (Ctrl+Alt+Win+Q)</c>로 조립된 채 잰 값이다(12pt Bold, 라틴 평균 자폭 6.13pt):
        /// <list type="bullet">
        ///   <item>한국어 × Windows — 잉크 <b>150pt</b> → 칩 <b>176pt</b></item>
        ///   <item>영어 × Windows(<c>Quit now …</c>) — 잉크 <b>148pt</b> → 칩 <b>174pt</b></item>
        ///   <item>푸터 아랫줄 가용 폭 <b>587pt</b> ⇒ 최악에서도 여유 <b>+413pt</b></item>
        ///   <item>2단 확인 <c>정말 종료?</c>는 84pt라 <b>이 하한이 실제로 이긴다</b> — 상수가
        ///     하는 일이 있다는 증거다.</item>
        /// </list>
        /// 만약 폭을 다시 <b>고정 132pt</b>로 되돌리면 영어에서 <b>16pt</b>, 한국어에서 <b>18pt</b>가
        /// 칩 밖으로 샌다(<c>HorizontalWrapMode.Overflow</c>라 잘리지도 않는다).
        /// 실측·회귀는 <c>Tests/PlayMode/SettingsQuitChipLatinBudgetTests</c>가 지킨다.
        /// <b>한계</b>: 이 머신은 Windows 폰트 스택이 아니다 — 자폭은 실기에서 다를 수 있고,
        /// 이 숫자가 닫는 것은 «예산 안인가»이지 «실기 픽셀»이 아니다.</para>
        /// </summary>
        private const float QuitButtonWidth = 132f;

        // ==================== 오른쪽 세로 스크롤 레일 (41-2 결정 3) ====================
        //
        // ★ 2026-09-02 — [▲][▼]를 <b>탭 줄에서 뺐다</b>. 예전 자리는 탭바 안, 탭 5개와 같은 줄·같은
        //   높이였고 그래서 <b>"탭 넘기는 버튼"</b>으로 읽혔다(민지). 직전 라운드가 칩을 "내용 위에
        //   떠 있어서" 그리로 옮겼는데, 한 오독(미세조정 버튼)을 다른 오독(탭 페이저)으로 바꿨을 뿐이다.
        //   본문 오른쪽 세로 열로 내려가면 그 오독이 <b>성립할 수 없다</b> — 세상의 모든 세로 레일은
        //   스크롤이다.
        //
        // ★ "1 / 2" 같은 <b>페이지 표기는 넣지 않는다</b>. 썸 하나가 (a) 더 있다 (b) 얼마나 남았다
        //   (c) 지금 어디다 를 동시에 말한다. 숫자는 (c)만 말하고 (b)를 못 말하며, 무엇보다
        //   <b>이 창은 페이지가 아니라 연속 스크롤이다</b> — 적는 순간 없는 개념(페이지 경계)을
        //   사용자에게 가르치게 된다. 41-9가 `1 / 6`을 지운 것과 같은 종류의 거짓말이다.
        //
        // ★ 버튼을 <b>남기는</b> 이유: 휠이 실제로 동작하는지가 아직 미확인이다(41-12 U2 — 이 저장소에
        //   `mouseScrollDelta`/`ScrollWheel` 참조가 0건이라 지금은 휠 핸들러 자체가 없다). 막대만
        //   그려 놓고 휠이 안 되면 <b>보이는데 못 움직이는 UI</b>가 된다. 칩은 막대의 양 끝에 붙어
        //   막대의 일부로 읽힌다(macOS 10.6 이전 / Windows 표준 배치).
        //
        // ※ 옛 주석의 기각 사유("휠이 밑에 있는 남의 앱으로 새는 경계가 생긴다")는 <b>틀렸다</b> —
        //   창 밖의 휠이 밑으로 가는 것은 결함이 아니라 모든 앱의 정상 동작이다. 결론(지금은 휠을
        //   안 쓴다)은 맞지만 이유가 틀렸고, 틀린 이유는 다음 사람을 잘못된 곳으로 보낸다.

        /// <summary>레일 세로 열의 왼쪽 x(패널 기준). 카드 오른쪽 끝(676)에서 4pt 띄운 자리다.</summary>
        private const float RailX = ContentPadX + SettingsControls.CardWidth + UiChrome.Space1;   // 680

        /// <summary>레일 폭. 오른쪽 여백 20pt와 합쳐 창 오른쪽 끝까지가 정확히 40pt다.</summary>
        private const float RailWidth = 20f;

        /// <summary>칩 한 변(pt) — 레일 폭과 같다(정사각형이라 위/아래 대칭).</summary>
        private const float PageButtonSize = RailWidth;

        /// <summary>트랙/썸의 굵기(pt).</summary>
        private const float RailBarWidth = 6f;

        /// <summary>레일 상단(= 콘텐츠 상단).</summary>
        private const float RailTop = HeaderHeight + TabBarHeight;                    // 88

        /// <summary>트랙 세로 길이 — 두 칩 사이에서 위아래로 4pt씩 뗀 값.</summary>
        private const float TrackLength = ContentHeight - (PageButtonSize + UiChrome.Space1) * 2f;  // 378

        /// <summary>썸이 아무리 짧아져도 이보다 짧아지지 않는다(잡을 수 없는 실오라기가 된다).</summary>
        private const float ThumbMinLength = 32f;

        private RectTransform _trackRect;
        private RectTransform _thumbRect;
        private Image _pageUpSurface, _pageDownSurface;
        private Image _pageUpOutline, _pageDownOutline;
        private Text _pageUpLabel, _pageDownLabel;

        /// <summary>스크롤 썸의 화면 사각형(진단/테스트 창구). 레일이 숨겨져 있으면 빈 사각형이다.</summary>
        public Rect ScrollThumbScreenRect => SettingsControlHost.ScreenRectOf(_thumbRect);

        private void BuildPageButtons()
        {
            _pageUpRect = AddPageButton("PageUp", "▲", RailTop,
                out _pageUpSurface, out _pageUpOutline, out _pageUpLabel);
            _pageDownRect = AddPageButton("PageDown", "▼", RailTop + ContentHeight - PageButtonSize,
                out _pageDownSurface, out _pageDownOutline, out _pageDownLabel);

            Image track = UiChrome.AddSurface(_panel, "ScrollTrack", SettingsControls.TrackOnPanel,
                Mathf.RoundToInt(RailBarWidth * 0.5f));
            _trackRect = track.rectTransform;
            UiChrome.PlaceTopLeft(_trackRect, RailX + (RailWidth - RailBarWidth) * 0.5f,
                -(RailTop + PageButtonSize + UiChrome.Space1), RailBarWidth, TrackLength);
            track.raycastTarget = false;

            Image thumb = UiChrome.AddSurface(_panel, "ScrollThumb", UiChrome.TextTertiary,
                Mathf.RoundToInt(RailBarWidth * 0.5f));
            _thumbRect = thumb.rectTransform;
            UiChrome.PlaceTopLeft(_thumbRect, RailX + (RailWidth - RailBarWidth) * 0.5f,
                -(RailTop + PageButtonSize + UiChrome.Space1), RailBarWidth, ThumbMinLength);
            thumb.raycastTarget = false;

            SyncPageButtons();
        }

        /// <summary>
        /// 레일 끝 칩 한 개. <b>겉모습은 여기서 만들지 않고</b> <see cref="ApplyPageChipEnabled"/>가
        /// 정한다 — 태어난 모습과 갱신된 모습이 두 벌이면 반드시 한쪽만 고쳐진다(그게 이 라운드가
        /// 정보창 카드에서 본 형태다). 여기서는 <b>같은 함수로 초기 상태를 한 번 칠할</b> 뿐이다.
        /// </summary>
        private RectTransform AddPageButton(string name, string glyph, float y,
            out Image surfaceOut, out Image outlineOut, out Text labelOut)
        {
            Image surface = UiChrome.AddSurface(_panel, name, PageChipFace(true), UiChrome.RadiusChip);
            UiChrome.PlaceTopLeft(surface.rectTransform, RailX, -y, PageButtonSize, PageButtonSize);
            Image outline = UiChrome.AddOutline(surface.rectTransform, "Outline",
                UiChrome.Flatten(UiChrome.CardBorder, PageChipFace(true)), UiChrome.RadiusChip);
            Text label = UiChrome.AddText(surface.rectTransform, "Label", UiChrome.FontCaption,
                TextAnchor.MiddleCenter,
                UiChrome.InkOnSurface(PageChipFace(true), UiChrome.InkRole.Title, true));
            UiChrome.Stretch(label.rectTransform);
            label.text = glyph;

            var button = surface.gameObject.AddComponent<Button>();
            button.targetGraphic = surface;
            int direction = glyph == "▲" ? -1 : +1;
            string key = name;
            button.onClick.AddListener(() =>
            {
                if (!CanScroll(direction)) return;   // 끝에 닿은 칩은 <b>아무 일도 하지 않는다</b>.
                if (TryClaimAction(key)) ScrollPage(direction);
            });
            surfaceOut = surface;
            outlineOut = outline;
            labelOut = label;
            return surface.rectTransform;
        }

        /// <summary>지금 탭에서 그 방향으로 <b>실제로 움직일 수 있는가</b>. 칩의 겉모습과 클릭 처리가
        /// <b>같은 하나</b>를 본다 — 두 벌로 두면 반드시 한쪽만 갱신되고, 그게 곧 표시-실제 불일치다.</summary>
        private bool CanScroll(int direction)
        {
            int i = (int)_tab;
            float max = Mathf.Max(0f, _pageHeights[i] - ContentHeight);
            if (max <= 0f) return false;
            return direction < 0 ? _pageScroll[i] > 0.5f : _pageScroll[i] < max - 0.5f;
        }

        private void ScrollPage(int direction)
        {
            int i = (int)_tab;
            float max = Mathf.Max(0f, _pageHeights[i] - ContentHeight);
            float next = Mathf.Clamp(_pageScroll[i] + direction * PageStep, 0f, max);
            if (Mathf.Approximately(next, _pageScroll[i])) return;
            _pageScroll[i] = next;
            ApplyScroll();
            SyncPageButtons();   // 방금 끝에 닿았을 수 있다.
        }

        private void ApplyScroll()
        {
            RectTransform page = _pages[(int)_tab];
            if (page == null) return;
            page.anchoredPosition = new Vector2(0f, _pageScroll[(int)_tab]);
            SyncScrollThumb();
        }

        /// <summary>썸의 길이와 자리를 지금 페이지에 맞춘다. <b>값이 바뀔 때만</b> 부른다 —
        /// 이 창은 매 프레임 레이아웃을 다시 계산하지 않는다(하루 종일 켜져 있는 앱).</summary>
        private void SyncScrollThumb()
        {
            if (_thumbRect == null) return;
            int i = (int)_tab;
            float pageHeight = Mathf.Max(1f, _pageHeights[i]);
            float overflow = pageHeight - ContentHeight;
            if (overflow <= 0f) return;   // 레일 자체가 꺼져 있다.

            float length = Mathf.Clamp(TrackLength * ContentHeight / pageHeight, ThumbMinLength, TrackLength);
            float travel = Mathf.Max(0f, TrackLength - length);
            float y = travel * Mathf.Clamp01(_pageScroll[i] / overflow);   // 0으로 나누지 않는다(overflow>0).

            _thumbRect.sizeDelta = new Vector2(RailBarWidth, length);
            UiChrome.PlaceTopLeft(_thumbRect, RailX + (RailWidth - RailBarWidth) * 0.5f,
                -(RailTop + PageButtonSize + UiChrome.Space1 + y), RailBarWidth, length);
        }

        /// <summary>페이지가 안 넘치는 탭(지금은 죽은 탭 3개)에서는 <b>레일 전체</b>를 숨긴다 —
        /// 썸이 트랙을 꽉 채운 모습은 "고장"으로 읽힌다.</summary>
        private void SyncPageButtons()
        {
            bool scrollable = _pageHeights[(int)_tab] > ContentHeight + 0.5f;
            SetRailPartActive(_pageUpRect, scrollable);
            SetRailPartActive(_pageDownRect, scrollable);
            SetRailPartActive(_trackRect, scrollable);
            SetRailPartActive(_thumbRect, scrollable);
            if (!scrollable) return;

            SyncScrollThumb();

            // ★ 2026-09-02 — 옛 코드는 "스크롤 가능하면 <b>둘 다</b> 보이기"에서 멈췄다. 그래서
            //   맨 위에서 [▲]가, 맨 아래에서 [▼]가 <b>완전히 활성으로 보이면서 아무 일도 안 했다</b>.
            //   보관함이 앓던 같은 병이 이 새 레일에 그대로 복제된 것이다. 칩을 숨기지는 않는다 —
            //   레일의 양 끝 캡이라 하나가 사라지면 막대가 <b>고장 난 것처럼</b> 보인다. 대신 죽인다.
            //   ★ 여기서도 규칙은 같다: <b>면을 죽이고 글자는 그 면에서 파생시킨다</b>(A와 같은 뿌리).
            ApplyPageChipEnabled(_pageUpSurface, _pageUpOutline, _pageUpLabel, CanScroll(-1));
            ApplyPageChipEnabled(_pageDownSurface, _pageDownOutline, _pageDownLabel, CanScroll(+1));
        }

        /// <summary>
        /// 레일 끝 칩의 <b>겉모습을 정하는 단 하나의 자리</b>. 끝에 닿은 칩을 죽이되 <b>숨기지 않는다</b> —
        /// 레일 양 끝 캡이라 하나가 사라지면 막대 자체가 고장 난 것처럼 보인다.
        ///
        /// <para>★★ 2026-09-06 (F1, <c>docs/UI_ALPHA_BLEED_POLICY.md</c> §7-3) — <b>어포던스가
        /// 글리프에서 「면」으로 옮겨 왔다.</b> 예전에는 칩의 면이 <see cref="UiChrome.CardSurfaceMuted"/>
        /// 하나로 고정이었고(창 바탕 대비 <b>1.01 : 1</b> — 창 바탕보다 <b>어두워서</b> 칩이 사실상
        /// 없었다) 살아 있음/죽음을 화살표 밝기만으로 말했다. 이제 살아 있는 칩은
        /// <see cref="SettingsControls.ControlFaceOnPanel"/>(창 바탕 대비 <b>4.88 : 1</b>)로 서고,
        /// 죽은 칩은 예전 면 그대로 물러난다.</para>
        ///
        /// <para>★ <b>글리프 잉크의 문이 바뀐 이유</b>: 밝은 면 위에서는 아이콘 사다리
        /// (<see cref="UiChrome.InkIcon"/>)가 <b>통하지 않는다</b>. 면만 밝히고 사다리를 그대로 두면
        /// 화살표가 지워진다(정책 §2-E "면과 잉크는 한 쌍이다"). 그래서 살아 있는 칩과 죽은 칩 모두
        /// <b>자기 면에서</b> 잉크를 받는다(<see cref="UiChrome.InkOnSurface"/>) — 문을 둘로 나누면
        /// 언젠가 한쪽만 고쳐진다. 두 상태의 잉크 밝기 서열이 뒤집히는 것은 결함이 아니라
        /// <b>어포던스 축이 면으로 옮겨간 결과</b>다: 죽은 칩은 어두운 면 위의 밝은 화살표,
        /// 살아 있는 칩은 밝은 면 위의 어두운 화살표이고, "누를 수 있음"을 말하는 것은 <b>면</b>이다.</para>
        ///
        /// <para>비활성 칩이 비텍스트 3 : 1을 안 지키는 것은 의도다 — WCAG 2.2 §1.4.11이 비활성
        /// 컴포넌트를 명시적으로 면제한다(꺼진 스위치 손잡이 1.70 : 1과 같은 근거).</para>
        /// </summary>
        private static void ApplyPageChipEnabled(Image surface, Image outline, Text glyph, bool enabled)
        {
            if (outline == null || glyph == null) return;

            Color face = PageChipFace(enabled);
            if (surface != null && surface.color != face) surface.color = face;

            // 테두리는 <b>자기 면 위에</b> 합성한다 — 면이 바뀌었는데 옛 바탕에 합성하면 α가 그대로 남아
            // 뒤 창이 비친다(이 라운드가 닫는 결함 그 자체다).
            Color edge = UiChrome.Flatten(enabled ? UiChrome.CardBorder : UiChrome.Divider, face);
            if (outline.color != edge) outline.color = edge;

            Color ink = UiChrome.InkOnSurface(face, UiChrome.InkRole.Title, enabled);
            if (glyph.color != ink) glyph.color = ink;
        }

        /// <summary>레일 끝 칩의 면 — 살아 있으면 F1 슬래브, 죽었으면 물러난 면.
        /// <para>생성부와 갱신부가 <b>같은 함수</b>를 부른다. 두 벌로 두면 한쪽만 고쳐진다.</para></summary>
        private static Color PageChipFace(bool enabled)
            => enabled ? SettingsControls.ControlFaceOnPanel : UiChrome.CardSurfaceMuted;

        private static void SetRailPartActive(RectTransform rt, bool active)
        {
            if (rt != null && rt.gameObject.activeSelf != active) rt.gameObject.SetActive(active);
        }

        private void SetTab(Tab tab, string source)
        {
            if (_tab == tab) return;
            _tab = tab;
            DisarmQuit();
            FlushPendingSave();
            ApplyTabVisibility();
            Debug.Log($"[설정창] 탭 전환 -> [{TabNames[(int)tab]}]({source}).");
        }

        /// <summary>
        /// 이 탭에 <b>내용이 있는가</b> — 탭 <b>안쪽</b>(회색 행)과 탭 <b>버튼</b>이 같은 하나를 본다.
        ///
        /// <para>2026-09-01까지 이 판정은 <see cref="BuildPages"/>의 <c>switch</c> 안에만 있었고
        /// <see cref="ApplyTabVisibility"/>는 다섯 탭을 <b>똑같이</b> 칠했다. 그래서 회색 처리가 탭을
        /// <b>누른 뒤에야</b> 보였고, 첫 방문에서 5탭 중 3탭이 헛걸음이 됐다(페르소나 M7).
        /// 판정을 두 벌로 두면 반드시 한쪽만 갱신되므로 <b>여기 하나</b>로 합친다.</para>
        /// </summary>
        private static bool IsTabReady(Tab tab) => tab == Tab.General || tab == Tab.Character;

        private void ApplyTabVisibility()
        {
            for (int i = 0; i < TabCount; i++)
            {
                bool active = i == (int)_tab;
                bool ready = IsTabReady((Tab)i);
                if (_pages[i] != null && _pages[i].gameObject.activeSelf != active)
                    _pages[i].gameObject.SetActive(active);
                if (_tabLabels[i] != null)
                {
                    // 준비 중인 탭은 <b>고르기 전에</b> 그렇게 보인다. 다만 골랐을 때는 완전히 죽이지
                    // 않는다 — "내가 지금 어디에 있는지"는 빈 탭에서도 읽혀야 한다.
                    // ★ 고르지 않은 탭은 준비 여부로 <b>더 흐려지지 않는다</b>. 옛 코드의 2.35:1이
                    //   "죽은 탭에는 글자가 한 자도 없다"는 신고를 만들었다 — 글자는 있었다.
                    //   준비 중이라는 사실은 아래 밑줄과 탭 내용이 말한다.
                    _tabLabels[i].color = UiChrome.InkTab(active, ready);
                    _tabLabels[i].fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
                }
                if (_tabUnderlines[i] != null)
                {
                    // 밑줄 색도 같은 말을 한다: 파란 밑줄(Accent)은 "여기 내용이 있다"의 표시였다.
                    _tabUnderlines[i].color = active
                        ? (ready ? UiChrome.Accent : UiChrome.NonTextMuted)
                        : Color.clear;
                }
            }
            ApplyScroll();
            SyncPageButtons();
        }

        private static void AddHorizontalDivider(RectTransform parent, float y)
        {
            Image divider = UiChrome.AddSurface(parent, "Divider", SettingsControls.DividerOnPanel, 2);
            UiChrome.PlaceTopLeft(divider.rectTransform, 0f, y, PanelWidth, 1f);
            divider.raycastTarget = false;
        }

        // ==================== 좌표 / 차단막 ====================

        private void ApplyCanvasScaleFactor()
        {
            if (_scaler == null) return;
            float target = ScreenCoordinateConverter.ResolveCanvasScaleFactor(_config);
            if (!Mathf.Approximately(_scaler.scaleFactor, target)) _scaler.scaleFactor = target;

            // ★ 2026-09-07 — 창이 움직일 수 있게 된 순간부터 «화면이 작아졌다 / 배율이 바뀌었다»가
            //   창을 화면 밖으로 밀어낼 수 있다. 드래그와 <b>같은 규칙</b>으로 매 프레임 되끌어온다
            //   (정보창의 <c>ClampPanelToScreen</c>이 하는 일과 같다).
            //
            // ★★ <b>옮긴 적이 없으면 손대지 않는다</b> — 이 가드가 없으면 회귀가 난다.
            //   이 창은 720×560 <b>고정</b>이라(정보창과 달리 줄이지 않는다) 화면이 그보다 낮으면
            //   <c>SurfaceSafeAreaPolicy</c>의 «넘칠 때는 상단 우선» 규칙이 발동해 창이 위로 붙는다.
            //   그건 그 자체로는 옳지만 <b>이 라운드가 요청받은 변경이 아니다</b>(배치모드 480px
            //   화면에서 설정창 전체가 56pt 위로 올라간다 = 아무도 부탁하지 않은 이동).
            //   그래서 «사용자가 실제로 옮긴 창»에만 적용한다 — 그 집합 밖에서는 한 픽셀도 안 바뀐다.
            if (_panel == null || !UiLayoutModel.HasWindowOffset(UiWindowId.Settings)) return;
            Vector2 clamped = ClampPanelPosition(_panel.anchoredPosition);
            if (clamped != _panel.anchoredPosition) _panel.anchoredPosition = clamped;
        }

        private void SyncClickBlocker()
        {
            if (_clickBlocker == null || _panel == null) return;
            Camera cam = _agent != null && _agent.Blackboard != null ? _agent.Blackboard.MainCamera : Camera.main;
            if (cam == null) { _clickBlocker.enabled = false; return; }

            _panel.GetWorldCorners(_corners);
            float depth = Mathf.Abs(cam.transform.position.z);
            Vector3 bl = cam.ScreenToWorldPoint(new Vector3(_corners[0].x, _corners[0].y, depth));
            Vector3 tr = cam.ScreenToWorldPoint(new Vector3(_corners[2].x, _corners[2].y, depth));

            _clickBlocker.enabled = true;
            _clickBlocker.transform.position = new Vector3((bl.x + tr.x) * 0.5f, (bl.y + tr.y) * 0.5f, 0f);
            _clickBlocker.size = new Vector2(Mathf.Abs(tr.x - bl.x), Mathf.Abs(tr.y - bl.y));
        }

        /// <summary>마스크에 잘린 자리는 <b>눌리지 않는다</b>(R2 M3의 규칙을 이 창에도 그대로 적용).
        /// 페이지를 넘겨 화면 밖으로 나간 행이 계속 눌리면 그것이 이 프로젝트가 "최악"이라고 부르는
        /// 패턴이다 — 안 보이는데 클릭은 먹는 UI.</summary>
        private bool ContainsScreenPoint(RectTransform rt, Vector2 screenPoint)
        {
            if (!RectContainsScreenPoint(rt, screenPoint)) return false;
            if (_masks == null || rt == null) return true;
            for (int i = 0; i < _masks.Length; i++)
            {
                RectMask2D mask = _masks[i];
                if (mask == null || !mask.isActiveAndEnabled) continue;
                RectTransform maskRect = mask.rectTransform;
                if (maskRect == null || maskRect == rt || !rt.IsChildOf(maskRect)) continue;
                if (!RectContainsScreenPoint(maskRect, screenPoint)) return false;
            }
            return true;
        }

        private static bool RectContainsScreenPoint(RectTransform rt, Vector2 screenPoint)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return false;
            rt.GetWorldCorners(_corners);
            return screenPoint.x >= _corners[0].x && screenPoint.x <= _corners[2].x &&
                   screenPoint.y >= _corners[0].y && screenPoint.y <= _corners[2].y;
        }

        /// <summary>씬에 EventSystem이 있어도 입력 모듈이 없으면 Button.onClick이 영원히 발동하지 않는다
        /// (이 프로젝트가 실제로 밟았던 함정) — 다른 창들과 같은 보강.</summary>
        private static void EnsureEventSystem()
        {
            EventSystem existing = EventSystem.current != null
                ? EventSystem.current
                : FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (existing != null)
            {
                if (existing.GetComponent<BaseInputModule>() == null)
                    existing.gameObject.AddComponent<StandaloneInputModule>();
                return;
            }
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(go);
        }
    }
}
