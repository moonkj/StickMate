using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Kirurobo;
using StickMate.Core;
using StickMate.Dialogue;
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
    ///
    /// ★ 2026-08-28 갱신(사용자 신고 "지금도 떠있는것처럼보임"): f가 0.2에서 "Dock 높이 비율"
    /// (NullPlatformWindowService.DockSafeBottomInsetPoints 75pt / ReferenceScreenHeightPoints 982pt
    /// ≈ 0.0764)로 내려갔다. f=0.2일 때는 딛을 창이 하나도 없으면 캐릭터가 화면 바닥에서 196pt나 위,
    /// 즉 화면 한가운데쯤에 서 있어 "허공에 떠 있는 것처럼" 보였다(그 신고의 직접 원인). 이제
    /// 안전망 발판의 상단이 실측 화면(1512x982) 기준 OS y=907 = Dock 바로 위가 된다.
    ///
    /// 현재 값(f≈0.0764, orthographicSize=12, cam.y=0) 기준:
    ///   groundTopWorldY = 0 - 12*(1 - 0.1527) = -10.167  (뷰포트 하단 -12에서 1.83유닛 위)
    /// 캐릭터 전신 높이(배율 1.0에서 발~정수리 약 2.27유닛, 기본 배율 0.5에서 약 1.14유닛 —
    /// BuildStickmanPrefab의 "크기 배율" 절 참고)를 얹어도 머리 상단이 뷰포트 상단(+12)까지 한참
    /// 남고(배율 1.0에서 -7.9), 발 아래 여백 1.83유닛은 프레이밍 테스트의 최소
    /// 요구치(0.5유닛)를 3.6배 상회한다 — 캐릭터가 작아지는 방향은 이 여백을 더 늘리므로 안전하다 — Tests/PlayMode의 StickmanOnScreenFramingTests.cs가 이를
    /// 매 실행마다 실측 검증한다(그 1.83유닛이 화면상 정확히 Dock 높이 75pt에 대응한다).
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
        // 선 두께 일괄 축소 배율(2026-08-28 사용자 피드백 "사이즈도 너무 커" 대응 라운드).
        // 카메라 orthographicSize를 5 -> 12로 키워 캐릭터가 화면에서 약 2.4배 작아졌는데(아래
        // OrthographicSize 상수 문서 참고), 선 두께를 그대로 두면 상대적으로 굵어져 작은 캐릭터가
        // 검은 뭉치처럼 뭉개져 보인다. 축소 후 화면상 획 두께가 약 2.5~3.0 포인트가 되도록 잡은 값이다
        // (리더 지시: "너무 얇으면 안 보이니 화면상 2~3px 정도는 유지"). 계산: 창 높이 846pt /
        // (2*12 유닛) = 35.25 pt/유닛 이므로 0.11*0.7*35.25 ~= 2.7pt.
        private const float LineWidthScale = 0.7f;

        // ★ 2026-08-29 크기 배율 도입 — 아래 값들은 전부 **배율 1.0 기준**이다(Baseline 접두사).
        // 실제로 쓰이는 값은 BuildStickmanPrefab이 StickConfig.characterScale을 곱해 만든 지역 변수다.
        private const float BaselineLineWidth = 0.11f * LineWidthScale;      // 기본 획 두께(몸통).
        private const float BaselineLegLineWidth = 0.12f * LineWidthScale;   // 다리는 기본보다 아주 약간 굵게.
        private const float BaselineArmLineWidth = 0.10f * LineWidthScale;   // 팔은 기본보다 아주 약간 얇게.
        private const int LineCapVertices = 8; // 끝/모서리를 확실히 둥글게(레퍼런스 스타일의 round cap).
        private const int HeadRingSegments = 24; // 머리 테두리 링 근사에 쓰는 선분 개수(24면 육안으로 매끈한 원).

        // 머리 테두리(검은 링) 두께 — 팔다리 획보다 약간 얇게 잡아 머리가 지나치게 두꺼워 보이지 않게
        // 한다(리더 지시: "팔다리 선 두께와 비슷하거나 약간 얇게"). 위 LineWidthScale이 함께 곱해진다.
        private const float BaselineHeadOutlineWidth = 0.09f * LineWidthScale;
        // 머리 시각 반경. 물리 CircleCollider2D.radius(0.4, 아래 참고)와는 별개 값 — 판정 크기는 무변경.
        // 머리는 이제 "검은 링(테두리)만, 안쪽은 완전히 비어 투명"이다(사용자 정정, 2026-08-28 —
        // "얼굴이 흰색이 아니고 색 자체가 없어야지"). 진짜 투명 창이 동작하게 되어 얼굴 안쪽으로
        // 바탕화면이 그대로 비쳐야 하므로, 예전의 흰 채움 원(CreateFilledHead)은 제거했다.
        private const float BaselineHeadVisualRadius = 0.22f;

// 눈동자 점(CreateFilledDot)이 쓰는 원 근사 세분화 수. 손/발 끝 점은 2026-08-28 사용자 요청으로
        // 완전히 제거했으므로("손과 발에 동그란 뭉치같은건 필요없을거 같은데") 이제 이 상수는 눈 전용이다.
        // 이 크기(반지름 0.018)에서는 8각형도 육안상 원으로 보인다 — 머리 링(24)만큼 세분화할 필요 없음.
        private const int FilledDotSegments = 8;


        // 눈(눈동자 점) — 2026-08-28 사용자 요청("나중에 마우스 위치에 따라 눈도 움직여야 해서 눈도
        // 있어야 하고"). 리더 지정 좌표: 머리 링 반경 0.22 기준 (±0.075, +0.02), 반경 0.018.
        // "눈도 너무 커서 이상함"(사용자) 대응으로 0.035 -> 0.018로 축소 — 머리 안에 작은 점 두 개가
        // 콕 찍힌 정도. 눈동자 이동 범위는 States/EyeController.cs의 MaxPupilOffset이 제한한다.
        private const float BaselineEyePupilRadius = 0.018f;
        private const float BaselineEyeOffsetX = 0.075f;
        private const float BaselineEyeOffsetY = 0.02f;

        // 중립(Idle) 팔 벌림 각도 — StickConfig.idleArmSpreadDegrees와 반드시 같은 값이어야 한다
        // (프리팹 저장 시점의 초기 localRotation과 런타임 포즈 목표각이 일치해야 첫 프레임에 튀지 않는다).
        private const float IdleArmSpreadDegrees = 40f;

        // 중립(Idle) 다리 벌림 각도 — StickConfig.idleLegSpreadDegrees와 반드시 같은 값이어야 한다
        // (프리팹 저장 자세와 런타임 포즈 목표각이 어긋나면 첫 프레임에 튄다). 접지 보정(LimbDrop)도
        // 이 값을 쓰므로 클래스 레벨 상수로 둔다.
        private const float IdleLegSpreadDegrees = 12f;

        // 팔다리 2분절 길이(리더 지정 총 길이를 상/하로 나눈 값) — 팔 0.75 = 0.38 + 0.37,
        // 다리 0.95 = 0.50 + 0.45.
        private const float BaselineArmUpperLength = 0.38f, BaselineArmLowerLength = 0.37f;
        private const float BaselineLegUpperLength = 0.50f, BaselineLegLowerLength = 0.45f;

        // 중립(Idle) 무릎/팔꿈치 굽힘 각도 — StickConfig의 같은 이름 필드와 반드시 일치해야 한다
        // (프리팹 저장 자세와 런타임 포즈 목표각이 어긋나면 첫 프레임에 튄다). 완전히 편 0도로 두면
        // 사용자 지적대로 "막대기" 느낌이 나므로 항상 살짝 굽혀둔다.
        private const float IdleKneeBendDegrees = 4f;
        private const float IdleElbowBendDegrees = 10f;

        // 굽힘 방향 부호 — States/StickmanPoseAnimator.cs의 KneeBendSign/ElbowBendSign과 같은 규약
        // (무릎은 뒤로, 팔꿈치는 앞으로). 사람 관절은 반대로 꺾이지 않는다.
        private const float KneeBendSign = -1f;
        private const float ElbowBendSign = 1f;

        // ================================================================================
        // RAGDOLL 관절 각도 제한 (2026-08-28 사용자 피드백 "떨어지면 이상하게 넘어짐" 대응)
        // ================================================================================
        // 사용자 스크린샷의 문제는 "누워 있는 것" 자체가 아니라 **팔다리가 사람이라면 불가능한 모양으로
        // 쭉 뻗어 있는 것**이었다(수평으로 일직선, 불가사리 같은 형태). 원인은 두 가지였다:
        //
        //   (1) 위 마디(대퇴/상완)에는 각도 제한이 **아예 없었다**(useLimits: false). 즉 RAGDOLL에서
        //       다리/팔이 고관절·어깨를 축으로 360도 자유 회전할 수 있었고, 그래서 몸통에 대해 완전히
        //       수직으로 뻗은 "대(大)자" 자세가 물리적으로 허용됐다.
        //   (2) 아래 마디(정강이/전완)의 제한이 "완전히 편 상태(0도)를 포함"하고 있었다
        //       (예전 팔꿈치 -5~+100). 사람의 무릎/팔꿈치는 힘이 빠져 늘어져 있어도 완전한 일직선이
        //       되지 않는다 — 0을 포함하니 막대기처럼 곧게 뻗은 그림이 나왔다.
        //
        // 그래서 (1) 위 마디에도 제한을 걸고, (2) 아래 마디의 제한 구간에서 0도를 **제외**한다
        // (항상 MinJointBendDegrees 이상 굽어 있음). 능동 상태에서는 관절 자체가 비활성이라 무관하며,
        // 여기 값들은 오직 RAGDOLL 구간의 모양만 결정한다.
        //
        // [이전 라운드의 '정직한 한계'는 이번에 해소했다] HingeJoint2D의 각도 제한은 관절이 enable될
        // 때의 상대 자세(referenceAngle)를 기준으로 재해석되므로, 여기 적힌 값을 그대로 두면 RAGDOLL
        // 진입 포즈에 따라 허용 범위가 통째로 밀린다(실측: 팔꿈치가 제한 -5~+100을 넘어 -59도까지 감).
        // 이제 States/RagdollRig.cs가 **관절을 켜는 순간** referenceAngle을 읽어 이 값들을 해부학적
        // 기준(= 마디의 localRotation 0도)으로 다시 환산해 넣는다. 즉 여기 숫자는 "마디를 완전히 편
        // 상태를 0도로 봤을 때의 허용 각도"라는 하나의 뜻만 갖는다.
        private const float MaxJointBendDegrees = 100f;

        // 무릎/팔꿈치가 최소한 이만큼은 항상 굽어 있게 한다(완전한 일직선 금지). 중립(Idle) 굽힘각
        // (무릎 4도 / 팔꿈치 10도)보다 반드시 작아야 한다 — 그렇지 않으면 서 있는 자세 자체가 제한
        // 밖이라 RAGDOLL에 들어가는 순간 관절이 튄다.
        private const float MinJointBendDegrees = 3f;

        // 고관절/어깨의 스윙 허용 범위(중립 0도 = 마디가 몸통 축과 나란한 상태 기준, ±).
        // 하한 조건: 보행 키포즈의 최대 각도(엉덩이 ±25도, 어깨 ±18도)와 Idle 벌림(다리 12도, 팔 40도)을
        // 모두 포함해야 한다 — 능동 포즈가 제한 밖이면 RAGDOLL 진입 프레임에 팔다리가 튄다.
        // 상한 조건: 90도(몸통에 완전히 수직)를 넘기지 않아야 "대자로 뻗은" 실루엣이 막힌다.
        private const float HipSwingLimitDegrees = 65f;
        private const float ShoulderSwingLimitDegrees = 75f;

        /// <summary>
        /// 중립 자세에서 엉덩이부터 발끝까지의 수직 낙차. 대퇴는 hipAngle, 정강이는 hipAngle+무릎각의
        /// 누적 각도로 각각 기울어 있으므로 따로 계산해 더한다(접지 보정 footLift 산출용).
        /// </summary>
        private static float LimbDrop(float hipAngleDegrees, float legUpperLength, float legLowerLength)
        {
            float hip = hipAngleDegrees * Mathf.Deg2Rad;
            float knee = (hipAngleDegrees + KneeBendSign * IdleKneeBendDegrees) * Mathf.Deg2Rad;
            return legUpperLength * Mathf.Cos(hip) + legLowerLength * Mathf.Cos(knee);
        }

        // BUG-SW-M1(Architect 결정, 2026-08-28) — 표준 Active Ragdoll 레이어 기법: 몸통/머리/팔다리를
        // 전부 이 레이어에 몰아넣고, 이 레이어끼리의 충돌만 Physics2D 매트릭스에서 끈다(EnsureStickmanLimbLayer 참고).
        private const string StickmanLimbLayerName = "StickmanLimb";

        // 클릭 잡기 영역(GrabArea, isTrigger) 치수 — 근거는 BuildStickmanPrefab의 해당 블록 주석 참고.
        private const float BaselineGrabAreaWidth = 0.8f;
        private const float BaselineGrabAreaVerticalPadding = 0.15f;

        /// <summary>
        /// Main Camera의 직교 크기. 5 -> 12 (2026-08-28 사용자 피드백: "사이즈도 너무 커", "창 위로
        /// 돌아다니고 해야 하는데 너무 크잖아").
        ///
        /// 계산 근거: 캐릭터 전신 높이는 지오메트리 상수에서 유도되어 **배율 1.0에서** 약 2.27
        /// 월드유닛이다(BuildStickmanPrefab의 totalHeight — 발끝 0에서 머리 꼭대기까지). 실측한
        /// Player 창 높이는 846 포인트이므로 화면상 캐릭터 높이 = 2.27 / (2 * orthographicSize) * 846.
        ///   orthographicSize=5  -> 192pt (기존, 너무 큼)
        ///   orthographicSize=12 -> 80pt  (목표 구간 70~90pt의 한가운데)  <- 채택
        /// macOS 제목표시줄(약 28pt)이나 Dock 아이콘(약 60pt) 위에 서 있는 게 자연스러운 크기다.
        ///
        /// ★ 2026-08-29 — 사용자가 그마저도 "절반 정도"를 요구해 StickConfig.characterScale(기본 0.5)이
        /// 도입됐다. **카메라는 건드리지 않고 프리팹만 줄인다**: 카메라를 키우면 BUG-SW-M2의 OS-px 필드
        /// 8종이 전부 의미가 달라져 재검토 의무가 딸려오지만, 프리팹만 줄이면 OS-px 값들의 유효 월드
        /// 크기가 그대로 보존되기 때문이다. 기본 배율에서 화면상 캐릭터 높이는 약 40pt다.
        ///
        /// ============================================================================
        /// BUG-SW-M2 함정 재확인(리더 명시 경고) — 이번에는 "조용히" 바꾸지 않는다
        /// ============================================================================
        /// 이 값은 Platform/ScreenCoordinateConverter.cs의 OS-px <-> 월드유닛 변환 비율에 곱연산으로
        /// 반영되므로, StickConfig의 px 단위 필드들의 "유효 월드 크기"가 5/12 -> 2.4배 넓어진다.
        /// 과거 5->20(4배) 변경이 접지 터널링을 유발해 되돌린 이력이 있다.
        ///
        /// 이번 변경이 안전하다고 판단한 근거(수치로 확인):
        ///   - groundSnapTolerance = 6 OS-px. 백킹 픽셀 기준 Screen.height=1692에서
        ///     월드 환산 = 6 * (2*12 / 1692) = 0.085 유닛. 캐릭터 전신(2.27유닛) 대비 3.7%로 여전히
        ///     매우 작다(변경 전에는 0.036유닛 = 1.6%). 즉 **허용 오차가 넓어지는 방향**이라
        ///     "발판을 뚫고 지나가는" 터널링은 오히려 덜 일어난다(BUG-SW-M2 때의 4배와 달리 2.4배이고,
        ///     절대값도 캐릭터 대비 4% 미만).
        ///   - 지면 Y(ComputeGroundTopWorldY)와 RAGDOLL 바닥(CreateGroundCollider)은 둘 다 카메라에서
        ///     유도되므로 자동으로 따라온다(고정 상수 없음).
        ///   - 캐릭터 프리팹의 월드 크기/질량/관절은 **전혀 건드리지 않는다** — 물리 거동이 그대로라
        ///     "안 넘어지고 걷는다"는 이미 검증된 동작이 보존된다(프리팹 축소 방식 대신 이 방식을 고른
        ///     가장 큰 이유).
        /// 그럼에도 실측 재검증은 필수다(90초+ 연속 실행, grounded 이탈/낙하 고착 0건 확인).
        /// </summary>
        private const float OrthographicSize = 12f;

        // ================================================================================
        // ★ 캐릭터 크기 배율 (2026-08-29 — 사용자 요구 "캐릭터 사이즈가 지금의 절반정도 되어야함
        //    추후 사이즈 조정가능해야하고"). 단일 소스: StickConfig.characterScale.
        // ================================================================================
        // 이 프리팹 빌더가 크기의 **유일한 생산자**다. 위 Baseline* 상수(= 배율 1.0에서 사용자 확인을
        // 받은 실루엣)에 배율을 곱해 몸통/팔다리 길이·머리 반경·눈·콜라이더·잡기 영역·선 두께를 전부
        // 만들고, 런타임은 그 결과를 실측해서 쓴다(Core/StickmanMetrics.cs / StickmanPoseAnimator /
        // StickmanAgent.TickVisualHalfWidth). 각도는 곱하지 않는다 — 각도는 크기 불변량이라 배율을
        // 곱하면 실루엣 자체가 뭉개진다(다리를 절반 길이로 줄이면서 벌림각까지 절반으로 줄이면
        // 캐릭터가 "작아지는" 게 아니라 "다른 캐릭터"가 된다).
        //
        // 배율을 곱하지 **않는** 것과 그 근거:
        //   · 관절 각도 제한 / 중립 벌림·굽힘 각도 : 위와 같은 이유(무차원량).
        //   · Rigidbody2D 질량(루트 1 / 다리 0.09 / 팔 0.06) : 중력은 가속도라 낙하 거동에 영향이 없고,
        //     질량을 줄이면 StickConfig.ragdollForceThreshold(충격량 기준)가 조용히 예민해진다.
        //   · 카메라 orthographicSize : 화면(=바탕화면)은 캐릭터와 함께 줄어들지 않는다. 이걸 건드리면
        //     BUG-SW-M2의 OS-px 필드 8종 재검토 의무가 통째로 딸려온다(위 그 문서 참고).

        /// <summary>실측한 Player 창 높이(포인트). 아래 "화면상 최소 크기" 하한들의 환산 기준이며,
        /// 다른 계산에는 쓰이지 않는다(정확한 창 높이는 런타임에만 알 수 있고, 하한 판정에는 이
        /// 근사치로 충분하다).</summary>
        private const float ReferenceWindowHeightPoints = 846f;

        /// <summary>월드 1유닛이 화면에서 몇 포인트인가 = 846 / (2*12) = 35.25.</summary>
        private const float PointsPerWorldUnit = ReferenceWindowHeightPoints / (2f * OrthographicSize);

        /// <summary>
        /// 획 두께의 **화면상 하한**(포인트). 캐릭터가 작아지면 선도 같이 얇아지는 것이 원칙적으로
        /// 맞지만, 선에는 크기와 무관한 절대 조건이 하나 있다 — "보여야 한다". 배율 1.0에서 획은
        /// 0.077유닛 = 약 2.7pt인데(리더 지시 "화면상 2~3pt는 유지"), 그대로 비례하면 배율 0.5에서
        /// 1.36pt가 되어 안티에일리어싱에 묻힌다. 그래서 비례로 줄이되 이 값에서 바닥을 받친다.
        /// 이 하한이 실제로 걸리기 시작하는 배율은 2.0/2.7 ≒ 0.74다.
        /// </summary>
        private const float MinStrokeScreenPoints = 2.0f;

        /// <summary>
        /// 클릭 잡기 영역 폭의 **화면상 하한**(포인트). 잡기 영역이 존재하는 이유 자체가 "마우스로
        /// 집을 수 있어야 한다"는 **사람 쪽 조건**이라 캐릭터가 작아져도 무한정 같이 작아지면 안 된다
        /// (배율 1.0에서 0.8유닛 = 약 28pt = "버튼만 한 표적", 배율 0.5 비례로는 14pt로 좁아진다).
        /// 세로는 항상 전신을 덮으므로 하한이 필요 없고, 가로만 받친다.
        /// </summary>
        private const float MinGrabAreaScreenPoints = 18f;

        /// <summary>화면상 최소 포인트 수를 월드 유닛으로 환산한다(하한 계산 전용).</summary>
        private static float ScreenPointsToWorld(float points) => points / PointsPerWorldUnit;

        /// <summary>
        /// 획 두께에 배율을 적용하되 화면상 최소 두께 아래로는 내려가지 않게 한다
        /// (<see cref="MinStrokeScreenPoints"/> 문서 참고).
        /// </summary>
        private static float ScaledStrokeWidth(float baselineWidth, float scale)
        {
            return Mathf.Max(baselineWidth * scale, ScreenPointsToWorld(MinStrokeScreenPoints));
        }

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

        /// <summary>
        /// ★ 캐릭터 크기 조정 전용 진입점(2026-08-29). StickConfig.characterScale을 바꾼 뒤 이걸 누르면
        /// 프리팹과 씬이 함께 다시 구워진다.
        ///
        /// 왜 프리팹만 다시 굽지 않는가: 씬의 라이벌 스틱맨은 프리팹 인스턴스가 아니라 **언팩된 사본**
        /// 이라(CreateRivalStickman 문서 참고) 프리팹만 갈면 플레이어만 작아지고 라이벌은 옛 크기로
        /// 남는다. 게다가 프리팹을 다시 저장하면 fileID가 재할당되어 Main.unity의 PrefabInstance
        /// 오버라이드가 고아가 된다(BUG-SW-M3). 그래서 크기 변경은 언제나 프리팹+씬 동시 재생성이다.
        /// </summary>
        [MenuItem("StickMate/Resize Stickman (characterScale 반영, 프리팹+씬 재생성)")]
        public static void ResizeStickmanMenuItem()
        {
            StickConfig config = AssetDatabase.LoadAssetAtPath<StickConfig>(ConfigAssetPath);
            float scale = config != null ? config.ResolveCharacterScale() : 1f;
            float height = StickConfig.BaselineCharacterTotalHeight * scale;

            if (!Application.isBatchMode && !EditorUtility.DisplayDialog(
                    "캐릭터 크기 재생성",
                    "StickConfig.characterScale = " + scale.ToString("F3") +
                    " (전신 높이 약 " + height.ToString("F3") + "유닛, 화면상 약 " +
                    (height * PointsPerWorldUnit).ToString("F0") + "pt)로\n" +
                    PrefabAssetPath + " 와 " + SceneAssetPath + " 을(를) 다시 굽습니다.\n\n" +
                    "Main.unity에 수동으로 추가한 내용은 이 작업으로 사라집니다. 계속할까요?",
                    "재생성", "취소"))
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

            // 색 프리셋(StickConfig.inkColor)을 반드시 ResolveInkColor()로 거쳐 읽는다 —
            // primaryOutlineColor를 직접 읽으면 흰색 프리셋이 무시된다(그 메서드 문서 참고).
            // 참고: 런타임에는 StickmanAgent.ApplyInkColorFromConfig()가 시작 시 한 번 더 일괄
            // 적용하므로, 프리팹을 다시 만들지 않고 에셋 값만 바꿔도 색이 바뀐다.
            Color outline = config != null ? config.ResolveInkColor() : Color.black;
            float gravityScale = config != null ? config.gravityScale : 3f;

            // ★ 크기 배율 — 아래 모든 지오메트리가 이 하나에서 파생된다(위 "캐릭터 크기 배율" 절 참고).
            float bodyScale = config != null ? config.ResolveCharacterScale() : 1f;
            float headVisualRadius = BaselineHeadVisualRadius * bodyScale;
            float headOutlineWidth = ScaledStrokeWidth(BaselineHeadOutlineWidth, bodyScale);
            float lineWidth = ScaledStrokeWidth(BaselineLineWidth, bodyScale);
            float legLineWidth = ScaledStrokeWidth(BaselineLegLineWidth, bodyScale);
            float armLineWidth = ScaledStrokeWidth(BaselineArmLineWidth, bodyScale);
            float armUpperLength = BaselineArmUpperLength * bodyScale;
            float armLowerLength = BaselineArmLowerLength * bodyScale;
            float legUpperLength = BaselineLegUpperLength * bodyScale;
            float legLowerLength = BaselineLegLowerLength * bodyScale;
            float eyePupilRadius = BaselineEyePupilRadius * bodyScale;
            float eyeOffsetX = BaselineEyeOffsetX * bodyScale;
            float eyeOffsetY = BaselineEyeOffsetY * bodyScale;
            // 잡기 영역: 세로 여백은 순수 비례, 가로는 "마우스로 집을 수 있는 최소 폭"에서 바닥을 받친다.
            float grabAreaWidth = Mathf.Max(BaselineGrabAreaWidth * bodyScale, ScreenPointsToWorld(MinGrabAreaScreenPoints));
            float grabAreaVerticalPadding = BaselineGrabAreaVerticalPadding * bodyScale;

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
            // RAGDOLL 전용 회전 감쇠(RootAngularDamping 문서 참고 — 능동 상태에서는 회전 자체가 잠겨 있어
            // 이 값이 개입할 여지가 없다).
            rb.angularDamping = RootAngularDamping;

            var capsule = root.AddComponent<CapsuleCollider2D>();
            capsule.direction = CapsuleDirection2D.Vertical;
            // 크기는 아래 지오메트리 계산이 끝난 뒤 전신 높이에서 유도해 대입한다(totalHeight 참고).

            var hitbox = root.AddComponent<StickmanClickHitbox>();

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
            // ★ 2026-08-29: 아래 세 수치는 **배율 1.0 기준**이며 곧바로 bodyScale이 곱해진다.
            const float BaselineSpecHipY = 0.45f, BaselineSpecShoulderY = 1.28f, BaselineSpecTorsoTopY = 1.35f;
            float SpecHipY = BaselineSpecHipY * bodyScale;
            float SpecShoulderY = BaselineSpecShoulderY * bodyScale;
            float SpecTorsoTopY = BaselineSpecTorsoTopY * bodyScale;
            // 중립 자세의 발끝 낙차 — 무릎이 살짝 굽어 있으므로 대퇴/정강이를 각자의 **누적 각도**로
            // 따로 계산해 더해야 정확하다. 무릎 굽힘 부호가 좌우 공통(사람 무릎은 둘 다 뒤로 접힌다)이라
            // 좌우 낙차가 아주 조금 달라지므로, 둘 중 **큰 쪽**을 기준으로 들어올려 어느 발도 지면 아래로
            // 내려가지 않게 한다(차이는 0.02유닛 미만이라 육안으로 구분되지 않는다).
            float leftDrop = LimbDrop(-IdleLegSpreadDegrees, legUpperLength, legLowerLength);
            float rightDrop = LimbDrop(IdleLegSpreadDegrees, legUpperLength, legLowerLength);
            float footLift = Mathf.Max(leftDrop, rightDrop) - SpecHipY;
            float hipY = SpecHipY + footLift;
            float shoulderY = SpecShoulderY + footLift;
            float torsoTopY = SpecTorsoTopY + footLift;
            float torsoBottomY = SpecHipY + footLift;
            // 머리는 몸통 꼭대기 바로 위에 얹는다(링 아래 끝이 몸통 상단과 만나도록) — 고정 상수를 따로
            // 두면 몸통 길이/머리 반경을 바꿀 때마다 목이 끊기거나 파묻히므로 항상 유도해서 쓴다.
            float headY = torsoTopY + headVisualRadius;
            // 루트 CapsuleCollider2D는 발끝(0)부터 머리 꼭대기까지 덮어야 RAGDOLL이 바닥에 자연스럽게
            // 눕는다 — 팔다리를 늘렸으므로 전신 높이에서 유도한다(예전 고정값 1.8은 새 비율과 어긋난다).
            float totalHeight = headY + headVisualRadius;

            // 루트 물리 캡슐 폭 0.4는 배율 1.0 기준값이라 함께 줄인다(팔다리와 같은 몸이므로).
            capsule.size = new Vector2(0.4f * bodyScale, totalHeight);
            capsule.offset = new Vector2(0f, totalHeight * 0.5f);

            Debug.Log($"[SceneBootstrapper] 캐릭터 크기 배율={bodyScale:F3} (StickConfig.characterScale) — " +
                      $"전신 높이={totalHeight:F4}유닛(배율 1.0 기준 {StickConfig.BaselineCharacterTotalHeight:F4}), " +
                      $"화면상 약 {(totalHeight * PointsPerWorldUnit):F0}pt, 머리 반경={headVisualRadius:F4}, " +
                      $"어깨 y={shoulderY:F4}, 획 두께={lineWidth:F4}유닛({(lineWidth * PointsPerWorldUnit):F1}pt), " +
                      $"잡기 영역 폭={grabAreaWidth:F3}유닛({(grabAreaWidth * PointsPerWorldUnit):F0}pt).");

            // ================================================================================
            // 클릭 잡기 영역(GrabArea) — 드래그&던지기 실배선 라운드(2026-08-28) 신설
            // ================================================================================
            // 왜 별도 콜라이더가 필요한가: 물리용 루트 캡슐은 폭 0.4유닛(화면상 약 14pt)이라 "손으로
            // 집기"에는 좁다. 그렇다고 물리 캡슐 자체를 넓히면 바닥/랙돌 거동이 바뀌어 이미 검증된
            // 물리를 흔들게 된다. 그래서 **isTrigger=true**인 별도 캡슐을 얹는다:
            //   - 트리거는 물리 충돌을 전혀 일으키지 않는다(바닥/랙돌 거동 무변경 보증).
            //   - 그러면서 Unity의 OnMouseDown 히트테스트와 UniWindowController의
            //     Physics2D.GetRayIntersection 히트테스트에는 **둘 다 잡힌다**
            //     (ProjectSettings/Physics2DSettings.asset의 m_QueriesHitTriggers=1 확인).
            // 크기 근거: 폭 BaselineGrabAreaWidth(배율 1.0에서 0.8유닛)는 카메라 orthographicSize=12, 창 높이 846pt 기준
            //   0.8 x 846/(2*12) = 약 28pt — 얇은 획(2.5~3pt) 대비 약 10배 넓은 "버튼만 한" 표적이다.
            //   세로는 전신을 덮고 위아래로 grabAreaVerticalPadding씩 여유를 준다(둘 다 배율이 곱해진다 —
            //   가로만 "마우스로 집을 수 있는 최소 폭"(MinGrabAreaScreenPoints)에서 바닥을 받친다).
            // 이보다 더 키우지 않는 이유: 캐릭터에서 멀리 떨어진 빈 공간까지 클릭을 잡으면 비침해
            //   원칙 2(그 외 영역 100% 관통)가 체감상 깨진다.
            var grabArea = root.AddComponent<CapsuleCollider2D>();
            grabArea.direction = CapsuleDirection2D.Vertical;
            grabArea.isTrigger = true;
            grabArea.size = new Vector2(grabAreaWidth, totalHeight + grabAreaVerticalPadding * 2f);
            grabArea.offset = new Vector2(0f, totalHeight * 0.5f);

            // ================================================================================
            // 캐릭터 실측 치수 조회 창구 (Core/StickmanMetrics.cs) — 2026-08-29 크기 배율 라운드
            // ================================================================================
            // 시각 레이어(말풍선/이모트/게이지/타이머 링)가 "머리 위 y+2.6" 같은 절대 상수 대신
            // "키의 n%"로 앵커를 잡을 수 있게 하는 단일 조회 경로다. 직렬화 필드가 없고 Awake()에서
            // 자기 계층(비-트리거 캡슐 / "Head" / "LeftArm" / "LeftLeg")을 **실측**하므로 배선이
            // 필요 없고, 이 빌더가 구운 값과 어긋날 수도 없다(굽힌 상수를 복사하지 않는다).
            // ★ 라이벌 스틱맨도 이 컴포넌트를 그대로 가져간다 — 라이벌 말풍선도 같은 API를 쓴다.
            root.AddComponent<StickmanMetrics>();

            // ================================================================================
            // Phase 3 상호작용 컨트롤러 배선 (드래그&던지기 / 로데오 커서)
            // ================================================================================
            // 이 두 컴포넌트의 로직은 Phase 3에 이미 완성돼 있었지만 **씬/프리팹 어디에도 배치되지
            // 않아** 실제로는 한 번도 동작한 적이 없었다(직전 라운드까지 프리팹의 스크립트는
            // StickmanAgent / StickmanClickHitbox / RagdollLimbImpactRelay 3종뿐이었다). 이번 라운드의
            // 핵심 수정이며, 여기서 코드로 배치해 --force 재현성을 유지한다.
            var dragThrow = root.AddComponent<DragThrowController>();
            var dragSo = new SerializedObject(dragThrow);
            dragSo.FindProperty("_player").objectReferenceValue = agent;
            dragSo.FindProperty("_hitbox").objectReferenceValue = hitbox;
            // 넉넉한 GrabArea를 히트박스 영역 기준으로 넘긴다(부분적 클릭관통 해제 15절의 영역 부기가
            // 실제 클릭 판정 영역과 일치하도록 — 물리 캡슐이 아니라 이쪽이 사용자가 실제로 누르는 영역이다).
            dragSo.FindProperty("_hitboxCollider").objectReferenceValue = grabArea;
            dragSo.ApplyModifiedPropertiesWithoutUndo();

            var rodeo = root.AddComponent<RodeoCursorWatcher>();
            var rodeoSo = new SerializedObject(rodeo);
            rodeoSo.FindProperty("_player").objectReferenceValue = agent;
            rodeoSo.FindProperty("_config").objectReferenceValue = config;
            rodeoSo.ApplyModifiedPropertiesWithoutUndo();

            // ================================================================================
            // Phase 3/4 스펙터클 배선 (격파 미니게임 / 그라피티) — 2026-08-29
            // ================================================================================
            // 위 DragThrowController/RodeoCursorWatcher와 **완전히 같은 유형의 누락**이었다: 두 기능의
            // 상태 머신/트리거/락 로직은 진작 완성돼 있었는데 Director가 씬 어디에도 배치되지 않았고,
            // 그 위에 이벤트를 구독해 실제로 그리는 렌더러조차 존재하지 않아 화면에는 한 픽셀도 나오지
            // 않았다. 여기서 Director 2개 + 이번 라운드에 신설한 렌더러 2개를 함께 배치한다.
            var battle = root.AddComponent<BattleMinigameDirector>();
            var battleSo = new SerializedObject(battle);
            battleSo.FindProperty("_player").objectReferenceValue = agent;
            battleSo.FindProperty("_hitbox").objectReferenceValue = hitbox;
            // 드래그&던지기와 같은 이유로 물리 캡슐이 아니라 넉넉한 GrabArea를 넘긴다(부분적 클릭관통
            // 해제 영역 부기가 사용자가 실제로 누르는 영역과 일치해야 한다).
            battleSo.FindProperty("_hitboxCollider").objectReferenceValue = grabArea;
            battleSo.FindProperty("_config").objectReferenceValue = config;
            battleSo.ApplyModifiedPropertiesWithoutUndo();

            // 소환 판자/기 모으기 게이지/파편을 그리는 시각 레이어. 직렬화 필드가 없고 Awake()에서 같은
            // GameObject의 StickmanAgent/StickmanClickHitbox를 직접 찾으므로 배선이 필요 없다
            // (AppControlDirector와 동일한 관례).
            root.AddComponent<BattleMinigameRenderer>();

            var graffiti = root.AddComponent<GraffitiDirector>();
            var graffitiSo = new SerializedObject(graffiti);
            graffitiSo.FindProperty("_player").objectReferenceValue = agent;
            graffitiSo.FindProperty("_config").objectReferenceValue = config;
            graffitiSo.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<GraffitiRenderer>();

            // ================================================================================
            // Phase 4 시각 레이어 배선 (창 도둑 / 창 크래시 / PC 하드웨어 반응) — 2026-08-29
            // ================================================================================
            // 격파/그라피티와 **완전히 같은 유형의 누락**이 3건 더 있었다: 세 기능의 Director/State
            // 로직은 Phase 4에 이미 완성돼 있었지만 Director가 씬 어디에도 배치되지 않았고(따라서
            // Update()가 한 번도 돌지 않아 트리거 추첨조차 일어나지 않았다), 그 위에
            // WindowTheftOverlayChanged / WindowCrashOverlayChanged / HardwareReactionChanged를
            // 구독하는 코드가 0건이라 화면에는 한 픽셀도 나오지 않았다. 이번 라운드에 신설한 렌더러
            // 3종과 함께 여기서 배치한다.
            //
            // SpectacleEventLock 참여 여부는 원 설계를 그대로 따른다 — 창 도둑/창 크래시는 ChangeState()로
            // 단일 상태 슬롯을 다투므로 참여(각 Director가 이미 TryAcquire/Release를 구현), 하드웨어 반응은
            // 상태 전이를 하지 않는 머리 위 이모트라 의도적으로 비참여(Phase 4 설계 결정 5).
            var windowTheft = root.AddComponent<WindowTheftDirector>();
            var theftSo = new SerializedObject(windowTheft);
            theftSo.FindProperty("_player").objectReferenceValue = agent;
            // 27-1 대상 창 선정("캐릭터 신장의 3배 이하 폭")에 쓰는 신장 측정용 콜라이더.
            // 클릭 표적이 아니라 **몸 크기**를 재는 용도이므로, 넉넉한 GrabArea가 아니라 실제 물리
            // 캡슐을 넘긴다(DragThrow/Battle이 GrabArea를 넘기는 것과 목적이 다르다).
            theftSo.FindProperty("_characterCollider").objectReferenceValue = capsule;
            theftSo.FindProperty("_config").objectReferenceValue = config;
            theftSo.ApplyModifiedPropertiesWithoutUndo();

            // 진짜 창 위에 겹쳐 그리는 "복사본(고스트) 창" + 힘줄/먼지. 직렬화 필드가 없고 Awake()에서
            // 같은 GameObject의 StickmanAgent를 직접 찾으므로 배선이 필요 없다(GraffitiRenderer와 동일 관례).
            root.AddComponent<WindowTheftRenderer>();

            var windowCrash = root.AddComponent<WindowCrashDirector>();
            var crashSo = new SerializedObject(windowCrash);
            crashSo.FindProperty("_player").objectReferenceValue = agent;
            crashSo.FindProperty("_config").objectReferenceValue = config;
            crashSo.ApplyModifiedPropertiesWithoutUndo();

            // 가짜 균열 오버레이. **콜라이더를 단 하나도 만들지 않는다** — 27-4가 못박은 "보기엔 깨진
            // 유리, 만지면 평범한 창"(3초 내내 100% 클릭관통)을 구조적으로 보장하는 지점이다.
            root.AddComponent<WindowCrashRenderer>();

            var hardware = root.AddComponent<HardwareReactionDirector>();
            var hardwareSo = new SerializedObject(hardware);
            hardwareSo.FindProperty("_player").objectReferenceValue = agent;
            hardwareSo.FindProperty("_config").objectReferenceValue = config;
            hardwareSo.ApplyModifiedPropertiesWithoutUndo();

            // 배터리/CPU/네트워크/충전 은유를 머리 위 작은 이모트로 그리는 시각 레이어(23절).
            root.AddComponent<HardwareReactionRenderer>();

            // ================================================================================
            // Phase 5 시각 레이어 배선 (스트레스 게이지 / 가출 / 투두 / 포모도로 감시자) — 2026-08-29
            // ================================================================================
            // 리더 전수 감사가 확정한 목록 그대로다. Phase 5의 Director/State 로직(트리거 판정,
            // 5페이즈 가출 진행, 포모도로 에스컬레이션, 포스트잇 데이터 모델)은 전부 완성돼 있었지만
            //   · Director 5개가 씬 어디에도 배치되지 않아 Update()가 단 한 번도 돌지 않았고,
            //   · StressLevelChanged / RunawayLifecycleChanged / RunawayHintPulseRequested /
            //     FocusWatchTierChanged 4개 이벤트의 구독자가 프로젝트 전체에 0건이었으며,
            //   · Core.TodoListModel.Add()를 호출하는 코드조차 0건이라 투두 기능 전체가 도달 불가능이었다
            //     (목록이 영원히 비어 있으니 포스트잇은 "빈 상태 예외"로 항상 숨겨졌다).
            // 이번 라운드에 신설한 렌더러 4종과 함께 여기서 배치한다.
            //
            // SpectacleEventLock 참여 여부는 Phase 5 설계 결정 1을 그대로 따른다 — 기준은
            // "ChangeState()를 직접 호출해 단일 상태 슬롯을 다투는가"다. TodoReminder/FocusPose/Sulky/
            // Runaway는 전부 ChangeState를 호출하므로 참여(각 Director가 이미 TryAcquire/Release를
            // 구현), StressGauge 자체는 값 보관 + 이벤트 발행만 하므로 비참여(HardwareReactionDirector와
            // 정확히 같은 논리). 이번 라운드에서 이 판단을 바꾸지 않았다.
            var stress = root.AddComponent<StressGaugeDirector>();
            var stressSo = new SerializedObject(stress);
            stressSo.FindProperty("_player").objectReferenceValue = agent;
            // "장시간 방치" 판정을 리셋하는 상호작용 신호로 캐릭터 클릭을 구독한다 — 클릭 **표적**이
            // 아니라 클릭 **사실**만 쓰므로 GrabArea가 아니라 히트박스 컴포넌트 자체를 넘긴다.
            stressSo.FindProperty("_hitbox").objectReferenceValue = hitbox;
            stressSo.FindProperty("_config").objectReferenceValue = config;
            stressSo.ApplyModifiedPropertiesWithoutUndo();

            // 어깨 처짐 + 한숨 퍼프(19절 "상시" 채널). 직렬화 필드가 없고 Awake()에서 같은 GameObject의
            // StickmanAgent를 직접 찾으므로 배선이 필요 없다(다른 렌더러들과 동일 관례).
            root.AddComponent<StressGaugeRenderer>();

            var runaway = root.AddComponent<RunawayDirector>();
            var runawaySo = new SerializedObject(runaway);
            runawaySo.FindProperty("_player").objectReferenceValue = agent;
            // 20절의 "찾기"는 신규 입력 경로를 만들지 않고 기존 캐릭터 히트박스를 그대로 쓴다.
            runawaySo.FindProperty("_hitbox").objectReferenceValue = hitbox;
            runawaySo.FindProperty("_config").objectReferenceValue = config;
            runawaySo.ApplyModifiedPropertiesWithoutUndo();

            // 속도선/먼지/은신처 힌트 파문/발견 폭발/[간식 주기] 과자. 과자만 클릭 대상이며 그 콜라이더는
            // StickmanClickHitbox.RegisterExtraCollider로 등록된다(BattleMinigameRenderer와 동일 경로).
            root.AddComponent<RunawayRenderer>();

            var todoReminder = root.AddComponent<TodoReminderDirector>();
            var todoSo = new SerializedObject(todoReminder);
            todoSo.FindProperty("_player").objectReferenceValue = agent;
            todoSo.FindProperty("_config").objectReferenceValue = config;
            todoSo.ApplyModifiedPropertiesWithoutUndo();

            // 손에 든 종이(17절 "들고 다니는 모드"). 할일 **텍스트**는 말풍선이 그린다(원칙 1 —
            // 같은 문자열의 소스를 두 벌 만들지 않는다).
            root.AddComponent<TodoReminderRenderer>();

            // 포스트잇 카드(17절 "포스트잇 모드"). 이 위젯만은 [SerializeField] _config를 갖고 있어
            // 배선이 필요하다(Awake()에서 StickmanAgent.Config 폴백도 하지만, 씬 에셋에 값이 구워져
            // 있어야 에디터에서 열었을 때 혼란이 없다 — 라이벌 _config null 사고와 같은 교훈).
            var postIt = root.AddComponent<TodoPostItWidget>();
            var postItSo = new SerializedObject(postIt);
            postItSo.FindProperty("_config").objectReferenceValue = config;
            postItSo.ApplyModifiedPropertiesWithoutUndo();

            var focusWatch = root.AddComponent<FocusWatchDirector>();
            var focusSo = new SerializedObject(focusWatch);
            focusSo.FindProperty("_player").objectReferenceValue = agent;
            focusSo.FindProperty("_config").objectReferenceValue = config;
            focusSo.ApplyModifiedPropertiesWithoutUndo();

            // 발밑 타이머 링 + 1/3단계 경고 연출(18절). 같은 GameObject의 FocusWatchDirector에서 남은
            // 시간을 읽으므로 배선이 필요 없다.
            root.AddComponent<FocusWatchRenderer>();

            // ================================================================================
            // 앱 제어 수단 배선 (2026-08-28 — "터미널 없이 끌 수 있어야 한다")
            // ================================================================================
            // Interaction/AppControlDirector.cs: 전역 단축키(Ctrl+Opt+Cmd+Q 종료 등)와 캐릭터 우클릭
            // 제어 메뉴. 직렬화 필드가 없고 Awake()에서 같은 GameObject의 StickmanAgent를 직접
            // 찾으므로(없으면 씬 전체에서 탐색) SerializedObject 배선이 필요 없다.
            root.AddComponent<AppControlDirector>();

            // 몸통(목) 선의 위쪽 끝 — **머리 링 안쪽으로 침범하지 않게** 정확히 맞춘다
            // (2026-08-28 사용자 지적: "목이 얼굴을 뚫고 올라와있는거 같고").
            //
            // 이력: 직전까지는 torsoTopY + headVisualRadius*0.5로 머리 원 **안쪽 깊숙이** 파고들게 했다.
            // 그때는 얼굴 안쪽이 흰색으로 꽉 채워져 있어서(sortingOrder 3) 파고든 부분이 가려져 보이지
            // 않았다. 이번 라운드에 얼굴을 투명하게 비우면서 그 선이 머리 안에서 그대로 드러났다.
            //
            // 계산: 머리 링은 반지름 headVisualRadius 원 경로를 headOutlineWidth 두께로 그리므로,
            // 링이 차지하는 반경 구간은 [R - W/2, R + W/2]다. 즉 링의 **안쪽 가장자리**는 머리 중심에서
            // R - W/2 만큼 떨어진 곳 = torsoTopY + headOutlineWidth/2 (torsoTopY = headY - R 이므로).
            // 몸통 선은 둥근 캡 때문에 끝점보다 lineWidth/2 만큼 더 위로 뻗으므로, 그 시각적 끝이 링
            // 안쪽 가장자리에 정확히 닿으려면:
            //     끝점 + lineWidth/2 = torsoTopY + headOutlineWidth/2
            //  => 끝점 = torsoTopY + (headOutlineWidth - lineWidth)/2
            // 이러면 (a) 링 안쪽 빈 공간으로는 1px도 침범하지 않고, (b) 몸통 획이 링 두께 구간을 완전히
            // 가로질러 겹치므로 목과 머리 사이에 틈도 생기지 않는다.
            float torsoTopOverlapped = torsoTopY + (headOutlineWidth - lineWidth) * 0.5f;
            float torsoCenterY = (torsoTopOverlapped + torsoBottomY) * 0.5f;
            float torsoHalf = (torsoTopOverlapped - torsoBottomY) * 0.5f;
            CreateLineSegmentVisual(root.transform, "Torso", new Vector3(0f, torsoCenterY, 0f),
                new Vector3(0f, torsoHalf, 0f), new Vector3(0f, -torsoHalf, 0f), lineWidth, outline, sortingOrder: 1);

            // 머리 — **검은 링(테두리)만 + 안쪽은 완전히 비어 투명**(2026-08-28 사용자 정정: "얼굴이
            // 흰색이 아니고 색 자체가 없어야지, 비워져있어야함").
            //
            // 이력: 직전 라운드까지는 "흰색으로 꽉 채운 원 + 검은 테두리"였는데, 그건 **불투명한 밝은
            // 회색 배경을 전제로 한 설계**였다(흰 얼굴이 회색 배경과 구분되도록). 이번 라운드에
            // UniWindowController로 진짜 투명 창이 실제 동작하게 되었으므로(사용자 실측 확인 — 바탕화면과
            // Dock이 그대로 비쳐 보임), 얼굴 안쪽도 아무것도 그리지 않아 바탕화면이 그대로 비치게 한다.
            // 흰 채움을 만들던 CreateFilledHead()는 LineRenderer가 없는 순수 앵커 CreateHeadAnchor()로
            // 대체했다.
            //
            // 두 겹을 sortingOrder로 쌓는다:  4: 검은 테두리 링(CreateRing)   5: 검은 눈동자 점
            // 물리 CircleCollider2D(반경 0.4, BUG-SW-M1 이후 무변경)는 앵커 오브젝트("Head")에 붙인다 —
            // 이 오브젝트가 머리의 기준 Transform이라 StickmanPoseAnimator의 몸 바운스(이름 "Head"로
            // 탐색)/EyeController의 부모 노릇을 함께 한다. 렌더러가 사라져도 이 역할은 그대로다.
            var head = CreateHeadAnchor(root.transform, "Head", new Vector3(0f, headY, 0f));
            head.layer = limbLayer;
            var headCollider = head.AddComponent<CircleCollider2D>();
            // 머리 물리 원(반경 0.4, 시각 링 0.22와 별개 — BUG-SW-M1 이후 비율 무변경)도 함께 줄인다.
            headCollider.radius = 0.4f * bodyScale;
            CreateRing(head.transform, "HeadOutline", Vector3.zero, headVisualRadius, headOutlineWidth,
                outline, sortingOrder: 4);

            // 눈(눈동자 점 2개) — **반드시 머리의 자식**이라야 RAGDOLL로 머리가 뒹굴 때도 따라간다.
            // 투명한(비어 있는) 얼굴 안에 검은 점 두 개가 떠 있는 형태. sortingOrder는 테두리(4)보다 위(5).
            // 런타임에 States/EyeController.cs가 이 점들의 localPosition을 중립에서 조금씩 오프셋해
            // 시선을 움직인다(다음 라운드에 커서 추적 연결 예정 — 그 클래스 문서의 배선 지점 참고).
            CreateFilledDot(head.transform, "LeftEye", new Vector3(-eyeOffsetX, eyeOffsetY, 0f),
                eyePupilRadius, outline, sortingOrder: 5);
            CreateFilledDot(head.transform, "RightEye", new Vector3(eyeOffsetX, eyeOffsetY, 0f),
                eyePupilRadius, outline, sortingOrder: 5);

            // ================================================================================
            // 말풍선 렌더러 배선 (2026-08-29 — 원칙 1의 산출물이 화면에 한 번도 안 나오던 문제)
            // ================================================================================
            // Dialogue/DialogueIntent 파이프라인은 여러 라운드에 걸쳐 정교하게 만들어졌지만
            // StickmanEventBus.DialogueRequested를 **구독하는 코드가 어디에도 없어서** 대사가 생성되고
            // 만료되기만 할 뿐 아무도 볼 수 없었다(DragThrowController/RodeoCursorWatcher가 "로직은 있는데
            // 씬에 배치가 안 됨" 상태였던 것과 정확히 같은 유형의 누락). 여기서 프리팹에 배치해
            // --force 재현성을 유지한다. 앵커는 머리 오브젝트 — RAGDOLL로 머리가 뒹굴어도 말풍선이
            // 정확히 머리를 따라간다.
            var bubble = root.AddComponent<DialogueBubbleRenderer>();
            var bubbleSo = new SerializedObject(bubble);
            bubbleSo.FindProperty("_agent").objectReferenceValue = agent;
            bubbleSo.FindProperty("_anchor").objectReferenceValue = head.transform;
            bubbleSo.FindProperty("_config").objectReferenceValue = config;
            bubbleSo.ApplyModifiedPropertiesWithoutUndo();

            // 팔다리 — 각각 2마디(위=대퇴/상완, 아래=정강이/전완). 아래 마디는 위 마디의 자식이라
            // 위 마디를 돌리면 딸려오고, 아래 마디를 추가로 돌리면 무릎/팔꿈치가 접힌다(CreateLimb 문서).
            // 중립 벌림/굽힘 각도는 LineRenderer를 비스듬히 그려서가 아니라 **transform.localRotation
            // 초기값**으로 준다 — 그래야 States/StickmanPoseAnimator.cs가 각도를 세팅할 때 이중으로
            // 더해지지 않는다.
            // 무릎은 뒤로만(KneeBendSign=-1) 접히므로 허용 구간이 [-100, -3], 팔꿈치는 앞으로만
            // (ElbowBendSign=+1) 접히므로 [+3, +100] — 두 구간 모두 0(완전히 편 상태)을 포함하지 않는다.
            CreateLimb(root.transform, rb, "LeftLeg", attachLocal: new Vector2(0f, hipY),
                upperLength: legUpperLength, lowerLength: legLowerLength, width: legLineWidth,
                upperAngle: -IdleLegSpreadDegrees, lowerAngle: KneeBendSign * IdleKneeBendDegrees,
                upperMinAngle: -HipSwingLimitDegrees, upperMaxAngle: HipSwingLimitDegrees,
                lowerMinAngle: -MaxJointBendDegrees, lowerMaxAngle: KneeBendSign * MinJointBendDegrees,
                outline, mass: 0.09f, gravityScale: gravityScale, sortingOrder: 0, limbLayer: limbLayer, agent: agent);
            CreateLimb(root.transform, rb, "RightLeg", attachLocal: new Vector2(0f, hipY),
                upperLength: legUpperLength, lowerLength: legLowerLength, width: legLineWidth,
                upperAngle: IdleLegSpreadDegrees, lowerAngle: KneeBendSign * IdleKneeBendDegrees,
                upperMinAngle: -HipSwingLimitDegrees, upperMaxAngle: HipSwingLimitDegrees,
                lowerMinAngle: -MaxJointBendDegrees, lowerMaxAngle: KneeBendSign * MinJointBendDegrees,
                outline, mass: 0.09f, gravityScale: gravityScale, sortingOrder: 0, limbLayer: limbLayer, agent: agent);
            CreateLimb(root.transform, rb, "LeftArm", attachLocal: new Vector2(0f, shoulderY),
                upperLength: armUpperLength, lowerLength: armLowerLength, width: armLineWidth,
                upperAngle: -IdleArmSpreadDegrees, lowerAngle: ElbowBendSign * IdleElbowBendDegrees,
                upperMinAngle: -ShoulderSwingLimitDegrees, upperMaxAngle: ShoulderSwingLimitDegrees,
                lowerMinAngle: ElbowBendSign * MinJointBendDegrees, lowerMaxAngle: MaxJointBendDegrees,
                outline, mass: 0.06f, gravityScale: gravityScale, sortingOrder: 2, limbLayer: limbLayer, agent: agent);
            CreateLimb(root.transform, rb, "RightArm", attachLocal: new Vector2(0f, shoulderY),
                upperLength: armUpperLength, lowerLength: armLowerLength, width: armLineWidth,
                upperAngle: IdleArmSpreadDegrees, lowerAngle: ElbowBendSign * IdleElbowBendDegrees,
                upperMinAngle: -ShoulderSwingLimitDegrees, upperMaxAngle: ShoulderSwingLimitDegrees,
                lowerMinAngle: ElbowBendSign * MinJointBendDegrees, lowerMaxAngle: MaxJointBendDegrees,
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

            // ★ 2026-08-29 실측으로 확인한 함정 — NewScene 이후 config 참조가 죽어 있을 수 있다.
            // EditorSceneManager.NewScene(Single)은 직전 씬을 파괴하면서 참조가 끊긴 에셋을 언로드한다.
            // 그러면 여기까지 인자로 들고 온 StickConfig의 네이티브 객체가 사라져, C# 참조는 남아 있어도
            // UnityEngine.Object의 "가짜 null" 상태가 된다. 실제로 이 라운드에서 라이벌 컴포넌트 2개의
            // _config가 조용히 null로 직렬화되어(씬 YAML에 fileID: 0) **라이벌이 영원히 스폰되지 않는**
            // 버그가 났다 — RivalEncounterDirector.Update()가 `_config == null`이면 즉시 return하기 때문.
            // 증상이 조용하다(에러도 경고도 없다)는 점이 이 함정의 가장 나쁜 부분이라, 여기서 한 번
            // 되살려 아래 모든 배선이 같은 인스턴스를 쓰게 만든다.
            if (config == null)
            {
                config = AssetDatabase.LoadAssetAtPath<StickConfig>(ConfigAssetPath);
                Debug.Log("[SceneBootstrapper] NewScene 이후 StickConfig 참조가 언로드되어 " + ConfigAssetPath +
                          "에서 다시 로드했습니다" + (config != null ? " (성공)." : " (실패 — 배선이 비어 있을 수 있습니다)."));
            }

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = OrthographicSize;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            // 계단 현상(알파 앤티에일리어싱) 제거 — 투명 창에서는 프레임버퍼 알파가 그대로 창 투명도가
            // 되므로, MSAA가 꺼져 있으면 캐릭터 윤곽선의 알파가 0/1로만 나와 들쭉날쭉해 보인다
            // (사용자 실측 지적: "캐릭터 주변으로 픽셀이 깨져보이는데"). 프로젝트 전역 MSAA 값은
            // Assets/Editor/BuildStandalone.cs의 ConfigureAntiAliasing()이 빌드마다 멱등적으로 강제하고,
            // 카메라 쪽 스위치는 여기서 명시적으로 켠다(기본값이 true지만 의존하지 않는다).
            cam.allowMSAA = true;
            cam.allowHDR = false; // 알파 채널 보존 — HDR 버퍼는 투명 합성에서 알파를 잃을 수 있다.
            cam.clearFlags = CameraClearFlags.SolidColor;
            // 진짜 투명 오버레이 재활성화(UniWindowController 도입 라운드, 2026-08-28).
            //
            // 왜 알파 0인가: UniWindowController가 창을 투명하게 만들어도, 카메라가 알파 1(불투명)로
            // 화면을 클리어하면 렌더 결과 자체가 불투명이라 창은 여전히 회색 사각형으로 보인다. 즉
            // "창 투명화(네이티브)"와 "렌더 결과의 알파 0(Unity)"은 반드시 짝을 이뤄야 한다.
            //
            // 왜 RGB는 검정이 아니라 밝은 회색인가(이전 라운드에서 확립된 방어책, 반드시 유지):
            // 자체 플러그인 시절 알파를 0으로 두고 RGB도 (0,0,0)이면, 투명화가 실패했을 때 화면이
            // "완전히 새까만 창"이 되어 검정 캐릭터 선이 검정 배경에 묻혀 아무것도 안 보이는 사고가
            // 반복됐다. RGB를 StickConfig.backgroundFallbackColor(밝은 회색)로 유지하면 투명화가
            // 실패하더라도 최악의 결과가 "밝은 회색 창 안의 검정 캐릭터"(= 최소한 보이는 상태)가 된다.
            // 그래서 UniWindowController 프리팹의 autoSwitchCameraBackground도 false로 꺼둔다 —
            // 그 기능이 켜져 있으면 라이브러리가 투명화 시점에 배경을 Color.clear(=RGB 0,0,0 + 알파 0)로
            // 덮어써 이 방어책을 무력화한다(ConfigureUniWindowController() 참고).
            Color fallbackBg = config != null ? config.backgroundFallbackColor : new Color(0.94f, 0.94f, 0.94f);
            cam.backgroundColor = new Color(fallbackBg.r, fallbackBg.g, fallbackBg.b, 0f);
            camGo.AddComponent<AudioListener>();

            // hitTestType=Raycast의 전제 조건(ConfigureUniWindowController 문서 (a) 참고).
            EnsureEventSystem();

            // 진짜 투명/클릭관통/항상위를 담당하는 UniWindowController를 씬에 자동 배치한다.
            // 수동 씬 편집 없이 --force로 항상 재현 가능해야 한다는 기존 컨벤션에 따라 코드로 생성한다.
            ConfigureUniWindowController(cam);

            // BUG-SW-M1 대응: RAGDOLL이 실제로 부딪혀 멈출 수 있는 정적 바닥. Rigidbody2D를 붙이지
            // 않으므로 Unity가 자동으로 정적 콜라이더로 취급한다(Architect 결정 — "표준 랙돌 기법").
            CreateGroundCollider(cam);

            if (stickmanPrefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(stickmanPrefab, scene);
                // ★ 2026-08-28 스폰 높이 변경 — 화면 세로 중앙에서 시작해 **떨어지면서 착지**하게 한다
                // (사용자 요청 "독위에서만 걷고 독아래로 가면 바닥으로 내려가야하는데").
                //
                // 이력: 예전에는 안전망 발판 상단 바로 위(ComputeGroundTopWorldY + 0.3)에 놓았다. 그런데
                // 이번 라운드에 안전망이 화면 최하단(OS y=942)으로 내려가고 Dock 발판(OS y=907)이 그보다
                // **위에** 새로 생기면서, 그 스폰 위치는 이미 Dock 상단선 아래가 되어버렸다. 착지는
                // "발판 상단선을 위->아래로 가로지를 때만" 성립하므로(States/GroundSensor.TryFindLandingCrossing),
                // 아래에서 시작한 캐릭터는 **Dock에 영원히 올라갈 수 없다** — 실측으로 확인했다
                // (자율 배회의 점프 높이는 jumpForce 6 / gravityScale 3 기준 약 0.61유닛인데 바닥에서
                // Dock 상단까지는 약 1.29유닛이라 점프로도 닿지 않는다).
                //
                // 화면 세로 중앙(= 카메라 y)에서 시작하면 첫 프레임부터 자유낙하해 그 x에서 가장 높은
                // 표면(창 -> Dock -> 바닥 안전망 순)에 자연스럽게 착지한다. 데스크톱 펫으로서도 이쪽이
                // 자연스럽고("어딘가에서 내려온다"), 헤드리스 테스트에서도 0.9초면 착지가 끝나 기존
                // 샘플 시점(t=5/10/15초)에 아무 영향이 없다(실측 확인).
                instance.transform.position = new Vector3(0f, cam.transform.position.y, 0f);

                // 라이벌 스틱맨(11절) 배선 — 아래 CreateRivalStickman 문서 참고.
                CreateRivalStickman(stickmanPrefab, config, instance.GetComponent<StickmanAgent>());
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
        //
        // ★ 2026-08-28 상향(0.6/1.5 -> 0.9/3.0, 사용자 피드백 "떨어지면 이상하게 넘어짐"): 관절 제한을
        // 조여도 감쇠가 약하면 팔다리가 제한 경계에서 오래 튕기며 파닥거려 "축 늘어진" 느낌 대신
        // "경련하는" 느낌이 난다. 두 값 모두 **RAGDOLL 구간에만** 유효하다는 점이 이 상향을 안전하게
        // 만든다 — 능동 상태에서 팔다리는 Kinematic이라 damping이 물리적으로 적용될 대상이 없다.
        private const float LimbLinearDamping = 0.9f;
        private const float LimbAngularDamping = 3f;

        // 루트(몸통)의 각(회전) 감쇠. 능동 상태에서는 루트 회전이 FreezeRotation으로 완전히 잠겨 있으므로
        // (States/RagdollRig.EnterActiveMode) 이 값 역시 **RAGDOLL 구간에서만** 의미를 갖는다 —
        // 걷기/점프/낙하 거동에는 영향이 없다(선형 damping은 그런 보장이 없어 0 그대로 둔다).
        // 몸통이 바닥에서 팽이처럼 계속 구르지 않고 몇 번 뒤척인 뒤 멈추게 하는 것이 목적이다.
        private const float RootAngularDamping = 2f;

        /// <summary>
        /// 더미 발판(Platform/NullPlatformWindowService.cs)의 상단 가장자리가 대응하는 월드 Y를,
        /// 실제 Screen.height 실측값과 무관한 폐쇄형 수식으로 계산한다(클래스 문서 상단 "좌표계 참고"
        /// 절에 유도 과정 있음). CreateGroundCollider()와 BuildMainScene()의 캐릭터 초기 배치가 반드시
        /// 이 헬퍼 하나만 거쳐야 한다 — 두 곳이 각자 따로(매직 넘버로) 계산하다가 서로 어긋난 것 자체가
        /// 이번 화면 프레이밍 버그의 근본 원인 중 하나였다(BUG-P1-R4-B1). NullPlatformWindowService의
        /// DummyFootholdHeightFraction 공개 상수를 그대로 참조해, 그 클래스의 발판 배치가 바뀌면 이
        /// 계산도 자동으로 함께 갱신되도록 한다(재발 방지).
        ///
        /// ★ 이 단일 소스 설계가 실제로 값을 한 번에 옮겨준 사례(2026-08-28): 안전망 발판을 화면 80%
        /// 지점에서 Dock 위로 내리는 작업에서 코드로 바꾼 것은 그 상수 하나뿐이고, 캐릭터 스폰 Y
        /// (BuildMainScene)와 RAGDOLL 물리 바닥 Y(CreateGroundCollider)는 둘 다 이 헬퍼를 거치므로
        /// 자동으로 함께 내려갔다(-7.2 -> -10.167). 씬 에셋에 구운 값이므로 --force 재생성이 필요하다.
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
        ///
        /// ============================================================================
        /// ★★ 이 물리 바닥은 **전체 폭 그대로 유지한다** — 논리적 발판과 일부러 모양이 다르다
        ///    (2026-08-29, 사용자 신고 "다시 독과 겹쳐서 걸음" 수정 라운드, 리더 지시 1항)
        /// ============================================================================
        /// 같은 라운드에 Platform/FallbackPlatformWindowService의 **논리적** 바닥 안전망은 Dock 가로
        /// 구간을 잘라낸 두 조각으로 쪼개졌다(그 클래스의 AppendBottomSafetyNet 문서 참고). 그런데
        /// 여기 이 BoxCollider2D는 그 구멍을 **따라가면 안 된다**. 둘의 역할이 다르기 때문이다:
        ///   • 논리적 발판(FallbackPlatformWindowService) : GroundSensor의 접지/착지/경계 **판정** 전용.
        ///     Dock 구간에 구멍이 있어야 "Dock 밑을 걸어다녀 Dock과 겹쳐 보이는" 상태가 원천 차단된다.
        ///   • 이 PhysicsGround(BoxCollider2D)          : Unity 2D 물리의 **실제 충돌면**.
        ///     RAGDOLL은 상태머신 판정이 아니라 순수 물리로 굴러다니므로 여기까지 구멍을 뚫으면
        ///     Dock 가로 구간(=화면 정중앙 65%, 캐릭터가 대부분의 시간을 보내는 곳)에서 랙돌이 바닥을
        ///     그대로 통과해 화면 아래로 사라진다.
        /// 정리하면, Dock 구간의 화면 최하단에서 캐릭터는 "물리적으로는 떠받쳐지지만 논리적으로는 접지
        /// 하지 않는다". 그 상태로 흘러드는 예외 경로(사용자가 그리로 던짐 등)는 상태머신 쪽 최종
        /// 안전망(StickmanBlackboard의 LostCharacterRescueSeconds Fall 감시 -> RescueToSafeGround)이
        /// 회수한다 — 그 대신 물리 바닥에 구멍을 뚫는 선택은 "랙돌이 화면 밖으로 사라진다"는 훨씬
        /// 나쁜 실패로 이어진다.
        /// </summary>
        /// <summary>
        /// UniWindowController(com.kirurobo.uniwinc) 인스턴스를 씬에 배치하고 이 프로젝트에 맞는 초기
        /// 상태로 설정한다(UniWindowController 도입 라운드, 2026-08-28).
        ///
        /// 패키지의 Runtime/Prefabs/UniWindowController.prefab을 먼저 시도하고(업스트림 업데이트를
        /// 자동으로 따라가기 위함), 찾지 못하면 빈 GameObject + AddComponent로 폴백한다 — 패키지 내부
        /// 경로가 향후 버전에서 바뀌더라도 씬 생성 자체가 실패하지 않게 하는 방어책이다.
        ///
        /// 설정값과 근거:
        ///   - _isTransparent = true   : 이번 라운드의 핵심 목표. 창 attach 시점에 네이티브로 자동 적용되며,
        ///     런타임에 MacWindowService.CreateOverlayWindow()가 한 번 더 명시적으로 적용한다(이중 안전).
        ///   - _isTopmost = false      : StickmanAgent.Start()가 SetAlwaysOnTop(true)로 명시적으로 켠다.
        ///   - isHitTestEnabled = false: 매우 중요. true면 라이브러리가 매 프레임 커서 아래 알파를 보고
        ///     isClickThrough를 자동으로 켜버려, StickmanAgent의 "시작 후 5초간 클릭관통 금지" 안전장치가
        ///     시작 즉시 무력화된다. 5초 뒤 MacWindowService.SetClickThrough(true)가 이 값을 켠다.
        ///   - autoSwitchCameraBackground = false : 위 카메라 배경 주석 참고 — 라이브러리가 배경을
        ///     Color.clear(RGB 0,0,0)로 덮어쓰는 것을 막아 "투명 실패 시 검정-on-검정" 사고를 예방한다.
        ///   - hitTestType = Raycast   : **커서 아래에 Collider2D가 있는지**로 판정(2026-08-28 전환).
        ///     이전의 Opacity(커서 아래 픽셀 알파)는 우리 캐릭터가 화면상 2.5~3pt짜리 얇은 선이라
        ///     "그 획을 정확히 맞춰야만 클릭이 먹는" 사실상 불가능한 UX였다(직전 라운드 한계 기록).
        ///     Raycast 모드는 UniWindowController.HitTestByRaycast()가 매 프레임
        ///     `EventSystem.current.RaycastAll` -> `Physics.Raycast` -> `Physics2D.GetRayIntersection`
        ///     순으로 확인하므로, 캐릭터에 이미 있는 Collider2D(루트 캡슐/머리 원/팔다리 박스) +
        ///     아래에서 추가하는 넉넉한 GrabArea 트리거가 그대로 클릭 영역이 된다.
        ///     **이 모드가 요구하는 3가지 전제를 이 파일이 전부 충족시킨다**:
        ///       (a) EventSystem이 씬에 있어야 한다 — 없으면 `EventSystem.current.RaycastAll`에서
        ///           NullReferenceException이 나고 히트테스트 코루틴이 통째로 죽는다(그러면 클릭관통
        ///           상태가 마지막 값에 얼어붙는다). EnsureEventSystem()이 생성한다.
        ///       (b) currentCamera가 유효해야 한다 — 아래에서 명시 지정.
        ///       (c) 클릭을 받고 싶지 **않은** 콜라이더는 "Ignore Raycast" 레이어에 둬야 한다.
        ///           라이브러리가 쓰는 레이어 마스크가 정확히 `~LayerMask.GetMask("Ignore Raycast")`이고
        ///           Physics2D 쪽도 DefaultRaycastLayers라 이 레이어만 제외되기 때문이다. 그래서
        ///           CreateGroundCollider()의 보이지 않는 물리 바닥을 그 레이어로 옮긴다 — 안 그러면
        ///           화면 하단 전체 띠에서 클릭이 앱에 잡혀 비침해 원칙 2가 깨진다.
        ///   - currentCamera           : Camera.main 자동 탐색에 의존하지 않고 명시 지정(헤드리스/배치
        ///     환경에서 탐색이 실패하는 경우를 없앤다).
        /// </summary>
        private static void ConfigureUniWindowController(Camera cam)
        {
            const string PrefabPath = "Packages/com.kirurobo.uniwinc/Runtime/Prefabs/UniWindowController.prefab";

            GameObject go = null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            }
            else
            {
                Debug.LogWarning("[SceneBootstrapper] " + PrefabPath + "을(를) 찾지 못해 빈 GameObject + " +
                    "AddComponent<UniWindowController>()로 폴백합니다(패키지 경로 변경 가능성).");
                go = new GameObject("UniWindowController");
                go.AddComponent<UniWindowController>();
            }

            var controller = go.GetComponent<UniWindowController>();
            if (controller == null)
            {
                controller = go.AddComponent<UniWindowController>();
            }

            // public 필드는 직접 대입.
            controller.isHitTestEnabled = false;
            controller.hitTestType = UniWindowController.HitTestType.Raycast;
            controller.autoSwitchCameraBackground = false;
            controller.currentCamera = cam;
            controller.forceWindowed = true;

            // _isTransparent / _isTopmost는 [SerializeField] private이라 SerializedObject로만 씬에
            // 저장 가능하다(런타임 프로퍼티 setter는 네이티브 창을 건드리므로 에디터에서 쓰면 안 된다).
            var so = new SerializedObject(controller);
            so.FindProperty("_isTransparent").boolValue = true;
            so.FindProperty("_isTopmost").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            // ============================================================================
            // 매우 중요 — 이 GameObject는 반드시 "비활성" 상태로 씬에 저장한다.
            // ============================================================================
            // 실측으로 발견한 사고(2026-08-28, PlayMode 테스트 3/3 -> 프로세스 크래시): 네이티브
            // LibUniWinC의 Swift 함수 _findMyWindow()는 NSApplication의 창 목록에서 자기 창을 찾는데,
            // `-batchmode -nographics`로 도는 Unity(= 우리 PlayMode 테스트 실행 방식)에는 NSWindow가
            // 아예 하나도 없어 그 안에서 프로세스가 통째로 죽는다(스택: LibUniWinC._findMyWindow <-
            // AttachMyWindow <- UniWindowController.UpdateTargetWindow <- Update). 컴포넌트가 활성인 채로
            // 씬에 저장되면 씬을 여는 모든 헤드리스 실행이 이 크래시를 밟는다.
            //
            // 그래서 씬에는 비활성으로 저장하고, 활성화는 실제 Standalone macOS Player 안에서만 일어나는
            // Platform/MacOS/MacWindowService.CreateOverlayWindow()가 담당한다(그 서비스 자체가
            // StickmanAgent.CreatePlatformService()의 `UNITY_STANDALONE_OSX && !UNITY_EDITOR` 분기에서만
            // 인스턴스화된다). 부수 효과로 "에디터 Play 모드에서 에디터 자신의 창을 건드리는" 사고도
            // 함께 원천 차단된다 — 어차피 공식 문서가 "투명은 에디터에서 동작하지 않으니 빌드해서
            // 테스트하라"고 경고하므로 에디터에서 이 컴포넌트가 돌아야 할 이유가 없다.
            go.SetActive(false);

            EditorUtility.SetDirty(controller);

            Debug.Log("[SceneBootstrapper] UniWindowController 배치 완료 " +
                "(activeSelf=false — 실제 Player에서 MacWindowService가 활성화, " +
                "_isTransparent=true, _isTopmost=false, isHitTestEnabled=false, hitTestType=Raycast, " +
                "autoSwitchCameraBackground=false, currentCamera=Main Camera).");
        }

        /// <summary>
        /// UniWindowController의 Raycast 히트테스트가 요구하는 EventSystem을 씬에 배치한다.
        ///
        /// 왜 필수인가: HitTestByRaycast()의 첫 줄이 `EventSystem.current.RaycastAll(...)`인데 null
        /// 체크가 없다. 씬에 EventSystem이 없으면 이 호출이 NullReferenceException을 던지고, 그 코루틴
        /// (HitTestCoroutine)이 통째로 종료된다 — 즉 히트테스트가 조용히 멈추고 클릭관통 상태가 마지막
        /// 값에 영구히 얼어붙는다. "조용한 오동작"이 가장 나쁜 형태라 반드시 함께 배치한다.
        ///
        /// 입력 모듈(StandaloneInputModule)은 일부러 붙이지 않는다 — 이 프로젝트에는 uGUI Canvas가
        /// 하나도 없어 RaycastAll이 항상 빈 결과를 돌려주고(그 다음 단계인 카메라 레이캐스트로 넘어간다),
        /// 모듈이 없어도 EventSystem.current는 OnEnable에서 정상적으로 채워진다. 필요 없는 컴포넌트가
        /// 매 프레임 입력을 훑지 않게 하는 편이 상주 앱에 유리하다.
        /// </summary>
        private static void EnsureEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            // ★ 2026-08-29: StandaloneInputModule을 함께 붙인다. 이전 주석은 "이 프로젝트에는 uGUI
            // Canvas가 하나도 없어 모듈이 필요 없다"고 했고 그때는 사실이었지만, 그 뒤 말풍선
            // (DialogueBubbleRenderer) / 앱 제어 메뉴(AppControlDirector) / 투두 포스트잇
            // (TodoPostItWidget)이 각자 Canvas를 만들면서 전제가 깨졌다.
            //
            // 입력 모듈이 없는 EventSystem은 포인터 이벤트를 **아예 처리하지 않으므로**
            // Button.onClick이 영원히 발동하지 않는다 — 포스트잇 체크박스가 한 번도 눌리지 않았던
            // 원인 절반이 정확히 이것이었다(Interaction/TodoPostItWidget.cs 클래스 문서 (1)).
            // 나머지 절반은 클릭관통이라 클릭이 이 창까지 오지도 않았던 것이고, 그쪽은 그 위젯이
            // 자체 차단막 콜라이더로 해결한다.
            go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Debug.Log("[SceneBootstrapper] EventSystem + StandaloneInputModule 배치 완료 — " +
                "EventSystem은 UniWindowController의 hitTestType=Raycast가 null 체크 없이 사용하므로 필수이고, " +
                "입력 모듈은 uGUI Button.onClick(투두 포스트잇 체크박스)이 발동하기 위해 필수다.");
        }

        /// <summary>
        /// 라이벌 스틱맨(docs/UX_FLOW.md 11절 "붉은 스틱맨이 난입해 서로 쫓아다니며 싸운다") 배선.
        ///
        /// ============================================================================
        /// 왜 이제야 배선하는가
        /// ============================================================================
        /// Interaction/RivalStickmanAgent.cs(추적/전투 AI)와 RivalEncounterDirector.cs(스폰 판정)는
        /// Phase 3에 이미 완성돼 있었지만 **씬 어디에도 배치되지 않아 한 번도 스폰된 적이 없었다** —
        /// DragThrowController/RodeoCursorWatcher가 겪었던 것과 정확히 같은 유형의 누락이다.
        ///
        /// ============================================================================
        /// 왜 별도 프리팹을 새로 만들지 않고 플레이어 프리팹을 복제해 깎아내는가
        /// ============================================================================
        /// 라이벌은 "붉은색이고 조종 대상이 아닌" 것 말고는 플레이어와 **완전히 같은 지오메트리**
        /// (2분절 팔다리 + 관절 + 콜라이더 + 레이어)를 필요로 한다. 그 지오메트리는 BuildStickmanPrefab
        /// 안에서 서로 얽힌 계산(footLift/totalHeight/관절 각도 제한)으로 만들어지므로, 별도 빌더로
        /// 복제하면 두 벌이 서로 어긋나는 순간 라이벌만 조용히 깨진다. 그래서 같은 프리팹을 인스턴스화한
        /// 뒤 **플레이어 전용 컴포넌트만 제거**한다 — 지오메트리에 대한 단일 진실 소스를 유지한다.
        ///
        /// 제거 대상(플레이어 전용): StickmanAgent(플랫폼 서비스/발판 폴러/자율 배회 소유자),
        /// StickmanClickHitbox / DragThrowController / RodeoCursorWatcher(유저 상호작용 — 라이벌은
        /// 관전 전용이라 클릭 대상이 아니다), AppControlDirector(앱 제어 메뉴는 하나면 된다),
        /// BattleMinigameDirector/Renderer + GraffitiDirector/Renderer(라이벌은 이 스펙터클의 주체가
        /// 아니고, 렌더러는 전역 이벤트를 구독하므로 남겨두면 소환물이 두 벌 생긴다 — 실측 확인).
        /// 남기는 것: Rigidbody2D/콜라이더/팔다리 계층/DialogueBubbleRenderer(라이벌도 말을 한다).
        ///
        /// 팔다리의 RagdollLimbImpactRelay는 남겨두어도 안전하다 — 그 컴포넌트는 StickmanAgent를
        /// 부모에서 찾아 쓰는데, 못 찾으면 아무것도 하지 않는다(Core/RagdollLimbImpactRelay.cs).
        /// </summary>
        private static void CreateRivalStickman(GameObject stickmanPrefab, StickConfig config, StickmanAgent player)
        {
            if (player == null)
            {
                Debug.LogError("[SceneBootstrapper] 플레이어 StickmanAgent를 찾지 못해 라이벌을 배선하지 못했습니다.");
                return;
            }

            // 호출 경로가 늘어나도 같은 함정(위 BuildMainScene의 NewScene 주석)에 빠지지 않도록 한 번 더 방어.
            if (config == null) config = AssetDatabase.LoadAssetAtPath<StickConfig>(ConfigAssetPath);

            var rival = (GameObject)PrefabUtility.InstantiatePrefab(stickmanPrefab);
            rival.name = "RivalStickman";
            // 프리팹 연결을 끊는다 — 아래에서 컴포넌트를 제거할 것이고, 프리팹 인스턴스에서는 프리팹이
            // 소유한 컴포넌트를 제거할 수 없다(그리고 남겨두면 플레이어 프리팹 수정이 라이벌의 삭제
            // 오버라이드와 충돌한다).
            PrefabUtility.UnpackPrefabInstance(rival, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            // 플레이어 전용 컴포넌트 제거. 다른 컴포넌트가 의존하는 것(StickmanAgent/StickmanClickHitbox)을
            // **나중에** 지워야 RequireComponent 제약에 걸리지 않는다.
            DestroyComponentIfPresent<AppControlDirector>(rival);
            // 격파 미니게임/그라피티(2026-08-29 신설) — 라이벌은 이 스펙터클의 주체가 아니다.
            // 특히 렌더러 2종을 남겨두면 **실측으로 확인된 실제 버그**가 난다: 두 렌더러 모두
            // StickmanEventBus의 전역 정적 이벤트를 구독하므로, 플레이어가 격파를 시작하면 라이벌의
            // 렌더러도 같은 이벤트를 받아 판자 한 벌을 더 소환한다(첫 실행 로그에 "[격파] 소환"이
            // 정확히 2번 찍혀 발견했다). 렌더러 쪽에도 "자기 GameObject의 StickmanAgent가 없으면
            // 아무것도 하지 않는다"는 자체 가드를 넣었지만, 애초에 배치하지 않는 것이 1차 방어다
            // (DragThrowController/RodeoCursorWatcher를 지우는 것과 정확히 같은 이유).
            DestroyComponentIfPresent<BattleMinigameRenderer>(rival);
            DestroyComponentIfPresent<BattleMinigameDirector>(rival);
            DestroyComponentIfPresent<GraffitiRenderer>(rival);
            DestroyComponentIfPresent<GraffitiDirector>(rival);
            // 창 도둑/창 크래시/하드웨어 반응(2026-08-29 신설) — 격파/그라피티와 **정확히 같은 함정**이다.
            // 세 렌더러 모두 StickmanEventBus의 전역 정적 이벤트를 구독하므로, 남겨두면 플레이어가
            // 창 도둑을 시작할 때 라이벌 쪽 렌더러도 같은 이벤트를 받아 고스트 창/균열/이모트가 두 벌
            // 그려진다(격파에서 "[격파] 소환"이 정확히 2번 찍혀 실측 확인된 그 버그). 각 렌더러에도
            // "자기 GameObject의 StickmanAgent가 없으면 아무것도 하지 않는다"는 자체 가드가 있지만,
            // 애초에 배치하지 않는 것이 1차 방어다. Director 3종은 플레이어 전용 트리거이므로 함께 제거한다.
            DestroyComponentIfPresent<WindowTheftRenderer>(rival);
            DestroyComponentIfPresent<WindowTheftDirector>(rival);
            DestroyComponentIfPresent<WindowCrashRenderer>(rival);
            DestroyComponentIfPresent<WindowCrashDirector>(rival);
            DestroyComponentIfPresent<HardwareReactionRenderer>(rival);
            DestroyComponentIfPresent<HardwareReactionDirector>(rival);
            // Phase 5(2026-08-29 신설) — 창 도둑/크래시/하드웨어 반응과 **정확히 같은 함정**이다.
            // 렌더러 4종이 전부 StickmanEventBus의 전역 정적 이벤트를 구독하므로, 남겨두면 플레이어가
            // 가출할 때 라이벌 쪽에도 과자/파문이 한 벌 더 그려지고 라이벌 어깨에도 처짐 표시가 뜬다.
            // TodoPostItWidget은 특히 위험하다 — 자기 Canvas와 클릭관통 차단막을 통째로 한 벌 더 만들어
            // 화면 우상단에 포스트잇 카드가 겹쳐 뜨고, 차단막 두 개가 같은 영역을 덮는다.
            // Director 4종은 플레이어 전용 트리거이므로 함께 제거한다(라이벌은 스트레스를 받지도,
            // 가출하지도, 할일을 갖지도, 집중 모드를 켜지도 않는다).
            DestroyComponentIfPresent<StressGaugeRenderer>(rival);
            DestroyComponentIfPresent<StressGaugeDirector>(rival);
            DestroyComponentIfPresent<RunawayRenderer>(rival);
            DestroyComponentIfPresent<RunawayDirector>(rival);
            DestroyComponentIfPresent<TodoReminderRenderer>(rival);
            DestroyComponentIfPresent<TodoReminderDirector>(rival);
            DestroyComponentIfPresent<TodoPostItWidget>(rival);
            DestroyComponentIfPresent<FocusWatchRenderer>(rival);
            DestroyComponentIfPresent<FocusWatchDirector>(rival);
            DestroyComponentIfPresent<RodeoCursorWatcher>(rival);
            DestroyComponentIfPresent<DragThrowController>(rival);
            DestroyComponentIfPresent<StickmanClickHitbox>(rival);
            DestroyComponentIfPresent<StickmanAgent>(rival);

            // 붉은색(11절). 런타임에도 RivalStickmanAgent.Awake()가 같은 값을 다시 적용하지만,
            // 씬 에셋에도 구워둬야 에디터에서 열었을 때 "왜 검은색이지?" 하는 혼란이 없다.
            Color rivalColor = config != null ? config.rivalInkColor : new Color(0.85f, 0.13f, 0.13f);
            var rivalLines = rival.GetComponentsInChildren<LineRenderer>(true);
            for (int i = 0; i < rivalLines.Length; i++)
            {
                rivalLines[i].startColor = rivalColor;
                rivalLines[i].endColor = rivalColor;
            }

            // 스폰 전 대기 위치는 화면 밖 멀리. RivalStickmanAgent.Awake()가 Rigidbody2D.simulated를
            // 꺼두므로 여기서 가만히 있다가, BeginDuel()이 실제 스폰 좌표로 옮긴다.
            rival.transform.position = new Vector3(RivalParkingWorldX, 0f, 0f);

            var rivalAgent = rival.AddComponent<RivalStickmanAgent>();
            var rivalSo = new SerializedObject(rivalAgent);
            rivalSo.FindProperty("_config").objectReferenceValue = config;
            rivalSo.ApplyModifiedPropertiesWithoutUndo();

            // 라이벌의 말풍선은 **자기 상태머신이 발급한 대사만** 그려야 한다(UX_FLOW.md 5절 규칙 7).
            // 그 상태머신은 첫 대결에서야 만들어지므로, 그 전까지는 이 플래그가 "화자 미지정 = 전부
            // 수신" 폴백을 막는다(Dialogue/DialogueBubbleRenderer.cs의 _requireBoundSpeaker 참고).
            var rivalBubble = rival.GetComponent<DialogueBubbleRenderer>();
            if (rivalBubble != null)
            {
                var bubbleSo = new SerializedObject(rivalBubble);
                bubbleSo.FindProperty("_agent").objectReferenceValue = null; // 라이벌은 StickmanAgent가 없다.
                bubbleSo.FindProperty("_requireBoundSpeaker").boolValue = true;
                bubbleSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // 스폰 판정기. 라이벌 오브젝트 자신에 붙인다(별도 관리 오브젝트를 늘리지 않는다).
            var director = rival.AddComponent<RivalEncounterDirector>();
            var directorSo = new SerializedObject(director);
            directorSo.FindProperty("_player").objectReferenceValue = player;
            directorSo.FindProperty("_rival").objectReferenceValue = rivalAgent;
            directorSo.FindProperty("_config").objectReferenceValue = config;
            directorSo.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("[SceneBootstrapper] 라이벌 스틱맨 배선 완료 — 붉은색 " + rivalColor +
                      ", 대기 위치 x=" + RivalParkingWorldX + ", 강제 소환 단축키 Ctrl+Opt+Cmd+V.");
        }

        /// <summary>대기 위치 x(월드 유닛). 화면 폭(orthographicSize=12 기준 약 32유닛)과 배회 범위
        /// (약 53유닛)를 모두 벗어나 어떤 판정에도 걸리지 않는다.</summary>
        private const float RivalParkingWorldX = 500f;

        private static void DestroyComponentIfPresent<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            if (component != null) Object.DestroyImmediate(component, allowDestroyingAssets: false);
        }

        private static void CreateGroundCollider(Camera cam)
        {
            var ground = new GameObject("PhysicsGround");
            // 레이어 2 = "Ignore Raycast"(Unity 예약 레이어). 물리 충돌에는 아무 영향이 없고
            // (레이어 충돌 매트릭스는 별개 — StickmanLimb와 계속 정상 충돌한다) **레이캐스트 질의에서만**
            // 제외된다. hitTestType=Raycast 전환의 필수 조건이다: 이 바닥은 눈에 보이지 않는 물리
            // 안전망인데도 화면 하단 띠(2026-08-28부터 Dock 높이 = 화면의 약 7.6%, 그 전에는 20%) 전체를
            // 덮고 있어, 레이캐스트 히트테스트가 여기에 걸리면 그 띠에서 클릭이 전부 우리 앱에 잡혀
            // 버린다(비침해 원칙 2 정면 위반).
            ground.layer = 2;

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
        private static LineRenderer ConfigureLine(GameObject go, Color color, int sortingOrder, bool loop, float width)
        {
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.material = GetLineMaterial();
            lr.startColor = color;
            lr.endColor = color;
            // ★ 2026-08-29: 예전에는 상수 LineWidth를 여기서 직접 읽었다. 크기 배율이 들어오면서
            // 두께가 호출부마다 달라지므로(전부 BuildStickmanPrefab이 배율을 곱해 만든다) 인자로 받는다.
            lr.startWidth = width;
            lr.endWidth = width;
            lr.numCapVertices = LineCapVertices; // 끝을 살짝 둥글려 손그림 느낌(각진 사각형 끝 대신).
            lr.numCornerVertices = LineCapVertices;
            lr.sortingOrder = sortingOrder;
            lr.loop = loop;
            return lr;
        }

        /// <summary>직선 하나로 된 시각 표현(몸통). 물리 없이 parent의 자식 Transform으로만 존재 —
        /// CreateStaticVisual이 하던 역할을 사각형 스프라이트 대신 LineRenderer로 대체한다.</summary>
        private static GameObject CreateLineSegmentVisual(Transform parent, string name, Vector3 localPos, Vector3 localStart, Vector3 localEnd, float width, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one;

            var lr = ConfigureLine(go, color, sortingOrder, loop: false, width);
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
        private static GameObject CreateHeadAnchor(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one;
            return go;
        }

        /// <summary>
        /// 속이 빈 원(링) — 머리 테두리 전용. 반지름 radius의 원 경로를 width 두께의 선으로 그린다
        /// (선이 반지름보다 얇으므로 가운데가 뚫린 링이 된다). 진짜 투명 창 도입 후 얼굴 안쪽은
        /// 아무것도 그리지 않으므로(CreateHeadAnchor 참고), 이 링이 머리의 유일한 외곽선이다.
        /// </summary>
        private static GameObject CreateRing(Transform parent, string name, Vector3 localPos, float radius,
            float width, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one;

            var lr = ConfigureLine(go, color, sortingOrder, loop: true, width);
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

            // 지름보다 넉넉히 두꺼워야 안쪽까지 완전히 채워진다.
            var lr = ConfigureLine(go, color, sortingOrder, loop: true, radius * 2.4f);
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
        /// **두 마디 모두** HingeJoint2D에 각도 제한(useLimits)을 건다 — 위 마디는 고관절/어깨 스윙
        /// 범위(upperMinAngle/upperMaxAngle), 아래 마디는 무릎/팔꿈치 굽힘 범위(lowerMinAngle/
        /// lowerMaxAngle). 2026-08-28까지 위 마디는 제한이 없어 RAGDOLL에서 팔다리가 몸통을 축으로
        /// 360도 돌 수 있었다(MaxJointBendDegrees 위 문서의 원인 (1) 참고).
        /// </summary>
        private static void CreateLimb(Transform hierarchyParent, Rigidbody2D connectedBody, string name,
            Vector2 attachLocal, float upperLength, float lowerLength, float width,
            float upperAngle, float lowerAngle,
            float upperMinAngle, float upperMaxAngle, float lowerMinAngle, float lowerMaxAngle,
            Color color, float mass, float gravityScale, int sortingOrder, int limbLayer, StickmanAgent agent)
        {
            GameObject upper = CreateLimbSegment(hierarchyParent, connectedBody, name, attachLocal, upperLength,
                width, upperAngle, useLimits: true, minAngle: upperMinAngle, maxAngle: upperMaxAngle,
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
            var lr = ConfigureLine(segment, color, sortingOrder, loop: false, width);
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
