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
    /// ★★★ 시스템 오디오 감지 <b>네이티브 두 구현</b>의 정적 감사 — 2026-09-03.
    ///
    /// ============================================================================
    /// 왜 리플렉션이 아니라 소스 스캔인가
    /// ============================================================================
    /// 두 파일 모두 <c>#if UNITY_STANDALONE_OSX</c> / <c>#if UNITY_STANDALONE_WIN</c> 안이라
    /// <b>활성 빌드 타깃 반대편은 타입이 아예 존재하지 않는다</b>(CLAUDE.md 「활성 빌드 타깃 규칙」).
    /// 리플렉션으로는 영원히 절반을 못 본다.
    ///
    /// ============================================================================
    /// ★★ 이 파일이 대신 서 주는 것 — <b>Windows는 이 머신에서 실행되지 않는다</b>
    /// ============================================================================
    /// COM 식별자(CLSID/IID)는 <b>한 글자만 틀려도 컴파일은 되고 실행만 조용히 실패한다.</b>
    /// 이 개발 머신에는 Windows가 없으므로 그 실패를 실행으로 잡을 수 없다.
    /// 그래서 값 자체를 <b>1차 출처(Microsoft Learn / mmdeviceapi.h · endpointvolume.h)</b>와
    /// 대조하고, 서로 다른지·파싱되는지를 잠근다.
    /// (같은 처방이 이미 <c>ITaskbarList</c>의 CLSID/IID에 쓰이고 있다 — 그 둘은 <b>끝 한 자리만</b>
    ///  다르다.)
    ///
    /// <para>macOS 쪽 FourCC 상수는 <b>테스트가 문자 4개로부터 다시 계산해</b> 대조한다 —
    /// 소스의 숫자를 베껴 오는 것이 아니라 <b>독립적으로 유도</b>하는 형태다.</para>
    /// </summary>
    public sealed class SystemAudioActivityProbeAuditTests
    {
        private static string PlatformRoot =>
            Path.Combine(Application.dataPath, "_Project", "Scripts", "Platform");

        private static string MacProbePath =>
            Path.Combine(PlatformRoot, "MacOS", "MacSystemAudioActivityProbe.cs");

        private static string WindowsProbePath =>
            Path.Combine(PlatformRoot, "Windows", "WindowsSystemAudioActivityProbe.cs");

        private const string ContractName = "ISystemAudioActivityProbe";

        // ====================================================================
        // ① 양 플랫폼이 같은 계약을 실제로 달았는가
        // ====================================================================

        [Test]
        public void 두_플랫폼_구현이_같은_중립_계약을_단다()
        {
            foreach (string path in new[] { MacProbePath, WindowsProbePath })
            {
                string src = Read(path);
                StringAssert.Contains(": " + ContractName, src,
                    $"{Path.GetFileName(path)}가 {ContractName}을 기반 목록에 달지 않았습니다 — " +
                    "계약이 갈라지면 중립 정책이 한쪽에서만 돕니다(FullscreenSuspendPolicy 사고).");

                StringAssert.Contains("TryReadIsAudioPlaying", src,
                    $"{Path.GetFileName(path)}에 사실 조회 구현이 없습니다.");
            }
        }

        /// <summary>★ 두 구현 모두 <b>플랫폼 가드 안</b>에 있는가. 빠지면 반대쪽 타깃에서
        /// <c>DllImport</c>/COM 선언이 통째로 컴파일되어 크로스 컴파일이 깨진다.</summary>
        [Test]
        public void 두_구현_모두_플랫폼_가드_안에_있다()
        {
            StringAssert.StartsWith("#if UNITY_STANDALONE_OSX", Read(MacProbePath).TrimStart(),
                "macOS 프로브가 #if UNITY_STANDALONE_OSX로 감싸이지 않았습니다.");
            StringAssert.StartsWith("#if UNITY_STANDALONE_WIN", Read(WindowsProbePath).TrimStart(),
                "Windows 프로브가 #if UNITY_STANDALONE_WIN으로 감싸이지 않았습니다.");
        }

        // ====================================================================
        // ② macOS — FourCC 셀렉터를 테스트가 다시 계산해 대조한다
        // ====================================================================

        /// <summary>
        /// ★ 소스의 숫자를 <b>베끼지 않는다</b>. 문자 4개(<c>'dOut'</c> 등)에서 값을 <b>유도</b>해
        /// 대조하므로, 누가 상수를 잘못 고치면 여기서 걸린다. CoreAudio는 셀렉터가 틀려도
        /// <c>OSStatus</c>만 다르게 돌려줄 뿐 컴파일은 통과한다.
        /// </summary>
        [Test]
        public void macOS_FourCC_셀렉터가_문자에서_유도한_값과_같다()
        {
            string src = Read(MacProbePath);

            var expected = new Dictionary<string, string>
            {
                ["SelectorDefaultOutputDevice"] = "dOut",      // kAudioHardwarePropertyDefaultOutputDevice
                ["SelectorDeviceIsRunningSomewhere"] = "gone", // kAudioDevicePropertyDeviceIsRunningSomewhere
                ["ScopeGlobal"] = "glob",                      // kAudioObjectPropertyScopeGlobal
            };

            foreach (KeyValuePair<string, string> pair in expected)
            {
                Match m = Regex.Match(src, @"\b" + pair.Key + @"\s*=\s*0x([0-9A-Fa-f]{8})\b");
                Assert.IsTrue(m.Success,
                    $"{pair.Key}의 16진 상수를 소스에서 찾지 못했습니다 — 이름이나 표기가 바뀌었다면 " +
                    "이 감사도 함께 고치십시오(못 찾은 채로 통과하면 아무것도 확인하지 않은 것입니다).");

                uint actual = Convert.ToUInt32(m.Groups[1].Value, 16);
                uint derived = FourCharCode(pair.Value);
                Assert.AreEqual(derived, actual,
                    $"★ {pair.Key}가 '{pair.Value}'의 FourCC와 다릅니다 " +
                    $"(소스=0x{actual:X8}, 유도=0x{derived:X8}). CoreAudio는 셀렉터가 틀려도 " +
                    "컴파일되고 OSStatus만 조용히 달라집니다 — 실기에서 «영원히 무음»으로 보입니다.");
            }
        }

        /// <summary>★ 스코프는 <b>Global</b>이고 <b>Input이 아니다</b>. 이 한 글자가 macOS 첫 실행의
        /// 마이크 동의창 유무를 가른다. (부재 단언 + 존재 대조를 같은 테스트에 둔다.)</summary>
        [Test]
        public void macOS_프로브는_출력_스코프만_읽는다()
        {
            string code = StripLineComments(Read(MacProbePath));

            StringAssert.Contains("ScopeGlobal", code,
                "양성 대조 실패 — 주석 제거 뒤 소스에서 ScopeGlobal조차 못 찾았습니다. " +
                "이 스캐너는 눈이 멀어 있고 아래 '없음' 판정은 무효입니다.");

            Assert.AreEqual(-1, code.IndexOf("ScopeInput", StringComparison.Ordinal),
                "★ macOS 프로브가 입력 스코프를 엽니다 — 첫 실행에 마이크 동의창이 뜹니다. " +
                "바탕화면 캐릭터가 마이크를 요구하는 것은 되돌릴 수 없는 신뢰 사고입니다.");
        }

        // ====================================================================
        // ③ Windows — COM 식별자를 1차 출처와 대조
        // ====================================================================

        /// <summary>
        /// ★ 값은 <b>Microsoft 헤더의 1차 출처</b>에서 왔다(우리 소스에서 복사한 것이 아니다).
        /// 이 머신에서는 실행으로 확인할 수 없으므로 이 대조가 유일한 방어선이다.
        /// </summary>
        [Test]
        public void Windows_COM_식별자가_1차_출처와_같고_서로_다르다()
        {
            string src = Read(WindowsProbePath);

            var expected = new Dictionary<string, string>
            {
                ["DeviceEnumeratorClsid"] = "BCDE0395-E52F-467C-8E3D-C4579291692E",   // CLSID_MMDeviceEnumerator
                ["DeviceEnumeratorIid"] = "A95664D2-9614-4F35-A746-DE8DB63617E6",     // IID_IMMDeviceEnumerator
                ["DeviceIid"] = "D666063F-1587-4E43-81F1-B948E807363F",               // IID_IMMDevice
                ["AudioMeterInformationIid"] = "C02216F6-8C67-4B5B-9D00-D008E73E0064",// IID_IAudioMeterInformation
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
        /// ★★ <b>오디오 스트림을 열지 않는다.</b> <c>IAudioClient</c>를 여는 순간 세션이 생기고,
        /// 그 다음 줄에 루프백 플래그를 얹고 싶어진다 — 그 길의 끝이 「도청」 프로필이다.
        /// <para>캡처 니들 7종은 <c>PlatformParityAuditTests</c>가 잠근다. 여기서는 그 목록에
        /// <b>없는</b> 한 겹을 더 얹는다(스트림 클라이언트 자체).</para>
        /// </summary>
        [Test]
        public void Windows_프로브는_오디오_스트림을_열지_않는다()
        {
            string code = StripLineComments(Read(WindowsProbePath));

            StringAssert.Contains("IAudioMeterInformation", code,
                "양성 대조 실패 — 주석 제거 뒤 소스에서 IAudioMeterInformation조차 못 찾았습니다. " +
                "이 스캐너는 눈이 멀어 있고 아래 '없음' 판정은 무효입니다.");
            StringAssert.Contains("GetPeakValue", code, "양성 대조 실패 — 피크 미터 호출이 없습니다.");

            Assert.AreEqual(-1, code.IndexOf("IAudioClient", StringComparison.Ordinal),
                "★ Windows 프로브가 IAudioClient를 엽니다 — 우리는 **미터 조회**만 하기로 했습니다. " +
                "스트림을 여는 순간 백신 프로필이 달라지고, 루프백까지는 한 줄 거리입니다 " +
                "(ENTITLEMENT_CONTRACT S-3, 사용자 실기 AhnLab V3).");
        }

        /// <summary>★ 레벨 → 불리언 변환(임계값)이 <b>Windows 구현체 안에</b> 있는가.
        /// 중립 정책으로 새 나가면 macOS 구현체가 레벨을 지어내야 한다(I-13).</summary>
        [Test]
        public void 레벨_임계값은_Windows_구현체가_소유한다()
        {
            string winCode = StripLineComments(Read(WindowsProbePath));
            StringAssert.Contains("PeakThreshold", winCode,
                "Windows 구현체에 레벨 임계값이 없습니다 — 그러면 변환이 어딘가 다른 곳에 있습니다.");

            string policy = StripLineComments(
                Read(Path.Combine(PlatformRoot, "AudioReactiveDancePolicy.cs")));
            Assert.AreEqual(-1, policy.IndexOf("Peak", StringComparison.Ordinal),
                "★ 중립 정책이 피크 레벨을 압니다 — 그 순간 macOS 구현체는 레벨을 **지어내야** 하고 " +
                "게이트가 무엇을 재는지 말할 수 없게 됩니다(I-13).");

            string macCode = StripLineComments(Read(MacProbePath));
            Assert.AreEqual(-1, macCode.IndexOf("Threshold", StringComparison.Ordinal),
                "★ macOS 구현체에 임계값이 생겼습니다 — macOS 신호는 불리언이라 임계값 개념이 " +
                "성립하지 않습니다. 값을 지어내고 있지 않은지 확인하십시오.");
        }

        // ====================================================================
        // 도구
        // ====================================================================

        private static string Read(string path)
        {
            Assert.IsTrue(File.Exists(path), $"소스를 찾지 못했습니다: {path}");
            string src = File.ReadAllText(path).Replace("\r\n", "\n");
            Assert.Greater(src.Length, 200, $"{Path.GetFileName(path)}를 {src.Length}자밖에 못 읽었습니다 — " +
                "빈 문자열 위의 검사는 아무것도 재지 않으면서 초록이 됩니다.");
            return src;
        }

        /// <summary>줄 주석(<c>//</c>, <c>///</c>)을 걷어낸다 — 이 파일의 <b>부재 단언</b>은 코드에만
        /// 걸려야 하고, 결함을 정직하게 <b>설명한</b> 주석에 걸리면 안 된다.</summary>
        private static string StripLineComments(string source)
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

        /// <summary>'d','O','u','t' → 0x644F7574. 소스의 숫자와 <b>독립적으로</b> 계산한다.</summary>
        private static uint FourCharCode(string four)
        {
            Assert.AreEqual(4, four.Length, "FourCC는 정확히 4글자여야 합니다: " + four);
            uint v = 0;
            foreach (char c in four)
            {
                Assert.Less((int)c, 128, "FourCC에 ASCII 밖 문자가 있습니다: " + four);
                v = (v << 8) | (uint)c;
            }
            return v;
        }
    }
}
