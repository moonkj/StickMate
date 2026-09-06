using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// ★ 2026-09-01 — <b>글리프가 정수 픽셀로 구워지는가</b>를 판정하는 순수 규칙.
    /// OS 호출이 한 줄도 없다(플랫폼 중립 위치 = <c>Platform/</c>, CLAUDE.md "정책은 중립 위치에").
    ///
    /// ============================================================================
    /// 어떤 신고를 고치는 규칙인가
    /// ============================================================================
    /// 사용자 신고(Windows 실기, 사진 첨부): <b>"여전히 창 겹침현상 텍스트도 다 번져보임"</b>.
    /// 실기 로그 <c>[GLYPH-SCALE]</c>가 원인을 확정했다:
    /// <code>
    ///   [GLYPH-SCALE!번짐] 캔버스 배율=1.500(정수 아님) — 13pt 폰트가 아틀라스에 20px로 구워진 뒤
    ///   0.9750배로 비정수 확대.
    /// </code>
    /// 레거시 uGUI <c>Text</c>는 글리프를 <c>round(pt × 캔버스배율)</c> 픽셀로 <b>한 번 굽고</b>,
    /// 화면에는 <c>pt × 캔버스배율</c> 크기로 올린다. 두 값이 다르면 그 비율만큼 비트맵이 리샘플되고
    /// 획이 이웃 픽셀로 새어 나간다 — 이것이 사용자가 본 "번짐"이다(알파/합성과 무관하다).
    ///
    /// ============================================================================
    /// ★ 고치는 방향이 <b>둘 중 하나뿐</b>인 이유 — 배율은 건드리지 않는다
    /// ============================================================================
    /// 잔차를 0으로 만드는 방법은 원리적으로 두 가지다. 이 저장소는 <b>두 번째만</b> 쓴다.
    ///   (A) 캔버스 배율을 정수로 스냅한다 → <b>금지</b>. Windows의 1.5는 <c>GetDpiForWindow/96</c>
    ///       (디스플레이 150%)에서 오고, 1이나 2로 스냅하면 UI의 <b>물리적 크기가 33% 바뀐다</b>.
    ///       2026-08-31에 이미 해결한 신고("캐릭터창 해상도도 엄청 낮아서 글씨도 잘 안보임")를
    ///       그대로 되살린다. 가장 먼저 떠오르는 답이지만 틀린 답이다.
    ///   (B) <b>폰트 pt를 배율에 맞춘다</b> → 이 클래스. 배율 1.5에서 <c>pt × 1.5</c>가 정수인 pt,
    ///       즉 <b>짝수 pt</b>만 잔차 0으로 구워진다(14pt → 21.0px 정확 / 13pt → 19.5 → 20px에
    ///       구워진 뒤 0.975배). 물리적 크기 변화는 최대 1pt(≈4%)라 이미 해결된 신고를 건드리지 않는다.
    ///
    /// ============================================================================
    /// 이 규칙이 <b>고치지 못하는 것</b>(정직하게 남긴다)
    /// ============================================================================
    /// 짝수 pt는 <see cref="ReferenceCanvasScale"/>(=Windows 150%)를 <b>기준으로</b> 고른 값이다.
    ///   · 배율 1.0 / 2.0(비Retina mac·Windows 100% / Retina mac·Windows 200%) — 모든 정수 pt가
    ///     이미 잔차 0이다. 즉 <b>이 규칙은 macOS에서 아무것도 바꾸지 않는다</b>.
    ///   · 배율 1.25 / 1.75(Windows 125% / 175%) — 정수 픽셀이 되려면 pt가 <b>4의 배수</b>여야 한다.
    ///     짝수 pt 중 절반만 잔차 0이고 나머지는 잔차가 남는다. 여기까지 맞추면 8~24pt 구간에
    ///     8/12/16/20/24 다섯 개만 남아 <b>타이포 계층(Display/Title/Body/Label/Caption)이 붕괴</b>한다 —
    ///     그래서 <b>일부러 맞추지 않았다</b>. 이 두 배율의 잔차는 각각 최대 ±12.5%로 1.5(±2.5%)보다
    ///     크며, 실제 신고가 들어오면 그때는 <see cref="SnapPoints"/>를 UI 생성 시점에 태우는
    ///     런타임 스냅이 후보다(다만 창이 다른 배율 모니터로 옮겨가면 값이 낡는다는 대가가 있다).
    ///
    /// ============================================================================
    /// ★★ 2026-09-06 (dev-platform) — <b>이 규칙은 「캔버스 배율」만 보고 있었다</b>
    /// ============================================================================
    /// <see cref="IsExact(int,float,float)"/>의 인자는 <c>points</c>와 <c>canvasScale</c> 둘뿐이었다.
    /// 그런데 레거시 uGUI <c>Text</c>의 <c>pixelsPerUnit</c>은 <b><c>canvas.scaleFactor</c>만</b> 보고
    /// 아틀라스를 굽는다 — <b>부모 <c>Transform.lossyScale</c>은 쳐다보지 않는다.</b> 그래서 조상 중
    /// 하나에 <c>localScale</c>이 걸리면:
    /// <code>
    ///   아틀라스 픽셀 = round(pt × canvasScale)                 ← transform 항이 <b>없다</b>
    ///   화면 픽셀     = pt × canvasScale × transformScale       ← transform 항이 <b>있다</b>
    ///   리샘플 비     = 화면 / 아틀라스 = transformScale (배율이 정수 pt를 만들 때)
    /// </code>
    /// 즉 <b>배율 항이 한쪽에만 들어간다</b>. 두 인자짜리 판정은 이 비대칭을 원리적으로 볼 수 없어
    /// <b>영원히 "잔차 0"이라고 답했다.</b>
    ///
    /// <para><b>실제 사례(2026-09-06 밤)</b>: 부채꼴 메뉴의 Ø36 축소 폴백이 처음으로 화면에 나왔다.
    /// 그 폴백은 버튼 묶음에 <c>localScale = 36/44 = 0.8181…</c>을 균일하게 건다. 배지 숫자는
    /// 10pt이고 캔버스 배율 1.5에서 <b>15px로 구워진 뒤 12.27px로 축소</b>되어 화면에 올라간다 —
    /// 이 저장소가 "번짐"이라 불러 온 바로 그 현상인데, 진단은 <c>10 × 1.5 = 15</c>만 보고
    /// <b>"리샘플 없음"</b>을 찍고 있었다.</para>
    ///
    /// <para>★ <b>그리고 이 경우 처방이 다르다</b> — pt를 옮겨도 해결되지 않는다.
    /// <c>transformScale = 9/11</c>에서 <c>pt × 1.5 × 9/11</c>이 정수가 되려면 pt가 22의 배수여야 하고,
    /// 타이포 계층에 그런 pt는 없다. 그래서 <see cref="SnapPoints"/>를 권하는 문장을
    /// <b>transform 배율이 1일 때로 제한</b>했다(<c>OverlayCompositionVerdict</c>). 틀린 처방을 내는
    /// 진단은 없는 진단보다 나쁘다 — 이 파일이 이미 한 번 그것으로 팀을 한 라운드 끌고 갔다.</para>
    /// </summary>
    public static class UiGlyphScalePolicy
    {
        /// <summary>
        /// 이 저장소가 <b>글리프 잔차 0을 보장하는 기준 캔버스 배율</b>. 사용자 실기(Windows 디스플레이
        /// 150% = <c>GetDpiForWindow 144 / 96</c>)에서 관측된 값 그대로다.
        ///
        /// <para>★ 테스트는 이 상수를 <b>참조</b>해야 하며 1.5를 숫자로 베끼면 안 된다(CLAUDE.md:
        /// "테스트에 프로덕션 상수를 숫자로 베끼지 않는다"). 그래야 이 값이 바뀌는 날 UI 폰트 감사
        /// 테스트가 자동으로 따라온다 — 예컨대 이 값을 1.25로 올리면 감사는 "4의 배수"를 요구하게 되고
        /// 지금의 짝수 pt들이 즉시 빨갛게 뜬다.</para>
        /// </summary>
        public const float ReferenceCanvasScale = 1.5f;

        /// <summary>"정수로 본다"의 허용 오차. 배율이 1.5f/1.25f처럼 2의 거듭제곱 분수면 부동소수 오차가
        /// 원리적으로 0이라 이 값은 사실상 float 잡음만 흡수한다(1e-3은 pt 1000까지 안전한 여유다).</summary>
        public const float ExactnessEpsilon = 1e-3f;

        /// <summary>스냅이 포기하기 전까지 위/아래로 훑는 최대 pt 거리. 3의 배수(배율 1/3 등)까지는
        /// 이 범위 안에서 반드시 답이 나오고, 답이 없는 무리수 배율에서는 원래 값을 그대로 돌려준다.</summary>
        private const int MaxSnapSearchPoints = 8;

        /// <summary>
        /// <paramref name="canvasScale"/>와 <paramref name="transformScale"/>가 함께 걸린 상태에서
        /// <paramref name="points"/>pt 글리프가 <b>정수 픽셀 격자에 떨어지는가</b>
        /// (<c>pt × canvasScale × transformScale</c>이 정수인가).
        ///
        /// <para>★ 2026-09-06 — 셋째 인자가 <b>기본값 1</b>이라 기존 두 인자 호출부는 한 글자도 바뀌지
        /// 않는다(하위호환). 조상에 <c>localScale</c>이 걸린 표면을 재는 쪽만 셋째 인자를 준다.</para>
        ///
        /// <para><b>이 술어의 범위를 정확히 적어 둔다</b>: 이것은 <b>레이아웃 질문</b>이다 —
        /// "그 글자가 정수 픽셀 자리에 놓이는가". <b>렌더 질문</b>("아틀라스 비트맵이 리샘플되는가")은
        /// <see cref="IsResampleFree"/>가 답한다. <c>transformScale = 1</c>에서 둘은 <b>같은 답</b>을
        /// 내지만(<c>정수_격자와_리샘플없음은_transform_1에서_같은_답을_낸다</c>가 잠근다),
        /// <c>transformScale ≠ 1</c>에서는 <b>정수 격자가 필요조건일 뿐 충분조건이 아니다</b>
        /// (예: 배율 2로 확대하면 픽셀 자리는 정수인데 15px 비트맵이 30px로 늘어난다).
        /// 소스 감사가 쓰는 것은 이쪽이고, 실기 프로브가 쓰는 것은 저쪽이다.</para>
        ///
        /// <para>배율이 0 이하/NaN이면 판정할 수 없으므로 <c>true</c>(무해)로 본다.
        /// <paramref name="transformScale"/> 쪽 미관측(0/NaN)은 <b>1로 정규화</b>한다 —
        /// "안 쟀다"를 "잔차가 있다"로 바꾸면 오탐이 되고, 오탐 한 번이면 아무도 진단을 안 믿는다.</para>
        /// </summary>
        public static bool IsExact(int points, float canvasScale, float transformScale = 1f)
        {
            if (points <= 0) return true;
            if (float.IsNaN(canvasScale) || float.IsInfinity(canvasScale) || canvasScale <= 0f) return true;
            float pixels = points * canvasScale * NormalizeScale(transformScale);
            return Mathf.Abs(pixels - Mathf.Round(pixels)) <= ExactnessEpsilon;
        }

        /// <summary>미관측/불량 배율(0 이하·NaN·무한)을 <b>1</b>로 접는다. 관측하지 못한 항을
        /// 결함으로 바꾸지 않기 위한 단일 규칙 — 프로브·판정기·테스트가 전부 이것을 쓴다
        /// (각자 <c>if (x &lt;= 0) x = 1</c>을 적으면 그중 하나가 반드시 빠진다).</summary>
        public static float NormalizeScale(float scale)
            => float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f ? 1f : scale;

        /// <summary>
        /// 레거시 uGUI가 글리프를 <b>실제로 굽는 픽셀 수</b>: <c>round(pt × canvasScale)</c>.
        ///
        /// <para>★ <b>이 함수에 transform 인자가 없는 것이 이 파일의 핵심 사실이다.</b>
        /// <c>Text.pixelsPerUnit</c>은 <c>canvas.scaleFactor</c>만 읽고 조상의 <c>lossyScale</c>은
        /// 무시한다. 그래서 부모에 <c>localScale</c>이 걸리면 아틀라스는 그대로인 채 메시만 늘거나
        /// 줄고, 그 비율만큼 비트맵이 리샘플된다. 인자를 하나 더 받고 싶어지면 그 순간
        /// <b>버그를 다시 만드는 것</b>이다 — 굽는 쪽은 transform을 모른다.</para>
        /// </summary>
        public static int AtlasPixels(int points, float canvasScale)
        {
            if (points <= 0) return 1;
            if (float.IsNaN(canvasScale) || float.IsInfinity(canvasScale) || canvasScale <= 0f) return Mathf.Max(1, points);
            return Mathf.Max(1, Mathf.RoundToInt(points * canvasScale));
        }

        /// <summary>그 글리프가 <b>화면에서 차지하는 픽셀 수</b>: <c>pt × canvasScale × transformScale</c>.
        /// 아틀라스와 달리 <b>transform 항이 들어간다</b>(<see cref="AtlasPixels"/> 문서 참고).</summary>
        public static float DisplayedPixels(int points, float canvasScale, float transformScale = 1f)
        {
            if (points <= 0) return 0f;
            float cs = float.IsNaN(canvasScale) || float.IsInfinity(canvasScale) || canvasScale <= 0f ? 1f : canvasScale;
            return points * cs * NormalizeScale(transformScale);
        }

        /// <summary>아틀라스 비트맵이 화면에 올라갈 때 걸리는 <b>리샘플 비</b>
        /// (<see cref="DisplayedPixels"/> ÷ <see cref="AtlasPixels"/>). 1이면 픽셀 대 픽셀,
        /// 1이 아니면 그 비율만큼 획이 이웃 픽셀로 샌다 = 사용자가 신고한 "번짐".</summary>
        public static float ResampleRatio(int points, float canvasScale, float transformScale = 1f)
        {
            if (points <= 0) return 1f;
            return DisplayedPixels(points, canvasScale, transformScale) / AtlasPixels(points, canvasScale);
        }

        /// <summary><b>렌더 질문</b>: 이 조합에서 글리프 비트맵이 리샘플되지 않는가.
        /// <see cref="IsExact(int,float,float)"/>(레이아웃 질문)와의 차이는 그 문서에 적어 두었다.</summary>
        public static bool IsResampleFree(int points, float canvasScale, float transformScale = 1f)
        {
            if (points <= 0) return true;
            if (float.IsNaN(canvasScale) || float.IsInfinity(canvasScale) || canvasScale <= 0f) return true;
            return Mathf.Abs(ResampleRatio(points, canvasScale, transformScale) - 1f) <= ExactnessEpsilon;
        }

        /// <summary>
        /// 관측한 <paramref name="lossyScale"/>에서 <b>순수 transform 성분</b>만 뽑는다
        /// (<c>lossyScale ÷ canvas.scaleFactor</c>).
        ///
        /// <para><b>왜 나누는가</b>: <c>RectTransform.lossyScale</c>에는 캔버스 자신의 배율이
        /// <b>이미 곱해져</b> 있다(Screen Space 캔버스의 루트가 <c>scaleFactor</c>를 스케일로 건다).
        /// 그걸 그대로 <c>transformScale</c>에 넣으면 캔버스 배율이 <b>두 번</b> 곱해져
        /// 배율 1.5짜리 정상 화면이 통째로 "리샘플 있음"으로 뜬다 — 오탐 폭탄이다.</para>
        ///
        /// <para>이 함수가 <c>Transform</c>이 아니라 <b>float 두 개</b>를 받는 이유: 그래야
        /// EditMode 테스트가 씬 오브젝트 없이 규칙만 전수 검증할 수 있다. 씬에서 <c>lossyScale</c>과
        /// <c>canvas.scaleFactor</c>를 읽는 것은 플랫폼 프로브의 <b>사실 조회</b> 몫이다(CLAUDE.md).</para>
        /// </summary>
        public static float PureTransformScale(float lossyScale, float canvasScaleFactor)
        {
            if (float.IsNaN(lossyScale) || float.IsInfinity(lossyScale) || lossyScale <= 0f) return 1f;
            if (float.IsNaN(canvasScaleFactor) || float.IsInfinity(canvasScaleFactor) || canvasScaleFactor <= 0f) return 1f;
            return lossyScale / canvasScaleFactor;
        }

        /// <summary>그 transform 배율이 <b>사실상 1</b>인가(= 이 표면에는 조상 스케일이 걸려 있지 않다).
        /// 판정 문구가 "pt를 옮기세요"를 권해도 되는지를 가르는 스위치다.</summary>
        public static bool IsTransformScaleNeutral(float transformScale)
            => Mathf.Abs(NormalizeScale(transformScale) - 1f) <= ExactnessEpsilon;

        /// <summary><see cref="ReferenceCanvasScale"/>에서의 <see cref="IsExact(int,float,float)"/>.
        /// 소스 감사 테스트가 쓰는 진입점이다.
        /// <para>★ 2026-09-06 — <b>transform 배율은 여기에 들어오지 않는다</b>. 소스 감사는 "그 pt가
        /// 소스에 적혀 있다"만 볼 수 있고, 그 글자가 <b>런타임에 어떤 스케일 아래로 들어가는지는</b>
        /// 정적으로 알 수 없기 때문이다. 그 사각지대는 실기 프로브
        /// (<c>OverlayCompositionVerdict</c>의 GLYPH-SCALE 줄)가 덮는다.</para></summary>
        public static bool IsExactAtReferenceScale(int points) => IsExact(points, ReferenceCanvasScale);

        /// <summary>
        /// <paramref name="points"/>에서 가장 가까운 "잔차 0" pt. 같은 거리면 <b>큰 쪽</b>을 고른다 —
        /// 글자를 줄이는 쪽으로 기울면 가독성 신고(2026-08-31 "글씨가 잘 안 보임")를 조금씩 되살리기 때문이다.
        /// 이미 잔차가 0이면 <b>그대로</b> 돌려주므로 배율 1/2(macOS)에서는 항등 함수다.
        /// </summary>
        public static int SnapPoints(int points, float canvasScale)
        {
            if (points <= 0) return points;
            if (float.IsNaN(canvasScale) || float.IsInfinity(canvasScale) || canvasScale <= 0f) return points;
            for (int d = 0; d <= MaxSnapSearchPoints; d++)
            {
                int up = points + d;
                if (IsExact(up, canvasScale)) return up;
                int down = points - d;
                if (down > 0 && IsExact(down, canvasScale)) return down;
            }
            return points;   // 이 배율에서는 근처에 정확한 크기가 없다 — 원래 값을 유지한다.
        }

        /// <summary>이 배율에서 잔차 0인 pt들의 간격(1이면 모든 정수 pt가 안전, 2면 짝수만, 4면 4의 배수만).
        /// 사람이 읽는 진단 문구와 감사 테스트의 실패 메시지에 쓴다. 답을 못 찾으면 0을 돌려준다.</summary>
        public static int ExactPointStep(float canvasScale)
        {
            if (float.IsNaN(canvasScale) || float.IsInfinity(canvasScale) || canvasScale <= 0f) return 1;
            for (int step = 1; step <= 16; step++)
            {
                if (IsExact(step, canvasScale)) return step;
            }
            return 0;
        }
    }
}
