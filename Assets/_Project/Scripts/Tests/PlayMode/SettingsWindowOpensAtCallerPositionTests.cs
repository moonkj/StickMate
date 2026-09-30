using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★ <b>정보창에서 [설정]을 누르면 설정창이 그 자리에서 열린다</b> — 2026-09-30 사용자 확정:
    /// <i>"설정창은 현재 열려있는 창(=정보창)의 위치에서 열려야 한다"</i>.
    ///
    /// ============================================================================
    /// 이 파일이 재는 축 — <b>배선</b>
    /// ============================================================================
    /// 가지를 고르는 순수 함수의 진리표는 EditMode
    /// (<c>SettingsWindowCallerPlacementTests</c>)가 잰다. 여기서 재는 것은 그 함수에
    /// <b>무엇이 흘러들어가는가</b>다:
    ///  ① 정보창 [설정] 핸들러가 <b>자기 패널 중심</b>을 실제로 넘기는가.
    ///  ② 부른 창이 없는 진입점(전역 단축키 ⌃⌥⌘P · 트레이 · 자동 복귀)이 <b>안 바뀌었는가</b>(음성 대조).
    ///  ③ 물려받은 자리도 <b>기존</b> 화면 안 고정을 통과하는가.
    ///  ④ 물려받은 자리가 설정창의 <b>저장 슬롯을 오염시키지 않는가</b>.
    ///
    /// ============================================================================
    /// ★ 배치 화면(640×480)의 함정 — 무엇을 좌표로 재고 무엇을 못 재는가
    /// ============================================================================
    /// 설정창은 <b>720×560 고정</b>이고 정보창은 이 화면에서 608pt로 <b>줄어든다</b>. 두 창 다
    /// 가로 여유가 0이라 클램프가 모든 희망값을 한 자리로 못박고, 그 상태에서 최종
    /// <c>PanelOffsetPoints</c>만 보면 <b>"물려받았다"와 "아무 일도 안 일어났다"가 똑같이 생긴다</b>
    /// (<see cref="WindowDragPersistenceTests"/>가 같은 함정을 문서로 남겼다). 그래서 판정을 지는 것은
    /// <b>클램프 전 희망값과 가지 이름</b>(<c>SettingsWindow.LastOpenDesiredCenterPoints</c> ·
    /// <c>LastOpenPlacement</c>)이고, 좌표 일치가 이 화면에서 공허해지면 그 사실을 <b>로그로 남긴다</b>
    /// (조용히 통과시키지 않는다).
    ///
    /// <para>★ 그리고 <b>[설정] 칩은 이 화면에서 접힌다</b>(<c>SyncHeaderChips</c> — 좁은 창에서
    /// 탭 스트립을 침범하면 꺼진다. 2026-09-27에 다른 스위트가 18회 연속 «판정 불가»였던 그 원인이다).
    /// 그래서 좌표 클릭을 <b>1순위로 시도하고</b>, 사각형이 비어 있으면 같은 핸들러를 부르는 문
    /// (<c>CharacterInfoWindow.OpenSettingsForTests</c>)으로 떨어진다 — 어느 경로를 썼는지 로그에 찍는다.
    /// <b><c>settings.Open(source)</c>를 손으로 대신 적지 않는다</b>: 그 대체는 2026-09-30부로
    /// 핸들러와 같은 호출이 아니게 됐고, 손으로 베낀 대체는 반드시 그렇게 낡는다.</para>
    /// </summary>
    public sealed class SettingsWindowOpensAtCallerPositionTests
    {
        private const string LogPrefix = "[설정창자리-PLAY]";

        /// <summary>합성 «부른 창의 자리». 0이 아니어야 기록기가 상수를 찍는지 가를 수 있다.</summary>
        private static readonly Vector2 SyntheticCaller = new Vector2(137f, -91f);

        /// <summary>합성 «이 창을 옮겨 둔 자리». 위와 달라야 두 가지를 가를 수 있다.</summary>
        private static readonly Vector2 SyntheticSaved = new Vector2(-64f, 41f);

        private CharacterInfoWindow _info;
        private SettingsWindow _settings;

        [OneTimeSetUp]
        public void RequireIsolatedSaveFileAndStartClean()
        {
            Assert.IsTrue(CharacterSaveStore.IsRedirectedForTesting,
                $"{LogPrefix} 저장 경로가 격리되지 않았습니다 — GlobalPlayModeTestIsolation이 돌지 " +
                "않았습니다. 이대로 진행하면 개발자의 실제 저장 파일을 읽고 씁니다(절대 불변 원칙 3).");
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
        }

        [OneTimeTearDown]
        public void ClearIsolatedSaveFile()
        {
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
            UiLayoutModel.ResetForTesting();
        }

        [SetUp]
        public void ResetLayout()
        {
            // 「옮긴 적 있음」이 남아 있으면 이 파일의 음성 대조가 통째로 다른 가지를 잰다.
            UiLayoutModel.ResetForTesting();
        }

        [UnityTearDown]
        public IEnumerator CloseEverything()
        {
            if (_settings != null && _settings.IsOpen) _settings.Close("테스트 정리");
            if (_info != null && _info.IsOpen) _info.Close("테스트 정리");
            _settings = null;
            _info = null;
            yield return null;
        }

        private IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _info = Object.FindFirstObjectByType<CharacterInfoWindow>();
            _settings = Object.FindFirstObjectByType<SettingsWindow>();
            Assert.IsNotNull(_info, $"{LogPrefix} 씬에 CharacterInfoWindow가 없습니다.");
            Assert.IsNotNull(_settings, $"{LogPrefix} 씬에 SettingsWindow가 없습니다 — " +
                "SceneBootstrapper.EnsurePrefabComponents가 프리팹에 붙였는지 확인하세요.");
        }

        // ====================================================================================
        // ① 정보창 [설정] → 설정창이 정보창 중심을 목표로 삼는다 (+ 저장 슬롯을 오염시키지 않는다)
        // ====================================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator SettingsInheritsTheInfoWindowCenterWhenOpenedFromItsHeaderChip()
        {
            yield return LoadScene();

            Assert.IsFalse(UiLayoutModel.HasWindowOffset(UiWindowId.Settings),
                $"{LogPrefix} 전제 실패 — 시작 시점에 설정창이 이미 «옮긴 적 있음»입니다([SetUp] 격리가 " +
                "깨졌습니다). 그 상태에서는 아래 단언이 «물려받았다»와 «자기 저장값을 썼다»를 못 가릅니다.");

            _info.Toggle("테스트 — 정보창을 먼저 연다");
            yield return null;
            yield return null;
            Assert.IsTrue(_info.IsOpen, $"{LogPrefix} 전제: 정보창이 열려야 합니다.");

            Vector2 infoCenter = _info.PanelOffsetPoints;

            // ---- 실제 경로로 누른다. 좌표 클릭이 1순위, 칩이 접혀 있으면 같은 핸들러 문으로 ----
            Rect chip = _info.SettingsChipScreenRect;
            bool byCoordinate = chip.width > 1f && chip.height > 1f;
            if (byCoordinate)
            {
                _info.FeedClickForTests(chip.center);
            }
            else
            {
                Assert.IsTrue(_info.OpenSettingsForTests("정보창 헤더 [설정]"),
                    $"{LogPrefix} 핸들러 문이 false를 냈습니다 — 정보창이 닫혀 있습니다(전제 붕괴).");
            }
            yield return null;

            Debug.Log($"{LogPrefix} 경로={(byCoordinate ? "좌표 클릭" : "핸들러 문(칩이 접힘)")} · " +
                $"게임 뷰 {Screen.width}×{Screen.height} · [설정] 칩 폭={chip.width:F1}px · " +
                $"정보창 중심={infoCenter} · 정보창 크기={_info.PanelSizePoints}.");

            Assert.IsTrue(_settings.IsOpen,
                $"{LogPrefix} [설정]을 눌렀는데 설정창이 열리지 않았습니다.");

            // ---- 본 판정 ① — 어느 가지를 탔는가(클램프가 좌표를 못박는 화면에서도 유효하다) ----
            Assert.AreEqual(SettingsWindow.OpenPlacement.CallerCenter, _settings.LastOpenPlacement,
                $"{LogPrefix} ★ 설정창이 «부른 창의 자리» 가지를 타지 않았습니다(실제 {_settings.LastOpenPlacement}). " +
                "정보창 헤더 [설정]으로 열었는데 그 창의 자리를 묻지 않은 것이고, 그것이 " +
                "2026-09-30 사용자 신고의 정확한 형태입니다.");

            // ---- 본 판정 ② — 넘어온 값이 정보창의 <b>지금</b> 중심인가 ----
            Assert.AreEqual(infoCenter.x, _settings.LastOpenDesiredCenterPoints.x, 0.01f,
                $"{LogPrefix} 희망 중심 x가 정보창 중심과 다릅니다(정보창 {infoCenter} / " +
                $"설정창 희망 {_settings.LastOpenDesiredCenterPoints}).");
            Assert.AreEqual(infoCenter.y, _settings.LastOpenDesiredCenterPoints.y, 0.01f,
                $"{LogPrefix} 희망 중심 y가 정보창 중심과 다릅니다(정보창 {infoCenter} / " +
                $"설정창 희망 {_settings.LastOpenDesiredCenterPoints}).");

            // ★ 공허함을 <b>조용히 넘기지 않는다</b> — 이 화면에서 정보창 중심이 0이면 위 두 단언은
            //   기본값(화면 중앙)과 구별되지 않는다. 그때 판정을 지는 것은 위 가지 이름과 아래 변이
            //   대조이고, 그 사실을 로그에 남겨 다음 사람이 «좌표를 재고 있다»고 착각하지 않게 한다.
            if (Mathf.Approximately(infoCenter.x, 0f) && Mathf.Approximately(infoCenter.y, 0f))
            {
                Debug.Log($"{LogPrefix} ⚠ 이 화면에서 정보창 중심이 (0,0)이라 <b>좌표 일치 단언은 공허</b>합니다 " +
                    "— 창이 화면보다 커서 클램프가 한 자리로 못박기 때문입니다. 판정을 지는 것은 " +
                    "위 «가지 이름»과 아래 «변이 대조» 둘입니다.");
            }

            // ---- 변이 대조 — 기록기가 인자를 실제로 따라가는가(상수를 찍는 것이 아닌가) ----
            _settings.Close("테스트 — 변이 대조 준비");
            yield return null;
            Vector2 mutated = infoCenter + SyntheticCaller;
            _settings.Open("테스트 — 변이 대조(합성 부른 창 자리)", mutated);
            yield return null;
            Assert.AreEqual(mutated.x, _settings.LastOpenDesiredCenterPoints.x, 0.01f,
                $"{LogPrefix} 변이 대조 실패 — 다른 값을 넘겼는데 희망 중심 x가 따라오지 않았습니다. " +
                "위 좌표 단언은 «기록기가 상수를 찍는다»와 구별되지 않습니다.");
            Assert.AreEqual(mutated.y, _settings.LastOpenDesiredCenterPoints.y, 0.01f,
                $"{LogPrefix} 변이 대조 실패 — 희망 중심 y가 따라오지 않았습니다.");

            // ---- 본 판정 ③ — 물려받은 자리가 설정창의 저장 슬롯을 오염시키지 않는다 ----
            Assert.IsFalse(UiLayoutModel.HasWindowOffset(UiWindowId.Settings),
                $"{LogPrefix} ★ 물려받은 자리가 설정창의 «옮긴 적 있음»으로 앉았습니다. 그러면 그 뒤로는 " +
                "전역 단축키로 열어도 정보창이 있던 자리에서 열리고, <b>사용자가 만들지 않은 저장값</b>이 " +
                "디스크에 남습니다. 저장은 사용자가 이 창을 직접 끌었을 때만 일어나야 합니다.");

            Debug.Log($"{LogPrefix} ① 통과 — 가지={_settings.LastOpenPlacement}, " +
                $"희망 중심={_settings.LastOpenDesiredCenterPoints}, 실제={_settings.PanelOffsetPoints}, " +
                $"저장 슬롯 오염 없음.");
        }

        // ====================================================================================
        // ② 음성 대조 — 부른 창이 없는 진입점은 예전 그대로다
        // ====================================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator HotkeyAndTrayPathStillUseTheSettingsWindowsOwnPosition()
        {
            yield return LoadScene();

            Assert.IsFalse(_info.IsOpen,
                $"{LogPrefix} 전제: 정보창은 닫혀 있어야 합니다(부른 창이 없는 경로를 잽니다).");
            Assert.IsFalse(UiLayoutModel.HasWindowOffset(UiWindowId.Settings),
                $"{LogPrefix} 전제 실패 — 설정창이 이미 «옮긴 적 있음»입니다.");

            // ---- (가) 옮긴 적 없음 → 화면 중앙, <b>클램프도 태우지 않는다</b>(예전과 비트 동일) ----
            //    AppControlDirector.ToggleSettings → SettingsWindow.Toggle → Open(source)와 같은 문이다.
            _settings.Open("테스트 — 전역 단축키 경로(부른 창이 없다)");
            yield return null;
            Assert.IsTrue(_settings.IsOpen, $"{LogPrefix} 단축키 경로로 설정창이 열리지 않았습니다.");

            Assert.AreEqual(SettingsWindow.OpenPlacement.ScreenCenter, _settings.LastOpenPlacement,
                $"{LogPrefix} ★ 부른 창이 없는 경로가 «화면 중앙» 가지를 벗어났습니다" +
                $"(실제 {_settings.LastOpenPlacement}) — 2026-09-30 라운드는 이 가지를 건드리지 " +
                "않았어야 합니다.");
            Assert.AreEqual(0f, _settings.LastOpenDesiredCenterPoints.x, 0f,
                $"{LogPrefix} 희망 중심 x가 0이 아닙니다({_settings.LastOpenDesiredCenterPoints}).");
            Assert.AreEqual(0f, _settings.LastOpenDesiredCenterPoints.y, 0f,
                $"{LogPrefix} 희망 중심 y가 0이 아닙니다({_settings.LastOpenDesiredCenterPoints}).");
            Assert.AreEqual(0f, _settings.PanelOffsetPoints.x, 0f,
                $"{LogPrefix} ★ 실제 자리 x가 0이 아닙니다({_settings.PanelOffsetPoints}) — 옮긴 적 없는 " +
                "창에 클램프가 개입했습니다. 이 창은 720×560 고정이라 좁은 화면에서 클램프가 " +
                "«아무도 부탁하지 않은 이동»을 만듭니다(2026-09-07이 명시적으로 막은 회귀).");
            Assert.AreEqual(0f, _settings.PanelOffsetPoints.y, 0f,
                $"{LogPrefix} ★ 실제 자리 y가 0이 아닙니다({_settings.PanelOffsetPoints}) — 같은 이유입니다.");

            // ---- (나) 양성 대조 — 자기 저장값이 있으면 그 자리를 쓴다(창별 위치 기억은 살아 있다) ----
            _settings.Close("테스트 — 저장값 준비");
            yield return null;
            UiLayoutModel.SetWindowOffset(UiWindowId.Settings, SyntheticSaved);
            Assert.IsTrue(UiLayoutModel.HasWindowOffset(UiWindowId.Settings),
                $"{LogPrefix} 전제 실패 — 저장값을 심었는데 «옮긴 적 있음»이 서지 않았습니다.");

            _settings.Open("테스트 — 전역 단축키 경로(옮겨 둔 자리)");
            yield return null;
            Assert.AreEqual(SettingsWindow.OpenPlacement.OwnSavedCenter, _settings.LastOpenPlacement,
                $"{LogPrefix} ★ 창별 위치 기억이 죽었습니다(실제 {_settings.LastOpenPlacement}) — " +
                "각 창을 독립적으로 옮긴 뒤 재시작하면 각자 마지막 자리에서 열려야 합니다" +
                "(2026-09-07 사용자 요청 PART1-1, da71068). 그 기능은 없애면 안 됩니다.");
            Assert.AreEqual(SyntheticSaved.x, _settings.LastOpenDesiredCenterPoints.x, 0.01f,
                $"{LogPrefix} 저장한 x를 희망 중심으로 쓰지 않았습니다(저장 {SyntheticSaved} / " +
                $"희망 {_settings.LastOpenDesiredCenterPoints}).");
            Assert.AreEqual(SyntheticSaved.y, _settings.LastOpenDesiredCenterPoints.y, 0.01f,
                $"{LogPrefix} 저장한 y를 희망 중심으로 쓰지 않았습니다.");

            Assert.AreNotEqual(SettingsWindow.OpenPlacement.CallerCenter, _settings.LastOpenPlacement,
                $"{LogPrefix} 부른 창이 없는데 «부른 창의 자리» 가지가 나왔습니다 — 새 인자가 " +
                "기본값으로 새어 들어왔습니다.");

            Debug.Log($"{LogPrefix} ② 통과 — 단축키 경로는 (가) 화면 중앙 0 비트 동일 / " +
                $"(나) 저장값 {SyntheticSaved} 그대로입니다.");
        }

        // ====================================================================================
        // ③ 물려받은 자리도 <b>기존</b> 화면 안 고정을 통과한다
        // ====================================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator InheritedCenterStillGoesThroughTheExistingOnScreenClamp()
        {
            yield return LoadScene();

            // 화면 밖으로 한참 벗어난 «부른 창의 자리»를 넘긴다. 정보창이 화면 밖에 있던 경우를
            // 흉내내는 것이고, 클램프가 없으면 [✕]가 화면 밖인 <b>닫을 수 없는 창</b>이 만들어진다.
            var wild = new Vector2(9000f, 9000f);
            _settings.Open("테스트 — 화면 밖 자리를 물려받는다", wild);
            yield return null;
            yield return null;   // ApplyCanvasScaleFactor(매 프레임 되끌어오기)까지 한 번 돈다.

            Assert.IsTrue(_settings.IsOpen, $"{LogPrefix} 설정창이 열리지 않았습니다.");
            Assert.AreEqual(SettingsWindow.OpenPlacement.CallerCenter, _settings.LastOpenPlacement,
                $"{LogPrefix} 전제 실패 — 물려받기 가지를 타지 않았습니다.");

            // 희망값 기록은 <b>클램프 전</b>이어야 한다 — 클램프 뒤 값으로 덮으면 "얼마나 개입했는가"를
            // 영영 못 본다.
            Assert.AreEqual(wild.x, _settings.LastOpenDesiredCenterPoints.x, 0.01f,
                $"{LogPrefix} 희망 중심 기록이 클램프 뒤 값으로 덮였습니다" +
                $"({_settings.LastOpenDesiredCenterPoints}).");

            Vector2 applied = _settings.PanelOffsetPoints;

            // ---- 양성 대조 — 클램프가 <b>실제로</b> 개입했는가 ----
            Assert.AreNotEqual(wild.x, applied.x,
                $"{LogPrefix} ★ 화면 밖 x가 그대로 앉았습니다({applied}) — 물려받은 자리가 클램프를 " +
                "건너뛰었습니다. 창 밖 클릭이 닫지 않는 이 앱에서 그건 닫을 수 없는 창입니다.");
            Assert.AreNotEqual(wild.y, applied.y,
                $"{LogPrefix} ★ 화면 밖 y가 그대로 앉았습니다({applied}) — 같은 이유입니다.");

            // ---- 성질 검사 — 가로 한계는 단순 클램프라 여기서 직접 잰다(숫자를 베끼지 않는다) ----
            //    캔버스 배율은 관측값에서 되돌린다(패널 화면 폭 ÷ 패널 포인트 폭).
            Vector2 sizePoints = _settings.PanelSizePoints;
            Rect panel = _settings.PanelScreenRect;
            Assert.Greater(sizePoints.x, 0f, $"{LogPrefix} 패널 크기가 0입니다 — 배율을 되돌릴 수 없습니다.");
            Assert.Greater(panel.width, 0f, $"{LogPrefix} 패널 화면 사각형이 비었습니다.");
            float scale = panel.width / sizePoints.x;
            Assert.Greater(scale, 0f, $"{LogPrefix} 되돌린 캔버스 배율이 0 이하입니다({scale}).");

            float screenWidthPoints = Screen.width / scale;
            float maxX = Mathf.Max(0f, (screenWidthPoints - sizePoints.x) * 0.5f
                                       - UiWindowDrag.ScreenMarginPoints);
            Assert.LessOrEqual(Mathf.Abs(applied.x), maxX + 0.5f,
                $"{LogPrefix} 창 가로가 안전 영역을 벗어났습니다(중심 {applied.x}, 한계 ±{maxX}, " +
                $"화면 {screenWidthPoints:F0}pt, 창 {sizePoints.x:F0}pt).");

            // 세로는 «넘칠 때는 상단 우선»(SurfaceSafeAreaPolicy)이라 부호 있는 한계가 화면·예약 띠에
            // 따라 갈린다 — 그 식을 여기서 다시 적지 않는다(두 벌이 되면 한쪽만 낡는다).
            // 여기서 재는 것은 «제정신 범위로 돌아왔는가» 하나다.
            float screenHeightPoints = Screen.height / scale;
            Assert.LessOrEqual(Mathf.Abs(applied.y), screenHeightPoints,
                $"{LogPrefix} 창 세로가 화면 한 장 밖입니다(중심 {applied.y}, 화면 {screenHeightPoints:F0}pt).");

            Debug.Log($"{LogPrefix} ③ 통과 — {wild} → {applied}(가로 한계 ±{maxX:F1}pt, " +
                $"화면 {screenWidthPoints:F0}×{screenHeightPoints:F0}pt, 창 {sizePoints.x:F0}×{sizePoints.y:F0}pt).");
        }
    }
}
