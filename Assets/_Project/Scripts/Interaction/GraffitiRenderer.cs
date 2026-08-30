using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 화면 낙서(그라피티) 시각 레이어 — docs/UX_FLOW.md 27-3절의 "유저가 보는 것"을 실제로 그리는 소비자.
    ///
    /// ============================================================================
    /// 왜 이 파일이 이제야 생겼는가
    /// ============================================================================
    /// Interaction/GraffitiDirector.cs는 "캐릭터 근처 200~300px 안에서, 어떤 발판(창) 사각형과도 겹치지
    /// 않는 빈 영역"을 찾아 StickmanEventBus.GraffitiOverlayChanged를 발행하는 데까지 이미 완성돼 있었다.
    /// 그런데 <b>그 이벤트를 구독하는 코드가 어디에도 없었다</b>(BattleMinigamePhaseChanged와 똑같은
    /// 상황). 영역 선정도 취소 감시도 정확히 동작했지만 화면에는 아무것도 그려지지 않았다.
    ///
    /// ============================================================================
    /// 절대 원칙 3(유저 자산 불변) — 이 클래스가 하지 않는 일
    /// ============================================================================
    /// 배경화면 이미지 파일도, 배경화면 설정 API도, 다른 창도 <b>전혀 건드리지 않는다</b>. 하는 일은
    /// "우리 오버레이 창 안에 LineRenderer 몇 개를 그렸다가 지우는 것"이 전부다 — 21절 던전 문 오버레이,
    /// 27-2 청소부 복제 스프라이트와 같은 "실제 OS 상태와 완전히 분리된 렌더 레이어" 패턴이다.
    /// 어디에 그릴지조차 스스로 정하지 않고 Director가 검증해 넘겨준 사각형만 쓴다.
    ///
    /// ============================================================================
    /// 연출 (27-3 "유저가 보는 것")
    /// ============================================================================
    /// · 미리 정의된 낙서 4종(웃는 얼굴 / 별 / 졸라맨 / 하트) 중 하나를 무작위로 고른다.
    /// · <b>스프레이하듯 순차적으로</b> 그려진다 — 모든 획의 길이를 합산한 "전체 경로 길이"를 기준으로
    ///   <see cref="DrawSeconds"/>에 걸쳐 진행률을 늘리며, 아직 도달하지 않은 획은 아예 그리지 않고
    ///   진행 중인 획은 마지막 선분을 중간까지만 잘라 그린다. 그래서 선이 한 번에 나타나지 않고 실제로
    ///   "손이 지나가는 것"처럼 자라난다.
    /// · 획 좌표에 아주 작은 무작위 지터를 섞어 자로 그린 도형이 아니라 손으로 뿌린 낙서처럼 보이게 한다
    ///   (지터는 생성 시 1회만 굳히므로 그려지는 동안 선이 떨리지는 않는다).
    /// · 지속시간이 끝나면(Director가 Completed 발행) <see cref="FadeOutSeconds"/>에 걸쳐 옅어지며 사라지고,
    ///   그리는 도중 그 자리에 새 창이 열리면(Cancelled) 훨씬 빠르게 걷어낸다 — 남의 창 위에 낙서가
    ///   겹쳐 보이는 시간을 최소화하는 것이 27-3의 핵심 요구다.
    ///
    /// 색: 캐릭터 잉크(검정)와 같은 색을 쓰면 캐릭터 획의 일부처럼 보여 "낙서"로 읽히지 않으므로,
    /// 스프레이 캔다운 채도 높은 색(<see cref="SprayColors"/>)을 매번 하나 골라 쓴다. 관전 전용이라
    /// 콜라이더를 만들지 않는다 — 27-3 명시: "클릭관통 유지, 부분적 클릭관통 해제 대상 아님".
    /// </summary>
    public sealed class GraffitiRenderer : MonoBehaviour
    {
        private const float DrawSeconds = 1.35f;     // 낙서 한 점을 다 그리는 데 걸리는 시간.
        private const float FadeOutSeconds = 0.8f;   // 정상 종료(27-3 "0.5~1초 페이드아웃") 범위 안.
        private const float CancelFadeSeconds = 0.18f; // 창이 덮쳐온 경우 — 최대한 빨리 걷어낸다.
        private const float StrokeWidthRatio = 0.055f;  // 영역 한 변 대비 획 두께(영역이 커지면 같이 굵어진다).
        private const float JitterRatio = 0.012f;    // 손그림 느낌을 주는 좌표 지터(영역 한 변 대비).
        private const int SortingOrder = 9;          // 캐릭터 획(0~5) 위, 격파 연출(10~15) 아래.

        private static readonly Color[] SprayColors =
        {
            new Color(0.95f, 0.25f, 0.35f, 1f), // 빨강
            new Color(0.20f, 0.55f, 0.95f, 1f), // 파랑
            new Color(0.20f, 0.72f, 0.42f, 1f), // 초록
            new Color(0.98f, 0.62f, 0.12f, 1f), // 주황
        };

        private enum Mode { None, Drawing, Holding, FadingOut }

        private sealed class Stroke
        {
            public LineRenderer Line;
            public Vector3[] Points;   // 월드(컨테이너 로컬) 좌표로 굳힌 획 전체.
            public float StartLength;  // 전체 경로에서 이 획이 시작되는 누적 길이.
            public float[] CumulativeLength; // 각 점까지의 누적 길이(획 내부 기준).
        }

        /// <summary>
        /// 이 렌더러가 담당하는 캐릭터. <b>같은 GameObject의 StickmanAgent만</b> 쓰고 씬 전체 탐색
        /// 폴백은 쓰지 않는다 — 이 프리팹이 복제되면 사본도 이 컴포넌트를 함께 갖게 되는데,
        /// 씬 폴백을 두면 사본의 렌더러가 **플레이어의** StickmanAgent를 자기 것으로 착각해
        /// 전역 이벤트에 반응하고 소환물이 두 벌 그려진다(실측 확인, 2026-08-29). 에이전트가 없으면
        /// 이 컴포넌트는 조용히 아무것도 하지 않는다(DialogueBubbleRenderer의 _requireBoundSpeaker와
        /// 같은 취지의 "화자 미지정이면 그리지 않는다" 규약 — UX_FLOW.md 5절 규칙 7).
        /// </summary>
        private StickmanAgent _agent;
        private Material _lineMaterial;

        private Mode _mode = Mode.None;
        private float _modeTimer;
        private float _fadeSeconds = FadeOutSeconds;

        private GameObject _container;
        private readonly List<Stroke> _strokes = new List<Stroke>();
        private float _totalLength;

        /// <summary>테스트/진단용 — 지금 화면에 낙서가 떠 있는지.</summary>
        public bool IsVisible => _mode != Mode.None;

        private void Awake()
        {
            _agent = GetComponent<StickmanAgent>();
        }

        private void OnEnable() => StickmanEventBus.GraffitiOverlayChanged += OnOverlayChanged;

        private void OnDisable()
        {
            StickmanEventBus.GraffitiOverlayChanged -= OnOverlayChanged;
            // 이 컴포넌트가 꺼질 때 낙서가 화면에 영구히 남지 않게 한다(Director들이 OnDisable()에서
            // SpectacleEventLock을 반드시 반환하는 것과 같은 취지의 정리 관례).
            Teardown();
        }

        private void OnOverlayChanged(GraffitiOverlayEvent evt)
        {
            if (_agent == null) return; // 자기 캐릭터가 없는 사본 — 전역 이벤트를 받아도 무시한다.

            switch (evt.Phase)
            {
                case SpectacleOverlayPhase.Started:
                    Begin(evt.RegionOsScreen);
                    break;
                case SpectacleOverlayPhase.Completed:
                    BeginFade(FadeOutSeconds);
                    break;
                case SpectacleOverlayPhase.Cancelled:
                    BeginFade(CancelFadeSeconds);
                    break;
            }
        }

        // ==================== 생성 ====================

        private void Begin(Rect regionOsScreen)
        {
            Teardown();

            var blackboard = _agent != null ? _agent.Blackboard : null;
            Camera cam = blackboard != null ? blackboard.MainCamera : null;
            if (cam == null || blackboard.Body == null)
            {
                Debug.LogWarning("[그라피티] 그리지 못했습니다 — 카메라/캐릭터 배선이 없습니다.");
                return;
            }

            // OS 화면 사각형 -> 월드 사각형. cameraDepth는 임의값을 넣으면 안 되고 반드시 왕복에 쓸
            // 값을 그대로 재사용해야 한다(Platform/ScreenCoordinateConverter.cs "왕복 정밀도" 참고).
            ScreenCoordinateConverter.WorldToOsScreen(cam, blackboard.Body.position, blackboard.Config, out float depth);
            Vector3 cornerA = ScreenCoordinateConverter.OsScreenToWorld(
                cam, new Vector2(regionOsScreen.xMin, regionOsScreen.yMin), depth, blackboard.Config);
            Vector3 cornerB = ScreenCoordinateConverter.OsScreenToWorld(
                cam, new Vector2(regionOsScreen.xMax, regionOsScreen.yMax), depth, blackboard.Config);

            float xMin = Mathf.Min(cornerA.x, cornerB.x);
            float xMax = Mathf.Max(cornerA.x, cornerB.x);
            float yMin = Mathf.Min(cornerA.y, cornerB.y);
            float yMax = Mathf.Max(cornerA.y, cornerB.y);
            float sizeX = Mathf.Max(0.01f, xMax - xMin);
            float sizeY = Mathf.Max(0.01f, yMax - yMin);
            float span = Mathf.Min(sizeX, sizeY);

            _lineMaterial = ResolveLineMaterial();
            _container = new GameObject("GraffitiOverlay");
            _container.transform.SetParent(null, false);
            _container.transform.position = new Vector3((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f, 0f);

            Color spray = SprayColors[Random.Range(0, SprayColors.Length)];
            float strokeWidth = span * StrokeWidthRatio;
            float jitter = span * JitterRatio;

            List<List<Vector2>> doodle = BuildRandomDoodle(out string doodleName);

            _totalLength = 0f;
            for (int i = 0; i < doodle.Count; i++)
            {
                Stroke stroke = CreateStroke(doodle[i], sizeX, sizeY, jitter, spray, strokeWidth, _totalLength);
                if (stroke == null) continue;
                _totalLength += stroke.CumulativeLength[stroke.CumulativeLength.Length - 1];
                _strokes.Add(stroke);
            }

            _mode = Mode.Drawing;
            _modeTimer = 0f;

            Debug.Log($"[그라피티] '{doodleName}'을(를) 스프레이로 그리기 시작 — 획 {_strokes.Count}개, " +
                $"OS영역 {regionOsScreen}, 월드 중심 {_container.transform.position}, " +
                $"{DrawSeconds:F2}초에 걸쳐 순차 등장. (배경화면 파일/설정 API 호출 0건 — 순수 오버레이)");
        }

        private Stroke CreateStroke(List<Vector2> normalizedPoints, float sizeX, float sizeY,
            float jitter, Color color, float strokeWidth, float startLength)
        {
            if (normalizedPoints == null || normalizedPoints.Count < 2) return null;

            var points = new Vector3[normalizedPoints.Count];
            for (int i = 0; i < normalizedPoints.Count; i++)
            {
                Vector2 n = normalizedPoints[i];
                // 정규화(0~1) -> 컨테이너 로컬(중심 원점). 지터는 여기서 1회만 굳힌다.
                points[i] = new Vector3(
                    (n.x - 0.5f) * sizeX + Random.Range(-jitter, jitter),
                    (n.y - 0.5f) * sizeY + Random.Range(-jitter, jitter),
                    0f);
            }

            var cumulative = new float[points.Length];
            cumulative[0] = 0f;
            for (int i = 1; i < points.Length; i++)
            {
                cumulative[i] = cumulative[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            }
            if (cumulative[cumulative.Length - 1] <= 0f) return null;

            var go = new GameObject("Stroke");
            go.transform.SetParent(_container.transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.material = _lineMaterial;
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = strokeWidth;
            lr.endWidth = strokeWidth;
            lr.numCapVertices = 6;   // 스프레이 자국처럼 끝이 둥글게.
            lr.numCornerVertices = 6;
            lr.sortingOrder = SortingOrder;
            lr.loop = false;         // "그려지는 중"을 표현하려면 항상 열린 폴리라인이어야 한다.
            lr.positionCount = 0;    // 진행률 0에서 시작 — 첫 프레임부터 완성형이 튀어나오지 않게.

            return new Stroke { Line = lr, Points = points, StartLength = startLength, CumulativeLength = cumulative };
        }

        // ==================== 매 프레임 갱신 ====================

        private void LateUpdate()
        {
            if (_mode == Mode.None) return;
            _modeTimer += Time.deltaTime;

            switch (_mode)
            {
                case Mode.Drawing:
                {
                    float t = Mathf.Clamp01(_modeTimer / DrawSeconds);
                    ApplyProgress(t * _totalLength);
                    if (t >= 1f) { _mode = Mode.Holding; _modeTimer = 0f; }
                    break;
                }

                case Mode.Holding:
                    // 유지 시간은 상태 머신(TimedSpectacleState, 3~5초)이 관리한다 — 여기서 따로 세지
                    // 않는다. 두 곳에서 같은 시간을 세면 반드시 어긋난다.
                    break;

                case Mode.FadingOut:
                {
                    float t = Mathf.Clamp01(_modeTimer / _fadeSeconds);
                    SetAlpha(1f - t);
                    if (t >= 1f) Teardown();
                    break;
                }
            }
        }

        /// <summary>전체 경로 길이 기준 진행량까지만 획을 드러낸다("스프레이하듯 순차적으로").</summary>
        private void ApplyProgress(float revealed)
        {
            for (int i = 0; i < _strokes.Count; i++)
            {
                Stroke s = _strokes[i];
                if (s?.Line == null) continue;

                float local = revealed - s.StartLength;
                if (local <= 0f) { s.Line.positionCount = 0; continue; }

                float strokeLength = s.CumulativeLength[s.CumulativeLength.Length - 1];
                if (local >= strokeLength)
                {
                    if (s.Line.positionCount != s.Points.Length)
                    {
                        s.Line.positionCount = s.Points.Length;
                        s.Line.SetPositions(s.Points);
                    }
                    continue;
                }

                // 진행 중인 획 — 완전히 지난 점들 + 마지막 선분을 중간까지 자른 점 하나.
                int last = 1;
                while (last < s.CumulativeLength.Length && s.CumulativeLength[last] < local) last++;

                float segStart = s.CumulativeLength[last - 1];
                float segLength = s.CumulativeLength[last] - segStart;
                float f = segLength > 0f ? Mathf.Clamp01((local - segStart) / segLength) : 1f;
                Vector3 tip = Vector3.Lerp(s.Points[last - 1], s.Points[last], f);

                s.Line.positionCount = last + 1;
                for (int p = 0; p < last; p++) s.Line.SetPosition(p, s.Points[p]);
                s.Line.SetPosition(last, tip);
            }
        }

        private void BeginFade(float seconds)
        {
            if (_mode == Mode.None || _mode == Mode.FadingOut) return;
            // 아직 다 그려지지 않았는데 취소된 경우, 그려진 만큼 그대로 두고 그 상태에서 옅어지게 한다
            // (갑자기 완성형이 나타났다가 사라지면 오히려 눈에 더 띈다).
            _mode = Mode.FadingOut;
            _modeTimer = 0f;
            _fadeSeconds = Mathf.Max(0.01f, seconds);
        }

        private void SetAlpha(float alpha)
        {
            for (int i = 0; i < _strokes.Count; i++)
            {
                LineRenderer lr = _strokes[i]?.Line;
                if (lr == null) continue;
                Color s = lr.startColor;
                Color e = lr.endColor;
                s.a = alpha;
                e.a = alpha;
                lr.startColor = s;
                lr.endColor = e;
            }
        }

        private void Teardown()
        {
            _strokes.Clear();
            _totalLength = 0f;
            if (_container != null)
            {
                Destroy(_container);
                _container = null;
            }
            _mode = Mode.None;
        }

        // ==================== 낙서 도형 정의 (정규화 0~1, y는 위쪽이 +) ====================

        private static List<List<Vector2>> BuildRandomDoodle(out string name)
        {
            switch (Random.Range(0, 4))
            {
                case 0: name = "웃는 얼굴"; return BuildSmiley();
                case 1: name = "별";       return BuildStar();
                case 2: name = "졸라맨";   return BuildStickFigure();
                default: name = "하트";    return BuildHeart();
            }
        }

        private static List<List<Vector2>> BuildSmiley()
        {
            var strokes = new List<List<Vector2>>
            {
                Arc(new Vector2(0.5f, 0.5f), 0.44f, 0f, 360f, 30),         // 얼굴 윤곽
                Arc(new Vector2(0.35f, 0.62f), 0.045f, 0f, 360f, 8),       // 왼쪽 눈
                Arc(new Vector2(0.65f, 0.62f), 0.045f, 0f, 360f, 8),       // 오른쪽 눈
                Arc(new Vector2(0.5f, 0.46f), 0.24f, 200f, 340f, 14),      // 웃는 입
            };
            return strokes;
        }

        private static List<List<Vector2>> BuildStar()
        {
            // 5각 별을 한 획으로 — 실제로 사람이 별을 그리는 순서(한붓그리기)와 같아 진행이 자연스럽다.
            var pts = new List<Vector2>();
            for (int i = 0; i <= 5; i++)
            {
                float angle = (90f + i * 144f) * Mathf.Deg2Rad;
                pts.Add(new Vector2(0.5f + Mathf.Cos(angle) * 0.46f, 0.5f + Mathf.Sin(angle) * 0.46f));
            }
            return new List<List<Vector2>> { pts };
        }

        private static List<List<Vector2>> BuildStickFigure()
        {
            return new List<List<Vector2>>
            {
                Arc(new Vector2(0.5f, 0.82f), 0.14f, 0f, 360f, 18),                                   // 머리
                new List<Vector2> { new Vector2(0.5f, 0.68f), new Vector2(0.5f, 0.34f) },              // 몸통
                new List<Vector2> { new Vector2(0.22f, 0.44f), new Vector2(0.78f, 0.60f) },            // 두 팔(한 획)
                new List<Vector2> { new Vector2(0.24f, 0.06f), new Vector2(0.5f, 0.34f), new Vector2(0.76f, 0.06f) }, // 두 다리
            };
        }

        private static List<List<Vector2>> BuildHeart()
        {
            var pts = new List<Vector2>();
            for (int i = 0; i <= 40; i++)
            {
                float t = i / 40f * Mathf.PI * 2f;
                // 고전적인 하트 매개변수 곡선을 0~1 박스에 맞춰 정규화.
                float x = 16f * Mathf.Pow(Mathf.Sin(t), 3f);
                float y = 13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t) - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t);
                pts.Add(new Vector2(0.5f + x / 34f, 0.5f + y / 34f));
            }
            return new List<List<Vector2>> { pts };
        }

        /// <summary>원/원호를 폴리라인으로. degrees는 화면 기준(반시계 +).</summary>
        private static List<Vector2> Arc(Vector2 center, float radius, float fromDeg, float toDeg, int segments)
        {
            var pts = new List<Vector2>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(fromDeg, toDeg, i / (float)segments) * Mathf.Deg2Rad;
                pts.Add(new Vector2(center.x + Mathf.Cos(a) * radius, center.y + Mathf.Sin(a) * radius));
            }
            return pts;
        }

        /// <summary>BattleMinigameRenderer와 같은 이유로 캐릭터 LineRenderer의 머티리얼을 빌려 쓴다
        /// (Shader.Find는 빌드 스트리핑 위험이 있어 쓰지 않는다).</summary>
        private Material ResolveLineMaterial()
        {
            if (_lineMaterial != null) return _lineMaterial;
            LineRenderer source = GetComponentInChildren<LineRenderer>(true);
            return source != null ? source.sharedMaterial : null;
        }
    }
}
