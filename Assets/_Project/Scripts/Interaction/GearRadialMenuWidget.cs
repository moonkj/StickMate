using UnityEngine;
using UnityEngine.UI;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// 부채꼴 메뉴의 네 칸. <b>값이 곧 슬롯 순서</b>다 — θ₀ + (1.5 − i)·30°
    /// (i=0 → θ₀+45°, i=3 → θ₀−45°). docs/UX_FLOW.md 36-3-4.
    ///
    /// ★ <b>기존 0/1/2는 재번호하지 않는다.</b> 32절이 "값이 곧 슬롯 순서"라고 못박았으므로 중간 삽입은
    /// Todo 2→3 재번호를 일으키고, 그 값을 읽는 switch가 조용히 어긋날 수 있다. 네 버튼이 기어에서
    /// 등거리(118~123pt)라 슬롯별 조작 비용 차이가 없어 "새 항목을 좋은 자리에" 논쟁 자체가 성립하지
    /// 않는다 — 관례대로 끝에 붙인다.
    /// </summary>
    public enum GearMenuButton
    {
        FocusMode = 0,
        Character = 1,
        Todo = 2,

        /// <summary>2026-08-31 신설 — 행동 명령창(<see cref="ActionCommandPopover"/>) 진입점.
        /// 이 버튼은 캐릭터의 상태를 <b>보는</b> 곳이 아니라 캐릭터에게 <b>시키는</b> 곳이다(36-5).</summary>
        Action = 3,
    }

    /// <summary>접힘의 종류 — <b>움직임이 서로 달라야</b> "내가 접었다"와 "시간이 지나 닫혔다"가 구분된다.</summary>
    public enum GearMenuCollapseMode
    {
        /// <summary>사용자 동작(기어 재클릭 / 바깥 클릭 / 버튼 선택). 0.13초, 반지름까지 빨려 들어감.</summary>
        User,

        /// <summary>기어를 옮기기 시작함. 0.08초, 가장 빠르게 치운다.</summary>
        Drag,

        /// <summary>6초 무반응. 0.26초, 제자리에서 스르르(반지름 고정, 알파만).</summary>
        Auto,
    }

    /// <summary>
    /// ★ 톱니를 짧게 클릭했을 때 <b>촤르륵 펼쳐지는 원버튼 4개</b> — docs/UX_FLOW.md <b>32절 + 36절</b>.
    /// 2026-08-30 사용자 원문: "기어메뉴를 클릭했을때 집중모드 버튼 캐릭터 버튼 오늘 할일 버튼 3가지가
    /// 촤르륵 원버튼 3개가 나오고 각 버튼을 클릭했을때 세부 메뉴로 들어가도록".
    /// 2026-08-31 사용자 원문: "버튼 메뉴들의 텍스트는 전부삭제 필요 ... 기어아이콘에 메뉴하나 추가해서
    /// 행동들은 거기서 클릭하면 창 하나가 떠서 행동 명령 내릴수 있게".
    ///
    /// ============================================================================
    /// 왜 버튼을 4개로 늘렸는데 부채꼴이 <b>더</b> 튼튼해지는가 (36-3, 반직관적이지만 계산이 그렇다)
    /// ============================================================================
    /// 32-1의 3개 배치(60° 간격 / R62)는 스팬이 <b>120°</b>였고, 기본 기어 위치에서 이미 평행이동
    /// 35.5pt를 받아 기어→버튼 실거리가 84/97/89pt로 제각각이었다 — "궤도"라는 말이 사실이 아니었다.
    /// 모서리에서 부채꼴을 막는 것은 반지름이 아니라 <b>각도 스팬</b>이므로, 간격을 30°로 줄이고 반지름을
    /// 111pt로 키우면 스팬이 <b>90°</b>가 되어 평행이동이 사실상 0이 된다(실측 격자 전수 계산: 평균
    /// 0.7pt / 최대 9pt, 세로일렬 폴백 0건). 반지름은 기어에서 화면 <b>안쪽</b>으로 뻗는 방향이라 화면
    /// 여백을 거의 소모하지 않는다. 결과적으로 기어→버튼 실거리가 118~123pt로 균일해진다.
    ///
    /// ============================================================================
    /// 라벨(이름표)은 <b>전부 지웠다</b> — 그리고 그 비용은 다른 데서 갚는다 (36-4)
    /// ============================================================================
    /// 32-1은 "심볼만 있는 원은 반드시 오독된다(툴팁을 띄울 상시 포커스가 없다)"며 라벨 알약을 넣었고,
    /// 그 논거 자체는 여전히 옳다. 지우라는 것은 사용자 지시이므로 지우되 비용은 <b>온보딩 1회 안내</b>
    /// (35-2)로 갚는다 — 아이콘 전용 내비게이션의 정답은 툴팁이 아니라 최초 1회 학습이다(Dock/툴바의
    /// 실증 사례). 호버 피드백(표면/테두리/심볼 색 0.09초)은 그대로 남으므로 "지금 이걸 누르면 이게
    /// 눌린다"는 보장은 유지된다.
    ///
    /// 라벨 <b>알약</b>(버튼마다 상시 붙어 있던 이름표) 코드는 완전히 지웠다 — 끄기 위해 죽은 채로
    /// 남겨두는 코드는 반드시 썩고, 라벨 폭이 클램프 상자 계산에 다시 끼어들면 위 36-3의 기하 근거가
    /// 조용히 무너지기 때문이다.
    ///
    /// ============================================================================
    /// ★ 호버 이름표 — 2026-08-31 사용자 추가 지시
    /// ============================================================================
    /// 사용자 원문: <i>"기어메뉴에서 4가지중 마우스로 선택되고있는 메뉴만 텍스트로 어떤 메뉴인지 이름이
    /// 보여야함"</i>. 즉 <b>상시 이름표는 없애되, 커서가 올라간 하나만</b> 이름을 보여준다.
    ///
    /// 이것은 앞선 지시("텍스트 전부 삭제")를 뒤집는 것이 아니라 <b>36-4가 인정한 비용을 직접 갚는
    /// 것</b>이다. 32-1은 "심볼만 있는 원은 반드시 오독된다(이 앱은 툴팁을 띄울 상시 포커스가 없다)"고
    /// 했고 그 논거는 옳았다. 그런데 우리는 이미 <see cref="InfoGearIconWidget"/>에서 커서를 전역
    /// 폴링하고 있으므로 <b>"상시 포커스가 없다"는 전제 자체가 더 이상 사실이 아니다</b> — 툴팁을 띄울
    /// 수 있다. 화면에는 언제나 이름이 <b>최대 하나</b>만 있으므로 "글자를 없애 달라"는 요구(어수선함
    /// 제거)와 "무슨 버튼인지 알고 싶다"는 요구가 동시에 만족된다.
    ///
    /// <b>★ 설계 제약(36-3의 기하를 지키기 위해 반드시 지킨다)</b>:
    ///  ① 호버 이름표는 <b>배치 계산에 참여하지 않는다.</b> 클램프 상자는 여전히 56×56 정사각이고
    ///     버튼 위치는 이름표가 있든 없든 같다. 이름표를 상자에 넣는 순간 36-3-1의 전수 계산 근거가
    ///     무너진다(그게 정확히 예전 라벨 알약이 저지른 일이다). 대신 <b>이름표 자신이</b> 화면 여백
    ///     안으로 클램프된다(아래로 안 들어가면 원 위로 뒤집는다).
    ///  ② <b>인스턴스는 하나뿐이다.</b> 버튼마다 하나씩 두지 않는다 — 동시에 한 커서만 존재하므로
    ///     "두 개가 같이 보이는" 상태를 <b>구조적으로 불가능</b>하게 만드는 편이 그 버그를 테스트로
    ///     쫓는 것보다 싸다.
    ///  ③ <see cref="UnionScreenRect"/>(클릭관통 차단 영역)에 <b>포함하지 않는다.</b> 이름표는 누를
    ///     수 있는 물건이 아니므로, 포함시키면 차단 영역만 넓어져 비침해가 나빠진다(원칙 2).
    ///
    /// 남는 글자는 이 호버 이름표와 <b>오늘 할일 미완료 배지</b>뿐이며, 배지는 이름표가 아니라
    /// <b>상태 수량</b>이다(지우면 "안 읽은 개수"라는 정보가 앱에서 사라지고 대체 표시 수단이 없다).
    ///
    /// ============================================================================
    /// 왜 LineRenderer 선화가 아니라 uGUI인가 (32-9 (E))
    /// ============================================================================
    /// 이 앱의 시각 요소는 두 부류다. <b>감상하는 것</b>(캐릭터/톱니/타이머 링)은 선화, <b>읽고 눌러야
    /// 하는 것</b>(우클릭 메뉴/포스트잇/캐릭터 창)은 불투명 표면 + 테두리 + 그림자다. 부채꼴 버튼은
    /// 후자다 — 사용자의 임의의 바탕화면(사진/코드 편집기) 위에 그려지는데 1.5pt 선 한 겹으로는 대비를
    /// 보장할 방법이 없고, <b>흰 잉크 프리셋에서는 밝은 배경 위에서 사실상 사라진다</b>. 그래서 심볼
    /// 색도 잉크색을 따라가지 않고 <see cref="UiChrome"/>의 고정 색을 쓴다.
    ///
    /// ============================================================================
    /// 입력은 이 컴포넌트가 폴링하지 않는다 (소유권 단일화)
    /// ============================================================================
    /// 커서/버튼 폴링은 <see cref="InfoGearIconWidget"/> 한 곳에서만 한다. 여기는 그 결과를 받아
    /// <see cref="HitTest"/>로 "어느 버튼인가"만 답하고 그림을 그린다. 폴링 주체가 둘이 되면 "누가
    /// 클릭을 먹었는가"가 두 곳에서 판정되는데, 이 프로젝트는 그 함정을 이미 여러 번 밟았다.
    ///
    /// ============================================================================
    /// 화면 밖으로 나가지 않는다 — <b>부채꼴 전체를 회전</b>시킨다 (32-1)
    /// ============================================================================
    /// 개별 버튼을 화면 안으로 밀어 넣으면 모서리에서 네 버튼이 한 점으로 뭉개져 히트 원이 겹치고,
    /// 그러면 <b>보이는 것과 실제로 눌리는 것이 달라진다</b>(먼저 검사되는 버튼이 이긴다). 그래서
    /// 형태를 유지한 채 문제를 푼다: ① θ₀를 ±15°씩 최대 ±90°까지 돌려보고 → ② 세로 일렬 폴백 →
    /// ③ 지름 축소(44→36) → ④ 그래도 안 되면 <b>네 버튼을 같은 벡터로</b> 평행이동(형태 보존).
    ///
    /// 기준각 θ₀는 사분면 부호가 아니라 <b>(화면 중심 − 기어 중심)의 실제 각도를 45° 단위로 스냅</b>한
    /// 값이다. 부호 방식이면 기어가 화면 위쪽 한가운데 있을 때 아래로 곧게 못 펼치고, 중앙선 근처에서
    /// 1픽셀 이동에 방향이 90° 튄다. 그리고 <b>펼치는 순간 한 번 계산해 그 열림이 끝날 때까지 고정</b>한다.
    /// </summary>
    public sealed class GearRadialMenuWidget : MonoBehaviour, IExclusiveSurface
    {
        public const int ButtonCount = 4;

        // ==================== 확정 수치 (docs/UX_FLOW.md 32-1 / 32-2) ====================

        public const float ButtonDiameterPoints = 44f;
        public const float ShrunkDiameterPoints = 36f;
        public const float HoverScale = 48f / 44f;
        /// <summary>36-3-3: 62 → 111pt. 하한은 기어 판정 반경 31.36 + 버튼 판정 26 = 57.4pt이므로
        /// 여유 53.6pt다. 반지름은 화면 안쪽으로 뻗어 여백을 거의 소모하지 않는다.</summary>
        public const float OrbitRadiusPoints = 111f;

        /// <summary>36-3-3: 60 → 30°. 4개 × 30° = 스팬 90°(기존 3개 × 60° = 120°보다 <b>좁다</b>).</summary>
        public const float ButtonAngleStepDegrees = 30f;

        // ★ 2026-09-02 — <b>원버튼 그림자와 그 번짐 상수(3pt)가 여기 있었다.</b> 사용자 지시
        //   "캐릭터창 둘레로도 그림자들이 있는데 다 없애줘 깔끔하게"로 UI 그림자를 전부 걷어냈다
        //   (UiChrome의 제거 노트 참고). 이웃 버튼 사이 틈은 이제 램프가 하나도 먹지 않아
        //   13.5pt 전부가 빈 공간이다 — "원 메뉴가 겹쳐 보인다"의 여지가 더 줄었다.
        //
        //   ★ 부수 효과로 <b>직전 신고가 함께 사라진다</b>: "오늘할일 등을 클릭했을 때 나머지 3메뉴가
        //   이상한 그림자로 남겨있음". 원인은 조립부가 그림자 Image의 반환값을 받지 않아 ButtonView에
        //   참조가 없었고, 그래서 접힘(=알파 페이드) 대상 목록에서 그림자만 빠져 있었던 것이다.
        //   스케일은 0.72까지만 줄어들어 알파 1.0짜리 검은 원 3개가 그대로 남았다. 그림자가 없으면
        //   남을 것도 없다. <b>그림자를 되살릴 때는 반드시 ApplyButtonStyle의 페이드 목록에 넣을 것.</b>

        public const float HitPaddingPoints = 4f;
        public const float ScreenMarginPoints = 8f;

        /// <summary>
        /// 화면 <b>위쪽</b>만 여백이 다르다 — OS가 예약한 상단 띠(macOS 메뉴바 / Windows 상단 도킹
        /// 작업표시줄)를 덮지 않기 위해서다. 궤도 62pt를 도는 버튼은 위로 뻗으면 그 띠를 가린다
        /// (실측 스크린샷에서 실제로 그랬다).
        ///
        /// <para>★ 이 값은 <b>하한</b>이다. 실제로 쓰는 값은 <see cref="EffectiveTopMarginPoints"/>가
        /// OS 보고값과 비교해 정한다 — 40은 macOS 메뉴바(33)를 넉넉히 덮지만 <b>Windows에는 그 근거가
        /// 없다</b>(작업표시줄이 위에 도킹되면 40pt 이상이고 배율 150%에서 더 두껍다).</para>
        /// </summary>
        public const float TopMarginPoints = 40f;

        /// <summary>클램프 상자 = 원 지름 + 이만큼(Ø44 → <b>56×56 정사각</b>, 중심 = 원 중심).
        /// 라벨이 사라지면서 상자의 비대칭 세로 오프셋(중심 아래 +10pt)도 함께 사라졌다 — 이제 상자와
        /// 원의 중심이 같으므로 "보이는 원"과 "화면 밖 판정 영역"이 처음으로 같은 것을 가리킨다.</summary>
        public const float ClampBoxPaddingPoints = 12f;

        public const float ExpandSecondsPerButton = 0.19f;

        /// <summary>36-3-3: 0.055 → 0.037초. "촤르륵"의 예산은 <b>0.30초로 정해져 있다</b>(32-2).
        /// 0.055를 그대로 두면 버튼 4개에서 0.355초가 된다 — 버튼이 하나 늘었다고 사용자를 매번 18%
        /// 더 기다리게 만들지 않는다. 0.19 + 0.037×3 = 0.301초로 예산 안에 들어온다.</summary>
        public const float ExpandStaggerSeconds = 0.037f;

        public const float AlphaFadeInSeconds = 0.11f;
        public const float StartRadiusFraction = 0.35f;
        public const float StartScale = 0.62f;

        public const float CollapseUserSeconds = 0.13f;
        public const float CollapseDragSeconds = 0.08f;
        public const float CollapseAutoSeconds = 0.26f;
        public const float AutoCollapseIdleSeconds = 6f;

        public const float HoverSeconds = 0.09f;

        // ---- 호버 이름표(2026-08-31 사용자 추가 지시) ----

        /// <summary>이름표 알약 높이. 부채꼴 배치 계산에는 <b>참여하지 않는다</b>(위 클래스 문서 ①).</summary>
        public const float HoverLabelHeightPoints = 18f;

        /// <summary>원 가장자리에서 이름표까지의 간격.</summary>
        public const float HoverLabelGapPoints = 8f;

        /// <summary>글자 좌우 여백(알약 폭 = 글자 폭 + 이것).</summary>
        public const float HoverLabelPaddingPoints = 14f;

        /// <summary>나타나고 사라지는 시간 — 원의 호버 강조(<see cref="HoverSeconds"/>)와 <b>같은 값</b>을
        /// 쓴다. 이름이 강조보다 늦게 뜨면 "느리다"가 아니라 "따로 논다"로 보인다.</summary>
        public const float HoverLabelFadeSeconds = HoverSeconds;
        public const float PressFlashSeconds = 0.09f;
        public const float MinClickableProgress = 0.5f;

        // ---- 최초 1회 온보딩 안내(2026-09-01 — 36-4가 라벨 삭제의 대가로 약속한 부채의 지급) ----
        //
        // ★ 이 클래스 문서가 스스로 적어 둔 빚이다: "지우라는 것은 사용자 지시이므로 지우되 비용은
        //   <b>온보딩 1회 안내</b>(35-2)로 갚는다 — 아이콘 전용 내비게이션의 정답은 툴팁이 아니라
        //   최초 1회 학습이다." 그런데 그 코드가 없어서(2026-09-01 페르소나 M13, grep 0건) 비용을
        //   매번 사용자가 대신 내고 있었다. 여기가 그 지급이다.
        //
        // ★ 형식은 35-2-4의 "팁" 규격을 따른다: 말풍선이 아니라 <b>작은 알약 캡션 4.5초</b>.
        //   말풍선을 쓰면 상태에서 파생되지 않은 문자열이 대사에 섞여 원칙 1이 흐려진다.
        //
        // ★ <b>반복 노출 금지</b>가 이 기능의 절반이다. 이미 아는 사용자에게 또 뜨면 그게 방해다
        //   (원칙 2). 그래서 "봤다"는 사실은 <b>디스크에</b> 남고, 뜨는 그 순간 기록한다.

        /// <summary>안내 알약이 화면에 머무는 시간(초) — 35-2-4의 4.5초.</summary>
        public const float OnboardingHintSeconds = 4.5f;

        /// <summary>한 줄이다. 두 줄이 필요하면 그건 안내가 아니라 설명서다.</summary>
        public const string OnboardingHintText = "커서를 올리면 각 버튼 이름이 보여요";

        /// <summary>
        /// "이 안내를 이미 봤다"의 저장 자리.
        ///
        /// <para>★ <b>왜 세이브 파일(CharacterSaveStore)이 아닌가</b>: 그쪽에 필드를 하나 더하려면
        /// 스키마 버전을 올리고 마이그레이션을 붙여야 하는데, 2026-09-01 현재 그 파일은 다른 작업자가
        /// 다운그레이드 방어(J1)를 들여다보는 중이다. <b>안내를 한 번 봤다</b>는 사실 하나 때문에
        /// 세이브 스키마를 흔드는 것은 위험 대비 이득이 맞지 않는다. 저장되는 것은 부울 하나뿐이고
        /// 유실돼도 최악이 "안내가 한 번 더 뜬다"이다(사용자 데이터가 아니다).
        /// 리더가 옳다고 판단하면 훗날 세이브 파일로 옮긴다 — 교차 레이어 로그에 남겨 두었다.</para>
        /// </summary>
        private const string OnboardingSeenKey = "StickMate.GearMenu.OnboardingSeen.v1";

        /// <summary>세로 일렬 폴백 간격. 라벨이 사라져 "지름 + 라벨 간격 + 라벨 높이"라는 하한 계산식이
        /// 함께 사라졌으므로 <b>52pt 고정</b>이다(36-3-3).</summary>
        public const float ColumnFallbackSpacingPoints = 52f;
        public const float RotationSearchStepDegrees = 15f;
        public const float RotationSearchMaxDegrees = 90f;

        /// <summary>
        /// 부채꼴 <b>전체</b>를 이만큼까지는 평행이동해서라도 화면 안에 넣는다 — 세로 일렬로 무너지기 전에.
        ///
        /// 왜 필요한가(실측으로 드러난 기하 모순): 톱니의 기본 위치는 화면 오른쪽 끝에서 30pt다. 실측
        /// 화면(1512×982pt)에서 클램프 상자가 화면 안에 들어오려면 버튼 중심이 θ∈[153°, 256°]에 있어야
        /// 하는데, 그 창은 103°이고 <b>3개 배치의</b> 부채꼴은 120°가 필요했다 — 어떤 각도로도 회전만으로는
        /// 성립하지 않았다. 실제로 이 단계가 없을 때 기본 위치에서 곧장 세로 일렬 폴백으로 떨어지는 것을
        /// 스크린샷으로 확인했다(= 사용자가 보게 될 기본 화면이 폴백이 된다).
        ///
        /// ★ 2026-08-31(36-3) 이후 스팬이 90°로 줄어 이 단계가 실제로 걸리는 일은 거의 없어졌다
        /// (실측 격자 전수 계산: 평균 이동 0.7pt / 최대 9pt). <b>그래도 지운다는 선택은 하지 않는다</b> —
        /// 800×600 같은 좁은 화면에서는 여전히 39pt까지 필요하고, 이 사다리가 없으면 그 화면은 곧장
        /// 세로 일렬 폴백으로 떨어진다.
        ///
        /// 평행이동은 <b>형태를 완전히 보존</b>한다(네 버튼의 상대 위치가 그대로다). 32-1이 금지한 것은
        /// 버튼을 <b>따로따로</b> 밀어 호를 찌그러뜨리는 일이지 강체 이동이 아니다.
        /// </summary>
        public const float MaxGroupShiftPoints = 48f;

        /// <summary>네 버튼이 전부 안착하기까지(0.19 + 0.037×3 = 0.301초 — 32-2의 0.30초 예산).</summary>
        public static float ExpandTotalSeconds => ExpandSecondsPerButton + ExpandStaggerSeconds * (ButtonCount - 1);

        /// <summary>포스트잇(30000)·캐릭터 창(31000)보다 위, 팝오버(31700)보다 아래 —
        /// 부채꼴은 방금 사용자가 부른 것이라 다른 상시 패널에 가리면 안 되고, 자기가 낳은 팝오버를
        /// 가려서도 안 된다. (2026-08-31: 32760에 있던 우클릭 제어 메뉴는 폐지됐다 — 36-9.)</summary>
        private const int SortingOrder = 31500;
        private const float SymbolStroke = 2.0f;
        private const float SymbolBoxPoints = 24f;

        /// <summary>
        /// 네 진입점의 이름. 두 곳에서 쓴다: <b>호버 이름표</b>(커서가 올라간 하나만)와 <b>로그/접힘
        /// 사유</b>("[오늘 할일] 재클릭" 같은 진단 줄에서 인덱스 숫자만 남으면 로그를 읽을 수 없다).
        ///
        /// 두 용도가 같은 배열을 쓰는 이유: 로그와 화면이 다른 이름을 부르면 사용자 신고("행동 버튼이
        /// 안 눌려요")를 로그에서 찾을 수 없다. 이름은 한 곳에서만 정의한다.
        /// </summary>
        private static readonly string[] ButtonNames = { "집중 모드", "캐릭터", "오늘 할일", "행동" };

        /// <summary>버튼 이름(테스트/진단 전용) — 호버 이름표가 실제로 이 값을 쓰는지 대조한다.</summary>
        public static string NameOf(int index)
            => index >= 0 && index < ButtonNames.Length ? ButtonNames[index] : string.Empty;

        // ==================== 내부 상태 ====================

        private enum Phase { Hidden, Expanding, Open, Collapsing }

        private sealed class ButtonView
        {
            public RectTransform Root;       // 원 + 심볼(스케일 대상).
            public RectTransform Group;      // 원 + 라벨을 함께 옮기는 컨테이너.
            public Image Surface;
            public Image Border;
            public Image Flash;
            public RectTransform Symbol;
            public Image[] SymbolParts;        // 상태에 따라 색이 바뀌는 획(TextPrimary <-> Accent).
            public Image[] SymbolFixedParts;   // 색이 고정된 획(체크마크 = 완료를 뜻하는 Accent) — 알파만 따라간다.
            public Image RingTrack;          // 집중 모드 전용 — 세션 중에는 잔여 시간 호가 된다.
            public Image RingFill;
            public RectTransform Badge;      // 오늘 할일 전용 미완료 배지.
            public Image BadgeSurface;
            public Text BadgeText;

            public Vector2 CenterPoints;     // 최종 안착 위치(캔버스 포인트, 좌하단 원점).
            public float Progress;
            public float Hover;              // 0~1.
            public float FlashTimer = -1f;
            public bool CollapsingNow;
        }

        private StickmanAgent _agent;
        private StickConfig _config;
        private FocusWatchDirector _focusDirector;
        private FocusSessionPopover _focusPopover;
        private TodoBoardPopover _todoPopover;
        private ActionCommandPopover _actionPopover;

        private Canvas _canvas;
        private CanvasScaler _scaler;
        private RectTransform _root;
        private readonly ButtonView[] _buttons = new ButtonView[ButtonCount];

        private Phase _phase = Phase.Hidden;
        private GearMenuCollapseMode _collapseMode = GearMenuCollapseMode.User;
        private float _timer;
        private float _idleTimer;
        private int _hoverIndex = -1;
        private int _activeIndex = -1;       // 팝오버를 띄운 채 남아 있는 버튼(-1 = 없음).
        private float _diameterPoints = ButtonDiameterPoints;
        private float _baseAngleDegrees = 225f;
        private Vector2 _gearCenterPoints;
        private Vector2 _screenPointsAtLayout;
        // 호버 이름표 — 인스턴스 하나뿐이다(클래스 문서 ②).
        private RectTransform _hoverLabel;
        private Image _hoverLabelSurface;
        private Image _hoverLabelBorder;
        private Text _hoverLabelText;
        private float _hoverLabelAlpha;
        private int _hoverLabelIndex = -1;      // 지금 알약에 적혀 있는 이름의 버튼(-1 = 없음).
        private readonly float[] _nameWidths = new float[ButtonCount];

        // 온보딩 안내 알약 — 호버 이름표와 <b>다른 인스턴스</b>다(둘은 절대 동시에 보이지 않는다:
        // 호버가 시작되는 순간 이쪽이 물러난다. 아래 ApplyOnboardingHint 참고).
        private RectTransform _onboardingHint;
        private Image _onboardingHintSurface;
        private Image _onboardingHintBorder;
        private Text _onboardingHintText;
        private float _onboardingHintWidth;
        private float _onboardingHintAlpha;
        private float _onboardingHintTimer = -1f;   // 음수 = 안내 중이 아님.

        private float _clockTimer;
        private int _lastShownRemainingSeconds = -1;
        private int _lastShownBadgeCount = -1;

        // ==================== 공개 상태 ====================

        /// <summary>펼쳐져 있는가(펼치는 중 + 팝오버 앵커 상태 포함). 클릭을 받는 상태의 기준.</summary>
        public bool IsExpanded => _phase == Phase.Expanding || _phase == Phase.Open;

        /// <summary>그림이 화면에 남아 있는가(접히는 중 포함).</summary>
        public bool IsVisible => _phase != Phase.Hidden;

        /// <summary>팝오버를 띄운 채 남아 있는 버튼(-1 = 없음).</summary>
        public int AnchoredButton => _activeIndex;

        /// <summary>네 버튼의 <b>클램프 상자</b>를 모두 덮는 사각형(Unity 스크린 픽셀). 톱니가 클릭관통
        /// 차단 콜라이더를 이만큼 넓혀야 버튼 클릭이 밑의 앱으로 새지 않는다.</summary>
        public Rect UnionScreenRect { get; private set; }

        /// <summary>지금 부채꼴이 쓰는 기준각(도). 회귀 테스트가 45° 스냅을 직접 확인한다.</summary>
        public float BaseAngleDegrees => _baseAngleDegrees;

        /// <summary>호버 이름표에 지금 보이는 글자(안 보이면 빈 문자열) — "선택된 것만 이름이 보인다"를
        /// 회귀 테스트가 직접 확인한다.</summary>
        public string VisibleHoverLabel
            => _hoverLabel != null && _hoverLabelAlpha > 0.5f && _hoverLabelText != null
                ? _hoverLabelText.text : string.Empty;

        /// <summary>지금 온보딩 안내 알약에 보이는 글자(안 보이면 빈 문자열) — 회귀 테스트 창구.</summary>
        public string VisibleOnboardingHint
            => _onboardingHint != null && _onboardingHintAlpha > 0.5f && _onboardingHintText != null
                ? _onboardingHintText.text : string.Empty;

        /// <summary>이 컴퓨터에서 안내를 이미 본 적이 있는가(디스크 기록).</summary>
        public static bool OnboardingHintSeen => PlayerPrefs.GetInt(OnboardingSeenKey, 0) == 1;

        /// <summary>테스트 전용 — "처음 쓰는 사용자"로 되돌린다. 제품 경로에는 지우는 코드가 없다.</summary>
        public static void ResetOnboardingHintForTests()
        {
            PlayerPrefs.DeleteKey(OnboardingSeenKey);
            PlayerPrefs.Save();
        }

        /// <summary>테스트 전용 — "이미 본 사용자"로 만든다.</summary>
        public static void MarkOnboardingHintSeenForTests()
        {
            PlayerPrefs.SetInt(OnboardingSeenKey, 1);
            PlayerPrefs.Save();
        }

        /// <summary>호버 이름표의 화면 사각형(Unity 스크린 픽셀). 안 보이면 빈 사각형.</summary>
        public Rect HoverLabelScreenRect
        {
            get
            {
                if (_hoverLabel == null || _hoverLabelAlpha <= 0.5f) return new Rect();
                Vector2 size = _hoverLabel.sizeDelta * PixelsPerPoint;
                Vector2 center = PointsToScreen(_hoverLabel.anchoredPosition);
                return new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y);
            }
        }

        /// <summary>지금 버튼 지름(포인트) — 축소 폴백이 걸렸는지 알 수 있다.</summary>
        public float ButtonDiameter => _diameterPoints;

        public float ButtonProgress(int index)
            => index >= 0 && index < ButtonCount && _buttons[index] != null ? _buttons[index].Progress : 0f;

        /// <summary>버튼 중심(Unity 스크린 픽셀).</summary>
        public Vector2 ButtonScreenCenter(int index)
        {
            if (index < 0 || index >= ButtonCount || _buttons[index] == null) return Vector2.zero;
            return PointsToScreen(_buttons[index].CenterPoints);
        }

        /// <summary>버튼 원의 클릭 판정 사각형(Unity 스크린 픽셀) — 팝오버 앵커 계산에도 쓴다.</summary>
        public Rect ButtonScreenRect(int index)
        {
            Vector2 c = ButtonScreenCenter(index);
            float r = (_diameterPoints * 0.5f) * PixelsPerPoint;
            return new Rect(c.x - r, c.y - r, r * 2f, r * 2f);
        }

        /// <summary>네 버튼 <b>중심</b> 사이의 최소 거리(포인트) — 겹침 회귀 테스트용.
        /// 아직 <see cref="BuildUi"/> 전(Awake 이전)이면 <see cref="float.MaxValue"/>를 돌려준다:
        /// "가장 좁은 간격"의 항등원이라 어떤 최소값 단언도 통과시키지 않고 조용히 넘어가지 않는다.</summary>
        public float MinimumCenterSpacingPoints()
        {
            float min = float.MaxValue;
            for (int i = 0; i < ButtonCount; i++)
            {
                if (_buttons[i] == null) continue;
                for (int j = i + 1; j < ButtonCount; j++)
                {
                    if (_buttons[j] == null) continue;
                    float d = Vector2.Distance(_buttons[i].CenterPoints, _buttons[j].CenterPoints);
                    if (d < min) min = d;
                }
            }
            return min;
        }

        /// <summary>버튼의 클램프 상자(캔버스 포인트, 좌하단 원점) — <b>원 중심에 정렬된 정사각형</b>.
        /// 범위 밖/미생성이면 빈 사각형(<see cref="Rect.Contains"/>가 항상 false) — 형제 접근자
        /// <see cref="ButtonScreenCenter"/>/<see cref="ButtonProgress"/>와 같은 가드 규약이다.</summary>
        public Rect ClampBoxPoints(int index)
        {
            if (index < 0 || index >= ButtonCount || _buttons[index] == null) return new Rect();
            return BoxFor(_buttons[index].CenterPoints, _diameterPoints);
        }

        // ==================== 수명 주기 ====================

        private void Awake()
        {
            _agent = GetComponent<StickmanAgent>();
            _config = _agent != null ? _agent.Config : null;
            BuildUi();
        }

        private void Start()
        {
            _focusDirector = GetComponent<FocusWatchDirector>();
            _focusPopover = GetComponent<FocusSessionPopover>();
            _todoPopover = GetComponent<TodoBoardPopover>();
            _actionPopover = GetComponent<ActionCommandPopover>();
            Debug.Log("[부채꼴] 준비 완료 — 톱니를 짧게 클릭하면 [집중 모드]/[캐릭터]/[오늘 할일]/[행동] " +
                $"**아이콘 전용** 원버튼 {ButtonCount}개가 Ø{ButtonDiameterPoints:F0}pt, 궤도 " +
                $"{OrbitRadiusPoints:F0}pt, 간격 {ButtonAngleStepDegrees:F0}도(스팬 " +
                $"{ButtonAngleStepDegrees * (ButtonCount - 1):F0}도)로 {ExpandTotalSeconds:F2}초 동안 " +
                "촤르륵 펼쳐집니다. 상시 이름표(라벨)는 2026-08-31 사용자 지시로 전부 삭제됐고, " +
                "대신 **커서가 올라간 버튼 하나만** 그 이름이 원 <b>바깥쪽</b>에 뜹니다" +
                // ★ 예전에는 $"0.{v*100:F0}초"였다 — v=0.09에서 9를 찍어 <b>"0.9초"</b>가 됐다
                //   (실제의 10배, 페르소나 소은 #8). 값을 그대로 서식하면 그런 종류의 거짓말이 없다.
                $"({HoverLabelFadeSeconds:0.00}초 페이드, 부채꼴 바깥쪽으로 밀려 형제 버튼을 덮지 않습니다). " +
                "그 밖에 남는 글자는 [오늘 할일] 미완료 배지 하나뿐입니다(이름이 아니라 상태 수량). " +
                $"기준각은 (화면 중심 − 기어 중심)을 45도 단위로 스냅해 펼침 순간에 고정하고, 화면 밖이면 " +
                $"부채꼴 전체를 ±{RotationSearchStepDegrees:F0}도씩 최대 ±{RotationSearchMaxDegrees:F0}도까지 " +
                $"회전해 봅니다. {AutoCollapseIdleSeconds:F0}초 동안 커서가 부채꼴 밖이면 자동으로 접힙니다.");
        }

        private void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        // ==================== 열기 / 닫기 ====================

        /// <summary>
        /// 톱니 클릭 프레임(t=0)에 불린다 — 회전이 끝나기를 기다리지 않는다(32-9 (B)). 클릭 후
        /// 100ms 안에 아무 변화가 없으면 사용자는 "안 먹었다"고 판단해 한 번 더 누르고, 그 두 번째
        /// 클릭은 토글 접힘이 되어 메뉴가 깜빡인다 — 실패 모드가 구조적으로 존재하게 된다.
        /// </summary>
        public void Expand(Vector2 gearCenterUnityScreen)
        {
            if (IsExpanded) return;

            _gearCenterPoints = ScreenToPoints(gearCenterUnityScreen);
            ComputeLayout();

            _phase = Phase.Expanding;
            _timer = 0f;
            _idleTimer = 0f;
            _activeIndex = -1;
            _hoverIndex = -1;
            for (int i = 0; i < ButtonCount; i++)
            {
                _buttons[i].Progress = 0f;
                _buttons[i].Hover = 0f;
                _buttons[i].CollapsingNow = false;
            }
            if (_canvas != null) _canvas.gameObject.SetActive(true);
            TryStartOnboardingHint();
            RefreshDynamicContent(force: true);
            ApplyVisuals();
        }

        /// <summary>
        /// 처음 펼친 그 한 번만 안내를 띄운다. <b>본 사실은 뜨는 순간 기록</b>한다 — 안내를 0.2초 보고
        /// 부채꼴을 접은 사용자에게 내일 또 띄우면 그게 방해다(원칙 2). "끝까지 읽었는가"를 조건으로
        /// 삼으면 반드시 그 경로가 생긴다.
        /// </summary>
        private void TryStartOnboardingHint()
        {
            if (_onboardingHint == null || OnboardingHintSeen) return;
            PlayerPrefs.SetInt(OnboardingSeenKey, 1);
            PlayerPrefs.Save();
            _onboardingHintTimer = 0f;
            Debug.Log($"[부채꼴] 최초 1회 안내를 띄웁니다({OnboardingHintSeconds:F1}초) — \"{OnboardingHintText}\". " +
                "36-4가 상시 라벨을 지우면서 약속한 대가(35-2 온보딩)의 지급이고, 이 컴퓨터에서 다시는 뜨지 않습니다.");
        }

        public void Collapse(GearMenuCollapseMode mode, string reason)
        {
            if (_phase == Phase.Hidden || _phase == Phase.Collapsing) return;

            ClosePopovers(reason);
            _activeIndex = -1;
            _phase = Phase.Collapsing;
            _collapseMode = mode;
            _timer = 0f;
            _hoverIndex = -1;
            for (int i = 0; i < ButtonCount; i++) _buttons[i].CollapsingNow = true;
            Debug.Log($"[부채꼴] 접힘({ModeLabel(mode)}) — {reason}.");
        }

        private static string ModeLabel(GearMenuCollapseMode mode) => mode switch
        {
            GearMenuCollapseMode.Drag => "이동 시작",
            GearMenuCollapseMode.Auto => "무반응 자동",
            _ => "사용자 동작",
        };

        private static float CollapseSecondsFor(GearMenuCollapseMode mode) => mode switch
        {
            GearMenuCollapseMode.Drag => CollapseDragSeconds,
            GearMenuCollapseMode.Auto => CollapseAutoSeconds,
            _ => CollapseUserSeconds,
        };

        // ==================== 버튼 발동 ====================

        /// <summary>
        /// 눌린 버튼을 실행한다. <b>캐릭터</b>는 넷이 모두 접히고 창이 뜬다. <b>집중 모드/오늘 할일/행동</b>은
        /// 나머지 3개만 접히고 누른 버튼이 활성 스타일로 남아, 그 버튼에서 팝오버가 자라난다 —
        /// 팝오버에 꼬리를 그리지 않고도 "이 창은 저 버튼에서 나왔다"를 보여주는 가장 싼 방법이다(32-3).
        /// </summary>
        public void Activate(int index)
        {
            if (index < 0 || index >= ButtonCount) return;
            _buttons[index].FlashTimer = 0f;

            // 이미 그 버튼으로 팝오버가 떠 있으면 재클릭 = 완전 종료(32-3).
            if (_activeIndex == index)
            {
                Collapse(GearMenuCollapseMode.User, $"[{ButtonNames[index]}] 재클릭");
                return;
            }

            switch ((GearMenuButton)index)
            {
                case GearMenuButton.Character:
                    ActivateCharacter();
                    return;
                case GearMenuButton.FocusMode:
                    AnchorPopover(index, OpenFocusPopover());
                    return;
                case GearMenuButton.Action:
                    AnchorPopover(index, OpenActionPopover());
                    return;
                default:
                    AnchorPopover(index, OpenTodoPopover());
                    return;
            }
        }

        private void ActivateCharacter()
        {
            var window = GetComponent<CharacterInfoWindow>();
            if (window == null)
            {
                Debug.LogWarning("[부채꼴] [캐릭터] — CharacterInfoWindow가 없어 건너뜁니다.");
                return;
            }
            window.Toggle("부채꼴 메뉴 [캐릭터]");
            Collapse(GearMenuCollapseMode.User, "[캐릭터] 선택");
        }

        private bool OpenFocusPopover()
        {
            if (_focusPopover == null) _focusPopover = GetComponent<FocusSessionPopover>();
            if (_focusPopover == null)
            {
                Debug.LogWarning("[부채꼴] [집중 모드] — FocusSessionPopover가 없어 건너뜁니다.");
                return false;
            }
            _focusPopover.Open(ButtonScreenRect((int)GearMenuButton.FocusMode), "부채꼴 [집중 모드]");
            return true;
        }

        private bool OpenTodoPopover()
        {
            if (_todoPopover == null) _todoPopover = GetComponent<TodoBoardPopover>();
            if (_todoPopover == null)
            {
                Debug.LogWarning("[부채꼴] [오늘 할일] — TodoBoardPopover가 없어 건너뜁니다.");
                return false;
            }
            _todoPopover.Open(ButtonScreenRect((int)GearMenuButton.Todo), "부채꼴 [오늘 할일]");
            return true;
        }

        /// <summary>④ 행동 명령창(36-6). 씬에 컴포넌트가 없으면 <b>조용히 실패하지 않고</b> 경고를 남긴다 —
        /// 33-9/34-9에서 반복 재발한 "신규 컴포넌트가 프리팹에 없어 런타임 부재" 함정의 관례 방어다.</summary>
        private bool OpenActionPopover()
        {
            if (_actionPopover == null) _actionPopover = GetComponent<ActionCommandPopover>();
            if (_actionPopover == null)
            {
                Debug.LogWarning("[부채꼴] [행동] — ActionCommandPopover가 없어 건너뜁니다. " +
                    "Assets/Editor/SceneBootstrapper.cs가 이 컴포넌트를 붙이는지 확인하세요.");
                return false;
            }
            _actionPopover.Open(ButtonScreenRect((int)GearMenuButton.Action), "부채꼴 [행동]");
            return true;
        }

        /// <summary>누른 버튼만 남기고 나머지를 접는다.</summary>
        private void AnchorPopover(int index, bool opened)
        {
            if (!opened)
            {
                Collapse(GearMenuCollapseMode.User, $"[{ButtonNames[index]}] 열기 실패");
                return;
            }

            _activeIndex = index;
            _phase = Phase.Open;
            _timer = ExpandTotalSeconds;
            for (int i = 0; i < ButtonCount; i++)
            {
                if (i == index) { _buttons[i].CollapsingNow = false; _buttons[i].Progress = 1f; continue; }
                _buttons[i].CollapsingNow = true;
            }
            _collapseMode = GearMenuCollapseMode.User;
            Debug.Log($"[부채꼴] [{ButtonNames[index]}] 선택 — 나머지 {ButtonCount - 1}개는 접히고 이 버튼만 " +
                "활성 스타일로 남습니다.");
        }

        private void ClosePopovers(string reason)
        {
            if (_focusPopover != null) _focusPopover.Close(reason);
            if (_todoPopover != null) _todoPopover.Close(reason);
            if (_actionPopover != null) _actionPopover.Close(reason);
        }

        /// <summary>
        /// ★ 2026-08-30 — 부채꼴과 팝오버 2종을 <b>단계에 상관없이</b> 전부 거둔다. 캐릭터 창(배타 모달)이
        /// 열릴 때 쓰는 <b>단일 창구</b>다. <see cref="Collapse"/>는 이미 접힌 상태면 즉시 돌아가므로,
        /// 팝오버 정리를 그쪽에만 맡기면 "메뉴는 접혔는데 팝오버만 남은" 조합이 그대로 샌다.
        /// 팝오버 참조는 지연 해석이라(열어 본 적 없으면 null) 여기서 한 번 확인해 준다.
        /// </summary>
        // ★ 배타 표면 등록(2026-09-01). "보이는가"의 기준은 IsVisible이다 — 접히는 중(Collapsing)도
        //   아직 화면에 있으므로 열린 것으로 센다. 닫기는 이미 있는 단일 창구를 그대로 쓴다.
        bool IExclusiveSurface.IsSurfaceOpen => IsVisible;
        void IExclusiveSurface.CloseSurface(string reason) => ForceCloseAll(reason);

        public void ForceCloseAll(string reason)
        {
            if (_focusPopover == null) _focusPopover = GetComponent<FocusSessionPopover>();
            if (_todoPopover == null) _todoPopover = GetComponent<TodoBoardPopover>();
            if (_actionPopover == null) _actionPopover = GetComponent<ActionCommandPopover>();
            ClosePopovers(reason);
            Collapse(GearMenuCollapseMode.User, reason);
        }

        private bool AnyPopoverOpen()
            => (_focusPopover != null && _focusPopover.IsOpen)
               || (_todoPopover != null && _todoPopover.IsOpen)
               || (_actionPopover != null && _actionPopover.IsOpen);

        // ==================== 히트 테스트 ====================

        /// <summary>커서 아래 버튼(없으면 -1). <b>원</b> 판정이다 — 동그란 버튼을 사각형으로 재면
        /// 모서리에서 "안 눌리는 자리를 눌렀는데 눌린다".</summary>
        public int HitTest(Vector2 cursorUnityScreen)
        {
            if (!IsExpanded) return -1;
            Vector2 p = ScreenToPoints(cursorUnityScreen);
            float r = _diameterPoints * 0.5f + HitPaddingPoints;
            float rSqr = r * r;
            for (int i = 0; i < ButtonCount; i++)
            {
                ButtonView b = _buttons[i];
                if (b.Progress < MinClickableProgress || b.CollapsingNow) continue;
                if ((p - b.CenterPoints).sqrMagnitude <= rSqr) return i;
            }
            return -1;
        }

        /// <summary>커서가 부채꼴 영역(클램프 상자 합집합) 안인가 — 자동 접힘 타이머의 기준.</summary>
        public bool ContainsCursor(Vector2 cursorUnityScreen)
        {
            if (!IsVisible) return false;
            Vector2 p = ScreenToPoints(cursorUnityScreen);
            for (int i = 0; i < ButtonCount; i++)
            {
                if (_buttons[i].CollapsingNow) continue;
                if (ClampBoxPoints(i).Contains(p)) return true;
            }
            return false;
        }

        public void SetHover(int index)
        {
            // 아직 안 보이는 버튼이 호버 강조를 먹으면 클릭 판정과 같은 종류의 거짓말이 된다(32-9 (A)).
            if (index >= 0 && (index >= ButtonCount || _buttons[index] == null ||
                _buttons[index].Progress < MinClickableProgress || _buttons[index].CollapsingNow))
                index = -1;
            _hoverIndex = index;
        }

        /// <summary>커서가 부채꼴 안에 있다는 신호 — 6초 자동 접힘 타이머를 되돌린다.</summary>
        public void KeepAlive() => _idleTimer = 0f;

        // ==================== 매 프레임 ====================

        private void LateUpdate()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.UiWindows);   // [스톨구간] 계측
            // ★★ 절대 불변 원칙 2(비침해) — 전체화면 게임이 감지되면 상시 표면을 <b>즉시</b> 거둔다.
            // StickmanAgent.Suspend()는 Awake에서 캐시한 캐릭터 렌더러만 끄므로, 씬 루트에 사는 이
            // 캔버스는 그 배열에 없어 그대로 남았다. 히트테스트는 <b>커서 아래 Collider2D</b>를 본다
            // (hitTestType=Raycast — InfoGearIconWidget.cs:51 참고. 픽셀 알파를 보는 구조가 <b>아니다</b>).
            // 남아 있으면 보이기만 하는 게 아니라 전체화면 게임의
            // 클릭까지 먹는다. 애니메이션(0.13초 접힘)이 아니라 Hide()로 한 프레임에 치우는 이유:
            // 접힘 연출은 "사용자가 닫았다"는 뜻인데 여기는 사용자 동작이 아니고, 그 0.13초 동안에도
            // 클릭을 먹기 때문이다. 복귀는 강제로 다시 열지 않는다 — 톱니만 돌아오고 메뉴는 사용자가
            // 다시 부른다(WindowCrashDirector가 오버레이를 되살리지 않는 것과 같은 판단).
            // (이미 접혀 있으면 캔버스도 꺼져 있어 거둘 것이 없다 — 평소의 비용 0을 유지한다.)
            if (_phase == Phase.Hidden) return;

            // ★★★ 2026-09-02 — <c>ArePanelsSuppressed</c>(등급 1 포함). 부채꼴은 <b>등급 1</b>이다:
            //    게임이 아닌 전체화면 앱 위에서도 걷는다. 반대로 <b>톱니는 등급 2</b>로 남겨 뒀다
            //    (InfoGearIconWidget) — 등급 1의 안전판이 "복구는 톱니 1클릭"인데 톱니까지 걷으면
            //    그 안전판이 자기 자신을 지운다.
            if (_agent != null && _agent.ArePanelsSuppressed)
            {
                ClosePopovers("전체화면 감지 — 자동 숨김");
                Hide();
                Debug.Log("[부채꼴] 전체화면 감지 — 부채꼴과 팝오버를 즉시 거둡니다(비침해 원칙 2).");
                return;
            }

            // ★ 부채꼴이 떠 있다 = 사용자가 지금 이것을 겨누고 있다는 관측된 사실이다.
            // 적응형 페이싱은 그 사실을 모른 채 "캐릭터 Idle + 무입력 2초"만 보고 Calm으로 내려가고,
            // Windows에서 Calm은 게임 루프 자체를 30Hz로 만든다(CharacterInfoWindow.Update()의 같은
            // 자리 주석에 인과 전체가 있다). 홀드는 만료 시각 방식이라 이 위젯이 어떤 경로로 죽어도
            // 0.5초 뒤 저절로 풀린다 — 해제 책임이 없다. 위 Hidden 가드 뒤에 있으므로 접혀 있는
            // 평소에는 호출조차 되지 않는다(평소 비용 0 유지).
            // 팝오버(집중/할일)가 떠 있는 동안에는 TickAutoCollapse가 접힘을 멈춰 _phase가 Open으로
            // 남으므로, 그 팝오버들의 상호작용도 이 한 줄이 함께 덮는다.
            FramePacing.HoldActiveForInteraction();

            float dt = Time.unscaledDeltaTime;
            _timer += dt;

            switch (_phase)
            {
                case Phase.Expanding:
                    if (_timer >= ExpandTotalSeconds) _phase = Phase.Open;
                    break;
                case Phase.Collapsing:
                    if (_timer >= CollapseSecondsFor(_collapseMode)) { Hide(); return; }
                    break;
            }

            TickAutoCollapse(dt);
            TickAnchoredPopover();
            RefreshDynamicContent(force: false);
            ApplyVisuals();
        }

        /// <summary>
        /// 6초 무반응 자동 접힘. 팝오버가 떠 있는 동안에는 돌지 않는다 — 사용자가 읽고 있는
        /// 창을 시간으로 닫아버리면 그건 편의가 아니라 사고다.
        ///
        /// <para>★ 2026-09-03 — 멈춤 조건이 <b>3개에서 4개</b>가 됐다. 네 번째는 <b>최초 1회 안내가
        /// 떠 있는 동안</b>이다(ux-widgets R3-4-4 【C-2】 채택안). 그 전까지 이 시계는 이렇게 돌았다:
        /// <see cref="Expand"/>가 <c>_idleTimer = 0f</c>와 <see cref="TryStartOnboardingHint"/>를
        /// <b>같은 함수 안에서 연달아</b> 부르므로 두 시계가 같은 순간에 출발했고, 그 사이에 시계를
        /// 되돌리는 줄이 하나도 없었다. 그런데 톱니를 막 누른 사람의 커서는 <b>톱니 위</b>에 있고
        /// 톱니는 어떤 클램프 상자에도 들어가지 않으므로(<see cref="ContainsCursor"/>)
        /// <see cref="KeepAlive"/>가 한 번도 불리지 않는다 — <b>앱이
        /// <see cref="OnboardingHintText"/>라고 말해 놓고, 그 문장을 읽는 동안 카운트다운을
        /// 멈추지 않았다.</b></para>
        ///
        /// <para>★ 그리고 실패의 대가가 영구적이다: "봤다"는
        /// <see cref="TryStartOnboardingHint"/>가 안내가 <b>뜨는 순간</b> 디스크에 적으므로, 그 창을
        /// 놓치면 이 컴퓨터에서 다시는 뜨지 않는다. <b>그 기록 시점은 일부러 건드리지 않았다</b> —
        /// "끝까지 읽었는가"를 조건으로 삼으면 0.2초 보고 접은 사용자에게 내일 또 띄우는 경로가
        /// 반드시 생기고 그건 방해다(원칙 2). <b>고칠 곳은 기록의 의미가 아니라 창의 길이다.</b></para>
        ///
        /// <para>예산 검산(상수는 전부 이 파일에서 읽은 값이다):
        /// 최초 1회 = <see cref="OnboardingHintSeconds"/>(4.5) + <see cref="AutoCollapseIdleSeconds"/>(6.0)
        /// = <b>10.5초</b>. 커서가 클램프 상자에 <b>한 번도 안 들어간다</b>고 최악으로 가정한 소요는
        /// 안내 읽기 1.78(<c>DialogueBudget.ReadingSeconds</c> 20자) + 톱니→첫 버튼 0.38 +
        /// 버튼 4개(<see cref="HoverLabelFadeSeconds"/> + 이름 읽기) 2.91 + 이웃 이동 3회 0.96
        /// = <b>6.03초</b> → 여유 <b>1.74배</b>. 고치기 전 예산은
        /// <see cref="ExpandTotalSeconds"/>(0.301) + 6.0 = <b>6.30초</b>여서 여유가 <b>1.04배</b>였다
        /// (= 0.27초만 머뭇거려도 안내가 영구히 사라졌다).
        /// <b>2회차부터는 안내가 안 뜨므로 아래 한 줄이 아예 안 걸리고 비용은 0이다.</b></para>
        ///
        /// <para>★ <b>멈추는 것이지 끄는 것이 아니다.</b> 안내가 스스로 물러나는 세 경로
        /// (4.5초 만료 · 호버 시작 · 접히는 중 — <see cref="ApplyOnboardingHint"/>)가 전부
        /// <c>_onboardingHintTimer</c>를 음수로 되돌리고, 그 밖의 종료 경로(<see cref="Hide"/>)도
        /// 같은 값을 되돌린다. <b>영구히 멈추는 경로는 없다.</b></para>
        /// </summary>
        private void TickAutoCollapse(float dt)
        {
            if (_phase != Phase.Open || _activeIndex >= 0 || AnyPopoverOpen()) { _idleTimer = 0f; return; }

            // ★ 4번째 멈춤 조건 — 최초 1회 안내가 떠 있는 동안(위 문단). 음수 = 안내 중이 아님.
            //   이 판정은 같은 프레임의 ApplyOnboardingHint보다 <b>앞</b>에서 돌므로 직전 프레임 값을
            //   본다 = 해제가 최대 한 프레임 늦다. 늦는 방향이 안전한 쪽이라 그대로 둔다.
            if (_onboardingHintTimer >= 0f) { _idleTimer = 0f; return; }

            _idleTimer += dt;
            if (_idleTimer < AutoCollapseIdleSeconds) return;
            Collapse(GearMenuCollapseMode.Auto, $"{AutoCollapseIdleSeconds:F0}초 동안 커서가 부채꼴 밖");
        }

        /// <summary>팝오버가 스스로 닫혔으면 남아 있던 버튼도 함께 거둔다.
        /// <para>★ 2026-09-02 — "스스로 닫힌다"의 경로가 <b>[✕] / 버튼 재클릭 / 무입력 180초</b>로
        /// 줄었다. <b>바깥 클릭은 더 이상 팝오버를 닫지 않는다</b>(사용자 지시 — UiChrome "창을 닫는 법").
        /// 즉 팝오버가 떠 있는 동안에는 이 부채꼴도 함께 남는다.</para></summary>
        private void TickAnchoredPopover()
        {
            if (_activeIndex < 0) return;
            if (AnyPopoverOpen()) { _idleTimer = 0f; return; }
            Collapse(GearMenuCollapseMode.User, "팝오버가 닫힘");
        }

        private void Hide()
        {
            _phase = Phase.Hidden;
            _activeIndex = -1;
            _hoverIndex = -1;
            _hoverLabelAlpha = 0f;
            _hoverLabelIndex = -1;
            _onboardingHintTimer = -1f;
            _onboardingHintAlpha = 0f;
            if (_onboardingHint != null) _onboardingHint.gameObject.SetActive(false);
            if (_hoverLabel != null) _hoverLabel.gameObject.SetActive(false);
            for (int i = 0; i < ButtonCount; i++) _buttons[i].Progress = 0f;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            UnionScreenRect = new Rect(PointsToScreen(_gearCenterPoints), Vector2.zero);
        }

        // ==================== 애니메이션 / 그리기 ====================

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        private static float EaseInQuad(float t) => t * t;
        private static float EaseInOutSine(float t) => 0.5f - 0.5f * Mathf.Cos(Mathf.PI * Mathf.Clamp01(t));
        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

        private void ApplyVisuals()
        {
            ApplyCanvasScaleFactor();

            float collapseSeconds = CollapseSecondsFor(_collapseMode);
            float collapseK = Mathf.Clamp01(_timer / Mathf.Max(0.001f, collapseSeconds));
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            bool anyVisible = false;

            for (int i = 0; i < ButtonCount; i++)
            {
                ButtonView b = _buttons[i];
                float radiusFactor = 1f;
                float scale = 1f;
                float alpha;

                if (b.CollapsingNow)
                {
                    if (_collapseMode == GearMenuCollapseMode.Auto)
                    {
                        // 제자리에서 스르르 — 반지름/스케일을 건드리지 않는다(사용자 접힘과 움직임이 달라야 한다).
                        float e = EaseInOutSine(collapseK);
                        alpha = 1f - e;
                    }
                    else
                    {
                        float e = EaseInQuad(collapseK);
                        radiusFactor = 1f - 0.5f * e;
                        scale = 1f - 0.28f * e;
                        alpha = 1f - e;
                    }
                    b.Progress = alpha;
                }
                else if (_phase == Phase.Expanding)
                {
                    float start = i * ExpandStaggerSeconds;
                    float p = Mathf.Clamp01((_timer - start) / ExpandSecondsPerButton);
                    float eased = EaseOutBack(p);
                    radiusFactor = StartRadiusFraction + (1f - StartRadiusFraction) * eased;
                    scale = StartScale + (1f - StartScale) * eased;
                    alpha = Mathf.Clamp01((_timer - start) / AlphaFadeInSeconds);
                    b.Progress = p;
                }
                else
                {
                    alpha = 1f;
                    b.Progress = 1f;
                }

                // 호버는 진행도와 무관한 별도 보간(0.09초) — 스케일에 곱해진다.
                float hoverTarget = i == _hoverIndex ? 1f : 0f;
                b.Hover = Mathf.MoveTowards(b.Hover, hoverTarget, Time.unscaledDeltaTime / HoverSeconds);
                scale *= Mathf.Lerp(1f, HoverScale, EaseOutQuad(b.Hover));

                Vector2 center = _gearCenterPoints + (b.CenterPoints - _gearCenterPoints) * radiusFactor;
                b.Group.anchoredPosition = center;
                b.Root.localScale = new Vector3(scale, scale, 1f);

                ApplyButtonStyle(b, i, alpha);

                if (alpha <= 0.001f) continue;
                anyVisible = true;
                Rect box = BoxFor(center, _diameterPoints);
                if (box.xMin < minX) minX = box.xMin;
                if (box.yMin < minY) minY = box.yMin;
                if (box.xMax > maxX) maxX = box.xMax;
                if (box.yMax > maxY) maxY = box.yMax;
            }

            // ★ 호버 이름표는 이 합집합에 <b>들어가지 않는다</b>(클래스 문서 ③) — 누를 수 있는 물건이
            //   아니라서, 포함시키면 클릭관통 차단 영역만 넓어져 비침해가 나빠진다(원칙 2).
            UnionScreenRect = anyVisible
                ? new Rect(PointsToScreen(new Vector2(minX, minY)),
                    new Vector2((maxX - minX) * PixelsPerPoint, (maxY - minY) * PixelsPerPoint))
                : new Rect(PointsToScreen(_gearCenterPoints), Vector2.zero);

            ApplyHoverLabel();
            ApplyOnboardingHint();
        }

        /// <summary>
        /// "마우스로 선택되고 있는 메뉴만 이름이 보인다"(2026-08-31 사용자 지시)를 실행한다.
        ///
        /// 이름표는 <b>커서가 올라간 버튼 하나</b>를 따라다니고, 커서가 벗어나면 같은 시간에 걸쳐
        /// 사라진다. 활성(팝오버를 띄운 채 남은) 버튼에는 이름을 띄우지 않는다 — 그 버튼의 창이 이미
        /// 화면에 제목을 달고 떠 있어서 같은 이름이 두 번 보이기 때문이다.
        /// </summary>
        private void ApplyHoverLabel()
        {
            if (_hoverLabel == null) return;

            int target = _hoverIndex;
            if (target >= 0 && (target == _activeIndex || _buttons[target] == null
                || _buttons[target].CollapsingNow || _buttons[target].Progress < MinClickableProgress))
            {
                target = -1;
            }

            float dt = Time.unscaledDeltaTime;
            _hoverLabelAlpha = Mathf.MoveTowards(_hoverLabelAlpha, target >= 0 ? 1f : 0f,
                dt / Mathf.Max(0.0001f, HoverLabelFadeSeconds));

            // 글자는 <b>값이 바뀐 프레임에만</b> 쓴다(Text.text 대입은 메시 재생성이다).
            if (target >= 0 && target != _hoverLabelIndex)
            {
                _hoverLabelIndex = target;
                _hoverLabelText.text = ButtonNames[target];
                _hoverLabel.sizeDelta = new Vector2(_nameWidths[target], HoverLabelHeightPoints);
            }

            if (_hoverLabelAlpha <= 0.001f)
            {
                if (_hoverLabel.gameObject.activeSelf) _hoverLabel.gameObject.SetActive(false);
                _hoverLabelIndex = -1;
                return;
            }
            if (!_hoverLabel.gameObject.activeSelf) _hoverLabel.gameObject.SetActive(true);

            // 사라지는 중이면 마지막으로 보이던 버튼을 그대로 따라간다(글자가 순간이동하지 않게).
            int anchorIndex = target >= 0 ? target : _hoverLabelIndex;
            if (anchorIndex >= 0 && _buttons[anchorIndex] != null)
            {
                _hoverLabel.anchoredPosition = ResolveHoverLabelCenter(
                    _buttons[anchorIndex].CenterPoints, _hoverLabel.sizeDelta.x);
            }
            SetHoverLabelAlpha(_hoverLabelAlpha);
        }

        /// <summary>
        /// 최초 1회 안내 알약 — 부채꼴 <b>가운데</b> 아래에 뜬다(특정 버튼의 이름표가 아니므로 어느
        /// 한 버튼에 붙이면 그 버튼 설명으로 오독된다).
        ///
        /// <para>호버가 시작되면 <b>즉시</b> 물러난다: 안내의 목적이 "커서를 올려 보라"인데, 올린
        /// 순간에도 안내가 남아 있으면 이름표와 알약 두 개가 동시에 뜬다(클래스 문서 ② "화면에는
        /// 언제나 이름이 최대 하나"의 정신).</para>
        ///
        /// <para>이름표와 같은 제약을 그대로 진다: 배치 계산에 참여하지 않고
        /// (<see cref="ComputeLayout"/>은 이 알약을 모른다), <see cref="UnionScreenRect"/>에도
        /// 들어가지 않는다 — 누를 수 없는 글자가 클릭관통 차단 영역을 넓히면 원칙 2 위반이다.</para>
        /// </summary>
        private void ApplyOnboardingHint()
        {
            if (_onboardingHint == null) return;

            if (_onboardingHintTimer >= 0f)
            {
                _onboardingHintTimer += Time.unscaledDeltaTime;
                if (_hoverIndex >= 0 || _phase == Phase.Collapsing
                    || _onboardingHintTimer >= OnboardingHintSeconds)
                {
                    _onboardingHintTimer = -1f;
                }
            }

            float dt = Time.unscaledDeltaTime;
            _onboardingHintAlpha = Mathf.MoveTowards(_onboardingHintAlpha, _onboardingHintTimer >= 0f ? 1f : 0f,
                dt / Mathf.Max(0.0001f, HoverLabelFadeSeconds));

            if (_onboardingHintAlpha <= 0.001f)
            {
                if (_onboardingHint.gameObject.activeSelf) _onboardingHint.gameObject.SetActive(false);
                return;
            }
            if (!_onboardingHint.gameObject.activeSelf) _onboardingHint.gameObject.SetActive(true);

            // 부채꼴의 <b>이등분 방향</b>으로, 가장 바깥 버튼만큼 나간 자리를 기준점으로 삼는다.
            // 그래야 이름표와 같은 기하 보장(ResolveHoverLabelCenter 문단)이 안내 알약에도 그대로
            // 적용된다 — 네 버튼 중심의 평균을 그대로 쓰면 기준점이 호 <b>안쪽</b>이라 보장이 약해진다.
            Vector2 middle = Vector2.zero;
            for (int i = 0; i < ButtonCount; i++) middle += _buttons[i].CenterPoints;
            middle = middle / ButtonCount - _gearCenterPoints;
            if (middle.sqrMagnitude < 1e-4f) middle = Vector2.down;
            middle.Normalize();

            float reach = 0f;
            for (int i = 0; i < ButtonCount; i++)
                reach = Mathf.Max(reach, Vector2.Dot(_buttons[i].CenterPoints - _gearCenterPoints, middle));

            _onboardingHint.anchoredPosition =
                ResolveHoverLabelCenter(_gearCenterPoints + middle * reach, _onboardingHintWidth);
            _onboardingHintSurface.color = Fade(UiChrome.PanelSurface, _onboardingHintAlpha);
            _onboardingHintBorder.color = Fade(UiChrome.AccentBorder, _onboardingHintAlpha);
            _onboardingHintText.color = Fade(UiChrome.TextPrimary, _onboardingHintAlpha);
        }

        /// <summary>
        /// ★ 2026-09-01(페르소나 소은 #1) — 이름표를 원 <b>아래</b>가 아니라 부채꼴 <b>바깥쪽
        /// (반지름 방향)</b>으로 민다.
        ///
        /// <para><b>무엇이 깨져 있었나</b>: 알약은 버튼 중심에서 아래로 22+8+9=<b>39pt</b>에 놓였는데
        /// 이웃 버튼 중심은 2·111·sin15° = <b>57.5pt</b>밖에 안 떨어져 있다. 그래서 [오늘 할일] 알약
        /// (폭 55.5pt)이 [캐릭터] 원을 가로로 8.5pt 파고들었다 — 알약 바탕이 불투명(α=1)이라 덮인
        /// 부분은 통째로 사라지고, 그 자리는 <b>보이지 않는데 클릭은 먹는</b> 영역이 됐다(4개 중 3개).</para>
        ///
        /// <para><b>왜 반지름 방향이면 구조적으로 안 겹치는가</b>: 알약의 <b>안쪽 모서리</b>를 기어
        /// 중심에서 (궤도반지름 + 버튼반지름 + 간격) 밖에 두면, 알약의 모든 점이 그 평면 바깥에 있다
        /// (축 정렬 사각형의 지지 함수 = |d.x|·반폭 + |d.y|·반높이). 반면 이웃 버튼의 그 방향 최대
        /// 투영은 111·cos30° + 22 = 118pt로 <b>141pt에 못 미친다</b>. 즉 <b>알약 폭이 아무리 넓어져도</b>
        /// 형제를 물 수 없다 — 폭에 의존하는 임시방편이 아니라 기하로 닫힌 보장이다.</para>
        ///
        /// <para>화면 밖으로 나가면 <b>클램프만</b> 하고 반대쪽으로 뒤집지 않는다. 뒤집으면 부채꼴
        /// 안쪽으로 들어가 정확히 위 문제가 되살아나기 때문이다(예전 "아래가 안 되면 위로" 규칙이
        /// 그 자리였다). 클램프가 실제로 걸리는 구성에서는 위 보장이 그만큼 약해지지만, 화면 밖으로
        /// 나간 이름표는 아예 읽을 수 없으므로 그쪽이 먼저다.</para>
        ///
        /// <para>★ 여전히 <b>이름표만</b> 움직인다 — 버튼 위치는 건드리지 않는다. 이름표가 배치에
        /// 개입하는 순간 36-3의 기하 근거(56×56 정사각 상자 전수 계산)가 무너진다.</para>
        /// </summary>
        private Vector2 ResolveHoverLabelCenter(Vector2 anchorCenter, float pillWidth)
        {
            float halfW = pillWidth * 0.5f;
            float halfH = HoverLabelHeightPoints * 0.5f;

            Vector2 outward = anchorCenter - _gearCenterPoints;
            if (outward.sqrMagnitude < 1e-4f) outward = Vector2.down;
            outward.Normalize();

            float reach = Mathf.Abs(outward.x) * halfW + Mathf.Abs(outward.y) * halfH;
            Vector2 center = anchorCenter + outward * (_diameterPoints * 0.5f + HoverLabelGapPoints + reach);

            float minX = EffectiveLeftMarginPoints + halfW;
            float maxX = _screenPointsAtLayout.x - EffectiveRightMarginPoints - halfW;
            if (maxX >= minX) center.x = Mathf.Clamp(center.x, minX, maxX);

            float minY = ScreenMarginPoints + halfH;
            float maxY = _screenPointsAtLayout.y - EffectiveTopMarginPoints - halfH;
            if (maxY >= minY) center.y = Mathf.Clamp(center.y, minY, maxY);

            return center;
        }

        private void SetHoverLabelAlpha(float alpha)
        {
            _hoverLabelSurface.color = Fade(UiChrome.PanelSurface, alpha);
            _hoverLabelBorder.color = Fade(UiChrome.PanelBorder, alpha);
            _hoverLabelText.color = Fade(UiChrome.TextPrimary, alpha);
        }

        private void ApplyButtonStyle(ButtonView b, int index, float alpha)
        {
            bool active = index == _activeIndex;
            float hover = EaseOutQuad(b.Hover);

            Color surface = Color.Lerp(UiChrome.CardSurface, UiChrome.AccentSurface, active ? 1f : hover);
            Color border = Color.Lerp(UiChrome.CardBorder, UiChrome.AccentBorder, active ? 1f : hover);
            Color symbol = Color.Lerp(UiChrome.TextPrimary, UiChrome.Accent, active ? 1f : hover);

            b.Surface.color = Fade(surface, alpha);
            b.Border.color = Fade(border, alpha);
            for (int i = 0; i < b.SymbolParts.Length; i++)
            {
                Image part = b.SymbolParts[i];
                if (part == null) continue;
                part.color = Fade(symbol, alpha);
            }
            if (b.SymbolFixedParts != null)
            {
                for (int i = 0; i < b.SymbolFixedParts.Length; i++)
                {
                    Image part = b.SymbolFixedParts[i];
                    if (part == null) continue;
                    part.color = Fade(UiChrome.Accent, alpha);
                }
            }

            if (b.FlashTimer >= 0f)
            {
                b.FlashTimer += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Clamp01(b.FlashTimer / PressFlashSeconds);
                b.Flash.color = new Color(UiChrome.Accent.r, UiChrome.Accent.g, UiChrome.Accent.b, 0.35f * k * alpha);
                if (b.FlashTimer >= PressFlashSeconds) b.FlashTimer = -1f;
            }
            else if (b.Flash.color.a > 0f)
            {
                b.Flash.color = new Color(0f, 0f, 0f, 0f);
            }

            if (b.Badge != null && b.Badge.gameObject.activeSelf)
            {
                b.BadgeSurface.color = Fade(UiChrome.Accent, alpha);
                b.BadgeText.color = Fade(UiChrome.OnAccentSolid, alpha);
            }
            if (b.RingFill != null && b.RingFill.gameObject.activeSelf)
            {
                b.RingFill.color = Fade(UiChrome.WarmAccent, alpha);
                b.RingTrack.color = Fade(UiChrome.TrackBackground, alpha);
            }
        }

        private static Color Fade(Color c, float alpha) => new Color(c.r, c.g, c.b, c.a * Mathf.Clamp01(alpha));

        /// <summary>
        /// 배지/잔여 시간 호는 <b>실제 값에서만</b> 파생한다(원칙 1의 UI판).
        ///
        /// ★ 36-4로 라벨이 사라지면서 "집중 · 12:30" 표기도 함께 사라졌다. 이제 <b>스톱워치 링의 잔여
        /// 호(fillAmount)가 유일한 표시</b>이며, 정확한 숫자는 팝오버를 열면 나온다. 원칙 1은 그대로
        /// 지켜진다 — 호는 여전히 <see cref="FocusWatchDirector"/>의 실제 값에서만 파생된다.
        /// </summary>
        private void RefreshDynamicContent(bool force)
        {
            _clockTimer += Time.unscaledDeltaTime;
            bool tick = force || _clockTimer >= 1f;
            if (tick) _clockTimer = 0f;

            // ---- 오늘 할일 배지 ----
            ButtonView todo = _buttons[(int)GearMenuButton.Todo];
            int uncompleted = TodoListModel.UncompletedCount;
            bool showBadge = uncompleted > 0;
            if (todo.Badge.gameObject.activeSelf != showBadge) todo.Badge.gameObject.SetActive(showBadge);
            if (showBadge && (tick || _lastShownBadgeCount != uncompleted))
            {
                _lastShownBadgeCount = uncompleted;
                todo.BadgeText.text = uncompleted >= 10 ? "9+" : uncompleted.ToString();
            }

            // ---- 집중 모드 잔여 시간 ----
            if (!tick) return;
            if (_focusDirector == null) _focusDirector = GetComponent<FocusWatchDirector>();

            ButtonView focus = _buttons[(int)GearMenuButton.FocusMode];
            bool running = _focusDirector != null && _focusDirector.IsSessionActive
                && _focusDirector.SessionDurationSeconds > 0f;

            if (focus.RingFill.gameObject.activeSelf != running) focus.RingFill.gameObject.SetActive(running);
            if (!running)
            {
                _lastShownRemainingSeconds = -1;
                return;
            }

            // 초 단위로 값이 바뀐 프레임에만 쓴다 — 하루 종일 켜져 있는 앱이다.
            int remaining = Mathf.Max(0, Mathf.CeilToInt(_focusDirector.RemainingSeconds));
            if (remaining == _lastShownRemainingSeconds) return;
            _lastShownRemainingSeconds = remaining;

            focus.RingFill.fillAmount = Mathf.Clamp01(_focusDirector.RemainingSeconds / _focusDirector.SessionDurationSeconds);
        }

        // ==================== 배치 계산 ====================

        /// <summary>(화면 중심 − 기어 중심)의 실제 각도를 45° 단위로 스냅한다 — 사분면 부호로 4방향만
        /// 쓰면 위쪽 한가운데에서 아래로 곧게 못 펼치고 중앙선에서 방향이 90° 튄다(32-9 (C)).</summary>
        public static float Snap45(Vector2 direction)
        {
            if (direction.sqrMagnitude < 1e-6f) return 225f;
            float degrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float snapped = Mathf.Round(degrees / 45f) * 45f;
            return Mathf.Repeat(snapped, 360f);
        }

        /// <summary>
        /// 화면 밖 방지 사다리(32-1 + 실측 보강):
        ///  ① θ₀ 그대로 → ② θ₀ ±15°씩 최대 ±90° 회전 → ③ <b>부채꼴 전체 평행이동</b>(형태 보존,
        ///  <see cref="MaxGroupShiftPoints"/>까지) → ④ 지름 축소(44→36) 후 ①~③ 반복 →
        ///  ⑤ 세로 일렬 폴백 → ⑥ 세로 일렬 + 평행이동.
        /// 개별 버튼 클램프는 어느 단계에서도 쓰지 않는다 — 그것만이 히트 원 겹침을 만든다.
        /// </summary>
        private void ComputeLayout()
        {
            _screenPointsAtLayout = ScreenSizePoints();
            _baseAngleDegrees = Snap45(_screenPointsAtLayout * 0.5f - _gearCenterPoints);

            if (TrySearchRotation(ButtonDiameterPoints, allowShift: false)) return;
            if (TrySearchRotation(ButtonDiameterPoints, allowShift: true)) return;
            if (TrySearchRotation(ShrunkDiameterPoints, allowShift: false)) return;
            if (TrySearchRotation(ShrunkDiameterPoints, allowShift: true)) return;
            if (TryColumn(ButtonDiameterPoints)) return;
            if (TryColumn(ShrunkDiameterPoints)) return;

            PlaceColumn(ShrunkDiameterPoints);
            ShiftGroupIntoScreen();
        }

        private bool TrySearchRotation(float diameter, bool allowShift)
        {
            for (float offset = 0f; offset <= RotationSearchMaxDegrees + 0.01f; offset += RotationSearchStepDegrees)
            {
                if (TryFan(_baseAngleDegrees + offset, diameter, allowShift)) return true;
                if (offset > 0f && TryFan(_baseAngleDegrees - offset, diameter, allowShift)) return true;
            }
            return false;
        }

        private bool TryFan(float baseDegrees, float diameter, bool allowShift)
        {
            Vector2 shift = Vector2.zero;
            if (allowShift)
            {
                shift = RequiredShift(baseDegrees, diameter);
                if (shift.magnitude > MaxGroupShiftPoints) return false;
            }

            for (int i = 0; i < ButtonCount; i++)
            {
                if (!IsBoxOnScreen(BoxFor(FanCenter(baseDegrees, i) + shift, diameter))) return false;
            }

            for (int i = 0; i < ButtonCount; i++) _buttons[i].CenterPoints = FanCenter(baseDegrees, i) + shift;
            _baseAngleDegrees = Mathf.Repeat(baseDegrees, 360f);
            _diameterPoints = diameter;
            return true;
        }

        /// <summary>슬롯 i의 각도 오프셋. θ₀ + ((n−1)/2 − i)·step — <b>부채꼴은 언제나 θ₀를 기준으로
        /// 좌우 대칭</b>이라 버튼 개수가 바뀌어도 "가운데가 화면 안쪽"이라는 성질이 유지된다.
        /// n=3이면 (1−i)·60°, n=4면 (1.5−i)·30°(36-3-3).
        ///
        /// <see cref="Snap45"/>와 같은 이유로 <b>public static 순수 함수</b>다 — 기하 확정치는 씬 없이
        /// EditMode에서 잠글 수 있어야 한다(36절의 계산이 코드에서 조용히 어긋나는 것을 막는 유일한 방법).</summary>
        public static float SlotOffsetDegrees(int index) => ((ButtonCount - 1) * 0.5f - index) * ButtonAngleStepDegrees;

        /// <summary>기어 중심과 기준각이 주어졌을 때 슬롯 i 버튼의 중심(캔버스 포인트). 순수 함수.</summary>
        public static Vector2 SlotCenterPoints(Vector2 gearCenterPoints, float baseDegrees, int index)
        {
            float a = (baseDegrees + SlotOffsetDegrees(index)) * Mathf.Deg2Rad;
            return gearCenterPoints + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * OrbitRadiusPoints;
        }

        private Vector2 FanCenter(float baseDegrees, int index)
            => SlotCenterPoints(_gearCenterPoints, baseDegrees, index);

        /// <summary>이 각도의 부채꼴을 화면 안으로 넣는 데 필요한 <b>최소 평행이동</b>.</summary>
        private Vector2 RequiredShift(float baseDegrees, float diameter)
        {
            Rect union = BoxFor(FanCenter(baseDegrees, 0), diameter);
            for (int i = 1; i < ButtonCount; i++)
            {
                Rect box = BoxFor(FanCenter(baseDegrees, i), diameter);
                union = Rect.MinMaxRect(Mathf.Min(union.xMin, box.xMin), Mathf.Min(union.yMin, box.yMin),
                    Mathf.Max(union.xMax, box.xMax), Mathf.Max(union.yMax, box.yMax));
            }
            return ShiftToFit(union);
        }

        private bool TryColumn(float diameter)
        {
            PlaceColumn(diameter);
            for (int i = 0; i < ButtonCount; i++)
            {
                if (!IsBoxOnScreen(BoxFor(_buttons[i].CenterPoints, diameter))) return false;
            }
            _diameterPoints = diameter;
            return true;
        }

        /// <summary>
        /// 세로 일렬 폴백 — 화면 안쪽 수직 방향. 간격은 <b>52pt 고정</b>이다(36-3-3): 라벨이 사라져
        /// "지름 + 라벨 간격 + 라벨 높이"라는 하한 계산식 자체가 없어졌고, Ø44 + 히트 여백 4×2 = 52라
        /// 이 값이 곧 "히트 원이 겹치지 않는 최소 간격"이다.
        /// </summary>
        private void PlaceColumn(float diameter)
        {
            float spacing = ColumnFallbackSpacingPoints;
            float sign = _gearCenterPoints.y > _screenPointsAtLayout.y * 0.5f ? -1f : 1f;
            for (int i = 0; i < ButtonCount; i++)
            {
                _buttons[i].CenterPoints = new Vector2(
                    _gearCenterPoints.x,
                    _gearCenterPoints.y + sign * (OrbitRadiusPoints + i * spacing));
            }
            _diameterPoints = diameter;
        }

        /// <summary>네 버튼을 <b>같은 벡터로</b> 평행이동해 화면 안으로 넣는다(형태 보존 — 개별 클램프 금지).</summary>
        private void ShiftGroupIntoScreen()
        {
            Rect union = BoxFor(_buttons[0].CenterPoints, _diameterPoints);
            for (int i = 1; i < ButtonCount; i++)
            {
                Rect box = BoxFor(_buttons[i].CenterPoints, _diameterPoints);
                union = Rect.MinMaxRect(Mathf.Min(union.xMin, box.xMin), Mathf.Min(union.yMin, box.yMin),
                    Mathf.Max(union.xMax, box.xMax), Mathf.Max(union.yMax, box.yMax));
            }

            Vector2 shift = ShiftToFit(union);
            for (int i = 0; i < ButtonCount; i++) _buttons[i].CenterPoints += shift;
        }

        /// <summary>
        /// ★ 2026-09-02 — 실제로 쓰는 상단 여백. <c>max(설계 하한 40, OS가 보고한 예약 띠 두께)</c>.
        ///
        /// <para><b>왜 max인가</b>: 40은 "메뉴바 위에 안 걸리게" 정한 <b>설계 여백</b>이고, 예약 띠
        /// 두께는 <b>사실</b>이다. 둘 중 하나로 갈아치우면 한쪽을 잃는다 — 사실만 쓰면 띠가 없는
        /// 환경에서 부채꼴이 화면 맨 위에 달라붙고, 설계값만 쓰면 띠가 40보다 두꺼운 환경
        /// (Windows 상단 도킹 작업표시줄 · 배율 150%)에서 남의 막대를 덮는다.</para>
        ///
        /// <para>실측 대조: macOS 메뉴바 33 → <c>max(40, 33) = 40</c>으로 <b>지금과 한 픽셀도 다르지
        /// 않다</b>. 값이 바뀌는 것은 두꺼운 상단 띠를 가진 Windows뿐이다(docs/UX_FLOW.md 51-13).</para>
        /// </summary>
        private float EffectiveTopMarginPoints => EffectiveMarginPoints(TopMarginPoints,
            ReservedTopBarProbe.TopInsetPoints(_agent != null ? _agent.PlatformService : null));

        /// <summary>
        /// ★★ 2026-09-03 — 실제로 쓰는 <b>왼쪽/오른쪽</b> 여백. 상단과 <b>같은 식</b>이다:
        /// <c>max(설계 여백 8, OS가 보고한 그 변의 예약 띠 두께)</c>.
        ///
        /// <para><b>왜 필요했나</b>: 톱니가 화면 오른쪽 끝에 사는데 <c>IsBoxOnScreen</c>·
        /// <c>ShiftToFit</c>가 오른쪽 한계를 <c>화면폭 − 8</c>로만 봤다. 그래서 <b>우측 도킹
        /// 작업표시줄</b>(48~62pt) 앞에서 부채꼴이 그 띠 안으로 40pt까지 들어갔다(실측 계산:
        /// 화면 1512 / 띠 48 / 기본 배치에서 슬롯 0의 상자 xMax = 1504, 띠 시작 1464).
        /// 톱니를 <b>눌러서 여는 것</b>이 이 부채꼴이라, 톱니만 고치고 여기를 두면
        /// 열린 메뉴가 여전히 남의 막대를 덮는다.</para>
        ///
        /// <para><b>회귀 없음</b>: 띠가 0이면 <c>max(8, 0) = 8</c>로 <b>지금과 비트 동일</b>하다.
        /// 못 쟀을 때도 프로브가 0을 주므로 같다 — 짐작으로 메우지 않는다
        /// (<see cref="ReservedEdgeProbe"/> 규약).</para>
        /// </summary>
        private float EffectiveLeftMarginPoints => EffectiveMarginPoints(ScreenMarginPoints,
            ReservedEdgeProbe.EdgeInsetPoints(_agent != null ? _agent.PlatformService : null, ReservedEdge.Left));

        /// <inheritdoc cref="EffectiveLeftMarginPoints"/>
        private float EffectiveRightMarginPoints => EffectiveMarginPoints(ScreenMarginPoints,
            ReservedEdgeProbe.EdgeInsetPoints(_agent != null ? _agent.PlatformService : null, ReservedEdge.Right));

        /// <summary>
        /// <b>설계 여백</b>과 <b>관측된 예약 띠 두께</b>를 합치는 식 — 네 변이 <b>이 한 줄</b>을 공유한다.
        ///
        /// <para><b>왜 max인가</b>: 설계 여백은 "화면 끝에 달라붙지 않게" 정한 값이고 띠 두께는
        /// <b>사실</b>이다. 하나로 갈아치우면 한쪽을 잃는다 — 사실만 쓰면 띠가 없는 환경에서 부채꼴이
        /// 화면 끝에 달라붙고, 설계값만 쓰면 띠가 그보다 두꺼운 환경에서 남의 막대를 덮는다.</para>
        ///
        /// <para><b>public static 순수 함수</b>인 이유는 <see cref="Snap45"/>와 같다 — 씬 없이
        /// EditMode에서 잠글 수 있어야 하고, 테스트가 <c>Mathf.Max</c>를 <b>다시 타이핑</b>하면
        /// 그 사본이 프로덕션과 조용히 갈라진다.</para>
        /// </summary>
        public static float EffectiveMarginPoints(float designMarginPoints, float reservedInsetPoints)
            => Mathf.Max(designMarginPoints, reservedInsetPoints);

        /// <summary>이 사각형을 화면 여백 안으로 넣는 최소 이동 벡터(들어와 있으면 0).</summary>
        private Vector2 ShiftToFit(Rect union)
        {
            var shift = Vector2.zero;
            // ★ 2026-09-03 — 좌·우도 상단과 같이 <b>예약 띠</b>를 본다(EffectiveLeftMarginPoints 문서).
            float leftMargin = EffectiveLeftMarginPoints, rightMargin = EffectiveRightMarginPoints;
            if (union.xMin < leftMargin) shift.x = leftMargin - union.xMin;
            else if (union.xMax > _screenPointsAtLayout.x - rightMargin)
                shift.x = _screenPointsAtLayout.x - rightMargin - union.xMax;
            if (union.yMin < ScreenMarginPoints) shift.y = ScreenMarginPoints - union.yMin;
            else if (union.yMax > _screenPointsAtLayout.y - EffectiveTopMarginPoints)
                shift.y = _screenPointsAtLayout.y - EffectiveTopMarginPoints - union.yMax;
            return shift;
        }

        /// <summary>
        /// 버튼이 실제로 차지하는 상자 — <b>원 중심에 정렬된 정사각형</b>(Ø44 → 56×56).
        ///
        /// 36-4로 라벨이 사라지면서 종전의 두 가지 비대칭이 함께 사라졌다: (1) 폭이 글자 길이에 따라
        /// 버튼마다 달랐고, (2) 상자가 원보다 아래로 20pt 길어 중심이 원 중심에서 10pt 어긋나 있었다.
        /// 그 비대칭이 곧 기본 위치에서 평행이동 35.5pt를 만들던 원인 중 하나였다(36-3-2).
        /// </summary>
        public static Rect ButtonClampBox(Vector2 center, float diameter)
        {
            float side = diameter + ClampBoxPaddingPoints;
            return new Rect(center.x - side * 0.5f, center.y - side * 0.5f, side, side);
        }

        private static Rect BoxFor(Vector2 center, float diameter) => ButtonClampBox(center, diameter);

        private bool IsBoxOnScreen(Rect box)
            => box.xMin >= EffectiveLeftMarginPoints && box.yMin >= ScreenMarginPoints
               && box.xMax <= _screenPointsAtLayout.x - EffectiveRightMarginPoints
               && box.yMax <= _screenPointsAtLayout.y - EffectiveTopMarginPoints;

        // ==================== 좌표 변환 ====================

        private float PixelsPerPoint => ScreenCoordinateConverter.CanvasToUnityScreen(1f, _config);

        private Vector2 ScreenToPoints(Vector2 unityScreen) => new Vector2(
            ScreenCoordinateConverter.UnityScreenToCanvas(unityScreen.x, _config),
            ScreenCoordinateConverter.UnityScreenToCanvas(unityScreen.y, _config));

        private Vector2 PointsToScreen(Vector2 points) => new Vector2(
            ScreenCoordinateConverter.CanvasToUnityScreen(points.x, _config),
            ScreenCoordinateConverter.CanvasToUnityScreen(points.y, _config));

        private Vector2 ScreenSizePoints() => ScreenToPoints(new Vector2(Screen.width, Screen.height));

        private void ApplyCanvasScaleFactor()
        {
            if (_scaler == null) return;
            float target = ScreenCoordinateConverter.ResolveCanvasScaleFactor(_config);
            if (!Mathf.Approximately(_scaler.scaleFactor, target)) _scaler.scaleFactor = target;
        }

        // ==================== 도형 만들기 ====================

        private void BuildUi()
        {
            var canvasGo = new GameObject("GearRadialMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            // ★★ 씬 루트에 단다(캐릭터의 자식이 아니다) — InfoGearIconWidget._container와 같은 전례.
            // 이유 둘: (1) ScreenSpaceOverlay 캔버스는 화면 좌표계에 사는 물건이라 걷고 넘어지는
            // 캐릭터의 Transform 계보에 속할 이유가 애초에 없다. (2) 2026-08-30 회귀 — 이 캔버스가
            // 캐릭터 루트 아래 있었기 때문에, 그 안의 "Head"라는 UI 자손이 이름으로 캐릭터 파츠를
            // 찾는 코드에 잡혀 머리·몸통이 영영 안 움직였다. 계층을 분리하면 이 UI가 어떤 이름을
            // 쓰든 캐릭터 탐색에 구조적으로 걸릴 수 없다. 정리는 OnDestroy가 책임진다.
            canvasGo.transform.SetParent(null, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;
            _scaler = canvasGo.GetComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            ApplyCanvasScaleFactor();

            var rootGo = new GameObject("Fan", typeof(RectTransform));
            rootGo.transform.SetParent(canvasGo.transform, false);
            _root = rootGo.GetComponent<RectTransform>();
            _root.anchorMin = _root.anchorMax = _root.pivot = Vector2.zero;
            _root.sizeDelta = Vector2.zero;

            for (int i = 0; i < ButtonCount; i++) _buttons[i] = BuildButton(i);
            BuildHoverLabel();
            BuildOnboardingHint();
            canvasGo.SetActive(false);
        }

        /// <summary>
        /// 호버 이름표 하나를 만든다(2026-08-31 사용자 지시). 버튼 그룹의 자식이 아니라 <b>_root의
        /// 직계</b>다 — 버튼과 함께 스케일/궤도 이동을 타면 호버로 원이 커질 때 글자까지 커져 어수선해지고
        /// (예전 라벨 알약이 Group/Root를 나눠야 했던 바로 그 이유), 무엇보다 이름표는 버튼의 일부가
        /// 아니라 <b>화면에 떠 있는 툴팁</b>이라 자기만의 클램프 규칙을 가져야 하기 때문이다.
        ///
        /// 글자 폭은 여기서 <b>한 번만</b> 잰다. <see cref="Text.preferredWidth"/>는 폰트 메시를 다시
        /// 재는 호출이라 매 프레임 부르면 24시간 상주 앱에서 그대로 비용이 된다.
        /// </summary>
        private void BuildHoverLabel()
        {
            var go = new GameObject("HoverLabel", typeof(RectTransform));
            go.transform.SetParent(_root, false);
            _hoverLabel = go.GetComponent<RectTransform>();
            _hoverLabel.anchorMin = _hoverLabel.anchorMax = _hoverLabel.pivot = new Vector2(0.5f, 0.5f);
            _hoverLabel.sizeDelta = new Vector2(44f, HoverLabelHeightPoints);

            _hoverLabelSurface = UiChrome.AddSurface(_hoverLabel, "Surface", UiChrome.PanelSurface, 9);
            UiChrome.Stretch(_hoverLabelSurface.rectTransform);
            _hoverLabelSurface.raycastTarget = false;   // 툴팁은 클릭을 먹지 않는다.
            _hoverLabelBorder = UiChrome.AddOutline(_hoverLabel, "Border", UiChrome.PanelBorder, 9);
            _hoverLabelBorder.raycastTarget = false;

            _hoverLabelText = UiChrome.AddText(_hoverLabel, "Text", UiChrome.FontCaption,
                TextAnchor.MiddleCenter, UiChrome.TextPrimary);
            UiChrome.Stretch(_hoverLabelText.rectTransform);

            for (int i = 0; i < ButtonCount; i++)
            {
                _hoverLabelText.text = ButtonNames[i];
                _nameWidths[i] = _hoverLabelText.preferredWidth + HoverLabelPaddingPoints;
            }
            _hoverLabelText.text = string.Empty;

            SetHoverLabelAlpha(0f);
        }

        /// <summary>최초 1회 안내 알약. 호버 이름표와 <b>같은 크롬</b>(알약 + 테두리 + 10pt 글자)을 쓰되
        /// 테두리만 강조색이다 — 같은 가족이면서 "지금 이건 다른 종류의 말"임을 색 하나로 구분한다.
        /// 글자 폭은 여기서 한 번만 잰다(<see cref="Text.preferredWidth"/>는 폰트 메시를 다시 재는 호출).</summary>
        private void BuildOnboardingHint()
        {
            var go = new GameObject("OnboardingHint", typeof(RectTransform));
            go.transform.SetParent(_root, false);
            _onboardingHint = go.GetComponent<RectTransform>();
            _onboardingHint.anchorMin = _onboardingHint.anchorMax = _onboardingHint.pivot = new Vector2(0.5f, 0.5f);

            _onboardingHintSurface = UiChrome.AddSurface(_onboardingHint, "Surface", UiChrome.PanelSurface, 9);
            UiChrome.Stretch(_onboardingHintSurface.rectTransform);
            _onboardingHintSurface.raycastTarget = false;   // 안내는 클릭을 먹지 않는다(원칙 2).
            _onboardingHintBorder = UiChrome.AddOutline(_onboardingHint, "Border", UiChrome.AccentBorder, 9);
            _onboardingHintBorder.raycastTarget = false;

            _onboardingHintText = UiChrome.AddText(_onboardingHint, "Text", UiChrome.FontCaption,
                TextAnchor.MiddleCenter, UiChrome.TextPrimary);
            UiChrome.Stretch(_onboardingHintText.rectTransform);
            _onboardingHintText.text = OnboardingHintText;

            _onboardingHintWidth = _onboardingHintText.preferredWidth + HoverLabelPaddingPoints;
            _onboardingHint.sizeDelta = new Vector2(_onboardingHintWidth, HoverLabelHeightPoints);

            _onboardingHintSurface.color = Fade(UiChrome.PanelSurface, 0f);
            _onboardingHintBorder.color = Fade(UiChrome.AccentBorder, 0f);
            _onboardingHintText.color = Fade(UiChrome.TextPrimary, 0f);
            go.SetActive(false);
        }

        private ButtonView BuildButton(int index)
        {
            var view = new ButtonView();
            float d = ButtonDiameterPoints;

            var groupGo = new GameObject("Button" + index, typeof(RectTransform));
            groupGo.transform.SetParent(_root, false);
            view.Group = groupGo.GetComponent<RectTransform>();
            view.Group.anchorMin = view.Group.anchorMax = view.Group.pivot = new Vector2(0.5f, 0.5f);
            view.Group.sizeDelta = Vector2.zero;

            // 원 묶음(스케일 대상). 라벨 알약이 있던 시절에는 Group/Root를 나눠 글자만 스케일에서
            // 빼야 했지만(호버로 글자가 커지면 어수선하다), 이제 둘 다 원만 담는다. 계층을 합치지 않은
            // 이유는 Group.anchoredPosition이 곧 "궤도 위 위치"이고 Root.localScale이 곧 "호버/펼침
            // 스케일"이라는 역할 분리가 애니메이션 코드를 단순하게 유지하기 때문이다.
            var circleGo = new GameObject("Circle", typeof(RectTransform));
            circleGo.transform.SetParent(view.Group, false);
            view.Root = circleGo.GetComponent<RectTransform>();
            view.Root.anchorMin = view.Root.anchorMax = view.Root.pivot = new Vector2(0.5f, 0.5f);
            view.Root.sizeDelta = new Vector2(d, d);

            view.Surface = UiChrome.AddCircle(view.Root, "Surface", d, UiChrome.CardSurface);
            view.Border = UiChrome.AddCircle(view.Root, "Border", d, UiChrome.CardBorder, 1.2f);
            view.Flash = UiChrome.AddCircle(view.Root, "Flash", d, new Color(0f, 0f, 0f, 0f));

            var symbolGo = new GameObject("Symbol", typeof(RectTransform));
            symbolGo.transform.SetParent(view.Root, false);
            view.Symbol = symbolGo.GetComponent<RectTransform>();
            view.Symbol.anchorMin = view.Symbol.anchorMax = view.Symbol.pivot = new Vector2(0.5f, 0.5f);
            view.Symbol.sizeDelta = new Vector2(SymbolBoxPoints, SymbolBoxPoints);

            view.SymbolParts = (GearMenuButton)index switch
            {
                GearMenuButton.FocusMode => BuildStopwatchSymbol(view),
                GearMenuButton.Character => BuildStickmanSymbol(view),
                GearMenuButton.Action => BuildMegaphoneSymbol(view.Symbol),
                _ => BuildChecklistSymbol(view),
            };

            // ★ 라벨 알약 서브트리는 2026-08-31에 통째로 삭제됐다(36-4, 사용자 지시). 되살릴 때는
            //   지금의 기하(56×56 정사각 클램프 상자)에 맞춰 새로 쓴다 — 옛 코드를 남겨두지 않았다.

            // ---- 오늘 할일 미완료 배지(유일하게 남은 글자 — 이름이 아니라 상태 수량이다) ----
            if ((GearMenuButton)index == GearMenuButton.Todo)
            {
                var badgeGo = new GameObject("Badge", typeof(RectTransform));
                badgeGo.transform.SetParent(view.Root, false);
                view.Badge = badgeGo.GetComponent<RectTransform>();
                view.Badge.anchorMin = view.Badge.anchorMax = view.Badge.pivot = new Vector2(0.5f, 0.5f);
                view.Badge.sizeDelta = new Vector2(16f, 16f);
                view.Badge.anchoredPosition = new Vector2(15f, 15f);
                view.BadgeSurface = UiChrome.AddCircle(view.Badge, "BadgeSurface", 16f, UiChrome.Accent);
                // ★ 2026-09-01 글리프 잔차 제거(사용자 신고 "텍스트도 다 번져보임"): 9 -> 10.
                //   캔버스 배율 1.5에서 홀수 pt는 반드시 비정수 배로 리샘플된다(9pt -> 13.5px 요청,
                //   14px로 구워짐 = 0.964배). 짝수만 잔차 0(Platform/UiGlyphScalePolicy.cs).
                //   레이아웃 영향 없음 — 배지는 16x16 원, 글자는 Stretch + MiddleCenter + Overflow이고
                //   최대 문자열은 "9+"(2자)라 10pt 볼드에서도 원 안에 남는다.
                view.BadgeText = UiChrome.AddText(view.Badge, "BadgeText", 10, TextAnchor.MiddleCenter,
                    UiChrome.OnAccentSolid, bold: true);
                UiChrome.Stretch(view.BadgeText.rectTransform);
                view.Badge.gameObject.SetActive(false);
            }

            return view;
        }

        /// <summary>① 집중 모드 — 스톱워치(용두 + 링 + 바늘 2). 세션이 돌면 이 링이 그대로 잔여 시간 호가 된다.</summary>
        private Image[] BuildStopwatchSymbol(ButtonView view)
        {
            Transform p = view.Symbol;
            var crown = UiChrome.AddStroke(p, "Crown", 6f, 4f, 0f, new Vector2(0f, 11.5f), UiChrome.TextPrimary);
            view.RingTrack = UiChrome.AddCircle(p, "Ring", 20f, UiChrome.TextPrimary, SymbolStroke);

            // 잔여 시간 호 — 같은 링 위에 겹쳐 그린다(세션 중에만 켠다).
            view.RingFill = UiChrome.AddCircle(p, "RingFill", 20f, UiChrome.WarmAccent, SymbolStroke);
            view.RingFill.type = Image.Type.Filled;
            view.RingFill.fillMethod = Image.FillMethod.Radial360;
            view.RingFill.fillOrigin = (int)Image.Origin360.Top;
            view.RingFill.fillClockwise = true;
            view.RingFill.fillAmount = 1f;
            view.RingFill.gameObject.SetActive(false);

            var minute = UiChrome.AddStroke(p, "MinuteHand", 6.5f, SymbolStroke, 90f, new Vector2(0f, 3.25f), UiChrome.TextPrimary);
            float hourAngle = -30f * Mathf.Deg2Rad;
            var hour = UiChrome.AddStroke(p, "HourHand", 5f, SymbolStroke, -30f,
                new Vector2(Mathf.Cos(hourAngle) * 2.5f, Mathf.Sin(hourAngle) * 2.5f), UiChrome.TextPrimary);

            return new[] { crown, view.RingTrack, minute, hour };
        }

        /// <summary>② 캐릭터 — 미니 스틱맨. <b>영원히 같은 그림</b>이다(장비/포즈를 반영하면 내비게이션
        /// 표지로서의 식별성을 잃는다 — 32-4 ②).</summary>
        private Image[] BuildStickmanSymbol(ButtonView view)
        {
            Transform p = view.Symbol;

            // ★★ 2026-08-30 회귀의 <b>생산자 측</b> 수정 — 부품 이름에 "Icon" 접두사를 붙인다.
            // 예전 이름은 "Head"/"ArmL"/"LegL"이었고, 그중 "Head"가 프리팹 캐릭터의 머리 앵커와
            // 글자 그대로 같은 이름이었다. 이름으로 캐릭터 파츠를 찾는 소비자(StickmanPoseAnimator /
            // StickmanMetrics / EyeController / DialogueBubbleRenderer / CharacterAccessoryRenderer)가
            // 이 UI 원을 진짜 머리로 착각해 캐릭터 머리·몸통이 영영 안 움직였다.
            // 소비자 쪽은 탐색 범위를 좁혀 이미 막았지만, 그 방어는 "지금의 계층 규약"에 기대는 것이라
            // 여기서 이름 충돌 자체를 없앤다(위 BuildUi의 씬 루트 부착과 합쳐 이중 차단).
            var head = UiChrome.AddCircle(p, "IconHead", 7f, UiChrome.TextPrimary, 1.8f);
            head.rectTransform.anchoredPosition = new Vector2(0f, 8f);

            var spine = UiChrome.AddStroke(p, "IconSpine", 9f, 1.8f, 90f, Vector2.zero, UiChrome.TextPrimary);
            var shoulder = new Vector2(0f, 3.5f);
            var pelvis = new Vector2(0f, -4.5f);
            var armL = UiChrome.AddStroke(p, "IconArmL", 6f, 1.8f, -140f, shoulder + Polar(-140f, 3f), UiChrome.TextPrimary);
            var armR = UiChrome.AddStroke(p, "IconArmR", 6f, 1.8f, -40f, shoulder + Polar(-40f, 3f), UiChrome.TextPrimary);
            var legL = UiChrome.AddStroke(p, "IconLegL", 7f, 1.8f, -106f, pelvis + Polar(-106f, 3.5f), UiChrome.TextPrimary);
            var legR = UiChrome.AddStroke(p, "IconLegR", 7f, 1.8f, -74f, pelvis + Polar(-74f, 3.5f), UiChrome.TextPrimary);

            return new[] { head, spine, armL, armR, legL, legR };
        }

        /// <summary>③ 오늘 할일 — 체크리스트(글줄 3 + 빈 박스 2 + 체크마크 + 취소선).</summary>
        private Image[] BuildChecklistSymbol(ButtonView view)
        {
            Transform p = view.Symbol;
            var line0 = UiChrome.AddStroke(p, "Line0", 9f, SymbolStroke, 0f, new Vector2(5.5f, 7f), UiChrome.TextPrimary);
            var line1 = UiChrome.AddStroke(p, "Line1", 9f, SymbolStroke, 0f, new Vector2(5.5f, 0f), UiChrome.TextPrimary);
            var line2 = UiChrome.AddStroke(p, "Line2", 9f, SymbolStroke, 0f, new Vector2(5.5f, -7f), UiChrome.TextPrimary);
            var strike = UiChrome.AddStroke(p, "Strike", 9f, 1.4f, 0f, new Vector2(5.5f, -7f), UiChrome.TextPrimary);

            Image box0 = AddSmallBox(p, "Box0", new Vector2(-6f, 7f));
            Image box1 = AddSmallBox(p, "Box1", new Vector2(-6f, 0f));

            var checkVertex = new Vector2(-7f, -8f);
            var checkShort = UiChrome.AddStroke(p, "CheckShort", 3.2f, 1.6f, 135f,
                checkVertex + Polar(135f, 1.6f), UiChrome.Accent);
            var checkLong = UiChrome.AddStroke(p, "CheckLong", 6f, 1.6f, 45f,
                checkVertex + Polar(45f, 3f), UiChrome.Accent);

            // 체크마크는 Accent 고정(완료를 뜻하는 유일한 색) — 심볼 색 보간 대상에서 제외하고
            // 알파만 따라가게 한다.
            view.SymbolFixedParts = new[] { checkShort, checkLong };
            return new[] { line0, line1, line2, strike, box0, box1 };
        }

        /// <summary>
        /// ④ 행동 — <b>확성기</b>(6획, 36-5). 나머지 셋(스톱워치=원 / 스틱맨=수직 대칭 / 체크리스트=수평
        /// 줄)이 전부 좌우 대칭인 반면 확성기는 <b>오른쪽을 향한 비대칭 실루엣</b>이라 22pt 축소에서도
        /// 형태가 충돌하지 않는다. 의미도 정확하다 — 이 버튼은 캐릭터의 상태를 <b>보는</b> 곳이 아니라
        /// 캐릭터에게 <b>시키는</b> 곳이다.
        ///
        /// 손잡이는 일부러 뺐다: 24pt 상자에서 손잡이 획은 잉크 얼룩이 되고, 나팔 + 소리선만으로 이미
        /// 확성기로 읽힌다. 배지도 상태 반영도 없다(32-4 ② — 내비게이션 표지는 영원히 같은 그림이다).
        /// </summary>
        private static Image[] BuildMegaphoneSymbol(Transform p)
        {
            var upper = UiChrome.AddStroke(p, "HornUpper", 13f, SymbolStroke, 13f, new Vector2(-1.6f, 5.0f), UiChrome.TextPrimary);
            var lower = UiChrome.AddStroke(p, "HornLower", 13f, SymbolStroke, -13f, new Vector2(-1.6f, -5.0f), UiChrome.TextPrimary);
            var neck = UiChrome.AddStroke(p, "HornNeck", 5.6f, SymbolStroke, 90f, new Vector2(-8.4f, 0f), UiChrome.TextPrimary);
            var mouth = UiChrome.AddStroke(p, "HornMouth", 11.6f, SymbolStroke, 90f, new Vector2(5.0f, 0f), UiChrome.TextPrimary);
            var waveUp = UiChrome.AddStroke(p, "WaveUpper", 4.6f, 1.6f, 30f, new Vector2(9.6f, 3.4f), UiChrome.TextPrimary);
            var waveDown = UiChrome.AddStroke(p, "WaveLower", 4.6f, 1.6f, -30f, new Vector2(9.6f, -3.4f), UiChrome.TextPrimary);
            return new[] { upper, lower, neck, mouth, waveUp, waveDown };
        }

        private static Image AddSmallBox(Transform parent, string name, Vector2 center)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(4.5f, 4.5f);
            rt.anchoredPosition = center;
            var image = go.GetComponent<Image>();
            image.sprite = UiChrome.RoundedOutline(2, 1);
            image.type = Image.Type.Sliced;
            image.color = UiChrome.TextPrimary;
            image.raycastTarget = false;
            return image;
        }

        private static Vector2 Polar(float degrees, float radius)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r) * radius, Mathf.Sin(r) * radius);
        }
    }
}
