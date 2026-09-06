using UnityEngine;

namespace StickMate.Core
{
    /// <summary>이 획이 <b>어느 화면상 하한</b>에 속하는가. 하한이 셋이므로 갈래도 셋이다.</summary>
    public enum StrokeFloorRole
    {
        /// <summary>낱선 — 그 선이 사라지면 그 요소가 사라지는 쪽.
        /// <see cref="StickConfig.MinStrokeScreenPoints"/>(2.00pt).</summary>
        Standalone = 0,

        /// <summary>채운 도형의 경계선 · 머리 링 — 보이게 하는 일을 채움이 한다.
        /// <see cref="StickConfig.MinFillOutlineScreenPoints"/>(1.00pt). 표식은 <see cref="FillOutlineStroke"/>.</summary>
        FillOutline = 1,

        /// <summary>인계본 착용 조각(계약 v2)의 획 — <b>의도적으로 얇게</b> 설계된 선.
        /// <see cref="StickConfig.MinAccessoryStrokeScreenPoints"/>(1.00pt). 표식은 <see cref="AccessoryStrokeMark"/>.</summary>
        Accessory = 2,
    }

    /// <summary>
    /// ★★ 「이 선은 어느 하한 소속인가」를 <b>한 곳에서만</b> 답한다 — 2026-09-06 (A-2).
    ///
    /// ============================================================================
    /// 왜 이 파일이 생겼나 — 같은 삼항식이 세 곳에 복사돼 있었고 한 곳만 갱신됐다
    /// ============================================================================
    /// <see cref="FillOutlineStroke"/>의 클래스 문서는 이미 이렇게 적어 두었다:
    /// <i>"세 곳이 각자 다른 방법으로 «이 선이 어느 하한 소속인가»를 판단하면 <b>반드시 갈라진다</b>"</i>.
    /// 그 예언이 그대로 일어났다. 하한이 <b>둘</b>이던 동안에는 <c>FillOutlineStroke.Is(lr)</c> 한 번이면
    /// 됐지만, 계약 v2(인계본)가 세 번째 갈래를 만들면서 판정이 <b>2단 삼항식</b>이 됐고 —
    /// 그 삼항식이 세 곳에 각각 복사돼 있었다:
    /// <list type="number">
    ///   <item><c>StickmanAgent.ApplyStrokeWidthsForScale</c>의 안전망 훑기 — <b>갱신됨</b></item>
    ///   <item><c>Platform/StrokeWidthDiagnostics.Measure</c>(<c>[렌더품질]</c> 로그) — <b>2분류인 채로 남음</b></item>
    ///   <item><c>Tests/PlayMode/CharacterScaleRuntimeTests</c>의 통 나누기 — <b>2분류인 채로 남음</b></item>
    /// </list>
    ///
    /// <para><b>증상</b>: 인계본 획은 설계대로 1.00pt(= 0.05000 유닛)로 그려지는데, 남은 두 곳은 그것을
    /// <b>낱선</b>으로 세어 2.00pt 하한(0.0999 유닛)과 비교했다 — 정확히 절반이라 언제나 "하한 미달 —
    /// 결함". <b>화면에 그려지는 그림은 처음부터 옳았고, 자(尺)가 낡은 것이다.</b> 그리고 이 오보는
    /// <c>[렌더품질]</c> 줄을 통해 사용자 신고로 되돌아온다 — 그 신고를 받은 사람은 멀쩡한 렌더러를
    /// 고치려고 한 라운드를 쓴다(M6이 채움 경계선에서 이미 한 번 겪은 형태 그대로다).</para>
    ///
    /// <para><b>그래서 판정을 여기 하나로 모은다.</b> 네 번째 갈래가 생기는 날 고칠 곳은 이 파일뿐이고,
    /// 나머지는 <see cref="Of"/>를 부르고 있으므로 자동으로 따라온다. 값(하한)도 여기서 상수를
    /// <b>참조</b>로 나른다 — 숫자를 소비자 쪽에 적으면 상수가 움직일 때 한쪽만 따라간다.</para>
    ///
    /// <para><b>플랫폼</b>: 여기에는 플랫폼 분기가 <b>하나도 없다</b>. 표식 조회와 하한 선택은 macOS와
    /// Windows가 같은 답을 내야 하는 규칙이고, 두 OS의 <c>OverlayStateEnforcer</c>가 같은
    /// <c>StrokeWidthDiagnostics</c>를 부르므로 이 파일이 갈라지면 <b>양쪽이 똑같이 틀린다</b>
    /// (= 플랫폼 갭이 아니라 공유 계측 갭). DPI/표시배율은 pt 환산에서만 곱해지고 <b>이 판정에는
    /// 개입하지 않는다</b> — 즉 Windows 125%에서도 갈래는 같고 숫자만 달라진다.</para>
    ///
    /// <para><b>우선순위</b>: 채움 경계선을 먼저 본다. 두 표식은 렌더러가
    /// <c>if (handoffWidth &gt; 0) AccessoryStrokeMark else if (isFillOutline) FillOutlineStroke</c>로
    /// <b>배타</b>하게 붙이므로 오늘은 순서가 결과를 바꾸지 않지만, 순서를 소비자마다 다르게 적으면
    /// 언젠가 갈라진다. 그래서 순서도 여기 한 줄로 고정한다.</para>
    /// </summary>
    public static class StrokeFloorRoles
    {
        /// <summary>이 선의 갈래를 <b>선 자신에게</b> 묻는다. 이름/목록으로 가르지 않는다 —
        /// 목록을 소비자 쪽에 적으면 새 DLC 도형이 조용히 규칙 밖으로 빠져나간다
        /// (<see cref="FillOutlineStroke"/> 문서). null이면 낱선으로 본다(가장 두꺼운 하한 = 안전한 쪽).</summary>
        public static StrokeFloorRole Of(LineRenderer line)
            => FillOutlineStroke.Is(line) ? StrokeFloorRole.FillOutline
                : AccessoryStrokeMark.Is(line) ? StrokeFloorRole.Accessory
                : StrokeFloorRole.Standalone;

        /// <summary>그 갈래의 <b>화면상</b> 하한(OS 포인트). 상수를 베끼지 않고 <see cref="StickConfig"/>에서 나른다.</summary>
        public static float ScreenPoints(StrokeFloorRole role)
        {
            switch (role)
            {
                case StrokeFloorRole.FillOutline: return StickConfig.MinFillOutlineScreenPoints;
                case StrokeFloorRole.Accessory: return StickConfig.MinAccessoryStrokeScreenPoints;
                default: return StickConfig.MinStrokeScreenPoints;
            }
        }

        /// <summary>그 갈래의 <b>월드</b> 하한을 고른다. 세 값은 부르는 쪽이 <b>같은 pt/유닛</b>으로
        /// 환산해서 넘긴다(<c>StickmanAgent.RefreshStrokeFloors</c>) — 여기서 다시 환산하면
        /// 환산이 두 곳이 되고, 화면이 바뀔 때 한쪽만 따라간다.</summary>
        public static float World(StrokeFloorRole role,
            float standaloneWorld, float fillOutlineWorld, float accessoryWorld)
        {
            switch (role)
            {
                case StrokeFloorRole.FillOutline: return fillOutlineWorld;
                case StrokeFloorRole.Accessory: return accessoryWorld;
                default: return standaloneWorld;
            }
        }
    }
}
