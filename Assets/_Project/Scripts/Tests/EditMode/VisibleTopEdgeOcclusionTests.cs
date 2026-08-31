using NUnit.Framework;
using UnityEngine;
using StickMate.Platform;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 사용자 신고 회귀 잠금(2026-08-31, 디버거):
    /// **"창이 겹쳐있을때 창이 뒤에 있음에도 그 경계면을 따라 걸음"**
    ///
    /// ============================================================================
    /// 확정된 원인 (코드 경로로 확정 — 추측 아님)
    /// ============================================================================
    /// Windows 구현체 <c>Win32WindowService.EnumerateFootholds()</c>가 <b>EnumWindows가 돌려준 창
    /// 전체 사각형을 그대로 발판으로 내보내고 있었다.</b> 가려짐(오클루전) 계산이 그 파일에 단 한 줄도
    /// 없었다 — 즉 앞 창에 완전히 덮여 사용자 눈에 한 픽셀도 보이지 않는 창의 상단선도 유효한 발판으로
    /// 남았고, 캐릭터가 그 보이지 않는 경계를 따라 걸었다.
    ///
    /// macOS는 2026-08-28 라운드에서 같은 결함을 이미 고쳤다. 그런데 그 수정이
    /// <c>MacWindowService.BuildVisibleTopEdgeFootholds()</c>라는 <b>macOS 전용 파일의 private
    /// 메서드 안에 갇혀 있었다</b>. 그래서 (1) Windows 구현이 재사용할 수 없었고 (2) 그 파일 전체가
    /// <c>#if</c>로 macOS에서만 컴파일되므로 <b>테스트로 겨냥할 수도 없었다</b>. 이번 라운드에
    /// 알고리즘을 플랫폼 중립 <see cref="VisibleTopEdgeSolver"/>로 끌어내 두 구현체가 공유하게 만든
    /// 이유가 이것이다 — 한쪽만 고쳐지는 재발 경로 자체를 없앤다.
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 것 (절대 조건 + 네거티브 컨트롤, 이 프로젝트 표준)
    /// ============================================================================
    ///  V1   신고 배치 그대로 — 앞 창에 덮인 구간은 발판이 되지 않고, 덮이지 않은 구간만 남는다.
    ///  V1n  (네거티브) <b>같은 배치에서 수정 전 규칙</b>(창 전체 사각형 = 발판)을 그대로 계산해 보면
    ///       가려진 구간이 발판으로 잡힌다 = V1이 항상 참인 단언이 아니라 진짜 버그를 잡고 있다는 증거.
    ///  V2   완전히 덮인 창은 발판을 하나도 내지 않는다(= 그 위 캐릭터는 낙하한다).
    ///  V3   z-order 방향 — 뒤에 있는 창은 앞 창을 가리지 못한다(부호가 뒤집히면 즉시 실패).
    ///  V4   상단선 높이를 품지 않는 앞 창은 가리지 못한다(창 내부를 덮는 것과 상단선을 덮는 것은 다르다).
    ///  V5   앞 창이 한가운데를 덮으면 좌/우 <b>두 조각</b>이 남는다(발판을 통째로 버리지 않는다).
    ///  V6   남은 조각이 캐릭터 몸통보다 훨씬 좁으면 버린다("허공에 떠 있다"는 인식 재발 방지).
    ///  V7   버퍼 재사용 정확성 — 같은 솔버로 다른 배치를 연속 계산해도 이전 패스가 새어 나오지 않는다
    ///       (24시간 상주 앱이라 모든 버퍼를 재사용하므로 이 오염이 실제 위험이다).
    ///
    /// 좌표계: 전부 OS 스크린 좌표(좌상단 원점) — <c>r.y</c>가 창 <b>상단선</b>이고 아래로 갈수록 y가
    /// 커진다. 이 파일은 OS를 전혀 호출하지 않는 순수 산술 검증이라 macOS 개발 환경에서도 Windows
    /// 시나리오를 그대로 실측할 수 있다.
    /// </summary>
    public sealed class VisibleTopEdgeOcclusionTests
    {
        private const string LogPrefix = "[창겹침발판-TEST]";

        /// <summary>Win32WindowService/MacWindowService가 쓰는 것과 같은 값.</summary>
        private const float MinVisibleWidth = 24f;

        private VisibleTopEdgeSolver _solver;

        [SetUp]
        public void SetUp() => _solver = new VisibleTopEdgeSolver();

        /// <summary>z-order 앞->뒤 순서로 창을 넣고 푼다(첫 인자가 맨 앞 창).</summary>
        private void SolveZOrder(params Rect[] frontToBack)
        {
            _solver.Begin();
            for (int i = 0; i < frontToBack.Length; i++) _solver.AddWindow(frontToBack[i]);
            _solver.Solve(MinVisibleWidth, false, default);
        }

        /// <summary>창 w가 낸 조각들 중 x를 품는 것이 있는지 = "이 x에서 그 창을 딛을 수 있는가".</summary>
        private bool CanStandOn(int windowIndex, float x)
        {
            for (int s = 0; s < _solver.SegmentCount; s++)
            {
                if (_solver.GetSegmentWindowIndex(s) != windowIndex) continue;
                float start = _solver.GetSegmentStartX(s);
                if (x >= start && x <= start + _solver.GetSegmentWidth(s)) return true;
            }
            return false;
        }

        private string DumpSegments()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("조각 ").Append(_solver.SegmentCount).Append("개 [");
            for (int s = 0; s < _solver.SegmentCount; s++)
            {
                if (s > 0) sb.Append(" | ");
                sb.Append("창").Append(_solver.GetSegmentWindowIndex(s)).Append(": x ")
                  .Append(_solver.GetSegmentStartX(s).ToString("F0")).Append("~")
                  .Append((_solver.GetSegmentStartX(s) + _solver.GetSegmentWidth(s)).ToString("F0"));
            }
            return sb.Append(']').ToString();
        }

        // ============================================================================
        // 신고 배치 — 창 A(앞)가 창 B(뒤)의 상단선 오른쪽 대부분을 덮는다
        //
        //   x:      100      200                     700        1000
        //   창B(뒤) |---------|=======가려짐==========|            상단선 y=500
        //   창A(앞)           |==========================|        y 300~900
        //
        // 사용자가 본 것: 캐릭터가 x=450 근처(창 A에 완전히 덮인 창 B의 경계) 위를 걸었다.
        // ============================================================================
        private static readonly Rect FrontWindow = new Rect(200f, 300f, 800f, 600f); // x 200~1000, y 300~900
        private static readonly Rect BackWindow = new Rect(100f, 500f, 600f, 400f);  // x 100~700, 상단선 y=500

        [Test]
        public void V1_앞_창에_덮인_구간은_발판이_되지_않고_보이는_구간만_남는다()
        {
            SolveZOrder(FrontWindow, BackWindow);
            Debug.Log($"{LogPrefix} V1 — {DumpSegments()}");

            Assert.IsFalse(CanStandOn(1, 450f),
                $"{LogPrefix} 신고 재현 — 창 A에 완전히 덮인 x=450에서 뒤 창(B)의 상단선이 아직 발판입니다. " +
                $"사용자 눈에는 캐릭터가 허공을 걷는 것으로 보입니다. {DumpSegments()}");
            Assert.IsFalse(CanStandOn(1, 690f),
                $"{LogPrefix} 덮인 구간의 오른쪽 끝(x=690)도 발판이면 안 됩니다. {DumpSegments()}");

            // 기능을 죽인 것이 아니라 가려진 동안만 막는다 — 눈에 보이는 왼쪽 조각은 그대로 발판이다.
            Assert.IsTrue(CanStandOn(1, 150f),
                $"{LogPrefix} 창 A 왼쪽으로 삐져나와 **실제로 보이는** 구간(x=150)까지 사라졌습니다 — " +
                $"이건 과잉 제거입니다. {DumpSegments()}");
            Assert.AreEqual(100f, _solver.GetVisibleWidth(1), 0.01f,
                $"{LogPrefix} 뒤 창의 보이는 상단 테두리 폭은 100(x 100~200)이어야 합니다. {DumpSegments()}");

            // 앞 창 자신은 아무에게도 가려지지 않았다.
            Assert.IsTrue(CanStandOn(0, 450f), $"{LogPrefix} 맨 앞 창이 발판을 잃었습니다. {DumpSegments()}");
            Assert.AreEqual(800f, _solver.GetVisibleWidth(0), 0.01f,
                $"{LogPrefix} 맨 앞 창의 상단 테두리는 통째로 보여야 합니다. {DumpSegments()}");
        }

        [Test]
        public void V1n_네거티브_수정_전_규칙이면_가려진_구간이_그대로_발판이_된다()
        {
            // 수정 전 Win32WindowService.OnEnumWindow()가 하던 일 그대로: 창 전체 사각형 = 발판.
            // (가려짐 계산 없음 — EnumWindows 결과를 그대로 _footholdBuffer에 넣었다.)
            Rect unfilteredBackFoothold = BackWindow;
            bool standsOnHiddenEdge =
                450f >= unfilteredBackFoothold.x &&
                450f <= unfilteredBackFoothold.x + unfilteredBackFoothold.width;

            Assert.IsTrue(standsOnHiddenEdge,
                $"{LogPrefix} 네거티브 컨트롤이 성립하지 않습니다 — 수정 전 규칙에서도 x=450이 뒤 창의 " +
                $"발판 범위에 들지 않으면, V1은 버그를 잡는 단언이 아니라 항상 참인 단언입니다. " +
                $"배치를 다시 설계해야 합니다.");

            // 같은 배치를 수정 후 규칙으로 풀면 그 x는 사라진다 = 이 수정이 실제로 무언가를 바꾼다.
            SolveZOrder(FrontWindow, BackWindow);
            Assert.IsFalse(CanStandOn(1, 450f),
                $"{LogPrefix} 수정 전/후가 같은 답을 냅니다 — 수정이 실효가 없습니다.");
            Debug.Log($"{LogPrefix} V1n 네거티브 컨트롤 성립 — 수정 전 규칙: x=450에서 뒤 창을 딛는다(버그). " +
                $"수정 후: 딛지 못한다. {DumpSegments()}");
        }

        [Test]
        public void V2_완전히_덮인_창은_발판을_하나도_내지_않는다()
        {
            Rect front = new Rect(0f, 0f, 1000f, 800f);      // 화면을 통째로 덮는 큰 창
            Rect fullyHidden = new Rect(200f, 300f, 400f, 300f); // 그 안에 완전히 들어간 창

            SolveZOrder(front, fullyHidden);
            Debug.Log($"{LogPrefix} V2 — {DumpSegments()}");

            Assert.AreEqual(0f, _solver.GetVisibleWidth(1), 0.001f,
                $"{LogPrefix} 완전히 덮인 창이 아직 보이는 폭을 갖고 있습니다 = 발판이 남습니다. {DumpSegments()}");
            for (int s = 0; s < _solver.SegmentCount; s++)
            {
                Assert.AreNotEqual(1, _solver.GetSegmentWindowIndex(s),
                    $"{LogPrefix} 완전히 덮인 창이 발판 조각을 냈습니다. {DumpSegments()}");
            }
        }

        [Test]
        public void V3_뒤에_있는_창은_앞_창을_가리지_못한다()
        {
            // V2와 완전히 같은 사각형인데 z-order만 뒤집었다. 이제 작은 창이 **앞**이므로,
            // 큰 창의 상단선(y=0)은 작은 창(y 300~600)이 품지 않아 그대로 다 보인다.
            Rect smallFront = new Rect(200f, 300f, 400f, 300f);
            Rect bigBack = new Rect(0f, 0f, 1000f, 800f);

            SolveZOrder(smallFront, bigBack);
            Debug.Log($"{LogPrefix} V3 — {DumpSegments()}");

            Assert.AreEqual(400f, _solver.GetVisibleWidth(0), 0.01f,
                $"{LogPrefix} 맨 앞 창이 뒤 창에 가려졌습니다 — z-order 방향이 뒤집혔습니다. {DumpSegments()}");
            Assert.AreEqual(1000f, _solver.GetVisibleWidth(1), 0.01f,
                $"{LogPrefix} 뒤 창의 상단선(y=0)은 앞 창(y 300~600)이 품지 않으므로 다 보여야 합니다. {DumpSegments()}");
        }

        [Test]
        public void V4_상단선_높이를_품지_않는_앞_창은_가리지_못한다()
        {
            // 앞 창이 뒤 창의 **아래쪽 내부**만 덮는다. 우리가 발판으로 쓰는 것은 상단선 한 줄뿐이므로
            // 이건 발판에 아무 영향이 없어야 한다(창 내부를 덮는 것 != 상단선을 덮는 것).
            Rect frontBelow = new Rect(100f, 700f, 800f, 200f); // y 700~900
            Rect back = new Rect(100f, 500f, 600f, 400f);       // 상단선 y=500

            SolveZOrder(frontBelow, back);
            Debug.Log($"{LogPrefix} V4 — {DumpSegments()}");

            Assert.AreEqual(600f, _solver.GetVisibleWidth(1), 0.01f,
                $"{LogPrefix} 상단선 높이(y=500)를 품지도 않는 창이 발판을 지웠습니다 — 과잉 제거입니다. {DumpSegments()}");
        }

        [Test]
        public void V5_앞_창이_한가운데를_덮으면_좌우_두_조각이_남는다()
        {
            Rect middleFront = new Rect(400f, 400f, 200f, 300f); // x 400~600, y 400~700
            Rect wideBack = new Rect(100f, 500f, 800f, 300f);    // x 100~900, 상단선 y=500

            SolveZOrder(middleFront, wideBack);
            Debug.Log($"{LogPrefix} V5 — {DumpSegments()}");

            int backSegments = 0;
            for (int s = 0; s < _solver.SegmentCount; s++)
            {
                if (_solver.GetSegmentWindowIndex(s) == 1) backSegments++;
            }
            Assert.AreEqual(2, backSegments,
                $"{LogPrefix} 좌/우 두 조각이 나와야 합니다 — 발판을 통째로 버리거나 통째로 살리면 안 됩니다. {DumpSegments()}");
            Assert.IsTrue(CanStandOn(1, 200f), $"{LogPrefix} 왼쪽 조각이 없습니다. {DumpSegments()}");
            Assert.IsFalse(CanStandOn(1, 500f), $"{LogPrefix} 가운데(가려진 구간)가 아직 발판입니다. {DumpSegments()}");
            Assert.IsTrue(CanStandOn(1, 800f), $"{LogPrefix} 오른쪽 조각이 없습니다. {DumpSegments()}");
            Assert.AreEqual(600f, _solver.GetVisibleWidth(1), 0.01f,
                $"{LogPrefix} 보이는 총 폭은 300+300=600이어야 합니다. {DumpSegments()}");
        }

        [Test]
        public void V6_캐릭터_몸통보다_훨씬_좁게_남은_조각은_버린다()
        {
            // 뒤 창의 왼쪽으로 10px만 삐져나온다 — MinVisibleWidth(24) 미만.
            Rect front = new Rect(110f, 300f, 800f, 600f);
            Rect back = new Rect(100f, 500f, 600f, 400f);

            SolveZOrder(front, back);
            Debug.Log($"{LogPrefix} V6 — {DumpSegments()}");

            Assert.AreEqual(0f, _solver.GetVisibleWidth(1), 0.001f,
                $"{LogPrefix} 폭 10짜리 실오라기 조각이 발판으로 남았습니다 — 그 위에 서면 '허공에 떠 있다'는 " +
                $"인식이 그대로 재발합니다. {DumpSegments()}");
        }

        [Test]
        public void V7_같은_솔버로_다른_배치를_연속_계산해도_이전_패스가_새어_나오지_않는다()
        {
            SolveZOrder(FrontWindow, BackWindow);
            int firstCount = _solver.SegmentCount;
            float firstBackWidth = _solver.GetVisibleWidth(1);

            // 전혀 다른 배치(창 1개)를 같은 솔버로 계산.
            SolveZOrder(new Rect(0f, 0f, 500f, 500f));
            Assert.AreEqual(1, _solver.WindowCount, $"{LogPrefix} Begin()이 입력을 비우지 않았습니다.");
            Assert.AreEqual(1, _solver.SegmentCount, $"{LogPrefix} 이전 패스의 조각이 남아 있습니다.");
            Assert.AreEqual(500f, _solver.GetVisibleWidth(0), 0.01f, $"{LogPrefix} 단일 창 결과가 오염됐습니다.");

            // 원래 배치를 다시 계산하면 처음과 정확히 같은 답이 나와야 한다.
            SolveZOrder(FrontWindow, BackWindow);
            Assert.AreEqual(firstCount, _solver.SegmentCount,
                $"{LogPrefix} 같은 입력인데 조각 수가 달라졌습니다 — 재사용 버퍼가 오염됩니다. {DumpSegments()}");
            Assert.AreEqual(firstBackWidth, _solver.GetVisibleWidth(1), 0.001f,
                $"{LogPrefix} 같은 입력인데 보이는 폭이 달라졌습니다 — 재사용 버퍼가 오염됩니다. {DumpSegments()}");
            Debug.Log($"{LogPrefix} V7 통과 — 버퍼 재사용이 결과를 오염시키지 않습니다.");
        }

        [Test]
        public void V8_화면_밖으로_뻗은_창은_클리핑을_켜면_화면_안쪽까지만_발판이_된다()
        {
            // macOS판이 쓰는 경로(리더 지시 6항: 걸어서 화면 밖으로 나가는 경로 자체를 없앤다).
            _solver.Begin();
            _solver.AddWindow(new Rect(-300f, 200f, 900f, 400f)); // x -300~600, 화면(0~1000) 왼쪽 밖으로 뻗음
            _solver.Solve(MinVisibleWidth, true, new Rect(0f, 0f, 1000f, 800f));

            Assert.AreEqual(1, _solver.SegmentCount, $"{LogPrefix} {DumpSegments()}");
            Assert.AreEqual(0f, _solver.GetSegmentStartX(0), 0.01f,
                $"{LogPrefix} 발판이 화면 왼쪽 밖(x<0)에서 시작합니다. {DumpSegments()}");
            Assert.AreEqual(600f, _solver.GetSegmentWidth(0), 0.01f,
                $"{LogPrefix} 화면 안쪽 구간(0~600)만 남아야 합니다. {DumpSegments()}");
        }
    }
}
