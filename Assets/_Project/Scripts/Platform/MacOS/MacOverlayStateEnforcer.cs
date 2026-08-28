#if UNITY_STANDALONE_OSX
using UnityEngine;
using Kirurobo;

namespace StickMate.Platform.MacOS
{
    /// <summary>
    /// UniWindowController의 "창 부착(Attach) 타이밍" 문제를 해결하는 런타임 전용 보조 컴포넌트
    /// (UniWindowController 도입 라운드, 2026-08-28 — 실측으로 발견한 사고 대응).
    ///
    /// ============================================================================
    /// 왜 필요한가 — 실측으로 확인한 순서 문제
    /// ============================================================================
    /// UniWindowController는 자기 자신의 NSWindow를 Awake()가 아니라 첫 Update()에서 붙잡는다
    /// (UpdateTargetWindow() -> UniWinCore.AttachMyWindow()). 그런데 우리 배선 지점인
    /// StickmanAgent.Start()는 그보다 먼저 실행되므로, 그 시점에 건 설정 중 일부가 조용히 사라진다:
    ///   - SetTopmost(true)는 `_isTopmost = _uniWinCore.IsTopmost`로 되읽는데, IsTopmost는
    ///     `IsActive && _isTopmost`라서 아직 부착 전이면 **무조건 false**로 되돌아간다. 실측 로그:
    ///     "[MacWindowService] SetAlwaysOnTop(True) 적용 완료 — isTopmost=False" + 외부
    ///     CGWindowListCopyWindowInfo 조회에서 kCGWindowLayer=0(= 일반 레이어, 항상위 아님).
    ///   - DetectDesktopDpiScale()도 같은 이유로 clientSize=(0,0)을 읽어 배율 보정을 못 했다
    ///     (이쪽은 MacWindowService에서 CoreGraphics 디스플레이 모드 조회로 따로 해결했다).
    /// 투명(isTransparent)만은 예외적으로 살아남는다 — UpdateTargetWindow()가 부착 성공 직후
    /// `SetTransparent(_isTransparent)`로 직렬화된 값을 다시 적용해주기 때문이다.
    ///
    /// 그래서 이 컴포넌트는 "우리가 의도한 목표 상태"를 들고 있다가 창이 실제로 부착된 것을 확인한 뒤
    /// 한 번 더 적용하고, 그 결과를 되읽어 Player.log에 검증 로그로 남긴다. 부착 판정은
    /// `windowSize`(부착 전에는 (0,0))로 한다.
    ///
    /// 생성 주체는 MacWindowService.CreateOverlayWindow()이며, 그 서비스 자체가 실제 Standalone macOS
    /// Player에서만 인스턴스화되므로(StickmanAgent.CreatePlatformService()의
    /// `UNITY_STANDALONE_OSX && !UNITY_EDITOR` 분기) 에디터/헤드리스에는 애초에 존재하지 않는다.
    /// 씬 에셋에도 저장되지 않는다(런타임 new GameObject).
    /// </summary>
    internal sealed class MacOverlayStateEnforcer : MonoBehaviour
    {
        private const string HostObjectName = "StickMate_MacOverlayStateEnforcer";

        /// <summary>부착 확인 후 목표 상태를 재적용할 최대 횟수. 창 스타일이 부착 직후 한두 프레임에
        /// 걸쳐 확정되는 경우가 있어 한 번만 적용하고 끝내지 않는다. 무한 반복은 하지 않는다 —
        /// 사용자가 창을 직접 조작했을 때 우리가 그것을 계속 되돌려버리는 것이 더 나쁘기 때문이다.</summary>
        private const int ReapplyAttempts = 5;

        /// <summary>재적용 간격(초).</summary>
        private const float ReapplyIntervalSeconds = 0.5f;

        private UniWindowController _controller;
        private int _appliedCount;
        private float _timer;
        private bool _attachDetected;
        private bool _gaveUpLogged;
        private bool _cameraBackgroundPremultiplyFixed;

        /// <summary>부착 대기 제한 시간(초). 이 안에 창을 못 붙잡으면 정직하게 실패 로그를 남긴다.</summary>
        private const float AttachTimeoutSeconds = 15f;
        private float _elapsed;

        // 목표 상태 — MacWindowService가 자기 API 호출 때마다 갱신한다.
        internal bool DesiredTransparent = true;
        internal bool DesiredTopmost;
        internal bool DesiredClickThrough;
        internal bool DesiredHitTest;

        internal static MacOverlayStateEnforcer EnsureExists(UniWindowController controller)
        {
            var existing = UnityEngine.Object.FindAnyObjectByType<MacOverlayStateEnforcer>(FindObjectsInactive.Include);
            if (existing != null)
            {
                existing._controller = controller;
                return existing;
            }

            var go = new GameObject(HostObjectName);
            UnityEngine.Object.DontDestroyOnLoad(go);
            var enforcer = go.AddComponent<MacOverlayStateEnforcer>();
            enforcer._controller = controller;
            return enforcer;
        }

        /// <summary>MacWindowService가 목표 상태를 바꿀 때마다 호출 — 재적용 카운터를 리셋해 새 목표가
        /// 확실히 반영되게 한다.</summary>
        internal void MarkDirty()
        {
            _appliedCount = 0;
            _timer = ReapplyIntervalSeconds; // 다음 Update에서 곧바로 한 번 적용.
        }

        private void Update()
        {
            if (_controller == null)
            {
                return;
            }

            _elapsed += Time.unscaledDeltaTime;

            // 부착 판정: 부착 전에는 네이티브가 크기를 (0,0)으로 보고한다.
            Vector2 windowSize = _controller.windowSize;
            bool attached = windowSize.x > 0f && windowSize.y > 0f;

            if (!attached)
            {
                if (!_gaveUpLogged && _elapsed > AttachTimeoutSeconds)
                {
                    _gaveUpLogged = true;
                    Debug.LogWarning($"[MacOverlayStateEnforcer] {AttachTimeoutSeconds}초가 지나도 " +
                        "UniWindowController가 자기 NSWindow를 붙잡지 못했습니다(windowSize=(0,0)). " +
                        "투명/항상위/클릭관통이 전부 적용되지 않은 상태입니다 — 정직한 실패 보고용 로그.");
                }
                return;
            }

            if (!_attachDetected)
            {
                _attachDetected = true;
                ApplyTransparentSafeCameraBackground();
                Debug.Log($"[MacOverlayStateEnforcer] 창 부착 감지 — windowSize={windowSize}, " +
                    $"clientSize={_controller.clientSize}, windowPosition={_controller.windowPosition}, " +
                    $"경과 {_elapsed:F2}초. 이제 목표 상태를 재적용합니다.");
                _timer = ReapplyIntervalSeconds;
            }

            if (_appliedCount >= ReapplyAttempts)
            {
                return;
            }

            _timer += Time.unscaledDeltaTime;
            if (_timer < ReapplyIntervalSeconds)
            {
                return;
            }
            _timer = 0f;
            _appliedCount++;

            // 순서 주의: 히트테스트 자동 제어를 먼저 목표값으로 맞춘 뒤 나머지를 적용한다.
            _controller.isHitTestEnabled = DesiredHitTest;
            _controller.isTransparent = DesiredTransparent;
            _controller.isTopmost = DesiredTopmost;
            _controller.isClickThrough = DesiredClickThrough;

            Debug.Log($"[MacOverlayStateEnforcer] 재적용 {_appliedCount}/{ReapplyAttempts} — " +
                $"목표(transparent={DesiredTransparent}, topmost={DesiredTopmost}, " +
                $"clickThrough={DesiredClickThrough}, hitTest={DesiredHitTest}) / " +
                $"되읽음(isTransparent={_controller.isTransparent}, isTopmost={_controller.isTopmost}, " +
                $"isClickThrough={_controller.isClickThrough}, isHitTestEnabled={_controller.isHitTestEnabled}) / " +
                $"windowSize={_controller.windowSize}, clientSize={_controller.clientSize}, " +
                $"windowPosition={_controller.windowPosition}, cameraBg={CameraBackgroundDescription()}.");
        }

        /// <summary>
        /// 투명이 실제로 켜진 것이 확인된 뒤에만, 카메라 배경 RGB를 검정으로 낮춘다(알파는 계속 0).
        ///
        /// ============================================================================
        /// 왜 필요한가 — "캐릭터 주변이 반짝거림"의 진짜 원인(2026-08-28 사용자 지적)
        /// ============================================================================
        /// 씬에는 카메라 배경이 (0.94, 0.94, 0.94, 0) = "밝은 회색 + 알파 0"으로 저장돼 있다. RGB를 밝은
        /// 회색으로 둔 것은 "투명화가 실패해도 검정-on-검정이 되지 않게" 하려는 이전 라운드의 방어책이다
        /// (SceneBootstrapper.BuildMainScene 주석 참고). 알파가 0이라 투명이 성공하면 이 RGB는 눈에
        /// 보이지 않는다 — **MSAA를 켜기 전까지는**.
        ///
        /// MSAA는 한 픽셀 안의 여러 서브샘플을 평균해서 최종 색을 만든다. 캐릭터 윤곽선 픽셀은 일부
        /// 서브샘플만 검은 선에 덮이므로, 예를 들어 50% 덮인 픽셀은
        ///     rgb = (검정 0.0 x 0.5) + (배경 0.94 x 0.5) = 0.47,  alpha = (1 x 0.5) + (0 x 0.5) = 0.5
        /// 가 된다. 즉 **알파 0인 배경의 밝은 RGB가 가장자리 픽셀로 새어 들어온다.** 그 결과 검은 캐릭터
        /// 둘레에 밝은 회색 테두리(프린지)가 생기고, 캐릭터가 서브픽셀 단위로 움직일 때마다 그 테두리
        /// 밝기가 프레임마다 변해 "반짝거리는" 것처럼 보인다.
        ///
        /// 배경 RGB를 검정으로 낮추면 같은 픽셀이 rgb = 0, alpha = 0.5가 되어 프린지 없이 정확히
        /// "50% 농도의 검은 선"으로 합성된다 — 계단 현상 제거(MSAA)와 반짝임 제거를 동시에 얻는다.
        /// 실제로 UniWindowController 자신도 autoSwitchCameraBackground가 켜져 있으면 투명화 시점에
        /// 배경을 Color.clear(= 0,0,0,0)로 바꾼다(SetCameraBackground()) — 우리가 그 자동 전환을 끄고
        /// 밝은 회색을 유지한 것이 바로 이 아티팩트의 원인이었다. 즉 이 메서드는 라이브러리가 원래 하던
        /// 일을 "투명이 실제로 확인된 뒤에만" 하도록 조건부로 되살리는 것이다.
        ///
        /// 방어책은 그대로 유지된다: 이 교정은 창이 실제로 부착되고 isTransparent가 true로 되읽힌
        /// 경우에만 수행한다. 투명화가 실패한 상황에서는 배경이 밝은 회색으로 남아, 예전처럼
        /// "밝은 회색 창 안의 검정 캐릭터"(최소한 보이는 상태)가 된다.
        /// </summary>
        private void ApplyTransparentSafeCameraBackground()
        {
            if (_cameraBackgroundPremultiplyFixed) return;
            if (!_controller.isTransparent) return;

            Camera cam = _controller.currentCamera != null ? _controller.currentCamera : Camera.main;
            if (cam == null) return;

            Color before = cam.backgroundColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, before.a);
            _cameraBackgroundPremultiplyFixed = true;

            Debug.Log($"[MacOverlayStateEnforcer] 투명 확인됨 — 카메라 배경 RGB를 검정으로 교정했습니다 " +
                $"(MSAA 가장자리 프린지/반짝임 제거): ({before.r:F2},{before.g:F2},{before.b:F2},{before.a:F2}) " +
                $"-> (0.00,0.00,0.00,{before.a:F2}). 알파는 그대로 유지.");
        }

        private string CameraBackgroundDescription()
        {
            Camera cam = _controller.currentCamera != null ? _controller.currentCamera : Camera.main;
            if (cam == null)
            {
                return "(카메라 없음)";
            }
            Color c = cam.backgroundColor;
            return $"clearFlags={cam.clearFlags}, rgba=({c.r:F2},{c.g:F2},{c.b:F2},{c.a:F2})";
        }
    }
}
#endif
