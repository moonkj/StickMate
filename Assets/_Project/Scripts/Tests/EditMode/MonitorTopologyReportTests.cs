using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ============================================================================
    /// Phase 0 계측 + 오프셋 (11,−45) 수렴 실패 — 2026-09-02
    /// ============================================================================
    /// 두 가지를 잠근다.
    ///
    /// <para><b>(1) 계측이 죽어 있지 않은가.</b> 이 개발 머신은 디스플레이 1대라 다중 모니터 매핑을
    /// 실기로 확인할 수 없다. 그래서 조립 함수를 <b>알려진 값으로 교정</b>하고(CLAUDE.md 공통 처방),
    /// 교정이 깨지면 그 뒤 숫자를 전부 폐기한다.</para>
    ///
    /// <para><b>(2) 수렴 실패가 재현되고 수정으로 사라지는가.</b> 실기 로그의 두 표본
    /// (2026-09-01 두 번째 모니터 <c>(3851,45 3831x2160)</c> / 2026-09-02 주 모니터
    /// <c>(11,45 3839x2160)</c>)에서 <b>모니터가 다른데 오프셋이 같다</b>. 그 값을 그대로 넣어
    /// <b>옛 규칙에서는 어긋난 채로 끝나고 새 규칙에서는 원점에 붙는다</b>는 것을 실행으로 보인다.
    /// 옛 규칙 쪽 단언이 이 파일의 <b>음성 대조</b>다 — 그것이 초록이면 시뮬레이션이 결함을 재현하지
    /// 못하는 것이므로 새 규칙의 초록도 무의미하다.</para>
    /// </summary>
    public class MonitorTopologyReportTests
    {
        // ------------------------------------------------------------------
        // 실기 표본 (Tasklist.md:17664 / 17803). 숫자를 여기 한 번만 적고 아래에서 재사용한다.
        // ------------------------------------------------------------------
        private static readonly Rect PrimaryOsRect = new Rect(0f, 0f, 3840f, 2160f);
        private static readonly Rect SecondOsRect = new Rect(3840f, 0f, 3840f, 2160f);

        /// <summary>네이티브 <c>SetBorderless</c>가 프레임→보더리스 전환에서 창을 옮기는 양
        /// (libuniwinc.cpp:736~ <c>newX = rcWin.left + bw; newY = rcWin.top + (dy - bh);</c>).
        /// Win32 좌표로 (+11,+45)이고, 라이브러리 좌표(하단 기준 상향 y)로는 (+11,−45)다.</summary>
        private static readonly Vector2 BorderlessShiftLibrary = new Vector2(11f, -45f);

        private const float Eps = OverlayBoundsFitPolicy.DefaultEpsilonPixels;

        // ==================================================================
        // 1. 교정 — 알려진 값에서 조립기가 옳은 답을 내는가
        // ==================================================================

        [Test]
        public void 교정_주모니터_한대에서_인덱스0은_주모니터다()
        {
            var os = new List<OsMonitorFact> { new OsMonitorFact(PrimaryOsRect, PrimaryOsRect, true, "P") };
            var lib = new List<Rect> { new Rect(0f, 0f, 3840f, 2160f) };

            Assert.AreEqual(true, MonitorTopologyReport.ClaimIndexZeroIsPrimary(lib, os),
                "디스플레이 1대 구성에서 0번이 주 모니터가 아니라고 나오면 매칭 자체가 깨진 것이다.");
        }

        [Test]
        public void 교정_왼쪽에_보조모니터가_있으면_인덱스0은_주모니터가_아니다()
        {
            // 네이티브 정렬은 left 오름차순이므로 왼쪽(-3840)이 0번이 된다.
            var leftOs = new Rect(-3840f, 0f, 3840f, 2160f);
            var os = new List<OsMonitorFact>
            {
                new OsMonitorFact(leftOs, leftOs, false, "L"),
                new OsMonitorFact(PrimaryOsRect, PrimaryOsRect, true, "P"),
            };
            var lib = new List<Rect> { new Rect(-3840f, 0f, 3840f, 2160f), new Rect(0f, 0f, 3840f, 2160f) };

            Assert.AreEqual(false, MonitorTopologyReport.ClaimIndexZeroIsPrimary(lib, os),
                "이것이 MacOverlayStateEnforcer.TryGetTargetMonitorRect의 `isPrimary = i == 0`이 " +
                "틀리는 정확한 구성이다. 여기서 true가 나오면 이 감사는 그 결함을 영영 못 본다.");
        }

        [Test]
        public void 교정_모르는_것은_모른다고_답한다()
        {
            Assert.IsNull(MonitorTopologyReport.ClaimIndexZeroIsPrimary(null, null),
                "OS 목록이 없는데 '아니오'로 단정하면 로그가 거짓말을 한다(모름과 아님은 다르다).");
            Assert.IsNull(MonitorTopologyReport.ClaimIndexZeroIsPrimary(
                new List<Rect> { PrimaryOsRect }, new List<OsMonitorFact>()));
        }

        [Test]
        public void 교정_y반전_항등식은_Windows에서만_잔차를_낸다()
        {
            // 주 모니터 하단 = 2160. 두 번째 모니터도 하단 2160 -> 라이브러리 y = 0.
            float residual = MonitorTopologyReport.YFlipResidual(
                new Rect(3840f, 0f, 3840f, 2160f), SecondOsRect, 2160f,
                LibraryMonitorYConvention.FlippedFromPrimaryBottom);
            Assert.AreEqual(0f, residual, 0.001f,
                "우리가 아는 항등식(libY = primaryBottom - osBottom)이 깨졌다면 이 계측의 y 해석 전체가 무효다.");

            // 일부러 어긋난 입력 — 잔차가 실제로 잡히는지(음성 대조).
            float broken = MonitorTopologyReport.YFlipResidual(
                new Rect(3840f, 45f, 3840f, 2160f), SecondOsRect, 2160f,
                LibraryMonitorYConvention.FlippedFromPrimaryBottom);
            Assert.AreEqual(45f, broken, 0.001f, "잔차 계산이 어긋남을 잡지 못하면 이 항목은 공허하다.");

            // macOS는 뒤집지 않으므로 이 항등식이 성립할 이유가 없다 -> NaN(0이 아니다).
            Assert.IsNaN(MonitorTopologyReport.YFlipResidual(
                new Rect(0f, 75f, 1512f, 874f), new Rect(0f, 0f, 1512f, 982f), 982f,
                LibraryMonitorYConvention.CocoaBottomLeft),
                "macOS에서 0을 돌려주면 '검증 통과'로 오독된다 — 해당 없음은 NaN으로 말한다.");
        }

        [Test]
        public void 교정_현재_모니터는_창중심_포함으로_고르고_폴백을_구분한다()
        {
            var lib = new List<Rect> { new Rect(0f, 0f, 3840f, 2160f), new Rect(3840f, 0f, 3840f, 2160f) };

            int inside = MonitorTopologyReport.ResolveCurrentMonitorIndex(
                lib, new Vector2(3840f + 1920f, 1080f), out bool contained);
            Assert.AreEqual(1, inside);
            Assert.IsTrue(contained);

            int outside = MonitorTopologyReport.ResolveCurrentMonitorIndex(
                lib, new Vector2(-9999f, -9999f), out bool contained2);
            Assert.AreEqual(0, outside, "어디에도 안 들어가면 원점 모니터로 떨어진다(네이티브와 같은 규칙).");
            Assert.IsFalse(contained2,
                "폴백을 '포함'으로 보고하면 로그를 읽는 사람이 '창이 그 모니터에 있다'고 오독한다.");
        }

        [Test]
        public void 계측_줄은_비어_있지_않고_모든_구획을_담는다()
        {
            var os = new List<OsMonitorFact>
            {
                new OsMonitorFact(PrimaryOsRect, new Rect(0f, 0f, 3840f, 2088f), true, "0xA"),
                new OsMonitorFact(SecondOsRect, SecondOsRect, false, "0xB"),
            };
            var lib = new List<Rect> { new Rect(0f, 0f, 3840f, 2160f), new Rect(3840f, 0f, 3840f, 2160f) };

            string line = MonitorTopologyReport.Compose(
                "Windows", LibraryMonitorYConvention.FlippedFromPrimaryBottom,
                lib, new Rect(11f, -45f, 3839f, 2160f), os, true,
                new List<Rect> { PrimaryOsRect }, new Rect(11f, 45f, 3839f, 2160f), true);

            Assert.IsNotEmpty(line);
            foreach (string needle in new[]
            {
                MonitorTopologyReport.LogTag, "라이브러리:", "OS:", "Unity(작업영역):", "우리 창:", "판정:",
                "★주", "y잔차",
            })
            {
                StringAssert.Contains(needle, line,
                    $"계측 줄에 '{needle}' 구획이 없습니다 — 이 줄 하나가 Phase 0의 전부이므로 " +
                    "구획이 빠지면 리더가 답을 못 얻습니다.");
            }
        }

        [Test]
        public void 계측_줄은_OS_열거_실패를_0으로_위장하지_않는다()
        {
            string line = MonitorTopologyReport.Compose(
                "Windows", LibraryMonitorYConvention.FlippedFromPrimaryBottom,
                new List<Rect> { PrimaryOsRect }, PrimaryOsRect,
                new List<OsMonitorFact>(), osEnumerationOk: false,
                unityDisplayRects: new List<Rect>(), overlayOsRect: default, overlayOsRectKnown: false);

            StringAssert.Contains("전수 열거 실패", line,
                "조회 실패를 '0개'로 찍으면 다음 사람이 '모니터가 없다'로 읽는다 — " +
                "이 저장소의 거짓 통과 4번(모든 '없음' 판정에 양성 대조)과 같은 형태다.");
            StringAssert.Contains("알 수 없음", line, "주 모니터를 모르면 모른다고 적어야 한다.");
        }

        // ==================================================================
        // 2. (11,−45) 수렴 — 옛 규칙은 실패하고 새 규칙은 수렴한다
        // ==================================================================

        /// <summary>
        /// 실기 순서를 그대로 재현하는 최소 시뮬레이터.
        ///
        /// <para>틱마다: 목표와 비교 → 필요하면 이동 → 되읽기 → 확정 판정. 그리고 <b>첫 틱이 끝난 뒤
        /// 한 번</b> 네이티브 <c>SetBorderless</c>가 창을 (+11,−45) 옮긴다(라이브러리 좌표).
        /// 실기에서 그 호출은 재적용 루프가 <c>Screen.SetResolution</c>의 지연 적용으로 되살아난
        /// 창 스타일을 보고 실행하는 것이며, <b>우리 확정보다 늦게</b> 일어난다.</para>
        /// </summary>
        private static Vector2 SimulateFit(Vector2 monitorOrigin, bool latchOnWriteTick,
            out int latchedAtTick, int ticks = 6)
        {
            Vector2 pos = monitorOrigin + BorderlessShiftLibrary;
            bool latched = false;
            latchedAtTick = -1;
            bool borderlessMovePending = true;

            for (int tick = 1; tick <= ticks; tick++)
            {
                if (!latched)
                {
                    bool needsMove = OverlayBoundsFitPolicy.ShouldMove(
                        pos.x, pos.y, monitorOrigin.x, monitorOrigin.y, Eps);
                    if (needsMove) pos = monitorOrigin;

                    bool within = OverlayBoundsFitPolicy.Within(
                        pos.x, pos.y, monitorOrigin.x, monitorOrigin.y, Eps);

                    bool ok = latchOnWriteTick
                        ? within                                                     // 옛 규칙
                        : OverlayBoundsFitPolicy.ShouldLatchFitApplied(within, needsMove);   // 새 규칙
                    if (ok)
                    {
                        latched = true;
                        latchedAtTick = tick;
                    }
                }

                // 네이티브가 창을 옮기는 순간. 확정 이후에 일어나면 되돌릴 주체가 없다.
                if (borderlessMovePending)
                {
                    borderlessMovePending = false;
                    pos += BorderlessShiftLibrary;
                }
            }
            return pos;
        }

        [Test]
        public void 음성대조_옛_규칙은_두_모니터_모두에서_오프셋을_남긴_채_끝난다()
        {
            foreach (Vector2 origin in new[] { Vector2.zero, new Vector2(3840f, 0f) })
            {
                Vector2 final = SimulateFit(origin, latchOnWriteTick: true, out int latchedAt);

                Assert.AreEqual(1, latchedAt,
                    "옛 규칙은 <b>쓰기와 같은 틱</b>에 확정한다 — 그것이 결함의 정의다.");
                Assert.AreEqual(origin + BorderlessShiftLibrary, final,
                    $"모니터 원점 {origin}에서 옛 규칙이 오프셋을 남기지 못하면 이 시뮬레이터가 " +
                    "실기 결함을 재현하지 못하는 것이고, 아래 새 규칙의 초록도 무의미하다(거짓 통과).");
            }
        }

        [Test]
        public void 새_규칙은_두_모니터_모두에서_모니터_원점에_수렴한다()
        {
            foreach (Vector2 origin in new[] { Vector2.zero, new Vector2(3840f, 0f) })
            {
                Vector2 final = SimulateFit(origin, latchOnWriteTick: false, out int latchedAt);

                Assert.AreEqual(origin, final,
                    $"모니터 원점 {origin}에 붙지 않았습니다. 실기 오프셋 (11,−45)는 " +
                    "네이티브 SetBorderless의 프레임→보더리스 재배치이며, 우리 확정이 그보다 " +
                    "먼저 서면 영구히 남습니다.");
                Assert.Greater(latchedAt, 1,
                    "확정은 쓰기가 0인 틱에서만 서야 한다(첫 틱은 반드시 쓰기 틱이다).");
                Assert.LessOrEqual(latchedAt, 6,
                    "6회 예산(0.5초 x 6 = 3초) 안에 확정되지 않으면 실기에서 루프가 먼저 끝난다.");
            }
        }

        [Test]
        public void 확정_지연은_불감대를_넓히지_않는다()
        {
            // 45px 어긋남은 <b>여전히 어긋남</b>으로 읽혀야 한다. 은폐 금지.
            Assert.IsTrue(OverlayBoundsFitPolicy.ShouldMove(11f, -45f, 0f, 0f, Eps),
                "이 판정이 false가 되는 순간(불감대를 키우면 그렇게 된다) 45px 어긋남이 은폐된다.");
            Assert.IsFalse(OverlayBoundsFitPolicy.ShouldLatchFitApplied(false, false),
                "기하가 안 맞는데 확정하면 안 된다.");
            Assert.IsFalse(OverlayBoundsFitPolicy.ShouldLatchFitApplied(true, true),
                "쓰기가 있던 틱의 측정으로 확정하면 안 된다(이번 라운드의 결함 그 자체).");
            Assert.IsTrue(OverlayBoundsFitPolicy.ShouldLatchFitApplied(true, false));
        }

        [Test]
        public void 불감대_상수는_이_라운드에서_커지지_않았다()
        {
            Assert.AreEqual(2f, OverlayBoundsFitPolicy.DefaultEpsilonPixels, 0.0001f,
                "리더 지시: eps를 키워서 '수렴한 척'하게 만드는 것은 금지다. 이 값을 올리려면 " +
                "실측 근거를 OverlayBoundsFitPolicy 문서에 함께 남기고 이 테스트를 같은 라운드에 갱신하라.");
        }

        // ==================================================================
        // 3. 배선 — 계측과 규칙이 양 플랫폼에 실제로 걸려 있는가(소스 스캔)
        // ==================================================================

        private static string PlatformRoot =>
            Path.Combine(Application.dataPath, "_Project", "Scripts", "Platform");

        [Test]
        public void 계측_호출이_양_플랫폼_Enforcer에_모두_있다()
        {
            foreach (string rel in new[]
            {
                Path.Combine("Windows", "WindowsOverlayStateEnforcer.cs"),
                Path.Combine("MacOS", "MacOverlayStateEnforcer.cs"),
            })
            {
                string path = Path.Combine(PlatformRoot, rel);
                Assert.IsTrue(File.Exists(path), path);
                string src = File.ReadAllText(path);
                StringAssert.Contains("MonitorTopologyReport.EmitOnce(", src,
                    $"{rel}에 Phase 0 계측 호출이 없습니다 — 한쪽만 계측하면 두 플랫폼 로그를 " +
                    "나란히 놓고 비교할 수 없습니다(이번 라운드의 목적 자체가 그 대조입니다).");
                StringAssert.Contains("OverlayBoundsFitPolicy.ShouldLatchFitApplied(", src,
                    $"{rel}이 확정 규칙을 부르지 않습니다 — 한쪽에만 고치면 그 결함이 그대로 남습니다.");
            }
        }
    }
}
