using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ <b>배포 ScriptableObject 에셋은 런타임에 절대 오염되지 않는다</b> — 2026-08-31 R3 Blocker 2 잠금.
    ///
    /// ============================================================================
    /// 무엇이 터졌었나
    /// ============================================================================
    /// <c>StickmanAgent.ApplyCharacterScale()</c>이 <c>_config.characterScale = v</c>로 <b>직렬화
    /// 필드</b>에 직접 썼다. 그 <c>_config</c>는 프리팹에 배선된 배포 에셋
    /// <c>Assets/_Project/Data/DefaultStickConfig.asset</c> <b>그 자체</b>다(런타임 복제본이 아니다).
    /// 유니티 에디터는 씬 오브젝트와 달리 ScriptableObject 애셋에 가한 플레이 모드 중 변경을
    /// <b>되돌리지 않으므로</b>, 다이얼을 한 번 돌린 뒤 프로젝트를 저장하면 그 값이 그대로
    /// 커밋되어 전 사용자에게 배포된다.
    ///
    /// 실제 피해: 개발자 저장 파일의 <c>characterScale 0.35</c>가 매 씬 로드마다 복원되면서
    /// 하루치 PlayMode 스위트 전체가 0.35배 캐릭터로 돌았다(한 스위트에 로그 146회). 프리팹은
    /// 0.75배로 정확히 구워져 있었는데도 네 명이 "프리팹 재베이크 누락"으로 오진했다.
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 계약
    /// ============================================================================
    ///   (1) 배율을 바꿔도 <b>직렬화 필드</b> <c>characterScale</c>은 한 비트도 안 바뀐다(= git diff 무변화).
    ///   (2) 그런데도 <b>실효 배율</b>(<c>ResolveCharacterScale()</c> / 실제 캐릭터 신장 / 보행 속도)은
    ///       정확히 따라간다 — (1)이 "아무 일도 안 일어나서" 통과하는 것이 아님을 함께 단언한다.
    ///   (3) 세션(씬 로드)은 언제나 <b>배포 기본 배율</b>에서 출발한다. 에셋 인스턴스는 씬 재로드에도
    ///       살아남으므로 이걸 안 지키면 앞 테스트의 배율이 다음 테스트로 샌다(이번 사고의 전파 경로).
    ///   (4) 테스트는 개발자의 <b>실제 저장 파일</b>을 열지 않는다(Tests/PlayMode/GlobalPlayModeTestIsolation.cs).
    ///
    /// ============================================================================
    /// 네거티브 컨트롤
    /// ============================================================================
    /// <see cref="NegativeControl_직렬화_필드에_직접_쓰면_이_검사가_실제로_잡아낸다"/>가
    /// <b>완전히 같은 검사 방법</b>으로 "옛 방식(직렬화 필드 직접 대입)"을 잡아내는 것을 보인다.
    /// 그 검사는 배포 에셋이 아니라 <b>복제본</b>에 대고 한다 — 검사 방법의 민감도만 증명하면 되고,
    /// 배포 에셋은 이 테스트 안에서도 건드리지 않는다(CLAUDE.md 절대 불변 원칙 3).
    /// </summary>
    public sealed class DeployedConfigAssetImmutabilityTests
    {
        private const string LogPrefix = "[ASSET-IMMUTABLE]";

        private StickmanAgent _agent;
        private StickConfig _deployed;
        private float _serializedDeployScale;
        private StickmanInkColor _serializedDeployInk;

        private IEnumerator LoadSceneAndFindAgent()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다 — Main.unity 배선 확인.");
            _deployed = _agent.Config;
            Assert.IsNotNull(_deployed, $"{LogPrefix} StickmanAgent에 StickConfig가 배선돼 있지 않습니다.");
            _serializedDeployScale = _deployed.characterScale;
            _serializedDeployInk = _deployed.inkColor;

            yield return new WaitForSeconds(0.5f);
        }

        [TearDown]
        public void TearDown()
        {
            // 런타임 배율만 지우고 지오메트리를 배포 기본값으로 되돌린다. 직렬화 필드는 애초에
            // 만진 적이 없으므로 되돌릴 것이 없다(그것이 이 파일이 증명하려는 바다).
            if (_deployed != null) _deployed.ClearRuntimeCharacterScale();
            if (_deployed != null) _deployed.ClearRuntimeInkColor();
            if (_agent != null && _serializedDeployScale > 0f)
                _agent.ApplyCharacterScale(_serializedDeployScale, "테스트 정리");
            _agent = null;
            _deployed = null;
        }

        // ============================================================================
        // (1)+(2) 직렬화 필드는 불변인데 실효 배율은 따라간다
        // ============================================================================

        [UnityTest]
        public IEnumerator 배율_조작이_배포_에셋의_직렬화_필드를_건드리지_않는다()
        {
            yield return LoadSceneAndFindAgent();

            float serializedBefore = _deployed.characterScale;
            float walkBase = _deployed.walkSpeed;
            Assert.IsFalse(_deployed.HasRuntimeCharacterScale,
                $"{LogPrefix} 씬 로드 직후인데 런타임 배율이 이미 설정돼 있습니다 — 앞선 테스트의 값이 샜습니다.");

            // ★ 2026-09-01 — 2.0f였다. 2026-08-31에 사용자 지시로 상한이 <b>1.5</b>로 내려가면서
            //   (StickConfig.MaxCharacterScale) 이 값이 clamp돼 "실효 배율이 2.0이 아니다"로 이 테스트가
            //   계속 실패하고 있었다(설정창 라운드의 회귀 실행에서 발견 — 그 전날부터 빨간불이었다).
            //   숫자를 손으로 다시 적지 않고 <b>상수에서 유도</b>한다: 상한이 또 바뀌어도 따라간다.
            const float Target = StickConfig.MaxCharacterScale;
            Assert.IsTrue(_agent.ApplyCharacterScale(Target, "에셋 불변 테스트"),
                $"{LogPrefix} 배율 {Target:F2} 적용이 무시됐습니다 — 무동작 가드가 잘못 걸렸습니다.");
            yield return null;

            Debug.Log($"{LogPrefix} 적용 후 — 직렬화 characterScale={_deployed.characterScale:F4}(기대 {serializedBefore:F4} 그대로), " +
                $"실효 ResolveCharacterScale()={_deployed.ResolveCharacterScale():F4}, " +
                $"CurrentCharacterScale={_agent.CurrentCharacterScale:F4}, " +
                $"전신 높이={_agent.Metrics.TotalHeight:F4}, 보행 속도={_deployed.ResolveWalkSpeed():F4}.");

            // ★ 핵심 단언 — 디스크에 직렬화되는 필드는 한 비트도 안 움직인다(허용 오차 0).
            Assert.AreEqual(serializedBefore, _deployed.characterScale, 0f,
                $"{LogPrefix} 배포 에셋(DefaultStickConfig.asset)의 직렬화 필드 characterScale이 런타임에 " +
                $"{serializedBefore:F4} -> {_deployed.characterScale:F4}로 바뀌었습니다. 에디터가 이 애셋을 " +
                "저장하는 순간 그 값이 전 사용자에게 배포됩니다(R3 Blocker 2 재발).");

            // ★ (1)이 "아무 일도 안 일어나서" 통과한 것이 아님을 같은 호흡에 증명한다.
            Assert.AreEqual(Target, _deployed.ResolveCharacterScale(), 1e-4f,
                $"{LogPrefix} 실효 배율이 따라오지 않았습니다 — 런타임 배율 경로가 죽었습니다.");
            Assert.AreEqual(Target, _agent.CurrentCharacterScale, 1e-3f,
                $"{LogPrefix} 실제 캐릭터가 목표 배율로 커지지 않았습니다.");
            Assert.AreEqual(StickConfig.BaselineCharacterTotalHeight * Target, _agent.Metrics.TotalHeight, 1e-3f,
                $"{LogPrefix} 실측 신장이 목표 배율을 따라오지 않았습니다.");
            Assert.AreEqual(walkBase * Target, _deployed.ResolveWalkSpeed(), 1e-3f,
                $"{LogPrefix} 보행 속도가 배율을 따라오지 않았습니다 — 보폭만 커지면 발이 미끄러집니다.");
        }

        // ============================================================================
        // 네거티브 컨트롤 — 위 검사가 "옛 방식"을 실제로 잡아내는가
        // ============================================================================

        [UnityTest]
        public IEnumerator NegativeControl_직렬화_필드에_직접_쓰면_이_검사가_실제로_잡아낸다()
        {
            yield return LoadSceneAndFindAgent();

            // 배포 에셋은 건드리지 않는다 — 검사 방법의 민감도만 복제본에서 증명한다.
            StickConfig probe = Object.Instantiate(_deployed);
            try
            {
                float before = probe.characterScale;
                probe.characterScale = 0.35f;   // ← 이것이 R3 Blocker 2의 옛 코드가 하던 일이다.

                Debug.Log($"{LogPrefix} 네거티브 컨트롤 — 복제본에 옛 방식으로 대입: {before:F4} -> {probe.characterScale:F4} " +
                    "(같은 검사가 배포 에셋에서는 변화 0을 보고한다).");

                Assert.AreNotEqual(before, probe.characterScale,
                    $"{LogPrefix} 직렬화 필드에 직접 대입했는데도 값이 그대로입니다 — 위 테스트의 검사 방법이 " +
                    "변화를 감지하지 못한다는 뜻이라, 그 통과는 무의미합니다.");

                // 그리고 그 대입은 실효 배율까지 바꿔 버린다(= 옛 코드가 통했던 이유이자, 오염이 눈에 안 띄던 이유).
                Assert.AreEqual(0.35f, probe.ResolveCharacterScale(), 1e-4f,
                    $"{LogPrefix} 복제본의 실효 배율이 직렬화 값을 따라가지 않습니다 — 폴백 경로가 깨졌습니다.");
            }
            finally
            {
                Object.Destroy(probe);
            }

            // 대조: 배포 에셋은 이 테스트 내내 한 번도 안 바뀌었다.
            Assert.AreEqual(_serializedDeployScale, _deployed.characterScale, 0f,
                $"{LogPrefix} 네거티브 컨트롤이 배포 에셋을 오염시켰습니다.");
        }

        // ============================================================================
        // (3) 세션은 언제나 배포 기본 배율에서 출발한다
        // ============================================================================

        [UnityTest]
        public IEnumerator 씬을_다시_로드하면_배포_기본_배율로_돌아온다()
        {
            yield return LoadSceneAndFindAgent();

            Assert.IsTrue(_agent.ApplyCharacterScale(1.5f, "누수 확인용"),
                $"{LogPrefix} 사전 조건: 배율 1.50 적용이 무시됐습니다.");
            Assert.IsTrue(_deployed.HasRuntimeCharacterScale, $"{LogPrefix} 런타임 배율이 기록되지 않았습니다.");

            yield return LoadSceneAndFindAgent();   // 같은 에셋 인스턴스가 그대로 살아 있는 상태에서 재로드.

            Debug.Log($"{LogPrefix} 재로드 후 — HasRuntimeCharacterScale={_deployed.HasRuntimeCharacterScale}, " +
                $"ResolveCharacterScale()={_deployed.ResolveCharacterScale():F4}, " +
                $"직렬화={_deployed.characterScale:F4}, CurrentCharacterScale={_agent.CurrentCharacterScale:F4}.");

            Assert.IsFalse(_deployed.HasRuntimeCharacterScale,
                $"{LogPrefix} 씬을 다시 로드했는데 앞선 배율이 남아 있습니다 — StickConfig는 에셋이라 씬 재로드에도 " +
                "살아남습니다. StickmanAgent.Awake의 ClearRuntimeCharacterScale()이 빠졌습니다.");
            Assert.AreEqual(_deployed.characterScale, _deployed.ResolveCharacterScale(), 1e-4f,
                $"{LogPrefix} 재로드 후 실효 배율이 배포 기본값과 다릅니다.");
        }


        // ============================================================================
        // ★ 잉크색 — characterScale과 <b>같은 실패 모드</b>였다 (2026-08-31 R5)
        // ============================================================================
        // 리더 지적: "characterScale만 잠그고 잉크색은 그물 밖"이었다. 정보창 스와치와 우클릭 메뉴가
        // `_config.inkColor = next`로 직렬화 필드에 직접 썼고, 같은 경로로 배포 에셋이 오염된다.
        // 아래 세 테스트가 그 구멍을 메운다(런타임 계약 2건 + 소스 정적 스캔 1건).

        [UnityTest]
        public IEnumerator 잉크색_전환이_배포_에셋의_직렬화_필드를_건드리지_않는다()
        {
            yield return LoadSceneAndFindAgent();

            StickmanInkColor serializedBefore = _deployed.inkColor;
            Assert.IsFalse(_deployed.HasRuntimeInkColor,
                $"{LogPrefix} 씬 로드 직후인데 런타임 잉크색이 이미 설정돼 있습니다 — 앞선 테스트의 값이 샜습니다.");

            // 배포 기본값의 반대색으로 바꾼다(어느 쪽이 구워져 있어도 실제 변화가 일어나게).
            StickmanInkColor target = serializedBefore == StickmanInkColor.White
                ? StickmanInkColor.Black
                : StickmanInkColor.White;

            // 프로덕션 경로와 같은 두 줄(Interaction/CharacterInfoWindow.OnInkSwatchClicked 참고).
            _deployed.SetRuntimeInkColor(target);
            _agent.ApplyInkColorFromConfig();
            yield return null;

            Color expected = target == StickmanInkColor.White ? _deployed.whiteInkColor : _deployed.primaryOutlineColor;
            Debug.Log($"{LogPrefix} 잉크색 적용 후 — 직렬화 inkColor={_deployed.inkColor}(기대 {serializedBefore} 그대로), " +
                $"실효 ResolveInkPreset()={_deployed.ResolveInkPreset()}, IsWhiteInk()={_deployed.IsWhiteInk()}, " +
                $"ResolveInkColor()=({_deployed.ResolveInkColor().r:F2},{_deployed.ResolveInkColor().g:F2},{_deployed.ResolveInkColor().b:F2}).");

            // ★ 핵심 단언 — 디스크에 직렬화되는 필드는 그대로다.
            Assert.AreEqual(serializedBefore, _deployed.inkColor,
                $"{LogPrefix} 배포 에셋(DefaultStickConfig.asset)의 직렬화 필드 inkColor가 런타임에 " +
                $"{serializedBefore} -> {_deployed.inkColor}로 바뀌었습니다. 에디터가 이 애셋을 저장하는 순간 " +
                "그 색이 전 사용자의 출하 기본값이 됩니다(R5 잉크색 오염 재발).");

            // ★ (1)이 "아무 일도 안 일어나서" 통과한 것이 아님을 같은 호흡에 증명한다.
            Assert.AreEqual(target, _deployed.ResolveInkPreset(),
                $"{LogPrefix} 실효 잉크 프리셋이 따라오지 않았습니다 — 런타임 오버라이드 경로가 죽었습니다.");
            Assert.AreEqual(target == StickmanInkColor.White, _deployed.IsWhiteInk(),
                $"{LogPrefix} IsWhiteInk()가 실효 프리셋과 어긋납니다.");
            Assert.AreEqual(expected, _deployed.ResolveInkColor(),
                $"{LogPrefix} 실제 선 색이 프리셋을 따라오지 않았습니다.");

            // 네거티브 컨트롤 — 옛 방식(직렬화 필드 직접 대입)은 복제본에서 즉시 검출된다.
            StickConfig probe = Object.Instantiate(_deployed);
            try
            {
                StickmanInkColor before = probe.inkColor;
                probe.inkColor = before == StickmanInkColor.White ? StickmanInkColor.Black : StickmanInkColor.White;
                Debug.Log($"{LogPrefix} 잉크색 네거티브 컨트롤 — 복제본에 옛 방식으로 대입: {before} -> {probe.inkColor}.");
                Assert.AreNotEqual(before, probe.inkColor,
                    $"{LogPrefix} 직렬화 필드에 직접 대입했는데도 값이 그대로입니다 — 위 단언의 검사 방법이 " +
                    "변화를 감지하지 못한다는 뜻이라 그 통과는 무의미합니다.");
            }
            finally
            {
                Object.Destroy(probe);
            }

            Assert.AreEqual(_serializedDeployInk, _deployed.inkColor,
                $"{LogPrefix} 네거티브 컨트롤이 배포 에셋의 잉크색을 오염시켰습니다.");
        }

        [UnityTest]
        public IEnumerator 씬을_다시_로드하면_배포_기본_잉크색으로_돌아온다()
        {
            yield return LoadSceneAndFindAgent();

            StickmanInkColor target = _deployed.inkColor == StickmanInkColor.White
                ? StickmanInkColor.Black
                : StickmanInkColor.White;
            _deployed.SetRuntimeInkColor(target);
            Assert.IsTrue(_deployed.HasRuntimeInkColor, $"{LogPrefix} 런타임 잉크색이 기록되지 않았습니다.");

            yield return LoadSceneAndFindAgent();   // 같은 에셋 인스턴스가 살아 있는 상태에서 재로드.

            Debug.Log($"{LogPrefix} 재로드 후 — HasRuntimeInkColor={_deployed.HasRuntimeInkColor}, " +
                $"ResolveInkPreset()={_deployed.ResolveInkPreset()}, 직렬화={_deployed.inkColor}.");

            Assert.IsFalse(_deployed.HasRuntimeInkColor,
                $"{LogPrefix} 씬을 다시 로드했는데 앞선 잉크색이 남아 있습니다 — StickConfig는 에셋이라 씬 " +
                "재로드에도 살아남습니다. StickmanAgent.Awake의 ClearRuntimeInkColor()가 빠졌습니다.");
            Assert.AreEqual(_deployed.inkColor, _deployed.ResolveInkPreset(),
                $"{LogPrefix} 재로드 후 실효 잉크색이 배포 기본값과 다릅니다.");
        }

        // ============================================================================
        // ★ 소스 정적 스캔 — 이 버그 클래스가 다시 새지 않게 하는 그물
        // ============================================================================
        // 위 두 테스트는 "지금 코드가 안 쓴다"만 증명한다. 내일 누군가 새 UI에서 다시
        // `config.inkColor = ...`라고 적으면 그 자리만 조용히 오염된다(정확히 이번에 벌어진 일이다).
        // 그래서 UserAssetImmutabilityAuditTests와 같은 방식의 텍스트 스캔으로 <b>패턴 자체</b>를 막는다.
        // 주석 줄은 건너뛴다 — 이 수정의 문서가 옛 코드 줄을 그대로 인용하고 있기 때문이다.

        [Test]
        public void 프로덕션_코드는_배포_설정의_직렬화_필드에_직접_쓰지_않는다()
        {
            string scriptsRoot = Path.Combine(Application.dataPath, "_Project", "Scripts");
            string testsRoot = (Path.Combine(scriptsRoot, "Tests") + Path.DirectorySeparatorChar).Replace('\\', '/');

            var files = new List<string>(Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories));
            files.RemoveAll(p => p.Replace('\\', '/').StartsWith(testsRoot, System.StringComparison.Ordinal));

            Assert.GreaterOrEqual(files.Count, 40,
                $"{LogPrefix} 스캔 대상 파일이 비정상적으로 적습니다({files.Count}) — 경로 계산 오류로 허위 통과할 위험.");

            // "무언가.inkColor =" / "무언가.characterScale =" (비교 연산자 ==는 제외).
            var writePattern = new Regex(@"\.(inkColor|characterScale)\s*=(?!=)");
            var violations = new List<string>();

            foreach (string file in files)
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string trimmed = lines[i].TrimStart();
                    if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("///")) continue;
                    Match m = writePattern.Match(lines[i]);
                    if (!m.Success) continue;
                    violations.Add($"{Path.GetFileName(file)}:{i + 1}: {lines[i].Trim()}");
                }
            }

            Debug.Log($"{LogPrefix} 정적 스캔 — 파일 {files.Count}개에서 직렬화 필드 직접 쓰기 {violations.Count}건.");

            Assert.IsTrue(violations.Count == 0,
                $"{LogPrefix} 배포 ScriptableObject 에셋(DefaultStickConfig.asset)의 직렬화 필드에 직접 쓰는 코드가 " +
                "발견됐습니다. 이 에셋은 프리팹 16개 컴포넌트에 배선된 출하 기본값이고, 에디터는 플레이 모드 중의 " +
                "변경을 되돌리지 않습니다 — 대신 SetRuntimeInkColor()/SetRuntimeCharacterScale()를 쓰고, 값이 " +
                "재시작을 넘어 남아야 하면 저장 파일(CharacterAppearanceModel/UiLayoutModel)에 기록하세요.\n\n" +
                string.Join("\n", violations));
        }

        // ============================================================================
        // (4) 테스트는 개발자의 실제 저장 파일을 열지 않는다
        // ============================================================================

        [Test]
        public void 테스트는_개발자의_실제_저장_파일을_읽지_않는다()
        {
            string real = Path.Combine(Application.persistentDataPath, "stickmate_character.json");

            Debug.Log($"{LogPrefix} 저장 경로 — 사용 중={CharacterSaveStore.FilePath}, " +
                $"개발자 실제 파일={real}, 리디렉션={CharacterSaveStore.IsRedirectedForTesting}.");

            Assert.IsTrue(CharacterSaveStore.IsRedirectedForTesting,
                $"{LogPrefix} 저장 경로 리디렉션이 꺼져 있습니다 — GlobalPlayModeTestIsolation의 [SetUpFixture]가 " +
                "실행되지 않았다는 뜻이고, 그러면 스위트 전체가 다시 개발자 개인 파일을 읽습니다(R3 Blocker 2 동반 사고).");
            Assert.AreNotEqual(real, CharacterSaveStore.FilePath,
                $"{LogPrefix} 테스트가 개발자의 실제 저장 파일을 가리키고 있습니다.");
            StringAssert.StartsWith(Application.temporaryCachePath, CharacterSaveStore.FilePath,
                $"{LogPrefix} 리디렉션 경로가 임시 캐시 폴더 밖입니다 — 이 앱에 배정된 자리 밖으로 나가면 안 됩니다.");
        }
    }
}
