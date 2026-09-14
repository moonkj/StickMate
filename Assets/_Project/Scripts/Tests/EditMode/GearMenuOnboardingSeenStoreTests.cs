using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// 톱니 부채꼴 「최초 1회 안내 봤음」 저장소 경계 — 하위 호환 · 계수 생존 · 우회 금지 (2026-09-14, coder).
    ///
    /// <para><b>왜 있는가.</b> PlayMode 스위트는 이 비트를 메모리 저장소로 돌리고, 끝에서 실제 저장소 구현의
    /// 계수 증가분 0을 단언한다(Tests/PlayMode/GlobalPlayModeTestIsolation). 그 0이 뜻을 가지려면 세 가지가
    /// 따로 참이어야 하고, 이 파일이 그 셋을 잠근다.</para>
    /// <list type="number">
    ///   <item><b>계수가 실제 경로에 붙어 있다</b> — 계수 자기 검증(실행, <b>안 봤음·봤음 두 경로</b>) + 계수가 모든 분기보다
    ///     먼저라는 구조 감사. 한 번도 오를 수 없는 계수의 0은 무효다.</item>
    ///   <item><b>계수를 거치지 않는 길이 없다</b> — 소스 감사 + 출하본 싱크의 가시성. 위젯이 경계를 우회해 설정 저장소를
    ///     직접 부르면 런타임 계수는 <b>조용히 0</b>으로 남는다. 실행으로는 못 잡는 형태라 텍스트와 타입으로 잡는다.</item>
    ///   <item><b>실제 저장소의 키·값 형식이 출하본과 같다</b> — 하위 호환. 기대값은 프로덕션 상수가 아니라
    ///     <b>디스크의 골든</b>(Golden/GearMenuOnboardingSeenPrefsGolden.txt)에서 온다.</item>
    /// </list>
    ///
    /// <para><b>2026-09-14 verify-change 1단계 보강</b>: B1(내부 가시성 싱크로 우회 — 감사 5/5 초록이었다) ·
    /// B7(「봤음이면 계수 전에 반환」 — 자기 검증이 안 봤음 경로만 쟀다) · B11(알약이 없으면 읽지도 않는다) ·
    /// 테스트용 주입 API의 프로덕션 호출 금지.</para>
    ///
    /// <para><b>2026-09-14 verify-change 2단계 보강</b>: S1(보관소 타입 <c>using</c> 별칭 뒤 테스트용 API 호출 — 별칭 선언 자체를
    /// 금지) · S2(null 가드 앞 <c>OnboardingHintSeen</c> 같은 위젯 멤버를 통한 간접 읽기 — 경계를 참조하는 위젯 멤버 이름을
    /// 소스에서 도출해 읽기로 센다).</para>
    ///
    /// <para><b>★ 이 파일의 감사가 잡지 못하는 것(한계 — 코드로 쫓지 않기로 리더 판정, 2026-09-14)</b>. 전부 글자 스캐너의
    /// 원리 한계이거나 일부러 꼬아야 생기는 형태다. 여기 적는 이유는 이 목록이 없으면 다음 사람이 「잠갔다」를 전부로 읽기 때문이다.</para>
    /// <list type="bullet">
    ///   <item><b>S3 — 출하본 싱크일 때만 계수를 건너뛰는 악성 형태</b>(예: 계수 증가량을 싱크 타입으로 갈라 0을 더함). 자기 검증은
    ///     기록용 싱크로만 재므로 출하본 경로의 차이를 못 보고, 계수 선행 감사는 계수 식별자가 먼저 오면 통과한다.</item>
    ///   <item><b>S4 — 계산된 이름 리플렉션</b>(이름을 이어 붙여 메서드·타입을 찾음). 이름이 코드 토큰에도 평문 리터럴(통째)에도 없다 —
    ///     <c>SourceTextScanner</c> 클래스 문서의 원리 한계와 같다.</item>
    ///   <item><b>간접 읽기는 한 단계까지</b>(S2 보강). 경계를 읽는 위젯 멤버를 부르는 또 다른 멤버를 거치면 못 본다.</item>
    ///   <item><b>거짓 빨강 F1</b> — B11 가드를 중괄호로 쓰면(<c>if (…) { return; }</c>) 가드를 못 찾아 빨개진다.</item>
    ///   <item><b>거짓 빨강 F2</b> — 계수를 <c>try/finally</c>로 올리면 계수 선행 감사가 <c>return</c>이 먼저라고 보고 빨개진다.</item>
    ///   <item><b>거짓 빨강 F3</b> — 프로덕션 평문 로그 문자열에 실제 저장소 타입 이름을 적으면 B8 타입 감사가 누출로 센다.</item>
    ///   <item><b>거짓 빨강 F4</b> — 프로덕션에서 <c>nameof(실제 저장소 타입)</c>·<c>nameof(OnboardingHintSeen)</c>를 쓰면 식별자로 세어
    ///     B8 감사·B11 간접 읽기가 빨개진다.</item>
    /// </list>
    /// </summary>
    public sealed class GearMenuOnboardingSeenStoreTests
    {
        private const string LogPrefix = "[온보딩봤음저장소]";

        private const string GoldenPath =
            "Assets/_Project/Scripts/Tests/EditMode/Golden/GearMenuOnboardingSeenPrefsGolden.txt";

        private static string PrefsTypeName => nameof(PlayerPrefs);

        private static string RealStoreFileName => nameof(PlayerPrefsGearMenuOnboardingSeenStore) + ".cs";

        private static string WidgetFileName => nameof(GearRadialMenuWidget) + ".cs";

        // ==================================================================
        // 도구
        // ==================================================================

        private sealed class Golden
        {
            public string Key;
            public int Seen;
            public int Unset;
        }

        /// <summary>골든을 읽는다. 칸이 비었거나 형식이 틀리면 <b>판독 무효</b>로 멈춘다 — 빈 기대값과의 비교는 아무것도 재지 않는다.</summary>
        private static Golden ReadGolden()
        {
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, GoldenPath);
            Assert.IsTrue(File.Exists(path), $"{LogPrefix} 하위 호환 골든이 없습니다: {GoldenPath}");

            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string raw in File.ReadAllLines(path))
            {
                if (raw.Length == 0 || raw[0] == '#') continue;
                int eq = raw.IndexOf('=');
                Assert.Greater(eq, 0, $"{LogPrefix} 골든 줄 형식(이름=값)이 아닙니다: \"{raw}\"");
                fields[raw.Substring(0, eq)] = raw.Substring(eq + 1);
            }

            foreach (string name in new[] { "key", "type", "seen", "unset" })
            {
                Assert.IsTrue(fields.TryGetValue(name, out string value) && value.Length > 0,
                    $"{LogPrefix} ★ 골든 판독 무효 — '{name}' 칸이 없거나 비었습니다. 아래 비교는 전부 폐기해야 합니다.");
            }
            Assert.AreEqual("int", fields["type"],
                $"{LogPrefix} ★ 골든 판독 무효 — 값 형식이 이 테스트가 아는 정수가 아닙니다('{fields["type"]}').");
            Assert.IsTrue(int.TryParse(fields["seen"], NumberStyles.Integer, CultureInfo.InvariantCulture, out int seen),
                $"{LogPrefix} ★ 골든 판독 무효 — seen이 정수가 아닙니다('{fields["seen"]}').");
            Assert.IsTrue(int.TryParse(fields["unset"], NumberStyles.Integer, CultureInfo.InvariantCulture, out int unset),
                $"{LogPrefix} ★ 골든 판독 무효 — unset이 정수가 아닙니다('{fields["unset"]}').");
            Assert.AreNotEqual(seen, unset, $"{LogPrefix} ★ 골든 판독 무효 — 봤음과 안 봤음이 같은 값입니다.");

            return new Golden { Key = fields["key"], Seen = seen, Unset = unset };
        }

        /// <summary>설정 저장소 흉내. 실제 PlayerPrefs처럼 키별로 값을 들고, 불린 순서를 남긴다.</summary>
        private sealed class RecordingSink : PlayerPrefsGearMenuOnboardingSeenStore.IIntPreferenceSink
        {
            public readonly Dictionary<string, int> Values = new Dictionary<string, int>(StringComparer.Ordinal);
            public readonly List<string> Calls = new List<string>();

            public int GetInt(string key, int defaultValue)
            {
                Calls.Add(GetCall(key, defaultValue));
                return Values.TryGetValue(key, out int value) ? value : defaultValue;
            }

            public void SetInt(string key, int value)
            {
                Calls.Add(SetCall(key, value));
                Values[key] = value;
            }

            public void Save() => Calls.Add(SaveCall);

            public static string GetCall(string key, int defaultValue) => $"{nameof(GetInt)}({key}, {defaultValue})";
            public static string SetCall(string key, int value) => $"{nameof(SetInt)}({key}, {value})";
            public static string SaveCall => nameof(Save);
        }

        /// <summary>출하본 싱크 타입 — 이름을 문자열로 베끼지 않고 리플렉션으로 찾는다(그 타입은 <c>private</c>라 <c>nameof</c>로 닿지 않는다).</summary>
        private static Type RealSinkType()
        {
            var found = new List<Type>();
            foreach (Type t in typeof(PlayerPrefsGearMenuOnboardingSeenStore).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!t.IsInterface && typeof(PlayerPrefsGearMenuOnboardingSeenStore.IIntPreferenceSink).IsAssignableFrom(t))
                    found.Add(t);
            }
            Assert.AreEqual(1, found.Count,
                $"{LogPrefix} ★ 실제 저장소 안의 싱크 구현이 정확히 하나가 아닙니다({found.Count}개) — 아래 판정은 무효입니다.");
            return found[0];
        }

        /// <summary>프로덕션 파일 중 이름이 정확히 같은 것 하나를 찾는다(둘 이상이거나 없으면 판정 무효).</summary>
        private static string FindProductionFile(string fileName)
        {
            var hits = new List<string>();
            foreach (string file in SourceTextScanner.ProductionSourceFilesUnderAssets())
                if (string.Equals(Path.GetFileName(file), fileName, StringComparison.Ordinal)) hits.Add(file);
            Assert.AreEqual(1, hits.Count, $"{LogPrefix} ★ {fileName}을 정확히 하나 찾지 못했습니다({hits.Count}개) — 판정 무효.");
            return hits[0];
        }

        /// <summary>지운 코드에서 <paramref name="openBrace"/> 위치의 '{'부터 짝이 맞는 '}'까지. 못 찾으면 null.</summary>
        private static string BlockFrom(string code, int openBrace)
        {
            if (openBrace < 0 || openBrace >= code.Length || code[openBrace] != '{') return null;
            int depth = 0;
            for (int k = openBrace; k < code.Length; k++)
            {
                if (code[k] == '{') depth++;
                else if (code[k] == '}' && --depth == 0) return code.Substring(openBrace, k + 1 - openBrace);
            }
            return null;
        }

        // ==================================================================
        // 1. 하위 호환
        // ==================================================================

        [Test]
        public void 하위호환_실제_저장소의_키_이름과_값_형식은_출하본_골든과_한_글자도_다르지_않다()
        {
            Golden golden = ReadGolden();

            const string why = " — 2026-09-01부터 출하된 빌드가 사용자 기계에 이미 이 이름·형식으로 값을 남겼습니다. " +
                "바뀌면 이미 안내를 본 사용자에게 안내가 다시 뜨고(Windows 값 이름의 _h 꼬리도 키에서 파생되어 함께 바뀝니다), " +
                "촬영 절차(docs/marketing/CAPTURE_PROTOCOL.md)가 키를 못 찾습니다. 골든을 고쳐 초록을 만들지 마십시오 — " +
                "옛 키를 옮기는 이전 코드가 먼저이고 리더 판정 사안입니다.";

            Assert.AreEqual(golden.Key, PlayerPrefsGearMenuOnboardingSeenStore.Key,
                $"{LogPrefix} 실제 저장소의 키 이름이 출하본과 다릅니다" + why);
            Assert.AreEqual(golden.Seen, PlayerPrefsGearMenuOnboardingSeenStore.SeenValue,
                $"{LogPrefix} 「봤음」 값이 출하본과 다릅니다" + why);
            Assert.AreEqual(golden.Unset, PlayerPrefsGearMenuOnboardingSeenStore.UnsetValue,
                $"{LogPrefix} 키가 없을 때의 값이 출하본과 다릅니다" + why);

            // 상수만 맞고 실제 경로가 다른 키·값을 쓰면 위 셋은 초록이다 — 코드를 태워 싱크에 도착한 것을 본다.
            // ① 처음 쓰는 기계.
            var fresh = new RecordingSink();
            var store = new PlayerPrefsGearMenuOnboardingSeenStore(fresh);
            Assert.IsFalse(store.IsSeen, $"{LogPrefix} 아무것도 적히지 않은 기계에서 「봤음」으로 읽혔습니다.");
            store.MarkSeen();
            CollectionAssert.AreEqual(
                new[]
                {
                    RecordingSink.GetCall(golden.Key, golden.Unset),
                    RecordingSink.SetCall(golden.Key, golden.Seen),
                    RecordingSink.SaveCall,
                },
                fresh.Calls,
                $"{LogPrefix} 실제 경로가 설정 저장소에 보낸 호출이 출하본 형식(정수 읽기 → 정수 쓰기 → 즉시 저장)과 " +
                $"다릅니다. 실제: [{string.Join(" · ", fresh.Calls)}]" + why);
            Assert.IsTrue(store.IsSeen, $"{LogPrefix} 「봤음」을 적은 뒤에도 「봤음」으로 읽히지 않습니다.");

            // ② 출하본이 이미 적어 둔 기계 — 키·값을 상수가 아니라 골든에서 심는다.
            var shipped = new RecordingSink();
            shipped.Values[golden.Key] = golden.Seen;
            var onShippedMachine = new PlayerPrefsGearMenuOnboardingSeenStore(shipped);
            Assert.IsTrue(onShippedMachine.IsSeen,
                $"{LogPrefix} 출하본이 적어 둔 값을 「봤음」으로 읽지 못합니다 — 이미 본 사용자에게 안내가 다시 뜹니다" + why);
            CollectionAssert.DoesNotContain(shipped.Calls, RecordingSink.SetCall(golden.Key, golden.Seen),
                $"{LogPrefix} 읽기만 했는데 쓰기가 일어났습니다.");
        }

        // ==================================================================
        // 2. 계수 자기 검증
        // ==================================================================

        [Test]
        public void 계수_자기검증_실제_저장소_구현의_읽기와_쓰기에서_정확히_한_번씩_오르고_메모리_저장소는_세지_않는다()
        {
            // ---- (가) 안 봤음 경로 ----
            int reads0 = PlayerPrefsGearMenuOnboardingSeenStore.ReadCount;
            int writes0 = PlayerPrefsGearMenuOnboardingSeenStore.WriteCount;

            var sink = new RecordingSink();
            var store = new PlayerPrefsGearMenuOnboardingSeenStore(sink);
            Assert.AreEqual(0, PlayerPrefsGearMenuOnboardingSeenStore.ReadCount - reads0,
                $"{LogPrefix} 만들기만 했는데 읽기 계수가 올랐습니다.");

            bool unseen = store.IsSeen;
            Assert.IsFalse(unseen, $"{LogPrefix} ★ 준비물 무효 — 빈 싱크에서 「봤음」으로 읽혀 안 봤음 경로를 태우지 못했습니다.");
            Assert.AreEqual(1, PlayerPrefsGearMenuOnboardingSeenStore.ReadCount - reads0,
                $"{LogPrefix} ★ 안 봤음 경로에서 읽기 계수가 오르지 않습니다 — PlayMode 스위트 끝의 「실제 저장소 읽기 0」이 무효가 됩니다.");
            Assert.AreEqual(0, PlayerPrefsGearMenuOnboardingSeenStore.WriteCount - writes0,
                $"{LogPrefix} 읽기만 했는데 쓰기 계수가 올랐습니다.");

            store.MarkSeen();
            Assert.AreEqual(1, PlayerPrefsGearMenuOnboardingSeenStore.WriteCount - writes0,
                $"{LogPrefix} ★ 쓰기 계수가 실제 쓰기 경로에서 오르지 않습니다 — PlayMode 스위트 끝의 「실제 저장소 쓰기 0」이 무효가 됩니다.");
            Assert.AreEqual(1, sink.Calls.FindAll(c => c == RecordingSink.SaveCall).Count,
                $"{LogPrefix} 계수는 올랐는데 그 경로가 설정 저장소의 저장까지 가지 않습니다.");

            // ---- (나) 이미 봤음 경로 (verify-change B7) ----
            // (가)만 재면 「봤음이면 계수 전에 반환」 형태가 통과하고, 이미 1이 적힌 기계에서 주입 누락이 읽기 0으로 숨는다.
            int reads1 = PlayerPrefsGearMenuOnboardingSeenStore.ReadCount;
            int writes1 = PlayerPrefsGearMenuOnboardingSeenStore.WriteCount;
            var seenSink = new RecordingSink();
            seenSink.Values[PlayerPrefsGearMenuOnboardingSeenStore.Key] = PlayerPrefsGearMenuOnboardingSeenStore.SeenValue;
            var seenStore = new PlayerPrefsGearMenuOnboardingSeenStore(seenSink);

            bool seen = seenStore.IsSeen;
            Assert.IsTrue(seen, $"{LogPrefix} ★ 준비물 무효 — 봤음 값을 심은 싱크에서 「봤음」으로 읽히지 않아 이미 봤음 경로를 태우지 못했습니다.");
            Assert.AreEqual(1, PlayerPrefsGearMenuOnboardingSeenStore.ReadCount - reads1,
                $"{LogPrefix} ★ 이미 봤음 경로에서 읽기 계수가 오르지 않습니다 — 1이 적힌 기계에서 주입 누락이 읽기 0으로 숨습니다.");
            seenStore.MarkSeen();
            Assert.AreEqual(1, PlayerPrefsGearMenuOnboardingSeenStore.WriteCount - writes1,
                $"{LogPrefix} ★ 이미 봤음 경로에서 쓰기 계수가 오르지 않습니다.");
            CollectionAssert.Contains(seenSink.Calls,
                RecordingSink.SetCall(PlayerPrefsGearMenuOnboardingSeenStore.Key, PlayerPrefsGearMenuOnboardingSeenStore.SeenValue),
                $"{LogPrefix} 이미 봤음 경로에서 계수는 올랐는데 설정 저장소 쓰기까지 가지 않습니다.");

            // ---- 음성 대조 — 메모리 저장소는 실제 계수를 건드리지 않는다. 건드리면 스위트 끝 0 단언이 늘 빨갛다 ----
            int reads2 = PlayerPrefsGearMenuOnboardingSeenStore.ReadCount;
            int writes2 = PlayerPrefsGearMenuOnboardingSeenStore.WriteCount;
            var memory = new InMemoryGearMenuOnboardingSeenStore(seen: false);
            bool before = memory.IsSeen;
            memory.MarkSeen();
            bool after = memory.IsSeen;
            Assert.AreEqual(reads2, PlayerPrefsGearMenuOnboardingSeenStore.ReadCount,
                $"{LogPrefix} 메모리 저장소 읽기가 실제 저장소 계수를 올렸습니다.");
            Assert.AreEqual(writes2, PlayerPrefsGearMenuOnboardingSeenStore.WriteCount,
                $"{LogPrefix} 메모리 저장소 쓰기가 실제 저장소 계수를 올렸습니다.");
            Assert.IsFalse(before, $"{LogPrefix} 「안 봤음」으로 만든 메모리 저장소가 「봤음」으로 읽힙니다.");
            Assert.IsTrue(after, $"{LogPrefix} 메모리 저장소에 적은 「봤음」이 읽히지 않습니다.");
            Assert.AreEqual(2, memory.ReadCount, $"{LogPrefix} 메모리 저장소 읽기 계수가 읽은 횟수와 다릅니다.");
            Assert.AreEqual(1, memory.WriteCount, $"{LogPrefix} 메모리 저장소 쓰기 계수가 쓴 횟수와 다릅니다.");
        }

        // ==================================================================
        // 3. 주입 자리
        // ==================================================================

        [Test]
        public void 주입이_없으면_실제_저장소다_메모리가_기본이면_출하본_사용자에게_안내가_매_실행_뜬다()
        {
            var mine = new InMemoryGearMenuOnboardingSeenStore(seen: false);
            IGearMenuOnboardingSeenStore previous = GearMenuOnboardingSeenStore.UseForTesting(mine);
            try
            {
                Assert.IsTrue(GearMenuOnboardingSeenStore.IsOverriddenForTesting, $"{LogPrefix} 주입이 걸리지 않았습니다.");
                Assert.AreSame(mine, GearMenuOnboardingSeenStore.Current, $"{LogPrefix} 주입한 저장소가 쓰이지 않습니다.");

                GearMenuOnboardingSeenStore.ResetForTesting();
                int reads0 = PlayerPrefsGearMenuOnboardingSeenStore.ReadCount;
                int writes0 = PlayerPrefsGearMenuOnboardingSeenStore.WriteCount;

                Assert.IsFalse(GearMenuOnboardingSeenStore.IsOverriddenForTesting, $"{LogPrefix} 주입이 걷히지 않았습니다.");
                Assert.IsInstanceOf<PlayerPrefsGearMenuOnboardingSeenStore>(GearMenuOnboardingSeenStore.Current,
                    $"{LogPrefix} 주입이 없을 때 실제 저장소가 아닙니다 — 출하본에서 「봤음」이 디스크에 남지 않아 " +
                    "안내가 매 실행 뜹니다(원칙 2).");
                Assert.AreEqual(reads0, PlayerPrefsGearMenuOnboardingSeenStore.ReadCount,
                    $"{LogPrefix} 기본 저장소를 얻기만 했는데 실제 설정 저장소를 읽었습니다.");
                Assert.AreEqual(writes0, PlayerPrefsGearMenuOnboardingSeenStore.WriteCount,
                    $"{LogPrefix} 기본 저장소를 얻기만 했는데 실제 설정 저장소에 썼습니다.");

                // null 주입은 거부된다 — 받아 주면 「주입했다」고 믿은 채 실제 저장소로 떨어진다.
                Assert.Throws<ArgumentNullException>(() => GearMenuOnboardingSeenStore.UseForTesting(null));
                Assert.IsFalse(GearMenuOnboardingSeenStore.IsOverriddenForTesting,
                    $"{LogPrefix} 거부된 null 주입이 주입 상태를 바꿨습니다.");

                // 되돌리기 왕복 — UseForTesting이 돌려준 직전 주입으로 정확히 돌아간다.
                IGearMenuOnboardingSeenStore beforeMine = GearMenuOnboardingSeenStore.UseForTesting(mine);
                Assert.IsNull(beforeMine, $"{LogPrefix} 주입이 없던 자리에서 직전 주입이 null이 아닙니다.");
                GearMenuOnboardingSeenStore.RestoreForTesting(beforeMine);
                Assert.IsFalse(GearMenuOnboardingSeenStore.IsOverriddenForTesting,
                    $"{LogPrefix} 직전 주입(null)으로 되돌렸는데 주입이 남았습니다.");
            }
            finally
            {
                GearMenuOnboardingSeenStore.RestoreForTesting(previous);
            }
        }

        // ==================================================================
        // 4. 우회 금지 (소스 감사 + 타입 — 활성 빌드 타깃 무관)
        // ==================================================================

        [Test]
        public void 우회금지_프로덕션의_설정_저장소_호출은_실제_저장소의_싱크_안에만_있고_싱크는_계수를_거친_두_함수에서만_불린다()
        {
            // 스캐너 생존(합성 입력) — 주석·문자열 속 이름은 세지 않고 코드 속 이름은 센다.
            string synthetic = $"x = UnityEngine.{PrefsTypeName}.GetInt(k, 0); // {PrefsTypeName}\ny = \"{PrefsTypeName}\";";
            Assert.AreEqual(1,
                SourceTextScanner.CountIdentifier(SourceTextScanner.BlankCommentsAndStrings(synthetic, null), PrefsTypeName),
                $"{LogPrefix} ★ 스캐너 교정 실패 — 합성 입력에서 코드 속 이름 1건을 세지 못했습니다. 아래 0건은 전부 무효입니다.");

            string realStoreCode = null;
            string widgetCode = null;
            int realStoreFiles = 0;
            var offenders = new List<string>();

            foreach (string file in SourceTextScanner.ProductionSourceFilesUnderAssets())
            {
                string name = Path.GetFileName(file);
                string code = SourceTextScanner.BlankCommentsAndStrings(File.ReadAllText(file), null);

                if (string.Equals(name, RealStoreFileName, StringComparison.Ordinal))
                {
                    realStoreCode = code;
                    realStoreFiles++;
                    continue;
                }
                if (string.Equals(name, WidgetFileName, StringComparison.Ordinal)) widgetCode = code;

                int n = SourceTextScanner.CountIdentifier(code, PrefsTypeName);
                if (n > 0) offenders.Add($"{file.Replace('\\', '/')} ({n}건)");
            }

            Assert.AreEqual(1, realStoreFiles,
                $"{LogPrefix} ★ {RealStoreFileName}을 정확히 하나 찾지 못했습니다({realStoreFiles}개) — 스캔 경로가 죽었거나 " +
                "같은 이름의 파일이 생겼습니다. 이 실행의 0건은 무효입니다.");
            Assert.IsNotNull(widgetCode, $"{LogPrefix} ★ {WidgetFileName}을 찾지 못했습니다 — 우회 검사가 위젯을 못 봤습니다.");

            Assert.IsEmpty(offenders,
                $"{LogPrefix} 실제 저장소 구현 밖에서 설정 저장소({PrefsTypeName})를 부릅니다:\n  " +
                string.Join("\n  ", offenders) + "\n" +
                "그 호출은 계수를 거치지 않으므로 PlayMode 스위트 끝의 「실제 저장소 접근 0」이 그것을 못 봅니다 — 테스트가 개발자 기계에 " +
                "조용히 씁니다. 새 저장소가 정말 필요하면 같은 형태(경계 + 계수 + 메모리 구현 + 스위트 주입)로 만들고 이 감사에 " +
                "허용 파일로 등재하십시오(리더 경유).");

            // ---- 위젯은 경계를 실제로 거친다(존재) ----
            Assert.GreaterOrEqual(SourceTextScanner.CountIdentifier(widgetCode, nameof(GearMenuOnboardingSeenStore)), 1,
                $"{LogPrefix} {WidgetFileName}이 {nameof(GearMenuOnboardingSeenStore)}를 거치지 않습니다.");
            Assert.GreaterOrEqual(SourceTextScanner.CountMemberAccess(widgetCode, nameof(IGearMenuOnboardingSeenStore.IsSeen)), 1,
                $"{LogPrefix} {WidgetFileName}이 경계로 「봤음」을 읽지 않습니다.");
            Assert.GreaterOrEqual(SourceTextScanner.CountMemberAccess(widgetCode, nameof(IGearMenuOnboardingSeenStore.MarkSeen)), 1,
                $"{LogPrefix} {WidgetFileName}이 경계로 「봤음」을 쓰지 않습니다.");

            // ---- 실제 저장소 파일 안: 설정 저장소 호출은 싱크 안에만, 멤버는 정수 읽기·쓰기·저장 셋뿐 ----
            int prefsInFile = SourceTextScanner.CountIdentifier(realStoreCode, PrefsTypeName);
            var members = new List<string>();
            foreach (Match m in Regex.Matches(realStoreCode, @"(?<![\w@])" + Regex.Escape(PrefsTypeName) + @"\s*\.\s*@?(\w+)"))
                members.Add(m.Groups[1].Value);
            Assert.AreEqual(prefsInFile, members.Count,
                $"{LogPrefix} {RealStoreFileName}에 멤버 접근이 아닌 {PrefsTypeName} 사용이 있습니다(별칭 등) — " +
                $"이름 {prefsInFile}건 · 멤버 접근 {members.Count}건.");
            CollectionAssert.AreEquivalent(
                new[] { nameof(PlayerPrefs.GetInt), nameof(PlayerPrefs.SetInt), nameof(PlayerPrefs.Save) },
                members,
                $"{LogPrefix} {RealStoreFileName}의 설정 저장소 호출이 「정수 읽기 1 · 정수 쓰기 1 · 저장 1」이 아닙니다: " +
                $"[{string.Join(", ", members)}]. 값 형식이 바뀌거나 지우기 능력이 생기면 하위 호환·원칙이 흔들립니다.");

            string sinkBody = SourceTextScanner.BlockMemberBody(realStoreCode, "class " + RealSinkType().Name);
            string readBody = SourceTextScanner.BlockMemberBody(realStoreCode, "bool " + nameof(IGearMenuOnboardingSeenStore.IsSeen));
            string writeBody = SourceTextScanner.BlockMemberBody(realStoreCode, "void " + nameof(IGearMenuOnboardingSeenStore.MarkSeen) + "(");
            Assert.IsNotNull(sinkBody, $"{LogPrefix} ★ 싱크 본문을 찾지 못했습니다 — 아래 판정은 무효입니다.");
            Assert.IsNotNull(readBody, $"{LogPrefix} ★ 읽기 본문을 찾지 못했습니다 — 아래 판정은 무효입니다.");
            Assert.IsNotNull(writeBody, $"{LogPrefix} ★ 쓰기 본문을 찾지 못했습니다 — 아래 판정은 무효입니다.");

            Assert.AreEqual(prefsInFile, SourceTextScanner.CountIdentifier(sinkBody, PrefsTypeName),
                $"{LogPrefix} {RealStoreFileName}에서 싱크 밖에 {PrefsTypeName} 호출이 있습니다 — 계수를 거치지 않는 길입니다.");

            string getOp = nameof(PlayerPrefsGearMenuOnboardingSeenStore.IIntPreferenceSink.GetInt);
            string setOp = nameof(PlayerPrefsGearMenuOnboardingSeenStore.IIntPreferenceSink.SetInt);
            string saveOp = nameof(PlayerPrefsGearMenuOnboardingSeenStore.IIntPreferenceSink.Save);
            AssertSinkCallReachedOnlyThrough(realStoreCode, sinkBody, writeBody, setOp, nameof(IGearMenuOnboardingSeenStore.MarkSeen));
            AssertSinkCallReachedOnlyThrough(realStoreCode, sinkBody, writeBody, saveOp, nameof(IGearMenuOnboardingSeenStore.MarkSeen));
            AssertSinkCallReachedOnlyThrough(realStoreCode, sinkBody, readBody, getOp, nameof(IGearMenuOnboardingSeenStore.IsSeen));

            // ---- 계수는 어떤 분기·반환·싱크 호출보다 먼저 (verify-change B7의 구조판) ----
            AssertCounterComesFirst(readBody, nameof(PlayerPrefsGearMenuOnboardingSeenStore.ReadCount),
                nameof(IGearMenuOnboardingSeenStore.IsSeen), getOp, setOp, saveOp);
            AssertCounterComesFirst(writeBody, nameof(PlayerPrefsGearMenuOnboardingSeenStore.WriteCount),
                nameof(IGearMenuOnboardingSeenStore.MarkSeen), getOp, setOp, saveOp);
        }

        /// <summary>파일 안의 <c>.op</c> 멤버 접근이 전부 「계수를 거친 본문」 또는 「싱크 구현」 안에만 있는가.</summary>
        private static void AssertSinkCallReachedOnlyThrough(string fileCode, string sinkBody, string ownerBody, string op, string ownerName)
        {
            int total = SourceTextScanner.CountMemberAccess(fileCode, op);
            int inOwner = SourceTextScanner.CountMemberAccess(ownerBody, op);
            int inSink = SourceTextScanner.CountMemberAccess(sinkBody, op);
            Assert.GreaterOrEqual(inOwner, 1, $"{LogPrefix} ★ {ownerName} 본문에서 .{op} 를 찾지 못했습니다 — 판정 무효.");
            Assert.GreaterOrEqual(inSink, 1, $"{LogPrefix} ★ 싱크 구현에서 .{op} 를 찾지 못했습니다 — 판정 무효.");
            Assert.AreEqual(total, inOwner + inSink,
                $"{LogPrefix} .{op} 가 {ownerName} 본문과 싱크 구현 밖에서도 불립니다(전체 {total} · {ownerName} {inOwner} · 싱크 {inSink}) — " +
                "계수를 거치지 않고 설정 저장소에 닿는 길입니다.");
        }

        /// <summary>본문에서 계수 증가가 첫 분기·반환 키워드와 첫 싱크 호출보다 앞에 있는가.</summary>
        private static void AssertCounterComesFirst(string body, string counter, string ownerName, params string[] sinkOps)
        {
            Match c = Regex.Match(body, @"(?<![\w@])" + Regex.Escape(counter) + @"(?!\w)");
            Assert.IsTrue(c.Success, $"{LogPrefix} {ownerName} 본문이 {counter}를 올리지 않습니다.");

            var earlier = new List<string>();
            foreach (string keyword in new[] { "if", "return", "switch", "throw", "goto" })
            {
                Match m = Regex.Match(body, @"(?<![\w@])" + keyword + @"(?!\w)");
                if (m.Success && m.Index < c.Index) earlier.Add(keyword);
            }
            foreach (string op in sinkOps)
            {
                Match m = Regex.Match(body, @"\.\s*@?" + Regex.Escape(op) + @"(?!\w)");
                if (m.Success && m.Index < c.Index) earlier.Add("." + op);
            }
            // ?: 와 && / || 단락 평가도 분기다.
            foreach (string shortCircuit in new[] { "?", "&&", "||" })
            {
                int at = body.IndexOf(shortCircuit, StringComparison.Ordinal);
                if (at >= 0 && at < c.Index) earlier.Add(shortCircuit);
            }

            Assert.IsEmpty(earlier,
                $"{LogPrefix} {ownerName} 본문에서 {counter} 증가보다 먼저 [{string.Join(", ", earlier)}]가 옵니다 — 그 분기·호출을 탄 " +
                "경로(예: 「이미 봤음이면 먼저 반환」)는 계수를 올리지 않아, 이미 1이 적힌 기계에서 주입 누락이 계수 0으로 숨습니다.");
        }

        [Test]
        public void 우회금지_출하본_싱크는_실제_저장소_안의_private_중첩이고_그_이름조차_그_파일_밖에_나오지_않는다()
        {
            Type sink = RealSinkType();
            Assert.IsTrue(sink.IsNestedPrivate,
                $"{LogPrefix} 출하본 싱크({sink.Name})가 private 중첩이 아닙니다 — 런타임 어셈블리 어느 파일에서든 그 인스턴스로 " +
                "설정 저장소를 직접 부를 수 있고, 그 호출은 PlayerPrefs 글자가 없어 텍스트 감사가, 계수를 거치지 않아 PlayMode 0 단언이 " +
                "못 봅니다(verify-change B1).");

            // 스캐너 생존 — 같은 이름을 코드 속 식별자와 평문 리터럴에서 각각 센다.
            var synLiterals = new List<string>();
            string synCode = SourceTextScanner.BlankCommentsAndStrings(
                $"var s = Outer.{sink.Name}.Instance; // {sink.Name}\nvar t = GetNestedType(\"{sink.Name}\");", synLiterals);
            Assert.AreEqual(1, SourceTextScanner.CountIdentifier(synCode, sink.Name),
                $"{LogPrefix} ★ 스캐너 교정 실패(식별자) — 아래 0건은 무효입니다.");
            Assert.AreEqual(1, synLiterals.FindAll(l => l.IndexOf(sink.Name, StringComparison.Ordinal) >= 0).Count,
                $"{LogPrefix} ★ 스캐너 교정 실패(리터럴) — 아래 0건은 무효입니다.");

            int inOwnFile = -1;
            var leaks = new List<string>();
            foreach (string file in SourceTextScanner.ProductionSourceFilesUnderAssets())
            {
                var literals = new List<string>();
                string code = SourceTextScanner.BlankCommentsAndStrings(File.ReadAllText(file), literals);
                int ident = SourceTextScanner.CountIdentifier(code, sink.Name);
                int lit = literals.FindAll(l => l.IndexOf(sink.Name, StringComparison.Ordinal) >= 0).Count;

                if (string.Equals(Path.GetFileName(file), RealStoreFileName, StringComparison.Ordinal))
                {
                    inOwnFile = ident;
                    continue;
                }
                if (ident + lit > 0) leaks.Add($"{file.Replace('\\', '/')} (식별자 {ident} · 리터럴 {lit})");
            }

            Assert.GreaterOrEqual(inOwnFile, 1,
                $"{LogPrefix} ★ {RealStoreFileName}에서 싱크 이름을 찾지 못했습니다({inOwnFile}) — 스캔 대상이 틀렸습니다. 판정 무효.");
            Assert.IsEmpty(leaks,
                $"{LogPrefix} 출하본 싱크 이름이 실제 저장소 파일 밖에 나옵니다:\n  " + string.Join("\n  ", leaks) +
                "\n리플렉션·별칭으로 싱크를 꺼내 쓰면 계수를 거치지 않고 설정 저장소에 닿습니다.");
        }

        // ==================================================================
        // 5. 알약이 없으면 읽지도 않는다 (verify-change B11)
        // ==================================================================

        [Test]
        public void 알약이_없으면_봤음_저장소를_읽지도_않는다()
        {
            string code = SourceTextScanner.BlankCommentsAndStrings(File.ReadAllText(FindProductionFile(WidgetFileName)), null);

            // 알약 필드 이름은 베끼지 않고 프로덕션의 「보이는 안내」 식에서 캡처한다.
            Match field = Regex.Match(code,
                @"(?<![\w@])" + Regex.Escape(nameof(GearRadialMenuWidget.VisibleOnboardingHint)) + @"\s*=>\s*(\w+)\s*!=\s*null");
            Assert.IsTrue(field.Success,
                $"{LogPrefix} ★ 판정 무효 — {nameof(GearRadialMenuWidget.VisibleOnboardingHint)} 식에서 알약 필드를 캡처하지 못했습니다.");
            string hintField = field.Groups[1].Value;

            MatchCollection marks = Regex.Matches(code, @"\.\s*@?" + Regex.Escape(nameof(IGearMenuOnboardingSeenStore.MarkSeen)) + @"(?!\w)");
            Assert.AreEqual(1, marks.Count, $"{LogPrefix} ★ 판정 무효 — 위젯의 「봤음」 쓰기 자리가 정확히 1곳이 아닙니다({marks.Count}).");
            int markAt = marks[0].Index;

            // 그 쓰기를 감싼 메서드 — 이름을 베끼지 않고 쓰기 자리 앞의 마지막 메서드 시그니처로 찾는다.
            Match signature = null;
            foreach (Match m in Regex.Matches(code.Substring(0, markAt),
                         @"(?:private|public|internal|protected)\s+[\w<>\[\],\s]*?\b(\w+)\s*\([^;{}()]*\)\s*\{"))
                signature = m;
            Assert.IsNotNull(signature, $"{LogPrefix} ★ 판정 무효 — 「봤음」 쓰기를 감싼 메서드를 찾지 못했습니다.");
            string methodName = signature.Groups[1].Value;
            Assert.IsNotNull(typeof(GearRadialMenuWidget).GetMethod(methodName,
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
                $"{LogPrefix} ★ 판정 무효 — 스캐너가 찾은 '{methodName}'이 실제 메서드가 아닙니다.");
            int open = signature.Index + signature.Length - 1;
            string body = BlockFrom(code, open);
            Assert.IsNotNull(body, $"{LogPrefix} ★ 판정 무효 — '{methodName}' 본문의 끝을 찾지 못했습니다.");
            Assert.Greater(open + body.Length, markAt, $"{LogPrefix} ★ 판정 무효 — 「봤음」 쓰기가 찾은 본문 밖에 있습니다.");

            // ★ verify-change 2단계 S2 — 「읽기」는 `.IsSeen`만이 아니다. `if (OnboardingHintSeen) return;`처럼 위젯 자신의 멤버가
            //   경계를 읽으면 그 이름을 부르는 것도 읽기다. 그런 멤버 이름을 베끼지 않고 **프로덕션 소스에서 도출**한다
            //   (식 본문·블록 본문 멤버 중 본문이 경계를 참조하는 것, 한 단계까지 — 한계는 클래스 문서).
            var boundaryIdent = new Regex(@"(?<![\w@])" + Regex.Escape(nameof(GearMenuOnboardingSeenStore)) + @"(?!\w)");
            var indirectReaders = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match m in Regex.Matches(code, @"(?:private|public|internal|protected)\s+[\w<>\[\],\s]*?\b(\w+)\s*=>([^;]*);"))
            {
                if (boundaryIdent.IsMatch(m.Groups[2].Value)) indirectReaders.Add(m.Groups[1].Value);
            }
            foreach (Match m in Regex.Matches(code, @"(?:private|public|internal|protected)\s+[\w<>\[\],\s]*?\b(\w+)\s*(?:\([^;{}()]*\)\s*)?\{"))
            {
                string memberBody = BlockFrom(code, m.Index + m.Length - 1);
                if (memberBody != null && boundaryIdent.IsMatch(memberBody)) indirectReaders.Add(m.Groups[1].Value);
            }
            indirectReaders.Remove(methodName);
            Assert.IsTrue(indirectReaders.Contains(nameof(GearRadialMenuWidget.OnboardingHintSeen)),
                $"{LogPrefix} ★ 판정 무효 — 경계를 읽는 위젯 멤버 도출이 {nameof(GearRadialMenuWidget.OnboardingHintSeen)}를 찾지 못했습니다" +
                $"([{string.Join(", ", indirectReaders)}]). 간접 읽기 검사가 죽었습니다.");

            Match guard = Regex.Match(body, @"(?<![\w@])if\s*\(\s*" + Regex.Escape(hintField) + @"\s*==\s*null\s*\)\s*return\s*;");
            Match directRead = Regex.Match(body, @"\.\s*@?" + Regex.Escape(nameof(IGearMenuOnboardingSeenStore.IsSeen)) + @"(?!\w)");
            Match firstBoundary = boundaryIdent.Match(body);
            Assert.IsTrue(directRead.Success, $"{LogPrefix} ★ 판정 무효 — '{methodName}'에서 「봤음」 읽기를 찾지 못했습니다.");
            Assert.IsTrue(firstBoundary.Success, $"{LogPrefix} ★ 판정 무효 — '{methodName}'에서 저장소 경계를 찾지 못했습니다.");

            int firstRead = directRead.Index;
            string firstReadWhat = "." + nameof(IGearMenuOnboardingSeenStore.IsSeen);
            foreach (string reader in indirectReaders)
            {
                Match m = Regex.Match(body, @"(?<![\w@])" + Regex.Escape(reader) + @"(?!\w)");
                if (m.Success && m.Index < firstRead)
                {
                    firstRead = m.Index;
                    firstReadWhat = reader;
                }
            }

            Assert.IsTrue(guard.Success && guard.Index < firstRead && guard.Index < firstBoundary.Index,
                $"{LogPrefix} '{methodName}'이 「{hintField}가 없으면 곧바로 반환」을 저장소 경계·「봤음」 읽기보다 먼저 하지 않습니다 — " +
                "안내 알약이 없는 화면에서도 저장소를 읽게 되고, 그 읽기는 실제 저장소에서는 설정 파일 접근입니다. " +
                $"(가드 {(guard.Success ? guard.Index.ToString(CultureInfo.InvariantCulture) : "없음")} · 첫 경계 {firstBoundary.Index} · " +
                $"첫 읽기 {firstRead}({firstReadWhat}))");
        }

        // ==================================================================
        // 6. 테스트용 주입 API는 프로덕션이 부르지 않는다
        // ==================================================================

        [Test]
        public void 테스트용_주입_API는_프로덕션_코드가_부르지_않는다()
        {
            string holder = nameof(GearMenuOnboardingSeenStore);
            string[] apis =
            {
                nameof(GearMenuOnboardingSeenStore.UseForTesting),
                nameof(GearMenuOnboardingSeenStore.RestoreForTesting),
                nameof(GearMenuOnboardingSeenStore.ResetForTesting),
                nameof(GearMenuOnboardingSeenStore.IsOverriddenForTesting),
            };
            var call = new Regex(@"(?<![\w@])" + Regex.Escape(holder) + @"\s*\.\s*@?(" +
                                 string.Join("|", Array.ConvertAll(apis, Regex.Escape)) + @")(?!\w)");
            var usingStatic = new Regex(@"(?<![\w@])using\s+static\s+[\w.\s]*?(?<![\w@])" + Regex.Escape(holder) + @"\s*;");

            // ★ verify-change 2단계 S1 — `using VcHolder = …GearMenuOnboardingSeenStore;` 뒤 `VcHolder.ResetForTesting()`는 위 `call`
            //   (보관소 이름 바로 뒤의 API)을 빠져나갔다. 별칭 이름을 추적하지 않고 **보관소 타입 별칭 선언 자체를 금지**한다 —
            //   프로덕션이 보관소에 별칭을 둘 정당한 이유가 없고, 선언이 없으면 그 별칭으로 무엇을 부르든 한꺼번에 막힌다.
            //   네임스페이스 별칭(`SI.GearMenuOnboardingSeenStore.X`)·`global::` 전체 한정 호출은 `call`이 이미 잡는다.
            var aliasOfHolder = new Regex(@"(?<![\w@])using\s+@?\w+\s*=\s*(?:global\s*::\s*)?[\w.\s]*?(?<![\w@])" +
                                          Regex.Escape(holder) + @"\s*;");

            // 스캐너 생존(합성 입력).
            string syn = SourceTextScanner.BlankCommentsAndStrings(
                $"a = StickMate.Interaction.{holder}.{apis[0]}(s); // {holder}.{apis[2]}()\nusing static StickMate.Interaction.{holder};\nb = \"{holder}.{apis[1]}\";",
                null);
            Assert.AreEqual(1, call.Matches(syn).Count, $"{LogPrefix} ★ 스캐너 교정 실패(호출) — 아래 0건은 무효입니다.");
            Assert.AreEqual(1, usingStatic.Matches(syn).Count, $"{LogPrefix} ★ 스캐너 교정 실패(using static) — 아래 0건은 무효입니다.");
            string synAlias = SourceTextScanner.BlankCommentsAndStrings(
                $"using VcHolder = StickMate.Interaction.{holder};\nusing VcGlobal = global::StickMate.Interaction.{holder};\n" +
                $"using VcOther = StickMate.Interaction.{holder}Other;\nusing VcIface = StickMate.Interaction.I{holder};\n" +
                $"// using VcComment = {holder};\nusing static StickMate.Interaction.{holder};\n", null);
            Assert.AreEqual(2, aliasOfHolder.Matches(synAlias).Count,
                $"{LogPrefix} ★ 스캐너 교정 실패(별칭) — 일반·global:: 별칭 2건만 잡고 다른 이름·인터페이스·주석·using static은 잡지 않아야 합니다. 아래 0건은 무효입니다.");

            // 존재 대조 — 같은 정규식이 실제 테스트 파일에서 네 API를 전부 찾는다.
            string ownTest = Path.Combine(Application.dataPath, "_Project", "Scripts", "Tests", "EditMode",
                nameof(GearMenuOnboardingSeenStoreTests) + ".cs");
            Assert.IsTrue(File.Exists(ownTest), $"{LogPrefix} ★ 존재 대조 파일이 없습니다: {ownTest}");
            var seenInTest = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match m in call.Matches(SourceTextScanner.BlankCommentsAndStrings(File.ReadAllText(ownTest), null)))
                seenInTest.Add(m.Groups[1].Value);
            CollectionAssert.IsSubsetOf(apis, seenInTest,
                $"{LogPrefix} ★ 존재 대조 실패 — 테스트 파일에서 네 API를 다 찾지 못했습니다([{string.Join(", ", seenInTest)}]). 판정 무효.");

            var offenders = new List<string>();
            int scanned = 0;
            foreach (string file in SourceTextScanner.ProductionSourceFilesUnderAssets())
            {
                scanned++;
                string code = SourceTextScanner.BlankCommentsAndStrings(File.ReadAllText(file), null);
                int calls = call.Matches(code).Count;
                int statics = usingStatic.Matches(code).Count;
                int aliases = aliasOfHolder.Matches(code).Count;
                if (calls + statics + aliases > 0)
                    offenders.Add($"{file.Replace('\\', '/')} (호출 {calls} · using static {statics} · 보관소 별칭 {aliases})");
            }
            Assert.Greater(scanned, 1, $"{LogPrefix} ★ 프로덕션 파일을 거의 못 읽었습니다({scanned}) — 판정 무효.");
            Assert.IsEmpty(offenders,
                $"{LogPrefix} 프로덕션 코드가 「봤음」 저장소의 테스트용 주입 API를 부르거나 그 길을 엽니다(보관소 타입 별칭 포함):\n  " +
                string.Join("\n  ", offenders) +
                "\n출하본에서 주입이 걸리면 「봤음」이 디스크에 남지 않거나(안내가 매 실행 뜬다) 남의 저장소를 씁니다.");
        }

        // ==================================================================
        // 6-2. 실제 저장소는 보관소 한 곳에서만 만든다 (verify-change B8)
        // ==================================================================

        /// <summary>
        /// ★ verify-change B8 — 위젯이 <c>GearMenuOnboardingSeenStore.Current</c> 대신 실제 저장소를 <b>직접 만들어</b> 쓰면
        /// 스위트 주입이 통째로 우회된다. 그 코드에는 <c>PlayerPrefs</c> 글자가 없어 우회 금지 감사가 못 보고, 위젯의 다른 자리에
        /// 경계가 남아 있으면 경계 존재 단언도 초록이다. 잡는 것은 PlayMode 스위트 끝의 계수 0 단언뿐인데, 그건 <b>부채꼴을 여는
        /// 픽스처가 그 실행에 들어 있을 때만</b> 무는 실행 잠금이다 — 그래서 텍스트로도 잠근다.
        ///
        /// <para>식별자로 막는 이유: <c>new X()</c>만 막으면 <c>X s = new()</c>(대상 형식 new) · <c>typeof(X)</c> · <c>Activator</c>가
        /// 빠진다. 타입 이름 자체가 자기 파일·보관소 파일 밖 프로덕션에 나오지 않게 한다(평문 리터럴의 이름 리플렉션 포함).</para>
        /// </summary>
        [Test]
        public void 우회금지_실제_저장소_타입은_자기_파일과_보관소_파일_밖_프로덕션에_나오지_않는다()
        {
            string realType = nameof(PlayerPrefsGearMenuOnboardingSeenStore);
            string holderFile = nameof(GearMenuOnboardingSeenStore) + ".cs";
            var construction = new Regex(@"(?<![\w@])new\s+(?:[\w.]+\.)?" + Regex.Escape(realType) + @"\s*\(");

            // 스캐너 생존(합성 입력) — 코드 속 식별자·생성식은 세고, 주석 속 이름은 세지 않으며, 리터럴은 따로 모은다.
            var synLiterals = new List<string>();
            string syn = SourceTextScanner.BlankCommentsAndStrings(
                $"var s = new StickMate.Interaction.{realType}(); // new {realType}()\nvar t = Type.GetType(\"{realType}\");", synLiterals);
            Assert.AreEqual(1, construction.Matches(syn).Count, $"{LogPrefix} ★ 스캐너 교정 실패(생성식) — 아래 0건은 무효입니다.");
            Assert.AreEqual(1, SourceTextScanner.CountIdentifier(syn, realType), $"{LogPrefix} ★ 스캐너 교정 실패(식별자) — 아래 0건은 무효입니다.");
            Assert.AreEqual(1, synLiterals.FindAll(l => l.IndexOf(realType, StringComparison.Ordinal) >= 0).Count,
                $"{LogPrefix} ★ 스캐너 교정 실패(리터럴) — 아래 0건은 무효입니다.");

            int holderConstructions = -1;
            int ownFileSeen = 0;
            var leaks = new List<string>();
            foreach (string file in SourceTextScanner.ProductionSourceFilesUnderAssets())
            {
                string name = Path.GetFileName(file);
                var literals = new List<string>();
                string code = SourceTextScanner.BlankCommentsAndStrings(File.ReadAllText(file), literals);

                if (string.Equals(name, RealStoreFileName, StringComparison.Ordinal)) { ownFileSeen++; continue; }
                if (string.Equals(name, holderFile, StringComparison.Ordinal))
                {
                    holderConstructions = construction.Matches(code).Count;
                    continue;
                }

                int ident = SourceTextScanner.CountIdentifier(code, realType);
                int lit = literals.FindAll(l => l.IndexOf(realType, StringComparison.Ordinal) >= 0).Count;
                if (ident + lit > 0) leaks.Add($"{file.Replace('\\', '/')} (식별자 {ident} · 리터럴 {lit})");
            }

            Assert.AreEqual(1, ownFileSeen, $"{LogPrefix} ★ {RealStoreFileName}을 정확히 하나 찾지 못했습니다({ownFileSeen}) — 판정 무효.");
            Assert.AreEqual(1, holderConstructions,
                $"{LogPrefix} ★ 존재 대조 실패 — {holderFile}의 실제 저장소 생성식이 정확히 1개가 아닙니다({holderConstructions}). " +
                "보관소가 기본값을 다른 방식으로 만들게 됐다면 이 감사를 함께 고쳐야 합니다. 판정 무효.");
            Assert.IsEmpty(leaks,
                $"{LogPrefix} 실제 저장소 타입이 자기 파일·보관소 파일 밖 프로덕션에 나옵니다:\n  " + string.Join("\n  ", leaks) +
                $"\n그 자리는 {nameof(GearMenuOnboardingSeenStore)}.{nameof(GearMenuOnboardingSeenStore.Current)}를 거치지 않으므로 스위트 주입이 " +
                "우회되고, 테스트가 개발자 기계의 실제 설정 저장소를 읽고 씁니다(verify-change B8).");
        }

        // ==================================================================
        // 7. 키 단일 정의
        // ==================================================================

        [Test]
        public void 키_문자열은_프로덕션_전체에서_실제_저장소의_선언_한_곳에만_있다()
        {
            string key = PlayerPrefsGearMenuOnboardingSeenStore.Key;
            Assert.IsNotEmpty(key, $"{LogPrefix} 키 상수가 비었습니다.");

            // 스캐너 생존 — 평문 리터럴은 수집하고 주석 속 같은 글자는 수집하지 않는다.
            var syntheticLiterals = new List<string>();
            SourceTextScanner.BlankCommentsAndStrings($"const string K = \"{key}\"; // \"{key}\"", syntheticLiterals);
            Assert.AreEqual(1, syntheticLiterals.FindAll(l => l.IndexOf(key, StringComparison.Ordinal) >= 0).Count,
                $"{LogPrefix} ★ 스캐너 교정 실패 — 합성 입력의 리터럴 1건을 세지 못했습니다. 아래 판정은 무효입니다.");

            var hits = new List<string>();
            foreach (string file in SourceTextScanner.ProductionSourceFilesUnderAssets())
            {
                var literals = new List<string>();
                SourceTextScanner.BlankCommentsAndStrings(File.ReadAllText(file), literals);
                foreach (string literal in literals)
                {
                    if (literal.IndexOf(key, StringComparison.Ordinal) >= 0)
                        hits.Add($"{Path.GetFileName(file)}: \"{literal}\"");
                }
            }

            Assert.AreEqual(1, hits.Count,
                $"{LogPrefix} 키 문자열이 프로덕션에 정확히 1곳(선언)이어야 하는데 {hits.Count}곳입니다:\n  " +
                string.Join("\n  ", hits) + "\n" +
                "0이면 선언이 이어 붙이기·보간으로 숨었고, 2 이상이면 사본이 생겨 한쪽만 바뀌는 순간 하위 호환이 조용히 깨집니다.");
            StringAssert.StartsWith(RealStoreFileName + ":", hits[0],
                $"{LogPrefix} 키 선언이 실제 저장소 파일 밖에 있습니다: {hits[0]}");
        }
    }
}
