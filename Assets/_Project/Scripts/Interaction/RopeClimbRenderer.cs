using UnityEngine;
using StickMate.Core;
using StickMate.States;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 밧줄 등반(2026-09-07, docs/DESIGN_ROPE_CLIMB_ARCHITECTURE.md 4절/7절) 시각 레이어 — 유저가
    /// 보는 밧줄 한 줄과 갈고리 표시만 그린다. 판정·타이밍·자세는 States/RopeClimbState.cs가 전담하고,
    /// 이 클래스는 그 상태가 발행한 <see cref="StickmanEventBus.RopeClimbOverlayChanged"/> 이벤트가
    /// 실어 보내는 <b>좌표만</b> 읽는다(설계 문서 4절 — "이 상태가 창에 대해 아는 것은 좌표 하나뿐"이라는
    /// 사실을 코드 구조로 강제하는 그 분리).
    ///
    /// 관례는 ArcheryRenderer/GraffitiRenderer를 그대로 따른다: 전역 이벤트 구독 → LineRenderer로만
    /// 그림(스프라이트 에셋 없음) → 캐릭터 LineRenderer 머티리얼을 빌려 씀(Shader.Find 금지, 빌드
    /// 스트리핑 위험) → 같은 GameObject의 StickmanAgent만 참조(씬 전체 탐색 폴백 없음 — 프리팹이
    /// 복제되면 사본 렌더러가 전역 이벤트에 함께 반응해 밧줄이 두 벌 그려지는 사고를 막는다,
    /// ArcheryRenderer 클래스 문서의 실측 사례와 같은 함정) → 콜라이더는 단 하나도 만들지 않는다
    /// (원칙 3/비침해 원칙 2 — 밧줄은 클릭할 필요가 없는 순수 관전 오브젝트다).
    ///
    /// 취소/완료 시 <b>즉시</b> 제거한다(설계 4절 금지 3 "잔여 그림" 방지 — 창이 닫혔는데 밧줄
    /// 그림만 허공에 남으면 "우리 앱이 그 창에 뭔가를 걸어뒀다"는 잘못된 인상을 준다).
    /// </summary>
    public sealed class RopeClimbRenderer : MonoBehaviour
    {
        private const float RopeWidthRatio = 0.0339f;  // 캐릭터 몸통 획과 같은 굵기(ArcheryRenderer.StrokeWidthRatio 재사용값).
        private const float HookRadiusRatio = 0.045f;
        private const int HookSegments = 14;
        private const int SortingRope = 9;   // 캐릭터 획(0~5)보다 위, 다른 스펙터클(10~)보다 아래.
        private const int SortingHook = 10;

        /// <summary>이 렌더러가 담당하는 캐릭터. 같은 GameObject의 StickmanAgent만 쓴다(클래스 문서 참고).</summary>
        private StickmanAgent _agent;
        private Material _lineMaterial;

        private GameObject _container;
        private LineRenderer _rope;
        private LineRenderer _hook;

        /// <summary>지금 <see cref="SuspendedOverlayGate"/>가 감춰 둔 상태인가(로그를 상태 전환에
        /// 한 번만 남기기 위한 플래그 — ArcheryRenderer와 같은 관례).</summary>
        private bool _suspendHidden;

        /// <summary>지금 화면에 밧줄이 떠 있는지 — 테스트/진단용 관찰 창구.</summary>
        public bool IsVisible => _container != null;

        /// <summary>이 연출이 만든 콜라이더 수 — 항상 0이어야 한다(관전 전용, 클릭관통 유지).</summary>
        public int ActiveColliderCount =>
            _container != null ? _container.GetComponentsInChildren<Collider2D>(true).Length : 0;

        private void Awake()
        {
            _agent = GetComponent<StickmanAgent>();
        }

        private void OnEnable()
        {
            StickmanEventBus.RopeClimbOverlayChanged += OnOverlayChanged;
        }

        private void OnDisable()
        {
            StickmanEventBus.RopeClimbOverlayChanged -= OnOverlayChanged;
            Teardown();
        }

        /// <summary>
        /// ★ 캐릭터가 숨겨져 있으면 밧줄/갈고리도 함께 감춘다(원칙 2, Core/SuspendedOverlayGate —
        /// ArcheryRenderer와 같은 이유). 이 컨테이너는 <c>SetParent(null)</c>인 독립 루트라
        /// StickmanAgent.Suspend()의 SetRenderersEnabled(false)가 구조적으로 닿지 못한다 —
        /// SuspendedOverlayLeakAuditTests가 이 누락을 소스 스캔으로 잡는다.
        /// </summary>
        private void LateUpdate()
        {
            SuspendedOverlayGate.FreezeAndHide(_agent, _container, ref _suspendHidden, "[밧줄등반]", "밧줄/갈고리");
        }

        private void OnOverlayChanged(RopeClimbOverlayEvent evt)
        {
            if (_agent == null) return; // 자기 캐릭터가 없는 사본 — 전역 이벤트를 받아도 무시한다.

            if (evt.Phase == SpectacleOverlayPhase.Completed || evt.Phase == SpectacleOverlayPhase.Cancelled)
            {
                Teardown();
                return;
            }

            EnsureBuilt();
            if (_rope == null || _hook == null) return;

            // 밧줄은 손에서, 아직 걸리지 않았으면(flightProgress01<1) 그 진행률만큼만 앵커 쪽으로
            // 뻗는다 — Throw의 Swing 동안 "밧줄이 실제로 날아가는" 그림이 되고, 걸린 뒤(Ascend
            // 포함)에는 손-앵커를 그대로 잇는다.
            Vector2 ropeEnd = Vector2.Lerp(evt.HandWorld, evt.AnchorWorld, Mathf.Clamp01(evt.FlightProgress01));
            _rope.SetPosition(0, evt.HandWorld);
            _rope.SetPosition(1, ropeEnd);

            // 갈고리 표시는 걸린 뒤에만 보인다(아직 날아가는 중인 밧줄 끝에 갈고리가 이미 벽에
            // 박혀 있는 것처럼 보이면 판정과 그림이 어긋난다).
            bool hooked = evt.FlightProgress01 >= 0.999f;
            SetLineVisible(_hook, hooked);
            if (hooked)
            {
                _hook.transform.position = new Vector3(evt.AnchorWorld.x, evt.AnchorWorld.y, 0f);
            }
        }

        private void EnsureBuilt()
        {
            if (_container != null) return;

            _lineMaterial = ResolveLineMaterial();
            Color ink = _agent.Config != null ? _agent.Config.ResolveInkColor() : Color.black;
            float height = _agent.Blackboard != null ? _agent.Blackboard.CharacterHeightWorld : StickConfig.BaselineCharacterTotalHeight;

            _container = new GameObject("RopeClimbVisuals");
            _container.transform.SetParent(null, false);

            _rope = CreateLine(_container.transform, "Rope", ink, Mathf.Max(0.001f, height * RopeWidthRatio), SortingRope);
            _rope.useWorldSpace = true;
            _rope.positionCount = 2;

            _hook = CreateLine(_container.transform, "Hook", ink, Mathf.Max(0.001f, height * RopeWidthRatio), SortingHook);
            _hook.useWorldSpace = false;
            _hook.loop = true;
            float r = Mathf.Max(0.001f, height * HookRadiusRatio);
            _hook.positionCount = HookSegments;
            for (int i = 0; i < HookSegments; i++)
            {
                float a = i / (float)HookSegments * Mathf.PI * 2f;
                _hook.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
            }
        }

        private static void SetLineVisible(LineRenderer lr, bool visible)
        {
            if (lr != null) lr.enabled = visible;
        }

        private LineRenderer CreateLine(Transform parent, string name, Color color, float width, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.material = _lineMaterial;
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.sortingOrder = sortingOrder;
            return lr;
        }

        /// <summary>캐릭터가 이미 쓰고 있는 LineRenderer 머티리얼을 그대로 빌려 쓴다 — Shader.Find로
        /// 런타임에 찾지 않는 이유는 ArcheryRenderer 문서와 같다(빌드 스트리핑 위험).</summary>
        private Material ResolveLineMaterial()
        {
            if (_lineMaterial != null) return _lineMaterial;
            LineRenderer source = GetComponentInChildren<LineRenderer>(true);
            return source != null ? source.sharedMaterial : null;
        }

        private void Teardown()
        {
            if (_container != null) Destroy(_container);
            _container = null;
            _rope = null;
            _hook = null;
        }
    }
}
