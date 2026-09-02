using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StickMate.Core;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 픽스처 오염 근본 수정의 <b>증거</b> — 2026-09-02.
    ///
    /// ============================================================================
    /// 무엇이 진범이었나 (qa-regression A/B 통제 실험 → 이 라운드에서 실측 확인)
    /// ============================================================================
    /// 거짓 빨강 5건의 원인은 <b>둘</b>이었고, 둘 다 "격리한 줄 알았는데 격리가 아니었다"였다.
    /// <list type="number">
    ///   <item><b>실행 사이 이월</b> — <c>CharacterSaveStore.RedirectToTemporaryDirectoryForTesting</c>은
    ///     폴더가 없으면 만들 뿐 <b>비우지 않는다</b>. 그 폴더는 macOS에서
    ///     <c>/var/folders/.../T</c> 아래라 <b>재부팅에서만</b> 사라진다.
    ///     ★ 2026-09-02 실측: 재부팅 뒤 19:29에 새로 만들어진 폴더가 20:10에 이미 파일 3개로
    ///     다시 차 있었다. 즉 <b>"지금 초록"은 고쳐진 증거가 아니라 재부팅의 부작용</b>이었다.</item>
    ///   <item><b>실행 안 이월</b> — 다섯 픽스처가 저장 파일을 <c>OneTimeSetUp</c>에서 읽어 두고
    ///     <c>OneTimeTearDown</c>에서 <b>다시 썼다</b>. 경로가 격리된 뒤로 그 코드의 뜻은
    ///     "개발자 파일 보호"가 아니라 <b>"앞선 오염을 되살린다"</b>로 뒤집혔고, 다섯 곳에 같은
    ///     코드가 있어 오염이 스위트를 타고 <b>세탁</b>됐다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// 이 파일이 하는 일
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><b>양성 대조</b> — 오염 파일을 <b>일부러 심고</b> 정리기가 실제로 지우는지 본다.
    ///     심은 파일이 심어졌다는 것부터 단언한다(프로브가 죽은 채 초록이 되지 않게).</item>
    ///   <item><b>가드가 무는지</b> — 임시 캐시 <b>밖</b> 폴더를 주면 지우지 않고 실패하는지,
    ///     그리고 그 폴더의 파일이 <b>실제로 살아남는지</b>를 파일로 확인한다.
    ///     "안 지웠다"를 말이 아니라 디스크로 증명한다.</item>
    ///   <item><b>재발 방지</b> — 다섯 픽스처에 옛 백업/복원 형태가 다시 나타나면 빨개진다.</item>
    ///   <item><b>사본 드리프트</b> — 정리기가 EditMode/PlayMode 두 어셈블리에 각각 있어야 하므로
    ///     (어셈블리끼리 참조가 없다) 두 사본이 갈라지지 않았는지 소스로 대조한다.</item>
    /// </list>
    /// </summary>
    public sealed class SaveIsolationPurgeTests
    {
        private const string LogPrefix = "[격리정리]";

        private const string PlayModeFixtureRelative =
            "_Project/Scripts/Tests/PlayMode/GlobalPlayModeTestIsolation.cs";
        private const string EditModeFixtureRelative =
            "_Project/Scripts/Tests/EditMode/GlobalEditModeTestIsolation.cs";
        private const string PlayModeTestsRelative = "_Project/Scripts/Tests/PlayMode";

        /// <summary>진범으로 지목된 다섯 픽스처. 파일명은 <c>ls</c>로 확인한 그대로다
        /// (손으로 옮겨 적으면 대소문자 함정이 열린다 — docs/TEAM.md 12번째 거짓 통과 형태).</summary>
        private static readonly string[] PollutedFixtures =
        {
            "InfoGearDragTests.cs",
            "ActionCommandPopoverTests.cs",
            "TodoDemoDataPollutionTests.cs",
            "InfoWindowClippedHitTestTests.cs",
            "FullscreenPanelRetreatTests.cs",
        };

        // ============================================================================
        // ① 양성 대조 — 심은 오염이 실제로 지워진다
        // ============================================================================

        [Test]
        public void 양성대조_심어_놓은_오염_파일을_정리기가_실제로_지운다()
        {
            Assert.IsTrue(CharacterSaveStore.IsRedirectedForTesting,
                "이 스위트의 저장 경로가 격리되지 않았습니다 — GlobalEditModeTestIsolation이 돌지 않았습니다.");

            string dir = Path.GetDirectoryName(CharacterSaveStore.FilePath);
            Directory.CreateDirectory(dir);

            string planted = Path.Combine(dir, "purge_probe_pollution.json");
            File.WriteAllText(planted, "{\"probe\":\"이 파일은 정리기가 지워야 한다\"}");

            // ★ 프로브 생존 확인 — 심지 못했다면 아래 "지워졌다"는 아무 의미가 없다.
            Assert.IsTrue(File.Exists(planted),
                $"오염 파일을 심지 못했습니다({planted}) — 이 양성 대조는 무효입니다.");
            int before = Directory.GetFiles(dir).Length;
            Assert.GreaterOrEqual(before, 1, "심었는데 폴더가 비어 있습니다 — 프로브가 죽었습니다.");

            int removed = GlobalEditModeTestIsolation.PurgeIsolatedDirectories();

            Debug.Log($"{LogPrefix} 양성 대조 — 심기 전 {before}개 → 정리 {removed}개 삭제, " +
                $"남은 {Directory.GetFiles(dir).Length}개 ({dir})");

            Assert.IsFalse(File.Exists(planted),
                "정리기가 심어 놓은 오염 파일을 지우지 않았습니다 — 격리가 여전히 이월을 허용합니다.");
            Assert.AreEqual(0, Directory.GetFiles(dir).Length,
                "정리 뒤에도 저장 폴더에 파일이 남았습니다.");
            Assert.GreaterOrEqual(removed, before,
                $"정리기가 보고한 삭제 개수({removed})가 실제로 있던 개수({before})보다 적습니다.");
        }

        // ============================================================================
        // ② 가드가 실제로 무는가 — "안 지웠다"를 디스크로 증명한다
        // ============================================================================

        [Test]
        public void 가드_임시캐시_밖_폴더는_지우지_않고_실패한다()
        {
            // 시스템 임시 폴더 아래에 만든다 — Application.temporaryCachePath의 **바깥**이면서
            // 개발자의 실제 저장 폴더는 건드리지 않는 자리다.
            string outside = Path.Combine(Path.GetTempPath(), "StickMatePurgeGuardProbe");
            Directory.CreateDirectory(outside);
            string survivor = Path.Combine(outside, "must_survive.json");
            File.WriteAllText(survivor, "{\"probe\":\"가드가 물면 이 파일은 살아남는다\"}");

            try
            {
                // 전제 확인 — 이 경로가 정말 임시 캐시 밖인가(안이면 이 검사가 공허하다).
                string temp = Path.GetFullPath(Application.temporaryCachePath)
                    .TrimEnd(Path.DirectorySeparatorChar);
                Assert.IsFalse(
                    Path.GetFullPath(outside).StartsWith(temp + Path.DirectorySeparatorChar,
                        System.StringComparison.Ordinal),
                    $"프로브 경로가 임시 캐시 안입니다({outside}) — 이 가드 검사가 공허합니다.");

                Assert.Throws<AssertionException>(
                    () => GlobalEditModeTestIsolation.PurgeGuarded(true, survivor, "가드 프로브"),
                    "임시 캐시 밖 폴더인데 정리기가 실패하지 않았습니다 — 사정거리 가드가 없습니다.");

                Assert.IsTrue(File.Exists(survivor),
                    "가드가 예외는 던졌지만 파일은 이미 지워졌습니다 — 순서가 잘못돼 가드가 무의미합니다.");

                Debug.Log($"{LogPrefix} 가드 확인 — 임시 캐시 밖({outside})은 지우지 않고 실패했고 파일이 살아남았다.");
            }
            finally
            {
                if (Directory.Exists(outside)) Directory.Delete(outside, recursive: true);
            }
        }

        [Test]
        public void 가드_리디렉션되지_않았으면_지우지_않고_실패한다()
        {
            // 경로는 멀쩡한 임시 캐시 안이지만 "리디렉션 아님"만으로 멈춰야 한다.
            string inside = Path.Combine(Application.temporaryCachePath, "StickMatePurgeGuardProbe2");
            Directory.CreateDirectory(inside);
            string survivor = Path.Combine(inside, "must_survive.json");
            File.WriteAllText(survivor, "{\"probe\":\"리디렉션 아님\"}");

            try
            {
                Assert.Throws<AssertionException>(
                    () => GlobalEditModeTestIsolation.PurgeGuarded(false, survivor, "가드 프로브2"),
                    "리디렉션되지 않았는데 정리기가 실패하지 않았습니다.");
                Assert.IsTrue(File.Exists(survivor), "가드가 물기 전에 파일이 지워졌습니다.");

                // ★ 네거티브 컨트롤 — 같은 경로를 '리디렉션됨'으로 주면 **실제로 지운다**.
                //   이게 없으면 위 통과가 "원래 아무것도 못 지우는 경로였다"와 구분되지 않는다.
                int removed = GlobalEditModeTestIsolation.PurgeGuarded(true, survivor, "가드 프로브2");
                Assert.AreEqual(1, removed, "가드를 통과했는데도 지우지 않았습니다.");
                Assert.IsFalse(File.Exists(survivor), "가드를 통과했는데 파일이 남았습니다.");

                Debug.Log($"{LogPrefix} 네거티브 컨트롤 — 같은 경로가 리디렉션 플래그 하나로 " +
                    "보존→삭제로 갈렸다(가드가 실제 판정자다).");
            }
            finally
            {
                if (Directory.Exists(inside)) Directory.Delete(inside, recursive: true);
            }
        }

        // ============================================================================
        // ③ 재발 방지 — 옛 백업/복원 형태가 다시 나타나면 빨개진다
        // ============================================================================

        /// <summary>
        /// 다섯 픽스처가 저장 파일을 <b>다시 쓰지</b> 않는지 소스로 확인한다.
        ///
        /// <para>★ 부재 단언이라 <b>썩으면 조용히 초록</b>이 된다(CLAUDE.md: 부재 단언 니들 61건이
        /// 그 형태다). 그래서 같은 검사 안에서 <b>대조</b>를 못박는다 —
        /// (a) 다섯 파일을 실제로 읽었는가(바이트 수 하한),
        /// (b) 찾는 형태가 <b>과거에 실재했는가</b>를 git 히스토리가 아니라 <b>현재 트리의 다른
        ///     증거</b>로 확인한다: 새 코드가 그 자리에 남긴 <c>ClearIsolatedSaveFile</c>이 있어야 한다.
        /// 둘 중 하나라도 없으면 이 "0건"은 무효다.</para>
        /// </summary>
        [Test]
        public void 재발방지_다섯_픽스처는_저장파일을_다시_쓰지_않는다()
        {
            var offenders = new List<string>();
            int scannedBytes = 0;

            foreach (string file in PollutedFixtures)
            {
                string path = Path.Combine(Application.dataPath, PlayModeTestsRelative, file);
                Assert.IsTrue(File.Exists(path),
                    $"픽스처를 찾지 못했습니다: {path} — 이름이 바뀌었다면 이 목록도 갱신하십시오. " +
                    "그 전까지 이 검사는 대상 없이 돌게 됩니다.");

                string raw = File.ReadAllText(path);
                scannedBytes += raw.Length;

                // ★ 2026-09-02 러너 실측으로 고침 — <b>주석을 코드로 착각했다</b>.
                //   1회차에서 이 검사가 다섯 파일 전부를 위반으로 신고했는데, 실제로 걸린 것은
                //   그 파일들에 내가 새로 쓴 <b>설명 주석</b> 안의 `<c>_hadFile == true</c>`였다.
                //   (a) 주석을 지우지 않고 훑었고, (b) 니들이 `\s*=`라 `==`의 첫 글자에도 물었다.
                //   둘 다 고친다 — 주석 줄을 걷어내고, `=(?!=)`로 대입만 본다.
                //   이 저장소가 반복해 온 "니들이 엉뚱한 것을 물어 거짓 빨강"의 표본이다.
                string text = StripCommentLines(raw);

                // (b) 대조 — 새 정리 코드가 그 자리에 실제로 있는가.
                Assert.IsTrue(text.Contains("ClearIsolatedSaveFile"),
                    $"{file}에 새 정리 코드(ClearIsolatedSaveFile)가 없습니다 — 이 파일에서 " +
                    "'옛 형태 0건'은 '고쳐졌다'가 아니라 '검사가 엉뚱한 파일을 보고 있다'일 수 있습니다.");

                // 옛 형태: 저장 파일 경로에 되쓰기.
                if (Regex.IsMatch(text, @"File\.WriteAllText\s*\(\s*path\s*,"))
                {
                    offenders.Add($"{file} — File.WriteAllText(path, ...) 되쓰기가 남아 있습니다");
                }
                if (Regex.IsMatch(text, @"\b_hadFile\b\s*=(?!=)") || Regex.IsMatch(text, @"\b_backup\b\s*=(?!=)"))
                {
                    offenders.Add($"{file} — 백업 필드(_hadFile/_backup) 대입이 되살아났습니다");
                }
            }

            Debug.Log($"{LogPrefix} 재발 방지 — 픽스처 {PollutedFixtures.Length}개 / " +
                $"{scannedBytes}바이트 검사, 위반 {offenders.Count}건");

            // (a) 대조 — 정말로 읽었는가. 0바이트면 "0건 = 깨끗"이 "아무것도 안 봤다"다.
            Assert.Greater(scannedBytes, 10000,
                $"다섯 픽스처를 합쳐 {scannedBytes}바이트만 읽었습니다 — 파일을 제대로 읽지 못했습니다.");

            Assert.IsEmpty(offenders, string.Join("\n", offenders));
        }

        // ============================================================================
        // ④ 사본 드리프트 — 두 어셈블리의 정리기가 같은가
        // ============================================================================

        /// <summary>EditMode/PlayMode 테스트 어셈블리는 서로를 참조하지 않으므로 정리기가
        /// <b>두 벌</b> 존재할 수밖에 없다. 두 벌이면 언젠가 갈라진다 — 그래서 소스로 대조한다
        /// (<c>Tests/EditMode/DuplicatedPoseConstantParityTests.cs</c>와 같은 관례).</summary>
        [Test]
        public void 정리기_두_사본이_한_글자도_다르지_않다()
        {
            string play = ExtractPurgeSource(PlayModeFixtureRelative);
            string edit = ExtractPurgeSource(EditModeFixtureRelative);

            Debug.Log($"{LogPrefix} 사본 대조 — PlayMode {play.Length}자 / EditMode {edit.Length}자");

            Assert.Greater(play.Length, 500,
                "PlayMode 정리기 소스를 제대로 뽑지 못했습니다 — 추출기가 깨졌습니다(대조 무효).");
            Assert.AreEqual(play, edit,
                "두 어셈블리의 정리기 사본이 갈라졌습니다. 한쪽만 고치면 그쪽 스위트만 격리되고 " +
                "다른 쪽은 조용히 이월을 계속합니다.");
        }

        /// <summary>주석 <b>줄</b>만 지운다(줄 안의 꼬리 주석은 건드리지 않는다 — 문자열 속 "//"를
        /// 잘못 자르지 않기 위해서다). <c>Tests/EditMode/UiGlyphExactnessAuditTests.cs</c>가 같은 이유로
        /// 같은 처리를 한다 — 이 저장소의 설명 주석이 <b>옛 코드를 그대로 인용</b>하고 있어, 이 단계가
        /// 없으면 감사가 주석을 코드로 착각한다(2026-09-02 러너 1회차에서 실제로 그렇게 됐다).</summary>
        private static string StripCommentLines(string source)
        {
            string[] lines = source.Split('\n');
            var kept = new List<string>(lines.Length);
            foreach (string line in lines)
            {
                string t = line.TrimStart();
                if (t.StartsWith("//", System.StringComparison.Ordinal)) continue;
                if (t.StartsWith("*", System.StringComparison.Ordinal)) continue;
                if (t.StartsWith("/*", System.StringComparison.Ordinal)) continue;
                kept.Add(line);
            }
            return string.Join("\n", kept);
        }

        /// <summary>★ 위 <see cref="StripCommentLines"/>가 <b>실제로 무는지</b> 확인한다.
        /// 주석 제거가 조용히 아무 일도 안 하면 위 감사가 다시 주석을 코드로 읽는다.</summary>
        [Test]
        public void 주석제거기가_주석을_실제로_걷어낸다()
        {
            const string Sample = "코드1\n            // _hadFile == true 라고 적힌 주석\n코드2\n            /// <c>_backup</c>\n";
            string stripped = StripCommentLines(Sample);

            Assert.IsTrue(Sample.Contains("_hadFile"), "표본에 니들이 없습니다 — 이 대조가 무의미합니다.");
            Assert.IsFalse(stripped.Contains("_hadFile"),
                "주석 줄이 제거되지 않았습니다 — 위 감사가 주석을 코드로 착각합니다.");
            Assert.IsFalse(stripped.Contains("_backup"), "문서 주석(///) 줄이 제거되지 않았습니다.");
            Assert.IsTrue(stripped.Contains("코드1") && stripped.Contains("코드2"),
                "코드 줄까지 지웠습니다 — 제거기가 너무 넓습니다.");

            // 니들 자체의 정확도 — `==`(비교)에 물면 안 되고 `=`(대입)에는 물어야 한다.
            Assert.IsFalse(Regex.IsMatch("if (_hadFile == true)", @"\b_hadFile\b\s*=(?!=)"),
                "니들이 비교(==)에 물었습니다 — 1회차 거짓 빨강의 원인입니다.");
            Assert.IsTrue(Regex.IsMatch("_hadFile = File.Exists(path);", @"\b_hadFile\b\s*=(?!=)"),
                "니들이 진짜 대입에 물지 않습니다 — 이 감사는 아무것도 못 잡습니다.");
        }

        /// <summary><c>PurgeIsolatedDirectories</c>부터 파일 끝(클래스 닫는 괄호 직전)까지를 뽑는다.
        /// 못 뽑으면 <b>실패</b>한다 — 빈 문자열끼리 비교해 초록이 되는 것이 이 저장소가
        /// 반복해 온 실패다.</summary>
        private static string ExtractPurgeSource(string relative)
        {
            string path = Path.Combine(Application.dataPath, relative);
            Assert.IsTrue(File.Exists(path), $"픽스처 소스를 찾지 못했습니다: {path}");

            string text = File.ReadAllText(path);
            int start = text.IndexOf("    public static int PurgeIsolatedDirectories()",
                System.StringComparison.Ordinal);
            Assert.Greater(start, 0,
                $"{Path.GetFileName(path)}에서 PurgeIsolatedDirectories를 찾지 못했습니다 — " +
                "이름이 바뀌었다면 이 대조도 함께 갱신하십시오.");

            return text.Substring(start).TrimEnd('\n', '\r', ' ', '}');
        }
    }
}
