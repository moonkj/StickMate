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
