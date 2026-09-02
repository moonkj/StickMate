using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 윈도우 크래시 시각 레이어 — docs/UX_FLOW.md 27-4절의 "유저가 보는 것"을 실제로 그리는 소비자.
    ///
    /// ============================================================================
    /// 왜 이 파일이 이제야 생겼는가
    /// ============================================================================
    /// Interaction/WindowCrashDirector.cs는 대상(포그라운드) 창 선정 · 3초 오버레이 수명 · 대상 창이
    /// 닫히거나 전체화면 게임이 뜨면 즉시 취소하는 로직까지 이미 완성돼 있었다. 그런데
    /// <b>StickmanEventBus.WindowCrashOverlayChanged를 구독하는 코드가 어디에도 없었고</b> Director 자신도
    /// 씬 어디에도 배치돼 있지 않았다 — 창 도둑/그라피티와 똑같은 "로직은 완성, 화면엔 0픽셀" 실패다.
    ///
    /// ============================================================================
    /// 절대 원칙 2 + 27-4의 가장 중요한 요구: 100% 클릭관통
    /// ============================================================================
    /// 27-4는 이렇게 못박는다 — <b>"보기엔 깨진 유리, 만지면 평범한 창"이 유일하게 허용되는 구현</b>.
    /// 크랙이 떠 있는 3초 동안 대상 창의 클릭/타이핑/스크롤이 조금이라도 막히면 그 자체로 비침해 원칙
    /// 위반이다. 이 클래스는 그것을 <b>구조적으로</b> 보장한다:
    ///
    ///  · 콜라이더를 <b>단 하나도</b> 만들지 않는다. 이 프로젝트에서 클릭관통이 풀리는 유일한 경로는
    ///    UniWindowController의 Raycast 히트테스트가 콜라이더를 발견하는 것뿐이므로(참고:
    ///    Interaction/AppControlDirector.cs의 _menuBlocker, RunawayRenderer의 [간식 주기] 과자),
    ///    콜라이더가 없으면 클릭이 막힐 방법 자체가 없다. <see cref="ActiveColliderCount"/>가 이를
    ///    테스트에서 절대 조건으로 단언할 수 있게 노출한다(항상 0).
    ///  · Platform.ILocalClickCaptureService / Interaction.StickmanClickHitbox를 이 파일 어디서도
    ///    참조하지 않는다(WindowCrashDirector가 같은 이유로 지키고 있는 규약을 렌더러까지 확장).
    ///  · 대상 창 자체에는 어떤 API도 호출하지 않는다 — 아는 것은 Director가 읽기 전용 열거로 얻어
    ///    스냅샷해 넘겨준 사각형 좌표 하나뿐이고, 그 위에 <b>가짜 균열</b>을 겹쳐 그릴 뿐이다.
    ///
    /// ============================================================================
    /// 연출 (27-4 "유저가 보는 것" + "3초 후 원복 연출")
    /// ============================================================================
    /// Started   -> 캐릭터가 서 있는 쪽에 가깝게 타격점을 잡고, 거기서 <see cref="RadialCrackCount"/>개의
    ///              갈라진 금이 창 가장자리를 향해 <see cref="GrowSeconds"/>에 걸쳐 뻗어나간다. 동심으로
    ///              울퉁불퉁한 링 <see cref="RingCount"/>개가 함께 번지고, 타격점에는 짧은 섬광이 터진다.
    /// (3초 유지) -> 유지 시간은 Director가 센다(windowCrashOverlayDurationSeconds). 렌더러는 세지 않는다 —
    ///              GraffitiRenderer의 Holding 주석과 같은 이유(두 곳에서 같은 시간을 세면 어긋난다).
    /// Completed -> 27-4 "유리 조각처럼 파편화되어 부서져 떨어지는 페이드아웃(0.3~0.5초)". 금 조각 하나하나가
    ///              중력을 받아 회전하며 떨어지고 동시에 옅어진다. 창은 처음부터 멀쩡했으므로 "복구"가
    ///              아니라 오버레이가 걷히는 것뿐이다.
    /// Cancelled -> 대상 창이 닫힘/최소화, 전체화면 게임 감지, 긴급정지. 파편 연출 없이 즉시 걷어낸다.
    /// </summary>
    public sealed class WindowCrashRenderer : MonoBehaviour
    {
        // ==================== 연출 상수 ====================

        private const int RadialCrackCount = 9;      // 타격점에서 뻗어나가는 주 균열.
        private const int RingCount = 3;             // 동심 균열 링.
        private const int RingSegments = 22;
        private const int CrackSegments = 5;         // 균열 하나를 이루는 꺾임 수(많을수록 지그재그가 촘촘).
        private const float CrackAngleJitter = 0.26f; // 균열이 직선이 아니라 갈라져 보이게 하는 각도 지터(rad).
        private const float RingRadiusJitter = 0.18f; // 링이 정원이 아니라 깨진 유리처럼 보이게 하는 반경 지터.

        private const float GrowSeconds = 0.22f;     // "쨍!" 하고 순식간에 번진다 — 느리면 파괴로 안 읽힌다.
        private const float ShatterSeconds = 0.42f;  // 27-4 "0.3~0.5초" 범위 안.
        private const float CancelFadeSeconds = 0.12f;
        private const float FlashSeconds = 0.16f;

        private const float ShardGravity = 9.0f;
        private const float ShardSpeedMin = 0.5f;
        private const float ShardSpeedMax = 2.4f;
        private const float ShardSpinMax = 520f;

        private const float StrokeWidthRatio = 0.010f;
        private const float StrokeWidthMin = 0.030f;
        private const float StrokeWidthMax = 0.085f;

        // 캐릭터 획(0~5)보다 뒤 = 해머를 든 캐릭터가 균열 앞에 서 있는 것처럼 보인다.
        private const int SortingCrack = -1;

        // ★ 2026-08-29 — 사용자 신고 "눈같이 내리는건 뭐야 캐릭하고 겹치는데".
        //
        // 예전 값은 거의 흰색(0.93, 0.95, 1.0, a=0.95)이었다. 이 앱은 밝은 배경(backgroundFallbackColor
        // 0.94 회색) 위에 검은 선화를 그리므로, **흰 파편은 배경 위에서는 거의 보이지 않고 오직 캐릭터의
        // 검은 선을 가로지를 때만 보인다.** 즉 유저 눈에는 "캐릭터 위에서만 어른거리는 정체불명의
        // 흰 조각"이 되고, 그게 신고 문구 그대로다 — 색 대비가 가장 나쁜 곳에만 정보가 남는 최악의 조합.
        //
        // 그래서 파편/균열도 잉크색을 따르게 한다(힘줄 표시 Interaction/WindowTheftRenderer와 같은 처리,
        // 말풍선이 이미 쓰는 StickConfig.ResolveInkColor() 경로). 배경 어디에서나 균일하게 읽히고,
        // 흰색/검은색 프리셋을 바꿔도 함께 따라간다. 알파도 낮춰(CrackMaxAlpha) 캐릭터보다 확실히
        // 뒤로 물러나 보이게 한다 — sortingOrder는 이미 캐릭터 뒤(-1)지만, 선 굵기와 밝기가 비슷하면
        // 앞뒤가 눈으로 구분되지 않아 "겹친다"고 읽힌다.
        private const float CrackMaxAlpha = 0.45f;
        private Color _crackColor = new Color(0f, 0f, 0f, CrackMaxAlpha);
        private Color _crackShadowColor = new Color(0f, 0f, 0f, CrackMaxAlpha * 0.7f);
        private static readonly Color FlashColor = new Color(1f, 1f, 1f, 1f);

        private enum Mode { None, Growing, Holding, Shattering, FadingOut }

        private sealed class Shard
        {
            public Transform Root;
            public LineRenderer Line;
            public Vector3[] Points;
            public Vector2 Velocity;
            public float Spin;
        }

        /// <summary>
        /// 이 렌더러가 담당하는 캐릭터. <b>같은 GameObject의 StickmanAgent만</b> 쓰고 씬 전체 탐색
        /// 폴백은 쓰지 않는다 — 이 프리팹이 복제되면 사본도 이 컴포넌트를 함께 갖게 되고,
        /// 폴백을 두면 크랙이 두 벌 그려진다(2026-08-29 격파 미니게임에서 실측 확인된 버그 — 기능은
        /// 2026-09-02에 삭제됐지만 이 함정은 모든 렌더러에 그대로 남아 있다).
        /// </summary>
        private StickmanAgent _agent;
        private Material _lineMaterial;

        private Mode _mode = Mode.None;
        private float _modeTimer;
        private float _fadeSeconds = CancelFadeSeconds;

        private GameObject _container;
        private readonly List<Shard> _shards = new List<Shard>(RadialCrackCount + RingCount + 1);
        private LineRenderer _flash;

        // ==================== 테스트/진단용 관찰 창구 ====================

        /// <summary>지금 화면에 균열 오버레이가 떠 있는지.</summary>
        public bool IsVisible => _mode != Mode.None;

        /// <summary>이 오버레이가 지금 실제로 만들어낸 LineRenderer 개수. 정리가 끝나면 반드시 0이다.</summary>
        public int ActiveVisualCount =>
            _container != null ? _container.GetComponentsInChildren<LineRenderer>(true).Length : 0;

        /// <summary>
        /// 이 오버레이가 만든 콜라이더 수 — <b>항상 0이어야 한다</b>. 27-4/27-7 체크리스트의 핵심 검증
        /// 포인트("크랙 레이어가 3초 내내 100% 클릭관통 상태인지")를 PlayMode 테스트가 절대 조건으로
        /// 단언할 수 있게 하는 창구다.
        /// </summary>
        public int ActiveColliderCount =>
            _container != null ? _container.GetComponentsInChildren<Collider2D>(true).Length : 0;

        // ==================== 생애주기 ====================

        private void Awake()
        {
            _agent = GetComponent<StickmanAgent>();
        }

        private void OnEnable() => StickmanEventBus.WindowCrashOverlayChanged += OnOverlayChanged;

        private void OnDisable()
        {
            StickmanEventBus.WindowCrashOverlayChanged -= OnOverlayChanged;
            // 이 컴포넌트가 꺼질 때 균열이 화면에 영구히 남지 않게 한다(Director들이 OnDisable()에서
            // SpectacleEventLock을 반드시 반환하는 것과 같은 취지의 정리 관례).
            Teardown();
        }

        private void OnOverlayChanged(WindowCrashOverlayEvent evt)
        {
            if (_agent == null) return; // 자기 캐릭터가 없는 사본 — 전역 이벤트를 받아도 무시한다.

            switch (evt.Phase)
            {
                case SpectacleOverlayPhase.Started:
                    Begin(evt.TargetRectOsScreen);
                    break;
                case SpectacleOverlayPhase.Completed:
                    BeginShatter();
                    break;
                case SpectacleOverlayPhase.Cancelled:
                    BeginFade(CancelFadeSeconds, "취소(대상 창 닫힘/최소화, 전체화면 게임 감지, 긴급정지)");
                    break;
            }
        }

        // ==================== 생성 ====================

        private void Begin(Rect targetRectOsScreen)
        {
            Teardown();

            var blackboard = _agent != null ? _agent.Blackboard : null;
            Camera cam = blackboard != null ? blackboard.MainCamera : null;
            if (cam == null || blackboard.Body == null)
            {
                Debug.LogWarning("[창크래시] 균열을 그리지 못했습니다 — 카메라/캐릭터 배선이 없습니다.");
                return;
            }

            Vector3 characterWorld = blackboard.Body.position;
            // 균열/파편 색을 이번 발동 시점의 잉크 프리셋으로 확정한다(위 색 상수 주석 참고).
            Color crackInk = blackboard.Config != null ? blackboard.Config.ResolveInkColor() : Color.black;
            _crackColor = new Color(crackInk.r, crackInk.g, crackInk.b, CrackMaxAlpha);
            _crackShadowColor = new Color(crackInk.r, crackInk.g, crackInk.b, CrackMaxAlpha * 0.7f);

            ScreenCoordinateConverter.WorldToOsScreen(cam, characterWorld, blackboard.Config, out float depth);
            Vector3 cornerA = ScreenCoordinateConverter.OsScreenToWorld(
                cam, new Vector2(targetRectOsScreen.xMin, targetRectOsScreen.yMin), depth, blackboard.Config);
            Vector3 cornerB = ScreenCoordinateConverter.OsScreenToWorld(
                cam, new Vector2(targetRectOsScreen.xMax, targetRectOsScreen.yMax), depth, blackboard.Config);

            float xMin = Mathf.Min(cornerA.x, cornerB.x);
            float xMax = Mathf.Max(cornerA.x, cornerB.x);
            float yMin = Mathf.Min(cornerA.y, cornerB.y);
            float yMax = Mathf.Max(cornerA.y, cornerB.y);
            float sizeX = Mathf.Max(0.05f, xMax - xMin);
            float sizeY = Mathf.Max(0.05f, yMax - yMin);

            _lineMaterial = ResolveLineMaterial();
            _container = new GameObject("WindowCrashOverlay");
            _container.transform.SetParent(null, false);
            Vector3 center = new Vector3((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f, 0f);
            _container.transform.position = center;

            // 타격점 — 캐릭터(해머를 휘두른 쪽)에 가깝게 잡되 창 안쪽 60% 영역을 벗어나지 않게 한다.
            // 가장자리에 붙으면 균열이 한쪽으로만 뻗어 "내리쳤다"로 읽히지 않는다.
            float impactX = Mathf.Clamp(characterWorld.x - center.x, -sizeX * 0.30f, sizeX * 0.30f);
            float impactY = Mathf.Clamp(characterWorld.y - center.y, -sizeY * 0.30f, sizeY * 0.30f);
            var impact = new Vector3(impactX, impactY, 0f);

            float stroke = Mathf.Clamp(Mathf.Min(sizeX, sizeY) * StrokeWidthRatio, StrokeWidthMin, StrokeWidthMax);
            float maxRadius = Mathf.Max(sizeX, sizeY) * 0.62f;

            // (1) 방사형 주 균열.
            float angleStep = Mathf.PI * 2f / RadialCrackCount;
            for (int i = 0; i < RadialCrackCount; i++)
            {
                float baseAngle = i * angleStep + Random.Range(-angleStep * 0.28f, angleStep * 0.28f);
                Vector3[] pts = BuildCrack(impact, baseAngle, maxRadius * Random.Range(0.55f, 1f), sizeX, sizeY);
                AddShard($"Crack{i}", pts, i % 3 == 0 ? _crackShadowColor : _crackColor, stroke, loop: false);
            }

            // (2) 동심 균열 링 — 깨진 유리의 "거미줄" 느낌은 방사선만으로는 안 난다.
            for (int r = 0; r < RingCount; r++)
            {
                float radius = maxRadius * (0.26f + 0.28f * r);
                Vector3[] pts = BuildJaggedRing(impact, radius, sizeX, sizeY);
                AddShard($"CrackRing{r}", pts, _crackColor, stroke * 0.8f, loop: true);
            }

            // (3) 타격 섬광.
            _flash = CreateLine("ImpactFlash", BuildCircle(impact, Mathf.Min(sizeX, sizeY) * 0.06f, 12),
                FlashColor, stroke * 2.2f, loop: true);

            _mode = Mode.Growing;
            _modeTimer = 0f;
            ApplyGrowth(0f);

            Debug.Log($"[창크래시] 가짜 균열 오버레이 생성 — OS영역 {targetRectOsScreen}, 월드중심 {center}, " +
                $"월드크기 {sizeX:F2}x{sizeY:F2}, 타격점(로컬) {impact}, 균열 {RadialCrackCount}개 + 링 {RingCount}개, " +
                $"시각 오브젝트 {ActiveVisualCount}개, 콜라이더 {ActiveColliderCount}개(항상 0 = 100% 클릭관통). " +
                "★ 대상 창에는 어떤 API도 호출하지 않는다 — 균열이 떠 있는 동안에도 그 창은 평소처럼 클릭/타이핑된다(27-4).");
        }

        /// <summary>타격점에서 한 방향으로 지그재그하며 뻗어나가는 균열 하나(창 사각형 안쪽으로 클램프).</summary>
        private static Vector3[] BuildCrack(Vector3 origin, float angle, float length, float sizeX, float sizeY)
        {
            var pts = new Vector3[CrackSegments + 1];
            pts[0] = origin;
            Vector3 cursor = origin;
            float a = angle;
            float step = length / CrackSegments;

            for (int i = 1; i <= CrackSegments; i++)
            {
                a += Random.Range(-CrackAngleJitter, CrackAngleJitter);
                cursor += new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * step * Random.Range(0.75f, 1.25f);
                // 창 밖으로 균열이 삐져나가면 "그 창이 깨졌다"로 읽히지 않는다 — 항상 안쪽으로 가둔다.
                cursor.x = Mathf.Clamp(cursor.x, -sizeX * 0.5f, sizeX * 0.5f);
                cursor.y = Mathf.Clamp(cursor.y, -sizeY * 0.5f, sizeY * 0.5f);
                pts[i] = cursor;
            }
            return pts;
        }

        private static Vector3[] BuildJaggedRing(Vector3 origin, float radius, float sizeX, float sizeY)
        {
            var pts = new Vector3[RingSegments];
            for (int i = 0; i < RingSegments; i++)
            {
                float a = i / (float)RingSegments * Mathf.PI * 2f;
                float r = radius * (1f + Random.Range(-RingRadiusJitter, RingRadiusJitter));
                pts[i] = new Vector3(
                    Mathf.Clamp(origin.x + Mathf.Cos(a) * r, -sizeX * 0.5f, sizeX * 0.5f),
                    Mathf.Clamp(origin.y + Mathf.Sin(a) * r, -sizeY * 0.5f, sizeY * 0.5f),
                    0f);
            }
            return pts;
        }

        /// <summary>
        /// 균열 하나를 <b>자기 원점을 가진 별도 GameObject</b>로 만든다 — Completed 시 이 오브젝트를
        /// 통째로 회전/낙하시키면 "유리 조각이 부서져 떨어지는" 연출이 추가 지오메트리 없이 나온다.
        /// </summary>
        private void AddShard(string name, Vector3[] pts, Color color, float width, bool loop)
        {
            // 조각의 무게중심을 자기 로컬 원점으로 옮긴다(그래야 제자리에서 회전한다).
            Vector3 pivot = Vector3.zero;
            for (int i = 0; i < pts.Length; i++) pivot += pts[i];
            pivot /= Mathf.Max(1, pts.Length);

            var local = new Vector3[pts.Length];
            for (int i = 0; i < pts.Length; i++) local[i] = pts[i] - pivot;

            LineRenderer lr = CreateLine(name, local, color, width, loop);
            lr.transform.localPosition = pivot;

            _shards.Add(new Shard
            {
                Root = lr.transform,
                Line = lr,
                Points = local,
                Velocity = new Vector2(Random.Range(-1f, 1f), Random.Range(-0.2f, 0.6f)).normalized
                           * Random.Range(ShardSpeedMin, ShardSpeedMax),
                Spin = Random.Range(-ShardSpinMax, ShardSpinMax),
            });
        }

        // ==================== 매 프레임 갱신 ====================

        private void LateUpdate()
        {
            using var __stall = global::StickMate.Platform.StallAttribution.Section(global::StickMate.Platform.StallSection.Renderers);   // [스톨구간] 계측
            if (_mode == Mode.None) return;
            _modeTimer += Time.deltaTime;

            switch (_mode)
            {
                case Mode.Growing:
                {
                    float t = Mathf.Clamp01(_modeTimer / GrowSeconds);
                    ApplyGrowth(t);
                    TickFlash();
                    if (t >= 1f) { _mode = Mode.Holding; _modeTimer = 0f; }
                    break;
                }

                case Mode.Holding:
                    // 유지 시간은 WindowCrashDirector가 관리한다(windowCrashOverlayDurationSeconds).
                    // 여기서 따로 세지 않는다 — 두 곳에서 같은 시간을 세면 반드시 어긋난다.
                    TickFlash();
                    break;

                case Mode.Shattering:
                {
                    float t = Mathf.Clamp01(_modeTimer / ShatterSeconds);
                    TickShards(Time.deltaTime);
                    SetAlpha(1f - t);
                    if (t >= 1f) { Teardown(); return; }
                    break;
                }

                case Mode.FadingOut:
                {
                    float t = Mathf.Clamp01(_modeTimer / _fadeSeconds);
                    SetAlpha(1f - t);
                    if (t >= 1f) { Teardown(); return; }
                    break;
                }
            }
        }

        /// <summary>균열이 타격점에서 바깥으로 "번져나가는" 진행률(0~1)만큼만 드러낸다.</summary>
        private void ApplyGrowth(float t)
        {
            for (int i = 0; i < _shards.Count; i++)
            {
                Shard s = _shards[i];
                if (s?.Line == null || s.Points == null || s.Points.Length < 2) continue;

                if (s.Line.loop)
                {
                    // 링은 잘라 그리면 어색하므로 진행률 후반부에 통째로 나타나며 굵어진다.
                    bool show = t >= 0.55f;
                    s.Line.positionCount = show ? s.Points.Length : 0;
                    if (show) s.Line.SetPositions(s.Points);
                    continue;
                }

                float exact = Mathf.Clamp01(t) * (s.Points.Length - 1);
                int full = Mathf.FloorToInt(exact);
                float frac = exact - full;

                if (full <= 0 && frac <= 0f) { s.Line.positionCount = 0; continue; }
                if (full >= s.Points.Length - 1)
                {
                    if (s.Line.positionCount != s.Points.Length)
                    {
                        s.Line.positionCount = s.Points.Length;
                        s.Line.SetPositions(s.Points);
                    }
                    continue;
                }

                s.Line.positionCount = full + 2;
                for (int p = 0; p <= full; p++) s.Line.SetPosition(p, s.Points[p]);
                s.Line.SetPosition(full + 1, Vector3.Lerp(s.Points[full], s.Points[full + 1], frac));
            }
        }

        private void TickFlash()
        {
            if (_flash == null) return;
            float age = _mode == Mode.Growing ? _modeTimer : FlashSeconds;
            float t = Mathf.Clamp01(age / FlashSeconds);
            Color c = FlashColor;
            c.a = 1f - t;
            _flash.startColor = c;
            _flash.endColor = c;
        }

        private void TickShards(float dt)
        {
            for (int i = 0; i < _shards.Count; i++)
            {
                Shard s = _shards[i];
                if (s?.Root == null) continue;
                s.Velocity += Vector2.down * ShardGravity * dt;
                s.Root.localPosition += (Vector3)(s.Velocity * dt);
                s.Root.localRotation *= Quaternion.Euler(0f, 0f, s.Spin * dt);
            }
        }

        // ==================== 종료 ====================

        private void BeginShatter()
        {
            if (_mode == Mode.None || _mode == Mode.Shattering || _mode == Mode.FadingOut) return;
            // 아직 다 번지지 않았어도 그려진 만큼 그대로 부순다(갑자기 완성형이 나타났다 사라지면 더 눈에 띈다).
            _mode = Mode.Shattering;
            _modeTimer = 0f;
            Debug.Log($"[창크래시] 3초 경과 — 유리 조각 {_shards.Count}개가 부서져 떨어지며 {ShatterSeconds:F2}초에 걸쳐 사라집니다. " +
                "창은 처음부터 멀쩡했으므로 '복구'가 아니라 오버레이가 걷히는 것뿐입니다(27-4).");
        }

        private void BeginFade(float seconds, string reason)
        {
            if (_mode == Mode.None || _mode == Mode.FadingOut) return;
            _mode = Mode.FadingOut;
            _modeTimer = 0f;
            _fadeSeconds = Mathf.Max(0.01f, seconds);
            Debug.Log($"[창크래시] 균열 오버레이 즉시 정리 — {reason}, {_fadeSeconds:F2}초 페이드아웃.");
        }

        private void SetAlpha(float alpha)
        {
            for (int i = 0; i < _shards.Count; i++)
            {
                LineRenderer lr = _shards[i]?.Line;
                if (lr == null) continue;
                Color s = lr.startColor;
                Color e = lr.endColor;
                s.a = alpha;
                e.a = alpha;
                lr.startColor = s;
                lr.endColor = e;
            }
            if (_flash != null)
            {
                Color f = _flash.startColor;
                f.a = Mathf.Min(f.a, alpha);
                _flash.startColor = f;
                _flash.endColor = f;
            }
        }

        private void Teardown()
        {
            _shards.Clear();
            _flash = null;
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
            lr.numCapVertices = 3;
            lr.numCornerVertices = 3;
            lr.sortingOrder = SortingCrack;
            lr.loop = loop;
            lr.positionCount = points.Length;
            lr.SetPositions(points);
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

        /// <summary>GraffitiRenderer와 같은 이유로 캐릭터 LineRenderer의 머티리얼을
        /// 빌려 쓴다(Shader.Find는 빌드 스트리핑 위험이 있어 쓰지 않는다).</summary>
        private Material ResolveLineMaterial()
        {
            if (_lineMaterial != null) return _lineMaterial;
            LineRenderer source = GetComponentInChildren<LineRenderer>(true);
            return source != null ? source.sharedMaterial : null;
        }
    }
}
