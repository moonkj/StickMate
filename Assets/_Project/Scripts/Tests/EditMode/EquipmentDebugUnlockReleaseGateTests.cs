using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StickMate.Core;
using UnityEditor;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <b>릴리스 차단 항목 회귀</b> (2026-09-01) — QA 해금 스위치가 사용자에게 나가는 빌드에서
    /// 꺼져 있는가.
    ///
    /// ============================================================================
    /// 왜 이 파일이 따로 있는가
    /// ============================================================================
    /// <c>EquipmentDebugUnlockTests</c>는 <b>스위치의 동작</b>(켜면 열리고 끄면 원래 규칙으로 돌아온다)을
    /// 본다. 이 파일이 보는 것은 그 위층 — <b>스위치가 언제 켜지는가</b>다. 2026-08-31~09-01 사이 이 값은
    /// <c>private const bool DefaultUnlockAll = true;</c> 한 줄이었고, 출시 전에 <b>사람이 손으로</b>
    /// false로 되돌려야 성장 요소가 살아남는 구조였다. 그건 잊히는 종류의 값이다 — 잊힌 채로 나가면
    /// 첫 실행부터 전 장비가 열려 레벨업 보상이 통째로 무의미해진다. 사람의 기억을 테스트로 대체한다.
    ///
    /// ============================================================================
    /// 에디터에서 "릴리스 빌드"를 어떻게 검증하는가
    /// ============================================================================
    /// 에디터는 언제나 <c>UNITY_EDITOR</c>라 <see cref="EquipmentDebugUnlock.UnlockAll"/>만 봐서는
    /// 릴리스 동작을 <b>물리적으로</b> 재현할 수 없다(<c>StickMateDevToolsGateTests</c>가 같은 이유로
    /// 순수 함수를 따로 테스트한다). 그래서 판정 규칙을
    /// <see cref="EquipmentDebugUnlock.ResolveUnlockAll"/>라는 순수 함수로 떼어 두었고, 여기서는
    /// <c>developmentConfiguration: false</c>를 넣어 <b>릴리스 빌드가 계산할 값 그 자체</b>를 잠근다.
    ///
    /// <para>순수 함수만으로는 "누군가 <c>#else</c> 가지를 true로 되돌리는" 경우를 못 잡는다. 그래서
    /// <see cref="비개발_구성_가지는_소스에서도_false다"/>가 소스 텍스트를 직접 읽어 그 가지를 감시한다 —
    /// 이 저장소의 기존 관례다(<c>PlatformParityAuditTests</c>가 같은 방식으로 플랫폼 분기를 감사한다).</para>
    /// </summary>
    public sealed class EquipmentDebugUnlockReleaseGateTests
    {
        private const string DefaultConfigPath = "Assets/_Project/Data/DefaultStickConfig.asset";

        private static string SourcePath => Path.Combine(
            Application.dataPath, "_Project", "Scripts", "Core", "EquipmentDebugUnlock.cs");

        [SetUp]
        public void Reset()
        {
            CharacterProgressionModel.ResetForTesting();
            EquipmentModel.ResetForTesting();
        }

        [TearDown]
        public void RestoreSuiteDefault()
        {
            // 스위트 규약으로 되돌린다 — 여기서 새면 뒤 테스트가 조용히 물러진다.
            EquipmentDebugUnlock.SetTestOverride(false);
            EquipmentModel.ResetForTesting();
        }

        /// <summary>★ 이 라운드의 핵심 안전망. 사용자에게 나가는 빌드(개발 구성 아님 + 환경변수
        /// 미설정)에서 스위치는 <b>반드시</b> 꺼져 있다.</summary>
        [Test]
        public void 릴리스_빌드에서는_해금_스위치가_꺼진다()
        {
            Assert.IsFalse(EquipmentDebugUnlock.ResolveUnlockAll(developmentConfiguration: false, environmentRaw: null),
                "릴리스 빌드에서 QA 해금 스위치가 켜져 있습니다 — 첫 실행부터 전 장비가 열려 " +
                "레벨업 보상이 통째로 무의미해집니다(릴리스 차단).");

            // 환경변수가 "세워지긴 했지만 꺼짐"인 값들도 전부 닫혀야 한다.
            foreach (string raw in new[] { "", "   ", "0", "false", "no", "off", "2", "아무거나" })
            {
                Assert.IsFalse(EquipmentDebugUnlock.ResolveUnlockAll(false, raw),
                    $"환경변수 \"{raw}\"가 켜짐으로 읽혔습니다.");
            }
        }

        /// <summary>스위치가 꺼졌을 때 <b>실제 제품 규칙</b>이 살아나는지 — 판정 함수만 false를 내고
        /// 정작 아이템은 열려 있으면 아무 의미가 없다. 릴리스 값을 그대로 주입해 결과를 본다.</summary>
        [Test]
        public void 릴리스_판정값을_그대로_넣으면_요구_레벨_아이템이_잠긴다()
        {
            bool releaseValue = EquipmentDebugUnlock.ResolveUnlockAll(developmentConfiguration: false, environmentRaw: null);
            EquipmentDebugUnlock.SetTestOverride(releaseValue);

            StickConfig config = AssetDatabase.LoadAssetAtPath<StickConfig>(DefaultConfigPath);
            Assert.IsNotNull(config, $"기본 설정 에셋을 찾지 못했습니다: {DefaultConfigPath}");
            Assert.AreEqual(1, CharacterProgressionModel.Level, "전제: Lv.1에서 시작.");

            int lockedAtLevelOne = 0;
            for (int s = 0; s < EquipmentModel.SlotCount; s++)
            {
                var slot = (EquipmentSlot)s;
                for (int i = 0; i < EquipmentModel.ItemCount(slot); i++)
                {
                    if (!ItemCatalog.Item(slot, i).IsOwned(config)) lockedAtLevelOne++;
                }
            }

            // 숫자를 적지 않는다(아이템이 늘 때마다 뒤처진다) — "잠긴 것이 하나라도 있는가"만 본다.
            Assert.Greater(lockedAtLevelOne, 0,
                "릴리스 판정값으로도 Lv.1에서 잠긴 장비가 하나도 없습니다 — 성장 요소가 사라졌습니다.");
            Assert.Less(lockedAtLevelOne, ItemCatalog.EquipmentCount,
                "릴리스 판정값에서 전 장비가 잠겼습니다 — 처음부터 쓸 수 있어야 할 기본 장비까지 막혔습니다.");
        }

        /// <summary>개발 중에는 계속 열려 있어야 한다 — 게이트를 세우다 개발 편의를 죽이면
        /// 다음 사람이 다시 상수를 true로 박는다(원래 문제로 되돌아간다).</summary>
        [Test]
        public void 개발_구성이면_환경변수_없이도_열린다()
        {
            Assert.IsTrue(EquipmentDebugUnlock.ResolveUnlockAll(developmentConfiguration: true, environmentRaw: null),
                "개발 구성에서 스위치가 닫혔습니다 — 에디터/개발 빌드에서 장비 전종을 눌러 볼 수 없습니다.");
        }

        /// <summary>릴리스 빌드에서도 팀이 실기로 QA할 수 있는 탈출구가 살아 있는가.</summary>
        [Test]
        public void 릴리스_빌드도_환경변수를_세우면_열린다()
        {
            foreach (string raw in new[] { "1", "true", "TRUE", "on", "yes", " 1 " })
            {
                Assert.IsTrue(EquipmentDebugUnlock.ResolveUnlockAll(false, raw),
                    $"환경변수 \"{raw}\"로 열리지 않았습니다 — 릴리스 실기 QA 절차가 죽습니다.");
            }
        }

        /// <summary>환경변수 이름이 바뀌면 그동안 쓰던 QA 절차가 조용히 죽는다.
        /// DevTools 게이트와 <b>일부러</b> 다른 이름이라는 사실도 함께 못 박는다.</summary>
        [Test]
        public void 환경변수_이름은_STICKMATE_UNLOCK_ALL이고_DevTools와_다르다()
        {
            Assert.AreEqual("STICKMATE_UNLOCK_ALL", EquipmentDebugUnlock.EnvironmentVariableName);
            Assert.AreNotEqual(StickMateDevTools.EnvironmentVariableName, EquipmentDebugUnlock.EnvironmentVariableName,
                "해금 스위치가 DevTools 게이트와 같은 환경변수를 씁니다 — 디버거가 연출을 보려고 " +
                "DevTools를 켜는 순간 장비가 전부 열려, 확인하려던 해금 버그가 그 아래 숨습니다.");
        }

        /// <summary>컴파일 심볼 배선이 살아 있는가 — 에디터는 개발 구성이어야 한다.
        /// (이게 false면 <c>#if</c> 블록이 통째로 잘못 배선된 것이다.)</summary>
        [Test]
        public void 에디터는_개발_구성으로_컴파일된다()
        {
            Assert.IsTrue(EquipmentDebugUnlock.IsDevelopmentConfiguration,
                "에디터인데 개발 구성이 아니라고 나옵니다 — #if UNITY_EDITOR 배선이 깨졌습니다.");

            // 스위트는 강제 OFF 상태로 돈다. 잠깐 강제를 풀어 <b>실제 판정 경로</b>를 한 번 태운다 —
            // 그러지 않으면 Resolve()가 이 스위트에서 한 번도 실행되지 않아 예외/오배선이 숨는다.
            // (TearDown이 스위트 규약으로 되돌린다.)
            EquipmentDebugUnlock.SetTestOverride(null);
            Assert.IsTrue(EquipmentDebugUnlock.UnlockAll,
                "에디터인데 스위치가 닫혔습니다 — 개발 편의가 죽었습니다.");
            Assert.IsNotEmpty(EquipmentDebugUnlock.SourceLabel, "게이트 사유가 비어 있어 로그로 원인을 알 수 없습니다.");
            StringAssert.Contains("개발 구성", EquipmentDebugUnlock.SourceLabel,
                "에디터에서 게이트 사유가 개발 구성이라고 말하지 않습니다.");
        }

        /// <summary>
        /// ★ 소스 감사 — <c>#else</c>(=비개발 구성) 가지가 <b>false</b>인지 텍스트로 확인한다.
        /// 순수 함수 테스트는 "규칙"을 잠그지만, 누군가 <c>#else</c> 쪽을 true로 되돌리면 규칙은 그대로인 채
        /// 릴리스만 열린다. 에디터에서 컴파일된 어셈블리로는 그 가지를 절대 볼 수 없으므로 소스를 읽는다.
        /// </summary>
        [Test]
        public void 비개발_구성_가지는_소스에서도_false다()
        {
            string path = SourcePath;
            Assert.IsTrue(File.Exists(path), $"소스를 찾지 못했습니다: {path} (파일이 옮겨졌다면 이 테스트도 따라가야 합니다.)");
            string src = File.ReadAllText(path).Replace("\r\n", "\n");

            Match block = Regex.Match(src,
                @"#if\s+UNITY_EDITOR\s*\|\|\s*DEVELOPMENT_BUILD\s*\n(?<dev>.*?)\n#else\s*\n(?<release>.*?)\n#endif",
                RegexOptions.Singleline);
            Assert.IsTrue(block.Success,
                "개발 구성 판정의 #if/#else 블록을 찾지 못했습니다 — 게이트가 통째로 사라졌거나 형태가 바뀌었습니다. " +
                "형태를 바꿨다면 이 감사도 같이 갱신하십시오(그냥 지우지 마십시오).");

            StringAssert.Contains("IsDevelopmentConfiguration = false", block.Groups["release"].Value,
                "릴리스(#else) 가지가 개발 구성을 true로 선언하고 있습니다 — 출시 빌드에서 전 장비가 열립니다.");
            StringAssert.Contains("IsDevelopmentConfiguration = true", block.Groups["dev"].Value,
                "개발(#if) 가지가 true가 아닙니다 — 에디터/개발 빌드에서 QA를 못 합니다.");

            // 예전 형태(사람이 손으로 되돌려야 하는 무조건 상수)가 되살아나지 않았는지.
            // ★ 주석은 걷어내고 본다 — 위 클래스 문서가 사고 경위를 설명하며 그 이름을 그대로 인용하고
            //   있어서, 텍스트를 통째로 훑으면 "설명을 썼다는 이유로" 빨간불이 켜진다(실제로 켜졌다).
            //   감사가 잡아야 하는 것은 <b>선언</b>이지 서술이 아니다.
            string code = Regex.Replace(src, @"^\s*//.*$", string.Empty, RegexOptions.Multiline);
            StringAssert.DoesNotMatch(@"\bconst\s+bool\s+\w*UnlockAll\w*\s*=", code,
                "요구 레벨을 무조건 우회하는 const 상수가 되살아났습니다 — 사람이 출시 전에 손으로 " +
                "되돌려야 하는 그 형태가 이번 릴리스 차단 사고의 원인이었습니다. " +
                "빌드 구성(#if)으로 갈리게 유지하십시오.");
        }
    }
}
