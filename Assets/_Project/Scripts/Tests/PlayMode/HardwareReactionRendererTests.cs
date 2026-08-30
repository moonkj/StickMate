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
    /// ★ PC 하드웨어 반응(docs/UX_FLOW.md 23 / 27-6) 시각 레이어 회귀 테스트.
    ///
    /// 창 도둑/창 크래시 테스트와 같은 이유로 <b>Main.unity를 실제로 로드해서</b> 검사한다 —
    /// HardwareReactionDirector도 이번 라운드 전까지 씬 어디에도 배치돼 있지 않아 Update()가 단 한 번도
    /// 돌지 않았고(폴링 자체가 없었고), HardwareReactionChanged 구독자도 0명이었다.
    ///
    /// ============================================================================
    /// 절대 조건으로 단언하는 것
    /// ============================================================================
    ///  ① 씬에 HardwareReactionDirector / HardwareReactionRenderer가 <b>정확히 1개씩</b> 있다.
    ///  ② 4종(배터리/CPU/네트워크/충전) <b>전부</b> Active=true에서 실제로 오브젝트를 만들고
    ///     Active=false에서 하나도 남기지 않는다 — 한 종류만 통과하고 나머지가 빈 껍데기인 상황을 막는다.
    ///  ③ 이모트는 콜라이더를 <b>정확히 0개</b> 만든다(관전 전용 = 클릭관통 유지).
    ///  ④ 23절 "동시에 두 가지 다른 표정/자세를 겹쳐 보이면 안 됨" — 다른 종류가 Active=true로 들어오면
    ///     이전 이모트가 <b>남아 있지 않고 교체</b>된다(컨테이너가 정확히 1개만 존재).
    ///
    /// SpectacleEventLock에 참여하지 않는 것이 정상이라는 점도 여기서 함께 확인한다(Phase 4 설계 결정 5):
    /// 이 테스트는 락을 전혀 잡지 않은 상태에서 이모트가 정상적으로 떠야 통과한다.
    /// </summary>
    public sealed class HardwareReactionRendererTests
    {
        private const string ContainerName = "HardwareReactionEmote";

        private static readonly HardwareReactionKind[] AllKinds =
        {
            HardwareReactionKind.LowBattery,
            HardwareReactionKind.HighCpu,
            HardwareReactionKind.NetworkDown,
            HardwareReactionKind.Charging,
        };

        private HardwareReactionRenderer _renderer;

        private IEnumerator LoadSceneAndResolve()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var directors = Object.FindObjectsByType<HardwareReactionDirector>(FindObjectsSortMode.None);
            Assert.AreEqual(1, directors.Length,
                $"씬의 HardwareReactionDirector 개수가 {directors.Length}개입니다 — 1개여야 합니다. " +
                "0개면 SceneBootstrapper 배치 누락(폴링 자체가 돌지 않는다), 2개 이상이면 씬에 중복 배치돼 " +
                "제거되지 않아 같은 신호가 두 번 판정됩니다.");

            var renderers = Object.FindObjectsByType<HardwareReactionRenderer>(FindObjectsSortMode.None);
            Assert.AreEqual(1, renderers.Length,
                $"씬의 HardwareReactionRenderer 개수가 {renderers.Length}개입니다 — 1개여야 합니다. " +
                "2개 이상이면 이모트가 한 벌 더 뜹니다.");

            _renderer = renderers[0];
            Assert.IsFalse(_renderer.IsVisible, "테스트 시작 시점에는 이모트가 떠 있으면 안 됩니다.");
            Assert.AreEqual(0, _renderer.ActiveVisualCount, "테스트 시작 시점의 시각 오브젝트는 0개여야 합니다.");
        }

        /// <summary>② + ③ — 4종 전부 실제로 그려지고, 전부 깨끗하게 정리된다.</summary>
        [UnityTest]
        public IEnumerator EveryReactionKindDrawsAndFullyCleansUp()
        {
            yield return LoadSceneAndResolve();

            for (int i = 0; i < AllKinds.Length; i++)
            {
                HardwareReactionKind kind = AllKinds[i];

                StickmanEventBus.RaiseHardwareReactionChanged(kind, active: true);

                Assert.IsTrue(_renderer.IsVisible,
                    $"HardwareReactionChanged({kind}, active:true)를 발행했는데 이모트가 나타나지 않았습니다.");
                Assert.AreEqual(kind, _renderer.VisibleKind,
                    $"표시 중인 반응 종류가 {_renderer.VisibleKind}로, 발행한 {kind}와 다릅니다.");
                Assert.Greater(_renderer.ActiveVisualCount, 0,
                    $"{kind} 이모트가 '보인다'고 보고하면서 실제 LineRenderer는 0개입니다(빈 껍데기) — " +
                    "종류별 도형 빌더가 아무것도 만들지 않았다는 뜻입니다.");
                Assert.AreEqual(0, _renderer.ActiveColliderCount,
                    $"{kind} 이모트가 콜라이더를 만들었습니다 — 관전 전용 연출이므로 클릭관통이 유지되어야 합니다.");

                yield return null;
                Assert.IsNotNull(GameObject.Find(ContainerName),
                    $"{kind}: '{ContainerName}' GameObject가 씬에 실존하지 않습니다.");

                int spawned = _renderer.ActiveVisualCount;

                StickmanEventBus.RaiseHardwareReactionChanged(kind, active: false);
                yield return new WaitForSeconds(0.8f); // FadeOutSeconds(0.40초) + 여유.

                Assert.IsFalse(_renderer.IsVisible, $"{kind}: active:false 후에도 이모트가 '보인다'고 보고합니다.");
                Assert.IsNull(_renderer.VisibleKind, $"{kind}: 정리 후에도 VisibleKind가 남아 있습니다.");
                Assert.AreEqual(0, _renderer.ActiveVisualCount,
                    $"{kind}: active:false 후에도 시각 오브젝트가 {_renderer.ActiveVisualCount}개 남아 있습니다(생성 시 {spawned}개).");
                Assert.IsNull(GameObject.Find(ContainerName),
                    $"{kind}: '{ContainerName}' GameObject가 씬에 그대로 남아 있습니다.");

                Debug.Log($"[하드웨어테스트] {kind} 검증 통과 — 시각 오브젝트 {spawned}개 생성 후 전부 제거, 콜라이더 0개.");
            }
        }

        /// <summary>④ 23절 "동시에 두 가지 표정을 겹쳐 보이면 안 됨" — 새 반응이 이전 것을 교체한다.</summary>
        [UnityTest]
        public IEnumerator NewReactionReplacesPreviousInsteadOfStacking()
        {
            yield return LoadSceneAndResolve();

            StickmanEventBus.RaiseHardwareReactionChanged(HardwareReactionKind.LowBattery, active: true);
            yield return null;
            Assert.AreEqual(HardwareReactionKind.LowBattery, _renderer.VisibleKind, "사전 조건이 성립하지 않습니다.");

            StickmanEventBus.RaiseHardwareReactionChanged(HardwareReactionKind.Charging, active: true);
            yield return null;

            Assert.AreEqual(HardwareReactionKind.Charging, _renderer.VisibleKind,
                "새 반응이 들어왔는데 이전 반응이 그대로 표시되고 있습니다.");

            var containers = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            int containerCount = 0;
            for (int i = 0; i < containers.Length; i++)
            {
                if (containers[i].name == ContainerName && containers[i].parent == null) containerCount++;
            }
            Assert.AreEqual(1, containerCount,
                $"'{ContainerName}' 컨테이너가 {containerCount}개 존재합니다 — 이전 이모트가 지워지지 않고 " +
                "새 이모트가 그 위에 겹쳐 그려졌다는 뜻입니다(23절 '동시에 두 가지 표정 금지' 위반).");

            StickmanEventBus.RaiseHardwareReactionChanged(HardwareReactionKind.Charging, active: false);
            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(0, _renderer.ActiveVisualCount, "교체 후 정리에서 오브젝트가 남았습니다.");
            Assert.IsNull(GameObject.Find(ContainerName), $"'{ContainerName}'가 씬에 그대로 남아 있습니다.");

            Debug.Log("[하드웨어테스트] 교체 검증 통과 — 컨테이너는 항상 1개, 종료 후 0개.");
        }

        /// <summary>컴포넌트가 꺼져도 이모트가 화면에 남지 않는다(OnDisable 정리 관례).</summary>
        [UnityTest]
        public IEnumerator DisablingRendererRemovesEveryObject()
        {
            yield return LoadSceneAndResolve();

            StickmanEventBus.RaiseHardwareReactionChanged(HardwareReactionKind.HighCpu, active: true);
            yield return null;
            Assert.Greater(_renderer.ActiveVisualCount, 0, "정리 검증의 사전 조건이 성립하지 않습니다.");

            _renderer.enabled = false;
            yield return null;

            Assert.AreEqual(0, _renderer.ActiveVisualCount,
                "렌더러를 비활성화했는데 이모트 오브젝트가 남아 있습니다 — 화면에 영구히 남습니다.");
            Assert.IsNull(GameObject.Find(ContainerName), $"'{ContainerName}'가 씬에 그대로 남아 있습니다.");

            Debug.Log("[하드웨어테스트] OnDisable 정리 검증 통과.");
        }

        // ============================================================================
        // ★ 자율 발동 마스터 게이트 (StickConfig.enableAutonomousHardwareReactions, 기본 false)
        // ============================================================================
        // 왜 이 잠금이 필요한가 — 사용자 실측 신고 2026-08-29:
        //   "머리위에 저 주황색이랑 눈같이 내리는건 뭐야 캐릭하고 겹치는데"
        // 주황색 물결 = CPU 과부하 이모트, 눈처럼 내리는 것 = 그 땀방울. 같은 라운드에 다른 구경거리
        // 연출은 자율 발동 **확률**을 0으로 내려 조용해졌지만, 하드웨어 반응만은 트리거가 확률이 아니라
        // 실제 배터리/프레임타임/네트워크/충전 상태라 0으로 내릴 확률 필드 자체가 없어 혼자 남아 계속 떴다.
        //
        // 아래 두 테스트는 **절대 조건**으로 단언한다:
        //   ⓐ 플래그가 false면 자율 트리거는 HardwareReactionChanged를 **정확히 0건** 발행한다.
        //   ⓑ 그 상태에서도 수동 발동(ForceTriggerNow = 단축키 Ctrl+Opt+Cmd+H / 우클릭 메뉴)은 뜬다.
        // 그리고 ⓒ 플래그를 true로 올리면 실제로 다시 뜬다 — ⓐ가 "게이트 덕분"이 아니라 "원래 안 뜨는
        // 조건이라서" 통과하는 가짜 그린이 되는 것을 막는 대조군이다.
        //
        // 실측 대상은 CPU 신호를 쓴다. 4종 중 유일하게 테스트가 조건을 실제로 성립시킬 수 있기 때문이다
        // (배터리 잔량/충전 상태/네트워크 연결성은 OS가 정하는 값이라 테스트가 만들 수 없고, 만들려고
        // 하는 것 자체가 원칙 3/27-7이 금지하는 OS 제어다). 프레임타임 임계값을 0에 가깝게 낮추면
        // 어떤 프레임이든 "과부하"로 판정되므로 신호가 확실히 성립한다.
        //
        // ★ 배포 에셋(DefaultStickConfig.asset)은 **절대 건드리지 않는다** — Object.Instantiate로 복제한
        // 사본을 Director에 꽂았다가 TearDown에서 원래 참조를 되돌리고 사본을 파괴한다
        // (CharacterScaleInvarianceTests가 쓰는 것과 같은 관례). 임시값이 커밋에 섞여 들어가는 사고를
        // 구조적으로 불가능하게 만드는 것이 목적이다.

        private static readonly FieldInfo ConfigField = typeof(HardwareReactionDirector)
            .GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>테스트 중 고정할 프레임 간격(초). 아래 ArmCpuSignal 주석의 근거 참고.</summary>
        private const float CapturedFrameSeconds = 0.05f;

        private HardwareReactionDirector _director;
        private StickConfig _originalDirectorConfig;
        private StickConfig _clonedConfig;
        private int _activeEventCount;
        private System.Action<HardwareReactionEvent> _counter;

        [TearDown]
        public void RestoreDirectorConfig()
        {
            // 엔진 전역 설정이므로 무슨 일이 있어도 되돌린다(다음 테스트가 고정 프레임으로 돌면 안 된다).
            Time.captureDeltaTime = 0f;
            if (_counter != null)
            {
                StickmanEventBus.HardwareReactionChanged -= _counter;
                _counter = null;
            }
            if (_director != null && _originalDirectorConfig != null)
            {
                ConfigField.SetValue(_director, _originalDirectorConfig);
            }
            if (_clonedConfig != null)
            {
                Object.Destroy(_clonedConfig);
                _clonedConfig = null;
            }
            _director = null;
            _originalDirectorConfig = null;
        }

        /// <summary>
        /// Director의 설정을 **복제본**으로 갈아끼우고, CPU 신호 하나만 확실히 성립하도록 세운다.
        /// 나머지 3종(배터리/충전/네트워크)은 폴링 주기를 테스트 길이보다 훨씬 길게 밀어 이 테스트가
        /// 도는 동안 판정 자체가 일어나지 않게 한다 — 그래야 관측된 이벤트가 CPU 게이트의 결과임이 확실해진다.
        /// </summary>
        private void ArmCpuSignal(bool autonomousEnabled)
        {
            var directors = Object.FindObjectsByType<HardwareReactionDirector>(FindObjectsSortMode.None);
            Assert.AreEqual(1, directors.Length, "씬의 HardwareReactionDirector는 1개여야 합니다.");
            _director = directors[0];

            _originalDirectorConfig = (StickConfig)ConfigField.GetValue(_director);
            Assert.IsNotNull(_originalDirectorConfig, "HardwareReactionDirector에 StickConfig가 배선돼 있지 않습니다.");

            _clonedConfig = Object.Instantiate(_originalDirectorConfig);
            _clonedConfig.enableAutonomousHardwareReactions = autonomousEnabled;

            // CPU 신호를 확실히 성립시킨다. 임계값을 0에 가깝게 내리는 것만으로는 부족했다 —
            // Director가 `Mathf.Max(0.001f, 임계값)`으로 **1ms 하한을 강제**하는데, batchmode -nographics
            // 에서는 렌더링이 없어 실제 프레임타임이 1ms보다 짧아 어떤 임계값을 넣어도 '과부하'가
            // 성립하지 않는다(첫 실행에서 이 테스트가 정확히 그 이유로 빨간불이 났다).
            // 그래서 프레임타임 자체를 Time.captureDeltaTime으로 고정한다 — 실행 환경의 속도와
            // 무관하게 매 프레임 정확히 50ms가 흐른 것으로 취급되므로, 판정이 결정적으로 재현된다.
            Time.captureDeltaTime = CapturedFrameSeconds;   // 50ms/프레임(하한 1ms의 50배)
            _clonedConfig.hardwareCpuHighFrameTimeThresholdSeconds = 0.002f; // 하한(1ms) 위, 50ms 아래
            _clonedConfig.hardwareCpuSampleInterval = 1f;
            _clonedConfig.hardwareCpuSustainWindowSeconds = 1f;
            _clonedConfig.hardwareReactionCooldownSeconds = 0f;

            // 나머지 3종은 이 테스트 시간 안에 폴링 자체가 돌지 않도록 밀어둔다.
            _clonedConfig.hardwareBatteryPollInterval = 9999f;
            _clonedConfig.hardwareChargingPollInterval = 9999f;
            _clonedConfig.hardwareNetworkPollInterval = 9999f;

            ConfigField.SetValue(_director, _clonedConfig);

            _activeEventCount = 0;
            _counter = evt => { if (evt.Active) _activeEventCount++; };
            StickmanEventBus.HardwareReactionChanged += _counter;
        }

        /// <summary>ⓐ 절대 조건 — 기본값(OFF)에서는 자율 발동이 단 1건도 일어나지 않는다.</summary>
        [UnityTest]
        public IEnumerator AutonomousReactionNeverFiresWhileDisabled()
        {
            yield return LoadSceneAndResolve();
            ArmCpuSignal(autonomousEnabled: false);

            // 샘플 주기(1초) + 지속 창(1초)을 넉넉히 넘기는 시간. 게이트가 없다면 이 사이에 반드시 뜬다
            // (그 사실은 아래 EnablingFlagRestoresAutonomousReaction 대조군이 실제로 증명한다).
            yield return new WaitForSeconds(3.0f);

            Assert.AreEqual(0, _activeEventCount,
                $"enableAutonomousHardwareReactions=false인데 자율 하드웨어 반응이 {_activeEventCount}건 발행됐습니다. " +
                "사용자가 신고한 '요청하지 않은 이모트가 캐릭터를 가림'이 그대로 재발합니다.");
            Assert.IsFalse(_renderer.IsVisible,
                $"자율 발동이 꺼져 있는데 이모트({_renderer.VisibleKind})가 화면에 떠 있습니다.");
            Assert.AreEqual(0, _renderer.ActiveVisualCount,
                "자율 발동이 꺼져 있는데 이모트 시각 오브젝트가 생성됐습니다.");

            Debug.Log("[하드웨어테스트] 자율 발동 OFF 검증 통과 — 3초 동안 자율 이벤트 0건.");
        }

        /// <summary>
        /// ⓑ + ⓒ — OFF 상태에서도 수동 발동은 살아 있고, 플래그를 켜면 자율 발동이 실제로 되살아난다.
        /// ⓒ가 없으면 ⓐ는 "게이트가 막아서"가 아니라 "애초에 조건이 성립하지 않아서" 통과하는 가짜
        /// 그린일 수 있다 — 같은 신호 설정으로 true에서 뜨는 것을 보여야 ⓐ의 의미가 확정된다.
        /// </summary>
        [UnityTest]
        public IEnumerator ManualPathStillWorksWhileDisabledAndFlagRestoresAutonomous()
        {
            yield return LoadSceneAndResolve();
            ArmCpuSignal(autonomousEnabled: false);

            // ⓑ 수동 발동(단축키/우클릭 메뉴가 호출하는 바로 그 API)은 게이트 바깥이라 그대로 떠야 한다.
            _director.ForceTriggerNow("테스트 수동 발동");
            yield return null;

            Assert.IsTrue(_renderer.IsVisible,
                "자율 발동이 꺼져 있다는 이유로 **수동** 데모 미리보기까지 막혔습니다 — " +
                "기능을 지우는 것이 아니라 기본값만 조용하게 만드는 것이 요구사항입니다 " +
                "(Ctrl+Opt+Cmd+H / 우클릭 메뉴는 반드시 살아 있어야 합니다).");
            Assert.Greater(_renderer.ActiveVisualCount, 0, "수동 발동인데 시각 오브젝트가 하나도 생성되지 않았습니다.");

            // 미리보기(6초)가 스스로 걷힐 때까지 기다린 뒤 대조군으로 넘어간다.
            yield return new WaitForSeconds(7.0f);
            Assert.IsFalse(_renderer.IsVisible, "데모 미리보기가 스스로 걷히지 않았습니다.");

            // ⓒ 대조군 — 같은 신호 설정에서 플래그만 true로 올리면 자율 발동이 실제로 일어난다.
            _clonedConfig.enableAutonomousHardwareReactions = true;
            _activeEventCount = 0;
            yield return new WaitForSeconds(3.0f);

            Assert.Greater(_activeEventCount, 0,
                "enableAutonomousHardwareReactions=true인데도 자율 하드웨어 반응이 한 번도 발행되지 않았습니다 — " +
                "게이트가 꺼짐(OFF) 검증을 무의미하게 만드는 가짜 그린이거나, 플래그를 올려도 기능이 " +
                "되살아나지 않는다는 뜻입니다(둘 다 회귀입니다).");
            Assert.AreEqual(HardwareReactionKind.HighCpu, _renderer.VisibleKind,
                "대조군에서 뜬 반응이 CPU 과부하가 아닙니다 — 테스트가 의도한 신호가 아닌 다른 신호가 섞였습니다.");

            Debug.Log($"[하드웨어테스트] 수동 경로 생존 + 플래그 ON 대조군 검증 통과 — 자율 이벤트 {_activeEventCount}건.");
        }

        // ============================================================================
        // ★ 겹침 금지 — 이모트가 캐릭터(머리)를 가리지 않는다
        // ============================================================================
        // 사용자 신고의 핵심은 "떴다"가 아니라 "**캐릭하고 겹친다**"였다. 직전 라운드(커밋 0106b21)에서
        // 이모트를 머리 **옆 대각선 위**로 빼고 말풍선을 그 위로 올리는 재설계가 들어갔는데, 그 배치가
        // 유지되는지를 지금까지는 사람 눈으로만 확인했다. 아래 테스트가 그것을 수치로 잠근다.
        //
        // 왜 "배율이 바뀌어도" 성립해야 하는가: HardwareReactionRenderer의 배치 상수는 전부 전신 높이
        // 대비 **비율**이라 어떤 캐릭터 크기에서도 같은 여유가 유지되는 것이 설계 의도다. 누군가 상수
        // 하나를 절대 유닛으로 되돌리면 특정 배율에서만 조용히 겹치기 시작한다 — 그 회귀를 잡는 것이
        // 이 테스트의 목적이다(실제로 이번 라운드에 땀방울 **속도**가 절대 유닛으로 남아 있던 것을 찾았다).

        // 아래 두 비율은 HardwareReactionRenderer의 배치 설계 주석에 적힌 실측 지오메트리다
        // (머리 중심 2.05 / 외곽선 포함 반경 약 0.27, 당시 전신 높이 2.2747 기준).
        private const float HeadCenterRatio = 2.05f / 2.2746944f;
        private const float HeadRadiusRatio = 0.27f / 2.2746944f;

        /// <summary>이모트 원과 머리 원이 닿지 않는다 — 어떤 캐릭터 배율에서도.</summary>
        [UnityTest]
        public IEnumerator EmoteNeverOverlapsCharacterHead()
        {
            yield return LoadSceneAndResolve();

            var agent = _renderer.GetComponent<StickmanAgent>();
            Assert.IsNotNull(agent, "렌더러와 같은 GameObject에 StickmanAgent가 없습니다.");

            StickmanEventBus.RaiseHardwareReactionChanged(HardwareReactionKind.HighCpu, active: true);
            yield return null;

            GameObject container = GameObject.Find(ContainerName);
            Assert.IsNotNull(container, $"'{ContainerName}' 컨테이너를 찾지 못했습니다.");
            Assert.IsTrue(_renderer.TryGetOccupiedTopWorldY(out float emoteTopY),
                "이모트가 떠 있는데 점유 상단 y를 돌려주지 않습니다(말풍선이 비켜설 근거가 사라집니다).");

            float height = agent.CharacterTotalHeightWorld;
            Assert.Greater(height, 0f, "캐릭터 전신 높이가 0 이하입니다.");

            Vector3 emoteCenter = container.transform.position;
            // TryGetOccupiedTopWorldY는 컨테이너 중심 + IconScale을 돌려주므로, 역산하면 이모트 반경이다.
            float iconRadius = emoteTopY - emoteCenter.y;
            Assert.Greater(iconRadius, 0f, "이모트 반경 역산값이 0 이하입니다.");

            Vector2 body = agent.Blackboard.Body.position;
            Vector2 headCenter = new Vector2(body.x, body.y + height * HeadCenterRatio);
            float headRadius = height * HeadRadiusRatio;

            float distance = Vector2.Distance(headCenter, (Vector2)emoteCenter);
            float required = headRadius + iconRadius;

            Assert.Greater(distance, required,
                $"이모트가 머리와 겹칩니다 — 중심거리 {distance:F3}유닛 <= 필요한 최소거리 {required:F3}유닛 " +
                $"(머리 반경 {headRadius:F3} + 이모트 반경 {iconRadius:F3}). " +
                $"전신 높이={height:F3}, 머리중심={headCenter}, 이모트중심={emoteCenter}. " +
                "사용자가 신고한 '캐릭하고 겹치는데'가 그대로 재발합니다.");

            // 세로로만 쌓인 것이 아니라 실제로 **옆으로** 비켜섰는지도 확인한다(가로 회피가 없으면
            // 말풍선이 화면 위로 계속 밀려나고, 머리와의 여유도 세로 값 하나에만 의존하게 된다).
            float horizontalOffset = Mathf.Abs(emoteCenter.x - body.x);
            Assert.Greater(horizontalOffset, headRadius,
                $"이모트가 머리 바로 위에 세로로만 쌓였습니다(가로 오프셋 {horizontalOffset:F3} <= 머리 반경 {headRadius:F3}) — " +
                "커밋 0106b21의 '머리 옆 대각선 위' 배치가 되돌려졌습니다.");

            Debug.Log($"[하드웨어테스트] 겹침 금지 검증 통과 — 전신 {height:F3}유닛, 중심거리 {distance:F3} > 필요 {required:F3}, " +
                $"가로 오프셋 {horizontalOffset:F3}(전신 대비 {horizontalOffset / height:F3}배).");

            StickmanEventBus.RaiseHardwareReactionChanged(HardwareReactionKind.HighCpu, active: false);
            yield return new WaitForSeconds(0.6f);
        }
    }
}
