using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★ <b>P0 회귀 — 「판정은 Ø36인데 화면은 Ø44로 그린다」</b> (2026-09-06, design-equipment 발견).
    ///
    /// ============================================================================
    /// 무엇이 터져 있었나
    /// ============================================================================
    /// 화면 밖 방지 사다리(<c>ComputeLayout</c>)는 자리가 모자라면 버튼 지름을 <b>44 → 36으로 줄여</b>
    /// 다시 시도하고, 마지막 칸(<c>PlaceColumn(ShrunkDiameterPoints)</c>)은 <b>무조건</b> 36으로 놓는다.
    /// 그 결정은 <c>_diameterPoints</c>에 들어가 히트 판정 · 클램프 상자 · 이름표 링 · 진단 로그가
    /// 전부 읽고 있었다. 그런데 <b>원을 만드는 코드만</b> 그 값을 보지 않았다 — <c>BuildButton</c>이
    /// <c>Awake</c> 때 <c>ButtonDiameterPoints</c>로 한 번 굽고 끝이었기 때문이다.
    /// <b>축소 폴백은 태어나서 한 번도 그려진 적이 없다.</b>
    ///
    /// 실측 결과(pt. 「설계대로 Ø44」 → 「버그」 → 「수정 후」):
    /// <code>
    ///   히트 반경 − 보이는 반경  평상 +4.00 → +0.00 → +4.00 / 호버 +2.00 → −2.00 → +2.36
    ///   클램프 상자 − 보이는 원  호버  4.00 →  0.00 → 4.36
    ///   세로 일렬 이웃 사이 틈   평상  8.00 →  8.00 → 16.00
    /// </code>
    /// <b>호버의 −2.00이 이 사고의 핵심</b>이다 — 커서를 올리면 <b>보이는 원이 눌리는 원보다 커져</b>
    /// 바깥 2pt 띠가 «보이는데 안 눌리는» 영역이 됐다. 32-1이 개별 버튼 클램프를 금지하면서까지
    /// 지키려던 «보이는 것과 눌리는 것이 같다»가 <b>축소 폴백에서만</b> 깨져 있었다.
    ///
    /// ============================================================================
    /// 이 픽스처가 다른 부채꼴 픽스처와 결정적으로 다른 점
    /// ============================================================================
    /// <b>판정값과 렌더값을 같은 실행 안에서 «다른 경로로» 재서 대조한다.</b>
    /// 기존 검사는 전부 <c>ButtonDiameter</c>(= 판정값) 하나만 봤고, 그 값은 버그가 있던 동안에도
    /// <b>정직하게 36을 돌려줬다</b> — 그래서 판정값만 보는 어떤 테스트도 이 사고를 볼 수 없었다.
    /// 여기서는 <c>RenderedButtonDiameterPoints</c>가 <c>_diameterPoints</c>를 쳐다보지 않고
    /// <b>씬 오브젝트의 상자 × 실제로 걸린 배치 배율</b>을 다시 잰다. 두 측정이 서로를 볼 수 없어야
    /// 대조가 대조가 된다(같은 함정에 같이 빠지지 않는다).
    ///
    /// ============================================================================
    /// 왜 앵커 (0,0)인가 — <b>화면 크기와 무관한 축소 트리거</b> (2026-09-06 전수 계산)
    /// ============================================================================
    /// 위성 폐지 후 배치(5슬롯 · 간격 22.5° · 궤도 148)에서 앵커를 화면 좌하단 꼭짓점에 두면
    /// 기준각은 45°로 스냅되고, 부채꼴 상자 합집합은 앵커 기준 <c>[−28, +176]²</c>가 된다.
    /// 여백을 최소(8)로 잡아도 필요한 평행이동이 <c>|(36, 36)| = 50.9pt</c>라
    /// 상한 <see cref="GearRadialMenuWidget.MaxGroupShiftPoints"/>(48)를 <b>구조적으로</b> 넘는다.
    /// 여백이 커지면 더 넘을 뿐이라 <b>여백 구성과 무관</b>하고, 회전을 ±90° 다 돌려봐도 45°가 최소라
    /// 개선되지 않는다. 세로 일렬(Ø44) 역시 <c>xMin = −28 &lt; 8</c>로 막힌다.
    /// ⇒ Ø44로 성립하는 칸이 하나도 없으므로 사다리는 <b>반드시</b> Ø36 칸(회전-축소 또는 최종 세로
    /// 일렬)으로 내려온다. 실제로 화면 29종 × 예약 띠 8종 × 진입점 2종 전수 계산에서
    /// <b>예외 0건</b>이었다. 그래서 이 검사에는 «화면이 작으면 조용히 건너뛴다»가 없다.
    /// </summary>
    public sealed class GearMenuShrinkFallbackRenderTests
    {
        private GearRadialMenuWidget _menu;

        [OneTimeSetUp]
        public void RequireIsolatedSaveFile()
        {
            Assert.IsTrue(CharacterSaveStore.IsRedirectedForTesting,
                "저장 경로가 격리되지 않았습니다 — GlobalPlayModeTestIsolation이 돌지 않았습니다.");
        }

        private IEnumerator LoadSceneAndResolve()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var found = Object.FindObjectsByType<GearRadialMenuWidget>(FindObjectsSortMode.None);
            Assert.AreEqual(1, found.Length,
                $"씬의 GearRadialMenuWidget 개수가 {found.Length}개입니다 — 1개여야 합니다.");
            _menu = found[0];
            yield return null;
        }

        /// <summary>연출 시간은 <b>벽시계</b>로 잰다(프레임 수 대기 금지 — 배치모드는 2,000fps로도 돈다).</summary>
        private static IEnumerator WaitWallClock(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        private IEnumerator CollapseAndSettle()
        {
            _menu.Collapse(GearMenuCollapseMode.User, "축소 폴백 렌더 회귀 테스트");
            yield return WaitWallClock(GearRadialMenuWidget.CollapseUserSeconds + 0.15f);
            Assert.IsFalse(_menu.IsVisible, "부채꼴이 접히지 않았습니다 — 다음 펼침이 무시됩니다(Expand의 IsExpanded 가드).");
        }

        /// <summary>판정 지름과 렌더 지름을 다섯 버튼 전부에서 대조한다. 어긋나면 그 자리에서 실패.</summary>
        private void AssertRenderMatchesDecision(string phase)
        {
            float decided = _menu.ButtonDiameter;
            for (int i = 0; i < GearRadialMenuWidget.ButtonCount; i++)
            {
                float rendered = _menu.RenderedButtonDiameterPoints(i);
                Assert.Greater(rendered, 0f,
                    $"[{phase}] {i}번({GearRadialMenuWidget.NameOf(i)}) 버튼의 렌더 지름이 0입니다 — " +
                    "측정기가 죽었습니다(아직 UI가 만들어지지 않았거나 Group/Surface가 사라졌습니다).");
                Assert.AreEqual(decided, rendered, 0.01f,
                    $"[{phase}] {i}번({GearRadialMenuWidget.NameOf(i)}) 버튼 — " +
                    $"배치 사다리의 판정 지름은 Ø{decided:F1}pt인데 화면에 그려지는 지름은 Ø{rendered:F1}pt입니다. " +
                    "이것이 2026-09-06에 고친 사고의 정확한 형태입니다: 사다리가 «이 크기면 화면에 " +
                    "들어간다»고 통과시킨 배치를 렌더가 다른 크기로 그리면, 통과 판정 자체가 거짓이 됩니다. " +
                    $"{nameof(GearRadialMenuWidget)}.ApplyLayoutDiameterToViews 배선을 확인하세요.");

                // ★ 그리고 그 어긋남이 <b>사용자에게 어떻게 보였는가</b>를 직접 잠근다:
                //   호버로 커진 원이 클릭 판정 원을 넘어가면 «보이는데 안 눌리는» 띠가 생긴다(32-1).
                //   버그가 있던 동안 이 값은 정확히 −2.00pt였다.
                float drawnHoverRadius = rendered * 0.5f * GearRadialMenuWidget.HoverScale;
                float hitRadius = decided * 0.5f + GearRadialMenuWidget.HitPaddingPoints;
                Assert.GreaterOrEqual(hitRadius, drawnHoverRadius - 0.01f,
                    $"[{phase}] {i}번({GearRadialMenuWidget.NameOf(i)}) 버튼 — 커서를 올렸을 때 " +
                    $"보이는 원(반경 {drawnHoverRadius:F2}pt)이 눌리는 원(반경 {hitRadius:F2}pt)보다 큽니다. " +
                    "바깥 테두리가 «보이는데 안 눌리는» 영역이 됩니다 — 32-1이 개별 버튼 클램프를 " +
                    "금지하면서까지 지키려던 불변식입니다.");
            }
        }

        // ==================== ① 핵심 — 축소가 걸리면 그림도 줄어든다 ====================

        [UnityTest]
        public IEnumerator 축소_폴백이_걸리면_실제로_그려지는_원도_같이_줄어든다()
        {
            yield return LoadSceneAndResolve();

            // 화면 좌하단 꼭짓점 — 위 클래스 문서의 전수 계산으로 Ø44가 구조적으로 불가능한 앵커.
            _menu.Expand(Vector2.zero);
            Assert.IsTrue(_menu.IsVisible, "부채꼴이 펼쳐지지 않았습니다 — 아래 측정이 무의미합니다.");

            // (가) 판정 — 사다리가 실제로 축소 칸으로 내려왔는가. 이것이 아니면 이 테스트는 아무것도
            //      재지 않은 것이므로, 조용히 통과시키지 않고 여기서 시끄럽게 실패한다.
            Assert.AreEqual(GearRadialMenuWidget.ShrunkDiameterPoints, _menu.ButtonDiameter, 0.01f,
                $"앵커 (0,0)에서 판정 지름이 Ø{_menu.ButtonDiameter:F1}pt입니다 — " +
                $"Ø{GearRadialMenuWidget.ShrunkDiameterPoints:F0}이어야 합니다. " +
                $"이 앵커에서 Ø{GearRadialMenuWidget.ButtonDiameterPoints:F0}가 성립하려면 " +
                "필요한 평행이동 50.9pt가 상한 " +
                $"{GearRadialMenuWidget.MaxGroupShiftPoints:F0}pt 안에 들어와야 하는데 그럴 수 없습니다. " +
                "배치 기하(궤도/간격/클램프 상자/이동 상한)가 바뀌었다면 이 픽스처의 앵커 근거를 다시 계산하세요 — " +
                "근거가 낡은 채로 두면 이 검사는 «축소를 한 번도 안 밟는» 빈 검사가 됩니다.");

            // (나) 렌더 — 펼침 애니메이션이 시작되는 그 프레임부터 이미 줄어 있어야 한다.
            AssertRenderMatchesDecision("펼침 직후");

            // (다) 그리고 <b>애니메이션이 끝난 뒤에도</b> 유지되어야 한다. 배치 배율을 애니메이션 배율과
            //      같은 트랜스폼에 실으면 다음 프레임의 ApplyVisuals가 그것을 덮어쓴다 — 즉 (나)만
            //      통과하고 (다)에서 무너지는 실패 모드가 실재한다. 두 시점을 모두 잰다.
            yield return WaitWallClock(GearRadialMenuWidget.ExpandTotalSeconds + 0.25f);
            Assert.IsTrue(_menu.IsVisible, "펼침 도중에 부채꼴이 사라졌습니다 — 아래 측정이 무의미합니다.");
            AssertRenderMatchesDecision("안착 후");

            // (라) 대조의 반대편 — 렌더값이 «항상 판정값을 되돌려 주는» 항등 함수가 아님을 보인다.
            //      기준 지름과 실제로 달라야 한다(같으면 축소가 화면에 도달하지 않은 것이다).
            Assert.That(_menu.RenderedButtonDiameterPoints(0),
                Is.Not.EqualTo(GearRadialMenuWidget.ButtonDiameterPoints).Within(0.01f),
                "판정은 축소인데 화면은 여전히 기준 지름 " +
                $"Ø{GearRadialMenuWidget.ButtonDiameterPoints:F0}로 그려집니다 — 이 버그 그 자체입니다.");

            Debug.Log($"[축소폴백테스트] 앵커(0,0) — 판정 Ø{_menu.ButtonDiameter:F1}pt / " +
                $"렌더 Ø{_menu.RenderedButtonDiameterPoints(0):F1}pt / " +
                $"세로일렬={_menu.IsColumnFallback}.");

            yield return CollapseAndSettle();
        }

        // ==================== ② 음성 대조 — 넉넉하면 기준 지름 그대로 ====================

        [UnityTest]
        public IEnumerator 공간이_넉넉하면_판정도_렌더도_기준_지름_그대로다()
        {
            yield return LoadSceneAndResolve();

            _menu.Expand(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            Assert.IsTrue(_menu.IsVisible, "부채꼴이 펼쳐지지 않았습니다 — 아래 측정이 무의미합니다.");

            // 포인트 공간 화면 크기를 <b>프로덕션 변환을 거쳐</b> 되잰다. 상수를 다시 타이핑하면
            // 그 사본이 프로덕션과 조용히 갈라진다(ButtonScreenRect.width == 판정 지름 × 픽셀/포인트).
            float pixelsPerPoint = _menu.ButtonScreenRect(0).width / Mathf.Max(0.0001f, _menu.ButtonDiameter);
            Assert.Greater(pixelsPerPoint, 0f, "픽셀/포인트 배율을 잴 수 없습니다 — 측정 전제가 깨졌습니다.");
            var screenPoints = new Vector2(Screen.width, Screen.height) / pixelsPerPoint;

            // 화면 한가운데에서 Ø44가 성립하려면 «어느 변으로도 도달 반경 + 여백»이 들어가야 한다.
            float reachPoints = GearRadialMenuWidget.OrbitRadiusPoints
                + (GearRadialMenuWidget.ButtonDiameterPoints
                   + GearRadialMenuWidget.ClampBoxPaddingPoints) * 0.5f;
            float neededPoints = 2f * (reachPoints + GearRadialMenuWidget.TopMarginPoints);

            if (screenPoints.x < neededPoints || screenPoints.y < neededPoints)
            {
                // 이 러너 화면에서는 «넉넉한 자리»가 물리적으로 없다. 조용히 초록으로 넘기지 않고
                // 러너에 «건너뜀»으로 계속 보이게 남긴다(CLAUDE.md 협업 프로토콜).
                Assert.Ignore($"러너 화면이 {screenPoints.x:F0}×{screenPoints.y:F0}pt로 " +
                    $"{neededPoints:F0}pt 요건에 못 미쳐 «넉넉한 자리» 대조를 잴 수 없습니다. " +
                    "핵심 검사(축소_폴백이_걸리면…)는 화면 크기와 무관하므로 그쪽은 그대로 돕니다.");
            }

            Assert.AreEqual(GearRadialMenuWidget.ButtonDiameterPoints, _menu.ButtonDiameter, 0.01f,
                $"화면 {screenPoints.x:F0}×{screenPoints.y:F0}pt 한가운데인데 축소 폴백이 걸렸습니다 — " +
                "확정 기하가 먼저 성립해야 합니다.");
            AssertRenderMatchesDecision("넉넉한 자리");

            // 축소 쪽 검사와 <b>반대 방향</b>의 대조: 여기서는 렌더가 기준 지름과 같아야 한다.
            // 두 방향을 다 잠가야 «렌더값이 어떤 상수에 고정됐다»는 실패 모드가 남지 않는다.
            Assert.AreEqual(GearRadialMenuWidget.ButtonDiameterPoints,
                _menu.RenderedButtonDiameterPoints(0), 0.01f,
                "넉넉한 자리인데 화면의 원이 기준 지름이 아닙니다 — 배치 배율이 1이 아닌 값에 눌어붙었습니다.");

            Debug.Log($"[축소폴백테스트] 화면중앙 — 판정/렌더 모두 Ø{_menu.ButtonDiameter:F1}pt " +
                $"(화면 {screenPoints.x:F0}×{screenPoints.y:F0}pt).");

            yield return CollapseAndSettle();
        }
    }
}
