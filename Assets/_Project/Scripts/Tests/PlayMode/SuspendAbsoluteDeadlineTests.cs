using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★ 전체화면 Suspend 동안 <b>절대 기한이 계속 흘러가던</b> 결함의 회귀 잠금(2026-09-02).
    ///
    /// ============================================================================
    /// 무엇이 틀렸었나 — 코드에 적힌 계약이 거짓이었다
    /// ============================================================================
    /// <c>StickmanAgent.Suspend()</c>의 주석은 이렇게 단언한다:
    /// <i>"진행 중이던 상태의 내부 타이머가 그대로 멈춰 있다가 Resume 이후 이어서 진행된다."</i>
    /// 그 문장은 <b>deltaTime 누적 타이머에만</b> 참이다(Tick을 건너뛰면 저절로 멈춘다).
    /// 그런데 <c>States/</c>·<c>Core/</c>를 통틀어 <b>단 하나</b>의 예외가 있다 —
    /// drop-through 유예 / 발 떼기 이송이 쓰는 <c>Time.time + duration</c> 절대 기한이다.
    /// 그것은 Tick과 무관하게 벽시계로 흘러가므로, 하강 도중 전체화면이 0.25초 창에 겹치면
    /// <b>Resume 시점에 둘 다 만료돼 그 하강이 조용히 무효</b>가 됐다.
    ///
    /// <para>피해 자체는 작다(자기회복 — Dock에 도로 착지한다). 고친 이유는 <b>문서화된 계약이
    /// 거짓이었기 때문</b>이다. 거짓 계약은 다음 사람이 그 위에 무언가를 더 얹는 순간 진짜 버그가 된다.</para>
    ///
    /// <para><b>왜 PlayMode인가</b>: 이 결함의 본질은 "숨어 있는 동안 <b>실제 시간이 흐른다</b>"이다.
    /// EditMode에서는 <c>Time.time</c>이 사실상 멈춰 있어 얼리기 전후가 같은 값이 되고,
    /// 그러면 이 테스트가 아무 것도 증명하지 못한다. 예산은 프레임 수가 아니라 <b>벽시계 초</b>로
    /// 잡는다(CLAUDE.md 협업 프로토콜 — 이 저장소의 배치모드 PlayMode는 2,000fps 이상으로 돈다).</para>
    /// </summary>
    public sealed class SuspendAbsoluteDeadlineTests
    {
        private const string LogPrefix = "[숨김기한-TEST]";

        /// <summary>유예보다 확실히 긴 "숨어 있는 시간". 유예 0.25초의 두 배 이상을 쓴다.</summary>
        private const float HiddenSeconds = 0.6f;

        private StickConfig _config;
        private const long DockHandle = -2L;
        private const float CarryVelocityX = 1.2f;

        private StickmanBlackboard NewBlackboard()
        {
            return new StickmanBlackboard { Config = _config };
        }

        private float IgnoreDuration => _config.hopDownDropThroughIgnoreDuration;

        [SetUp]
        public void SetUp()
        {
            // 프로덕션 기본값을 그대로 쓴다 — 유예 길이를 테스트가 숫자로 베끼지 않는다.
            _config = ScriptableObject.CreateInstance<StickConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null) Object.DestroyImmediate(_config);
            _config = null;
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — 얼리지 않으면 같은 시간이 지났을 때 창이 <b>반드시 죽어 있어야</b> 한다.
        /// 이게 없으면 아래 본 검증이 "언제나 살아 있다"는 오답과 구별되지 않는다.
        /// </summary>
        [UnityTest]
        public IEnumerator 네거티브_얼리지_않으면_유예는_그대로_만료된다()
        {
            StickmanBlackboard bb = NewBlackboard();
            bb.BeginDropThroughIgnore(DockHandle, IgnoreDuration);
            bb.BeginStepOffCarry(CarryVelocityX);
            Assert.IsTrue(bb.TryGetStepOffCarryVelocityX(out _),
                $"{LogPrefix} 전제 실패 — 무장 직후인데 이송이 살아 있지 않습니다.");

            Assert.Greater(HiddenSeconds, IgnoreDuration,
                $"{LogPrefix} 전제 실패 — 대기 {HiddenSeconds:F2}초가 유예 {IgnoreDuration:F2}초보다 " +
                "짧으면 만료를 관측할 수 없습니다.");

            yield return new WaitForSeconds(HiddenSeconds);

            Assert.AreEqual(0L, bb.DropThroughIgnoredFootholdHandle,
                $"{LogPrefix} 얼리지 않았는데도 {HiddenSeconds:F2}초 뒤에 drop-through 유예가 살아 있습니다 — " +
                "그렇다면 이 기한은 애초에 절대 기한이 아니고, 아래 본 검증도 아무 것도 증명하지 못합니다.");
            Assert.IsFalse(bb.TryGetStepOffCarryVelocityX(out _),
                $"{LogPrefix} 같은 이유로 이송도 만료되어 있어야 합니다.");
        }

        /// <summary>
        /// 본 검증 — 얼렸다 되살리면 <b>숨어 있던 시간만큼 창이 뒤로 밀린다</b>.
        /// </summary>
        [UnityTest]
        public IEnumerator 숨어_있는_동안에는_유예가_흐르지_않는다()
        {
            StickmanBlackboard bb = NewBlackboard();
            bb.BeginDropThroughIgnore(DockHandle, IgnoreDuration);
            bb.BeginStepOffCarry(CarryVelocityX);

            float hiddenAt = Time.time;
            bb.SuspendAbsoluteTimeWindows();

            // 숨어 있는 동안에는 창이 **닫힌 것으로** 읽혀야 한다 — 이 시점에 끼어드는 어떤 경로도
            // "만료되지 않은 척"이 아니라 수정 이전의 기본 거동(창 없음)을 보는 쪽이 항상 안전하다.
            Assert.AreEqual(0L, bb.DropThroughIgnoredFootholdHandle,
                $"{LogPrefix} 얼린 동안에도 유예가 열려 있습니다 — 물리가 멎은 채 창만 살아 있는 " +
                "중간 상태가 생깁니다.");

            yield return new WaitForSeconds(HiddenSeconds);
            float hidden = Time.time - hiddenAt;

            bb.ResumeAbsoluteTimeWindows();

            Assert.AreEqual(DockHandle, bb.DropThroughIgnoredFootholdHandle,
                $"{LogPrefix} ★회귀★ {hidden:F2}초 숨어 있었더니 유예(원래 {IgnoreDuration:F2}초)가 " +
                "만료됐습니다 — Suspend()가 단언하는 '타이머가 그대로 멈춰 있다'가 거짓이 됩니다.");
            Assert.IsTrue(bb.TryGetStepOffCarryVelocityX(out float carry),
                $"{LogPrefix} 같은 이유로 발 떼기 이송도 살아 있어야 합니다 — 이 둘은 같은 타이머를 씁니다.");
            Assert.AreEqual(CarryVelocityX, carry, 1e-4f,
                $"{LogPrefix} 되살아난 이송 속도가 무장값과 다릅니다.");

            Debug.Log($"{LogPrefix} 숨김 {hidden:F2}초(유예 {IgnoreDuration:F2}초) 뒤 재기점 — " +
                $"무시 핸들={bb.DropThroughIgnoredFootholdHandle}, 이송={carry:F3}유닛/초.");

            // 재기점된 창도 **유한**해야 한다. 영구히 살아 있으면 반대쪽 사고다.
            yield return new WaitForSeconds(HiddenSeconds);
            Assert.AreEqual(0L, bb.DropThroughIgnoredFootholdHandle,
                $"{LogPrefix} 재기점 뒤 {HiddenSeconds:F2}초가 더 지났는데도 유예가 살아 있습니다 — " +
                "창이 영구화됐습니다(원래 설계의 '시간이 지나면 스스로 풀린다'가 깨졌습니다).");
        }

        /// <summary>
        /// 숨기 직전에 이미 창이 닫혀 있었다면 Resume이 그것을 <b>되살리지 않는다</b>.
        /// (재기점이 "없던 창을 만드는" 방향으로 새는 것을 막는다.)
        /// </summary>
        [UnityTest]
        public IEnumerator 숨기_전에_이미_끝난_창은_되살아나지_않는다()
        {
            StickmanBlackboard bb = NewBlackboard();
            bb.BeginDropThroughIgnore(DockHandle, IgnoreDuration);
            bb.BeginStepOffCarry(CarryVelocityX);

            yield return new WaitForSeconds(HiddenSeconds);   // 숨기 전에 자연 만료시킨다.
            Assert.AreEqual(0L, bb.DropThroughIgnoredFootholdHandle,
                $"{LogPrefix} 전제 실패 — 창이 아직 안 끝났습니다.");

            bb.SuspendAbsoluteTimeWindows();
            yield return null;
            bb.ResumeAbsoluteTimeWindows();

            Assert.AreEqual(0L, bb.DropThroughIgnoredFootholdHandle,
                $"{LogPrefix} Resume이 이미 끝난 창을 되살렸습니다 — 재기점이 '없던 창을 만드는' " +
                "방향으로 새고 있습니다.");
            Assert.IsFalse(bb.TryGetStepOffCarryVelocityX(out _),
                $"{LogPrefix} 같은 이유로 이송도 되살아나면 안 됩니다.");
        }

        /// <summary>
        /// ★ 이송이 <b>직전 뛰어내리기의 유예 창을 물려받지 않는가</b>(2026-09-02).
        ///
        /// <para><c>BeginDropThroughIgnore</c>는 핸들 0 / 유예 0이면 <b>조기 return</b>이라 창을 열지
        /// 않는데, <c>BeginStepOffCarry</c>는 무조건 실행됐다. 그래서 "직전 창이 아직 살아 있는데
        /// 새 이송 속도만 갈아끼우는" 조합이 원리적으로 가능했다 — 창을 물려받는 형태다.</para>
        ///
        /// <para><b>정직한 한계</b>: 이 조합을 실기에서 관측한 적은 없고, 기본 설정에서는 도달하기
        /// 어렵다(발판 핸들 0은 이 프로젝트에서 "발판 없음" 관례값이라 접지가 성립하지 않고,
        /// 유예 0은 사용자가 설정을 0으로 내려야 한다). 그래도 <b>기전은 실재</b>하므로 구조로 막고
        /// 여기에 잠근다 — 시간 조건 하나에 기대는 설계가 아니라는 것이 요점이다.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator 창을_열지_못한_호출은_직전_유예_창을_물려받지_않는다()
        {
            StickmanBlackboard bb = NewBlackboard();
            bb.BeginDropThroughIgnore(DockHandle, IgnoreDuration);
            bb.BeginStepOffCarry(CarryVelocityX);
            Assert.IsTrue(bb.TryGetStepOffCarryVelocityX(out _),
                $"{LogPrefix} 전제 실패 — 정상 무장이 되지 않았습니다.");

            yield return null;   // 프레임을 넘긴다. 창은 아직(0.25초) 살아 있다.

            Assert.AreNotEqual(0L, bb.DropThroughIgnoredFootholdHandle,
                $"{LogPrefix} 검사 무효 — 한 프레임 만에 창이 닫혔습니다. 그러면 아래 단언이 " +
                "'물려받기 차단'이 아니라 '유예 만료'를 확인하는 공허한 검사가 됩니다.");

            // 창을 **열지 못하는** 호출(핸들 0 = 발판 없음) 뒤에 이송만 갈아끼운다.
            bb.BeginDropThroughIgnore(0L, IgnoreDuration);
            bb.BeginStepOffCarry(-9f);

            Assert.IsFalse(bb.TryGetStepOffCarryVelocityX(out float leaked),
                $"{LogPrefix} 창을 열지 못한 호출이 직전 창을 물려받아 이송 {leaked:F2}유닛/초가 살아났습니다 — " +
                "그 속도는 아무도 무장한 적 없는 값입니다.");

            // ★ 네거티브 컨트롤 — 창을 **제대로** 여는 호출이면 같은 자리에서 정상 무장된다.
            bb.BeginDropThroughIgnore(DockHandle, IgnoreDuration);
            bb.BeginStepOffCarry(-9f);
            Assert.IsTrue(bb.TryGetStepOffCarryVelocityX(out float rearmed),
                $"{LogPrefix} 차단이 과잉입니다 — 정상적인 재무장까지 막고 있습니다.");
            Assert.AreEqual(-9f, rearmed, 1e-4f);
        }

        /// <summary>
        /// ★ 배선 검사 — 위 두 메서드를 실제로 부르는 곳이 <c>StickmanAgent</c>의 Suspend/Resume인가.
        /// 이 파일의 나머지는 블랙보드를 <b>직접</b> 부르므로, 배선이 끊겨도 전부 초록이다.
        /// (전체화면 판정은 플랫폼 서비스에 물려 있어 PlayMode에서 합성하기 어렵다 — 그래서 구조를 본다.)
        /// </summary>
        [Test]
        public void 에이전트의_Suspend와_Resume이_절대기한을_얼리고_되살린다()
        {
            string src = System.IO.File.ReadAllText(System.IO.Path.Combine(
                Application.dataPath, "_Project", "Scripts", "Core", "StickmanAgent.cs"));

            var sb = new System.Text.StringBuilder(src.Length);
            foreach (string line in src.Replace("\r\n", "\n").Split('\n'))
            {
                int c = line.IndexOf("//", System.StringComparison.Ordinal);
                sb.Append(c >= 0 ? line.Substring(0, c) : line).Append('\n');
            }
            string exec = sb.ToString();

            int suspend = exec.IndexOf("private void Suspend()", System.StringComparison.Ordinal);
            int resume = exec.IndexOf("private void Resume()", System.StringComparison.Ordinal);
            Assert.Greater(suspend, 0, "Suspend()가 사라졌다 — 이 검사의 대상이 없다.");
            Assert.Greater(resume, suspend, "Resume()이 Suspend()보다 앞에 있다 — 아래 구간 계산이 깨진다.");

            string suspendBody = exec.Substring(suspend, resume - suspend);
            StringAssert.Contains("SuspendAbsoluteTimeWindows()", suspendBody,
                "Suspend()가 절대 기한을 얼리지 않는다 — 숨어 있는 동안 유예가 계속 흘러가고, " +
                "그 클래스의 '타이머가 그대로 멈춰 있다'는 주석이 다시 거짓이 된다.");
            StringAssert.Contains("ResumeAbsoluteTimeWindows()", exec.Substring(resume),
                "Resume()이 얼린 기한을 되살리지 않는다 — 한 번 숨으면 유예가 영영 닫힌 채로 남는다.");
        }
    }
}
