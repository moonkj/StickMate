using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using StickMate.Core;
using StickMate.States;
using UnityEditor;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ============================================================================
    /// 인용 감사 #5 — <b>「실제 두 설정을 나란히 세워 대조한다」던 그 기계</b>
    /// (test-engineer, 2026-09-06)
    /// ============================================================================
    /// <c>States/StickmanBlackboard.cs</c>의 <c>BuildDancePoseSettings</c> 문서 원문:
    ///
    /// <para><i>"★ 폴백 리터럴은 <b>StickConfig의 실효값과 같아야 한다</b>. 정상 실행에서는 절대
    /// 쓰이지 않으므로 어긋나도 화면이 안 바뀌고 그래서 조용히 낡는다 — 이 저장소가 한 파일에서만
    /// 여섯 개를 그렇게 잃은 적이 있다. <c>Tests/EditMode/ConfigFallbackLiteralDriftTests</c>가
    /// 정규식으로, <c>DancePoseFallbackParityTests</c>가 <b>실제 두 설정을 나란히 세워</b>
    /// (서로 다른 방법으로) 대조한다."</i></para>
    ///
    /// <b>둘 중 뒤쪽은 2026-09-06까지 존재하지 않았다.</b> 즉 «서로 다른 방법 두 개»라던 대조에서
    /// <b>한쪽만 살아 있었다</b>. 이 파일이 나머지 한쪽이다.
    ///
    /// ============================================================================
    /// ★ 「서로 다른 방법」이 왜 중요한가 — 무엇이 실제로 달라지는가
    /// ============================================================================
    /// <see cref="ConfigFallbackLiteralDriftTests"/>는 <b>소스 텍스트</b>를 정규식으로 훑는다.
    /// 이 파일은 <b>실행 결과</b>를 본다: 설정 없는 블랙보드와 배포 에셋을 문 블랙보드에서
    /// <c>BuildDancePoseSettings()</c>를 각각 부르고, 완성된 구조체를 <b>필드 단위로</b> 맞춰 본다.
    ///
    /// <para>그래서 정규식이 <b>구조적으로 못 보는</b> 것들이 여기서 잡힌다:</para>
    /// <list type="bullet">
    ///  <item>폴백이 숫자 리터럴이 아니라 <b>다른 상수/식</b>인 경우(정규식은 리터럴만 문다).</item>
    ///  <item><c>h *</c> 환산이 한쪽에만 붙거나 빠진 경우 — 값은 같은데 <b>단위가 다르다</b>.</item>
    ///  <item>생성자 인자 <b>순서</b>가 어긋나 값이 다른 슬롯에 들어간 경우(둘의 값이 다르면 잡힌다).</item>
    /// </list>
    ///
    /// <para>반대로 <b>이쪽이 못 보는 것</b>도 정직하게 적는다: 양쪽이 <b>같은 잘못된 배선</b>을
    /// 공유하면(같은 필드를 같은 슬롯에 잘못 넣었다면) 두 결과가 나란히 틀려서 통과한다.
    /// 그건 이 방법의 한계이고, 그래서 정규식 쪽을 <b>지우면 안 된다</b> — 두 다리가 필요하다.</para>
    ///
    /// ============================================================================
    /// ★ 기준은 <b>배포 에셋</b>이다 (2026-09-01 리더 정책 판정)
    /// ============================================================================
    /// <i>"폴백이 흉내 내야 하는 것은 «코드에 적힌 초기값»이 아니라 <b>실제로 돌아가는 값</b>이다."</i>
    /// 그래서 <see cref="ScriptableObject.CreateInstance{T}"/>(코드 기본값)가 아니라 디스크의
    /// <c>DefaultStickConfig.asset</c>을 문다. 이 저장소의 거짓 통과 #9가 정확히 그 혼동이었다
    /// (애셋이 코드 기본값을 덮는데 애셋을 안 고쳐 스위치가 꺼진 채 출하될 뻔했다).
    ///
    /// <para><b>플랫폼</b>: 플랫폼 중립. 에셋과 코드를 <b>읽기만</b> 한다 — 어떤 파일도 쓰지 않는다
    /// (양성 대조에서 값을 바꾸는 것은 <see cref="ScriptableObject.CreateInstance{T}"/>로 만든
    /// <b>메모리 안의 사본</b>이고 디스크에 저장하지 않는다).</para>
    /// </summary>
    public sealed class DancePoseFallbackParityTests
    {
        private const string LogPrefix = "[춤폴백대조]";

        /// <summary>배포 설정 에셋. <see cref="ConfigFallbackLiteralDriftTests"/>와 같은 경로를 본다 —
        /// 두 감사가 <b>같은 기준</b>을 봐야 «서로 다른 방법»이 같은 사실을 재는 것이 된다.</summary>
        private const string DeployedConfigPath = "Assets/_Project/Data/DefaultStickConfig.asset";

        /// <summary>비교가 공허해지는 것을 막는 바닥값. 2026-09-06 실측: 춤 7종 묶음의 말단 필드는
        /// 51개 이상이다(폴백 리터럴만 세어도 51개). 이 수 아래로 떨어지면 «차이 0건»은
        /// «같다»가 아니라 <b>«거의 안 봤다»</b>이다.</summary>
        private const int MinComparedLeaves = 40;

        private const float Tolerance = 1e-4f;

        // ====================================================================
        // 비교기 — 순수 함수. 아래 대조들이 <b>같은 함수</b>에 가짜 입력을 흘린다.
        // ====================================================================

        /// <summary>
        /// 두 값(구조체 포함)을 <b>말단 필드까지</b> 재귀로 맞춰 본다.
        /// <para>차이는 <paramref name="diffs"/>에 «경로 = 왼쪽 vs 오른쪽» 꼴로 쌓이고,
        /// 실제로 비교한 말단 개수는 <paramref name="leaves"/>로 돌아온다 —
        /// <b>0건이 「같다」인지 「아무것도 안 봤다」인지 가르는 유일한 수</b>다.</para>
        /// </summary>
        internal static void CompareDeep(object left, object right, string path,
            List<string> diffs, ref int leaves)
        {
            if (left == null || right == null)
            {
                if (!ReferenceEquals(left, right)) diffs.Add($"{path} = {left ?? "(null)"} vs {right ?? "(null)"}");
                leaves++;
                return;
            }

            Type type = left.GetType();
            if (type != right.GetType())
            {
                diffs.Add($"{path} = 형이 다르다 ({type.Name} vs {right.GetType().Name})");
                leaves++;
                return;
            }

            if (type == typeof(float))
            {
                leaves++;
                float a = (float)left;
                float b = (float)right;
                if (Mathf.Abs(a - b) > Tolerance) diffs.Add($"{path} = {a} vs {b}");
                return;
            }

            if (type.IsPrimitive || type.IsEnum || type == typeof(string))
            {
                leaves++;
                if (!left.Equals(right)) diffs.Add($"{path} = {left} vs {right}");
                return;
            }

            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            if (fields.Length == 0)
            {
                leaves++;
                if (!left.Equals(right)) diffs.Add($"{path} = {left} vs {right}");
                return;
            }

            foreach (FieldInfo field in fields)
                CompareDeep(field.GetValue(left), field.GetValue(right),
                    path.Length == 0 ? field.Name : path + "." + field.Name, diffs, ref leaves);
        }

        // ====================================================================
        // 리그
        // ====================================================================

        private static StickConfig LoadDeployedConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<StickConfig>(DeployedConfigPath);
            Assert.IsNotNull(config,
                $"{LogPrefix} 배포 설정 에셋을 찾지 못했습니다: {DeployedConfigPath}\n" +
                "경로가 바뀌었다면 ConfigFallbackLiteralDriftTests도 같은 경로를 보고 있으므로 " +
                "두 감사를 함께 고쳐야 합니다. 그대로 두면 이 대조는 <b>실행되지 않고</b> " +
                "«차이 0건»조차 나오지 않습니다.");
            return config;
        }

        /// <summary><see cref="StickmanBlackboard.Body"/>가 없으므로 <c>Metrics</c>도 null이고,
        /// <c>CharacterHeightWorld</c>는 양쪽 다 <c>StickConfig.BaselineCharacterTotalHeight</c>로
        /// 되메워진다 — <b>그래야 <c>h *</c> 환산이 걸린 필드들을 같은 조건에서 비교할 수 있다.</b>
        /// 그 전제는 아래에서 <b>단언으로</b> 확인한다(가정으로 두지 않는다).</summary>
        private static StickmanBlackboard MakeBlackboard(StickConfig config)
            => new StickmanBlackboard { Config = config };

        // ====================================================================
        // 1. ★ 본론 — 실제 두 설정을 나란히 세운다
        // ====================================================================

        [Test]
        public void 춤_폴백_리터럴이_배포_설정의_실효값과_같다()
        {
            StickConfig deployed = LoadDeployedConfig();

            StickmanBlackboard fallback = MakeBlackboard(null);
            StickmanBlackboard effective = MakeBlackboard(deployed);

            // ★ 전제 확인 — 신장이 다르면 h 환산이 걸린 필드가 «값이 달라서»가 아니라
            //   «분모가 달라서» 어긋난다. 그 거짓 빨강을 여기서 먼저 배제한다.
            Assert.AreEqual(fallback.CharacterHeightWorld, effective.CharacterHeightWorld, Tolerance,
                $"{LogPrefix} 두 블랙보드의 신장이 다릅니다 " +
                $"({fallback.CharacterHeightWorld} vs {effective.CharacterHeightWorld}). " +
                "Body/Metrics 없는 경로가 더 이상 같은 값으로 되메워지지 않는다는 뜻이고, " +
                "그 상태에서 아래 비교는 <b>단위가 다른 두 수</b>를 맞대는 것이라 무의미합니다.");

            var diffs = new List<string>();
            int leaves = 0;
            CompareDeep(fallback.BuildDancePoseSettings(), effective.BuildDancePoseSettings(),
                string.Empty, diffs, ref leaves);

            Debug.Log($"{LogPrefix} 말단 {leaves}개 비교 / 차이 {diffs.Count}건");

            // ★ 「0건」이 초록불이 되는 것을 먼저 막는다.
            Assert.GreaterOrEqual(leaves, MinComparedLeaves,
                $"{LogPrefix} 말단 필드를 {leaves}개밖에 비교하지 못했습니다(바닥값 {MinComparedLeaves}). " +
                "구조체가 필드 대신 프로퍼티로 바뀌었거나 비교기가 재귀를 멈췄습니다 — " +
                "이 상태의 «차이 0건»은 «같다»가 아니라 «거의 안 봤다»입니다.");

            Assert.IsEmpty(diffs,
                $"{LogPrefix} 춤 폴백 리터럴이 배포 설정의 실효값과 갈라졌습니다:\n  " +
                string.Join("\n  ", diffs) + "\n" +
                "이 드리프트는 <b>화면에 안 나타납니다</b> — 정상 실행에서는 폴백이 쓰이지 않기 " +
                "때문입니다. 대신 <b>설정 에셋 없이 블랙보드를 만드는 테스트들</b>이 프로덕션과 " +
                "다른 자세를 검증하게 되고, 그 순간 그 테스트들의 초록은 아무 뜻이 없어집니다. " +
                "폴백 쪽을 실효값으로 맞추세요(이 저장소는 한 파일에서만 여섯 개를 그렇게 잃은 적이 " +
                "있습니다). 의도된 차이라면 ConfigFallbackLiteralDriftTests의 Ledger에 " +
                "<b>사유와 두 값을 함께</b> 등재하고, 이 파일에도 같은 예외를 명시하세요 — " +
                "한쪽에만 적으면 두 감사가 서로 다른 판정을 냅니다.");
        }

        // ====================================================================
        // 2. 양성 대조 — 비교기가 실제로 차이를 잡는가
        // ====================================================================

        /// <summary>
        /// ★ 디스크의 에셋은 <b>건드리지 않는다</b>. <see cref="ScriptableObject.CreateInstance{T}"/>로
        /// 만든 <b>메모리 안의 사본</b> 하나만 바꾼다(유저 자산 불변 원칙은 테스트에도 적용된다 —
        /// 이 저장소는 «애셋을 안 고쳐 스위치가 꺼진 채 출하될 뻔한» 사고와 그 반대편 사고를 둘 다 겪었다).
        /// </summary>
        [Test]
        public void 양성대조_한_값만_바꾸면_그_경로가_정확히_한_건_잡힌다()
        {
            var mutated = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                StickmanBlackboard baseline = MakeBlackboard(mutated);
                StickmanPoseAnimator.DancePoseSettings before = baseline.BuildDancePoseSettings();

                // 코드 기본값에서 한 값만 옮긴다. 값 자체는 의미가 없다 — «달라졌다»만 필요하다.
                mutated.dancePirouetteArmDegrees += 7.5f;
                StickmanPoseAnimator.DancePoseSettings after = baseline.BuildDancePoseSettings();

                var diffs = new List<string>();
                int leaves = 0;
                CompareDeep(before, after, string.Empty, diffs, ref leaves);

                Assert.GreaterOrEqual(leaves, MinComparedLeaves,
                    $"{LogPrefix} 말단을 {leaves}개만 비교했습니다 — 비교기가 재귀를 멈췄습니다.");
                Assert.AreEqual(1, diffs.Count,
                    $"{LogPrefix} 한 값만 바꿨는데 차이가 {diffs.Count}건입니다: " +
                    string.Join(" / ", diffs) + "\n" +
                    "0건이면 비교기가 <b>아무것도 못 보는 상태</b>이고(그러면 위의 «차이 0건»은 " +
                    "«같다»를 뜻하지 않습니다), 2건 이상이면 한 설정 필드가 여러 슬롯에 " +
                    "동시에 흘러 들어가고 있다는 뜻입니다.");
                StringAssert.Contains("ArmDegrees", diffs[0],
                    $"{LogPrefix} 바뀐 값이 엉뚱한 경로로 보고됐습니다: {diffs[0]}. " +
                    "실패 메시지가 «어디»를 못 가리키면 이 감사는 빨개져도 쓸모가 없습니다.");
                StringAssert.Contains("Pirouette", diffs[0],
                    $"{LogPrefix} 경로에 소속 묶음이 없습니다: {diffs[0]}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mutated);
            }
        }

        /// <summary>같은 것을 비교하면 0건 — 비교기가 <b>아무 데서나 빨개지지 않는가</b>.</summary>
        [Test]
        public void 음성대조_같은_설정끼리는_차이가_0건이다()
        {
            StickConfig deployed = LoadDeployedConfig();
            StickmanBlackboard a = MakeBlackboard(deployed);
            StickmanBlackboard b = MakeBlackboard(deployed);

            var diffs = new List<string>();
            int leaves = 0;
            CompareDeep(a.BuildDancePoseSettings(), b.BuildDancePoseSettings(), string.Empty, diffs, ref leaves);

            Assert.IsEmpty(diffs,
                $"{LogPrefix} <b>같은 설정</b>으로 만든 두 결과가 다릅니다: " + string.Join(" / ", diffs) + "\n" +
                "빌드 결과에 시간·난수 같은 비결정 요소가 섞였다는 뜻이고, 그러면 위 본론 대조는 " +
                "언제 빨개질지 모르는 불안정 테스트가 됩니다.");
            Assert.GreaterOrEqual(leaves, MinComparedLeaves,
                $"{LogPrefix} 말단을 {leaves}개만 비교했습니다.");
        }

        // ====================================================================
        // 3. 비교기 자체의 네거티브 컨트롤 — 가짜 값을 같은 함수에 흘린다
        // ====================================================================

        [Test]
        public void NegativeControl_비교기가_중첩_구조체_말단까지_들어간다()
        {
            var left = new Nested(new Leaf(1f, 2), new Leaf(3f, 4));
            var same = new Nested(new Leaf(1f, 2), new Leaf(3f, 4));
            var deep = new Nested(new Leaf(1f, 2), new Leaf(3f, 5));   // 두 칸 안쪽의 int 하나

            var diffs = new List<string>();
            int leaves = 0;
            CompareDeep(left, same, string.Empty, diffs, ref leaves);
            Assert.IsEmpty(diffs, $"{LogPrefix} 같은 값을 다르다고 했습니다.");
            Assert.AreEqual(4, leaves,
                $"{LogPrefix} 말단을 {leaves}개로 셌습니다(기대 4) — 재귀가 얕습니다.");

            diffs.Clear();
            leaves = 0;
            CompareDeep(left, deep, string.Empty, diffs, ref leaves);
            Assert.AreEqual(1, diffs.Count,
                $"{LogPrefix} 두 칸 안쪽의 차이를 {diffs.Count}건으로 봤습니다: " + string.Join(" / ", diffs));
            StringAssert.Contains("B.Count", diffs[0],
                $"{LogPrefix} 경로가 말단까지 안 적혔습니다: {diffs[0]}");
        }

        [Test]
        public void NegativeControl_허용오차_안팎을_가른다()
        {
            var diffs = new List<string>();
            int leaves = 0;

            CompareDeep(new Leaf(1f, 0), new Leaf(1f + Tolerance * 0.5f, 0), string.Empty, diffs, ref leaves);
            Assert.IsEmpty(diffs, $"{LogPrefix} 허용오차 <b>안</b>의 float를 차이로 셌습니다 — " +
                                  "부동소수 마지막 자리 때문에 이 감사가 상시 빨강이 됩니다.");

            diffs.Clear();
            CompareDeep(new Leaf(1f, 0), new Leaf(1f + Tolerance * 10f, 0), string.Empty, diffs, ref leaves);
            Assert.AreEqual(1, diffs.Count,
                $"{LogPrefix} 허용오차 <b>밖</b>의 float를 놓쳤습니다 — 허용오차가 너무 큽니다.");
        }

        // 비교기 대조용 표본. 프로덕션 타입을 흉내 낸 <b>중첩 readonly 구조체</b>다.
        private readonly struct Leaf
        {
            public readonly float Value;
            public readonly int Count;
            public Leaf(float value, int count) { Value = value; Count = count; }
        }

        private readonly struct Nested
        {
            public readonly Leaf A;
            public readonly Leaf B;
            public Nested(Leaf a, Leaf b) { A = a; B = b; }
        }
    }
}
