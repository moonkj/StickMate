using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Dialogue;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 만화 레터링 배치 회귀 테스트 — 2026-08-29 사용자 요구
    ///   "말풍선 말고 텍스트만 캐릭터 걸어가는방향 반대쪽 대각선 상단에 나타나게 해줘" / "만화처럼".
    ///
    /// ============================================================================
    /// 이 파일이 잡으려는 실패
    /// ============================================================================
    /// (1) <b>방향이 뒤집히는 실패</b>. "진행 방향의 반대쪽"은 부호 하나로 결정되고, 그 부호는
    ///     실수로 뒤집혀도 **컴파일도 되고 예외도 나지 않는다** — 글자가 캐릭터의 진행 방향 앞을
    ///     가로막고 서 있게 될 뿐이다. 사용자가 요구한 의도(앞을 가리지 말 것) 자체가 조용히 반대가
    ///     되는 유형이라, 눈으로 보지 않으면 영영 발견되지 않는다.
    /// (2) <b>오프셋이 절대 상수로 굳는 실패</b>. 이 프로젝트가 이미 여러 번 겪은 유형이다
    ///     (PlayMode/RendererScaleRatioTests.cs 참고). characterScale은 사용자가 계속 바꾸므로
    ///     (지금 0.75) 간격이 캐릭터 키를 따라가지 않으면 배율을 바꾸는 순간 글자가 캐릭터에서
    ///     떨어져 나가거나 머리를 파고든다.
    /// (3) <b>화면 밖 잘림</b>. 캐릭터는 화면 좌우 끝에 서 있는 시간이 길다(가장자리 회전/벽타기).
    /// (4) <b>말풍선 도형이 되살아나는 실패</b>. "텍스트만"이라는 요구가 깨지는 지점.
    /// (5) <b>글자가 조용히 수평으로 돌아가는 실패</b> (2026-08-31 사용자 요구 "캐릭터가 말하는
    ///     텍스트는 좀 대각선으로 작성해줘"). 이 실패는 실제로 한 번 일어났다 — 회전 코드는
    ///     처음부터 있었지만 각도가 ±2.5도(눈에 띄지 않게 하려던 "손글씨 흔들림")였고 부호까지
    ///     대사 해시에서 사실상 무작위였다. 컴파일도 되고 로그도 정상이며 회전값도 0이 아니어서,
    ///     "회전이 걸려 있는가"만 보는 검사로는 절대 잡히지 않는다. 그래서 아래 (E)는 각도의
    ///     <b>크기 하한</b>과 <b>부호 규칙</b>을 함께 못박는다.
    ///
    /// ============================================================================
    /// 무엇을 어떻게 단언하는가 — 비율 비교가 아니라 **절대 조건**이다
    /// ============================================================================
    ///  (A) 방향: 캐릭터가 오른쪽으로 걸으면 글자 블록 전체가 기준점보다 **왼쪽**에 있고, 왼쪽으로
    ///      걸으면 **오른쪽**에 있다. 그리고 두 경우 모두 기준점보다 **위**에 있다(대각선 상단).
    ///      "오른쪽일 때와 왼쪽일 때가 서로 반대"라는 상대 비교가 아니라, 각각이 절대적으로
    ///      어느 쪽에 있어야 하는지를 못박는다.
    ///  (B) 간격: 기준점에서 글자 블록 **가장자리**까지의 간격을, 같은 카메라로 투영한 **캐릭터
    ///      키**로 나눈 값이 배율 1.0 / 0.75 / 0.5 어디서나 정확히 TextGapXRatio(0.20) /
    ///      TextGapYRatio(0.10)이다. 자기 자신을 기준으로 하는 비율 비교가 아니라 바깥에서 온
    ///      숫자와 맞대는 절대 단언이고, 화면 해상도와도 무관하다.
    ///  (C) 클램프: 어떤 자리에서도 글자 블록이 화면 밖으로 나가지 않고, 화면 끝에서는 안쪽으로
    ///      밀리는 대신 **반대쪽으로 뒤집힌다**(밀면 글자가 머리 위로 올라타 요구가 깨진다).
    ///  (D) 도형 부재: 렌더러가 만든 캔버스에 **켜져 있는 Image가 하나도 없다**.
    ///  (E) 기울기: 글자 블록의 RectTransform이 실제로 회전해 있고, 그 각도의 크기가 눈에 보이는
    ///      하한(<see cref="MinVisibleTiltDegrees"/>) 이상이며, 부호가 **놓인 쪽의 거울상**이다
    ///      (왼쪽 위 -> 반시계 +, 오른쪽 위 -> 시계 -). 그리고 클램프에 쓰이는 블록 크기가
    ///      **회전한 축 정렬 경계**여야 한다 — 회전 전 크기로 클램프하면 화면 끝에서 모서리만 잘린다.
    ///
    /// ============================================================================
    /// 네거티브 컨트롤 (이 테스트가 정말 무언가를 보고 있는가)
    /// ============================================================================
    /// <see cref="NegativeControl_SameSidePlacementBreaksTheOppositeDirectionAssertion"/>이
    /// "방향 반전을 되돌린"(= 진행 방향과 **같은 쪽**에 놓는) 배치를 같은 순수 함수로 계산해,
    /// (A)의 조건이 실제로 깨진다는 것을 같은 파일 안에서 증명한다. 즉 (A)가 통과하는 이유가
    /// "조건이 너무 헐거워서"가 아님을 스스로 보인다.
    ///
    /// ============================================================================
    /// 리그를 손으로 조립하는 이유
    /// ============================================================================
    /// 프리팹/씬은 StickConfig.characterScale 하나로 구워지므로 한 번 실행에 세 배율을 동시에 볼 수
    /// 없다. Core/StickmanMetrics.cs가 실측하는 소스(루트의 비-트리거 CapsuleCollider2D,
    /// "Head/HeadOutline" 링 LineRenderer, "LeftArm"/"LeftLeg" 부착 높이)만 갖춘 최소 리그를 배율별로
    /// 만들어 비교한다 — 렌더러가 캐릭터 치수를 묻는 창구가 StickmanMetrics 하나뿐이라 가능한
    /// 방식이고, 그 자체가 "치수 조회 경로가 정말 하나인가"에 대한 확인이기도 하다.
    /// </summary>
    public sealed class DialogueComicTextPlacementTests
    {
        private const string TalkText = "산책 중";

        /// <summary>배율 1.0 프리팹의 실측 치수(Editor/SceneBootstrapper.cs가 굽는 값 그대로).</summary>
        private const float BaseHeight = StickConfig.BaselineCharacterTotalHeight; // 2.2746944
        private const float BaseHeadRadius = 0.22f;
        private const float BaseShoulderY = 1.7646944f;
        private const float BaseHipY = 0.9346944f;

        /// <summary>DialogueBubbleRenderer의 TextGapXRatio / TextGapYRatio와 같은 값(바깥에서 온 숫자).</summary>
        private const float ExpectedGapXRatio = 0.20f;
        private const float ExpectedGapYRatio = 0.10f;

        // ---- (E) 기울기 계약의 바깥 숫자 ----
        /// <summary>이 테스트가 렌더러에 주입하는 기울기 기준값(도). 출하 기본값과 같다.</summary>
        private const float TiltBaseDegrees = 8f;
        /// <summary>DialogueBubbleRenderer.ComicTiltJitterRatio와 같은 값 — 크기에만 붙는 결정적 편차.</summary>
        private const float TiltJitterRatio = 0.25f;
        /// <summary>
        /// "눈에 보이는 기울기"의 하한(도). ★ 이 숫자가 (5) 실패를 잡는 핵심이다 — 직전 구현의
        /// 최대 각도가 2.5도였으므로, 그 시절 코드로 되돌아가면 이 단언에서 반드시 걸린다.
        /// 4도의 근거: 한 줄(약 20pt) x 80pt 블록에서 양 끝 높이차가 5.6pt로 글자 높이의 4분의 1이다 —
        /// 그 아래로는 "기울인 것"이 아니라 "레이아웃이 약간 어긋난 것"으로 보인다.
        /// </summary>
        private const float MinVisibleTiltDegrees = 4f;
        /// <summary>기울기 테스트에서 쓰는 글자 크기(캔버스 유닛 기준값). 배율 0.75 x 만화 배율
        /// 0.875를 거쳐 21pt가 되고, dpi 오버라이드 1과 곱해 물리 21px — 렌더러의 회전 하한
        /// (물리 14px)을 여유 있게 넘긴다. 이 리그를 출하 조합(10pt x Retina 2x = 20px)과 같은
        /// 글리프 크기대에 맞추기 위한 값이지 임의의 큰 숫자가 아니다.</summary>
        private const int TiltTestFontSize = 32;

        /// <summary>진입할 때마다 지정된 텍스트로 DialogueIntent를 하나 만드는 테스트 상태
        /// (DialogueBubbleContractTests와 같은 방식 — 물리/씬 없이 대사 하나를 정확히 재현한다).</summary>
        private sealed class TalkingState : IStickmanState
        {
            private readonly string _text;
            public TalkingState(StickmanStateId id, string text) { StateId = id; _text = text; }
            public StickmanStateId StateId { get; }
            public void Enter(StateTransitionContext context) => _ = new DialogueIntent(context, id => _text);
            public void Tick(float deltaTime) { }
            public void Exit() { }
        }

        private sealed class SilentState : IStickmanState
        {
            public SilentState(StickmanStateId id) { StateId = id; }
            public StickmanStateId StateId { get; }
            public void Enter(StateTransitionContext context) { }
            public void Tick(float deltaTime) { }
            public void Exit() { }
        }

        private readonly List<GameObject> _spawned = new List<GameObject>(4);
        private readonly List<StickConfig> _configs = new List<StickConfig>(2);
        /// <summary>리그 이름을 유일하게 만드는 일련번호 — 아래 SpawnRig 주석의 함정 참고.</summary>
        private int _rigSerial;
        private readonly List<StickmanStateMachine> _machines = new List<StickmanStateMachine>(4);
        private readonly List<Camera> _suspendedCameras = new List<Camera>(2);
        private Camera _camera;

        [SetUp]
        public void SetUp()
        {
            // ★ 먼저 씬에 남아 있는 다른 카메라를 전부 끈다 (실측으로 확인한 함정, 2026-08-29).
            //   PlayMode 테스트는 한 플레이어 세션에서 이어 돌고, 앞선 테스트가 실제 씬을 로드해 두면
            //   그 Main Camera(orthographicSize = 12)가 그대로 남는다. 그러면 렌더러의
            //   ResolveCamera()가 Camera.main으로 **그 카메라**를 집어, 렌더러는 12로 투영하고 이
            //   테스트는 3으로 환산해 비율이 정확히 1/4로 어긋난다(실제로 0.20 대신 0.050이 나왔다).
            //   테스트 종료 시 원래대로 되돌린다.
            var existing = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] == null || !existing[i].enabled) continue;
                existing[i].enabled = false;
                _suspendedCameras.Add(existing[i]);
            }

            // 직교 카메라 하나를 화면 중앙에 세운다. 배율별 리그를 전부 이 카메라로 투영하므로
            // "캔버스 유닛 / 캐릭터 키"라는 비율 단언이 해상도와 무관하게 성립한다.
            var camGo = new GameObject("ComicTextTestCamera");
            camGo.tag = "MainCamera";
            _camera = camGo.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 3f;
            _camera.transform.position = new Vector3(0f, 1.2f, -10f);
            _spawned.Add(camGo);
        }

        [TearDown]
        public void TearDown()
        {
            // 살아 있는 DialogueIntent가 정적 이벤트 구독을 물고 다음 테스트로 넘어가지 않도록,
            // 침묵 상태로 한 번 더 전이시켜 전부 만료시킨다(세대 증가 = 일괄 만료).
            for (int i = 0; i < _machines.Count; i++)
            {
                StickmanStateMachine m = _machines[i];
                if (m != null && m.CurrentStateId != StickmanStateId.Ragdoll) m.ChangeState(StickmanStateId.Ragdoll);
            }
            _machines.Clear();
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            }
            _spawned.Clear();
            // 주입한 테스트 전용 StickConfig 정리. ★ 렌더러의 _config를 먼저 null로 되돌린 뒤 지운다 —
            // 파괴된 ScriptableObject를 붙든 채 남으면 다음 프레임의 LateUpdate가 그것을 읽는다.
            for (int i = 0; i < _configs.Count; i++)
            {
                if (_configs[i] != null) Object.DestroyImmediate(_configs[i]);
            }
            _configs.Clear();
            _camera = null;
            for (int i = 0; i < _suspendedCameras.Count; i++)
            {
                if (_suspendedCameras[i] != null) _suspendedCameras[i].enabled = true;
            }
            _suspendedCameras.Clear();
        }

        // ────────────────────────────────────────────────────────────────────────
        // (A) 방향 — 진행 방향의 반대쪽 대각선 상단
        // ────────────────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator TextIsPlacedOppositeToWalkingDirection()
        {
            // 오른쪽으로 걸을 때와 왼쪽으로 걸을 때를 한 테스트 안에서 나란히 본다
            // ([Values] + [UnityTest] 조합은 Unity Test Framework 버전에 따라 지원이 갈려 쓰지 않는다).
            float[] facings = { 1f, -1f };
            foreach (float facing in facings)
            {
            DialogueBubbleRenderer renderer = SpawnRig(0.75f, facing, out StickmanStateMachine machine);
            yield return null; // 렌더러의 Start()까지 한 번 돌린다.

            machine.Start(StickmanStateId.Attack);
            Assert.IsTrue(renderer.IsBubbleVisible, "대사가 표시되지 않았다 — 배치를 잴 수 없다.");
            yield return null; // LateUpdate 한 번 = 실제 배치 계산.

            Vector2 anchor = renderer.LastTextAnchorCanvas;
            Vector2 center = renderer.LastTextCenterCanvas;
            Vector2 size = renderer.LastTextSizeCanvas;
            Assert.Greater(size.x, 1f, "글자 블록 크기가 잡히지 않았다(폰트/레이아웃 경로가 죽었다).");

            float expectedSide = -Mathf.Sign(facing);
            Assert.AreEqual(expectedSide, renderer.LastTextSideSign, 1e-4f,
                $"진행 방향 {(facing > 0f ? "오른쪽" : "왼쪽")}인데 글자가 " +
                $"{(renderer.LastTextSideSign > 0f ? "오른쪽" : "왼쪽")}에 놓였다 — " +
                "사용자 요구는 '걸어가는 방향 반대쪽'이다(진행 방향 앞을 글자가 가리면 안 된다).");

            // ★ 절대 조건: 글자 블록 **전체**(중심 ± 반폭)가 기준점의 반대쪽에 있다.
            //    중심만 보면 블록이 기준점을 물고 걸쳐 있어도 통과해 버린다.
            float near = center.x - expectedSide * size.x * 0.5f; // 캐릭터 쪽 가장자리
            if (expectedSide < 0f)
            {
                Assert.Less(near, anchor.x,
                    "오른쪽으로 걷는데 글자 블록이 기준점보다 오른쪽까지 걸쳐 있다(진행 방향을 가린다).");
            }
            else
            {
                Assert.Greater(near, anchor.x,
                    "왼쪽으로 걷는데 글자 블록이 기준점보다 왼쪽까지 걸쳐 있다(진행 방향을 가린다).");
            }

            // 대각선 **상단**: 블록 아랫변이 기준점보다 위다.
            Assert.Greater(center.y - size.y * 0.5f, anchor.y,
                "글자가 기준점(머리 위)보다 아래에 걸쳐 있다 — '대각선 상단'이 아니다.");
            }
        }

        // ────────────────────────────────────────────────────────────────────────
        // (B) 간격 — 캐릭터 키에 정확히 비례 (배율 1.0 / 0.75 / 0.5)
        // ────────────────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator TextGapStaysProportionalToCharacterHeightAtEveryScale()
        {
            // 1.0(비율 기준선) / 0.75(현재 출하 배율) / 0.5(직전 출하 배율).
            float[] scales = { 1f, 0.75f, 0.5f };
            foreach (float scale in scales)
            {
            DialogueBubbleRenderer renderer = SpawnRig(scale, facing: 1f, out StickmanStateMachine machine);
            yield return null;

            machine.Start(StickmanStateId.Attack);
            yield return null;

            Vector2 anchor = renderer.LastTextAnchorCanvas;
            Vector2 center = renderer.LastTextCenterCanvas;
            Vector2 size = renderer.LastTextSizeCanvas;

            // 클램프가 걸린 자리에서 재면 간격이 아니라 화면 여백을 재게 된다 — 그 경우 이 단언
            // 자체가 무의미하므로 먼저 "클램프에 닿지 않았다"를 확인한다.
            float screenW = CanvasWidth();
            float screenH = CanvasHeight();
            Assert.IsTrue(center.x - size.x * 0.5f > 9f && center.x + size.x * 0.5f < screenW - 9f
                          && center.y + size.y * 0.5f < screenH - 9f,
                "테스트 리그가 화면 클램프에 걸렸다 — 간격이 아니라 화면 여백을 재게 된다. " +
                $"(블록 x {center.x - size.x * 0.5f:F1}~{center.x + size.x * 0.5f:F1}, 화면 폭 {screenW:F1})");

            float charHeightCanvas = CanvasHeightOfWorldSpan(scale * BaseHeight);
            Assert.Greater(charHeightCanvas, 1f, "캐릭터 키가 화면에서 1유닛도 안 된다 — 카메라 설정 오류.");

            // 렌더러와 테스트가 **같은 카메라**로 투영했는지 먼저 확인한다. 다른 카메라를 썼다면
            // 아래 비율은 의미가 없다(위 SetUp의 실측 함정 참고) — 그때 나오는 오해하기 쉬운
            // "간격이 0.05배다" 대신 원인을 곧바로 가리키는 메시지를 낸다.
            Transform head = renderer.transform.Find("Head");
            float tipWorldY = head.position.y + scale * BaseHeight * 0.1498f;
            Vector3 tipScreen = _camera.WorldToScreenPoint(new Vector3(head.position.x, tipWorldY, 0f));
            float expectedAnchorY =
                StickMate.Platform.ScreenCoordinateConverter.UnityScreenToCanvas(tipScreen.y, null);
            Assert.AreEqual(expectedAnchorY, anchor.y, Mathf.Max(2f, charHeightCanvas * 0.03f),
                "렌더러가 이 테스트와 다른 카메라로 투영했다(Camera.main이 앞선 테스트가 남긴 씬 " +
                "카메라를 집었을 가능성) — 간격 비율 단언이 성립하지 않는다.");

            float gapX = Mathf.Abs(center.x - anchor.x) - size.x * 0.5f;
            float gapY = (center.y - size.y * 0.5f) - anchor.y;

            // ★ 절대 단언: 바깥에서 온 숫자(0.20 / 0.10)와 맞댄다. 절대 상수가 하나라도 남아 있으면
            //   배율 1.0에서만 맞고 0.75/0.5에서 즉시 깨진다.
            Assert.AreEqual(ExpectedGapXRatio, gapX / charHeightCanvas, 0.02f,
                $"배율 {scale:F2}에서 가로 간격이 캐릭터 키의 {gapX / charHeightCanvas:F3}배다 — " +
                $"{ExpectedGapXRatio}배여야 한다(오프셋이 절대 상수로 굳었다는 뜻).");
            Assert.AreEqual(ExpectedGapYRatio, gapY / charHeightCanvas, 0.02f,
                $"배율 {scale:F2}에서 세로 간격이 캐릭터 키의 {gapY / charHeightCanvas:F3}배다 — " +
                $"{ExpectedGapYRatio}배여야 한다.");
            }
        }

        // ────────────────────────────────────────────────────────────────────────
        // (C) 화면 클램프 / 뒤집기 — 순수 함수 직접 검사
        // ────────────────────────────────────────────────────────────────────────

        [Test]
        public void NearScreenEdge_TextFlipsToTheOtherSideInsteadOfBeingPushedOverTheHead()
        {
            const float screenW = 1000f, screenH = 800f, margin = 8f;
            var size = new Vector2(120f, 30f);
            const float gapX = 20f, gapY = 10f;

            // 캐릭터가 화면 왼쪽 끝에서 **왼쪽으로** 걷는 중 -> 선호 쪽은 오른쪽(+1)이라 문제없다.
            // 반대로 오른쪽 끝에서 왼쪽으로 걸으면 선호 쪽(+1)이 화면 밖이므로 -1로 뒤집혀야 한다.
            var tipRightEdge = new Vector2(screenW - 30f, 400f);
            DialogueBubbleRenderer.ComicTextPlacement p =
                DialogueBubbleRenderer.ComputeComicTextPlacement(
                    tipRightEdge, size, preferredSideSign: 1f, gapX, gapY, screenW, screenH, margin);

            Assert.AreEqual(-1f, p.SideSign, 1e-4f,
                "화면 오른쪽 끝에서 오른쪽에 글자를 놓으려다 잘리는 상황인데 반대쪽으로 뒤집히지 않았다.");
            Assert.IsTrue(p.FlippedByScreenEdge, "뒤집힘 플래그가 서지 않았다.");
            Assert.LessOrEqual(p.Center.x + size.x * 0.5f, screenW - margin + 1e-3f,
                "뒤집고도 글자가 화면 오른쪽으로 삐져나갔다.");
        }

        [Test]
        public void TextNeverLeavesTheScreenEvenAtTheCorners()
        {
            const float screenW = 900f, screenH = 700f, margin = 8f;
            var size = new Vector2(160f, 44f);
            const float gapX = 24f, gapY = 12f;

            float[] xs = { 0f, 5f, 40f, screenW * 0.5f, screenW - 40f, screenW - 5f, screenW };
            float[] ys = { 0f, 5f, screenH * 0.5f, screenH - 20f, screenH, screenH + 50f };
            float[] sides = { -1f, 1f };

            foreach (float x in xs)
            {
                foreach (float y in ys)
                {
                    foreach (float side in sides)
                    {
                        DialogueBubbleRenderer.ComicTextPlacement p =
                            DialogueBubbleRenderer.ComputeComicTextPlacement(
                                new Vector2(x, y), size, side, gapX, gapY, screenW, screenH, margin);

                        Assert.GreaterOrEqual(p.Center.x - size.x * 0.5f, margin - 1e-3f,
                            $"(tip {x:F0},{y:F0} side {side:F0}) 글자가 화면 왼쪽으로 잘렸다.");
                        Assert.LessOrEqual(p.Center.x + size.x * 0.5f, screenW - margin + 1e-3f,
                            $"(tip {x:F0},{y:F0} side {side:F0}) 글자가 화면 오른쪽으로 잘렸다.");
                        Assert.GreaterOrEqual(p.Center.y - size.y * 0.5f, margin - 1e-3f,
                            $"(tip {x:F0},{y:F0} side {side:F0}) 글자가 화면 아래로 잘렸다.");
                        Assert.LessOrEqual(p.Center.y + size.y * 0.5f, screenH - margin + 1e-3f,
                            $"(tip {x:F0},{y:F0} side {side:F0}) 글자가 창 상단 테두리 밖으로 잘렸다.");
                    }
                }
            }
        }

        // ────────────────────────────────────────────────────────────────────────
        // 네거티브 컨트롤 — 방향 반전을 되돌리면 실제로 깨지는가
        // ────────────────────────────────────────────────────────────────────────

        [Test]
        public void NegativeControl_SameSidePlacementBreaksTheOppositeDirectionAssertion()
        {
            const float screenW = 1600f, screenH = 900f, margin = 8f;
            var size = new Vector2(120f, 32f);
            const float gapX = 30f, gapY = 15f;
            var tip = new Vector2(screenW * 0.5f, screenH * 0.5f); // 화면 한가운데 = 클램프 무관.

            const float facing = 1f; // 오른쪽으로 걷는 중.

            // 올바른 배치: 진행 방향의 **반대**(-facing).
            DialogueBubbleRenderer.ComicTextPlacement correct =
                DialogueBubbleRenderer.ComputeComicTextPlacement(
                    tip, size, -facing, gapX, gapY, screenW, screenH, margin);
            Assert.Less(correct.Center.x + size.x * 0.5f, tip.x,
                "정상 경로부터 이미 틀렸다 — 오른쪽으로 걸을 때 글자 블록은 통째로 기준점 왼쪽이어야 한다.");

            // 버그를 되살린 배치: 진행 방향과 **같은 쪽**(+facing). 위 단언이 반드시 깨져야 한다.
            DialogueBubbleRenderer.ComicTextPlacement reversed =
                DialogueBubbleRenderer.ComputeComicTextPlacement(
                    tip, size, facing, gapX, gapY, screenW, screenH, margin);
            Assert.IsFalse(reversed.Center.x + size.x * 0.5f < tip.x,
                "방향 반전을 되돌렸는데도 (A)의 조건이 통과했다 — 이 테스트는 아무것도 보고 있지 않다.");
            Assert.Greater(reversed.Center.x, correct.Center.x,
                "같은 쪽 배치와 반대쪽 배치가 같은 자리로 계산됐다 — 부호가 배치에 쓰이지 않는다.");
        }

        // ────────────────────────────────────────────────────────────────────────
        // (D) "텍스트만" — 말풍선 도형이 하나도 그려지지 않는다
        // ────────────────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator NoBubbleShapeIsDrawn_TextOnly()
        {
            DialogueBubbleRenderer renderer = SpawnRig(0.75f, facing: 1f, out StickmanStateMachine machine);
            yield return null;
            machine.Start(StickmanStateId.Attack);
            yield return null;

            Canvas canvas = FindRendererCanvas(renderer);
            Assert.IsNotNull(canvas, "말풍선 캔버스를 찾지 못했다.");

            var images = canvas.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Assert.IsFalse(images[i].enabled,
                    $"'{images[i].name}' Image가 켜져 있다 — 사용자 요구는 '말풍선 말고 텍스트만'이다. " +
                    "(말풍선을 되살리려면 DialogueBubbleRenderer.DrawBubbleShapes를 true로 되돌리고 " +
                    "이 테스트를 그 계약에 맞춰 갱신할 것 — 지우지 말 것.)");
            }

            var texts = canvas.GetComponentsInChildren<Text>(true);
            Assert.AreEqual(1, texts.Length, "글자는 정확히 하나여야 한다.");
            Assert.AreEqual(TalkText, texts[0].text, "표시된 글자가 대사와 다르다.");

            // 만화 레터링의 핵심 + 가독성 대책: 잉크색 글자 + **반대색** 외곽선.
            var outline = texts[0].GetComponent<Outline>();
            Assert.IsNotNull(outline,
                "글자 외곽선이 없다 — 배경이 사라진 뒤 글자와 바탕화면을 가르는 유일한 수단이다 " +
                "(검은 글자 + 어두운 바탕화면에서 글자가 그대로 사라진다).");
            Assert.Greater(outline.effectDistance.x, 0.3f, "외곽선 두께가 0에 가깝다.");
            // ★ 외곽선은 **글자 크기에 비례**해야 한다(고정 두께면 작은 글자를 잡아먹는다).
            //   글자 크기의 20%를 넘으면 이웃 글자의 후광이 붙어 한글 자모 사이가 메워진다.
            Assert.Less(outline.effectDistance.x, texts[0].fontSize * 0.2f,
                $"외곽선({outline.effectDistance.x:F2})이 글자 크기({texts[0].fontSize})에 비해 너무 두껍다 — " +
                "한글은 자모 사이가 메워져 읽을 수 없게 된다.");

            // 기본 프리셋은 검은 잉크 -> 외곽선은 흰색이어야 한다(반대색).
            float inkLuma = texts[0].color.grayscale;
            float outlineLuma = outline.effectColor.grayscale;
            Assert.Greater(Mathf.Abs(inkLuma - outlineLuma), 0.5f,
                $"글자({inkLuma:F2})와 외곽선({outlineLuma:F2})의 명도가 비슷하다 — 외곽선이 대비를 " +
                "만들지 못하면 밝은/어두운 바탕화면 어느 한쪽에서 글자가 사라진다.");
            Assert.AreEqual(1f, outline.effectColor.a, 1e-3f,
                "외곽선 알파가 1이 아니다 — 이 선은 글자와 바탕화면 사이의 유일한 분리막이다.");
        }

        /// <summary>
        /// ★ 잉크색 프리셋 양쪽에서 외곽선이 **반대색으로 뒤집히는지** — 배경이 사라진 뒤 글자가
        /// 바탕화면에 묻히지 않게 하는 유일한 장치다(리더 지시 "외곽선이 두 프리셋에서 반대로
        /// 뒤집혀야 정상이다").
        ///
        /// 실패 모드가 둘 다 실재한다:
        ///   · 검은 잉크 + 어두운 바탕화면 -> 흰 외곽선이 없으면 글자가 사라진다.
        ///   · 흰 잉크 + 밝은 바탕화면   -> 검은 외곽선이 없으면 글자가 사라진다.
        /// 한쪽만 확인하면 반대쪽이 조용히 깨진 채로 출하된다(이 프로젝트가 직전 라운드에 겪은 유형).
        /// </summary>
        [UnityTest]
        public IEnumerator TextOutlineIsAlwaysTheOppositeOfTheInkPreset()
        {
            DialogueBubbleRenderer renderer = SpawnRig(0.75f, facing: 1f, out StickmanStateMachine machine);
            yield return null;
            machine.Start(StickmanStateId.Attack);
            yield return null;

            Canvas canvas = FindRendererCanvas(renderer);
            Assert.IsNotNull(canvas);
            Text label = canvas.GetComponentInChildren<Text>(true);
            Outline outline = label != null ? label.GetComponent<Outline>() : null;
            Assert.IsNotNull(outline, "글자 외곽선이 없다.");

            // 프리팹/에셋을 건드리지 않도록 테스트 전용 StickConfig를 만들어 주입한다.
            // (_config는 [SerializeField] private이라 리플렉션이 유일한 주입 경로다 —
            //  프로덕션 코드에 테스트 전용 setter를 뚫지 않기 위한 선택이다.)
            var config = ScriptableObject.CreateInstance<StickConfig>();
            System.Reflection.FieldInfo field = typeof(DialogueBubbleRenderer)
                .GetField("_config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "_config 필드를 찾지 못했다 — 필드명이 바뀌었다면 이 테스트도 갱신할 것.");

            try
            {
                config.inkColor = StickmanInkColor.Black;
                field.SetValue(renderer, config);
                renderer.RefreshColors();
                Assert.Less(label.color.grayscale, 0.5f, "검정 프리셋인데 글자가 밝다.");
                Assert.Greater(outline.effectColor.grayscale, 0.5f,
                    "검정 잉크인데 외곽선이 밝지 않다 — 어두운 바탕화면 위에서 글자가 그대로 사라진다.");
                Assert.AreEqual(1f, outline.effectColor.a, 1e-3f, "외곽선 알파가 1이 아니다.");

                config.inkColor = StickmanInkColor.White;
                renderer.RefreshColors();
                Assert.Greater(label.color.grayscale, 0.5f, "흰색 프리셋인데 글자가 어둡다.");
                Assert.Less(outline.effectColor.grayscale, 0.5f,
                    "흰 잉크인데 외곽선이 어둡지 않다 — 밝은 바탕화면 위에서 글자가 그대로 사라진다.");
                Assert.AreEqual(1f, outline.effectColor.a, 1e-3f, "외곽선 알파가 1이 아니다.");
            }
            finally
            {
                field.SetValue(renderer, null);
                Object.DestroyImmediate(config);
            }
        }

        // ────────────────────────────────────────────────────────────────────────
        // (E) 기울기 — 배치만 대각선이 아니라 **글자 자체가** 비스듬하다
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 사용자 요구 2026-08-31 "캐릭터가 말하는 텍스트는 좀 대각선으로 작성해줘".
        ///
        /// 세 가지를 한꺼번에 못박는다:
        ///   · <b>크기</b> — 실제 RectTransform 회전각의 크기가 <see cref="MinVisibleTiltDegrees"/> 이상.
        ///     (직전 구현의 상한 2.5도로 되돌아가면 여기서 즉시 깨진다 = 이 테스트의 존재 이유.)
        ///   · <b>부호</b> — 놓인 쪽의 거울상. 왼쪽 위면 반시계(+), 오른쪽 위면 시계(-).
        ///     좌우 두 경우의 크기가 정확히 같아야 한다(거울상이지 서로 다른 각도가 아니다).
        ///   · <b>적용</b> — 렌더러가 보고하는 값(LastTextTiltDegrees)과 화면에 실제로 걸린
        ///     Transform 회전이 일치한다. "계산은 했는데 적용을 안 한" 실패를 따로 잡는다.
        /// </summary>
        [UnityTest]
        public IEnumerator TextItselfIsTilted_AndTheTiltMirrorsTheSideItSitsOn()
        {
            var magnitudeBySide = new Dictionary<float, float>(2);
            float[] facings = { 1f, -1f };
            foreach (float facing in facings)
            {
            DialogueBubbleRenderer renderer = SpawnRig(0.75f, facing, out StickmanStateMachine machine);
            InjectTiltConfig(renderer, TiltBaseDegrees, TiltTestFontSize);
            yield return null;

            machine.Start(StickmanStateId.Attack);
            Assert.IsTrue(renderer.IsBubbleVisible, "대사가 표시되지 않았다 — 기울기를 잴 수 없다.");
            yield return null;

            RectTransform panel = FindTextPanel(renderer);
            Assert.IsNotNull(panel, "글자 블록(BubblePanel)을 찾지 못했다.");

            float z = SignedZDegrees(panel);
            float side = renderer.LastTextSideSign;

            // (E-1) 계산값과 실제 적용값이 같은가.
            Assert.AreEqual(renderer.LastTextTiltDegrees, z, 1e-2f,
                $"렌더러는 기울기 {renderer.LastTextTiltDegrees:F2}도라고 보고하는데 글자 블록의 실제 " +
                $"회전은 {z:F2}도다 — 각도를 계산해 놓고 Transform에 적용하지 않았다.");

            // (E-2) 눈에 보이는 크기인가. ★ 이 단언이 "회전은 0이 아니지만 아무도 못 알아보는"
            //       종전 상태(±2.5도)를 잡는다.
            Assert.GreaterOrEqual(Mathf.Abs(z), MinVisibleTiltDegrees,
                $"글자 기울기가 {Mathf.Abs(z):F2}도뿐이다 — 사용자 요구는 '좀 대각선으로'인데 이 정도는 " +
                "화면에서 수평과 구분되지 않는다(직전 구현의 상한이 정확히 2.5도였다).");
            Assert.LessOrEqual(Mathf.Abs(z), TiltBaseDegrees * (1f + TiltJitterRatio) + 0.01f,
                $"글자 기울기가 {Mathf.Abs(z):F2}도로 설정 기준값({TiltBaseDegrees}도)의 편차 범위를 넘었다 — " +
                "각도가 설정을 따르지 않는다.");
            Assert.GreaterOrEqual(Mathf.Abs(z), TiltBaseDegrees * (1f - TiltJitterRatio) - 0.01f,
                $"글자 기울기가 {Mathf.Abs(z):F2}도로 설정 기준값({TiltBaseDegrees}도)의 편차 범위보다 작다.");

            // (E-3) 부호 = 놓인 쪽의 거울상.
            if (side < 0f)
            {
                Assert.Greater(z, 0f,
                    "글자가 캐릭터 **왼쪽 위**에 놓였는데 시계 방향으로 기울었다 — 반시계(오른쪽 끝이 " +
                    "올라감)여야 머리에 가장 가까운 아래쪽 안쪽 모서리가 떠오르고, 좌우 배치가 서로 " +
                    "거울상이 된다.");
            }
            else
            {
                Assert.Less(z, 0f,
                    "글자가 캐릭터 **오른쪽 위**에 놓였는데 반시계로 기울었다 — 시계 방향이어야 한다.");
            }

            // (E-4) 클램프 크기가 **회전한 축 정렬 경계**인가. 회전 전 크기로 클램프하면 화면
            //       위/옆에서 글자 모서리만 잘려 나간다.
            Vector2 raw = panel.sizeDelta;
            float rad = Mathf.Abs(z) * Mathf.Deg2Rad;
            var expected = new Vector2(
                raw.x * Mathf.Cos(rad) + raw.y * Mathf.Sin(rad),
                raw.x * Mathf.Sin(rad) + raw.y * Mathf.Cos(rad));
            Assert.AreEqual(expected.x, renderer.LastTextSizeCanvas.x, 0.05f,
                "클램프에 쓰인 가로 크기가 회전 경계와 다르다 — 회전 전 사각형으로 클램프하고 있다.");
            Assert.AreEqual(expected.y, renderer.LastTextSizeCanvas.y, 0.05f,
                "클램프에 쓰인 세로 크기가 회전 경계와 다르다 — 회전 전 사각형으로 클램프하고 있다.");
            Assert.Greater(renderer.LastTextSizeCanvas.y, raw.y,
                "기울었는데 클램프 세로 크기가 회전 전과 같다(회전이 클램프에 반영되지 않았다).");

            magnitudeBySide[side] = Mathf.Abs(z);
            }

            // (E-5) 좌우가 정확한 거울상인가 — 같은 대사면 크기는 같고 부호만 반대여야 한다.
            Assert.AreEqual(2, magnitudeBySide.Count,
                "좌우 두 배치가 같은 쪽으로 계산됐다 — 거울상 여부를 확인할 수 없다.");
            Assert.AreEqual(magnitudeBySide[-1f], magnitudeBySide[1f], 1e-2f,
                $"왼쪽 배치({magnitudeBySide[-1f]:F2}도)와 오른쪽 배치({magnitudeBySide[1f]:F2}도)의 " +
                "기울기 크기가 다르다 — 부호만 뒤집는 거울상이어야 한다.");
        }

        /// <summary>
        /// 기울기가 <b>설정을 따르고</b>, 글리프가 너무 작을 때는 스스로 꺼지는가.
        ///
        /// 두 번째 절반이 중요하다: 기울이면 글자 쿼드가 픽셀 격자와 어긋나 글리프 아틀라스가
        /// 바이리니어로 다시 샘플링된다. 12px 한글에서는 자모 획이 통째로 뭉개져 읽을 수 없게 되므로
        /// (2026-08-29 실측), 그 크기대에서는 "대각선으로"보다 "읽힌다"가 먼저다.
        ///
        /// 동시에 이 테스트는 (E)의 **네거티브 컨트롤**이기도 하다 — 각도가 설정에 따라 0까지
        /// 내려가는 것을 같은 측정 방법으로 보이므로, (E)가 통과한 이유가 "어딘가 고정된 상수를
        /// 읽어서"가 아님이 증명된다.
        /// </summary>
        [UnityTest]
        public IEnumerator TiltFollowsTheConfig_AndTurnsItselfOffForGlyphsTooSmallToRotate()
        {
            DialogueBubbleRenderer renderer = SpawnRig(0.75f, facing: 1f, out StickmanStateMachine machine);
            StickConfig config = InjectTiltConfig(renderer, TiltBaseDegrees, TiltTestFontSize);
            yield return null;

            machine.Start(StickmanStateId.Attack);
            yield return null;
            Assert.GreaterOrEqual(Mathf.Abs(SignedZDegrees(FindTextPanel(renderer))), MinVisibleTiltDegrees,
                "기준 조건(기울기 8도 + 충분히 큰 글자)에서부터 기울지 않았다 — 아래 비교가 무의미해진다.");

            // (1) 설정으로 끄면 정확히 수평이 된다.
            config.dialogueTiltDegrees = 0f;
            yield return RespeakAsync(machine);
            Assert.AreEqual(0f, SignedZDegrees(FindTextPanel(renderer)), 1e-3f,
                "dialogueTiltDegrees = 0인데 글자가 여전히 기울어 있다 — 각도가 설정을 읽지 않는다.");

            // (2) 글리프가 회전 하한(물리 14px)보다 작으면 설정과 무관하게 꺼진다.
            //     32 -> 8pt: 8 x 0.75(배율) x 0.875(만화 배율) = 5.25 -> 만화 모드 폰트 하한 9pt로 받쳐지고,
            //     dpi 오버라이드 1이라 물리 9px < 14px이므로 기울기가 꺼져야 한다.
            config.dialogueTiltDegrees = TiltBaseDegrees;
            config.dialogueFontSize = 8;
            yield return RespeakAsync(machine);
            Assert.AreEqual(0f, SignedZDegrees(FindTextPanel(renderer)), 1e-3f,
                "물리 9px짜리 글리프인데도 기울였다 — 회전 리샘플링으로 한글 자모 획이 뭉개진다 " +
                "(2026-08-29 실측). 이 크기대에서는 '대각선으로'보다 '읽힌다'가 먼저다.");
            Assert.AreEqual(0f, renderer.LastTextTiltDegrees, 1e-3f,
                "렌더러가 보고하는 기울기와 실제 적용값이 어긋난다.");
        }

        /// <summary>
        /// ★ <b>출하 조합에서 정말 기울어지는가</b> — 이 테스트가 없으면 (E)는 "테스트용으로 크게 키운
        /// 글자에서만 기울어진다"를 통과시킬 수 있다.
        ///
        /// 회전 하한이 **물리 픽셀** 단위라, 판정은 글자 크기와 화면 배율의 <b>곱</b>에 달려 있다.
        /// 나머지 PlayMode 리그는 DPI가 1이라 실제 사용자 화면(Retina 2x)과 조건이 다르다 —
        /// 그쪽에서만 확인하면 "테스트는 통과하는데 화면은 그대로 수평"이 성립해 버린다.
        /// 그래서 여기서는 출하 조합을 그대로 재현한다:
        ///   dialogueFontSize 16(배포 에셋) x characterScale 0.75(출하) x 0.875(만화 배율) = 10pt,
        ///   x Retina 캔버스 배율 2.0 = 물리 20px >= 하한 14px  ->  <b>켜져야 한다</b>.
        /// (desktopDpiScale = 0.5 = "OS 포인트 / Unity 픽셀"이 곧 캔버스 배율 2.0의 역수다.)
        /// </summary>
        [UnityTest]
        public IEnumerator ShippingConfigurationOnARetinaScreen_ActuallyTiltsTheText()
        {
            DialogueBubbleRenderer renderer = SpawnRig(0.75f, facing: 1f, out StickmanStateMachine machine);
            StickConfig config = InjectTiltConfig(renderer, TiltBaseDegrees, fontSize: 16);
            config.desktopDpiScale = 0.5f; // Retina 2x — 캔버스 1유닛 = 물리 2픽셀.
            yield return null;

            machine.Start(StickmanStateId.Attack);
            yield return null;

            float z = SignedZDegrees(FindTextPanel(renderer));
            Assert.GreaterOrEqual(Mathf.Abs(z), MinVisibleTiltDegrees,
                $"출하 그대로의 설정(16pt x 0.75 x 0.875 = 10pt, Retina 2x = 물리 20px)에서 기울기가 " +
                $"{Mathf.Abs(z):F2}도뿐이다 — 사용자 화면에서 글자가 수평으로 보인다는 뜻이다. " +
                "회전 하한(ComicTiltMinGlyphPixels)이 출하 조합을 잘라내고 있는지 확인할 것.");
            Assert.Greater(z, 0f, "오른쪽으로 걷는 중이라 글자는 왼쪽 위 = 반시계여야 한다.");
        }

        // ────────────────────────────────────────────────────────────────────────
        // 리그 조립 / 좌표 헬퍼
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// ★ 리그 이름에 일련번호를 붙이는 이유 (2026-08-31 실측으로 잡은 **플래키**):
        /// <see cref="FindRendererCanvas"/>는 캔버스를 <b>이름</b>("DialogueBubbleCanvas (리그이름)")으로
        /// 찾는다 — 렌더러가 만드는 캔버스가 씬 루트에 있고 렌더러 쪽 참조가 private이라 그것이
        /// 유일한 경로다. 그런데 한 테스트가 <b>같은 배율의 리그를 둘</b> 만들면(좌/우 진행 방향을
        /// 나란히 보는 경우) 이름이 겹치고, 앞 리그는 TearDown 전까지 살아 있으므로
        /// <c>FindObjectsByType</c>의 순서에 따라 <b>남의 캔버스</b>가 잡힌다.
        /// 실제로 같은 테스트가 한 번은 통과하고 한 번은 "계산값과 실제 회전이 반대"로 실패했다
        /// (두 번째 리그를 재면서 첫 번째 리그의 패널을 읽었다). 이름을 갈라 원인을 없앤다.
        /// </summary>
        private DialogueBubbleRenderer SpawnRig(float scale, float facing, out StickmanStateMachine machine)
        {
            GameObject root = BuildMetricsRig($"ComicRig_{scale:F2}#{++_rigSerial}", scale);
            _spawned.Add(root);

            var renderer = root.AddComponent<DialogueBubbleRenderer>();
            renderer.FacingSource = () => facing;

            machine = new StickmanStateMachine(new Dictionary<StickmanStateId, IStickmanState>
            {
                { StickmanStateId.Attack, new TalkingState(StickmanStateId.Attack, TalkText) },
                { StickmanStateId.Ragdoll, new SilentState(StickmanStateId.Ragdoll) },
            });
            _machines.Add(machine);
            renderer.Bind(machine, root.transform.Find("Head"));
            return renderer;
        }

        /// <summary>
        /// StickmanMetrics가 실측하는 소스만 갖춘 최소 캐릭터 리그(PlayMode/RendererScaleRatioTests.cs와
        /// 같은 조립법). 컴포넌트 부착 순서가 중요하다 — StickmanMetrics.Awake()가 즉시 계층을 재므로
        /// 지오메트리를 <b>먼저</b> 다 만든 뒤에 붙인다.
        /// </summary>
        private static GameObject BuildMetricsRig(string name, float scale)
        {
            var root = new GameObject(name);
            root.transform.position = Vector3.zero;

            float height = BaseHeight * scale;

            var capsule = root.AddComponent<CapsuleCollider2D>();
            capsule.size = new Vector2(0.4f * scale, height);
            capsule.offset = new Vector2(0f, height * 0.5f);

            var head = new GameObject("Head");
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, height - BaseHeadRadius * scale, 0f);
            var outline = new GameObject("HeadOutline");
            outline.transform.SetParent(head.transform, false);
            var lr = outline.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 1;
            lr.SetPosition(0, new Vector3(BaseHeadRadius * scale, 0f, 0f));

            var arm = new GameObject("LeftArm");
            arm.transform.SetParent(root.transform, false);
            arm.transform.localPosition = new Vector3(0f, BaseShoulderY * scale, 0f);

            var leg = new GameObject("LeftLeg");
            leg.transform.SetParent(root.transform, false);
            leg.transform.localPosition = new Vector3(0f, BaseHipY * scale, 0f);

            root.AddComponent<StickmanMetrics>();
            return root;
        }

        /// <summary>기울기 계약 검사용 StickConfig를 만들어 렌더러에 주입한다.
        /// (_config는 [SerializeField] private이라 리플렉션이 유일한 주입 경로다 — 프로덕션 코드에
        ///  테스트 전용 setter를 뚫지 않기 위한 선택이고, 같은 파일의 외곽선 테스트와 같은 방식이다.)
        /// desktopDpiScale을 1로 못박는 이유: 회전 하한이 **물리 픽셀** 단위라 앞선 테스트가 남긴
        /// 전역 DPI 실측값에 따라 결과가 달라지면 안 된다.</summary>
        private StickConfig InjectTiltConfig(DialogueBubbleRenderer renderer, float tiltDegrees, int fontSize)
        {
            var config = ScriptableObject.CreateInstance<StickConfig>();
            config.dialogueTiltDegrees = tiltDegrees;
            config.dialogueFontSize = fontSize;
            config.desktopDpiScale = 1f;
            _configs.Add(config);

            System.Reflection.FieldInfo field = typeof(DialogueBubbleRenderer)
                .GetField("_config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "_config 필드를 찾지 못했다 — 필드명이 바뀌었다면 이 테스트도 갱신할 것.");
            field.SetValue(renderer, config);
            return config;
        }

        /// <summary>같은 대사를 한 번 더 시키고(상태를 빠져나갔다 다시 들어간다) 배치가 갱신될 때까지
        /// 기다린다. 기울기 크기는 대사가 뜨는 순간 한 번 확정되므로, 설정을 바꾼 뒤에는 반드시
        /// 새 대사를 띄워야 새 값이 반영된다.</summary>
        private static IEnumerator RespeakAsync(StickmanStateMachine machine)
        {
            machine.ChangeState(StickmanStateId.Ragdoll);
            yield return null;
            machine.ChangeState(StickmanStateId.Attack);
            yield return null;
        }

        /// <summary>글자 블록(회전/팝인 스케일을 먹는 컨테이너) RectTransform.</summary>
        private RectTransform FindTextPanel(DialogueBubbleRenderer renderer)
        {
            Canvas canvas = FindRendererCanvas(renderer);
            if (canvas == null) return null;
            Text label = canvas.GetComponentInChildren<Text>(true);
            return label != null ? label.transform.parent as RectTransform : null;
        }

        /// <summary>Z축 회전을 -180~+180 범위의 **부호 있는** 각도로 읽는다
        /// (eulerAngles는 0~360이라 -8도가 352도로 나와 부호 단언이 통째로 무의미해진다).</summary>
        private static float SignedZDegrees(RectTransform rect)
        {
            Assert.IsNotNull(rect, "글자 블록 RectTransform이 없다.");
            float z = rect.localEulerAngles.z;
            return z > 180f ? z - 360f : z;
        }

        private Canvas FindRendererCanvas(DialogueBubbleRenderer renderer)
        {
            string expected = "DialogueBubbleCanvas (" + renderer.gameObject.name + ")";
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null && canvases[i].name == expected) return canvases[i];
            }
            return null;
        }

        private float CanvasWidth() =>
            StickMate.Platform.ScreenCoordinateConverter.UnityScreenToCanvas(Screen.width, null);

        private float CanvasHeight() =>
            StickMate.Platform.ScreenCoordinateConverter.UnityScreenToCanvas(Screen.height, null);

        /// <summary>월드 세로 길이를 렌더러와 **같은 경로**로 캔버스 유닛으로 환산한다
        /// (WorldToScreenPoint -> UnityScreenToCanvas). 카메라 배율/해상도가 그대로 흡수된다.</summary>
        private float CanvasHeightOfWorldSpan(float worldSpan)
        {
            Vector3 a = _camera.WorldToScreenPoint(Vector3.zero);
            Vector3 b = _camera.WorldToScreenPoint(new Vector3(0f, worldSpan, 0f));
            return Mathf.Abs(
                StickMate.Platform.ScreenCoordinateConverter.UnityScreenToCanvas(b.y, null) -
                StickMate.Platform.ScreenCoordinateConverter.UnityScreenToCanvas(a.y, null));
        }
    }
}
