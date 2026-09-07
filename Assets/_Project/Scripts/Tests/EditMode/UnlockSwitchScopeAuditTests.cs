using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ============================================================================
    /// E-9 #2 — <b>QA 해금 스위치는 유료 경계를 넘지 않는다</b> (security, 2026-09-02)
    /// ============================================================================
    /// 규범: <c>docs/security/ENTITLEMENT_CONTRACT.md</c> §E-6(문법적 격리).
    /// 짝: <c>EquipmentDebugUnlockReleaseGateTests</c>(스위치가 <b>릴리스에서 꺼지는가</b>) —
    /// 그쪽은 <b>값</b>을 재고, 이쪽은 <b>도달 범위</b>를 잰다. 둘 다 있어야 한다:
    /// 스위치가 꺼져도 <b>C층 판정에 배선되어 있으면</b> 언젠가 켜지는 날 DLC가 함께 열리고,
    /// 배선이 없어도 릴리스에서 켜져 있으면 성장 요소가 통째로 무의미해진다.
    ///
    /// ============================================================================
    /// 왜 이것이 "재화 치트"가 아니라 <b>유료 경계</b> 문제인가
    /// ============================================================================
    /// 이 앱은 네트워크 0 · 싱글플레이다. 세이브를 고쳐 동전을 올리면 <b>잃는 사람은 자기 자신뿐</b>이라
    /// 막을 가치가 낮다(§E-8). 그러나 <c>UnlockAll</c>이 <b>C층 판정</b>에까지 걸려 있으면
    /// 환경변수 한 줄(<c>STICKMATE_UNLOCK_ALL=1</c>)이 <b>결제 경계</b>를 넘고,
    /// 그때 잃는 사람은 유저가 아니라 <b>개발자(매출)</b>다.
    /// 정의서 위협표에서 ★가 붙은 유일한 칸이 정확히 여기다.
    ///
    /// ============================================================================
    /// 오늘의 실측(2026-09-02) — 이 숫자가 기대값이다
    /// ============================================================================
    /// 선언 파일 1개, 그 <b>바깥</b> 참조는 <b>정확히 2파일</b>
    /// (<c>ItemCatalog.cs</c>의 <c>ItemCatalogEntry.IsOwned</c> / <c>EquipmentModel.cs</c>의
    /// <c>IsItemOwned</c>). 둘 다 A·B층 보유 판정이고, 둘 다 <b><c>UnlockAll</c> 한 멤버만</b> 읽는다.
    ///
    /// <para>★ 파일 이름을 <b>기대값</b>으로만 쓰고 <b>탐지</b>에는 쓰지 않는다 — 선언 파일은
    /// 이름이 아니라 <c>class</c> 선언으로 찾는다. 이름으로 찾으면 리팩터링 한 번에
    /// 스캔이 <b>조용히 0건</b>이 되고, 그 0건은 "위반 없음"과 똑같이 생겼다.</para>
    ///
    /// <para>리플렉션 0줄(활성 빌드 타깃 사각지대 회피) · 정규식 0줄(.NET <c>\b</c>가 한글을 낱말로
    /// 세는 함정 회피 — 이 저장소가 그 함정으로 참조 46%를 놓친 적이 있다).</para>
    ///
    /// ============================================================================
    /// ★★ 2026-09-07 개정 — §2(테스트 강제값)의 <b>계기를 바꿨다</b>
    /// ============================================================================
    /// §1(해금 스위치 참조 범위)은 <b>한 줄도 바뀌지 않았다</b>. 바뀐 것은 §2다.
    ///
    /// <para>구판 §2는 «프로덕션 전체의 public 테스트 강제값 <b>개수</b> &lt;= 1»이었다.
    /// 그러나 §E-6-c가 규정하는 대상은 <b>C층(유료 권한) 판정의 강제값</b>이지 저장소 전체의
    /// 개수가 아니다. 그 어긋남이 두 방향으로 동시에 사고를 냈다.</para>
    ///
    /// <list type="number">
    ///   <item><b>과탐</b> — 밧줄등반 QA 스위치 2종(연출 확률 · 합성 시험벽)이 결제 경계와
    ///     아무 관계가 없는데도 이 감사를 빨갛게 만들었다. 도달 종점을 실제로 추적하면
    ///     배회 AI의 추첨 확률과 발판 목록뿐이고, XP·동전·아이템·엔타이틀먼트 모델은
    ///     그 경로에 <b>한 곳도</b> 없다.</item>
    ///   <item><b>미탐</b> — 구판 탐지기는 줄에 <c>(</c>가 있어야만 셌다. 그래서 <b>자기 가족
    ///     안에서도</b> 괄호 없는 공개 속성 하나를 통째로 못 봤다(실측: 그 가족의 public 수는
    ///     3이 아니라 <b>4</b>였다). <b>상한만 올렸다면 그 미탐을 그대로 안고 갔을 것이다.</b></item>
    /// </list>
    ///
    /// <para>개정판은 상한을 올리지 않는다. §2-a가 «<b>C층에 도달하는</b> public 강제값 0건»을
    /// 잠그고(진짜 위험), §2-b가 좁은 가족의 public 목록을 <b>집합 등호</b>로 잠근다(습관).
    /// 개수 상한이 아니라 등호인 이유: 개수는 <b>맞바꿈</b>도 <b>사라짐</b>도 못 본다.</para>
    ///
    /// <para>실측(2026-09-07, 프로덕션 258파일): C층 파일 3개 · 강제값 선언 63~64건(public 45~46) ·
    /// <b>C층 도달 0건</b> · 좁은 가족 public 4건.</para>
    /// </summary>
    public sealed class UnlockSwitchScopeAuditTests
    {
        private const string LogPrefix = "[해금스위치범위]";

        /// <summary>감사 대상 스위치 타입 이름. <b>선언이 실재하는지</b>를 같은 테스트가 확인한다
        /// (존재 단언 — 썩으면 조용히 초록이 되는 대신 시끄럽게 빨개진다).</summary>
        private const string SwitchTypeName = "EquipmentDebugUnlock";

        /// <summary>선언 파일 밖에서 읽어도 되는 <b>유일한</b> 멤버.</summary>
        private const string AllowedMember = "UnlockAll";

        /// <summary>
        /// 선언 파일 밖 참조가 허용된 파일. <b>기대값</b>이지 탐지 수단이 아니다.
        /// 여기 없는 파일이 스위치를 읽으면 실패한다 — C층 배선이 이 목록에 몰래 끼지 못하게 하는 잠금.
        /// </summary>
        private static readonly string[] AllowedReferrerFiles =
        {
            "ItemCatalog.cs",
            "EquipmentModel.cs",
        };

        /// <summary>
        /// 스위치 참조 주변에서 <b>원래 규칙이 함께 살아 있는지</b> 확인하는 앵커.
        /// <para>§E-6-a의 뜻은 "<c>UnlockAll</c>은 합집합의 한 가지일 뿐, 판정 전체가 아니다"이다.
        /// 즉 스위치가 꺼지면 <b>원래 요구 레벨 규칙으로 돌아와야</b> 한다. 그 규칙이 같은 자리에
        /// 남아 있는지를 이 앵커로 잰다.</para>
        /// <para>★ 이건 <b>존재 단언</b>이다. 요구 레벨 규칙의 이름이 바뀌면 이 감사는 <b>빨개진다</b>
        /// (조용히 초록이 되지 않는다). 그때 앵커를 갱신하는 것이 올바른 조치다.</para>
        /// </summary>
        private const string OwnershipRuleAnchor = "Level";

        /// <summary>앵커를 찾을 때 참조 위치 앞뒤로 보는 글자 수. 넉넉히 잡는다 —
        /// 좁게 잡으면 정직한 리팩터링(예: <c>if (UnlockAll) return true;</c> + 그 아래 레벨 검사)에
        /// 빨개져서, 감사가 몇 번 만에 꺼진다.</summary>
        private const int AnchorWindow = 320;

        // ====================================================================
        // 스캔 — 순수 함수. 네거티브 컨트롤이 같은 함수에 가짜 소스를 흘린다.
        // ====================================================================

        private struct Reference
        {
            public string File;
            public int Line;
            public string Member;
            public bool RuleAnchorNearby;
        }

        /// <summary>
        /// 주석 제거본에서 <paramref name="typeName"/>의 <b>낱말 단위</b> 등장을 전부 찾아
        /// 뒤따르는 멤버 이름과 "원래 규칙 앵커가 근처에 있는가"를 함께 기록한다.
        /// </summary>
        private static List<Reference> FindReferences(string fileLabel, string strippedSource, string typeName)
        {
            var hits = new List<Reference>();
            int from = 0;
            while (true)
            {
                int at = strippedSource.IndexOf(typeName, from, StringComparison.Ordinal);
                if (at < 0) break;
                from = at + 1;

                int before = at - 1;
                int after = at + typeName.Length;
                bool leftOk = before < 0 || !IsIdentifierChar(strippedSource[before]);
                bool rightOk = after >= strippedSource.Length || !IsIdentifierChar(strippedSource[after]);
                if (!leftOk || !rightOk) continue;

                // 선언 자체(`class EquipmentDebugUnlock`)는 참조가 아니다.
                int lineStart = strippedSource.LastIndexOf('\n', Math.Max(0, at - 1)) + 1;
                string head = strippedSource.Substring(lineStart, Math.Max(0, at - lineStart));
                if (head.IndexOf("class ", StringComparison.Ordinal) >= 0) continue;

                string member = "(멤버 없음)";
                int p = after;
                while (p < strippedSource.Length && (strippedSource[p] == ' ' || strippedSource[p] == '\t')) p++;
                if (p < strippedSource.Length && strippedSource[p] == '.')
                {
                    p++;
                    int start = p;
                    while (p < strippedSource.Length && IsIdentifierChar(strippedSource[p])) p++;
                    if (p > start) member = strippedSource.Substring(start, p - start);
                }

                int wStart = Math.Max(0, at - AnchorWindow);
                int wEnd = Math.Min(strippedSource.Length, after + AnchorWindow);
                string window = strippedSource.Substring(wStart, wEnd - wStart);

                hits.Add(new Reference
                {
                    File = fileLabel,
                    Line = EntitlementAuditSource.LineNumberAt(strippedSource, at),
                    Member = member,
                    RuleAnchorNearby = window.IndexOf(OwnershipRuleAnchor, StringComparison.Ordinal) >= 0,
                });
            }
            return hits;
        }

        private static bool IsIdentifierChar(char c)
            => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';

        /// <summary><paramref name="typeName"/>을 선언하는 프로덕션 파일 전부(정상은 1개).</summary>
        private static List<string> DeclaringFiles(string typeName)
        {
            var found = new List<string>();
            foreach (string path in EntitlementAuditSource.ProductionSourceFiles())
            {
                string stripped = EntitlementAuditSource.StripComments(File.ReadAllText(path));
                if (EntitlementAuditSource.DeclaresType(stripped, typeName)) found.Add(path);
            }
            return found;
        }

        // ====================================================================
        // 1. 본론 — 도달 범위
        // ====================================================================

        [Test]
        public void 해금_스위치는_선언파일_밖에서_정확히_보유판정_두_곳만_읽는다()
        {
            string[] production = EntitlementAuditSource.ProductionSourceFiles();
            Assert.GreaterOrEqual(production.Length, EntitlementAuditSource.MinProductionFileCount,
                $"{LogPrefix} 프로덕션 .cs를 {production.Length}개밖에 읽지 못했습니다 — " +
                "아래 '참조 2곳뿐'은 측정이 아니라 착시가 됩니다.");

            List<string> declaring = DeclaringFiles(SwitchTypeName);
            Assert.AreEqual(1, declaring.Count,
                $"{LogPrefix} {SwitchTypeName} 선언 파일이 {declaring.Count}개입니다(기대 1개). " +
                "0개라면 타입 이름이 바뀐 것이고, 그러면 이 감사는 그 순간부터 아무것도 보고 있지 " +
                "않습니다 — 이름을 고치기 전에는 '참조 0건'을 믿으면 안 됩니다.\n" +
                "찾은 것: " + string.Join(", ", declaring));

            string declaringFile = Path.GetFileName(declaring[0]);
            var references = new List<Reference>();
            var report = new StringBuilder();
            report.Append(LogPrefix).Append(' ').Append(SwitchTypeName)
                  .Append(" 선언=").Append(declaringFile).Append('\n');

            foreach (string path in production)
            {
                if (string.Equals(path, declaring[0], StringComparison.Ordinal)) continue;
                string stripped = EntitlementAuditSource.StripComments(File.ReadAllText(path));
                references.AddRange(FindReferences(Path.GetFileName(path), stripped, SwitchTypeName));
            }

            foreach (Reference r in references)
            {
                report.Append("  ").Append(r.File).Append(':').Append(r.Line)
                      .Append('\t').Append(SwitchTypeName).Append('.').Append(r.Member)
                      .Append(r.RuleAnchorNearby ? "\t(원래 규칙 근처)" : "\t★원래 규칙 없음")
                      .Append('\n');
            }
            Debug.Log(report.ToString());

            var referrerFiles = new List<string>();
            foreach (Reference r in references)
                if (!referrerFiles.Contains(r.File)) referrerFiles.Add(r.File);
            referrerFiles.Sort(StringComparer.Ordinal);

            var expected = new List<string>(AllowedReferrerFiles);
            expected.Sort(StringComparer.Ordinal);

            var unexpected = new List<string>();
            foreach (string f in referrerFiles) if (!expected.Contains(f)) unexpected.Add(f);
            var missing = new List<string>();
            foreach (string f in expected) if (!referrerFiles.Contains(f)) missing.Add(f);

            Assert.IsEmpty(unexpected,
                $"{LogPrefix} 허용되지 않은 파일이 QA 해금 스위치를 읽습니다: {string.Join(", ", unexpected)}\n" +
                "★ 그 파일이 <b>유료 권한(C층) 판정</b>이라면 이건 결제 경계가 환경변수 한 줄로 " +
                "열린다는 뜻입니다(§E-6-a/§E-6-b: C층 판정 함수의 본문과 호출 그래프 어디에도 " +
                $"{SwitchTypeName}이 나타나지 않는다).\n" +
                "A·B층(무료 레벨 해금)을 하나 더 늘린 것이라면 위 AllowedReferrerFiles에 추가하되, " +
                "추가하기 전에 그 자리가 정말 무료 경계 안인지 확인하세요.");

            Assert.IsEmpty(missing,
                $"{LogPrefix} 있어야 할 참조가 사라졌습니다: {string.Join(", ", missing)}\n" +
                "스위치를 실제로 걷어낸 것이라면 AllowedReferrerFiles에서도 지우세요. " +
                "그냥 두면 이 감사는 <b>존재하지 않는 것</b>을 지키게 되고, 그 초록은 아무 뜻이 없습니다.");

            var wrongMember = new List<string>();
            foreach (Reference r in references)
            {
                if (string.Equals(r.Member, AllowedMember, StringComparison.Ordinal)) continue;
                wrongMember.Add($"  · {r.File}:{r.Line} → {SwitchTypeName}.{r.Member}");
            }
            Assert.IsEmpty(wrongMember,
                $"{LogPrefix} 선언 파일 밖에서 {AllowedMember} 외의 멤버를 읽습니다:\n" +
                string.Join("\n", wrongMember) + "\n" +
                "특히 테스트 강제값(SetTestOverride 계열)을 프로덕션이 부르면, 스위치가 " +
                "릴리스에서 꺼진다는 보장(EquipmentDebugUnlockReleaseGateTests)이 우회됩니다.");

            var ruleGone = new List<string>();
            foreach (Reference r in references)
            {
                if (r.RuleAnchorNearby) continue;
                ruleGone.Add($"  · {r.File}:{r.Line}");
            }
            Assert.IsEmpty(ruleGone,
                $"{LogPrefix} 스위치 참조 근처({AnchorWindow}자)에 원래 규칙 앵커 '{OwnershipRuleAnchor}'가 " +
                "없습니다:\n" + string.Join("\n", ruleGone) + "\n" +
                "§E-6-a: UnlockAll은 합집합의 <b>한 가지</b>여야 하고 판정 전체일 수 없습니다. " +
                "스위치가 꺼지면 원래 요구 레벨 규칙으로 돌아와야 합니다.\n" +
                "★ 규칙 이름을 바꾼 것이라면 OwnershipRuleAnchor를 갱신하세요 — " +
                "이 실패는 '위험'이 아니라 '앵커가 늙었다'일 수도 있습니다.");

            Assert.IsNotEmpty(references,
                $"{LogPrefix} 스위치 참조를 한 건도 못 찾았습니다. 이 저장소는 실제로 2곳에서 읽으므로 " +
                "0건은 '없다'가 아니라 <b>스캐너가 눈이 멀었다</b>입니다.");
        }

        // ====================================================================
        // 2. 테스트 강제값 — §E-6-c  (2026-09-07 security 재작성)
        // ====================================================================
        //
        // ★ 구판은 무엇이었고, 왜 「상한을 올린 것」이 아닌가
        // --------------------------------------------------------------------
        // 구판은 «프로덕션 전체의 public 테스트 강제값 개수 <= 1»이라는 <b>거친 계기</b>였다.
        // 밧줄등반 라운드가 QA 스위치 2종을 신설하면서 3건이 되어 빨개졌고, 그 라운드는 그것을
        // «이 라운드와 무관한 기존결함»으로 보고했다 — 리더 실측으로 그 보고는 <b>틀렸다</b>.
        // 원인 파일이 정확히 그 라운드가 신설한 것이었다.
        //
        // 이 재작성은 <b>숫자를 올리지 않는다.</b> §E-6-c가 실제로 규정하는 대상을 잰다:
        // «<b>C층(유료 권한) 판정에 도달하는</b> 강제값». 그 판정으로 오늘 실측하면 이렇다.
        //
        //   측정 1 — C층 도달: <b>0건</b>. 넓은 가족(아래) public 45~46건 중 C층과 참조 간선을
        //            가진 것이 하나도 없다. 밧줄등반 스위치 2종도 여기 포함된다 —
        //            도달 종점은 «배회 AI의 로프 추첨 확률»과 «발판 목록에 합성 벽 1개»이고,
        //            XP/동전/아이템/엔타이틀먼트 어느 모델도 그 경로에 없다.
        //
        //   측정 2 — 구판 계기의 <b>미탐</b>: 구판은 <b>자기 가족 안에서도</b>
        //            <c>AmbientCalendarClock.WallClockOverrideForTesting</c>을 못 봤다.
        //            «(»를 요구하는 탐지 조건이 <b>괄호 없는 속성 선언</b>을 구조적으로 못 본다.
        //            ⇒ 그 가족의 실제 public 수는 3이 아니라 <b>4</b>였다.
        //            <b>상한을 3으로 올렸다면 그 미탐을 그대로 안고 갔을 것이다</b> —
        //            이 저장소가 반복해 당한 «죽은 프로브가 산 프로브와 똑같이 생겼다»의 그 형태다.
        //
        // ★ 무엇을 «강제값»으로 세는가 — <b>쓰기만 센다. 읽기는 세지 않는다.</b>
        //   · 센다:    메서드 / <b>공개 setter가 있는</b> 속성 / 가변 필드
        //   · 안 센다: <c>const</c> · 식 본문 getter(<c>=></c>) · <c>private set</c> 속성
        //   근거: 강제값이 위험한 이유는 «바깥에서 값을 밀어 넣을 수 있다»는 것 하나다.
        //   <c>public const</c>는 테스트가 프로덕션 상수를 <b>숫자로 베끼지 않게</b> 하는 장치라
        //   (CLAUDE.md 규칙) 오히려 권장되는 형태다. 둘이 같은 «public인 이유» 문장을 달고 있어
        //   혼동되기 쉽지만 성질이 정반대다 — <c>FallbackPlatformWindowService</c>의
        //   <c>RopeClimbTestWall…</c> 상수 2개가 그 예이고, 이 감사가 그것을 <b>안 세는 것이 옳다</b>
        //   (아래 <see cref="대조_public_const는_강제값이_아니다"/>가 그 판정을 매 실행 못박는다).
        //
        // ★ 이 감사가 <b>못 보는 것</b>(정직하게 적는다)
        //   · 참조 간선을 <b>1홉</b>만 본다. «C층 → X → 강제값» 같은 2홉 이상 간접 흐름은 안 보인다.
        //     그 대신 이 파일의 §1(해금 스위치 참조 파일 집합 등호)이 <b>정확한</b> 잠금을 건다 —
        //     둘이 다른 축이라 서로를 보완한다.
        //   · 선언이 4줄 이상으로 흩어지면 이어 붙이기(최대 3줄)가 못 따라간다.

        /// <summary>강제값 선언의 <b>모양</b>. 값을 밀어 넣을 수 있는 것만 여기 들어온다.</summary>
        private enum SeamShape { Method, Property, Field }

        /// <summary>테스트 강제값 선언 하나.</summary>
        private struct ForcingSeam
        {
            public string File;
            public string Path;
            public int Line;
            public string Name;
            public SeamShape Shape;
            public bool IsPublic;
            /// <summary>구판 래칫이 겨눴던 <b>좁은 가족</b>(이름에 <c>Test</c>와 <c>Override</c>가 함께).</summary>
            public bool IsNarrowFamily;

            public string Entry => File + " :: " + Name;
        }

        private static readonly string[] AccessModifiers = { "public ", "internal ", "private ", "protected " };

        /// <summary>«테스트 전용»을 뜻하는 이름 표지. <c>ForTest</c>는 <c>ForTests</c>·<c>ForTesting</c>을
        /// 함께 덮는다 — 이 저장소의 실제 관례를 전수해서 뽑은 것이지 상상한 목록이 아니다.</summary>
        private const string TestSeamMarker = "ForTest";

        /// <summary>«값을 밀어 넣는다»를 뜻하는 메서드 접두. 읽기 전용 조회 헬퍼를 걸러 낸다.</summary>
        /// <summary>타입 선언 키워드 — 이 줄들은 강제값이 아니다.</summary>
        private static readonly string[] TypeDeclarationKeywords = { "class", "struct", "interface", "enum" };

        private static readonly string[] MutatingPrefixes =
        {
            "Set", "Clear", "Reset", "Redirect", "Force", "Install", "Use", "Mark", "Enable", "Disable", "Bypass",
        };

        /// <summary>C층(유료 권한) 정책 타입을 알아보는 이름 조각.
        /// <para>★ <c>EntitlementFailOpenAuditTests</c>가 쓰는 것과 <b>같은 축</b>이다. 일부러 각자
        /// 들고 있다 — 한 곳으로 합치면 그 목록이 늙는 날 <b>두 감사가 함께 눈이 먼다</b>
        /// (TEAM.md의 «생성기와 검사기가 같은 함정에 같이 빠진다»). 대신 아래 본론이
        /// <c>nameof</c>로 <b>실재하는 C층 타입의 선언 파일이 실제로 잡히는지</b>를 먼저 재고,
        /// 그 앵커는 이름이 바뀌면 <b>컴파일 자체가 실패</b>해 조용히 늙을 수 없다.</para></summary>
        private static readonly string[] CTierTypeNameFragments = { "Entitlement", "Ownership", "License" };

        // --------------------------------------------------------------------
        // 스캐너 — 전부 순수 함수. 대조 테스트가 같은 함수에 가짜 세계를 흘린다.
        // --------------------------------------------------------------------

        private static string IdentifierEndingAt(string line, int exclusiveEnd)
        {
            int end = Math.Min(exclusiveEnd, line.Length);
            while (end > 0 && (line[end - 1] == ' ' || line[end - 1] == '\t')) end--;
            int start = end;
            while (start > 0 && IsIdentifierChar(line[start - 1])) start--;
            return start == end ? null : line.Substring(start, end - start);
        }

        /// <summary><c>{ get; set; }</c>처럼 <b>바깥에서 쓸 수 있는</b> setter가 있는가.
        /// <c>private set</c>/<c>protected set</c>은 쓰기 창구가 아니므로 제외한다.</summary>
        private static bool HasExternallyWritableSetter(string unit)
        {
            if (unit.IndexOf("private set", StringComparison.Ordinal) >= 0) return false;
            if (unit.IndexOf("protected set", StringComparison.Ordinal) >= 0) return false;
            // ★ 낱말 단위로 본다. 부분 문자열로 보면 <c>offset</c>·<c>reset</c> 같은 이름이
            //   전부 "setter가 있다"로 읽혀 정직한 선언이 강제값으로 오인된다(실측 위험).
            return EntitlementAuditSource.ContainsIdentifier(unit, "set");
        }

        private static string FieldNameOrNull(string unit)
        {
            string body = unit.EndsWith(";", StringComparison.Ordinal)
                ? unit.Substring(0, unit.Length - 1) : unit;
            int eq = body.IndexOf('=');
            if (eq >= 0) body = body.Substring(0, eq);
            string[] parts = body.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 3 ? parts[parts.Length - 1] : null;   // 접근제어자 + 형 + 이름
        }

        private static bool StartsWithMutatingPrefix(string name)
        {
            foreach (string p in MutatingPrefixes)
                if (name.StartsWith(p, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>
        /// 한 파일(주석 제거본)에서 <b>쓰기 가능한 테스트 강제값 선언</b>을 전부 찾는다.
        /// <para>선언이 여러 줄로 흩어지는 경우(<c>public bool X</c> 다음 줄에 <c>=> …;</c> 또는
        /// <c>{ get; set; }</c>)를 위해 <b>최대 3줄</b>까지 이어 붙인 뒤 판정한다 — 줄 단위로만 보면
        /// 그 형태가 통째로 사각지대가 된다(실측: 이 저장소에 그 형태가 실제로 있다).</para>
        /// </summary>
        private static List<ForcingSeam> FindForcingSeams(string fileLabel, string path, string stripped)
        {
            var found = new List<ForcingSeam>();
            string[] lines = stripped.Replace("\r\n", "\n").Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string head = lines[i].Trim();

                bool isDeclaration = false;
                foreach (string m in AccessModifiers)
                    if (head.StartsWith(m, StringComparison.Ordinal)) { isDeclaration = true; break; }
                if (!isDeclaration) continue;
                if (head.IndexOf(" const ", StringComparison.Ordinal) >= 0) continue;   // 읽기 전용 상수는 강제값이 아니다

                // 타입 선언은 강제값이 아니다. 그리고 아래 «이어 붙이기»가 클래스 선언 줄에
                // 멤버 줄을 빨아들여 엉뚱한 이름을 만드는 것도 여기서 함께 막는다.
                bool declaresType = false;
                foreach (string kw in TypeDeclarationKeywords)
                    if (EntitlementAuditSource.ContainsIdentifier(head, kw)) { declaresType = true; break; }
                if (declaresType) continue;

                string unit = head;
                for (int k = 0; k < 3; k++)
                {
                    if (unit.IndexOf('(') >= 0 || unit.IndexOf(';') >= 0) break;
                    if (i + 1 + k >= lines.Length) break;
                    unit = unit + " " + lines[i + 1 + k].Trim();
                }

                string name = null;
                SeamShape shape;
                int paren = unit.IndexOf('(');
                int brace = unit.IndexOf('{');

                if (paren > 0 && (brace < 0 || paren < brace))
                {
                    name = IdentifierEndingAt(unit, paren);
                    shape = SeamShape.Method;
                }
                else if (brace > 0 && HasExternallyWritableSetter(unit))
                {
                    name = IdentifierEndingAt(unit, brace);
                    shape = SeamShape.Property;
                }
                else if (unit.EndsWith(";", StringComparison.Ordinal)
                         && unit.IndexOf("=>", StringComparison.Ordinal) < 0)
                {
                    name = FieldNameOrNull(unit);
                    shape = SeamShape.Field;
                }
                else
                {
                    continue;
                }
                if (string.IsNullOrEmpty(name)) continue;

                bool narrow = name.IndexOf("Override", StringComparison.OrdinalIgnoreCase) >= 0
                              && name.IndexOf("Test", StringComparison.OrdinalIgnoreCase) >= 0;
                bool marked = name.IndexOf(TestSeamMarker, StringComparison.OrdinalIgnoreCase) >= 0;
                bool mutating = shape != SeamShape.Method || StartsWithMutatingPrefix(name);
                if (!narrow && !(marked && mutating)) continue;

                found.Add(new ForcingSeam
                {
                    File = fileLabel,
                    Path = path,
                    Line = i + 1,
                    Name = name,
                    Shape = shape,
                    IsPublic = head.StartsWith("public ", StringComparison.Ordinal),
                    IsNarrowFamily = narrow,
                });
            }
            return found;
        }

        /// <summary>파일이 선언하는 <b>모든</b> 타입 이름. <c>DeclaredTypeNamesContaining</c>에 빈 조각을
        /// 주면 «전부»가 된다(<c>IndexOf("")==0</c>). 그 성질 자체를 아래 대조가 잰다 —
        /// 조용히 0건이 되면 D1 판정이 통째로 죽기 때문이다.</summary>
        private static List<string> AllDeclaredTypeNames(string stripped)
            => EntitlementAuditSource.DeclaredTypeNamesContaining(stripped, string.Empty);

        /// <summary>한 «세계»(경로 → 주석제거본)의 스캔 결과.</summary>
        private struct WorldScan
        {
            public List<string> CTierFiles;
            public List<string> CTierTypeNames;
            public List<ForcingSeam> Seams;
            /// <summary>public이면서 C층에 도달하는 것 — <b>이것이 0이어야 한다</b>.</summary>
            public List<string> Violations;
        }

        /// <summary>
        /// 본론 판정. <b>실제 프로덕션도 대조용 가짜 세계도 이 함수 하나를 통과한다</b> —
        /// 그래야 «대조가 통과했는데 본론은 다른 코드로 판정»하는 갈라짐이 생기지 않는다.
        /// </summary>
        private static WorldScan ScanWorld(IDictionary<string, string> strippedByPath)
        {
            var paths = new List<string>(strippedByPath.Keys);
            paths.Sort(StringComparer.Ordinal);

            // 파일마다 선언 타입을 <b>한 번만</b> 수집하고(비싼 스캔) 그 목록을 조각으로 거른다.
            var declaredByPath = new Dictionary<string, List<string>>(paths.Count, StringComparer.Ordinal);
            var cTierFiles = new List<string>();
            var cTierTypeNames = new List<string>();
            foreach (string p in paths)
            {
                List<string> declared = AllDeclaredTypeNames(strippedByPath[p]);
                declaredByPath[p] = declared;

                bool isCTier = false;
                foreach (string n in declared)
                {
                    foreach (string fragment in CTierTypeNameFragments)
                    {
                        if (n.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        isCTier = true;
                        if (!cTierTypeNames.Contains(n)) cTierTypeNames.Add(n);
                        break;
                    }
                }
                if (isCTier) cTierFiles.Add(p);
            }

            var seams = new List<ForcingSeam>();
            foreach (string p in paths)
                seams.AddRange(FindForcingSeams(Path.GetFileName(p), p, strippedByPath[p]));

            var violations = new List<string>();
            foreach (ForcingSeam seam in seams)
            {
                if (!seam.IsPublic) continue;
                string why = CTierReachReasonOrNull(seam, strippedByPath, declaredByPath, cTierFiles, cTierTypeNames);
                if (why != null) violations.Add($"  · {seam.File}:{seam.Line} → {seam.Name}  [{why}]");
            }

            return new WorldScan
            {
                CTierFiles = cTierFiles,
                CTierTypeNames = cTierTypeNames,
                Seams = seams,
                Violations = violations,
            };
        }

        /// <summary>
        /// 이 강제값이 C층에 <b>닿는가</b>. 닿으면 사람이 읽을 사유를, 아니면 <c>null</c>.
        /// <list type="bullet">
        ///   <item><b>D0</b> — 강제값이 C층 파일 <b>안에</b> 있다(§E-6-c 직격).</item>
        ///   <item><b>D1</b> — C층 파일이 이 강제값의 <b>선언 타입을 참조</b>한다(§E-6-b의 뜻:
        ///     C층은 그 타입을 «알지도 못해야» 한다).</item>
        ///   <item><b>D2</b> — 반대 방향. 이 파일이 <b>C층 정책 타입을 참조</b>한다.</item>
        /// </list>
        /// </summary>
        private static string CTierReachReasonOrNull(ForcingSeam seam,
            IDictionary<string, string> strippedByPath, IDictionary<string, List<string>> declaredByPath,
            List<string> cTierFiles, List<string> cTierTypeNames)
        {
            if (cTierFiles.Contains(seam.Path)) return "D0 C층 파일 안의 public 강제값";

            List<string> declaredHere = declaredByPath[seam.Path];
            foreach (string cf in cTierFiles)
            {
                string cSource = strippedByPath[cf];
                foreach (string t in declaredHere)
                {
                    if (EntitlementAuditSource.ContainsIdentifier(cSource, t))
                        return $"D1 C층 {Path.GetFileName(cf)}이 {t}를 참조";
                }
            }

            string here = strippedByPath[seam.Path];
            foreach (string ct in cTierTypeNames)
            {
                if (EntitlementAuditSource.ContainsIdentifier(here, ct))
                    return $"D2 이 파일이 C층 타입 {ct}를 참조";
            }
            return null;
        }

        private static Dictionary<string, string> StripAll(string[] paths)
        {
            var map = new Dictionary<string, string>(paths.Length, StringComparer.Ordinal);
            foreach (string p in paths) map[p] = EntitlementAuditSource.StripComments(File.ReadAllText(p));
            return map;
        }

        /// <summary>대조용 가짜 세계를 만든다(파일명 → 원본 소스, 주석 제거는 여기서 한다).</summary>
        private static Dictionary<string, string> FakeWorld(params (string File, string Source)[] files)
        {
            var map = new Dictionary<string, string>(files.Length, StringComparer.Ordinal);
            foreach ((string file, string source) in files) map[file] = EntitlementAuditSource.StripComments(source);
            return map;
        }

        // --------------------------------------------------------------------
        // 2-a. 본론 — C층에 도달하는 public 강제값은 0건이어야 한다
        // --------------------------------------------------------------------

        /// <summary>
        /// §E-6-c: C층에는 테스트 오버라이드를 두지 않는다. 두어야 한다면 <c>internal</c>이고,
        /// <b>그 사실을 감사가 잠근다</b> — 이 테스트가 그 «감사»다.
        /// </summary>
        [Test]
        public void C층에_도달하는_public_테스트_강제값이_없다()
        {
            // 이 세 숫자는 «막는 값»이 아니라 «스캐너가 눈이 멀었는지»를 재는 바닥값이다.
            // ★ 바닥값이지 상한이 아니다. 넓은 가족은 <b>다른 라운드가 테스트 이음매를 더할 때마다</b>
            //   늘어난다 — 실제로 이 개정 도중에도 63 → 64로 움직였다(다른 라운드의 CrispText 이음매).
            //   그래서 «정확히 N건»으로 잠그면 남의 라운드를 벌주는 감사가 된다. 여기서 잴 것은
            //   «스캐너가 눈이 멀었는가» 하나뿐이라 실측(63~64)의 3분의 1 아래로 넉넉히 잡는다.
            const int MinForcingSeams = 20;     // 2026-09-07 실측 63~64건(그중 public 45~46)

            string[] production = EntitlementAuditSource.ProductionSourceFiles();
            Assert.GreaterOrEqual(production.Length, EntitlementAuditSource.MinProductionFileCount,
                $"{LogPrefix} 프로덕션 .cs를 {production.Length}개밖에 읽지 못했습니다 — " +
                "아래 '위반 0건'은 측정이 아니라 착시가 됩니다.");

            Dictionary<string, string> world = StripAll(production);
            WorldScan scan = ScanWorld(world);

            // ---- 비공허성 ① : C층 탐지가 살아 있는가 ----
            // 앵커는 <b>실재하는 C층 상태 타입</b>이다. 이름이 바뀌면 nameof가 컴파일에서 먼저 죽는다.
            string anchorTypeName = nameof(StickMate.Core.PackEntitlementState);
            string anchorFile = null;
            foreach (string p in production)
                if (EntitlementAuditSource.DeclaresType(world[p], anchorTypeName)) { anchorFile = p; break; }

            Assert.IsNotNull(anchorFile,
                $"{LogPrefix} C층 상태 타입 {anchorTypeName}의 선언 파일을 찾지 못했습니다 — " +
                "타입 스캐너가 눈이 멀었거나 C층이 사라졌습니다. 어느 쪽이든 아래 판정은 무효입니다.");
            Assert.Contains(anchorFile, scan.CTierFiles,
                $"{LogPrefix} {anchorTypeName}을 선언하는 파일이 C층 파일 목록에 없습니다 " +
                $"(잡힌 것: {string.Join(", ", scan.CTierFiles)}).\n" +
                "CTierTypeNameFragments가 늙었다는 뜻이고, 그러면 이 감사는 <b>C층을 못 본 채</b> " +
                "'위반 0건'을 보고합니다.");

            // ---- 비공허성 ② : 강제값 스캐너가 살아 있는가 ----
            Assert.GreaterOrEqual(scan.Seams.Count, MinForcingSeams,
                $"{LogPrefix} 테스트 강제값 선언을 {scan.Seams.Count}건밖에 못 찾았습니다" +
                $"(2026-09-07 실측 63~64건, 바닥값 {MinForcingSeams}건). 이름 관례나 선언 모양이 " +
                "바뀌었다면 탐지 조건을 고치세요 — 그 전까지 아래 판정은 아무것도 재지 않습니다.");

            var report = new StringBuilder();
            report.Append(LogPrefix).Append(" C층 파일 ").Append(scan.CTierFiles.Count)
                  .Append("개 / 강제값 ").Append(scan.Seams.Count).Append("건(public ");
            int publicCount = 0;
            foreach (ForcingSeam s in scan.Seams) if (s.IsPublic) publicCount++;
            report.Append(publicCount).Append("건) / C층 도달 ").Append(scan.Violations.Count).Append("건\n");
            foreach (string cf in scan.CTierFiles) report.Append("  C층 ").Append(Path.GetFileName(cf)).Append('\n');
            Debug.Log(report.ToString());

            Assert.IsEmpty(scan.Violations,
                $"{LogPrefix} <b>C층(유료 권한) 판정에 도달하는 public 테스트 강제값</b>이 있습니다:\n" +
                string.Join("\n", scan.Violations) + "\n" +
                "§E-6-c: 그 강제값은 <b>internal</b>이어야 합니다. public이면 프로덕션 어셈블리 밖에서 " +
                "부를 수 있고, 그 한 줄이 결제 경계를 무력화합니다.\n" +
                "★ 이 실패는 «개수가 늘었다»가 아니라 «결제 경계에 닿았다»입니다 — " +
                "상한을 올려서 넘어갈 수 있는 종류가 아닙니다.");
        }

        // --------------------------------------------------------------------
        // 2-b. 명부 — 좁은 가족(Test+Override)의 public 목록을 <b>등호</b>로 잠근다
        // --------------------------------------------------------------------

        /// <summary>
        /// 2-a가 «결제 경계»를 잰다면 이쪽은 <b>습관</b>을 잰다. §E-6-c의 근거 문장이 지목한 가족
        /// (이름에 <c>Test</c>와 <c>Override</c>가 함께 들어가는 강제값)이 <b>소리 없이 늘거나 줄지</b>
        /// 못하게 한다.
        ///
        /// <para><b>개수 상한이 아니라 집합 등호</b>인 이유가 셋이다.
        /// (1) 개수는 <b>맞바꿈</b>을 못 본다 — 무해한 하나를 지우고 위험한 하나를 넣으면 숫자는 그대로다.
        /// (2) 개수는 <b>사라진 것</b>을 못 본다(부재 단언이 조용히 초록이 되는 그 형태).
        /// (3) 항목마다 «왜 이것이 C층에 안 닿는가»를 <b>사람이 한 번은 적게</b> 만든다.</para>
        ///
        /// <para>★ 이름을 <c>nameof</c>로 적는다 — 문자열로 베끼면 개명될 때 명부가 조용히 늙는다
        /// (CLAUDE.md 규칙). <c>nameof</c>는 개명되면 <b>컴파일이 실패</b>한다.</para>
        /// </summary>
        [Test]
        public void 좁은가족의_public_강제값_명부가_등호로_잠긴다()
        {
            // ---- 명부: 항목마다 «왜 C층에 안 닿는가»가 근거다(2026-09-07 실측) ----
            //  1·2  RopeClimbQaOverride — 도달 종점이 «배회 AI 로프 추첨 확률»(AutoWanderController)과
            //       «발판 목록의 합성 시험벽 1개»(FallbackPlatformWindowService)뿐이다.
            //       XP·동전·아이템·엔타이틀먼트 모델은 그 경로에 <b>한 곳도</b> 없다.
            //       public인 이유: PlayMode 어셈블리는 InternalsVisibleTo 대상이 아니다(AssemblyInfo.cs).
            //  3    StickMateDevTools — 개발 단축키 게이트. UnlockAll은 이 강제값이 아니라
            //       <b>순수 파서</b>(ResolveFromEnvironmentValue)만 공유하므로 이 강제값이 보유 판정에 닿지 않는다.
            //  4    AmbientCalendarClock — 가짜 벽시계 주입. ★ 구판 계기가 <b>못 보던</b> 항목이고
            //       (괄호 없는 속성), 프로덕션 «설치» 0건은 그 파일 전용 감사가 따로 잠근다.
            string[] expected =
            {
                nameof(StickMate.Core.RopeClimbQaOverride) + ".cs :: "
                    + nameof(StickMate.Core.RopeClimbQaOverride.SetChanceTestOverride),
                nameof(StickMate.Core.RopeClimbQaOverride) + ".cs :: "
                    + nameof(StickMate.Core.RopeClimbQaOverride.SetWallTestOverride),
                nameof(StickMate.Core.StickMateDevTools) + ".cs :: "
                    + nameof(StickMate.Core.StickMateDevTools.SetTestOverride),
                nameof(StickMate.Dialogue.AmbientCalendarClock) + ".cs :: "
                    + nameof(StickMate.Dialogue.AmbientCalendarClock.WallClockOverrideForTesting),
            };

            string[] production = EntitlementAuditSource.ProductionSourceFiles();
            Assert.GreaterOrEqual(production.Length, EntitlementAuditSource.MinProductionFileCount,
                $"{LogPrefix} 프로덕션 .cs를 {production.Length}개밖에 읽지 못했습니다 — 아래 대조가 공허해집니다.");

            WorldScan scan = ScanWorld(StripAll(production));

            var actual = new List<string>();
            var narrowAll = new List<string>();
            foreach (ForcingSeam s in scan.Seams)
            {
                if (!s.IsNarrowFamily) continue;
                narrowAll.Add(s.Entry + (s.IsPublic ? " (public)" : " (비공개)"));
                if (s.IsPublic && !actual.Contains(s.Entry)) actual.Add(s.Entry);
            }
            actual.Sort(StringComparer.Ordinal);
            Debug.Log($"{LogPrefix} 좁은 가족 {narrowAll.Count}건\n  " + string.Join("\n  ", narrowAll));

            // ★ 좁은 가족은 넓은 가족의 <b>부분집합</b>이어야 한다 — 두 자가 같은 계기임을 매 실행 확인한다.
            Assert.IsNotEmpty(narrowAll,
                $"{LogPrefix} 좁은 가족을 한 건도 못 찾았습니다. 이 저장소는 실제로 여러 건을 갖고 있으므로 " +
                "0건은 '없다'가 아니라 <b>스캐너가 눈이 멀었다</b>입니다.");

            var expectedSorted = new List<string>(expected);
            expectedSorted.Sort(StringComparer.Ordinal);

            var unexpected = new List<string>();
            foreach (string a in actual) if (!expectedSorted.Contains(a)) unexpected.Add(a);
            var missing = new List<string>();
            foreach (string e in expectedSorted) if (!actual.Contains(e)) missing.Add(e);

            Assert.IsEmpty(unexpected,
                $"{LogPrefix} 명부에 없는 <b>public</b> 테스트 강제값이 생겼습니다:\n  " +
                string.Join("\n  ", unexpected) + "\n" +
                "§E-6-c. 명부에 올리기 전에 <b>도달 범위를 먼저 재세요</b> — 그 강제값이 C층 판정에 " +
                "닿으면 위 «C층에_도달하는_public_테스트_강제값이_없다»가 이미 빨간색일 것이고, " +
                "그때는 명부 등재가 아니라 <b>internal 전환</b>이 답입니다.\n" +
                "★ 숫자를 올리는 식의 해소는 없습니다 — 이 검사는 개수가 아니라 <b>집합</b>을 봅니다.");

            Assert.IsEmpty(missing,
                $"{LogPrefix} 명부에 있는데 실물이 없습니다:\n  " + string.Join("\n  ", missing) + "\n" +
                "internal로 바꿨거나 걷어냈다면 명부에서도 지우세요. 그냥 두면 이 감사는 " +
                "<b>존재하지 않는 것</b>을 지키게 되고, 그 초록은 아무 뜻이 없습니다.");
        }

        // ====================================================================
        // 3. 네거티브 컨트롤
        // ====================================================================

        [Test]
        public void NegativeControl_새_파일이_스위치를_읽으면_잡는다()
        {
            string fake = "public static class FakeEntitlementGate\n" +
                          "{\n" +
                          "    public static bool HasPack(string id)\n" +
                          "        => EquipmentDebugUnlock.UnlockAll || StoreLevel(id);\n" +
                          "}\n";
            List<Reference> hits = FindReferences("Fake.cs",
                EntitlementAuditSource.StripComments(fake), SwitchTypeName);
            Assert.IsNotEmpty(hits,
                $"{LogPrefix} 새 파일의 스위치 참조를 놓쳤습니다 — 그러면 C층이 이 스위치에 " +
                "배선되어도 이 감사는 계속 초록입니다.");
        }

        [Test]
        public void NegativeControl_주석_속_스위치_언급은_참조가_아니다()
        {
            string fake = "public static class FakeGate\n" +
                          "{\n" +
                          "    /// <summary><see cref=\"EquipmentDebugUnlock.UnlockAll\"/>과 무관하다.</summary>\n" +
                          "    // EquipmentDebugUnlock을 여기서는 쓰지 않는다.\n" +
                          "    public static bool Always() => true;\n" +
                          "}\n";
            List<Reference> hits = FindReferences("Fake.cs",
                EntitlementAuditSource.StripComments(fake), SwitchTypeName);
            Assert.IsEmpty(hits,
                $"{LogPrefix} 주석 속 언급을 참조로 셌습니다. 이 저장소의 두 참조 파일은 " +
                "본문 1줄 + 주석 여러 줄이라, 주석을 세면 개수 기대값이 통째로 어긋납니다.");
        }

        [Test]
        public void NegativeControl_한글이_붙어도_참조를_놓치지_않는다()
        {
            // .NET 정규식 \b는 한글을 낱말 문자로 세어 경계를 만들지 않는다.
            // 이 저장소는 그 함정으로 참조 534건 중 245건(46%)을 놓친 적이 있다.
            string fake = "public static class FakeGate\n" +
                          "{\n" +
                          "    public static bool X() => EquipmentDebugUnlock.UnlockAll;\n" +
                          "    public static string 설명 = \"EquipmentDebugUnlock를 읽는다\";\n" +
                          "}\n";
            List<Reference> hits = FindReferences("Fake.cs",
                EntitlementAuditSource.StripComments(fake), SwitchTypeName);
            Assert.GreaterOrEqual(hits.Count, 2,
                $"{LogPrefix} 한글이 바로 뒤에 붙은 참조를 놓쳤습니다({hits.Count}건, 기대 2건 이상). " +
                "낱말 경계 판정이 유니코드에 물들면 이 감사의 탐지력이 절반으로 떨어집니다.");
        }

        [Test]
        public void NegativeControl_타입이름의_부분문자열은_참조가_아니다()
        {
            string fake = "public static class FakeGate\n" +
                          "{\n" +
                          "    public static bool X() => EquipmentDebugUnlockTests.Helper();\n" +
                          "}\n";
            List<Reference> hits = FindReferences("Fake.cs",
                EntitlementAuditSource.StripComments(fake), SwitchTypeName);
            Assert.IsEmpty(hits,
                $"{LogPrefix} 'EquipmentDebugUnlockTests'를 스위치 참조로 셌습니다 — " +
                "낱말 경계가 오른쪽에서 깨졌습니다.");
        }

        [Test]
        public void NegativeControl_원래_규칙이_사라진_참조를_잡는다()
        {
            string fake = "public static class FakeGate\n" +
                          "{\n" +
                          "    public static bool IsOwned() => EquipmentDebugUnlock.UnlockAll;\n" +
                          "}\n";
            List<Reference> hits = FindReferences("Fake.cs",
                EntitlementAuditSource.StripComments(fake), SwitchTypeName);
            Assert.AreEqual(1, hits.Count, $"{LogPrefix} 참조를 1건 찾지 못했습니다.");
            Assert.IsFalse(hits[0].RuleAnchorNearby,
                $"{LogPrefix} 원래 규칙이 하나도 없는데 앵커를 찾았다고 보고했습니다 — " +
                "이 검사는 '스위치가 판정 전체가 되는 것'을 잡으라고 있습니다.");
        }

        [Test]
        public void NegativeControl_선언줄은_참조로_세지_않는다()
        {
            string fake = "public static class EquipmentDebugUnlock\n" +
                          "{\n" +
                          "    public static bool UnlockAll => false;\n" +
                          "}\n";
            List<Reference> hits = FindReferences("Fake.cs",
                EntitlementAuditSource.StripComments(fake), SwitchTypeName);
            Assert.IsEmpty(hits,
                $"{LogPrefix} 선언 줄 자체를 참조로 셌습니다 — 선언 파일을 제외해도 " +
                "개수 기대값이 어긋납니다.");
        }

        // ====================================================================
        // 4. 대조 — 강제값 스캐너와 C층 도달 판정 (2026-09-07 security)
        // ====================================================================
        //
        // ★ 이 절이 없으면 2-a의 «위반 0건»은 아무것도 증명하지 못한다.
        //   이 저장소가 반복해 당한 형태가 정확히 그것이다 — <b>죽은 프로브의 0건이
        //   산 프로브의 0건과 똑같이 생겼다</b>. 아래는 양성(잡는다)과 음성(안 잡는다)을
        //   <b>같은 판정 함수</b>(<see cref="ScanWorld"/>)에 흘려 매 실행 교정한다.

        /// <summary>대조용 C층 파일 — 이름 조각 <c>Entitlement</c>를 가진 타입을 선언한다.</summary>
        private const string FakeCTierSource =
            "public enum FakeShopEntitlementState { Owned, NotOwned, Unknown }\n";

        [Test]
        public void 대조_C층_파일_안의_public_강제값을_잡는다()
        {
            WorldScan scan = ScanWorld(FakeWorld(
                ("CTier.cs", FakeCTierSource +
                             "public static class FakeCTierGate\n" +
                             "{\n" +
                             "    public static void SetTestOverride(bool v) { }\n" +
                             "}\n")));

            Assert.IsNotEmpty(scan.CTierFiles,
                $"{LogPrefix} 가짜 C층 파일을 C층으로 알아보지 못했습니다 — " +
                "그러면 진짜 C층이 들어와도 이 감사는 계속 초록입니다.");
            Assert.IsNotEmpty(scan.Violations,
                $"{LogPrefix} C층 파일 <b>안의</b> public 강제값(D0)을 놓쳤습니다.");
            StringAssert.Contains("D0", string.Join("\n", scan.Violations));
        }

        [Test]
        public void 대조_같은_강제값이_internal이면_C층_안에서도_통과한다()
        {
            WorldScan scan = ScanWorld(FakeWorld(
                ("CTier.cs", FakeCTierSource +
                             "public static class FakeCTierGate\n" +
                             "{\n" +
                             "    internal static void SetTestOverride(bool v) { }\n" +
                             "}\n")));

            // ★ 먼저 «찾기는 찾았는가»를 확인한다. 이게 없으면 통과의 이유가
            //   «internal이라서»인지 «못 봐서»인지 구분할 수 없다 — 그 둘은 똑같이 생겼다.
            Assert.IsNotEmpty(scan.Seams,
                $"{LogPrefix} internal 강제값 선언 자체를 못 찾았습니다. 그러면 바로 아래 " +
                "'위반 0건'은 접근성 판정이 아니라 <b>탐지 실패</b>입니다.");
            Assert.IsEmpty(scan.Violations,
                $"{LogPrefix} §E-6-c가 요구하는 <b>internal</b> 형태를 위반으로 셌습니다 — " +
                "그러면 이 감사는 올바른 처방을 벌주게 되고, 몇 번 만에 꺼집니다.");
        }

        [Test]
        public void 대조_C층이_참조하는_타입의_public_강제값을_잡는다()
        {
            WorldScan scan = ScanWorld(FakeWorld(
                ("CTier.cs", FakeCTierSource +
                             "public static class FakeCTierGate\n" +
                             "{\n" +
                             "    public static bool Has(string id) => FakeQaSwitch.Enabled;\n" +
                             "}\n"),
                ("Switch.cs", "public static class FakeQaSwitch\n" +
                              "{\n" +
                              "    public static bool Enabled;\n" +
                              "    public static void SetWallTestOverride(bool v) { }\n" +
                              "}\n")));

            Assert.IsNotEmpty(scan.Violations,
                $"{LogPrefix} C층이 <b>참조하는</b> 타입의 public 강제값(D1)을 놓쳤습니다 — " +
                "§E-6-b의 뜻은 'C층은 그 타입을 알지도 못한다'입니다. " +
                "이 경로를 못 보면 스위치를 옆 파일로 옮기는 것만으로 감사가 무력해집니다.");
            StringAssert.Contains("D1", string.Join("\n", scan.Violations));
        }

        [Test]
        public void 대조_C층_타입을_참조하는_파일의_public_강제값을_잡는다()
        {
            WorldScan scan = ScanWorld(FakeWorld(
                ("CTier.cs", FakeCTierSource),
                ("Switch.cs", "public static class FakeQaSwitch\n" +
                              "{\n" +
                              "    private static FakeShopEntitlementState _last;\n" +
                              "    public static void SetChanceTestOverride(float v) { }\n" +
                              "}\n")));

            Assert.IsNotEmpty(scan.Violations,
                $"{LogPrefix} 반대 방향(D2 — 강제값 파일이 C층 타입을 만지는 경우)을 놓쳤습니다.");
            StringAssert.Contains("D2", string.Join("\n", scan.Violations));
        }

        /// <summary>
        /// ★ <b>이 대조가 오늘의 통과를 정당화한다.</b> 밧줄등반 QA 스위치와 같은 모양 —
        /// C층과 참조 간선이 없는 파일의 public 강제값은 사거리 밖이다.
        /// <para>이게 없으면 «정밀화»가 «전부 통과»와 구분되지 않는다.</para>
        /// </summary>
        [Test]
        public void 대조_C층과_무관한_파일의_public_강제값은_사거리_밖이다()
        {
            WorldScan scan = ScanWorld(FakeWorld(
                ("CTier.cs", FakeCTierSource),
                ("RopeLike.cs", "public static class FakeRopeQa\n" +
                                "{\n" +
                                "    public static void SetWallTestOverride(bool v) { }\n" +
                                "    public static void SetChanceTestOverride(float v) { }\n" +
                                "}\n")));

            Assert.AreEqual(2, scan.Seams.Count,
                $"{LogPrefix} 강제값 2건을 찾지 못했습니다({scan.Seams.Count}건) — " +
                "아래 '사거리 밖' 판정이 탐지 실패와 구분되지 않습니다.");
            Assert.IsEmpty(scan.Violations,
                $"{LogPrefix} C층과 아무 참조 간선이 없는 파일의 강제값을 위반으로 셌습니다. " +
                "그러면 이 감사는 §E-6-c가 아니라 '프로덕션 전체의 public 개수'를 재는 " +
                "<b>구판의 거친 계기</b>로 되돌아간 것입니다.");
        }

        [Test]
        public void 대조_public_const는_강제값이_아니다()
        {
            // (a) 실재하는 모양 — FallbackPlatformWindowService의 시험벽 상수. 테스트가 프로덕션 값을
            //     숫자로 베끼지 않게 하려고 public이다(CLAUDE.md 규칙). 이건 <b>읽기</b>다.
            // (b) 이름까지 좁은 가족에 정확히 들어가는 const — 그래도 세면 안 된다.
            // (c) 같은 선언에서 const만 뺀 것 — 이건 <b>반드시</b> 세야 한다(양성 절반).
            WorldScan constants = ScanWorld(FakeWorld(
                ("Consts.cs", "public static class FakeConsts\n" +
                              "{\n" +
                              "    public const float RopeClimbTestWallAboveGroundHeightFraction = 0.15f;\n" +
                              "    public const bool WallTestOverrideDefault = false;\n" +
                              "}\n")));
            Assert.IsEmpty(constants.Seams,
                $"{LogPrefix} <c>public const</c>를 강제값으로 셌습니다. 상수는 바깥에서 값을 " +
                "밀어 넣을 수 없고, 테스트가 프로덕션 상수를 베끼지 않게 하는 <b>권장</b> 형태입니다 — " +
                "이걸 벌주면 CLAUDE.md의 '상수를 숫자로 베끼지 않는다'와 정면 충돌합니다.");

            WorldScan mutable = ScanWorld(FakeWorld(
                ("Mutable.cs", "public static class FakeMutable\n" +
                               "{\n" +
                               "    public static bool WallTestOverrideDefault = false;\n" +
                               "}\n")));
            Assert.IsNotEmpty(mutable.Seams,
                $"{LogPrefix} <c>const</c>만 뺀 <b>가변</b> 공개 필드를 놓쳤습니다 — " +
                "그러면 위 '상수는 안 센다'는 면제가 아니라 <b>맹점</b>입니다.");
        }

        [Test]
        public void 대조_읽기전용_getter는_강제값이_아니다()
        {
            WorldScan read = ScanWorld(FakeWorld(
                ("Read.cs", "public static class FakeRead\n" +
                            "{\n" +
                            "    public static int ShopEntryCountForTests => 7;\n" +
                            "}\n")));
            Assert.IsEmpty(read.Seams,
                $"{LogPrefix} 식 본문 <b>getter</b>를 강제값으로 셌습니다. 관찰 창구는 값을 " +
                "밀어 넣지 못하므로 §E-6-c의 대상이 아닙니다(이 저장소에 그 형태가 수십 건 있고, " +
                "전부 빨개지면 이 감사는 즉시 꺼집니다).");

            WorldScan write = ScanWorld(FakeWorld(
                ("Write.cs", "public static class FakeWrite\n" +
                             "{\n" +
                             "    public static void ResetForTests() { }\n" +
                             "}\n")));
            Assert.IsNotEmpty(write.Seams,
                $"{LogPrefix} 같은 이름 관례의 <b>쓰기</b> 메서드를 놓쳤습니다 — " +
                "그러면 위 면제는 면제가 아니라 맹점입니다.");
        }

        /// <summary>
        /// ★ <b>구판이 못 보던 구멍의 양성 대조.</b> 구판 탐지기는 줄에 <c>(</c>가 있어야만 셌고,
        /// 그래서 <b>괄호 없는 속성 선언</b>을 구조적으로 못 봤다. 실측으로 이 저장소에
        /// 그 형태가 실재한다(공개 설정 가능 속성, 좁은 가족).
        /// </summary>
        [Test]
        public void 대조_괄호없는_설정가능_속성도_강제값으로_센다()
        {
            const string declaration = "    public Func<DateTime> WallClockOverrideForTesting { get; set; }\n";
            Assert.Less(declaration.IndexOf('('), 0,
                $"{LogPrefix} 이 대조의 전제가 깨졌습니다 — 예시 선언에 '('가 들어 있으면 " +
                "구판 탐지기도 이걸 봤다는 뜻이라, 아래 검사는 구멍을 증명하지 못합니다.");

            WorldScan scan = ScanWorld(FakeWorld(
                ("Clock.cs", "public sealed class FakeClock\n" +
                             "{\n" + declaration + "}\n")));
            Assert.IsNotEmpty(scan.Seams,
                $"{LogPrefix} 괄호 없는 <b>설정 가능 속성</b>을 놓쳤습니다 — 구판이 뚫려 있던 바로 그 구멍입니다.");
            Assert.IsTrue(scan.Seams[0].IsNarrowFamily,
                $"{LogPrefix} 좁은 가족(Test+Override) 판정이 이 속성에 붙지 않았습니다 — " +
                "그러면 2-b의 명부가 이 항목을 영원히 못 봅니다.");
        }

        [Test]
        public void 대조_private_set_속성은_강제값이_아니다()
        {
            WorldScan closed = ScanWorld(FakeWorld(
                ("Closed.cs", "public sealed class FakeClosed\n" +
                              "{\n" +
                              "    public bool ForcedTestOverrideFlag { get; private set; }\n" +
                              "}\n")));
            Assert.IsEmpty(closed.Seams,
                $"{LogPrefix} <c>private set</c> 속성을 강제값으로 셌습니다 — 바깥에서 쓸 수 없으므로 " +
                "§E-6-c의 대상이 아닙니다.");

            WorldScan open = ScanWorld(FakeWorld(
                ("Open.cs", "public sealed class FakeOpen\n" +
                            "{\n" +
                            "    public bool ForcedTestOverrideFlag { get; set; }\n" +
                            "}\n")));
            Assert.IsNotEmpty(open.Seams,
                $"{LogPrefix} 공개 setter가 있는 같은 속성을 놓쳤습니다 — 그러면 위 면제는 맹점입니다.");
        }

        [Test]
        public void 대조_여러_줄로_흩어진_선언도_본다()
        {
            WorldScan multi = ScanWorld(FakeWorld(
                ("Multi.cs", "public sealed class FakeMulti\n" +
                             "{\n" +
                             "    public bool ForcedTestOverrideFlag\n" +
                             "    {\n" +
                             "        get; set;\n" +
                             "    }\n" +
                             "}\n")));
            Assert.IsNotEmpty(multi.Seams,
                $"{LogPrefix} 3줄로 흩어진 속성 선언을 놓쳤습니다 — 줄 단위로만 보면 " +
                "선언을 두 줄로 나누는 것만으로 이 감사를 피할 수 있습니다.");

            WorldScan expressionBodied = ScanWorld(FakeWorld(
                ("Expr.cs", "public sealed class FakeExpr\n" +
                            "{\n" +
                            "    public bool SetPanelShowsProgressForTests\n" +
                            "        => true;\n" +
                            "}\n")));
            Assert.IsEmpty(expressionBodied.Seams,
                $"{LogPrefix} 다음 줄에 <c>=></c>가 오는 <b>읽기 전용</b> 속성을 강제값으로 셌습니다 " +
                "(이 저장소에 실재하는 형태입니다).");
        }

        [Test]
        public void 대조_모든_타입이름_수집이_실제로_전부를_돌려준다()
        {
            // D1 판정은 «이 파일이 선언한 모든 타입 이름»에 의존한다. 그 수집이 조용히 0건이 되면
            // D1이 통째로 죽고, 그 0건은 '위반 없음'과 똑같이 생긴다.
            List<string> names = AllDeclaredTypeNames(EntitlementAuditSource.StripComments(
                "public static class Alpha { }\npublic enum Beta { X }\n"));
            Assert.Contains("Alpha", names,
                $"{LogPrefix} 선언 타입 전수 수집이 class를 놓쳤습니다 — D1 판정이 죽습니다.");
            Assert.Contains("Beta", names,
                $"{LogPrefix} 선언 타입 전수 수집이 enum을 놓쳤습니다 — D1 판정이 죽습니다.");
        }
    }
}
