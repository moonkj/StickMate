using System.IO;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ============================================================================
    /// "topmost 밴드 <b>안</b>에서 우리가 아래로 내려간다" — 2026-09-01 3차 (debugger)
    /// ============================================================================
    /// 실기 로그(20260901d)가 <c>WS_EX_TOPMOST</c> 비트는 <b>정상</b>인데 순위만 뒤집힌 상태를 찍었다:
    /// <code>
    /// ★ 우리가 아래(우리 #19 &gt; 전경 #18) — 캐릭터가 가려집니다
    /// / 전경 창 0x7E1D02 "Explorer.EXE" (3840,2088 3840x72) exStyle=0x00000088(TOPMOST)
    /// / 우리 창 0x8292A "StickMate" (3851,45 3831x2160)      exStyle=0x00080028(TOPMOST)
    /// </code>
    /// 이 파일이 잠그는 것은 두 가지다:
    ///   (1) <b>분류</b> — 우리 위의 창이 "하단 예약 막대(작업표시줄)"인지 "그 밖의 창"인지.
    ///       이 구분을 잃으면 다음 로그도 "작업표시줄이 위에 있다"만 반복하고 원인은 또 안 갈린다.
    ///   (2) <b>절대 불변 원칙 2(비침해)</b> — 밴드 안에서 우리를 다시 올리는 규칙이
    ///       시작 메뉴/알림 센터/전체화면 게임 위로는 <b>절대</b> 올라가지 않는다.
    ///
    /// <para>테스트 좌표는 전부 <b>실기 로그의 실측값</b>이다. 임의의 숫자를 쓰면 회귀 테스트가
    /// 현실과 다른 것을 잠근다.</para>
    /// </summary>
    public class TopmostBandOcclusionTests
    {
        // 실기(사용자 Windows, 릴리즈 20260901d)의 보조 모니터 구성 — 전부 로그에서 그대로 옮겼다.
        private static BandRect Monitor => new BandRect(3840, 0, 7680, 2160);
        private const int WorkAreaBottom = 2088;                       // rcWork.Bottom
        private static BandRect Taskbar => new BandRect(3840, 2088, 7680, 2160);   // (3840,2088 3840x72)
        private static BandRect OurWindow => new BandRect(3851, 45, 7682, 2205);   // (3851,45 3831x2160)

        // ====================================================================
        // 1. 분류 — "작업표시줄인가, 진짜 가림인가"
        // ====================================================================

        [Test]
        public void 실기_작업표시줄_사각형은_하단예약막대로_분류된다()
        {
            Assert.AreEqual(BandOccluderKind.ReservedBottomBar,
                TopmostBandOcclusionPolicy.Classify(Taskbar, Monitor, WorkAreaBottom),
                "실기 로그의 전경 창 (3840,2088 3840x72)는 같은 로그의 " +
                "`[Win32WindowService] 작업표시줄 실측 — rect=(x:3840, y:2088, width:3840, height:72)`와 " +
                "정확히 같다. 이것을 '진짜 가림'으로 분류하면 상시 거짓 경보가 된다 — " +
                "캐릭터는 이 띠의 **윗면**에 서므로 가려지지 않는다(macOS Dock과 같은 상태).");
        }

        [Test]
        public void 작업표시줄은_우리_창과_실제로_겹친다()
        {
            Assert.IsTrue(TopmostBandOcclusionPolicy.Overlaps(Taskbar, OurWindow),
                "겹치지 않으면 밴드 스캔이 애초에 이 창을 후보로도 안 잡는다 — " +
                "이 전제가 깨지면 위 분류 테스트가 무의미해진다.");
        }

        [Test]
        public void 화면_하단에_걸친_일반_창은_막대가_아니다()
        {
            // 작업표시줄 위쪽까지 올라오는 창(예: 하단에 붙인 미디어 플레이어). 캐릭터를 실제로 가린다.
            var straddling = new BandRect(4000, 1900, 5000, 2160);
            Assert.AreEqual(BandOccluderKind.Other,
                TopmostBandOcclusionPolicy.Classify(straddling, Monitor, WorkAreaBottom),
                "막대 띠 **안에 완전히** 들어가지 않으면 막대가 아니다. 여기서 관대해지면 " +
                "화면 하단을 덮는 진짜 가림이 '작업표시줄이니 정상'으로 묻힌다.");
        }

        [Test]
        public void 전체화면_topmost_창은_막대가_아니다()
        {
            Assert.AreEqual(BandOccluderKind.Other,
                TopmostBandOcclusionPolicy.Classify(Monitor, Monitor, WorkAreaBottom),
                "모니터 전체를 덮는 topmost 창은 캐릭터를 통째로 가린다 — 최우선 경보 대상이다.");
        }

        [Test]
        public void 픽셀_1_2개_오차는_여전히_막대로_본다()
        {
            var shifted = new BandRect(3839, 2087, 7681, 2161);
            Assert.AreEqual(BandOccluderKind.ReservedBottomBar,
                TopmostBandOcclusionPolicy.Classify(shifted, Monitor, WorkAreaBottom),
                "배율(125%/150%)·테두리 때문에 작업표시줄 사각형이 rcWork 경계와 1~2px 어긋나는 것은 " +
                "정상이다. 여기서 엄격하면 고배율 사용자에게만 거짓 경보가 쏟아진다.");
        }

        [Test]
        public void 자동숨김이면_어떤_창도_막대로_분류되지_않는다()
        {
            // 자동 숨김이면 Windows가 작업 영역을 줄이지 않는다 -> rcWork.Bottom == rcMonitor.Bottom.
            Assert.AreEqual(BandOccluderKind.Other,
                TopmostBandOcclusionPolicy.Classify(Taskbar, Monitor, workAreaBottom: 2160),
                "예약 막대가 없는데 '막대라서 정상'이라고 넘기면, 자동 숨김에서 튀어나온 작업표시줄이 " +
                "캐릭터를 덮는 상황이 조용히 묻힌다.");
        }

        [Test]
        public void 다른_모니터의_막대는_우리_모니터의_막대가_아니다()
        {
            var otherMonitorBar = new BandRect(0, 2088, 3840, 2160);
            Assert.AreEqual(BandOccluderKind.Other,
                TopmostBandOcclusionPolicy.Classify(otherMonitorBar, Monitor, WorkAreaBottom),
                "멀티모니터에서 좌표를 섞으면 판정이 통째로 뒤집힌다(이 프로젝트가 이미 여러 번 겪은 유형).");
        }

        [Test]
        public void 빈_사각형은_아무것도_가리지_않는다()
        {
            Assert.AreEqual(BandOccluderKind.None,
                TopmostBandOcclusionPolicy.Classify(new BandRect(10, 10, 10, 10), Monitor, WorkAreaBottom));
            Assert.IsFalse(TopmostBandOcclusionPolicy.Overlaps(new BandRect(10, 10, 10, 10), OurWindow));
        }

        [Test]
        public void 맞닿기만_하는_것은_겹침이_아니다()
        {
            var above = new BandRect(3851, 0, 7682, 45);   // 우리 창 바로 위에서 딱 맞닿는다.
            Assert.IsFalse(TopmostBandOcclusionPolicy.Overlaps(above, OurWindow),
                "경계가 맞닿는 창까지 가림으로 세면 밴드 스캔이 상시 오보를 낸다.");
        }

        // ====================================================================
        // 2. ★ 절대 불변 원칙 2(비침해) — 밴드 내 재올림 규칙의 금지선
        //    이 규칙은 아직 **배선되지 않았다**(어떤 Win32 쓰기에도 연결돼 있지 않다).
        //    그래도 잠가 둔다 — 배선하는 사람이 조건을 지우고 켜는 것이 가장 위험한 경로다.
        // ====================================================================

        [Test]
        public void 원칙2_전체화면_게임_숨김중에는_절대_올리지_않는다()
        {
            Assert.IsFalse(TopmostBandOcclusionPolicy.ShouldRaiseWithinBand(
                desiredTopmost: true, suspended: true,
                barOccludesUs: true, characterInsideBar: true, otherOccluderCount: 0),
                "게임 위로 기어 올라가는 것은 원칙 2 위반이고, 독점 전체화면 앱과 z-order를 다투면 " +
                "그쪽 화면만 깜빡이는 실해가 남는다.");
        }

        [Test]
        public void 원칙2_시작메뉴_알림센터가_떠_있으면_절대_올리지_않는다()
        {
            Assert.IsFalse(TopmostBandOcclusionPolicy.ShouldRaiseWithinBand(
                desiredTopmost: true, suspended: false,
                barOccludesUs: true, characterInsideBar: true, otherOccluderCount: 1),
                "막대가 아닌 topmost 창이 하나라도 우리를 덮고 있으면 그것은 시작 메뉴/알림 센터/" +
                "트레이 플라이아웃일 수 있다. 그 위에 졸라맨을 그리는 것은 명백한 업무 방해다 — " +
                "'시작 메뉴가 열려 있지 않을 때만'을 이 한 숫자로 보수적으로 만족시킨다.");
        }

        [Test]
        public void 캐릭터가_막대_안에_없으면_올릴_이유가_없다()
        {
            Assert.IsFalse(TopmostBandOcclusionPolicy.ShouldRaiseWithinBand(
                desiredTopmost: true, suspended: false,
                barOccludesUs: true, characterInsideBar: false, otherOccluderCount: 0),
                "캐릭터가 막대 **윗면**에 서 있으면 가려지는 픽셀이 없다. 그런데도 올리면 " +
                "얻는 것 없이 작업표시줄만 영구히 덮는다 — macOS에서 Dock 창(layer 20)이 우리(layer 3) " +
                "위에 상시 있는데도 아무 문제가 없었던 것이 그 증거다.");
        }

        [Test]
        public void 항상위를_원하지_않으면_올리지_않는다()
        {
            Assert.IsFalse(TopmostBandOcclusionPolicy.ShouldRaiseWithinBand(
                desiredTopmost: false, suspended: false,
                barOccludesUs: true, characterInsideBar: true, otherOccluderCount: 0));
        }

        [Test]
        public void 네_조건이_전부_맞을_때만_올린다()
        {
            Assert.IsTrue(TopmostBandOcclusionPolicy.ShouldRaiseWithinBand(
                desiredTopmost: true, suspended: false,
                barOccludesUs: true, characterInsideBar: true, otherOccluderCount: 0),
                "이 하나가 유일하게 허용되는 경우다 — 캐릭터가 실제로 막대에 파묻혀 보이지 않고, " +
                "그때 셸 팝업도 없고, 숨김 중도 아닐 때.");
        }

        // ====================================================================
        // 3. 전이 추적 — 24시간 상주 앱이라 "바뀔 때만" 로그해야 한다
        // ====================================================================

        [Test]
        public void 첫_관측은_기준선만_잡는다()
        {
            var t = new BandOcclusionTracker();
            Assert.AreEqual(BandOcclusionEvent.None,
                t.Observe(BandOccluderKind.ReservedBottomBar, 1.0, out _),
                "기동 시점에 이미 가려져 있는 것은 흔하다 — 그걸 사건으로 찍으면 매 실행마다 경보가 뜬다.");
        }

        [Test]
        public void 가림이_시작되면_Started다()
        {
            var t = new BandOcclusionTracker();
            t.Observe(BandOccluderKind.None, 1.0, out _);
            Assert.AreEqual(BandOcclusionEvent.Started,
                t.Observe(BandOccluderKind.ReservedBottomBar, 1.1, out _));
            Assert.AreEqual(1, t.EpisodeCount);
        }

        [Test]
        public void 상태가_그대로면_반복_로그하지_않는다()
        {
            var t = new BandOcclusionTracker();
            t.Observe(BandOccluderKind.None, 1.0, out _);
            t.Observe(BandOccluderKind.ReservedBottomBar, 1.1, out _);
            for (int i = 0; i < 200; i++)
            {
                Assert.AreEqual(BandOcclusionEvent.None,
                    t.Observe(BandOccluderKind.ReservedBottomBar, 1.2 + i * 0.1, out _),
                    "작업표시줄은 한 번 위로 올라가면 그 세션 내내 위에 있다(우리 창은 클릭 관통이라 " +
                    "활성화될 수 없어 다시 못 올라간다). 매 스캔 로그하면 Player.log가 죽는다.");
            }
        }

        [Test]
        public void 덮는_창의_정체가_바뀌면_KindChanged다()
        {
            var t = new BandOcclusionTracker();
            t.Observe(BandOccluderKind.None, 1.0, out _);
            t.Observe(BandOccluderKind.ReservedBottomBar, 1.1, out _);
            Assert.AreEqual(BandOcclusionEvent.KindChanged,
                t.Observe(BandOccluderKind.Other, 1.2, out _),
                "'작업표시줄만 위에 있다'와 '다른 창도 우리를 덮는다'는 심각도가 완전히 다르다.");
        }

        [Test]
        public void 가림이_풀리면_지속시간을_보고한다()
        {
            var t = new BandOcclusionTracker();
            t.Observe(BandOccluderKind.None, 1.0, out _);
            t.Observe(BandOccluderKind.Other, 2.0, out _);
            var evt = t.Observe(BandOccluderKind.None, 5.5, out double occludedFor);
            Assert.AreEqual(BandOcclusionEvent.Cleared, evt);
            Assert.AreEqual(3.5, occludedFor, 1e-6,
                "'잠깐 덮였다'와 '몇 분째 덮여 있다'를 로그 한 줄로 갈라야 한다.");
        }

        [Test]
        public void KindChanged가_지속시간_시계를_되감지_않는다()
        {
            // ★ 이 테스트는 실행 검증(스탠드얼론 하니스)에서 해석이 갈려 추가됐다.
            //   "얼마나 오래 덮여 있었나"가 알고 싶은 값이지 "마지막으로 덮개가 바뀐 뒤"가 아니다.
            var t = new BandOcclusionTracker();
            t.Observe(BandOccluderKind.None, 1.0, out _);
            t.Observe(BandOccluderKind.ReservedBottomBar, 1.1, out _);   // 에피소드 시작
            t.Observe(BandOccluderKind.Other, 30.0, out _);              // 덮개만 바뀜
            t.Observe(BandOccluderKind.None, 33.5, out double occludedFor);

            Assert.AreEqual(32.4, occludedFor, 1e-6,
                "에피소드 전체(1.1초 -> 33.5초)를 보고해야 한다. 마지막 KindChanged(30.0)부터 재면 " +
                "'몇 분째 덮여 있다'가 '방금 덮였다'로 둔갑해 심각도 판단이 뒤집힌다.");
        }

        // ====================================================================
        // 4. 직전 이벤트 링 — 리더 지시 "아래로 내려간 순간의 직전 이벤트를 남겨라"
        // ====================================================================

        [Test]
        public void 같은_사건이_연속되면_링을_채우지_않는다()
        {
            var ring = new WatchTraceRing();
            for (int i = 0; i < 50; i++) ring.Record(WatchTraceKind.BandScanClear, 0, i * 0.1);
            Assert.AreEqual(1, ring.Count,
                "폴링이 초당 10회다. 중복을 안 접으면 링이 같은 항목으로 가득 차 맥락이 통째로 사라진다 — " +
                "그러면 '직전 이벤트'가 아무것도 알려주지 못한다.");
        }

        [Test]
        public void 링은_최신_Capacity개만_남긴다()
        {
            var ring = new WatchTraceRing();
            for (int i = 0; i < WatchTraceRing.Capacity + 5; i++)
                ring.Record(WatchTraceKind.ForegroundChanged, 0x100 + i, i);
            Assert.AreEqual(WatchTraceRing.Capacity, ring.Count);

            string described = ring.Describe(100.0);
            StringAssert.Contains($"0x{0x100 + WatchTraceRing.Capacity + 4:X}", described,
                "가장 최근 항목이 없으면 '직전 이벤트'라는 이름이 거짓말이 된다.");
            StringAssert.DoesNotContain($"0x{0x100:X})", described,
                "가장 오래된 항목은 밀려나야 한다.");
        }

        [Test]
        public void 링은_최신순으로_경과시간과_함께_보고한다()
        {
            var ring = new WatchTraceRing();
            ring.Record(WatchTraceKind.SuspendedOff, 0, 10.0);
            ring.Record(WatchTraceKind.ForegroundChanged, 0x7E1D02, 12.0);

            string described = ring.Describe(12.12);
            StringAssert.StartsWith("120ms전 전경창바뀜(0x7E1D02)", described,
                "실기 로그에서 '작업표시줄이 전경이 된 직후 우리가 밑으로 갔다'를 인과로 확정하려면 " +
                "이 순서와 경과 시간이 반드시 있어야 한다.");
            StringAssert.Contains("전체화면숨김OFF", described);
        }

        [Test]
        public void 빈_링도_안전하게_보고한다()
        {
            Assert.AreEqual("(직전 이벤트 없음)", new WatchTraceRing().Describe(1.0));
        }

        // ====================================================================
        // 5. Windows 구현 구조 잠금(소스 정적 스캔)
        //    — #if UNITY_STANDALONE_WIN 안이라 이 macOS EditMode에서는 타입이 없다.
        // ====================================================================

        private static string ReadWatchdog() => File.ReadAllText(Path.Combine(
            Application.dataPath, "_Project", "Scripts", "Platform", "Windows", "WindowsTopmostWatchdog.cs"));

        /// <summary>
        /// 주석을 뺀 <b>코드만</b>. 금지 API 스캔은 반드시 이쪽을 써야 한다 —
        /// 이 파일들은 "왜 그것을 쓰지 않는가"를 주석에 길게 적어 두므로, 원문 전체를 스캔하면
        /// <b>금지 이유를 설명한 문장 자체가 테스트를 실패시킨다</b>(이 라운드에서 실제로 발생).
        /// </summary>
        private static string CodeOnly(string src)
        {
            var sb = new System.Text.StringBuilder(src.Length);
            foreach (string line in src.Split('\n'))
            {
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("//", System.StringComparison.Ordinal)) continue;
                sb.Append(line).Append('\n');
            }
            return sb.ToString();
        }

        [Test]
        public void 밴드_가림_스캔이_배선되어_있다()
        {
            string src = ReadWatchdog();
            StringAssert.Contains("ScanBandOcclusion", src,
                "이게 없으면 WS_EX_TOPMOST 비트만 보던 예전 상태로 돌아간다 — 실기에서 관측된 " +
                "'비트는 True인데 순위만 뒤집힘'을 사건으로 취급조차 못 한다.");
            StringAssert.Contains("TopmostBandOcclusionPolicy.Classify", src,
                "작업표시줄과 진짜 가림을 구분하지 않으면 다음 실기 로그도 원인을 못 가른다.");
        }

        [Test]
        public void 가림_시작_로그에_직전_이벤트가_포함된다()
        {
            string src = ReadWatchdog();
            StringAssert.Contains("_trace.Describe(", src,
                "리더 지시(2026-09-01 3차): '우리가 아래로 내려간 순간의 직전 이벤트'가 로그에 남아야 한다. " +
                "이 호출이 사라지면 다음 신고에서도 '이미 밀려 있다'는 사진만 남고 원인은 모른다.");
            StringAssert.Contains("직전 이벤트:", src, "사람이 로그에서 찾을 수 있는 문구가 있어야 한다.");
        }

        [Test]
        public void 원칙2_밴드_내_강제_올림은_아직_배선되지_않았다()
        {
            string code = CodeOnly(ReadWatchdog());
            StringAssert.DoesNotContain("HWND_TOPMOST", code,
                "밴드 안에서 위로 올라가는 유일한 수단은 SetWindowPos(HWND_TOPMOST)이고, 그것은 " +
                "작업표시줄/시작 메뉴 위에 **영구히** 올라앉는 행위라 절대 불변 원칙 2와 정면 충돌한다. " +
                "배선하려면 리더 승인 + TopmostBandOcclusionPolicy.ShouldRaiseWithinBand의 네 조건 " +
                "전부를 통과시켜야 하며, 이 테스트도 함께 갱신해야 한다.");
            StringAssert.DoesNotContain("SetWindowPos", code,
                "원칙 3 — 창을 바꾸는 API는 이 감시 파일에 선언조차 없다.");
            StringAssert.DoesNotContain("DwmSetWindowAttribute", code,
                "DWM 쪽도 조회(DwmGetWindowAttribute)만 쓴다 — 세터는 선언조차 금지.");
        }

        [Test]
        public void 밴드_스캔은_클로킹된_셸_창을_거른다()
        {
            string src = ReadWatchdog();
            StringAssert.Contains("DWMWA_CLOAKED", src,
                "Win11의 시작 메뉴/셸 호스트는 닫혀도 파괴되지 않고 '클로킹'된 채 topmost 밴드에 남는다. " +
                "이때 IsWindowVisible은 true라, 안 거르면 '시작 메뉴가 항상 우리를 덮는다'는 상시 오보가 " +
                "되고 otherOccluderCount가 영원히 0이 아니게 되어 판정 전체가 무의미해진다.");
        }

        [Test]
        public void 밴드_스캔_순회는_우리_자신에서_멈춘다()
        {
            string src = ReadWatchdog();
            StringAssert.Contains("if (h == overlay) { foundSelf = true; break; }", src,
                "우리 위의 창만 세면 되므로 순회는 우리 자신에서 끝나야 한다(실기 rank 19 -> 20회). " +
                "이 break가 사라지면 예전의 '열거 818개'가 그대로 돌아온다.");
        }
    }
}
