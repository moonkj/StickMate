using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★★ <b>「캐릭터가 안 보이면 캐릭터가 하는 일도 못 시킨다」의 구조적 잠금</b> — 2026-09-03.
    ///
    /// ============================================================================
    /// 무엇을 지키는가
    /// ============================================================================
    /// 사용자 명시 숨김이 «캐릭터만» 가리도록 좁아지면서 행동 명령창이 <b>남았고</b>, 그래서
    /// 보이지 않는 캐릭터에게 연출을 시키는 경로가 열렸다(<c>coder</c> 배치모드 실측: 그라피티와
    /// 가출이 <b>실제로 발동</b>했다). 처방은 <see cref="HiddenCharacterCommandGate"/> 한 줄을
    /// 각 Director의 <c>GetAvailability()</c>에 넣는 것이다 — 그 함수 하나가 <b>회색 처리와 실제
    /// 실행을 동시에</b> 결정하므로 창과 전역 단축키가 같은 답을 낸다(36-7 절대 규칙).
    ///
    /// ============================================================================
    /// 왜 <b>명부가 아니라 스캔</b>인가
    /// ============================================================================
    /// "대상 파일을 손으로 적는" 검사는 이 저장소에서 이미 한 번 샜다(포스트잇이 명부에 없어서
    /// 전체화면 위에 그대로 떴다). 그래서 여기서는 <c>Interaction/</c>의 <b>모든</b>
    /// <c>CommandAvailability</c> 반환 메서드를 <b>본문 단위로</b> 훑고, 게이트가 없으면
    /// <b>사유와 함께 면제 명부에 있어야만</b> 통과시킨다. 새 스펙터클 Director가 생기면
    /// 아무도 이 파일을 고치지 않아도 자동으로 걸린다.
    /// </summary>
    public sealed class HiddenCharacterCommandGateAuditTests
    {
        private static string InteractionDirectory =>
            Path.Combine(Application.dataPath, "_Project", "Scripts", "Interaction");

        private static string CoreDirectory =>
            Path.Combine(Application.dataPath, "_Project", "Scripts", "Core");

        /// <summary>
        /// <b>게이트를 붙이면 안 되는</b> 판정 — 사유와 함께. 막는 순간 사용자가 갇히는 것들이다.
        /// <para>키는 <c>파일명::메서드명</c>. 이 명부가 비면 <see cref="면제_명부는_실제로_존재하고_실제로_비어_있다"/>가
        /// 그 사실을 <b>기대값으로</b> 잡아 준다 — 빈 컬렉션 위의 검사는 아무것도 재지 않으면서 초록이 된다.</para>
        /// </summary>
        private static readonly Dictionary<string, string> Exempt = new Dictionary<string, string>
        {
            ["RunawayDirector.cs::GetRecallAvailability"] =
                "가출 [돌아와!] 소환은 UX_FLOW 20절이 «찾기 미니게임을 강제하지 않는 상시 탈출구»로 못박은 " +
                "자리다. 가출 중에 캐릭터를 숨겼다면 되돌릴 유일한 길이 이 소환이므로 막으면 갇힌다. " +
                "막는 쪽은 같은 파일의 GetForcedRunawayAvailability(발동)이고, 그쪽에는 게이트가 있다.",
        };

        private const string GateCall = "HiddenCharacterCommandGate.BlocksNow";

        // ====================================================================
        // ① 게이트가 읽는 값 — IsSuspended여야 한다
        // ====================================================================

        /// <summary>
        /// ★ 조건은 <b>「캐릭터가 안 보인다」</b>이지 「표면을 걷는다」가 아니다.
        /// <c>HidesScreenSurfaces</c>로 바꾸면 <b>사용자 명시 숨김에서 안 막힌다</b> — 그게 정확히
        /// 이 게이트가 고치려는 결함이라, 그 한 글자가 기능 전체를 소리 없이 없앤다.
        ///
        /// <para><b>부재 단언에 존재 대조를 붙인다</b>: 두 니들 모두 <c>nameof</c>로 프로덕션
        /// 프로퍼티를 참조하므로, 이름이 바뀌면 조용히 초록이 되는 대신 <b>컴파일되지 않는다</b>.</para>
        /// </summary>
        [Test]
        public void 게이트는_표면_채널이_아니라_캐릭터_채널을_읽는다()
        {
            string body = MethodBody(ReadFile(CoreDirectory, "HiddenCharacterCommandGate.cs"),
                "BlocksNow", "public static bool ");

            string character = "." + nameof(StickMate.Core.StickmanAgent.IsSuspended);
            StringAssert.Contains(character, body,
                $"게이트가 캐릭터 채널({character})을 읽지 않습니다:\n  " + body.Trim());

            string surfaces = "." + nameof(StickMate.Core.StickmanAgent.HidesScreenSurfaces);
            Assert.AreEqual(-1, body.IndexOf(surfaces, StringComparison.Ordinal),
                $"★ 게이트가 표면 채널({surfaces})을 읽고 있습니다:\n  " + body.Trim() +
                "\n그 값은 사용자 명시 숨김 단독에서 <b>false</b>라, 이 게이트가 통째로 무력화됩니다. " +
                "보이지 않는 캐릭터가 다시 그라피티를 그립니다(2026-09-03 실측 결함의 회귀).");
        }

        // ====================================================================
        // ② 모든 CommandAvailability 판정에 게이트가 있다 (면제는 사유와 함께)
        // ====================================================================

        [Test]
        public void 모든_명령_판정이_숨김_게이트를_통과한다()
        {
            List<(string File, string Method, string Body)> methods = AllAvailabilityMethods();

            // ★ 스캔이 깨져 "0개를 찾고 전부 통과"하는 것을 먼저 막는다(이 저장소의 거짓 통과 #5).
            //   2026-09-03 현재 실제 개수는 7개다(활쏘기/그라피티/창도둑/창크래시/할일알림 +
            //   가출 2종). 하한을 실제 개수와 같게 두어 하나라도 줄면 바로 걸리게 한다.
            Assert.GreaterOrEqual(methods.Count, 7,
                $"CommandAvailability 판정을 {methods.Count}개밖에 못 찾았습니다 — 스캔이 깨졌습니다. " +
                "찾은 것: " + Describe(methods));

            var missing = new List<string>();
            foreach ((string file, string method, string body) in methods)
            {
                string key = file + "::" + method;
                bool gated = body.IndexOf(GateCall, StringComparison.Ordinal) >= 0;
                if (Exempt.ContainsKey(key))
                {
                    Assert.IsFalse(gated,
                        $"{key}는 면제 명부에 있는데 게이트가 붙어 있습니다 — 둘 중 하나가 틀렸습니다. " +
                        "면제 사유: " + Exempt[key]);
                    continue;
                }
                if (!gated) missing.Add(key);
            }

            Assert.IsEmpty(missing,
                "숨김 게이트가 없는 명령 판정이 있습니다: " + string.Join(", ", missing) +
                ". 이 명령은 <b>보이지 않는 캐릭터</b>에게 연출을 시킵니다(절대 불변 원칙 1 — 상태와 " +
                "화면이 갈리고, 그 상태에서 파생된 말풍선이 주인 없이 뜹니다). " +
                "막으면 사용자가 갇히는 종류라면 Exempt에 <b>사유와 함께</b> 적으십시오 — 조용히 빼지 마십시오.");
        }

        /// <summary>면제가 <b>실재하고</b> <b>지금 정확히 1건</b>인지. 명부가 늘어나면 여기서 먼저 걸려
        /// "왜 늘었는가"를 사람이 보게 만든다(빈/낡은 명부 위의 검사는 아무것도 재지 않는다).</summary>
        [Test]
        public void 면제_명부는_실제로_존재하고_실제로_비어_있다()
        {
            Assert.AreEqual(1, Exempt.Count,
                "면제 개수가 바뀌었습니다: " + string.Join(", ", Exempt.Keys) +
                ". 면제 자체는 허용되지만(사유를 적었다면) 이 단언을 그때 함께 고치십시오.");

            List<(string File, string Method, string Body)> methods = AllAvailabilityMethods();
            foreach (KeyValuePair<string, string> entry in Exempt)
            {
                Assert.IsNotEmpty(entry.Value, $"{entry.Key}의 면제 사유가 비어 있습니다.");
                bool found = methods.Exists(m => m.File + "::" + m.Method == entry.Key);
                Assert.IsTrue(found,
                    $"면제 명부의 {entry.Key}를 소스에서 찾지 못했습니다 — 메서드가 사라졌거나 이름이 " +
                    "바뀌었습니다. 명부도 함께 고치십시오(남겨 두면 다음 사람에게 거짓말이 됩니다).");
            }
        }

        // ====================================================================
        // ③ 탈출구에는 게이트가 없다 (막으면 갇힌다)
        // ====================================================================

        /// <summary>
        /// ★ <b>부재 단언 + 같은 파일 안의 존재 대조.</b> 아래 세 경로에 게이트가 붙으면 사용자는
        /// 숨긴 캐릭터를 되돌릴 방법을 잃는다 — 2026-09-03 신고
        /// <i>"다시 나오게 할 방법이 없어"</i> 그 자체다.
        ///
        /// <para>같은 파일(<c>AppControlDirector.cs</c>)의 <c>GetSayNowAvailability</c>에는 게이트가
        /// <b>있어야</b> 한다. 이 대조가 없으면 «게이트가 이 파일에 아예 배선되지 않았다»도
        /// 조용히 통과한다.</para>
        /// </summary>
        [Test]
        public void 탈출구_명령에는_게이트가_붙지_않는다()
        {
            string src = ReadFile(InteractionDirectory, "AppControlDirector.cs");

            string sayNow = MethodBody(src, "GetSayNowAvailability", "public CommandAvailability ");
            StringAssert.Contains(GateCall, sayNow,
                "말 걸기 판정에 게이트가 없습니다 — 아래 '탈출구에는 없다'가 " +
                "'이 파일에 게이트가 아예 없다'로도 통과하게 됩니다(공허한 부재 단언).");

            foreach (string escape in new[] { "ToggleUserHide", "ToggleSettings", "ToggleCharacterInfo" })
            {
                string body = MethodBody(src, escape, "private void ");
                Assert.AreEqual(-1, body.IndexOf(GateCall, StringComparison.Ordinal),
                    $"★ 탈출구({escape})에 숨김 게이트가 붙었습니다. 숨긴 사용자가 캐릭터를 되돌릴 " +
                    "경로를 잃습니다 — 이 라운드가 고친 신고의 정확한 재현입니다.");
            }
        }

        // ====================================================================
        // 도구
        // ====================================================================

        private static string ReadFile(string dir, string name)
        {
            string path = Path.Combine(dir, name);
            Assert.IsTrue(File.Exists(path), $"소스를 찾지 못했습니다: {path}");
            return File.ReadAllText(path).Replace("\r\n", "\n");
        }

        /// <summary><c>Interaction/</c>의 모든 <c>CommandAvailability</c> 반환 메서드를 본문과 함께.
        /// 파일 단위가 아니라 <b>메서드 단위</b>여야 한다 — 한 파일이 «막는 판정»과 «막으면 안 되는
        /// 판정»을 동시에 갖고 있기 때문이다(RunawayDirector가 정확히 그렇다).</summary>
        private static List<(string File, string Method, string Body)> AllAvailabilityMethods()
        {
            var result = new List<(string, string, string)>();
            var signature = new Regex(@"public CommandAvailability (\w+)\(\)");
            foreach (string path in Directory.GetFiles(InteractionDirectory, "*.cs", SearchOption.AllDirectories))
            {
                string src = File.ReadAllText(path).Replace("\r\n", "\n");
                foreach (Match m in signature.Matches(src))
                {
                    result.Add((Path.GetFileName(path), m.Groups[1].Value, BodyFrom(src, m.Index)));
                }
            }
            result.Sort((a, b) => string.CompareOrdinal(a.Item1 + a.Item2, b.Item1 + b.Item2));
            return result;
        }

        /// <summary>시그니처 문자열로 메서드를 찾아 본문을 뜬다(중괄호 균형).</summary>
        private static string MethodBody(string src, string method, string prefix)
        {
            int at = src.IndexOf(prefix + method + "(", StringComparison.Ordinal);
            Assert.Greater(at, 0, $"{prefix}{method}(...)을 찾지 못했습니다 — 이름이 바뀌었다면 이 감사도 " +
                "함께 고치십시오(이 단언이 없으면 빈 문자열 위에서 모든 검사가 통과합니다).");
            return BodyFrom(src, at);
        }

        /// <summary>메서드 본문을 뜬다. <b>식 본문 멤버(<c>=&gt; ... ;</c>)도 함께 다룬다</b> —
        /// 처음 버전은 <c>{</c>만 찾다가 <c>BlocksNow</c>(식 본문)에서 멈췄다. 다행히 «못 찾았다»를
        /// <c>Assert</c>로 잡아 두어 <b>빈 문자열 위에서 조용히 통과</b>하지 않았다. 그 가드를 지우지 마라.</summary>
        private static string BodyFrom(string src, int signatureIndex)
        {
            int open = src.IndexOf('{', signatureIndex);
            int arrow = src.IndexOf("=>", signatureIndex, StringComparison.Ordinal);
            if (arrow > 0 && (open < 0 || arrow < open))
            {
                int semi = src.IndexOf(';', arrow);
                Assert.Greater(semi, arrow, "식 본문 멤버의 종결 세미콜론을 찾지 못했습니다.");
                return src.Substring(arrow, semi - arrow + 1);
            }

            Assert.Greater(open, 0, "메서드 본문의 여는 중괄호를 찾지 못했습니다.");
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
            Assert.Fail("메서드 본문의 닫는 중괄호를 찾지 못했습니다 — 스캔이 깨졌습니다.");
            return string.Empty;
        }

        private static string Describe(List<(string File, string Method, string Body)> methods)
        {
            var names = new List<string>(methods.Count);
            foreach ((string f, string m, string _) in methods) names.Add(f + "::" + m);
            return string.Join(", ", names);
        }
    }
}
