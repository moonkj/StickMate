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
    /// ProjectSettings 변경 0건으로 같은 격리를 얻는다(라이벌 대기 좌표 x=500과도 한참 떨어져 있다).
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
        /// <summary>촬영장 좌표. 메인 카메라 가시 범위(약 ±16유닛)와 라이벌 대기 좌표(500)에서 한참 멀다.</summary>
        public const float StageWorldX = 10000f;

        /// <summary>RT를 화면 표시 크기의 몇 배로 찍을 것인가. 2배로 찍어 축소 표시하면 MSAA 없이도
        /// 대각선 획이 매끄럽다(2026-08-29 "선 화질 조사" 라운드에서 MSAA 8x가 오히려 함정이었다).</summary>
        private const int Supersample = 2;

        private const int MaxTextureSide = 2048;

        // 팔다리 길이/자세 — Editor/SceneBootstrapper.cs의 배율 1.0 기준값과 같은 값이어야 "화면 속
        // 캐릭터"와 초상화가 같은 몸으로 읽힌다(관절 위치는 StickmanMetrics 실측을 쓰므로 여기 있는
        // 것은 마디 길이와 중립 각도뿐이다).
        private const float ArmUpperRatio = 0.38f / StickConfig.BaselineCharacterTotalHeight;
        private const float ArmLowerRatio = 0.37f / StickConfig.BaselineCharacterTotalHeight;
        private const float LegUpperRatio = 0.50f / StickConfig.BaselineCharacterTotalHeight;
        private const float LegLowerRatio = 0.45f / StickConfig.BaselineCharacterTotalHeight;
        private const float EyeOffsetXRatio = 0.075f / StickConfig.BaselineCharacterTotalHeight;
        private const float EyeOffsetYRatio = 0.02f / StickConfig.BaselineCharacterTotalHeight;
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
        // ────────────────────────────────────────────────────────────────────────
        private const float FrameCenterHeightRatio = 0.58f;
        private const float FrameOrthoRatio = 0.62f;   // 모자 여유분까지 담기는 최소 크기 + 약간의 여백.

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

        /// <summary>잉크색에 따라 뒤집히는 액자 바탕색 — 흰 잉크에 흰 종이면 선이 보이지 않는다.
        /// 정보창도 테두리 색을 이 판단에 맞춰 고른다(색 결정이 두 곳으로 흩어지지 않게 여기 둔다).</summary>
        public static Color ResolveBackdropColor(StickConfig config)
        {
            bool whiteInk = config != null && config.inkColor == StickmanInkColor.White;
            return whiteInk
                ? new Color(0.145f, 0.157f, 0.180f, 1f)   // 목탄
                : new Color(0.965f, 0.969f, 0.976f, 1f);  // 종이
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

        /// <summary>표시 크기(캔버스 유닛)와 화면 배율로 RT를 준비한다. 실패하면 false —
        /// 호출부는 검은 상자 대신 안내 문구를 띄운다(리더 지시).</summary>
        public bool TryEnsureTexture(float displayWidth, float displayHeight, float dpiScale)
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

            int w = Mathf.Clamp(Mathf.RoundToInt(displayWidth * dpiScale) * Supersample, 32, MaxTextureSide);
            int h = Mathf.Clamp(Mathf.RoundToInt(displayHeight * dpiScale) * Supersample, 32, MaxTextureSide);
            if (_texture != null && _texture.width == w && _texture.height == h && _texture.IsCreated()) return true;

            ReleaseTexture();
            var rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "CharacterPortraitRT",
                filterMode = FilterMode.Bilinear,
                antiAliasing = 1,   // 2배 슈퍼샘플로 대신한다(위 Supersample 문서 참고).
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

        private int ComputeSignature()
        {
            int mask = 0;
            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                var slot = (EquipmentSlot)i;
                if (EquipmentModel.IsEquipped(slot) && EquipmentModel.IsUnlocked(slot, _config)) mask |= 1 << i;
            }
            mask |= (int)_pose << 8;
            mask ^= ResolveInk().GetHashCode();
            return mask;
        }

        private Color ResolveInk() => _config != null ? _config.ResolveInkColor() : Color.black;

        private void Rebuild()
        {
            for (int i = 0; i < _lines.Count; i++)
            {
                if (_lines[i] != null) Destroy(_lines[i].gameObject);
            }
            _lines.Clear();

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
            float eyeY = HeadCenterY + h * EyeOffsetYRatio;
            float eyeR = h * EyeRadiusRatio;
            AddCircle("EyeBack", new Vector2(-h * EyeOffsetXRatio, eyeY), eyeR, ink, 8);
            AddCircle("EyeFront", new Vector2(h * EyeOffsetXRatio, eyeY), eyeR, ink, 8);
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

        private void DrawAccessories(Color ink)
        {
            // ★ 도형은 실제 캐릭터와 <b>같은 코드</b>에서 나온다(AccessoryShapeBuilder) — 이중 정의 금지.
            var rig = new AccessoryShapeBuilder.Rig(HeadRadius, HeadCenterY, ShoulderY, HipY, 1f);

            if (EquippedAndUnlocked(EquipmentSlot.Shoulders))
            {
                AddLine("CapeOutline", AccessoryShapeBuilder.CapeOutline(rig), ink, loop: true);
                AddLine("CapeFold", AccessoryShapeBuilder.CapeFold(rig), ink, loop: false);
            }
            if (EquippedAndUnlocked(EquipmentSlot.Neck))
            {
                AddLine("BowTieLeft", AccessoryShapeBuilder.BowTieLeftWing(rig), ink, loop: false);
                AddLine("BowTieRight", AccessoryShapeBuilder.BowTieRightWing(rig), ink, loop: false);
                AddLine("BowTieKnot", AccessoryShapeBuilder.BowTieKnot(rig), ink, loop: true);
            }
            if (EquippedAndUnlocked(EquipmentSlot.Head))
            {
                AddLine("HatCrown", AccessoryShapeBuilder.HatCrown(rig), ink, loop: true);
                AddLine("HatBrim", AccessoryShapeBuilder.HatBrim(rig), ink, loop: true);
            }
            if (EquippedAndUnlocked(EquipmentSlot.Eyes))
            {
                AddLine("GlassesLensFront", AccessoryShapeBuilder.GlassesLensFront(rig), ink, loop: true);
                AddLine("GlassesLensBack", AccessoryShapeBuilder.GlassesLensBack(rig), ink, loop: true);
                AddLine("GlassesBridge", AccessoryShapeBuilder.GlassesBridge(rig), ink, loop: false);
                AddLine("GlassesTemple", AccessoryShapeBuilder.GlassesTemple(rig), ink, loop: false);
            }
        }

        private bool EquippedAndUnlocked(EquipmentSlot slot)
            => EquipmentModel.IsEquipped(slot) && EquipmentModel.IsUnlocked(slot, _config);

        private static Vector3 V(float x, float y) => new Vector3(x, y, 0f);

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

        private void AddLine(string name, Vector3[] points, Color ink, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_figureRoot, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.material = _lineMaterial;
            lr.startColor = ink;
            lr.endColor = ink;
            lr.startWidth = Stroke;
            lr.endWidth = Stroke;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.loop = loop;
            lr.positionCount = points.Length;
            lr.SetPositions(points);
            _lines.Add(lr);
        }
    }
}
