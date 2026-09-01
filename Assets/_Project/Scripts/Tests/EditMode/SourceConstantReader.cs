using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 다른 어셈블리/접근제한 때문에 <b>리플렉션이 닿지 않는 상수</b>를 소스에서 읽는다.
    ///
    /// ============================================================================
    /// 왜 소스를 읽는가 — 대안이 더 나쁘다
    /// ============================================================================
    /// <c>CharacterPortraitStage.TallestAccessoryAboveHeadCenterInR</c>는 <b>private const</b>이고
    /// <c>Editor/SceneBootstrapper</c>의 상수들은 <b>다른 어셈블리</b>(Assembly-CSharp-Editor)에 있다.
    /// 테스트가 그 값을 알아야 하는데 코드로는 닿을 수 없다. 그때 선택지는 셋뿐이다.
    /// <list type="number">
    ///   <item><b>숫자를 베낀다</b> — CLAUDE.md가 금지한다. 실제로 이 저장소에서 하드코딩 잔존이
    ///         2026-09-01 하루에만 <b>세 번</b> 나왔다(<c>MaxCharacterScale</c> 2건, 저장 스키마
    ///         버전 2건, 그리고 이 파일이 고치는 <c>AccessoryStrokeBudgetTests</c>의 액자 1.75f).</item>
    ///   <item><b>접근제한을 푼다</b> — 테스트를 위해 프로덕션 캡슐화를 깨는 것은 순서가 거꾸로다.</item>
    ///   <item><b>소스를 읽는다</b> — 값이 한 곳에만 남고, 이름이 바뀌면 <b>실패</b>한다.</item>
    /// </list>
    /// 이 저장소는 이미 (3)을 쓰고 있다(<see cref="HeadFillGeometryTests"/>의 <c>ReadConst</c>,
    /// <c>ShoulderSwingAsymmetryTests</c>의 <c>ReadFloatConst</c>). 그런데 그 구현이 파일마다
    /// <b>따로</b> 있었다 — 사본 드리프트를 잡는 라운드가 사본을 세 번째로 만들 수는 없어서
    /// 여기 하나로 모은다.
    ///
    /// <para><b>못 찾으면 반드시 실패한다.</b> 조용히 0을 돌려주면 "상수가 사라졌다"가
    /// "값이 0이다"로 둔갑해 검산 전체가 무의미해진다
    /// 각 사용처의 네거티브 컨트롤이 <c>TryReadFloat</c>이 없는 이름에 실제로 거짓을 돌려주는지 잠근다.</para>
    /// </summary>
    internal static class SourceConstantReader
    {
        internal static string PortraitStagePath => Path.Combine(
            Application.dataPath, "_Project", "Scripts", "Interaction", "CharacterPortraitStage.cs");

        internal static string SceneBootstrapperPath => Path.Combine(
            Application.dataPath, "Editor", "SceneBootstrapper.cs");

        /// <summary>C# 소스에서 <c>이름 = 숫자f</c> 형태의 상수 하나를 읽는다.
        /// 못 찾으면 <c>false</c>(예외 없음) — 네거티브 컨트롤이 이 경로를 직접 확인할 수 있어야 한다.</summary>
        internal static bool TryReadFloat(string filePath, string name, out float value)
        {
            value = 0f;
            if (!File.Exists(filePath)) return false;

            Match m = Regex.Match(File.ReadAllText(filePath),
                @"\b" + Regex.Escape(name) + @"\s*=\s*(-?[0-9]*\.?[0-9]+)f\s*[;,]");
            if (!m.Success) return false;

            value = float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            return true;
        }

        /// <summary>못 찾으면 <b>테스트를 실패시킨다</b>.</summary>
        internal static float ReadFloat(string filePath, string name)
        {
            Assert.IsTrue(File.Exists(filePath), $"[소스상수] 소스 파일을 찾지 못했습니다: {filePath}");
            Assert.IsTrue(TryReadFloat(filePath, name, out float value),
                $"[소스상수] {Path.GetFileName(filePath)}에서 상수 '{name}'을 찾지 못했습니다.\n" +
                "이름이 바뀌었다면 읽는 쪽도 함께 갱신하십시오 — 그 전까지 그 검산은 " +
                "<b>대상 없이</b> 돌게 되고, 그 상태로 초록불이 뜨는 것이 이 저장소가 반복해 온 실패입니다.");
            return value;
        }
    }
}
