using System.IO;
using NUnit.Framework;
using StickMate.Platform;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-05 진단 로그 (b) — Windows 오버레이 WS_EX_LAYERED 타임라인의 <b>엣지 규칙·포맷·상한</b>을 잠근다
    /// (docs/DEBUG_WINDOWS_THROW_LANDING_STALL.md §6). 규칙은 플랫폼 중립 <see cref="LayeredHybridTimeline"/>에 있어
    /// Windows 실기 없이 전 분기를 실행한다. 시간은 벽시계 초를 인자로 넘긴다 — 기다리지 않는다.
    /// 배선(Windows 전용 파일이 실제로 이 타임라인을 부르는가)은 소스 니들로 잠그되, 니들이 살아 있음을
    /// 같은 테스트 안의 대조로 못박는다(CLAUDE.md "부재 단언은 썩으면 조용히 초록").
    /// </summary>
    public sealed class ThrowLandingDiag_LayeredTimelineTests
    {
        [Test]
        public void 라이브러리_관통_캐시는_바뀐_프레임에만_한줄이고_같은_값이면_침묵한다()
        {
            var t = new LayeredHybridTimeline();
            string first = t.ObserveLibraryClickThrough(true, frame: 100, nowSeconds: 10.000f);
            string same = t.ObserveLibraryClickThrough(true, frame: 101, nowSeconds: 10.016f);
            string flipped = t.ObserveLibraryClickThrough(false, frame: 115, nowSeconds: 10.250f);

            Assert.IsNotNull(first);
            StringAssert.Contains("타임라인 #1", first);
            StringAssert.Contains("초기 관측", first);
            StringAssert.Contains("프레임#100", first);
            StringAssert.Contains("t=10.000초", first);
            StringAssert.Contains("첫 사건", first);
            Assert.IsNull(same, "같은 값이 이어지면 아무것도 남기지 않는다(24시간 상주 — 프레임마다 로그 금지).");
            Assert.IsNotNull(flipped);
            StringAssert.Contains("타임라인 #2", flipped);
            StringAssert.Contains("관통 OFF", flipped);
            StringAssert.Contains("직전 사건 +0.250초", flipped);
            Assert.AreEqual(2, t.EventCount);
            Assert.AreEqual(2, t.LineCount);
        }

        [Test]
        public void OS_실측_LAYERED_비트도_엣지에서만_남는다()
        {
            var t = new LayeredHybridTimeline();
            Assert.IsNotNull(t.ObserveOsLayered(false, 1, 1f));
            Assert.IsNull(t.ObserveOsLayered(false, 2, 1.25f));
            string on = t.ObserveOsLayered(true, 3, 1.5f);
            Assert.IsNotNull(on);
            StringAssert.Contains("WS_EX_LAYERED 켜짐", on);
            string off = t.ObserveOsLayered(false, 4, 1.75f);
            StringAssert.Contains("WS_EX_LAYERED 꺼짐", off);
        }

        [Test]
        public void 제거와_복구는_매번_남고_누적_횟수와_사유를_적는다()
        {
            var t = new LayeredHybridTimeline();
            string strip = t.NoteStripped(7, 500, 42.5f);
            StringAssert.Contains("WS_EX_LAYERED 제거", strip);
            StringAssert.Contains("누적 제거 7회", strip);
            StringAssert.Contains("프레임#500", strip);
            string restore = t.NoteRestored("검증 실패 사유 X", 501, 42.6f);
            StringAssert.Contains("WS_EX_LAYERED 복구", restore);
            StringAssert.Contains("검증 실패 사유 X", restore);
            StringAssert.Contains("직전 사건 +0.100초", restore);
        }

        [Test]
        public void 모든_줄은_레이어드해소_태그로_시작하고_한_줄이다()
        {
            var t = new LayeredHybridTimeline();
            string[] lines =
            {
                t.ObserveLibraryClickThrough(true, 1, 0f),
                t.ObserveOsLayered(true, 2, 0.25f),
                t.NoteStripped(1, 3, 0.5f),
                t.NoteRestored("x", 4, 0.75f),
            };
            foreach (string line in lines)
            {
                Assert.IsNotNull(line);
                StringAssert.StartsWith(LayeredHybridTimeline.LogPrefix + " 타임라인", line);
                Assert.IsFalse(line.Contains("\n"));
            }
            // 태그는 해소기가 쓰던 그 단어여야 사용자가 한 단어로 전부 찾는다 — 리더 지시(기존 태그 관례).
            Assert.AreEqual("[레이어드해소]", LayeredHybridTimeline.LogPrefix);
        }

        [Test]
        public void 상한에_닿으면_한줄로_알리고_그_뒤로는_세기만_한다()
        {
            var t = new LayeredHybridTimeline();
            int cap = LayeredHybridTimeline.MaxLogLines;
            Assert.AreEqual(LayeredHybridPolicy.DefaultMaxStrips * 2, cap, "상한은 제거 상한에서 유도된다(숫자 베끼기 금지).");

            for (int i = 0; i < cap; i++)
            {
                Assert.IsNotNull(t.NoteStripped(i + 1, i, i * 0.25f), $"상한 전 {i + 1}번째 줄은 남아야 한다.");
            }
            Assert.AreEqual(cap, t.LineCount);

            string notice = t.NoteStripped(cap + 1, cap, cap * 0.25f);
            Assert.IsNotNull(notice);
            StringAssert.Contains("상한", notice);
            StringAssert.Contains($"{cap}줄", notice);
            Assert.IsNull(t.NoteStripped(cap + 2, cap + 1, (cap + 1) * 0.25f), "알림은 한 번뿐이다.");
            Assert.IsNull(t.ObserveLibraryClickThrough(true, cap + 2, (cap + 2) * 0.25f));
            Assert.AreEqual(cap + 3, t.EventCount, "사건은 상한 뒤에도 계속 센다.");
            Assert.AreEqual(3, t.SuppressedCount);
            Assert.AreEqual(cap, t.LineCount, "상한 알림 줄은 세지 않는다.");
        }

        [Test]
        public void 사건_이름은_전부_비어있지_않고_서로_다르다()
        {
            var names = new System.Collections.Generic.HashSet<string>();
            foreach (LayeredHybridTimelineEvent ev in System.Enum.GetValues(typeof(LayeredHybridTimelineEvent)))
            {
                string d = LayeredHybridTimeline.Describe(ev);
                Assert.IsFalse(string.IsNullOrWhiteSpace(d), ev.ToString());
                Assert.IsTrue(names.Add(d), $"{ev} 이름이 다른 사건과 같다: {d}");
            }
        }

        // ============================================================================
        // 배선 감사 — Windows 전용 파일은 이 머신에서 실행되지 않으므로 소스로 잠근다
        // ============================================================================

        private static string ReadScript(params string[] relative)
        {
            string path = Path.Combine(UnityEngine.Application.dataPath, "_Project", "Scripts");
            foreach (string r in relative) path = Path.Combine(path, r);
            Assert.IsTrue(File.Exists(path), $"소스가 없다: {path}");
            return File.ReadAllText(path);
        }

        [Test]
        public void 해소기와_Enforcer가_타임라인을_실제로_배선했다_니들_대조_포함()
        {
            string resolver = ReadScript("Platform", "Windows", "WindowsLayeredHybridResolver.cs");
            string enforcer = ReadScript("Platform", "Windows", "WindowsOverlayStateEnforcer.cs");
            string macEnforcer = ReadScript("Platform", "MacOS", "MacOverlayStateEnforcer.cs");

            // 존재 단언 — 니들이 실재하는지 파일 자체의 알려진 이름으로 먼저 확인(양성 대조).
            StringAssert.Contains("class WindowsLayeredHybridResolver", resolver);
            StringAssert.Contains("class WindowsOverlayStateEnforcer", enforcer);

            StringAssert.Contains("LayeredHybridTimeline", resolver, "해소기가 타임라인을 들고 있지 않다.");
            StringAssert.Contains(".ObserveLibraryClickThrough(", resolver, "라이브러리 캐시 엣지가 배선되지 않았다.");
            StringAssert.Contains(".ObserveOsLayered(", resolver, "OS 실측 엣지가 배선되지 않았다.");
            StringAssert.Contains(".NoteStripped(", resolver, "제거 사건이 배선되지 않았다.");
            StringAssert.Contains(".NoteRestored(", resolver, "복구 사건이 배선되지 않았다.");
            StringAssert.Contains("bool libraryClickThrough)", resolver, "Tick이 isClickThrough 캐시를 받지 않는다.");
            StringAssert.Contains("_layeredHybridResolver.Tick(Time.unscaledDeltaTime, (int)_controller.transparentType, _controller.isClickThrough)",
                enforcer, "Enforcer가 isClickThrough 캐시를 넘기지 않는다.");

            // 부재 대조 — 같은 니들이 macOS Enforcer에는 없어야 한다(니들이 실제로 가려낸다는 증거).
            //   이 축 자체가 Windows에만 있다(LayeredHybridPolicy 문서: macOS ignoresMouseEvents는 합성 경로를 안 건드린다).
            Assert.IsFalse(macEnforcer.Contains(".ObserveLibraryClickThrough("),
                "니들이 아무 파일에나 걸린다면 위 존재 단언은 아무것도 증명하지 않는다.");
        }

        [Test]
        public void 타임라인_파일은_자기창_스타일_쓰기_API_이름을_담지_않는다()
        {
            // LayeredHybridPolicyTests.자기창_스타일_쓰기_API는_해소기_한_파일에만_있다 감사가 전 소스를 훑는다.
            // 이 플랫폼 중립 파일이 그 이름을 주석에라도 담으면 그 감사가 거짓 빨강이 된다 — 여기서 먼저 잡는다.
            string timeline = ReadScript("Platform", "LayeredHybridTimeline.cs");
            StringAssert.Contains("class LayeredHybridTimeline", timeline);
            Assert.IsFalse(timeline.Contains("SetWindowLong"), "스타일 쓰기 API 이름은 해소기 한 파일에만 있어야 한다.");
        }
    }
}
