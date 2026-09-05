using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using StickMate.Interaction;
using StickMate.States;

namespace StickMate.EditorTools
{
    /// <summary>
    /// ★ 앱 아이콘 캡처 파이프라인 — <c>design/character/APP_ICON_SPEC.md</c> §4·§6의 구현체.
    ///
    /// ============================================================================
    /// 이 스크립트가 무엇을 하고, 무엇을 <b>하지 않는가</b>
    /// ============================================================================
    /// <b>한다</b>: <c>Assets/_Project/Prefabs/Stickman.prefab</c>를 정물로 세우고, 전용 직교 카메라로
    /// 정사각형 RenderTexture에 찍어, 2티어 크롭(≤32px 반신 / ≥40px 전신)과 불투명 플레이트 합성을 거쳐
    /// 타일 10종(16·20·24·32·40·48·64·96·128·256) PNG와 <c>.ico</c>를 낸다.
    ///
    /// <b>하지 않는다</b>: <c>ProjectSettings.asset</c>의 <c>m_BuildTargetIcons</c> 배선.
    /// 그건 별도 단계다(M-5의 마지막 칸). 이 스크립트는 <b>파일만</b> 만든다.
    ///
    /// ============================================================================
    /// ★ 새 조형이 0개다 — 이건 "그린 아이콘"이 아니라 "찍은 아이콘"이다
    /// ============================================================================
    /// 사양 §0의 못: 아이콘의 모든 치수는 프리팹의 <c>widthCurve</c> 실측에서 나온다.
    /// 그래서 이 스크립트는 좌표를 <b>손으로 옮겨 적지 않고</b> 살아 있는 인스턴스에서 잰다
    /// (<see cref="FigureGeometry.Probe"/>). 사양 §4-2/§4-4의 숫자는 <b>기대값</b>으로만 쓰이고,
    /// 실측이 그 기대값에서 벗어나면 <see cref="StickmanIconGates.CalibrateGeometry"/>가 굽기를 멈춘다.
    ///
    /// <para>이 방향이 반대보다 나은 이유는 사양 §6-3에 이미 적혀 있다 — <i>"좌표를 문서에 베껴 적는
    /// 순간 언젠가 갈라진다(이 저장소가 반복해서 당한 형태)"</i>. 문서를 1차 출처로 삼으면 프리팹이
    /// 바뀌어도 아무도 모른다. 실측을 1차로 두고 문서를 <b>교정 기준</b>으로 쓰면 갈라지는 순간
    /// 굽기가 죽는다.</para>
    ///
    /// ============================================================================
    /// ★ 실행 방법 — 그리고 <c>-nographics</c>를 쓰지 마라
    /// ============================================================================
    /// <list type="bullet">
    ///   <item>에디터: 메뉴 <c>StickMate/Bake App Icon (Windows)</c></item>
    ///   <item>배치:
    ///     <c>Unity -batchmode -projectPath &lt;repo&gt;
    ///     -executeMethod StickMate.EditorTools.StickmanIconBaker.BatchBakeWindows
    ///     -quit -logFile &lt;path&gt;</c></item>
    /// </list>
    /// ★ <b><c>-nographics</c>를 붙이면 안 된다.</b> 이건 실제로 GPU에 그리는 작업이라
    ///   <c>Camera.Render()</c>가 빈 텍스처를 낸다. 그리고 그 실패는 <b>성공과 똑같이 생겼다</b>
    ///   (종료코드 0 + PNG 파일 존재). 그래서 <see cref="StickmanIconGates"/>가 잉크 화소 수를 세고
    ///   0이면 죽는다 — 종료코드로 판정하지 마라.
    /// ★ <c>-quit</c>은 여기서 <b>안전하다</b>. CLAUDE.md의 "-quit 금지"는 <c>-runTests</c> 전용
    ///   컨벤션이다(0건 실행 + 종료코드 0). 굽기는 그 자체로 완결된 배치 작업이다.
    ///   다만 판정은 여전히 <b>산출물</b>(PNG 10장 + <c>bake_report.txt</c>의 게이트 표)로 한다.
    ///
    /// ============================================================================
    /// 유저 자산 불변 / 비침해
    /// ============================================================================
    /// <list type="bullet">
    ///   <item>프리팹 <b>에셋</b>을 수정하지 않는다. 임시 씬에 인스턴스를 만들고 그 씬을
    ///     <b>저장 없이</b> 닫는다(<see cref="Stage"/>).</item>
    ///   <item>사용자가 열어 둔 씬을 건드리지 않는다 — <c>NewSceneMode.Additive</c>로 우리 씬을
    ///     따로 만들고 우리 것만 닫는다.</item>
    ///   <item>인스턴스의 <c>transform</c>을 한 톨도 안 건드린다. 자세는 프리팹에 저장된 그대로다
    ///     (사양 §6-2 2단계).</item>
    ///   <item>콜라이더 전부 <c>enabled = false</c>, <c>Rigidbody2D.simulated = false</c> —
    ///     "관전 전용 = 콜라이더 0" 정신(<c>UserAssetImmutabilityAuditTests</c>).</item>
    /// </list>
    /// </summary>
    public static class StickmanIconBaker
    {
        // ====================================================================
        // 사양 상수 — 전부 design/character/APP_ICON_SPEC.md 에서 온다
        // ====================================================================

        /// <summary>사양 §4-1: 소재는 구운 프리팹 그대로다.</summary>
        public const string PrefabAssetPath = "Assets/_Project/Prefabs/Stickman.prefab";

        /// <summary>
        /// 촬영장 X. 사양 §6-2 1단계가 지정한 값 — <c>CharacterPortraitStage</c>가 이미
        /// 10000(<c>StageWorldX</c>)과 10200(<c>SecondaryStageWorldX</c>)을 쓰므로 충돌을 피한다.
        /// </summary>
        public const float StageWorldX = 20000f;

        /// <summary>사양 §4-3: 그림 높이 ÷ 타일 변. 16px에서 상하 여백이 정확히 1.00px가 되는 최대값.</summary>
        public const float FrameFill = 0.875f;

        /// <summary>사양 §6-2 4-a: 슈퍼샘플 배수. <b>MSAA를 쓰지 않는다</b>(2026-08-29 "선 화질 조사").</summary>
        public const int Supersample = 8;

        /// <summary>사양 §4-6의 타일 10종. 순서를 바꾸지 마라 — 보고서와 <c>.ico</c> 항목 순서가 이걸 따른다.</summary>
        public static readonly int[] Tiles = { 16, 20, 24, 32, 40, 48, 64, 96, 128, 256 };

        /// <summary>사양 §4-6이 256을 최대 타일로 못박았다. <c>.ico</c>에서 이 크기만 PNG로 임베드된다.</summary>
        public const int PngEmbedTile = 256;

        // ---- 사양 §4-2 / §4-4 의 기대값 (1차 출처가 아니다 — 교정 기준이다) -------------
        // 이 값들을 카메라에 직접 대입하지 않는다. 살아 있는 프리팹에서 잰 값과 대조해
        // 벌어지면 굽기를 멈추는 데에만 쓴다(클래스 문서의 "실측이 1차" 참고).

        /// <summary>사양 §4-2 잉크 사각형 x ∈ [−0.361132, +0.434959], y ∈ [−0.047025, +1.734390].</summary>
        public static readonly Rect DocumentedInkRect =
            Rect.MinMaxRect(-0.361132f, -0.047025f, 0.434959f, 1.734390f);

        /// <summary>사양 §4-2 티어 S 크롭 하단 <c>yb = hipY − W_다리/2 = 0.701021 − 0.047025</c>.</summary>
        public const float DocumentedTierSmallBottomY = 0.653996f;

        /// <summary>사양 §4-4 티어 L <c>orthographicSize</c>.</summary>
        public const float DocumentedOrthoSizeLarge = 1.017951f;

        /// <summary>사양 §4-4 티어 S <c>orthographicSize</c>.</summary>
        public const float DocumentedOrthoSizeSmall = 0.617368f;

        /// <summary>사양 §4-4 바라보는 점의 x(두 티어 공통) — 잉크 사각형의 가로 중심.</summary>
        public const float DocumentedCenterX = 0.036914f;

        /// <summary>사양 §4-4 티어 L 이 바라보는 점의 y.</summary>
        public const float DocumentedCenterYLarge = 0.843682f;

        /// <summary>사양 §4-4 티어 S 가 바라보는 점의 y.</summary>
        public const float DocumentedCenterYSmall = 1.194193f;

        // ====================================================================
        // 파라미터 — ★ 사양이 "미확정"으로 남긴 2건이 전부 여기 있다
        // ====================================================================

        /// <summary>
        /// 굽기 파라미터. <b>사양의 미확정 2건은 판단하지 않고 여기 노출했다</b>(사양 §3-5 / §4-5).
        /// 기본값은 사양이 "잠정"으로 적어 둔 값이고, 최종 결정은 <b>실기 A/B</b>와
        /// <b><c>design-art</c> 판정</b>이다 — 코드가 대신 정하지 않는다.
        /// </summary>
        public sealed class Settings
        {
            /// <summary>
            /// ★ <b>미확정 1건 (사양 §3-5 · 게이트 G7)</b> — 이 값 <b>이상</b>의 타일이 전신(티어 L),
            /// 미만이 반신(티어 S)이다.
            /// <para>사양의 잠정값은 <b>40</b>이고, <b>48</b>로 늦추는 안이 함께 올라와 있다.
            /// 사양 원문: <i>"40px에서 전신은 '된다'이지 '좋다'가 아니다. <b>산술로는 안 갈린다</b> —
            /// §8의 실기 A/B로만 갈린다."</i></para>
            /// <para>⇒ <b>이 라운드는 판정하지 않는다.</b> <see cref="StickmanIconBaker.BakeTierBoundaryAb"/>가
            /// 40px 타일을 전신/반신 두 장으로 함께 구워 나란히 볼 수 있게 한다(G7의 입력).</para>
            /// </summary>
            public int TierBoundaryPx = 40;

            /// <summary>
            /// ★ <b>미확정 2건 (사양 §4-5)</b> — 플레이트 모서리 반경 계수(<c>r = 계수 · S</c>).
            /// <para>사양 원문: <i>"모서리 반경 계수 0.1875와 '테두리 없음'은 <b>시각 언어 결정</b>이라
            /// 내 소관이 아니다"</i> → <c>design-art</c> 판정 대기.</para>
            /// <para>사양이 못박은 <b>기하학적 제약</b>만 코드가 강제한다: 계수가 0.25를 넘으면
            /// 16px에서 코너가 그림을 문다 ⇒ <see cref="MaxCornerRadiusFactor"/>에서 거절한다.</para>
            /// <para>★ macOS <c>.icns</c>로 갈 때는 <b>0으로</b> 둔다 — 시스템이 스퀘어클로 마스킹하므로
            /// 여기서도 깎으면 이중 라운딩이 된다(사양 §10). 이번 라운드 범위 밖.</para>
            /// </summary>
            public float CornerRadiusFactor = 0.1875f;

            /// <summary>
            /// ★ <b>미확정 2건의 나머지 절반</b> — 플레이트 테두리 선. 사양 §4-5 잠정 결론은 "없음"
            /// (16px에서 1px 테두리는 그림 예산의 14%를 먹는다). <c>design-art</c> 판정 대기라
            /// 값만 노출하고 기본은 사양대로 0이다. 0보다 크면 그 두께(px)로 플레이트 안쪽에 그린다.
            /// </summary>
            public float BorderWidthPx = 0f;

            /// <summary>테두리 색. <see cref="BorderWidthPx"/>가 0이면 안 쓰인다. <c>design-art</c> 소관.</summary>
            public Color BorderColor = new Color(0f, 0f, 0f, 1f);

            /// <summary>
            /// 그림 높이 ÷ 타일 변. 기본은 사양 §4-3의 0.875.
            /// <para>★ macOS는 1024 캔버스 안 824px 안전 영역 때문에 <b>≈0.80</b>이 된다(사양 §10).
            /// 이번 라운드 범위 밖이라 <b>값만 열어 뒀다</b> — macOS 경로는 미구현·미검증이다.</para>
            /// </summary>
            public float FrameFillOverride = FrameFill;

            /// <summary>
            /// 잉크색 덮어쓰기. <c>null</c>이면 <b>프리팹에 구워진 색 그대로</b>(출하 기본 = 검정).
            /// <para>사양 §2-3의 조건부 재발주 조항 — 훗날 M1-A(유채색 잉크)로 기본 잉크가 바뀌면
            /// <b>여기 한 줄만</b> 바꿔 다시 구우면 된다.</para>
            /// <para>⚠ 흰 잉크로 바꾸면 플레이트도 함께 재판정해야 한다. 사양 §2-1이
            /// "검은 플레이트 + 흰 그림"을 <b>대칭이 아니라 더 나쁘다</b>로 기각했다
            /// (검정 플레이트 ↔ Win11 다크 작업표시줄 1.27:1).</para>
            /// </summary>
            public Color? InkColorOverride = null;

            /// <summary>
            /// 플레이트 색. 기본은 <see cref="UiChrome.PortraitSurface"/> = <c>#E9EAE6</c>.
            /// <b>토큰을 읽는다 — hex를 옮겨 적지 않는다.</b> 사양 §2-3의 "신규 색 0개"가 그 뜻이다
            /// (정보창 액자 색이 바뀌면 아이콘도 같이 따라가야 한다는 인계 계약이기도 하다).
            /// </summary>
            public Color PlateColor = UiChrome.PortraitSurface;

            /// <summary>PNG 출력 폴더(저장소 루트 기준 상대). 사양 §6-2 4-f.</summary>
            public string OutputFolder = "docs/branding/icon/win";

            /// <summary>
            /// <c>.ico</c> 출력 경로. <c>null</c>이면 안 만든다.
            /// <para>★ 기본값이 <c>Assets/</c> <b>바깥</b>인 것은 의도다 — 사양 §6-2 5단계는
            /// <c>Assets/_Project/Branding/</c>를 제안하지만, 거기 쓰면 그 순간 임포트가 돌고
            /// <c>ProjectSettings</c> 배선과 뒤섞인다. 배선은 별도 단계다.</para>
            /// </summary>
            public string IcoOutputPath = "docs/branding/icon/win/StickMate.ico";

            /// <summary>
            /// 실측 잉크 사각형 대신 사양 §4-2의 문서 값을 카메라에 직접 대입한다(기본 false).
            /// 프리팹이 바뀌어 교정이 깨졌을 때 <b>옛 사양대로</b> 한 장 뽑아 비교하는 용도다.
            /// 상시 사용 금지 — 이 상태로 구운 아이콘은 프리팹과 갈라져 있다.
            /// </summary>
            public bool UseDocumentedInkRect = false;

            /// <summary>
            /// 교정(<c>C1</c>) 허용 오차 — 실측 잉크 사각형과 사양 §4-2 값의 최대 허용 차(월드 유닛).
            /// <para>0.0027 ≈ 잉크 높이의 0.15%. 이 정도는 <c>LineRenderer</c>의 캡 테셀레이션
            /// (<c>numCapVertices = 8</c>, 이상적인 원의 최대 99.8%)으로 설명되는 범위다(사양 §9 말미).
            /// 그보다 벌어지면 <b>프리팹이 바뀌었거나 사양이 낡은 것</b>이므로 굽지 않는다.</para>
            /// </summary>
            public float GeometryToleranceUnits = 0.0027f;

            /// <summary>기하 교정(C1)이 깨져도 계속 굽는다. <b>기본 false</b> — 교정이 깨지면 그 뒤 숫자를
            /// 전부 폐기하는 것이 이 저장소 규칙이다. 조사 목적으로만 켠다.</summary>
            public bool ContinueOnCalibrationFailure = false;

            /// <summary>기하학적 상한 — 사양 §4-5가 못박은 "0.25·S를 넘으면 16px에서 코너가 그림을 문다".</summary>
            public const float MaxCornerRadiusFactor = 0.25f;
        }

        // ====================================================================
        // 산출물 자료구조
        // ====================================================================

        /// <summary>구워진 타일 한 장. 게이트가 이 구조체만 보고 잰다(파일을 다시 읽지 않는다).</summary>
        public sealed class Tile
        {
            /// <summary>타일 변(px).</summary>
            public int Size;

            /// <summary>true면 티어 S(엉덩이 위 반신), false면 티어 L(전신).</summary>
            public bool TierSmall;

            /// <summary>RGBA32, <b>아래에서 위로</b>(<c>ReadPixels</c>/<c>GetPixels32</c> 순서 그대로).</summary>
            public Color32[] Pixels;

            /// <summary>이 타일을 찍은 카메라의 중심(촬영장 로컬 = 루트 기준). 월드→픽셀 매핑에 쓴다.</summary>
            public Vector2 CameraCenter;

            /// <summary>이 타일을 찍은 카메라의 <c>orthographicSize</c>(= 세로 반높이, aspect 1).</summary>
            public float OrthoSize;

            /// <summary>파일로 나갈 때의 이름(확장자 제외). A/B 산출물은 접미사가 붙는다.</summary>
            public string FileStem;

            /// <summary>루트 로컬 좌표 → 이 타일의 픽셀 좌표(원점 좌하단, y 위쪽 +).</summary>
            public Vector2 WorldToPixel(Vector2 local)
            {
                float half = OrthoSize;                       // aspect = 1 이므로 가로/세로 반높이가 같다
                float px = (local.x - CameraCenter.x) / (2f * half) * Size + Size * 0.5f;
                float py = (local.y - CameraCenter.y) / (2f * half) * Size + Size * 0.5f;
                return new Vector2(px, py);
            }

            /// <summary>루트 로컬 유닛 → 픽셀 배율.</summary>
            public float UnitsToPixels => Size / (2f * OrthoSize);
        }

        /// <summary>한 번의 굽기 결과 전체.</summary>
        public sealed class BakeOutput
        {
            public Settings Config;
            public FigureGeometry Geometry;
            public List<Tile> TileList = new List<Tile>();

            /// <summary>G0 양성 대조용 — 256 전신을 <b>일부러</b> 24px로 면적 축소한 틀린 타일.</summary>
            public Tile PositiveControl24;

            /// <summary>G7 A/B — 경계 타일을 두 티어로 각각 구운 것(둘 중 하나는 본편과 같은 비트다).</summary>
            public Tile BoundaryTierSmall;
            public Tile BoundaryTierLarge;

            public List<string> Log = new List<string>();
        }

        // ====================================================================
        // 메뉴 / 배치 진입점
        // ====================================================================

        [MenuItem("StickMate/Bake App Icon (Windows)")]
        public static void BakeWindowsMenu() => BakeWindows(new Settings());

        [MenuItem("StickMate/Bake App Icon — 티어 경계 A/B (G7)")]
        public static void BakeTierBoundaryAbMenu() => BakeTierBoundaryAb(new Settings());

        /// <summary>배치 진입점. <b><c>-nographics</c> 금지</b>(클래스 문서 참고).</summary>
        public static void BatchBakeWindows() => BakeWindows(new Settings());

        /// <summary>
        /// 본편 굽기 — 타일 10종 + 게이트 + 보고서(+ 요청 시 <c>.ico</c>).
        /// </summary>
        /// <returns>모든 <b>자동</b> 게이트가 통과하면 true. ★ G5/G6은 자동으로 못 재므로
        /// 이 반환값이 true여도 "아이콘이 됐다"는 뜻이 <b>아니다</b>(사양 §8 원칙).</returns>
        public static bool BakeWindows(Settings settings)
        {
            if (settings == null) settings = new Settings();
            string root = RepoRoot();
            BakeOutput output = null;
            try
            {
                output = Capture(settings, includePositiveControl: true, includeBoundaryAb: false);
                if (output == null) return false;

                StickmanIconGates.Report report = StickmanIconGates.RunAll(output);
                WriteTiles(root, settings, output.TileList);
                if (!string.IsNullOrEmpty(settings.IcoOutputPath))
                {
                    WriteIco(Path.Combine(root, settings.IcoOutputPath), output.TileList);
                }
                WriteReport(root, settings, output, report);

                Debug.Log("[아이콘] 굽기 완료 — " + report.OneLineSummary() +
                          "\n  산출물: " + Path.Combine(root, settings.OutputFolder) +
                          "\n  ★ G5(실기 캡처)/G6(exe 리소스)는 자동으로 못 잰다. 보고서의 '미확인' 칸을 읽어라.");
                return report.AllAutomaticGatesPassed;
            }
            catch (Exception e)
            {
                Debug.LogError("[아이콘] 굽기 실패 — " + e);
                return false;
            }
            finally
            {
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// ★ 게이트 G7 (사양 §3-5 미확정 1건) — 경계 타일을 <b>전신/반신 두 장</b>으로 굽는다.
        /// 두 장을 G5의 ③(Alt+Tab) ④(바탕화면 바로가기) 위치에서 나란히 보는 것이 판정 방법이고,
        /// <b>산술로는 안 갈린다</b>. 이 함수는 그 입력을 만들 뿐 판정하지 않는다.
        /// </summary>
        public static bool BakeTierBoundaryAb(Settings settings)
        {
            if (settings == null) settings = new Settings();
            string root = RepoRoot();
            try
            {
                BakeOutput output = Capture(settings, includePositiveControl: false, includeBoundaryAb: true);
                if (output == null) return false;

                string abFolder = Path.Combine(settings.OutputFolder, "ab");
                WriteTiles(root, settings, new List<Tile> { output.BoundaryTierLarge, output.BoundaryTierSmall }, abFolder);
                Debug.Log($"[아이콘] G7 A/B 산출 — {settings.TierBoundaryPx}px 전신/반신 2장: " +
                          Path.Combine(root, abFolder) +
                          "\n  ★ 판정은 실기다(사양 §8 G7). 이 두 장을 나란히 보기 전에는 경계를 확정하지 마라.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[아이콘] G7 A/B 굽기 실패 — " + e);
                return false;
            }
            finally
            {
                AssetDatabase.Refresh();
            }
        }

        // ====================================================================
        // 촬영 — 사양 §6-2
        // ====================================================================

        /// <summary>
        /// 사양 §6-2 1~4단계. 임시 씬에 정물을 세우고 타일 전부를 찍는다.
        /// <para>★ 파일을 쓰지 않는다 — 순수하게 픽셀만 만든다. 그래야 게이트가 <b>디스크를 거치지 않고</b>
        /// 그 자리에서 재고, 교정이 깨졌을 때 <b>아무것도 안 나가게</b> 할 수 있다.</para>
        /// </summary>
        public static BakeOutput Capture(Settings settings, bool includePositiveControl, bool includeBoundaryAb)
        {
            if (settings.CornerRadiusFactor < 0f || settings.CornerRadiusFactor > Settings.MaxCornerRadiusFactor)
            {
                throw new ArgumentOutOfRangeException(nameof(settings.CornerRadiusFactor),
                    $"모서리 반경 계수 {settings.CornerRadiusFactor}는 허용 범위 [0, {Settings.MaxCornerRadiusFactor}] 밖이다. " +
                    "사양 §4-5: 0.25·S를 넘으면 16px에서 코너가 그림을 문다.");
            }
            if (settings.FrameFillOverride <= 0f || settings.FrameFillOverride > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(settings.FrameFillOverride));
            }

            var output = new BakeOutput { Config = settings };

            using (var stage = Stage.Open(settings))
            {
                FigureGeometry geom = FigureGeometry.Probe(stage.Root.transform, StageWorldX);
                output.Geometry = geom;

                // ---- 교정 C1: 실측 기하 ↔ 사양 §4-2 문서 값 --------------------------------
                // 이 저장소 규칙: 계산기를 만들면 알려진 값으로 먼저 교정하고, 교정이 깨지면
                // 그 뒤 숫자를 전부 폐기한다. 여기가 그 자리다.
                string calib = StickmanIconGates.CalibrateGeometry(geom, settings.GeometryToleranceUnits);
                if (calib != null)
                {
                    output.Log.Add("C1 기하 교정 실패 — " + calib);
                    if (!settings.ContinueOnCalibrationFailure)
                    {
                        Debug.LogError("[아이콘] C1 기하 교정 실패 — 아무것도 굽지 않는다.\n" + calib +
                            "\n  프리팹이 바뀌었으면 design/character/APP_ICON_SPEC.md §4-2/§4-4를 먼저 갱신해라. " +
                            "조사 목적이면 Settings.ContinueOnCalibrationFailure = true.");
                        return null;
                    }
                    Debug.LogWarning("[아이콘] C1 기하 교정 실패인데 ContinueOnCalibrationFailure=true — " +
                                     "이 산출물의 숫자를 근거로 쓰지 마라.\n" + calib);
                }
                else
                {
                    output.Log.Add("C1 기하 교정 통과 — 실측 잉크 사각형이 사양 §4-2와 " +
                                   settings.GeometryToleranceUnits.ToString("0.#####") + " 유닛 안에서 일치한다.");
                }

                Rect ink = settings.UseDocumentedInkRect ? DocumentedInkRect : geom.InkRect;
                float tierSmallBottom = settings.UseDocumentedInkRect
                    ? DocumentedTierSmallBottomY
                    : geom.TierSmallBottomY;

                // ---- C2: 프러스텀 오염 검사 (사양 §6-2 3단계) -----------------------------
                stage.PrepareCamera(ink, tierSmallBottom, tierSmall: false, settings);
                string intruder = StickmanIconGates.FindFrustumIntruder(stage.Cam, stage.Root);
                if (intruder != null)
                {
                    throw new InvalidOperationException(
                        "[아이콘] C2 실패 — 촬영장 프러스텀에 우리 인스턴스가 아닌 렌더러가 있다: " + intruder +
                        "\n  아이콘에 없어야 할 것이 찍힌다. 사양 §6-2 3단계.");
                }

                // ---- 타일 10종 ------------------------------------------------------------
                foreach (int size in Tiles)
                {
                    bool tierSmall = size < settings.TierBoundaryPx;
                    Tile t = stage.CaptureTile(size, ink, tierSmallBottom, tierSmall, settings);
                    t.FileStem = "icon_" + size;
                    output.TileList.Add(t);
                }

                // ---- G0 양성 대조 ---------------------------------------------------------
                // 사양 §8 G0: "일부러 틀린 타일(=256 전신을 24px로 박스 축소한 것)을 G3에 넣어
                //              반드시 실패해야 한다."
                // 이것이 §3-3이 말한 "그냥 256만 넣고 Windows가 축소하게 두자"와 정확히 같은 물건이다.
                if (includePositiveControl)
                {
                    Tile big = output.TileList.Find(x => x.Size == PngEmbedTile);
                    if (big == null) throw new InvalidOperationException("256 타일이 없다 — G0을 만들 수 없다.");
                    output.PositiveControl24 = AreaResample(big, 24);
                    output.PositiveControl24.FileStem = "_positive_control_24_from256";
                }

                // ---- G7 A/B --------------------------------------------------------------
                if (includeBoundaryAb)
                {
                    int b = settings.TierBoundaryPx;
                    output.BoundaryTierLarge = stage.CaptureTile(b, ink, tierSmallBottom, tierSmall: false, settings);
                    output.BoundaryTierLarge.FileStem = "icon_" + b + "_tierL_전신";
                    output.BoundaryTierSmall = stage.CaptureTile(b, ink, tierSmallBottom, tierSmall: true, settings);
                    output.BoundaryTierSmall.FileStem = "icon_" + b + "_tierS_반신";
                }
            }

            return output;
        }

        // ====================================================================
        // 촬영장 — 사양 §6-2 1~3단계
        // ====================================================================

        /// <summary>
        /// 임시 씬 + 프리팹 인스턴스 + 전용 카메라. <c>using</c>으로 감싸면 <b>반드시</b> 치워진다.
        /// </summary>
        private sealed class Stage : IDisposable
        {
            public GameObject Root;

            /// <summary>이름을 <c>Camera</c>로 두지 않는다 — 타입명과 같으면 읽는 사람이 매번 멈춘다.</summary>
            public Camera Cam;

            private Scene _scene;
            private GameObject _cameraGo;
            private bool _sceneOpened;

            public static Stage Open(Settings settings)
            {
                var stage = new Stage();
                stage._scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                stage._sceneOpened = true;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);
                if (prefab == null)
                {
                    stage.Dispose();
                    throw new FileNotFoundException(
                        PrefabAssetPath + "이(가) 없다 — 먼저 [StickMate/Build All]로 프리팹을 구워라.");
                }

                stage.Root = PrefabUtility.InstantiatePrefab(prefab, stage._scene) as GameObject;
                if (stage.Root == null)
                {
                    stage.Dispose();
                    throw new InvalidOperationException("프리팹 인스턴스 생성 실패: " + PrefabAssetPath);
                }
                stage.Root.transform.position = new Vector3(StageWorldX, 0f, 0f);

                MakeStillLife(stage.Root, settings);

                stage._cameraGo = new GameObject("StickmanIconCamera");
                SceneManager.MoveGameObjectToScene(stage._cameraGo, stage._scene);
                stage.Cam = stage._cameraGo.AddComponent<Camera>();
                Camera cam = stage.Cam;
                cam.enabled = false;                    // 매 프레임 그리지 않는다. Render()로만 부른다.
                cam.orthographic = true;
                cam.aspect = 1f;                        // 사양 §4-4
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Opaque(settings.PlateColor);
                cam.allowMSAA = false;                  // 사양 §6-2 4-a — MSAA 금지
                cam.allowHDR = false;
                cam.allowDynamicResolution = false;
                cam.useOcclusionCulling = false;
                cam.cullingMask = ~0;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 100f;
                cam.depthTextureMode = DepthTextureMode.None;
                return stage;
            }

            /// <summary>
            /// 사양 §6-2 2단계 — "정물"로 만든다.
            /// <para>★ <c>transform</c>을 한 톨도 안 건드린다. 자세는 저장된 그대로다.</para>
            /// </summary>
            private static void MakeStillLife(GameObject root, Settings settings)
            {
                foreach (var rb in root.GetComponentsInChildren<Rigidbody2D>(true)) rb.simulated = false;
                foreach (var col in root.GetComponentsInChildren<Collider2D>(true)) col.enabled = false;
                foreach (var joint in root.GetComponentsInChildren<Joint2D>(true)) joint.enabled = false;

                // ★ 곡선(무릎/팔꿈치 필렛)을 실제로 적용된 점 목록으로 만든다.
                //   사양 §6-2는 "LimbCurveRenderer를 켠 채로 한 번 LateUpdate를 태운다"고 적었는데,
                //   <b>에디터 모드에는 LateUpdate가 없다</b>(Awake도 안 돈다). 그래서 이 저장소가 이미
                //   같은 목적으로 두고 있는 공개 API를 그대로 부른다 — SceneBootstrapper가 프리팹을
                //   구울 때 부르는 것과 같은 호출이라, 런타임과 완전히 같은 코드 경로다.
                foreach (var limb in root.GetComponentsInChildren<LimbCurveRenderer>(true))
                {
                    limb.BakeEditorPreview();
                }

                // 그 뒤 전부 끈다. 아이콘은 세이브 상태·DLC 보유 여부에 무관해야 하므로(사양 §4-1)
                // 장비/펫/FX/말풍선 렌더러가 하나라도 살아 있으면 안 된다.
                // ★ 이름으로 열거하지 않고 <b>전부</b> 끈다 — Director가 늘어나도 새는 구멍이 안 생긴다.
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    mb.enabled = false;
                }

                if (settings.InkColorOverride.HasValue)
                {
                    Color ink = Opaque(settings.InkColorOverride.Value);
                    foreach (var lr in root.GetComponentsInChildren<LineRenderer>(true))
                    {
                        lr.startColor = ink;
                        lr.endColor = ink;
                    }
                }
            }

            /// <summary>카메라를 티어에 맞춰 세운다(사양 §4-4 유도식).</summary>
            public void PrepareCamera(Rect ink, float tierSmallBottomY, bool tierSmall, Settings settings)
            {
                ResolveFraming(ink, tierSmallBottomY, tierSmall, settings.FrameFillOverride,
                    out Vector2 center, out float orthoSize);
                Cam.orthographicSize = orthoSize;
                Cam.transform.position = new Vector3(StageWorldX + center.x, center.y, -10f);
                Cam.transform.rotation = Quaternion.identity;
                Cam.backgroundColor = Opaque(settings.PlateColor);
            }

            /// <summary>사양 §6-2 4단계 a~e. 파일은 쓰지 않는다.</summary>
            public Tile CaptureTile(int size, Rect ink, float tierSmallBottomY, bool tierSmall, Settings settings)
            {
                PrepareCamera(ink, tierSmallBottomY, tierSmall, settings);
                ResolveFraming(ink, tierSmallBottomY, tierSmall, settings.FrameFillOverride,
                    out Vector2 center, out float orthoSize);

                int ss = size * Supersample;
                RenderTexture rt = null;
                RenderTexture previousActive = RenderTexture.active;
                Texture2D full = null;
                try
                {
                    var desc = new RenderTextureDescriptor(ss, ss, RenderTextureFormat.ARGB32, 24)
                    {
                        msaaSamples = 1,                 // ★ MSAA 금지(사양 §6-2 4-a)
                        sRGB = false,                    // 감마 색공간 프로젝트 — 변환을 끼우지 않는다
                        useMipMap = false,
                        autoGenerateMips = false,
                    };
                    rt = RenderTexture.GetTemporary(desc);
                    rt.filterMode = FilterMode.Point;

                    Cam.targetTexture = rt;
                    Cam.Render();
                    Cam.targetTexture = null;

                    RenderTexture.active = rt;
                    full = new Texture2D(ss, ss, TextureFormat.RGBA32, false, true);
                    full.ReadPixels(new Rect(0, 0, ss, ss), 0, 0, false);
                    full.Apply(false, false);
                }
                finally
                {
                    RenderTexture.active = previousActive;
                    if (rt != null) RenderTexture.ReleaseTemporary(rt);
                }

                Color32[] hi = full.GetPixels32();
                UnityEngine.Object.DestroyImmediate(full);

                // (d) 박스 다운샘플 8×8 → 1. Graphics.Blit 의 쌍선형에 맡기지 않는다 —
                //     8배 축소에서 쌍선형 커널은 4탭뿐이라 64샘플 중 60개를 버린다(사양 §6-2 4-d).
                Color32[] lo = BoxDownsample(hi, ss, ss, Supersample);

                // (e) 모서리 둥근 사각형 알파 마스크를 <b>슈퍼샘플 해상도에서</b> 계산해 같은 커널로
                //     내린 뒤 곱한다. S 해상도에서 만들면 코너가 계단이 된다(사양 §6-2 4-e).
                float[] mask = RoundedRectMask(size, ss, settings.CornerRadiusFactor);
                ApplyAlphaMask(lo, mask);

                if (settings.BorderWidthPx > 0f)
                {
                    // design-art 미확정 — 기본은 사양대로 "테두리 없음"(BorderWidthPx = 0).
                    DrawInsetBorder(lo, size, ss, settings);
                }

                return new Tile
                {
                    Size = size,
                    TierSmall = tierSmall,
                    Pixels = lo,
                    CameraCenter = center,
                    OrthoSize = orthoSize,
                };
            }

            public void Dispose()
            {
                if (_cameraGo != null) UnityEngine.Object.DestroyImmediate(_cameraGo);
                if (Root != null) UnityEngine.Object.DestroyImmediate(Root);
                // ★ 저장하지 않고 닫는다. 프리팹 에셋도, 사용자가 열어 둔 씬도 그대로다.
                if (_sceneOpened && _scene.IsValid()) EditorSceneManager.CloseScene(_scene, true);
                _sceneOpened = false;
            }
        }

        /// <summary>
        /// 사양 §4-4의 유도식을 코드로 한 번만 적는다:
        /// <c>orthographicSize = 크롭높이 / (2·f)</c>, 중심 = 크롭 사각형의 중심.
        /// <para>가로는 <b>잉크 사각형을 좌우 가운데</b>에 둔다(사양 §4-3). 티어 S도 같은 x를 쓴다 —
        /// 팔이 엉덩이보다 위에 있어서 반신 크롭의 가로 범위가 전신과 같기 때문이다.</para>
        /// </summary>
        public static void ResolveFraming(Rect ink, float tierSmallBottomY, bool tierSmall, float frameFill,
            out Vector2 center, out float orthoSize)
        {
            float bottom = tierSmall ? tierSmallBottomY : ink.yMin;
            float cropHeight = ink.yMax - bottom;
            orthoSize = cropHeight / (2f * frameFill);
            center = new Vector2((ink.xMin + ink.xMax) * 0.5f, (bottom + ink.yMax) * 0.5f);
        }

        // ====================================================================
        // 픽셀 유틸
        // ====================================================================

        /// <summary>정수배 박스 다운샘플. 합을 int로 누적한다(감마 값 그대로 평균 — 사양 §9의 오프라인 모델과 같다).</summary>
        public static Color32[] BoxDownsample(Color32[] src, int srcW, int srcH, int factor)
        {
            int dw = srcW / factor, dh = srcH / factor;
            var dst = new Color32[dw * dh];
            int n = factor * factor;
            for (int y = 0; y < dh; y++)
            {
                for (int x = 0; x < dw; x++)
                {
                    int r = 0, g = 0, b = 0, a = 0;
                    int y0 = y * factor, x0 = x * factor;
                    for (int j = 0; j < factor; j++)
                    {
                        int rowBase = (y0 + j) * srcW + x0;
                        for (int i = 0; i < factor; i++)
                        {
                            Color32 c = src[rowBase + i];
                            r += c.r; g += c.g; b += c.b; a += c.a;
                        }
                    }
                    dst[y * dw + x] = new Color32(
                        (byte)((r + n / 2) / n), (byte)((g + n / 2) / n),
                        (byte)((b + n / 2) / n), (byte)((a + n / 2) / n));
                }
            }
            return dst;
        }

        /// <summary>
        /// ★ G0 전용 — 임의 배율 면적 평균 축소. "그냥 256만 넣고 Windows가 축소하게 두자"를 재현한다.
        /// <b>본편 타일에는 절대 쓰지 마라</b>(사양 §3-3: 그것이 측정상 최악이다).
        /// </summary>
        public static Tile AreaResample(Tile src, int targetSize)
        {
            int sw = src.Size;
            var dst = new Color32[targetSize * targetSize];
            double ratio = (double)sw / targetSize;
            for (int y = 0; y < targetSize; y++)
            {
                double y0 = y * ratio, y1 = (y + 1) * ratio;
                for (int x = 0; x < targetSize; x++)
                {
                    double x0 = x * ratio, x1 = (x + 1) * ratio;
                    double r = 0, g = 0, b = 0, a = 0, wsum = 0;
                    int iy0 = (int)Math.Floor(y0), iy1 = (int)Math.Ceiling(y1);
                    int ix0 = (int)Math.Floor(x0), ix1 = (int)Math.Ceiling(x1);
                    for (int sy = iy0; sy < iy1 && sy < sw; sy++)
                    {
                        double wy = Math.Min(y1, sy + 1) - Math.Max(y0, sy);
                        if (wy <= 0) continue;
                        for (int sx = ix0; sx < ix1 && sx < sw; sx++)
                        {
                            double wx = Math.Min(x1, sx + 1) - Math.Max(x0, sx);
                            if (wx <= 0) continue;
                            double w = wx * wy;
                            Color32 c = src.Pixels[sy * sw + sx];
                            r += c.r * w; g += c.g * w; b += c.b * w; a += c.a * w; wsum += w;
                        }
                    }
                    if (wsum <= 0) wsum = 1;
                    dst[y * targetSize + x] = new Color32(
                        (byte)Mathf.Clamp((int)Math.Round(r / wsum), 0, 255),
                        (byte)Mathf.Clamp((int)Math.Round(g / wsum), 0, 255),
                        (byte)Mathf.Clamp((int)Math.Round(b / wsum), 0, 255),
                        (byte)Mathf.Clamp((int)Math.Round(a / wsum), 0, 255));
                }
            }
            return new Tile
            {
                Size = targetSize,
                TierSmall = src.TierSmall,
                Pixels = dst,
                CameraCenter = src.CameraCenter,   // 프레이밍은 그대로다 — 해상도만 떨어뜨렸다
                OrthoSize = src.OrthoSize,
                FileStem = "_positive_control",
            };
        }

        /// <summary>
        /// 사양 §4-5의 모서리 둥근 정사각형 알파 마스크. 슈퍼샘플 해상도에서 계산해 같은 커널로 내린다.
        /// <para>반경은 <c>r = 계수 · S</c>, 단 <b>S ≤ 24는 정수로 반올림</b>(16→3, 20→4, 24→5).
        /// ★ 20 → 3.75 와 24 → 4.5 는 <b>away-from-zero</b> 반올림이어야 사양 표(4, 5)와 맞는다.
        /// <c>Mathf.Round</c>는 은행가 반올림이라 4.5를 4로 내린다 — 여기서 쓰면 안 된다.</para>
        /// </summary>
        public static float[] RoundedRectMask(int size, int ssSize, float cornerRadiusFactor)
        {
            var mask = new float[size * size];
            if (cornerRadiusFactor <= 0f)
            {
                for (int i = 0; i < mask.Length; i++) mask[i] = 1f;
                return mask;
            }

            float rTile = cornerRadiusFactor * size;
            if (size <= 24) rTile = (float)Math.Round(rTile, MidpointRounding.AwayFromZero);

            int ss = ssSize / size;
            float r = rTile * ss;                 // 슈퍼샘플 좌표계의 반경
            float side = ssSize;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int inside = 0;
                    for (int j = 0; j < ss; j++)
                    {
                        float py = y * ss + j + 0.5f;
                        for (int i = 0; i < ss; i++)
                        {
                            float px = x * ss + i + 0.5f;
                            if (InsideRoundedRect(px, py, side, r)) inside++;
                        }
                    }
                    mask[y * size + x] = inside / (float)(ss * ss);
                }
            }
            return mask;
        }

        private static bool InsideRoundedRect(float px, float py, float side, float r)
        {
            float dx = Mathf.Max(Mathf.Max(r - px, px - (side - r)), 0f);
            float dy = Mathf.Max(Mathf.Max(r - py, py - (side - r)), 0f);
            return dx * dx + dy * dy <= r * r;
        }

        /// <summary>알파만 곱한다. RGB는 건드리지 않는다 — 게이트가 RGB로 잉크 피복률을 재기 때문이다.</summary>
        public static void ApplyAlphaMask(Color32[] pixels, float[] mask)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 c = pixels[i];
                c.a = (byte)Mathf.Clamp(Mathf.RoundToInt(c.a * mask[i]), 0, 255);
                pixels[i] = c;
            }
        }

        /// <summary>
        /// design-art 판정 대기 항목(사양 §4-5 "테두리 선: 없음"). 기본은 안 그린다.
        /// 켜면 플레이트 안쪽 <paramref name="settings"/>.BorderWidthPx 만큼을 테두리 색으로 덮는다.
        /// </summary>
        private static void DrawInsetBorder(Color32[] pixels, int size, int ssSize, Settings settings)
        {
            float rTile = settings.CornerRadiusFactor * size;
            if (size <= 24) rTile = (float)Math.Round(rTile, MidpointRounding.AwayFromZero);
            int ss = ssSize / size;
            float outerR = rTile * ss;
            float side = ssSize;
            float inset = settings.BorderWidthPx * ss;
            Color32 border = Opaque(settings.BorderColor);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int onBorder = 0;
                    for (int j = 0; j < ss; j++)
                    {
                        float py = y * ss + j + 0.5f;
                        for (int i = 0; i < ss; i++)
                        {
                            float px = x * ss + i + 0.5f;
                            bool inOuter = InsideRoundedRect(px, py, side, outerR);
                            bool inInner = px >= inset && py >= inset && px <= side - inset && py <= side - inset
                                           && InsideRoundedRect(px - inset, py - inset, side - 2f * inset,
                                                Mathf.Max(0f, outerR - inset));
                            if (inOuter && !inInner) onBorder++;
                        }
                    }
                    if (onBorder == 0) continue;
                    float t = onBorder / (float)(ss * ss);
                    int idx = y * size + x;
                    Color32 c = pixels[idx];
                    pixels[idx] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Lerp(c.r, border.r, t)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(c.g, border.g, t)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(c.b, border.b, t)),
                        c.a);
                }
            }
        }

        private static Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);

        // ====================================================================
        // 파일 출력
        // ====================================================================

        private static void WriteTiles(string repoRoot, Settings settings, List<Tile> tiles, string folderOverride = null)
        {
            string folder = Path.Combine(repoRoot, folderOverride ?? settings.OutputFolder);
            Directory.CreateDirectory(folder);
            foreach (Tile t in tiles)
            {
                if (t == null) continue;
                var tex = new Texture2D(t.Size, t.Size, TextureFormat.RGBA32, false, true);
                tex.SetPixels32(t.Pixels);
                tex.Apply(false, false);
                byte[] png = tex.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(tex);
                File.WriteAllBytes(Path.Combine(folder, t.FileStem + ".png"), png);
            }
        }

        /// <summary>
        /// 사양 §6-2 5단계 — <c>.ico</c> 조립.
        /// 16~128 = 32bpp BGRA(+ AND 마스크), 256 = PNG 압축 임베드.
        /// <para>★ 이 파일을 만드는 것과 <c>ProjectSettings.m_BuildTargetIcons</c>에 <b>배선</b>하는 것은
        /// 다른 일이다. 배선은 여기서 하지 않는다.</para>
        /// <para>★ 그리고 <c>.ico</c>가 생겼다고 <b>exe에 들어갔다는 뜻이 아니다</b> — 그건 G6이고
        /// 빌드 산출물의 그룹 아이콘 리소스를 직접 열거해야 안다(사양 §8 G6: "빌드 날짜로 판단하지 마라").</para>
        /// </summary>
        public static void WriteIco(string absolutePath, List<Tile> tiles)
        {
            string dir = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var entries = new List<byte[]>();
            var descriptors = new List<int[]>();   // {width, height}

            foreach (Tile t in tiles)
            {
                if (t == null) continue;
                if (t.Size >= PngEmbedTile) entries.Add(EncodePngForIco(t));
                else entries.Add(EncodeBmpForIco(t));
                descriptors.Add(new[] { t.Size, t.Size });
            }

            int count = entries.Count;
            int offset = 6 + 16 * count;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write((ushort)0);        // reserved
                w.Write((ushort)1);        // type = icon
                w.Write((ushort)count);
                for (int i = 0; i < count; i++)
                {
                    int side = descriptors[i][0];
                    w.Write((byte)(side >= 256 ? 0 : side));   // 256은 0으로 적는다(ICO 규격)
                    w.Write((byte)(side >= 256 ? 0 : side));
                    w.Write((byte)0);      // 팔레트 색 수 = 0 (32bpp)
                    w.Write((byte)0);      // reserved
                    w.Write((ushort)1);    // planes
                    w.Write((ushort)32);   // bit count
                    w.Write(entries[i].Length);
                    w.Write(offset);
                    offset += entries[i].Length;
                }
                for (int i = 0; i < count; i++) w.Write(entries[i]);
                File.WriteAllBytes(absolutePath, ms.ToArray());
            }
        }

        private static byte[] EncodePngForIco(Tile t)
        {
            var tex = new Texture2D(t.Size, t.Size, TextureFormat.RGBA32, false, true);
            tex.SetPixels32(t.Pixels);
            tex.Apply(false, false);
            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            return png;
        }

        /// <summary>BITMAPINFOHEADER + 32bpp BGRA XOR(아래에서 위로) + AND 마스크.</summary>
        private static byte[] EncodeBmpForIco(Tile t)
        {
            int s = t.Size;
            int andRowBytes = ((s + 31) / 32) * 4;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(40);                    // biSize
                w.Write(s);                     // biWidth
                w.Write(s * 2);                 // biHeight = XOR + AND (ICO 규격)
                w.Write((ushort)1);             // biPlanes
                w.Write((ushort)32);            // biBitCount
                w.Write(0);                     // biCompression = BI_RGB
                w.Write(s * s * 4 + andRowBytes * s); // biSizeImage
                w.Write(0); w.Write(0); w.Write(0); w.Write(0);

                // XOR: 아래에서 위로. t.Pixels 도 아래에서 위로 저장돼 있으므로 그대로 흘린다.
                for (int y = 0; y < s; y++)
                {
                    for (int x = 0; x < s; x++)
                    {
                        Color32 c = t.Pixels[y * s + x];
                        w.Write(c.b); w.Write(c.g); w.Write(c.r); w.Write(c.a);
                    }
                }
                // AND 마스크: 전부 0(= 불투명으로 취급). 32bpp 아이콘은 알파 채널이 실제 투명도를 정하고
                // AND 마스크는 구식 셸 폴백용이다. 모서리는 알파로 이미 0이다.
                var zeros = new byte[andRowBytes];
                for (int y = 0; y < s; y++) w.Write(zeros);
                return ms.ToArray();
            }
        }

        private static void WriteReport(string repoRoot, Settings settings, BakeOutput output,
            StickmanIconGates.Report report)
        {
            string folder = Path.Combine(repoRoot, settings.OutputFolder);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "bake_report.txt"), report.Render(settings, output));
        }

        internal static string RepoRoot() => Directory.GetParent(Application.dataPath).FullName;
    }
}
