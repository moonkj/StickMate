using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.Platform;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 창 도둑(docs/UX_FLOW.md 27-1)이 <b>크기 설정 변경만으로 조용히 죽었던</b> 사건의 회귀 방지
    /// (Tasklist.md "크기 변경이 조용히 죽인 것", 2026-08-29).
    ///
    /// 무엇이 죽었나 — 두 가지가 겹쳤다.
    ///  (1) 대상 창 폭 상한이 <b>캐릭터 신장에만 비례</b>했다. characterScale은 순수 시각 설정인데,
    ///      0.75로 내리자 상한이 237pt(실측 신장 79.0pt x 3)로 줄어 macOS 표준 창 대부분이 탈락했고
    ///      (계산기 230pt가 겨우 걸치는 수준, 배율 0.5였다면 158pt로 계산기조차 탈락) 후보가 사실상
    ///      0개가 됐다.
    ///  (2) 후보 소스가 <b>발판 목록</b>(= 상단 테두리가 실제로 보이는 창)이라, 작은 창이 큰 창 뒤에
    ///      가려져 있으면 폭 판정에 도달하기도 전에 사라졌다.
    ///
    /// 이 테스트는 그 두 조건을 <b>절대값</b>으로 잠근다(상대적 여유 금지 — 이 프로젝트는 상대 여유
    /// 방식 테스트가 버그를 놓친 전례가 있다). 각 항목마다 <b>네거티브 컨트롤</b>을 함께 둬서, 수정
    /// 이전 값/이전 구조를 넣으면 반드시 실패하는지도 같이 확인한다.
    ///
    /// 원칙 3: 여기서는 사각형 숫자만 만든다 — 실제 창을 열거하지도, 건드리지도 않는다.
    /// </summary>
    public class WindowTheftTargetSelectionTests
    {
        private const string DefaultConfigPath = "Assets/_Project/Data/DefaultStickConfig.asset";

        /// <summary>실측값(2026-08-29, 이 개발기): characterScale=0.75에서 클릭 히트박스 OS 높이.
        /// 로그 원문 — "히트박스 OS=(x:650.07, y:832.59, width:24.55, height:79.01)".</summary>
        private const float MeasuredCharacterHeightAt075 = 79.01f;

        /// <summary>실측값: 배율 0.5였을 때의 신장(위 값 x 0.5/0.75).</summary>
        private const float MeasuredCharacterHeightAt050 = 52.67f;

        /// <summary>실측값: macOS 계산기 창 폭(발판리포트 원문 "계산기@(306,454 230x408)").</summary>
        private const float CalculatorWidthPt = 230f;

        /// <summary>실측값: Finder 창 폭(27-1이 "너무 커서 안 민다"고 봐야 하는 쪽).</summary>
        private const float FinderWidthPt = 483f;

        private static StickConfig LoadDefaultConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<StickConfig>(DefaultConfigPath);
            Assert.IsNotNull(config, $"기본 설정 자산을 찾지 못했습니다: {DefaultConfigPath}");
            return config;
        }

        private static StickConfig NewConfig(float multiplier, float minWidth)
        {
            var c = ScriptableObject.CreateInstance<StickConfig>();
            c.windowTheftMaxTargetWidthMultiplier = multiplier;
            c.windowTheftMinTargetWidthPoints = minWidth;
            return c;
        }

        private static PlatformFoothold Window(long handle, float width)
            => new PlatformFoothold(handle, new Rect(100f, 100f, width, 300f), false);

        // ==================== 1. 폭 상한 공식 ====================

        [Test]
        public void 폭_상한은_절대하한_아래로_내려가지_않는다()
        {
            StickConfig c = NewConfig(3f, 280f);
            float limit = WindowTheftTargetRules.ComputeMaxTargetWidthOsPx(MeasuredCharacterHeightAt075, c);
            Assert.AreEqual(280f, limit, 0.01f,
                $"배율 0.75(신장 {MeasuredCharacterHeightAt075}pt)에서 상한이 {limit:F1}pt입니다 — " +
                "신장 비례값(237pt)이 아니라 절대 하한 280pt가 채택돼야 합니다. 이 규칙이 없으면 " +
                "characterScale(순수 시각 설정)이 게임플레이 가용성을 조용히 바꿉니다.");
        }

        [Test]
        public void 네거티브_컨트롤_절대하한이_0이면_수정_이전_거동이_그대로_재현된다()
        {
            StickConfig c = NewConfig(3f, 0f); // 수정 전 공식과 동일.
            float limit = WindowTheftTargetRules.ComputeMaxTargetWidthOsPx(MeasuredCharacterHeightAt075, c);
            Assert.AreEqual(237.03f, limit, 0.05f, "네거티브 컨트롤이 성립하지 않습니다(공식이 신장 x 배수가 아님).");

            StickConfig half = NewConfig(3f, 0f);
            float halfLimit = WindowTheftTargetRules.ComputeMaxTargetWidthOsPx(MeasuredCharacterHeightAt050, half);
            Assert.Less(halfLimit, CalculatorWidthPt,
                $"배율 0.5 + 절대하한 0에서 상한이 {halfLimit:F1}pt로 계산기 폭({CalculatorWidthPt}pt)보다 " +
                "작아야 합니다 — 이게 이번에 고친 '조용한 죽음'의 재현 조건입니다.");
        }

        [Test]
        public void 캐릭터가_아무리_작아져도_계산기는_후보로_남는다()
        {
            StickConfig c = LoadDefaultConfig();
            foreach (float height in new[] { MeasuredCharacterHeightAt050, MeasuredCharacterHeightAt075, 105.3f })
            {
                float limit = WindowTheftTargetRules.ComputeMaxTargetWidthOsPx(height, c);
                Assert.IsTrue(WindowTheftTargetRules.IsEligibleTarget(Window(1, CalculatorWidthPt), limit),
                    $"신장 {height:F1}pt에서 상한이 {limit:F1}pt라 계산기({CalculatorWidthPt}pt)가 탈락했습니다 — " +
                    "배율을 바꿨다고 훔칠 수 있는 창의 종류가 바뀌면 안 됩니다.");
            }
        }

        [Test]
        public void 큰_창은_여전히_거부한다_상한을_없앤_것이_아니다()
        {
            StickConfig c = LoadDefaultConfig();
            float limit = WindowTheftTargetRules.ComputeMaxTargetWidthOsPx(MeasuredCharacterHeightAt075, c);
            Assert.IsFalse(WindowTheftTargetRules.IsEligibleTarget(Window(1, FinderWidthPt), limit),
                $"Finder 폭({FinderWidthPt}pt)이 상한 {limit:F1}pt를 통과했습니다 — 27-1은 큰 창을 억지로 " +
                "대상으로 삼는 것을 금지합니다(밀어도 안 움직이는 게 당연해 보여 개그가 죽는다).");
            Assert.IsFalse(WindowTheftTargetRules.IsEligibleTarget(Window(1, limit + 0.5f), limit),
                "상한을 0.5pt 넘긴 창이 통과했습니다(경계 판정이 <= 가 아님).");
            Assert.IsTrue(WindowTheftTargetRules.IsEligibleTarget(Window(1, limit), limit),
                "정확히 상한과 같은 폭의 창이 거부됐습니다(경계 판정이 < 로 좁아짐).");
        }

        [Test]
        public void 설정_자산의_절대하한은_계산기를_담을_수_있어야_한다()
        {
            StickConfig c = LoadDefaultConfig();
            Assert.GreaterOrEqual(c.windowTheftMinTargetWidthPoints, CalculatorWidthPt,
                $"windowTheftMinTargetWidthPoints({c.windowTheftMinTargetWidthPoints:F0}pt)가 계산기 폭" +
                $"({CalculatorWidthPt}pt)보다 작습니다 — 이 값이 이 관계를 깨는 순간 기능이 다시 조용히 죽습니다.");
            Assert.Less(c.windowTheftMinTargetWidthPoints, FinderWidthPt,
                $"windowTheftMinTargetWidthPoints({c.windowTheftMinTargetWidthPoints:F0}pt)가 Finder 폭" +
                $"({FinderWidthPt}pt) 이상입니다 — 하한을 이렇게까지 올리면 '작은 창만 민다'는 27-1이 무너집니다.");
        }

        // ==================== 2. 후보 자격 ====================

        [Test]
        public void 합성_발판과_폭_0은_후보가_아니다()
        {
            const float limit = 280f;
            Assert.IsFalse(WindowTheftTargetRules.IsEligibleTarget(Window(-1, 200f), limit),
                "Dock/안전망 합성 발판(Handle<0)이 후보가 됐습니다 — 원본 창이 없어 고스트를 그릴 대상이 아닙니다.");
            Assert.IsFalse(WindowTheftTargetRules.IsEligibleTarget(Window(7, 0f), limit),
                "폭 0인 창이 후보가 됐습니다.");
        }

        [Test]
        public void 후보_수집은_버퍼를_재사용하고_자격있는_것만_담는다()
        {
            const float limit = 280f;
            var source = new List<PlatformFoothold>
            {
                Window(-1, 120f),               // 합성 발판
                Window(11, CalculatorWidthPt),  // 통과
                Window(12, FinderWidthPt),      // 너무 넓음
                Window(13, 279f),               // 통과
            };
            var buffer = new List<PlatformFoothold>(8);
            buffer.Add(Window(99, 10f)); // 앞 호출의 잔재 — Clear되어야 한다.

            int count = WindowTheftTargetRules.CollectCandidates(source, limit, buffer);

            Assert.AreEqual(2, count, "자격 있는 창의 수가 다릅니다.");
            Assert.AreEqual(2, buffer.Count, "버퍼가 Clear되지 않아 앞 호출의 잔재가 남았습니다.");
            CollectionAssert.AreEquivalent(new long[] { 11, 13 }, new[] { buffer[0].Handle, buffer[1].Handle });
        }

        // ==================== 3. 후보 소스: 가려짐 필터 이전 원본 창 ====================

        /// <summary>발판 목록과 원본 창 목록이 서로 다른 상황(= 실제 데스크톱)을 그대로 재현하는 가짜 서비스.</summary>
        private sealed class FakeOccludingService : IPlatformWindowService, IRawWindowRectSource
        {
            private readonly List<PlatformFoothold> _footholds = new List<PlatformFoothold>();
            private readonly List<PlatformFoothold> _raw = new List<PlatformFoothold>();

            public FakeOccludingService()
            {
                // 앞의 큰 창만 발판이 된다(작은 계산기는 그 뒤에 완전히 가려져 발판 목록에서 빠짐).
                _footholds.Add(Window(100, FinderWidthPt));
                _raw.Add(Window(100, FinderWidthPt));
                _raw.Add(Window(200, CalculatorWidthPt));
            }

            public IReadOnlyList<PlatformFoothold> EnumerateFootholds() => _footholds;
            public IReadOnlyList<PlatformFoothold> RawWindows => _raw;
            public bool CreateOverlayWindow() => true;
            public void SetClickThrough(bool enabled) { }
            public void SetAlwaysOnTop(bool enabled) { }
            public bool IsFullscreenAppActive() => false;
        }

        /// <summary>네거티브 컨트롤용 — 원본 창 채널을 지원하지 않는 플랫폼(Windows/모바일/에디터 폴백).</summary>
        private sealed class FakeFootholdOnlyService : IPlatformWindowService
        {
            private readonly List<PlatformFoothold> _footholds = new List<PlatformFoothold>
            {
                Window(100, FinderWidthPt),
            };

            public IReadOnlyList<PlatformFoothold> EnumerateFootholds() => _footholds;
            public bool CreateOverlayWindow() => true;
            public void SetClickThrough(bool enabled) { }
            public void SetAlwaysOnTop(bool enabled) { }
            public bool IsFullscreenAppActive() => false;
        }

        [Test]
        public void 폴러는_가려진_창까지_담은_원본_목록을_따로_노출한다()
        {
            var config = ScriptableObject.CreateInstance<StickConfig>();
            var poller = new FootholdPoller(new FakeOccludingService(), config);

            Assert.AreEqual(1, poller.CachedFootholds.Count,
                "발판 목록이 바뀌었습니다 — 이번 수정은 발판 열거/가려짐 계산을 건드리면 안 됩니다(접지/걷기의 근간).");
            Assert.AreEqual(100L, poller.CachedFootholds[0].Handle);

            Assert.AreEqual(2, poller.CachedRawWindows.Count,
                "원본 창 목록에 가려진 창이 빠졌습니다 — 이 채널의 존재 이유가 바로 그 창입니다.");

            bool foundOccluded = false;
            for (int i = 0; i < poller.CachedRawWindows.Count; i++)
            {
                if (poller.CachedRawWindows[i].Handle == 200L) foundOccluded = true;
            }
            Assert.IsTrue(foundOccluded,
                "완전히 가려진 계산기(handle=200)가 원본 창 목록에 없습니다 — 창 도둑은 '미는' 연출이라 " +
                "가려짐과 무관하게 대상이 될 수 있어야 합니다.");
        }

        [Test]
        public void 가려진_계산기는_실제로_후보로_선정된다()
        {
            StickConfig c = LoadDefaultConfig();
            var poller = new FootholdPoller(new FakeOccludingService(), c);
            float limit = WindowTheftTargetRules.ComputeMaxTargetWidthOsPx(MeasuredCharacterHeightAt075, c);

            var buffer = new List<PlatformFoothold>(8);
            int fromRaw = WindowTheftTargetRules.CollectCandidates(poller.CachedRawWindows, limit, buffer);
            Assert.AreEqual(1, fromRaw, "원본 창 목록에서 후보가 정확히 1개(계산기)여야 합니다.");
            Assert.AreEqual(200L, buffer[0].Handle, "가려진 계산기가 아닌 다른 창이 뽑혔습니다.");

            // 네거티브 컨트롤 — 예전 소스(발판 목록)로는 같은 상황에서 후보가 0개다(= 조용한 죽음).
            int fromFootholds = WindowTheftTargetRules.CollectCandidates(poller.CachedFootholds, limit, buffer);
            Assert.AreEqual(0, fromFootholds,
                "발판 목록으로도 후보가 잡혔습니다 — 이 네거티브 컨트롤이 깨지면 이번 수정의 필요성 자체가 " +
                "증명되지 않습니다(가려진 창이 발판 목록에 새어 들어왔다는 뜻).");
        }

        [Test]
        public void 네거티브_컨트롤_원본_채널_미지원_플랫폼에서는_빈_목록이다()
        {
            var config = ScriptableObject.CreateInstance<StickConfig>();
            var poller = new FootholdPoller(new FakeFootholdOnlyService(), config);

            Assert.AreEqual(0, poller.CachedRawWindows.Count,
                "원본 창 채널을 구현하지 않은 서비스인데 목록이 비어 있지 않습니다 — 소비 측의 " +
                "'비어 있으면 발판 목록으로 폴백' 판정이 무너집니다.");
            Assert.AreEqual(1, poller.CachedFootholds.Count, "폴백 경로의 발판 목록까지 함께 죽었습니다.");
        }

        [Test]
        public void 폴백_데코레이터가_원본_채널을_그대로_통과시킨다()
        {
            var inner = new FakeOccludingService();
            var decorated = new FallbackPlatformWindowService(inner);
            var raw = decorated as IRawWindowRectSource;

            Assert.IsNotNull(raw,
                "FallbackPlatformWindowService가 IRawWindowRectSource를 구현하지 않습니다 — 실제 런타임은 " +
                "MacWindowService를 이 데코레이터로 감싸서 쓰므로, 통과시키지 않으면 채널이 런타임에서 사라집니다" +
                "(IGlobalPointerButtonService가 같은 이유로 한 번 조용히 끊겼던 전례가 있습니다).");
            Assert.AreEqual(2, raw.RawWindows.Count, "내부 서비스의 원본 창 목록이 그대로 전달되지 않았습니다.");

            var noRaw = new FallbackPlatformWindowService(new FakeFootholdOnlyService()) as IRawWindowRectSource;
            Assert.IsNotNull(noRaw);
            Assert.AreEqual(0, noRaw.RawWindows.Count,
                "원본 채널이 없는 내부 서비스인데 빈 목록이 아닙니다(null 반환은 소비 측 NRE 위험).");
        }
    }
}
