using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// ★ "이 <see cref="LineRenderer"/>의 <b>그려진 두께에 역잉크 분리막이 포함돼 있다</b>"를
    /// 선이 스스로 들고 다니는 표식 + 그 <b>산술 계약</b>(2026-09-03).
    ///
    /// ============================================================================
    /// 왜 필요한가 — 게이트가 조용히 거짓말을 하기 때문이다
    /// ============================================================================
    /// 막을 걸면 <see cref="LineRenderer.startWidth"/>는 <b>잉크 + 막</b>이 된다. 그런데
    /// <c>Platform/StrokeWidthDiagnostics</c>도 <see cref="StickmanAgent"/>의 하한 되올리기도
    /// 그 값을 <b>잉크</b>로 읽는다. 그대로 두면 이런 상태가 만들어진다:
    ///
    /// <code>
    ///   Windows 100% · 배율 0.35 · 낱선 하한 2.00pt(= 2.00 물리픽셀)
    ///   startWidth = 2.00pt  →  "하한 지켜짐"      ← 게이트의 판정
    ///   실제 잉크  = 2.00 − 2×1.00 = 0.00 물리픽셀 ← 화면에는 잉크가 <b>없다</b>
    /// </code>
    ///
    /// 이 저장소의 서명(署名) 사고다 — <b>실패한 측정과 성공한 측정이 똑같이 생겼다.</b>
    /// 그래서 두께를 다루는 모든 곳이 <b>같은 두 함수</b>(<see cref="Drawn"/>/<see cref="InkCore"/>)를
    /// 통해서만 잉크와 막을 오간다.
    ///
    /// ============================================================================
    /// 계약 — <b>순서가 전부다</b>
    /// ============================================================================
    /// <code>
    ///   ○ 옳은 순서:  inkCore = Max(baked × ratio, floor);   drawn = inkCore + sides × membrane
    ///   ✗ 금지 형태:  drawn   = Max(baked × ratio + sides × membrane, floor)
    /// </code>
    /// 두 줄은 코드상 거의 같아 보이지만 <b>정반대</b>다. 금지 형태는 막이 하한을 대신 채워 주므로
    /// 위 Win100% 예시에서 <b>잉크 코어가 0.000px</b>이 된다. 하한은 "선이 보여야 한다"는 조건이고,
    /// 그 조건을 만족시켜야 하는 것은 <b>잉크</b>지 막이 아니다.
    ///
    /// <para><b>sides</b>: 낱선은 좌우 양쪽에 막이 붙으므로 <see cref="StandaloneSides"/> = 2,
    /// 머리 링·채움 경계선은 채움이 한쪽을 막고 있어 바깥 한쪽뿐이라 <see cref="FillBoundarySides"/> = 1.
    /// (<see cref="FillOutlineStroke"/>가 이미 그 두 범주를 가르고 있다 — 여기서 다시 가르지 않는다.)</para>
    ///
    /// ============================================================================
    /// ★ 오늘은 막이 <b>0</b>이다 — 이 파일은 계약과 게이트만 먼저 세운다
    /// ============================================================================
    /// <see cref="MembranePhysicalPixels"/>가 0인 동안 <see cref="Drawn"/>은 입력을 <b>그대로</b>
    /// 돌려주고 표식도 붙지 않는다. 즉 병합 이전과 <b>비트 동일</b>이다.
    /// 그것이 이 라운드가 되돌릴 수 있는 문이다 — 막 자체(종단 원판 + 정렬층)는
    /// <c>game-architect</c> 경유 판정 대기이고, 그 판정이 무엇이든 <b>하한 계약은 먼저 옳아야 한다</b>
    /// (design-character R9 §21: "하한 계약 파국은 A·A′ 공통이고 Windows 100% 전용이다").
    ///
    /// <para><b>왜 이 상수가 <see cref="StickConfig"/>에 없는가</b>: (1) 이 값은 사용자가 조절할
    /// 성질이 아니라 "펜이 어떻게 생겼는가"라는 <b>렌더링 불변식</b>이고(같은 이유로
    /// <c>States/LimbCurveRenderer</c>의 필렛 상수들도 그 클래스에 있다), (2) 에디터 굽기 · 런타임 ·
    /// 진단 · 테스트가 <b>같은 하나</b>를 봐야 하며, (3) 물리픽셀 단위라 OS 포인트 단위인
    /// <see cref="StickConfig.MinStrokeScreenPoints"/> 옆에 두면 단위가 섞여 읽힌다.
    /// ★ 막이 사용자 설정으로 올라가면 그때 <see cref="StickConfig"/>로 옮긴다 —
    /// 그 순간 <c>DefaultStickConfig.asset</c>도 함께 고쳐야 한다(거짓 통과 9번 형태).</para>
    ///
    /// <para><b>비용</b>: 필드 하나짜리 컴포넌트이고, 붙는 시점은 <b>배율 변경/다시 굽기</b>처럼
    /// 드문 경로뿐이다. 조회는 <c>TryGetComponent</c>(할당 0). 막이 0인 동안에는 아예 붙지 않는다.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InkMembraneStroke : MonoBehaviour
    {
        /// <summary>막 한 겹의 두께(<b>물리픽셀</b>). ★ <b>오늘은 0 — 막 미도입.</b>
        /// design-art의 설계값은 1 물리픽셀이고, 그 값을 여기 넣는 순간 막이 전면적으로 켜진다.
        /// <para>단위가 OS 포인트가 아니라 <b>물리픽셀</b>인 이유: 막의 목적이 "배경이 잉크색이어도
        /// 잉크의 경계가 보인다"이고, 그 조건은 <b>화면에 실제로 찍히는 픽셀</b>에서 성립해야 한다.
        /// Retina에서 1pt는 2픽셀이라 pt로 적으면 플랫폼마다 다른 두께가 된다.</para></summary>
        public const float MembranePhysicalPixels = 0f;

        /// <summary>낱선 — 좌우 양쪽에 막이 붙는다.</summary>
        public const int StandaloneSides = 2;

        /// <summary>채운 도형의 경계선 · 머리 링 — 안쪽은 채움이 막고 있어 바깥 한쪽뿐이다.</summary>
        public const int FillBoundarySides = 1;

        [SerializeField] private int sides = StandaloneSides;

        /// <summary>이 선의 그려진 두께에 포함된 막의 겹 수(0~2).</summary>
        public int Sides => Mathf.Clamp(sides, 0, StandaloneSides);

        /// <summary>선의 <b>역할</b>이 정하는 겹 수. 역할 판정은 <see cref="FillOutlineStroke"/> 하나뿐이다.</summary>
        public static int SidesFor(bool isFillOutline) => isFillOutline ? FillBoundarySides : StandaloneSides;

        /// <summary>선의 역할에서 곧바로 구하는 겹 수(널이면 낱선으로 본다).</summary>
        public static int SidesFor(LineRenderer line) => SidesFor(FillOutlineStroke.Is(line));

        /// <summary>이 선에 <b>실제로 걸려 있는</b> 겹 수. 표식이 없으면 0이다 —
        /// "역할상 2겹이어야 한다"와 "지금 2겹이 들어 있다"는 <b>다른 질문</b>이고,
        /// 게이트가 물어야 하는 것은 후자다(막에서 제외된 선을 빼면 그 선의 잉크를 과소평가한다).</summary>
        public static int AppliedSides(LineRenderer line)
            => line != null && line.TryGetComponent(out InkMembraneStroke m) ? m.Sides : 0;

        /// <summary>잉크 코어 → 화면에 그려질 두께. 막이 0이거나 겹이 0이면 <b>입력 그대로</b>다
        /// (덧셈조차 하지 않는다 — 비트 동일을 부동소수 추론 없이 보장하기 위해서다).</summary>
        public static float Drawn(float inkCoreWidth, float membraneWidth, int sides)
            => membraneWidth > 0f && sides > 0 ? inkCoreWidth + sides * membraneWidth : inkCoreWidth;

        /// <summary>그려진 두께 → 잉크 코어. <see cref="Drawn"/>의 정확한 역이다.
        /// <b>게이트는 반드시 이 값으로 하한을 판정한다</b>(클래스 문서의 Win100% 사고).</summary>
        public static float InkCore(float drawnWidth, float membraneWidth, int sides)
            => membraneWidth > 0f && sides > 0 ? drawnWidth - sides * membraneWidth : drawnWidth;

        /// <summary>표식을 붙인다(겹 수가 다르면 갱신). <paramref name="membraneWidth"/>가 0 이하면
        /// <b>아무 일도 하지 않는다</b> — 막이 없는데 표식만 남으면 게이트가 잉크를 과소평가한다.</summary>
        public static void Mark(LineRenderer line, int sides, float membraneWidth)
        {
            if (line == null || membraneWidth <= 0f || sides <= 0) return;
            if (!line.TryGetComponent(out InkMembraneStroke marker))
                marker = line.gameObject.AddComponent<InkMembraneStroke>();
            marker.sides = Mathf.Clamp(sides, 0, StandaloneSides);
        }
    }
}
