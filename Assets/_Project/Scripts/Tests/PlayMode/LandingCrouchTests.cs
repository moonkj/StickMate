using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 무릎앉아 착지(2026-08-29, 사용자 명시 요청 "떨어질때 관절이 이상하게 꺾이면서 넘어지는데
    /// 떨어질때 무릎앉아 형태로 멋지게 착지해야지")의 실측 잠금.
    ///
    /// ============================================================================
    /// 무엇을 보고 있는가 — 로그가 아니라 **실제 관절/머리 좌표**
    /// ============================================================================
    /// 이 프로젝트는 "통과하는 테스트가 버그를 2라운드 연속 놓친" 전례가 있다(프레이밍 테스트의 상대
    /// 마진 방식). 그래서 여기서는 상태 ID만 보지 않고, 포즈가 **실제로 낮아졌는지**를 세 가지 절대
    /// 조건으로 함께 확인한다:
    ///   (A) 무릎앉아 동안 **머리 월드 Y가 서 있을 때보다 신장의 일정 비율 이상 내려간다.**
    ///       루트 위치는 착지 스냅으로 지면에 고정되어 있으므로, 머리가 내려갔다면 그건 순수하게
    ///       포즈(관절 각도 + 몸 오프셋)의 결과다.
    ///   (B) 그동안 **아래쪽 발은 지면에 붙어 있다.** 앉는 깊이를 각도에서 유도하지 않고 몸을 그냥
    ///       내리면 발이 지면을 파고들거나 뜬다 — 이 조건이 그 오답을 걸러낸다.
    ///   (C) 무릎 굽힘의 크기가 충분히 크고, **부호가 뒤집히지 않는다**(사람 무릎은 한 방향으로만
    ///       접힌다 — 직전 라운드에 관절 각도 제한 좌우 반전 버그가 있었던 자리다).
    ///
    /// ============================================================================
    /// 네거티브 컨트롤 2종 (이 프로젝트 표준: "수정을 되돌리면 실제로 실패해야 한다")
    /// ============================================================================
    ///   (1) StickConfig.landingImpactRagdollShield를 끄면 **같은 낙하가 실제로 RAGDOLL이 된다.**
    ///       이것이 사용자가 신고한 "관절이 이상하게 꺾이면서 넘어지는" 증상의 재현이며, 동시에
    ///       "정상 착지에서 랙돌로 가지 않는다"는 본 테스트가 우연이 아님을 증명한다.
    ///   (2) StickConfig.landingCrouchEnabled를 끄면 **무릎앉아가 실제로 사라진다**(상태도, 머리
    ///       내려감도). 연출을 끄면 예전 거동으로 정확히 되돌아간다는 탈출구 확인도 겸한다.
    ///
    /// 배치는 EdgeHopDownTests/LedgeHangDescentTests와 같은 관례를 따른다 — 실제 씬(Main.unity)의
    /// StickmanAgent를 그대로 쓰되 결정론적 발판/이동의도만 주입하고, StickConfig는 복제본을 꽂아
    /// 원본 자산을 절대 건드리지 않는다(CLAUDE.md 불변 원칙 3).
    ///
    /// ★ 발판을 **씬의 물리 바닥(PhysicsGround) 상단과 같은 높이**에 까는 것이 이 파일의 핵심 장치다.
    /// 그래야 착지 순간 실제 Collider2D 충돌이 발생해 위 네거티브 컨트롤 (1)이 성립한다 — 논리적
    /// 발판만 있는 높이에 착지시키면 충돌 자체가 없어 "랙돌이 안 된다"가 아무것도 증명하지 못한다.
    /// </summary>
    public sealed class LandingCrouchTests
    {
        private const string LogPrefix = "[무릎앉아-TEST]";

        private const long FlatGroundHandle = 9101L;

        /// <summary>구르기 임계값(기본 2유닛)을 확실히 넘고, 물리 충돌 충격량도 ragdollForceThreshold
        /// (기본 8)를 크게 넘는 낙하. v = sqrt(2*9.81*gravityScale*h)이므로 gravityScale 3에서
        /// h=6이면 약 18.8유닛/초 = 충격량 18.8(질량 1) ≫ 8이다.</summary>
        private const float HighDropUnits = 6f;

        /// <summary>더 얕지만 여전히 임계값을 넘는 낙하 — 깊이/시간 램프가 실제로 작동하는지 대조군.</summary>
        private const float ModerateDropUnits = 2.4f;

        /// <summary>★ Dock 상단 → 바닥 안전망 상단 낙차(월드 유닛). **하드코딩하지 않는다** —
        /// Core/DockGeometry.cs가 (tilesize + dockThicknessTilePaddingPoints − BottomSafetyNetInsetPoints)를
        /// 월드로 환산해 주는 단일 소스다(이 개발 머신 tilesize=49 → 67pt → 1.63747유닛).
        /// 2026-08-30 횡단 리뷰 M1: 이 값이 파일마다 0.855(안전망이 40pt 위였던 시절의 화석) / 1.6375로
        /// 갈라져 있었고, 그 탓에 배율 불변식 테스트가 실제 시스템이 아니라 자기 상수를 지키고 있었다.</summary>
        /// <remarks>★ 2026-08-30: 갱신된 실측 낙차 1.6375는 여전히 rollLandingHeightThreshold(2유닛)
        /// **미만**이라 "Dock 단차에서는 무릎앉아 착지를 하지 않는다"는 리더 지시는 그대로 성립한다.
        /// 다만 예전 0.855보다 임계값에 훨씬 가까워졌다(여유 1.145 → 0.363유닛) — tilesize 를 크게 쓰는
        /// 사용자(83 이상)에게는 Dock 단차가 임계값을 넘어 연출이 실제로 발동한다. 그 자체는 물리적으로
        /// 옳은 거동이지만(낙차가 정말 커졌다), 지시의 전제가 tilesize에 의존한다는 사실은 기록해 둔다.</remarks>
        private static readonly float DockStepDropUnits = DockGeometry.ReferenceDockDropWorldUnits;

        private const float SettleWaitSeconds = 2.0f;
        private const float MaxObserveSeconds = 8f;

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

        /// <summary>한 번의 낙하를 관찰한 결과 묶음 — 각 테스트가 같은 관찰기를 재사용한다.</summary>
        private sealed class DropObservation
        {
            public bool SawLandingCrouch;
            public bool SawRagdoll;
            public bool SawGetup;
            public bool Settled;                 // Idle/Walk로 복귀했는가
            public float CrouchSeconds;          // LandingCrouch에 머문 시간
            public float MaxHeadDrop;            // 서 있을 때 대비 머리가 내려간 최대 깊이(월드 유닛)
            public float MaxKneeBendAbs;         // 무릎 굽힘 크기의 최댓값(도)
            public float WorstFootGroundError;   // 낮은 쪽 발과 지면의 최대 어긋남(월드 유닛)
            public float MinKneeSignedProduct;   // 좌우 무릎 각도의 부호 곱 최솟값(양수여야 = 같은 방향)
            public string WorstFootFrameDetail = "(없음)"; // 최악 프레임의 상세(진단용)
            public StickmanStateId FinalState;
        }

        private StickmanAgent _agent;
        private StickConfig _originalConfig;
        private StickConfig _clonedConfig;
        private IMovementIntentSource _originalIntent;
        private FootholdPoller _originalPoller;
        private Vector2 _savedOrigin;

        private TestFootholdService _service;
        private ScriptedIntentSource _intent;
        private Transform _head;
        private float _groundWorldY;
        private float _standingHeadY;
        private float _characterHeight;

        [TearDown]
        public void TearDown()
        {
            if (_agent != null && _agent.Blackboard != null)
            {
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
        // 공통 준비
        // ============================================================================

        /// <summary>
        /// 씬의 **물리 바닥(PhysicsGround) 상단과 정확히 같은 높이**에 화면 전폭 발판 1장을 깔고,
        /// 캐릭터를 그 위에 세운 뒤 Idle로 만든다. 클래스 문서 ★ 참고 — 착지 순간 실제 Collider2D
        /// 충돌이 발생하는 배치여야 네거티브 컨트롤이 성립한다.
        /// </summary>
        private IEnumerator SetUpFlatGround()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다 — Main.unity 배선 확인.");

            yield return new WaitForSeconds(SettleWaitSeconds);

            StickmanBlackboard bb = _agent.Blackboard;
            _originalConfig = bb.Config;
            _originalIntent = bb.IntentSource;
            _originalPoller = bb.FootholdPoller;
            _savedOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;

            _clonedConfig = Object.Instantiate(_originalConfig);
            bb.Config = _clonedConfig;

            // 씬의 실제 물리 충돌면 상단을 읽는다(상수를 다시 적지 않는다 — 이 프로젝트가 두 번 겪은
            // "같은 값을 두 곳에서 따로 계산해 어긋나는" 실패를 피하기 위함).
            GameObject physicsGround = GameObject.Find("PhysicsGround");
            Assert.IsNotNull(physicsGround, $"{LogPrefix} 씬에서 PhysicsGround를 찾지 못했습니다 — " +
                "이 테스트는 '착지 순간 실제 물리 충돌이 일어나는' 배치를 전제로 합니다.");
            var groundBox = physicsGround.GetComponent<BoxCollider2D>();
            Assert.IsNotNull(groundBox, $"{LogPrefix} PhysicsGround에 BoxCollider2D가 없습니다.");
            _groundWorldY = groundBox.bounds.max.y;

            Camera cam = bb.MainCamera;
            float w = Screen.width;
            float h = Screen.height;
            Vector2 groundOs = ScreenCoordinateConverter.WorldToOsScreen(cam,
                new Vector2(0f, _groundWorldY), _clonedConfig, out _);
            Assert.Less(groundOs.y, h, $"{LogPrefix} 준비 실패 — 물리 바닥 상단이 화면 아래로 벗어납니다.");

            _service = new TestFootholdService();
            _service.Footholds.Add(new PlatformFoothold(FlatGroundHandle,
                new Rect(0f, groundOs.y, w, Mathf.Max(1f, h - groundOs.y)), true));
            bb.FootholdPoller = new FootholdPoller(_service, _clonedConfig);

            _intent = new ScriptedIntentSource { MoveInputX = 0f };
            bb.IntentSource = _intent;

            // 캐릭터를 바닥 정중앙에 세운다.
            Vector2 start = new Vector2(0f, _groundWorldY);
            bb.Body.position = start;
            bb.Body.transform.position = new Vector3(start.x, start.y, bb.Body.transform.position.z);
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = FlatGroundHandle;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);

            // 직립 중립 포즈가 자리를 잡을 시간(지수 감쇠 보간).
            yield return new WaitForSeconds(0.6f);

            _head = FindChildByName(bb.Body.transform, "Head");
            Assert.IsNotNull(_head, $"{LogPrefix} 프리팹에서 Head를 찾지 못했습니다 — 머리 높이 기반 검증이 불가능합니다.");
            // ★ 머리 높이는 **월드 Y가 아니라 루트 Transform 기준 로컬 높이**로 잰다.
            // 이유(실측으로 배운 것): 이 프로젝트는 Physics2D.autoSyncTransforms가 꺼져 있어
            // Rigidbody2D.position에 대입한 값(FallState.ConfirmLanding의 착지 스냅이 그렇게 한다)이
            // 다음 물리 스텝의 되쓰기 전까지 Transform에 반영되지 않는다. 그 한 스텝 동안 Body.position과
            // Transform.position이 최대 0.2유닛 넘게 어긋나므로, 월드 Y로 재면 **포즈와 무관한 그 차이가
            // 그대로 측정값에 섞여 들어간다**(처음에 실제로 그렇게 재서 0.22유닛짜리 유령 오차를 봤다).
            // 루트 Transform 기준으로 재면 남는 것은 순수하게 포즈가 만든 높이뿐이다.
            _standingHeadY = _head.position.y - bb.Body.transform.position.y;
            _characterHeight = bb.CharacterHeightWorld;

            Assert.AreEqual(_groundWorldY, bb.Body.position.y, 0.05f,
                $"{LogPrefix} 준비 실패 — 캐릭터가 물리 바닥 상단에 서 있지 않습니다.");

            var bodyColliders = bb.Body.GetComponents<Collider2D>();
            string colliderReport = "";
            foreach (var c in bodyColliders)
            {
                colliderReport += $"{c.GetType().Name}(enabled={c.enabled},trigger={c.isTrigger}) ";
            }
            Debug.Log($"{LogPrefix} 충돌 배선 진단 — 루트 bodyType={bb.Body.bodyType}, simulated={bb.Body.simulated}, " +
                $"레이어={bb.Body.gameObject.layer}, 콜라이더=[{colliderReport}], " +
                $"물리바닥 레이어={physicsGround.layer}, 지금 바닥과 접촉={bb.Body.IsTouching(groundBox)}, " +
                $"레이어 충돌 허용={!Physics2D.GetIgnoreLayerCollision(bb.Body.gameObject.layer, physicsGround.layer)}.");

            Debug.Log($"{LogPrefix} 준비 완료 — 물리바닥 상단 월드Y={_groundWorldY:F3}, 발판 OS y={groundOs.y:F1}, " +
                $"신장={_characterHeight:F3}유닛(배율 {_clonedConfig.ResolveCharacterScale():F2}), " +
                $"서 있을 때 머리 로컬높이={_standingHeadY:F3}, 구르기 임계={_clonedConfig.rollLandingHeightThreshold:F2}유닛, " +
                $"랙돌 임계 충격량={_clonedConfig.ragdollForceThreshold:F1}, 중력배율={_clonedConfig.gravityScale:F1}.");
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                if (root.GetChild(i).name == name) return root.GetChild(i);
            }
            return null;
        }

        /// <summary>지정한 높이만큼 위로 순간이동시킨 뒤 Fall로 강제 전이하고, 착지 결과를 관찰해 돌려준다.</summary>
        private IEnumerator DropAndObserve(float dropUnits, DropObservation result)
        {
            StickmanBlackboard bb = _agent.Blackboard;
            StickmanPoseAnimator pose = bb.GetPoseAnimator();

            Vector2 from = new Vector2(0f, _groundWorldY + dropUnits);
            bb.Body.position = from;
            bb.Body.transform.position = new Vector3(from.x, from.y, bb.Body.transform.position.z);
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = 0L;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(StickmanStateId.Fall, isForcedInterrupt: true);

            result.MinKneeSignedProduct = float.MaxValue;
            result.WorstFootGroundError = 0f;

            float elapsed = 0f;
            bool everLeftCrouch = false;
            while (elapsed < MaxObserveSeconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
                StickmanStateId state = bb.Machine.CurrentStateId;

                if (state == StickmanStateId.Ragdoll) result.SawRagdoll = true;
                if (state == StickmanStateId.Getup) result.SawGetup = true;

                if (state == StickmanStateId.LandingCrouch)
                {
                    result.SawLandingCrouch = true;
                    result.CrouchSeconds += Time.deltaTime;

                    // (A) 머리가 얼마나 내려갔는가 — 루트는 착지 스냅으로 지면에 고정돼 있으므로
                    //     이 차이는 전부 포즈의 결과다.
                    float rootTransformY = bb.Body.transform.position.y;
                    result.MaxHeadDrop = Mathf.Max(result.MaxHeadDrop,
                        _standingHeadY - (_head.position.y - rootTransformY));

                    // (C) 무릎 굽힘 크기와 부호 일관성.
                    pose.GetJointAngles(out float leftKnee, out float rightKnee, out _, out _);
                    result.MaxKneeBendAbs = Mathf.Max(result.MaxKneeBendAbs,
                        Mathf.Max(Mathf.Abs(leftKnee), Mathf.Abs(rightKnee)));
                    result.MinKneeSignedProduct = Mathf.Min(result.MinKneeSignedProduct, leftKnee * rightKnee);

                    // (B) 낮은 쪽 발이 지면에 붙어 있는가.
                    pose.GetFootWorldPositions(out Vector2 leftFoot, out Vector2 rightFoot);
                    float lowestFootY = Mathf.Min(leftFoot.y, rightFoot.y);
                    // 발도 같은 이유로 Body.position이 아니라 **루트 Transform**과 비교한다 —
                    // 발 좌표 자체가 Transform 계층에서 나오므로 기준도 같은 공간이어야 한다.
                    float footError = Mathf.Abs(lowestFootY - rootTransformY);
                    if (footError > result.WorstFootGroundError)
                    {
                        result.WorstFootGroundError = footError;
                        pose.GetUpperAngles(out float lHip, out float rHip, out _, out _);
                        result.WorstFootFrameDetail =
                            $"경과={result.CrouchSeconds:F3}초, 루트Transform Y={rootTransformY:F3}" +
                            $"(Body.position Y={bb.Body.position.y:F3}), " +
                            $"왼발Y={leftFoot.y:F3}, 오른발Y={rightFoot.y:F3}, " +
                            $"왼엉덩이={lHip:F1}도/왼무릎={leftKnee:F1}도, 오른엉덩이={rHip:F1}도/오른무릎={rightKnee:F1}도, " +
                            $"머리하강={(_standingHeadY - (_head.position.y - rootTransformY)):F3}";
                    }
                }
                else if (result.SawLandingCrouch)
                {
                    everLeftCrouch = true;
                }

                if ((state == StickmanStateId.Idle || state == StickmanStateId.Walk) &&
                    (everLeftCrouch || elapsed > 1.2f))
                {
                    result.Settled = true;
                    break;
                }
            }

            result.FinalState = bb.Machine.CurrentStateId;
            Debug.Log($"{LogPrefix} 낙하 {dropUnits:F2}유닛 관찰 — 무릎앉아={result.SawLandingCrouch}" +
                $"({result.CrouchSeconds:F2}초), 랙돌={result.SawRagdoll}, 기상={result.SawGetup}, " +
                $"복귀={result.Settled}({result.FinalState}), 머리 최대 하강={result.MaxHeadDrop:F3}유닛" +
                $"(신장의 {(_characterHeight > 0f ? result.MaxHeadDrop / _characterHeight * 100f : 0f):F1}%), " +
                $"최대 무릎굽힘={result.MaxKneeBendAbs:F1}도, 발-지면 최대오차={result.WorstFootGroundError:F3}유닛, " +
                $"최종 Y={bb.Body.position.y:F3}(지면 {_groundWorldY:F3}), 총 {elapsed:F2}초. " +
                $"최악 프레임 상세 -> {result.WorstFootFrameDetail}");
        }

        // ============================================================================
        // (1) 핵심 — 높은 낙하는 랙돌로 가지 않고 무릎앉아를 거쳐 Idle로 복귀한다
        // ============================================================================

        [UnityTest]
        public IEnumerator HighFallLandsInCrouchWithoutRagdoll()
        {
            yield return SetUpFlatGround();

            // 전제: 이 낙하가 구르기 임계값을 확실히 넘는다(넘지 않으면 연출 자체가 발동하지 않는다).
            Assert.Greater(HighDropUnits, _clonedConfig.rollLandingHeightThreshold,
                $"{LogPrefix} 전제 실패 — 낙하 높이가 구르기 임계값 이하라 연출이 애초에 발동하지 않습니다.");
            // "랙돌로 가지 않는다"가 우연이 아님을 보이는 책임은 아래 (4)번(차단 스위치 대조)에 있다 —
            // 이 정상 착지 경로에서는 FallState의 스냅이 먼저 하강 속도를 지워 충돌 충격량이 0이 되므로,
            // 여기서 "충격량이 임계값을 넘었을 것"이라고 전제하면 사실과 다르다(실측 로그 [착지충격]).

            var obs = new DropObservation();
            yield return DropAndObserve(HighDropUnits, obs);

            Assert.IsFalse(obs.SawRagdoll,
                $"{LogPrefix} 정상 낙하가 RAGDOLL로 전이했습니다 — 착지 충격이 외력으로 새고 있습니다" +
                "(StickConfig.landingImpactRagdollShield / RagdollImpactResolver.IsOwnLandingContact 회귀).");
            Assert.IsFalse(obs.SawGetup, $"{LogPrefix} Getup이 관측되었습니다 — 랙돌을 거쳤다는 뜻입니다.");
            Assert.IsTrue(obs.SawLandingCrouch,
                $"{LogPrefix} 무릎앉아 상태로 전이하지 않았습니다 — FallState.ConfirmLanding의 전이 또는 " +
                "StickmanAgent의 LandingCrouch 등록이 빠졌을 가능성이 큽니다.");
            Assert.IsTrue(obs.Settled,
                $"{LogPrefix} {MaxObserveSeconds}초 안에 Idle/Walk로 복귀하지 못했습니다(최종 {obs.FinalState}).");

            // (A) 절대 조건 — 머리가 신장의 12% 이상 내려가야 "앉았다"고 부를 수 있다.
            float minHeadDrop = _characterHeight * 0.12f;
            Assert.Greater(obs.MaxHeadDrop, minHeadDrop,
                $"{LogPrefix} 무릎앉아 중 머리가 충분히 내려가지 않았습니다({obs.MaxHeadDrop:F3} <= {minHeadDrop:F3}유닛). " +
                "상태 전이만 일어나고 포즈가 적용되지 않았을 가능성이 큽니다" +
                "(StickmanBlackboard.TickPose의 LandingCrouch 분기가 빠지면 Idle 중립 포즈가 매 프레임 덧씌워집니다).");

            // (C) 절대 조건 — 무릎이 실제로 깊게 접혔고, 좌우가 같은 방향으로 접혔다.
            Assert.Greater(obs.MaxKneeBendAbs, 45f,
                $"{LogPrefix} 무릎 굽힘이 {obs.MaxKneeBendAbs:F1}도에 그쳤습니다 — 무릎앉아로 보이지 않습니다.");
            Assert.Greater(obs.MinKneeSignedProduct, 0f,
                $"{LogPrefix} 좌우 무릎이 서로 반대 방향으로 접혔습니다(부호 곱 {obs.MinKneeSignedProduct:F1}) — " +
                "사람 관절은 한 방향으로만 접힙니다(관절 각도 제한 좌우 반전 회귀).");

            // (B) 절대 조건 — 앉은 동안에도 낮은 쪽 발이 지면을 벗어나지 않는다.
            // 허용오차는 신장의 3%다. ApplyLandingCrouchPose가 각도를 먼저 확정한 뒤 그 각도로 몸 높이를
            // 정하므로(ReapplyCurrentAngles) 이론상 오차는 0이고, 남는 것은 부동소수/마디 길이 반올림뿐이다.
            // 이 조건이 걸러내려는 오답은 "몸만 내리고 다리 각도를 안 바꾼" 구현(발이 앉은 깊이 전체만큼
            // 뜬다 — 신장의 16%+)이라, 3%면 충분히 구분되면서도 우연한 통과를 허용하지 않는다.
            float footTolerance = _characterHeight * 0.03f;
            Assert.Less(obs.WorstFootGroundError, footTolerance,
                $"{LogPrefix} 무릎앉아 중 발이 지면에서 {obs.WorstFootGroundError:F3}유닛 어긋났습니다" +
                $"(허용 {footTolerance:F3}). 앉는 깊이를 다리 각도에서 유도하지 않고 몸만 내렸을 때 나오는 증상입니다.");

            // 착지 높이 자체도 함께 잠근다(연출이 끝난 뒤 지면에 정확히 서 있는가).
            Assert.AreEqual(_groundWorldY, _agent.Blackboard.Body.position.y, 0.06f,
                $"{LogPrefix} 연출이 끝난 뒤 발 높이가 지면과 어긋났습니다.");
        }

        // ============================================================================
        // (2) 낮은 낙차(Dock 단차)에서는 연출이 발동하지 않는다
        // ============================================================================

        [UnityTest]
        public IEnumerator DockStepDropDoesNotTriggerCrouch()
        {
            yield return SetUpFlatGround();

            Assert.Less(DockStepDropUnits, _clonedConfig.rollLandingHeightThreshold,
                $"{LogPrefix} 전제 실패 — Dock 단차({DockStepDropUnits:F3})가 구르기 임계값" +
                $"({_clonedConfig.rollLandingHeightThreshold:F2}) 이상이 되어버렸습니다. " +
                "이 경우 한 계단 내려올 때마다 무릎을 꿇게 됩니다(리더가 명시적으로 금지한 거동).");

            var obs = new DropObservation();
            yield return DropAndObserve(DockStepDropUnits, obs);

            Assert.IsFalse(obs.SawLandingCrouch,
                $"{LogPrefix} 낙차 {DockStepDropUnits:F3}유닛(Dock 단차)에서 무릎앉아가 발동했습니다 — " +
                "임계값 판정이 무력화됐습니다.");
            Assert.IsFalse(obs.SawRagdoll, $"{LogPrefix} 작은 낙차에서 랙돌로 전이했습니다.");
            Assert.IsTrue(obs.Settled, $"{LogPrefix} 작은 낙차에서 Idle/Walk로 복귀하지 못했습니다(최종 {obs.FinalState}).");
        }

        // ============================================================================
        // (3) 높을수록 더 깊이 앉고 더 오래 유지한다 (리더 지시의 직접 잠금)
        // ============================================================================

        [UnityTest]
        public IEnumerator DeeperFallCrouchesDeeperAndLonger()
        {
            yield return SetUpFlatGround();

            Assert.Greater(ModerateDropUnits, _clonedConfig.rollLandingHeightThreshold,
                $"{LogPrefix} 전제 실패 — 대조군 낙하가 임계값 이하라 무릎앉아가 발동하지 않습니다.");

            var shallow = new DropObservation();
            yield return DropAndObserve(ModerateDropUnits, shallow);
            Assert.IsTrue(shallow.SawLandingCrouch, $"{LogPrefix} 대조군({ModerateDropUnits}유닛)에서 무릎앉아가 발동하지 않았습니다.");

            yield return new WaitForSeconds(0.4f);

            var deep = new DropObservation();
            yield return DropAndObserve(HighDropUnits, deep);
            Assert.IsTrue(deep.SawLandingCrouch, $"{LogPrefix} 실험군({HighDropUnits}유닛)에서 무릎앉아가 발동하지 않았습니다.");

            Debug.Log($"{LogPrefix} 램프 대조 — 얕은 낙하 {ModerateDropUnits:F2}유닛: 깊이 {shallow.MaxHeadDrop:F3}유닛 / " +
                $"{shallow.CrouchSeconds:F2}초, 깊은 낙하 {HighDropUnits:F2}유닛: 깊이 {deep.MaxHeadDrop:F3}유닛 / " +
                $"{deep.CrouchSeconds:F2}초.");

            Assert.Greater(deep.MaxHeadDrop, shallow.MaxHeadDrop * 1.05f,
                $"{LogPrefix} 더 높이 떨어졌는데 더 깊이 앉지 않았습니다 — 깊이 램프" +
                "(LandingCrouchState.Enter의 landingCrouchMinDepth01 -> 1 보간)가 작동하지 않습니다.");
            Assert.Greater(deep.CrouchSeconds, shallow.CrouchSeconds * 1.05f,
                $"{LogPrefix} 더 높이 떨어졌는데 더 오래 유지하지 않았습니다 — 지속시간 램프" +
                "(landingCrouchDurationShallow -> landingCrouchDurationDeep 보간)가 작동하지 않습니다.");
        }

        // ============================================================================
        // (4) 네거티브 컨트롤 A — 착지 충격 차단 스위치가 실제로 무언가를 막고 있는가
        // ============================================================================
        //
        // ★ 이 테스트의 시나리오가 왜 "논리 발판이 없는 구간으로의 낙하"인가 (실측으로 바뀐 설계)
        // ────────────────────────────────────────────────────────────────────────────
        // 처음에는 위 (1)과 **똑같은 낙하**에서 차단만 끄면 랙돌이 재현될 것이라 예상했다. 계산상으로는
        // 맞다(질량 1 / 임계 8 / 중력배율 3 -> 1.09유닛만 떨어져도 충격량 8). 그런데 실측하니 그 낙하의
        // 충돌 충격량은 **0.00**이었다(로그 [착지충격]). 이유가 명확하다:
        //   FallState의 스윕 교차 판정이 Update에서 먼저 착지를 확정하고 몸을 발판 상단으로 스냅하면서
        //   하강 속도를 0으로 지운다 -> 그 다음 물리 스텝에서 생기는 접촉은 이미 **정지 상태의 안착
        //   접촉**이라 relativeVelocity가 0이다.
        // 즉 논리 발판이 있는 정상 착지에서는 착지 충격이 애초에 랙돌 경로로 흐르지 않는다.
        //
        // 그렇다고 차단 스위치가 무의미한 것은 아니다 — 이 앱에는 **물리 바닥은 있는데 논리 발판은
        // 없는 구간**이 실제로 존재한다. Editor/SceneBootstrapper.CreateGroundCollider 문서가 명시한
        // 그 상황이다: 화면 최하단 안전망은 Dock 가로 구간에 구멍이 뚫려 있는 반면 PhysicsGround는
        // 전체 폭이라, 그 구간에서 캐릭터는 "물리적으로는 떠받쳐지지만 논리적으로는 접지하지 않는다".
        // 그리로 떨어지면 FallState는 착지를 확정하지 않으므로 스냅도 없고, 몸이 **전속력 그대로**
        // 물리 바닥에 부딪힌다. 그것이 차단 스위치가 실제로 겨냥하는 지점이다.
        //
        // 그래서 이 테스트는 같은 낙하를 스위치만 바꿔 두 번 돌린다 — 껐을 때 실제로 랙돌이 되고,
        // 켰을 때 되지 않아야 스위치가 "무언가를 막고 있다"는 증거가 된다.

        [UnityTest]
        public IEnumerator ShieldDecidesWhetherFullSpeedGroundImpactBecomesRagdoll()
        {
            yield return SetUpFlatGround();

            float impactSpeed = Mathf.Sqrt(2f * 9.81f * _clonedConfig.gravityScale * HighDropUnits);
            Assert.Greater(impactSpeed, _clonedConfig.ragdollForceThreshold,
                $"{LogPrefix} 전제 실패 — 예상 착지 속도 {impactSpeed:F1}이 랙돌 임계값 이하라 " +
                "이 테스트는 아무것도 걸러내지 못합니다.");

            // ── (a) 차단 OFF: 전속력 지면 충돌이 실제로 RAGDOLL이 된다.
            MoveLogicalFootholdAwayFromCharacter();
            _clonedConfig.landingImpactRagdollShield = false;
            var without = new DropObservation();
            yield return DropIntoLogicalVoid(HighDropUnits, without);

            Assert.IsTrue(without.SawRagdoll,
                $"{LogPrefix} 네거티브 컨트롤 실패 — 차단을 껐는데도 전속력 지면 충돌이 랙돌이 되지 않았습니다. " +
                "그렇다면 이 스위치는 아무것도 막고 있지 않다는 뜻이므로 (1)번 테스트의 결론도 재검토해야 합니다. " +
                $"로그 [착지충격]의 충격량을 먼저 확인하세요(물리바닥 상단 {_groundWorldY:F3}).");
            Debug.Log($"{LogPrefix} 네거티브 컨트롤 A-(a) — 차단 OFF에서 전속력 지면 충돌이 실제로 RAGDOLL이 됐습니다.");

            // 랙돌/기상이 끝나 상태가 정리될 때까지 기다린 뒤 같은 조건으로 다시.
            yield return new WaitForSeconds(2.5f);

            // ── (b) 차단 ON: 같은 낙하가 랙돌이 되지 않는다.
            _clonedConfig.landingImpactRagdollShield = true;
            MoveLogicalFootholdAwayFromCharacter();
            var with = new DropObservation();
            yield return DropIntoLogicalVoid(HighDropUnits, with);

            Assert.IsFalse(with.SawRagdoll,
                $"{LogPrefix} 차단을 켰는데도 전속력 지면 충돌이 랙돌이 됐습니다 — " +
                "RagdollImpactResolver.IsOwnLandingContact의 접촉 높이 판정이 성립하지 않았습니다.");
            Debug.Log($"{LogPrefix} 네거티브 컨트롤 A-(b) — 차단 ON에서 같은 충돌이 랙돌로 흐르지 않습니다. " +
                "스위치 하나만 바뀌었으므로 이 차이의 원인은 그 스위치입니다.");
        }

        /// <summary>
        /// 논리 발판을 캐릭터 X에서 치워, "물리 바닥은 있는데 논리 발판은 없는 구간"(실제 앱의 Dock 가로
        /// 구간과 같은 상황)을 만든다. 이 상태에서 떨어지면 FallState가 착지를 확정하지 못해 스냅/속도
        /// 제거가 일어나지 않고, 몸이 전속력 그대로 PhysicsGround에 부딪힌다.
        /// </summary>
        private void MoveLogicalFootholdAwayFromCharacter()
        {
            StickmanBlackboard bb = _agent.Blackboard;
            Camera cam = bb.MainCamera;
            float w = Screen.width;
            float h = Screen.height;
            Vector2 groundOs = ScreenCoordinateConverter.WorldToOsScreen(cam,
                new Vector2(0f, _groundWorldY), _clonedConfig, out _);

            _service.Footholds.Clear();
            // 화면 왼쪽 15%에만 남긴다 — 캐릭터는 x=0(월드 중앙)에서 떨어지므로 그 위/아래에는 논리
            // 발판이 전혀 없다.
            _service.Footholds.Add(new PlatformFoothold(FlatGroundHandle,
                new Rect(0f, groundOs.y, w * 0.15f, Mathf.Max(1f, h - groundOs.y)), true));
            bb.FootholdPoller = new FootholdPoller(_service, _clonedConfig);
            bb.CurrentFootholdHandle = 0L;
            bb.ResetGroundLossTimer();
        }

        /// <summary>
        /// 논리 발판이 없는 구간으로 떨어뜨리고 관찰한다. 착지가 확정되지 않으므로 위 DropAndObserve의
        /// "Idle/Walk 복귀" 종료 조건이 성립하지 않는다 — 정해진 시간 동안 랙돌 발생 여부만 본다.
        /// </summary>
        private IEnumerator DropIntoLogicalVoid(float dropUnits, DropObservation result)
        {
            StickmanBlackboard bb = _agent.Blackboard;
            Vector2 from = new Vector2(0f, _groundWorldY + dropUnits);
            bb.Body.position = from;
            bb.Body.transform.position = new Vector3(from.x, from.y, bb.Body.transform.position.z);
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = 0L;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(StickmanStateId.Fall, isForcedInterrupt: true);

            float elapsed = 0f;
            // 3초면 낙하(약 0.64초) + 충돌 + 랙돌 전이까지 충분하고, 최종 안전망
            // (StickmanBlackboard의 6초 캐릭터 구조)에는 걸리지 않는다.
            while (elapsed < 3f)
            {
                yield return null;
                elapsed += Time.deltaTime;
                StickmanStateId state = bb.Machine.CurrentStateId;
                if (state == StickmanStateId.Ragdoll) result.SawRagdoll = true;
                if (state == StickmanStateId.Getup) result.SawGetup = true;
                if (state == StickmanStateId.LandingCrouch) result.SawLandingCrouch = true;
            }
            result.FinalState = bb.Machine.CurrentStateId;

            Debug.Log($"{LogPrefix} 논리 발판 없는 구간으로 낙하 {dropUnits:F2}유닛 — 차단스위치=" +
                $"{_clonedConfig.landingImpactRagdollShield}, 랙돌={result.SawRagdoll}, 기상={result.SawGetup}, " +
                $"무릎앉아={result.SawLandingCrouch}, 최종 상태={result.FinalState}, " +
                $"최종 Y={bb.Body.position.y:F3}(물리바닥 {_groundWorldY:F3}).");
        }

        // ============================================================================
        // (5) 네거티브 컨트롤 B — 연출을 끄면 무릎앉아가 실제로 사라진다
        // ============================================================================

        [UnityTest]
        public IEnumerator NegativeControl_CrouchDisabledSkipsTheWholePerformance()
        {
            yield return SetUpFlatGround();

            _clonedConfig.landingCrouchEnabled = false;

            var obs = new DropObservation();
            yield return DropAndObserve(HighDropUnits, obs);

            Assert.IsFalse(obs.SawLandingCrouch,
                $"{LogPrefix} 네거티브 컨트롤 실패 — landingCrouchEnabled=false인데도 무릎앉아로 전이했습니다" +
                "(FallState가 스위치를 읽지 않고 있습니다).");
            Assert.AreEqual(0f, obs.MaxHeadDrop, 0.0001f,
                $"{LogPrefix} 네거티브 컨트롤 실패 — 연출을 껐는데 머리 하강이 관측됐습니다.");
            Assert.IsFalse(obs.SawRagdoll,
                $"{LogPrefix} 연출만 껐는데 랙돌이 됐습니다 — 두 스위치가 서로 얽혀 있습니다" +
                "(landingImpactRagdollShield는 landingCrouchEnabled와 독립이어야 합니다).");
            Assert.IsTrue(obs.Settled, $"{LogPrefix} 연출을 껐을 때 Idle/Walk로 복귀하지 못했습니다(최종 {obs.FinalState}).");

            Debug.Log($"{LogPrefix} 네거티브 컨트롤 B 통과 — 스위치를 끄면 예전 거동(착지 즉시 Idle/Walk)으로 정확히 되돌아갑니다.");
        }

        // ============================================================================
        // (6) 진행 곡선 자체 — 눌림/버팀/일어섬 + 반동의 형태가 실제로 그 순서인가
        // ============================================================================

        [Test]
        public void CrouchCurveHasCompressHoldRiseAndRebound()
        {
            const float compress = 0.18f;
            const float hold = 0.24f;
            const float rebound = 0.22f;

            float atStart = LandingCrouchState.EvaluateCrouchCurve(0f, compress, hold, rebound);
            float atCompressEnd = LandingCrouchState.EvaluateCrouchCurve(compress, compress, hold, rebound);
            float midHold = LandingCrouchState.EvaluateCrouchCurve(compress + hold * 0.5f, compress, hold, rebound);
            float atHoldEnd = LandingCrouchState.EvaluateCrouchCurve(compress + hold, compress, hold, rebound);
            float atEnd = LandingCrouchState.EvaluateCrouchCurve(1f, compress, hold, rebound);

            // 눌림 구간이 **앞쪽에서 더 많이** 움직인다(easeOut) — "툭" 받는 느낌의 근거.
            float quarter = LandingCrouchState.EvaluateCrouchCurve(compress * 0.25f, compress, hold, rebound);

            float minValue = float.MaxValue;
            float minAt = 0f;
            for (int i = 0; i <= 200; i++)
            {
                float t = i / 200f;
                float v = LandingCrouchState.EvaluateCrouchCurve(t, compress, hold, rebound);
                if (v < minValue) { minValue = v; minAt = t; }
            }

            Debug.Log($"{LogPrefix} 곡선 실측 — 시작={atStart:F3}, 눌림끝={atCompressEnd:F3}, 버팀중={midHold:F3}, " +
                $"버팀끝={atHoldEnd:F3}, 끝={atEnd:F3}, 눌림 25%지점={quarter:F3}, 최저={minValue:F3}(t={minAt:F3}).");

            Assert.AreEqual(0f, atStart, 0.0001f, $"{LogPrefix} 곡선이 0(직립)에서 시작하지 않습니다.");
            Assert.AreEqual(1f, atCompressEnd, 0.001f, $"{LogPrefix} 눌림 구간 끝에서 최대 깊이(1)에 도달하지 않습니다.");
            Assert.AreEqual(1f, midHold, 0.001f, $"{LogPrefix} 버팀 구간이 최대 깊이를 유지하지 않습니다.");
            Assert.AreEqual(1f, atHoldEnd, 0.001f, $"{LogPrefix} 버팀 구간 끝이 최대 깊이가 아닙니다.");
            Assert.AreEqual(0f, atEnd, 0.001f, $"{LogPrefix} 곡선이 정확히 0(직립)으로 끝나지 않습니다 — " +
                "연출이 끝난 뒤 자세가 중립으로 돌아오지 않으면 Idle 포즈와 이어질 때 튑니다.");
            Assert.Greater(quarter, 0.4f,
                $"{LogPrefix} 눌림 구간이 easeOut이 아닙니다(25% 지점에서 {quarter:F3}) — 앞쪽에서 크게 움직여야 " +
                "'스스로 앉는 것'이 아니라 '충격을 받는 것'으로 보입니다.");
            Assert.Less(minValue, -rebound * 0.8f,
                $"{LogPrefix} 반동이 사실상 나타나지 않았습니다(최저 {minValue:F3}, 설정 {rebound:F2}) — " +
                "일어서면서 중립을 지나쳐야 '눌렸다가 펴지는 리듬'이 보입니다.");
            Assert.Greater(minAt, compress + hold,
                $"{LogPrefix} 반동이 일어서는 구간이 아니라 앞쪽에서 나타났습니다(t={minAt:F3}).");
        }
    }
}
