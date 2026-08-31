using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// 회귀 잠금 — <b>주 디스플레이 ID를 캐시하지 않는다</b>(2026-08-31 R5 Major 1).
    ///
    /// <para><c>CGMainDisplayID()</c>는 디스플레이 구성이 바뀌면 값이 바뀐다(클램셸, 모니터 연결/해제,
    /// 미러링). <c>MacViewerPresenceService</c> 인스턴스는 앱 시작 시
    /// 한 번 만들어져 24시간 사는데, 시작 시점의 ID를 캐시하면 노트북 덮개를 닫는 순간 이미 꺼진 내장
    /// 패널을 계속 물어보게 되어 <c>DisplayOff</c>(4fps)에 영구 고착된다(복구 = 앱 재시작).</para>
    ///
    /// <para><b>왜 정적 스캔인가</b>: 이 결함은 <b>실제 디스플레이 구성 변경</b>이 있어야 런타임에
    /// 드러나고, 그건 헤드리스 배치 테스트로 만들 수 없다. 또한 해당 파일은
    /// <c>#if UNITY_STANDALONE_OSX</c> 안이라 리플렉션으로 잡히지 않을 수 있다. 그래서
    /// <c>UserAssetImmutabilityAuditTests</c>와 같은 소스 텍스트 스캔으로 "캐시가 없는 형태"를 잠근다 —
    /// 하드웨어 없이 잠글 수 있는 유일한 방법이다.</para>
    /// </summary>
    public class MainDisplayFreshnessTests
    {
        private static string ReadPresenceSource()
        {
            string path = Path.Combine(Application.dataPath,
                "_Project", "Scripts", "Platform", "MacOS", "MacViewerPresenceService.cs");
            Assert.IsTrue(File.Exists(path), $"소스를 찾지 못했다: {path}");
            return File.ReadAllText(path);
        }

        [Test]
        public void 주디스플레이ID는_매_폴링마다_재조회된다()
        {
            string source = ReadPresenceSource();

            int probe = source.IndexOf("public bool TryGetPresence", StringComparison.Ordinal);
            Assert.Greater(probe, 0, "TryGetPresence가 사라졌거나 이름이 바뀌었다 — 이 테스트를 갱신하라.");

            // 폴링 진입점 본문 안에서 조회가 일어나는지까지 본다(파일 어딘가에 있기만 한 것으로는 부족).
            int bodyEnd = source.IndexOf("\n        private ", probe, StringComparison.Ordinal);
            if (bodyEnd < 0) bodyEnd = source.Length;
            string body = source.Substring(probe, bodyEnd - probe);

            StringAssert.Contains("CGDisplayIsAsleep(CGMainDisplayID())", body,
                "주 디스플레이 ID를 폴링마다 재조회하지 않는다 — 캐시된 ID를 쓰면 클램셸/외장 모니터 " +
                "전환 시 4fps(DisplayOff)에 영구 고착된다.");
        }

        [Test]
        public void 네거티브컨트롤_주디스플레이ID를_담아둘_필드가_존재하지_않는다()
        {
            string source = ReadPresenceSource();

            // 위 테스트는 "재조회 형태가 있는가"만 본다. 캐시 필드를 되살려 두 경로가 공존하는 어중간한
            // 상태(가장 헷갈리는 회귀)를 이쪽에서 막는다. uint = CGDirectDisplayID.
            StringAssert.DoesNotContain("_mainDisplayResolved", source,
                "주 디스플레이 ID 캐시가 되살아났다(R5 Major 1 회귀).");
            StringAssert.DoesNotContain("private uint _", source,
                "CGDirectDisplayID(uint)를 인스턴스 필드에 보관하는 것은 캐시 회귀다.");
        }
    }
}
