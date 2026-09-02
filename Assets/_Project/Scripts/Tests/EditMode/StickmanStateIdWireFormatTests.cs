using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <see cref="StickmanStateId"/>의 <b>정수 값</b>은 파일에 나가는 값이다 — 2026-09-02 확정.
    ///
    /// ============================================================================
    /// 왜 잠그는가
    /// ============================================================================
    /// Unity는 enum을 정수로 직렬화하고, <c>Plugins/MotionPluginSO.applicableStates</c> ·
    /// <c>Plugins/EffectPluginSO.applicableStates</c>가 <c>StickmanStateId[]</c>다. 즉 DLC 매니페스트
    /// <c>.asset</c> 안에는 상태가 <b>숫자로</b> 남는다.
    ///
    /// 그리고 <b>이미 한 번 밀렸다</b>: 2026-09-02 격파 미니게임(<c>BattleMinigame</c>)을 지웠는데
    /// 그것이 8번 자리였고, <c>Dragged</c>(9 -&gt; 8) 이후 <b>19개 값 전부</b>가 한 칸씩 당겨졌다.
    /// 지금은 매니페스트 에셋이 0개라 피해가 없다. 첫 팩이 나간 뒤 같은 일이 벌어지면 출하된 팩이
    /// 전부 <b>엉뚱한 상태에 배선</b>된다 — 모션이 안 나오는 것이 아니라 다른 상태에서 나온다.
    ///
    /// ============================================================================
    /// 두 겹으로 잠근다
    /// ============================================================================
    /// ① <b>값 표</b> — 이름 -&gt; 번호. 순서를 바꾸거나 중간을 지우면 여기서 걸린다.
    /// ② <b>소스 검사</b> — 선언에 <c>= N</c>이 <b>붙어 있는지</b>. ①만으로는 번호 없이 맨 끝에 더한
    ///    새 값이 그대로 통과하고(암묵값이 우연히 맞으므로), 그 다음 삭제 때 다시 밀린다.
    ///    즉 ①은 <b>지금</b>을, ②는 <b>다음 사람</b>을 잠근다.
    /// </summary>
    public sealed class StickmanStateIdWireFormatTests
    {
        private static string SourcePath => Path.Combine(
            Application.dataPath, "_Project", "Scripts", "Core", "StickmanEventBus.cs");

        /// <summary>2026-09-02에 확정된 배선. <b>여기 값을 고치는 것은 출하된 DLC를 깨는 것</b>이다.
        /// 새 상태는 맨 끝에 다음 번호로만 더한다.</summary>
        private static readonly (StickmanStateId Id, int Wire)[] Wire =
        {
            (StickmanStateId.Idle, 0),
            (StickmanStateId.Walk, 1),
            (StickmanStateId.Jump, 2),
            (StickmanStateId.Fall, 3),
            (StickmanStateId.ParkourClimb, 4),
            (StickmanStateId.Attack, 5),
            (StickmanStateId.Ragdoll, 6),
            (StickmanStateId.Getup, 7),
            (StickmanStateId.Dragged, 8),
            (StickmanStateId.RodeoCursor, 9),
            (StickmanStateId.WindowTheft, 10),
            (StickmanStateId.Graffiti, 11),
            (StickmanStateId.DesktopTidy, 12),
            (StickmanStateId.BlackholeSummon, 13),
            (StickmanStateId.WindowCrash, 14),
            (StickmanStateId.TodoReminder, 15),
            (StickmanStateId.FocusStart, 16),
            (StickmanStateId.FocusComplete, 17),
            (StickmanStateId.FocusCancelled, 18),
            (StickmanStateId.FocusNudge, 19),
            (StickmanStateId.Sulky, 20),
            (StickmanStateId.Runaway, 21),
            (StickmanStateId.LedgeHang, 22),
            (StickmanStateId.LandingCrouch, 23),
            (StickmanStateId.ThrowTumble, 24),
            (StickmanStateId.Archery, 25),
            (StickmanStateId.GroundLossHang, 26),
        };

        [Test]
        public void 상태_번호는_파일에_나간_값_그대로다()
        {
            for (int i = 0; i < Wire.Length; i++)
            {
                Assert.AreEqual(Wire[i].Wire, (int)Wire[i].Id,
                    $"{Wire[i].Id}의 번호가 {(int)Wire[i].Id}로 바뀌었습니다(확정값 {Wire[i].Wire}). " +
                    "이미 나간 DLC 매니페스트가 있다면 그 팩의 모션이 <b>다른 상태에서</b> 재생됩니다 — " +
                    "안 나오는 것보다 나쁩니다. 새 상태는 맨 끝에 다음 번호로만 더하세요.");
            }
        }

        [Test]
        public void 새_상태가_생겼으면_이_표에도_있어야_한다()
        {
            var declared = new HashSet<StickmanStateId>();
            foreach (StickmanStateId id in (StickmanStateId[])Enum.GetValues(typeof(StickmanStateId)))
            {
                declared.Add(id);
            }

            var locked = new HashSet<StickmanStateId>();
            for (int i = 0; i < Wire.Length; i++) locked.Add(Wire[i].Id);

            declared.ExceptWith(locked);
            Assert.IsEmpty(declared,
                $"이 표에 없는 상태가 있습니다: {string.Join(", ", declared)}. 상태를 더했으면 확정 번호를 " +
                "여기 함께 적으세요 — 적지 않으면 다음 삭제 때 조용히 밀립니다.");
        }

        [Test]
        public void 번호가_겹치지_않는다()
        {
            var seen = new Dictionary<int, StickmanStateId>();
            foreach (StickmanStateId id in (StickmanStateId[])Enum.GetValues(typeof(StickmanStateId)))
            {
                Assert.IsFalse(seen.ContainsKey((int)id),
                    $"{id}와 {(seen.TryGetValue((int)id, out StickmanStateId other) ? other.ToString() : "?")}가 " +
                    $"같은 번호 {(int)id}를 씁니다 — 직렬화된 값이 어느 쪽인지 구분되지 않습니다.");
                seen[(int)id] = id;
            }
        }

        // ============================================================================
        // ② 소스 검사 — 선언에 번호가 <b>붙어 있는가</b>
        // ============================================================================

        [Test]
        public void 모든_상태_선언에_명시_번호가_붙어_있다()
        {
            string path = SourcePath;
            Assert.IsTrue(File.Exists(path), $"원본을 찾을 수 없습니다: {path}");
            string src = File.ReadAllText(path).Replace("\r\n", "\n");

            Match body = Regex.Match(src, @"public enum StickmanStateId\s*\n\s*\{(?<body>.*?)\n    \}", RegexOptions.Singleline);
            Assert.IsTrue(body.Success, "enum 본문을 찾지 못했습니다 — 이 검사가 공허해졌습니다.");

            // 양성 대조: 본문을 정말 읽었는가. 0건 매치를 "깨끗함"으로 오독하지 않기 위한 교정이다.
            MatchCollection members = Regex.Matches(body.Groups["body"].Value, @"^ {8}(?<name>[A-Za-z][A-Za-z0-9]*) *(?<assign>= *(?<value>-?\d+))? *,", RegexOptions.Multiline);
            Assert.AreEqual(Wire.Length, members.Count,
                $"본문에서 찾은 선언이 {members.Count}개인데 확정 표는 {Wire.Length}개입니다 — " +
                "정규식이 본문을 못 읽고 있거나 표가 낡았습니다(어느 쪽이든 이 검사는 무효입니다).");

            var missing = new List<string>();
            for (int i = 0; i < members.Count; i++)
            {
                if (!members[i].Groups["assign"].Success) missing.Add(members[i].Groups["name"].Value);
            }

            Assert.IsEmpty(missing,
                $"번호 없이 선언된 상태가 있습니다: {string.Join(", ", missing)}. " +
                "Unity가 이 값을 정수로 직렬화하므로, 번호가 없으면 위치에 따라 값이 움직입니다.");
        }
    }
}
