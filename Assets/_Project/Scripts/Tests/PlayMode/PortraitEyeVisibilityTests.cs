using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 2026-09-01 — <b>초상화에 눈이 없다</b>를 잠근다(그림체 전환 P1, docs/UX_FLOW.md 38-4 / 38-5).
    ///
    /// ============================================================================
    /// 이 파일은 <b>뒤집혔다</b> — 그리고 그것이 이 파일이 살아 있는 이유다
    /// ============================================================================
    /// 2026-08-31까지 이 파일은 정확히 반대를 단언했다: "안경 4종 어느 것을 써도 초상화에 눈 두 개가
    /// 그대로 있다". 그때 고친 결함은 <c>DrawBody</c>가 <b>EYES 카테고리에 뭐가 걸쳐지면 눈을 통째로
    /// 지우던</b> 이중 정의였다(실제 캐릭터는 눈을 계속 그리는데 초상화만 혼자 지웠다).
    ///
    /// 2026-09-01에 사용자가 그림체를 두꺼운 채움 실루엣으로 바꾸면서 <b>눈 자체를 삭제</b>했다
    /// ("눈 삭제하고 머리도 그냥 다 채워주고"). 그래서 단언을 뒤집는다 — <b>지우지 않고</b> 뒤집는
    /// 이유는 이 파일이 잠그던 진짜 불변식이 "눈이 보인다"가 아니라
    /// <b>"몸과 초상화가 같은 그림이어야 한다"</b>였기 때문이다. 그 불변식은 지금도 유효하고,
    /// 방향만 반대다:
    ///
    ///  · 예전: 몸은 눈을 그리는데 초상화만 지웠다  → 결함
    ///  · 지금: 몸이 눈을 안 그리므로 초상화도 안 그려야 한다 → 이 파일이 그것을 잠근다
    ///
    /// 눈을 되살리는 날(상수 3개 되돌리기 — Editor/SceneBootstrapper.BakeEyes 문서 참고)에는 이 파일이
    /// <b>즉시 빨간불</b>이 되어 "초상화도 같이 되살려야 한다"고 알려 준다. 그것이 이 파일을 삭제하지
    /// 않는 실질적 이유다.
    ///
    /// ============================================================================
    /// 이 파일이 지키는 절대 조건
    /// ============================================================================
    ///  ① 미착용/EYES 전종 <b>모든 경우</b>에 초상화에 <c>EyeBack</c>/<c>EyeFront</c>가 <b>둘 다 없다</b>.
    ///  ② <b>공허한 통과 방지 1</b>: 같은 프레임에 머리(<c>Head</c> + <c>HeadFill</c>)가 <b>실제로 그려져
    ///     있다</b>. 이게 없으면 "그림 자체가 안 그려져서 눈도 없었다"를 구분할 수 없다.
    ///  ③ <b>공허한 통과 방지 2</b>: 각 회차에 해당 안경의 렌즈 도형이 <b>실제로 그려져 있다</b>
    ///     (= 장비가 진짜로 걸쳐졌다). 안경을 못 걸치면 EYES 경로를 하나도 안 밟은 셈이다.
    ///  ④ 한 종류도 착용하지 못했으면 <b>통과가 아니라 실패</b>다.
    ///
    /// 측정은 색/조건문이 아니라 <b>실제로 만들어진 LineRenderer 오브젝트 이름</b>으로 한다 —
    /// 프로덕션이 어떤 분기를 쓰든 "그림에 눈이 있는가"만 본다.
    /// </summary>
    public sealed class PortraitEyeVisibilityTests
    {
        private const string LogPrefix = "[초상화눈-TEST]";

        /// <summary>EYES 카테고리 <b>전종</b>의 자리와 그 아이템이 그리는 <b>가리개 판</b>의 도형 이름
        /// (<c>AccessoryShapeBuilder.AppendEyes</c>의 <c>Shape</c> 이름 그대로).
        /// 이름이 바뀌면 대조군이 즉시 실패해 알려 준다.
        /// <para>2026-09-01 카테고리당 +2종으로 4 -> 6이 됐다. 아래 개수와 카탈로그가 어긋나면
        /// 테스트 첫 줄이 그 사실을 먼저 말한다(그 단언이 이 목록의 유지보수 알람이다).</para>
        /// <para>★ 2026-09-01(같은 날, 늦게) 이름 4개가 바뀌었다 — EYES 6종이 "렌즈(윤곽선)"에서
        /// <b>불투명 바이저(채움)</b>로 재설계되면서다(38-7 E2). 이 목록은 <b>대조군</b>일 뿐이라
        /// 검사의 뜻(눈이 없다)은 그대로다. 옛 이름: GlassesLensFront / RoundLensFront /
        /// MonocleRing / BrowlineLensFront.</para></summary>
        private static readonly (int Index, string Label, string LensShape)[] Glasses =
        {
            (0, "선글라스",   "SunglassVisor"),
            (1, "동그란안경", "RoundPodFront"),
            (2, "고글",       "GoggleLens"),
            (3, "외알안경",   "MonoclePod"),
            (4, "뿔테안경",   "BrowlineVisor"),
            (5, "안대",       "PatchCover"),
        };

        private sealed class StillIntentSource : IMovementIntentSource
        {
            public float MoveInputX => 0f;
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }

        private CharacterInfoWindow _window;
        private StickmanAgent _agent;
        private IMovementIntentSource _originalIntent;
        private int _restoreWornEyes = -1;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            StickConfig config = _agent != null && _agent.Blackboard != null ? _agent.Blackboard.Config : null;
            EquipmentModel.TryWear(EquipmentSlot.Eyes, _restoreWornEyes, config);

            if (_agent != null && _agent.Blackboard != null && _originalIntent != null)
            {
                _agent.Blackboard.IntentSource = _originalIntent;
            }
            _agent = null;
            _originalIntent = null;

            if (_window != null) _window.Close("테스트 정리");
            _window = null;
            yield return null;
        }

        private static T ExactlyOne<T>() where T : Object
        {
            var found = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            Assert.AreEqual(1, found.Length, $"씬의 {typeof(T).Name} 개수가 {found.Length}개입니다 — 1개여야 합니다.");
            return found[0];
        }

        /// <summary>880 정보창이 쓰는 <b>주 촬영장</b>(구석 호버 패널의 2번 촬영장과 구분한다).</summary>
        private static CharacterPortraitStage PrimaryStage()
        {
            var found = Object.FindObjectsByType<CharacterPortraitStage>(FindObjectsSortMode.None);
            CharacterPortraitStage primary = null;
            int atPrimaryX = 0;
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] == null) continue;
                if (Mathf.Abs(found[i].transform.position.x - CharacterPortraitStage.StageWorldX) > 1f) continue;
                atPrimaryX++;
                primary = found[i];
            }
            Assert.AreEqual(1, atPrimaryX,
                $"{LogPrefix} X={CharacterPortraitStage.StageWorldX:F0}에 선 촬영장이 {atPrimaryX}개입니다 — 1개여야 합니다.");
            return primary;
        }

        private IEnumerator SetUpOpenWindow()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _window = ExactlyOne<CharacterInfoWindow>();
            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다.");
            Assert.IsNotNull(_agent.Blackboard, $"{LogPrefix} 블랙보드가 아직 만들어지지 않았습니다.");

            _originalIntent = _agent.Blackboard.IntentSource;
            _agent.Blackboard.IntentSource = new StillIntentSource();
            _restoreWornEyes = EquipmentModel.WornIndex(EquipmentSlot.Eyes);

            _window.Open("테스트");
            yield return null;
            yield return null;
        }

        /// <summary>미니 피규어에 그 이름의 선이 실제로 그려져 있는가.</summary>
        private static bool HasDrawnPart(CharacterPortraitStage stage, string partName)
        {
            Transform figure = stage.transform.Find("MiniFigure");
            Assert.IsNotNull(figure, $"{LogPrefix} 촬영장에서 MiniFigure를 찾지 못했습니다.");
            Transform part = figure.Find(partName);
            return part != null && part.GetComponent<LineRenderer>() != null;
        }

        /// <summary>공허한 통과 방지 — "눈이 없다"를 재기 전에 <b>그림이 실제로 그려져 있는지</b> 확인한다.</summary>
        private static void AssertFigureIsActuallyDrawn(CharacterPortraitStage stage, string what)
        {
            Assert.IsTrue(HasDrawnPart(stage, "Head"),
                $"{LogPrefix} {what} — 초상화에 머리 링(Head)이 없습니다. 그림 자체가 안 그려졌다면 " +
                "'눈이 없다'는 아무것도 증명하지 못합니다(대조군 실패).");
            Assert.IsTrue(HasDrawnPart(stage, "HeadFill"),
                $"{LogPrefix} {what} — 초상화에 머리 채움(HeadFill)이 없습니다. 실제 캐릭터는 꽉 찬 머리인데 " +
                "초상화만 빈 링이면 2026-08-31에 고친 것과 같은 유형의 이중 정의가 재발한 것입니다 " +
                "(docs/UX_FLOW.md 38-1 소비처 2).");
            Assert.IsTrue(HasDrawnPart(stage, "Torso"),
                $"{LogPrefix} {what} — 초상화에 몸통이 없습니다(대조군 실패).");
        }

        // ============================================================================
        // (1) 핵심 — EYES 전종 어느 것을 써도 눈이 없다 (+ 미착용 대조군)
        // ============================================================================
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator EyesAreAbsentUnderEveryGlassesItem()
        {
            yield return SetUpOpenWindow();
            var stage = PrimaryStage();
            StickConfig config = _agent.Blackboard.Config;

            Assert.AreEqual(Glasses.Length, EquipmentModel.ItemCount(EquipmentSlot.Eyes),
                $"{LogPrefix} EYES 카테고리의 아이템 수가 {EquipmentModel.ItemCount(EquipmentSlot.Eyes)}개로 바뀌었습니다 — " +
                "이 테스트의 목록(Glasses)을 함께 갱신해야 합니다.");

            // 대조군 0 — 아무것도 안 썼을 때. 그림은 있고 눈만 없어야 한다.
            EquipmentModel.TryWear(EquipmentSlot.Eyes, -1, config);
            yield return null;
            yield return null;
            AssertFigureIsActuallyDrawn(stage, "미착용");
            Assert.IsFalse(HasDrawnPart(stage, "EyeBack") || HasDrawnPart(stage, "EyeFront"),
                $"{LogPrefix} 미착용 상태에서 초상화에 눈이 그려졌습니다 — 실제 캐릭터에는 눈이 없습니다 " +
                "(Editor/SceneBootstrapper.BakeEyes=false). 두 소비처가 어긋났습니다.");

            int tested = 0;
            var skipped = new System.Text.StringBuilder();

            for (int i = 0; i < Glasses.Length; i++)
            {
                (int index, string label, string lens) = Glasses[i];

                if (!EquipmentModel.IsItemOwned(EquipmentSlot.Eyes, index))
                {
                    skipped.Append($" {label}(미보유)");
                    continue;
                }

                Assert.IsTrue(EquipmentModel.TryWear(EquipmentSlot.Eyes, index, config),
                    $"{LogPrefix} {label}을(를) 착용시키지 못했습니다 — 관측 전제가 깨졌습니다.");
                yield return null;
                yield return null;   // 장비 변경 -> 서명 변경 -> Rebuild가 도는 데 필요한 프레임.

                bool eyeBack = HasDrawnPart(stage, "EyeBack");
                bool eyeFront = HasDrawnPart(stage, "EyeFront");
                bool lensDrawn = HasDrawnPart(stage, lens);

                Debug.Log($"{LogPrefix} {label} 착용 — EyeBack={eyeBack}, EyeFront={eyeFront}, 렌즈({lens})={lensDrawn}.");

                // ② 그림이 실제로 그려져 있다.
                AssertFigureIsActuallyDrawn(stage, label);

                // ③ 대조군: 안경이 진짜로 그려졌는가. 아니면 EYES 경로를 한 번도 안 밟은 공허한 통과다.
                Assert.IsTrue(lensDrawn,
                    $"{LogPrefix} {label}을(를) 착용했는데 렌즈 도형 '{lens}'이 초상화에 없습니다 — " +
                    "이 회차는 아무것도 증명하지 못합니다(대조군 실패).");

                // ① 절대 조건.
                Assert.IsFalse(eyeBack,
                    $"{LogPrefix} {label} 착용 시 <뒤쪽 눈>이 그려졌습니다 — 실제 캐릭터에는 눈이 없습니다.");
                Assert.IsFalse(eyeFront,
                    $"{LogPrefix} {label} 착용 시 <앞쪽 눈>이 그려졌습니다 — 실제 캐릭터에는 눈이 없습니다.");
                tested++;
            }

            Debug.Log($"{LogPrefix} 검사한 안경 {tested}/{Glasses.Length}종." +
                (skipped.Length > 0 ? $" 건너뜀:{skipped}" : ""));

            // ④ 한 종류도 못 걸쳤으면 통과가 아니라 실패다.
            Assert.Greater(tested, 0,
                $"{LogPrefix} 착용 가능한 안경이 하나도 없어 아무것도 검사하지 못했습니다 — " +
                "요구 레벨/보유 조건을 확인하십시오(조용히 통과하는 테스트를 막기 위한 단언입니다).");
        }
    }
}
