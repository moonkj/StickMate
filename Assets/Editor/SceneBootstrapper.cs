using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.Platform;

namespace StickMate.EditorTools
{
    /// <summary>
    /// Phase 0~6이 순수 C# 게임로직만 구현하고 의도적으로 남겨둔 씬/프리팹 배선(README.md "빌드/실행
    /// 방법" 3번 항목)을 채우는 에디터 빌더. 실제 아트 에셋이 아직 없으므로 코드로 생성한 시각 표현만으로
    /// "졸라맨을 연상시키는 최소 구성"을 만든다.
    ///
    /// 시각 스타일(사용자 확정 요청 대응, 2026-08-28): 사용자가 실행 중인 앱을 직접 보고 채워진
    /// 사각형/원 블록 형태가 "이상하게 나온다"고 지적했고, 두 스타일 중 "고전적 졸라맨 느낌(동그란
    /// 머리 + 가는 선만으로 그린 몸통/팔다리)"을 명시적으로 선택했다. 그래서 채워진 SpriteRenderer
    /// 사각형/원 대신 LineRenderer로 얇은 선(몸통/팔다리)과 속이 빈 원(머리)을 그린다 — 물리 파츠
    /// 배치/크기(Rigidbody2D/Collider2D/HingeJoint2D)는 이 교체로 전혀 바뀌지 않는다(아래
    /// BuildStickmanPrefab 문서, ConfigureLine/CreateHeadRingVisual/CreateLineSegmentVisual 참고).
    ///
    /// 사용법:
    /// - 에디터: 메뉴 StickMate/Build All (최초 1회) — 이미 있는 에셋은 건너뛴다. 기존 에셋을 의도적으로
    ///   덮어쓰려면 StickMate/Rebuild All (기존 자산 덮어씀, 주의)을 쓸 것(BUG-SW-M3, 아래 클래스 문서
    ///   경고 참고).
    /// - 배치 모드(최초 생성/CI용): Unity -batchmode -nographics -projectPath <repo>
    ///   -executeMethod StickMate.EditorTools.SceneBootstrapper.BuildAll -quit -logFile <path>
    ///   (기존 에셋이 있으면 건너뛴다 — 강제로 덮어쓰려면 커맨드라인 끝에 --force 추가)
    ///
    /// 좌표계 참고(BuildMainScene 배치 근거, BUG-P1-R4-B1 핫픽스로 갱신 — 2026-08-28, Architect 진단):
    /// 사용자가 GUI 에디터에서 Main.unity를 직접 Play시켜 육안으로 "화면 제일 상단에서 뭔가 걸려 잘려
    /// 보인다"고 보고했고, 캐릭터가 카메라 뷰포트 최상단 가장자리에 걸쳐 정착하고 있었음이 원인으로
    /// 밝혀졌다. 근본 원인은 Platform/NullPlatformWindowService.cs의 더미 발판이 `y=0`(OS 좌상단
    /// 원점 기준 화면 "맨 위")에 놓여 있던 반대 버그였다(주석은 "작업표시줄"이라 해놓고 실제로는
    /// 화면 최상단에 배치 — Platform/FallbackPlatformWindowService.cs가 예전에(BUG-P1-R3-B1) 고쳤던
    /// 것과 정확히 같은 종류의 실수인데 그때는 이 클래스를 건드리지 않아 남아 있었다). 이제 그 발판은
    /// 화면 세로 길이의 `NullPlatformWindowService.DummyFootholdHeightFraction` 비율만큼을 화면 진짜
    /// "맨 아래"에서 위로 잡는다(그 클래스 상단 문서에 상세 유도 과정 있음).
    ///
    /// 단순히 위/아래만 뒤집으면 반대쪽 가장자리(화면 하단)에서 캐릭터가 잘리는 동일 계열 버그가
    /// 재발할 수 있으므로, 아래 ComputeGroundTopWorldY()가 발판 상단 가장자리(=캐릭터가 서는 지면)의
    /// 월드 Y를 카메라 설정만으로 계산하는 폐쇄형 수식을 제공한다. Screen.height와 발판 두께가 둘 다
    /// 같은 비율(DummyFootholdHeightFraction=f)로 스케일되므로 Screen.height 항이 정확히 상쇄되어
    /// 아래처럼 해상도와 무관한 값이 나온다(카메라가 x=0, z=-10에 있고 orthographic이라고 가정):
    ///   groundTopWorldY = cam.y - orthographicSize * (1 - 2*f)
    /// f=0.2(기본값) 기준 orthographicSize=5일 때 groundTopWorldY = cam.y - 3, 즉 뷰포트 하단
    /// (cam.y-5)에서 2유닛 위, 뷰포트 상단(cam.y+5)까지는 8유닛 남는다. 캐릭터 전신 높이(발~정수리
    /// 약 1.8유닛, BuildStickmanPrefab의 Head/Torso/팔다리 로컬 좌표 참고)를 이 위에 얹으면 머리
    /// 상단이 cam.y-1.2로, 위/아래 여백이 각각 6.2유닛/2유닛 확보되어 최소 요구치(0.5~1유닛)를 크게
    /// 상회한다 — Tests/PlayMode의 StickmanOnScreenFramingTests.cs가 이를 매 실행마다 실측 검증한다.
    ///
    /// 주의(BUG-SW-M2, Architect 반려 수정, 2026-08-28, docs/BUG_REPORT_SCENE_WIRING.md): 카메라
    /// orthographicSize를 바꾸면 GroundSensor의 OS-px↔world-unit 변환 비율(px/unit =
    /// Screen.height/(2*orthographicSize))도 함께 바뀐다. 이 비율에는 StickConfig.groundSnapTolerance
    /// 뿐 아니라 wanderCursorReactionRadiusPx/rodeoStillRadiusPx/rodeoReachDistancePx/
    /// graffitiMinRadiusPx/graffitiMaxRadiusPx/graffitiRegionSizePx/runawayHideSpotMarginPx까지
    /// 총 7개의 OS-px 단위 필드가 종속되어 있다 — orthographicSize를 조정할 때는 반드시 이 8개
    /// 필드(위 7개 + groundSnapTolerance) 전부의 유효 월드 크기를 함께 재검토할 것(Tasklist.md
    /// "씬/프리팹 배선" 절의 재검토 표 참고). "화면이 좁아 배회 관찰 범위가 부족하다"는 문제는
    /// 카메라가 아니라 Platform/NullPlatformWindowService.cs의 더미 발판 폭(OS-px)을 넓히는 것으로
    /// 해결한다 — 그래야 카메라 스케일과 발판 관측 범위가 서로 독립적으로 조정 가능하다.
    ///
    /// 주의(BUG-SW-M3, Architect 반려 수정, 2026-08-28): 아래 BuildAll()/각 Build* 메서드는 기본적으로
    /// "최초 생성" 전용이다 — 대상 에셋(Stickman.prefab/Main.unity/DefaultStickConfig.asset)이 이미
    /// 있으면 건드리지 않고 건너뛴다(로그만 남김). 특히 Main.unity를 강제로 재생성하면 그 사이 씬에
    /// 수동으로 추가한 내용이 전부 사라진다 — 정말로 덮어써야 한다면 메뉴 "StickMate/Rebuild All
    /// (기존 자산 덮어씀, 주의)"을 쓰거나 배치 모드에서 커맨드라인 인자에 --force를 추가할 것.
    /// </summary>
    public static class SceneBootstrapper
    {
        private const string DataFolder = "Assets/_Project/Data";
        private const string PrefabFolder = "Assets/_Project/Prefabs";
        private const string SceneFolder = "Assets/_Project/Scenes";

        private const string ConfigAssetPath = DataFolder + "/DefaultStickConfig.asset";
        private const string PrefabAssetPath = PrefabFolder + "/Stickman.prefab";
        private const string SceneAssetPath = SceneFolder + "/Main.unity";

        // 사용자 확정 요청 대응(2026-08-28) — "고전적 졸라맨"(동그란 머리 + 가는 선만으로 그린 몸통/
        // 팔다리) 시각 스타일. LineRenderer로만 그리므로 스프라이트 텍스처가 더 이상 필요 없다(이전에
        // 쓰던 SpriteTextureSize/GetOrCreateSprite/RectSprite·CircleSprite.asset 제거). 아래 값들은
        // 오직 렌더링에만 영향을 준다 — 물리 파츠 배치/크기(Rigidbody2D/Collider2D/HingeJoint2D)는
        // 전부 무변경.
        private const float LineWidth = 0.05f; // 손그림 느낌의 얇은 선 두께(월드 유닛).
        private const int LineCapVertices = 4; // 선 끝/모서리를 살짝 둥글려 각진 느낌을 줄임(손그림 느낌).
        private const int HeadRingSegments = 24; // 머리 원 근사에 쓰는 선분 개수(24면 육안으로 매끈한 원).
        private const float HeadVisualRadius = 0.25f; // 머리 링의 시각 반경. 물리 CircleCollider2D.radius(0.4, 아래 참고)와는 별개 값 — 판정 크기는 무변경.

        // 손/발 끝 표현(BUG-P1-R5-B4, Architect 웹 레퍼런스 조사 기반 반려 수정, 2026-08-28) — 봉선화
        // (棒線畵, "졸라맨") 표준 표현은 손/발을 "짧은 직각선(hook)"이 아니라 "작은 점(채워진 원)"으로
        // 그린다. 예전 CreateEndMark()는 limb 끝에 짧은 가로선을 그려 몸통 선과 만나 T자/훅 모양이
        // 됐는데, 이게 레퍼런스와 다르다는 지적을 받아 "속이 채워진 작은 원"으로 교체한다(아래
        // CreateEndMark 참고). 채워진 원이지만 SpriteRenderer를 다시 들여오지 않고, 이번 라운드에서
        // 확립한 "LineRenderer만 사용" 컨벤션을 유지한 채로 만든다 — 반지름보다 두꺼운 선 폭으로 아주
        // 작은 원 경로를 그리면 링의 두께가 중심까지 겹쳐 채워진 원처럼 보인다(HeadRingSegments=24는
        // 머리처럼 큰 "속이 빈" 원에 맞는 값이라 그대로 재사용하지 않고, 이 작은 "채워진" 점 전용으로
        // 별도 세그먼트 수/반지름/선폭을 둔다).
        private const float HandFootDotRadius = 0.04f; // Architect 지시 범위(0.03~0.05유닛)의 중간값.
        private const int HandFootDotSegments = 8; // 이 크기(반지름 0.04)에서는 8각형도 육안상 원으로 보임 — 머리(24)만큼 세분화할 필요 없음.
        private const float HandFootDotLineWidth = HandFootDotRadius * 2.4f; // 반지름의 2배(지름)보다 넉넉히 두꺼워야 링 안쪽까지 완전히 채워져 "속이 빈 원"이 아니라 "채워진 점"으로 보인다.

        // BUG-SW-M1(Architect 결정, 2026-08-28) — 표준 Active Ragdoll 레이어 기법: 몸통/머리/팔다리를
        // 전부 이 레이어에 몰아넣고, 이 레이어끼리의 충돌만 Physics2D 매트릭스에서 끈다(EnsureStickmanLimbLayer 참고).
        private const string StickmanLimbLayerName = "StickmanLimb";

        // BUG-SW-M3(Architect 결정, 2026-08-28) — 배치 모드에서 기존 에셋을 의도적으로 덮어쓰고 싶을 때만
        // 켜는 커맨드라인 플래그. 예: -executeMethod StickMate.EditorTools.SceneBootstrapper.BuildAll --force
        private const string ForceCommandLineArg = "--force";

        [MenuItem("StickMate/Build All (최초 1회)")]
        public static void BuildAll()
        {
            BuildAllInternal(HasForceFlag());
        }

        [MenuItem("StickMate/Rebuild All (기존 자산 덮어씀, 주의)")]
        public static void RebuildAllMenuItem()
        {
            // BUG-SW-M3 대응: 대화형 에디터에서는 실수로 기존 씬/프리팹을 날리지 않도록 확인을 받는다.
            // 배치 모드(CI 등)에서는 이 메뉴 항목을 직접 호출한 것 자체가 명시적 의도이므로 대화상자로
            // 막지 않는다(대화상자는 배치 모드에서 응답 불가 상태로 멈출 수 있어 더 위험하다).
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog(
                    "Rebuild All 확인",
                    "기존 " + PrefabAssetPath + " / " + SceneAssetPath + " / " + ConfigAssetPath + "을(를) 전부 덮어씁니다.\n" +
                    "Main.unity에 수동으로 추가한 내용은 이 작업으로 전부 사라집니다. 계속할까요?",
                    "덮어쓰기", "취소"))
            {
                return;
            }
            BuildAllInternal(force: true);
        }

        private static void BuildAllInternal(bool force)
        {
            StickConfig config = CreateOrLoadConfig(force);
            GameObject prefab = BuildStickmanPrefab(config, force);
            BuildMainScene(prefab, config, force);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SceneBootstrapper] BuildAll 완료(force=" + force + ") — " + ConfigAssetPath + ", " + PrefabAssetPath + ", " + SceneAssetPath);
        }

        private static bool HasForceFlag()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == ForceCommandLineArg) return true;
            }
            return false;
        }

        [MenuItem("StickMate/Build Default StickConfig")]
        public static StickConfig CreateOrLoadConfig()
        {
            return CreateOrLoadConfig(force: false);
        }

        /// <summary>
        /// BUG-SW-M3 대응: 이미 존재하는 config는 기본적으로(force==false) 건드리지 않는다 — 이전에는
        /// groundSnapTolerance 한 필드만 재실행 때마다 항상 강제 재적용해, 누군가 에디터에서 이 값을
        /// 다른 이유로 수동 조정해둬도 다음 BuildAll 실행 때 조용히 20으로 되돌아가는 문제가 있었다
        /// (docs/BUG_REPORT_SCENE_WIRING.md Minor 2). 이제는 "새로 만드는 경우" 또는 "force==true"일
        /// 때만 이 튜닝값을 적용한다.
        /// </summary>
        public static StickConfig CreateOrLoadConfig(bool force)
        {
            EnsureFolder(DataFolder);
            var existing = AssetDatabase.LoadAssetAtPath<StickConfig>(ConfigAssetPath);

            if (existing != null && !force)
            {
                Debug.Log("[SceneBootstrapper] " + ConfigAssetPath + "이(가) 이미 존재해 건너뜁니다(기존 값 보존, --force로 강제 가능) — BUG-SW-M3.");
                return existing;
            }

            StickConfig config = existing != null ? existing : ScriptableObject.CreateInstance<StickConfig>();

            // 실측 튜닝(플레이테스트 R1 결과, Tasklist.md "씬/프리팹 배선" 절 참고): 기본값 6px는
            // Screen=640x480 헤드리스 배치 환경에서 세계단위로 환산하면 약 0.125유닛 폭의 매우 좁은
            // 밴드가 되는데, 그 프레임의 실제 프레임타임이 충분히 크면(관찰상 이 환경에서 실제로 발생)
            // 캐릭터가 단 1유닛 낙하만으로도 그 밴드를 한 프레임에 통과("접지 감지 터널링")해 무한
            // 낙하하는 것을 배치 모드 PlayMode 스모크 테스트로 실측 확인했다(States/GroundSensor.cs
            // 로직 자체는 무수정 — 여기서는 StickConfig.cs가 "추후 물리 튜닝으로 교체될 임시값"이라고
            // 명시한 데이터 값만 조정한다). 20px로 넉넉히 키워 이 배치 환경 기준 약 0.3~0.4유닛 밴드를
            // 확보한다(orthographicSize=5 기준 — BUG-SW-M2 대응으로 orthographicSize를 원래 값으로
            // 되돌렸으므로 이 계산은 다시 유효하다. orthographicSize를 바꿀 경우의 재검토 의무는 클래스
            // 문서 상단 BUG-SW-M2 경고 참고).
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
            GameObject prefab = BuildStickmanPrefab(CreateOrLoadConfig(force: false), force: false);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        /// <summary>
        /// 졸라맨 프리팹 생성. 루트(Rigidbody2D+CapsuleCollider2D+StickmanClickHitbox+StickmanAgent)는
        /// StickmanBlackboard.SenseGround()가 Body.position을 그대로 "발" 위치로 취급하므로 로컬 y=0을
        /// 발 높이로 둔다. 팔다리는 States/RagdollRig.cs가 GetComponentsInChildren&lt;Rigidbody2D/
        /// HingeJoint2D&gt;(true)로 순회할 수 있도록 각자 독립된 Rigidbody2D+HingeJoint2D(root에 연결)를
        /// 갖는다.
        ///
        /// BUG-SW-M1 반려 수정(Architect 결정, 2026-08-28, docs/BUG_REPORT_SCENE_WIRING.md): 이전에는
        /// 팔다리에 Collider2D를 아예 안 붙여 자체충돌 떨림을 막았는데, 그 결과 씬에 바닥 Collider2D도
        /// 없는 것과 겹쳐 RAGDOLL이 무엇과도 충돌할 수 없어 영원히 낙하하는 구조적 결함을 낳았다.
        /// 이제는 표준 Active Ragdoll 기법(레이어 기반 자체충돌 차단)을 쓴다: 루트/머리/팔다리 전부를
        /// 하나의 전용 레이어(StickmanLimbLayerName)에 몰아넣고, 그 레이어끼리의 충돌만 Physics2D
        /// 매트릭스에서 끈다(EnsureStickmanLimbLayer). 이러면 팔다리는 실제 Collider2D를 갖고 바닥
        /// 등 다른 레이어와는 정상 충돌하면서도, 서로(그리고 몸통과)는 여전히 충돌하지 않는다 — 원래
        /// 걱정했던 "몸통/팔다리 겹치는 콜라이더의 상시 떨림"은 콜라이더 제거가 아니라 레이어
        /// 필터링으로 해결된다.
        /// </summary>
        public static GameObject BuildStickmanPrefab(StickConfig config)
        {
            return BuildStickmanPrefab(config, force: false);
        }

        /// <summary>force==false(기본값)면 Stickman.prefab이 이미 존재할 때 건드리지 않고 건너뛴다
        /// (BUG-SW-M3 대응 — 재실행마다 fileID가 무작위로 재할당되어 Main.unity의 PrefabInstance
        /// 오버라이드가 고아가 되는 것을 방지).</summary>
        public static GameObject BuildStickmanPrefab(StickConfig config, bool force)
        {
            EnsureFolder(PrefabFolder);

            // 레이어/충돌 매트릭스 설정은 멱등적이고 되돌릴 위험이 없는 프로젝트 설정 변경이라, 프리팹
            // 자체를 건너뛰는 경우에도 항상 재확인해 최신 상태로 유지한다(BUG-SW-M1).
            int limbLayer = EnsureStickmanLimbLayer();

            var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);
            if (existingPrefab != null && !force)
            {
                Debug.Log("[SceneBootstrapper] " + PrefabAssetPath + "이(가) 이미 존재해 건너뜁니다(기존 fileID 보존, --force로 강제 가능) — BUG-SW-M3.");
                return existingPrefab;
            }

            Color outline = config != null ? config.primaryOutlineColor : Color.black;
            float gravityScale = config != null ? config.gravityScale : 3f;

            var root = new GameObject("Stickman");
            root.layer = limbLayer;

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

            // 몸통 — 시각 전용(물리 없음, root 자식 Transform으로 그대로 따라다님). 채워진 사각형 대신
            // 얇은 세로 선 하나로 표현(사용자 확정 요청, 클래스 문서 상단 참고). 기존 사각형과 동일한
            // 세로 범위(로컬 y 0.6~1.4)를 그대로 유지해 화면 프레이밍(BUG-P1-R4-B1)에 영향이 없다.
            CreateLineSegmentVisual(root.transform, "Torso", new Vector3(0f, 1.0f, 0f),
                new Vector3(0f, 0.4f, 0f), new Vector3(0f, -0.4f, 0f), outline, sortingOrder: 1);

            // 머리 — 시각(속이 빈 링) + 작은 CircleCollider2D(루트 Rigidbody2D의 compound collider로
            // 자동 합산됨). 루트와 같은 limbLayer에 두어야 팔다리와의 자체충돌 무시 매트릭스가 머리에도
            // 적용된다. 물리 판정 반경(0.4)은 BUG-SW-M1 이후 그대로 — 시각 반경(HeadVisualRadius)만
            // 사용자 요청에 맞춰 "채워진 원"에서 "속이 빈 동그라미"로 바꿨을 뿐 판정 크기는 무변경.
            var head = CreateHeadRingVisual(root.transform, "Head", new Vector3(0f, 1.6f, 0f), HeadVisualRadius, outline, sortingOrder: 3);
            head.layer = limbLayer;
            var headCollider = head.AddComponent<CircleCollider2D>();
            headCollider.radius = 0.4f;

            // 팔다리 — Rigidbody2D + HingeJoint2D(connectedBody=root) + Collider2D(limbLayer). 조인트
            // anchor 계산이 스케일에 영향받지 않도록 물리 오브젝트 자체는 scale=1로 유지하고, 스프라이트는
            // 별도 자식(Visual)에서만 스케일.
            const float hipY = 0.6f, shoulderY = 1.3f;
            const float legHalfLength = 0.3f, armHalfLength = 0.25f;

            CreateLimb(root.transform, rb, "LeftLeg", new Vector2(0.12f, 0.6f),
                localPos: new Vector3(-0.12f, hipY - legHalfLength, 0f),
                anchor: new Vector2(0f, legHalfLength), connectedAnchor: new Vector2(-0.12f, hipY),
                outline, mass: 0.15f, gravityScale: gravityScale, sortingOrder: 0, limbLayer: limbLayer, agent: agent);
            CreateLimb(root.transform, rb, "RightLeg", new Vector2(0.12f, 0.6f),
                localPos: new Vector3(0.12f, hipY - legHalfLength, 0f),
                anchor: new Vector2(0f, legHalfLength), connectedAnchor: new Vector2(0.12f, hipY),
                outline, mass: 0.15f, gravityScale: gravityScale, sortingOrder: 0, limbLayer: limbLayer, agent: agent);
            CreateLimb(root.transform, rb, "LeftArm", new Vector2(0.1f, 0.5f),
                localPos: new Vector3(-0.28f, shoulderY - armHalfLength, 0f),
                anchor: new Vector2(0f, armHalfLength), connectedAnchor: new Vector2(-0.28f, shoulderY),
                outline, mass: 0.1f, gravityScale: gravityScale, sortingOrder: 2, limbLayer: limbLayer, agent: agent);
            CreateLimb(root.transform, rb, "RightArm", new Vector2(0.1f, 0.5f),
                localPos: new Vector3(0.28f, shoulderY - armHalfLength, 0f),
                anchor: new Vector2(0f, armHalfLength), connectedAnchor: new Vector2(0.28f, shoulderY),
                outline, mass: 0.1f, gravityScale: gravityScale, sortingOrder: 2, limbLayer: limbLayer, agent: agent);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabAssetPath, out bool success);
            Object.DestroyImmediate(root);

            if (!success)
            {
                Debug.LogError("[SceneBootstrapper] Stickman 프리팹 저장 실패: " + PrefabAssetPath);
            }
            return prefab;
        }

        /// <summary>
        /// 최소 씬 생성: Main Camera(직교) + Stickman 프리팹 인스턴스 1개 + RAGDOLL용 정적 바닥
        /// Collider2D 1개. 인스턴스는 더미 발판(클래스 문서 상단 좌표계 설명, ComputeGroundTopWorldY
        /// 참고) 바로 위쪽 — 그 지면보다 0.3유닛 위 — 에서 낙하해 스냅되도록 배치한다.
        /// </summary>
        public static void BuildMainScene(GameObject stickmanPrefab)
        {
            BuildMainScene(stickmanPrefab, CreateOrLoadConfig(force: false), force: false);
        }

        /// <summary>force==false(기본값)면 Main.unity가 이미 존재할 때 건드리지 않고 건너뛴다(BUG-SW-M3
        /// 대응 — EditorSceneManager.NewScene(EmptyScene)로 항상 완전히 새로 만들던 이전 동작은 그
        /// 사이 씬에 수동으로 추가된 모든 내용을 경고 없이 파괴했다).</summary>
        public static void BuildMainScene(GameObject stickmanPrefab, StickConfig config, bool force)
        {
            var existingScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneAssetPath);
            if (existingScene != null && !force)
            {
                Debug.Log("[SceneBootstrapper] " + SceneAssetPath + "이(가) 이미 존재해 건너뜁니다(수동 편집 내용 보존, --force로 강제 가능) — BUG-SW-M3.");
                RegisterSceneInBuildSettings(SceneAssetPath); // 이미 등록돼 있어도 안전(중복 등록 방지 로직 있음).
                return;
            }

            EnsureFolder(SceneFolder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // BUG-SW-M2 반려 수정(Architect 결정, 2026-08-28, docs/BUG_REPORT_SCENE_WIRING.md):
            // orthographicSize를 원래 설계값(5)으로 되돌린다. 이전 라운드는 "화면이 좁아 배회 AI가
            // 15초 관찰 구간 안에 화면 끝(=유일한 더미 발판) 가장자리에 도달해버린다"는 문제를
            // orthographicSize를 5→20으로 키워서 해결했는데, 이 값은 GroundSensor의 OS-px↔world-unit
            // 변환 비율에도 곱연산으로 반영되어 groundSnapTolerance 등 8개 OS-px 필드의 유효 월드
            // 크기를 조용히 4배 넓혀버리는 부작용을 냈다(클래스 문서 상단 BUG-SW-M2 경고 참고). 관찰
            // 범위가 좁다는 원래 문제는 카메라가 아니라 Platform/NullPlatformWindowService.cs의 더미
            // 발판 폭(OS-px, DummyFootholdWidthMultiplier)을 넓히는 것으로 독립적으로 해결한다.
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            // 투명 오버레이 비활성화(Architect 결정, 2026-08-28, "완전히 새까만 화면" 사고 대응): 이전
            // 라운드들은 이 알파를 0으로 낮춰 카메라 배경을 완전 투명으로 만들고
            // Platform/MacOS/StickMateOverlayPlugin.m의 SM_ConfigureOverlayWindow(transparent=1)와 짝을
            // 이루려 했으나, 그 네이티브 창 투명화가 여러 라운드에 걸쳐 한 번도 실제로 성공한 적이 없다
            // (Unity Standalone Mac Player의 렌더 서페이스가 기본적으로 불투명 합성을 가정). 그 결과
            // 알파=0인 픽셀이 RGB 값과 무관하게 그냥 검정으로 합성되어 "완전히 균일한 검정 화면"으로
            // 보이는 사고가 재발했다(사용자 실측, 2026-08-28) — 알파 0을 유지하는 한 RGB를 무엇으로
            // 설정해도 의미가 없었다는 뜻이다. 진짜 투명 창은 명시적으로 다음 과제로 미루고, 이번
            // 라운드는 카메라 배경 알파를 항상 1(완전 불투명)로 고정해 RGB(StickConfig.
            // backgroundFallbackColor, 기본 밝은 회색)가 확실히, 그대로 렌더링되게 한다 —
            // primaryOutlineColor(검정) 캐릭터 선과 대비되는 밝은 배경. MacWindowService.cs의
            // SM_ConfigureOverlayWindow 호출도 이 라운드에서 transparent=0으로 바뀌었다(클릭관통/
            // 항상위는 계속 실제 NSWindow API를 사용 — 그쪽은 이미 로그로 정상 동작이 확인됨).
            Color fallbackBg = config != null ? config.backgroundFallbackColor : new Color(0.94f, 0.94f, 0.94f);
            cam.backgroundColor = new Color(fallbackBg.r, fallbackBg.g, fallbackBg.b, 1f);
            camGo.AddComponent<AudioListener>();

            // BUG-SW-M1 대응: RAGDOLL이 실제로 부딪혀 멈출 수 있는 정적 바닥. Rigidbody2D를 붙이지
            // 않으므로 Unity가 자동으로 정적 콜라이더로 취급한다(Architect 결정 — "표준 랙돌 기법").
            CreateGroundCollider(cam);

            if (stickmanPrefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(stickmanPrefab, scene);
                // 더미 발판(클래스 문서 상단 좌표계 설명, ComputeGroundTopWorldY 참고)의 상단 가장자리
                // 바로 위에서 낙하해 스냅되도록 배치한다. 낙하 높이는 0.3유닛으로 보수적으로 잡아
                // CreateOrLoadConfig에서 넓힌 groundSnapTolerance와 함께 헤드리스 환경의 낮은
                // 프레임레이트로 인한 접지 감지 터널링을 이중으로 예방한다.
                instance.transform.position = new Vector3(0f, ComputeGroundTopWorldY(cam) + 0.3f, 0f);
            }
            else
            {
                Debug.LogError("[SceneBootstrapper] Stickman 프리팹이 없어 씬에 배치하지 못했습니다.");
            }

            EditorSceneManager.SaveScene(scene, SceneAssetPath);
            RegisterSceneInBuildSettings(SceneAssetPath);
        }

        // BUG-SW-M1 대응: RAGDOLL 물리 안전망 바닥의 폭. 화면 폭(약 13.3유닛, orthoSize=5 기준)이나
        // NullPlatformWindowService의 넓힌 배회 범위(약 53유닛, DummyFootholdWidthMultiplier=4 기준)
        // 보다 넉넉히 넓게 고정폭으로 잡는다 — 정확한 화면 폭 계산식에 종속시키지 않는 이유는
        // BUG-SW-M2의 교훈(서로 다른 목적의 크기 계산을 하나의 값에 묶으면 한쪽을 조정할 때 다른 쪽이
        // 조용히 깨진다) 때문이다. 캐릭터가 배회 범위 어디에 있다가 RAGDOLL에 진입해도 이 바닥을
        // 벗어나지 않는다.
        private const float GroundColliderHalfWidth = 100f;
        private const float GroundColliderThickness = 2f;

        // BUG-SW-M4 대응(Architect 결정, 2026-08-28, docs/BUG_REPORT_SCENE_WIRING.md) — 팔다리
        // Rigidbody2D가 linearDamping=0(Unity 기본값)이었던 것이 "이동(Walk) 중 피격" RAGDOLL이
        // GETUP으로 절대 복귀하지 못하는 근본 원인 중 하나였다: 걷기 관성이 HingeJoint2D를 통해
        // 팔다리로 전파된 채 감쇠 없이 계속 진동해 RagdollState의 정착 판정
        // (StickConfig.ragdollSettleSpeedThreshold 이하가 ragdollSettleHoldDuration초 유지)이
        // 15초 관찰 안에 성립하지 않았다(실측: 8회 중 2회, 전부 Walk 피격에서 재현). 실제 랙돌은
        // 항상 0이 아닌 damping을 갖는다는 것이 물리적으로 당연하므로 이는 설계 결함이 아니라 프리팹
        // 튜닝 누락이었다. 값은 실측(StickmanRagdollRecoveryTests 10회+ 반복 PlayMode 실행)으로
        // "이동 중 피격도 안정적으로 정착"과 "너무 뻣뻣해 보이지 않음(순간 정지처럼 안 보임)" 사이의
        // 균형을 잡아 선정했다 — 너무 크면 랙돌이 마치 진흙 속에 있는 것처럼 즉시 멈춰버리고, 너무
        // 작으면 이번 버그가 재발한다.
        private const float LimbLinearDamping = 0.6f;
        private const float LimbAngularDamping = 1.5f;

        /// <summary>
        /// 더미 발판(Platform/NullPlatformWindowService.cs)의 상단 가장자리가 대응하는 월드 Y를,
        /// 실제 Screen.height 실측값과 무관한 폐쇄형 수식으로 계산한다(클래스 문서 상단 "좌표계 참고"
        /// 절에 유도 과정 있음). CreateGroundCollider()와 BuildMainScene()의 캐릭터 초기 배치가 반드시
        /// 이 헬퍼 하나만 거쳐야 한다 — 두 곳이 각자 따로(매직 넘버로) 계산하다가 서로 어긋난 것 자체가
        /// 이번 화면 프레이밍 버그의 근본 원인 중 하나였다(BUG-P1-R4-B1). NullPlatformWindowService의
        /// DummyFootholdHeightFraction 공개 상수를 그대로 참조해, 그 클래스의 발판 배치가 바뀌면 이
        /// 계산도 자동으로 함께 갱신되도록 한다(재발 방지).
        /// </summary>
        private static float ComputeGroundTopWorldY(Camera cam)
        {
            float fraction = NullPlatformWindowService.DummyFootholdHeightFraction;
            return cam.transform.position.y - cam.orthographicSize * (1f - 2f * fraction);
        }

        /// <summary>
        /// RAGDOLL이 실제로 착지할 수 있는 정적 바닥(Rigidbody2D 없음 — Unity 표준 정적 콜라이더).
        /// Y좌표는 NullPlatformWindowService의 더미 발판이 논리적으로 대응하는 높이(ComputeGroundTopWorldY
        /// 참고)와 일치시킨다 — Idle/Walk의 SnapToGround가 캐릭터를 스냅시키는 바로 그 Y이므로, RAGDOLL
        /// 진입 직후 root의 CapsuleCollider2D(발 피벗 기준 바닥이 로컬 y=0)가 곧바로 이 바닥과 접촉한다.
        /// 레이어는 Default(0)로 둔다 — StickmanLimbLayerName과는 자기들끼리만 충돌을 끄는 매트릭스이므로
        /// Default 레이어와는 정상적으로 충돌한다.
        /// </summary>
        private static void CreateGroundCollider(Camera cam)
        {
            var ground = new GameObject("PhysicsGround");
            ground.layer = 0; // Default.

            float groundTopWorldY = ComputeGroundTopWorldY(cam);
            ground.transform.position = new Vector3(0f, groundTopWorldY - GroundColliderThickness * 0.5f, 0f);

            var collider = ground.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(GroundColliderHalfWidth * 2f, GroundColliderThickness);
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

        /// <summary>
        /// SpriteRenderer가 기본으로 쓰는 것과 동일한 Unity 내장 머티리얼을 그대로 재사용한다 — 새
        /// 에셋을 만들 필요가 없고(임시 Material 인스턴스를 그냥 대입하면 프리팹 저장 시 참조가
        /// 유실될 위험이 있는 것과 달리, 이건 항상 유효한 영구 엔진 내장 에셋 참조다), Built-in 렌더
        /// 파이프라인(ProjectSettings/GraphicsSettings.asset의 m_CustomRenderPipeline: {fileID: 0}로
        /// 확인됨 — URP/HDRP 미사용)에서 확실히 렌더링됨이 보장된다.
        /// </summary>
        private static Material GetLineMaterial()
        {
            return AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        }

        /// <summary>
        /// LineRenderer 공통 설정. useWorldSpace=false로 두는 것이 핵심 — 점 좌표를 자신이 붙은
        /// Transform 기준 로컬 좌표로 해석하게 해서, 팔다리처럼 HingeJoint2D 물리로 매 프레임
        /// 이동/회전하는 오브젝트에 붙여도 별도의 "매프레임 위치 갱신" 스크립트 없이 자동으로 따라간다
        /// (Renderer는 어차피 매 프레임 자신이 속한 Transform의 world 행렬로 로컬 정점을 다시 그리기
        /// 때문 — MeshRenderer/SpriteRenderer와 동일한 원리).
        /// </summary>
        private static LineRenderer ConfigureLine(GameObject go, Color color, int sortingOrder, bool loop)
        {
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.material = GetLineMaterial();
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = LineWidth;
            lr.endWidth = LineWidth;
            lr.numCapVertices = LineCapVertices; // 끝을 살짝 둥글려 손그림 느낌(각진 사각형 끝 대신).
            lr.numCornerVertices = LineCapVertices;
            lr.sortingOrder = sortingOrder;
            lr.loop = loop;
            return lr;
        }

        /// <summary>직선 하나로 된 시각 표현(몸통). 물리 없이 parent의 자식 Transform으로만 존재 —
        /// CreateStaticVisual이 하던 역할을 사각형 스프라이트 대신 LineRenderer로 대체한다.</summary>
        private static GameObject CreateLineSegmentVisual(Transform parent, string name, Vector3 localPos, Vector3 localStart, Vector3 localEnd, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one;

            var lr = ConfigureLine(go, color, sortingOrder, loop: false);
            lr.positionCount = 2;
            lr.SetPosition(0, localStart);
            lr.SetPosition(1, localEnd);
            return go;
        }

        /// <summary>속이 빈 원(링) 시각 표현(머리) — HeadRingSegments개의 점을 원주 위에 찍고
        /// loop=true로 닫아 "채워지지 않은 동그라미"를 그린다.</summary>
        private static GameObject CreateHeadRingVisual(Transform parent, string name, Vector3 localPos, float radius, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one;

            var lr = ConfigureLine(go, color, sortingOrder, loop: true);
            lr.positionCount = HeadRingSegments;
            for (int i = 0; i < HeadRingSegments; i++)
            {
                float angle = (i / (float)HeadRingSegments) * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }
            return go;
        }

        /// <summary>손/발 표현용 작은 채워진 점(봉선화 표준 표현, 클래스 상단 BUG-P1-R5-B4 문서 참고).
        /// parent(limb)의 자식으로 둬 부모와 함께 물리로 이동/회전한다. 아주 작은 원형 경로를 그 반지름보다
        /// 두꺼운 선으로 그려 "속이 빈 원"이 아니라 "채워진 점"처럼 보이게 한다(LineRenderer만 쓰는
        /// 컨벤션 유지 — SpriteRenderer 재도입 없음).</summary>
        private static void CreateEndMark(Transform parent, Vector3 localAt, Color color, int sortingOrder)
        {
            var go = new GameObject("EndMark");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localAt;
            go.transform.localScale = Vector3.one;

            var lr = ConfigureLine(go, color, sortingOrder, loop: true);
            lr.startWidth = HandFootDotLineWidth;
            lr.endWidth = HandFootDotLineWidth;
            lr.positionCount = HandFootDotSegments;
            for (int i = 0; i < HandFootDotSegments; i++)
            {
                float angle = (i / (float)HandFootDotSegments) * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * HandFootDotRadius, Mathf.Sin(angle) * HandFootDotRadius, 0f));
            }
        }

        private static void CreateLimb(Transform hierarchyParent, Rigidbody2D connectedBody, string name,
            Vector2 worldSize, Vector3 localPos, Vector2 anchor, Vector2 connectedAnchor, Color color, float mass, float gravityScale,
            int sortingOrder, int limbLayer, StickmanAgent agent)
        {
            var limb = new GameObject(name);
            limb.transform.SetParent(hierarchyParent, false);
            limb.transform.localPosition = localPos;
            limb.transform.localScale = Vector3.one; // 조인트 anchor 계산이 스케일에 영향받지 않도록 유지.
            limb.layer = limbLayer; // BUG-SW-M1: 루트/머리와 같은 레이어 — 자체충돌은 매트릭스가 끄고, 바닥 등과는 정상 충돌.

            var rb = limb.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.mass = mass;
            rb.gravityScale = gravityScale;
            // BUG-SW-M4: 아래 클래스 상수 선언부 주석 참고 — 감쇠 없는 팔다리가 이동 중 피격 RAGDOLL의
            // 정착 실패 원인이었다.
            rb.linearDamping = LimbLinearDamping;
            rb.angularDamping = LimbAngularDamping;

            var joint = limb.AddComponent<HingeJoint2D>();
            joint.connectedBody = connectedBody;
            joint.autoConfigureConnectedAnchor = false; // anchor/connectedAnchor를 초기 배치와 정확히 일치하게 수동 고정(자동 재계산으로 인한 예측 불가 오차 방지).
            joint.anchor = anchor;
            joint.connectedAnchor = connectedAnchor;
            joint.useMotor = false;

            // BUG-SW-M1: 팔다리에 실제 Collider2D를 부여한다(이전에는 자체충돌 떨림을 막으려고 아예
            // 없앴는데, 그 결과 RagdollLimbImpactRelay가 영구히 발동 불가능해지고 바닥과도 충돌할 수
            // 없어 RAGDOLL이 절대 안착하지 못했다). limb 자신의 원점(anchor 계산 기준)에 그대로
            // 겹치게 둔다. 물리 판정 크기(worldSize)는 시각 표현(가는 선)과 완전히 독립 — 사용자 요청에
            // 따라 시각만 얇은 선으로 바꿨을 뿐 이 BUG-SW-M1 튜닝값에는 손대지 않았다.
            var collider = limb.AddComponent<BoxCollider2D>();
            collider.size = worldSize;

            // BUG-SW-M1: 사지 피격을 StickmanAgent.ReportExternalImpact()로 중계 — 이전에는 어떤
            // 프리팹에도 부착되지 않아 죽은 코드였다. Reset()/Awake() 기반 자동 탐색(GetComponentInParent)에
            // 의존하지 않고, StickmanAgent._config와 동일한 패턴(SerializedObject 직접 대입)으로
            // 에디터 시점에 확실하게 배선한다 — 에디터 스크립팅 중에는 MonoBehaviour 생명주기 콜백
            // 실행 시점이 보장되지 않기 때문이다.
            var relay = limb.AddComponent<RagdollLimbImpactRelay>();
            var relaySo = new SerializedObject(relay);
            relaySo.FindProperty("_agent").objectReferenceValue = agent;
            relaySo.ApplyModifiedPropertiesWithoutUndo();

            // 사용자 확정 요청 대응(2026-08-28) — "고전적 졸라맨" 시각 스타일(클래스 문서 상단 참고).
            // limb 자신은 이미 scale=1(위 주석 — 조인트 anchor 계산 때문)이라 LineRenderer를 별도
            // "Visual" 자식 없이 limb에 직접 붙인다. 로컬 y축을 따라 관절(anchor) 쪽에서 반대쪽 끝
            // (손/발)까지 얇은 선 하나를 그린다 — anchor=(0, halfLength)가 이미 이 limb의 "위쪽 끝"이므로
            // 그대로 재사용해 하드코딩 중복 없이 시각과 물리 anchor가 항상 일치하게 한다.
            float halfLength = worldSize.y * 0.5f;
            var lr = ConfigureLine(limb, color, sortingOrder, loop: false);
            lr.positionCount = 2;
            lr.SetPosition(0, new Vector3(0f, halfLength, 0f));
            lr.SetPosition(1, new Vector3(0f, -halfLength, 0f));

            // 손/발 표현(필수는 아니지만 "졸라맨" 느낌 강화) — limb 끝(관절 반대쪽, 손/발 위치)에
            // 짧은 가로선을 하나 더 그린다. limb의 자식으로 둬 부모와 함께 물리로 이동/회전한다.
            CreateEndMark(limb.transform, new Vector3(0f, -halfLength, 0f), color, sortingOrder);
        }

        /// <summary>
        /// BUG-SW-M1 대응(Architect 결정, 2026-08-28) — 표준 Active Ragdoll 레이어 기법: 몸통/머리/
        /// 팔다리를 전부 이 레이어에 몰아넣고, 이 레이어끼리의 충돌만 Physics2D 레이어 충돌 매트릭스에서
        /// 끈다(자체충돌 떨림 방지). 콜라이더를 아예 없애던 기존 접근과 달리, 다른 레이어(바닥 등)와는
        /// 정상적으로 충돌한다. 이미 존재하는 레이어면 그 인덱스를 그대로 재사용한다(재실행 시 중복
        /// 생성/재할당 없음 — BUG-SW-M3와 동일한 멱등성 원칙). Physics2D.IgnoreLayerCollision 호출은
        /// 에디터(비-Play 모드)에서 실행되면 ProjectSettings/Physics2DSettings.asset에 바로 반영된다
        /// (Project Settings > Physics 2D 창에서 매트릭스를 직접 클릭하는 것과 동일한 효과).
        /// </summary>
        private static int EnsureStickmanLimbLayer()
        {
            var tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManagerAssets == null || tagManagerAssets.Length == 0)
            {
                Debug.LogError("[SceneBootstrapper] ProjectSettings/TagManager.asset을 열지 못해 '" +
                    StickmanLimbLayerName + "' 레이어를 만들지 못했습니다 — 팔다리 자체충돌 방지가 적용되지 않습니다.");
                return 0;
            }

            var tagManager = new SerializedObject(tagManagerAssets[0]);
            var layersProp = tagManager.FindProperty("layers");

            for (int i = 0; i < layersProp.arraySize; i++)
            {
                if (layersProp.GetArrayElementAtIndex(i).stringValue == StickmanLimbLayerName)
                {
                    Physics2D.IgnoreLayerCollision(i, i, true);
                    return i;
                }
            }

            // 사용자 레이어 슬롯은 8~31(0~7은 Unity 내장 예약 레이어). 첫 빈 슬롯에 배정한다.
            for (int i = 8; i < layersProp.arraySize; i++)
            {
                var element = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(element.stringValue))
                {
                    element.stringValue = StickmanLimbLayerName;
                    tagManager.ApplyModifiedProperties();
                    Physics2D.IgnoreLayerCollision(i, i, true);
                    return i;
                }
            }

            Debug.LogError("[SceneBootstrapper] 빈 레이어 슬롯이 없어 '" + StickmanLimbLayerName + "' 레이어를 만들지 못했습니다.");
            return 0;
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
