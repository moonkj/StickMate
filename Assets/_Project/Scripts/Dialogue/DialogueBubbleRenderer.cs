using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using StickMate.Core;
using StickMate.States;
using StickMate.Interaction;
using StickMate.Platform;

namespace StickMate.Dialogue
{
    /// <summary>
    /// ★ 말풍선 렌더링 — docs/UX_FLOW.md 5절 `DialogueIntent` UX 계약의 **화면 구현부**.
    ///
    /// ============================================================================
    /// 왜 이 파일이 이제야 생겼는가 (정직한 이력)
    /// ============================================================================
    /// 이 프로젝트의 1순위 원칙(CLAUDE.md 절대 불변 원칙 1 "행동-텍스트 싱크")의 산출물인
    /// `DialogueIntent` 파이프라인은 여러 라운드에 걸쳐 정교하게 만들어졌고 EditMode 테스트 8건으로
    /// 계약까지 고정돼 있었지만, <b>`StickmanEventBus.DialogueRequested`를 구독해 실제로 말풍선을 그리는
    /// 코드가 어디에도 없었다</b>. 대사는 계속 생성되고 만료됐지만 아무도 볼 수 없었다. 이 컴포넌트가
    /// 정확히 그 빠진 조각이다 — 파이프라인 쪽은 한 줄도 바꾸지 않고(이벤트 2개만 구독) 순수 소비자로
    /// 붙는다.
    ///
    /// ============================================================================
    /// UX 계약 준수 방식 (5절 규칙별 대응, 이 클래스의 존재 이유)
    /// ============================================================================
    /// · 규칙 3(a) 정상 종료  : `DialogueExpired`가 "그 전이가 강제 인터럽트가 아니었을 때" 도착하면
    ///                          종류에 따라 갈린다(아래 규칙 4-c) — 서술은 즉시, 반응은 가독예산을
    ///                          채운 뒤 <see cref="FadeOutSeconds"/> 페이드아웃.
    /// · 규칙 3(b) 강제 취소  : `DialogueExpired`가 **같은 프레임의 강제 인터럽트 전이**로 인해 도착하면
    ///                          페이드아웃 없이 <b>그 자리에서 동기적으로 즉시 제거</b>
    ///                          (<see cref="HideImmediate"/>). 이벤트 핸들러 안에서 바로 지우므로
    ///                          "취소된 상태의 말풍선이 화면에 남아있는 시간"이 구조적으로 0 프레임이다.
    /// · 규칙 4 우선순위      : ★ 2026-09-01 개정 — 대사 종류 축(<see cref="DialogueKind"/>) 도입.
    ///                          ① 강제 인터럽트 → 같은 프레임 즉시 제거(종류·경과시간을 아예 안 본다)
    ///                          ② 상한 dialogueMaxVisibleSeconds → 종류 무관
    ///                          ③ Narrative + 정상 종료 → **즉시 컷**(가독예산 무시)
    ///                          ④ Reaction + 정상 종료 → 글자수 비례 가독예산
    ///                             (<see cref="DialogueBudget.ReadingSeconds"/>)을 채운 뒤 페이드아웃
    ///                          즉 <b>취소는 여전히 항상 이긴다</b>(구판의 핵심 방어선 그대로). 바뀐 것은
    ///                          "정상 종료"가 더 이상 고정 하한에 지지 않는다는 점뿐이다.
    /// · 규칙 8 발화 자격     : ③의 즉시 컷이 "0.08초 번쩍이는 글자"를 만들지 않도록, 컷될 대사는 애초에
    ///                          만들어지지 않는다 — 게이트는 여기가 아니라 생성 경로
    ///                          (<see cref="DialogueIntent.TryCreate"/>)에 있다.
    /// · 규칙 5 큐잉 금지     : 새 `DialogueRequested`가 오면 이전 말풍선을 즉시 교체한다 — 다음 대사를
    ///                          모아두었다가 나중에 꺼내는 큐가 애초에 없다.
    /// · 규칙 6 위치/스타일   : 등장(알파) 60ms / 팝 스케일 바운스 180ms / 소멸 120ms
    ///                          (2026-09-01 개정 — 세 값 전부 <see cref="DialogueTiming"/>이 단일 소스).
    ///                          배치는 아래 "만화 레터링 전환"으로 갱신됐다
    ///                          (종전: 머리 위 + 꼬리가 캐릭터를 가리킴, 화면 경계에서는 꼬리 방향을
    ///                          유지한 채 박스만 안쪽으로 — <see cref="UpdateBubblePlacement"/>에 보존).
    /// · 규칙 7 다중 캐릭터   : `Bind()`로 화자(StickmanStateMachine)를 지정하면 그 머신이 발급한
    ///                          대사만 표시한다 — 두 번째 화자가 동시에 말해도 서로의 말풍선을 훔치지
    ///                          않는다(각자 자기 렌더러를 하나씩 갖는다).
    ///
    /// ============================================================================
    /// "강제 인터럽트인지"를 어떻게 아는가 — 이벤트 순서에 대한 근거
    /// ============================================================================
    /// `DialogueIntent`는 만료 사유를 페이로드에 싣지 않는다(세대 불일치만 본다). 그 판단 근거는
    /// `StateTransitionEvent.IsForcedInterrupt`에 있으므로 이 클래스가 두 이벤트를 함께 구독해 잇는다.
    /// 순서가 항상 성립하는 근거(StickmanStateMachine.ChangeState 구현 기준):
    ///   1) ChangeState -> 세대 증가 -> 새 상태 Enter()(여기서 새 DialogueIntent가 만들어질 수 있음)
    ///   2) 그 다음에야 RaiseStateTransitioned(from, to, isForcedInterrupt)
    ///   3) StateTransitioned 구독자 순서 = 구독 등록 순서 = [이 렌더러(OnEnable, 씬 시작 시점), ...,
    ///      각 DialogueIntent(생성 시점)] — 즉 <b>렌더러가 항상 먼저</b> 플래그를 받는다.
    ///   4) 구세대 DialogueIntent가 자기 차례에 Expire() -> RaiseDialogueExpired
    /// 따라서 DialogueExpired를 받는 시점에는 같은 프레임의 IsForcedInterrupt 값이 이미 손에 있다.
    /// 프레임 번호까지 함께 비교하므로(<see cref="_forcedInterruptFrame"/>) 오래된 플래그를 재사용하는
    /// 사고도 생기지 않는다.
    ///
    /// ============================================================================
    /// 렌더링 방식 — 왜 uGUI(Canvas)인가
    /// ============================================================================
    /// 캐릭터 자체는 LineRenderer로 그리지만(월드 공간), 말풍선은 <b>글자</b>가 본체라 텍스트 레이아웃/
    /// 줄바꿈/폰트 아틀라스가 필요하다. 이 프로젝트에는 TextMeshPro가 없고, 이미
    /// `Interaction/TodoPostItWidget.cs`와 `Interaction/AppControlDirector.cs`가 legacy uGUI
    /// (ScreenSpaceOverlay Canvas + `UnityEngine.UI.Text`)를 런타임 생성해 쓰는 전례가 있어 같은 관례를
    /// 따른다. 투명 오버레이에서도 문제가 없다 — 카메라는 알파 0으로 클리어하지만 ScreenSpaceOverlay
    /// 캔버스는 그 위에 자기 알파로 합성되므로, 불투명 흰 Image가 있는 픽셀만 알파 1이 되어 <b>말풍선
    /// 모양 그대로만</b> 화면에 남는다(배경은 그대로 비친다).
    ///
    /// ============================================================================
    /// ★★ 만화 레터링 전환 (2026-08-29, 사용자 요구 — 이 클래스가 지금 실제로 그리는 것)
    /// ============================================================================
    /// 원문 두 건:
    ///   (1) "말풍선 말고 텍스트만 캐릭터 걸어가는방향 반대쪽 대각선 상단에 나타나게 해줘"
    ///   (2) "만화처럼" / "만화스타일"
    ///
    /// 그래서 <b>말풍선 도형(타원 링 몸통 + 삼각 꼬리)을 더 이상 그리지 않는다.</b> 화면에 남는 것은
    /// 글자뿐이고, 그 글자는:
    ///   · <b>진행 방향의 반대쪽 대각선 위</b>에 놓인다(오른쪽으로 걸으면 왼쪽 위) — 진행 방향 앞을
    ///     글자가 가리지 않게 뒤로 흘리는 것이 의도다. 쪽은 대사가 뜨는 순간 한 번 확정되어 그 대사가
    ///     사라질 때까지 고정된다(캐릭터가 돌아설 때마다 글자가 좌우로 날아다니면 읽을 수 없다).
    ///   · <b>잉크색 글자 + 반대색 외곽선</b>으로 그려진다. 이것이 만화 레터링의 기본 문법이면서
    ///     동시에 배경이 사라진 뒤의 유일한 가독성 대책이다 — 검은 캐릭터 + 어두운 바탕화면,
    ///     흰 캐릭터 + 밝은 바탕화면 양쪽에서 글자가 사라지는 것을 이 선 하나가 막는다.
    ///   · 굵은 페이스(AppleSDGothicNeo-Heavy 계열) + 등장 시 팝(스케일 바운스).
    ///   · <b>글자 자체가 비스듬히 기울어 있다</b>(사용자 요구 2026-08-31 "좀 대각선으로 작성해줘").
    ///     기울기의 부호는 놓인 쪽의 거울상이라 배치의 대각선과 맞물린다 — 자세한 근거는 아래
    ///     "글자 기울기" 상수 블록 참고.
    ///
    /// 도형을 그리던 코드는 <b>지우지 않고 전부 남겼다</b>(<see cref="DrawBubbleShapes"/> 플래그 하나로
    /// 종전 그림이 그대로 복원된다) — 되돌리기 요구에 대비한 리더 지시다.
    ///
    /// 이 전환은 순수하게 "어떻게 보이는가"만 바꾼다. 대사 생성/만료 계약(DialogueRequested /
    /// DialogueExpired / IsForcedInterrupt / TransitionGeneration)은 한 줄도 손대지 않았다.
    ///
    /// 한글 폰트: Unity 내장 `LegacyRuntime.ttf`(Arial 계열)에는 한글 글리프가 없어 네모(두부)로 깨진다.
    /// 그래서 <see cref="ResolveKoreanFont"/>가 OS 설치 폰트에서 한글이 실제로 렌더링되는 것을
    /// **글리프 단위로 실측**해(RequestCharactersInTexture -> GetCharacterInfo) 고른다.
    /// </summary>
    public sealed class DialogueBubbleRenderer : MonoBehaviour
    {
        // ==================== 스타일 상수 ====================
        // 캐릭터가 "굵은 검은 획 + 빈 얼굴"이므로 말풍선도 같은 문법(흰 배경 + 굵은 검은 테두리 +
        // 검은 글씨)을 따른다. 값의 단위는 **캔버스 유닛 == OS 포인트**다(Retina에서도 물리적 크기가
        // 같다 — CanvasScaler가 흡수한다. 아래 "두 배율의 합성" 블록 참고).
        //
        // ★★ 여기 적힌 숫자는 **캐릭터 배율 1.0 기준의 baseline**이다. 실제 레이아웃 코드는 이 상수를
        // 직접 쓰지 말고 반드시 Scaled* 프로퍼티(ScaledBorderThickness 등)를 써야 한다 — 캐릭터가
        // 절반 크기가 되면 말풍선도 함께 줄어들어야 하고, 한 군데라도 원본 상수가 남으면 그 항만
        // 고정분으로 남아 "폰트를 줄여도 말풍선이 캐릭터보다 크다"가 재발한다(리더 실측 보고, 2026-08-29).
        // TodoPostItWidget(30000) 위, 캐릭터 창(31900)/앱 제어 메뉴(32760) 아래.
        // ※ 2026-08-30까지 캐릭터 창도 31000이라 <b>동률</b>이었다 — 동률 오버레이 캔버스의 그리기 순서는
        //   Unity가 보장하지 않아(생성 순서 의존) 창 위를 지나며 말할 때 대사가 뚫리거나 묻힐 수 있었다.
        //   창을 31900으로 올려 값을 갈랐다. 말풍선이 모달 창 아래로 가는 것은 의도다.
        private const int SortingOrderBubble = 31000;
        private const float BorderThickness = 2.5f;     // 검은 테두리 두께.
        private const float TextPadding = 7f;           // 테두리 안쪽 여백.
        private const float MaxTextWidth = 220f;        // 이 폭을 넘으면 줄바꿈.
        private const float TailWidth = 24f;
        private const float TailHeight = 15f;
        // 꼬리 채움이 몸통 테두리를 덮어 자연스럽게 잇는 양. 타원 전환(2026-08-29)으로 상향:
        // 사각형이면 아래 변이 평평해 3px면 충분했지만, 타원은 중앙에서 멀어질수록 아래 경계가
        // 위로 휘어 올라가고 **안쪽(채움) 타원**은 바깥 타원보다 더 위에 있다. 실측 계산상 최대
        // 세로 간격이 약 2.5px라 그보다 확실히 큰 값이어야 이음매가 생기지 않는다.
        private const float TailPanelOverlap = 5f;
        /// <summary>꼬리가 붙을 수 있는 최대 위치(몸통 반폭 대비 비율, 꼬리 바깥 모서리 기준).
        /// 이보다 옆으로 나가면 타원 아래 경계가 너무 높이 휘어 꼬리가 몸통 옆구리에 매달린 것처럼 보인다.</summary>
        private const float TailEllipseSpanLimit = 0.75f;
        private const float ScreenEdgeMargin = 8f;      // 화면 가장자리 최소 여백(규칙 6 "잘리지 않게").
        // ★ 2026-08-29 리더 지시 — 캐릭터 기준 오프셋은 **전신 높이 대비 비율**로만 둔다.
        // 절대 유닛으로 두면 캐릭터 크기를 바꾸는 순간(사용자가 "절반 크기 + 추후 조정 가능"을 요구했다)
        // 꼬리가 머리를 파고들거나 허공에 뜬다. 기준값의 단일 소스는 StickmanAgent.CharacterTotalHeightWorld.
        // 현재 프리팹 실측 2.27유닛에 곱하면 검증을 마친 종전 값(0.34)이 그대로 나온다.
        private const float HeadTopOffsetRatio = 0.1498f; // 머리 중심에서 꼬리 끝까지(0.34 / 2.27).

        // ============================================================================
        // ★★ 만화 레터링 모드 (사용자 요구 2026-08-29)
        //   "말풍선 말고 텍스트만 캐릭터 걸어가는방향 반대쪽 대각선 상단에 나타나게 해줘"
        //   "만화처럼 / 만화스타일"
        // ============================================================================
        // 말풍선 도형(타원 링 몸통 + 삼각 꼬리)을 **그리지 않고 글자만** 띄운다. 도형을 만드는 코드
        // (CreateTailPart / UpdateEllipseSprites / BuildEllipseRingSprite / BuildEllipseSprite /
        //  BuildTriangleEdgeBandSprite / BuildTriangleSprite / UpdateBubblePlacement)는 **지우지 않고
        // 그대로 남겨 둔다** — 사용자가 "예전 말풍선으로 되돌려 달라"고 할 수 있고, 그때 이 플래그
        // 하나만 true로 되돌리면 종전 그림이 한 줄의 수정도 없이 그대로 돌아온다(리더 지시).
        //
        // ★ const가 아니라 static readonly인 이유: `const bool DrawBubbleShapes = false`로 두면
        //   `if (DrawBubbleShapes) { ... }` 블록 전체가 **CS0162 "도달할 수 없는 코드"** 경고가 된다.
        //   이 프로젝트의 기준선은 경고 0건이므로 컴파일 타임 상수로 만들지 않는다(런타임 분기 한 번의
        //   비용은 대사 표시 빈도를 생각하면 측정조차 되지 않는다).
        private static readonly bool DrawBubbleShapes = false;

        // ---- 만화 레터링 스타일 (배율 1.0 기준 baseline — 실제 사용은 Scaled*/Resolve* 경유) ----
        //
        // ★ 왜 외곽선이 "스타일"이자 동시에 "기능"인가:
        //   말풍선 배경이 사라지면서 글자가 **바탕화면과 직접 맞닿는다.** 잉크색은 캐릭터 프리셋을
        //   따르므로(검정/흰색) 검은 글자 + 어두운 바탕화면, 흰 글자 + 밝은 바탕화면 조합에서는
        //   글자가 그냥 사라진다. 만화 레터링의 표준 문법인 "잉크색 글자 + 반대색 외곽선"이 그 두
        //   요구(만화 느낌 / 가독성)를 **하나의 해법으로** 동시에 푼다.
        // ============================================================================
        // ★★ 2026-09-01 — "말풍선만 글자가 뭉갠다"(사용자 신고 "텍스트들도 선명하지 않고")
        // ============================================================================
        // 검증 담당 실측(에지 폭 = 명암 범위 / 99퍼센타일 기울기, Retina 물리 픽셀):
        //     창 제목 1.41px · 창 탭 1.49px · 창 본문 0.84px · **말풍선 1.80px**
        // 창 UI는 멀쩡하고 말풍선만 넓다. 후보는 둘이었다 — (a) 9.1도 회전 리샘플, (b) 0.84pt 외곽선.
        //
        // ★ 두 후보를 **오프라인 A/B로 갈랐다**(uGUI Text + Outline + 회전 파이프라인을 그대로
        //   복제해 각 변수를 단독으로 스윕. 프로덕션 파라미터에서 실측 1.82px / 복제 1.79px로
        //   일치했으므로 복제가 유효하다):
        //
        //     기울기 9.1도 -> 0도  : 전이 픽셀 -9%,  **속공간 열림 0.000 -> 0.000 (변화 없음)**
        //     외곽선 0.84 -> 0.4pt : 전이 픽셀 -42%, **속공간 열림 0.000 -> 0.186**
        //
        //   즉 <b>범인은 외곽선이고 기울기가 아니다.</b> 각도를 0으로 만들어도 사용자가 본 뭉갬
        //   (하의 ㅇ / 암의 ㅁ 속공간이 메워지는 것)은 <b>한 픽셀도 나아지지 않는다</b>.
        //   그래서 손글씨 기울기 연출은 <b>그대로 둔다</b> — 없앨 이유가 실측으로 사라졌다.
        //
        // 왜 외곽선이 범인인가 — 기하로 확정된다:
        //   uGUI Outline은 원본 메시를 네 대각선(±t, ±t)에 복제하므로 글자 모양이 **사방으로 t만큼
        //   팽창**한다. 속공간은 양쪽에서 좁아지므로 **2t**만큼 줄어든다.
        //   그런데 실측한 한글 속공간의 최소 폭은 em의 11.3%다(아래 상수). 종전 t = 0.06em이면
        //   2t = 0.12em > 0.113em — <b>원리적으로 반드시 닫힌다.</b> 우연이 아니라 필연이었다.

        /// <summary>
        /// 한글 <b>속공간</b>(ㅇ 안, ㅁ 안 …)의 가장 좁은 폭. em(=글자 크기) 대비 비율이다.
        ///
        /// <para>실측(2026-09-01): Apple SD Gothic Neo <b>Bold</b>를 400px em으로 래스터라이즈해
        /// "글자 안에 갇힌 배경"의 가로/세로 런 길이를 전수 조사한 값의 10퍼센타일 중앙값.
        /// 이 렌더러가 실제로 고르는 페이스(Heavy/ExtraBold)는 이보다 <b>더 좁으므로</b> 이 값은
        /// 보수적인 쪽이 아니라 <b>낙관적인</b> 쪽이다 — 예산을 여기에 맞추면 안전 여유가 남는다.</para>
        ///
        /// <para>표본: 하 암 오 늘 뭐 지 창 위 는 미 끄 러 워 구 경 중 이 야 (12자에서 유효 속공간 검출).
        /// 글자별 분포는 0.076 ~ 0.164em이었고, 최솟값이 아니라 10퍼센타일을 쓰는 이유는 1~2px짜리
        /// 래스터 잡음이 최솟값을 비현실적으로 끌어내리기 때문이다.</para>
        /// </summary>
        public const float NarrowestHangulCounterEmRatio = 0.113f;

        /// <summary>
        /// 외곽선이 <b>잡아먹어도 되는</b> 속공간의 비율(양쪽 합 기준). 1.0이면 속공간이 정확히
        /// 0이 되는 지점 — 종전 값 0.06em은 이 예산으로 환산하면 <b>1.06</b>이었다(이미 넘겨 있었다).
        ///
        /// <para>0.72로 잡은 근거: 가장 좁은 속공간의 <b>28%가 남는다</b>. 14pt Retina(28px)에서
        /// 0.28 x 0.113 x 28 = 약 0.9px이라, 한 픽셀짜리 구멍이지만 <b>있는 것과 없는 것이
        /// 구분된다</b>. 더 낮추면(=외곽선을 더 얇게) 속공간은 더 열리지만 오프라인 A/B에서
        /// 외곽선 링의 무결성이 0.62 -> 0.37로 무너졌다 — 그 선은 글자와 바탕화면 사이의 유일한
        /// 분리막이라 얇아지는 데 한계가 있다. 0.72가 두 요구가 만나는 지점이다.</para>
        /// </summary>
        public const float CounterClosureBudget = 0.72f;

        /// <summary>
        /// 글자 외곽선 두께 = **글자 크기에 대한 비율**(em 비율). ★ 고정 두께로 두면 안 된다
        /// (2026-08-29 리더 지시, 그리고 실측으로 확인한 실패): 외곽선은 글자 뒤에 사방으로 깔리므로
        /// 글자가 작아질수록 이웃 글자의 후광끼리 붙어 자모 사이를 메운다. 한글은 한 글자에 자모가
        /// 2~3개라 라틴 문자보다 훨씬 빨리 뭉개진다 — 폰트를 줄이면 선도 같은 비율로 줄어야 한다.
        ///
        /// <para>★ 숫자를 손으로 적지 않고 <b>실측 + 예산에서 유도</b>한다(2026-09-01). 속공간은
        /// 양쪽에서 2t만큼 좁아지므로 <c>2t = 속공간 x 예산</c>, 즉 <c>t = 속공간 x 예산 / 2</c>다.
        /// 0.113 x 0.72 / 2 = <b>0.0407em</b>(14pt에서 0.57pt = Retina 1.14물리px).
        /// 폰트를 바꾸거나 예산을 조정하면 이 값이 자동으로 따라온다 — 종전처럼 리터럴 0.06을
        /// 적어 두면 <b>왜 그 값인지가 사라지고</b>, 실제로 그렇게 사라진 채 속공간을 넘겨 있었다
        /// (직전 주석은 존재하지도 않는 값 0.09의 근거를 설명하고 있었다).</para>
        /// </summary>
        public const float TextOutlineEmRatio = NarrowestHangulCounterEmRatio * CounterClosureBudget * 0.5f;
        /// <summary>외곽선 두께의 화면상 하한(캔버스 유닛 = OS 포인트). 아주 작은 글자에서도 선이
        /// 0으로 수렴해 사라지면 안 된다 — 이 선이 글자와 바탕화면 사이의 유일한 분리막이다.
        ///
        /// <para>★ 2026-09-01 남겨 두는 미해결 항목(리더 보고 대상): 이 하한은 <b>화면 배율을
        /// 모른다</b>. Retina(배율 2)에서는 0.8물리px이고 비Retina/Windows 100%(배율 1)에서는
        /// 0.4물리px — 후자는 사실상 반투명 얼룩이라 "분리막"이 되지 못한다. 물리적으로 옳은 하한은
        /// <b>1 물리 픽셀</b>(= 1 / <c>ScreenCoordinateConverter.ResolveCanvasScaleFactor</c>)이지만,
        /// 그렇게 바꾸면 배율 1 환경의 외곽선이 0.4 -> 1.0pt로 <b>두꺼워져</b> 속공간 예산
        /// (<see cref="CounterClosureBudget"/>)을 그쪽에서 다시 넘긴다. 즉 배율 1에서는 두 요구가
        /// 물리적으로 양립하지 않으며, 진짜 답은 그 환경의 <b>폰트 하한을 올리는 것</b>이다.
        /// 이번 라운드는 사용자 지시로 macOS(항상 Retina 2)만 다루므로 값을 건드리지 않는다.</para>
        ///
        /// <para>★ 2026-09-01 후속 — <b>배포 조합에서는 이 하한이 더 이상 물지 않는다.</b>
        /// <see cref="ResolveMinComicFontSize"/>가 글자 하한을 "em 규칙이 1 물리픽셀을 내는 크기"로
        /// 올렸고(그 문서의 유도 참고), 그 하한(13pt)의 em 규칙이 0.53pt라 여기를 이미 넘는다. 즉
        /// <b>고정 두께가 속공간 예산을 넘기던 유일한 경로가 닫혔다</b>. 지금 이 값이 실제로 무는 곳은
        /// 캔버스 배율 3배 이상(물리 요구치가 <see cref="LegacyComicFontFloor"/>보다 낮아져 가독성
        /// 하한이 이기는 구간)뿐이며, 그 구간에서는 링이 이미 1물리px을 넘으므로 안전하다.</para></summary>
        private const float MinTextOutlineThickness = 0.4f;
        /// <summary>
        /// 만화 레터링 폰트 배율. ★ 2026-08-29 사용자 요구 "일단 텍스트 크기 지금의 절반".
        /// 리더가 지정한 목표는 현재 출하 배율(characterScale = 0.75)에서 **실효 6pt**이고,
        /// 16(설정값) x 0.75(캐릭터 배율) x 0.5 = 6이 정확히 그 값이다.
        /// ★ 특정 숫자에 하드코딩하지 않는다 — `Mathf.Max(하한, 설정값 x 캐릭터배율 x 이 값)` 구조를
        /// 그대로 유지하므로, 사용자가 characterScale이나 dialogueFontSize를 바꾸면 함께 따라간다.
        /// </summary>
        private const float ComicFontScale = 0.875f;
        /// <summary>텍스트 전용 모드의 줄바꿈 최대 폭(배율 1.0 기준). 말풍선 시절(220)보다 좁다 —
        /// 만화 레터링은 가로로 긴 한 줄보다 짧은 여러 줄로 쌓이는 쪽이 자연스럽다.</summary>
        private const float ComicMaxTextWidth = 170f;
        /// <summary>글자 블록과 머리 사이의 대각선 간격(전신 높이 대비 비율). 가로 / 세로.
        /// ★ 절대 유닛이 아니라 비율인 이유는 HeadTopOffsetRatio와 완전히 같다 — 사용자가
        /// characterScale을 계속 바꾸므로(현재 0.75) 절대값은 그 순간 전부 틀린 값이 된다.</summary>
        private const float TextGapXRatio = 0.20f;
        private const float TextGapYRatio = 0.10f;
        /// <summary>팝인(툭 튀어나오는 등장) 지속 시간 — 규칙 6 개정(2026-09-01)으로 **알파 페이드인과
        /// 분리된 독립 상수(180ms)** 가 됐다. 예전에는 <c>= FadeInSeconds</c>로 묶여 있어 알파를
        /// 줄이면 바운스도 함께 짧아졌고, 그 커플링이 둘 다 어중간하게 만들었다(교차 레이어 로그 L3).
        /// 값의 단일 소스는 <see cref="DialogueTiming"/>다 — 발화 자격 게이트(규칙 8)가 같은 상수를
        /// 읽어야 하므로 렌더러가 소유하지 않는다.</summary>
        private const float PopInSeconds = DialogueTiming.PopInSeconds;
        private const float PopInStartScale = 0.55f;
        private const float PopInOvershoot = 1.12f;
        /// <summary>팝인에서 오버슈트 정점에 도달하는 지점(0~1 진행도).</summary>
        private const float PopInPeakAt = 0.6f;
        // ========================================================================
        // ★★ 글자 기울기 (사용자 요구 2026-08-31 "캐릭터가 말하는 텍스트는 좀 대각선으로 작성해줘")
        // ========================================================================
        // 직전까지의 기울기는 "손글씨 흔들림"(±2.5도, 부호는 대사 해시에서 사실상 무작위)이었다.
        // 2.5도는 10pt 한 줄짜리 블록에서 양 끝 높이차가 2pt 남짓이라 **의도적으로 눈에 띄지 않는**
        // 값이고, 부호까지 대사마다 뒤집혀 "기울어져 있다"는 인상 자체가 생기지 않았다 — 사용자가
        // 화면에서 본 것은 사실상 수평 글자였다. 배치(대각선 상단)만 대각선이고 글자는 아니었다.
        //
        // 그래서 두 가지를 바꾼다:
        //   (1) 크기 — 눈에 보이는 각도로 올린다(StickConfig.dialogueTiltDegrees, 기본 8도).
        //   (2) 부호 — 무작위가 아니라 **글자가 놓인 쪽의 거울상**으로 고정한다.
        //       왼쪽 위에 놓이면 반시계(+, 오른쪽 끝이 올라감) / 오른쪽 위면 시계(-).
        //       이 부호라야 블록에서 캐릭터에 **가장 가까운 아래쪽 안쪽 모서리가 들리는 쪽**이 된다.
        //       (폭 80pt 블록에서 8도면 그 모서리가 5.6pt 올라간다 — 잘림은 어차피 아래 회전 경계
        //       클램프가 막지만, 눈에 보이는 여백이 최소 보장치보다 넓어져 글자가 머리에서 대각선으로
        //       **날아가는** 것처럼 읽힌다. 반대 부호면 같은 모서리가 그만큼 머리 쪽으로 내려온다.)
        //       덤으로 좌우 배치가 서로 정확한 거울상이 되어, 캐릭터가 돌아설 때 글자도 함께 뒤집힌다.
        //       부호가 대사마다 뒤집히면 같은 자리에 놓인 글자가 매번 다른 방향으로 넘어져 배치의
        //       대각선과 싸운다.
        // 손글씨 느낌을 버리는 것은 아니다 — 결정적 해시는 이제 **부호가 아니라 크기의 미세 편차**에 쓴다.

        /// <summary>설정(StickConfig)을 받지 못했을 때 쓰는 기울기 기준값(도).
        /// 8도의 근거: 두 줄(약 24pt) x 170pt 블록에서 양 끝 높이차가 약 24pt — 한 줄 높이만큼
        /// 기울어 한눈에 "비스듬하다"가 보이면서, 글자 하나하나의 세로축은 여전히 수직에 가까워
        /// 한글 네모 글리프의 가독성이 유지되는 상한대다(15도를 넘기면 넘어지는 것처럼 보인다).</summary>
        private const float DefaultComicTiltDegrees = 8f;
        /// <summary>대사마다 달라지는 기울기 **크기**의 편차 비율(±). 부호가 아니라 크기에만 걸린다
        /// (8도 기준 6~10도). 대사 문자열의 결정적 해시에서 뽑으므로 같은 대사는 항상 같은 각도이고,
        /// 프레임마다 다시 계산해도 글자가 떨리지 않는다(Dialogue/AmbientChatter.cs와 같은 컨벤션).</summary>
        private const float ComicTiltJitterRatio = 0.25f;
        /// <summary>
        /// 기울기를 켜기 위한 글리프의 **물리 픽셀** 하한. 이 아래에서는 기울이지 않는다.
        ///
        /// ★ 단위가 바뀌었다(종전: "캔버스 유닛 10 미만이면 끔"). 근거가 된 실측(2026-08-29)은
        /// "12px 한글은 회전 리샘플링으로 자모가 통째로 뭉개지고 32px는 멀쩡하다"였는데, 거기서의 px는
        /// uGUI가 글리프를 굽는 **물리 픽셀**이지 캔버스 유닛이 아니다. Retina 대응 이후 캔버스는
        /// scaleFactor = 1/dpi로 그려지므로 물리 픽셀 = fontSize x canvas.scaleFactor다. 같은 10pt가
        /// Retina에서는 20px(문제없음)이고 1x 화면에서는 10px(뭉개짐)이라, 종전처럼 캔버스 유닛으로
        /// 재면 정반대인 두 경우를 구분하지 못한다 — Retina에서 멀쩡한 글자의 기울기를 끄거나
        /// 1x에서 뭉개질 글자를 기울이거나 둘 중 하나가 된다.
        /// 14의 근거: 뭉개짐이 확인된 12 바로 위, 멀쩡한 32 아래. 현재 출하 조합(10pt x Retina 2x =
        /// 20px)은 여유 있게 통과하고 만화 모드 폰트 하한(9pt x 2 = 18px)도 통과한다 — 즉 Retina에서는
        /// 캐릭터 배율을 어떻게 바꿔도 기울기가 조용히 꺼지지 않는다.
        /// </summary>
        private const float ComicTiltMinGlyphPixels = 14f;
        /// <summary>감탄사 강조 배율 — 느낌표가 든 대사("윽…!")를 조금 더 크게 외친다(만화 문법).</summary>
        private const float ComicEmphasisScale = 1.14f;
        /// <summary>화면 끝 뒤집기 보간 속도(쪽 부호/초). 순간이동처럼 튀지 않게 좌우로 미끄러진다.</summary>
        private const float SideFlipSpeed = 5f;

        // ============================================================================
        // ★★ 두 배율의 합성 — 어디에 무엇을 곱하는가 (2026-08-29, 리더 지시)
        // ============================================================================
        // 이 컴포넌트에는 성격이 전혀 다른 배율이 **두 개** 얹힌다. 둘을 같은 곳에 곱하면 반드시 어긋난다.
        //
        //   [A] Retina DPI 배율  — CanvasScaler.scaleFactor = 1/dpi (ApplyCanvasScaleFactor)
        //       단계: 캔버스 유닛 -> 물리 픽셀.  겉보기 크기를 **바꾸지 않는다**(해상도만 올린다).
        //       그래서 아래 상수들은 이 배율을 몰라도 되고, 알아서도 안 된다 — 이 배율 덕분에
        //       "캔버스 1유닛 == OS 포인트 1"이라는 불변식이 성립하고, 배치 계산(UnityScreenToCanvas)이
        //       그 불변식 위에 서 있다.
        //
        //   [B] 캐릭터 크기 배율 — StickmanMetrics.Scale (BubbleScale)
        //       단계: 상수 -> 캔버스 유닛.  겉보기 크기를 **바꾼다**(캐릭터가 절반이면 말풍선도 절반).
        //
        // 최종 화면 크기 = 상수 x [B] x [A]px/pt.  즉 배율 0.5 캐릭터 + Retina 2x면
        // "테두리 2.5 -> 1.25pt -> 물리 2.5px"가 된다. [B]를 CanvasScaler에 얹지 않는 이유가 여기 있다:
        // 그랬다면 [A]의 불변식이 깨져 배치가 어긋나고, 무엇보다 **폰트까지 함께 비례**해 버린다.
        // 폰트는 아래 ResolveFontSize()가 가독성 하한을 걸어 따로 처리해야 하는 유일한 예외다.
        //
        // ============================================================================
        // 왜 [B]가 필요한가 — 실측 (리더 보고, 2026-08-29)
        // ============================================================================
        // 캐릭터 크기 작업으로 전신이 2.2747 -> 1.1373유닛(화면상 80pt -> 40pt)이 됐다. 그런데 말풍선
        // 몸통 높이는 대략 `textH x 1.414 + 19pt`이고, 그 19pt는 (BorderThickness 2.5 + TextPadding 7)의
        // 양쪽 합이라 **폰트와 무관한 고정분**이다. 그래서 fontSize를 코드 하한 8까지 낮춰도 32pt가
        // 바닥이라, 폰트 설정만으로는 말풍선이 캐릭터(40pt)보다 커지는 것을 못 막는다 —
        // 고정분 자체를 배율에 태워야 한다.
        private const float BaselineTotalHeightFallback = StickConfig.BaselineCharacterTotalHeight;

        /// <summary>
        /// 말풍선 기하(테두리/여백/꼬리/최대 줄바꿈 폭)에 곱할 캐릭터 크기 배율.
        /// 단일 소스는 <see cref="StickmanMetrics"/>(프리팹 계층 실측) — 상수를 복사하지 않는다.
        /// 컴포넌트를 못 찾는 리그/테스트에서는 1.0으로 폴백해 예전과 동일하게 동작한다.
        ///
        /// 하한 0.35: 배율을 아주 작게 준 사용자가 있어도 테두리가 0에 수렴해 사라지거나 여백이 음수가
        /// 되지 않게 받친다(캐릭터 획 두께에 화면상 하한을 둔 SceneBootstrapper와 같은 태도).
        /// </summary>
        private float BubbleScale
        {
            get
            {
                StickmanMetrics metrics = _metrics != null ? _metrics : (_metrics = StickmanMetrics.Find(this));
                float scale = metrics != null ? metrics.Scale : 1f;
                return Mathf.Clamp(scale, 0.35f, 4f);
            }
        }

        private StickmanMetrics _metrics;

        // 배율이 곱해진 실제 사용 값 — 아래 레이아웃/생성 코드는 반드시 이쪽을 쓴다(원본 상수 직접 사용 금지).
        private float ScaledBorderThickness => BorderThickness * BubbleScale;
        private float ScaledTextPadding => TextPadding * BubbleScale;
        private float ScaledMaxTextWidth => MaxTextWidth * BubbleScale;
        private float ScaledTailWidth => TailWidth * BubbleScale;
        private float ScaledTailHeight => TailHeight * BubbleScale;
        /// <summary>
        /// 꼬리 윗변을 몸통 타원 경계보다 이만큼 **안쪽**에 둔다.
        ///
        /// ★ 투명 말풍선 전환(2026-08-29)으로 의미가 바뀌었다. 예전에는 "채움이 몸통 아래 테두리를
        /// 덮는 양"(5pt)이었지만, 이제 몸통은 링이고 안쪽이 비어 있다 — 겹침이 링 두께보다 크면 꼬리
        /// 선의 윗부분이 **투명한 몸통 안쪽으로 삐져 들어와** 말풍선 안에 짧은 선 두 개가 떠 보인다.
        /// 그래서 정확히 링 두께만큼만 넣는다: 꼬리 선의 열린 윗변이 링 두께 안에 완전히 파묻혀
        /// 링과 이어지고, 안쪽으로는 한 픽셀도 넘어오지 않는다.
        /// (원본 상수 TailPanelOverlap은 그 시절 근거와 함께 문서로 남겨 둔다 — 지금은 쓰이지 않는다.)
        /// </summary>
        private float ScaledTailPanelOverlap => ScaledBorderThickness;

        /// <summary>글자 외곽선 두께(캔버스 유닛). **글자 크기에 비례**하되 화면상 하한을 받친다
        /// (<see cref="TextOutlineEmRatio"/> 문서 참고 — 고정 두께면 작은 글자를 잡아먹는다).</summary>
        private float ScaledTextOutline => Mathf.Max(MinTextOutlineThickness, ResolveFontSize() * TextOutlineEmRatio);
        /// <summary>텍스트 전용 모드의 줄바꿈 최대 폭(캔버스 유닛).</summary>
        private float ScaledComicMaxTextWidth => ComicMaxTextWidth * BubbleScale;

        /// <summary>
        /// 말풍선 글자 크기(캔버스 유닛 = OS 포인트). **여기만 단순 비례가 아니다.**
        ///
        /// 기하(테두리/여백/꼬리)는 캐릭터 배율에 그대로 비례해도 되지만 글자는 안 된다 — 크기를 줄이면
        /// 어느 지점부터 그냥 못 읽는다. 그래서 비례로 줄이되 <see cref="MinReadableFontSize"/>에서
        /// 바닥을 받친다(SceneBootstrapper가 캐릭터 획 두께에 화면상 하한을 둔 것과 같은 문법).
        ///
        /// 하한 12pt의 근거: 배율 0.5에서 단순 비례면 8pt이고, 그 크기의 한글은 획이 서로 붙어 읽히지
        /// 않는다(이 프로젝트가 이미 겪은 문제 — ResolveKoreanFont의 "합성 볼드가 16px 한글을 뭉갠다"
        /// 기록 참고). 12pt면 배율 0.5에서 말풍선 전체가 약 29pt = 캐릭터(40pt)의 73%로, 머리 위에
        /// 자연스럽게 얹히면서 글자도 읽힌다. Retina에서 실제 렌더는 24 물리픽셀이라 선명도도 충분하다.
        /// </summary>
        private int ResolveFontSize()
        {
            // ★ 2026-09-01 설정창 — 사용자가 고른 값은 배포 에셋이 아니라 AppSettingsModel에 있다
            //   (그 클래스 문서의 "왜 StickConfig에 직접 쓰지 않는가" 참고). 고른 적이 없으면
            //   에셋의 값이 그대로 나오므로 지금까지의 거동은 한 톨도 바뀌지 않는다.
            int configured = Mathf.Max(8, StickMate.Core.AppSettingsModel.ResolveDialogueFontSize(_config));
            // ★ 배율을 여기서 한 번만 조회해 아래 하한과 글리프 스냅이 <b>같은 값</b>을 본다 —
            //   두 번 부르면 그 사이에 모니터가 바뀌었을 때 하한과 스냅이 서로 다른 배율로 계산된다.
            float canvasScale = ScreenCoordinateConverter.ResolveCanvasScaleFactor(_config);
            // 말풍선 모드로 되돌리면 종전 크기(하한 12pt)가 그대로 복원된다.
            int points = DrawBubbleShapes
                ? Mathf.Max(MinReadableFontSize, Mathf.RoundToInt(configured * BubbleScale))
                // 만화 레터링 모드 — 기준값은 ComicFontScale이 내리고, 하한은 <b>외곽선 링이 1 물리
                // 픽셀이 되는 크기</b>에서 유도한다(ResolveMinComicFontSize 문서의 부등식).
                : Mathf.Max(ResolveMinComicFontSize(canvasScale),
                            Mathf.RoundToInt(configured * BubbleScale * ComicFontScale));

            // ★ 2026-09-01 — 마지막에 <글리프 잔차 0>으로 스냅한다(사용자 신고 "텍스트도 다 번져보임").
            //   여기만 정적 상수로 못 고치는 이유: 이 pt는 (사용자 설정 8~28) x (캐릭터 배율) x (만화 배율)의
            //   런타임 곱이라 어떤 값이든 나올 수 있다. UI 창들처럼 "짝수 상수로 옮기기"가 성립하지 않는다.
            //   그래서 규칙 자체(Platform/UiGlyphScalePolicy)를 호출해 <지금 캔버스 배율에서> 정수 픽셀이
            //   되는 가장 가까운 pt로 올린다.
            //   · 배율 1(비Retina) / 2(Retina mac·Windows 200%)에서는 모든 정수 pt가 이미 잔차 0이라
            //     이 호출은 **항등**이다 — macOS와 기존 테스트의 거동이 한 톨도 바뀌지 않는다.
            //   · 배율 1.5(Windows 150%)에서만 홀수가 +1로 올라간다(최대 변화 1pt).
            //   ResolveFontSize()는 말풍선을 그릴 때마다 다시 호출되므로, 창을 배율이 다른 모니터로
            //   옮겨도 다음 대사부터 자동으로 따라간다(생성 시점에 한 번 굳는 UI 창들과 다른 점이다).
            return UiGlyphScalePolicy.SnapPoints(points, canvasScale);
        }

        /// <summary>말풍선(도형) 모드 글자의 화면상 하한(캔버스 유닛 = OS 포인트). 위 ResolveFontSize() 참고.</summary>
        private const int MinReadableFontSize = 12;

        // ============================================================================
        // ★★ 2026-09-01 — 만화 레터링 글자 하한을 <b>물리 픽셀에서 유도</b>한다
        // ============================================================================
        // 직전 라운드가 뭉갬의 범인을 외곽선으로 확정하고 <see cref="TextOutlineEmRatio"/>를 실측에서
        // 유도했지만, <b>실행 중인 빌드가 9pt로 그리고 있어서</b> 그 수정이 화면에 닿지 못했다.
        // 리더 검산이 이유를 확정했다 — 9pt에서는 두 요구가 <b>양립 자체가 불가능</b>하다:
        //
        //     링이 보이려면        외곽선 t ≥ 1 물리픽셀
        //     속공간이 열리려면    t가 em 규칙(0.0407em)으로 나와야 한다
        //                          (고정 하한 0.4pt가 물면 그 순간 예산을 넘긴다)
        //     두 요구를 합치면     0.0407em × fontSize ≥ 1 물리픽셀
        //
        // 9pt = 18물리px에서 Heavy 한글 속공간은 약 2물리px이고, 외곽선이 사방 1px씩 먹으면 0이 된다.
        // <b>외곽선을 어떻게 잡아도 9pt에서는 해결되지 않는다</b> — 고칠 수 있는 변수는 글자 크기뿐이다.
        //
        // ★ 그래서 하한을 숫자로 적지 않고 <b>위 부등식을 그대로 코드로 옮긴다</b>. 이 저장소가
        //   반복해 겪은 사고는 전부 "한 진실의 사본이 두 곳에 있는데 대조 장치가 없다"였다.
        //   외곽선 비율(=속공간 실측 × 예산)을 다시 재는 날 하한이 <b>자동으로</b> 따라온다.

        /// <summary>
        /// 외곽선 링이 "있다"고 보이기 위한 최소 두께(<b>물리 픽셀</b>). 1보다 작으면 GPU가 한 픽셀을
        /// 부분 커버리지로 섞어 <b>반투명 얼룩</b>이 되고, 그건 글자와 바탕화면 사이의 분리막이 되지
        /// 못한다(말풍선 도형을 없앤 뒤로 이 링이 <b>유일한</b> 분리막이다).
        /// </summary>
        public const float OutlineRingMinPhysicalPixels = 1f;

        /// <summary>
        /// 이 저장소가 <b>실제 캡처로 판정한</b> 캔버스 배율 — macOS Retina(항상 2) / Windows 200%.
        /// <see cref="ResolveMinComicFontSize"/>의 상한이 여기서 나온다.
        ///
        /// <para>★ 배율 1(Windows 100%·비Retina)에서는 위 부등식이 <b>25pt</b>를 요구한다. 그건 배율
        /// 0.35 캐릭터(화면상 약 28pt)보다 글자가 커진다는 뜻이라, 신고를 하나 고치고 다른 하나를
        /// 만드는 교환이다. 사용자 지시(2026-09-01 "윈도우는 일단 미루고 맥만")에 따라 그 환경은
        /// <b>검증된 요구치에서 멈춘다</b> — 링이 1물리px에 못 미치는 갭이 남으며, 그 갭은
        /// <c>Tests/EditMode/ComicFontFloorOutlineRingTests</c>가 <c>Assert.Ignore</c>로 계속
        /// 러너에 "건너뜀"으로 보여 준다(CLAUDE.md: 못 고친 갭은 잊히지 않게 남긴다).</para>
        /// </summary>
        public const float VerifiedCanvasScale = 2f;

        /// <summary>
        /// 만화 레터링 글자의 <b>이력상 가독성 하한</b>(사용자 요구에서 온 값).
        /// 이력: 12(말풍선 시절) -> 6("지금의 절반") -> 9("지금의 1.5배", 6 x 1.5).
        ///
        /// <para>물리 요구치가 이보다 낮게 나오는 초고배율(3배 초과)에서도 여기서 멈춘다 — 그 아래는
        /// 링과 무관하게 그냥 읽히지 않는다. 회귀 테스트의 <b>네거티브 컨트롤</b>이 이 값을 쓴다:
        /// 하한을 여기로 되돌리면 링 요구가 실제로 깨지는지 증명한다.</para>
        /// </summary>
        public const int LegacyComicFontFloor = 9;

        /// <summary>캔버스 유닛(=OS 포인트)으로 잰 <b>1 물리 픽셀</b>. Retina에서 0.5pt인 것은 배율이
        /// 2여서지 상수여서가 아니다 — 그래서 숫자로 적지 않고 배율에서 나눈다.
        /// 배율을 모르면(0/NaN/무한대) 1pt로 본다(가장 보수적인 = 가장 큰 하한을 내는 쪽).</summary>
        public static float PointsPerPhysicalPixel(float canvasScale)
            => canvasScale > 0f && !float.IsNaN(canvasScale) && !float.IsInfinity(canvasScale)
                ? 1f / canvasScale
                : 1f;

        /// <summary>
        /// 외곽선 링이 <see cref="OutlineRingMinPhysicalPixels"/>를 채우는 데 필요한 글자 크기(pt).
        /// <c>t = fontSize × TextOutlineEmRatio ≥ n × (1pt/배율)</c>를 fontSize에 대해 푼 것이며,
        /// pt는 정수여야 하므로 올림한다. <b>상한 없는 순수 요구치</b>다 — 정책(상한)은
        /// <see cref="ResolveMinComicFontSize"/>가 얹는다.
        /// </summary>
        public static int OutlineLegibleFontFloor(float canvasScale)
            => Mathf.CeilToInt(OutlineRingMinPhysicalPixels * PointsPerPhysicalPixel(canvasScale)
                               / TextOutlineEmRatio);

        /// <summary>
        /// 만화 레터링 모드 글자의 화면상 하한(pt). <see cref="OutlineLegibleFontFloor"/>(물리 요구)를
        /// <see cref="LegacyComicFontFloor"/>(가독성)와 <see cref="VerifiedCanvasScale"/>의 요구치
        /// (검증 범위) 사이로 자른다.
        ///
        /// <para>배율 2에서 이 값은 <b>13pt</b>다(0.0407em × 13 = 0.529pt = 1.06물리px). 종전 9pt는
        /// 0.366pt = 0.73물리px으로 링이 얼룩이 됐고, em 규칙이 고정 하한 0.4pt에 걸려 속공간 예산까지
        /// 넘겼다. 두 결함이 <b>같은 한 줄</b>에서 동시에 사라진다.</para>
        ///
        /// <para>★ 부작용(리더 육안 판정 대상): 하한이 올라가면 <b>캐릭터가 작을 때 글자가 상대적으로
        /// 커진다</b>. 배율 1.0에서는 기준값(16 × 1.0 × 0.875 = 14pt)이 하한보다 커서 변화가 없지만,
        /// 배율 0.75에서는 10 -> 13pt, 배율 0.35에서는 9 -> 13pt가 된다.</para>
        /// </summary>
        public static int ResolveMinComicFontSize(float canvasScale)
            => Mathf.Clamp(OutlineLegibleFontFloor(canvasScale),
                           LegacyComicFontFloor,
                           OutlineLegibleFontFloor(VerifiedCanvasScale));

        /// <summary>
        /// 이 캐릭터의 전신 높이(월드 유닛). 캐릭터 기준 오프셋의 유일한 기준값.
        ///
        /// 조회 순서: StickmanAgent(그 자신이 <see cref="StickmanMetrics"/>.TotalHeight로 위임한다) ->
        /// 계층에서 직접 찾은 StickmanMetrics -> 배율 1.0 폴백. 두 번째 단계가 있어야 에이전트를 갖지
        /// 않는 화자(테스트 리그 등)에서도 오프셋이 캐릭터 배율을 따라간다 —
        /// 폴백 상수로 떨어지면 배율 0.75 캐릭터 옆에 배율 1.0 간격으로 글자가 뜬다.
        /// </summary>
        private float CharacterHeight
        {
            get
            {
                if (_agent != null) return _agent.CharacterTotalHeightWorld;
                StickmanMetrics metrics = _metrics != null ? _metrics : (_metrics = StickmanMetrics.Find(this));
                return metrics != null ? metrics.TotalHeight : BaselineTotalHeightFallback;
            }
        }

        /// <summary>머리 중심에서 꼬리 끝까지(월드 유닛) — 해상도/줌 무관, 캐릭터 크기 추종.</summary>
        private float HeadTopWorldOffset => CharacterHeight * HeadTopOffsetRatio;

        /// <summary>글자 블록과 머리 사이의 가로 간격(월드 유닛) — 캐릭터 크기 추종.</summary>
        private float TextGapWorldX => CharacterHeight * TextGapXRatio;

        /// <summary>글자 블록과 머리 사이의 세로 간격(월드 유닛) — 캐릭터 크기 추종.</summary>
        private float TextGapWorldY => CharacterHeight * TextGapYRatio;
        // 규칙 6 개정(2026-09-01): 등장 150ms -> **60ms**. 만화 레터링은 페이드로 등장하지 않는다 —
        // 등장감은 PopInSeconds(스케일 바운스, 180ms)가 만든다. 소멸 120ms는 "100~150ms" 범위 안이라
        // 유지. 두 값의 단일 소스는 DialogueTiming이다(발화 자격 게이트가 페이드인을 함께 읽는다).
        private const float FadeInSeconds = DialogueTiming.FadeInSeconds;    // 규칙 6 "등장 60ms".
        private const float FadeOutSeconds = DialogueTiming.FadeOutSeconds;  // 규칙 6 "소멸 100~150ms".

        // ============================================================================
        // ★ 타원 말풍선 기하 (사용자 신고 2026-08-29: "말풍선도 네모가 아닌 타원 형태의 말풍선")
        // ============================================================================
        // 가로 tw, 세로 th인 글자 블록을 품는 **최소 넓이 타원**의 반지름은 (tw/2·√2, th/2·√2)다
        // (직사각형의 네 꼭짓점이 타원 위에 놓이는 조건 (x/a)²+(y/b)²=1을 넓이 최소로 푼 결과).
        // 그래서 사각형 시절의 "글자 + 여백" 대신 "글자·√2 + 여백"으로 몸통 크기를 잡는다 —
        // 이걸 빼먹으면 모서리 쪽 글자가 타원 밖으로 삐져나온다.
        private const float EllipseFitFactor = 1.41421356f;
        /// <summary>타원에 내접하는 최대 직사각형의 가장자리 여백 비율((1 - 1/√2)/2). 글자 RectTransform을
        /// 이만큼 안으로 넣으면 글자 영역이 정확히 그 내접 사각형이 된다.</summary>
        private const float EllipseInsetFactor = 0.14644661f;

        // ============================================================================
        // ★ 글자 선명도 (사용자 신고 2026-08-29: "텍스트 폰트가 부드럽지 않음" / "해상도가 너무 안좋음")
        // ============================================================================
        // 진단(자세한 근거는 아래 ResolveKoreanFont 위 주석): 진짜 원인은 이 컴포넌트가 아니라
        // ProjectSettings의 `macRetinaSupport: 0`이었다 — 앱 프레임버퍼가 1x인데 화면이 2x Retina라
        // OS 컴포지터가 전부 2배로 확대하고 있었다.
        //
        // **2026-08-29 Retina 대응 라운드에서 그 근본 원인이 해소됐다**(`macRetinaSupport: 1` +
        // CanvasScaler.scaleFactor = 1/dpi, ApplyCanvasScaleFactor() 참고). 이제 캔버스가 물리 픽셀
        // 기준으로 2배 해상도에 그려지고 uGUI가 글리프를 `fontSize * canvas.scaleFactor` 크기로
        // 래스터라이즈하므로, 글자는 **네이티브 Retina 해상도로 직접** 그려진다.
        //
        // 그래서 이 슈퍼샘플링 배율은 1(=끔)로 되돌린다. 근본 원인이 사라진 지금 2로 두면 글리프를
        // 32 x 2 = 64px로 구워 절반으로 줄여 그리는 셈이라, 폰트 아틀라스만 4배로 쓰면서 오히려 한 번
        // 더 리샘플링돼 미세하게 무뎌진다(상시 실행 앱이라 아틀라스 낭비도 그냥 낭비가 아니다).
        // 상수와 나눗셈 경로는 그대로 남겨 둔다 — 값이 1이면 전부 항등이 되고, 훗날 비Retina 환경에서
        // 다시 필요해지면 이 숫자 하나만 바꾸면 된다.
        private const int TextSupersample = 1;

        [SerializeField] private StickmanAgent _agent;   // 플레이어용 자동 배선(같은 GameObject 우선).
        [SerializeField] private Transform _anchor;      // 머리 Transform. 비면 Awake에서 "Head"를 찾는다.
        [SerializeField] private StickConfig _config;

        [Tooltip("true면 Bind()로 화자가 명시되기 전까지 어떤 대사도 그리지 않는다. 자기 상태머신을 " +
                 "상태머신이 첫 대결 시점에야 만들어지는 화자용 — 이 플래그가 없으면 그 사이에 " +
                 "'화자 미지정 = 모든 대사 수신' 폴백이 걸려 남의 대사를 자기 머리 위에 " +
                 "그려버린다(UX_FLOW.md 5절 규칙 7 위반).")]
        [SerializeField] private bool _requireBoundSpeaker;

        // 이 렌더러가 담당하는 화자. null이면 "모든 대사를 받는다"(단일 캐릭터 폴백).
        private StickmanStateMachine _machine;

        private Canvas _canvas;
        private CanvasScaler _scaler;
        private CanvasGroup _group;
        private RectTransform _panel;      // 검은 테두리(바깥)
        private RectTransform _tailOutline;
        private RectTransform _tailFill;
        private Image _panelOutlineImage;
        private Image _panelInnerImage;
        private Image _tailOutlineImage;
        private Image _tailFillImage;
        private Text _label;
        private Outline _labelOutline;   // 만화 레터링 외곽선(잉크색의 반대색). 텍스트 전용 모드에서만 붙는다.
        private RectTransform _labelRect;
        private Camera _camera;

        // 타원 몸통 스프라이트 — 크기가 바뀔 때마다 그 크기에 딱 맞춰 다시 만든다(아래
        // UpdateEllipseSprites 문서 참고). 인스턴스별로 갖는다: 말풍선 크기는 화자마다 다르다.
        private Sprite _ellipseOuterSprite;
        private Sprite _ellipseInnerSprite;
        private Vector2Int _ellipseSpriteSize = new Vector2Int(-1, -1);

        // ==================== 표시 상태 ====================
        private DialogueIntent _active;        // 지금 표시 중인 대사(만료됐지만 최소 노출 중일 수도 있음).
        private string _activeText;
        private float _shownAtUnscaledTime;
        private DialogueKind _activeKind;      // 지금 표시 중인 대사의 종류(규칙 4-a). 만료 처리의 분기 축.
        private bool _expiredPendingFadeOut;   // 정상 종료로 만료된 **반응** 대사 — 가독예산을 채운 뒤 페이드아웃.
        private bool _fadingOut;
        private float _alpha;

        // 같은 프레임의 강제 인터럽트 판정용(클래스 문서 "이벤트 순서에 대한 근거" 참고).
        private bool _lastTransitionForced;
        private int _forcedInterruptFrame = -1;

        // ==================== 만화 레터링 표시 상태 ====================
        // ★ 왜 "쪽"을 대사 시작 시점에 한 번 정하고 그대로 두는가 (리더 지시에 대한 판단 근거)
        //   캐릭터는 걷다가 화면 끝에서 돌아서고, 유휴 중에도 방향이 바뀔 수 있다. 매 프레임
        //   FacingSign을 그대로 따라가면 캐릭터가 돌아설 때마다 글자가 머리 위를 가로질러 좌우로
        //   날아다녀 **읽을 수가 없다**. 그래서 쪽은 대사가 뜨는 순간의 진행 방향으로 한 번 확정하고
        //   그 대사가 사라질 때까지 유지한다(대사 수명은 최대 4초 — dialogueMaxVisibleSeconds).
        //   글자 자체는 머리를 계속 따라다니므로 캐릭터가 글자를 두고 떠나는 일은 없다.
        //   유일한 예외가 화면 끝 클램프인데, 그때도 순간이동이 아니라 _sideBlend로 미끄러진다.
        private float _latchedTextSide = -1f; // +1 = 캐릭터 오른쪽 / -1 = 왼쪽. 기본값은 "오른쪽을 보고 있다"의 반대.
        private float _sideBlend = -1f;       // 실제로 그려지는 연속 쪽 값(-1 ~ +1). 뒤집기 보간용.
        private bool _snapSideBlend = true;   // 새 대사의 첫 프레임에는 보간 없이 제자리에서 시작한다.
        private float _popElapsed;            // 팝인 경과 시간(초).
        private float _steadyScale = 1f;      // 감탄사 강조 등 정상 상태의 배율(팝인과 곱해진다).
        private float _tiltMagnitudeDegrees;  // 이 대사의 기울기 **크기**(도, 부호 없음). 대사마다 고정.
        private float _tiltDegrees;           // 실제로 적용되는 부호 있는 기울기(= -놓인 쪽 x 크기).

        // ==================== 테스트/진단용 공개 관측점 ====================
        /// <summary>지금 말풍선이 화면에 있는가(알파 &gt; 0이고 루트가 활성). 즉시 제거의 "같은 프레임"
        /// 보장을 PlayMode 테스트가 동기적으로 확인하는 지점이다.</summary>
        public bool IsBubbleVisible => _active != null || _fadingOut;

        /// <summary>지금 화면에 떠 있는 대사 텍스트(없으면 null).</summary>
        public string VisibleText => IsBubbleVisible ? _activeText : null;

        /// <summary>지금 떠 있는 대사가 화면에 있었던 시간(초). 안 떠 있으면 0.
        /// <para>★ 교체 경로의 발화 자격(<see cref="DialogueBudget.CanReplaceVisible"/>)을 검증하려면
        /// <b>노출 시계가 리셋됐는가</b>를 봐야 하는데, 텍스트만으로는 "같은 글자로 교체됐다"와
        /// "교체되지 않았다"가 구분되지 않는다.</para></summary>
        public float VisibleSeconds => IsBubbleVisible ? Time.unscaledTime - _shownAtUnscaledTime : 0f;

        /// <summary>마지막으로 "강제 인터럽트에 의한 즉시 제거"가 일어난 Time.frameCount(없으면 -1).</summary>
        public int LastImmediateRemovalFrame { get; private set; } = -1;

        /// <summary>지금까지 즉시 제거가 몇 번 일어났는지(회귀 테스트 카운터).</summary>
        public int ImmediateRemovalCount { get; private set; }

        /// <summary>마지막으로 계산된 글자 블록 중심(캔버스 유닛). 테스트/진단 전용 관측점.</summary>
        public Vector2 LastTextCenterCanvas { get; private set; }

        /// <summary>마지막으로 계산된 기준점 = 머리 바로 위(캔버스 유닛). 테스트/진단 전용 관측점.</summary>
        public Vector2 LastTextAnchorCanvas { get; private set; }

        /// <summary>지금 글자가 놓인 쪽(+1 캐릭터 오른쪽 / -1 왼쪽). 정의상 **진행 방향의 반대**다.</summary>
        public float LastTextSideSign { get; private set; } = -1f;

        /// <summary>배치에 실제로 쓰인 글자 블록 크기(캔버스 유닛, 강조 배율 반영·팝인 제외).
        /// 테스트가 "기준점에서 글자 블록 **가장자리**까지의 간격"을 계산하는 데 쓴다.</summary>
        public Vector2 LastTextSizeCanvas { get; private set; }

        /// <summary>글자 블록에 실제로 적용된 기울기(도, 반시계가 +). 정의상 **놓인 쪽의 거울상**이라
        /// 왼쪽 위에 놓이면 +, 오른쪽 위에 놓이면 -다. 0이면 기울기가 꺼진 것이다
        /// (<see cref="ComicTiltMinGlyphPixels"/> 하한 또는 설정값 0). 테스트/진단 전용 관측점.</summary>
        public float LastTextTiltDegrees { get; private set; }

        /// <summary>
        /// 화자가 바라보는 방향(+1 오른쪽 / -1 왼쪽)의 공급자. null이면 <see cref="StickmanAgent"/>의
        /// Blackboard.FacingSign을 읽는다.
        ///
        /// 왜 주입 창구가 필요한가: 플레이어 외의 화자(테스트 리그 등)는 플레이어와 다른
        /// <c>StickmanBlackboard</c>를 자기 필드로 들고 있고 StickmanAgent를 갖지 않는다 — 그 화자의
        /// 말풍선 렌더러가 플레이어의 방향을 읽으면 글자가 엉뚱한 쪽에 붙는다(규칙 7 화자 분리의
        /// 배치 판). Bind()의 시그니처를 바꾸지 않고 붙일 수 있는 최소한의 이음매다.
        /// </summary>
        public System.Func<float> FacingSource { get; set; }

        /// <summary>
        /// 이 렌더러가 담당할 화자를 지정한다. 자기 상태머신을 따로 가진 캐릭터가
        /// 자기 말풍선만 그리게 하려고 쓴다(5절 규칙 7). 플레이어는 Start()에서 자동 배선되므로 보통
        /// 호출할 필요가 없다.
        /// </summary>
        public void Bind(StickmanStateMachine machine, Transform anchor)
        {
            _machine = machine;
            if (anchor != null) _anchor = anchor;
        }

        private void Awake()
        {
            if (_agent == null) _agent = GetComponent<StickmanAgent>();
            if (_anchor == null) _anchor = transform.Find("Head");
            if (_anchor == null) _anchor = transform;
            if (_config == null && _agent != null) _config = _agent.Config;
            // 같은 GameObject의 이모트만 본다 — 씬 전체 탐색을 쓰면 남의 이모트를 보고
            // 플레이어 말풍선이 올라가는 사고가 난다(_requireBoundSpeaker와 같은 취지의 화자 분리).
            _hardware = GetComponent<HardwareReactionRenderer>();
            BuildUi();
            HideImmediateInternal(logReason: null);
        }

        private void Start()
        {
            _camera = ResolveCamera();
            if (_machine == null && _agent != null && _agent.Blackboard != null) _machine = _agent.Blackboard.Machine;
            if (_config == null && _agent != null) _config = _agent.Config;
            string speaker = _machine != null ? "지정됨"
                : (_requireBoundSpeaker ? "미지정(바인딩 전까지 아무것도 그리지 않음)" : "미지정(모든 대사 수신)");
            Debug.Log("[말풍선] 렌더러 준비 완료 — 화자=" + speaker +
                      $", 폰트='{(_label != null && _label.font != null ? _label.font.name : "없음")}'" +
                      $", 한글렌더={( _koreanGlyphVerified ? "실측 확인" : "미확인(폴백 폰트)")}.");
        }

        private void OnEnable()
        {
            // 구독 순서가 계약의 일부다 — 이 렌더러가 StateTransitioned를 **DialogueIntent보다 먼저**
            // 받아야 같은 프레임의 IsForcedInterrupt를 들고 DialogueExpired를 처리할 수 있다
            // (클래스 문서 "이벤트 순서에 대한 근거"). OnEnable은 어떤 상태 전이보다도 앞선다.
            StickmanEventBus.StateTransitioned += OnStateTransitioned;
            StickmanEventBus.DialogueRequested += OnDialogueRequested;
            StickmanEventBus.DialogueExpired += OnDialogueExpired;
        }

        private void OnDestroy()
        {
            // 캔버스가 씬 루트에 있으므로(BuildUi 참고) 이 컴포넌트가 사라질 때 직접 정리해야 고아가
            // 남지 않는다 — 캐릭터가 파괴되는데 말풍선 캔버스만 화면에 남는 사고 방지.
            if (_canvas != null) Destroy(_canvas.gameObject);
            _canvas = null;
            // 타원 스프라이트/텍스처는 HideFlags.HideAndDontSave라 자동 회수 대상이 아니다 —
            // 여기서 명시적으로 지우지 않으면 캐릭터가 파괴될 때마다 텍스처가 누수된다
            // (두 번째 화자는 씬 수명 중에 만들어지고 사라질 수 있다).
            DestroyEllipseSprites();
        }

        private void OnDisable()
        {
            // 정적 이벤트가 파괴된 인스턴스를 붙들지 않도록 반드시 해제(StickmanEventBus 클래스 문서 3).
            StickmanEventBus.StateTransitioned -= OnStateTransitioned;
            StickmanEventBus.DialogueRequested -= OnDialogueRequested;
            StickmanEventBus.DialogueExpired -= OnDialogueExpired;
            HideImmediateInternal(logReason: null);
        }

        // ==================== 이벤트 처리 (UX 계약의 본체) ====================

        private void OnStateTransitioned(StateTransitionEvent evt)
        {
            _lastTransitionForced = evt.IsForcedInterrupt;
            if (!evt.IsForcedInterrupt) return;
            _forcedInterruptFrame = Time.frameCount;

            // 이미 페이드아웃 중이던(=정상 종료된 옛 상태의) 말풍선도 강제 인터럽트가 오면 즉시 지운다.
            // 규칙 4 "취소가 항상 이긴다"를 페이드 잔상에까지 확장한 것 — 널브러지는 캐릭터 옆에
            // 옛 대사가 반투명으로 남아 있는 순간을 0으로 만든다. 이 시점에는 새 상태의 Enter()가
            // 이미 끝나 있으므로, 새 대사가 있었다면 _fadingOut은 false로 초기화된 뒤다(교체 우선).
            if (_fadingOut)
            {
                LastImmediateRemovalFrame = Time.frameCount;
                ImmediateRemovalCount++;
                HideImmediateInternal("강제 인터럽트 — 페이드아웃 잔상 즉시 제거");
            }
        }

        private void OnDialogueRequested(DialogueIntent intent)
        {
            if (intent == null) return;
            if (!IsMine(intent)) return;
            if (!StickMate.Core.AppSettingsModel.ResolveDialogueBubbleEnabled(_config)) return;

            // ★★ 2026-09-02 — 교체 경로의 발화 자격(DialogueBudget.CanReplaceVisible 문서에 실기 로그).
            //   여기가 이 결함이 살아 있던 자리다: 최소 노출 보호는 **만료** 경로에만 있었고 이
            //   **교체** 경로에는 한 줄도 없어서, 방금 뜬 글자가 다음 프레임에 지워질 수 있었다
            //   (실측 0.02초 = 1.2프레임 번쩍임 2연속).
            //   ★ 정책은 여기서 고르지 않는다 — 렌더러는 "지금 몇 초 떠 있었나"라는 **사실만** 주고
            //     판정은 플랫폼/표현 중립 위치(DialogueBudget)가 한다.
            if (IsBubbleVisible && !_fadingOut && _active != null)
            {
                float activeVisible = Time.unscaledTime - _shownAtUnscaledTime;
                bool replacesItself = intent.Text == _activeText
                                      && intent.StateId == _active.StateId
                                      && intent.Kind == _activeKind;
                if (!DialogueBudget.CanReplaceVisible(activeVisible, DialogueTiming.PopInSeconds, replacesItself))
                {
                    // 침묵이 정상 결과일수록 로그가 유일한 계기판이다 — 규칙 8의 "발화 보류"와 같은 어법.
                    Debug.Log($"[말풍선] 발화 보류 ({intent.StateId}) \"{intent.Text}\" — " +
                        (replacesItself
                            ? "지금 떠 있는 것과 같은 글자다(같은 상태·같은 종류). 교체하면 노출 시계와 " +
                              "팝인이 리셋돼 같은 글자가 다시 튀어오른다(렌더 글리치)."
                            : $"이전 \"{_activeText}\"(종류={KindLabel(_activeKind)})가 아직 팝인 중이다 — " +
                              $"노출 {activeVisible:F2}초 < 팝인 {DialogueTiming.PopInSeconds:F2}초.") +
                        $" 규칙 5는 유지된다(큐에 쌓지 않고 버린다). frame={Time.frameCount}");
                    return;
                }
            }

            // ★ 2026-09-01 — 교체 경로의 제거 로그(MOTION_SPEC 3-6에서 발견된 결함).
            //   지금까지 이 경로에는 "[말풍선] 제거"가 없어서, 표시 37건 중 34건만 제거 로그가 남고
            //   3건이 **조용히 사라졌다**. 화면을 볼 수 없는 검증 환경에서 그 3건은 "노출 시간 미상"이
            //   되어 3-7절의 계약 검증(서술 대사가 자기 상태보다 오래 남았는가)을 통째로 무력화한다.
            //   표시/제거가 항상 쌍으로 찍히게 만드는 것이 이 블록의 전부다.
            if (IsBubbleVisible && !string.IsNullOrEmpty(_activeText))
            {
                Debug.Log($"[말풍선] 교체 — 이전 \"{_activeText}\"" +
                          $"({(_active != null ? _active.StateId.ToString() : "페이드아웃중")}, " +
                          $"종류={KindLabel(_activeKind)}) 노출 {(Time.unscaledTime - _shownAtUnscaledTime):F2}초, " +
                          $"새 \"{intent.Text}\"({intent.StateId}, 종류={KindLabel(intent.Kind)}), " +
                          $"컷사유=교체(규칙 5 큐잉 금지), frame={Time.frameCount}");
            }

            // 규칙 5(큐잉 금지): 이전 말풍선은 즉시 교체된다 — 다음 대사를 모아두는 큐가 없다.
            _active = intent;
            _activeKind = intent.Kind;
            _activeText = intent.Text;
            _expiredPendingFadeOut = false;
            _fadingOut = false;
            _shownAtUnscaledTime = Time.unscaledTime;
            _alpha = 0f;

            _snapEmoteLift = true; // 첫 프레임부터 이모트 위에 자리 잡는다(미끄러져 올라오지 않는다).

            // ★ 진행 방향의 **반대쪽**을 이 대사의 수명 동안 고정한다(_latchedTextSide 문서 참고).
            //   ResolveFacingSign()은 방향을 알 수 없을 때 직전 판단을 그대로 유지하므로, 정지(Idle)
            //   중이라고 해서 글자가 가운데로 튀어나오는 일이 없다.
            _latchedTextSide = -ResolveFacingSign();
            _snapSideBlend = true;
            _popElapsed = 0f;
            // 기울기 **크기**만 여기서 대사별로 확정한다. 부호는 매 프레임 "실제로 놓인 쪽"에서
            // 파생되므로(UpdateComicTextPlacement) 화면 끝에서 쪽이 뒤집히면 기울기도 함께 뒤집힌다.
            _tiltMagnitudeDegrees = ComicTiltMagnitudeFor(_activeText, ResolveGlyphPixelSize(), ResolveTiltDegrees());
            _tiltDegrees = -_latchedTextSide * _tiltMagnitudeDegrees;
            _steadyScale = ComicEmphasisFor(_activeText);

            RefreshColors(); // 잉크색 프리셋(Ctrl+Opt+Cmd+C)이 런타임에 바뀌어도 다음 대사부터 즉시 반영.
            ApplyText(_activeText);
            if (_canvas != null) _canvas.gameObject.SetActive(true);
            UpdatePlacement();
            ApplyAlpha(0f);

            // 배치 정보를 함께 남긴다 — 화면을 볼 수 없는 검증 환경에서도 "어느 쪽에 놓였는지"를
            // 실행 로그만으로 재구성할 수 있어야 한다(이 프로젝트의 표시/제거 로그 쌍과 같은 취지).
            Debug.Log($"[말풍선] 표시 ({intent.StateId}) \"{_activeText}\" — " +
                      $"종류={KindLabel(intent.Kind)}, 가독예산={DialogueBudget.ReadingSeconds(_activeText):F2}초, " +
                      $"노출상한={ResolveMaxVisibleSeconds():F2}초, " +
                      $"진행방향={(_latchedTextSide < 0f ? "오른쪽" : "왼쪽")}, " +
                      $"글자쪽={(_latchedTextSide < 0f ? "왼쪽위" : "오른쪽위")}, " +
                      $"글자크기={ResolveFontSize()}pt, 외곽선={ScaledTextOutline:F2}pt, " +
                      $"기울기={_tiltDegrees:F1}도({(_tiltMagnitudeDegrees <= 0f ? "꺼짐" : _tiltDegrees > 0f ? "반시계" : "시계")}), " +
                      $"frame={Time.frameCount}");
        }

        private void OnDialogueExpired(DialogueIntent intent)
        {
            if (intent == null || intent != _active) return; // 이미 새 대사로 교체된 옛 대사는 무시.

            bool forcedNow = _lastTransitionForced && _forcedInterruptFrame == Time.frameCount;
            if (forcedNow)
            {
                // ★ 규칙 3(b)/규칙 4 — 강제 인터럽트는 최소 노출 시간을 무시하고 항상 이긴다.
                //   페이드아웃을 기다리지 않고 이 호출 스택 안에서 동기적으로 지운다 = 같은 프레임 제거.
                LastImmediateRemovalFrame = Time.frameCount;
                ImmediateRemovalCount++;
                HideImmediateInternal($"강제 인터럽트 즉시 제거 ({intent.StateId})");
                return;
            }

            // ★ 규칙 4-c ③ (2026-09-01 신설) — Narrative(진행 서술)는 상태가 끝나는 순간 문장 자체가
            //   거짓이 된다("걷기 시작한 캐릭터 위의 '잠깐 쉬는 중'"). 그러므로 가독예산 잔여와 무관하게
            //   **그 프레임에 페이드아웃을 개시**한다. 이 대사가 애초에 화면에 뜬 것은 규칙 8(발화 자격
            //   게이트)이 "계획 잔여 체류 >= 페이드인 + 가독예산"을 이미 확인했기 때문이므로, 여기서
            //   잘리는 것은 **계획이 외부 사건으로 깨진 소수 경우**뿐이다.
            if (intent.Kind == DialogueKind.Narrative)
            {
                Debug.Log($"[말풍선] 즉시 컷 ({intent.StateId}) \"{_activeText}\" — 종류=서술, " +
                          $"컷사유=상태종료, 노출 {(Time.unscaledTime - _shownAtUnscaledTime):F2}초, " +
                          $"frame={Time.frameCount}");
                BeginFadeOut();
                return;
            }

            // 규칙 4-c ④ — Reaction(순간 반응)은 상태가 끝나도 여전히 참이므로 가독예산을 채운 뒤
            // 페이드아웃한다(Tick에서 처리).
            _expiredPendingFadeOut = true;
        }

        /// <summary>로그용 종류 라벨. 검증이 로그만으로 "서술이 자기 상태보다 오래 남았는가"를 셀 수
        /// 있으려면 표시/교체/제거 로그 전부에 이 축이 찍혀야 한다(MOTION_SPEC 3-6/3-7).</summary>
        private static string KindLabel(DialogueKind kind)
            => kind == DialogueKind.Narrative ? "서술" : "반응";

        /// <summary>이 대사가 내가 담당하는 화자의 것인지(규칙 7 다중 캐릭터 분리).</summary>
        private bool IsMine(DialogueIntent intent)
        {
            if (_machine == null) return !_requireBoundSpeaker; // 화자 미지정 = 단일 캐릭터 폴백(플래그로 차단 가능).
            return intent.OriginMachine == _machine;
        }

        // ==================== 프레임 갱신 ====================

        private void LateUpdate()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.Dialogue);   // [스톨구간] 계측
            // 전체화면 게임 감지로 캐릭터가 숨겨졌으면 말풍선도 함께 사라져야 한다(비침해 원칙 2).
            if (_agent != null && _agent.IsSuspended && IsBubbleVisible)
            {
                HideImmediateInternal("전체화면 감지(Suspended)");
                return;
            }
            if (!IsBubbleVisible) return;

            // 배율은 실행 중에 바뀔 수 있다(창을 다른 배율의 모니터로 옮기거나, 시작 직후
            // MacOverlayStateEnforcer가 창을 화면 전체로 넓히는 시점). 보이는 동안만 매 프레임 추종한다.
            ApplyCanvasScaleFactor();

            float dt = Time.unscaledDeltaTime;
            float elapsed = Time.unscaledTime - _shownAtUnscaledTime;
            _popElapsed += dt; // 팝인(등장 스케일 바운스) 진행 — PopScale() 참고.

            if (!_fadingOut)
            {
                float maxVisible = ResolveMaxVisibleSeconds();
                // ★ 규칙 4-b(2026-09-01) — 고정 하한 0.7초가 **글자수 비례 가독예산**으로 대체됐다.
                //   구판은 "음..."(4자)과 "창 위는 미끄러워"(9자)를 같은 시간 띄웠다. 가독성 하한이라면서
                //   실제로는 가독성을 보장하지 못한 값이다. dialogueMinVisibleSeconds는 그 위에 얹는
                //   **추가 절대 하한**으로 남는다(기본 0 = 예산에 전부 맡김).
                // ★ 2026-09-02 — 사용자 노출 배율(대사 표시 시간 3단)은 <b>여기(화면 노출)</b>에만 곱한다.
                //   발화 자격 게이트에는 곱하지 않는다(DialogueBudget.RequiredDwellSeconds 문서).
                float minVisible = Mathf.Max(
                    DialogueBudget.MinVisibleSecondsFor(_activeText, ResolveVisibleScale()),
                    StickMate.Core.AppSettingsModel.ResolveDialogueMinVisibleSeconds(_config));
                if (maxVisible > 0f) minVisible = Mathf.Min(minVisible, maxVisible);

                // 정상 종료 대기분(반응 대사만 여기에 온다 — 서술은 만료 즉시 컷됐다): 가독예산을
                // 채우면 페이드아웃 시작.
                if (_expiredPendingFadeOut && elapsed >= minVisible) BeginFadeOut();
                // 상한: 상태가 아주 오래 지속돼도(예: Idle 6초) 말풍선이 화면에 눌러앉지 않게 한다.
                else if (!_expiredPendingFadeOut && maxVisible > 0f && elapsed >= maxVisible) BeginFadeOut();
            }

            if (_fadingOut)
            {
                _alpha -= dt / Mathf.Max(0.01f, FadeOutSeconds);
                if (_alpha <= 0f)
                {
                    // 정상 종료 경로의 제거도 로그로 남긴다 — 표시/제거가 항상 쌍으로 찍혀야
                    // "말풍선이 언제 사라졌는지"를 실행 로그만으로 재구성할 수 있다(빈도는 표시와
                    // 같으므로 로그가 늘어나는 양도 표시 로그와 동일하다).
                    HideImmediateInternal($"정상 종료 페이드아웃 완료, 종류={KindLabel(_activeKind)}, " +
                        $"노출 {(Time.unscaledTime - _shownAtUnscaledTime):F2}초");
                    return;
                }
            }
            else if (_alpha < 1f)
            {
                _alpha = Mathf.Min(1f, _alpha + dt / Mathf.Max(0.01f, FadeInSeconds));
            }

            UpdatePlacement();
            ApplyAlpha(_alpha);
        }

        /// <summary>
        /// ★ 지금 떠 있는 대사의 **노출 상한**(초). 표시 로그와 LateUpdate가 반드시 같은 값을 보게
        /// 한 곳에 모은다 — 로그가 실제 판정과 다른 숫자를 찍으면 검증이 통째로 거짓말이 된다.
        ///
        /// 상한은 <b>둘</b>이고 둘 다 상한이므로 짧은 쪽(교집합)을 쓴다:
        /// <list type="number">
        ///   <item><b>가독예산 기반</b>(<see cref="DialogueBudget.MaxVisibleSecondsFor"/>) — 2026-09-01
        ///     규칙 4-b 개정의 나머지 절반. 구판은 하한만 글자수 비례로 바꾸고 상한을 글자수 무관
        ///     고정 4초로 남겨, 실측에서 결과가 정확히 뒤집혔다("하암...(5자)" 4.14초 vs
        ///     "창 위는 미끄러워(9자)" 1.45초 — <b>짧은 대사가 더 오래</b> 떠 있었다).</item>
        ///   <item><b>사용자 설정</b>(<c>dialogueMaxVisibleSeconds</c>) — 0 이하면 "상한 없음"이라
        ///     예산 쪽만 남는다.</item>
        /// </list>
        /// 짧은 쪽을 고르는 방향(더 일찍 사라짐)은 계약이 막는 실패 모드("행동보다 텍스트가 오래
        /// 남음")의 <b>반대편</b>이라 언제나 안전하다.
        /// </summary>
        private float ResolveMaxVisibleSeconds()
        {
            float budgetMax = DialogueBudget.MaxVisibleSecondsFor(_activeText,
                DialogueTiming.PopInSeconds, DialogueTiming.FadeOutSeconds, ResolveVisibleScale());
            float settingMax = StickMate.Core.AppSettingsModel.ResolveDialogueMaxVisibleSeconds(_config);
            return settingMax > 0f ? Mathf.Min(settingMax, budgetMax) : budgetMax;
        }

        /// <summary>사용자가 고른 노출 배율(대사 표시 시간 3단). 기본 100%에서는 배율 도입 이전과
        /// 화면 결과가 <b>한 톨도 다르지 않다</b>.</summary>
        private static float ResolveVisibleScale()
            => StickMate.Core.AppSettingsModel.ResolveDialogueVisibleScale();

        private void BeginFadeOut()
        {
            if (_fadingOut) return;
            _fadingOut = true;
            _active = null; // 더 이상 새 만료 이벤트의 대상이 아니다(중복 처리 방지).
        }

        /// <summary>사라지는 애니메이션 없이 이 자리에서 즉시 제거(규칙 3(b)).</summary>
        private void HideImmediateInternal(string logReason)
        {
            _active = null;
            _expiredPendingFadeOut = false;
            _fadingOut = false;
            _alpha = 0f;
            ApplyAlpha(0f);
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            if (logReason != null) Debug.Log($"[말풍선] 제거 — {logReason}, frame={Time.frameCount}");
            _activeText = null;
        }

        /// <summary>외부(테스트/긴급정지)에서 즉시 제거를 요청하는 공개 진입점.</summary>
        public void HideImmediate() => HideImmediateInternal("외부 요청");

        // ==================== 배치 (규칙 6) ====================

        /// <summary>
        /// ScreenSpaceOverlay 캔버스의 스케일을 현재 화면 배율에 맞춘다 — **캔버스 1유닛 == OS 포인트 1**.
        ///
        /// 왜 필요한가(2026-08-29 Retina 대응 라운드, 리더 지시 5항): `macRetinaSupport`를 켜면
        /// Screen.width/height가 물리 백킹 픽셀(3024x1964)이 되고, scaleFactor가 1인 캔버스는 그 픽셀을
        /// 그대로 자기 좌표로 쓴다 — 즉 이 파일의 모든 상수(fontSize 16, TextPadding, MaxTextWidth 220 …
        /// 전부 "포인트 기준으로 눈으로 맞춘 값")가 **물리적으로 절반 크기**가 된다. scaleFactor를 1/dpi
        /// (Retina면 2)로 두면 물리적 크기는 Retina 이전과 정확히 같고 렌더 해상도만 2배가 된다.
        /// 즉 "같은 크기, 두 배 선명" — 가독성 하한 문제가 애초에 발생하지 않는 형태다.
        /// </summary>
        private void ApplyCanvasScaleFactor()
        {
            if (_scaler == null) return;
            float target = ScreenCoordinateConverter.ResolveCanvasScaleFactor(_config);
            if (!Mathf.Approximately(_scaler.scaleFactor, target)) _scaler.scaleFactor = target;
        }

        private void UpdatePlacement()
        {
            if (_panel == null) return;
            if (_camera == null) _camera = ResolveCamera();
            if (_camera == null || _anchor == null) return;

            // 기준점 = 머리 바로 위(월드 오프셋이라 줌/해상도가 바뀌어도 자동 추종).
            // 말풍선 모드에서는 꼬리 끝이 가리키는 지점이고, 텍스트 모드에서는 대각선 오프셋의 원점이다.
            // 하드웨어 반응 이모트가 떠 있으면 그 위로 비켜 선다(아래 TickEmoteLift 문서 참고).
            Vector3 tipWorld = _anchor.position + Vector3.up * (HeadTopWorldOffset + TickEmoteLift());
            Vector3 tipScreen = _camera.WorldToScreenPoint(tipWorld);
            if (tipScreen.z < 0f) return; // 카메라 뒤 — 배치 불가(직교 카메라에서는 사실상 발생하지 않음).

            // ★ 단위 변환(2026-08-29 Retina 대응): WorldToScreenPoint/Screen.width는 **Unity 픽셀**인데,
            // 아래 anchoredPosition/sizeDelta는 **캔버스 유닛**이다. Retina에서 CanvasScaler.scaleFactor가
            // 2가 되면서 둘이 정확히 2배 어긋나므로, 배치 계산에 들어가기 전에 캔버스 유닛으로 환산한다
            // (환산 후 캔버스 1유닛 == OS 포인트 1이라, 아래의 모든 상수는 예전 의미 그대로다).
            // 이 변환을 빼먹으면 말풍선이 화면 우상단 밖으로 날아간다.
            Vector2 tip = new Vector2(
                ScreenCoordinateConverter.UnityScreenToCanvas(tipScreen.x, _config),
                ScreenCoordinateConverter.UnityScreenToCanvas(tipScreen.y, _config));

            Vector2 panelSize = _panel.sizeDelta;
            float screenW = ScreenCoordinateConverter.UnityScreenToCanvas(Screen.width, _config);
            float screenH = ScreenCoordinateConverter.UnityScreenToCanvas(Screen.height, _config);

            LastTextAnchorCanvas = tip;

            if (DrawBubbleShapes) UpdateBubblePlacement(tip, panelSize, screenW, screenH);
            else UpdateComicTextPlacement(tipWorld, tip, panelSize, screenW, screenH);
        }

        // ============================================================================
        // ★ 만화 레터링 배치 — "진행 방향의 반대쪽 대각선 상단"
        // ============================================================================
        // 의도(사용자 요구 원문 "캐릭터 걸어가는방향 반대쪽 대각선 상단"): 진행 방향 **앞**을 글자가
        // 가리지 않게 뒤로 흘린다. 오른쪽으로 걸으면 글자는 왼쪽 위, 왼쪽으로 걸으면 오른쪽 위.
        //
        // 간격을 **월드 유닛으로 잡고 그 다음에 화면 좌표로 환산**하는 이유: 캔버스 유닛으로 직접
        // 잡으면 캐릭터 배율(characterScale)과 카메라 줌 어느 쪽도 따라가지 못한다. 기준점과
        // "기준점 + (gapX, gapY)" 두 월드 점을 각각 투영해 그 차이를 쓰면 두 배율이 모두 자동으로
        // 반영된다(투영 방식이 무엇이든 성립한다).
        private void UpdateComicTextPlacement(Vector3 tipWorld, Vector2 tip, Vector2 panelSize,
            float screenW, float screenH)
        {
            Vector3 gapWorld = tipWorld + new Vector3(TextGapWorldX, TextGapWorldY, 0f);
            Vector3 gapScreen = _camera.WorldToScreenPoint(gapWorld);
            float gapX = Mathf.Abs(ScreenCoordinateConverter.UnityScreenToCanvas(gapScreen.x, _config) - tip.x);
            float gapY = Mathf.Abs(ScreenCoordinateConverter.UnityScreenToCanvas(gapScreen.y, _config) - tip.y);

            // 클램프에는 정상 상태 배율만 반영한다 — 팝인 오버슈트(최대 1.12배)는 150ms짜리 순간이고,
            // 그것까지 넣으면 등장할 때마다 글자가 화면 안쪽으로 한 번 밀렸다가 제자리로 돌아온다.
            // ★ 기울인 뒤에는 **회전한 블록의 축 정렬 경계**로 재야 화면 클램프가 성립한다.
            //   8도면 170pt 폭 블록의 세로 경계가 24pt 더 커지므로, 회전 전 크기로 클램프하면 화면
            //   위/옆에서 글자의 모서리만 잘려 나간다(가장자리에 붙어 있는 시간이 긴 캐릭터다).
            //   이 크기는 기울기의 **부호와 무관**하다(|cos|,|sin|) — 쪽이 뒤집혀도 같은 값이라
            //   뒤집기 보간 중에 블록 크기가 흔들리지 않는다.
            Vector2 size = RotatedBounds(panelSize * Mathf.Max(0.01f, _steadyScale), _tiltMagnitudeDegrees);

            ComicTextPlacement placement = ComputeComicTextPlacement(
                tip, size, _latchedTextSide, gapX, gapY, screenW, screenH, ScreenEdgeMargin);

            // 뒤집기(화면 끝)는 순간이동이 아니라 좌우로 미끄러진다 — 새 대사의 첫 프레임에만 스냅한다.
            if (_snapSideBlend)
            {
                _snapSideBlend = false;
                _sideBlend = placement.SideSign;
            }
            else
            {
                _sideBlend = Mathf.MoveTowards(_sideBlend, placement.SideSign,
                    Time.unscaledDeltaTime * SideFlipSpeed);
            }

            // ★ 기울기의 부호는 **놓인 쪽의 거울상**이다(위 "글자 기울기" 블록 참고).
            //   연속값 _sideBlend에서 파생하므로 쪽이 뒤집히는 동안 각도도 함께 미끄러진다 —
            //   부호가 0을 지날 때 글자가 잠깐 수평이 됐다가 반대로 기운다. 목표 각도를 바로 대입하면
            //   머리 위를 가로지르는 동안 글자만 홱 뒤집혀 배치와 따로 논다.
            _tiltDegrees = -_sideBlend * _tiltMagnitudeDegrees;

            // 보간 중에는 연속값 _sideBlend로 X를 다시 잡는다(부호가 0을 지나며 머리 위를 가로지른다).
            float half = size.x * 0.5f;
            float centerX = tip.x + _sideBlend * (gapX + half);
            float minX = ScreenEdgeMargin + half;
            float maxX = Mathf.Max(minX, screenW - ScreenEdgeMargin - half);
            centerX = Mathf.Clamp(centerX, minX, maxX);

            var center = new Vector2(centerX, placement.Center.y);
            _panel.anchoredPosition = center;
            _panel.localScale = Vector3.one * (_steadyScale * PopScale());
            _panel.localRotation = Quaternion.Euler(0f, 0f, _tiltDegrees);

            LastTextCenterCanvas = center;
            LastTextSideSign = placement.SideSign;
            LastTextSizeCanvas = size;
            LastTextTiltDegrees = _tiltDegrees;
        }

        /// <summary>글자 블록 배치 계산 결과(<see cref="ComputeComicTextPlacement"/>).</summary>
        public readonly struct ComicTextPlacement
        {
            public ComicTextPlacement(Vector2 center, float sideSign, bool flippedByScreenEdge)
            {
                Center = center;
                SideSign = sideSign;
                FlippedByScreenEdge = flippedByScreenEdge;
            }

            /// <summary>글자 블록 중심(캔버스 유닛).</summary>
            public Vector2 Center { get; }

            /// <summary>실제로 놓인 쪽(+1 캐릭터 오른쪽 / -1 왼쪽).</summary>
            public float SideSign { get; }

            /// <summary>화면 밖으로 잘릴 상황이라 선호 쪽에서 반대로 뒤집혔는가.</summary>
            public bool FlippedByScreenEdge { get; }
        }

        /// <summary>
        /// 글자 블록을 놓을 자리를 구한다 — **순수 함수**라 카메라/씬 없이 그대로 테스트할 수 있다
        /// (PlayMode의 DialogueComicTextPlacementTests가 이 함수를 직접 호출해 계약을 잠근다).
        ///
        /// 규칙:
        ///   1) 선호 쪽(<paramref name="preferredSideSign"/> = 진행 방향의 반대)의 대각선 위에 놓는다.
        ///      기준점에서 가로로 gapX + 반폭, 세로로 gapY + 반높이 떨어진 자리 = "대각선 상단".
        ///   2) 그 자리에서 화면 좌우로 잘리면 **반대쪽으로 뒤집는다**. 안쪽으로 밀어 넣지 않는 이유:
        ///      밀면 글자가 캐릭터 머리 위로 올라타 "반대쪽 대각선"이라는 요구 자체가 깨진다
        ///      (캐릭터는 화면 좌우 끝에 서 있는 시간이 길다 — 벽타기/가장자리 회전).
        ///   3) 양쪽 다 안 되는 극단(글자가 화면보다 넓음)에서는 최소한 잘리지 않게 안쪽으로 민다.
        ///   4) 세로는 창 상단 테두리에서 잘리지 않게 클램프한다(규칙 6 "잘리지 않게").
        /// </summary>
        public static ComicTextPlacement ComputeComicTextPlacement(
            Vector2 tipCanvas, Vector2 textSize, float preferredSideSign,
            float gapX, float gapY, float screenW, float screenH, float margin)
        {
            float side = preferredSideSign >= 0f ? 1f : -1f;
            float half = textSize.x * 0.5f;

            float centerX = tipCanvas.x + side * (gapX + half);
            bool flipped = false;
            if (centerX + half > screenW - margin || centerX - half < margin)
            {
                float mirrored = tipCanvas.x - side * (gapX + half);
                if (mirrored + half <= screenW - margin && mirrored - half >= margin)
                {
                    centerX = mirrored;
                    side = -side;
                    flipped = true;
                }
            }

            float minX = margin + half;
            float maxX = Mathf.Max(minX, screenW - margin - half);
            centerX = Mathf.Clamp(centerX, minX, maxX);

            float halfH = textSize.y * 0.5f;
            float centerY = tipCanvas.y + gapY + halfH;
            float minY = margin + halfH;
            float maxY = Mathf.Max(minY, screenH - margin - halfH);
            centerY = Mathf.Clamp(centerY, minY, maxY);

            return new ComicTextPlacement(new Vector2(centerX, centerY), side, flipped);
        }

        /// <summary>
        /// 지금 화자가 바라보는 방향(+1 오른쪽 / -1 왼쪽).
        /// 방향을 알 수 없으면 **직전 판단을 그대로 유지한다** — 0을 돌려주면 정지(Idle) 중에 글자가
        /// 가운데로 튀어나온다(사용자 요구 "방향이 없다고 가운데로 튀면 안 된다").
        /// </summary>
        private float ResolveFacingSign()
        {
            if (FacingSource != null)
            {
                float injected = FacingSource();
                if (Mathf.Abs(injected) > 0.001f) return Mathf.Sign(injected);
            }
            if (_agent != null && _agent.Blackboard != null)
            {
                float facing = _agent.Blackboard.FacingSign;
                if (Mathf.Abs(facing) > 0.001f) return Mathf.Sign(facing);
            }
            return -_latchedTextSide; // 직전에 쓰던 쪽의 반대 = 직전에 알고 있던 진행 방향.
        }

        /// <summary>
        /// 팝인(툭 튀어나오는 등장) 배율. 0 -> <see cref="PopInStartScale"/>,
        /// <see cref="PopInPeakAt"/> -> <see cref="PopInOvershoot"/>, 1 -> 1.0의 2구간 SmoothStep.
        /// 페이드보다 만화답게 보이라는 리더 지시에 대한 구현이며, 알파 페이드(150ms)는 그대로 둔다.
        /// </summary>
        private float PopScale()
        {
            if (_popElapsed >= PopInSeconds) return 1f;
            float t = Mathf.Clamp01(_popElapsed / Mathf.Max(0.01f, PopInSeconds));
            return t < PopInPeakAt
                ? Mathf.Lerp(PopInStartScale, PopInOvershoot, Mathf.SmoothStep(0f, 1f, t / PopInPeakAt))
                : Mathf.Lerp(PopInOvershoot, 1f, Mathf.SmoothStep(0f, 1f, (t - PopInPeakAt) / (1f - PopInPeakAt)));
        }

        /// <summary>
        /// 이 대사의 기울기 **크기**(도, 항상 0 이상). 부호는 여기서 정하지 않는다 — 부호는 글자가
        /// 실제로 놓인 쪽에서 파생되고(UpdateComicTextPlacement), 이 함수는 "얼마나"만 답한다.
        ///
        /// 크기는 기준값에 대사 문자열의 **결정적 해시**로 뽑은 ±<see cref="ComicTiltJitterRatio"/>
        /// 편차를 곱해 만든다. 같은 대사는 항상 같은 각도이므로 프레임마다 다시 계산해도 글자가 떨리지
        /// 않는다(난수를 쓰지 않는 것은 이 프로젝트의 컨벤션이다 — Dialogue/AmbientChatter.cs
        /// "같은 입력이면 항상 같은 출력").
        /// </summary>
        private static float ComicTiltMagnitudeFor(string text, float glyphPixels, float baseDegrees)
        {
            if (string.IsNullOrEmpty(text) || baseDegrees <= 0f) return 0f;
            // ★ 아주 작은 글리프는 기울이지 않는다 (2026-08-29 실측으로 확인한 실패).
            //   기울이면 글자 쿼드가 픽셀 격자와 어긋나 글리프 아틀라스가 바이리니어로 다시 샘플링된다.
            //   32px 글리프에서는 눈에 띄지 않지만 12px 한글에서는 자모 획이 통째로 뭉개져 읽을 수
            //   없게 된다 — "대각선으로"보다 "읽힌다"가 먼저다.
            //   하한을 **물리 픽셀**로 재는 이유는 ComicTiltMinGlyphPixels 문서 참고.
            if (glyphPixels < ComicTiltMinGlyphPixels) return 0f;
            int hash = 17;
            for (int i = 0; i < text.Length; i++) hash = unchecked(hash * 31 + text[i]);
            float t = ((hash & 0x7fffffff) % 1000) / 999f;
            return baseDegrees * Mathf.Lerp(1f - ComicTiltJitterRatio, 1f + ComicTiltJitterRatio, t);
        }

        /// <summary>이 대사에 쓸 기울기 기준값(도). 설정 경유가 원칙이고, 설정이 없을 때만 기본값으로
        /// 떨어진다(에디터/테스트 리그). 음수는 "반대로 기울여라"가 아니라 설정 실수이므로 0으로 막는다
        /// — 방향은 배치 쪽에서 결정되는 값이지 사람이 설정으로 뒤집을 값이 아니다.</summary>
        private float ResolveTiltDegrees()
            => _config != null ? Mathf.Max(0f, _config.dialogueTiltDegrees) : DefaultComicTiltDegrees;

        /// <summary>uGUI가 글리프를 실제로 굽는 크기(**물리 픽셀**) = 글자 크기(캔버스 유닛) x 캔버스
        /// 배율. 회전 리샘플링 하한(<see cref="ComicTiltMinGlyphPixels"/>)이 이 단위로 걸린다.</summary>
        private float ResolveGlyphPixelSize()
            => ResolveFontSize() * Mathf.Max(0.01f, ScreenCoordinateConverter.ResolveCanvasScaleFactor(_config));

        /// <summary>기울인 사각형의 축 정렬 경계 크기. 기울기의 부호와 무관하다(|cos|, |sin|).</summary>
        private static Vector2 RotatedBounds(Vector2 size, float degrees)
        {
            float rad = Mathf.Abs(degrees) * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad);
            float s = Mathf.Sin(rad);
            return new Vector2(size.x * c + size.y * s, size.x * s + size.y * c);
        }

        /// <summary>감탄사 강조 배율 — 느낌표가 든 대사는 조금 더 크게 외친다(만화 문법).</summary>
        private static float ComicEmphasisFor(string text)
        {
            if (string.IsNullOrEmpty(text)) return 1f;
            return text.IndexOf('!') >= 0 || text.IndexOf('\uFF01') >= 0 ? ComicEmphasisScale : 1f;
        }

        /// <summary>
        /// 종전 말풍선(타원 몸통 + 꼬리) 배치. <see cref="DrawBubbleShapes"/>가 true일 때만 호출된다 —
        /// 지금은 만화 레터링 모드라 실행되지 않지만, 되돌리기 요구에 대비해 **한 줄도 바꾸지 않고**
        /// 그대로 보존한다(플래그 하나로 종전 그림이 그대로 복원된다).
        /// </summary>
        private void UpdateBubblePlacement(Vector2 tip, Vector2 panelSize, float screenW, float screenH)
        {
            // 박스는 꼬리 위에 놓인다. 화면 위/아래로 넘치면 안쪽으로 민다.
            float scaledTailHeight = ScaledTailHeight;
            float scaledTailWidth = ScaledTailWidth;
            float scaledOverlap = ScaledTailPanelOverlap;
            float panelBottom = tip.y + scaledTailHeight - scaledOverlap;
            panelBottom = Mathf.Min(panelBottom, screenH - ScreenEdgeMargin - panelSize.y);
            panelBottom = Mathf.Max(panelBottom, ScreenEdgeMargin);

            // 규칙 6: "꼬리 방향을 유지한 채 몸통만 안쪽으로" — 몸통 x를 화면 안으로 클램프하고,
            // 꼬리는 캐릭터 x를 그대로 따라가되 몸통 폭 안에 머물게만 한다.
            float a = panelSize.x * 0.5f;   // 타원 가로 반지름
            float b = panelSize.y * 0.5f;   // 타원 세로 반지름
            float panelCenterX = Mathf.Clamp(tip.x, ScreenEdgeMargin + a, screenW - ScreenEdgeMargin - a);

            // ★ 타원 전환(2026-08-29): 사각형이면 아래 변이 평평해 꼬리를 어디에 붙여도 같은 높이였지만,
            // 타원은 중앙에서 멀어질수록 아래 경계가 위로 휘어 올라간다. 그대로 두면 꼬리가 몸통에서
            // 떨어져 허공에 뜬다. 그래서 (1) 꼬리가 붙을 수 있는 좌우 범위를 타원 기준으로 제한하고,
            // (2) 꼬리 **바깥 모서리**에서의 타원 아래 경계 높이를 구해 그 위에 꼬리 윗변을 얹는다
            // (바깥 모서리로 재야 꼬리 윗변 전체가 타원 안에 들어간다).
            float maxOffset = Mathf.Max(0f, a * TailEllipseSpanLimit - scaledTailWidth * 0.5f);
            float tailCenterX = Mathf.Clamp(tip.x, panelCenterX - maxOffset, panelCenterX + maxOffset);

            float outerDx = Mathf.Abs(tailCenterX - panelCenterX) + scaledTailWidth * 0.5f;
            float t = a > 0.01f ? Mathf.Clamp01(1f - (outerDx * outerDx) / (a * a)) : 0f;
            float ellipseBottomY = panelBottom + b - b * Mathf.Sqrt(t);

            _panel.anchoredPosition = new Vector2(panelCenterX, panelBottom);

            // 꼬리 윗변은 타원 경계보다 TailPanelOverlap만큼 위(=몸통 안쪽)에 두고, 아래 끝은 항상
            // 머리 바로 위(tip)를 정확히 가리키게 길이를 맞춘다.
            float tailTop = ellipseBottomY + scaledOverlap;
            float tailHeight = Mathf.Max(scaledTailHeight * 0.6f, tailTop - tip.y);
            var tailPos = new Vector2(tailCenterX, tailTop);
            var tailSize = new Vector2(scaledTailWidth, tailHeight);
            _tailOutline.anchoredPosition = tailPos;
            _tailOutline.sizeDelta = tailSize;
            _tailFill.anchoredPosition = tailPos;
            _tailFill.sizeDelta = tailSize;
        }

        // ============================================================================
        // ★ 하드웨어 반응 이모트와의 겹침 회피 (리더 좌표 확인, 2026-08-29 — 신고 4건째)
        // ============================================================================
        // 겹치는 이유: 머리 중심 앵커 ≈ 2.05, 정수리 ≈ 2.27인데 꼬리 끝은 앵커 + HeadTopWorldOffset(0.34)
        // = 2.39이고, 하드웨어 이모트 중심은 2.32다. 즉 이모트가 **정수리와 꼬리 끝 사이에 정확히 끼어**
        // 있어서, 반경까지 감안하면 꼬리와 패널 바닥을 그대로 관통했다. 게다가 하드웨어 반응은
        // SpectacleEventLock에 참여하지 않으므로(의도된 설계) 유휴 혼잣말과 언제든 동시에 뜬다.
        //
        // 해법: 이모트가 떠 있는 동안 꼬리 끝을 **이모트의 실제 상단 위로** 올린다. 상수로 계산하지 않고
        // HardwareReactionRenderer가 알려주는 실제 월드 y를 쓰는 이유는, 이모트가 화면 위 클램프에
        // 걸려 머리와 다른 높이에 있을 수 있고(그쪽 FollowHead 참고) 두 연출이 서로 다른 앵커
        // (말풍선=Head 트랜스폼 / 이모트=Body 위치)를 쓰기 때문이다 — 상수로 맞추면 포즈가 바뀔 때마다
        // 어긋난다.
        //
        // 이모트 쪽은 같은 시간 동안 가로로 비켜 준다(HardwareReactionRenderer.TickDialogueDodge).
        // 세로/가로를 나눠 각자 자기 좌표만 만지므로 서로의 배치 로직을 알 필요가 없다.
        //
        // 화면 위 잘림: 아래 UpdatePlacement의 기존 클램프
        //   panelBottom = Min(panelBottom, screenH - ScreenEdgeMargin - panelSize.y)
        // 가 올라간 몸통을 그대로 화면 안으로 되돌린다. 꼬리 길이는 그 클램프 결과에서 다시 계산되므로
        // (tailTop - tip.y) 끝점은 계속 머리를 가리킨다.

        /// <summary>이모트 상단과 꼬리 끝 사이에 남길 여유(전신 높이 대비 비율, 0.14 / 2.27).</summary>
        private const float EmoteClearanceRatio = 0.0617f;

        /// <summary>회피가 순간이동처럼 보이지 않게 하는 접근 속도(유닛/초).</summary>
        private const float EmoteLiftSpeed = 3.6f;

        private HardwareReactionRenderer _hardware;
        private float _emoteLift;
        private bool _snapEmoteLift;

        private float TickEmoteLift()
        {
            float desired = 0f;
            if (_hardware != null && _hardware.TryGetOccupiedTopWorldY(out float emoteTop))
            {
                desired = Mathf.Max(0f,
                    (emoteTop + CharacterHeight * EmoteClearanceRatio) - (_anchor.position.y + HeadTopWorldOffset));
            }

            if (_snapEmoteLift)
            {
                // 말풍선이 처음 뜨는 프레임에는 즉시 제자리로 — 아래에서 위로 미끄러져 올라오면
                // 그 사이 프레임 동안 이모트를 그대로 관통한다.
                _snapEmoteLift = false;
                _emoteLift = desired;
            }
            else
            {
                _emoteLift = Mathf.MoveTowards(_emoteLift, desired, Time.unscaledDeltaTime * EmoteLiftSpeed);
            }
            return _emoteLift;
        }

        private Camera ResolveCamera()
        {
            if (_agent != null && _agent.Blackboard != null && _agent.Blackboard.MainCamera != null)
                return _agent.Blackboard.MainCamera;
            return Camera.main;
        }

        private void ApplyAlpha(float a)
        {
            if (_group != null) _group.alpha = Mathf.Clamp01(a);
        }

        /// <summary>
        /// 지금 StickConfig에 설정된 잉크색/말풍선색을 네 개의 Image와 글자에 다시 입힌다.
        /// 캐릭터 선 색 프리셋 전환(Interaction/AppControlDirector.cs의 Ctrl+Opt+Cmd+C)은
        /// StickmanAgent.ApplyInkColorFromConfig()가 LineRenderer만 갱신하므로, 말풍선은 여기서
        /// 자기 몫을 따라간다 — 흰 캐릭터(어두운 배경)일 때 흰 말풍선에 흰 글씨가 되는 사고를 막는다.
        /// </summary>
        /// <summary>
        /// 말풍선 **안쪽 채움** 색. 기본은 알파 0(완전 투명)이고, 그때 말풍선은 캐릭터 머리와 똑같이
        /// "잉크 링만 있고 안은 비어 있는" 모습이 된다(2026-08-29 사용자 요구 "얼굴처럼 투명").
        ///
        /// 알파를 살려 두는 이유(리더 허용 범위): 아이콘/글자가 빽빽한 바탕화면 위에서는 배경이 완전히
        /// 비쳐 글자가 안 읽힐 수 있다. 그럴 때 StickConfig.dialogueBubbleColor의 **알파만** 0.1~0.2로
        /// 올려 아주 옅은 판을 깔 수 있게 남긴다 — 흰 배경을 통째로 되살리는 것과는 다르다.
        ///
        /// 잉크색 프리셋 연동 유지: 흰 캐릭터(어두운 배경) 프리셋에서는 옅은 채움도 검정 쪽이어야 한다.
        /// 단 **알파는 설정값을 그대로 보존**한다 — 예전 코드처럼 Color.black을 통째로 대입하면 알파가
        /// 1로 되살아나 "투명하게 해 달라"는 요구가 흰 캐릭터에서만 조용히 깨진다.
        /// </summary>
        private Color ResolveBubbleFillColor()
        {
            if (_config == null) return new Color(1f, 1f, 1f, 0f);
            Color fill = _config.dialogueBubbleColor;
            if (_config.IsWhiteInk()) fill = new Color(0f, 0f, 0f, fill.a);
            return fill;
        }

        /// <summary>
        /// 글자 **외곽선** 색 — 잉크색의 반대색. 흰 잉크(어두운 배경용 프리셋)면 검정, 검은 잉크면 흰색.
        ///
        /// ★ 여기서만 알파를 1로 고정하는 것이 의도다(<see cref="ResolveBubbleFillColor"/>의 "알파를
        /// 보존하라"와 반대). 말풍선 채움은 "있어도 되고 없어도 되는 옅은 판"이지만, 이 선은 글자와
        /// 바탕화면 사이의 **유일한 분리막**이라 옅게 만들면 존재 이유가 사라진다 — 검은 캐릭터가
        /// 어두운 바탕화면 위에서, 흰 캐릭터가 밝은 바탕화면 위에서 글자를 잃는 바로 그 실패다.
        /// </summary>
        private Color ResolveTextOutlineColor()
        {
            bool whiteInk = _config != null && _config.IsWhiteInk();
            return whiteInk ? new Color(0f, 0f, 0f, 1f) : new Color(1f, 1f, 1f, 1f);
        }

        public void RefreshColors()
        {
            Color ink = _config != null ? _config.ResolveInkColor() : Color.black;
            Color bubble = ResolveBubbleFillColor();

            // ★ null 허용: 만화 레터링 모드에서는 꼬리/몸통 이미지가 아예 만들어지지 않는다
            //   (BuildUi의 DrawBubbleShapes 분기). 예전처럼 "첫 이미지가 null이면 통째로 return"하면
            //   글자 색과 외곽선 색까지 함께 건너뛰어 프리셋 전환이 조용히 무시된다.
            if (_panelOutlineImage != null) _panelOutlineImage.color = ink;
            if (_tailOutlineImage != null) _tailOutlineImage.color = ink;
            if (_panelInnerImage != null) _panelInnerImage.color = bubble;
            if (_tailFillImage != null) _tailFillImage.color = bubble;
            if (_label != null) _label.color = ink;
            if (_labelOutline != null) _labelOutline.effectColor = ResolveTextOutlineColor();
        }

        // ==================== UI 구성 (런타임 생성 — 씬/프리팹 수동 배선 불필요) ====================

        private void ApplyText(string text)
        {
            if (_label == null) return;
            _label.text = text ?? string.Empty;

            // 글자 크기를 매 대사마다 다시 맞춘다. BuildUi(Awake)에서 한 번만 정하면 실행 중에
            // 캐릭터 배율이 바뀌었을 때(모니터 이동/설정 변경) 글자만 옛 크기로 남고, 무엇보다
            // 외곽선 두께가 글자 크기에서 유도되므로(ScaledTextOutline) 둘이 어긋나면 선이 획을 메운다.
            int fontSize = ResolveFontSize() * Mathf.Max(1, TextSupersample);
            if (_label.fontSize != fontSize) _label.fontSize = fontSize;

            // 줄바꿈을 감안한 실제 크기 계산. CanvasScaler를 붙이지 않아 scaleFactor는 1이지만,
            // 나중에 누가 스케일러를 붙여도 조용히 깨지지 않도록 명시적으로 나눠준다.
            // 라벨은 TextSupersample배로 확대된 좌표계에서 살고 localScale로 되돌아오므로, 제너레이터가
            // 주는 값도 그 배율만큼 크다 — 캔버스 픽셀로 환산해서 쓴다.
            float ss = Mathf.Max(1, TextSupersample);
            float maxTextWidth = DrawBubbleShapes ? ScaledMaxTextWidth : ScaledComicMaxTextWidth;
            TextGenerationSettings settings = _label.GetGenerationSettings(new Vector2(maxTextWidth * ss, 0f));
            float scale = settings.scaleFactor > 0f ? settings.scaleFactor : 1f;
            TextGenerator gen = _label.cachedTextGeneratorForLayout;
            float textW = Mathf.Min(maxTextWidth, gen.GetPreferredWidth(_label.text, settings) / scale / ss);
            settings = _label.GetGenerationSettings(new Vector2(textW * ss, 0f));
            float textH = gen.GetPreferredHeight(_label.text, settings) / scale / ss;

            if (!DrawBubbleShapes)
            {
                // ★ 만화 레터링: 몸통/여백/테두리가 없으므로 **글자 블록 자체가 곧 배치 단위**다.
                //   유일한 고정분은 외곽선이 글자 바깥으로 번지는 두께다 — 그만큼 넓혀야 화면 클램프가
                //   외곽선까지 감싸고, 그러지 않으면 화면 끝에서 선만 잘려 글자가 잘린 것처럼 보인다.
                float outlinePad = ScaledTextOutline * 2f;
                _panel.sizeDelta = new Vector2(
                    Mathf.Ceil(textW + outlinePad),
                    Mathf.Ceil(textH + outlinePad));
                // 라벨 사각형은 측정치보다 아주 조금 넉넉히 준다 — 딱 맞추면 반올림 오차 한 픽셀에
                // 줄바꿈이 한 번 더 일어나 마지막 글자가 아래로 떨어진다.
                _labelRect.sizeDelta = new Vector2((textW + 2f) * ss, (textH + 2f) * ss);
                ApplyOutlineStyle();
                return;
            }

            // 타원은 같은 넓이의 사각형보다 모서리 쪽 유효 폭이 좁다 — 글자 블록에 √2를 곱한 뒤
            // 여백을 더해야 글자가 타원 안에 온전히 들어간다(위 EllipseFitFactor 문서 참고).
            // ★ 고정분(테두리+여백)도 캐릭터 배율에 태운다 — 이 항이 폰트와 무관한 상수로 남아 있던 것이
            // "폰트를 하한까지 줄여도 말풍선이 캐릭터보다 크다"의 직접 원인이었다(BubbleScale 문서 참고).
            float pad = (ScaledBorderThickness + ScaledTextPadding) * 2f;
            float w = Mathf.Ceil(textW * EllipseFitFactor + pad);
            float h = Mathf.Ceil(textH * EllipseFitFactor + pad);
            _panel.sizeDelta = new Vector2(w, h);

            // 글자 영역 = 타원에 내접하는 최대 직사각형.
            float innerW = Mathf.Max(1f, w * (1f - EllipseInsetFactor * 2f));
            float innerH = Mathf.Max(1f, h * (1f - EllipseInsetFactor * 2f));
            _labelRect.sizeDelta = new Vector2(innerW * ss, innerH * ss);

            UpdateEllipseSprites(w, h);
        }

        private void BuildUi()
        {
            Color ink = _config != null ? _config.ResolveInkColor() : Color.black;
            Color bubble = ResolveBubbleFillColor();
            int fontSize = ResolveFontSize(); // 캐릭터 배율 비례 + 가독성 하한(ResolveFontSize 문서 참고).

            // 캔버스는 **씬 루트에** 만든다(부모 없음). ScreenSpaceOverlay 캔버스를 움직이는 캐릭터의
            // 자식으로 두면 RAGDOLL로 루트가 회전/이동할 때 부모 변환이 섞여 들어갈 수 있다.
            // Interaction/AppControlDirector.cs도 정확히 같은 이유로 SetParent(null)을 쓴다(그쪽은 실제
            // 실행에서 검증된 유일한 uGUI 경로다). 위치는 매 프레임 UpdatePlacement()가 스크린 좌표로
            // 직접 계산하므로 부모가 없어도 캐릭터를 정확히 따라간다.
            var canvasGo = new GameObject("DialogueBubbleCanvas (" + gameObject.name + ")",
                typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasGo.transform.SetParent(null, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrderBubble;
            _scaler = canvasGo.GetComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            ApplyCanvasScaleFactor();
            _group = canvasGo.GetComponent<CanvasGroup>();
            // 말풍선은 순수 관전용 표시물이다 — 클릭을 절대 가로채지 않는다(비침해 원칙 2).
            _group.blocksRaycasts = false;
            _group.interactable = false;

            if (DrawBubbleShapes)
            {
                // 그리는 순서(뒤 -> 앞): 꼬리 테두리 / 박스 테두리 / 박스 안쪽 / 꼬리 채움 / 글자.
                // 꼬리 채움이 박스 아래 테두리 위에 와야 꼬리와 박스가 하나로 이어져 보인다.
                _tailOutline = CreateTailPart(canvasGo.transform, "TailOutline", ink, filled: true, out _tailOutlineImage);
                _panel = CreatePanel(canvasGo.transform, ink, bubble);
                _tailFill = CreateTailPart(canvasGo.transform, "TailFill", bubble, filled: false, out _tailFillImage);
            }
            else
            {
                // ★ 만화 레터링 모드 — 도형은 하나도 만들지 않는다. 몸통은 "글자를 담아 옮기고
                //   회전/팝인 스케일을 먹는 빈 컨테이너"로만 남는다(Image는 꺼서 흰 사각형이 그려지는
                //   것을 막는다 — 스프라이트 없는 Image는 흰 판을 그린다).
                _panel = CreatePanel(canvasGo.transform, ink, bubble);
                _panelOutlineImage.enabled = false;
                _panelInnerImage.enabled = false;
                // 회전/팝인의 중심이 글자 한가운데여야 한다(몸통 바닥 중앙 기준이면 글자가 그 아래
                // 축을 중심으로 휘둘린다). 배치도 그에 맞춰 "중심 좌표"를 직접 넣는다.
                _panel.pivot = new Vector2(0.5f, 0.5f);
            }

            // 글자는 몸통의 자식이라 몸통을 옮기면 함께 따라온다. 스트레치 앵커가 아니라 **중앙 앵커 +
            // 명시 크기**를 쓰는 이유: 아래 localScale(1/TextSupersample)과 스트레치 앵커를 같이 쓰면
            // 부모 크기에서 한 번, 스케일에서 또 한 번 줄어들어 글자 영역이 절반이 된다.
            // 실제 크기는 ApplyText()가 타원의 내접 사각형으로 매번 다시 잡는다.
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(_panel, false);
            _labelRect = labelGo.GetComponent<RectTransform>();
            _labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            _labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            _labelRect.pivot = new Vector2(0.5f, 0.5f);
            _labelRect.anchoredPosition = Vector2.zero;
            _labelRect.localScale = Vector3.one / Mathf.Max(1, TextSupersample);
            _label = labelGo.GetComponent<Text>();
            _label.font = ResolveKoreanFont(fontSize * Mathf.Max(1, TextSupersample));
            _label.fontSize = fontSize * Mathf.Max(1, TextSupersample);
            // 합성 볼드는 16px 한글에서 획을 서로 붙여 뭉갠다 — 진짜 Bold 페이스를 잡았으면 그걸 쓰고,
            // 못 잡았을 때만 예전처럼 합성 볼드로 굵기를 흉내 낸다(캐릭터의 굵은 획과 같은 문법).
            _label.fontStyle = _cachedFontIsRealBold ? FontStyle.Normal : FontStyle.Bold;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = ink;
            _label.horizontalOverflow = HorizontalWrapMode.Wrap;
            _label.verticalOverflow = VerticalWrapMode.Overflow;
            _label.raycastTarget = false;

            if (!DrawBubbleShapes)
            {
                // ★ 만화 레터링의 외곽선. uGUI 기본 제공 Outline(Shadow 파생)은 글리프 메시를 네 대각선
                //   방향으로 복제해 깔아 준다 — 짧은 대사(대부분 3~8자)에서 정점 수가 문제 될 양이 아니고,
                //   이 프로젝트에 TextMeshPro가 없어 SDF 외곽선을 쓸 수 없으므로 이것이 표준 경로다.
                //
                //   useGraphicAlpha = false인 이유: 페이드는 이미 CanvasGroup.alpha가 캔버스 전체에
                //   곱해 처리한다(ApplyAlpha). 여기서 글자 알파를 한 번 더 곱하면 이중 적용이 되어
                //   등장/소멸 중에 외곽선만 먼저 옅어져 글자가 배경에 잠깐 묻힌다.
                _labelOutline = labelGo.AddComponent<Outline>();
                _labelOutline.useGraphicAlpha = false;
                _labelOutline.effectColor = ResolveTextOutlineColor();
                ApplyOutlineStyle();
            }
        }

        /// <summary>외곽선 두께를 현재 캐릭터 배율에 맞춘다(폰트 크기와 함께 매 대사마다 갱신된다).</summary>
        private void ApplyOutlineStyle()
        {
            if (_labelOutline == null) return;
            float thickness = ScaledTextOutline * Mathf.Max(1, TextSupersample);
            _labelOutline.effectDistance = new Vector2(thickness, thickness);
        }

        private RectTransform CreatePanel(Transform parent, Color ink, Color bubble)
        {
            var go = new GameObject("BubblePanel", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0f); // 바닥 중앙 기준 — 꼬리와 이어 붙이기 쉬운 기준점.
            rect.sizeDelta = new Vector2(80f, 30f);
            _panelOutlineImage = go.GetComponent<Image>();
            _panelOutlineImage.color = ink;
            _panelOutlineImage.raycastTarget = false;

            var innerGo = new GameObject("Inner", typeof(RectTransform), typeof(Image));
            innerGo.transform.SetParent(go.transform, false);
            var innerRect = innerGo.GetComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            float border = ScaledBorderThickness;
            innerRect.offsetMin = new Vector2(border, border);
            innerRect.offsetMax = new Vector2(-border, -border);
            _panelInnerImage = innerGo.GetComponent<Image>();
            _panelInnerImage.color = bubble;
            _panelInnerImage.raycastTarget = false;
            return rect;
        }

        private RectTransform CreateTailPart(Transform parent, string name, Color color, bool filled, out Image image)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 1f); // 위쪽 중앙 기준 — 박스 바닥에 매달린다.
            rect.sizeDelta = new Vector2(ScaledTailWidth, ScaledTailHeight);
            image = go.GetComponent<Image>();
            image.sprite = filled ? GetTailOutlineSprite() : GetTailFillSprite();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        // ==================== 타원 몸통 스프라이트 (런타임 생성) ====================
        //
        // 사각 Image로는 타원이 나오지 않는다. 꼬리(BuildTriangleSprite)와 **완전히 같은 방법**으로,
        // 안티에일리어싱된 알파 커버리지를 담은 텍스처를 코드로 만들어 흰 마스크로 쓴다(색은 Image.color가
        // 입히므로 잉크색/말풍선색 프리셋 전환이 그대로 따라온다 — RefreshColors는 한 줄도 바뀌지 않는다).
        //
        // 왜 고정 크기 텍스처 한 장을 늘려 쓰지 않는가: 말풍선 크기는 대사 길이에 따라 60~240px까지
        // 변한다. 큰 텍스처를 축소하면 밉맵 없는 bilinear 축소가 되어 1픽셀짜리 AA 띠가 뭉개지고,
        // 작은 텍스처를 확대하면 경계가 흐물해진다. 그래서 **표시 크기와 1:1인 텍스처**를 그때그때
        // 만든다. 대사가 바뀔 때만(수 초에 한 번) ~100x50 루프를 도는 것이라 비용은 무시할 수 있고,
        // 같은 크기면 재사용하므로 같은 길이의 대사가 이어질 때는 아예 다시 만들지 않는다.
        /// <summary>
        /// 말풍선 타원(테두리/채움)의 스프라이트를 몸통 크기에 맞춰 다시 굽는다.
        ///
        /// ★ 해상도 주의(2026-08-29 Retina 대응): 인자 width/height는 **캔버스 유닛**(= OS 포인트)인데,
        /// 이 텍스처가 실제로 화면에 깔리는 것은 **물리 픽셀**이다. 캔버스 유닛 크기 그대로 구우면
        /// Retina에서 텍스처가 2배로 늘어나 깔리면서 타원 가장자리의 안티에일리어싱이 뭉개진다 —
        /// 글자만 선명해지고 말풍선 윤곽만 흐린, 더 이상해 보이는 결과가 된다. 그래서 캔버스 스케일만큼
        /// 곱해 **물리 픽셀 해상도로 굽는다**(RectTransform 크기는 그대로라 보이는 크기는 변하지 않는다).
        /// 캐시 키에 그 배율도 포함해, 모니터를 옮겨 배율이 바뀌면 자동으로 다시 굽는다.
        /// </summary>
        private void UpdateEllipseSprites(float width, float height)
        {
            // 캔버스 유닛 -> 물리 픽셀 배율(비Retina 1, Retina 2). 상한을 두는 이유: 말풍선이 커진
            // 상태에서 배율까지 곱해지므로, 병리적인 값이 들어오면 텍스처 메모리가 제곱으로 튄다.
            float deviceScale = Mathf.Clamp(ScreenCoordinateConverter.ResolveCanvasScaleFactor(_config), 1f, 4f);

            int w = Mathf.Max(8, Mathf.CeilToInt(width * deviceScale));
            int h = Mathf.Max(8, Mathf.CeilToInt(height * deviceScale));
            if (_ellipseSpriteSize.x == w && _ellipseSpriteSize.y == h && _ellipseOuterSprite != null) return;

            DestroyEllipseSprites();

            // ★★ 투명 말풍선(2026-08-29 사용자 요구 "말풍선도 흰색바탕이 아니고 얼굴처럼 투명한데다
            // 텍스트가 써져야함"): 바깥 스프라이트를 **채운 타원이 아니라 링(테두리 띠)** 으로 굽는다.
            //
            // 왜 "안쪽 이미지를 투명하게" 로는 안 되는가: 예전 구조는 [채운 잉크 타원] 위에 [조금 작은
            // 말풍선색 타원]을 덮어 그 차집합이 테두리처럼 보이게 한 것이었다. 안쪽만 투명하게 만들면
            // 덮개가 사라져 **잉크로 꽉 찬 검은 타원**이 된다(정확히 반대 결과다).
            //
            // 그래서 캐릭터 머리와 **같은 문법**을 쓴다: 머리는 링 하나(LineRenderer, loop)만 있고 안은
            // 완전히 비어 있다(Editor/SceneBootstrapper.cs "머리 — 검은 링(테두리)만 + 안쪽은 완전히
            // 비어 투명" 참고). 말풍선도 링 한 장이 유일한 외곽선이고 안쪽은 비워 둔다.
            float ringPx = Mathf.Max(1f, ScaledBorderThickness * deviceScale);
            _ellipseOuterSprite = BuildEllipseRingSprite(w, h, ringPx, "StickMateBubbleEllipseRing");

            // 안쪽 이미지는 이제 "선택적 옅은 반투명 채움"이다(기본은 알파 0 = 완전 투명).
            // StickConfig.dialogueBubbleColor의 **알파**가 그 세기를 조절하는 유일한 창구다 —
            // 복잡한 바탕화면(아이콘/글자) 위에서 글자가 안 읽힐 때 살짝 깔아 주기 위한 여지로 남긴다.
            // 링 두께만큼 안으로 들어간 타원이라 링과 겹치지 않는다.
            int border = Mathf.CeilToInt(ScaledBorderThickness * 2f * deviceScale);
            int iw = Mathf.Max(4, w - border);
            int ih = Mathf.Max(4, h - border);
            _ellipseInnerSprite = BuildEllipseSprite(iw, ih, 0f, "StickMateBubbleEllipseInner");
            _ellipseSpriteSize = new Vector2Int(w, h);

            if (_panelOutlineImage != null) _panelOutlineImage.sprite = _ellipseOuterSprite;
            if (_panelInnerImage != null) _panelInnerImage.sprite = _ellipseInnerSprite;
        }

        private void DestroyEllipseSprites()
        {
            DestroySpriteAndTexture(ref _ellipseOuterSprite);
            DestroySpriteAndTexture(ref _ellipseInnerSprite);
            _ellipseSpriteSize = new Vector2Int(-1, -1);
        }

        private static void DestroySpriteAndTexture(ref Sprite sprite)
        {
            if (sprite == null) return;
            Texture2D tex = sprite.texture;
            Destroy(sprite);
            if (tex != null) Destroy(tex);
            sprite = null;
        }

        /// <summary>
        /// 텍스처 사각형에 내접하는 타원의 알파 마스크를 만든다. 경계는 약 1픽셀 폭으로
        /// 안티에일리어싱되므로(투명 오버레이에서 계단이 그대로 보이는 이 앱에서 특히 중요하다)
        /// 가장자리가 매끄럽다.
        ///
        /// 커버리지는 타원 방정식 f = (x/a)² + (y/b)² - 1 을 그 기울기 크기로 나눠 얻는
        /// **근사 부호거리**로 구한다(f 자체는 거리에 비례하지 않아 긴 타원에서 위아래 AA 폭이
        /// 달라진다 — 나눠주면 어느 방향에서도 같은 1픽셀 띠가 된다).
        /// </summary>
        /// <summary>
        /// 타원 **테두리 띠(링)** 의 알파 마스크. 안쪽은 완전히 투명하다 — 캐릭터 머리와 같은 문법
        /// (Editor/SceneBootstrapper.cs의 HeadOutline 링).
        ///
        /// 수학: <see cref="BuildEllipseSprite"/>와 같은 부호 있는 거리 dist(양수 = 타원 바깥)를 쓰고,
        /// "바깥 경계 안쪽" AND "경계에서 thickness보다 깊지 않음"의 교집합을 덮개율로 삼는다.
        ///   바깥쪽 경계 덮개 = clamp01(0.5 - dist)          (dist=0에서 0.5, 안으로 갈수록 1)
        ///   안쪽 경계 덮개  = clamp01(dist + thickness + 0.5)
        ///   링 덮개 = min(둘)
        /// 두 항 모두 1픽셀 폭으로 부드럽게 떨어지므로 투명 오버레이에서도 계단이 보이지 않는다
        /// (이 앱은 프레임버퍼 알파가 곧 창 투명도라 경계 안티에일리어싱이 특히 중요하다).
        /// </summary>
        private static Sprite BuildEllipseRingSprite(int w, int h, float thickness, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            float a = Mathf.Max(0.5f, w * 0.5f);
            float b = Mathf.Max(0.5f, h * 0.5f);
            float cx = w * 0.5f;
            float cy = h * 0.5f;
            float invA2 = 1f / (a * a);
            float invB2 = 1f / (b * b);
            float t = Mathf.Max(0.75f, thickness);

            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float py = y + 0.5f - cy;
                float py2 = py * py;
                for (int x = 0; x < w; x++)
                {
                    float px = x + 0.5f - cx;
                    float f = px * px * invA2 + py2 * invB2 - 1f;
                    float gx = px * invA2;
                    float gy = py * invB2;
                    float g = 2f * Mathf.Sqrt(gx * gx + gy * gy);
                    float dist = g > 1e-6f ? f / g : -1f; // 양수 = 타원 바깥
                    float outer = Mathf.Clamp01(0.5f - dist);
                    float inner = Mathf.Clamp01(dist + t + 0.5f);
                    float coverage = Mathf.Min(outer, inner);
                    pixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            var sprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite BuildEllipseSprite(int w, int h, float inset, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            float a = Mathf.Max(0.5f, w * 0.5f - inset);
            float b = Mathf.Max(0.5f, h * 0.5f - inset);
            float cx = w * 0.5f;
            float cy = h * 0.5f;
            float invA2 = 1f / (a * a);
            float invB2 = 1f / (b * b);

            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float py = y + 0.5f - cy;
                float py2 = py * py;
                for (int x = 0; x < w; x++)
                {
                    float px = x + 0.5f - cx;
                    float f = px * px * invA2 + py2 * invB2 - 1f;
                    // |∇f| = 2·sqrt((x/a²)² + (y/b²)²)
                    float gx = px * invA2;
                    float gy = py * invB2;
                    float g = 2f * Mathf.Sqrt(gx * gx + gy * gy);
                    float dist = g > 1e-6f ? f / g : -1f; // 양수 = 타원 바깥
                    float coverage = Mathf.Clamp01(0.5f - dist);
                    pixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            var sprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        // ==================== 꼬리 스프라이트 (런타임 생성 — 텍스처 에셋 없이) ====================
        //
        // 이 프로젝트에는 스프라이트 에셋이 하나도 없다(캐릭터조차 LineRenderer로 그린다). 삼각형
        // 꼬리는 사각형 Image 조합으로는 깔끔하게 나오지 않으므로, 알파 커버리지를 담은 작은 텍스처
        // 두 장을 코드로 만들어 쓴다. 색은 Image.color로 입히므로(텍스처는 흰색 마스크) 잉크색 프리셋
        // 전환이 그대로 반영된다.
        private static Sprite _tailOutlineSprite;
        private static Sprite _tailFillSprite;

        /// <summary>
        /// 꼬리의 **선**(두 빗변만). 투명 말풍선 전환(2026-08-29) 전에는 "채운 삼각형 위에 조금 작은
        /// 채움 삼각형을 덮어" 테두리를 만들었지만, 안쪽이 투명해지면 덮개가 없어 잉크 삼각형이 그대로
        /// 남는다 — 그래서 처음부터 두 빗변만 있는 띠로 굽는다(몸통 링과 같은 문법).
        ///
        /// 윗변이 자동으로 열리는 이유: 아래 BuildTriangleSprite의 거리 d는 **두 빗변까지의 거리**만
        /// 보므로, 윗변 한가운데는 삼각형 깊숙한 안쪽(d가 큼)이라 띠에 포함되지 않는다. 즉 별도 처리
        /// 없이 "위가 뚫린 V자"가 나오고, 그 열린 윗변이 몸통 타원 링 안으로 파묻혀 선이 이어진다
        /// (UpdatePlacement의 tailTop = 타원 경계 + 링 두께).
        /// </summary>
        private static Sprite GetTailOutlineSprite()
        {
            if (_tailOutlineSprite == null)
            {
                // 띠 두께(텍스처 픽셀). 몸통 링과 같은 화면 두께로 보이도록 "테두리 두께 / 꼬리 폭"
                // 비율을 텍스처 폭에 투영한다 — 이 비율은 캐릭터 배율에 대해 불변이다(아래 참고).
                float texBorder = BorderThickness * (TriangleTexWidth / TailWidth);
                _tailOutlineSprite = BuildTriangleEdgeBandSprite(texBorder, "StickMateTailOutline");
            }
            return _tailOutlineSprite;
        }

        /// <summary>
        /// 꼬리 안쪽의 **선택적 옅은 반투명 채움**(기본 알파 0 = 완전 투명). 몸통의 Inner 이미지와 짝이며,
        /// 세기는 StickConfig.dialogueBubbleColor의 알파 하나로 함께 조절된다.
        /// 두 빗변에서 선 두께만큼 안으로 들어간 삼각형이라 꼬리 선과 겹치지 않는다.
        /// </summary>
        private static Sprite GetTailFillSprite()
        {
            if (_tailFillSprite == null)
            {
                // ★ 여기만 원본 상수를 그대로 쓴다(캐릭터 배율을 곱하지 않는다). 이 값은 "텍스처 폭 대비
                // 테두리 두께의 **비율**"이라 분자/분모에서 배율이 정확히 상쇄된다:
                //   (BorderThickness x s) x (96 / (TailWidth x s)) == BorderThickness x (96 / TailWidth).
                // 그래서 이 정적 캐시는 캐릭터 배율과 무관하게 모든 화자가 공유해도 안전하다
                // (static 메서드라 인스턴스 프로퍼티를 참조할 수도 없다).
                float texBorder = BorderThickness * (TriangleTexWidth / TailWidth);
                _tailFillSprite = BuildTriangleSprite(texBorder, "StickMateTailFill");
            }
            return _tailFillSprite;
        }

        private const int TriangleTexWidth = 96;
        private const int TriangleTexHeight = 72;

        /// <summary>
        /// 아래로 뾰족한 삼각형의 알파 마스크 텍스처를 만든다. inset&gt;0이면 두 빗변에서 그만큼 안으로
        /// 들어간(윗변은 그대로인) 작은 삼각형이 된다. 경계는 1픽셀 안티에일리어싱되어 투명 오버레이
        /// 창에서도 계단이 보이지 않는다.
        /// </summary>
        /// <summary>
        /// 삼각형의 **두 빗변만** 남긴 띠(윗변은 열려 있다). 안쪽은 완전히 투명하다.
        /// 덮개율 = min(clamp01(d + 0.5), clamp01(band - d + 0.5)) — d는 두 빗변 중 가까운 쪽까지의
        /// 안쪽 거리이므로, 0 &lt;= d &lt;= band 인 띠만 남고 그보다 깊은 안쪽/바깥은 투명해진다.
        /// </summary>
        private static Sprite BuildTriangleEdgeBandSprite(float band, string name)
        {
            const int w = TriangleTexWidth;
            const int h = TriangleTexHeight;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var topLeft = new Vector2(0f, h);
            var topRight = new Vector2(w, h);
            var apex = new Vector2(w * 0.5f, 0f);
            float t = Mathf.Max(1f, band);

            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = Mathf.Min(SignedDistance(p, topLeft, apex), SignedDistance(p, apex, topRight));
                    float coverage = Mathf.Min(Mathf.Clamp01(d + 0.5f), Mathf.Clamp01(t - d + 0.5f));
                    pixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            var sprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite BuildTriangleSprite(float inset, string name)
        {
            const int w = TriangleTexWidth;
            const int h = TriangleTexHeight;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            // 삼각형 꼭짓점: 위 좌우 모서리와 아래 중앙 꼭짓점(텍스처 좌표, y가 위쪽).
            var topLeft = new Vector2(0f, h);
            var topRight = new Vector2(w, h);
            var apex = new Vector2(w * 0.5f, 0f);

            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    // 각 빗변 안쪽까지의 거리(양수 = 삼각형 안쪽). 왼쪽 빗변은 topLeft->apex,
                    // 오른쪽 빗변은 apex->topRight로 방향을 잡아 안쪽이 항상 왼편이 되게 한다.
                    float dLeft = SignedDistance(p, topLeft, apex);
                    float dRight = SignedDistance(p, apex, topRight);
                    float d = Mathf.Min(dLeft, dRight) - inset;
                    float coverage = Mathf.Clamp01(d + 0.5f); // 1픽셀 폭 안티에일리어싱.
                    pixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            var sprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        /// <summary>선분 a->b 기준 부호 있는 거리. 진행 방향의 **왼쪽**이 양수다.</summary>
        private static float SignedDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len = ab.magnitude;
            if (len < 1e-5f) return 0f;
            Vector2 n = new Vector2(-ab.y, ab.x) / len; // 왼쪽 법선
            return Vector2.Dot(p - a, n);
        }

        // ==================== 한글 폰트 해석 ====================

        private static Font _cachedFont;
        private static bool _koreanGlyphVerified;
        /// <summary>고른 폰트가 **진짜 Bold 페이스**인가. true면 Unity의 합성 볼드를 끈다.</summary>
        private static bool _cachedFontIsRealBold;

        /// <summary>
        /// 한글이 **실제로 그려지는** 폰트를 고른다. Unity 내장 LegacyRuntime.ttf(Arial 계열)는 한글
        /// 글리프가 없어 네모(두부)로 나오므로, OS 설치 폰트를 후보 순서대로 만들어 보고
        /// RequestCharactersInTexture -> GetCharacterInfo로 "한" 글자의 글리프 폭이 실제로 잡히는지를
        /// 실측해 첫 성공 폰트를 쓴다(이름만 보고 믿지 않는다 — 설치 여부/이름 표기가 OS마다 다르다).
        /// 전부 실패하면 내장 폰트로 폴백하고 경고를 남긴다(앱이 죽지는 않는다).
        ///
        /// ============================================================================
        /// "글자가 부드럽지 않다"(사용자 신고 2026-08-29) 진단 기록 — 추측 금지, 실측만
        /// ============================================================================
        /// 가설 (a) "캔버스 스케일 때문에 작게 렌더된 뒤 확대된다" -> **기각**. 이 캔버스에는
        ///   CanvasScaler가 없어 scaleFactor는 정확히 1이고, 글리프는 fontSize 그대로
        ///   1:1 픽셀로 그려지고 있었다.
        /// 가설 (b) "레거시 Text의 비트맵 아틀라스 한계" -> **부분적**. 아틀라스 자체는 FreeType
        ///   그레이스케일 AA라 그 크기에서는 이미 매끈하다.
        /// 실제 원인 -> **앱 프레임버퍼가 1x인데 화면이 2x Retina라 OS가 전부 2배로 확대한다.**
        ///   실측 증거 3가지:
        ///     1) 실행 로그: `Screen=(1512x982)`, `system_profiler`: 실제 패널 `3024 x 1964 Retina`.
        ///     2) ProjectSettings의 `macRetinaSupport: 0`, 빌드된 Info.plist에
        ///        `NSHighResolutionCapable` 키 없음.
        ///     3) 같은 스크린샷 안에서 macOS가 그린 글자는 선명한데 말풍선 글자만 2x2 블록으로
        ///        뭉개져 있다(같은 물리 크기, 절반의 실효 해상도).
        ///   이 값은 8baa871에서 Retina 관련 실험 중 1 -> 0으로 바뀐 뒤 되돌려지지 않은 것으로,
        ///   `Assets/Editor/BuildStandalone.cs`의 해당 주석은 그 실험을 "폐기했다(코드는 되돌림)"고
        ///   기록하고 있다(설정 값만 남았다).
        ///
        /// ★ 해소됨 (2026-08-29 Retina 대응 라운드, 사용자 신고 "전체적으로 해상도가 너무 안좋음").
        /// `macRetinaSupport`를 1로 되돌리고, 그로 인해 딸려오는 문제들을 함께 처리했다:
        ///   · 좌표계: DPI 배율의 단일 소스를 Platform/ScreenCoordinateConverter로 옮기고 런타임 실측
        ///     (창 폭[OS 포인트] / Screen.width[Unity 픽셀])으로 자동 산출. StickConfig.desktopDpiScale은
        ///     "수동 오버라이드(0 이하 = 자동)"로 의미가 바뀌었다.
        ///   · UI 크기: 세 ScreenSpaceOverlay 캔버스 전부 CanvasScaler.scaleFactor = 1/dpi
        ///     (<see cref="ApplyCanvasScaleFactor"/>) — 캔버스 1유닛 == OS 포인트 1이라 물리적 크기는
        ///     Retina 이전과 같고 해상도만 2배가 된다.
        ///   · "OS-px 단위 필드" 재검토: 결론은 그 필드들이 전부 **OS 포인트**이며 배율 보정 뒤에는
        ///     값을 바꿀 필요가 없다는 것이었다(Core/StickConfig.cs의 해당 필드 주석 참고).
        ///
        /// 이 컴포넌트에 남은 선명도 대책:
        ///   · 합성 볼드 대신 **진짜 Bold 페이스** 사용(아래) — 16pt 한글에서 합성 볼드는 획을
        ///     서로 붙여 뭉개는 가장 큰 원인이다.
        ///   · 글자를 기울이면 글리프 아틀라스가 다시 샘플링되므로, 기울기는 글리프가 충분히 큰
        ///     경우에만 켠다(<see cref="ComicTiltMinGlyphPixels"/>).
        ///   · <see cref="TextSupersample"/>은 근본 원인이 사라져 1(끔)로 되돌렸다(그 상수 주석 참고).
        /// </summary>
        private static Font ResolveKoreanFont(int size)
        {
            if (_cachedFont != null) return _cachedFont;

            // 1순위: 진짜 Bold 페이스(합성 볼드 회피). 실패하면 조용히 아래 일반 후보로 넘어간다.
            // ★ 만화 레터링용으로 **더 무거운 페이스를 먼저** 시도한다(리더 지시 "굵고 또렷하게").
            //   실측(2026-08-29, system_profiler SPFontsDataType): 이 머신의 AppleSDGothicNeo.ttc에는
            //   Heavy / ExtraBold / SemiBold / Bold 페이스가 모두 들어 있다. 한글 손글씨·만화 전용
            //   폰트는 시스템에 없고(Comic Sans MS / Marker Felt / Noteworthy는 전부 한글 글리프가
            //   없어 두부가 된다 — 이 프로젝트가 이미 겪은 실패), 그래서 "가장 무거운 고딕 + 외곽선 +
            //   미세 기울임"으로 만화 느낌을 낸다. 어떤 후보든 아래 CanRenderKorean 실측을 통과해야만
            //   채택되므로, 목록에 없는 환경에서도 두부가 되는 일은 없다.
            var boldCandidates = new List<string>
            {
                "AppleSDGothicNeo-Heavy", "Apple SD Gothic Neo Heavy",
                "AppleSDGothicNeo-ExtraBold", "Apple SD Gothic Neo ExtraBold",
                "NanumGothicExtraBold", "NanumSquareRoundEB", "NanumSquareEB",
                "AppleSDGothicNeo-Bold", "Apple SD Gothic Neo Bold", "AppleGothic Bold",
                "Malgun Gothic Bold", "맑은 고딕 Bold", "NanumGothicBold", "NanumGothic Bold",
                "NanumBarunGothicBold", "PingFangSC-Semibold", "HiraginoSans-W6",
            };
            for (int i = 0; i < boldCandidates.Count; i++)
            {
                Font f = TryCreateFont(boldCandidates[i], size);
                if (f == null) continue;
                if (!CanRenderKorean(f, size, FontStyle.Normal)) continue;
                _cachedFont = f;
                _koreanGlyphVerified = true;
                _cachedFontIsRealBold = true;
                Debug.Log($"[말풍선] 한글 폰트 확정: '{boldCandidates[i]}' (진짜 Bold 페이스, 글리프 실측 통과) — " +
                    "합성 볼드를 끄고 이 페이스를 그대로 씁니다.");
                return _cachedFont;
            }

            var candidates = new List<string>
            {
                // macOS 기본 한글 폰트
                "Apple SD Gothic Neo", "AppleSDGothicNeo-Regular", "AppleGothic", "AppleMyungjo",
                // Windows 기본 한글 폰트
                "Malgun Gothic", "맑은 고딕", "Gulim", "Dotum", "Batang",
                // 흔히 설치되는 무료 한글 폰트
                "NanumGothic", "Nanum Gothic", "NanumBarunGothic",
                // CJK 전반을 담는 범용 폰트
                "PingFang SC", "Hiragino Sans", "Arial Unicode MS",
            };

            // 설치 목록을 훑어 이름에 한글 계열 키워드가 든 폰트도 후보 뒤에 붙인다(이름 표기가 달라
            // 위 목록이 전부 빗나가는 환경 대비).
            try
            {
                string[] installed = Font.GetOSInstalledFontNames();
                if (installed != null)
                {
                    for (int i = 0; i < installed.Length; i++)
                    {
                        string n = installed[i];
                        if (string.IsNullOrEmpty(n)) continue;
                        if (n.IndexOf("Gothic", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                            n.IndexOf("Nanum", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                            n.IndexOf("Myungjo", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                            n.IndexOf("PingFang", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                            n.IndexOf("Hiragino", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            if (!candidates.Contains(n)) candidates.Add(n);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[말풍선] OS 폰트 목록 조회 실패(무시하고 후보 목록만 사용): " + e.Message);
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                Font f = TryCreateFont(candidates[i], size);
                if (f == null) continue;
                if (!CanRenderKorean(f, size, FontStyle.Bold)) continue;
                _cachedFont = f;
                _koreanGlyphVerified = true;
                _cachedFontIsRealBold = false;
                Debug.Log($"[말풍선] 한글 폰트 확정: '{candidates[i]}' (글리프 실측 통과, 합성 볼드 사용).");
                return _cachedFont;
            }

            _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _koreanGlyphVerified = false;
            Debug.LogWarning("[말풍선] 한글을 렌더링할 수 있는 OS 폰트를 찾지 못해 내장 폰트로 폴백합니다 — " +
                             "말풍선의 한글이 네모로 보일 수 있습니다.");
            return _cachedFont;
        }

        private static Font TryCreateFont(string name, int size)
        {
            try { return Font.CreateDynamicFontFromOSFont(name, size); }
            catch { return null; }
        }

        /// <summary>"한글" 글자의 글리프가 실제로 잡히는지 실측한다(이름만 보고 믿지 않는다).
        /// 실제로 쓸 <paramref name="style"/> 그대로 조회해야 의미가 있다 — 합성 볼드를 끌 폰트를
        /// Bold로 조회하면 검증한 것과 다른 경로를 재는 셈이 된다.</summary>
        private static bool CanRenderKorean(Font font, int size, FontStyle style)
        {
            try
            {
                font.RequestCharactersInTexture("한글", size, style);
                if (!font.GetCharacterInfo('한', out CharacterInfo info, size, style)) return false;
                return info.glyphWidth > 0 && info.advance > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
