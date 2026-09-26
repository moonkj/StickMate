using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★★ <b>행동 명령창 헤더 한 줄의 파생 규칙</b> — 구조적 잠금 (2026-09-26).
    /// 정본: <c>docs/narrative/ACTION_POPOVER_HEADER_REASON.md</c> §0 (design-narrative 2판).
    ///
    /// ============================================================================
    /// 무엇을 고쳤고 무엇을 잠그는가
    /// ============================================================================
    /// 종전 헤더는 누를 수 있는 칸이 0이면 <b>이유를 묻지 않고</b> 규칙 4(«다른 일 하는 중»)를 썼다.
    /// 그래서 네 칸이 전부 <b>자리·대상 사유</b>로 회색인 장면에서 캐릭터는 한가히 걷는데 헤더가
    /// <b>캐릭터 상태를 지어냈다</b>(절대 불변 원칙 1). 규칙 5를 신설해 갈랐다.
    ///
    /// ============================================================================
    /// 이 파일이 재는 네 가지
    /// ============================================================================
    /// ① <b>상태 enum 전수</b> — «바쁨»의 여집합이 정확히 Idle·Walk 둘인가. 새 상태가 enum에 늘면
    ///    자동으로 바쁨으로 분류되는데, 그것이 의도인지 여기서 사람이 보게 된다.
    /// ② <b>규칙 4는 상태만 본다</b> — 헤더가 연출 락을 읽지 않는다(부재 단언 + <b>존재 짝 대조</b>).
    /// ③ <b>네 Director에 상태 식이 있다</b> — 헤더와 칸 사유가 같은 식에서 나오는지(존재 단언).
    /// ④ <b>사다리의 순서와 상수 경유</b> — 문구 리터럴이 판정 식에 직접 박히지 않았는가.
    ///
    /// <para>★ 문구는 <b>전부 프로덕션 상수 참조</b>다. 글자를 베끼지 않으므로 design-narrative가
    /// 글자를 바꾸는 날 이 단언들이 조용히 초록으로 남지 않는다(CLAUDE.md 니들 규칙).</para>
    /// </summary>
    public sealed class ActionCommandPopoverHeaderRuleTests
    {
        private static string InteractionDirectory =>
            Path.Combine(Application.dataPath, "_Project", "Scripts", "Interaction");

        /// <summary>규칙 2의 문구는 <b>게이트</b>가 집이다 — 명령창은 그 상수를 <b>참조만</b> 한다.
        /// 그래서 그 문구의 존재 대조는 명령창이 아니라 이 폴더에서 해야 한다(실측: 명령창 파일에
        /// 그 리터럴은 <b>0회</b>다 — 여기를 틀리면 존재 짝 대조가 «죽은 니들»로 빨개진다).</summary>
        private static string CoreDirectory =>
            Path.Combine(Application.dataPath, "_Project", "Scripts", "Core");

        private const string PopoverFile = "ActionCommandPopover.cs";
        private const string GateFile = "HiddenCharacterCommandGate.cs";

        /// <summary>헤더가 요약하는 네 칸의 주인들. 헤더의 상태 식은 이들과 <b>같은 식</b>이어야 한다.</summary>
        private static readonly string[] FourDirectors =
        {
            "ArcheryDirector.cs", "GraffitiDirector.cs", "WindowTheftDirector.cs", "WindowCrashDirector.cs",
        };

        // ====================================================================
        // ① 상태 enum 전수 — 한가한 것은 Idle·Walk 둘뿐이다
        // ====================================================================

        /// <summary>
        /// <see cref="ActionCommandPopover.IsBusyHeaderState"/>를 <b>enum 전수</b>로 돌린다.
        ///
        /// <para>★ 왜 전수인가: 설계 문서가 «Idle·Walk가 아닌 것»이라고 적었지만, 그 목록을 테스트에
        /// 손으로 옮겨 적으면 enum이 늘 때 기준과 대상이 <b>같이</b> 낡는다. 그래서 목록을 적지 않고
        /// <c>Enum.GetValues</c>로 전부 돌린 뒤 <b>여집합이 정확히 둘</b>임을 본다.</para>
        ///
        /// <para>새 상태가 늘면 이 단언은 <b>통과한다</b>(자동으로 바쁨이 된다). 늘어난 상태가 한가한
        /// 쪽이어야 한다면 여기서 빨개지도록 여집합을 <b>값으로</b> 못박아 둔 것이다.</para>
        /// </summary>
        [Test]
        public void 바쁨_판정은_상태_enum_전수에서_Idle과_Walk만_한가로_본다()
        {
            var values = (StickmanStateId[])Enum.GetValues(typeof(StickmanStateId));
            string[] names = Enum.GetNames(typeof(StickmanStateId));

            // ★ 빈/깨진 열거 위의 검사는 아무것도 재지 않으면서 초록이 된다(이 저장소의 거짓 통과 #5).
            Assert.AreEqual(names.Length, values.Length,
                "StickmanStateId의 이름 수와 값 수가 다릅니다 — 열거가 깨졌거나 중복 값이 있습니다.");
            Assert.Greater(values.Length, 2,
                $"상태가 {values.Length}개뿐입니다 — 전수 열거가 깨졌습니다. 이 상태로 아래 단언은 " +
                "«여집합이 둘»을 공짜로 통과시킵니다.");

            var idle = new List<StickmanStateId>();
            var busy = new List<StickmanStateId>();
            foreach (StickmanStateId state in values)
            {
                if (ActionCommandPopover.IsBusyHeaderState(state)) busy.Add(state);
                else idle.Add(state);
            }

            CollectionAssert.AreEquivalent(new[] { StickmanStateId.Idle, StickmanStateId.Walk }, idle,
                "헤더가 «한가»로 보는 상태가 Idle·Walk 둘이 아닙니다. 실제로 한가로 분류된 것: " +
                string.Join(", ", idle) + ". 설계 문서(ACTION_POPOVER_HEADER_REASON.md §0)는 규칙 4의 " +
                "바쁨을 «현재 상태가 Idle·Walk가 아님»으로 정의합니다 — 여기가 갈라지면 헤더가 " +
                "캐릭터 상태를 잘못 주장합니다(원칙 1).");

            // 양성 대조: 판정이 «전부 한가»로 죽어 있지 않다.
            Assert.IsNotEmpty(busy,
                "바쁨으로 분류된 상태가 하나도 없습니다 — 판정이 죽었습니다. 그러면 규칙 4가 영원히 " +
                "쓰이지 않고, 진짜로 바쁜 캐릭터에게도 규칙 5가 뜹니다.");
            Assert.AreEqual(values.Length - 2, busy.Count,
                "한가 2개 + 바쁨 나머지의 합이 전수와 맞지 않습니다 — 분류가 겹치거나 빠졌습니다.");
        }

        // ====================================================================
        // ② 규칙 4는 «상태만» 본다 — 연출 락을 읽지 않는다
        // ====================================================================

        /// <summary>
        /// ★★ 설계 2판의 핵심 좁힘(§3-5). 락은 <b>앱의 배타 토큰</b>이고 캐릭터 상태가 아니다.
        /// 락 보유 13종 중 <b>크랙만</b> 금 수명까지 락을 들고 있어, 스윙이 끝난 뒤 약 2.6초 동안
        /// <b>캐릭터는 한가한데 락이 남는다</b>. 헤더가 락을 보면 그 구간에 거짓말을 한다.
        ///
        /// <para><b>부재 단언에 존재 짝을 붙인다</b>: 같은 니들이 네 Director 판정에는 <b>실재</b>함을
        /// 먼저 보인다. 니들이 오타거나 프로덕션이 이름을 바꾸면 아래 «없다» 두 건은 <b>아무것도 재지
        /// 않은 초록</b>이 되는데, 그 형태가 이 저장소가 반복해서 밟은 함정이다.</para>
        ///
        /// <para>니들은 <c>nameof</c>로 얻으므로 타입 이름이 바뀌면 조용히 초록이 되는 대신
        /// <b>컴파일되지 않는다</b>.</para>
        /// </summary>
        [Test]
        public void 헤더_판정은_연출_락을_읽지_않는다_그리고_그_니들은_살아_있다()
        {
            string needle = nameof(SpectacleEventLock);

            // ---- 존재 짝 대조: 니들이 살아 있는가 ----
            var alive = new List<string>();
            foreach (string file in FourDirectors)
            {
                string body = AvailabilityBody(file);
                if (body.IndexOf(needle, StringComparison.Ordinal) >= 0) alive.Add(file);
            }
            Assert.AreEqual(FourDirectors.Length, alive.Count,
                $"네 Director 판정 중 '{needle}'이 실재하는 곳이 {alive.Count}개뿐입니다(" +
                string.Join(", ", alive) + "). 니들이 죽었거나 락 검사가 사라졌습니다 — 이 상태로 아래 " +
                "«헤더는 락을 읽지 않는다»를 통과시키면 그것은 측정이 아니라 공허한 부재 단언입니다.");

            // ---- 부재 단언: 헤더 경로에는 없다 ----
            string src = ReadFile(InteractionDirectory, PopoverFile);
            foreach ((string method, string prefix) in new[]
            {
                ("SetStatusCaption", "private void "),
                ("CharacterStateIsBusy", "private bool "),
                ("IsBusyHeaderState", "public static bool "),
            })
            {
                string body = MethodBody(src, method, prefix);
                Assert.AreEqual(-1, body.IndexOf(needle, StringComparison.Ordinal),
                    $"★ 헤더 경로({method})가 '{needle}'을 읽고 있습니다:\n  " + body.Trim() +
                    "\n규칙 4는 <b>상태만</b> 본다가 설계 2판의 판정입니다(§3-5). 락을 보면 크랙 금이 " +
                    "남은 약 2.6초 동안 한가히 걷는 캐릭터에게 «다른 일 하는 중»을 띄웁니다(원칙 1).");
            }
        }

        // ====================================================================
        // ③ 네 Director에 상태 식이 있다 (설계 §4 테스트 (iv))
        // ====================================================================

        /// <summary>
        /// 헤더의 바쁨 식과 칸 사유의 바쁨 식이 <b>같은 식</b>이어야 한다. 한쪽만 바뀌면 헤더는
        /// «바쁘다»인데 칸은 눌릴 수 있는(또는 반대의) 어긋남이 생긴다.
        ///
        /// <para>★ <b>존재 단언으로</b> 짠다 — 설계 문서가 못박은 대로다. 부재 단언으로 짜면 니들이
        /// 썩는 순간 조용히 초록이 된다.</para>
        /// </summary>
        [Test]
        public void 네_Director_판정에_상태_식이_있다()
        {
            string idle = nameof(StickmanStateId) + "." + nameof(StickmanStateId.Idle);
            string walk = nameof(StickmanStateId) + "." + nameof(StickmanStateId.Walk);

            foreach (string file in FourDirectors)
            {
                string body = AvailabilityBody(file);
                StringAssert.Contains(idle, body,
                    $"{file}의 GetAvailability에 상태 식({idle})이 없습니다 — 헤더 규칙 4가 주장하는 " +
                    "«바쁨»과 이 칸의 사유가 다른 근거에서 나옵니다(설계 §3-2).");
                StringAssert.Contains(walk, body,
                    $"{file}의 GetAvailability에 상태 식({walk})이 없습니다 — 같은 이유입니다.");
            }
        }

        // ====================================================================
        // ④ 사다리 — 순서와 상수 경유
        // ====================================================================

        /// <summary>
        /// 다섯 문구가 <b>서로 다르고</b>, 판정 식이 <b>상수를 경유</b>하며(리터럴 직박기 0),
        /// <b>위에서부터</b> 규칙 1 → 2 → 3 → 4 → 5 순서로 놓였는지.
        ///
        /// <para>★ 순서가 곧 규칙이다(설계 §0 «위에서부터 처음 맞는 줄»). 가출을 숨김보다 앞에 두는
        /// 것은 <b>일부러</b>다 — 가출 중에 숨긴 경우 사용자에게 필요한 것은 [돌아와!] 칩이고
        /// 헤더가 그 칩을 설명해야 한다.</para>
        ///
        /// <para>★ 리터럴 부재 단언의 <b>존재 짝</b>: 같은 문자열이 그 파일의 <b>상수 선언</b>에는
        /// 실재함을 먼저 확인한다(그래서 «본문에 없다»가 «문구 자체가 사라졌다»와 구별된다).</para>
        /// </summary>
        [Test]
        public void 헤더_사다리는_상수를_경유하고_규칙_순서대로_놓였다()
        {
            string src = ReadFile(InteractionDirectory, PopoverFile);
            string body = MethodBody(src, "SetStatusCaption", "private void ");

            // ★★ <b>판정 식만</b> 떠낸다 — 본문 전체에서 식별자를 찾으면 <b>주석이 먼저 걸려</b> 순서
            //   판정이 뒤집힌다. 2026-09-03 근거 주석이 «HiddenCharacterCommandGate.HiddenReason»을
            //   언급하고 있어, 실측에서 그 주석(@362)이 규칙 1의 상수(@1074)보다 앞에 잡혔다.
            //   이 라운드의 오프라인 재측정이 그 거짓 빨강을 먼저 잡았다.
            string ladder식 = LadderExpression(body);

            string gateSrc = ReadFile(CoreDirectory, GateFile);

            // Home* = 그 문구의 <b>집</b>(상수 선언이 있는 파일). 규칙 2만 집이 다르다.
            (string Identifier, string Literal, string HomeFile, string HomeSource)[] ladder =
            {
                (nameof(ActionCommandPopover.RunawayCaption),
                    ActionCommandPopover.RunawayCaption, PopoverFile, src),
                (nameof(HiddenCharacterCommandGate.HiddenReason),
                    HiddenCharacterCommandGate.HiddenReason, GateFile, gateSrc),
                (nameof(ActionCommandPopover.ReadyCaption),
                    ActionCommandPopover.ReadyCaption, PopoverFile, src),
                (nameof(ActionCommandPopover.BusyCaption),
                    ActionCommandPopover.BusyCaption, PopoverFile, src),
                (nameof(ActionCommandPopover.NothingAvailableCaption),
                    ActionCommandPopover.NothingAvailableCaption, PopoverFile, src),
            };

            // ---- 서로 다르고 비어 있지 않다 ----
            for (int i = 0; i < ladder.Length; i++)
            {
                Assert.IsNotEmpty(ladder[i].Literal, $"{ladder[i].Identifier}의 문구가 비어 있습니다 — " +
                    "헤더 줄은 상시 존재해야 레이아웃이 점프하지 않습니다(36-6).");
                for (int j = i + 1; j < ladder.Length; j++)
                {
                    Assert.AreNotEqual(ladder[i].Literal, ladder[j].Literal,
                        $"{ladder[i].Identifier}와 {ladder[j].Identifier}의 문구가 같습니다 — 두 규칙이 " +
                        "화면에서 구별되지 않으면 갈라 놓은 의미가 없습니다.");
                }
            }

            // ---- 상수 경유: 식별자는 있고, 리터럴은 없다(존재 짝 = 상수 선언에는 실재) ----
            var order = new List<int>(ladder.Length);
            foreach ((string identifier, string literal, string homeFile, string homeSource) in ladder)
            {
                int at = ladder식.IndexOf(identifier, StringComparison.Ordinal);
                Assert.Greater(at, -1,
                    $"판정 식에 {identifier}가 없습니다 — 사다리가 이 규칙을 잃었거나 상수를 경유하지 " +
                    "않습니다:\n  " + ladder식.Trim());
                order.Add(at);

                // 존재 짝: 문구가 <b>자기 집</b>에는 실재한다(«본문에 없다»와 «문구가 사라졌다»를 가른다).
                Assert.Greater(homeSource.IndexOf(literal, StringComparison.Ordinal), -1,
                    $"{identifier}의 문구가 집({homeFile})에 실재하지 않습니다 — 니들이 죽었습니다. " +
                    "이 상태로 아래 리터럴 부재 단언은 아무것도 재지 않습니다.");
                Assert.AreEqual(-1, ladder식.IndexOf(literal, StringComparison.Ordinal),
                    $"판정 식에 {identifier}의 문구가 <b>리터럴로</b> 박혀 있습니다 — 테스트가 참조할 " +
                    "단일 출처가 사라집니다(CLAUDE.md: 상수를 문자열로 베끼지 않는다).");

                // 집이 다른 문구는 명령창 파일에 <b>한 글자도</b> 없어야 한다 — 같은 사실을 두 곳에
                // 다른 말로 적으면 design-narrative가 글자를 고칠 때 한쪽만 남는다.
                if (homeFile == PopoverFile) continue;
                Assert.AreEqual(-1, src.IndexOf(literal, StringComparison.Ordinal),
                    $"{identifier}의 문구가 {PopoverFile}에 <b>복제</b>됐습니다 — 집은 {homeFile}이고 " +
                    "명령창은 그 상수를 참조만 해야 합니다.");
            }

            // ---- 순서: 규칙 1 → 2 → 3 → 4 → 5 ----
            for (int i = 1; i < order.Count; i++)
            {
                Assert.Less(order[i - 1], order[i],
                    $"사다리 순서가 어긋났습니다 — {ladder[i - 1].Identifier}가 " +
                    $"{ladder[i].Identifier}보다 뒤에 있습니다. 설계 §0은 «위에서부터 처음 맞는 줄»이고 " +
                    "순서 자체가 규칙입니다(가출 > 숨김 > 가능 > 상태 바쁨 > 그 밖).");
            }
        }

        // ====================================================================
        // ⑤ 푸터 앞 절이 기대는 사실 — 자동 발동 확률이 0이다
        // ====================================================================

        /// <summary>
        /// ★★ 푸터 앞 절(<see cref="ActionCommandPopover.FooterHintText"/>의 「시켜야만 해요」)은
        /// <b>구조가 아니라 애셋에 기대어</b> 참이다.
        ///
        /// <para>뒷절은 구조가 보장한다 — 명령창이 <c>OnClosing</c>을 재정의하지 않으므로 닫아도 연출이
        /// 계속된다. 그러나 앞 절은 <b>출하 애셋의 자동 발동 확률 네 개가 0</b>이라서 참이고, 네 감독에는
        /// 자동 발동 경로가 <b>살아 있다</b>. 같은 애셋의 다른 확률들은 0이 아니므로
        /// <b>숫자 하나를 올리는 순간 이 문장이 조용히 거짓이 된다</b> — 그때 빨개질 자리가 여기다.
        /// 값을 <b>애셋에서 읽어</b> 비교한다(0이 아닌 값을 테스트에 베껴 두면 기준과 대상이 같이 낡는다).</para>
        ///
        /// <para>★ <b>양성 대조가 이 테스트의 절반이다</b>: 애셋을 못 읽거나(경로 오타·직렬화 파손)
        /// 리더가 전부 0을 돌려주면 아래 네 단언은 <b>아무것도 재지 않고 초록</b>이 된다. 그래서 0이
        /// 아니어야 할 확률 하나가 실제로 0이 아님을 <b>먼저</b> 보인다.</para>
        /// </summary>
        [Test]
        public void 푸터_앞절이_기대는_자동_발동_확률이_출하_애셋에서_0이다()
        {
            var shipped = AssetDatabase.LoadAssetAtPath<StickConfig>(DeployedConfigPath);
            Assert.IsNotNull(shipped,
                $"출하 튜닝 애셋을 찾지 못했습니다: {DeployedConfigPath}. 경로가 바뀌었다면 이 감사도 " +
                "함께 고치십시오(못 찾은 채로 두면 아래 단언이 전부 무의미해집니다).");

            // ---- 양성 대조: 리더가 살아 있다 ----
            Assert.Greater(shipped.ropeClimbChance, 0f,
                $"양성 대조 실패 — 0이 아니어야 할 확률({nameof(StickConfig.ropeClimbChance)})까지 0으로 " +
                "읽혔습니다. 애셋을 못 읽었거나 직렬화가 깨진 것이고, 이 상태에서 아래 «0이다» 네 건은 " +
                "아무것도 재지 않은 초록입니다.");

            foreach ((string name, float chance) in new[]
            {
                (nameof(StickConfig.archeryChance), shipped.archeryChance),
                (nameof(StickConfig.graffitiChance), shipped.graffitiChance),
                (nameof(StickConfig.windowTheftChance), shipped.windowTheftChance),
                (nameof(StickConfig.windowCrashChance), shipped.windowCrashChance),
            })
            {
                Assert.That(chance, Is.Zero,
                    $"출하 애셋의 {name}이(가) {chance}입니다 — 이 명령이 <b>사용자가 시키지 않아도</b> " +
                    "스스로 발동한다는 뜻입니다. ★ 행동 명령창 푸터 앞 절" +
                    $"({nameof(ActionCommandPopover)}.{nameof(ActionCommandPopover.FooterHintText)}의 " +
                    "「시켜야만 해요」)이 <b>바로 이 값에 기대고 있습니다</b> — 확률을 올리려면 그 문장을 " +
                    "먼저 고치십시오(원칙 1: 화면 문장은 확정된 사실에서만 파생한다).");
            }
        }

        // ====================================================================
        // 도구
        // ====================================================================

        /// <summary>출하 튜닝 애셋 — <b>코드 기본값이 아니라 이쪽이 사용자에게 나간다</b>
        /// (이 저장소의 거짓 통과 #9가 그 혼동이었다).</summary>
        private const string DeployedConfigPath = "Assets/_Project/Data/DefaultStickConfig.asset";

        /// <summary>
        /// <see cref="ActionCommandPopover"/>의 헤더 <b>판정 식 한 줄</b>만 떠낸다(대입문 → 세미콜론).
        ///
        /// <para>★ 왜 본문 전체를 쓰지 않는가: 본문에는 규칙 2의 근거 주석이 상수 이름을 <b>언급</b>한다.
        /// 본문에서 식별자를 찾으면 그 주석이 먼저 걸려 «사다리 순서»가 실제와 무관하게 뒤집힌다
        /// (실측: 주석 @362 vs 규칙 1 상수 @1074). 니들이 죽으면 조용히 통과하는 대신 여기서 <b>빨개진다</b>.</para>
        /// </summary>
        private static string LadderExpression(string body)
        {
            const string head = "string text = ";
            int at = body.IndexOf(head, StringComparison.Ordinal);
            Assert.Greater(at, -1,
                $"SetStatusCaption 본문에서 판정 식(«{head}…»)을 찾지 못했습니다 — 사다리가 다른 모양으로 " +
                "바뀌었다면 이 감사도 함께 고치십시오(이 가드가 없으면 빈 문자열 위에서 전부 통과합니다).");
            int semi = body.IndexOf(';', at);
            Assert.Greater(semi, at, "판정 식의 종결 세미콜론을 찾지 못했습니다 — 스캔이 깨졌습니다.");
            return body.Substring(at, semi - at + 1);
        }

        private static string ReadFile(string directory, string name)
        {
            string path = Path.Combine(directory, name);
            Assert.IsTrue(File.Exists(path), $"소스를 찾지 못했습니다: {path}");
            return File.ReadAllText(path).Replace("\r\n", "\n");
        }

        private static string AvailabilityBody(string file) =>
            MethodBody(ReadFile(InteractionDirectory, file), "GetAvailability", "public CommandAvailability ");

        /// <summary>시그니처 문자열로 메서드를 찾아 본문을 뜬다(중괄호 균형 / 식 본문 멤버 겸용).
        /// <para>★ «못 찾았다»를 <c>Assert</c>로 잡는다 — 빈 문자열 위에서는 모든 부재 단언이
        /// 조용히 통과한다(<see cref="HiddenCharacterCommandGateAuditTests"/>가 실제로 밟았던 자리).</para></summary>
        private static string MethodBody(string src, string method, string prefix)
        {
            int at = src.IndexOf(prefix + method + "(", StringComparison.Ordinal);
            Assert.Greater(at, 0,
                $"{prefix}{method}(...)을 찾지 못했습니다 — 이름이나 접근 수준이 바뀌었다면 이 감사도 " +
                "함께 고치십시오(이 가드가 없으면 빈 문자열 위에서 전부 통과합니다).");

            int open = src.IndexOf('{', at);
            int arrow = src.IndexOf("=>", at, StringComparison.Ordinal);
            if (arrow > 0 && (open < 0 || arrow < open))
            {
                int semi = src.IndexOf(';', arrow);
                Assert.Greater(semi, arrow, $"{method}의 식 본문 종결 세미콜론을 찾지 못했습니다.");
                return src.Substring(arrow, semi - arrow + 1);
            }

            Assert.Greater(open, 0, $"{method} 본문의 여는 중괄호를 찾지 못했습니다.");
            int depth = 0;
            for (int i = open; i < src.Length; i++)
            {
                if (src[i] == '{') depth++;
                else if (src[i] == '}')
                {
                    depth--;
                    if (depth == 0) return src.Substring(open, i - open + 1);
                }
            }
            Assert.Fail($"{method} 본문의 닫는 중괄호를 찾지 못했습니다 — 스캔이 깨졌습니다.");
            return string.Empty;
        }
    }
}
