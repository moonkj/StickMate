using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <b>사용자 명시 숨김의 구조적 계약</b>을 소스에서 직접 잠근다 — 2026-09-02.
    ///
    /// ============================================================================
    /// 왜 소스를 읽는가
    /// ============================================================================
    /// 여기서 지키려는 것은 "지금 값이 무엇인가"가 아니라 <b>"두 축이 한 조건식에 합쳐지지 않았는가"</b>다.
    /// 그건 런타임 관측으로는 <b>사후에만</b> 잡히고(누군가 합친 뒤 특정 조합을 재현해야 한다),
    /// 소스에서는 <b>즉시</b> 잡힌다. 이 저장소가 플랫폼 감사를 소스 스캔으로 짠 것과 같은 이유다.
    ///
    /// <para>또 하나 — <c>Win32WindowService.cs</c>는 전체가 <c>#if UNITY_STANDALONE_WIN</c> 안이라
    /// macOS 타깃에서는 <b>타입이 존재하지 않는다</b>. 그래서 "양 플랫폼이 K를 매핑하고 있는가"는
    /// 리플렉션이 아니라 <b>소스 파일 읽기</b>로만 확인할 수 있다(TEAM.md "활성 빌드 타깃 사각지대").</para>
    /// </summary>
    public sealed class ManualHideAxisSeparationAuditTests
    {
        private static string Root => Path.Combine(Application.dataPath, "_Project", "Scripts");

        private static string ReadSource(params string[] parts)
        {
            string path = Path.Combine(Root, Path.Combine(parts));
            Assert.IsTrue(File.Exists(path), $"소스를 찾지 못했습니다: {path}");
            return File.ReadAllText(path).Replace("\r\n", "\n");
        }

        /// <summary>줄 앞머리 주석(<c>//</c>, <c>///</c>)을 걷어낸다. 이 파일의 검사 대상은 <b>코드</b>이고,
        /// 이 저장소의 주석은 설계 의도를 길게 인용하므로 그대로 두면 전부 오탐이 된다.</summary>
        private static string StripComments(string source)
        {
            string[] lines = source.Split('\n');
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("//")) continue;
                int idx = lines[i].IndexOf("//", System.StringComparison.Ordinal);
                sb.Append(idx >= 0 ? lines[i].Substring(0, idx) : lines[i]).Append('\n');
            }
            return sb.ToString();
        }

        // ====================================================================
        // ① 두 축을 한 조건식에 합치지 않는다
        // ====================================================================

        /// <summary>
        /// ★ 리더 판정 2026-09-02: <b>전체화면 판정 줄에 사용자 숨김을 얹지 마라.</b>
        ///
        /// <para>그 줄은 <c>... &amp;&amp; AppSettingsModel.AutoHideOnFullscreen</c>과 함께 계산된다. 여기에
        /// <c>|| _userHidden</c>을 얹으면 <b>설정창 토글 하나가 두 축을 동시에 끈다</b> — 화면공유 중에
        /// 사용자가 자동 숨김을 끄는 순간 직접 숨겨 둔 캐릭터가 발표 화면으로 돌아온다.
        /// 실패 비용의 방향이 반대다: 자동 숨김은 오탐이 크고(엑셀 전체화면을 게임으로 오인한 실제 사고),
        /// 사용자 숨김은 본인이 눌렀으므로 오탐이 0이다.</para>
        ///
        /// <para>★ 2026-09-02 등급 배선 이후 — 축이 <b>셋</b>이 됐다(<c>_fullscreenPanelRetreat</c> = 등급 1).
        /// 그래서 이 감사는 축 <b>1과 3 각각</b>을 같은 기준으로 본다. 축 3에도 같은 설정 게이트가 붙어
        /// 있어야 한다: 사용자가 자동 숨김을 껐는데 창만 계속 걷히면, 그건 사용자가 끈 적 없는 동작이다.</para>
        ///
        /// <para><b>줄 단위가 아니라 문장(<c>;</c>) 단위로 읽는다</b>: 예전 버전은 <c>^.*$</c> 한 줄만 봤고,
        /// 대입문이 두 줄로 감기는 순간 뒷줄의 <c>|| _userHidden</c>을 <b>구조적으로 못 봤다</b>.</para>
        /// </summary>
        [Test]
        public void 전체화면_판정_줄에_사용자숨김을_얹지_않는다()
        {
            string src = StripComments(ReadSource("Core", "StickmanAgent.cs"));

            AssertAxisStatementIsGatedAndPure(src, "_fullscreenAutoHide",
                "축 1(등급 2 — 전체화면 게임이면 캐릭터까지 숨긴다)");
            AssertAxisStatementIsGatedAndPure(src, "_fullscreenPanelRetreat",
                "축 3(등급 1 — 게임이 아닌 전체화면 앱이면 표면만 걷는다)");

            // 합성 지점이 실제로 존재하고, 실제로 OR인가 — 위 단언의 공허함 방지.
            StringAssert.Contains("_fullscreenAutoHide || _userHidden", src,
                "두 축의 합성 지점(ApplySuspendDecision)이 사라졌습니다. 위 '얹지 않았다'는 " +
                "'사용자 숨김이라는 축 자체가 없다'로도 통과하므로, 합성이 실재하는지를 함께 봅니다.");
        }

        /// <summary>축 하나의 <b>대입문 전체</b>(다음 <c>;</c>까지)를 떠서 두 가지를 본다:
        /// (가) 설정창 게이트가 붙어 있는가 (나) 사용자 숨김이 얹히지 않았는가.</summary>
        private static void AssertAxisStatementIsGatedAndPure(string src, string field, string what)
        {
            var statement = new Regex(field + @"\s*=[^;]*;", RegexOptions.Singleline);
            MatchCollection hits = statement.Matches(src);
            Assert.AreEqual(1, hits.Count,
                $"StickmanAgent에서 {field} 대입문을 {hits.Count}개 찾았습니다({what}) — 0개면 그 축이 " +
                "사라진 것이고, 2개 이상이면 계산이 두 곳으로 갈라진 것입니다. 어느 쪽이든 이 감사의 " +
                "전제가 깨졌으니 감사도 함께 고치십시오.");

            string line = hits[0].Value;
            StringAssert.Contains("AutoHideOnFullscreen", line,
                $"{what}의 계산에서 설정창 게이트가 사라졌습니다:\n  " + line.Trim() +
                "\n사용자가 자동 숨김을 꺼도 그 축이 계속 동작합니다. " +
                "★ 게이트를 지역 변수로 빼도 이 단언이 걸립니다 — 그때는 감사를 약화시키지 말고 " +
                "코드 쪽에 상수 표현을 그대로 두십시오(그 이유가 StickmanAgent에 주석으로 적혀 있습니다).");
            Assert.IsFalse(line.Contains("_userHidden"),
                $"★ {what}의 계산 줄에 사용자 숨김이 얹혔습니다:\n  " + line.Trim() +
                "\n이 문장은 `AutoHideOnFullscreen` 게이트를 달고 있으므로, 여기에 사용자 숨김을 넣으면 " +
                "설정창 토글 하나가 두 축을 동시에 끕니다. 합성은 ApplySuspendDecision()에서만 하십시오.");
        }

        /// <summary>
        /// ★ 2026-09-02 — <b>등급 1은 캐릭터를 절대 숨기지 않는다</b>는 계약을 소스에서 잠근다.
        ///
        /// <para><c>ArePanelsSuppressed</c>가 <c>_isSuspended</c>를 <b>포함</b>해야 하고
        /// (그래야 "캐릭터는 숨었는데 차단막은 남은" 최악의 프레임이 구조적으로 불가능하다),
        /// 반대로 <c>IsSuspended</c>에는 <c>_fullscreenPanelRetreat</c>가 <b>들어가면 안 된다</b>
        /// (들어가는 순간 2026-08-31 신고 "엑셀 전체화면에서 캐릭터가 사라진다"의 완전한 회귀다).</para>
        ///
        /// <para><b>왜 소스인가</b>: 이건 "지금 값이 무엇인가"가 아니라 <b>방향</b>의 계약이다.
        /// 런타임 관측으로는 누군가 뒤집은 뒤 특정 조합을 재현해야 잡히고, 소스에서는 즉시 잡힌다.</para>
        /// </summary>
        [Test]
        public void 등급1은_캐릭터_숨김에_섞이지_않는다()
        {
            string src = StripComments(ReadSource("Core", "StickmanAgent.cs"));

            var suspended = new Regex(@"public bool IsSuspended\s*=>[^;]*;", RegexOptions.Singleline);
            MatchCollection sHits = suspended.Matches(src);
            Assert.AreEqual(1, sHits.Count,
                $"StickmanAgent.IsSuspended 정의를 {sHits.Count}개 찾았습니다 — 이 감사의 전제가 깨졌습니다.");
            Assert.IsFalse(sHits[0].Value.Contains("_fullscreenPanelRetreat"),
                "★ IsSuspended에 등급 1(축 3)이 섞였습니다:\n  " + sHits[0].Value.Trim() +
                "\n이 한 줄이 2026-08-31 사용자 신고(\"엑셀같은 프로그램 전체화면에서 엑셀 클릭하면 " +
                "캐릭터가 없어져버림\")의 완전한 회귀입니다. 등급 1은 ArePanelsSuppressed로만 나갑니다.");

            var panels = new Regex(@"public bool ArePanelsSuppressed\s*=>[^;]*;", RegexOptions.Singleline);
            MatchCollection pHits = panels.Matches(src);
            Assert.AreEqual(1, pHits.Count,
                $"StickmanAgent.ArePanelsSuppressed 정의를 {pHits.Count}개 찾았습니다 — 0개면 등급 1 소비자 " +
                "창구가 통째로 없는 것입니다.");
            StringAssert.Contains("_isSuspended", pHits[0].Value,
                "★ ArePanelsSuppressed가 _isSuspended를 포함하지 않습니다:\n  " + pHits[0].Value.Trim() +
                "\n포함관계(등급 2 ⊇ 등급 1)가 깨지면 캐릭터는 숨었는데 창과 차단막이 남는 상태가 " +
                "구조적으로 가능해집니다 — 안 보이는데 클릭만 먹는, 이 앱에서 가장 나쁜 형태입니다.");
            StringAssert.Contains("_fullscreenPanelRetreat", pHits[0].Value,
                "ArePanelsSuppressed가 축 3을 읽지 않습니다 — 등급 1이 어떤 소비자에게도 도달하지 않습니다.");
        }

        /// <summary>
        /// ★ 네이티브 조회는 <b>폴링당 1회</b>여야 한다(<c>IForeignFullscreenTierSource</c>의 구현 계약).
        /// 등급과 bool을 각각 조회하면 24시간 상주 앱의 창 열거·카테고리 조회 비용이 그대로 두 배가 되고,
        /// 두 조회가 서로 다른 순간을 보게 되어 디바운서가 같은 폴링에서 두 번 갱신된다.
        /// </summary>
        [Test]
        public void 전체화면_조회는_폴링당_한_번이다()
        {
            string src = StripComments(ReadSource("Core", "StickmanAgent.cs"));

            int tierCalls = CountOccurrences(src, "GetForeignFullscreenTier()");
            Assert.AreEqual(1, tierCalls,
                $"StickmanAgent에서 GetForeignFullscreenTier() 호출을 {tierCalls}개 찾았습니다 — " +
                "원본 조회는 정확히 한 곳(PollForeignFullscreenTier)이어야 합니다.");

            int boolCalls = CountOccurrences(src, "IsFullscreenAppActive()");
            Assert.AreEqual(1, boolCalls,
                $"StickmanAgent에서 IsFullscreenAppActive() 호출을 {boolCalls}개 찾았습니다 — " +
                "등급을 모르는 서비스를 위한 <b>강등 경로 한 줄</b>만 남아 있어야 합니다. " +
                "폴링 경로에서 등급과 bool을 각각 부르면 네이티브 조회가 두 배가 됩니다.");
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, System.StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }

        /// <summary>
        /// ★ 사용자 숨김이 <b>Suspend() 경로</b>를 타는가 — 렌더러만 끄는 옛 경로로 되돌아가지 않았는가.
        ///
        /// <para>옛 <c>SettingsWindow.SetCharacterVisibleNow</c>는
        /// <c>StickmanBlackboard.SetCharacterVisible</c>(= 렌더러 토글)만 불렀다. 그러면 열린 창과
        /// 그 클릭 차단막이 그대로 남아, 캐릭터만 사라지고 UI가 발표 화면에 찍힌다.</para>
        /// </summary>
        [Test]
        public void 사용자숨김은_렌더러가_아니라_Suspend_경로를_탄다()
        {
            string agent = StripComments(ReadSource("Core", "StickmanAgent.cs"));
            StringAssert.Contains("public bool SetUserHidden", agent,
                "StickmanAgent에 사용자 명시 숨김 창구가 없습니다.");
            StringAssert.Contains("ApplySuspendDecision();", agent,
                "SetUserHidden이 합성 지점을 부르지 않으면 폴링 주기(기본 1초)만큼 늦게 숨습니다 — " +
                "'지금 화면을 공유한다'는 맥락에서는 없는 것과 같습니다.");

            string settings = StripComments(ReadSource("Interaction", "SettingsWindow.cs"));
            Assert.IsFalse(settings.Contains("SetCharacterVisible("),
                "설정창이 다시 렌더러 토글(SetCharacterVisible)로 숨기고 있습니다 — 그 경로는 열린 창과 " +
                "클릭 차단막을 걷지 못하고, 전체화면 감지가 한 번 왕복하면 Resume()이 되살립니다.");
            StringAssert.Contains("SetUserHidden", settings,
                "설정창 [일반]의 '지금 즉시'가 사용자 명시 숨김 상태에 연결되어 있지 않습니다.");
        }

        // ====================================================================
        // ② 단축키 — 플랫폼 파일을 고치지 않고도 양쪽이 매핑돼 있는가
        // ====================================================================

        /// <summary>
        /// ★ <c>GlobalKey.K</c>는 <b>양 플랫폼에 이미 키코드가 매핑</b>돼 있었고(그래서 이 기능은
        /// <c>Platform/</c>을 한 줄도 고치지 않았다), 그 사실이 계속 참인지를 <b>소스로</b> 확인한다 —
        /// Windows 파일은 이 머신에서 타입이 존재하지 않아 리플렉션으로는 셀 수 없다.
        /// </summary>
        [Test]
        public void K는_양_플랫폼에_이미_매핑되어_있다()
        {
            string mac = StripComments(ReadSource("Platform", "MacOS", "MacWindowService.cs"));
            StringAssert.Contains("case GlobalKey.K:", mac,
                "macOS 키 매핑에서 K가 사라졌습니다 — 사용자 명시 숨김의 유일한 탈출구가 죽습니다.");
            StringAssert.Contains("kVK_ANSI_K", mac, "macOS가 K를 실제 가상 키코드로 읽지 않습니다.");

            string win = StripComments(ReadSource("Platform", "Windows", "Win32WindowService.cs"));
            StringAssert.Contains("case GlobalKey.K:", win,
                "Windows 키 매핑에서 K가 사라졌습니다 — 이 파일은 이 머신에서 컴파일되지 않으므로 " +
                "소스로만 확인할 수 있습니다.");

            // 표기는 반드시 단일 정의처를 거친다(Windows에서는 Ctrl+Alt+Win+K로 렌더된다).
            StringAssert.EndsWith(StickmanAgent.UserHideHotkeyLetter,
                ShortcutLabel.MacChord(StickmanAgent.UserHideHotkeyLetter),
                "동작키 상수와 표기가 어긋났습니다.");
            StringAssert.EndsWith(StickmanAgent.UserHideHotkeyLetter,
                ShortcutLabel.WindowsChord(StickmanAgent.UserHideHotkeyLetter),
                "Windows 표기가 동작키 상수와 어긋났습니다.");
        }

        /// <summary>
        /// ★ <b>OS가 이미 가져간 조합이 아니어야 한다.</b> 설정창 단축키가 <c>⌃⌥⌘,</c>였을 때
        /// macOS 접근성 "대비 줄이기"와 충돌해 사용자의 OS 설정이 실제로 내려간 사고가 있었다
        /// (<see cref="ShortcutLabel.MacReservedActionKeys"/>).
        /// <para>기대값을 <b>숫자가 아니라 그 배열</b>로 검사한다 — 프로덕션 상수를 테스트에 베끼지 않는다.</para>
        /// </summary>
        [Test]
        public void 사용자숨김_동작키가_OS_예약과_충돌하지_않는다()
        {
            CollectionAssert.DoesNotContain(ShortcutLabel.MacReservedActionKeys,
                StickmanAgent.UserHideHotkeyLetter,
                "사용자 명시 숨김의 동작키가 macOS 예약 조합입니다 — 한 번 누를 때 두 가지 일이 " +
                "일어나고, 그중 하나는 우리가 통제하지 못하는 OS 설정 변경입니다(원칙 2·3 위반).");
            CollectionAssert.DoesNotContain(ShortcutLabel.WindowsReservedActionKeys,
                StickmanAgent.UserHideHotkeyLetter,
                "사용자 명시 숨김의 동작키가 Windows 예약 조합입니다.");

            // ★ 빈 목록이 기대값이라는 사실을 <b>명시</b>한다(TEAM.md 거짓 통과 #5) —
            //   Windows 예약이 실제로 0건인 것이 조사 결과이고, 그 전제가 바뀌면 여기서 먼저 걸린다.
            Assert.AreEqual(0, ShortcutLabel.WindowsReservedActionKeys.Length,
                "Windows 예약 목록이 더 이상 비어 있지 않습니다 — 위 DoesNotContain이 " +
                "'목록이 비어서 통과'가 아니라 실제 검사가 되도록 전제를 여기서 고정합니다.");
            Assert.Greater(ShortcutLabel.MacReservedActionKeys.Length, 0,
                "macOS 예약 목록이 비었습니다 — 그러면 위 DoesNotContain은 <b>어떤 글자를 넣어도</b> " +
                "통과하는 공허한 단언이 됩니다(이 저장소가 실제로 겪은 '빈 목록이라 foreach가 아무것도 " +
                "안 재고 초록' 사고와 같은 형태).");
        }

        // ====================================================================
        // ③ 탈출구 — 이 키는 개발 게이트 뒤에 있으면 안 된다
        // ====================================================================

        /// <summary>
        /// ★ K가 <c>StickMateDevTools</c> 게이트 뒤로 들어가면 릴리스 빌드에서 죽는다. 숨는 동안에는
        /// 톱니가 <c>IsSuspended</c>를, 부채꼴·창·팝오버가 <c>ArePanelsSuppressed</c>를 보고 전부 스스로
        /// 내려가므로(사용자 명시 숨김은 축 2라 두 창구 모두에서 참이다) <b>마우스 경로가 0</b>이고,
        /// 이 키가 죽으면 사용자에게 남는 수단은 강제 종료뿐이다. Q(종료)와 같은 급의 계약이다.
        /// </summary>
        [Test]
        public void 숨기기_단축키는_개발_게이트_뒤에_있지_않다()
        {
            string src = StripComments(ReadSource("Interaction", "AppControlDirector.cs"));

            var readLine = new Regex(@"^.*IsKeyDown\(GlobalKey\.K\).*$", RegexOptions.Multiline);
            MatchCollection hits = readLine.Matches(src);
            Assert.AreEqual(1, hits.Count,
                $"AppControlDirector에서 GlobalKey.K 조회 줄을 {hits.Count}개 찾았습니다 — 0개면 " +
                "단축키가 배선되지 않은 것이고, 2개 이상이면 이 감사의 전제가 깨진 것입니다.");

            Assert.IsFalse(hits[0].Value.Contains("dev &&"),
                "★ 숨기기 단축키가 개발 전용 게이트 뒤에 있습니다:\n  " + hits[0].Value.Trim() +
                "\n릴리스 빌드에서 이 키가 죽으면 숨긴 사용자의 탈출구가 0이 됩니다.");

            // 실제로 동작에 연결돼 있는가 — "키만 읽고 아무것도 안 한다"를 막는다.
            StringAssert.Contains("ControlAction.UserHide", src,
                "K를 읽기만 하고 어떤 동작에도 연결하지 않았습니다.");
            StringAssert.Contains("ToggleUserHidden", src,
                "AppControlDirector가 StickmanAgent의 사용자 숨김 토글을 부르지 않습니다.");
        }

        /// <summary>
        /// ★ 부팅 배너의 사용자 단축키 목록에 K가 실려 있는가. 이 앱에는 트레이도 메뉴바도 없어
        /// <b>단축키는 발견 불가능</b>하고, 배너가 팀과 사용자 모두에게 유일한 목록이다.
        /// 격파 놀이 삭제 라운드에 K의 <b>바인딩만</b> 지워지면서 이 자리가 비어 있었다.
        /// </summary>
        [Test]
        public void 부팅_배너의_사용자_단축키_목록에_K가_있다()
        {
            string src = ReadSource("Interaction", "AppControlDirector.cs");
            int banner = src.IndexOf("string userKeys", System.StringComparison.Ordinal);
            Assert.Greater(banner, 0, "부팅 배너의 사용자 단축키 줄(userKeys)을 찾지 못했습니다.");

            int end = src.IndexOf("string devKeys", System.StringComparison.Ordinal);
            Assert.Greater(end, banner, "부팅 배너의 구조가 바뀌었습니다 — 이 감사도 함께 고치십시오.");

            string block = src.Substring(banner, end - banner);
            StringAssert.Contains("UserHideHotkeyLetter", block,
                "부팅 배너의 사용자 단축키 목록에 숨기기 키가 없습니다. 글자를 손으로 적지 말고 " +
                "StickmanAgent.UserHideHotkeyLetter를 쓰십시오 — 키를 옮기는 날 목록만 남아 거짓이 됩니다.");
        }
    }
}
