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
            //
            // ★ 2026-09-01 — 예전에는 `for (int i = 0; i < 120; i++)`였다. 주석은 "몇 초 동안"이라고
            //   적혀 있었지만 배치 모드(0.11~0.45ms/프레임)에서 120프레임은 실제로
            //   <b>0.013~0.054초</b>였다. 이 패널의 호버 폴링 주기는 0.05초
            //   (CornerHoverPanel.PollInterval)이므로, 프레임이 빠른 쪽에서는 폴링이
            //   <b>단 한 번도 돌지 않은 채</b> 루프가 끝났다 — "폴링이 도는 동안"을 재겠다는
            //   이 테스트가 폴링을 한 번도 못 본 것이다(거짓 통과).
            //   2초면 폴링이 40회 돈다 — 프레임률과 무관하게 성립한다.
            const float WatchSeconds = 2f;
            yield return TestClock.SampleForSeconds(WatchSeconds, elapsed =>
            {
                if (_panel.IsClickBlockerEnabled)
                    Assert.Fail($"{LogPrefix} {elapsed:F3}초째에 차단막이 켜졌습니다 — 숨어 있는 동안에는 " +
                        "화면 좌하단에 콜라이더가 하나도 없어야 합니다.");
            });
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

                const int TargetIndex = 18;                       // 0.35 + 18 × 0.05 = 1.25
                Assert.Less(TargetIndex, SizeDialWidget.TickCount,
                    $"{LogPrefix} 목표 눈금이 범위 밖입니다 — 상한이 또 바뀌었다면 이 인덱스도 함께 낮추세요.");
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

        /// <summary>눈금 i가 놓인 자리(Unity 스크린 픽셀). 방향 = (sin θ, −cos θ).
        /// <para>★ 각도는 <see cref="SizeDialWidget.DegreesForIndex"/>에서 <b>받아온다</b>. 예전에는
        /// <c>−132° + i × 8°</c>를 여기 베껴 적었는데, 2026-08-31에 상한이 2.0 → 1.5로 바뀌어 눈금이
        /// 34 → 24칸이 되자 그 식은 <b>존재하지 않는 각도</b>를 가리키게 된다 — 테스트가 링 밖을 눌러
        /// "다이얼이 안 먹는다"고 거짓 실패했을 것이다.</para></summary>
        private static Vector2 PointOnRing(Vector2 center, float pixelsPerPoint, int index)
        {
            const float RingRadiusPoints = 60f;   // 히트 원환(20~90pt)의 한가운데.
            float degrees = SizeDialWidget.DegreesForIndex(index);
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
        public void 다이얼_눈금은_설정_범위를_그대로_24칸으로_나눈다()
        {
            // ★ 2026-08-31 사용자 지시 "캐릭터 사이즈는 max를 1.5까지만" — 상한 2.0 → 1.5, 34칸 → 24칸.
            Assert.AreEqual(24, SizeDialWidget.TickCount,
                "눈금 개수가 24가 아닙니다 — StickConfig.Min/MaxCharacterScale 또는 ValueStep이 바뀌었다면 " +
                "SizeDialWidget 클래스 문서의 각도 배치(스윕 = (칸−1) × 8°)도 함께 갱신하세요.");

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

        // ============================================================================
        // ★ 2026-08-31 사용자 신고: "크기조절 원이 먼저 떠 있고 상자가 나중에 커짐"
        // ============================================================================
        //
        // 근본 원인: 다이얼과 카드는 패널의 <b>자식</b>인데 패널에는 마스크가 없어서, 상자 크기
        // (_peekBlend)와 내용물의 보임 여부가 서로 <b>다른 출처</b>에서 나왔다. PEEK(104×14pt)에서도
        // 다이얼(눈금이 패널 바닥 기준 27~129pt를 차지)이 100% 그려져 원이 허공에 떠 있었다.
        //
        // 기존 테스트가 이걸 놓친 이유: 위의 잠금들은 전부 <b>정적</b>이거나(상수 등식) <b>애니메이션이
        // 끝난 뒤</b>를 본다(E2E는 0.6초를 기다리고 시작한다). "끝난 그림"은 처음부터 옳았다 —
        // 틀린 것은 <b>시간축의 순서</b>뿐이었다. 그래서 아래 둘은 t를 명시적으로 본다.

        /// <summary>
        /// ★ 기하 불변식 — 내용물이 <b>조금이라도 보이는 순간</b>에는 다이얼 눈금이 상자 안에 있다.
        ///
        /// <para>게이트 값 0.9는 취향이 아니라 유도된 수다: 눈금 꼭대기(78 + 51 = 129pt)가 상자
        /// 안에 들어가는 최소 블렌드가 0.858이다. 상자/다이얼 상수 중 하나만 바뀌어 그 부등식이
        /// 깨지면 "원이 상자 밖에 삐져나오는" 그 사고가 그대로 재발한다.</para>
        /// </summary>
        [Test]
        public void 내용물_게이트에서_다이얼이_상자_안에_완전히_들어간다()
        {
            // _expand = 0 이 최악의 경우다(펼치면 상자는 더 커지기만 한다).
            Vector2 box = CornerHoverPanel.PanelSizePointsAt(CornerHoverPanel.ContentGateBlend, 0f);

            float tickTop = CornerHoverPanel.DialCenterFromBottomPoints + SizeDialWidget.TickVisualOuterRadius;
            // 다이얼 중심 x = 다 펼쳤을 때 패널 폭의 절반(ApplyLayout이 쓰는 그 수).
            float tickRight = CornerHoverPanel.PanelSizePointsAt(1f, 0f).x * 0.5f
                + SizeDialWidget.TickVisualOuterRadius;

            Assert.GreaterOrEqual(box.y, tickTop,
                $"{LogPrefix} 내용물이 뜨기 시작하는 블렌드({CornerHoverPanel.ContentGateBlend})에서 " +
                $"상자 높이가 {box.y:F1}pt인데 다이얼 눈금은 {tickTop:F1}pt까지 뻗습니다 — 원이 상자 " +
                "위로 삐져나온 채 보입니다(사용자 신고 '원이 먼저 떠 있고 상자가 나중에 커짐'의 재발). " +
                "ContentGateBlend를 올리거나 다이얼/상자 상수를 되돌리세요.");

            Assert.GreaterOrEqual(box.x, tickRight,
                $"{LogPrefix} 같은 블렌드에서 상자 폭 {box.x:F1}pt < 눈금 오른쪽 끝 {tickRight:F1}pt입니다.");

            // 게이트가 의미를 가지려면 "상자가 다 자라기 전"이어야 한다(1.0이면 게이트가 아니다).
            Assert.Less(CornerHoverPanel.ContentGateBlend, 1.0001f);
            Assert.Greater(CornerHoverPanel.ContentGateBlend, 0.5f,
                $"{LogPrefix} 게이트가 너무 낮습니다 — 상자가 절반도 안 자란 때 원이 뜨면 신고된 그림과 같아집니다.");
        }

        /// <summary>
        /// ★★ 시간축 회귀 — <b>상자가 먼저, 원이 나중</b>. 반대로 뒤집히면 실패한다.
        ///
        /// <para>매 프레임 (상자 성장 진행도, 다이얼 알파)를 함께 찍어서
        /// <c>다이얼 알파 &gt; 0 ⇒ 상자가 게이트 이상</c>을 <b>모든 프레임</b>에서 확인한다.
        /// 열림뿐 아니라 <b>닫힘</b>도 본다 — 닫힐 때 원이 남고 상자만 줄어드는 것도 같은 버그다.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator 상자가_다_자란_뒤에_다이얼이_나타난다()
        {
            yield return SetUp();
            Assert.IsNotNull(_panel);

            _panel.HoldStageForTests = true;
            try
            {
                // ── 열림: HIDDEN → COLLAPSED
                _panel.ForceStageForTests(expanded: false);

                Assert.AreEqual(0f, _panel.PanelGrowProgress, 1e-3f,
                    $"{LogPrefix} 상자가 손잡이 크기에서 시작하지 않았습니다(직전 표시의 잔상).");
                Assert.AreEqual(0f, _panel.DialRevealProgress, 1e-3f,
                    $"{LogPrefix} 상자가 자라기도 전에 다이얼이 이미 보입니다 — 신고된 버그 그대로입니다.");

                float firstDialFrame = -1f;
                float fullDialFrame = -1f;
                float boxDoneFrame = -1f;
                float growAtFirstDial = -1f;
                float elapsed = 0f;

                // ★★ 2026-09-01 — 예산을 <b>프레임 수</b>가 아니라 <b>벽시계 시간</b>으로 잡는다.
                //
                // 예전에는 `for (int i = 0; i < 180; i++)`였고, 이 테스트는 <b>10회 중 10회</b> 같은
                // 자리(다이얼이 끝내 나타나지 않았습니다)에서 실패했다 — "간헐적"이 아니라 결정적이었다.
                // 원인은 프로덕션이 아니라 <b>여기</b>다: 등장 연출은 Time.unscaledDeltaTime으로 굴러가는
                // 벽시계 애니메이션인데(상자 0.14s → 내용물 0.10s), 예산만 프레임으로 적혀 있었다.
                // 배치 모드(-nographics)는 렌더링이 없어 극단적으로 빨리 돈다 — 같은 스위트의 세 테스트
                // 길이차에서 역산한 실측이 0.11~0.45ms/프레임(약 2,200~8,900fps)이라, 180프레임은
                // 고작 <b>0.014~0.082초</b>였다. 상자가 게이트에 닿는 데만 0.9 × 0.14 = 0.126초가 드니
                // 예산이 끝날 때 상자는 겨우 0.58까지 자랐고, 그래서 내용물은 <b>시작조차</b> 못 했다.
                // (프로덕션 타이밍은 옳았다: 상자 완성 0.14s < 다이얼 완성 0.226s로 순서가 정확하다.)
                float openBudget = CornerHoverPanel.OpenSequenceSeconds * 4f + 0.1f;
                while (elapsed < openBudget)
                {
                    yield return null;
                    elapsed += Time.unscaledDeltaTime;

                    float grow = _panel.PanelGrowProgress;
                    float dial = _panel.DialRevealProgress;

                    Assert.AreEqual(_panel.ContentRevealProgress, dial, 1e-3f,
                        $"{LogPrefix} 패널이 시킨 값({_panel.ContentRevealProgress:F3})과 다이얼이 들고 있는 " +
                        $"값({dial:F3})이 다릅니다 — SetReveal 배선이 끊겼습니다.");

                    if (dial > 0f)
                        Assert.GreaterOrEqual(grow, CornerHoverPanel.ContentGateBlend - 1e-3f,
                            $"{LogPrefix} t={elapsed:F3}s: 다이얼이 {dial:F2}만큼 보이는데 상자는 아직 " +
                            $"{grow:F2}밖에 안 자랐습니다 — <b>원이 상자보다 먼저</b> 떴습니다(사용자 신고 재발).");

                    if (firstDialFrame < 0f && dial > 0f) { firstDialFrame = elapsed; growAtFirstDial = grow; }
                    if (boxDoneFrame < 0f && grow >= 1f) boxDoneFrame = elapsed;
                    if (fullDialFrame < 0f && dial >= 1f) { fullDialFrame = elapsed; break; }
                }

                // 실패했을 때 "예산이 모자랐나 / 연출이 멈췄나"를 한 줄로 가르도록 실측을 함께 찍는다.
                Assert.Greater(firstDialFrame, 0f,
                    $"{LogPrefix} 다이얼이 끝내 나타나지 않았습니다 — 예산 {openBudget:F3}s 동안 실제로 " +
                    $"{elapsed:F3}s를 돌았고 그때 상자는 {_panel.PanelGrowProgress:F3}까지 자랐습니다" +
                    $"(게이트 {CornerHoverPanel.ContentGateBlend}). 상자가 게이트에 못 닿았다면 예산 부족이고, " +
                    "닿았는데도 다이얼이 0이면 내용물 배선이 끊긴 것입니다.");
                Assert.Greater(boxDoneFrame, 0f,
                    $"{LogPrefix} 상자가 끝내 다 자라지 않았습니다 — {elapsed:F3}s 뒤 진행도 " +
                    $"{_panel.PanelGrowProgress:F3}(상자 성장에 필요한 시간 {CornerHoverPanel.PeekGrowSeconds:F3}s).");

                // ★ 프레임률에 의존하지 않는 두 단언.
                //   (a) 원이 처음 보인 그 프레임에 상자는 이미 게이트를 넘어 있었다(= 원이 상자를 앞지르지 않았다).
                Assert.GreaterOrEqual(growAtFirstDial, CornerHoverPanel.ContentGateBlend - 1e-3f,
                    $"{LogPrefix} 다이얼이 처음 보인 프레임의 상자 진행도가 {growAtFirstDial:F3}입니다 " +
                    $"(게이트 {CornerHoverPanel.ContentGateBlend}) — 원이 상자보다 먼저 떴습니다.");
                //   (b) 원이 <b>다 보이기</b> 전에 상자는 이미 다 자라 있었다.
                Assert.LessOrEqual(boxDoneFrame, fullDialFrame + 1e-3f,
                    $"{LogPrefix} 상자 완성 {boxDoneFrame:F3}s가 다이얼 완전 등장 {fullDialFrame:F3}s보다 " +
                    "늦습니다 — 순서가 뒤집혔습니다.");
                Assert.AreEqual(1f, _panel.DialRevealProgress, 1e-3f);

                Debug.Log($"{LogPrefix} 등장 순서 확인 — 상자 완성 {boxDoneFrame:F3}s / 다이얼 등장 " +
                    $"{firstDialFrame:F3}s(그때 상자 {growAtFirstDial:F3}) / 완전히 보임 {fullDialFrame:F3}s.");

                // ── 닫힘: 등장의 <b>정확한 역순</b>이어야 한다.
                //    원이 남은 채 상자만 줄어들면 그것도 같은 종류의 어긋남이다.
                _panel.ForcePeekForTests();

                float dialGoneAt = -1f;
                float shrinkStartedAt = -1f;
                float closeElapsed = 0f;

                // 닫힘도 같은 이유로 벽시계 예산이다(내용물 0.07s → 상자 0.14s).
                float closeBudget = CornerHoverPanel.CloseSequenceSeconds * 4f + 0.1f;
                while (closeElapsed < closeBudget)
                {
                    yield return null;
                    closeElapsed += Time.unscaledDeltaTime;

                    float grow = _panel.PanelGrowProgress;
                    float dial = _panel.DialRevealProgress;

                    if (dial > 0f)
                        Assert.GreaterOrEqual(grow, CornerHoverPanel.ContentGateBlend - 1e-3f,
                            $"{LogPrefix} 닫히는 중 t={closeElapsed:F3}s: 다이얼이 아직 {dial:F2}만큼 보이는데 " +
                            $"상자는 벌써 {grow:F2}로 줄었습니다 — <b>원만 남고 상자가 사라집니다</b>.");

                    if (dialGoneAt < 0f && dial <= 0f) dialGoneAt = closeElapsed;
                    if (shrinkStartedAt < 0f && grow < 1f - 1e-3f) shrinkStartedAt = closeElapsed;
                    if (grow <= 0f) break;
                }

                Assert.Greater(dialGoneAt, 0f, $"{LogPrefix} 닫히는데 다이얼이 끝내 사라지지 않았습니다.");
                Assert.Greater(shrinkStartedAt, 0f, $"{LogPrefix} 상자가 끝내 줄어들지 않았습니다.");
                Assert.LessOrEqual(dialGoneAt, shrinkStartedAt + 1e-3f,
                    $"{LogPrefix} 다이얼이 사라진 시각({dialGoneAt:F3}s)이 상자가 줄기 시작한 시각" +
                    $"({shrinkStartedAt:F3}s)보다 늦습니다 — 닫힘이 열림의 역순이 아닙니다.");

                Debug.Log($"{LogPrefix} 닫힘 순서 확인 — 다이얼 소멸 {dialGoneAt:F3}s → 상자 축소 시작 " +
                    $"{shrinkStartedAt:F3}s → 손잡이 복귀 {closeElapsed:F3}s.");

                // ── 즉시 거두기(ESC/전체화면과 같은 경로)는 둘을 <b>같은 순간에</b> 0으로 되돌린다.
                _panel.ForceStageForTests(expanded: false);
                // 0.4f 상수였다 — 등장에 필요한 실제 시간(0.226s)의 1.8배뿐이라 여유가 얇았다.
                // 같은 상수에서 유도해, 연출 길이가 바뀌어도 이 대기가 저절로 따라가게 한다.
                yield return new WaitForSecondsRealtime(CornerHoverPanel.OpenSequenceSeconds * 4f + 0.1f);
                Assert.AreEqual(1f, _panel.DialRevealProgress, 1e-3f, $"{LogPrefix} 다시 열리지 않았습니다.");

                _panel.ForceHide("테스트: 즉시 거두기");
                yield return null;

                Assert.AreEqual(0f, _panel.PanelGrowProgress, 1e-3f,
                    $"{LogPrefix} 즉시 거두기 뒤에도 상자 진행도가 남아 있습니다 — 다음에 뜰 때 상자가 " +
                    "이미 커진 채로 시작해 원이 먼저 보입니다.");
                Assert.AreEqual(0f, _panel.DialRevealProgress, 1e-3f,
                    $"{LogPrefix} 즉시 거두기 뒤에도 다이얼이 남아 있습니다.");
            }
            finally
            {
                _panel.HoldStageForTests = false;
            }
        }

        /// <summary>
        /// ★ 2026-08-31 사용자 지시 <i>"캐릭터 사이즈는 max를 1.5까지만"</i>의 회귀 잠금.
        /// 다이얼은 어떤 입력으로도 1.5×를 넘는 값을 만들 수 없어야 한다 — 눈금 인덱스,
        /// 각도 매핑(빈 구역은 끝값에 붙는다), 저장 복원 clamp의 <b>세 경로 전부</b>를 본다.
        /// </summary>
        [Test]
        public void 다이얼은_15배를_넘는_값을_만들_수_없다()
        {
            Assert.AreEqual(1.5f, StickConfig.MaxCharacterScale, 1e-4f,
                "캐릭터 배율 상한이 1.5가 아닙니다 — 사용자가 지정한 값입니다(2026-08-31).");

            // (a) 눈금 축 — 마지막 눈금이 곧 상한이고, 그 너머 인덱스는 clamp된다.
            Assert.AreEqual(1.5f, SizeDialWidget.IndexToValue(SizeDialWidget.TickCount - 1), 1e-4f);
            for (int i = -5; i < SizeDialWidget.TickCount + 20; i++)
                Assert.LessOrEqual(SizeDialWidget.IndexToValue(i), 1.5f + 1e-4f,
                    $"인덱스 {i}가 1.5×를 넘는 값 {SizeDialWidget.IndexToValue(i):F3}을 만들었습니다.");

            // (b) 값 → 인덱스 — 상한을 넘겨 들어와도 마지막 눈금에 붙는다.
            foreach (float over in new[] { 1.5001f, 1.6f, 2f, 10f, 1e6f })
            {
                int idx = SizeDialWidget.ValueToIndex(over);
                Assert.AreEqual(SizeDialWidget.TickCount - 1, idx,
                    $"{over:F2}×가 마지막 눈금에 붙지 않았습니다(인덱스 {idx}).");
                Assert.LessOrEqual(SizeDialWidget.IndexToValue(idx), 1.5f + 1e-4f);
            }

            // (c) 설정 조회 경로 — 예전 에셋/예전 저장 파일이 2.0을 들고 와도 1.5로 나온다.
            StickConfig probe = ScriptableObject.CreateInstance<StickConfig>();
            try
            {
                probe.SetRuntimeCharacterScale(2f);
                Assert.AreEqual(1.5f, probe.ResolveCharacterScale(), 1e-4f,
                    "저장된 2.0×가 상한으로 내려오지 않았습니다 — 표시(1.50×)와 실제가 갈라집니다(원칙 1).");
                probe.SetRuntimeCharacterScale(0.1f);
                Assert.AreEqual(StickConfig.MinCharacterScale, probe.ResolveCharacterScale(), 1e-4f,
                    "하한은 건드리지 않기로 했는데 함께 바뀌었습니다.");
            }
            finally { Object.DestroyImmediate(probe); }

            Debug.Log($"{LogPrefix} 상한 확인 — {SizeDialWidget.TickCount}칸 / " +
                $"{SizeDialWidget.IndexToValue(0):F2}× ~ {SizeDialWidget.IndexToValue(SizeDialWidget.TickCount - 1):F2}× / " +
                $"스윕 ±{SizeDialWidget.DegreesForIndex(SizeDialWidget.TickCount - 1):F0}°.");
        }
    }
}
