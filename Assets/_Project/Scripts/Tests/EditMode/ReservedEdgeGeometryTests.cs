using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 2026-09-03 (debugger) — <b>예약 띠 뺄셈을 「값」으로 재는 첫 테스트.</b>
    ///
    /// ============================================================================
    /// 왜 이 파일이 생겼나 — 없어서 사고가 났다
    /// ============================================================================
    /// 이 저장소의 예약 띠 검사는 전부 <b>소스 텍스트 감사</b>(그 문자열이 있는가)이거나
    /// <b>스텁 주입</b>(내가 33을 심으면 33이 나오는가)이었다. 즉
    /// <i>"화면 (0,0,1512,982)와 작업영역 (0,75,1512,874)를 실제로 넣으면 상단이 33이 되는가"</i>를
    /// 재는 것이 <b>0건</b>이었다.
    ///
    /// <para>그 공백에서 2026-09-03 새벽에 사고가 났다. 앱 로그가 <i>"OS 예약 띠 0.0pt"</i>를 찍었고,
    /// 그 한 줄을 근거로 <i>"메뉴 막대 수정이 이 기계에서 아무것도 안 고쳤다"</i>는 판정이 나왔다.
    /// <b>확인할 방법이 없었기 때문에</b> 그 판정을 반증할 수도 증명할 수도 없었다.</para>
    ///
    /// ============================================================================
    /// ★ 기대값의 출처 — 프로덕션 함수가 아니다 (docs/TEAM.md 「생성기와 검사기가 같이 틀린다」)
    /// ============================================================================
    /// 아래 상수는 <b>앱과 무관한 독립 경로</b>로 잰 값이다:
    /// <list type="number">
    ///  <item><c>StickMate.app</c> 안의 <c>LibUniWinC</c> 네이티브 바이너리를 <c>dlopen</c>해서
    ///        C#이 P/Invoke하는 <b>바로 그 심볼</b> <c>GetMonitorRectangle</c>을 직접 호출.</item>
    ///  <item>같은 프로세스에서 <c>NSScreen.visibleFrame</c> / <c>CGDisplayBounds</c>를 대조군으로 조회.</item>
    /// </list>
    /// 셋이 <b>소수점까지 일치</b>했다. 만약 기대값을 프로덕션 조회로 만들었다면 이 파일은
    /// "함수가 자기 자신과 같다"만 증명하고 <b>단위가 픽셀로 바뀌어도 초록</b>이었을 것이다.
    ///
    /// ============================================================================
    /// 이 테스트가 <b>못 재는 것</b> (정직하게 적는다 — 실기 확인 항목)
    /// ============================================================================
    /// 에디터/배치모드에는 <b>우리 오버레이 창도, 부착된 <c>LibUniWinC</c>도 없다</b>.
    /// 그래서 여기서 재는 것은 <b>산술과 가드</b>뿐이고, <i>"실기에서 조회가 실제로 그 값을
    /// 돌려주는가"</i>는 <b>구조적으로 잴 수 없다</b>. <c>Assert.Ignore</c>로 감추지 않고
    /// <see cref="실기_확인_항목_이_테스트가_대신할_수_없는_것"/>에 <b>실행되는 형태로</b> 남긴다.
    /// </summary>
    public sealed class ReservedEdgeGeometryTests
    {
        private const string LogPrefix = "[예약띠산술]";

        // ── 이 개발 머신(14" M2 Pro, 배율 2.0, Dock 하단)의 독립 실측값 ──────────────
        //    출처는 클래스 문서의 "기대값의 출처" 절. 프로덕션 함수에서 온 값이 아니다.
        private static readonly Rect 화면전체_실측 = new Rect(0f, 0f, 1512f, 982f);   // CGDisplayBounds
        private static readonly Rect 작업영역_실측 = new Rect(0f, 75f, 1512f, 874f);  // NSScreen.visibleFrame

        private const float 메뉴막대_실측pt = 33f;   // 982 − (75 + 874)
        private const float Dock_실측pt = 75f;       // visibleFrame.y

        // ── 라이브러리가 창을 붙잡기 전에 GetMonitorRect(0)이 내는 값 ───────────────
        //    실측: GetMonitorCount()=0, GetMonitorRectangle(0)=false -> UniWindowController가 Rect.zero.
        private static readonly Rect 부착전_작업영역 = Rect.zero;

        /// <summary>
        /// ★★ <b>이 파일의 본론.</b> 이 기계의 두 사각형을 넣으면 상단이 33pt로 나오는가.
        /// 이 단언이 없어서 "메뉴 막대를 고쳤는가"를 아무도 확인할 수 없었다.
        /// </summary>
        [Test]
        public void 이_머신_두_사각형에서_상단_33pt와_하단_75pt가_나온다()
        {
            Assert.IsTrue(
                ReservedEdgeGeometry.TryFromDisplayAndVisibleFrame(화면전체_실측, 작업영역_실측, out var insets),
                $"{LogPrefix} 실측 사각형 한 쌍에서 한 변도 못 쟀습니다 — 이 조합이 실패하면 " +
                "실기에서도 메뉴 막대를 영원히 못 잡습니다.");

            Assert.IsTrue(insets.IsMeasured(ReservedEdge.All),
                $"{LogPrefix} 네 변이 전부 측정으로 표시되지 않았습니다: {insets}");

            Assert.AreEqual(메뉴막대_실측pt, insets.PointsFor(ReservedEdge.Top), 0.001f,
                $"{LogPrefix} 상단이 메뉴 막대 실측(33pt)과 다릅니다: {insets}");
            Assert.AreEqual(Dock_실측pt, insets.PointsFor(ReservedEdge.Bottom), 0.001f,
                $"{LogPrefix} 하단이 Dock 실측(75pt)과 다릅니다: {insets}");
            Assert.AreEqual(0f, insets.PointsFor(ReservedEdge.Left), 0.001f, $"{LogPrefix} 좌 {insets}");
            Assert.AreEqual(0f, insets.PointsFor(ReservedEdge.Right), 0.001f, $"{LogPrefix} 우 {insets}");
        }

        /// <summary>
        /// ★ <b>단위가 픽셀로 바뀌면 즉시 빨개진다.</b> 이 저장소가 이번에 세운 가설 중 하나가
        /// <i>"조회가 포인트가 아니라 픽셀을 돌려주는 것 아니냐"</i>였다(실측으로 반증됐다).
        /// 그 가설이 <b>참이 되는 날</b>(라이브러리 갱신 등)을 이 단언이 잡는다 — 배율 2.0에서
        /// 픽셀 값은 상식 클램프(높이의 25%)를 훌쩍 넘어 상단이 <b>미측정</b>이 된다.
        /// </summary>
        [Test]
        public void 조회가_픽셀로_바뀌면_조용히_통과하지_않는다()
        {
            var 픽셀_화면 = new Rect(0f, 0f, 3024f, 1964f);
            var 픽셀_작업영역 = new Rect(0f, 150f, 3024f, 1748f);

            // (양성 대조) 픽셀끼리 짝이 맞으면 산술 자체는 성립한다 — 66px = 33pt × 2.
            Assert.IsTrue(
                ReservedEdgeGeometry.TryFromDisplayAndVisibleFrame(픽셀_화면, 픽셀_작업영역, out var 픽셀쌍),
                $"{LogPrefix} 픽셀끼리 짝지은 대조군이 실패했습니다 — 이 대조가 죽으면 아래 판정이 무의미합니다.");
            Assert.AreEqual(66f, 픽셀쌍.PointsFor(ReservedEdge.Top), 0.001f,
                $"{LogPrefix} 픽셀 쌍의 상단이 66이 아닙니다: {픽셀쌍}");

            // (본 판정) 한쪽만 픽셀이면 = 단위 혼용. 상단은 반드시 '모름'이어야 한다.
            Assert.IsFalse(
                ReservedEdgeGeometry.TryFromDisplayAndVisibleFrame(화면전체_실측, 픽셀_작업영역, out var 혼용),
                $"{LogPrefix} 포인트 화면 + 픽셀 작업영역이 통과했습니다 — 단위 혼용이 조용히 " +
                $"값을 냅니다: {혼용}");
            Assert.IsFalse(혼용.IsMeasured(ReservedEdge.Top),
                $"{LogPrefix} 단위가 혼용됐는데 상단이 '측정됨'입니다: {혼용}");
        }

        /// <summary>
        /// ★★ <b>2026-09-03 사고의 뿌리를 못박는다.</b>
        /// <c>LibUniWinC</c>는 모니터 목록을 캐시로 들고 있고 그 캐시를 채우는 경로는
        /// <c>_attachWindow</c> 하나뿐이다(배포 바이너리 역어셈블 + <c>dlopen</c> 실측 둘 다 확인).
        /// 창 부착 전에는 <c>GetMonitorRect(0)</c>이 <see cref="Rect.zero"/>다 —
        /// 이 머신에서 부착까지 <b>2.19초</b>가 걸렸고, 그동안의 0은
        /// <b>"띠가 없다"가 아니라 "아직 못 쟀다"</b>여야 한다.
        /// </summary>
        [Test]
        public void 부착_전_Rect_zero는_측정된_0이_아니라_모름이다()
        {
            Assert.IsFalse(
                ReservedEdgeGeometry.TryFromDisplayAndVisibleFrame(화면전체_실측, 부착전_작업영역, out var insets),
                $"{LogPrefix} 라이브러리 미부착 상태가 '측정 성공'으로 보고됐습니다.");

            Assert.AreEqual(ReservedEdge.None, insets.MeasuredEdges,
                $"{LogPrefix} 미부착인데 측정 비트가 남았습니다: {insets}. 이 비트가 켜지면 소비 측이 " +
                "「OS가 아무것도 예약하지 않았다」는 거짓 사실을 받습니다 — 그것이 이번 사고의 형태입니다.");

            // ★ 양성 대조 — 같은 함수가 부착 후 값에서는 실제로 성공한다.
            //   이게 없으면 위 단언은 "함수가 늘 false다"여도 초록이다.
            Assert.IsTrue(
                ReservedEdgeGeometry.TryFromDisplayAndVisibleFrame(화면전체_실측, 작업영역_실측, out _),
                $"{LogPrefix} 양성 대조 실패 — 부착 후 값에서도 false입니다. 위 '미측정' 단언은 무효입니다.");
        }

        /// <summary>
        /// ★★ <b>멀티모니터 — 짝이 어긋나면 존재하지 않는 메뉴 막대가 「측정됨」으로 나왔다.</b>
        ///
        /// <para>macOS 네이티브의 모니터 정렬은 <i>"가장 왼쪽 먼저"</i>라 <b>인덱스 0이 주 화면이
        /// 아닐 수 있다</b>. 주 화면 1512×982 <b>왼쪽</b>에 1280×800 외장을 붙이면 인덱스 0이
        /// 그 외장이 되고, 짝이 어긋난 채로 뺄셈이 돈다.</para>
        ///
        /// <para>이 테스트는 <b>결함이 실재했음을 먼저 보이고</b>(옛 식을 그대로 계산해 182가 나오는 것),
        /// 그 다음 가드가 그것을 막는지 본다. 결함 재현이 없으면 이 단언은 "아무 사각형이나 거부한다"와
        /// 구분되지 않는다.</para>
        /// </summary>
        [Test]
        public void 짝이_어긋난_모니터는_존재하지_않는_182pt_띠를_만들었다()
        {
            var 주화면 = 화면전체_실측;                                  // (0,0 1512x982)
            var 왼쪽외장_작업영역 = new Rect(-1280f, 0f, 1280f, 800f);   // Cocoa 전역 좌표

            // (1) 결함 재현 — 옛 식(가드 없음)을 그대로 손으로 계산한다.
            float 옛_상단 = 주화면.height - (왼쪽외장_작업영역.y + 왼쪽외장_작업영역.height);
            Assert.AreEqual(182f, 옛_상단, 0.001f,
                $"{LogPrefix} 결함 재현이 깨졌습니다 — 이 숫자가 182가 아니면 아래 판정이 다른 것을 잽니다.");
            Assert.Less(옛_상단, 주화면.height * ReservedEdgeGeometry.SanityMaxInsetFraction,
                $"{LogPrefix} 182pt가 상식 클램프에 걸려 버립니다 — 그렇다면 이 사고는 값 검사로 " +
                "막을 수 있었다는 뜻이고, 이 테스트의 전제가 틀렸습니다.");

            // (2) 가드 — 짝이 어긋난 쌍은 통째로 '모름'이다. 한 변만 접지 않는다.
            Assert.IsFalse(ReservedEdgeGeometry.IsSameDisplayPair(주화면, 왼쪽외장_작업영역),
                $"{LogPrefix} 어긋난 짝이 '같은 화면'으로 판정됐습니다.");
            Assert.IsFalse(
                ReservedEdgeGeometry.TryFromDisplayAndVisibleFrame(주화면, 왼쪽외장_작업영역, out var insets),
                $"{LogPrefix} 어긋난 짝에서 값이 나왔습니다: {insets}");
            Assert.AreEqual(ReservedEdge.None, insets.MeasuredEdges,
                $"{LogPrefix} 어긋난 짝인데 측정 비트가 남았습니다: {insets}");

            // (3) ★ 하단이 특히 위험했다 — visible.y = 0이라 「Dock 없음을 확인했다」가 될 뻔했다.
            Assert.AreEqual(0f, insets.PointsFor(ReservedEdge.Bottom), 0.001f,
                $"{LogPrefix} 값은 0이어야 하고(짐작 금지), 위 단언대로 비트는 없어야 합니다.");
            Assert.IsFalse(insets.IsMeasured(ReservedEdge.Bottom),
                $"{LogPrefix} 하단이 '측정된 0'으로 나왔습니다 — 실제 이 화면의 Dock은 75pt입니다. " +
                "이것이 계약 문서가 절대 내지 않겠다고 적은 바로 그 거짓 사실입니다.");

            // (4) 양성 대조 — 같은 가드가 옳은 짝은 통과시킨다.
            Assert.IsTrue(ReservedEdgeGeometry.IsSameDisplayPair(화면전체_실측, 작업영역_실측),
                $"{LogPrefix} 가드가 옳은 짝까지 막습니다 — 그러면 이 기계에서 띠가 영원히 0입니다.");
        }

        /// <summary>오른쪽에 붙은 외장이 인덱스 0이 되는 배치도 같은 형태로 걸러진다.</summary>
        [Test]
        public void 오른쪽으로_삐져나간_짝도_걸러진다()
        {
            var 오른쪽외장 = new Rect(1512f, 0f, 1280f, 800f);
            Assert.IsFalse(ReservedEdgeGeometry.IsSameDisplayPair(화면전체_실측, 오른쪽외장),
                $"{LogPrefix} 화면 밖으로 나간 작업영역이 통과했습니다.");
            Assert.IsFalse(
                ReservedEdgeGeometry.TryFromDisplayAndVisibleFrame(화면전체_실측, 오른쪽외장, out var insets),
                $"{LogPrefix} {insets}");
        }

        /// <summary>
        /// <b>진짜 0은 진짜 0이다.</b> 메뉴 막대와 Dock을 둘 다 자동 숨김으로 두면
        /// <c>visibleFrame</c>이 화면 전체와 같아지고, 그때 네 변 0은
        /// <b>"OS가 그 띠를 예약하지 않았다"는 확정 사실</b>이다 — 미측정과 달리 비트가 켜져야 한다.
        /// 이 구분이 무너지면 "0"의 뜻이 두 가지가 되어 원격 진단이 다시 틀린다.
        /// </summary>
        [Test]
        public void 자동숨김의_0은_측정된_0이다()
        {
            Assert.IsTrue(
                ReservedEdgeGeometry.TryFromDisplayAndVisibleFrame(화면전체_실측, 화면전체_실측, out var insets),
                $"{LogPrefix} 자동 숨김 상태가 '못 쟀다'로 보고됐습니다.");
            Assert.AreEqual(ReservedEdge.All, insets.MeasuredEdges,
                $"{LogPrefix} 자동 숨김인데 측정 비트가 모자랍니다: {insets}");
            Assert.AreEqual(0f, insets.PointsFor(ReservedEdge.Top), 0.001f, $"{LogPrefix} {insets}");
        }

        /// <summary>
        /// 상식 범위를 넘는 변은 <b>그 변만</b> 접힌다 — 나머지는 살아남는다.
        /// (짝 자체가 어긋난 경우와 다르다. 그쪽은 통째로 접는다.)
        /// </summary>
        [Test]
        public void 상식_범위를_넘는_변만_접히고_나머지는_살아남는다()
        {
            // 하단 Dock이 화면 높이의 25%를 넘게 두껍다고 나오는 경우(높이 982 -> 임계 245.5).
            float 임계 = 화면전체_실측.height * ReservedEdgeGeometry.SanityMaxInsetFraction;
            float 과한하단 = 임계 + 10f;
            var 작업영역 = new Rect(0f, 과한하단, 1512f, 982f - 과한하단 - 33f);

            Assert.IsTrue(
                ReservedEdgeGeometry.TryFromDisplayAndVisibleFrame(화면전체_실측, 작업영역, out var insets),
                $"{LogPrefix} 한 변만 이상한데 전부 버렸습니다: {insets}");
            Assert.IsFalse(insets.IsMeasured(ReservedEdge.Bottom),
                $"{LogPrefix} 상식 범위를 넘은 하단이 측정됨으로 남았습니다: {insets}");
            Assert.IsTrue(insets.IsMeasured(ReservedEdge.Top),
                $"{LogPrefix} 멀쩡한 상단까지 함께 접혔습니다: {insets}");
            Assert.AreEqual(33f, insets.PointsFor(ReservedEdge.Top), 0.001f, $"{LogPrefix} {insets}");
        }

        /// <summary>
        /// ★ <b>리더 질의에 대한 답을 실행되는 형태로 남긴다</b>: <i>"음수를 어떻게 다루는가.
        /// 클래스 문서는 「음수는 관측이 아니다」라고 적었는데 상식 클램프는 위쪽만 자른다"</i>.
        ///
        /// <para><b>답: 문서는 참이다.</b> 접는 주체가 상식 클램프가 아니라
        /// <see cref="ReservedEdgeInsets.Observed"/>일 뿐이다. 두 겹이고, 여기서는 아래쪽 겹을 직접 잰다
        /// (위쪽 겹은 포함 가드가 생긴 뒤로 음수를 애초에 도달시키지 않는다 —
        /// <b>그래서 더더욱 이 겹이 살아 있는지 따로 재야 한다</b>).</para>
        /// </summary>
        [Test]
        public void 음수는_그_변만_미측정으로_접힌다()
        {
            ReservedEdgeInsets insets = ReservedEdgeInsets.Observed(-98f, 75f, 0f, 0f);

            Assert.IsFalse(insets.IsMeasured(ReservedEdge.Top),
                $"{LogPrefix} 음수 상단이 '측정됨'으로 남았습니다: {insets}");
            Assert.AreEqual(0f, insets.PointsFor(ReservedEdge.Top), 0.001f,
                $"{LogPrefix} 음수가 값으로 새어 나왔습니다: {insets}");

            // 양성 대조 — 같은 호출에서 멀쩡한 변은 살아남는다(전부 접는 함수가 아니다).
            Assert.IsTrue(insets.IsMeasured(ReservedEdge.Bottom), $"{LogPrefix} {insets}");
            Assert.AreEqual(75f, insets.PointsFor(ReservedEdge.Bottom), 0.001f, $"{LogPrefix} {insets}");
        }

        /// <summary>NaN·무한대 성분이 포함 검사를 <b>조용히 통과</b>하지 않는다.
        /// (부동소수 비교는 NaN에 대해 모든 부등호가 false라 가드가 통째로 무력화될 수 있다.)</summary>
        [Test]
        public void NaN_성분은_포함_검사를_통과하지_못한다()
        {
            var nanX = new Rect(float.NaN, 75f, 1512f, 874f);
            Assert.IsFalse(ReservedEdgeGeometry.IsSameDisplayPair(화면전체_실측, nanX),
                $"{LogPrefix} NaN x가 포함 검사를 통과했습니다 — 모든 부등호가 false가 되기 때문입니다.");
            Assert.IsFalse(ReservedEdgeGeometry.TryFromDisplayAndVisibleFrame(화면전체_실측, nanX, out _),
                $"{LogPrefix} NaN 작업영역에서 값이 나왔습니다.");

            var infY = new Rect(0f, float.PositiveInfinity, 1512f, 874f);
            Assert.IsFalse(ReservedEdgeGeometry.TryFromDisplayAndVisibleFrame(화면전체_실측, infY, out _),
                $"{LogPrefix} 무한대 작업영역에서 값이 나왔습니다.");
        }

        /// <summary>
        /// ★ <b>실기 확인 항목</b> — <c>Assert.Ignore</c>로 감추지 않고 실행되는 형태로 남긴다.
        ///
        /// <para>에디터/배치모드에는 우리 오버레이 창도, 부착된 <c>LibUniWinC</c>도, 메뉴 막대 개념도
        /// 없다. 그래서 <b>이 파일이 구조적으로 못 재는 것</b>이 정확히 둘이다:</para>
        /// <list type="number">
        ///  <item><b>실기에서 두 조회가 실제로 이 사각형을 돌려주는가</b> — 위 상수는 독립 실측이지만
        ///        <i>앱 프로세스 안에서</i> 같은 값이 나오는지는 빌드해서 봐야 한다.
        ///        확인 방법: 앱 로그 <c>[모니터지형]</c> 줄의 <c>라이브러리 L0</c>와 <c>OS0 전체</c>.</item>
        ///  <item><b>소비 측이 부착 이후 값을 실제로 다시 읽는가</b> — 기동 배너 한 줄만 보고
        ///        판정하면 2026-09-03 사고를 그대로 반복한다.</item>
        /// </list>
        ///
        /// <para>여기서는 <b>그 두 값이 서로 모순되지 않는지</b>만 잠근다: 상수를 고치는 사람이
        /// 상단·하단·화면 높이를 따로따로 고쳐 <b>합이 안 맞는 세계</b>를 만들지 못하게 한다.</para>
        /// </summary>
        [Test]
        public void 실기_확인_항목_이_테스트가_대신할_수_없는_것()
        {
            Assert.AreEqual(화면전체_실측.height,
                메뉴막대_실측pt + 작업영역_실측.height + Dock_실측pt, 0.001f,
                $"{LogPrefix} 상수 사이의 항등식이 깨졌습니다 — 메뉴막대 + 작업영역 높이 + Dock 이 " +
                "화면 높이와 같아야 합니다. 한 값만 고치면 이 파일 전체가 존재하지 않는 화면을 잽니다.");

            Assert.AreEqual(작업영역_실측.y, Dock_실측pt, 0.001f,
                $"{LogPrefix} Cocoa 좌하단 원점 규약이 깨졌습니다 — visibleFrame.y가 곧 하단 띠입니다.");

            Assert.AreEqual(화면전체_실측.width, 작업영역_실측.width, 0.001f,
                $"{LogPrefix} 이 머신은 Dock이 하단이라 좌·우 띠가 0이어야 합니다.");
        }
    }
}
