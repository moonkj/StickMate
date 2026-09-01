using NUnit.Framework;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ (다) 개발 전용 격리 게이트 회귀 — docs/UX_FLOW.md <b>36-2</b>.
    ///
    /// 이 게이트가 새면 하드웨어 반응 미리보기 / 스트레스 게이지 순환 / 할일 알림 데모 / 집중 모드
    /// 90초 데모 / 진단 로그 / 가출 강제 발동이 사용자에게 도달한다 — 전부 "표시된 것과 실제가 다르다"를
    /// 만드는 경로라 CLAUDE.md 원칙 1의 직접 위반이다.
    ///
    /// <b>왜 <see cref="StickMateDevTools.ResolveFromEnvironmentValue"/>를 따로 테스트하는가</b>:
    /// 에디터에서는 <c>UNITY_EDITOR</c> 때문에 <see cref="StickMateDevTools.Enabled"/>가 언제나 true다.
    /// 즉 <c>Enabled</c>만 봐서는 "릴리스 빌드에서 환경변수 규칙이 맞는가"를 <b>구조적으로 확인할 수
    /// 없다</b>. 그래서 판정 규칙을 순수 함수로 떼어내 그것만 직접 잠근다.
    /// </summary>
    public sealed class StickMateDevToolsGateTests
    {
        [TearDown]
        public void TearDown() => StickMateDevTools.SetTestOverride(null);

        /// <summary>릴리스 빌드에서도 팀이 Player.log로 검증할 수 있어야 한다 — 이 이름이 바뀌면
        /// 그동안 쓰던 검증 절차가 조용히 죽는다.</summary>
        [Test]
        public void 환경변수_이름은_STICKMATE_DEVTOOLS다()
        {
            Assert.AreEqual("STICKMATE_DEVTOOLS", StickMateDevTools.EnvironmentVariableName,
                "환경변수 이름이 바뀌었습니다 — 릴리스 빌드 검증 절차(36-2 규칙 2)가 이 이름에 걸려 있습니다.");
        }

        [Test]
        public void 참으로_읽는_값들()
        {
            Assert.IsTrue(StickMateDevTools.ResolveFromEnvironmentValue("1"), "\"1\"이 켜짐으로 읽히지 않았습니다.");
            Assert.IsTrue(StickMateDevTools.ResolveFromEnvironmentValue("true"));
            Assert.IsTrue(StickMateDevTools.ResolveFromEnvironmentValue("TRUE"), "대소문자를 구분하고 있습니다.");
            Assert.IsTrue(StickMateDevTools.ResolveFromEnvironmentValue("on"));
            Assert.IsTrue(StickMateDevTools.ResolveFromEnvironmentValue("yes"));
            Assert.IsTrue(StickMateDevTools.ResolveFromEnvironmentValue(" 1 "), "앞뒤 공백이 제거되지 않았습니다.");
        }

        /// <summary>★ 기본값은 <b>닫힘</b>이다. 이 테스트가 이 라운드의 핵심 안전망이다 —
        /// 미설정/빈 문자열/"0"/"false"가 하나라도 열림으로 읽히면 릴리스 빌드에 개발 경로가 산다.</summary>
        [Test]
        public void 미설정이거나_거짓이면_닫힌다()
        {
            Assert.IsFalse(StickMateDevTools.ResolveFromEnvironmentValue(null),
                "환경변수 미설정이 켜짐으로 읽혔습니다 — 릴리스 빌드에 개발 전용 경로가 그대로 삽니다.");
            Assert.IsFalse(StickMateDevTools.ResolveFromEnvironmentValue(string.Empty));
            Assert.IsFalse(StickMateDevTools.ResolveFromEnvironmentValue("   "));
            Assert.IsFalse(StickMateDevTools.ResolveFromEnvironmentValue("0"));
            Assert.IsFalse(StickMateDevTools.ResolveFromEnvironmentValue("false"));
            Assert.IsFalse(StickMateDevTools.ResolveFromEnvironmentValue("no"));
            Assert.IsFalse(StickMateDevTools.ResolveFromEnvironmentValue("off"));
            Assert.IsFalse(StickMateDevTools.ResolveFromEnvironmentValue("2"));
            Assert.IsFalse(StickMateDevTools.ResolveFromEnvironmentValue("아무거나"));
        }

        /// <summary>테스트 강제가 실제로 <see cref="StickMateDevTools.Enabled"/>를 뒤집는다 —
        /// 이 훅이 없으면 "게이트가 닫힌 빌드"의 동작을 에디터에서 한 번도 재현할 수 없다.</summary>
        [Test]
        public void 테스트_강제로_게이트를_양방향으로_뒤집을_수_있다()
        {
            StickMateDevTools.SetTestOverride(false);
            Assert.IsFalse(StickMateDevTools.Enabled, "강제 OFF가 먹지 않았습니다 — 닫힌 빌드를 재현할 수 없습니다.");

            StickMateDevTools.SetTestOverride(true);
            Assert.IsTrue(StickMateDevTools.Enabled, "강제 ON이 먹지 않았습니다.");

            StickMateDevTools.SetTestOverride(null);
            Assert.IsTrue(StickMateDevTools.Enabled,
                "강제를 해제했는데 에디터에서 게이트가 닫혀 있습니다 — UNITY_EDITOR 경로가 깨졌습니다.");
        }

        /// <summary>왜 열렸는지/닫혔는지가 로그에 남아야 한다(시작 배너가 이 값을 그대로 인쇄한다).</summary>
        [Test]
        public void 게이트_사유_문자열이_비어_있지_않다()
        {
            Assert.IsNotEmpty(StickMateDevTools.SourceLabel, "게이트 사유가 비어 있어 로그로 원인을 알 수 없습니다.");

            StickMateDevTools.SetTestOverride(false);
            Assert.IsNotEmpty(StickMateDevTools.SourceLabel);
        }
    }
}
