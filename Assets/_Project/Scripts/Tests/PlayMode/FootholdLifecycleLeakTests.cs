using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★ 사용자 신고 잠금(2026-09-01): <b>"껏다가 재실행하면 처음과 똑같다가 점점 느려짐 … 초기화 된것처럼"</b>
    /// / <b>"뭔가 데이터가 계속계속 누적되는거 같은 느낌이야"</b>
    ///
    /// 앱 재시작만으로 회복된다는 것은 <b>프로세스 안에 단조 증가하는 상태가 있다</b>는 뜻이고,
    /// 그중 가장 유력한 후보로 지목된 것이 <b>발판(창) 오브젝트의 생명주기</b>였다:
    /// "창을 열고 닫을 때마다 발판이 만들어지는데 사라진 창의 발판이 정리되지 않으면
    ///  매 FixedUpdate가 O(누적 창 수)로 무거워진다."
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 계약 (전부 "누적 총량"이 아니라 "현재 창 수"에 비례해야 한다)
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><b>L1</b> 발판/원본창 캐시는 폴링마다 <b>교체</b>된다. 창 목록을 수백 번 갈아끼워도
    ///         캐시 크기는 현재 창 수와 같고, 지나간 라운드의 핸들은 한 개도 살아남지 않는다.</item>
    ///   <item><b>L2</b> 발판 파이프라인은 <b>GameObject/Collider2D/Rigidbody2D를 하나도 만들지 않는다</b>.
    ///         (비활성 오브젝트까지 포함해 센서스한다 — "Destroy 안 하고 비활성화만" 패턴이 이 검사의 표적이다.)</item>
    ///   <item><b>L2n</b> (네거티브 컨트롤) 일부러 발판 하나당 GameObject를 만드는 가짜 소비자를 붙이면
    ///         L2의 센서스가 <b>반드시 증가를 잡아낸다</b>. 이게 없으면 L2는 "항상 참인 단언"일 수 있다.</item>
    ///   <item><b>L3</b> 재사용 버퍼(풀)의 용량은 <b>동시 창 수의 최고치</b>에만 반응하고, 그 뒤로는
    ///         아무리 오래 돌려도 다시 자라지 않는다("최대치가 계속 갱신되며 커지는" 패턴 금지).</item>
    ///   <item><b>L4</b> 창이 전부 사라지면 캐시는 0이 된다(잔재 없음).</item>
    ///   <item><b>L5</b> 폴링을 반복해도 <c>StickmanEventBus.FootholdsChanged</c> 구독자 수가 늘지 않는다
    ///         (정적 이벤트가 죽은 인스턴스를 붙드는 전형적 Unity 누수의 파수꾼).</item>
    ///   <item><b>L6</b> 위를 <b>실제 Tick 경로</b>(폴링 주기 게이팅 포함)에서도 확인한다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// 규칙 준수 메모 (CLAUDE.md)
    /// ============================================================================
    /// · <b>벽시계 예산</b>: L6은 프레임 수가 아니라 <c>Time.realtimeSinceStartup</c> 기준 초로 예산을 잡는다.
    ///   이 저장소의 배치모드 PlayMode는 8,200~13,200fps로 돌아 "180프레임"짜리 예산이 실제로는 0.0x초가 된다.
    /// · <b>프로덕션 상수 베끼기 금지</b>: 폴링 주기는 <c>StickConfig.footholdPollInterval</c>을 <b>읽어서</b>
    ///   기대 폴링 횟수를 계산하고, 버퍼 초기 용량은 생성 직후의 실제 <c>List.Capacity</c>를 기준선으로 삼는다
    ///   (FootholdPoller 안의 <c>new List&lt;PlatformFoothold&gt;(64)</c> 리터럴을 여기 옮겨 적지 않는다).
    /// · <b>원칙 3(유저 자산 불변)</b>: 여기서는 사각형 숫자만 만든다 — 실제 창을 열거하지도, 건드리지도 않는다.
    ///
    /// ============================================================================
    /// 플랫폼 (CLAUDE.md 플랫폼 동시 검토)
    /// ============================================================================
    /// 잠그는 대상이 <c>FootholdPoller</c>(플랫폼 중립)와 그 소비 계약이므로 <b>macOS/Windows 공통</b>이다.
    /// MacWindowService / Win32WindowService 어느 쪽을 고쳐도 발판이 누적되기 시작하면 이 파일이 깨진다.
    /// 실제 OS 열거는 가짜 서비스로 대체하므로 이 개발기(macOS)에서 Windows 시나리오도 그대로 검증된다.
    /// </summary>
    public sealed class FootholdLifecycleLeakTests
    {
        private const string LogPrefix = "[발판생명주기]";

        /// <summary>창 목록을 몇 번 통째로 갈아끼울 것인가. 실제 데스크톱에서 창 열고 닫기를 반복한 상황.</summary>
        private const int ChurnRounds = 300;

        /// <summary>한 라운드에 동시에 떠 있는 창 수(누적 총량 = ChurnRounds x 이 값).</summary>
        private const int WindowsPerRound = 6;

        private StickConfig _config;

        // ====================================================================================
        // 가짜 플랫폼 서비스 — 매 열거마다 창 목록을 통째로 교체한다(창을 전부 닫고 새로 여는 최악 케이스).
        // ====================================================================================

        private sealed class ChurningWindowService : IPlatformWindowService, IRawWindowRectSource
        {
            private readonly List<PlatformFoothold> _footholds = new List<PlatformFoothold>(8);
            private readonly List<PlatformFoothold> _raw = new List<PlatformFoothold>(8);

            private long _nextHandle = 1000L;
            private int _windowCount;

            /// <summary>지금까지 이 서비스가 만들어 낸 창의 <b>누적</b> 개수. 캐시 크기와 대비할 기준값.</summary>
            public long TotalWindowsEverPublished { get; private set; }

            /// <summary>마지막 라운드가 발급한 첫 핸들. 이보다 작은 핸들이 캐시에 남아 있으면 그게 곧 누적이다.</summary>
            public long LastRoundFirstHandle { get; private set; }

            public int EnumerateCallCount { get; private set; }

            public int WindowCount => _windowCount;

            public ChurningWindowService(int windowCount)
            {
                _windowCount = windowCount;
            }

            /// <summary>다음 열거부터 동시 창 수를 바꾼다.</summary>
            public void SetWindowCount(int count) => _windowCount = count;

            private void Rebuild()
            {
                _footholds.Clear();
                _raw.Clear();
                LastRoundFirstHandle = _nextHandle;
                for (int i = 0; i < _windowCount; i++)
                {
                    long h = _nextHandle++;
                    // 사각형도 매 라운드 달라지게 한다 — FootholdPoller.HasChanged()가 반드시 true가 되어
                    // "캐시 교체" 경로를 실제로 밟게 하기 위한 것이다(변경 없음 경로만 타면 검사가 헐거워진다).
                    var rect = new Rect(10f + i * 37f, 20f + (h % 211L), 300f + i, 200f);
                    var f = new PlatformFoothold(h, rect, i == 0);
                    _footholds.Add(f);
                    _raw.Add(f);
                    TotalWindowsEverPublished++;
                }
            }

            public IReadOnlyList<PlatformFoothold> EnumerateFootholds()
            {
                EnumerateCallCount++;
                Rebuild(); // 매 열거 = 창 세대 교체.
                return _footholds;
            }

            public IReadOnlyList<PlatformFoothold> RawWindows => _raw;

            public bool CreateOverlayWindow() => true;
            public void SetClickThrough(bool enabled) { }
            public void SetAlwaysOnTop(bool enabled) { }
            public bool IsFullscreenAppActive() => false;
        }

        /// <summary>
        /// L2n 네거티브 컨트롤 전용 — "사라진 창의 발판이 정리되지 않는" 가설을 <b>일부러 구현한</b> 가짜 소비자.
        /// 발판이 바뀔 때마다 발판 하나당 GameObject(+Collider2D +Rigidbody2D)를 만들고 절대 지우지 않는다.
        /// 프로덕션에는 이런 코드가 없다는 것이 L2의 주장이고, 이 클래스는 그 주장을 검증하는 센서스가
        /// 실제로 증가를 감지하는지 확인하는 용도다.
        /// </summary>
        private sealed class LeakyFootholdBodySpawner : IDisposable
        {
            private readonly FootholdPoller _poller;
            private readonly List<GameObject> _spawned = new List<GameObject>(1024);

            public LeakyFootholdBodySpawner(FootholdPoller poller)
            {
                _poller = poller;
                StickmanEventBus.FootholdsChanged += OnFootholdsChanged;
            }

            public int SpawnedCount => _spawned.Count;

            private void OnFootholdsChanged()
            {
                IReadOnlyList<PlatformFoothold> footholds = _poller.CachedFootholds;
                for (int i = 0; i < footholds.Count; i++)
                {
                    // ★ hideFlags를 설정하지 않는다 — Object.FindObjectsByType는 HideFlags.DontSave가 걸린
                    //   오브젝트를 돌려주지 않으므로, 여기서 숨기면 네거티브 컨트롤이 스스로를 못 보게 된다
                    //   (= L2n이 항상 실패). Dispose()에서 전부 DestroyImmediate하므로 씬을 더럽히지도 않는다.
                    var go = new GameObject("LeakProbeFoothold");
                    go.AddComponent<BoxCollider2D>();
                    go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
                    go.SetActive(false); // ★ "Destroy 안 하고 비활성화만" — 센서스가 이것까지 세는지 확인한다.
                    _spawned.Add(go);
                }
            }

            public void Dispose()
            {
                StickmanEventBus.FootholdsChanged -= OnFootholdsChanged;
                for (int i = 0; i < _spawned.Count; i++)
                {
                    if (_spawned[i] != null) UnityEngine.Object.DestroyImmediate(_spawned[i]);
                }
                _spawned.Clear();
            }
        }

        // ====================================================================================
        // 센서스 / 리플렉션 유틸
        // ====================================================================================

        private struct Census
        {
            public int Transforms;
            public int Colliders2D;
            public int Bodies2D;

            public override string ToString()
                => $"Transform {Transforms}개 / Collider2D {Colliders2D}개 / Rigidbody2D {Bodies2D}개";
        }

        /// <summary>
        /// 씬에 살아 있는 오브젝트 수를 센다. <b>비활성 오브젝트를 포함</b>하는 것이 이 검사의 핵심이다 —
        /// "사라진 창의 발판을 Destroy하지 않고 SetActive(false)만 한다"는 가설이 바로 그 형태이기 때문이다.
        /// </summary>
        private static Census TakeCensus()
        {
            return new Census
            {
                Transforms = UnityEngine.Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                Colliders2D = UnityEngine.Object.FindObjectsByType<Collider2D>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                Bodies2D = UnityEngine.Object.FindObjectsByType<Rigidbody2D>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
            };
        }

        private static List<PlatformFoothold> PrivateBuffer(FootholdPoller poller, string fieldName)
        {
            FieldInfo f = typeof(FootholdPoller).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f,
                $"{LogPrefix} FootholdPoller의 내부 버퍼 '{fieldName}'을 찾지 못했습니다. 필드 이름이 바뀌었다면 " +
                "이 테스트도 함께 고쳐야 합니다 — 이름만 바뀐 채 검사가 조용히 죽으면 누수 감시가 사라집니다.");
            var list = f.GetValue(poller) as List<PlatformFoothold>;
            Assert.IsNotNull(list, $"{LogPrefix} '{fieldName}'이 List<PlatformFoothold>가 아닙니다.");
            return list;
        }

        /// <summary>
        /// <c>StickmanEventBus.FootholdsChanged</c>의 현재 구독자 수. 필드형 이벤트의 백킹 필드를 직접 읽는다.
        /// </summary>
        private static int FootholdsChangedSubscriberCount()
        {
            FieldInfo f = typeof(StickmanEventBus).GetField("FootholdsChanged",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(f,
                $"{LogPrefix} StickmanEventBus.FootholdsChanged의 백킹 필드를 찾지 못했습니다 " +
                "(필드형 이벤트가 아니라 명시적 add/remove로 바뀐 경우). 구독자 누수 파수꾼이 죽었으므로 " +
                "이 테스트를 새 구조에 맞게 고쳐야 합니다.");
            var d = f.GetValue(null) as Delegate;
            return d == null ? 0 : d.GetInvocationList().Length;
        }

        private FootholdPoller NewPoller(ChurningWindowService service) => new FootholdPoller(service, _config);

        // ====================================================================================
        // SetUp / TearDown
        // ====================================================================================

        [SetUp]
        public void SetUp()
        {
            // 배포용 설정 자산은 절대 건드리지 않는다(CLAUDE.md 불변 원칙 3). 기본값 인스턴스를 새로 만든다.
            _config = ScriptableObject.CreateInstance<StickConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            // FootholdPoller 생성자가 전역 정적 설정을 밀어 넣으므로(StallAttribution/PlayerLogPolicy),
            // 다음 테스트로 새어 나가지 않게 되돌린다.
            PlayerLogPolicy.ResetForTests();
            if (_config != null) UnityEngine.Object.DestroyImmediate(_config);
            _config = null;
        }

        // ====================================================================================
        // L1 — 캐시는 누적이 아니라 교체다
        // ====================================================================================

        [Test]
        public void L1_창_목록을_수백번_갈아끼워도_캐시는_현재_창_수와_같다()
        {
            var service = new ChurningWindowService(WindowsPerRound);
            FootholdPoller poller = NewPoller(service);

            for (int r = 0; r < ChurnRounds; r++) poller.PollImmediately();

            // 전제 — 이 테스트가 의미를 가지려면 "누적 총량"이 "현재 창 수"보다 압도적으로 커야 한다.
            Assert.Greater(service.TotalWindowsEverPublished, WindowsPerRound * 100L,
                $"{LogPrefix} 전제 실패 — 창 교체가 충분히 일어나지 않았습니다(누적 {service.TotalWindowsEverPublished}개). " +
                "누적 총량이 작으면 '누적 vs 현재'를 구분할 수 없습니다.");

            Assert.AreEqual(WindowsPerRound, poller.CachedFootholds.Count,
                $"{LogPrefix} 발판 캐시가 {poller.CachedFootholds.Count}개입니다 — 현재 창 {WindowsPerRound}개여야 합니다. " +
                $"누적 총량은 {service.TotalWindowsEverPublished}개였습니다. 캐시가 교체가 아니라 누적되고 있으면 " +
                "매 프레임 접지 판정이 O(지금까지 열린 모든 창)이 되어 '쓸수록 느려지고 재시작하면 회복'이 그대로 재현됩니다.");

            Assert.AreEqual(WindowsPerRound, poller.CachedRawWindows.Count,
                $"{LogPrefix} 원본 창 캐시가 {poller.CachedRawWindows.Count}개입니다 — 현재 창 {WindowsPerRound}개여야 합니다. " +
                "이쪽은 발판 변경 여부와 무관하게 매 폴링 갱신되므로 누수가 생기면 더 빨리 자랍니다.");

            // 지나간 세대의 핸들이 한 개라도 살아남으면 그게 곧 "사라진 창의 발판이 정리되지 않음"이다.
            for (int i = 0; i < poller.CachedFootholds.Count; i++)
            {
                Assert.GreaterOrEqual(poller.CachedFootholds[i].Handle, service.LastRoundFirstHandle,
                    $"{LogPrefix} 발판 캐시에 지난 세대의 핸들({poller.CachedFootholds[i].Handle})이 남아 있습니다 — " +
                    $"마지막 라운드는 {service.LastRoundFirstHandle} 이상만 발급했습니다. 사라진 창의 발판이 정리되지 않았습니다.");
            }
            for (int i = 0; i < poller.CachedRawWindows.Count; i++)
            {
                Assert.GreaterOrEqual(poller.CachedRawWindows[i].Handle, service.LastRoundFirstHandle,
                    $"{LogPrefix} 원본 창 캐시에 지난 세대의 핸들({poller.CachedRawWindows[i].Handle})이 남아 있습니다.");
            }

            Debug.Log($"{LogPrefix} L1 통과 — 누적 {service.TotalWindowsEverPublished}개를 거쳤지만 캐시는 " +
                $"발판 {poller.CachedFootholds.Count}개 / 원본창 {poller.CachedRawWindows.Count}개.");
        }

        // ====================================================================================
        // L2 / L2n — 발판 하나당 오브젝트가 생기지 않는다 (+ 그 검사가 진짜로 잡아내는지)
        // ====================================================================================

        [Test]
        public void L2_발판_파이프라인은_GameObject를_하나도_만들지_않는다()
        {
            var service = new ChurningWindowService(WindowsPerRound);

            // ★ 센서스 사이에 yield가 없다 = 다른 MonoBehaviour의 Update가 끼어들 수 없다.
            //   그래서 이 비교는 다른 씬이 함께 로드돼 있어도 결정적이다(불안정 테스트 방지).
            FootholdPoller poller = NewPoller(service);
            Census before = TakeCensus();
            for (int r = 0; r < ChurnRounds; r++) poller.PollImmediately();
            Census after = TakeCensus();

            Assert.AreEqual(before.Transforms, after.Transforms,
                $"{LogPrefix} 창을 {ChurnRounds}번 갈아끼우는 동안 GameObject가 " +
                $"{after.Transforms - before.Transforms}개 늘었습니다(비활성 포함). 이전 {before} -> 이후 {after}. " +
                "발판은 순수 값(struct)이어야 하며 창 하나당 씬 오브젝트를 만들면 안 됩니다.");
            Assert.AreEqual(before.Colliders2D, after.Colliders2D,
                $"{LogPrefix} Collider2D가 {after.Colliders2D - before.Colliders2D}개 늘었습니다 — " +
                "물리 오브젝트가 쌓이면 매 FixedUpdate가 O(누적)으로 무거워집니다.");
            Assert.AreEqual(before.Bodies2D, after.Bodies2D,
                $"{LogPrefix} Rigidbody2D가 {after.Bodies2D - before.Bodies2D}개 늘었습니다.");

            Debug.Log($"{LogPrefix} L2 통과 — 누적 {service.TotalWindowsEverPublished}개 창을 거쳐도 씬 오브젝트 수 불변({after}).");
        }

        [Test]
        public void L2n_네거티브_발판당_오브젝트를_만드는_소비자를_붙이면_센서스가_증가를_잡는다()
        {
            const int LeakRounds = 20;

            var service = new ChurningWindowService(WindowsPerRound);
            FootholdPoller poller = NewPoller(service);

            Census before = TakeCensus();
            using (var leak = new LeakyFootholdBodySpawner(poller))
            {
                for (int r = 0; r < LeakRounds; r++) poller.PollImmediately();
                Census leaked = TakeCensus();

                Assert.AreEqual(LeakRounds * WindowsPerRound, leak.SpawnedCount,
                    $"{LogPrefix} 네거티브 컨트롤이 예상만큼 오브젝트를 만들지 않았습니다 " +
                    "(FootholdsChanged가 매 폴링 발행되지 않았다는 뜻 — 그러면 L2n이 성립하지 않습니다).");

                Assert.AreEqual(before.Transforms + leak.SpawnedCount, leaked.Transforms,
                    $"{LogPrefix} 일부러 만든 누수를 센서스가 정확히 잡아내지 못했습니다 " +
                    $"({before.Transforms} -> {leaked.Transforms}, 만든 수 {leak.SpawnedCount}). " +
                    "L2의 센서스가 '항상 참인 단언'이 아니라는 증명이 바로 이 항목입니다.");
                Assert.AreEqual(before.Colliders2D + leak.SpawnedCount, leaked.Colliders2D,
                    $"{LogPrefix} Collider2D 센서스가 누수를 잡지 못했습니다 " +
                    "(비활성 오브젝트를 세지 않으면 여기서 어긋납니다 — FindObjectsInactive.Include 확인).");
                Assert.AreEqual(before.Bodies2D + leak.SpawnedCount, leaked.Bodies2D,
                    $"{LogPrefix} Rigidbody2D 센서스가 누수를 잡지 못했습니다.");
            }

            Census restored = TakeCensus();
            Assert.AreEqual(before.Transforms, restored.Transforms,
                $"{LogPrefix} 네거티브 컨트롤이 만든 오브젝트를 정리하지 못했습니다 — 이 테스트 자신이 누수원이 됩니다.");

            Debug.Log($"{LogPrefix} L2n 통과 — 일부러 만든 누수 {LeakRounds * WindowsPerRound}개를 센서스가 정확히 감지/복구.");
        }

        // ====================================================================================
        // L3 — 재사용 버퍼의 용량은 "동시 창 수 최고치"에만 반응한다
        // ====================================================================================

        [Test]
        public void L3_재사용_버퍼_용량은_동시_창수_최고치에만_반응하고_계속_자라지_않는다()
        {
            var service = new ChurningWindowService(WindowsPerRound);
            FootholdPoller poller = NewPoller(service);

            List<PlatformFoothold> cache = PrivateBuffer(poller, "_cache");
            List<PlatformFoothold> rawCache = PrivateBuffer(poller, "_rawCache");

            // 기준선은 "생성 직후의 실제 용량"이다 — FootholdPoller 안의 초기 용량 리터럴을 여기 베끼지 않는다.
            int baseCap = cache.Capacity;
            int baseRawCap = rawCache.Capacity;
            Assert.Greater(baseCap, 0, $"{LogPrefix} 전제 실패 — 초기 용량을 읽지 못했습니다.");

            // (1) 동시 창 수가 초기 용량보다 훨씬 적은 상태로 오래 돌린다 -> 용량은 1바이트도 자라면 안 된다.
            for (int r = 0; r < ChurnRounds; r++) poller.PollImmediately();
            Assert.AreEqual(baseCap, cache.Capacity,
                $"{LogPrefix} 동시 창 {WindowsPerRound}개(초기 용량 {baseCap})인데 발판 버퍼 용량이 " +
                $"{cache.Capacity}로 자랐습니다 — 폴링마다 Clear 없이 Add되고 있다는 결정적 증거입니다.");
            Assert.AreEqual(baseRawCap, rawCache.Capacity,
                $"{LogPrefix} 원본 창 버퍼 용량이 {rawCache.Capacity}로 자랐습니다(초기 {baseRawCap}).");

            // (2) 동시 창 수를 초기 용량 너머로 한 번 크게 올린다 -> 여기서 자라는 것은 정상이다(동시 창 수 비례).
            int peak = baseCap * 3;
            service.SetWindowCount(peak);
            for (int r = 0; r < 5; r++) poller.PollImmediately();
            int capAfterPeak = cache.Capacity;
            int rawCapAfterPeak = rawCache.Capacity;
            Assert.AreEqual(peak, poller.CachedFootholds.Count, $"{LogPrefix} 전제 실패 — 스파이크가 반영되지 않았습니다.");
            Assert.LessOrEqual(capAfterPeak, Mathf.Max(baseCap, peak * 2),
                $"{LogPrefix} 동시 창 {peak}개인데 버퍼 용량이 {capAfterPeak}입니다 — 동시 창 수에 비례하지 않습니다.");

            // (3) 다시 줄인 뒤 오래 돌린다 -> 최고치가 계속 갱신되며 커지면 안 된다(이 항목이 "풀 상한" 검사의 본체).
            service.SetWindowCount(WindowsPerRound);
            for (int r = 0; r < ChurnRounds; r++) poller.PollImmediately();
            Assert.AreEqual(capAfterPeak, cache.Capacity,
                $"{LogPrefix} 창 수를 {WindowsPerRound}개로 되돌린 뒤에도 발판 버퍼 용량이 " +
                $"{capAfterPeak} -> {cache.Capacity}로 계속 커졌습니다. 풀의 최대치가 세션 내내 갱신되는 패턴 = " +
                "'쓸수록 느려지고 재시작하면 회복'의 교과서적 형태입니다.");
            Assert.AreEqual(rawCapAfterPeak, rawCache.Capacity,
                $"{LogPrefix} 원본 창 버퍼 용량이 계속 커졌습니다({rawCapAfterPeak} -> {rawCache.Capacity}).");

            Assert.AreEqual(WindowsPerRound, poller.CachedFootholds.Count,
                $"{LogPrefix} 동시 창 수를 줄였는데 캐시가 따라 줄지 않았습니다({poller.CachedFootholds.Count}개).");

            Debug.Log($"{LogPrefix} L3 통과 — 초기 용량 {baseCap}, 최고 동시 {peak}개 이후 용량 {capAfterPeak}에서 고정. " +
                $"누적 {service.TotalWindowsEverPublished}개 창을 거쳤지만 그 뒤 추가 성장 0.");
        }

        // ====================================================================================
        // L4 — 창이 전부 사라지면 잔재가 남지 않는다
        // ====================================================================================

        [Test]
        public void L4_창이_전부_사라지면_캐시는_0이_된다()
        {
            var service = new ChurningWindowService(WindowsPerRound);
            FootholdPoller poller = NewPoller(service);
            for (int r = 0; r < 50; r++) poller.PollImmediately();
            Assert.AreEqual(WindowsPerRound, poller.CachedFootholds.Count, $"{LogPrefix} 전제 실패 — 발판이 잡히지 않았습니다.");

            service.SetWindowCount(0);
            poller.PollImmediately();

            Assert.AreEqual(0, poller.CachedFootholds.Count,
                $"{LogPrefix} 창이 전부 사라졌는데 발판이 {poller.CachedFootholds.Count}개 남아 있습니다 — " +
                "캐릭터가 존재하지 않는 창의 경계를 딛게 되고(허공 걷기), 그 잔재는 영원히 사라지지 않습니다.");
            Assert.AreEqual(0, poller.CachedRawWindows.Count,
                $"{LogPrefix} 원본 창 캐시에 {poller.CachedRawWindows.Count}개가 남아 있습니다.");

            // 다시 창이 생기면 정확히 그만큼만 돌아온다(0으로 만든 뒤 복구 경로도 함께 잠근다).
            service.SetWindowCount(WindowsPerRound);
            poller.PollImmediately();
            Assert.AreEqual(WindowsPerRound, poller.CachedFootholds.Count,
                $"{LogPrefix} 창이 다시 생겼는데 발판이 {poller.CachedFootholds.Count}개입니다.");

            Debug.Log($"{LogPrefix} L4 통과 — 0개 -> 잔재 없음 -> {WindowsPerRound}개로 정확히 복구.");
        }

        // ====================================================================================
        // L5 — 정적 이벤트 구독자가 늘지 않는다
        // ====================================================================================

        [Test]
        public void L5_폴링을_반복해도_FootholdsChanged_구독자_수가_늘지_않는다()
        {
            int before = FootholdsChangedSubscriberCount();

            var service = new ChurningWindowService(WindowsPerRound);
            FootholdPoller poller = NewPoller(service);
            for (int r = 0; r < ChurnRounds; r++) poller.PollImmediately();

            int after = FootholdsChangedSubscriberCount();
            Assert.AreEqual(before, after,
                $"{LogPrefix} FootholdsChanged 구독자가 {before} -> {after}로 늘었습니다. 정적 이벤트가 " +
                "죽은 인스턴스를 계속 붙드는 전형적 누수이며, 발행 1회 비용이 세션 시간에 비례해 커집니다.");

            // 폴러를 여러 개 만들어도 마찬가지여야 한다(폴러가 스스로 구독하지 않는다는 계약).
            for (int i = 0; i < 20; i++)
            {
                FootholdPoller extra = NewPoller(new ChurningWindowService(WindowsPerRound));
                extra.PollImmediately();
            }
            Assert.AreEqual(before, FootholdsChangedSubscriberCount(),
                $"{LogPrefix} FootholdPoller를 20개 더 만들자 구독자 수가 {FootholdsChangedSubscriberCount()}로 늘었습니다 — " +
                "폴러는 이벤트를 발행만 하고 구독하지 않아야 합니다.");

            Debug.Log($"{LogPrefix} L5 통과 — 구독자 수 {before} 유지.");
        }

        // ====================================================================================
        // L6 — 실제 Tick 경로(폴링 주기 게이팅 포함), 벽시계 예산
        // ====================================================================================

        /// <summary>
        /// 벽시계 예산(초). ★ 프레임 수 기준 금지 — 배치모드 PlayMode가 8,200~13,200fps로 돌기 때문에
        /// "N프레임" 예산은 실제로 수 밀리초가 되어 폴링이 한 번도 안 일어난 채 통과할 수 있다(CLAUDE.md).
        /// </summary>
        private const float TickBudgetSeconds = 3.0f;

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator L6_실제_Tick_경로에서도_캐시와_버퍼가_자라지_않는다()
        {
            var service = new ChurningWindowService(WindowsPerRound);
            FootholdPoller poller = NewPoller(service);
            List<PlatformFoothold> cache = PrivateBuffer(poller, "_cache");
            List<PlatformFoothold> rawCache = PrivateBuffer(poller, "_rawCache");

            int baseCap = cache.Capacity;
            int baseRawCap = rawCache.Capacity;
            int enumAtStart = service.EnumerateCallCount;

            float start = Time.realtimeSinceStartup;
            int frames = 0;
            while (Time.realtimeSinceStartup - start < TickBudgetSeconds)
            {
                // 프로덕션(StickmanAgent.Update)은 Time.deltaTime을 넘긴다. 여기서 unscaled를 쓰는 이유는
                // 다른 테스트가 Time.timeScale을 건드린 채 끝났더라도 벽시계 예산과 폴링 횟수의 관계가
                // 무너지지 않게 하기 위해서다(누수 성질 자체는 어느 시계로 몰아도 동일하다).
                poller.Tick(Time.unscaledDeltaTime);
                frames++;
                yield return null;
            }
            float elapsed = Time.realtimeSinceStartup - start;
            int polls = service.EnumerateCallCount - enumAtStart;

            // 프로덕션 상수를 베끼지 않는다 — 기대 폴링 횟수는 설정값(StickConfig.footholdPollInterval)에서
            // 계산한다. FootholdPoller가 이 값에 하한 클램프를 걸지만, 하한이 걸리면 폴링이 더 자주 일어나
            // 실제 횟수가 기대치보다 커질 뿐이라 아래 "최소 횟수" 단언은 어느 쪽으로도 안전하다.
            float interval = Mathf.Max(0.001f, _config.footholdPollInterval);
            int expected = Mathf.FloorToInt(elapsed / interval);
            Assert.GreaterOrEqual(polls, Mathf.Max(2, expected / 2),
                $"{LogPrefix} {elapsed:F2}초({frames}프레임) 동안 폴링이 {polls}회뿐입니다 — 폴링 주기 " +
                $"{interval:F2}초 기준 기대치 약 {expected}회. 폴링이 사실상 안 일어났다면 이 테스트는 " +
                "누수를 검증한 것이 아니라 아무것도 하지 않은 것입니다(무의미 통과 방지 가드).");

            Assert.AreEqual(WindowsPerRound, poller.CachedFootholds.Count,
                $"{LogPrefix} Tick 경로에서 발판 캐시가 {poller.CachedFootholds.Count}개가 됐습니다 " +
                $"(현재 창 {WindowsPerRound}개, 누적 {service.TotalWindowsEverPublished}개).");
            Assert.AreEqual(WindowsPerRound, poller.CachedRawWindows.Count,
                $"{LogPrefix} Tick 경로에서 원본 창 캐시가 {poller.CachedRawWindows.Count}개가 됐습니다.");
            Assert.AreEqual(baseCap, cache.Capacity,
                $"{LogPrefix} Tick 경로에서 발판 버퍼 용량이 {baseCap} -> {cache.Capacity}로 자랐습니다.");
            Assert.AreEqual(baseRawCap, rawCache.Capacity,
                $"{LogPrefix} Tick 경로에서 원본 창 버퍼 용량이 {baseRawCap} -> {rawCache.Capacity}로 자랐습니다.");

            Debug.Log($"{LogPrefix} L6 통과 — 벽시계 {elapsed:F2}초 / {frames}프레임 / 폴링 {polls}회, " +
                $"누적 창 {service.TotalWindowsEverPublished}개를 거쳐도 캐시 {poller.CachedFootholds.Count}개, 용량 {cache.Capacity} 고정.");
        }
    }
}
