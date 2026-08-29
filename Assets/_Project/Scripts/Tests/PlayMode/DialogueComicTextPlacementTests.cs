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
        // 리그 조립 / 좌표 헬퍼
        // ────────────────────────────────────────────────────────────────────────

        private DialogueBubbleRenderer SpawnRig(float scale, float facing, out StickmanStateMachine machine)
        {
            GameObject root = BuildMetricsRig($"ComicRig_{scale:F2}", scale);
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
