using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StickMate.Core;
using StickMate.States;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 회귀 잠금 — <b>방향 부호가 뒤집힐 때 상체와 팔다리가 갈라지는 결함</b> (2026-09-02).
    ///
    /// ============================================================================
    /// 사용자 신고와 실제 원인
    /// ============================================================================
    /// 신고: <i>"활쏘기한 바로 직후 뒤돌았을때 다시 활쏘기 누르면 간헐적으로 몸에서 팔이 분리됨"</i>
    ///
    /// <b>팔은 어깨에서 떨어질 수 없다</b> — 팔 위 마디의 부착점은 매 프레임
    /// <c>StickmanPoseAnimator.ApplyAngle</c>이 어깨 피벗으로 다시 써 준다. 떨어져 보인 것은
    /// <b>몸통</b>이다. <c>_facingSign</c>을 소비하는 <b>적용 지점이 둘</b>인데
    /// (<c>ApplyAngle</c> = 팔다리 / <c>ApplyBodyPlacement</c> = 몸통·머리)
    /// 옛 <c>SetFacing()</c>은 <b>필드 대입 한 줄이 전부</b>였다. 즉 새 부호가 화면에 반영되는 시점이
    /// "누가 먼저 자기 적용 지점을 다시 부르는가"에 달려 있었고, 둘 사이가 벌어진 프레임이 그대로
    /// 렌더로 나갔다.
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 <b>단 하나의 불변식</b>
    /// ============================================================================
    /// <b>상체가 기운 상태에서 방향 부호를 뒤집은 뒤, <c>어깨 부착점 x</c>와 <c>몸통 상단 x</c>는
    /// 반드시 같은 쪽에 있다.</b>
    ///
    /// 두 지표를 고른 이유는 <b>둘이 서로 다른 코드 경로에서 나오기 때문</b>이다 —
    /// 어깨는 <c>ApplyAngle</c>이, 몸통 상단은 <c>ApplyBodyPlacement</c>가 쓴다. 한 경로에서만
    /// 재면 두 경로가 갈라진 사실 자체를 볼 수 없다(그게 이 버그가 오래 살아남은 이유다).
    ///
    /// ============================================================================
    /// ★ 기대값은 <b>프로덕션 함수로 만들지 않는다</b> (docs/TEAM.md 「생성기와 검사기가 같이 틀린다」)
    /// ============================================================================
    /// 예측은 <b>프리팹 YAML 실측</b>에서만 나온다.
    /// <list type="bullet">
    ///   <item>어깨 <c>HingeJoint2D.connectedAnchor.y = 1.3235208</c></item>
    ///   <item>엉덩이 <c>HingeJoint2D.connectedAnchor.y = 0.7010208</c></item>
    ///   <item>→ <c>d = 0.6225</c> (어깨−엉덩이)</item>
    /// </list>
    /// 어깨 이탈 = <c>2·d·sin(lean)</c> = <c>0.7298 · H · sin(lean)</c>.
    /// 이 식은 <b>독립 실측 6건</b>으로 교정된다(아래 <see cref="교정_프리팹_실측이_알려진_값들을_되살린다"/>):
    /// <c>debugger</c> 실기 관측 <c>lean 8.22° → 0.1044 H</c>와 노출 표 4행(24°/26°/14°/10°).
    /// <b>교정이 깨지면 그 뒤 숫자는 전부 폐기한다.</b>
    ///
    /// ============================================================================
    /// ★ 양성 대조 — 이 프로브가 <b>정말로 무엇인가를 잰다</b>는 증명
    /// ============================================================================
    /// 이 저장소가 반복해 당한 형태는 <b>"프로브가 죽어서 초록"</b>이다. 그래서 같은 파일 안에
    /// <b>옛 문</b>(리플렉션으로 <c>_facingSign</c>만 대입 — 수정 전 <c>SetFacing</c>의 본문 그 자체)을
    /// 두고, <b>완전히 같은 시나리오</b>를 두 문으로 각각 통과시킨다.
    /// 옛 문에서 <b>반드시 갈라져야</b> 하고, 갈라진 양이 <c>2·d·sin(lean)</c>과 일치해야 한다.
    /// (<see cref="양성대조_옛_문이었다면_같은_시나리오에서_실제로_갈라진다"/>)
    ///
    /// <para>★ 이 장치가 필요한 이유의 실물 사례가 이 저장소에 있다:
    /// <c>Tests/EditMode/BodyLeanHipPivotTests.cs</c>의 <c>기울임_방향은_바라보는_방향을_따른다</c>는
    /// 이 결함 위를 걷고 있었는데 <b>우연히 초록</b>이었다 — 뒤따르는 <c>ApplyIdlePose</c>가 부르는
    /// <c>SetBodyOffset</c>이 몸통을 다시 배치해 구제해 줬기 때문이다. 그래서 이 파일의 시나리오는
    /// <b>뒤집은 뒤에 포즈 경로가 오지 않는 순서</b>로 짠다.</para>
    ///
    /// ============================================================================
    /// ★ 내장 네거티브 대조 — <c>Idle</c>은 안전하다
    /// ============================================================================
    /// <c>StickmanBlackboard.TickPoseRouting</c>에서 <c>Idle</c>은 방향 덮어쓰기 <b>뒤에</b>
    /// <c>ApplyIdlePose</c>가 돈다 — 팔다리와 몸통이 <b>함께</b> 새 부호로 다시 써진다.
    /// 그래서 <b>옛 문으로도 갈라지지 않는다</b>. 전부 초록이면 재는 척만 하는 것이고,
    /// <c>Idle</c>만 다르게 나오면 진짜로 <b>순서</b>를 재고 있는 것이다.
    ///
    /// <para>물리도 씬도 없이 성립하는 이유는 <c>Tests/EditMode/BodyLeanHipPivotTests.cs</c>와 같다 —
    /// <c>StickmanPoseAnimator</c>는 순수 C# 클래스이고 입력이 Transform 실측뿐이다.</para>
    ///
    /// <para><b>플랫폼</b>: 이 결함이 있는 파일들에는 <c>#if UNITY_STANDALONE_*</c> 분기가 0건이라
    /// macOS/Windows에 <b>같은 크기로</b> 난다. 이 검사도 플랫폼 분기가 없다.</para>
    /// </summary>
    public sealed class FacingFlipBodySplitTests
    {
        private const string LogPrefix = "[방향뒤집기]";

        private const string PrefabRelativePath = "_Project/Prefabs/Stickman.prefab";
        private const string ScriptsRelativePath = "_Project/Scripts";

        private const float Dt = 1f / 60f;
        private const float PoseSmoothingRate = 35f;

        /// <summary>실기 관측(2026-09-02, <c>debugger</c>) — 활쏘기 직전 잔류 기울임과 그때의 어깨 이탈.
        /// <b>프로덕션 상수가 아니라 바깥의 관측값</b>이라 숫자로 적는다(교정 기준점).
        /// 2차 독립 측정은 8.82°였고 둘의 차이가 약 7%다.</summary>
        private const float ObservedArcheryLeanDegrees = 8.22f;
        private const float ObservedArcheryLeanDegrees2nd = 8.82f;
        private const float ObservedShoulderDepartureInHeights = 0.1044f;

        /// <summary>노출 표(<c>debugger</c> 산출)의 예측값 — 교정 대상. 역시 바깥의 핀이다.</summary>
        private const float TableClimb24 = 0.297f;
        private const float TableHang26 = 0.320f;
        private const float TableHit14 = 0.177f;
        private const float TableWalk10 = 0.127f;

        /// <summary>계수 핀 — <c>2·d / H</c>. 이것이 어긋나면 위 다섯 개 핀이 전부 무효다.</summary>
        private const float DepartureCoefficientPin = 0.7298f;

        /// <summary>뒤집히는 프레임에 <b>기울임 곡선이 한 칸 움직인다</b>는 사실을 만드는 배수.
        /// 실기에서는 등반 진행도·낙하 전조·보행 속도가 매 프레임 목표를 옮기므로 <c>TickBodyLean</c>이
        /// 반드시 값을 바꾸고, 그것이 <c>SetBodyLean → ApplyBodyPlacement</c>를 부른다.
        /// <para>★ 이 한 칸이 없으면 <c>SetBodyLean</c>이 <b>조기 return</b>해
        /// <c>ApplyBodyPlacement</c>가 아예 돌지 않고, 그러면 몸통도 옛 부호로 남아 <b>갈라지지
        /// 않는다</b> — 즉 시나리오가 결함을 재현하지 못한 채 초록이 된다. 그래서 각 시나리오는
        /// "기울임이 실제로 움직였는가"를 <b>단언</b>한다(프로브 생존 확인).</para></summary>
        private const float LeanCurveStepRatio = 0.99f;

        private const float PositionTolerance = 1e-4f;
        private const float HeightsTolerance = 0.002f;

        private GameObject _root;
        private Transform _torso;
        private Transform _head;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            _root = null;
            _torso = null;
            _head = null;
        }

        // ============================================================================
        // ① 교정 — 알려진 값으로 먼저 맞춘다. 깨지면 아래 숫자는 전부 폐기한다.
        // ============================================================================

        [Test]
        public void 교정_프리팹_실측이_알려진_값들을_되살린다()
        {
            RigGeometry g = ReadPrefabGeometry();

            Debug.Log($"{LogPrefix} 프리팹 실측 — 엉덩이 {g.HipY:F7} / 어깨 {g.ShoulderY:F7} / " +
                $"d={g.ShoulderAboveHip:F7} / 몸통중심 {g.TorsoCenterY:F7}(반길이 {g.TorsoHalfLength:F7}) / " +
                $"머리 {g.HeadY:F7}(반지름 {g.HeadRadius:F7}) / 신장 H={g.TotalHeight:F7}");

            // (a) d — 이 파일 전체가 이 한 값 위에 서 있다.
            Assert.AreEqual(0.6225f, g.ShoulderAboveHip, 1e-6f,
                $"어깨−엉덩이 거리가 {g.ShoulderAboveHip:F7}입니다(기대 0.6225). 프리팹 관절 배선이 " +
                "바뀌었다면 이 파일의 모든 예측값을 다시 유도해야 합니다.");

            // (b) 몸통 선의 아랫끝이 정확히 엉덩이다 — '몸통 상단'이 유효한 프로브라는 근거.
            Assert.AreEqual(g.HipY, g.TorsoCenterY - g.TorsoHalfLength, 1e-6f,
                "몸통 선의 아랫끝이 엉덩이 피벗과 다릅니다 — 그러면 '몸통 상단'은 엉덩이 피벗 회전의 " +
                "결과가 아니게 되어 이 파일의 프로브가 성립하지 않습니다.");

            // (c) 신장 — 프리팹은 기준 신장의 0.75배로 구워져 있다(독립 출처 교차 확인).
            Assert.AreEqual(StickConfig.BaselineCharacterTotalHeight * 0.75f, g.TotalHeight, 1e-5f,
                $"프리팹 신장 {g.TotalHeight:F7}이 기준 신장×0.75와 다릅니다 — 배율이 다시 구워졌다면 " +
                "H로 정규화한 아래 핀들을 함께 갱신해야 합니다.");

            // (d) 계수 2d/H
            float coefficient = 2f * g.ShoulderAboveHip / g.TotalHeight;
            Debug.Log($"{LogPrefix} 어깨 이탈 계수 2d/H = {coefficient:F6} (핀 {DepartureCoefficientPin})");
            Assert.AreEqual(DepartureCoefficientPin, coefficient, 0.0005f,
                "어깨 이탈 계수가 핀과 다릅니다 — 예측식 '0.7298·H·sin(lean)'이 더 이상 유효하지 않습니다.");

            // (e) ★ 실기 관측 되살리기 — 이것이 진짜 교정이다(바깥에서 잰 값이다).
            float predicted = DepartureInHeights(g, ObservedArcheryLeanDegrees);
            Debug.Log($"{LogPrefix} 예측({ObservedArcheryLeanDegrees}°) = {predicted:F5} H / " +
                $"실기 관측 = {ObservedShoulderDepartureInHeights:F5} H");
            Assert.AreEqual(ObservedShoulderDepartureInHeights, predicted,
                ObservedShoulderDepartureInHeights * 0.01f,
                $"예측식이 실기 관측을 1% 안에서 되살리지 못했습니다(예측 {predicted:F5} vs 관측 " +
                $"{ObservedShoulderDepartureInHeights:F5}). 교정 실패 — 이 파일의 다른 숫자를 믿지 마십시오.");

            // 2차 독립 측정(8.82°)과의 벌어짐이 보고된 7% 근처인지도 함께 남긴다.
            float predicted2nd = DepartureInHeights(g, ObservedArcheryLeanDegrees2nd);
            float spread = Mathf.Abs(predicted2nd - ObservedShoulderDepartureInHeights)
                / ObservedShoulderDepartureInHeights;
            Debug.Log($"{LogPrefix} 2차 측정({ObservedArcheryLeanDegrees2nd}°) 예측 = {predicted2nd:F5} H " +
                $"→ 1차 관측 대비 {spread * 100f:F1}%");
            Assert.LessOrEqual(spread, 0.08f,
                $"두 독립 측정의 예측 차이가 {spread * 100f:F1}%로 보고된 7%를 크게 넘습니다 — " +
                "둘 중 하나가 잘못 기록됐거나 기하가 바뀌었습니다.");

            // (f) 노출 표 4행
            AssertTableRow(g, 24f, TableClimb24, "ParkourClimb");
            AssertTableRow(g, 26f, TableHang26, "GroundLossHang");
            AssertTableRow(g, 14f, TableHit14, "피격 리액션");
            AssertTableRow(g, 10f, TableWalk10, "Walk");
        }

        private void AssertTableRow(RigGeometry g, float leanDegrees, float pin, string label)
        {
            float predicted = DepartureInHeights(g, leanDegrees);
            Debug.Log($"{LogPrefix} 표 검산 — {label} {leanDegrees:F0}° → 예측 {predicted:F4} H (핀 {pin})");
            Assert.AreEqual(pin, predicted, 0.001f,
                $"{label}({leanDegrees:F0}°) 예측이 표의 핀 {pin}과 다릅니다({predicted:F4}).");
        }

        // ============================================================================
        // ② 노출 표가 <b>완전한가</b> — 기울임 출처 전수
        // ============================================================================

        /// <summary>
        /// 기울임을 요청하는 창구는 셋뿐이다(<c>RequestBodyLean</c> / <c>RequestBodyLeanDegrees</c> /
        /// <c>AddHitLean</c>). 그 <b>호출부 파일 집합</b>을 못박는다 — 새 상태가 기울임을 쓰기 시작하면
        /// 이 검사가 빨개져서 "노출 표에 그 상태를 추가하라"고 말한다.
        ///
        /// <para>왜 표를 손으로 유지하지 않는가: 리더가 준 최초 표에 <c>Idle 앰비언트</c>와
        /// <c>LandingCrouch</c>의 실제 크기가 빠져 있었고, 지금도 사람이 세면 또 빠진다.
        /// <b>목록은 소스가 갖고 있게 한다.</b></para>
        /// </summary>
        [Test]
        public void 기울임_출처는_알려진_네_파일뿐이다()
        {
            var expected = new HashSet<string>
            {
                "StickmanPoseAnimator.cs",   // 유휴 앰비언트 / 등반 / 보행 — 포즈 자신이 요청한다
                "LandingCrouchState.cs",     // 무릎앉아 착지 상체 앞기울기
                "GroundLossHangState.cs",    // 발판 상실 낙하 전조
                "RagdollImpactResolver.cs",  // 피격 임펄스(AddHitLean)
            };

            Dictionary<string, List<int>> found = ScanLeanRequestSites();

            foreach (KeyValuePair<string, List<int>> kv in found)
            {
                Debug.Log($"{LogPrefix} 기울임 출처 — {kv.Key} 줄 {string.Join(",", kv.Value)}");
            }

            // "0건 = 깨끗"을 막는 바닥선. 스캐너가 죽으면 여기서 먼저 걸린다.
            Assert.GreaterOrEqual(found.Count, 1,
                "기울임 요청 호출부를 하나도 찾지 못했습니다 — 스캐너가 죽었습니다(주석 제거나 " +
                "정규식이 깨졌을 수 있습니다). 이 상태의 '완전함' 판정은 무효입니다.");

            var actual = new HashSet<string>(found.Keys);
            var missing = new List<string>(expected);
            missing.RemoveAll(f => actual.Contains(f));
            var extra = new List<string>(actual);
            extra.RemoveAll(f => expected.Contains(f));

            Assert.IsEmpty(extra,
                $"노출 표에 없는 새 기울임 출처가 생겼습니다: {string.Join(", ", extra)}. " +
                "그 상태의 최대 기울임 각도를 재서 이 파일의 시나리오에 한 줄 추가하십시오 — " +
                "어깨 이탈은 sin(lean)에 정비례하므로 각도가 크면 그만큼 크게 갈라집니다.");
            Assert.IsEmpty(missing,
                $"알려진 기울임 출처가 사라졌습니다: {string.Join(", ", missing)}. " +
                "정말 없어진 것이면 이 목록에서도 지우십시오(안 지우면 그만큼 검사가 헐거워집니다).");
        }

        // ============================================================================
        // ③ 핵심 회귀 — 상태별 노출 크기 순으로
        // ============================================================================

        [Test]
        public void 등반_24도에서_방향을_뒤집어도_어깨와_몸통이_같은_쪽에_남는다()
        {
            AssertNoSplitAt(ConfigLean(c => c.parkourClimbTorsoLeanDegrees), "ParkourClimb(등반)");
        }

        [Test]
        public void 발판상실_26도에서_방향을_뒤집어도_어깨와_몸통이_같은_쪽에_남는다()
        {
            AssertNoSplitAt(ConfigLean(c => c.groundLossHangFallTellLeanDegrees), "GroundLossHang(발판 상실)");
        }

        [Test]
        public void 피격_14도에서_방향을_뒤집어도_어깨와_몸통이_같은_쪽에_남는다()
        {
            AssertNoSplitAt(ConfigLean(c => c.bodyLeanHitDegrees), "피격 리액션");
        }

        [Test]
        public void 걷기_10도에서_방향을_뒤집어도_어깨와_몸통이_같은_쪽에_남는다()
        {
            AssertNoSplitAt(ConfigLean(c => c.bodyLeanRunMaxDegrees), "Walk(보행 전방 기울임)");
        }

        [Test]
        public void 유휴_주위살피기_7도에서_방향을_뒤집어도_어깨와_몸통이_같은_쪽에_남는다()
        {
            AssertNoSplitAt(ConfigLean(c => c.bodyLeanLookAroundDegrees), "Idle 앰비언트 주위 살피기");
        }

        /// <summary>사용자가 실제로 신고한 그 자리 — 활쏘기는 <b>자기 기울임을 요청하지 않는다</b>.
        /// 접근 보행이 만든 기울임이 τ=1/12초로 감쇠하는 도중에 방향이 뒤집힌다.
        /// 각도는 <c>debugger</c>의 실기 관측값을 그대로 쓴다(설정값이 아니라 관측값이라 숫자로 적는다).</summary>
        [Test]
        public void 활쏘기_잔류_8_22도에서_방향을_뒤집어도_어깨와_몸통이_같은_쪽에_남는다()
        {
            AssertNoSplitAt(ObservedArcheryLeanDegrees, "Archery(활쏘기 — 접근 보행 잔류 기울임)");
        }

        /// <summary>
        /// ★ <c>ThrowTumble</c> · <c>Dragged</c> · <c>Getup</c> — 리더가 노출 표에 추가하라고 지목한 셋.
        /// 실측 결과 <b>셋 다 자기 기울임을 요청하지 않는다</b>(아래에서 소스로 확인한다).
        /// 그래서 이들의 노출은 <b>직전 상태에서 넘어온 잔류 기울임</b>뿐이고, 그 잔류는
        /// <c>TickBodyLean</c>이 τ=1/<c>bodyLeanSmoothingRate</c>초로 <b>매 프레임 깎는다</b> —
        /// 값이 매 프레임 바뀌므로 <c>SetBodyLean</c>의 조기 return을 통과해
        /// <c>ApplyBodyPlacement</c>가 계속 돈다. 즉 <b>감쇠하는 몇 프레임 동안은 노출된다.</b>
        ///
        /// <para>잔류의 상한은 "직전 상태가 걸어 놓을 수 있었던 최대 기울임"이고, 그 최대치는
        /// <see cref="기울임_출처는_알려진_네_파일뿐이다"/>가 전수 확인하는 <b>네 출처</b>의 최대값이다.
        /// 여기서는 그 최대값으로 같은 불변식을 건다 — 상태 이름을 하나씩 흉내 내는 대신
        /// <b>이 셋이 겪을 수 있는 가장 나쁜 각도</b>를 직접 쓴다.</para>
        ///
        /// <para>★ <c>Getup</c>은 <b>구조적으로 잔류가 0</b>이다: 프로덕션에서 <c>Getup</c>으로 가는
        /// 유일한 경로가 <c>States/RagdollState.cs</c>이고, RAGDOLL 동안 <c>TickPoseRouting</c>이
        /// 매 프레임 <c>ClearBodyLean()</c>을 부른다(<c>SetBodyLean(0)</c> = 즉시 직립).
        /// 그래도 이 검사에 넣는 이유는 <b>진입 경로가 하나 더 생기면 그 전제가 조용히 깨지기 때문</b>이라
        /// 아래에서 그 경로 수를 함께 센다.</para>
        /// </summary>
        [Test]
        public void 던지기회전_붙잡힘_기상은_자기_기울임이_없고_잔류_최대치에서도_갈라지지_않는다()
        {
            // (1) 셋 다 자기 기울임 출처가 아니다 — 소스로 확인한다(추측 금지).
            Dictionary<string, List<int>> sites = ScanLeanRequestSites();
            foreach (string file in new[] { "ThrowTumbleState.cs", "DragThrowState.cs", "GetupState.cs" })
            {
                Assert.IsFalse(sites.ContainsKey(file),
                    $"{file}이 기울임을 스스로 요청하기 시작했습니다 — 그러면 이 검사의 전제" +
                    "('잔류만 있다')가 깨집니다. 그 각도로 전용 시나리오를 추가하십시오.");
            }

            // (2) Getup의 진입 경로가 여전히 RAGDOLL 하나뿐인가 — 그래야 잔류 0이 보장된다.
            int getupEntries = CountProductionMatches(@"ChangeState\(\s*StickmanStateId\.Getup");
            Debug.Log($"{LogPrefix} Getup 진입 경로(프로덕션) = {getupEntries}곳");
            Assert.AreEqual(1, getupEntries,
                $"Getup 진입 경로가 {getupEntries}곳입니다(기대 1 — States/RagdollState.cs). " +
                "RAGDOLL 외의 경로가 생기면 ClearBodyLean이 먼저 돈다는 보장이 사라져 " +
                "Getup도 잔류 기울임을 갖게 됩니다.");

            // (3) 잔류 상한 = 네 출처의 최대값. 그 각도에서 같은 불변식을 건다.
            float worstResidual = Mathf.Max(
                Mathf.Max(ConfigLean(c => c.landingCrouchTorsoPitchBraceDegrees),
                          ConfigLean(c => c.groundLossHangFallTellLeanDegrees)),
                Mathf.Max(ConfigLean(c => c.parkourClimbTorsoLeanDegrees),
                          ConfigLean(c => c.bodyLeanHitDegrees)));

            // 적용 상한(MaxBodyLeanDegrees)에 잘리므로 실제로 걸 수 있는 각도로 맞춘다.
            StickmanPoseAnimator probe = BuildRig(ReadPrefabGeometry());
            probe.SetBodyLean(worstResidual);
            float applicable = probe.BodyLeanDegrees;
            TearDown();

            Debug.Log($"{LogPrefix} 잔류 상한 요청 {worstResidual:F1}° → 실제 적용 {applicable:F1}°");
            Assert.Greater(applicable, 0f, "잔류 상한이 0입니다 — 이 검사가 공허합니다.");

            AssertNoSplitAt(applicable, "ThrowTumble/Dragged (직전 상태 잔류 최대치)");
        }

        /// <summary>프로덕션 소스(테스트 제외)에서 정규식에 맞는 줄 수 — 주석은 뺀다.</summary>
        private static int CountProductionMatches(string pattern)
        {
            var rx = new Regex(pattern);
            string root = Path.Combine(Application.dataPath, ScriptsRelativePath);
            int count = 0;
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').Contains("/Tests/")) continue;
                foreach (string raw in File.ReadAllLines(file))
                {
                    string line = raw;
                    string trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*")) continue;
                    int c = line.IndexOf("//", System.StringComparison.Ordinal);
                    if (c >= 0) line = line.Substring(0, c);
                    if (rx.IsMatch(line)) count++;
                }
            }
            return count;
        }

        /// <summary>
        /// ★ <c>LandingCrouch</c>는 <b>실기 재현이 안 됐다</b>(<c>debugger</c>, 2026-09-02).
        /// 구조는 다른 상태와 완전히 같고 — <c>TickPoseRouting</c>이 조기 return하는 목록에 있다 —
        /// 오히려 <b>이 저장소에서 가장 큰 기울임</b>을 쓴다. 그래서 초록으로 닫지 않고 러너에
        /// "건너뜀"으로 계속 보이게 남긴다(이 저장소 관례).
        ///
        /// <para>★ 리더가 준 표의 <i>"LandingCrouch는 스스로 RequestBodyLean을 안 한다"</i>는
        /// <b>반증됐다</b>: <c>States/LandingCrouchState.cs</c>가
        /// <c>RequestBodyLeanDegrees(_torsoPitchDegrees × CurrentCrouchAmount)</c>를 매 프레임 부르고,
        /// <c>_torsoPitchDegrees</c>의 상한은 <c>landingCrouchTorsoPitchBraceDegrees</c>(출하 30°)다.
        /// 즉 잔류 2~4프레임이 아니라 <b>연출 내내</b> 노출되며, 크기도 등반(24°)보다 크다.
        /// 실기 재현이 되는 즉시 이 항목을 <see cref="AssertNoSplitAt"/> 호출로 바꾸면 된다 —
        /// 아래에 예측값을 미리 계산해 로그로 남겨 둔다.</para>
        /// </summary>
        [Test]
        public void 무릎앉아_착지는_실기_미재현이라_보류한다()
        {
            RigGeometry g = ReadPrefabGeometry();
            float brace = ConfigLean(c => c.landingCrouchTorsoPitchBraceDegrees);
            float deep = ConfigLean(c => c.landingCrouchTorsoPitchDegrees);
            float predicted = DepartureInHeights(g, brace);

            Assert.Ignore(
                $"[미확인] LandingCrouch는 debugger가 실기 재현을 못 했습니다(2026-09-02). " +
                $"구조는 노출 조건과 같고(TickPoseRouting 조기 return), 기울임은 " +
                $"landingCrouchTorsoPitchDegrees={deep:F0}° · " +
                $"landingCrouchTorsoPitchBraceDegrees={brace:F0}°로 이 저장소 최대입니다 — " +
                $"예상 어깨 이탈 {predicted:F3} H(등반 {TableClimb24} H보다 큽니다). " +
                $"리더 표의 '스스로 RequestBodyLean을 하지 않는다'는 States/LandingCrouchState.cs 실측으로 " +
                $"반증됐습니다(매 프레임 RequestBodyLeanDegrees를 부릅니다). " +
                $"실기 재현이 되면 이 Ignore를 AssertNoSplitAt(brace, ...) 호출로 바꾸십시오.");
        }

        // ============================================================================
        // ④ ★ 양성 대조 — 옛 문에서는 <b>실제로 갈라진다</b>
        // ============================================================================

        /// <summary>
        /// 같은 리그, 같은 시나리오, 같은 각도. <b>문만 다르다.</b>
        /// <list type="number">
        ///   <item><b>옛 문</b>(리플렉션으로 <c>_facingSign</c>만 대입 = 수정 전 <c>SetFacing</c> 본문) →
        ///         어깨와 몸통 상단의 <b>부호가 갈라지고</b>, 갈라진 양이 <c>2·d·sin(lean)</c>과 일치한다.</item>
        ///   <item><b>새 문</b>(프로덕션 <c>SetFacing</c>) → 같은 지표가 <b>0</b>이다.</item>
        /// </list>
        /// 둘을 한 검사 안에 넣는 이유: 따로 두면 "새 문이 초록"인 것이 <b>고쳐져서</b>인지
        /// <b>입력이 애초에 무해해서</b>인지 구분되지 않는다.
        /// </summary>
        [Test]
        public void 양성대조_옛_문이었다면_같은_시나리오에서_실제로_갈라진다()
        {
            RigGeometry g = ReadPrefabGeometry();
            float lean = ConfigLean(c => c.parkourClimbTorsoLeanDegrees);   // 가장 큰 것부터.

            // --- 옛 문 ---
            StickmanPoseAnimator legacy = BuildRig(g);
            FlipOutcome before = RunFlipFrame(legacy, lean, useProductionDoor: false);
            float legacyGapInHeights = Mathf.Abs(before.ShoulderX - before.CorrectShoulderX) / g.TotalHeight;

            Debug.Log($"{LogPrefix} 옛 문 — 어깨 x={before.ShoulderX:F5} / 몸통상단 x={before.TorsoTopX:F5} / " +
                $"이탈 {legacyGapInHeights:F4} H (예측 {DepartureInHeights(g, lean):F4} H)");

            Assert.Less(before.ShoulderX * before.TorsoTopX, 0f,
                $"옛 문에서 어깨({before.ShoulderX:F5})와 몸통 상단({before.TorsoTopX:F5})의 부호가 " +
                "갈라지지 않았습니다 — 이 시나리오가 결함을 재현하지 못했다는 뜻이고, 그러면 아래 " +
                "'새 문에서 초록'은 아무것도 증명하지 않습니다(프로브 사망).");

            Assert.AreEqual(DepartureInHeights(g, lean), legacyGapInHeights, HeightsTolerance,
                $"갈라진 양이 예측(2·d·sin(lean))과 다릅니다 — 실측 {legacyGapInHeights:F4} H. " +
                "예측식이나 시나리오 중 하나가 틀렸습니다.");

            TearDown();

            // --- 새 문 (완전히 같은 시나리오) ---
            StickmanPoseAnimator fixedPose = BuildRig(g);
            FlipOutcome after = RunFlipFrame(fixedPose, lean, useProductionDoor: true);
            float fixedGapInHeights = Mathf.Abs(after.ShoulderX - after.CorrectShoulderX) / g.TotalHeight;

            Debug.Log($"{LogPrefix} 새 문 — 어깨 x={after.ShoulderX:F5} / 몸통상단 x={after.TorsoTopX:F5} / " +
                $"이탈 {fixedGapInHeights:F6} H");

            Assert.Greater(after.ShoulderX * after.TorsoTopX, 0f,
                $"새 문에서도 어깨({after.ShoulderX:F5})와 몸통 상단({after.TorsoTopX:F5})이 갈라졌습니다.");
            Assert.AreEqual(0f, fixedGapInHeights, HeightsTolerance,
                $"새 문에서 어깨가 여전히 {fixedGapInHeights:F5} H 떠 있습니다 — " +
                "SetFacing이 ReapplyCurrentAngles를 부르지 않았을 수 있습니다.");
        }

        /// <summary>★ 프로브 생존 확인 — 리플렉션 옛 문이 <b>정말로</b> 부호를 바꾸는가.
        /// 이름이 바뀌어 <c>FieldInfo</c>가 null이 되면 양성 대조가 조용히 "아무 일도 안 함"이 되고,
        /// 그러면 위 검사가 <b>거짓 빨강</b>이 아니라 <b>거짓 초록</b>으로 무너진다.</summary>
        [Test]
        public void 프로브_생존_옛_문과_새_문은_부호_결과가_같고_재적용만_다르다()
        {
            RigGeometry g = ReadPrefabGeometry();

            StickmanPoseAnimator a = BuildRig(g);
            Assert.AreEqual(1f, a.FacingSign, 0f, "초기 방향 부호가 +1이 아닙니다.");
            FlipWithoutReapply(a, -1f);
            Assert.AreEqual(-1f, a.FacingSign, 0f,
                "리플렉션 옛 문이 방향 부호를 바꾸지 못했습니다 — 이 파일의 양성 대조 전부가 무효입니다.");
            TearDown();

            StickmanPoseAnimator b = BuildRig(g);
            b.SetFacing(-1f);
            Assert.AreEqual(-1f, b.FacingSign, 0f, "프로덕션 SetFacing이 방향 부호를 바꾸지 못했습니다.");

            Debug.Log($"{LogPrefix} 프로브 생존 확인 — 두 문 모두 FacingSign을 -1로 만든다. " +
                "차이는 '적용 지점을 다시 부르는가' 하나뿐이다.");
        }

        // ============================================================================
        // ⑤ 내장 네거티브 대조 — Idle 순서는 옛 문으로도 안전하다
        // ============================================================================

        /// <summary>
        /// <c>Idle</c>이 안전한 이유는 <b>순서</b>다: <c>TickPoseRouting</c>이 방향을 덮은 <b>뒤에</b>
        /// <c>ApplyIdlePose</c>가 돌아 팔다리와 몸통을 <b>함께</b> 새 부호로 다시 쓴다.
        /// 그래서 <b>옛 문으로도</b> 갈라지지 않는다.
        ///
        /// <para>이 검사가 이 파일의 내장 대조다 — 전부 초록이면 재는 척만 하는 것이고,
        /// 여기만 다르게 나오면 위 검사들이 진짜로 <b>순서</b>를 재고 있다는 뜻이다.</para>
        /// </summary>
        [Test]
        public void 대조군_Idle_순서에서는_옛_문으로도_갈라지지_않는다()
        {
            RigGeometry g = ReadPrefabGeometry();
            float lean = ConfigLean(c => c.parkourClimbTorsoLeanDegrees);   // 위와 같은 각도로 비교한다.
            StickmanPoseAnimator pose = BuildRig(g);

            pose.SetFacing(1f);
            pose.SetBodyLean(lean);
            SettleIdle(pose);

            // --- Idle 프레임 ---
            // (1) TickPoseRouting이 방향을 덮는다 — 옛 문으로.
            FlipWithoutReapply(pose, -1f);
            // (2) Idle 분기는 조기 return하지 않는다. 덮어쓰기 **뒤에** 포즈가 돈다.
            pose.ApplyIdlePose(Dt, StaticPoseSettings(), PoseSmoothingRate);
            // (3) TickPose 꼬리.
            pose.RequestBodyLeanDegrees(lean * LeanCurveStepRatio);
            pose.TickBodyLean(Dt, LeanSmoothingRate());

            float shoulderX = ShoulderAttachX();
            float torsoTopX = TorsoTopLocal().x;

            Debug.Log($"{LogPrefix} 대조군 Idle — 어깨 x={shoulderX:F5} / 몸통상단 x={torsoTopX:F5} " +
                $"(기울임 {pose.BodyLeanDegrees:F3}°)");

            // 검사가 공허하지 않다는 확인 — 기울임이 0이면 두 값 다 0이라 무엇이든 통과한다.
            Assert.Greater(Mathf.Abs(pose.BodyLeanDegrees), 1f,
                "대조군의 기울임이 거의 0입니다 — 그러면 이 검사는 아무것도 재지 않습니다.");
            Assert.Greater(Mathf.Abs(shoulderX), PositionTolerance * 10f,
                "대조군의 어깨 x가 0에 가깝습니다 — 검사가 공허합니다.");

            Assert.Greater(shoulderX * torsoTopX, 0f,
                $"Idle 순서인데도 갈라졌습니다(어깨 {shoulderX:F5} / 몸통 {torsoTopX:F5}) — " +
                "이 대조가 성립하지 않으면 위 검사들이 '순서'가 아니라 '아무 뒤집기나'를 재고 있다는 뜻입니다.");
            Assert.AreEqual(-1f, Mathf.Sign(shoulderX), 0f,
                "왼쪽을 보는데 어깨가 앞(+x)에 있습니다.");
        }

        // ============================================================================
        // ⑥ RAGDOLL 가드 — 물리가 마디를 소유할 때는 손대지 않는다
        // ============================================================================

        /// <summary>
        /// 새 <c>SetFacing</c>은 팔다리 재적용에 <c>PhysicsOwnsLimbs()</c> 가드를 건다. 그 가드는
        /// <b>장식이 아니다</b>: <c>TickPoseRouting</c>은 <c>SetFacing</c>을 <c>Ragdoll</c> 분기보다
        /// <b>먼저</b> 부르고, <c>WanderIntentMayDriveFacing(Ragdoll)</c>이 <c>true</c>라 배회 AI의
        /// 이동 의도가 RAGDOLL 중에도 방향을 바꾼다. 가드가 없으면 뒹굴던 Dynamic 바디의 Transform에
        /// 직접 써서 팔다리가 포즈 자세로 <b>순간이동</b>한다.
        ///
        /// <para>몸통·머리는 <c>Rigidbody2D</c>가 <b>아예 없어서</b>(시각 전용 오브젝트) 언제 불러도
        /// 안전하다 — 그래서 이 검사는 "팔다리는 안 움직이고 몸통은 움직인다"를 동시에 요구한다.</para>
        /// </summary>
        [Test]
        public void 랙돌_중에는_방향을_뒤집어도_팔다리_Transform이_안_튄다()
        {
            RigGeometry g = ReadPrefabGeometry();
            StickmanPoseAnimator pose = BuildRig(g, RigidbodyType2D.Dynamic);

            pose.SetFacing(1f);
            pose.SetBodyLean(ConfigLean(c => c.parkourClimbTorsoLeanDegrees));
            SettleIdle(pose);

            Vector3 armBefore = Find("LeftArm").localPosition;
            Quaternion armRotBefore = Find("LeftArm").localRotation;
            Vector3 legBefore = Find("RightLeg").localPosition;
            float torsoBefore = TorsoTopLocal().x;

            pose.SetFacing(-1f);

            Assert.AreEqual(armBefore.x, Find("LeftArm").localPosition.x, 0f,
                "RAGDOLL 중인데 SetFacing이 팔 부착점을 옮겼습니다 — 뒹굴던 팔이 포즈 자세로 튑니다.");
            Assert.AreEqual(armBefore.y, Find("LeftArm").localPosition.y, 0f,
                "RAGDOLL 중인데 SetFacing이 팔 부착점 높이를 옮겼습니다.");
            Assert.IsTrue(armRotBefore == Find("LeftArm").localRotation,
                "RAGDOLL 중인데 SetFacing이 팔 각도를 덮었습니다.");
            Assert.AreEqual(legBefore.x, Find("RightLeg").localPosition.x, 0f,
                "RAGDOLL 중인데 SetFacing이 다리를 옮겼습니다.");

            // 몸통은 반대로 **반드시** 따라와야 한다(물리 바디가 없으므로 포즈가 유일한 주인이다).
            float torsoAfter = TorsoTopLocal().x;
            Debug.Log($"{LogPrefix} 랙돌 가드 — 팔다리 불변 / 몸통 상단 x {torsoBefore:F5} → {torsoAfter:F5}");
            Assert.AreEqual(-torsoBefore, torsoAfter, PositionTolerance,
                "RAGDOLL 중 몸통이 새 방향으로 미러링되지 않았습니다 — 몸통·머리에는 Rigidbody2D가 " +
                "없으므로 이 경로는 언제나 안전하고, 건너뛰면 랙돌에서 빠져나온 뒤에도 옛 부호가 남습니다.");
        }

        /// <summary>네거티브 컨트롤 — 같은 리그를 <b>Kinematic</b>(= 능동 모드)으로 두면 같은 호출이
        /// 팔다리를 <b>실제로 옮긴다</b>. 이게 없으면 위 검사의 "안 움직였다"가
        /// "원래 아무것도 안 움직이는 리그였다"와 구분되지 않는다.</summary>
        [Test]
        public void 네거티브컨트롤_능동_모드에서는_같은_호출이_팔다리를_실제로_옮긴다()
        {
            RigGeometry g = ReadPrefabGeometry();
            StickmanPoseAnimator pose = BuildRig(g, RigidbodyType2D.Kinematic);

            pose.SetFacing(1f);
            pose.SetBodyLean(ConfigLean(c => c.parkourClimbTorsoLeanDegrees));
            SettleIdle(pose);

            float armBefore = Find("LeftArm").localPosition.x;
            pose.SetFacing(-1f);
            float armAfter = Find("LeftArm").localPosition.x;

            Debug.Log($"{LogPrefix} 네거티브 컨트롤 — Kinematic 어깨 x {armBefore:F5} → {armAfter:F5}");
            Assert.AreEqual(-armBefore, armAfter, PositionTolerance,
                "능동 모드인데 SetFacing이 팔 부착점을 미러링하지 않았습니다 — " +
                "그렇다면 위 RAGDOLL 검사는 아무것도 증명하지 않습니다.");
        }

        // ============================================================================
        // ⑦ 왕복 — 두 번 뒤집으면 정확히 제자리
        // ============================================================================

        [Test]
        public void 두_번_뒤집으면_한_톨도_다르지_않게_돌아온다()
        {
            RigGeometry g = ReadPrefabGeometry();
            StickmanPoseAnimator pose = BuildRig(g);

            pose.SetFacing(1f);
            pose.SetBodyLean(ConfigLean(c => c.groundLossHangFallTellLeanDegrees));
            SettleIdle(pose);

            Vector3 armBefore = Find("LeftArm").localPosition;
            Vector3 legBefore = Find("RightLeg").localPosition;
            Vector3 torsoBefore = _torso.localPosition;
            Quaternion torsoRotBefore = _torso.localRotation;
            Vector3 headBefore = _head.localPosition;

            pose.SetFacing(-1f);
            pose.SetFacing(1f);

            Assert.AreEqual(armBefore.x, Find("LeftArm").localPosition.x, PositionTolerance, "팔 부착점 x");
            Assert.AreEqual(armBefore.y, Find("LeftArm").localPosition.y, PositionTolerance, "팔 부착점 y");
            Assert.AreEqual(legBefore.x, Find("RightLeg").localPosition.x, PositionTolerance, "다리 부착점 x");
            Assert.AreEqual(torsoBefore.x, _torso.localPosition.x, PositionTolerance, "몸통 x");
            Assert.AreEqual(torsoBefore.y, _torso.localPosition.y, PositionTolerance, "몸통 y");
            Assert.AreEqual(headBefore.x, _head.localPosition.x, PositionTolerance, "머리 x");
            Assert.AreEqual(0f, Quaternion.Angle(torsoRotBefore, _torso.localRotation), 1e-3f, "몸통 회전");

            Debug.Log($"{LogPrefix} 왕복 — 두 번 뒤집은 뒤 팔/다리/몸통/머리 전부 원위치.");
        }

        // ============================================================================
        // 시나리오 / 헬퍼
        // ============================================================================

        private struct FlipOutcome
        {
            public float ShoulderX;         // 실측 — ApplyAngle이 쓴 어깨 부착점 x
            public float TorsoTopX;         // 실측 — ApplyBodyPlacement가 쓴 몸통 선 윗끝 x
            public float CorrectShoulderX;  // 예측 — 프리팹 d와 뒤집기 직전 기울임으로 독립 계산
            public float LeanBefore;
            public float LeanAfter;
        }

        /// <summary>
        /// <b>노출 상태의 한 프레임</b>을 프로덕션과 같은 순서로 재현한다.
        /// <list type="number">
        ///   <item><c>_machine.Tick</c> — 상태가 자기 포즈를 쓴다. <b>이때는 아직 옛 방향</b>이다.</item>
        ///   <item><c>TickPoseRouting</c> — 방향을 덮어쓰고, 이 상태는 그 뒤 <b>조기 return</b>한다
        ///         (Walk / GroundLossHang / LandingCrouch / Archery / ParkourClimb / ThrowTumble /
        ///         Dragged / Getup 분기). 즉 <b>포즈 경로가 다시 오지 않는다</b>.</item>
        ///   <item><c>TickPose</c> 꼬리 — <c>TickBodyLean</c>이 진행 곡선의 다음 점을 확정하고,
        ///         그 안의 <c>SetBodyLean</c>이 <c>ApplyBodyPlacement</c>를 부른다. <b>몸통만</b> 새 방향이 된다.</item>
        /// </list>
        /// ★ 3단계에서 기울임이 실제로 <b>변해야</b> <c>SetBodyLean</c>의 조기 return을 통과해
        /// <c>ApplyBodyPlacement</c>가 돈다. 안 변하면 시나리오가 결함을 재현하지 못한 채 초록이 되므로
        /// 여기서 <b>단언</b>한다.
        /// </summary>
        private FlipOutcome RunFlipFrame(StickmanPoseAnimator pose, float leanDegrees, bool useProductionDoor)
        {
            pose.SetFacing(1f);
            pose.SetBodyLean(leanDegrees);
            SettleIdle(pose);

            Assert.AreEqual(leanDegrees, pose.BodyLeanDegrees, 1e-4f,
                $"준비 실패 — 요청한 기울임 {leanDegrees:F2}°가 적용되지 않았습니다" +
                $"(실제 {pose.BodyLeanDegrees:F2}°). 상한 클램프에 걸렸을 수 있습니다.");
            Assert.Greater(ShoulderAttachX(), 0f,
                "준비 실패 — 오른쪽을 보고 앞으로 기울였는데 어깨가 앞(+x)에 있지 않습니다.");

            float leanBefore = pose.BodyLeanDegrees;

            // (1) 상태가 자기 포즈를 쓴다 — 옛 방향.
            pose.ApplyIdlePose(Dt, StaticPoseSettings(), PoseSmoothingRate);
            // (2) 라우팅이 방향을 덮고 조기 return한다.
            if (useProductionDoor) pose.SetFacing(-1f);
            else FlipWithoutReapply(pose, -1f);
            // (3) TickPose 꼬리 — 진행 곡선이 한 칸 움직인다.
            pose.RequestBodyLeanDegrees(leanDegrees * LeanCurveStepRatio);
            pose.TickBodyLean(Dt, LeanSmoothingRate());

            float leanAfter = pose.BodyLeanDegrees;
            Assert.AreNotEqual(leanBefore, leanAfter,
                $"시나리오 무효 — 뒤집히는 프레임에 기울임이 {leanBefore:F5}° 그대로였습니다. " +
                "그러면 SetBodyLean이 조기 return해 ApplyBodyPlacement가 아예 돌지 않고, " +
                "몸통도 옛 부호로 남아 이 시나리오는 결함을 재현하지 못합니다(거짓 초록).");

            RigGeometry g = ReadPrefabGeometry();
            return new FlipOutcome
            {
                ShoulderX = ShoulderAttachX(),
                TorsoTopX = TorsoTopLocal().x,
                // 예측은 프리팹 실측 d와 뒤집기 직전 기울임으로만 만든다(프로덕션 함수를 쓰지 않는다).
                // 팔 부착점은 설계상 "직전 ApplyAngle이 본 기울임"을 쓴다 — 그 계약이
                // StickmanPoseAnimator.SetBodyLean 문서에 명시돼 있다.
                CorrectShoulderX = -1f * g.ShoulderAboveHip * Mathf.Sin(leanBefore * Mathf.Deg2Rad),
                LeanBefore = leanBefore,
                LeanAfter = leanAfter,
            };
        }

        private void AssertNoSplitAt(float leanDegrees, string label)
        {
            RigGeometry g = ReadPrefabGeometry();
            StickmanPoseAnimator pose = BuildRig(g);
            FlipOutcome o = RunFlipFrame(pose, leanDegrees, useProductionDoor: true);

            float exposureIfBroken = DepartureInHeights(g, leanDegrees);
            float actualGap = Mathf.Abs(o.ShoulderX - o.CorrectShoulderX) / g.TotalHeight;

            Debug.Log($"{LogPrefix} {label} {leanDegrees:F2}° — 어깨 x={o.ShoulderX:F5} / " +
                $"몸통상단 x={o.TorsoTopX:F5} / 이탈 {actualGap:F6} H " +
                $"(옛 문이었다면 {exposureIfBroken:F4} H)");

            // 검사가 공허하지 않다는 확인부터.
            Assert.Greater(exposureIfBroken, HeightsTolerance * 5f,
                $"{label}의 노출 예상치가 {exposureIfBroken:F5} H로 허용오차와 구분되지 않습니다 — " +
                "이 각도에서는 이 검사가 사실상 아무것도 재지 않습니다.");
            Assert.Greater(Mathf.Abs(o.ShoulderX), PositionTolerance * 10f,
                $"{label}에서 어깨 x가 0에 가깝습니다 — 검사가 공허합니다.");

            Assert.Greater(o.ShoulderX * o.TorsoTopX, 0f,
                $"{label}({leanDegrees:F2}°)에서 어깨({o.ShoulderX:F5})와 몸통 상단({o.TorsoTopX:F5})이 " +
                $"반대쪽에 있습니다 — 사용자가 본 '몸에서 팔이 분리됨'이 그대로 재현됐습니다. " +
                $"예상 이탈 {exposureIfBroken:F4} H.");

            Assert.AreEqual(-1f, Mathf.Sign(o.ShoulderX), 0f,
                $"{label}에서 왼쪽을 보는데 어깨가 앞(+x)에 남았습니다.");
            Assert.AreEqual(-1f, Mathf.Sign(o.TorsoTopX), 0f,
                $"{label}에서 왼쪽을 보는데 몸통 상단이 앞(+x)에 남았습니다.");

            Assert.AreEqual(0f, actualGap, HeightsTolerance,
                $"{label}에서 어깨가 예측 자리에서 {actualGap:F5} H 벗어났습니다 — " +
                $"SetFacing이 팔다리를 다시 적용하지 않았을 때의 값이 {exposureIfBroken:F4} H입니다.");
        }

        /// <summary>어깨 부착점 x(루트 로컬) — <c>ApplyAngle</c>이 쓰는 값 그 자체다.
        /// 관절 <c>anchor</c>가 (0,0)이라(프리팹 8개 전부) 팔 각도와 무관하게 피벗 x가 그대로 남는다.</summary>
        private float ShoulderAttachX() => Find("LeftArm").localPosition.x;

        /// <summary>몸통 선의 <b>윗끝</b>(루트 로컬) — <c>ApplyBodyPlacement</c>가 쓴 위치+회전의 결과다.
        /// 몸통 오브젝트의 원점은 선의 <b>중점</b>이므로 로컬 <c>(0, +반길이)</c>를 올려야 한다.</summary>
        private Vector3 TorsoTopLocal()
        {
            RigGeometry g = ReadPrefabGeometry();
            return _root.transform.InverseTransformPoint(
                _torso.TransformPoint(new Vector3(0f, g.TorsoHalfLength, 0f)));
        }

        /// <summary>수정 전 <c>SetFacing</c>의 본문 그 자체 — 필드 대입 한 줄.
        /// <b>못 찾으면 실패한다</b>(조용히 아무 일도 안 하면 양성 대조가 통째로 무효가 된다).</summary>
        private static void FlipWithoutReapply(StickmanPoseAnimator pose, float sign)
        {
            FieldInfo field = typeof(StickmanPoseAnimator).GetField(
                "_facingSign", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field,
                "StickmanPoseAnimator._facingSign 필드를 찾지 못했습니다 — 이름이 바뀌었다면 이 " +
                "양성 대조도 함께 갱신해야 합니다. 그 전까지 이 파일의 '옛 문' 검사는 전부 무효입니다.");

            float expected = sign >= 0f ? 1f : -1f;
            field.SetValue(pose, expected);
            Assert.AreEqual(expected, pose.FacingSign, 0f,
                "리플렉션 옛 문이 방향 부호를 바꾸지 못했습니다 — 프로브가 죽었습니다.");
        }

        private static float LeanSmoothingRate() => ConfigLean(c => c.bodyLeanSmoothingRate);

        /// <summary>설정값을 <b>코드 기본값 인스턴스</b>에서 읽는다(숫자로 베끼지 않는다 — CLAUDE.md).
        /// 배포 에셋과 코드 기본값의 드리프트는 <c>Tests/EditMode/ConfigAssetDriftLedgerTests.cs</c>가
        /// 따로 잠그므로 여기서 한 번 더 세지 않는다.</summary>
        private static float ConfigLean(System.Func<StickConfig, float> pick)
        {
            var config = ScriptableObject.CreateInstance<StickConfig>();
            try { return pick(config); }
            finally { Object.DestroyImmediate(config); }
        }

        /// <summary>예측 — <b>프로덕션 함수가 아니라 프리팹 실측</b>에서만 나온다.</summary>
        private static float DepartureInHeights(RigGeometry g, float leanDegrees)
            => 2f * g.ShoulderAboveHip * Mathf.Sin(leanDegrees * Mathf.Deg2Rad) / g.TotalHeight;

        private void SettleIdle(StickmanPoseAnimator pose)
        {
            for (int i = 0; i < 240; i++) pose.ApplyIdlePose(Dt, StaticPoseSettings(), PoseSmoothingRate);
        }

        /// <summary>호흡/흔들림이 0인 포즈 설정 — "기울임과 방향 말고는 아무 것도 변하지 않는다"를 만든다.</summary>
        private static StickmanPoseAnimator.PoseSettings StaticPoseSettings()
            => new StickmanPoseAnimator.PoseSettings(
                legSpread: 12f, armSpread: 40f, idleKnee: 4f, idleElbow: 10f,
                breathAmplitude: 0f, breathFrequencyHz: 0f, breathArmDegrees: 0f);

        private Transform Find(string name)
        {
            Transform t = _root.transform.Find(name);
            Assert.IsNotNull(t, $"리그에서 {name}을 찾지 못했습니다.");
            return t;
        }

        // ============================================================================
        // 리그 — 치수는 전부 프리팹 실측이다
        // ============================================================================

        /// <param name="limbBodyType">팔다리 마디에 붙일 <c>Rigidbody2D</c>의 종류.
        /// null이면 물리 바디를 아예 붙이지 않는다(<c>PhysicsOwnsLimbs()</c>가 false를 돌려주는 경로).</param>
        private StickmanPoseAnimator BuildRig(RigGeometry g, RigidbodyType2D? limbBodyType = null)
        {
            _root = new GameObject("PrefabMeasuredRig");
            _root.transform.position = Vector3.zero;

            AddLimb("LeftLeg", g.HipY, g.LegUpperLength, g.LegLowerLength, limbBodyType);
            AddLimb("RightLeg", g.HipY, g.LegUpperLength, g.LegLowerLength, limbBodyType);
            AddLimb("LeftArm", g.ShoulderY, g.ArmUpperLength, g.ArmLowerLength, limbBodyType);
            AddLimb("RightArm", g.ShoulderY, g.ArmUpperLength, g.ArmLowerLength, limbBodyType);

            var torso = new GameObject("Torso");
            torso.transform.SetParent(_root.transform, false);
            torso.transform.localPosition = new Vector3(0f, g.TorsoCenterY, 0f);
            _torso = torso.transform;

            var head = new GameObject("Head");
            head.transform.SetParent(_root.transform, false);
            head.transform.localPosition = new Vector3(0f, g.HeadY, 0f);
            _head = head.transform;

            var pose = new StickmanPoseAnimator(_root.transform);
            Assert.IsTrue(pose.HasLimbs, "리그에서 팔다리를 찾지 못했습니다 — 이름 규약이 바뀌었을 수 있습니다.");
            return pose;
        }

        private void AddLimb(string name, float attachY, float upperLength, float lowerLength,
            RigidbodyType2D? bodyType)
        {
            var upper = new GameObject(name);
            upper.transform.SetParent(_root.transform, false);
            upper.transform.localPosition = new Vector3(0f, attachY, 0f);
            AddSegmentParts(upper, upperLength, bodyType);

            var lower = new GameObject(name + "Lower");
            lower.transform.SetParent(upper.transform, false);
            lower.transform.localPosition = new Vector3(0f, -upperLength, 0f);
            AddSegmentParts(lower, lowerLength, bodyType);
        }

        private static void AddSegmentParts(GameObject go, float length, RigidbodyType2D? bodyType)
        {
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.05f, length);
            box.offset = new Vector2(0f, -length * 0.5f);

            if (bodyType == null) return;
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = bodyType.Value;
            // 에디트 모드 결정성 — 이 검사는 bodyType만 읽는다(PhysicsOwnsLimbs). 시뮬레이션이
            // 끼어들 여지를 아예 없앤다.
            body.simulated = false;
        }

        // ============================================================================
        // 프리팹 파서 — 기대값의 유일한 출처
        // ============================================================================

        private struct RigGeometry
        {
            public float HipY;
            public float ShoulderY;
            public float TorsoCenterY;
            public float TorsoHalfLength;
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
            var names = new Dictionary<string, string>();
            var jointAnchors = new Dictionary<string, float>();   // 소유자 이름 -> connectedAnchor.y
            var linePoints = new Dictionary<string, List<Vector2>>();
            var transforms = new Dictionary<string, float>();      // 소유자 이름 -> localPosition.y

            // 1차 — GameObject 이름 표
            foreach (string doc in docs)
            {
                if (!Regex.IsMatch(doc, @"(?m)^GameObject:")) continue;
                Match id = Regex.Match(doc, @"^(\d+)");
                Match name = Regex.Match(doc, @"(?m)^\s*m_Name:\s*(.+)$");
                if (id.Success && name.Success) names[id.Groups[1].Value] = name.Groups[1].Value.Trim();
            }

            // 2차 — 컴포넌트
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
                LegUpperLength = Mathf.Abs(RequireAnchor(jointAnchors, "LeftLegLower")),
                LegLowerLength = Mathf.Abs(RequireAnchor(jointAnchors, "LeftLegLower")),
                ArmUpperLength = Mathf.Abs(RequireAnchor(jointAnchors, "LeftArmLower")),
                ArmLowerLength = Mathf.Abs(RequireAnchor(jointAnchors, "LeftArmLower")),
                TorsoCenterY = RequireTransform(transforms, "Torso"),
                HeadY = RequireTransform(transforms, "Head"),
                TorsoHalfLength = MaxAbsY(linePoints, "Torso"),
                HeadRadius = MaxRadius(linePoints, "HeadOutline"),
            };

            // 파서 생존 확인 — 좌우가 같은 값이어야 한다(한쪽만 읽고 지나가는 사고 방지).
            Assert.AreEqual(g.HipY, RequireAnchor(jointAnchors, "RightLeg"), 1e-7f,
                "좌우 고관절 부착점이 다릅니다 — 파서나 프리팹 중 하나가 이상합니다.");
            Assert.AreEqual(g.ShoulderY, RequireAnchor(jointAnchors, "RightArm"), 1e-7f,
                "좌우 어깨 부착점이 다릅니다 — 파서나 프리팹 중 하나가 이상합니다.");
            Assert.Greater(g.ShoulderAboveHip, 0f, "어깨가 엉덩이보다 아래에 있습니다 — 파싱이 깨졌습니다.");
            Assert.Greater(g.TotalHeight, 0f, "신장이 0 이하입니다 — 파싱이 깨졌습니다.");

            _cachedGeometry = g;
            _hasCachedGeometry = true;
            return g;
        }

        private static List<Vector2> ReadLinePositions(string doc)
        {
            var result = new List<Vector2>();
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

        private static float RequireAnchor(Dictionary<string, float> map, string owner)
        {
            Assert.IsTrue(map.ContainsKey(owner),
                $"프리팹에서 {owner}의 HingeJoint2D.connectedAnchor를 읽지 못했습니다 — " +
                "파서가 깨졌거나 프리팹 계층이 바뀌었습니다. 이 파일의 모든 예측값이 이 값에서 나옵니다.");
            return map[owner];
        }

        private static float RequireTransform(Dictionary<string, float> map, string owner)
        {
            Assert.IsTrue(map.ContainsKey(owner),
                $"프리팹에서 {owner}의 Transform.localPosition을 읽지 못했습니다 — 파서가 깨졌습니다.");
            return map[owner];
        }

        private static float MaxAbsY(Dictionary<string, List<Vector2>> map, string owner)
        {
            Assert.IsTrue(map.ContainsKey(owner) && map[owner].Count > 0,
                $"프리팹에서 {owner}의 LineRenderer 점을 읽지 못했습니다 — 파서가 깨졌습니다.");
            float max = 0f;
            foreach (Vector2 p in map[owner]) max = Mathf.Max(max, Mathf.Abs(p.y));
            return max;
        }

        private static float MaxRadius(Dictionary<string, List<Vector2>> map, string owner)
        {
            Assert.IsTrue(map.ContainsKey(owner) && map[owner].Count > 0,
                $"프리팹에서 {owner}의 LineRenderer 점을 읽지 못했습니다 — 파서가 깨졌습니다.");
            float max = 0f;
            foreach (Vector2 p in map[owner]) max = Mathf.Max(max, p.magnitude);
            return max;
        }

        // ============================================================================
        // 기울임 출처 스캐너
        // ============================================================================

        /// <summary>기울임 요청 창구 3종의 <b>호출부</b>를 소스에서 센다(선언부·주석은 제외).
        /// 소스를 읽는 이유는 <c>Tests/EditMode/ShoulderSwingAsymmetryTests.cs</c>와 같다 —
        /// 값이 한 곳에만 남고, 이름이 바뀌면 <b>실패</b>한다.</summary>
        private static Dictionary<string, List<int>> ScanLeanRequestSites()
        {
            string root = Path.Combine(Application.dataPath, ScriptsRelativePath);
            Assert.IsTrue(Directory.Exists(root), $"스크립트 폴더를 찾지 못했습니다: {root}");

            var call = new Regex(@"\b(RequestBodyLean|RequestBodyLeanDegrees|AddHitLean)\s*\(");
            var declaration = new Regex(@"\b(void|float|int|bool)\s+(RequestBodyLean|RequestBodyLeanDegrees|AddHitLean)\s*\(");

            var result = new Dictionary<string, List<int>>();
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').Contains("/Tests/")) continue;

                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    string trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*")) continue;

                    int comment = line.IndexOf("//", System.StringComparison.Ordinal);
                    if (comment >= 0) line = line.Substring(0, comment);

                    if (!call.IsMatch(line)) continue;
                    if (declaration.IsMatch(line)) continue;   // 선언부는 출처가 아니다.

                    string name = Path.GetFileName(file);
                    if (!result.TryGetValue(name, out List<int> hits))
                    {
                        hits = new List<int>();
                        result[name] = hits;
                    }
                    hits.Add(i + 1);
                }
            }
            return result;
        }
    }
}
