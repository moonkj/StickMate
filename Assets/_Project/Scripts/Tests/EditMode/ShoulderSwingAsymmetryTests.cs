using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StickMate.States;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ P9-d 회귀 잠금 — <b>"어깨 제한은 사람 어깨처럼 비대칭이다."</b> (docs/UX_FLOW.md 38-14-3 (d))
    ///
    /// ============================================================================
    /// 무엇을 바꿨고 왜인가
    /// ============================================================================
    /// 어깨(상완)의 RAGDOLL 각도 제한이 <b>±75도 대칭</b>이었다. 사람 어깨는 대칭이 아니다 —
    /// 앞/위로는 170도 가까이 올라가고 뒤로는 60도쯤에서 막힌다. 대칭값은 그 비대칭을 평균으로
    /// 뭉갠 값이라 <b>양쪽 다 틀렸다</b>:
    /// <list type="bullet">
    /// <item>참고자료(피격 2~4프레임)의 "팔이 머리 위로 홱 튕겨 올라감"은 어깨 약 150도를 요구한다.
    ///       75도 제한에서는 <b>충격량을 아무리 키워도 솔버가 75도에서 자른다</b> — 원리적 불가능.</item>
    /// <item>반대로 뒤쪽 75도는 너무 관대했다. 두 팔이 +75/−75로 벌어진 실루엣이 곧
    ///       2026-08-28에 막으려던 "불가사리 대(大)자"다(수평에서 15도 모자란 상태).</item>
    /// </list>
    /// 그래서 <b>[-75,+75] -&gt; [-60,+150]</b>. 앞은 열고 뒤는 조인다.
    ///
    /// ============================================================================
    /// 이 파일이 지키는 불변식
    /// ============================================================================
    /// <list type="number">
    /// <item><b>소스 상수와 구워진 프리팹이 일치한다.</b> 프리팹은 손으로 고칠 수 있는 자산이라
    ///       코드와 조용히 갈라질 수 있다 — 이 프로젝트에서 가장 자주 겪은 종류의 회귀다.</item>
    /// <item><b>"대(大)자"는 여전히, 오히려 더 확실히 막힌다.</b> 대자는 한 팔이 앞으로 수평(+90),
    ///       다른 팔이 뒤로 수평(−90)일 때 나온다. 뒤쪽 한계가 75 -&gt; 60으로 <b>내려갔으므로</b>
    ///       그 실루엣은 이전보다 더 멀어졌다. 2026-08-28의 목적은 훼손되지 않는다.</item>
    /// <item><b>진입 프레임에 팔이 튀지 않는다.</b> 능동 포즈(Idle 벌림 / 보행 어깨 키 × 진폭 상한)가
    ///       전부 허용 구간 안에 있어야 한다. 하나라도 밖이면 RAGDOLL 첫 프레임에 솔버가 팔을
    ///       튕겨 넣는다(2026-08-29에 실제로 겪은 "픽 하는 꺾임").</item>
    /// <item><b>좌우 반전에 새 배관이 필요 없다.</b> <see cref="RagdollRig.MirrorIfFacingLeft"/>가
    ///       [min,max] -&gt; [-max,-min]으로 정확히 뒤집는다. 대칭 구간에서는 이 함수가 <b>항등</b>이라
    ///       틀려도 티가 안 났지만, 비대칭이 되면 여기가 틀리는 순간 왼쪽을 볼 때 팔이 뒤로만
    ///       꺾인다.</item>
    /// <item><b>다리(고관절)는 손대지 않았다.</b> 비대칭화가 엉덩이로 새면 걷기가 통째로 바뀐다.</item>
    /// </list>
    ///
    /// <para>소스/자산을 직접 파싱하는 방식은 이 프로젝트의 선례를 따른다
    /// (<c>HeadFillGeometryTests</c>가 C# 소스를, <c>EyeRestorePathContractTests</c>가 되살리기
    /// 경로를 같은 방식으로 감사한다). 값을 테스트에 베껴 적으면 값이 바뀔 때 둘 다 조용히 바뀐다.</para>
    /// </summary>
    public sealed class ShoulderSwingAsymmetryTests
    {
        private const string BootstrapperRelativePath = "Editor/SceneBootstrapper.cs";
        private const string PrefabRelativePath = "_Project/Prefabs/Stickman.prefab";

        /// <summary>참고자료의 "팔이 머리 위로 홱" 자세에 필요한 어깨 각도(도, 설계 근거 38-14-3).
        /// 앞쪽 한계가 이보다 작으면 그 그림은 <b>원리적으로</b> 나올 수 없다.</summary>
        private const float ReferenceOverheadThrowDegrees = 150f;

        /// <summary>팔이 몸통에 완전히 수직(= 수평으로 뻗음)이 되는 각도. 앞뒤 양쪽에서 이 각도가
        /// 동시에 가능하면 그것이 "대(大)자"다.</summary>
        private const float HorizontalOutstretchDegrees = 90f;

        /// <summary>2026-08-28~2026-09-01 이전의 대칭 한계. 뒤쪽은 여기서 <b>더 조여야</b> 한다.</summary>
        private const float PreviousSymmetricLimitDegrees = 75f;

        private static string AssetsPath(string relative)
            => Path.Combine(Application.dataPath, relative);

        // ========================================================================
        // (1) 소스 상수 자체가 비대칭인가
        // ========================================================================

        [Test]
        public void 소스_어깨_제한이_비대칭이고_앞쪽이_레퍼런스_각도를_허용한다()
        {
            float back = ReadFloatConst("ShoulderSwingBackLimitDegrees");
            float forward = ReadFloatConst("ShoulderSwingForwardLimitDegrees");

            Debug.Log($"[P9D-TEST] 소스 어깨 제한 = [-{back:F0}, +{forward:F0}]도 " +
                $"(진행 방향이 +. 이전: ±{PreviousSymmetricLimitDegrees:F0} 대칭)");

            Assert.Greater(forward, back,
                $"어깨 제한이 여전히 대칭이거나 뒤쪽이 더 큽니다(뒤 {back:F0} / 앞 {forward:F0}) — " +
                "사람 어깨는 앞/위로 훨씬 크게 열린다는 것이 P9-d의 전제입니다.");
            Assert.GreaterOrEqual(forward, ReferenceOverheadThrowDegrees,
                $"앞쪽 한계가 {forward:F0}도라 참고자료의 '팔이 머리 위로 홱'(약 " +
                $"{ReferenceOverheadThrowDegrees:F0}도)이 원리적으로 불가능합니다 — 충격량 튜닝으로는 못 고칩니다.");
        }

        // ========================================================================
        // (2) ★ "대(大)자"는 여전히 막힌다 — 2026-08-28의 목적이 훼손되지 않았는가
        // ========================================================================

        [Test]
        public void 뒤로_수평으로_뻗는_대자_실루엣은_여전히_불가능하다()
        {
            float back = ReadFloatConst("ShoulderSwingBackLimitDegrees");

            Assert.Less(back, HorizontalOutstretchDegrees,
                $"뒤쪽 한계가 {back:F0}도라 팔이 뒤로 수평({HorizontalOutstretchDegrees:F0}도)까지 뻗습니다 — " +
                "'불가사리 대(大)자' 방지(2026-08-28)가 깨졌습니다.");

            // ★ 핵심 논증: 이번 변경은 대자 방지를 **약화시킨 것이 아니라 강화**했다.
            Assert.LessOrEqual(back, PreviousSymmetricLimitDegrees,
                $"뒤쪽 한계가 이전 대칭값({PreviousSymmetricLimitDegrees:F0}도)보다 커졌습니다({back:F0}도) — " +
                "P9-d는 앞을 여는 대신 뒤를 조이는 변경이어야 합니다.");
        }

        // ========================================================================
        // (3) 구워진 프리팹이 소스와 일치하는가 (자산-코드 동기화)
        // ========================================================================

        [Test]
        public void 프리팹의_상완_관절_제한이_소스_상수와_정확히_일치한다()
        {
            float back = ReadFloatConst("ShoulderSwingBackLimitDegrees");
            float forward = ReadFloatConst("ShoulderSwingForwardLimitDegrees");

            List<PrefabJoint> joints = ReadPrefabJoints();
            var arms = joints.FindAll(j => j.OwnerName == "LeftArm" || j.OwnerName == "RightArm");

            Assert.AreEqual(2, arms.Count,
                $"프리팹에서 상완(LeftArm/RightArm) 관절을 {arms.Count}개 찾았습니다(기대 2) — " +
                "프리팹 계층이 바뀌었거나 파싱이 깨졌습니다.");

            foreach (PrefabJoint j in arms)
            {
                Debug.Log($"[P9D-TEST] 프리팹 {j.OwnerName} 관절 제한 = [{j.Min:F0}, {j.Max:F0}]도");
                Assert.AreEqual(-back, j.Min, 0.001f,
                    $"{j.OwnerName}의 뒤쪽 제한이 소스 상수와 다릅니다 — 프리팹을 다시 구워야 합니다.");
                Assert.AreEqual(forward, j.Max, 0.001f,
                    $"{j.OwnerName}의 앞쪽 제한이 소스 상수와 다릅니다 — 프리팹을 다시 구워야 합니다.");
            }
        }

        /// <summary>네거티브 컨트롤 — 고관절은 대칭 그대로여야 한다(비대칭화가 다리로 새지 않았는가).</summary>
        [Test]
        public void 고관절은_대칭_그대로다()
        {
            float hip = ReadFloatConst("HipSwingLimitDegrees");
            List<PrefabJoint> joints = ReadPrefabJoints();
            var legs = joints.FindAll(j => j.OwnerName == "LeftLeg" || j.OwnerName == "RightLeg");

            Assert.AreEqual(2, legs.Count, "프리팹에서 대퇴(LeftLeg/RightLeg) 관절 2개를 찾지 못했습니다.");
            foreach (PrefabJoint j in legs)
            {
                Assert.AreEqual(-hip, j.Min, 0.001f, $"{j.OwnerName}의 고관절 하한이 바뀌었습니다.");
                Assert.AreEqual(hip, j.Max, 0.001f, $"{j.OwnerName}의 고관절 상한이 바뀌었습니다.");
            }
        }

        // ========================================================================
        // (4) 진입 프레임에 팔이 튀지 않는가 — 능동 포즈가 전부 구간 안인가
        // ========================================================================

        [Test]
        public void 능동_포즈의_어깨_각도가_전부_허용_구간_안에_있다()
        {
            float back = ReadFloatConst("ShoulderSwingBackLimitDegrees");
            float forward = ReadFloatConst("ShoulderSwingForwardLimitDegrees");
            float idleSpread = ReadFloatConst("IdleArmSpreadDegrees");

            // 보행 어깨 키의 최대 절댓값 18도(StickmanPoseAnimator.ArmShoulderKeys)에 P9-c의 진폭
            // 상한(WalkAmplitudeAtFullSpeed=1.35)을 곱한 값이 실제로 나올 수 있는 최대 보행 어깨각이다.
            const float WalkShoulderKeyMaxDegrees = 18f;
            const float MaxWalkAmplitudeScale = 1.35f;
            float walkMax = WalkShoulderKeyMaxDegrees * MaxWalkAmplitudeScale;

            float[] activePoseAngles = { idleSpread, -idleSpread, walkMax, -walkMax };
            foreach (float a in activePoseAngles)
            {
                Assert.GreaterOrEqual(a, -back,
                    $"능동 포즈 어깨각 {a:F1}도가 뒤쪽 제한 -{back:F0}도 밖입니다 — RAGDOLL 진입 첫 " +
                    "프레임에 솔버가 팔을 튕겨 넣습니다(2026-08-29의 '픽 하는 꺾임').");
                Assert.LessOrEqual(a, forward,
                    $"능동 포즈 어깨각 {a:F1}도가 앞쪽 제한 +{forward:F0}도 밖입니다.");
            }
        }

        // ========================================================================
        // (5) ★ 좌우 반전 — 새 배관 없이 정말 되는가 (비대칭이 되어서야 검증 가능해진 항목)
        // ========================================================================

        [Test]
        public void 왼쪽을_볼_때_어깨_제한이_거울상으로_정확히_뒤집힌다()
        {
            float back = ReadFloatConst("ShoulderSwingBackLimitDegrees");
            float forward = ReadFloatConst("ShoulderSwingForwardLimitDegrees");
            var anatomical = new JointAngleLimits2D { min = -back, max = forward };

            JointAngleLimits2D right = RagdollRig.MirrorIfFacingLeft(anatomical, mirrored: false);
            JointAngleLimits2D left = RagdollRig.MirrorIfFacingLeft(anatomical, mirrored: true);

            Debug.Log($"[P9D-TEST] 해부학 [{anatomical.min:F0},{anatomical.max:F0}] -> " +
                $"오른쪽 [{right.min:F0},{right.max:F0}] / 왼쪽 [{left.min:F0},{left.max:F0}]");

            Assert.AreEqual(anatomical.min, right.min, 0.001f, "오른쪽을 볼 때는 반전이 없어야 합니다.");
            Assert.AreEqual(anatomical.max, right.max, 0.001f, "오른쪽을 볼 때는 반전이 없어야 합니다.");

            // 거울상: 앞쪽(양수)이 그대로 뒤집혀 왼쪽에서는 음수 방향이 "앞"이 된다.
            Assert.AreEqual(-forward, left.min, 0.001f,
                "왼쪽을 볼 때 '앞으로 크게'가 거울상으로 넘어오지 않았습니다 — 왼쪽에서만 팔이 안 올라갑니다.");
            Assert.AreEqual(back, left.max, 0.001f,
                "왼쪽을 볼 때 '뒤로 조금'이 거울상으로 넘어오지 않았습니다.");

            // 폭 보존 — 어느 쪽을 보든 어깨가 움직일 수 있는 총 범위는 같아야 한다.
            Assert.AreEqual(right.max - right.min, left.max - left.min, 0.001f,
                "좌우에서 허용 범위의 폭이 다릅니다 — 한쪽이 더 뻣뻣해집니다.");

            // 진입 자세(중립 0도 근처)가 양쪽 모두 구간 안에 있어야 튐이 없다(2026-08-29 판정법).
            Assert.Less(left.min, 0f, "왼쪽 구간이 0을 포함하지 않습니다 — 진입 순간 제한 위반 상태입니다.");
            Assert.Greater(left.max, 0f, "왼쪽 구간이 0을 포함하지 않습니다 — 진입 순간 제한 위반 상태입니다.");
        }

        /// <summary>네거티브 컨트롤 — 대칭 구간에서는 이 함수가 항등이라, 이 함수만으로는 비대칭
        /// 처리를 증명할 수 없다는 사실 자체를 명시적으로 잠근다(왜 이전에는 안 드러났는가).</summary>
        [Test]
        public void 대칭_구간에서는_반전_함수가_항등이라_버그가_숨는다()
        {
            var symmetric = new JointAngleLimits2D { min = -75f, max = 75f };
            JointAngleLimits2D mirrored = RagdollRig.MirrorIfFacingLeft(symmetric, mirrored: true);

            Assert.AreEqual(symmetric.min, mirrored.min, 0.001f);
            Assert.AreEqual(symmetric.max, mirrored.max, 0.001f);
        }

        // ========================================================================
        // 파서
        // ========================================================================

        private static float ReadFloatConst(string name)
        {
            string path = AssetsPath(BootstrapperRelativePath);
            Assert.IsTrue(File.Exists(path), $"소스를 찾지 못했습니다: {path}");
            string source = File.ReadAllText(path);
            var m = Regex.Match(source, @"const\s+float\s+" + Regex.Escape(name) + @"\s*=\s*(-?[0-9.]+)f?\s*;");
            Assert.IsTrue(m.Success,
                $"{BootstrapperRelativePath}에서 상수 {name}을(를) 찾지 못했습니다 — 이름이 바뀌었다면 " +
                "이 테스트도 함께 갱신해야 합니다(그게 이 감사의 목적입니다).");
            return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private struct PrefabJoint
        {
            public string OwnerName;
            public float Min;
            public float Max;
        }

        /// <summary>프리팹 YAML에서 HingeJoint2D의 각도 제한을 소유 GameObject 이름과 함께 읽는다.
        /// Unity YAML은 문서마다 <c>--- !u!&lt;classId&gt; &amp;&lt;fileID&gt;</c> 헤더를 갖고, 컴포넌트는
        /// <c>m_GameObject: {fileID: N}</c>으로 소유자를 가리킨다.</summary>
        private static List<PrefabJoint> ReadPrefabJoints()
        {
            string path = AssetsPath(PrefabRelativePath);
            Assert.IsTrue(File.Exists(path), $"프리팹을 찾지 못했습니다: {path}");
            string text = File.ReadAllText(path);

            string[] docs = Regex.Split(text, @"(?m)^--- !u!\d+ &");
            var names = new Dictionary<string, string>();
            var raw = new List<(string ownerId, float min, float max)>();

            foreach (string doc in docs)
            {
                var idMatch = Regex.Match(doc, @"^(\d+)");
                if (!idMatch.Success) continue;
                string fileId = idMatch.Groups[1].Value;

                if (Regex.IsMatch(doc, @"(?m)^GameObject:"))
                {
                    var nameMatch = Regex.Match(doc, @"(?m)^\s*m_Name:\s*(.+)$");
                    if (nameMatch.Success) names[fileId] = nameMatch.Groups[1].Value.Trim();
                    continue;
                }

                if (!Regex.IsMatch(doc, @"(?m)^HingeJoint2D:")) continue;
                var ownerMatch = Regex.Match(doc, @"m_GameObject:\s*\{fileID:\s*(\d+)\}");
                var minMatch = Regex.Match(doc, @"(?m)^\s*m_LowerAngle:\s*(-?[0-9.]+)");
                var maxMatch = Regex.Match(doc, @"(?m)^\s*m_UpperAngle:\s*(-?[0-9.]+)");
                if (!ownerMatch.Success || !minMatch.Success || !maxMatch.Success) continue;

                raw.Add((ownerMatch.Groups[1].Value,
                    float.Parse(minMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(maxMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)));
            }

            var result = new List<PrefabJoint>();
            foreach ((string ownerId, float min, float max) in raw)
            {
                result.Add(new PrefabJoint
                {
                    OwnerName = names.TryGetValue(ownerId, out string n) ? n : "?",
                    Min = min,
                    Max = max,
                });
            }

            Assert.Greater(result.Count, 0, "프리팹에서 HingeJoint2D를 하나도 파싱하지 못했습니다 — 파서가 깨졌습니다.");
            return result;
        }
    }
}
