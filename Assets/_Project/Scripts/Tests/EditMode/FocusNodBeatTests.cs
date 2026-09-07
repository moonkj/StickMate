using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using StickMate.Core;
using StickMate.States;
using UnityEditor;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 집중 세션 G2 «끄덕임 2박»(2026-09-07 design-motion 개정)의 회귀 못.
    ///
    /// <para><b>왜 필요한가</b>: 옛 G2는 <b>발밑 타이머 링을 내려다보는</b> 제스처였는데 그 링이
    /// 2026-09-06에 삭제됐다 — 대상이 없는데 내려다보는 그림은 절대 불변 원칙 1 위반이라
    /// «끄덕임 2박»으로 교체됐다. 그런데 <b>«몇 박인가»를 잠그는 테스트가 한 건도 없었다</b>:
    /// 누가 <c>StickmanPoseAnimator</c>의 <c>FocusNodRecover01</c> 같은 곡선 경계를 만져 되올림이
    /// 얕아지면 두 박이 <b>한 박으로 뭉쳐도 아무 테스트가 안 잡는다</b>. 이 파일이 그 자리다.</para>
    ///
    /// <para><b>무엇을 어떻게 재는가 — 「목표 곡선」이 아니라 「실제로 보이는 것」을 잰다.</b>
    /// 곡선 상수만 보는 검사는 <b>감쇠를 지나면 두 박이 하나로 뭉치는</b> 경우를 구조적으로 못 본다
    /// (그게 정확히 이 개정이 두려워하는 고장이다). 그래서 프로덕션 곡선
    /// (<c>FocusGestureLeanDegrees</c>)을 프로덕션 감쇠(<c>Damp</c>, 계수는 배포 자산의
    /// <c>bodyLeanSmoothingRate</c>)에 <b>실제로 통과시킨 뒤</b>의 시계열에서 정점을 센다.
    /// <b>이 파일이 만든 식은 하나도 없다</b> — 곡선·감쇠·계수·치수가 전부 프로덕션과 프리팹에서 온다.</para>
    ///
    /// <para><b>★ 합격선을 「pt」가 아니라 「무차원」으로 쓴 이유</b>: 요구는 «2박 진폭이 L1 상시
    /// 흔들림 왕복폭(≈2.07pt)보다 크다»인데, 두 값 모두 <b>머리의 수평 이동</b>이고
    /// <c>이동 = 지렛대 × sin(기울임) × pt환산</c>이다. 지렛대와 pt환산이 <b>양변에서 그대로
    /// 약분</b>되므로 부등식은 <c>sin</c>만으로 정확히 성립한다 — 즉 <b>프리팹 치수를 잘못 읽어도
    /// 이 판정은 틀리지 않는다</b>. pt 값은 사람이 읽을 진단으로만 찍고, 그 계산기는
    /// design-motion 이 독립적으로 낸 2.06~2.07pt로 <b>교정</b>한다(교정이 깨지면 pt 숫자를 폐기한다 —
    /// 부등식 판정은 그와 무관하게 유효하다).</para>
    ///
    /// <para><b>거짓 통과 방지</b>: 정점 세기가 «1박과 2박을 실제로 구분하는가»를 같은 파일에서
    /// <b>네거티브 컨트롤</b>로 증명한다 — 같은 계산기에 G3(외박자 하나짜리 곡선)를 넣으면 정확히
    /// 1개가 나와야 한다. 이게 없으면 «2개»는 «세기가 아무 값이나 2를 낸다»와 구분되지 않는다.</para>
    ///
    /// <para><b>플랫폼</b>: 중립(순수 수치 · 자산 읽기 전용). macOS/Windows 모두 이 제스처는
    /// 같은 <c>Update</c> 주기(60Hz) 위에서 돈다 — 절감 등급이 바꾸는 것은 «보는 사람이 있는»
    /// 구간에서 <c>renderFrameInterval</c>뿐이고 게임 루프가 아니다(<c>FramePacingPolicy.BuildPlan</c>).
    /// 그래도 30Hz 표본을 함께 돌려 박자 판정이 프레임 예산에 기대지 않음을 확인한다.</para>
    /// </summary>
    public sealed class FocusNodBeatTests
    {
        private const string LogPrefix = "[끄덕임박자]";
        private const string PrefabAssetPath = "Assets/_Project/Prefabs/Stickman.prefab";
        private const string DefaultConfigPath = "Assets/_Project/Data/DefaultStickConfig.asset";

        // ====================================================================
        // 프로덕션에서 꺼내 오는 것들 — 이 파일에는 곡선도 감쇠식도 없다
        // ====================================================================

        private static readonly Type Animator = typeof(StickmanPoseAnimator);

        /// <summary>비공개 정적 멤버를 «있음»을 단언하며 꺼낸다 — 이름이 바뀌면 조용히 초록이 되는 대신
        /// 여기서 빨개진다(CLAUDE.md: 니들을 쓰면 그것이 실재함을 같은 테스트에서 못박는다).</summary>
        private static float PrivateConst(string name)
        {
            FieldInfo f = Animator.GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(f,
                $"{LogPrefix} StickmanPoseAnimator.{name} 을 찾지 못했습니다 — 이름이 바뀌었다면 이 검사는 " +
                "아무것도 재지 못하고 조용히 초록이 됩니다.");
            return Convert.ToSingle(f.GetValue(null));
        }

        private static MethodInfo _leanCurve;

        /// <summary>프로덕션 곡선 <c>FocusGestureLeanDegrees(gesture, p, baseLean, glanceTurnsAround)</c>.</summary>
        private static float LeanTargetDegrees(WanderAmbientMotion gesture, float p, float baseLean,
            bool glanceTurnsAround = false)
        {
            if (_leanCurve == null)
            {
                _leanCurve = Animator.GetMethod("FocusGestureLeanDegrees",
                    BindingFlags.NonPublic | BindingFlags.Static, null,
                    new[] { typeof(WanderAmbientMotion), typeof(float), typeof(float), typeof(bool) }, null);
                Assert.IsNotNull(_leanCurve,
                    $"{LogPrefix} StickmanPoseAnimator.FocusGestureLeanDegrees(WanderAmbientMotion, float, " +
                    "float, bool) 을 찾지 못했습니다 — 시그니처가 바뀌었다면 이 파일은 «옛 곡선»을 재고 " +
                    "있을 수도 없이 그냥 죽습니다.");
            }
            return (float)_leanCurve.Invoke(null, new object[] { gesture, p, baseLean, glanceTurnsAround });
        }

        private static MethodInfo _damp;

        /// <summary>프로덕션 감쇠 <c>Damp(current, target, rate, dt)</c>. <b>테스트가 지수식을 다시
        /// 적지 않는다</b> — 다시 적으면 프로덕션이 감쇠 방식을 바꾼 날 둘이 조용히 갈라진다.</summary>
        private static float Damp(float current, float target, float rate, float dt)
        {
            if (_damp == null)
            {
                _damp = Animator.GetMethod("Damp", BindingFlags.NonPublic | BindingFlags.Static, null,
                    new[] { typeof(float), typeof(float), typeof(float), typeof(float) }, null);
                Assert.IsNotNull(_damp,
                    $"{LogPrefix} StickmanPoseAnimator.Damp(float,float,float,float) 을 찾지 못했습니다 — " +
                    "감쇠식을 여기에 베껴 적지 마십시오. 프로덕션이 바꾸면 둘이 갈라집니다.");
            }
            return (float)_damp.Invoke(null, new object[] { current, target, rate, dt });
        }

        private static float LeanSmoothingRate()
        {
            var config = AssetDatabase.LoadAssetAtPath<StickConfig>(DefaultConfigPath);
            Assert.IsNotNull(config, $"{LogPrefix} 배포 설정 자산을 찾지 못했습니다: {DefaultConfigPath}");
            Assert.Greater(config.bodyLeanSmoothingRate, 0f,
                $"{LogPrefix} bodyLeanSmoothingRate 가 {config.bodyLeanSmoothingRate}입니다 — 0 이하면 " +
                "Damp 가 목표로 즉시 점프해 «감쇠를 통과시킨다»는 이 검사의 전제가 사라집니다.");
            return config.bodyLeanSmoothingRate;
        }

        // ====================================================================
        // 시계열 — 프로덕션 곡선 → 프로덕션 감쇠
        // ====================================================================

        /// <summary>제스처 한 번을 <paramml="dt"/> 간격으로 돌린 «실제로 적용된» 기울임(도) 시계열.
        /// 시작값은 <paramref name="startLean"/>(제스처 직전 L1 위상에 따라 달라진다).</summary>
        private static float[] Simulate(WanderAmbientMotion gesture, float durationSeconds, float baseLean,
            float startLean, float rate, float dt, bool glanceTurnsAround = false)
        {
            int steps = Mathf.CeilToInt(durationSeconds / dt);
            Assert.Greater(steps, 20,
                $"{LogPrefix} 표본이 {steps}개뿐입니다 — 이 해상도로는 «두 정점»과 «한 정점»을 " +
                "구분할 수 없습니다.");

            var series = new float[steps + 1];
            float lean = startLean;
            series[0] = lean;
            for (int i = 1; i <= steps; i++)
            {
                float p = Mathf.Clamp01(i * dt / durationSeconds);
                lean = Damp(lean, LeanTargetDegrees(gesture, p, baseLean, glanceTurnsAround), rate, dt);
                series[i] = lean;
            }
            return series;
        }

        /// <summary>기울임(도) 시계열을 <b>머리 수평 이동 단위</b>로 옮긴다 —
        /// <c>이동 = 지렛대 × sin(기울임) × pt환산</c>에서 «지렛대 × pt환산»은 모든 비교 양변에
        /// 공통이므로 떼어 낸다. 그래서 이 단위의 값들은 <b>서로 비교할 때만</b> 뜻이 있고,
        /// 프리팹 치수를 잘못 읽어도 그 비교는 틀리지 않는다.</summary>
        private static float[] ToHeadShiftUnits(float[] leanDegrees)
        {
            var u = new float[leanDegrees.Length];
            for (int i = 0; i < u.Length; i++) u[i] = Mathf.Sin(leanDegrees[i] * Mathf.Deg2Rad);
            return u;
        }

        /// <summary>
        /// «앞으로 숙인» 방향의 <b>국소 최댓값</b> 인덱스들. 잡음/평탄부에 속지 않도록
        /// <paramref name="minProminence"/> 이상 <b>올라갔다 내려온</b> 봉우리만 센다.
        /// 입력과 문턱은 둘 다 <see cref="ToHeadShiftUnits"/> 단위다.
        ///
        /// <para>★★ <b>문턱을 «깊이의 몇 %»로 잡으면 안 된다</b>(2026-09-07 자기 반려): 처음에
        /// «끄덕임 깊이의 5%»로 썼더니, 되올림 구간을 좁혀 <b>두 박 사이 골이 눈에 안 보일 만큼
        /// 얕아진</b> 돌연변이(N1: <c>FocusNodRecover01</c> 0.48 → 0.30)가 <b>그대로 초록</b>이었다 —
        /// 신호에는 여전히 봉우리가 둘 있지만 <b>화면에서는 한 번 끄덕인 것</b>이다. 이 검사가 막아야
        /// 하는 고장이 정확히 그것인데 못 잡았다.</para>
        ///
        /// <para>그래서 문턱을 <b>가시성</b>에 묶는다: 두 박을 가르는 골은 <b>L1 상시 흔들림 왕복폭
        /// 이상</b>이어야 한다. 배경으로 늘 일어나는 흔들림보다 얕은 골은 사람이 «박의 경계»로 읽지
        /// 않는다 — 진폭 판정에 쓰는 것과 <b>같은 자</b>다.</para>
        ///
        /// <para>평탄한 꼭대기(유지 구간이 감쇠로 점근하는 형태)는 그 구간의 <b>마지막</b> 표본
        /// 하나만 봉우리로 센다 — 그렇지 않으면 한 박이 수십 개로 세어진다.</para>
        /// </summary>
        private static List<int> FindForwardPeaks(float[] series, float minProminence)
        {
            var peaks = new List<int>();
            int i = 1;
            while (i < series.Length - 1)
            {
                // 이 표본이 왼쪽보다 크지 않으면 봉우리가 아니다.
                if (series[i] <= series[i - 1]) { i++; continue; }
                // 같은 높이가 이어지면(평탄 꼭대기) 끝까지 밀어 마지막 표본을 대표로 삼는다.
                int j = i;
                while (j + 1 < series.Length && Mathf.Approximately(series[j + 1], series[j])) j++;
                if (j + 1 >= series.Length) break;
                if (series[j + 1] < series[j])
                {
                    float rise = series[j] - LocalFloor(series, j, -1);
                    float fall = series[j] - LocalFloor(series, j, +1);
                    if (Mathf.Min(rise, fall) >= minProminence) peaks.Add(j);
                }
                i = j + 1;
            }
            return peaks;
        }

        /// <summary>봉우리 <paramref name="peak"/>에서 <paramref name="dir"/> 방향으로 내려가다 다시
        /// 올라가기 직전의 최저점(= 그 쪽 골). 돌출도(prominence) 계산용.</summary>
        private static float LocalFloor(float[] series, int peak, int dir)
        {
            float floor = series[peak];
            for (int k = peak + dir; k >= 0 && k < series.Length; k += dir)
            {
                if (series[k] > floor + 1e-6f) break;
                floor = Mathf.Min(floor, series[k]);
            }
            return floor;
        }

        // ====================================================================
        // 프리팹 실측 — pt 진단 전용(부등식 판정에는 쓰이지 않는다)
        // ====================================================================

        /// <summary>머리 중심이 엉덩이 피벗 위로 떨어져 있는 거리(월드 유닛, 프리팹이 구워진 배율).
        /// 상체 기울임의 회전 중심은 다리 부착점이다(<c>StickmanPoseAnimator</c>의 <c>_hipPivotLocal</c>).</summary>
        private static void LoadHeadLever(out float leverBaked, out float bakedScale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);
            Assert.IsNotNull(prefab, $"{LogPrefix} 프리팹을 찾지 못했습니다: {PrefabAssetPath}");

            Transform head = prefab.transform.Find("Head");
            Transform leg = prefab.transform.Find("LeftLeg");
            Assert.IsNotNull(head, $"{LogPrefix} 프리팹에 Head 가 없습니다.");
            Assert.IsNotNull(leg, $"{LogPrefix} 프리팹에 LeftLeg 이 없습니다.");

            var hipJoint = leg.GetComponent<HingeJoint2D>();
            float hipY = hipJoint != null ? hipJoint.connectedAnchor.y : leg.localPosition.y;

            float height = 0f;
            foreach (CapsuleCollider2D c in prefab.GetComponents<CapsuleCollider2D>())
                if (c != null && !c.isTrigger) height = Mathf.Max(height, c.size.y);
            Assert.Greater(height, 0f, $"{LogPrefix} 프리팹에서 전신 높이를 못 읽었습니다.");

            bakedScale = height / StickConfig.BaselineCharacterTotalHeight;
            leverBaked = head.localPosition.y - hipY;
            Assert.Greater(leverBaked, 0f,
                $"{LogPrefix} 머리 지렛대(머리 y − 엉덩이 y)가 {leverBaked:F4}입니다 — 0 이하면 pt 진단이 " +
                "무의미합니다.");
        }

        /// <summary>기울임 <paramref name="degrees"/>에서 머리가 <b>직립 대비</b> 옆으로 간 거리(pt),
        /// 배율 <paramref name="scale"/> 기준. 진단 전용이다.</summary>
        private static float HeadShiftPoints(float degrees, float leverBaked, float bakedScale, float scale)
            => Mathf.Sin(degrees * Mathf.Deg2Rad) * leverBaked * (scale / bakedScale)
             * StickConfig.ReferencePointsPerWorldUnitApprox;

        // ====================================================================
        // ★ 본 단언
        // ====================================================================

        /// <summary>
        /// ★★ G2 는 감쇠를 통과한 뒤에도 <b>정점이 정확히 두 개</b>이고, <b>두 번째 박</b>이
        /// L1 상시 흔들림의 <b>왕복폭보다 크다</b>(= 배경 흔들림에 묻히지 않는다).
        ///
        /// <para>이 두 조건이 «2박으로 읽힌다»의 필요조건 전부다. 정점이 하나로 뭉치면 그냥 «한 번
        /// 숙였다»이고, 두 번째가 L1보다 작으면 <b>11초 주기로 늘 일어나는 흔들림과 구분되지 않는다</b>
        /// — 둘 중 하나만 깨져도 사용자가 보는 것은 «2박»이 아니다.</para>
        ///
        /// <para>제스처 직전의 L1 위상은 알 수 없으므로 시작 기울임을 <b>세 지점</b>(가장 앞 / 기준 /
        /// 가장 뒤)에서 전부 돌린다. 프레임 간격도 60Hz·30Hz 두 가지를 돌려 박자 판정이 프레임 예산에
        /// 기대지 않음을 확인한다. 자세는 팔짱(P1)과 뒷짐(P2) 둘 다 본다 — 기준 기울임이 다르다.</para>
        /// </summary>
        [Test]
        public void 끄덕임은_감쇠를_통과한_뒤에도_정점이_정확히_둘이고_2박이_L1_흔들림보다_크다()
        {
            float rate = LeanSmoothingRate();
            float sway = PrivateConst("FocusStanceLeanSwayDegrees");
            float dip = PrivateConst("FocusNodDipDegrees");
            LoadHeadLever(out float leverBaked, out float bakedScale);

            Assert.Greater(sway, 0f,
                $"{LogPrefix} L1 왕복폭이 {sway}°입니다 — 0이면 «L1보다 크다»가 무엇을 넣어도 통과합니다.");
            Assert.Greater(dip, 0f, $"{LogPrefix} 끄덕임 깊이가 {dip}° 입니다(+ = 앞으로 숙임).");

            var stances = new (string Name, float BaseLean)[]
            {
                ("팔짱(P1)", PrivateConst("FocusStanceCrossLeanDegrees")),
                ("뒷짐(P2)", PrivateConst("FocusStanceBackLeanDegrees")),
            };

            foreach ((string stanceName, float baseLean) in stances)
            {
                // L1 상시 왕복폭 — 이 자세에서 «가만히 서 있을 때» 머리가 오가는 폭.
                // ★ 이 하나가 이 테스트의 <b>유일한 자</b>다: 진폭 합격선이자 «두 박을 가르는 골»의
                //   가시성 문턱이다. 둘을 같은 자로 재는 것이 이 검사의 핵심이다.
                float l1Unit = Mathf.Abs(Mathf.Sin((baseLean + sway) * Mathf.Deg2Rad)
                                       - Mathf.Sin((baseLean - sway) * Mathf.Deg2Rad));
                float l1Points = Mathf.Abs(HeadShiftPoints(baseLean + sway, leverBaked, bakedScale, 0.75f)
                                         - HeadShiftPoints(baseLean - sway, leverBaked, bakedScale, 0.75f));
                Assert.Greater(l1Unit, 0f,
                    $"{LogPrefix} L1 왕복폭이 0입니다 — 문턱이 0이면 «정점 2개»가 아무 잔물결에나 " +
                    "통과하고 «L1보다 크다»도 무엇을 넣어도 통과합니다.");

                foreach (float dt in new[] { 1f / 60f, 1f / 30f })
                foreach (float startLean in new[] { baseLean + sway, baseLean, baseLean - sway })
                {
                    float[] series = Simulate(WanderAmbientMotion.FocusNod, FocusAmbientGestures.NodSeconds,
                        baseLean, startLean, rate, dt);
                    List<int> peaks = FindForwardPeaks(ToHeadShiftUnits(series), l1Unit);

                    string where = $"{stanceName} · {1f / dt:F0}Hz · 시작 {startLean:F1}°";

                    Assert.AreEqual(2, peaks.Count,
                        $"{LogPrefix} ★★ {where}: 감쇠를 통과한 뒤 정점이 {peaks.Count}개입니다(기대 2).\n" +
                        (peaks.Count < 2
                            ? "두 박이 하나로 뭉쳤습니다 — 화면에서는 «한 번 숙였다 폈다»로 보이고, 그건 " +
                              "2026-09-07 개정 이전(1박)으로 조용히 되돌아간 것입니다.\n" +
                              "★ 볼 곳: <b>되올림 창</b> = FocusNodFirstHoldEnd01 ~ FocusNodRecover01 " +
                              "(출하 0.27~0.48 = 0.252초 = 3.0τ). 이 창이 좁아지면 두 박 사이 골이 " +
                              $"L1 상시 흔들림({l1Points:F2}pt)보다 얕아지고, 그러면 신호에 봉우리가 둘 " +
                              "있어도 화면에서는 한 번입니다.\n" +
                              "★ 실측 감도(2026-09-07): 출하 골 돌출은 문턱의 1.39배이고, " +
                              "FocusNodFirstHoldEnd01 을 0.27 → 0.46 으로 늘리면 문턱 아래로 내려간다. " +
                              "FocusNodRecover01 을 0.48 → 0.275 로 <b>좁히는 것만으로는</b> 안 뭉친다 " +
                              "(SmoothStep 의 시작 기울기가 0이라 2박 램프가 천천히 출발한다)."
                            : "잔물결이 박으로 세어졌거나 박이 늘었습니다 — 끄덕임은 «두 번»이 설계입니다.") +
                        $"\n기울임 시계열(도): {Preview(series)}");

                    float peak2 = series[peaks[1]];
                    float peak1 = series[peaks[0]];

                    // ★ 본 부등식 — 지렛대와 pt 환산이 양변에서 약분되므로 sin 만으로 정확하다.
                    //   («2박 머리 이동 > L1 머리 왕복폭»과 수학적으로 동일하다.)
                    float beat2Unit = Mathf.Sin(peak2 * Mathf.Deg2Rad) - Mathf.Sin(baseLean * Mathf.Deg2Rad);
                    float beat2Points = Mathf.Abs(HeadShiftPoints(peak2, leverBaked, bakedScale, 0.75f)
                                                - HeadShiftPoints(baseLean, leverBaked, bakedScale, 0.75f));

                    Assert.Greater(beat2Unit, l1Unit,
                        $"{LogPrefix} ★★ {where}: 2박 진폭이 머리 이동 {beat2Points:F2}pt 로 " +
                        $"L1 상시 흔들림 왕복폭 {l1Points:F2}pt 이하입니다.\n" +
                        "두 번째 끄덕임이 «11초 주기로 늘 일어나는 흔들림»과 크기가 같으면 사용자는 그것을 " +
                        "박으로 세지 않습니다 — 화면에서는 «한 번 끄덕이고 흔들렸다»가 됩니다. " +
                        "2박의 램프+유지 구간(FocusNodSecondDip01~FocusNodSecondHoldEnd01)이 감쇠 시상수 " +
                        $"τ={1f / rate:F3}초에 비해 짧아지지 않았는지 확인하십시오.\n" +
                        $"기울임 시계열(도): {Preview(series)}");

                    // ★ 두 박이 «한 쌍»으로 읽히는가 — 어느 한쪽이 흔적만 남으면 «끄덕임 + 잔떨림»이
                    //   되지 «2박»이 아니다. 비율 자체는 design-motion 소관이라 못박지 않고, 한쪽이
                    //   사라지는 것만 막는 느슨한 래칫이다(현재 실측 100.4% — 아래 로그 참고).
                    float ratio = (peak2 - baseLean) / (peak1 - baseLean);
                    Assert.That(ratio, Is.InRange(0.70f, 1.30f),
                        $"{LogPrefix} {where}: 2박이 1박의 {ratio * 100f:F0}%입니다 — 한쪽이 흔적만 남으면 " +
                        "화면에서는 «한 번 끄덕이고 잔떨림»으로 읽히고 그건 2박이 아닙니다. " +
                        "(비율의 «정답»은 design-motion 소관입니다. 이 단언은 한쪽이 사라지는 것만 막습니다.)");

                    if (Mathf.Approximately(dt, 1f / 60f) && Mathf.Approximately(startLean, baseLean))
                    {
                        float period = (peaks[1] - peaks[0]) * dt;
                        float beat1Points = Mathf.Abs(HeadShiftPoints(peak1, leverBaked, bakedScale, 0.75f)
                                                    - HeadShiftPoints(baseLean, leverBaked, bakedScale, 0.75f));
                        Debug.Log($"{LogPrefix} {where} — 1박 {peak1:F2}°({beat1Points:F2}pt) / " +
                            $"2박 {peak2:F2}°({beat2Points:F2}pt) = 1박의 {ratio * 100f:F1}%, " +
                            $"정점 간격 {period:F3}초 = {1f / period:F2}Hz. " +
                            $"L1 왕복폭 {l1Points:F2}pt 대비 2박 {beat2Points / l1Points:F2}배(배율 0.75). " +
                            "★ StickmanPoseAnimator.FocusNodDipDegrees 문서는 «2박 2.88pt = 81%»라고 " +
                            "적고 있으나 출하 상수로 실제 감쇠를 통과시키면 위 값이 나옵니다 — " +
                            "되올림이 baseLean 까지 못 내려오고(골 −0.36°) 2박 구간이 오히려 " +
                            "더 길어서(0.336s vs 0.324s) 2박이 목표에 «더» 닿습니다.");

                        Assert.Greater(period, 0.30f,
                            $"{LogPrefix} 두 정점 간격이 {period:F3}초입니다 — 너무 붙으면 사람 눈에 " +
                            "«두 번»이 아니라 «한 번 떨었다»로 뭉칩니다.");
                        Assert.Less(period, 0.90f,
                            $"{LogPrefix} 두 정점 간격이 {period:F3}초입니다 — 너무 멀면 «한 박자» 안의 " +
                            "두 번이 아니라 별개의 두 제스처로 읽힙니다.");
                    }
                }

                // 계산기 교정 — design-motion 이 <b>독립적으로</b> 낸 «배율 0.75에서 약 2.06pt»와 맞는가.
                // 어긋나면 위에서 찍은 pt 숫자를 폐기해야 한다(부등식 판정은 무차원이라 그대로 유효하다).
                Assert.That(l1Points, Is.InRange(1.6f, 2.6f),
                    $"{LogPrefix} {stanceName}: L1 왕복폭 계산이 {l1Points:F2}pt 로 design-motion 실측" +
                    "(≈2.06pt, 배율 0.75)에서 크게 벗어났습니다 — 프리팹 치수를 잘못 읽고 있다는 뜻이고, " +
                    "이 테스트가 로그에 찍는 pt 숫자는 전부 폐기해야 합니다. " +
                    "(정점 개수와 «2박 > L1» 부등식은 무차원이라 이 교정과 무관하게 유효합니다.)");
            }
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — <b>같은 정점 세기</b>에 «한 박짜리» 곡선을 넣으면 정확히 1개가 나온다.
        ///
        /// <para>이것이 없으면 위의 «2개»는 «세기가 무엇을 넣어도 2를 낸다»와 구분되지 않는다.
        /// 대조군은 <b>프로덕션의 다른 어휘</b>(G3 같은 쪽 돌아보기 = 내밀고 · 유지하고 · 돌아옴)를
        /// 쓴다 — 부등호를 뒤집은 «다른 식»을 여기 적으면 그건 대조가 아니다.</para>
        /// </summary>
        [Test]
        public void 네거티브_컨트롤_한_박짜리_곡선은_같은_세기에서_정점이_하나다()
        {
            float rate = LeanSmoothingRate();
            float sway = PrivateConst("FocusStanceLeanSwayDegrees");
            float baseLean = PrivateConst("FocusStanceCrossLeanDegrees");
            float glanceLean = PrivateConst("FocusGlanceLeanDegrees");
            float l1Unit = Mathf.Abs(Mathf.Sin((baseLean + sway) * Mathf.Deg2Rad)
                                   - Mathf.Sin((baseLean - sway) * Mathf.Deg2Rad));

            Assert.Greater(glanceLean, baseLean,
                $"{LogPrefix} G3(같은 쪽)의 기울임 {glanceLean}°가 기준 {baseLean}°보다 앞이 아닙니다 — " +
                "대조군이 «앞으로 한 번 나오는 곡선»이 아니게 되어 이 대조가 성립하지 않습니다.");

            float[] glance = Simulate(WanderAmbientMotion.FocusScreenGlance,
                FocusAmbientGestures.ScreenGlanceSeconds, baseLean, baseLean, rate, 1f / 60f,
                glanceTurnsAround: false);
            List<int> glancePeaks = FindForwardPeaks(ToHeadShiftUnits(glance), l1Unit);

            Assert.AreEqual(1, glancePeaks.Count,
                $"{LogPrefix} 한 박짜리 곡선(G3 같은 쪽)에서 정점이 {glancePeaks.Count}개 나왔습니다(기대 1) — " +
                "정점 세기가 박을 세는 것이 아니라 다른 무언가를 세고 있습니다. 그러면 위 «G2는 2개»는 " +
                $"아무것도 증명하지 못합니다.\n기울임 시계열(도): {Preview(glance)}");

            // 양성 대조 — 같은 계산기가 G2 에서는 2개를 낸다(두 결과가 한 테스트 안에서 갈린다).
            float[] nod = Simulate(WanderAmbientMotion.FocusNod, FocusAmbientGestures.NodSeconds,
                baseLean, baseLean, rate, 1f / 60f);
            Assert.AreEqual(2, FindForwardPeaks(ToHeadShiftUnits(nod), l1Unit).Count,
                $"{LogPrefix} 같은 계산기가 G2 에서 2개를 내지 못했습니다 — 대조의 다른 쪽이 무너졌습니다.");

            // ★★ 2026-09-07 자기 반려로 추가된 두 번째 대조 — 「봉우리는 둘인데 골이 안 보이는」 곡선.
            //    이게 이 파일 최초판의 실제 구멍이었다(돌연변이 N1: FocusNodRecover01 0.48 → 0.30 이
            //    그대로 초록이었다 — 신호에는 봉우리가 둘 남지만 두 박 사이 골이 배경 흔들림보다
            //    얕아 화면에서는 한 번이다). 골 문턱을 «깊이의 5%»에서 «L1 상시 흔들림»으로 올려
            //    막았고, 여기서 그 막음이 <b>실제로 작동함</b>을 합성 신호로 증명한다.
            //    (합성인 이유: 프로덕션에는 지금 그런 곡선이 없다 — 없는 것을 대조하려면 만들어야 한다.)
            //
            //    ★ 만드는 법이 중요하다: <b>두 봉우리 «사이»만</b> 들어 올린다. 전체를 클램프하면
            //      바깥 바닥까지 함께 올라가 «봉우리가 아예 0개»가 나오고, 그건 이 대조가 재려던 것이
            //      아니다(최초 구현이 실제로 그렇게 틀렸다).
            float[] nodUnits = ToHeadShiftUnits(nod);
            List<int> rawPeaks = FindForwardPeaks(nodUnits, 1e-4f);
            Assert.AreEqual(2, rawPeaks.Count,
                $"{LogPrefix} 합성 대조군의 재료(실제 G2)에서 봉우리를 2개 못 찾았습니다 — 대조군을 " +
                "만들 수 없습니다.");

            var shallow = (float[])nodUnits.Clone();
            float innerFloor = Mathf.Min(nodUnits[rawPeaks[0]], nodUnits[rawPeaks[1]]) - 0.4f * l1Unit;
            for (int i = rawPeaks[0]; i <= rawPeaks[1]; i++) shallow[i] = Mathf.Max(shallow[i], innerFloor);

            //    ★ 기대는 «1»이 아니라 «2가 아님»이다. 이 세기는 돌출이 모자란 봉우리를 <b>버리기만</b>
            //      하고 뭉친 덩어리를 하나로 다시 세지 않으므로, 뭉친 신호는 0 또는 1을 낸다.
            //      둘 다 «두 박으로 안 보인다»는 같은 뜻이고, 위 본 단언(정확히 2)이 그래서 빨개진다.
            int fakeCount = FindForwardPeaks(shallow, l1Unit).Count;
            Assert.Less(fakeCount, 2,
                $"{LogPrefix} ★★ 골이 L1 상시 흔들림의 40%밖에 안 되는 «가짜 2박»이 {fakeCount}개로 " +
                "세어졌습니다(«2 미만»이어야 합니다) — 그러면 이 검사는 «되올림을 좁혀 두 박을 뭉개는» " +
                "조용한 되돌림(이 파일이 막아야 하는 바로 그 고장)을 못 잡습니다. 돌출 문턱이 다시 " +
                "«깊이의 몇 %»로 돌아가지 않았는지 확인하십시오.");

            Debug.Log($"{LogPrefix} 네거티브 컨트롤 통과 — 같은 세기가 G3(한 박) {glancePeaks.Count}개 / " +
                $"G2(두 박) 2개 / 골이 얕은 가짜 2박 {fakeCount}개(2 미만)를 구분했습니다" +
                $"(골 가시성 문턱 = L1 왕복폭 {l1Unit:F4} 머리이동단위).");
        }

        private static string Preview(float[] series)
        {
            var sb = new System.Text.StringBuilder();
            int stride = Mathf.Max(1, series.Length / 30);
            for (int i = 0; i < series.Length; i += stride) sb.Append($"{series[i]:F2} ");
            return sb.ToString().TrimEnd();
        }
    }
}
