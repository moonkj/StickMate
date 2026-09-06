using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 집중 모드 팝오버 <b>뽀모도로 다이얼 + 시간 자유 입력</b> 회귀 잠금 —
    /// docs/UX_WIDGETS.md §R5 (사용자 요청 2026-09-06:
    /// <i>"타이머를 첨부파일안 사진처럼 디자인 해주고 … 시간 설정을 고정된시간이 아닌 자율성있게"</i>).
    ///
    /// ============================================================================
    /// 이 파일이 <b>동작</b>으로 잠그는 네 가지
    /// ============================================================================
    ///  ① <b>눈금판 기하</b> — 숫자 12개가 정확히 θ(m) = m × 6°에, 같은 반경에 앉는다.
    ///     눈금은 긴 12 + 짧은 48 = 60개, 시작 표식 1개. 계산식이 틀리면 각도에서 걸린다.
    ///  ② <b>초가 매 프레임 넘어간다</b>(§R5-5-2). 절대 다이얼에서 호는 15fps 기준 프레임당
    ///     0.0038pt(1픽셀의 1/260)만 움직여 <b>사실상 정지 그림</b>이다. 그래서 진행 페이지에서
    ///     움직이는 것이 <c>mm:ss</c> 하나만 남고, 0.25초 느린 갱신의 톱니(15fps에서 간격이
    ///     1066.7 / 1066.7 / <b>800.0</b>ms로 갈라진다)가 숨을 곳이 없어진다.
    ///     ★ 이 검사는 <b>프레임 수가 아니라 벽시계</b>로 예산을 잡는다(CLAUDE.md).
    ///  ③ <b>[직접] 칩이 전역 폴링 경로에서 산다</b>(§R5-6 함정 ①). 옛 코드는 하나의 <c>for(i&lt;3)</c>가
    ///     두 종류의 칩을 함께 돌아서, 시간 칩이 4개가 되면 마지막 칩이 <b>이 경로에서만</b>
    ///     조용히 죽었다(uGUI Wire 경로로는 눌리므로 손으로 눌러 보면 멀쩡하다).
    ///  ④ <b>되감기 없음 · 세로 증가 0pt</b> — 끝에서 한 번 더 눌러도 반대 끝으로 튀지 않고,
    ///     자유 입력 행이 프리셋 행과 <b>같은 y·같은 높이</b>를 차지한다.
    ///
    /// <para><b>플랫폼</b>: 잠그는 성질이 전부 <c>Interaction/</c> uGUI 층이라 macOS/Windows 공통이다.
    /// <c>Platform/</c> 코드를 한 줄도 경유하지 않는다.</para>
    /// </summary>
    public sealed class FocusSessionDialTests
    {
        private const string LogPrefix = "[집중다이얼-TEST]";

        /// <summary>초 갱신을 관측하는 벽시계 예산(초). 최소 2번의 초 넘어감을 담아야 한다.</summary>
        private const float SecondsObservationBudget = 2.6f;

        private FocusSessionPopover _focus;
        private FocusWatchDirector _director;

        private IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _focus = Object.FindFirstObjectByType<FocusSessionPopover>();
            _director = Object.FindFirstObjectByType<FocusWatchDirector>();
            Assert.IsNotNull(_focus, $"{LogPrefix} 씬에 FocusSessionPopover가 없습니다.");
            Assert.IsNotNull(_director, $"{LogPrefix} 씬에 FocusWatchDirector가 없습니다.");
            if (_director.IsSessionActive) _director.StopFocusSession();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_director != null && _director.IsSessionActive) _director.StopFocusSession();
            if (_focus != null && _focus.IsOpen) _focus.Close("테스트 정리");
            _focus = null;
            _director = null;
            yield return null;
        }

        private void OpenAtScreenCenter()
        {
            var anchor = new Rect(Screen.width * 0.5f - 22f, Screen.height * 0.5f - 22f, 44f, 44f);
            _focus.Open(anchor, "테스트");
            Assert.IsTrue(_focus.IsOpen, $"{LogPrefix} 팝오버가 열리지 않았습니다.");
        }

        /// <summary>같은 손잡이를 연달아 누를 때 중복 방지 창이 클릭을 <b>먹는다</b> — 그만큼
        /// 벽시계로 기다린다. 값을 베끼지 않고 프로덕션 상수를 참조한다.</summary>
        private static WaitForSecondsRealtime WaitPastActionDedup()
            => new WaitForSecondsRealtime(PopoverPanel.ActionDedupSeconds + 0.05f);

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindDeep(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }

        /// <summary>부모(좌상단 앵커 배치) 안에서의 <b>다이얼 중심 기준</b> 오프셋. y는 위가 양수다.</summary>
        private static Vector2 DialOffsetOf(RectTransform child, RectTransform face)
        {
            Rect r = child.rect;
            Vector2 ap = child.anchoredPosition;
            // PlaceTopLeft 규약: 앵커/피벗 (0,1), x는 오른쪽 양수, y는 아래쪽 음수.
            float centerX = ap.x + r.width * 0.5f;
            float centerY = ap.y - r.height * 0.5f;
            return new Vector2(centerX - face.rect.width * 0.5f, centerY + face.rect.height * 0.5f);
        }

        // ==================== ① 눈금판 기하 ====================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator DialFaceIsDrawnAtSixDegreesPerMinuteOnOneRadius()
        {
            yield return LoadScene();
            _director.StartFocusSession(25f);
            OpenAtScreenCenter();
            yield return null;

            Assert.IsTrue(_focus.ShowingRunningPage, $"{LogPrefix} 세션을 시작했는데 진행 페이지가 아닙니다.");
            Assert.IsTrue(_focus.DialFaceVisible,
                $"{LogPrefix} 눈금판이 꺼져 있습니다 — 60분 한 바퀴 안의 세션인데 상대 링으로 물러났습니다.");

            GameObject canvasGo = GameObject.Find("FocusSessionPopoverCanvas");
            Assert.IsNotNull(canvasGo, $"{LogPrefix} FocusSessionPopoverCanvas를 찾지 못했습니다.");
            Transform faceTf = FindDeep(canvasGo.transform, "Face");
            Assert.IsNotNull(faceTf, $"{LogPrefix} 다이얼 눈금판(Face)을 찾지 못했습니다.");
            var face = (RectTransform)faceTf;

            int major = 0, minor = 0, markers = 0;
            var numbers = new List<KeyValuePair<int, Vector2>>();
            for (int i = 0; i < face.childCount; i++)
            {
                Transform child = face.GetChild(i);
                if (child.name == "TickMajor") { major++; continue; }
                if (child.name == "TickMinor") { minor++; continue; }
                if (child.name == "StartMarker") { markers++; continue; }
                var text = child.GetComponent<Text>();
                if (text == null || !int.TryParse(text.text, out int minute)) continue;
                numbers.Add(new KeyValuePair<int, Vector2>(minute, DialOffsetOf((RectTransform)child, face)));
            }

            Assert.AreEqual(12, major, $"{LogPrefix} 긴 눈금이 {major}개입니다 — 5분마다 하나로 12개여야 합니다.");
            Assert.AreEqual(48, minor, $"{LogPrefix} 짧은 눈금이 {minor}개입니다 — 60칸 중 긴 눈금 자리를 뺀 48개여야 합니다.");
            Assert.AreEqual(1, markers, $"{LogPrefix} 시작 표식이 {markers}개입니다 — 정확히 1개여야 합니다.");
            Assert.AreEqual(12, numbers.Count,
                $"{LogPrefix} 숫자 눈금판이 {numbers.Count}개입니다 — 0,5,…,55로 12개여야 합니다.");

            // 12개가 같은 반경 위에, θ(m) = m × 6°에 앉는가.
            float radius0 = numbers[0].Value.magnitude;
            Assert.Greater(radius0, 1f, $"{LogPrefix} 숫자가 다이얼 중심에 겹쳐 있습니다.");
            foreach (KeyValuePair<int, Vector2> entry in numbers)
            {
                Assert.Zero(entry.Key % 5,
                    $"{LogPrefix} 숫자 눈금판에 {entry.Key}이(가) 있습니다 — 5분 배수만 그립니다.");
                Assert.AreEqual(radius0, entry.Value.magnitude, 0.05f,
                    $"{LogPrefix} 숫자 {entry.Key}의 반경이 다른 숫자와 다릅니다 " +
                    $"({entry.Value.magnitude:F2} vs {radius0:F2}) — 눈금판이 타원이 됩니다.");

                // 12시 = 0°, 시계방향. atan2(x, y)가 정확히 그 정의다.
                float degrees = Mathf.Atan2(entry.Value.x, entry.Value.y) * Mathf.Rad2Deg;
                if (degrees < 0f) degrees += 360f;
                float expected = entry.Key * 6f;
                float error = Mathf.Abs(Mathf.DeltaAngle(degrees, expected));
                Assert.Less(error, 0.5f,
                    $"{LogPrefix} 숫자 {entry.Key}이(가) {degrees:F2}°에 있습니다(기대 {expected:F2}°). " +
                    "θ(m) = m × 6°가 깨졌습니다 — 호 끝이 가리키는 숫자가 남은 분이 아니게 됩니다(원칙 1).");
            }

            // 진행 페이지가 패널 밖으로 흘러나가지 않는가(R2-2-1이 잡은 함정, §R5-4-1에서 닫혔다).
            Rect panel = _focus.PanelScreenRect;
            Rect stop = _focus.StopButtonScreenRect;
            Assert.GreaterOrEqual(stop.yMin, panel.yMin - 0.5f,
                $"{LogPrefix} [그만두기]가 패널 아래로 {panel.yMin - stop.yMin:F1}px 삐져나갔습니다 — " +
                "Content 사각형은 Awake에 한 번만 만들어지므로 조용히 패널 밖에 그려집니다.");

            Debug.Log($"{LogPrefix} ① 통과 — 긴 눈금 {major} · 짧은 눈금 {minor} · 숫자 {numbers.Count} · " +
                $"시작 표식 {markers}, 반경 {radius0:F2}pt, 각도 오차 < 0.5°.");
        }

        // ==================== ② 초는 매 프레임 넘어간다 ====================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator SecondsAdvanceEveryFrameNotOnlyOnTheQuarterSecondTick()
        {
            yield return LoadScene();
            _director.StartFocusSession(25f);
            OpenAtScreenCenter();
            yield return null;

            Assert.IsTrue(_focus.ShowingRunningPage, $"{LogPrefix} 진행 페이지가 아닙니다.");
            Assert.AreNotEqual("--:--", _focus.TimeLabel, $"{LogPrefix} 여는 순간 mm:ss가 채워지지 않았습니다.");

            // ★ 벽시계 예산이다. 이 저장소의 배치모드 PlayMode는 수천 fps로 돌아 "N프레임" 예산은
            //   수 밀리초가 된다(CLAUDE.md). 반대로 에디터 GUI에서는 60fps라 프레임 수가 적다 —
            //   그래서 문턱도 프레임 수에 기대지 않고 <b>초 넘어감 횟수</b>에 건다.
            float deadline = Time.realtimeSinceStartup + SecondsObservationBudget;
            int frames = 0, mismatches = 0, secondChanges = 0;
            int previous = int.MinValue;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                int expected = Mathf.CeilToInt(Mathf.Max(0f, _director.RemainingSeconds));
                int shown = ParseMmSs(_focus.TimeLabel);
                frames++;
                if (previous != expected) { if (previous != int.MinValue) secondChanges++; previous = expected; }
                if (shown != expected) mismatches++;
            }

            // 네거티브 컨트롤 (a) — 초가 한 번도 안 넘어갔다면 이 검사는 어떤 구현에서도 통과한다.
            Assert.GreaterOrEqual(secondChanges, 2,
                $"{LogPrefix} {SecondsObservationBudget:F1}초 동안 초가 {secondChanges}번 넘어갔습니다 — " +
                "타이머가 멈춰 있으면 이 단언은 <b>빈 조건</b>입니다(거짓 초록).");
            Assert.Greater(frames, 30, $"{LogPrefix} 프레임 표본이 {frames}개뿐입니다 — 관측이 성립하지 않습니다.");

            // 매 프레임 판정이면 어긋나는 프레임은 "디렉터가 팝오버 뒤에 도는 프레임"뿐 —
            // 초 넘어감 1회당 최대 1프레임이다. 0.25초 느린 갱신에만 맡기면 매 초의 최대 0.25초,
            // 즉 <b>프레임의 12.5%</b>가 어긋난다(수백~수천 프레임). 두 세계는 자릿수가 다르다.
            Assert.LessOrEqual(mismatches, secondChanges + 3,
                $"{LogPrefix} {frames}프레임 중 {mismatches}프레임에서 화면의 mm:ss가 실제 남은 초와 " +
                $"달랐습니다(초 넘어감 {secondChanges}회). 초 판정이 다시 TickSlow(0.25초) 안으로 " +
                "들어갔습니다 — 절대 다이얼에서는 호가 프레임당 0.0038pt만 움직여 <b>움직이는 것이 " +
                "숫자 하나뿐</b>이라, 800ms 톱니가 숨을 곳이 없습니다(§R5-5-2).");

            Debug.Log($"{LogPrefix} ② 통과 — 벽시계 {SecondsObservationBudget:F1}초 / {frames}프레임, " +
                $"초 넘어감 {secondChanges}회, 불일치 {mismatches}프레임.");
        }

        private static int ParseMmSs(string text)
        {
            if (string.IsNullOrEmpty(text)) return -1;
            string[] parts = text.Split(':');
            if (parts.Length != 2) return -1;
            return int.TryParse(parts[0], out int m) && int.TryParse(parts[1], out int s) ? m * 60 + s : -1;
        }

        // ==================== ③ [직접] 칩이 전역 폴링 경로에서 산다 (함정 ①) ====================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator CustomDurationRowLivesOnTheGlobalClickPathAndClampsAtTheTop()
        {
            yield return LoadScene();
            OpenAtScreenCenter();
            yield return null;

            Assert.IsFalse(_focus.ShowingRunningPage, $"{LogPrefix} 대기 페이지여야 합니다.");
            Assert.IsFalse(_focus.ShowingCustomDurationRow,
                $"{LogPrefix} 팝오버가 자유 입력 행으로 열렸습니다 — 프리셋으로 열려야 " +
                "「프리셋 index 1 = 25분」 회귀 계약이 성립합니다(§R5-6 함정 ②).");

            Rect presetChip = _focus.DurationChipScreenRect(0);
            Assert.Greater(presetChip.width, 0f, $"{LogPrefix} 프리셋 칩을 찾지 못했습니다.");

            // 50분 프리셋을 고른 뒤 [직접]으로 들어간다 — 자유 입력은 <b>지금 고른 값에서 출발</b>한다.
            _focus.FeedClickForTests(_focus.DurationChipScreenRect(2).center);
            yield return null;
            Assert.AreEqual(50f, _focus.SelectedMinutes, 0.01f, $"{LogPrefix} 50분 프리셋이 안 골라졌습니다.");

            Rect customChip = _focus.DurationChipScreenRect(3);
            Assert.Greater(customChip.width, 0f,
                $"{LogPrefix} [직접] 칩이 없습니다 — 시간 칩이 아직 3개입니다(자유 입력이 안 들어왔습니다).");
            Assert.AreEqual(new Rect(), _focus.DurationChipScreenRect(4),
                $"{LogPrefix} 없는 인덱스 4가 사각형을 돌려줬습니다 — 범위 검사가 배열 길이를 세지 않습니다.");

            _focus.FeedClickForTests(customChip.center);
            yield return null;

            // ★★ 여기가 함정 ①이다. 옛 for(i<3) 루프에서는 이 클릭이 <b>아무 데도 닿지 않는다</b> —
            //    uGUI Wire 경로로는 눌리므로 손으로 눌러 보면 멀쩡해 보인다.
            Assert.IsTrue(_focus.ShowingCustomDurationRow,
                $"{LogPrefix} [직접] 칩을 <b>전역 폴링 경로</b>로 눌렀는데 자유 입력 행이 열리지 않았습니다 — " +
                "시간 칩 루프가 배열 길이가 아니라 상수 3에서 멈추고 있습니다(§R5-6 함정 ①).");
            Assert.AreEqual(50, _focus.CustomMinutesForTests,
                $"{LogPrefix} 자유 입력이 고른 프리셋(50분)에서 출발하지 않았습니다.");
            Assert.AreEqual(50f, _focus.SelectedMinutes, 0.01f, $"{LogPrefix} SelectedMinutes가 모드 분기를 안 합니다.");

            // 세로 증가 0pt — 같은 y·같은 높이의 행이 통째로 교체된다.
            Rect listChip = _focus.DurationListChipScreenRect;
            Assert.Greater(listChip.width, 0f, $"{LogPrefix} [목록] 칩을 찾지 못했습니다.");
            Assert.AreEqual(presetChip.yMin, listChip.yMin, 0.5f,
                $"{LogPrefix} 자유 입력 행이 프리셋 행과 다른 높이에 앉았습니다 — 세로 예산이 0pt 늘어야 합니다.");
            Assert.AreEqual(presetChip.height, listChip.height, 0.5f,
                $"{LogPrefix} 자유 입력 행의 높이가 프리셋 행과 다릅니다.");
            Assert.AreEqual(presetChip.xMin, listChip.xMin, 0.5f,
                $"{LogPrefix} 자유 입력 행의 왼쪽 끝이 프리셋 행과 어긋났습니다.");
            Assert.AreEqual(customChip.xMax, _focus.CustomStepScreenRect(5).xMax, 0.5f,
                $"{LogPrefix} 자유 입력 행의 오른쪽 끝이 프리셋 행과 어긋났습니다 — 6원소 폭 합이 " +
                "콘텐츠 폭 212pt를 채우지 못하거나 넘칩니다(40+8+26+4+26+4+44+4+26+4+26).");

            // 모든 타깃이 WCAG 2.2 SC 2.5.8 하한을 넘는가 — 화면 픽셀을 <b>포인트로 되돌려</b> 잰다
            // (배율을 테스트가 다시 계산하지 않게, 폭이 알려진 [✕] 칩으로 환산비를 얻는다).
            float pxPerPoint = _focus.CloseButtonScreenRectForTests.width / PopoverPanel.CloseChipWidth;
            Assert.Greater(pxPerPoint, 0f, $"{LogPrefix} [✕] 칩 폭이 0입니다 — 환산비를 못 얻습니다.");
            AssertTargetSize("목록", listChip, pxPerPoint);
            AssertTargetSize("−5", _focus.CustomStepScreenRect(-5), pxPerPoint);
            AssertTargetSize("−", _focus.CustomStepScreenRect(-1), pxPerPoint);
            AssertTargetSize("+", _focus.CustomStepScreenRect(1), pxPerPoint);
            AssertTargetSize("+5", _focus.CustomStepScreenRect(5), pxPerPoint);

            // 2단 스테퍼가 실제로 움직인다(전부 전역 폴링 경로).
            _focus.FeedClickForTests(_focus.CustomStepScreenRect(5).center);
            yield return null;
            Assert.AreEqual(55, _focus.CustomMinutesForTests, $"{LogPrefix} +5가 안 먹었습니다.");
            _focus.FeedClickForTests(_focus.CustomStepScreenRect(1).center);
            yield return null;
            Assert.AreEqual(56, _focus.CustomMinutesForTests, $"{LogPrefix} +1이 안 먹었습니다.");

            // 상한 클램프 — 되감기 없음(§R5-3-3). 같은 손잡이 연타라 중복 방지 창을 기다린다.
            yield return WaitPastActionDedup();
            _focus.FeedClickForTests(_focus.CustomStepScreenRect(5).center);
            yield return null;
            Assert.AreEqual(FocusSessionPopover.MaximumSessionMinutes, _focus.CustomMinutesForTests,
                $"{LogPrefix} 56 + 5가 상한에서 멈추지 않았습니다.");
            yield return WaitPastActionDedup();
            _focus.FeedClickForTests(_focus.CustomStepScreenRect(5).center);
            yield return null;
            Assert.AreEqual(FocusSessionPopover.MaximumSessionMinutes, _focus.CustomMinutesForTests,
                $"{LogPrefix} 상한에서 한 번 더 눌렀더니 값이 {_focus.CustomMinutesForTests}로 튀었습니다 — " +
                "되감기는 「실수로 60분」을 만듭니다.");

            // [목록] — 프리셋으로 되돌아오고, 고른 프리셋 값이 다시 유효해진다.
            _focus.FeedClickForTests(listChip.center);
            yield return null;
            Assert.IsFalse(_focus.ShowingCustomDurationRow, $"{LogPrefix} [목록]이 프리셋 행으로 되돌리지 않았습니다.");
            Assert.AreEqual(50f, _focus.SelectedMinutes, 0.01f,
                $"{LogPrefix} [목록]으로 돌아왔는데 고른 프리셋(50분)이 아닙니다.");

            Debug.Log($"{LogPrefix} ③ 통과 — [직접]이 전역 폴링 경로에서 살아 있고 상한 " +
                $"{FocusSessionPopover.MaximumSessionMinutes}분에서 멈춥니다.");
        }

        private static void AssertTargetSize(string label, Rect screenRect, float pxPerPoint)
        {
            float w = screenRect.width / pxPerPoint;
            float h = screenRect.height / pxPerPoint;
            Assert.GreaterOrEqual(w, UiChrome.MinTargetSizePoints - 0.01f,
                $"{LogPrefix} [{label}] 폭이 {w:F1}pt로 최소 타깃 {UiChrome.MinTargetSizePoints:F0}pt 미만입니다.");
            Assert.GreaterOrEqual(h, UiChrome.MinTargetSizePoints - 0.01f,
                $"{LogPrefix} [{label}] 높이가 {h:F1}pt로 최소 타깃 {UiChrome.MinTargetSizePoints:F0}pt 미만입니다.");
        }

        // ==================== ④ 하한도 되감지 않는다 ====================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator CustomDurationClampsAtTheBottomWithoutWrapping()
        {
            yield return LoadScene();
            OpenAtScreenCenter();
            yield return null;

            // 15분 프리셋에서 출발해 −5 세 번이면 0이 되는데, 하한에서 멈춰야 한다(−5가 −2가 아니다).
            _focus.FeedClickForTests(_focus.DurationChipScreenRect(0).center);
            yield return null;
            Assert.AreEqual(15f, _focus.SelectedMinutes, 0.01f, $"{LogPrefix} 15분 프리셋이 안 골라졌습니다.");

            _focus.FeedClickForTests(_focus.DurationChipScreenRect(3).center);
            yield return null;
            Assert.IsTrue(_focus.ShowingCustomDurationRow, $"{LogPrefix} [직접] 행이 열리지 않았습니다.");
            Assert.AreEqual(15, _focus.CustomMinutesForTests, $"{LogPrefix} 자유 입력이 15분에서 출발하지 않았습니다.");

            for (int i = 0; i < 3; i++)
            {
                _focus.FeedClickForTests(_focus.CustomStepScreenRect(-5).center);
                yield return null;
                yield return WaitPastActionDedup();
            }

            Assert.AreEqual(FocusSessionPopover.MinimumSessionMinutes, _focus.CustomMinutesForTests,
                $"{LogPrefix} 15에서 −5를 세 번 눌렀더니 {_focus.CustomMinutesForTests}분입니다 — " +
                $"하한 {FocusSessionPopover.MinimumSessionMinutes}분에서 멈춰야 합니다(0분이나 음수 세션은 없습니다).");

            _focus.FeedClickForTests(_focus.CustomStepScreenRect(-1).center);
            yield return null;
            Assert.AreEqual(FocusSessionPopover.MinimumSessionMinutes, _focus.CustomMinutesForTests,
                $"{LogPrefix} 하한에서 −1을 눌렀더니 {_focus.CustomMinutesForTests}분으로 튀었습니다(되감기 금지).");

            // 고른 값이 실제로 세션에 실린다 — 화면의 숫자와 시작되는 길이가 갈라지면 원칙 1 위반이다.
            _focus.FeedClickForTests(_focus.StartButtonScreenRect.center);
            yield return null;
            Assert.IsTrue(_director.IsSessionActive, $"{LogPrefix} [시작]을 눌렀는데 세션이 시작되지 않았습니다.");
            Assert.AreEqual(FocusSessionPopover.MinimumSessionMinutes * 60f, _director.SessionDurationSeconds, 0.5f,
                $"{LogPrefix} 화면은 {_focus.CustomMinutesForTests}분인데 세션은 " +
                $"{_director.SessionDurationSeconds:F0}초입니다 — 자유 입력 값이 디렉터로 넘어가지 않았습니다.");

            Debug.Log($"{LogPrefix} ④ 통과 — 하한 {FocusSessionPopover.MinimumSessionMinutes}분에서 멈추고 " +
                $"그 값이 세션 {_director.SessionDurationSeconds:F0}초로 실립니다.");
        }
    }
}
