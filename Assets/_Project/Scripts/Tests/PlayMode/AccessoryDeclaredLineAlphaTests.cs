using System.Collections;
using System.Collections.Generic;
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
    /// ★★ 2026-09-06 — <b>조각이 선언한 선 알파가 전역 페이드에 덮이지 않는다</b>(debugger 규명 → coder 수정).
    ///
    /// ============================================================================
    /// 무엇이 고장나 있었나
    /// ============================================================================
    /// 계약 v2 인계본 조각은 <c>lineAlpha</c>를 스스로 갖는다 — 하이라이트 0.42가 17건,
    /// 낱선 0.4/0.35/0.55가 6건, 합쳐 <b>23조각</b>이 1 미만의 선 알파를 선언한다.
    /// 그런데 <c>CharacterAccessoryRenderer.ApplyAlpha</c>가 전역 페이드 값을
    /// <b>대입</b>하고 있었다(<c>c.a = _alpha</c>). 재구성이 0.42로 구운 선이 <b>같은
    /// LateUpdate의 끝에서</b> 1.0으로 밀렸고, 그래서 선언된 부분투명은 <b>한 프레임도</b>
    /// 화면에 나온 적이 없다(실기 증거: 천 모자 관의 흰 하이라이트가 선명한 흰 획).
    ///
    /// 고침은 «대입 → 곱셈»이다: <c>c.a = 선언값 × _alpha</c>.
    ///
    /// ============================================================================
    /// 이 파일이 재는 두 가지
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>페이드가 없을 때</b>(<c>_alpha == 1</c>) 선언값이 그대로 남는가.
    ///     — 결함 상태에서는 모든 선이 1.0이므로 이 검사가 <b>직접</b> 빨개진다.</item>
    ///   <item><b>페이드 중</b>(<c>0 &lt; _alpha &lt; 1</c>) 조각 사이의 <b>비율</b>이 유지되는가.
    ///     — 페이드 자체(랙돌 진입/복귀)를 훼손하지 않았다는 반대쪽 잠금이기도 하다.</item>
    /// </list>
    ///
    /// <para><b>숫자를 베끼지 않는다</b>: 0.42를 단언에 쓰지 않는다. (1)은 «1 미만인 선과 1인 선이
    /// <b>공존</b>한다»로, (2)는 «(1)에서 <b>실측한</b> 비율이 페이드 내내 유지된다»로 잰다.
    /// 그래서 장비 담당이 하이라이트 알파를 0.38로 바꿔도 이 파일은 낡지 않는다.</para>
    ///
    /// <para><b>니들 <c>Piece_H3</c>는 존재 단언과 함께 쓴다</b>(CLAUDE.md 부재 단언 규칙):
    /// 그 이름이 사라지면 «부분투명 선이 없다»가 아니라 <b>«그 이름을 못 찾았다»로 빨개진다</b>.</para>
    /// </summary>
    public sealed class AccessoryDeclaredLineAlphaTests
    {
        private const string LogPrefix = "[선알파]";

        /// <summary>천 모자. ★ PlayMode 어셈블리에는 <c>InternalsVisibleTo</c>가 없어
        /// <c>AccessoryShapeBuilder.HeadCap</c>을 참조할 수 없다(이 어셈블리의 공통 사정).
        /// 번호가 재배치되면 <c>Wear</c>는 여전히 true이므로, 아래 <see cref="PartialAlphaPieceName"/>
        /// 존재 단언이 «이 번호가 아직 천 모자다»의 증인이 된다.</summary>
        private const int ClothHat = 0;

        /// <summary>천 모자의 <b>흰 하이라이트</b> 조각 — 이 저장소에서 «1 미만의 선 알파»의 증인.
        /// 사라지면 이 파일은 조용한 초록이 아니라 <b>빨간불</b>이 된다(존재 단언).</summary>
        private const string PartialAlphaPieceName = "Piece_H3";

        /// <summary>페이드가 끝나 <c>_alpha</c>가 1에 안착할 시간(벽시계). 프로덕션 페이드는
        /// 0.18초라 그 두 배 이상을 준다 — 프레임 수로 세지 않는다(배치 모드 2,000fps 규약).</summary>
        private const float SettleSeconds = 0.6f;

        /// <summary>
        /// ★ 2026-09-06 실측 — <b><see cref="LineRenderer"/>의 시작/끝 색은 8비트로 양자화된다.</b>
        /// 내부적으로 <c>Gradient</c>에 담기고 그 키가 채널당 1바이트라, 0.42를 넣고 되읽으면
        /// <c>107/255 = 0.419608</c>이 나온다.
        ///
        /// <para>그래서 이 검사는 <b>비율</b>이 아니라 <b>절대 알파</b>를 본다. 비율로 재면 분모(기준 선의
        /// 알파)가 작아질수록 같은 1LSB 오차가 부풀어, 실측에서 기준 알파 0.05 부근에서만
        /// 0.086(= 22/255)까지 튀었다 — 프로덕션은 멀쩡한데 테스트만 빨개지는 형태다.</para>
        ///
        /// <para>허용치 <b>2/255</b>의 유도: 관측값과 기대값이 각각 한 번씩 반올림되므로 각 1/510,
        /// 합쳐 1/255가 상한이고 여기에 2배 여유를 뒀다. 결함이 되살아나면 어긋남이
        /// <c>1 − 선언비율</c>(하이라이트에서 약 0.58 = 148/255) 규모라 이 문턱과 <b>두 자릿수</b>
        /// 차이가 난다 — 여유를 줘도 판별력은 그대로다.</para>
        /// </summary>
        private const float AlphaQuantizationSlack = 2f / 255f;

        [UnityTearDown]
        public IEnumerator TearDownAll()
        {
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            yield return null;
        }

        // ============================================================================
        // (1) 페이드가 없을 때 — 선언값이 그대로 살아 있다
        // ============================================================================

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 조각이_선언한_부분투명_선이_전역알파에_덮이지_않는다()
        {
            yield return LoadSceneAndPinIdle();
            yield return WearClothHat();

            Transform container = AccessoryContainer();
            List<LineRenderer> lines = LinesUnder(container);

            var opaque = new List<string>();
            var partial = new List<string>();
            LineRenderer witness = null;
            for (int i = 0; i < lines.Count; i++)
            {
                float a = lines[i].startColor.a;
                if (a >= 0.99f) opaque.Add($"{lines[i].name}={a:F3}");
                else partial.Add($"{lines[i].name}={a:F3}");
                if (lines[i].name == PartialAlphaPieceName) witness = lines[i];
            }

            Debug.Log($"{LogPrefix} 천 모자 선 {lines.Count}개 — 불투명 [{string.Join(", ", opaque)}] / " +
                $"부분투명 [{string.Join(", ", partial)}].");

            // 존재 단언(니들이 썩으면 조용한 초록이 아니라 빨강) — 이 이름이 없으면 아래 판정은
            // 「부분투명이 없다」가 아니라 「그 조각을 못 찾았다」다.
            Assert.IsNotNull(witness,
                $"{LogPrefix} 천 모자에서 '{PartialAlphaPieceName}'을 찾지 못했습니다(그려진 선: " +
                $"[{string.Join(", ", lines.ConvertAll(l => l.name))}]). HEAD {ClothHat}번이 더 이상 " +
                "천 모자가 아니거나 하이라이트 조각의 이름이 바뀌었습니다 — 어느 쪽이든 이 검사는 " +
                "지금 <b>다른 것을 재고 있습니다</b>.");

            // 대조군 — 「전부 반투명」이 아니라는 것. 이것이 없으면 아래 단언은 머티리얼/셰이더가
            // 통째로 반투명해진 세상에서도 초록이다.
            Assert.IsNotEmpty(opaque,
                $"{LogPrefix} 불투명한 선이 하나도 없습니다 — 페이드가 아직 안 끝났거나(_alpha < 1) " +
                "전역 알파가 통째로 낮아졌습니다. 그렇다면 아래 판정은 아무것도 증명하지 못합니다.");

            // ★ 핵심 — 결함 상태에서는 여기가 빨개진다(ApplyAlpha가 전부 1.0으로 밀었으므로).
            Assert.Less(witness.startColor.a, 0.99f,
                $"{LogPrefix} '{PartialAlphaPieceName}'의 선 알파가 {witness.startColor.a:F3}입니다 — " +
                "조각이 선언한 부분투명(계약 v2 lineAlpha)이 전역 페이드 값으로 <b>대체</b>됐습니다. " +
                "ApplyAlpha가 «대입»으로 되돌아갔는지 확인하십시오(고침은 «선언값 × _alpha» 곱셈입니다).");
            Assert.Greater(witness.startColor.a, 0.01f,
                $"{LogPrefix} '{PartialAlphaPieceName}'의 선 알파가 {witness.startColor.a:F3}이라 " +
                "사실상 보이지 않습니다 — 곱셈의 한쪽이 0이 됐습니다(선언값을 못 캡처했거나 _alpha가 0).");

            // 선과 <b>끝색</b>이 함께 간다(둘 중 하나만 밀면 선이 그라데이션이 된다).
            Assert.AreEqual(witness.startColor.a, witness.endColor.a, 1e-4f,
                $"{LogPrefix} '{PartialAlphaPieceName}'의 시작/끝 알파가 다릅니다.");
        }

        // ============================================================================
        // (2) 페이드 중 — 조각 사이의 비율이 유지된다
        // ============================================================================
        //
        // 페이드를 <b>실제로</b> 태운다: 랙돌은 전신을 물리에 위임해 액세서리가 몸에서 떨어져 나가므로
        // 렌더러가 스스로 사라진다(ResolveWantVisible). 몸은 그대로 보이므로 «즉시 0» 경로가 아니라
        // 0.18초짜리 <b>점진 페이드</b>가 돈다 — 이 검사가 필요한 바로 그 구간이다.

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 페이드_중에도_조각_사이의_알파_비율이_유지된다()
        {
            yield return LoadSceneAndPinIdle();
            yield return WearClothHat();

            Transform container = AccessoryContainer();
            List<LineRenderer> lines = LinesUnder(container);

            LineRenderer witness = lines.Find(l => l.name == PartialAlphaPieceName);
            Assert.IsNotNull(witness,
                $"{LogPrefix} '{PartialAlphaPieceName}'을 찾지 못했습니다 — (1)의 존재 단언과 같은 사정입니다.");

            LineRenderer reference = lines.Find(l => l != witness && l.startColor.a >= 0.99f);
            Assert.IsNotNull(reference,
                $"{LogPrefix} 기준이 될 불투명 선이 없습니다 — 비율을 잴 짝이 없습니다.");

            // 기준 비율은 <b>지금 실측</b>한다. 0.42를 베끼지 않는 이유(클래스 문서).
            float baseline = witness.startColor.a / reference.startColor.a;
            Assert.Less(baseline, 0.99f,
                $"{LogPrefix} 실측 기준 비율이 {baseline:F4}라 1과 구분되지 않습니다 — " +
                "«비율이 유지되는가»는 비율이 1이면 아무것도 증명하지 못합니다.");

            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            agent.Blackboard.Machine.ChangeState(StickmanStateId.Ragdoll, isForcedInterrupt: true);

            int samples = 0;
            float worstDelta = 0f;
            float minSeen = 1f, maxSeen = 0f;
            // 예산은 프로덕션 페이드(0.18초)보다 넉넉히 — 프레임 수가 아니라 벽시계다.
            yield return TestClock.SampleForSeconds(1.2f, _ =>
            {
                if (witness == null || reference == null) return false;
                float refA = reference.startColor.a;
                // 양 끝(0/1)은 분해능이 없다 — 가운데 구간만 표본으로 삼는다.
                if (refA <= 0.02f || refA >= 0.98f) return true;
                // ★ <b>비율</b>이 아니라 <b>절대 알파</b>로 잰다(아래 양자화 문단).
                worstDelta = Mathf.Max(worstDelta, Mathf.Abs(witness.startColor.a - baseline * refA));
                minSeen = Mathf.Min(minSeen, refA);
                maxSeen = Mathf.Max(maxSeen, refA);
                samples++;
                return true;
            });

            Debug.Log($"{LogPrefix} 페이드 표본 {samples}장 — 기준 알파 {minSeen:F3}~{maxSeen:F3} 구간에서 " +
                $"기대값(비율 {baseline:F4} × 기준) 대비 최대 어긋남 {worstDelta:E3} " +
                $"(= {worstDelta * 255f:F2}/255, 허용 {AlphaQuantizationSlack * 255f:F0}/255).");

            // 대조군 — 페이드가 <b>실제로</b> 돌았는가. 표본이 없으면 아래 상한은 공허하다.
            Assert.Greater(samples, 10,
                $"{LogPrefix} 페이드 중간 구간의 표본이 {samples}장뿐입니다 — 페이드가 돌지 않았거나 " +
                "(랙돌 전이가 튕겼거나 몸이 통째로 숨어 «즉시 0» 경로를 탔거나) 관측 창이 어긋났습니다. " +
                "그러면 아래 상한은 아무것도 검증하지 못합니다.");
            Assert.Greater(maxSeen - minSeen, 0.2f,
                $"{LogPrefix} 기준 알파가 {minSeen:F3}~{maxSeen:F3}밖에 안 움직였습니다 — " +
                "페이드를 «지나가는 중»으로 보지 못했습니다.");

            Assert.Less(worstDelta, AlphaQuantizationSlack,
                $"{LogPrefix} 페이드 중 하이라이트 선의 알파가 «선언 비율 × 전역 알파»에서 최대 " +
                $"{worstDelta:E3}(= {worstDelta * 255f:F2}/255) 어긋났습니다 — 전역 페이드가 조각의 " +
                "선언값을 여전히 «대체»하고 있습니다(대체였다면 두 선의 알파가 <b>같아져</b> " +
                $"어긋남이 대략 {1f - baseline:F2}까지 벌어집니다).");
        }

        // ==================== 유틸 ====================

        private static Transform AccessoryContainer()
        {
            var renderer = Object.FindFirstObjectByType<CharacterAccessoryRenderer>();
            Assert.IsNotNull(renderer, $"{LogPrefix} 씬에 CharacterAccessoryRenderer가 없습니다.");
            Transform container = FindChild(renderer.transform, "EquipmentAccessories");
            Assert.IsNotNull(container,
                $"{LogPrefix} EquipmentAccessories 컨테이너가 없습니다 — 재구성이 안 돌았습니다.");
            return container;
        }

        private static List<LineRenderer> LinesUnder(Transform container)
        {
            var lines = new List<LineRenderer>(container.GetComponentsInChildren<LineRenderer>(true));
            Assert.Greater(lines.Count, 1,
                $"{LogPrefix} 액세서리 선이 {lines.Count}개뿐입니다 — 비교할 짝이 없습니다.");
            return lines;
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            return null;
        }

        private IEnumerator WearClothHat()
        {
            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            StickConfig config = agent.Config;
            RaiseLevelTo(24, config);

            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                EquipmentModel.TryWear((EquipmentSlot)i, EquipmentModel.NotWorn, config);
            }
            EquipmentModel.TryWear(EquipmentSlot.Head, ClothHat, config);
            Assert.AreEqual(ClothHat, EquipmentModel.WornIndex(EquipmentSlot.Head),
                $"{LogPrefix} 천 모자를 걸치지 못했습니다.");

            // ★ 페이드가 1에 안착할 때까지 <b>벽시계</b>로 기다린다. 프레임 수로 세면 배치 모드에서
            //   0.01초짜리 대기가 되어, 아직 페이드 중인 값을 «선언값»으로 오독한다.
            yield return TestClock.SampleForSeconds(SettleSeconds, _ => { });
        }

        private static void RaiseLevelTo(int level, StickConfig config)
        {
            for (int guard = 0; guard < 4096 && CharacterProgressionModel.Level < level; guard++)
            {
                CharacterProgressionModel.AddXp(CharacterProgressionModel.XpToNextLevel(config) + 1f, config);
            }
            Assert.GreaterOrEqual(CharacterProgressionModel.Level, level, $"{LogPrefix} 레벨을 못 올렸습니다.");
        }

        private IEnumerator LoadSceneAndPinIdle()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(agent, $"{LogPrefix} StickmanAgent가 없습니다.");
            Assert.IsNotNull(agent.Blackboard, $"{LogPrefix} 블랙보드가 없습니다.");
            agent.Blackboard.IntentSource = new StillSource();

            float deadline = Time.realtimeSinceStartup + 15f;
            float idleSince = -1f;
            StickmanStateId last = agent.Blackboard.Machine.CurrentStateId;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                last = agent.Blackboard.Machine.CurrentStateId;
                if (last != StickmanStateId.Idle) { idleSince = -1f; continue; }
                if (idleSince < 0f) idleSince = Time.realtimeSinceStartup;
                if (Time.realtimeSinceStartup - idleSince >= 0.5f) break;
            }
            Assert.AreEqual(StickmanStateId.Idle, last, $"{LogPrefix} Idle로 안정되지 않았습니다.");
        }

        private sealed class StillSource : IMovementIntentSource
        {
            public float MoveInputX => 0f;
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }
    }
}
