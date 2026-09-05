using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★ 「대기 톱니」 게이트의 <b>런타임 거동</b> — 2026-09-05 신설(test-engineer).
    ///
    /// ============================================================================
    /// 왜 이 파일이 필요한가 — <b>지금 이 조건은 아무도 실행으로 재지 않았다</b>
    /// ============================================================================
    /// 같은 날 톱니가 <b>「평상시 숨김 / 캐릭터가 화면에서 사라진 동안에만 표시」</b>로 바뀌었다.
    /// 그런데 이 어셈블리의 톱니 픽스처 12개는 전부 <b>「톱니는 항상 보인다」</b>를 전제로 쓰였고,
    /// 그 60여 건이 한꺼번에 빨개지는 것을 막으려고 <c>GlobalPlayModeTestIsolation</c>이
    /// <c>InfoGearIconWidget.SetStandbyGateBypassedForTests(true)</c> <b>한 줄</b>로 게이트를 우회한다.
    ///
    /// <para>그 결과 <b>지금 이 어셈블리 안에서는 게이트가 한 번도 참여하지 않는다</b>. 조건식 자체는
    /// EditMode(<c>RightClickFanGateTests.대기_톱니는_사용자숨김과_가출_두_상태를_본다</c>)가 <b>소스</b>로
    /// 잠그지만, 그것은 «그 이름이 파일에 적혀 있다»까지다 — <b>실제로 화면에 나타나고 사라지는가</b>는
    /// 소스 감사가 구조적으로 못 본다(배치 경로·<c>SyncStandbyVisibility</c>·히트 사각형·차단막이
    /// 전부 그 뒤에 있다).</para>
    ///
    /// <para><b>그래서 이 픽스처만 우회를 스스로 끈다.</b> <c>SetUp</c>에서 <c>false</c>로 놓고
    /// <c>TearDown</c>에서 다시 <c>true</c>로 돌려놓는다(<c>InfoGearIconWidget</c>의 훅 문서가 요구하는
    /// 그대로다). 기존 60여 건은 한 글자도 건드리지 않는다.</para>
    ///
    /// ============================================================================
    /// 무엇을 재는가 — <b>플래그가 아니라 실물 3종</b>
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>판정</b> — <see cref="InfoGearIconWidget.IsStandbyGearVisible"/></item>
    ///   <item><b>그림</b> — <see cref="InfoGearIconWidget.IsIconVisible"/>(GameObject의 실제 활성 상태)</item>
    ///   <item><b>히트 영역 + 차단막</b> — <see cref="InfoGearIconWidget.IconScreenRect"/> 넓이와
    ///         <see cref="InfoGearIconWidget.IsClickBlockerEnabled"/></item>
    /// </list>
    /// <b>셋을 함께 보는 이유</b>: 그림만 끄고 사각형·차단막을 남기면 «보이지 않는데 클릭만 먹는»
    /// 최악의 형태가 되고(절대 불변 원칙 2), 그 자리는 화면 우상단이라 사용자는 원인을 절대 못 찾는다.
    /// 가시성 하나만 재는 테스트는 그 상태를 <b>초록으로 통과시킨다</b>(내 F-표의 FP-7과 같은 형태다).
    ///
    /// <para><b>시간 예산은 전부 벽시계(초)</b>다 — 이 저장소의 배치모드 PlayMode는 2,000fps 이상으로
    /// 돌아서 프레임 수 기반 대기는 실제로 0.0x초밖에 안 된다(CLAUDE.md).</para>
    /// </summary>
    public sealed class StandbyGearVisibilityTests
    {
        private const string LogPrefix = "[대기톱니-TEST]";

        /// <summary>LateUpdate가 한 바퀴 돌고 배치(PlaceOnScreen → SyncStandbyVisibility)까지 반영되는 데
        /// 필요한 여유(초). 한 프레임이면 논리상 충분하지만, 표면마다 단계가 달라 여유를 준다
        /// (<c>ManualHideUserAxisTests</c>와 같은 사정·같은 값).</summary>
        private const float SettleSeconds = 0.2f;

        private StickmanAgent _agent;
        private InfoGearIconWidget _gear;
        private RunawayDirector _runaway;

        // ==================== 준비 / 정리 ====================

        /// <summary>
        /// ★ <b>우회가 켜져 있는 세상에서 출발했다</b>는 것을 먼저 못박는다.
        ///
        /// <para>이 단언이 없으면 아래 «평상시에는 안 보인다»가 <b>«게이트가 원래 없었다»</b>와
        /// 구별되지 않는다. <c>GlobalPlayModeTestIsolation</c>이 <c>[OneTimeSetUp]</c>에서 이 어셈블리의
        /// 모든 테스트보다 먼저 <c>true</c>로 놓으므로, 여기서 <c>true</c>가 아니면 격리 자체가 안 돈
        /// 것이다.</para>
        /// </summary>
        [OneTimeSetUp]
        public void RequireBypassWasOnBeforeWeTurnItOff()
        {
            Assert.IsTrue(InfoGearIconWidget.IsStandbyGateBypassedForTests,
                $"{LogPrefix} 이 픽스처가 시작하는 시점에 「대기 톱니」 게이트 우회가 꺼져 있습니다 — " +
                "GlobalPlayModeTestIsolation이 돌지 않았다는 뜻이고, 그러면 이 픽스처가 «우회를 끄는 " +
                "유일한 자리»라는 전제가 무너집니다(다른 곳이 이미 껐다면 기존 60여 건이 다른 이유로 " +
                "빨개지고 있을 것입니다).");

            Assert.IsTrue(CharacterSaveStore.IsRedirectedForTesting,
                $"{LogPrefix} 저장 경로가 격리되지 않았습니다 — 이대로 진행하면 개발자의 실제 저장 " +
                "파일을 읽고 씁니다(절대 불변 원칙 3).");
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
        }

        /// <summary>★ <b>정적 우회는 도메인을 넘어 살아남는다</b> — 이 픽스처가 어떤 경로로 죽어도
        /// 다음 픽스처는 반드시 «우회 켜짐» 세상을 물려받아야 한다. 그래서 되돌리는 자리가 두 겹이다
        /// (<see cref="RestoreBypassAndCleanScene"/> + 이 <c>[OneTimeTearDown]</c>).</summary>
        [OneTimeTearDown]
        public void ForceRestoreBypassForTheRestOfTheAssembly()
        {
            InfoGearIconWidget.SetStandbyGateBypassedForTests(true);
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
        }

        [SetUp]
        public void TurnBypassOff()
        {
            // ★ 여기서부터 이 픽스처는 «진짜 게이트»가 도는 세상에 있다.
            InfoGearIconWidget.SetStandbyGateBypassedForTests(false);
        }

        [UnityTearDown]
        public IEnumerator RestoreBypassAndCleanScene()
        {
            // 순서가 중요하다: 씬 상태(숨김/가출)를 먼저 풀어야 다음 케이스가 그것을 물려받지 않는다.
            if (_agent != null) _agent.SetUserHidden(false, "대기 톱니 테스트 정리");
            if (_agent != null && _agent.Blackboard != null && _agent.Blackboard.Machine != null
                && _agent.Blackboard.Machine.CurrentStateId == StickmanStateId.Runaway)
            {
                _agent.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            }
            if (_runaway != null) SpectacleEventLock.Release(_runaway);

            InfoGearIconWidget.SetStandbyGateBypassedForTests(true);

            _agent = null;
            _gear = null;
            _runaway = null;
            yield return null;
        }

        private IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에 StickmanAgent가 없습니다.");

            // ★ FindObjectsInactive.Include — 대기 톱니는 «평상시 GameObject가 꺼져 있는» 상태가
            //   정상이다. 기본 탐색(활성만)으로는 이 위젯 자체를 못 찾는 경우가 생긴다.
            _gear = Object.FindFirstObjectByType<InfoGearIconWidget>(FindObjectsInactive.Include);
            Assert.IsNotNull(_gear, $"{LogPrefix} 씬에 InfoGearIconWidget이 없습니다.");

            _runaway = Object.FindFirstObjectByType<RunawayDirector>(FindObjectsInactive.Include);
            Assert.IsNotNull(_runaway, $"{LogPrefix} 씬에 RunawayDirector가 없습니다.");

            Assert.IsFalse(_agent.IsUserHidden, $"{LogPrefix} 새 씬인데 이미 사용자 숨김 상태입니다.");
            Assert.IsFalse(_agent.IsSuspended, $"{LogPrefix} 새 씬인데 이미 Suspended 상태입니다.");

            yield return Wait(SettleSeconds);
        }

        private static IEnumerator Wait(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline) yield return null;
        }

        // ==================== 관측 도구 ====================

        private static float HitArea(Rect r) => Mathf.Max(0f, r.width) * Mathf.Max(0f, r.height);

        /// <summary>세 관측(판정 · 그림 · 히트영역+차단막)을 <b>한 문장</b>으로 찍는다 — 실패 메시지가
        /// «무엇이 갈라졌는가»를 그 자리에서 말하게 하려고.</summary>
        private string Describe()
        {
            Rect hit = _gear.IconScreenRect;
            return $"판정={_gear.IsStandbyGearVisible}, 그림={_gear.IsIconVisible}, " +
                   $"히트넓이={HitArea(hit):F1}px²({hit.width:F1}×{hit.height:F1}), " +
                   $"차단막={_gear.IsClickBlockerEnabled}, " +
                   $"사용자숨김단독={_agent.IsUserHiddenOnly}, 가출={_runaway.IsRunawayActive}, " +
                   $"게이트우회={InfoGearIconWidget.IsStandbyGateBypassedForTests}";
        }

        private void AssertGearAbsent(string phase)
        {
            Assert.IsFalse(_gear.IsStandbyGearVisible,
                $"{LogPrefix} [{phase}] 대기 톱니 «판정»이 참입니다 — {Describe()}");
            Assert.IsFalse(_gear.IsIconVisible,
                $"{LogPrefix} [{phase}] ★ 톱니 «그림»이 화면에 남아 있습니다 — 2026-09-05 사용자 지시" +
                $"(평소 진입점은 캐릭터 우클릭)의 회귀입니다. {Describe()}");
            Assert.AreEqual(0f, HitArea(_gear.IconScreenRect), 0.0001f,
                $"{LogPrefix} [{phase}] ★★ 그림은 없는데 «히트 사각형»이 넓이를 갖고 있습니다 — " +
                "«보이지 않는데 클릭만 먹는» 최악의 형태이고(절대 불변 원칙 2) 그 자리는 화면 우상단이라 " +
                $"사용자는 원인을 절대 못 찾습니다. {Describe()}");
            Assert.IsFalse(_gear.IsClickBlockerEnabled,
                $"{LogPrefix} [{phase}] ★★ 클릭관통 «차단막»이 켜져 있습니다 — 위와 같은 결함의 " +
                $"다른 절반입니다(차단막은 실제로 밑의 앱에서 클릭을 빼앗습니다). {Describe()}");
        }

        private void AssertGearPresent(string phase)
        {
            Assert.IsTrue(_gear.IsStandbyGearVisible,
                $"{LogPrefix} [{phase}] 대기 톱니 «판정»이 거짓입니다 — {Describe()}");
            Assert.IsTrue(_gear.IsIconVisible,
                $"{LogPrefix} [{phase}] ★ 톱니 «그림»이 나타나지 않았습니다 — 캐릭터를 우클릭할 몸이 " +
                $"없는 동안의 유일한 마우스 진입점이 0개가 됩니다. {Describe()}");
            Assert.Greater(HitArea(_gear.IconScreenRect), 0f,
                $"{LogPrefix} [{phase}] ★ 톱니가 보이는데 «히트 사각형»이 넓이 0입니다 — 그림만 있고 " +
                $"누를 수 없다는 뜻이라 «진입점이 있다»가 거짓이 됩니다. {Describe()}");
            Assert.IsTrue(_gear.IsClickBlockerEnabled,
                $"{LogPrefix} [{phase}] ★ 클릭관통 차단막이 꺼져 있습니다 — 톱니 위 클릭이 밑의 앱으로 " +
                $"새어 나가 «눌러도 아무 일이 없다»가 됩니다. {Describe()}");
        }

        // ==================== (a) 평상시 ====================

        /// <summary>
        /// ★ <b>평상시(캐릭터가 화면에 정상 표시)에는 톱니가 실제로 없다</b> — 그림도, 히트 영역도.
        ///
        /// <para><b>양성 대조를 같은 테스트 안에 넣는다</b>: 게이트 우회를 잠깐 켜서 <b>같은 관측 3종</b>이
        /// «보인다»를 실제로 낼 수 있는지 먼저 보인다. 그 대조가 없으면 위의 «안 보인다»는
        /// <b>«위젯이 아예 없다 / LateUpdate가 안 돈다 / 카메라를 못 잡았다»</b>와 구별되지 않는다
        /// (그 셋도 전부 «안 보인다»로 초록이 된다).</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 평상시에는_톱니가_그림도_히트영역도_없다()
        {
            yield return LoadScene();

            // ── 전제: 두 표시 조건이 모두 거짓인가 ──────────────────────────
            Assert.IsFalse(_agent.IsUserHiddenOnly,
                $"{LogPrefix} 전제 불성립 — 새 씬인데 사용자 명시 숨김입니다. {Describe()}");
            Assert.IsFalse(_runaway.IsRunawayActive,
                $"{LogPrefix} 전제 불성립 — 새 씬인데 이미 가출 중입니다. {Describe()}");
            Assert.IsFalse(_agent.HidesScreenSurfaces,
                $"{LogPrefix} 전제 불성립 — 새 씬인데 전체화면 감지(축 1)가 켜져 있습니다. 그 축은 " +
                "이 라운드가 한 비트도 바꾸지 않은 별개 경로라, 켜져 있으면 «게이트 때문에 사라졌다»와 " +
                $"«원칙 2 때문에 사라졌다»를 가를 수 없습니다. {Describe()}");

            // ── 본 검사 ────────────────────────────────────────────────────
            AssertGearAbsent("평상시");

            // ── ★ 양성 대조: 같은 관측이 «보인다»를 낼 수 있는가 ────────────
            InfoGearIconWidget.SetStandbyGateBypassedForTests(true);
            yield return Wait(SettleSeconds);
            AssertGearPresent("양성 대조(게이트 우회 켬)");
            Debug.Log($"{LogPrefix} 양성 대조 통과 — 이 픽스처의 관측 3종은 실제로 «보인다»를 냅니다. " +
                $"{Describe()}");

            // ── 되돌려서 다시 사라지는가(파괴가 아니라 게이트인가) ──────────
            InfoGearIconWidget.SetStandbyGateBypassedForTests(false);
            yield return Wait(SettleSeconds);
            AssertGearAbsent("우회 되돌린 뒤");

            Debug.Log($"{LogPrefix} (a) 확인 — 평상시 톱니 없음 / 우회 켬=보임 / 되돌림=없음. {Describe()}");
        }

        // ==================== (b) 사용자 명시 숨김 ====================

        /// <summary>
        /// ★ <b>사용자 명시 숨김(⌃⌥⌘K 상당)이면 대기 톱니가 실제로 나타난다</b>, 그리고
        /// <b>해제하면 다시 사라진다</b>.
        ///
        /// <para><b>트리거는 프로덕션 본체 그대로다</b> — 단축키 경로
        /// (<c>AppControlDirector.ToggleUserHide</c>)가 하는 일은 <c>_agent.ToggleUserHidden(source)</c>
        /// 한 줄이므로, 여기서 같은 함수를 부르는 것이 곧 그 키를 누르는 것이다. 키 서비스는 에디터에
        /// 없으므로(<c>_keyService == null</c>) 그 위층은 어차피 재현할 수 없고, 재현했다 해도 재는 것은
        /// 같은 한 줄이다.</para>
        ///
        /// <para><b>왜 되돌리는 절반이 같은 케이스에 있는가</b>: «나타난다»만 재면 <b>«한 번 나타나면
        /// 영영 안 사라진다»</b>는 구현도 통과한다. 그건 2026-09-05 변경의 정확한 반대(«평상시 숨김»이
        /// 첫 실행에만 성립)이고, 사용자는 그것을 «톱니가 다시 생겼다»로 신고하게 된다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 사용자_명시_숨김이면_대기_톱니가_나타나고_해제하면_사라진다()
        {
            yield return LoadScene();
            AssertGearAbsent("숨기기 전");

            bool hidden = _agent.ToggleUserHidden("테스트 — ⌃⌥⌘K 상당");
            Assert.IsTrue(hidden, $"{LogPrefix} 첫 토글이 숨김으로 가지 않았습니다 — 이 케이스의 트리거가 " +
                "성립하지 않습니다.");
            yield return Wait(SettleSeconds);

            // 게이트가 <b>읽는 그 값</b>을 먼저 확인한다. 이게 거짓이면 아래 실패는 게이트가 아니라
            // 축 분리(IsUserHiddenOnly)의 문제다 — 두 원인을 실패 메시지에서 갈라 준다.
            Assert.IsTrue(_agent.IsUserHiddenOnly,
                $"{LogPrefix} 숨겼는데 IsUserHiddenOnly가 거짓입니다 — 게이트가 읽는 값 자체가 서지 " +
                $"않았습니다(축 1/축 3이 섞였을 수 있습니다). {Describe()}");

            AssertGearPresent("사용자 명시 숨김 중");

            // ── 되돌리기 ────────────────────────────────────────────────────
            bool shown = _agent.ToggleUserHidden("테스트 — 같은 키 다시");
            Assert.IsFalse(shown, $"{LogPrefix} 두 번째 토글이 숨김을 풀지 않았습니다 — 탈출구가 없습니다.");
            yield return Wait(SettleSeconds);

            Assert.IsFalse(_agent.IsUserHiddenOnly, $"{LogPrefix} 숨김이 풀리지 않았습니다. {Describe()}");
            AssertGearAbsent("숨김 해제 뒤");

            Debug.Log($"{LogPrefix} (b) 확인 — 숨김에서 톱니 3종이 모두 서고, 해제하면 모두 내려갑니다.");
        }

        // ==================== (c) 가출 ====================

        /// <summary>
        /// ★★ <b>가출(Runaway) 중에도 대기 톱니가 나타난다</b> — ux-designer <b>P0-2</b>가 실제로
        /// 닫혔는지를 <b>소스가 아니라 실행</b>으로 확인한다.
        ///
        /// <para><b>왜 P0인가</b>: 가출은 <b>사용자가 만든 상태가 아니다</b>. 캐릭터가 스스로 숨고 최대
        /// <c>runawayAutoReturnSeconds</c>(5400초 = 90분) 동안 화면에 없다. 되돌리는 UI(행동창 [돌아와!])로
        /// 가는 마우스 문이 «캐릭터 우클릭»뿐이면 <b>우클릭할 몸이 없으므로 논리적으로 도달 불가능</b>하고,
        /// 그 구멍은 <b>macOS 전용</b>이다 — Windows는 트레이가 같은 일을 하지만 macOS에는
        /// <c>NSStatusItem</c>이 없다.</para>
        ///
        /// <para>★ <b>두 조건이 섞이지 않았음을 같은 케이스에서 못박는다</b>: 가출 중에
        /// <c>IsUserHiddenOnly</c>가 <b>거짓</b>임을 함께 단언한다. 이 줄이 없으면 «가출이 내부적으로
        /// 사용자 숨김 비트를 켜서» 통과한 경우와 구별되지 않고, 그러면 이 테스트는 P0-2가 아니라
        /// (b)를 한 번 더 재는 것이 된다.</para>
        ///
        /// <para><b>발동은 프로덕션 경로 그대로</b>(<c>RunawayDirector.TryForceRunawayNow</c>)다 —
        /// 상태를 직접 <c>ChangeState</c>로 밀어 넣으면 상호배제 락·은신처 좌표·진입 조건을 전부
        /// 건너뛰게 되어 «실제로 가출했을 때»와 다른 것을 재게 된다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 가출_중에도_대기_톱니가_나타나고_돌아오면_사라진다()
        {
            yield return LoadScene();
            AssertGearAbsent("가출 전");

            // 강제 발동의 진입 조건은 Idle/Walk다. 씬 로드 직후 상태는 낙하일 수 있으므로 맞춰 준다.
            StickmanStateId before = _agent.Blackboard.Machine.CurrentStateId;
            if (before != StickmanStateId.Idle && before != StickmanStateId.Walk)
            {
                _agent.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
                yield return Wait(SettleSeconds);
            }

            CommandAvailability availability = _runaway.GetForcedRunawayAvailability();
            Assert.IsTrue(availability.IsReady,
                $"{LogPrefix} 가출 강제 발동이 불가입니다(\"{availability.Reason}\") — 이 케이스의 트리거가 " +
                $"성립하지 않습니다. 진입 상태={_agent.Blackboard.Machine.CurrentStateId}, {Describe()}");

            Assert.IsTrue(_runaway.TryForceRunawayNow("대기 톱니 테스트"),
                $"{LogPrefix} 가출이 시작되지 않았습니다. {Describe()}");
            yield return Wait(SettleSeconds);

            Assert.IsTrue(_runaway.IsRunawayActive,
                $"{LogPrefix} 가출 상태가 아닙니다 — 게이트가 읽는 값 자체가 서지 않았습니다. {Describe()}");

            // ★ 두 조건이 섞이지 않았는가 — 이 줄이 이 케이스를 (b)와 다른 것으로 만든다.
            Assert.IsFalse(_agent.IsUserHiddenOnly,
                $"{LogPrefix} ★ 가출 중인데 «사용자 명시 숨김»까지 참입니다 — 그러면 이 케이스는 가출 " +
                "가지를 재는 것이 아니라 (b)를 한 번 더 재는 것이 됩니다(P0-2는 여전히 미검증). " +
                $"{Describe()}");

            AssertGearPresent("가출 중");

            // ── 돌아오면 사라지는가 ─────────────────────────────────────────
            //    자진 복귀는 최대 5400초라 실행으로 기다릴 수 없다. 상태를 되돌리는 것으로 «가출이
            //    끝난 세상»을 만든다 — 여기서 재는 것은 «복귀 연출»이 아니라 «게이트가 조건이
            //    사라지면 내려가는가»다.
            _agent.Blackboard.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            SpectacleEventLock.Release(_runaway);
            yield return Wait(SettleSeconds);

            Assert.IsFalse(_runaway.IsRunawayActive, $"{LogPrefix} 가출이 끝나지 않았습니다. {Describe()}");
            AssertGearAbsent("가출 종료 뒤");

            Debug.Log($"{LogPrefix} (c) 확인 — 가출 중 톱니 3종이 서고(P0-2 실행으로 확인), " +
                "돌아오면 모두 내려갑니다.");
        }
    }
}
