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
    /// 2026-08-31 사용자 신고 "캐릭터 회전할때도 모자착용중인데 모자가 없어짐" 조사용.
    ///
    /// 리더 가설(H1): 좌우 반전이 삼각형 <b>와인딩</b>을 뒤집고, Sprites-Default가 backface culling을
    /// 켜고 있어 뒤집힌 쪽에서 채움 메시가 GPU에 의해 버려진다.
    /// 이 파일은 그 가설을 <b>추측이 아니라 실행</b>으로 검증한다 — 반증되면 그 사실도 기록한다.
    /// </summary>
    public sealed class AccessoryFacingFlipFillTests
    {
        private const string LogPrefix = "[FLIPFILL]";
        private const int Cap = 0;      // 천 모자(비대칭 챙 — 방향이 그림에 드러나는 아이템)
        private const int Beanie = 1;
        private const int Fedora = 2;

        private static string OutDir => Path.Combine(Application.dataPath, "..", "Logs", "evidence_20260831_dbg_flip");

        [UnityTearDown]
        public IEnumerator TearDownAll()
        {
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            yield return null;
        }

        // ============================================================================
        // (1) 좌우 반전을 반복해도 채움 면이 항상 유효한가 — 기하/씬 그래프 층
        // ============================================================================

        [UnityTest]
        public IEnumerator 좌우반전을_20회_반복해도_모자_채움이_항상_유효하다()
        {
            yield return LoadSceneAndPinIdle();
            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            RaiseLevelTo(24, agent.Config);
            ClearAll(agent.Config);
            Wear(EquipmentSlot.Head, Cap, agent.Config);
            for (int i = 0; i < 8; i++) yield return null;

            var renderer = Object.FindFirstObjectByType<CharacterAccessoryRenderer>();
            Assert.IsNotNull(renderer, $"{LogPrefix} CharacterAccessoryRenderer가 없습니다.");

            for (int round = 0; round < 20; round++)
            {
                float want = (round % 2 == 0) ? 1f : -1f;
                agent.Blackboard.SetFacingSign(want);
                for (int i = 0; i < 3; i++) yield return null;

                Assert.AreEqual(want, renderer.FacingSign,
                    $"{LogPrefix} round {round}: 렌더러가 방향을 못 따라왔습니다.");

                Transform container = FindChild(renderer.transform, "EquipmentAccessories");
                Assert.IsNotNull(container, $"{LogPrefix} round {round}: 컨테이너가 사라졌습니다.");

                var fills = new List<MeshRenderer>(container.GetComponentsInChildren<MeshRenderer>(true));
                Assert.AreEqual(2, fills.Count,
                    $"{LogPrefix} round {round}(facing {want:+0;-0}): 채움 MeshRenderer가 {fills.Count}개입니다(기대 2 — 관/챙). " +
                    "이 숫자가 0이면 반전 시 채움이 통째로 생성되지 않는 것입니다.");

                foreach (MeshRenderer mr in fills)
                {
                    Assert.IsTrue(mr.enabled, $"{LogPrefix} round {round}: {mr.name}.enabled=false.");
                    Assert.IsTrue(mr.gameObject.activeInHierarchy, $"{LogPrefix} round {round}: {mr.name} 오브젝트 비활성.");
                    Assert.IsNotNull(mr.sharedMaterial, $"{LogPrefix} round {round}: {mr.name} 재질 없음.");

                    Mesh mesh = mr.GetComponent<MeshFilter>().sharedMesh;
                    Assert.IsNotNull(mesh, $"{LogPrefix} round {round}: {mr.name} 메시 없음.");
                    Assert.Greater(mesh.triangles.Length, 0, $"{LogPrefix} round {round}: {mr.name} 삼각형 0개.");

                    // ★ 와인딩 실측 — 리더 가설(H1)의 핵심. 모든 삼각형의 부호 넓이가 양수(CCW)면
                    //   방향이 바뀌어도 와인딩은 뒤집히지 않는다는 뜻이다(= H1 반증).
                    Vector3[] v = mesh.vertices;
                    int[] t = mesh.triangles;
                    int cw = 0, ccw = 0, degenerate = 0;
                    float totalArea = 0f;
                    for (int k = 0; k + 2 < t.Length; k += 3)
                    {
                        Vector3 a = v[t[k]], b = v[t[k + 1]], c = v[t[k + 2]];
                        float s = 0.5f * ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x));
                        totalArea += s;
                        if (s > 1e-9f) ccw++;
                        else if (s < -1e-9f) cw++;
                        else degenerate++;
                    }
                    if (round < 2 || round >= 18)
                    {
                        Debug.Log($"{LogPrefix} round {round} facing={want:+0;-0} {mr.name}: " +
                            $"삼각형 {t.Length / 3}개(CCW {ccw} / CW {cw} / 퇴화 {degenerate}), " +
                            $"부호넓이합={totalArea:F6}, worldBounds={mr.bounds}, alpha={mesh.colors[0].a:F3}");
                    }
                    Assert.AreEqual(0, cw,
                        $"{LogPrefix} round {round} facing={want:+0;-0} {mr.name}: CW 삼각형이 {cw}개 있습니다 — " +
                        "와인딩이 방향에 따라 뒤집힙니다(H1 성립 조건).");
                    Assert.Greater(totalArea, 0f, $"{LogPrefix} round {round}: {mr.name} 면적이 0 이하.");
                }

                // 챙은 비대칭이다 — 채움 무게중심의 x 부호가 방향을 따라와야 한다(도형이 실제로 뒤집혔다는 증거).
                MeshRenderer brim = fills.Find(f => f.name.StartsWith("HatBrim"));
                Assert.IsNotNull(brim, $"{LogPrefix} round {round}: HatBrimFill 없음.");
                float cxLocal = brim.GetComponent<MeshFilter>().sharedMesh.bounds.center.x;
                Assert.AreEqual(want >= 0f, cxLocal > 0f,
                    $"{LogPrefix} round {round}: facing={want:+0;-0}인데 챙 채움 중심 x={cxLocal:F5} — 도형이 안 뒤집혔습니다.");
            }

            Debug.Log($"{LogPrefix} 좌우 반전 20회 — 채움 2개가 매 회 존재/활성/유효(CW 삼각형 0개).");
        }

        // ============================================================================
        // (2) ★ 결정적 실험 — 재질이 양면(Cull Off)인가. H1의 <b>전제 자체</b>를 GPU로 확인한다.
        //     같은 메시의 와인딩만 강제로 뒤집어 두 번 렌더하고 픽셀 수를 비교한다.
        //       · 픽셀 수가 같다  -> 양면(Cull Off) -> H1은 구조적으로 성립 불가.
        //       · 뒤집으면 0     -> 단면(Cull Back) -> H1은 성립 가능(그러면 와인딩 정규화가 유일한 방벽).
        // ============================================================================

        [UnityTest]
        public IEnumerator 채움_재질이_양면인지_와인딩_반전_렌더로_실측한다()
        {
            yield return LoadSceneAndPinIdle();
            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            RaiseLevelTo(24, agent.Config);
            ClearAll(agent.Config);
            Wear(EquipmentSlot.Head, Cap, agent.Config);
            for (int i = 0; i < 8; i++) yield return null;

            var renderer = Object.FindFirstObjectByType<CharacterAccessoryRenderer>();
            Transform container = FindChild(renderer.transform, "EquipmentAccessories");
            MeshRenderer crown = null;
            foreach (var mr in container.GetComponentsInChildren<MeshRenderer>(true))
                if (mr.name.StartsWith("HatCrown")) crown = mr;
            Assert.IsNotNull(crown, $"{LogPrefix} HatCrownFill 없음.");

            Shader sh = crown.sharedMaterial.shader;
            Debug.Log($"{LogPrefix} 채움 재질 = '{crown.sharedMaterial.name}' / 셰이더 = '{sh.name}' " +
                $"(renderQueue={crown.sharedMaterial.renderQueue}, _Cull 프로퍼티 존재={crown.sharedMaterial.HasProperty("_Cull")})");

            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.Log($"{LogPrefix} -nographics 실행 — GPU 픽셀 실측은 건너뜁니다(그래픽 있는 실행에서 확인 필요).");
                yield break;
            }

            Mesh mesh = crown.GetComponent<MeshFilter>().sharedMesh;
            int[] original = mesh.triangles;

            int normal = CountNonBackgroundPixels(agent, "flip_winding_normal");

            // 와인딩만 뒤집는다(정점은 그대로).
            var reversed = new int[original.Length];
            for (int k = 0; k + 2 < original.Length; k += 3)
            {
                reversed[k] = original[k + 2];
                reversed[k + 1] = original[k + 1];
                reversed[k + 2] = original[k];
            }
            mesh.triangles = reversed;
            yield return null;
            int flipped = CountNonBackgroundPixels(agent, "flip_winding_reversed");

            mesh.triangles = original;   // 즉시 원복.
            yield return null;

            Debug.Log($"{LogPrefix} ★ 결정적 실험 — 정상 와인딩 픽셀 {normal}개 / 뒤집은 와인딩 픽셀 {flipped}개 " +
                $"(차이 {Mathf.Abs(normal - flipped)}개). 같으면 양면(Cull Off) = H1 성립 불가.");
            Assert.Greater(normal, 0, $"{LogPrefix} 정상 와인딩에서도 아무것도 안 그려졌습니다 — 측정 자체가 무효입니다.");
            Assert.AreEqual(normal, flipped, normal * 0.02f,
                $"{LogPrefix} 와인딩을 뒤집자 그려지는 픽셀이 {normal} -> {flipped}로 바뀌었습니다 — " +
                "재질이 단면(backface culling ON)입니다.");
        }

        // ============================================================================
        // (3) H2 — 캐릭터가 <b>회전</b>하는 상태(RAGDOLL / 던져져 공중회전)에서 액세서리가 어떻게 되는가.
        //     이것은 설계상 "숨긴다"이므로 실패로 단언하지 않고 <b>사실을 기록</b>한다.
        // ============================================================================

        [UnityTest]
        public IEnumerator 랙돌_회전_중_액세서리_가시성을_기록한다()
        {
            yield return LoadSceneAndPinIdle();
            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            RaiseLevelTo(24, agent.Config);
            ClearAll(agent.Config);
            Wear(EquipmentSlot.Head, Cap, agent.Config);
            for (int i = 0; i < 8; i++) yield return null;

            var renderer = Object.FindFirstObjectByType<CharacterAccessoryRenderer>();
            Transform container = FindChild(renderer.transform, "EquipmentAccessories");
            Assert.IsNotNull(container, $"{LogPrefix} 컨테이너 없음(사전 조건).");
            int fillsBefore = container.GetComponentsInChildren<MeshRenderer>(true).Length;
            Debug.Log($"{LogPrefix} [H2] 랙돌 진입 전 — 컨테이너 활성={container.gameObject.activeInHierarchy}, " +
                $"채움 {fillsBefore}개, 상태={agent.Blackboard.Machine.CurrentStateId}");
            Assert.Greater(fillsBefore, 0, $"{LogPrefix} 사전 조건 실패 — 랙돌 전에 채움이 없습니다.");

            // ★ 페이드(0.18초)는 <b>시간</b> 기준인데 -batchmode는 프레임이 매우 빨라 deltaTime이 작다.
            //   프레임 수로 기다리면 페이드가 끝나지 않는다(첫 시도에서 실제로 이 함정에 빠졌다).
            //   그래서 <b>실시간</b>으로 기다리며 매 프레임 상태/가시성을 기록한다.
            agent.Blackboard.Machine.ChangeState(StickmanStateId.Ragdoll);

            float t0 = Time.realtimeSinceStartup;
            int minVisible = int.MaxValue;
            var seenStates = new HashSet<StickmanStateId>();
            int frames = 0;
            while (Time.realtimeSinceStartup - t0 < 2.0f)
            {
                yield return null;
                frames++;
                seenStates.Add(agent.Blackboard.Machine.CurrentStateId);
                Transform c = FindChild(renderer.transform, "EquipmentAccessories");
                int v = 0;
                if (c != null)
                {
                    foreach (var mr in c.GetComponentsInChildren<MeshRenderer>(true))
                        if (mr.enabled && mr.gameObject.activeInHierarchy) v++;
                }
                if (v < minVisible) minVisible = v;
            }

            container = FindChild(renderer.transform, "EquipmentAccessories");
            bool containerActive = container != null && container.gameObject.activeInHierarchy;
            int visible = 0, total = 0;
            if (container != null)
            {
                foreach (var mr in container.GetComponentsInChildren<MeshRenderer>(true))
                {
                    total++;
                    if (mr.enabled && mr.gameObject.activeInHierarchy) visible++;
                }
            }
            Debug.Log($"{LogPrefix} [H2] RAGDOLL 2s observe ({frames} frames) - states={string.Join("/", seenStates)}, " +
                $"end={agent.Blackboard.Machine.CurrentStateId}, containerActive={containerActive}, " +
                $"fills total={total} visibleAtEnd={visible} minVisibleDuringWindow={minVisible}. " +
                "minVisible==0 means the hat really vanishes while the character tumbles (documented intentional hide).");
        }

        // ============================================================================
        // (4) 상태 전수 스윕 — "어느 상태에서 모자가 사라지는가"를 표로 남긴다.
        //     사용자 신고("회전할 때 모자가 없어짐")의 후보를 추측이 아니라 실행으로 좁힌다.
        // ============================================================================

        [UnityTest]
        public IEnumerator 상태별_모자_가시성_전수_스윕()
        {
            yield return LoadSceneAndPinIdle();
            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            RaiseLevelTo(24, agent.Config);
            ClearAll(agent.Config);
            Wear(EquipmentSlot.Head, Cap, agent.Config);
            for (int i = 0; i < 8; i++) yield return null;

            var renderer = Object.FindFirstObjectByType<CharacterAccessoryRenderer>();
            var ids = new[]
            {
                StickmanStateId.Idle, StickmanStateId.Walk, StickmanStateId.Jump,
                StickmanStateId.Ragdoll, StickmanStateId.Getup, StickmanStateId.ThrowTumble,
            };

            foreach (StickmanStateId id in ids)
            {
                agent.Blackboard.Machine.ChangeState(id);
                float t0 = Time.realtimeSinceStartup;
                int minVisible = int.MaxValue, maxVisible = 0, samples = 0;
                var seen = new HashSet<StickmanStateId>();
                float rootAngleMax = 0f;
                while (Time.realtimeSinceStartup - t0 < 0.8f)
                {
                    yield return null;
                    samples++;
                    seen.Add(agent.Blackboard.Machine.CurrentStateId);
                    rootAngleMax = Mathf.Max(rootAngleMax,
                        Mathf.Abs(Mathf.DeltaAngle(0f, agent.transform.eulerAngles.z)));
                    Transform c = FindChild(renderer.transform, "EquipmentAccessories");
                    int v = 0;
                    if (c != null)
                        foreach (var mr in c.GetComponentsInChildren<MeshRenderer>(true))
                            if (mr.enabled && mr.gameObject.activeInHierarchy) v++;
                    minVisible = Mathf.Min(minVisible, v);
                    maxVisible = Mathf.Max(maxVisible, v);
                }
                Debug.Log($"{LogPrefix} [SWEEP] request={id} seen={string.Join("/", seen)} " +
                    $"samples={samples} hatFillVisible min={minVisible} max={maxVisible} " +
                    $"rootTiltMaxDeg={rootAngleMax:F1}");
            }
        }

        // ============================================================================
        // (5) ★ 사용자 시나리오 재현 — <b>던져서 공중 회전</b>시키는 동안 모자가 어떻게 되는가.
        //     "캐릭터 회전할때 모자가 없어짐"에 가장 가까운 실제 조작이다.
        // ============================================================================

        [UnityTest]
        public IEnumerator 던져서_공중회전하는_동안_모자_가시성을_기록한다()
        {
            yield return LoadSceneAndPinIdle();
            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            RaiseLevelTo(24, agent.Config);
            ClearAll(agent.Config);
            Wear(EquipmentSlot.Head, Cap, agent.Config);
            for (int i = 0; i < 8; i++) yield return null;

            var renderer = Object.FindFirstObjectByType<CharacterAccessoryRenderer>();
            var bb = agent.Blackboard;

            // 실제 던지기와 같은 입력: 위로 크게 던진 속도 + ThrowTumble 진입(DragThrowState가 하는 것).
            float h = bb.CharacterHeightWorld;
            var throwVel = new Vector2(h * 4f, h * 6f);
            bb.LastThrowVelocity = throwVel;
            // 공중에서 시작해야 회전이 실제로 돈다 — 지면에 붙은 채 진입하면 첫 Tick에서 착지 처리된다.
            bb.Body.position = bb.Body.position + new Vector2(0f, h * 6f);
            bb.Body.linearVelocity = throwVel;
            bb.Machine.ChangeState(StickmanStateId.ThrowTumble, isForcedInterrupt: true);

            // 정지 상태에서의 "머리 로컬 좌표계에서 본 모자 위치"를 기준선으로 잡는다.
            Transform head = null;
            for (int i = 0; i < agent.transform.childCount; i++)
                if (agent.transform.GetChild(i).name == "Head") head = agent.transform.GetChild(i);
            Assert.IsNotNull(head, $"{LogPrefix} Head를 못 찾았습니다.");
            MeshRenderer crown = null;
            foreach (var mr in renderer.GetComponentsInChildren<MeshRenderer>(true))
                if (mr.name.StartsWith("HatCrown")) crown = mr;
            Assert.IsNotNull(crown, $"{LogPrefix} HatCrownFill을 못 찾았습니다.");
            float headRadius = agent.GetComponent<StickmanMetrics>().HeadRadius;
            // ★ bounds.center는 <b>축정렬</b> 박스의 중심이라 물체가 회전하면 강체로 붙어 있어도 값이 변한다
            //   (첫 시도에서 이걸로 재다가 27.9%라는 가짜 이탈을 봤다 — 지표가 틀렸던 것이다).
            //   회전 불변으로 재려면 <b>실제 정점 하나</b>를 월드로 올려 머리 로컬로 되돌려야 한다.
            Mesh crownMesh = crown.GetComponent<MeshFilter>().sharedMesh;
            Vector3 probeLocal = crownMesh.vertices[0];
            Vector3 restOffsetInHead = head.InverseTransformPoint(crown.transform.TransformPoint(probeLocal));

            float t0 = Time.realtimeSinceStartup;
            int minVisible = int.MaxValue, maxVisible = 0, tumbleFrames = 0, hiddenWhileTumbling = 0;
            float maxTilt = 0f, maxHatDrift = 0f;
            bool shot = false;
            int driftSamples = 0;
            var seen = new HashSet<StickmanStateId>();
            while (Time.realtimeSinceStartup - t0 < 3.0f)
            {
                yield return null;
                StickmanStateId id = bb.Machine.CurrentStateId;
                seen.Add(id);
                float tilt = Mathf.Abs(Mathf.DeltaAngle(0f, agent.transform.eulerAngles.z));
                maxTilt = Mathf.Max(maxTilt, tilt);

                Transform c = FindChild(renderer.transform, "EquipmentAccessories");
                int v = 0;
                if (c != null)
                    foreach (var mr in c.GetComponentsInChildren<MeshRenderer>(true))
                        if (mr.enabled && mr.gameObject.activeInHierarchy) v++;
                minVisible = Mathf.Min(minVisible, v);
                maxVisible = Mathf.Max(maxVisible, v);

                if (id == StickmanStateId.ThrowTumble || id == StickmanStateId.Ragdoll)
                {
                    tumbleFrames++;
                    if (v == 0) hiddenWhileTumbling++;
                }

                // ★ "보인다"만으로는 부족하다 — 모자가 <b>머리에 붙어</b> 있어야 한다.
                //   회전 중에도 머리 로컬 좌표계에서 본 모자 위치가 정지 상태와 같아야 한다.
                // 육안 증거 — 회전 중간(기울기 60도 넘긴 첫 프레임)의 스냅샷 한 장.
                if (id == StickmanStateId.ThrowTumble && !shot && tilt > 60f
                    && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    shot = true;
                    CountNonBackgroundPixels(agent, "throw_spin_hat_visible", head.position, 3.0f);
                }

                if (id == StickmanStateId.ThrowTumble && crown != null && head != null)
                {
                    Vector3 inHead = head.InverseTransformPoint(crown.transform.TransformPoint(probeLocal));
                    maxHatDrift = Mathf.Max(maxHatDrift, Vector3.Distance(inHead, restOffsetInHead));
                    driftSamples++;
                }
            }

            Debug.Log($"{LogPrefix} [THROW] states={string.Join("/", seen)} maxRootTiltDeg={maxTilt:F1} " +
                $"hatFillVisible min={minVisible} max={maxVisible} " +
                $"tumbleFrames={tumbleFrames} framesWithHatHiddenWhileTumbling={hiddenWhileTumbling} " +
                $"({(tumbleFrames > 0 ? (100f * hiddenWhileTumbling / tumbleFrames) : 0f):F0}%) " +
                $"| hatDrift: samples={driftSamples} maxDriftWorldUnits={maxHatDrift:F5} " +
                $"(headRadius={headRadius:F5}, = {(headRadius > 0f ? maxHatDrift / headRadius : 0f):P1} of head radius)");

            Assert.Greater(tumbleFrames, 0, $"{LogPrefix} ThrowTumble에 한 프레임도 못 들어갔습니다 — 측정 무효.");
            Assert.AreEqual(0, hiddenWhileTumbling,
                $"{LogPrefix} 공중 회전 {tumbleFrames}프레임 중 {hiddenWhileTumbling}프레임에서 모자가 사라졌습니다 " +
                "— 사용자 신고 '캐릭터 회전할때 모자가 없어짐' 재발.");
            Assert.Greater(driftSamples, 0, $"{LogPrefix} 모자 이탈 표본이 0개 — 측정 무효.");
            Assert.Less(maxHatDrift, headRadius * 0.25f,
                $"{LogPrefix} 회전 중 모자가 머리에서 최대 {maxHatDrift:F5}유닛(머리 반경의 " +
                $"{maxHatDrift / headRadius:P0}) 떨어졌습니다 — 숨기지 않으면 '몸에서 분리된 모자'가 보입니다. " +
                "그 경우 ThrowTumble을 숨김 목록에 되돌려야 합니다.");
        }

        // ==================== 유틸 ====================

        /// <summary>캐릭터 머리 주변만 렌더해 배경색이 아닌 픽셀 수를 센다(= 실제로 그려진 잉크의 양).</summary>
        private static int CountNonBackgroundPixels(StickmanAgent agent, string saveAs,
            Vector3? focusOverride = null, float zoomHeadRadii = 1.6f)
        {
            var metrics = agent.GetComponent<StickmanMetrics>();
            float h = metrics != null ? metrics.TotalHeight : 1.7f;
            Camera main = agent.Blackboard != null ? agent.Blackboard.MainCamera : Camera.main;
            if (main == null) return -1;

            // 모자만 프레임에 넣는다 — 몸통 선이 섞이면 채움의 변화가 묻힌다.
            Vector3 focus = focusOverride ?? new Vector3(agent.transform.position.x,
                agent.transform.position.y + metrics.HeadCenterLocalY + metrics.HeadRadius * 1.1f, 0f);

            var go = new GameObject("DbgFlipCam");
            var cam = go.AddComponent<Camera>();
            cam.CopyFrom(main);
            cam.orthographic = true;
            cam.orthographicSize = metrics.HeadRadius * zoomHeadRadii;
            cam.transform.position = new Vector3(focus.x, focus.y, main.transform.position.z);
            cam.clearFlags = CameraClearFlags.SolidColor;
            var bg = new Color(0f, 1f, 0f, 1f);   // 캐릭터/장비에 절대 없는 순수 초록.
            cam.backgroundColor = bg;

            var rt = new RenderTexture(600, 600, 24);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            int count = 0;
            Color32[] px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].g > 200 && px[i].r < 60 && px[i].b < 60) continue;   // 배경
                count++;
            }

            if (!string.IsNullOrEmpty(saveAs))
            {
                Directory.CreateDirectory(OutDir);
                File.WriteAllBytes(Path.Combine(OutDir, saveAs + ".png"), tex.EncodeToPNG());
            }

            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
            return count;
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
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
            agent.Blackboard.IntentSource = new StillIntent();

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

        private static void Wear(EquipmentSlot slot, int index, StickConfig config)
        {
            EquipmentModel.TryWear(slot, index, config);
            Assert.AreEqual(index, EquipmentModel.WornIndex(slot),
                $"{LogPrefix} {slot} {index}번을 걸치지 못했습니다.");
        }

        private static void ClearAll(StickConfig config)
        {
            for (int i = 0; i < EquipmentModel.SlotCount; i++)
                EquipmentModel.TryWear((EquipmentSlot)i, EquipmentModel.NotWorn, config);
        }

        private sealed class StillIntent : IMovementIntentSource
        {
            public float MoveInputX => 0f;
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }
    }
}
