using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using StickMate.Platform;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// <b>세션 가시성 게이트</b>(2026-09-02) — 잠금 화면 / UAC 보안 데스크톱 / 화면 꺼짐 동안
    /// 발판 재열거를 세우는 규칙과 그 배선의 회귀 잠금.
    ///
    /// ============================================================================
    /// 이 파일이 지키려는 것 — <b>두 다리</b>와 <b>두 방향</b>
    /// ============================================================================
    /// <list type="number">
    /// <item><b>OR의 두 항이 각각 하중을 받는다.</b> macOS는 <c>DisplayAsleep</c>으로,
    ///   Windows는 <c>SessionLocked</c>로 선다. 한쪽만 참인 스냅샷 각각에 대해 게이트가 서는지를
    ///   따로 못 박는다 — 그러지 않으면 "한쪽은 항상 false네, 정리하자"는 리팩터링이
    ///   <b>한 플랫폼에서만 기능을 통째로 지우고도 초록으로 통과</b>한다.</item>
    /// <item><b>서는 것만이 아니라 다시 도는 것도 잰다.</b> 멈추는 쪽만 검사하면 "영원히 멈춘"
    ///   구현이 만점을 받는다. 이 저장소가 고치려는 버그가 정확히 그것(발판 전멸)이다.</item>
    /// </list>
    ///
    /// <para><b>★ 기대값을 프로덕션 함수로 만들지 않는다</b>(TEAM.md 「생성기와 검사기가 같이
    /// 틀린다」). 아래 진리표 8행은 <see cref="SessionVisibilityPolicy"/>를 부르지 않고
    /// <b>손으로 적은 리터럴</b>이다. 규칙이 뒤집히면 표가 함께 뒤집히지 않으므로 반드시 빨개진다.</para>
    ///
    /// <para>OS 조회(<c>WTSQuerySessionInformation</c>, <c>CGDisplayIsAsleep</c>)는 플랫폼 구현체에
    /// 있고 이 머신에서는 한 줄도 실행되지 않는다 — 여기서는 관측값을 손으로 만들어 넣는다.
    /// <b>실기(Windows) 확인은 별개이며 아직 없다.</b></para>
    /// </summary>
    public sealed class SessionVisibilityPolicyTests
    {
        [TearDown]
        public void TearDown()
        {
            // 정적 창구라 다음 테스트로 샌다. 반드시 되돌린다.
            FootholdPoller.PresenceProbeOverride = null;
            PlayerLogPolicy.ResetForTests();
        }

        private static ViewerPresenceSnapshot Snapshot(bool asleep, bool locked)
            => new ViewerPresenceSnapshot(asleep, secondsSinceUserInput: 0f,
                lowPowerMode: false, onBattery: false, sessionLocked: locked);

        // ====================================================================
        // 1. 진리표 — 기대값은 손으로 적은 리터럴이다
        // ====================================================================

        /// <summary>
        /// <c>Valid</c> x <c>DisplayAsleep</c> x <c>SessionLocked</c> = 8칸 전수.
        /// <b>기대 열은 프로덕션을 부르지 않고 손으로 적었다.</b>
        /// </summary>
        [Test]
        public void 세션가시성_진리표_8칸_전수()
        {
            // valid, asleep, locked, 기대(멈추는가)
            var rows = new[]
            {
                new { valid = false, asleep = false, locked = false, expect = false },
                new { valid = false, asleep = true,  locked = false, expect = false }, // 관측실패는 값이 무의미
                new { valid = false, asleep = false, locked = true,  expect = false },
                new { valid = false, asleep = true,  locked = true,  expect = false },
                new { valid = true,  asleep = false, locked = false, expect = false }, // 보고 있다
                new { valid = true,  asleep = true,  locked = false, expect = true  }, // ★ macOS 다리
                new { valid = true,  asleep = false, locked = true,  expect = true  }, // ★ Windows 다리
                new { valid = true,  asleep = true,  locked = true,  expect = true  },
            };

            int trueCells = 0;
            foreach (var r in rows)
            {
                ViewerPresenceSnapshot s = r.valid ? Snapshot(r.asleep, r.locked) : default;
                Assert.AreEqual(r.expect, SessionVisibilityPolicy.ShouldSuspendFootholdScan(s),
                    $"valid={r.valid}, asleep={r.asleep}, locked={r.locked}에서 판정이 다릅니다.");
                if (r.expect) trueCells++;
            }

            // ★ 비공허성: 표가 실수로 전부 false 기대가 되면 위 루프는 "아무것도 안 재고" 통과한다.
            Assert.AreEqual(8, rows.Length, "진리표가 8칸이 아닙니다 — 축이 늘었는데 표가 안 늘었습니다.");
            Assert.AreEqual(3, trueCells, "멈추는 칸이 3개가 아닙니다. 표 자체가 무너졌습니다.");
        }

        /// <summary>
        /// ★★ <b>두 다리가 각각 하중을 받는다.</b> 이 테스트가 이 파일의 존재 이유다.
        ///
        /// <para>OR에서 한 항을 지우면 그 항으로만 서던 플랫폼이 조용히 기능을 잃는다. 그런데
        /// 스냅샷 하나(둘 다 참)만 검사하면 <b>어느 항을 지워도 여전히 초록</b>이다. 그래서 각 플랫폼이
        /// 실제로 만들어 내는 <b>모양 그대로</b>의 스냅샷을 따로 넣는다.</para>
        /// </summary>
        [Test]
        public void 두_다리가_각각_단독으로_게이트를_세운다()
        {
            // macOS가 실제로 만드는 모양: CGDisplayIsAsleep은 채워지고 SessionLocked은 언제나 false.
            ViewerPresenceSnapshot macShape = Snapshot(asleep: true, locked: false);
            Assert.IsTrue(SessionVisibilityPolicy.ShouldSuspendFootholdScan(macShape),
                "macOS 모양(화면 꺼짐 단독)에서 게이트가 서지 않습니다 — DisplayAsleep 항이 지워졌다면 " +
                "macOS에서 이 기능이 통째로 사라진 것입니다. macOS에는 SessionLocked를 채울 " +
                "문서화된 수단이 없습니다(MacViewerPresenceService 클래스 문서의 후보 3종 배제 사유).");

            // Windows가 실제로 만드는 모양: DisplayAsleep은 언제나 false, 잠금만 채워진다.
            ViewerPresenceSnapshot winShape = Snapshot(asleep: false, locked: true);
            Assert.IsTrue(SessionVisibilityPolicy.ShouldSuspendFootholdScan(winShape),
                "Windows 모양(세션 잠금 단독)에서 게이트가 서지 않습니다 — SessionLocked 항이 " +
                "지워졌다면 Windows에서 이 기능이 통째로 사라진 것입니다. Windows는 창 프로시저를 " +
                "가로챌 수 없어 DisplayAsleep을 영원히 false로 보고합니다.");

            // 음성 대조 — 둘 다 거짓이면 서지 않는다(위 두 단언이 '항상 참'이 아님을 보인다).
            Assert.IsFalse(SessionVisibilityPolicy.ShouldSuspendFootholdScan(Snapshot(false, false)),
                "아무 신호도 없는데 게이트가 섰습니다 — 위 두 단언은 이제 아무것도 재지 않습니다.");
        }

        [Test]
        public void 관측_실패는_보고있다로_간다()
        {
            Assert.IsFalse(SessionVisibilityPolicy.ShouldSuspendFootholdScan(default),
                "Valid=false(조회 실패)에서 게이트가 섰습니다. 오판의 대가는 비대칭입니다 — " +
                "잘못 멈추면 사용자가 얼어붙은 캐릭터를 보고, 잘못 안 멈추면 전기를 조금 더 씁니다.");
            Assert.AreEqual("관측실패", SessionVisibilityPolicy.DescribeSuspendReason(default));
        }

        [Test]
        public void 사유_문자열이_어느_다리인지_구분한다()
        {
            // 실기 Windows가 없으므로 로그가 사실상 유일한 배선 확인 수단이다. 사유가 뭉개지면
            // "무엇이 멈췄는지"를 원격에서 가를 수 없다.
            Assert.AreEqual("화면꺼짐", SessionVisibilityPolicy.DescribeSuspendReason(Snapshot(true, false)));
            Assert.AreEqual("세션잠금", SessionVisibilityPolicy.DescribeSuspendReason(Snapshot(false, true)));
            Assert.AreEqual("화면꺼짐+세션잠금", SessionVisibilityPolicy.DescribeSuspendReason(Snapshot(true, true)));
            Assert.AreEqual("없음", SessionVisibilityPolicy.DescribeSuspendReason(Snapshot(false, false)));
        }

        // ====================================================================
        // 2. 보조 신호(보안 데스크톱) 신뢰 시한
        // ====================================================================

        /// <summary>
        /// <c>OpenInputDesktop</c>이 영구히 실패하는 환경에서 스캔이 <b>영원히</b> 멈추면, 이 라운드가
        /// 고치려는 버그(낡은 발판 고착)를 스스로 만든다. 그래서 보조 신호에는 시한이 있다.
        ///
        /// <para>상수를 숫자로 베끼지 않는다 — <see cref="SessionVisibilityPolicy.SecureDesktopTrustSeconds"/>를
        /// 직접 참조해 경계를 만든다(CLAUDE.md: 테스트에 프로덕션 상수를 숫자로 베끼지 않는다).</para>
        /// </summary>
        [Test]
        public void 보안데스크톱_보조신호는_시한을_넘기면_신뢰를_잃는다()
        {
            float limit = SessionVisibilityPolicy.SecureDesktopTrustSeconds;

            Assert.IsTrue(SessionVisibilityPolicy.ShouldTrustSecureDesktopSignal(0f),
                "막 시작된 보안 데스크톱을 못 믿으면 UAC 구간을 아예 못 덮습니다.");
            Assert.IsTrue(SessionVisibilityPolicy.ShouldTrustSecureDesktopSignal(limit * 0.5f));
            Assert.IsTrue(SessionVisibilityPolicy.ShouldTrustSecureDesktopSignal(limit),
                "경계값(정확히 시한)은 아직 믿는 쪽입니다.");
            Assert.IsFalse(SessionVisibilityPolicy.ShouldTrustSecureDesktopSignal(limit + 0.001f),
                "시한을 넘겼는데도 계속 믿으면, 권한 문제로 OpenInputDesktop이 늘 실패하는 환경에서 " +
                "발판 스캔이 영원히 멈춘 채 낡은 캐시로 굳습니다.");

            // "모름"은 믿지 않는 쪽으로 떨어진다 — 이 파일 전체의 보수 방향.
            Assert.IsFalse(SessionVisibilityPolicy.ShouldTrustSecureDesktopSignal(-1f));
            Assert.IsFalse(SessionVisibilityPolicy.ShouldTrustSecureDesktopSignal(float.NaN),
                "NaN이 신뢰 쪽으로 새면 시각 계산이 깨진 순간 스캔이 영구히 멈춥니다.");
        }

        // ====================================================================
        // 3. ★ 양성 대조 — 폴러가 실제로 서고, 실제로 다시 돈다
        // ====================================================================

        /// <summary>열거 횟수를 세는 스텁. 목록은 고정이라 <c>HasChanged</c>가 첫 폴링 이후 false다.</summary>
        private sealed class CountingWindowService : IPlatformWindowService
        {
            public int EnumerateCalls;
            private readonly List<PlatformFoothold> _list = new List<PlatformFoothold>
            {
                new PlatformFoothold(1L, new Rect(0f, 100f, 800f, 20f), false),
                new PlatformFoothold(2L, new Rect(200f, 300f, 400f, 20f), false),
            };

            public IReadOnlyList<PlatformFoothold> EnumerateFootholds()
            {
                EnumerateCalls++;
                return _list;
            }

            public bool CreateOverlayWindow() => true;
            public void SetClickThrough(bool enabled) { }
            public void SetAlwaysOnTop(bool enabled) { }
            public bool IsFullscreenAppActive() => false;
        }

        private sealed class FakePresence : IViewerPresenceService
        {
            public bool Valid = true;
            public bool DisplayAsleep;
            public bool SessionLocked;

            public bool TryGetPresence(out ViewerPresenceSnapshot snapshot)
            {
                if (!Valid) { snapshot = default; return false; }
                snapshot = new ViewerPresenceSnapshot(DisplayAsleep, 0f, false, false, SessionLocked);
                return true;
            }
        }

        /// <summary>0.5초(config=null일 때의 기본 주기)를 확실히 넘기는 틱 묶음.</summary>
        private static void TickFor(FootholdPoller poller, float seconds, float dt = 0.1f)
        {
            for (float t = 0f; t < seconds; t += dt) poller.Tick(dt);
        }

        [Test]
        public void 잠기면_폴링이_서고_풀리면_즉시_다시_돈다()
        {
            var service = new CountingWindowService();
            var presence = new FakePresence();
            FootholdPoller.PresenceProbeOverride = presence;

            // 생성자가 부트스트랩 1회를 돈다(첫 프레임에 발판이 없으면 캐릭터가 허공에 뜬다).
            var poller = new FootholdPoller(service, null);
            Assert.AreEqual(1, service.EnumerateCalls, "생성자 부트스트랩 폴링이 사라졌습니다.");
            Assert.AreEqual(2, poller.CachedFootholds.Count);
            Assert.IsFalse(poller.IsScanSuspended);

            // ---- (a) 잠기지 않음: 평소대로 돈다 (★ 양성 대조의 '켜짐' 쪽) ----
            TickFor(poller, 3f);
            int running = service.EnumerateCalls;
            Assert.Greater(running, 1,
                "잠기지 않았는데 폴링이 한 번도 돌지 않았습니다 — 아래 '멈춤' 단언이 " +
                "'원래 안 돌았다'와 구분되지 않으므로 이 테스트 전체가 무효입니다.");

            // ---- (b) 세션 잠금(Windows 모양): 완전히 선다 ----
            presence.SessionLocked = true;
            TickFor(poller, 5f);
            Assert.AreEqual(running, service.EnumerateCalls,
                "세션이 잠긴 동안에도 창 열거가 계속됐습니다 — 그 집합은 사용자가 잠금을 푼 뒤 " +
                "보게 될 화면과 아무 관계가 없습니다.");
            Assert.IsTrue(poller.IsScanSuspended);
            Assert.AreEqual(1, poller.ScanSuspendCount);

            // ★ 캐시를 비우지 않았다 — 비우는 순간 그것이 곧 발판 전멸이고, 고치려던 버그를 스스로 만든다.
            Assert.AreEqual(2, poller.CachedFootholds.Count,
                "중단 중에 발판 캐시가 비었습니다. 잠금 해제 순간 캐릭터가 화면 밖으로 떨어집니다.");

            // ---- (c) 해제: 주기를 기다리지 않고 그 틱에 즉시 한 번 돈다 ----
            presence.SessionLocked = false;
            poller.Tick(0.001f);   // 주기(0.5초)에 한참 못 미치는 dt
            Assert.AreEqual(running + 1, service.EnumerateCalls,
                "잠금 해제 직후 즉시 재열거가 없습니다 — 최대 footholdPollInterval 동안 " +
                "잠금 이전의 낡은 집합으로 접지 판정을 하게 됩니다.");
            Assert.IsFalse(poller.IsScanSuspended);
            Assert.AreEqual(1, poller.ScanResumeCount);
        }

        /// <summary>
        /// ★ 음성 대조 — 위 (c)의 "즉시 1회"가 <b>전이 때문</b>임을 보인다.
        /// 중단을 겪지 않은 폴러는 같은 0.001초 틱에 <b>돌지 않는다</b>. 이것이 없으면 (c)는
        /// "원래 매 틱 돌고 있었다"와 구분되지 않는다.
        /// </summary>
        [Test]
        public void 중단을_겪지_않았으면_짧은_틱에_돌지_않는다()
        {
            var service = new CountingWindowService();
            FootholdPoller.PresenceProbeOverride = new FakePresence();
            var poller = new FootholdPoller(service, null);

            int afterBootstrap = service.EnumerateCalls;
            poller.Tick(0.001f);
            Assert.AreEqual(afterBootstrap, service.EnumerateCalls,
                "주기에 못 미치는 틱인데 폴링이 돌았습니다 — 그렇다면 '해제 직후 즉시 1회'는 " +
                "전이가 만든 것이 아니라 원래 매 틱 돌던 것이고, 그 테스트는 아무것도 재지 않습니다.");
            Assert.AreEqual(0, poller.ScanResumeCount);
        }

        /// <summary>
        /// macOS 다리도 <b>같은 배선</b>을 통과하는지 확인한다. 정책만 검사하고 폴러는 Windows 다리로만
        /// 확인하면, macOS에서 실제로 게이트가 서는지는 아무도 재지 않은 것이 된다.
        /// </summary>
        [Test]
        public void 화면꺼짐_다리도_같은_배선으로_폴링을_세운다()
        {
            var service = new CountingWindowService();
            var presence = new FakePresence();
            FootholdPoller.PresenceProbeOverride = presence;
            var poller = new FootholdPoller(service, null);

            TickFor(poller, 3f);
            int running = service.EnumerateCalls;
            Assert.Greater(running, 1, "양성 대조 실패 — 애초에 돌고 있지 않았습니다.");

            presence.DisplayAsleep = true;      // macOS: CGDisplayIsAsleep
            presence.SessionLocked = false;     // macOS: 언제나 false
            TickFor(poller, 5f);
            Assert.AreEqual(running, service.EnumerateCalls,
                "화면이 꺼졌는데도 창 열거가 계속됐습니다(macOS 경로).");
            Assert.IsTrue(poller.IsScanSuspended);
        }

        /// <summary>
        /// 관측이 실패(<c>Valid=false</c>)하면 <b>지금까지의 동작이 그대로</b>여야 한다.
        /// 기능이 조용히 꺼지는 방향은 안전한 쪽이고, 반대 방향(모르는데 멈춤)은 없어야 한다.
        /// </summary>
        [Test]
        public void 관측이_실패하면_지금까지의_폴링이_그대로_유지된다()
        {
            var service = new CountingWindowService();
            var presence = new FakePresence { Valid = false };
            FootholdPoller.PresenceProbeOverride = presence;
            var poller = new FootholdPoller(service, null);

            TickFor(poller, 3f);
            Assert.Greater(service.EnumerateCalls, 1,
                "관측 실패인데 폴링이 멈췄습니다 — '모르면 멈추지 않는다'가 깨졌습니다.");
            Assert.IsFalse(poller.IsScanSuspended);
            Assert.AreEqual(0, poller.ScanSuspendCount);
        }
    }
}
