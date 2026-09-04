#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace StickMate.Platform
{
    /// <summary>PE export 테이블에서 뽑아 낸 심볼 하나.</summary>
    public readonly struct PeExportedSymbol
    {
        /// <summary>export 이름 테이블에 적힌 이름(ASCII).</summary>
        public readonly string Name;

        /// <summary>서수(1부터). <c>Base + 함수배열 인덱스</c>.</summary>
        public readonly int Ordinal;

        /// <summary>이 심볼이 가리키는 RVA. 데이터 export면 <b>변수 자신의 주소</b>다.</summary>
        public readonly uint Rva;

        /// <summary>파일 안에서의 바이트 위치. forwarder거나 파일에 없으면 -1.</summary>
        public readonly int FileOffset;

        /// <summary><see cref="Rva"/>를 담은 섹션 이름(<c>.data</c> 등). 없으면 null.</summary>
        public readonly string SectionName;

        /// <summary>다른 DLL로 넘기는 forwarder인가. 그렇다면 값이 아니라 문자열을 가리킨다.</summary>
        public readonly bool IsForwarder;

        public PeExportedSymbol(string name, int ordinal, uint rva, int fileOffset,
                                string sectionName, bool isForwarder)
        {
            Name = name;
            Ordinal = ordinal;
            Rva = rva;
            FileOffset = fileOffset;
            SectionName = sectionName;
            IsForwarder = isForwarder;
        }

        public override string ToString() =>
            $"{Name}(ord {Ordinal}, RVA 0x{Rva:x}, 파일 0x{FileOffset:x}, {SectionName ?? "-"}" +
            (IsForwarder ? ", forwarder)" : ")");
    }

    /// <summary>
    /// PE(Portable Executable) 파일의 <b>export 디렉터리를 읽기만 하는</b> 순수 파서.
    ///
    /// ========================================================================
    /// ★ 왜 이 클래스가 있는가 — 하드코딩 오프셋을 쓰지 않기 위해서다
    /// ========================================================================
    /// Windows 빌드 후처리는 exe 안의 값 DWORD 두 개를 바꾼다. 그 위치를 상수로 박아 두면
    /// (실측한 값이 있긴 하다) Unity 마이너 버전이 올라 템플릿이 밀리는 순간 <b>엉뚱한 바이트를
    /// 덮어쓴다 — 이 접근의 진짜 사고 지점이다</b>. 그래서 후처리는 위치를 <b>알지 못한 채</b>
    /// 시작하고, 이 파서가 <b>심볼 이름으로</b> 찾아 준 위치만 쓴다.
    /// 근거와 실측: docs/verify/WINDOWS_DGPU_REPORT.md.
    ///
    /// <para><b>이 클래스는 한 바이트도 쓰지 않는다.</b> 입력은 <c>byte[]</c> 하나이고 출력은
    /// 위치와 이름뿐이다. 쓰기는 에디터 후처리(<c>Assets/Editor/WindowsHybridGpuExportPostprocessor.cs</c>)만
    /// 한다 — 읽는 쪽과 쓰는 쪽을 갈라 두면 테스트가 쓰기 없이 파싱만 전량 검증할 수 있다.</para>
    ///
    /// <para><b>왜 <c>#if UNITY_EDITOR</c> 인가</b>: 빌드 타임 기계이지 런타임 기능이 아니다.
    /// 출하 플레이어에 PE 파서가 들어갈 이유가 없다. 에디터 어셈블리가 아니라
    /// <c>Platform/</c>(런타임 어셈블리)에 두는 이유는 <b>EditMode 테스트가 참조할 수 있어야</b>
    /// 하기 때문이다 — asmdef 어셈블리는 <c>Assembly-CSharp-Editor</c>를 참조할 수 없다.</para>
    ///
    /// <para><b>실패는 예외가 아니라 <c>false</c> + 사유 문자열</b>로 돌려준다. 호출자가 그 사유를
    /// 그대로 빌드 실패 메시지에 실을 수 있게 하기 위해서다. "왜 못 찾았는지 모르겠지만 실패"는
    /// 이 저장소에서 금지된 형태다.</para>
    /// </summary>
    public static class PortableExecutableExportReader
    {
        /// <summary>export 개수가 이보다 크면 파싱이 어긋난 것으로 본다(무한 루프 방지).</summary>
        public const int SymbolCountSanityLimit = 65536;

        private const ushort Pe32Magic = 0x010B;
        private const ushort Pe32PlusMagic = 0x020B;
        private const int CoffHeaderSize = 20;
        private const int SectionHeaderSize = 40;
        private const int ExportDirectorySize = 40;
        private const int MaxSymbolNameLength = 512;

        private readonly struct Section
        {
            public readonly string Name;
            public readonly uint VirtualSize;
            public readonly uint VirtualAddress;
            public readonly uint RawSize;
            public readonly uint RawPointer;

            public Section(string name, uint virtualSize, uint virtualAddress, uint rawSize, uint rawPointer)
            {
                Name = name;
                VirtualSize = virtualSize;
                VirtualAddress = virtualAddress;
                RawSize = rawSize;
                RawPointer = rawPointer;
            }
        }

        /// <summary>
        /// export 테이블 전량을 <b>이름 테이블 순서 그대로</b> 읽는다.
        /// </summary>
        /// <param name="image">PE 파일 전체 바이트.</param>
        /// <param name="symbols">성공 시 심볼 목록(이름 테이블 순서). 실패 시 null.</param>
        /// <param name="error">실패 사유. 성공 시 null.</param>
        /// <returns>성공하면 true.</returns>
        public static bool TryReadExports(byte[] image, out List<PeExportedSymbol> symbols, out string error)
        {
            symbols = null;
            error = null;

            if (image == null) { error = "이미지가 null이다."; return false; }
            if (image.Length < 0x40) { error = $"파일이 너무 작다({image.Length} bytes) — DOS 헤더도 담지 못한다."; return false; }
            if (image[0] != (byte)'M' || image[1] != (byte)'Z')
            {
                error = "MZ 서명이 없다 — PE 파일이 아니다.";
                return false;
            }

            if (!TryU32(image, 0x3C, out uint lfanewRaw, out error)) return false;
            long lfanew = lfanewRaw;
            if (lfanew <= 0 || lfanew + 4 + CoffHeaderSize > image.Length)
            {
                error = $"e_lfanew(0x{lfanew:x})가 파일 범위를 벗어난다(파일 크기 0x{image.Length:x}).";
                return false;
            }

            int peSig = (int)lfanew;
            if (image[peSig] != (byte)'P' || image[peSig + 1] != (byte)'E'
                || image[peSig + 2] != 0 || image[peSig + 3] != 0)
            {
                error = $"PE\\0\\0 서명이 없다(e_lfanew=0x{lfanew:x}).";
                return false;
            }

            int coff = peSig + 4;
            if (!TryU16(image, coff + 2, out ushort sectionCount, out error)) return false;
            if (!TryU16(image, coff + 16, out ushort optionalHeaderSize, out error)) return false;

            int optional = coff + CoffHeaderSize;
            if (!TryU16(image, optional, out ushort magic, out error)) return false;

            int dataDirectoryOffset;
            int rvaCountOffset;
            if (magic == Pe32PlusMagic) { dataDirectoryOffset = optional + 112; rvaCountOffset = optional + 108; }
            else if (magic == Pe32Magic) { dataDirectoryOffset = optional + 96; rvaCountOffset = optional + 92; }
            else
            {
                error = $"알 수 없는 optional header magic 0x{magic:x3} — PE32(0x{Pe32Magic:x3})도 " +
                        $"PE32+(0x{Pe32PlusMagic:x3})도 아니다.";
                return false;
            }

            if (!TryU32(image, rvaCountOffset, out uint rvaCount, out error)) return false;
            if (rvaCount < 1)
            {
                error = "데이터 디렉터리가 0개다 — export 테이블이 있을 수 없다.";
                return false;
            }

            if (!TryU32(image, dataDirectoryOffset, out uint exportRva, out error)) return false;
            if (!TryU32(image, dataDirectoryOffset + 4, out uint exportSize, out error)) return false;
            if (exportRva == 0)
            {
                error = "export 데이터 디렉터리가 비어 있다 — 이 exe에는 export가 하나도 없다.";
                return false;
            }

            int sectionTable = optional + optionalHeaderSize;
            if (sectionCount == 0)
            {
                error = "섹션이 0개다 — RVA를 파일 오프셋으로 옮길 수 없다.";
                return false;
            }
            if (sectionTable + sectionCount * SectionHeaderSize > image.Length)
            {
                error = $"섹션 표({sectionCount}개)가 파일 범위를 벗어난다.";
                return false;
            }

            var sections = new Section[sectionCount];
            for (int i = 0; i < sectionCount; i++)
            {
                int s = sectionTable + i * SectionHeaderSize;
                sections[i] = new Section(
                    ReadSectionName(image, s),
                    ReadU32Unchecked(image, s + 8),
                    ReadU32Unchecked(image, s + 12),
                    ReadU32Unchecked(image, s + 16),
                    ReadU32Unchecked(image, s + 20));
            }

            if (!TryRvaToOffset(sections, image.Length, exportRva, out int exportOffset, out _, out error))
            {
                error = "export 디렉터리를 찾을 수 없다 — " + error;
                return false;
            }
            if (exportOffset + ExportDirectorySize > image.Length)
            {
                error = $"export 디렉터리(0x{exportOffset:x})가 파일 끝을 넘는다.";
                return false;
            }

            uint functionCount = ReadU32Unchecked(image, exportOffset + 20);
            uint nameCount = ReadU32Unchecked(image, exportOffset + 24);
            uint functionsRva = ReadU32Unchecked(image, exportOffset + 28);
            uint namesRva = ReadU32Unchecked(image, exportOffset + 32);
            uint ordinalsRva = ReadU32Unchecked(image, exportOffset + 36);
            uint ordinalBase = ReadU32Unchecked(image, exportOffset + 16);

            if (functionCount > SymbolCountSanityLimit || nameCount > SymbolCountSanityLimit)
            {
                error = $"export 개수가 비상식적이다(함수 {functionCount}, 이름 {nameCount}, " +
                        $"상한 {SymbolCountSanityLimit}) — 파싱이 어긋났다.";
                return false;
            }

            if (!TryRvaToOffset(sections, image.Length, namesRva, out int namesOffset, out _, out error))
            { error = "이름 배열을 찾을 수 없다 — " + error; return false; }
            if (!TryRvaToOffset(sections, image.Length, ordinalsRva, out int ordinalsOffset, out _, out error))
            { error = "서수 배열을 찾을 수 없다 — " + error; return false; }
            if (!TryRvaToOffset(sections, image.Length, functionsRva, out int functionsOffset, out _, out error))
            { error = "함수 배열을 찾을 수 없다 — " + error; return false; }

            var result = new List<PeExportedSymbol>((int)nameCount);
            for (int i = 0; i < nameCount; i++)
            {
                if (!TryU32(image, namesOffset + 4 * i, out uint nameRva, out error)) return false;
                if (!TryRvaToOffset(sections, image.Length, nameRva, out int nameOffset, out _, out error))
                { error = $"이름 #{i}의 문자열을 찾을 수 없다 — " + error; return false; }
                if (!TryReadAscii(image, nameOffset, out string name, out error)) return false;

                if (!TryU16(image, ordinalsOffset + 2 * i, out ushort ordinalIndex, out error)) return false;
                if (ordinalIndex >= functionCount)
                {
                    error = $"'{name}'의 서수 인덱스({ordinalIndex})가 함수 개수({functionCount})를 벗어난다.";
                    return false;
                }

                if (!TryU32(image, functionsOffset + 4 * ordinalIndex, out uint funcRva, out error)) return false;

                bool forwarder = funcRva >= exportRva && funcRva < exportRva + exportSize;
                int fileOffset = -1;
                string sectionName = null;
                if (!forwarder)
                {
                    if (!TryRvaToOffset(sections, image.Length, funcRva, out fileOffset, out sectionName, out error))
                    {
                        error = $"'{name}'(RVA 0x{funcRva:x})의 파일 위치를 계산할 수 없다 — " + error;
                        return false;
                    }
                }

                result.Add(new PeExportedSymbol(name, (int)(ordinalBase + ordinalIndex), funcRva,
                                                fileOffset, sectionName, forwarder));
            }

            symbols = result;
            return true;
        }

        /// <summary>이름으로 심볼 하나를 찾는다(대소문자 구분 — export 이름은 대소문자를 가린다).</summary>
        public static bool TryFind(IReadOnlyList<PeExportedSymbol> symbols, string name,
                                   out PeExportedSymbol found)
        {
            found = default;
            if (symbols == null || name == null) return false;
            for (int i = 0; i < symbols.Count; i++)
            {
                if (string.Equals(symbols[i].Name, name, StringComparison.Ordinal))
                {
                    found = symbols[i];
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// export 이름 테이블이 사전순 오름차순인가. PE 로더 규약이며,
        /// 깨지면 <c>GetProcAddress</c>의 이진 탐색이 <b>같은 테이블의 다른 심볼까지</b> 못 찾는다.
        /// 값만 바꾸는 우리 후처리는 이 순서를 건드리지 않으므로, 패치 전후로 항상 true여야 한다.
        /// </summary>
        public static bool AreExportNamesAscending(IReadOnlyList<PeExportedSymbol> symbols)
        {
            if (symbols == null) return false;
            for (int i = 1; i < symbols.Count; i++)
            {
                if (string.CompareOrdinal(symbols[i - 1].Name, symbols[i].Name) >= 0) return false;
            }
            return true;
        }

        /// <summary>파일 오프셋에서 리틀엔디안 32비트를 읽는다.</summary>
        public static bool TryReadUInt32(byte[] image, int offset, out uint value, out string error)
        {
            value = 0;
            if (image == null) { error = "이미지가 null이다."; return false; }
            return TryU32(image, offset, out value, out error);
        }

        // --------------------------------------------------------------------
        // 내부
        // --------------------------------------------------------------------

        private static bool TryRvaToOffset(Section[] sections, int imageLength, uint rva,
                                           out int offset, out string sectionName, out string error)
        {
            offset = -1;
            sectionName = null;
            error = null;

            for (int i = 0; i < sections.Length; i++)
            {
                Section s = sections[i];
                long span = Math.Max(s.VirtualSize, s.RawSize);
                if (rva < s.VirtualAddress || rva >= s.VirtualAddress + span) continue;

                long delta = rva - s.VirtualAddress;
                if (delta >= s.RawSize)
                {
                    error = $"RVA 0x{rva:x}는 섹션 {s.Name}의 초기화되지 않은 영역이라 파일에 실체가 없다.";
                    return false;
                }

                long candidate = s.RawPointer + delta;
                if (candidate < 0 || candidate >= imageLength)
                {
                    error = $"RVA 0x{rva:x}가 파일 밖(0x{candidate:x})을 가리킨다(파일 크기 0x{imageLength:x}).";
                    return false;
                }

                offset = (int)candidate;
                sectionName = s.Name;
                return true;
            }

            error = $"RVA 0x{rva:x}를 담는 섹션이 없다(섹션 {sections.Length}개).";
            return false;
        }

        private static string ReadSectionName(byte[] image, int offset)
        {
            int end = offset;
            while (end < offset + 8 && image[end] != 0) end++;
            return System.Text.Encoding.ASCII.GetString(image, offset, end - offset);
        }

        private static bool TryReadAscii(byte[] image, int offset, out string text, out string error)
        {
            text = null;
            error = null;
            int end = offset;
            while (end < image.Length && end - offset < MaxSymbolNameLength && image[end] != 0) end++;
            if (end >= image.Length)
            {
                error = $"0x{offset:x}의 문자열이 NUL 없이 파일 끝에 닿았다.";
                return false;
            }
            if (end - offset >= MaxSymbolNameLength)
            {
                error = $"0x{offset:x}의 문자열이 {MaxSymbolNameLength}바이트를 넘는다 — 파싱이 어긋났다.";
                return false;
            }
            text = System.Text.Encoding.ASCII.GetString(image, offset, end - offset);
            return true;
        }

        private static bool TryU16(byte[] image, int offset, out ushort value, out string error)
        {
            value = 0;
            if (offset < 0 || offset + 2 > image.Length)
            {
                error = $"범위 밖 읽기(16비트) at 0x{offset:x} — 파일 크기 0x{image.Length:x}.";
                return false;
            }
            value = (ushort)(image[offset] | (image[offset + 1] << 8));
            error = null;
            return true;
        }

        private static bool TryU32(byte[] image, int offset, out uint value, out string error)
        {
            value = 0;
            if (offset < 0 || offset + 4 > image.Length)
            {
                error = $"범위 밖 읽기(32비트) at 0x{offset:x} — 파일 크기 0x{image.Length:x}.";
                return false;
            }
            value = ReadU32Unchecked(image, offset);
            error = null;
            return true;
        }

        private static uint ReadU32Unchecked(byte[] image, int offset) =>
            (uint)(image[offset] | (image[offset + 1] << 8) | (image[offset + 2] << 16) | (image[offset + 3] << 24));
    }
}
#endif
