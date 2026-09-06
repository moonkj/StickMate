using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ HEAD 6종 H-2 / H-2b 회귀 게이트 — 2026-09-06 R25 이식 직후 신설.
    ///
    /// <para><b>왜 생겼나.</b> R25(모자 6종 H-2 목표 +0.45 상향 + H-2b 신설)가 착지하면서 v1 모자 규약
    /// 검사 5건이 전부 <see cref="HandoffTestGate.SkipIfHandoff"/> / <see cref="HandoffTestGate.SkipIfR25Hat"/>로
    /// 건너뛰기가 됐다. <c>AccessoryStrokeBudgetTests.모자가_머리를_감싸고_커버선이_머리_중심_언저리까지_내려온다</c>는
    /// 그 자리에 <i>"이 자리는 「덜 잠긴 규칙」이 아니라 <b>빈 게이트</b>다 … 대체 자(H-2 착용선 대역)를
    /// 프로덕션 테스트로 세우는 것은 test-engineer 배정 항목"</i>이라고 직접 적어 두었다. <b>이 파일이 그 대체 자다.</b>
    /// 그 전까지 HEAD 6종의 착용선은 <b>어떤 러너도 보지 않았고</b>, 모자가 다시 내려앉아 안경을 덮어도
    /// 아무 테스트도 빨개지지 않았다.</para>
    ///
    /// <para>★ 2026-09-06 후속 — 그 5건 중 <b>2건은 다시 열렸고</b>(R25d 가 베레모 테·밀짚모자 띠를
    /// 몸/관 밑변에서 유도하도록 다시 쓰면서 「올린 띠」 규약이 성립하게 됐다), <b>1건은 삭제됐다</b>.
    /// 삭제된 것이 위 문단이 인용한 그 검사다 — 커버선 ≤ +0.10 R 규약은 이 파일이 요구하는 착용선 대역
    /// [+0.30, +0.45] R과 <b>수학적으로 양립 불가</b>(실측 커버선 베레모 +0.3602 · 밀짚모자 +0.4162)라,
    /// 리더 판정으로 <b>「재개방 대기」가 아니라 폐기</b>로 재분류됐다. 판정 근거는
    /// <c>AccessoryStrokeBudgetTests.cs</c> 의 묘비 주석에 남아 있다.
    /// <c>SkipIfR25Hat</c> 호출부는 5 → 3 → <b>2</b>(남은 둘은 재저작 대기 중인 음성 대조).
    /// <b>즉 이 파일은 «아직 못 세운 대체 자»가 아니라, 그 자리를 정식으로 물려받은 게이트다.</b></para>
    ///
    /// <para><b>무엇을 재는가.</b> <c>design/equipment/verify/r24_hats.py</c>(프로덕션 <c>.cs</c> 직접 파싱)와
    /// <b>같은 판정</b>을 C#으로 옮겼다. 좌표는 파이썬처럼 소스를 파싱하지 않고 <b>실제로 조립된 조각</b>
    /// (<see cref="AccessorySilhouetteMetrics.Build"/>)에서 읽는다 — 상수/좌표를 이 파일에 베끼지 않는다.</para>
    /// <list type="number">
    ///   <item><b>H-2 착용선</b> — 앞층 채움(<c>Filled</c> ∧ <c>SortingOrder ≥ SortEyes</c>)의 합집합이
    ///         머리 현을 <b>연속으로 덮는 첫 구간의 밑단</b>. 대역 [+0.30, +0.45] R.
    ///         <br/>· <b>너무 낮으면</b>(&lt; 하한) 모자가 내려앉아 <b>안경을 가린다</b> — 사용자 신고 그 자체다.
    ///         <br/>· <b>너무 높으면</b>(&gt; 상한) 머리를 못 덮고 <b>얹힌 것처럼</b> 보인다.</item>
    ///   <item><b>덮임이 머리 꼭대기에서 시작하는가</b> — 착용선 하나만 보면
    ///         「위가 비었는데 아래에서 처음 덮인 것」을 <b>통과로 오독</b>한다(r24 ① 경고, 옛 v1 밀짚모자가 그 경우였다).</item>
    ///   <item><b>H-2b 앞층 채움 밑단</b>(머리 폭 |x| ≤ 머리 반경 안에서 잰다) — <b>가려짐을 실제로 정하는 자</b>.
    ///         착용선은 상한만 있어서 「그 아래로 얼마나 더 내려오는가」를 한 번도 재지 않았고,
    ///         그 구멍으로 털모자가 「목표 0.00을 −0.044로 통과」하면서 안경을 71.3%까지 지웠다
    ///         (<c>docs/EQUIPMENT_HANDOFF_PORT_SPEC.md §14-15-0</c>).</item>
    /// </list>
    ///
    /// <para><b>기대값의 출처.</b> 대역 숫자는 <b>프로덕션 함수가 아니라 설계 문서(골든)</b>에서 온다 —
    /// TEAM.md 「기대값을 프로덕션 함수로 만들지 마라. 그 함수가 틀어지면 기대값도 함께 틀어져
    /// <b>아무것도 못 잰다</b>」. 반대로 <b>재는 대상</b>(좌표)은 전부 프로덕션에서 조립해 읽는다.</para>
    ///
    /// <para><b>자의 교정.</b> TEAM.md 공통 처방 — 「계산기·검사기를 만들면 <b>알려진 값으로 먼저 교정</b>한다.
    /// 교정이 깨지면 그 뒤 숫자를 전부 폐기한다」. 그래서 이 파일의 스캔라인 자는
    /// <see cref="자_교정_해석적으로_답을_아는_도형에서_착용선이_맞다"/>에서 <b>손으로 답이 풀리는 사각형</b>
    /// 여섯 개로 먼저 교정된다(모자 좌표가 하나도 안 들어간다). 그 교정이 깨지면 아래 6종 숫자는 전부 무효다.</para>
    ///
    /// <para><b>이 자는 파이썬 검산기와 대조돼 있다</b>(2026-09-06, Unity 밖 Mono 실행):
    /// 같은 앞층 채움 좌표를 넣었을 때 <c>r24_hats.py</c>의 래스터 자와 이 파일의 스캔라인 자가
    /// 6종 전부 <b>소수 6자리까지 같은 값</b>을 냈다(착용선 0.448213/0.328213/0.412213/0.440213/0.360213/0.416213 ·
    /// 밑단 0.301000/0.259617/0.284450/0.315200/0.311560/0.314000). 두 구현은 서로 독립이라
    /// <b>같은 함정에 같이 빠지지 않는다</b>(TEAM.md 열 번째 형태의 처방).</para>
    ///
    /// <para><b>양성 대조.</b> §7의 세 검사가 6종 좌표를 <b>사본에서</b> 위·아래로 밀어 판정이 실제로
    /// 뒤집히는지 매번 증명한다. 이 저장소가 아홉 번 당한 「실패한 측정과 성공한 측정이 똑같이 생겼다」를
    /// 여기서 막는다 — 자가 죽으면 이 대조가 먼저 빨개진다.</para>
    ///
    /// <para>★★ <b>두 자가 하는 일이 다르다 — 실측으로 확인했고, 직관과 다르다</b>
    /// (Mono 교차검증 2026-09-06, 이동 스윕 −0.30~+0.30 R):
    /// <list type="bullet">
    ///   <item><b>H-2b 밑단은 평행이동에 정확히 비례한다</b> — Δ만큼 그대로 내려간다. 그래서
    ///         「모자가 내려앉았다」를 <b>확실히</b> 잡는다. 6종 전부 <b>−0.05 R</b>만 내려도 하한을 깬다.</item>
    ///   <item><b>H-2 착용선은 평행이동에 단조가 아니다.</b> 모자를 내리면 착용선이 <b>올라갈 수도</b> 있다 —
    ///         관이 내려가면 그 높이의 머리 현이 넓어져 <b>덮임 구간이 먼저 끊기고</b>, 그 아래 챙이 만드는
    ///         것은 <i>둘째</i> 구간이라 착용선으로 안 쳐 준다. 실측: 야구모자를 −0.20 R 내리면 착용선이
    ///         +0.4482 → <b>+0.4762</b>로 <b>올라간다</b>. −0.15 R에서는 +0.3082로 <b>대역 안에 그대로 남는다</b>
    ///         (그때 밑단은 +0.151이라 H-2b가 잡는다).</item>
    /// </list>
    /// ⇒ <b>착용선만 보면 내려앉은 모자를 놓친다.</b> 스펙 §14-15-0이 글로 적은 사실이 이 자에서도 재현된다.
    /// 그래서 두 검사를 <b>둘 다</b> 둔다. 이 문단을 안 읽고 양성 대조를 <c>«아래로 밀면 하한 미달»</c>로 쓰면
    /// 6종 중 3종에서 <b>거짓 빨강</b>이 난다 — 이 파일의 첫 판이 실제로 그랬고, Mono 교차검증이 잡았다.</para>
    ///
    /// <para><b>유저 자산 불변.</b> 이 파일은 읽기만 한다. 변이는 전부 <c>double[]</c> <b>사본</b> 위에서
    /// 일어나고 프로덕션 <see cref="AccessoryShapeBuilder.Shape.Points"/>는 건드리지 않는다.</para>
    ///
    /// <para>★ 2026-09-06 — 이 클래스는 <b>partial</b>이다. 짝 파일
    /// <c>AccessoryHatWearLineBandTests.CoverLine.cs</c>(§8)가 <b>같은 자</b>로
    /// 「커버선(<see cref="AccessoryShapeBuilder.HatCoverLocalY"/>) ≥ 그 모자의 H-2 착용선」을 잠근다.
    /// 자를 복사하지 않고 <see cref="ChordCoveredAt"/>·<see cref="FrontFillPolygonsInR"/>·
    /// <see cref="Measure"/>를 그대로 쓰기 위해 나눈 것이다 — 자가 둘이 되면 두 게이트가 조용히 갈라진다.</para>
    /// </summary>
    public sealed partial class AccessoryHatWearLineBandTests
    {
        // ====================================================================
        // 0. 자(尺) 상수 — 프로덕션에서 유도하는 것 / 설계 문서에서 오는 것을 갈라 둔다
        // ====================================================================

        /// <summary>H-2가 쓰는 「가장 큰 머리」 = 배율 하한(0.35)에서 링 획 1pt를 포함한 잉크 원반의 바깥 반경(R 배수).
        /// <para><b>숫자를 베끼지 않는다</b> — <see cref="AccessoryShapeBuilder.BaselineHeadVisualRadius"/> ·
        /// <see cref="StickConfig.MinCharacterScale"/> · <see cref="StickConfig.ReferencePointsPerWorldUnitApprox"/>
        /// 셋에서 유도한다. 유도식은 <c>design/equipment/verify/r17_model.py:32</c>
        /// (<c>HEAD_R_COVER = 1.0 + ONE_PT_R[0.35] / 2</c>)와 같다.</para>
        /// <para>왜 <b>가장 큰</b> 머리인가: 모자는 도달 가능한 최대 머리를 가려야 한다
        /// (<c>docs/CHARACTER_BODY_AUDIT_2026-09-05.md §2</c> — 출하 실효값은 1.17193 R이고 1.18421 R은 상한).</para></summary>
        private static double HeadCoverRadiusInR
        {
            get
            {
                double headRadiusInPointsAtMinScale =
                    (double)AccessoryShapeBuilder.BaselineHeadVisualRadius
                    * StickConfig.MinCharacterScale
                    * StickConfig.ReferencePointsPerWorldUnitApprox;
                Assert.Greater(headRadiusInPointsAtMinScale, 0.0,
                    "머리 반경(pt)이 0 이하입니다 — 유도 입력 세 상수 중 하나가 사라졌거나 0이 됐습니다. " +
                    "이 값이 0이면 아래 모든 판정이 대상 없이 돌게 됩니다.");
                return 1.0 + 1.0 / headRadiusInPointsAtMinScale / 2.0;
            }
        }

        /// <summary>설계 골든 — <c>r17_model.py:32</c>가 찍는 값. 유도 경로가 틀어지면 여기서 먼저 걸린다.
        /// (이 상수는 <b>기대값</b>이지 프로덕션 값이 아니다 — 그래서 여기 적혀 있어도 규칙 위반이 아니다.)</summary>
        private const double HeadCoverRadiusDesignGolden = 1.1842129501703970;

        /// <summary>H-2 여유 — 「합집합의 중앙 반폭 ≥ 그 높이 머리 현 반폭 <b>+ 0.06 R</b>」.
        /// 출처: <c>docs/EQUIPMENT_HANDOFF_PORT_SPEC.md</c> 규칙 표 <b>H-2</b>.</summary>
        private const double CoverMarginInR = 0.06;

        /// <summary>착용선 대역 <b>상한</b> — 「R25 개정: 6종 공통 <b>+0.45 R</b>」(스펙 규칙 표 H-2 · §14-15).
        /// 이보다 높으면 머리를 못 덮고 <b>얹힌다</b>.</summary>
        private const double WearLineCeilingInR = 0.45;

        /// <summary>착용선 대역 <b>하한</b> — 이보다 낮으면 모자가 내려앉아 <b>안경을 가린다</b>.
        /// <para>★ <b>잠정값이다.</b> 출처는 <c>design/equipment/verify/r24_hats.py</c>의
        /// <c>H2_BAND = (0.30, 0.45)</c>이고, 그 파일이 스스로 <i>"coder가 확정 6종의 실측에서 역산한 추정 …
        /// 정본은 design-equipment 문서다"</i>라고 적어 두었다. design-equipment가 하한을 공식 문서화하면
        /// <b>이 한 줄만</b> 그 값으로 바꾼다.</para>
        /// <para>현행 여유(가장 빠듯한 쪽): 털모자 +0.3282 → 하한까지 0.028 R.</para></summary>
        private const double WearLineFloorInR = 0.30;

        /// <summary>H-2b 앞층 채움 밑단 <b>일반 하한</b> — 스펙 규칙 표 <b>H-2b</b>
        /// (<i>"앞층 채움 잉크의 밑단(머리 폭 |x| ≤ 1.1842 R 안에서 잰다)이 +0.28 R 이상"</i>).</summary>
        private const double FrontFillBottomFloorInR = 0.28;

        /// <summary>H-2b 수렴 목표 — 왕관 실측 밑단(+0.3152)에 맞춘 값(스펙 §14-15-2).
        /// 판정에는 쓰지 않고 <b>로그에만</b> 찍는다: 하한만 걸면 탐색이 「가능한 한 높이」로 도망간다는
        /// 실측(베레모 밑단 +0.4355 → 가려짐 2.3%)이 스펙에 남아 있어, 목표에서 얼마나 벗어났는지는
        /// 사람이 봐야 할 정보지 자동 실패시킬 값이 아니다.</summary>
        private const double FrontFillBottomAimInR = 0.315;

        /// <summary>★ 승인된 예외 1건 — <b>털모자</b>의 H-2b 하한.
        /// <para>스펙 §14-15-7이 리더 판정용으로 A/B/C 세 안을 올렸고 <b>안 B(정체 우선)</b>가 채택됐다
        /// (프로덕션 <c>AccessoryShapeBuilder.Handoff.cs</c> <c>case HeadBeanie</c>의 <c>OffsetYInR</c>가
        /// 안 B의 값이다). 안 B의 밑단은 <b>+0.2596</b>으로 일반 하한(+0.28) 아래이고, 그 대가로
        /// 「이 카테고리에서 가장 깊이 눌러쓰는 모자가 털모자」라는 정체와
        /// <c>AccessoryShapeCatalogTests</c>의 커버선 계약이 함께 산다.</para>
        /// <para>여유는 스캔 눈금 한 칸(<see cref="ScanStepInR"/>)만 준다 — 이 예외가 <b>더 내려가는 것</b>은
        /// 여전히 회귀다. 그리고 이 예외가 <b>낡아서 조용히 사는 것</b>은
        /// <see cref="H2b_예외는_털모자_하나뿐이고_지금도_살아_있다"/>가 막는다.</para></summary>
        private const double BeanieFrontFillBottomFloorInR = 0.2596 - ScanStepInR;

        /// <summary>세로 스캔 눈금. <c>r24_hats.py</c>의 <c>h2_wear</c>·<c>coverage_runs(step=0.004)</c>와 같다.
        /// <para>양자화는 <b>상한 쪽으로 보수적</b>이다: 돌려주는 값은 「실제로 덮인 것이 확인된 가장 낮은 표본」이라
        /// 상한(≤ 0.45) 판정은 증거가 있는 쪽으로만 통과한다.</para></summary>
        private const double ScanStepInR = 0.004;

        /// <summary>덮임 구간의 시작이 「머리 꼭대기」인지 보는 여유(r24 ①과 같다).</summary>
        private const double HeadTopToleranceInR = 0.02;

        /// <summary>턱 — 스캔 하한(r24와 같다). 이보다 아래는 모자의 자리가 아니다.</summary>
        private const double ChinLimitInR = -1.2;

        /// <summary>부동소수 비교 여유(r24의 <c>1e-6</c>과 같다).</summary>
        private const double Epsilon = 1e-6;

        /// <summary>왕관 앵커 — R25가 <b>무변경</b>으로 남긴 기준 대역(스펙 §14-15-2 「왕관 … 이미 목표 안」).
        /// 나머지 다섯이 여기로 수렴했으므로, 이 값이 움직이면 대역 자체가 움직인 것이다.</summary>
        private const double CrownWearLineGoldenInR = 0.4402;

        private const double CrownFrontFillBottomGoldenInR = 0.3152;

        /// <summary>앵커 허용 오차 — 자의 눈금(0.004)보다 넉넉하되 설계 이동은 잡을 만큼 좁게.</summary>
        private const double CrownGoldenToleranceInR = 0.01;

        // ====================================================================
        // 1. 대상 — 표를 코드가 갖고, 표가 카탈로그 전부를 덮는지 스스로 센다
        // ====================================================================

        /// <summary>이 게이트가 지키는 HEAD 번호 전부. <b>여기 없는 모자는 아무도 안 본다</b> —
        /// 그래서 <see cref="게이트_표가_HEAD_카탈로그_전부를_덮는다"/>가 개수를 카탈로그에서 세어 대조한다.</summary>
        private static readonly int[] GatedHeadItems =
        {
            AccessoryShapeBuilder.HeadCap,
            AccessoryShapeBuilder.HeadBeanie,
            AccessoryShapeBuilder.HeadFedora,
            AccessoryShapeBuilder.HeadCrown,
            AccessoryShapeBuilder.HeadBeret,
            AccessoryShapeBuilder.HeadStraw,
        };

        /// <summary>그 모자의 H-2b 하한. 털모자만 승인된 예외값을 쓴다.</summary>
        private static double FrontFillBottomFloorFor(int item)
            => item == AccessoryShapeBuilder.HeadBeanie
                ? BeanieFrontFillBottomFloorInR
                : FrontFillBottomFloorInR;

        private static string Label(int item)
            => $"HEAD {item}번({ItemCatalog.Item(EquipmentSlot.Head, item).DisplayName})";

        // ====================================================================
        // 2. 자 — 정확 스캔라인(래스터 아님). r25_hats.py가 쓰는 «빠른 등가물»과 같은 식이다
        // ====================================================================

        /// <summary>한 모자의 계측 결과. <b>「못 쟀다」와 「0이 나왔다」를 형태로 갈라 둔다</b> —
        /// 조각이 없어 아무것도 못 잰 상태가 «착용선 0»으로 둔갑하면 그 초록은 아무것도 증명하지 않는다.</summary>
        private readonly struct HatCoverage
        {
            /// <summary>앞층 채움 조각이 하나라도 있는가. false면 아래 값은 전부 무의미하다.</summary>
            public readonly bool HasFrontFill;

            /// <summary>머리 현을 연속으로 덮는 구간이 하나라도 있는가.</summary>
            public readonly bool Covered;

            /// <summary>그 첫 구간이 <b>머리 꼭대기</b>에서 시작하는가.</summary>
            public readonly bool StartsAtHeadTop;

            /// <summary>첫 덮임 구간의 밑단 = H-2 착용선(R 배수).</summary>
            public readonly double WearLineInR;

            /// <summary>앞층 채움 잉크가 <b>머리 폭 안</b>에 닿는 가장 낮은 높이 = H-2b(R 배수).</summary>
            public readonly double FrontFillBottomInR;

            public HatCoverage(bool hasFrontFill, bool covered, bool startsAtHeadTop,
                double wearLineInR, double frontFillBottomInR)
            {
                HasFrontFill = hasFrontFill;
                Covered = covered;
                StartsAtHeadTop = startsAtHeadTop;
                WearLineInR = wearLineInR;
                FrontFillBottomInR = frontFillBottomInR;
            }

            /// <summary>꼬리 = 착용선 − 밑단. 모자를 통째로 올려도 <b>안 변하는</b> 조형 상수다(스펙 §14-15-1).
            /// 꼬리가 (상한 − 하한)보다 길면 그 모자는 <b>평행이동으로는 절대 못 맞춘다</b>.</summary>
            public double TailInR => WearLineInR - FrontFillBottomInR;
        }

        private enum WearLineVerdict
        {
            Pass,

            /// <summary>앞층 채움 조각이 아예 없다 — 잴 대상이 없다.</summary>
            NoFrontFill,

            /// <summary>어느 높이에서도 머리 현을 못 덮는다.</summary>
            NeverCovers,

            /// <summary>덮기는 하는데 머리 꼭대기가 비어 있다(아래에서 처음 덮인다).</summary>
            NotFromHeadTop,

            /// <summary>대역 하한 미달 — 너무 내려왔다. <b>안경을 가린다.</b></summary>
            BelowFloor,

            /// <summary>대역 상한 초과 — 너무 올라갔다. <b>얹힌다.</b></summary>
            AboveCeiling,
        }

        private static WearLineVerdict JudgeWearLine(in HatCoverage m)
        {
            if (!m.HasFrontFill) return WearLineVerdict.NoFrontFill;
            if (!m.Covered) return WearLineVerdict.NeverCovers;
            if (!m.StartsAtHeadTop) return WearLineVerdict.NotFromHeadTop;
            if (m.WearLineInR < WearLineFloorInR - Epsilon) return WearLineVerdict.BelowFloor;
            if (m.WearLineInR > WearLineCeilingInR + Epsilon) return WearLineVerdict.AboveCeiling;
            return WearLineVerdict.Pass;
        }

        /// <summary>다각형과 수평선 <paramref name="y"/>의 교차 x들을 <paramref name="into"/>에 넣는다(짝수-홀수).
        /// <c>r17_model._scan_intervals</c>·<c>r24_hats.inside</c>와 <b>같은 규약</b>이다.</summary>
        private static void AppendCrossings(double[] poly, double y, List<double> into)
        {
            int n = poly.Length / 2;
            for (int i = 0; i < n; i++)
            {
                int j = i + 1 == n ? 0 : i + 1;
                double ay = poly[i * 2 + 1];
                double by = poly[j * 2 + 1];
                if (ay > y == by > y) continue;
                double ax = poly[i * 2];
                double bx = poly[j * 2];
                into.Add(ax + (y - ay) * (bx - ax) / (by - ay));
            }
        }

        /// <summary>여러 다각형 합집합의 <paramref name="y"/> 스캔라인 구간들(정렬·병합됨).</summary>
        private static List<double> MergedSpans(List<double[]> polys, double y, List<double> crossingScratch)
        {
            var spans = new List<double>();
            for (int p = 0; p < polys.Count; p++)
            {
                crossingScratch.Clear();
                AppendCrossings(polys[p], y, crossingScratch);
                crossingScratch.Sort();
                for (int k = 0; k + 1 < crossingScratch.Count; k += 2)
                {
                    spans.Add(crossingScratch[k]);
                    spans.Add(crossingScratch[k + 1]);
                }
            }
            if (spans.Count == 0) return spans;

            // (시작, 끝) 쌍을 시작 기준으로 정렬한 뒤 병합. 쌍 단위라 단순 정렬을 쓸 수 없어 인덱스로 고른다.
            int pairs = spans.Count / 2;
            var order = new int[pairs];
            for (int i = 0; i < pairs; i++) order[i] = i;
            Array.Sort(order, (a, b) => spans[a * 2].CompareTo(spans[b * 2]));

            var merged = new List<double>();
            for (int i = 0; i < pairs; i++)
            {
                double a = spans[order[i] * 2];
                double b = spans[order[i] * 2 + 1];
                if (merged.Count > 0 && a <= merged[merged.Count - 1] + 1e-9)
                {
                    merged[merged.Count - 1] = Math.Max(merged[merged.Count - 1], b);
                }
                else
                {
                    merged.Add(a);
                    merged.Add(b);
                }
            }
            return merged;
        }

        /// <summary>x = 0을 품은 구간의 <b>작은 쪽 반폭</b>(보수적). 그런 구간이 없으면 0.</summary>
        private static double CentralHalfWidth(List<double[]> polys, double y, List<double> scratch)
        {
            List<double> merged = MergedSpans(polys, y, scratch);
            for (int i = 0; i + 1 < merged.Count; i += 2)
            {
                if (merged[i] <= 0.0 && 0.0 <= merged[i + 1])
                {
                    return Math.Min(-merged[i], merged[i + 1]);
                }
            }
            return 0.0;
        }

        /// <summary>그 높이에서 잉크가 <b>머리 폭 안</b>([−xLimit, +xLimit])에 조금이라도 닿는가.
        /// <para>챙 끝(±1.8 R)이나 베레모의 옆 처짐은 얼굴을 못 가린다 — 그래서 밑단은 전체가 아니라
        /// 머리 폭 안에서 잰다(스펙 §14-15-1).</para></summary>
        private static bool TouchesHeadWidth(List<double[]> polys, double y, double xLimit, List<double> scratch)
        {
            List<double> merged = MergedSpans(polys, y, scratch);
            for (int i = 0; i + 1 < merged.Count; i += 2)
            {
                if (merged[i + 1] >= -xLimit && merged[i] <= xLimit) return true;
            }
            return false;
        }

        /// <summary>★ <b>H-2 덮임 술어</b> — 「그 높이에서 앞층 채움이 머리 현을 (여유 포함) 덮는가」.
        ///
        /// <para><see cref="Measure"/>의 착용선 탐색과 §8 커버선 게이트가 <b>이 함수 하나</b>를 쓴다.
        /// 술어를 두 곳에 적으면 한쪽만 고쳐지는 순간 두 게이트가 <b>다른 것을 재면서 같은 이름</b>으로
        /// 보고한다 — 이 저장소가 반복해 온 형태다. <c>r23_crown.py</c>의
        /// <c>_central_hw(polys, y) &gt;= _need(y)</c>와 같은 식이다.</para>
        ///
        /// <para><paramref name="headR"/>를 인자로 받는 이유: <see cref="HeadCoverRadiusInR"/>는
        /// 호출마다 상수 셋을 다시 유도하고 <c>Assert</c>까지 도는 프로퍼티라, 스캔 표본마다 부르면
        /// 같은 값을 수천 번 다시 만든다.</para></summary>
        private static bool ChordCoveredAt(List<double[]> polys, double y, double headR, List<double> scratch)
        {
            double need = Math.Sqrt(Math.Max(headR * headR - y * y, 0.0)) + CoverMarginInR;
            return CentralHalfWidth(polys, y, scratch) >= need;
        }

        /// <summary>앞층 채움 다각형들에서 H-2 / H-2b를 잰다.</summary>
        private static HatCoverage Measure(List<double[]> polys)
        {
            if (polys == null || polys.Count == 0)
            {
                return new HatCoverage(false, false, false, double.NaN, double.NaN);
            }

            double headR = HeadCoverRadiusInR;
            var scratch = new List<double>(32);

            // ── H-2 착용선: 머리 꼭대기에서 내려오며 «첫 덮임 구간»의 밑단을 찾는다 ──
            bool covered = false;
            bool startsAtTop = false;
            double wear = double.NaN;
            int steps = (int)Math.Ceiling((headR - ChinLimitInR) / ScanStepInR);
            for (int i = 0; i < steps; i++)
            {
                double y = headR - i * ScanStepInR;   // 누적 덧셈이 아니라 곱셈 — 오차가 쌓이지 않는다
                if (ChordCoveredAt(polys, y, headR, scratch))
                {
                    if (!covered)
                    {
                        covered = true;
                        startsAtTop = y >= headR - HeadTopToleranceInR;
                    }
                    wear = y;
                }
                else if (covered)
                {
                    break;   // 첫 구간이 끝났다. 아래에 또 있어도 그것은 «착용선»이 아니다
                }
            }

            // ── H-2b 앞층 채움 밑단(머리 폭 안) ──
            double minY = double.MaxValue;
            double maxY = double.MinValue;
            for (int p = 0; p < polys.Count; p++)
            {
                double[] poly = polys[p];
                for (int i = 1; i < poly.Length; i += 2)
                {
                    if (poly[i] < minY) minY = poly[i];
                    if (poly[i] > maxY) maxY = poly[i];
                }
            }

            double yHi = maxY + 0.01;
            double yLo = minY - 0.01;
            double bottom = double.NaN;
            int bottomSteps = (int)Math.Ceiling((yHi - yLo) / ScanStepInR);
            for (int i = 0; i < bottomSteps; i++)
            {
                double y = yHi - i * ScanStepInR;
                if (TouchesHeadWidth(polys, y, headR, scratch)) bottom = y;
            }

            return new HatCoverage(true, covered, startsAtTop, wear, bottom);
        }

        // ====================================================================
        // 3. 프로덕션 좌표 읽기 — 소스 파싱이 아니라 «실제로 조립된 조각»에서
        // ====================================================================

        private static AccessoryShapeBuilder.Rig Rig() => AccessorySilhouetteMetrics.Rig();

        /// <summary>앞층 채움 조각(<c>Filled</c> ∧ <c>SortingOrder ≥ SortEyes</c>)만 골라 R 배수 좌표로 옮긴다.
        /// <para>왜 이 둘인가: 화면에서 <b>안경 위를 덮는 것</b>은 눈 층보다 앞에 오는 <b>불투명 면</b>뿐이다.
        /// 뒤층(<c>AccessoryPieceLayer.Back</c> → <see cref="AccessoryShapeBuilder.SortBack"/>)으로 간
        /// 먼 쪽 챙은 머리 뒤라 얼굴을 못 가리고, 채움이 없는 낱선은 이 자에서 세지 않는다
        /// (윤곽까지 포함한 둘째 자는 r24가 따로 찍는다 — 여기서는 §14-12 자 하나만 쓴다).</para></summary>
        private static List<double[]> FrontFillPolygonsInR(int item, out int totalPieces)
        {
            AccessoryShapeBuilder.Rig rig = Rig();
            List<AccessoryShapeBuilder.Shape> shapes =
                AccessorySilhouetteMetrics.Build(rig, EquipmentSlot.Head, item);
            totalPieces = shapes.Count;

            var polys = new List<double[]>();
            for (int s = 0; s < shapes.Count; s++)
            {
                if (!shapes[s].Filled) continue;
                if (shapes[s].SortingOrder < AccessoryShapeBuilder.SortEyes) continue;

                Vector3[] pts = shapes[s].Points;
                if (pts == null || pts.Length < 3) continue;

                var flat = new double[pts.Length * 2];
                for (int i = 0; i < pts.Length; i++)
                {
                    // rig.F 가 x 에만 facing 부호를 곱하므로 되돌릴 때도 x 에만 나눈다.
                    flat[i * 2] = pts[i].x / (rig.HeadRadius * rig.Facing);
                    flat[i * 2 + 1] = (pts[i].y - rig.HeadCenterY) / rig.HeadRadius;
                }
                polys.Add(flat);
            }
            return polys;
        }

        /// <summary>좌표를 세로로 민 <b>사본</b>. 프로덕션 배열은 건드리지 않는다.</summary>
        private static List<double[]> ShiftedCopy(List<double[]> polys, double deltaY)
        {
            var copy = new List<double[]>(polys.Count);
            for (int p = 0; p < polys.Count; p++)
            {
                var q = (double[])polys[p].Clone();
                for (int i = 1; i < q.Length; i += 2) q[i] += deltaY;
                copy.Add(q);
            }
            return copy;
        }

        // ====================================================================
        // 4. 자 교정 — 모자 좌표가 하나도 안 들어간다. 여기가 깨지면 아래는 전부 무효다
        // ====================================================================

        /// <summary>유도한 머리 반경이 설계 골든과 같은가. 유도 입력 세 상수 중 하나가 바뀌면 여기가 먼저 말한다.</summary>
        [Test]
        public void 자_교정_머리_반경을_프로덕션_상수_셋에서_유도한다()
        {
            double derived = HeadCoverRadiusInR;
            Assert.AreEqual(HeadCoverRadiusDesignGolden, derived, 1e-5,
                $"H-2가 쓰는 머리 반경을 {derived:F7} R로 유도했는데 설계 골든은 " +
                $"{HeadCoverRadiusDesignGolden:F7} R입니다(design/equipment/verify/r17_model.py:32).\n" +
                "유도 입력은 셋뿐입니다 — AccessoryShapeBuilder.BaselineHeadVisualRadius(" +
                $"{AccessoryShapeBuilder.BaselineHeadVisualRadius}) · StickConfig.MinCharacterScale(" +
                $"{StickConfig.MinCharacterScale}) · StickConfig.ReferencePointsPerWorldUnitApprox(" +
                $"{StickConfig.ReferencePointsPerWorldUnitApprox}).\n" +
                "셋 중 하나가 의도적으로 바뀌었다면 모자 6종의 착용선도 함께 움직인 것이고, " +
                "design-equipment가 대역을 다시 재야 합니다. 이 골든만 고치면 안 됩니다.");

            // 대역이 뒤집혀 있으면(하한 ≥ 상한) 아래 모든 판정이 무조건 실패/통과로 굳는다.
            Assert.Less(WearLineFloorInR, WearLineCeilingInR,
                "착용선 대역의 하한이 상한보다 크거나 같습니다 — 대역이 아니라 빈 집합입니다.");
            Assert.Less(FrontFillBottomFloorInR, WearLineCeilingInR,
                "H-2b 하한이 착용선 상한보다 높습니다 — 밑단이 착용선 위에 있어야 한다는 뜻이 되어 " +
                "산술적으로 만족할 수 없습니다.");
        }

        /// <summary>★ <b>손으로 답을 아는 도형</b> 두 개로 스캔라인 자를 교정한다.
        ///
        /// <para>(가) <b>넓은 판</b> — x ∈ [−2, +2], y ∈ [+0.30, +3.0]. 반폭 2.0은 머리 현 최대치
        /// (반경 + 0.06 ≈ 1.244)보다 항상 크므로 <b>판이 있는 높이는 전부 덮인다</b>.
        /// ⇒ 착용선 = 판의 밑변 +0.30(눈금 한 칸 안).</para>
        ///
        /// <para>(나) <b>좁은 기둥</b> — x ∈ [−0.5, +0.5], y ∈ [−1.0, +3.0]. 반폭 0.5는
        /// <c>√(R² − y²) + 0.06</c>이 0.5 이하인 높이에서만 이긴다.
        /// ⇒ 착용선 = <c>√(R² − 0.44²)</c> = 이 파일 밖에서도 손으로 풀리는 닫힌 식이다.</para>
        ///
        /// <para>(다) <b>아래로 내려간 판</b> — 같은 판을 y ∈ [+0.30, +0.90]으로 잘라 머리 꼭대기를 비운다.
        /// ⇒ 덮기는 하지만 <b>꼭대기에서 시작하지 않는다</b>. 착용선 하나만 보는 자는 이것을 통과로 읽는다.</para>
        ///
        /// <para>(마)(바) <b>판정 갈래 도달성</b> — 판을 y ≥ +0.10 / y ≥ +0.60으로 놓아
        /// <see cref="WearLineVerdict.BelowFloor"/>와 <see cref="WearLineVerdict.AboveCeiling"/>을
        /// <b>각각 실제로 내게</b> 한다. 실제 모자 6종은 전부 대역 안이라, 이 두 갈래가 코드에서 죽어 있어도
        /// 6종 검사만으로는 <b>영원히 안 드러난다</b>.</para></summary>
        [Test]
        public void 자_교정_해석적으로_답을_아는_도형에서_착용선이_맞다()
        {
            double headR = HeadCoverRadiusInR;

            // (가) 넓은 판
            var widePlate = new List<double[]>
            {
                new[] { -2.0, 0.30, 2.0, 0.30, 2.0, 3.0, -2.0, 3.0 },
            };
            HatCoverage wide = Measure(widePlate);
            Assert.IsTrue(wide.HasFrontFill && wide.Covered, "(가) 넓은 판이 아예 안 덮였습니다 — 자가 죽었습니다.");
            Assert.IsTrue(wide.StartsAtHeadTop, "(가) 넓은 판은 머리 꼭대기부터 덮어야 합니다.");
            Assert.GreaterOrEqual(wide.WearLineInR, 0.30 - Epsilon,
                $"(가) 착용선 {wide.WearLineInR:F6}이 판의 밑변(+0.30)보다 아래입니다 — 없는 잉크를 덮였다고 셌습니다.");
            Assert.Less(wide.WearLineInR, 0.30 + ScanStepInR,
                $"(가) 착용선 {wide.WearLineInR:F6}이 판의 밑변(+0.30)에서 눈금 한 칸({ScanStepInR}) 넘게 떴습니다.");
            Assert.GreaterOrEqual(wide.FrontFillBottomInR, 0.30 - ScanStepInR,
                $"(가) 밑단 {wide.FrontFillBottomInR:F6}이 판의 밑변(+0.30)보다 눈금 넘게 아래입니다.");
            Assert.Less(wide.FrontFillBottomInR, 0.30 + ScanStepInR,
                $"(가) 밑단 {wide.FrontFillBottomInR:F6}이 판의 밑변(+0.30)에서 눈금 넘게 떴습니다.");

            // (나) 좁은 기둥 — 닫힌 식으로 기대값을 만든다(측정 코드와 다른 경로다)
            const double columnHalfWidth = 0.5;
            double expectedNarrowWear =
                Math.Sqrt(headR * headR - (columnHalfWidth - CoverMarginInR) * (columnHalfWidth - CoverMarginInR));
            var narrowColumn = new List<double[]>
            {
                new[] { -columnHalfWidth, -1.0, columnHalfWidth, -1.0, columnHalfWidth, 3.0, -columnHalfWidth, 3.0 },
            };
            HatCoverage narrow = Measure(narrowColumn);
            Assert.IsTrue(narrow.Covered && narrow.StartsAtHeadTop,
                "(나) 좁은 기둥은 머리 꼭대기(현 반폭 0.06)에서는 덮어야 합니다.");
            Assert.GreaterOrEqual(narrow.WearLineInR, expectedNarrowWear - Epsilon,
                $"(나) 착용선 {narrow.WearLineInR:F6}이 닫힌 식 {expectedNarrowWear:F6}보다 아래입니다 — " +
                "머리 현을 못 덮는 높이를 덮였다고 셌습니다.");
            Assert.Less(narrow.WearLineInR, expectedNarrowWear + ScanStepInR,
                $"(나) 착용선 {narrow.WearLineInR:F6}이 닫힌 식 {expectedNarrowWear:F6}에서 눈금 한 칸 넘게 떴습니다.");

            // (다) 꼭대기가 빈 판 — 「착용선 하나」로는 안 보이는 실패
            var lowPlate = new List<double[]>
            {
                new[] { -2.0, 0.30, 2.0, 0.30, 2.0, 0.90, -2.0, 0.90 },
            };
            HatCoverage low = Measure(lowPlate);
            Assert.IsTrue(low.Covered, "(다) 아래쪽 판은 자기 높이에서는 덮여야 합니다.");
            Assert.IsFalse(low.StartsAtHeadTop,
                "(다) 머리 꼭대기가 비어 있는데 «꼭대기부터 덮는다»로 읽혔습니다 — " +
                "이 갈래가 죽으면 옛 v1 밀짚모자처럼 «위가 뚫린 모자»가 조용히 통과합니다.");
            Assert.AreEqual(WearLineVerdict.NotFromHeadTop, JudgeWearLine(low),
                "(다) 판정이 NotFromHeadTop이 아닙니다 — 판정 함수가 이 갈래를 안 보고 있습니다.");

            // (라) 잴 것이 없을 때 — 조용한 0이 아니라 «못 쟀다»여야 한다
            HatCoverage nothing = Measure(new List<double[]>());
            Assert.IsFalse(nothing.HasFrontFill, "(라) 빈 목록인데 «잴 것이 있다»로 읽혔습니다.");
            Assert.AreEqual(WearLineVerdict.NoFrontFill, JudgeWearLine(nothing),
                "(라) 앞층 채움이 0개인데 판정이 통과 쪽으로 흘렀습니다 — " +
                "이것이 이 저장소가 반복해 온 «면제 목록이 비어 아무것도 안 재고 초록»입니다.");

            // (마)(바) 판정 갈래 도달성 — 실제 모자로는 절대 안 나오는 두 갈래를 여기서 낸다.
            var floorBreaker = new List<double[]>
            {
                new[] { -2.0, 0.10, 2.0, 0.10, 2.0, 3.0, -2.0, 3.0 },
            };
            Assert.AreEqual(WearLineVerdict.BelowFloor, JudgeWearLine(Measure(floorBreaker)),
                $"(마) 밑변 +0.10인 판이 하한 {WearLineFloorInR:F2}을 깨야 하는데 그렇게 판정되지 않았습니다 — " +
                "하한 갈래가 죽어 있으면 «내려앉은 모자»를 영원히 못 잡습니다.");

            var ceilingBreaker = new List<double[]>
            {
                new[] { -2.0, 0.60, 2.0, 0.60, 2.0, 3.0, -2.0, 3.0 },
            };
            Assert.AreEqual(WearLineVerdict.AboveCeiling, JudgeWearLine(Measure(ceilingBreaker)),
                $"(바) 밑변 +0.60인 판이 상한 {WearLineCeilingInR:F2}을 넘겨야 하는데 그렇게 판정되지 않았습니다 — " +
                "상한 갈래가 죽어 있으면 «얹힌 모자»를 영원히 못 잡습니다.");
        }

        // ====================================================================
        // 5. 회귀 게이트 — HEAD 6종
        // ====================================================================

        /// <summary>모자마다 앞층 채움 조각이 <b>실제로 있는가</b>. 이 단언이 없으면 아래 판정 전부가
        /// 「대상 없이 도는 초록」이 될 수 있다(거짓 통과 유형 5).</summary>
        [TestCase(AccessoryShapeBuilder.HeadCap, TestName = "HEAD 야구모자")]
        [TestCase(AccessoryShapeBuilder.HeadBeanie, TestName = "HEAD 털모자")]
        [TestCase(AccessoryShapeBuilder.HeadFedora, TestName = "HEAD 중절모")]
        [TestCase(AccessoryShapeBuilder.HeadCrown, TestName = "HEAD 왕관")]
        [TestCase(AccessoryShapeBuilder.HeadBeret, TestName = "HEAD 베레모")]
        [TestCase(AccessoryShapeBuilder.HeadStraw, TestName = "HEAD 밀짚모자")]
        public void 모자에_앞층_채움_조각이_실제로_있다(int item)
        {
            List<double[]> polys = FrontFillPolygonsInR(item, out int totalPieces);
            Assert.Greater(totalPieces, 0, $"{Label(item)}가 조각을 하나도 안 만듭니다.");
            Assert.Greater(polys.Count, 0,
                $"{Label(item)}에 앞층 채움 조각(Filled ∧ SortingOrder ≥ SortEyes)이 0개입니다 — " +
                "H-2 / H-2b는 이 조각들로만 재므로, 0개가 되는 순간 아래 검사는 " +
                "<b>대상 없이</b> 돌게 됩니다. 조각이 전부 뒤층으로 갔거나 채움이 꺼졌는지 확인하십시오.");
        }

        /// <summary>★ <b>H-2 착용선이 대역 안인가</b>. 이 라운드에 비어 버린 회귀 감시를 대신하는 본체다.</summary>
        [TestCase(AccessoryShapeBuilder.HeadCap, TestName = "HEAD 야구모자")]
        [TestCase(AccessoryShapeBuilder.HeadBeanie, TestName = "HEAD 털모자")]
        [TestCase(AccessoryShapeBuilder.HeadFedora, TestName = "HEAD 중절모")]
        [TestCase(AccessoryShapeBuilder.HeadCrown, TestName = "HEAD 왕관")]
        [TestCase(AccessoryShapeBuilder.HeadBeret, TestName = "HEAD 베레모")]
        [TestCase(AccessoryShapeBuilder.HeadStraw, TestName = "HEAD 밀짚모자")]
        public void H2_착용선이_대역_안이다(int item)
        {
            List<double[]> polys = FrontFillPolygonsInR(item, out _);
            HatCoverage m = Measure(polys);
            WearLineVerdict verdict = JudgeWearLine(m);

            Debug.Log($"[H-2] {Label(item)} 착용선 {m.WearLineInR:F4} R " +
                      $"(대역 [{WearLineFloorInR:F2}, {WearLineCeilingInR:F2}]) · " +
                      $"밑단 {m.FrontFillBottomInR:F4} R · 꼬리 {m.TailInR:F4} R · " +
                      $"앞층 채움 {polys.Count}개 · 판정 {verdict}.");

            Assert.AreEqual(WearLineVerdict.Pass, verdict,
                $"{Label(item)}의 H-2 착용선이 대역을 벗어났습니다.\n" +
                $"  착용선 = {m.WearLineInR:F4} R · 대역 = [{WearLineFloorInR:F2}, {WearLineCeilingInR:F2}] R · " +
                $"판정 = {verdict}\n" +
                "  · BelowFloor  = 모자가 <b>내려앉았다</b>. 사용자 신고(「안경 착용시 모자들이 가리는 현상이 많았음」)가 " +
                "바로 이것이고, R25가 6종을 올려 고친 결함입니다.\n" +
                "  · AboveCeiling= 모자가 머리를 못 덮고 <b>얹혔다</b>.\n" +
                "  · NotFromHeadTop = 아래에서만 덮는다(머리 꼭대기가 비었다).\n" +
                "  대역의 출처: 상한 +0.45는 docs/EQUIPMENT_HANDOFF_PORT_SPEC.md 규칙 표 H-2(R25 개정), " +
                "하한 +0.30은 design/equipment/verify/r24_hats.py의 H2_BAND 잠정값입니다.\n" +
                "  이 값을 <b>의도적으로</b> 움직였다면 design-equipment가 대역을 다시 정하고 " +
                "이 파일의 상수 한 줄을 그 값으로 바꾸는 것이 순서입니다 — 테스트를 먼저 늘리지 마십시오.");
        }

        /// <summary>덮임이 <b>머리 꼭대기에서 시작</b>하는가. 착용선 값 하나만 보면 안 보이는 실패다
        /// (r24 ①: <i>"구간이 [1.18→0.07]처럼 위가 비어 있어도 아래에서 처음 덮이면 그 값을 「착용선」이라 부른다"</i>).</summary>
        [TestCase(AccessoryShapeBuilder.HeadCap, TestName = "HEAD 야구모자")]
        [TestCase(AccessoryShapeBuilder.HeadBeanie, TestName = "HEAD 털모자")]
        [TestCase(AccessoryShapeBuilder.HeadFedora, TestName = "HEAD 중절모")]
        [TestCase(AccessoryShapeBuilder.HeadCrown, TestName = "HEAD 왕관")]
        [TestCase(AccessoryShapeBuilder.HeadBeret, TestName = "HEAD 베레모")]
        [TestCase(AccessoryShapeBuilder.HeadStraw, TestName = "HEAD 밀짚모자")]
        public void 앞층_채움이_머리_꼭대기부터_연속으로_덮는다(int item)
        {
            List<double[]> polys = FrontFillPolygonsInR(item, out _);
            HatCoverage m = Measure(polys);

            Assert.IsTrue(m.HasFrontFill, $"{Label(item)}에 앞층 채움 조각이 없습니다.");
            Assert.IsTrue(m.Covered,
                $"{Label(item)}의 앞층 채움이 <b>어느 높이에서도</b> 머리 현을 덮지 못합니다 — " +
                "모자를 써도 머리가 그대로 비쳐 보입니다.");
            Assert.IsTrue(m.StartsAtHeadTop,
                $"{Label(item)}의 덮임이 머리 꼭대기({HeadCoverRadiusInR:F4} R)가 아니라 더 아래에서 시작합니다.\n" +
                "착용선 값 하나만 보면 이 실패가 <b>통과처럼 생깁니다</b> — 옛 v1 밀짚모자가 그 경우였습니다.");
        }

        /// <summary>★ <b>H-2b 앞층 채움 밑단</b>이 하한 위인가. <b>가려짐을 실제로 정하는 자</b>다
        /// (밑단 ↔ 최대 가려짐이 단조: +0.3127→30.2% · +0.1330→44.6% · −0.4000→78.4%, 스펙 §14-15-0).</summary>
        [TestCase(AccessoryShapeBuilder.HeadCap, TestName = "HEAD 야구모자")]
        [TestCase(AccessoryShapeBuilder.HeadBeanie, TestName = "HEAD 털모자(승인된 예외)")]
        [TestCase(AccessoryShapeBuilder.HeadFedora, TestName = "HEAD 중절모")]
        [TestCase(AccessoryShapeBuilder.HeadCrown, TestName = "HEAD 왕관")]
        [TestCase(AccessoryShapeBuilder.HeadBeret, TestName = "HEAD 베레모")]
        [TestCase(AccessoryShapeBuilder.HeadStraw, TestName = "HEAD 밀짚모자")]
        public void H2b_앞층_채움_밑단이_하한_위다(int item)
        {
            List<double[]> polys = FrontFillPolygonsInR(item, out _);
            HatCoverage m = Measure(polys);
            double floor = FrontFillBottomFloorFor(item);

            Assert.IsTrue(m.HasFrontFill, $"{Label(item)}에 앞층 채움 조각이 없습니다.");
            Assert.IsFalse(double.IsNaN(m.FrontFillBottomInR),
                $"{Label(item)}의 앞층 채움이 머리 폭 안에 한 번도 닿지 않습니다 — " +
                "모자가 얼굴 옆으로 완전히 비켜났다는 뜻이고, 그 상태로 «가려짐 0»을 통과로 읽으면 안 됩니다.");

            Assert.GreaterOrEqual(m.FrontFillBottomInR, floor - Epsilon,
                $"{Label(item)}의 앞층 채움 밑단 {m.FrontFillBottomInR:F4} R이 하한 {floor:F4} R 아래입니다 " +
                $"(목표 {FrontFillBottomAimInR:F3} R에서 {m.FrontFillBottomInR - FrontFillBottomAimInR:+0.0000;-0.0000} R).\n" +
                "  밑단이 내려갈수록 안경이 지워집니다 — 이 자가 규칙에 없어서 털모자가 " +
                "「착용선 목표 0.00을 −0.044로 통과」하면서 안경을 71.3% 가렸습니다(스펙 §14-15-0).\n" +
                "  꼬리(착용선 − 밑단) = " + m.TailInR.ToString("F4", CultureInfo.InvariantCulture) + " R. " +
                $"꼬리가 {WearLineCeilingInR - FrontFillBottomFloorInR:F3} R보다 길면 " +
                "<b>평행이동으로는 절대 못 맞춥니다</b> — 조형을 고쳐야 합니다(스펙 §14-15-1).");
        }

        // ====================================================================
        // 6. 존재/부재 대조 — 예외가 낡은 채로 조용히 살지 못하게
        // ====================================================================

        /// <summary>★ H-2b 예외가 <b>하나뿐이고 지금도 살아 있는가</b>.
        ///
        /// <para>CLAUDE.md: <i>"부재 단언은 썩으면 <b>조용히 초록</b>이 된다"</i>. 「털모자는 예외」라고만
        /// 적어 두면 나중에 털모자가 고쳐져도 러너는 계속 초록이고, <b>예외 목록만 남는다</b>.
        /// 그래서 여기서 두 방향을 <b>대조로</b> 못박는다.</para>
        /// <list type="number">
        ///   <item><b>존재</b> — 털모자의 밑단은 실제로 일반 하한(+0.28) <b>아래</b>다. 그래서 예외가 필요하다.
        ///         고쳐져서 +0.28을 넘으면 <b>여기서 빨개지고</b>, 그때 할 일은 예외를 지우는 것이다.</item>
        ///   <item><b>부재</b> — 나머지 다섯은 예외 없이 일반 하한을 넘는다. 두 번째 예외가 필요해졌다면
        ///         그것은 조용히 넘어갈 일이 아니라 리더 판정 사항이다(스펙 §14-15-7이 A/B/C를 올린 그 자리).</item>
        /// </list></summary>
        [Test]
        public void H2b_예외는_털모자_하나뿐이고_지금도_살아_있다()
        {
            var below = new List<int>();
            var detail = new StringBuilder();
            double beanieBottom = double.NaN;

            for (int i = 0; i < GatedHeadItems.Length; i++)
            {
                int item = GatedHeadItems[i];
                HatCoverage m = Measure(FrontFillPolygonsInR(item, out _));
                if (item == AccessoryShapeBuilder.HeadBeanie) beanieBottom = m.FrontFillBottomInR;
                if (m.FrontFillBottomInR < FrontFillBottomFloorInR - Epsilon)
                {
                    below.Add(item);
                    detail.Append($" · {Label(item)} {m.FrontFillBottomInR:F4} R");
                }
            }

            // (1) 존재 — 털모자는 <b>지금도</b> 예외가 필요하다.
            Assert.Less(beanieBottom, FrontFillBottomFloorInR,
                $"{Label(AccessoryShapeBuilder.HeadBeanie)}의 밑단 {beanieBottom:F4} R이 " +
                $"일반 하한 {FrontFillBottomFloorInR:F2} R을 <b>넘었습니다</b> — 좋은 소식입니다.\n" +
                "그렇다면 이 예외(BeanieFrontFillBottomFloorInR)는 더 이상 필요 없습니다. <b>지우십시오.</b>\n" +
                "예외가 실효보다 오래 사는 것이 이 저장소가 반복해 온 「부재 단언이 조용히 초록이 되는」 형태입니다.");

            // (2) 부재 — 예외가 필요한 것은 그 하나뿐이다.
            Assert.AreEqual(1, below.Count,
                $"H-2b 일반 하한({FrontFillBottomFloorInR:F2}) 아래로 내려간 모자가 {below.Count}종입니다:{detail}\n" +
                "승인된 예외는 <b>털모자 1건뿐</b>입니다(스펙 §14-15-7 「안 B」 — 리더 판정). " +
                "두 번째가 생겼다면 그것은 회귀이거나, 아니면 리더가 다시 판정해야 할 설계 변경입니다. " +
                "테스트에 예외를 더 넣어 초록으로 만들지 마십시오.");
            Assert.AreEqual(AccessoryShapeBuilder.HeadBeanie, below[0],
                $"일반 하한 아래로 내려간 것이 털모자가 아니라 {Label(below[0])}입니다 — 예외의 주인이 바뀌었습니다.");
        }

        /// <summary>왕관 앵커 — R25가 <b>무변경</b>으로 남긴 기준 대역. 나머지 다섯이 여기로 수렴했으므로
        /// 이 값이 움직였다면 <b>대역 자체가 움직인 것</b>이고, 그러면 다른 다섯의 「통과」도 뜻이 달라진다.
        /// <para>이 골든의 출처는 프로덕션 함수가 아니라 스펙 §14-15-2 표와 <c>r24_hats.out.txt</c>다 —
        /// 그래서 «프로덕션이 틀렸나, 골든이 틀렸나»를 <b>가를 수 있다</b>(TEAM.md 열 번째 형태).</para></summary>
        [Test]
        public void 왕관_앵커가_설계_골든과_같다()
        {
            HatCoverage crown = Measure(FrontFillPolygonsInR(AccessoryShapeBuilder.HeadCrown, out _));

            Assert.AreEqual(CrownWearLineGoldenInR, crown.WearLineInR, CrownGoldenToleranceInR,
                $"왕관 착용선이 {crown.WearLineInR:F4} R입니다 — 설계 골든은 {CrownWearLineGoldenInR:F4} R" +
                "(스펙 §14-15-2 「왕관 … 무변경」 · r24_hats.out.txt).\n" +
                "왕관은 R25가 <b>손대지 않은</b> 기준입니다. 이것이 움직였다면 좌표가 아니라 " +
                "<b>자(尺)나 리그가</b> 움직였을 가능성을 먼저 보십시오 — 그 경우 나머지 다섯의 초록도 무효입니다.");

            Assert.AreEqual(CrownFrontFillBottomGoldenInR, crown.FrontFillBottomInR, CrownGoldenToleranceInR,
                $"왕관 앞층 밑단이 {crown.FrontFillBottomInR:F4} R입니다 — 설계 골든은 " +
                $"{CrownFrontFillBottomGoldenInR:F4} R(스펙 §14-15-2 · H-2b 수렴 목표의 근거값).");
        }

        /// <summary>게이트 표가 HEAD 카탈로그 <b>전부</b>를 덮는가. 7번째 모자가 들어와도 이 자리가 조용히
        /// 6종만 보고 초록이 되지 않게 한다 — 개수를 코드에 적지 않고 카탈로그에서 센다.</summary>
        [Test]
        public void 게이트_표가_HEAD_카탈로그_전부를_덮는다()
        {
            int catalogCount = ItemCatalog.ItemCountIn(EquipmentSlot.Head);
            Assert.Greater(catalogCount, 0, "HEAD 카탈로그가 비었습니다 — 이 게이트가 아무것도 안 재고 있습니다.");
            Assert.AreEqual(catalogCount, GatedHeadItems.Length,
                $"HEAD 카탈로그는 {catalogCount}종인데 이 게이트의 표는 {GatedHeadItems.Length}종입니다.\n" +
                "새 모자를 추가했다면 GatedHeadItems와 위 [TestCase] 목록에 <b>둘 다</b> 넣으십시오 — " +
                "빠진 모자는 착용선을 아무도 보지 않습니다.");

            var seen = new HashSet<int>();
            for (int i = 0; i < GatedHeadItems.Length; i++)
            {
                Assert.IsTrue(seen.Add(GatedHeadItems[i]),
                    $"게이트 표에 HEAD {GatedHeadItems[i]}번이 두 번 들어 있습니다 — " +
                    "개수는 맞는데 실제로는 한 종을 안 보고 있습니다.");
                Assert.Less(GatedHeadItems[i], catalogCount,
                    $"게이트 표의 HEAD {GatedHeadItems[i]}번이 카탈로그 범위 밖입니다.");
            }
        }

        // ====================================================================
        // 7. 양성 대조 — 자가 죽으면 여기가 먼저 빨개진다
        // ====================================================================

        /// <summary>★ <b>고의로 대역 밖으로 옮긴 모자를 이 자가 실제로 잡는가</b>(6종 전부, 매 실행).
        ///
        /// <para>이 저장소가 아홉 번 당한 형태는 <i>"실패한 측정과 성공한 측정이 똑같이 생겼다"</i>였다.
        /// 좌표가 그대로인 한 위 검사들은 <b>자가 죽어도 초록</b>이다.</para>
        ///
        /// <para>미는 폭 <c>±0.20 R</c>은 대역 폭(0.15 R)보다 크다. 이 값에서 6종이 어떻게 되는지는
        /// 클래스 문서의 실측 표에 있다 — <b>위로</b>는 6종 전부 <see cref="WearLineVerdict.AboveCeiling"/>이지만
        /// <b>아래로</b>는 사유가 갈린다(야구모자·중절모·왕관은 구간이 끊겨 <c>AboveCeiling</c>,
        /// 털모자·베레모·밀짚모자는 <c>BelowFloor</c>). 그래서 아래 방향은 <b>사유를 못박지 않고</b>
        /// 「통과가 아니다」만 요구한다 — 사유를 못박았던 첫 판이 3종에서 거짓 빨강을 냈다.</para>
        ///
        /// <para>프로덕션 배열은 <see cref="ShiftedCopy"/>가 <c>Clone</c>으로 복제하므로 건드리지 않는다.</para></summary>
        [Test]
        public void 양성_대조_대역_밖으로_옮긴_모자는_실제로_빨개진다()
        {
            double push = (WearLineCeilingInR - WearLineFloorInR) + 0.05;   // 0.20 R
            var report = new StringBuilder();

            for (int i = 0; i < GatedHeadItems.Length; i++)
            {
                int item = GatedHeadItems[i];
                List<double[]> polys = FrontFillPolygonsInR(item, out _);

                WearLineVerdict asIs = JudgeWearLine(Measure(polys));
                HatCoverage down = Measure(ShiftedCopy(polys, -push));
                HatCoverage up = Measure(ShiftedCopy(polys, +push));
                WearLineVerdict downVerdict = JudgeWearLine(down);
                WearLineVerdict upVerdict = JudgeWearLine(up);

                report.AppendLine($"  {Label(item)}: 그대로={asIs} · 아래로 {push:F2}R={downVerdict}" +
                                  $"(밑단 {down.FrontFillBottomInR:F4}) · 위로 {push:F2}R={upVerdict}");

                Assert.AreEqual(WearLineVerdict.Pass, asIs,
                    $"{Label(item)}가 손대지 않은 상태에서 이미 실패입니다 — " +
                    "양성 대조를 하기 전에 그 회귀부터 보십시오(H2_착용선이_대역_안이다가 자세히 말합니다).");

                Assert.AreNotEqual(WearLineVerdict.Pass, downVerdict,
                    $"{Label(item)}를 <b>아래로 {push:F2} R</b> 내렸는데 판정이 여전히 통과입니다 — " +
                    "자가 좌표를 안 보고 있습니다. 위 6종의 초록은 전부 무효입니다.");

                Assert.AreEqual(WearLineVerdict.AboveCeiling, upVerdict,
                    $"{Label(item)}를 <b>위로 {push:F2} R</b> 올렸는데 사유가 {upVerdict}입니다 — " +
                    "상한 초과여야 합니다(실측: 6종 전부 +0.20 R에서 착용선 +0.52~+0.62). " +
                    "대역의 상한 쪽(모자가 얹힌다)이 죽었거나, 도형이 크게 바뀌었습니다.");
            }

            Debug.Log("[H-2 양성 대조 ±" + push.ToString("F2", CultureInfo.InvariantCulture) + " R]\n" + report);
        }

        /// <summary>★ <b>H-2b가 「내려앉은 모자」를 실제로 잡는가</b> — 이 게이트의 <b>본체</b>다.
        ///
        /// <para>클래스 문서의 실측대로, 밑단은 평행이동에 <b>정확히 비례</b>한다. 그래서 두 가지를 못박는다.</para>
        /// <list type="number">
        ///   <item><b>비례성</b> — Δ만큼 내리면 밑단도 Δ만큼(눈금 한 칸 안에서) 내려간다.
        ///         이것이 「자가 좌표를 진짜로 읽고 있다」의 가장 직접적인 증거다.</item>
        ///   <item><b>민감도</b> — <b>−0.05 R</b>만 내려도 6종 전부 자기 하한을 깬다.
        ///         실측 기준값: 왕관 +0.3152 → +0.265(하한 0.28) · 털모자 +0.2596 → +0.210(예외 하한 0.2556).
        ///         착용선 대역은 이 폭에서 6종 중 여럿이 <b>여전히 통과</b>한다 —
        ///         <b>이 검사가 아니면 그 회귀는 아무도 못 잡는다.</b></item>
        /// </list></summary>
        [Test]
        public void 양성_대조_모자를_조금만_내려도_H2b가_잡는다()
        {
            const double smallDrop = 0.05;
            var report = new StringBuilder();

            for (int i = 0; i < GatedHeadItems.Length; i++)
            {
                int item = GatedHeadItems[i];
                List<double[]> polys = FrontFillPolygonsInR(item, out _);
                double floor = FrontFillBottomFloorFor(item);

                HatCoverage asIs = Measure(polys);
                HatCoverage dropped = Measure(ShiftedCopy(polys, -smallDrop));

                report.AppendLine($"  {Label(item)}: 밑단 {asIs.FrontFillBottomInR:F4} → " +
                                  $"{dropped.FrontFillBottomInR:F4} (하한 {floor:F4})");

                // (1) 비례성 — 좌표를 실제로 읽고 있다는 직접 증거.
                double moved = asIs.FrontFillBottomInR - dropped.FrontFillBottomInR;
                Assert.AreEqual(smallDrop, moved, ScanStepInR,
                    $"{Label(item)}를 {smallDrop:F2} R 내렸는데 밑단은 {moved:F4} R만 움직였습니다 — " +
                    "밑단 자가 좌표를 안 읽고 있거나, 다른 무언가에 고정돼 있습니다.");

                // (2) 민감도 — 이 폭이면 반드시 하한을 깬다.
                Assert.Less(dropped.FrontFillBottomInR, floor,
                    $"{Label(item)}를 {smallDrop:F2} R 내렸는데 밑단 {dropped.FrontFillBottomInR:F4} R이 " +
                    $"여전히 하한 {floor:F4} R 위입니다 — H-2b가 「모자가 내려앉아 안경을 가리는 것」을 " +
                    "못 잡고 있다는 뜻이고, 이 게이트 전체가 무의미해집니다.");
            }

            Debug.Log("[H-2b 양성 대조 −" + smallDrop.ToString("F2", CultureInfo.InvariantCulture) + " R]\n" + report);
        }

        /// <summary>꼬리(착용선 − 밑단)가 음수가 아닌가. 두 자가 <b>같은 도형을 재고 있다</b>는 불변식이다 —
        /// 착용선 높이에서는 중앙이 덮여 있으므로 밑단은 그보다 아래이거나 같아야 한다.
        /// <para>어긋나면 둘 중 하나가 다른 조각 집합을 보고 있다는 뜻이고, 그 순간 두 판정은 서로 무관해진다.</para>
        /// <para>꼬리는 스펙 §14-15-1의 진단자이기도 하다 — (상한 − 하한)보다 길면 그 모자는
        /// <b>평행이동으로는 대역에 못 들어간다</b>. 그건 실패가 아니라 <b>조형 과제</b>라 로그로만 남긴다.</para></summary>
        [Test]
        public void 꼬리는_음수가_아니다_그리고_길면_로그로_남긴다()
        {
            double reachableTail = WearLineCeilingInR - FrontFillBottomFloorInR;   // 0.170 R
            var report = new StringBuilder();

            for (int i = 0; i < GatedHeadItems.Length; i++)
            {
                int item = GatedHeadItems[i];
                HatCoverage m = Measure(FrontFillPolygonsInR(item, out _));

                Assert.GreaterOrEqual(m.TailInR, -Epsilon,
                    $"{Label(item)}의 꼬리가 {m.TailInR:F4} R로 음수입니다 " +
                    $"(착용선 {m.WearLineInR:F4} · 밑단 {m.FrontFillBottomInR:F4}).\n" +
                    "착용선 높이에서는 중앙이 덮여 있으므로 밑단은 그보다 아래여야 합니다 — " +
                    "두 자가 <b>서로 다른 조각 집합</b>을 보고 있습니다.");

                report.AppendLine($"  {Label(item)}: 꼬리 {m.TailInR:F4} R" +
                                  (m.TailInR > reachableTail
                                      ? $"  ★ {reachableTail:F3} R보다 길다 — 평행이동으로는 대역에 못 들어간다(조형 과제)"
                                      : string.Empty));
            }

            Debug.Log($"[꼬리 = 착용선 − 밑단 · 도달 가능 상한 {reachableTail:F3} R]\n" + report);
        }
    }
}
