using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ <b>M6 — 화면상 하한을 역할로 쪼갠다</b>의 회귀 잠금
    /// (2026-09-02, docs/CHARACTER_FORM_SPEC.md 16~26절. 사용자 신고
    /// <i>"각 장비별 리디자인 한거 맞아? 아직도 전부다 조잡한데"</i>의 직접 처방).
    ///
    /// ============================================================================
    /// 무엇이 문제였나 — 이미 제품 안에 A/B가 있었다
    /// ============================================================================
    /// 채운 도형은 <b>채움 + 윤곽선(채움색 × 0.62)</b>으로 그려지고, 폭 W인 펜이 경계에 <b>중심</b>을
    /// 두므로 도형은 안쪽으로 정확히 W/2를 잃는다. 같은 좌표를 쓰는 두 그림이 제품 안에 이미 있었고
    /// 결과가 3.3배 달랐다:
    /// <code>
    ///   인벤토리 카드 44px (하한 없음)   색면 생존율 중앙값 72.5%
    ///   착용 그림 배율 0.60 (하한 2.00pt) 색면 생존율 중앙값 22.0%
    /// </code>
    /// <b>좌표가 원인이면 카드도 조잡해야 한다. 원인은 펜이었다.</b>
    /// 그래서 리디자인이 카드에서는 보이고 착용 그림에서는 보이지 않았다.
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 것 — 특히 <b>조용한 무효화</b>
    /// ============================================================================
    /// 값을 고치는 것만으로는 부족하다. <c>StickmanAgent.ApplyStrokeWidthsForScale</c>이
    /// <b>두 경로</b>에서 모든 선을 하한으로 되올리므로, 한 경로만 고치면 렌더러가 1.00pt로 그린 직후
    /// 되돌아간다 — <b>화면은 하나도 안 바뀌는데 테스트는 초록</b>이다.
    /// 실행 단언은 <c>Tests/PlayMode/CharacterScaleRuntimeTests</c>의
    /// <c>NegativeControl_M6_되올리기_두_경로가...</c>가 맡고, 이 파일은 그 구조가
    /// <b>소스에서</b> 유지되는지를 본다(두 짝이 함께 있어야 한 쪽이 지워져도 드러난다).
    /// </summary>
    public sealed class FillOutlineStrokeFloorTests
    {
        private const string LogPrefix = "[M6-하한분리-TEST]";

        private static string ScriptsRoot => Path.Combine(Application.dataPath, "_Project", "Scripts");
        private static string AgentPath => Path.Combine(ScriptsRoot, "Core", "StickmanAgent.cs");
        private static string AccessoryRendererPath =>
            Path.Combine(ScriptsRoot, "Interaction", "CharacterAccessoryRenderer.cs");
        private static string DiagnosticsPath =>
            Path.Combine(ScriptsRoot, "Platform", "StrokeWidthDiagnostics.cs");
        private static string CardIconPath =>
            Path.Combine(ScriptsRoot, "Interaction", "AccessoryCardIcon.cs");

        private static string Read(string path)
        {
            Assert.IsTrue(File.Exists(path), $"{LogPrefix} 소스 파일을 찾지 못했습니다: {path} — " +
                "경로가 바뀌면 아래 스캔이 <b>아무것도 안 보고</b> 통과합니다(거짓 초록).");
            return File.ReadAllText(path);
        }

        // ============================================================================
        // 1. 두 하한의 관계와 유도 — 숫자가 아니라 <b>부등식</b>을 잠근다
        // ============================================================================

        [Test]
        public void 채움_경계선_하한은_낱선_하한보다_낮고_1물리픽셀_이상이다()
        {
            Assert.Less(StickConfig.MinFillOutlineScreenPoints, StickConfig.MinStrokeScreenPoints,
                $"{LogPrefix} 채움 경계선 하한({StickConfig.MinFillOutlineScreenPoints}pt)이 낱선 하한" +
                $"({StickConfig.MinStrokeScreenPoints}pt)보다 낮지 않습니다 — M6이 무효화됐습니다. " +
                "이 둘이 같아지면 채움 61개가 다시 자기 윤곽선에 색면을 잃습니다.");

            // ★ 아래 한계 — 이 값을 정한 것은 macOS가 아니라 <b>Windows</b>다.
            //   지원하는 가장 낮은 픽셀 밀도는 Windows 표시배율 100%(dpiScale = 1)이고,
            //   거기서 1.00pt = 1.00 물리픽셀이다. 그보다 낮추면 경계가 소실되고
            //   AccessoryShapeBuilder.FillOutlineColor가 존재하는 이유 자체가 무너진다.
            //   macOS Retina에서는 1.00pt가 2물리픽셀이라 0.5pt도 멀쩡해 보인다 —
            //   맥 캡처만 보고 면제를 고르면 Windows에서 경계가 사라진다.
            const float windowsHundredPercentDpiScale = 1f;
            float physicalPixelsAtWindows100 =
                StickConfig.MinFillOutlineScreenPoints / windowsHundredPercentDpiScale;
            Assert.GreaterOrEqual(physicalPixelsAtWindows100, 1f,
                $"{LogPrefix} Windows 표시배율 100%에서 채움 경계선이 {physicalPixelsAtWindows100:F2} " +
                "물리픽셀이 됩니다 — 1픽셀 미만은 안티에일리어싱에 흡수되어 경계가 사라집니다. " +
                "★ 이 판정은 macOS 캡처로 대신할 수 없습니다(Retina에서는 같은 값이 2픽셀입니다).");

            Debug.Log($"{LogPrefix} 하한 두 종 — 낱선 {StickConfig.MinStrokeScreenPoints:F2}pt / " +
                $"채움 경계선 {StickConfig.MinFillOutlineScreenPoints:F2}pt " +
                $"(Windows 100%에서 {physicalPixelsAtWindows100:F2}물리픽셀, macOS Retina에서 " +
                $"{physicalPixelsAtWindows100 * 2f:F2}물리픽셀).");
        }

        /// <summary>
        /// ★ 하한이 <b>물리기 시작하는 배율</b>을 배포 상수에서 유도한다.
        /// 설계의 핵심 논거("사용자 0.60 / 출하 0.75 / 다이얼 최대 1.00에서는 아예 안 물린다")가
        /// 상수 변경으로 조용히 거짓이 되는 것을 막는다.
        /// </summary>
        [Test]
        public void 채움_경계선_하한이_물리는_배율은_다이얼_하한_근처에만_남는다()
        {
            float floorWorld = StickConfig.MinFillOutlineScreenPoints
                / StickConfig.ReferencePointsPerWorldUnitApprox;
            float bindingScale = floorWorld / AccessoryShapeBuilder.BaselineStrokeWidth;

            // 현행 낱선 하한이었다면 다이얼 전 구간에서 물린다 — 그것이 지금 고치는 상태다.
            float oldFloorWorld = StickConfig.MinStrokeScreenPoints
                / StickConfig.ReferencePointsPerWorldUnitApprox;
            float oldBindingScale = oldFloorWorld / AccessoryShapeBuilder.BaselineStrokeWidth;
            Assert.Greater(oldBindingScale, StickConfig.MaxCharacterScale,
                $"{LogPrefix} 네거티브 컨트롤 실패 — 낱선 하한이 물리는 배율({oldBindingScale:F3})이 " +
                $"다이얼 최대({StickConfig.MaxCharacterScale:F2}) 이하입니다. " +
                "그렇다면 '현행 하한이 전 구간에서 채움을 먹고 있다'는 이 라운드의 전제가 틀린 것입니다.");

            Assert.Less(bindingScale, 0.60f,
                $"{LogPrefix} 채움 경계선 하한이 물리는 배율({bindingScale:F3})이 사용자 저장 배율(0.60) " +
                "이상입니다 — 그 배율에서 색면이 다시 하한에 먹힙니다.");
            Assert.Greater(bindingScale, StickConfig.MinCharacterScale,
                $"{LogPrefix} 채움 경계선 하한이 다이얼 전 구간에서 한 번도 물리지 않습니다" +
                $"({bindingScale:F3} ≤ {StickConfig.MinCharacterScale:F2}) — 안전망이 사라졌습니다. " +
                "0.35~0.509 구간은 '실루엣 전용 구간'이고 거기서는 하한이 남아 있어야 합니다.");

            Debug.Log($"{LogPrefix} 하한이 물리는 배율 — 채움 경계선 {bindingScale:F4} " +
                $"(낱선은 {oldBindingScale:F4}로 다이얼 전 구간). 사용자 0.60 / 출하 0.75 / " +
                "최대 1.00은 전부 순수 비례 구간입니다.");
        }

        // ============================================================================
        // 2. ★★ 조용한 무효화 방지 — 되올리기 <b>두 경로</b>가 모두 역할을 안다
        // ============================================================================

        /// <summary>ApplyStrokeWidthsForScale의 본문만 잘라낸다(중괄호 균형).
        /// 파일 전체를 훑으면 문서 주석의 인용문만으로도 통과하는 거짓 초록이 된다.</summary>
        private static string ApplyStrokeWidthsBody()
        {
            string src = Read(AgentPath);
            int sig = src.IndexOf("private void ApplyStrokeWidthsForScale(", System.StringComparison.Ordinal);
            Assert.Greater(sig, 0,
                $"{LogPrefix} StickmanAgent에서 ApplyStrokeWidthsForScale 정의를 찾지 못했습니다 — " +
                "이름이 바뀌었다면 이 검사도 함께 갱신해야 합니다(그 전까지 검사는 대상 없이 돕니다).");

            int open = src.IndexOf('{', sig);
            int depth = 0;
            for (int i = open; i < src.Length; i++)
            {
                if (src[i] == '{') depth++;
                else if (src[i] == '}')
                {
                    depth--;
                    if (depth == 0) return src.Substring(open, i - open + 1);
                }
            }
            Assert.Fail($"{LogPrefix} ApplyStrokeWidthsForScale의 본문 끝을 찾지 못했습니다.");
            return string.Empty;
        }

        [Test]
        public void 되올리기_두_경로가_모두_채움_경계선_하한을_쓴다()
        {
            string body = ApplyStrokeWidthsBody();

            // 비공허성 — 본문에 두 훑기가 실제로 들어 있는지부터 확인한다.
            StringAssert.Contains("_bakedStrokeWidths", body,
                $"{LogPrefix} 본문에서 '구워진 선 훑기'를 찾지 못했습니다 — 잘라낸 범위가 틀렸습니다.");
            StringAssert.Contains("_dynamicVisuals", body,
                $"{LogPrefix} 본문에서 '단일 창구 안전망 훑기'를 찾지 못했습니다 — 잘라낸 범위가 틀렸습니다.");

            // 경로 (1) — 구워진 선. 머리 링이 여기 들어 있다.
            StringAssert.Contains("_bakedStrokeIsFillOutline", body,
                $"{LogPrefix} ★ 경로 (1)이 선의 역할을 모릅니다 — 구워진 선 훑기가 머리 링까지 " +
                "낱선 하한(2.00pt)으로 되올립니다. M6의 머리 링 이득(배율 0.60에서 몸통 획 ÷ 머리 지름 " +
                "22.04% → 22.291%)이 통째로 사라지고, 화면은 하나도 안 바뀝니다.");

            // 경로 (2) — 런타임 생성 잉크. 액세서리 채움 경계선이 여기 들어 있다.
            StringAssert.Contains("FillOutlineStroke.Is", body,
                $"{LogPrefix} ★ 경로 (2)가 선의 역할을 모릅니다 — 안전망 훑기가 액세서리 채움 경계선을 " +
                "낱선 하한으로 되올립니다. 렌더러가 1.00pt로 그린 직후 여기서 2.00pt가 되므로 " +
                "<b>렌더러만 고치면 아무 일도 일어나지 않습니다</b>.");

            // 두 경로가 같은 값을 쓰는가(각자 환산하면 화면이 바뀔 때 한쪽만 따라간다).
            int uses = Regex.Matches(body, "fillOutlineFloorWorld").Count;
            Assert.GreaterOrEqual(uses, 2,
                $"{LogPrefix} 채움 경계선 하한이 본문에서 {uses}번만 쓰입니다 — 두 경로가 각각 한 번씩, " +
                "최소 2번 나와야 합니다(한 경로만 고친 상태일 가능성이 큽니다).");

            Debug.Log($"{LogPrefix} 되올리기 두 경로 모두 역할을 확인함 " +
                $"(fillOutlineFloorWorld 사용 {uses}회).");
        }

        [Test]
        public void 액세서리_렌더러가_채운_도형의_윤곽선에만_낮은_하한을_쓴다()
        {
            string src = Read(AccessoryRendererPath);

            StringAssert.Contains("RenderFillOutlineStrokeWidth", src,
                $"{LogPrefix} 액세서리 렌더러에 채움 경계선 전용 두께가 없습니다 — " +
                "모든 선이 다시 낱선 하한으로 그려집니다.");
            StringAssert.Contains("MinFillOutlineWorld", src,
                $"{LogPrefix} 액세서리 렌더러가 채움 경계선 하한을 읽지 않습니다.");
            StringAssert.Contains("FillOutlineStroke.Mark", src,
                $"{LogPrefix} 액세서리 렌더러가 채움 경계선에 표식을 붙이지 않습니다 — " +
                "에이전트의 안전망 훑기와 진단이 그 선의 역할을 알 수 없게 됩니다.");

            // ★ 낱선 20개는 1pt도 얇아지면 안 된다 — 그 선이 그 자리의 유일한 잉크다.
            //   판정 근거가 shape.Filled 하나임을 소스에서 확인한다(이름 목록으로 가르면 DLC가 샌다).
            StringAssert.Contains("shape.Filled", src,
                $"{LogPrefix} 채움/낱선 판정이 shape.Filled에서 나오지 않습니다 — " +
                "도형 이름 목록으로 가르면 새 DLC 도형이 조용히 규칙 밖으로 빠져나갑니다.");

            Debug.Log($"{LogPrefix} 액세서리 렌더러 — 채움 경계선 전용 두께/표식 확인.");
        }

        /// <summary>
        /// ★ 진단 로그가 <b>두 하한을 따로</b> 나른다 — 안 그러면 Windows 사용자의 <c>[렌더품질]</c> 줄이
        /// 정상적으로 1.18pt인 채움 경계선을 "하한 미달 — 결함"으로 오진한다(그리고 그 신고를 받은
        /// 사람이 멀쩡한 코드를 고치려고 한 라운드를 쓴다).
        /// </summary>
        [Test]
        public void 진단_계측기가_두_하한을_모두_나른다()
        {
            string src = Read(DiagnosticsPath);

            StringAssert.Contains("MinStrokeScreenPoints", src,
                $"{LogPrefix} 계측기가 낱선 하한을 참조하지 않습니다.");
            StringAssert.Contains("MinFillOutlineScreenPoints", src,
                $"{LogPrefix} ★ 계측기가 채움 경계선 하한을 모릅니다 — 최소값 하나를 하한 하나와 " +
                "비교하는 구조라, 정상적으로 얇은 채움 경계선이 결함으로 신고됩니다. " +
                "이 로그는 macOS/Windows가 같은 함수를 부르므로 오진도 양 플랫폼에서 똑같이 납니다.");
            StringAssert.Contains("FillOutlineStroke.Is", src,
                $"{LogPrefix} 계측기가 선의 역할을 묻지 않습니다 — 두 통으로 나눌 방법이 없습니다.");

            Debug.Log($"{LogPrefix} 진단 계측기 — 두 하한 + 역할 조회 확인.");
        }

        /// <summary>
        /// ★ 이 라운드의 A/B 대조군을 <b>그대로 유지</b>한다 — 인벤토리 카드는 하한이 <b>없다</b>.
        /// 카드에 하한이 생기는 순간 "같은 좌표 · 다른 펜 · 3.3배 다른 결과"라는 증거가 사라지고,
        /// 카드 그림도 착용 그림과 같은 이유로 조잡해진다.
        /// </summary>
        [Test]
        public void 인벤토리_카드_아이콘에는_여전히_화면상_하한이_없다()
        {
            string src = Read(CardIconPath);

            Assert.IsFalse(src.Contains("MinStrokeScreenPoints"),
                $"{LogPrefix} 카드 아이콘이 낱선 하한을 참조하기 시작했습니다 — 44px 카드에 2pt 하한을 " +
                "걸면 색면이 통째로 먹힙니다(착용 그림이 22.0%였던 것과 정확히 같은 원인).");
            Assert.IsFalse(src.Contains("MinFillOutlineScreenPoints"),
                $"{LogPrefix} 카드 아이콘이 채움 경계선 하한을 참조하기 시작했습니다 — " +
                "카드는 화면상 하한이 <b>없는 쪽</b>이고, 그것이 이 판정의 대조군입니다.");

            Debug.Log($"{LogPrefix} 카드 아이콘 — 하한 참조 0건(대조군 유지).");
        }
    }
}
