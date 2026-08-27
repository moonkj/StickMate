using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.EditorTools
{
    /// <summary>
    /// Phase 0~6이 순수 C# 게임로직만 구현하고 의도적으로 남겨둔 씬/프리팹 배선(README.md "빌드/실행
    /// 방법" 3번 항목)을 채우는 에디터 빌더. 실제 아트 에셋이 아직 없으므로 코드로 생성한 단색
    /// 사각형/원 스프라이트만으로 "졸라맨을 연상시키는 최소 구성"을 만든다.
    ///
    /// 사용법:
    /// - 에디터: 메뉴 StickMate/Build All (Config + Prefab + Scene).
    /// - 배치 모드(재생성/CI용): Unity -batchmode -nographics -projectPath <repo>
    ///   -executeMethod StickMate.EditorTools.SceneBootstrapper.BuildAll -quit -logFile <path>
    ///
    /// 좌표계 참고(BuildMainScene 배치 근거): Platform/NullPlatformWindowService.cs의 더미 발판은
    /// OS 좌상단 원점 기준 y=[0,40] 구간(화면 최상단 40px 밴드)에 고정되어 있고, Platform/
    /// ScreenCoordinateConverter.cs를 거치면 이는 항상 "카메라 뷰포트의 최상단 가장자리" 월드 Y로
    /// 환산된다(카메라 위치/orthographicSize와 무관하게 top = cam.y + orthographicSize로 고정되는
    /// 수학적 귀결 — 임의로 바꿀 수 없다). 즉 이 더미 폴백에서는 캐릭터가 "화면 상단에 걸린 작업표시줄"
    /// 위에 서게 되며, 발(=Root Transform 피벗, StickmanBlackboard.SenseGround가 Body.position을
    /// 그대로 발 위치로 쓰기 때문)이 뷰포트 맨 위 가장자리에 닿는 순간 몸통 대부분이 시야 밖으로
    /// 벗어난다. 이는 NullPlatformWindowService(Phase 1, 이미 테스트로 검증된 기존 코드)의 기존
    /// 특성이며 이번 배선 작업의 범위 밖이라 수정하지 않는다 — 플레이테스트는 화면 렌더링이 아니라
    /// transform.position 실측 로그로 검증하므로 이 시각적 프레이밍 이슈와 무관하게 유효하다.
    /// </summary>
    public static class SceneBootstrapper
    {
        private const string DataFolder = "Assets/_Project/Data";
        private const string SpritesFolder = DataFolder + "/Sprites";
        private const string PrefabFolder = "Assets/_Project/Prefabs";
        private const string SceneFolder = "Assets/_Project/Scenes";

        private const string ConfigAssetPath = DataFolder + "/DefaultStickConfig.asset";
        private const string PrefabAssetPath = PrefabFolder + "/Stickman.prefab";
        private const string SceneAssetPath = SceneFolder + "/Main.unity";

        private const int SpriteTextureSize = 64; // PPU와 동일하게 잡아 스프라이트 1장 = 세계 단위 1x1가 되게 함.

        [MenuItem("StickMate/Build All (Config + Prefab + Scene)")]
        public static void BuildAll()
        {
            StickConfig config = CreateOrLoadConfig();
            GameObject prefab = BuildStickmanPrefab(config);
            BuildMainScene(prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SceneBootstrapper] BuildAll 완료 — " + ConfigAssetPath + ", " + PrefabAssetPath + ", " + SceneAssetPath);
        }

        [MenuItem("StickMate/Build Default StickConfig")]
        public static StickConfig CreateOrLoadConfig()
        {
            EnsureFolder(DataFolder);
            var existing = AssetDatabase.LoadAssetAtPath<StickConfig>(ConfigAssetPath);
            StickConfig config = existing != null ? existing : ScriptableObject.CreateInstance<StickConfig>();

            // 실측 튜닝(플레이테스트 R1 결과, Tasklist.md "씬/프리팹 배선" 절 참고): 기본값 6px는
            // Screen=640x480 헤드리스 배치 환경에서 세계단위로 환산하면 약 0.125유닛 폭의 매우 좁은
            // 밴드가 되는데, 그 프레임의 실제 프레임타임이 충분히 크면(관찰상 이 환경에서 실제로 발생)
            // 캐릭터가 단 1유닛 낙하만으로도 그 밴드를 한 프레임에 통과("접지 감지 터널링")해 무한
            // 낙하하는 것을 배치 모드 PlayMode 스모크 테스트로 실측 확인했다(States/GroundSensor.cs
            // 로직 자체는 무수정 — 여기서는 StickConfig.cs가 "추후 물리 튜닝으로 교체될 임시값"이라고
            // 명시한 데이터 값만 조정한다). 20px로 넉넉히 키워 이 배치 환경 기준 약 0.3~0.4유닛 밴드를
            // 확보한다.
            config.groundSnapTolerance = 20f;

            if (existing == null)
            {
                AssetDatabase.CreateAsset(config, ConfigAssetPath);
            }
            else
            {
                EditorUtility.SetDirty(config);
            }
            AssetDatabase.SaveAssets();
            return config;
        }

        [MenuItem("StickMate/Build Stickman Prefab")]
        public static GameObject BuildStickmanPrefabMenuItem()
        {
            GameObject prefab = BuildStickmanPrefab(CreateOrLoadConfig());
            AssetDatabase.SaveAssets();
            return prefab;
        }

        /// <summary>
        /// 졸라맨 프리팹 생성. 루트(Rigidbody2D+CapsuleCollider2D+StickmanClickHitbox+StickmanAgent)는
        /// StickmanBlackboard.SenseGround()가 Body.position을 그대로 "발" 위치로 취급하므로 로컬 y=0을
        /// 발 높이로 둔다. 팔다리는 States/RagdollRig.cs가 GetComponentsInChildren&lt;Rigidbody2D/
        /// HingeJoint2D&gt;(true)로 순회할 수 있도록 각자 독립된 Rigidbody2D+HingeJoint2D(root에 연결)를
        /// 갖는다 — 콜라이더는 없음(의도: 몸통/팔다리끼리 겹치는 콜라이더가 상시 물리 시뮬레이션 중
        /// 서로 충돌 판정을 일으켜 걷는 동안 떨림/폭주를 유발하는 것을 원천 차단, 머리의 작은
        /// CircleCollider2D만 루트의 compound collider로 합쳐진다).
        /// </summary>
        public static GameObject BuildStickmanPrefab(StickConfig config)
        {
            EnsureFolder(PrefabFolder);

            Sprite rectSprite = GetOrCreateSprite(SpritesFolder + "/RectSprite.asset", isCircle: false);
            Sprite circleSprite = GetOrCreateSprite(SpritesFolder + "/CircleSprite.asset", isCircle: true);
            Color outline = config != null ? config.primaryOutlineColor : Color.black;
            float gravityScale = config != null ? config.gravityScale : 3f;

            var root = new GameObject("Stickman");

            var rb = root.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = gravityScale;
            rb.mass = 1f;

            var capsule = root.AddComponent<CapsuleCollider2D>();
            capsule.direction = CapsuleDirection2D.Vertical;
            capsule.size = new Vector2(0.4f, 1.8f);
            capsule.offset = new Vector2(0f, 0.9f);

            root.AddComponent<StickmanClickHitbox>();

            var agent = root.AddComponent<StickmanAgent>();
            var so = new SerializedObject(agent);
            so.FindProperty("_config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 몸통 — 시각 전용(물리 없음, root 자식 Transform으로 그대로 따라다님).
            CreateStaticVisual(root.transform, "Torso", rectSprite, new Vector3(0f, 1.0f, 0f), new Vector2(0.16f, 0.8f), outline, sortingOrder: 1);

            // 머리 — 시각 + 작은 CircleCollider2D(루트 Rigidbody2D의 compound collider로 자동 합산됨).
            var head = CreateStaticVisual(root.transform, "Head", circleSprite, new Vector3(0f, 1.6f, 0f), new Vector2(0.4f, 0.4f), outline, sortingOrder: 3);
            var headCollider = head.AddComponent<CircleCollider2D>();
            headCollider.radius = 0.4f; // 시각 크기(반경 0.5 상당)보다 작게 잡아 "작은" 콜라이더로.

            // 팔다리 — Rigidbody2D + HingeJoint2D(connectedBody=root). 조인트 anchor 계산이 스케일에
            // 영향받지 않도록 물리 오브젝트 자체는 scale=1로 유지하고, 스프라이트는 별도 자식(Visual)에서만 스케일.
            const float hipY = 0.6f, shoulderY = 1.3f;
            const float legHalfLength = 0.3f, armHalfLength = 0.25f;

            CreateLimb(root.transform, rb, "LeftLeg", rectSprite, new Vector2(0.12f, 0.6f),
                localPos: new Vector3(-0.12f, hipY - legHalfLength, 0f),
                anchor: new Vector2(0f, legHalfLength), connectedAnchor: new Vector2(-0.12f, hipY),
                outline, mass: 0.15f, gravityScale: gravityScale, sortingOrder: 0);
            CreateLimb(root.transform, rb, "RightLeg", rectSprite, new Vector2(0.12f, 0.6f),
                localPos: new Vector3(0.12f, hipY - legHalfLength, 0f),
                anchor: new Vector2(0f, legHalfLength), connectedAnchor: new Vector2(0.12f, hipY),
                outline, mass: 0.15f, gravityScale: gravityScale, sortingOrder: 0);
            CreateLimb(root.transform, rb, "LeftArm", rectSprite, new Vector2(0.1f, 0.5f),
                localPos: new Vector3(-0.28f, shoulderY - armHalfLength, 0f),
                anchor: new Vector2(0f, armHalfLength), connectedAnchor: new Vector2(-0.28f, shoulderY),
                outline, mass: 0.1f, gravityScale: gravityScale, sortingOrder: 2);
            CreateLimb(root.transform, rb, "RightArm", rectSprite, new Vector2(0.1f, 0.5f),
                localPos: new Vector3(0.28f, shoulderY - armHalfLength, 0f),
                anchor: new Vector2(0f, armHalfLength), connectedAnchor: new Vector2(0.28f, shoulderY),
                outline, mass: 0.1f, gravityScale: gravityScale, sortingOrder: 2);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabAssetPath, out bool success);
            Object.DestroyImmediate(root);

            if (!success)
            {
                Debug.LogError("[SceneBootstrapper] Stickman 프리팹 저장 실패: " + PrefabAssetPath);
            }
            return prefab;
        }

        /// <summary>
        /// 최소 씬 생성: Main Camera(직교) + Stickman 프리팹 인스턴스 1개. 인스턴스는 더미 발판(클래스
        /// 문서 상단 좌표계 설명 참고) 바로 위쪽 — 카메라 뷰포트 상단 가장자리(cam.y+orthographicSize)
        /// 보다 0.3유닛 위 — 에서 낙하해 스냅되도록 배치한다.
        /// </summary>
        public static void BuildMainScene(GameObject stickmanPrefab)
        {
            EnsureFolder(SceneFolder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // orthographicSize=20 채택 근거(플레이테스트 R1 결과): 5로는 640x480 헤드리스 환경에서
            // 세계 폭이 약 13.3유닛에 불과해, 자율 배회 AI(WalkState.walkSpeed=2.5유닛/초)가 15초
            // 관찰 구간 안에 실제로 화면(=유일한 더미 발판) 가장자리에 도달해 버려 CheckScreenBoundsOrFall이
            // 정상적으로 Fall 전이를 발생시키는 것을 실측으로 확인했다(버그가 아니라 의도된 "발판 이탈 시
            // 낙하" 동작 그 자체 — States/StickmanBlackboard.cs 참고). 배회 행동을 화면 끝에 닿지 않고
            // 충분히 관찰하기 위해 세계 폭을 4배(약 53유닛)로 넓힌다 — 캐릭터/물리 배선과는 무관한
            // 순수 카메라 프레이밍 조정.
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 20f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.85f, 0.85f, 0.85f, 1f); // 데스크톱 배경 대용 임시 밝은 회색.
            camGo.AddComponent<AudioListener>();

            if (stickmanPrefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(stickmanPrefab, scene);
                // 더미 발판(클래스 문서 상단 좌표계 설명)은 항상 카메라 뷰포트 "상단" 가장자리(=
                // cam.y+orthographicSize)에 위치한다. 낙하 높이는 0.3유닛으로 보수적으로 잡아
                // CreateOrLoadConfig에서 넓힌 groundSnapTolerance와 함께 헤드리스 환경의 낮은
                // 프레임레이트로 인한 접지 감지 터널링을 이중으로 예방한다.
                instance.transform.position = new Vector3(0f, cam.transform.position.y + cam.orthographicSize + 0.3f, 0f);
            }
            else
            {
                Debug.LogError("[SceneBootstrapper] Stickman 프리팹이 없어 씬에 배치하지 못했습니다.");
            }

            EditorSceneManager.SaveScene(scene, SceneAssetPath);
            RegisterSceneInBuildSettings(SceneAssetPath);
        }

        private static void RegisterSceneInBuildSettings(string scenePath)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
            {
                if (s.path == scenePath) return; // 이미 등록됨.
            }

            var list = new List<EditorBuildSettingsScene>(scenes)
            {
                new EditorBuildSettingsScene(scenePath, true)
            };
            EditorBuildSettings.scenes = list.ToArray();
        }

        private static GameObject CreateStaticVisual(Transform parent, string name, Sprite sprite, Vector3 localPos, Vector2 worldSize, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(worldSize.x, worldSize.y, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return go;
        }

        private static void CreateLimb(Transform hierarchyParent, Rigidbody2D connectedBody, string name, Sprite sprite,
            Vector2 worldSize, Vector3 localPos, Vector2 anchor, Vector2 connectedAnchor, Color color, float mass, float gravityScale, int sortingOrder)
        {
            var limb = new GameObject(name);
            limb.transform.SetParent(hierarchyParent, false);
            limb.transform.localPosition = localPos;
            limb.transform.localScale = Vector3.one; // 조인트 anchor 계산이 스케일에 영향받지 않도록 유지.

            var rb = limb.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.mass = mass;
            rb.gravityScale = gravityScale;

            var joint = limb.AddComponent<HingeJoint2D>();
            joint.connectedBody = connectedBody;
            joint.autoConfigureConnectedAnchor = false; // anchor/connectedAnchor를 초기 배치와 정확히 일치하게 수동 고정(자동 재계산으로 인한 예측 불가 오차 방지).
            joint.anchor = anchor;
            joint.connectedAnchor = connectedAnchor;
            joint.useMotor = false;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(limb.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(worldSize.x, worldSize.y, 1f);

            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
        }

        private static Sprite GetOrCreateSprite(string path, bool isCircle)
        {
            var existingSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existingSprite != null) return existingSprite;

            EnsureFolder(SpritesFolder);

            int size = SpriteTextureSize;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            var pixels = new Color32[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inside = true;
                    if (isCircle)
                    {
                        float dx = (x + 0.5f) - half;
                        float dy = (y + 0.5f) - half;
                        inside = (dx * dx + dy * dy) <= half * half;
                    }
                    pixels[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            tex.name = isCircle ? "CircleTex" : "RectTex";

            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = isCircle ? "CircleSprite" : "RectSprite";

            AssetDatabase.CreateAsset(tex, path);
            AssetDatabase.AddObjectToAsset(sprite, tex);
            AssetDatabase.ImportAsset(path);
            AssetDatabase.SaveAssets();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
