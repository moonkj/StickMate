using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.States;
using UnityEditor;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 팔다리 곡선화(<see cref="LimbCurveRenderer"/>)의 기하학 계약(2026-09-01).
    ///
    /// <para><b>왜 필요한가.</b> 사용자 신고 "아직도 고도화가 덜됨 / 몸의 선 자체가 얇아서"에 대응해
    /// (a) 무릎·팔꿈치를 원호로 갈아내고 (b) 획을 굵혔다. 이 둘은 <b>서로를 필요로 한다</b> —
    /// 각진 관절에 굵은 획을 얹으면 안쪽에 잉크가 뭉치고(2026-08-28에 겪고 후퇴한 그 실패),
    /// 곡선만 넣으면 여전히 앙상하다. 그래서 "곡선이 실제로 뭉침을 막을 만큼 충분한가"를
    /// 숫자로 잠근다.</para>
    ///
    /// <para><b>이 파일에는 프로덕션 숫자를 베껴 적지 않는다</b>(CLAUDE.md 규칙). 전부 두 곳에서 읽는다:
    /// <list type="bullet">
    /// <item>곡선 파라미터 → <see cref="LimbCurveRenderer"/>의 public const</item>
    /// <item>마디 길이/획 두께 → <b>실제로 구워진 프리팹</b>(Stickman.prefab)</item>
    /// <item>관절 굽힘 각도 범위 → <see cref="StickConfig"/>의 무릎/팔꿈치 필드 <b>전수 조사</b> +
    ///       RAGDOLL 한계(Editor/SceneBootstrapper.cs 소스 파싱)</item>
    /// </list>
    /// 각도를 전수 조사하는 이유: 누가 나중에 더 깊게 접히는 포즈를 추가해도 이 테스트가
    /// <b>자동으로</b> 그 각도까지 검사한다. 목록을 손으로 적으면 그 순간 낡는다.</para>
    /// </summary>
    public sealed class LimbCurveGeometryTests
    {
        private const string LogPrefix = "[곡선]";
        private const string PrefabAssetPath = "Assets/_Project/Prefabs/Stickman.prefab";
        private const string BootstrapperRelativePath = "Editor/SceneBootstrapper.cs";

        // ====================================================================
        // 리그 — 실제 컴포넌트를 실제 계층 위에서 돌린다(수식을 테스트에 다시 적지 않는다)
        // ====================================================================

        private sealed class Rig
        {
            public GameObject Root;
            public LineRenderer Upper;
            public LineRenderer Lower;
            public Transform LowerTransform;
            public float UpperLength;
            public float LowerLength;
            public float Width;
            public float RootScale;
        }

        /// <summary>네 팔다리를 모두 같은 규격으로 만든 리그. 넷 다 만드는 이유는
        /// <see cref="LimbCurveRenderer"/>가 일부만 찾으면 경고를 남기기 때문이다(정상 동작).</summary>
        private static Rig BuildRig(float upperLength, float lowerLength, float width, float bendDegrees)
            => BuildRig(upperLength, lowerLength, width, bendDegrees, 1f);

        /// <summary>
        /// ★ <b>캐릭터 배율까지 재현하는</b> 리그(2026-09-01). 프로덕션
        /// (<see cref="StickmanAgent.ApplyCharacterScale"/>)이 배율을 적용하는 방식을 <b>그대로</b> 흉내낸다:
        /// <list type="number">
        /// <item>루트 <c>localScale</c>에 배율비를 넣는다 — 마디 길이는 <b>로컬 값 그대로</b> 남는다.</item>
        /// <item>획 두께는 <b>월드 유닛</b>으로 직접 대입한다 — Transform 스케일을 따라가지 않으므로
        ///       (StickmanAgent._bakedStrokeWidths 문서) 배율비를 곱하고 화면 하한으로 자른 값이다.</item>
        /// </list>
        ///
        /// <para><b>왜 이래야만 하는가.</b> 길이만 곱한 "월드 단위 리그"를 만들면 루트 스케일이 1이라
        /// 프로덕션의 <b>단위 환산 경로가 아예 실행되지 않는다</b> — 2026-09-01에 고친 단위 불일치
        /// (docs/CHARACTER_FORM_SPEC.md 4-3)를 누가 되돌려도 그런 리그는 초록으로 통과한다.
        /// 루트를 실제로 스케일해야 그 경로가 돌고, 그래야 이 테스트가 그 버그를 잡는다.</para>
        /// </summary>
        private static Rig BuildRig(float upperLength, float lowerLength, float width, float bendDegrees,
            float rootScale)
        {
            var root = new GameObject("CurveRig");
            root.transform.localScale = new Vector3(rootScale, rootScale, 1f);
            LineRenderer probeUpper = null, probeLower = null;
            Transform probeLowerT = null;

            string[] names = { "LeftLeg", "RightLeg", "LeftArm", "RightArm" };
            foreach (string name in names)
            {
                var upper = new GameObject(name);
                upper.transform.SetParent(root.transform, false);
                LineRenderer ul = MakeLine(upper, upperLength, width);

                var lower = new GameObject(name + "Lower");
                lower.transform.SetParent(upper.transform, false);
                lower.transform.localPosition = new Vector3(0f, -upperLength, 0f);
                lower.transform.localRotation = Quaternion.Euler(0f, 0f, bendDegrees);
                LineRenderer ll = MakeLine(lower, lowerLength, width);

                if (probeUpper == null)
                {
                    probeUpper = ul; probeLower = ll; probeLowerT = lower.transform;
                }
            }

            root.AddComponent<LimbCurveRenderer>().BakeEditorPreview();

            return new Rig
            {
                Root = root, Upper = probeUpper, Lower = probeLower, LowerTransform = probeLowerT,
                UpperLength = upperLength, LowerLength = lowerLength, Width = width, RootScale = rootScale,
            };
        }

        private static LineRenderer MakeLine(GameObject go, float length, float width)
        {
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.positionCount = 2;
            lr.SetPosition(0, Vector3.zero);
            lr.SetPosition(1, new Vector3(0f, -length, 0f));
            return lr;
        }

        private static Vector3[] Read(LineRenderer lr)
        {
            var p = new Vector3[lr.positionCount];
            lr.GetPositions(p);
            return p;
        }

        /// <summary>아래 마디 로컬 좌표를 위 마디 로컬 좌표로 옮긴다(무릎/팔꿈치 = (0, −Lu)).</summary>
        private static Vector2 ToUpperFrame(Vector3 lowerLocal, float bendDegrees, float upperLength)
        {
            Vector3 rotated = Quaternion.Euler(0f, 0f, bendDegrees) * lowerLocal;
            return new Vector2(rotated.x, rotated.y - upperLength);
        }

        /// <summary>세 점을 지나는 원의 반지름(외접원). 곡선 위 연속한 세 표본에 쓰면 그 구간의
        /// 곡률 반경이 그대로 나온다 — 프로덕션 수식을 다시 적지 않고 <b>결과에서</b> 재는 방법이다.</summary>
        private static float Circumradius(Vector2 p0, Vector2 p1, Vector2 p2)
        {
            float a = Vector2.Distance(p0, p1);
            float b = Vector2.Distance(p1, p2);
            float c = Vector2.Distance(p2, p0);
            float cross = (p1.x - p0.x) * (p2.y - p0.y) - (p1.y - p0.y) * (p2.x - p0.x);
            float area2 = Mathf.Abs(cross);
            if (area2 < 1e-9f) return float.PositiveInfinity; // 일직선 = 곡률 0
            return a * b * c / (2f * area2);
        }

        // ====================================================================
        // 프로덕션에서 값 읽어오기
        // ====================================================================

        private static GameObject LoadPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);
            Assert.IsNotNull(prefab, $"{LogPrefix} 프리팹을 찾지 못했습니다: {PrefabAssetPath} — " +
                "메뉴 StickMate/Rebuild All로 먼저 구워야 합니다.");
            return prefab;
        }

        /// <summary>프리팹에서 (위 마디 길이, 아래 마디 길이, 획 두께)를 실측한다.
        /// 길이는 선의 <b>마지막 점 |y|</b>(곡선으로 구워져 있어도 마지막 점은 항상 마디 끝이다).</summary>
        private static (float upper, float lower, float width) ReadLimbSpec(GameObject prefab, string limbName)
        {
            Transform upper = prefab.transform.Find(limbName);
            Assert.IsNotNull(upper, $"{LogPrefix} 프리팹에 '{limbName}'이 없습니다.");
            Transform lower = upper.Find(limbName + "Lower");
            Assert.IsNotNull(lower, $"{LogPrefix} 프리팹에 '{limbName}Lower'가 없습니다.");

            var ul = upper.GetComponent<LineRenderer>();
            var ll = lower.GetComponent<LineRenderer>();
            Assert.IsNotNull(ul); Assert.IsNotNull(ll);

            // 마디 끝 인덱스를 PointsPerSegment−1로 자르는 이유는 프로덕션
            // (LimbCurveRenderer.ReadSegmentLength)과 같다: 2026-09-01에 잠깐 있었던 "발"이
            // 마디 끝 뒤에 점을 하나 더 붙인 프리팹이 남아 있어도 마디 길이를 잘못 읽지 않는다.
            int lowerEnd = Mathf.Min(ll.positionCount - 1, LimbCurveRenderer.PointsPerSegment - 1);

            return (Mathf.Abs(ul.GetPosition(ul.positionCount - 1).y),
                    Mathf.Abs(ll.GetPosition(lowerEnd).y),
                    ll.startWidth);
        }

        /// <summary>
        /// 이 캐릭터가 실제로 취할 수 있는 <b>무릎/팔꿈치 굽힘 각도 전부</b>(도, 절댓값).
        /// StickConfig의 public float 필드 중 이름에 Knee/Elbow가 들어가고 Degrees로 끝나는 것을
        /// 전수 조사하고, RAGDOLL의 관절 한계(MaxJointBendDegrees)를 더한다.
        /// </summary>
        private static List<float> CollectJointBendAngles() => CollectBendAngles(null);

        /// <summary>
        /// ★ <b>관절별</b> 각도 전수 조사(2026-09-01). <paramref name="jointKeyword"/>가
        /// <c>"Knee"</c>면 무릎만, <c>"Elbow"</c>면 팔꿈치만, <c>null</c>이면 둘 다.
        /// RAGDOLL 한계(<c>MaxJointBendDegrees</c>)는 <b>양쪽 관절에 실제로 걸리므로</b> 언제나 더한다.
        ///
        /// <para><b>왜 나눴나(중요).</b> 예전에는 무릎 각도와 팔꿈치 각도를 <b>합쳐서</b> 다리와 팔
        /// <b>양쪽에 전부</b> 대입했다. 그건 <c>landingCrouchFrontKneeDegrees(126°)</c>를 <b>팔꿈치</b>에
        /// 넣어 보는 것과 같은데, 팔꿈치의 실제 최대는 <c>idleAmbientLookElbowDegrees(98°)</c>라
        /// <b>어떤 상태 조합으로도 도달할 수 없는 자세</b>다. 배율 0.75 하나만 재던 시절에는 여유가 커서
        /// 티가 안 났지만, 배율 0.35까지 훑는 순간 그 <b>불가능한 조합</b>이 유일한 실패로 떠서 진짜
        /// 위반을 가린다. 검사 대상은 "이 캐릭터가 실제로 취하는 자세"여야 한다.</para>
        /// </summary>
        private static List<float> CollectBendAngles(string jointKeyword)
        {
            var angles = new List<float>();
            var config = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                FieldInfo[] fields = typeof(StickConfig).GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (FieldInfo f in fields)
                {
                    if (f.FieldType != typeof(float)) continue;
                    if (!f.Name.EndsWith("Degrees")) continue;
                    bool knee = f.Name.IndexOf("Knee", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    bool elbow = f.Name.IndexOf("Elbow", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!knee && !elbow) continue;
                    if (jointKeyword != null &&
                        f.Name.IndexOf(jointKeyword, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    float v = Mathf.Abs((float)f.GetValue(config));
                    if (v > 0.001f) angles.Add(v);
                }
            }
            finally
            {
                Object.DestroyImmediate(config);
            }

            angles.Add(ReadBootstrapperConst("MaxJointBendDegrees"));
            angles.Sort();
            Assert.Greater(angles.Count, 5,
                $"{LogPrefix} 관절 각도({jointKeyword ?? "전체"})를 {angles.Count}개밖에 못 모았습니다 — " +
                "전수 조사가 아무것도 못 보고 초록이 되는 상태(거짓 통과)입니다.");
            return angles;
        }

        // ====================================================================
        // ★ 배율 스윕 — 이 라운드의 핵심(2026-09-01)
        // ====================================================================
        //
        // <b>기존 테스트가 위반을 못 잡은 이유가 정확히 여기다.</b> (3)(4)는 ReadLimbSpec(prefab)으로
        // <b>프리팹에 구워진 폭</b>만 읽었고, 프리팹은 StickConfig.characterScale(출하 기본)로 구워지므로
        // <b>언제나 그 배율 하나</b>만 검사했다. 사용자가 다이얼을 내린 조합은 검사 범위 밖이었고,
        // 실제 위반은 바로 그 구간(배율 0.45 미만)에 있었다(docs/CHARACTER_FORM_SPEC.md 4-2).
        //
        // 원인은 <b>화면상 획 하한</b>이다: 배율을 내리면 기하학은 줄지만 획은 2pt에서 멈춘다.
        // 그래서 낮은 배율일수록 "마디에 비해 획이 굵은" 상태가 되고, 필렛 원호가 획 반두께보다
        // 작아지는 순간 안쪽 윤곽이 자기교차한다.

        /// <summary>프리팹이 구워진 배율. <see cref="StickmanMetrics.Scale"/>과 <b>같은 식</b>이다
        /// (실측 전신 높이 ÷ 배율 1.0 기준 신장) — 숫자를 베끼지 않기 위해 그 정의를 그대로 옮긴다.</summary>
        private static float BakedScale(GameObject prefab)
        {
            float height = 0f;
            foreach (CapsuleCollider2D c in prefab.GetComponents<CapsuleCollider2D>())
            {
                if (c != null && !c.isTrigger) height = Mathf.Max(height, c.size.y);
            }
            Assert.Greater(height, 0f, $"{LogPrefix} 프리팹에서 전신 높이를 못 읽었습니다.");
            float baked = height / StickConfig.BaselineCharacterTotalHeight;
            Assert.Greater(baked, 0.0001f, $"{LogPrefix} 구워진 배율이 0입니다.");
            return baked;
        }

        /// <summary>검사할 배율 목록 — 다이얼이 갈 수 있는 <b>전 구간</b>(<see cref="StickConfig.MinCharacterScale"/>
        /// ~ <see cref="StickConfig.MaxCharacterScale"/>)을 균등 분할하고 <b>구워진 배율</b>을 더한다.
        /// 최악값은 언제나 하단(획 하한이 가장 크게 부푸는 곳)이므로 하한은 반드시 포함된다.</summary>
        private static List<float> ScaleSamples(GameObject prefab)
        {
            const int steps = 8;
            var scales = new List<float>(steps + 2);
            for (int i = 0; i <= steps; i++)
            {
                scales.Add(Mathf.Lerp(StickConfig.MinCharacterScale, StickConfig.MaxCharacterScale,
                    i / (float)steps));
            }
            scales.Add(BakedScale(prefab));
            scales.Sort();
            return scales;
        }

        /// <summary>배율 <paramref name="scale"/>에서 <b>실제로 그려지는</b> 획 두께(월드 유닛).
        /// 프로덕션(<c>StickmanAgent.ApplyStrokeWidthsForScale</c>)과 <b>같은 두 단계</b>다:
        /// 구워진 폭에 배율비를 곱하고, 화면상 하한(<see cref="StickConfig.MinStrokeScreenPoints"/>)으로 자른다.</summary>
        private static float WorldStrokeWidth(float bakedWidth, float bakedScale, float scale)
        {
            float floorWorld = StickConfig.MinStrokeScreenPoints / StickConfig.ReferencePointsPerWorldUnitApprox;
            return Mathf.Max(bakedWidth * (scale / bakedScale), floorWorld);
        }

        /// <summary>
        /// ★ <b>규칙 B</b>(단조 체인)의 임계 비율. 두께 W인 폴리라인의 연속 꼭짓점이 전부 같은 쪽으로
        /// Δφ씩 꺾이면 안쪽 오프셋이 양 끝에서 동시에 깎이므로 <c>선분 ≥ W·tan(Δφ/2)</c>가 필요하고,
        /// 원호에서 <c>선분 = 2r·sin(Δφ/2)</c>이므로 이는 <c>r ≥ (W/2)/cos(Δφ/2)</c>와 <b>같은 부등식</b>이다.
        /// 즉 규칙 B는 곡률 규칙 C를 <c>1/cos(Δφ/2)</c>배(최대 1.7%)만큼 조인 것이다
        /// (docs/CHARACTER_FORM_SPEC.md 4-1). <b>그래서 본체 팔다리에 "최단 선분" 테스트를 따로 만들지
        /// 않는다</b> — 그건 같은 부등식을 두 벌 적는 것이다.
        /// </summary>
        private static float CreaseThreshold(float bendDegrees)
        {
            float deltaPhi = 0.5f * Mathf.Abs(bendDegrees) * Mathf.Deg2Rad
                / (LimbCurveRenderer.ArcSamplesPerHalf - 1);
            return 1f / Mathf.Cos(0.5f * deltaPhi);
        }

        private static float ReadBootstrapperConst(string name)
        {
            string path = Path.Combine(Application.dataPath, BootstrapperRelativePath);
            Assert.IsTrue(File.Exists(path), $"{LogPrefix} SceneBootstrapper.cs를 찾지 못했습니다: {path}");
            string src = File.ReadAllText(path);
            Match m = Regex.Match(src, @"const\s+float\s+" + Regex.Escape(name) + @"\s*=\s*([-\d.]+)f");
            Assert.IsTrue(m.Success, $"{LogPrefix} SceneBootstrapper.cs에서 상수 {name}을 못 읽었습니다.");
            return Mathf.Abs(float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        // ====================================================================
        // (1) 이음매 — 두 반호가 관절에서 정확히 만나고 기울기까지 이어진다
        // ====================================================================

        [Test]
        public void 위마디와_아래마디의_곡선이_관절에서_정확히_이어진다()
        {
            GameObject prefab = LoadPrefab();
            var specs = new[] { ("LeftLeg", ReadLimbSpec(prefab, "LeftLeg")), ("LeftArm", ReadLimbSpec(prefab, "LeftArm")) };
            List<float> angles = CollectJointBendAngles();

            float worstGap = 0f, worstTurn = 0f;
            string worstWhere = "";

            foreach (var (name, spec) in specs)
            foreach (float raw in angles)
            foreach (float bend in new[] { raw, -raw })
            {
                Rig rig = BuildRig(spec.upper, spec.lower, spec.width, bend);
                try
                {
                    Vector3[] up = Read(rig.Upper);
                    Vector3[] lo = Read(rig.Lower);

                    Vector2 upEnd = up[up.Length - 1];
                    Vector2 loStart = ToUpperFrame(lo[0], bend, spec.upper);
                    float gap = Vector2.Distance(upEnd, loStart);

                    // 이음매의 꺾임이 다른 표본 간격의 꺾임과 같아야 "이음매가 특별한 자리가 아님"이다.
                    Vector2 beforeJoint = up[up.Length - 2];
                    Vector2 afterJoint = ToUpperFrame(lo[1], bend, spec.upper);
                    float jointTurn = Vector2.Angle(upEnd - beforeJoint, afterJoint - loStart);
                    // 호 전체를 (2 × (ArcSamplesPerHalf−1))개 변으로 나누므로 변 하나의 회전각.
                    float expectedTurn = Mathf.Abs(bend) / (2f * (LimbCurveRenderer.ArcSamplesPerHalf - 1));
                    float turnError = Mathf.Abs(jointTurn - expectedTurn);

                    if (gap > worstGap) { worstGap = gap; worstWhere = $"{name} {bend:F0}도"; }
                    worstTurn = Mathf.Max(worstTurn, turnError);
                }
                finally { Object.DestroyImmediate(rig.Root); }
            }

            Debug.Log($"{LogPrefix} 이음매 최대 벌어짐 = {worstGap:E2}유닛 ({worstWhere}), " +
                $"이음매 꺾임과 일반 표본 꺾임의 최대 차이 = {worstTurn:F4}도. " +
                $"검사 각도 {angles.Count}종 × 부호 2 × 팔다리 2종.");

            Assert.Less(worstGap, 1e-4f,
                $"{LogPrefix} 위/아래 마디의 곡선이 관절에서 {worstGap:E2}유닛 벌어집니다({worstWhere}) — " +
                "두 반호는 같은 원 위에 있어야 하므로 정확히 만나야 합니다.");
            Assert.Less(worstTurn, 0.5f,
                $"{LogPrefix} 이음매의 꺾임이 다른 표본 구간보다 {worstTurn:F3}도 더 큽니다 — " +
                "관절 자리에만 각이 남아 있다는 뜻이고, 그것이 바로 없애려던 그 각진 모서리입니다.");
        }

        // ====================================================================
        // (2) 끝점 불변 — 물리/IK/접지는 손대지 않았다
        // ====================================================================

        [Test]
        public void 관절과_끝점은_곡선화_뒤에도_한치도_움직이지_않는다()
        {
            GameObject prefab = LoadPrefab();
            var spec = ReadLimbSpec(prefab, "LeftLeg");
            List<float> angles = CollectJointBendAngles();

            foreach (float bend in angles)
            {
                Rig rig = BuildRig(spec.upper, spec.lower, spec.width, bend);
                try
                {
                    Vector3[] up = Read(rig.Upper);
                    Vector3[] lo = Read(rig.Lower);

                    Assert.AreEqual(0f, up[0].magnitude, 1e-6f,
                        $"{LogPrefix} 위 마디의 첫 점이 관절(0,0)에서 벗어났습니다 — 회전 중심과 " +
                        "선의 시작점이 어긋나면 팔다리가 몸에서 떨어져 보입니다.");
                    Assert.AreEqual(-spec.lower, lo[lo.Length - 1].y, 1e-6f,
                        $"{LogPrefix} 아래 마디의 끝점(발끝/손끝) y가 바뀌었습니다 — " +
                        "접지 판정/보폭/사격 조준이 이 좌표에 얹혀 있습니다.");
                    Assert.AreEqual(0f, lo[lo.Length - 1].x, 1e-6f,
                        $"{LogPrefix} 아래 마디의 끝점 x가 0이 아닙니다(굽힘 {bend:F0}도).");
                }
                finally { Object.DestroyImmediate(rig.Root); }
            }
        }

        // ====================================================================
        // (3) ★ 곡률 상한 — 관절이 녹아 흐물거리지 않는다
        // ====================================================================

        [Test]
        public void 관절_끝점이_획_두께_이상으로_안쪽으로_물러나지_않는다()
        {
            GameObject prefab = LoadPrefab();
            float baked = BakedScale(prefab);
            List<float> scales = ScaleSamples(prefab);

            float worstRatio = 0f; string worstWhere = "";

            foreach (LimbCase limb in LimbCases(prefab))
            foreach (float scale in scales)
            {
                float worldWidth = WorldStrokeWidth(limb.Spec.width, baked, scale);
                float k = scale / baked;

                foreach (float bend in limb.Angles)
                {
                    Rig rig = BuildRig(limb.Spec.upper, limb.Spec.lower, worldWidth, bend, k);
                    try
                    {
                        Vector3[] up = Read(rig.Upper);
                        // 원래 관절 끝점(각진 corner) = (0, −Lu). 곡선의 관절부 최고점과의 거리 = sagitta.
                        // 점은 로컬이고 획은 월드다 — 이 어긋남이 바로 4-3의 버그였다. 점에 루트
                        // 스케일을 곱해 둘 다 월드로 맞춘 뒤에 비교한다.
                        Vector2 corner = new Vector2(0f, -limb.Spec.upper);
                        float sagitta = Vector2.Distance(up[up.Length - 1], corner) * k;
                        float ratio = sagitta / worldWidth;
                        if (ratio > worstRatio)
                        {
                            worstRatio = ratio;
                            worstWhere = $"{limb.Name} {bend:F0}도 배율 {scale:F2}";
                        }
                    }
                    finally { Object.DestroyImmediate(rig.Root); }
                }
            }

            Debug.Log($"{LogPrefix} 관절 후퇴량(sagitta) 최대 = 획 두께의 {worstRatio:F2}배 ({worstWhere}), " +
                $"상한 {LimbCurveRenderer.MaxSagittaPerStrokeWidth:F2}배. " +
                $"배율 {scales.Count}종({scales[0]:F2}~{scales[scales.Count - 1]:F2}) 전수.");

            Assert.LessOrEqual(worstRatio, LimbCurveRenderer.MaxSagittaPerStrokeWidth + 1e-3f,
                $"{LogPrefix} 관절이 획 두께의 {worstRatio:F2}배만큼 안으로 물러났습니다({worstWhere}) — " +
                "무릎앉아 착지/활쏘기에서 관절이 사라져 흐물거립니다.");
        }

        /// <summary>검사 대상 조합: (마디 이름, 프리팹 실측 규격, <b>그 관절이 실제로 취하는</b> 각도들).
        /// 무릎 각도는 다리에만, 팔꿈치 각도는 팔에만 대입한다(<see cref="CollectBendAngles"/> 문서).</summary>
        private static List<LimbCase> LimbCases(GameObject prefab)
        {
            return new List<LimbCase>
            {
                new LimbCase { Name = "LeftLeg", Spec = ReadLimbSpec(prefab, "LeftLeg"), Angles = CollectBendAngles("Knee") },
                new LimbCase { Name = "LeftArm", Spec = ReadLimbSpec(prefab, "LeftArm"), Angles = CollectBendAngles("Elbow") },
            };
        }

        private sealed class LimbCase
        {
            public string Name;
            public (float upper, float lower, float width) Spec;
            public List<float> Angles;
        }

        // ====================================================================
        // (4) ★★ 안쪽 크리즈 — 두께 상향이 안전한 이유 그 자체
        // ====================================================================

        [Test]
        public void 곡률_반경이_획_반두께보다_커서_안쪽에_각진_크리즈가_남지_않는다()
        {
            GameObject prefab = LoadPrefab();
            float baked = BakedScale(prefab);
            List<float> scales = ScaleSamples(prefab);

            float worstMargin = float.PositiveInfinity; string worstWhere = "";
            float worstScale = 0f;

            foreach (LimbCase limb in LimbCases(prefab))
            foreach (float scale in scales)
            {
                float worldWidth = WorldStrokeWidth(limb.Spec.width, baked, scale);
                float k = scale / baked;

                foreach (float bend in limb.Angles)
                {
                    Rig rig = BuildRig(limb.Spec.upper, limb.Spec.lower, worldWidth, bend, k);
                    try
                    {
                        Vector3[] up = Read(rig.Upper);
                        Vector3[] lo = Read(rig.Lower);

                        // 관절을 가로지르는 연속 세 점의 외접원 = 그 구간의 곡률 반경(원호이므로 정확).
                        Vector2 a = up[up.Length - 2];
                        Vector2 b = up[up.Length - 1];
                        Vector2 c = ToUpperFrame(lo[1], bend, limb.Spec.upper);
                        float radius = Circumradius(a, b, c) * k;   // 로컬 → 월드(획과 같은 단위로)

                        // 획 폭 W인 선의 **안쪽** 가장자리 반경 = r − W/2. 이것이 0 이하가 되는 순간
                        // 안쪽 윤곽이 자기 자신과 교차해 각진 크리즈(= 잉크 뭉침)가 남는다.
                        // 임계값은 1.0이 아니라 규칙 B의 1/cos(Δφ/2)다(CreaseThreshold 문서).
                        float ratio = radius / (worldWidth * 0.5f);
                        float margin = ratio / CreaseThreshold(bend);
                        if (margin < worstMargin)
                        {
                            worstMargin = margin;
                            worstScale = scale;
                            worstWhere = $"{limb.Name} {bend:F0}도 배율 {scale:F2} " +
                                $"(r={radius:F4}, W/2={worldWidth * 0.5f:F4}, r/ρ={ratio:F3})";
                        }
                    }
                    finally { Object.DestroyImmediate(rig.Root); }
                }
            }

            Debug.Log($"{LogPrefix} 규칙 B 여유(= r/ρ ÷ 임계) 최솟값 = {worstMargin:F3} ({worstWhere}). " +
                $"배율 {scales.Count}종({scales[0]:F2}~{scales[scales.Count - 1]:F2}) 전수. " +
                "1.0 아래로 내려가면 관절 안쪽에 각진 크리즈가 남습니다.");

            Assert.Greater(worstMargin, 1.0f,
                $"{LogPrefix} 곡률 반경이 획 반두께(규칙 B 임계)보다 작습니다({worstWhere}) — " +
                "관절 안쪽 윤곽이 자기 자신과 교차해 '검은 뭉치'가 다시 생깁니다.\n" +
                $"★ 배율 {worstScale:F2}에서 걸렸다면 원인은 화면상 획 하한일 가능성이 큽니다: " +
                $"배율을 내리면 마디만 짧아지고 획은 {StickConfig.MinStrokeScreenPoints}pt에서 멈춥니다. " +
                $"처방은 {nameof(LimbCurveRenderer)}.{nameof(LimbCurveRenderer.FilletLengthRatio)}를 키우거나 " +
                "그 자세의 굽힘 각도(StickConfig)를 낮추는 것입니다. " +
                "획 두께(SceneBootstrapper.LineWidthScale)를 낮추는 것은 낮은 배율에서는 효과가 없습니다 — " +
                "이미 하한에 눌려 있기 때문입니다.");
        }

        // ====================================================================
        // (4-B) ★ 납작한 폴리라인에 <b>길이 0인 선분</b>이 없다 (2026-09-01 회귀)
        // ====================================================================
        //
        // docs/CHARACTER_FORM_SPEC.md 4-5: BuildLimbPolyline이 관절점을 <b>두 번</b> 담아
        // 인덱스 4와 5가 같은 좌표였다. 두꺼운 폴리라인 속의 퇴화 선분은 2026-09-01 "발" 실패와
        // 같은 계열이고(코너 조인이 자기교차), 이 함수는 정보창 초상화가 <b>이미</b> 쓰고 있으며
        // 펫(CharacterPetRenderer)도 이제 같은 함수를 부른다. 그래서 회귀로 잠근다.

        [Test]
        public void 납작한_폴리라인에_길이_0인_선분이_없다()
        {
            GameObject prefab = LoadPrefab();
            float baked = BakedScale(prefab);
            List<float> scales = ScaleSamples(prefab);
            var buffer = new Vector3[LimbCurveRenderer.PolylinePointCount];

            float worstEdgeRatio = float.PositiveInfinity; string worstWhere = "";

            foreach (LimbCase limb in LimbCases(prefab))
            foreach (float scale in scales)
            {
                // 폴리라인 소비자(초상화/펫)는 도형과 획을 <b>같은 프레임</b>에서 만든다 —
                // SolveFilletLength의 단위 계약대로 길이와 획을 같은 단위로 넘긴다.
                float k = scale / baked;
                float width = WorldStrokeWidth(limb.Spec.width, baked, scale);
                float upper = limb.Spec.upper * k;
                float lower = limb.Spec.lower * k;

                foreach (float raw in limb.Angles)
                foreach (float bend in new[] { raw, -raw })
                {
                    int count = LimbCurveRenderer.BuildLimbPolyline(upper, lower, bend, width, buffer);
                    Assert.AreEqual(LimbCurveRenderer.PolylinePointCount, count,
                        $"{LogPrefix} BuildLimbPolyline이 {count}점을 채웠습니다(계약 " +
                        $"{LimbCurveRenderer.PolylinePointCount}점).");

                    // 양 끝점 계약: 첫 점은 뿌리(0,0), 마지막 점은 굽힌 상태의 끝점.
                    Assert.AreEqual(0f, buffer[0].magnitude, 1e-6f,
                        $"{LogPrefix} 폴리라인의 첫 점이 뿌리(0,0)가 아닙니다(굽힘 {bend:F0}도).");

                    for (int i = 1; i < count; i++)
                    {
                        float edge = Vector3.Distance(buffer[i - 1], buffer[i]);
                        Assert.Greater(edge, 0f,
                            $"{LogPrefix} 폴리라인의 선분 {i - 1}→{i}가 길이 0입니다" +
                            $"({limb.Name} {bend:F0}도 배율 {scale:F2}) — 같은 점을 두 번 담고 있습니다. " +
                            $"{nameof(LimbCurveRenderer)}.{nameof(LimbCurveRenderer.PolylineJointIndex)}로 " +
                            "관절점을 한 번만 담아야 합니다.");

                        // 길이 0만 막으면 "1e−7짜리 선분"이 통과한다. 실제 계약은 규칙 B다.
                        Vector3 prev = i >= 2 ? buffer[i - 2] : buffer[i - 1];
                        float turn = i >= 2
                            ? Vector3.Angle(buffer[i - 1] - prev, buffer[i] - buffer[i - 1]) * Mathf.Deg2Rad
                            : 0f;
                        float required = width * Mathf.Tan(0.5f * turn);
                        float ratio = required > 1e-9f ? edge / required : float.PositiveInfinity;
                        if (ratio < worstEdgeRatio)
                        {
                            worstEdgeRatio = ratio;
                            worstWhere = $"{limb.Name} {bend:F0}도 배율 {scale:F2} 선분 {i - 1}→{i} " +
                                $"(길이 {edge:F5}, 필요 {required:F5})";
                        }
                    }

                    Assert.AreEqual(LimbCurveRenderer.PolylineJointIndex,
                        NearestIndexToJoint(buffer, count, upper, bend),
                        $"{LogPrefix} 관절점이 {nameof(LimbCurveRenderer.PolylineJointIndex)}에 있지 않습니다 — " +
                        "초상화/테스트가 무릎을 그 인덱스로 집습니다.");
                }
            }

            Debug.Log($"{LogPrefix} 납작한 폴리라인 최단 선분 여유(규칙 B) = {worstEdgeRatio:F2}배 ({worstWhere}). " +
                $"점 {LimbCurveRenderer.PolylinePointCount}개 / 관절 인덱스 {LimbCurveRenderer.PolylineJointIndex}.");

            Assert.Greater(worstEdgeRatio, 1.0f,
                $"{LogPrefix} 납작한 폴리라인의 선분이 규칙 B를 어깁니다({worstWhere}) — " +
                "두꺼운 폴리라인의 안쪽 오프셋이 자기교차합니다.");
        }

        /// <summary>폴리라인에서 각진 corner(0, −Lu)에 <b>가장 가까운</b> 점의 인덱스. 필렛은 그 corner를
        /// 깎아내므로 관절점이 언제나 그 자리에 가장 가깝다 — 인덱스를 수식으로 다시 적지 않고
        /// <b>결과에서</b> 찾는 방법이다.</summary>
        private static int NearestIndexToJoint(Vector3[] points, int count, float upperLength, float bendDegrees)
        {
            var corner = new Vector2(0f, -upperLength);
            int best = -1; float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                float d = Vector2.Distance(points[i], corner);
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            return best;
        }

        // ====================================================================
        // (4-C) ★ 구워진 프리팹의 <b>모든 획</b>이 전 배율에서 규칙 B를 지킨다
        // ====================================================================
        //
        // (3)(4)는 팔다리의 <b>자세</b>를 훑는다. 이쪽은 반대로 <b>구워진 도형 전부</b>(머리 링/몸통/
        // 팔다리)를 훑어, 누가 새 도형을 프리팹에 추가했을 때 낮은 배율에서 조용히 뭉치는 것을 잡는다.

        /// <summary>
        /// ★ <b>규칙 적용 예외</b>(docs/CHARACTER_FORM_SPEC.md 4-7). 여기 적힌 선은 규칙 B를
        /// <b>정의상</b> 위반하며 그것이 의도다 — 검사에서 반드시 빼야 오탐이 나지 않는다.
        ///
        /// <para><c>HeadFill</c>: 경로반경 r인 원을 폭 2.4r로 그려 안을 채우는 <b>의도적 과채움</b>이다
        /// (Editor/SceneBootstrapper.CreateFilledDisc). W/2 = 1.2r &gt; r 이라 "곡률 반경 ≥ 획 반두께"가
        /// 성립할 수 없다. 규칙은 <b>"획으로 읽혀야 하는 선"</b>에만 적용한다 — 채움 면은 획이 아니다.</para>
        ///
        /// <para><b>왜 안전한가(규칙 B가 막는 사고가 여기서는 안 일어난다).</b> 규칙 B가 막는 것은
        /// <b>보이는 실루엣이 스스로를 파고드는 것</b>이다. 볼록 다각형을 두껍게 그리면 자기교차하는
        /// 쪽은 <b>안쪽 오프셋</b>인데 그건 전부 잉크 <b>내부</b>에 있고, 실루엣을 만드는
        /// <b>바깥 오프셋</b>은 볼록 다각형에서 <b>절대 자기교차하지 않는다</b>. 그래서 결함이
        /// 화면에 나타날 통로가 없다.</para>
        ///
        /// <para>★★ <b>단, 전제가 하나 있다 — 캐릭터 잉크가 완전 불투명(α = 1)일 때만 성립한다.</b>
        /// k = W/r = 2.4이므로 안쪽 가장자리는 r − W/2 = <b>−0.2r</b>, 즉 중심을 0.2r <b>지나친다</b>.
        /// 24각형 근사에서 중심과 각 현의 거리는 r·cos(π/24) = <b>0.99144r</b> &lt; 1.2r 이므로
        /// <b>중심점은 24개 사각형 전부의 내부</b>에 있다. 겹침 수 실측(같은 기하로 계산):
        /// 중심 <b>24겹</b> / 0.2r <b>12겹</b> / 0.5r <b>2~3겹</b>(각도에 따라 진동 = 24갈래 무늬) /
        /// 팔다리 같은 단순 선은 1겹. α = 1이면 24번을 덮어도 같은 색이라 <b>한 겹과 구분 불가</b>다.
        /// 그래서 지금은 안 보인다.</para>
        ///
        /// <para><b>이 전제를 깰 수 있는 것: <c>Core/StickmanAgent</c>의 알파 페이드 TODO</b>
        /// ("TODO(Phase 2 렌더링 레이어): 즉시 on/off 대신 ≤200ms 페이드 아웃/인 연출 추가",
        /// Suspend()/Resume()). 그것이 들어오면 <b>구현 방식에 따라 갈린다</b>:
        /// <list type="bullet">
        ///   <item><b>깨진다</b> — 렌더러/머티리얼마다 색 알파를 낮추는 방식(예: 각 LineRenderer의
        ///     색 a를 낮추거나 <c>StickConfig.ResolveInkColor()</c>의 알파를 태우는 방식).
        ///     겹칠 때마다 다시 합성되므로 실효 알파는 1 − (1 − a)^겹수가 된다.</item>
        ///   <item><b>안 깨진다</b> — 레이어 전체를 한 번에 합성하는 방식(RenderTexture에 α = 1로
        ///     그린 뒤 그 텍스처를 a로 합성, 또는 창 자체의 불투명도를 낮추는 방식).
        ///     이 경우 겹침은 텍스처 안에서 이미 해소돼 있다.</item>
        /// </list></para>
        ///
        /// <para><b>★ 판정 방법(구현이 들어오면 이대로 재라 — 산술로 예측까지 끝나 있다).</b>
        /// 잉크 알파를 <b>a = 0.5</b>로 두고 머리를 확대 캡처해 반경별 실효 알파를 잰다.
        /// <list type="number">
        ///   <item><b>예측(깨지는 경우)</b>: 중심 1 − 0.5^24 = <b>1.000</b>(사실상 불투명) /
        ///     0.2r <b>0.99976</b> / 0.5r <b>0.750 ~ 0.875</b>(24갈래로 진동, 진폭 12.5%p) /
        ///     팔다리 <b>0.500</b>. 즉 <b>머리만 몸보다 2배 진하고</b> 중심에 24갈래 무늬가 뜬다.</item>
        ///   <item><b>예측(안 깨지는 경우)</b>: 머리·몸 전부 <b>0.500</b> 균일. 반경에 따른 변화 없음.</item>
        ///   <item>둘의 차이가 <b>0.25 ~ 0.50</b>이라 MSAA 양자화(0.25px)나 캡처 오차로는 절대 안 묻힌다 —
        ///     육안으로도 갈린다. <b>실제 빌드 캡처로만 판정한다</b>(오프라인 렌더러는 오버드로를
        ///     재현하지 않아 거짓 초록이 난다 — 2026-09-01 발 실패와 같은 함정).</item>
        /// </list>
        /// <b>미확인</b>: 알파 페이드는 아직 구현되지 않았으므로 위 두 갈래 중 어느 쪽인지는
        /// <b>현재 미확인</b>이다. 구현하는 라운드가 이 판정을 함께 수행하고, 깨지는 쪽이면
        /// 이 예외 문구와 <c>CreateFilledDisc</c>의 k를 함께 재검토해야 한다
        /// (k를 2.0 미만으로 낮추면 안쪽 오프셋이 중심을 지나치지 않아 겹침 수가 급감한다 —
        /// 단 그러면 24각형 변 중앙에 구멍이 남는지 따로 검산해야 한다).</para>
        ///
        /// <para>여기에 이름을 <b>추가할 때는 반드시 같은 수준의 근거를 적어라.</b> 근거 없이 이름만
        /// 늘리면 이 배열이 "빨간 테스트를 끄는 스위치"가 된다.</para>
        /// </summary>
        private static readonly string[] StrokeRuleExemptNames = { "HeadFill" };

        [Test]
        public void 구워진_모든_획이_전_배율에서_규칙B를_지킨다_단_머리채움은_예외()
        {
            GameObject prefab = LoadPrefab();
            float baked = BakedScale(prefab);
            List<float> scales = ScaleSamples(prefab);

            float worstMargin = float.PositiveInfinity; string worstWhere = "";
            int checkedLines = 0, exempted = 0;

            foreach (LineRenderer lr in prefab.GetComponentsInChildren<LineRenderer>(true))
            {
                if (lr == null || lr.positionCount < 3) continue;   // 2점 직선은 꺾임이 0이다(Torso)
                if (System.Array.IndexOf(StrokeRuleExemptNames, lr.name) >= 0) { exempted++; continue; }
                checkedLines++;

                Vector3[] local = Read(lr);
                bool loop = lr.loop;
                int vertexCount = local.Length;

                foreach (float scale in scales)
                {
                    float k = scale / baked;
                    float width = WorldStrokeWidth(Mathf.Max(lr.startWidth, lr.endWidth), baked, scale);

                    // 꼭짓점 i에서의 꺾임과 그 양옆 선분. 루프면 끝에서 처음으로 이어진다.
                    int first = loop ? 0 : 1;
                    int last = loop ? vertexCount - 1 : vertexCount - 2;
                    for (int i = first; i <= last; i++)
                    {
                        Vector3 prev = local[(i - 1 + vertexCount) % vertexCount] * k;
                        Vector3 here = local[i] * k;
                        Vector3 next = local[(i + 1) % vertexCount] * k;

                        float turn = Vector3.Angle(here - prev, next - here) * Mathf.Deg2Rad;
                        float required = width * Mathf.Tan(0.5f * turn);
                        if (required <= 1e-9f) continue;   // 꺾임 0 = 제약 없음

                        float shortest = Mathf.Min(Vector3.Distance(prev, here), Vector3.Distance(here, next));
                        float margin = shortest / required;
                        if (margin < worstMargin)
                        {
                            worstMargin = margin;
                            worstWhere = $"{lr.name} 꼭짓점 {i} 배율 {scale:F2} " +
                                $"(꺾임 {turn * Mathf.Rad2Deg:F1}도, 선분 {shortest:F5}, 필요 {required:F5}, W {width:F5})";
                        }
                    }
                }
            }

            Assert.Greater(checkedLines, 0, $"{LogPrefix} 검사한 선이 0개입니다 — 거짓 통과입니다.");
            Assert.AreEqual(StrokeRuleExemptNames.Length, exempted,
                $"{LogPrefix} 예외 목록({string.Join(", ", StrokeRuleExemptNames)})의 선을 " +
                $"{exempted}개만 만났습니다 — 예외 항목이 이름을 바꿨거나 사라졌습니다(대조군 실패).");

            Debug.Log($"{LogPrefix} 구워진 획 {checkedLines}종 × 배율 {scales.Count}종 규칙 B 최소 여유 = " +
                $"{worstMargin:F2}배 ({worstWhere}). 예외 {exempted}종: {string.Join(", ", StrokeRuleExemptNames)}.");

            Assert.Greater(worstMargin, 1.0f,
                $"{LogPrefix} 구워진 획이 규칙 B를 어깁니다({worstWhere}) — 낮은 배율에서 획이 " +
                $"화면 하한({StickConfig.MinStrokeScreenPoints}pt)에 눌려 부풀기 때문일 가능성이 큽니다. " +
                "의도적 과채움이라면 StrokeRuleExemptNames에 근거와 함께 추가하세요.");
        }

        // ====================================================================
        // (5) 비용 — 점 개수가 고정이고, 안 움직인 프레임에는 아무것도 쓰지 않는다
        // ====================================================================

        [Test]
        public void 점_개수가_각도와_무관하게_고정이라_메시_재할당이_없다()
        {
            GameObject prefab = LoadPrefab();
            var spec = ReadLimbSpec(prefab, "LeftLeg");

            foreach (float bend in new[] { 0f, 0.01f, 4f, 55f, 126f, -126f })
            {
                Rig rig = BuildRig(spec.upper, spec.lower, spec.width, bend);
                try
                {
                    Assert.AreEqual(LimbCurveRenderer.PointsPerSegment, rig.Upper.positionCount,
                        $"{LogPrefix} 굽힘 {bend}도에서 위 마디 점 개수가 달라집니다 — " +
                        "positionCount가 매 프레임 바뀌면 LineRenderer가 메시를 다시 할당합니다.");
                    Assert.AreEqual(LimbCurveRenderer.PointsPerSegment, rig.Lower.positionCount,
                        $"{LogPrefix} 굽힘 {bend}도에서 아래 마디 점 개수가 달라집니다.");
                }
                finally { Object.DestroyImmediate(rig.Root); }
            }
        }

        [Test]
        public void 각도가_그대로면_다시_굽지_않는다()
        {
            GameObject prefab = LoadPrefab();
            var spec = ReadLimbSpec(prefab, "LeftLeg");

            Rig rig = BuildRig(spec.upper, spec.lower, spec.width, 55f);
            try
            {
                var curve = rig.Root.GetComponent<LimbCurveRenderer>();
                Assert.AreEqual(4, curve.TrackedLimbCount, $"{LogPrefix} 리그의 팔다리 4개를 다 찾지 못했습니다.");

                // BakeEditorPreview()는 force:true라 8마디를 전부 굽는다.
                Assert.AreEqual(8, curve.LastRebuiltSegmentCount,
                    $"{LogPrefix} 최초 굽기에서 8마디가 아니라 {curve.LastRebuiltSegmentCount}마디를 구웠습니다.");

                // 각도를 그대로 둔 채 한 번 더 — 0마디여야 한다(정지 화면에서 LineRenderer 쓰기 0회).
                curve.RefreshNow();
                Assert.AreEqual(0, curve.LastRebuiltSegmentCount,
                    $"{LogPrefix} 각도가 안 바뀌었는데 {curve.LastRebuiltSegmentCount}마디를 다시 구웠습니다 — " +
                    "24시간 상주 앱에서 정지 중에도 매 프레임 메시를 다시 만든다는 뜻입니다.");

                // 임계값 이상으로 움직이면 다시 굽는다(게이트가 영원히 닫히지 않는지 확인).
                foreach (Transform child in rig.Root.transform)
                {
                    Transform lower = child.Find(child.name + "Lower");
                    if (lower != null) lower.localRotation = Quaternion.Euler(0f, 0f, 55f + LimbCurveRenderer.RebuildEpsilonDegrees * 10f);
                }
                curve.RefreshNow();
                Assert.AreEqual(8, curve.LastRebuiltSegmentCount,
                    $"{LogPrefix} 각도가 바뀌었는데 다시 굽지 않았습니다 — 곡선이 자세를 따라가지 않습니다.");
            }
            finally { Object.DestroyImmediate(rig.Root); }
        }

        // ====================================================================
        // (P) 펫(리틀스틱메이트) 마디 — 본체와 같은 문법, 다른 구조
        // ====================================================================
        //
        // 펫의 팔다리는 **마디가 하나**라 갈아낼 관절이 없다. 그래서 본체의 필렛 대신 마디 전체를
        // 완만한 활로 굽힌다(AppearanceShapeBuilder.Limb). 여기서 잠그는 것은 그 활이
        // **CharacterPetRenderer의 기존 계약을 하나도 건드리지 않았는가**다 — 그쪽 파일은
        // 이 라운드에서 한 줄도 수정하지 않았으므로, 깨진다면 도형 쪽이 계약을 어긴 것이다.

        /// <summary>펫 도형이 쓰는 키(월드 유닛) — 프리팹 실측에서 유도한다(숫자를 베끼지 않는다).</summary>
        private static float PetHeight(GameObject prefab)
        {
            float ownerHeight = 0f;
            foreach (CapsuleCollider2D c in prefab.GetComponents<CapsuleCollider2D>())
            {
                if (c != null && !c.isTrigger) ownerHeight = Mathf.Max(ownerHeight, c.size.y);
            }
            Assert.Greater(ownerHeight, 0f, $"{LogPrefix} 프리팹에서 주인 신장을 못 읽었습니다.");
            return ownerHeight * AppearanceShapeBuilder.MiniScale;
        }

        [Test]
        public void 펫_마디의_양_끝점이_곧은_막대였을_때와_정확히_같다()
        {
            GameObject prefab = LoadPrefab();
            float h = PetHeight(prefab);

            foreach (float facing in new[] { 1f, -1f })
            {
                Vector3[][] parts = AppearanceShapeBuilder.MiniFigure(h, facing);
                Assert.AreEqual(6, parts.Length,
                    $"{LogPrefix} MiniFigure의 도형 개수가 6이 아닙니다 — " +
                    "CharacterPetRenderer.BuildMini이 인덱스 0~5를 이름으로 고정해 쓰고 있습니다.");

                // 인덱스 4/5 = 다리. 뿌리는 엉덩이, 끝점 y는 정확히 발바닥 0이어야 한다
                // (AppearanceShapeBuilder.MiniHipRatio 문서 — 접지/무릎앉아 계산이 여기 얹혀 있다).
                float hipY = h * AppearanceShapeBuilder.MiniHipRatio;
                for (int i = 4; i <= 5; i++)
                {
                    Vector3[] leg = parts[i];
                    Assert.AreEqual(0f, leg[0].x, 1e-5f, $"{LogPrefix} 다리 {i}의 뿌리 x가 0이 아닙니다 — " +
                        "CharacterPetRenderer.MakeLine이 이 점을 스윙 회전축으로 씁니다.");
                    Assert.AreEqual(hipY, leg[0].y, 1e-5f, $"{LogPrefix} 다리 {i}의 뿌리 y가 엉덩이가 아닙니다.");
                    Assert.AreEqual(0f, leg[leg.Length - 1].y, 1e-5f,
                        $"{LogPrefix} 다리 {i}의 끝점 y가 발바닥(0)이 아닙니다 — 펫이 뜨거나 땅에 박힙니다.");
                    Assert.AreEqual(h * AppearanceShapeBuilder.MiniLegTipXRatio,
                        Mathf.Abs(leg[leg.Length - 1].x), 1e-5f,
                        $"{LogPrefix} 다리 {i}의 끝점 x가 바뀌었습니다 — 무릎앉아 내림 거리 유도가 어긋납니다.");
                }

                // 인덱스 2/3 = 팔. 뿌리는 어깨, 끝점은 어깨에서 h*0.30 아래.
                float shoulderY = h * 0.72f;
                for (int i = 2; i <= 3; i++)
                {
                    Vector3[] arm = parts[i];
                    Assert.AreEqual(0f, arm[0].x, 1e-5f, $"{LogPrefix} 팔 {i}의 뿌리 x가 0이 아닙니다.");
                    Assert.AreEqual(shoulderY, arm[0].y, 1e-5f, $"{LogPrefix} 팔 {i}의 뿌리 y가 어깨가 아닙니다.");
                    Assert.AreEqual(shoulderY - h * 0.30f, arm[arm.Length - 1].y, 1e-5f,
                        $"{LogPrefix} 팔 {i}의 끝점 y가 바뀌었습니다 — LimbNeutralDegrees가 이 점으로 " +
                        "마디 기본 각도를 실측합니다.");
                }
            }
        }

        [Test]
        public void 펫_마디의_점_개수가_계약값이고_선분이_획보다_짧지_않다()
        {
            GameObject prefab = LoadPrefab();
            float h = PetHeight(prefab);
            // 출하 배율에서 실제로 그려지는 펫 획 = 화면상 하한(2pt)에 눌린 값이다
            // (CharacterPetRenderer.RenderStroke 문서). 그 하한이 단일 소스다.
            float w = StickConfig.MinStrokeScreenPoints / StickConfig.ReferencePointsPerWorldUnitApprox;

            Vector3[][] parts = AppearanceShapeBuilder.MiniFigure(h, 1f);
            float shortest = float.MaxValue; int shortestLimb = -1;

            for (int i = 2; i <= 5; i++)
            {
                Assert.AreEqual(AppearanceShapeBuilder.MiniLimbPoints, parts[i].Length,
                    $"{LogPrefix} 펫 마디 {i}의 점 개수가 계약값과 다릅니다.");
                for (int k = 1; k < parts[i].Length; k++)
                {
                    float edge = Vector3.Distance(parts[i][k - 1], parts[i][k]);
                    if (edge < shortest) { shortest = edge; shortestLimb = i; }
                }
            }

            Debug.Log($"{LogPrefix} 펫 마디 최단 선분 = {shortest:F4}유닛 = 획({w:F4})의 {shortest / w:F2}배 " +
                $"(마디 {shortestLimb}, 점 {AppearanceShapeBuilder.MiniLimbPoints}개).");

            Assert.GreaterOrEqual(shortest, w,
                $"{LogPrefix} 펫 마디의 가장 짧은 선분({shortest:F4})이 획({w:F4})보다 짧습니다 — " +
                "화면에서 통째로 먹혀 곡선이 아니라 뭉친 점으로 보입니다(37-6 규칙 1). " +
                $"{nameof(AppearanceShapeBuilder.MiniLimbPoints)}를 줄여야 합니다.");
        }

        /// <remarks>
        /// ★ 2026-09-01 — 이 테스트가 재는 <b>활</b>은 이제 화면에 그려지지 않는다.
        /// <c>CharacterPetRenderer.PrepareMiniLimbs</c>가 마디 2~5의 <b>중간 모양</b>을 본체와 같은
        /// 필렛 관절로 덮어쓰기 때문이다(양 끝점은 그대로 쓴다). 그래도 이 테스트를 남기는 이유는
        /// <c>AppearanceShapeBuilder.Limb</c>이 여전히 <b>펫의 양 끝점을 정하는 유일한 자리</b>라서,
        /// 여기가 깨지면 펫의 접지/스윙이 함께 깨지기 때문이다.
        /// <para>실제로 그려지는 관절의 방향은 아래
        /// <see cref="펫_무릎은_주인과_같은_쪽으로_접힌다"/>가 잠근다.</para>
        /// </remarks>
        [Test]
        public void 펫_네_마디가_모두_같은_쪽으로_굽어_O자_X자_다리가_되지_않는다()
        {
            GameObject prefab = LoadPrefab();
            float h = PetHeight(prefab);

            // 볼록 방향은 "현(chord)의 어느 쪽에 중간 점이 있는가" = 2D 외적의 부호로 잰다.
            // ★ 부호의 절대값을 기대값으로 적지 않는다 — 그건 좌표계 규약이라 손으로 적으면 틀린다
            //   (실제로 이 테스트를 처음 쓸 때 반대로 적었고 수치 검산에서 잡혔다).
            //   계약은 두 가지뿐이다: (1) 네 마디가 서로 같은 쪽, (2) facing을 뒤집으면 함께 뒤집힌다.
            float signAtFacingPlus = 0f;

            foreach (float facing in new[] { 1f, -1f })
            {
                Vector3[][] parts = AppearanceShapeBuilder.MiniFigure(h, facing);
                float first = 0f;

                for (int i = 2; i <= 5; i++)
                {
                    Vector3[] p = parts[i];
                    Vector3 chord = p[p.Length - 1] - p[0];
                    Vector3 mid = p[p.Length / 2] - p[0];
                    float cross = chord.x * mid.y - chord.y * mid.x;

                    Assert.AreNotEqual(0f, cross,
                        $"{LogPrefix} 펫 마디 {i}가 굽지 않았습니다(facing {facing:F0}) — 곧은 막대 그대로입니다.");

                    if (i == 2) first = Mathf.Sign(cross);
                    else
                    {
                        Assert.AreEqual(first, Mathf.Sign(cross),
                            $"{LogPrefix} 펫 마디 {i}의 볼록 방향이 마디 2와 반대입니다(facing {facing:F0}) — " +
                            "두 다리가 서로 반대로 휘면 O자/X자 다리가 됩니다" +
                            $"({nameof(AppearanceShapeBuilder.MiniLimbBowRatio)} 문서의 반증).");
                    }
                }

                if (facing > 0f) signAtFacingPlus = first;
                else
                {
                    Assert.AreEqual(-signAtFacingPlus, first,
                        $"{LogPrefix} facing을 뒤집었는데 볼록 방향이 따라 뒤집히지 않았습니다 — " +
                        "펫이 왼쪽을 볼 때 팔다리가 진행 방향의 반대로 휩니다.");
                }
            }
        }

        // ====================================================================
        // (P2) ★ 펫의 무릎/팔꿈치 — 본체와 <b>같은 수식·같은 부호</b>인가 (2026-09-01)
        // ====================================================================
        //
        // docs/CHARACTER_FORM_SPEC.md 3-4-A: 펫과 주인의 시각 언어 차이 중 진짜 결함은
        // "펫에 무릎이 없다" 하나였다. 그래서 CharacterPetRenderer가
        // LimbCurveRenderer.BuildLimbPolylineBetween을 부르게 했다.
        //
        // 여기서 잠그는 것은 세 가지이며, <b>펫 렌더러의 내부 계산을 베끼지 않는다</b>:
        //   (a) 끝점 계약 — 굽혀도 뿌리/끝점이 정확히 그대로인가(펫의 접지가 여기 얹혀 있다)
        //   (b) 부호 규약 — 같은 부호를 주면 주인과 <b>같은 쪽</b>으로 접히는가
        //   (c) 규칙 B   — 배율 최소값에서도 안쪽 윤곽이 자기교차하지 않는가

        /// <summary>펫이 실제로 그리는 획(월드 유닛). 펫 획은 <b>전 배율에서 화면 하한에 눌려 있다</b> —
        /// 비례 두께가 하한을 넘으려면 배율이 1.134 이상이어야 하는데
        /// <see cref="StickConfig.MaxCharacterScale"/>이 그보다 작다(CHARACTER_FORM_SPEC 3-1).
        /// 그래서 하한 하나가 단일 소스다(기존 펫 테스트와 같은 근거).</summary>
        private static float PetStrokeWorld()
            => StickConfig.MinStrokeScreenPoints / StickConfig.ReferencePointsPerWorldUnitApprox;

        [Test]
        public void 펫_마디는_굽혀도_뿌리와_끝점이_한치도_움직이지_않는다()
        {
            GameObject prefab = LoadPrefab();
            float w = PetStrokeWorld();
            var buffer = new Vector3[LimbCurveRenderer.PolylinePointCount];

            // 배율 하단이 가장 위험하다(획은 하한 고정인데 마디만 짧아진다).
            float petHeight = PetHeight(prefab) * (StickConfig.MinCharacterScale / BakedScale(prefab));

            foreach (float facing in new[] { 1f, -1f })
            {
                Vector3[][] parts = AppearanceShapeBuilder.MiniFigure(petHeight, facing);
                for (int i = 2; i <= 5; i++)
                {
                    Vector3 root = parts[i][0];
                    Vector3 tip = parts[i][parts[i].Length - 1];
                    float chord = Vector3.Distance(root, tip);
                    float maxBend = LimbCurveRenderer.MaxSafeBendDegrees(chord * 0.5f, chord * 0.5f, w);

                    for (int step = 0; step <= 10; step++)
                    {
                        float bend = Mathf.Lerp(-maxBend, maxBend, step / 10f);
                        int count = LimbCurveRenderer.BuildLimbPolylineBetween(root, tip, bend, 0.5f, w, buffer);
                        Assert.AreEqual(LimbCurveRenderer.PolylinePointCount, count,
                            $"{LogPrefix} 펫 마디 {i}의 폴리라인 점 개수가 계약값이 아닙니다.");

                        Assert.AreEqual(0f, Vector3.Distance(buffer[0], root), 1e-6f,
                            $"{LogPrefix} 펫 마디 {i}의 뿌리가 굽힘 {bend:F0}도에서 움직였습니다 — " +
                            "CharacterPetRenderer.MakeLine이 이 점을 스윙 회전축으로 씁니다.");
                        Assert.AreEqual(0f, Vector3.Distance(buffer[count - 1], tip), 1e-6f,
                            $"{LogPrefix} 펫 마디 {i}의 끝점이 굽힘 {bend:F0}도에서 움직였습니다 — " +
                            "다리는 이 y가 발바닥(0)이라 접지와 무릎앉아 내림 거리가 통째로 어긋납니다.");
                    }
                }
            }
        }

        [Test]
        public void 펫_무릎은_주인과_같은_쪽으로_접힌다()
        {
            GameObject prefab = LoadPrefab();
            float w = PetStrokeWorld();
            float petHeight = PetHeight(prefab);
            var buffer = new Vector3[LimbCurveRenderer.PolylinePointCount];

            // ── 대조군: 주인이 같은 부호로 접었을 때 관절이 현의 어느 쪽에 있는가.
            //    숫자를 적지 않고 <b>주인 프리팹 규격 + 주인 부호</b>로 실제로 굽혀서 잰다.
            var legSpec = ReadLimbSpec(prefab, "LeftLeg");
            var armSpec = ReadLimbSpec(prefab, "LeftArm");
            float ownerKneeSide = OwnerJointSide(legSpec, StickmanPoseAnimator.KneeBendSign * 40f, buffer);
            float ownerElbowSide = OwnerJointSide(armSpec, StickmanPoseAnimator.ElbowBendSign * 40f, buffer);

            Assert.AreNotEqual(Mathf.Sign(ownerKneeSide), Mathf.Sign(ownerElbowSide),
                $"{LogPrefix} 주인의 무릎과 팔꿈치가 같은 쪽으로 접힙니다 — 대조군이 성립하지 않습니다 " +
                $"({nameof(StickmanPoseAnimator.KneeBendSign)}/{nameof(StickmanPoseAnimator.ElbowBendSign)} 확인).");

            foreach (float facing in new[] { 1f, -1f })
            {
                Vector3[][] parts = AppearanceShapeBuilder.MiniFigure(petHeight, facing);
                for (int i = 2; i <= 5; i++)
                {
                    bool isLeg = i >= 4;
                    float sign = (isLeg ? StickmanPoseAnimator.KneeBendSign
                                        : StickmanPoseAnimator.ElbowBendSign) * facing;

                    Vector3 root = parts[i][0];
                    Vector3 tip = parts[i][parts[i].Length - 1];
                    int count = LimbCurveRenderer.BuildLimbPolylineBetween(root, tip, sign * 40f, 0.5f, w, buffer);
                    Assert.Greater(count, 0, $"{LogPrefix} 펫 마디 {i}를 굽지 못했습니다.");

                    float side = SideOfChord(root, tip, buffer[LimbCurveRenderer.PolylineJointIndex]);
                    // facing을 곱해 "진행 방향 기준"으로 정규화하면 주인의 관측값과 직접 비교된다.
                    float expected = isLeg ? ownerKneeSide : ownerElbowSide;
                    Assert.AreEqual(Mathf.Sign(expected), Mathf.Sign(side * facing),
                        $"{LogPrefix} 펫 마디 {i}({(isLeg ? "무릎" : "팔꿈치")})가 주인과 반대쪽으로 " +
                        $"접힙니다(facing {facing:F0}) — 미니어처가 아니라 관절이 거꾸로 달린 다른 생물이 됩니다. " +
                        $"부호는 {nameof(StickmanPoseAnimator)}의 것을 그대로 써야 합니다.");
                }
            }

            Debug.Log($"{LogPrefix} 펫 관절 부호 대조 통과 — 주인 무릎 {ownerKneeSide:+0.0000;-0.0000}, " +
                $"팔꿈치 {ownerElbowSide:+0.0000;-0.0000}(현 기준 가로 오프셋, + = +x).");
        }

        [Test]
        public void 펫_마디가_안전_상한까지_접혀도_규칙B를_지킨다()
        {
            GameObject prefab = LoadPrefab();
            float w = PetStrokeWorld();
            float baked = BakedScale(prefab);
            var buffer = new Vector3[LimbCurveRenderer.PolylinePointCount];

            float worstMargin = float.PositiveInfinity; string worstWhere = "";

            foreach (float scale in ScaleSamples(prefab))
            {
                float petHeight = PetHeight(prefab) * (scale / baked);
                Vector3[][] parts = AppearanceShapeBuilder.MiniFigure(petHeight, 1f);

                for (int i = 2; i <= 5; i++)
                {
                    Vector3 root = parts[i][0];
                    Vector3 tip = parts[i][parts[i].Length - 1];
                    float chord = Vector3.Distance(root, tip);
                    float maxBend = LimbCurveRenderer.MaxSafeBendDegrees(chord * 0.5f, chord * 0.5f, w);

                    Assert.Greater(maxBend, 0f,
                        $"{LogPrefix} 펫 마디 {i}의 안전 굽힘 상한이 0입니다(배율 {scale:F2}) — " +
                        "그 배율에서는 무릎을 아예 넣을 수 없다는 뜻입니다.");

                    int count = LimbCurveRenderer.BuildLimbPolylineBetween(root, tip, maxBend, 0.5f, w, buffer);
                    Assert.Greater(count, 0, $"{LogPrefix} 펫 마디 {i}를 굽지 못했습니다.");

                    for (int k = 2; k < count; k++)
                    {
                        float turn = Vector3.Angle(buffer[k - 1] - buffer[k - 2], buffer[k] - buffer[k - 1])
                            * Mathf.Deg2Rad;
                        float required = w * Mathf.Tan(0.5f * turn);
                        if (required <= 1e-9f) continue;
                        float shortest = Mathf.Min(Vector3.Distance(buffer[k - 2], buffer[k - 1]),
                                                   Vector3.Distance(buffer[k - 1], buffer[k]));
                        float margin = shortest / required;
                        if (margin < worstMargin)
                        {
                            worstMargin = margin;
                            worstWhere = $"마디 {i} 배율 {scale:F2} 상한 {maxBend:F1}도 꼭짓점 {k - 1}";
                        }
                    }
                }
            }

            Debug.Log($"{LogPrefix} 펫 마디를 안전 상한까지 접었을 때의 규칙 B 여유 = " +
                $"{worstMargin:F3}배 ({worstWhere}). 획 {PetStrokeWorld():F5}유닛(화면 하한 고정).");

            // 상한 그 자체를 검사하므로 여유는 1.0 근처가 정상이다 — 아래로 내려가면 상한 유도가 틀린 것이다.
            Assert.GreaterOrEqual(worstMargin, 0.999f,
                $"{LogPrefix} {nameof(LimbCurveRenderer.MaxSafeBendDegrees)}가 돌려준 상한에서 이미 " +
                $"규칙 B를 어깁니다({worstWhere}) — 상한 유도가 틀렸다는 뜻이고, 펫이 그 각도까지 접으면 " +
                "관절 안쪽에 잉크가 뭉칩니다.");
        }

        /// <summary>주인 마디를 <paramref name="bendDegrees"/>만큼 접었을 때 관절이 현의 어느 쪽에 있는가
        /// (부호 있는 가로 오프셋, + = +x). 숫자를 적지 않고 프로덕션 함수로 직접 재는 대조군이다.</summary>
        private static float OwnerJointSide((float upper, float lower, float width) spec,
            float bendDegrees, Vector3[] buffer)
        {
            int count = LimbCurveRenderer.BuildLimbPolyline(spec.upper, spec.lower, bendDegrees,
                spec.width, buffer);
            Assert.Greater(count, 0, "주인 마디를 굽지 못했습니다(대조군 실패).");
            return SideOfChord(buffer[0], buffer[count - 1], buffer[LimbCurveRenderer.PolylineJointIndex]);
        }

        /// <summary>현(root→tip) 기준 점의 <b>부호 있는</b> 가로 오프셋. 현이 아래를 향할 때 + = +x 쪽.</summary>
        private static float SideOfChord(Vector3 root, Vector3 tip, Vector3 point)
        {
            Vector2 chord = new Vector2(tip.x - root.x, tip.y - root.y);
            float length = chord.magnitude;
            if (length < 1e-9f) return 0f;
            Vector2 v = new Vector2(point.x - root.x, point.y - root.y);
            return (chord.x * v.y - chord.y * v.x) / length;
        }

        // ====================================================================
        // (6) 구워진 프리팹이 실제로 곡선인가 — 소스만 고치고 자산은 안 구운 상태 감지
        // ====================================================================

        [Test]
        public void 구워진_프리팹의_팔다리가_직선_2점이_아니다()
        {
            GameObject prefab = LoadPrefab();
            string[] segments = { "LeftLeg", "RightLeg", "LeftArm", "RightArm" };

            foreach (string name in segments)
            {
                Transform upper = prefab.transform.Find(name);
                Assert.IsNotNull(upper, $"{LogPrefix} 프리팹에 '{name}'이 없습니다.");
                Transform lower = upper.Find(name + "Lower");
                Assert.IsNotNull(lower, $"{LogPrefix} 프리팹에 '{name}Lower'가 없습니다.");

                foreach (Transform t in new[] { upper, lower })
                {
                    var lr = t.GetComponent<LineRenderer>();
                    Assert.IsNotNull(lr, $"{LogPrefix} '{t.name}'에 LineRenderer가 없습니다.");
                    Assert.AreEqual(LimbCurveRenderer.PointsPerSegment, lr.positionCount,
                        $"{LogPrefix} '{t.name}'이 점 {lr.positionCount}개입니다 — 프리팹이 곡선화 이전이거나 " +
                        "2026-09-01에 잠깐 있었던 '발'이 붙은 채로 남아 있습니다" +
                        "(메뉴 StickMate/Rebuild Character Geometry 필요).");
                }
            }

            Assert.IsNotNull(prefab.GetComponent<LimbCurveRenderer>(),
                $"{LogPrefix} 프리팹 루트에 {nameof(LimbCurveRenderer)}가 없습니다 — " +
                "런타임에 자세가 바뀌어도 곡선이 따라가지 않습니다(프리팹의 정지 자세만 곡선).");
        }

        // ====================================================================
        // (7) 발 — <b>넣었다가 같은 날 되돌렸다</b>(2026-09-01)
        // ====================================================================
        //
        // 사용자 지시: "이럴바엔 그냥 다시 다리를 원래대로 돌리는게 맞음. 발을 넣으면서 이상해짐".
        // 그래서 발 관련 단언 4건을 여기서 지웠다.
        //
        // ★ <b>"발이 없다"를 적극적으로 잠그지는 않는다.</b> 다시 넣을 수 있고, 그때 이 자리에
        //   테스트를 다시 놓으면 된다. 재측정값(획 1.0배 두께 / 0.5획 길이)과 지면 침투 처방,
        //   그리고 실패 가설 두 가지는 States/LimbCurveRenderer.cs의 "발" 기록 문단에 보존했다.
        //
        // ⚠ 다시 넣는 사람이 <b>먼저 깔아야 할 테스트</b>: 본체 팔다리의 "최단 선분 ≥ 획 두께"
        //   (이 파일의 펫_마디의_점_개수가_계약값이고_선분이_획보다_짧지_않다 와 같은 규칙).
        //   지난번 발은 길이 0.5획 × 꺾임 90°라 두꺼운 폴리라인의 자기교차 경계에 정확히 걸려 있었고,
        //   본체 팔다리에는 그 규칙을 검사하는 테스트가 없어서 그대로 통과했다.

    }
}
