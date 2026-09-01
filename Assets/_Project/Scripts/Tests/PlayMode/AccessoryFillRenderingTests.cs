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

        [UnityTest]
        public IEnumerator 왕관은_채움이_없다는_사실을_기록한다()
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
            Debug.Log($"{LogPrefix} 왕관 착용 시 채움 면 {fills.Length}개(설계상 0 — HatCoverLocalY=+∞).");
            Assert.AreEqual(0, fills.Length, $"{LogPrefix} 왕관에 채움이 생겼습니다(설계 위반).");

            yield return Capture("fill_crown_none");
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
