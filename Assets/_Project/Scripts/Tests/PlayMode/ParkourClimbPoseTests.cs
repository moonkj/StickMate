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
    /// ★ 사용자 실사용 신고(2026-09-01): "독을 올라갈때도 지금처럼 어설프게 점프로 올라가는게 아니고
    /// 사람처럼 손으로 집고 다리를 올려서 올라가야지".
    ///
    /// <para>이 파일이 재는 것은 <b>연출의 감상평이 아니라 기하</b>다. "손으로 집고"가 참이려면
    /// 실제 손끝 Transform이 붙잡은 발판 상단선 위에 있어야 하고, "다리를 올려서"가 참이려면 실제 발끝
    /// Transform이 그 선까지 올라와야 한다. 둘 다 <b>포즈 애니메이터의 내부 계산을 한 줄도 참조하지
    /// 않고</b> 계층의 Transform에서 직접 잰다 — 그 계산이 틀리면 여기서 반드시 걸린다
    /// (LedgeHangHandAlignmentTests가 같은 이유로 같은 방식을 쓴다).</para>
    ///
    /// <para><b>왜 이 검사가 필요한가</b>: 마디 길이/부착점은 <b>루트 로컬 유닛</b>인데 발판 좌표는
    /// 월드 유닛이다. 둘을 섞으면 배포 배율(0.75, 루트 localScale = 1)에서만 우연히 맞고 사용자가
    /// 크기 다이얼을 돌리는 순간 통째로 어긋난다 — 이 저장소가 매달리기에서 이미 한 번 겪은 사고다
    /// (BUG-LH-B1, 배율 0.35에서 손끝이 1.0유닛 아래). 그래서 <b>기본 배율이 아닌 배율에서도</b> 잰다.</para>
    ///
    /// <para>시간 예산은 전부 <b>벽시계(초)</b>다(CLAUDE.md). 프레임 수 예산은 이 저장소의 배치모드
    /// PlayMode가 수천 fps로 도는 탓에 밀리초가 되어 버린다.</para>
    /// </summary>
    public sealed class ParkourClimbPoseTests
    {
        private const string LogPrefix = "[등반자세]";

        private const long WallHandle = 7301L;
        private const long GroundHandle = 7302L;

        /// <summary>씬 부팅 후 배회/접지가 안정될 때까지(벽시계 초).</summary>
        private const float SettleWaitSeconds = 2.5f;

        /// <summary>이 테스트에서 쓰는 등반 시간(벽시계 초). 프로덕션 기본값보다 길게 잡아 한 박자마다
        /// 표본이 넉넉히 쌓이게 한다 — 진행도별 판정을 하려면 표본 밀도가 필요하고, 그렇다고 프레임 수로
        /// 잡으면 배치모드에서 예산이 통째로 무의미해진다.</summary>
        private const float ClimbDurationSeconds = 4f;

        /// <summary>등반이 끝날 때까지 기다리는 상한(벽시계 초).</summary>
        private const float ClimbTimeoutSeconds = 12f;

        /// <summary>오를 턱의 높이를 신장의 몇 배로 만들 것인가. 실사용(사용자 Dock, 낙차 1.637유닛 /
        /// 신장 1.706유닛)과 같은 비율이다 — "가슴 높이보다 조금 낮은 턱"이 이 앱의 대표 사례다.</summary>
        private const float WallHeightOfCharacterHeight = 0.96f;

        /// <summary>손/발이 모서리에 "붙어 있다"고 볼 허용오차(신장 대비). 실측 최악값은 0.03유닛
        /// (신장의 1.8%)이고 여기에 포즈 스무딩 지연분의 여유를 더한 값이다. 단위 환산이 빠지는
        /// 종류의 회귀는 이보다 한 자릿수 큰 오차를 내므로(BUG-LH-B1 실측 1.0유닛) 이 여유로도 잡힌다.</summary>
        private const float GripToleranceOfHeight = 0.10f;

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
        private StickConfig _originalConfig;
        private StickConfig _clonedConfig;
        private IMovementIntentSource _originalIntent;
        private FootholdPoller _originalPoller;
        private Vector2 _savedOrigin;
        private float _savedScale = -1f;

        private TestFootholdService _service;
        private ScriptedIntentSource _intent;

        [TearDown]
        public void TearDown()
        {
            if (_agent != null)
            {
                if (_savedScale > 0f) _agent.ApplyCharacterScale(_savedScale, "테스트 복원");
                if (_agent.Blackboard != null)
                {
                    if (_originalConfig != null) _agent.Blackboard.Config = _originalConfig;
                    if (_originalIntent != null) _agent.Blackboard.IntentSource = _originalIntent;
                    if (_originalPoller != null) _agent.Blackboard.FootholdPoller = _originalPoller;
                }
            }
            ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOrigin;
            if (_clonedConfig != null) Object.DestroyImmediate(_clonedConfig);
            _clonedConfig = null;
            _agent = null;
            _savedScale = -1f;
        }

        /// <summary>마디 끝(손끝/발끝)의 월드 좌표. 마디의 원점이 관절이고 선이 로컬 −y로 length만큼
        /// 그려지므로(Editor/SceneBootstrapper.CreateLimbSegment) 끝점의 로컬 좌표는 (0, −length)다.
        /// TransformPoint를 쓰므로 루트 스케일까지 자동으로 반영된다.</summary>
        private static bool TryLimbTipWorld(Transform root, string limbName, out Vector3 tip)
        {
            tip = default;
            if (root == null) return false;
            Transform upper = root.Find(limbName);
            if (upper == null) return false;
            Transform end = upper.Find(limbName + "Lower") ?? upper;
            var box = end.GetComponent<BoxCollider2D>();
            float length = box != null ? box.size.y : 0f;
            tip = end.TransformPoint(new Vector3(0f, -length, 0f));
            return true;
        }

        /// <summary>두 팔(또는 두 다리) 중 <b>모서리에 더 가까운 쪽</b>의 끝점 Y. 등반은 좌우 비대칭이라
        /// (한 손은 앞, 한 발은 턱 위) 평균이 아니라 "가까운 쪽"이 의미 있는 값이다.</summary>
        private static float NearestTipYTo(Transform root, string a, string b, float targetY)
        {
            float best = float.NaN;
            if (TryLimbTipWorld(root, a, out Vector3 pa)) best = pa.y;
            if (TryLimbTipWorld(root, b, out Vector3 pb))
            {
                if (float.IsNaN(best) || Mathf.Abs(pb.y - targetY) < Mathf.Abs(best - targetY)) best = pb.y;
            }
            return best;
        }

        private IEnumerator SetUpClimbRig(float characterScale)
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다.");

            yield return new WaitForSeconds(SettleWaitSeconds);

            StickmanBlackboard bb = _agent.Blackboard;
            _originalConfig = bb.Config;
            _originalIntent = bb.IntentSource;
            _originalPoller = bb.FootholdPoller;
            _savedOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            _savedScale = _agent.CurrentCharacterScale;
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;

            _clonedConfig = Object.Instantiate(_originalConfig);
            bb.Config = _clonedConfig;
            _clonedConfig.parkourClimbDuration = ClimbDurationSeconds;

            // ApplyCharacterScale는 "이미 그 배율"이면 false를 돌려준다(변경 없음). 기본 배율로 도는
            // 테스트에서 그것을 실패로 읽으면 안 되므로, 실제로 달라질 때만 적용하고 검사한다.
            if (!Mathf.Approximately(_agent.CurrentCharacterScale, characterScale))
            {
                Assert.IsTrue(_agent.ApplyCharacterScale(characterScale, "등반 자세 실측"),
                    $"{LogPrefix} 캐릭터 배율 {characterScale:F2} 적용에 실패했습니다.");
            }
            yield return null;

            float w = Screen.width;
            float h = Screen.height;
            float groundTopOs = h * 0.82f;

            // 원하는 **월드** 낙차를 OS 픽셀로 환산한다. 상수를 베끼지 않고 실제 변환기에게 물어본다 —
            // 카메라/DPI가 바뀌어도 이 리그가 같은 비율의 턱을 만든다.
            Camera cam = bb.MainCamera;
            float y0 = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.5f, groundTopOs), 10f, _clonedConfig).y;
            float y1 = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(w * 0.5f, groundTopOs - 100f), 10f, _clonedConfig).y;
            float unitsPerPixel = Mathf.Abs(y1 - y0) / 100f;
            Assert.Greater(unitsPerPixel, 0.0001f, $"{LogPrefix} 좌표 변환이 성립하지 않습니다.");

            float wantRiseWorld = WallHeightOfCharacterHeight * bb.CharacterHeightWorld;
            float wallTopOs = groundTopOs - wantRiseWorld / unitsPerPixel;

            // ★ 바닥은 **턱이 시작되는 자리에서 끝나야** 한다. GroundSensor.TryFindClimbableWall은
            // "지금 딛고 있는 발판의 진행방향 경계 근처인가"를 먼저 보므로(그 게이트가 없으면 화면
            // 한복판에서도 벽을 잡는다), 전폭 바닥 위에 서 있으면 벽을 영원히 찾지 못한다.
            float wallLeftOs = w * 0.55f;
            _service = new TestFootholdService();
            _service.Footholds.Add(new PlatformFoothold(WallHandle,
                new Rect(wallLeftOs, wallTopOs, w * 0.40f, (groundTopOs - wallTopOs) + h * 0.10f), true));
            _service.Footholds.Add(new PlatformFoothold(GroundHandle,
                new Rect(0f, groundTopOs, wallLeftOs, h * 0.16f), false));

            bb.FootholdPoller = new FootholdPoller(_service, _clonedConfig);

            _intent = new ScriptedIntentSource { MoveInputX = 1f };
            bb.IntentSource = _intent;

            // 턱의 왼쪽 모서리 바로 앞(= 경계 근접 게이트 안쪽)에 세운다. 거리는 상수를 베끼지 않고
            // 그 게이트가 실제로 쓰는 유도값(EdgeProbeReachWorld)의 절반으로 잡는다 — 캐릭터 배율이
            // 바뀌면 게이트도 함께 변하므로 고정 픽셀로 잡으면 작은 배율에서만 조용히 깨진다.
            float standBack = bb.EdgeProbeReachWorld * 0.5f;
            Vector3 wallEdge = ScreenCoordinateConverter.OsScreenToWorld(cam,
                new Vector2(wallLeftOs, groundTopOs), 10f, _clonedConfig);
            Vector3 stand = new Vector3(wallEdge.x - standBack, wallEdge.y, wallEdge.z);
            bb.Body.position = new Vector2(stand.x, stand.y);
            bb.Body.transform.position = new Vector3(stand.x, stand.y, bb.Body.transform.position.z);
            bb.Body.linearVelocity = Vector2.zero;
            bb.CurrentFootholdHandle = GroundHandle;
            bb.ResetGroundLossTimer();
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);
            yield return null;

            Debug.Log($"{LogPrefix} 리그 준비 — 화면 {w:F0}x{h:F0}, 배율 {characterScale:F2}" +
                $"(신장 {bb.CharacterHeightWorld:F3}유닛), 목표 낙차 {wantRiseWorld:F3}유닛, " +
                $"선 자리 월드=({stand.x:F3},{stand.y:F3}).");
        }

        /// <summary>등반에 진입시키고 매 프레임 손끝/발끝을 실측한다. 진행도는 <b>설정값에서 유도</b>한다
        /// (프로덕션 상수를 숫자로 베끼지 않는다는 규약 — 여기서도 박자 경계를 config에서 읽는다).</summary>
        private IEnumerator RunClimbAndMeasure(System.Action<float, float, float, int> report)
        {
            StickmanBlackboard bb = _agent.Blackboard;
            Transform root = _agent.transform;

            bb.Machine.ChangeState(StickmanStateId.ParkourClimb, isForcedInterrupt: true);
            yield return null;
            Assert.AreEqual(StickmanStateId.ParkourClimb, bb.Machine.CurrentStateId,
                $"{LogPrefix} 전제 실패 — ParkourClimb에 진입하지 못했습니다(오를 벽을 찾지 못했을 수 있습니다).");

            float reachFraction = _clonedConfig.parkourClimbReachFraction;
            float hangFraction = _clonedConfig.parkourClimbHangFraction;
            float pullFraction = _clonedConfig.parkourClimbPullFraction;
            float duration = _clonedConfig.parkourClimbDuration;

            // 손이 모서리를 잡고 있어야 하는 구간 = 뻗기가 끝난 시점부터 당기기의 앞부분까지.
            // 그 뒤로는 어깨가 모서리보다 팔 길이 이상 위로 올라가 "손이 닿는 자세가 존재하지 않는다"
            // (StickmanPoseAnimator.ClimbGripReleaseReach 문서). 그 구간까지 잡으라고 요구하면
            // 물리적으로 불가능한 것을 요구하는 테스트가 된다.
            float gripWindowEnd = hangFraction + (pullFraction - hangFraction) * 0.35f;

            float elapsed = 0f;
            float worstGripError = 0f;
            float bestFootError = float.PositiveInfinity;
            float lastFootError = float.NaN;
            int gripSamples = 0;

            while (bb.Machine.CurrentStateId == StickmanStateId.ParkourClimb && elapsed < ClimbTimeoutSeconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
                float progress = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;

                if (!bb.TryGetFootholdTopWorldY(WallHandle, out float ledgeY)) continue;

                float footY = NearestTipYTo(root, "LeftLeg", "RightLeg", ledgeY);
                if (!float.IsNaN(footY))
                {
                    float fe = Mathf.Abs(footY - ledgeY);
                    if (progress > hangFraction) bestFootError = Mathf.Min(bestFootError, fe);
                    lastFootError = fe;
                }

                if (progress < reachFraction || progress > gripWindowEnd) continue;
                float handY = NearestTipYTo(root, "LeftArm", "RightArm", ledgeY);
                if (float.IsNaN(handY)) continue;
                worstGripError = Mathf.Max(worstGripError, Mathf.Abs(handY - ledgeY));
                gripSamples++;
            }

            Assert.Less(elapsed, ClimbTimeoutSeconds,
                $"{LogPrefix} 등반이 {ClimbTimeoutSeconds:F0}초 안에 끝나지 않았습니다.");
            Assert.Greater(gripSamples, 10,
                $"{LogPrefix} 손 실측 표본이 {gripSamples}개뿐입니다 — 등반이 너무 빨리 지나갔습니다.");

            report(worstGripError, bestFootError, lastFootError, gripSamples);
        }

        /// <summary>
        /// ★ "손으로 집고" — 뻗기가 끝난 뒤부터 당기기 초반까지, 손끝이 <b>붙잡은 발판 상단선</b> 위에
        /// 있어야 한다. 그리고 "다리를 올려서" — 등반 도중 발끝이 그 선까지 올라와야 한다.
        /// </summary>
        [UnityTest]
        public IEnumerator HandsGripLedgeAndFootReachesItAtDefaultScale()
        {
            yield return SetUpClimbRig(_agentDefaultScale());
            yield return VerifyGripAndFoot();
        }

        /// <summary>
        /// ★ 같은 검사를 <b>다른 캐릭터 배율</b>에서 한 번 더. 마디 길이는 루트 로컬 유닛이고 발판은
        /// 월드 유닛이라, 환산이 빠지면 <b>여기서만</b> 깨진다(BUG-LH-B1과 같은 계열의 회귀 방지).
        /// </summary>
        [UnityTest]
        public IEnumerator HandsGripLedgeAndFootReachesItAtSmallScale()
        {
            yield return SetUpClimbRig(0.45f);
            yield return VerifyGripAndFoot();
        }

        private float _agentDefaultScale() => _savedScale > 0f ? _savedScale : 0.75f;

        private IEnumerator VerifyGripAndFoot()
        {
            StickmanBlackboard bb = _agent.Blackboard;
            float height = bb.CharacterHeightWorld;
            float tolerance = GripToleranceOfHeight * height;

            float grip = 0f, foot = 0f, last = 0f;
            int samples = 0;
            yield return RunClimbAndMeasure((g, f, l, n) => { grip = g; foot = f; last = l; samples = n; });

            Debug.Log($"{LogPrefix} 실측 — 신장 {height:F3}유닛, 허용오차 {tolerance:F3}유닛, 표본 {samples}개. " +
                $"손끝 최악 오차 {grip:F4}유닛({grip / height * 100f:F1}% 신장), " +
                $"발끝 최소 오차 {foot:F4}유닛, 마지막 프레임 발끝 오차 {last:F4}유닛.");

            Assert.LessOrEqual(grip, tolerance,
                $"{LogPrefix} 손끝이 턱 모서리에서 최대 {grip:F3}유닛 떨어졌습니다(허용 {tolerance:F3}). " +
                "\"손으로 집고 올라간다\"가 성립하지 않습니다 — 루트 로컬/월드 단위 환산이 빠졌거나 " +
                "그립 IK 목표가 모서리를 향하고 있지 않습니다.");

            Assert.LessOrEqual(foot, tolerance,
                $"{LogPrefix} 등반 내내 발끝이 턱 상단까지 올라오지 못했습니다(최소 오차 {foot:F3}유닛, " +
                $"허용 {tolerance:F3}). \"다리를 올려서\"가 성립하지 않습니다.");

            Assert.LessOrEqual(last, tolerance * 2f,
                $"{LogPrefix} 등반이 끝나는 프레임에 발끝이 턱에서 {last:F3}유닛 떨어져 있습니다 — " +
                "Idle로 넘어가는 순간 포즈가 튑니다(시각 오프셋이 진행도 1에서 0으로 수렴하지 않습니다).");
        }
    }
}
