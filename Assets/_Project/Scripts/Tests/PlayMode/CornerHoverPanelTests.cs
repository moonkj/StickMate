using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 구석 호버 패널(docs/UX_FLOW.md 34-4~34-6)의 <b>배선 + 비침해</b> 회귀 잠금.
    ///
    /// ============================================================================
    /// 이 파일이 잡으려는 첫 번째 실패는 "존재하지 않는다"이다
    /// ============================================================================
    /// 33-9 #10이 미리 경고했고 실제로 Blocker B1로 터진 사고: <b>신규 컴포넌트를 프리팹에 얹는 것을
    /// 잊어</b> 클래스는 컴파일되는데 런타임에는 아무것도 없는 상태. 34-9 #10이 같은 함정을 이 패널에
    /// 대해 다시 예고했으므로, 그 배선을 여기서 실행으로 확인한다.
    ///
    /// ============================================================================
    /// 두 번째는 <b>비침해</b>다 (34-8 / 절대 불변 원칙 2)
    /// ============================================================================
    ///  · 숨어 있는 동안 클릭 차단막이 꺼져 있어야 한다 — 그 구석의 클릭관통이 100% 그대로여야 한다.
    ///  · 전체화면 게임이 감지되면 <b>연출 없이 그 프레임에</b> 차단막까지 거둬야 한다.
    /// 이 둘은 "안 보이니까 괜찮겠지"로 넘어가기 쉬운데, macOS 히트테스트는 커서 아래 <b>콜라이더</b>를
    /// 보므로 그림이 없어도 차단막이 살아 있으면 클릭을 먹는다(PopoverPanel이 실제로 겪은 사고).
    ///
    /// <b>커서 좌표를 만들 수 없는 환경</b>(배치 모드/헤드리스)에서는 이 패널이 스스로 비활성화된다
    /// (34-4-6의 첫 번째 "거부" 상태). 그 경우에도 "차단막이 꺼져 있다"는 단언은 그대로 유효하며,
    /// 오히려 <b>가장 중요한 보장</b>이다 — 쓰지 않는 기능이 클릭관통을 막고 있으면 안 된다.
    /// </summary>
    public sealed class CornerHoverPanelTests
    {
        private const string LogPrefix = "[CORNER-PANEL-TEST]";

        private StickmanAgent _agent;
        private CornerHoverPanel _panel;

        /// <summary>전체화면 감지를 실제로 일으킬 수 없으므로(다른 앱을 전체화면으로 만들 방법이 없다)
        /// 소비자가 읽는 값 하나(<c>_isSuspended</c>)를 직접 주입한다 —
        /// FullscreenSuspendUiHidingTests가 이미 쓰는 이 프로젝트의 관례이며, 소비 경로는 완전히 같다.</summary>
        private static readonly System.Reflection.FieldInfo SuspendedField =
            typeof(StickmanAgent).GetField("_isSuspended",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        private void SetSuspended(bool on)
        {
            Assert.IsNotNull(SuspendedField, $"{LogPrefix} StickmanAgent._isSuspended 필드를 찾지 못했습니다.");
            SuspendedField.SetValue(_agent, on);
        }

        [TearDown]
        public void RestoreAgent()
        {
            if (_agent != null && SuspendedField != null) SuspendedField.SetValue(_agent, false);
            if (_panel != null) _panel.HoldStageForTests = false;
        }

        private IEnumerator SetUp()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다.");
            yield return new WaitForSeconds(1.0f);

            _panel = _agent.GetComponent<CornerHoverPanel>();
        }

        [UnityTest]
        public IEnumerator 패널_컴포넌트가_캐릭터_프리팹에_실제로_붙어_있다()
        {
            yield return SetUp();

            Assert.IsNotNull(_panel,
                $"{LogPrefix} CornerHoverPanel이 캐릭터에 없습니다 — Assets/Editor/SceneBootstrapper.cs의 " +
                "AddComponent/EnsurePrefabComponents 배선이 빠졌습니다(33-9 #10과 같은 Blocker: " +
                "클래스는 컴파일되는데 런타임에는 아무것도 존재하지 않습니다).");

            Debug.Log($"{LogPrefix} 배선 확인 — 활성 {_panel.enabled}, 보임 {_panel.IsVisible}, " +
                $"차단막 {_panel.IsClickBlockerEnabled}, 다이얼 값 {_panel.DialValue:F2}.");
        }

        [UnityTest]
        public IEnumerator 숨어_있는_동안_클릭_차단막이_꺼져_있다()
        {
            yield return SetUp();
            Assert.IsNotNull(_panel);

            Assert.IsFalse(_panel.IsVisible,
                $"{LogPrefix} 아무 조작도 없었는데 패널이 보입니다 — 감지 영역에 커서를 두지 않았는데 " +
                "열렸다면 호버 판정이 항상 참입니다.");
            Assert.IsFalse(_panel.IsClickBlockerEnabled,
                $"{LogPrefix} 패널이 숨어 있는데 클릭 차단막이 켜져 있습니다 — 화면 좌하단의 클릭관통이 " +
                "이유 없이 해제된 채 남습니다(비침해 원칙 2 위반).");

            // 몇 초 동안 계속 꺼져 있어야 한다(폴링이 도는 동안 한 번이라도 켜지면 실패).
            for (int i = 0; i < 120; i++)
            {
                yield return null;
                if (_panel.IsClickBlockerEnabled)
                    Assert.Fail($"{LogPrefix} {i}프레임째에 차단막이 켜졌습니다 — 숨어 있는 동안에는 " +
                        "화면 좌하단에 콜라이더가 하나도 없어야 합니다.");
            }
        }

        [UnityTest]
        public IEnumerator 전체화면_감지에서_즉시_거둔다()
        {
            yield return SetUp();
            Assert.IsNotNull(_panel);

            // 감지 경로 없이도 상태를 직접 밀어 넣어 "보이는 상태"를 만든다(배치 모드에는 전역 커서가 없다).
            // HoldStageForTests가 없으면 다음 프레임에 "이 플랫폼에는 호버가 없음"으로 즉시 다시 숨는다.
            _panel.HoldStageForTests = true;
            _panel.ForceStageForTests(expanded: false);
            yield return null;
            Assert.IsTrue(_panel.IsVisible, $"{LogPrefix} 테스트용 강제 표시가 먹지 않았습니다.");

            SetSuspended(true);
            yield return null;
            yield return null;

            Assert.IsFalse(_panel.IsVisible,
                $"{LogPrefix} 전체화면 감지 후에도 패널이 보입니다 — 접힘 연출을 기다리면 그동안 차단막이 " +
                "살아 있어 전체화면 게임 위에서 클릭을 먹습니다(PopoverPanel이 겪은 사고와 같은 유형).");
            Assert.IsFalse(_panel.IsClickBlockerEnabled,
                $"{LogPrefix} 전체화면 감지 후에도 차단막이 켜져 있습니다(비침해 원칙 2).");

            SetSuspended(false);
            yield return null;
            yield return null;

            Assert.IsFalse(_panel.IsVisible,
                $"{LogPrefix} 전체화면에서 빠져나오자마자 패널이 스스로 다시 열렸습니다 — 사용자가 부르지도 " +
                "않은 UI가 게임을 끄는 순간 튀어나오면 그 자체가 방해입니다.");
        }

        /// <summary>
        /// ★★ <b>끝에서 끝까지</b> — 다이얼을 실제로 "돌려서" 캐릭터 크기가 정말 바뀌는가.
        ///
        /// 이 테스트가 없으면 원칙 1 위반이 조용히 통과한다: 다이얼 숫자만 바뀌고 캐릭터는 그대로인
        /// 상태(34-3-6이 "절대 허용하지 않는다"고 못박은 바로 그 실패)가 컴파일도 되고 화면도 나온다.
        /// 그래서 <b>실제 입력과 같은 처리 경로</b>(ProcessPointer)에 커서를 먹여, 표시 값과
        /// <see cref="StickmanAgent.CurrentCharacterScale"/>과 저장 모델이 <b>셋 다</b> 같은 값이 되는지 본다.
        /// </summary>
        [UnityTest]
        public IEnumerator 다이얼을_돌리면_캐릭터_크기가_실제로_바뀐다()
        {
            yield return SetUp();
            Assert.IsNotNull(_panel);

            // 배포 에셋을 건드리지 않는다 — ApplyCharacterScale은 에이전트의 _config에 값을 쓴다.
            StickConfig original = _agent.Config;
            StickConfig clone = Object.Instantiate(original);
            AgentConfigField.SetValue(_agent, clone);
            _agent.Blackboard.Config = clone;
            float restore = _agent.CurrentCharacterScale;

            try
            {
                _panel.HoldStageForTests = true;
                _panel.ForceStageForTests(expanded: false);
                yield return new WaitForSecondsRealtime(0.6f);   // 펼침/접힘 애니메이션이 끝날 때까지.

                float px = Platform.ScreenCoordinateConverter.CanvasToUnityScreen(1f, clone);
                Vector2 center = new Vector2(_panel.PanelScreenRect.xMin + 132f * px,
                                             _panel.PanelScreenRect.yMin + 78f * px);

                const int TargetIndex = 25;                       // 0.35 + 25 × 0.05 = 1.60
                float target = SizeDialWidget.IndexToValue(TargetIndex);
                Vector2 from = PointOnRing(center, px, SizeDialWidget.ValueToIndex(_panel.DialValue));
                Vector2 to = PointOnRing(center, px, TargetIndex);

                Debug.Log($"{LogPrefix} 다이얼 드래그 — 중심 {center}, 시작 {from}(값 {_panel.DialValue:F2}) " +
                    $"→ 끝 {to}(목표 {target:F2}), 패널 {_panel.PanelScreenRect}.");

                _panel.FeedPointerForTests(true, from);
                yield return null;
                _panel.FeedPointerForTests(true, to);
                yield return null;
                _panel.FeedPointerForTests(false, to);
                yield return null;
                yield return null;

                Assert.AreEqual(target, _panel.DialValue, 0.001f,
                    $"{LogPrefix} 다이얼 표시 값이 목표 눈금으로 가지 않았습니다(각도→값 매핑 확인).");
                Assert.AreEqual(target, _agent.CurrentCharacterScale, 0.02f,
                    $"{LogPrefix} 다이얼은 {_panel.DialValue:F2}×인데 캐릭터는 " +
                    $"{_agent.CurrentCharacterScale:F2}×입니다 — 표시와 행동이 어긋났습니다(절대 불변 원칙 1 위반).");
                Assert.AreEqual(target, UiLayoutModel.CharacterScale, 0.001f,
                    $"{LogPrefix} 저장 모델에 값이 반영되지 않아 재시작하면 크기가 되돌아갑니다.");

                Debug.Log($"{LogPrefix} 적용 확인 — 다이얼 {_panel.DialValue:F2}× / 캐릭터 " +
                    $"{_agent.CurrentCharacterScale:F2}× / 저장 {UiLayoutModel.CharacterScale:F2}× / " +
                    $"전신 {_agent.Metrics.TotalHeight:F4}유닛.");
            }
            finally
            {
                _panel.HoldStageForTests = false;
                _agent.ApplyCharacterScale(restore, "테스트 정리");
                AgentConfigField.SetValue(_agent, original);
                if (_agent.Blackboard != null) _agent.Blackboard.Config = original;
                if (clone != null) Object.Destroy(clone);
                UiLayoutModel.ResetForTesting();
            }
        }

        /// <summary>눈금 i가 놓인 자리(Unity 스크린 픽셀). θ = −132° + i × 8°, 방향 = (sin θ, −cos θ).</summary>
        private static Vector2 PointOnRing(Vector2 center, float pixelsPerPoint, int index)
        {
            const float RingRadiusPoints = 60f;   // 히트 원환(20~90pt)의 한가운데.
            float degrees = -132f + index * 8f;
            float rad = degrees * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad));
            return center + dir * (RingRadiusPoints * pixelsPerPoint);
        }

        private static readonly System.Reflection.FieldInfo AgentConfigField =
            typeof(StickmanAgent).GetField("_config",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        /// <summary>
        /// ★ 원칙 1(행동-텍스트 싱크)의 다이얼 판 — <b>표시 숫자와 실제 적용 값이 같은 곳에서 나온다.</b>
        /// 눈금은 0.05 간격에 스냅되므로 "켜진 눈금 수 = 표시 숫자 = 실제 배율"이 구조적으로 일치한다.
        /// </summary>
        [Test]
        public void 다이얼_눈금은_설정_범위를_그대로_34칸으로_나눈다()
        {
            Assert.AreEqual(34, SizeDialWidget.TickCount,
                "눈금 개수가 34가 아닙니다 — StickConfig.Min/MaxCharacterScale 또는 ValueStep이 바뀌었다면 " +
                "34-3-2의 각도 배치(264° 스윕 / 12시 96° 비움)도 함께 재유도해야 합니다.");

            Assert.AreEqual(StickConfig.MinCharacterScale, SizeDialWidget.IndexToValue(0), 1e-4f);
            Assert.AreEqual(StickConfig.MaxCharacterScale,
                SizeDialWidget.IndexToValue(SizeDialWidget.TickCount - 1), 1e-4f);

            // 표시 문자열과 값이 같은 인덱스에서 파생되는가(원칙 1의 구조적 보장).
            for (int i = 0; i < SizeDialWidget.TickCount; i++)
            {
                float v = SizeDialWidget.IndexToValue(i);
                Assert.AreEqual(i, SizeDialWidget.ValueToIndex(v),
                    $"인덱스 {i}(값 {v:F2})의 왕복이 깨졌습니다 — 표시와 적용이 갈라질 수 있습니다.");
                StringAssert.Contains(v.ToString("0.00"), SizeDialWidget.FormatValue(v));
            }
        }

        /// <summary>
        /// ★ 2026-08-31(통합검증 R2, M2) — 카드 하단과 다이얼 원환 상단이 정확히 맞닿도록 손으로 맞춘
        /// 상수 4개(패널 펼침 높이 / 카드 높이·들림값 / 다이얼 중심 높이·원환 바깥반지름)가 지금까지
        /// 어디에도 적혀 있지 않고 잠긴 적도 없었다. 넷 중 하나만 바뀌면 원환이 카드 밑을 먹거나(그러면
        /// M1이 고친 "빈 구역 탭" 게이트가 카드 영역까지 넓어져 클릭 오작동이 재발한다) 둘 사이에 보기
        /// 싫은 틈이 생긴다. 이 등식이 깨지면 넷 중 무엇을 바꿔야 하는지는 각 상수 선언부의 문서를 봐라.
        /// </summary>
        [Test]
        public void 카드_하단과_다이얼_원환_상단이_정확히_맞닿는다()
        {
            float cardBottomFromPanelBottom = CornerHoverPanel.ExpandedHeightPoints
                - CornerHoverPanel.CardRisePoints - CornerHoverPanel.CardHeightPoints;
            float ringTopFromPanelBottom = CornerHoverPanel.DialCenterFromBottomPoints
                + SizeDialWidget.HitOuterRadius;

            Assert.AreEqual(ringTopFromPanelBottom, cardBottomFromPanelBottom, 0.01f,
                $"{LogPrefix} 카드 하단({cardBottomFromPanelBottom}pt)과 다이얼 원환 상단" +
                $"({ringTopFromPanelBottom}pt)이 어긋났습니다 — ExpandedHeightPoints/CardRisePoints/" +
                "CardHeightPoints/DialCenterFromBottomPoints/HitOuterRadius 중 하나만 고치고 나머지를 " +
                "안 맞춘 것입니다.");
        }
    }
}
