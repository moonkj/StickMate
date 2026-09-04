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
    /// Windows 빌드 후처리(하이브리드 GPU export 값 1 -> 0)의 <b>PE 파싱 로직</b> 회귀 잠금.
    ///
    /// ========================================================================
    /// ★ 이 테스트가 실제로 지키는 것
    /// ========================================================================
    /// 후처리의 진짜 사고 지점은 "값을 못 바꿨다"가 아니라 <b>"엉뚱한 바이트를 덮어썼다"</b>이다.
    /// Unity 마이너 버전이 올라 플레이어 템플릿의 배치가 밀리면 하드코딩 오프셋은 조용히 다른 곳을
    /// 가리킨다. 그래서 아래 <see cref="양성_섹션이_밀려도_이름으로_찾은_위치가_따라_움직인다"/>가
    /// <b>같은 심볼을 서로 다른 파일 위치에 놓은 두 PE</b>를 먹여서, 파서가 위치를 <b>기억</b>하는지
    /// <b>계산</b>하는지를 갈라 낸다. 오프셋을 상수로 박은 구현은 이 테스트에서 반드시 실패한다.
    ///
    /// <para><b>실제 exe에 의존하지 않는다.</b> <c>Builds/</c>는 있을 수도 없을 수도 있고, 있어도
    /// 언젠가 내용이 바뀐다. 테스트가 산출물에 기대면 "빌드가 없어서 초록"이라는 가장 조용한
    /// 거짓 통과가 생긴다. 그래서 PE를 <b>바이트 단위로 직접 조립</b>해 먹인다.</para>
    ///
    /// <para><b>실기 검증과는 다른 것을 잰다.</b> 이 테스트는 "파서가 옳은 위치를 찾는가"만 본다.
    /// "값 0이 실제로 내장 GPU로 귀결되는가"는 Windows 실기에서만 확인할 수 있고 이 머신에는
    /// Windows가 없다 — 그 갭은 <c>Tests/EditMode/PlatformParityAuditTests.cs</c>가 러너에 띄운다.</para>
    /// </summary>
    public sealed class HybridGpuExportPatchTests
    {
        // ====================================================================
        // 합성 PE 조립기 — 실제 산출물에 기대지 않기 위한 장치
        // ====================================================================

        private sealed class SyntheticPe
        {
            public byte[] Bytes;
            public int NvValueOffset;
            public int AmdValueOffset;
            public int NvNameOffset;
        }

        private const int HeaderSpan = 0x200;
        private const int RDataPointer = 0x200;
        private const uint RDataVirtualAddress = 0x1000;
        private const uint DataVirtualAddress = 0x2000;
        private const int SectionSpan = 0x200;

        /// <summary>
        /// 최소 PE32+ 이미지를 조립한다. <paramref name="dataRawPointer"/>로 <c>.data</c> 섹션의
        /// 파일 위치를 <b>옮길 수 있다</b> — 그것이 "오프셋을 기억하는 구현"을 잡는 지렛대다.
        /// </summary>
        private static SyntheticPe BuildSyntheticPe(uint nvValue, uint amdValue, int dataRawPointer = 0x400)
        {
            int length = dataRawPointer + SectionSpan;
            var b = new byte[length];

            // --- DOS 헤더 ---
            b[0] = (byte)'M';
            b[1] = (byte)'Z';
            WriteU32(b, 0x3C, 0x80);

            // --- PE 서명 + COFF 헤더 ---
            b[0x80] = (byte)'P';
            b[0x81] = (byte)'E';
            int coff = 0x84;
            WriteU16(b, coff + 0, 0x8664);      // Machine = x64
            WriteU16(b, coff + 2, 2);           // NumberOfSections
            WriteU16(b, coff + 16, 240);        // SizeOfOptionalHeader

            // --- Optional 헤더 (PE32+) ---
            int optional = coff + 20;           // 0x98
            WriteU16(b, optional, 0x20B);
            WriteU32(b, optional + 108, 16);                    // NumberOfRvaAndSizes
            WriteU32(b, optional + 112, RDataVirtualAddress);   // DataDirectory[0].VirtualAddress
            WriteU32(b, optional + 116, 0x200);                 // DataDirectory[0].Size

            // --- 섹션 표 ---
            int sectionTable = optional + 240;  // 0x188
            WriteSection(b, sectionTable, ".rdata", RDataVirtualAddress, RDataPointer);
            WriteSection(b, sectionTable + 40, ".data", DataVirtualAddress, dataRawPointer);

            // --- .rdata: export 디렉터리 ---
            const uint functionsRva = RDataVirtualAddress + 0x40;
            const uint namesRva = RDataVirtualAddress + 0x50;
            const uint ordinalsRva = RDataVirtualAddress + 0x60;
            const uint amdNameRva = RDataVirtualAddress + 0x80;
            const uint nvNameRva = RDataVirtualAddress + 0xC0;
            const uint dllNameRva = RDataVirtualAddress + 0x100;

            int ed = RDataPointer;
            WriteU32(b, ed + 12, dllNameRva);
            WriteU32(b, ed + 16, 1);            // Base
            WriteU32(b, ed + 20, 2);            // NumberOfFunctions
            WriteU32(b, ed + 24, 2);            // NumberOfNames
            WriteU32(b, ed + 28, functionsRva);
            WriteU32(b, ed + 32, namesRva);
            WriteU32(b, ed + 36, ordinalsRva);

            // 함수 배열 — 인덱스 0 = AMD, 인덱스 1 = NV
            WriteU32(b, RvaToFile(functionsRva), DataVirtualAddress + 4);
            WriteU32(b, RvaToFile(functionsRva) + 4, DataVirtualAddress + 0);

            // 이름 배열 — 사전순 오름차순(PE 로더 규약)
            WriteU32(b, RvaToFile(namesRva), amdNameRva);
            WriteU32(b, RvaToFile(namesRva) + 4, nvNameRva);

            WriteU16(b, RvaToFile(ordinalsRva), 0);
            WriteU16(b, RvaToFile(ordinalsRva) + 2, 1);

            WriteAscii(b, RvaToFile(amdNameRva), HybridGpuPreferencePolicy.AmdExportSymbol);
            WriteAscii(b, RvaToFile(nvNameRva), HybridGpuPreferencePolicy.NvidiaExportSymbol);
            WriteAscii(b, RvaToFile(dllNameRva), "SynthPlayer.exe");

            // --- .data: 값 두 개 ---
            WriteU32(b, dataRawPointer + 0, nvValue);
            WriteU32(b, dataRawPointer + 4, amdValue);

            return new SyntheticPe
            {
                Bytes = b,
                NvValueOffset = dataRawPointer + 0,
                AmdValueOffset = dataRawPointer + 4,
                NvNameOffset = RvaToFile(nvNameRva),
            };
        }

        private static int RvaToFile(uint rva) => (int)(rva - RDataVirtualAddress) + RDataPointer;

        private static void WriteSection(byte[] b, int at, string name, uint virtualAddress, int rawPointer)
        {
            for (int i = 0; i < name.Length && i < 8; i++) b[at + i] = (byte)name[i];
            WriteU32(b, at + 8, SectionSpan);          // VirtualSize
            WriteU32(b, at + 12, virtualAddress);
            WriteU32(b, at + 16, SectionSpan);         // SizeOfRawData
            WriteU32(b, at + 20, (uint)rawPointer);
        }

        private static void WriteU16(byte[] b, int at, int value)
        {
            b[at] = (byte)(value & 0xFF);
            b[at + 1] = (byte)((value >> 8) & 0xFF);
        }

        private static void WriteU32(byte[] b, int at, uint value)
        {
            b[at] = (byte)(value & 0xFF);
            b[at + 1] = (byte)((value >> 8) & 0xFF);
            b[at + 2] = (byte)((value >> 16) & 0xFF);
            b[at + 3] = (byte)((value >> 24) & 0xFF);
        }

        private static void WriteAscii(byte[] b, int at, string text)
        {
            for (int i = 0; i < text.Length; i++) b[at + i] = (byte)text[i];
            b[at + text.Length] = 0;
        }

        // ====================================================================
        // 1. 양성 — 조립기 자신이 먼저 옳아야 아래 음성들이 뜻을 가진다
        // ====================================================================

        [Test]
        public void 양성_합성PE에서_두_심볼을_이름으로_찾고_값을_읽는다()
        {
            SyntheticPe pe = BuildSyntheticPe(HybridGpuPreferencePolicy.UnityTemplateValue,
                                              HybridGpuPreferencePolicy.UnityTemplateValue);

            Assert.IsTrue(PortableExecutableExportReader.TryReadExports(pe.Bytes, out List<PeExportedSymbol> symbols, out string error),
                "합성 PE 파싱이 실패했습니다 — 조립기가 틀렸다면 아래 음성 대조가 전부 무의미해집니다. 사유: " + error);
            Assert.AreEqual(2, symbols.Count, "합성 PE의 export 개수가 다릅니다.");

            Assert.IsTrue(PortableExecutableExportReader.TryFind(symbols, HybridGpuPreferencePolicy.NvidiaExportSymbol,
                out PeExportedSymbol nv), "NVIDIA 심볼을 이름으로 찾지 못했습니다.");
            Assert.IsTrue(PortableExecutableExportReader.TryFind(symbols, HybridGpuPreferencePolicy.AmdExportSymbol,
                out PeExportedSymbol amd), "AMD 심볼을 이름으로 찾지 못했습니다.");

            Assert.AreEqual(pe.NvValueOffset, nv.FileOffset, "NVIDIA 심볼의 파일 오프셋이 다릅니다.");
            Assert.AreEqual(pe.AmdValueOffset, amd.FileOffset, "AMD 심볼의 파일 오프셋이 다릅니다.");
            Assert.AreEqual(".data", nv.SectionName, "값이 .data 섹션에 있어야 합니다.");
            Assert.IsFalse(nv.IsForwarder, "값 export가 forwarder로 잘못 판정됐습니다.");

            Assert.IsTrue(PortableExecutableExportReader.TryReadUInt32(pe.Bytes, nv.FileOffset, out uint nvValue, out _));
            Assert.IsTrue(PortableExecutableExportReader.TryReadUInt32(pe.Bytes, amd.FileOffset, out uint amdValue, out _));
            Assert.AreEqual(HybridGpuPreferencePolicy.UnityTemplateValue, nvValue);
            Assert.AreEqual(HybridGpuPreferencePolicy.UnityTemplateValue, amdValue);

            Assert.IsTrue(PortableExecutableExportReader.AreExportNamesAscending(symbols),
                "합성 PE의 export 이름 테이블이 오름차순이 아닙니다 — 조립기가 규약을 어겼습니다.");
        }

        /// <summary>
        /// ★ <b>이 파일에서 가장 중요한 테스트.</b> 같은 심볼을 서로 다른 파일 위치에 놓은 PE 두 개를
        /// 먹인다. 파서가 위치를 계산한다면 두 답이 <b>달라야</b> 하고, 각각 정확해야 한다.
        /// 오프셋을 상수로 기억하는 구현은 여기서 반드시 깨진다.
        /// </summary>
        [Test]
        public void 양성_섹션이_밀려도_이름으로_찾은_위치가_따라_움직인다()
        {
            SyntheticPe near = BuildSyntheticPe(1, 1, 0x400);
            SyntheticPe far = BuildSyntheticPe(1, 1, 0x600);

            Assert.AreNotEqual(near.NvValueOffset, far.NvValueOffset,
                "두 합성 PE가 같은 위치를 쓰고 있습니다 — 이 테스트가 아무것도 구분하지 못합니다.");

            foreach (SyntheticPe pe in new[] { near, far })
            {
                Assert.IsTrue(PortableExecutableExportReader.TryReadExports(pe.Bytes, out List<PeExportedSymbol> symbols, out string error), error);
                Assert.IsTrue(PortableExecutableExportReader.TryFind(symbols, HybridGpuPreferencePolicy.NvidiaExportSymbol, out PeExportedSymbol nv));
                Assert.IsTrue(PortableExecutableExportReader.TryFind(symbols, HybridGpuPreferencePolicy.AmdExportSymbol, out PeExportedSymbol amd));

                Assert.AreEqual(pe.NvValueOffset, nv.FileOffset,
                    "섹션이 옮겨졌는데 파서가 옛 위치를 돌려줬습니다 — 하드코딩 오프셋이 남아 있습니다.");
                Assert.AreEqual(pe.AmdValueOffset, amd.FileOffset,
                    "섹션이 옮겨졌는데 파서가 옛 위치를 돌려줬습니다 — 하드코딩 오프셋이 남아 있습니다.");
            }
        }

        // ====================================================================
        // 2. 음성 — 조작된 바이트에서 실제로 실패하는가
        // ====================================================================

        [Test]
        public void 음성_MZ_서명이_깨지면_사유와_함께_실패한다()
        {
            SyntheticPe pe = BuildSyntheticPe(1, 1);
            pe.Bytes[0] = (byte)'X';

            Assert.IsFalse(PortableExecutableExportReader.TryReadExports(pe.Bytes, out List<PeExportedSymbol> symbols, out string error),
                "MZ가 아닌 파일을 PE로 읽었습니다 — 이 파서는 아무 바이너리나 통과시킵니다.");
            Assert.IsNull(symbols, "실패했는데 심볼 목록이 채워졌습니다.");
            Assert.IsNotEmpty(error ?? string.Empty, "실패 사유가 비어 있습니다 — 조용한 실패입니다.");
        }

        [Test]
        public void 음성_PE_서명이_깨지면_실패한다()
        {
            SyntheticPe pe = BuildSyntheticPe(1, 1);
            pe.Bytes[0x80] = (byte)'X';

            Assert.IsFalse(PortableExecutableExportReader.TryReadExports(pe.Bytes, out _, out string error));
            Assert.IsNotEmpty(error ?? string.Empty);
        }

        [Test]
        public void 음성_export_데이터_디렉터리가_비면_실패한다()
        {
            SyntheticPe pe = BuildSyntheticPe(1, 1);
            WriteU32(pe.Bytes, 0x98 + 112, 0);   // DataDirectory[0].VirtualAddress = 0

            Assert.IsFalse(PortableExecutableExportReader.TryReadExports(pe.Bytes, out _, out string error),
                "export가 없는 PE를 성공으로 읽었습니다.");
            Assert.IsNotEmpty(error ?? string.Empty);
        }

        [Test]
        public void 음성_서수_인덱스가_함수_개수를_넘으면_실패한다()
        {
            SyntheticPe pe = BuildSyntheticPe(1, 1);
            WriteU16(pe.Bytes, RvaToFile(RDataVirtualAddress + 0x60) + 2, 9999);

            Assert.IsFalse(PortableExecutableExportReader.TryReadExports(pe.Bytes, out _, out string error),
                "범위를 벗어난 서수를 그대로 따라갔습니다 — 임의의 메모리 위치를 가리킬 수 있습니다.");
            Assert.IsNotEmpty(error ?? string.Empty);
        }

        [Test]
        public void 음성_값이_파일_끝을_넘으면_읽기가_실패한다()
        {
            SyntheticPe pe = BuildSyntheticPe(1, 1, 0x400);

            // (가) 잘린 이미지는 **파싱 단계에서** 실패해야 한다. 값 하나가 파일 밖인데도 성공을
            //      돌려주면, 호출자는 존재하지 않는 바이트에 쓰기를 시도하게 된다.
            byte[] truncated = new byte[pe.AmdValueOffset];   // AMD 값의 첫 바이트조차 파일 밖이다
            Array.Copy(pe.Bytes, truncated, truncated.Length);
            Assert.IsFalse(PortableExecutableExportReader.TryReadExports(truncated, out List<PeExportedSymbol> cut, out string cutError),
                "값이 파일 밖으로 나간 이미지를 성공으로 읽었습니다.");
            Assert.IsNull(cut, "실패했는데 심볼 목록이 채워졌습니다.");
            Assert.IsNotEmpty(cutError ?? string.Empty, "실패 사유가 비어 있습니다 — 조용한 실패입니다.");

            // (나) 값 읽기 자체의 경계 — 대조를 함께 둔다(둘 다 실패하면 검사가 무의미하다).
            Assert.IsTrue(PortableExecutableExportReader.TryReadUInt32(pe.Bytes, pe.Bytes.Length - 4, out _, out _),
                "파일 안의 마지막 4바이트를 읽지 못했습니다 — 경계 검사가 지나치게 빡빡합니다.");
            Assert.IsFalse(PortableExecutableExportReader.TryReadUInt32(pe.Bytes, pe.Bytes.Length - 2, out _, out string edgeError),
                "파일 끝을 넘는 4바이트를 읽어 냈습니다.");
            Assert.IsNotEmpty(edgeError ?? string.Empty);
        }

        /// <summary>
        /// ★ 심볼 이름을 바꾸면 <b>찾지 못해야</b> 한다. 이것이 "이름으로 찾는다"의 증명이다 —
        /// 오프셋으로 찾는 구현은 이름이 무엇이든 같은 자리를 읽으므로 이 테스트가 초록으로 남는다.
        /// </summary>
        [Test]
        public void 음성_심볼_이름이_바뀌면_이름_조회가_실패한다()
        {
            SyntheticPe pe = BuildSyntheticPe(1, 1);
            pe.Bytes[pe.NvNameOffset] = (byte)'X';   // NvOptimusEnablement -> XvOptimusEnablement

            Assert.IsTrue(PortableExecutableExportReader.TryReadExports(pe.Bytes, out List<PeExportedSymbol> symbols, out string error),
                "이름만 바뀐 PE는 여전히 파싱되어야 합니다. 사유: " + error);
            Assert.IsFalse(PortableExecutableExportReader.TryFind(symbols, HybridGpuPreferencePolicy.NvidiaExportSymbol, out _),
                "이름이 바뀌었는데도 찾아냈습니다 — 조회가 이름이 아니라 위치에 의존하고 있습니다.");

            // 대조: 같은 이미지에서 AMD 쪽은 그대로 찾혀야 한다(스캐너가 통째로 눈이 먼 것이 아님을 못박는다).
            Assert.IsTrue(PortableExecutableExportReader.TryFind(symbols, HybridGpuPreferencePolicy.AmdExportSymbol, out _),
                "AMD 심볼까지 못 찾았습니다 — 조회가 통째로 고장 났습니다.");
        }

        [Test]
        public void 이름_오름차순_판정이_양성과_음성을_가른다()
        {
            SyntheticPe pe = BuildSyntheticPe(1, 1);
            Assert.IsTrue(PortableExecutableExportReader.TryReadExports(pe.Bytes, out List<PeExportedSymbol> symbols, out _));
            Assert.IsTrue(PortableExecutableExportReader.AreExportNamesAscending(symbols));

            var reversed = new List<PeExportedSymbol>(symbols);
            reversed.Reverse();
            Assert.IsFalse(PortableExecutableExportReader.AreExportNamesAscending(reversed),
                "내림차순 목록을 오름차순이라고 답했습니다 — 이 검사는 아무것도 잡지 못합니다.");
        }

        // ====================================================================
        // 3. 정책 — 판정이 세 갈래로 실제로 갈라지는가
        // ====================================================================

        [Test]
        public void 정책_템플릿값은_중립화_중립값은_멱등_그밖은_거부다()
        {
            Assert.AreEqual(HybridGpuPreferencePolicy.Verdict.NeedsNeutralize,
                HybridGpuPreferencePolicy.Classify(HybridGpuPreferencePolicy.UnityTemplateValue, out string a),
                "템플릿 기본값을 고치지 않겠다고 판정했습니다.");
            Assert.IsNotEmpty(a ?? string.Empty);

            Assert.AreEqual(HybridGpuPreferencePolicy.Verdict.AlreadyNeutral,
                HybridGpuPreferencePolicy.Classify(HybridGpuPreferencePolicy.DesiredValue, out string b),
                "이미 중립값인데 다시 쓰겠다고 판정했습니다 — 멱등이 깨집니다.");
            Assert.IsNotEmpty(b ?? string.Empty);

            foreach (uint stray in new uint[] { 2u, 3u, 0x10001u, uint.MaxValue })
            {
                Assert.AreEqual(HybridGpuPreferencePolicy.Verdict.Unexpected,
                    HybridGpuPreferencePolicy.Classify(stray, out string c),
                    $"기대 밖의 값 {stray}를 조용히 통과시켰습니다 — 템플릿이 바뀐 exe에 바이트를 씁니다.");
                Assert.IsNotEmpty(c ?? string.Empty);
            }
        }

        [Test]
        public void 정책_기대값과_템플릿값은_서로_달라야_한다()
        {
            Assert.AreNotEqual(HybridGpuPreferencePolicy.UnityTemplateValue, HybridGpuPreferencePolicy.DesiredValue,
                "고치기 전 값과 고친 뒤 값이 같습니다 — 후처리가 아무 일도 하지 않으면서 통과합니다.");
            Assert.AreEqual(HybridGpuPreferencePolicy.IgnoreHintValue, HybridGpuPreferencePolicy.DesiredValue,
                "원하는 값은 NVIDIA 문서가 정의한 '힌트 무시'여야 합니다.");
        }

        [Test]
        public void 정책_Windows_심볼_목록이_두_상수와_일치한다()
        {
            IReadOnlyList<string> symbols = HybridGpuPreferencePolicy.WindowsExportSymbols;
            Assert.AreEqual(2, symbols.Count, "패치 대상 심볼이 2개가 아닙니다.");
            CollectionAssert.Contains(symbols, HybridGpuPreferencePolicy.NvidiaExportSymbol);
            CollectionAssert.Contains(symbols, HybridGpuPreferencePolicy.AmdExportSymbol);
        }

        // ====================================================================
        // 4. 훅 소스 감사 — 하드코딩 오프셋으로 되돌아가지 못하게
        // ====================================================================

        private static string HookSourcePath =>
            Path.Combine(Application.dataPath, "Editor", "WindowsHybridGpuExportPostprocessor.cs");

        private static bool ContainsIgnoreCase(string haystack, string needle) =>
            haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        /// ★ <b>부재 단언은 썩으면 조용히 초록이 된다</b>(CLAUDE.md). 그래서 같은 스캐너로
        /// (1) 반드시 있어야 하는 것을 먼저 찾아 보고(스캐너가 눈이 멀지 않았음을 증명),
        /// (2) 같은 니들을 일부러 끼워 넣은 사본에서 실제로 검출되는지까지 확인한 뒤
        /// (3) 진짜 소스에는 없다고 단언한다.
        /// </summary>
        [Test]
        public void 훅_소스가_하드코딩된_파일오프셋을_쓰지_않는다()
        {
            Assert.IsTrue(File.Exists(HookSourcePath),
                $"후처리 훅 소스를 찾지 못했습니다({HookSourcePath}). 파일이 옮겨졌다면 이 경로를 갱신하세요 — " +
                "그대로 두면 아래 단언이 전부 공허해집니다.");
            string source = File.ReadAllText(HookSourcePath);

            // (1) 양성 대조 — 스캐너가 실제 내용을 보고 있는가.
            foreach (string required in new[]
                     {
                         nameof(PortableExecutableExportReader),
                         nameof(HybridGpuPreferencePolicy),
                         nameof(BuildTarget.StandaloneWindows64),
                         nameof(BuildFailedException),
                     })
            {
                StringAssert.Contains(required, source,
                    $"훅 소스에 '{required}'가 없습니다. 훅이 이름 기반 파싱/정책/플랫폼 게이트/요란한 실패 중 " +
                    "하나를 잃었거나, 이 테스트가 엉뚱한 파일을 읽고 있습니다.");
            }

            // (2)·(3) 부재 단언 — 실측된 템플릿 오프셋을 코드에 박으면 잡는다.
            //     (이 두 값은 '지금 Unity 6000.0.82f1 템플릿'의 실측치일 뿐이며, 버전이 오르면 밀린다.
            //      그래서 코드가 이 숫자를 알아서는 안 된다.)
            foreach (string forbidden in new[] { "0x17a00", "0x17a04" })
            {
                Assert.IsTrue(ContainsIgnoreCase(source + "\n// " + forbidden, forbidden),
                    "네거티브 컨트롤 실패 — 니들을 일부러 끼워 넣었는데도 검출되지 않았습니다. " +
                    "아래 부재 단언은 신뢰할 수 없습니다.");

                Assert.IsFalse(ContainsIgnoreCase(source, forbidden),
                    $"훅 소스에 하드코딩된 파일 오프셋 '{forbidden}'이 있습니다. Unity 마이너 버전이 올라 " +
                    "템플릿 배치가 밀리면 엉뚱한 바이트를 덮어씁니다 — 이 접근의 진짜 사고 지점입니다. " +
                    "심볼 이름으로 찾은 위치만 쓰세요.");
            }
        }
    }
}
