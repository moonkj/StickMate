using System;
using System.Collections.Generic;
using UnityEngine;
using StickMate.States;

namespace StickMate.EditorTools
{
    /// <summary>
    /// ★ 살아 있는 <c>Stickman</c> 인스턴스에서 <b>사실만</b> 잰다 — 판단하지 않는다.
    ///
    /// <para>이 클래스가 존재하는 이유는 하나다: <c>design/character/APP_ICON_SPEC.md</c> §4-2의
    /// 좌표를 <b>코드에 옮겨 적지 않기 위해서</b>다. 사양 §6-3이 그 위험을 직접 적어 뒀다 —
    /// <i>"좌표를 문서에 베껴 적는 순간 언젠가 갈라진다(이 저장소가 반복해서 당한 형태)"</i>.
    /// 여기서 잰 값이 1차 출처이고, 사양의 숫자는 <see cref="StickmanIconGates.CalibrateGeometry"/>의
    /// <b>교정 기준</b>으로만 쓰인다.</para>
    ///
    /// ============================================================================
    /// 잉크 사각형을 어떻게 재는가 — <c>Renderer.bounds</c>를 안 쓴다
    /// ============================================================================
    /// <c>LineRenderer.bounds</c>는 실제로 테셀레이트된 메시의 AABB라 <b>캡 근사(정16각형)</b>만큼
    /// 작게 나오고, 에디터 모드에서 갱신 시점이 보장되지 않는다. 그래서 대신
    /// <b>점 목록 ± 폭/2</b>의 합집합을 직접 계산한다. 이 화풍은 모든 끝이 둥근 캡이라
    /// (<c>numCapVertices = 8</c>, <c>numCornerVertices</c> 동일) 이 식이 <b>이상적인 잉크 경계</b>와
    /// 정확히 같다 — 그리고 사양 §4-2의 값도 같은 이상 모델에서 나왔으므로 <b>같은 자로 재는 것</b>이다.
    ///
    /// <para>실제 래스터는 캡 근사 때문에 최대 0.2% 작다(사양 §9 말미: <c>cos(π/16) = 99.8%</c>).
    /// 그 0.2%는 여백 쪽으로만 작용하므로 f = 0.875 프레이밍을 <b>느슨하게</b> 만들 뿐 물지 않는다.</para>
    ///
    /// ============================================================================
    /// 획 표본점 — G1/G3이 "어디를 재야 하는지"를 여기서 정한다
    /// ============================================================================
    /// 라스터에서 눈먼 리지 탐색을 하지 않는다. 우리는 <b>관절 좌표를 알고 있으므로</b>
    /// 아래팔/몸통/정강이의 중앙점과 방향을 정확히 집어 그 자리의 가로 단면만 잰다.
    /// 이 방식이 §9의 "3×3 국소최대 리지" 방식보다 나은 점은 <b>어느 마디를 쟀는지 확정된다</b>는
    /// 것이다(§9는 상반신을 섞으면 목이 중앙값을 끌어올린다는 함정을 스스로 적어 뒀다).
    /// </summary>
    public sealed class FigureGeometry
    {
        // ---- 프리팹 안의 이름 (SceneBootstrapper가 굽는 계층 그대로) -----------------------
        public const string TorsoName = "Torso";
        public const string HeadName = "Head";
        public const string HeadFillName = "HeadFill";
        public const string HeadRingName = "HeadOutline";
        public const string LeftArmName = "LeftArm";
        public const string RightArmName = "RightArm";
        public const string LeftLegName = "LeftLeg";
        public const string RightLegName = "RightLeg";

        /// <summary>획 하나를 재기 위한 표본 — 마디의 양 끝(루트 로컬)과 방향, 그리고 명목 폭.</summary>
        public struct StrokeProbe
        {
            /// <summary>마디의 시작점(루트 로컬). 팔다리는 <b>관절</b>(팔꿈치/무릎) 쪽이다.</summary>
            public Vector2 Start;

            /// <summary>마디의 끝점(루트 로컬). 팔다리는 <b>끝</b>(손끝/발끝) 쪽이다.</summary>
            public Vector2 End;

            /// <summary>
            /// 측정창의 파라미터 구간 [T0, T1] — <c>Start</c>→<c>End</c> 위를 달린다.
            /// <para>★ 양 끝을 그대로 재면 안 된다. 관절 쪽에는 <b>이웃 마디의 둥근 캡</b>이 얹혀
            /// 가로 단면이 부풀고, 끝 쪽에는 <b>자기 캡</b>이 있어 단면이 줄어든다. 캡슐의 성질상
            /// 마디 <i>안쪽</i>에서는 가로 단면이 정확히 <c>W / |dir.y|</c>로 일정하므로
            /// (경계점의 축 투영이 중심선과 같다), 오염 구간만 잘라내면 나머지는 전부 쓸 수 있다.</para>
            /// <para>관절 쪽 오염 폭은 이웃 캡의 반지름(= 폭/2)이므로, 아래팔(길이 0.37·배율,
            /// 폭 0.078375)에서 그 비율은 약 22%다 ⇒ <c>T0 = 0.35</c>는 그 위에 여유를 둔 값이다.</para>
            /// </summary>
            public float WindowT0, WindowT1;

            /// <summary>마디의 중앙점(루트 로컬 좌표). 표본 행이 하나뿐인 작은 타일에서 쓴다.</summary>
            public Vector2 Point => (Start + End) * 0.5f;

            /// <summary>
            /// 마디 방향(정규화) — <b>프리팹 트랜스폼에서 온 값</b>이다.
            /// <para>★ 이것은 판정용 1차 추정량이 <b>아니다.</b> 사양 §12-2(A)가 못박은 대로 판정에
            /// 쓰는 방향은 <b>이미지에서 뽑는다</b>(<c>StickmanIconGates.MeasureLimbAxis</c>).
            /// 이 값은 그 이미지 추정과 <b>대조</b>하는 데 쓰고, 이미지 추정이 설 수 없을 만큼
            /// 행이 적을 때(작은 타일) <b>되돌아갈 자리</b>다.</para>
            /// <para>이 값도 하드코딩이 아니다 — 프리팹 인스턴스의 실제 관절 좌표에서 나온다.
            /// 아래팔은 팔 벌림 40°에 팔꿈치 굽힘이 더해져 수직에서 약 50°다(사양 §12-2(A)).
            /// <b>40°를 코드에 적으면 그 순간 틀린다.</b></para>
            /// </summary>
            public Vector2 Direction;

            /// <summary>이 마디의 <c>LineRenderer</c> 폭(월드 유닛). 기대값 계산의 근거.</summary>
            public float NominalWidth;

            /// <summary>사람이 읽는 이름("왼쪽 아래팔" 등).</summary>
            public string Label;

            /// <summary>
            /// 가로 단면 폭 → 마디에 수직인 진짜 획 폭으로 바꾸는 계수(<b>프리팹 기하 쪽</b>).
            /// <para>세로에서 θ만큼 기운 띠를 가로선이 자르면 현의 길이는 <c>W / cos θ</c>다.
            /// 따라서 <c>W = 가로단면 × cos θ</c>이고 <c>cos θ = |dir.y|</c>(dir은 정규화).</para>
            /// <para>★ 이 보정이 <b>없으면</b> 사양 §8 G0(양성 대조)이 통과해 버린다 — 24px 전신의
            /// 팔은 진짜 폭 0.92px이지만 기울어 있어 가로 단면은 1.4px대가 되고,
            /// "가로 폭 ≥ 1.0px"라는 문언 그대로의 판정을 <b>합격</b>한다. 게이트가 죽는다.</para>
            /// </summary>
            public float GeometricPerpendicularFactor => Mathf.Abs(Direction.y);
        }

        /// <summary>팔다리 측정창 — 관절 쪽 22% 오염 구간을 잘라내고 끝 캡 직전까지.</summary>
        public const float LimbWindowT0 = 0.35f, LimbWindowT1 = 0.90f;

        /// <summary>몸통 측정창 — 위(목·머리)와 아래(고관절·다리) 양쪽이 오염원이라 <b>대칭</b>으로 자른다.</summary>
        public const float TorsoWindowT0 = 0.35f, TorsoWindowT1 = 0.65f;

        /// <summary>잉크 사각형(루트 로컬). 사양 §4-2.</summary>
        public Rect InkRect;

        /// <summary>고관절 y(루트 로컬). 다리 위 마디의 원점이다.</summary>
        public float HipY;

        /// <summary>어깨 y(루트 로컬). 팔 위 마디의 원점이다.</summary>
        public float ShoulderY;

        /// <summary>다리 획 폭(월드 유닛).</summary>
        public float LegStrokeWidth;

        /// <summary>
        /// 티어 S 크롭 하단 = <c>hipY − W_다리/2</c>. 사양 §4-2의 못:
        /// <i>"정확히 거기가 고관절 둥근 캡의 아래 끝이다. 1픽셀이라도 위에서 자르면 잉크가 직선으로
        /// 잘려 절단면이 생긴다."</i>
        /// </summary>
        public float TierSmallBottomY;

        /// <summary>머리 중심(루트 로컬).</summary>
        public Vector2 HeadCenter;

        /// <summary>
        /// 머리 <b>잉크</b> 바깥 반경 = 링 경로 반경 + 링 폭/2.
        /// <para>★ 분모를 <c>2R</c>로 잡으면 안 된다(사양 §1-2 / <c>SceneBootstrapper.cs</c>의
        /// <c>LineWidthScale</c> 유도 주석 중 <c>CreateFilledDisc</c>의 띠 <c>[R−W/2, R+W/2]</c>를
        /// 드는 문단).
        /// 이 저장소가 실제로 한 번 그렇게 틀려서 "이미 충분하다"는 오판을 냈다.</para>
        /// </summary>
        public float HeadInkRadius;

        /// <summary>머리 잉크 지름. G1의 분모이자 G2의 분모.</summary>
        public float HeadInkDiameter => HeadInkRadius * 2f;

        /// <summary>
        /// <b>잉크 높이</b> ÷ 머리 잉크 지름 — 래스터가 실제로 잴 수 있는 유일한 "머리 개수"다.
        /// G2의 기대값이 이것이다(래스터에는 지면선이 안 찍힌다).
        /// </summary>
        public float ExpectedHeadCount => InkRect.height / HeadInkDiameter;

        /// <summary>
        /// <b>지면(y=0)에서 잉크 꼭대기까지</b> ÷ 머리 잉크 지름.
        ///
        /// <para>★ 사양 §1-2의 <b>4.4847</b>이 실제로 계산한 값이 이것이다(문서의 잉크 사각형
        /// <c>yMax = 1.734390</c>, <c>D = 0.386738</c> ⇒ 4.48466 — 소수 넷째 자리까지 일치).
        /// 그런데 <b>사양 §8 G2의 문언은 "잉크 높이 ÷ 머리 잉크 지름"</b>이라고 적혀 있고 그 값은
        /// <b>4.6063</b>이다. 두 값의 차 <c>0.1216</c>은 정확히 <b>지면 아래로 내려간 발 캡의 절반</b>
        /// (<c>|yMin| = 0.047025</c> ÷ D = 0.121594)이다 — 우연이 아니라 <b>정의가 두 개</b>인 것이다.</para>
        ///
        /// <para>그래서 여기서는 <b>둘 다</b> 낸다. G2(래스터)는 잉크 높이 쪽을 쓰고,
        /// C1(문서 교정)은 문서가 실제로 계산한 이쪽과 대조한다. 하나로 합치면 반드시 한쪽이 틀린다.
        /// ★ 사양 §8 G2 문언 정정은 <c>design-character</c> 소관이다.</para>
        /// </summary>
        public float ExpectedHeadCountAboveGround => InkRect.yMax / HeadInkDiameter;

        /// <summary>아래팔 표본(좌·우 각각). G3은 <b>둘 중 나쁜 쪽</b>으로 판정한다.</summary>
        public readonly List<StrokeProbe> Forearms = new List<StrokeProbe>();

        /// <summary>정강이 표본(좌·우).</summary>
        public readonly List<StrokeProbe> Shins = new List<StrokeProbe>();

        /// <summary>몸통 표본(1개).</summary>
        public StrokeProbe Torso;

        /// <summary>팔/몸통/다리 명목 획(월드 유닛) — 사양 §1-2 표의 0.078375 / 0.086212 / 0.094050.</summary>
        public float ArmStrokeWidth, TorsoStrokeWidth;

        /// <summary>사양 §1-2의 "획 ÷ 머리 잉크 지름 평균 22.29%"에 대응하는 <b>기대</b> 비율.</summary>
        public float ExpectedMeanStrokeRatio =>
            (ArmStrokeWidth + TorsoStrokeWidth + LegStrokeWidth) / 3f / HeadInkDiameter;

        // ====================================================================
        // 측정
        // ====================================================================

        /// <summary>
        /// 인스턴스에서 전부 잰다. <paramref name="stageWorldX"/>를 빼서 <b>루트 로컬</b>로 정규화한다
        /// (촬영장을 20000에 세워도 좌표가 사양과 같은 축에 남는다).
        /// </summary>
        public static FigureGeometry Probe(Transform root, float stageWorldX)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            var g = new FigureGeometry();
            Vector2 origin = new Vector2(stageWorldX, 0f);

            LineRenderer[] lines = root.GetComponentsInChildren<LineRenderer>(true);
            if (lines.Length == 0) throw new InvalidOperationException("LineRenderer가 없다 — 프리팹이 비어 있다.");

            // ---- 잉크 사각형 = 모든 점 ± 폭/2 의 합집합 ------------------------------------
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (LineRenderer lr in lines)
            {
                if (lr == null || lr.positionCount < 1) continue;
                int n = lr.positionCount;
                for (int i = 0; i < n; i++)
                {
                    Vector3 local = lr.GetPosition(i);
                    Vector3 world = lr.useWorldSpace ? local : lr.transform.TransformPoint(local);
                    float half = WidthAt(lr, n <= 1 ? 0f : i / (float)(n - 1)) * 0.5f;
                    float x = world.x - origin.x, y = world.y - origin.y;
                    if (x - half < minX) minX = x - half;
                    if (x + half > maxX) maxX = x + half;
                    if (y - half < minY) minY = y - half;
                    if (y + half > maxY) maxY = y + half;
                }
            }
            g.InkRect = Rect.MinMaxRect(minX, minY, maxX, maxY);

            // ---- 머리 ---------------------------------------------------------------------
            LineRenderer ring = FindLine(root, HeadRingName);
            if (ring == null) throw new InvalidOperationException(HeadRingName + " LineRenderer를 못 찾았다.");
            g.HeadCenter = LocalCentroid(ring, origin);
            g.HeadInkRadius = LocalMaxRadius(ring, origin, g.HeadCenter) + WidthAt(ring, 0f) * 0.5f;

            // ---- 몸통 ---------------------------------------------------------------------
            LineRenderer torso = FindLine(root, TorsoName);
            if (torso == null) throw new InvalidOperationException(TorsoName + " LineRenderer를 못 찾았다.");
            g.TorsoStrokeWidth = WidthAt(torso, 0.5f);
            g.Torso = MidProbe(torso, origin, g.TorsoStrokeWidth, "몸통");

            // ---- 팔다리 -------------------------------------------------------------------
            // 팔다리 하나 = 병합 폴리라인 하나다(LimbCurveRenderer §병합). 아래 마디의 관절 좌표는
            // 아래 Transform 의 localPosition, 끝점은 폴리라인의 마지막 점이다.
            AddLimbProbe(g.Forearms, root, LeftArmName, origin, "왼쪽 아래팔");
            AddLimbProbe(g.Forearms, root, RightArmName, origin, "오른쪽 아래팔");
            AddLimbProbe(g.Shins, root, LeftLegName, origin, "왼쪽 정강이");
            AddLimbProbe(g.Shins, root, RightLegName, origin, "오른쪽 정강이");

            LineRenderer leftLeg = FindLine(root, LeftLegName);
            LineRenderer leftArm = FindLine(root, LeftArmName);
            if (leftLeg == null || leftArm == null)
            {
                throw new InvalidOperationException("팔/다리 LineRenderer를 못 찾았다 — 프리팹 계층이 바뀌었다.");
            }
            g.LegStrokeWidth = WidthAt(leftLeg, 0.5f);
            g.ArmStrokeWidth = WidthAt(leftArm, 0.5f);

            Transform hip = FindChild(root, LeftLegName);
            Transform shoulder = FindChild(root, LeftArmName);
            g.HipY = hip.position.y - origin.y;
            g.ShoulderY = shoulder.position.y - origin.y;

            // ★ 사양 §4-2 — hipY 를 그대로 쓰면 캡이 잘린다. 반드시 다리획/2 만큼 더 내린다.
            g.TierSmallBottomY = g.HipY - g.LegStrokeWidth * 0.5f;

            return g;
        }

        // ====================================================================
        // 보조
        // ====================================================================

        private static void AddLimbProbe(List<StrokeProbe> into, Transform root, string upperName,
            Vector2 origin, string label)
        {
            Transform upper = FindChild(root, upperName);
            if (upper == null) return;
            Transform lower = FindChild(upper, upperName + "Lower");
            var line = upper.GetComponent<LineRenderer>();
            if (lower == null || line == null || line.positionCount < 2) return;

            // 관절(= 아래 마디의 원점)과 끝점을 위 마디 로컬에서 읽어 월드로 올린다.
            Vector3 jointLocal = lower.localPosition;
            Vector3 tipLocal = line.GetPosition(line.positionCount - 1);
            Vector2 joint = (Vector2)upper.TransformPoint(jointLocal) - origin;
            Vector2 tip = (Vector2)upper.TransformPoint(tipLocal) - origin;

            Vector2 dir = tip - joint;
            if (dir.sqrMagnitude < 1e-12f) return;

            into.Add(new StrokeProbe
            {
                Start = joint,
                End = tip,
                WindowT0 = LimbWindowT0,
                WindowT1 = LimbWindowT1,
                Direction = dir.normalized,
                NominalWidth = WidthAt(line, 1f),
                Label = label,
            });
        }

        private static StrokeProbe MidProbe(LineRenderer lr, Vector2 origin, float width, string label)
        {
            int n = lr.positionCount;
            Vector3 a = lr.GetPosition(0);
            Vector3 b = lr.GetPosition(n - 1);
            Vector2 wa = (Vector2)(lr.useWorldSpace ? a : lr.transform.TransformPoint(a)) - origin;
            Vector2 wb = (Vector2)(lr.useWorldSpace ? b : lr.transform.TransformPoint(b)) - origin;
            Vector2 dir = wb - wa;
            return new StrokeProbe
            {
                Start = wa,
                End = wb,
                WindowT0 = TorsoWindowT0,
                WindowT1 = TorsoWindowT1,
                Direction = dir.sqrMagnitude < 1e-12f ? Vector2.up : dir.normalized,
                NominalWidth = width,
                Label = label,
            };
        }

        /// <summary><c>widthCurve × widthMultiplier</c>. 상수 커브면 t와 무관하게 같은 값이 나온다.</summary>
        public static float WidthAt(LineRenderer lr, float t)
        {
            AnimationCurve curve = lr.widthCurve;
            float v = (curve != null && curve.length > 0) ? curve.Evaluate(Mathf.Clamp01(t)) : 1f;
            return Mathf.Abs(v * lr.widthMultiplier);
        }

        private static Vector2 LocalCentroid(LineRenderer lr, Vector2 origin)
        {
            int n = lr.positionCount;
            // 닫힌 루프면 마지막 점이 첫 점과 겹칠 수 있다 — 평균에는 영향이 거의 없으므로 그대로 쓴다.
            double sx = 0, sy = 0;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = lr.GetPosition(i);
                Vector3 w = lr.useWorldSpace ? p : lr.transform.TransformPoint(p);
                sx += w.x - origin.x;
                sy += w.y - origin.y;
            }
            return new Vector2((float)(sx / n), (float)(sy / n));
        }

        private static float LocalMaxRadius(LineRenderer lr, Vector2 origin, Vector2 center)
        {
            float max = 0f;
            for (int i = 0; i < lr.positionCount; i++)
            {
                Vector3 p = lr.GetPosition(i);
                Vector3 w = lr.useWorldSpace ? p : lr.transform.TransformPoint(p);
                float d = Vector2.Distance(new Vector2(w.x - origin.x, w.y - origin.y), center);
                if (d > max) max = d;
            }
            return max;
        }

        /// <summary>루트 아래 어디든 이름으로 찾는다(깊이 우선).</summary>
        public static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChild(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static LineRenderer FindLine(Transform root, string name)
        {
            Transform t = FindChild(root, name);
            return t == null ? null : t.GetComponent<LineRenderer>();
        }

        /// <summary>
        /// 이 프리팹에 <see cref="LimbCurveRenderer"/>가 붙어 있는지 — 없으면 무릎/팔꿈치가
        /// <b>각진 채로</b> 찍힌다(사양 §1 말미가 "필렛 원호를 직선 2분절로 근사했다"고 적은 그 부분이
        /// 실제로 직선이 되어 버린다). 굽기 전에 한 번 확인한다.
        /// </summary>
        public static bool HasLimbCurve(Transform root) =>
            root.GetComponentInChildren<LimbCurveRenderer>(true) != null;
    }
}
