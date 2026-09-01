using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 배타 표면 규칙의 <b>구조 감사</b>(2026-09-01, 사용자 신고 "케릭터창도 겹쳐서보이는 문제있고").
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 것은 "설정창 한 줄"이 아니라 <b>그 한 줄을 빠뜨리게 만든 구조</b>다
    /// ============================================================================
    /// 사고의 실제 모양:
    ///  · <c>CharacterInfoWindow.CloseOverlappingSurfaces()</c>가 닫을 대상을 <b>손으로 나열</b>했고
    ///    그 목록에 <c>SettingsWindow</c>가 없었다.
    ///  · 게다가 그 함수에는 <c>if (_menu != null) { ...; return; }</c> <b>조기 반환</b>이 있어서,
    ///    부채꼴이 있는 정식 조립에서는 목록 뒷부분이 <b>한 번도 실행되지 않았다.</b>
    ///    → "아래에 설정창 한 줄 추가"라는 가장 자연스러운 수정은 화면에서 아무 효과가 없었을 것이다.
    ///  · 반대 방향(<c>SettingsWindow</c>)은 정상 동작했고, 그쪽 주석은 정보창 쪽을 "같은 규약"이라고
    ///    <b>선언</b>하고 있었다 — <b>선언만 있고 구현이 없었다.</b>
    ///
    /// 그래서 잠그는 방식도 "목록에 설정창이 있는지"가 아니다. 아래 세 가지를 잠근다:
    ///  1. <see cref="EveryClosableWindowLikeSurfaceIsRegistered"/> —
    ///     닫을 수 있는 창/팝오버를 새로 만들면서 <see cref="IExclusiveSurface"/>를 구현하지 않으면 실패.
    ///     <b>목록에 추가하는 것을 잊을 자리 자체가 없다</b>(리플렉션이 타입을 직접 찾는다).
    ///  2. <see cref="NoSurfaceKeepsItsOwnHandWrittenCloseList"/> —
    ///     어느 표면이든 배타 정리를 <b>스스로 나열</b>하면 실패. 집행 지점은 하나뿐이어야 한다.
    ///  3. <see cref="TheEnforcementPointHasNoEarlyReturn"/> —
    ///     집행 지점의 순회 루프에 조기 반환/break이 생기면 실패. 이 사고의 직접 원인이다.
    ///
    /// ★ 프로덕션 상수를 숫자로 베끼지 않는다(CLAUDE.md): 표면 개수/이름을 여기 적지 않고
    ///   전부 리플렉션과 소스 스캔으로 <b>지금의 코드</b>에서 유도한다.
    /// </summary>
    public sealed class ExclusiveSurfaceRegistryTests
    {
        private const string LogPrefix = "[배타표면-감사]";

        private static string InteractionRoot =>
            Path.Combine(Application.dataPath, "_Project", "Scripts", "Interaction");

        private static Assembly RuntimeAssembly => typeof(ExclusiveSurfaces).Assembly;

        // ============================================================================
        // 1. 새 표면이 등록을 빠뜨리면 실패한다
        // ============================================================================

        /// <summary>
        /// "닫을 수 있고, 열렸는지 물어볼 수 있는" <see cref="MonoBehaviour"/>는 정의상 배타 표면
        /// 후보다 — 화면을 점유하는 UI가 아니면 그런 API가 있을 이유가 없다. 그런 타입이
        /// <see cref="IExclusiveSurface"/>를 구현하지 않았다면, 그건 <b>다른 창을 열어도 안 닫히는
        /// 표면</b>이라는 뜻이고 이번 신고와 정확히 같은 버그다.
        /// </summary>
        [Test]
        public void EveryClosableWindowLikeSurfaceIsRegistered()
        {
            List<Type> missing = new List<Type>();
            List<Type> found = new List<Type>();

            foreach (Type t in RuntimeAssembly.GetTypes())
            {
                if (t.IsAbstract || !typeof(MonoBehaviour).IsAssignableFrom(t)) continue;
                if (t.Namespace != typeof(ExclusiveSurfaces).Namespace) continue;
                if (!LooksLikeAClosableSurface(t)) continue;

                found.Add(t);
                if (!typeof(IExclusiveSurface).IsAssignableFrom(t)) missing.Add(t);
            }

            Assert.IsNotEmpty(found,
                $"{LogPrefix} '닫을 수 있는 표면'을 한 개도 못 찾았습니다 — 탐지 규칙이 " +
                "프로덕션과 어긋나 이 감사가 아무것도 지키지 않고 있습니다(초록 거짓말).");

            Assert.IsEmpty(missing,
                $"{LogPrefix} 다음 타입은 Close(string) + IsOpen을 공개하면서 {nameof(IExclusiveSurface)}를 " +
                $"구현하지 않았습니다: {string.Join(", ", missing.Select(m => m.Name))}\n" +
                "→ 다른 배타 표면이 열려도 이 표면은 화면에 남습니다(사용자 신고 \"케릭터창도 겹쳐서보이는\"과 " +
                "같은 결함). 클래스 선언에 인터페이스를 붙이고 명시적 구현 두 줄을 추가하세요.");
        }

        /// <summary>부채꼴처럼 Close(string)이 아닌 이름을 쓰는 표면도 놓치지 않는다 —
        /// 이번 사고에서 조기 반환의 주인공이 바로 그 타입이었다.</summary>
        [Test]
        public void TheRadialFanIsRegisteredEvenThoughItsCloserHasADifferentName()
        {
            Assert.IsTrue(typeof(IExclusiveSurface).IsAssignableFrom(typeof(GearRadialMenuWidget)),
                $"{LogPrefix} {nameof(GearRadialMenuWidget)}가 {nameof(IExclusiveSurface)}를 구현하지 " +
                "않았습니다. 이 타입은 Close(string)이 아니라 ForceCloseAll(string)을 쓰기 때문에 " +
                "1번 감사의 그물에 걸리지 않습니다 — 그래서 여기서 이름으로 못 박습니다.");
        }

        private static bool LooksLikeAClosableSurface(Type t)
        {
            MethodInfo close = t.GetMethod("Close", BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(string) }, null);
            if (close == null || close.ReturnType != typeof(void)) return false;

            PropertyInfo isOpen = t.GetProperty("IsOpen", BindingFlags.Public | BindingFlags.Instance);
            return isOpen != null && isOpen.PropertyType == typeof(bool);
        }

        // ============================================================================
        // 2. 집행 지점은 하나뿐이다 — 어느 표면도 목록을 스스로 들고 있지 않다
        // ============================================================================

        [Test]
        public void NoSurfaceKeepsItsOwnHandWrittenCloseList()
        {
            string enforcementFile = Path.Combine(InteractionRoot, "ExclusiveSurfaces.cs")
                .Replace('\\', '/');

            // "다른 표면 타입을 GetComponent로 집어 와서 닫는" 형태를 찾는다. 이것이 손으로 적은 목록이다.
            string[] surfaceTypeNames = RuntimeAssembly.GetTypes()
                .Where(t => !t.IsAbstract
                            && typeof(MonoBehaviour).IsAssignableFrom(t)
                            && typeof(IExclusiveSurface).IsAssignableFrom(t))
                .Select(t => t.Name)
                .ToArray();

            Assert.IsNotEmpty(surfaceTypeNames, $"{LogPrefix} 등록된 배타 표면이 하나도 없습니다.");

            var offenders = new List<string>();
            foreach (string path in Directory.GetFiles(InteractionRoot, "*.cs", SearchOption.AllDirectories))
            {
                string norm = path.Replace('\\', '/');
                if (norm == enforcementFile) continue;

                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (IsCommentLine(line)) continue;
                    if (line.IndexOf("GetComponent<", StringComparison.Ordinal) < 0) continue;

                    foreach (string name in surfaceTypeNames)
                    {
                        if (line.IndexOf("GetComponent<" + name + ">", StringComparison.Ordinal) < 0) continue;
                        // 자기 자신을 찾는 것은 배타 규칙과 무관하다(예: 시트 복귀용 참조).
                        if (Path.GetFileNameWithoutExtension(path) == name) continue;
                        // 같은 줄이나 아주 가까운 줄에서 닫고 있으면 손으로 적은 목록이다.
                        if (!ClosesNearby(lines, i)) continue;
                        offenders.Add($"{Path.GetFileName(path)}:{i + 1}  {line.Trim()}");
                    }
                }
            }

            Assert.IsEmpty(offenders,
                $"{LogPrefix} 배타 정리를 스스로 나열하는 코드가 남아 있습니다:\n  " +
                string.Join("\n  ", offenders) + "\n" +
                $"→ 이런 목록은 (a) 새 표면이 생기면 반드시 빠지고 (b) 그 아래에 조기 반환이 끼면 " +
                $"조용히 건너뛰어집니다. 정리는 {nameof(ExclusiveSurfaces)}.{nameof(ExclusiveSurfaces.CloseAllExcept)} " +
                "한 곳에만 두세요.");
        }

        private static bool ClosesNearby(string[] lines, int index)
        {
            for (int i = index; i < Math.Min(lines.Length, index + 4); i++)
            {
                string l = lines[i];
                if (IsCommentLine(l)) continue;
                if (l.Contains(".Close(") || l.Contains(".ForceCloseAll(") || l.Contains(".Collapse(")) return true;
            }
            return false;
        }

        private static bool IsCommentLine(string line)
        {
            string t = line.TrimStart();
            return t.StartsWith("//", StringComparison.Ordinal)
                   || t.StartsWith("///", StringComparison.Ordinal)
                   || t.StartsWith("*", StringComparison.Ordinal);
        }

        // ============================================================================
        // 3. 집행 지점의 순회에 조기 반환이 없다 — 이 사고의 직접 원인
        // ============================================================================

        [Test]
        public void TheEnforcementPointHasNoEarlyReturn()
        {
            string path = Path.Combine(InteractionRoot, "ExclusiveSurfaces.cs");
            Assert.IsTrue(File.Exists(path), $"{LogPrefix} 집행 지점 파일이 없습니다: {path}");

            string[] lines = File.ReadAllLines(path);
            int start = Array.FindIndex(lines, l =>
                l.Contains("public static int " + nameof(ExclusiveSurfaces.CloseAllExcept)));
            Assert.Greater(start, 0,
                $"{LogPrefix} {nameof(ExclusiveSurfaces.CloseAllExcept)}를 소스에서 찾지 못했습니다 — " +
                "이름이 바뀌었다면 이 감사도 함께 옮기세요(감사가 조용히 무력화됩니다).");

            int loop = Array.FindIndex(lines, start, l => l.TrimStart().StartsWith("for (", StringComparison.Ordinal));
            Assert.Greater(loop, start, $"{LogPrefix} 표면 순회 루프를 찾지 못했습니다.");

            for (int i = loop; i < lines.Length; i++)
            {
                string t = lines[i].Trim();
                if (t.StartsWith("}", StringComparison.Ordinal) && lines[i].Length - t.Length <= 12) break;
                if (IsCommentLine(lines[i])) continue;

                Assert.IsFalse(t.StartsWith("return", StringComparison.Ordinal) || t.Contains(" return "),
                    $"{LogPrefix} 표면 순회 루프 안에 return이 생겼습니다({path}:{i + 1}) — " +
                    "\"{0}\"\n→ 이 사고의 직접 원인이 정확히 그 형태였습니다: 앞쪽 표면 하나를 닫고 " +
                    "return해서 뒤쪽 표면이 영원히 안 닫혔습니다.".Replace("{0}", t));
                Assert.IsFalse(t.StartsWith("break", StringComparison.Ordinal),
                    $"{LogPrefix} 표면 순회 루프 안에 break이 생겼습니다({path}:{i + 1}).");
            }
        }
    }
}
