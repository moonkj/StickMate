using System.IO;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// 실행 중 디스플레이 구성 변경 -> 오버레이 전체화면 <b>재적합 재무장</b> 회귀 잠금
    /// (2026-08-31 perf-doc 정적 분석 지적).
    ///
    /// ============================================================================
    /// 무엇이 깨졌었나
    /// ============================================================================
    /// 양 플랫폼 Enforcer의 <c>TickFullScreenBounds()</c>는 한 번 성공하면
    /// <c>_fullScreenBoundsApplied = true</c>를 걸었고, <b>그 플래그를 다시 false로 되돌리는 경로가
    /// 코드 어디에도 없었다.</b> 앱이 켜진 채 해상도/모니터 구성/DPI를 바꾸면 오버레이 창은 최초 기동
    /// 시점 해상도 그대로 영원히 고정되고, 재시작 전까지 창 크기/원점/좌표계가 새 화면과 어긋난 채
    /// 남았다.
    ///
    /// ============================================================================
    /// 이 테스트가 지키는 두 불변식
    /// ============================================================================
    /// 1. <b>재무장된다</b> — 화면 구성이 실제로 달라지면 재적합 신호가 반드시 한 번 나온다.
    /// 2. <b>★ 디바운스된다</b> — 한 번의 전환에 OS가 중간 상태를 여러 번 노출해도 신호는 <b>딱 1회</b>.
    ///    이쪽이 더 중요하다. 디바운스가 없으면 중간 상태마다 <c>Screen.SetResolution</c>이 불려
    ///    <b>고치려던 것보다 큰 히치를 우리가 직접 만든다</b>. 그래서 "몇 번 트리거됐나"를 세는 단언과
    ///    "일찍 발화하지 않는다"는 네거티브 컨트롤을 함께 둔다.
    ///
    /// 순수 로직(<see cref="DisplayTopologyWatcher"/>)만 검사한다. 실제 모니터 조회
    /// (<c>UniWindowController.GetMonitorRect</c> / <c>CGDisplayBounds</c>)는 플랫폼 Enforcer 안에
    /// 있고 배치 모드 EditMode에서는 실행되지 않으므로, 여기서는 관측값을 손으로 만들어 넣는다
    /// (= perf-doc이 요구한 "모니터 사각형 변화 모킹").
    /// </summary>
    public sealed class DisplayTopologyRefitTests
    {
        // 실제 Enforcer가 쓰는 값과 같은 디바운스(0.75초)를 그대로 검사한다.
        private const float Settle = DisplayTopologyWatcher.DefaultSettleSeconds;
        private const float Tick = 1f / 60f;

        private static DisplayTopologySignature Sig(float w, float h, int monitors = 1,
            float x = 0f, float y = 0f, float density = 0f)
            => DisplayTopologySignature.Create(monitors, new Rect(x, y, w, h), new Vector2(w, h), density);

        /// <summary>지정한 시간만큼 같은 관측을 60fps로 흘려보내고, 그 동안 나온 트리거 수를 센다.</summary>
        private static int Advance(DisplayTopologyWatcher watcher, in DisplayTopologySignature s, float seconds)
        {
            int triggers = 0;
            for (float t = 0f; t < seconds; t += Tick)
            {
                if (watcher.Observe(s, Tick)) triggers++;
            }
            return triggers;
        }

        // ========================================================================
        // 1. 재무장 — 기본 동작
        // ========================================================================

        [Test]
        public void 첫_관측은_기준값만_잡고_재적합하지_않는다()
        {
            var watcher = new DisplayTopologyWatcher();
            DisplayTopologySignature start = Sig(1920f, 1080f);

            Assert.IsFalse(watcher.Observe(start, Tick),
                "기동 직후 첫 관측은 '변경'이 아니라 '기준값 설정'이다. 여기서 트리거되면 앱이 켜지자마자 "
                + "쓸데없는 재적합이 한 번 더 돈다.");
            Assert.AreEqual(0, watcher.TriggerCount);
            Assert.IsTrue(watcher.HasBaseline);
        }

        [Test]
        public void 해상도가_바뀌고_안정되면_재적합이_트리거된다()
        {
            var watcher = new DisplayTopologyWatcher();
            watcher.Observe(Sig(1920f, 1080f), Tick);

            DisplayTopologySignature changed = Sig(2560f, 1440f);
            int triggers = Advance(watcher, changed, Settle + 0.2f);

            Assert.AreEqual(1, triggers, "해상도 변경이 안정된 뒤에는 반드시 재적합이 재무장돼야 한다 "
                + "— 이것이 없던 것이 이번 버그의 본체다.");
            Assert.AreEqual(changed, watcher.Baseline, "트리거 후 기준값은 새 구성으로 갱신돼야 한다.");
        }

        [Test]
        public void 모니터_개수가_바뀌어도_재적합이_트리거된다()
        {
            var watcher = new DisplayTopologyWatcher();
            watcher.Observe(Sig(1920f, 1080f, monitors: 1), Tick);

            int triggers = Advance(watcher, Sig(1920f, 1080f, monitors: 2), Settle + 0.2f);

            Assert.AreEqual(1, triggers, "모니터를 붙였다 떼는 것도 토폴로지 변경이다 "
                + "(해상도는 그대로여도 창 중심이 속한 모니터가 달라질 수 있다).");
        }

        [Test]
        public void 해상도가_같아도_DPI배율만_바뀌면_재적합이_트리거된다()
        {
            var watcher = new DisplayTopologyWatcher();
            watcher.Observe(Sig(1920f, 1080f, density: 1.0f), Tick);

            int triggers = Advance(watcher, Sig(1920f, 1080f, density: 1.5f), Settle + 0.2f);

            Assert.AreEqual(1, triggers, "Windows에서 100% -> 150% 배율 변경은 물리 픽셀 해상도를 바꾸지 "
                + "않는다. UI 밀도 항이 시그니처에 없으면 이 변경이 통째로 안 잡힌다.");
        }

        [Test]
        public void 변화가_없으면_아무리_오래_돌아도_트리거되지_않는다()
        {
            var watcher = new DisplayTopologyWatcher();
            DisplayTopologySignature stable = Sig(1920f, 1080f);
            watcher.Observe(stable, Tick);

            Assert.AreEqual(0, Advance(watcher, stable, 10f),
                "24시간 상주 앱이다. 가만히 있는데 주기적으로 Screen.SetResolution이 불리면 그 자체가 결함이다.");
        }

        // ========================================================================
        // 2. ★ 디바운스 — 이번 수정의 가장 중요한 제약
        // ========================================================================

        [Test]
        public void 디바운스_창_안에서는_아직_트리거되지_않는다()
        {
            var watcher = new DisplayTopologyWatcher();
            watcher.Observe(Sig(1920f, 1080f), Tick);

            // 안정 시간에 못 미치는 동안은 계속 침묵해야 한다(네거티브 컨트롤 — 즉시 발화하는 구현이면
            // 여기서 실패한다).
            int triggers = Advance(watcher, Sig(2560f, 1440f), Settle - 0.1f);

            Assert.AreEqual(0, triggers, "마지막 변화 직후에 곧바로 재적합하면 해상도 전환 중간 상태마다 "
                + "SetResolution이 불려 지금보다 큰 히치를 만든다.");
            Assert.IsTrue(watcher.IsSettling, "아직 안정 대기 중이어야 한다.");
        }

        [Test]
        public void 한_번의_전환에서_중간_상태가_여러_번_와도_재적합은_정확히_1회다()
        {
            var watcher = new DisplayTopologyWatcher();
            watcher.Observe(Sig(1920f, 1080f), Tick);

            int triggers = 0;

            // 해상도 모드 전환 1회의 현실적인 모양: 모니터가 잠깐 사라지고(개수 변화), 임시 해상도를
            // 거쳐, 최종 해상도로 안착한다. 각 중간 상태는 디바운스보다 짧게 머문다.
            triggers += Advance(watcher, Sig(1920f, 1080f, monitors: 2), 0.1f);
            triggers += Advance(watcher, Sig(1024f, 768f), 0.15f);
            triggers += Advance(watcher, Sig(2560f, 1440f, density: 1.25f), 0.1f);
            DisplayTopologySignature final = Sig(2560f, 1440f, density: 1.5f);
            triggers += Advance(watcher, final, Settle + 0.2f);

            Assert.AreEqual(1, triggers, "★ 이 단언이 이번 수정의 핵심이다 — 중간 상태 개수만큼 "
                + "재적합하면 고치려던 히치보다 큰 히치를 직접 만든다.");
            Assert.AreEqual(final, watcher.Baseline, "채택된 구성은 중간 상태가 아니라 최종 구성이어야 한다.");
        }

        [Test]
        public void 디바운스_창_안의_추가_변경은_안정_타이머를_처음부터_다시_센다()
        {
            var watcher = new DisplayTopologyWatcher();
            watcher.Observe(Sig(1920f, 1080f), Tick);

            // (Settle - 0.1)초 동안 A로 흔들다가 B로 바뀐다. 타이머를 리셋하지 않는 구현이라면
            // B로 바뀐 직후 0.1초 만에 발화해 버린다(= 중간 상태 위에서 재적합).
            Assert.AreEqual(0, Advance(watcher, Sig(1024f, 768f), Settle - 0.1f));
            Assert.AreEqual(0, Advance(watcher, Sig(2560f, 1440f), Settle - 0.1f),
                "값이 또 바뀌었으면 안정 시간은 처음부터 다시 세야 한다.");

            Assert.AreEqual(1, Advance(watcher, Sig(2560f, 1440f), 0.2f),
                "마지막 변화로부터 안정 시간이 채워지는 순간 그때 딱 한 번 트리거된다.");
        }

        [Test]
        public void 잠깐_흔들렸다가_원래_구성으로_돌아오면_재적합하지_않는다()
        {
            var watcher = new DisplayTopologyWatcher();
            DisplayTopologySignature original = Sig(1920f, 1080f);
            watcher.Observe(original, Tick);

            Advance(watcher, Sig(1024f, 768f), 0.2f);          // 전환 중간 상태
            int triggers = Advance(watcher, original, Settle + 0.3f); // 원래대로 복귀

            Assert.AreEqual(0, triggers, "순 변화가 0인데 재적합하면 그것은 순수한 손해(히치)다.");
            Assert.AreEqual(original, watcher.Baseline);
        }

        [Test]
        public void 관측_실패는_상태를_건드리지_않는다()
        {
            var watcher = new DisplayTopologyWatcher();
            watcher.Observe(Sig(1920f, 1080f), Tick);
            Advance(watcher, Sig(2560f, 1440f), 0.2f);

            // 모니터 조회가 잠깐 실패하는 프레임(전환 순간에 실제로 일어난다).
            for (int i = 0; i < 30; i++)
            {
                Assert.IsFalse(watcher.Observe(DisplayTopologySignature.Invalid, Tick),
                    "조회 실패를 '변경'으로 오인해 재적합하면 안 된다.");
            }
            Assert.IsTrue(watcher.IsSettling, "실패 프레임은 진행 중인 디바운스를 취소하지도 않는다.");

            Assert.AreEqual(1, Advance(watcher, Sig(2560f, 1440f), Settle + 0.2f),
                "조회가 돌아오면 원래 흐름대로 한 번 트리거된다.");
        }

        [Test]
        public void 미세한_부동소수_흔들림은_변경으로_보지_않는다()
        {
            var watcher = new DisplayTopologyWatcher();
            watcher.Observe(Sig(1920f, 1080f), Tick);

            Assert.AreEqual(0, Advance(watcher, Sig(1920.2f, 1079.8f), Settle + 0.5f),
                "시그니처는 정수로 양자화된다 — 0.2pt 흔들림에 재적합하면 그 자체가 상시 히치다.");
        }

        // ========================================================================
        // 3. 자기 되먹임 차단 — Enforcer가 ResetBaseline을 쓰는 이유
        // ========================================================================

        [Test]
        public void 재적합_후_기준값_재동기화하면_같은_변화로_다시_트리거되지_않는다()
        {
            var watcher = new DisplayTopologyWatcher();
            watcher.Observe(Sig(1920f, 1080f), Tick);

            DisplayTopologySignature changed = Sig(2560f, 1440f);
            Assert.AreEqual(1, Advance(watcher, changed, Settle + 0.2f));

            // Enforcer는 재적합 에피소드가 끝나면 관측 대신 ResetBaseline을 1회 호출한다. 재적합이
            // 스스로 만든 변화(창 크기/Unity 해상도)를 새 사건으로 오인하지 않게 하는 지점이다.
            watcher.ResetBaseline(changed);

            Assert.AreEqual(1, watcher.TriggerCount, "재동기화 자체는 트리거가 아니다.");
            Assert.AreEqual(0, Advance(watcher, changed, 5f),
                "재적합 -> 시그니처 변화 -> 재적합의 무한 루프가 생기면 앱이 영원히 해상도를 다시 잡는다.");
        }

        [Test]
        public void 기준값_재동기화는_대기중인_디바운스를_버린다()
        {
            var watcher = new DisplayTopologyWatcher();
            watcher.Observe(Sig(1920f, 1080f), Tick);
            Advance(watcher, Sig(2560f, 1440f), Settle - 0.1f);
            Assert.IsTrue(watcher.IsSettling);

            watcher.ResetBaseline(Sig(2560f, 1440f));

            Assert.IsFalse(watcher.IsSettling);
            Assert.AreEqual(0, watcher.TriggerCount);
        }

        // ========================================================================
        // 4. 양 플랫폼 동시 수정 잠금 (오늘 VisibleTopEdgeSolver 한쪽만 고쳐 재발한 사례 대응)
        // ========================================================================

        /// <summary>
        /// Windows/macOS Enforcer <b>둘 다</b>가 재무장 경로를 갖고 있는지 소스 텍스트로 확인한다.
        /// 두 파일은 각각 <c>#if UNITY_STANDALONE_WIN</c> / <c>_OSX</c> 안에 있어 EditMode에서는
        /// 컴파일조차 되지 않으므로 리플렉션으로는 검사할 수 없다 — 그래서
        /// UserAssetImmutabilityAuditTests와 같은 정적 소스 스캔 방식을 쓴다.
        ///
        /// 검사 항목은 "구현 세부"가 아니라 이번 결함의 <b>구조적 조건</b> 세 가지다:
        ///   (a) 공용 디바운스 감시기를 쓴다        — 한쪽만 자체 구현하면 다시 갈라진다
        ///   (b) 성공 플래그를 false로 되돌린다     — 이 경로가 없던 것이 버그의 본체
        ///   (c) 재적합 직후 좌표계를 즉시 보고한다 — 폴링 대기 동안 좌표가 어긋나지 않게
        /// </summary>
        [Test]
        public void 양_플랫폼_Enforcer가_모두_재무장_경로를_갖는다()
        {
            string platformRoot = Path.Combine(Application.dataPath, "_Project", "Scripts", "Platform");
            string[] enforcers =
            {
                Path.Combine(platformRoot, "Windows", "WindowsOverlayStateEnforcer.cs"),
                Path.Combine(platformRoot, "MacOS", "MacOverlayStateEnforcer.cs"),
            };

            foreach (string path in enforcers)
            {
                Assert.IsTrue(File.Exists(path), $"Enforcer 소스를 찾지 못했습니다: {path}");
                string src = File.ReadAllText(path);
                string name = Path.GetFileName(path);

                StringAssert.Contains("DisplayTopologyWatcher", src,
                    $"{name}: 공용 디바운스 감시기를 쓰지 않는다 — 플랫폼마다 따로 구현하면 " +
                    "한쪽만 고쳐 다른 쪽에서 재발하는 사고가 반복된다.");
                StringAssert.Contains("_fullScreenBoundsApplied = false", src,
                    $"{name}: 전체화면 적합 성공 플래그를 되돌리는 경로가 없다 — 오버레이 창이 최초 " +
                    "기동 해상도에 영원히 박제되는 바로 그 결함이다.");
                StringAssert.Contains("OverlayRectReporter", src,
                    $"{name}: 재적합 직후 좌표계를 즉시 보고하는 훅이 없다 — 폴링 주기 동안 원점/배율이 " +
                    "옛 값이라 캐릭터가 화면 밖으로 튄다.");
            }
        }
    }
}
