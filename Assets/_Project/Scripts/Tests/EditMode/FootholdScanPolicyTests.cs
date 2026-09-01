using NUnit.Framework;
using StickMate.Platform;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// <see cref="FootholdScanPolicy"/> 순수 규칙 검증.
    ///
    /// <para><b>이 규칙은 지금 제품 코드에 배선돼 있지 않다</b>(그 파일의 "상태" 절 참고 — 창 열거가
    /// 실행 시간의 0.5%로 실측돼 구조 변경이 중단됐다). 그럼에도 테스트를 남기는 이유는 두 가지다:</para>
    /// <list type="number">
    /// <item>규칙이 <b>실제로 실행돼 통과한 적이 있다</b>는 사실을 저장소가 보존해야, 다음에 이 방향을
    ///   다시 열 때 "설계만 있고 검증은 없는 문서"에서 시작하지 않는다.</item>
    /// <item>특히 <see cref="ResolveWorstCaseStalenessSeconds_안전망을_늘려도_유예_유도값이_부풀지_않는다"/>는
    ///   <b>미래의 실수를 막는 못</b>이다. 폴링을 없앨 때 가장 저지르기 쉬운 실수가 "이제 폴링 안 하니까
    ///   저빈도 안전망(2~5초)을 유예 유도식에 넣자"인데, 그러면 발판 상실 유예가 0.45초에서 3~7.5초로
    ///   부풀어 캐릭터가 허공에 몇 초씩 떠 있게 된다.</item>
    /// </list>
    ///
    /// <para>UnityEngine 의존이 없는 순수 규칙이라 이 테스트는 Windows 실기 없이 개발 머신에서 그대로
    /// 돈다(<c>OverlayResizeRatchetTests</c>와 같은 관례).</para>
    /// </summary>
    public class FootholdScanPolicyTests
    {
        private const string LogPrefix = "[발판스캔규칙]";

        /// <summary>배포 형상에 맞춘 기본 신호(폴링 0.3초 / 안전망 3초 / 이벤트 상한 10Hz).</summary>
        private static FootholdScanSignals Baseline(bool notifierActive)
        {
            return new FootholdScanSignals
            {
                HasBootstrapped = true,
                NotifierActive = notifierActive,
                SafetyNetIntervalSeconds = 3f,
                FallbackPollIntervalSeconds = 0.3f,
                MinEventScanIntervalSeconds = 0.1f,
                MinStateScanIntervalSeconds = 0.3f,
            };
        }

        // ================= 1. 폴백은 이 라운드 이전 거동과 정확히 같다 =================

        [Test]
        public void 캐시가_없으면_무조건_전체_열거한다()
        {
            var s = Baseline(true);
            s.HasBootstrapped = false;
            var d = FootholdScanPolicy.Decide(in s);
            Assert.IsTrue(d.ShouldEnumerate, $"{LogPrefix} 첫 스캔이 없으면 첫 프레임에 발판 정보가 없다.");
            Assert.AreEqual(FootholdScanTrigger.Bootstrap, d.Trigger);
        }

        [Test]
        public void 통보창구가_없으면_옛_폴링_주기를_그대로_쓴다()
        {
            var s = Baseline(false);
            s.SecondsSinceFullScan = 0.29f;
            Assert.IsFalse(FootholdScanPolicy.Decide(in s).ShouldEnumerate,
                $"{LogPrefix} 폴백은 주기 전에 열거하지 않는다.");

            s.SecondsSinceFullScan = 0.30f;
            var d = FootholdScanPolicy.Decide(in s);
            Assert.IsTrue(d.ShouldEnumerate);
            Assert.AreEqual(FootholdScanTrigger.FallbackPolling, d.Trigger,
                $"{LogPrefix} 폴백 경로는 반드시 폴링 사유로 잡혀야 원격 로그에서 구분된다.");
        }

        [Test]
        public void 통보창구가_죽으면_저빈도_안전망이_아니라_폴링_주기를_쓴다()
        {
            // ★ 이것이 폴백 설계의 핵심이다. 훅이 죽었는데 안전망(5초)으로 떨어지면 캐릭터가
            //   최대 5초 동안 낡은 발판 목록 위에 서 있게 된다 = 훅만 믿은 것과 같은 결과.
            var s = Baseline(false);
            s.SafetyNetIntervalSeconds = 5f;
            s.SecondsSinceFullScan = 0.31f;
            Assert.IsTrue(FootholdScanPolicy.Decide(in s).ShouldEnumerate,
                $"{LogPrefix} 안전망 5초가 폴백 주기 0.3초를 덮어쓰면 안 된다.");
        }

        // ================= 2. "언제" — 아무 일도 없으면 비용 0 =================

        [Test]
        public void 통보가_없고_캐릭터가_정지해_있으면_열거하지_않는다()
        {
            var s = Baseline(true);
            s.CharacterStill = true;
            s.CharacterGrounded = true;
            s.SecondsSinceFullScan = 2.9f;
            Assert.IsFalse(FootholdScanPolicy.Decide(in s).ShouldEnumerate,
                $"{LogPrefix} 이 라운드의 목표 상태 — 창이 안 움직이면 OS 호출 0회.");
        }

        [Test]
        public void 저빈도_안전망은_반드시_돈다()
        {
            var s = Baseline(true);
            s.CharacterStill = true;
            s.CharacterGrounded = true;
            s.SecondsSinceFullScan = 3.0f;
            var d = FootholdScanPolicy.Decide(in s);
            Assert.IsTrue(d.ShouldEnumerate, $"{LogPrefix} 훅이 이벤트를 흘렸을 때의 유일한 바닥이다.");
            Assert.AreEqual(FootholdScanTrigger.SafetyNet, d.Trigger);
        }

        // ================= 3. "무엇을" — 좁히기 =================

        [Test]
        public void 딛고_있는_창의_변화는_좁혀도_반드시_통과한다()
        {
            var s = Baseline(true);
            s.CharacterStill = true;
            s.CharacterGrounded = true;
            s.WatchedWindowEventPending = true;
            s.SecondsSinceFullScan = 0.5f;
            var d = FootholdScanPolicy.Decide(in s);
            Assert.IsTrue(d.ShouldEnumerate,
                $"{LogPrefix} 좁게 보다가 딛고 있는 창을 놓치는 것이 이 방향의 가장 큰 위험이다.");
            Assert.AreEqual(FootholdScanTrigger.WatchedWindowEvent, d.Trigger);
        }

        [Test]
        public void 정지_접지_상태에서는_감시_밖_창의_변화를_무시한다()
        {
            var s = Baseline(true);
            s.CharacterStill = true;
            s.CharacterGrounded = true;
            s.GlobalWindowEventPending = true;
            s.SecondsSinceFullScan = 0.5f;
            Assert.IsFalse(FootholdScanPolicy.Decide(in s).ShouldEnumerate,
                $"{LogPrefix} 사용자가 남의 창을 드래그해도 우리 비용이 0이어야 한다.");
        }

        [Test]
        public void 걷는_중에는_감시_밖_창의_변화도_열거로_이어진다()
        {
            var s = Baseline(true);
            s.CharacterStill = false;
            s.CharacterGrounded = true;
            s.GlobalWindowEventPending = true;
            s.SecondsSinceFullScan = 0.5f;
            var d = FootholdScanPolicy.Decide(in s);
            Assert.IsTrue(d.ShouldEnumerate,
                $"{LogPrefix} 걷는 중에는 곧 올라탈 수 있는 창이 어느 것인지 모른다.");
            Assert.AreEqual(FootholdScanTrigger.GlobalWindowEvent, d.Trigger);
        }

        [Test]
        public void 좁히기_조건은_넷_모두_성립할_때만_참이다()
        {
            Assert.IsFalse(FootholdScanPolicy.ShouldNarrowToWatchedWindows(false, true, true, false, false),
                $"{LogPrefix} 훅이 죽으면 좁힐 대상 자체가 없다.");
            Assert.IsFalse(FootholdScanPolicy.ShouldNarrowToWatchedWindows(true, false, true, false, false),
                $"{LogPrefix} 움직이는 동안에는 좁히지 않는다.");
            Assert.IsFalse(FootholdScanPolicy.ShouldNarrowToWatchedWindows(true, true, false, false, false),
                $"{LogPrefix} 접지가 없으면 '그 창만 보면 된다'가 성립하지 않는다.");
            Assert.IsFalse(FootholdScanPolicy.ShouldNarrowToWatchedWindows(true, true, true, true, false),
                $"{LogPrefix} 붙잡히면 어디로든 던져질 수 있다(사용자 지적).");
            Assert.IsFalse(FootholdScanPolicy.ShouldNarrowToWatchedWindows(true, true, true, false, true),
                $"{LogPrefix} 공중에서는 착지 후보를 알아야 한다.");
            Assert.IsTrue(FootholdScanPolicy.ShouldNarrowToWatchedWindows(true, true, true, false, false));
        }

        // ================= 4. 넓게 봐야 하는 순간들 =================

        [Test]
        public void 붙잡힘_공중_반경이탈은_정지_중이어도_넓게_본다()
        {
            var grabbed = Baseline(true);
            grabbed.CharacterGrabbed = true;
            grabbed.CharacterStill = true;
            grabbed.CharacterGrounded = true;
            grabbed.SecondsSinceFullScan = 0.4f;
            Assert.AreEqual(FootholdScanTrigger.Grabbed, FootholdScanPolicy.Decide(in grabbed).Trigger);

            var airborne = Baseline(true);
            airborne.CharacterAirborne = true;
            airborne.SecondsSinceFullScan = 0.4f;
            Assert.AreEqual(FootholdScanTrigger.Airborne, FootholdScanPolicy.Decide(in airborne).Trigger);

            var exited = Baseline(true);
            exited.NeighborhoodExited = true;
            exited.SecondsSinceFullScan = 0.4f;
            Assert.AreEqual(FootholdScanTrigger.NeighborhoodExit, FootholdScanPolicy.Decide(in exited).Trigger);
        }

        // ================= 5. 이벤트 폭풍 상한 =================

        [Test]
        public void 이벤트_폭풍은_상한에_걸려_폴링보다_나빠지지_않는다()
        {
            // EVENT_OBJECT_LOCATIONCHANGE는 드래그 중 초당 수백 번 온다. 상한이 없으면 프레임당
            // 1회(=초당 60회)까지 올라가 폴링(3.3회)보다 훨씬 나빠진다.
            var s = Baseline(true);
            s.WatchedWindowEventPending = true;
            s.SecondsSinceFullScan = 0.05f;
            var d = FootholdScanPolicy.Decide(in s);
            Assert.IsFalse(d.ShouldEnumerate);
            Assert.AreEqual(FootholdScanTrigger.Throttled, d.Trigger);
        }

        [Test]
        public void 붙잡힘_공중도_프레임마다_열거하지_않는다()
        {
            var s = Baseline(true);
            s.CharacterGrabbed = true;
            s.SecondsSinceFullScan = 1f / 60f;
            Assert.AreEqual(FootholdScanTrigger.Throttled, FootholdScanPolicy.Decide(in s).Trigger,
                $"{LogPrefix} 던지기 한 번에 60회 열거하면 그 자체가 새 스파이크가 된다.");
        }

        // ================= 6. 안전 최우선 — 발판 상실 확인 사살 =================

        [Test]
        public void 발판_상실_확인_사살은_어떤_상한보다도_우선한다()
        {
            var s = Baseline(true);
            s.GroundLossConfirmRequested = true;
            s.CharacterStill = true;
            s.CharacterGrounded = true;
            s.SecondsSinceFullScan = 0.001f; // 상한에 확실히 걸리는 시점
            var d = FootholdScanPolicy.Decide(in s);
            Assert.IsTrue(d.ShouldEnumerate,
                $"{LogPrefix} 이 경로가 늦으면 캐릭터가 멀쩡한 창 위에서 떨어진다 — 비용보다 우선한다.");
            Assert.AreEqual(FootholdScanTrigger.GroundLossConfirm, d.Trigger);
        }

        // ================= 7. 유도값 =================

        [Test]
        public void 근처_반경은_보행속도와_갱신주기에서_유도된다()
        {
            // 배포 형상: 보행 2.5유닛/초 x 약 40.9pt/유닛 ≈ 102pt/초, 안전망 3초, 여유 1.5배.
            float r = FootholdScanPolicy.ResolveNeighborhoodRadiusOsPx(102f, 3f, 1.5f);
            Assert.AreEqual(459f, r, 0.5f, $"{LogPrefix} 반경 = 속도 x 지평선 x 여유.");

            Assert.AreEqual(FootholdScanPolicy.MinNeighborhoodRadiusOsPx,
                FootholdScanPolicy.ResolveNeighborhoodRadiusOsPx(0f, 3f, 1.5f),
                $"{LogPrefix} 속도가 0이어도 감시 상자가 캐릭터 몸보다 작아지면 안 된다.");
            Assert.AreEqual(FootholdScanPolicy.MinNeighborhoodRadiusOsPx,
                FootholdScanPolicy.ResolveNeighborhoodRadiusOsPx(float.NaN, 3f, 1.5f),
                $"{LogPrefix} 측정이 깨져도(NaN) 하한으로 안전하게 떨어져야 한다.");
        }

        [Test]
        public void ResolveWorstCaseStalenessSeconds_안전망을_늘려도_유예_유도값이_부풀지_않는다()
        {
            // ★ 이 테스트가 이 파일에서 가장 중요하다 — 위 클래스 문서의 (2)번 이유.
            float withNet2 = FootholdScanPolicy.ResolveWorstCaseStalenessSeconds(true, 0.3f, 2f, 0.3f);
            float withNet5 = FootholdScanPolicy.ResolveWorstCaseStalenessSeconds(true, 0.3f, 5f, 0.3f);
            float hookDown = FootholdScanPolicy.ResolveWorstCaseStalenessSeconds(false, 0.3f, 5f, 0.3f);

            Assert.AreEqual(0.3f, withNet2, 1e-5f);
            Assert.AreEqual(0.3f, withNet5, 1e-5f,
                $"{LogPrefix} 저빈도 안전망은 '딛고 있는 발판'의 지연이 아니다 — 그쪽은 확인 사살이 담당한다.");
            Assert.AreEqual(0.3f, hookDown, 1e-5f,
                $"{LogPrefix} 훅이 죽으면 폴백이 옛 주기를 쓰므로 같은 값이다.");

            // 배포 배수 1.5를 곱한 결과가 전환 전 유예(0.45초)와 같아야 한다.
            Assert.AreEqual(0.45f, withNet5 * 1.5f, 1e-4f,
                $"{LogPrefix} 이 방향은 비용을 줄이는 것이지 유예를 줄이는 것이 아니다.");
        }

        // ================= 8. 60초 시뮬레이션 — 효과의 크기 =================

        [Test]
        public void 창이_움직이지_않는_60초에서_열거_횟수가_한자릿수_배로_줄어든다()
        {
            int polling = SimulateSixtySeconds(notifierActive: false, globalEventsPerSecond: 0, still: true);
            int evented = SimulateSixtySeconds(notifierActive: true, globalEventsPerSecond: 0, still: true);
            int stormed = SimulateSixtySeconds(notifierActive: true, globalEventsPerSecond: 2, still: true);

            UnityEngine.Debug.Log($"{LogPrefix} 60초 시뮬레이션 — 폴링 {polling}회 / 이벤트 {evented}회 / " +
                $"남의 창을 초당 2회 드래그해도 {stormed}회. " +
                "(참고: 실기 계측상 열거 1회는 약 1.8ms이고 전체의 0.5%다 — 이 절감은 '옳지만 작다'.)");

            Assert.AreEqual(199, polling, 1, $"{LogPrefix} 기준선은 60/0.3 = 200회 근처여야 한다.");
            Assert.LessOrEqual(evented, polling / 5,
                $"{LogPrefix} 안전망만 도는 상태여야 한다(3초 주기 = 약 20회).");
            Assert.AreEqual(evented, stormed,
                $"{LogPrefix} 좁히기가 동작하면 감시 밖 드래그는 열거 횟수를 한 번도 늘리지 않는다.");
        }

        private static int SimulateSixtySeconds(bool notifierActive, int globalEventsPerSecond, bool still)
        {
            const float dt = 1f / 60f;
            float sinceScan = 0f;
            int scans = 0;
            int everyNFrames = globalEventsPerSecond > 0 ? 60 / globalEventsPerSecond : 0;

            for (int frame = 0; frame < 3600; frame++)
            {
                sinceScan += dt;
                var s = Baseline(notifierActive);
                s.CharacterStill = still;
                s.CharacterGrounded = true;
                s.SecondsSinceFullScan = sinceScan;
                s.GlobalWindowEventPending = everyNFrames > 0 && frame % everyNFrames == 0;

                if (!FootholdScanPolicy.Decide(in s).ShouldEnumerate) continue;
                scans++;
                sinceScan = 0f;
            }
            return scans;
        }
    }
}
