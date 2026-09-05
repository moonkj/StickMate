using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StickMate.Core;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 원칙 2(비침해) 봉인 — ESC 클릭관통 긴급 OFF 핸들러는 <b>개발 게이트 뒤</b>에만 있다 (2026-09-05, persona-stress 실기 신고).
    ///
    /// <para>릴리스 빌드에서 톱니를 좌클릭해 포커스를 얻은 뒤 ESC 를 누르면 <c>StickmanAgent.Update</c>의 핸들러가
    /// <c>ApplyClickThrough(false)</c>를 불러 창 전체가 클릭을 삼키는 상태로 굳었고, 되돌리는 코드 경로가 없었다(유일한 복구 = 강제 종료).
    /// 리더 결정: 다른 dev 전용 키와 같은 <see cref="StickMateDevTools.Enabled"/> 게이트 뒤로 — 게이트가 닫히면 <b>조회조차</b> 안 한다.</para>
    ///
    /// <para>소스 감사다(입력 시스템은 EditMode 에서 흉내낼 수 없다). 니들은 게이트 프로퍼티 이름(<c>nameof</c>)과 키 상수 이름이고,
    /// <b>존재 대조</b>(그 상수가 실제로 선언돼 있다 · 게이트 패턴이 다른 dev 키에도 실재한다)와 <b>음성 대조</b>(게이트 없는 줄은 같은
    /// 판정기가 위반으로 낸다)를 같은 파일에서 함께 본다. 플랫폼 중립 파일이라 Windows 도 같은 수정으로 닫힌다.</para>
    /// </summary>
    public sealed class EmergencyClickThroughKeyGateTests
    {
        private const string AgentPath = "Assets/_Project/Scripts/Core/StickmanAgent.cs";
        private const string DirectorPath = "Assets/_Project/Scripts/Interaction/AppControlDirector.cs";
        private const string KeyConstName = "EmergencyDisableKey";

        private static string Root => Directory.GetParent(Application.dataPath).FullName;
        private static string Read(string rel) => File.ReadAllText(Path.Combine(Root, rel)).Replace("\r\n", "\n");

        /// <summary>같은 판정기 — 키 조회 줄들을 찾아 게이트 없는 것을 돌려준다.</summary>
        private static int UngatedQueries(string src, out int total)
        {
            string gate = nameof(StickMateDevTools) + "." + nameof(StickMateDevTools.Enabled);
            total = 0; int ungated = 0;
            foreach (Match m in Regex.Matches(src, @"^.*Input\.GetKeyDown\(" + KeyConstName + @"\).*$", RegexOptions.Multiline))
            {
                total++;
                string line = m.Value;
                int gateAt = line.IndexOf(gate + " &&", System.StringComparison.Ordinal);
                int queryAt = line.IndexOf("Input.GetKeyDown(", System.StringComparison.Ordinal);
                if (gateAt < 0 || gateAt > queryAt) ungated++;
            }
            return ungated;
        }

        [Test]
        public void ESC_긴급_클릭관통_OFF는_개발_게이트가_앞에_붙은_줄에서만_조회된다()
        {
            string src = Read(AgentPath);
            // 존재 대조 1 — 키 상수가 실재한다(이름이 바뀌면 이 감사는 아무것도 못 본다).
            Assert.IsTrue(Regex.IsMatch(src, @"const KeyCode " + KeyConstName + @"\s*="), $"{KeyConstName} 상수 선언을 찾지 못했습니다 — 감사 대상이 사라졌거나 이름이 바뀌었습니다.");

            int ungated = UngatedQueries(src, out int total);
            Assert.Greater(total, 0, $"{KeyConstName} 조회 줄이 하나도 없습니다 — 핸들러가 통째로 사라졌으면 이 감사도 함께 정리하십시오.");
            Assert.AreEqual(0, ungated,
                $"{KeyConstName} 조회 {ungated}/{total}건이 개발 게이트({nameof(StickMateDevTools)}.{nameof(StickMateDevTools.Enabled)} &&) 앞에 있지 않습니다 — " +
                "릴리스 빌드에서 ESC 한 번에 창 전체가 클릭을 삼킵니다(원칙 2 위반, 되돌릴 경로 없음).");

            // 존재 대조 2 — 같은 게이트 패턴이 다른 dev 전용 키에도 실재한다(패턴 자체가 살아 있다).
            Assert.Greater(Regex.Matches(Read(DirectorPath), nameof(StickMateDevTools) + @"\." + nameof(StickMateDevTools.Enabled)).Count, 0,
                "다른 dev 키의 게이트 패턴을 찾지 못했습니다 — 이 감사가 요구하는 형태가 저장소에 더 없다면 형태를 다시 정하십시오.");
        }

        /// <summary>음성 대조 — 게이트 없는 줄(옛 형태)과 게이트가 뒤에 붙은 줄은 같은 판정기가 위반으로 낸다.</summary>
        [Test]
        public void 컨트롤_게이트_없는_조회_줄은_판정기가_잡는다()
        {
            string bare = "            if (Input.GetKeyDown(" + KeyConstName + "))\n";
            Assert.AreEqual(1, UngatedQueries(bare, out int t1)); Assert.AreEqual(1, t1);
            string after = "            if (Input.GetKeyDown(" + KeyConstName + ") && " + nameof(StickMateDevTools) + "." + nameof(StickMateDevTools.Enabled) + ")\n";
            Assert.AreEqual(1, UngatedQueries(after, out _), "게이트가 조회 뒤에 있으면 조회는 이미 일어난 뒤다 — 위반으로 잡아야 합니다.");
            string good = "            if (" + nameof(StickMateDevTools) + "." + nameof(StickMateDevTools.Enabled) + " && Input.GetKeyDown(" + KeyConstName + "))\n";
            Assert.AreEqual(0, UngatedQueries(good, out int t3)); Assert.AreEqual(1, t3);
        }

        /// <summary>게이트 자체의 릴리스 값 — 컴파일 심볼도 환경변수도 없으면 닫힌다(<see cref="StickMateDevTools.ResolveFromEnvironmentValue"/>가 null 에 false).</summary>
        [Test]
        public void 릴리스_조건에서는_게이트가_닫힌다()
        {
            Assert.IsFalse(StickMateDevTools.ResolveFromEnvironmentValue(null), "환경변수가 없는데 게이트가 열립니다.");
            Assert.IsFalse(StickMateDevTools.ResolveFromEnvironmentValue(""), "빈 환경변수에 게이트가 열립니다.");
        }
    }
}
