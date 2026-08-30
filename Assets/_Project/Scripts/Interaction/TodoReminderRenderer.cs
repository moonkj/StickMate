using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 투두 말풍선 "들고 다니는 모드" 시각 레이어 — docs/UX_FLOW.md 17절 "캐릭터가 걷다가 주기적으로
    /// <b>종이 한 장을 꺼내 들고 확인하는 동작</b> → 그 순간 확정된 REMINDER 상태에서 파생된 말풍선으로
    /// 할일 텍스트 노출 → 종이는 다 보여준 뒤 다시 접어 넣고 원래 루프로 복귀"의 <b>종이</b> 부분.
    ///
    /// ============================================================================
    /// 역할 분담 — 글자는 이 렌더러가 그리지 않는다
    /// ============================================================================
    /// 할일 텍스트 자체는 <b>말풍선</b>(Dialogue/DialogueBubbleRenderer.cs)이 그린다. 그 대사는
    /// States/TimedSpectacleState.cs가 TodoReminder 상태로 전이가 확정된 뒤 Enter() 안에서
    /// TodoListModel.ConsumePendingReminderText로 파생시킨 것이라 <b>원칙 1(행동-텍스트 싱크)</b>이
    /// 이미 보장돼 있다. 이 렌더러가 종이 위에 텍스트를 따로 렌더링하면 같은 문자열의 소스가 두 벌이
    /// 되어 그 계약이 깨지므로, 종이에는 <b>글자를 흉내 낸 짧은 선 3줄만</b> 그린다.
    ///
    /// ============================================================================
    /// 왜 상태 전이를 구독하는가 (TodoListChanged가 아니라)
    /// ============================================================================
    /// 17절의 "들고 다니는 모드"는 목록이 바뀔 때가 아니라 <b>REMINDER 상태에 있는 동안</b>만 보여야
    /// 한다. 그래서 WindowTheftRenderer가 포기 순간을 self-transition으로 받는 것과 같은 이유로,
    /// 종이의 등장/퇴장 타이밍은 렌더러가 자체 타이머로 흉내 내지 않고 상태 머신에서 직접 받는다 —
    /// StickConfig.todoReminderHoldSeconds를 두 곳에서 따로 세면 반드시 어긋난다.
    ///
    /// 목록 데이터(Core/TodoListModel.cs)를 이 렌더러가 읽는 유일한 목적은 "종이에 줄을 몇 개 그릴까"
    /// 뿐이며, 실제 파일/캘린더/할일 앱은 절대 읽지도 쓰지도 않는다(CLAUDE.md 불변 원칙 3 —
    /// 이 기능의 데이터는 처음부터 끝까지 앱 내부 상태다).
    /// </summary>
    public sealed class TodoReminderRenderer : MonoBehaviour
    {
        // ==================== 연출 상수 ====================

        private const float UnfoldSeconds = 0.22f;  // 주머니에서 꺼내 펴는 시간.
        private const float FoldSeconds = 0.20f;    // 다시 접어 넣는 시간.

        // ============================================================================
        // ★ 2026-08-29 리더 지시 — 캐릭터 기준 치수는 전부 **전신 높이 대비 비율**이다.
        // ============================================================================
        // 캐릭터 루트는 **발 높이**가 y=0이고, 손은 몸통 중간~아래이므로 종이는 가슴 높이 언저리에 든다.
        // 종전에는 이 값들이 전부 절대 월드유닛이었다. StickConfig.characterScale이 0.5가 되면 캐릭터만
        // 절반이 되고 종이(반폭 0.24 / 반높이 0.30, 오프셋 y+1.02)는 그대로라, 종이가 **정수리
        // (배율 0.5에서 1.137) 언저리 허공에 몸통만 한 크기로** 떠 있게 된다 — "손에 들고 있는 것처럼
        // 보인다"는 이 연출의 전제가 통째로 깨진다.
        //
        // 기준 치수의 유일한 조회 경로는 Core/StickmanMetrics.cs다(상수 복사가 아니라 계층 실측).
        // 분자는 검증을 마친 종전 값 그 자체, 분모는 배율 1.0 기준 신장이므로 배율 1.0에서는 지금까지와
        // 완전히 같은 그림이 나온다(= 회귀 없음의 증거).
        //
        // ★ 시간 상수(UnfoldSeconds/FoldSeconds)와 흔들림 각진동수는 길이 차원이 아니라 그대로 둔다.
        //   이 연출에는 길이/초 차원의 속도가 없다(종이는 캐릭터를 따라다닐 뿐 스스로 이동하지 않는다).
        private const float PaperOffsetXRatio = 0.66f / StickConfig.BaselineCharacterTotalHeight;
        private const float PaperOffsetYRatio = 1.02f / StickConfig.BaselineCharacterTotalHeight;
        private const float PaperHalfWRatio = 0.24f / StickConfig.BaselineCharacterTotalHeight;
        private const float PaperHalfHRatio = 0.30f / StickConfig.BaselineCharacterTotalHeight;
        private const int PaperTextLines = 3;

        // 종이 안쪽 디테일(접힌 모서리 / 글자 줄)은 **종이 크기 대비 비율**이라 종이와 함께 저절로
        // 따라온다 — 종전 절대값(0.10 / 0.11 / 0.07)을 배율 1.0 종이 반폭 0.24, 반높이 0.30으로 나눈 값이다.
        private const float PaperFoldCornerOfHalfW = 0.10f / 0.24f;
        private const float PaperFoldCornerOfHalfH = 0.10f / 0.30f;
        private const float PaperTextTopInsetOfHalfH = 0.11f / 0.30f;
        private const float PaperTextLineStepOfHalfH = 0.11f / 0.30f;
        private const float PaperTextLeftInsetOfHalfW = 0.07f / 0.24f;

        private const float StrokeWidthRatio = 0.045f / StickConfig.BaselineCharacterTotalHeight;
        private const int SortingOrder = 7; // 캐릭터 획(0~5) 앞 = 손에 들고 있는 것처럼 보인다.

        private static readonly Color PaperColor = new Color(0.30f, 0.33f, 0.38f, 1f);
        private static readonly Color PaperTextColor = new Color(0.46f, 0.50f, 0.56f, 1f);

        private enum Mode { None, Unfolding, Holding, Folding }

        /// <summary>
        /// 이 렌더러가 담당하는 캐릭터. <b>같은 GameObject의 StickmanAgent만</b> 쓰고 씬 전체 탐색
        /// 폴백은 쓰지 않는다 — 이 프리팹이 복제되면 폴백을 두었을 때 사본 손에도 종이가
        /// 한 벌 더 생긴다(2026-08-29 격파 미니게임에서 실측 확인된 버그와 같은 함정).
        /// </summary>
        private StickmanAgent _agent;
        private Material _lineMaterial;

        // ==================== 캐릭터 실측 치수 조회 ====================

        /// <summary>캐릭터 치수의 <b>유일한</b> 조회 경로(Core/StickmanMetrics.cs). 매 프레임 쓰이는
        /// 값이라 컴포넌트를 한 번만 찾아 캐시한다. 못 찾으면 null을 캐시하고 비율 폴백으로 떨어진다.</summary>
        private StickmanMetrics _metrics;
        private bool _metricsResolved;

        private StickmanMetrics Metrics
        {
            get
            {
                if (_metrics != null) return _metrics;
                if (_metricsResolved) return null;
                _metricsResolved = true;
                _metrics = _agent != null ? _agent.Metrics : StickmanMetrics.Find(this);
                return _metrics;
            }
        }

        /// <summary>이 캐릭터의 전신 높이(월드 유닛) — 위 모든 비율의 유일한 기준값.</summary>
        private float Height
        {
            get
            {
                StickmanMetrics m = Metrics;
                return m != null ? m.TotalHeight : StickConfig.BaselineCharacterTotalHeight;
            }
        }

        // ==================== 테스트/진단용 배치 관찰 창구 ====================
        // (Tests/PlayMode/RendererScaleRatioTests.cs가 배율 1.0/0.5 양쪽에서 단언한다.)

        /// <summary>종이가 놓이는 로컬 X(발바닥 기준, 바라보는 방향 부호를 곱하기 전).</summary>
        public float PaperOffsetLocalX => Height * PaperOffsetXRatio;

        /// <summary>종이가 놓이는 로컬 Y(발바닥 기준) — 가슴 높이 언저리.</summary>
        public float PaperOffsetLocalY => Height * PaperOffsetYRatio;

        /// <summary>종이의 반폭(월드 유닛).</summary>
        public float PaperHalfWidth => Height * PaperHalfWRatio;

        /// <summary>종이의 반높이(월드 유닛).</summary>
        public float PaperHalfHeight => Height * PaperHalfHRatio;

        /// <summary>획 두께(월드 유닛).</summary>
        public float StrokeWidth => Height * StrokeWidthRatio;

        private GameObject _container;
        private readonly List<LineRenderer> _lines = new List<LineRenderer>(6);
        private Mode _mode = Mode.None;
        private float _modeTimer;
        private float _facingAtSpawn = 1f;

        // ==================== 테스트/진단용 관찰 창구 ====================

        /// <summary>지금 캐릭터가 종이를 들고 있는지.</summary>
        public bool IsVisible => _mode != Mode.None;

        /// <summary>이 연출이 지금 실제로 만들어낸 LineRenderer 개수. 정리가 끝나면 반드시 0이다.</summary>
        public int ActiveVisualCount =>
            _container != null ? _container.GetComponentsInChildren<LineRenderer>(true).Length : 0;

        /// <summary>이 연출이 만든 콜라이더 수 — 항상 0이어야 한다(종이는 관전 전용 = 클릭관통 유지.
        /// 클릭으로 조작하는 것은 포스트잇 카드 쪽이고, 그건 Interaction/TodoPostItWidget.cs의 몫이다).</summary>
        public int ActiveColliderCount =>
            _container != null ? _container.GetComponentsInChildren<Collider2D>(true).Length : 0;

        // ==================== 생애주기 ====================

        private void Awake()
        {
            _agent = GetComponent<StickmanAgent>();
        }

        private void OnEnable() => StickmanEventBus.StateTransitioned += OnStateTransitioned;

        private void OnDisable()
        {
            StickmanEventBus.StateTransitioned -= OnStateTransitioned;
            // 이 컴포넌트가 꺼질 때 종이가 화면에 영구히 남지 않게 한다(다른 렌더러들과 같은 정리 관례).
            Teardown();
        }

        private void OnStateTransitioned(StateTransitionEvent evt)
        {
            if (_agent == null) return; // 자기 캐릭터가 없는 사본 — 전역 이벤트를 받아도 무시한다.

            if (evt.To == StickmanStateId.TodoReminder && evt.From != StickmanStateId.TodoReminder)
            {
                Begin();
                return;
            }

            if (evt.From == StickmanStateId.TodoReminder && evt.To != StickmanStateId.TodoReminder)
            {
                // 강제 인터럽트(긴급정지/피격)면 말풍선과 같은 규칙으로 즉시 걷는다(UX 5절: 강제 취소 시
                // 최소 노출시간을 무시하고 즉시 제거). 정상 종료면 접어 넣는 연출을 보여준다.
                if (evt.IsForcedInterrupt) Teardown();
                else BeginFold();
            }
        }

        // ==================== 생성 ====================

        private void Begin()
        {
            Teardown();

            var blackboard = _agent != null ? _agent.Blackboard : null;
            if (blackboard == null || blackboard.Body == null)
            {
                Debug.LogWarning("[투두] 종이를 그리지 못했습니다 — 캐릭터 배선이 없습니다.");
                return;
            }

            _lineMaterial = ResolveLineMaterial();
            _facingAtSpawn = blackboard.FacingSign != 0f ? Mathf.Sign(blackboard.FacingSign) : 1f;

            _container = new GameObject("TodoReminderPaper");
            _container.transform.SetParent(null, false);
            _container.transform.position = PaperWorldPosition();

            // 종이 테두리. 아래 지역 변수는 전부 전신 높이 비율에서 나온 값이다(위 비율 상수 블록 참고).
            float halfW = PaperHalfWidth;
            float halfH = PaperHalfHeight;
            float stroke = StrokeWidth;
            _lines.Add(CreateLine("PaperSheet", new[]
            {
                new Vector3(-halfW, -halfH, 0f),
                new Vector3(halfW, -halfH, 0f),
                new Vector3(halfW, halfH, 0f),
                new Vector3(-halfW, halfH, 0f),
            }, PaperColor, stroke, loop: true));

            // 오른쪽 위 접힌 모서리 — 사각형 하나만 있으면 "종이"로 읽히지 않는다.
            float foldW = halfW * PaperFoldCornerOfHalfW;
            float foldH = halfH * PaperFoldCornerOfHalfH;
            _lines.Add(CreateLine("PaperFold", new[]
            {
                new Vector3(halfW - foldW, halfH, 0f),
                new Vector3(halfW - foldW, halfH - foldH, 0f),
                new Vector3(halfW, halfH - foldH, 0f),
            }, PaperColor, stroke * 0.85f, loop: false));

            // "글자" 흉내 3줄. 진짜 텍스트는 말풍선이 그린다(클래스 문서 "역할 분담" 참고).
            // 남은 할일 개수에 따라 줄 길이만 살짝 달라져 "뭔가 적혀 있다"는 느낌을 준다.
            int remaining = Mathf.Max(1, TodoListModel.UncompletedCount);
            float textTopInset = halfH * PaperTextTopInsetOfHalfH;
            float textStep = halfH * PaperTextLineStepOfHalfH;
            float textLeftInset = halfW * PaperTextLeftInsetOfHalfW;
            for (int i = 0; i < PaperTextLines; i++)
            {
                float y = halfH - textTopInset - i * textStep;
                float len = halfW * (i == PaperTextLines - 1 ? 0.85f : 1.45f)
                    * (i == 0 ? 1f : Mathf.Clamp01(0.55f + 0.15f * remaining));
                _lines.Add(CreateLine($"PaperTextLine{i}", new[]
                {
                    new Vector3(-halfW + textLeftInset, y, 0f),
                    new Vector3(-halfW + textLeftInset + len, y, 0f),
                }, PaperTextColor, stroke * 0.7f, loop: false));
            }

            _mode = Mode.Unfolding;
            _modeTimer = 0f;
            SetUnfoldProgress(0f);

            Debug.Log($"[투두] 들고 다니는 모드 — 종이를 꺼냈습니다(미완료 {TodoListModel.UncompletedCount}건). " +
                $"시각 오브젝트 {ActiveVisualCount}개, 콜라이더 {ActiveColliderCount}개(항상 0). " +
                "★ 할일 텍스트 자체는 말풍선이 그린다(원칙 1 — 상태 전이가 확정된 뒤 그 상태에서만 파생).");
        }

        // ==================== 매 프레임 갱신 ====================

        private void LateUpdate()
        {
            if (_mode == Mode.None || _container == null) return;

            _modeTimer += Time.deltaTime;
            _container.transform.position = PaperWorldPosition();

            switch (_mode)
            {
                case Mode.Unfolding:
                {
                    float t = Mathf.Clamp01(_modeTimer / UnfoldSeconds);
                    SetUnfoldProgress(t);
                    if (t >= 1f) { _mode = Mode.Holding; _modeTimer = 0f; }
                    break;
                }

                case Mode.Holding:
                    // 손에 들고 있는 미세한 흔들림 — 종이가 굳어 보이지 않게 하는 최소한의 생동감.
                    _container.transform.localRotation =
                        Quaternion.Euler(0f, 0f, Mathf.Sin(_modeTimer * 3.1f) * 2.4f);
                    break;

                case Mode.Folding:
                {
                    float t = Mathf.Clamp01(_modeTimer / FoldSeconds);
                    SetUnfoldProgress(1f - t);
                    if (t >= 1f) { Teardown(); return; }
                    break;
                }
            }
        }

        /// <summary>펴짐 정도(0=주머니 속, 1=완전히 펴짐)를 가로 스케일 + 알파로 표현한다.</summary>
        private void SetUnfoldProgress(float t)
        {
            if (_container == null) return;
            _container.transform.localScale = new Vector3(Mathf.Lerp(0.15f, 1f, t), Mathf.Lerp(0.7f, 1f, t), 1f);
            for (int i = 0; i < _lines.Count; i++)
            {
                LineRenderer lr = _lines[i];
                if (lr == null) continue;
                Color c = lr.startColor;
                c.a = t;
                lr.startColor = c;
                lr.endColor = c;
            }
        }

        private Vector3 PaperWorldPosition()
        {
            var blackboard = _agent != null ? _agent.Blackboard : null;
            Vector3 body = blackboard != null && blackboard.Body != null
                ? (Vector3)blackboard.Body.position
                : transform.position;

            // 종이를 든 방향은 꺼낸 순간의 방향으로 고정한다 — 들고 있는 동안 캐릭터가 방향을 바꿔도
            // 종이가 몸을 관통해 반대편으로 순간이동하면 안 된다(이 프로젝트 사용자가 순간이동성
            // 아티팩트에 반복적으로 민감했다는 이력, process.md 참고).
            Vector3 target = new Vector3(body.x + PaperOffsetLocalX * _facingAtSpawn, body.y + PaperOffsetLocalY, 0f);

            // 종이가 화면 밖으로 잘려 나가지 않게 뷰포트 안으로 클램프한다 — 캐릭터는 창 상단 테두리나
            // Dock 위에 서 있는 시간이 길다(HardwareReactionRenderer.FollowHead()와 같은 이유이자 같은
            // 관례). 여유가 종이 크기 배수라 비율화의 혜택을 저절로 받는다 — 절대 유닛이었다면 배율
            // 0.5에서 캐릭터 키의 40%가 넘는 값을 화면 안쪽으로 끌어당겨 종이만 몸에서 떨어져 나갔을 것이다.
            Camera cam = blackboard != null ? blackboard.MainCamera : null;
            if (cam != null && cam.orthographic)
            {
                float halfH = cam.orthographicSize;
                float halfW = halfH * cam.aspect;
                float margin = Mathf.Max(PaperHalfWidth, PaperHalfHeight) * 1.6f;
                Vector3 camPos = cam.transform.position;
                target.x = Mathf.Clamp(target.x, camPos.x - halfW + margin, camPos.x + halfW - margin);
                target.y = Mathf.Clamp(target.y, camPos.y - halfH + margin, camPos.y + halfH - margin);
            }
            return target;
        }

        // ==================== 종료 ====================

        private void BeginFold()
        {
            if (_mode == Mode.None || _mode == Mode.Folding) return;
            _mode = Mode.Folding;
            _modeTimer = 0f;
            Debug.Log($"[투두] 다 보여줬으니 종이를 다시 접어 넣습니다({FoldSeconds:F2}초).");
        }

        private void Teardown()
        {
            _lines.Clear();
            if (_container != null)
            {
                Destroy(_container);
                _container = null;
            }
            _mode = Mode.None;
        }

        // ==================== 도형 유틸 ====================

        private LineRenderer CreateLine(string name, Vector3[] points, Color color, float width, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_container.transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.material = _lineMaterial;
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.sortingOrder = SortingOrder;
            lr.loop = loop;
            lr.positionCount = points.Length;
            lr.SetPositions(points);
            return lr;
        }

        /// <summary>다른 렌더러들과 같은 이유로 캐릭터 LineRenderer의 머티리얼을 빌려 쓴다
        /// (Shader.Find는 빌드 스트리핑 위험이 있어 쓰지 않는다).</summary>
        private Material ResolveLineMaterial()
        {
            if (_lineMaterial != null) return _lineMaterial;
            LineRenderer source = GetComponentInChildren<LineRenderer>(true);
            return source != null ? source.sharedMaterial : null;
        }
    }
}
