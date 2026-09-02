using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

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
    ///
    /// ============================================================================
    /// ★★ 2026-09-02 — 네 번째가 실제로 났고, <b>이 감사는 4시간 반 동안 초록이었다</b>
    /// ============================================================================
    /// <c>Win32WindowService</c>가 <c>IReservedTopBarService</c>를 달았는데(04:52 커밋) 데코레이터가
    /// 통과시키지 않았다. 그런데 러너 이력은 이렇다:
    /// <code>
    ///   05:15 · 05:26 · 05:31 · 05:52 · 06:01 · 06:05 · 06:14 · 06:22 · 07:22   → Passed
    ///   09:28 · 09:29 · 09:31                                                     → Failed
    /// </code>
    /// 코드는 그 사이 한 줄도 안 바뀌었다. 바뀐 것은 <b>에디터의 활성 빌드 타깃</b>이다
    /// (Bee 응답 파일 실측: 07:32 <c>UNITY_STANDALONE_OSX</c> → 07:54 이후 <c>UNITY_STANDALONE_WIN</c>).
    ///
    /// <para><b>즉 위 리플렉션 감사는 활성 빌드 타깃에 종속이다.</b> <c>Win32WindowService.cs</c>는
    /// 파일 전체가 <c>#if UNITY_STANDALONE_WIN</c> 안이라 macOS 타깃에서는 <b>타입이 존재하지 않고</b>,
    /// 리플렉션은 없는 타입의 인터페이스를 셀 수 없다. 이 저장소의 평소 상태가 macOS 타깃이므로
    /// (CLAUDE.md: "Windows 전용 파일은 이 개발 머신에서 한 번도 컴파일되지 않는다") 이 감사는
    /// <b>Windows 쪽 절반을 구조적으로 못 본다</b> — 하필 갭이 실제로 쌓이는 쪽이다.</para>
    ///
    /// <para>그래서 <see cref="빌드타깃과_무관하게_소스에서도_통과_누락을_감사한다"/>를 쌍으로 둔다.
    /// 소스 텍스트를 읽으므로 <c>#if</c>와 무관하다(<c>PlatformParityAuditTests</c>와 같은 방식,
    /// 같은 이유). 리플렉션판도 그대로 남긴다 — 소스 스캔이 못 보는 것(인터페이스 상속으로 전파된
    /// 구현, 부분 클래스, 생성된 코드)을 그쪽이 본다. 둘은 서로의 사각을 덮는다.</para>
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

        /// <summary>
        /// 리플렉션판. <b>활성 빌드 타깃에 컴파일된 서비스만</b> 본다 — 그 한계와 실제 사고는 클래스
        /// 문서 참고. 타깃 무관 감사는 <see cref="빌드타깃과_무관하게_소스에서도_통과_누락을_감사한다"/>다.
        /// </summary>
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

        // ============================================================================
        // ★★ 2026-09-02 (debugger) — 빌드 타깃 무관 소스 감사
        // ============================================================================
        // 위 리플렉션 감사가 Windows 절반을 못 본다는 것이 실측으로 드러났다(클래스 문서의 러너 이력).
        // 여기는 소스 텍스트만 읽으므로 #if 와 활성 빌드 타깃에 영향받지 않는다.

        private static string PlatformRoot =>
            Path.Combine(Application.dataPath, "_Project", "Scripts", "Platform");

        /// <summary>줄 주석(<c>//</c>, <c>///</c>)과 블록 주석 본문 줄을 지운다 —
        /// 이 저장소의 거짓 초록 두 건이 모두 "결함을 설명하는 주석을 구현으로 오인"한 형태였다.</summary>
        private static string StripComments(string source)
        {
            var sb = new StringBuilder(source.Length);
            foreach (string line in source.Split('\n'))
            {
                string t = line.TrimStart();
                if (t.StartsWith("//", StringComparison.Ordinal)) continue;
                if (t.StartsWith("*", StringComparison.Ordinal)) continue;
                if (t.StartsWith("/*", StringComparison.Ordinal)) continue;
                sb.Append(line).Append('\n');
            }
            return sb.ToString();
        }

        // `class X : A, B, C {` 의 **기반 목록만** 잡는다(여는 중괄호 앞까지). 제네릭/줄바꿈 허용.
        private static readonly Regex ClassWithBases =
            new Regex(@"\bclass\s+(?<name>\w+)\s*:\s*(?<bases>[^{;]+)\{", RegexOptions.Singleline);

        private static readonly Regex InterfaceDecl =
            new Regex(@"\binterface\s+(?<name>I\w+)", RegexOptions.Singleline);

        private static IEnumerable<string> PlatformSourceFiles()
            => Directory.EnumerateFiles(PlatformRoot, "*.cs", SearchOption.AllDirectories);

        private static List<string> ParseBases(string bases)
            => bases.Split(',')
                    .Select(b => b.Trim())
                    .Where(b => b.Length > 0)
                    .ToList();

        /// <summary>Platform/ 아래 소스에서 "기반 목록에 IPlatformWindowService가 있는 클래스"를 모은다.</summary>
        private static Dictionary<string, List<string>> ScanPlatformServiceBases(out List<string> allInterfaceNames)
        {
            var services = new Dictionary<string, List<string>>();
            var ifaces = new List<string>();
            foreach (string file in PlatformSourceFiles())
            {
                string src = StripComments(File.ReadAllText(file));
                foreach (Match m in InterfaceDecl.Matches(src)) ifaces.Add(m.Groups["name"].Value);
                foreach (Match m in ClassWithBases.Matches(src))
                {
                    List<string> bases = ParseBases(m.Groups["bases"].Value);
                    if (!bases.Contains(nameof(IPlatformWindowService))) continue;
                    services[m.Groups["name"].Value] = bases;
                }
            }
            allInterfaceNames = ifaces.Distinct().ToList();
            return services;
        }

        [Test]
        public void 빌드타깃과_무관하게_소스에서도_통과_누락을_감사한다()
        {
            Dictionary<string, List<string>> services = ScanPlatformServiceBases(out List<string> platformInterfaces);

            // ---- (0) 파서 자체가 죽지 않았는지 먼저 못 박는다(조용히 0건 감사 = 거짓 초록) ----
            Assert.IsTrue(services.ContainsKey(nameof(FallbackPlatformWindowService)),
                $"{LogPrefix} 소스 스캔이 데코레이터조차 못 찾았습니다 — 파서가 깨졌다는 뜻입니다.");
            // ★ 이 두 이름은 문자열일 수밖에 없다. 반대편 플랫폼 타깃에서는 타입이 아예 없어
            //   nameof()가 컴파일되지 않는다 — 바로 그것이 이 소스 감사가 존재하는 이유다.
            Assert.IsTrue(services.ContainsKey("Win32WindowService"),
                $"{LogPrefix} 소스 스캔이 Win32WindowService를 못 찾았습니다. macOS 타깃에서는 " +
                "이 타입이 컴파일되지 않으므로, 이 검사가 못 찾으면 Windows 쪽 통과 누락은 " +
                "**어떤 검사로도** 드러나지 않습니다(2026-09-02에 실제로 4시간 반 동안 그랬습니다).");
            Assert.IsTrue(services.ContainsKey("MacWindowService"),
                $"{LogPrefix} 소스 스캔이 MacWindowService를 못 찾았습니다 — 활성 빌드 타깃이 " +
                "Windows일 때 macOS 쪽이 같은 방식으로 안 보이게 됩니다(대칭 사각지대).");
            Assert.Greater(platformInterfaces.Count, 3,
                $"{LogPrefix} Platform/ 에서 수집한 인터페이스가 너무 적습니다 — 수집이 깨졌습니다.");
            Assert.Contains(nameof(IReservedTopBarService), platformInterfaces,
                $"{LogPrefix} 인터페이스 수집이 Platform/ 중립 위치의 계약을 놓쳤습니다.");

            var decoratorBases = new HashSet<string>(services[nameof(FallbackPlatformWindowService)]);
            var known = new HashSet<string>(platformInterfaces);
            var violations = new List<string>();

            foreach (KeyValuePair<string, List<string>> svc in services.OrderBy(kv => kv.Key))
            {
                if (svc.Key == nameof(FallbackPlatformWindowService)) continue;
                foreach (string iface in svc.Value)
                {
                    if (iface == nameof(IPlatformWindowService)) continue;
                    if (!known.Contains(iface)) continue;          // Platform 계층이 정의한 것만 본다.
                    if (decoratorBases.Contains(iface)) continue;
                    if (DeliberatelyConsumedNotForwarded.ContainsKey(iface)) continue;

                    violations.Add($"{svc.Key} -> {iface} (데코레이터 기반 목록에 없음)");
                }
            }

            Assert.IsTrue(violations.Count == 0,
                $"{LogPrefix} 소스 감사: 데코레이터가 통과시키지 않는 선택적 인터페이스가 있습니다:\n  "
                + string.Join("\n  ", violations)
                + "\n\n  -> FallbackPlatformWindowService의 기반 목록에 추가하고 `_inner as ...` 위임을 두세요.\n"
                + "  -> 정말 '소비'가 맞다면 DeliberatelyConsumedNotForwarded에 이유와 함께 등록하세요.");
        }

        /// <summary>
        /// ★ 이 테스트가 <b>리플렉션 감사의 사각지대를 러너에 보이게</b> 한다. 2026-09-02에 그 사각지대는
        /// "추정"이 아니라 실측이었다 — 코드가 한 줄도 안 바뀐 채로 감사가 초록에서 빨강으로 넘어갔고,
        /// 바뀐 것은 에디터의 활성 빌드 타깃뿐이었다(클래스 문서의 러너 이력).
        ///
        /// <para>불변식: <c>Win32WindowService</c>와 <c>MacWindowService</c>는 각각
        /// <c>#if UNITY_STANDALONE_WIN</c> / <c>_OSX</c> 안이라 <b>어떤 타깃에서도 둘 다 컴파일되지는
        /// 않는다</b>. 따라서 소스 스캔이 보는 집합은 리플렉션이 보는 집합의 <b>진부분집합이 아닌
        /// 진상위집합</b>이어야 한다. 이 관계가 깨지면(예: 누가 <c>#if</c>를 걷어냈다면) 이 테스트가
        /// 실패해서 "이제 리플렉션만으로 충분하다"는 사실을 알려 준다 — 그때 소스 감사를 지워도 된다.</para>
        /// </summary>
        [Test]
        public void 리플렉션_감사는_활성_빌드타깃에_컴파일된_것만_본다()
        {
            Dictionary<string, List<string>> sourceServices = ScanPlatformServiceBases(out _);
            var sourceNames = new HashSet<string>(sourceServices.Keys);
            var reflectedNames = new HashSet<string>(RealPlatformServiceTypes().Select(t => t.Name))
            {
                nameof(FallbackPlatformWindowService), // 리플렉션 목록은 데코레이터 자신을 제외한다.
            };

            foreach (string reflected in reflectedNames)
            {
                Assert.IsTrue(sourceNames.Contains(reflected),
                    $"{LogPrefix} 리플렉션에는 보이는데 소스 스캔에는 없는 서비스가 있습니다: {reflected}. " +
                    "소스 감사가 그만큼 좁아졌다는 뜻이라 통과 누락을 놓칠 수 있습니다(파서 확인 필요).");
            }

            List<string> blind = sourceNames.Except(reflectedNames).OrderBy(n => n).ToList();
            Debug.Log($"{LogPrefix} 활성 빌드 타깃에서 리플렉션이 못 보는 플랫폼 서비스 " +
                      $"{blind.Count}종: {(blind.Count == 0 ? "(없음)" : string.Join(", ", blind))}. " +
                      $"소스 스캔 {sourceNames.Count}종 / 리플렉션 {reflectedNames.Count}종.");

            Assert.Greater(blind.Count, 0,
                $"{LogPrefix} 리플렉션이 모든 플랫폼 서비스를 보고 있습니다 — 이 저장소의 전제가 " +
                "바뀌었다는 뜻입니다(Win32WindowService/MacWindowService의 #if 가드가 사라졌거나 " +
                "서비스가 통합됨). 그렇다면 소스 감사는 더 이상 필요 없으니 함께 정리하세요. " +
                "반대로 이 단언이 그냥 거슬려서 지우면, 러너는 다시 절반만 보면서 초록이 됩니다.");
        }

        [Test]
        public void 소스_감사는_주석에_적힌_인터페이스_이름을_구현으로_세지_않는다()
        {
            // 이 저장소의 거짓 초록 두 건이 모두 이 모양이었다(PlatformParityAuditTests 문서).
            // 파서에 미끼를 직접 먹여 본다 — 프로덕션 파일을 건드리지 않고 파서만 검증한다.
            string decoy =
                "namespace X {\n" +
                "    /// <summary>이 클래스는 IReservedTopBarService를 곧 달 예정이다.</summary>\n" +
                "    // IReservedTopBarService 구현이 이미 같은 호출에서 rcWork를 읽고 있다.\n" +
                "    public sealed class Decoy : IPlatformWindowService\n" +
                "    {\n" +
                "        private void Log() { Debug.Log(\"IReservedTopBarService 미구현\"); }\n" +
                "    }\n" +
                "}\n";

            string stripped = StripComments(decoy);
            Match m = ClassWithBases.Match(stripped);
            Assert.IsTrue(m.Success, $"{LogPrefix} 미끼에서 클래스 선언을 못 찾았습니다 — 파서가 깨졌습니다.");
            List<string> bases = ParseBases(m.Groups["bases"].Value);

            CollectionAssert.DoesNotContain(bases, nameof(IReservedTopBarService),
                $"{LogPrefix} 주석/본문에 있는 인터페이스 이름이 '구현했다'로 집계됐습니다 — " +
                "이 감사는 그 순간 영구 거짓 초록이 됩니다.");
            CollectionAssert.Contains(bases, nameof(IPlatformWindowService),
                $"{LogPrefix} 반대로 진짜 기반 목록을 못 읽으면 감사 대상이 통째로 비어 버립니다.");
        }

        // ============================================================================
        // ★ 2026-09-02 (debugger) — 인과 확정용 실측 테스트
        // ============================================================================
        // 위 감사가 빨개졌을 때 함께 보고된 서술은 "소비 측 `as` 캐스팅이 항상 null이라 Windows
        // 상단 인셋이 조용히 0"이었다. 그런데 그 문장은 감사 실패 메시지의 **템플릿 문구**이지
        // 실측이 아니다. 상단 인셋의 실제 소비 경로는 ReservedTopBarProbe 한 곳이고, 그 안은
        // `as`가 아니라 `decorator.Inner`로 **데코레이터를 벗긴 뒤** 캐스팅한다.
        // 이 테스트가 그 경로를 실제로 통과시켜 "인셋이 도달하는가"를 값으로 못 박는다 —
        // 감사(구조)와 배선(동작)은 다른 질문이고, 둘 다 잠가야 한다.
        private sealed class StubTopBarInner : IPlatformWindowService, IReservedTopBarService
        {
            private readonly float _inset;
            public StubTopBarInner(float inset) { _inset = inset; }
            public IReadOnlyList<PlatformFoothold> EnumerateFootholds() => new List<PlatformFoothold>();
            public bool CreateOverlayWindow() => true;
            public void SetClickThrough(bool enabled) { }
            public void SetAlwaysOnTop(bool enabled) { }
            public bool IsFullscreenAppActive() => false;
            public bool TryGetReservedTopInsetPoints(out float insetPoints)
            {
                insetPoints = _inset;
                return _inset > 0f;
            }
        }

        [SetUp]
        public void 프로브_정적상태_초기화() => ReservedTopBarProbe.ResetForTests();

        [TearDown]
        public void 프로브_정적상태_정리() => ReservedTopBarProbe.ResetForTests();

        [Test]
        public void 상단_인셋은_데코레이터를_거쳐도_소비_측에_도달한다()
        {
            const float 실측_두께 = 33f;
            var decorated = new FallbackPlatformWindowService(new StubTopBarInner(실측_두께), null);

            float 도달값 = ReservedTopBarProbe.TopInsetPoints(decorated);

            Assert.AreEqual(실측_두께, 도달값, 0.001f,
                $"{LogPrefix} 데코레이터로 감싼 뒤 상단 예약 띠 두께가 소비 측(팝오버·정보창·설정창·톱니)에 " +
                "도달하지 않습니다 — 상단 도킹 작업표시줄/메뉴바를 쓰는 화면에서 그 띠를 덮습니다.");
        }

        [Test]
        public void 상단_인셋이_없으면_0으로_접는다()
        {
            var decorated = new FallbackPlatformWindowService(new StubTopBarInner(0f), null);

            Assert.AreEqual(0f, ReservedTopBarProbe.TopInsetPoints(decorated), 0.001f,
                $"{LogPrefix} '상단 예약 띠 없음'을 짐작값으로 메우면 멀쩡한 화면 위쪽을 낭비합니다 " +
                "(IReservedTopBarService 계약: false는 '추정하라'가 아니라 '없다'다).");
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
