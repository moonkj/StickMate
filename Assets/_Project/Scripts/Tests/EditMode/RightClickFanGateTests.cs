using System.IO;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Dialogue;
using StickMate.Interaction;
using StickMate.Platform;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <b>캐릭터 우클릭 → 부채꼴</b>의 순수 판정과 기하를 씬 없이 잠근다 — 2026-09-05.
    /// 설계 정본: <c>docs/UX_RIGHTCLICK_FAN_MENU.md</c> · 테스트 설계:
    /// <c>docs/TEST_PLAN_FAN_AND_EQUIPMENT.md</c> F1 · F5c · F6′ · F7′ · F18 · F24(순수 절반).
    ///
    /// ============================================================================
    /// 이 파일이 EditMode인 이유
    /// ============================================================================
    /// 재는 대상이 전부 <b>OS 호출 0줄 순수 함수</b>다(테스트 설계 C-1). PlayMode로 옮기면 같은
    /// 판정을 씬·프레임·플랫폼 서비스를 거쳐 <b>간접적으로</b> 재게 되고, 그때는 «게이트가 틀렸다»와
    /// «씬이 안 떴다»가 구분되지 않는다.
    ///
    /// ============================================================================
    /// 숫자를 베끼지 않는다
    /// ============================================================================
    /// 각도·바이어스·예산은 전부 프로덕션 상수를 <b>참조</b>한다. 이 저장소가 2026-09-01에
    /// 하드코딩 잔존으로 4건을 잃은 형태(기준과 대상이 갈라져도 아무도 모른다)를 피하기 위해서다.
    /// 단 하나 예외가 <see cref="ShippedGearCenterPoints"/>인데, 그것은 <b>상수의 사본이 아니라
    /// 「출하 기본 화면이 이 자리였다」는 역사적 사실의 고정 표식</b>이고, 그래서 그 값이 바뀌면
    /// 이 테스트가 «회귀»를 말하는 것이 맞다(UX_FLOW 36-3-4).
    /// </summary>
    public sealed class RightClickFanGateTests
    {
        /// <summary>출하 기본 톱니 중심(pt) — UX_FLOW 36-3-4가 못박은 θ₀ = 225°의 그 자리.</summary>
        private static readonly Vector2 ShippedGearCenterPoints = new Vector2(1482f, 924f);

        /// <summary>실측 화면(pt). §1-4 스윕이 쓴 것과 같은 크기.</summary>
        private static readonly Vector2 ScreenPoints = new Vector2(1512f, 982f);

        // ==================================================================
        // F1 — 수용 조건 진리표 2⁵ = 32행 전수
        // ==================================================================

        /// <summary>다섯 항의 32행을 <b>루프로</b> 만든다 — 손으로 적으면 그 표가 곧 두 번째 구현이 된다.</summary>
        private static bool Bit(int row, int index) => ((row >> index) & 1) != 0;

        [Test]
        public void F1_커서가_캐릭터_밖이면_32행_중_16행이_전부_거짓이다()
        {
            int falseRows = 0;
            int trueRowsOverall = 0;

            for (int row = 0; row < 32; row++)
            {
                bool cursorOver = Bit(row, 0);
                bool rising = Bit(row, 1);
                bool swallowAllows = Bit(row, 2);
                bool suppressed = Bit(row, 3);
                bool primaryHeld = Bit(row, 4);

                bool open = AppControlDirector.RightClickFanGatePolicy.ShouldOpenFan(
                    cursorOver, rising, swallowAllows, suppressed, primaryHeld);

                if (open) trueRowsOverall++;
                if (cursorOver) continue;

                falseRows++;
                Assert.IsFalse(open,
                    $"커서가 캐릭터 밖인 행({row:00})이 부채꼴을 엽니다 — 캐릭터 밖 우클릭은 " +
                    "절대 잡히면 안 됩니다(비침해 원칙 2: 남의 앱 위에서 남의 문맥 메뉴를 가로챈다).");
            }

            Assert.AreEqual(16, falseRows, "커서 항이 거짓인 행은 정확히 16개여야 합니다(2⁴).");

            // ★ 양성 대조 — 「전부 거짓」이 «조건이 죽어서»가 아님을 같은 실행 안에서 증명한다.
            //   이 단언이 없으면 ShouldOpenFan이 항상 false를 돌려줘도 위 루프가 초록이다.
            Assert.AreEqual(1, trueRowsOverall,
                "32행 중 참인 행이 정확히 1개여야 합니다(다섯 항이 전부 원하는 값일 때 딱 한 조합). " +
                "0개면 이 테스트가 아무것도 재지 않은 것입니다.");
        }

        [Test]
        public void F1_음성대조_커서항을_상수참으로_바꾸면_최소_한_행이_뒤집힌다()
        {
            int flipped = 0;
            for (int row = 0; row < 32; row++)
            {
                bool real = AppControlDirector.RightClickFanGatePolicy.ShouldOpenFan(
                    Bit(row, 0), Bit(row, 1), Bit(row, 2), Bit(row, 3), Bit(row, 4));

                // 커서 항만 상수 true로 치환한 식(= 그 항을 없앤 세상).
                bool withoutCursorTerm = AppControlDirector.RightClickFanGatePolicy.ShouldOpenFan(
                    true, Bit(row, 1), Bit(row, 2), Bit(row, 3), Bit(row, 4));

                if (real != withoutCursorTerm) flipped++;
            }

            Assert.Greater(flipped, 0,
                "커서 항을 없애도 결과가 한 행도 안 바뀝니다 — 그 항이 실제로는 아무 일도 하지 않는다는 뜻입니다.");
        }

        [Test]
        public void F18_전체화면_억제_중에는_어떤_조합도_열지_않는다()
        {
            int checkedRows = 0;
            int flipped = 0;

            for (int row = 0; row < 16; row++)
            {
                bool cursorOver = Bit(row, 0);
                bool rising = Bit(row, 1);
                bool swallowAllows = Bit(row, 2);
                bool primaryHeld = Bit(row, 3);

                checkedRows++;
                Assert.IsFalse(AppControlDirector.RightClickFanGatePolicy.ShouldOpenFan(
                        cursorOver, rising, swallowAllows, panelsSuppressed: true, primaryButtonHeld: primaryHeld),
                    $"전체화면 억제 중인데 열립니다(행 {row:00}) — 원칙 2 정면 위반입니다.");

                bool suppressedOff = AppControlDirector.RightClickFanGatePolicy.ShouldOpenFan(
                    cursorOver, rising, swallowAllows, panelsSuppressed: false, primaryButtonHeld: primaryHeld);
                if (suppressedOff) flipped++;
            }

            Assert.AreEqual(16, checkedRows);
            // ★ 양성 대조 — 억제를 풀면 실제로 열리는 행이 있다(빈 조건이 아니다).
            Assert.Greater(flipped, 0, "억제를 풀어도 열리는 행이 하나도 없습니다 — 측정기가 죽었습니다.");
        }

        [Test]
        public void F24_좌버튼을_잡고_있는_동안에는_열지_않는다()
        {
            for (int row = 0; row < 8; row++)
            {
                Assert.IsFalse(AppControlDirector.RightClickFanGatePolicy.ShouldOpenFan(
                        Bit(row, 0), Bit(row, 1), Bit(row, 2), panelsSuppressed: false, primaryButtonHeld: true),
                    "좌버튼으로 잡고 있는 중에 부채꼴이 열립니다 — 던지려던 동작을 메뉴가 가로챕니다.");
            }

            // ★ 양성 대조 — 손을 떼면 같은 조합이 열린다.
            Assert.IsTrue(AppControlDirector.RightClickFanGatePolicy.ShouldOpenFan(
                cursorOverCharacter: true, secondaryRisingEdge: true, swallowAllowsOpen: true,
                panelsSuppressed: false, primaryButtonHeld: false));
        }

        // ==================================================================
        // F5c — 삼킴 상태 fail-open (리더 판정 L-2)
        // ==================================================================

        [Test]
        public void F5c_삼킴상태_세_갈래가_실제로_갈린다()
        {
            // (가) 미지원 — 못 읽으면 «연다». 이것이 L-2의 전부다.
            Assert.IsTrue(AppControlDirector.RightClickFanGatePolicy.SwallowAllowsOpen(queried: false, swallowed: false),
                "삼킴 상태를 못 읽었는데 닫아걸었습니다 — fail-closed로 되돌린 변경입니다(리더 판정 L-2 위반). " +
                "그 실패는 «우클릭이 아예 안 먹는다»이고, 사용자가 신고한 바로 그 증상이며, 조용합니다.");

            // 미지원이면 swallowed 값이 무엇이든 결과가 같아야 한다(읽지 못한 값을 보면 안 된다).
            Assert.IsTrue(AppControlDirector.RightClickFanGatePolicy.SwallowAllowsOpen(queried: false, swallowed: true));

            // (나) 삼켜짐 — 연다.
            Assert.IsTrue(AppControlDirector.RightClickFanGatePolicy.SwallowAllowsOpen(queried: true, swallowed: true));

            // (다) ★ 명시적 미삼킴 — <b>이 한 갈래만</b> 닫는다. 여기가 1렌더프레임 경합을 닫는 자리다.
            Assert.IsFalse(AppControlDirector.RightClickFanGatePolicy.SwallowAllowsOpen(queried: true, swallowed: false),
                "OS가 «이 클릭은 우리 것이 아니다»라고 답했는데도 엽니다 — 게이트가 아무 일도 하지 않습니다.");
        }

        // ==================================================================
        // F6′ — Snap45 회귀 잠금 (§1-3-4 「유일한 함정」)
        // ==================================================================

        [Test]
        public void F6_바이어스가_0이면_SnapFanBaseAngle은_Snap45와_전_격자에서_같다()
        {
            int compared = 0;
            for (float x = 0f; x <= ScreenPoints.x; x += 37f)
            {
                for (float y = 0f; y <= ScreenPoints.y; y += 29f)
                {
                    var anchor = new Vector2(x, y);
                    float legacy = GearRadialMenuWidget.Snap45(ScreenPoints * 0.5f - anchor);
                    float now = GearRadialMenuWidget.SnapFanBaseAngle(anchor, ScreenPoints, 0f);
                    compared++;
                    Assert.AreEqual(legacy, now,
                        $"앵커 {anchor}에서 갈립니다 — 바이어스 0은 옛 식과 <b>비트 동일</b>해야 합니다.");
                }
            }
            Assert.Greater(compared, 0, "격자가 비어 있습니다 — 이 테스트가 아무것도 재지 않았습니다.");
        }

        [Test]
        public void F6_출하_기본_톱니자리의_기준각은_이_변경으로_움직이지_않는다()
        {
            float shipped = GearRadialMenuWidget.SnapFanBaseAngle(ShippedGearCenterPoints, ScreenPoints, 0f);
            Assert.AreEqual(225f, shipped, 0.001f,
                "출하 기본 톱니 자리의 θ₀가 225°가 아닙니다 — UX_FLOW 36-3-4가 못박은 기본 화면이 바뀌었습니다.");

            // ★ 음성 대조 — 바이어스를 <b>톱니 경로에</b> 태우면 그 화면이 실제로 깨진다.
            //   이 단언이 「분리된 함수」의 존재 이유 전부다(§1-3-4).
            float poisoned = GearRadialMenuWidget.SnapFanBaseAngle(
                ShippedGearCenterPoints, ScreenPoints, GearRadialMenuWidget.FanUpBiasPoints);
            Assert.AreNotEqual(shipped, poisoned,
                "바이어스를 태워도 톱니 기본각이 그대로입니다 — 바이어스가 아무 일도 하지 않거나 " +
                "이 테스트가 함정을 재현하지 못하고 있습니다.");
            Assert.AreEqual(180f, poisoned, 0.001f,
                "함정의 실제 값은 180°입니다(§1-3-4). 값이 다르면 기하 전제가 바뀐 것이므로 설계를 다시 보세요.");
        }

        [Test]
        public void F6_바이어스는_도달_반경에서_유도된다()
        {
            // 새 상수를 「고른 값」으로 남기지 않는다: 위성 궤도 + 클램프 상자 반폭.
            float derived = GearRadialMenuWidget.SatelliteOrbitRadiusPoints
                + (GearRadialMenuWidget.ButtonDiameterPoints + GearRadialMenuWidget.ClampBoxPaddingPoints) * 0.5f;
            Assert.AreEqual(derived, GearRadialMenuWidget.FanUpBiasPoints, 0.001f,
                "FanUpBiasPoints가 「위성 궤도 + 클램프 상자 반폭」과 갈라졌습니다 — 임의값이 되었습니다(§1-3-3).");
        }

        // ==================================================================
        // F7′ — 화면 중앙선의 180° 반전이 사라졌는가
        // ==================================================================

        private static int CountSharpReversals(float upBias, float scanY, out float firstReversalX)
        {
            firstReversalX = float.NaN;
            int reversals = 0;
            float prev = GearRadialMenuWidget.SnapFanBaseAngle(new Vector2(0f, scanY), ScreenPoints, upBias);
            for (float x = 1f; x <= ScreenPoints.x; x += 1f)
            {
                float now = GearRadialMenuWidget.SnapFanBaseAngle(new Vector2(x, scanY), ScreenPoints, upBias);
                float delta = Mathf.Abs(Mathf.DeltaAngle(prev, now));
                if (delta >= 135f)
                {
                    reversals++;
                    if (float.IsNaN(firstReversalX)) firstReversalX = x;
                }
                prev = now;
            }
            return reversals;
        }

        [Test]
        public void F7_바이어스가_화면_중앙선의_180도_반전을_없앤다()
        {
            float centerY = ScreenPoints.y * 0.5f;

            // ★ 양성 대조 먼저 — 측정기가 살아 있는가. 바이어스 0에서는 반전이 실제로 잡혀야 한다.
            int legacyReversals = CountSharpReversals(0f, centerY, out float legacyX);
            Assert.Greater(legacyReversals, 0,
                "바이어스 0에서도 급반전이 0건입니다 — 스캐너가 죽었습니다(이 상태로는 아래 단언이 무의미합니다).");
            Assert.AreEqual(ScreenPoints.x * 0.5f, legacyX, 1.5f,
                "급반전 지점이 화면 가로 중앙 근처가 아닙니다 — 스캔 전제가 설계와 다릅니다.");

            // 본 단언 — 캐릭터 경로에서는 같은 스캔에 급반전이 0건이다.
            int biased = CountSharpReversals(GearRadialMenuWidget.FanUpBiasPoints, centerY, out _);
            Assert.AreEqual(0, biased,
                "캐릭터가 가장 자주 지나는 높이(화면 세로 중앙선)에서 방향이 여전히 뒤집힙니다 — " +
                "1pt 걸을 때마다 부채꼴이 좌우로 갈립니다(§1-3-2).");
        }

        [Test]
        public void F7_반전_지점은_중앙_위_바이어스만큼_이동한다()
        {
            float movedScanY = ScreenPoints.y * 0.5f + GearRadialMenuWidget.FanUpBiasPoints;
            int reversals = CountSharpReversals(GearRadialMenuWidget.FanUpBiasPoints, movedScanY, out _);
            Assert.Greater(reversals, 0,
                "반전이 사라진 것이 아니라 «중앙 위 바이어스만큼» 옮겨 간 것이어야 합니다 — " +
                "그 자리에서도 0건이면 스캔이 잘못됐거나 바이어스가 안 걸린 것입니다.");
        }

        // ==================================================================
        // 호명 반응 비트 — 붙잡는 시간은 대사 예산에서 나온다 (MOTION §1-8-4)
        // ==================================================================

        [Test]
        public void 대사가_없으면_붙잡기는_펼침_예산과_같다()
        {
            Assert.AreEqual(GearRadialMenuWidget.ExpandTotalSeconds,
                AppControlDirector.ReactionHoldSecondsFor(null), 1e-5f);
            Assert.AreEqual(GearRadialMenuWidget.ExpandTotalSeconds,
                AppControlDirector.ReactionHoldSecondsFor(string.Empty), 1e-5f);
        }

        [Test]
        public void 대사가_있으면_붙잡기는_그_대사의_노출_상한이다()
        {
            string line = AppControlDirector.PlaceholderReactionLine;

            // 기대값은 프로덕션 예산 함수에서 <b>독립적으로</b> 만든다(같은 상수를 두 번 타이핑하지 않는다).
            float expected = DialogueBudget.MaxVisibleSecondsFor(line,
                DialogueTiming.PopInSeconds, DialogueTiming.FadeOutSeconds);

            Assert.AreEqual(expected, AppControlDirector.ReactionHoldSecondsFor(line), 1e-5f,
                "붙잡는 시간이 대사 노출 상한과 갈라졌습니다 — 그만큼 말풍선이 메뉴에서 떨어져 나갑니다.");

            // ★ 양성 대조 — 그 값이 「대사 없음」 분기와 실제로 다르다(둘이 같으면 위 단언이 공허하다).
            Assert.Greater(expected, GearRadialMenuWidget.ExpandTotalSeconds,
                "대사 예산이 펼침 예산보다 크지 않습니다 — 두 분기가 구분되지 않습니다.");
        }

        [Test]
        public void 반응_대사_공급자는_기본적으로_비어_있다()
        {
            // design-narrative 문구 대기 — 지금은 아무 말도 하지 않는 것이 <b>정직한 상태</b>다.
            // 문구가 확정되면 이 단언을 함께 고쳐라(그때 이 줄이 「누가 문구를 넣었는가」를 묻는다).
            Assert.IsNull(AppControlDirector.ReactionLineProvider,
                "반응 대사 공급자가 꽂혀 있습니다 — 문구가 확정됐다면 이 테스트도 함께 갱신하세요.");
        }

        // ==================================================================
        // 소스 감사 — F3′ · F4 · F20c · 앵커
        // ==================================================================

        private static string Root => Path.Combine(Application.dataPath, "_Project", "Scripts");

        private static string ReadSource(params string[] parts)
        {
            string path = Path.Combine(Root, Path.Combine(parts));
            Assert.IsTrue(File.Exists(path), $"소스를 찾지 못했습니다: {path}");
            return File.ReadAllText(path).Replace("\r\n", "\n");
        }

        private static string StripComments(string source)
        {
            string[] lines = source.Split('\n');
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("///")) continue;
                int idx = lines[i].IndexOf("//", System.StringComparison.Ordinal);
                sb.Append(idx >= 0 ? lines[i].Substring(0, idx) : lines[i]).Append('\n');
            }
            return sb.ToString();
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, System.StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }

        [Test]
        public void F3_우클릭_수용_경로는_클릭관통을_추가로_해제하지_않는다()
        {
            string director = StripComments(ReadSource("Interaction", "AppControlDirector.cs"));

            // 부재 단언 — 우클릭을 받는 그 자리에서 관통을 건드리면 비침해가 실제로 나빠진다.
            Assert.AreEqual(-1, director.IndexOf("SetClickThrough", System.StringComparison.Ordinal),
                "우클릭 경로가 클릭관통을 직접 건드립니다 — 관통 해제는 히트테스트가 상시로 하고 있고, " +
                "여기서 추가로 해제하면 그만큼 남의 앱의 클릭을 더 먹습니다(원칙 2).");
            Assert.AreEqual(-1, director.IndexOf("ILocalClickCaptureService", System.StringComparison.Ordinal),
                "우클릭 경로가 별도의 클릭 포획 서비스를 씁니다 — 설계에 없는 삼킴 수단입니다(dev-platform §2-6에서 전부 기각).");

            // ★ 부재 단언에 존재 대조를 붙인다 — 프로브가 살아 있는지 같은 실행 안에서 증명한다.
            //   (같은 스캐너가 이 파일의 다른 실재 심볼은 실제로 찾아낸다.)
            StringAssert.Contains(nameof(IGlobalPointerButtonService), director,
                "우클릭 채널 자체가 이 파일에 없습니다 — 위 두 «없음» 판정이 무효입니다(파일을 잘못 읽었습니다).");
        }

        [Test]
        public void F4_우클릭_조회_호출부는_정확히_한_곳이다()
        {
            string director = StripComments(ReadSource("Interaction", "AppControlDirector.cs"));
            string needle = nameof(IGlobalPointerButtonService.TryGetSecondaryButtonPressed);

            Assert.AreEqual(1, CountOccurrences(director, needle),
                $"{needle} 호출부가 하나가 아닙니다 — 폴링 주체가 둘이 되면 «누가 우클릭을 먹었나»가 갈립니다.");

            // ★ 존재 대조 — 같은 스캐너가 좌클릭 조회도 실제로 찾아낸다(니들이 살아 있다).
            Assert.Greater(CountOccurrences(director, nameof(IGlobalPointerButtonService.TryGetPrimaryButtonPressed)), 0,
                "좌클릭 조회를 못 찾았습니다 — 스캐너가 죽었을 가능성이 높습니다.");
        }

        [Test]
        public void F20b_허가는_반드시_펼침보다_앞에서_난다()
        {
            string director = StripComments(ReadSource("Interaction", "AppControlDirector.cs"));

            int grant = director.IndexOf(nameof(StickmanAgent.TryGrantUserSummon), System.StringComparison.Ordinal);
            int expand = director.IndexOf(nameof(GearRadialMenuWidget.ExpandOrReanchor), System.StringComparison.Ordinal);

            Assert.Greater(grant, 0, "우클릭 경로에 등급 1 허가 발급이 없습니다 — 전체화면 앱 위에서 우클릭이 먹지 않습니다.");
            Assert.Greater(expand, 0, "우클릭 경로에 펼침 호출이 없습니다.");
            Assert.Less(grant, expand,
                "허가가 펼침보다 <b>뒤</b>에 있습니다 — 부채꼴은 자기 LateUpdate에서 ArePanelsSuppressed를 " +
                "폴링해 스스로 접으므로, 펼쳐지는 그 프레임에 회수되어 «우클릭이 안 먹는다»가 됩니다. " +
                "2026-09-03에 톱니에서 실제로 났던 증상입니다.");
        }

        [Test]
        public void F20c_허가_발급_지점은_세_곳이다()
        {
            string needle = nameof(StickmanAgent.TryGrantUserSummon);
            string[][] issuers =
            {
                new[] { "Interaction", "InfoGearIconWidget.cs" },
                new[] { "Interaction", "SettingsWindow.cs" },
                new[] { "Interaction", "AppControlDirector.cs" },
            };

            int found = 0;
            foreach (string[] parts in issuers)
            {
                string src = StripComments(ReadSource(parts));
                if (CountOccurrences(src, needle + "(") > 0) found++;
            }

            Assert.AreEqual(issuers.Length, found,
                $"허가 발급 지점이 {found}곳입니다 — 톱니 클릭 · 설정창 열기 · 캐릭터 우클릭 세 곳이어야 합니다. " +
                "하나라도 빠지면 그 진입점은 등급 1(전체화면 앱 위)에서 통째로 죽습니다.");

            // ★ 음성 대조 — 존재하지 않는 심볼로 같은 스캐너를 돌리면 0곳이어야 한다(프로브 생존).
            int ghosts = 0;
            foreach (string[] parts in issuers)
            {
                if (CountOccurrences(StripComments(ReadSource(parts)), "ZzzNotARealSymbolQqq(") > 0) ghosts++;
            }
            Assert.AreEqual(0, ghosts, "가짜 심볼이 검출됐습니다 — 이 감사 전체가 무효입니다.");
        }

        [Test]
        public void 앵커는_머리가_아니라_몸_중심이다()
        {
            string director = StripComments(ReadSource("Interaction", "AppControlDirector.cs"));

            StringAssert.Contains("CharacterHeightWorld * 0.5f", director,
                "앵커가 «발밑 + 신장/2»(= 몸 중심)로 계산되지 않습니다. 머리 중심에 걸면 배율 1.00의 " +
                "아래 세 방향에서 버튼 히트원이 캐릭터 잡기영역을 −5.21pt 관통합니다(§1-2).");
        }

        [Test]
        public void F25_잡기_억제는_부채꼴의_판정식_하나만_부른다()
        {
            string hitbox = StripComments(ReadSource("Interaction", "StickmanClickHitbox.cs"));

            StringAssert.Contains(nameof(GearRadialMenuWidget.HitTest), hitbox,
                "부채꼴 버튼 위에서 캐릭터 잡기를 억제하는 가드가 없습니다 — 세로 일렬 폴백/평행이동에서 " +
                "같은 클릭이 «버튼 누름»과 «캐릭터 잡기»로 둘 다 해석됩니다(§5-4).");

            // ★ 판정식을 두 벌 쓰지 않았는가 — 히트박스가 자기 원·반지름 계산을 다시 만들지 않았다.
            Assert.AreEqual(-1, hitbox.IndexOf(nameof(GearRadialMenuWidget.OrbitRadiusPoints), System.StringComparison.Ordinal),
                "히트박스가 부채꼴의 궤도 상수를 직접 읽습니다 — 판정식이 두 벌이 되면 배치 사다리와 " +
                "조용히 갈라지고, 그 갈라짐은 «가끔 캐릭터가 안 잡힌다»로만 나타납니다.");
        }

        [Test]
        public void 대기_톱니는_사용자숨김과_가출_두_상태를_본다()
        {
            string gear = StripComments(ReadSource("Interaction", "InfoGearIconWidget.cs"));

            StringAssert.Contains(nameof(StickmanAgent.IsUserHiddenOnly), gear,
                "대기 톱니가 사용자 명시 숨김을 보지 않습니다.");
            StringAssert.Contains(nameof(RunawayDirector.IsRunawayActive), gear,
                "대기 톱니가 가출을 보지 않습니다 — 가출 Hidden은 최대 5400초(90분)이고 그동안 " +
                "macOS에는 마우스 진입점이 0개가 됩니다(ux-designer P0-2).");

            // ★★ 리더 판정 L-10 — 설정 토글은 이 조건에 <b>들어가면 안 된다</b>(자기 잠금이 된다).
            //   부재 단언이라 위 두 존재 단언이 프로브 생존을 함께 증명한다.
            Assert.AreEqual(-1, gear.IndexOf(nameof(AppSettingsModel.GearIconVisible), System.StringComparison.Ordinal),
                "★ 「톱니 아이콘」 설정 토글이 다시 게이트에 들어왔습니다 — 그 값은 세이브에 내려가고, " +
                "끄면 대기 톱니가 사라져 되돌리는 문(설정창)에 닿는 마우스 경로가 0이 됩니다. " +
                "2026-09-03 사용자 신고 «다시 나오게 할 방법이 없어»의 정확한 재발입니다(리더 판정 L-10).");
        }

        // ==================================================================
        // 대기 톱니 게이트 — 순수 정책 진리표 (test-engineer 개선 ①)
        // ==================================================================

        [Test]
        public void 대기톱니_진리표_4행_전수()
        {
            // 조건은 「사용자가 숨겼다」가 아니라 <b>「우클릭할 몸이 없다」</b>다 — 두 상태가 OR로 합쳐진다.
            Assert.IsFalse(InfoGearIconWidget.StandbyGearPolicy.ShouldShow(false, false),
                "캐릭터가 화면에 있는데 톱니가 섭니다 — 「평소에는 없다」가 이 라운드의 사용자 지시입니다.");
            Assert.IsTrue(InfoGearIconWidget.StandbyGearPolicy.ShouldShow(true, false),
                "사용자 명시 숨김에서 톱니가 없습니다 — 2026-09-03 확정(«메뉴버튼은 보여야지»)의 회귀입니다.");
            Assert.IsTrue(InfoGearIconWidget.StandbyGearPolicy.ShouldShow(false, true),
                "가출 중에 톱니가 없습니다 — macOS에서 최대 5400초(90분) 마우스 진입점 0입니다(P0-2).");
            Assert.IsTrue(InfoGearIconWidget.StandbyGearPolicy.ShouldShow(true, true));

            // ★ 음성 대조 — 각 항이 실제로 일한다. 항 하나를 상수 false로 죽이면 결과가 바뀌어야 한다.
            int flippedByHidden = 0, flippedByRunaway = 0;
            for (int row = 0; row < 4; row++)
            {
                bool hidden = Bit(row, 0), runaway = Bit(row, 1);
                bool real = InfoGearIconWidget.StandbyGearPolicy.ShouldShow(hidden, runaway);
                if (real != InfoGearIconWidget.StandbyGearPolicy.ShouldShow(false, runaway)) flippedByHidden++;
                if (real != InfoGearIconWidget.StandbyGearPolicy.ShouldShow(hidden, false)) flippedByRunaway++;
            }
            Assert.Greater(flippedByHidden, 0, "사용자 숨김 항이 아무 일도 하지 않습니다.");
            Assert.Greater(flippedByRunaway, 0,
                "가출 항이 아무 일도 하지 않습니다 — P0-2를 닫는 그 항입니다(ux-designer §1-7-3 가-1).");
        }

        [Test]
        public void 대기톱니_사유_문장은_네_상태를_전부_구분한다()
        {
            // 「왜 지금 서 있는가」를 테스트가 게이트를 <b>재구현하지 않고</b> 물어보는 창구(개선 ②).
            string[] said =
            {
                InfoGearIconWidget.StandbyGearPolicy.Describe(false, false),
                InfoGearIconWidget.StandbyGearPolicy.Describe(true, false),
                InfoGearIconWidget.StandbyGearPolicy.Describe(false, true),
                InfoGearIconWidget.StandbyGearPolicy.Describe(true, true),
            };
            CollectionAssert.AllItemsAreNotNull(said);
            CollectionAssert.AllItemsAreUnique(said,
                "네 상태 중 둘이 같은 문장을 냅니다 — 로그를 봐도 어느 쪽인지 못 가릅니다.");
            foreach (string s in said) Assert.IsNotEmpty(s);
        }

        [Test]
        public void 테스트_우회_훅은_프로덕션에서_한_번도_불리지_않는다()
        {
            // ★ 개선 ③ — 이 훅은 릴리스 런타임에 <c>public static</c>으로 실려 있다. 프로덕션이
            //   실수로 부르면 「대기 톱니」 규칙이 통째로 무력해지고, 그 실패는 <b>조용하다</b>
            //   (화면에 톱니가 그냥 보일 뿐이라 아무도 버그로 신고하지 않는다).
            string needle = nameof(InfoGearIconWidget.SetStandbyGateBypassedForTests);

            // 런타임 스크립트 + 에디터 스크립트(SceneBootstrapper 등)를 <b>둘 다</b> 훑는다 —
            // 씬 조립기가 부르면 그것도 프로덕션 경로다.
            var roots = new System.Collections.Generic.List<string> { Root };
            string editorRoot = Path.Combine(Application.dataPath, "Editor");
            if (Directory.Exists(editorRoot)) roots.Add(editorRoot);

            var offenders = new System.Collections.Generic.List<string>();
            int productionFilesScanned = 0;
            int testHits = 0;

            var allFiles = new System.Collections.Generic.List<string>();
            foreach (string r in roots) allFiles.AddRange(Directory.GetFiles(r, "*.cs", SearchOption.AllDirectories));

            foreach (string path in allFiles)
            {
                bool isTest = path.Replace('\\', '/').Contains("/Tests/");
                string src = StripComments(File.ReadAllText(path));
                int hits = CountOccurrences(src, needle);
                if (isTest) { testHits += hits; continue; }

                productionFilesScanned++;
                // 선언 자체는 프로덕션에 있어야 하므로 <b>호출부</b>만 센다.
                if (CountOccurrences(src, needle + "(") > CountOccurrences(src, "void " + needle + "("))
                    offenders.Add(Path.GetFileName(path));
            }

            // ★ 스캔이 죽지 않았음을 먼저 보인다(존재 대조) — 이 두 단언이 없으면 «0건»이
            //   «깨끗함»인지 «아무것도 안 읽음»인지 구분되지 않는다.
            Assert.Greater(productionFilesScanned, 0, "프로덕션 소스를 한 개도 못 읽었습니다 — 경로가 틀렸습니다.");
            Assert.Greater(testHits, 0,
                $"{needle} 호출을 테스트에서도 못 찾았습니다 — 니들이 죽었습니다(PlayMode 격리 픽스처가 부릅니다).");

            Assert.IsEmpty(offenders,
                $"프로덕션이 테스트 우회 훅을 부릅니다: {string.Join(", ", offenders)}. " +
                "그 순간 「평소에는 톱니가 없다」가 전원에게 거짓이 되고, 화면에는 그냥 톱니가 보일 뿐이라 " +
                "아무도 버그로 신고하지 않습니다.");
        }

        [Test]
        public void 차단막은_넓이0인_톱니_사각형의_위치를_삼키지_않는다()
        {
            // ★★ P0 회귀 잠금 (test-engineer 실측) — 톱니가 「평상시 숨김」이면 IconScreenRect는
            //   넓이 0이지만 <b>위치는 화면 우상단 그대로</b>다. 경계상자 합집합은 그 점의 위치까지
            //   삼켜서, 부채꼴과 우상단을 잇는 <b>보이지 않는 띠</b>가 차단막이 된다(원칙 2 위반).
            string gear = StripComments(ReadSource("Interaction", "InfoGearIconWidget.cs"));

            StringAssert.Contains("gearHits", gear,
                "차단막 계산에 「톱니 사각형이 실제로 넓이를 갖는가」 분기가 없습니다 — " +
                "넓이 0인 점의 위치가 합집합에 새어 화면을 가로지르는 클릭 흡수 띠가 됩니다.");

            // 조건부 없이 Union 한 줄로 되돌리면 그 띠가 그대로 돌아온다 — 그 형태를 부재로 못박는다.
            Assert.AreEqual(-1,
                gear.IndexOf("fanVisible ? Union(", System.StringComparison.Ordinal),
                "★ 차단막이 다시 «부채꼴이 보이면 무조건 Union»으로 되돌아갔습니다. " +
                "그 한 줄이 P0의 정확한 형태입니다(실측 640×480에서 정당 면적의 2.31배).");
        }

        [Test]
        public void 죽은_톱니_토글은_설정창에서_사라졌다()
        {
            string settings = StripComments(ReadSource("Interaction", "SettingsWindow.cs"));

            Assert.AreEqual(-1, settings.IndexOf("general.gearIcon", System.StringComparison.Ordinal),
                "「톱니 아이콘」 토글이 설정창에 남아 있습니다 — 게이트가 그 값을 더 이상 읽지 않으므로 " +
                "꺼도 아무 일도 일어나지 않고, 그러면서 캡션은 «끄면 진입점이 사라진다»고 거짓을 말합니다. " +
                "게다가 그 값은 세이브에 내려가 「되돌리는 문이 없는 저장 항목」이 됩니다(41-8 위반).");

            // ★ 존재 대조 — 같은 스캐너가 같은 카드의 다른 행은 실제로 찾아낸다(프로브 생존).
            StringAssert.Contains("general.gearHome", settings,
                "[톱니 위치] 행까지 사라졌습니다 — 그 행은 <b>남겨야 한다</b>(ux-designer W-5 철회): " +
                "톱니는 여전히 드래그로 옮길 수 있고 그 자리는 세이브에 영구히 앉습니다.");
        }

        [Test]
        public void 부채꼴은_자기_임대를_스스로_갱신한다()
        {
            string fan = StripComments(ReadSource("Interaction", "GearRadialMenuWidget.cs"));

            StringAssert.Contains(nameof(StickmanAgent.RenewUserSummonGrant), fan,
                "★ 부채꼴이 자기 임대를 갱신하지 않습니다(game-architect I-28). 톱니가 대신 보고하던 " +
                "그 코드는 톱니의 가시성 게이트 뒤에 있었고, 톱니가 「평상시 숨김」이 된 지금 그 조합은 " +
                "평상시입니다 — 등급 1에서 사용자가 쓰는 도중에 부채꼴이 스스로 걷힙니다.");
        }
    }
}
