using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;

namespace StickMate.Platform
{
    /// <summary>
    /// ★★ Dock 가로 구간의 **물리 계단**(2026-08-30, 리더 지시 — "Dock 사각지대 근본 제거").
    ///
    /// ============================================================================
    /// 무슨 문제를 없애는가
    /// ============================================================================
    /// 이 앱의 바닥은 두 층이었다:
    ///   • **물리 바닥**(Editor/SceneBootstrapper.CreateGroundCollider의 PhysicsGround) — 화면 최하단에
    ///     깔린 **전체 폭 한 장**. Unity 2D 물리의 실제 충돌면.
    ///   • **논리 발판**(Platform/FallbackPlatformWindowService) — Dock 상단은 그보다 **1.64유닛 위**에
    ///     떠 있고, 화면 최하단 안전망은 그 Dock 가로 구간이 **구멍**으로 뚫려 있다.
    /// 그래서 Dock 가로 구간에는 "논리 발판(위)과 물리 바닥(아래) 사이의 큰 빈 공간"이 있었다. 접지
    /// 스냅을 부르지 않는 순간(상태 전이 틈새, 랙돌, 던져짐)에 캐릭터는 그 빈 공간을 통과해 1.64유닛
    /// 자유낙하했고, 도착지는 "물리적으로는 떠받쳐지지만 논리적으로는 접지하지 않는" 사각지대였다.
    /// 2026-08-30 라운드는 그 사각지대에 **빠진 뒤 0.35초 만에 회수**하는 임시방편
    /// (<see cref="States.StickmanBlackboard"/>의 사각지대 회수)을 넣었다. 이 컴포넌트는 그
    /// **빈 공간 자체를 없앤다** — Dock 가로 구간 아래에 Dock 상단 높이의 물리 콜라이더를 놓아,
    /// 물리 바닥이 더 이상 균일한 한 장이 아니라 Dock 구간에서 위로 솟은 **계단**이 되게 한다.
    /// 회수 로직은 지우지 않고 안전망으로 남긴다(방어적 이중화) — 다만 발동할 일이 없어야 정상이다.
    ///
    /// ============================================================================
    /// 왜 씬에 정적으로 굽지 않고 런타임에 갱신하는가 (판단 근거)
    /// ============================================================================
    /// SceneBootstrapper는 **에디터 도구**다. 그런데 이 계단의 X 구간과 Y 높이는 씬을 굽는 시점에
    /// 알 수 없다:
    ///   (a) 에디터/배치모드에서는 플랫폼 서비스가 NullPlatformWindowService라 **Dock 발판이 아예
    ///       존재하지 않는다**(Core/StickmanAgent.CreatePlatformService의 #else 분기). 즉 씬을 구울 때
    ///       읽을 수 있는 "진짜 Dock"이 없다.
    ///   (b) 실제 macOS에서도 Dock 폭은 **실행 중에 변한다** — 앱 하나를 켜고 끄는 것만으로
    ///       `x201~1312 ↔ x174~1338`로 움직이는 것이 실측됐다.
    /// 정적으로 한 번 구우면 그 계단은 실제 Dock과 어긋난 자리에 있는 "보이지 않는 벽"이 된다 —
    /// 없는 것보다 나쁘다. 그래서 씬에는 **꺼진 껍데기**만 굽고, 실제 좌표/크기는 런타임에 이
    /// 컴포넌트가 채운다.
    ///
    /// ============================================================================
    /// 단일 소스 (이 프로젝트가 여러 번 깨진 지점이라 특히 엄격하게)
    /// ============================================================================
    /// 계단의 사각형은 **한 글자도 여기서 계산하지 않는다.** 발판 폴러가 캐시해 둔 목록에서
    /// <see cref="FallbackPlatformWindowService.DockFootholdHandle"/> 발판을 그대로 집어 쓴다. 그 발판은
    /// <see cref="FallbackPlatformWindowService.TryGetDockRectOsScreen"/>(= Dock 발판 / 안전망 구멍 /
    /// <see cref="FallbackPlatformWindowService.TryGetDockSpanOsScreen"/>이 전부 파생되는 그 단일 소스)
    /// 하나에서 나온다. 폴러의 캐시를 쓰는 것은 값을 다시 계산하는 것이 아니라 **같은 값을 같은 순간의
    /// 스냅샷으로** 받는 것이고, X(구간)와 Y(상단 높이)가 서로 다른 두 번의 조회에서 나올 여지를 없앤다.
    /// (이 프로젝트는 "같은 값을 두 곳이 따로 계산해 어긋난" 사고가 Dock 구간/화면 클램프/착지 위치에서
    /// 이미 여러 번 있었다.)
    ///
    /// ============================================================================
    /// 새 폴링 루프를 만들지 않는다
    /// ============================================================================
    /// Update()에서 하는 일은 <see cref="FootholdPoller.CachedFootholds"/>(순수 메모리 리스트) 훑기 +
    /// 직전 적용값과의 float 비교뿐이다. OS 호출도, 할당도 없다 — States/*.cs가 매 프레임 같은 캐시를
    /// 읽는 것과 동일한 비용이다. 실제 OS 재열거 빈도는 지금도 FootholdPoller 하나가 전담한다
    /// (`StickConfig.footholdPollInterval`). 콜라이더를 실제로 만지는 것은 **Dock 사각형이 바뀐 그
    /// 순간뿐**이다.
    ///
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DockPhysicsStep : MonoBehaviour
    {
        [SerializeField] private StickConfig _config;

        [Tooltip("발판 폴러를 들고 있는 플레이어 에이전트. 비어 있으면 첫 프레임에 씬에서 찾는다.")]
        [SerializeField] private StickmanAgent _agent;

        [Tooltip("계단이 솟아오르는 기준이 되는 전체 폭 물리 바닥(PhysicsGround). 계단의 아랫면을 이 " +
                 "바닥의 아랫면과 정확히 맞춰 둘 사이에 틈이 생길 수 없게 한다.")]
        [SerializeField] private BoxCollider2D _baseGround;

        [SerializeField] private BoxCollider2D _stepCollider;

        // 직전에 실제로 적용한 Dock 사각형(OS 좌표). 이것과 같으면 콜라이더를 건드리지 않는다.
        private Rect _appliedDockRectOs;
        private bool _hasApplied;

        // 좌표 변환에 쓰는 오버레이 원점/DPI가 바뀌면 같은 OS 사각형도 다른 월드 위치가 된다.
        private Vector2 _appliedOverlayOrigin = new Vector2(float.NaN, float.NaN);
        private float _appliedDpi = float.NaN;

        private bool _loggedMissingWiring;
        private string _lastAppliedLog;

        // 배선이 비었을 때의 폴백 탐색 쿨다운(초). 매 프레임 씬 전체를 뒤지지 않기 위한 것 — 정상
        // 경로(SceneBootstrapper가 _agent를 구워 둠)에서는 이 탐색이 한 번도 실행되지 않는다.
        private const float AgentSearchIntervalSeconds = 1f;
        private float _agentSearchCooldown;

        /// <summary>
        /// 실제로 따르는 설정. **에이전트가 지금 쓰고 있는 인스턴스가 우선**이다 — 씬에 구운 참조는
        /// 원본 에셋이라, 런타임에 설정을 복제해 갈아끼우는 경로(PlayMode 테스트의 표준 관례:
        /// 원본 자산을 절대 건드리지 않는다)에서 이 컴포넌트만 옛 값을 보고 있으면 스위치가 조용히
        /// 듣지 않는다. 배선이 비어 있을 때를 위해 직렬화 참조를 폴백으로 남긴다.
        /// </summary>
        private StickConfig ActiveConfig
            => _agent != null && _agent.Blackboard != null && _agent.Blackboard.Config != null
                ? _agent.Blackboard.Config
                : _config;

        /// <summary>진단/테스트용 — 지금 계단이 실제로 켜져 있는가.</summary>
        public bool IsActive => _stepCollider != null && _stepCollider.enabled;

        /// <summary>진단/테스트용 — 지금 계단이 덮고 있는 월드 사각형(꺼져 있으면 의미 없음).</summary>
        public Bounds StepBounds => _stepCollider != null ? _stepCollider.bounds : default;

        private void Awake()
        {
            if (_stepCollider == null) _stepCollider = GetComponent<BoxCollider2D>();
            // 실제 Dock을 확인하기 전까지는 꺼 둔다 — 잘못된 자리의 보이지 않는 벽은 없는 것보다 나쁘다.
            if (_stepCollider != null) _stepCollider.enabled = false;
        }

        private void Update()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.DockPhysics);   // [스톨구간] 계측
            if (_stepCollider == null) return;

            FootholdPoller poller = ResolvePoller();
            Camera cam = ResolveCamera();

            StickConfig config = ActiveConfig;
            if (config != null && !config.dockPhysicsStepEnabled)
            {
                Disable("설정으로 꺼짐(dockPhysicsStepEnabled=false)");
                return;
            }
            if (poller == null || cam == null)
            {
                Disable("발판 폴러 또는 카메라를 아직 찾지 못함");
                return;
            }

            if (!TryFindDockRect(poller.CachedFootholds, out Rect dockOs))
            {
                // Dock이 비활성(자동 숨김 / 좌우 세로 Dock / 비-macOS)이면 계단도 없어야 한다 —
                // 그때는 모든 낙하가 화면 최하단 안전망으로 가는 것이 원래 설계다.
                Disable("Dock 발판 없음");
                return;
            }

            Vector2 origin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            float dpi = ScreenCoordinateConverter.ResolveDpiScale(config);
            if (_hasApplied && _stepCollider.enabled
                && dockOs == _appliedDockRectOs
                && origin == _appliedOverlayOrigin
                && Mathf.Approximately(dpi, _appliedDpi))
            {
                return; // 바뀐 것이 없다 — 콜라이더를 건드리지 않는다(24시간 상주 앱).
            }

            Apply(cam, config, dockOs, origin, dpi);
        }

        /// <summary>
        /// Dock 사각형(OS 좌표)을 월드 계단으로 옮긴다. 윗면은 Dock 발판 상단과 **정확히 같은 Y**여야
        /// 한다 — 이 프로젝트의 규약상 캐릭터 루트 원점이 곧 발바닥이고(StickmanBlackboard.SenseGround
        /// 문서), 논리 접지 스냅이 캐릭터를 올려놓는 높이가 바로 그 선이기 때문이다. 둘이 어긋나면
        /// "물리적으로 떠받쳐지는 높이"와 "논리적으로 서는 높이"가 달라져 정확히 이번에 없애려는
        /// 사각지대가 다시 생긴다.
        /// </summary>
        private void Apply(Camera cam, StickConfig config, Rect dockOs, Vector2 origin, float dpi)
        {
            // 직교 카메라라 depth는 x/y 결과에 영향을 주지 않지만, 변환 API의 왕복 계약을 지키기 위해
            // 같은 호출 세트에서 얻은 값을 그대로 넘긴다(ScreenCoordinateConverter 문서).
            ScreenCoordinateConverter.WorldToOsScreen(cam, transform.position, config, out float depth);

            Vector3 topLeft = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(dockOs.xMin, dockOs.yMin), depth, config);
            Vector3 topRight = ScreenCoordinateConverter.OsScreenToWorld(cam, new Vector2(dockOs.xMax, dockOs.yMin), depth, config);

            float leftWorldX = Mathf.Min(topLeft.x, topRight.x);
            float rightWorldX = Mathf.Max(topLeft.x, topRight.x);
            float topWorldY = topLeft.y;
            float bottomWorldY = ResolveBottomWorldY(cam);

            float width = rightWorldX - leftWorldX;
            float height = topWorldY - bottomWorldY;
            if (width <= 0f || height <= 0f)
            {
                Disable($"계단 크기가 유효하지 않음(폭 {width:F3}, 높이 {height:F3})");
                return;
            }

            // 콜라이더는 오브젝트 원점 기준 offset/size로 정의된다. 부모 스케일 1을 전제로 한다
            // (SceneBootstrapper가 그렇게 굽고, 이 오브젝트는 그 외 아무도 건드리지 않는다).
            Vector3 pos = transform.position;
            transform.position = new Vector3((leftWorldX + rightWorldX) * 0.5f, (topWorldY + bottomWorldY) * 0.5f, pos.z);
            _stepCollider.offset = Vector2.zero;
            _stepCollider.size = new Vector2(width, height);
            _stepCollider.enabled = true;

            _appliedDockRectOs = dockOs;
            _appliedOverlayOrigin = origin;
            _appliedDpi = dpi;
            _hasApplied = true;

            LogOnce($"Dock 물리 계단 적용 — 월드 x {leftWorldX:F3}~{rightWorldX:F3}, 윗면 y={topWorldY:F3}, " +
                    $"아랫면 y={bottomWorldY:F3}(높이 {height:F3}). OS 사각형 x {dockOs.xMin:F1}~{dockOs.xMax:F1}, 상단 y={dockOs.yMin:F1}.");
        }

        /// <summary>
        /// 계단의 아랫면. 전체 폭 물리 바닥(PhysicsGround)의 아랫면과 맞춘다 — 둘 사이에 틈이 생기면
        /// 그 틈으로 빠지는 새로운 사각지대를 만드는 셈이 된다. 배선이 비어 있으면 카메라 뷰포트
        /// 아래까지 내려 보수적으로 덮는다(화면 안에서는 어떤 경우에도 틈이 없다).
        /// </summary>
        private float ResolveBottomWorldY(Camera cam)
        {
            if (_baseGround != null) return _baseGround.bounds.min.y;
            return cam.transform.position.y - cam.orthographicSize * 2f;
        }

        private static bool TryFindDockRect(IReadOnlyList<PlatformFoothold> footholds, out Rect dockOs)
        {
            dockOs = default;
            if (footholds == null) return false;
            for (int i = 0; i < footholds.Count; i++)
            {
                if (footholds[i].Handle != FallbackPlatformWindowService.DockFootholdHandle) continue;
                dockOs = footholds[i].ScreenRect;
                return true;
            }
            return false;
        }

        private void Disable(string reason)
        {
            if (_stepCollider == null || !_stepCollider.enabled)
            {
                _hasApplied = false;
                return;
            }
            _stepCollider.enabled = false;
            _hasApplied = false;
            LogOnce($"Dock 물리 계단 비활성 — {reason}.");
        }

        private FootholdPoller ResolvePoller()
        {
            if (_agent == null)
            {
                // 심층 방어 — 씬 배선이 비어도 동작하게 한다(SceneBootstrapper가 NewScene 이후 참조를
                // 잃어 컴포넌트가 조용히 무동작이 됐던 사고와 같은 계열의 방어).
                _agentSearchCooldown -= Time.unscaledDeltaTime;
                if (_agentSearchCooldown > 0f) return null;
                _agentSearchCooldown = AgentSearchIntervalSeconds;

                _agent = Object.FindFirstObjectByType<StickmanAgent>();
                if (_agent == null)
                {
                    if (!_loggedMissingWiring)
                    {
                        _loggedMissingWiring = true;
                        Debug.LogWarning("[Dock계단] 씬에서 StickmanAgent를 찾지 못했습니다 — Dock 물리 계단이 꺼진 채로 남습니다.");
                    }
                    return null;
                }
            }
            return _agent.Blackboard != null ? _agent.Blackboard.FootholdPoller : null;
        }

        private Camera ResolveCamera()
        {
            if (_agent != null && _agent.Blackboard != null && _agent.Blackboard.MainCamera != null)
            {
                return _agent.Blackboard.MainCamera;
            }
            return Camera.main;
        }

        // 내용이 바뀔 때만 남긴다(FallbackPlatformWindowService.LogDockSpanOnce와 같은 컨벤션) —
        // 24시간 상주 앱에서 "지금 계단이 어디에 있는가"를 항상 최신 1줄로 유지하기 위해서다.
        private void LogOnce(string message)
        {
            if (_lastAppliedLog == message) return;
            _lastAppliedLog = message;
            Debug.Log("[Dock계단] " + message);
        }
    }
}
