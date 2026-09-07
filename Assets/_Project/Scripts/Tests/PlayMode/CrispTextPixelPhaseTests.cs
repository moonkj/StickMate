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

            var holderGo = new GameObject("Holder", typeof(RectTransform));
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
        /// 창이 <b>가만히 있으면</b> 위상은 이미 격자 위이므로 재스냅이 한 번도 일어나면 안 된다.
        /// 여기서 카운터가 오르면 24시간 상주 앱이 <b>조용히 CPU를 태운다</b>.
        /// </summary>
        [UnityTest]
        public IEnumerator 정지한_표면에서는_재스냅이_돌지_않는다()
        {
            (Text text, _, _) = BuildSurface(UiGlyphScalePolicy.ReferenceCanvasScale, WorstCasePhase);
            Pump(text);
            yield return null;
            yield return null;   // 첫 프레임의 스냅이 확정될 여유.

            CrispText.ResetCountersForTest();
            float until = Time.realtimeSinceStartup + 0.5f;    // 벽시계 예산(TEAM.md: 프레임 수 금지).
            while (Time.realtimeSinceStartup < until) yield return null;

            Assert.AreEqual(0, CrispText.ResnapCount,
                $"{LogPrefix} 정지한 글자가 {CrispText.ResnapCount}회 재스냅됐습니다 — " +
                "스냅이 수렴하지 않는다는 뜻이고, 상주 앱에서 매 프레임 메시를 다시 만들게 됩니다.");
            Assert.Greater(CrispText.LastDriftCheckCount, 0,
                $"{LogPrefix} 드리프트 검사 자체가 0건입니다 — 드라이버가 안 돌았다는 뜻이고, " +
                "그러면 위 «0회» 는 «검사를 안 했다»와 구분되지 않습니다(공허한 통과).");
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

            CrispText.ResetCountersForTest();

            // 반 픽셀만큼 옮긴다 — 레이아웃은 안 바뀌고 «위치»만 바뀌므로 uGUI는 메시를 다시
            // 만들지 않는다. 그 사각지대를 CrispText가 덮는지 보는 것이 이 테스트다.
            holder.anchoredPosition += new Vector2(
                WorstCasePhase / UiGlyphScalePolicy.ReferenceCanvasScale, 0f);
            yield return null;
            yield return null;

            Assert.Greater(CrispText.ResnapCount, 0,
                $"{LogPrefix} 표면이 반 픽셀 움직였는데 재스냅이 0회입니다 — " +
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
