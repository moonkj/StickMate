using System;
using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// 부분적 클릭관통 해제(docs/UX_FLOW.md 15절)의 Unity 게임 오브젝트 레벨 절반 — 캐릭터의
    /// Collider2D 위에서 마우스 다운/업을 감지한다.
    ///
    /// Unity의 OnMouseDown/OnMouseUp은 Camera.main 기준 물리 히트테스트(2D 콜라이더 포함)를 엔진이
    /// 매 프레임 자체적으로 수행하므로, 이 컴포넌트 자체가 "동적 히트박스 추적"(15절 제약 1)을 사실상
    /// 공짜로 만족시킨다 — 캐릭터가 움직이면 콜라이더도 함께 움직이고, Unity가 매 프레임 그 최신
    /// 위치로 히트테스트하기 때문이다. 별도의 폴링/캐싱 코드가 필요 없다.
    ///
    /// ============================================================================
    /// 이중 입력 경로 (드래그&던지기 실배선 라운드, 2026-08-28)
    /// ============================================================================
    /// 이 컴포넌트는 이제 두 경로에서 같은 MouseDown/MouseUp 이벤트를 낸다. 어느 쪽이 먼저 잡든
    /// 결과는 동일하며, _pressed 플래그 하나로 중복 발생을 막는다(엣지 트리거).
    ///
    /// (1) Unity 표준 경로 — OnMouseDown/OnMouseUp. 우리 창이 실제로 마우스 이벤트를 수신할 때 동작.
    /// (2) 전역 폴링 경로 — Platform/IGlobalPointerButtonService(macOS: CGEventSourceButtonState) +
    ///     StickmanAgent.TryGetCursorPosition(CGEventGetLocation)의 조합. 창 포커스와 무관하다.
    ///
    /// (2)가 필요한 이유: 이 앱의 창은 항상위 투명 오버레이이고 평소 클릭관통 상태이며 대개 비활성
    /// 앱이다. macOS에서 비활성 앱의 창을 클릭하면 그 첫 클릭이 "앱 활성화"에만 소비되고 콘텐츠 뷰까지
    /// 내려오지 않을 수 있어(NSView.acceptsFirstMouse 기본 NO), (1)만으로는 "한 번 눌렀는데 아무 일도
    /// 안 일어나는" 상황이 생길 수 있다. (2)는 그 경우에도 확실히 잡는다.
    ///
    /// **비침해 원칙(CLAUDE.md 2)은 (2)에서도 그대로 유지된다**: 전역 폴링이 "버튼이 눌렸다"고 알려주는
    /// 것만으로는 아무 일도 하지 않는다. 반드시 그 순간 커서가 이 캐릭터의 Collider2D 안에 있을 때만
    /// MouseDown을 낸다 — 판정 영역이 (1)과 완전히 동일하므로, 캐릭터 밖 클릭은 두 경로 어느 쪽으로도
    /// 절대 잡히지 않는다.
    ///
    /// [OS 레벨 절반과의 관계] 이 컴포넌트가 보장하는 것은 "캐릭터를 클릭하면 Unity가 그 사실을 안다"
    /// 까지다. "그 외 영역은 항상 100% 관통된다"는 보장은 OS 레벨 계약이고, 지금은
    /// UniWindowController의 히트테스트(hitTestType=Raycast, Assets/Editor/SceneBootstrapper.cs 참고)가
    /// 그 역할을 실제로 수행한다 — 커서 아래에 우리 Collider2D가 있을 때만 클릭관통을 풀고, 그 외에는
    /// 계속 관통시킨다. 즉 두 절반이 **같은 Collider2D**를 판정 기준으로 쓰게 되어 서로 정확히 일치한다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class StickmanClickHitbox : MonoBehaviour
    {
        /// <summary>이 오브젝트의 콜라이더 위에서 마우스 버튼이 눌린 프레임에 발생.</summary>
        public event Action MouseDown;

        /// <summary>마우스 버튼이 떼졌을 때(Unity 표준 동작상 다운을 시작한 콜라이더 기준으로 항상 발생)
        /// 발생 — 반드시 같은 콜라이더 위에서 뗄 필요는 없다(드래그 도중 캐릭터가 커서를 벗어나도 정상 수신).</summary>
        public event Action MouseUp;

        private StickmanAgent _agent;
        private Collider2D[] _colliders;
        private IGlobalPointerButtonService _buttonService;

        // 두 입력 경로가 공유하는 "지금 잡고 있다" 상태 — 엣지 트리거로 중복 발생을 막는다.
        private bool _pressed;
        // 전역 폴링 경로의 직전 프레임 버튼 상태(상승/하강 엣지 판정용). 첫 폴링은 기록만 하고 넘어가,
        // 앱 시작 순간 이미 버튼이 눌려 있던 경우를 클릭으로 오인하지 않는다.
        private bool _globalPressedPrev;
        private bool _globalPressedInitialized;

        private void Awake()
        {
            _agent = GetComponent<StickmanAgent>();
            // 루트/머리/팔다리 전부 — UniWindowController의 Raycast 히트테스트가 판정에 쓰는 집합과
            // 정확히 같게 맞춘다(두 절반의 판정 영역 일치, 클래스 문서 참고).
            _colliders = GetComponentsInChildren<Collider2D>(true);
        }

        private void Start()
        {
            _buttonService = _agent != null ? _agent.PlatformService as IGlobalPointerButtonService : null;

            // 실측 검증용 준비 상태 로그(리더 지시: "상태 전이가 실제로 일어날 준비가 됐는지"를 로그로 확인).
            int activeColliders = 0;
            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] != null && _colliders[i].enabled) activeColliders++;
            }
            Debug.Log($"[StickmanClickHitbox] 준비 완료 — agent={( _agent != null )}, " +
                $"콜라이더 {activeColliders}/{_colliders.Length}개 활성, " +
                $"전역버튼경로={(_buttonService != null ? "사용 가능" : "미지원(Unity OnMouseDown만)")}, " +
                $"MouseDown 구독자={(MouseDown != null ? MouseDown.GetInvocationList().Length : 0)}명, " +
                $"레이어={LayerMask.LayerToName(gameObject.layer)}({gameObject.layer}).");
        }

        private void Update()
        {
            if (_buttonService == null) return;
            if (!_buttonService.TryGetPrimaryButtonPressed(out bool down)) return;

            if (!_globalPressedInitialized)
            {
                _globalPressedInitialized = true;
                _globalPressedPrev = down;
                return;
            }

            bool rising = down && !_globalPressedPrev;
            bool falling = !down && _globalPressedPrev;
            _globalPressedPrev = down;

            if (rising && !_pressed && IsCursorOverHitbox()) BeginPress("전역폴링");
            else if (falling && _pressed) EndPress("전역폴링");
        }

        /// <summary>커서(OS 전역 좌표)가 지금 이 캐릭터의 콜라이더 중 하나 안에 있는지. Unity 표준
        /// 경로의 판정과 같은 콜라이더 집합을 쓰므로 두 경로의 판정 영역이 정확히 일치한다.</summary>
        private bool IsCursorOverHitbox()
        {
            if (_agent == null) return false;
            var blackboard = _agent.Blackboard;
            if (blackboard == null) return false;
            if (!blackboard.TryGetCursorWorldPosition(out Vector2 cursorWorld)) return false;

            for (int i = 0; i < _colliders.Length; i++)
            {
                Collider2D c = _colliders[i];
                if (c == null || !c.enabled) continue;
                if (c.OverlapPoint(cursorWorld)) return true;
            }
            return false;
        }

        private void OnMouseDown()
        {
            if (_pressed) return;
            BeginPress("Unity OnMouseDown");
        }

        private void OnMouseUp()
        {
            if (!_pressed) return;
            EndPress("Unity OnMouseUp");
        }

        private void BeginPress(string source)
        {
            _pressed = true;
            Debug.Log($"[StickmanClickHitbox] 캐릭터 위 마우스다운 감지({source}).");
            MouseDown?.Invoke();
        }

        private void EndPress(string source)
        {
            _pressed = false;
            Debug.Log($"[StickmanClickHitbox] 마우스업 감지({source}).");
            MouseUp?.Invoke();
        }
    }
}
