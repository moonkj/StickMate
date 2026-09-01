using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ "보이지 않는 것은 눌리지 않는다" — 2026-08-30 R2 M3 회귀.
    ///
    /// ============================================================================
    /// 무엇이 문제였나
    /// ============================================================================
    /// 세로가 짧은 화면(1366x768 노트북)에서는 <c>CharacterInfoWindow.ClampPanelToScreen</c>이 창 높이를
    /// 줄이고, 본문 아래쪽이 <see cref="UnityEngine.UI.RectMask2D"/>에 잘려 <b>화면에서 사라진다</b>.
    /// 그런데 전역 폴링 히트테스트는 마스크를 모르는 순수 사각형 판정이라 그 자리가 계속 눌렸다 —
    /// 이 프로젝트가 <b>최악의 형태</b>라고 부르는 패턴(안 보이는데 클릭은 먹는 UI)이며, 전체화면 감지 때
    /// 차단막을 함께 끄는 원칙과 정확히 같은 규칙이다.
    ///
    /// ============================================================================
    /// ★ 2026-09-01 — 관측 대상을 <b>카드 하단 [착용] 버튼</b>으로 옮겼다
    /// ============================================================================
    /// 원래 이 파일은 <b>상세 패널의 [착용] 버튼</b>을 눌러 봤다. 사용자 신고("각 장비별 착용버튼으로
    /// 했는데 왜 옛날처럼 하단에 착용상자가 따로 있음?")로 그 중복 버튼이 제거되면서 관측 대상이
    /// 사라졌다. <b>규칙 자체는 그 버튼의 성질이 아니라 이 창 전체의 성질</b>이므로, 살아남은 착용
    /// 손잡이(카드 하단 버튼)로 같은 세 단계를 그대로 옮긴다.
    ///
    /// ============================================================================
    /// 절대 조건으로 잠그는 것
    /// ============================================================================
    ///  ① <b>양성 대조</b> — 넉넉한 높이에서는 그 카드의 [착용] 버튼이 100% 보이고 실제로 눌린다
    ///     (눌러서 착용이 바뀐다).
    ///  ② 화면이 낮아 같은 버튼이 <b>0% 보이게</b> 되면 같은 좌표를 눌러도 <b>아무 일도 일어나지 않는다</b>
    ///     (착용 상태 서명이 그대로다). 플래그가 아니라 실제 입력 경로(FeedPointerForTests)로 확인한다.
    ///  ③ 높이를 되돌리면 다시 눌린다(기능을 죽인 것이 아니라 가려진 동안만 막는다).
    ///
    /// 화면 높이를 배치 실행에서 바꿀 수단이 없어, 실제 클램프 함수에 <b>스케일 팩터</b>를 주입해
    /// 같은 계산 경로로 창을 줄인다(available = Screen.height / scaleFactor − 여백). 리플렉션을 쓰는 이유는
    /// FullscreenSuspendUiHidingTests와 같다 — 실경로에 주입 지점이 없고, 소비자가 읽는 값은 동일하다.
    ///
    /// <para><b>어느 카드를 볼지 상수로 적지 않는다.</b> "①에서 온전히 보이고 ②에서 통째로 잘리는"
    /// 카드를 <b>그 자리에서 찾는다</b> — 섹션 높이나 카드 배치를 바꾸면 지정된 인덱스는 조용히
    /// 무의미해지고, 그러면 이 파일이 아무것도 지키지 않게 된다.</para>
    /// </summary>
    public sealed class InfoWindowClippedHitTestTests
    {
        private const string LogPrefix = "[가려진클릭-TEST]";

        private static readonly MethodInfo ClampMethod = typeof(CharacterInfoWindow).GetMethod(
            "ClampPanelToScreen", BindingFlags.Instance | BindingFlags.NonPublic);

        private CharacterInfoWindow _window;
        private StickConfig _config;
        private string _backup;
        private bool _hadFile;

        [OneTimeSetUp]
        public void BackupRealSaveFile()
        {
            string path = CharacterSaveStore.FilePath;
            _hadFile = File.Exists(path);
            _backup = _hadFile ? File.ReadAllText(path) : null;
        }

        [OneTimeTearDown]
        public void RestoreRealSaveFile()
        {
            string path = CharacterSaveStore.FilePath;
            if (_hadFile) File.WriteAllText(path, _backup);
            else if (File.Exists(path)) File.Delete(path);
            UiLayoutModel.ResetForTesting();
        }

        [UnityTearDown]
        public IEnumerator TearDownAll()
        {
            if (_window != null && _window.IsOpen) _window.Close("테스트 정리");
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            _window = null;
            _config = null;
            yield return null;
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ClippedCardEquipButtonIsNotClickableAndComesBackWhenVisibleAgain()
        {
            yield return LoadSceneAndOpenWindow();

            Assert.IsNotNull(ClampMethod, $"{LogPrefix} ClampPanelToScreen을 찾지 못했습니다 — 이름이 바뀌었습니다.");

            // ★ 프레임을 넘기지 않고 측정한다 — Update가 매 프레임 실제 화면 크기로 다시 클램프하므로
            //   주입한 크기는 그 프레임 안에서만 유효하다(레이아웃 그룹이 없어 코너는 즉시 갱신된다).

            int probe = FindCardVisibleWhenTallAndClippedWhenShort();
            Assert.GreaterOrEqual(probe, 0,
                $"{LogPrefix} 창을 키우면 온전히 보이고 최소 높이로 줄이면 통째로 잘리는 카드 버튼을 " +
                "하나도 찾지 못했습니다 — 이 화면에서는 '가려짐'을 만들 수 없어 관측 전제가 성립하지 않습니다.");

            EnsureOwned(probe);

            // ---- ① 양성 대조: 넉넉한 세로에서는 100% 보이고 실제로 눌린다 ----
            Clamp(ScaleFactorForTallScreen());

            Assert.Greater(_window.CardEquipButtonVisibleFraction(probe), 0.99f,
                $"{LogPrefix} 창을 키웠는데도 {probe}번 카드의 [착용] 버튼이 잘려 있습니다.");

            Vector2 visibleCenter = _window.CardEquipButtonRawScreenRect(probe).center;
            Assert.IsTrue(_window.IsCardEquipButtonHittableAt(probe, visibleCenter),
                $"{LogPrefix} 보이는데도 히트테스트가 거부했습니다.");

            int before = EquipmentModel.WornStateSignature;
            PressAndRelease(visibleCenter);
            Assert.AreNotEqual(before, EquipmentModel.WornStateSignature,
                $"{LogPrefix} 보이는 [착용] 버튼을 눌렀는데 착용 상태가 그대로입니다 — " +
                "이 양성 대조가 없으면 아래 '안 눌린다'는 기능이 죽어도 통과합니다.");

            // 클릭 중복 억제(0.35초)를 확실히 지나 보낸다 — 아래 '안 눌린다'가 중복 억제 덕분에
            // 통과하는 가짜 초록이 되지 않게. 벽시계 기준이다(CLAUDE.md).
            yield return new WaitForSecondsRealtime(0.6f);

            // ---- ② 세로가 짧아 통째로 잘리면 같은 자리를 눌러도 아무 일도 없다 ----
            Clamp(ScaleFactorForShortScreen());

            Assert.AreEqual(0f, _window.CardEquipButtonVisibleFraction(probe), 1e-4f,
                $"{LogPrefix} 창을 최소 높이로 줄였는데 {probe}번 카드의 [착용] 버튼이 아직 보입니다.");

            Vector2 hiddenCenter = _window.CardEquipButtonRawScreenRect(probe).center;
            Assert.IsFalse(_window.IsCardEquipButtonHittableAt(probe, hiddenCenter),
                $"{LogPrefix} 마스크에 완전히 잘린 버튼이 여전히 히트테스트를 통과합니다 — " +
                "안 보이는데 눌리는 최악의 형태입니다.");

            int clippedBefore = EquipmentModel.WornStateSignature;
            PressAndRelease(hiddenCenter);
            Assert.AreEqual(clippedBefore, EquipmentModel.WornStateSignature,
                $"{LogPrefix} 보이지 않는 [착용] 버튼 자리를 눌렀는데 착용 상태가 바뀌었습니다.");

            // ★ 그 자리는 줄어든 창 <b>바깥</b>이기도 하다. 2026-09-01까지는 33-7-9의 "창 밖 클릭"
            //   탈출구가 그 클릭으로 창을 닫아서 여기서 다시 열어 줘야 했지만, 2026-09-02 사용자
            //   지시로 그 탈출구가 사라졌으므로 이제 창은 <b>그대로 열려 있어야</b> 한다.
            //   방어적으로 다시 여는 대신 <b>단언</b>한다 — 조용히 다시 열면 그 회귀를 못 본다.
            Assert.IsTrue(_window.IsOpen,
                $"{LogPrefix} 창 밖 자리를 눌렀더니 창이 닫혔습니다 — 2026-09-02 사용자 지시로 " +
                "\"창 밖 클릭\" 탈출구는 걷어냈습니다(사용자가 닫기 전에는 안 꺼져야 합니다).");
            yield return new WaitForSecondsRealtime(0.6f);

            // ---- ③ 되돌리면 다시 눌린다(가려진 동안만 막는 것이지 기능을 죽인 것이 아니다) ----
            Clamp(ScaleFactorForTallScreen());

            Assert.Greater(_window.CardEquipButtonVisibleFraction(probe), 0.99f,
                $"{LogPrefix} 높이를 되돌렸는데 버튼이 여전히 잘려 있습니다.");
            Assert.IsTrue(_window.IsCardEquipButtonHittableAt(probe, _window.CardEquipButtonRawScreenRect(probe).center),
                $"{LogPrefix} 높이를 되돌렸는데 히트테스트가 계속 거부합니다 — 기능을 죽였습니다.");

            Debug.Log($"{LogPrefix} 통과({probe}번 카드) — 보이면 눌리고, 가려지면 안 눌리고, 되돌리면 다시 눌립니다.");
            yield return null;
        }

        // ==================== 도구 ====================

        /// <summary>①의 전제(온전히 보임)와 ②의 전제(통째로 잘림)를 <b>동시에</b> 만족하는 카드를 찾는다.
        /// 프레임을 넘기지 않으므로 두 클램프 결과를 같은 프레임 안에서 비교할 수 있다.</summary>
        private int FindCardVisibleWhenTallAndClippedWhenShort()
        {
            int count = _window.CardCountForTests;
            var visibleWhenTall = new bool[count];

            Clamp(ScaleFactorForTallScreen());
            for (int i = 0; i < count; i++)
            {
                visibleWhenTall[i] = _window.IsCardVisibleForTests(i)
                                     && _window.CardEquipButtonVisibleFraction(i) > 0.99f;
            }

            Clamp(ScaleFactorForShortScreen());
            for (int i = 0; i < count; i++)
            {
                if (!visibleWhenTall[i]) continue;
                if (_window.CardEquipButtonVisibleFraction(i) <= 1e-4f) return i;
            }
            return -1;
        }

        /// <summary>카드 하단 버튼은 <b>뗄 때</b> 확정된다(미는 동안에는 착용되지 않게 하려고 그렇게 만들었다).
        /// 그래서 단발 클릭 진입점이 아니라 누름/뗌을 그대로 먹인다.</summary>
        private void PressAndRelease(Vector2 point)
        {
            _window.FeedPointerForTests(false, point);
            _window.FeedPointerForTests(true, point);
            _window.FeedPointerForTests(false, point);
        }

        /// <summary>이 카드가 잠겨 있으면 <b>레벨을 실제로 올려서</b> 연다 — 임시 QA 해금 스위치가
        /// 꺼지는 날에도 이 파일이 그대로 돌게 한다(잠금 규칙 자체는 한 줄도 우회하지 않는다).
        /// 잠긴 카드를 누르면 착용이 안 되므로 ①의 양성 대조가 성립하지 않는다.</summary>
        private void EnsureOwned(int cardIndex)
        {
            Assert.IsTrue(_window.TryGetCardSlotForTests(cardIndex, out EquipmentSlot slot),
                $"{LogPrefix} {cardIndex}번 카드의 슬롯을 읽지 못했습니다.");
            int item = _window.CardItemForTests(cardIndex);

            int guard = 0;
            while (!EquipmentModel.IsItemOwned(slot, item) && guard++ < 500)
            {
                CharacterProgressionModel.AddXp(1000f, _config);
            }
            Assert.IsTrue(EquipmentModel.IsItemOwned(slot, item),
                $"{LogPrefix} {slot} {item}번을 레벨을 올려도 열지 못했습니다" +
                $"(요구 레벨={EquipmentModel.RequiredLevel(slot, item)}).");
        }

        private void Clamp(float scaleFactor) => ClampMethod.Invoke(_window, new object[] { scaleFactor });

        /// <summary>창이 설계 높이(861)를 다 쓸 만큼 <b>세로가 넉넉한</b> 화면을 흉내내는 스케일 팩터.
        /// 배치 실행의 실제 화면은 작을 수 있어 상수로 적으면 이 테스트가 조용히 무의미해진다.</summary>
        private static float ScaleFactorForTallScreen() => Mathf.Max(0.01f, Screen.height / 1200f);

        /// <summary>본문 아래쪽이 통째로 잘리도록 창을 클램프 하한(320)까지 줄이는 스케일 팩터.</summary>
        private static float ScaleFactorForShortScreen() => Mathf.Max(0.01f, Screen.height / 300f);

        private IEnumerator LoadSceneAndOpenWindow()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _window = Object.FindFirstObjectByType<CharacterInfoWindow>();
            Assert.IsNotNull(_window, $"{LogPrefix} 씬에서 CharacterInfoWindow를 찾지 못했습니다.");

            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            _config = agent != null ? agent.Config : null;
            Assert.IsNotNull(_config, $"{LogPrefix} StickConfig를 찾지 못했습니다 — 레벨을 올릴 수 없습니다.");

            _window.Open("가려진 클릭 테스트");
            yield return null;
            yield return null;   // HorizontalLayoutGroup/ContentSizeFitter가 한 번 돌 기회를 준다.
            Assert.IsTrue(_window.IsOpen, $"{LogPrefix} 창이 열리지 않았습니다.");
        }
    }
}
