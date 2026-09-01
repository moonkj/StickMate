using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 화면 우상단 상시 <b>톱니 한 개</b> — 정보/장비 창의 주 진입점.
    /// (2026-09-01 P0-3로 두 개 -> 한 개가 됐다. 아래 "단일 기어로 바꿨다" 절이 그 산술 근거다 —
    ///  이 첫 줄이 여덟 줄 뒤에서 스스로 부정당하고 있었다.)
    /// 2026-08-29 사용자 원문: "바탕화면 오른쪽 상단에 기어 표시같은걸 띄워놓고 클릭하면 기어가
    /// 회전하면서 캐릭터 창이 나오게끔".
    /// 2026-08-30 사용자 원문: "바탕화면 기어표시도 지금 너무 단순하게 되어있잖아 클릭하면
    /// <b>큰기어와 작은기어가 맞물려 움직이면서</b> 캐릭터 창이뜨게.. 기어의 디자인도 좀 멋있게 바꿔줘".
    ///
    /// ============================================================================
    /// ★ 2026-09-01 — 맞물린 두 기어를 <b>단일 기어</b>로 바꿨다 (P0-3, 리더 승인)
    /// ============================================================================
    /// 위 요청("큰기어와 작은기어가 맞물려")은 <b>지켜지지 않는다</b>. 그 사실을 여기 남겨 둔다.
    ///
    /// 이유는 취향이 아니라 산술이다. 사용자 신고 "깔끔한 게 하나도 없어"의 최상위 결함이
    /// <b>이 아이콘이 배경에 따라 아예 안 보인다</b>(회색 전 구간 보장 대비 1.00:1)는 것이었고,
    /// 그 해법인 역상 헤일로 2겹은 <b>획 폭의 2.2배(3.74pt)</b>를 먹는다. 그런데 옛 작은 기어의
    /// 이 골은 <b>1.68pt</b>뿐이라(이 높이 − 획 = −0.02pt = 톱니가 물리적으로 없다) 헤일로를
    /// <b>한 겹도</b> 넣을 수 없었다. 두 기어를 유지하려면 묶음을 2.82배(bbox 약 102pt)로 키워야 하고,
    /// 그건 화면 구석 아이콘이 아니다. 자세한 계산은 아래 형태 상수 블록에 적어 뒀다.
    ///
    /// 남긴 것: 클릭하면 <b>기어가 회전하며</b> 창이 뜨는 연출(원문의 "회전하면서"), 그리고
    /// "단순하지 않은" 조형(사다리꼴 6이 + 허브 링). 잃은 것: 두 번째 기어와 맞물림 기구학.
    /// 되돌리려면 이 파일의 형태 상수와 Tests/PlayMode/InfoGearMeshingTests.cs를 함께 되살리면 된다
    /// (그 테스트는 지우지 않고 <c>Assert.Ignore</c>로 남겨 러너에 계속 보이게 해 뒀다).
    ///
    /// ============================================================================
    /// 왜 uGUI가 아니라 LineRenderer인가
    /// ============================================================================
    /// 이 앱의 시각 요소는 전부 프로시저럴 선화다(스프라이트 에셋이 없다). 캐릭터와 같은 LineRenderer +
    /// <b>캐릭터의 머티리얼을 빌려 쓰는</b> 관례를 그대로 따르면 그림체도 일관되고(잉크색 전환에도 함께
    /// 따라간다) 의존성도 늘지 않는다.
    ///
    /// ============================================================================
    /// 크기는 <b>화면 고정</b>이다 — 캐릭터 배율을 따라가지 않는다
    /// ============================================================================
    /// 캐릭터에 붙는 액세서리와 정반대의 규칙이다. 이건 캐릭터의 일부가 아니라 화면 구석의 작은 UI
    /// 버튼이라, 캐릭터가 커지고 작아진다고 함께 커지면 이상하다(리더 지시). 그래서 모든 치수를
    /// <b>OS 포인트</b>로 정하고 ScreenCoordinateConverter로 환산한다(Retina에서도 물리적으로 같은 크기).
    ///
    /// ============================================================================
    /// 클릭 판정 — 기존 메커니즘을 그대로 재사용한다(새로 만들지 않는다)
    /// ============================================================================
    ///  (1) 아이콘 영역을 덮는 isTrigger BoxCollider2D — UniWindowController의 hitTestType=Raycast가
    ///      "커서 아래 Collider2D가 있는가"로 클릭관통을 판정하므로, <b>이 작은 영역만</b> 클릭을 받고
    ///      나머지 화면은 100% 관통 그대로다. 이번 라운드에 기어가 둘이 되면서 그 사각형은
    ///      <b>두 기어를 함께 덮는 최소 사각형</b>으로 넓어졌다(그 이상은 넓히지 않는다).
    ///  (2) 전역 폴링(IGlobalPointerButtonService + 커서 좌표) — macOS에서 비활성 앱의 첫 클릭이
    ///      "앱 활성화"에만 소비되는 경우에도 확실히 잡는다.
    /// <b>비침해 보장</b>: (2)는 "버튼이 눌렸다"만으로는 아무 일도 하지 않는다. 반드시 그 순간 커서가
    /// 아이콘 사각형 <b>안</b>일 때만 반응한다.
    ///
    /// <b>메뉴바를 피한다</b>: 세로 여백 <see cref="MarginTopPoints"/>는 macOS 메뉴바(노치 기준 최대 약
    /// 38pt)보다 확실히 아래에 아이콘을 놓기 위한 값이다.
    ///
    /// ============================================================================
    /// 짧게 클릭 vs 길게 눌러 옮기기 (2026-08-30 사용자 요청)
    /// ============================================================================
    /// 사용자 원문: "캐릭터 설정 기어들도 길게 클릭해서 위치 옮길 수 있게 해줘".
    ///  · <b>짧게 클릭</b> — 기어가 도는 것과 <b>동시에</b> 부채꼴 버튼 4개가 펼쳐진다
    ///    (2026-08-31에 [행동]이 늘어 3 -> 4가 됐다. 개수의 단일 출처는 <see cref="GearRadialMenuWidget.ButtonCount"/>).
    ///  · <b>길게 누르기</b>(<see cref="LongPressSeconds"/> 이상) 또는 누른 채
    ///    <see cref="DragMoveThresholdPoints"/> 이상 이동 — 드래그로 전환되어 커서를 따라간다.
    ///    떼면 그 자리에 확정되고 저장 파일에 남아 <b>재시작해도 유지</b>된다(Core/UiLayoutModel.cs).
    ///
    /// <b>왜 클릭 판정이 뗄 때로 옮겨갔는가</b>: 누른 순간에 창을 열면 그 클릭이 드래그가 될지 아직
    /// 모른다 — 옮기려고 눌렀는데 창부터 뜨는 것이 이 요구에서 가장 흔한 실패다. 그래서 "눌렀다"는
    /// 즉시 아무 일도 하지 않고, <b>뗄 때</b> 드래그였는지 아닌지가 확정된 뒤에 창을 연다.
    ///
    /// <b>판정 영역도 함께 따라간다</b>: 히트 사각형/콜라이더는 매 프레임 현재 중심에서 다시 계산되므로
    /// 드래그 중에도 커서가 계속 "기어 위"다(States/DragThrowState의 개념과 같지만, 이쪽은 물리 바디가
    /// 아니라 화면 좌표 UI라 힘이 아니라 좌표를 직접 옮긴다). 드래그가 아닐 때 그 사각형 밖 클릭이
    /// 걸리지 않는다는 비침해 보장은 예전과 완전히 동일하다.
    ///
    /// <b>화면 밖으로 못 나간다</b>: 중심이 아니라 <b>두 기어를 덮는 사각형 전체</b>를 화면 안으로
    /// 클램프한다. 저장된 위치가 (외장 모니터 분리 등으로) 화면 밖이 된 경우에도 다음 프레임에 그대로
    /// 끌려 들어오고, 그 보정값이 다시 모델로 되돌아가 저장된다.
    ///
    /// ============================================================================
    /// 클릭 -> 회전 -> 부채꼴 메뉴 (2026-08-30 사용자 요청)
    /// ============================================================================
    /// 사용자 원문: "기어메뉴를 클릭했을때 집중모드 버튼 캐릭터 버튼 오늘 할일 버튼 3가지가 촤르륵
    /// 원버튼 3개가 나오고 각 버튼을 클릭했을때 세부 메뉴로 들어가도록".
    /// 클릭한 프레임에 <see cref="GearRadialMenuWidget"/>가 원형 버튼 4개를 펼치기 시작하고, 동시에
    /// 기어가 <see cref="SpinSeconds"/> 동안 돈다(P0-3 이전에는 두 기어가 맞물려 돌았다).
    /// <b>회전과 펼침은 동시에 시작한다</b>(docs/UX_FLOW.md 32-9 (B)) — 회전이 끝나기를 기다리면
    /// 클릭부터 첫 픽셀까지 520ms가 걸리고, 그동안 아무 변화가 없으면 사용자는 "안 먹었다"고 판단해
    /// 한 번 더 누른다. 그 두 번째 클릭이 토글 접힘이 되어 메뉴가 깜빡이는 실패 모드가 구조적으로
    /// 생긴다. 기어가 아직 돌고 있는 동안 버튼이 안착하므로 "톱니를 돌려 버튼을 뽑아냈다"는 인과가
    /// 오히려 더 또렷해진다.
    ///  · <b>기어 재클릭</b> — 펼쳐져 있으면 접는다(토글).
    ///  · <b>부채꼴 바깥 클릭</b> — 접는다. 그 클릭을 우리가 먹지는 않는다(메뉴의 표준 관례이자 비침해).
    ///  · <b>길게 누르기</b> — 예전 그대로 이동이다. 드래그로 전환되는 순간 메뉴는 접힌다(기어만 따라가고
    ///    버튼들이 뒤에 남아 끌려다니는 그림을 만들지 않는다).
    /// 톱니는 <b>이동</b>이 필요해 뗄 때 판정하고, 부채꼴 버튼은 이동이 없으므로 누른 버튼 위에서 뗐을
    /// 때만 발동한다(버튼 밖으로 끌고 나가 떼면 취소 — 모든 OS의 버튼 관례).
    ///
    /// <b>클릭관통 차단 영역이 함께 넓어진다</b>: 콜라이더는 톱니 사각형이 아니라
    /// <see cref="InteractiveScreenRect"/>(톱니 + 펼쳐진 버튼들의 합집합)로 잡는다. 안 그러면 버튼을
    /// 눌러도 그 클릭이 밑의 앱으로 새어 나간다. 메뉴가 접히면 즉시 예전 크기로 돌아온다.
    /// </summary>
    public sealed class InfoGearIconWidget : MonoBehaviour
    {
        // ==================== 화면 고정 치수(전부 OS 포인트) ====================

        /// <summary>화면 오른쪽 끝에서 <b>큰 기어 중심</b>까지의 거리.</summary>
        private const float MarginRightPoints = 30f;

        /// <summary>화면 위쪽 끝에서 <b>큰 기어 중심</b>까지의 거리. macOS 메뉴바(최대 약 38pt)보다
        /// 확실히 아래여야 한다 — 클래스 문서 "메뉴바를 피한다" 참고.</summary>
        private const float MarginTopPoints = 58f;

        // ============================================================================
        // ★ 2026-09-01 P0-3 — <b>어떤 잉크색으로도 안 보이던 아이콘</b>을 2겹(헤일로 + 잉크)으로 고친다
        // ============================================================================
        //
        // 결함(docs/UI_SURFACE_SPEC.md §5.1): 잉크는 캐릭터 잉크 고정(기본 검정)이고 배경 보정이 없는데,
        // 이 아이콘만은 <b>유저의 임의의 데스크톱</b> 위에 맨몸으로 놓인다.
        //     검정 잉크의 회색 전 구간 <b>최악 대비 = 1.00 : 1</b>  (거의 검은 배경에서 완전히 사라진다)
        //     흰 잉크로 뒤집어도 흰 배경에서 1.00 : 1 — 문제가 이동할 뿐이다.
        // <b>단색으로는 원리상 해결 불가</b>다. 그래서 만화 레터링의 Outline과 같은 관용구를 쓴다:
        // 같은 경로를 <b>잉크의 역상</b>으로 먼저 굵게 긋고(헤일로), 그 위에 잉크를 긋는다.
        // 보장 대비는 <see cref="ResolveHaloColor"/>가 계산하고
        // Tests/PlayMode/InfoGearContrastTests가 회색 0~255 전 구간을 훑어 ≥3:1을 확인한다.
        //
        // ---------------------------------------------------------------------------
        // ★ 왜 형태까지 함께 바꿨나 — 헤일로와 옛 형태는 <b>공존이 불가능</b>했다 (산술)
        // ---------------------------------------------------------------------------
        // 두 획 사이의 <b>남는 빈 폭</b> = (중심선 간격) − (획 폭)이다. 옛 형태의 반지름 간격은
        //     큰 기어  허브3.6→림7.0 = 3.4 / 림7.0→뿌리10.2 = 3.2 / 뿌리10.2→팁13.0 = <b>2.8</b>
        //     작은 기어 허브2.92→뿌리6.12 = 3.20 / 뿌리6.12→팁7.80 = <b>1.68</b>
        // 헤일로 폭은 <c>획 × 2.2 = 3.74pt</c>다. <b>1.68 &lt; 3.74</b> 이므로 작은 기어의 이 골은
        // 헤일로만으로 완전히 메워진다. 옛 형태에서 가능한 최대 헤일로 배율은 1.68/1.7 = <b>0.99배</b>
        // — 즉 <b>헤일로를 한 겹도 넣을 수 없다</b>. 형태를 안 바꾸면 P0-3은 구현이 불가능하다.
        //
        // 그리고 애초에 옛 작은 기어에는 <b>이가 없었다</b>: 이 높이 1.68 − 획 1.7 = <b>−0.02pt</b>
        // (= −0.01획). 톱니바퀴가 아니라 울퉁불퉁한 고리 하나였다(§5.2 실측).
        //
        // <b>두 기어를 유지한 채</b> 헤일로를 넣으려면 작은 기어의 이 높이가 3.74 + 여유 1.0 = 4.74pt
        // 이상이어야 하고, 그러려면 지금의 2.82배로 키워야 한다 — 묶음 bbox가 36×31pt에서 약
        // <b>102pt</b>가 된다. 화면 구석 아이콘으로 성립하지 않는다. <b>단일 기어가 강제된다.</b>
        //
        // ---------------------------------------------------------------------------
        // 신규 형태 — 요소 3겹(허브 링 / 이 윤곽) · 6이 · 획 1.7 · 헤일로 2.2배
        // ---------------------------------------------------------------------------
        // 잇수 6은 <b>헤일로까지 계산한 상한</b>이다. 이 하나와 골 하나가 각각 헤일로 폭 + 여유 1.0pt
        // (= 4.74pt)를 먹으므로 뿌리원 둘레는 N × 9.48pt 이상이어야 한다. 뿌리 r 9.0의 둘레는
        // 56.5pt라 N ≤ 5.96 → <b>6</b>. (스펙 문서의 8이는 헤일로를 계산에 넣지 않은 값이다.)
        //
        //  구간                     중심선Δ    잉크 여유      헤일로 여유
        //  허브4.2 → 뿌리9.0         4.80      3.10 = 1.82획   1.06pt
        //  뿌리9.0 → 팁13.8(이 높이)  4.80      3.10 = 1.82획   1.06pt
        //  이 폭 @뿌리(호)           4.71      3.01 = 1.77획   0.97pt
        //  골 폭 @뿌리(호)           4.71      3.01 = 1.77획   0.97pt
        //  이 폭 @팁(호)             4.91      3.21 = 1.89획   1.17pt
        // 전 구간 <b>잉크 여유 ≥ 1.77획</b>(규칙 1.5획) · <b>헤일로 여유 ≥ 0.97pt</b>(Retina에서 2물리픽셀).
        // bbox 31.3 × 31.3pt — 옛 묶음(36.2 × 31.2)보다 작고, 방사 대칭이라 <b>광학 중심 = 히트 중심</b>이다.

        private const float TipRadiusPoints = 13.8f;    // 이 끝(팁원).
        private const float RootRadiusPoints = 9.0f;    // 이 뿌리(루트원).
        private const float HubRadiusPoints = 4.2f;     // 가운데 축(링).
        private const int ToothCount = 6;

        private const float StrokeWidthPoints = 1.7f;
        private const float HitPaddingPoints = 5f;

        /// <summary>헤일로 획 폭 = 잉크 획 × 이 값. 2.2는 스펙 값이면서 <b>이 형태가 감당하는 상한</b>이다
        /// (위 표: 가장 좁은 구간이 4.71pt이고 2.2배 헤일로가 3.74pt라 0.97pt가 남는다).
        /// 더 키우면 이 골이 메워져 톱니가 원반이 된다 — 스펙의 호버 3.0배를 채택하지 않은 이유다.</summary>
        private const float HaloWidthFactor = 2.2f;

        /// <summary>헤일로가 잉크 바깥으로 번져 나가는 반경(양쪽 각각). 히트/클램프 계산에 쓴다.</summary>
        private const float HaloOverhangPoints = StrokeWidthPoints * (HaloWidthFactor - 1f) * 0.5f;

        /// <summary>시각 반경 = 팁원 + 헤일로가 삐져나온 만큼.</summary>
        private const float VisualRadiusPoints = TipRadiusPoints + HaloOverhangPoints;

        // 이 프로필(피치 대비 비율) — 사다리꼴 이를 또렷하게 만든다.
        private const float ToothTipHalfFraction = 0.17f;    // 이 끝(마루)의 반각.
        private const float ToothRootHalfFraction = 0.25f;   // 이 뿌리의 반각(마루보다 넓어야 사다리꼴).

        private const float SpinSeconds = 1.4f;     // 2026-09-01 페르소나(민지) 발견 M5: SpinTurns를 4까지 올리면서
                                                     // 이 값을 그대로 두면 ease-out 시작 순간 각속도가 138°/프레임(60fps)까지
                                                     // 치솟아 이 6개가 앨리어싱으로 뭉개진다(옛 잇수10 묶음에서 처음 발견). 회전량에 비례해
                                                     // 늘려 체감 가독 구간을 넓힌다(0.52 -> 1.4, 민지 권장 범위 1.2~1.6 중간값).
        private const float SpinTurns = 4f;         // 회전량(2026-09-01 사용자 요청으로 0.75 -> 1.25 -> 4).
        /// <summary>
        /// 평소 불투명도. "관찰형 앱이라 은은하게"가 이 값의 근거였고 그 근거는 지금도 유효하지만,
        /// <b>0.70은 P0-3의 대비 보장을 먹어 버린다</b>.
        ///
        /// <para>알파는 <b>두 겹 모두</b>에 걸리므로, 화면에 실제로 나오는 색은
        /// <c>헤일로' = mix(헤일로, 배경, α)</c> / <c>잉크' = mix(잉크, 헤일로', α)</c>다.
        /// 즉 알파가 낮을수록 두 겹이 <b>배경 쪽으로 함께 끌려간다</b>. 회색 전 구간 실측:</para>
        /// <code>
        ///   α 0.70 → 보장 대비 2.65 : 1   ✘ (비텍스트 최소 3:1 미달)
        ///   α 0.75 → 2.90 : 1             ✘
        ///   α 0.80 → 3.19 : 1             ✔
        ///   α 1.00 → 4.37 : 1
        /// </code>
        /// <para>스펙(§5.1)이 계산한 4.18은 <b>α = 1 전제</b>였고, 이 앱이 실제로 그리는 알파를
        /// 계산에 넣지 않았다. 그래서 0.70 → <b>0.80</b>. 여전히 "은은하게"이면서 최소 보장을 넘는다.
        /// Tests/EditMode/InfoGearHaloContrastTests가 <b>이 상수를 읽어</b> 합성까지 재현해 검사한다.</para>
        /// </summary>
        private const float IdleAlpha = 0.80f;

        private const float ActiveAlpha = 0.95f;    // 커서가 위에 있거나 창이 열려 있을 때.
        private const float AlphaFadeSpeed = 6f;

        /// <summary>호버할 때 살짝 커진다 — <b>알파가 아니라 크기</b>가 주 단서다.
        /// α 0.70 → 0.95(36% 변화)는 저대비 배경에서 사실상 아무 일도 안 일어난 것과 같다(§5.1).
        /// 스펙은 "헤일로를 3.0배로"라고 했지만 그러면 이 골(4.71pt)이 헤일로(5.1pt)에 메워져
        /// 톱니가 원반이 된다 — 배경 휘도와 무관한 단서라는 <b>목적은 같고</b> 형태가 안 무너지는
        /// 수단으로 바꿨다. 균일 배율이라 위 여유 표의 <b>비율이 전부 보존</b>된다.</summary>
        private const float HoverScale = 1.10f;

        private const float ClickPollInterval = 0.05f;
        private const int SortingOrder = 40;        // 캐릭터/액세서리보다 위(화면 UI다).

        // ---- 길게 눌러 옮기기 ----

        /// <summary>이만큼 누르고 있으면 드래그로 전환된다. 0.4초는 "실수로 길게 눌리는" 일이 드물면서
        /// 옮기려는 사람이 답답함을 느끼기 전인 구간이다(macOS Dock/홈 화면 아이콘 정리와 같은 감각).</summary>
        private const float LongPressSeconds = 0.4f;

        /// <summary>시간을 채우기 전이라도 이만큼(OS 포인트) 끌면 즉시 드래그다 — 일반적인 드래그 UX
        /// 관례. 손떨림(1~2pt)으로는 넘지 않는 값이어야 짧은 클릭이 드래그로 오인되지 않는다.</summary>
        private const float DragMoveThresholdPoints = 4f;

        /// <summary>드래그 중 시각 피드백 — 살짝 커지고(들어올린 느낌) 살짝 옅어진다(화면에서 떠 있다는
        /// 표시). 회전과 충돌하지 않는다: 회전은 자식(큰/작은 기어)의 각도, 이건 부모의 스케일/알파다.</summary>
        private const float DragScale = 1.12f;
        private const float DragScaleSpeed = 8f;
        private const float DragAlpha = 0.55f;

        private StickmanAgent _agent;
        private StickConfig _config;
        private CharacterInfoWindow _window;
        private FocusWatchDirector _focusDirector;   // 지연 탐색 후 캐시(AppControlDirector와 같은 관례).
        private TodoReminderDirector _todoDirector;
        private IGlobalPointerButtonService _buttonService;
        private Camera _camera;

        private GameObject _container;
        private Transform _gear;
        private readonly List<LineRenderer> _lines = new List<LineRenderer>(10);
        private BoxCollider2D _clickTarget;
        private Material _lineMaterial;

        private float _spinTimer = -1f;   // 음수 = 회전 중 아님.
        private float _alpha = IdleAlpha;
        private bool _highlighted;
        private float _clickPollTimer;
        private bool _leftPrev;
        private bool _leftInitialized;

        // ---- 길게 눌러 옮기기 상태 ----
        private bool _hasCustomCenter;          // 사용자가 옮긴 적이 있는가(없으면 매 프레임 기본 위치를 다시 계산).
        private Vector2 _customCenterPoints;    // 창 좌상단 원점, OS 포인트(UiLayoutModel과 같은 좌표계).
        private bool _restoredFromSave;
        private bool _pressActive;
        private bool _dragging;
        private float _pressStartTime;
        private Vector2 _pressStartCursor;      // Unity 스크린 픽셀.
        private Vector2 _grabOffsetPoints;      // 잡은 순간의 (중심 - 커서). 기어가 커서로 순간이동하지 않게 한다.
        private float _visualScale = 1f;

        // ---- 부채꼴 메뉴 ----
        private GearRadialMenuWidget _menu;
        private int _menuPressIndex = -1;   // 지금 누르고 있는 버튼(-1 = 없음).

        private bool _builtGeometry;
        private float _builtRadiusWorld = -1f;
        private Color _builtInk = new Color(-1f, -1f, -1f, -1f);

        /// <summary>전체화면 감지로 <b>우리가</b> 숨긴 상태인가 — 복귀 판정과 로그 1회 출력에 쓴다.</summary>
        private bool _hiddenBySuspend;

        /// <summary>지금 회전 연출 중인가(테스트/진단 전용).</summary>
        public bool IsSpinning => _spinTimer >= 0f;

        /// <summary>톱니 그림이 실제로 켜져 있는가(진단/테스트 전용). <b>플래그가 아니라 GameObject의
        /// 실제 상태</b>를 돌려준다 — "숨겼다고 기록은 됐는데 화면에는 남아 있다"를 잡기 위해서다.</summary>
        public bool IsIconVisible => _container != null && _container.activeSelf;

        /// <summary>톱니의 클릭관통 차단막이 켜져 있는가(진단/테스트 전용). 전체화면 감지 중에 이것이
        /// 켜져 있으면 <b>보이지 않는데 클릭만 먹는</b> 최악의 형태가 된다(비침해 원칙 2).</summary>
        public bool IsClickBlockerEnabled => _clickTarget != null && _clickTarget.enabled;

        /// <summary>큰 기어 중심의 Unity 스크린 좌표(픽셀). 실측 검증용.</summary>
        public Vector2 IconScreenCenter { get; private set; }

        /// <summary>두 기어를 함께 덮는 히트 사각형(Unity 스크린 픽셀). "이 밖에서는 절대 안 걸린다"를
        /// 테스트가 직접 확인할 수 있게 노출한다.</summary>
        public Rect IconScreenRect { get; private set; }

        /// <summary>톱니 + <b>펼쳐진 부채꼴 버튼</b>을 함께 덮는 사각형(Unity 스크린 픽셀). 클릭관통
        /// 차단 콜라이더가 쓰는 값이며, 메뉴가 접혀 있으면 <see cref="IconScreenRect"/>와 같다.</summary>
        public Rect InteractiveScreenRect { get; private set; }

        /// <summary>부채꼴이 펼쳐져 있는가(펼치는 중 포함).</summary>
        public bool IsMenuExpanded => _menu != null && _menu.IsExpanded;

        /// <summary>부채꼴 그림이 화면에 남아 있는가(접히는 중 포함).</summary>
        public bool IsMenuVisible => _menu != null && _menu.IsVisible;

        /// <summary>부채꼴 버튼 중심(Unity 스크린 픽셀) — 실측/테스트가 이 좌표로 클릭을 먹인다.</summary>
        public Vector2 MenuButtonScreenCenter(GearMenuButton button)
            => _menu != null ? _menu.ButtonScreenCenter((int)button) : Vector2.zero;

        /// <summary>버튼의 펼침 진행도(0~1).</summary>
        public float MenuButtonProgress(GearMenuButton button)
            => _menu != null ? _menu.ButtonProgress((int)button) : 0f;

        /// <summary>세 버튼이 전부 펼쳐지는 데 걸리는 시간(초) — 테스트가 이 값만큼만 기다리면 된다.</summary>
        public static float MenuExpandTotalSeconds => GearRadialMenuWidget.ExpandTotalSeconds;

        /// <summary>
        /// 클릭 후 <b>다음 클릭이 먹기까지</b>의 시간(초). 그림(부채꼴 펼침)과 <b>손잡이가 다시 살아나는
        /// 시점</b>이 다르면 큰 쪽을 쓴다.
        ///
        /// <para>★ 2026-09-01 — 예전에는 <see cref="GearRadialMenuWidget.ExpandTotalSeconds"/>(0.30초)
        /// 하나였다. 회전이 0.52초여도 펼침과 <b>동시에</b> 진행되므로 그림은 0.30초면 끝났기 때문이다.
        /// 그런데 <see cref="ActivateClick"/>은 첫 줄이 <c>if (IsSpinning) return;</c>이라 <b>회전이 끝날
        /// 때까지 클릭 자체를 먹지 않는다</b>. 그림만 보고 만든 이 값은 그 사실을 몰랐고, M5 조치로
        /// 회전이 0.52 → <see cref="SpinSeconds"/> 1.4초가 되는 순간 "다 펼쳐졌다고 알려 준 시점에
        /// 눌렀는데 아무 일도 안 일어난다"가 됐다(부채꼴 토글 회귀 3건이 그 자리에서 깨졌다).</para>
        ///
        /// <para>고친 것은 <b>숫자가 아니라 정의</b>다 — 이 값은 "그림이 끝나는 시각"이 아니라
        /// <b>"조작이 다시 먹는 시각"</b>이고, 그래서 두 게이트의 <see cref="Mathf.Max"/>다.
        /// 회전 시간을 바꾸는 사람이 이 값을 따로 기억할 필요가 없어진다.</para>
        /// </summary>
        public static float MenuReadySeconds
            => Mathf.Max(GearRadialMenuWidget.ExpandTotalSeconds, SpinSeconds);

        /// <summary>기어의 현재 회전각(도). 회귀 테스트가 회전 연출이 살아 있는지 직접 잰다.</summary>
        public float GearAngleDegrees => _gear != null ? _gear.localEulerAngles.z : 0f;

        /// <summary>잇수 — 형태 회귀 테스트가 이 값에서 이/골의 호 길이를 다시 계산한다.</summary>
        public static int Teeth => ToothCount;

        /// <summary>형태 여유 계산에 필요한 반지름 3종과 획/헤일로(전부 OS 포인트).
        /// 테스트가 숫자를 베끼지 않고 <b>이 값들로 직접 재도록</b> 열어 둔다(CLAUDE.md).</summary>
        public static float TipRadius => TipRadiusPoints;

        public static float RootRadius => RootRadiusPoints;
        public static float HubRadius => HubRadiusPoints;
        public static float StrokeWidth => StrokeWidthPoints;
        public static float HaloWidth => StrokeWidthPoints * HaloWidthFactor;
        public static float ToothTipHalfFraction_ForTests => ToothTipHalfFraction;
        public static float ToothRootHalfFraction_ForTests => ToothRootHalfFraction;

        /// <summary>
        /// ★ 헤일로 색 — <b>잉크에서 더 멀리 떨어진 쪽</b>을 고른다(임계값을 손으로 정하지 않는다).
        /// 잉크가 검정이면 밝은 쪽(#f2f4f7), 흰색이면 어두운 쪽(#0b1016)이 뽑힌다.
        /// <para>이 한 쌍이 명도 축의 양 끝에 있으므로, <b>어떤 배경이 와도</b> 둘 중 하나는 반드시
        /// 충분한 대비를 낸다: 회색 전 구간 최악값이 <b>4.37 : 1</b>(WCAG 비텍스트 최소 3:1을
        /// 46% 상회). 그 단언은 Tests/PlayMode/InfoGearContrastTests가 전 구간을 훑어서 한다.</para>
        /// </summary>
        public static Color ResolveHaloColor(Color ink)
            => UiChrome.ContrastRatio(ink, UiChrome.TextPrimary) >= UiChrome.ContrastRatio(ink, UiChrome.OnAccentSolid)
                ? UiChrome.TextPrimary
                : UiChrome.OnAccentSolid;

        /// <summary>지금 쓰이는 잉크색(테스트/진단).</summary>
        public Color InkColorForTests => ResolveInk();

        /// <summary>평상시 불투명도 — <b>대비 보장은 이 값에서 계산해야 한다</b>(IdleAlpha 문서 참고).
        /// 드래그 중(더 옅다)은 커서가 위치를 말해 주는 직접 조작 상태라 이 보장의 대상이 아니다.</summary>
        public static float IdleOpacity => IdleAlpha;

        /// <summary>지금 길게 눌러 옮기는 중인가(테스트/진단 전용).</summary>
        public bool IsDraggingIcon => _dragging;

        /// <summary>사용자가 한 번이라도 옮겼는가 — false면 화면 우상단 기본 위치를 쓰고 있다.</summary>
        public bool HasCustomPosition => _hasCustomCenter;

        /// <summary>기어 중심의 현재 위치(창 좌상단 원점, OS 포인트). 저장값과 같은 좌표계다.</summary>
        public Vector2 IconCenterPoints => _hasCustomCenter ? _customCenterPoints : DefaultCenterPoints();

        /// <summary>드래그 전환 임계값(초) — 테스트가 이 숫자를 직접 기준으로 삼는다.</summary>
        public static float DragLongPressSeconds => LongPressSeconds;

        /// <summary>드래그 전환 이동 임계값(OS 포인트).</summary>
        public static float DragMoveThreshold => DragMoveThresholdPoints;

        /// <summary>테스트 전용 — 클릭 없이 회전 연출만 시작한다(창은 회전이 끝나면 정상적으로 열린다).</summary>
public void StartSpinForTests() => _spinTimer = 0f;

        /// <summary>
        /// 테스트 전용 진입점 — <b>실제 입력과 완전히 같은 처리 경로</b>(<see cref="ProcessPointer"/>)에
        /// 버튼 상태와 커서 좌표를 그대로 먹인다. PlayMode 테스트는 OS 커서를 옮겨 진짜 버튼을 누를 수
        /// 없으므로(전역 입력은 합성 입력에 반응하지 않는다 — Interaction/StickmanClickHitbox.cs의
        /// SimulateMouseDownForTests와 같은 사정) 이 경로가 필요하다. 별도의 테스트 전용 분기를 만들지
        /// 않았으므로, 테스트가 통과한다는 것은 실제 클릭/드래그 경로가 동작한다는 뜻이다.
        /// </summary>
        /// <param name="buttonDown">지금 왼쪽 버튼이 눌려 있는가(엣지는 내부에서 판정한다).</param>
        /// <param name="cursorUnityScreen">그 순간의 커서(Unity 스크린 픽셀, 좌하단 원점).</param>
        public void FeedPointerForTests(bool buttonDown, Vector2 cursorUnityScreen)
            => ProcessPointer(buttonDown, cursorUnityScreen, hasCursor: true);

        /// <summary>테스트/디버그 전용 — 기본 위치(우상단)로 되돌린다. 저장은 하지 않는다(호출한 쪽이
        /// 필요하면 직접 저장한다).</summary>
        public void ResetPositionForTests()
        {
            _hasCustomCenter = false;
            _pressActive = false;
            _dragging = false;
            _menuPressIndex = -1;
            if (_menu != null) _menu.Collapse(GearMenuCollapseMode.User, "테스트 초기화");
        }

        private void Awake()
        {
            // 같은 GameObject의 StickmanAgent만 쓴다 — 복제본에 이 컴포넌트가 남아 있어도
            // 톱니가 두 벌 겹쳐 뜨지 않게 하는 2차 방어(1차는 SceneBootstrapper의 제거).
            _agent = GetComponent<StickmanAgent>();
            _config = _agent != null ? _agent.Config : null;
            _window = GetComponent<CharacterInfoWindow>();
            _menu = GetComponent<GearRadialMenuWidget>();
        }

        private void Start()
        {
            if (_agent == null)
            {
                enabled = false;
                return;
            }
            _buttonService = _agent.PlatformService as IGlobalPointerButtonService;
            // ★ 2026-09-01 — 문구를 <b>실물에 맞춘다</b>. P0-3에서 두 기어가 단일 기어로 바뀌었는데
            //   이 배너만 "맞물린 기어 2개"로 남아, 로그를 읽는 사람이 화면과 다른 그림을 상상하게
            //   만들고 있었다(사용자 신고). 형태를 되돌리면 이 문구도 함께 되돌린다.
            Debug.Log("[톱니] 준비 완료 — 화면 우상단에 기어 1개가 상시 표시됩니다(오른쪽 " +
                $"{MarginRightPoints:F0}pt / 위 {MarginTopPoints:F0}pt, 기어 팁 반지름 {TipRadiusPoints:F1}pt / " +
                $"잇수 {ToothCount} / 획 {StrokeWidthPoints:F1}pt + 역상 헤일로 {HaloWidth:F1}pt, " +
                $"시각 지름 {VisualRadiusPoints * 2f:F1}pt). 클릭하면 기어가 돌고 그 뒤 **아이콘 전용** 부채꼴 버튼 " +
                $"{GearRadialMenuWidget.ButtonCount}개([집중 모드]/[캐릭터]/[오늘 할일]/[행동], " +
                $"Ø{GearRadialMenuWidget.ButtonDiameterPoints:F0}pt / 궤도 " +
                $"{GearRadialMenuWidget.OrbitRadiusPoints:F0}pt / 간격 " +
                $"{GearRadialMenuWidget.ButtonAngleStepDegrees:F0}도)가 **회전과 동시에** 촤르륵 펼쳐집니다. " +
                $"전역 폴링 경로={(_buttonService != null ? "사용 가능" : "미지원 — 콜라이더 경로만")}. " +
                $"★ {LongPressSeconds:F2}초 이상 누르고 있거나 누른 채 {DragMoveThresholdPoints:F0}pt 이상 끌면 " +
                "드래그 모드로 바뀌어 커서를 따라가고, 떼면 그 자리에 고정되며 저장됩니다(재시작해도 유지). " +
                "★ 클릭 판정은 두 기어를 덮는 작은 사각형 안에서만 일어나며, 그 밖은 100% 클릭관통 그대로입니다.");
        }

        private void OnDestroy()
        {
            if (_container != null) Destroy(_container);
            if (_clickTarget != null) Destroy(_clickTarget.gameObject);
        }

        private void LateUpdate()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.UiWindows);   // [스톨구간] 계측
            if (_agent == null) return;

            // ★★ 절대 불변 원칙 2(비침해) — 전체화면 게임 감지 시 상시 표면을 전부 거둔다.
            // 여기가 이 계열의 <b>1차 관문</b>이다: 톱니는 이 앱에서 유일하게 24시간 화면에 떠 있는
            // UI이고(StickmanAgent가 SetAlwaysOnTop(true)를 켜므로 전체화면 게임 위에도 뜬다),
            // 히트테스트가 커서 아래 픽셀 알파를 보므로 남아 있으면 그 영역의 클릭까지 먹는다.
            // StickmanAgent.Suspend()가 끄는 것은 Awake에서 캐시한 캐릭터 렌더러뿐인데 _container는
            // 씬 루트라 그 배열에 없다 — 액세서리가 겪었던 "몸이 사라진 자리에 모자만 남는다"와 같은 구조.
            // 부채꼴/팝오버/정보창은 각자 IsSuspended를 폴링해 스스로 닫지만(소유권 분리), 여기서도
            // 메뉴를 명시적으로 접어 "톱니는 사라졌는데 버튼만 남는" 한 프레임을 없앤다.
            // ★ 2026-09-01 설정창 [일반] "톱니 아이콘" 토글 — 끄면 전체화면 감지와 <b>같은 경로</b>로
            //   거둔다(그림/차단막/부채꼴/창까지 한 번에). 새 숨김 경로를 만들지 않는 이유: 숨기는
            //   방법이 둘이 되면 "무엇을 되살려야 하는가"의 목록도 둘이 되고, 그 목록은 반드시 갈라진다.
            if (_agent.IsSuspended || !AppSettingsModel.GearIconVisible)
            {
                ApplySuspendHide(_agent.IsSuspended ? "전체화면 감지" : "설정창에서 톱니 아이콘을 껐습니다");
                return;
            }
            if (_hiddenBySuspend) ReleaseSuspendHide();

            if (_camera == null) _camera = _agent.Blackboard != null ? _agent.Blackboard.MainCamera : Camera.main;
            if (_camera == null) return;

            RestoreSavedPositionOnce();

            // 순서에 의미가 있다: 먼저 현재 위치로 히트 사각형을 갱신해야(PlaceOnScreen) 그 사각형으로
            // "커서가 기어 위인가"를 판정할 수 있고, 드래그가 중심을 옮겼으면 <b>같은 프레임 안에</b>
            // 다시 배치해야 한 프레임 늦게 따라오는 느낌이 없다. PlaceOnScreen은 할당이 없어
            // 두 번 불러도 매 프레임 GC가 늘지 않는다(24시간 상주 앱).
            PlaceOnScreen();
            TickSpin();
            TickPointer();
            if (_dragging) PlaceOnScreen();
            TickHoverAlpha();
            TickDragVisual();
            TickMenuHover();
        }

        /// <summary>전체화면 감지 동안 톱니 그림과 클릭 차단막을 내린다. 눌림/드래그 상태도 함께
        /// 취소한다 — 안 그러면 숨는 순간의 "누르고 있음"이 그대로 남아, 복귀하자마자 놓는 동작이
        /// 클릭이나 위치 이동으로 오인된다. 도형은 파괴하지 않는다(복귀할 때 다시 굽지 않기 위해).</summary>
        private void ApplySuspendHide(string reason = "전체화면 감지")
        {
            if (_hiddenBySuspend) return;
            _hiddenBySuspend = true;

            if (_menu != null) _menu.Collapse(GearMenuCollapseMode.User, reason + " — 자동 숨김");
            if (_window != null) _window.Close(reason + " — 자동 숨김");
            if (_container != null) _container.SetActive(false);
            if (_clickTarget != null) _clickTarget.enabled = false;

            _pressActive = false;
            _dragging = false;
            _menuPressIndex = -1;
            _spinTimer = -1f;
            _leftInitialized = false;   // 복귀 후 첫 폴링이 눌림 엣지를 새로 잡게 한다.
            Debug.Log($"[톱니] {reason} — 톱니/부채꼴/정보창을 모두 거두고 클릭 차단막도 내립니다" +
                "(비침해 원칙 2). 사유가 사라지면 톱니만 다시 나타납니다.");
        }

        /// <summary>복귀 — 톱니만 되살린다. 숨기기 전에 열려 있던 메뉴/창은 <b>일부러</b> 복원하지 않는다
        /// (사용자가 부르지도 않은 창이 게임을 끄자마자 튀어나오면 그 자체가 방해다).</summary>
        private void ReleaseSuspendHide()
        {
            _hiddenBySuspend = false;
            if (_container != null) _container.SetActive(true);
            if (_clickTarget != null) _clickTarget.enabled = true;
            Debug.Log("[톱니] 전체화면 해제 — 톱니가 다시 나타납니다(메뉴/창은 사용자가 다시 엽니다).");
        }

        /// <summary>
        /// 커서가 올라간 버튼만 진하게 + <b>그 버튼의 이름표만</b> 띄운다(2026-08-31 사용자 지시:
        /// "4가지중 마우스로 선택되고있는 메뉴만 텍스트로 어떤 메뉴인지 이름이 보여야함").
        /// 이름표 자체는 <see cref="GearRadialMenuWidget"/>가 그린다 — 여기는 "어느 버튼인가"만 넘긴다
        /// (입력 소유권 단일화: 커서 폴링은 이 클래스 한 곳에서만 한다).
        ///
        /// 메뉴가 떠 있는 동안에만 커서를 묻는다(평소에는 추가 비용 0 — 24시간 상주 앱).
        /// </summary>
        private void TickMenuHover()
        {
            if (_menu == null || !_menu.IsVisible) return;
            if (!TryGetCursorUnityScreen(out Vector2 cursor)) { _menu.SetHover(-1); return; }
            _menu.SetHover(_menu.HitTest(cursor));
            // 커서가 부채꼴 안이면 6초 자동 접힘 타이머를 되돌린다(32-3).
            if (_menu.ContainsCursor(cursor)) _menu.KeepAlive();
        }

        /// <summary>저장된 위치를 딱 한 번 가져온다. Start가 아니라 첫 LateUpdate인 이유: 저장 파일을
        /// 읽는 쪽(Interaction/CharacterProgressionDirector.Start)과의 실행 순서가 보장되지 않기 때문이다
        /// (LateUpdate는 그 프레임의 모든 Start 뒤에 온다). 화면 밖 좌표 보정은 PlaceOnScreen의 클램프가
        /// 매 프레임 하므로 여기서는 값만 받는다.</summary>
        private void RestoreSavedPositionOnce()
        {
            if (_restoredFromSave) return;
            _restoredFromSave = true;
            if (!UiLayoutModel.HasGearCenter) return;

            _hasCustomCenter = true;
            _customCenterPoints = UiLayoutModel.GearCenterPoints;
            Debug.Log($"[톱니] 저장된 위치를 복원합니다 — 중심 ({_customCenterPoints.x:F0}, {_customCenterPoints.y:F0})pt " +
                "(창 좌상단 원점). 화면 밖이면 이번 프레임에 화면 안으로 끌어당겨 보정합니다.");
        }

        // ==================== 화면 배치 ====================

        /// <summary>현재 중심(기본 우상단 또는 사용자가 옮긴 위치)으로 매 프레임 옮긴다 —
        /// <b>캐릭터 위치와 완전히 무관</b>하다.</summary>
        private void PlaceOnScreen()
        {
            float depth = Mathf.Abs(_camera.transform.position.z);

            // 화면 경계 클램프는 매 프레임 한다 — 저장된 위치가 화면 밖인 경우(외장 모니터 분리 등)도
            // 여기서 자동 복구된다. 보정 결과를 모델로 되돌려 주어 다음 저장에 그 값이 남는다.
            Vector2 centerPoints = ClampCenterPoints(_hasCustomCenter ? _customCenterPoints : DefaultCenterPoints());
            if (_hasCustomCenter)
            {
                _customCenterPoints = centerPoints;
                if (!_dragging) UiLayoutModel.SetGearCenter(centerPoints); // 드래그 중에는 뗄 때 한 번만 확정한다.
            }

            // OS 포인트 -> Unity 픽셀(Retina 대응) -> 월드 유닛.
            IconScreenCenter = LocalPointsToUnityScreen(centerPoints);

            float pxPerPoint = ScreenCoordinateConverter.CanvasToUnityScreen(1f, _config);

            // 기어를 덮는 최소 정사각형(+여유). 그 이상은 넓히지 않는다(비침해).
            // 단일 기어는 방사 대칭이라 <b>광학 중심과 히트 중심이 일치</b>한다(옛 묶음은 어긋나 있었다).
            float r = (VisualRadiusPoints + HitPaddingPoints) * pxPerPoint;
            IconScreenRect = new Rect(IconScreenCenter.x - r, IconScreenCenter.y - r, r * 2f, r * 2f);

            Vector3 centerWorld = _camera.ScreenToWorldPoint(new Vector3(IconScreenCenter.x, IconScreenCenter.y, depth));
            Vector3 unitEdgeWorld = _camera.ScreenToWorldPoint(new Vector3(IconScreenCenter.x + pxPerPoint, IconScreenCenter.y, depth));
            float worldPerPoint = Mathf.Abs(unitEdgeWorld.x - centerWorld.x);

            EnsureBuilt(worldPerPoint);
            if (_container == null) return;

            _container.transform.position = new Vector3(centerWorld.x, centerWorld.y, 0f);

            // 차단막은 톱니 사각형이 아니라 <b>톱니 + 펼쳐진 버튼</b>의 합집합을 덮어야 한다 —
            // 안 그러면 버튼을 눌러도 그 클릭이 밑의 앱으로 새어 나간다. 접히면 즉시 원래 크기다(비침해).
            InteractiveScreenRect = _menu != null && _menu.IsVisible
                ? Union(IconScreenRect, _menu.UnionScreenRect)
                : IconScreenRect;

            if (_clickTarget != null)
            {
                Vector3 rectCenterWorld = _camera.ScreenToWorldPoint(
                    new Vector3(InteractiveScreenRect.center.x, InteractiveScreenRect.center.y, depth));
                Vector3 rectMaxWorld = _camera.ScreenToWorldPoint(
                    new Vector3(InteractiveScreenRect.xMax, InteractiveScreenRect.yMax, depth));
                _clickTarget.transform.position = new Vector3(rectCenterWorld.x, rectCenterWorld.y, 0f);
                _clickTarget.size = new Vector2(Mathf.Abs(rectMaxWorld.x - rectCenterWorld.x) * 2f,
                    Mathf.Abs(rectMaxWorld.y - rectCenterWorld.y) * 2f);
            }
        }

        /// <summary>두 사각형을 모두 덮는 최소 사각형. <see cref="Rect.Encapsulate"/>가 없어 직접 만든다.</summary>
        private static Rect Union(Rect a, Rect b)
        {
            float minX = Mathf.Min(a.xMin, b.xMin);
            float minY = Mathf.Min(a.yMin, b.yMin);
            float maxX = Mathf.Max(a.xMax, b.xMax);
            float maxY = Mathf.Max(a.yMax, b.yMax);
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        // ==================== 좌표/경계 (전부 OS 포인트, 창 좌상단 원점) ====================

        /// <summary>사용자가 옮긴 적이 없을 때의 위치 — 예전과 완전히 같은 화면 우상단이다.
        /// 상수로 굳히지 않고 매번 계산하는 이유: 창 크기(그리고 실측 DPI 배율)가 실행 중에 바뀌므로
        /// "오른쪽 끝에서 30pt"라는 정의를 그때그때 다시 풀어야 정확하다.</summary>
        private Vector2 DefaultCenterPoints()
            => new Vector2(ScreenSizePoints().x - MarginRightPoints, MarginTopPoints);

        private Vector2 ScreenSizePoints() => new Vector2(
            ScreenCoordinateConverter.UnityScreenToCanvas(Screen.width, _config),
            ScreenCoordinateConverter.UnityScreenToCanvas(Screen.height, _config));

        private Vector2 LocalPointsToUnityScreen(Vector2 centerPoints) => new Vector2(
            ScreenCoordinateConverter.CanvasToUnityScreen(centerPoints.x, _config),
            Screen.height - ScreenCoordinateConverter.CanvasToUnityScreen(centerPoints.y, _config));

        private Vector2 UnityScreenToLocalPoints(Vector2 unityScreen) => new Vector2(
            ScreenCoordinateConverter.UnityScreenToCanvas(unityScreen.x, _config),
            ScreenCoordinateConverter.UnityScreenToCanvas(Screen.height - unityScreen.y, _config));

        /// <summary>
        /// 중심이 아니라 <b>두 기어를 덮는 히트 사각형 전체</b>가 화면 안에 남도록 중심을 끌어당긴다.
        /// 히트 사각형(시각 크기 + <see cref="HitPaddingPoints"/>) 기준인 이유: 그것이 실제로 클릭이
        /// 먹는 영역이고, 그게 안에 있으면 그림은 당연히 전부 보인다(사각형 ⊇ 그림).
        /// 화면이 아이콘보다 작은 병적인 경우에도 NaN/역전이 나지 않게 상한을 하한 아래로 내려보내지 않는다.
        /// </summary>
        private Vector2 ClampCenterPoints(Vector2 centerPoints)
        {
            Vector2 screen = ScreenSizePoints();
            if (screen.x <= 0f || screen.y <= 0f) return centerPoints;

            float r = VisualRadiusPoints + HitPaddingPoints;   // 방사 대칭 — 네 방향이 같다.

            float minX = r, maxX = Mathf.Max(r, screen.x - r);
            float minY = r, maxY = Mathf.Max(r, screen.y - r);   // y는 위에서 아래로 자란다.
            return new Vector2(Mathf.Clamp(centerPoints.x, minX, maxX), Mathf.Clamp(centerPoints.y, minY, maxY));
        }

        /// <summary>배율(화면 해상도/DPI)이나 잉크색이 바뀌지 않으면 도형을 다시 만들지 않는다 —
        /// 24시간 상주 앱. 잉크색을 서명에 넣는 이유는 CharacterAccessoryRenderer와 같다(⌃⌥⌘C /
        /// 정보창 [외형] 탭에서 색을 바꿔도 이 아이콘이 옛 색으로 남지 않게).</summary>
        private void EnsureBuilt(float worldPerPoint)
        {
            Color ink = ResolveInk();
            bool sameSize = _builtGeometry && Mathf.Abs(worldPerPoint - _builtRadiusWorld) < worldPerPoint * 0.01f;
            if (sameSize && ink == _builtInk) return;

            Build(worldPerPoint, ink);
            _builtRadiusWorld = worldPerPoint;
            _builtInk = ink;
            _builtGeometry = true;
        }

        private Color ResolveInk() => _config != null ? _config.ResolveInkColor() : Color.black;

        private void Build(float worldPerPoint, Color ink)
        {
            if (_container != null) Destroy(_container);
            _lines.Clear();

            _lineMaterial = ResolveLineMaterial();

            _container = new GameObject("InfoGearIcon");
            _container.transform.SetParent(null, false); // 씬 루트 — 캐릭터가 걷거나 랙돌로 회전해도 따라 돌면 안 된다.

            float stroke = StrokeWidthPoints * worldPerPoint;
            float halo = HaloWidth * worldPerPoint;
            Color haloColor = ResolveHaloColor(ink);

            var gearGo = new GameObject("Gear");
            gearGo.transform.SetParent(_container.transform, false);
            _gear = gearGo.transform;

            Vector3[] teeth = BuildGearOutline(ToothCount,
                TipRadiusPoints * worldPerPoint, RootRadiusPoints * worldPerPoint, 0f);
            Vector3[] hub = BuildCircle(HubRadiusPoints * worldPerPoint, 20);

            // ★ 2겹 — <b>헤일로 먼저(뒤), 잉크 나중(앞)</b>.
            //   형제 순서가 아니라 sortingOrder로 순서를 정한다: 같은 sortingOrder의 LineRenderer들은
            //   그리기 순서가 보장되지 않아 프레임마다 앞뒤가 뒤집힐 수 있다(그러면 잉크가 헤일로에
            //   먹혀 아이콘이 통째로 역상 색으로 보인다).
            AddLine(_gear, "TeethHalo", teeth, haloColor, halo, loop: true, sortingOrder: SortingOrder);
            AddLine(_gear, "HubHalo", hub, haloColor, halo, loop: true, sortingOrder: SortingOrder);
            AddLine(_gear, "Teeth", teeth, ink, stroke, loop: true, sortingOrder: SortingOrder + 1);
            AddLine(_gear, "Hub", hub, ink, stroke, loop: true, sortingOrder: SortingOrder + 1);

            // ★ 림/스포크를 지웠다. 옛 형태는 허브3.6–림7.0–뿌리10.2의 간격이 1.00/0.88획(규칙 1.5획
            //   위반)이라 잉크만으로도 이미 뭉쳐 있었고, 헤일로(3.74pt)를 얹으면 세 원이 한 덩어리가
            //   된다. 요소를 줄이는 것이 <b>이 크기에서 유일하게 성립하는 조형</b>이다.

            if (_clickTarget == null)
            {
                var hitGo = new GameObject("InfoGearClickTarget");
                _clickTarget = hitGo.AddComponent<BoxCollider2D>();
                _clickTarget.isTrigger = true; // 캐릭터가 톱니에 부딪혀 튕기면 안 된다(메뉴 차단막과 같은 이유).
            }

            ApplyAlphaToAll();
        }

        /// <summary>
        /// 사다리꼴 이를 가진 기어 윤곽. 이 하나당 5점 — 뿌리(앞) / 마루(앞) / 마루(뒤) / 뿌리(뒤) /
        /// 골 중앙. 원에 삼각 홈을 낸 예전 모양과 달리 <b>이 끝이 평평</b>해 진짜 기어로 읽힌다.
        /// </summary>
        private static Vector3[] BuildGearOutline(int teeth, float outer, float root, float phaseDegrees)
        {
            var pts = new Vector3[teeth * 5];
            float pitch = Mathf.PI * 2f / teeth;
            float tipHalf = pitch * ToothTipHalfFraction;
            float rootHalf = pitch * ToothRootHalfFraction;
            float phase = phaseDegrees * Mathf.Deg2Rad;

            int k = 0;
            for (int i = 0; i < teeth; i++)
            {
                float center = phase + i * pitch;
                pts[k++] = Polar(center - rootHalf, root);
                pts[k++] = Polar(center - tipHalf, outer);
                pts[k++] = Polar(center + tipHalf, outer);
                pts[k++] = Polar(center + rootHalf, root);
                pts[k++] = Polar(center + pitch * 0.5f, root * 0.985f); // 골 중앙(살짝 눌러 둥근 골).
            }
            return pts;
        }

        private static Vector3 Polar(float angleRadians, float radius)
            => new Vector3(Mathf.Cos(angleRadians) * radius, Mathf.Sin(angleRadians) * radius, 0f);

        private static Vector3[] BuildCircle(float radius, int segments)
        {
            var pts = new Vector3[Mathf.Max(3, segments)];
            for (int i = 0; i < pts.Length; i++)
            {
                float a = i / (float)pts.Length * Mathf.PI * 2f;
                pts[i] = Polar(a, radius);
            }
            return pts;
        }

        private void AddLine(Transform parent, string name, Vector3[] points, Color color, float width, bool loop,
            int sortingOrder = SortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.material = _lineMaterial;
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 2;
            lr.sortingOrder = sortingOrder;
            lr.loop = loop;
            lr.positionCount = points.Length;
            lr.SetPositions(points);
            _lines.Add(lr);
        }

        // ==================== 회전 연출 ====================

        /// <summary>
        /// 기어를 시계 방향으로 돌린다. ease-out(감속)은 기계가 돌다 멈추는 느낌 — 등속으로 돌리면
        /// 뚝 끊겨 보인다. 잇수 6이라 60도마다 같은 그림이 되지만, 4바퀴를 1.4초에 걸쳐 감속하므로
        /// "돌았다"는 <b>속도 변화</b>에서 읽힌다(정지 위상이 같은 것은 오히려 안정적이다).
        /// </summary>
        private void TickSpin()
        {
            if (_container == null || _gear == null) return;
            if (_spinTimer < 0f) return;

            _spinTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_spinTimer / SpinSeconds);
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);

            _gear.localRotation = Quaternion.Euler(0f, 0f, -eased * 360f * SpinTurns);   // 시계 방향.

            if (t < 1f) return;

            _spinTimer = -1f;
            _gear.localRotation = Quaternion.identity;

            // 부채꼴은 <b>클릭 프레임에 이미</b> 펼쳐지기 시작했다(32-9 (B)) — 회전이 끝나기를
            // 기다리지 않는다. 그래서 여기서는 각도만 원위치로 돌려놓고 끝난다.
        }

        private void ExpandMenu()
        {
            if (_menu == null)
            {
                Debug.LogWarning("[톱니] 부채꼴 메뉴 위젯(GearRadialMenuWidget)이 없어 펼치지 못했습니다.");
                return;
            }
            _menu.Expand(IconScreenCenter);
        }

        private void CollapseMenu(GearMenuCollapseMode mode, string reason)
        {
            if (_menu == null || !_menu.IsExpanded) return;
            _menu.Collapse(mode, reason);
            _menuPressIndex = -1;
        }

        private void TickHoverAlpha()
        {
            bool highlight = IsSpinning || IsMenuExpanded || (_window != null && _window.IsOpen) || IsCursorOverIcon();
            _highlighted = highlight;   // TickDragVisual이 같은 프레임에 크기로도 표현한다(§5.1).
            // 드래그 중에는 옅게 — "지금 들려서 떠 있다"는 표시다(호버 강조보다 우선한다).
            float target = _dragging ? DragAlpha : (highlight ? ActiveAlpha : IdleAlpha);
            float next = Mathf.MoveTowards(_alpha, target, AlphaFadeSpeed * Time.unscaledDeltaTime);
            if (Mathf.Approximately(next, _alpha)) return;
            _alpha = next;
            ApplyAlphaToAll();
        }

        private void ApplyAlphaToAll()
        {
            for (int i = 0; i < _lines.Count; i++)
            {
                LineRenderer lr = _lines[i];
                if (lr == null) continue;
                Color c = lr.startColor;
                c.a = _alpha;
                lr.startColor = c;
                lr.endColor = c;
            }
        }

        // ==================== 클릭 / 길게 눌러 옮기기 ====================

        private void TickPointer()
        {
            if (_buttonService == null) return;

            // 평소에는 0.05초 간격으로만 OS에 묻는다(24시간 상주 앱). 다만 누르고 있는 동안에는 매
            // 프레임 본다 — 폴링 간격만큼 커서를 늦게 따라가면 드래그가 뚝뚝 끊겨 보인다.
            if (!_pressActive)
            {
                _clickPollTimer += Time.unscaledDeltaTime;
                if (_clickPollTimer < ClickPollInterval) return;
                _clickPollTimer = 0f;
            }

            if (!_buttonService.TryGetPrimaryButtonPressed(out bool left))
            {
                // 버튼 상태를 못 읽는데 누른 상태로 남겨두면 기어가 커서에 영원히 붙는다.
                AbortPress("버튼 상태를 읽지 못함");
                return;
            }
            if (!_leftInitialized) { _leftInitialized = true; _leftPrev = left; return; }

            bool hasCursor = TryGetCursorUnityScreen(out Vector2 cursor);
            ProcessPointer(left, cursor, hasCursor);
        }

        /// <summary>버튼 상태 + 커서 좌표만으로 눌림/드래그/뗌을 판정하는 <b>단일 경로</b>.
        /// 실제 입력(TickPointer)과 테스트(FeedPointerForTests)가 이 함수를 공유한다.</summary>
        private void ProcessPointer(bool buttonDown, Vector2 cursorUnityScreen, bool hasCursor)
        {
            bool prev = _leftPrev;
            _leftPrev = buttonDown;
            _leftInitialized = true;

            if (buttonDown && !prev) BeginPress(cursorUnityScreen, hasCursor);
            else if (buttonDown && _pressActive) UpdatePress(cursorUnityScreen, hasCursor);
            else if (!buttonDown && prev) EndPress(cursorUnityScreen, hasCursor);
        }

        private void BeginPress(Vector2 cursorUnityScreen, bool hasCursor)
        {
            _menuPressIndex = -1;

            // 부채꼴이 펼쳐져 있으면 그쪽이 먼저다: 버튼 위면 그 버튼을 누른 것이고, 톱니도 버튼도
            // 아닌 곳이면 접는다. 접기는 그 클릭을 <b>소비하지 않는다</b> — 밑에서 하려던 일은
            // 그대로 일어난다(메뉴의 표준 관례이자 비침해 원칙).
            if (IsMenuExpanded && hasCursor)
            {
                int hit = _menu.HitTest(cursorUnityScreen);
                if (hit >= 0)
                {
                    _menuPressIndex = hit;
                    return;
                }
                if (!IconScreenRect.Contains(cursorUnityScreen))
                {
                    // 팝오버가 떠 있으면 그쪽이 자기 바깥 클릭을 스스로 처리한다(팝오버가 닫히면
                    // 부채꼴도 따라 접힌다) — 여기서 먼저 접으면 팝오버가 고아로 남는다.
                    if (_menu.AnchoredButton < 0) CollapseMenu(GearMenuCollapseMode.User, "부채꼴 바깥 클릭");
                    return;
                }
            }

            // ★ 비침해 — 버튼이 눌렸다는 사실만으로는 아무 일도 하지 않는다. 커서가 아이콘 사각형
            //   안일 때만 반응한다(클래스 문서 "비침해 보장").
            if (!hasCursor || !IconScreenRect.Contains(cursorUnityScreen)) return;
            if (IsSpinning) return;

            _pressActive = true;
            _dragging = false;
            _pressStartTime = Time.unscaledTime;
            _pressStartCursor = cursorUnityScreen;

            // 잡은 지점과 중심의 차이를 기억한다 — 드래그가 시작될 때 기어가 커서로 순간이동하지 않게.
            Vector2 center = _hasCustomCenter ? _customCenterPoints : DefaultCenterPoints();
            _grabOffsetPoints = center - UnityScreenToLocalPoints(cursorUnityScreen);
        }

        private void UpdatePress(Vector2 cursorUnityScreen, bool hasCursor)
        {
            if (!hasCursor) return;

            if (!_dragging)
            {
                float heldSeconds = Time.unscaledTime - _pressStartTime;
                float movedPoints = ScreenCoordinateConverter.UnityScreenToCanvas(
                    (cursorUnityScreen - _pressStartCursor).magnitude, _config);
                if (heldSeconds < LongPressSeconds && movedPoints < DragMoveThresholdPoints) return;

                _dragging = true;
                // 옮기는 동안 버튼들이 뒤에 남아 끌려다니면 안 된다. 접고 나서 옮긴다.
                CollapseMenu(GearMenuCollapseMode.Drag, "톱니를 옮기기 시작");
                Debug.Log($"[톱니] 길게 누름 감지({heldSeconds:F2}초 / {movedPoints:F1}pt 이동) — " +
                    "드래그 모드로 전환합니다. 이제 커서를 따라가고, 떼면 그 자리에 고정됩니다(부채꼴 메뉴는 펼쳐지지 않습니다).");
            }

            _hasCustomCenter = true;
            _customCenterPoints = ClampCenterPoints(UnityScreenToLocalPoints(cursorUnityScreen) + _grabOffsetPoints);
        }

        private void EndPress(Vector2 cursorUnityScreen, bool hasCursor)
        {
            // 부채꼴 버튼은 이동이 없으므로 <b>누른 그 버튼 위에서 뗐을 때만</b> 발동한다.
            // 끌고 나가서 떼면 취소 — 모든 OS의 버튼 관례이자, 잘못 누른 것을 되돌릴 유일한 방법이다.
            if (_menuPressIndex >= 0)
            {
                int index = _menuPressIndex;
                _menuPressIndex = -1;
                if (hasCursor && _menu != null && _menu.HitTest(cursorUnityScreen) == index) ActivateMenuButton(index);
                else Debug.Log("[톱니] 부채꼴 버튼 선택 취소 — 누른 버튼 밖에서 뗐습니다.");
                return;
            }

            if (!_pressActive) return;
            _pressActive = false;

            if (_dragging)
            {
                _dragging = false;
                CommitDragPosition();
                return;
            }

            ActivateClick();
        }

        /// <summary>입력 상태를 잃었을 때의 안전 종료 — 드래그였으면 지금 자리를 확정하고, 아니면
        /// 아무 일도 하지 않는다(눌린 적 없던 것으로 되돌린다 — 창이 제멋대로 열리면 안 된다).</summary>
        private void AbortPress(string reason)
        {
            _menuPressIndex = -1;
            if (!_pressActive) return;
            _pressActive = false;
            _leftPrev = false;

            if (!_dragging)
            {
                Debug.Log($"[톱니] 누름 취소 — {reason}. 창은 열지 않습니다.");
                return;
            }

            _dragging = false;
            CommitDragPosition();
        }

        private void CommitDragPosition()
        {
            Vector2 center = ClampCenterPoints(_customCenterPoints);
            _customCenterPoints = center;
            UiLayoutModel.SetGearCenter(center);

            // 즉시 저장한다 — 주기 저장(기본 60초)만 믿으면 옮긴 직후 종료했을 때 위치가 날아간다.
            bool saved = CharacterSaveStore.Save();
            Debug.Log($"[톱니] 위치 확정 — 중심 ({center.x:F0}, {center.y:F0})pt(창 좌상단 원점). " +
                $"저장 {(saved ? "완료" : "실패(메모리 값 유지, 다음 주기에 재시도)")} — 재시작해도 이 자리에 뜹니다.");
        }

        /// <summary>짧은 클릭의 동작 — 부채꼴 메뉴 토글이다. 펼쳐져 있으면 접고, 아니면 회전 연출 뒤
        /// 펼친다. 호출 시점은 예전 그대로 <b>뗀 순간</b>이다(그래야 드래그와 구분된다).</summary>
        private void ActivateClick()
        {
            if (IsSpinning) return;

            // ★ 2026-08-30 역방향 잠금 — 정보창이 열려 있으면 톱니는 <b>그 창을 닫는 버튼</b>이다.
            // 예전에는 창을 조회조차 하지 않고 부채꼴을 창 위에 펼쳤다(부채꼴 31500 > 창 31000이었다).
            // 톱니가 주 진입점이라 "닫으려고 한 번 더 누른다"가 가장 자연스러운 동작인데, 그때마다
            // 창 위에 부채꼴이 얹힌 화면이 됐다. 닫기만 하고 부채꼴은 펼치지 않는다 — 한 번의 클릭이
            // "지금 떠 있는 표면을 닫는다"는 뜻이면 충분하고, 닫으려던 사용자에게 곧바로 다른 UI를
            // 들이미는 것은 같은 실수의 반복이다. 창이 없어야 다음 클릭이 평소처럼 부채꼴을 편다.
            if (_window != null && _window.IsOpen)
            {
                _window.Close("톱니 재클릭(창 닫기)");
                return;
            }

            if (IsMenuExpanded)
            {
                // 닫을 때는 회전하지 않는다 — 회전은 "기계를 여는" 신호다(32-3).
                CollapseMenu(GearMenuCollapseMode.User, "톱니 재클릭(토글 닫기)");
                return;
            }

            _spinTimer = 0f;
            ExpandMenu();   // ★ 회전과 <b>동시에</b> 펼친다.
            // 개수를 손으로 적지 않는다 — 2026-08-31에 버튼이 3 -> 4로 늘었을 때 이 줄만 3에 남아
            // 로그가 화면과 다른 말을 했다(페르소나 소은 #8). 세는 곳은 부채꼴 자신이다.
            Debug.Log($"[톱니] 클릭 — 기어가 도는 것과 동시에 부채꼴 버튼 " +
                $"{GearRadialMenuWidget.ButtonCount}개가 펼쳐집니다.");
        }

        /// <summary>부채꼴 버튼의 동작은 <see cref="GearRadialMenuWidget"/>가 전담한다 — 그쪽이
        /// 팝오버를 알고 "누른 버튼만 남기고 나머지를 접는" 규칙(32-3)도 갖고 있다. 여기서는 어느
        /// 버튼이 눌렸는지만 넘긴다.</summary>
        private void ActivateMenuButton(int index)
        {
            if (_menu == null) return;
            _menu.Activate(index);
        }

        /// <summary>드래그/호버를 눈으로 알 수 있게 살짝 키운다. 회전(자식의 각도)과 겹치지 않는
        /// 부모의 스케일이라 회전 연출과 충돌하지 않는다.
        /// <para>★ 호버가 여기 들어온 이유(P0-3): 알파만으로는 <b>배경이 어두우면 아무 일도 일어나지
        /// 않는다</b>. 크기는 배경 휘도와 무관한 단서다. 균일 배율이라 형태 여유 비율도 보존된다.</para></summary>
        private void TickDragVisual()
        {
            if (_container == null) return;
            float target = _dragging ? DragScale : (_highlighted ? HoverScale : 1f);
            if (!Mathf.Approximately(_visualScale, target))
                _visualScale = Mathf.MoveTowards(_visualScale, target, DragScaleSpeed * Time.unscaledDeltaTime);

            // 도형을 다시 만들면(Build) 스케일이 1로 돌아오므로 현재 값과 비교해 필요한 프레임에만 쓴다.
            if (Mathf.Approximately(_container.transform.localScale.x, _visualScale)) return;
            _container.transform.localScale = new Vector3(_visualScale, _visualScale, 1f);
        }

        // ==================== 테스트용 커서 주입 ====================

        private bool _hasTestCursor;
        private Vector2 _testCursor;

        /// <summary>
        /// 테스트 전용 — <b>이 위젯이 커서를 읽는 단 하나의 창구</b>(<see cref="TryGetCursorUnityScreen"/>)에
        /// 좌표를 밀어 넣는다. <see cref="FeedPointerForTests"/>와 같은 관례이며, 같은 이유로 존재한다:
        /// PlayMode는 진짜 OS 커서를 원하는 자리로 옮길 수 없다.
        ///
        /// ★ 왜 <c>GearRadialMenuWidget.SetHover</c>를 테스트가 직접 부르지 않는가: 호버의 <b>소유자는
        /// 이 클래스의 폴링</b>이라(<see cref="TickMenuHover"/>) 밖에서 SetHover를 부르면 다음 프레임에
        /// 폴링이 곧바로 덮어쓴다 — 실제로 그렇게 짠 첫 회귀 테스트가 전부 빈 이름표를 봤다.
        /// 여기에 좌표를 넣으면 히트테스트/호버/이름표/자동접힘이 <b>전부 실제 코드 경로</b>를 탄다.
        /// </summary>
        public void FeedHoverCursorForTests(Vector2 cursorUnityScreen)
        {
            _hasTestCursor = true;
            _testCursor = cursorUnityScreen;
        }

        /// <summary>주입한 커서를 걷고 실제 OS 커서로 되돌린다.</summary>
        public void ClearHoverCursorForTests() => _hasTestCursor = false;

        private bool TryGetCursorUnityScreen(out Vector2 cursorUnityScreen)
        {
            if (_hasTestCursor) { cursorUnityScreen = _testCursor; return true; }
            cursorUnityScreen = default;
            if (_agent == null || !_agent.TryGetCursorPosition(out Vector2 osScreen)) return false;
            cursorUnityScreen = ScreenCoordinateConverter.OsScreenToUnityScreen(osScreen, _config);
            return true;
        }

        private bool IsCursorOverIcon()
            => TryGetCursorUnityScreen(out Vector2 cursor) && IconScreenRect.Contains(cursor);

        /// <summary>다른 렌더러들과 같은 이유로 캐릭터 LineRenderer의 머티리얼을 빌려 쓴다
        /// (Shader.Find는 빌드 스트리핑 위험이 있어 쓰지 않는다).</summary>
        private Material ResolveLineMaterial()
        {
            if (_lineMaterial != null) return _lineMaterial;
            LineRenderer source = GetComponentInChildren<LineRenderer>(true);
            return source != null ? source.sharedMaterial : null;
        }
    }
}
