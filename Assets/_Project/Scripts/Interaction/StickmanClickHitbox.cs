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

        /// <summary>
        /// 테스트 전용 — <b>실제 클릭과 완전히 같은 이벤트</b>를 발행한다. PlayMode 테스트는 OS 커서를
        /// 캐릭터 위로 옮겨 실제 버튼을 누를 수 없으므로(전역 입력은 합성 입력에 반응하지 않는다) 이
        /// 진입점이 필요하다. 구독자 쪽 분기를 우회하지 않고 <b>같은 이벤트</b>를 쏘므로, 테스트가
        /// 통과한다는 것은 실제 클릭 경로가 동작한다는 뜻이다(별도 테스트 전용 경로를 만들면 그 보장이
        /// 사라진다).
        /// </summary>
        public void SimulateMouseDownForTests() => MouseDown?.Invoke();

        private StickmanAgent _agent;
        private Collider2D[] _colliders;
        private IGlobalPointerButtonService _buttonService;

        /// <summary>
        /// 캐릭터 계층 <b>바깥</b>에 런타임 생성된 임시 클릭 대상(현재 유일한 사용처: 격파 미니게임에서
        /// 소환되는 기와 스택/기 모으기 게이지 — Interaction/BattleMinigameRenderer.cs).
        ///
        /// 왜 _colliders 배열을 다시 스캔하지 않는가: 그 배열은 Awake()에서 GetComponentsInChildren으로
        /// 한 번만 캐시되고, 소환 오브젝트는 (캐릭터가 걸어도 허공의 제자리에 남아야 하므로) 캐릭터의
        /// 자식이 아니라 씬 루트에 만들어진다. 매 프레임 재스캔은 낭비이고, 자식으로 붙이면 캐릭터를
        /// 던졌을 때 기와가 함께 날아가버린다. 그래서 "명시적으로 등록/해제하는 추가 목록"이라는
        /// 가장 좁은 형태를 택했다 — 등록 주체가 자기 수명 안에서 반드시 짝을 맞춰 해제한다.
        /// </summary>
        private readonly System.Collections.Generic.List<Collider2D> _extraColliders =
            new System.Collections.Generic.List<Collider2D>();

        // 두 입력 경로가 공유하는 "지금 잡고 있다" 상태 — 엣지 트리거로 중복 발생을 막는다.
        private bool _pressed;
        // 눌림 시작 시각 — 실제로 "누르고 끌었는지" vs "즉시 떼졌는지"를 실측으로 판별하기 위한 진단용
        // (리더 지시, 2026-08-28: Player.log에는 타임스탬프가 없어 로그 두 줄이 인접해 보이는 것만으로는
        // 시간 간격을 알 수 없다 — 홀드 시간을 직접 찍어야 판별이 가능하다).
        private float _pressStartTime;
        // 전역 폴링 경로의 직전 프레임 버튼 상태(상승/하강 엣지 판정용). 첫 폴링은 기록만 하고 넘어가,
        // 앱 시작 순간 이미 버튼이 눌려 있던 경우를 클릭으로 오인하지 않는다.
        private bool _globalPressedPrev;
        private bool _globalPressedInitialized;

        /// <summary>임시 클릭 대상 등록(중복 등록/null은 무시). 호출자는 반드시 짝이 되는
        /// <see cref="UnregisterExtraCollider"/>를 자기 정리 경로에서 호출해야 한다.</summary>
        public void RegisterExtraCollider(Collider2D collider)
        {
            if (collider == null || _extraColliders.Contains(collider)) return;
            _extraColliders.Add(collider);
        }

        /// <summary>등록 해제(미등록/null은 조용히 무시 — 멱등).</summary>
        public void UnregisterExtraCollider(Collider2D collider)
        {
            if (collider == null) return;
            _extraColliders.Remove(collider);
        }

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
            Debug.Log($"[StickmanClickHitbox] [0/6] 준비 완료 — agent={( _agent != null )}, " +
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
            _globalPressedPrev = down;

            if (rising && !_pressed && IsCursorOverHitbox()) BeginPress("전역폴링");
            // 놓기 판정은 **엣지가 아니라 현재 상태**로 한다: Unity의 OnMouseUp이 먼저 튀어 press를
            // 끝내버린 경우 falling 엣지를 이미 놓쳤을 수 있고, 반대로 press가 유지되는 한 "버튼이
            // 실제로 떼졌는가"만 보면 되기 때문이다.
            else if (!down && _pressed) EndPress("전역폴링");
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

            // 임시 클릭 대상(격파 미니게임의 기와/게이지). 파괴된 항목은 만나는 즉시 목록에서 지운다 —
            // 등록자가 해제하기 전에 GameObject가 먼저 Destroy되는 경우에도 목록이 새지 않게 한다.
            for (int i = _extraColliders.Count - 1; i >= 0; i--)
            {
                Collider2D c = _extraColliders[i];
                if (c == null) { _extraColliders.RemoveAt(i); continue; }
                if (!c.enabled) continue;
                if (c.OverlapPoint(cursorWorld)) return true;
            }
            return false;
        }

        private void OnMouseDown()
        {
            if (_pressed) return;
            BeginPress("Unity OnMouseDown");
        }

        /// <summary>
        /// Unity 표준 마우스업.
        ///
        /// ★ 중요 — 전역 폴링 경로가 살아 있으면 이 경로로는 press를 끝내지 않는다(2026-08-28,
        /// 리더 가설 (b) 대응). 우리 창은 투명 + 클릭관통 + 대개 비활성 앱이라, macOS가 마우스 캡처를
        /// 우리에게 계속 쥐여 준다는 보장이 없다 — 사용자가 버튼을 누른 채 끌고 있는데도 Unity가
        /// 곧바로 OnMouseUp을 쏴 드래그가 즉시 끝나버릴 수 있다(사용자 신고 "안 잡힘"과 정확히 일치하는
        /// 증상이다). 그래서 **"버튼이 실제로 떼졌는가"의 판정은 창 포커스와 무관한
        /// IGlobalPointerButtonService(CGEventSourceButtonState) 폴링에 맡기고**, 이쪽은 진단 로그만
        /// 남긴다. 전역 경로를 못 쓰는 플랫폼에서는 예전처럼 이 경로가 그대로 놓기를 담당한다.
        /// </summary>
        private void OnMouseUp()
        {
            if (!_pressed) return;

            if (_buttonService != null && _buttonService.TryGetPrimaryButtonPressed(out bool stillDown) && stillDown)
            {
                Debug.Log($"[StickmanClickHitbox] Unity OnMouseUp이 왔지만 전역 폴링은 버튼이 **아직 눌려 있다**고 " +
                    $"보고합니다(홀드 {Time.time - _pressStartTime:F2}초) — 창 마우스 캡처 유실로 판단해 무시하고 " +
                    "드래그를 계속합니다(놓기는 전역 폴링이 판정).");
                return;
            }

            EndPress("Unity OnMouseUp");
        }

        private void BeginPress(string source)
        {
            _pressed = true;
            _pressStartTime = Time.time;

            string cursorInfo = "(커서 조회 불가)";
            if (_agent != null && _agent.Blackboard != null
                && _agent.Blackboard.TryGetCursorWorldPosition(out Vector2 cw))
            {
                cursorInfo = cw.ToString("F2");
            }
            Debug.Log($"[StickmanClickHitbox] [1/6] 캐릭터 위 마우스다운 감지({source}) — 커서 월드={cursorInfo}, " +
                $"전역버튼경로={(_buttonService != null ? "활성" : "미지원")}.");
            MouseDown?.Invoke();
        }

        private void EndPress(string source)
        {
            _pressed = false;
            Debug.Log($"[StickmanClickHitbox] [5/6] 마우스업 감지({source}) — 홀드 시간 {Time.time - _pressStartTime:F2}초.");
            MouseUp?.Invoke();
        }
    }
}
