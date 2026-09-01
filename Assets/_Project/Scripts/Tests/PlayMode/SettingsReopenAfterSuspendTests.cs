using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ <b>전체화면이 지나가면 설정창이 사라지고 안 돌아온다</b> — 2026-09-02 (리더 검증 중 3회 발생).
    ///
    /// ============================================================================
    /// 무엇이 문제였나 — <b>사라진 것</b>이 아니라 <b>흔적이 없는 것</b>이 문제였다
    /// ============================================================================
    /// 캐릭터가 숨는 것은 원칙 2이고 옳다. 그런데 <b>설정을 만지던 창</b>이 예고 없이 닫히고,
    /// 게임을 끄고 돌아와도 복구되지 않고, 아무 흔적도 남지 않았다. 읽히는 뜻은 하나다 —
    /// <i>"설정창이 자꾸 혼자 꺼진다 = 고장"</i>.
    ///
    /// <para><b>판정: 되돌린다.</b> 우리는 그 창을 <b>닫은 게 아니라 빼앗았다</b>. 빼앗은 것을
    /// 돌려주는 것은 "부르지 않은 창을 띄우는 것"이 아니라 되돌리기다. 이 앱은 같은 판단을 이미
    /// 두 번 했다(톱니의 자동 복귀 / 설정창의 정보창 시트 복귀 M8). 게다가 이 창의 마우스 재진입은
    /// <b>3홉</b>(톱니 → 부채꼴 [캐릭터] → 정보창 [설정])이라 안 돌려주면 대가가 가장 크다.</para>
    ///
    /// ============================================================================
    /// 이 파일이 지키는 <b>경계 세 개</b>
    /// ============================================================================
    ///  ① 짧게 지나갔으면 <b>돌아온다</b>.
    ///  ② <b>아직 전체화면이면 절대 안 돌아온다</b>(원칙 2 — 이게 깨지면 이 기능은 폐기다).
    ///  ③ <b>사용자가 직접 닫았으면</b> 나중에 튀어나오지 않는다(빼앗은 게 아니므로 돌려줄 것도 없다).
    ///  ④ 오래 걸렸으면 포기한다 — 예전 판단(<i>"게임을 끄자마자 창이 튀어나오면 그 자체가 방해"</i>)은
    ///     <b>긴</b> 전체화면에 대해서는 여전히 옳다.
    ///
    /// <para><b>시간은 벽시계(초)로 잰다</b> — 이 저장소의 배치모드 PlayMode는 2,000fps 이상으로 돌아
    /// 프레임 수 기반 예산은 실제로 0.01초짜리가 된다(CLAUDE.md).</para>
    /// </summary>
    public sealed class SettingsReopenAfterSuspendTests
    {
        private const string LogPrefix = "[전체화면복귀-TEST]";

        /// <summary>관측 중 에이전트의 자체 폴링이 <c>Resume()</c>을 부르지 못하게 하는 값(초) —
        /// <see cref="FullscreenSuspendUiHidingTests"/>와 같은 수법, 같은 이유.</summary>
        private const float ObservePollInterval = 9999f;

        /// <summary>테스트용 유예. <b>프로덕션 20초를 진짜로 기다리지 않는다.</b></summary>
        private const float TestGraceSeconds = 0.30f;

        private static readonly FieldInfo SuspendedField =
            typeof(StickmanAgent).GetField("_isSuspended", BindingFlags.Instance | BindingFlags.NonPublic);

        private SettingsWindow _settings;
        private StickmanAgent _agent;
        private StickConfig _config;
        private float _savedPollInterval;

        private IEnumerator LoadAndOpen()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _settings = Object.FindFirstObjectByType<SettingsWindow>();
            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_settings, $"{LogPrefix} 씬에 SettingsWindow가 없습니다.");
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에 StickmanAgent가 없습니다.");
            Assert.IsNotNull(SuspendedField,
                $"{LogPrefix} StickmanAgent._isSuspended 필드를 찾지 못했습니다 — 이름이 바뀌었다면 " +
                "이 테스트도 함께 고쳐야 합니다(리플렉션 주입은 실제 소비 경로와 등가입니다).");

            _config = _agent.Config;
            if (_config != null)
            {
                _savedPollInterval = _config.fullscreenPollInterval;
                _config.fullscreenPollInterval = ObservePollInterval;
            }

            SettingsWindow.SetReopenGraceForTests(TestGraceSeconds);

            _settings.Open("테스트");
            yield return null;
            Assume.That(_settings.IsOpen, Is.True, $"{LogPrefix} 전제: 설정창이 열려 있어야 합니다.");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            SetSuspended(false);
            SettingsWindow.ResetReopenGraceForTests();
            if (_config != null) _config.fullscreenPollInterval = _savedPollInterval;
            if (_settings != null && _settings.IsOpen) _settings.Close("테스트 정리");
            _settings = null; _agent = null; _config = null;
            AppSettingsModel.ResetForTesting();
            yield return null;
        }

        private void SetSuspended(bool on)
        {
            if (_agent == null || SuspendedField == null) return;
            SuspendedField.SetValue(_agent, on);
        }

        /// <summary>벽시계 기준으로 <paramref name="seconds"/>초 동안 프레임을 돌린다.</summary>
        private static IEnumerator WaitWallClock(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        // ==================== ① 짧게 지나가면 돌아온다 ====================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ShortFullscreenTakesTheWindowAndGivesItBack()
        {
            yield return LoadAndOpen();

            SetSuspended(true);
            yield return null;
            yield return null;

            Assert.IsFalse(_settings.IsOpen,
                $"{LogPrefix} 전체화면이 감지됐는데 설정창이 남아 있습니다 — 원칙 2 위반입니다.");
            Assert.IsTrue(_settings.IsReopenAfterSuspendArmed,
                $"{LogPrefix} 창을 빼앗아 놓고 <b>돌려줄 예약</b>을 걸지 않았습니다. 예약이 없으면 " +
                "사용자에게는 '창이 혼자 꺼졌고 흔적도 없다'로만 보입니다.");

            SetSuspended(false);
            yield return null;
            yield return null;

            Assert.IsTrue(_settings.IsOpen,
                $"{LogPrefix} 전체화면이 지나갔는데 창이 돌아오지 않았습니다. 이 창의 마우스 재진입은 " +
                "3홉(톱니 → 부채꼴 [캐릭터] → 정보창 [설정])이라, 안 돌려주면 사용자는 처음부터 " +
                "다시 걸어야 합니다.");
            Assert.IsFalse(_settings.IsReopenAfterSuspendArmed,
                $"{LogPrefix} 돌려준 뒤에도 예약이 남았습니다 — 다음 닫기에서 유령처럼 다시 뜹니다.");
        }

        // ==================== ② 원칙 2 — 아직 전체화면이면 절대 안 돌아온다 ====================

        /// <summary>★ 이 단언이 깨지면 기능 자체를 폐기해야 한다. 게임 위에 방금 치운 창을 다시
        /// 얹는 것은 자동 숨김의 <b>목적을 뒤집는</b> 일이다.</summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator NeverComesBackWhileTheGameIsStillFullscreen()
        {
            yield return LoadAndOpen();

            SetSuspended(true);
            yield return null;
            yield return null;
            Assume.That(_settings.IsOpen, Is.False);
            Assume.That(_settings.IsReopenAfterSuspendArmed, Is.True);

            // 예약이 걸린 채로 <b>유예를 훌쩍 넘겨</b> 전체화면을 유지한다.
            yield return WaitWallClock(TestGraceSeconds * 3f);

            Assert.IsFalse(_settings.IsOpen,
                $"{LogPrefix} 아직 전체화면인데 설정창이 되살아났습니다 — 절대 불변 원칙 2 위반입니다. " +
                "복귀 조건은 문자열 사유가 아니라 IsSuspended <b>상태</b>여야 합니다.");
        }

        // ==================== ③ 사용자가 닫은 창은 되살리지 않는다 ====================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator UserClosedWindowIsNotResurrectedByAPassingFullscreen()
        {
            yield return LoadAndOpen();

            _settings.Close("테스트 — 사용자가 [✕]를 눌렀다");
            yield return null;
            Assert.IsFalse(_settings.IsReopenAfterSuspendArmed,
                $"{LogPrefix} 사용자가 닫았는데 복귀 예약이 남았습니다.");

            SetSuspended(true);
            yield return null;
            SetSuspended(false);
            yield return null;
            yield return null;

            Assert.IsFalse(_settings.IsOpen,
                $"{LogPrefix} 사용자가 스스로 닫은 창이 전체화면이 지나가자 튀어나왔습니다 — " +
                "빼앗은 적이 없으므로 돌려줄 것도 없습니다. 이건 복구가 아니라 새 방해입니다.");
        }

        // ==================== ④ 오래 걸렸으면 포기한다 ====================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator LongFullscreenSessionDoesNotPopTheWindowBackMuchLater()
        {
            yield return LoadAndOpen();

            SetSuspended(true);
            yield return null;
            yield return null;
            Assume.That(_settings.IsOpen, Is.False);

            // 유예보다 <b>확실히</b> 길게 전체화면을 유지한다(벽시계 기준).
            yield return WaitWallClock(TestGraceSeconds * 2.5f);

            SetSuspended(false);
            yield return null;
            yield return null;

            Assert.IsFalse(_settings.IsOpen,
                $"{LogPrefix} 유예({SettingsWindow.ReopenAfterSuspendGraceSeconds:F2}초)를 훌쩍 넘겨 " +
                "게임을 하고 나왔는데 창이 뒤늦게 튀어나왔습니다 — 그만큼 지났으면 사용자는 다른 일을 " +
                "하고 있고, 그때의 복귀는 되돌리기가 아니라 방해입니다(예전 판단이 옳았던 구간).");
            Assert.IsFalse(_settings.IsReopenAfterSuspendArmed,
                $"{LogPrefix} 포기한 뒤에도 예약이 남았습니다.");
        }

        /// <summary>★ 네거티브 컨트롤 — 유예가 실제로 <b>재는 값</b>인가. 테스트가 낮춘 값이
        /// 프로덕션 기본값과 같다면 위 ④는 20초를 기다린 적이 없으므로 아무것도 증명하지 못한다.</summary>
        [Test]
        public void NegativeControl_TestGraceIsActuallyShorterThanProduction()
        {
            SettingsWindow.SetReopenGraceForTests(TestGraceSeconds);
            try
            {
                Assert.Less(SettingsWindow.ReopenAfterSuspendGraceSeconds,
                    SettingsWindow.DefaultReopenAfterSuspendGraceSeconds,
                    $"{LogPrefix} 테스트 유예가 프로덕션 기본값을 낮추지 못했습니다 — 그러면 ④는 " +
                    "'유예를 넘겼다'를 한 번도 재현하지 못한 채 초록이 됩니다.");
            }
            finally { SettingsWindow.ResetReopenGraceForTests(); }

            Assert.AreEqual(SettingsWindow.DefaultReopenAfterSuspendGraceSeconds,
                SettingsWindow.ReopenAfterSuspendGraceSeconds, 1e-4f,
                $"{LogPrefix} 되돌리기가 동작하지 않아 정적 상태가 다음 테스트로 샙니다.");
        }
    }
}
