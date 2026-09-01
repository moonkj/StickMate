using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ PlayMode 테스트의 <b>시간 예산</b> 공용 도구 (2026-09-01 신설).
    ///
    /// <para><b>왜 만들었나 — 하루에 세 번 같은 함정에 빠졌다.</b> 이 저장소의 배치 모드
    /// (<c>-batchmode -nographics</c>) PlayMode는 렌더링이 없어 <b>2,000~9,000fps</b>로 돈다
    /// (실측 0.11~0.45ms/프레임). 그래서 <c>for (int i = 0; i &lt; N; i++) yield return null;</c>로 잡은
    /// "N프레임 예산"은 실제로는 밀리초 단위다:</para>
    /// <code>
    /// 프레임 수    실제 경과 시간(0.11ms/f ~ 0.45ms/f)
    ///     60       0.007초 ~ 0.027초
    ///    120       0.013초 ~ 0.054초
    ///    240       0.026초 ~ 0.108초
    ///    900       0.099초 ~ 0.405초
    /// </code>
    /// <para>검증 대상이 <b>초 단위</b>(등장/페이드 연출, 자율 상태 전이, 물리 정착, 폴링 주기,
    /// sway 같은 주기 애니메이션)라면 이 예산은 의도한 구간을 <b>단 한 번도 보지 못한다</b>.
    /// 그런데도 단언이 초록이면 그건 통과가 아니라 <b>거짓 통과</b>다 — 실제로
    /// <c>CornerHoverPanelTests</c>는 이 함정 때문에 10/10 결정적 실패를 "불안정한 테스트"로
    /// 네 라운드 동안 오진당했고, <c>CharacterVisualHalfWidthTests</c>의 900프레임 표본은
    /// 전부가 앱 시작 낙하 한 동작 안에 갇혀 있었다.</para>
    ///
    /// <para><b>규칙(CLAUDE.md 확정).</b> 시간 기반 연출/거동을 검증하는 PlayMode 테스트는 예산을
    /// <b>반드시 초</b>로 잡는다. 각 테스트가 프레임 루프를 직접 짜지 않도록 그 예산 코드를 여기
    /// 한 곳에 모았다. 이 규칙은 EditMode의 <c>FrameBudgetLintTests</c>가 소스 스캔으로 자동
    /// 감시한다(새 테스트가 같은 함정을 다시 파면 그쪽이 빨갛게 된다).</para>
    ///
    /// <para><b>어떤 시계를 쓰나.</b> 전부 <see cref="Time.unscaledDeltaTime"/> 누적 = <b>벽시계</b>다.
    /// 이 프로젝트의 UI 연출은 <c>unscaledDeltaTime</c>으로, 시뮬레이션은 <c>deltaTime</c>으로
    /// 굴러가는데 테스트가 <c>Time.timeScale</c>을 건드리는 곳이 하나도 없어(전수 확인) 둘이
    /// 같은 값이다. 벽시계를 고른 이유는 <b>연출이 멈춰도 예산은 반드시 끝나기</b> 때문이다
    /// (timeScale이 0으로 새면 게임 시간 예산은 영원히 안 끝나 테스트가 통째로 멈춘다).</para>
    /// </summary>
    public static class TestClock
    {
        /// <summary>배치 모드 프레임 시간의 실측 상한(초). 예산이 실제로 몇 프레임이었는지 로그에
        /// 남길 때만 쓴다 — 단언에는 쓰지 않는다.</summary>
        public const float MeasuredBatchFrameSecondsMax = 0.00045f;

        /// <summary>
        /// <paramref name="seconds"/>(벽시계) 동안 매 프레임 <paramref name="onFrame"/>을 부른다.
        /// 인자는 시작 시점부터의 경과 시간(초). <paramref name="onFrame"/>이 <c>false</c>를 돌려주면
        /// 그 자리에서 멈춘다(원하는 사건을 찾자마자 끝내고 싶을 때).
        /// </summary>
        /// <returns>실제로 흘린 시간과 프레임 수는 <paramref name="onFrame"/> 쪽에서 세면 된다.</returns>
        public static IEnumerator SampleForSeconds(float seconds, Func<float, bool> onFrame)
        {
            if (onFrame == null) throw new ArgumentNullException(nameof(onFrame));
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                if (!onFrame(elapsed)) yield break;
            }
        }

        /// <summary><paramref name="seconds"/>(벽시계) 동안 매 프레임 <paramref name="onFrame"/>을
        /// 부르고 끝까지 돈다(중간에 멈추지 않는 표본용).</summary>
        public static IEnumerator SampleForSeconds(float seconds, Action<float> onFrame)
        {
            if (onFrame == null) throw new ArgumentNullException(nameof(onFrame));
            return SampleForSeconds(seconds, t => { onFrame(t); return true; });
        }

        /// <summary>
        /// <paramref name="condition"/>이 참이 될 때까지 프레임을 흘린다. <paramref name="timeoutSeconds"/>
        /// 안에 참이 안 되면 <b>그 자리에서 실패</b>시킨다 — 조용히 다음 줄로 넘어가면 뒤따르는 단언이
        /// "왜 실패했는지 모르는" 형태로 깨지기 때문이다.
        /// </summary>
        /// <param name="what">실패 메시지에 들어갈 "무엇을 기다렸는지"(사람이 읽는 문장).</param>
        public static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, string what)
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            float elapsed = 0f;
            while (!condition())
            {
                if (elapsed >= timeoutSeconds)
                {
                    Assert.Fail($"[시간예산] {what} — {timeoutSeconds:F2}초(벽시계) 안에 일어나지 않았습니다. " +
                        "예산이 모자란 것인지 조건 자체가 영영 안 되는 회귀인지 프로덕션 타이밍 상수와 " +
                        "나란히 확인하세요.");
                }
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
        }

        /// <summary>
        /// 상태 머신이 <paramref name="id"/>에 들어가 <paramref name="holdSeconds"/> 동안 <b>머무를</b>
        /// 때까지 기다린다. 스쳐 지나가는 한 프레임을 "도착"으로 오인하지 않으려면 홀드가 필요하다
        /// (앱 시작 낙하 → 착지 → 무릎앉기 구간에는 Idle이 한두 프레임 튀는 자리가 있다).
        /// </summary>
        public static IEnumerator WaitForState(
            StickmanBlackboard bb, StickmanStateId id, float timeoutSeconds, float holdSeconds = 0.25f)
        {
            Assert.IsNotNull(bb, "[시간예산] WaitForState에 블랙보드가 null로 들어왔습니다.");
            float elapsed = 0f;
            float heldFor = 0f;
            StickmanStateId last = bb.Machine.CurrentStateId;

            while (true)
            {
                last = bb.Machine.CurrentStateId;
                if (last == id)
                {
                    if (heldFor >= holdSeconds) yield break;
                }
                else
                {
                    heldFor = 0f;
                }

                if (elapsed >= timeoutSeconds)
                {
                    Assert.Fail($"[시간예산] {timeoutSeconds:F2}초(벽시계) 안에 {id} 상태로 안정되지 " +
                        $"않았습니다 — 지금 상태는 {last}입니다.");
                }

                yield return null;
                float dt = Time.unscaledDeltaTime;
                elapsed += dt;
                if (bb.Machine.CurrentStateId == id) heldFor += dt; else heldFor = 0f;
            }
        }
    }
}
