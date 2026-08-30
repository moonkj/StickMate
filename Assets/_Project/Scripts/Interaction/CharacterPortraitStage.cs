using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>초상화 포즈 3버킷 + 숨김. 상태 ID에서 <b>파생</b>되며 이 목록 밖의 값은 없다.</summary>
    public enum PortraitPose
    {
        /// <summary>서 있음(중립).</summary>
        Standing = 0,

        /// <summary>넘어져 있음(랙돌/던져짐/일어나는 중). <b>붙잡힘은 여기가 아니다</b> —
        /// 2026-08-30 사용자 신고 참고(<see cref="PoseForState"/>의 Dragged 주석).</summary>
        Fallen = 1,

        /// <summary>뭔가 하는 중(활쏘기/격파/낙서/붙잡혀 버둥거림 등) — 한쪽 팔을 들어
        /// "작업 중 / 들려 있는 중"으로 읽힌다.</summary>
        Busy = 2,

        /// <summary>가출 중 — 액자를 <b>비운다</b>(없는 사람을 그리지 않는다).</summary>
        Hidden = 3,
    }

    /// <summary>
    /// ★ 정보창 초상화의 촬영장 — 2026-08-30 리디자인 라운드(리더/UX 디자이너 확정안).
    /// 화면 밖 먼 좌표에 <b>전용 미니 피규어</b>를 세우고 전용 카메라 1대로 RenderTexture에 찍는다.
    /// 정보창은 그 텍스처를 RawImage로 붙이기만 한다.
    ///
    /// ============================================================================
    /// 왜 "라이브 캐릭터"가 아니라 "전용 미니 피규어"인가 (디자이너 지적, 리더 확정)
    /// ============================================================================
    /// 실제 캐릭터를 찍으면 랙돌로 널브러진 모습, 프레임 밖으로 걸어나간 빈 상자, 가출 중 빈 화면이
    /// 그대로 초상화가 된다. 반대로 uGUI로 미니어처를 새로 그리면 액세서리 도형이 이중 정의가 된다.
    /// 그래서 <b>도형은 공유하고(Interaction/AccessoryShapeBuilder.cs) 개체만 분리</b>한다 —
    /// 모자 챙을 저 파일에서 고치면 캐릭터와 초상화가 함께 바뀐다.
    ///
    /// ============================================================================
    /// 메인 카메라의 cullingMask를 <b>건드리지 않는다</b> (리더 지시 3항에 대한 실행 판단)
    /// ============================================================================
    /// 리더 안은 "전용 레이어 + 메인 카메라 컬링 마스크에서 제외"였다. 실제로 해보면 그 방법은
    /// <b>런타임에 레이어를 새로 만들 수 없다</b>는 Unity 제약에 걸린다(레이어 추가는 에디터에서
    /// ProjectSettings/TagManager를 고치는 일이다 — Editor/SceneBootstrapper.EnsureStickmanLimbLayer가
    /// 그래서 에디터 코드다). 그래서 같은 목적을 <b>더 안전한 방법</b>으로 달성한다:
    /// 미니 피규어를 <see cref="StageWorldX"/>(= 10000유닛) 떨어진 곳에 세운다. 메인 카메라는
    /// 직교 크기 12(가시 폭 약 32유닛)라 그 좌표를 <b>절대 볼 수 없고</b>(프러스텀 컬링), 그 자리에는
    /// 다른 오브젝트가 하나도 없어 전용 카메라도 미니 피규어만 찍는다. 즉 메인 카메라 설정 변경 0건,
    /// ProjectSettings 변경 0건으로 같은 격리를 얻는다.
    ///
    /// ============================================================================
    /// 비침해 / 감사 규칙
    /// ============================================================================
    /// · 미니 피규어에는 <b>Collider가 하나도 없다</b>(Tests/EditMode/UserAssetImmutabilityAuditTests가
    ///   감사하는 "관전 전용 = 콜라이더 0개" 규칙, 그리고 물리에 개입하지 않기 위해서도 그렇다).
    /// · Rigidbody도 없다 — 포즈는 전부 정적 좌표다.
    /// · 카메라는 <b>창이 열려 있는 동안만</b> enabled=true다(닫히면 렌더 비용 0).
    /// · 이 오브젝트는 캐릭터의 자식이 <b>아니다</b>. 가출 은신/전체화면 자동 숨김은 캐릭터의 렌더러만
    ///   끄므로 초상화와 무관하다 — 초상화는 오직 창 개폐와 <see cref="SetPose"/>로만 켜고 끈다.
    /// </summary>
    public sealed class CharacterPortraitStage : MonoBehaviour
    {
        /// <summary>촬영장 좌표. 메인 카메라 가시 범위(약 ±16유닛)에서 한참 멀다.</summary>
        public const float StageWorldX = 10000f;

        /// <summary>RT를 화면 표시 <b>물리 픽셀</b>의 몇 배로 찍을 것인가. 2배로 찍어 축소 표시하면 MSAA 없이도
        /// 대각선 획이 매끄럽다(2026-08-29 "선 화질 조사" 라운드에서 MSAA 8x가 오히려 함정이었다).
        /// <para>기준이 "표시 물리 픽셀"이라는 점이 중요하다 — 캔버스 유닛을 기준으로 삼으면 Retina에서
        /// 슈퍼샘플이 아니라 등배가 된다(<see cref="TryEnsureTexture"/>의 2026-08-30 사고 참고).</para></summary>
        private const int Supersample = 2;

        private const int MaxTextureSide = 2048;

        // 팔다리 길이/자세 — Editor/SceneBootstrapper.cs의 배율 1.0 기준값과 같은 값이어야 "화면 속
        // 캐릭터"와 초상화가 같은 몸으로 읽힌다(관절 위치는 StickmanMetrics 실측을 쓰므로 여기 있는
        // 것은 마디 길이와 중립 각도뿐이다).
        private const float ArmUpperRatio = 0.38f / StickConfig.BaselineCharacterTotalHeight;
        private const float ArmLowerRatio = 0.37f / StickConfig.BaselineCharacterTotalHeight;
        private const float LegUpperRatio = 0.50f / StickConfig.BaselineCharacterTotalHeight;
        private const float LegLowerRatio = 0.45f / StickConfig.BaselineCharacterTotalHeight;
        // ★ 2026-08-30: 눈 중립 오프셋은 Interaction/AccessoryShapeBuilder.cs가 단일 정의처다
        //   (모자/안경 도형이 같은 눈 좌표를 기준선으로 쓴다 — 두 곳에 적으면 한쪽만 어긋난다).
        //   여기서는 머리 반경 배수로 받아 쓴다.
        private const float EyeRadiusRatio = 0.030f / StickConfig.BaselineCharacterTotalHeight;
        private const float StrokeWidthRatio = 0.048f / StickConfig.BaselineCharacterTotalHeight;

        private const float IdleArmSpreadDegrees = 40f;
        private const float IdleLegSpreadDegrees = 12f;
        private const float IdleElbowBendDegrees = 10f;
        private const float IdleKneeBendDegrees = -4f;

        /// <summary>숨쉬기 — 2초 주기로 ±1pt 남짓. 정지 화면이 아니라 "지금 살아 있는 동료"로 읽히게.</summary>
        private const float BreathPeriodSeconds = 2f;
        private const float BreathAmplitudeRatio = 0.006f;

        // ────────────────────────────────────────────────────────────────────────
        // 액자(카메라) 배치 — 키 배수. 서 있는 그림 기준으로 잡은 값이지만, 넘어짐 프레이밍도
        // 여기서 가시 사각형을 역산하므로 상수로 못박는다(두 계산이 따로 놀면 조용히 잘린다).
        //
        // ★ 2026-08-30 재조정 (docs/UX_FLOW.md 33-7/33-8) — 액자 종횡비가 152/214 = 0.710에서
        //   188/180 = 1.044로 바뀌면서 세로 표시 크기가 214pt -> 180pt로 줄었다. 옛 값
        //   (FrameOrthoRatio 0.62 / FrameCenterHeightRatio 0.58)을 그대로 두면 캐릭터만 작아진다.
        //
        //   33-8절은 "0.50 부근"을 제안했지만 그대로 쓸 수 없다. 0.50이면 가시 세로 높이가 정확히
        //   키 1.0배인데, **지금 그릴 수 있는 가장 높은 그림은 키의 1.077배**다(아래 유도). 취향이
        //   아니라 기하학적으로 안 들어간다. 그래서 추정 대신 **가장 높은 액세서리에서 역산**한다:
        //
        //     · 최고점 = 머리 중심 + R·1.80  (털모자 방울 꼭대기와 왕관 지그재그 꼭짓점이 공동 1위)
        //              = (H − R) + 1.80R = H + 0.80R = 1.0774·H
        //     · 최저점 = 발끝 획의 아래쪽 = −(획 두께/2) = −0.0106·H
        //     · 필요한 세로 span = 1.0880·H, 여기에 여백 5%를 더해 1.1424·H
        //       -> FrameOrthoRatio(반높이) = 0.5712,  FrameCenterHeightRatio(중심) = 0.5334
        //
        //   숫자를 손으로 적지 않고 식을 그대로 상수 식으로 둔다 — 모자가 더 높아지면 위 한 줄
        //   (TallestAccessoryAboveHeadCenterInR)만 고치면 액자가 따라온다.
        //
        //   ⚠ 육안 검증 1회는 아직 남아 있다(이 에이전트는 Unity를 실행할 수 없다). 이 값은
        //     "잘리지 않는 최소 + 5%"이지 "가장 보기 좋은 값"이 아니다.
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>머리 반경 / 전신 높이. 배율 1.0 프리팹 실측치에서 온다(AccessoryShapeBuilder와 같은 출처).</summary>
        private const float HeadRadiusInHeight =
            AccessoryShapeBuilder.BaselineHeadVisualRadius / StickConfig.BaselineCharacterTotalHeight;

        /// <summary>가장 높이 솟는 액세서리의 머리 중심 기준 높이(R 배수) — 털모자 방울 / 왕관 지그재그.</summary>
        private const float TallestAccessoryAboveHeadCenterInR = 1.80f;

        private const float FrameInkTopRatio =
            (1f - HeadRadiusInHeight) + TallestAccessoryAboveHeadCenterInR * HeadRadiusInHeight;

        private const float FrameInkBottomRatio = -StrokeWidthRatio * 0.5f;

        /// <summary>가시 사각형이 그림보다 얼마나 넉넉해야 하는가(여백 비율).</summary>
        private const float FrameMarginRatio = 0.05f;

        private const float FrameCenterHeightRatio = (FrameInkTopRatio + FrameInkBottomRatio) * 0.5f;
        private const float FrameOrthoRatio =
            (FrameInkTopRatio - FrameInkBottomRatio) * (1f + FrameMarginRatio) * 0.5f;

        /// <summary>넘어짐 — 몸을 눕히는 각도. 90도면 완전 수평이라 12도를 남겨 "쓰러진" 느낌을 준다.</summary>
        private const float FallenLayDownDegrees = -78f;

        /// <summary>넘어짐 프레이밍 — 그림이 가시 사각형의 몇 할까지 차지해도 되는가(나머지는 여백).
        /// 넘어진 몸은 가로로 <b>키만큼</b> 길어지는데 액자 가시 폭은 키의 1.02배뿐이라, 여백을
        /// 남기려면 줄이는 수밖에 없다(모자를 쓰면 키를 넘겨 더 줄어든다).</summary>
        private const float FallenFrameFill = 0.94f;

        /// <summary>넘어짐 프레이밍 — 그림의 중심을 액자 <b>아래에서</b> 몇 지점에 둘 것인가(0=바닥, 1=천장).
        /// 0.5보다 낮은 이유는 원래 의도 그대로다: 넘어진 사람이 액자 위쪽에 떠 있으면 "누워 있다"로
        /// 읽히지 않는다. 값은 옛 구현의 실효 위치(0.345)를 실측해 그 구도를 유지한 것이다.</summary>
        private const float FallenFrameCenterFromBottom = 0.34f;

        private StickConfig _config;
        private StickmanMetrics _metrics;
        private Material _lineMaterial;

        private Camera _camera;
        private Transform _figureRoot;
        private RenderTexture _texture;
        private readonly List<LineRenderer> _lines = new List<LineRenderer>(16);

        private PortraitPose _pose = PortraitPose.Standing;
        private bool _renderingRequested;
        private int _builtSignature = -1;
        private float _baseFigureY;

        /// <summary>지금 그려지고 있는 텍스처. 만들지 못했으면 null(정보창이 폴백 문구를 띄운다).</summary>
        public RenderTexture Texture => _texture;

        /// <summary>RT를 한 번이라도 만들지 못했는가 — 정보창의 폴백 표시 판단용.</summary>
        public bool HasTexture => _texture != null && _texture.IsCreated();

        public PortraitPose Pose => _pose;

        /// <summary>독립 루트 오브젝트로 촬영장을 만든다(캐릭터의 자식이 아니다 — 클래스 문서 참고).</summary>
        public static CharacterPortraitStage Create(StickConfig config, StickmanMetrics metrics, Material lineMaterial)
        {
            var go = new GameObject("CharacterPortraitStage");
            go.transform.position = new Vector3(StageWorldX, 0f, 0f);
            var stage = go.AddComponent<CharacterPortraitStage>();
            stage._config = config;
            stage._metrics = metrics;
            stage._lineMaterial = lineMaterial;
            stage.BuildCamera();
            stage.SetRenderingEnabled(false);
            return stage;
        }

        private void OnEnable() => StickmanEventBus.CharacterEquipmentChanged += OnEquipmentChanged;

        private void OnDisable() => StickmanEventBus.CharacterEquipmentChanged -= OnEquipmentChanged;

        private void OnDestroy()
        {
            if (_camera != null) _camera.targetTexture = null;
            ReleaseTexture();
        }

        private void OnEquipmentChanged() => _builtSignature = -1;

        private void Update()
        {
            // 카메라가 아니라 <b>요청 여부</b>로 판단한다 — 헤드리스(그래픽 장치 없음)에서는 카메라를
            // 켜지 않지만 도형은 그대로 만들어져야 테스트가 배치/포즈를 검증할 수 있다.
            if (!_renderingRequested) return;

            EnsureFigureBuilt();
            ApplyBreathing();
        }

        // ==================== 공개 조작 ====================

        /// <summary>창이 열려 있는 동안만 카메라를 돌린다(닫히면 렌더 비용 0).</summary>
        public void SetRenderingEnabled(bool on)
        {
            _renderingRequested = on;
            if (_camera != null) _camera.enabled = on && _texture != null;
            if (on)
            {
                _builtSignature = -1; // 열 때마다 지금 상태로 다시 굽는다(잉크색/장비가 그 사이 바뀌었을 수 있다).
                EnsureFigureBuilt();
            }
        }

        /// <summary>포즈를 바꾼다. 실제로 달라졌을 때만 도형을 다시 굽는다(24시간 상주 앱).</summary>
        public void SetPose(PortraitPose pose)
        {
            if (_pose == pose) return;
            _pose = pose;
            _builtSignature = -1;
        }

        /// <summary>잉크색/배경을 다시 읽는다(정보창의 [외형] 탭에서 잉크색을 바꿨을 때).</summary>
        public void RefreshTheme()
        {
            if (_camera != null) _camera.backgroundColor = ResolveBackdropColor(_config);
            _builtSignature = -1;
        }

        /// <summary>
        /// 잉크색에 따라 뒤집히는 액자 바탕색 — 흰 잉크에 흰 종이면 선이 보이지 않는다.
        /// 정보창도 테두리 색을 이 판단에 맞춰 고른다(색 결정이 두 곳으로 흩어지지 않게 여기 둔다).
        ///
        /// <para>★ 2026-08-30 (병행 레이아웃 코더 발견, 리더 라우팅) — "종이" 값이 옛 팔레트
        /// <c>#f6f7f9</c>로 남아 있어, 33-1절 신규 팔레트로 칠해진 액자(<see cref="UiChrome.PortraitSurface"/>
        /// = <c>#f4f3ef</c>)와 <b>RT 캡처 영역 경계에 색 이음매</b>가 보였다. 값을 손으로 옮겨 적지 않고
        /// <see cref="UiChrome"/> 토큰을 그대로 읽는다 — 팔레트가 또 바뀌어도 두 표면이 함께 따라간다
        /// (같은 색을 두 곳에 적어 어긋난 것이 애초에 이 결함의 원인이었다).</para>
        /// </summary>
        public static Color ResolveBackdropColor(StickConfig config)
        {
            bool whiteInk = config != null && config.inkColor == StickmanInkColor.White;
            return whiteInk
                ? new Color(0.145f, 0.157f, 0.180f, 1f)   // 목탄 — 흰 잉크용 반전 바탕(33-1절에 대응 토큰 없음)
                : UiChrome.PortraitSurface;               // 종이 — 정보창 액자와 정확히 같은 색
        }

        /// <summary>상태 ID -> 포즈. <b>같은 스냅샷에서 프레즌스 문구와 함께 파생</b>된다 —
        /// 초상화가 서 있는데 문구만 "넘어져 있는 중"인 어긋남을 구조적으로 막는다(원칙 1의 정신을
        /// 이미지에도 적용, 이번 라운드 신규 규칙).</summary>
        public static PortraitPose PoseForState(StickmanStateId id)
        {
            switch (id)
            {
                case StickmanStateId.Runaway:
                    return PortraitPose.Hidden;

                case StickmanStateId.Ragdoll:
                case StickmanStateId.ThrowTumble:
                case StickmanStateId.Getup:
                    return PortraitPose.Fallen;

                // ★ 2026-08-30 사용자 신고 수정: "캐릭터를 잡으면 캐릭터창에서는 가만히 있어야 하는데
                //   옆으로 이상하게 됨". 원인은 실시간 추종이 아니라 <b>이 한 줄의 버킷 선택</b>이었다 —
                //   Dragged가 Fallen에 들어 있어서, 붙잡는 순간 액자 속 인물이 78도 기울고 가로로
                //   0.51유닛 밀려났다(실측: Tests/PlayMode/PortraitDragIndependenceTests).
                //   붙잡힌 캐릭터는 <b>넘어져 있는 것이 아니라 들린 채 버둥거리는 중</b>이고, 프레즌스
                //   문구도 "붙잡혀 있는 중"이라 Fallen("넘어져 있는 중")과는 애초에 어긋나 있었다.
                //   그래서 Busy(정지된 준비 자세, 한쪽 팔을 든 모습)로 옮긴다 — 문구와도 맞고,
                //   무엇보다 <b>액자 속 인물이 똑바로 선 채 고정</b>된다.
                case StickmanStateId.Dragged:

                case StickmanStateId.Attack:
                case StickmanStateId.BattleMinigame:
                case StickmanStateId.Archery:
                case StickmanStateId.Graffiti:
                case StickmanStateId.WindowTheft:
                case StickmanStateId.WindowCrash:
                case StickmanStateId.DesktopTidy:
                case StickmanStateId.BlackholeSummon:
                case StickmanStateId.TodoReminder:
                case StickmanStateId.RodeoCursor:
                case StickmanStateId.ParkourClimb:
                case StickmanStateId.LedgeHang:
                    return PortraitPose.Busy;

                default:
                    return PortraitPose.Standing;
            }
        }

        /// <summary>
        /// 표시 크기(캔버스 유닛)로 RT를 준비한다. 실패하면 false — 호출부는 검은 상자 대신 안내 문구를
        /// 띄운다(리더 지시).
        ///
        /// <para>★★ 2026-08-30 <b>실측 버그 수정</b> — 사용자 신고 "캐릭터 창에서 보이는 캐릭터도 픽셀이
        /// 다 깨져보임". 원인은 안티에일리어싱이 아니라 <b>RT가 표시 크기보다 작았던 것</b>이다.
        /// 이 함수는 예전에 <c>ScreenCoordinateConverter.ResolveDpiScale()</c>의 값을 받았는데, 그 값의
        /// 단위는 <b>OS 포인트 / Unity 픽셀</b>이라 <b>Retina에서 2가 아니라 0.5</b>다
        /// (<c>AutoDpiScale = 창 폭(포인트) / Screen.width(픽셀)</c> = 1512/3024). 그 결과:
        /// <code>
        ///   액자 표시 크기 188 캔버스유닛 × 캔버스 scaleFactor 2 = 376 물리 픽셀로 표시
        ///   RT 크기 = 188 × 0.5(잘못된 배율) × 2(슈퍼샘플) = 188 픽셀
        ///   -> 376픽셀 자리에 188픽셀 텍스처를 늘려 붙였다. 슈퍼샘플 2배가 아니라 <b>0.5배 축소</b>였고
        ///      면적으로는 의도의 1/16이다. 계단이 보이는 것이 당연하다.
        /// </code>
        /// 필요한 배율은 "캔버스 유닛 -> Unity 픽셀"인 <see cref="ScreenCoordinateConverter.ResolveCanvasScaleFactor"/>
        /// 다(같은 값을 <c>CanvasScaler.scaleFactor</c>에도 넣는다). 파라미터 이름도 단위가 드러나게 바꿔
        /// 같은 혼동이 다시 나지 않게 한다 — 두 값은 서로 역수라 <b>틀려도 컴파일도 되고 그림도 나온다</b>.
        /// </para>
        /// </summary>
        /// <param name="pixelsPerCanvasUnit">캔버스 1유닛이 몇 Unity 픽셀인가(= <c>CanvasScaler.scaleFactor</c>).
        /// Retina에서 2, 비Retina에서 1.</param>
        public bool TryEnsureTexture(float displayWidth, float displayHeight, float pixelsPerCanvasUnit)
        {
            // ★ 헤드리스(-batchmode -nographics)에서는 오프스크린 카메라를 절대 켜지 않는다.
            //   실측: PlayMode 테스트가 EXIT=139로 죽었고 네이티브 스택이 정확히
            //   RenderManager::RenderOffscreenCameras -> DrawLineOrTrail... 이었다(GfxDevice가 없는데
            //   RT에 선을 그리려다 크래시). 이 프로젝트는 같은 계열의 사고를 이미 겪었다
            //   (UniWindowController의 _findMyWindow가 NSWindow 없는 배치 모드에서 크래시).
            //   RT가 없으면 카메라는 꺼진 채이고 정보창은 안내 문구를 띄운다.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                return false;
            }

            if (pixelsPerCanvasUnit <= 0f || float.IsNaN(pixelsPerCanvasUnit)) pixelsPerCanvasUnit = 1f;
            int w = Mathf.Clamp(Mathf.RoundToInt(displayWidth * pixelsPerCanvasUnit) * Supersample, 32, MaxTextureSide);
            int h = Mathf.Clamp(Mathf.RoundToInt(displayHeight * pixelsPerCanvasUnit) * Supersample, 32, MaxTextureSide);
            if (_texture != null && _texture.width == w && _texture.height == h && _texture.IsCreated()) return true;

            ReleaseTexture();
            var rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "CharacterPortraitRT",
                filterMode = FilterMode.Bilinear,
                // RenderTexture는 QualitySettings.antiAliasing을 <b>상속하지 않는다</b>(생성 인자를 안 주면
                // 항상 1 = MSAA 없음). 여기서는 그래도 1로 둔다 — 2배 슈퍼샘플 축소가 이 선화에는
                // MSAA보다 낫고(2026-08-29 "선 화질 조사"), MSAA를 켜면 해상 단계가 하나 더 늘어
                // 상주 앱의 메모리만 커진다. 대신 <b>슈퍼샘플이 실제로 걸리는지</b>가 중요하다 —
                // 위 파라미터 단위 사고가 정확히 그것을 무너뜨렸다.
                antiAliasing = 1,
                autoGenerateMips = false,
            };

            if (!rt.Create())
            {
                Debug.LogWarning("[초상화] RenderTexture를 만들지 못했습니다 — 정보창은 안내 문구로 대체합니다.");
                Destroy(rt);
                return false;
            }

            _texture = rt;
            if (_camera != null)
            {
                _camera.targetTexture = _texture;
                _camera.aspect = (float)w / h;
            }
            _builtSignature = -1;
            return true;
        }

        private void ReleaseTexture()
        {
            if (_texture == null) return;
            if (_camera != null) _camera.targetTexture = null;
            _texture.Release();
            Destroy(_texture);
            _texture = null;
        }

        // ==================== 촬영장 구성 ====================

        private void BuildCamera()
        {
            float h = TotalHeight;

            var camGo = new GameObject("PortraitCamera");
            camGo.transform.SetParent(transform, false);
            camGo.transform.localPosition = new Vector3(0f, h * FrameCenterHeightRatio, -10f);
            _camera = camGo.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = h * FrameOrthoRatio;
            // ★ RT가 생기기 전에도 종횡비를 못박는다. 안 그러면 그때까지 화면 해상도(헤드리스에서는
            //   배치 모드 기본값)가 액자 구도를 좌우해, 넘어짐 프레이밍이 실기와 테스트에서 달라진다.
            _camera.aspect = DesignAspect;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = ResolveBackdropColor(_config);
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 50f;
            _camera.depth = -50f;                    // 메인 카메라보다 먼저 그려도 무방(별도 타깃).
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            _camera.useOcclusionCulling = false;
            _camera.enabled = false;

            var figureGo = new GameObject("MiniFigure");
            figureGo.transform.SetParent(transform, false);
            _figureRoot = figureGo.transform;
            _baseFigureY = 0f;
        }

        /// <summary>액자의 설계 종횡비(가로/세로). 숫자를 새로 적지 않고 정보창 레이아웃에서 파생시킨다 —
        /// 액자 크기를 바꾼 사람이 여기를 같이 고치는 일을 기대하면 언젠가 반드시 어긋난다.</summary>
        public static float DesignAspect
        {
            get
            {
                Vector2 size = CharacterInfoWindow.PortraitContentSize;
                return size.y > 0.01f ? size.x / size.y : 1f;
            }
        }

        /// <summary>촬영장 로컬 좌표에서의 액자 중심(= 카메라가 보고 있는 점).</summary>
        private Vector2 FrameCenterLocal => _camera != null
            ? (Vector2)_camera.transform.localPosition
            : new Vector2(0f, TotalHeight * FrameCenterHeightRatio);

        /// <summary>액자 가시 사각형의 반폭/반높이(유닛). 넘어짐 프레이밍이 "여기 안에 들어오는가"를
        /// 판단하는 유일한 기준이다.</summary>
        private Vector2 FrameHalfExtents
        {
            get
            {
                float half = _camera != null ? _camera.orthographicSize : TotalHeight * FrameOrthoRatio;
                float aspect = _camera != null && _camera.aspect > 0.01f ? _camera.aspect : DesignAspect;
                return new Vector2(half * aspect, half);
            }
        }

        private float TotalHeight => _metrics != null ? _metrics.TotalHeight : StickConfig.BaselineCharacterTotalHeight;
        private float HeadRadius => _metrics != null ? _metrics.HeadRadius : 0.22f;
        private float HeadCenterY => _metrics != null ? _metrics.HeadCenterLocalY : TotalHeight - HeadRadius;
        private float ShoulderY => _metrics != null ? _metrics.ShoulderLocalY
            : TotalHeight * (1.7646944f / StickConfig.BaselineCharacterTotalHeight);
        private float HipY => _metrics != null ? _metrics.HipLocalY
            : TotalHeight * (0.9346944f / StickConfig.BaselineCharacterTotalHeight);
        private float Stroke => TotalHeight * StrokeWidthRatio;

        private void ApplyBreathing()
        {
            if (_figureRoot == null) return;
            float amp = _pose == PortraitPose.Standing ? TotalHeight * BreathAmplitudeRatio : 0f;
            float y = _baseFigureY + Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / BreathPeriodSeconds) * amp;
            Vector3 p = _figureRoot.localPosition;
            if (!Mathf.Approximately(p.y, y)) _figureRoot.localPosition = new Vector3(p.x, y, p.z);
        }

        private void EnsureFigureBuilt()
        {
            int signature = ComputeSignature();
            if (signature == _builtSignature) return;
            _builtSignature = signature;
            Rebuild();
        }

        /// <summary>
        /// ★ 2026-08-30 — 캐릭터 렌더러와 <b>정확히 같은 결함</b>을 여기서도 고쳤다.
        /// 카테고리 비트마스크는 "천 모자 -> 왕관"처럼 <b>같은 카테고리 안에서 아이템만 바뀌는</b>
        /// 경우를 못 잡는다(마스크가 그대로다). 캐릭터는 안 바뀌는데 초상화만 옛 모자를 쓰고 있거나
        /// 그 반대가 되는, 찾기 어려운 불일치가 된다.
        /// <see cref="EquipmentModel.WornStateSignature"/>는 아이템 자리까지 섞는다.
        /// </summary>
        private int ComputeSignature()
        {
            int hash = EquipmentModel.WornStateSignature;

            int unlockedMask = 0;
            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                if (EquipmentModel.IsUnlocked((EquipmentSlot)i)) unlockedMask |= 1 << i;
            }
            hash = hash * 31 + unlockedMask;
            hash = hash * 31 + (int)_pose;
            hash = hash * 31 + ResolveInk().GetHashCode();
            return hash;
        }

        private Color ResolveInk() => _config != null ? _config.ResolveInkColor() : Color.black;

        private void Rebuild()
        {
            for (int i = 0; i < _lines.Count; i++)
            {
                if (_lines[i] != null) Destroy(_lines[i].gameObject);
            }
            _lines.Clear();

            for (int i = 0; i < _fillObjects.Count; i++)
            {
                if (_fillObjects[i] != null) Destroy(_fillObjects[i]);
            }
            _fillObjects.Clear();
            for (int i = 0; i < _fillMeshes.Count; i++)
            {
                if (_fillMeshes[i] != null) Destroy(_fillMeshes[i]);
            }
            _fillMeshes.Clear();

            if (_figureRoot == null) return;
            _figureRoot.localRotation = Quaternion.identity;
            _figureRoot.localPosition = Vector3.zero;
            _figureRoot.localScale = Vector3.one;
            _baseFigureY = 0f;

            if (_pose == PortraitPose.Hidden) return; // 가출 — 액자를 비운다.

            Color ink = ResolveInk();
            float armSpread = _pose == PortraitPose.Busy ? 64f : IdleArmSpreadDegrees;
            float backArmSpread = _pose == PortraitPose.Busy ? 128f : IdleArmSpreadDegrees; // 한쪽 팔을 든다.

            DrawBody(ink, armSpread, backArmSpread);
            DrawAccessories(ink);
            DrawAppearancePreview(ink);

            // 눕히기는 <b>다 그린 뒤</b>에 한다 — 얼마나 큰 그림인지 알아야 액자에 넣을 수 있다.
            if (_pose == PortraitPose.Fallen) FrameFallenFigure();
        }

        /// <summary>
        /// ★ 넘어짐 프레이밍 — 2026-08-30 "초상화에서 머리가 잘린다" 결함 수정.
        ///
        /// 옛 구현은 <b>발을 회전축</b>으로 몸을 눕혔다. 발은 로컬 원점이라 회전해도 제자리인데 머리는
        /// 원점에서 키만큼(회전 반경 = 키) 떨어져 있어, 눕히는 순간 머리만 액자 밖으로 쓸려나갔다
        /// (실측: 머리 중심이 가시 x범위 밖으로 0.074×키. 증거 사진 Logs/evidence_20260830_portrait_drag/1_*.png).
        ///
        /// 그래서 회전축을 <b>그림 자체의 중심</b>으로 옮긴다. 중심을 "키의 절반"으로 가정하지 않고
        /// 방금 그린 선에서 실측하는 이유는 모자/망토가 몸 밖으로 나가기 때문이다 — 모자를 쓰면
        /// 그림 중심이 0.482×키에서 0.518×키로 움직인다(가정값 0.5는 둘 다 정확히는 틀린다).
        ///
        /// 그리고 눕힌 몸은 가로로 키만큼 길어지는데 액자 가시 폭은 키의 1.02배뿐이다. 회전축만 고쳐도
        /// 머리 원은 들어오지만 발끝 선 굵기가 반대쪽으로 삐져나가고, 모자를 쓰면 챙이 통째로 잘린다.
        /// 그래서 넘치는 만큼만 균일 축소한다(넘치지 않으면 배율 1 그대로 — 평소에는 아무 일도 안 한다).
        /// </summary>
        private void FrameFallenFigure()
        {
            var rotation = Quaternion.Euler(0f, 0f, FallenLayDownDegrees);
            if (!TryMeasureRotatedInk(rotation, out Vector2 inkMin, out Vector2 inkMax)) return;

            Vector2 inkSize = inkMax - inkMin;
            Vector2 half = FrameHalfExtents;

            float scale = 1f;
            if (inkSize.x > 0.0001f) scale = Mathf.Min(scale, half.x * 2f * FallenFrameFill / inkSize.x);
            if (inkSize.y > 0.0001f) scale = Mathf.Min(scale, half.y * 2f * FallenFrameFill / inkSize.y);

            // 액자 안 목표 지점 — 가로는 정중앙, 세로는 아래쪽(누운 사람이 공중에 뜨지 않게).
            Vector2 frameCenter = FrameCenterLocal;
            var target = new Vector2(
                frameCenter.x,
                frameCenter.y - half.y + half.y * 2f * FallenFrameCenterFromBottom);

            // 회전축(= 그림의 중심)이 회전·축소 뒤 정확히 target에 오도록 루트 위치를 역산한다.
            Vector2 rotatedPivot = (inkMin + inkMax) * 0.5f * scale;

            _figureRoot.localRotation = rotation;
            _figureRoot.localScale = new Vector3(scale, scale, 1f);
            _baseFigureY = target.y - rotatedPivot.y;
            _figureRoot.localPosition = new Vector3(target.x - rotatedPivot.x, _baseFigureY, 0f);
        }

        /// <summary>방금 그린 선 전체를 <paramref name="rotation"/>만 적용해 재고, 선 굵기의 절반만큼
        /// 부풀린 사각형을 돌려준다(획의 바깥쪽까지가 "보이는 그림"이다). Rebuild에서만 돈다 —
        /// 매 프레임 경로가 아니므로 순회 비용을 감수해도 된다.</summary>
        private bool TryMeasureRotatedInk(Quaternion rotation, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);

            bool any = false;
            for (int i = 0; i < _lines.Count; i++)
            {
                LineRenderer lr = _lines[i];
                if (lr == null) continue;
                int count = lr.positionCount;
                for (int p = 0; p < count; p++)
                {
                    Vector3 q = rotation * lr.GetPosition(p);
                    if (q.x < min.x) min.x = q.x;
                    if (q.y < min.y) min.y = q.y;
                    if (q.x > max.x) max.x = q.x;
                    if (q.y > max.y) max.y = q.y;
                    any = true;
                }
            }
            if (!any) return false;

            float pad = Stroke * 0.5f;
            min -= new Vector2(pad, pad);
            max += new Vector2(pad, pad);
            return true;
        }

        private void DrawBody(Color ink, float frontArmSpread, float backArmSpread)
        {
            float h = TotalHeight;
            float r = HeadRadius;

            // ★ "Head"/"Torso"라는 이름을 여기서 그대로 쓰는 것이 안전한 이유(2026-08-30 R3 m1 재확인):
            // 이 도형들은 <b>캐릭터 계층 밖</b>에 산다. Create()가 촬영장을 씬 루트 GameObject로 만들고
            // (CharacterInfoWindow는 이걸 재부모화하지 않는다), 이 선들은 그 아래 "MiniFigure"의 자식이다
            // (AddLine이 _figureRoot에 붙인다). 이름으로 캐릭터 파츠를 찾는 코드는 전부 캐릭터 루트에서
            // 출발하므로 여기에 닿을 수 없다 — 같은 날의 "Head" 회귀를 낸 것은 <b>캐릭터 루트 밑에</b>
            // 캔버스를 달았던 부채꼴 메뉴였고, 이 파일은 원인이 아니었다.
            // 이름을 바꾸지 않는 이유: PortraitFallenFramingTests가 figure.Find("Head")로 이 원을 실측해
            // 액자 밖으로 잘리는지 검사한다(개명은 이득 없이 그 잠금장치를 끊는다).
            AddCircle("Head", Vector2.up * HeadCenterY, r, ink, 28);
            AddLine("Torso", new[] { V(0f, HeadCenterY - r), V(0f, HipY) }, ink, loop: false);

            DrawLimb("ArmBack", new Vector2(0f, ShoulderY), -backArmSpread, IdleElbowBendDegrees,
                h * ArmUpperRatio, h * ArmLowerRatio, ink);
            DrawLimb("ArmFront", new Vector2(0f, ShoulderY), frontArmSpread, IdleElbowBendDegrees,
                h * ArmUpperRatio, h * ArmLowerRatio, ink);
            DrawLimb("LegBack", new Vector2(0f, HipY), -IdleLegSpreadDegrees, IdleKneeBendDegrees,
                h * LegUpperRatio, h * LegLowerRatio, ink);
            DrawLimb("LegFront", new Vector2(0f, HipY), IdleLegSpreadDegrees, IdleKneeBendDegrees,
                h * LegUpperRatio, h * LegLowerRatio, ink);

            // 눈 — 선글라스를 쓰면 렌즈에 가려지므로 그리지 않는다(실제 캐릭터와 같은 겹침 관계).
            if (EquippedAndUnlocked(EquipmentSlot.Eyes)) return;
            float eyeX = r * AccessoryShapeBuilder.EyeOffsetXInHeadRadii;
            float eyeY = HeadCenterY + r * AccessoryShapeBuilder.EyeOffsetYInHeadRadii;
            float eyeR = h * EyeRadiusRatio;
            AddCircle("EyeBack", new Vector2(-eyeX, eyeY), eyeR, ink, 8);
            AddCircle("EyeFront", new Vector2(eyeX, eyeY), eyeR, ink, 8);
        }

        /// <summary>2분절 마디. 각도는 아래 방향을 0으로 보고 x쪽으로 벌어지는 각
        /// (Editor/SceneBootstrapper.LimbDrop과 같은 규약).</summary>
        private void DrawLimb(string name, Vector2 root, float spreadDegrees, float bendDegrees,
            float upperLength, float lowerLength, Color ink)
        {
            float a1 = spreadDegrees * Mathf.Deg2Rad;
            float a2 = (spreadDegrees + bendDegrees) * Mathf.Deg2Rad;
            var joint = new Vector2(root.x + Mathf.Sin(a1) * upperLength, root.y - Mathf.Cos(a1) * upperLength);
            var tip = new Vector2(joint.x + Mathf.Sin(a2) * lowerLength, joint.y - Mathf.Cos(a2) * lowerLength);
            AddLine(name, new[] { V(root.x, root.y), V(joint.x, joint.y), V(tip.x, tip.y) }, ink, loop: false);
        }

        /// <summary>
        /// ★ 도형은 실제 캐릭터와 <b>같은 코드</b>에서 나온다(AccessoryShapeBuilder) — 이중 정의 금지.
        /// 2026-08-30 32종 확장에서 슬롯별 if 사다리를 순회로 바꿨다: 카테고리가 8개가 되면서
        /// 하나를 빠뜨려도 컴파일은 통과하고 <b>초상화에서만 조용히 사라지는</b> 구조가 됐기 때문이다.
        /// 머리(HAIR)도 여기서 함께 그려진다 — 정보창에서 고른 것이 초상화에 안 나오면
        /// 그건 "골랐다"가 아니다.
        /// <para>초상화는 언제나 정면(facing +1)이고 <b>흔들지 않는다</b> — 액자 속 인물이 걷고 있지
        /// 않으므로 HemSway를 적용하면 그림과 상태가 어긋난다(원칙 1).</para>
        /// </summary>
        private void DrawAccessories(Color ink)
        {
            var rig = new AccessoryShapeBuilder.Rig(HeadRadius, HeadCenterY, ShoulderY, HipY, 1f);

            _shapes.Clear();
            float cover = EquippedAndUnlocked(EquipmentSlot.Head)
                ? AccessoryShapeBuilder.HatCoverLocalY(EquipmentModel.WornIndex(EquipmentSlot.Head), rig)
                : float.PositiveInfinity;

            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                var slot = (EquipmentSlot)i;
                if (!EquippedAndUnlocked(slot)) continue;

                int item = EquipmentModel.WornIndex(slot);
                ItemCatalog.ResolveWornPalette(slot, item, ink, out Color primary, out Color secondary);

                int start = _shapes.Count;
                AccessoryShapeBuilder.Append(_shapes, slot, item, rig, cover, Stroke * 0.5f, IsMondayForTie);
                for (int k = start; k < _shapes.Count; k++)
                {
                    AccessoryShapeBuilder.Shape shape = _shapes[k];
                    Color color = ToneColor(shape.Tone, primary, secondary);

                    // 채움 면(모자류)은 실제 캐릭터와 같은 규칙 — 윤곽선 바로 아래에 깔고 윤곽은 어둡게.
                    Color outline = color;
                    if (shape.Filled)
                    {
                        AddFill(shape, color);
                        outline = AccessoryShapeBuilder.FillOutlineColor(color);
                    }
                    AddLine(shape.Name, shape.Points, outline, shape.Loop, shape.SortingOrder);
                }
            }
        }

        // ============================================================================
        // ★ FX / 펫 정적 미리보기 — 2026-08-30 사용자 신고
        //   "캐릭터 설정창에서 발자국이나, 공 이런건 왼쪽 캐릭터에서 미리보기로 보여줘야하는데 안보여짐"
        // ============================================================================
        // FX/펫은 실시간 캐릭터에만 붙어 있었다(발자국은 보폭마다, 공은 주인을 따라 구른다). 그래서
        // 정보창에서 골라도 액자에는 아무 일도 일어나지 않았다 — 사용자 입장에서는 "고른 게 아니다".
        //
        // 여기서는 <b>움직임을 재현하지 않는다</b>. 액자 속 인물은 걷고 있지 않으므로 발자국이
        // 찍히는 순간도, 공이 굴러오는 궤적도 있을 수 없다(원칙 1의 그림 버전 — 상태에 없는 동작을
        // 그리지 않는다). 대표 한 컷만 정지 화면으로 놓는다: 발자국 2개, 반짝임 2개, 먼지 한 뭉치,
        // 펫 1마리. 크기는 실물과 같은 상수(AppearanceShapeBuilder)에서 나오므로 "이만한 게 생긴다"가
        // 그대로 읽힌다.
        //
        // 놓는 자리는 겹치지 않게 좌우로 나눈다 — FX는 왼쪽(지나온 쪽), 펫은 오른쪽.
        private const float FxPreviewXRatio = -0.26f;   // 신장 배수
        private const float PetPreviewXRatio = 0.38f;

        /// <summary>미리보기 레이어. 펫/FX는 몸에 붙은 것이 아니므로 몸통 획(0~2) 뒤에 둔다.</summary>
        private const int PreviewSortingOrder = -3;

        /// <summary>
        /// <para>넘어짐/가출 포즈에서는 그리지 않는다. 넘어짐은 다 그린 뒤 도형 전체를 눕혀 액자에
        /// 맞추는데(<see cref="FrameFallenFigure"/>), 발자국까지 함께 누우면 "땅에 찍힌 자국"이라는
        /// 뜻이 사라지고 액자 프레이밍도 미리보기 크기에 끌려간다.</para>
        /// </summary>
        private void DrawAppearancePreview(Color ink)
        {
            if (_pose != PortraitPose.Standing && _pose != PortraitPose.Busy) return;

            DrawFxPreview(ink);
            DrawPetPreview(ink);
        }

        /// <summary>FX는 캐릭터가 <b>자기 펜으로 남기는 자국</b>이라 실시간 렌더러와 같이 잉크색으로
        /// 그린다(Interaction/CharacterFxRenderer.cs와 같은 규약 — 미리보기가 실물과 달라지면 안 된다).</summary>
        private void DrawFxPreview(Color ink)
        {
            if (!EquippedAndUnlocked(EquipmentSlot.Fx)) return;
            int item = EquipmentModel.WornIndex(EquipmentSlot.Fx);

            float h = TotalHeight;
            float r = HeadRadius;
            float x = h * FxPreviewXRatio;

            switch (item)
            {
                case AppearanceShapeBuilder.FxFootprint:
                {
                    float radius = Stroke * 0.9f;
                    AddDotPreview("FxFootprintA", x, radius, ink);
                    AddDotPreview("FxFootprintB", x - h * 0.11f, radius, ink);
                    break;
                }

                case AppearanceShapeBuilder.FxSparkle:
                {
                    float arm = r * AppearanceShapeBuilder.SparkleArmInR;
                    AddSparklePreview("FxSparkleA", x, HeadCenterY + r * 1.15f, arm, ink);
                    AddSparklePreview("FxSparkleB", x - h * 0.09f, HeadCenterY + r * 0.35f, arm * 0.7f, ink);
                    break;
                }

                case AppearanceShapeBuilder.FxDust:
                {
                    float radius = r * 0.5f;
                    for (int i = 0; i < 2; i++)
                    {
                        Vector3[] pts = AppearanceShapeBuilder.DustCrescent(radius, i);
                        Offset(pts, x, Stroke);
                        AddLine(i == 0 ? "FxDustA" : "FxDustB", pts, ink, false, PreviewSortingOrder);
                    }
                    break;
                }
            }
        }

        /// <summary>펫은 <b>물건</b>이라 자기 색을 갖는다(빨간 공, 종이 비행기) —
        /// 색표는 Core/ItemCatalog 하나뿐이고 실시간 펫도 같은 값을 읽는다.</summary>
        private void DrawPetPreview(Color ink)
        {
            if (!EquippedAndUnlocked(EquipmentSlot.Pet)) return;
            int item = EquipmentModel.WornIndex(EquipmentSlot.Pet);
            ItemCatalog.ResolveWornPalette(EquipmentSlot.Pet, item, ink, out Color primary, out Color secondary);

            float h = TotalHeight;
            float r = HeadRadius;
            float x = h * PetPreviewXRatio;

            switch (item)
            {
                case AppearanceShapeBuilder.PetBall:
                {
                    float radius = h * AppearanceShapeBuilder.BallRadiusInHeight;
                    AddPreviewLine("PetBallRing", AppearanceShapeBuilder.BallRing(radius, 12), x, radius,
                        true, primary);
                    // 반지름 선은 실물과 같이 그린다(굴러가면 이 선이 회전을 읽히게 한다).
                    AddPreviewLine("PetBallSpoke", AppearanceShapeBuilder.BallSpoke(radius), x, radius,
                        false, secondary);
                    break;
                }

                case AppearanceShapeBuilder.PetPlane:
                {
                    float span = r * AppearanceShapeBuilder.PlaneWingSpanInR;
                    float y = HeadCenterY + r * 0.60f;
                    AddPreviewLine("PetPlaneBody", AppearanceShapeBuilder.PlaneBody(span), x, y, true, primary);
                    AddPreviewLine("PetPlaneFold", AppearanceShapeBuilder.PlaneFold(span), x, y, false, secondary);
                    break;
                }

                case AppearanceShapeBuilder.PetMini:
                {
                    // 정면(facing +1) — 액자 속 인물과 같은 방향을 본다.
                    Vector3[][] parts = AppearanceShapeBuilder.MiniFigure(h * AppearanceShapeBuilder.MiniScale, 1f);
                    for (int i = 0; i < parts.Length; i++)
                    {
                        AddPreviewLine("PetMini" + i, parts[i], x, 0f, i == 0, primary);
                    }
                    break;
                }

                case AppearanceShapeBuilder.PetCursor:
                {
                    float size = r * AppearanceShapeBuilder.CursorSizeInR;
                    AddPreviewLine("PetCursor", AppearanceShapeBuilder.CursorArrow(size),
                        x, HeadCenterY + r * 0.95f, false, primary);
                    break;
                }
            }
        }

        private void AddDotPreview(string name, float x, float radius, Color ink)
        {
            Vector3[] pts = AppearanceShapeBuilder.DotSegment(radius);
            Offset(pts, x, 0f);
            AddLine(name, pts, ink, false, PreviewSortingOrder, radius * 2f);
        }

        private void AddSparklePreview(string name, float x, float y, float arm, Color ink)
        {
            for (int i = 0; i < 2; i++)
            {
                Vector3[] pts = AppearanceShapeBuilder.SparkleStroke(arm, i);
                Offset(pts, x, y);
                AddLine(name + i, pts, ink, false, PreviewSortingOrder);
            }
        }

        private void AddPreviewLine(string name, Vector3[] points, float x, float y, bool loop, Color color)
        {
            Offset(points, x, y);
            AddLine(name, points, color, loop, PreviewSortingOrder);
        }

        /// <summary>도형은 자기 원점 기준으로 만들어지므로(실시간 렌더러가 오브젝트를 옮겨 놓는다)
        /// 정적 미리보기는 점을 직접 옮긴다 — 오브젝트를 하나 더 두지 않기 위해서다.</summary>
        private static void Offset(Vector3[] points, float dx, float dy)
        {
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new Vector3(points[i].x + dx, points[i].y + dy, points[i].z);
            }
        }

        /// <summary>재구성 때만 쓰는 조립 버퍼(매 프레임 경로가 아니다).</summary>
        private readonly List<AccessoryShapeBuilder.Shape> _shapes = new List<AccessoryShapeBuilder.Shape>(16);

        /// <summary>33-2-5 (D) 줄무늬 타이의 요일 상태. 재구성은 포즈/장비/색이 바뀔 때만 도므로
        /// 여기서는 캐싱 없이 그때 한 번만 읽는다(캐릭터 렌더러 쪽은 매 프레임 경로라 캐싱한다).</summary>
        private static bool IsMondayForTie => System.DateTime.Now.DayOfWeek == System.DayOfWeek.Monday;

        private bool EquippedAndUnlocked(EquipmentSlot slot)
            => EquipmentModel.IsEquipped(slot) && EquipmentModel.IsUnlocked(slot);

        private static Vector3 V(float x, float y) => new Vector3(x, y, 0f);

        /// <summary>도형의 <b>역할</b>을 색으로. 실제 캐릭터(CharacterAccessoryRenderer)와 같은 표다.</summary>
        private static Color ToneColor(byte tone, Color primary, Color secondary)
        {
            if (tone == AccessoryShapeBuilder.Accent) return secondary;
            if (tone == AccessoryShapeBuilder.Shade) return AccessoryShapeBuilder.FillOutlineColor(primary);
            return primary;
        }

        /// <summary>채움 면 하나(모자류). 미니 피규어를 통째로 다시 만들 때 함께 지워지도록
        /// 메시를 <see cref="_fillMeshes"/>가 들고 있는다 — GameObject를 지워도 메시는 남는다.</summary>
        private void AddFill(in AccessoryShapeBuilder.Shape shape, Color color)
        {
            Mesh mesh = AccessoryShapeBuilder.BuildFillMesh(shape.Points, color);
            if (mesh == null) return;

            var go = new GameObject(shape.Name + "Fill");
            go.transform.SetParent(_figureRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _lineMaterial;
            mr.sortingOrder = shape.FillSortingOrder;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            _fillMeshes.Add(mesh);
            _fillObjects.Add(go);
        }

        private readonly List<Mesh> _fillMeshes = new List<Mesh>(4);
        private readonly List<GameObject> _fillObjects = new List<GameObject>(4);

        private void AddCircle(string name, Vector2 center, float radius, Color ink, int segments)
        {
            var points = new Vector3[segments];
            float step = Mathf.PI * 2f / segments;
            for (int i = 0; i < segments; i++)
            {
                points[i] = new Vector3(center.x + Mathf.Cos(step * i) * radius, center.y + Mathf.Sin(step * i) * radius, 0f);
            }
            AddLine(name, points, ink, loop: true);
        }

        /// <param name="sortingOrder">33-2-0의 레이어 재배치표. 미니 피규어도 같은 순서를 써야
        /// "화면 속 캐릭터"와 겹침 관계가 같아진다(망토가 몸 뒤로 간다).</param>
        /// <param name="width">획 두께 override(0 이하면 캐릭터 획과 같은 <see cref="Stroke"/>).
        /// 발자국처럼 <b>굵은 캡이 곧 점</b>인 도형만 이 인자를 쓴다.</param>
        private void AddLine(string name, Vector3[] points, Color ink, bool loop, int sortingOrder = 0,
            float width = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_figureRoot, false);

            float stroke = width > 0f ? width : Stroke;
            var lr = go.AddComponent<LineRenderer>();
            lr.sortingOrder = sortingOrder;
            lr.useWorldSpace = false;
            lr.material = _lineMaterial;
            lr.startColor = ink;
            lr.endColor = ink;
            lr.startWidth = stroke;
            lr.endWidth = stroke;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.loop = loop;
            lr.positionCount = points.Length;
            lr.SetPositions(points);
            _lines.Add(lr);
        }
    }
}
