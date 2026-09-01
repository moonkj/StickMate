using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// ★ "이 <see cref="LineRenderer"/>는 <b>채운 도형의 경계선</b>이다"를 선이 스스로 들고 다니는 표식
    /// (2026-09-02, docs/CHARACTER_FORM_SPEC.md 19절 M6).
    ///
    /// ============================================================================
    /// 왜 컴포넌트인가 — 하한이 둘이 되는 순간 <b>세 곳</b>이 같은 질문을 하게 됐다
    /// ============================================================================
    /// <list type="number">
    ///   <item><see cref="StickmanAgent"/>의 구워진 선 훑기(<c>_lineRenderers</c>, 머리 링 포함)</item>
    ///   <item>같은 클래스의 <c>_dynamicVisuals</c> 안전망 훑기(액세서리/펫/FX)</item>
    ///   <item><c>Platform/StrokeWidthDiagnostics</c>의 씬 전체 계측(<c>[렌더품질]</c> 로그)</item>
    /// </list>
    /// 세 곳이 각자 다른 방법으로 "이 선이 어느 하한 소속인가"를 판단하면 <b>반드시 갈라진다</b> —
    /// 그리고 갈라져도 화면은 멀쩡해 보이는 종류의 갈라짐이다(렌더러가 1.00pt로 그린 직후 에이전트가
    /// 2.00pt로 되올려 놓으면, 테스트는 초록인데 그림은 하나도 안 바뀐다). 그래서 <b>판단하지 않고
    /// 물어본다</b>: 선을 만든 쪽이 그 자리에서 표식을 붙이고, 소비자는 전부
    /// <see cref="Is"/> 하나만 부른다.
    ///
    /// <para><b>왜 이름으로 가르지 않는가</b>: 액세서리 도형 이름은 30종 81개이고 DLC로 늘어난다.
    /// 목록을 소비자 쪽에 적어 두면 새 도형을 추가한 사람이 그 목록을 고치는 것을 잊는 순간
    /// 그 도형만 조용히 규칙 밖으로 빠져나간다(<see cref="CharacterVisualRegistry"/>가 pull 방식을
    /// 고른 것과 같은 이유).</para>
    ///
    /// <para><b>비용</b>: 필드가 없는 빈 컴포넌트이고, 붙는 자리는 액세서리를 다시 구울 때
    /// 생성되는 채움 도형의 선(착용 중인 것만, 보통 10개 미만)과 머리 링 1개뿐이다.
    /// 조회는 <c>TryGetComponent</c>(할당 0)이고 매 프레임 경로가 아니다 —
    /// 배율 변경/진단 로그처럼 드물게 도는 곳에서만 부른다.</para>
    ///
    /// <para><b>머리 링은 왜 프리팹에 굽지 않는가</b>: 구우면 <c>StickMate/Rebuild All</c>을
    /// 돌리기 전까지 변경이 조용히 무효가 된다(빌드는 프리팹 에셋을 그대로 쓴다).
    /// 대신 <see cref="StickmanAgent"/>가 Awake에서 실행 중에 붙인다 —
    /// 프리팹은 1바이트도 바뀌지 않고, 다음 빌드부터 즉시 적용된다.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FillOutlineStroke : MonoBehaviour
    {
        /// <summary>이 선이 채움 경계선인가. <paramref name="line"/>이 null이면 false.</summary>
        public static bool Is(LineRenderer line)
            => line != null && line.TryGetComponent(out FillOutlineStroke _);

        /// <summary>표식을 붙인다(이미 있으면 그대로). 붙일 수 없으면 아무 일도 하지 않는다.</summary>
        public static void Mark(LineRenderer line)
        {
            if (line == null || line.TryGetComponent(out FillOutlineStroke _)) return;
            line.gameObject.AddComponent<FillOutlineStroke>();
        }
    }
}
