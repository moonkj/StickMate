using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// 2026-08-30 신설된 액세서리 채움(fill) 렌더링의 회귀 잠금 (test-engineer 통합검증 R4가 지적한 m1 —
    /// 이 렌더링 경로는 그때까지 회귀 테스트가 0건이었다. 누가 `filled: true`를 지워도 EditMode/PlayMode
    /// 전체가 초록으로 남고, 사용자만 "모자가 다시 투명해졌다"고 신고하게 되는 구멍이었다.
    ///
    /// 이 프로젝트가 반복해 온 실패("컴파일 통과 ≠ 화면에 실제로 나옴")를 정면으로 겨눈다 — 채움이
    /// 실제로 씬에 존재하고, 머리 링 윗호를 기하학적으로 덮는지를 실행으로 확인한다.
    /// </summary>
    public sealed class AccessoryFillRenderingTests
    {
        private const string LogPrefix = "[FILL]";
        private const int Cap = 0;      // 천 모자
        private const int Curly = 2;    // 곱슬(머리 링 위로 얹히는 것)

        /// <summary>털모자. ★ PlayMode 어셈블리에는 <c>InternalsVisibleTo</c>가 없어
        /// <c>AccessoryShapeBuilder.HeadBeanie</c>를 참조할 수 없다(위 두 상수와 같은 사정).
        /// 번호가 재배치되면 <c>Wear</c>는 <b>여전히 true</b>이므로 착용 단언은 못 잡는다 —
        /// 그래서 이 번호를 쓰는 검사는 <b>도형 이름</b>(<c>BeanieCuff</c>)이 실제로 나왔는지를
        /// 함께 확인한다. 그것이 "이 번호가 아직 털모자다"의 유일한 증거다.</summary>
        private const int Beanie = 1;

        /// <summary>털모자의 <b>접힌 단</b> — 2026-09-02에 채움에서 낱선이 된 도형.
        /// 아래 양성 대조가 "낱선이 실재한다"를 증명하는 데 쓰는 증인이다.</summary>
        private const string BeanieCuffShapeName = "BeanieCuff";

        private static string OutDir => Path.Combine(Application.dataPath, "..", "Logs", "evidence_20260830_te_fill");

        [UnityTearDown]
        public IEnumerator TearDownAll()
        {
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            yield return null;
        }

        // ============================================================================
        // (1) 채움 면이 실제로 생기고, 머리 링 윗호를 덮는가
        // ============================================================================

        [UnityTest]
        public IEnumerator 모자_채움_면이_실제로_생기고_머리_링_윗호를_덮는다()
        {
            yield return LoadSceneAndPinIdle();

            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            StickConfig config = agent.Config;
            RaiseLevelTo(24, config);

            ClearAll(config);
            Wear(EquipmentSlot.Head, Cap, config);
            Wear(EquipmentSlot.Hair, Curly, config);
            for (int i = 0; i < 8; i++) yield return null;

            var renderer = Object.FindFirstObjectByType<CharacterAccessoryRenderer>();
            Assert.IsNotNull(renderer, $"{LogPrefix} 씬에 CharacterAccessoryRenderer가 없습니다.");

            Transform container = FindChild(renderer.transform, "EquipmentAccessories");
            Assert.IsNotNull(container, $"{LogPrefix} EquipmentAccessories 컨테이너가 없습니다 — 재구성이 안 돌았습니다.");

            var fills = new List<MeshRenderer>(container.GetComponentsInChildren<MeshRenderer>(true));
            Debug.Log($"{LogPrefix} 채움 MeshRenderer {fills.Count}개: " + string.Join(", ", fills.ConvertAll(f => f.name)));
            Assert.Greater(fills.Count, 0,
                $"{LogPrefix} 모자를 썼는데 채움 면이 하나도 없습니다 — 채움 렌더링이 화면에 나오지 않습니다.");

            MeshRenderer crown = fills.Find(f => f.name.StartsWith("HatCrown"));
            Assert.IsNotNull(crown, $"{LogPrefix} HatCrownFill이 없습니다.");
            Assert.IsTrue(crown.enabled, $"{LogPrefix} HatCrownFill이 꺼져 있습니다.");
            Assert.IsTrue(crown.gameObject.activeInHierarchy, $"{LogPrefix} HatCrownFill 오브젝트가 비활성입니다.");
            Assert.IsNotNull(crown.sharedMaterial, $"{LogPrefix} HatCrownFill에 재질이 없습니다 — 아무것도 안 그려집니다.");

            Mesh mesh = crown.GetComponent<MeshFilter>().sharedMesh;
            Assert.IsNotNull(mesh, $"{LogPrefix} HatCrownFill에 메시가 없습니다.");
            Assert.Greater(mesh.vertexCount, 2, $"{LogPrefix} 채움 메시 정점이 {mesh.vertexCount}개뿐입니다.");
            Assert.Greater(mesh.triangles.Length, 0, $"{LogPrefix} 채움 메시에 삼각형이 0개입니다.");
            Assert.Greater(mesh.colors.Length, 0, $"{LogPrefix} 채움 메시에 정점 색이 없습니다(색이 안 칠해집니다).");

            // 정렬 — 채움은 자기 윤곽선 바로 아래여야 한다.
            LineRenderer crownLine = null;
            foreach (var lr in container.GetComponentsInChildren<LineRenderer>(true))
                if (lr.name == "HatCrown") crownLine = lr;
            Assert.IsNotNull(crownLine, $"{LogPrefix} HatCrown 윤곽선을 찾지 못했습니다.");
            Assert.AreEqual(crownLine.sortingOrder - 1, crown.sortingOrder,
                $"{LogPrefix} 채움({crown.sortingOrder})이 윤곽선({crownLine.sortingOrder}) 바로 아래가 아닙니다.");

            // ★ 핵심 — 채움이 <b>머리 링 윗호</b>를 실제로 덮는가. 덮지 않으면 "모자가 투명해 보임"이 남는다.
            var metrics = renderer.GetComponent<StickmanMetrics>();
            Assert.IsNotNull(metrics, $"{LogPrefix} StickmanMetrics가 없습니다.");
            float cx = 0f, cy = metrics.HeadCenterLocalY, r = metrics.HeadRadius;

            Vector3[] verts = mesh.vertices;
            int[] tris = mesh.triangles;
            int covered = 0, sampled = 0;
            for (int deg = 40; deg <= 140; deg += 10)
            {
                float a = deg * Mathf.Deg2Rad;
                var p = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
                sampled++;
                if (PointInMesh(p, verts, tris)) covered++;
                else Debug.Log($"{LogPrefix} 머리 링 {deg}도 지점 {p} — 채움 밖(비쳐 보이는 자리).");
            }
            Debug.Log($"{LogPrefix} 머리 링 윗호 표본 {sampled}개 중 채움이 덮은 것 {covered}개. " +
                $"(머리 중심 y={cy:F4}, 반경={r:F4}, 채움 bounds={mesh.bounds})");
            Assert.GreaterOrEqual(covered, sampled - 2,
                $"{LogPrefix} 머리 링 윗호 {sampled}개 중 {covered}개만 채움에 덮였습니다 — " +
                "모자 안쪽으로 머리 선이 그대로 비칩니다(사용자 신고 재발).");

            // 네거티브 컨트롤 — 머리 링 <b>아래쪽</b>(턱 근처)은 덮이면 안 된다(모자가 얼굴까지 먹은 것).
            var chin = new Vector2(cx, cy - r * 0.9f);
            Assert.IsFalse(PointInMesh(chin, verts, tris),
                $"{LogPrefix} 네거티브 컨트롤 실패 — 모자 채움이 턱 근처({chin})까지 덮었습니다. " +
                "이 판정이 '아무 점이나 다 포함'이 아님을 증명하지 못합니다.");

            yield return Capture("fill_cap_curly");
        }

        // ============================================================================
        // (2) 왕관은 의도적으로 안 채운다(리더 보류 확인 항목) — 네거티브 컨트롤
        // ============================================================================

        /// <summary>
        /// ★ 2026-09-01(3차) <b>뜻이 뒤집힌 검사</b>. 옛 이름은 <c>왕관은_채움이_없다는_사실을_기록한다</c>였고
        /// 채움 0개를 단언했다.
        ///
        /// <para>그 단언은 <b>두 가지 다른 사실을 하나로 묶고 있었다</b>: (a) 왕관은 밑이 뚫려 있다,
        /// (b) 그래서 채우지 않는다. (a)는 여전히 참이지만 (b)는 결론이 틀렸다 — 이 앱의 획은
        /// <b>둥근 캡</b>이라 <b>선으로는 끝이 뾰족해질 수 없다</b>(37-6 규칙 6). 채움 없는 지그재그는
        /// 봉우리 끝을 통째로 뭉개고, 그것이 사용자 신고 "장비들 모양이 너무 조잡해"의 왕관 쪽 정체다.
        /// 뾰족해질 수 있는 것은 <b>채운 도형의 꼭짓점</b>뿐이다.</para>
        ///
        /// <para>그래서 왕관은 <b>채운다</b>. 밑이 뚫린 성질은 채움이 아니라
        /// <c>AccessoryShapeBuilder.HatCoverLocalY = +∞</c>가 계속 보장한다 —
        /// 그 값 때문에 머리카락이 한 점도 잘리지 않는다(EditMode
        /// <c>AccessoryShapeCatalogTests.왕관은_머리카락을_한_점도_자르지_않는다</c>).
        /// 즉 (a)와 (b)는 원래 독립이었고, 이제 코드가 그렇게 말한다.</para>
        ///
        /// <para>★★ <b>2026-09-03(스펙 14-1) — 채움 개수를 숫자로 적지 않게 고쳤다.</b>
        /// 이 검사는 <c>Assert.AreEqual(1, fills.Length)</c>로 <b>「채움은 CrownBody 하나뿐」</b>을
        /// 박아 두고 있었다. 그런데 그건 규약이 아니라 <b>그날의 형태</b>였다 — 테(<c>CrownRim</c>)가
        /// 낱선에서 닫힌 채움 띠가 되자 2개가 되어, 이 파일이 <b>고치는 것을 막는 테스트</b>가 됐다
        /// (이 저장소가 베레모 폴백에서 이미 겪은 형태).</para>
        ///
        /// <para>대신 <b>씬 자신에게 묻는다</b>: 프로덕션 렌더러는 <c>shape.Filled</c> <b>하나</b>로
        /// (a) 채움 메시를 만들고 (b) 그 윤곽선에 <see cref="FillOutlineStroke"/> 표식을 붙인다
        /// (<c>CharacterAccessoryRenderer.AddShape</c>). 그러면 <b>{표식 붙은 선} ↔ {채움 메시}</b>는
        /// 이름으로 <b>일대일</b>이어야 하고, 그 대응이 깨지는 것은 실제 결함이다
        /// (선은 얇아졌는데 면이 없다 / 면은 있는데 선이 낱선 두께다).
        /// PlayMode 어셈블리에는 <c>InternalsVisibleTo</c>가 없어 <c>AccessoryShapeBuilder</c>를 읽을 수
        /// 없으므로, 이것이 "몸에서 유도"할 수 있는 <b>유일한 경로</b>다.</para>
        ///
        /// <para>바뀌지 않은 것: <b>CrownBody는 반드시 채워져 있어야 한다</b>. 그것이 이 파일이 처음
        /// 생긴 이유(누가 <c>filled: true</c>를 지워도 전 스위트가 초록이던 구멍)이고,
        /// 위 일대일 대응만으로는 <b>0개 ↔ 0개</b>도 통과하므로 반드시 함께 잠근다.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator 왕관은_채워지되_얹는_물건으로_남는다()
        {
            yield return LoadSceneAndPinIdle();
            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            RaiseLevelTo(24, agent.Config);
            ClearAll(agent.Config);
            Wear(EquipmentSlot.Head, 3, agent.Config);
            for (int i = 0; i < 8; i++) yield return null;

            var renderer = Object.FindFirstObjectByType<CharacterAccessoryRenderer>();
            Transform container = FindChild(renderer.transform, "EquipmentAccessories");
            var fills = container.GetComponentsInChildren<MeshRenderer>(true);

            AssertFillsMatchOutlineMarks(container, "왕관");

            MeshRenderer bodyFill = FindFill(fills, "CrownBody");
            Assert.IsNotNull(bodyFill,
                $"{LogPrefix} 왕관의 채움 중 CrownBody가 없습니다(있는 것: " +
                string.Join(", ", System.Array.ConvertAll(fills, f => f.name)) + "). " +
                "봉우리 끝이 뾰족해질 수 있는 것은 <b>채운 도형의 꼭짓점</b>뿐이라(37-6 규칙 6), " +
                "CrownBody의 채움이 사라지면 왕관이 다시 둥근 캡에 뭉개진 지그재그가 됩니다.");

            Mesh mesh = bodyFill.GetComponent<MeshFilter>().sharedMesh;
            Assert.IsNotNull(mesh, $"{LogPrefix} CrownBodyFill에 메시가 없습니다.");
            Assert.Greater(mesh.triangles.Length, 0, $"{LogPrefix} CrownBodyFill 삼각형이 0개입니다.");

            // ★ "얹는 물건"의 계측 가능한 형태 — 채움이 <b>턱까지 내려오지 않는다</b>.
            //   여기가 무너지면 왕관이 모자가 된 것이고, 그때는 커버선도 함께 유한해져야 한다.
            //   ★ 이름으로 찾은 <b>CrownBody</b>에 대해서만 묻는다 — 예전에는 fills[0]이었고,
            //     채움이 둘이 되는 순간 그 인덱스 가정은 다른 도형을 재게 된다.
            var metrics = renderer.GetComponent<StickmanMetrics>();
            float cy = metrics.HeadCenterLocalY, r = metrics.HeadRadius;
            var chin = new Vector2(0f, cy - r * 0.9f);
            Assert.IsFalse(PointInMesh(chin, mesh.vertices, mesh.triangles),
                $"{LogPrefix} 왕관 채움이 턱 근처({chin})까지 덮었습니다 — 왕관은 씌우는 것이 아니라 " +
                "얹는 것입니다(HatCoverLocalY = +∞와 같은 사실의 그림 버전).");

            yield return Capture("fill_crown_filled");
        }

        /// <summary>
        /// ★★ <b>양성 대조 — 「전부 채움」이 아니라는 것을 씬에서 증명한다.</b>
        ///
        /// <para>위 검사의 핵심 단언(<see cref="AssertFillsMatchOutlineMarks"/>)은 <b>대응</b>을 본다.
        /// 만약 렌더러가 <b>모든</b> 선에 표식을 붙이는 상태가 되면 그 대응은 언제나 성립하고,
        /// 검사는 아무것도 재지 않으면서 초록이 된다. 그래서 <b>표식이 없는 선</b>이 실제로
        /// 존재하는 장비를 하나 걸쳐 본다.</para>
        ///
        /// <para>고른 것은 <b>털모자</b>다. 접힌 단(<c>BeanieCuff</c>)은 2026-09-02에 <b>채움에서
        /// 낱선으로</b> 바뀐 도형이라, 이 저장소에서 "낱선이 실재한다"는 사실의 가장 최근 증인이다
        /// (그 변경의 사유는 <c>AccessoryShapeBuilder</c> 털모자 절에 적혀 있다).</para>
        /// </summary>
        [UnityTest]
        public IEnumerator 양성대조_낱선은_표식도_채움도_없다()
        {
            yield return LoadSceneAndPinIdle();
            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            RaiseLevelTo(24, agent.Config);
            ClearAll(agent.Config);
            Wear(EquipmentSlot.Head, Beanie, agent.Config);
            for (int i = 0; i < 8; i++) yield return null;

            var renderer = Object.FindFirstObjectByType<CharacterAccessoryRenderer>();
            Transform container = FindChild(renderer.transform, "EquipmentAccessories");

            AssertFillsMatchOutlineMarks(container, "털모자");

            var unmarked = new List<string>();
            var marked = new List<string>();
            foreach (LineRenderer lr in container.GetComponentsInChildren<LineRenderer>(true))
            {
                if (FillOutlineStroke.Is(lr)) marked.Add(lr.name);
                else unmarked.Add(lr.name);
            }

            Debug.Log($"{LogPrefix} 털모자 — 표식 있는 선 [{string.Join(", ", marked)}] / " +
                $"표식 없는 선(낱선) [{string.Join(", ", unmarked)}].");

            // 존재/부재를 <b>같은 검사 안</b>에서 맞세운다(CLAUDE.md 부재 단언 규칙).
            Assert.IsNotEmpty(marked,
                $"{LogPrefix} 표식이 붙은 선이 하나도 없습니다 — 털모자에는 채움이 있어야 합니다. " +
                "탐지기가 표식을 못 읽고 있을 수도 있습니다.");
            Assert.IsNotEmpty(unmarked,
                $"{LogPrefix} <b>표식 없는 선이 하나도 없습니다.</b> 그렇다면 위 일대일 대응 검사는 " +
                "'모든 선이 채움'이라는 자명한 상태에서 통과한 것이라 아무것도 증명하지 못합니다. " +
                $"털모자 접힌 단({BeanieCuffShapeName})이 다시 채움이 됐는지 확인하십시오 — " +
                "그 도형이 낱선이라는 것이 2026-09-02 사용자 신고('털모자착용시 거의 머리전체를가림')의 처방입니다.");

            // ★ 번호 사본이 썩지 않았는가 — 이 검사가 실제로 <b>털모자</b>를 보고 있음을 도형 이름으로
            //   못 박는다. 번호가 재배치돼 다른 아이템이 걸쳐졌다면 Wear는 여전히 true라 안 걸린다.
            Assert.Contains(BeanieCuffShapeName, unmarked,
                $"{LogPrefix} 표식 없는 선 중에 '{BeanieCuffShapeName}'이 없습니다(있는 것: " +
                $"[{string.Join(", ", unmarked)}]). HEAD {Beanie}번이 더 이상 털모자가 아니거나, " +
                "접힌 단이 채움으로 되돌아갔습니다. 어느 쪽이든 이 양성 대조는 지금 " +
                "<b>다른 것을 재고 있습니다</b>.");

            Transform strayFill = null;
            foreach (MeshRenderer mr in container.GetComponentsInChildren<MeshRenderer>(true))
            {
                foreach (string name in unmarked)
                {
                    if (mr.name == name + "Fill") strayFill = mr.transform;
                }
            }
            Assert.IsNull(strayFill,
                $"{LogPrefix} 표식 없는 선({(strayFill != null ? strayFill.name : "-")})에 채움 면이 " +
                "붙어 있습니다 — 선 두께는 낱선 하한(2.00pt)인데 면은 그려진 상태입니다. " +
                "두 갈래가 같은 shape.Filled에서 나오지 않고 있습니다.");
        }

        /// <summary>
        /// <b>{채움 메시} ↔ {채움 윤곽선 표식이 붙은 선}</b>이 이름으로 일대일인가.
        /// <para>프로덕션에서 이 둘은 <c>shape.Filled</c> <b>하나</b>에서 갈라져 나온다
        /// (<c>AddFill</c> / <c>AddLine(..., isFillOutline: shape.Filled)</c>). 그래서 이 대응이
        /// 깨졌다는 것은 <b>두 갈래가 서로 다른 근거를 쓰기 시작했다</b>는 뜻이고,
        /// 화면에서는 "면 없는 얇은 선" 또는 "낱선 두께로 그려진 채움 경계"로 나온다.</para>
        /// <para>개수를 숫자로 적지 않으므로, 장비 담당이 어떤 도형을 채움으로 바꾸든
        /// <b>이 검사가 막지 않는다</b> — 막는 것은 <b>어긋남</b>뿐이다.</para>
        /// </summary>
        private static void AssertFillsMatchOutlineMarks(Transform container, string label)
        {
            var markedLines = new List<string>();
            foreach (LineRenderer lr in container.GetComponentsInChildren<LineRenderer>(true))
            {
                if (FillOutlineStroke.Is(lr)) markedLines.Add(lr.name);
            }

            var fillNames = new List<string>();
            foreach (MeshRenderer mr in container.GetComponentsInChildren<MeshRenderer>(true))
            {
                fillNames.Add(mr.name);
            }

            Debug.Log($"{LogPrefix} {label} — 채움 면 {fillNames.Count}개 [{string.Join(", ", fillNames)}] / " +
                $"채움 윤곽선 표식 {markedLines.Count}개 [{string.Join(", ", markedLines)}].");

            Assert.AreEqual(markedLines.Count, fillNames.Count,
                $"{LogPrefix} {label}의 채움 면이 {fillNames.Count}개인데 채움 윤곽선 표식은 " +
                $"{markedLines.Count}개입니다 — 둘 다 shape.Filled 하나에서 나오므로 같아야 합니다.");

            foreach (string line in markedLines)
            {
                Assert.Contains(line + "Fill", fillNames,
                    $"{LogPrefix} {label}: 선 '{line}'은 채움 경계선 표식(두께 1.00pt 하한)을 달고 있는데 " +
                    "같은 이름의 채움 면이 없습니다 — 면 없는 채움 경계선은 그냥 <b>얇은 낱선</b>입니다.");
            }

            foreach (string fill in fillNames)
            {
                Assert.IsTrue(fill.EndsWith("Fill"),
                    $"{LogPrefix} {label}: 채움 면 이름 '{fill}'이 'Fill'로 끝나지 않습니다 — " +
                    "렌더러의 명명 규약(shape.Name + \"Fill\")이 바뀌었다면 이 대조도 함께 고쳐야 합니다.");
                Assert.Contains(fill.Substring(0, fill.Length - "Fill".Length), markedLines,
                    $"{LogPrefix} {label}: 채움 면 '{fill}'에 대응하는 <b>표식 붙은 선</b>이 없습니다 — " +
                    "면은 그려졌는데 경계선은 낱선 하한(2.00pt)으로 그려지고 있다는 뜻이라, " +
                    "그 도형은 자기 윤곽선에 색면을 잃습니다(규칙 1-C).");
            }
        }

        private static MeshRenderer FindFill(MeshRenderer[] fills, string shapeName)
        {
            for (int i = 0; i < fills.Length; i++)
            {
                if (fills[i].name == shapeName + "Fill") return fills[i];
            }
            return null;
        }

        // ============================================================================
        // (3) 망토 채움이 흔들림(sway)을 따라가는가 — 걷는 동안 윤곽선만 움직이면 면이 어긋난다
        // ============================================================================

        [UnityTest]
        public IEnumerator 망토_채움이_흔들리는_윤곽선을_따라가는가()
        {
            yield return LoadSceneAndPinIdle();
            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            RaiseLevelTo(24, agent.Config);
            ClearAll(agent.Config);
            Wear(EquipmentSlot.Shoulders, 0, agent.Config);
            for (int i = 0; i < 8; i++) yield return null;

            var renderer = Object.FindFirstObjectByType<CharacterAccessoryRenderer>();
            Transform container = FindChild(renderer.transform, "EquipmentAccessories");
            MeshRenderer capeFill = null;
            foreach (var mr in container.GetComponentsInChildren<MeshRenderer>(true))
                if (mr.name.StartsWith("CapeOutline")) capeFill = mr;
            Assert.IsNotNull(capeFill, $"{LogPrefix} CapeOutlineFill이 없습니다.");

            LineRenderer capeLine = null;
            foreach (var lr in container.GetComponentsInChildren<LineRenderer>(true))
                if (lr.name == "CapeOutline") capeLine = lr;
            Assert.IsNotNull(capeLine, $"{LogPrefix} CapeOutline 선을 찾지 못했습니다.");

            // 걷게 만든다 — 흔들림은 걷는 속도에 비례한다.
            agent.Blackboard.IntentSource = null;
            var body = agent.Blackboard.Body;
            float walk = agent.Config.ResolveWalkSpeed();
            Mesh mesh = capeFill.GetComponent<MeshFilter>().sharedMesh;
            Vector3 fillBefore = mesh.vertices[4];

            // ★★ 2026-09-01 — 표본 창을 60프레임에서 <b>1.5초(벽시계)</b>로 바꿨다.
            //
            //   흔들림(HemSway)의 위상은 <c>Time.time × 2π / SwayPeriodSeconds</c>이고
            //   <c>SwayPeriodSeconds = 0.62초</c>다(CharacterAccessoryRenderer). 그런데 배치 모드는
            //   0.11~0.45ms/프레임이라 60프레임은 실제로 <b>0.007~0.027초</b> — 한 주기의
            //   <b>1.1%~4.3%</b>다. 즉 위상이 0.04~0.17rad밖에 안 돌아, "걷는 동안 채움이 윤곽선을
            //   따라가는가"를 본다면서 사실상 <b>정지 화면 한 장</b>을 보고 있었다(거짓 통과).
            //   1.5초면 위상이 2.4주기를 돌아 자락이 앞뒤로 여러 번 왕복한다.
            //
            //   함께 넣은 진단값 <c>maxExcursion</c>은 "표본 동안 윤곽선이 실제로 움직이기는 했는가"다 —
            //   이것이 0이면 위 maxGap 상한은 (움직임이 없으니) 언제나 참이라 아무 의미가 없다.
            const float SampleSeconds = 1.5f;
            float maxGap = 0f;
            float maxExcursion = 0f;
            Vector3[] firstLinePts = null;
            yield return TestClock.SampleForSeconds(SampleSeconds, _ =>
            {
                body.linearVelocity = new Vector2(walk, body.linearVelocity.y);
                var linePts = new Vector3[capeLine.positionCount];
                capeLine.GetPositions(linePts);
                Vector3[] fillPts = mesh.vertices;
                for (int k = 2; k <= 6 && k < linePts.Length && k < fillPts.Length; k++)
                {
                    maxGap = Mathf.Max(maxGap, Vector3.Distance(linePts[k], fillPts[k]));
                    if (firstLinePts != null && k < firstLinePts.Length)
                        maxExcursion = Mathf.Max(maxExcursion, Vector3.Distance(linePts[k], firstLinePts[k]));
                }
                firstLinePts ??= linePts;
            });

            float stroke = renderer.StrokeWidth;
            Debug.Log($"{LogPrefix} 걷는 동안({SampleSeconds:F1}초 = sway {SampleSeconds / 0.62f:F1}주기) " +
                $"망토 윤곽선과 채움 면의 최대 어긋남 = {maxGap:F5} 월드유닛 " +
                $"(획 두께 {stroke:F5}, 획의 {maxGap / stroke:P0}). 채움 첫 표본 {fillBefore} -> {mesh.vertices[4]}. " +
                $"[네거티브 진단] 표본 동안 윤곽선이 실제로 움직인 최대 거리 = {maxExcursion:F5} 월드유닛 " +
                $"(0이면 흔들림이 아예 안 돌았다는 뜻이라 위 상한은 무의미합니다).");
            // ★ 2026-09-01 네거티브 컨트롤 — 표본 창이 흔들림을 <b>실제로</b> 봤는가.
            //   이 단언이 없으면 "채움이 선을 따라간다"는 아래 상한은 <b>선이 안 움직이기만 해도</b>
            //   통과한다(60프레임 예산이 sway 한 주기의 1.1%였을 때가 정확히 그 상태였다).
            //   실측 0.02128유닛이 나오므로 문턱 0.005는 4배 여유다.
            Assert.Greater(maxExcursion, 0.005f,
                $"{LogPrefix} 표본 {SampleSeconds:F1}초 동안 망토 윤곽선이 {maxExcursion:F5}유닛밖에 " +
                "움직이지 않았습니다 — 흔들림(HemSway)이 돌지 않았다는 뜻이고, 그러면 아래 " +
                "'채움이 선을 따라간다' 상한은 아무것도 검증하지 못합니다(표본 창이 다시 " +
                "sway 주기 0.62초보다 짧아졌는지, 보행 속도 주입이 먹히는지 확인하세요).");

            // ★ 2026-08-30 통합검증 m4 — 그때는 획 두께의 60% 미만이었다("구조적으로 망토 채움이 sway를
            // 따라가지 않는 공백이 있다 — _swayLines가 LineRenderer만 갱신").
            // ★ 2026-09-01 — 그 공백은 메워졌다(TickHemMotion이 선과 채움을 같은 버퍼로 함께 갱신).
            //   표본 창을 프레임 → 시간으로 고친 뒤 실측 어긋남은 <b>0.00000유닛(획의 0%)</b>이다.
            //   상한은 그대로 둔다 — 회귀가 생기면 이 값이 다시 올라오는 것이 조기 경보다.
            Assert.Less(maxGap, stroke * 0.7f,
                $"{LogPrefix} 흔들리는 동안 채움 면이 윤곽선에서 최대 {maxGap:F5}(획 두께의 " +
                $"{maxGap / stroke:P0}) 어긋납니다 — 걸을 때 면이 선 밖으로 삐져나옵니다.");
        }

        // ============================================================================
        // (4) 이미 배포된 v5 저장 파일(wornFace 키 포함)이 그대로 읽히는가 — FACE 삭제 마이그레이션
        // ============================================================================

        [Test]
        public void 옛_v5_저장파일의_wornFace_키가_있어도_나머지가_보존된다()
        {
            string path = CharacterSaveStore.FilePath;
            string backup = File.Exists(path) ? File.ReadAllText(path) : null;
            try
            {
                string json =
                    "{\n" +
                    "    \"version\": 5,\n" +
                    "    \"level\": 9,\n" +
                    "    \"currentXp\": 1.0,\n" +
                    "    \"totalXpEarned\": 3000.0,\n" +
                    "    \"characterName\": \"표정삭제전\",\n" +
                    "    \"wornHead\": \"equip.head.fedora\",\n" +
                    "    \"wornEyes\": \"\",\n" +
                    "    \"wornNeck\": \"equip.neck.bowtie\",\n" +
                    "    \"wornShoulders\": \"\",\n" +
                    "    \"wornFace\": \"look.face.smile\",\n" +
                    "    \"wornHair\": \"look.hair.curly\",\n" +
                    "    \"wornFx\": \"\",\n" +
                    "    \"wornPet\": \"\"\n" +
                    "}";
                File.WriteAllText(path, json);
                EquipmentModel.ResetForTesting();
                CharacterProgressionModel.ResetForTesting();
                CharacterSaveStore.Load();

                Assert.IsTrue(CharacterSaveStore.LoadedFromFile,
                    "wornFace 키 하나 때문에 저장 파일 전체가 버려졌습니다.");
                Assert.AreEqual(9, CharacterProgressionModel.Level, "레벨이 함께 날아갔습니다.");
                Assert.AreEqual("표정삭제전", CharacterProgressionModel.CharacterName, "이름이 날아갔습니다.");
                Assert.AreEqual(2, EquipmentModel.WornIndex(EquipmentSlot.Head), "중절모가 벗겨졌습니다.");
                Assert.AreEqual(0, EquipmentModel.WornIndex(EquipmentSlot.Neck), "나비넥타이가 벗겨졌습니다.");
                Assert.AreEqual(2, EquipmentModel.WornIndex(EquipmentSlot.Hair), "곱슬이 벗겨졌습니다.");
                Debug.Log($"{LogPrefix} v5(wornFace 포함) 저장 파일 로드 OK — 다른 값 전부 보존.");
            }
            finally
            {
                if (backup != null) File.WriteAllText(path, backup);
                else if (File.Exists(path)) File.Delete(path);
            }
        }

        // ==================== 유틸 ====================

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private static bool PointInMesh(Vector2 p, Vector3[] verts, int[] tris)
        {
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
                float d1 = Side(p, a, b), d2 = Side(p, b, c), d3 = Side(p, c, a);
                bool neg = d1 < 0f, pos = d1 > 0f;
                neg |= d2 < 0f; pos |= d2 > 0f;
                neg |= d3 < 0f; pos |= d3 > 0f;
                if (!(neg && pos)) return true;
            }
            return false;
        }

        private static float Side(Vector2 p, Vector3 a, Vector3 b)
            => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);

        /// <summary>
        /// 캐릭터 주변만 크게 렌더해 PNG로 남긴다(사람이 눈으로 확인하기 위한 증거 이미지).
        ///
        /// ★ 2026-08-30 (윈도우 지원 라운드에서 발견) — `-nographics` 가드가 반드시 필요하다.
        /// 원래 주석은 "배치 실행에서도 동작하는 RT 경로"라고 적혀 있었지만 **실측으로 반증됐다**:
        /// `-batchmode -nographics`에는 GPU 디바이스가 아예 없어(SystemInfo.graphicsDeviceType == Null)
        /// 아래 `cam.Render()`가 네이티브에서 SIGSEGV로 **프로세스를 통째로 죽인다**
        /// (스택: GfxDevice::DrawSharedGeometryJobs ← DrawUtil::DrawLineOrTrailMultiple... ←
        /// Camera::Render ← 이 메서드). 그러면 PlayMode 전체 스위트가 EXIT=139로 중단되어, 이 뒤에
        /// 실행돼야 할 테스트 수십 개가 **아예 돌지 않은 채** 결과 XML만 부분적으로 남는다
        /// (실측: 253개 중 235/237개까지만 기록되고 종료). Tasklist.md에 이미 같은 계열의 사고가
        /// "오프스크린 카메라는 -batchmode -nographics에서 프로세스를 죽인다"로 기록돼 있다.
        ///
        /// 이 가드는 **검증을 약화시키지 않는다** — 이 메서드가 만드는 것은 사람이 볼 PNG 증거일 뿐이고,
        /// 실제 합격/불합격 판정(채움 메시가 존재하는가 / 머리 링 윗호를 덮는가 / 턱은 안 덮는가)은
        /// 전부 호출부의 Assert가 메시 정점 기하로 수행한다. 그래픽이 있는 실행(에디터 Test Runner,
        /// -batchmode without -nographics)에서는 예전과 똑같이 PNG가 저장된다.
        /// </summary>
        private static IEnumerator Capture(string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.Log($"{LogPrefix} 그래픽 디바이스가 없는 실행(-nographics)이라 증거 PNG 캡처를 " +
                    $"건너뜁니다 — 판정은 메시 기하 Assert가 이미 끝냈습니다(name={name}).");
                yield break;
            }

            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            Camera main = agent != null && agent.Blackboard != null ? agent.Blackboard.MainCamera : Camera.main;
            if (main == null || agent == null) yield break;

            var metrics = agent.GetComponent<StickmanMetrics>();
            float h = metrics != null ? metrics.TotalHeight : 1.7f;
            Vector3 focus = agent.transform.position + new Vector3(0f, h * 0.85f, 0f);

            var go = new GameObject("TeCaptureCam");
            var cam = go.AddComponent<Camera>();
            cam.CopyFrom(main);
            cam.orthographic = true;
            cam.orthographicSize = h * 0.55f;
            cam.transform.position = new Vector3(focus.x, focus.y, main.transform.position.z);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.18f, 0.20f, 0.24f, 1f);

            var rt = new RenderTexture(900, 900, 24);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            Directory.CreateDirectory(OutDir);
            string file = Path.Combine(OutDir, name + ".png");
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Debug.Log($"{LogPrefix} 캡처 저장 — {file}");

            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
            yield return null;
        }

        private static void RaiseLevelTo(int level, StickConfig config)
        {
            for (int guard = 0; guard < 4096 && CharacterProgressionModel.Level < level; guard++)
                CharacterProgressionModel.AddXp(CharacterProgressionModel.XpToNextLevel(config) + 1f, config);
            Assert.GreaterOrEqual(CharacterProgressionModel.Level, level, "레벨을 못 올렸습니다.");
        }

        private IEnumerator LoadSceneAndPinIdle()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(agent, $"{LogPrefix} StickmanAgent가 없습니다.");
            Assert.IsNotNull(agent.Blackboard, $"{LogPrefix} 블랙보드가 없습니다.");
            agent.Blackboard.IntentSource = new StillIntentSource();

            float deadline = Time.realtimeSinceStartup + 15f;
            float idleSince = -1f;
            StickmanStateId last = agent.Blackboard.Machine.CurrentStateId;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                last = agent.Blackboard.Machine.CurrentStateId;
                if (last != StickmanStateId.Idle) { idleSince = -1f; continue; }
                if (idleSince < 0f) idleSince = Time.realtimeSinceStartup;
                if (Time.realtimeSinceStartup - idleSince >= 0.5f) break;
            }
            Assert.AreEqual(StickmanStateId.Idle, last, $"{LogPrefix} Idle로 안정되지 않았습니다.");
        }

        /// <summary>★ 개발 기기의 실제 저장 파일이 이미 그 아이템을 걸치고 있으면 TryWear가 false(변화 없음)를
        /// 돌려준다 — 준비 조건을 "TryWear == true"로 두면 기기 상태 때문에 테스트가 죽는다. 그래서
        /// <b>결과 상태</b>로 단언한다.</summary>
        private static void Wear(EquipmentSlot slot, int index, StickConfig config)
        {
            EquipmentModel.TryWear(slot, index, config);
            Assert.AreEqual(index, EquipmentModel.WornIndex(slot),
                $"{LogPrefix} {slot} {index}번을 걸치지 못했습니다(지금 {EquipmentModel.WornIndex(slot)}번).");
        }

        /// <summary>실제 저장 파일의 차림이 관측을 오염시키지 않게 전 카테고리를 벗긴다.</summary>
        private static void ClearAll(StickConfig config)
        {
            for (int i = 0; i < EquipmentModel.SlotCount; i++)
                EquipmentModel.TryWear((EquipmentSlot)i, EquipmentModel.NotWorn, config);
        }

        private sealed class StillIntentSource : IMovementIntentSource
        {
            public float MoveInputX => 0f;
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }
    }
}
