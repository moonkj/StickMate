using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 배선 감사 잔여 2건(2026-08-30)의 실측 잠금 —
    /// <see cref="StickmanEventBus.LandingRollRequested"/> /
    /// <see cref="StickmanEventBus.WanderAmbientMotionRequested"/>.
    /// (세 번째였던 대결 시작 이벤트는 해당 기능 전체 삭제(2026-08-30)로 이벤트 자체가 사라졌다.)
    ///
    /// ============================================================================
    /// 왜 실제 씬(Main.unity)을 로드하는가
    /// ============================================================================
    /// 이 프로젝트가 여섯 번 반복한 실패 모드는 "로직은 완성됐는데 아무도 구독/배치를 안 해서 화면에
    /// 한 픽셀도 안 나온다"이다. 컴포넌트를 테스트 안에서 새로 만들어 검사하면 그 실패 모드를
    /// <b>구조적으로 놓친다</b> — 씬에 배치돼 있는지 자체가 검사 대상이어야 한다
    /// (Tests/PlayMode/Phase5VisualLayerTests.cs와 같은 관례).
    ///
    /// ============================================================================
    /// 절대 조건으로 단언하는 것(상대 마진 방식 금지 — 이 프로젝트는 그 방식이 버그를 2라운드 연속
    /// 놓친 전례가 있다)
    /// ============================================================================
    ///  ① 두 소비자 컴포넌트가 씬에 <b>정확히 1개씩</b> 있다(0 = 배치 누락, 2 = 중복 배치).
    ///  ② 착지 먼지: <b>실제로 6유닛을 떨어뜨려</b> FallState가 스스로 이벤트를 발행하게 하고,
    ///     그 결과 'LandingDust' GameObject가 씬에 실존하며 <b>발밑 높이</b>에 생긴다.
    ///  ③ 유휴 동작: 신호를 받은 뒤 <b>실제 관절 각도</b>가 중립에서 벗어난다(플래그만 보지 않는다).
    ///     기지개는 두 팔이 모두 머리 위로, 주위 살피기는 <b>한쪽 팔만</b> 올라간다 — 두 동작이
    ///     실제로 서로 다른 그림인지까지 확인한다.
    ///  ④ 둘 다 콜라이더를 하나도 만들지 않는다(관전 전용 = 클릭관통 유지, CLAUDE.md 불변 원칙 2).
    ///
    /// 네거티브 컨트롤(이 프로젝트 표준): 두 StickConfig 스위치를 각각 끄고 같은 자극을 다시 주어
    /// <b>연출이 실제로 사라지는지</b>를 확인한다 — "통과하는 테스트가 실제로 이 배선을 보고 있다"는 증거다.
    /// </summary>
    public sealed class EventWiringVisualTests
    {
        private const string LogPrefix = "[배선3건-TEST]";

        private const string DustContainerName = "LandingDust";

        private const long FlatGroundHandle = 9401L;

        /// <summary>구르기 임계값(기본 2유닛)을 확실히 넘는 낙하 — LandingCrouchTests와 같은 값.</summary>
        private const float HighDropUnits = 6f;

        private const float SettleWaitSeconds = 1.6f;
        private const float MaxObserveSeconds = 6f;

        private sealed class TestFootholdService : IPlatformWindowService
        {
            public readonly List<PlatformFoothold> Footholds = new List<PlatformFoothold>();
            public IReadOnlyList<PlatformFoothold> EnumerateFootholds() => Footholds;
            public bool CreateOverlayWindow() => true;
            public void SetClickThrough(bool enabled) { }
            public void SetAlwaysOnTop(bool enabled) { }
            public bool IsFullscreenAppActive() => false;
        }

        private sealed class ScriptedIntentSource : IMovementIntentSource
        {
            public float MoveInputX { get; set; }
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }

        private StickmanAgent _agent;
        private LandingDustRenderer _dust;
        private IdleAmbientMotionRenderer _ambient;

        private StickConfig _originalConfig;
        private StickConfig _clonedConfig;
        private IMovementIntentSource _originalIntent;
        private FootholdPoller _originalPoller;
        private Vector2 _savedOrigin;

        private TestFootholdService _service;
        private ScriptedIntentSource _intent;
        private Transform _head;
        private Vector3 _headNeutralLocal;
        private float _groundWorldY;
        private float _characterHeight;

        // ★ 자율 발행자 침묵용 — AutoWanderController는 StickmanAgent가 **원본 config 에셋**을 들려
        // 생성한다(복제본을 꽂아도 그쪽은 원본을 계속 본다). 유휴 동작 테스트가 자기 신호만 보려면
        // 원본 값을 잠깐 바꿔야 하며, 테스트가 중간에 실패해도 반드시 되돌아가야 하므로 TearDown에 둔다.
        private bool _silencedWander;
        private float _silencedAtTime;
        private float _savedLookDelayMin;
        private float _savedLookDelayMax;
        private float _savedSitChance;

        [TearDown]
        public void TearDown()
        {
            if (_originalConfig != null && _silencedWander)
            {
                _originalConfig.wanderLookAroundDelayMin = _savedLookDelayMin;
                _originalConfig.wanderLookAroundDelayMax = _savedLookDelayMax;
                _originalConfig.wanderRestExtendSitChance = _savedSitChance;
                _silencedWander = false;
            }

            if (_agent != null && _agent.Blackboard != null)
            {
                _agent.Blackboard.CancelIdleAmbientMotion();
                if (_originalConfig != null) _agent.Blackboard.Config = _originalConfig;
                if (_originalIntent != null) _agent.Blackboard.IntentSource = _originalIntent;
                if (_originalPoller != null) _agent.Blackboard.FootholdPoller = _originalPoller;
            }

            ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOrigin;
            if (_clonedConfig != null) Object.DestroyImmediate(_clonedConfig);
            _clonedConfig = null;
            _agent = null;
            _head = null;
        }

        // ============================================================================
        // 공통 준비 — LandingCrouchTests.SetUpFlatGround와 같은 관례(실제 씬 + 결정론적 발판/의도)
        // ============================================================================

        private IEnumerator SetUpFlatGround()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = ExactlyOne<StickmanAgent>();
            _dust = ExactlyOne<LandingDustRenderer>();
            _ambient = ExactlyOne<IdleAmbientMotionRenderer>();

            // ★ 자율 발행자 침묵은 **아무것도 기다리기 전에** 해야 한다. 실측으로 밟은 함정:
            // AutoWanderController는 Idle 진입 후 1.0~2.5초 뒤 스스로 LookAround를 발행하므로,
            // 아래 정착 대기(1.6초) 동안 손차양 자세가 이미 시작돼 "신호 전인데 팔이 106.8도"로
            // 준비 단언이 깨졌다(테스트가 자기 신호만 보고 있다는 전제가 무너진 것).
            _originalConfig = _agent.Blackboard.Config;
            SilenceAutonomousWanderSignals();

            yield return new WaitForSeconds(SettleWaitSeconds);

            StickmanBlackboard bb = _agent.Blackboard;
            _originalIntent = bb.IntentSource;
            _originalPoller = bb.FootholdPoller;
            _savedOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;

            _clonedConfig = Object.Instantiate(_originalConfig);
            bb.Config = _clonedConfig;

            GameObject physicsGround = GameObject.Find("PhysicsGround");
            Assert.IsNotNull(physicsGround, $"{LogPrefix} 씬에서 PhysicsGround를 찾지 못했습니다.");
            var groundBox = physicsGround.GetComponent<BoxCollider2D>();
            Assert.IsNotNull(groundBox, $"{LogPrefix} PhysicsGround에 BoxCollider2D가 없습니다.");
            _groundWorldY = groundBox.bounds.max.y;

            Camera cam = bb.MainCamera;
            float w = Screen.width;
            float h = Screen.height;
            Vector2 groundOs = ScreenCoordinateConverter.WorldToOsScreen(cam,
                new Vector2(0f, _groundWorldY), _clonedConfig, out _);

            _service = new TestFootholdService();
            _service.Footholds.Add(new PlatformFoothold(FlatGroundHandle,
                new Rect(0f, groundOs.y, w, Mathf.Max(1f, h - groundOs.y)), true));
            bb.FootholdPoller = new FootholdPoller(_service, _clonedConfig);

            _intent = new ScriptedIntentSource { MoveInputX = 0f };
            bb.IntentSource = _intent;

            StandOnGround();
            yield return SettleToNeutralPose(bb);

            _head = FindChildByName(bb.Body.transform, "Head");
            Assert.IsNotNull(_head, $"{LogPrefix} 프리팹에서 Head를 찾지 못했습니다.");
            _headNeutralLocal = _head.localPosition;
            _characterHeight = bb.CharacterHeightWorld;

            Debug.Log($"{LogPrefix} 준비 완료 — 바닥 월드Y={_groundWorldY:F3}, 신장={_characterHeight:F3}유닛, " +
                $"구르기 임계={_clonedConfig.rollLandingHeightThreshold:F2}유닛.");
        }

        /// <summary>AutoWanderController(원본 config를 보는 자율 발행자)가 테스트 도중 자기 신호를
        /// 끼워 넣지 못하게 침묵시킨다 — 유휴 동작 테스트만 사용한다.</summary>
        private void SilenceAutonomousWanderSignals()
        {
            _savedLookDelayMin = _originalConfig.wanderLookAroundDelayMin;
            _savedLookDelayMax = _originalConfig.wanderLookAroundDelayMax;
            _savedSitChance = _originalConfig.wanderRestExtendSitChance;
            _originalConfig.wanderLookAroundDelayMin = 9999f;
            _originalConfig.wanderLookAroundDelayMax = 9999f;
            _originalConfig.wanderRestExtendSitChance = 0f;
            _silencedWander = true;
            _silencedAtTime = Time.time;
        }

        /// <summary>
        /// 침묵 이전에 <b>이미 예약돼 있던</b> 자율 LookAround 1건이 만료될 때까지 기다린다.
        ///
        /// 왜 필요한가(실측 근거 — 이 테스트가 세 번 실패하며 좁혀낸 원인): AutoWanderController는
        /// Idle 구간에 들어갈 때 그 구간의 LookAround 지연시간을 <b>미리 뽑아 둔다</b>. 그래서 설정을
        /// 9999초로 올려도 <b>진행 중인 Idle 구간 하나</b>는 옛 값으로 예약된 채 남고, 그 1건이 테스트
        /// 도중에 터져 <c>_idleAmbientMotion</c>을 LookAround로 덮어썼다(기지개를 요청했는데 한쪽 팔만
        /// 올라가는 그림이 됐다 — 로그에 [유휴동작] 기지개 재생 직후 주위 살피기 재생이 찍혀 확정).
        ///
        /// 대기 시간은 추측이 아니라 <b>설정에서 유도한 상한</b>이다: 그 예약은 반드시 자기 Idle 구간
        /// 안에서 끝나고(TickResting), Idle 구간의 최대 길이는 wanderIdleDurationMax에 지터 상한
        /// (1 + wanderDurationJitterRatio)을 곱한 값이다. 그 구간이 끝나면 다음 EnterResting()이
        /// 9999초짜리 지연을 새로 뽑으므로 이후로는 구조적으로 조용하다.
        /// </summary>
        private IEnumerator WaitForPendingWanderSignalToExpire()
        {
            StickmanBlackboard bb = _agent.Blackboard;
            float jitter = _originalConfig != null ? Mathf.Abs(_originalConfig.wanderDurationJitterRatio) : 0.175f;
            float restMax = _originalConfig != null ? _originalConfig.wanderIdleDurationMax : 6f;
            float guaranteed = restMax * (1f + jitter) + 0.5f;

            while (Time.time - _silencedAtTime < guaranteed)
            {
                bb.CancelIdleAmbientMotion();
                yield return null;
            }
            yield return SettleToNeutralPose(bb);
        }

        /// <summary>
        /// 팔이 <b>실제로</b> Idle 중립으로 수렴할 때까지 기다린다(고정 대기 금지).
        ///
        /// 왜 고정 대기로는 안 되는가(실측으로 두 번 밟았다): AutoWanderController는 Idle에 들어갈 때
        /// 다음 LookAround 지연시간을 <b>그 시점의 설정값으로 미리 뽑아둔다</b>. 그래서 준비 단계에서
        /// 설정을 9999초로 올려도 <b>이미 예약돼 있던 1건</b>은 그대로 터지고, 그 시각은 0~2.5초 사이
        /// 어디든 될 수 있다. 처음엔 신호 전 팔 각도가 106.8도, 대기를 늘린 뒤엔 63.1도로 값만 바뀌었을 뿐
        /// 원인이 그대로였던 이유다. 여기서는 매 프레임 취소를 걸어 그 1건까지 확실히 걷어내고,
        /// 각도가 중립 범위에 들어온 것을 <b>확인한 뒤</b> 진행한다.
        /// </summary>
        private IEnumerator SettleToNeutralPose(StickmanBlackboard bb)
        {
            StickmanPoseAnimator pose = bb.GetPoseAnimator();
            float neutralMax = NeutralArmAngleMax();
            float elapsed = 0f;
            while (elapsed < 5f)
            {
                bb.CancelIdleAmbientMotion();
                yield return null;
                elapsed += Time.deltaTime;
                if (elapsed < 0.5f) continue; // 최소 정착 시간(지수 감쇠 수렴 여유).

                pose.GetUpperAngles(out _, out _, out float leftArm, out float rightArm);
                if (Mathf.Max(Mathf.Abs(leftArm), Mathf.Abs(rightArm)) <= neutralMax) yield break;
            }
            Assert.Fail($"{LogPrefix} 준비 실패 — 5초를 기다려도 팔이 중립({neutralMax:F1}도 이내)으로 " +
                "돌아오지 않았습니다.");
        }

        /// <summary>Idle 중립 자세에서 팔 어깨 각도가 가질 수 있는 최대 크기(도) — 벌림 + 호흡 진폭.
        /// 임의의 매직넘버가 아니라 StickConfig에서 유도하므로, 중립 각도를 튜닝해도 테스트가 조용히
        /// 어긋나지 않는다.</summary>
        private float NeutralArmAngleMax()
        {
            StickConfig cfg = _clonedConfig != null ? _clonedConfig : _originalConfig;
            float spread = cfg != null ? cfg.idleArmSpreadDegrees : 40f;
            float breath = cfg != null ? cfg.idleBreathArmDegrees : 1.5f;
            return Mathf.Abs(spread) + Mathf.Abs(breath) + 1f; // +1 = 지수 감쇠 잔여 오차 여유.
        }

        private void StandOnGround()
        {
            StickmanBlackboard bb = _agent.Blackboard;
            var start = new Vector2(0f, _groundWorldY);
            bb.Body.position = start;
            bb.Body.transform.position = new Vector3(start.x, start.y, bb.Body.transform.position.z);
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = FlatGroundHandle;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
        }

        private static T ExactlyOne<T>() where T : Object
        {
            var found = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            Assert.AreEqual(1, found.Length,
                $"씬의 {typeof(T).Name} 개수가 {found.Length}개입니다 — 1개여야 합니다. " +
                "0개면 SceneBootstrapper 배치 누락(이 컴포넌트가 단 한 번도 실행되지 않는다), " +
                "2개 이상이면 씬에 중복 배치돼 같은 전역 이벤트에 두 번 반응합니다.");
            return found[0];
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                if (root.GetChild(i).name == name) return root.GetChild(i);
            }
            return null;
        }

        // ============================================================================
        // ① 배선 자체
        // ============================================================================

        [UnityTest]
        public IEnumerator BothEventConsumersArePlacedExactlyOnce()
        {
            yield return SetUpFlatGround();

            Assert.IsNotNull(_dust);
            Assert.IsNotNull(_ambient);

            Assert.IsFalse(_dust.IsVisible, "시작 시점에 먼지가 떠 있으면 안 됩니다.");
            Assert.IsFalse(_agent.Blackboard.IsIdleAmbientMotionActive,
                "시작 시점에 유휴 동작이 재생 중이면 안 됩니다.");

            Debug.Log($"{LogPrefix} 배선 검증 통과 — 소비자 2종이 정확히 1개씩 배치되어 있고 초기 상태는 전부 비활성.");
        }

        // ============================================================================
        // ② 착지 먼지 — 실제 낙하로 FallState가 스스로 이벤트를 발행하게 한다
        // ============================================================================

        [UnityTest]
        public IEnumerator HighLandingRaisesDustAtFeetAndCleansUp()
        {
            yield return SetUpFlatGround();

            var seen = new DustObservation();
            yield return DropAndWatchDust(seen);

            Assert.IsTrue(seen.SawDust,
                $"{LogPrefix} {HighDropUnits}유닛을 떨어뜨렸는데 착지 먼지가 한 프레임도 나타나지 않았습니다 — " +
                "LandingRollRequested 구독이 끊겼거나 임계값 판정이 바뀌었습니다.");
            Assert.IsTrue(seen.SawContainerInScene,
                $"{LogPrefix} 렌더러는 '보인다'고 보고했지만 '{DustContainerName}' GameObject가 씬에 실존하지 않았습니다(빈 껍데기).");
            Assert.Greater(seen.MaxPuffCount, 0,
                $"{LogPrefix} 먼지 컨테이너는 생겼는데 실제 LineRenderer가 0개입니다.");
            Assert.AreEqual(0, seen.MaxColliderCount,
                $"{LogPrefix} 먼지가 콜라이더를 만들었습니다 — 관전 전용 연출이므로 클릭관통이 유지되어야 합니다.");

            // 발밑에 생겨야 한다. 신장의 절반보다 위에 생기면 "발밑 먼지"가 아니라 몸통 근처 연출이다.
            Assert.LessOrEqual(Mathf.Abs(seen.ContainerWorldY - _groundWorldY), _characterHeight * 0.25f,
                $"{LogPrefix} 먼지가 발밑이 아니라 월드Y={seen.ContainerWorldY:F3}에 생겼습니다(바닥 {_groundWorldY:F3}).");

            // 세기 램프가 실제로 계산됐는가(임계값을 크게 넘긴 낙하이므로 하한보다 커야 한다).
            Assert.Greater(_dust.LastIntensity, _clonedConfig.landingDustMinIntensity,
                $"{LogPrefix} 6유닛 낙하인데 먼지 세기가 하한({_clonedConfig.landingDustMinIntensity:F2})에 머물렀습니다 — " +
                "낙하 높이 램프가 동작하지 않습니다.");

            // 스스로 사라져야 한다(24시간 상주 앱에서 오브젝트가 남으면 곧 누수다).
            yield return new WaitForSeconds(_clonedConfig.landingDustSeconds + 0.4f);
            Assert.IsFalse(_dust.IsVisible, $"{LogPrefix} 지속시간이 지났는데 먼지가 남아 있습니다.");
            Assert.IsNull(GameObject.Find(DustContainerName),
                $"{LogPrefix} '{DustContainerName}' GameObject가 씬에서 실제로 사라지지 않았습니다.");

            Debug.Log($"{LogPrefix} 착지 먼지 통과 — 획 {seen.MaxPuffCount}개, 세기 {_dust.LastIntensity:F2}, " +
                $"월드Y={seen.ContainerWorldY:F3}, 콜라이더 0개, 자동 소멸 확인.");
        }

        /// <summary>네거티브 컨트롤 — 스위치를 끄면 <b>같은 낙하</b>에서 먼지가 실제로 사라진다.</summary>
        [UnityTest]
        public IEnumerator LandingDustDisabledDrawsNothing()
        {
            yield return SetUpFlatGround();

            _clonedConfig.landingDustEnabled = false;

            var seen = new DustObservation();
            yield return DropAndWatchDust(seen);

            Assert.IsFalse(seen.SawDust,
                $"{LogPrefix} landingDustEnabled=false인데 먼지가 나타났습니다 — 스위치가 실제로는 아무것도 끄지 않습니다.");
            Assert.IsFalse(seen.SawContainerInScene,
                $"{LogPrefix} landingDustEnabled=false인데 '{DustContainerName}'이 씬에 생겼습니다.");
            Assert.IsTrue(seen.SawLandingCrouch,
                $"{LogPrefix} 대조 실패 — 먼지를 껐더니 착지 자체(무릎앉아)까지 사라졌습니다. " +
                "두 층은 독립이어야 합니다(먼지는 부수 연출, 무릎앉아는 흐름 그 자체).");

            Debug.Log($"{LogPrefix} 네거티브 컨트롤 통과 — 먼지만 사라지고 무릎앉아 착지는 그대로.");
        }

        private sealed class DustObservation
        {
            public bool SawDust;
            public bool SawContainerInScene;
            public bool SawLandingCrouch;
            public int MaxPuffCount;
            public int MaxColliderCount;
            public float ContainerWorldY;
        }

        private IEnumerator DropAndWatchDust(DustObservation result)
        {
            StickmanBlackboard bb = _agent.Blackboard;

            var from = new Vector2(0f, _groundWorldY + HighDropUnits);
            bb.Body.position = from;
            bb.Body.transform.position = new Vector3(from.x, from.y, bb.Body.transform.position.z);
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = 0L;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(StickmanStateId.Fall, isForcedInterrupt: true);

            float elapsed = 0f;
            bool landed = false;
            while (elapsed < MaxObserveSeconds)
            {
                yield return null;
                elapsed += Time.deltaTime;

                if (bb.Machine.CurrentStateId == StickmanStateId.LandingCrouch) result.SawLandingCrouch = true;
                if (bb.Machine.CurrentStateId != StickmanStateId.Fall) landed = true;

                if (_dust.IsVisible)
                {
                    result.SawDust = true;
                    GameObject container = GameObject.Find(DustContainerName);
                    if (container != null)
                    {
                        result.SawContainerInScene = true;
                        result.ContainerWorldY = container.transform.position.y;
                        result.MaxPuffCount = Mathf.Max(result.MaxPuffCount,
                            container.GetComponentsInChildren<LineRenderer>(true).Length);
                        result.MaxColliderCount = Mathf.Max(result.MaxColliderCount,
                            container.GetComponentsInChildren<Collider2D>(true).Length);
                    }
                }

                // 착지 후 먼지 수명(기본 0.38초)만큼 더 지켜본 뒤 종료.
                if (landed && elapsed > 0.1f && result.SawDust) break;
                if (landed && elapsed > 2.0f) break;
            }
        }

        // ============================================================================
        // ③ 유휴 앰비언트 동작 — 실제 관절 각도가 중립을 벗어나는지
        // ============================================================================

        [UnityTest]
        public IEnumerator StretchSignalRaisesBothArmsOverhead()
        {
            yield return SetUpFlatGround();
            yield return WaitForPendingWanderSignalToExpire();

            float neutralMax = NeutralArmAngleMax();
            var before = SampleArms();
            Assert.LessOrEqual(before.MaxAbsArm, neutralMax,
                $"{LogPrefix} 준비 실패 — 신호 전인데 팔이 이미 중립({neutralMax:F1}도)을 벗어나 있습니다({before.MaxAbsArm:F1}도).");

            StickmanEventBus.RaiseWanderAmbientMotionRequested(WanderAmbientMotion.SitAndYawn);
            yield return null;

            Assert.IsTrue(_ambient.LastRequestAccepted,
                $"{LogPrefix} 기지개 신호가 거부됐습니다 — 상태={_agent.Blackboard.Machine.CurrentStateId}.");
            Assert.IsTrue(_agent.Blackboard.IsIdleAmbientMotionActive,
                $"{LogPrefix} 신호를 받았는데 재생이 시작되지 않았습니다.");
            Assert.AreEqual(WanderAmbientMotion.SitAndYawn, _agent.Blackboard.CurrentIdleAmbientMotion);

            var peak = new ArmPeak();
            yield return WatchArms(_clonedConfig.idleAmbientStretchSeconds, peak);

            // 기지개 = **두 팔 모두** 머리 위(각도 180 근처). 매달리기와 같은 규약이라 |각도|가 커진다.
            Assert.Greater(peak.MaxAbsLeftArm, 120f,
                $"{LogPrefix} 기지개인데 왼팔이 머리 위로 올라가지 않았습니다(최대 |{peak.MaxAbsLeftArm:F1}|도).");
            Assert.Greater(peak.MaxAbsRightArm, 120f,
                $"{LogPrefix} 기지개인데 오른팔이 머리 위로 올라가지 않았습니다(최대 |{peak.MaxAbsRightArm:F1}|도).");
            // 몸 상승(idleAmbientStretchRiseRatio)은 **단언하지 않고 로그로만 남긴다** — 같은 축(머리
            // 로컬 Y)에 Idle 호흡 오프셋이 상시로 실려 있어, 이 축의 절대 단언은 "연출이 만든 상승"과
            // "호흡이 만든 상승"을 구분하지 못한다(이 프로젝트가 금지한 '통과하지만 아무것도 증명하지
            // 않는 단언'이 된다). 대신 호흡이 절대 건드리지 않는 축(머리 로컬 X)을 주위 살피기 쪽에서
            // 절대 조건으로 단언한다.

            // 끝나면 정확히 중립으로 되돌아온다(연출 잔재가 남으면 그때부터 캐릭터가 계속 이상하다).
            yield return new WaitForSeconds(0.7f);
            Assert.IsFalse(_agent.Blackboard.IsIdleAmbientMotionActive,
                $"{LogPrefix} 지속시간이 지났는데 유휴 동작이 끝나지 않았습니다.");
            var after = SampleArms();
            Assert.LessOrEqual(after.MaxAbsArm, neutralMax,
                $"{LogPrefix} 기지개가 끝났는데 팔이 중립({neutralMax:F1}도)으로 돌아오지 않았습니다({after.MaxAbsArm:F1}도).");
            Assert.Less(Mathf.Abs(_head.localPosition.x - _headNeutralLocal.x), 0.001f,
                $"{LogPrefix} 머리 좌우 오프셋이 원복되지 않았습니다.");

            Debug.Log($"{LogPrefix} 기지개 통과 — 왼팔 최대 |{peak.MaxAbsLeftArm:F1}|도 / 오른팔 최대 " +
                $"|{peak.MaxAbsRightArm:F1}|도, 몸 상승 {peak.MaxHeadRise:F4}유닛, 종료 후 중립 복귀 확인.");
        }

        [UnityTest]
        public IEnumerator LookAroundSignalRaisesOneArmAndShiftsHead()
        {
            yield return SetUpFlatGround();
            yield return WaitForPendingWanderSignalToExpire();

            StickmanEventBus.RaiseWanderAmbientMotionRequested(WanderAmbientMotion.LookAround);
            yield return null;

            Assert.IsTrue(_ambient.LastRequestAccepted, $"{LogPrefix} 주위 살피기 신호가 거부됐습니다.");
            Assert.AreEqual(WanderAmbientMotion.LookAround, _agent.Blackboard.CurrentIdleAmbientMotion);

            var peak = new ArmPeak();
            yield return WatchArms(_clonedConfig.idleAmbientLookAroundSeconds, peak);

            // 주위 살피기 = **한쪽 팔만** 이마로. 두 동작이 실제로 다른 그림인지까지 잠근다
            // (같은 포즈에 이름만 다르면 유저는 구분할 수 없다).
            float neutralMax = NeutralArmAngleMax();
            Assert.Greater(peak.MaxAbsRightArm, 80f,
                $"{LogPrefix} 손차양 자세인데 팔이 올라가지 않았습니다(최대 |{peak.MaxAbsRightArm:F1}|도).");
            Assert.LessOrEqual(peak.MaxAbsLeftArm, neutralMax,
                $"{LogPrefix} 주위 살피기인데 반대쪽 팔까지 움직였습니다(|{peak.MaxAbsLeftArm:F1}|도 > 중립 " +
                $"{neutralMax:F1}도) — 기지개와 실루엣이 구분되지 않습니다.");

            float expectedShift = _characterHeight * _clonedConfig.idleAmbientLookHeadShiftRatio;
            Assert.Greater(peak.MaxHeadShiftX, expectedShift * 0.4f,
                $"{LogPrefix} 머리가 좌우로 움직이지 않았습니다(최대 {peak.MaxHeadShiftX:F4}유닛, 기대 {expectedShift:F4}).");

            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(_agent.Blackboard.IsIdleAmbientMotionActive);
            Assert.Less(Mathf.Abs(_head.localPosition.x - _headNeutralLocal.x), 0.001f,
                $"{LogPrefix} 머리 좌우 오프셋이 원복되지 않았습니다.");

            Debug.Log($"{LogPrefix} 주위 살피기 통과 — 올린 팔 |{peak.MaxAbsRightArm:F1}|도 / 반대 팔 " +
                $"|{peak.MaxAbsLeftArm:F1}|도, 머리 이동 {peak.MaxHeadShiftX:F4}유닛.");
        }

        /// <summary>네거티브 컨트롤 — 스위치를 끄면 같은 신호에 포즈가 전혀 변하지 않는다.</summary>
        [UnityTest]
        public IEnumerator IdleAmbientMotionDisabledKeepsNeutralPose()
        {
            yield return SetUpFlatGround();
            yield return WaitForPendingWanderSignalToExpire();

            _clonedConfig.idleAmbientMotionEnabled = false;

            StickmanEventBus.RaiseWanderAmbientMotionRequested(WanderAmbientMotion.SitAndYawn);
            yield return null;

            Assert.IsFalse(_ambient.LastRequestAccepted,
                $"{LogPrefix} idleAmbientMotionEnabled=false인데 재생이 수락됐습니다 — 스위치가 아무것도 끄지 않습니다.");
            Assert.IsFalse(_agent.Blackboard.IsIdleAmbientMotionActive);

            var peak = new ArmPeak();
            yield return WatchArms(_clonedConfig.idleAmbientStretchSeconds, peak);

            float neutralMax = NeutralArmAngleMax();
            Assert.LessOrEqual(peak.MaxAbsLeftArm, neutralMax,
                $"{LogPrefix} 스위치를 껐는데 왼팔이 중립({neutralMax:F1}도)을 벗어났습니다(|{peak.MaxAbsLeftArm:F1}|도).");
            Assert.LessOrEqual(peak.MaxAbsRightArm, neutralMax,
                $"{LogPrefix} 스위치를 껐는데 오른팔이 중립({neutralMax:F1}도)을 벗어났습니다(|{peak.MaxAbsRightArm:F1}|도).");
            Assert.Less(peak.MaxHeadShiftX, 0.001f,
                $"{LogPrefix} 스위치를 껐는데 머리가 움직였습니다({peak.MaxHeadShiftX:F4}유닛).");

            Debug.Log($"{LogPrefix} 네거티브 컨트롤 통과 — 스위치를 끄면 Idle 중립 포즈가 100% 유지됨.");
        }

        private sealed class ArmPeak
        {
            public float MaxAbsLeftArm;
            public float MaxAbsRightArm;
            public float MaxHeadRise;
            public float MaxHeadShiftX;
        }

        private struct ArmSample
        {
            public float MaxAbsArm;
        }

        private ArmSample SampleArms()
        {
            StickmanPoseAnimator pose = _agent.Blackboard.GetPoseAnimator();
            pose.GetUpperAngles(out _, out _, out float leftArm, out float rightArm);
            return new ArmSample { MaxAbsArm = Mathf.Max(Mathf.Abs(leftArm), Mathf.Abs(rightArm)) };
        }

        /// <summary>지정 시간 동안 매 프레임 팔 각도/머리 오프셋의 절대 최대치를 모은다.</summary>
        private IEnumerator WatchArms(float seconds, ArmPeak peak)
        {
            StickmanPoseAnimator pose = _agent.Blackboard.GetPoseAnimator();
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                yield return null;
                elapsed += Time.deltaTime;

                pose.GetUpperAngles(out _, out _, out float leftArm, out float rightArm);
                peak.MaxAbsLeftArm = Mathf.Max(peak.MaxAbsLeftArm, Mathf.Abs(leftArm));
                peak.MaxAbsRightArm = Mathf.Max(peak.MaxAbsRightArm, Mathf.Abs(rightArm));
                peak.MaxHeadRise = Mathf.Max(peak.MaxHeadRise, _head.localPosition.y - _headNeutralLocal.y);
                peak.MaxHeadShiftX = Mathf.Max(peak.MaxHeadShiftX,
                    Mathf.Abs(_head.localPosition.x - _headNeutralLocal.x));
            }
        }
    }
}
