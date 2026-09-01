using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;
using StickMate.Dialogue;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ PC 하드웨어 반응 시각 레이어 — docs/UX_FLOW.md 23절 "반응별 설계" 4종의 은유를 실제로 그리는 소비자.
    ///
    /// ============================================================================
    /// 왜 이 파일이 이제야 생겼는가
    /// ============================================================================
    /// Interaction/HardwareReactionDirector.cs는 4개 신호(배터리/충전/CPU/네트워크)의 저빈도 공용 폴링,
    /// 지속조건, 회복 게이트, 우선순위 리졸버(배터리&gt;CPU&gt;네트워크&gt;충전)까지 완성돼 있었다. 그런데
    /// <b>StickmanEventBus.HardwareReactionChanged를 구독하는 코드가 어디에도 없었고</b> Director 자신도
    /// 씬 어디에도 배치돼 있지 않았다 — 창 도둑/창 크래시와 똑같은 "로직 완성, 화면엔 0픽셀" 실패다.
    ///
    /// ============================================================================
    /// 무엇을 그리는가 (23절 "반응별 설계" — 숫자가 아니라 은유)
    /// ============================================================================
    /// 캐릭터 머리 위에 <b>작은 이모트 하나</b>를 띄우고, 그 반응이 끝날 때(Active=false) 걷는다.
    ///  · LowBattery  : 거의 빈 배터리 아이콘이 느리게 깜빡인다(23절 "힘없이 비틀거리거나 하품").
    ///  · HighCpu     : 열기 물결 3줄이 피어오르고 땀방울이 떨어진다(23절 "헥헥거리며 부채질, 더워하는 은유").
    ///  · NetworkDown : 와이파이 호 3개에 사선이 그어져 있고 호가 순차로 깜빡인다(23절 "안테나를 찾듯 두리번").
    ///  · Charging    : 번개 표시 + 위로 솟는 반짝임(23절 "에너지 차오르는 파티클").
    ///
    /// <b>실제 %/수치는 절대 표시하지 않는다</b> — 23절이 명시적으로 금지했고(OS 자체 알림과 중복/충돌
    /// 방지, 이 앱은 감성적 은유만 담당), 애초에 HardwareReactionEvent에 수치가 실려 오지도 않는다.
    ///
    /// ============================================================================
    /// 절대 원칙 — 이 클래스가 하지 않는 일 (27-7 체크리스트)
    /// ============================================================================
    /// OS 설정을 바꾸는 어떤 제어(쓰기) API도 호출하지 않는다. 애초에 이 클래스는 하드웨어를 조회조차
    /// 하지 않는다 — Director가 읽기 전용 조회로 판정해 넘겨준 <see cref="HardwareReactionKind"/> 하나만
    /// 보고 미리 정해진 은유를 고를 뿐이다. 콜라이더도 만들지 않는다(순수 관전 연출 = 클릭관통 유지).
    ///
    /// SpectacleEventLock에 참여하지 않는 것도 의도다(Phase 4 설계 결정 5 / HardwareReactionDirector의
    /// 클래스 문서) — 이 연출은 ChangeState()를 호출해 단일 상태 슬롯을 다투지 않는 가벼운 머리 위
    /// 이모트라, 창 도둑/크래시 같은 능동 개입 스펙터클과 같은 상호배제 세트에 들어가지 않는다.
    /// 그래서 창 도둑이 진행되는 중에도 배터리 이모트는 그대로 떠 있을 수 있고, 그것이 정상이다.
    /// </summary>
    public sealed class HardwareReactionRenderer : MonoBehaviour
    {
        // ==================== 연출 상수 ====================

        private const float PopInSeconds = 0.28f;     // 등장(작게 튀어오르며).
        private const float FadeOutSeconds = 0.40f;   // 조건 회복/우선순위 교체로 사라질 때.
        // 캐릭터 루트(Rigidbody2D.position)는 **발 높이**가 로컬 y=0이다. 현재 프리팹 실측(2026-08-29):
        // 전신 2.27 / 머리 중심 2.05 / 머리 반경 0.22 -> 정수리 2.27.
        // 이력: 처음 1.05로 잡았다가 이모트가 **가슴팍에 겹쳐** 보여 정수리 위로 올렸고(2.32),
        // 그 다음 라운드에 사용자가 **머리와 겹친다**고 다시 신고했다 — 세로 값 하나만 만지는 방식의
        // 한계다. 지금은 세로(비율) + 가로(머리 옆 대각선)로 배치를 다시 잡았다(아래 배치 설계 절).
        // ★ 2026-08-29 리더 지시 — 아래 치수는 전부 **캐릭터 전신 높이 대비 비율**이다.
        // 절대 유닛으로 두면 캐릭터 크기를 바꾸는 순간(사용자가 "절반 크기 + 추후 조정 가능"을 요구했다)
        // 전부 다시 겹친다. 기준 높이는 StickmanAgent.CharacterTotalHeightWorld 하나뿐이며,
        // 지금 프리팹 실측 2.27유닛에서 아래 비율을 곱하면 검증을 마친 종전 값이 그대로 나온다
        // (2.32 / 0.045 / 0.42 / 0.052).
        private const float HeadOffsetRatio = 1.0220f;   // 발 원점 기준 이모트 중심 높이(2.32 / 2.27).
        private const float BobAmplitudeRatio = 0.0198f; // 아주 약한 상하 부유(0.045 / 2.27).
        private const float IconScaleRatio = 0.1850f;    // 정규화(-1~1) 도형을 월드로 옮기는 배율(0.42 / 2.27).
        private const float StrokeWidthRatio = 0.0229f;  // 이모트 획 두께(0.052 / 2.27).

        private const float BobSpeed = 1.9f;
        private const float ClampMarginRatio = 1.25f; // 화면 경계에서 띄울 여유(IconScale 배수) — FollowHead 참고.

        /// <summary>이 캐릭터의 전신 높이(월드 유닛). 위 비율들의 유일한 기준값.</summary>
        private float Height => _agent != null ? _agent.CharacterTotalHeightWorld : 2.27f;

        private float HeadOffsetY => Height * HeadOffsetRatio;
        private float BobAmplitude => Height * BobAmplitudeRatio;
        private float IconScale => Height * IconScaleRatio;
        private float StrokeWidth => Height * StrokeWidthRatio;
        private const int SortingOrder = 8;           // 캐릭터 획(0~5) 위, 그라피티(9)/격파(10~15) 아래.

        private const float SparkleLifeSeconds = 0.85f;
        private const float SparkleSpawnInterval = 0.30f;
        private const int SparkleMaxAlive = 4;
        private const float DropletFallSeconds = 0.9f;

        // ★ 2026-08-29 비율화 누락분 보강 — 위 배치 상수는 전부 전신 높이 대비 비율로 옮겼는데,
        // 땀방울/반짝임의 **이동 속도**만 절대 유닛(월드/초)으로 남아 있었다. 속도가 절대값이면
        // 캐릭터가 작아질수록 같은 수명 동안 몸 길이 대비 더 멀리 이동한다 — 배율 1.0에서 땀방울은
        // 전신 높이의 0.25배만 떨어지지만 배율 0.5에서는 0.49배(가슴~허리 높이까지)를 떨어져,
        // 사용자가 신고한 "눈같이 내리는" 알갱이가 캐릭터 쪽으로 더 깊이 파고든다.
        // 종전 검증값(0.55 / 0.62 / 0.08)을 당시 기준 높이 2.27로 나눠 비율로 환산했으므로,
        // 배율 1.0에서는 지금까지와 완전히 같은 움직임이 그대로 나온다.
        private const float SparkleRiseSpeedRatio = 0.2423f; // 충전 반짝임 상승(0.55 / 2.27).
        private const float DropletFallSpeedRatio = 0.2731f; // 땀방울 낙하(0.62 / 2.27).
        private const float DropletDriftSpeedRatio = 0.0352f; // 땀방울 좌우 흔들림 최대(0.08 / 2.27).

        private static readonly Color BatteryColor = new Color(0.93f, 0.35f, 0.28f, 1f); // 거의 빈 배터리 = 경고 계열.
        private static readonly Color HeatColor = new Color(0.98f, 0.55f, 0.16f, 1f);
        private static readonly Color SweatColor = new Color(0.36f, 0.68f, 0.96f, 1f);
        private static readonly Color NetworkColor = new Color(0.55f, 0.57f, 0.62f, 1f);
        private static readonly Color NetworkSlashColor = new Color(0.90f, 0.28f, 0.28f, 1f);
        private static readonly Color ChargeColor = new Color(0.36f, 0.82f, 0.38f, 1f);

        private enum Mode { None, PoppingIn, Showing, FadingOut }

        private sealed class Mote
        {
            public Transform Root;
            public LineRenderer Line;
            public float Age;
            public float Life;
            public Vector2 Velocity;
        }

        /// <summary>
        /// 이 렌더러가 담당하는 캐릭터. <b>같은 GameObject의 StickmanAgent만</b> 쓰고 씬 전체 탐색
        /// 폴백은 쓰지 않는다 — 이 프리팹이 복제되면 사본도 이 컴포넌트를 함께 갖게 되고, 폴백을 두면
        /// 이모트가 사본 머리 위에도 한 벌 더 뜬다(2026-08-29 격파 미니게임에서 실측 확인된 버그와
        /// 같은 함정). 애초에 배치하지 않는 것이 1차 방어이고 이 가드가 2차다.
        /// </summary>
        private StickmanAgent _agent;
        private Material _lineMaterial;

        private Mode _mode = Mode.None;
        private float _modeTimer;
        private float _showTimer;

        private GameObject _container;
        private HardwareReactionKind _kind;
        private readonly List<LineRenderer> _iconLines = new List<LineRenderer>(8);
        private readonly List<LineRenderer> _blinkLines = new List<LineRenderer>(4);
        private readonly List<Mote> _motes = new List<Mote>(SparkleMaxAlive);
        private float _moteTimer;

        // ==================== 테스트/진단용 관찰 창구 ====================

        /// <summary>지금 머리 위에 하드웨어 반응 이모트가 떠 있는지.</summary>
        public bool IsVisible => _mode != Mode.None;

        /// <summary>지금 표현 중인 반응 종류(떠 있지 않으면 null).</summary>
        public HardwareReactionKind? VisibleKind => _mode != Mode.None ? _kind : (HardwareReactionKind?)null;

        /// <summary>
        /// 지금 이모트가 차지하고 있는 월드 y 상한. 말풍선(Dialogue/DialogueBubbleRenderer)이 그 위로
        /// 비켜설 수 있도록 열어둔 <b>읽기 전용</b> 창구다 — 이쪽에서 말풍선을 건드리는 일은 없다.
        /// 떠 있지 않으면 false.
        /// </summary>
        public bool TryGetOccupiedTopWorldY(out float topWorldY)
        {
            topWorldY = 0f;
            if (_mode == Mode.None || _container == null) return false;
            topWorldY = _container.transform.position.y + IconScale;
            return true;
        }

        /// <summary>이 이모트가 지금 실제로 만들어낸 LineRenderer 개수. 정리가 끝나면 반드시 0이다.</summary>
        public int ActiveVisualCount =>
            _container != null ? _container.GetComponentsInChildren<LineRenderer>(true).Length : 0;

        /// <summary>이 이모트가 만든 콜라이더 수 — 항상 0이어야 한다(관전 전용, 클릭관통 유지).</summary>
        public int ActiveColliderCount =>
            _container != null ? _container.GetComponentsInChildren<Collider2D>(true).Length : 0;

        // ==================== 생애주기 ====================

        private void Awake()
        {
            _agent = GetComponent<StickmanAgent>();
        }

        private void OnEnable() => StickmanEventBus.HardwareReactionChanged += OnReactionChanged;

        private void OnDisable()
        {
            StickmanEventBus.HardwareReactionChanged -= OnReactionChanged;
            // 이 컴포넌트가 꺼질 때 이모트가 화면에 영구히 남지 않게 한다(Director들이 OnDisable()에서
            // SpectacleEventLock을 반드시 반환하는 것과 같은 취지의 정리 관례).
            Teardown();
        }

        private void OnReactionChanged(HardwareReactionEvent evt)
        {
            if (_agent == null) return; // 자기 캐릭터가 없는 사본 — 전역 이벤트를 받아도 무시한다.

            if (evt.Active) Begin(evt.Kind);
            else if (_mode != Mode.None && _kind == evt.Kind) BeginFade(evt.Kind);
        }

        // ==================== 생성 ====================

        private void Begin(HardwareReactionKind kind)
        {
            Teardown();
            ChooseSide(); // 이번 이모트가 머리 어느 쪽으로 나갈지 여기서 한 번 정한다.

            var blackboard = _agent != null ? _agent.Blackboard : null;
            if (blackboard == null || blackboard.Body == null)
            {
                Debug.LogWarning("[하드웨어] 반응 이모트를 그리지 못했습니다 — 캐릭터 배선이 없습니다.");
                return;
            }

            _lineMaterial = ResolveLineMaterial();
            _kind = kind;
            _container = new GameObject("HardwareReactionEmote");
            _container.transform.SetParent(null, false);
            _container.transform.position = HeadWorldPosition();

            switch (kind)
            {
                case HardwareReactionKind.LowBattery: BuildLowBattery(); break;
                case HardwareReactionKind.HighCpu: BuildHighCpu(); break;
                case HardwareReactionKind.NetworkDown: BuildNetworkDown(); break;
                default: BuildCharging(); break;
            }

            _mode = Mode.PoppingIn;
            _modeTimer = 0f;
            _showTimer = 0f;
            _moteTimer = 0f;

            Debug.Log($"[하드웨어] {KindLabel(kind)} 반응 이모트 표시 시작 — 캐릭터 머리 " +
                $"{(_sideSign > 0f ? "오른" : "왼")}쪽 대각선 위(전신 {Height:F2}유닛 기준 " +
                $"가로 {HeadSideOffsetX:F2} / 세로 {HeadOffsetY:F2}유닛), " +
                $"시각 오브젝트 {ActiveVisualCount}개, 콜라이더 {ActiveColliderCount}개(항상 0). " +
                "★ 수치(%)는 표시하지 않고 은유만 그린다(23절), OS 제어(쓰기) API 호출 0건(27-7).");
        }

        /// <summary>23절 "힘없이 비틀거리거나 하품 + 배고픈 은유" — 거의 빈 배터리가 느리게 깜빡인다.</summary>
        private void BuildLowBattery()
        {
            // 본체(가로로 누운 배터리).
            _iconLines.Add(CreateLine("BatteryBody", new[]
            {
                new Vector3(-0.85f, -0.42f, 0f), new Vector3(0.72f, -0.42f, 0f),
                new Vector3(0.72f, 0.42f, 0f), new Vector3(-0.85f, 0.42f, 0f),
            }, BatteryColor, loop: true));

            // 단자.
            _iconLines.Add(CreateLine("BatteryTerminal", new[]
            {
                new Vector3(0.72f, -0.17f, 0f), new Vector3(0.92f, -0.17f, 0f),
                new Vector3(0.92f, 0.17f, 0f), new Vector3(0.72f, 0.17f, 0f),
            }, BatteryColor, loop: true));

            // 거의 바닥난 잔량 막대 — 깜빡이는 대상.
            var fill = CreateLine("BatteryFill", new[]
            {
                new Vector3(-0.72f, 0f, 0f), new Vector3(-0.42f, 0f, 0f),
            }, BatteryColor, loop: false);
            fill.startWidth = StrokeWidth * 4.2f;
            fill.endWidth = StrokeWidth * 4.2f;
            _blinkLines.Add(fill);
        }

        /// <summary>23절 "헥헥거리며 부채질" — 열기 물결이 피어오르고 땀방울이 떨어진다.</summary>
        private void BuildHighCpu()
        {
            for (int i = 0; i < 3; i++)
            {
                float x = -0.55f + i * 0.55f;
                var pts = new Vector3[7];
                for (int p = 0; p < pts.Length; p++)
                {
                    float t = p / (float)(pts.Length - 1);
                    pts[p] = new Vector3(x + Mathf.Sin(t * Mathf.PI * 2f + i) * 0.16f, -0.7f + t * 1.4f, 0f);
                }
                _iconLines.Add(CreateLine($"HeatWave{i}", pts, HeatColor, loop: false));
            }
        }

        /// <summary>23절 "안테나를 찾듯 두리번거리는 가벼운 코믹 연출" — 사선 그은 와이파이 호.</summary>
        private void BuildNetworkDown()
        {
            for (int i = 0; i < 3; i++)
            {
                float radius = 0.34f + i * 0.30f;
                var arc = new List<Vector3>();
                const int seg = 12;
                for (int p = 0; p <= seg; p++)
                {
                    float a = Mathf.Lerp(35f, 145f, p / (float)seg) * Mathf.Deg2Rad;
                    arc.Add(new Vector3(Mathf.Cos(a) * radius, -0.55f + Mathf.Sin(a) * radius, 0f));
                }
                _blinkLines.Add(CreateLine($"WifiArc{i}", arc.ToArray(), NetworkColor, loop: false));
            }

            _iconLines.Add(CreateLine("WifiDot", BuildCircle(new Vector3(0f, -0.52f, 0f), 0.08f, 8),
                NetworkColor, loop: true));

            // 끊김을 뜻하는 사선(깜빡이지 않고 항상 또렷하게 — 이게 "끊겼다"의 핵심 기호다).
            _iconLines.Add(CreateLine("WifiSlash", new[]
            {
                new Vector3(-0.72f, 0.78f, 0f), new Vector3(0.72f, -0.72f, 0f),
            }, NetworkSlashColor, loop: false));
        }

        /// <summary>23절 "밥 먹듯 기뻐하는 연출 + 에너지 차오르는 파티클".</summary>
        private void BuildCharging()
        {
            _iconLines.Add(CreateLine("Bolt", new[]
            {
                new Vector3(0.26f, 0.92f, 0f),
                new Vector3(-0.30f, 0.10f, 0f),
                new Vector3(0.06f, 0.10f, 0f),
                new Vector3(-0.22f, -0.92f, 0f),
                new Vector3(0.36f, -0.02f, 0f),
                new Vector3(0.00f, -0.02f, 0f),
            }, ChargeColor, loop: true));
        }

        // ==================== 매 프레임 갱신 ====================

        private void LateUpdate()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.Renderers);   // [스톨구간] 계측
            if (_mode == Mode.None) return;

            float dt = Time.deltaTime;
            _modeTimer += dt;
            _showTimer += dt;

            FollowHead();

            switch (_mode)
            {
                case Mode.PoppingIn:
                {
                    float t = Mathf.Clamp01(_modeTimer / PopInSeconds);
                    // 살짝 오버슈트하며 튀어오른다(작은 이모트라 이 정도 과장이 있어야 눈에 띈다).
                    float scale = Mathf.LerpUnclamped(0.2f, 1f, 1f - Mathf.Pow(1f - t, 3f)) * (1f + 0.12f * Mathf.Sin(t * Mathf.PI));
                    SetScale(scale);
                    SetAlpha(t);
                    if (t >= 1f) { _mode = Mode.Showing; _modeTimer = 0f; SetScale(1f); SetAlpha(1f); }
                    break;
                }

                case Mode.Showing:
                    TickBlink();
                    TickMotes(dt, spawn: true);
                    break;

                case Mode.FadingOut:
                {
                    float t = Mathf.Clamp01(_modeTimer / FadeOutSeconds);
                    SetAlpha(1f - t);
                    SetScale(Mathf.Lerp(1f, 0.72f, t));
                    TickMotes(dt, spawn: false);
                    if (t >= 1f) { Teardown(); return; }
                    break;
                }
            }
        }

        /// <summary>
        /// 이모트를 캐릭터 머리 위에 붙여 따라다니게 하되, <b>반드시 화면 안에 머무르게 클램프</b>한다.
        ///
        /// 이 클램프가 없으면 이 기능은 실사용에서 거의 보이지 않는다(2026-08-29 실측으로 확인):
        /// 캐릭터는 창의 상단 테두리를 발판으로 삼기 때문에 화면 최상단(예: OS y=33)에 서 있는 시간이
        /// 길고, 그때 머리 위 <see cref="HeadOffsetY"/>유닛(약 43 OS px)은 화면 밖이라 이모트가 통째로
        /// 잘려 나간다 — "로직은 도는데 화면엔 안 보인다"는 이 프로젝트의 반복된 실패 모드가 좌표
        /// 차원에서 재현되는 셈이다. 화면 위쪽 경계에 닿으면 이모트가 머리와 살짝 겹치더라도 보이는
        /// 쪽을 택한다(안 보이는 것보다 겹치는 것이 낫다).
        /// </summary>
        private void FollowHead()
        {
            if (_container == null) return;
            Vector3 target = HeadWorldPosition();
            target.y += Mathf.Sin(_showTimer * BobSpeed) * BobAmplitude;

            Camera cam = _agent != null && _agent.Blackboard != null ? _agent.Blackboard.MainCamera : null;

            if (cam != null && cam.orthographic)
            {
                float halfH = cam.orthographicSize;
                float halfW = halfH * cam.aspect;
                float margin = IconScale * ClampMarginRatio;
                Vector3 camPos = cam.transform.position;
                target.x = Mathf.Clamp(target.x, camPos.x - halfW + margin, camPos.x + halfW - margin);
                target.y = Mathf.Clamp(target.y, camPos.y - halfH + margin, camPos.y + halfH - margin);
            }

            _container.transform.position = target;
        }

        // ============================================================================
        // ★ 머리 위 3요소(머리 / 이모트 / 말풍선) 세로·가로 배치 재설계
        //   (사용자 실측 신고 2026-08-29: "머리위에 저 주황색... 캐릭하고 겹치는데")
        // ============================================================================
        // 무엇이 잘못돼 있었나 — 값 하나가 아니라 **배치 자체**가 없었다:
        //
        //   캐릭터 정수리 ........ 2.27  (SceneBootstrapper: headY 2.05 + HeadVisualRadius 0.22)
        //   이모트 아래 끝 ....... 2.02  (HeadOffsetY 2.32 - 0.7*IconScale, CPU 열기 물결 기준)
        //   이모트 위 끝 ......... 2.61 ~ 2.74 (종류별 최대 세로 반경까지)
        //   말풍선 꼬리 끝 ....... 2.39  (머리중심 2.05 + HeadTopWorldOffset 0.34)
        //
        // 즉 이모트는 아래로 **정수리 안쪽 0.25유닛까지 파고들고**, 위로는 **꼬리 끝을 넘어서** 있었다.
        // 세 요소가 전부 x=0 한 줄에 있었으니 겹치지 않을 도리가 없다. 이전 라운드에서 1.05 -> 2.32로
        // 올린 것은 "가슴팍 겹침"만 본 수정이라 머리 겹침이 그대로 남았다 — 그래서 이번엔 값을 더
        // 올리는 대신 **차원을 하나 더 쓴다**.
        //
        // 새 배치(리더 승인 방향 "머리 옆 대각선"):
        //   · 이모트는 머리 **옆 대각선 위**로 나간다  -> 이 파일(HeadSideOffsetX)
        //   · 말풍선은 이모트 **실제 상단 위**로 올라간다 -> DialogueBubbleRenderer.TickEmoteLift()
        //   세로로만 쌓지 않기 때문에 말풍선이 화면 위로 밀려나는 양도 최소로 유지된다(리더 지시).
        //
        // 왜 HeadOffsetY(2.32)는 그대로 두는가: 그 값은 "가슴팍 겹침"을 고치며 올린 것이라 내리면
        // 그 버그가 되살아난다(리더 명시). 가로로 비키면 내릴 필요 자체가 없다.
        //
        // 왜 가로 오프셋인가(정량): 머리 원은 중심 (0, 2.05) 반경 0.22 + 외곽선 두께 절반 ≈ 0.27.
        // 이모트는 가장 큰 종류 기준 반경 IconScale(0.42, 정규화 도형이 -1~1을 넘지 않으므로 상한이다).
        // 두 원이 닿지 않으려면
        // 가로 거리가 0.27 + 0.42 = 0.69보다 커야 하고, 여기에 여유 0.17을 더해 0.86으로 잡았다.
        // 말풍선 꼬리(반폭 약 0.29유닛)와도 자동으로 벌어진다(0.86 - 0.42 = 0.44 > 0.29).

        /// <summary>이모트를 머리 옆으로 밀어놓는 가로 거리(전신 높이 대비 비율, 0.86 / 2.27).
        /// 위 산출 근거 참고 — 머리 반경 + 이모트 반경 + 여유를 전부 비율로 환산한 값이다.</summary>
        private const float HeadSideOffsetRatio = 0.3789f;

        private float HeadSideOffsetX => Height * HeadSideOffsetRatio;


        /// <summary>이번 이모트가 나가 있는 방향(+1 오른쪽 / -1 왼쪽). 소환 시 한 번 정하고 그 이모트가
        /// 사라질 때까지 바꾸지 않는다 — 캐릭터가 화면 중앙을 지날 때마다 좌우로 튀면 산만하다.</summary>
        private float _sideSign = 1f;

        /// <summary>이모트를 어느 쪽으로 낼지 정한다. **화면 안쪽(중앙 쪽)**을 고르는 이유는 바깥쪽으로
        /// 내면 아래 FollowHead의 화면 클램프가 도로 끌어당겨 회피가 통째로 무효가 되기 때문이다 —
        /// 이 앱의 캐릭터는 화면 가장자리에 서 있는 시간이 길다.</summary>
        private void ChooseSide()
        {
            Camera cam = _agent != null && _agent.Blackboard != null ? _agent.Blackboard.MainCamera : null;
            var blackboard = _agent != null ? _agent.Blackboard : null;
            float x = blackboard != null && blackboard.Body != null ? blackboard.Body.position.x : transform.position.x;
            _sideSign = cam != null && x > cam.transform.position.x ? -1f : 1f;
        }

        private Vector3 HeadWorldPosition()
        {
            var blackboard = _agent != null ? _agent.Blackboard : null;
            Vector3 basePos = blackboard != null && blackboard.Body != null
                ? (Vector3)blackboard.Body.position
                : transform.position;
            // ★ 머리 **옆 대각선 위** — 세로로만 쌓으면 머리와도 말풍선과도 겹친다(위 배치 설계 참고).
            return new Vector3(basePos.x + _sideSign * HeadSideOffsetX, basePos.y + HeadOffsetY, 0f);
        }

        /// <summary>배터리 잔량 막대 / 와이파이 호 — 종류마다 "깜빡임"의 의미가 다르다(경고 vs 탐색).</summary>
        private void TickBlink()
        {
            if (_blinkLines.Count == 0) return;

            for (int i = 0; i < _blinkLines.Count; i++)
            {
                LineRenderer lr = _blinkLines[i];
                if (lr == null) continue;

                float alpha;
                if (_kind == HardwareReactionKind.NetworkDown)
                {
                    // 호가 안쪽 -> 바깥쪽으로 순차 점등했다 꺼진다("신호를 찾는 중").
                    float phase = Mathf.Repeat(_showTimer * 1.6f - i * 0.22f, 1f);
                    alpha = phase < 0.55f ? 1f : 0.18f;
                }
                else
                {
                    // 배터리는 느리고 무겁게 깜빡인다("힘없이").
                    alpha = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(_showTimer * 3.1f));
                }

                Color c = lr.startColor;
                c.a = alpha;
                lr.startColor = c;
                lr.endColor = c;
            }
        }

        /// <summary>CPU 과부하의 땀방울 / 충전 중의 반짝임 — 종류가 그 둘일 때만 생성된다.</summary>
        private void TickMotes(float dt, bool spawn)
        {
            bool wantsMotes = _kind == HardwareReactionKind.HighCpu || _kind == HardwareReactionKind.Charging;

            if (spawn && wantsMotes && _container != null)
            {
                _moteTimer += dt;
                if (_moteTimer >= SparkleSpawnInterval && _motes.Count < SparkleMaxAlive)
                {
                    _moteTimer = 0f;
                    SpawnMote();
                }
            }

            for (int i = _motes.Count - 1; i >= 0; i--)
            {
                Mote m = _motes[i];
                if (m?.Line == null) { _motes.RemoveAt(i); continue; }

                m.Age += dt;
                float t = Mathf.Clamp01(m.Age / m.Life);
                if (t >= 1f)
                {
                    if (m.Root != null) Destroy(m.Root.gameObject);
                    _motes.RemoveAt(i);
                    continue;
                }

                m.Root.localPosition += (Vector3)(m.Velocity * dt);
                Color c = m.Line.startColor;
                c.a = (1f - t) * CurrentGlobalAlpha();
                m.Line.startColor = c;
                m.Line.endColor = c;
            }
        }

        private void SpawnMote()
        {
            bool charging = _kind == HardwareReactionKind.Charging;
            Color color = charging ? ChargeColor : SweatColor;

            Vector3[] shape = charging
                ? new[]
                {
                    // 작은 4각 반짝임.
                    new Vector3(0f, 0.10f, 0f), new Vector3(0.05f, 0f, 0f),
                    new Vector3(0f, -0.10f, 0f), new Vector3(-0.05f, 0f, 0f),
                }
                : BuildCircle(Vector3.zero, 0.07f, 8); // 땀방울.

            var go = new GameObject(charging ? "ChargeSparkle" : "SweatDrop");
            go.transform.SetParent(_container.transform, false);
            go.transform.localPosition = new Vector3(Random.Range(-0.7f, 0.7f) * IconScale,
                (charging ? -0.9f : 0.6f) * IconScale, 0f);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.material = _lineMaterial;
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = StrokeWidth * 0.85f;
            lr.endWidth = StrokeWidth * 0.85f;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.sortingOrder = SortingOrder;
            lr.loop = true;
            lr.positionCount = shape.Length;
            for (int i = 0; i < shape.Length; i++) lr.SetPosition(i, shape[i] * IconScale);

            _motes.Add(new Mote
            {
                Root = go.transform,
                Line = lr,
                Age = 0f,
                Life = charging ? SparkleLifeSeconds : DropletFallSeconds,
                // 절대 유닛이 아니라 전신 높이 비율 — 위 SparkleRiseSpeedRatio 주석의 근거 참고.
                Velocity = charging
                    ? new Vector2(0f, Height * SparkleRiseSpeedRatio)
                    : new Vector2(Random.Range(-1f, 1f) * Height * DropletDriftSpeedRatio,
                                  -Height * DropletFallSpeedRatio),
            });
        }

        private float CurrentGlobalAlpha()
            => _mode == Mode.FadingOut ? Mathf.Clamp01(1f - _modeTimer / FadeOutSeconds) : 1f;

        // ==================== 종료 ====================

        private void BeginFade(HardwareReactionKind kind)
        {
            if (_mode == Mode.None || _mode == Mode.FadingOut) return;
            _mode = Mode.FadingOut;
            _modeTimer = 0f;
            Debug.Log($"[하드웨어] {KindLabel(kind)} 반응 종료 — 조건이 회복됐거나 우선순위가 교체됐습니다. " +
                $"{FadeOutSeconds:F2}초 페이드아웃 후 이모트를 전부 제거합니다.");
        }

        private void SetAlpha(float alpha)
        {
            for (int i = 0; i < _iconLines.Count; i++) ApplyAlpha(_iconLines[i], alpha);
            // 깜빡이는 선은 TickBlink가 매 프레임 다시 쓰므로, 페이드 중에는 그쪽을 멈추고 여기서만 제어한다.
            if (_mode != Mode.Showing)
            {
                for (int i = 0; i < _blinkLines.Count; i++) ApplyAlpha(_blinkLines[i], alpha);
            }
        }

        private static void ApplyAlpha(LineRenderer lr, float alpha)
        {
            if (lr == null) return;
            Color s = lr.startColor;
            Color e = lr.endColor;
            s.a = alpha;
            e.a = alpha;
            lr.startColor = s;
            lr.endColor = e;
        }

        private void SetScale(float scale)
        {
            if (_container == null) return;
            _container.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void Teardown()
        {
            _iconLines.Clear();
            _blinkLines.Clear();
            _motes.Clear();
            if (_container != null)
            {
                Destroy(_container);
                _container = null;
            }
            _mode = Mode.None;
        }

        // ==================== 도형 유틸 ====================

        private LineRenderer CreateLine(string name, Vector3[] normalizedPoints, Color color, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_container.transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.material = _lineMaterial;
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = StrokeWidth;
            lr.endWidth = StrokeWidth;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.sortingOrder = SortingOrder;
            lr.loop = loop;
            lr.positionCount = normalizedPoints.Length;
            for (int i = 0; i < normalizedPoints.Length; i++) lr.SetPosition(i, normalizedPoints[i] * IconScale);
            return lr;
        }

        private static Vector3[] BuildCircle(Vector3 center, float radius, int segments)
        {
            var pts = new Vector3[Mathf.Max(3, segments)];
            for (int i = 0; i < pts.Length; i++)
            {
                float a = i / (float)pts.Length * Mathf.PI * 2f;
                pts[i] = new Vector3(center.x + Mathf.Cos(a) * radius, center.y + Mathf.Sin(a) * radius, 0f);
            }
            return pts;
        }

        private static string KindLabel(HardwareReactionKind kind)
        {
            switch (kind)
            {
                case HardwareReactionKind.LowBattery: return "배터리 부족";
                case HardwareReactionKind.HighCpu: return "CPU 과부하";
                case HardwareReactionKind.NetworkDown: return "네트워크 끊김";
                default: return "충전 중";
            }
        }

        /// <summary>GraffitiRenderer/BattleMinigameRenderer와 같은 이유로 캐릭터 LineRenderer의 머티리얼을
        /// 빌려 쓴다(Shader.Find는 빌드 스트리핑 위험이 있어 쓰지 않는다).</summary>
        private Material ResolveLineMaterial()
        {
            if (_lineMaterial != null) return _lineMaterial;
            LineRenderer source = GetComponentInChildren<LineRenderer>(true);
            return source != null ? source.sharedMaterial : null;
        }
    }
}
