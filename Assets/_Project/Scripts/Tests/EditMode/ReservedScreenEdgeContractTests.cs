using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using StickMate.Interaction;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-03 (dev-platform) — <b>네 방향 예약 띠</b> 계약
    /// (<see cref="IReservedScreenEdgeService"/> / <see cref="ReservedEdgeProbe"/> /
    /// <see cref="SurfaceSafeAreaPolicy"/> 가로축)의 회귀 잠금 + 플랫폼 패리티 감사.
    ///
    /// ============================================================================
    /// 왜 생겼나 — 상단 프로브로는 원리상 못 잡는 결함이 있다
    /// ============================================================================
    /// 할일 메모 카드는 화면 <b>오른쪽</b>에 16pt 여백으로 붙는데, <b>우측 도킹 작업표시줄</b>
    /// (통상 48~62pt)이나 <b>우측 Dock</b> 앞에서는 그 띠를 <b>통째로</b> 덮는다.
    /// <c>Win32WindowService</c>의 상단 조회는 <c>rcWork.Top − rcMonitor.Top</c>만 보므로 그때
    /// <b>정확히 0</b>을 낸다 — 버그가 아니라 축이 다른 것이다.
    ///
    /// ============================================================================
    /// 이 파일이 지키는 규칙 (전부 이 저장소가 당한 사고에서 나왔다)
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>타입이 아니라 소스 파일을 읽는다.</b> <c>Win32WindowService</c>는 파일 전체가
    ///     <c>#if UNITY_STANDALONE_WIN</c> 안이라 macOS 타깃에서 <b>타입이 존재하지 않는다</b>.
    ///     리플렉션 감사는 없는 타입을 셀 수 없고, 그 초록은 반대쪽 절반을 구조적으로 못 본 결과다.</item>
    ///   <item><b>부재 단언에는 반드시 존재 대조를 붙인다.</b> 부재 단언은 니들이 썩으면
    ///     <b>조용히 초록</b>이 된다. 이 파일의 "산술이 두 벌이 아니다"는 검사는 같은 니들이
    ///     <b>다른 메서드에는 실재한다</b>는 것을 같은 테스트 안에서 먼저 증명한다.</item>
    ///   <item><b>메서드 이름은 계약에서 리플렉션으로 뽑는다.</b> 문자열로 베끼면 계약이 바뀐 날
    ///     검사만 낡는다.</item>
    ///   <item><b>못 고친 갭은 <c>Assert.Fail</c>이 아니라 <c>Assert.Ignore</c>(사유 포함)</b>로 남겨
    ///     러너에 "건너뜀"으로 계속 보이게 한다. 그리고 <b>갭이 닫히면 실패</b>하게 만들어
    ///     명부가 조용히 늙지 않게 한다.</item>
    /// </list>
    /// </summary>
    public sealed class ReservedScreenEdgeContractTests
    {
        private const string LogPrefix = "[예약띠·네방향]";

        private static string ScriptsRoot => Path.Combine(Application.dataPath, "_Project", "Scripts");
        private static string PlatformRoot => Path.Combine(ScriptsRoot, "Platform");
        private static string InteractionRoot => Path.Combine(ScriptsRoot, "Interaction");

        // ★ 클래스 이름은 문자열일 수밖에 없다 — 두 클래스 다 반대편 타깃에서는 타입이 없다.
        //   대신 아래 TryGetBaseList가 "선언을 못 찾으면 빨강"이라 니들이 썩으면 조용하지 않다.
        private const string WinServiceClass = "Win32WindowService";
        private const string MacEdgeServiceClass = "MacReservedScreenEdgeService";
        private const string MacTopServiceClass = "MacReservedTopBarService";

        [SetUp]
        public void 프로브_정적상태_초기화() => ReservedEdgeProbe.ResetForTests();

        [TearDown]
        public void 프로브_정적상태_정리() => ReservedEdgeProbe.ResetForTests();

        // ==================================================================
        // ① ReservedEdgeInsets — "0"과 "모름"이 갈라져 있는가
        // ==================================================================

        /// <summary>
        /// 이 구조체의 존재 이유 전체가 여기 있다. <b>측정된 0</b>과 <b>미측정 0</b>은 값이 같고
        /// 소비 측 동작도 같지만, <b>사실로서 정반대</b>다. 하나는 "OS가 그 변에 아무것도 예약하지
        /// 않았음을 확인했다"이고 다른 하나는 "우리는 모른다"다.
        /// 이 둘이 섞이면 다음 라운드가 <i>"좌우는 이미 0으로 확인됐다"</i>는 거짓 근거를 얻는다.
        /// </summary>
        [Test]
        public void 측정된_0과_미측정_0은_다른_사실이다()
        {
            ReservedEdgeInsets 하단독_전부측정 = ReservedEdgeInsets.Observed(33f, 75f, 0f, 0f);
            ReservedEdgeInsets 아무것도_못잼 = ReservedEdgeInsets.Unknown;

            // 값은 둘 다 0이다 — 여기까지는 구분이 안 된다(그래서 마스크가 필요하다).
            Assert.AreEqual(0f, 하단독_전부측정.PointsFor(ReservedEdge.Left), 0.0001f);
            Assert.AreEqual(0f, 아무것도_못잼.PointsFor(ReservedEdge.Left), 0.0001f);

            // ★ 대조: 같은 0인데 한쪽만 '측정됨'이다.
            Assert.IsTrue(하단독_전부측정.IsMeasured(ReservedEdge.Left),
                $"{LogPrefix} 한 번의 조회로 네 변을 모두 관측했는데 왼쪽이 '미측정'으로 접혔습니다 — " +
                "그러면 '왼쪽에 띠가 없음을 확인했다'는 사실을 아무도 쓸 수 없습니다.");
            Assert.IsFalse(아무것도_못잼.IsMeasured(ReservedEdge.Left),
                $"{LogPrefix} 아무것도 못 쟀는데 '측정됨'으로 보고합니다 — 0을 '띠 없음'으로 위장하는 " +
                "이 한 줄이 짐작값으로 메우는 것과 정확히 같은 해악을 냅니다.");

            Assert.AreEqual(ReservedEdge.All, 하단독_전부측정.MeasuredEdges);
            Assert.AreEqual(ReservedEdge.None, 아무것도_못잼.MeasuredEdges);
            Assert.AreEqual(33f, 하단독_전부측정.PointsFor(ReservedEdge.Top), 0.0001f);
            Assert.AreEqual(75f, 하단독_전부측정.PointsFor(ReservedEdge.Bottom), 0.0001f);
        }

        /// <summary>
        /// 관측이 아닌 값(NaN·무한대·음수)은 그 변만 <b>미측정</b>으로 접고 나머지 변은 살린다.
        /// <c>rcWork</c>가 <c>rcMonitor</c> 밖으로 나가는 일은 정의상 없으므로 음수는 조회가 어긋난 것이다.
        /// </summary>
        [Test]
        public void 관측이_아닌_값은_그_변만_미측정으로_접는다()
        {
            ReservedEdgeInsets m = ReservedEdgeInsets.Observed(
                float.NaN,             // 상: 상식 범위 밖으로 거부된 값
                75f,                   // 하: 정상
                -1f,                   // 좌: 음수 = 조회 어긋남
                float.PositiveInfinity // 우: 비유한
            );

            Assert.IsFalse(m.IsMeasured(ReservedEdge.Top), $"{LogPrefix} NaN이 측정값으로 통과했습니다.");
            Assert.IsFalse(m.IsMeasured(ReservedEdge.Left), $"{LogPrefix} 음수가 측정값으로 통과했습니다.");
            Assert.IsFalse(m.IsMeasured(ReservedEdge.Right), $"{LogPrefix} 무한대가 측정값으로 통과했습니다.");

            // ★ 존재 대조 — 위 셋이 전부 false인 것이 "구조체가 통째로 죽어서"가 아님을 증명한다.
            Assert.IsTrue(m.IsMeasured(ReservedEdge.Bottom),
                $"{LogPrefix} 멀쩡한 값까지 함께 접혔습니다 — 한 변이 어긋나면 나머지 세 변도 " +
                "버리게 되고, 그러면 이 계약이 아무 사실도 나르지 못합니다.");
            Assert.AreEqual(75f, m.PointsFor(ReservedEdge.Bottom), 0.0001f);

            // 접힌 변의 값은 반드시 0이어야 한다(NaN이 소비 측으로 새면 배치가 통째로 깨진다).
            Assert.AreEqual(0f, m.PointsFor(ReservedEdge.Top), 0.0001f);
            Assert.AreEqual(0f, m.PointsFor(ReservedEdge.Left), 0.0001f);
            Assert.AreEqual(0f, m.PointsFor(ReservedEdge.Right), 0.0001f);
        }

        /// <summary>
        /// 구식 상단 전용 계약만 있는 플랫폼을 좁혀 담을 때, <b>나머지 세 변을 0으로 위장하지 않는다</b>.
        /// </summary>
        [Test]
        public void 상단만_아는_플랫폼은_좌우를_모른다고_말한다()
        {
            ReservedEdgeInsets m = ReservedEdgeInsets.TopOnly(33f);

            Assert.IsTrue(m.IsMeasured(ReservedEdge.Top), $"{LogPrefix} 아는 값까지 버렸습니다.");
            Assert.AreEqual(33f, m.PointsFor(ReservedEdge.Top), 0.0001f);

            Assert.IsFalse(m.IsMeasured(ReservedEdge.Right),
                $"{LogPrefix} 상단만 아는 플랫폼이 '오른쪽에 띠가 없다'고 단언했습니다 — " +
                "그 단언은 관측이 아니라 침묵을 사실로 바꾼 것입니다.");
            Assert.IsFalse(m.IsMeasured(ReservedEdge.Left));
            Assert.IsFalse(m.IsMeasured(ReservedEdge.Bottom));
        }

        /// <summary><see cref="ReservedEdgeInsets.IsMeasured"/>는 복합 마스크를 <b>전부</b> 기준으로 답한다.
        /// <see cref="ReservedEdgeInsets.PointsFor"/>는 단일 변만 받는다(복합은 0).</summary>
        [Test]
        public void 복합_마스크와_단일_선택자의_규약()
        {
            ReservedEdgeInsets 전부 = ReservedEdgeInsets.Observed(1f, 2f, 3f, 4f);
            ReservedEdgeInsets 상단만 = ReservedEdgeInsets.TopOnly(1f);

            Assert.IsTrue(전부.IsMeasured(ReservedEdge.Left | ReservedEdge.Right));
            Assert.IsFalse(상단만.IsMeasured(ReservedEdge.Top | ReservedEdge.Right),
                $"{LogPrefix} 일부만 측정됐는데 복합 마스크가 true를 냈습니다 — " +
                "'좌우도 쟀다'는 거짓 사실이 여기서 새어 나갑니다.");
            Assert.IsFalse(전부.IsMeasured(ReservedEdge.None),
                $"{LogPrefix} 빈 마스크가 true입니다 — 아무것도 안 물었는데 답이 나옵니다.");

            Assert.AreEqual(0f, 전부.PointsFor(ReservedEdge.All), 0.0001f,
                $"{LogPrefix} 복합 마스크에 두께를 돌려주면 '어느 변의 두께인가'가 사라집니다.");
            Assert.AreEqual(3f, 전부.PointsFor(ReservedEdge.Left), 0.0001f);
        }

        // ==================================================================
        // ② SurfaceSafeAreaPolicy 가로축 — 검산이 붙은 판정
        // ==================================================================

        /// <summary>
        /// ★ <b>회귀 없음 보증</b>. 좌·우 예약 띠가 0이면 요청한 인셋이 <b>그대로</b> 나온다 =
        /// 이 계약 도입 이전과 한 픽셀도 다르지 않다. 이 단언이 깨지면 띠가 없는 사용자
        /// (= 압도적 다수)의 화면이 이유 없이 움직인 것이다.
        /// </summary>
        [Test]
        public void 예약띠가_0이면_가로_배치는_한_픽셀도_바뀌지_않는다()
        {
            const float 화면폭 = 1512f, 카드폭 = 220f, 여백 = 16f, 요청인셋 = 16f;

            float 결과 = SurfaceSafeAreaPolicy.ClampRightAnchoredInset(
                요청인셋, 카드폭, 화면폭, leftInset: 0f, rightInset: 0f, margin: 여백);

            Assert.AreEqual(요청인셋, 결과, 0.001f,
                $"{LogPrefix} 예약 띠가 없는데 인셋이 {결과:F2}pt로 바뀌었습니다 — " +
                "이 함수를 끼워 넣는 것만으로 배치가 움직이면 회귀입니다.");
        }

        /// <summary>
        /// 신고된 결함 그 자체. 우측 도킹 작업표시줄 48pt 앞에서 카드 우변이 그 띠를 <b>1pt도</b> 안 덮는다.
        /// <code>
        ///   화면 1512 / 카드 220 / 여백 16 / 우 예약 48
        ///   maxCenterX = 1512 − 48 − 16 − 110 = 1338
        ///   카드 우변 = 1338 + 110 = 1448  ->  화면 오른쪽에서 64pt (= 48 + 16)
        ///   띠 시작점 1512 − 48 = 1464  ->  1448 &lt; 1464  ✔ 겹침 0
        /// </code>
        /// 고치기 전 값(16pt 고정)이었다면 카드 우변은 1496이라 띠를 <b>32pt</b> 덮었다.
        /// </summary>
        [Test]
        public void 우측_도킹_작업표시줄_48pt를_카드가_덮지_않는다()
        {
            const float 화면폭 = 1512f, 카드폭 = 220f, 여백 = 16f, 우예약 = 48f;

            float 인셋 = SurfaceSafeAreaPolicy.ClampRightAnchoredInset(
                여백, 카드폭, 화면폭, leftInset: 0f, rightInset: 우예약, margin: 여백);

            Assert.AreEqual(우예약 + 여백, 인셋, 0.001f,
                $"{LogPrefix} 우측 예약 띠 {우예약}pt 앞에서 인셋이 {인셋:F2}pt입니다 " +
                $"(기대 {우예약 + 여백:F2} = 띠 + 여백).");

            float 카드우변_화면오른쪽에서 = 인셋;
            float 띠_시작_화면오른쪽에서 = 우예약;
            Assert.GreaterOrEqual(카드우변_화면오른쪽에서, 띠_시작_화면오른쪽에서,
                $"{LogPrefix} 카드가 우측 예약 띠를 " +
                $"{띠_시작_화면오른쪽에서 - 카드우변_화면오른쪽에서:F2}pt 덮습니다(절대 불변 원칙 2).");

            // ★ 고치기 전 상태를 같은 테스트 안에서 재현해 "이 검사가 실제로 결함을 잡는다"를 증명한다.
            float 옛인셋 = 여백;
            Assert.Less(옛인셋, 띠_시작_화면오른쪽에서,
                $"{LogPrefix} 옛 고정 인셋 {옛인셋}pt가 띠를 안 덮는 것으로 계산됐습니다 — " +
                "그렇다면 이 테스트는 결함이 있던 시절에도 초록이었을 것이고, 아무것도 재고 있지 않습니다.");
        }

        /// <summary>
        /// 표면이 안전 영역보다 넓으면 <b>예약 띠가 얇은 쪽으로</b> 넘긴다.
        /// 가운데 정렬을 고르면 양쪽 띠를 반씩 덮어 이 규칙의 존재 이유를 무효로 만든다.
        /// </summary>
        [Test]
        public void 넘칠_수밖에_없으면_얇은_띠_쪽으로_넘친다()
        {
            const float 화면폭 = 400f, 카드폭 = 380f, 여백 = 16f;

            // 오른쪽에만 두꺼운 띠 -> 왼쪽(띠 0)으로 넘쳐야 한다 = 오른쪽 한계에 붙는다.
            float 중심_우측띠 = SurfaceSafeAreaPolicy.ClampCenterX(
                999f, 카드폭, 화면폭, leftInset: 0f, rightInset: 48f, margin: 여백);
            float 기대_우측띠 = 화면폭 - 48f - 여백 - 카드폭 * 0.5f;   // 400 − 48 − 16 − 190 = 146
            Assert.AreEqual(기대_우측띠, 중심_우측띠, 0.001f,
                $"{LogPrefix} 우측 띠 48pt 앞에서 표면이 그 띠 쪽으로 넘쳤습니다.");

            // 왼쪽에만 두꺼운 띠 -> 오른쪽(띠 0)으로 넘쳐야 한다 = 왼쪽 한계에 붙는다.
            float 중심_좌측띠 = SurfaceSafeAreaPolicy.ClampCenterX(
                -999f, 카드폭, 화면폭, leftInset: 48f, rightInset: 0f, margin: 여백);
            float 기대_좌측띠 = 48f + 여백 + 카드폭 * 0.5f;             // 48 + 16 + 190 = 254
            Assert.AreEqual(기대_좌측띠, 중심_좌측띠, 0.001f,
                $"{LogPrefix} 좌측 띠 48pt 앞에서 표면이 그 띠 쪽으로 넘쳤습니다.");

            Assert.AreNotEqual(중심_우측띠, 중심_좌측띠,
                $"{LogPrefix} 좌/우 어느 쪽에 띠가 있든 같은 자리가 나옵니다 — " +
                "'얇은 쪽으로 넘긴다'가 실제로는 아무것도 안 하고 있습니다.");
        }

        /// <summary>가로축을 넣으면서 <b>세로축 규칙을 건드리지 않았는가</b>. 하단은 여전히 강제하지 않는다
        /// (Dock은 캐릭터 발판이다 — 두 축을 같은 규칙으로 묶으면 발판 설계와 정면충돌한다).</summary>
        [Test]
        public void 세로축_규칙은_그대로다()
        {
            const float 화면높이 = 982f, 패널높이 = 560f, 여백 = 12f, 상단띠 = 33f;

            float 위쪽한계 = SurfaceSafeAreaPolicy.ClampCenterY(
                99999f, 패널높이, 화면높이, 상단띠, 여백);
            Assert.AreEqual(화면높이 - 상단띠 - 여백 - 패널높이 * 0.5f, 위쪽한계, 0.001f,
                $"{LogPrefix} 상단 클램프가 바뀌었습니다.");

            float 아래쪽한계 = SurfaceSafeAreaPolicy.ClampCenterY(
                -99999f, 패널높이, 화면높이, 상단띠, 여백);
            Assert.AreEqual(여백 + 패널높이 * 0.5f, 아래쪽한계, 0.001f,
                $"{LogPrefix} 하단에 예약 띠 회피가 들어왔습니다 — Dock은 이 앱의 발판입니다.");
        }

        // ==================================================================
        // ③ ReservedEdgeProbe 배선 — 값이 소비 측까지 도달하는가
        // ==================================================================

        private sealed class 네방향_스텁 : IPlatformWindowService, IReservedScreenEdgeService
        {
            private readonly ReservedEdgeInsets _insets;
            private readonly bool _ok;
            public 네방향_스텁(ReservedEdgeInsets insets, bool ok = true) { _insets = insets; _ok = ok; }
            public IReadOnlyList<PlatformFoothold> EnumerateFootholds() => new List<PlatformFoothold>();
            public bool CreateOverlayWindow() => true;
            public void SetClickThrough(bool enabled) { }
            public void SetAlwaysOnTop(bool enabled) { }
            public bool IsFullscreenAppActive() => false;
            public bool TryGetReservedEdgeInsetsPoints(out ReservedEdgeInsets insets)
            {
                insets = _ok ? _insets : ReservedEdgeInsets.Unknown;
                return _ok;
            }
        }

        private sealed class 상단전용_스텁 : IPlatformWindowService, IReservedTopBarService
        {
            private readonly float _top;
            public 상단전용_스텁(float top) { _top = top; }
            public IReadOnlyList<PlatformFoothold> EnumerateFootholds() => new List<PlatformFoothold>();
            public bool CreateOverlayWindow() => true;
            public void SetClickThrough(bool enabled) { }
            public void SetAlwaysOnTop(bool enabled) { }
            public bool IsFullscreenAppActive() => false;
            public bool TryGetReservedTopInsetPoints(out float insetPoints)
            {
                insetPoints = _top;
                return _top > 0f;
            }
        }

        private sealed class 계측없는_스텁 : IPlatformWindowService
        {
            public IReadOnlyList<PlatformFoothold> EnumerateFootholds() => new List<PlatformFoothold>();
            public bool CreateOverlayWindow() => true;
            public void SetClickThrough(bool enabled) { }
            public void SetAlwaysOnTop(bool enabled) { }
            public bool IsFullscreenAppActive() => false;
        }

        /// <summary>데코레이터(<see cref="FallbackPlatformWindowService"/>)로 감싸도 네 변이 소비 측에 도달한다.
        /// 상단 프로브가 같은 함정에 빠졌던 적이 있어(그쪽은 <c>as</c>가 아니라 <c>Inner</c>로 벗긴다)
        /// 여기도 같은 실측을 붙인다.</summary>
        [Test]
        public void 네_변이_데코레이터를_거쳐도_소비_측에_도달한다()
        {
            var 실측 = ReservedEdgeInsets.Observed(0f, 0f, 0f, 48f);   // 우측 도킹 작업표시줄
            var decorated = new FallbackPlatformWindowService(new 네방향_스텁(실측), null);

            Assert.AreEqual(48f, ReservedEdgeProbe.EdgeInsetPoints(decorated, ReservedEdge.Right), 0.001f,
                $"{LogPrefix} 데코레이터로 감싼 뒤 우측 예약 띠가 소비 측에 도달하지 않습니다 — " +
                "화면 오른쪽에 붙는 표면이 그 띠를 그대로 덮습니다.");
            Assert.IsTrue(ReservedEdgeProbe.Insets(decorated).IsMeasured(ReservedEdge.Left),
                $"{LogPrefix} 전달 과정에서 측정 마스크가 유실됐습니다.");
        }

        /// <summary>
        /// ★★ 2026-09-03 — <b>위 테스트가 잠그지 못하는 나머지 절반</b>.
        /// 위쪽은 <see cref="ReservedEdgeProbe"/>를 지나고, 그 안은 <c>as</c>가 아니라
        /// <c>decorator.Inner</c>로 <b>한 겹 벗긴 뒤</b> 캐스팅한다. 그래서 데코레이터가 이 계약을
        /// 통과시키지 않아도 <b>초록이었다</b> — 실제로 그랬다(<c>FallbackServicePassthroughTests</c>의
        /// 소스 감사만 빨갰다).
        ///
        /// <para>이 테스트는 <b>이 저장소의 관례 경로</b>(<c>PlatformService as I…</c>)를 직접 잰다.
        /// 그 관례가 죽으면 다음 소비자가 조용히 "미지원"으로 폴백한다 — 같은 병으로 이미 네 번
        /// 당했다(<c>IGlobalPointerButtonService</c> · <c>IRawWindowRectSource</c> ·
        /// <c>IWindowEnumerationCostSource</c> · <c>IReservedTopBarService</c>).</para>
        ///
        /// <para>★ 존재(양성)와 부재(음성)를 같은 테스트 안에서 대조한다 — 통과 경로가 통째로
        /// 죽어도 부재 단언만 있으면 조용히 초록이다.</para>
        /// </summary>
        [Test]
        public void 네_변은_관례대로_as_캐스팅해도_데코레이터를_통과한다()
        {
            var 실측 = ReservedEdgeInsets.Observed(0f, 0f, 0f, 48f);   // 우측 도킹 작업표시줄
            var decorated = new FallbackPlatformWindowService(new 네방향_스텁(실측), null);

            var 관례경로 = decorated as IReservedScreenEdgeService;
            Assert.IsNotNull(관례경로,
                $"{LogPrefix} 데코레이터가 네 방향 계약을 통과시키지 않습니다 — " +
                "`PlatformService as IReservedScreenEdgeService`가 항상 null이 되어 " +
                "그 기능이 예외도 로그도 없이 죽습니다.");

            Assert.IsTrue(관례경로.TryGetReservedEdgeInsetsPoints(out ReservedEdgeInsets 통과값),
                $"{LogPrefix} 통과 경로가 열려는 있는데 안쪽 값을 못 가져옵니다.");
            Assert.AreEqual(48f, 통과값.PointsFor(ReservedEdge.Right), 0.001f,
                $"{LogPrefix} 통과 경로가 안쪽과 다른 값을 냅니다(데코레이터가 값을 가공하면 안 됩니다).");
            Assert.IsTrue(통과값.IsMeasured(ReservedEdge.Right),
                $"{LogPrefix} 통과 과정에서 측정 마스크가 유실됐습니다.");

            // ── 음성 대조: 안쪽이 네 방향을 모르면 '실패는 0이다'로 접힌다(짐작으로 메우지 않는다).
            var 모름 = new FallbackPlatformWindowService(new 계측없는_스텁(), null)
                as IReservedScreenEdgeService;
            Assert.IsFalse(모름.TryGetReservedEdgeInsetsPoints(out ReservedEdgeInsets 빈값),
                $"{LogPrefix} 안쪽이 미지원인데 데코레이터가 '쟀다'고 보고합니다.");
            Assert.AreEqual(ReservedEdge.None, 빈값.MeasuredEdges,
                $"{LogPrefix} 미측정을 '측정된 0'으로 위장하면 다음 라운드가 " +
                "'좌우는 이미 0으로 확인됐다'는 거짓 근거를 얻습니다.");
        }

        /// <summary>
        /// 구식 상단 전용 계약만 구현한 플랫폼에서는 <b>상단만</b> 측정되고 좌·우는 <b>모름</b>으로 남는다.
        /// ★ 존재/부재를 같은 테스트 안에서 대조한다 — 부재 단언만 있으면 배선이 통째로 죽어도 초록이다.
        /// </summary>
        [Test]
        public void 상단전용_구현은_좌우를_0으로_위장하지_않는다()
        {
            var service = new 상단전용_스텁(33f);
            ReservedEdgeInsets m = ReservedEdgeProbe.Insets(service);

            Assert.IsTrue(m.IsMeasured(ReservedEdge.Top),
                $"{LogPrefix} 구식 계약에서 상단 값조차 못 받았습니다 — 아래 부재 단언은 " +
                "'배선이 죽어서' 통과하는 것일 수 있으므로 이 존재 대조가 먼저입니다.");
            Assert.AreEqual(33f, m.PointsFor(ReservedEdge.Top), 0.001f);

            Assert.IsFalse(m.IsMeasured(ReservedEdge.Right),
                $"{LogPrefix} 상단만 아는 구현이 '오른쪽에 띠가 없다'고 단언했습니다.");
            Assert.AreEqual(0f, m.PointsFor(ReservedEdge.Right), 0.001f);
        }

        /// <summary><b>실패는 0이다.</b> 계측을 전혀 구현하지 않은 플랫폼(에디터/모바일)에서는
        /// 네 변 모두 0이고, 소비 측은 아무것도 바꾸지 않는다.</summary>
        [Test]
        public void 계측이_없으면_네_변_모두_0이고_짐작하지_않는다()
        {
            var service = new 계측없는_스텁();
            ReservedEdgeInsets m = ReservedEdgeProbe.Insets(service);

            Assert.AreEqual(ReservedEdge.None, m.MeasuredEdges,
                $"{LogPrefix} 계측이 없는데 무언가를 '측정했다'고 보고합니다.");
            foreach (ReservedEdge edge in new[]
                     { ReservedEdge.Top, ReservedEdge.Bottom, ReservedEdge.Left, ReservedEdge.Right })
            {
                Assert.AreEqual(0f, ReservedEdgeProbe.EdgeInsetPoints(service, edge), 0.001f,
                    $"{LogPrefix} {edge} 변에 짐작값이 채워졌습니다 — 화면 폭에서 빼서 추정하는 것은 " +
                    "이 계약의 '실패는 0이다' 규약을 정면으로 깹니다.");
            }
            Assert.AreEqual(0f, ReservedEdgeProbe.EdgeInsetPoints(null, ReservedEdge.Right), 0.001f,
                $"{LogPrefix} 서비스가 null일 때 터지거나 짐작값을 냅니다.");
        }

        /// <summary>
        /// 두 프로브가 <b>같은 화면의 같은 사실</b>을 본다. 한쪽만 주입하면 물리적으로 존재할 수 없는
        /// 세계(상단 33pt인데 상단 0pt인 화면)에서 검증하게 된다.
        /// ★ 주입(양성)과 걷기(음성)를 같은 테스트 안에서 대조한다.
        /// </summary>
        [Test]
        public void 네방향_주입은_상단_프로브까지_함께_움직인다()
        {
            ReservedEdgeProbe.SetInsetsForTests(ReservedEdgeInsets.Observed(33f, 75f, 0f, 48f));

            Assert.AreEqual(33f, ReservedEdgeProbe.EdgeInsetPoints(null, ReservedEdge.Top), 0.001f);
            Assert.AreEqual(33f, ReservedTopBarProbe.TopInsetPoints(null), 0.001f,
                $"{LogPrefix} 네 방향 주입이 상단 프로브에 닿지 않습니다 — 상단 축을 쓰는 소비 호출부 " +
                "다섯 곳이 주입한 세계와 다른 세계를 보게 됩니다.");

            ReservedEdgeProbe.ResetForTests();

            Assert.AreEqual(0f, ReservedEdgeProbe.EdgeInsetPoints(null, ReservedEdge.Top), 0.001f,
                $"{LogPrefix} 주입이 안 걷혔습니다 — 다음 픽스처가 오염됩니다.");
            Assert.AreEqual(0f, ReservedTopBarProbe.TopInsetPoints(null), 0.001f,
                $"{LogPrefix} 상단 프로브의 주입이 안 걷혔습니다.");
        }

        /// <summary>주기 상수가 두 벌이 아니다 — 한쪽만 고쳐지는 것을 막는다.</summary>
        [Test]
        public void 갱신_주기는_상단_프로브와_같은_상수_하나다()
        {
            Assert.AreEqual(ReservedTopBarProbe.RefreshIntervalSeconds,
                ReservedEdgeProbe.RefreshIntervalSeconds, 0.0001f,
                $"{LogPrefix} 두 프로브의 갱신 주기가 갈라졌습니다.");
        }

        // ==================================================================
        // ④ 플랫폼 패리티 감사 — 소스 파일을 읽는다(활성 빌드 타깃과 무관)
        // ==================================================================

        /// <summary>
        /// 계약과 정책이 <b>플랫폼 중립 위치</b>(<c>Platform/</c> 바로 아래)에 있는가.
        /// 판정이 <c>Platform/MacOS/</c> 안에 있으면 Windows가 물리적으로 호출할 수 없다
        /// (실제 사고: <c>Platform/FullscreenSuspendPolicy.cs</c>).
        /// </summary>
        [Test]
        public void 계약과_정책은_플랫폼_중립_위치에_있다()
        {
            string contract = Path.Combine(PlatformRoot, "IReservedScreenEdgeService.cs");
            string probe = Path.Combine(PlatformRoot, "ReservedEdgeProbe.cs");
            string policy = Path.Combine(PlatformRoot, "SurfaceSafeAreaPolicy.cs");

            Assert.IsTrue(File.Exists(contract), $"{LogPrefix} 네 방향 계약이 중립 위치에 없습니다.");
            Assert.IsTrue(File.Exists(probe), $"{LogPrefix} 네 방향 프로브가 중립 위치에 없습니다.");
            Assert.IsTrue(File.Exists(policy), $"{LogPrefix} 가로축 판정이 중립 위치에 없습니다.");

            StringAssert.DoesNotContain("UNITY_STANDALONE_", StripComments(ReadSource(policy)),
                $"{LogPrefix} 정책 파일에 플랫폼 분기가 들어왔습니다 — 이 파일은 순수 산술이어야 하고, " +
                "그래야 양쪽 플랫폼이 같은 규칙을 씁니다.");
            StringAssert.DoesNotContain("UNITY_STANDALONE_", StripComments(ReadSource(contract)),
                $"{LogPrefix} 계약 파일에 플랫폼 분기가 들어왔습니다.");
        }

        /// <summary>
        /// 양 플랫폼이 계약을 <b>기반 목록에</b> 달고 계약 메서드를 실제로 갖고 있는가.
        /// 인터페이스 이름이 주석에만 있으면 <see cref="ReservedEdgeProbe"/>의 <c>is</c> 판정이
        /// <b>조용히 null</b>이 되어, 기능이 있는 것처럼 보이면서 한 번도 호출되지 않는다.
        /// </summary>
        [Test]
        public void 네방향_조회가_양_플랫폼에_모두_배선되어_있다()
        {
            MethodInfo[] methods = typeof(IReservedScreenEdgeService).GetMethods();
            Assert.AreEqual(1, methods.Length,
                $"{LogPrefix} 계약 메서드 수가 1이 아닙니다 — 이 검사는 '그 하나'로 양 플랫폼을 " +
                "대조합니다. 계약이 늘었다면 아래 대조도 함께 늘리세요.");
            string contractMethod = methods[0].Name;

            AssertDeclaresInterface(Path.Combine(PlatformRoot, "Windows", "Win32WindowService.cs"),
                WinServiceClass, nameof(IReservedScreenEdgeService), contractMethod);
            AssertDeclaresInterface(Path.Combine(PlatformRoot, "MacOS", "MacReservedScreenEdgeService.cs"),
                MacEdgeServiceClass, nameof(IReservedScreenEdgeService), contractMethod);

            // 소비 배선: 프로브가 두 경로를 모두 잡는가.
            string probeSource = StripComments(ReadSource(Path.Combine(PlatformRoot, "ReservedEdgeProbe.cs")));
            StringAssert.Contains("is " + nameof(IReservedScreenEdgeService), probeSource,
                $"{LogPrefix} 프로브가 '플랫폼 서비스가 직접 구현한 경우'를 잡지 않습니다 — " +
                "Windows는 그 분기가 유일한 경로입니다.");
            StringAssert.Contains(MacEdgeServiceClass + ".TryCreate(", probeSource,
                $"{LogPrefix} macOS 조립 경로가 사라졌습니다 — MacWindowService는 이 인터페이스를 " +
                "직접 달지 않고 별도 어댑터로 조립합니다. 이 줄이 없으면 macOS가 조용히 0이 됩니다.");
        }

        /// <summary>
        /// ★★ <b>산술이 한 벌인가</b> — 이 파일에서 가장 값나가는 검사다.
        ///
        /// <para>상단 계약은 소비 호출부가 다섯이라 지울 수 없다. 그래서 <b>계약은 둘, 산술은 하나</b>로
        /// 만들었다: 상단 구현이 네 방향 조회를 호출해 <c>Top</c>만 꺼낸다. 상단 구현 안에
        /// 뺄셈을 다시 적으면 두 벌이 되고, 이 저장소의 규칙대로 다음 라운드에 반드시 한쪽만 고쳐진다.</para>
        ///
        /// <para><b>부재 단언에 존재 대조를 붙인다</b>: 같은 니들이 네 방향 메서드에는 <b>실재</b>한다는 것을
        /// 먼저 증명한다. 그 증명이 없으면 니들이 썩었을 때(필드명 변경 등) 이 검사가 조용히 초록이 된다.</para>
        /// </summary>
        [Test]
        public void 상단_산술은_네방향_조회에만_존재한다()
        {
            // ---- Windows ----
            string winSrc = StripComments(ReadSource(
                Path.Combine(PlatformRoot, "Windows", "Win32WindowService.cs")));
            const string winNeedle = "rcWork.Top";

            Assert.IsTrue(TryGetMethodBody(winSrc, "TryGetReservedEdgeInsetsPoints", out string winEdgeBody),
                $"{LogPrefix} Windows 네 방향 메서드 본문을 못 찾았습니다 — 감사 앵커가 낡았습니다. " +
                "그대로 두면 아래 부재 단언이 '못 찾았다'로 조용히 통과합니다.");
            Assert.IsTrue(TryGetMethodBody(winSrc, "TryGetReservedTopInsetPoints", out string winTopBody),
                $"{LogPrefix} Windows 상단 메서드 본문을 못 찾았습니다.");

            // (존재 대조) 니들이 실재하는가 — 이게 깨지면 아래 부재 단언은 아무 뜻이 없다.
            StringAssert.Contains(winNeedle, winEdgeBody,
                $"{LogPrefix} 니들 '{winNeedle}'이 네 방향 메서드에도 없습니다 — 니들이 썩었습니다. " +
                "이 상태에서 아래 부재 단언은 영구 거짓 초록입니다.");
            // (부재 단언) 상단 메서드는 산술을 갖지 않는다.
            StringAssert.DoesNotContain(winNeedle, winTopBody,
                $"{LogPrefix} Windows 상단 메서드가 뺄셈을 <b>다시</b> 갖고 있습니다 — 산술이 두 벌입니다. " +
                "좌/우 축을 고치는 날 한쪽만 고쳐집니다.");

            // ---- macOS ----
            string macEdgeSrc = StripComments(ReadSource(
                Path.Combine(PlatformRoot, "MacOS", "MacReservedScreenEdgeService.cs")));
            string macTopSrc = StripComments(ReadSource(
                Path.Combine(PlatformRoot, "MacOS", "MacReservedTopBarService.cs")));
            const string macNeedle = "GetMonitorRect";

            StringAssert.Contains(macNeedle, macEdgeSrc,
                $"{LogPrefix} 니들 '{macNeedle}'이 macOS 네 방향 조회에도 없습니다 — 니들이 썩었습니다.");
            StringAssert.DoesNotContain(macNeedle, macTopSrc,
                $"{LogPrefix} macOS 상단 어댑터가 조회를 <b>다시</b> 하고 있습니다 — 산술이 두 벌입니다.");
            StringAssert.Contains(MacEdgeServiceClass, macTopSrc,
                $"{LogPrefix} macOS 상단 어댑터가 네 방향 조회를 거치지 않습니다 — " +
                "위 부재 단언이 '조회를 아예 안 한다'는 이유로 통과했을 수 있습니다.");
        }

        // ==================================================================
        // ⑤ 소비 배선 — 2026-09-03 승격. Assert.Ignore였던 자리다
        // ==================================================================
        //
        // ★ 승격 경위: 이 자리에는 <c>화면_오른쪽_표면의_소비_배선은_아직_없다_미해결()</c>이
        //   Assert.Ignore로 서 있었고, 그 안에 <b>역방향 래칫</b>이 들어 있었다 —
        //   "Interaction/ 어디든 ReservedEdgeProbe 소비가 하나라도 생기면 Ignore 앞에서 Assert.Fail".
        //   그 래칫이 실제로 발동했고(소비 3곳 착지), 설계대로 실단언으로 승격했다.
        //   ⇒ 명부(Tests/EditMode/TestClaimExpiryAuditTests.cs)의 같은 이름 항목은 <b>등록 해제 필요</b>다.
        //     그 파일은 같은 시각 다른 라운드가 잡고 있어 여기서 건드리지 않았다 — 보고로 넘긴다.

        /// <summary>
        /// ★ <b>화면 오른쪽에 사는 표면이 네 방향 예약 띠를 실제로 읽는가</b>(소스 스캔).
        ///
        /// <para>이 검사는 <b>배선의 존재</b>만 본다. 그 배선이 <b>맞는 좌표</b>를 내는지는 아래
        /// 기하 단언들이 순수 함수로 따로 잰다 — 둘을 한 테스트에 섞으면 어느 쪽이 깨졌는지 모른다.</para>
        ///
        /// <para><b>양성 대조를 그대로 물려받는다</b>: 같은 스캐너로 <c>ReservedTopBarProbe</c>(실재하는
        /// 니들)를 세어 5가 나오는지 먼저 본다. 스캐너가 죽으면 이 검사의 모든 0/N이 뜻을 잃는다.</para>
        /// </summary>
        [Test]
        public void 화면_오른쪽에_사는_표면이_네방향_프로브를_소비한다()
        {
            Assert.IsTrue(Directory.Exists(InteractionRoot),
                $"{LogPrefix} 스캔 대상 폴더가 없습니다 — 0건이 '깨끗함'이 아니라 '못 읽음'입니다.");

            string[] 후보 = Directory.GetFiles(InteractionRoot, "*.cs", SearchOption.AllDirectories);
            Assert.Greater(후보.Length, 10,
                $"{LogPrefix} 스캔이 {후보.Length}개 파일밖에 못 읽었습니다 — 최소 수집량 가드. " +
                "스캐너가 아무것도 못 읽고 초록불이 되는 것이 이 저장소의 사고 #4·#5였습니다.");

            var 네방향_소비자 = new List<string>();
            var 상단프로브_소비자 = new List<string>();
            foreach (string path in 후보)
            {
                string src = ReadSource(path);
                string name = Path.GetFileName(path);
                if (src.IndexOf(nameof(ReservedEdgeProbe), StringComparison.Ordinal) >= 0) 네방향_소비자.Add(name);
                if (src.IndexOf(nameof(ReservedTopBarProbe), StringComparison.Ordinal) >= 0) 상단프로브_소비자.Add(name);
            }

            // (가) 스캐너가 살아 있는가 — 이게 깨지면 아래 모든 셈이 뜻을 잃는다.
            Assert.Greater(상단프로브_소비자.Count, 0,
                $"{LogPrefix} 같은 스캐너가 '실재하는' 상단 프로브 소비 호출부도 0건으로 셌습니다 — " +
                "스캐너가 죽었습니다. 이 상태의 숫자는 전부 무효입니다.");

            // (나) 그 수가 문서와 같은가 — 여러 문서가 오랫동안 '넷'이라고 잘못 적어 두었던 바로 그 수다.
            Assert.AreEqual(5, 상단프로브_소비자.Count,
                $"{LogPrefix} 상단 프로브 소비 호출부가 5개가 아니라 {상단프로브_소비자.Count}개입니다 " +
                $"({string.Join(", ", 상단프로브_소비자)}). 늘렸다면 " +
                "Platform/ReservedTopBarProbe.cs · Platform/IReservedScreenEdgeService.cs · " +
                "Platform/MacOS/MacReservedTopBarService.cs의 '다섯 파일' 목록도 함께 고치세요 — " +
                "그 목록이 조용히 늙는 것을 막으려고 이 단언이 있습니다.");

            // (다) ★ 실단언 — 오른쪽 끝에 사는 표면 셋이 네 방향 값을 읽는다.
            //     파일명은 문자열로 베끼지 않고 타입 이름에서 뽑는다(파일을 옮기거나 이름을 바꾸면
            //     여기가 조용히 통과하는 대신 시끄럽게 빨개진다).
            string[] 필수 =
            {
                nameof(InfoGearIconWidget) + ".cs",     // 톱니 — 등급 1의 유일한 탈출구
                nameof(GearRadialMenuWidget) + ".cs",   // 그 톱니를 눌러서 열리는 부채꼴
                nameof(TodoPostItWidget) + ".cs",       // 최초 신고 대상(오른쪽 16pt 카드)
            };
            foreach (string 파일 in 필수)
            {
                Assert.Contains(파일, 네방향_소비자,
                    $"{LogPrefix} {파일}이 {nameof(ReservedEdgeProbe)}를 소비하지 않습니다 — " +
                    $"지금 소비하는 것은 [{string.Join(", ", 네방향_소비자)}]뿐입니다. " +
                    "이 표면은 화면 오른쪽 끝에 살기 때문에 우측 도킹 작업표시줄(48~62pt) 앞에서 " +
                    "그 띠를 덮습니다(절대 불변 원칙 2).");
            }
        }

        // ==================================================================
        // ⑥ 톱니 — 우측 도킹 작업표시줄 뒤로 들어가지 않는가
        // ==================================================================

        /// <summary>
        /// ★★ <b>고친 결함 그 자체</b>. 톱니의 기본 위치는 <b>화면 오른쪽 끝에서 중심 30pt</b>이고
        /// 히트 반지름은 19.82pt다 — 즉 화면 오른쪽 끝에서 <b>10.18~49.82pt</b> 구간을 차지한다.
        /// 48pt 우측 도킹 작업표시줄이면 히트 폭 39.64pt 중 <b>37.82pt(95%)</b>가 띠 안이었다.
        ///
        /// <para><b>왜 이게 P0인가</b>: 작업표시줄은 최상위 창이라 그 위의 클릭은 우리에게 오지 않는다.
        /// 그리고 이 앱에는 <b>등급 1을 끄는 탈출구가 톱니 1클릭뿐</b>이다
        /// (<see cref="UserSurfaceSummonPolicy"/>). 눌리지 않는 탈출구는 탈출구가 아니다.</para>
        ///
        /// <para><b>숫자를 베끼지 않는다</b>: 30 / 19.82 / 0 전부 프로덕션 상수를 참조한다.
        /// 48은 시나리오 입력(도킹 작업표시줄 두께)이지 우리 상수가 아니다.</para>
        /// </summary>
        [Test]
        public void 톱니가_우측_도킹_작업표시줄_뒤로_들어가지_않는다()
        {
            const float 화면폭 = 1512f, 우예약 = 48f;
            float r = InfoGearIconWidget.HitRadiusPoints;
            float 요청 = 화면폭 - InfoGearIconWidget.DefaultRightMarginPoints;

            float 중심 = SurfaceSafeAreaPolicy.ClampCenterX(요청, r * 2f, 화면폭,
                leftInset: 0f, rightInset: 우예약, margin: InfoGearIconWidget.SideBandMarginPoints);

            float 히트우변_화면오른쪽에서 = SurfaceSafeAreaPolicy.RightEdgeFromScreenRight(중심, r * 2f, 화면폭);

            // ★ 허용오차는 손으로 고른 숫자가 아니라 **유도값**이다 — 근거는 FloatRoundingBudget 문서.
            float 예산 = FloatRoundingBudget(화면폭 - 우예약, 우예약);

            Assert.GreaterOrEqual(히트우변_화면오른쪽에서, 우예약 - 예산,
                $"{LogPrefix} 톱니 히트 사각형이 우측 예약 띠를 " +
                $"{우예약 - 히트우변_화면오른쪽에서:F6}pt 덮습니다(절대 불변 원칙 2). " +
                $"float 반올림 예산 {예산:R}pt를 넘었으므로 이것은 반올림 잡음이 아니라 " +
                "클램프 산술의 결함입니다. 작업표시줄은 최상위 창이라 그만큼이 '눌리지 않는 버튼'입니다.");

            // ★ 고치기 전 상태를 같은 테스트 안에서 재현 — 이 검사가 실제로 결함을 잡는다는 증명.
            //   (없으면 결함이 있던 시절에도 초록이었을 수 있고, 그러면 아무것도 재고 있지 않다.)
            float 옛_히트우변 = SurfaceSafeAreaPolicy.RightEdgeFromScreenRight(요청, r * 2f, 화면폭);
            Assert.Less(옛_히트우변, 우예약,
                $"{LogPrefix} 옛 좌표(화면폭 − {InfoGearIconWidget.DefaultRightMarginPoints:F0})의 히트 우변이 " +
                $"띠를 안 덮는 것으로 계산됐습니다({옛_히트우변:F2}pt ≥ {우예약:F0}pt) — " +
                "그렇다면 이 테스트는 결함이 있던 시절에도 초록이었습니다.");
            Assert.AreEqual(37.82f, 우예약 - 옛_히트우변, 0.01f,
                $"{LogPrefix} 리더가 실측한 침해량 37.82pt와 계산이 다릅니다 — " +
                "히트 반지름이나 우측 여백 상수가 바뀌었다면 이 라운드의 근거 문서도 함께 고치세요.");

            // ★ 가장 빡빡한 진술: 히트 사각형 우변이 <b>정확히</b> 띠 시작점에 닿는다(여백 0 규약).
            //   더 밀면 띠가 없는 사용자까지 밀리고, 덜 밀면 덮는다.
            //   ★ 여기도 같은 유도 예산을 쓴다(옛 0.0001f는 손으로 고른 값이었고, 유도 예산보다
            //     1.6배 느슨했다 — 즉 이 단언은 필요보다 헐거웠다. 좁히는 방향이다).
            Assert.AreEqual(우예약, 히트우변_화면오른쪽에서, 예산,
                $"{LogPrefix} 톱니가 띠 시작점에 정확히 붙지 않았습니다 " +
                $"({히트우변_화면오른쪽에서:R} vs {우예약:R}, 예산 {예산:R}) — 측면 여백 상수가 " +
                $"{InfoGearIconWidget.SideBandMarginPoints:F2}pt가 아닌 값으로 바뀌었다면 " +
                "그 근거를 SideMarginPoints 문서에 함께 쓰세요.");
        }

        /// <summary>
        /// ★★ 2026-09-03 — <b>「허용오차를 넣으니 초록이 됐다」로 끝내지 않기 위한 테스트.</b>
        ///
        /// 위 테스트가 <c>47.9999466 &lt; 48</c>로 빨갰다. 그때 물어야 하는 것은 "얼마를 봐주면
        /// 통과하나"가 아니라 <b>"이 잔차가 한쪽으로 쏠려 있나"</b>다. 쏠려 있으면 그건 잡음이
        /// 아니라 <b>클램프가 띠를 실제로 조금씩 먹고 있다</b>는 뜻이고, 허용오차가 아니라
        /// 프로덕션을 고쳐야 한다.
        ///
        /// <para>그래서 화면폭 19종 × 우측 띠 10종 = 190개 조합에서 잔차가
        /// <see cref="FloatRoundingBudget"/>(뺄셈 1회 + 저장 1회의 ½ ULP) <b>안</b>에 있는지를
        /// 매 실행 다시 잰다. 예산을 넘는 조합이 하나라도 나오면 그건 반올림이 아니다.</para>
        ///
        /// <para>★ 부호 분포는 <b>단언하지 않고 로그로 남긴다</b> — 부호는 런타임의 중간 정밀도에
        /// 따라 달라진다(에디터 Mono는 넓게 들고 있다가 마지막에 접고, 순수 float32라면 상쇄되어
        /// 전부 0이다). <b>양쪽 다 예산 안</b>이라는 것이 이 테스트가 지키는 불변식이고,
        /// 부호를 단언하면 런타임이 바뀐 날 이 테스트가 <b>결함과 무관하게</b> 빨개진다.</para>
        ///
        /// <para>기준 위치는 <c>화면폭 × 2</c> — <b>반드시 클램프에 걸리는</b> 값이라
        /// 톱니의 기본 여백 상수가 바뀌어도 이 측정이 흔들리지 않는다.</para>
        /// </summary>
        [Test]
        public void 우변_잔차는_한_번의_float_반올림_예산_안에_있다()
        {
            float r = InfoGearIconWidget.HitRadiusPoints;
            float 여백 = InfoGearIconWidget.SideBandMarginPoints;
            float[] 화면폭들 = { 800f, 1024f, 1280f, 1366f, 1440f, 1512f, 1512.5f, 1600f, 1680f, 1728f,
                               1920f, 2048f, 2560f, 2880f, 3008f, 3440f, 3440.75f, 3840f, 5120f };
            float[] 우측띠들 = { 40f, 48f, 52f, 54f, 56f, 62f, 72f, 80f, 96f, 120f };

            int 덮음 = 0, 남김 = 0, 정확 = 0;
            float 최악비 = 0f;
            string 최악설명 = "(없음)";

            foreach (float w in 화면폭들)
            {
                foreach (float 띠 in 우측띠들)
                {
                    float 중심 = SurfaceSafeAreaPolicy.ClampCenterX(w * 2f, r * 2f, w, 0f, 띠, 여백);
                    float 우변 = SurfaceSafeAreaPolicy.RightEdgeFromScreenRight(중심, r * 2f, w);

                    float 이상 = 띠 + 여백;                       // 여백 0 규약이면 띠에 정확히 닿는다.
                    float 잔차 = 우변 - 이상;
                    float 예산 = FloatRoundingBudget(w - 띠, 이상);

                    if (잔차 < 0f) 덮음++; else if (잔차 > 0f) 남김++; else 정확++;
                    float 비 = 예산 > 0f ? Math.Abs(잔차) / 예산 : 0f;
                    if (비 > 최악비)
                    {
                        최악비 = 비;
                        최악설명 = $"화면폭 {w:R} / 띠 {띠:R} → 잔차 {잔차:R}pt (예산 {예산:R}pt)";
                    }

                    Assert.LessOrEqual(Math.Abs(잔차), 예산,
                        $"{LogPrefix} 화면폭 {w:R} / 우측 띠 {띠:R}에서 히트 우변이 {우변:R}pt로 " +
                        $"이상값 {이상:R}pt와 {잔차:R}pt 어긋났습니다. float 반올림 예산 {예산:R}pt를 " +
                        "넘었으므로 이것은 반올림 잡음이 아니라 클램프 산술의 결함입니다 — " +
                        "허용오차를 늘리지 말고 SurfaceSafeAreaPolicy.ClampCenterX를 보세요.");
                }
            }

            Debug.Log($"{LogPrefix} 우변 잔차 분포 {화면폭들.Length}×{우측띠들.Length}: " +
                      $"띠를 조금 덮음 {덮음} / 조금 남김 {남김} / 정확 {정확}. " +
                      $"예산 대비 최악 {최악비:F3} — {최악설명}. " +
                      "한쪽으로 100% 쏠리면 잡음이 아닐 수 있으니 다음 사람이 여기를 먼저 보라.");
        }

        /// <summary>
        /// ★★ <b>회귀 없음의 유일한 증거 — 비트 동일</b>. 좌·우 예약 띠가 0이면(측정된 0이든
        /// 미측정 0이든) 톱니 좌표가 변경 전과 <b>한 비트도</b> 다르지 않다.
        ///
        /// <para>허용오차 <c>0f</c>로는 부족하다 — <c>Assert.AreEqual(a, b, 0f)</c>는 <c>-0.0f</c>와
        /// <c>0.0f</c>를 같다고 하고 그 둘은 비트가 다르다. 그래서 <b>비트 패턴</b>을 직접 비교한다.</para>
        /// </summary>
        [Test]
        public void 예약띠가_0이면_톱니_좌표는_변경_전과_비트_동일하다()
        {
            float r = InfoGearIconWidget.HitRadiusPoints;
            float m = InfoGearIconWidget.SideBandMarginPoints;
            float[] 화면폭들 = { 800f, 1280f, 1512f, 1512.5f, 1920f, 2560f, 3440.75f };

            foreach (float w in 화면폭들)
            {
                // (1) 기본 위치 — DefaultCenterPoints가 요청하는 좌표.
                float 요청 = w - InfoGearIconWidget.DefaultRightMarginPoints;
                float 결과 = SurfaceSafeAreaPolicy.ClampCenterX(요청, r * 2f, w, 0f, 0f, m);
                AssertBitIdentical(요청, 결과, $"화면폭 {w} 기본 위치");

                // (2) 드래그 클램프 — 옛 식은 Mathf.Clamp(x, r, Mathf.Max(r, w − r))였다.
                //     옛 식을 여기에 <b>독립 구현</b>으로 다시 쓴다(프로덕션 함수로 기대값을 만들면
                //     그 함수가 틀어질 때 기대값도 함께 틀어져 아무것도 못 잰다 — docs/TEAM.md).
                float[] 후보 = { -500f, 0f, r, r + 0.5f, w * 0.5f, w - r, w - 30f, w, w + 500f };
                foreach (float x in 후보)
                {
                    float 옛 = Mathf.Clamp(x, r, Mathf.Max(r, w - r));
                    float 새 = SurfaceSafeAreaPolicy.ClampCenterX(x, r * 2f, w, 0f, 0f, m);
                    AssertBitIdentical(옛, 새, $"화면폭 {w} 드래그 x={x}");
                }
            }
        }

        /// <summary>
        /// 옛 동작과 <b>갈리는 유일한 구간</b>을 못 박는다 — 화면 폭이 히트 지름(39.64pt)보다 좁을 때.
        /// 옛 코드는 중심을 <c>r</c>에 박아 오른쪽으로 넘쳤고, 정책은 <i>"예약 띠가 얇은 쪽으로 넘긴다"</i>에
        /// 따라 반대로 넘긴다. 40pt보다 좁은 화면은 실재하지 않지만, <b>조용히 바뀌지 않게</b> 적어 둔다
        /// (이 저장소가 반복해서 당한 것은 언제나 "아무도 안 보는 사이에 갈라진 것"이다).
        /// </summary>
        [Test]
        public void 화면이_히트_지름보다_좁은_구간에서만_옛_동작과_갈린다()
        {
            float r = InfoGearIconWidget.HitRadiusPoints;
            float m = InfoGearIconWidget.SideBandMarginPoints;
            const float 좁은폭 = 30f;   // < 2r
            Assert.Less(좁은폭, r * 2f, $"{LogPrefix} 시나리오 전제가 깨졌습니다 — 이 폭은 좁지 않습니다.");

            float 옛 = Mathf.Clamp(999f, r, Mathf.Max(r, 좁은폭 - r));
            float 새 = SurfaceSafeAreaPolicy.ClampCenterX(999f, r * 2f, 좁은폭, 0f, 0f, m);

            Assert.AreEqual(r, 옛, 0.0001f, $"{LogPrefix} 옛 식 재현이 틀렸습니다.");
            Assert.AreEqual(좁은폭 - r, 새, 0.0001f,
                $"{LogPrefix} 좁은 화면에서 정책이 '얇은 띠 쪽으로 넘긴다'를 따르지 않았습니다.");
            Assert.AreNotEqual(옛, 새,
                $"{LogPrefix} 이 구간이 실제로는 갈리지 않습니다 — 그렇다면 이 테스트가 " +
                "존재하지 않는 차이를 문서화하고 있는 것이므로 지우세요.");
        }

        // ==================================================================
        // ⑦ 부채꼴 — 톱니를 눌러서 열리는 그것
        // ==================================================================

        /// <summary>
        /// ★ 부채꼴의 <b>좌·우 한계</b>가 <c>max(설계 여백, 관측된 띠 두께)</c>인가.
        /// 상단이 2026-09-02에 세운 식과 <b>같은 한 줄</b>을 쓴다.
        ///
        /// <para><b>띠 0에서 비트 동일</b>: <c>max(8, 0) = 8</c>. 프로덕션 상수를 참조하므로
        /// 설계 여백이 바뀌면 이 검사도 함께 따라간다.</para>
        /// </summary>
        [Test]
        public void 부채꼴_측면_여백은_설계값과_예약띠_중_큰_쪽이다()
        {
            float 설계 = GearRadialMenuWidget.ScreenMarginPoints;

            AssertBitIdentical(설계, GearRadialMenuWidget.EffectiveMarginPoints(설계, 0f),
                "띠 0에서 부채꼴 측면 여백");
            Assert.AreEqual(48f, GearRadialMenuWidget.EffectiveMarginPoints(설계, 48f), 0.0001f,
                $"{LogPrefix} 우측 도킹 48pt인데 부채꼴이 설계 여백 {설계:F0}pt만 남겼습니다 — " +
                "그 차이만큼 부채꼴 버튼이 작업표시줄 뒤로 들어갑니다.");
            Assert.AreEqual(설계, GearRadialMenuWidget.EffectiveMarginPoints(설계, 설계 * 0.5f), 0.0001f,
                $"{LogPrefix} 띠가 설계 여백보다 얇은데 여백이 줄었습니다 — " +
                "'사실만 쓰면 띠가 없는 환경에서 화면 끝에 달라붙는다'는 이유로 max입니다.");
        }

        /// <summary>
        /// ★★ 톱니만 고치고 부채꼴을 두면 <b>열린 메뉴가 여전히 띠를 덮는다</b>는 것을 못 박는다.
        ///
        /// <para>부채꼴 배치 사다리(회전 → 평행이동 → 축소 → 일렬)는 인스턴스 메서드라 EditMode에서
        /// 못 돌린다. 대신 그 사다리가 쓰는 <b>한계선</b>과 <b>상자 기하</b>(둘 다 프로덕션의
        /// public static)를 직접 대고 잰다 — 사다리가 어떤 배치를 고르든 그 결과는 이 한계선을
        /// 넘지 못한다는 것이 <c>IsBoxOnScreen</c>·<c>ShiftToFit</c>의 계약이다.</para>
        ///
        /// <para><b>실측 계산</b>(화면 1512 / 우예약 48 / 톱니 클램프 후 중심 1444.18):
        /// 톱니 바로 아래 슬롯의 상자 우변 = 1444.18 + (44+12)/2 = <b>1472.18</b>,
        /// 띠 시작 = 1464 → 옛 한계선(1512−8=1504)은 이것을 <b>통과시킨다</b>(8.18pt 침해).
        /// 새 한계선(1512−48=1464)은 <b>거부</b>하고, 사다리가 부채꼴 전체를 8.18pt 왼쪽으로 민다.</para>
        /// </summary>
        [Test]
        public void 부채꼴_상자가_우측_예약_띠_안으로_들어가지_않는다()
        {
            const float 화면폭 = 1512f, 우예약 = 48f;
            float r = InfoGearIconWidget.HitRadiusPoints;

            // 톱니가 이 라운드의 클램프를 지난 뒤의 중심 — 부채꼴은 이 점을 원점으로 펼쳐진다.
            float 톱니중심x = SurfaceSafeAreaPolicy.ClampCenterX(
                화면폭 - InfoGearIconWidget.DefaultRightMarginPoints, r * 2f, 화면폭,
                0f, 우예약, InfoGearIconWidget.SideBandMarginPoints);

            // 가장 오른쪽으로 나가는 슬롯 = 톱니 바로 아래(기본 기준각 225° + 슬롯0 오프셋 +45°).
            var 톱니중심 = new Vector2(톱니중심x, 800f);
            float 최우변 = float.NegativeInfinity;
            for (int i = 0; i < GearRadialMenuWidget.ButtonCount; i++)
            {
                Vector2 c = GearRadialMenuWidget.SlotCenterPoints(톱니중심, 225f, i);
                최우변 = Mathf.Max(최우변,
                    GearRadialMenuWidget.ButtonClampBox(c, GearRadialMenuWidget.ButtonDiameterPoints).xMax);
            }

            float 새한계 = 화면폭 - GearRadialMenuWidget.EffectiveMarginPoints(
                GearRadialMenuWidget.ScreenMarginPoints, 우예약);
            float 옛한계 = 화면폭 - GearRadialMenuWidget.ScreenMarginPoints;

            // (가) 옛 한계선은 띠 안쪽에 있다 = 부채꼴이 띠를 덮는 것을 허용했다.
            Assert.Greater(옛한계, 화면폭 - 우예약,
                $"{LogPrefix} 옛 한계선이 띠 밖에 있습니다 — 그렇다면 고칠 것이 없었다는 뜻이고, " +
                "이 테스트는 아무것도 재고 있지 않습니다.");
            Assert.Greater(최우변, 화면폭 - 우예약,
                $"{LogPrefix} 톱니를 고친 뒤에도 슬롯 상자가 띠를 침해해야 이 검사가 뜻이 있습니다 " +
                $"(계산: 상자 우변 {최우변:F2} vs 띠 시작 {화면폭 - 우예약:F2}). " +
                "침해가 없다면 부채꼴 수정은 불필요했던 것이므로 근거를 다시 쓰세요.");

            // (나) 새 한계선은 정확히 띠 시작점이다 — 사다리는 이것을 넘는 배치를 채택하지 않는다.
            Assert.AreEqual(화면폭 - 우예약, 새한계, 0.0001f,
                $"{LogPrefix} 새 한계선이 띠 시작점과 다릅니다.");
            Assert.Less(새한계, 최우변,
                $"{LogPrefix} 새 한계선이 지금 배치를 그대로 통과시킵니다 — 아무것도 안 밀립니다.");
        }

        // ==================================================================
        // ⑧ 아직 안 고친 가로축 갭 — Assert.Ignore로 러너에 계속 보이게
        // ==================================================================

        /// <summary>
        /// ★ <b>미해결 · 나머지 상단 프로브 소비 2곳의 가로축</b>.
        /// 이 라운드는 <c>InfoGearIconWidget</c>(톱니)과 <c>GearRadialMenuWidget</c>(부채꼴)까지 고쳤다.
        /// <c>PopoverPanel</c>과 <c>CharacterInfoWindow.Layout</c>은 <b>측정만</b> 했다.
        ///
        /// <para><b>갭이 닫히면 이 테스트는 실패한다</b>(Ignore가 아니라) — 명부가 조용히 늙지 않게.</para>
        /// </summary>
        [Test]
        public void 팝오버와_정보창의_가로축은_아직_예약띠를_모른다_미해결()
        {
            string popover = ReadSource(Path.Combine(InteractionRoot, "PopoverPanel.cs"));
            string infoWin = ReadSource(Path.Combine(InteractionRoot, "CharacterInfoWindow.Layout.cs"));

            // ★ 존재 대조 — 두 파일 다 <b>세로축</b>은 이미 정책을 부른다. 이게 참이라야
            //   아래 "가로축 부재"가 '스캐너가 죽었다'가 아니라 '정말 없다'가 된다.
            StringAssert.Contains(nameof(SurfaceSafeAreaPolicy.ClampCenterY), popover,
                $"{LogPrefix} PopoverPanel이 세로축 정책조차 안 부릅니다 — 니들이 썩었습니다.");
            StringAssert.Contains(nameof(SurfaceSafeAreaPolicy.ClampCenterOriginOffsetY), infoWin,
                $"{LogPrefix} CharacterInfoWindow.Layout이 세로축 정책조차 안 부릅니다 — 니들이 썩었습니다.");

            bool popoverFixed = popover.IndexOf(nameof(SurfaceSafeAreaPolicy.ClampCenterX),
                StringComparison.Ordinal) >= 0;
            bool infoFixed = infoWin.IndexOf(nameof(SurfaceSafeAreaPolicy.ClampCenterOriginOffsetX),
                StringComparison.Ordinal) >= 0;

            if (popoverFixed || infoFixed)
            {
                Assert.Fail($"{LogPrefix} 갭이 닫혔습니다 — " +
                    $"팝오버={popoverFixed}, 정보창={infoFixed}. 이 Assert.Ignore를 실제 검사로 승격하고 " +
                    "명부(TestClaimExpiryAuditTests)에서 이 항목을 지우세요.");
            }

            Assert.Ignore(
                $"{LogPrefix} 【미해결 · 가로축 잔여 2곳】 2026-09-03 (dev-platform)\n" +
                "  · 이 라운드에 착지: 톱니(InfoGearIconWidget) · 부채꼴(GearRadialMenuWidget) 가로축.\n" +
                "  · 미착지 ①  CharacterInfoWindow.Layout.ClampPanelPosition — 실제 침해가 있다.\n" +
                "      계산(1512폭 / 창폭 1042 / ScreenMargin 16): maxX = (1512−1042)/2 − 16 = 219 →\n" +
                "      오른쪽 끝까지 끌면 창 우변이 화면 오른쪽에서 16pt → 우예약 48이면 32pt, 62면 46pt 침해.\n" +
                "      다만 톱니와 달리 <b>버튼이 죽는 결함이 아니다</b>(창은 1010pt가 남고 드래그로 되돌릴 수 있다).\n" +
                "  · 미착지 ②  PopoverPanel.UpdatePlacement — <b>구조적으로는 눈이 없지만 지금은 도달 불가</b>.\n" +
                "      가로 클램프가 Screen.width − (12 + 반폭)이라 띠를 모른다(최악 36pt 침해 여지).\n" +
                "      그러나 팝오버 앵커는 <b>언제나 부채꼴 버튼</b>이고(3개 호출부 전부),\n" +
                "      팝오버는 앵커에서 <b>화면 중앙 쪽</b>으로만 눕는다. 부채꼴이 이 라운드에 띠를 알게 됐으므로\n" +
                "      계산상 침해 0이다(1512/띠48: 우변 1380.4 / 1279.6 / 1263.1 vs 띠 시작 1464).\n" +
                "  · 왜 이 라운드에서 안 했나: 두 곳 다 <b>비트 동일 보증이 이 라운드의 방식으로는 안 나온다</b>.\n" +
                "      정보창은 <c>ClampCenterOriginOffsetX</c>가 (W/2 + dx) − W/2 왕복을 하는데 그 왕복은\n" +
                "      dx의 하위 비트를 잃는다(비트 동일 아님). 팝오버는 상·하한 식의 <b>뺄셈 순서</b>가\n" +
                "      옛 식과 달라 경계에서 1 ULP가 갈릴 수 있다. 두 축 다 '띠 0에서 비트 동일'을\n" +
                "      성립시키려면 정책에 <b>새 창구</b>가 필요하고, 그건 별도 라운드의 판단이다.\n" +
                "\n" +
                "  【실기 미확인 — 이 머신에 Windows가 없다】 위 숫자는 전부 <b>계산</b>이다.\n" +
                "  · 실측이 필요한 것: (1) 좌/우 도킹에서 rcMonitor−rcWork가 내는 실제 물리px,\n" +
                "    (2) 배율 150%에서 논리 pt 환산이 소비 측과 일치하는지,\n" +
                "    (3) 자동 숨김 작업표시줄을 우리가 강제로 보이게 한 상태(승인된 예외 1건)에서\n" +
                "        rcWork가 실제로 좁아지는지 — 좁아지지 않으면 이 모든 회피가 헛돈다.");
        }


        // ==================================================================
        // 도구 — 소스 파싱 (전부 이 파일 안에서만 쓴다)
        // ==================================================================

        /// <summary>
        /// 두 float의 <b>비트 패턴</b>이 같은지. <c>Assert.AreEqual(a, b, 0f)</c>보다 강하다 —
        /// 그쪽은 <c>-0.0f == 0.0f</c>를 통과시키고 그 둘은 비트가 다르다.
        /// <para><c>BitConverter.GetBytes</c>를 쓰는 이유: <c>SingleToInt32Bits</c>는 런타임 버전에
        /// 따라 없을 수 있는데, 이 검사가 <b>컴파일 안 되는 것</b>과 <b>다르게 재는 것</b> 중
        /// 후자가 훨씬 위험하다.</para>
        /// </summary>
        /// <summary>
        /// <paramref name="magnitude"/> 근처에서 float 하나가 갖는 <b>1 ULP</b>(이웃한 두 표현값 사이 간격).
        /// 비트 패턴을 1 올려서 구한다 — 지수 구간을 손으로 계산하지 않는다(그 산수도 틀릴 수 있다).
        /// </summary>
        private static float UlpAt(float magnitude)
        {
            float m = Math.Abs(magnitude);
            if (float.IsNaN(m) || float.IsInfinity(m)) return 0f;
            int bits = BitConverter.ToInt32(BitConverter.GetBytes(m), 0);
            float next = BitConverter.ToSingle(BitConverter.GetBytes(bits + 1), 0);
            return next - m;
        }

        /// <summary>
        /// ★★ 2026-09-03 — <b>허용오차를 손으로 고르지 않기 위한 유도 함수.</b>
        ///
        /// ============================================================================
        /// 무엇을 재는가 — 이 잔차의 출처는 <b>뺄셈 딱 한 번</b>이다
        /// ============================================================================
        /// 톱니 클램프의 상한은 <c>maxCenterX = 화면폭 − 우예약 − 여백 − 반지름</c>이고, 그 결과가
        /// <b>float 하나에 담기면서</b> 최대 <c>½ ULP</c>만큼 반올림된다. 히트 우변은
        /// <c>화면폭 − (maxCenterX + 반지름)</c>이므로 그 반올림 오차 <b>e</b>가 그대로
        /// <c>우변 = 우예약 + 여백 − e</c>로 나온다. 즉 잔차는 <b>정확히 그 한 번의 반올림</b>이다.
        ///
        /// <para><b>실측 검산(2026-09-03 러너, docs/verify/runs/lead-final_edit.xml)</b>:
        /// 화면폭 1512 / 우예약 48 / r = 19.82f(= 19.819999694824219) 일 때
        /// <c>1464 − r</c>의 <b>정확한</b> 값은 <c>1444.180000305175781</c>이고, 이는 2⁻¹³ 격자에서
        /// <c>11830722.5625</c>칸이라 <c>11830723</c>칸(<c>+0.4375 ULP</c>)으로 올림된다.
        /// <c>0.4375 × 2⁻¹³ = 5.340576171875e-5</c> — 러너가 낸
        /// <c>47.9999466</c>(= 48 − 그 값, 48에서 정확히 14 ULP 아래)와 <b>비트까지 일치</b>한다.
        /// 즉 이 잔차는 pt 환산도, 히트 반지름 계산도, 화면 폭 나눗셈도 아니다.</para>
        ///
        /// ============================================================================
        /// 계통 오차인가, 잡음인가 — <b>잡음이다</b>(그래서 프로덕션을 안 고친다)
        /// ============================================================================
        /// 부호가 <b>양쪽으로 갈린다</b>. 같은 식에서 화면폭 800pt면 우변이 띠보다
        /// <c>+7.6e-6pt</c> <b>남고</b>, 1512pt면 <c>−5.3e-5pt</c> <b>모자란다</b> — 방향은
        /// (화면폭 − 우예약 − r)이 2⁻¹³ 격자의 어느 쪽에 떨어지느냐가 정하며, 클램프가 띠를
        /// 조금씩 먹는 <b>편향이 아니다</b>. 아래 <see cref="우변_잔차는_한_번의_float_반올림_예산_안에_있다"/>가
        /// 화면폭 19종 × 띠 10종을 훑어 이 진술을 매 실행 다시 증명한다.
        ///
        /// <para><b>그리고 이 잔차는 float32만 쓰면 아예 0이다</b> — <c>(W−r)+r</c>이 W로 되돌아오기
        /// 때문이다(오프라인 float32 모사에서 정확히 48.0). 러너 값이 0이 아닌 것은 에디터 Mono가
        /// 중간값을 더 넓은 정밀도로 들고 있다가 <b>마지막에 한 번만</b> float로 접기 때문이고,
        /// 그래서 상쇄가 일어나지 않는다. <b>이 축은 실기(IL2CPP) 미확인이다</b> — 다만 어느 쪽이든
        /// 잔차는 아래 예산 안이라 판정은 같다.</para>
        ///
        /// <para><b>물리적 크기</b>: 5.3e-5pt는 배율 2 화면에서 1.1e-4물리px다. 창은 정수 픽셀에
        /// 놓이므로 이 값이 실제로 무언가를 덮는 일은 없다. 그래도 <b>예산으로 못 박는</b> 이유는,
        /// 예산을 넘는 순간 그건 반올림이 아니라 산술 결함이기 때문이다.</para>
        ///
        /// <para>★ <b>띠가 0일 때의 비트 동일 단언은 이 예산과 무관하게 그대로 엄격하다</b>
        /// (<see cref="AssertBitIdentical"/>) — 그것이 회귀 없음의 유일한 증거다.</para>
        /// </summary>
        /// <param name="clampMagnitude">클램프 상한이 계산되는 크기(화면폭 − 우예약). 여기서 뺄셈 반올림이 난다.</param>
        /// <param name="resultMagnitude">결과가 담기는 크기(우예약). 마지막 float 저장에서 한 번 더 접힌다.</param>
        private static float FloatRoundingBudget(float clampMagnitude, float resultMagnitude)
            => UlpAt(clampMagnitude) * 0.5f + UlpAt(resultMagnitude) * 0.5f;

        private static void AssertBitIdentical(float expected, float actual, string what)
        {
            int a = BitConverter.ToInt32(BitConverter.GetBytes(expected), 0);
            int b = BitConverter.ToInt32(BitConverter.GetBytes(actual), 0);
            Assert.AreEqual(a, b,
                $"{LogPrefix} {what}: 예약 띠가 0인데 좌표가 바뀌었습니다 " +
                $"({expected:R} -> {actual:R}, 비트 0x{a:X8} -> 0x{b:X8}). " +
                "이 함수를 끼워 넣는 것만으로 배치가 움직이면 그것이 회귀입니다 — " +
                "띠가 없는 사용자가 압도적 다수입니다.");
        }

        private static string ReadSource(string path)
        {
            Assert.IsTrue(File.Exists(path),
                $"{LogPrefix} 감사 대상 파일이 없습니다: {path} — 파일을 옮겼다면 이 검사도 함께 갱신하세요. " +
                "그대로 두면 '못 찾았다'가 조용한 초록이 됩니다.");
            return File.ReadAllText(path);
        }

        /// <summary>줄 주석(<c>//</c>, XML <c>///</c> 포함)을 지운다 — 결함을 설명하는 <b>주석</b>이
        /// 구현으로 오인되는 것이 이 저장소의 대표적 거짓 초록이다.</summary>
        private static string StripComments(string source)
        {
            var sb = new System.Text.StringBuilder(source.Length);
            foreach (string line in source.Split('\n'))
            {
                int at = line.IndexOf("//", StringComparison.Ordinal);
                sb.Append(at >= 0 ? line.Substring(0, at) : line).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary><c>class X</c> 선언과 여는 중괄호 <b>사이</b>(= 기반 목록)만 잘라 낸다.</summary>
        private static bool TryGetBaseList(string source, string className, out string baseList)
        {
            baseList = string.Empty;
            int at = source.IndexOf("class " + className, StringComparison.Ordinal);
            if (at < 0) return false;
            int brace = source.IndexOf('{', at);
            if (brace < 0) return false;
            int colon = source.IndexOf(':', at + ("class " + className).Length);
            if (colon < 0 || colon > brace) return false;
            baseList = source.Substring(colon + 1, brace - colon - 1);
            return true;
        }

        private static void AssertDeclaresInterface(
            string path, string className, string interfaceName, string requiredMethodName)
        {
            string src = StripComments(ReadSource(path));

            Assert.IsTrue(TryGetBaseList(src, className, out string baseList),
                $"{LogPrefix} {Path.GetFileName(path)}에서 '{className}' 선언(또는 그 기반 목록)을 " +
                "찾지 못했습니다 — 감사 앵커가 낡았습니다. 그대로 두면 '못 찾았다'가 조용한 초록이 됩니다.");

            StringAssert.Contains(interfaceName, baseList,
                $"{LogPrefix} {Path.GetFileName(path)}의 {className}이 {interfaceName}을 " +
                "**기반 목록에 달지 않았습니다**. 메서드만 있고 인터페이스가 없으면 소비 측의 " +
                "'is/as' 판정이 조용히 null이 되어, 기능이 있는 것처럼 보이면서 한 번도 호출되지 " +
                $"않습니다(컴파일도 통과합니다).\n찾은 기반 목록: {baseList.Replace("\n", " ").Trim()}");

            StringAssert.Contains(requiredMethodName + "(", src,
                $"{LogPrefix} {Path.GetFileName(path)}에 계약 메서드 {requiredMethodName}()가 없습니다.");
        }

        /// <summary>
        /// 메서드 <b>본문</b>만 중괄호 짝을 세어 잘라 낸다. 파일 전체를 대상으로 부재 단언을 하면
        /// 다른 메서드의 같은 문자열에 걸려 <b>영원히 빨강</b>이거나, 반대로 범위가 너무 넓어 뜻을 잃는다.
        /// </summary>
        private static bool TryGetMethodBody(string source, string methodName, out string body)
        {
            body = string.Empty;
            int at = source.IndexOf(methodName + "(", StringComparison.Ordinal);
            while (at >= 0)
            {
                int brace = source.IndexOf('{', at);
                if (brace < 0) return false;

                int depth = 0;
                for (int i = brace; i < source.Length; i++)
                {
                    if (source[i] == '{') depth++;
                    else if (source[i] == '}')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            body = source.Substring(brace, i - brace + 1);
                            return true;
                        }
                    }
                }
                at = source.IndexOf(methodName + "(", at + 1, StringComparison.Ordinal);
            }
            return false;
        }

        /// <summary>★ 양성 대조 — 위 파서들이 <b>실제로 있는 것을 찾아내는지</b> 먼저 증명한다.
        /// 파서가 죽으면 이 파일의 모든 "없다" 판정이 무효다(이 저장소의 사고 #4·#5).</summary>
        [Test]
        public void 양성대조_소스_파서가_살아_있다()
        {
            string probeSrc = StripComments(ReadSource(Path.Combine(PlatformRoot, "ReservedEdgeProbe.cs")));

            // ★ 중괄호 본문 메서드를 고른다 — 식 본문(=>)에는 자를 중괄호가 없다.
            Assert.IsTrue(TryGetMethodBody(probeSrc, "Insets", out string body),
                $"{LogPrefix} 실재하는 메서드 본문을 못 잘랐습니다 — 파서가 죽었습니다.");
            Assert.IsFalse(TryGetMethodBody(probeSrc, "이런메서드는존재하지않는다", out _),
                $"{LogPrefix} 존재하지 않는 메서드를 찾아냈습니다 — 파서가 아무 문자열이나 잡습니다.");
            StringAssert.Contains(nameof(ReservedEdgeProbe.RefreshIntervalSeconds), body,
                $"{LogPrefix} 잘라 낸 본문이 그 메서드의 것이 아닙니다.");

            string macEdge = StripComments(ReadSource(
                Path.Combine(PlatformRoot, "MacOS", "MacReservedScreenEdgeService.cs")));
            Assert.IsTrue(TryGetBaseList(macEdge, MacEdgeServiceClass, out string bases),
                $"{LogPrefix} 실재하는 기반 목록을 못 읽었습니다.");
            StringAssert.Contains(nameof(IReservedScreenEdgeService), bases);
            Assert.IsFalse(TryGetBaseList(macEdge, "이런클래스는존재하지않는다", out _),
                $"{LogPrefix} 존재하지 않는 클래스의 기반 목록을 읽어 냈습니다.");

            // 주석 제거가 실제로 동작하는가 — 미끼로 확인한다(이름이 주석에만 있으면 세면 안 된다).
            const string decoy = "public sealed class Decoy : IPlatformWindowService\n" +
                                 "{\n    // " + nameof(IReservedScreenEdgeService) + " 미구현\n}\n";
            Assert.IsTrue(TryGetBaseList(StripComments(decoy), "Decoy", out string decoyBases));
            StringAssert.DoesNotContain(nameof(IReservedScreenEdgeService), decoyBases,
                $"{LogPrefix} 주석에 있는 인터페이스 이름이 '구현했다'로 집계됩니다 — " +
                "이 감사는 그 순간 영구 거짓 초록이 됩니다.");
        }
    }
}
