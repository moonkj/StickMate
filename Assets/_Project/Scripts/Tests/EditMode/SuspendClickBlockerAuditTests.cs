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
                if (source.IndexOf("IsSuspended", StringComparison.Ordinal) < 0) missing.Add(file);
            }

            Assert.IsEmpty(missing,
                "클릭 차단막을 만들면서 전체화면 감지(IsSuspended)를 한 번도 읽지 않는 표면이 있다: " +
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
