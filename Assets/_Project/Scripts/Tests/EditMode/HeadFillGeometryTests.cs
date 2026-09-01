using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-01 그림체 전환 P1 — <b>"머리를 채웠는데 머리 크기는 1pt도 안 변한다"</b>의 오프라인 검산
    /// (docs/UX_FLOW.md 38-4-1).
    ///
    /// ============================================================================
    /// 무엇을 재는가
    /// ============================================================================
    /// 머리 채움(<c>HeadFill</c>)은 <b>폭이 지름만 한 닫힌 원 경로</b> 하나다. 폭 W인 원 경로(경로반경 r)가
    /// 덮는 반경 구간은 <c>[r − W/2, r + W/2]</c>이므로,
    /// <code>
    ///   (a) 바깥이 정확히 R      :  r + W/2 = R
    ///   (b) 안쪽까지 완전히 채움 :  r − W/2 ≤ 0
    ///   W = k·r 로 두면  r = R / (1 + k/2),  W = k·r      (k &gt; 2 여야 (b)에 여유가 생긴다)
    /// </code>
    /// 이 파일은 그 부등식을 <b>배포 상수로 직접</b> 검산한다. 상수를 손으로 옮겨 적지 않고
    /// <b>C# 소스에서 파싱</b>하는 이유는 하나다 — 누군가 <c>SceneBootstrapper</c>의 값을 바꾸면 이 테스트가
    /// <b>바뀐 값으로</b> 다시 검산해야 하고, 그래야 "테스트만 옛 숫자를 알고 통과하는" 상태가 불가능해진다.
    ///
    /// ============================================================================
    /// 왜 EditMode(오프라인)인가 — 그리고 무엇을 못 재는가(정직한 한계)
    /// ============================================================================
    /// 여기서 재는 것은 <b>수식과 상수</b>다. "실제로 구워진 프리팹이 이 수식대로인가"는 이 파일이
    /// 재지 못하고 <c>Tests/PlayMode/CharacterHeadFillTests</c>가 실제 씬에서 잰다. 두 파일이 짝이다.
    /// </summary>
    public sealed class HeadFillGeometryTests
    {
        private const string LogPrefix = "[머리채움-TEST]";

        private static string BootstrapperPath =>
            Path.Combine(Application.dataPath, "Editor", "SceneBootstrapper.cs");

        private static string PortraitPath =>
            Path.Combine(Application.dataPath, "_Project", "Scripts", "Interaction", "CharacterPortraitStage.cs");

        /// <summary>C# 소스에서 <c>이름 = 숫자f</c> 형태의 상수 하나를 읽는다.</summary>
        private static float ReadConst(string filePath, string name)
        {
            Assert.IsTrue(File.Exists(filePath), $"{LogPrefix} 소스 파일을 찾지 못했습니다: {filePath}");
            string text = File.ReadAllText(filePath);
            var m = Regex.Match(text, Regex.Escape(name) + @"\s*=\s*(-?[0-9]*\.?[0-9]+)f");
            Assert.IsTrue(m.Success,
                $"{LogPrefix} {Path.GetFileName(filePath)}에서 상수 '{name}'을 찾지 못했습니다 — " +
                "이름이 바뀌었다면 이 테스트도 함께 갱신해야 합니다(그 전까지는 검산이 무의미해집니다).");
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        private static int ReadIntConst(string filePath, string name)
        {
            string text = File.ReadAllText(filePath);
            var m = Regex.Match(text, Regex.Escape(name) + @"\s*=\s*([0-9]+)\s*;");
            Assert.IsTrue(m.Success, $"{LogPrefix} 상수 '{name}'을 찾지 못했습니다.");
            return int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        /// <summary>배포 상수로 유도한 (경로반경, 폭).</summary>
        private static void Derive(float outerRadius, float k, out float pathRadius, out float width)
        {
            pathRadius = outerRadius / (1f + k * 0.5f);
            width = pathRadius * k;
        }

        // ============================================================================
        // (1) 절대 조건 — 바깥 반경이 정확히 R이다 (= 머리 크기 무변경)
        // ============================================================================
        [Test]
        public void FilledDiscOuterRadiusEqualsHeadRadiusExactly()
        {
            float k = ReadConst(BootstrapperPath, "FilledDiscWidthPerPathRadius");
            float r0 = ReadConst(BootstrapperPath, "BaselineHeadVisualRadius");

            // 배율 전 구간에서 확인한다 — 유도식이 배율에 선형이라는 사실 자체를 잠근다.
            float[] scales = { StickConfig.MinCharacterScale, 0.5f, 0.75f, 1f, StickConfig.MaxCharacterScale };
            foreach (float s in scales)
            {
                float R = r0 * s;
                Derive(R, k, out float pathRadius, out float width);
                float outer = pathRadius + width * 0.5f;

                Assert.AreEqual(R, outer, R * 1e-6f,
                    $"{LogPrefix} 배율 {s:F2}에서 채움 바깥 반경이 {outer:F6}으로 머리 반경 {R:F6}과 다릅니다 — " +
                    "머리 크기가 변합니다(사용자 요구: '머리를 채우되 크기는 그대로').");
            }

            Debug.Log($"{LogPrefix} 바깥 반경 = R 확인 — k={k:F2}, r=R/{1f + k * 0.5f:F2}={1f / (1f + k * 0.5f):F4}R, " +
                $"W={k / (1f + k * 0.5f):F4}R.");
        }

        // ============================================================================
        // (2) 완전 채움 — 안쪽 가장자리가 중심을 지나쳐야 바늘구멍이 안 남는다
        // ============================================================================
        [Test]
        public void FilledDiscHasNoPinholeAtCenter()
        {
            float k = ReadConst(BootstrapperPath, "FilledDiscWidthPerPathRadius");
            float r0 = ReadConst(BootstrapperPath, "BaselineHeadVisualRadius");

            Assert.Greater(k, 2f,
                $"{LogPrefix} 폭/경로반경 비 k={k:F3}이 2.0 이하입니다 — 안쪽 정점 {ReadIntConst(BootstrapperPath, "HeadRingSegments")}개가 " +
                "중심 한 점에 겹치거나 못 미쳐 머리 한가운데에 구멍/별 모양이 남을 수 있습니다.");

            Derive(r0, k, out float pathRadius, out float width);
            float innerEdge = pathRadius - width * 0.5f;
            Assert.Less(innerEdge, 0f,
                $"{LogPrefix} 채움 안쪽 가장자리가 {innerEdge:F6}(≥0)입니다 — 머리 중심이 비어 있게 됩니다.");

            // 네거티브 컨트롤 — "이론 최소" k=2.0이었다면 안쪽 가장자리가 정확히 0이 되어 여유가 0이다.
            Derive(r0, 2f, out float p2, out float w2);
            Assert.AreEqual(0f, p2 - w2 * 0.5f, 1e-7f,
                $"{LogPrefix} 네거티브 컨트롤이 성립하지 않습니다 — k=2.0에서 안쪽 가장자리는 정확히 0이어야 합니다.");

            Debug.Log($"{LogPrefix} 완전 채움 확인 — 안쪽 가장자리 {innerEdge / r0:F4}R(중심을 지나침), " +
                $"이론 최소 k=2.0에서는 정확히 0R(여유 없음).");
        }

        // ============================================================================
        // (3) ★ 화면상 최소 획 하한과 채움의 관계 — 기준 디스플레이에서는 걸리지 않는다
        // ============================================================================
        // StickmanAgent.ApplyStrokeWidthsForScale은 배율이 바뀔 때마다 모든 LineRenderer의 폭에
        // 화면상 2pt 하한(StickConfig.MinStrokeScreenPoints)을 건다. 하한의 월드 값은 **실행 환경마다
        // 다르다**(화면 높이 ÷ 직교 크기로 실측한다 — ResolveMinStrokeWorldWidth). 여기서는 프리팹을
        // 구울 때 쓰는 **기준 디스플레이**(846pt) 기준으로 여유를 확인한다.
        //
        // ★ 실측으로 확인한 사실(2026-09-01): 배치 모드 헤드리스 화면(480pt)에서는 하한이 0.100유닛이
        //   되어 최소 배율에서 **실제로 구속한다**. 그래도 머리는 커지지 않는다 — 왜인지는 아래 (3')이
        //   부등식으로 증명한다. 그래서 이 테스트는 "여유 확인"이고, **안전성의 근거는 (3')**이다.
        [Test]
        public void FillWidthHasHeadroomOverTheStrokeFloorOnTheReferenceDisplay()
        {
            float k = ReadConst(BootstrapperPath, "FilledDiscWidthPerPathRadius");
            float r0 = ReadConst(BootstrapperPath, "BaselineHeadVisualRadius");
            float floorWorld = StickConfig.MinStrokeScreenPoints / StickConfig.ReferencePointsPerWorldUnitApprox;

            Derive(r0 * StickConfig.MinCharacterScale, k, out _, out float widthAtMin);

            Assert.Greater(widthAtMin, floorWorld,
                $"{LogPrefix} 기준 디스플레이에서 최소 배율 {StickConfig.MinCharacterScale:F2}의 채움 폭 " +
                $"{widthAtMin:F5}유닛이 획 하한 {floorWorld:F5}유닛 이하입니다.");

            // 이 부등식이 공허하지 않다는 증거 — 하한이 구속력을 갖기 시작하는 배율이 실제로 존재하고,
            // 그 배율이 사용자가 쓸 수 있는 구간(MinCharacterScale) '아래'에 있다.
            float bindingScale = floorWorld / (r0 * k / (1f + k * 0.5f));
            Assert.Less(bindingScale, StickConfig.MinCharacterScale,
                $"{LogPrefix} 하한이 구속하기 시작하는 배율({bindingScale:F3})이 최소 배율 " +
                $"{StickConfig.MinCharacterScale:F2} 이상입니다.");
            Assert.Greater(bindingScale, 0f,
                $"{LogPrefix} 네거티브 컨트롤 계산이 0 이하입니다 — 검산식이 틀렸습니다.");

            Debug.Log($"{LogPrefix} 기준 디스플레이 여유 — 최소 배율 폭 {widthAtMin:F5} vs 하한 {floorWorld:F5} " +
                $"(여유 {(widthAtMin / floorWorld - 1f) * 100f:F0}%). 하한이 구속하는 배율은 {bindingScale:F3} " +
                $"= 다이얼 최소({StickConfig.MinCharacterScale:F2})보다 아래.");
        }

        // ============================================================================
        // (3') ★★ 하한이 아무리 세게 걸려도 머리는 커지지 않는다 — 무조건 성립하는 부등식
        // ============================================================================
        // 머리의 바깥 실루엣을 정하는 것은 **링**이다(바깥 가장자리 = R + W_ring/2). 채움이 그보다
        // 밖으로 나가지 않는 한 머리 크기는 정의상 변하지 않는다. 하한 F는 **채움과 링에 똑같이**
        // 걸리므로, 경로 반경이 더 작은 쪽(채움: R/2.2 &lt; R)이 하한 때문에 상대적으로 커지는 일이
        // 생길 수 없다. 아래는 그 두 부등식을 F를 넓은 범위로 훑으며 실제로 확인한 것이다
        // (헤드리스 실측값 0.100, 기준 디스플레이 0.0567, 그리고 비현실적으로 큰 값까지).
        [Test]
        public void FillNeverEscapesTheRingUnderAnyStrokeFloor()
        {
            float k = ReadConst(BootstrapperPath, "FilledDiscWidthPerPathRadius");
            float r0 = ReadConst(BootstrapperPath, "BaselineHeadVisualRadius");
            float ringWidth0 = 0.09f * 0.7f;   // BaselineHeadOutlineWidth = 0.09 * LineWidthScale(0.7).

            float[] floors = { 0f, 0.02f, 0.056737f, 0.1f, 0.2f, 0.5f };
            float[] scales = { StickConfig.MinCharacterScale, 0.5f, 0.75f, 1f, StickConfig.MaxCharacterScale };

            foreach (float F in floors)
            {
                foreach (float s in scales)
                {
                    float R = r0 * s;
                    Derive(R, k, out float fillPath, out float fillDesignWidth);

                    float wFill = Mathf.Max(fillDesignWidth, F);
                    float wRing = Mathf.Max(ringWidth0 * s, F);

                    float fillOuter = fillPath + wFill * 0.5f;
                    float ringOuter = R + wRing * 0.5f;

                    // (i) 채움이 머리 바깥 실루엣을 넘지 않는다 = 머리가 커지지 않는다.
                    Assert.LessOrEqual(fillOuter, ringOuter + 1e-6f,
                        $"{LogPrefix} 하한 F={F:F4}, 배율 {s:F2}에서 채움 바깥({fillOuter:F5})이 " +
                        $"머리 바깥 실루엣({ringOuter:F5})을 넘었습니다 — 머리가 커집니다.");

                    // (ii) 채움이 최소한 링의 중심선까지는 닿는다 = 채움과 링 사이에 틈이 없다.
                    Assert.GreaterOrEqual(fillOuter, R - 1e-6f,
                        $"{LogPrefix} 하한 F={F:F4}, 배율 {s:F2}에서 채움 바깥({fillOuter:F5})이 " +
                        $"링 중심선({R:F5})에 못 미칩니다 — 얼굴 가장자리에 빈 고리가 생깁니다.");

                    // (iii) 중심에 구멍이 없다.
                    Assert.LessOrEqual(fillPath - wFill * 0.5f, 0f,
                        $"{LogPrefix} 하한 F={F:F4}, 배율 {s:F2}에서 채움 안쪽 가장자리가 0을 넘습니다.");
                }
            }

            Debug.Log($"{LogPrefix} 무조건 안전 확인 — 하한 {floors.Length}종 × 배율 {scales.Length}종 " +
                $"= {floors.Length * scales.Length}조합 전부에서 " +
                "(채움 바깥 ≤ 링 바깥) ∧ (채움 바깥 ≥ 링 중심선) ∧ (안쪽 가장자리 ≤ 0).");
        }

        // ============================================================================
        // (4) 다각형 근사 오차가 링 두께 안에 묻힌다
        // ============================================================================
        [Test]
        public void PolygonApproximationErrorIsInvisible()
        {
            float k = ReadConst(BootstrapperPath, "FilledDiscWidthPerPathRadius");
            float r0 = ReadConst(BootstrapperPath, "BaselineHeadVisualRadius");
            int segments = ReadIntConst(BootstrapperPath, "HeadRingSegments");

            Derive(r0, k, out float pathRadius, out _);
            // 변 중앙에서 경로가 안쪽으로 들어가는 양(= 다각형의 변심거리 부족분).
            float inset = pathRadius * (1f - Mathf.Cos(Mathf.PI / segments));
            float insetPoints = inset * StickConfig.ReferencePointsPerWorldUnitApprox * 0.75f; // 출하 배율.

            Assert.Less(inset, r0 * 0.01f,
                $"{LogPrefix} 다각형 근사 오차 {inset / r0:P2}가 머리 반경의 1%를 넘습니다(세분화 {segments}각형).");
            Assert.Less(insetPoints, 0.25f,
                $"{LogPrefix} 근사 오차가 출하 배율에서 {insetPoints:F3}pt로 육안에 들어옵니다.");

            Debug.Log($"{LogPrefix} 근사 오차 — {segments}각형에서 {inset / r0:P2}R = 출하 배율 {insetPoints:F3}pt " +
                "(머리 링 두께 2pt 안에 완전히 묻힘).");
        }

        // ============================================================================
        // (5) 실제 캐릭터와 초상화가 같은 유도식을 쓴다 (이중 정의 방지)
        // ============================================================================
        // 어셈블리가 달라(Editor vs Runtime) 상수를 공유할 수 없으므로 두 곳에 같은 값이 있다.
        // 그 '같음'을 사람 기억이 아니라 테스트가 잠근다 — docs/UX_FLOW.md 38-12 #10이 지적한
        // "초상화가 몸과 다른 획을 쓰고 있었다"와 정확히 같은 유형의 결함을 미리 막는 것이다.
        [Test]
        public void PortraitUsesTheSameFilledDiscRatioAsTheRealCharacter()
        {
            float body = ReadConst(BootstrapperPath, "FilledDiscWidthPerPathRadius");
            float portrait = ReadConst(PortraitPath, "FilledDiscWidthPerPathRadius");

            Assert.AreEqual(body, portrait, 1e-6f,
                $"{LogPrefix} 실제 캐릭터({body:F3})와 초상화({portrait:F3})의 채움 비율이 다릅니다 — " +
                "초상화 머리만 다른 크기/모양이 됩니다(이중 정의).");

            Debug.Log($"{LogPrefix} 채움 비율 일치 확인 — 실제 {body:F2} == 초상화 {portrait:F2}.");
        }
    }
}
