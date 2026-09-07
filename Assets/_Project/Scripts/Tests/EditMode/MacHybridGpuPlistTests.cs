using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using StickMate.Platform;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// macOS 빌드 후처리(<c>Info.plist</c>에 <c>NSSupportsAutomaticGraphicsSwitching=true</c> 보장)의
    /// <b>파싱·삽입 로직</b> 회귀 잠금. Windows 쪽 <see cref="HybridGpuExportPatchTests"/>와 대칭이다.
    ///
    /// ========================================================================
    /// ★ 이 테스트가 실제로 지키는 것
    /// ========================================================================
    /// 후처리의 진짜 사고 지점은 "키를 못 넣었다"가 아니라 <b>"다른 것을 망가뜨렸다"</b>이다.
    /// plist는 앱의 신원(<c>CFBundleIdentifier</c>)·아이콘·최소 OS 버전이 전부 들어 있는 파일이고,
    /// 게다가 <b>코드 서명에 봉인</b>돼 있어 잘못 건드리면 Apple Silicon에서 앱이 아예 실행되지 않는다.
    /// 그래서 아래 검사들은 "키가 들어갔는가"보다 <b>"나머지가 그대로인가"</b>에 무게를 둔다.
    ///
    /// <para><b>가장 중요한 한 건</b>은
    /// <see cref="음성_중첩_dict_안의_같은_이름은_루트_선언으로_세지_않는다"/>이다. 루트가 아닌 곳의
    /// 같은 이름을 "있다"로 세면 후처리가 <b>필요한 삽입을 조용히 건너뛴다</b> — 실패한 측정과
    /// 성공한 측정이 똑같이 생기는, 이 저장소가 반복해 당한 바로 그 형태다.</para>
    ///
    /// <para><b>실제 산출물에 의존하지 않는다.</b> <c>Builds/</c>는 있을 수도 없을 수도 있다.
    /// 테스트가 산출물에 기대면 "빌드가 없어서 초록"이라는 가장 조용한 거짓 통과가 생긴다.
    /// 그래서 plist를 <b>텍스트로 직접 조립</b>해 먹인다(Windows 쪽이 PE를 바이트로 조립하는 것과 같다).</para>
    ///
    /// <para><b>실기 검증과는 다른 것을 잰다.</b> 이 테스트는 "plist가 옳게 바뀌는가"만 본다.
    /// "그 키가 실제로 내장 GPU 선택으로 귀결되는가"는 <b>듀얼 GPU Mac에서만</b> 확인할 수 있고
    /// 이 개발 머신은 Apple Silicon이라 내장/외장이라는 개념 자체가 없다 — 그 갭은
    /// <c>Tests/EditMode/PlatformParityAuditTests.cs</c>가 러너에 「건너뜀」으로 띄운다.</para>
    /// </summary>
    public sealed class MacHybridGpuPlistTests
    {
        // ====================================================================
        // 합성 plist 조립기 — 실제 산출물에 기대지 않기 위한 장치
        // ====================================================================

        private const string Header =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" " +
            "\"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
            "<plist version=\"1.0\">\n" +
            "  <dict>\n";

        private const string Footer =
            "  </dict>\n" +
            "</plist>\n";

        /// <summary>
        /// Unity 6000.0.82f1이 실제로 굽는 <c>Info.plist</c>와 <b>같은 모양</b>의 최소 문서를 만든다:
        /// DOCTYPE이 있고, 루트 dict가 2칸 들여쓰기이며, 항목은 4칸이고, 빈 요소는 <c>&lt;true /&gt;</c>
        /// 표기이며, <b>중첩 dict가 하나 있다</b>(<c>NSAppTransportSecurity</c>).
        /// </summary>
        /// <param name="extraBody">루트 dict 끝에 추가로 넣을 원문(없으면 빈 문자열).</param>
        private static string BuildSyntheticPlist(string extraBody = "")
        {
            return Header +
                   "    <key>CFBundleDevelopmentRegion</key>\n" +
                   "    <string>English</string>\n" +
                   "    <key>CFBundleExecutable</key>\n" +
                   "    <string>StickMate</string>\n" +
                   "    <key>CFBundleIdentifier</key>\n" +
                   "    <string>com.Vibelab.StickMate</string>\n" +
                   "    <key>NSAppTransportSecurity</key>\n" +
                   "    <dict>\n" +
                   "      <key>NSAllowsArbitraryLoads</key>\n" +
                   "      <true />\n" +
                   "    </dict>\n" +
                   "    <key>LSMinimumSystemVersion</key>\n" +
                   "    <string>11.0</string>\n" +
                   extraBody +
                   Footer;
        }

        private static string DeclarationBody(string elementName) =>
            "    <key>" + HybridGpuPreferencePolicy.MacAutomaticGraphicsSwitchingKey + "</key>\n" +
            "    <" + elementName + " />\n";

        private static string DesiredElementName =>
            HybridGpuPreferencePolicy.MacDesiredAutomaticGraphicsSwitching
                ? HybridGpuPreferencePolicy.MacTrueElementName
                : HybridGpuPreferencePolicy.MacFalseElementName;

        // ====================================================================
        // 1. 양성 — 조립기와 파서가 먼저 옳아야 아래 음성들이 뜻을 가진다
        // ====================================================================

        [Test]
        public void 양성_합성plist에서_루트_항목을_순서대로_읽는다()
        {
            string plist = BuildSyntheticPlist();

            Assert.IsTrue(PropertyListRootReader.TryReadRootEntries(plist, out List<PlistRootEntry> entries, out string error),
                "합성 plist 파싱이 실패했습니다 — 조립기가 틀렸다면 아래 음성 대조가 전부 무의미해집니다. 사유: " + error);
            Assert.IsNull(error, "성공했는데 사유가 채워졌습니다.");

            var keys = new List<string>();
            foreach (PlistRootEntry entry in entries) keys.Add(entry.Key);

            CollectionAssert.AreEqual(
                new[]
                {
                    "CFBundleDevelopmentRegion", "CFBundleExecutable", "CFBundleIdentifier",
                    "NSAppTransportSecurity", "LSMinimumSystemVersion",
                },
                keys,
                "루트 항목의 집합이나 순서가 다릅니다 — 파서가 문서 순서를 지키지 않으면 " +
                "'기존 항목이 그대로인가' 대조가 통째로 무의미해집니다.");

            Assert.IsTrue(PropertyListRootReader.TryFind(entries, "CFBundleIdentifier", out PlistRootEntry bundleId));
            Assert.AreEqual("string", bundleId.ValueElementName, "문자열 값의 요소 이름을 잘못 읽었습니다.");
            Assert.AreEqual("com.Vibelab.StickMate", bundleId.ValueText, "문자열 값의 내용을 잘못 읽었습니다.");

            Assert.IsTrue(PropertyListRootReader.TryFind(entries, "NSAppTransportSecurity", out PlistRootEntry ats));
            Assert.AreEqual("dict", ats.ValueElementName, "중첩 dict의 요소 이름을 잘못 읽었습니다.");
            Assert.IsEmpty(ats.ValueText,
                "컨테이너의 값 텍스트를 읽어 냈습니다 — 중첩 내용이 값으로 새어 들어오면 " +
                "'기존 항목이 그대로인가' 대조가 엉뚱한 이유로 빨개집니다.");

            // 음성 대조: 없는 키는 못 찾아야 한다(조회가 통째로 'true'를 돌려주지 않음을 못박는다).
            Assert.IsFalse(PropertyListRootReader.TryFind(entries, "ThisKeyDoesNotExist", out _),
                "없는 키를 찾아냈습니다 — 조회가 아무거나 통과시킵니다.");
        }

        /// <summary>
        /// ★ <b>이 파일에서 가장 중요한 테스트.</b> <c>NSAppTransportSecurity</c> 중첩 dict 안에
        /// 우리 키와 <b>같은 이름</b>을 넣는다. 루트만 보는 구현은 "없다"고 답해야 하고,
        /// 문서 전체를 훑는 구현(문자열 <c>Contains</c> 같은 것)은 여기서 반드시 깨진다.
        ///
        /// <para>왜 치명적인가: "있다"고 잘못 세면 후처리가 <b>삽입을 건너뛰고 PASS를 찍는다</b>.
        /// 출하된 앱에는 키가 없는데 로그는 초록이다 — 실패한 측정과 성공한 측정이 똑같이 생긴다.</para>
        /// </summary>
        [Test]
        public void 음성_중첩_dict_안의_같은_이름은_루트_선언으로_세지_않는다()
        {
            string key = HybridGpuPreferencePolicy.MacAutomaticGraphicsSwitchingKey;
            string plist = Header +
                           "    <key>NSAppTransportSecurity</key>\n" +
                           "    <dict>\n" +
                           "      <key>" + key + "</key>\n" +
                           "      <true />\n" +
                           "    </dict>\n" +
                           "    <key>CFBundleIdentifier</key>\n" +
                           "    <string>com.Vibelab.StickMate</string>\n" +
                           Footer;

            // 양성 대조 ①: 이 문서에 그 이름이 **문자 그대로 존재한다**(그래서 이 검사가 공허하지 않다).
            StringAssert.Contains(key, plist,
                "표본에 그 이름이 아예 없습니다 — 이 테스트는 아무것도 구분하지 못합니다.");

            Assert.IsTrue(PropertyListRootReader.TryReadRootEntries(plist, out List<PlistRootEntry> entries, out string error), error);

            // 양성 대조 ②: 같은 문서에서 루트 키는 제대로 찾힌다(파서가 통째로 눈이 먼 것이 아님).
            Assert.IsTrue(PropertyListRootReader.TryFind(entries, "CFBundleIdentifier", out _),
                "루트 키까지 못 찾았습니다 — 조회가 통째로 고장 났습니다.");

            Assert.IsFalse(PropertyListRootReader.TryFind(entries, key, out _),
                "중첩 dict 안의 같은 이름을 루트 선언으로 셌습니다. 이대로면 후처리가 삽입을 " +
                "건너뛰고 PASS를 찍는데, 출하된 앱에는 그 키가 없습니다.");

            // 그리고 그 상태의 판정은 반드시 '삽입 필요'여야 한다.
            Assert.AreEqual(HybridGpuPreferencePolicy.PlistVerdict.NeedsDeclaration,
                HybridGpuPreferencePolicy.ClassifyMacPlistEntry(false, null, out _),
                "키가 루트에 없는데 삽입이 필요하지 않다고 판정했습니다.");
        }

        // ====================================================================
        // 2. 삽입 — 넣는 것보다 "나머지가 그대로인가"가 중요하다
        // ====================================================================

        [Test]
        public void 삽입은_키_하나만_더하고_기존_항목을_한_글자도_바꾸지_않는다()
        {
            string original = BuildSyntheticPlist();
            Assert.IsTrue(PropertyListRootReader.TryReadRootEntries(original, out List<PlistRootEntry> before, out string beforeError), beforeError);

            Assert.IsTrue(PropertyListRootReader.TryInsertBooleanEntry(original,
                    HybridGpuPreferencePolicy.MacAutomaticGraphicsSwitchingKey,
                    HybridGpuPreferencePolicy.MacDesiredAutomaticGraphicsSwitching,
                    out string patched, out string insertError),
                "삽입이 실패했습니다: " + insertError);
            Assert.IsNull(insertError, "성공했는데 사유가 채워졌습니다.");

            Assert.IsTrue(PropertyListRootReader.TryReadRootEntries(patched, out List<PlistRootEntry> after, out string afterError), afterError);
            Assert.AreEqual(before.Count + 1, after.Count, "루트 항목이 정확히 1개 늘지 않았습니다.");

            for (int i = 0; i < before.Count; i++)
            {
                Assert.AreEqual(before[i].Key, after[i].Key, $"기존 항목 {i}번의 키가 바뀌었습니다.");
                Assert.AreEqual(before[i].ValueElementName, after[i].ValueElementName,
                    $"기존 항목 {i}번의 값 종류가 바뀌었습니다.");
                Assert.AreEqual(before[i].ValueText, after[i].ValueText,
                    $"기존 항목 {i}번의 값 내용이 바뀌었습니다.");
            }

            PlistRootEntry added = after[after.Count - 1];
            Assert.AreEqual(HybridGpuPreferencePolicy.MacAutomaticGraphicsSwitchingKey, added.Key,
                "삽입된 항목의 키가 다릅니다.");
            Assert.AreEqual(DesiredElementName, added.ValueElementName,
                "삽입된 값이 정책이 원하는 값이 아닙니다.");

            // 원본 문자열은 불변이어야 한다(순수 함수).
            Assert.AreEqual(BuildSyntheticPlist(), original,
                "원본 문자열이 바뀌었습니다 — 이 함수는 순수 함수여야 합니다.");

            // DOCTYPE·XML 선언이 살아 있는가. 재직렬화 구현은 여기서 깨진다.
            StringAssert.Contains("<!DOCTYPE plist PUBLIC", patched,
                "DOCTYPE이 사라졌습니다 — 문서를 재직렬화했다는 뜻이고, 그러면 '무엇이 바뀌었는가'를 " +
                "아무도 대조할 수 없습니다.");
            StringAssert.Contains("<?xml version=", patched, "XML 선언이 사라졌습니다.");
        }

        /// <summary>결과가 <b>연속된 한 덩어리의 삽입</b>인지 문자 단위로 확인한다. 앞뒤가 원본과
        /// 완전히 같아야 하고, 늘어난 길이가 곧 삽입된 조각의 길이여야 한다.</summary>
        [Test]
        public void 삽입은_연속된_한_덩어리이고_앞뒤가_원본과_동일하다()
        {
            string original = BuildSyntheticPlist();
            Assert.IsTrue(PropertyListRootReader.TryInsertBooleanEntry(original,
                HybridGpuPreferencePolicy.MacAutomaticGraphicsSwitchingKey, true, out string patched, out _));

            int common = 0;
            while (common < original.Length && common < patched.Length && original[common] == patched[common]) common++;

            int tail = 0;
            while (tail < original.Length - common && tail < patched.Length - common &&
                   original[original.Length - 1 - tail] == patched[patched.Length - 1 - tail])
            {
                tail++;
            }

            Assert.AreEqual(original.Length, common + tail,
                "원본이 '앞부분 + 뒷부분'으로 남지 않았습니다 — 삽입이 아니라 여러 곳을 고쳤습니다. " +
                $"(공통 앞 {common}자 + 공통 뒤 {tail}자, 원본 {original.Length}자)");
            Assert.Greater(patched.Length, original.Length, "아무것도 늘지 않았습니다.");
        }

        /// <summary>★ 멱등 — 이미 선언돼 있으면 정책이 "쓰지 마라"라고 답해야 하고,
        /// 삽입기는 <b>조용히 넘어가는 대신 사유와 함께 거부</b>해야 한다(중복 키 방지).</summary>
        [Test]
        public void 멱등_이미_선언돼_있으면_판정은_통과이고_삽입은_거부된다()
        {
            string original = BuildSyntheticPlist();
            Assert.IsTrue(PropertyListRootReader.TryInsertBooleanEntry(original,
                HybridGpuPreferencePolicy.MacAutomaticGraphicsSwitchingKey, true, out string once, out _));

            // (1) 정책: 두 번째 판정은 '이미 선언됨'이어야 한다.
            Assert.IsTrue(PropertyListRootReader.TryReadRootEntries(once, out List<PlistRootEntry> entries, out _));
            Assert.IsTrue(PropertyListRootReader.TryFind(entries,
                HybridGpuPreferencePolicy.MacAutomaticGraphicsSwitchingKey, out PlistRootEntry entry));
            Assert.AreEqual(HybridGpuPreferencePolicy.PlistVerdict.AlreadyDeclared,
                HybridGpuPreferencePolicy.ClassifyMacPlistEntry(true, entry.ValueElementName, out string reason),
                "이미 선언돼 있는데 다시 쓰겠다고 판정했습니다 — 멱등이 깨집니다.");
            Assert.IsNotEmpty(reason ?? string.Empty, "사유가 비어 있습니다.");

            // (2) 삽입기: 두 번째 호출은 실패해야 한다(조용히 중복을 만들지 않는다).
            Assert.IsFalse(PropertyListRootReader.TryInsertBooleanEntry(once,
                    HybridGpuPreferencePolicy.MacAutomaticGraphicsSwitchingKey, true, out string twice, out string dupError),
                "이미 있는 키를 또 삽입했습니다 — plist에 같은 키가 두 번 들어가면 어느 값이 이기는지 " +
                "아무도 설명할 수 없습니다.");
            Assert.IsNull(twice, "실패했는데 결과 텍스트가 채워졌습니다.");
            Assert.IsNotEmpty(dupError ?? string.Empty, "실패 사유가 비어 있습니다 — 조용한 실패입니다.");
        }

        /// <summary>빈 요소 표기(<c>&lt;true /&gt;</c> vs <c>&lt;true/&gt;</c>)를 원본에서 관찰해 따라가는가.
        /// 새 줄만 다르게 생기면 사람이 디프에서 "원래 있던 것"을 잘못 읽는다.</summary>
        [Test]
        public void 삽입은_원본의_빈요소_표기와_들여쓰기를_따라간다()
        {
            string spaced = BuildSyntheticPlist();                       // "<true />" 표기를 쓴다
            Assert.IsTrue(PropertyListRootReader.TryInsertBooleanEntry(spaced,
                HybridGpuPreferencePolicy.MacAutomaticGraphicsSwitchingKey, true, out string spacedOut, out _));
            StringAssert.Contains("    <" + HybridGpuPreferencePolicy.MacTrueElementName + " />", spacedOut,
                "공백 있는 빈 요소 표기(<true />)를 따라가지 않았습니다.");

            string tight = spaced.Replace("<" + HybridGpuPreferencePolicy.MacTrueElementName + " />",
                                          "<" + HybridGpuPreferencePolicy.MacTrueElementName + "/>");
            Assert.AreNotEqual(spaced, tight, "표본 두 개가 같습니다 — 이 검사가 아무것도 구분하지 못합니다.");
            Assert.IsTrue(PropertyListRootReader.TryInsertBooleanEntry(tight,
                HybridGpuPreferencePolicy.MacAutomaticGraphicsSwitchingKey, true, out string tightOut, out _));
            StringAssert.Contains("    <" + HybridGpuPreferencePolicy.MacTrueElementName + "/>", tightOut,
                "공백 없는 빈 요소 표기(<true/>)를 따라가지 않았습니다.");
            StringAssert.DoesNotContain("<" + HybridGpuPreferencePolicy.MacTrueElementName + " />", tightOut,
                "원본에 없던 표기를 새로 들여왔습니다.");
        }

        [Test]
        public void 삽입은_CRLF_문서에서_CRLF를_유지한다()
        {
            string crlf = BuildSyntheticPlist().Replace("\n", "\r\n");
            Assert.IsTrue(PropertyListRootReader.TryInsertBooleanEntry(crlf,
                    HybridGpuPreferencePolicy.MacAutomaticGraphicsSwitchingKey, true, out string patched, out string error),
                "CRLF 문서에서 삽입이 실패했습니다: " + error);

            StringAssert.Contains("</key>\r\n", patched, "삽입한 줄이 CRLF로 끝나지 않았습니다.");
            Assert.AreEqual(0, CountLoneLf(patched),
                "CRLF 문서에 LF 단독 줄바꿈이 섞였습니다 — 줄끝이 뒤섞이면 디프가 통째로 더러워집니다.");
        }

        private static int CountLoneLf(string text)
        {
            int count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '\n') continue;
                if (i == 0 || text[i - 1] != '\r') count++;
            }
            return count;
        }

        // ====================================================================
        // 3. 음성 — 망가진 입력에서 실제로 실패하는가
        // ====================================================================

        [Test]
        public void 음성_XML이_깨지면_사유와_함께_실패한다()
        {
            string broken = BuildSyntheticPlist().Replace("</dict>", "</dixt>");

            Assert.IsFalse(PropertyListRootReader.TryReadRootEntries(broken, out List<PlistRootEntry> entries, out string error),
                "깨진 XML을 성공으로 읽었습니다.");
            Assert.IsNull(entries, "실패했는데 항목 목록이 채워졌습니다.");
            Assert.IsNotEmpty(error ?? string.Empty, "실패 사유가 비어 있습니다 — 조용한 실패입니다.");
        }

        [Test]
        public void 음성_루트가_plist가_아니면_실패한다()
        {
            string notPlist = "<?xml version=\"1.0\"?>\n<notplist>\n  <dict>\n  </dict>\n</notplist>\n";

            Assert.IsFalse(PropertyListRootReader.TryReadRootEntries(notPlist, out _, out string error),
                "plist가 아닌 문서를 plist로 읽었습니다.");
            Assert.IsNotEmpty(error ?? string.Empty);
        }

        [Test]
        public void 음성_바이너리plist는_다루지_않고_거부한다()
        {
            string binary = PropertyListRootReader.BinaryPlistMagic + "00 rubbish";

            Assert.IsFalse(PropertyListRootReader.TryReadRootEntries(binary, out _, out string error),
                "바이너리 plist를 XML로 읽어 냈습니다 — 그 위에 텍스트를 삽입하면 파일이 깨집니다.");
            StringAssert.Contains(PropertyListRootReader.BinaryPlistMagic, error ?? string.Empty,
                "실패 사유가 무엇이 문제인지 말하지 않습니다.");
        }

        [Test]
        public void 음성_key와_값의_짝이_깨지면_실패한다()
        {
            string doubledKey = Header +
                                "    <key>A</key>\n" +
                                "    <key>B</key>\n" +
                                "    <string>x</string>\n" +
                                Footer;
            Assert.IsFalse(PropertyListRootReader.TryReadRootEntries(doubledKey, out _, out string error1),
                "<key>가 연달아 두 번 나온 문서를 성공으로 읽었습니다.");
            Assert.IsNotEmpty(error1 ?? string.Empty);

            string danglingKey = Header + "    <key>A</key>\n" + Footer;
            Assert.IsFalse(PropertyListRootReader.TryReadRootEntries(danglingKey, out _, out string error2),
                "짝 없는 <key>로 끝나는 문서를 성공으로 읽었습니다.");
            Assert.IsNotEmpty(error2 ?? string.Empty);

            string valueFirst = Header + "    <string>x</string>\n" + Footer;
            Assert.IsFalse(PropertyListRootReader.TryReadRootEntries(valueFirst, out _, out string error3),
                "<key> 없이 값이 먼저 나온 문서를 성공으로 읽었습니다.");
            Assert.IsNotEmpty(error3 ?? string.Empty);

            // 양성 대조: 짝이 맞으면 통과한다(위 셋이 '무조건 실패'가 아님을 못박는다).
            string wellFormed = Header + "    <key>A</key>\n    <string>x</string>\n" + Footer;
            Assert.IsTrue(PropertyListRootReader.TryReadRootEntries(wellFormed, out List<PlistRootEntry> ok, out string okError), okError);
            Assert.AreEqual(1, ok.Count);
        }

        [Test]
        public void 음성_XML_특수문자가_든_키는_거부한다()
        {
            string original = BuildSyntheticPlist();
            foreach (string bad in new[] { "a<b", "a>b", "a&b" })
            {
                Assert.IsFalse(PropertyListRootReader.TryInsertBooleanEntry(original, bad, true, out string result, out string error),
                    $"XML 특수문자가 든 키 '{bad}'를 이스케이프 없이 써 넣었습니다 — 문서가 깨집니다.");
                Assert.IsNull(result);
                Assert.IsNotEmpty(error ?? string.Empty);
            }

            // 양성 대조: 평범한 키는 통과한다.
            Assert.IsTrue(PropertyListRootReader.TryInsertBooleanEntry(original, "PlainKeyName", true, out _, out _),
                "평범한 키까지 거부했습니다 — 이 검사가 삽입을 통째로 막고 있습니다.");
        }

        // ====================================================================
        // 4. 정책 — 판정이 세 갈래로 실제로 갈라지는가
        // ====================================================================

        [Test]
        public void 정책_없으면_삽입_true면_멱등_그밖은_거부다()
        {
            Assert.AreEqual(HybridGpuPreferencePolicy.PlistVerdict.NeedsDeclaration,
                HybridGpuPreferencePolicy.ClassifyMacPlistEntry(false, null, out string a),
                "키가 없는데 삽입하지 않겠다고 판정했습니다.");
            Assert.IsNotEmpty(a ?? string.Empty);

            Assert.AreEqual(HybridGpuPreferencePolicy.PlistVerdict.AlreadyDeclared,
                HybridGpuPreferencePolicy.ClassifyMacPlistEntry(true, DesiredElementName, out string b),
                "이미 원하는 값인데 다시 쓰겠다고 판정했습니다 — 멱등이 깨집니다.");
            Assert.IsNotEmpty(b ?? string.Empty);

            foreach (string stray in new[]
                     {
                         HybridGpuPreferencePolicy.MacFalseElementName, "string", "integer", "dict", null,
                     })
            {
                Assert.AreEqual(HybridGpuPreferencePolicy.PlistVerdict.Unexpected,
                    HybridGpuPreferencePolicy.ClassifyMacPlistEntry(true, stray, out string c),
                    $"기대 밖의 값 <{stray ?? "(null)"}/>을 조용히 통과시켰습니다 — 사람이 일부러 꺼 둔 " +
                    "결정을 빌드 후처리가 덮어쓰게 됩니다.");
                Assert.IsNotEmpty(c ?? string.Empty);
            }
        }

        [Test]
        public void 정책_macOS_키와_값이_애플_문서의_이름과_형태를_지킨다()
        {
            Assert.IsTrue(HybridGpuPreferencePolicy.MacDesiredAutomaticGraphicsSwitching,
                "이 앱은 24시간 상주 장식 앱이라 '전환을 감당한다'고 선언해 내장에서 시작해야 합니다. " +
                "false로 바꾸려면 그 이유를 정책 문서에 먼저 적으십시오.");
            Assert.AreEqual("true", HybridGpuPreferencePolicy.MacTrueElementName,
                "plist의 boolean은 요소 이름 자체가 값입니다 — 이름이 바뀌면 문서가 깨집니다.");
            Assert.AreEqual("false", HybridGpuPreferencePolicy.MacFalseElementName);
            Assert.AreNotEqual(HybridGpuPreferencePolicy.MacTrueElementName,
                HybridGpuPreferencePolicy.MacFalseElementName,
                "두 값이 같습니다 — 판정이 아무것도 가르지 못합니다.");

            StringAssert.StartsWith("NS", HybridGpuPreferencePolicy.MacAutomaticGraphicsSwitchingKey,
                "Cocoa의 Info.plist 키가 아닙니다 — 이름을 잘못 적으면 macOS가 그냥 무시하고, " +
                "그 실패는 로그에도 남지 않습니다(가장 조용한 실패).");
        }

        // ====================================================================
        // 5. 소스 감사 — 읽기/쓰기 분리와 훅의 안전장치가 사라지지 않게
        // ====================================================================

        private static string PlatformRoot =>
            Path.Combine(Application.dataPath, "_Project", "Scripts", "Platform");

        private static string ReaderSourcePath =>
            Path.Combine(PlatformRoot, "PropertyListRootReader.cs");

        private static string HookSourcePath =>
            Path.Combine(Application.dataPath, "Editor", "MacHybridGpuInfoPlistPostprocessor.cs");

        /// <summary>
        /// 읽는 쪽(<see cref="PropertyListRootReader"/>)은 <b>디스크에 한 글자도 쓰지 않는다</b>.
        /// Windows 쪽 PE 파서에 건 것과 같은 규칙이다 — 그래야 테스트가 산출물 없이 로직 전량을
        /// 먹여 볼 수 있고, "빌드가 없어서 초록"이라는 거짓 통과가 생기지 않는다.
        /// </summary>
        [Test]
        public void 읽는쪽은_디스크_API를_갖지_않는다()
        {
            Assert.IsTrue(File.Exists(ReaderSourcePath),
                $"plist 파서 소스를 찾지 못했습니다({ReaderSourcePath}). 경로가 바뀌었다면 갱신하세요 — " +
                "그대로 두면 아래 단언이 전부 공허해집니다.");
            string reader = File.ReadAllText(ReaderSourcePath);

            // (1) 양성 대조 — 스캐너가 실제 내용을 보고 있는가.
            StringAssert.Contains(nameof(PropertyListRootReader.TryReadRootEntries), reader,
                "파서에서 진입점을 찾지 못했습니다 — 이 검사가 엉뚱한 파일을 읽고 있습니다.");

            // (2) 네거티브 컨트롤 — 니들을 일부러 끼워 넣으면 잡히는가.
            foreach (string forbidden in new[] { "File.WriteAllText(", "File.WriteAllBytes(", "File.ReadAllText(" })
            {
                Assert.IsTrue((reader + "\n// " + forbidden).Contains(forbidden),
                    $"네거티브 컨트롤 실패 — 니들 '{forbidden}'을 일부러 끼워 넣었는데도 검출되지 " +
                    "않았습니다. 아래 부재 단언은 신뢰할 수 없습니다.");

                // (3) 부재 단언.
                StringAssert.DoesNotContain(forbidden, reader,
                    $"plist 파서가 디스크 API('{forbidden}')를 갖고 있습니다. 읽기·계산(Platform/)과 " +
                    "쓰기(에디터 후처리)를 갈라 둔 이유는 테스트가 쓰기 없이 전량 검증할 수 있게 " +
                    "하기 위해서입니다.");
            }
        }

        /// <summary>
        /// 훅이 <b>중립 판정을 부르고</b>, <b>플랫폼 게이트를 갖고</b>, <b>요란하게 실패하고</b>,
        /// <b>서명을 되살리는지</b>를 소스에서 확인한다.
        ///
        /// <para>★ 서명 항목이 왜 여기 있는가: plist를 고치면 애드혹 서명이 깨진다(실측 확인:
        /// <c>invalid Info.plist (plist or signature have been modified)</c>). Apple Silicon에서
        /// 서명이 깨진 앱은 <b>아예 실행되지 않는다</b> — 즉 이 재서명 단계가 사라지면
        /// "GPU를 아끼려다 앱을 못 켜게 만드는" 사고가 난다.</para>
        /// </summary>
        [Test]
        public void 훅이_중립판정_플랫폼게이트_요란한실패_재서명을_모두_갖고_있다()
        {
            Assert.IsTrue(File.Exists(HookSourcePath),
                $"후처리 훅 소스를 찾지 못했습니다({HookSourcePath}). 훅이 사라지면 macOS 산출물은 " +
                "다시 선언 없이 출하되고, 듀얼 GPU Mac에서 외장 GPU가 깨어납니다.");
            string hook = File.ReadAllText(HookSourcePath);

            foreach (string required in new[]
                     {
                         nameof(HybridGpuPreferencePolicy),
                         nameof(HybridGpuPreferencePolicy.MacAutomaticGraphicsSwitchingKey),
                         nameof(PropertyListRootReader),
                         nameof(BuildTarget.StandaloneOSX),
                         nameof(BuildFailedException),
                         "--force",
                         "--verify",
                     })
            {
                StringAssert.Contains(required, hook,
                    $"훅 소스에 '{required}'가 없습니다. 훅이 중립 판정/파서/플랫폼 게이트/요란한 실패/" +
                    "재서명 중 하나를 잃었거나, 이 테스트가 엉뚱한 파일을 읽고 있습니다.");
            }

            // ★ 키 이름을 문자열로 베끼면 기준이 두 곳으로 갈라진다 — 중립 상수만 참조해야 한다.
            //   (네거티브 컨트롤을 먼저 세우고 부재를 단언한다: 부재 단언은 썩으면 조용히 초록이 된다.)
            string literal = HybridGpuPreferencePolicy.MacAutomaticGraphicsSwitchingKey;
            Assert.IsTrue((hook + "\n// \"" + literal + "\"").Contains("\"" + literal + "\""),
                "네거티브 컨트롤 실패 — 니들을 일부러 끼워 넣었는데도 검출되지 않았습니다.");
            StringAssert.DoesNotContain("\"" + literal + "\"", hook,
                $"훅이 키 이름을 문자열 리터럴('{literal}')로 베껴 두었습니다. 정책 상수를 " +
                $"{nameof(HybridGpuPreferencePolicy)}.{nameof(HybridGpuPreferencePolicy.MacAutomaticGraphicsSwitchingKey)}로 " +
                "참조하세요 — 베껴 두면 상수가 바뀌어도 훅은 옛 이름을 계속 씁니다.");

            // 영수증을 번들 '안'에 쓰면 그 파일 자체가 봉인 대상이 되어 서명이 다시 깨진다.
            StringAssert.Contains("GetDirectoryName", hook,
                "영수증을 산출물 '옆'(번들 밖)에 쓰는 흔적이 없습니다. 번들 안에 쓰면 그 파일이 " +
                "봉인 대상이 되어 방금 되살린 서명이 다시 깨집니다.");
        }

        /// <summary>
        /// 실제로 구워진 <c>.app</c>이 있으면 그 <c>Info.plist</c>를 <b>같은 파서로</b> 읽어 본다.
        /// 합성 표본이 진짜와 다른 모양이었다는 사고를 막는 <b>보조</b> 대조다.
        ///
        /// <para><b>이 검사에 판정을 걸지 않는다.</b> <c>Builds/</c>는 없을 수 있고, 없을 때
        /// "초록"이 되면 그것이 바로 이 저장소가 반복해 당한 조용한 거짓 통과다. 그래서 어느 쪽으로
        /// 갔는지를 <b>로그에 남기고</b>, 있을 때만 단언한다.</para>
        /// </summary>
        [Test]
        public void 보조_실제_산출물이_있으면_같은_파서로_읽힌다()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string plistPath = Path.Combine(projectRoot, "Builds", "macOS", "StickMate.app", "Contents", "Info.plist");

            if (!File.Exists(plistPath))
            {
                Debug.Log("[macGPU-plist] 보조 대조 미실행 — 산출물이 없습니다(" + plistPath + "). " +
                          "이 테스트의 초록은 '실제 plist를 읽었다'는 뜻이 아닙니다. " +
                          "판정의 근거는 위 합성 표본 검사들입니다.");
                Assert.Pass("산출물 없음 — 보조 대조를 건너뜁니다(위 합성 표본 검사가 판정의 근거입니다).");
                return;
            }

            string text = File.ReadAllText(plistPath);
            Assert.IsTrue(PropertyListRootReader.TryReadRootEntries(text, out List<PlistRootEntry> entries, out string error),
                "실제로 구워진 Info.plist를 파서가 읽지 못했습니다 — 합성 표본이 진짜와 다른 " +
                "모양이라는 뜻입니다. 사유: " + error);
            Assert.IsTrue(PropertyListRootReader.TryFind(entries, "CFBundleIdentifier", out _),
                "실제 Info.plist에서 CFBundleIdentifier를 찾지 못했습니다 — 파서가 루트를 잘못 잡았습니다.");

            bool declared = PropertyListRootReader.TryFind(entries,
                HybridGpuPreferencePolicy.MacAutomaticGraphicsSwitchingKey, out PlistRootEntry entry);
            Debug.Log($"[macGPU-plist] 보조 대조 실행 — 루트 항목 {entries.Count}개, " +
                      $"{HybridGpuPreferencePolicy.MacAutomaticGraphicsSwitchingKey} " +
                      (declared ? $"= <{entry.ValueElementName}/>" : "없음(이 산출물은 후처리 이전 빌드입니다)") +
                      $" — {plistPath}");
        }
    }
}
