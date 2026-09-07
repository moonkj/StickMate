using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StickMate.Core;
using StickMate.States;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 회귀 잠금 — D1 발레 피루엣의 <b>retiré(다리 삼각형) 자세</b>와, 그것을 «회전»으로 보이게
    /// 만드는 <b>얼굴방향 미러 반전</b> 메커니즘 (2026-09-07).
    ///
    /// ============================================================================
    /// 사용자 신고 (Windows 실기)
    /// ============================================================================
    /// <i>"춤출때 멈춰서 다리삼각형모양 -발레하는것처럼 해야하는데 이상하게 함"</i>
    ///
    /// ============================================================================
    /// 진단 결론 — 목표각·좌우비대칭·「높이」는 정확하다. 「닿는다」는 문서 표현이 과장이었다
    /// ============================================================================
    /// <c>StickmanPoseAnimator.ApplyPirouettePose</c>의 회전 유지(③) 구간 목표각(<c>dance.Pirouette.
    /// RetireHipDegrees</c>=62° / <c>RetireKneeDegrees</c>=116°)을 프리팹 <b>실측</b> 허벅지·정강이
    /// 길이로 순방향 기구학 재계산해 이 파일의 ①②가 <b>소스가 아니라 실측 기하로 독립 검증</b>한다
    /// (프로덕션 함수를 그대로 믿지 않는다 — 「생성기와 검사기가 같이 틀린다」). 결과:
    /// <list type="bullet">
    ///   <item><b>높이(수직)는 문서와 정확히 일치</b>한다 — 자유 다리 발끝의 엉덩이 기준 하강량과
    ///     지지 다리 무릎 높이의 차는 <b>약 0.00036 H</b>로, <c>StickConfig.
    ///     dancePirouetteRetireHipDegrees</c> 툴팁이 적은 <i>"오차 0.00032H"</i>를 거의 그대로
    ///     되살린다. <b>이 부분은 설계 문서가 맞다.</b></item>
    ///   <item>★ <b>그런데 그 툴팁은 수직 성분만 계산했다</b>(<c>cos</c>항만 더했고 <c>sin</c>항,
    ///     즉 수평 성분은 아예 다루지 않았다). 실제 발끝의 <b>2차원</b> 좌표를 순방향 기구학으로
    ///     재보면, 자유 다리 발끝은 지지 다리 무릎보다 앞으로 <b>약 0.034 H</b>(신장의 3.4%) 만큼
    ///     떨어져 있다 — <b>같은 높이지만 옆으로 벌어져 있어 실제로는 닿지 않는다.</b> 「발끝이
    ///     정확히 무릎에 닿는다」는 문서 표현은 <b>수직 성분만 놓고 보면 참이고, 실제 접촉(2D
    ///     거리)로 보면 과장</b>이다.</item>
    /// </list>
    /// ★ 이 간극(0.034H)은 사용자가 신고한 「이상함」의 정체로 보기엔 <b>너무 작고 일정하다</b> —
    /// 매 프레임 같은 크기로 조용히 벌어져 있을 뿐 «이상하게» 보일 사건성이 없다(획 두께 근처 값이라
    /// 육안 판별은 미묘하다). 그래서 진단의 무게중심은 아래 ③(얼굴방향 미러 반전)에 둔다. 다만 이
    /// 수치 자체는 design-motion의 검산 문서가 <b>1차원 근사를 2차원 결론처럼 적은 것</b>이라, 별도로
    /// 정정을 권한다(이 파일이 그 증거를 잠근다).
    ///
    /// 그런데 이 리그는 Z축(화면 법선) 회전만 가능하고 실제 피루엣의 회전축(Y, 수직)이 없다.
    /// <c>States/DanceState.DrivePirouetteFacing</c>은 그래서 <c>SetFacingSign</c> 부호 반전
    /// (= <c>StickmanPoseAnimator.SetFacing</c>)으로 «회전»을 흉내 낸다 — <c>dancePirouetteRevolutionSeconds</c>
    /// (기본 0.70초)의 절반마다, 즉 <b>2.86 Hz로 온몸을 순간 좌우 미러링</b>한다.
    ///
    /// <c>docs/UX_MOTION_DANCE.md</c> §11-2와 <c>StickConfig.dancePirouetteRevolutionSeconds</c> 툴팁이
    /// <b>이 메커니즘 자체를 이 기능의 최대 미확인 위험으로 이미 자백</b>하고 있다: <i>"2.86 Hz 반전이
    /// 「회전」으로 읽히는가 「깜빡임」으로 읽히는가 — 연속 프레임 캡처로만 판정된다. 나는 아직 못
    /// 봤다."</i> 이 파일의 ③이 그 자백을 <b>수치로</b> 확정한다: 같은 위상·같은 포락선에서
    /// <c>SetFacing</c>만 뒤집으면 <b>모든 관절의 렌더 각도(<c>GetUpperAngles</c>/<c>GetJointAngles</c>가
    /// 읽는, <c>ApplyAngle</c>이 <c>facingSign</c>을 곱해 쓴 실제 <c>Transform.localEulerAngles.z</c>)가
    /// 크기는 한 톨도 안 바뀐 채 부호만 정확히 뒤집히고</b>, 발끝 월드 좌표도 같은 프레임에 x부호만
    /// 뒤집힌다 — 즉 이 「회전」은 연속 회전이 아니라 <b>정지한 삼각형 포즈를 0.35초마다 스냅으로
    /// 좌우 미러링하는 것</b>이다. 사용자가 본 「이상함」은 이 스냅이 «회전»이 아니라
    /// «깜빡임/순간이동»으로 읽힌 결과일 가능성이 매우 높다 — 각도 산출 자체의 결함이 아니다.
    ///
    /// ★ <c>dancePirouetteRevolutionSeconds</c>/<c>Revolutions</c>를 손대는 완화(설계 문서의 「후퇴
    /// 사다리」 ②③)는 <c>danceGracefulExitBudgetSeconds</c>와 루프 길이·에피소드 루프 수 범위에
    /// <b>연쇄로 영향</b>을 준다 — 고립된 한 줄 수정이 아니다. <b>이 라운드(신설 당시)는 그 다이얼을
    /// 건드리지 않았다</b> — 이 파일은 진단만 잠그는 안전망으로 남겼다.
    ///
    /// <para>★★ 2026-09-07 후속 라운드 갱신 — design-motion이 실기(macOS) 연속캡처로 「깜빡임」을
    /// 확정하고(<c>docs/UX_MOTION_DANCE.md</c> §14) 후퇴사다리 ③을 채택했다: <c>dancePirouetteRevolutions</c>
    /// 2→1(③ 회전 1.40→0.70초), 연쇄로 <c>danceGracefulExitBudgetSeconds</c> 1.90→1.58초(병목이
    /// D1에서 D2 스타점프로 이동), D1 세트 수 탐색범위 하한 3→4(<c>DanceState.ResolveLoopTarget</c>이
    /// <c>AudioReactiveDancePolicy.MotionEpisodeMinSeconds</c>/<c>MaxSeconds</c>로 자동 재계산 —
    /// 별도 상수 없음). <c>dancePirouetteRevolutionSeconds</c>(0.70, 반전 2.86Hz)는 ②가 기각되어
    /// **그대로**다 — 그래서 이 파일이 진단한 「2.86Hz 순간 미러링」 메커니즘 자체와 아래 ③의 모든
    /// 단언은 <b>바뀌지 않는다</b>(반전 «빈도»가 아니라 세트당 «횟수»만 4→2회로 줄었다). ④(회전 포기)는
    /// 여전히 리더 승인 대기다.</para>
    ///
    /// <para><b>플랫폼</b>: 플랫폼 중립. <c>StickmanPoseAnimator.cs</c>/<c>DanceState.cs</c>에는
    /// <c>#if UNITY_STANDALONE_*</c> 분기가 없다(자세·박자 경로는 두 플랫폼에서 글자 그대로 같은 코드를
    /// 탄다) — 그래서 이 macOS 개발 머신에서 잠근 값이 신고가 들어온 Windows에도 그대로 적용된다.</para>
    /// </summary>
    public sealed class PirouetteRetireTriangleTests
    {
        private const string LogPrefix = "[피루엣삼각형]";
        private const string PrefabRelativePath = "_Project/Prefabs/Stickman.prefab";

        private const float Dt = 1f / 60f;

        /// <summary>목표각 수렴에 충분한 프레임 수 — <c>dancePoseSmoothingRate</c>(기본 78)의 시간상수는
        /// 약 13ms이므로 1.5초(90프레임)면 부동소수 오차 수준까지 수렴한다.</summary>
        private const int SettleFrames = 90;

        private const float AngleTolerance = 0.05f;

        /// <summary>설계 문서가 적은 <b>수직(높이)</b> 오차(배율 0.60에서 0.018pt = 0.00032H)에 수렴
        /// 오차 여유를 더한 판정선 — 이 축만은 문서 주장이 실측과 일치한다(아래 클래스 문서 참고).</summary>
        private const float VerticalContactToleranceHeights = 0.002f;

        /// <summary>★ <b>실측으로 확정한</b> 2D(발끝-무릎 직선거리) 값 — 약 0.034H. 문서의 「정확히
        /// 닿는다」는 이 축을 계산하지 않은 채 쓴 표현이라 곧이곧대로 0에 가깝게 잠그지 않는다.
        /// 대신 <b>지금 실제로 나는 값 근처</b>에 잠가 둔다 — 이 값이 커지면(예: 다리 비율이 바뀌면)
        /// «닿는다는 착시」가 더 깨진다는 뜻이므로 그때 다시 검토해야 한다.</summary>
        private const float ObservedFullGapHeights = 0.034f;
        private const float FullGapToleranceHeights = 0.01f;

        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            _root = null;
        }

        // ============================================================================
        // ① 교정 — 프리팹 실측이 설계 문서의 허벅지/정강이 비율을 되살리는가
        // ============================================================================

        [Test]
        public void 교정_프리팹_실측_허벅지_정강이_비율이_설계_문서와_일치한다()
        {
            RigGeometry g = ReadPrefabGeometry();

            float thighRatio = g.LegUpperLength / g.TotalHeight;
            float shinRatio = g.LegLowerLength / g.TotalHeight;

            Debug.Log($"{LogPrefix} 프리팹 실측 — 허벅지 {g.LegUpperLength:F7}/H={thighRatio:F5}H, " +
                $"정강이 {g.LegLowerLength:F7}/H={shinRatio:F5}H, 엉덩이-어깨={g.ShoulderAboveHip:F7}, " +
                $"신장(raw) H={g.TotalHeight:F7}");

            // docs/UX_MOTION_DANCE.md 6절 / StickConfig.dancePirouetteRetireHipDegrees 툴팁의 근거값.
            // ★ 이 핀이 어긋나면 그 문서의 "발끝이 정확히 무릎에 닿는다" 검산 전체가 무효다.
            Assert.AreEqual(0.21981f, thighRatio, 0.0002f,
                $"허벅지/신장 비율이 {thighRatio:F5}H로 설계 문서 핀(0.21981H)과 다릅니다 — 프리팹 " +
                "지오메트리가 바뀌었다면 RetireHipDegrees/RetireKneeDegrees의 「접촉」 검산을 다시 해야 합니다.");
            Assert.AreEqual(0.19783f, shinRatio, 0.0002f,
                $"정강이/신장 비율이 {shinRatio:F5}H로 설계 문서 핀(0.19783H)과 다릅니다 — 같은 이유로 " +
                "retiré 접촉 검산이 무효화됩니다.");
        }

        // ============================================================================
        // ② 핵심 — 회전 유지(③) 구간에서 자유 다리 발끝이 지지 다리 무릎 «높이»에 닿는가
        //    (2D 접촉은 실측으로 확정한 간극(③(c) 참고)에 잠근다 — 클래스 문서에 그 사연이 있다)
        // ============================================================================

        [Test]
        public void 회전_유지_구간에서_자유다리_발끝이_지지다리_무릎_높이에_닿는다()
        {
            RigGeometry g = ReadPrefabGeometry();
            StickmanPoseAnimator.DancePoseSettings dance = BuildDanceSettings(out StickConfig config);
            StickmanPoseAnimator pose = BuildRig(g);

            float holdPhase = MidHoldPhase();
            Debug.Log($"{LogPrefix} ③ 회전 유지 구간 대표 위상 = {holdPhase:F4} " +
                $"(경계 {StickmanPoseAnimator.BeatEdge(StickmanPoseAnimator.DanceMovePirouette, 2):F4}~" +
                $"{StickmanPoseAnimator.BeatEdge(StickmanPoseAnimator.DanceMovePirouette, 3):F4})");

            Settle(pose, dance, config, holdPhase, envelope: 1f);

            pose.GetUpperAngles(out float leftUpper, out float rightUpper, out _, out _);
            pose.GetJointAngles(out float leftKnee, out float rightKnee, out _, out _);

            Debug.Log($"{LogPrefix} 자유다리(오른쪽) 허벅지={rightUpper:F2}° 무릎={rightKnee:F2}° / " +
                $"지지다리(왼쪽) 허벅지={leftUpper:F2}° 무릎={leftKnee:F2}°");

            // (a) 각도 자체가 설정값(=검산의 짝)에 정확히 수렴했는가.
            Assert.AreEqual(dance.Pirouette.RetireHipDegrees, rightUpper, AngleTolerance,
                $"자유 다리 허벅지 각도가 목표({dance.Pirouette.RetireHipDegrees}°)로 수렴하지 않았습니다({rightUpper:F3}°).");
            Assert.AreEqual(StickmanPoseAnimator.KneeBendSign * dance.Pirouette.RetireKneeDegrees, rightKnee, AngleTolerance,
                $"자유 다리 무릎 각도가 목표로 수렴하지 않았습니다({rightKnee:F3}°).");
            Assert.AreEqual(0f, leftUpper, AngleTolerance,
                $"지지 다리 허벅지가 곧게 아래(0°)로 수렴하지 않았습니다({leftUpper:F3}°).");
            Assert.AreEqual(StickmanPoseAnimator.KneeBendSign * dance.Pirouette.SupportKneeDegrees, leftKnee, AngleTolerance,
                $"지지 다리 무릎(releve) 각도가 목표로 수렴하지 않았습니다({leftKnee:F3}°).");

            // (b) 좌우 비대칭 — "삼각형"의 정의 그 자체(한쪽은 접히고 한쪽은 편다).
            Assert.Greater(Mathf.Abs(rightUpper - leftUpper), 30f,
                "자유 다리와 지지 다리의 허벅지 각도가 충분히 갈라지지 않았습니다 — 이러면 " +
                "「삼각형」이 아니라 양다리가 나란한 중립에 가깝습니다.");
            Assert.Greater(Mathf.Abs(rightKnee - leftKnee), 60f,
                "자유 다리와 지지 다리의 무릎 굽힘이 충분히 갈라지지 않았습니다.");

            // (c) ★ 진짜 판정 — 순방향 기구학 실측. 자유 다리(오른쪽) 발끝의 월드 좌표와
            //     지지 다리(왼쪽) 무릎(= LeftLegLower 마디의 원점) 월드 좌표를 두 축으로 나눠 잰다.
            pose.GetFootWorldPositions(out Vector2 _, out Vector2 rightFootWorld);
            Vector3 supportKneeWorld = FindInRig("LeftLeg/LeftLegLower").position;

            float verticalGapInHeights = Mathf.Abs(rightFootWorld.y - supportKneeWorld.y) / g.TotalHeight;
            float fullGapInHeights = Vector2.Distance(rightFootWorld, supportKneeWorld) / g.TotalHeight;

            Debug.Log($"{LogPrefix} 자유다리 발끝={rightFootWorld} / 지지다리 무릎={(Vector2)supportKneeWorld} " +
                $"/ 수직간극={verticalGapInHeights:F5} H (허용 {VerticalContactToleranceHeights:F3}) " +
                $"/ 2D간극={fullGapInHeights:F5} H (기준 {ObservedFullGapHeights:F3}±{FullGapToleranceHeights:F3})");

            // ★ 수직(높이)만은 문서의 「닿는다」 주장이 실제로 성립한다.
            Assert.Less(verticalGapInHeights, VerticalContactToleranceHeights,
                $"자유 다리 발끝의 높이가 지지 다리 무릎 높이에서 {verticalGapInHeights:F5} H 벗어났습니다" +
                $"(허용 {VerticalContactToleranceHeights:F3} H) — retiré의 «같은 높이» 자체가 깨졌습니다.");

            // ★ 2D 거리는 실측으로 확정한 값(약 0.034H) 근처에 잠근다 — 0에 가깝다고 우기지 않는다.
            //   design-motion의 「정확히 닿는다」는 수직 성분만 계산한 것이라 실제로는 그만큼 벌어져
            //   있다(클래스 문서 참고). 이 값이 갑자기 커지면(다리 비율 변경 등) 재검토가 필요하다.
            Assert.AreEqual(ObservedFullGapHeights, fullGapInHeights, FullGapToleranceHeights,
                $"자유 다리 발끝-지지 다리 무릎의 2D 거리가 {fullGapInHeights:F5} H로, 실측 기준값 " +
                $"({ObservedFullGapHeights:F3}±{FullGapToleranceHeights:F3} H)에서 벗어났습니다 — 다리 " +
                "비율이나 retiré 각도가 바뀌었을 수 있습니다. design-motion에 재검산을 요청하십시오.");
        }

        /// <summary>★ 네거티브 컨트롤 — 위 검사가 «항상 작다»고 우기는 헐거운 검사가 아님을 증명한다.
        /// 중립(envelope=0, 춤을 아예 안 추는 Idle에 가까운 자세)에서는 같은 거리 계산이 <b>크게</b>
        /// 벌어져야 한다 — 안 벌어지면 위 (c)는 아무것도 재지 않은 것이다.</summary>
        [Test]
        public void 네거티브컨트롤_중립_포즈에서는_발끝이_무릎에서_멀다()
        {
            RigGeometry g = ReadPrefabGeometry();
            StickmanPoseAnimator.DancePoseSettings dance = BuildDanceSettings(out StickConfig config);
            StickmanPoseAnimator pose = BuildRig(g);

            float holdPhase = MidHoldPhase();
            Settle(pose, dance, config, holdPhase, envelope: 0f);   // 춤 자세를 전혀 섞지 않는다.

            pose.GetFootWorldPositions(out Vector2 _, out Vector2 rightFootWorld);
            Vector3 supportKneeWorld = FindInRig("LeftLeg/LeftLegLower").position;
            float gapInHeights = Vector2.Distance(rightFootWorld, supportKneeWorld) / g.TotalHeight;

            Debug.Log($"{LogPrefix} 네거티브 컨트롤(중립) — 발끝-무릎 거리 = {gapInHeights:F4} H " +
                "(위 본검사의 허용선보다 훨씬 커야 프로브가 살아있다는 뜻이다).");

            // retiré 유지 중 실측 2D 간극(약 0.034H)보다도 뚜렷이 커야 한다 — 안 그러면 위 ②(c)의
            // "이 값이 갑자기 커지면 재검토" 경보가 애초에 아무 것도 구분하지 못하는 검사가 된다.
            Assert.Greater(gapInHeights, (ObservedFullGapHeights + FullGapToleranceHeights) * 2f,
                $"중립 포즈에서도 발끝-무릎 거리가 {gapInHeights:F5} H로 작습니다 — 이 리그 형태에서는 " +
                "위 ②(c) 검사가 무엇을 춰도 통과하는 공허한 검사일 수 있습니다.");
        }

        // ============================================================================
        // ③ ★ 근본 원인 — 같은 위상에서 SetFacing만 뒤집으면 전신이 그 자리에서 순간 미러링된다
        // ============================================================================

        /// <summary>
        /// 사용자가 본 「이상함」의 가장 유력한 정체를 수치로 못박는다. <c>DanceState.
        /// DrivePirouetteFacing</c>이 0.35초마다 부르는 <c>SetFacingSign</c>(= <c>SetFacing</c>)이
        /// 실제로 하는 일은 <b>정지한 삼각형 포즈를 순간 좌우 미러링하는 것뿐</b>이지 «자세가 계속
        /// 이어서 도는 것»이 아니다 — 그래서 4번의 반전 사이(각 0.35초)는 완전히 정지한 그림이고,
        /// 반전 순간만 순간이동처럼 보인다.
        ///
        /// <para>★ <c>GetUpperAngles</c>/<c>GetJointAngles</c>는 «방향 중립 공간」의
        /// <c>Segment.CurrentAngle</c>이 아니라 <c>ApplyAngle</c>이 실제로 쓴 <b>렌더 각도</b>
        /// (<c>Transform.localEulerAngles.z</c>, <c>= CurrentAngle × facingSign</c>)를 읽는다 —
        /// 그래서 반전 뒤 이 값은 <b>그대로 남지 않고 크기는 같은 채 부호만 뒤집힌다</b>. 이 파일이
        /// 처음에 "각도가 불변"이라고 잘못 예측했다가 실측(<c>Assert.AreEqual(62°, -62°)</c> 실패)으로
        /// 정정한 지점이 바로 여기다 — 「생성기와 검사기가 같이 틀린다」의 실물 사례를 만들지 않으려고
        /// 이 사실을 그대로 남긴다.</para>
        ///
        /// <para>이 검사가 초록이면 "정지 포즈의 순간 미러링"이라는 사실이 재확인된 것이고, 이 사실이
        /// 바뀌면(예: 미러링에 보간을 추가하거나 진짜 회전으로 바꾸면) 이 검사가 먼저 반응한다 —
        /// 의도적 변경인지 되묻는 안전장치다.</para>
        /// </summary>
        [Test]
        public void 위상을_고정한_채_SetFacing만_뒤집으면_모든_관절각과_발끝이_그_자리에서_미러링된다()
        {
            RigGeometry g = ReadPrefabGeometry();
            StickmanPoseAnimator.DancePoseSettings dance = BuildDanceSettings(out StickConfig config);
            StickmanPoseAnimator pose = BuildRig(g);

            float holdPhase = MidHoldPhase();
            Settle(pose, dance, config, holdPhase, envelope: 1f);

            pose.GetUpperAngles(out float leftUpperBefore, out float rightUpperBefore, out _, out _);
            pose.GetJointAngles(out float leftKneeBefore, out float rightKneeBefore, out _, out _);
            pose.GetFootWorldPositions(out Vector2 leftFootBefore, out Vector2 rightFootBefore);

            Assert.AreEqual(1f, pose.FacingSign, 0f, "준비 실패 — 초기 방향이 +1이 아닙니다.");

            // ★ 시간을 흘리지 않는다 — DanceState.DrivePirouetteFacing이 실제로 반전 순간에 하는 일이
            //   "이 한 호출"이 전부이기 때문이다(±9° 팔 스윕은 _idleTime 기반이라 별개다).
            pose.SetFacing(-1f);

            pose.GetUpperAngles(out float leftUpperAfter, out float rightUpperAfter, out _, out _);
            pose.GetJointAngles(out float leftKneeAfter, out float rightKneeAfter, out _, out _);
            pose.GetFootWorldPositions(out Vector2 leftFootAfter, out Vector2 rightFootAfter);

            Debug.Log($"{LogPrefix} 반전 전 — 허벅지(자유/지지)=({rightUpperBefore:F2},{leftUpperBefore:F2})° " +
                $"발끝(자유)={rightFootBefore}");
            Debug.Log($"{LogPrefix} 반전 후 — 허벅지(자유/지지)=({rightUpperAfter:F2},{leftUpperAfter:F2})° " +
                $"발끝(자유)={rightFootAfter}");

            // (a) ★ 렌더 각도(ApplyAngle이 실제로 쓴 값)는 facingSign이 곱해진 값이라
            //     크기는 그대로, 부호만 정확히 뒤집혀야 한다 — "불변"이 아니라 "미러링"이다.
            const float zeroTol = 1e-4f;
            Assert.AreEqual(-rightUpperBefore, rightUpperAfter, zeroTol,
                $"SetFacing 반전 후 자유 다리 허벅지 각도가 정확히 미러링되지 않았습니다 " +
                $"({rightUpperBefore:F3}° -> {rightUpperAfter:F3}°) — 이러면 이 「회전」이 이미 이 파일이 " +
                "아는 것과 다른 메커니즘으로 바뀐 것입니다(재진단 필요).");
            Assert.AreEqual(-rightKneeBefore, rightKneeAfter, zeroTol,
                $"SetFacing 반전 후 자유 다리 무릎 각도가 정확히 미러링되지 않았습니다 " +
                $"({rightKneeBefore:F3}° -> {rightKneeAfter:F3}°).");
            Assert.AreEqual(-leftUpperBefore, leftUpperAfter, zeroTol,
                $"SetFacing 반전 후 지지 다리 허벅지 각도가 정확히 미러링되지 않았습니다 " +
                $"({leftUpperBefore:F3}° -> {leftUpperAfter:F3}°).");
            Assert.AreEqual(-leftKneeBefore, leftKneeAfter, zeroTol,
                $"SetFacing 반전 후 지지 다리 무릎 각도가 정확히 미러링되지 않았습니다 " +
                $"({leftKneeBefore:F3}° -> {leftKneeAfter:F3}°).");

            // (b) 그런데 월드 발끝 좌표는 순간(보간 없이) x부호만 정확히 뒤집힌다 — 미러링이지 회전이 아니다.
            Assert.AreEqual(-rightFootBefore.x, rightFootAfter.x, 1e-4f,
                "SetFacing 반전 후 자유 다리 발끝 x가 정확히 미러링되지 않았습니다.");
            Assert.AreEqual(rightFootBefore.y, rightFootAfter.y, 1e-4f,
                "SetFacing 반전으로 y가 바뀌었습니다 — 좌우 미러는 y에 영향이 없어야 합니다.");
            Assert.AreEqual(-leftFootBefore.x, leftFootAfter.x, 1e-4f,
                "SetFacing 반전 후 지지 다리 발끝 x가 정확히 미러링되지 않았습니다.");

            // (c) 검사가 공허하지 않다는 확인 — 애초에 발끝이 원점(x=0)이면 미러링해도 안 움직인다.
            Assert.Greater(Mathf.Abs(rightFootBefore.x), 0.01f,
                "자유 다리 발끝의 x가 0에 가깝습니다 — 미러링 검사가 공허합니다.");

            Debug.Log($"{LogPrefix} 결론 — 렌더 각도가 크기 불변·부호만 반전(오차<{zeroTol}) + 발끝 x " +
                "정확히 반전 = 이 「회전」은 연속 회전이 아니라 정지 포즈의 순간 좌우 미러링입니다. " +
                "docs/UX_MOTION_DANCE.md §11-2가 자백한 «회전 vs 깜빡임» 미확인 위험이 이 수치로 확정됩니다.");
        }

        // ============================================================================
        // 헬퍼
        // ============================================================================

        /// <summary>③ 회전 유지 구간(스핀 포락선이 1로 평평한 구간)의 대표 위상. <c>BeatEdge(2)</c>는
        /// ②가 끝나는 지점(스핀 완성), <c>BeatEdge(3)</c>는 ③이 끝나는 지점(닫기 시작)이다 —
        /// 그 사이 어디를 찍어도 <c>DanceState.ApplyPirouettePose</c>의 <c>spin</c>은 정확히 1이다.</summary>
        private static float MidHoldPhase()
        {
            float e1 = StickmanPoseAnimator.BeatEdge(StickmanPoseAnimator.DanceMovePirouette, 2);
            float e2 = StickmanPoseAnimator.BeatEdge(StickmanPoseAnimator.DanceMovePirouette, 3);
            return Mathf.Lerp(e1, e2, 0.5f);
        }

        private static StickmanPoseAnimator.DancePoseSettings BuildDanceSettings(out StickConfig config)
        {
            config = ScriptableObject.CreateInstance<StickConfig>();
            // StickmanBlackboard.BuildDancePoseSettings()는 Body가 없으면 CharacterHeightWorld를
            // StickConfig.BaselineCharacterTotalHeight로 되메운다 — Body/Metrics 없이도 안전하다
            // (Tests/EditMode/DancePoseFallbackParityTests.cs와 같은 관례).
            var blackboard = new StickmanBlackboard { Config = config };
            return blackboard.BuildDancePoseSettings();
        }

        private void Settle(StickmanPoseAnimator pose, in StickmanPoseAnimator.DancePoseSettings dance,
            StickConfig config, float phase01, float envelope)
        {
            StickmanPoseAnimator.PoseSettings settings = StaticPoseSettings();
            for (int i = 0; i < SettleFrames; i++)
            {
                pose.ApplyDancePose(Dt, settings, config.dancePoseSmoothingRate, dance,
                    StickmanPoseAnimator.DanceMovePirouette, phase01, envelope);
            }
        }

        private static StickmanPoseAnimator.PoseSettings StaticPoseSettings()
            => new StickmanPoseAnimator.PoseSettings(
                legSpread: 12f, armSpread: 40f, idleKnee: 4f, idleElbow: 10f,
                breathAmplitude: 0f, breathFrequencyHz: 0f, breathArmDegrees: 0f);

        /// <summary><c>Transform.Find</c>는 <b>직계 자식만</b> 이름으로 찾는다 — 아래 마디("…Lower")는
        /// 위 마디의 자식이라 <c>"LeftLeg/LeftLegLower"</c>처럼 경로로 찾아야 한다.</summary>
        private Transform FindInRig(string path)
        {
            Transform t = _root.transform.Find(path);
            Assert.IsNotNull(t, $"리그에서 {path}을 찾지 못했습니다.");
            return t;
        }

        // ============================================================================
        // 리그 — Tests/EditMode/FacingFlipBodySplitTests.cs와 같은 패턴(치수는 프리팹 실측).
        // ★ 그 파일과 달리 다리 아래 마디 길이는 「위 마디와 같은 값」으로 근사하지 않고
        //   BoxCollider2D.size.y(그 파일도 궁극적으로 같은 값을 쓰지만 다리에서는 재사용하지
        //   않는다)를 직접 읽는다 — retiré 접촉 판정은 실제 정강이 길이가 핵심이라 근사가 위험하다.
        // ============================================================================

        private StickmanPoseAnimator BuildRig(RigGeometry g)
        {
            _root = new GameObject("PirouetteTriangleRig");
            _root.transform.position = Vector3.zero;

            AddLimb("LeftLeg", g.HipY, g.LegUpperLength, g.LegLowerLength);
            AddLimb("RightLeg", g.HipY, g.LegUpperLength, g.LegLowerLength);
            AddLimb("LeftArm", g.ShoulderY, g.ArmUpperLength, g.ArmLowerLength);
            AddLimb("RightArm", g.ShoulderY, g.ArmUpperLength, g.ArmLowerLength);

            var torso = new GameObject("Torso");
            torso.transform.SetParent(_root.transform, false);
            torso.transform.localPosition = new Vector3(0f, g.TorsoCenterY, 0f);

            var head = new GameObject("Head");
            head.transform.SetParent(_root.transform, false);
            head.transform.localPosition = new Vector3(0f, g.HeadY, 0f);

            var pose = new StickmanPoseAnimator(_root.transform);
            Assert.IsTrue(pose.HasLimbs, "리그에서 팔다리를 찾지 못했습니다 — 이름 규약이 바뀌었을 수 있습니다.");
            return pose;
        }

        private void AddLimb(string name, float attachY, float upperLength, float lowerLength)
        {
            var upper = new GameObject(name);
            upper.transform.SetParent(_root.transform, false);
            upper.transform.localPosition = new Vector3(0f, attachY, 0f);
            var upperBox = upper.AddComponent<BoxCollider2D>();
            upperBox.size = new Vector2(0.05f, upperLength);
            upperBox.offset = new Vector2(0f, -upperLength * 0.5f);

            var lower = new GameObject(name + "Lower");
            lower.transform.SetParent(upper.transform, false);
            lower.transform.localPosition = new Vector3(0f, -upperLength, 0f);
            var lowerBox = lower.AddComponent<BoxCollider2D>();
            lowerBox.size = new Vector2(0.05f, lowerLength);
            lowerBox.offset = new Vector2(0f, -lowerLength * 0.5f);
        }

        // ============================================================================
        // 프리팹 파서 — 기대값의 유일한 출처(FacingFlipBodySplitTests와 별도 구현 — 같은 파서가
        // 같은 실수를 공유하면 「독립 검증」이 아니게 된다).
        // ============================================================================

        private struct RigGeometry
        {
            public float HipY;
            public float ShoulderY;
            public float TorsoCenterY;
            public float HeadY;
            public float HeadRadius;
            public float LegUpperLength;
            public float LegLowerLength;
            public float ArmUpperLength;
            public float ArmLowerLength;

            public float ShoulderAboveHip => ShoulderY - HipY;
            public float TotalHeight => HeadY + HeadRadius;
        }

        private static RigGeometry _cachedGeometry;
        private static bool _hasCachedGeometry;

        private static RigGeometry ReadPrefabGeometry()
        {
            if (_hasCachedGeometry) return _cachedGeometry;

            string path = Path.Combine(Application.dataPath, PrefabRelativePath);
            Assert.IsTrue(File.Exists(path), $"프리팹을 찾지 못했습니다: {path}");
            string text = File.ReadAllText(path);

            string[] docs = Regex.Split(text, @"(?m)^--- !u!\d+ &");
            var names = new System.Collections.Generic.Dictionary<string, string>();
            var jointAnchors = new System.Collections.Generic.Dictionary<string, float>();
            var boxSizes = new System.Collections.Generic.Dictionary<string, float>();
            var transforms = new System.Collections.Generic.Dictionary<string, float>();
            var linePoints = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Vector2>>();

            foreach (string doc in docs)
            {
                if (!Regex.IsMatch(doc, @"(?m)^GameObject:")) continue;
                Match id = Regex.Match(doc, @"^(\d+)");
                Match name = Regex.Match(doc, @"(?m)^\s*m_Name:\s*(.+)$");
                if (id.Success && name.Success) names[id.Groups[1].Value] = name.Groups[1].Value.Trim();
            }

            foreach (string doc in docs)
            {
                Match owner = Regex.Match(doc, @"m_GameObject:\s*\{fileID:\s*(\d+)\}");
                if (!owner.Success) continue;
                if (!names.TryGetValue(owner.Groups[1].Value, out string ownerName)) continue;

                if (Regex.IsMatch(doc, @"(?m)^HingeJoint2D:"))
                {
                    Match anchor = Regex.Match(doc, @"m_ConnectedAnchor:\s*\{x:\s*(-?[\d.eE+-]+),\s*y:\s*(-?[\d.eE+-]+)\}");
                    if (anchor.Success) jointAnchors[ownerName] = ParseFloat(anchor.Groups[2].Value);
                }
                else if (Regex.IsMatch(doc, @"(?m)^BoxCollider2D:"))
                {
                    Match size = Regex.Match(doc, @"m_Size:\s*\{x:\s*(-?[\d.eE+-]+),\s*y:\s*(-?[\d.eE+-]+)\}");
                    if (size.Success) boxSizes[ownerName] = ParseFloat(size.Groups[2].Value);
                }
                else if (Regex.IsMatch(doc, @"(?m)^Transform:"))
                {
                    Match pos = Regex.Match(doc, @"m_LocalPosition:\s*\{x:\s*(-?[\d.eE+-]+),\s*y:\s*(-?[\d.eE+-]+)");
                    if (pos.Success) transforms[ownerName] = ParseFloat(pos.Groups[2].Value);
                }
                else if (Regex.IsMatch(doc, @"(?m)^LineRenderer:"))
                {
                    linePoints[ownerName] = ReadLinePositions(doc);
                }
            }

            var g = new RigGeometry
            {
                HipY = RequireAnchor(jointAnchors, "LeftLeg"),
                ShoulderY = RequireAnchor(jointAnchors, "LeftArm"),
                // ★ 실제 마디 길이는 BoxCollider2D.size.y다(StickmanPoseAnimator.BuildSegment와 같은 출처).
                //   위/아래 마디 길이가 다를 수 있으므로 각각 따로 읽는다 — 재사용하지 않는다.
                LegUpperLength = RequireBoxSize(boxSizes, "LeftLeg"),
                LegLowerLength = RequireBoxSize(boxSizes, "LeftLegLower"),
                ArmUpperLength = RequireBoxSize(boxSizes, "LeftArm"),
                ArmLowerLength = RequireBoxSize(boxSizes, "LeftArmLower"),
                TorsoCenterY = RequireTransform(transforms, "Torso"),
                HeadY = RequireTransform(transforms, "Head"),
                HeadRadius = MaxRadius(linePoints, "HeadOutline"),
            };

            // 파서 생존 확인 — 좌우가 같은 값이어야 한다.
            Assert.AreEqual(g.HipY, RequireAnchor(jointAnchors, "RightLeg"), 1e-7f,
                "좌우 고관절 부착점이 다릅니다 — 파서나 프리팹 중 하나가 이상합니다.");
            Assert.AreEqual(g.LegUpperLength, RequireBoxSize(boxSizes, "RightLeg"), 1e-7f,
                "좌우 허벅지 길이가 다릅니다.");
            Assert.AreEqual(g.LegLowerLength, RequireBoxSize(boxSizes, "RightLegLower"), 1e-7f,
                "좌우 정강이 길이가 다릅니다.");
            Assert.Greater(g.ShoulderAboveHip, 0f, "어깨가 엉덩이보다 아래에 있습니다 — 파싱이 깨졌습니다.");
            Assert.Greater(g.TotalHeight, 0f, "신장이 0 이하입니다 — 파싱이 깨졌습니다.");
            // ★ 위/아래 마디 길이가 우연히 같다고 가정하지 않는다(FacingFlipBodySplitTests가 그렇게
            //   근사했다 — 그 파일의 목적에는 무해하지만 이 파일의 목적에는 치명적이다).
            Assert.AreNotEqual(g.LegUpperLength, g.LegLowerLength,
                "허벅지와 정강이 길이가 정확히 같습니다 — 파서가 같은 값을 두 번 읽었을 수 있습니다.");

            _cachedGeometry = g;
            _hasCachedGeometry = true;
            return g;
        }

        private static System.Collections.Generic.List<Vector2> ReadLinePositions(string doc)
        {
            var result = new System.Collections.Generic.List<Vector2>();
            int start = doc.IndexOf("m_Positions:", System.StringComparison.Ordinal);
            if (start < 0) return result;

            foreach (Match m in Regex.Matches(doc.Substring(start),
                @"(?m)^\s*-\s*\{x:\s*(-?[\d.eE+-]+),\s*y:\s*(-?[\d.eE+-]+),\s*z:\s*(-?[\d.eE+-]+)\}"))
            {
                result.Add(new Vector2(ParseFloat(m.Groups[1].Value), ParseFloat(m.Groups[2].Value)));
            }
            return result;
        }

        private static float ParseFloat(string s)
            => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        private static float RequireAnchor(System.Collections.Generic.Dictionary<string, float> map, string owner)
        {
            Assert.IsTrue(map.ContainsKey(owner),
                $"프리팹에서 {owner}의 HingeJoint2D.connectedAnchor를 읽지 못했습니다.");
            return map[owner];
        }

        private static float RequireBoxSize(System.Collections.Generic.Dictionary<string, float> map, string owner)
        {
            Assert.IsTrue(map.ContainsKey(owner),
                $"프리팹에서 {owner}의 BoxCollider2D.size를 읽지 못했습니다.");
            return map[owner];
        }

        private static float RequireTransform(System.Collections.Generic.Dictionary<string, float> map, string owner)
        {
            Assert.IsTrue(map.ContainsKey(owner),
                $"프리팹에서 {owner}의 Transform.localPosition을 읽지 못했습니다.");
            return map[owner];
        }

        private static float MaxRadius(
            System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Vector2>> map, string owner)
        {
            Assert.IsTrue(map.ContainsKey(owner) && map[owner].Count > 0,
                $"프리팹에서 {owner}의 LineRenderer 점을 읽지 못했습니다.");
            float max = 0f;
            foreach (Vector2 p in map[owner]) max = Mathf.Max(max, p.magnitude);
            return max;
        }
    }
}
