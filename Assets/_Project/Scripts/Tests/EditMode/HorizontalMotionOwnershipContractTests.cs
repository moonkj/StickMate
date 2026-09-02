using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using StickMate.Core;
using StickMate.States;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-02 — 두 가지 계약을 잠근다. 둘 다 같은 신고에서 나왔다
    /// ("그라피티 그릴때 윈도우버전은 캐릭터가 미끄러져이동함").
    ///
    /// <list type="number">
    ///   <item><b>수평 소유권 계약</b> — <see cref="StickmanBlackboard.IsHorizontalMotionSelfManaged"/>의
    ///         멤버십. 물리 거동 자체는 PlayMode(HorizontalDriftSafetyNetTests)가 재고, 여기서는
    ///         <b>목록이 조용히 바뀌는 것</b>을 잡는다.</item>
    ///   <item><b>연출 종료 분류 계약</b> — <see cref="SpectacleExitClassification"/>과, 그것을
    ///         <b>실제로 참조하는지</b>에 대한 디렉터 전수 감사.</item>
    /// </list>
    ///
    /// <para>★★ 2026-09-02 추가 — <b>축이 두 개 더 붙었다.</b> 리더 지적: "미끄러짐을 막은 그 계약은
    /// <b>속도만 보고 방향 부호는 안 본다</b>"(이 파일의 facing 계열 언급 0건 / velocity 계열 6건).
    /// <list type="number">
    ///   <item><b>(1-B) 방향 부호 소유권</b> — <see cref="StickmanBlackboard.IsFacingSelfManaged"/>
    ///         멤버십 + <b>소스 전수 감사</b>(SetFacingSign을 부르는 상태는 전부 여기 있어야 한다).
    ///         활쏘기 접근 페이즈가 매 프레임 <c>SetFacingSign(진행 방향)</c>을 부르고도 같은 프레임
    ///         뒤쪽 <c>TickPose</c>에 덮여 "몸은 과녁을 보는데 발은 반대로 가는" 그림이 됐다.</item>
    ///   <item><b>(1-C) 화면 끝 제자리걸음</b> —
    ///         <see cref="AutoWanderController.ResolveEffectiveEdgeBoundary"/>. 2026-08-29에 고친
    ///         러닝머신이 <b>멀티모니터에서 되살아났다</b>(게이트 <c>isTrueScreenEdge</c>가 거짓이 된다).</item>
    /// </list></para>
    /// </summary>
    public sealed class HorizontalMotionOwnershipContractTests
    {
        private const string DefaultConfigPath = "Assets/_Project/Data/DefaultStickConfig.asset";

        private static StickConfig LoadDeployedConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<StickConfig>(DefaultConfigPath);
            Assert.IsNotNull(config, $"기본 설정 자산을 찾지 못했습니다: {DefaultConfigPath}");
            return config;
        }

        private static StickConfig NewCodeDefault() => ScriptableObject.CreateInstance<StickConfig>();

        private static IEnumerable<StickmanStateId> AllStates()
            => (StickmanStateId[])Enum.GetValues(typeof(StickmanStateId));

        // ====================================================================
        // (1) 수평 소유권 멤버십 — 목록이 조용히 바뀌면 빨간불
        // ====================================================================

        /// <summary>
        /// 지금 <b>수평 이동을 스스로 소유한다</b>고 선언된 상태들. 리더 승인 12종 +
        /// <see cref="StickmanStateId.LandingCrouch"/>(coder가 추가, 리더에 보고) = 13종이다.
        ///
        /// <para>LandingCrouch를 넣은 근거: <see cref="LandingCrouchState"/>는 이미
        /// <c>landingCrouchHorizontalDamping</c>으로 <b>매 프레임</b> 수평 속도를 죽이고 있고, 그
        /// 코드 주석이 "0으로 즉시 대입하지 않는 이유: 공중에서의 수평 이동이 착지 순간 뚝 끊기면
        /// 오히려 더 부자연스럽다"고 명시한다. 안전망은 상태 Tick <b>직후</b>에 돌므로 여기서 빼면
        /// 그 튜닝된 감쇠가 통째로 죽은 코드가 되고 착지 박자가 바뀐다.</para>
        /// </summary>
        private static readonly StickmanStateId[] ExpectedSelfManaged =
        {
            StickmanStateId.Walk,
            StickmanStateId.Jump,
            StickmanStateId.Fall,
            StickmanStateId.ThrowTumble,
            StickmanStateId.Ragdoll,
            StickmanStateId.Dragged,
            StickmanStateId.RodeoCursor,
            StickmanStateId.LedgeHang,
            StickmanStateId.ParkourClimb,
            StickmanStateId.Runaway,
            StickmanStateId.Archery,
            StickmanStateId.GroundLossHang,
            StickmanStateId.LandingCrouch,
        };

        [Test]
        public void 수평_자기소유_목록이_승인된_구성과_같다()
        {
            var actual = AllStates().Where(StickmanBlackboard.IsHorizontalMotionSelfManaged).ToArray();

            var added = actual.Except(ExpectedSelfManaged).ToArray();
            var removed = ExpectedSelfManaged.Except(actual).ToArray();

            Assert.IsEmpty(added,
                "수평 자기소유 목록에 승인되지 않은 상태가 들어왔습니다: " + string.Join(", ", added) +
                ".\n여기 넣으면 그 상태의 **제자리 표류를 안전망이 더 이상 막지 않는다**. 정말 넣어야 한다면 " +
                "그 상태가 제자리 페이즈 동안 매 프레임 스스로 수평 속도를 죽이는지 먼저 확인하고" +
                "(ArcheryState의 비-Approach 분기가 참고 구현), 이 목록도 함께 갱신해라.");
            Assert.IsEmpty(removed,
                "수평 자기소유 목록에서 상태가 빠졌습니다: " + string.Join(", ", removed) +
                ".\n여기서 빼면 안전망이 그 상태의 수평 속도를 매 프레임 지운다 — 접근 페이즈가 있는 " +
                "상태라면 **영원히 목적지에 도달하지 못한다**.");
        }

        [Test]
        public void 순수_타이머_연출_상태는_전부_안전망의_보호를_받는다()
        {
            // TimedSpectacleState를 공유하는 10종 + Idle/Attack/Getup/WindowTheft.
            // 이들은 몸을 전혀 건드리지 않으므로(States/*.cs에 Body 참조가 한 줄도 없다) 반드시
            // 안전망 대상이어야 한다 — 신고된 Graffiti가 바로 이 부류다.
            StickmanStateId[] pureSpectacles =
            {
                StickmanStateId.Idle,
                StickmanStateId.Attack,
                StickmanStateId.Getup,
                StickmanStateId.WindowTheft,
                StickmanStateId.Graffiti,
                StickmanStateId.DesktopTidy,
                StickmanStateId.BlackholeSummon,
                StickmanStateId.WindowCrash,
                StickmanStateId.TodoReminder,
                StickmanStateId.FocusStart,
                StickmanStateId.FocusComplete,
                StickmanStateId.FocusCancelled,
                StickmanStateId.FocusNudge,
                StickmanStateId.Sulky,
            };

            foreach (StickmanStateId id in pureSpectacles)
            {
                Assert.IsFalse(StickmanBlackboard.IsHorizontalMotionSelfManaged(id),
                    $"{id}는 몸을 스스로 움직이지 않는 순수 연출 상태인데 수평 자기소유로 선언돼 있습니다 — " +
                    "그러면 잔여 속도를 아무도 죽이지 않아 연출 내내 등속으로 미끄러집니다(신고 그대로).");
            }
        }

        [Test]
        public void 두_안전망의_제외목록은_서로_다른_축이라_같을_필요가_없다()
        {
            // GroundLossHang이 그 증거다: 세로는 붙잡아야 하므로 접지 자기소유가 **아니고**(중력 억제
            // 대상이어야 한다), 가로는 들고 온 속도를 유지해야 하므로 수평 자기소유가 **맞다**.
            // 두 목록을 "같아야 한다"로 묶으려는 다음 사람을 여기서 멈춘다.
            Assert.IsFalse(StickmanBlackboard.IsGroundKeepingSelfManaged(StickmanStateId.GroundLossHang),
                "GroundLossHang이 접지 자기소유가 되면 중력 억제 대상에서 빠져 몸이 자유낙하합니다 " +
                "— 붙잡음이 사라지는 것이고, 그건 2026-09-01 수정이 막으려던 버그 그 자체입니다.");
            Assert.IsTrue(StickmanBlackboard.IsHorizontalMotionSelfManaged(StickmanStateId.GroundLossHang),
                "GroundLossHang의 수평 속도를 안전망이 지우면 허공에서 걸어가는 코요테 개그가 사라집니다 " +
                "— 그 연출이 이 상태의 존재 이유입니다(2026-09-01 소은 실측).");
        }

        // ====================================================================
        // (1-B) ★ 방향 부호(facing) 소유권 — 2026-09-02 신설
        //
        //   리더 지적: "미끄러짐을 막은 그 계약은 '속도'만 보고 '방향 부호'는 안 본다."
        //   실제로 이 파일의 facing 계열 언급은 **0건**이었고, 그 사이 활쏘기 접근 페이즈가
        //   매 프레임 SetFacingSign(dir)을 부르고도 TickPose에 덮여 뒷걸음질로 보였다.
        //
        //   ★ 리더가 제시한 계약 문구는 **한 군데 수정해서** 잠근다.
        //     제시: "수평 이동을 자기가 소유하는 상태는 방향 부호도 소유한다."
        //     반례: Walk. Walk는 수평 자기소유(true)지만 그 수평 속도를 배회 AI의 MoveInputX에서
        //           그대로 유도한다 — 그 상태에서 방향 갱신을 막으면 캐릭터가 한쪽만 보고 걷는다.
        //     확정: "이동 방향을 MoveInputX가 아닌 곳에서 정하는 상태(= SetFacingSign을 스스로
        //           부르는 상태)는 방향 부호도 소유한다."
        //     아래 (1-B-2) 소스 전수 감사가 그 '스스로 부르는' 쪽을 기계적으로 판정한다.
        // ====================================================================

        /// <summary>방향 부호를 스스로 소유한다고 선언된 상태들.</summary>
        private static readonly StickmanStateId[] ExpectedFacingSelfManaged =
        {
            StickmanStateId.Archery,
            StickmanStateId.ParkourClimb,
        };

        [Test]
        public void 방향부호_자기소유_목록이_승인된_구성과_같다()
        {
            var actual = AllStates().Where(StickmanBlackboard.IsFacingSelfManaged).ToArray();

            var added = actual.Except(ExpectedFacingSelfManaged).ToArray();
            var removed = ExpectedFacingSelfManaged.Except(actual).ToArray();

            Assert.IsEmpty(added,
                "방향 부호 자기소유 목록에 승인되지 않은 상태가 들어왔습니다: " + string.Join(", ", added) +
                ".\n여기 넣으면 그 상태 동안 배회 AI가 방향을 바꾸지 못합니다 — 이동 방향을 MoveInputX에서 " +
                "유도하는 상태(Walk 등)를 넣으면 캐릭터가 한쪽만 보고 걷습니다.");
            Assert.IsEmpty(removed,
                "방향 부호 자기소유 목록에서 상태가 빠졌습니다: " + string.Join(", ", removed) +
                ".\n여기서 빼면 그 상태가 부른 SetFacingSign이 같은 프레임 뒤쪽 TickPose에 덮여 **죽은 코드**가 " +
                "됩니다(2026-09-02 활쏘기 접근 페이즈에서 실제로 일어난 일 — 몸은 과녁을 보는데 발은 반대로 갔다).");
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — 배회로 움직이는 상태는 <b>반드시</b> 배회 AI가 방향을 준다.
        /// 이 짝이 없으면 "전부 자기소유로 만들면 통과"하는 오답이 초록이 된다.
        /// </summary>
        [Test]
        public void 배회_이동의도로_걷는_상태는_방향을_배회AI가_준다()
        {
            StickmanStateId[] wanderDriven =
            {
                StickmanStateId.Idle, StickmanStateId.Walk, StickmanStateId.Jump,
                StickmanStateId.Fall, StickmanStateId.LedgeHang, StickmanStateId.Graffiti,
            };

            var blackboard = new StickmanBlackboard();
            foreach (StickmanStateId id in wanderDriven)
            {
                Assert.IsFalse(StickmanBlackboard.IsFacingSelfManaged(id),
                    $"{id}의 진행 방향은 배회 AI의 MoveInputX가 정합니다 — 방향 자기소유로 선언하면 " +
                    "그 갱신이 멎어 캐릭터가 뒤를 본 채로 걷습니다(고치려던 버그와 정확히 같은 그림).");
                Assert.IsTrue(blackboard.WanderIntentMayDriveFacing(id),
                    $"{id}에서 배회 AI가 방향을 줄 수 없다고 판정됐습니다(FacingLocked 기본값은 false입니다).");
            }

            // FacingLocked(조준 구간 전용 동적 플래그)는 여전히 모든 상태에서 이깁니다.
            blackboard.FacingLocked = true;
            foreach (StickmanStateId id in wanderDriven)
            {
                Assert.IsFalse(blackboard.WanderIntentMayDriveFacing(id),
                    $"{id}에서 FacingLocked가 켜졌는데도 배회 AI가 방향을 덮을 수 있다고 판정됐습니다 — " +
                    "조준 중 몸이 홱 돌아가 화살이 뒤통수에서 나갑니다.");
            }
        }

        /// <summary>
        /// ★★ (1-B-2) 전수 감사 — <c>States/*.cs</c>에서 <c>SetFacingSign</c>을 스스로 부르는 상태는
        /// 반드시 방향 자기소유여야 한다.
        ///
        /// <para>왜 소스 스캔인가: 목록만 잠그면 <b>새 상태</b>가 같은 함정에 그대로 빠진다. 활쏘기는
        /// 이 호출을 매 프레임 하고도 한 달 가까이 덮이고 있었고, 아무도 몰랐던 이유는 "호출했으니
        /// 됐다"가 코드상 완벽히 그럴듯해 보였기 때문이다. 곡괭이질·낚시·닦기·쓰다듬기가 전부 같은
        /// 형태(접근 보행 → 제자리)로 예정돼 있다 — 그 사람들이 기억하기를 기대하지 않는다.</para>
        /// </summary>
        [Test]
        public void SetFacingSign을_스스로_부르는_상태는_전부_방향_자기소유다()
        {
            string root = Path.Combine(Application.dataPath, "_Project", "Scripts", "States");
            Assert.IsTrue(Directory.Exists(root), $"States 폴더를 찾지 못했습니다: {root}");

            var offenders = new List<string>();
            var audited = new List<string>();

            foreach (string path in Directory.GetFiles(root, "*State.cs", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileNameWithoutExtension(path);           // 예: ArcheryState
                string source = File.ReadAllText(path);
                if (source.IndexOf("SetFacingSign", StringComparison.Ordinal) < 0) continue;

                string idName = fileName.EndsWith("State", StringComparison.Ordinal)
                    ? fileName.Substring(0, fileName.Length - "State".Length)
                    : fileName;
                if (!Enum.TryParse(idName, out StickmanStateId id))
                {
                    // 파일명 ↔ 상태 ID 규약이 깨졌다. 조용히 건너뛰면 감사가 껍데기가 되므로 드러낸다.
                    offenders.Add($"{fileName}(상태 ID '{idName}'를 찾을 수 없음 — 파일명 규약 확인)");
                    continue;
                }

                audited.Add(id.ToString());
                if (!StickmanBlackboard.IsFacingSelfManaged(id)) offenders.Add(fileName);
            }

            Assert.Greater(audited.Count, 0,
                "SetFacingSign을 부르는 상태 파일을 한 건도 찾지 못했습니다 — 스캔이 소스와 어긋났다는 뜻이고, " +
                "그러면 이 감사는 언제나 초록인 껍데기입니다(거짓 통과).");

            Assert.IsEmpty(offenders,
                "다음 상태가 SetFacingSign을 부르면서 방향 자기소유로 선언돼 있지 않습니다: " +
                string.Join(", ", offenders) +
                ".\n그 호출은 같은 프레임 뒤쪽 StickmanBlackboard.TickPose가 배회 AI의 MoveInputX 부호로 " +
                "덮어써 **아무 효과가 없습니다**(StickmanAgent.Update 순서: _autoWander.Tick -> _machine.Tick -> " +
                "TickPose). StickmanBlackboard.IsFacingSelfManaged에 그 상태를 추가하십시오.");

            Debug.Log($"[방향소유감사] SetFacingSign을 부르는 상태 {audited.Count}건({string.Join(", ", audited)})을 " +
                "검사했고 전부 방향 자기소유로 선언돼 있습니다.");
        }

        // ====================================================================
        // (1-C) ★ 화면 끝 제자리걸음(러닝머신) — 2026-09-02 사용자 신고(멀티모니터)
        // ====================================================================

        /// <summary>
        /// 사용자 로그 실측값을 그대로 쓴다(월드 유닛 환산). 화면 오른쪽 끝 3838pt, 클램프가 붙잡은
        /// 자리 ≈3803pt(좌우여유 35.2pt), 돌아서는 임계 ≈24pt(0.3유닛). 40pt = 1유닛으로 환산한다.
        /// </summary>
        private const float PtPerUnit = 40f;

        [Test]
        public void 클램프가_발판_끝보다_앞에서_막으면_그_지점이_화면_끝이다()
        {
            // 멀티모니터 재현: 2번 모니터의 창까지 발판으로 열거돼 통합 경계(union)가 화면 밖으로 뻗는다.
            // 그래서 "지금 딛은 발판의 오른쪽 끝"은 통합 경계가 **아니다** — 옛 게이트가 꺼지던 조건.
            float foothold = 3838f / PtPerUnit;   // 화면을 꽉 채운 창의 오른쪽 끝
            float union    = 5000f / PtPerUnit;   // 2번 모니터 창까지 포함한 통합 경계
            float walkable = 3802.8f / PtPerUnit; // 하드 클램프가 실제로 붙잡는 자리(사용자 로그 값)

            float boundary = AutoWanderController.ResolveEffectiveEdgeBoundary(
                foothold, union, hasWalkable: true, walkableBoundaryX: walkable,
                direction: 1, out bool isTrueScreenEdge);

            Assert.AreEqual(walkable, boundary, 1e-4f,
                "발판 끝보다 앞에서 클램프가 막는데도 경계 판정이 발판 원시 끝을 쓰고 있습니다 — " +
                $"캐릭터는 {foothold - walkable:F3}유닛(={(foothold - walkable) * PtPerUnit:F1}pt) 앞의 보이지 않는 벽에 " +
                "막힌 채 '아직 남았다'고 계산해 제자리걸음합니다(2026-09-02 사용자 신고 그대로).");
            Assert.IsTrue(isTrueScreenEdge,
                "클램프가 막는 지점은 더 갈 곳이 없는 '화면의 끝'입니다 — false로 두면 그 자리에서 " +
                "뛰어내리기/매달리기/되올라가기 추첨과 경계 점프가 살아나 화면 밖으로 나가려 합니다.");

            // 실제로 돌아서는가 — 잔여 거리가 임계 이하가 되어야 BeginEdgePause로 간다.
            float characterX = walkable;                       // 클램프에 붙잡혀 서 있는 자리
            float remaining = boundary - characterX;
            float stopDistance = 0.3f;                         // wanderEdgeStopDistance 기본값
            Assert.LessOrEqual(remaining, stopDistance,
                $"잔여 거리 {remaining:F3}유닛이 돌아서기 임계 {stopDistance:F2}유닛보다 큽니다 — " +
                "임계가 영영 성립하지 않는 것이 러닝머신의 정의입니다.");
        }

        [Test]
        public void 왼쪽도_대칭으로_동작한다()
        {
            float foothold = 0f;
            float union = -30f;
            float walkable = 0.88f;

            float boundary = AutoWanderController.ResolveEffectiveEdgeBoundary(
                foothold, union, hasWalkable: true, walkableBoundaryX: walkable,
                direction: -1, out bool isTrueScreenEdge);

            Assert.AreEqual(walkable, boundary, 1e-4f, "왼쪽 방향에서 클램프 한계가 반영되지 않았습니다.");
            Assert.IsTrue(isTrueScreenEdge, "왼쪽 클램프 지점도 화면의 끝입니다.");
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — 화면 한복판의 평범한 발판 경계는 <b>아무 것도 바뀌면 안 된다</b>.
        /// 여기서 클램프를 끌어다 쓰면 캐릭터가 창 끝에서 뛰어내리지도, 매달리지도 못하게 된다.
        /// </summary>
        [Test]
        public void 화면_안쪽의_평범한_발판_경계는_그대로다()
        {
            float foothold = 1000f / PtPerUnit;    // 화면 한복판 창의 오른쪽 끝
            float union    = 5000f / PtPerUnit;
            float walkable = 3802.8f / PtPerUnit;  // 클램프는 한참 바깥 — 구속하지 않는다

            float boundary = AutoWanderController.ResolveEffectiveEdgeBoundary(
                foothold, union, hasWalkable: true, walkableBoundaryX: walkable,
                direction: 1, out bool isTrueScreenEdge);

            Assert.AreEqual(foothold, boundary, 1e-4f,
                "화면 안쪽 발판 경계까지 클램프 한계로 바뀌면, 창 끝에서 뛰어내리기/매달리기/되올라가기가 " +
                "전부 죽습니다(그 세 갈래는 isTrueScreenEdge가 false일 때만 추첨합니다).");
            Assert.IsFalse(isTrueScreenEdge,
                "화면 한복판의 창 경계가 '화면의 끝'으로 판정됐습니다 — 그 자리에서 경계 행동 추첨이 통째로 막힙니다.");
        }

        /// <summary>
        /// ★ 회귀 짝 — 2026-08-29에 고친 <b>단일 모니터</b> 경로(발판 경계 == 통합 경계)도 계속 통해야 한다.
        /// </summary>
        [Test]
        public void 단일모니터_전폭_발판에서도_클램프_한계를_쓴다()
        {
            float foothold = 3838f / PtPerUnit;
            float union    = 3838f / PtPerUnit;   // 발판이 하나뿐 = 통합 경계와 같다
            float walkable = 3802.8f / PtPerUnit;

            float boundary = AutoWanderController.ResolveEffectiveEdgeBoundary(
                foothold, union, hasWalkable: true, walkableBoundaryX: walkable,
                direction: 1, out bool isTrueScreenEdge);

            Assert.AreEqual(walkable, boundary, 1e-4f, "2026-08-29 수정(단일 모니터 러닝머신)이 회귀했습니다.");
            Assert.IsTrue(isTrueScreenEdge);
        }

        /// <summary>클램프 한계를 못 구한 경우(카메라/몸 미배선)에는 예전 그대로 원시 경계를 쓴다.</summary>
        [Test]
        public void 클램프_한계를_모르면_원시_경계로_되돌아간다()
        {
            float foothold = 3838f / PtPerUnit;
            float boundary = AutoWanderController.ResolveEffectiveEdgeBoundary(
                foothold, 5000f / PtPerUnit, hasWalkable: false, walkableBoundaryX: 0f,
                direction: 1, out bool isTrueScreenEdge);

            Assert.AreEqual(foothold, boundary, 1e-4f);
            Assert.IsFalse(isTrueScreenEdge, "통합 경계와 다르고 클램프도 모르면 '화면 끝'이라 단정할 근거가 없습니다.");
        }

        // ====================================================================
        // (2) 설정 스위치 — 코드 기본값과 배포 에셋이 같아야 한다
        //     (이 저장소는 "에셋이 언제나 이긴다" — 안 구우면 스위치가 꺼진 채 출하된다)
        // ====================================================================

        [Test]
        public void 신규_2필드가_코드기본값과_배포에셋에서_같다()
        {
            StickConfig deployed = LoadDeployedConfig();
            StickConfig codeDefault = NewCodeDefault();
            try
            {
                Assert.AreEqual(codeDefault.horizontalDriftSafetyNetEnabled,
                    deployed.horizontalDriftSafetyNetEnabled,
                    "horizontalDriftSafetyNetEnabled의 코드 기본값과 배포 에셋값이 다릅니다 — " +
                    "이 저장소는 에셋 값이 코드 기본값을 이기므로, 안 구우면 안전망이 꺼진 채 출하됩니다.");
                Assert.AreEqual(codeDefault.horizontalDriftBrakeSeconds,
                    deployed.horizontalDriftBrakeSeconds, 1e-5f,
                    "horizontalDriftBrakeSeconds의 코드 기본값과 배포 에셋값이 다릅니다.");
                Assert.IsTrue(deployed.horizontalDriftSafetyNetEnabled,
                    "배포 에셋에서 수평 표류 안전망이 꺼져 있습니다 — 신고된 버그가 그대로 출하됩니다.");
                Assert.Greater(deployed.horizontalDriftBrakeSeconds, 0f,
                    "배포 에셋의 정지 박자가 0 이하입니다 — 급정지 튐 방지가 사라집니다.");
            }
            finally { UnityEngine.Object.DestroyImmediate(codeDefault); }
        }

        // ====================================================================
        // (3) 연출 종료 분류 — 순수 정책
        // ====================================================================

        [Test]
        public void 비정상이탈_목록은_물리적_이탈만_담는다()
        {
            StickmanStateId[] abnormal =
            {
                StickmanStateId.Fall, StickmanStateId.Ragdoll,
                StickmanStateId.ThrowTumble, StickmanStateId.GroundLossHang,
            };

            foreach (StickmanStateId id in abnormal)
            {
                Assert.IsTrue(SpectacleExitClassification.IsAbnormalExit(id),
                    $"{id}로 나가는 것은 연출이 완수된 것이 아니라 몸이 밀려난 것입니다 — " +
                    "완료로 기록하면 절대 불변 원칙 1(행동-기록 싱크)을 위반하고 쿨다운까지 걸립니다.");
            }

            foreach (StickmanStateId id in AllStates().Except(abnormal))
            {
                Assert.IsFalse(SpectacleExitClassification.IsAbnormalExit(id),
                    $"{id}가 비정상 이탈로 분류됐습니다 — 거짓 양성은 정상 완료를 취소로 만들고 " +
                    "쿨다운을 무력화해 연출이 과도하게 반복됩니다.");
            }
        }

        [Test]
        public void 연출의_정상_종료지점인_Idle은_비정상이_아니다()
        {
            // TimedSpectacleState / ArcheryState 모두 타이머 만료 시 ChangeState(Idle)이다.
            // 이 한 줄이 무너지면 모든 연출이 영원히 "취소"로 기록된다.
            Assert.IsFalse(SpectacleExitClassification.IsAbnormalExit(StickmanStateId.Idle),
                "Idle은 모든 연출의 정상 종료지점입니다.");
        }

        [Test]
        public void 상태전이_이벤트가_같은_판정을_노출한다()
        {
            var abnormalEvt = new StateTransitionEvent(StickmanStateId.Graffiti, StickmanStateId.Fall, false);
            var normalEvt = new StateTransitionEvent(StickmanStateId.Graffiti, StickmanStateId.Idle, false);

            Assert.IsTrue(abnormalEvt.IsAbnormalExit,
                "발판 상실 Fall은 강제 인터럽트가 아니므로(isForcedInterrupt=false) IsForcedInterrupt만 " +
                "보던 예전 코드가 이것을 '정상 완료'로 기록했습니다. 두 축은 독립입니다.");
            Assert.IsFalse(abnormalEvt.IsForcedInterrupt,
                "이 재현 자체가 성립하려면 '비정상 이탈이면서 강제 인터럽트가 아닌' 조합이 존재해야 합니다.");
            Assert.IsFalse(normalEvt.IsAbnormalExit);
        }

        // ====================================================================
        // (4) ★ 전수 감사 — 같은 형태가 다른 디렉터에 또 생기는 것을 막는다
        // ====================================================================

        /// <summary>
        /// <c>OnStateTransitioned</c> 안에서 <c>SpectacleOverlayPhase.Completed</c>를 발행하는 디렉터는
        /// 반드시 <c>IsAbnormalExit</c>를 함께 봐야 한다.
        ///
        /// <para>왜 소스 스캔인가: 이 결함은 <b>네 개 디렉터에 똑같이</b> 있었고(그라피티/청소부·블랙홀/
        /// 창도둑/활쏘기), 이 저장소의 반복 실패 유형이 정확히 "같은 패턴의 다른 경로에는 안 넣기"다.
        /// 다섯 번째 디렉터가 생길 때 사람이 기억하기를 기대하지 않는다 — 러너가 말해 준다.</para>
        /// </summary>
        [Test]
        public void 완료를_발행하는_모든_디렉터가_비정상이탈을_함께_본다()
        {
            string root = Path.Combine(Application.dataPath, "_Project", "Scripts", "Interaction");
            Assert.IsTrue(Directory.Exists(root), $"Interaction 폴더를 찾지 못했습니다: {root}");

            // OnStateTransitioned(...) { ... } 의 본문을 중괄호 균형으로 잘라낸다.
            var offenders = new List<string>();
            int audited = 0;

            foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(path);
                foreach (Match m in Regex.Matches(source, @"void\s+OnStateTransitioned\s*\([^)]*\)\s*\{"))
                {
                    string body = ExtractBlock(source, m.Index + m.Length - 1);
                    if (body == null) continue;
                    if (body.IndexOf("SpectacleOverlayPhase.Completed", StringComparison.Ordinal) < 0) continue;

                    audited++;
                    if (body.IndexOf("IsAbnormalExit", StringComparison.Ordinal) < 0)
                    {
                        offenders.Add(Path.GetFileName(path));
                    }
                }
            }

            Assert.Greater(audited, 0,
                "완료를 발행하는 OnStateTransitioned를 한 건도 찾지 못했습니다 — 정규식이 소스와 " +
                "어긋났다는 뜻이고, 그러면 이 감사는 언제나 초록인 껍데기입니다(거짓 통과).");

            Assert.IsEmpty(offenders,
                "다음 디렉터가 OnStateTransitioned에서 Completed를 발행하면서 도착 상태를 보지 않습니다: " +
                string.Join(", ", offenders) +
                ".\n연출 도중 발판 밖으로 떨어져도 '정상 완료'로 기록되고 쿨다운까지 걸립니다" +
                "(2026-09-02 그라피티에서 실제로 일어난 일). " +
                "Core/SpectacleExitClassification.IsAbnormalExit를 함께 보고, 비정상이면 Cancelled로 " +
                "발행하고 쿨다운을 걸지 마십시오.");

            Debug.Log($"[연출종료감사] Completed를 발행하는 OnStateTransitioned {audited}건을 검사했고 전부 " +
                "도착 상태를 함께 봅니다.");
        }

        /// <summary>여는 중괄호 위치에서 시작해 균형이 맞는 닫는 중괄호까지의 본문을 돌려준다.</summary>
        private static string ExtractBlock(string source, int openBraceIndex)
        {
            int depth = 0;
            for (int i = openBraceIndex; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0) return source.Substring(openBraceIndex, i - openBraceIndex + 1);
                }
            }
            return null;
        }
    }
}
