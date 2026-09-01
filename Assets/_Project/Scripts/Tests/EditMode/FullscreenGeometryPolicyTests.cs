using NUnit.Framework;
using StickMate.Platform;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// 절대 불변 원칙 2 회귀 잠금 2탄 — <b>macOS 네이티브 전체화면 게임에서 캐릭터가 숨는다.</b>
    ///
    /// ============================================================================
    /// 무엇이 깨져 있었나 (2026-09-02, 실기 통제 실험)
    /// ============================================================================
    /// 판정이 "창 사각형 == 디스플레이 사각형"의 <b>정확 일치</b>였는데, macOS 네이티브 전체화면
    /// (초록 버튼 / <c>toggleFullScreen:</c>) 창의 실측값은 그렇지 않다:
    /// <code>
    ///   CGDisplayBounds  = (0,  0, 1512, 982)
    ///   실제 전체화면 창 = (0, 33, 1512, 949)
    /// </code>
    /// 그래서 그 경로에서는 원칙 2가 통째로 죽어 있었다(보더리스 전체화면만 잡혔다).
    ///
    /// ============================================================================
    /// ★ 이 테스트의 숫자는 전부 **실측**이다 (여기가 이 파일의 존재 이유다)
    /// ============================================================================
    /// 같은 라운드에서 "원인을 고쳤다"는 서사만 남고 동작이 하나도 안 바뀌는 처방이 세 번 나왔다
    /// (<c>safeAreaInsets.top</c>=32 / <c>statusThick</c>=22 / <c>auxiliaryTopLeftArea</c>=32 —
    /// 실측 여백은 <b>33</b>이라 셋 다 epsilon 0.5를 못 넘고 0/24 샘플로 실패한다).
    /// 그래서 이 테스트는 반올림한 예쁜 숫자가 아니라 <b>실기에서 읽은 사각형 그대로</b>를 넣는다.
    /// <b>반증된 처방들이 이 테스트를 통과하지 못하는 것</b>이 이 파일의 가장 중요한 성질이다
    /// (<see cref="반증된_safeAreaInsets_상한_32는_실측_33을_못_넘는다"/>).
    /// </summary>
    public class FullscreenGeometryPolicyTests
    {
        // ---- 실측값(2026-09-02, 14인치 노치 맥북, 스케일드 1512x982) -------------------------
        private const double DispX = 0.0, DispY = 0.0, DispW = 1512.0, DispH = 982.0;

        /// <summary>네이티브 전체화면 — 상단 33pt가 자동 숨김 메뉴바에 남는다.</summary>
        private const double NativeY = 33.0, NativeH = 949.0;

        /// <summary>줌(최대화) — 상단 여백은 같지만 <b>Dock이 보여 하단이 뜬다</b>.</summary>
        private const double ZoomY = 33.0, ZoomH = 874.0;

        private static bool Covers(double x, double y, double w, double h)
            => FullscreenGeometry.CoversDisplay(x, y, w, h, DispX, DispY, DispW, DispH,
                FullscreenGeometry.Epsilon);

        // ====================================================================================
        // 양성 — 여기서 숨어야 한다
        // ====================================================================================

        [Test]
        public void 보더리스_전체화면은_정확일치로_잡힌다()
        {
            Assert.IsTrue(Covers(DispX, DispY, DispW, DispH),
                "기존에 유일하게 동작하던 경로다. 이게 깨지면 회귀다.");
        }

        [Test]
        public void 네이티브_전체화면_실측_사각형이_잡힌다()
        {
            Assert.IsTrue(Covers(0.0, NativeY, DispW, NativeH),
                $"실측 (0,{NativeY},{DispW},{NativeH})가 전체화면으로 잡히지 않으면 원칙 2가 " +
                "네이티브 전체화면 게임에서 통째로 죽는다 — 이번 라운드가 고친 바로 그 버그다.");
        }

        // ====================================================================================
        // ★ 음성 대조 — "게임일 때만 숨는다"의 반대편(= 아무 때나 숨는다)을 새로 깨지 않게 잠근다
        // ====================================================================================

        [Test]
        public void 줌_최대화_창은_하단이_떠서_탈락한다()
        {
            // ★ 이 조건이 이번 규칙의 핵심 방벽이다. 상단 여백만 보고 통과시키면 Dock을 띄운 채
            //   최대화한 업무 창이 전부 전체화면으로 오판된다.
            Assert.IsFalse(Covers(0.0, ZoomY, DispW, ZoomH),
                $"줌 창 (0,{ZoomY},{DispW},{ZoomH})는 하단이 {ZoomY + ZoomH}로 디스플레이 하단 {DispH}와 " +
                "어긋난다 — 반드시 탈락해야 한다.");
        }

        [Test]
        public void 일반_창은_탈락한다()
        {
            Assert.IsFalse(Covers(200.0, 150.0, 900.0, 632.0));
            Assert.IsFalse(Covers(306.0, 454.0, 230.0, 408.0));   // 실측: 계산기 창
        }

        [Test]
        public void 반쪽화면_Split_View는_전폭이_아니라_탈락한다()
        {
            Assert.IsFalse(Covers(0.0, NativeY, DispW / 2.0, NativeH), "왼쪽 반쪽.");
            Assert.IsFalse(Covers(DispW / 2.0, NativeY, DispW / 2.0, NativeH), "오른쪽 반쪽.");
        }

        [Test]
        public void 상단_여백이_허용치를_넘으면_탈락한다()
        {
            double limit = DispH * FullscreenGeometry.MenuBarStripFraction;

            // 허용치 바로 안쪽 — 하단 밀착을 유지한 채 상단 여백만 키운다.
            double inside = limit - 1.0;
            Assert.IsTrue(Covers(0.0, inside, DispW, DispH - inside),
                $"상단 여백 {inside:F1}pt는 허용치 {limit:F1}pt 안이므로 통과해야 한다.");

            // 허용치 바깥 — 여기부터는 "전체화면"이라고 부를 수 없다.
            double outside = limit + 2.0;
            Assert.IsFalse(Covers(0.0, outside, DispW, DispH - outside),
                $"상단 여백 {outside:F1}pt는 허용치 {limit:F1}pt를 넘었는데 통과했다.");
        }

        [Test]
        public void 퇴화_사각형은_절대_전체화면이_아니다()
        {
            // ★ 이번 버그의 진범 계열: macOS가 네이티브 전체화면마다 만드는 자동 숨김 타이틀바
            //   컨테이너(layer 0, alpha 0)와 같은 부류다. 알파 필터가 1차 방어이고 이건 2차다.
            Assert.IsFalse(Covers(0.0, 0.0, 0.0, 0.0));
            Assert.IsFalse(Covers(0.0, DispY, DispW, 0.0));
            Assert.IsFalse(Covers(0.0, DispY, 0.0, DispH));
        }

        [Test]
        public void 화면_위로_삐져나온_창은_탈락한다()
        {
            Assert.IsFalse(Covers(0.0, -20.0, DispW, DispH + 20.0),
                "상단 여백이 음수인 창은 '메뉴바만 남긴 전체화면'이 아니다.");
        }

        // ====================================================================================
        // ★★ 네거티브 컨트롤 — 반증된 처방이 이 테스트를 통과하지 못한다
        // ====================================================================================

        [Test]
        public void 반증된_safeAreaInsets_상한_32는_실측_33을_못_넘는다()
        {
            // 이 라운드에서 제안됐다가 실측으로 기각된 상한 후보들. 하나라도 33 이상이면
            // "그 상수를 쓰면 되지 않나"는 제안이 다시 살아난다 — 그게 아님을 여기서 못 박는다.
            double[] rejected = { 32.0 /* safeAreaInsets.top */, 22.0 /* statusThick */, 32.0 /* auxiliaryTopLeftArea */ };
            foreach (double candidate in rejected)
            {
                Assert.Less(candidate + FullscreenGeometry.Epsilon, NativeY,
                    $"상한 후보 {candidate}pt(+eps {FullscreenGeometry.Epsilon})로는 실측 상단 여백 " +
                    $"{NativeY}pt를 덮지 못한다. 그래서 절대 상수가 아니라 비율을 쓴다.");
            }

            // 채택한 비율 상한은 실측을 충분한 여유로 덮는다(검산: 33/982 = 3.36% < 5%).
            double limit = DispH * FullscreenGeometry.MenuBarStripFraction;
            Assert.Greater(limit, NativeY,
                $"비율 상한 {limit:F1}pt가 실측 {NativeY}pt를 못 덮으면 이번 수정은 동작 변화가 0이다.");
            Assert.Greater(limit / NativeY, 1.2,
                "여유가 1.2배도 안 되면 다음 기기/스케일링에서 또 0/N으로 실패한다.");
        }

        [Test]
        public void 정확일치_전용_판정은_네이티브_전체화면을_잡지_않는다()
        {
            // Windows가 계속 쓰는 관용 없는 쪽. 두 함수가 실제로 다른 답을 낸다는 것을 잠근다 —
            // 같아져 버리면 Windows 쪽 "의도적 분기" 주석이 조용히 거짓이 된다.
            Assert.IsFalse(FullscreenGeometry.MatchesExactly(0.0, NativeY, DispW, NativeH,
                DispX, DispY, DispW, DispH, FullscreenGeometry.Epsilon));
            Assert.IsTrue(FullscreenGeometry.MatchesExactly(DispX, DispY, DispW, DispH,
                DispX, DispY, DispW, DispH, FullscreenGeometry.Epsilon));
        }

        [Test]
        public void 디스플레이가_퇴화면_어떤_창도_전체화면이_아니다()
        {
            Assert.IsFalse(FullscreenGeometry.CoversDisplay(0, 0, 100, 100, 0, 0, 0, 0,
                FullscreenGeometry.Epsilon));
        }
    }
}
