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
    ///     <para>★★ <b>정정 2026-09-07(debugger) — 「말풍선은 이 항목의 예가 아니다」는 절반만 참이다.</b>
    ///     원문은 만화 레터링의 손글씨 기울기(<c>-9.2도</c>) 때문에
    ///     <see cref="GlyphPixelSnapPolicy.IsAxisAligned"/>가 false라 그 표면에서는 스냅이 아예 돌지
    ///     않는다고 적었다. 그런데 그 기울기는 <b>무조건 켜지지 않는다</b> —
    ///     <c>DialogueBubbleRenderer.ComicTiltMinGlyphPixels</c>(=14)가 <b>물리 픽셀</b> 하한을 걸어
    ///     <c>fontSize × canvasScale &lt; 14</c>면 각도가 <b>0</b>이 된다.
    ///     <list type="bullet">
    ///       <item><b>Retina 2x</b>: 13pt × 2 = 26px ≥ 14 → 기울기 켜짐 → 스냅 <b>안 돈다</b>(원문이 맞다).</item>
    ///       <item><b>1x 화면</b>(Windows 100% · 비Retina macOS): 13pt × 1 = 13px &lt; 14 → 기울기 <b>0도</b> →
    ///         축 정렬 → 스냅이 <b>돈다</b>. 즉 <b>원문이 틀리는 쪽이 하필 이 수정의 주 타깃인 Windows다</b>.</item>
    ///     </list>
    ///     실측 근거: <c>docs/verify/runs/ropeclimb-r3_play.xml</c> 배치모드(배율 1.0) 로그가
    ///     «글자크기=13pt … <b>기울기=0.0도(꺼짐)</b>»를 찍었다.
    ///     ⇒ 1x에서 말풍선 글자는 <b>움직이는 동안 매 프레임 재스냅</b>되고, 그 라벨에는
    ///     <c>Outline</c>/<c>Shadow</c>(<c>IMeshModifier</c>)가 붙어 있어 재빌드 비용이 아래 2번 항목의
    ///     «정점 복사뿐»보다 크다(외곽선은 글리프 메시를 네 방향으로 복제한다).
    ///     그 표면을 스냅에서 빼야 하는지는 <b>연출 판단</b>이라 여기서 단독으로 정하지 않는다 —
    ///     리더/<c>dev-platform</c> 소관으로 올려 둔다.
    ///     (한편 그쪽 흐림의 주범이 외곽선이라는 2026-09-01 오프라인 A/B 실측은 그대로 유효하다 —
    ///     <c>DialogueBubbleRenderer</c>의 「말풍선만 글자가 뭉개다」 블록.)</para></item>
    ///   <item><b>움직이는 동안 메시를 다시 만든다.</b> 위상이 격자에서 벗어난 프레임에만
    ///     <see cref="Graphic.SetVerticesDirty"/>를 건다. <see cref="Text"/>의 <c>TextGenerator</c>는
    ///     문자열·설정이 그대로면 캐시를 돌려주므로 비용은 정점 복사뿐이다. 정지한 창에서는
    ///     <b>재빌드가 0회</b>다(월드 좌표가 그대로면 드리프트 검사가 즉시 끝난다).</item>
    ///   <item><b>낱글자 사이 위상은 이 클래스가 손대지 않는다.</b> 블록 원점 하나만 격자에
    ///     올린다 — 블록 안에서 글자마다 누적되는 advance는 네이티브 <c>TextGenerator</c> 소관이다.
    ///     <para>★ <b>실측 (1) 블록 <b>안</b>의 advance는 정수다</b>(2026-09-07, macOS Retina 2x
    ///     실기 캡처): 같은 문자열 안의 두 'S'가 <b>정수 이동만으로 픽셀 단위 완전 일치</b>했다
    ///     (평균 절대차 0.00/255. 양성 대조 자기 자신 0.00, 음성 대조 다른 글리프 29.75).
    ///     ⇒ 남는 위상 항은 <b>블록 원점 하나뿐</b>이고, 그것이 이 클래스가 고치는 것이다.</para>
    ///     <para>★★ <b>실측 (2) — 그리고 여기서 판정 기준 하나가 틀렸다(자백).</b>
    ///     처음에 «서로 다른 블록의 같은 글리프가 MAD 0.00이면 격자에 붙어 있다»를 판정 기준으로
    ///     세웠는데, <b>그것은 「위상이 0이다」가 아니라 「두 블록의 위상이 서로 같다」만 말한다.</b>
    ///     배율 1.5 A/B 실측이 그것을 증명했다: 반복되는 카드 라벨은 스냅을 <b>끈 판에서도</b>
    ///     블록 간 MAD가 <b>271/271 = 100% 0.00</b>이었는데, 같은 글자의 에지 폭은 2.75px였고
    ///     스냅을 켜니 2.53px로 줄었다. <b>둘 다 0.00인데 하나는 흐렸다.</b>
    ///     ⇒ 위상 0을 재는 지표는 <b>에지 폭 / 획 봉우리 커버리지</b>
    ///     (<see cref="GlyphPixelSnapPolicy.PeakCoverage"/>)이지 블록 간 일치가 아니다.
    ///     같은 함정을 다시 파지 마라.</para></item>
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

        /// <summary>직전 틱에서 드리프트를 검사한 인스턴스 수(진단용). <b>앱 전체</b>가 몇 개의
        /// 글자를 들고 있는지를 말한다 — 프로세스 전역이다.
        /// <para>0이면 <b>드라이버가 안 돌았다</b>는 뜻이다.</para>
        /// <para>★ <b>다만 «내 글자가 검사됐는가»의 근거로는 쓰지 마라</b>(2026-09-07 정정). 앱의
        /// 다른 글자 하나만 살아 있어도 이 값은 &gt; 0이라, 「내 글자는 목록에 없었다」와
        /// 구분되지 않는다 — <see cref="ResnapCount"/>가 588회 거짓 빨강을 낸 것과 <b>같은 병</b>이고,
        /// 이쪽은 조용히 <b>초록</b>이 되는 쪽이라 더 위험하다.
        /// 인스턴스 단위 근거는 <see cref="InstanceDriftCheckCount"/>다.</para></summary>
        public static int LastDriftCheckCount { get; private set; }

        /// <summary>프로세스 수명 동안 <b>살아 있는 모든 인스턴스</b>가 위상 드리프트로 메시를 다시
        /// 만든 횟수(진단용 총계).
        ///
        /// <para>★★ <b>이 값을 「수렴했는가」의 판정에 쓰지 마라</b>(2026-09-07 거짓 빨강 1건).
        /// 원래 주석은 «정지한 창만 떠 있는데 이 값이 계속 오르면 스냅이 수렴하지 않는다는 뜻»이라고
        /// 적었고, 그 문장을 그대로 믿은 PlayMode 테스트가 <b>588회</b>를 세어 실패했다. 그런데
        /// 이 카운터는 <b>프로세스 전역</b>이고 <see cref="TickAll"/>은 <b>앱의 모든 글자</b>를 훑는다 —
        /// 배치모드 PlayMode는 <c>Main.unity</c>를 띄운 채 돌므로, <b>설계대로 매 프레임 움직이는</b>
        /// 말풍선 라벨(<c>DialogueBubbleRenderer</c>의 <c>Label</c>)이 같은 카운터에 함께 쌓인다.
        /// 즉 <b>«내 글자가 발산했다»와 «남의 글자가 정상적으로 따라다녔다»가 똑같이 생긴다</b> —
        /// 이 저장소가 반복해 겪은 그 형태다.</para>
        ///
        /// <para>⇒ <b>수렴 판정은 반드시 <see cref="InstanceResnapCount"/>(인스턴스별)로 한다.</b>
        /// 이 총계는 «앱 전체가 지금 얼마나 글자를 다시 굽고 있는가»라는 <b>부하 지표</b>로만 읽는다.</para></summary>
        public static int ResnapCount { get; private set; }

        /// <summary>테스트가 <b>전역</b> 카운터를 되돌리는 통로. <b>프로덕션은 부르지 않는다</b>.
        /// <para>★ 전역 값은 «앱 전체 부하» 지표다. 「이 글자가 수렴했는가」를 물으려면
        /// <see cref="ResetInstanceCountersForTest"/> + <see cref="InstanceResnapCount"/>를 써라 —
        /// 이유는 <see cref="ResnapCount"/> 문서에 있다.</para></summary>
        public static void ResetCountersForTest()
        {
            LastDriftCheckCount = 0;
            ResnapCount = 0;
        }

        // ------------------------------------------------------------------------
        // ★★ 인스턴스별 진단 카운터 (2026-09-07 debugger 신설)
        //
        // 왜 생겼나: 위 전역 두 개만으로는 <b>「내 글자」와 「남의 글자」를 구분할 수 없다</b>.
        // 배치모드 PlayMode는 Main.unity(앱 전체)를 띄운 채 도는데, TickAll()은 프로세스의 모든
        // 인스턴스를 훑으므로 <b>설계대로 매 프레임 따라다니는 말풍선 라벨</b>이 같은 카운터에
        // 쌓인다. 실제로 그것 때문에 «정지한 글자가 588회 재스냅됐다»는 거짓 빨강이 났다.
        //
        // 이 둘은 프로덕션 로직이 <b>읽지 않는다</b>(순수 관측). int 두 개라 인스턴스당 8바이트다.
        // ------------------------------------------------------------------------

        /// <summary><b>이 인스턴스</b>가 «스냅이 걸린 상태로» 드리프트 검사를 받은 횟수.
        /// <para>0이면 이 글자는 애초에 스냅 경로에 들어가지 못했다(캔버스 없음 / 회전됨 /
        /// 정점 0개 / <see cref="SnapEnabled"/> false). 그 상태에서 «재스냅 0회»는 «괜찮다»가
        /// 아니라 <b>«재 보지 않았다»</b>이므로, 수렴을 단언하는 테스트는 이 값이 &gt; 0임을
        /// 반드시 함께 확인한다.</para></summary>
        public int InstanceDriftCheckCount { get; private set; }

        /// <summary><b>이 인스턴스</b>가 위상 드리프트로 메시를 다시 만들게 한 횟수.
        /// <para>표면이 <b>정지해 있는데</b> 이 값이 계속 오르면 그때는 진짜로 스냅이 수렴하지
        /// 않는 것이고, 그건 이 클래스의 결함이다(무한 재빌드 = 상주 앱에서 조용한 부하).
        /// <b>전역 <see cref="ResnapCount"/>로는 그 판정을 할 수 없다.</b></para></summary>
        public int InstanceResnapCount { get; private set; }

        /// <summary>이 인스턴스의 카운터를 되돌린다. <b>프로덕션은 부르지 않는다</b>.</summary>
        public void ResetInstanceCountersForTest()
        {
            InstanceDriftCheckCount = 0;
            InstanceResnapCount = 0;
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
            // ★ 이 줄이 «스냅이 걸린 채로 실제로 재 봤다»의 증거다. 가드보다 <b>뒤</b>에 두는 것이
            //   핵심 — 앞에 두면 «방문했다»만 세게 되어 «재 보지 않았다»와 구분되지 않는다.
            InstanceDriftCheckCount++;
            Vector3 world = rectTransform.localToWorldMatrix.MultiplyPoint3x4(_snappedLocal);
            if (GlyphPixelSnapPolicy.IsPixelAligned(world.x, world.y)) return;
            ResnapCount++;
            InstanceResnapCount++;
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
