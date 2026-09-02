using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// 회귀 잠금 — <b>"클릭 차단막을 만드는 UI 표면은 전부 전체화면 감지를 폴링한다."</b>
    /// (절대 불변 원칙 2: 비침해 — 전체화면 게임 감지 시 자동 숨김)
    ///
    /// ============================================================================
    /// 무엇이 터졌었나 (2026-09-01, 코드 근거 확정)
    /// ============================================================================
    /// <c>Interaction/TodoPostItWidget.cs</c>의 <c>IsSuspended</c> 참조가 <b>0건</b>이었다.
    /// <c>Update()</c>가 조건 없이 <c>SyncClickThroughBlocker()</c>를 매 프레임 부르고, 그 함수는
    /// 패널의 <c>activeSelf</c>만 보고 차단막을 켠다. 차단막은 <b>씬 루트</b>에 있는데
    /// <c>StickmanAgent.Suspend()</c>는 Awake에서 캐시한 <b>캐릭터 렌더러만</b> 끈다. 결과: 전체화면
    /// 게임 위에 포스트잇이 그대로 뜨고 그 사각형의 클릭까지 먹었다.
    ///
    /// 같은 종류의 차단막을 가진 다른 표면들(<c>CharacterInfoWindow</c> / <c>InfoGearIconWidget</c> /
    /// <c>PopoverPanel</c> / <c>SettingsWindow</c> 등)은 전부 폴링하고 있었다 — <b>하나만 빠졌다</b>.
    ///
    /// ============================================================================
    /// 왜 명부가 아니라 전수 조사인가
    /// ============================================================================
    /// PlayMode 쪽 <c>FullscreenSuspendUiHidingTests</c>는 표면을 <b>손으로 적는</b> 방식이었고,
    /// 거기 <c>TodoBoardPopover</c>(이름이 비슷한 <b>다른</b> 표면)가 들어 있어서 "할 일 쪽은
    /// 검증됨"으로 보였다. 명부 방식은 이미 한 번 샜다.
    ///
    /// 그래서 이 테스트는 <c>Interaction/</c>의 <b>모든</b> 소스를 훑어 "클릭 차단막을 만드는 파일"을
    /// 스스로 찾아낸다. 새 표면이 생기면 이 파일을 고치지 않아도 자동으로 검사 대상이 된다.
    /// 소스 스캔이라 씬 조립도, 플랫폼도, 실행도 필요 없다(양 플랫폼 공통 코드를 재는 것이므로
    /// macOS/Windows 어느 쪽에서 돌려도 같은 답이다).
    ///
    /// <para><b>한계(정직하게 적는다)</b>: 이 검사는 "IsSuspended라는 글자가 그 파일에 있는가"만
    /// 본다. 폴링을 <b>제대로</b> 하는지(차단막까지 끄는지)는 PlayMode 쪽 실측 테스트가 잰다.
    /// 두 테스트는 서로를 대체하지 않는다 — 이건 <b>빠짐</b>을 잡고, 저건 <b>동작</b>을 잡는다.</para>
    /// </summary>
    public sealed class SuspendClickBlockerAuditTests
    {
        /// <summary>차단막/히트타깃 GameObject 이름의 규약. 이 규약이 깨지면 아래 최소 개수 단언이
        /// 먼저 실패해서 "아무것도 못 찾고 초록불"이 되지 않는다.</summary>
        private static readonly string[] BlockerNameMarkers = { "Blocker", "ClickTarget" };

        /// <summary>
        /// 검사 면제 — <b>사유가 있는 것만</b>, 사유와 함께.
        ///
        /// <para>★ 2026-09-02 현재 <b>비어 있다</b>. 유일한 항목이던 <c>BattleMinigameRenderer</c>는
        /// 격파 놀이 기능이 통째로 삭제되면서 파일 자체가 사라졌다(간접 경로 면제였다: 표적이
        /// 미니게임 컨테이너의 자식이라 디렉터가 걷힐 때 함께 파괴됐고, 자기 파일에서
        /// <c>IsSuspended</c>를 읽지는 않았다). 비어 있는 것이 정상이며 <b>기본값</b>이다 —
        /// 새로 면제를 넣으려면 사유를 반드시 함께 적어라.</para>
        /// </summary>
        private static readonly Dictionary<string, string> Exempt = new Dictionary<string, string>();

        private static string InteractionDirectory =>
            Path.Combine(Application.dataPath, "_Project", "Scripts", "Interaction");

        /// <summary>이 파일이 클릭 차단막/히트타깃 GameObject를 <b>만드는가</b>.
        /// <c>new GameObject(...)</c> 호출 안에 이름 규약 표식이 있으면 소유자로 본다
        /// (<c>GetType().Name + "Blocker"</c>처럼 조립되는 이름도 그대로 잡힌다).</summary>
        private static bool CreatesClickBlocker(string source)
        {
            const string needle = "new GameObject(";
            int i = 0;
            while ((i = source.IndexOf(needle, i, StringComparison.Ordinal)) >= 0)
            {
                int start = i + needle.Length;
                int end = source.IndexOf('\n', start);
                if (end < 0) end = source.Length;
                string arg = source.Substring(start, end - start);
                for (int m = 0; m < BlockerNameMarkers.Length; m++)
                {
                    if (arg.IndexOf(BlockerNameMarkers[m], StringComparison.Ordinal) >= 0) return true;
                }
                i = start;
            }
            return false;
        }

        private static List<string> FindBlockerOwners()
        {
            var owners = new List<string>();
            foreach (string path in Directory.GetFiles(InteractionDirectory, "*.cs", SearchOption.AllDirectories))
            {
                if (CreatesClickBlocker(File.ReadAllText(path))) owners.Add(Path.GetFileName(path));
            }
            owners.Sort(StringComparer.Ordinal);
            return owners;
        }

        [Test]
        public void 차단막을_만드는_모든_표면이_전체화면_감지를_폴링한다()
        {
            List<string> owners = FindBlockerOwners();

            // ★ 스캔이 망가져 "0개를 찾고 전부 통과"하는 것을 먼저 막는다.
            //   2026-09-01 기준 6개 → 2026-09-02 기준 <b>정확히 5개</b>(격파 놀이 삭제로
            //   BattleMinigameRenderer.cs가 사라졌다). 하한 5는 지금 실제 개수와 같으므로
            //   표면이 하나라도 더 줄면 이 단언이 바로 걸린다 — 하한이지 목표가 아니다.
            Assert.GreaterOrEqual(owners.Count, 5,
                $"차단막 소유자를 {owners.Count}개밖에 못 찾았다({string.Join(", ", owners)}) — " +
                "GameObject 이름 규약이 바뀌었다면 BlockerNameMarkers를 함께 고쳐라. " +
                "이 단언이 없으면 스캔이 깨진 날 이 테스트가 조용히 무의미해진다.");

            // ★ 실제로 터졌던 파일이 스캔에 잡히는지 못박는다(이 사고의 회귀 앵커).
            CollectionAssert.Contains(owners, "TodoPostItWidget.cs",
                "포스트잇이 차단막 소유자로 잡히지 않는다 — 이 테스트가 잡아야 할 바로 그 파일이다.");

            var missing = new List<string>();
            foreach (string file in owners)
            {
                if (Exempt.ContainsKey(file)) continue;
                string source = File.ReadAllText(Path.Combine(InteractionDirectory, file));
                if (!PollsAnySuspendChannel(source)) missing.Add(file);
            }

            Assert.IsEmpty(missing,
                "클릭 차단막을 만들면서 전체화면 감지(IsSuspended / ArePanelsSuppressed)를 한 번도 읽지 않는 표면이 있다: " +
                $"{string.Join(", ", missing)}. 이 표면은 전체화면 게임 위에 그대로 그려지고 그 사각형의 " +
                "클릭까지 먹는다(절대 불변 원칙 2 위반). 사유가 있어 면제하려면 Exempt에 이유와 함께 " +
                "적어라 — 조용히 빼지 마라.");
        }

        [Test]
        public void 면제_목록은_실제로_존재하는_파일만_담는다()
        {
            // 면제는 "지금은 사유가 있다"는 기록이다. 파일이 사라졌는데 면제만 남으면 그 기록은
            // 다음 사람에게 거짓말이 된다.
            //
            // ★ 2026-09-02 — Exempt가 <b>비었다</b>. 빈 컬렉션 위의 foreach는 아무것도 단언하지
            //   않으면서 초록불이 된다(이 저장소가 같은 밤에 '거짓 통과 9건'을 겪은 바로 그 모양).
            //   그래서 "비어 있음"을 <b>기대값으로 명시</b>한다: 누군가 면제를 추가하면 이 단언이
            //   먼저 걸려서, 아래 파일존재/사유 검사가 실제로 도는지 눈으로 확인하게 만든다.
            Assert.IsEmpty(Exempt,
                "면제가 새로 생겼다: " + string.Join(", ", Exempt.Keys) + ". 면제 자체는 허용되지만 " +
                "(사유를 적었다면) 이 단언을 그때 함께 고쳐라 — 이 줄이 없으면 아래 foreach는 " +
                "빈 목록 위에서 아무것도 재지 않는 초록불이 된다.");

            foreach (KeyValuePair<string, string> entry in Exempt)
            {
                string path = Path.Combine(InteractionDirectory, entry.Key);
                Assert.IsTrue(File.Exists(path),
                    $"면제 목록의 {entry.Key}가 더 이상 존재하지 않는다 — 면제도 함께 지워라. 사유: {entry.Value}");
                Assert.IsNotEmpty(entry.Value, $"{entry.Key}의 면제 사유가 비어 있다.");
            }
        }

        // ============================================================================
        // 등급 1(ForeignFullscreenTier.PanelsOnly) — 2026-09-02 출시 Blocker
        // ============================================================================
        //
        // 왜 <c>IsSuspended</c>만으로는 부족해졌는가:
        // 페르소나 `재현`이 실기에서 재현한 것 — 카테고리를 선언하지 않은 앱(Zoom/Teams/Keynote 부류)을
        // 네이티브 전체화면으로 올리면 <c>IsSuspended</c>가 <b>영원히 false</b>이고, 그 위에 정보창
        // 877x853pt(화면 1512x982pt의 <b>면적 50.38%</b>)가 그대로 뜬 채 그 사각형의 클릭까지 먹었다.
        // 그래서 표면이 읽어야 하는 값이 <c>ArePanelsSuppressed</c>로 바뀌었고, 이 검사가 그것을 잠근다.
        //
        // ★ <b>차단막(Blocker)과 히트타깃(ClickTarget)을 구분한다</b> — 리더 판정으로 톱니는 등급 2에
        //   남았다(등급 1의 안전판이 "복구는 톱니 1클릭"인데, 톱니를 등급 1에 넣으면 그 안전판이 자기
        //   자신을 지운다). 톱니가 소유한 것은 아이콘 크기의 <b>히트타깃</b>이지 화면을 덮는 차단막이
        //   아니므로, 이 검사의 대상은 이름이 "Blocker"로 끝나는 소유자뿐이다.

        /// <summary>"Blocker" 표식으로 GameObject를 만드는가 — 화면/패널을 덮는 <b>차단막</b> 소유자.
        /// 톱니의 <c>InfoGearClickTarget</c>(아이콘 크기 히트타깃)은 여기 잡히지 않는다.</summary>
        private static bool CreatesFullRectBlocker(string source)
        {
            const string needle = "new GameObject(";
            int i = 0;
            while ((i = source.IndexOf(needle, i, StringComparison.Ordinal)) >= 0)
            {
                int start = i + needle.Length;
                int end = source.IndexOf('\n', start);
                if (end < 0) end = source.Length;
                if (source.IndexOf("Blocker", start, end - start, StringComparison.Ordinal) >= 0) return true;
                i = start;
            }
            return false;
        }

        private static bool PollsAnySuspendChannel(string source)
            => source.IndexOf("IsSuspended", StringComparison.Ordinal) >= 0
               || source.IndexOf("ArePanelsSuppressed", StringComparison.Ordinal) >= 0;

        private static bool PollsPanelChannel(string source)
            => source.IndexOf("ArePanelsSuppressed", StringComparison.Ordinal) >= 0;

        /// <summary>
        /// <b>아직 배선되지 않은 차단막 소유자</b> — 사유와 함께. 비어 있는 것이 목표 상태다.
        ///
        /// <para>여기 있는 항목은 <c>Assert.Fail</c>이 아니라 <c>Assert.Ignore</c>로 떨어진다.
        /// 러너에 <b>"건너뜀"으로 계속 보이게</b> 하려는 것이다(CLAUDE.md 플랫폼 갭 관례와 같은 처방) —
        /// 빨간불로 두면 다른 진짜 실패를 가리고, 조용히 통과시키면 잊힌다.</para>
        ///
        /// <para>★ <b>2026-09-02 현재 비어 있다.</b> 유일한 항목이던 <c>TodoPostItWidget.cs</c>가
        /// <c>_agent.ArePanelsSuppressed</c>로 배선되어 지워졌다(등급 배선 라운드 시점에는
        /// 자동 접힘 P0 작업이 같은 파일을 점유 중이라 리더가 파일을 갈라 제외했던 항목이다).
        /// <b>비어 있는 것이 기본값이자 목표 상태</b>이고, 그 사실을
        /// <see cref="미배선_목록은_실제로_존재하고_아직_배선되지_않은_파일만_담는다"/>가
        /// 기대값으로 못박는다 — 빈 컬렉션 위의 <c>foreach</c>는 아무것도 재지 않으면서 초록이 된다
        /// (이 저장소의 거짓 통과 #5).</para>
        /// </summary>
        private static readonly Dictionary<string, string> PendingPanelWiring = new Dictionary<string, string>();

        [Test]
        public void 차단막_소유자는_등급1_창구를_읽는다()
        {
            var owners = new List<string>();
            foreach (string path in Directory.GetFiles(InteractionDirectory, "*.cs", SearchOption.AllDirectories))
            {
                if (CreatesFullRectBlocker(File.ReadAllText(path))) owners.Add(Path.GetFileName(path));
            }
            owners.Sort(StringComparer.Ordinal);

            // ★ 0개를 찾고 전부 통과하는 것을 먼저 막는다(이 저장소의 거짓 통과 #5 형태).
            //   2026-09-02 현재 정확히 4개: CharacterInfoWindow / PopoverPanel / SettingsWindow / TodoPostItWidget.
            Assert.GreaterOrEqual(owners.Count, 4,
                $"차단막(Blocker) 소유자를 {owners.Count}개밖에 못 찾았다({string.Join(", ", owners)}) — " +
                "이름 규약이 바뀌었다면 CreatesFullRectBlocker를 함께 고쳐라.");
            CollectionAssert.Contains(owners, "PopoverPanel.cs",
                "팝오버 기반 클래스가 차단막 소유자로 잡히지 않는다 — 스캔이 깨졌다.");

            var missing = new List<string>();
            foreach (string file in owners)
            {
                if (Exempt.ContainsKey(file)) continue;
                if (!PollsPanelChannel(File.ReadAllText(Path.Combine(InteractionDirectory, file)))) missing.Add(file);
            }

            var stillPending = new List<string>();
            var unexpected = new List<string>();
            foreach (string file in missing)
            {
                if (PendingPanelWiring.ContainsKey(file)) stillPending.Add(file);
                else unexpected.Add(file);
            }

            Assert.IsEmpty(unexpected,
                "차단막을 만들면서 등급 1 창구(ArePanelsSuppressed)를 읽지 않는 표면이 있다: " +
                $"{string.Join(", ", unexpected)}. 이 표면은 게임이 아닌 전체화면 앱(발표·화상회의) 위에 " +
                "그대로 그려지고 그 사각형의 클릭까지 먹는다 — 정보창 하나가 화면의 50.38%를 덮은 것이 " +
                "이번 출시 Blocker의 실측이다. IsSuspended는 등급 1에서 false이므로 그것만으로는 부족하다.");

            if (stillPending.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append("등급 1 배선이 아직 안 끝난 차단막 소유자 ").Append(stillPending.Count).Append("개: ");
                foreach (string file in stillPending)
                {
                    sb.Append(file).Append(" — ").Append(PendingPanelWiring[file]).Append(' ');
                }
                Assert.Ignore(sb.ToString());
            }
        }

        [Test]
        public void 미배선_목록은_실제로_존재하고_아직_배선되지_않은_파일만_담는다()
        {
            // ★ 2026-09-02 — PendingPanelWiring이 <b>비었다</b>. 빈 컬렉션 위의 foreach는 아무것도
            //   단언하지 않으면서 초록불이 된다(같은 파일의 Exempt가 이미 겪은 모양, 거짓 통과 #5).
            //   그래서 "비어 있음"을 <b>기대값으로 명시</b>한다. 누군가 미배선을 다시 추가하면 이 단언이
            //   먼저 걸려서, 아래 파일존재/역방향 검사가 실제로 도는지 눈으로 확인하게 만든다.
            Assert.IsEmpty(PendingPanelWiring,
                "미배선 항목이 새로 생겼다: " + string.Join(", ", PendingPanelWiring.Keys) + ". " +
                "미배선 자체는 허용되지만(사유를 적었다면) 이 단언을 그때 함께 고쳐라 — 이 줄이 없으면 " +
                "아래 foreach는 빈 목록 위에서 아무것도 재지 않는 초록불이 된다. 그리고 항목이 있는 동안 " +
                "차단막_소유자는_등급1_창구를_읽는다는 Assert.Ignore로 떨어져 러너에서 사실상 사라진다.");

            // 배선이 끝났는데 목록만 남으면 이 테스트는 <b>영원히 Ignore</b>가 되어 러너에서 사라진 것과
            // 같아진다. 그래서 "이미 고쳐진 파일이 목록에 남아 있는가"를 반대 방향으로도 잰다.
            foreach (KeyValuePair<string, string> entry in PendingPanelWiring)
            {
                string path = Path.Combine(InteractionDirectory, entry.Key);
                Assert.IsTrue(File.Exists(path),
                    $"미배선 목록의 {entry.Key}가 더 이상 존재하지 않는다 — 목록도 함께 지워라. 사유: {entry.Value}");
                Assert.IsNotEmpty(entry.Value, $"{entry.Key}의 미배선 사유가 비어 있다.");
                Assert.IsFalse(PollsPanelChannel(File.ReadAllText(path)),
                    $"{entry.Key}는 이미 ArePanelsSuppressed를 읽는다 — 배선이 끝났으니 PendingPanelWiring에서 " +
                    "지워라. 남겨 두면 위 테스트가 영원히 Ignore로 떨어져 러너에서 사실상 사라진다.");
            }
        }

        [Test]
        public void 네거티브컨트롤_등급1_스캔이_히트타깃을_차단막으로_보지_않는다()
        {
            // ★ 양성 대조 없는 "없음" 판정을 만들지 않는다(TEAM.md 거짓 통과 #4).
            Assert.IsTrue(CreatesFullRectBlocker("var go = new GameObject(\"SettingsClickBlocker\");"));
            Assert.IsTrue(CreatesFullRectBlocker("var go = new GameObject(GetType().Name + \"Blocker\");"));
            Assert.IsFalse(CreatesFullRectBlocker("var hitGo = new GameObject(\"InfoGearClickTarget\");"),
                "톱니 히트타깃까지 차단막으로 보면 등급 2로 남긴 리더 판정을 테스트가 뒤집는다.");
            Assert.IsFalse(CreatesFullRectBlocker("var go = new GameObject(\"TodoRow\");"));

            Assert.IsTrue(PollsPanelChannel("if (_agent.ArePanelsSuppressed) return;"));
            Assert.IsFalse(PollsPanelChannel("if (_agent.IsSuspended) return;"),
                "IsSuspended만 읽는 파일을 등급 1 배선으로 세면 이 검사 전체가 무의미해진다.");
            Assert.IsTrue(PollsAnySuspendChannel("if (_agent.IsSuspended) return;"));
            Assert.IsFalse(PollsAnySuspendChannel("if (_open) return;"));
        }

        [Test]
        public void 네거티브컨트롤_스캔은_아무_파일이나_소유자로_보지_않는다()
        {
            // 위 단언이 "항상 참"이 아님을 보인다 — 차단막을 만들지 않는 평범한 파일은 잡히면 안 된다.
            Assert.IsFalse(CreatesClickBlocker("var go = new GameObject(\"TodoRow\");"),
                "차단막이 아닌 GameObject까지 소유자로 잡으면 이 감사는 소음이 된다.");
            Assert.IsTrue(CreatesClickBlocker("var go = new GameObject(\"TodoPostItClickBlocker\");"));
            Assert.IsTrue(CreatesClickBlocker("var go = new GameObject(GetType().Name + \"Blocker\");"));
        }
    }
}
