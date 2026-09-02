using System;
using System.IO;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// 사용자 2차 신고 <b>"윈도우 버전인데 여전히 사용할수록 렉생김"</b>(2026-09-02)의 근본 원인을
    /// 잠그는 테스트.
    ///
    /// ============================================================================
    /// 신고와 증거 (사용자 Windows 실기 로그)
    /// ============================================================================
    /// <code>
    /// 재적용 1/5 ... windowSize=(2560.00, 1600.00)
    /// 재적용 2/5 ... windowSize=(2559.00, 1600.00)
    /// 재적용 3/5 ... windowSize=(2558.00, 1600.00)
    /// 재적용 4/5 ... windowSize=(2557.00, 1600.00)
    /// 재적용 5/5 ... windowSize=(2556.00, 1600.00)
    /// [Win32WindowService] SetClickThrough(True) 적용 완료 ...
    /// 재적용 1/5 ... windowSize=(2555.00, 1600.00)      ← 새 라운드가 무장되어 또 5px
    /// [프레임스파이크] 268ms 멈춤 — 백버퍼: 2560x1600 -> 2550x1600 (스왑체인 재생성 유력)
    /// </code>
    /// <b>재적용 루프는 크기를 한 줄도 대입하지 않는다.</b> 그런데도 폭만(높이는 1600 고정) 줄었다.
    ///
    /// ============================================================================
    /// 확정된 인과 (추측 아님 — 패키지 C++ 원본 실측)
    /// ============================================================================
    /// <c>_controller.isTransparent = true</c> 한 줄이
    /// <c>UniWinCore.EnableTransparent</c>를 거쳐 <c>LibUniWinC.SetBorderless(TRUE)</c>를 부르고,
    /// 그 함수가 <b>동등성 가드 없이</b> 매번 <c>SetWindowPos</c> 4회로 창 폭을 ±1 흔든다
    /// (libuniwinc.cpp:694~, <c>offset = -1</c>). 흔들기는 <b>폭에만</b> 걸리고, 다음 호출의 기준값을
    /// <c>GetWindowRect</c>로 다시 읽으므로 잔차가 누적된다 — 로그의 모양과 정확히 일치한다.
    /// 더 큰 피해는 폭 1px이 아니라 <b>클라이언트 영역 변경 4회 = 스왑체인 재생성 4회</b>다.
    ///
    /// ============================================================================
    /// 이 파일이 순수 규칙을 실행해 검증하는 이유
    /// ============================================================================
    /// <b>이 개발 머신에는 Windows가 없다.</b> 그래서 판정을 P/Invoke 없는
    /// <see cref="OverlayStateReapplyPolicy"/>로 뽑아내 여기서 <b>실행으로</b> 돌리고, 나머지
    /// (호출 배선)는 소스 스캔으로 잠근다 — <c>OverlayResizeRatchetTests</c>와 같은 관례다.
    /// <b>실기에서 1px이 실제로 사라졌는지는 이 테스트가 대답하지 못한다</b>(정직한 한계).
    /// </summary>
    public class OverlayTransparencyReapplyTests
    {
        // ────────────────────────────────────────────────────────────────────────
        // 실기 로그에서 그대로 가져온 관측값 (프로덕션 상수가 아니라 "사용자 화면의 사실")
        // ────────────────────────────────────────────────────────────────────────

        private const float ObservedInitialWidth = 2560f;
        private const float ObservedHeight = 1600f;

        private static string ScriptsDir => Path.Combine(Application.dataPath, "_Project", "Scripts");
        private static string WinEnforcerPath => Path.Combine(
            ScriptsDir, "Platform", "Windows", "WindowsOverlayStateEnforcer.cs");
        private static string MacEnforcerPath => Path.Combine(
            ScriptsDir, "Platform", "MacOS", "MacOverlayStateEnforcer.cs");
        private static string StyleProbePath => Path.Combine(
            ScriptsDir, "Platform", "Windows", "WindowsWindowStyleProbe.cs");

        /// <summary>
        /// 주석을 걷어낸 소스. <b>"이 코드가 지금 무엇을 하는가"를 물을 때만</b> 쓴다.
        ///
        /// <para>이 라운드에서 반드시 필요하다: Windows Enforcer의 주석 블록이 결함을 설명하려고
        /// <c>_controller.isTransparent = DesiredTransparent;</c>를 <b>글자 그대로 인용</b>한다.
        /// 걷어내지 않으면 "가드 없는 대입이 남아 있다"는 거짓 실패가 난다 —
        /// <c>OverlayResizeRatchetTests.StripComments</c>가 남긴 교훈과 같은 함정이다.</para>
        /// </summary>
        private static string StripComments(string source)
        {
            var sb = new System.Text.StringBuilder(source.Length);
            foreach (string line in source.Split('\n'))
            {
                string t = line.TrimStart();
                if (t.StartsWith("//", StringComparison.Ordinal)) continue;
                if (t.StartsWith("*", StringComparison.Ordinal)) continue;
                if (t.StartsWith("/*", StringComparison.Ordinal)) continue;
                sb.Append(line).Append('\n');
            }
            return sb.ToString();
        }

        // ────────────────────────────────────────────────────────────────────────
        // 창 모형 — 네이티브 SetBorderless의 관측된 부작용만 재현한다
        // ────────────────────────────────────────────────────────────────────────

        private struct FakeWindow
        {
            public float Width;
            public float Height;
            public bool Borderless;
            public int ResizeEpisodes;      // SetBorderless가 실제로 실행된 횟수(1회 = SetWindowPos 4회)
            public int GlassReapplies;      // 유리(DWM)만 다시 건 횟수
        }

        /// <summary>수정 <b>전</b> 규칙: 판정 없이 매 회차 라이브러리 전체 경로.</summary>
        private static void ReapplyOldRule(ref FakeWindow w)
        {
            ApplyFullLibraryPath(ref w);
        }

        /// <summary>수정 <b>후</b> 규칙: OS 실측으로 정책이 고른 경로만 실행한다.</summary>
        private static void ReapplyNewRule(ref FakeWindow w, bool desiredTransparent,
            bool styleReadOk = true, bool glassAvailable = true)
        {
            TransparencyReapply decision = OverlayStateReapplyPolicy.DecideTransparencyReapply(
                desiredTransparent,
                styleReadOk,
                // 실측값은 "지금 창의 진짜 상태"에서 나온다 — 하드코딩하면 모형이 규칙을 검증하지 못한다.
                styleReadOk && w.Borderless,
                glassAvailable);

            if (OverlayStateReapplyPolicy.CausesWindowResize(decision)) ApplyFullLibraryPath(ref w);
            else w.GlassReapplies++;   // 유리만: 창 사각형을 건드리지 않는다.
        }

        /// <summary>
        /// <c>isTransparent</c> 대입 = <c>SetTransparent</c>(유리) + <c>SetBorderless</c>(폭 흔들기).
        /// 폭 손실은 프로덕션 상수를 참조한다 — 숫자를 베끼지 않는다(CLAUDE.md).
        /// </summary>
        private static void ApplyFullLibraryPath(ref FakeWindow w)
        {
            w.GlassReapplies++;
            w.Borderless = true;
            w.Width -= OverlayStateReapplyPolicy.BorderlessJiggleWidthLossPixels;
            w.ResizeEpisodes++;
        }

        private static FakeWindow SteadyState() => new FakeWindow
        {
            Width = ObservedInitialWidth,
            Height = ObservedHeight,
            Borderless = true,      // 기동 시 라이브러리가 이미 보더리스로 만든 뒤의 상태.
        };

        // ────────────────────────────────────────────────────────────────────────
        // 1. 래칫 재현 — 수정 전 규칙이 실기 로그를 그대로 만들어낸다
        // ────────────────────────────────────────────────────────────────────────

        [Test]
        public void 수정전_규칙은_재적용_한_라운드마다_실기_로그와_같은_폭을_잃는다()
        {
            int n = OverlayStateReapplyPolicy.ReapplyAttempts;
            var w = SteadyState();
            for (int i = 0; i < n; i++) ReapplyOldRule(ref w);

            float expected = ObservedInitialWidth - n * OverlayStateReapplyPolicy.BorderlessJiggleWidthLossPixels;
            Assert.AreEqual(expected, w.Width, 0.001f,
                $"수정 전 규칙이 실기 로그({ObservedInitialWidth} -> {expected})를 재현하지 못하면 " +
                "이 테스트가 잠그는 인과 자체가 틀린 것이다. 모형을 먼저 의심해라.");
            Assert.AreEqual(n, w.ResizeEpisodes,
                "재적용 1회 = SetBorderless 1회 = SetWindowPos 4회. 이 등식이 깨지면 모형이 잘못됐다.");
            Assert.AreEqual(ObservedHeight, w.Height, 0.001f,
                "흔들기는 폭에만 걸린다(newH 고정) — 실기에서 높이 1600이 불변인 것과 같아야 한다.");
        }

        [Test]
        public void 수정전_규칙은_UI_개폐로_재무장될_때마다_손실이_누적된다()
        {
            // 실기: 2560 -> (5회) -> 2555 -> SetClickThrough로 재무장 -> (5회) -> 2550
            int n = OverlayStateReapplyPolicy.ReapplyAttempts;
            var w = SteadyState();
            for (int round = 0; round < 2; round++)
                for (int i = 0; i < n; i++) ReapplyOldRule(ref w);

            float expected = ObservedInitialWidth - 2 * n * OverlayStateReapplyPolicy.BorderlessJiggleWidthLossPixels;
            Assert.AreEqual(expected, w.Width, 0.001f,
                "MarkDirty()가 라운드를 재무장할 때마다 손실이 누적되는 것이 '쓸수록 느려진다'의 형태다.");
        }

        // ────────────────────────────────────────────────────────────────────────
        // 2. 래칫 차단 — 이 라운드의 본론
        // ────────────────────────────────────────────────────────────────────────

        [Test]
        public void 정상_상태에서_N회_재적용_후_창_폭이_처음과_같다()
        {
            int n = OverlayStateReapplyPolicy.ReapplyAttempts;
            var w = SteadyState();
            for (int i = 0; i < n; i++) ReapplyNewRule(ref w, desiredTransparent: true);

            Assert.AreEqual(ObservedInitialWidth, w.Width, 0.001f,
                $"{n}회 재적용 후 창 폭이 처음과 달라졌다 — 래칫이 되살아났다.");
            Assert.AreEqual(0, w.ResizeEpisodes,
                "정상 상태(이미 보더리스)에서는 SetBorderless가 한 번도 실행되면 안 된다. " +
                "1회당 클라이언트 영역 변경 4회 = 스왑체인 재생성 4회다.");
        }

        [Test]
        public void UI를_스무_번_열고_닫아도_창_폭이_처음과_같다()
        {
            // 재무장(MarkDirty)이 20번 일어나는 하루치 사용을 모형화한다.
            int n = OverlayStateReapplyPolicy.ReapplyAttempts;
            var w = SteadyState();
            for (int round = 0; round < 20; round++)
                for (int i = 0; i < n; i++) ReapplyNewRule(ref w, desiredTransparent: true);

            Assert.AreEqual(ObservedInitialWidth, w.Width, 0.001f,
                "재무장이 반복돼도 폭은 불변이어야 한다 — 이것이 '쓸수록 느려진다'를 끝내는 조건이다.");
            Assert.AreEqual(0, w.ResizeEpisodes, "재무장은 리사이즈를 만들면 안 된다.");
        }

        /// <summary>
        /// <b>무조건 재적용을 없앤 것이 아니다</b>를 잠근다. 리더 지시의 핵심 제약이자
        /// 이 저장소가 <c>isTopmost</c>에서 한 번 데인 함정이다 — "이미 목표값이니 생략"이
        /// 캐시 거짓말과 만나면 회색 불투명 전체화면 창이 된다.
        /// </summary>
        [Test]
        public void 유리는_리사이즈를_생략한_회차에도_반드시_다시_걸린다()
        {
            int n = OverlayStateReapplyPolicy.ReapplyAttempts;
            var w = SteadyState();
            for (int i = 0; i < n; i++) ReapplyNewRule(ref w, desiredTransparent: true);

            Assert.AreEqual(n, w.GlassReapplies,
                "리사이즈를 생략한 회차에도 유리(DWM 확장 프레임)는 매번 다시 걸려야 한다. " +
                "생략하면 '투명이 조용히 풀렸는데 캐시 때문에 못 고치는' 최악의 경우가 돌아온다.");

            // 규칙 자체에 "아무것도 하지 않는다"라는 선택지가 없어야 한다.
            foreach (TransparencyReapply d in Enum.GetValues(typeof(TransparencyReapply)))
            {
                Assert.IsNotNull(OverlayStateReapplyPolicy.Describe(d),
                    $"{d}에 대한 한국어 사유가 없다 — Player.log만 보고 판단할 수 없게 된다.");
            }
        }

        // ────────────────────────────────────────────────────────────────────────
        // 3. 안전 방향 — "모를 때는 거는 쪽"이 유지되는가
        // ────────────────────────────────────────────────────────────────────────

        [Test]
        public void 스타일_실측에_실패하면_전체_경로로_간다()
        {
            var w = SteadyState();
            ReapplyNewRule(ref w, desiredTransparent: true, styleReadOk: false);

            Assert.AreEqual(1, w.ResizeEpisodes,
                "실측을 못 하면 '모른다'이고, 모를 때는 거는 쪽이 안전하다. " +
                "여기서 생략하면 투명화 실패를 영원히 못 고친다.");
            Assert.AreEqual(TransparencyReapply.ReassignStyleUnreadable,
                OverlayStateReapplyPolicy.DecideTransparencyReapply(true, false, false, true));
        }

        [Test]
        public void 실측이_목표와_다르면_전체_경로로_가서_실제로_고친다()
        {
            // 기동 직후: 아직 테두리가 있는 상태.
            var w = new FakeWindow { Width = ObservedInitialWidth, Height = ObservedHeight, Borderless = false };

            ReapplyNewRule(ref w, desiredTransparent: true);
            Assert.IsTrue(w.Borderless, "실측이 목표와 다른데도 SetBorderless를 부르지 않았다 — 테두리가 남는다.");
            Assert.AreEqual(1, w.ResizeEpisodes, "진짜로 필요한 1회는 감수한다.");

            // 그리고 그 뒤로는 더 이상 부르지 않는다(1회로 수렴 — 래칫이 아니다).
            int before = w.ResizeEpisodes;
            for (int i = 0; i < OverlayStateReapplyPolicy.ReapplyAttempts; i++)
                ReapplyNewRule(ref w, desiredTransparent: true);
            Assert.AreEqual(before, w.ResizeEpisodes,
                "필요해서 한 번 부른 뒤에도 계속 부르면 그것이 곧 래칫이다.");
        }

        [Test]
        public void 유리_전용_경로를_못_쓰면_투명화를_포기하지_않고_전체_경로로_간다()
        {
            var w = SteadyState();
            ReapplyNewRule(ref w, desiredTransparent: true, glassAvailable: false);

            Assert.AreEqual(1, w.ResizeEpisodes,
                "유리 전용 경로가 없으면 1px 래칫을 감수하더라도 투명화를 걸어야 한다 — " +
                "회색 불투명 전체화면 창이 훨씬 나쁜 실패다.");
            Assert.AreEqual(TransparencyReapply.ReassignGlassPathUnavailable,
                OverlayStateReapplyPolicy.DecideTransparencyReapply(true, true, true, false));
        }

        // ────────────────────────────────────────────────────────────────────────
        // 4. 보더리스 판정 비트 — 네거티브 컨트롤 포함
        // ────────────────────────────────────────────────────────────────────────

        [Test]
        public void 프레임_비트가_하나라도_있으면_보더리스가_아니다()
        {
            const long WsVisible = 0x10000000L;
            const long WsPopup = unchecked((long)0x80000000L);
            const long WsBorder = 0x00800000L;
            const long WsDlgFrame = 0x00400000L;
            const long WsThickFrame = 0x00040000L;
            const long WsCaption = WsBorder | WsDlgFrame;

            // 네이티브 SetBorderless(TRUE)가 실제로 세우는 값.
            Assert.IsTrue(OverlayStateReapplyPolicy.IsBorderless(WsVisible | WsPopup),
                "WS_VISIBLE|WS_POPUP은 네이티브가 보더리스로 세우는 값 그대로다.");

            foreach (long frameBit in new[] { WsBorder, WsDlgFrame, WsThickFrame, WsCaption })
            {
                Assert.IsFalse(OverlayStateReapplyPolicy.IsBorderless(WsVisible | WsPopup | frameBit),
                    $"프레임 비트 0x{frameBit:X}가 있는데 보더리스로 판정했다 — 테두리가 생겨도 " +
                    "SetBorderless를 부르지 않게 되어 사용자 화면에 창틀이 남는다.");
            }

            // 판정이 항상 true를 돌려주는 고장(= 검사가 아무것도 안 하는 상태)을 배제한다.
            Assert.IsFalse(OverlayStateReapplyPolicy.IsBorderless(OverlayStateReapplyPolicy.WindowFrameStyleBits),
                "네거티브 컨트롤: 프레임 비트만 있는 값을 보더리스로 보면 판정이 죽은 것이다.");
        }

        // ────────────────────────────────────────────────────────────────────────
        // 5. 배선 — 규칙이 존재해도 Enforcer가 안 부르면 아무 의미가 없다
        // ────────────────────────────────────────────────────────────────────────

        [Test]
        public void Windows_Enforcer의_투명_재대입은_정책_가드_안에만_있다()
        {
            string[] lines = StripComments(File.ReadAllText(WinEnforcerPath)).Split('\n');
            int found = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains("_controller.isTransparent = DesiredTransparent;")) continue;
                found++;

                // 바로 위의 "빈 줄이 아닌" 줄이 정책 가드여야 한다.
                string guard = null;
                for (int j = i - 1; j >= 0 && j >= i - 4; j--)
                {
                    if (lines[j].Trim().Length == 0 || lines[j].Trim() == "{") continue;
                    guard = lines[j];
                    break;
                }
                Assert.IsNotNull(guard, "투명 재대입 앞에 아무 가드도 없다.");
                StringAssert.Contains("OverlayStateReapplyPolicy.CausesWindowResize", guard,
                    "isTransparent 재대입이 정책 가드 밖에 있다. 그 한 줄이 네이티브 SetBorderless를 " +
                    "부르고, 그것이 SetWindowPos 4회 + 폭 1px 손실이다(2026-09-02 신고의 직접 원인).");
            }
            Assert.AreEqual(1, found,
                "Windows Enforcer의 isTransparent 재대입은 정확히 한 곳(정책 가드 안)이어야 한다.");
        }

        [Test]
        public void Windows_Enforcer가_OS_실측과_정책을_실제로_부른다()
        {
            string src = StripComments(File.ReadAllText(WinEnforcerPath));
            foreach (string call in new[]
            {
                "WindowsWindowStyleProbe.TryReadStyle(",
                "OverlayStateReapplyPolicy.IsBorderless(",
                "OverlayStateReapplyPolicy.DecideTransparencyReapply(",
                "UniWinCNativeHandle.TrySetTransparent(",
            })
            {
                StringAssert.Contains(call, src,
                    $"WindowsOverlayStateEnforcer가 \"{call}\"을 부르지 않는다 — 판정 근거가 " +
                    "OS 실측이 아니라 다시 캐시로 돌아갔거나, 유리 재적용이 사라진 것이다.");
            }
        }

        /// <summary>
        /// 클릭 관통(<b>절대 원칙 2</b>)은 이 라운드에서 <b>한 글자도</b> 조여지면 안 된다.
        /// 이 저장소에는 전례가 있다 — <c>WS_EX_LAYERED</c>를 떼었더니 관통이 사라져 즉시 되돌렸다.
        /// 네이티브 <c>SetClickThrough</c>는 <c>SetWindowLong(GWL_EXSTYLE)</c>뿐이라 창 사각형을
        /// 건드리지 않으므로, 조일 이유도 없다.
        /// </summary>
        [Test]
        public void 클릭관통_재적용은_여전히_무조건이다()
        {
            string[] lines = StripComments(File.ReadAllText(WinEnforcerPath)).Split('\n');
            bool found = false;
            foreach (string line in lines)
            {
                if (!line.Contains("_controller.isClickThrough = DesiredClickThrough;")) continue;
                found = true;
                Assert.IsFalse(line.TrimStart().StartsWith("if ", StringComparison.Ordinal),
                    "클릭 관통 재적용에 조건이 붙었다 — 원칙 2를 조이는 변경은 이 라운드의 범위 밖이다.");
            }
            Assert.IsTrue(found, "클릭 관통 재적용이 통째로 사라졌다.");
        }

        // ────────────────────────────────────────────────────────────────────────
        // 6. 상수 단일화 — 테스트가 숫자를 베끼지 않게 만든 장치가 유지되는가
        // ────────────────────────────────────────────────────────────────────────

        [Test]
        public void 양_플랫폼_Enforcer가_재적용_횟수_상수를_각자_들고_있지_않다()
        {
            foreach (string path in new[] { WinEnforcerPath, MacEnforcerPath })
            {
                string src = StripComments(File.ReadAllText(path));
                StringAssert.Contains("OverlayStateReapplyPolicy.ReapplyAttempts", src,
                    $"{Path.GetFileName(path)}가 공용 상수를 참조하지 않는다.");
                StringAssert.DoesNotContain("ReapplyAttempts = 5", src,
                    $"{Path.GetFileName(path)}가 리터럴 5를 다시 들고 있다 — 두 플랫폼과 테스트에서 " +
                    "값이 갈라진다(CLAUDE.md: 테스트에 프로덕션 상수를 숫자로 베끼지 않는다).");
            }
        }

        /// <summary>
        /// 새 Windows 헬퍼는 <b>사실 조회만</b> 한다(CLAUDE.md: 플랫폼 전용 코드는 사실 조회,
        /// 정책은 플랫폼 중립). 겸사겸사 원칙 3(유저 자산 불변)도 여기서 잠근다.
        /// </summary>
        [Test]
        public void 새_Windows_스타일_프로브는_쓰기_계열_호출을_하지_않는다()
        {
            string src = StripComments(File.ReadAllText(StyleProbePath));
            foreach (string forbidden in new[]
            {
                "SetWindowPos", "MoveWindow", "SetWindowLong", "SetWindowPlacement", "ShowWindow",
            })
            {
                StringAssert.DoesNotContain(forbidden, src,
                    $"WindowsWindowStyleProbe에 쓰기 계열 호출({forbidden})이 들어왔다 — " +
                    "이 파일은 GetWindowLong 실측 전용이다.");
            }
            // 판정(보더리스 여부)이 플랫폼 전용 파일로 새어 들어오지 않았는가.
            StringAssert.DoesNotContain("IsBorderless", src,
                "보더리스 판정이 Platform/Windows/로 내려왔다. 정책은 플랫폼 중립 위치에 있어야 " +
                "다른 플랫폼이 물리적으로 호출할 수 있다(FullscreenSuspendPolicy 사고 재발 방지).");
        }
    }
}
