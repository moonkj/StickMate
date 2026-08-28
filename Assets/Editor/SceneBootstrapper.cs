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
    /// BuildStickmanPrefab 문서, ConfigureLine/CreateFilledHead/CreateLineSegmentVisual 참고).
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
        // 선 두께/캡 — 2026-08-28 사용자가 제시한 시각 레퍼런스(Alan Becker "Animator vs Animation"
        // 계열 스틱맨) 반영. 그 스타일의 핵심은 (a) 아주 굵은 검은 획, (b) 모든 선 끝이 둥근 캡이라
        // 관절에서 둥근 끝끼리 자연스럽게 겹쳐 매끄럽게 이어진다는 점이다. 우리가 계속 고생했던
        // "관절이 나눠져 보임"/"검은 뭉치" 문제는 이 스타일에서는 저절로 해결된다 — 오히려 관절 부위가
        // 살짝 뭉쳐 보이는 게 정상이고 자연스럽다(리더 명시).
        private const float LineWidth = 0.11f;      // 기본 획 두께(몸통). 머리 반경 0.22의 절반.
        private const float LegLineWidth = 0.12f;   // 다리는 기본보다 아주 약간 굵게.
        private const float ArmLineWidth = 0.10f;   // 팔은 기본보다 아주 약간 얇게.
        private const int LineCapVertices = 8; // 끝/모서리를 확실히 둥글게(레퍼런스 스타일의 round cap).
        private const int HeadRingSegments = 24; // 머리 테두리 링 근사에 쓰는 선분 개수(24면 육안으로 매끈한 원).

        // 흰 얼굴의 검은 테두리 두께 — 팔다리 획(0.10~0.12)보다 약간 얇게 잡아 머리가 지나치게 두꺼워
        // 보이지 않게 한다(리더 지시: "팔다리 선 두께와 비슷하거나 약간 얇게").
        private const float HeadOutlineWidth = 0.09f;
        // 머리 시각 반경. 물리 CircleCollider2D.radius(0.4, 아래 참고)와는 별개 값 — 판정 크기는 무변경.
        // 머리는 "흰색으로 채워진 원 + 검은 테두리"다(사용자 정정, 2026-08-28) — CreateFilledHead()가
        // 길이 0인 선분에 지름만큼의 선 폭 + 둥근 캡을 줘서 흰 채움을 만들고, 그 위에 CreateRing()이
        // 검은 링을 겹쳐 그린다(SpriteRenderer 재도입 없이 "LineRenderer만 사용" 컨벤션 유지).
        private const float HeadVisualRadius = 0.22f;

// 눈동자 점(CreateFilledDot)이 쓰는 원 근사 세분화 수. 손/발 끝 점은 2026-08-28 사용자 요청으로
        // 완전히 제거했으므로("손과 발에 동그란 뭉치같은건 필요없을거 같은데") 이제 이 상수는 눈 전용이다.
        // 이 크기(반지름 0.018)에서는 8각형도 육안상 원으로 보인다 — 머리 링(24)만큼 세분화할 필요 없음.
        private const int FilledDotSegments = 8;

        // 머리(채워진 원)의 둥근 캡 세분화 — 반지름이 크므로 점(8)보다 훨씬 촘촘해야 매끈해 보인다.
        private const int HeadCapVertices = 16;


        // 눈(눈동자 점) — 2026-08-28 사용자 요청("나중에 마우스 위치에 따라 눈도 움직여야 해서 눈도
        // 있어야 하고"). 리더 지정 좌표: 머리 링 반경 0.22 기준 (±0.075, +0.02), 반경 0.018.
        // "눈도 너무 커서 이상함"(사용자) 대응으로 0.035 -> 0.018로 축소 — 머리 안에 작은 점 두 개가
        // 콕 찍힌 정도. 눈동자 이동 범위는 States/EyeController.cs의 MaxPupilOffset이 제한한다.
        private const float EyePupilRadius = 0.018f;
        private const float EyeOffsetX = 0.075f;
        private const float EyeOffsetY = 0.02f;

        // 중립(Idle) 팔 벌림 각도 — StickConfig.idleArmSpreadDegrees와 반드시 같은 값이어야 한다
        // (프리팹 저장 시점의 초기 localRotation과 런타임 포즈 목표각이 일치해야 첫 프레임에 튀지 않는다).
        private const float IdleArmSpreadDegrees = 40f;

        // 중립(Idle) 다리 벌림 각도 — StickConfig.idleLegSpreadDegrees와 반드시 같은 값이어야 한다
        // (프리팹 저장 자세와 런타임 포즈 목표각이 어긋나면 첫 프레임에 튄다). 접지 보정(LimbDrop)도
        // 이 값을 쓰므로 클래스 레벨 상수로 둔다.
        private const float IdleLegSpreadDegrees = 12f;

        // 팔다리 2분절 길이(리더 지정 총 길이를 상/하로 나눈 값) — 팔 0.75 = 0.38 + 0.37,
        // 다리 0.95 = 0.50 + 0.45.
        private const float ArmUpperLength = 0.38f, ArmLowerLength = 0.37f;
        private const float LegUpperLength = 0.50f, LegLowerLength = 0.45f;

        // 중립(Idle) 무릎/팔꿈치 굽힘 각도 — StickConfig의 같은 이름 필드와 반드시 일치해야 한다
        // (프리팹 저장 자세와 런타임 포즈 목표각이 어긋나면 첫 프레임에 튄다). 완전히 편 0도로 두면
        // 사용자 지적대로 "막대기" 느낌이 나므로 항상 살짝 굽혀둔다.
        private const float IdleKneeBendDegrees = 4f;
        private const float IdleElbowBendDegrees = 10f;

        // 굽힘 방향 부호 — States/StickmanPoseAnimator.cs의 KneeBendSign/ElbowBendSign과 같은 규약
        // (무릎은 뒤로, 팔꿈치는 앞으로). 사람 관절은 반대로 꺾이지 않는다.
        private const float KneeBendSign = -1f;
        private const float ElbowBendSign = 1f;

        // RAGDOLL에서 무릎/팔꿈치 HingeJoint2D에 거는 각도 제한. 접히는 쪽으로는 이만큼까지 허용하고,
        // 반대(과신전) 쪽으로는 아주 약간의 여유만 준다 — 물리로 넘어간 뒤에도 관절이 사람처럼
        // 한 방향으로만 접히게 하기 위해서다. 능동 상태에서는 관절 자체가 비활성이라 무관하다.
        // [정직한 한계] HingeJoint2D의 각도 제한은 관절이 enable될 때의 상대 자세를 기준으로 해석되므로,
        // RAGDOLL 진입 시점의 포즈가 기준이 된다(항상 해부학적 0도가 기준인 것은 아니다). 그래도 진입
        // 자세에서 크게 벗어나는 과신전은 확실히 막힌다.
        private const float MaxJointBendDegrees = 100f;
        private const float JointHyperExtendMarginDegrees = 5f;

        /// <summary>
        /// 중립 자세에서 엉덩이부터 발끝까지의 수직 낙차. 대퇴는 hipAngle, 정강이는 hipAngle+무릎각의
        /// 누적 각도로 각각 기울어 있으므로 따로 계산해 더한다(접지 보정 footLift 산출용).
        /// </summary>
        private static float LimbDrop(float hipAngleDegrees)
        {
            float hip = hipAngleDegrees * Mathf.Deg2Rad;
            float knee = (hipAngleDegrees + KneeBendSign * IdleKneeBendDegrees) * Mathf.Deg2Rad;
            return LegUpperLength * Mathf.Cos(hip) + LegLowerLength * Mathf.Cos(knee);
        }

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
            // 근본 재구현(2026-08-28, States/RagdollRig.cs 클래스 문서 참고) — 루트(몸통) 회전 고정.
            // 기존 프리팹은 Rigidbody2D 5개 전부 m_Constraints: 0이었고, 그래서 몸통이 자유롭게 넘어질
            // 수 있었다(사용자가 여러 번 보고한 "바닥에 쓰러져 누운 채 팔다리가 제멋대로 뻗은" 모습의
            // 직접 원인). 능동 상태에서는 항상 이 제약이 걸려 있어야 하고, RAGDOLL 진입 시에만
            // RagdollRig.EnterRagdoll()이 런타임에 이 비트를 푼다. 프리팹 저장값 자체를 능동 모드
            // 기본값으로 둬서, 씬 로드 직후 첫 물리 스텝부터 절대 넘어지지 않게 한다.
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            var capsule = root.AddComponent<CapsuleCollider2D>();
            capsule.direction = CapsuleDirection2D.Vertical;
            // 크기는 아래 지오메트리 계산이 끝난 뒤 전신 높이에서 유도해 대입한다(totalHeight 참고).

            root.AddComponent<StickmanClickHitbox>();

            var agent = root.AddComponent<StickmanAgent>();
            var so = new SerializedObject(agent);
            so.FindProperty("_config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();

            // ================================================================================
            // 졸라맨 지오메트리 (2026-08-28 리더 지정 좌표, 사용자 스크린샷 판독 결과 반영)
            // ================================================================================
            // 리더가 지정한 루트 로컬 좌표(발끝 보정 전, 2026-08-28 3차 사용자 피드백 반영):
            //   머리 링 반경 0.22 (0.25에서 축소 — "머리가 몸에 비해 크다"), 몸통 바로 위에 얹는다
            //   몸통 (0, 1.35) -> (0, 0.45)             (길이 0.9)
            //   어깨 부착점 (0, 1.18)  — 좌우 팔이 **같은 점**에서 시작 (레퍼런스 /|\)
            //   엉덩이 부착점 (0, 0.45) — 좌우 다리가 **같은 점**에서 시작 (레퍼런스 / \)
            //   팔 길이 0.75 (0.5에서 연장), 중립 벌림 40도
            //   다리 길이 0.95 (0.6에서 연장), 중립 벌림 12도 (18도는 너무 벌어져 보였다)
            // 팔다리 연장은 사용자 지적 "팔 몸 다리 비율이 이상하고" 대응 — 머리 대비 팔다리가 짧아
            // 뭉툭해 보였다. 손/발 끝 점(EndMark)은 사용자 요청으로 **전부 제거**했다("손과 발에
            // 동그란 뭉치같은건 필요없을거 같은데") — 팔다리는 그냥 선으로 끝난다.
            //
            // 왜 좌우를 같은 x=0 점에 두는가: 어깨를 x=±0.05로 두었더니 팔이 거의 수직인 순간 몸통 선과
            // 완전히 겹쳐 **팔이 아예 안 보였다**(사용자 스크린샷). 레퍼런스 졸라맨은 팔다리가 몸통 위의
            // 한 점에서 갈라져 나오고 벌어짐은 전적으로 **각도**가 만든다. 부착점에서 선끼리 겹치는 것은
            // 정상이고 의도된 것이다(그게 "관절"이다).
            //
            // FootLift: 위 좌표를 그대로 쓰면 중립 자세의 발끝 y = 0.45 - 0.95*cos(12°) = -0.48로 루트
            // 원점보다 한참 아래로 내려간다. 그런데 이 프로젝트는 States/GroundSensor.cs / SnapToGround()가
            // "루트 원점 = 발 높이"를 전제로 접지/스냅을 계산한다(StickmanBlackboard.SenseGround 문서).
            // 그래서 실루엣(상대 거리/각도)은 리더 지정값 그대로 두고 **전체를 그 낙차만큼 위로 평행이동**해
            // 발끝이 정확히 루트 원점(=지면)에 닿게 한다. 하드코딩이 아니라 실제 다리 길이/각도에서
            // 유도하므로, 다리 길이나 중립 각도를 바꾸면 접지가 자동으로 따라온다.
            // 어깨 1.18 -> 1.28 (2026-08-28 리더 지시, 사용자 "캐릭터 목이 좀 긴 것 같음, 지금의 절반
            // 정도가 맞을 것 같은데"): "목"으로 보이는 것은 머리 아래 끝에서 어깨까지 드러난 몸통 선
            // 구간이다. 머리 중심 y는 항상 몸통 상단 + 머리 반경으로 유도되므로 **머리 아래 끝은 정확히
            // 몸통 상단(SpecTorsoTopY=1.35)**이고, 목 길이 = 1.35 - 어깨y. 즉 0.17 -> 0.07이 된다.
            // 머리/몸통 좌표 자체는 건드리지 않는다(그 비율은 이미 사용자 확인을 받았다).
            const float SpecHipY = 0.45f, SpecShoulderY = 1.28f, SpecTorsoTopY = 1.35f;
            // 중립 자세의 발끝 낙차 — 무릎이 살짝 굽어 있으므로 대퇴/정강이를 각자의 **누적 각도**로
            // 따로 계산해 더해야 정확하다. 무릎 굽힘 부호가 좌우 공통(사람 무릎은 둘 다 뒤로 접힌다)이라
            // 좌우 낙차가 아주 조금 달라지므로, 둘 중 **큰 쪽**을 기준으로 들어올려 어느 발도 지면 아래로
            // 내려가지 않게 한다(차이는 0.02유닛 미만이라 육안으로 구분되지 않는다).
            float leftDrop = LimbDrop(-IdleLegSpreadDegrees);
            float rightDrop = LimbDrop(IdleLegSpreadDegrees);
            float footLift = Mathf.Max(leftDrop, rightDrop) - SpecHipY;
            float hipY = SpecHipY + footLift;
            float shoulderY = SpecShoulderY + footLift;
            float torsoTopY = SpecTorsoTopY + footLift;
            float torsoBottomY = SpecHipY + footLift;
            // 머리는 몸통 꼭대기 바로 위에 얹는다(링 아래 끝이 몸통 상단과 만나도록) — 고정 상수를 따로
            // 두면 몸통 길이/머리 반경을 바꿀 때마다 목이 끊기거나 파묻히므로 항상 유도해서 쓴다.
            float headY = torsoTopY + HeadVisualRadius;
            // 루트 CapsuleCollider2D는 발끝(0)부터 머리 꼭대기까지 덮어야 RAGDOLL이 바닥에 자연스럽게
            // 눕는다 — 팔다리를 늘렸으므로 전신 높이에서 유도한다(예전 고정값 1.8은 새 비율과 어긋난다).
            float totalHeight = headY + HeadVisualRadius;

            capsule.size = new Vector2(0.4f, totalHeight);
            capsule.offset = new Vector2(0f, totalHeight * 0.5f);

            // 몸통 위쪽 끝을 머리 원 안으로 살짝 파고들게 한다(레퍼런스가 그렇다 — 목 부분에서 굵은
            // 획이 머리 덩어리와 자연스럽게 이어져 보인다). 파고드는 깊이는 머리 반경의 절반.
            float torsoTopOverlapped = torsoTopY + HeadVisualRadius * 0.5f;
            float torsoCenterY = (torsoTopOverlapped + torsoBottomY) * 0.5f;
            float torsoHalf = (torsoTopOverlapped - torsoBottomY) * 0.5f;
            CreateLineSegmentVisual(root.transform, "Torso", new Vector3(0f, torsoCenterY, 0f),
                new Vector3(0f, torsoHalf, 0f), new Vector3(0f, -torsoHalf, 0f), outline, sortingOrder: 1);

            // 머리 — **흰색으로 꽉 채운 원 + 검은 테두리**(2026-08-28 사용자 정정: "얼굴은 흰색에 눈이
            // 검은색이어야지"). 배경이 밝은 회색(backgroundFallbackColor)이라 흰 얼굴만으로는 배경과
            // 구분되지 않으므로 검은 테두리가 반드시 필요하다. 세 겹을 sortingOrder로 쌓는다:
            //   3: 흰색 채움(CreateFilledHead)  4: 검은 테두리 링(CreateRing)  5: 검은 눈동자 점
            // 물리 CircleCollider2D(반경 0.4, BUG-SW-M1 이후 무변경)는 채움 오브젝트("Head")에 붙인다 —
            // 이 오브젝트가 머리의 기준 Transform이라 StickmanPoseAnimator의 몸 바운스/EyeController의
            // 부모 노릇을 함께 한다.
            var head = CreateFilledHead(root.transform, "Head", new Vector3(0f, headY, 0f), HeadVisualRadius,
                Color.white, sortingOrder: 3);
            head.layer = limbLayer;
            var headCollider = head.AddComponent<CircleCollider2D>();
            headCollider.radius = 0.4f;
            CreateRing(head.transform, "HeadOutline", Vector3.zero, HeadVisualRadius, HeadOutlineWidth,
                outline, sortingOrder: 4);

            // 눈(눈동자 점 2개) — **반드시 머리의 자식**이라야 RAGDOLL로 머리가 뒹굴 때도 따라간다.
            // 흰 얼굴 위에 검은 점 두 개(사용자 정정 반영). sortingOrder는 테두리(4)보다 위(5).
            // 런타임에 States/EyeController.cs가 이 점들의 localPosition을 중립에서 조금씩 오프셋해
            // 시선을 움직인다(다음 라운드에 커서 추적 연결 예정 — 그 클래스 문서의 배선 지점 참고).
            CreateFilledDot(head.transform, "LeftEye", new Vector3(-EyeOffsetX, EyeOffsetY, 0f),
                EyePupilRadius, outline, sortingOrder: 5);
            CreateFilledDot(head.transform, "RightEye", new Vector3(EyeOffsetX, EyeOffsetY, 0f),
                EyePupilRadius, outline, sortingOrder: 5);

            // 팔다리 — 각각 2마디(위=대퇴/상완, 아래=정강이/전완). 아래 마디는 위 마디의 자식이라
            // 위 마디를 돌리면 딸려오고, 아래 마디를 추가로 돌리면 무릎/팔꿈치가 접힌다(CreateLimb 문서).
            // 중립 벌림/굽힘 각도는 LineRenderer를 비스듬히 그려서가 아니라 **transform.localRotation
            // 초기값**으로 준다 — 그래야 States/StickmanPoseAnimator.cs가 각도를 세팅할 때 이중으로
            // 더해지지 않는다.
            CreateLimb(root.transform, rb, "LeftLeg", attachLocal: new Vector2(0f, hipY),
                upperLength: LegUpperLength, lowerLength: LegLowerLength, width: LegLineWidth,
                upperAngle: -IdleLegSpreadDegrees, lowerAngle: KneeBendSign * IdleKneeBendDegrees,
                lowerMinAngle: -MaxJointBendDegrees, lowerMaxAngle: JointHyperExtendMarginDegrees,
                outline, mass: 0.09f, gravityScale: gravityScale, sortingOrder: 0, limbLayer: limbLayer, agent: agent);
            CreateLimb(root.transform, rb, "RightLeg", attachLocal: new Vector2(0f, hipY),
                upperLength: LegUpperLength, lowerLength: LegLowerLength, width: LegLineWidth,
                upperAngle: IdleLegSpreadDegrees, lowerAngle: KneeBendSign * IdleKneeBendDegrees,
                lowerMinAngle: -MaxJointBendDegrees, lowerMaxAngle: JointHyperExtendMarginDegrees,
                outline, mass: 0.09f, gravityScale: gravityScale, sortingOrder: 0, limbLayer: limbLayer, agent: agent);
            CreateLimb(root.transform, rb, "LeftArm", attachLocal: new Vector2(0f, shoulderY),
                upperLength: ArmUpperLength, lowerLength: ArmLowerLength, width: ArmLineWidth,
                upperAngle: -IdleArmSpreadDegrees, lowerAngle: ElbowBendSign * IdleElbowBendDegrees,
                lowerMinAngle: -JointHyperExtendMarginDegrees, lowerMaxAngle: MaxJointBendDegrees,
                outline, mass: 0.06f, gravityScale: gravityScale, sortingOrder: 2, limbLayer: limbLayer, agent: agent);
            CreateLimb(root.transform, rb, "RightArm", attachLocal: new Vector2(0f, shoulderY),
                upperLength: ArmUpperLength, lowerLength: ArmLowerLength, width: ArmLineWidth,
                upperAngle: IdleArmSpreadDegrees, lowerAngle: ElbowBendSign * IdleElbowBendDegrees,
                lowerMinAngle: -JointHyperExtendMarginDegrees, lowerMaxAngle: MaxJointBendDegrees,
                outline, mass: 0.06f, gravityScale: gravityScale, sortingOrder: 2, limbLayer: limbLayer, agent: agent);

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
        /// <summary>
        /// 채워진 검은 머리 - 레퍼런스(Alan Becker 계열)는 머리가 "속이 빈 동그라미"가 아니라 **꽉 찬
        /// 검은 덩어리**다. 길이 0인 선분에 지름만큼의 선 폭 + 둥근 캐을 주면 LineRenderer 하나로 완전히
        /// 채워진 원이 나온다(SpriteRenderer를 다시 들여오지 않고 "LineRenderer만 사용" 컨벤션 유지).
        /// </summary>
        private static GameObject CreateFilledHead(Transform parent, string name, Vector3 localPos, float radius,
            Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one;

            var lr = ConfigureLine(go, color, sortingOrder, loop: false);
            lr.startWidth = radius * 2f;
            lr.endWidth = radius * 2f;
            lr.numCapVertices = HeadCapVertices;
            lr.positionCount = 2;
            lr.SetPosition(0, Vector3.zero);
            lr.SetPosition(1, Vector3.zero);
            return go;
        }

        /// <summary>
        /// 속이 빈 원(링) — 지금은 흰 얼굴의 검은 테두리 전용이다. 반지름 radius의 원 경로를 width
        /// 두께의 선으로 그린다(선이 반지름보다 얇으므로 가운데가 뚫린 링이 된다 — 채워진 원을 만드는
        /// CreateFilledHead와는 정확히 이 점만 다르다).
        /// </summary>
        private static GameObject CreateRing(Transform parent, string name, Vector3 localPos, float radius,
            float width, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one;

            var lr = ConfigureLine(go, color, sortingOrder, loop: true);
            lr.startWidth = width;
            lr.endWidth = width;
            lr.positionCount = HeadRingSegments;
            for (int i = 0; i < HeadRingSegments; i++)
            {
                float angle = (i / (float)HeadRingSegments) * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }
            return go;
        }

        /// <summary>
        /// 채워진 작은 원(점) 하나. 반지름보다 두꺼운 선으로 원 경로를 그려 "속이 빈 원"이 아니라
        /// "채워진 점"으로 보이게 한다(SpriteRenderer를 재도입하지 않고 "LineRenderer만 사용" 컨벤션 유지).
        /// 손/발 끝 점과 눈동자가 이 하나를 공유한다.
        /// </summary>
        private static GameObject CreateFilledDot(Transform parent, string name, Vector3 localAt, float radius,
            Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localAt;
            go.transform.localScale = Vector3.one;

            var lr = ConfigureLine(go, color, sortingOrder, loop: true);
            lr.startWidth = radius * 2.4f; // 지름보다 넉넉히 두꺼워야 안쪽까지 완전히 채워진다.
            lr.endWidth = radius * 2.4f;
            lr.positionCount = FilledDotSegments;
            for (int i = 0; i < FilledDotSegments; i++)
            {
                float angle = (i / (float)FilledDotSegments) * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }
            return go;
        }

        /// <summary>
        /// 팔다리 하나를 **2마디**로 만든다(2026-08-28 사용자 "손이랑 다리가 다 그냥 막대기 같음" 대응).
        ///
        ///   위 마디(대퇴/상완)     : 부모=root, transform 원점 = 관절 부착점(attachLocal),
        ///                            선 (0,0) -> (0,-upperLength)
        ///   아래 마디(정강이/전완) : 부모=위 마디, 원점 = 무릎/팔꿈치 지점 (0,-upperLength),
        ///                            선 (0,0) -> (0,-lowerLength)
        ///
        /// 왜 원점을 관절에 두는가: 능동 상태에서는 HingeJoint2D가 꺼져 있고 StickmanPoseAnimator가
        /// transform.localRotation을 직접 세팅해 포즈를 만든다. transform 회전은 항상 그 transform의
        /// 원점을 중심으로 일어나므로, 원점이 마디 한가운데에 있으면 다리가 고관절이 아니라 허벅지
        /// 중간을 축으로 돌아 몸에서 떨어져 보인다. 원점 = 관절이면 그 사각지대가 기하학 레벨에서
        /// 사라지고, 물리 anchor도 (0,0)으로 단순해진다 — 시각/물리/회전축이 전부 하나의 값에서
        /// 파생되므로 서로 어긋나는 것 자체가 불가능하다.
        ///
        /// 아래 마디의 HingeJoint2D에는 각도 제한(useLimits)을 걸어 RAGDOLL에서도 관절이 사람처럼 한
        /// 방향으로만 접히게 한다(lowerMinAngle/lowerMaxAngle — MaxJointBendDegrees 문서의 한계 참고).
        /// </summary>
        private static void CreateLimb(Transform hierarchyParent, Rigidbody2D connectedBody, string name,
            Vector2 attachLocal, float upperLength, float lowerLength, float width,
            float upperAngle, float lowerAngle, float lowerMinAngle, float lowerMaxAngle,
            Color color, float mass, float gravityScale, int sortingOrder, int limbLayer, StickmanAgent agent)
        {
            GameObject upper = CreateLimbSegment(hierarchyParent, connectedBody, name, attachLocal, upperLength,
                width, upperAngle, useLimits: false, minAngle: 0f, maxAngle: 0f,
                color, mass, gravityScale, sortingOrder, limbLayer, agent);

            // 아래 마디는 위 마디의 자식이고, 그 관절 부착점은 위 마디 로컬 공간의 (0, -upperLength)
            // (= 무릎/팔꿈치). 위 마디의 Rigidbody2D에 연결한다.
            CreateLimbSegment(upper.transform, upper.GetComponent<Rigidbody2D>(), name + "Lower",
                new Vector2(0f, -upperLength), lowerLength, width, lowerAngle,
                useLimits: true, minAngle: lowerMinAngle, maxAngle: lowerMaxAngle,
                color, mass, gravityScale, sortingOrder, limbLayer, agent);
        }

        private static GameObject CreateLimbSegment(Transform hierarchyParent, Rigidbody2D connectedBody, string name,
            Vector2 attachLocal, float length, float width, float neutralAngleDegrees,
            bool useLimits, float minAngle, float maxAngle,
            Color color, float mass, float gravityScale, int sortingOrder, int limbLayer, StickmanAgent agent)
        {
            var segment = new GameObject(name);
            segment.transform.SetParent(hierarchyParent, false);
            segment.transform.localPosition = new Vector3(attachLocal.x, attachLocal.y, 0f); // 원점 = 관절.
            segment.transform.localScale = Vector3.one; // 조인트 anchor 계산이 스케일에 영향받지 않도록 유지.
            // 중립 각도는 여기(초기 localRotation)에만 준다 — LineRenderer는 항상 로컬 -y 방향으로 곧게
            // 그리고 각도는 오직 회전으로 표현한다. 선을 비스듬히 그린 뒤 회전까지 시키면 각도가 이중으로
            // 더해져 런타임 포즈와 프리팹 저장 자세가 어긋난다.
            segment.transform.localRotation = Quaternion.Euler(0f, 0f, neutralAngleDegrees);
            segment.layer = limbLayer; // BUG-SW-M1: 자체충돌은 레이어 매트릭스가 끄고, 바닥 등과는 정상 충돌.

            var rb = segment.AddComponent<Rigidbody2D>();
            // 근본 재구현(2026-08-28): 마디의 저장 기본값은 Kinematic이다 — 능동 상태(앱 시작 직후 포함)에서
            // 팔다리는 물리가 아니라 States/StickmanPoseAnimator.cs가 transform으로 직접 제어하기 때문이다.
            // RAGDOLL 진입 시에만 RagdollRig가 Dynamic으로 되돌린다. mass/damping/gravityScale은 그 RAGDOLL
            // 구간에서 그대로 유효하므로 값을 유지한다.
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.mass = mass;
            rb.gravityScale = gravityScale;
            // BUG-SW-M4: 감쇠 없는 팔다리가 이동 중 피격 RAGDOLL의 정착 실패 원인이었다.
            rb.linearDamping = LimbLinearDamping;
            rb.angularDamping = LimbAngularDamping;

            var joint = segment.AddComponent<HingeJoint2D>();
            joint.connectedBody = connectedBody;
            joint.autoConfigureConnectedAnchor = false; // 자동 재계산으로 인한 예측 불가 오차 방지.
            joint.anchor = Vector2.zero;                // 원점이 곧 관절이므로 정확히 (0,0).
            joint.connectedAnchor = attachLocal;        // 부모 로컬 공간의 같은 지점.
            joint.useMotor = false;
            if (useLimits)
            {
                joint.limits = new JointAngleLimits2D { min = minAngle, max = maxAngle };
                joint.useLimits = true;
            }
            // 능동 모드 기본값: 관절 비활성(RagdollRig가 RAGDOLL에서만 켠다). 마디가 Kinematic이어도
            // 살아있는 HingeJoint2D는 Dynamic인 쪽을 잡아당겨 절차적 포즈를 미세하게 흔들 수 있으므로
            // 컴포넌트 자체를 꺼둔다(RagdollRig.cs 클래스 문서 참고).
            joint.enabled = false;

            // BUG-SW-M1: 마디에 실제 Collider2D를 부여한다(콜라이더가 없으면 RagdollLimbImpactRelay가
            // 영구히 발동 불가능해지고 바닥과도 충돌할 수 없어 RAGDOLL이 절대 안착하지 못한다).
            // 원점이 관절이므로 박스도 그만큼 아래로 옮겨 시각(선)과 물리 형상을 일치시킨다.
            var collider = segment.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(width, length);
            collider.offset = new Vector2(0f, -length * 0.5f);

            // BUG-SW-M1: 사지 피격을 StickmanAgent.ReportExternalImpact()로 중계. Reset()/Awake() 기반
            // 자동 탐색에 의존하지 않고 SerializedObject 직접 대입으로 에디터 시점에 확실하게 배선한다.
            var relay = segment.AddComponent<RagdollLimbImpactRelay>();
            var relaySo = new SerializedObject(relay);
            relaySo.FindProperty("_agent").objectReferenceValue = agent;
            relaySo.ApplyModifiedPropertiesWithoutUndo();

            // 레퍼런스 스타일의 굵은 검은 획 — 관절(로컬 원점)에서 마디 끝까지. 시작점이 정확히 원점이라
            // 회전 중심과 선의 시작점이 항상 같고, 둥근 캡(LineCapVertices=8)이 관절에서 자연스럽게
            // 겹쳐 매끄럽게 이어진다.
            var lr = ConfigureLine(segment, color, sortingOrder, loop: false);
            lr.startWidth = width;
            lr.endWidth = width;
            lr.positionCount = 2;
            lr.SetPosition(0, Vector3.zero);
            lr.SetPosition(1, new Vector3(0f, -length, 0f));
            return segment;
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
