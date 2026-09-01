using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 설정창의 <b>돌아갈 문</b>과 <b>준비 중인 탭의 표시</b> — 2026-09-01 페르소나(민지) M8 / M7.
    ///
    /// ============================================================================
    /// M8 — 배타 규칙은 옳았고, 없던 것은 되돌아오는 길이었다
    /// ============================================================================
    /// 설정창의 <b>유일한 마우스 진입점</b>은 캐릭터 정보창 헤더의 [설정] 칩이다. 그런데 설정창이
    /// 열리면서 그 정보창을 닫아 버리고(배타 모달 — 그 자체는 옳다) 설정창을 닫아도 돌아오지 않아,
    /// "장비 구경 → [설정] → [✕] → <b>빈 바탕화면</b>"이라는 막다른 길이 있었다. 시트를 걷으면
    /// 그 밑에 있던 창이 그대로 있어야 한다.
    ///
    /// 이 파일이 <b>진짜 클릭 좌표</b>로 확인하는 이유: 복귀를 <c>Open()/Close()</c> 직접 호출로만
    /// 검증하면 "칩을 눌렀을 때 그 경로를 타는가"가 빠진다 — 실제 사용자는 칩을 누른다.
    ///
    /// ============================================================================
    /// M7 — <b>2026-09-02 재판정</b>: 미구현 탭은 색이 아니라 <b>문장</b>으로 갈린다
    /// ============================================================================
    /// 원래 이 절의 단언은 "준비 중인 탭과 멀쩡한 탭의 <b>탭바 글자색이 다르다</b>"였고, 그것이
    /// 2026-09-02에 빨간불이 됐다. 그런데 <b>원인은 회귀가 아니라 상위 규칙의 도입</b>이었다.
    ///
    /// <para>대비 라운드가 <c>UiChrome.TabInactive</c>를 <b>삭제</b>하고 모든 글자색을
    /// <c>UiChrome.Ink(역할, 활성)</c> <b>사다리 3단</b>으로 통합했다(그 라운드의 실측: 설정창 비활성
    /// 탭 [캐릭터]가 4.84 → 5.79). 그 사다리에는 "준비 안 됨"이라는 단이 <b>없다</b>. 고르지 않은 탭은
    /// 준비 여부와 무관하게 전부 <see cref="UiChrome.InkMeta"/>(하한)다.</para>
    ///
    /// <para><b>판정: 색을 하나 더 만들지 않는다.</b> 4단째를 만드는 순간 사다리가 무너지고,
    /// 그 사다리가 <b>위계 역전을 물리적으로 불가능하게</b> 만든 것이 그 라운드의 성과다. 실제로 옛
    /// <c>TabInactive</c>는 2.35:1까지 내려가 페르소나가 <i>"화면에 한 글자도 없다"</i>고 적게 만들었다 —
    /// 글자는 있었다. <b>"준비 안 됨"을 더 흐리게 칠하는 것은 이미 한 번 실패한 해법이다.</b></para>
    ///
    /// <para>그래서 갈라지는 축을 <b>색 → 문장</b>으로 옮긴다. 선례는 [행동 명령] 창이 비활성 항목마다
    /// <b>왜 지금 못 쓰는지 한 줄로 적는</b> 어법이고, 설정창은 이미 같은 장치를 갖고 있다
    /// (<c>SettingsControls.ComposeCaption</c>이 사유 캡션을 조립하고 <see cref="UiChrome.InkMeta"/>로
    /// 그린다 — Meta가 비활성에서도 <b>안 흐려지는 것</b>이 그 규칙의 핵심이다).</para>
    ///
    /// <para>아래 세 테스트가 그 판정을 잠근다: ① 안 고른 탭 둘은 <b>같은 색</b>이다(사다리 유지)
    /// ② 미구현 탭은 <b>문장</b>으로 이유를 말한다 ③ 그 문장을 지우거나 흐리게 칠하면 ②가
    /// <b>실제로 빨개진다</b>(네거티브 컨트롤 — 색 단언을 문장 단언으로 바꾸는 라운드에서 이게 없으면
    /// 그 초록은 초록이 아니다).</para>
    ///
    /// <para>★ 2026-09-02 — 남아 있던 갭(<i>"눌러 봐야 빈 탭인 걸 안다"</i>)을 <c>Assert.Ignore</c>에서
    /// <b>정식 검사</b>로 올렸다: <see cref="UnimplementedTabsWearAReadyLaterWordInTheTabBarBeforeAnyClick"/>.
    /// 갈라지는 축은 여기서도 <b>색이 아니라 글자</b>다 — 탭 라벨 오른쪽에 10pt 보조 어절
    /// <c>준비 중</c>이 붙고, <b>라벨 색은 준비 여부와 무관하게 여전히 같다</b>(사다리 3단).</para>
    /// </summary>
    public sealed class SettingsWindowReturnPathTests
    {
        private const string LogPrefix = "[설정창복귀-TEST]";

        private CharacterInfoWindow _info;
        private SettingsWindow _settings;

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

        [UnityTearDown]
        public IEnumerator CloseEverything()
        {
            if (_settings != null && _settings.IsOpen) _settings.Close("테스트 정리");
            if (_info != null && _info.IsOpen) _info.Close("테스트 정리");
            _settings = null;
            _info = null;
            AppSettingsModel.ResetForTesting();
            yield return null;
        }

        // ==================== M8 ====================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ClosingSettingsReopensTheInfoWindowItReplaced()
        {
            yield return LoadScene();

            _info.Toggle("테스트");
            yield return null;
            Assert.IsTrue(_info.IsOpen, $"{LogPrefix} 전제: 정보창이 열려 있어야 합니다.");

            Rect chip = _info.SettingsChipScreenRect;
            Assert.Greater(chip.width, 1f, $"{LogPrefix} 헤더의 [설정] 칩 사각형이 비었습니다.");
            _info.FeedClickForTests(chip.center);
            yield return null;

            Assert.IsTrue(_settings.IsOpen, $"{LogPrefix} [설정] 칩을 눌렀는데 설정창이 열리지 않았습니다.");
            Assert.IsFalse(_info.IsOpen,
                $"{LogPrefix} 전제: 설정창은 배타 모달이라 정보창을 닫습니다(이 규칙 자체는 바꾸지 않았습니다).");

            _settings.FeedClickForTests(_settings.CloseButtonScreenRect.center);
            yield return null;

            Assert.IsFalse(_settings.IsOpen, $"{LogPrefix} [✕]를 눌렀는데 설정창이 닫히지 않았습니다.");
            Assert.IsTrue(_info.IsOpen,
                $"{LogPrefix} 설정창을 닫았는데 보던 캐릭터/장비창이 돌아오지 않았습니다 — " +
                "이 앱에서 설정창의 마우스 진입점은 그 창의 [설정] 칩 하나뿐이라, 돌아오지 않으면 " +
                "톱니 → [캐릭터]를 처음부터 다시 밟아야 하는 막다른 길이 됩니다(M8).");
        }

        /// <summary>
        /// ★★ <b>2026-09-02에 뒤집힌 테스트</b>. 여기에는 원래 "바깥 클릭으로 닫아도 정보창이
        /// 돌아온다"가 있었다. 사용자 지시로 <b>바깥 클릭이 아예 닫지 않게</b> 됐으므로, 이제 이
        /// 테스트가 잠그는 것은 정반대다 — <b>설정창 밖을 눌러도 설정창은 그대로 있고</b>, 그
        /// 결과로 밀려나 있던 정보창도 <b>되돌아오지 않는다</b>(돌아오면 그건 창이 닫혔다는 뜻이다).
        ///
        /// <para>★ 왜 이 자리를 비우지 않고 뒤집는가: 이 파일은 "설정창을 <b>어떤 경로로 닫든</b>
        /// 정보창이 돌아온다"를 잠근다. 경로가 하나 사라졌으면 그 사실 자체를 잠가 둬야 다음 사람이
        /// "복귀가 안 되네?" 하고 바깥 클릭 경로를 되살리지 않는다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ClickingOutsideSettingsNeitherClosesItNorReturnsTheInfoWindow()
        {
            yield return LoadScene();

            _info.Toggle("테스트");
            yield return null;
            _info.FeedClickForTests(_info.SettingsChipScreenRect.center);
            yield return null;
            Assume.That(_settings.IsOpen, Is.True, $"{LogPrefix} 전제: 설정창이 열려 있어야 합니다.");
            Assume.That(_info.IsOpen, Is.False, $"{LogPrefix} 전제: 설정창은 배타 모달이라 정보창을 닫습니다.");

            // ★ "창 밖"을 좌표로 못 박지 않는다 — 배치모드의 좁은 화면(예: 640×480)에서는 720×560 패널이
            //   화면을 통째로 덮어 <b>화면 안에는</b> 바깥이 존재하지 않는다(첫 작성본이 (4,4)를 찍었다가
            //   그 지점이 패널 <b>안쪽</b>이라 실패했다).
            //   ★ 2026-09-02 — 그때는 Assert.Ignore로 넘겼는데, 그러면 이 경로가 <b>어디에서도</b>
            //     검증되지 않는다(조용한 구멍). 화면 안에 없으면 <b>화면 밖이라도 패널 밖</b>인 점을
            //     쓴다: 프로덕션이 보는 조건은 "패널 사각형 안인가" 하나(SettingsWindow.FeedClick)라
            //     같은 분기를 그대로 지나고, 차이는 그 자리가 눈에 보이는가뿐이다.
            Rect panel = _settings.PanelScreenRect;
            float outsideX = panel.xMin - 20f;
            bool onScreen = outsideX >= 1f;
            if (!onScreen && panel.xMax + 20f <= Screen.width - 1f)
            {
                outsideX = panel.xMax + 20f;
                onScreen = true;
            }
            var outside = new Vector2(outsideX, panel.center.y);
            Assert.IsFalse(panel.Contains(outside),
                $"{LogPrefix} 고른 좌표 {outside}가 패널({panel}) 안입니다 — 전제가 무너졌습니다.");
            if (!onScreen)
            {
                Debug.Log($"{LogPrefix} 화면({Screen.width}×{Screen.height})이 창({panel})보다 좁아 " +
                    $"화면 밖 좌표 {outside}를 씁니다 — 프로덕션 분기는 동일하게 지납니다.");
            }

            _settings.FeedClickForTests(outside);
            yield return null;

            Assert.IsTrue(_settings.IsOpen,
                $"{LogPrefix} 창 밖을 눌렀더니 설정창이 꺼졌습니다 — 2026-09-02 사용자 지시는 " +
                "\"사용자가 닫기전에는 안꺼져야함\"입니다.");
            Assert.IsFalse(_info.IsOpen,
                $"{LogPrefix} 설정창은 그대로인데 정보창이 뒤에서 되살아났습니다 — 복귀는 설정창이 " +
                "<b>실제로 닫힐 때</b>만 일어나야 합니다(부르지 않은 창이 뜨는 것도 방해입니다).");

            // ★ 네거티브 컨트롤 — [✕] 경로는 여전히 닫고, 여전히 정보창을 되돌린다.
            //   이게 없으면 위 두 단언은 "닫기가 통째로 고장난" 상태에서도 초록이다.
            _settings.FeedClickForTests(_settings.CloseButtonScreenRect.center);
            yield return null;
            Assert.IsFalse(_settings.IsOpen, $"{LogPrefix} [✕]를 눌렀는데 설정창이 닫히지 않았습니다.");
            Assert.IsTrue(_info.IsOpen,
                $"{LogPrefix} [✕]로 닫았는데 정보창이 돌아오지 않았습니다 — 복귀 자체가 죽었습니다.");
        }

        /// <summary>★ 반대 방향 — 정보창이 <b>열려 있지 않았다면</b> 설정창을 닫아도 아무것도 열리지
        /// 않아야 한다. 부르지 않은 창이 뜨는 것은 그 자체로 방해다(원칙 2).</summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ClosingSettingsOpenedFromHotkeyDoesNotSummonTheInfoWindow()
        {
            yield return LoadScene();

            Assume.That(_info.IsOpen, Is.False, $"{LogPrefix} 전제: 정보창은 닫혀 있어야 합니다.");

            _settings.Open("테스트 — 단축키 경로");
            yield return null;
            Assume.That(_settings.IsOpen, Is.True);

            _settings.Close("테스트 — [✕]");
            yield return null;

            Assert.IsFalse(_info.IsOpen,
                $"{LogPrefix} 열려 있지도 않던 정보창이 설정창을 닫자 튀어나왔습니다 — 복귀는 " +
                "<b>내가 밀어낸 창</b>에만 적용됩니다.");
        }

        // ==================== 소은 #7-b — 페이지 칩은 내용 위에 앉지 않는다 ====================

        /// <summary>
        /// ★ [▲][▼]가 내용 영역 <b>밖</b>에 있어야 한다 — 2026-09-01 페르소나(소은) #7-b / (민지) M12.
        ///
        /// <para>예전 자리는 패널 y 496~520pt인데 내용 뷰포트가 88~526pt였다. 실물에서 [▼]가 "잡담 빈도"의
        /// "100%" 값 라벨을 덮었고, 그 줄이 <c>[−] ▬▬ [+] [▲] [▼] 100%</c>로 읽혀 <b>페이지 스크롤이
        /// 슬라이더 미세 조정처럼</b> 보였다. 행을 하나 더하거나 캡션을 한 줄 붙이는 것만으로 어떤 행이든
        /// 그 밴드에 들어올 수 있으므로, 좌표를 손으로 비교하는 대신 <b>겹치지 않는다</b>를 잠근다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator PageChipsNeverSitOnTopOfTheContentArea()
        {
            yield return LoadScene();

            _settings.Open("테스트");
            yield return null;
            yield return null;

            Rect content = _settings.ContentViewportScreenRect;
            Assert.Greater(content.width, 1f, $"{LogPrefix} 내용 영역 사각형이 비었습니다.");

            foreach (var chip in new[] { ("▲", _settings.PageUpScreenRect), ("▼", _settings.PageDownScreenRect) })
            {
                Assert.Greater(chip.Item2.width, 1f, $"{LogPrefix} [{chip.Item1}] 칩 사각형이 비었습니다.");
                Assert.IsFalse(chip.Item2.Overlaps(content),
                    $"{LogPrefix} [{chip.Item1}] 칩({chip.Item2})이 내용 영역({content}) 위에 앉아 있습니다 — " +
                    "그 아래 어떤 행이 오느냐에 따라 값 라벨이나 토글이 가려지고, 페이지 버튼이 그 행의 " +
                    "일부처럼 읽힙니다(소은 #7-b).");
            }
        }

        // ==================== M7 (2026-09-02 재판정: 색 → 문장) ====================

        /// <summary><c>SettingsControls.BeginRow</c>가 사유 한 줄에 붙이는 GameObject 이름.
        /// <para>이름에 의존하는 것이 옳은 이유: 프로덕션도 같은 관례로 부품을 찾는다
        /// (<c>SettingsControls.StepGlyph</c>가 <c>transform.Find("Label")</c>을 쓴다). 이름이 바뀌면
        /// 이 테스트는 <b>조용히 통과하지 않고 빨개진다</b> — 그것이 원하는 거동이다.</para></summary>
        private const string ReasonLineObjectName = "Caption";

        private static GameObject SettingsCanvas()
        {
            GameObject go = GameObject.Find("SettingsCanvas");
            Assert.IsNotNull(go, $"{LogPrefix} 씬에서 SettingsCanvas를 찾지 못했습니다 — " +
                "이름이 바뀌었다면 이 테스트도 함께 고쳐야 합니다(SettingsUserFacingCopyTests와 같은 관례).");
            return go;
        }

        /// <summary>색 비교는 <b>근사</b>로 한다 — 같은 상수에서 온 값이라 지금은 정확히 같지만,
        /// 중간에 알파 합성이 한 번만 끼어도 부동소수 비교가 이유 없이 깨진다.</summary>
        private static bool SameInk(Color a, Color b)
            => Mathf.Abs(a.r - b.r) < 1e-3f && Mathf.Abs(a.g - b.g) < 1e-3f
            && Mathf.Abs(a.b - b.b) < 1e-3f && Mathf.Abs(a.a - b.a) < 1e-3f;

        /// <summary>
        /// 지금 <b>내용 영역 안에 실제로 떠 있는</b> 사유 한 줄들.
        /// <para>비활성 페이지는 <c>GetComponentsInChildren(false)</c>가 걸러 내고(고르지 않은 탭의 문장은
        /// "화면에 있는 것"이 아니다), 마스크에 잘려 안 보이는 자리는 뷰포트 사각형이 걸러 낸다 —
        /// 이 스위트의 [▲][▼] 테스트가 쓰는 것과 같은 창구다.</para>
        /// </summary>
        private List<Text> VisibleReasonLines()
        {
            Rect content = _settings.ContentViewportScreenRect;
            var found = new List<Text>();
            foreach (Text t in SettingsCanvas().GetComponentsInChildren<Text>(false))
            {
                if (t == null || t.gameObject.name != ReasonLineObjectName) continue;
                if (string.IsNullOrWhiteSpace(t.text)) continue;
                if (!SettingsControlHost.ScreenRectOf(t.rectTransform).Overlaps(content)) continue;
                found.Add(t);
            }
            return found;
        }

        /// <summary>
        /// "왜 지금 못 쓰는지"를 말하는 한 줄이 <b>Meta 하한 잉크로</b> 화면에 있는가.
        /// <para>잉크까지 함께 보는 것이 요점이다: 문장이 있어도 <see cref="UiChrome.NonTextMuted"/> 같은
        /// <b>글자 금지 잉크</b>로 그려지면 그건 "있다"가 아니다(그 잉크로 라벨을 그렸을 때 실제로
        /// <i>"화면에 한 글자도 없다"</i>는 신고가 나왔다).</para>
        /// </summary>
        private bool TryFindReasonLine(out Text reason, out string diagnosis)
        {
            List<Text> lines = VisibleReasonLines();
            for (int i = 0; i < lines.Count; i++)
            {
                if (!SameInk(lines[i].color, UiChrome.InkMeta)) continue;
                reason = lines[i];
                diagnosis = null;
                return true;
            }

            reason = null;
            if (lines.Count == 0)
            {
                diagnosis = "내용 영역에 사유 한 줄이 하나도 없습니다";
                return false;
            }

            var seen = new List<string>();
            for (int i = 0; i < lines.Count; i++) seen.Add($"\"{lines[i].text}\"={lines[i].color}");
            diagnosis = $"사유 한 줄 {lines.Count}개가 전부 Meta 하한 잉크({UiChrome.InkMeta})가 " +
                $"아닙니다 — {string.Join(" / ", seen)}";
            return false;
        }

        private static SettingsWindow.Tab FirstUnimplementedTab()
        {
            foreach (SettingsWindow.Tab tab in System.Enum.GetValues(typeof(SettingsWindow.Tab)))
                if (!SettingsWindow.IsTabImplemented(tab)) return tab;
            Assert.Fail($"{LogPrefix} 미구현 탭이 하나도 없습니다 — 전부 채워졌다면 아래 테스트들은 " +
                "지울 때가 된 것입니다(빈 채로 통과시키지 마세요).");
            return SettingsWindow.Tab.General;
        }

        /// <summary>실제 클릭 경로로 탭을 연다(<c>SetTab</c> 직접 호출은 클릭 배선을 건너뛴다).</summary>
        private IEnumerator ClickTab(SettingsWindow.Tab tab)
        {
            _settings.FeedClickForTests(_settings.TabScreenRect(tab).center);
            yield return null;
            Assert.AreEqual(tab, _settings.ActiveTab,
                $"{LogPrefix} [{tab}] 탭을 눌렀는데 전환되지 않았습니다 — 탭 클릭 경로가 죽었습니다.");
        }

        // -------------------- ① 사다리: 안 고른 탭 둘은 같은 색이다 --------------------

        /// <summary>
        /// ★ 이 단언은 <b>일부러 "같다"를 요구한다</b>. 2026-09-02 이전의 반대 단언("다르다")이
        /// 대비 라운드와 정면으로 충돌했고, 그 충돌에서 이긴 쪽은 <b>사다리</b>다.
        ///
        /// <para>"같다"만 보면 <b>모든 잉크가 한 색으로 무너져도</b> 통과한다 — 그래서 같은 테스트에서
        /// <b>고른 탭과는 다르다</b>는 것도 함께 본다. 사다리가 살아 있다는 증거를 이 파일 안에 둔다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator UnselectedTabsShareOneInkSoTheThreeStepLadderCannotGrowAFourthStep()
        {
            yield return LoadScene();

            _settings.Open("테스트");
            yield return null;
            yield return null;

            Assume.That(_settings.ActiveTab, Is.EqualTo(SettingsWindow.Tab.General),
                $"{LogPrefix} 전제: 처음 열면 [일반] 탭입니다.");
            Assert.IsFalse(SettingsWindow.IsTabImplemented(SettingsWindow.Tab.Event),
                $"{LogPrefix} 전제: [이벤트] 탭은 아직 비어 있습니다.");
            Assert.IsTrue(SettingsWindow.IsTabImplemented(SettingsWindow.Tab.Character),
                $"{LogPrefix} 전제: [캐릭터] 탭에는 내용이 있습니다.");

            Color selected = _settings.TabLabelColor(SettingsWindow.Tab.General);   // 고른 탭
            Color ready = _settings.TabLabelColor(SettingsWindow.Tab.Character);    // 안 고름 + 내용 있음
            Color empty = _settings.TabLabelColor(SettingsWindow.Tab.Event);        // 안 고름 + 내용 없음

            Debug.Log($"{LogPrefix} 탭바 잉크 — 고른 탭={selected}, 안 고른 탭(내용 있음)={ready}, " +
                $"안 고른 탭(내용 없음)={empty}. 사다리 기준값 InkTab(false)={UiChrome.InkTab(selected: false)}.");

            Assert.IsTrue(SameInk(UiChrome.InkTab(selected: false), ready),
                $"{LogPrefix} 안 고른 탭이 사다리 값을 안 씁니다(실제 {ready} != {UiChrome.InkTab(selected: false)}).");
            Assert.IsTrue(SameInk(ready, empty),
                $"{LogPrefix} 준비된 탭({ready})과 미구현 탭({empty})의 탭바 잉크가 <b>갈라졌습니다</b> — " +
                "사다리에 4단째가 생겼다는 뜻입니다. 위계 역전을 구조적으로 막던 3단 규칙이 무너지므로 " +
                "되돌리세요. '준비 안 됨'은 색이 아니라 문장으로 말합니다(이 파일의 M7 절).");

            // ★ 위 두 단언은 "모든 잉크가 한 색"이어도 통과한다. 그래서 단이 실제로 존재하는지 본다.
            Assert.IsFalse(SameInk(selected, empty),
                $"{LogPrefix} 고른 탭과 안 고른 탭의 잉크가 같습니다({selected}) — 사다리가 통째로 " +
                "한 단으로 무너졌고, 그러면 위 '같다' 단언은 아무것도 증명하지 않습니다.");

            Assert.IsFalse(SameInk(empty, UiChrome.RetiredInk.TabInactive),
                $"{LogPrefix} 폐기된 옛 TabInactive({UiChrome.RetiredInk.TabInactive}, 4.15:1)가 " +
                "탭바에 되돌아왔습니다 — 대비 라운드가 지운 값입니다.");
        }

        // -------------------- ② 미구현 탭은 문장으로 이유를 말한다 --------------------

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator EveryUnimplementedTabSaysWhyInWordsInsteadOfADimmerColour()
        {
            yield return LoadScene();

            _settings.Open("테스트");
            yield return null;
            yield return null;

            int inspected = 0;
            foreach (SettingsWindow.Tab tab in System.Enum.GetValues(typeof(SettingsWindow.Tab)))
            {
                if (SettingsWindow.IsTabImplemented(tab)) continue;
                inspected++;

                yield return ClickTab(tab);

                bool found = TryFindReasonLine(out Text reason, out string diagnosis);
                Assert.IsTrue(found,
                    $"{LogPrefix} [{tab}] 탭을 열었는데 <b>왜 비었는지 말하는 문장</b>이 없습니다 — " +
                    $"{diagnosis}. 탭바 색은 준비된 탭과 같으므로(사다리 3단), 이 문장이 사라지면 " +
                    "미구현 탭과 '지금 안 열린 탭'을 가르는 신호가 화면에서 <b>완전히</b> 없어집니다. " +
                    "[행동 명령] 창이 비활성 항목에 이유를 적는 어법과 같은 자리입니다.");

                Debug.Log($"{LogPrefix} [{tab}] 사유 한 줄 = \"{reason.text}\" ({reason.color}).");
            }

            Assert.Greater(inspected, 0,
                $"{LogPrefix} 미구현 탭이 하나도 없어 이 테스트가 아무것도 검사하지 않았습니다 — " +
                "전부 채워졌다면 이 테스트를 지울 때입니다(빈 채로 통과시키지 마세요).");
        }

        // -------------------- ③ 네거티브 컨트롤 --------------------

        /// <summary>
        /// ★ <b>"색이 같아서 실패"를 "문장이 있어서 통과"로 바꾸는 라운드</b>라서 이게 반드시 필요하다.
        /// 새 단언이 실제로 무언가를 잡지 못하면, 위 ②는 그냥 <b>단언을 지운 것</b>과 다르지 않다.
        ///
        /// <para>두 방향을 다 민다: (a) 문장을 <b>지우면</b> 잡히는가 (b) 문장을 남긴 채
        /// <b>글자 금지 잉크</b>로 칠하면 잡히는가. (b)가 없으면 "흐려서 안 읽히는 문장"이 통과한다 —
        /// 옛 <c>TabInactive</c> 2.35:1이 정확히 그 형태였다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator NegativeControl_ErasingOrDimmingTheReasonLineActuallyTripsTheCheck()
        {
            yield return LoadScene();

            _settings.Open("테스트");
            yield return null;
            yield return null;

            SettingsWindow.Tab tab = FirstUnimplementedTab();
            yield return ClickTab(tab);

            Assume.That(TryFindReasonLine(out Text reason, out _), Is.True,
                $"{LogPrefix} 전제: [{tab}] 탭에 사유 한 줄이 있어야 이 네거티브 컨트롤이 성립합니다.");
            string originalText = reason.text;
            Color originalInk = reason.color;

            // (a) 문장을 지운다.
            reason.text = string.Empty;
            yield return null;
            bool foundAfterErase = TryFindReasonLine(out _, out string eraseDiagnosis);
            Assert.IsFalse(foundAfterErase,
                $"{LogPrefix} 사유 한 줄을 통째로 지웠는데도 검사가 통과했습니다 — 이 검사는 화면을 " +
                "보고 있지 않습니다(② 테스트의 초록이 무의미합니다).");
            Debug.Log($"{LogPrefix} 네거티브 컨트롤 (a) 문장 삭제 → 정상적으로 잡힘: {eraseDiagnosis}");

            // (b) 문장은 되돌리고 <b>글자 금지 잉크</b>로만 칠한다.
            reason.text = originalText;
            reason.color = UiChrome.RetiredInk.Quaternary;
            yield return null;
            bool foundAfterDim = TryFindReasonLine(out _, out string dimDiagnosis);
            Assert.IsFalse(foundAfterDim,
                $"{LogPrefix} 사유 한 줄을 글자 금지 잉크({UiChrome.RetiredInk.Quaternary})로 칠했는데도 " +
                "검사가 통과했습니다 — '문장이 있다'만 보고 '읽히는가'를 안 보면 옛 2.35:1 사고가 " +
                "그대로 재발합니다.");
            Debug.Log($"{LogPrefix} 네거티브 컨트롤 (b) 흐린 잉크 → 정상적으로 잡힘: {dimDiagnosis}");

            // 되돌린다. 이 캔버스는 씬 재로드로 사라지지만, 같은 씬 안에서 도는 뒷 단언을 오염시키지 않는다.
            reason.color = originalInk;
            yield return null;
            Assert.IsTrue(TryFindReasonLine(out _, out string restoreDiagnosis),
                $"{LogPrefix} 원상 복구 후에도 검사가 실패합니다 — 네거티브 컨트롤이 화면을 망가뜨린 " +
                $"채 끝났습니다({restoreDiagnosis}).");
        }

        // -------------------- ④ 탭바 pre-click 신호(2026-09-02: 갭 → 정식 검사) --------------------

        /// <summary><c>BuildTabBar</c>가 미구현 탭에 붙이는 보조 어절의 GameObject 이름.
        /// <para>이름에 기대는 것은 이 파일의 <see cref="ReasonLineObjectName"/>과 같은 관례다 —
        /// 이름이 바뀌면 <b>조용히 통과하지 않고 빨개진다</b>.</para></summary>
        private const string TabBadgeObjectName = "Badge";

        /// <summary>
        /// 탭 <b>버튼 하나</b> 안에 실제로 떠 있는 글자들(라벨 + 있으면 배지).
        /// <para>탭 사각형이 글자 상자의 <b>중심</b>을 담고 있는지로 가른다 — 탭 사이 간격이 4pt라
        /// <c>Overlaps</c>로 재면 옆 탭의 글자가 섞일 수 있다.</para>
        /// </summary>
        private List<Text> TabBarTextsIn(SettingsWindow.Tab tab)
        {
            Rect box = _settings.TabScreenRect(tab);
            var found = new List<Text>();
            foreach (Text t in SettingsCanvas().GetComponentsInChildren<Text>(false))
            {
                if (t == null || string.IsNullOrWhiteSpace(t.text)) continue;
                if (!box.Contains(SettingsControlHost.ScreenRectOf(t.rectTransform).center)) continue;
                found.Add(t);
            }
            return found;
        }

        /// <summary>
        /// ★ M7의 나머지 절반 — <b>누르기 전에</b> 탭바가 "이 탭은 아직 없다"를 말하는가.
        ///
        /// <para>2026-09-02까지 이 자리는 <c>Assert.Ignore</c>였다. 그때 적어 둔 후보(비텍스트 기호)는
        /// <b>대비가 아니라 의미론에서</b> 닫혔다: 이 앱의 자물쇠는 이미 <i>"놀면 열린다"</i>(장비 카드의
        /// <c>Lv.n에 열림</c>)라 미구현 탭에 붙이면 <b>거짓 약속</b>이 되고, 탭바의 도트는 관례상
        /// <i>"새 것/안 읽음"</i>이라 <b>클릭을 부르는</b> 기호이며, 밑줄은 이 앱에서 <i>"지금 여기"</i>다.
        /// 그래서 확정안은 <b>글자</b>다 — 라벨 오른쪽 10pt 보조 어절 <c>준비 중</c>.</para>
        ///
        /// <para>★★ <b>이 검사는 "색이 다르다"를 보지 않는다.</b> 위 ①의 <c>ready == empty</c> 등식은
        /// <b>계속 참이어야 한다</b>(사다리 3단). 여기서 보는 것은 <b>모양</b>이다:
        /// ① 미구현 탭 안에 라벨 말고 <see cref="UiChrome.InkMeta"/> 글자가 하나 더 있다
        /// ② 준비된 탭 안에는 글자가 정확히 하나뿐이다
        /// ③ 라벨 잉크는 두 탭에서 여전히 같다(<b>네거티브 컨트롤</b> — 사다리가 안 무너졌다)
        /// ④ 넓어진 탭의 오른쪽 끝이 [▲][▼] 페이지 칩을 밀지 않는다(소은 #7-b 회귀 가드).</para>
        ///
        /// <para>마지막에 <b>배지를 지워서</b> ①이 실제로 빨개지는지 민다 — 그 짝이 없으면 "글자가
        /// 하나 더 있다"는 단언은 무엇도 증명하지 못한다(이 파일 ③과 같은 이유).</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator UnimplementedTabsWearAReadyLaterWordInTheTabBarBeforeAnyClick()
        {
            yield return LoadScene();

            _settings.Open("테스트");
            yield return null;
            yield return null;

            Assume.That(_settings.ActiveTab, Is.EqualTo(SettingsWindow.Tab.General),
                $"{LogPrefix} 전제: 처음 열면 [일반] 탭입니다 — 이 검사는 <b>아무 탭도 누르기 전</b>을 봅니다.");

            Rect chip = _settings.PageUpScreenRect;
            Assert.Greater(chip.width, 1f,
                $"{LogPrefix} [▲] 페이지 칩의 화면 사각형이 비어 있어 ④(칩 불가침)를 잴 수 없습니다.");

            int badged = 0;
            Text sampleBadge = null;
            SettingsWindow.Tab sampleTab = SettingsWindow.Tab.General;

            foreach (SettingsWindow.Tab tab in System.Enum.GetValues(typeof(SettingsWindow.Tab)))
            {
                Rect box = _settings.TabScreenRect(tab);
                List<Text> texts = TabBarTextsIn(tab);

                // ④ 배지가 붙어 넓어진 탭이 페이지 칩을 밀지 않는가.
                Assert.Less(box.xMax, chip.xMin,
                    $"{LogPrefix} [{tab}] 탭의 오른쪽 끝({box.xMax})이 [▲][▼] 페이지 칩({chip.xMin})에 " +
                    "닿았습니다 — 칩이 탭 위로 올라오면 '지금 어느 탭인지'와 '스크롤'이 같은 자리에서 " +
                    "겹칩니다(소은 #7-b / 민지 M12가 칩을 이 자리로 옮긴 라운드가 무효가 됩니다).");

                if (SettingsWindow.IsTabImplemented(tab))
                {
                    // ② 준비된 탭은 글자 하나(라벨)뿐이다 — 배지가 새어 나가면 "없는 기능"이라는
                    //    거짓말이 되고, 그게 이 설계에서 가장 비싼 오작동이다.
                    Assert.AreEqual(1, texts.Count,
                        $"{LogPrefix} 준비된 [{tab}] 탭에 글자가 {texts.Count}개 있습니다 " +
                        $"({DescribeTexts(texts)}) — 라벨 하나여야 합니다.");
                    continue;
                }

                // ① 미구현 탭에는 라벨 말고 글자가 하나 더 있다.
                Assert.AreEqual(2, texts.Count,
                    $"{LogPrefix} 미구현 [{tab}] 탭의 탭바 글자가 {texts.Count}개입니다 " +
                    $"({DescribeTexts(texts)}) — 라벨 + 보조 어절 둘이어야 합니다. 이게 없으면 첫 " +
                    "방문자는 [일반]·[캐릭터]를 훑고 나서 '기능이 없는 건가, 내가 못 찾은 건가'에 " +
                    "스스로 답해야 하고, 그 두 답은 다음 행동을 정반대로 바꿉니다(M7).");

                Text badge = texts.Find(t => t.gameObject.name == TabBadgeObjectName);
                Assert.IsNotNull(badge,
                    $"{LogPrefix} 미구현 [{tab}] 탭에서 \"{TabBadgeObjectName}\" 글자를 찾지 못했습니다 " +
                    $"({DescribeTexts(texts)}).");
                Assert.AreEqual(SettingsWindow.TabBadgeText, badge.text,
                    $"{LogPrefix} 배지 문구가 캡션 접두사와 다른 어휘가 됐습니다 — 탭에서 읽은 말과 " +
                    "눌러서 읽는 말이 갈라지면 어휘가 두 벌이 됩니다.");
                Assert.IsTrue(SameInk(badge.color, UiChrome.InkMeta),
                    $"{LogPrefix} 배지 잉크가 Meta 하한({UiChrome.InkMeta})이 아닙니다(실제 {badge.color}) — " +
                    "사다리에 4단째가 생겼거나 글자 금지 잉크가 들어왔습니다.");
                Assert.AreEqual(UiChrome.FontCaption, badge.fontSize,
                    $"{LogPrefix} 배지가 캡션 단(10pt)이 아닙니다 — 라벨과 배지의 종속 관계는 색이 아니라 " +
                    "<b>글자 크기</b>가 만듭니다(둘 다 Meta 단이라 색으로는 못 가릅니다).");

                Text label = texts.Find(t => t.gameObject.name != TabBadgeObjectName);
                Assert.IsNotNull(label, $"{LogPrefix} 미구현 [{tab}] 탭의 라벨 글자를 찾지 못했습니다.");
                Assert.AreEqual(UiChrome.FontBody, label.fontSize,
                    $"{LogPrefix} [{tab}] 탭 라벨이 본문 단(12pt)에서 내려왔습니다 — 배지를 붙이면서 " +
                    "라벨을 건드리면 안 됩니다.");

                badged++;
                sampleBadge = badge;
                sampleTab = tab;
            }

            Assert.Greater(badged, 0,
                $"{LogPrefix} 미구현 탭이 하나도 없어 이 검사가 아무것도 보지 않았습니다 — 전부 " +
                "채워졌다면 이 테스트를 지울 때입니다(빈 채로 통과시키지 마세요).");

            // ③ 네거티브 컨트롤 A — 라벨 잉크는 <b>여전히 같다</b>. 이 등식이 깨지면 사다리에 4단째가
            //    생겼다는 뜻이고, 그건 이 설계가 피하려던 바로 그 실패다(옛 TabInactive 2.35:1).
            Color readyInk = _settings.TabLabelColor(SettingsWindow.Tab.Character);
            Color emptyInk = _settings.TabLabelColor(sampleTab);
            Assert.IsTrue(SameInk(readyInk, emptyInk),
                $"{LogPrefix} 배지를 붙이면서 라벨 색까지 갈랐습니다(준비됨 {readyInk} != 미구현 {emptyInk}) — " +
                "'준비 안 됨'은 색이 아니라 글자로 말합니다. 색으로도 말하면 사다리가 무너집니다.");

            Debug.Log($"{LogPrefix} 탭바 배지 — [{sampleTab}] \"{sampleBadge.text}\" {sampleBadge.fontSize}pt " +
                $"{sampleBadge.color}, 라벨 잉크는 준비된 탭과 동일({readyInk}). 미구현 탭 {badged}개 전부 확인.");

            // ③ 네거티브 컨트롤 B — 배지를 지우면 ①이 실제로 빨개지는가.
            string originalText = sampleBadge.text;
            sampleBadge.text = string.Empty;
            yield return null;
            Assert.AreEqual(1, TabBarTextsIn(sampleTab).Count,
                $"{LogPrefix} 배지 글자를 통째로 지웠는데도 [{sampleTab}] 탭에서 글자가 여전히 2개로 " +
                "세어집니다 — 이 검사는 화면을 보고 있지 않습니다(위 단언의 초록이 무의미합니다).");
            Debug.Log($"{LogPrefix} 네거티브 컨트롤 — 배지 삭제 → 정상적으로 1개로 떨어짐.");

            sampleBadge.text = originalText;
            yield return null;
            Assert.AreEqual(2, TabBarTextsIn(sampleTab).Count,
                $"{LogPrefix} 원상 복구 후에도 배지가 세어지지 않습니다 — 네거티브 컨트롤이 화면을 " +
                "망가뜨린 채 끝났습니다.");
        }

        private static string DescribeTexts(List<Text> texts)
        {
            var seen = new List<string>();
            for (int i = 0; i < texts.Count; i++)
                seen.Add($"{texts[i].gameObject.name}=\"{texts[i].text}\"({texts[i].fontSize}pt)");
            return seen.Count == 0 ? "없음" : string.Join(" / ", seen);
        }
    }
}
