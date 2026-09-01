using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StickMate.Core;
using UnityEditor;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <b>주석으로만 존재하던 계약</b>을 검사로 바꾼다 — 중립(Idle) 포즈 각도의 <b>사본 3벌</b>이
    /// 실제로 같은 값인가 (2026-09-01 설정 드리프트 라운드의 곁가지).
    ///
    /// ============================================================================
    /// 왜 이 파일이 생겼는가
    /// ============================================================================
    /// 같은 라운드에서 <see cref="ConfigAssetDriftLedgerTests"/>가 잡은 실패 유형은
    /// "<b>한 진실의 사본이 여러 곳에 있고, 대조 장치가 없다</b>"였다. 배포 에셋만 그런 게 아니었다 —
    /// 중립 포즈 각도는 <b>세 곳</b>에 적혀 있다.
    /// <list type="number">
    ///   <item><see cref="StickConfig"/>의 필드(+ 배포 에셋의 직렬화 값) — <b>런타임이 쓰는 값</b>.</item>
    ///   <item><c>Assets/Editor/SceneBootstrapper.cs</c> — <b>프리팹을 굽는</b> 값. 어긋나면
    ///         프리팹 저장 자세와 런타임 목표각이 달라 <b>첫 프레임에 팔다리가 튄다</b>.</item>
    ///   <item><c>Interaction/CharacterPortraitStage.cs</c> — <b>정보창 초상화</b>가 그리는 값.
    ///         어긋나면 초상화와 바탕화면의 캐릭터가 서로 다른 자세로 서 있다.</item>
    /// </list>
    ///
    /// 세 곳이 같아야 한다는 사실은 <b>세 파일의 주석에 각각 적혀 있었지만</b>(예: SceneBootstrapper —
    /// "StickConfig.idleArmSpreadDegrees와 <b>반드시 같은 값이어야 한다</b>"), 그것을 확인하는 검사는
    /// 하나도 없었다. 어셈블리가 셋으로 갈려(Runtime / Editor / Runtime) 컴파일러도 못 잡는다.
    /// 지금은 우연히 셋 다 일치한다 — 그 우연이 깨지는 날 조용히 깨진다는 것이 문제다.
    ///
    /// ============================================================================
    /// 무엇과 비교하는가 — <b>코드 기본값이 아니라 실효값</b>이다
    /// ============================================================================
    /// 사본이 따라가야 하는 것은 "코드에 적힌 초기값"이 아니라 <b>실제로 돌아가는 값</b>, 즉
    /// 배포 에셋의 직렬화 값이다. 이 구분이 바로 이번 사고의 교훈이다(에셋이 코드 기본값을 이긴다).
    /// 에셋과 코드 기본값이 갈라지는 것 자체는 <see cref="ConfigAssetDriftLedgerTests"/>가 대장으로
    /// 관리하므로, 여기서는 <b>실효값</b> 하나만 본다.
    ///
    /// ============================================================================
    /// 부호 규약
    /// ============================================================================
    /// 초상화의 <c>IdleKneeBendDegrees</c>는 <b>부호를 상수에 이미 넣어</b> 둔다(-4). 프리팹 베이커는
    /// 크기만 두고 <c>KneeBendSign</c>을 곱한다(4 × -1). 그래서 무릎만 <b>크기</b>로 비교하고, 그
    /// 사실을 표에 명시한다 — 규약이 바뀌면 이 줄을 고쳐야 한다는 것까지 눈에 보이게.
    ///
    /// ============================================================================
    /// 네거티브 컨트롤
    /// ============================================================================
    /// <see cref="네거티브_컨트롤_소스_상수_읽기가_실제로_값을_구분한다"/> — 리더가 항상 같은 값을
    /// 뱉거나 정규식이 아무거나 물면 위 검사 전체가 "항상 참인 단언"이 된다.
    ///
    /// <para><b>플랫폼</b>: 플랫폼 중립이다(세 파일 모두 플랫폼 분기가 없다).
    /// 이 테스트는 소스와 에셋을 <b>읽기만</b> 한다.</para>
    /// </summary>
    public sealed class DuplicatedPoseConstantParityTests
    {
        private const string LogPrefix = "[포즈사본]";
        private const string DeployedConfigPath = "Assets/_Project/Data/DefaultStickConfig.asset";

        private static string BootstrapperPath => SourceConstantReader.SceneBootstrapperPath;

        private static string PortraitPath => SourceConstantReader.PortraitStagePath;

        /// <summary>한 줄이 곧 "이 사본은 저 필드를 따라간다"는 계약이다.</summary>
        private readonly struct Copy
        {
            public readonly string ConfigField;
            public readonly string SourceLabel;
            public readonly Func<string> SourcePath;
            public readonly string ConstName;
            /// <summary>부호를 상수에 미리 넣어 둔 사본인가(그러면 크기로만 비교한다).</summary>
            public readonly bool MagnitudeOnly;
            public readonly string SignNote;

            public Copy(string configField, string sourceLabel, Func<string> sourcePath, string constName,
                        bool magnitudeOnly = false, string signNote = null)
            {
                ConfigField = configField;
                SourceLabel = sourceLabel;
                SourcePath = sourcePath;
                ConstName = constName;
                MagnitudeOnly = magnitudeOnly;
                SignNote = signNote;
            }
        }

        private static readonly Copy[] Copies =
        {
            // ---- 프리팹 베이커(Editor 어셈블리) — 어긋나면 첫 프레임에 팔다리가 튄다.
            new Copy("idleArmSpreadDegrees",  "SceneBootstrapper", () => BootstrapperPath, "IdleArmSpreadDegrees"),
            new Copy("idleLegSpreadDegrees",  "SceneBootstrapper", () => BootstrapperPath, "IdleLegSpreadDegrees"),
            new Copy("idleElbowBendDegrees",  "SceneBootstrapper", () => BootstrapperPath, "IdleElbowBendDegrees"),
            new Copy("idleKneeBendDegrees",   "SceneBootstrapper", () => BootstrapperPath, "IdleKneeBendDegrees"),

            // ---- 정보창 초상화(Runtime 어셈블리) — 어긋나면 초상화와 실제 캐릭터의 자세가 다르다.
            new Copy("idleArmSpreadDegrees",  "CharacterPortraitStage", () => PortraitPath, "IdleArmSpreadDegrees"),
            new Copy("idleLegSpreadDegrees",  "CharacterPortraitStage", () => PortraitPath, "IdleLegSpreadDegrees"),
            new Copy("idleElbowBendDegrees",  "CharacterPortraitStage", () => PortraitPath, "IdleElbowBendDegrees"),
            new Copy("idleKneeBendDegrees",   "CharacterPortraitStage", () => PortraitPath, "IdleKneeBendDegrees",
                     magnitudeOnly: true,
                     signNote: "초상화는 굽힘 부호(KneeBendSign = -1)를 상수에 미리 넣어 둔다 — 크기로 비교한다."),
        };

        // ============================================================================
        // 도구
        // ============================================================================

        /// <summary>C# 소스에서 <c>이름 = 숫자f</c> 형태의 상수 하나를 읽는다
        /// (<see cref="HeadFillGeometryTests"/>가 이미 쓰는 관례와 같은 방식 —
        /// 어셈블리가 갈려 리플렉션이 닿지 않는 Editor 코드까지 같은 어법으로 다룰 수 있다).</summary>
        // ★ 소스에서 상수를 읽는 구현은 SourceConstantReader 하나뿐이다. 파일마다 따로 두면
        //   이 파일이 잡으려는 바로 그 병(사본 드리프트)을 테스트 코드가 앓게 된다.
        private static bool TryReadSourceConst(string filePath, string name, out float value)
            => SourceConstantReader.TryReadFloat(filePath, name, out value);

        private static float ReadSourceConst(string filePath, string name)
            => SourceConstantReader.ReadFloat(filePath, name);

        /// <summary>배포 에셋이 실제로 들고 있는 값 = <b>런타임이 쓰는 값</b>. 숫자를 베끼지 않고
        /// 필드 이름으로만 조회한다.</summary>
        private static float ReadEffective(StickConfig config, string fieldName)
        {
            FieldInfo f = typeof(StickConfig).GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, $"{LogPrefix} StickConfig에 '{fieldName}' 필드가 없습니다 — 표(Copies)가 낡았습니다.");
            Assert.AreEqual(typeof(float), f.FieldType, $"{LogPrefix} '{fieldName}'이 float가 아닙니다.");
            return (float)f.GetValue(config);
        }

        private static StickConfig LoadDeployedConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<StickConfig>(DeployedConfigPath);
            Assert.IsNotNull(config, $"{LogPrefix} 배포 설정 에셋을 찾지 못했습니다: {DeployedConfigPath}");
            return config;
        }

        // ============================================================================
        // 본 검사
        // ============================================================================

        [Test]
        public void 중립_포즈_각도의_사본들이_배포_에셋의_실효값과_같다()
        {
            StickConfig config = LoadDeployedConfig();
            var mismatches = new List<string>();
            var lines = new List<string>();

            foreach (Copy copy in Copies)
            {
                float effective = ReadEffective(config, copy.ConfigField);
                float actual = ReadSourceConst(copy.SourcePath(), copy.ConstName);

                float lhs = copy.MagnitudeOnly ? Mathf.Abs(effective) : effective;
                float rhs = copy.MagnitudeOnly ? Mathf.Abs(actual) : actual;

                lines.Add($"  {copy.SourceLabel}.{copy.ConstName} = {actual:0.###} " +
                          $"vs StickConfig.{copy.ConfigField} = {effective:0.###}" +
                          (copy.MagnitudeOnly ? " (크기 비교)" : string.Empty));

                if (Mathf.Abs(lhs - rhs) <= 1e-4f) continue;

                mismatches.Add(
                    $"  · {copy.SourceLabel}.{copy.ConstName} = {actual:0.###} 인데 " +
                    $"StickConfig.{copy.ConfigField}의 실효값은 {effective:0.###} 입니다." +
                    (copy.SignNote == null ? string.Empty : $"\n      ({copy.SignNote})"));
            }

            Debug.Log($"{LogPrefix} 사본 {Copies.Length}벌 대조\n" + string.Join("\n", lines));

            Assert.IsEmpty(mismatches,
                $"{LogPrefix} 중립 포즈 각도의 사본이 배포 에셋의 실효값과 다릅니다 ({mismatches.Count}건).\n" +
                string.Join("\n", mismatches) +
                "\n\n무엇이 깨지는가.\n" +
                "  · SceneBootstrapper 쪽이 다르면 → 프리팹에 구워진 자세와 런타임 목표각이 달라\n" +
                "    캐릭터가 <b>씬 진입 첫 프레임에 팔다리를 튕긴다</b>.\n" +
                "  · CharacterPortraitStage 쪽이 다르면 → 정보창 초상화와 바탕화면의 캐릭터가\n" +
                "    <b>서로 다른 자세</b>로 선다.\n" +
                "\n무엇을 하면 되는가 — 두 갈래 중 하나입니다.\n" +
                "  (가) 값을 바꾼 것이 의도라면 → <b>사본 세 곳을 전부</b> 같이 고치십시오\n" +
                "       (StickConfig 필드 기본값 / 배포 에셋 / 위 표의 사본들).\n" +
                "  (나) 부호나 단위 규약이 바뀐 것이라면 → 위 Copies 표의 해당 줄에\n" +
                "       magnitudeOnly / signNote를 갱신해 <b>새 규약을 표에 적으십시오</b>.\n" +
                "       (주석에만 적고 검사를 안 고치면 이 계약은 다시 사라집니다 —\n" +
                "        이 파일이 생긴 이유가 정확히 그것입니다.)");
        }

        [Test]
        public void 사본_표가_실재하는_필드와_상수만_가리킨다()
        {
            StickConfig config = LoadDeployedConfig();
            var seen = new HashSet<string>();

            foreach (Copy copy in Copies)
            {
                ReadEffective(config, copy.ConfigField);              // 필드가 없으면 여기서 죽는다
                ReadSourceConst(copy.SourcePath(), copy.ConstName);   // 상수가 없으면 여기서 죽는다

                Assert.IsTrue(seen.Add($"{copy.SourceLabel}.{copy.ConstName}"),
                    $"{LogPrefix} 표에 {copy.SourceLabel}.{copy.ConstName}이 두 번 있습니다.");
            }

            Assert.Greater(Copies.Length, 0, $"{LogPrefix} 표가 비면 이 파일은 아무것도 지키지 않습니다.");
        }

        // ============================================================================
        // ★ 네거티브 컨트롤
        // ============================================================================

        [Test]
        public void 네거티브_컨트롤_소스_상수_읽기가_실제로_값을_구분한다()
        {
            // 리더가 항상 같은 값을 뱉으면 위 검사 전체가 "항상 참인 단언"이 된다.
            // 같은 파일에서 <b>다른 값을 가진</b> 두 상수를 읽어, 서로 다르게 나오는지 확인한다.
            float arm = ReadSourceConst(BootstrapperPath, "IdleArmSpreadDegrees");
            float leg = ReadSourceConst(BootstrapperPath, "IdleLegSpreadDegrees");

            Assert.AreNotEqual(arm, leg,
                $"{LogPrefix} 소스 리더가 팔/다리 벌림 각도를 같은 값({arm})으로 읽었습니다 — " +
                "정규식이 엉뚱한 곳을 물고 있거나 리더가 고장났습니다. 이 상태에서는 위 대조가 " +
                "모두 무의미합니다.");

            // 음수 상수도 부호까지 읽는가(초상화 무릎이 그렇다 — 부호를 놓치면 크기 비교가 무의미해진다).
            float portraitKnee = ReadSourceConst(PortraitPath, "IdleKneeBendDegrees");
            Assert.Less(portraitKnee, 0f,
                $"{LogPrefix} 초상화의 IdleKneeBendDegrees를 {portraitKnee}로 읽었습니다 — " +
                "이 상수는 굽힘 부호를 이미 담고 있어 음수여야 합니다. 양수로 읽혔다면 정규식이 " +
                "'-'를 흘린 것이고, 그러면 부호 규약 검사가 통째로 거짓이 됩니다.");
        }

        [Test]
        public void 네거티브_컨트롤_없는_상수는_읽기가_실패로_보고된다()
        {
            // 리더가 못 찾았을 때 조용히 0을 돌려주면, 사라진 사본이 "0이라서 다르다"가 아니라
            // 아무 말 없이 지나갈 수 있다. "못 찾았다"가 반드시 <b>실패로 보고</b>돼야 한다.
            Assert.IsFalse(TryReadSourceConst(BootstrapperPath, "존재하지않는상수이름Xyz", out float _),
                $"{LogPrefix} 없는 상수를 '찾았다'고 보고했습니다 — 정규식이 아무거나 물고 있습니다.");

            Assert.IsFalse(TryReadSourceConst(Path.Combine(Application.dataPath, "없는파일Xyz.cs"),
                                              "IdleArmSpreadDegrees", out float _),
                $"{LogPrefix} 존재하지 않는 파일에서 상수를 읽었다고 보고했습니다.");

            // 그리고 있는 상수는 정상적으로 찾는다 — 위 둘이 "항상 false"라서 통과한 게 아님을 보인다.
            Assert.IsTrue(TryReadSourceConst(BootstrapperPath, "IdleArmSpreadDegrees", out float arm),
                $"{LogPrefix} 실재하는 상수를 못 찾았습니다 — 리더가 통째로 고장났습니다.");
            Assert.AreNotEqual(0f, arm, $"{LogPrefix} 리더가 0을 돌려줬습니다.");
        }
    }
}
