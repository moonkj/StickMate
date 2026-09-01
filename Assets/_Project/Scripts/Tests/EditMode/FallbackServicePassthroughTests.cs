using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using StickMate.Platform;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// <see cref="FallbackPlatformWindowService"/>가 <b>안쪽 서비스의 선택적 인터페이스를 하나도
    /// 감추지 않는지</b> 리플렉션으로 감사한다.
    ///
    /// ============================================================================
    /// 왜 생겼는가 — 같은 결함이 세 번 반복됐다
    /// ============================================================================
    /// 이 데코레이터는 실제 플랫폼 서비스를 감싸 "발판이 0개일 때 화면 하단 합성 발판"을 보장한다.
    /// 그런데 소비자들은 하나같이 <c>PlatformService as I어떤서비스</c>로 <b>선택적 인터페이스</b>를
    /// 캐스팅해 쓴다. 데코레이터가 그 인터페이스를 통과시키지 않으면 <b>캐스팅이 조용히 null이 되고,
    /// 그 기능은 "미지원"으로 폴백해 영영 죽는다.</b> 예외도 로그도 없다 — 그게 이 결함이 세 번이나
    /// 반복된 이유다:
    /// <list type="number">
    /// <item><b>2026-08-28</b> — <c>IGlobalPointerButtonService</c>. 사용자 신고 "마우스로 안 잡힘".
    ///   실측 로그가 <c>전역버튼경로=미지원</c>이었고, 창 포커스와 무관한 전역 버튼 폴링 경로가
    ///   <b>한 번도 활성화된 적이 없었다.</b></item>
    /// <item><b>2026-08-29</b> — <c>IRawWindowRectSource</c>. 창 도둑이 대상 창을 하나도 못 찾았다.</item>
    /// <item><b>2026-09-01</b> — <c>IWindowEnumerationCostSource</c>. 스파이크 계측이
    ///   <c>전체 창 -1개, 정밀검사 -1회</c>로 찍혔다. "창이 800개일 때 비싸지는가"를 원격에서
    ///   확인할 수단이 통째로 죽어 있었다.</item>
    /// </list>
    ///
    /// <b>네 번째를 막는 것이 이 테스트의 유일한 목적이다.</b> 인터페이스 이름을 하드코딩하지 않고
    /// "실제 플랫폼 서비스가 구현한 것"을 리플렉션으로 모아 대조하므로, 앞으로 새 선택적 인터페이스가
    /// 생기면 <b>아무도 이 파일을 고치지 않아도</b> 자동으로 감사 대상에 들어온다.
    /// </summary>
    public class FallbackServicePassthroughTests
    {
        private const string LogPrefix = "[데코레이터통과]";

        /// <summary>
        /// 데코레이터가 <b>일부러 통과시키지 않는</b> 인터페이스와 그 이유.
        /// 항목을 추가하려면 "왜 통과가 아니라 소비인가"를 여기 적어야 한다 — 이유를 적을 수 없다면
        /// 그건 통과시켜야 하는 것이다.
        /// </summary>
        private static readonly Dictionary<string, string> DeliberatelyConsumedNotForwarded =
            new Dictionary<string, string>
            {
                {
                    nameof(IDockMetricsService),
                    "이 데코레이터는 Dock 실측을 **소비해서 합성 발판을 만드는** 쪽이다(TryGetDockRectOsScreen). " +
                    "그대로 통과시키면 소비자가 '데코레이터가 보정한 Dock'과 '원본 Dock' 중 어느 쪽을 " +
                    "보는지 알 수 없게 되어, Dock 사각형 단일 소스 계약이 깨진다."
                },
            };

        /// <summary>감사 대상이 되는 실제 플랫폼 서비스 구현체들(에디터에서 인스턴스화하지 않고 타입만 본다).</summary>
        private static IEnumerable<Type> RealPlatformServiceTypes()
        {
            var assembly = typeof(FallbackPlatformWindowService).Assembly;
            return assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => typeof(IPlatformWindowService).IsAssignableFrom(t))
                .Where(t => t != typeof(FallbackPlatformWindowService))
                .OrderBy(t => t.Name);
        }

        [Test]
        public void 안쪽_서비스의_선택적_인터페이스는_전부_데코레이터를_통과한다()
        {
            var decorator = typeof(FallbackPlatformWindowService);
            var decoratorInterfaces = new HashSet<Type>(decorator.GetInterfaces());
            var violations = new List<string>();
            int audited = 0;

            foreach (var serviceType in RealPlatformServiceTypes())
            {
                foreach (var iface in serviceType.GetInterfaces())
                {
                    // 플랫폼 계층이 스스로 정의한 인터페이스만 본다(IDisposable 등 BCL 인터페이스 제외).
                    if (iface.Namespace != typeof(IPlatformWindowService).Namespace) continue;
                    audited++;
                    if (decoratorInterfaces.Contains(iface)) continue;
                    if (DeliberatelyConsumedNotForwarded.ContainsKey(iface.Name)) continue;

                    violations.Add(
                        $"{serviceType.Name}이(가) {iface.Name}을(를) 구현하는데 " +
                        $"{decorator.Name}이(가) 통과시키지 않습니다.\n" +
                        $"    -> 소비자의 `PlatformService as {iface.Name}` 캐스팅이 **항상 null**이 되어 " +
                        "그 기능이 조용히 죽습니다(위 클래스 문서의 3연속 사고 참고).\n" +
                        $"    -> 고치는 법: {decorator.Name}의 implements 목록에 {iface.Name}을 추가하고 " +
                        "`_inner as ...` 위임 필드를 두어 그대로 통과시키세요. " +
                        "정말로 통과가 아니라 '소비'가 맞다면 DeliberatelyConsumedNotForwarded에 이유와 함께 등록하세요.");
                }
            }

            Assert.Greater(audited, 0,
                $"{LogPrefix} 감사한 인터페이스가 0개입니다 — 리플렉션 수집이 깨졌다는 뜻이라 " +
                "이 테스트가 조용히 무의미해지지 않도록 실패로 알립니다.");

            Assert.IsTrue(violations.Count == 0,
                $"{LogPrefix} 데코레이터가 감추고 있는 인터페이스가 있습니다:\n\n" + string.Join("\n\n", violations));
        }

        [Test]
        public void 열거_비용_계측_인터페이스가_실제로_통과된다()
        {
            // 위 일반 규칙과 별개로, 2026-09-01에 실제로 죽어 있던 그 인터페이스를 이름으로 한 번 더 못 박는다.
            // (일반 규칙은 "구현체가 그 인터페이스를 계속 갖고 있을 때"만 유효하므로, 구현체 쪽에서
            //  사라지면 규칙이 조용히 통과해 버린다. 이 테스트는 그 경우에도 실패한다.)
            Assert.IsTrue(typeof(IWindowEnumerationCostSource)
                    .IsAssignableFrom(typeof(FallbackPlatformWindowService)),
                $"{LogPrefix} FallbackPlatformWindowService가 IWindowEnumerationCostSource를 통과시키지 " +
                "않으면 [발판열거]/[스톨귀인] 로그가 다시 `전체 창 -1개, 정밀검사 -1회`로 찍힙니다 — " +
                "그러면 '창이 800개일 때 열거가 비싸지는가'를 원격에서 영영 확인할 수 없습니다.");
        }

        [Test]
        public void 미지원일_때는_0이_아니라_음수로_보고한다()
        {
            // NullPlatformWindowService는 계측을 구현하지 않는다 -> 데코레이터는 -1을 그대로 내보내야 한다.
            // "모르는 값"을 0으로 위장하면 '0개 열거'와 '미지원'이 섞여 원격 진단이 틀린 결론에 도달한다
            // (IWindowEnumerationCostSource 문서의 명시적 계약).
            var service = new FallbackPlatformWindowService(new NullPlatformWindowService(), null);
            var cost = (IWindowEnumerationCostSource)service;

            Assert.AreEqual(-1, cost.LastEnumeratedWindowCount,
                $"{LogPrefix} 미지원 플랫폼에서 0을 보고하면 '창이 0개였다'로 읽힙니다.");
            Assert.AreEqual(-1, cost.LastDwmProbeCount,
                $"{LogPrefix} 미지원 플랫폼에서 0을 보고하면 'DWM 호출이 0회였다'로 읽혀 " +
                "후보가 거짓으로 무죄방면됩니다.");
        }
    }
}
