using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 창 겹침 회귀 — 사용자 신고(2026-09-01) <b>"케릭터창도 겹쳐서보이는 문제있고"</b>.
    ///
    /// ============================================================================
    /// 재현된 비대칭
    /// ============================================================================
    /// <code>
    ///   정보창 -> P  :  [정보창] 닫힘 + [설정창] 열림            (정상)
    ///   설정창 -> I  :  [정보창] 열림 ... 설정창은 그대로 남음    (버그)
    /// </code>
    /// 720x560 설정창이 880x861 정보창 위에 왼쪽 위로 치우쳐 떠서 초상화 / STRESS·EXP /
    /// 근속·함께한 시간·보유 장비·활쏘기 명중과 장비 그리드 가운데를 가렸다.
    ///
    /// ★ <b>양방향을 모두</b> 검증한다. 한 방향만 보면 이번처럼 "한쪽만 구현된" 상태를 통과시킨다.
    ///
    /// ★ 시간 대기는 전부 <b>벽시계(초)</b>다(CLAUDE.md) — 배치모드 PlayMode는 2,000fps를 넘겨서
    ///   "N프레임" 예산이 실제로는 0.01초밖에 안 되는 경우가 있다. 부채꼴 접힘은 애니메이션이라
    ///   프레임 수로 재면 프로덕션은 멀쩡한데 테스트만 불안정해진다.
    /// </summary>
    public sealed class ExclusiveSurfaceOverlapTests
    {
        private const string LogPrefix = "[창겹침-TEST]";

        /// <summary>부채꼴 접힘 애니메이션이 끝나기를 기다리는 상한(초).</summary>
        private const float SettleTimeoutSeconds = 3.0f;

        private CharacterInfoWindow _info;
        private SettingsWindow _settings;
        private readonly List<string> _openNames = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _info = Object.FindFirstObjectByType<CharacterInfoWindow>();
            _settings = Object.FindFirstObjectByType<SettingsWindow>();
            Assert.IsNotNull(_info, $"{LogPrefix} 씬에 {nameof(CharacterInfoWindow)}가 없습니다.");
            Assert.IsNotNull(_settings, $"{LogPrefix} 씬에 {nameof(SettingsWindow)}가 없습니다.");

            // ★ 이 회귀의 함정은 "부채꼴이 있을 때만" 발동했다(조기 반환). 부채꼴이 없는 조립에서
            //   테스트하면 버그를 재현할 수 없다 — 전제를 명시적으로 못 박는다.
            Assert.IsNotNull(_info.GetComponent<GearRadialMenuWidget>(),
                $"{LogPrefix} 같은 GameObject에 {nameof(GearRadialMenuWidget)}가 없습니다 — " +
                "이 회귀(부채꼴이 떠 있는 정식 조립에서만 정리가 건너뛰어짐)를 재현할 조건 자체가 " +
                "사라졌습니다. 씬 조립이 바뀌었다면 이 테스트도 함께 옮기세요.");
        }

        [UnityTearDown]
        public IEnumerator CloseAll()
        {
            if (_info != null && _info.IsOpen) _info.Close("테스트 정리");
            if (_settings != null && _settings.IsOpen) _settings.Close("테스트 정리");
            _info = null;
            _settings = null;
            yield return null;
        }

        // ============================================================================
        // 양방향 — 어느 쪽을 먼저 열든 창은 한 번에 하나만
        // ============================================================================

        /// <summary>정상이라고 보고됐던 방향. <b>고치면서 깨지지 않았는지</b>를 지킨다.</summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator SettingsAfterInfoLeavesOnlyTheSettingsWindow()
        {
            _info.Toggle("테스트 I");
            Assert.IsTrue(_info.IsOpen, $"{LogPrefix} 정보창이 열리지 않았습니다.");
            yield return null;

            _settings.Toggle("테스트 P");
            yield return null;

            Assert.IsTrue(_settings.IsOpen, $"{LogPrefix} 설정창이 열리지 않았습니다.");
            Assert.IsFalse(_info.IsOpen,
                $"{LogPrefix} P 이후에도 정보창이 열려 있습니다 — 두 창이 겹쳐 보입니다.");
        }

        /// <summary>★ 신고된 방향. 고치기 전에는 <b>여기서만</b> 실패했다.</summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator InfoAfterSettingsLeavesOnlyTheInfoWindow()
        {
            _settings.Toggle("테스트 P");
            Assert.IsTrue(_settings.IsOpen, $"{LogPrefix} 설정창이 열리지 않았습니다.");
            yield return null;

            _info.Toggle("테스트 I");
            yield return null;

            Assert.IsTrue(_info.IsOpen, $"{LogPrefix} 정보창이 열리지 않았습니다.");
            Assert.IsFalse(_settings.IsOpen,
                $"{LogPrefix} I 이후에도 설정창이 열려 있습니다 — 720x560 설정창이 880x861 정보창 위에 " +
                "겹쳐 떠서 초상화/스탯/장비 그리드를 가립니다(사용자 신고 그 화면).");
        }

        /// <summary>정보창 -> 설정창 -> 정보창처럼 <b>왕복</b>해도 한 창만 남는가.
        /// 설정창의 시트 복귀 예약(<c>_restoreInfoWindowOnClose</c>)이 배타 규칙과 싸우면
        /// 두 창이 동시에 열린 상태로 되살아날 수 있는 자리다.</summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator RoundTrippingBetweenTheTwoWindowsNeverLeavesBothOpen()
        {
            for (int cycle = 0; cycle < 3; cycle++)
            {
                _info.Toggle($"테스트 I #{cycle}");
                yield return null;
                AssertAtMostOneWindow($"I #{cycle}");

                _settings.Toggle($"테스트 P #{cycle}");
                yield return null;
                AssertAtMostOneWindow($"P #{cycle}");
            }
        }

        private void AssertAtMostOneWindow(string step)
        {
            Assert.IsFalse(_info.IsOpen && _settings.IsOpen,
                $"{LogPrefix} [{step}] 정보창과 설정창이 동시에 열려 있습니다.");
        }

        // ============================================================================
        // 함정 자체 — 부채꼴/팝오버가 떠 있어도 "전부" 닫힌다
        // ============================================================================

        /// <summary>
        /// ★ 이번 사고의 <b>구조적</b> 부분. 종전 코드는 부채꼴이 있으면
        /// <c>ForceCloseAll(); return;</c>으로 빠져나가 그 뒤의 표면을 건드리지 않았다.
        /// 그래서 "부채꼴이 떠 있는 상태"에서 창을 열어 <b>등록된 모든 표면</b>이 거둬지는지 본다 —
        /// 표면 목록을 여기 적지 않고 <see cref="ExclusiveSurfaces.CountOpen"/>로 <b>세기만</b> 한다
        /// (새 표면이 추가돼도 이 테스트를 고칠 필요가 없다).
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator OpeningAWindowClosesEveryOtherRegisteredSurface()
        {
            var fan = _info.GetComponent<GearRadialMenuWidget>();
            fan.Expand(new Vector2(Screen.width - 40f, Screen.height - 40f));
            _settings.Toggle("테스트 P");
            yield return null;

            _info.Toggle("테스트 I");
            yield return null;

            // 부채꼴은 접힘 애니메이션이 있으므로 사라질 때까지 벽시계로 기다린다.
            float deadline = Time.realtimeSinceStartup + SettleTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline && ExclusiveSurfaces.CountOpen(_info) > 1)
            {
                yield return null;
            }

            ExclusiveSurfaces.CollectOpenNames(_info, _openNames);
            Assert.IsTrue(_info.IsOpen, $"{LogPrefix} 정보창이 열리지 않았습니다.");
            Assert.AreEqual(1, ExclusiveSurfaces.CountOpen(_info),
                $"{LogPrefix} {SettleTimeoutSeconds:F1}초가 지나도 열려 있는 배타 표면이 " +
                $"{ExclusiveSurfaces.CountOpen(_info)}개입니다: [{string.Join(", ", _openNames)}] — " +
                "정보창 하나만 남아야 합니다.");
        }
    }
}
