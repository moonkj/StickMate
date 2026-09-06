using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ <b>「순 색면(net)」 자</b> — 2026-09-06, debugger 규명 [Major-2] 후속(test-engineer 배정).
    ///
    /// ============================================================================
    /// 왜 새 자가 필요한가 — <b>통과한 상태와 안 보이는 상태가 똑같이 생겼다</b>
    /// ============================================================================
    /// 짝 파일(<see cref="AccessoryHatBandAndBellTests"/>)의 옛 띠 검사는 <b>도형의 폭</b>
    /// (gross)을 <see cref="AccessoryFilledBandRuler.SeparationStrokes"/>배와 견준다. 그런데 화면에
    /// 남는 것은 그 폭이 아니라 <b>위아래 경계에 얹힌 획이 먹고 남긴 순 색면</b>이다.
    ///
    /// <para>실측(배율 0.75 · 굽기 근사 846pt/(2×12) = 35.25pt/유닛): 천모자 챙 띠 <c>Piece_B2near</c>는
    /// 총두께 <b>2.123pt</b>라 옛 자로 «2.12획 ≥ 1.5획» 통과였지만, 위를 관 <c>Piece_B0</c>의
    /// <b>닫힘변</b>이(1.0pt 하한 획), 아래를 챙 윤곽 <c>Piece_B2na</c>가 각각 반폭씩 먹어
    /// 실제 색면은 <b>1.123pt</b>였다 — Windows 100%에서 <b>1.12 디바이스 픽셀</b>, 그나마 챙 바깥
    /// 1/3은 0px. 사용자 신고 「갈색 줄이 사라졌다 생겼다」의 실체가 이것이다.
    /// <br/>★ 같은 밤 design-equipment 가 관을 «윤곽 없는 채움 + 닫힘변을 뺀 열린 호(<c>Piece_B0a</c>)»로
    /// 갈라 좌표 한 점 안 옮기고 <b>1.623pt</b>가 됐다. 그 전/후를
    /// <see cref="AccessoryHatBandAndBellTests.지표가_천모자_챙_띠의_옛_잉크를_실제로_잡는다"/>가
    /// <b>살아 있는 좌표로</b> 매 실행 재현한다 — 초록이 된 뒤에도 «이 자가 그것을 잡았다»가
    /// 증명된 채로 남게.</para>
    ///
    /// <para>즉 옛 자는 <b>통과 판정을 내면서 동시에 화면에서 안 보이는 상태</b>를 놓쳤다.
    /// 이 저장소가 반복해서 당한 형태 그대로다 — «실패한 측정과 성공한 측정이 똑같이 생겼다».</para>
    ///
    /// ============================================================================
    /// ★ 경계에 얹힌 획은 <b>선언이 아니라 좌표 일치</b>로 찾는다
    /// ============================================================================
    /// 천모자의 띠 위쪽을 먹는 것은 <b>띠 자신의 윤곽이 아니다</b>(그 조각은 <c>noStroke</c>라
    /// 선을 아예 안 그린다). 먹는 것은 <b>다른 조각</b> — 관 <c>Piece_B0</c>의 닫힘변이고,
    /// 그것이 하필 띠 윗변과 같은 y(+0.663 R)에 있다. 조각 이름·소유권으로 «내 경계의 획»을 찾는
    /// 자는 이 경우를 <b>구조적으로 못 본다</b>. 그래서 이 자는 <b>같은 아이템의 모든 잉크 획</b>을
    /// 열 단위로 훑어 좌표가 일치하는 것을 찾는다.
    /// (그 눈멂 자체를 <see cref="AccessoryHatBandAndBellTests.자_교정_소유권으로_찾는_자는_남의_조각이_먹는_경계를_못_본다"/>가
    /// 답을 아는 도형에서 재현해 못박는다.)
    ///
    /// ============================================================================
    /// ★ 두 문턱을 <b>둘 다</b> 둔다 — 그리고 어느 쪽이 실제로 무는지 숫자로 남긴다
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>가시성 바닥</b> = 1 물리픽셀 @ 최저 지원 DPI(<see cref="OnePhysicalPixelInR"/>).
    ///     근거는 <see cref="StickConfig.MinAccessoryStrokeScreenPoints"/>의 정의 그 자체다 —
    ///     «1pt 는 Windows 100% 에서 디바이스 픽셀 1개 — 그 아래는 없다».</item>
    ///   <item><b>가독 예산</b> = 그 조각이 실제로 쓰는 펜 × <see cref="AccessoryFilledBandRuler.SeparationStrokes"/>.
    ///     규칙 1/4가 «두 선이 구분되려면»에 쓰는 그 값이고, 색면에도 같은 값을 쓴다.</item>
    /// </list>
    /// <para>★ <b>바닥만 걸었다면 천모자를 못 잡았다</b>(처방 전 실측 1.123pt &gt; 바닥 1.000pt).
    /// 실제로 문 것은 예산 쪽이다 — 배정문이 제안한 문턱(«net ≥ 1 물리픽셀») 하나만으로는
    /// 이 결함이 <b>그대로 통과</b>했을 것이라는 뜻이다. 이 사실을 말로 두지 않고
    /// <see cref="AccessoryHatBandAndBellTests.자_교정_1물리픽셀_바닥은_1점5펜_예산보다_약하다"/>가
    /// 매 실행 다시 잰다 — 누가 예산을 지우고 바닥만 남기면 그 검사가 «무엇이 새어 나가는지»를
    /// 숫자로 낸다.</para>
    /// </summary>
    internal static class AccessoryBandNetInkRuler
    {
        internal const string LogPrefix = "[순색면]";

        /// <summary>세로 열을 몇 개 훑는가. 가장 넓은 모자 조각(반폭 1.83 R)에서도 열 간격이
        /// 0.0023 R = 획의 1/75이라, 가장 얇은 띠(0.19 R)의 최댓값을 놓칠 수 없다.</summary>
        private const int Columns = 1601;

        /// <summary>OS 포인트 1개가 몇 R인가(출하 배율). 화면 단위와 R 배수를 잇는 <b>유일한 환산</b>이고,
        /// 프로덕션의 두 예산 함수(<see cref="AccessoryShapeBuilder.StrokeBudgetInHeadRadii"/> ·
        /// <see cref="AccessoryShapeBuilder.FillOutlineBudgetInHeadRadii"/>)가 하한을 R로 옮길 때 쓰는 식과 같다.</summary>
        private static float RPerPoint => 1f / (StickConfig.ReferencePointsPerWorldUnitApprox
            * AccessoryShapeBuilder.BaselineHeadVisualRadius * AccessoryShapeBuilder.ShippingCharacterScale);

        internal static float PointsToR(float points) => points * RPerPoint;

        internal static float RToPoints(float inR) => inR / RPerPoint;

        /// <summary>
        /// 출하 배율에서 <b>1 물리픽셀 @ 최저 지원 DPI</b>가 차지하는 R 배수.
        /// <para>«최저 지원 DPI»는 Windows 표시배율 100%(1pt = 디바이스 픽셀 1개)다 — 그 사실의 정의처가
        /// <see cref="StickConfig.MinAccessoryStrokeScreenPoints"/>의 문서고, 여기서 숫자를 새로 적지 않는다.
        /// macOS Retina 는 같은 1pt 가 2 물리픽셀이라 <b>여유가 두 배</b>다. 즉 이 바닥은 두 OS 중
        /// <b>나쁜 쪽</b>을 기준으로 잡혀 있다(CLAUDE.md 플랫폼 동시 검토).</para>
        /// </summary>
        internal static float OnePhysicalPixelInR => PointsToR(StickConfig.MinAccessoryStrokeScreenPoints);

        /// <summary>
        /// ★ 그 조각이 화면에서 <b>실제로 쓰는 펜</b>(R 배수). 갈래는 셋이고, 근거는 전부 렌더러다.
        /// <list type="bullet">
        ///   <item><b>인계본 조각</b>(<see cref="AccessoryShapeBuilder.Shape.IsHandoff"/>) —
        ///     <c>CharacterAccessoryRenderer.AddLine</c>의 <c>max(handoffWidth, MinAccessoryStrokeWorld)</c>.</item>
        ///   <item><b>v1 채움의 윤곽선</b> — <see cref="AccessoryFilledBandRuler.FillOutlinePenInR"/>.</item>
        ///   <item><b>v1 낱선</b> — <see cref="AccessoryFilledBandRuler.StrokePenInR"/>.</item>
        /// </list>
        /// <para>★ 왜 <see cref="AccessoryFilledBandRuler.PenInR"/>를 그대로 못 쓰는가: 그 자는
        /// <c>Filled</c> 하나로만 갈라서 <b>인계본 조각에 v1 채움 자(1.269pt)를 배정</b>한다.
        /// 실제 인계본 획은 1.000pt라 그 자로는 예산이 <b>27% 과대</b>가 된다. 인계본 계약(§14-1)이
        /// 하한을 2pt→1pt로 분리한 그 날부터 생긴 틈이고, 여기서 메운다.</para>
        /// </summary>
        internal static float PenInR(in AccessoryShapeBuilder.Shape shape)
        {
            if (!shape.IsHandoff) return AccessoryFilledBandRuler.PenInR(shape);

            float scale = Mathf.Max(0.0001f, AccessoryShapeBuilder.ShippingCharacterScale);
            float radius = AccessoryShapeBuilder.BaselineHeadVisualRadius * scale;
            float world = Mathf.Max(shape.StrokeInR * radius,
                StickConfig.MinAccessoryStrokeScreenPoints / StickConfig.ReferencePointsPerWorldUnitApprox);
            return world / radius;
        }

        /// <summary>화면에 실제로 그어지는 획 하나. <b>이름은 진단용</b>이고 판정에는 안 들어간다 —
        /// 판정은 좌표뿐이다.</summary>
        internal readonly struct InkStroke
        {
            internal readonly string Owner;
            internal readonly Vector3[] Points;
            internal readonly bool Loop;
            internal readonly float PenInR;

            internal InkStroke(string owner, Vector3[] points, bool loop, float penInR)
            {
                Owner = owner;
                Points = points;
                Loop = loop;
                PenInR = penInR;
            }
        }

        /// <summary>
        /// 그 아이템이 <b>실제로 긋는 획</b> 전부. 판정 근거는 <see cref="AccessoryShapeBuilder.Shape.NoStroke"/>
        /// 하나 — 렌더러의 두 경로가 같은 하나로 선을 끊는다
        /// (<c>AddShape</c>의 <c>if (shape.NoStroke) return;</c> · <c>ResolveHandoffBody</c>의 <c>hasLine = !s.NoStroke</c>).
        /// <para>선 알파(하이라이트 α0.42 등)는 <b>안 본다</b> — 반투명 획도 그 자리의 색을 흐리므로,
        /// 세는 쪽이 보수적이다. 이 선택이 판정을 바꾸는 자리가 생기면 그때 다시 판단한다.</para>
        /// </summary>
        internal static List<InkStroke> InkOf(IList<AccessoryShapeBuilder.Shape> item)
        {
            var list = new List<InkStroke>();
            if (item == null) return list;
            for (int i = 0; i < item.Count; i++)
            {
                AccessoryShapeBuilder.Shape s = item[i];
                if (s.NoStroke) continue;
                if (s.Points == null || s.Points.Length < 2) continue;
                list.Add(new InkStroke(s.Name, s.Points, s.Loop, PenInR(s)));
            }
            return list;
        }

        /// <summary>한 번의 계측 결과. 실패 메시지가 <b>어디를 어떻게 쟀는지</b>를 그대로 말할 수 있게
        /// 중간값을 전부 들고 나온다 — «몇 획이다»만 남기면 다음 사람이 다시 재야 한다.</summary>
        internal readonly struct Reading
        {
            internal readonly float NetInR, GrossInR, EatTopInR, EatBottomInR, AtXInR, PenInR;
            internal readonly string TopEater, BottomEater;

            internal Reading(float net, float gross, float eatTop, float eatBottom, float atXInR, float pen,
                string topEater, string bottomEater)
            {
                NetInR = net; GrossInR = gross; EatTopInR = eatTop; EatBottomInR = eatBottom;
                AtXInR = atXInR; PenInR = pen; TopEater = topEater; BottomEater = bottomEater;
            }

            internal float NetInStrokes => PenInR > 0f ? NetInR / PenInR : 0f;

            internal string Describe(string label)
                => $"{label}: 총두께 {AccessoryBandNetInkRuler.RToPoints(GrossInR):F3}pt" +
                   $" − 위 {AccessoryBandNetInkRuler.RToPoints(EatTopInR):F3}pt({TopEater ?? "없음"})" +
                   $" − 아래 {AccessoryBandNetInkRuler.RToPoints(EatBottomInR):F3}pt({BottomEater ?? "없음"})" +
                   $" = <b>순 색면 {AccessoryBandNetInkRuler.RToPoints(NetInR):F3}pt</b>" +
                   $" = {NetInStrokes:F3}펜(펜 {AccessoryBandNetInkRuler.RToPoints(PenInR):F3}pt)" +
                   $" · Windows 100% {AccessoryBandNetInkRuler.RToPoints(NetInR):F2}물리px" +
                   $" / macOS Retina {AccessoryBandNetInkRuler.RToPoints(NetInR) * 2f:F2}물리px" +
                   $" · 잰 열 x={AtXInR:F4} R";
        }

        /// <summary>
        /// 채운 도형 <paramref name="band"/>의 <b>순 색면</b>을 잰다 — 세로 열을 훑어 각 열에서
        /// «그 조각이 덮는 가장 두꺼운 구간»을 잡고, 그 구간의 위/아래 끝과 <b>좌표가 일치하는</b>
        /// 잉크 획의 반폭을 뺀 뒤, <b>열 전체의 최댓값</b>을 돌려준다.
        ///
        /// <para><b>왜 최댓값인가</b>: 띠도 챙도 끝으로 갈수록 얇아진다(점으로 수렴하는 것이 옳은 조형이다).
        /// 최솟값을 쓰면 모든 도형이 0으로 읽혀 아무것도 못 가른다. 물어야 할 것은
        /// «이 색이 <b>어딘가에서</b> 읽히는가»이고, 가장 두꺼운 자리가 그 답이다.</para>
        ///
        /// <para><b>왜 한 경계에서 최대만 빼는가</b>(합이 아니라): 같은 경계에 획 둘이 겹쳐 있으면
        /// 잉크는 겹쳐 덮이지 더해지지 않는다. 두 <b>경계</b>의 몫은 서로 더한다.</para>
        /// </summary>
        internal static Reading Measure(in AccessoryShapeBuilder.Rig rig,
            IList<InkStroke> ink, in AccessoryShapeBuilder.Shape band)
        {
            Assert.IsTrue(band.Filled,
                $"{LogPrefix} '{band.Name}'은 채운 도형이 아닙니다 — 색면이 없는 것에 색면 예산을 댈 수 없습니다.");
            Vector3[] pts = band.Points;
            Assert.IsNotNull(pts, $"{LogPrefix} '{band.Name}'에 점이 없습니다.");
            Assert.GreaterOrEqual(pts.Length, 3, $"{LogPrefix} '{band.Name}'이 {pts.Length}점이라 면이 아닙니다.");

            float r = rig.HeadRadius;
            float xMin = float.MaxValue, xMax = float.MinValue;
            for (int i = 0; i < pts.Length; i++)
            {
                xMin = Mathf.Min(xMin, pts[i].x);
                xMax = Mathf.Max(xMax, pts[i].x);
            }

            float pen = PenInR(band);
            float tolWorld = AccessoryFilledBandRuler.CoincidenceInR * r;
            var crossings = new List<float>(32);
            var best = new Reading(-1f, 0f, 0f, 0f, 0f, pen, null, null);

            for (int c = 0; c < Columns; c++)
            {
                float x = Mathf.Lerp(xMin, xMax, c / (float)(Columns - 1));
                if (!ThickestSpan(pts, x, crossings, out float lo, out float hi)) continue;

                float eatTop = 0f, eatBottom = 0f;
                string topEater = null, bottomEater = null;
                for (int s = 0; s < ink.Count; s++)
                {
                    InkStroke stroke = ink[s];
                    float half = stroke.PenInR * 0.5f;
                    if (half <= eatTop && half <= eatBottom) continue;   // 더 굵은 것이 이미 먹었다.
                    CollectY(stroke.Points, stroke.Loop, x, crossings);
                    for (int k = 0; k < crossings.Count; k++)
                    {
                        float y = crossings[k];
                        if (Mathf.Abs(y - hi) <= tolWorld && half > eatTop) { eatTop = half; topEater = stroke.Owner; }
                        if (Mathf.Abs(y - lo) <= tolWorld && half > eatBottom) { eatBottom = half; bottomEater = stroke.Owner; }
                    }
                }

                float gross = (hi - lo) / r;
                float net = Mathf.Max(0f, gross - eatTop - eatBottom);
                if (net > best.NetInR)
                {
                    best = new Reading(net, gross, eatTop, eatBottom, x / r, pen, topEater, bottomEater);
                }
            }

            return best.NetInR < 0f ? new Reading(0f, 0f, 0f, 0f, 0f, pen, null, null) : best;
        }

        /// <summary>
        /// 그 열에서 <b>채움이 덮는 가장 두꺼운 구간</b>. 채움 면은 <c>Loop</c>와 무관하게 닫힌
        /// 다각형이다(<see cref="AccessoryShapeBuilder.BuildFillMesh"/>가 점열을 그대로 삼각분할한다) —
        /// 그래서 여기서는 <b>닫힘변까지</b> 센다.
        /// <para>꼭짓점이 정확히 그 열에 놓일 때 두 번 세지 않도록 <b>반열림 규약</b>
        /// <c>[min, max)</c>을 쓴다. 이 한 줄이 없으면 대칭 도형의 한가운데 열에서 교차 수가 홀수가 되어
        /// 「보조색 없음」으로 읽힌다(실제로 첫 판이 그렇게 틀렸다).</para>
        /// </summary>
        private static bool ThickestSpan(Vector3[] pts, float x, List<float> buffer, out float lo, out float hi)
        {
            CollectY(pts, true, x, buffer);
            buffer.Sort();
            lo = hi = 0f;
            float thickest = 0f;
            for (int i = 0; i + 1 < buffer.Count; i += 2)
            {
                float a = buffer[i], b = buffer[i + 1];
                if (b - a <= thickest) continue;
                thickest = b - a;
                lo = a; hi = b;
            }
            return thickest > 0f;
        }

        /// <summary>그 열을 지나는 변들의 y. <paramref name="loop"/>가 거짓이면 <b>닫힘변은 안 센다</b> —
        /// 열린 낱선은 그 변을 긋지 않기 때문이고, 천모자 관의 처방(닫힘변 잉크 제거)이 바로 이 갈래를 탄다.</summary>
        private static void CollectY(Vector3[] pts, bool loop, float x, List<float> into)
        {
            into.Clear();
            int n = pts.Length;
            int edges = loop ? n : n - 1;
            for (int e = 0; e < edges; e++)
            {
                Vector3 a = pts[e], b = pts[(e + 1) % n];
                if (Mathf.Approximately(a.x, b.x)) continue;
                float min = Mathf.Min(a.x, b.x), max = Mathf.Max(a.x, b.x);
                if (x < min || x >= max) continue;                     // 반열림 [min, max)
                into.Add(Mathf.Lerp(a.y, b.y, (x - a.x) / (b.x - a.x)));
            }
        }

        /// <summary>그 아이템의 <b>보조색 채움</b> 조각들(규칙 3-2의 «형제와 나를 가르는 부분»).
        /// 이름을 적지 않는다 — 역할(<see cref="AccessoryShapeBuilder.Accent"/>)로만 고른다.</summary>
        internal static List<int> AccentFillIndices(IList<AccessoryShapeBuilder.Shape> item)
        {
            var list = new List<int>();
            for (int i = 0; i < item.Count; i++)
            {
                if (item[i].Filled && item[i].Tone == AccessoryShapeBuilder.Accent) list.Add(i);
            }
            return list;
        }

        /// <summary>
        /// <paramref name="band"/>의 <b>윗변 위에 실제로 걸쳐 있는 남의 조각</b>을 <b>좌표로</b> 찾는다
        /// (이름·소유권으로 찾으면 못 보는 그 조각). 못 찾으면 −1.
        /// <para>선을 긋는지 여부는 안 본다 — 「지금은 안 긋지만 좌표는 거기 있다」가 이 함수의 쓸모다
        /// (처방이 들어와 잉크가 사라진 뒤에도 양성 대조가 그 세상을 되살릴 수 있어야 한다).</para>
        /// </summary>
        internal static int NeighbourOnTopEdge(in AccessoryShapeBuilder.Rig rig,
            IList<AccessoryShapeBuilder.Shape> item, in AccessoryShapeBuilder.Shape band, float atXInR)
        {
            float r = rig.HeadRadius;
            float x = atXInR * r;
            var buffer = new List<float>(32);
            if (!ThickestSpan(band.Points, x, buffer, out _, out float hi)) return -1;

            float tol = AccessoryFilledBandRuler.CoincidenceInR * r;
            for (int i = 0; i < item.Count; i++)
            {
                if (item[i].Name == band.Name) continue;
                if (item[i].Points == null || item[i].Points.Length < 2) continue;
                CollectY(item[i].Points, true, x, buffer);
                for (int k = 0; k < buffer.Count; k++)
                {
                    if (Mathf.Abs(buffer[k] - hi) <= tol) return i;
                }
            }
            return -1;
        }
    }

    public sealed partial class AccessoryHatBandAndBellTests
    {
        private const string NetPrefix = AccessoryBandNetInkRuler.LogPrefix;

        /// <summary>
        /// 게이트가 도는 모자 번호. <b>이 배열 하나가 TestCase 목록</b>이고, 바로 아래
        /// <see cref="보조색_색면_게이트가_HEAD_전_종을_덮는다"/>가 이 배열과 카탈로그의 실제 모자 수를
        /// 대조한다 — 7번째 모자가 들어오는 날 <b>여기서 먼저 빨개진다</b>.
        /// <para>★ 옛 띠 검사들이 «중절모·밀짚모자 2건» 목록이었고, 그중 중절모는 인계본이라 건너뛰어
        /// <b>모자 6종 중 실제로 검사되는 것이 1종</b>이었다. 목록을 손으로 적으면서 커버리지를 아무도
        /// 안 세는 것이 그 결함의 뿌리였다.</para>
        /// </summary>
        private static readonly int[] GatedHats =
        {
            AccessoryShapeBuilder.HeadCap,
            AccessoryShapeBuilder.HeadBeanie,
            AccessoryShapeBuilder.HeadFedora,
            AccessoryShapeBuilder.HeadCrown,
            AccessoryShapeBuilder.HeadBeret,
            AccessoryShapeBuilder.HeadStraw,
        };

        // ============================================================================
        // 1. 본 게이트 — 보조색이 화면에서 «읽히는» 색면을 갖는가
        // ============================================================================

        /// <summary>
        /// 모자의 <b>보조색</b>(띠·테·챙 띠)이 화면에서 읽히는 순 색면을 갖는다.
        ///
        /// <para><b>무엇을 재는가</b>: 그 아이템의 보조색 채움 조각을 전부 재고 <b>가장 두꺼운 하나</b>를
        /// 대표로 삼는다. 물음이 «이 모자의 보조색이 어딘가에서 읽히는가»이기 때문이다 — 왕관 보석처럼
        /// 작은 보조색 조각이 여럿인 경우에 이름을 적어 면제하지 않아도 되고(면제표는 썩는다),
        /// 천모자처럼 <b>보조색이 챙 띠 하나뿐</b>인 경우에는 그 하나가 그대로 판정 대상이 된다.</para>
        ///
        /// <para><b>인계본도 함께 돈다</b>. 이 검사가 재는 것은 v1 교리(정원 2~4 · 보조색 정확히 1 ·
        /// 낱선 1.5획)가 아니라 <b>렌더러가 실제로 칠하는 픽셀</b>이라, 계약 v2가 폐지한 규칙과
        /// 겹치지 않는다. 그래서 <see cref="HandoffTestGate"/>를 걸지 않는다 —
        /// 짝 파일의 옛 띠 검사가 중절모·천모자·털모자·왕관 <b>넷을 통째로 못 보던 구멍</b>이 여기서 막힌다.</para>
        /// </summary>
        [TestCaseSource(nameof(GatedHats))]
        public void 모자_보조색은_화면에서_읽히는_순_색면을_갖는다(int item)
        {
            AccessoryShapeBuilder.Rig rig = AccessorySilhouetteMetrics.Rig();
            List<AccessoryShapeBuilder.Shape> hat = AccessorySilhouetteMetrics.Build(rig, EquipmentSlot.Head, item);
            string label = ItemCatalog.Item(EquipmentSlot.Head, item).DisplayName;

            List<int> accents = AccessoryBandNetInkRuler.AccentFillIndices(hat);
            Assert.IsNotEmpty(accents,
                $"{NetPrefix} {label}에 <b>보조색 채움 조각이 하나도 없습니다</b>. 보조색은 이 아이템을 " +
                "형제와 가르는 단 한 부분입니다(37-6 규칙 3-2) — 색면이 얇아 빨개진 검사를 " +
                "«조각을 지워서» 통과시키는 길을 여기서 막습니다.");

            List<AccessoryBandNetInkRuler.InkStroke> ink = AccessoryBandNetInkRuler.InkOf(hat);
            var report = new StringBuilder();
            AccessoryBandNetInkRuler.Reading best = default;
            string bestName = null;

            for (int i = 0; i < accents.Count; i++)
            {
                AccessoryShapeBuilder.Shape piece = hat[accents[i]];
                AccessoryBandNetInkRuler.Reading reading = AccessoryBandNetInkRuler.Measure(rig, ink, piece);
                report.Append("\n    · ").Append(reading.Describe(piece.Name));
                if (bestName == null || reading.NetInStrokes > best.NetInStrokes)
                {
                    best = reading;
                    bestName = piece.Name;
                }
            }

            float budget = best.PenInR * AccessoryFilledBandRuler.SeparationStrokes;
            float floor = AccessoryBandNetInkRuler.OnePhysicalPixelInR;

            Debug.Log($"{NetPrefix} {label} — 대표 조각 '{bestName}' {best.NetInStrokes:F3}펜 " +
                $"(예산 {AccessoryFilledBandRuler.SeparationStrokes:F1}펜 = " +
                $"{AccessoryBandNetInkRuler.RToPoints(budget):F3}pt · 바닥 " +
                $"{AccessoryBandNetInkRuler.RToPoints(floor):F3}pt).{report}");

            Assert.GreaterOrEqual(best.NetInR, floor,
                $"{NetPrefix} {label}의 보조색이 가장 두꺼운 자리에서도 " +
                $"{AccessoryBandNetInkRuler.RToPoints(best.NetInR):F3}pt뿐입니다 — " +
                $"1 물리픽셀({AccessoryBandNetInkRuler.RToPoints(floor):F3}pt @ Windows 100%) <b>아래</b>라 " +
                $"화면에 아예 없습니다.{report}");

            Assert.GreaterOrEqual(best.NetInR, budget,
                $"{NetPrefix} {label}의 보조색 순 색면이 {best.NetInStrokes:F3}펜뿐입니다" +
                $"(예산 {AccessoryFilledBandRuler.SeparationStrokes:F1}펜). " +
                "<b>총두께로 재면 통과하는데 화면에서는 안 보이는</b> 바로 그 상태입니다 — " +
                "경계에 얹힌 획이 위아래에서 색을 먹고 있습니다. 고칠 곳은 띠 두께가 아니라 " +
                "<b>그 경계를 먹는 획</b>일 수 있습니다(먹는 획의 이름이 아래에 있습니다).\n" +
                "★ 선례: 천모자 챙 띠가 정확히 이 형태로 <b>1.123펜</b>이었다(2026-09-06 debugger 규명 " +
                "[Major-1]). 원인은 띠가 얇아서가 아니라 <b>관 조각의 닫힘변</b>이 띠 윗변과 같은 y에 " +
                "놓여 반폭을 먹었기 때문이고, 그 조각을 «윤곽 없는 채움 + 닫힘변을 뺀 열린 호»로 가르자 " +
                "좌표 한 점 안 옮기고 <b>1.623펜</b>이 됐다." + report);
        }

        /// <summary>★ 7번째 모자 안전망 — 위 <see cref="GatedHats"/>가 카탈로그의 모자를 <b>전부</b> 덮는가.
        /// <para>이 검사가 없으면 목록에 안 적힌 모자는 <b>조용히</b> 검사 밖에 남는다. 그것이 이 라운드가
        /// 고치는 결함의 원형이었다(천모자는 TestCase 목록에 아예 없었다).</para></summary>
        [Test]
        public void 보조색_색면_게이트가_HEAD_전_종을_덮는다()
        {
            int count = ItemCatalog.ItemCountIn(EquipmentSlot.Head);
            Assert.AreEqual(count, GatedHats.Length,
                $"{NetPrefix} 모자가 {count}종인데 색면 게이트는 {GatedHats.Length}종만 돕니다 — " +
                "새 모자가 들어왔다면 GatedHats에 번호를 더하십시오.");

            for (int i = 0; i < count; i++)
            {
                Assert.Contains(i, GatedHats,
                    $"{NetPrefix} 모자 {i}번({ItemCatalog.Item(EquipmentSlot.Head, i).DisplayName})이 " +
                    "색면 게이트 목록에 없습니다.");
            }

            // 부재 단언 대조 — 목록에 <b>없는 번호</b>가 남아 있으면 그것도 잡는다(지워진 아이템의 유령).
            for (int i = 0; i < GatedHats.Length; i++)
            {
                Assert.Less(GatedHats[i], count,
                    $"{NetPrefix} 색면 게이트가 없는 모자 {GatedHats[i]}번을 돌고 있습니다.");
                Assert.IsNotNull(ItemCatalog.Item(EquipmentSlot.Head, GatedHats[i]),
                    $"{NetPrefix} 모자 {GatedHats[i]}번이 카탈로그에서 사라졌습니다.");
            }
        }

        // ============================================================================
        // 2. 자 교정 — «이 자가 실제로 그 결함을 잡는가»
        // ============================================================================

        /// <summary>
        /// ★ <b>자 교정(답을 아는 도형)</b> — 잉크 없는 직사각형 띠에서 시작해, 경계에 획을 하나씩
        /// 얹으며 자가 <b>정확히 반폭씩</b> 먹는지 본다. 그리고 <b>1펜 떨어진 획은 안 먹는지</b>도 함께 본다.
        /// <para>이 검사가 없으면 위 본 게이트의 숫자는 «그럴듯한 값»일 뿐이다 — 자가 아무것도 안 먹고
        /// 총두께를 그대로 돌려줘도 초록이고, 반대로 아무 획이나 다 먹어도 초록이다.</para>
        /// </summary>
        [Test]
        public void 자_교정_경계에_얹은_획을_자가_정확히_반폭씩_먹는다()
        {
            AccessoryShapeBuilder.Rig rig = AccessorySilhouetteMetrics.Rig();
            float r = rig.HeadRadius;
            const float thicknessInR = 0.5f;
            const float halfWidthInR = 1.0f;
            float baseY = rig.HeadCenterY;

            var band = new AccessoryShapeBuilder.Shape("교정띠", new[]
            {
                rig.F(-halfWidthInR * r, baseY),
                rig.F(halfWidthInR * r, baseY),
                rig.F(halfWidthInR * r, baseY + thicknessInR * r),
                rig.F(-halfWidthInR * r, baseY + thicknessInR * r),
            }, true, AccessoryShapeBuilder.SortHead, tone: AccessoryShapeBuilder.Accent, filled: true);

            float pen = AccessoryBandNetInkRuler.PenInR(band);
            var noInk = new List<AccessoryBandNetInkRuler.InkStroke>();

            AccessoryBandNetInkRuler.Reading bare = AccessoryBandNetInkRuler.Measure(rig, noInk, band);
            Assert.AreEqual(thicknessInR, bare.NetInR, 1e-4f,
                $"{NetPrefix} 잉크가 하나도 없는데 자가 {bare.NetInR:F5}R로 읽었습니다(기대 {thicknessInR:F5}R) — " +
                "총두께조차 못 재는 자입니다.");
            Assert.AreEqual(thicknessInR, bare.GrossInR, 1e-4f, $"{NetPrefix} 총두께가 틀렸습니다.");

            // (1) 윗변에 <b>남의 조각</b>이 그은 획 — 이름이 다르다는 것이 요점이다.
            var onTop = new List<AccessoryBandNetInkRuler.InkStroke>
            {
                Horizontal(rig, "남의조각_윗변", baseY + thicknessInR * r, halfWidthInR, pen),
            };
            AccessoryBandNetInkRuler.Reading topOnly = AccessoryBandNetInkRuler.Measure(rig, onTop, band);
            Assert.AreEqual(thicknessInR - pen * 0.5f, topOnly.NetInR, 1e-4f,
                $"{NetPrefix} 윗변에 얹은 획 하나를 자가 반폭({pen * 0.5f:F5}R)만큼 안 먹었습니다 " +
                $"(읽은 값 {topOnly.NetInR:F5}R).");
            Assert.AreEqual("남의조각_윗변", topOnly.TopEater,
                $"{NetPrefix} 먹은 획의 주인을 자가 못 짚었습니다 — 진단 메시지가 «어느 조각이 먹는지»를 " +
                "말하지 못하면 다음 사람이 처음부터 다시 재야 합니다.");

            // (2) 위아래 둘 다 — 두 경계의 몫은 <b>서로 더한다</b>.
            var bothSides = new List<AccessoryBandNetInkRuler.InkStroke>(onTop)
            {
                Horizontal(rig, "남의조각_아랫변", baseY, halfWidthInR, pen),
            };
            AccessoryBandNetInkRuler.Reading both = AccessoryBandNetInkRuler.Measure(rig, bothSides, band);
            Assert.AreEqual(thicknessInR - pen, both.NetInR, 1e-4f,
                $"{NetPrefix} 위아래 두 획을 합쳐 한 펜만큼 안 먹었습니다(읽은 값 {both.NetInR:F5}R).");

            // (3) 같은 경계에 획 둘 — 겹쳐 덮이지 <b>더해지지 않는다</b>.
            var doubled = new List<AccessoryBandNetInkRuler.InkStroke>(onTop)
            {
                Horizontal(rig, "겹친획", baseY + thicknessInR * r, halfWidthInR, pen),
            };
            Assert.AreEqual(topOnly.NetInR, AccessoryBandNetInkRuler.Measure(rig, doubled, band).NetInR, 1e-4f,
                $"{NetPrefix} 같은 경계의 획 둘을 <b>합산</b>했습니다 — 잉크는 겹쳐 덮이지 더해지지 않습니다.");

            // (4) 떨어진 획은 안 먹는다 — 자가 «아무거나 다 먹는» 쪽으로 고장 나면 여기서 잡힌다.
            var away = new List<AccessoryBandNetInkRuler.InkStroke>
            {
                Horizontal(rig, "떨어진획", baseY + (thicknessInR + pen) * r, halfWidthInR, pen),
            };
            Assert.AreEqual(thicknessInR, AccessoryBandNetInkRuler.Measure(rig, away, band).NetInR, 1e-4f,
                $"{NetPrefix} 경계에서 1펜({pen:F5}R) 떨어진 획을 자가 먹었습니다 — " +
                "그렇다면 위 숫자들은 «경계에 얹혔는가»가 아니라 «근처에 있는가»를 재고 있습니다.");

            Debug.Log($"{NetPrefix} 자 교정 — 펜 {AccessoryBandNetInkRuler.RToPoints(pen):F3}pt, " +
                $"맨몸 {AccessoryBandNetInkRuler.RToPoints(bare.NetInR):F3}pt / " +
                $"윗변만 {AccessoryBandNetInkRuler.RToPoints(topOnly.NetInR):F3}pt / " +
                $"위아래 {AccessoryBandNetInkRuler.RToPoints(both.NetInR):F3}pt.");
        }

        /// <summary>
        /// ★★ <b>양성 대조 — 소유권으로 찾는 자는 남의 조각이 먹는 경계를 못 본다.</b>
        ///
        /// <para>이 라운드가 고친 결함의 <b>핵심</b>이 이것이다. 천모자 띠의 윗변을 먹는 것은 띠 자신의
        /// 윤곽이 아니라 <b>관 조각의 닫힘변</b>이다. «내 조각이 그은 획»만 보는 자는 그 획을 못 보고
        /// 총두께를 그대로 색면으로 읽는다 — 즉 <b>통과시킨다</b>.</para>
        ///
        /// <para>답을 아는 도형에서 두 자를 나란히 돌려 그 눈멂을 숫자로 못박는다. 이 검사는 프로덕션
        /// 좌표에 의존하지 않으므로 <b>처방이 들어와도 계속 유효</b>하다.</para>
        /// </summary>
        [Test]
        public void 자_교정_소유권으로_찾는_자는_남의_조각이_먹는_경계를_못_본다()
        {
            AccessoryShapeBuilder.Rig rig = AccessorySilhouetteMetrics.Rig();
            float r = rig.HeadRadius;
            float baseY = rig.HeadCenterY;

            // 천모자와 <b>같은 구조</b>: 띠는 스스로 선을 안 긋고(noStroke), 위를 남의 조각이 먹는다.
            float thicknessInR = AccessoryBandNetInkRuler.PointsToR(2.2f);   // 총두께는 예산 위, 색면은 예산 아래가 되게
            var band = new AccessoryShapeBuilder.Shape("띠", new[]
            {
                rig.F(-r, baseY),
                rig.F(r, baseY),
                rig.F(r, baseY + thicknessInR * r),
                rig.F(-r, baseY + thicknessInR * r),
            }, true, AccessoryShapeBuilder.SortHead, tone: AccessoryShapeBuilder.Accent, filled: true,
                strokeInR: 0.13845f, noStroke: true);

            float pen = AccessoryBandNetInkRuler.PenInR(band);
            float budget = pen * AccessoryFilledBandRuler.SeparationStrokes;

            var owned = new List<AccessoryBandNetInkRuler.InkStroke>();            // 소유권 자: 내 조각의 획만
            var scanned = new List<AccessoryBandNetInkRuler.InkStroke>
            {
                Horizontal(rig, "관_닫힘변", baseY + thicknessInR * r, 1f, pen),   // 좌표 자: 남의 획도 본다
                Horizontal(rig, "띠_윤곽", baseY, 1f, pen),
            };

            AccessoryBandNetInkRuler.Reading blind = AccessoryBandNetInkRuler.Measure(rig, owned, band);
            AccessoryBandNetInkRuler.Reading seeing = AccessoryBandNetInkRuler.Measure(rig, scanned, band);

            Assert.GreaterOrEqual(blind.NetInR, budget,
                $"{NetPrefix} 소유권 자가 이 도형을 이미 빨간불로 읽었습니다 — 그러면 이 대조가 공허합니다 " +
                "(총두께를 예산 위로 잡아야 «통과시키는 자»가 됩니다).");
            Assert.Less(seeing.NetInR, budget,
                $"{NetPrefix} 좌표 자도 이 도형을 통과시켰습니다 — 그렇다면 이 라운드의 자 교체는 " +
                "<b>아무것도 새로 잡지 않는다</b>는 뜻입니다.");
            Assert.AreEqual("관_닫힘변", seeing.TopEater,
                $"{NetPrefix} 윗변을 먹은 획의 주인이 '{seeing.TopEater}'로 읽혔습니다 — " +
                "남의 조각이어야 이 대조가 성립합니다.");

            Debug.Log($"{NetPrefix} 양성 대조 — 같은 도형, 소유권 자 {blind.NetInStrokes:F3}펜(<b>통과</b>) vs " +
                $"좌표 자 {seeing.NetInStrokes:F3}펜(<b>불통과</b>). 예산 " +
                $"{AccessoryFilledBandRuler.SeparationStrokes:F1}펜. 이 간격이 곧 옛 게이트가 놓치던 폭이다.");
        }

        /// <summary>
        /// ★ <b>1 물리픽셀 «바닥»만으로는 이 결함을 못 잡는다</b> — 배정문이 제안한 문턱을 그대로 쓰면
        /// 무엇이 새어 나가는지를 숫자로 낸다.
        ///
        /// <para>실측(2026-09-06 처방 전): 천모자 띠의 순 색면은 1.123pt였다. 바닥(1.000pt @ Windows 100%)
        /// 보다 <b>위</b>라 바닥만으로는 <b>통과</b>했을 값이다. 실제로 문 것은 가독 예산(1.5펜 = 1.500pt)
        /// 쪽이다. 그래서 이 파일은 두 문턱을 <b>둘 다</b> 걸고, 그 사이 구간이 실재함을 여기서 증명한다.</para>
        /// </summary>
        [Test]
        public void 자_교정_1물리픽셀_바닥은_1점5펜_예산보다_약하다()
        {
            float floor = AccessoryBandNetInkRuler.OnePhysicalPixelInR;

            // 두 펜 체계(인계본 1.0pt / v1 채움 윤곽선 1.269pt) 각각에서 바닥이 예산보다 약해야 한다.
            float handoffPen = AccessoryBandNetInkRuler.PointsToR(StickConfig.MinAccessoryStrokeScreenPoints);
            float v1FillPen = AccessoryFilledBandRuler.FillOutlinePenInR;

            foreach (float pen in new[] { handoffPen, v1FillPen })
            {
                float budget = pen * AccessoryFilledBandRuler.SeparationStrokes;
                Assert.Less(floor, budget,
                    $"{NetPrefix} 바닥({AccessoryBandNetInkRuler.RToPoints(floor):F3}pt)이 예산" +
                    $"({AccessoryBandNetInkRuler.RToPoints(budget):F3}pt)보다 약하지 않습니다 — " +
                    "그렇다면 두 문턱 중 하나는 아무 일도 하지 않습니다.");

                // 두 문턱 사이 한가운데를 <b>유도</b>한다(숫자를 적으면 상수가 움직일 때 창 밖으로 나간다).
                float probe = (floor + budget) * 0.5f;
                Assert.GreaterOrEqual(probe, floor, $"{NetPrefix} 탐침이 창 아래로 나갔습니다.");
                Assert.Less(probe, budget, $"{NetPrefix} 탐침이 창 위로 나갔습니다.");
            }

            Debug.Log($"{NetPrefix} 문턱 두 개 — 바닥 {AccessoryBandNetInkRuler.RToPoints(floor):F3}pt" +
                $"(1 물리픽셀 @ Windows 100%, macOS Retina 는 같은 값이 2 물리픽셀) / " +
                $"예산 인계본 {AccessoryBandNetInkRuler.RToPoints(handoffPen * AccessoryFilledBandRuler.SeparationStrokes):F3}pt · " +
                $"v1 채움 {AccessoryBandNetInkRuler.RToPoints(v1FillPen * AccessoryFilledBandRuler.SeparationStrokes):F3}pt. " +
                "<b>천모자 챙 띠의 처방 전 실측 1.123pt 가 이 둘 사이</b>에 있었다 — 바닥만 걸었다면 통과했다.");
        }

        /// <summary>
        /// ★★ <b>네거티브 컨트롤 — 이 자가 «그 결함»을 실제로 잡는가.</b>
        ///
        /// <para><b>사건 순서</b>(2026-09-06 밤): 이 자를 짜던 시각의 프로덕션 좌표에서는 관
        /// <c>Piece_B0</c>이 <b>닫힘변까지 그리는 닫힌 획</b>이었고, 그 획이 챙 띠 <c>Piece_B2near</c>의
        /// 윗변(+0.663 R)과 같은 자리라 반폭을 먹었다 — 실측 <b>1.123펜(1.123pt)</b>, 예산 1.5펜
        /// <b>미달</b>. 같은 밤 design-equipment 가 생성기(r19_model.py)를 고쳐 그 조각을
        /// <c>noStroke</c> 채움 + 닫힘변을 뺀 열린 호(<c>Piece_B0a</c>)로 갈랐고, 지금은
        /// <b>1.623펜</b>이다(위 본 게이트가 그 값을 매 실행 다시 잰다).</para>
        ///
        /// <para><b>그래서 이 검사가 필요하다</b>: 고쳐진 뒤에는 본 게이트가 초록이라, «이 자가 정말
        /// 그 결함을 빨간불로 읽었는가»를 아무도 증명하지 않는다. 여기서 <b>살아 있는 좌표로</b>
        /// 옛 세상(관이 닫힘변을 긋던 세상)을 되살려, 자가 그것을 예산 미달로 읽는지 매 실행 확인한다.
        /// 숫자를 박제하지 않으므로 좌표가 또 움직여도 대조는 함께 움직인다.</para>
        ///
        /// <para>★ 그리고 이 검사는 <b>되돌림보다 오래 산다</b> — 누가 생성기를 되돌려
        /// <c>Piece_B0</c>이 다시 닫힘변을 그으면, 되살릴 것이 없어져 «옛 세상 = 지금 세상»이 되고
        /// 본 게이트가 먼저 빨개진다.</para>
        /// </summary>
        [Test]
        public void 지표가_천모자_챙_띠의_옛_잉크를_실제로_잡는다()
        {
            AccessoryShapeBuilder.Rig rig = AccessorySilhouetteMetrics.Rig();
            List<AccessoryShapeBuilder.Shape> hat =
                AccessorySilhouetteMetrics.Build(rig, EquipmentSlot.Head, AccessoryShapeBuilder.HeadCap);

            List<int> accents = AccessoryBandNetInkRuler.AccentFillIndices(hat);
            Assert.IsNotEmpty(accents, $"{NetPrefix} 천모자에 보조색 채움이 없습니다.");

            // 보조색 조각 중 <b>가장 두꺼운</b> 것이 이 모자의 띠다 — 이름을 적지 않는다.
            List<AccessoryBandNetInkRuler.InkStroke> ink = AccessoryBandNetInkRuler.InkOf(hat);
            AccessoryShapeBuilder.Shape band = hat[accents[0]];
            AccessoryBandNetInkRuler.Reading reading = AccessoryBandNetInkRuler.Measure(rig, ink, band);
            for (int i = 1; i < accents.Count; i++)
            {
                AccessoryBandNetInkRuler.Reading candidate = AccessoryBandNetInkRuler.Measure(rig, ink, hat[accents[i]]);
                if (candidate.NetInStrokes <= reading.NetInStrokes) continue;
                reading = candidate;
                band = hat[accents[i]];
            }

            int neighbour = AccessoryBandNetInkRuler.NeighbourOnTopEdge(rig, hat, band, reading.AtXInR);
            Assert.GreaterOrEqual(neighbour, 0,
                $"{NetPrefix} 천모자 띠('{band.Name}') 윗변(x={reading.AtXInR:F4} R)에 걸친 <b>남의 조각</b>을 못 찾았습니다 — " +
                "debugger 규명 [Major-1]은 관 조각의 닫힘변이 그 자리에 있다고 말합니다. 좌표가 바뀌었다면 " +
                "이 검사와 짝인 색면 게이트의 서술도 함께 고쳐야 합니다.");

            AccessoryShapeBuilder.Shape eater = hat[neighbour];
            Assert.AreNotEqual(band.Name, eater.Name,
                $"{NetPrefix} 윗변에 걸친 조각이 띠 자신입니다 — 그렇다면 이 라운드의 전제(«소유권으로 찾으면 " +
                "놓친다»)가 이 아이템에서는 성립하지 않습니다.");

            // ★ 옛 세상 되살리기 — 그 조각이 <b>닫힘변까지 긋던</b> 때를 살아 있는 좌표에서 재현한다.
            //   (지금은 열린 호 Piece_B0a 가 닫힘변만 빼고 같은 자리를 이미 긋고 있으므로,
            //    이 한 줄이 더하는 새 잉크는 <b>닫힘변 하나</b>다.)
            float eaterPen = AccessoryBandNetInkRuler.PenInR(eater);
            var withEater = new List<AccessoryBandNetInkRuler.InkStroke>(ink);
            bool alreadyInked = !eater.NoStroke && eater.Loop;
            if (!alreadyInked)
            {
                withEater.Add(new AccessoryBandNetInkRuler.InkStroke(eater.Name + "(닫힘변 복원)",
                    eater.Points, true, eaterPen));
            }
            AccessoryBandNetInkRuler.Reading inked = AccessoryBandNetInkRuler.Measure(rig, withEater, band);
            float budget = reading.PenInR * AccessoryFilledBandRuler.SeparationStrokes;

            Debug.Log($"{NetPrefix} 천모자 네거티브 컨트롤 — 띠 '{band.Name}'의 윗변에 걸친 남의 조각은 '{eater.Name}'" +
                $"(지금 선을 {(alreadyInked ? "<b>긋는다</b>" : "긋지 않는다")}). " +
                $"\n    지금: {reading.Describe(band.Name)}" +
                $"\n    그 조각이 닫힘변을 그으면: {inked.Describe(band.Name)}" +
                $"\n    예산 {AccessoryFilledBandRuler.SeparationStrokes:F1}펜 = " +
                $"{AccessoryBandNetInkRuler.RToPoints(budget):F3}pt.");

            // (1) 되살린 잉크가 <b>정확히 반폭</b>만큼 색면을 먹는다 — 자가 그 획을 실제로 봤다는 증거.
            Assert.AreEqual(reading.NetInR - eaterPen * 0.5f, inked.NetInR, 1e-4f,
                $"{NetPrefix} 관 조각('{eater.Name}')의 닫힘변을 되살렸는데 색면이 반폭" +
                $"({AccessoryBandNetInkRuler.RToPoints(eaterPen * 0.5f):F3}pt)만큼 줄지 않았습니다 " +
                $"({AccessoryBandNetInkRuler.RToPoints(reading.NetInR):F3} → " +
                $"{AccessoryBandNetInkRuler.RToPoints(inked.NetInR):F3}pt). " +
                "자가 «남의 조각이 그은 획»을 못 보고 있다는 뜻이고, 그러면 이 파일이 잡았다고 " +
                "주장하는 결함을 실제로는 아무도 안 잡습니다.");
            Assert.AreEqual(eater.Name + (alreadyInked ? "" : "(닫힘변 복원)"), inked.TopEater,
                $"{NetPrefix} 윗변을 먹은 획의 주인이 '{inked.TopEater}'로 읽혔습니다 — " +
                $"기대는 관 조각('{eater.Name}')입니다.");

            // (2) 그 세상에서 자는 <b>예산 미달</b>을 낸다 — 「이 자가 그 결함을 잡는다」의 본문.
            Assert.Less(inked.NetInR, budget,
                $"{NetPrefix} 관 닫힘변이 살아 있던 옛 세상의 색면이 {inked.NetInStrokes:F3}펜으로 읽혔습니다 — " +
                $"예산({AccessoryFilledBandRuler.SeparationStrokes:F1}펜) <b>위</b>라 자가 통과시킵니다. " +
                "실측(2026-09-06, 처방 전 프로덕션 좌표)은 1.123펜이었습니다. 챙이 두꺼워졌다면 " +
                "이 대조는 더 이상 아무것도 통제하지 못하므로 <b>다시 저작</b>해야 합니다 — " +
                "지금 그대로 두면 «사실이 아닌 문장에 감싸인 초록»이 남습니다.");
        }

        /// <summary>가로 획 하나(양성 대조용). 좌표만 만들고 판정은 안 한다.</summary>
        private static AccessoryBandNetInkRuler.InkStroke Horizontal(
            in AccessoryShapeBuilder.Rig rig, string owner, float localY, float halfWidthInR, float penInR)
        {
            float r = rig.HeadRadius;
            return new AccessoryBandNetInkRuler.InkStroke(owner, new[]
            {
                rig.F(-halfWidthInR * r, localY),
                rig.F(halfWidthInR * r, localY),
            }, false, penInR);
        }
    }
}
