using System.IO;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// 절대 불변 원칙 2(비침해) 회귀 잠금 — <b>이 앱은 사용자의 디스플레이 절전을 막아서는 안 된다.</b>
    ///
    /// <para>배경(2026-08-31 성능 라운드 / R5 Major 2): Unity 플레이어는 <c>Screen.sleepTimeout</c>
    /// 기본값이 <c>NeverSleep</c>이라 시작 시 <c>PreventUserIdleDisplaySleep</c> IOPM 어서션을 걸고
    /// 앱이 사는 동안 유지한다. 24시간 상주 장식 앱에서는 "사용자가 자리를 비워도 화면이 영영 꺼지지
    /// 않는" 침해다. <see cref="FramePacing.ApplyDisplaySleepPolicy"/>가 이를 해제한다.</para>
    ///
    /// <para><b>왜 이 테스트가 필요했나</b>: 그 수정은 <c>ApplyOnce</c> 안에 있고, <c>ApplyOnce</c>는
    /// 에디터/테스트에서 절대 실행되지 않도록 의도적으로 설계돼 있다. 그래서 R5 검증 시점에는 그 줄을
    /// 통째로 지워도 EditMode 174건 + PlayMode 313건이 전부 초록불이었다. 여기서 메서드를 직접 불러
    /// 잠근다.</para>
    ///
    /// <para><b>범위의 정직한 한계</b>: OS 레벨 실증(<c>pmset -g assertions</c>가 비어 있는지)은 빌드된
    /// .app 실행이 필요해 배치 테스트 밖이다(perf-doc 라운드의 5회 반복 실측이 그쪽 근거). 이 테스트가
    /// 잠그는 것은 "정책이 올바른 값을 실제로 설정하는가"와 "그 정책이 시작 경로에 배선돼 있는가"다.</para>
    /// </summary>
    public class DisplaySleepPolicyTests
    {
        [Test]
        public void 디스플레이슬립정책은_sleepTimeout을_시스템설정으로_되돌린다()
        {
            int original = Screen.sleepTimeout;
            try
            {
                // 네거티브 컨트롤: 호출 전에는 침해 상태(NeverSleep)여야 아래 단언이 실제로 갈린다.
                // 이 전제가 깨지면(플랫폼이 값을 안 받아주면) 아래 단언은 "항상 참"이 되므로 여기서 막는다.
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
                Assert.AreEqual(SleepTimeout.NeverSleep, Screen.sleepTimeout,
                    "네거티브 컨트롤 전제 실패 — 이 환경이 Screen.sleepTimeout 쓰기를 반영하지 않으면 " +
                    "본 단언이 무의미해진다.");

                FramePacing.ApplyDisplaySleepPolicy();

                Assert.AreEqual(SleepTimeout.SystemSetting, Screen.sleepTimeout,
                    "원칙 2 위반 — 디스플레이 절전 정책이 시스템 설정으로 복원되지 않았다.");
            }
            finally
            {
                Screen.sleepTimeout = original;
            }
        }

        [Test]
        public void 디스플레이슬립정책은_앱_시작경로에_배선돼_있다()
        {
            // 위 테스트는 메서드를 직접 부르므로, 누군가 ApplyOnce에서 호출만 빼도 통과해 버린다.
            // 배선 자체는 정적 스캔으로 잠근다(실제 플레이어 시작 경로는 배치 테스트에서 실행 불가).
            string path = Path.Combine(Application.dataPath,
                "_Project", "Scripts", "Platform", "FramePacing.cs");
            Assert.IsTrue(File.Exists(path), $"소스를 찾지 못했다: {path}");

            string source = File.ReadAllText(path);
            int applyOnce = source.IndexOf("static void ApplyOnce(", System.StringComparison.Ordinal);
            Assert.Greater(applyOnce, 0, "ApplyOnce가 사라졌거나 이름이 바뀌었다 — 이 테스트를 갱신하라.");

            int callSite = source.IndexOf("ApplyDisplaySleepPolicy();", applyOnce, System.StringComparison.Ordinal);
            Assert.Greater(callSite, applyOnce,
                "ApplyOnce가 ApplyDisplaySleepPolicy()를 더 이상 호출하지 않는다 — 원칙 2 위반 수정이 " +
                "배선에서 빠졌다.");
        }
    }
}
