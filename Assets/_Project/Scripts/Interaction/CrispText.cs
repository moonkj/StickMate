using System.Collections.Generic;
using StickMate.Platform;
using UnityEngine;
using UnityEngine.UI;

namespace StickMate.Interaction
{
    /// <summary>
    /// 레거시 uGUI <see cref="Text"/>에 <b>글자만</b> 픽셀 격자에 맞추는 동작을 더한 것.
    /// 판정 산술은 한 톨도 여기 없다 — 전부 <see cref="GlyphPixelSnapPolicy"/>에 있다
    /// (CLAUDE.md: 정책은 플랫폼 중립 위치, 컴포넌트는 "사실 조회와 적용"만).
    ///
    /// ============================================================================
    /// 무슨 신고를 고치는가
    /// ============================================================================
    /// 사용자 신고(2026-09-07): <b>"전체적으로 글자가 흐리고 일부는 번져 보임"</b>.
    ///
    /// <para>사용자가 근거로 든 <c>[GLYPH-SCALE]</c> 로그(캔버스 배율 1.500, <b>13pt</b>)는 <b>지난
    /// 빌드의 것</b>이다 — <c>UiChrome.FontTitle</c>은 그 뒤 13→14로 옮겨졌고(그 상수 옆 주석이
    /// "13 -&gt; 14: 19.5px -&gt; 21px"라고 적고 있다), 지금 이 저장소의 타이포 상수는 20/14/12/12/10으로 <b>전부
    /// 짝수</b>라 배율 1.5에서 크기 잔차가 이미 0이다. 말풍선도 <c>ResolveFontSize()</c>가
    /// <see cref="UiGlyphScalePolicy.SnapPoints"/>를 태운다. <b>즉 「비정수 배율 확대」는 이미
    /// 해결돼 있고, 그런데도 신고가 남았다.</b></para>
    ///
    /// <para>남은 원인이 이것이다 — <b>위상(位相)</b>. 아무리 정확한 픽셀 수로 구워도 그 비트맵이
    /// 화면 x=100.37px에 얹히면 획이 두 픽셀에 나뉘어 이중선형 보간된다. 자세한 기전과 uGUI 소스
    /// 인용은 <see cref="GlyphPixelSnapPolicy"/> 클래스 문서에 있다.</para>
    ///
    /// ============================================================================
    /// ★ 왜 <c>canvas.pixelPerfect = true</c> 한 줄로 끝내지 않았는가
    /// ============================================================================
    /// 그 플래그는 <b>같은 캔버스의 모든 <see cref="Image"/></b>에도 걸려 사각형의 min/max를 각각
    /// 반올림한다 → <b>선 두께가 바뀐다</b>. 이 저장소의 획은 1pt 미만이고 45° 회전본이 있으며
    /// 부채꼴 축소 폴백은 균일 스케일이다. 대비값으로 교정해 둔 수치가 소리 없이 무너진다.
    /// <see cref="GlyphPixelSnapPolicy"/> 문서의 목록 참고.
    ///
    /// ============================================================================
    /// ★ 이 클래스가 치르는 대가 — 숨기지 않는다
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>움직이는 글자는 계단 이동한다.</b> 창을 드래그하는 동안 글자는 1 물리 픽셀 단위로
    ///     뛰고, 부드럽게 움직이는 패널 테두리와 최대 ±0.5 물리 픽셀 어긋난다
    ///     (Retina 2x에서 0.25pt, Windows 150%에서 0.33pt). <b>대안은 「항상 번져 있음」</b>이다.
    ///     <para>★ <b>말풍선은 이 항목의 예가 아니다</b> — 만화 레터링 모드는 글자에 손글씨
    ///     기울기(실기 로그 실측 <c>-9.2도</c>)를 걸어 두어
    ///     <see cref="GlyphPixelSnapPolicy.IsAxisAligned"/>가 false다. 즉 그 표면에서는 스냅이
    ///     <b>아예 돌지 않는다</b>(그쪽 흐림의 주범은 외곽선이라는 것이 2026-09-01 오프라인
    ///     A/B로 이미 실측됐다 — <c>DialogueBubbleRenderer</c>의 「말풍선만 글자가 뭉개다」 블록).</para></item>
    ///   <item><b>움직이는 동안 메시를 다시 만든다.</b> 위상이 격자에서 벗어난 프레임에만
    ///     <see cref="Graphic.SetVerticesDirty"/>를 건다. <see cref="Text"/>의 <c>TextGenerator</c>는
    ///     문자열·설정이 그대로면 캐시를 돌려주므로 비용은 정점 복사뿐이다. 정지한 창에서는
    ///     <b>재빌드가 0회</b>다(월드 좌표가 그대로면 드리프트 검사가 즉시 끝난다).</item>
    ///   <item><b>낱글자 사이 위상은 이 클래스가 손대지 않는다.</b> 블록 원점 하나만 격자에
    ///     올린다 — 블록 안에서 글자마다 누적되는 advance는 네이티브 <c>TextGenerator</c> 소관이다.
    ///     <para>★ <b>실측(2026-09-07, macOS Retina 2x 실기 캡처)</b>: 같은 문자열 안의 두 'S'와,
    ///     <b>서로 다른 두 텍스트 블록</b>의 'S'(정보창 STATUS ↔ DISPLAY)가 <b>정수 이동만으로
    ///     픽셀 단위 완전 일치</b>했다(평균 절대차 0.00/255. 양성 대조 자기 자신 0.00,
    ///     음성 대조 다른 글리프 29.75). ⇒ 글리프 배치는 <b>정수 픽셀로 양자화</b>되어 있고,
    ///     따라서 남는 위상 항은 <b>블록 원점 하나뿐</b>이다 — 이 클래스가 고치는 바로 그것이다.
    ///     <b>다만 배율 1.5에서도 같은지는 미확인이다</b>(이 머신은 Retina 2x만 낼 수 있다).</para></item>
    /// </list>
    ///
    /// <para><b>끄는 법</b>: <see cref="SnapEnabled"/> = false. 그러면 이 컴포넌트는
    /// <see cref="Text"/>와 <b>완전히 동일하게</b> 동작한다(스냅 경로가 통째로 건너뛰어진다) —
    /// 회귀가 의심되면 이 스위치 하나로 A/B를 가른다.</para>
    /// </summary>
    public class CrispText : Text
    {
        /// <summary>전역 킬 스위치. false면 이 컴포넌트는 순수 <see cref="Text"/>가 된다.
        /// <para>기본값 true인 이유: 이 스냅이 <b>기본 동작</b>이어야 신고가 사라진다. 스위치는
        /// 회귀 판별용이지 옵트인용이 아니다.</para></summary>
        public static bool SnapEnabled = true;

        /// <summary>스냅을 <b>끈 채로</b> 빌드를 돌리는 계측용 환경변수(값이 있으면 끈다).
        /// <para>★ 이것이 없으면 위 "이 스위치 하나로 A/B를 가른다"가 <b>거짓말</b>이다 —
        /// 정적 필드는 실기 빌드에서 바꿀 방법이 없다. 같은 빌드·같은 창 위치에서 스냅만
        /// 껐다 켜는 것이 이 결함의 <b>유일하게 결정적인 대조</b>다(«원래 정수였다»와
        /// «스냅이 고쳤다»는 그것 없이는 똑같이 생긴다).</para>
        /// <para>관례는 <c>STICKMATE_FORCE_MSAA</c>·
        /// <see cref="Platform.UiDensityOverride.EnvironmentVariableName"/>와 같다 —
        /// 안 주면 <b>아무 일도 일어나지 않는다</b>.</para></summary>
        public const string DisableEnvironmentVariableName = "STICKMATE_DISABLE_GLYPH_SNAP";

        /// <summary>이번 실행에서 환경변수로 꺼졌는가(진단·테스트용).</summary>
        public static bool DisabledByEnvironment { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyEnvironmentKillSwitch()
        {
            DisabledByEnvironment = false;
            string raw;
            try { raw = System.Environment.GetEnvironmentVariable(DisableEnvironmentVariableName); }
            catch (System.Exception) { return; }     // 샌드박스에서 조회가 막힐 수 있다.
            if (string.IsNullOrEmpty(raw)) return;   // ← 평상시 경로. 여기서 끝난다.

            SnapEnabled = false;
            DisabledByEnvironment = true;
            Debug.LogWarning($"[글리프위상] ★ {DisableEnvironmentVariableName}=\"{raw}\" — " +
                "글자 픽셀 스냅을 <껐습니다>(계측용 음성 대조). 이 실행의 글자는 스냅 이전 상태로 " +
                "그려집니다. 제품 판단에 쓰지 마세요.");
        }

        /// <summary>살아 있는 인스턴스 목록. <b>정적 하나</b>로 모으는 이유: 인스턴스마다
        /// <c>LateUpdate</c>를 두면 창 하나에 60~80개가 뜨는 이 앱에서 MonoBehaviour 호출
        /// 오버헤드만 매 프레임 수십 마이크로초가 된다(24시간 상주 앱이다).</summary>
        private static readonly List<CrispText> Live = new List<CrispText>(64);

        private static GameObject _driver;

        // ------------------------------------------------------------------------
        // 진단 카운터 — <b>public인 이유</b>: (1) PlayMode 테스트 어셈블리에는
        // InternalsVisibleTo가 없고(AssemblyInfo.cs는 EditMode만 허용한다), (2) 앞으로 위상 축의
        // 실기 계기판(GLYPH-PHASE 줄)을 만들 때 프로브가 읽을 값이 바로 이 둘이다.
        // 프로덕션 로직은 이 값을 <b>읽지 않는다</b> — 순수 관측용이다.
        // ------------------------------------------------------------------------

        /// <summary>직전 틱에서 드리프트를 검사한 인스턴스 수(진단·테스트용).
        /// <para>0이면 <b>드라이버가 안 돌았다</b>는 뜻이다 — 그 상태에서 "재스냅 0회"는
        /// "괜찮다"가 아니라 "재 보지 않았다"이다. 두 테스트가 이 값을 함께 본다.</para></summary>
        public static int LastDriftCheckCount { get; private set; }

        /// <summary>프로세스 수명 동안 위상 드리프트로 메시를 다시 만든 횟수(진단·테스트용).
        /// <b>정지한 창만 떠 있는데 이 값이 계속 오르면 스냅이 수렴하지 않는다는 뜻</b>이고,
        /// 그건 이 클래스의 결함이다(무한 재빌드 = 상주 앱에서 조용한 부하).</summary>
        public static int ResnapCount { get; private set; }

        /// <summary>테스트가 카운터를 되돌리는 통로. <b>프로덕션은 부르지 않는다</b>.</summary>
        public static void ResetCountersForTest()
        {
            LastDriftCheckCount = 0;
            ResnapCount = 0;
        }

        /// <summary>스냅을 적용한 뒤의 정점 0 <b>로컬</b> 좌표. 다음 프레임에 이 점만 다시 월드로
        /// 보내 격자에서 벗어났는지 보면 된다 — 정점 배열을 다시 읽을 필요가 없다.</summary>
        private Vector3 _snappedLocal;

        private bool _snapApplied;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (Application.isPlaying)
            {
                Live.Add(this);
                EnsureDriver();
            }
        }

        protected override void OnDisable()
        {
            if (Application.isPlaying) Live.Remove(this);
            _snapApplied = false;
            base.OnDisable();
        }

        /// <summary>
        /// <see cref="Text"/>가 만든 메시를 <b>그대로 받아</b> 통째로 평행이동만 시킨다.
        ///
        /// <para><b>재구현하지 않고 <c>base</c>를 부르는 것이 핵심이다.</b> uGUI의 생성 경로를 베끼면
        /// 그쪽이 고쳐질 때 우리 사본만 낡는다 — 이 저장소가 「설계 거울이 프로덕션과 13건 갈라졌다」로
        /// 이미 겪은 형태다. 여기서는 결과 정점만 옮기므로 <c>base</c>가 무엇을 바꾸든 따라간다.</para>
        ///
        /// <para><b>레이아웃은 한 톨도 안 바뀐다</b>: <c>anchoredPosition</c>·<c>rect</c>·
        /// <c>preferredWidth</c>는 건드리지 않고 <b>메시 정점</b>만 옮긴다. 그래서 기존 배치 감사
        /// 테스트(<c>DialogueComicTextPlacementTests</c>, <c>UiTextWidthModelTests</c> 등)가 재는 값은
        /// 이 변경 전후로 동일하다.</para>
        /// </summary>
        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            base.OnPopulateMesh(toFill);
            _snapApplied = false;

            if (!SnapEnabled) return;

            Canvas c = canvas;
            // WorldSpace 캔버스에서는 "월드 xy == 스크린 픽셀"이 성립하지 않는다 — 손대지 않는다.
            if (c == null || c.renderMode == RenderMode.WorldSpace) return;

            int count = toFill.currentVertCount;
            if (count <= 0) return;

            RectTransform rt = rectTransform;
            Matrix4x4 m = rt.localToWorldMatrix;
            Vector3 ex = m.MultiplyVector(Vector3.right);
            Vector3 ey = m.MultiplyVector(Vector3.up);
            if (!GlyphPixelSnapPolicy.IsAxisAligned(ex.x, ex.y, ey.x, ey.y)) return;

            var v = new UIVertex();
            toFill.PopulateUIVertex(ref v, 0);
            Vector3 local0 = v.position;
            Vector3 world0 = m.MultiplyPoint3x4(local0);

            // ScreenSpaceOverlay/Camera 캔버스에서 월드 xy는 곧 스크린 픽셀이다
            // (ScreenCoordinateConverter 문서: "GetWorldCorners는 캔버스 루트의 localScale이 이미
            //  곱해진 스크린 픽셀을 돌려준다"). 그래서 여기서 반올림하면 물리 픽셀 격자에 맞는다.
            if (GlyphPixelSnapPolicy.IsPixelAligned(world0.x, world0.y))
            {
                _snappedLocal = local0;      // 이미 격자 위 — 정점을 건드리지 않는다.
                _snapApplied = true;
                return;
            }

            float dx = GlyphPixelSnapPolicy.SnapDelta(world0.x);
            float dy = GlyphPixelSnapPolicy.SnapDelta(world0.y);

            // 축 정렬이 확인됐으므로 스크린 이동량을 성분별로 나누면 그대로 로컬 이동량이다.
            var delta = new Vector3(dx / ex.x, dy / ey.y, 0f);
            for (int i = 0; i < count; i++)
            {
                toFill.PopulateUIVertex(ref v, i);
                v.position += delta;
                toFill.SetUIVertex(v, i);
            }

            _snappedLocal = local0 + delta;
            _snapApplied = true;
        }

        /// <summary>
        /// 조상이 움직여 위상이 다시 어긋났으면 메시를 다시 만들게 한다.
        ///
        /// <para><b>왜 필요한가</b>: <see cref="Graphic"/>은 <b>크기</b>가 바뀔 때만 메시를 다시
        /// 만든다(<c>OnRectTransformDimensionsChange</c>). 창을 드래그하거나 말풍선이 캐릭터를
        /// 따라가는 것은 <b>위치</b> 변화라 메시가 그대로 남고, 그러면 우리가 구워 넣은 이동량이
        /// 낡아 글자가 다시 격자에서 벗어난다.</para>
        ///
        /// <para>정지한 표면에서는 <see cref="GlyphPixelSnapPolicy.IsPixelAligned"/>가 즉시 true라
        /// <b>아무 일도 하지 않는다</b> — 행렬 곱 한 번이 전부다.</para>
        /// </summary>
        internal void ReSnapIfDrifted()
        {
            if (!_snapApplied || !SnapEnabled) return;
            Vector3 world = rectTransform.localToWorldMatrix.MultiplyPoint3x4(_snappedLocal);
            if (GlyphPixelSnapPolicy.IsPixelAligned(world.x, world.y)) return;
            ResnapCount++;
            SetVerticesDirty();
        }

        /// <summary>목록 전체를 한 번 훑는다. <see cref="CrispTextSnapDriver"/>가
        /// <c>LateUpdate</c>에서 부른다.
        /// <para><c>Canvas.willRenderCanvases</c>에 붙이지 <b>않은</b> 이유: 그 이벤트의 구독자 중
        /// 하나가 uGUI 자신의 <c>CanvasUpdateRegistry.PerformUpdate</c>이고, 우리가 먼저 도는지
        /// 나중에 도는지가 <b>등록 순서</b>에 달려 있다. 실제로 uGUI 쪽이 먼저 등록된다
        /// (<c>Graphic.OnEnable → SetAllDirty →</c> 레지스트리 최초 생성). 나중에 도는 우리가 건
        /// dirty는 <b>한 프레임 늦게</b> 반영돼 움직이는 글자가 계속 흐리다.
        /// <c>LateUpdate</c>는 캔버스 갱신(PostLateUpdate)보다 <b>반드시</b> 앞선다.</para></summary>
        internal static void TickAll()
        {
            LastDriftCheckCount = 0;
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                CrispText t = Live[i];
                if (t == null) { Live.RemoveAt(i); continue; }
                LastDriftCheckCount++;
                t.ReSnapIfDrifted();
            }
        }

        private static void EnsureDriver()
        {
            if (_driver != null) return;
            _driver = new GameObject("CrispTextSnapDriver", typeof(CrispTextSnapDriver))
            {
                hideFlags = HideFlags.HideInHierarchy,
            };
            Object.DontDestroyOnLoad(_driver);
        }
    }
}
