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

        /// <summary><paramref name="canvasScale"/>에서 <paramref name="points"/>pt 글리프가
        /// <b>정수 픽셀로 구워지는가</b>(= 아틀라스 픽셀과 표시 픽셀이 같아 리샘플이 없는가).
        /// 배율이 0 이하/NaN이면 판정할 수 없으므로 <c>true</c>(무해)로 본다.</summary>
        public static bool IsExact(int points, float canvasScale)
        {
            if (points <= 0) return true;
            if (float.IsNaN(canvasScale) || float.IsInfinity(canvasScale) || canvasScale <= 0f) return true;
            float pixels = points * canvasScale;
            return Mathf.Abs(pixels - Mathf.Round(pixels)) <= ExactnessEpsilon;
        }

        /// <summary><see cref="ReferenceCanvasScale"/>에서의 <see cref="IsExact(int,float)"/>.
        /// 소스 감사 테스트가 쓰는 진입점이다.</summary>
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
