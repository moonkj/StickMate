using System.IO;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ============================================================================
    /// 맥 실기 로그(2026-09-01, PID 11451) — "오버레이 창 원점이 두 값 사이를 오간다"
    /// ============================================================================
    /// <code>
    ///   origin=(0.00,  0.00), size=(1512x982),  Screen=(3024x1964)   ... A
    ///   origin=(0.00, 33.00), size=(1512x1010), Screen=(3024x1964)   ... B
    /// </code>
    ///
    /// <b>확정된 것 1 — 산술 모순은 없다.</b> <c>33 + 1010 = 1043 &gt; 982</c>(화면 높이)라 처음에는
    /// "두 값이 서로 모순이라 원리적으로 수렴할 수 없다"고 읽혔지만 <b>모순이 아니다</b>.
    /// <c>size</c>는 <c>kCGWindowBounds</c>, 즉 <b>frame</b> 사각형이고 B는 <b>타이틀바가 붙어 있는
    /// 상태</b>다. 같은 로그의 기동 구간이 이것을 못박는다:
    /// <list type="bullet">
    ///   <item>CGWindow(frame) = (0, 33, 1512, 1010)</item>
    ///   <item>같은 순간 라이브러리 보고: windowPosition=(0,-61), windowSize=clientSize=(1512,982)</item>
    ///   <item>1010 - 982 = <b>28</b> = macOS 타이틀바, 982 - (-61) - 1010 = <b>33</b> = 메뉴바</item>
    /// </list>
    /// 타이틀바가 있는 창은 아래쪽이 화면 밖으로 나가도 된다(AppKit이 제약하는 것은 타이틀바뿐).
    /// B 상태의 <b>콘텐츠</b> 사각형은 (0, 61, 1512, 982)다.
    ///
    /// <b>확정된 것 2 — B는 "기동 중인 StickMate 창"의 모양이다.</b> 위 값은 우리 자신의 기동
    /// 구간(부착 전 2.3초)에 찍힌 값과 바이트 단위로 같다.
    ///
    /// <b>확정되지 않은 것(정직하게)</b> — 18:05~18:06의 <b>교대</b>가 왜 일어났는가.
    /// 그 시각에 조사용 <b>두 번째 인스턴스</b>가 떠 있었고, 그때
    /// <c>MacWindowService.IsSelfWindow</c>의 <b>이름 기반 폴백</b>이 남의 인스턴스 창을 "내 창"으로
    /// 통과시키고 있었다(같은 라운드에 <c>IsSelfProcessWindow</c>/<c>IsOwnAppWindow</c> 분리로 수정).
    /// <b>인스턴스가 1개일 때도 교대가 재현되는지는 아직 확인되지 않았다.</b>
    ///
    /// <para>이 파일은 세 가지를 실행으로 잠근다:</para>
    /// <list type="number">
    ///   <item><see cref="OverlayContentRectPolicy"/> — frame에서 콘텐츠를 뽑아낸다(우리 자신의
    ///         기동 구간이 실측으로 확인된 오보고 구간이다).</item>
    ///   <item><see cref="OverlayGeometryOscillationGuard"/> — <b>어떤 이유로든</b> 값이 A↔B로 오가면
    ///         N회 뒤 재적용을 멈춘다. 불감대는 1px 래칫만 막고 A/B 진동은 원리적으로 못 막는다.</item>
    ///   <item>좌표계의 출처는 PID로만 고른다 — 같은 앱의 두 번째 인스턴스는 이름이 정확히 같다.</item>
    /// </list>
    ///
    /// <para><b>숫자 규약</b>(CLAUDE.md): 프로덕션 상수는 절대 베끼지 않고 상수를 <b>참조</b>한다.
    /// 아래에 나오는 1512/982/1010/33/61/3024/1964는 프로덕션 상수가 아니라 <b>실기 로그 실측값</b>이며,
    /// 그것이 이 테스트가 재현하려는 사실 그 자체다.</para>
    /// </summary>
    public class OverlayGeometryOscillationTests
    {
        // 실기 실측(2026-09-01 PID 11451). 프로덕션 상수가 아니라 관측 사실이다.
        private static readonly Rect FrameBorderless = new Rect(0f, 0f, 1512f, 982f);      // A
        private static readonly Rect FrameTitled = new Rect(0f, 33f, 1512f, 1010f);        // B
        private static readonly Vector2 ContentSize = new Vector2(1512f, 982f);            // clientSize
        private const int BackbufferPixelW = 3024;
        private const int BackbufferPixelH = 1964;
        private const float TitleBarPoints = 28f;   // 1010 - 982
        private const float MenuBarPoints = 33f;

        // ────────────────────────────────────────────────────────────────────────
        // 1. frame -> content : 두 상태가 "무엇이었는지"를 산술로 확정한다
        // ────────────────────────────────────────────────────────────────────────

        [Test]
        public void 타이틀바가_붙은_frame에서_콘텐츠_사각형을_정확히_뽑아낸다()
        {
            bool stripped = OverlayContentRectPolicy.TryStripTopDecoration(
                FrameTitled, ContentSize, OverlayContentRectPolicy.DefaultEpsilonPoints,
                out Rect content, out float top);

            Assert.IsTrue(stripped, "타이틀바가 붙은 frame은 반드시 보정돼야 한다 — 보정하지 않으면 " +
                "ScreenCoordinateConverter의 원점이 28pt 위로 밀려 발판/커서 판정이 통째로 어긋난다.");
            Assert.AreEqual(TitleBarPoints, top, 0.01f, "걷어낸 두께는 macOS 타이틀바(28pt)여야 한다.");
            Assert.AreEqual(new Rect(0f, MenuBarPoints + TitleBarPoints, ContentSize.x, ContentSize.y), content,
                "B 상태의 콘텐츠 사각형은 (0,61,1512,982)다 — 같은 로그의 이전 라운드가 실측으로 " +
                "'Quartz 원점=(0,61)'이라고 남긴 값과 정확히 같아야 한다.");
        }

        [Test]
        public void 보더리스_frame은_손대지_않는다()
        {
            bool stripped = OverlayContentRectPolicy.TryStripTopDecoration(
                FrameBorderless, ContentSize, OverlayContentRectPolicy.DefaultEpsilonPoints,
                out Rect content, out float top);

            Assert.IsFalse(stripped, "보더리스(정상 경로)에서는 보정이 일어나면 안 된다.");
            Assert.AreEqual(FrameBorderless, content);
            Assert.AreEqual(0f, top);
        }

        [Test]
        public void 콘텐츠_크기를_모르면_아무것도_바꾸지_않는다()
        {
            // 창 부착 전 라이브러리는 clientSize=(0,0)을 돌려준다.
            Assert.IsFalse(OverlayContentRectPolicy.TryStripTopDecoration(
                FrameTitled, Vector2.zero, OverlayContentRectPolicy.DefaultEpsilonPoints,
                out Rect content, out _),
                "모르는 값으로 좌표계를 옮기는 것이 가장 나쁘다 — 모르면 원본을 그대로 쓴다.");
            Assert.AreEqual(FrameTitled, content);
        }

        [Test]
        public void 장식이라기엔_너무_두꺼우면_보정하지_않는다()
        {
            float tooThick = OverlayContentRectPolicy.MaxTopDecorationPoints + 1f;
            var brokenFrame = new Rect(0f, 0f, ContentSize.x, ContentSize.y + tooThick);

            Assert.IsFalse(OverlayContentRectPolicy.TryStripTopDecoration(
                brokenFrame, ContentSize, OverlayContentRectPolicy.DefaultEpsilonPoints,
                out _, out _),
                "장식 두께 상한을 넘는 차이는 '측정이 깨진 것'이다. 깨진 값으로 원점을 옮기면 " +
                "캐릭터가 화면 밖으로 튄다.");

            // 상한 바로 안쪽은 보정한다(경계가 상수를 실제로 따라가는지 확인).
            var okFrame = new Rect(0f, 0f, ContentSize.x,
                ContentSize.y + OverlayContentRectPolicy.MaxTopDecorationPoints);
            Assert.IsTrue(OverlayContentRectPolicy.TryStripTopDecoration(
                okFrame, ContentSize, OverlayContentRectPolicy.DefaultEpsilonPoints, out _, out _));
        }

        [Test]
        public void 폭이_다르면_보정하지_않는다()
        {
            var sideDecorated = new Rect(0f, 0f, ContentSize.x + 8f, ContentSize.y + TitleBarPoints);

            Assert.IsFalse(OverlayContentRectPolicy.TryStripTopDecoration(
                sideDecorated, ContentSize, OverlayContentRectPolicy.DefaultEpsilonPoints, out _, out _),
                "좌우 장식이 있는 형상은 '위쪽만 걷어낸다'는 이 규칙의 전제 밖이다 — 조용히 포기해야 한다.");
        }

        [Test]
        public void 부착_전에는_백버퍼로_콘텐츠_크기를_유도한다()
        {
            Assert.IsTrue(OverlayContentRectPolicy.TryDeriveContentSizeFromBackbuffer(
                FrameTitled, BackbufferPixelW, BackbufferPixelH, out Vector2 derived));

            Assert.AreEqual(ContentSize.x, derived.x, 0.01f);
            Assert.AreEqual(ContentSize.y, derived.y, 0.01f,
                "1964 x (1512 / 3024) = 982 — 라이브러리가 부착 후 보고한 clientSize와 같아야 한다. " +
                "다르면 기동 직후 몇 초 동안 원점이 28pt 틀린 채로 첫 발판 판정이 돈다.");

            // 그 유도값으로 곧바로 보정까지 성립해야 한다(두 단계가 실제로 맞물리는지).
            Assert.IsTrue(OverlayContentRectPolicy.TryStripTopDecoration(
                FrameTitled, derived, OverlayContentRectPolicy.DefaultEpsilonPoints,
                out Rect content, out _));
            Assert.AreEqual(MenuBarPoints + TitleBarPoints, content.y, 0.01f);
        }

        [Test]
        public void 백버퍼가_아직_없으면_유도하지_않는다()
        {
            Assert.IsFalse(OverlayContentRectPolicy.TryDeriveContentSizeFromBackbuffer(
                FrameTitled, 0, 0, out Vector2 derived));
            Assert.AreEqual(Vector2.zero, derived);
        }

        // ────────────────────────────────────────────────────────────────────────
        // 2. A→B→A→B 진동 : N회 뒤 재적용이 멈춘다
        // ────────────────────────────────────────────────────────────────────────

        [Test]
        public void 진동은_상한_횟수에_도달하면_재적용을_멈춘다()
        {
            var guard = new OverlayGeometryOscillationGuard();
            int latchedAt = -1;

            // A B A B ... 를 상한보다 넉넉히 많이 넣는다. 첫 표본은 기준값이라 교대로 세지 않는다.
            int samples = (OverlayGeometryOscillationGuard.DefaultAlternationsToLatch + 4) * 2;
            for (int i = 0; i < samples; i++)
            {
                bool justLatched = guard.Observe(i % 2 == 0 ? FrameBorderless : FrameTitled);
                if (justLatched && latchedAt < 0) latchedAt = i;
            }

            Assert.IsTrue(guard.IsOscillating,
                "A↔B 진동은 반드시 확정돼야 한다 — 두 값의 차이(28~33pt)가 불감대(2pt)보다 크므로 " +
                "재적용은 원리적으로 수렴할 수 없다.");
            Assert.AreEqual(OverlayGeometryOscillationGuard.DefaultAlternationsToLatch,
                guard.AlternationCount,
                "래치 시점의 교대 횟수는 상수와 정확히 같아야 한다(상수를 참조해 검증 — 숫자를 베끼지 않는다).");
            Assert.GreaterOrEqual(latchedAt, 0, "래치 순간을 알리는 반환값이 정확히 한 번 나와야 한다.");
            StringAssert.Contains("교대", guard.Diagnosis, "사람이 읽을 진단 문구가 비어 있으면 안 된다.");
        }

        [Test]
        public void 래치_순간의_반환값은_정확히_한_번만_참이다()
        {
            var guard = new OverlayGeometryOscillationGuard();
            int trueCount = 0;
            for (int i = 0; i < 40; i++)
            {
                if (guard.Observe(i % 2 == 0 ? FrameBorderless : FrameTitled)) trueCount++;
            }

            Assert.AreEqual(1, trueCount,
                "경고 로그는 프로세스당 한 번이어야 한다 — 24시간 상주 앱에서 진동 경고가 " +
                "매 폴링마다 찍히면 그 자체로 로그가 잠긴다.");
        }

        [Test]
        public void 정상_정착은_진동으로_오판되지_않는다()
        {
            var guard = new OverlayGeometryOscillationGuard();

            // 한 번 이동(A -> B) 뒤 계속 같은 값. 정상 세션의 모양이다.
            guard.Observe(FrameTitled);
            for (int i = 0; i < 200; i++) guard.Observe(FrameBorderless);

            Assert.IsFalse(guard.IsOscillating,
                "정착한 세션에서 이 가드가 걸리면 Windows 기존 동작을 바꾸게 된다(회귀). " +
                "가드는 '수렴하지 않는다'는 사실에만 반응해야 한다.");
            Assert.AreEqual(0, guard.AlternationCount);
        }

        [Test]
        public void 불감대_안의_흔들림은_교대로_세지_않는다()
        {
            var guard = new OverlayGeometryOscillationGuard();
            float inside = OverlayGeometryOscillationGuard.DefaultEpsilonPoints * 0.5f;
            var jitter = new Rect(FrameBorderless.x, FrameBorderless.y + inside,
                FrameBorderless.width, FrameBorderless.height);

            for (int i = 0; i < 100; i++) guard.Observe(i % 2 == 0 ? FrameBorderless : jitter);

            Assert.IsFalse(guard.IsOscillating,
                "1px급 되읽기 오차는 OverlayBoundsFitPolicy의 불감대가 이미 담당한다. " +
                "이 가드까지 거기에 반응하면 정상 세션에서 재적합이 멈춰버린다.");
        }

        [Test]
        public void 제3의_값이_오면_교대_카운터가_초기화된다()
        {
            var guard = new OverlayGeometryOscillationGuard();
            var third = new Rect(0f, 0f, 1280f, 800f);   // 모니터를 바꾼 것처럼 완전히 다른 값

            // 상한 직전까지 교대시킨 뒤 제3의 값을 넣는다.
            for (int i = 0; i < OverlayGeometryOscillationGuard.DefaultAlternationsToLatch; i++)
            {
                guard.Observe(i % 2 == 0 ? FrameBorderless : FrameTitled);
            }
            int before = guard.AlternationCount;
            guard.Observe(third);

            Assert.Greater(before, 0, "먼저 교대가 실제로 쌓여 있어야 이 검증이 의미가 있다.");
            Assert.AreEqual(0, guard.AlternationCount,
                "제3의 값은 '진동'이 아니라 '이동 중'이다 — 모니터 전환의 중간 상태를 진동으로 " +
                "오판하면 정상적인 재적합이 막힌다.");
            Assert.IsFalse(guard.IsOscillating);
        }

        [Test]
        public void 한번_확정되면_스스로_풀리지_않는다()
        {
            var guard = new OverlayGeometryOscillationGuard();
            for (int i = 0; i < 40; i++) guard.Observe(i % 2 == 0 ? FrameBorderless : FrameTitled);
            Assert.IsTrue(guard.IsOscillating);

            for (int i = 0; i < 200; i++) guard.Observe(FrameBorderless);   // 조용해져도
            Assert.IsTrue(guard.IsOscillating,
                "자동으로 풀리면 진동이 다시 시작될 때 상한이 사실상 사라진다 — " +
                "_setResolutionCalls를 재무장에서 되돌리지 않는 것과 같은 이유다.");
        }

        // ────────────────────────────────────────────────────────────────────────
        // 3. 상수의 단일 출처 — 값이 두 벌로 갈라지면 한쪽만 고쳐진다
        // ────────────────────────────────────────────────────────────────────────

        [Test]
        public void 불감대_상수는_한_곳에서만_온다()
        {
            Assert.AreEqual(OverlayBoundsFitPolicy.DefaultEpsilonPixels,
                OverlayContentRectPolicy.DefaultEpsilonPoints,
                "창 장식 판정이 자기 불감대를 따로 들면 값이 두 벌로 갈라진다.");
            Assert.AreEqual(OverlayBoundsFitPolicy.DefaultEpsilonPixels,
                OverlayGeometryOscillationGuard.DefaultEpsilonPoints,
                "진동 가드가 자기 불감대를 따로 들면 값이 두 벌로 갈라진다.");
        }

        [Test]
        public void 실기의_두_사각형은_불감대로는_절대_흡수되지_않는다()
        {
            // 이 라운드의 출발점이 된 오해("불감대를 늘려 덮으면 되지 않나")를 숫자로 봉인한다.
            Assert.IsTrue(
                OverlayBoundsFitPolicy.ShouldMove(FrameBorderless.x, FrameBorderless.y,
                    FrameTitled.x, FrameTitled.y, OverlayBoundsFitPolicy.DefaultEpsilonPixels),
                "33pt 차이는 불감대(2pt) 밖이다.");
            Assert.Greater(MenuBarPoints, OverlayBoundsFitPolicy.DefaultEpsilonPixels * 10f,
                "불감대를 이 차이만큼 늘리면 사람이 인지하는 어긋남까지 전부 삼킨다 — " +
                "그래서 처방은 '덮기'가 아니라 '콘텐츠 사각형을 계산하기'다.");
        }

        // ────────────────────────────────────────────────────────────────────────
        // 3-2. 창 크기 재대입 수명 상한 — Screen.SetResolution과 성질이 같다
        // ────────────────────────────────────────────────────────────────────────

        [Test]
        public void 창크기_재대입에도_수명_상한이_있다()
        {
            int max = OverlayBoundsFitPolicy.DefaultMaxWindowResizeCalls;

            // 상한 직전까지는 (진짜로 어긋나 있으면) 재대입을 허용한다.
            Assert.IsTrue(OverlayBoundsFitPolicy.ShouldResizeWithinBudget(
                1000f, 800f, 1512f, 982f, OverlayBoundsFitPolicy.DefaultEpsilonPixels,
                callsSoFar: max - 1, maxCalls: max));

            // 상한에 닿으면 어긋나 있어도 더는 부르지 않는다.
            Assert.IsFalse(OverlayBoundsFitPolicy.ShouldResizeWithinBudget(
                1000f, 800f, 1512f, 982f, OverlayBoundsFitPolicy.DefaultEpsilonPixels,
                callsSoFar: max, maxCalls: max),
                "창 크기 재대입도 OS 표면 재생성(수백 ms 정지)이다. SetResolution에만 상한을 걸고 " +
                "이쪽을 무제한으로 두면 같은 사고가 나머지 한쪽으로 그대로 재발한다.");
        }

        [Test]
        public void 두_재생성_호출의_상한이_같은_값에서_유도된다()
        {
            Assert.AreEqual(OverlayBoundsFitPolicy.DefaultMaxSetResolutionCalls,
                OverlayBoundsFitPolicy.DefaultMaxWindowResizeCalls,
                "두 호출은 같은 함수의 같은 에피소드에서 짝으로 일어난다 — 상한이 갈라지면 " +
                "한쪽만 조여진 상태가 조용히 남는다.");
        }

        [Test]
        public void 상한_안에서도_이미_맞았으면_재대입하지_않는다()
        {
            Assert.IsFalse(OverlayBoundsFitPolicy.ShouldResizeWithinBudget(
                1512f, 982f, 1512f, 982f, OverlayBoundsFitPolicy.DefaultEpsilonPixels,
                callsSoFar: 0, maxCalls: OverlayBoundsFitPolicy.DefaultMaxWindowResizeCalls),
                "상한을 붙이면서 불감대가 사라지면 안 된다 — 상한은 최후의 방어선이지 1차 방어선이 아니다.");
        }

        // ────────────────────────────────────────────────────────────────────────
        // 4. 배선 — 규칙이 있어도 부르지 않으면 아무 일도 일어나지 않는다
        //    (이번 사고의 본질: 정책은 중립 위치에 있는데 한쪽이 호출하지 않았다)
        // ────────────────────────────────────────────────────────────────────────

        private static string PlatformRoot => Path.Combine(
            Application.dataPath, "_Project", "Scripts", "Platform");

        private static string ReadNoComments(string path)
        {
            Assert.IsTrue(File.Exists(path), $"소스를 찾지 못했습니다: {path}");
            var sb = new System.Text.StringBuilder();
            foreach (string line in File.ReadAllText(path).Split('\n'))
            {
                string t = line.TrimStart();
                if (t.StartsWith("//")) continue;
                if (t.StartsWith("*")) continue;
                sb.Append(line).Append('\n');
            }
            return sb.ToString();
        }

        [Test]
        public void macOS_보고_경로가_창장식_제거_규칙을_실제로_부른다()
        {
            string mac = ReadNoComments(Path.Combine(PlatformRoot, "MacOS", "MacWindowService.cs"));

            StringAssert.Contains("OverlayContentRectPolicy.TryStripTopDecoration(", mac,
                "kCGWindowBounds는 frame 사각형이다. 그대로 ScreenCoordinateConverter에 넘기면 " +
                "창이 보더리스가 아닌 순간 원점이 28pt 어긋난다. Windows판은 같은 부류를 이미 " +
                "TryGetVisualWindowRect로 막고 있었다 — macOS만 비어 있던 것이 이번 사고다.");
        }

        /// <summary>
        /// <b>좌표계의 출처</b>는 반드시 PID로만 고른다 — 같은 앱의 <b>두 번째 인스턴스</b>는
        /// <c>kCGWindowOwnerName</c>이 정확히 같기 때문이다.
        ///
        /// <para>2026-09-01까지 <c>MacWindowService.IsSelfWindow</c>는 "PID가 같거나 <b>또는</b> 이름이
        /// 같으면 내 창"이었다. 그래서 조사용으로 띄운 두 번째 인스턴스의 창이 우리 좌표계를
        /// 덮어썼고, 실기 로그에 <c>(0,0,1512,982)</c> ↔ <c>(0,33,1512,1010)</c> 교대가 남았다
        /// (뒤엣것은 <b>기동 중인 StickMate 창</b>의 모양 그 자체다).</para>
        ///
        /// <para>Windows는 원래부터 안전했다: 자기 창 제외는 <c>pid == _currentProcessId</c> 단독이고,
        /// 오버레이 원점의 출처는 <c>_overlayHwnd</c>(자기 프로세스의 MainWindowHandle) 하나뿐이라
        /// 남의 창이 들어올 경로가 없다.</para>
        /// </summary>
        [Test]
        public void 좌표계_출처_판정은_이름이_아니라_PID로만_한다()
        {
            string mac = ReadNoComments(Path.Combine(PlatformRoot, "MacOS", "MacWindowService.cs"));

            StringAssert.Contains("IsSelfProcessWindow(", mac,
                "좌표계 출처 판정(PID 단독)이 별도 함수로 분리돼 있어야 한다 — '내 오버레이인가'와 " +
                "'발판에서 빼야 하는가'는 다른 질문이고, 한 함수가 겸하던 것이 사고의 구조적 원인이었다.");
            Assert.IsFalse(mac.Contains("private bool IsSelfWindow("),
                "이름 폴백을 포함한 옛 판정이 남아 있으면 같은 이름의 두 번째 인스턴스 창이 다시 " +
                "우리 좌표계를 덮어쓴다.");

            // CaptureOverlayOrigin(좌표계 출처)은 반드시 PID 단독 판정 뒤에서만 불려야 한다.
            int capture = mac.IndexOf("CaptureOverlayOrigin(windowDict)", System.StringComparison.Ordinal);
            Assert.Greater(capture, 0, "열거 루프의 오버레이 원점 캡처 지점을 찾지 못했다.");
            string guardLine = mac.Substring(0, capture);
            int lastGuard = guardLine.LastIndexOf("IsSelfProcessWindow(", System.StringComparison.Ordinal);
            int lastBroad = guardLine.LastIndexOf("IsOwnAppWindow(", System.StringComparison.Ordinal);
            Assert.Greater(lastGuard, lastBroad,
                "CaptureOverlayOrigin 직전의 판정이 넓은(이름 포함) 판정이면, 남의 인스턴스 창이 " +
                "다시 좌표계로 흘러들어간다.");
        }

        [Test]
        public void 진동_가드가_양_플랫폼_Enforcer에_모두_배선되어_있다()
        {
            string mac = ReadNoComments(Path.Combine(PlatformRoot, "MacOS", "MacOverlayStateEnforcer.cs"));
            string win = ReadNoComments(Path.Combine(PlatformRoot, "Windows", "WindowsOverlayStateEnforcer.cs"));

            foreach (var pair in new[] { ("MacOverlayStateEnforcer", mac), ("WindowsOverlayStateEnforcer", win) })
            {
                StringAssert.Contains("new OverlayGeometryOscillationGuard()", pair.Item2,
                    $"{pair.Item1}에 진동 가드 인스턴스가 없습니다 — 규칙이 중립 위치에 있어도 " +
                    "부르지 않으면 그 플랫폼에는 존재하지 않는 것과 같습니다(이번 사고의 본질).");
                StringAssert.Contains(".Observe(", pair.Item2,
                    $"{pair.Item1}이 가드를 만들기만 하고 관측하지 않습니다.");
                StringAssert.Contains("IsOscillating", pair.Item2,
                    $"{pair.Item1}이 진동 확정 뒤에도 재적용/재무장을 계속합니다.");
            }
        }
    }
}
