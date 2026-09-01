using NUnit.Framework;
using StickMate.Platform;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// 절대 불변 원칙 2 회귀 잠금 — <b>"전체화면 <i>게임</i>"에서만 숨는다.</b>
    ///
    /// <para>배경(2026-08-31, 디버거 실측 -> 리더 결정): macOS의 전체화면 판정이 기하(창 bounds ==
    /// 디스플레이 bounds)만 봤기 때문에 엑셀/키노트/브라우저를 전체화면으로 쓰는 동안에도 캐릭터가
    /// 사라졌다. 원칙 2의 문구는 "전체화면 <b>게임</b> 감지 시 자동 숨김"이므로, 앱 번들의
    /// <c>LSApplicationCategoryType</c>이 게임 계열일 때만 숨기도록 좁혔다.</para>
    ///
    /// <para><b>왜 이 형태로 테스트하는가</b>: 실제 조회 경로(NSRunningApplication -> NSBundle ->
    /// Info.plist)는 네이티브이고 배치 테스트에서 재현할 수 없다. 그래서 "카테고리 문자열 -> 숨김
    /// 여부"라는 규칙만 <see cref="FullscreenGameCategory"/>로 분리했고, 여기서 그 규칙을 잠근다.
    /// 네이티브 조회는 이 함수에 입력값을 공급하는 역할만 한다(MacWindowService.QueryAppCategory).</para>
    ///
    /// <para><b>정직한 한계</b>: "실제 게임 앱이 정말 이 카테고리를 선언하는가"는 앱 개발자에게 달린
    /// 문제라 코드로 잠글 수 없다. 미선언 앱을 어떻게 취급할지가 그 한계에 대한 우리의 정책이며(아래
    /// 세 번째 케이스), 보수적으로 "숨기지 않음"을 택했다.</para>
    /// </summary>
    public class FullscreenGameSuspendPolicyTests
    {
        [Test]
        public void 게임카테고리면_숨긴다()
        {
            Assert.IsTrue(FullscreenGameCategory.IsGameCategory("public.app-category.games"),
                "게임 대분류는 반드시 게임으로 판정되어야 한다.");
        }

        [Test]
        public void 게임_세부장르도_전부_게임으로_본다()
        {
            // 실제 App Store 게임 대부분은 대분류가 아니라 세부 장르를 선언한다.
            string[] genres =
            {
                "public.app-category.action-games", "public.app-category.adventure-games",
                "public.app-category.arcade-games", "public.app-category.board-games",
                "public.app-category.card-games", "public.app-category.casino-games",
                "public.app-category.dice-games", "public.app-category.educational-games",
                "public.app-category.family-games", "public.app-category.kids-games",
                "public.app-category.music-games", "public.app-category.puzzle-games",
                "public.app-category.racing-games", "public.app-category.role-playing-games",
                "public.app-category.simulation-games", "public.app-category.sports-games",
                "public.app-category.strategy-games", "public.app-category.trivia-games",
                "public.app-category.word-games",
            };
            foreach (string genre in genres)
            {
                Assert.IsTrue(FullscreenGameCategory.IsGameCategory(genre), $"{genre}는 게임이어야 한다.");
            }
        }

        [Test]
        public void 비게임카테고리면_숨기지_않는다()
        {
            // 사용자가 실제로 전체화면으로 쓰다가 캐릭터가 사라졌던 앱들의 카테고리.
            string[] nonGames =
            {
                "public.app-category.productivity",   // 키노트/넘버스/오피스 계열
                "public.app-category.business",
                "public.app-category.developer-tools",
                "public.app-category.utilities",
                "public.app-category.video",
                "public.app-category.graphics-design",
                "public.app-category.social-networking",
            };
            foreach (string category in nonGames)
            {
                Assert.IsFalse(FullscreenGameCategory.IsGameCategory(category),
                    $"{category}는 게임이 아니므로 캐릭터를 숨기면 안 된다(원칙 2의 문구는 '게임').");
            }
        }

        [Test]
        public void 카테고리_미선언이면_숨기지_않는다()
        {
            Assert.IsFalse(FullscreenGameCategory.IsGameCategory(null),
                "Info.plist에 LSApplicationCategoryType이 없는 앱은 게임으로 추정하지 않는다.");
            Assert.IsFalse(FullscreenGameCategory.IsGameCategory(string.Empty),
                "빈 문자열도 미선언과 같게 취급한다.");
        }

        [Test]
        public void 게임처럼_생긴_다른_문자열에_속지_않는다()
        {
            // 접두사가 없는 임의 문자열이나 "games"가 섞인 비UTI 문자열을 게임으로 오인하면
            // 원칙 2 위반이 조용히 되살아난다.
            Assert.IsFalse(FullscreenGameCategory.IsGameCategory("games"));
            Assert.IsFalse(FullscreenGameCategory.IsGameCategory("com.example.action-games"));
            Assert.IsFalse(FullscreenGameCategory.IsGameCategory("public.app-category.gamesomething"));
        }
    }

    /// <summary>
    /// 부수 발견 회귀 잠금(2026-08-31, 디버거) — <b>메뉴바 호출로 전체화면 판정이 깜빡이면 안 된다.</b>
    ///
    /// <para>같은 전체화면 창인데도 커서를 화면 상단에 올려 메뉴바를 부르면 CGWindow bounds가
    /// <c>(0,33 ...)</c> 과 <c>(0,0 ...)</c> 사이를 오간다. 기하 판정이 그때마다 뒤집혀 Resume/Suspend가
    /// 반복되면 캐릭터가 깜빡이고 프레임 등급도 요동친다. <see cref="FullscreenVerdictDebouncer"/>가
    /// "바뀐 값이 연속으로 유지될 때만 확정"으로 이를 흡수한다.</para>
    /// </summary>
    public class FullscreenVerdictDebouncerTests
    {
        private const double Hold = 1.0;

        [Test]
        public void 최초_관측은_즉시_확정한다()
        {
            var d = new FullscreenVerdictDebouncer();
            // 앱 시작 시점에 이미 전체화면 게임이 떠 있었다면 지연 없이 그 상태로 시작해야 한다.
            Assert.IsTrue(d.Update(true, 0.0, Hold));
        }

        [Test]
        public void 유지시간을_채우기_전에는_전환하지_않는다()
        {
            var d = new FullscreenVerdictDebouncer();
            d.Update(false, 0.0, Hold);
            Assert.IsFalse(d.Update(true, 0.2, Hold), "0.2초만 지났으므로 아직 확정하면 안 된다.");
            Assert.IsFalse(d.Update(true, 0.9, Hold), "후보 관측 시각(0.2초)부터 0.7초뿐이라 아직 미달이다.");
            Assert.IsTrue(d.Update(true, 1.25, Hold), "후보가 1.0초 이상 연속 유지됐으므로 확정된다.");
        }

        [Test]
        public void 되돌아오는_깜빡임은_흡수된다()
        {
            var d = new FullscreenVerdictDebouncer();
            d.Update(false, 0.0, Hold);

            // 메뉴바 호출로 bounds가 튀는 상황 재현: true/false가 짧게 번갈아 들어온다.
            for (int i = 1; i <= 20; i++)
            {
                bool raw = (i % 2) == 0;
                Assert.IsFalse(d.Update(raw, i * 0.3, Hold),
                    "연속 유지되지 않는 값은 몇 번을 흔들려도 확정되면 안 된다.");
            }
        }

        [Test]
        public void 유지시간이_0이면_즉시_반영된다()
        {
            var d = new FullscreenVerdictDebouncer();
            d.Update(false, 0.0, 0.0);
            Assert.IsTrue(d.Update(true, 0.0, 0.0), "디바운스를 끈 설정에서는 원시 판정이 그대로 나와야 한다.");
        }

        [Test]
        public void 확정_후_반대방향_전환도_같은_규칙을_따른다()
        {
            var d = new FullscreenVerdictDebouncer();
            d.Update(false, 0.0, Hold);
            d.Update(true, 0.1, Hold);
            Assert.IsTrue(d.Update(true, 1.2, Hold));

            // 게임을 껐다 -> 곧바로 되돌리지 않고 1초 유지되어야 캐릭터가 돌아온다.
            Assert.IsTrue(d.Update(false, 1.3, Hold));
            Assert.IsFalse(d.Update(false, 2.5, Hold));
        }
    }
}
