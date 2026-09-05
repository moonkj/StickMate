using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-05 (M-8) — Windows 가상 데스크톱 <b>소속 조회</b> 구현의 정적 감사.
    ///
    /// ============================================================================
    /// 왜 리플렉션이 아니라 소스 스캔인가
    /// ============================================================================
    /// <c>Platform/Windows/WindowsVirtualDesktopProbe.cs</c>는 파일 전체가
    /// <c>#if UNITY_STANDALONE_WIN</c> 안이라 <b>macOS 타깃에서는 타입이 존재하지 않는다</b>
    /// (CLAUDE.md 「활성 빌드 타깃 규칙」). 리플렉션으로는 영원히 못 본다.
    ///
    /// ============================================================================
    /// ★★ 이 파일이 대신 서 주는 것 — <b>이 코드는 이 머신에서 실행되지 않는다</b>
    /// ============================================================================
    /// COM 식별자(CLSID/IID)와 <b>vtable 슬롯 순서</b>는 둘 다 <b>한 글자·한 줄만 틀려도
    /// 컴파일은 되고 실행만 조용히 실패한다</b>. 슬롯 순서가 어긋나면 더 나쁘다 — 「조회」를
    /// 부른 줄 알았는데 실제로는 <b>창을 옮기는</b> 슬롯이 불린다. 그래서 값과 순서를
    /// <b>1차 출처(Microsoft Learn / <c>shobjidl_core.h</c>)</b>와 대조한다.
    /// (같은 처방이 <c>SystemAudioActivityProbeAuditTests</c>와 <c>ITaskbarList</c> 쪽에 이미 있다.)
    /// </summary>
    public sealed class VirtualDesktopProbeAuditTests
    {
        private static string ProbePath => Path.Combine(Application.dataPath,
            "_Project", "Scripts", "Platform", "Windows", "WindowsVirtualDesktopProbe.cs");

        private static string Read()
        {
            Assert.IsTrue(File.Exists(ProbePath),
                $"가상 데스크톱 프로브 소스를 찾지 못했습니다({ProbePath}) — 파일이 옮겨졌다면 이 감사도 " +
                "함께 갱신하세요. 그대로 두면 모든 판정이 조용히 무의미해집니다.");
            return File.ReadAllText(ProbePath).Replace("\r\n", "\n");
        }

        /// <summary>
        /// <paramref name="signature"/>로 시작하는 멤버의 <b>몸통만</b> 잘라낸다 — 다음 멤버 선언
        /// (같은 들여쓰기의 <c>private</c>/<c>internal</c>/<c>public</c>) 직전까지.
        /// <para>중괄호를 세지 <b>않는</b> 이유: 보간 문자열(<c>$"{x:F1}"</c>)의 중괄호가 섞여 있어
        /// 단순 짝맞추기가 어긋난다. 검사 창이 좁기만 하면 되므로 선언 경계로 자르는 편이 안전하고,
        /// <b>실제로 좁게 잘렸는지는 호출부가 음성 대조로 확인한다</b>.</para>
        /// </summary>
        private static string MethodBodyAfter(string code, string signature)
        {
            int start = code.IndexOf(signature, StringComparison.Ordinal);
            if (start < 0) return string.Empty;

            int end = code.Length;
            foreach (string boundary in new[] { "\n        private ", "\n        internal ", "\n        public " })
            {
                int at = code.IndexOf(boundary, start + signature.Length, StringComparison.Ordinal);
                if (at >= 0 && at < end) end = at;
            }
            return code.Substring(start, end - start);
        }

        /// <summary>주석(<c>//</c>·<c>///</c>·블록 본문)을 걷어낸 «실제 코드». 결함을 설명하는 주석이
        /// 구현으로 오인되던 함정이 이 저장소에서 실제로 두 번 났다.</summary>
        private static string StripComments(string source)
        {
            var sb = new StringBuilder(source.Length);
            foreach (string line in source.Split('\n'))
            {
                string t = line.TrimStart();
                if (t.StartsWith("//", StringComparison.Ordinal)) continue;
                if (t.StartsWith("*", StringComparison.Ordinal)) continue;
                sb.Append(line).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>
        /// ★ 값은 <b>Microsoft의 1차 출처</b>에서 왔다(우리 소스에서 복사한 것이 아니다).
        /// 이 머신에서는 실행으로 확인할 수 없으므로 이 대조가 유일한 방어선이다.
        /// </summary>
        [Test]
        public void COM_식별자가_1차_출처와_같고_서로_다르다()
        {
            string src = Read();

            var expected = new Dictionary<string, string>
            {
                ["VirtualDesktopManagerClsid"] = "AA509086-5CA9-4C25-8F95-589D3C07B48A", // CLSID_VirtualDesktopManager
                ["VirtualDesktopManagerIid"] = "A5CD92FF-29BE-454C-8D04-D82879FB3F1B",   // IID_IVirtualDesktopManager
            };

            var seen = new List<string>();
            foreach (KeyValuePair<string, string> pair in expected)
            {
                Match m = Regex.Match(src, @"\b" + pair.Key + @"\s*=\s*""([0-9A-Fa-f\-]{36})""");
                Assert.IsTrue(m.Success,
                    $"{pair.Key}의 GUID 리터럴을 소스에서 찾지 못했습니다 — 이름이나 표기가 바뀌었다면 " +
                    "이 감사도 함께 고치십시오.");

                string actual = m.Groups[1].Value;
                Assert.DoesNotThrow(() => new Guid(actual), $"{pair.Key}가 GUID로 파싱되지 않습니다: {actual}");
                Assert.AreEqual(pair.Value.ToUpperInvariant(), actual.ToUpperInvariant(),
                    $"★ {pair.Key}가 1차 출처 값과 다릅니다. 한 글자만 틀려도 개체 생성이나 " +
                    "QueryInterface가 **조용히** 실패하고, 이 머신에는 Windows가 없어 실행으로 잡을 수 없습니다.");

                Assert.IsFalse(seen.Contains(actual.ToUpperInvariant()),
                    $"★ {pair.Key}가 다른 식별자와 같은 값입니다 — 복사-붙여넣기 사고입니다.");
                seen.Add(actual.ToUpperInvariant());
            }

            Assert.AreEqual(expected.Count, seen.Count, "식별자 수집이 어긋났습니다.");
        }

        /// <summary>
        /// ★★ <b>슬롯 순서가 곧 ABI다.</b> <c>IVirtualDesktopManager</c>의 슬롯은
        /// <c>IsWindowOnCurrentVirtualDesktop</c> → <c>GetWindowDesktopId</c> →
        /// <c>MoveWindowToDesktop</c> 순이다(1차 출처: <c>shobjidl_core.h</c>).
        ///
        /// <para>줄 순서가 바뀌면 <b>「지금 보이는가」를 물었는데 「이 창을 옮겨라」가 불린다</b> —
        /// 컴파일도 되고, 이 머신에서는 실행조차 되지 않으니 아무도 모른다. 그래서 순서를 잠근다.</para>
        /// </summary>
        [Test]
        public void vtable_슬롯_순서가_1차_출처와_같다()
        {
            string code = StripComments(Read());

            int slot0 = code.IndexOf("IsWindowOnCurrentVirtualDesktop(IntPtr", StringComparison.Ordinal);
            int slot1 = code.IndexOf("ReservedSlot1(IntPtr", StringComparison.Ordinal);
            int slot2 = code.IndexOf("ReservedSlot2(IntPtr", StringComparison.Ordinal);

            Assert.Greater(slot0, -1, "슬롯 0(IsWindowOnCurrentVirtualDesktop) 선언을 찾지 못했습니다.");
            Assert.Greater(slot1, -1, "슬롯 1 자리표시자가 없습니다 — 슬롯을 비우면 그 아래 슬롯이 " +
                "한 칸씩 당겨져 <b>엉뚱한 함수</b>가 불립니다.");
            Assert.Greater(slot2, -1, "슬롯 2 자리표시자가 없습니다 — 같은 이유입니다.");

            Assert.Less(slot0, slot1, "슬롯 0이 슬롯 1보다 뒤에 선언됐습니다 — vtable 순서가 어긋났습니다.");
            Assert.Less(slot1, slot2, "슬롯 1이 슬롯 2보다 뒤에 선언됐습니다 — vtable 순서가 어긋났습니다.");
        }

        /// <summary>
        /// ★★★ <b>읽기 전용 보증</b> — 창을 옮기는 슬롯은 <b>이름으로도 등장하지 않는다</b>.
        ///
        /// <para>절대 불변 원칙 3의 승인된 예외는 <b>작업표시줄 자동 숨김 비트 하나뿐</b>이고
        /// (<c>docs/TASKBAR_REVEAL.md</c>), 가상 데스크톱 이동은 거기 포함되지 않는다.
        /// 우리 창이라도 마찬가지다 — 어느 데스크톱에서 앱을 켰는지는 사용자의 결정이다.</para>
        ///
        /// <para><b>부재 단언에 존재 대조를 붙인다</b>(CLAUDE.md): 아래 '없음' 판정 앞에
        /// '있어야 하는 것'을 먼저 요구한다. 그러지 않으면 파일을 못 읽어 <b>아무것도 없는 상태</b>가
        /// 조용한 초록이 된다.</para>
        /// </summary>
        [Test]
        public void 창을_다른_데스크톱으로_옮기는_API가_없다()
        {
            string code = StripComments(Read());

            // 양성 대조 — 스캐너가 눈이 멀지 않았는가.
            StringAssert.Contains("IsWindowOnCurrentVirtualDesktop", code,
                "양성 대조 실패 — 주석 제거 뒤 소스에서 조회 호출조차 못 찾았습니다. " +
                "이 스캐너는 눈이 멀어 있고 아래 '없음' 판정은 무효입니다.");

            Assert.AreEqual(-1, code.IndexOf("MoveWindowToDesktop", StringComparison.Ordinal),
                "★ 창을 다른 데스크톱으로 <b>옮기는</b> API가 선언됐습니다 — 이 계약은 읽기 전용이고, " +
                "그 슬롯은 이름 없는 자리표시자로만 채웁니다(원칙 3).");
            Assert.AreEqual(-1, code.IndexOf("SetWindowDesktopId", StringComparison.Ordinal),
                "★ 데스크톱 소속을 <b>쓰는</b> 호출이 들어왔습니다 — 위와 같은 이유로 금지입니다.");

            // ★ 비공개 API 금지 — 리더가 기각한 옵션 (b)의 정확한 형태.
            //   빌드마다 IID가 바뀌어 OS 업데이트에서 깨지고, 비문서 API 누적은 백신 휴리스틱
            //   위험을 키운다(사용자 실기는 AhnLab V3 — docs/security/ENTITLEMENT_CONTRACT.md S-3).
            foreach (string forbidden in new[]
                     {
                         "IVirtualDesktopManagerInternal",
                         "IVirtualDesktopNotificationService",
                         "IApplicationViewCollection",
                     })
            {
                Assert.AreEqual(-1, code.IndexOf(forbidden, StringComparison.Ordinal),
                    $"★ 비공개 COM({forbidden})이 들어왔습니다 — 공개 API만 쓴다는 것이 " +
                    "이 라운드에서 옵션 (a)를 채택한 근거 그 자체입니다.");
            }
        }

        /// <summary>
        /// ★ 24시간 상주 앱 규율 — <b>실패가 무한 재시도로 굳지 않는가.</b>
        /// <para>탐색기 재시작으로 프록시가 끊기면 한 번은 다시 만들어 봐야 하지만, 영원히
        /// 1.5초마다 <c>CoCreateInstance</c>를 재시도하면 그 자체가 상주 앱의 비용이 된다.</para>
        /// </summary>
        [Test]
        public void 연속_실패에_상한이_있다()
        {
            string src = Read();

            Match m = Regex.Match(src, @"\bMaxConsecutiveFailures\s*=\s*(\d+)\s*;");
            Assert.IsTrue(m.Success,
                "연속 실패 상한 상수를 찾지 못했습니다 — 없으면 조회 실패가 영원한 재시도 루프가 됩니다.");

            int limit = int.Parse(m.Groups[1].Value);
            Assert.Greater(limit, 1,
                "상한이 1 이하입니다 — 탐색기 재시작으로 프록시가 한 번 끊기면 그대로 영구 포기하고, " +
                "그 세션 내내 데스크톱 전환을 감지하지 못합니다.");
            Assert.LessOrEqual(limit, 20,
                "상한이 너무 큽니다 — 24시간 상주 앱에서 폴링마다 COM 개체를 다시 만드는 구간이 " +
                "그만큼 길어집니다.");
        }

        // ====================================================================================
        // ★ 2026-09-05 (perf-doc 지적) — 메인 스레드에서 셸에 보내는 동기 요청
        // ====================================================================================
        // Query()는 explorer.exe로 <b>동기</b>로 건너간다. 셸이 느려지거나 멈추면 그 프레임이 통째로
        // 멈추는데, 계측이 없으면 그 시간은 [스톨귀인]의 «기타로직»이라는 <b>이름 없는 잔차</b>로
        // 흘러가 원장이 <b>우리 코드</b>를 가리킨다(RecordFullscreenProbe 문서의 「원장의 17%가
        // 비어 있었다」와 같은 형태). 계측과 워치독은 <b>기능이 아니라서 빠져도 아무 테스트가
        // 깨지지 않는다</b> — 그래서 여기서 소스로 잠근다.

        /// <summary>
        /// ★ 계측이 <b>Query 안에</b>, 그리고 <b>COM 호출보다 먼저</b> 배선돼 있는가.
        /// <para>호출 뒤에 열리면 정작 멈추는 그 구간이 장부 밖에 남는다 — 계측이 있는 것처럼 보이면서
        /// 아무것도 재지 않는, 이 저장소가 반복해 겪은 조용한 실패 형태다.</para>
        /// </summary>
        [Test]
        public void 셸_동기요청이_스톨구간_계측에_들어와_있다()
        {
            string code = StripComments(Read());

            int queryAt = code.IndexOf("VirtualDesktopMembership Query(", StringComparison.Ordinal);
            int sectionAt = code.IndexOf("StallSection.VirtualDesktop", StringComparison.Ordinal);
            int comCallAt = code.IndexOf("IsWindowOnCurrentVirtualDesktop(topLevelWindow",
                StringComparison.Ordinal);

            Assert.Greater(queryAt, -1, "Query() 정의를 찾지 못했습니다 — 이 감사의 전제가 깨졌습니다.");
            Assert.Greater(comCallAt, -1,
                "COM 호출 지점을 찾지 못했습니다 — 위 전제가 깨졌으므로 아래 순서 판정은 무효입니다.");
            Assert.Greater(sectionAt, -1,
                "★ 셸에 동기 요청을 보내는 구간에 [스톨구간] 계측이 없습니다. 사용자 히칭 신고가 왔을 때 " +
                "이 호출을 배제할 수도, 지목할 수도 없게 됩니다(perf-doc 필수 권고 2026-09-05).");

            Assert.Less(queryAt, sectionAt, "계측이 Query() 밖에 있습니다.");
            Assert.Less(sectionAt, comCallAt,
                "★ 계측이 COM 호출 <뒤>에 열립니다 — 정작 멈추는 구간이 장부에 들어오지 않습니다.");
        }

        /// <summary>
        /// ★★ <b>니들이 가리키는 열거값이 실재하는가</b> — 위 검사는 <b>문자열</b>일 뿐이다.
        ///
        /// <para>플랫폼 중립 파일(<c>Platform/StallAttribution.cs</c>)은 macOS 타깃에서도 컴파일되므로
        /// 여기만은 <b>리플렉션</b>으로 잰다. 두 검사가 서로 다른 방법을 쓰는 것이 요점이다 —
        /// 소스 텍스트만 보면 <c>StallSection.VirtualDesktop</c>이 <b>존재하지 않는 이름</b>이어도
        /// (Windows 타깃에서만 컴파일되므로 이 머신에서는 컴파일 에러조차 나지 않는다) 초록이 된다.</para>
        /// </summary>
        [Test]
        public void 계측_구간이_중립_열거형에_실재하고_이름표가_있다()
        {
            Assert.IsTrue(Enum.IsDefined(typeof(StickMate.Platform.StallSection), "VirtualDesktop"),
                "★ Windows 소스가 참조하는 StallSection.VirtualDesktop이 중립 열거형에 없습니다 — " +
                "그 파일은 이 머신에서 컴파일되지 않으므로 이 어긋남은 <b>실기에서만</b> 드러납니다.");

            string label = StickMate.Platform.StallAttribution.SectionName(
                StickMate.Platform.StallSection.VirtualDesktop);
            Assert.AreNotEqual("알수없음", label,
                "구간에 사람이 읽는 이름표가 없습니다 — 실기 로그가 '구간 10번'이라고 말하면 아무도 못 읽습니다.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(label));
        }

        /// <summary>
        /// ★ <b>느린 성공</b>도 실패로 세는가(perf-doc 권고 워치독).
        /// <para>셸이 응답만 하고 느리면 HRESULT는 <c>S_OK</c>라 기존 카운터에 아무것도 잡히지 않는다.
        /// 그때 대가는 <b>매 폴링마다 프레임이 멈추는 것</b>이고, 24시간 상주 앱에서 이건 「가끔
        /// 히칭한다」는 신고로 나타난다.</para>
        /// </summary>
        [Test]
        public void 느린_조회가_기존_실패_래치에_반영된다()
        {
            string src = Read();

            Match m = Regex.Match(src, @"\bSlowQueryBudgetMs\s*=\s*([0-9]+(?:\.[0-9]+)?)\s*;");
            Assert.IsTrue(m.Success,
                "★ 소요시간 예산 상수가 없습니다 — 느린 성공(S_OK인데 수백 ms)이 어떤 카운터에도 " +
                "잡히지 않고, 그 프레임 멈춤이 영원히 계속됩니다.");

            double budget = double.Parse(m.Groups[1].Value,
                System.Globalization.CultureInfo.InvariantCulture);
            Assert.Greater(budget, 0.0, "예산이 0 이하입니다 — 정상 조회까지 전부 실패로 셉니다.");
            Assert.LessOrEqual(budget, 16.7,
                "예산이 60fps 프레임 예산(16.7ms)보다 큽니다 — 한 프레임을 통째로 날리는 조회가 " +
                "'정상'으로 통과합니다.");

            string code = StripComments(src);
            StringAssert.Contains("SlowQueryBudgetMs", code,
                "예산 상수가 선언만 되고 코드에서 쓰이지 않습니다 — 상수는 있는데 아무도 안 보는 형태입니다.");

            // ★ 새 정책을 만들지 않았는가 — 기존 상한/래치를 그대로 재사용해야 한다.
            //   ★ 파일 전체가 아니라 <b>그 함수 몸통만</b> 본다. 전체를 보면 아래쪽 RecordFailure가
            //     같은 이름들을 갖고 있어 <b>워치독이 비어 있어도 초록</b>이 된다(거짓 통과).
            string slowBody = MethodBodyAfter(code, "private bool RecordSlowQueryIfOverBudget");
            Assert.IsNotEmpty(slowBody, "워치독 함수 몸통을 잘라내지 못했습니다 — 이 감사의 전제가 깨졌습니다.");

            // 대조 — 잘라내기가 정말로 <b>좁게</b> 잘랐는가. 아래 이름은 다음 함수(EnsureManager)의
            // 것이므로 여기 들어와 있으면 창이 새어 위 판정이 무의미하다.
            Assert.AreEqual(-1, slowBody.IndexOf("Type.GetTypeFromCLSID", StringComparison.Ordinal),
                "★ 잘라낸 창이 다음 함수까지 삼켰습니다 — 이 창으로는 아무것도 못 잽니다.");

            StringAssert.Contains("MaxConsecutiveFailures", slowBody,
                "★ 워치독이 기존 연속 실패 상한을 쓰지 않습니다 — 정지 규칙이 두 벌이 되면 " +
                "반드시 한쪽만 고쳐집니다.");
            StringAssert.Contains("_unavailable", slowBody,
                "★ 워치독이 기존 영구 정지 래치를 쓰지 않습니다 — 같은 이유입니다.");
        }
    }
}
