using System.IO;
using NUnit.Framework;
using UnityEngine;
using StickMate.Interaction;
using StickMate.States;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 초상화 마디가 <b>조용히 사라지는</b> 실패 양상에 대한 회귀 검사 (2026-09-01).
    ///
    /// ============================================================================
    /// 무엇을 잠그는가
    /// ============================================================================
    /// 페르소나가 "부팅 직후 첫 열기에 정보창 초상화가 <b>검은 원 + 세로 막대</b>만 나온다"를 100%
    /// 재현했다(팔·다리·망토·모자 전부 소실). 스택은
    /// <c>LimbCurveRenderer.FillArcs → BuildLimbPolyline → CharacterPortraitStage.DrawLimb</c>였다.
    ///
    /// 그 <b>직접 원인</b>은 다른 라운드가 이미 고쳤다. 이 파일이 잠그는 것은 원인이 아니라
    /// <b>실패 양상</b>이다 — 예전 DrawLimb은 어떤 실패든 결과가 똑같이 "팔다리 없음"이었다:
    /// <code>
    ///     int count = LimbCurveRenderer.BuildLimbPolyline(...);
    ///     if (count &lt;= 0) return;      // ← 아무 흔적 없이 사라진다
    /// </code>
    /// 이제는 굽히기(원호 필렛)에 실패해도 <b>관절만 각진 직선 3점</b>으로 반드시 그린다.
    /// 없어지는 것보다 뻣뻣한 것이 낫다.
    ///
    /// ============================================================================
    /// ★ 대조군이 있다 — "항상 참인 단언"이 아님을 증명한다
    /// ============================================================================
    /// 이 저장소는 "폴백을 빼도 통과하는 테스트"를 여러 번 겪었다. 그래서
    /// <see cref="대조군_원본_빌더는_버퍼가_한_칸만_모자라도_아무것도_돌려주지_않는다"/>가
    /// <b>먼저</b> 있다: 같은 짧은 버퍼를 원본 빌더에 넣으면 0이 나온다(= 옛 코드였다면 팔다리 소실).
    /// 그 다음 같은 입력으로 폴백이 3점을 돌려주는지 본다. 대조군이 깨지면 이 파일의 나머지 단언은
    /// 아무것도 증명하지 못하는 상태가 된 것이므로 함께 빨개진다.
    ///
    /// <para><b>수치는 전부 프로덕션 상수를 참조</b>한다(<see cref="LimbCurveRenderer.PolylinePointCount"/>,
    /// <see cref="LimbCurveRenderer.PolylineJointIndex"/>,
    /// <see cref="CharacterPortraitStage.StraightLimbPointCount"/>) — 숫자를 베끼지 않는다.
    /// 마디 길이/각도만 이 테스트가 고른 <b>입력값</b>이다(프로덕션 상수가 아니다).</para>
    ///
    /// <para><b>플랫폼</b>: 플랫폼 중립(순수 기하 계산). Windows/macOS 동일.</para>
    /// </summary>
    public sealed class PortraitLimbFallbackTests
    {
        // 이 테스트가 고른 입력값 — 프로덕션 상수의 사본이 아니다.
        // 굽힘각을 크게 잡은 이유: 필렛이 관절을 실제로 얼마나 깎는지가 눈에 보여야
        // "직선 폴백 = 필렛 없는 같은 마디"라는 단언이 의미를 갖는다(작은 각에서는 둘이 거의 같다).
        private const float Upper = 0.52f;
        private const float Lower = 0.47f;
        private const float Bend = 40f;
        private const float Stroke = 0.045f;
        private const float Tol = 1e-5f;

        private static Vector3[] FullBuffer() => new Vector3[LimbCurveRenderer.PolylinePointCount];
        private static Vector3[] ShortBuffer() => new Vector3[LimbCurveRenderer.PolylinePointCount - 1];

        // ====================================================================
        // ① 대조군 — 폴백이 없으면 실제로 실패한다
        // ====================================================================

        [Test]
        public void 대조군_원본_빌더는_버퍼가_한_칸만_모자라도_아무것도_돌려주지_않는다()
        {
            int raw = LimbCurveRenderer.BuildLimbPolyline(Upper, Lower, Bend, Stroke, ShortBuffer());

            Assert.AreEqual(0, raw,
                $"원본 빌더가 {LimbCurveRenderer.PolylinePointCount - 1}칸 버퍼에서 {raw}점을 돌려줬습니다 — " +
                "이 대조군이 깨지면 아래 폴백 검사들은 '항상 참인 단언'이 됩니다(폴백을 지워도 통과한다는 뜻).");
        }

        // ====================================================================
        // ② 폴백 — 사라지지 않고 뻣뻣해진다
        // ====================================================================

        [Test]
        public void 버퍼가_모자라면_굽히지_못해도_직선_마디를_그린다()
        {
            Vector3[] buffer = ShortBuffer();
            int count = CharacterPortraitStage.BuildLimbPolylineResilient(Upper, Lower, Bend, Stroke,
                buffer, out CharacterPortraitStage.LimbPolylineSource origin, out System.Exception error);

            Assert.AreEqual(CharacterPortraitStage.StraightLimbPointCount, count,
                "버퍼가 모자랄 때 마디가 사라졌습니다 — 굽히기는 장식이고 '팔다리가 몸에 붙어 있다'가 사실입니다.");
            Assert.AreEqual(CharacterPortraitStage.LimbPolylineSource.StraightBufferTooSmall, origin,
                "폴백 사유가 '버퍼 부족'으로 보고되지 않았습니다 — 호출부가 남길 로그의 내용이 틀어집니다.");
            Assert.IsNull(error, "예외가 없었는데 예외가 보고됐습니다.");
        }

        [Test]
        public void 직선_대체_마디는_굽힌_마디와_양_끝이_정확히_같고_관절만_각진다()
        {
            Vector3[] curved = FullBuffer();
            int curvedCount = LimbCurveRenderer.BuildLimbPolyline(Upper, Lower, Bend, Stroke, curved);
            Assert.AreEqual(LimbCurveRenderer.PolylinePointCount, curvedCount, "굽힌 기준 그림을 만들지 못했습니다.");

            Vector3[] straight = ShortBuffer();
            int count = CharacterPortraitStage.BuildLimbPolylineResilient(Upper, Lower, Bend, Stroke,
                straight, out _, out _);
            Assert.AreEqual(CharacterPortraitStage.StraightLimbPointCount, count);

            // (a) 뿌리와 끝점이 같아야 손끝/발끝 위치와 액자 프레이밍(잉크 범위)이 흔들리지 않는다.
            AssertSamePoint(curved[0], straight[0], "뿌리");
            AssertSamePoint(curved[curvedCount - 1], straight[count - 1], "끝점");

            // (b) 가운데 점은 <b>필렛이 없는</b> 관절, 즉 정확히 (0, -위마디길이)다.
            AssertSamePoint(new Vector3(0f, -Upper, 0f), straight[1], "관절");

            // (c) 그리고 그 관절은 굽힌 그림의 관절과 <b>달라야</b> 한다 — 같다면 필렛이 아무것도
            //     깎지 않았다는 뜻이고, 그러면 (a)(b)는 아무것도 구분하지 못한다.
            float pulled = Vector3.Distance(curved[LimbCurveRenderer.PolylineJointIndex], straight[1]);
            Assert.Greater(pulled, Stroke * 0.1f,
                $"굽힌 관절과 각진 관절의 거리가 {pulled:F6}뿐입니다 — 이 입력에서는 필렛이 사실상 " +
                "아무것도 깎지 않으므로, 이 테스트가 '직선 대체'를 구분하지 못합니다(입력값을 키우세요).");
        }

        [Test]
        public void 버퍼가_충분하면_원본과_한_점도_다르지_않다()
        {
            Vector3[] expected = FullBuffer();
            int rawCount = LimbCurveRenderer.BuildLimbPolyline(Upper, Lower, Bend, Stroke, expected);

            Vector3[] actual = FullBuffer();
            int count = CharacterPortraitStage.BuildLimbPolylineResilient(Upper, Lower, Bend, Stroke,
                actual, out CharacterPortraitStage.LimbPolylineSource origin, out _);

            Assert.AreEqual(CharacterPortraitStage.LimbPolylineSource.Curved, origin,
                "정상 입력인데 폴백 경로가 돌았습니다 — 평소 그림이 뻣뻣해집니다.");
            Assert.AreEqual(rawCount, count, "정상 경로의 점 개수가 원본과 다릅니다.");
            for (int i = 0; i < count; i++) AssertSamePoint(expected[i], actual[i], $"{i}번 점");
        }

        // ====================================================================
        // ③ 정말로 못 그리는 경우 — 조용히 넘어가지 않고 사유를 보고한다
        // ====================================================================

        [Test]
        public void 직선_3점조차_담을_수_없으면_사유를_붙여_0을_돌려준다()
        {
            var tiny = new Vector3[CharacterPortraitStage.StraightLimbPointCount - 1];
            int count = CharacterPortraitStage.BuildLimbPolylineResilient(Upper, Lower, Bend, Stroke,
                tiny, out CharacterPortraitStage.LimbPolylineSource origin, out _);

            Assert.AreEqual(0, count);
            Assert.AreEqual(CharacterPortraitStage.LimbPolylineSource.NotDrawn, origin,
                "그리지 못했는데 사유가 NotDrawn이 아닙니다 — 호출부가 로그를 남길 근거를 잃습니다.");
        }

        [Test]
        public void 버퍼가_null이어도_예외를_던지지_않고_사유를_보고한다()
        {
            int count = CharacterPortraitStage.BuildLimbPolylineResilient(Upper, Lower, Bend, Stroke,
                null, out CharacterPortraitStage.LimbPolylineSource origin, out _);

            Assert.AreEqual(0, count);
            Assert.AreEqual(CharacterPortraitStage.LimbPolylineSource.NotDrawn, origin);
        }

        [Test]
        public void 길이가_NaN이면_조용히_빈_선이_되지_않고_NotDrawn으로_보고된다()
        {
            // LineRenderer는 NaN 점을 받아도 예외를 던지지 않는다 — 그림만 사라진다.
            // 그 경로가 바로 "조용한 실패"이므로 여기서 잡아 사유를 남긴다.
            Vector3[] buffer = FullBuffer();
            int count = CharacterPortraitStage.BuildLimbPolylineResilient(float.NaN, Lower, Bend, Stroke,
                buffer, out CharacterPortraitStage.LimbPolylineSource origin, out _);

            Assert.AreEqual(0, count);
            Assert.AreEqual(CharacterPortraitStage.LimbPolylineSource.NotDrawn, origin,
                "NaN 마디가 사유 없이 통과했습니다 — 화면에서는 팔다리가 그냥 사라집니다.");
        }

        // ====================================================================
        // ④ 초상화가 <b>실제로</b> 이 폴백을 지난다 (원본 빌더 직접 호출 금지)
        // ====================================================================

        /// <summary>
        /// 위 검사들은 <b>이음매</b>를 검증한다. 초상화가 그 이음매를 실제로 지나가는지는 별개의 사실이라
        /// 소스에서 직접 확인한다 — 누군가 DrawLimb을 원본 빌더 직접 호출로 되돌리면 위 검사는 전부
        /// 초록인 채로 옛 실패 양상이 돌아온다.
        ///
        /// <para>주석 줄은 제외하고 센다: 이 파일은 "예전에는 이렇게 적혀 있었다"를 <b>주석으로</b>
        /// 보존하고 있어서, 걸러내지 않으면 그 설명 문장 때문에 빨개진다.</para>
        /// </summary>
        [Test]
        public void 초상화의_코드는_원본_빌더를_직접_부르지_않는다()
        {
            string path = Path.Combine(Application.dataPath, "_Project", "Scripts", "Interaction",
                "CharacterPortraitStage.cs");
            Assert.IsTrue(File.Exists(path), $"초상화 소스를 찾지 못했습니다: {path}");

            string[] lines = File.ReadAllLines(path);
            int rawCalls = 0, resilientCalls = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("//")) continue;   // 설명 주석(옛 코드 인용 포함)은 코드가 아니다("///"도 포함된다).
                if (lines[i].Contains("LimbCurveRenderer.BuildLimbPolyline(")) rawCalls++;
                if (lines[i].Contains("BuildLimbPolylineResilient(")) resilientCalls++;
            }

            // 네거티브 컨트롤: 걸러내기가 과해서 아무 줄도 안 보게 되면 위 단언이 무의미해진다.
            Assert.GreaterOrEqual(resilientCalls, 2,
                $"소스에서 Resilient 호출을 {resilientCalls}곳밖에 못 찾았습니다(선언 1 + DrawLimb 1 이상이어야 " +
                "합니다) — 주석 걸러내기가 코드까지 지웠거나 파일 구조가 바뀌었습니다. 이 검사는 지금 " +
                "아무것도 확인하지 못하는 상태입니다.");

            Assert.AreEqual(1, rawCalls,
                $"초상화 코드에서 원본 빌더를 {rawCalls}곳에서 부르고 있습니다 — 허용되는 곳은 " +
                "BuildLimbPolylineResilient 안 한 곳뿐입니다. DrawLimb이 직접 부르면 " +
                "버퍼 부족/예외가 다시 '팔다리 소실'로 돌아갑니다.");
        }

        private static void AssertSamePoint(Vector3 expected, Vector3 actual, string what)
        {
            Assert.AreEqual(expected.x, actual.x, Tol, $"{what}의 x가 다릅니다.");
            Assert.AreEqual(expected.y, actual.y, Tol, $"{what}의 y가 다릅니다.");
            Assert.AreEqual(expected.z, actual.z, Tol, $"{what}의 z가 다릅니다.");
        }
    }
}
