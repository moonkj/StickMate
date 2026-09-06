using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// 부채꼴 메뉴의 칸. <b>값이 곧 슬롯 순서</b>다 — 다섯이 전부 한 호 위에 θ₀ + (2 − i)·22.5°로
    /// 선다(i=0 → θ₀+45°, i=4 → θ₀−45°, 궤도 148pt). docs/UX_FLOW.md 36-3-4 / 53.
    ///
    /// ★ <b>2026-09-06 — i=4는 더 이상 「축 위의 위성」이 아니다.</b> 사용자 신고
    /// <i>"끄기 버튼만 따로 떨어져 있다"</i>로 [앱 종료]가 <b>다섯 번째 호 슬롯</b>이 됐다.
    ///
    /// ★ <b>기존 0/1/2는 재번호하지 않는다.</b> 32절이 "값이 곧 슬롯 순서"라고 못박았으므로 중간 삽입은
    /// Todo 2→3 재번호를 일으키고, 그 값을 읽는 switch가 조용히 어긋날 수 있다. 이제 <b>다섯 버튼이
    /// 앵커에서 완전히 등거리(148pt)</b>라 슬롯별 조작 비용 차이가 없어 "새 항목을 좋은 자리에" 논쟁
    /// 자체가 성립하지 않는다 — 관례대로 끝에 붙인다.
    /// </summary>
    public enum GearMenuButton
    {
        FocusMode = 0,
        Character = 1,
        Todo = 2,

        /// <summary>2026-08-31 신설 — 행동 명령창(<see cref="ActionCommandPopover"/>) 진입점.
        /// 이 버튼은 캐릭터의 상태를 <b>보는</b> 곳이 아니라 캐릭터에게 <b>시키는</b> 곳이다(36-5).</summary>
        Action = 3,

        /// <summary>
        /// ★ 2026-09-03 신설 — <b>앱 종료</b>(사용자 지시: *"프로그램 종료버튼은 나사 메뉴 지금 4개중에
        /// 버튼 하나 추가해서 종료버튼으로 만들어줘"*).
        ///
        /// <para>★★ <b>2026-09-06 — 위성에서 다섯 번째 호 슬롯으로 옮겼다.</b> 사용자 신고 원문:
        /// <i>"끄기 버튼만 따로 떨어져 있다"</i>. 원래 설계(53-0)는 «되돌릴 수 없는 버튼을 가장 멀고
        /// 가장 다른 자리에 둔다»를 <b>위치</b>로 달성하려 했고, 궤도 168pt의 축 위 위성이 그 답이었다.
        /// <b>그 설계는 실패했다</b> — 사용자에게 그것은 «안전한 자리»가 아니라 <b>«메뉴에서 떨어져 나온
        /// 미아»</b>로 읽혔다. 기하 증명(debugger 2026-09-06): 위성을 호에 «붙어 보이게» 하는 궤도는
        /// <b>존재하지 않는다</b>(축 위에서 호와 히트 원이 안 겹치려면 최소 150.56pt가 필요한데,
        /// 168을 155로 줄여도 여유가 15.23 → 3.75pt로 깎일 뿐 13pt밖에 안 가까워진다).</para>
        ///
        /// <para><b>그래서 「다름」을 위치가 아니라 <see cref="ApplyButtonStyle"/>의 색·무장 단계에
        /// 전적으로 맡긴다</b>: 평상 <c>SubtleSurface</c>(다른 넷보다 어둡다) · 호버해도 강조색까지 가지
        /// 않고 겨우 «다른 버튼의 평상 수준» · <b>2단 확인 3초</b>. 오폭 방어의 실질은 원래도 2단 확인이었다.</para>
        ///
        /// <para>★ 번호는 <b>끝</b>이다. 36-3-4가 [행동]을 넣을 때 못박은 규칙과 같다 — 기존 0~3을
        /// 재번호하면 그 값을 읽는 switch가 조용히 어긋난다. 그리고 <b>세로 일렬 폴백에서 「가장 위험한
        /// 것이 가장 멀다」가 유지되는 것도 이 번호 덕분</b>이다(폴백은 번호 순으로 멀어진다).</para>
        /// </summary>
        Quit = 4,
    }

    /// <summary>
    /// ★ 이 부채꼴을 <b>어느 문이 열었는가</b>(2026-09-05, 진입점이 둘이 됐다 —
    /// docs/UX_RIGHTCLICK_FAN_MENU.md §5-7).
    ///
    /// <para>필요한 이유는 하나다: <b>같은 문을 다시 누르면 토글로 닫고, 다른 문을 누르면 접었다가
    /// 그 자리에서 다시 편다.</b> 앵커 좌표를 부동소수로 비교해 «같은 문인가»를 판정하면
    /// 캐릭터가 1pt만 걸어도 «다른 문»이 되어 토글이 사라진다 — 그래서 좌표가 아니라 <b>출처</b>로 가른다.</para>
    /// </summary>
    public enum GearMenuAnchorSource
    {
        /// <summary>화면 위 톱니 아이콘(<c>InfoGearIconWidget</c>). 위쪽 바이어스 0.</summary>
        Gear = 0,

        /// <summary>캐릭터 몸 중심 우클릭(<c>AppControlDirector</c>). 위쪽 바이어스
        /// <see cref="GearRadialMenuWidget.FanUpBiasPoints"/>.</summary>
        Character = 1,
    }

    /// <summary>접힘의 종류 — <b>움직임이 서로 달라야</b> "내가 접었다"와 "시간이 지나 닫혔다"가 구분된다.</summary>
    public enum GearMenuCollapseMode
    {
        /// <summary>사용자 동작(기어 재클릭 / 바깥 클릭 / 버튼 선택). 0.13초, 반지름까지 빨려 들어감.</summary>
        User,

        /// <summary>★ <b>앵커가 움직였다</b>. 0.08초, 가장 빠르게 치운다.
        /// <para>2026-09-05에 뜻이 <b>넓어졌다</b>: 원래는 「톱니를 옮기기 시작함」 전용이었고, 이제는
        /// <b>재앵커</b>(다른 문이 눌려 부채꼴이 자리를 옮긴다 — §5-7)가 같은 모드를 쓴다.
        /// 두 경우 모두 «앵커가 더 이상 여기 없다»라서 "가장 빠르게 치운다"가 정답이라 값이 그대로 맞는다.
        /// <b>이 멤버를 지우지 마라</b> — <c>switch</c> 두 곳(<c>ModeLabel</c>·<c>CollapseSecondsFor</c>)의
        /// 폴백이 정상값을 삼킨다.</para></summary>
        Drag,

        /// <summary>6초 무반응. 0.26초, 제자리에서 스르르(반지름 고정, 알파만).</summary>
        Auto,
    }

    /// <summary>
    /// ★ 톱니를 짧게 클릭했을 때 <b>촤르륵 펼쳐지는 원버튼 5개</b>(2026-09-06부터 <b>전부 한 호 위</b>) —
    /// docs/UX_FLOW.md <b>32절 + 36절 + 53절</b>.
    /// 2026-08-30 사용자 원문: "기어메뉴를 클릭했을때 집중모드 버튼 캐릭터 버튼 오늘 할일 버튼 3가지가
    /// 촤르륵 원버튼 3개가 나오고 각 버튼을 클릭했을때 세부 메뉴로 들어가도록".
    /// 2026-08-31 사용자 원문: "버튼 메뉴들의 텍스트는 전부삭제 필요 ... 기어아이콘에 메뉴하나 추가해서
    /// 행동들은 거기서 클릭하면 창 하나가 떠서 행동 명령 내릴수 있게".
    /// 2026-09-03 사용자 원문: "프로그램 종료버튼은 나사 메뉴 지금 4개중에 버튼 하나 추가해서 종료버튼으로 만들어줘".
    /// 2026-09-06 사용자 원문: "끄기 버튼만 따로 떨어져 있다" → 위성 폐지, 다섯 번째 호 슬롯으로 편입.
    /// 2026-09-06 사용자 원문: "메뉴를 펼쳤을때는 캐릭터가 제자리대기."
    ///   → 이 위젯은 캐릭터를 <b>움직이지 않는다</b>. <see cref="IsVisible"/>이라는 사실만 내놓고,
    ///     <c>States/StickmanBlackboard.IsRadialMenuHoldActive</c>를 거쳐 배회 AI가 스스로 멈춘다
    ///     (수평 이동 소유권은 배회 AI에 그대로 있다).
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
    /// ★★ <b>2026-09-06 — 다섯 번째가 호로 들어왔다. 그런데도 스팬은 여전히 90°다.</b>
    /// 사용자 신고 <i>"끄기 버튼만 따로 떨어져 있다"</i>로 위성([앱 종료], 궤도 168pt)이 폐지됐다.
    ///
    /// ★ <b>「간격 30°를 지킨 채 5칸」은 하면 안 된다</b> — 그러면 스팬이 <b>120°</b>가 되고,
    /// 53-1이 계산한 대로 모서리 각도창(1512×982에서 73.3~73.8°)을 회전 ±90°로도 못 메운다.
    /// <b>이 계산은 상시 톱니가 걷힌 뒤에도 여전히 참이다</b> — 캐릭터는 화면 한복판만 지나다니는 것이
    /// 아니라 <b>바닥과 좌우 끝까지 걸어다니기</b> 때문이다. 2026-09-06 실측 재계산(화면 8종 × 예약 띠
    /// 16종 · 12,265,232 표본):
    /// <code>
    ///   간격 30° 유지(스팬 120°)  세로 일렬 폴백 0.3770% -> <b>1.4960%</b>  (3.97배)
    ///   간격 22.5°  (스팬  90°)   세로 일렬 폴백 0.3770% -> <b>0.3942%</b>  (1.05배, 사실상 동수)
    /// </code>
    /// ⇒ <b>스팬을 지키고 간격을 22.5°로 줄이고 궤도를 148pt로 넓혔다.</b> 궤도를 넓히는 이유는
    /// 히트 원 겹침 하나뿐이다(유도는 <see cref="OrbitRadiusPoints"/> 문서). 그 결과 이웃 간
    /// 중심거리·시각 틈·히트 여유가 <b>전부 예전보다 미세하게 넉넉해졌고</b>(57.75 / 13.75 / 5.75pt),
    /// 부채꼴의 최대 도달 반경은 위성이 사라져 <b>196 -> 176pt로 줄었다</b>.
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
    /// 개별 버튼을 화면 안으로 밀어 넣으면 모서리에서 버튼들이 한 점으로 뭉개져 히트 원이 겹치고,
    /// 그러면 <b>보이는 것과 실제로 눌리는 것이 달라진다</b>(먼저 검사되는 버튼이 이긴다). 그래서
    /// 형태를 유지한 채 문제를 푼다: ① θ₀를 ±15°씩 최대 ±90°까지 돌려보고 → ② 세로 일렬 폴백 →
    /// ③ 지름 축소(44→36) → ④ 그래도 안 되면 <b>전체를 같은 벡터로</b> 평행이동(형태 보존).
    ///
    /// 기준각 θ₀는 사분면 부호가 아니라 <b>(화면 중심 − 기어 중심)의 실제 각도를 45° 단위로 스냅</b>한
    /// 값이다. 부호 방식이면 기어가 화면 위쪽 한가운데 있을 때 아래로 곧게 못 펼치고, 중앙선 근처에서
    /// 1픽셀 이동에 방향이 90° 튄다. 그리고 <b>펼치는 순간 한 번 계산해 그 열림이 끝날 때까지 고정</b>한다.
    /// </summary>
    public sealed class GearRadialMenuWidget : MonoBehaviour, IExclusiveSurface
    {
        /// <summary>부채꼴이 가진 버튼의 <b>총수</b>이자 <b>각도식의 분모</b>. 2026-09-03에 4 -> 5가 됐다.
        ///
        /// <para>★ <b>2026-09-06 — 여기 있던 「분모로 쓰지 마라」 경고가 사라졌다.</b> 그 경고는
        /// <c>ArcButtonCount</c>(호 4 + 위성 1)가 존재하던 동안에만 뜻이 있었고, 위성이 폐지되면서
        /// <b>다섯이 전부 한 호 위에 있다</b> — 총수와 분모가 같은 수가 됐으므로 함정 자체가 없다.
        /// 다시 호 밖으로 나가는 버튼을 만들 생각이라면 그 갈래를 되살리기 전에 아래
        /// <see cref="OrbitRadiusPoints"/> 문서를 읽어라(이름표 링이 그 전제 위에 서 있다).</para></summary>
        public const int ButtonCount = 5;

        // ==================== 확정 수치 (docs/UX_FLOW.md 32-1 / 32-2 / 53) ====================

        public const float ButtonDiameterPoints = 44f;
        public const float ShrunkDiameterPoints = 36f;
        public const float HoverScale = 48f / 44f;

        /// <summary>
        /// 호(弧) 궤도. 36-3-3에서 62 → 111이었고, <b>2026-09-06에 111 → 148</b>이 됐다.
        ///
        /// <para><b>왜 늘었나</b> — 사용자 신고 <i>"끄기 버튼만 따로 떨어져 있다"</i>로 [앱 종료]가
        /// 축 위 위성(궤도 168)에서 <b>다섯 번째 호 슬롯</b>으로 들어왔다. 다섯을 한 호에 세우면서도
        /// <b>스팬 90°를 지키려면</b> 간격이 30° → 22.5°가 되는데(<see cref="ButtonAngleStepDegrees"/>),
        /// 그러면 같은 궤도에서 이웃 간 중심거리가 <c>2·111·sin11.25° = 43.32pt</c>로 떨어져
        /// <b>히트 원(지름 52pt)이 겹친다</b> — 그 순간 «보이는 것과 눌리는 것이 달라진다».
        /// 반지름은 그 겹침을 푸는 <b>유일한 자유변수</b>다:
        /// <code>
        ///   비겹침 하한 : R ≥ 26 / sin(11.25°) = 133.34pt
        ///   시각 틈 13.5pt 유지 : R ≥ 57.5 / (2·sin11.25°) = 147.36pt
        ///   DPI(4의 배수)      : 148 = 4 × 37 → ×1.25/1.50/1.75 = 185 / 222 / 259px 전부 정수
        /// </code>
        /// 148에서 이웃 중심거리 <b>57.75pt</b> · 시각 틈 <b>13.75pt</b> · 히트 여유 <b>5.75pt</b>로,
        /// 셋 다 <b>111/30° 시절(57.46 / 13.46 / 5.46)보다 미세하게 낫다</b>. 36-3-1 표의 «틈 13.5pt»가
        /// 그대로 산다.</para>
        ///
        /// <para>★★ <b>「멀어졌다」가 아니다 — 부채꼴의 최대 도달 반경은 오히려 줄었다.</b>
        /// 폐지된 위성이 168pt에 있었으므로 가장 바깥 원의 중심은 <b>168 → 148pt로 20pt 안쪽</b>으로
        /// 들어왔고, 클램프 상자까지 포함한 도달 반경은 <b>196 → 176pt</b>다. 사용자가 이미 보고 있던
        /// 어떤 버튼보다도 멀리 나가는 것이 하나도 없다.</para>
        ///
        /// <para>★ <b>실측으로 정한 값이다</b>(2026-09-06 여백 격자 전수 재계산, 화면 8종 × 예약 띠
        /// 16종). 리더 인계안인 «간격 30° 유지 · 스팬 120°»는 캐릭터 도달 가능 앵커 띠에서 세로 일렬
        /// 폴백을 <b>16건 → 149건(9.3배)</b>으로 늘렸다 — 36-3의 «모서리에서 부채꼴을 막는 것은
        /// 반지름이 아니라 각도 스팬»이 위성 폐지 후에도 그대로 참이었다. 스팬 90°를 지킨 이 안은
        /// <b>18건</b>으로 현행과 사실상 동수다. 자세한 격자는 리더 보고 참조.</para>
        ///
        /// <para>★ 하한(기어 판정 반경 31.36 + 버튼 판정 26 = 57.4pt)은 전과 같이 여유롭게 넘는다.
        /// 캐릭터 GrabArea와의 이격은 오히려 커진다(호 히트 원 내측 가장자리 85 → <b>122pt</b>,
        /// 배율 1.00 GrabArea 최원점 91.68pt).</para>
        ///
        /// <para>★ 차단막(<see cref="UnionScreenRect"/>) 비용은 <b>이 변경에서 유일하게 나빠진 축</b>이고,
        /// <b>기준각에 따라 부호가 갈린다</b>(부채꼴이 정사각 상자에 어떻게 눕느냐의 문제다):
        /// <code>
        ///   θ₀ = 45°의 홀수배(대각)   175×175 -> <b>204×204</b>   면적 <b>+36.2%</b>
        ///   θ₀ = 0/90/180/270°(정방)  213×146 -> <b>265×99</b>    면적 <b>−15.0%</b>
        /// </code>
        /// 캐릭터 앵커는 <see cref="FanUpBiasPoints"/> 때문에 화면 중앙 넓은 영역에서 <b>θ₀=90°로
        /// 고정</b>되므로(§1-3-3 실측: 바닥을 걷는 캐릭터의 θ₀ 분포에서 90°가 최빈) 실사용에서는
        /// 줄어드는 쪽이 더 자주 걸린다. 어느 쪽이든 부채꼴이 <b>떠 있는 동안만</b>의 비용이고
        /// (6초 무반응이면 자동으로 접힌다) 1512×982에서 화면의 2.1% → 2.8%(대각) / 1.8%(정방)다.</para>
        /// </summary>
        public const float OrbitRadiusPoints = 148f;

        /// <summary>슬롯 사이 각도. 36-3-3에서 60 → 30이었고, <b>2026-09-06에 30 → 22.5</b>가 됐다.
        ///
        /// <para><see cref="ButtonCount"/>개 × 22.5° = <b>스팬 90°</b> — 36-3-1이 뽑아낸 핵심 결론이
        /// 버튼이 다섯이 되고 위성이 폐지된 뒤에도 <b>한 도(度)도 안 바뀌었다</b>. 그것이 이 값을 고른
        /// 이유 전부다: 모서리에서 부채꼴을 막는 것은 반지름이 아니라 <b>각도 스팬</b>이고, 스팬이
        /// 그대로면 화면 밖 방지 사다리의 통계도 그대로다(실측: 세로 일렬 폴백 16 → 18건).</para>
        ///
        /// <para>22.5 = 45/2라 θ₀(45° 스냅)와 <b>정확히 정합</b>한다 — 슬롯 각도가 부동소수 나머지를
        /// 남기지 않는다. 겹침을 푸는 반지름 유도는 <see cref="OrbitRadiusPoints"/> 문서에 있다.</para></summary>
        public const float ButtonAngleStepDegrees = 22.5f;

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

        /// <summary>36-3-3: 0.055 → 0.037 → <b>0.0275</b>초. "촤르륵"의 예산은 <b>0.30초로 정해져
        /// 있다</b>(32-2). <b>버튼이 하나 늘었다고 사용자를 매번 더 기다리게 만들지 않는다</b> —
        /// 이 값은 그 예산을 지키기 위해 버튼 수에서 <b>역산</b>된다:
        /// <code>
        ///   0.055 × 3 + 0.19 = 0.355   (3버튼 시절, 예산 초과)
        ///   0.037 × 3 + 0.19 = 0.301   (4버튼)
        ///   0.0275 × 4 + 0.19 = <b>0.300</b>  (5버튼 — 2026-09-03, 예산 정확히)
        /// </code>
        /// ★ 버튼을 또 늘릴 때 <b>여기를 안 고치면 열림이 길어진다.</b>
        /// <c>GearRadialFanGeometryTests</c>가 <see cref="ExpandTotalSeconds"/> ≤ 0.30을 잠근다.</summary>
        public const float ExpandStaggerSeconds = 0.0275f;

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
        /// 평행이동은 <b>형태를 완전히 보존</b>한다(버튼들의 상대 위치가 그대로다). 32-1이 금지한 것은
        /// 버튼을 <b>따로따로</b> 밀어 호를 찌그러뜨리는 일이지 강체 이동이 아니다.
        /// </summary>
        public const float MaxGroupShiftPoints = 48f;

        /// <summary>
        /// ★ <b>캐릭터 앵커 전용 「위쪽 바이어스」</b>(pt) — docs/UX_RIGHTCLICK_FAN_MENU.md §1-3-3.
        ///
        /// <para>임의값이 아니라 <b>부채꼴의 도달 반경</b>에서 나왔다: 당시 가장 바깥 버튼이던
        /// 위성 궤도 168 + 클램프 상자 반폭 28 = <b>196pt</b>. 앵커가 어느 변에서도 이만큼 떨어져
        /// 있으면 «어느 방향으로 열어도 화면에 들어간다» — 그 영역 안에서는 방향을 자유롭게 고를 수
        /// 있으므로 <b>고정된 하나</b>(위)를 고르는 편이 낫다.</para>
        ///
        /// <para>★ <b>2026-09-06 — 위성이 폐지되어 실제 도달 반경은 148 + 28 = 176pt로 줄었다.
        /// 그래도 이 값은 196 그대로 둔다.</b> 유도식이 «이만큼 떨어져 있으면 안전»이라는 <b>충분조건</b>이라
        /// 크게 잡는 쪽이 안전한 방향이고, 여기를 건드리면 §1-3-3이 1pt 격자로 실측한 θ₀ 분포표
        /// («항상 위 90°가 되는 반경 |Δ| ≤ 75.0pt» / «180° 반전 지점 y=687»)가 통째로 낡는다 —
        /// 이번 라운드가 재계산한 것은 <b>배치 사다리</b>이지 θ₀ 분포가 아니다.
        /// 줄이려면 그 표를 다시 재는 라운드와 함께 와야 한다.</para>
        ///
        /// <para><b>왜 필요한가</b>: 톱니는 화면 구석에 살아서 «화면중심 − 앵커»가 늘 길었다. 캐릭터는
        /// 화면 한가운데를 하루 종일 지나다니고, 그 순간 그 벡터는 0에 수렴해 <b>1pt 이동에 방향이
        /// 180° 뒤집힌다</b>(y = 화면 세로 중앙선. 캐릭터가 가장 자주 지나는 높이다).
        /// 바이어스를 태우면 그 반전 지점이 중앙 위 196pt로 밀려나고, 중앙선 가로 스캔은
        /// «0° → [225°] → 180°»에서 «45° → 90° → 135°»의 45° 계단이 된다.</para>
        ///
        /// <para>★★ <b><see cref="Snap45"/>에 이 값을 직접 넣지 마라.</b> 그 함수는 톱니 경로도 같이
        /// 쓴다 — 출하 기본 톱니 중심(1482, 924)의 θ₀가 <b>225° → 180°로 바뀌어 기본 화면이 통째로
        /// 달라진다</b>(UX_FLOW 36-3-4가 못박은 값). 그래서 바이어스는 <see cref="SnapFanBaseAngle"/>의
        /// <c>upBiasPoints</c> 인자로만 들어가고, 톱니는 0을 준다.</para>
        /// </summary>
        public const float FanUpBiasPoints = 196f;

        /// <summary>버튼이 전부 안착하기까지(0.19 + 0.0275×4 = <b>0.300초</b> — 32-2의 0.30초 예산).</summary>
        public static float ExpandTotalSeconds => ExpandSecondsPerButton + ExpandStaggerSeconds * (ButtonCount - 1);

        /// <summary>포스트잇(30000)·캐릭터 창(31000)보다 위, 팝오버(31700)보다 아래 —
        /// 부채꼴은 방금 사용자가 부른 것이라 다른 상시 패널에 가리면 안 되고, 자기가 낳은 팝오버를
        /// 가려서도 안 된다. (2026-08-31: 32760에 있던 우클릭 제어 메뉴는 폐지됐다 — 36-9.)</summary>
        private const int SortingOrder = 31500;

        // ====================================================================================
        // ★ FG-2 획 사다리 (docs/DESIGN_FAN_MENU_ICONS.md R26-5) — 2026-09-06
        // ====================================================================================
        // 이 다섯 글리프는 예전에 획 폭이 **6종**(1.0 / 1.4 / 1.6 / 1.8 / 2.0 / 4.0)이었고 그중 다섯이
        // 어떤 규칙에서도 유도되지 않았다. 장비 카드 42종은 `Frame.StrokeFraction` 하나에 **연속 배수**를
        // 곱해 등급을 만든다(EQUIPMENT_HANDOFF_PORT_SPEC §13-2-3의 strokeGrade 0/2/3). 같은 앱 안에서
        // 문법이 둘로 갈리지 않도록 **같은 배수**를 여기에도 둔다: ×0.75 / ×1.00 / ×1.50.
        //
        // ★ 이 셋 <b>밖의 값을 쓰지 마라.</b> 하한이 ×0.75인 이유는 광학이다 —
        //   `UiChrome.EdgeFeather`(0.5pt/변)가 획 코어 바깥에 알파 램프를 붙이므로 1.5pt 획은 그려지는
        //   폭 2.5pt 중 **38.5 %가 램프**다. 그 아래로 내려가면 1×(Windows 100 %)에서 획이 회색 얼룩이
        //   된다(옛 체크리스트의 1.0pt 박스가 정확히 그 사고였다).
        // ★ g2(×1.50)는 **글리프당 최대 한 조각**이다. 굵은 획은 "여기가 이 물건의 위계 꼭대기"라는
        //   뜻이라 둘 이상이면 뜻이 사라진다(옛 스톱워치는 가장 덜 중요한 부속인 용두가 4.0pt = 2W로
        //   메뉴에서 가장 굵은 잉크였다 — 위계가 거꾸로였다).

        /// <summary>부채꼴 심볼의 <b>펜 하나</b>(g0 ×1.00). 다섯 글리프의 기본 획이다.</summary>
        private const float SymbolStroke = 2.0f;

        /// <summary>FG-2 g1 = ×0.75. <b>종속 부속</b>(체크리스트의 빈 표식 상자)에만 쓴다 —
        /// 「이것은 글줄보다 한 단 낮은 물건」을 두께 하나로 말한다.</summary>
        private const float SymbolStrokeDetail = 0.75f * SymbolStroke;

        /// <summary>FG-2 g2 = ×1.50. <b>글리프당 한 조각</b>만 허용(스톱워치의 용두 단추).</summary>
        private const float SymbolStrokeHeavy = 1.5f * SymbolStroke;

        /// <summary>심볼 레이아웃 상자(pt). <b>경계가 아니라 좌표계</b>다 — 잉크가 이 사각형을 넘는 것은
        /// 결함이 아니고, 실제 경계는 FG-1 광학 필드(원판 중심에서 r ≤ 14.0pt, 단일 돌출만 15.0pt)다.
        /// 조각이 원형 버튼 안에 앉으므로 판정도 원(반경)으로 해야 한다.</summary>
        private const float SymbolBoxPoints = 24f;

        /// <summary>FG-1 광학 필드 반경(pt) — 잉크가 넘지 않아야 하는 선. 유도: `ART_FAN_MENU_LANGUAGE`
        /// F-1 링의 안쪽 가장자리가 r = 20이고 거기서 <b>3W = 6.0pt</b>를 비운다.</summary>
        private const float SymbolFieldRadiusPoints = 14f;

        /// <summary>
        /// 네 진입점의 이름. 두 곳에서 쓴다: <b>호버 이름표</b>(커서가 올라간 하나만)와 <b>로그/접힘
        /// 사유</b>("[오늘 할일] 재클릭" 같은 진단 줄에서 인덱스 숫자만 남으면 로그를 읽을 수 없다).
        ///
        /// 두 용도가 같은 배열을 쓰는 이유: 로그와 화면이 다른 이름을 부르면 사용자 신고("행동 버튼이
        /// 안 눌려요")를 로그에서 찾을 수 없다. 이름은 한 곳에서만 정의한다.
        /// </summary>
        private static readonly string[] ButtonNames = { "집중 모드", "캐릭터", "오늘 할일", "행동", "앱 종료" };

        /// <summary>[앱 종료]가 <b>무장</b>됐을 때 이름표 알약에 적히는 글자.
        /// <para>★ 새 문자열이 아니다 — 행동창 푸터의 [앱 종료]가 쓰던 확인 문구를 <b>그대로</b> 옮겨
        /// 왔다(그 칩은 같은 라운드에 삭제됐다). 되돌릴 수 없는 행동의 확인 문구가 앱 안에서 두 벌이
        /// 되지 않게 한다. 최종 문구 확정은 <c>design-narrative</c> 소관이다.</para>
        /// <para>왜 원 안에 안 적는가: Ø<see cref="ButtonDiameterPoints"/> 안에 6글자가 안 들어간다
        /// (FontCaption 10pt 기준 폭 약 62pt &gt; 44pt). 그래서 <b>글자는 이미 있는 호버 이름표 알약에,
        /// 카운트다운은 이미 있는 링에</b> 싣는다 — 새 부품을 만들지 않는다(53-4).</para></summary>
        public const string QuitArmedLabel = "정말 종료?";

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

        // ★ 2026-09-05 — 진입점이 둘이 되면서 생긴 상태 넷. 전부 「어느 문이 열었는가」와 「재앵커 예약」이다.
        private GearMenuAnchorSource _anchorSource = GearMenuAnchorSource.Gear;
        private float _fanUpBiasPoints;              // 기준각에만 들어간다. 톱니 0 / 캐릭터 FanUpBiasPoints.
        private bool _pendingReanchor;
        private Vector2 _pendingAnchorScreen;
        private GearMenuAnchorSource _pendingAnchorSource;
        private float _pendingUpBiasPoints;

        // ---- 배치 결과에서 파생되는 것들(이름표 기하가 이 셋만 본다) ----
        //
        // ★★ 2026-09-03 — <b>여기가 없어서 이름표가 [앱 종료]를 5.93pt 덮고 있었다.</b>
        //   배치 사다리의 ③단계(평행이동)는 <b>버튼만</b> 옮기고 <see cref="_gearCenterPoints"/>는
        //   그대로 둔다. 그런데 이름표는 방향을 <c>버튼중심 − 기어중심</c>으로 잡았으므로,
        //   평행이동이 걸린 순간 그 방향이 <b>진짜 반지름 방향이 아니게</b> 됐다.
        //   기본 톱니 위치는 평행이동 (−6, −36.2)가 걸리는 자리라 <b>출하 기본 화면이 그 상태였다.</b>
        private Vector2 _layoutShiftPoints;
        private bool _columnLayout;
        private float _labelRingRadiusPoints;
        private Vector2 _screenPointsAtLayout;
        // 호버 이름표 — 인스턴스 하나뿐이다(클래스 문서 ②).
        private RectTransform _hoverLabel;
        private Image _hoverLabelSurface;
        private Image _hoverLabelBorder;
        private Text _hoverLabelText;
        private float _hoverLabelAlpha;
        private int _hoverLabelIndex = -1;      // 지금 알약에 적혀 있는 이름의 버튼(-1 = 없음).
        private readonly float[] _nameWidths = new float[ButtonCount];
        private float _quitArmedLabelWidth;

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

        // ---- [앱 종료]의 2단 확인(53-4) ----
        //
        // ★ <b>새 관용구를 만들지 않았다.</b> 시간은 ActionCommandPopover.QuitConfirmSeconds(3초)를
        //   <b>참조</b>하고(TodoBoardPopover의 삭제 확인과도 같은 값), 글자는 이미 있는 호버 이름표
        //   알약에, 카운트다운은 이미 있는 링(ButtonView.RingFill)에 싣는다.
        private bool _quitArmed;
        private float _quitArmTimer;

        // ==================== 공개 상태 ====================

        /// <summary>펼쳐져 있는가(펼치는 중 + 팝오버 앵커 상태 포함). 클릭을 받는 상태의 기준.</summary>
        public bool IsExpanded => _phase == Phase.Expanding || _phase == Phase.Open;

        /// <summary>그림이 화면에 남아 있는가(접히는 중 포함).</summary>
        public bool IsVisible => _phase != Phase.Hidden;

        /// <summary>팝오버를 띄운 채 남아 있는 버튼(-1 = 없음).</summary>
        public int AnchoredButton => _activeIndex;

        /// <summary>버튼 전부의 <b>클램프 상자</b>를 덮는 사각형(Unity 스크린 픽셀). 톱니가 클릭관통
        /// 차단 콜라이더를 이만큼 넓혀야 버튼 클릭이 밑의 앱으로 새지 않는다.</summary>
        public Rect UnionScreenRect { get; private set; }

        /// <summary>지금 부채꼴이 쓰는 기준각(도). 회귀 테스트가 45° 스냅을 직접 확인한다.</summary>
        public float BaseAngleDegrees => _baseAngleDegrees;

        /// <summary>버튼들이 <b>실제로</b> 그 둘레에 배열된 점(캔버스 포인트) =
        /// 기어 중심 + 배치 사다리가 적용한 평행이동. <b>기어 중심과 다를 수 있다.</b>
        /// <para>이름표 방향은 반드시 이 점에서 파생해야 한다 — 기어 중심에서 재면 평행이동이 걸린
        /// 배치에서 방향이 틀어지고, 그게 2026-09-03 이름표 겹침의 원인이었다.</para></summary>
        public Vector2 FanOriginPoints => _gearCenterPoints + _layoutShiftPoints;

        /// <summary>지금 떠 있는 부채꼴을 <b>어느 문이 열었는가</b>(§5-7). 접혀 있으면 마지막으로 연 문.</summary>
        public GearMenuAnchorSource AnchorSource => _anchorSource;

        /// <summary>기준각에 들어간 위쪽 바이어스(pt) — 톱니 0 / 캐릭터 <see cref="FanUpBiasPoints"/>.
        /// 테스트가 «어느 식이 실제로 돌았는가»를 값으로 확인한다.</summary>
        public float ActiveUpBiasPoints => _fanUpBiasPoints;

        /// <summary>접힘이 끝나는 프레임에 다른 앵커로 다시 펼 예정인가(재앵커 진행 중).</summary>
        public bool IsReanchorPending => _pendingReanchor;

        /// <summary>세로 일렬 폴백으로 떨어졌는가(진단/테스트 창구).</summary>
        public bool IsColumnFallback => _columnLayout;

        /// <summary>이름표 알약의 <b>안쪽 모서리</b>가 놓이는 반지름(<see cref="FanOriginPoints"/> 기준).
        /// = 가장 바깥 버튼까지의 거리 + 버튼 반지름 + <see cref="HoverLabelGapPoints"/>.</summary>
        public float HoverLabelRingRadiusPoints => _labelRingRadiusPoints;

        /// <summary>호버 이름표에 지금 보이는 글자(안 보이면 빈 문자열) — "선택된 것만 이름이 보인다"를
        /// 회귀 테스트가 직접 확인한다.</summary>
        public string VisibleHoverLabel
            => _hoverLabel != null && _hoverLabelAlpha > 0.5f && _hoverLabelText != null
                ? _hoverLabelText.text : string.Empty;

        /// <summary>[앱 종료]가 1차 클릭을 받아 <b>"정말 종료?"</b> 상태인가(53-4).</summary>
        public bool IsQuitArmed => _quitArmed;

        /// <summary>무장이 저절로 풀리기까지 남은 시간(초). 무장 중이 아니면 0.
        /// <para>카운트다운 링의 <c>fillAmount</c>가 <b>이 값에서만</b> 파생된다 — 화면과 실제가
        /// 갈라질 자리를 만들지 않는다(원칙 1의 UI판).</para></summary>
        public float QuitArmRemainingSeconds
            => _quitArmed ? Mathf.Max(0f, ActionCommandPopover.QuitConfirmSeconds - _quitArmTimer) : 0f;

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

        /// <summary>배치 사다리가 <b>정한</b> 버튼 지름(포인트) — 축소 폴백이 걸렸는지 알 수 있다.
        /// <para>★ 이것은 <b>판정값</b>이다. 화면에 실제로 그려진 지름은
        /// <see cref="RenderedButtonDiameterPoints"/>가 <b>씬 오브젝트에서 직접</b> 잰다 —
        /// 둘을 갈라 둔 이유는 그 문서에 있다.</para></summary>
        public float ButtonDiameter => _diameterPoints;

        /// <summary>
        /// 버튼 <paramref name="index"/>가 <b>실제로 그려지는</b> 원의 지름(포인트).
        ///
        /// <para><b>왜 <see cref="ButtonDiameter"/>와 따로 있는가</b>: 2026-09-06 이전에는 판정이 Ø36을
        /// 요구해도 화면은 <b>언제나 Ø44</b>였다(<see cref="ApplyLayoutDiameterToViews"/> 문서). 그런데
        /// 그때도 <c>ButtonDiameter</c>는 36을 정직하게 돌려줬기 때문에, <b>판정값만 보는 어떤 테스트도
        /// 이 사고를 볼 수 없었다.</b> 그래서 이 접근자는 <c>_diameterPoints</c>를 <b>쳐다보지 않고</b>
        /// 구워진 원의 상자와 실제로 걸린 배치 배율을 씬에서 다시 잰다 — 두 값을 <b>다른 방법으로</b>
        /// 재야 대조가 대조가 된다.</para>
        ///
        /// <para>애니메이션 배율(펼침/호버)은 <see cref="ButtonView.Root"/>에 걸리므로 여기에 섞이지
        /// 않는다 — 이 값은 <b>안착 상태의</b> 지름이고, 펼치는 도중에 재도 같은 값이 나온다.</para>
        ///
        /// <para><b>왜 <see cref="ButtonView.Surface"/>를 재는가</b>: 사용자가 실제로 보는 물건이
        /// 그것이기 때문이다. <c>Root.sizeDelta</c>는 원들이 <b>늘어붙지 않는</b> 컨테이너라 값이
        /// 조용히 낡아도 화면은 멀쩡하다 — 그런 값을 «렌더값»이라 부르면 이 접근자가 두 번째
        /// 거짓 측정기가 된다. 면의 상자는 <see cref="UiChrome.AddCircle"/> 규약대로
        /// «지름 + 램프 여유 ×2»라 그 여유를 되빼면 지름이 나온다.</para>
        ///
        /// <para>아직 <see cref="BuildUi"/> 전이거나 범위 밖이면 0 — 형제 접근자
        /// <see cref="ButtonScreenCenter"/>/<see cref="ButtonProgress"/>와 같은 가드 규약이고,
        /// 0은 어떤 «Ø44/Ø36과 같다» 단언도 통과시키지 않아 조용히 넘어가지 않는다.</para>
        /// </summary>
        public float RenderedButtonDiameterPoints(int index)
        {
            if (index < 0 || index >= ButtonCount) return 0f;
            ButtonView b = _buttons[index];
            if (b?.Group == null || b.Surface == null) return 0f;

            float bakedDiameter = b.Surface.rectTransform.sizeDelta.x - UiChrome.EdgeFeatherPoints * 2f;
            return bakedDiameter * b.Group.localScale.x;
        }

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

        /// <summary>버튼 <b>중심</b> 사이의 최소 거리(포인트) — 겹침 회귀 테스트용.
        /// <para>★ <b>전 쌍</b>을 잰다. 다섯이 한 호에 등간격으로 서므로(2026-09-06 위성 폐지)
        /// 최소값은 언제나 <b>이웃끼리</b>의 <c>2·R·sin(간격/2)</c> = 57.75pt다.</para>
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
            Debug.Log("[부채꼴] 준비 완료 — 톱니를 짧게 클릭하면 [집중 모드]/[캐릭터]/[오늘 할일]/[행동]" +
                $"/[{NameOf((int)GearMenuButton.Quit)}] " +
                // ★ 스팬은 «(개수 − 1) × 간격»으로 센다. 상수를 베껴 적으면 기하가 바뀌었을 때
                //   로그만 옛 숫자를 말하는 상태가 된다 — 원격 진단이 틀린 결론에 도달하는 거짓말이다.
                $"**아이콘 전용** 원버튼 {ButtonCount}개가 <b>한 호 위에</b> " +
                $"Ø{ButtonDiameterPoints:F0}pt, 궤도 {OrbitRadiusPoints:F0}pt, 간격 " +
                $"{ButtonAngleStepDegrees:0.##}도(스팬 " +
                $"{ButtonAngleStepDegrees * (ButtonCount - 1):F0}도, 이웃 중심거리 " +
                $"{2f * OrbitRadiusPoints * Mathf.Sin(ButtonAngleStepDegrees * 0.5f * Mathf.Deg2Rad):F2}pt)로 " +
                $"{ExpandTotalSeconds:F2}초 동안 " +
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
        ///
        /// <para>★ 2026-09-05 — 인자가 둘 늘었지만 <b>기존 한 인자 호출부는 그대로 컴파일된다</b>
        /// (기본값 = 톱니 경로). 테스트 설계 C-2(«<c>Expand(Vector2)</c> 형태 유지»)가 요구한 형태다.</para>
        ///
        /// <para>★ <b>첫 줄이 <see cref="FramePacing.HoldActiveForInteraction"/>인 이유</b>(리더 판정 L-10):
        /// 정지 등급(Still)에서는 렌더가 15fps라 0.300초 펼침이 <b>5프레임</b>으로 뭉개진다.
        /// <see cref="LateUpdate"/>의 홀드는 <b>다음</b> 프레임에야 걸리므로 최악 66.7ms(예산의 22.2%)를
        /// 잃는다. 여는 그 자리에서 한 번 더 부르면 그 한 프레임이 사라진다 — 홀드는 만료 시각 방식이라
        /// 중복 호출이 누수를 만들지 않는다.</para>
        /// </summary>
        /// <param name="upBiasPoints">기준각 계산에만 쓰이는 위쪽 바이어스. 톱니는 0,
        /// 캐릭터 경로는 <see cref="FanUpBiasPoints"/>(§1-3-4의 «유일한 함정»).</param>
        public void Expand(Vector2 gearCenterUnityScreen,
            GearMenuAnchorSource source = GearMenuAnchorSource.Gear, float upBiasPoints = 0f)
        {
            if (IsExpanded) return;

            FramePacing.HoldActiveForInteraction();

            _pendingReanchor = false;
            _anchorSource = source;
            _fanUpBiasPoints = upBiasPoints;
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
            LogExpandGeometry();
        }

        /// <summary>
        /// ★ 2026-09-06 진단 로그 — <b>펼침 한 번당 한 줄</b>. 매 프레임이 아니므로 문자열 보간이
        /// 24시간 상주 비용이 되지 않는다(<c>Start()</c>의 기동 배너와 같은 성격).
        ///
        /// <para><b>왜 이 여섯 값인가</b>: 「부채꼴이 이상한 자리에 떴다」는 신고가 올라오면 원격에서
        /// 물어야 하는 것이 정확히 이것들이다 — <b>어느 문으로 열었나</b>(톱니/캐릭터는 위쪽 바이어스가
        /// 달라 θ₀ 자체가 다르다), <b>앵커가 어디였나</b>, <b>사다리의 어느 칸에서 멈췄나</b>
        /// (θ₀ 회전량 · 지름 축소 · 평행이동 · 세로 일렬). 이 중 하나라도 없으면 로그를 보고도
        /// 「사다리가 정상 동작한 결과」와 「기하가 틀어진 결과」를 가를 수 없다.</para>
        /// </summary>
        private void LogExpandGeometry()
        {
            float rotation = Mathf.DeltaAngle(
                SnapFanBaseAngle(_gearCenterPoints, _screenPointsAtLayout, _fanUpBiasPoints), _baseAngleDegrees);
            Debug.Log($"[부채꼴] 펼침 — 진입점={(_anchorSource == GearMenuAnchorSource.Character ? "캐릭터 우클릭" : "톱니")}" +
                $"(위쪽 바이어스 {_fanUpBiasPoints:F0}pt) · 앵커=({_gearCenterPoints.x:F1}, {_gearCenterPoints.y:F1})pt " +
                $"/ 화면 {_screenPointsAtLayout.x:F0}x{_screenPointsAtLayout.y:F0}pt · θ₀={_baseAngleDegrees:F0}도" +
                $"(스냅값에서 {rotation:+0;-0;0}도 회전) · 지름={_diameterPoints:F0}pt" +
                $"{(_diameterPoints < ButtonDiameterPoints ? "(축소 폴백)" : "")} · " +
                $"평행이동=({_layoutShiftPoints.x:F1}, {_layoutShiftPoints.y:F1})pt" +
                $"[{_layoutShiftPoints.magnitude:F1}/{MaxGroupShiftPoints:F0}] · " +
                $"세로일렬 폴백={(_columnLayout ? "예" : "아니오")} · 여백 상{EffectiveTopMarginPoints:F0}" +
                $"/하{EffectiveBottomMarginPoints:F0}/좌{EffectiveLeftMarginPoints:F0}" +
                $"/우{EffectiveRightMarginPoints:F0}pt.");
        }

        /// <summary>
        /// ★ <b>진입점이 둘이라서 필요한 단 하나의 새 동작</b> — docs/UX_RIGHTCLICK_FAN_MENU.md §5-7 /
        /// UX_MOTION_FAN_AND_CAPE.md §1-7 (D).
        ///
        /// <list type="bullet">
        /// <item>접혀 있으면 → 그냥 편다.</item>
        /// <item><b>다른 문</b>이 눌렸으면 → <see cref="GearMenuCollapseMode.Drag"/>(0.08초)로 접고,
        ///   접힘이 끝나는 <b>그 프레임에</b> 새 앵커로 다시 편다. 총 0.080 + 0.300 = <b>0.380초</b>.</item>
        /// <item><b>같은 문</b>이면 → 아무것도 하지 않고 <c>false</c>를 돌려준다. 토글로 닫을지는
        ///   호출자가 정한다(톱니는 «떠 있는 표면을 닫는다», 우클릭도 같다 — §3-1).</item>
        /// </list>
        ///
        /// <para>★ <b>왜 순간이동시키지 않는가</b>: 부채가 화면을 튄다. 그리고 0.13/0.08초의 접힘은
        /// 감춰야 할 비용이 아니라 «옮겨 갔다»를 눈으로 알려 주는 신호다(§5-7).</para>
        ///
        /// <para>★ <b>깜빡임 금지</b>: 접힘 완료와 재펼침이 <b>같은 프레임</b>에 일어나므로
        /// (<see cref="LateUpdate"/>의 <c>Phase.Collapsing</c> 분기) 프레임 경계에서 관측하는 쪽은
        /// <see cref="IsVisible"/>가 거짓이 되는 순간을 <b>한 번도 보지 못한다</b>.</para>
        /// </summary>
        /// <returns>이번 호출이 «열기 또는 재앵커»를 실제로 시작했는가. <c>false</c>면 같은 문의 재입력이다.</returns>
        public bool ExpandOrReanchor(Vector2 anchorUnityScreen, GearMenuAnchorSource source,
            float upBiasPoints, string reason)
        {
            if (!IsVisible)
            {
                Expand(anchorUnityScreen, source, upBiasPoints);
                return true;
            }

            if (_anchorSource == source && !_pendingReanchor) return false;

            FramePacing.HoldActiveForInteraction();
            _pendingReanchor = true;
            _pendingAnchorScreen = anchorUnityScreen;
            _pendingAnchorSource = source;
            _pendingUpBiasPoints = upBiasPoints;

            // 이미 접히는 중이면 다시 접지 않는다 — 타이머를 되감으면 0.380초 예산이 늘어난다.
            if (_phase != Phase.Collapsing) Collapse(GearMenuCollapseMode.Drag, reason);
            return true;
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
            // 해제 조건 ③ — 부채꼴이 접히면 무장은 화면 밖으로 살아 나가지 않는다(53-4 / 53-7).
            DisarmQuit($"부채꼴 접힘({reason})");
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
            GearMenuCollapseMode.Drag => "앵커 이동",
            GearMenuCollapseMode.Auto => "무반응 자동",
            _ => "사용자 동작",
        };

        /// <summary>진입점 이름(로그 전용). <b>정상값을 <c>default:</c>로 흘리지 않는다</b> —
        /// 모든 멤버를 명시하고, 여기 오는 것은 «배선이 빠진 새 멤버»뿐이라 그때만 시끄럽게 드러난다.</summary>
        public static string AnchorSourceLabel(GearMenuAnchorSource source)
        {
            switch (source)
            {
                case GearMenuAnchorSource.Gear: return "톱니";
                case GearMenuAnchorSource.Character: return "캐릭터 우클릭";
                default:
                    Debug.LogWarning($"[부채꼴] 이름 없는 진입점({(int)source})이 부채꼴을 열었습니다 — " +
                        $"{nameof(GearMenuAnchorSource)}에 멤버를 늘렸다면 {nameof(AnchorSourceLabel)}도 함께 고치세요.");
                    return $"미상({(int)source})";
            }
        }

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
                case GearMenuButton.Quit:
                    ActivateQuit();
                    return;
                case GearMenuButton.Todo:
                    AnchorPopover(index, OpenTodoPopover());
                    return;
                default:
                    // ★ 2026-09-03 — 예전에는 [오늘 할일]이 이 <c>default:</c>로 흘렀다. 그래서 슬롯을
                    //   하나 더 늘리면 <b>그 새 버튼이 아무 소리 없이 할일 창을 열었다</b>.
                    //   이제 정상값은 전부 위에서 잡히므로 여기 오는 것은 <b>진짜로 배선이 빠진 경우</b>뿐이다
                    //   — 정상 사용자에게 거짓 경보가 찍히지 않으면서 누락은 시끄럽게 드러난다.
                    Debug.LogError($"[부채꼴] 슬롯 {index}({NameOf(index)})에 동작이 배선되지 않았습니다 — " +
                        "GearMenuButton에 값을 더했다면 Activate의 switch에도 그 가지를 더해야 합니다. " +
                        "지금 이 버튼은 눌러도 아무 일도 하지 않습니다.");
                    return;
            }
        }

        /// <summary>
        /// ★ [앱 종료]의 2단 확인(53-4). <b>1차 클릭은 아무것도 끝내지 않는다</b> — 무장만 한다.
        ///
        /// <para><b>왜 팝오버를 안 여는가</b>: 다른 셋과 달리 이 버튼은 <b>창을 여는 진입점이 아니라
        /// 그 자체가 행동</b>이다. 창을 하나 더 만들면 되돌릴 수 없는 행동의 확인 구현이 앱 안에서
        /// <b>세 벌</b>이 된다(설정창 · 할일 삭제 · 여기).</para>
        ///
        /// <para><b>미스가 안전한 쪽으로 실패한다</b>: 정지 등급 15fps에서 아주 짧은 클릭이 폴링을
        /// 스쳐 지나가면 <b>아무 일도 일어나지 않는다</b>(종료가 아니라 <b>무장 실패</b>).
        /// 되돌릴 수 없는 쪽으로 실패하는 경로는 없다.</para>
        /// </summary>
        private void ActivateQuit()
        {
            if (!_quitArmed)
            {
                _quitArmed = true;
                _quitArmTimer = 0f;
                Debug.Log($"[부채꼴] [{ButtonNames[(int)GearMenuButton.Quit]}] 1차 클릭 — " +
                    $"{ActionCommandPopover.QuitConfirmSeconds:F0}초 안에 다시 누르면 종료합니다" +
                    "(다른 버튼으로 커서를 옮기거나 부채꼴이 접히면 그냥 풀립니다).");
                return;
            }

            Debug.Log("[부채꼴] [앱 종료] 확정 — Application.Quit()을 호출합니다. " +
                "에디터에서는 재생 모드만 멈춥니다.");
            DisarmQuit("확정");
            AppControlDirector.QuitApplication("부채꼴 [앱 종료]");
        }

        /// <summary>무장을 푼다. <b>해제 조건은 넷</b>이고 전부 여기로 모인다(53-4):
        /// ① 3초 경과 ② 커서가 <b>다른 버튼</b>으로 옮겨감 ③ 부채꼴이 접힘 ④ 앱이 숨김으로 전환.
        /// <para>★ ②가 라벨 경쟁을 <b>구조적으로</b> 없앤다 — 무장 중에는 다른 버튼에 커서가 있을 수
        /// 없으므로 "무장 알약과 호버 알약 중 누가 이기는가"라는 질문 자체가 성립하지 않는다
        /// (알약 인스턴스는 하나다).</para></summary>
        private void DisarmQuit(string reason)
        {
            if (!_quitArmed) return;
            _quitArmed = false;
            _quitArmTimer = 0f;
            ButtonView quit = _buttons[(int)GearMenuButton.Quit];
            if (quit?.RingFill != null && quit.RingFill.gameObject.activeSelf)
            {
                quit.RingFill.gameObject.SetActive(false);
            }
            Debug.Log($"[부채꼴] [앱 종료] 무장 해제 — {reason}.");
        }

        /// <summary>무장 시계. <b>매 프레임</b> 돈다 — 최대 3초짜리라 초 단위로 그리면 3칸짜리 계단이
        /// 된다(집중 모드 링이 초 단위인 것은 25분짜리라서다).
        /// <para>★ <b>여기서 <c>FramePacing.HoldActiveForInteraction()</c>을 부르지 않는다.</b>
        /// 부채꼴이 열려 있는 동안은 <see cref="LateUpdate"/>의 홀드 한 줄이 이미 덮고 있고,
        /// 정지 등급 15fps에서도 3초 = <b>45단계(2.2%/단계)</b>라 카운트다운은 충분히 읽힌다.
        /// 상주 앱이 3초 동안 60fps를 <b>추가로</b> 잡아둘 근거가 없다(원칙 2).</para></summary>
        private void TickQuitArm(float dt)
        {
            if (!_quitArmed) return;

            _quitArmTimer += dt;
            if (_quitArmTimer >= ActionCommandPopover.QuitConfirmSeconds)
            {
                DisarmQuit($"{ActionCommandPopover.QuitConfirmSeconds:F0}초 경과");
                return;
            }

            // 해제 조건 ② — 커서가 다른 버튼 위로 갔다(마음이 바뀐 신호).
            if (_hoverIndex >= 0 && _hoverIndex != (int)GearMenuButton.Quit)
            {
                DisarmQuit($"커서가 [{ButtonNames[_hoverIndex]}]로 옮겨감");
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

            // ★★★ 2026-09-05 (game-architect I-28) — <b>임대 갱신은 각 표면이 자기 생존을 보고한다.</b>
            //   여기 오기 전까지 이 보고를 <b>톱니가 대신</b>했고(InfoGearIconWidget), 그 코드는 톱니의
            //   가시성 게이트 <b>뒤</b>에 있었다. 「톱니가 안 보이는 상태 + 등급 1」이 겹치면 아무도
            //   갱신하지 않아 <b>사용자가 쓰고 있는 도중에 부채꼴이 스스로 걷혔다</b>.
            //   ★ 2026-09-05에 톱니가 「평상시 숨김」이 되면서 그 조합이 <b>평상시</b>가 됐다 —
            //     즉 이 한 줄이 없으면 등급 1에서 우클릭 부채꼴이 임대 만료로 사라진다.
            //   갱신은 <b>만료된 임대를 되살리지 않는다</b>(UserSurfaceSummonPolicy) — 위 게이트를
            //   통과했다는 것이 곧 «지금 이 표면이 정당하게 떠 있다»는 뜻이라 안전하다.
            //   ※ 정보창(CharacterInfoWindow)은 아직 자기 갱신이 없다 — 그쪽은 별도 배정이다.
            if (_agent != null) _agent.RenewUserSummonGrant();

            float dt = Time.unscaledDeltaTime;
            _timer += dt;

            switch (_phase)
            {
                case Phase.Expanding:
                    if (_timer >= ExpandTotalSeconds) _phase = Phase.Open;
                    break;
                case Phase.Collapsing:
                    if (_timer >= CollapseSecondsFor(_collapseMode))
                    {
                        // ★ 재앵커(§5-7)는 <b>같은 프레임 안에서</b> 접힘을 끝내고 다시 편다.
                        //   Hide()와 Expand() 사이에 프레임 경계가 없으므로 밖에서 보는 IsVisible은
                        //   한 번도 거짓이 되지 않는다 — 「깜빡임 금지」가 구조로 지켜진다.
                        if (_pendingReanchor)
                        {
                            Vector2 anchor = _pendingAnchorScreen;
                            GearMenuAnchorSource source = _pendingAnchorSource;
                            float bias = _pendingUpBiasPoints;
                            Hide();
                            Expand(anchor, source, bias);
                            Debug.Log($"[부채꼴] 재앵커 — {AnchorSourceLabel(source)} 자리에서 다시 펼칩니다.");
                            return;
                        }
                        Hide();
                        return;
                    }
                    break;
            }

            TickQuitArm(dt);
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
        /// <see cref="ExpandTotalSeconds"/>(0.300) + 6.0 = <b>6.30초</b>여서 여유가 <b>1.04배</b>였다
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
            // 해제 조건 ④ — 전체화면 감지로 즉시 거둘 때도 무장이 남지 않는다. Collapse를 거치지 않는
            // 유일한 경로가 여기라, 이 한 줄이 없으면 다음에 열었을 때 <b>이미 장전된 채</b> 뜬다.
            DisarmQuit("부채꼴 숨김");
            // ★ 재앵커 예약은 숨는 순간 버린다. 재앵커 경로는 이 함수를 부르기 <b>전에</b> 값을
            //   지역변수로 떠 두므로 영향이 없고, 반대로 전체화면 감지 같은 다른 Hide 경로에서
            //   예약이 살아남으면 «다음에 여는 순간 엉뚱한 자리로 튄다».
            _pendingReanchor = false;
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

            // 링의 켜짐/꺼짐이 먼저 정해져야 아래 ApplyButtonStyle의 activeSelf 검사가 같은 프레임을 본다.
            ApplyQuitCountdown();

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

                // ★ 2026-09-05 design-motion §1-12(나) — 펼침/수축 원점은 톱니 중심이 아니라 부채 원점(평행이동 포함)이다.
                //   이름표·호버 이등분선은 이미 FanOriginPoints 를 보는데 이 애니메이션만 옛 원점을 써서 가장자리 배치에서 최대 31.2pt 어긋났다.
                Vector2 center = FanOriginPoints + (b.CenterPoints - FanOriginPoints) * radiusFactor;
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
        /// 무장 카운트다운 링 — <b>남은 시간에서만</b> 파생된다(원칙 1의 UI판).
        ///
        /// <para><b>매 프레임</b> 갱신한다. 집중 모드 링은 "초 단위로 바뀐 프레임에만" 쓰지만 그건
        /// 25분짜리라서고, 이건 <b>최대 3초</b>다 — 초 단위로 그리면 3칸짜리 계단이 된다.
        /// 비용 상한은 3초 × 프레임률이고, 그 3초 동안 프레임을 <b>추가로</b> 붙잡지도 않는다
        /// (<see cref="TickQuitArm"/> 문서).</para>
        /// </summary>
        private void ApplyQuitCountdown()
        {
            ButtonView quit = _buttons[(int)GearMenuButton.Quit];
            if (quit?.RingFill == null) return;

            if (quit.RingFill.gameObject.activeSelf != _quitArmed)
            {
                quit.RingFill.gameObject.SetActive(_quitArmed);
            }
            if (!_quitArmed) return;

            quit.RingFill.fillAmount = Mathf.Clamp01(
                QuitArmRemainingSeconds / Mathf.Max(0.0001f, ActionCommandPopover.QuitConfirmSeconds));
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

            // ★ 무장 중에는 알약이 <b>[앱 종료]에 고정</b>되고 확인 문구를 문다(53-4). 커서가 다른 버튼으로
            //   가면 그 순간 무장이 풀리므로(TickQuitArm) 두 문구가 경쟁하는 상태는 <b>존재할 수 없다</b> —
            //   알약 인스턴스가 하나뿐이라는 클래스 문서 ②의 성질이 그대로 유지된다.
            int target = _quitArmed ? (int)GearMenuButton.Quit : _hoverIndex;
            if (target >= 0 && (target == _activeIndex || _buttons[target] == null
                || _buttons[target].CollapsingNow || _buttons[target].Progress < MinClickableProgress))
            {
                target = -1;
            }

            float dt = Time.unscaledDeltaTime;
            _hoverLabelAlpha = Mathf.MoveTowards(_hoverLabelAlpha, target >= 0 ? 1f : 0f,
                dt / Mathf.Max(0.0001f, HoverLabelFadeSeconds));

            // 글자는 <b>값이 바뀐 프레임에만</b> 쓴다(Text.text 대입은 메시 재생성이다).
            // ★ 비교 대상이 인덱스가 아니라 <b>문자열</b>인 이유: 같은 버튼([앱 종료])에서 이름 -> 확인 문구로
            //   글자만 바뀌는 전이가 생겼다. 인덱스만 보면 그 전이를 놓쳐 알약이 "앱 종료"인 채로 남는다.
            if (target >= 0)
            {
                string wanted = _quitArmed ? QuitArmedLabel : ButtonNames[target];
                if (!string.Equals(_hoverLabelText.text, wanted, System.StringComparison.Ordinal))
                {
                    _hoverLabelText.text = wanted;
                    _hoverLabel.sizeDelta = new Vector2(
                        _quitArmed ? _quitArmedLabelWidth : _nameWidths[target], HoverLabelHeightPoints);
                }
                _hoverLabelIndex = target;
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
            // 적용된다 — 버튼 중심의 평균을 그대로 쓰면 기준점이 호 <b>안쪽</b>이라 보장이 약해진다.
            // ★ reach가 <b>최대 투영</b>이라 어느 슬롯이 가장 바깥이든 자동으로 기준이 된다 — 안내
            //   알약은 언제나 부채꼴 <b>바깥</b>에 선다. 위성 도입에도, 2026-09-06 위성 폐지에도
            //   이 블록이 한 줄도 안 바뀐 이유가 그것이다.
            // ★ 2026-09-03 — 기준을 <see cref="FanOriginPoints"/>로 옮겼다. 평행이동이 걸린 배치에서
            //   기어 중심으로 재면 이등분 방향이 실제 대칭축에서 벗어난다(이름표와 같은 결함).
            Vector2 origin = FanOriginPoints;
            Vector2 middle = Vector2.zero;
            for (int i = 0; i < ButtonCount; i++) middle += _buttons[i].CenterPoints;
            middle = middle / ButtonCount - origin;
            if (middle.sqrMagnitude < 1e-4f) middle = Vector2.down;
            middle.Normalize();

            float reach = 0f;
            for (int i = 0; i < ButtonCount; i++)
                reach = Mathf.Max(reach, Vector2.Dot(_buttons[i].CenterPoints - origin, middle));

            _onboardingHint.anchoredPosition =
                ResolveHoverLabelCenter(origin + middle * reach, _onboardingHintWidth);
            _onboardingHintSurface.color = Fade(UiChrome.PanelSurface, _onboardingHintAlpha);
            _onboardingHintBorder.color =
                Fade(UiChrome.Flatten(UiChrome.AccentBorder, UiChrome.PanelSurface), _onboardingHintAlpha);
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
        /// <summary>
        /// ★★ 이름표 알약의 중심 — <b>public static 순수 함수</b>다(<see cref="SlotCenterPoints"/>와 같은 이유).
        ///
        /// <para><b>2026-09-03 — 여기가 두 가지로 틀려 있었다.</b> 러너 실패:
        /// <c>[오늘 할일] 이름표가 [앱 종료]를 5.9px 덮습니다 (16.07 &lt; 22.0)</c>.
        /// <b>세로 일렬 폴백이 아니라 정상 부채꼴에서</b>, 그리고 <b>기본 톱니 위치에서</b> 났다
        /// (실측 스윕: 640×480부터 2560×1440까지 전 해상도 동일하게 −5.93pt, 기어 위치 격자의
        /// <b>61~71%</b>가 위반). 원인 둘:</para>
        /// <list type="number">
        ///   <item><b>방향이 틀렸다.</b> <c>버튼중심 − 기어중심</c>으로 쟀는데, 배치 사다리 ③단계
        ///     (평행이동)는 <b>버튼만</b> 옮긴다. 기본 위치는 평행이동 (−6, −36.2)가 걸리는 자리라
        ///     방향이 실제 반지름에서 벗어나 있었다 → <see cref="FanOriginPoints"/>에서 잰다.</item>
        ///   <item><b>반지름이 틀렸다.</b> 36-4의 「폭 무관 보장」은 <c>궤도 + 버튼반경 + 간격</c>을
        ///     전제로 세워졌는데, 위성이 궤도 <b>168</b>로 나가면서 그 전제가 깨졌다 — 호 버튼의
        ///     이름표가 놓이던 반지름(≈170pt)이 <b>위성 궤도와 같은 자리</b>였다.
        ///     이제 <see cref="HoverLabelRingRadiusPoints"/>(= 가장 바깥 버튼 + 버튼반경 + 간격)를 쓴다.</item>
        /// </list>
        ///
        /// <para>★★ <b>2026-09-06 — 그 수리는 「겹침」만 고쳤고 「누구 이름표인가」는 못 고쳤다.</b>
        /// 전역 단일 링은 알약이 <b>형제 버튼을 물지 않게</b> 하는 데는 성공했지만, 궤도 111의 호
        /// 버튼 이름표를 궤도 168짜리 링 위에 올려놓았다 — 즉 <b>겹치지는 않는데 엉뚱한 버튼 옆에
        /// 떠 있었다</b>(슬롯 1[캐릭터]: 자기 버튼 109.04pt vs [앱 종료] 72.30pt). 두 결함은 원인이
        /// 하나(궤도가 갈라져 있다)이고, <b>위성 폐지로 함께 사라졌다</b>. 자세한 것은
        /// <see cref="FinalizeLayout"/> 문서.</para>
        ///
        /// <para><b>보장이 되살아난다</b>(36-4와 같은 지지 함수 논증): 모든 버튼 원은 원점에서 반지름
        /// <c>ring − 간격</c> 안에 있고, 알약의 <b>모든 점</b>은 <c>ring</c> 밖에 있다
        /// (축 정렬 사각형의 지지 함수 = <c>|u.x|·반폭 + |u.y|·반높이</c> = <paramref name="labelSizePoints"/>의
        /// reach). <b>알약이 아무리 넓어져도 형제를 물 수 없다</b> — 각도에도, 평행이동에도, 축소
        /// 폴백에도 의존하지 않는다.</para>
        ///
        /// <para>★ <b>세로 일렬 폴백은 규칙이 다르다</b>(<paramref name="columnLayout"/>). 거기서는 모든
        /// 버튼이 <b>한 직선 위</b>에 있어서 위 규칙을 쓰면 이름표 다섯 개가 맨 끝 버튼 바깥 <b>한 자리에
        /// 겹쳐 쌓인다</b>. 그래서 <b>가로로</b> 민다 — 세로로 밀면 간격 52pt짜리 이웃을 문다
        /// (검산: 안쪽 모서리 30pt 옆 · 이웃까지 세로 52 − 반높이 9 = 43 → 거리 52.4 &gt; 반경 22).
        /// <b>이건 이번 라운드가 만든 결함이 아니라 원래 깨져 있던 자리다</b>(4칸 시절에도 간격은 52였다).</para>
        ///
        /// <para>화면 밖으로 나가면 <b>클램프만</b> 하고 반대쪽으로 뒤집지 않는다. 뒤집으면 부채꼴
        /// 안쪽으로 들어가 정확히 위 문제가 되살아난다. 클램프가 걸리는 구성에서는 보장이 그만큼
        /// 약해지지만, 화면 밖으로 나간 이름표는 아예 읽을 수 없으므로 그쪽이 먼저다.</para>
        /// </summary>
        public static Vector2 HoverLabelCenterPoints(
            Vector2 anchorCenter, Vector2 patternOrigin, float ringRadiusPoints, float diameterPoints,
            bool columnLayout, Vector2 labelSizePoints, Vector2 screenPoints,
            float leftMargin, float rightMargin, float bottomMargin, float topMargin)
        {
            float halfW = labelSizePoints.x * 0.5f;
            float halfH = labelSizePoints.y * 0.5f;

            Vector2 center;
            if (columnLayout)
            {
                // 화면 <b>안쪽</b>으로 민다 — 바깥으로 밀면 곧장 클램프에 걸려 되돌아온다.
                float dir = patternOrigin.x <= screenPoints.x * 0.5f ? 1f : -1f;
                center = anchorCenter
                    + new Vector2(dir, 0f) * (diameterPoints * 0.5f + HoverLabelGapPoints + halfW);
            }
            else
            {
                Vector2 outward = anchorCenter - patternOrigin;
                if (outward.sqrMagnitude < 1e-4f) outward = Vector2.down;
                outward.Normalize();

                float reach = Mathf.Abs(outward.x) * halfW + Mathf.Abs(outward.y) * halfH;
                center = patternOrigin + outward * (ringRadiusPoints + reach);
            }

            float minX = leftMargin + halfW;
            float maxX = screenPoints.x - rightMargin - halfW;
            if (maxX >= minX) center.x = Mathf.Clamp(center.x, minX, maxX);

            float minY = bottomMargin + halfH;
            float maxY = screenPoints.y - topMargin - halfH;
            if (maxY >= minY) center.y = Mathf.Clamp(center.y, minY, maxY);

            return center;
        }

        /// <summary>인스턴스의 지금 상태로 위 순수 함수를 부른다 — 여백 네 개를 부르는 쪽마다
        /// 다시 적지 않게 하는 한 줄짜리 어댑터다.</summary>
        private Vector2 ResolveHoverLabelCenter(Vector2 anchorCenter, float pillWidth)
            => HoverLabelCenterPoints(anchorCenter, FanOriginPoints, _labelRingRadiusPoints,
                _diameterPoints, _columnLayout, new Vector2(pillWidth, HoverLabelHeightPoints),
                _screenPointsAtLayout, EffectiveLeftMarginPoints, EffectiveRightMarginPoints,
                EffectiveBottomMarginPoints, EffectiveTopMarginPoints);

        private void SetHoverLabelAlpha(float alpha)
        {
            _hoverLabelSurface.color = Fade(UiChrome.PanelSurface, alpha);
            _hoverLabelBorder.color = Fade(UiChrome.Flatten(UiChrome.PanelBorder, UiChrome.PanelSurface), alpha);
            _hoverLabelText.color = Fade(UiChrome.TextPrimary, alpha);
        }

        /// <summary>
        /// ★ [앱 종료]는 <b>호버에서 강조색까지 가지 않는다</b>(53-4).
        ///
        /// <para>★★ <b>2026-09-06부터 이 색 규칙이 「다름」의 전부를 짊어진다.</b> 위성이 폐지되어
        /// 위치로는 다른 넷과 구별되지 않기 때문이다 — 그 판단의 근거는 <see cref="GearMenuButton.Quit"/> 문서.</para>
        ///
        /// <para>다른 넷은 호버에서 <c>AccentSurface</c>/<c>Accent</c>까지 가지만, [앱 종료]는 호버에서
        /// 겨우 <b>다른 버튼의 평상 수준</b>(<c>CardSurface</c>)에 도달한다. 강조색은 <b>무장 전용</b>이다 —
        /// 그래야 이 표면에서 "이 버튼이 밝아졌다"의 뜻이 <b>「되돌릴 수 없는 것이 장전됐다」 하나</b>가 된다.</para>
        ///
        /// <para>새 색은 <b>하나도</b> 만들지 않았다: <c>WarmAccent</c>/<c>AccentSurface</c>/
        /// <c>AccentBorder</c>는 삭제된 행동창 [앱 종료] 무장 스타일이 쓰던 <b>바로 그 색</b>이다.
        /// 위험색(빨강)은 <b>도입하지 않았다</b> — 필요 판단이 서면 <c>design-art</c> 판정을 거친다.</para>
        /// </summary>
        private void ApplyButtonStyle(ButtonView b, int index, float alpha)
        {
            bool active = index == _activeIndex;
            float hover = EaseOutQuad(b.Hover);
            bool armedQuit = _quitArmed && index == (int)GearMenuButton.Quit;

            // ★★ 2026-09-06 (docs/UI_ALPHA_BLEED_POLICY.md §5-3) — <b>여기는 창 안이 아니라
            //   바탕화면 직접</b>이라 비침 식이 1−α²다: AccentSurface(α0.14)는 <b>98%</b>,
            //   AccentBorder(α0.55)는 <b>69.75%</b>가 비쳤다. 즉 "밝아졌다"를 말해야 할 강조 면이
            //   실제로는 <b>구멍</b>이었다. 두 끝점을 미리 합성해 α=1로 만든다 — 보이는 색은 같다.
            //   <b>토큰은 하나도 바꾸지 않았다</b>: 무장 링을 Accent로 올리는 §5-4는 연출 강도
            //   판정이라 P2로 남는다(리더 배분 사항).
            Color quitArmedFace = UiChrome.Flatten(UiChrome.AccentSurface, UiChrome.CardSurface);
            Color accentFace = quitArmedFace;   // 같은 합성 — 강조 면의 끝점은 한 값이다.

            Color surface, border, symbol;
            if (index == (int)GearMenuButton.Quit)
            {
                // 평상 SubtleSurface -> 호버 CardSurface -> 무장 AccentSurface. 호버는 강조까지 안 간다.
                surface = armedQuit ? quitArmedFace
                    : Color.Lerp(UiChrome.SubtleSurface, UiChrome.CardSurface, hover);
                // 테두리의 바탕은 팬이 아니라 <b>방금 정한 이 버튼의 면</b>이다(호버 중에도 따라간다).
                border = UiChrome.Flatten(armedQuit ? UiChrome.AccentBorder : UiChrome.CardBorder, surface);
                symbol = armedQuit ? UiChrome.WarmAccent
                    : Color.Lerp(UiChrome.TextSecondary, UiChrome.TextPrimary, hover);
            }
            else
            {
                // 끝점을 미리 합성한 뒤 Lerp한다 — raw Lerp는 색과 <b>α를 함께</b> 보간하므로
                // 중간 프레임마다 창 알파가 흔들린다. 끝점이 같으면 0.09초짜리 호버 보간의
                // 중간값은 사람이 판정할 수 없다(§5-3 주).
                surface = Color.Lerp(UiChrome.CardSurface, accentFace, active ? 1f : hover);
                border = Color.Lerp(UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface),
                                    UiChrome.Flatten(UiChrome.AccentBorder, accentFace),
                                    active ? 1f : hover);
                symbol = Color.Lerp(UiChrome.TextPrimary, UiChrome.Accent, active ? 1f : hover);
            }

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
                // ★ [앱 종료]의 카운트다운 링에는 트랙이 없다(전원 기호의 원호가 그 자리에 이미 있다).
                //   가드 없이 쓰면 [앱 종료] 무장 첫 프레임에 NullReference로 죽는다.
                if (b.RingTrack != null)
                {
                    // 트랙의 바탕은 이 버튼의 <b>면</b>이다(알파를 먹이기 <b>전</b> 값으로 합성한다 —
                    // Fade 뒤의 색을 바탕으로 쓰면 반투명 위에 합성하는 셈이라 계산이 거짓말을 한다).
                    b.RingTrack.color = Fade(UiChrome.Flatten(UiChrome.TrackBackground, surface), alpha);
                }
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
        /// ★ 부채꼴 기준각 θ₀ — <b><see cref="Snap45"/>를 감싸는 유일한 자리</b>
        /// (docs/UX_RIGHTCLICK_FAN_MENU.md §1-3-3, 신규 2026-09-05).
        ///
        /// <para><c>upBiasPoints = 0</c>이면 <b>옛 식과 한 비트도 다르지 않다</b> — 아래 분기가
        /// 그것을 문법으로 보장한다(<c>y</c>에 0을 더하는 연산조차 하지 않는다). 그래서 톱니의
        /// 출하 기본 θ₀ 225°는 이 변경으로 <b>움직일 수 없다</b>.</para>
        ///
        /// <para><b>이 함수가 따로 있는 이유가 그 보장 하나다.</b> 바이어스를 <see cref="Snap45"/>
        /// 안에 넣으면 톱니 기본 화면이 225° → 180°로 바뀐다(§1-3-4 «유일한 함정»).
        /// <b>합치지 마라.</b></para>
        /// </summary>
        /// <param name="anchorPoints">앵커(캔버스 포인트).</param>
        /// <param name="screenPoints">화면 크기(캔버스 포인트).</param>
        /// <param name="upBiasPoints">위쪽 바이어스. 톱니 0 / 캐릭터 <see cref="FanUpBiasPoints"/>.</param>
        public static float SnapFanBaseAngle(Vector2 anchorPoints, Vector2 screenPoints, float upBiasPoints)
        {
            Vector2 toCenter = screenPoints * 0.5f - anchorPoints;
            if (upBiasPoints != 0f) toCenter.y += upBiasPoints;
            return Snap45(toCenter);
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
            _baseAngleDegrees = SnapFanBaseAngle(_gearCenterPoints, _screenPointsAtLayout, _fanUpBiasPoints);

            if (TrySearchRotation(ButtonDiameterPoints, allowShift: false)) { FinalizeLayout(false); return; }
            if (TrySearchRotation(ButtonDiameterPoints, allowShift: true)) { FinalizeLayout(false); return; }
            if (TrySearchRotation(ShrunkDiameterPoints, allowShift: false)) { FinalizeLayout(false); return; }
            if (TrySearchRotation(ShrunkDiameterPoints, allowShift: true)) { FinalizeLayout(false); return; }
            if (TryColumn(ButtonDiameterPoints)) { FinalizeLayout(true); return; }
            if (TryColumn(ShrunkDiameterPoints)) { FinalizeLayout(true); return; }

            PlaceColumn(ShrunkDiameterPoints);
            ShiftGroupIntoScreen();
            FinalizeLayout(true);
        }

        /// <summary>
        /// 배치가 끝난 뒤 <b>이름표 기하가 쓸 세 값</b>을 확정한다 — 사다리의 <b>모든</b> 출구가 여기를 지난다.
        ///
        /// <para><b>왜 링 반지름을 여기서 「재는가」</b>: 상수로 적으면 축소 폴백(Ø36)·평행이동·
        /// 세로 일렬에서 값이 어긋난다. <b>실제로 놓인 버튼까지의 최대 거리</b>를 재면 어떤 경로로
        /// 배치됐든 «모든 버튼 원이 이 반지름 안에 있다»가 사실이 된다 — 그것이 36-4가 말한
        /// <b>폭 무관 보장</b>의 전제다.</para>
        ///
        /// <para>★★ <b>그러나 「가장 바깥 버튼 하나」로 링을 만드는 것은 궤도가 하나일 때만 옳다</b>
        /// (2026-09-06 사용자 신고의 정체). 위성이 궤도 168pt에 있던 동안, 궤도 111pt의 호 버튼들은
        /// 이름표가 <b>자기 원에서 87pt나 떨어진 자리</b>(= 위성 옆)에 놓였다 — 실측으로 슬롯 1[캐릭터]의
        /// 이름표가 자기 버튼에서 109.04pt, [앱 종료]에서는 <b>72.30pt</b>였다(1.51배 더 가깝다).
        /// 이름표는 «지금 커서가 올라간 버튼이 무엇인가»를 말하는 물건이라 그 배치는 <b>틀린 말</b>이다.</para>
        ///
        /// <para>★ <b>위성 폐지로 궤도가 하나가 되면서 이 전역 링이 처음으로 정확해졌다</b> —
        /// 모든 슬롯에서 <c>링 = 자기 궤도 + 버튼반경 + 간격</c>이 성립한다. 즉 <b>슬롯별 링을 만들
        /// 필요가 없다</b>(쓰이지 않을 분기를 미리 깔면 반드시 썩는다 — 라벨 알약을 통째로 지운 것과
        /// 같은 판단). 대신 그 <b>전제</b>를 <see cref="SlotRadiusPoints"/> 문서와
        /// <c>GearRadialFanGeometryTests</c>가 잠근다: 궤도를 다시 가르면 러너가 «그때는 슬롯별 링이
        /// 필요하다»고 말해 준다.</para>
        /// </summary>
        private void FinalizeLayout(bool column)
        {
            _columnLayout = column;

            Vector2 origin = FanOriginPoints;
            float farthest = 0f;
            for (int i = 0; i < ButtonCount; i++)
            {
                if (_buttons[i] == null) continue;
                farthest = Mathf.Max(farthest, (_buttons[i].CenterPoints - origin).magnitude);
            }
            _labelRingRadiusPoints = HoverLabelRingRadius(farthest, _diameterPoints);
            ApplyLayoutDiameterToViews();
        }

        /// <summary>
        /// ★★ 2026-09-06 — <b>사다리가 정한 지름을 화면에 옮기는 유일한 자리.</b>
        ///
        /// <para><b>여기가 없어서 축소 폴백은 태어나서 한 번도 그려진 적이 없었다.</b>
        /// <see cref="_diameterPoints"/>는 히트 판정(<see cref="HitTest"/>)·클램프 상자
        /// (<see cref="BoxFor"/>)·이름표 링(<see cref="HoverLabelRingRadius"/>)·진단 로그가 모두
        /// 읽고 있었는데, <b>원을 만드는 코드만</b> 그 값을 안 봤다 — <see cref="BuildButton"/>이
        /// <c>Awake</c> 때 <see cref="ButtonDiameterPoints"/>로 한 번 굽고 끝이었다.
        /// 그래서 Ø36 판정이 걸린 화면에서 <b>판정 지름 36 · 그려지는 지름 44</b>가 됐다.</para>
        ///
        /// <para><b>그 어긋남의 실측 결과</b>(pt. 「설계대로 Ø44」 → 「버그(판정36/렌더44)」 → 「수정 후」):
        /// <code>
        ///   히트 반경 − 보이는 반경  평상 +4.00 → <b>+0.00</b> → +4.00
        ///                            호버 +2.00 → <b>−2.00</b> → +2.36
        ///   클램프 상자 − 보이는 원  평상  6.00 →  2.00  → 6.00
        ///                            호버  4.00 →  <b>0.00</b> → 4.36
        ///   세로 일렬 이웃 사이 틈   평상  8.00 →  8.00  → <b>16.00</b>
        /// </code>
        /// <b>가장 날카로운 것은 호버의 −2.00이다</b> — 커서를 올린 순간 <b>보이는 원이 눌리는 원보다
        /// 커져</b>, 바깥 2pt 띠가 «보이는데 안 눌리는» 영역이 됐다. 32-1이 개별 버튼 클램프를 금지하며
        /// 지키려던 바로 그 불변식(«보이는 것과 눌리는 것이 같다»)이 <b>축소 폴백에서만</b> 깨져 있었다.
        /// 둘째로 호버 상자 여유 0.00 — 보이는 원의 가장자리가 <b>예약 띠 경계선에 정확히 닿았다</b>.
        /// 셋째로 세로 일렬에서 축소가 <b>시야에 아무것도 벌어주지 못했다</b>(틈 8.00으로 Ø44와 동일).</para>
        ///
        /// <para><b>왜 <see cref="ButtonView.Group"/>의 스케일인가</b> — 층을 나눠야 두 배율이 서로를
        /// 덮어쓰지 않는다:
        /// <list type="bullet">
        ///   <item><b>Group</b> = 위치 + <b>배치 배율</b>. 펼침 한 번에 한 번만 쓴다.</item>
        ///   <item><b>Root</b> = 펼침/호버 <b>애니메이션 배율</b>. 매 프레임 덮어쓰인다
        ///     (<see cref="ApplyVisuals"/>). 여기에 배치 배율을 곱해 넣으면 <b>다음 프레임이 지운다</b>.</item>
        /// </list>
        /// Group은 <c>sizeDelta</c>가 0이고 원은 그 중심에 놓이므로 스케일은 <b>버튼 중심을 축으로</b>
        /// 걸린다 — <c>anchoredPosition</c>(궤도 위 위치)은 부모 좌표계 값이라 영향을 받지 않는다.</para>
        ///
        /// <para><b>왜 원의 <c>sizeDelta</c>를 직접 안 고치는가</b>: <see cref="UiChrome.AddCircle"/>은
        /// 테두리 두께 비율(1.2/44)과 가장자리 램프 여유를 <b>스프라이트에 구워</b> 넣는다. 상자만
        /// 줄이면 스프라이트가 늘어나 결국 같은 배율이 걸리는데, 원 3개 + 심볼 + 배지를 <b>각각</b>
        /// 다시 재야 하고 그중 하나만 빠지면 그날로 어긋난다. 균일 배율은 그 어긋남이 원리상 불가능하다.
        /// 게다가 심볼 도형 함수를 한 줄도 건드리지 않는다(같은 파일을 동시에 만지는 작업과의 충돌 회피).</para>
        ///
        /// <para>Ø44에서는 계수가 정확히 1이라 <b>비트 동일</b>하다 — 축소가 안 걸린 화면은 아무것도 안 바뀐다.</para>
        /// </summary>
        private void ApplyLayoutDiameterToViews()
        {
            float k = LayoutDiameterScale;
            for (int i = 0; i < ButtonCount; i++)
            {
                if (_buttons[i]?.Group == null) continue;
                _buttons[i].Group.localScale = new Vector3(k, k, 1f);
            }
        }

        /// <summary>사다리가 정한 지름을 <b>기준 지름 대비 배율</b>로 옮긴 값(Ø44 → 1, Ø36 → 0.8181…).</summary>
        private float LayoutDiameterScale => _diameterPoints / ButtonDiameterPoints;

        /// <summary>가장 바깥 버튼까지의 거리에서 <b>이름표 링 반지름</b>을 만든다.
        /// <para><b>public static 순수 함수</b>인 이유는 <see cref="EffectiveMarginPoints"/>와 같다 —
        /// 테스트가 <c>+ 반지름 + 간격</c>을 <b>다시 타이핑하면</b> 그 사본이 프로덕션과 조용히 갈라진다.</para></summary>
        public static float HoverLabelRingRadius(float farthestButtonDistancePoints, float diameterPoints)
            => farthestButtonDistancePoints + diameterPoints * 0.5f + HoverLabelGapPoints;

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
            _layoutShiftPoints = shift;   // ★ 이름표 방향이 이 값을 봐야 한다(FanOriginPoints).
            return true;
        }

        /// <summary>슬롯 i의 각도 오프셋 — θ₀ + ((<see cref="ButtonCount"/>−1)/2 − i)·step.
        /// <b>부채꼴은 언제나 θ₀를 기준으로 좌우 대칭</b>이라 버튼 개수가 바뀌어도 "가운데가 화면
        /// 안쪽"이라는 성질이 유지된다. n=3이면 (1−i)·60°, n=4면 (1.5−i)·30°(36-3-3),
        /// <b>n=5면 (2−i)·22.5°</b>(2026-09-06) → {+45, +22.5, 0, −22.5, −45}, 합 0, 스팬 90°.
        ///
        /// <para>★ <b>2026-09-06 — 여기 있던 「분모를 ArcButtonCount로 갈라라」 경고가 사라졌다.</b>
        /// 그 경고는 위성([앱 종료])이 호 밖에 있던 동안의 것이었다. 사용자 신고
        /// <i>"끄기 버튼만 따로 떨어져 있다"</i>로 위성이 폐지되면서 <b>다섯이 전부 이 한 식을 탄다</b> —
        /// 갈라질 분모가 없으므로 그 함정도 없다. 대신 새로 지켜야 하는 것은
        /// <b>스팬(= (n−1)·step)이 90°라는 사실</b> 하나이고,
        /// <c>GearRadialFanGeometryTests</c>가 그것을 프로덕션 상수에서 다시 계산해 잠근다.</para>
        ///
        /// <see cref="Snap45"/>와 같은 이유로 <b>public static 순수 함수</b>다 — 기하 확정치는 씬 없이
        /// EditMode에서 잠글 수 있어야 한다(36절의 계산이 코드에서 조용히 어긋나는 것을 막는 유일한 방법).</summary>
        public static float SlotOffsetDegrees(int index)
            => ((ButtonCount - 1) * 0.5f - index) * ButtonAngleStepDegrees;

        /// <summary>슬롯 i가 도는 <b>궤도 반지름</b>(pt). <b>다섯이 전부 같다</b>(2026-09-06 위성 폐지).
        ///
        /// <para>★ <b>이 함수를 지우지 마라 — 값이 하나여도 자리는 하나여야 한다.</b> 호버 이름표 링이
        /// «가장 바깥 버튼» 하나에서 나오는 <b>전역 값</b>이라(<see cref="FinalizeLayout"/>),
        /// 궤도가 슬롯마다 달라지는 순간 안쪽 궤도 버튼의 이름표가 자기 원에서 그 차이만큼 떠
        /// <b>다른 버튼 옆에 붙는다</b> — 그것이 2026-09-06 사용자 신고의 정체였다(위성 168 vs 호 111).
        /// 그래서 «궤도가 하나»라는 사실을 <b>이 한 함수</b>가 말하고,
        /// <c>GearRadialFanGeometryTests.전역_단일_이름표_링의_전제인_단일_궤도가_유지된다</c>가 잠근다.
        /// 다시 갈라야 한다면 링을 <b>슬롯별</b>로 바꾸는 작업이 같은 라운드에 함께 와야 한다.</para></summary>
        public static float SlotRadiusPoints(int index) => OrbitRadiusPoints;

        /// <summary>기어 중심과 기준각이 주어졌을 때 슬롯 i 버튼의 중심(캔버스 포인트). 순수 함수.</summary>
        public static Vector2 SlotCenterPoints(Vector2 gearCenterPoints, float baseDegrees, int index)
        {
            float a = (baseDegrees + SlotOffsetDegrees(index)) * Mathf.Deg2Rad;
            return gearCenterPoints + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * SlotRadiusPoints(index);
        }

        private Vector2 FanCenter(float baseDegrees, int index)
            => SlotCenterPoints(_gearCenterPoints, baseDegrees, index);

        /// <summary>세로 일렬 폴백에서 슬롯 <paramref name="index"/>의 중심. <paramref name="sign"/>은
        /// 화면 안쪽 수직 방향(+1 = 위, −1 = 아래).
        /// <para><see cref="SlotCenterPoints"/>와 같은 이유로 <b>public static 순수 함수</b>다 —
        /// 폴백 기하도 씬 없이 EditMode에서 잠글 수 있어야 하고, 테스트가 이 식을 <b>베껴 적으면</b>
        /// 그 사본이 프로덕션과 조용히 갈라진다.</para></summary>
        public static Vector2 ColumnSlotCenterPoints(Vector2 originPoints, float sign, int index)
            => new Vector2(originPoints.x,
                originPoints.y + sign * (OrbitRadiusPoints + index * ColumnFallbackSpacingPoints));

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
                _buttons[i].CenterPoints = ColumnSlotCenterPoints(_gearCenterPoints, sign, i);
            }
            _diameterPoints = diameter;
            _layoutShiftPoints = Vector2.zero;
        }

        /// <summary>버튼 전부를 <b>같은 벡터로</b> 평행이동해 화면 안으로 넣는다(형태 보존 — 개별 클램프 금지).</summary>
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
            _layoutShiftPoints += shift;
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
        /// ★★ 2026-09-03 — <b>마지막 남은 변.</b> 좌·우·상은 이미 예약 띠를 보는데 <b>하단만
        /// <c>ScreenMarginPoints</c> 8pt 고정</b>이었다. 그런데 <b>Windows 작업표시줄의 기본 도킹
        /// 위치가 하단</b>이다(UX_FLOW 53-9 / UX_WIDGETS R4-6 #5).
        ///
        /// <para><b>실측 계산</b>: 하단 막대 48pt 구성에서 톱니를 화면 아래로 옮기면 부채꼴 상자의
        /// <c>yMin</c>이 8까지 내려갈 수 있으므로 최대 <b>40pt가 막대 뒤</b>로 들어간다. 작업표시줄은
        /// 최상위 창이라 <b>그 위의 클릭은 우리에게 오지 않는다</b> — 보이는데 안 눌리는 버튼이 된다.</para>
        ///
        /// <para>★★ <b>「Dock은 캐릭터의 발판이다」와 충돌하지 않는다.</b> 그 원칙은
        /// <c>SurfaceSafeAreaPolicy.ClampCenterY</c>가 지키고 있고 그쪽은 <b>안 건드렸다</b>.
        /// 두 규칙의 대상이 다르기 때문이다:
        /// <list type="bullet">
        ///   <item><b>캐릭터</b>는 Dock 위를 <b>걷는다</b> — 그림이고, 가려도 잃는 것이 없다.</item>
        ///   <item><b>부채꼴 버튼</b>은 <b>눌러야 하는 것</b>이다 — 막대 뒤로 들어가면 클릭 자체가
        ///     도착하지 않는다. 「발판」 설계는 그림에 대한 것이지 클릭 표면에 대한 것이 아니다.</item>
        /// </list></para>
        ///
        /// <para><b>회귀 없음</b>: 띠가 0이면 <c>max(8, 0) = 8</c>로 <b>지금과 비트 동일</b>하다.
        /// macOS Dock이 있는 환경에서는 부채꼴이 Dock 위로 올라선다 — 그쪽도 클릭을 먹는 최상위
        /// 표면이므로 같은 이유로 올바르다.</para>
        /// </summary>
        private float EffectiveBottomMarginPoints => EffectiveMarginPoints(ScreenMarginPoints,
            ReservedEdgeProbe.EdgeInsetPoints(_agent != null ? _agent.PlatformService : null, ReservedEdge.Bottom));

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
            float bottomMargin = EffectiveBottomMarginPoints;
            if (union.yMin < bottomMargin) shift.y = bottomMargin - union.yMin;
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
            => box.xMin >= EffectiveLeftMarginPoints && box.yMin >= EffectiveBottomMarginPoints
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
            _hoverLabelBorder = UiChrome.AddOutline(_hoverLabel, "Border",
                UiChrome.Flatten(UiChrome.PanelBorder, UiChrome.PanelSurface), 9);
            _hoverLabelBorder.raycastTarget = false;

            _hoverLabelText = UiChrome.AddText(_hoverLabel, "Text", UiChrome.FontCaption,
                TextAnchor.MiddleCenter, UiChrome.TextPrimary);
            UiChrome.Stretch(_hoverLabelText.rectTransform);

            for (int i = 0; i < ButtonCount; i++)
            {
                _hoverLabelText.text = ButtonNames[i];
                _nameWidths[i] = _hoverLabelText.preferredWidth + HoverLabelPaddingPoints;
            }
            // 무장 문구도 <b>여기서 한 번만</b> 잰다 — 무장 중에는 매 프레임 도는 코드가 있으므로
            // 그쪽에서 preferredWidth를 부르면 3초 동안 폰트 메시를 매 프레임 다시 재게 된다.
            _hoverLabelText.text = QuitArmedLabel;
            _quitArmedLabelWidth = _hoverLabelText.preferredWidth + HoverLabelPaddingPoints;
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
            _onboardingHintBorder = UiChrome.AddOutline(_onboardingHint, "Border",
                UiChrome.Flatten(UiChrome.AccentBorder, UiChrome.PanelSurface), 9);
            _onboardingHintBorder.raycastTarget = false;

            _onboardingHintText = UiChrome.AddText(_onboardingHint, "Text", UiChrome.FontCaption,
                TextAnchor.MiddleCenter, UiChrome.TextPrimary);
            UiChrome.Stretch(_onboardingHintText.rectTransform);
            _onboardingHintText.text = OnboardingHintText;

            _onboardingHintWidth = _onboardingHintText.preferredWidth + HoverLabelPaddingPoints;
            _onboardingHint.sizeDelta = new Vector2(_onboardingHintWidth, HoverLabelHeightPoints);

            _onboardingHintSurface.color = Fade(UiChrome.PanelSurface, 0f);
            _onboardingHintBorder.color = Fade(UiChrome.Flatten(UiChrome.AccentBorder, UiChrome.PanelSurface), 0f);
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
            view.Border = UiChrome.AddCircle(view.Root, "Border", d,
                UiChrome.Flatten(UiChrome.CardBorder, UiChrome.CardSurface), 1.2f);
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
                GearMenuButton.Quit => BuildPowerSymbol(view),
                GearMenuButton.Todo => BuildChecklistSymbol(view),
                _ => UnknownSymbolFallback(view, index),
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

        /// <summary>배선이 빠진 슬롯 — <b>조용히 넘어가지 않는다</b>.
        /// <para>예전에는 [오늘 할일]이 <c>_ =&gt;</c>로 흘러 들어와서, 슬롯을 하나 더 늘리면 그 버튼이
        /// <b>체크리스트 그림을 달고</b> 나타났다. 정상값을 전부 명시로 옮긴 지금 여기 도달하는 것은
        /// 배선 누락뿐이다 — 그림은 뭐라도 그려야 하므로 물러나되, <b>화면과 뜻이 어긋났다는 사실</b>을
        /// 로그로 남긴다.</para></summary>
        private Image[] UnknownSymbolFallback(ButtonView view, int index)
        {
            Debug.LogError($"[부채꼴] 슬롯 {index}({NameOf(index)})에 심볼이 없습니다 — " +
                "GearMenuButton에 값을 더했다면 BuildButton의 switch에도 그 가지를 더해야 합니다. " +
                "지금은 체크리스트 심볼로 물러났고, 그 버튼의 그림과 뜻이 어긋난 상태입니다.");
            return BuildChecklistSymbol(view);
        }

        // ====================================================================================
        // ① 집중 모드 — 스톱워치
        // ====================================================================================

        /// <summary>스톱워치 링의 지름(pt). <b>불변</b> — <c>RingTrack</c>/<c>RingFill</c>(잔여 시간 호)이
        /// 같은 원을 재사용하는 계약이 여기에 걸려 있다.</summary>
        private const float StopwatchRingDiameterPoints = 20f;

        /// <summary>용두 목의 아래 끝(pt). 링 코어는 r ∈ [8, 10]이므로 9.2는 <b>코어 한가운데</b>다 —
        /// 즉 목이 링에 <b>용접</b>되고, 옛 「혹처럼 붙은 4pt 획 하나」가 사라진다(FG-3 ①).</summary>
        private static readonly Vector2[] StopwatchCrownStemPath =
        {
            new Vector2(0f, 9.2f),
            new Vector2(0f, 11.6f),
        };

        /// <summary>분침 — 끝 y = 4.0 ⇒ 잉크 끝 5.0, 링 안쪽(8.0)까지 <b>3.00pt = 1.5W</b>(FG-3 ②).
        /// 옛 값(길이 6.5, 끝 6.5)은 1.53pt로 링에 붙어 있었다.</summary>
        private static readonly Vector2[] StopwatchMinuteHandPath =
        {
            Vector2.zero,
            new Vector2(0f, 4f),
        };

        /// <summary>시침 — −30° 방향 길이 3.0(끝 잉크 r = 4.0, 링 안쪽까지 4.00pt).</summary>
        private static readonly Vector2[] StopwatchHourHandPath =
        {
            Vector2.zero,
            Polar(-30f, 3f),
        };

        /// <summary>① 집중 모드 — 스톱워치(용두 목 + 용두 단추 + 링 + 바늘 2). 세션이 돌면 이 링이
        /// 그대로 잔여 시간 호가 된다.
        /// <para>★ 2026-09-06 재조형(DESIGN_FAN_MENU_ICONS §6-①). 옛 용두는 <b>획 4.0pt = 2W</b>짜리
        /// 캡슐 하나였다 — 이 메뉴에서 가장 굵은 잉크가 가장 덜 중요한 부속에 붙어 <b>위계가 거꾸로</b>였고,
        /// 링과 0.5pt 겹쳐 목 없이 「혹」으로 읽혔다. 이제 <b>목(g0) + 단추(g2)</b> 두 조각이라
        /// 물건 안에 위계가 생긴다.</para>
        /// <para>단추를 <b>가로</b>로 두는 것이 중요하다 — 세로로 두면 ⑤ 전원의 세로획과 같은 실루엣이 된다.</para></summary>
        private Image[] BuildStopwatchSymbol(ButtonView view)
        {
            Transform p = view.Symbol;
            var parts = new List<Image>(8);

            UiChrome.AddPolyline(p, "CrownStem", StopwatchCrownStemPath, SymbolStroke, UiChrome.TextPrimary, parts);
            // 용두 단추 = 이 글리프의 유일한 g2. 꺾은선이 아니라 캡슐 하나라 AddStroke를 직접 쓴다.
            parts.Add(UiChrome.AddStroke(p, "CrownCap", 6f, SymbolStrokeHeavy, 0f,
                new Vector2(0f, 12.4f), UiChrome.TextPrimary));

            view.RingTrack = UiChrome.AddCircle(p, "Ring", StopwatchRingDiameterPoints,
                UiChrome.TextPrimary, SymbolStroke);
            parts.Add(view.RingTrack);

            // 잔여 시간 호 — 같은 링 위에 겹쳐 그린다(세션 중에만 켠다).
            view.RingFill = UiChrome.AddCircle(p, "RingFill", StopwatchRingDiameterPoints,
                UiChrome.WarmAccent, SymbolStroke);
            view.RingFill.type = Image.Type.Filled;
            view.RingFill.fillMethod = Image.FillMethod.Radial360;
            view.RingFill.fillOrigin = (int)Image.Origin360.Top;
            view.RingFill.fillClockwise = true;
            view.RingFill.fillAmount = 1f;
            view.RingFill.gameObject.SetActive(false);

            UiChrome.AddPolyline(p, "MinuteHand", StopwatchMinuteHandPath, SymbolStroke, UiChrome.TextPrimary, parts);
            UiChrome.AddPolyline(p, "HourHand", StopwatchHourHandPath, SymbolStroke, UiChrome.TextPrimary, parts);

            return parts.ToArray();
        }

        // ====================================================================================
        // ② 캐릭터 — 스틱맨
        // ====================================================================================

        private const float StickHeadDiameterPoints = 7f;
        private const float StickHeadCenterY = 8f;
        private const float StickShoulderY = 3.5f;
        private const float StickPelvisY = -4.5f;

        /// <summary>팔 길이 6.0 → <b>7.0</b>. 어깨에서 아래로 30° 내리면 끝점 y가 정확히 0이 되어
        /// 좌표가 깨끗해지고, 잉크 상자 폭이 9.60 → <b>14.09pt</b>가 된다 — 다섯 칸 중 혼자 절반이던
        /// 실루엣이 나머지 넷과 균형을 맞춘다(FG-8).</summary>
        private const float StickArmLengthPoints = 7f;
        private const float StickArmDropDegrees = 30f;

        /// <summary>다리 벌림 = 수직에서 ±25°(옛 ±16°). 발 끝 사이 <b>3.07pt = 1.54W</b>로 FG-3을
        /// 넘긴다(옛 1.56pt는 미달이었다 — 1×에서 두 발이 한 덩어리로 뭉갰다).</summary>
        private const float StickLegLengthPoints = 6f;
        private const float StickLegSpreadDegrees = 25f;

        private static readonly Vector2 StickShoulder = new Vector2(0f, StickShoulderY);
        private static readonly Vector2 StickPelvis = new Vector2(0f, StickPelvisY);

        private static readonly Vector2[] StickSpinePath = { new Vector2(0f, 4.5f), StickPelvis };
        private static readonly Vector2[] StickArmLPath =
            { StickShoulder, StickShoulder + Polar(-180f + StickArmDropDegrees, StickArmLengthPoints) };
        private static readonly Vector2[] StickArmRPath =
            { StickShoulder, StickShoulder + Polar(-StickArmDropDegrees, StickArmLengthPoints) };
        private static readonly Vector2[] StickLegLPath =
            { StickPelvis, StickPelvis + Polar(-90f - StickLegSpreadDegrees, StickLegLengthPoints) };
        private static readonly Vector2[] StickLegRPath =
            { StickPelvis, StickPelvis + Polar(-90f + StickLegSpreadDegrees, StickLegLengthPoints) };

        /// <summary>② 캐릭터 — 미니 스틱맨. <b>영원히 같은 그림</b>이다(장비/포즈를 반영하면 내비게이션
        /// 표지로서의 식별성을 잃는다 — 32-4 ②).
        ///
        /// <para>★ 2026-09-06 재조형(DESIGN_FAN_MENU_ICONS §6-②). 두 가지가 바뀌었다.</para>
        /// <para><b>(1) 머리가 링에서 「채운 원반」이 됐다.</b> 이 버튼은 「캐릭터로 가는 문」의 표지인데,
        /// 그 캐릭터 본체의 머리는 <c>1.171932 R</c>짜리 <b>채운 원</b>이다
        /// (docs/CHARACTER_BODY_AUDIT_2026-09-05.md §2). 표지가 가리키는 대상과 다른 문법으로 그려져
        /// 있었다. 카툰 비례로 캐리커처하는 것은 의도된 왜곡이다(글리프 머리/전신 0.31 vs 본체 0.23) —
        /// 작은 크기에서 머리를 키우지 않으면 사람으로 안 읽힌다.</para>
        /// <para><b>(2) 획 1.8 → 2.0(g0).</b> 다섯 칸 중 <b>혼자</b> 10 % 가늘어 1×에서 거미줄처럼
        /// 얇았다. 이제 사다리 위(FG-2)에 있다.</para>
        ///
        /// <para>여섯 조각은 전부 <b>한 덩어리로 용접</b>된다(머리 밑 y = 4.5가 척추 잉크 위 끝 5.5보다
        /// 아래라 진짜로 겹친다). 옛 형태는 정확히 <b>접선</b>(간극 0.00)이라 「한 덩어리」로도
        /// 「떨어진 둘」로도 판정되지 않는 애매한 자리였다.</para></summary>
        private Image[] BuildStickmanSymbol(ButtonView view)
        {
            Transform p = view.Symbol;
            var parts = new List<Image>(8);

            // ★★ 2026-08-30 회귀의 <b>생산자 측</b> 수정 — 부품 이름에 "Icon" 접두사를 붙인다.
            // 예전 이름은 "Head"/"ArmL"/"LegL"이었고, 그중 "Head"가 프리팹 캐릭터의 머리 앵커와
            // 글자 그대로 같은 이름이었다. 이름으로 캐릭터 파츠를 찾는 소비자(StickmanPoseAnimator /
            // StickmanMetrics / EyeController / DialogueBubbleRenderer / CharacterAccessoryRenderer)가
            // 이 UI 원을 진짜 머리로 착각해 캐릭터 머리·몸통이 영영 안 움직였다.
            // 소비자 쪽은 탐색 범위를 좁혀 이미 막았지만, 그 방어는 "지금의 계층 규약"에 기대는 것이라
            // 여기서 이름 충돌 자체를 없앤다(위 BuildUi의 씬 루트 부착과 합쳐 이중 차단).
            // ★ ringThickness를 <b>생략</b>하는 것이 곧 「채운 원반」이다(UiChrome.AddCircle).
            parts.Add(UiChrome.AddCircle(p, "IconHead", StickHeadDiameterPoints, UiChrome.TextPrimary,
                0f, new Vector2(0f, StickHeadCenterY)));

            UiChrome.AddPolyline(p, "IconSpine", StickSpinePath, SymbolStroke, UiChrome.TextPrimary, parts);
            UiChrome.AddPolyline(p, "IconArmL", StickArmLPath, SymbolStroke, UiChrome.TextPrimary, parts);
            UiChrome.AddPolyline(p, "IconArmR", StickArmRPath, SymbolStroke, UiChrome.TextPrimary, parts);
            UiChrome.AddPolyline(p, "IconLegL", StickLegLPath, SymbolStroke, UiChrome.TextPrimary, parts);
            UiChrome.AddPolyline(p, "IconLegR", StickLegRPath, SymbolStroke, UiChrome.TextPrimary, parts);

            return parts.ToArray();
        }

        // ====================================================================================
        // ③ 오늘 할일 — 체크리스트 (2행)
        // ====================================================================================
        // ★ 3행은 이 필드에서 <b>산술적으로 불가능하다</b>(DESIGN_FAN_MENU_ICONS §6-③).
        //   표식(빈 박스)의 최소 세로 = 구멍 1.5W(3.0) + 획 2×g1(2×1.5) = 6.0pt이고 행 간 골도
        //   1.5W = 3.0pt가 필요하다 ⇒ 3×6.0 + 2×3.0 = 24.0pt로 필드(Ø28의 세로 유효폭)를 꽉 채우고
        //   여백이 0이다. 옛 코드가 3행을 우겨넣느라 박스를 4.5pt·획 1.0pt(0.5W)까지 줄인 것이
        //   <b>1×에서 박스 둘이 점으로 뭉개지던 원인</b>이었다. 2행으로 내린다.
        //   「몇 개 남았는가」는 이미 <b>배지</b>가 나른다(BuildButton의 Todo 배지).

        private const float ChecklistRowY = 5f;          // 두 글줄의 |y|. 코어 간극 8.0pt(= 4W).
        private const float ChecklistMarkX = -7.75f;     // 표식 칸 중심 x.
        private const float ChecklistMarkCellPoints = 5f; // 꺾은선 경로 한 변 → 바깥 6.5 · 구멍 3.5(≥1.5W).
        private const float ChecklistLineX0 = -0.5f;
        private const float ChecklistLineX1 = 9.5f;

        /// <summary>빈 표식 상자 — <b>꺾은선 사각형</b>이다. <c>UiChrome.RoundedOutline</c>의 두께는
        /// <b>정수 텍셀(=정수 pt)</b> 뿐이라 g1(1.5pt)을 표현할 수 없다. 그래서 옛 <c>AddSmallBox</c>는
        /// 폐기됐다(호출부 0).</summary>
        private static readonly Vector2[] ChecklistBoxPath =
        {
            new Vector2(ChecklistMarkX - ChecklistMarkCellPoints * 0.5f, ChecklistRowY - ChecklistMarkCellPoints * 0.5f),
            new Vector2(ChecklistMarkX + ChecklistMarkCellPoints * 0.5f, ChecklistRowY - ChecklistMarkCellPoints * 0.5f),
            new Vector2(ChecklistMarkX + ChecklistMarkCellPoints * 0.5f, ChecklistRowY + ChecklistMarkCellPoints * 0.5f),
            new Vector2(ChecklistMarkX - ChecklistMarkCellPoints * 0.5f, ChecklistRowY + ChecklistMarkCellPoints * 0.5f),
            new Vector2(ChecklistMarkX - ChecklistMarkCellPoints * 0.5f, ChecklistRowY - ChecklistMarkCellPoints * 0.5f),
        };

        private static readonly Vector2[] ChecklistLine0Path =
            { new Vector2(ChecklistLineX0, ChecklistRowY), new Vector2(ChecklistLineX1, ChecklistRowY) };

        private static readonly Vector2[] ChecklistLine1Path =
            { new Vector2(ChecklistLineX0, -ChecklistRowY), new Vector2(ChecklistLineX1, -ChecklistRowY) };

        /// <summary>체크마크 — <b>꺾은선 한 조각</b>(옛 캡슐 2조각). 표식 칸 안에 앉고 아래 글줄과
        /// 3.55pt 떨어진다.</summary>
        private static readonly Vector2[] ChecklistCheckPath =
        {
            new Vector2(-9.6f, -4.8f),
            new Vector2(-7.8f, -6.6f),
            new Vector2(-5.6f, -2.8f),
        };

        /// <summary>③ 오늘 할일 — 체크리스트 <b>2행</b>(빈 표식 + 글줄 / 체크 + 글줄).
        /// <para>★ 2026-09-06 재조형(DESIGN_FAN_MENU_ICONS §6-③). 사라진 것이 둘이다.</para>
        /// <para><b>(1) <c>Strike</c>(취소선) 삭제.</b> 옛 코드는 그것을 <c>Line2</c>와 <b>정확히 같은
        /// 자리·같은 색</b>으로 그렸다 — 화면에 존재하지 않는 <see cref="Image"/> 한 개였다(FG-4가
        /// 금지하는 「100 % 가려진 조각」). 게다가 「선으로 그린 글줄」에 취소선을 그으면 원리상
        /// <b>더 굵은 선 하나</b>가 될 뿐이라 되살릴 이유도 없다.</para>
        /// <para><b>(2) 3행 → 2행.</b> 위 블록 주석의 산술.</para></summary>
        private Image[] BuildChecklistSymbol(ButtonView view)
        {
            Transform p = view.Symbol;
            var parts = new List<Image>(8);

            UiChrome.AddPolyline(p, "Box", ChecklistBoxPath, SymbolStrokeDetail, UiChrome.TextPrimary, parts);
            UiChrome.AddPolyline(p, "Line0", ChecklistLine0Path, SymbolStroke, UiChrome.TextPrimary, parts);
            UiChrome.AddPolyline(p, "Line1", ChecklistLine1Path, SymbolStroke, UiChrome.TextPrimary, parts);

            // 체크마크는 Accent 고정(완료를 뜻하는 유일한 색) — 심볼 색 보간 대상에서 제외하고
            // 알파만 따라가게 한다.
            // ★ FG-7(「Accent 고정 조각은 글리프당 최대 1개」)은 <b>조형 조각</b>을 세는 규칙이고
            //   여기 배열은 <b>Image</b>를 담는다. 조각은 꺾은선 <b>하나</b>이고 그것이 선분 2개로
            //   실현될 뿐이다 — 이 배열의 길이로 FG-7을 검사하지 마라(옛 형태는 캡슐 <b>2조각</b>을
            //   따로 놓아 꼭짓점 각도가 두 곳에 흩어져 있었다).
            var fixedParts = new List<Image>(2);
            UiChrome.AddPolyline(p, "Check", ChecklistCheckPath, SymbolStroke, UiChrome.Accent, fixedParts);
            view.SymbolFixedParts = fixedParts.ToArray();

            return parts.ToArray();
        }

        // ====================================================================================
        // ④ 행동 명령 — 확성기 (재작성)
        // ====================================================================================
        // ★ design-art F-L2 「확성기 → 지휘봉 교체」는 <b>이 크기에서 반증됐다</b>
        //   (DESIGN_FAN_MENU_ICONS §6-④, 후보 8안을 좌표로 만들어 실크기까지 렌더한 결과).
        //   · 「봉 19pt + 방사 3획」은 기하학적으로 불가능하다 — 수렴점에서 방사 3획이 서로 3.0pt를
        //     지키려면 안쪽 끝이 r₀ ≥ 8.2pt여야 하고 그러면 바깥 끝이 r ≈ 21pt로 필드(15pt)를 6pt 넘는다.
        //   · 십자 반짝임은 <b>금지 글리프</b>다: 대각 십자 = ✕(창 닫기 전용), 수직 십자 = +(증가·추가).
        //   · 남는 5안은 실크기에서 핀 / 펜 / 마이크 / 스와시 / 열쇠고리로 읽혔다.
        //   ⇒ <b>형태 교체가 아니라 조형 재작성</b>으로 이행한다(리더 판정, 2026-09-06).

        private const float MegaphoneMouthX = 5f;          // 나팔 입(오른쪽 변)의 x. 소리선 호의 중심이기도 하다.
        private const float MegaphoneWaveRadius = 6.6f;
        private const float MegaphoneWaveSpanDegrees = 38f;
        private const int MegaphoneWaveSegments = 8;       // FG-5: 원호는 8분할.

        /// <summary>나팔 — <b>닫힌 꺾은선 한 조각</b>(옛 낱획 4개). 옛 형태의 병은 「조각 넷이 서로
        /// 0.43pt로 붙어 있다」였고 그 0.43pt는 이 카탈로그 <b>전체 최악</b>이었다(램프까지 세면 골이
        /// −0.57px = 두 획이 1×에서 <b>한 줄로 합쳐진다</b>). 원래 한 물건이므로 한 조각으로 그리면
        /// 간극 규칙 자체가 사라진다(FG-3 ①「겹쳐서 한 덩어리」).</summary>
        private static readonly Vector2[] MegaphoneHornPath =
        {
            new Vector2(-8.4f, 2.6f),
            new Vector2(-8.4f, -2.6f),
            new Vector2(MegaphoneMouthX, -5.8f),
            new Vector2(MegaphoneMouthX, 5.8f),
            new Vector2(-8.4f, 2.6f),
        };

        /// <summary>손잡이 — 시작점 (−1.0, −4.37)은 나팔 <b>아랫변 위</b>다(x = −1.0에서 그 변의
        /// y = −2.6 + (7.4/13.4)×(−3.2) = −4.368) ⇒ 용접된다.
        /// <para>옛 주석은 <i>"24pt 상자에서 손잡이 획은 잉크 얼룩이 된다"</i>며 손잡이를 뺐는데, 그 결과
        /// 실루엣이 <b>스피커(볼륨) 아이콘과 같아졌다</b> — 이 앱에는 소리 설정이 따로 있다. 얼룩이 되지
        /// 않는 방법은 「가늘게」가 아니라 <b>「용접된 g0 한 획」</b>이다.</para></summary>
        private static readonly Vector2[] MegaphoneHandlePath =
        {
            new Vector2(-1f, -4.37f),
            new Vector2(-2.2f, -9.2f),
        };

        /// <summary>소리선 — 호 <b>1개</b>(옛 2개). 두 호가 서로 3.0pt를 지키려면 반경 차가
        /// <c>3.0 + 2×(W/2) = 5.0pt</c> 필요하고, 안쪽 호는 나팔 입(코어 x = 6.0)에서 3.0pt를 띄우느라
        /// 반경 6.6 아래로 못 내려가므로 바깥 호는 반경 11.6 — 그 먼 점이 <c>5.0 + 11.6 + 1.0 = 17.6pt</c>로
        /// 필드 상한 15pt를 2.6pt 넘는다. 옛 코드가 소리선 2개를 우겨넣느라 입과 1.50pt(0.75W)로
        /// 붙어 있었다.</summary>
        private static readonly Vector2[] MegaphoneWavePath = BuildArcPath(
            new Vector2(MegaphoneMouthX, 0f), MegaphoneWaveRadius,
            -MegaphoneWaveSpanDegrees, MegaphoneWaveSpanDegrees, MegaphoneWaveSegments);

        /// <summary>
        /// ④ 행동 — <b>확성기</b>(3조각: 닫힌 나팔 + 손잡이 + 소리선 1). 나머지 넷(스톱워치=원 /
        /// 스틱맨=수직 대칭 / 체크리스트=수평 줄 / 전원=트인 원)이 전부 좌우 대칭인 반면 확성기는
        /// <b>오른쪽을 향한 비대칭 실루엣</b>이라 축소에서도 형태가 충돌하지 않는다. 의미도 정확하다 —
        /// 이 버튼은 캐릭터의 상태를 <b>보는</b> 곳이 아니라 캐릭터에게 <b>시키는</b> 곳이고,
        /// 손잡이가 있는 확성기는 스피커(수동적 알림)가 아니라 <b>들고 외치는 도구</b>다.
        ///
        /// <para>배지도 상태 반영도 없다(32-4 ② — 내비게이션 표지는 영원히 같은 그림이다).</para>
        /// </summary>
        private static Image[] BuildMegaphoneSymbol(Transform p)
        {
            var parts = new List<Image>(16);
            UiChrome.AddPolyline(p, "Horn", MegaphoneHornPath, SymbolStroke, UiChrome.TextPrimary, parts);
            UiChrome.AddPolyline(p, "Handle", MegaphoneHandlePath, SymbolStroke, UiChrome.TextPrimary, parts);
            UiChrome.AddPolyline(p, "Wave", MegaphoneWavePath, SymbolStroke, UiChrome.TextPrimary, parts);
            return parts.ToArray();
        }

        /// <summary>원호 한 개를 <b>꺾은선 점 목록</b>으로 편다(FG-5 — 이 앱의 곡선은 전부 평탄화 꺾은선이다.
        /// 장비 카드 32종이 같은 방식이고, 부채꼴만 「직선 캡슐 + 정원」이라 문법이 갈려 있었다).
        /// <para>분할 수의 근거: 24pt 상자에서 8분할이면 r = 7pt·180° 기준 현 처짐이 <b>0.13pt</b>
        /// = 1×에서 0.13px다 — 보이지 않는다. 여기 호는 76°뿐이라 처짐이 0.03pt 아래다.</para></summary>
        private static Vector2[] BuildArcPath(Vector2 center, float radius, float startDegrees,
            float endDegrees, int segments)
        {
            segments = Mathf.Max(1, segments);
            var points = new Vector2[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                points[i] = center + Polar(Mathf.Lerp(startDegrees, endDegrees, t), radius);
            }
            return points;
        }

        /// <summary>위쪽이 트인 원호의 <b>틈 각도</b>(도). 세로획이 그 틈을 지난다.
        /// <para>★ 2026-09-06 50 → <b>62</b>. 세로획 ↔ 틈 안쪽 가장자리는
        /// <c>(d/2 − W)·sin(틈/2) − W/2</c>다. 옛 Ø20·50°는 <b>2.38pt = 1.19W</b>로 FG-3(1.5W)
        /// <b>미달</b>이었다(design-art R14 표는 여기를 3.23pt = 통과로 적었는데, 캡슐 캡 중심을
        /// ±L/2로 잡은 뒤 반지름을 한 번 더 빼는 계산 착오였다 — DESIGN_FAN_MENU_ICONS §1-2).
        /// 새 Ø22·62°는 <b>3.64pt = 1.82W</b>다. Ø20을 유지했다면 틈 하한이 60°였다
        /// (<c>8·sin30° − 1.0 = 3.0</c>).</para></summary>
        private const float PowerGapDegrees = 62f;

        /// <summary>전원 기호가 쓰는 원의 지름(pt) — 무장 카운트다운 링과 <b>같은 값</b>이다.
        /// <para>53-4가 "링이 그 원호와 같은 반지름에 겹치므로 도형이 하나 더 늘지 않는다"고 적은 것이
        /// 이 한 줄이다. 두 곳에 따로 적으면 하나만 바뀌는 날 카운트다운이 원호에서 벗어난다.</para>
        ///
        /// <para>★★ <b>2026-09-06 20 → 22 (리더 판정 승인).</b> 이 상수 하나가 ⑤와 ①의
        /// <b>실루엣 포함 관계</b>를 푼다. 실측(DESIGN_FAN_MENU_ICONS §6-⑤): Ø20에서 ⑤의 잉크는
        /// <b>94 %가 ①(스톱워치)에 포함</b>돼 고유 잉크가 <b>2.9 %</b>뿐이었다 — 옛 주석이
        /// <i>"원은 스톱워치와 공유하지만 트인 틈 + 관통하는 세로획이 구분한다"</i>고 적은 것은
        /// <b>의도였지 사실이 아니었다</b>. Ø22에서 쌍 IoU 0.702 → <b>0.316</b>, 고유 잉크
        /// 0.056 → <b>0.480</b>(FG-6 기준 IoU ≤ 0.35 · 고유 ≥ 0.45).
        /// Ø24가 실루엣상 더 좋지만 잉크 대각이 34.6pt로 FG-8 상한(32)을 넘고, 무엇보다
        /// <b>되돌릴 수 없는 버튼이 가장 크고 눈에 먼저 띄게</b> 된다.</para>
        ///
        /// <para>★ <b>옛 주석의 「정수 픽셀」 논거는 이 경로에 해당하지 않는다.</b> 여기 있던 문장은
        /// <i>"20 = 4×5라 ×1.25/1.5/1.75에서 25/30/35px 전부 정수"</i>였다(22는 27.5/33/38.5px).
        /// 그러나 <see cref="UiChrome.AddCircle"/>은 <c>CircleSprite</c>를 <c>Image.Type.Simple</c>로
        /// <b>어떤 크기로든 스케일</b>하고, <c>EdgeFeather</c> 0.5pt 알파 램프가 이미 가장자리를
        /// 지배한다. 정수 픽셀 논거는 <b>글리프 베이킹</b>(<c>Platform/UiGlyphScalePolicy.cs</c>,
        /// uGUI <c>Text</c>)의 것이지 이 스프라이트 경로의 것이 아니다.</para></summary>
        private const float PowerRingDiameterPoints = 22f;

        /// <summary>세로획의 아래 끝(pt). 위 끝은 <b>링 반지름에서 파생</b>한다(원이 커지면 같이 큰다) —
        /// 잉크는 <c>EdgeFeather</c> 밖의 코어 기준으로 y ∈ [0, 12]가 되어 링 바깥(11)을 1.0pt 뚫는다.</summary>
        private const float PowerStemBottomPoints = 1f;

        private static readonly Vector2[] PowerStemPath =
        {
            new Vector2(0f, PowerStemBottomPoints),
            new Vector2(0f, PowerRingDiameterPoints * 0.5f),
        };

        /// <summary>
        /// ⑤ 앱 종료 — <b>전원 기호</b>(위가 트인 원호 + 세로획). 2획.
        ///
        /// <para>★ <b><c>✕</c>를 쓰지 않는다</b>(53-5). <c>✕</c>는 이 앱에서 <b>「창 닫기」 전용
        /// 글리프</b>다(정보창 · 팝오버 · 설정창 · 할일 삭제 = 4곳). 부채꼴 위의 <c>✕</c>는
        /// <b>"메뉴 닫기"</b>로 먼저 읽힌다 — 되돌릴 수 없는 버튼에 가장 나쁜 오독이다.</para>
        ///
        /// <para>36-5의 규칙(<b>서로 다른 실루엣</b>)도 만족한다: 스톱워치(원+바늘) · 스틱맨(수직 대칭
        /// 인체) · 체크리스트(수평 줄) · 확성기(오른쪽 비대칭) 넷 중 어느 것과도 형태가 겹치지 않는다.
        /// 원은 스톱워치와 공유하지만 <b>트인 틈 + 관통하는 세로획</b>이 구분한다.</para>
        ///
        /// <para>★★ <b>바로 위 문장은 2026-09-06 전까지 「의도」였지 사실이 아니었다.</b> Ø20에서 실측하면
        /// 이 글리프의 잉크는 <b>94 %가 스톱워치에 포함</b>됐다(쌍 IoU 0.735 · 고유 잉크 <b>0.029</b>) —
        /// 「트인 틈 + 세로획」이 실루엣을 실제로 가르지 못했다. <see cref="PowerRingDiameterPoints"/>를
        /// 22로 올린 지금은 <b>IoU 0.316 · 고유 잉크 0.480</b>이라 그 문장이 <b>측정으로도 참</b>이다.
        /// 되돌리면 문장이 다시 거짓이 된다.</para>
        ///
        /// <para>★ 위 숫자는 전부 <b>오프라인 래스터</b>다. <b>실기 캡처는 아직 없다</b> —
        /// 최종 판정은 실제 빌드 캡처로만 한다(CLAUDE.md 디자인 공통 규약).</para>
        /// </summary>
        private Image[] BuildPowerSymbol(ButtonView view)
        {
            Transform p = view.Symbol;
            var parts = new List<Image>(4);

            Image ring = UiChrome.AddCircle(p, "PowerRing", PowerRingDiameterPoints,
                UiChrome.TextPrimary, SymbolStroke);
            parts.Add(ring);
            // 위쪽을 <b>틔운다</b>. Filled/Radial360은 시작점(Top)에서 시계 방향으로 채우므로 남는
            // 틈이 시작점 <b>바로 앞</b>에 생긴다 — 그대로 두면 틈이 왼쪽 위로 치우친다. 링은 틈만
            // 빼면 회전 대칭이라, 링 자체를 틈의 <b>절반</b>만큼 되돌리면 틈이 정확히 위로 온다.
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.fillAmount = 1f - PowerGapDegrees / 360f;
            ring.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -PowerGapDegrees * 0.5f);

            // 세로획 — 원 중심에서 틈을 지나 위로. 위 끝은 원 반지름에서 파생시킨다(원이 커지면 같이 큰다).
            UiChrome.AddPolyline(p, "PowerStem", PowerStemPath, SymbolStroke, UiChrome.TextPrimary, parts);

            // ---- 무장 카운트다운 링 ---- 평소에는 꺼져 있다(도형이 하나 더 늘지 않는다는 말의 실체는
            //   "같은 반지름 위에 겹친다"이지 "Image가 0개"가 아니다 — 채움 비율을 그리려면 별도 채널이
            //   필요하고, 그건 집중 모드의 잔여 시간 호와 <b>같은 부품(RingFill)</b>을 재사용한다).
            view.RingFill = UiChrome.AddCircle(p, "QuitCountdown", PowerRingDiameterPoints,
                UiChrome.WarmAccent, SymbolStroke);
            view.RingFill.type = Image.Type.Filled;
            view.RingFill.fillMethod = Image.FillMethod.Radial360;
            view.RingFill.fillOrigin = (int)Image.Origin360.Top;
            view.RingFill.fillClockwise = true;
            view.RingFill.fillAmount = 1f;
            view.RingFill.gameObject.SetActive(false);

            return parts.ToArray();
        }

        // ★ 2026-09-06 삭제: `AddSmallBox`(4.5pt 정사각 + RoundedOutline(2, 1)).
        //   호출부는 체크리스트의 빈 표식 상자 2개뿐이었고 그 둘이 2행 재조형에서 <b>꺾은선 사각형</b>
        //   하나로 바뀌었다. 되살리지 마라 — `UiChrome.RoundedOutline`의 두께 인자는 <b>정수 텍셀</b>
        //   (= 정수 pt)이라 FG-2 사다리의 g1(1.5pt)을 표현할 수 없고, 옛 호출이 쓰던 두께 1pt는
        //   0.5W로 사다리 밖이었다(1×에서 박스가 점으로 뭉갠 원인).

        private static Vector2 Polar(float degrees, float radius)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r) * radius, Mathf.Sin(r) * radius);
        }
    }
}
