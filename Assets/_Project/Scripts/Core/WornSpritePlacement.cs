using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// ★ <b>착용 비트맵의 배치 규약</b> — 「캔버스를 몸의 어디에 얼마만큼 놓는가」와
    /// 「그 안 어디까지가 실제 잉크인가」의 <b>단일 정의처</b>.
    /// 2026-09-09, docs/GAME_ARCHITECTURE_REVIEW.md §19-8-3.
    ///
    /// ============================================================================
    /// 왜 이 타입이 따로 있는가 — <b>같은 규칙을 세 어셈블리가 쓴다</b>
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>렌더러</b>(<c>StickMate.Runtime</c>) — 이 규칙으로 스프라이트를 놓고 잉크 범위를 신고한다.</item>
    ///   <item><b>에디터 굽기</b>(<c>Assembly-CSharp-Editor</c>) — PNG 알파를 훑어 값을 만든다.</item>
    ///   <item><b>테스트</b>(EditMode / PlayMode) — 구운 값이 기대와 같은지 대조한다.</item>
    /// </list>
    /// 셋이 각자 산수를 갖고 있으면 «굽는 값»과 «읽는 값»이 갈라지는 날 증상이
    /// <b>「캐릭터가 이유 없이 공중에 뜬다 / 화면 끝에서 밀려난다」</b>로 나타나고,
    /// 그 원인은 <b>그림 쪽으로 오진된다</b>(§19-9-d 2번). 그래서 한 벌만 둔다.
    ///
    /// ============================================================================
    /// 좌표 규약 (여기가 정본)
    /// ============================================================================
    /// <list type="bullet">
    ///   <item>단위는 전부 <b>머리 반경 R의 배수</b>다. 월드 유닛 절대값이 하나도 없다 —
    ///     그래야 캐릭터 배율 다이얼(0.35~1.00)을 자동으로 따라간다.</item>
    ///   <item>원점은 <b>앵커</b>다(머리/눈/머리카락 = 머리 중심, 목/어깨 = 어깨선).
    ///     앵커를 데이터에 안 넣는 이유는 <b>자리끼리의 규칙</b>이라 팩 작성자가 정하면 안 되기 때문이다.</item>
    ///   <item>정규화 좌표 <c>u</c>,<c>v</c>는 캔버스 <b>좌하단 (0,0) ~ 우상단 (1,1)</b>
    ///     (Unity 텍스처 좌표계 그대로 — y가 위다).</item>
    ///   <item>픽셀 -> 정규화는 <b>픽셀 가장자리</b> 기준이다: <c>minU = minPx / width</c>,
    ///     <c>maxU = (maxPx + 1) / width</c>. 중심 기준으로 재면 잉크가 정확히 한 픽셀 좁아진다.</item>
    ///   <item>좌우 반전(facing)은 <b>여기서 적용하지 않는다</b> — 벡터 도형과 같은
    ///     «바라보는 쪽 +x» 좌표계로 두고 마지막에 렌더러가 부호를 곱한다
    ///     (<c>CharacterAccessoryRenderer</c> 클래스 문서 (2)의 관례와 같다).</item>
    /// </list>
    /// </summary>
    public static class WornSpritePlacement
    {
        /// <summary>
        /// 크기를 안 적은 비트맵의 되메움 폭(R 배수) = <b>머리 지름</b>.
        /// <para>숫자 2에 미학적 근거는 없다 — 아이템은 <see cref="AccessoryDefSO.wornSpriteRectInR"/>에
        /// 자기 크기를 적어야 하고, 이 값이 화면에 나타났다면 그 칸이 비어 있다는
        /// <b>눈에 보이는 신호</b>다. 「모른다」를 「안 그린다」로 바꾸지 않기 위해 있다.</para>
        /// </summary>
        public const float DefaultWidthInR = 2f;

        /// <summary>
        /// 배치 사각형의 되메움. 폭/높이를 안 적었으면 머리 지름 폭에 <b>그림 비율을 유지</b>한다.
        /// </summary>
        /// <param name="canvasAspect">캔버스 높이 ÷ 너비. 0 이하면 정사각으로 본다.</param>
        public static Rect ResolveRectInR(Rect declared, float canvasAspect)
        {
            if (declared.width > 0f && declared.height > 0f) return declared;
            float aspect = canvasAspect > 0.0001f ? canvasAspect : 1f;
            return new Rect(declared.x, declared.y, DefaultWidthInR, DefaultWidthInR * aspect);
        }

        /// <summary>
        /// 정규화된 알파 경계(캔버스 비율)를 배치 사각형 좌표계(R 배수)로 옮긴다.
        /// <para><paramref name="rectInR"/>의 <c>x</c>,<c>y</c>는 캔버스 <b>중심</b>이므로
        /// 정규화 좌표의 원점을 0.5로 옮겨 곱한다.</para>
        /// </summary>
        public static Vector4 InkBoxFromNormalized(Rect rectInR, float minU, float minV, float maxU, float maxV)
        {
            return new Vector4(
                rectInR.x + (minU - 0.5f) * rectInR.width,
                rectInR.y + (minV - 0.5f) * rectInR.height,
                rectInR.x + (maxU - 0.5f) * rectInR.width,
                rectInR.y + (maxV - 0.5f) * rectInR.height);
        }

        /// <summary>
        /// 이 값이 <b>실제로 구워진</b> 박스인가. 기본값(전부 0)과 뒤집힌 값은 「안 구웠다」로 본다.
        /// <para>「안 구웠다」의 뜻은 <b>「잉크가 없다」가 아니라 「모른다」</b>이고, 소비자는 그때
        /// 사각형 전체를 잉크로 본다 — 안전한 방향(여유를 더 주는 쪽)으로 틀린다.
        /// 반대로 했다면(잉크 0으로 본다) 안 구운 아이템이 바닥을 뚫고 화면 밖으로 나간다.</para>
        /// </summary>
        public static bool IsInkBoxBaked(Vector4 box) => box.z > box.x && box.w > box.y;
    }
}
