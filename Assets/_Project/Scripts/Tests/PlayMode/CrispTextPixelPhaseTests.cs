using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Interaction;
using StickMate.Platform;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ <b>글자 메시가 실제로 픽셀 격자에 얹히는가</b> — 씬에서 정점을 직접 읽어 확인한다.
    /// 사용자 신고 "전체적으로 글자가 흐리고 일부는 번져 보임"(2026-09-07).
    ///
    /// ============================================================================
    /// 이 파일이 스스로에게 거는 제약
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><b>기대값을 프로덕션 함수로 만들지 않는다.</b> "정수인가"는
    ///     <c>Mathf.Abs(v - Mathf.Round(v)) &lt;= 허용오차</c>로 <b>테스트가 직접</b> 판정한다.
    ///     <see cref="GlyphPixelSnapPolicy.IsPixelAligned"/>를 쓰면 그 함수가 "언제나 true"로
    ///     망가져도 초록이다(TEAM.md «생성기와 검사기가 같이 틀린다»).</item>
    ///   <item><b>음성 대조를 반드시 붙인다.</b> 같은 배치에서 <see cref="CrispText.SnapEnabled"/>를
    ///     끄면 정점이 <b>정수에서 벗어나야</b> 한다. 이 대조가 없으면 «어차피 정수였다»와
    ///     «스냅이 동작했다»가 <b>똑같이 생긴다</b> — 이 저장소가 아홉 번 당한 형태다.</item>
    ///   <item><b>정점은 캔버스 갱신 경로에서 가로챈다.</b> <see cref="CanvasRenderer"/>에서
    ///     정점을 되읽을 공개 API가 없으므로, <see cref="IMeshModifier"/>를 하나 붙여
    ///     <c>OnPopulateMesh</c> <b>직후</b>의 <see cref="VertexHelper"/>를 그대로 본다
    ///     (<c>Graphic.DoMeshGeneration</c>이 그 순서로 부른다).</item>
    /// </list>
    /// </summary>
    public sealed class CrispTextPixelPhaseTests
    {
        private const string LogPrefix = "[글리프위상-PLAY]";

        /// <summary>"정수로 본다"의 허용 오차(스크린 픽셀). <see cref="GlyphPixelSnapPolicy"/>의
        /// 상수를 <b>참조하지 않고</b> 테스트가 직접 정한다 — 위 "기대값을 프로덕션 함수로 만들지
        /// 않는다" 참고. 값은 float 정밀도(월드 좌표 ~4000에서 약 4e-4)보다 한 자리 크게 잡았다.</summary>
        private const float IntegerTolerance = 0.005f;

        /// <summary>일부러 격자에서 벗어나게 만드는 오프셋. 반 픽셀이 <b>최악</b>이다
        /// (획이 두 픽셀에 반반 나뉜다).</summary>
        private const float WorstCasePhase = 0.5f;

        /// <summary>재스냅 관측 창(초). <b>벽시계</b>다 — 이 저장소의 배치모드 PlayMode는
        /// 수천 fps로 돌아서 «N프레임»은 예산이 되지 못한다(CLAUDE.md).</summary>
        private const float ResnapWindowSeconds = 0.5f;

        /// <summary>양성 대조 표면을 <b>매 프레임</b> 미는 양(스크린 픽셀).
        /// <para>0.37을 고른 이유: 아래 <see cref="MoverCycleFrames"/>주기의 어떤 배수에서도 잔차가
        /// <see cref="IntegerTolerance"/> 안으로 들어오지 않는다
        /// (0.37 / 0.74 / 0.11 / 0.48 / 0.85 / 0.22 / 0.59). 0.5나 0.25처럼 «떨어지는» 값을 쓰면
        /// 주기적으로 격자에 붙어 대조군이 <b>조용히 쉬는 프레임</b>이 생긴다.</para></summary>
        private const float MoverStepPixels = 0.37f;

        /// <summary>양성 대조 표면이 원위치로 되감기는 주기(프레임). 좌표가 끝없이 커지면
        /// float 정밀도가 흔들려 «대조군이 안 움직였다»와 «정밀도가 무너졌다»가 섞인다.</summary>
        private const int MoverCycleFrames = 7;

        private GameObject _root;
        private bool _savedSnapEnabled;

        [SetUp]
        public void SetUp()
        {
            _savedSnapEnabled = CrispText.SnapEnabled;
            CrispText.SnapEnabled = true;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            CrispText.SnapEnabled = _savedSnapEnabled;
            if (_root != null) Object.DestroyImmediate(_root);
            _root = null;
            yield return null;
        }

        // ============================================================================
        // 하네스
        // ============================================================================

        /// <summary><c>OnPopulateMesh</c> 직후의 정점을 그대로 붙잡는 스파이.
        /// <para><c>Graphic.DoMeshGeneration</c>은 <c>OnPopulateMesh(vh)</c>를 부른 뒤
        /// <b>같은 <see cref="VertexHelper"/></b>를 모든 <see cref="IMeshModifier"/>에 넘긴다.
        /// 그래서 여기서 읽는 값이 곧 «화면에 올라가는 로컬 좌표»다.</para></summary>
        private sealed class VertexSpy : MonoBehaviour, IMeshModifier
        {
            public bool Captured;
            public Vector3 FirstVertexLocal;
            public int VertexCount;

            public void ModifyMesh(Mesh mesh) { }

            public void ModifyMesh(VertexHelper verts)
            {
                VertexCount = verts.currentVertCount;
                if (VertexCount <= 0) { Captured = false; return; }
                var v = new UIVertex();
                verts.PopulateUIVertex(ref v, 0);
                FirstVertexLocal = v.position;
                Captured = true;
            }
        }

        /// <summary>캔버스 → 소수 픽셀 자리로 밀어 둔 홀더 → 글자. 홀더에 <b>일부러</b> 반 픽셀을
        /// 걸어 두는 것이 이 하네스의 요점이다.</summary>
        private (Text text, VertexSpy spy, RectTransform holder) BuildSurface(float canvasScaleFactor,
            float extraPhasePixels)
        {
            _root = new GameObject("PhaseTestCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = canvasScaleFactor;

            return AddSurface("Holder", canvasScaleFactor, extraPhasePixels);
        }

        /// <summary><see cref="BuildSurface"/>가 만든 <b>같은 캔버스</b>에 표면을 하나 더 얹는다.
        /// <para>왜 필요한가: 「정지한 글자는 재스냅되지 않는다」를 단언하려면 <b>같은 관측 창 안에</b>
        /// «움직이면 실제로 재스냅된다»를 보여 주는 대조군이 있어야 한다. 대조군을 다른 테스트로
        /// 떼어 놓으면 <b>그 창에서 계기가 살아 있었는지</b>를 증명하지 못한다 — 그것이 이 저장소가
        /// 반복해 당한 «죽은 프로브가 산 프로브와 똑같이 생겼다»의 형태다.</para></summary>
        private (Text text, VertexSpy spy, RectTransform holder) AddSurface(string holderName,
            float canvasScaleFactor, float extraPhasePixels)
        {
            var holderGo = new GameObject(holderName, typeof(RectTransform));
            holderGo.transform.SetParent(_root.transform, false);
            var holder = holderGo.GetComponent<RectTransform>();
            holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0.5f);
            holder.pivot = new Vector2(0.5f, 0.5f);
            holder.sizeDelta = new Vector2(240f, 60f);

            Text text = UiChrome.AddText(holder, "Label", UiChrome.FontBody, TextAnchor.MiddleLeft,
                UiChrome.TextPrimary);
            var rt = (RectTransform)text.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(200f, 40f);
            rt.anchoredPosition = Vector2.zero;
            // 라틴만 쓴다 — 한글은 OS 폰트 폴백을 타므로 배치모드에서 흔들릴 수 있다.
            text.text = "Hgy 019";

            var spy = text.gameObject.AddComponent<VertexSpy>();

            // ★ 홀더를 «스크린 픽셀» 단위로 민다. 캔버스 유닛으로 밀면 배율만큼 곱해져
            //   의도한 위상이 안 나온다(1.5배에서 0.5 유닛 = 0.75px).
            holder.anchoredPosition = new Vector2(extraPhasePixels / canvasScaleFactor, 0f);
            return (text, spy, holder);
        }

        private static void Pump(Graphic g)
        {
            g.SetAllDirty();
            Canvas.ForceUpdateCanvases();
        }

        private static Vector3 WorldOfFirstVertex(Text text, VertexSpy spy)
            => text.rectTransform.localToWorldMatrix.MultiplyPoint3x4(spy.FirstVertexLocal);

        private static bool NearInteger(float v) => Mathf.Abs(v - Mathf.Round(v)) <= IntegerTolerance;

        // ============================================================================
        // (0) 전제 — 하네스가 실제로 글자를 만들었는가
        // ============================================================================

        [UnityTest]
        public IEnumerator 전제_하네스가_정점을_실제로_잡는다()
        {
            (Text text, VertexSpy spy, _) = BuildSurface(UiGlyphScalePolicy.ReferenceCanvasScale, WorstCasePhase);
            Pump(text);
            yield return null;

            Assert.IsTrue(spy.Captured,
                $"{LogPrefix} 정점을 하나도 못 잡았습니다 — 폰트가 안 올라왔거나 캔버스가 안 돌았습니다. " +
                "이 상태에서 아래 «정수다» 판정은 전부 공허하게 통과합니다.");
            Assert.GreaterOrEqual(spy.VertexCount, 4,
                $"{LogPrefix} 정점이 {spy.VertexCount}개뿐입니다 — 글자가 실제로 생성되지 않았습니다.");
            Assert.IsInstanceOf<CrispText>(text,
                $"{LogPrefix} UiChrome.AddText가 CrispText를 만들지 않았습니다 — " +
                "배선이 되돌려졌다면 아래 테스트가 재는 대상 자체가 다릅니다.");
        }

        // ============================================================================
        // (1) 음성 대조 먼저 — 스냅이 꺼지면 격자를 벗어나야 한다
        // ============================================================================

        /// <summary>
        /// ★ <b>이 테스트가 먼저다.</b> 이것이 통과해야 다음 테스트의 "정수였다"가 의미를 갖는다.
        /// 안 그러면 «원래부터 정수였다»와 «스냅이 고쳤다»가 구분되지 않는다.
        /// </summary>
        [UnityTest]
        public IEnumerator 음성_대조_스냅을_끄면_반_픽셀_어긋난_채로_남는다()
        {
            CrispText.SnapEnabled = false;
            (Text text, VertexSpy spy, _) = BuildSurface(UiGlyphScalePolicy.ReferenceCanvasScale, WorstCasePhase);
            Pump(text);
            yield return null;

            Assert.IsTrue(spy.Captured, $"{LogPrefix} 정점을 못 잡았습니다(전제 실패).");
            Vector3 world = WorldOfFirstVertex(text, spy);
            float residual = world.x - Mathf.Round(world.x);

            Assert.IsFalse(NearInteger(world.x),
                $"{LogPrefix} 스냅을 껐는데도 x={world.x:F4}가 이미 정수입니다 — " +
                "하네스가 위상을 만들지 못했다는 뜻이고, 그러면 아래 «스냅이 고쳤다»도 증명되지 않습니다.");
            Assert.AreEqual(WorstCasePhase, Mathf.Abs(residual), 0.05f,
                $"{LogPrefix} 의도한 위상 {WorstCasePhase}px가 아니라 {Mathf.Abs(residual):F4}px가 나왔습니다 — " +
                "하네스의 좌표 단위 가정(월드 xy == 스크린 픽셀)이 깨졌습니다.");
        }

        // ============================================================================
        // (2) 본 판정 — 켜면 정수 픽셀에 붙는다
        // ============================================================================

        [UnityTest]
        public IEnumerator 스냅이_켜지면_글자_원점이_정수_픽셀에_붙는다()
        {
            (Text text, VertexSpy spy, _) = BuildSurface(UiGlyphScalePolicy.ReferenceCanvasScale, WorstCasePhase);
            Pump(text);
            yield return null;

            Assert.IsTrue(spy.Captured, $"{LogPrefix} 정점을 못 잡았습니다(전제 실패).");
            Vector3 world = WorldOfFirstVertex(text, spy);

            Assert.IsTrue(NearInteger(world.x),
                $"{LogPrefix} 글자 원점 x={world.x:F4}가 정수 픽셀이 아닙니다 " +
                $"(잔차 {world.x - Mathf.Round(world.x):F4}px). 획이 두 픽셀에 걸쳐 번집니다.");
            Assert.IsTrue(NearInteger(world.y),
                $"{LogPrefix} 글자 원점 y={world.y:F4}가 정수 픽셀이 아닙니다 " +
                $"(잔차 {world.y - Mathf.Round(world.y):F4}px).");
        }

        /// <summary>배율을 바꿔 가며 — 100% / 125% / 150% / 175% / 200%.
        /// <para>★ <b>125%와 175%가 특히 중요하다</b>: 그 두 배율은 <see cref="UiGlyphScalePolicy"/>가
        /// «타이포 계층이 붕괴하므로 일부러 맞추지 않았다»고 남겨 둔 구간이다. 위상 축은 크기 축과
        /// 독립이므로 <b>거기서도 격자에 붙어야 한다</b>.</para></summary>
        [UnityTest]
        public IEnumerator 모든_디스플레이_배율에서_격자에_붙는다([Values(1f, 1.25f, 1.5f, 1.75f, 2f)] float scale)
        {
            (Text text, VertexSpy spy, _) = BuildSurface(scale, WorstCasePhase);
            Pump(text);
            yield return null;

            Assert.IsTrue(spy.Captured, $"{LogPrefix} 배율 {scale}에서 정점을 못 잡았습니다(전제 실패).");
            Vector3 world = WorldOfFirstVertex(text, spy);
            Assert.IsTrue(NearInteger(world.x) && NearInteger(world.y),
                $"{LogPrefix} 배율 {scale}에서 글자 원점 ({world.x:F4}, {world.y:F4})가 격자 밖입니다.");
        }

        // ============================================================================
        // (3) 레이아웃 불변 — 이 변경이 배치를 건드리지 않았음을 못박는다
        // ============================================================================

        /// <summary>
        /// 스냅은 <b>메시 정점</b>만 옮긴다. <c>anchoredPosition</c>·<c>rect</c>·<c>preferredWidth</c>는
        /// 그대로여야 한다 — 기존 배치/폭 감사 테스트가 재는 값이 전부 그것이기 때문이다.
        /// 하나라도 움직였다면 이 변경의 «파급 없음» 주장이 거짓이다.
        /// </summary>
        [UnityTest]
        public IEnumerator 레이아웃_값은_스냅_켜고_끄고에_상관없이_같다()
        {
            CrispText.SnapEnabled = false;
            (Text off, _, _) = BuildSurface(UiGlyphScalePolicy.ReferenceCanvasScale, WorstCasePhase);
            Pump(off);
            yield return null;
            Vector2 offAnchored = off.rectTransform.anchoredPosition;
            Rect offRect = off.rectTransform.rect;
            float offWidth = off.preferredWidth;
            float offHeight = off.preferredHeight;
            Object.DestroyImmediate(_root);
            _root = null;

            CrispText.SnapEnabled = true;
            (Text on, _, _) = BuildSurface(UiGlyphScalePolicy.ReferenceCanvasScale, WorstCasePhase);
            Pump(on);
            yield return null;

            // ★ Vector2/Rect를 통째로 AreEqual에 넣지 않는다 — NUnit이 Equals(정확 비교)를 타서
            //   같은 계산이라도 마지막 자리에서 갈리면 «불안정»으로 오판된다. 성분별 + 허용오차로 잰다.
            Assert.AreEqual(offAnchored.x, on.rectTransform.anchoredPosition.x, 1e-4f,
                $"{LogPrefix} anchoredPosition.x가 달라졌습니다 — 스냅이 레이아웃을 건드렸습니다.");
            Assert.AreEqual(offAnchored.y, on.rectTransform.anchoredPosition.y, 1e-4f,
                $"{LogPrefix} anchoredPosition.y가 달라졌습니다 — 스냅이 레이아웃을 건드렸습니다.");
            Assert.AreEqual(offRect.width, on.rectTransform.rect.width, 1e-4f,
                $"{LogPrefix} rect.width가 달라졌습니다 — 스냅이 레이아웃을 건드렸습니다.");
            Assert.AreEqual(offRect.height, on.rectTransform.rect.height, 1e-4f,
                $"{LogPrefix} rect.height가 달라졌습니다 — 스냅이 레이아웃을 건드렸습니다.");
            Assert.AreEqual(offWidth, on.preferredWidth, 1e-4f,
                $"{LogPrefix} preferredWidth가 달라졌습니다 — UiChrome.Ellipsize와 " +
                "SettingsControls의 폭 모형이 전부 이 값을 씁니다.");
            Assert.AreEqual(offHeight, on.preferredHeight, 1e-4f,
                $"{LogPrefix} preferredHeight가 달라졌습니다.");
        }

        // ============================================================================
        // (4) 상주 앱 안전 — 정지 화면에서 재빌드가 돌지 않는다
        // ============================================================================

        /// <summary>
        /// 창이 <b>가만히 있으면</b> 위상은 이미 격자 위이므로 그 글자는 재스냅이 한 번도 일어나면
        /// 안 된다. 여기서 카운터가 오르면 24시간 상주 앱이 <b>조용히 CPU를 태운다</b>.
        ///
        /// ============================================================================
        /// ★★ 2026-09-07 재설계 — 이 테스트는 <b>측정 대상을 잘못 잡아</b> 거짓 빨강을 냈다
        /// ============================================================================
        /// 원판은 <b>전역</b> <c>CrispText.ResnapCount</c>를 봤다. 그런데 그 값은 프로세스의
        /// <b>모든</b> 인스턴스가 함께 쌓는 총계이고, 이 저장소의 배치모드 PlayMode는
        /// <c>Main.unity</c>(앱 전체)를 띄운 채 돈다. 그래서 <b>설계대로 캐릭터를 따라다니는</b>
        /// 말풍선 라벨(<c>DialogueBubbleRenderer</c>의 <c>Label</c>, 이것도 <see cref="CrispText"/>다)이
        /// 같은 카운터에 매 프레임 1씩 얹었다.
        /// <list type="bullet">
        ///   <item>실제 결과: <c>docs/verify/runs/ropeclimb-r3_play.xml</c> — «정지한 글자가 <b>588회</b>
        ///     재스냅됐습니다». 같은 실행의 그 테스트 출력에 <c>[말풍선] 표시 … 기울기=0.0도(꺼짐)</c>가
        ///     함께 찍혀 있다(배율 1.0이라 만화 기울기가 꺼져 축 정렬 → 스냅 대상이 된다).</item>
        ///   <item>같은 실행에서 <c>스냅이_켜지면_…</c>과 배율 5종은 <b>전부 통과</b>했다. 그 테스트들이
        ///     재는 잔차 한계는 <see cref="IntegerTolerance"/>(0.005)로
        ///     <c>GlyphPixelSnapPolicy.ResidualEpsilon</c>(0.01)보다 <b>좁다</b> — 즉 스냅 직후의 잔차는
        ///     드리프트 임계 아래였고, <b>하네스의 글자는 재스냅될 수 없었다</b>.
        ///     ⇒ 588회는 우리 글자의 것이 아니었다.</item>
        /// </list>
        /// 그래서 판정을 <b>인스턴스별</b>(<see cref="CrispText.InstanceResnapCount"/>)로 옮기고,
        /// <b>같은 관측 창 안에</b> «매 프레임 미는 표면»을 대조군으로 함께 돌린다. 대조군이 실제로
        /// 수백 회 올라야만 «정지 표면 0회»가 의미를 갖는다 — 계기가 죽어서 0인 경우와 갈라진다.
        /// </summary>
        [UnityTest]
        public IEnumerator 정지한_표면에서는_재스냅이_돌지_않는다()
        {
            const float Scale = UiGlyphScalePolicy.ReferenceCanvasScale;

            (Text stillText, VertexSpy stillSpy, _) = BuildSurface(Scale, WorstCasePhase);
            (Text moverText, _, RectTransform moverHolder) = AddSurface("MovingHolder", Scale, WorstCasePhase);
            Vector2 moverHome = moverHolder.anchoredPosition;

            Pump(stillText);
            Pump(moverText);
            yield return null;
            yield return null;
            yield return null;   // 캔버스 크기·배율이 자리 잡고 첫 스냅이 확정될 여유.

            var still = (CrispText)stillText;
            var mover = (CrispText)moverText;
            still.ResetInstanceCountersForTest();
            mover.ResetInstanceCountersForTest();
            CrispText.ResetCountersForTest();

            int frames = 0;
            float until = Time.realtimeSinceStartup + ResnapWindowSeconds;  // 벽시계(TEAM.md: 프레임 수 금지).
            while (Time.realtimeSinceStartup < until)
            {
                // 대조군만 민다. 정지 표면은 이 창 동안 한 번도 건드리지 않는다.
                moverHolder.anchoredPosition = moverHome
                    + new Vector2(frames % MoverCycleFrames * MoverStepPixels / Scale, 0f);
                frames++;
                yield return null;
            }

            int globalCount = CrispText.ResnapCount;
            int othersCount = globalCount - still.InstanceResnapCount - mover.InstanceResnapCount;

            // ★ 통과해도 숫자를 남긴다. 「전역 − 내 것」이 곧 <b>이 실행에 함께 살아 있던 앱의
            //   글자들이 같은 창에서 다시 구워진 횟수</b>이고, 그것이 이 테스트를 588회로 빨갛게
            //   만든 그 값이다. 로그로 남겨 두면 다음 사람이 같은 오진을 반복하지 않는다.
            Debug.Log($"{LogPrefix} 관측 {frames}프레임/{ResnapWindowSeconds:F2}초 — " +
                      $"정지 표면 {still.InstanceResnapCount}회(검사 {still.InstanceDriftCheckCount}회) / " +
                      $"대조군 {mover.InstanceResnapCount}회 / 전역 총계 {globalCount}회 " +
                      $"⇒ 앱의 다른 글자 {othersCount}회.");

            // ---- (1) 계기가 살아 있었는가 — 이것부터다 -------------------------------
            Assert.Greater(still.InstanceDriftCheckCount, 0,
                $"{LogPrefix} 정지 표면에 대한 드리프트 검사가 0건입니다 — 드라이버가 안 돌았거나 이 " +
                $"글자가 스냅 경로에 애초에 들어가지 못했다는 뜻이고(캔버스 없음/회전/정점 0), " +
                $"그러면 아래 «재스냅 0회»는 «재 보지 않았다»와 구분되지 않습니다(공허한 통과).");

            // ---- (2) 양성 대조 — 진짜로 폭주하면 이 계기가 잡는가 --------------------
            Assert.Greater(mover.InstanceResnapCount, frames / 2,
                $"{LogPrefix} 매 프레임 {MoverStepPixels}px씩 민 대조군이 {frames}프레임 중 " +
                $"{mover.InstanceResnapCount}회밖에 재스냅되지 않았습니다 — 인스턴스 카운터가 " +
                "폭주를 못 잡는다는 뜻이고, 그러면 아래 «0회»는 아무것도 증명하지 않습니다.");

            // ---- (3) 본 판정 ---------------------------------------------------------
            Assert.AreEqual(0, still.InstanceResnapCount,
                $"{LogPrefix} 정지한 글자가 {still.InstanceResnapCount}회 재스냅됐습니다 " +
                $"({frames}프레임 관측, 검사 {still.InstanceDriftCheckCount}회). 스냅이 수렴하지 " +
                $"않는다는 뜻이고, 상주 앱에서 매 프레임 메시를 다시 만들게 됩니다. " +
                $"[참고] 같은 창의 전역 총계는 {globalCount}회이고 그중 대조군이 " +
                $"{mover.InstanceResnapCount}회입니다 — 전역 값에는 앱의 다른 글자(말풍선 등)가 " +
                "함께 들어가므로 전역 값으로는 이 판정을 하지 않습니다.");

            // ---- (4) «0회»가 «스냅이 아예 안 걸렸다»가 아님을 못박는다 ----------------
            Vector3 world = WorldOfFirstVertex(stillText, stillSpy);
            Assert.IsTrue(NearInteger(world.x) && NearInteger(world.y),
                $"{LogPrefix} 재스냅은 0회인데 정지한 글자의 원점 ({world.x:F4}, {world.y:F4})이 " +
                "격자 밖입니다 — 수렴한 것이 아니라 스냅이 걸리지 않은 것입니다.");
        }

        /// <summary>
        /// 반대편: 표면이 <b>움직이면</b> 재스냅이 반드시 일어나야 한다. 안 그러면 말풍선처럼
        /// 매 프레임 이동하는 글자가 <b>구워 넣은 낡은 이동량</b>을 그대로 달고 다닌다.
        /// </summary>
        [UnityTest]
        public IEnumerator 움직이는_표면은_다시_스냅된다()
        {
            (Text text, VertexSpy spy, RectTransform holder) =
                BuildSurface(UiGlyphScalePolicy.ReferenceCanvasScale, 0f);
            Pump(text);
            yield return null;
            yield return null;

            var crisp = (CrispText)text;
            crisp.ResetInstanceCountersForTest();

            // 반 픽셀만큼 옮긴다 — 레이아웃은 안 바뀌고 «위치»만 바뀌므로 uGUI는 메시를 다시
            // 만들지 않는다. 그 사각지대를 CrispText가 덮는지 보는 것이 이 테스트다.
            holder.anchoredPosition += new Vector2(
                WorstCasePhase / UiGlyphScalePolicy.ReferenceCanvasScale, 0f);
            yield return null;
            yield return null;

            // ★ 전역 CrispText.ResnapCount를 쓰지 않는다 — 앱의 말풍선 하나만 떠 있어도 이 단언이
            //   «내 글자가 재스냅됐다»와 무관하게 통과한다(거짓 초록). 인스턴스 값으로 본다.
            Assert.Greater(crisp.InstanceResnapCount, 0,
                $"{LogPrefix} 표면이 반 픽셀 움직였는데 이 글자의 재스냅이 0회입니다 — " +
                "움직이는 글자는 계속 격자를 벗어난 채로 남습니다.");

            Vector3 world = WorldOfFirstVertex(text, spy);
            Assert.IsTrue(NearInteger(world.x),
                $"{LogPrefix} 이동 후 글자 원점 x={world.x:F4}가 다시 격자에 붙지 않았습니다.");
        }

        // ============================================================================
        // (5) 회전된 글자는 손대지 않는다 — 처방을 잘못 내리지 않는다는 계약
        // ============================================================================

        [UnityTest]
        public IEnumerator 회전된_글자는_건드리지_않는다()
        {
            (Text text, VertexSpy spy, RectTransform holder) =
                BuildSurface(UiGlyphScalePolicy.ReferenceCanvasScale, WorstCasePhase);
            holder.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Pump(text);
            yield return null;

            Assert.IsTrue(spy.Captured, $"{LogPrefix} 정점을 못 잡았습니다(전제 실패).");

            // 스냅을 끈 판과 <완전히 같은 로컬 좌표>여야 한다 = 아무것도 안 했다.
            Vector3 rotatedLocal = spy.FirstVertexLocal;
            Object.DestroyImmediate(_root);
            _root = null;

            CrispText.SnapEnabled = false;
            (Text off, VertexSpy offSpy, RectTransform offHolder) =
                BuildSurface(UiGlyphScalePolicy.ReferenceCanvasScale, WorstCasePhase);
            offHolder.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Pump(off);
            yield return null;

            Assert.AreEqual(offSpy.FirstVertexLocal.x, rotatedLocal.x, 1e-4f,
                $"{LogPrefix} 회전된 글자를 움직였습니다 — 회전 상태의 반올림은 글자를 비스듬히 밀어 " +
                "위치만 틀어집니다(GlyphPixelSnapPolicy 문서의 «고치지 못하는 것»).");
            Assert.AreEqual(offSpy.FirstVertexLocal.y, rotatedLocal.y, 1e-4f,
                $"{LogPrefix} 회전된 글자를 움직였습니다(y).");
        }

        // ============================================================================
        // (6) 실제 표면 — 말풍선/창이 아니라 <이 앱의 팩토리>가 만든 글자인가
        // ============================================================================

        /// <summary>
        /// <c>UiChrome.AddText</c>가 만드는 글자가 전부 <see cref="CrispText"/>임을 <b>실물</b>로
        /// 확인한다. 소스 감사(<c>GlyphPixelPhaseAuditTests</c>)는 «typeof(Text)가 없다»는
        /// <b>부재</b>를 보므로, 팩토리가 통째로 사라져도 초록이다.
        /// </summary>
        [UnityTest]
        public IEnumerator 팩토리가_만드는_글자는_전부_CrispText다()
        {
            _root = new GameObject("FactoryProbe", typeof(Canvas));
            _root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var made = new List<Text>
            {
                UiChrome.AddText(_root.transform, "Display", UiChrome.FontDisplay,
                    TextAnchor.MiddleCenter, UiChrome.TextPrimary),
                UiChrome.AddText(_root.transform, "Title", UiChrome.FontTitle,
                    TextAnchor.MiddleLeft, UiChrome.TextPrimary, bold: true),
                UiChrome.AddText(_root.transform, "Caption", UiChrome.FontCaption,
                    TextAnchor.UpperLeft, UiChrome.TextSecondary, wrap: true),
            };

            foreach (Text t in made)
            {
                Assert.IsInstanceOf<CrispText>(t,
                    $"{LogPrefix} UiChrome.AddText가 «{t.name}»에 맨 Text를 붙였습니다 — " +
                    "그 표면의 글자만 혼자 흐리게 나옵니다.");
            }
            yield return null;
        }
    }
}
