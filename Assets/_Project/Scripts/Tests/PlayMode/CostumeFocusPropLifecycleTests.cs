using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★ 프롭 합격선 <b>P-1 · P-2 · P-5</b> + 리더가 재정의한 <b>L-5 「프롭」 반쪽</b>
    /// (<c>docs/DESIGN_COSTUME_FOCUS_ARCHITECTURE.md</c> 6-5절).
    ///
    /// ============================================================================
    /// 무엇을 재는가 — <b>양성과 음성을 같은 실행에서</b>
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>양성</b> — 빌드 프레임에 <see cref="CostumePropRenderer.PointWriteCount"/>가
    ///     <b>실제로 오른다</b>. 오르는 양은 매니페스트 에셋에서 <b>독립으로 센 값</b>과 같아야 한다
    ///     (<see cref="CostumeFocusRig.ExpectedBuildPointWrites"/>).</item>
    ///   <item><b>음성</b> — 유지 구간(몰입기 내내) 프레임에는 <b>한 칸도</b> 안 오른다.</item>
    /// </list>
    /// <b>둘 다 있어야 「0이 절감이다」가 성립한다.</b> 음성만 재면 «계기가 안 붙어서 0»과 구별되지
    /// 않고, 그것이 이 저장소가 아홉 번 당한 형태다.
    ///
    /// <para>★ <b>다른 자로 한 번 더 잰다</b>: 같은 빌드 프레임의
    /// <see cref="CostumePropRenderer.ActiveVisualCount"/>(실제로 생긴 <c>LineRenderer</c> 수)가
    /// 점 쓰기 증가분과 <b>같아야 한다</b>. 두 계기는 서로 다른 곳에서 나오므로, 둘이 같다는 것은
    /// 어느 한쪽이 굳어 있지 않다는 뜻이다.</para>
    ///
    /// <para>★ 예산은 전부 <b>벽시계(초)</b>다(CLAUDE.md). 세션 시간은
    /// <see cref="Time.timeScale"/>로 압축하지만 <c>TestClock</c>은 <c>unscaledDeltaTime</c>을 쓰므로
    /// 예산 자체는 압축되지 않는다.</para>
    /// </summary>
    public sealed class CostumeFocusPropLifecycleTests
    {
        private const string LogPrefix = "[코스튬프롭-TEST]";

        /// <summary>세션 시간 압축 배율. 60초 세션이 벽시계 15초 안팎이 되고 몰입기는 3초쯤이다.
        /// <b>프레임 페이싱은 여기서 재지 않으므로</b>(그쪽은 <c>CostumeFocusStillTierTests</c>)
        /// 벽시계 기준의 절감 문턱과 충돌하지 않는다.</summary>
        private const float TimeCompression = 4f;

        private float _savedTimeScale = 1f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            CostumeFocusRig.RestoreGlobals(_savedTimeScale);
            yield return null;
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 프롭은_빌드에서만_점을_쓰고_유지구간에는_한_칸도_안_오른다()
        {
            yield return CostumeFocusRig.LoadSceneAndSettle();

            StickmanAgent agent = CostumeFocusRig.Agent();
            FocusWatchDirector director = CostumeFocusRig.Director();
            CostumePropRenderer prop = CostumeFocusRig.EnsureCostumeProp(agent);

            CostumeDescriptor costume = CostumeFocusRig.WearOfficeCostume(agent.Config);
            int stage = CostumeFocusRig.CurrentStageOf(costume);
            int expectedWrites = CostumeFocusRig.ExpectedBuildPointWrites(costume, stage);

            // ★ 기대값이 0이면 아래 「양성」은 「음성」과 구별되지 않는다 — 그 상태를 먼저 거부한다.
            Assert.Greater(expectedWrites, 0,
                $"{LogPrefix} 단계 {stage}에서 그릴 조각이 0개입니다(에셋 기준). " +
                "그러면 «빌드에서 점이 오른다»를 잴 수 없으므로 이 검사는 아무것도 증명하지 못합니다.");

            _savedTimeScale = Time.timeScale;
            Time.timeScale = TimeCompression;

            int writesBefore = prop.PointWriteCount;
            int buildsBefore = prop.BuildCount;

            yield return CostumeFocusRig.StartAndReachImmersion(director, prop, TimeCompression);

            int writesAfterBuild = prop.PointWriteCount;
            int visuals = prop.ActiveVisualCount;
            int colliders = prop.ActiveColliderCount;
            int rung = prop.PlacementRung;

            Debug.Log($"{LogPrefix} 빌드 프레임 — 단계 {stage}, 조각 " +
                $"{CostumeFocusRig.StageShapesOf(costume, stage).Count}개, 기대 점 쓰기 {expectedWrites}회, " +
                $"실측 {writesAfterBuild - writesBefore}회, 생긴 LineRenderer {visuals}개, " +
                $"콜라이더 {colliders}개, 폴백 {rung}단, 코스튬 '{prop.PlacedCostumeKey}'.");

            // ---- ① 양성: 빌드 프레임에 계기가 실제로 오른다 ----
            Assert.AreEqual(expectedWrites, writesAfterBuild - writesBefore,
                $"{LogPrefix} 빌드 프레임의 점 쓰기가 기대({expectedWrites})와 다릅니다. " +
                "기대값은 프로덕션 함수가 아니라 <b>매니페스트 에셋</b>에서 독립으로 셌으므로, 어긋났다면 " +
                "(가) 단계 조형 선택이 갈라졌거나 (나) 채움/획 규칙이 바뀌었거나 (다) 조각 하나가 " +
                "좌표 스트림 오류로 통째로 건너뛰어졌습니다([코스튬프롭] LogError를 보십시오).");

            // ---- ② 다른 자로 대조: 생긴 LineRenderer 수 == 점 쓰기 수 ----
            Assert.AreEqual(writesAfterBuild - writesBefore, visuals,
                $"{LogPrefix} 점 쓰기 {writesAfterBuild - writesBefore}회인데 LineRenderer는 {visuals}개입니다 — " +
                "두 계기가 갈라졌습니다(한쪽이 굳어 있거나 선이 컨테이너 밖에 생겼습니다).");

            // ---- ③ P-5: 콜라이더 0개 (비침해 원칙 2 — 클릭 관통이 프롭에 막히면 안 된다) ----
            Assert.AreEqual(0, colliders,
                $"{LogPrefix} 프롭에 콜라이더가 {colliders}개 붙었습니다 — 합격선 P-5 위반이고 " +
                "절대 불변 원칙 2(클릭 관통)를 직접 깹니다.");

            // ---- ④ P-2: 빌드는 세션당 정확히 1회 ----
            Assert.AreEqual(1, prop.BuildCount - buildsBefore,
                $"{LogPrefix} 몰입기 진입에 프롭을 {prop.BuildCount - buildsBefore}번 지었습니다 — 정확히 1번이어야 합니다.");

            Assert.GreaterOrEqual(rung, 1,
                $"{LogPrefix} 폴백 단이 {rung}입니다 — 0은 «미배치»인데 PropPlaced가 참입니다(모순).");

            // ---- ⑤ 음성: 유지 구간에는 한 칸도 안 오른다 ----
            float immersionWall = CostumeFocusRig.ImmersionWallSeconds(
                director.SessionDurationSeconds, TimeCompression);
            float holdBudget = immersionWall * 0.5f;

            int frames = 0;
            int worstWrites = writesAfterBuild;
            int worstVisuals = visuals;
            float lastElapsed = 0f;
            yield return TestClock.SampleForSeconds(holdBudget, t =>
            {
                lastElapsed = t;
                if (director.CurrentPhase != FocusSessionPhase.Immersion) return false;
                frames++;
                if (prop.PointWriteCount > worstWrites) worstWrites = prop.PointWriteCount;
                if (prop.ActiveVisualCount != worstVisuals) worstVisuals = prop.ActiveVisualCount;
                return true;
            });

            Debug.Log($"{LogPrefix} 유지 구간 표본 — 벽시계 {lastElapsed:F2}초 / {frames}프레임 " +
                $"(몰입기 벽시계 길이 {immersionWall:F2}초). 점 쓰기 {writesAfterBuild} -> {worstWrites}, " +
                $"LineRenderer {visuals} -> {worstVisuals}.");

            // 대조군 — 표본이 없으면 아래 「0」은 공허하다.
            Assert.Greater(frames, 30,
                $"{LogPrefix} 유지 구간 표본이 {frames}프레임뿐입니다 — 몰입기를 지나쳤거나 예산이 " +
                "어긋났습니다. 그러면 아래 «0회 증가»는 아무것도 증명하지 못합니다.");
            Assert.Greater(lastElapsed, 0.2f,
                $"{LogPrefix} 유지 구간을 벽시계 {lastElapsed:F3}초만 봤습니다 — 프레임 수는 많아도 " +
                "실제 시간이 없으면 «매 프레임 재드로우 금지»를 본 것이 아닙니다.");

            Assert.AreEqual(writesAfterBuild, worstWrites,
                $"{LogPrefix} 유지 구간에 점 쓰기가 {worstWrites - writesAfterBuild}회 더 일어났습니다 — " +
                "합격선 P-1 위반입니다(사용자 요구 「1회만 그리고 고정, 매 프레임 재드로우 금지」의 실체).");
            Assert.AreEqual(visuals, worstVisuals,
                $"{LogPrefix} 유지 구간에 LineRenderer 수가 {visuals} -> {worstVisuals}로 변했습니다 — " +
                "합격선 P-2(진입 1회 이후 생성 0) 위반입니다.");
            Assert.AreEqual(0, prop.ActiveColliderCount,
                $"{LogPrefix} 유지 구간 도중 콜라이더가 생겼습니다 — P-5는 «항상»입니다.");

            Debug.Log($"{LogPrefix} ① 통과 — 양성(빌드 +{expectedWrites}) / 음성(유지 {frames}프레임 +0). " +
                "0이 «절감»인지 «계기 죽음»인지가 이 한 실행 안에서 갈렸습니다.");
        }

        /// <summary>
        /// ★ 몰입기 <b>도중 취소</b>에서 프롭이 화면에 남지 않는다(설계 6-3 ④ · 규약 6번).
        /// <para>이 경로가 위험한 이유는 구간 전이 이벤트가 <b>세션 종료에는 오지 않기</b> 때문이다 —
        /// 이벤트만 구독했다면 여기서 그림이 남는다. 남으면 «우리 앱이 그 창에 뭘 걸어뒀다»가 된다.</para>
        ///
        /// <para>★★ <b>이 픽스처는 「즉시」를 두 단계로 나눠 잰다 — 합치지 마라</b>(2026-09-29 수리).
        /// 원래는 <c>StopFocusSession()</c> 다음 줄에서 <b>프레임을 하나도 흘리지 않고</b>
        /// <see cref="CostumePropRenderer.PropPlaced"/>를 물었고, 그래서 <b>결정론적으로</b> 빨간불이었다.
        /// 프롭 렌더러는 그 사실을 <c>LateUpdate</c>에서 <b>조회</b>하는데(구독하지 않는 근거가 그쪽 클래스
        /// 문서에 있다) PlayMode 코루틴 본문은 <c>Update</c> 뒤 · <c>LateUpdate</c> <b>앞</b>에서 재개되므로,
        /// 그 자리에서는 조회가 아직 한 번도 돌지 않았다 — 「아직 안 물어봤다」를 「안 꺼졌다」로 읽은 것이다.
        /// 프로덕션은 세 종료 경로를 파생값 한 줄로 전부 덮고 있었다(<c>IsSessionActive = false</c> 세 자리 ·
        /// <c>CurrentPhase</c> 파생 · <c>SyncToPhase</c>의 <c>from == Immersion</c> 한 줄).
        /// 같은 사실을 재는 형제 픽스처(<c>CostumeFocusLfvsPoseTests</c>)는 처음부터 세션을 끊은 뒤
        /// 벽시계 0.4초를 기다린 다음 물었다 — 이 파일만 그 경계를 빠뜨렸다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 몰입기_도중_취소해도_프롭이_화면에_남지_않는다()
        {
            yield return CostumeFocusRig.LoadSceneAndSettle();

            StickmanAgent agent = CostumeFocusRig.Agent();
            FocusWatchDirector director = CostumeFocusRig.Director();
            CostumePropRenderer prop = CostumeFocusRig.EnsureCostumeProp(agent);
            CostumeFocusRig.WearOfficeCostume(agent.Config);

            _savedTimeScale = Time.timeScale;
            Time.timeScale = TimeCompression;

            int teardownsBefore = prop.TeardownCount;
            yield return CostumeFocusRig.StartAndReachImmersion(director, prop, TimeCompression);

            Assert.Greater(prop.ActiveVisualCount, 0,
                $"{LogPrefix} 취소 전에 그려진 선이 0개입니다 — 아래 «사라졌다»가 공허해집니다.");

            // ★ 양성 대조 — 맨 아래 «취소 뒤 IsCostumeImmersionActive가 거짓»이 «계기가 처음부터
            //   거짓»과 구별되게 한다. 블랙보드는 프롭 렌더러를 <b>1회 탐색 + 캐싱</b>하므로(못 찾으면
            //   다시 찾지 않는다) 탐색이 어긋난 회차에서는 그 값이 영원히 거짓이고, 부재 단언은
            //   그대로 조용히 초록이 된다.
            Assert.IsTrue(agent.Blackboard.IsCostumeImmersionActive,
                $"{LogPrefix} 프롭은 섰는데 취소 전 IsCostumeImmersionActive가 거짓입니다 — 블랙보드가 " +
                "프롭 렌더러를 못 찾은 채 캐싱했을 수 있습니다(그러면 아래 부재 단언은 아무것도 " +
                "증명하지 않습니다).");

            float outro = agent.Config != null ? agent.Config.costumeFocusPropOutroSeconds : 0.22f;

            director.StopFocusSession();

            // ---- ① 즉시(동기): 세션 종료가 <b>구간</b>을 닫는다 = 프롭 해제의 단일 판정 입력 ----
            // CurrentPhase는 IsSessionActive의 파생값이라 완주·중도취소·긴급정지 세 경로가 전부
            // 「같은 한 줄」로 닫힌다(FocusWatchDirector.CurrentPhase 문서). 여기가 빨간불이면
            // «종료 경로는 셋인데 처리는 하나»가 되돌아온 것이고, 그때는 프롭이 아무리 조회해도
            // 영원히 안 꺼진다 — 그래서 이 단언이 아래 둘보다 먼저 있다.
            Assert.AreEqual(FocusSessionPhase.None, director.CurrentPhase,
                $"{LogPrefix} 취소 호출이 끝났는데 구간이 {director.CurrentPhase}입니다 — " +
                "프롭 해제의 단일 판정 입력이 닫히지 않았습니다.");

            // ---- ② 다음 프레임: 조회가 한 바퀴 돌면 포즈 층이 내려간다 ----
            // ★ 여기서 한 프레임을 흘리는 것은 <b>느슨함이 아니라 계약</b>이다. 프롭 렌더러는 위 파생값을
            //   구독하지 않고 LateUpdate에서 <b>조회</b>한다 — 구간 전이 이벤트가 세션 종료에는 오지
            //   않아서 구독하면 바로 이 경로에서 그림이 남기 때문이다(CostumePropRenderer 클래스 문서).
            //   PlayMode 코루틴 본문은 Update 뒤 · LateUpdate <b>앞</b>에서 재개되므로, 취소한 그
            //   프레임에는 조회가 아직 한 번도 돌지 않았다. 그 자리에서 PropPlaced를 물으면
            //   «조회 전»을 «안 꺼짐»으로 읽는다(리그의 빌드 쪽도 같은 이유로 PropPlaced를 기다린다 —
            //   CostumeFocusRig.StartAndReachImmersion).
            // ★ 그 한 프레임이 포즈 층에는 <b>보이지 않는다</b>: 층은 StickmanAgent.Update가 굴리므로
            //   (TickPose → TickCostumeKeypose) 취소 프레임의 Update는 이미 지나갔고, 다음 Update는
            //   아래 LateUpdate가 내려놓은 값을 본다. 유령 제스처 프레임은 0이다.
            float cancelFrameDelta = Time.deltaTime;   // 이 프레임의 LateUpdate가 퇴장 타이머에 더할 값
            // 프레임예산-OK: 시간 예산이 아니라 «LateUpdate 조회가 한 바퀴 돌았는가»라는 구조적 대기다.
            yield return null;

            Assert.IsFalse(prop.PropPlaced,
                $"{LogPrefix} 취소 다음 프레임에도 PropPlaced가 참입니다 — 코스튬 포즈 층이 " +
                "사라지는 물건을 계속 짚습니다(절대 불변 원칙 1). 이 자리가 빨간불이면 해제가 " +
                "BeginOutro에서 빠져 Teardown까지 밀린 것입니다(퇴장 연출 내내 유령 제스처).");

            // ★ 그리고 층은 <b>그림보다 먼저</b> 내려가야 한다 — 순서가 뒤집히면 위 단언은 초록인데
            //   유령 제스처는 그대로 남는다. 그림이 이 시점에 벌써 없다면 정상 경로일 수 없다.
            //   유일한 예외는 그 한 프레임의 scaled dt가 퇴장 길이를 통째로 넘긴 히치이고, 그 예외를
            //   짐작으로 넘기지 않고 <b>같은 자리에서 수치로</b> 가른다(배치모드 실측 dt는 밀리초다).
            bool drawingStillOnScreenAtLayerOff = prop.ActiveVisualCount > 0;
            if (!drawingStillOnScreenAtLayerOff)
            {
                Assert.GreaterOrEqual(cancelFrameDelta, outro,
                    $"{LogPrefix} 층이 내려간 그 프레임에 그림도 이미 사라졌습니다" +
                    $"(그 프레임 dt={cancelFrameDelta:F4}초 < 퇴장 {outro:F2}초) — 퇴장 연출을 건너뛰고 " +
                    "즉시 파괴한 것이므로 등장/퇴장 포락선 계약이 깨졌습니다.");
            }

            // 퇴장 타이머는 scaled deltaTime이라 압축된다. 넉넉히 6배 + 하한 1초를 준다.
            float budget = Mathf.Max(1f, outro / TimeCompression * 6f);
            yield return TestClock.WaitUntil(() => prop.ActiveVisualCount == 0, budget,
                "취소 뒤 프롭 철거(ActiveVisualCount == 0)");

            Assert.AreEqual(1, prop.TeardownCount - teardownsBefore,
                $"{LogPrefix} 철거가 {prop.TeardownCount - teardownsBefore}회입니다 — 세션당 정확히 1회여야 합니다.");
            // 위 ①이 「닫혔다」를 쟀으니 여기서는 「닫힌 채로 있다」를 잰다 — 퇴장 연출이 도는 동안
            // 구간이 되살아나면 프롭이 다시 지어지고 그 순간 철거 계수도 어긋난다.
            Assert.AreEqual(FocusSessionPhase.None, director.CurrentPhase,
                $"{LogPrefix} 철거가 끝난 뒤 구간이 {director.CurrentPhase}로 되살아났습니다.");
            Assert.IsFalse(agent.Blackboard.IsCostumeImmersionActive,
                $"{LogPrefix} 취소 뒤에도 IsCostumeImmersionActive가 참입니다 — 배회 사다리와 포즈 층이 " +
                "«없는 프롭» 곁에 묶입니다.");

            Debug.Log($"{LogPrefix} ② 통과 — 구간은 취소 호출 안에서 즉시 닫히고, 포즈 층은 다음 " +
                $"LateUpdate 조회에서 내려갔다(그 프레임 dt {cancelFrameDelta:F4}초 / 퇴장 {outro:F2}초, " +
                $"층이 내려간 시점에 그림 잔존 {drawingStillOnScreenAtLayerOff}). 철거 1회 · 잔재 0개.");
        }
    }
}
