using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Interaction;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 카드 <b>폴백</b> 아이콘이 <b>지금의 몸 도형</b>과 같은 물건인가 — 30종 전수
    /// (2026-09-01 설정 드리프트 라운드 임무 B).
    ///
    /// ============================================================================
    /// 왜 이 파일이 필요했나 — <b>거짓말하는 테스트</b>를 발견했다
    /// ============================================================================
    /// <see cref="AccessoryFallbackIconParityTests"/>의 문서는 이렇게 선언한다:
    /// <i>"비교는 언제나 '몸이 지금 뭐라고 말하는가'에 건다. 폴백에 숫자를 새로 적어 두면 몸이 다시
    /// 바뀌는 날 폴백만 옛 그림으로 남는다."</i>
    /// <b>그런데 구현이 그렇지 않았다.</b> 베레모 검사는 <c>crown = icon[0]</c>과
    /// <c>rim = Accent(icon)</c>을 비교했는데 <b>둘 다 에셋에서 온 폴백 조각</b>이라,
    /// <see cref="AccessoryShapeBuilder"/>를 한 번도 읽지 않았다. 폴백을 폴백과 대조한 것이다.
    ///
    /// <para>그 결과 <c>Assert.AreEqual(4, rim.Values.Length)</c>(= 2점 직선)가 <b>갈라진 상태를
    /// 잠그고 있었다</b> — 몸의 <c>BeretRim</c>은 이미 <b>3점</b>
    /// (<c>frontFoot → innerFoot → backTip</c>, 밑변 전체)인데도. 이건 실패한 테스트가 아니라
    /// <b>장비 담당이 몸을 고칠 때마다 "고치지 마라"고 막는 테스트</b>였다.</para>
    ///
    /// ============================================================================
    /// 무엇을 몸에서 유도하는가 (30종 = 5슬롯 × 6)
    /// ============================================================================
    /// 폴백은 40×40 격자의 <b>단순화</b>다. 그래서 "좌표가 같아야 한다"로 잠그면 안 된다.
    /// 단순화해도 <b>변하면 안 되는 것</b> 둘만 몸에서 읽어 대조한다.
    /// <list type="number">
    ///   <item><b>보조색 조각 수</b>(규칙 3-2: 아이템당 정확히 1개). 그 한 조각이 아이템의 <b>식별
    ///         특징</b>이다. 1개를 2개로 쪼개는 것은 단순화가 아니라 <b>다른 그림</b>이다.
    ///         몸이 실제로 1개라는 사실도 여기서 함께 확인한다(가정하지 않는다).</item>
    ///   <item><b>보조색 조각의 꼭짓점 수</b>. 베레모 사고가 정확히 이 축이었다. 폴백의 보조색이
    ///         원/점 같은 <b>다른 원시도형</b>이면 꼭짓점 개념이 없으므로 이 검사에서 빠진다
    ///         (털모자 폼폼 Ring, 방울 Dot).</item>
    /// </list>
    ///
    /// ============================================================================
    /// 대장 — 못 맞추는 것은 <b>사유와 실측값</b>과 함께 등재한다
    /// ============================================================================
    /// 폴백 에셋(<c>Resources/Items/*.asset</c>)을 새로 굽는 것은 <b>장비 담당 소유</b>다.
    /// 테스트가 할 일은 "지금 어디가 갈라졌는지"를 숫자로 못 박아 넘기는 것까지다. 그래서 이 라운드가
    /// 세운 어법(<see cref="ConfigAssetDriftLedgerTests"/> 의도된 차이 대장 /
    /// <see cref="AccessoryRuleOneCoverageTests"/> 면제 대장)을 그대로 쓴다.
    /// <list type="bullet">
    ///   <item>새 아이템/새 축은 <b>기본이 검사</b>다. 아무것도 안 적으면 잡힌다.</item>
    ///   <item>대장은 <b>스스로 만료된다</b> — 고쳐졌는데 줄이 남으면 빨간불이다
    ///         (<see cref="대장_항목은_지금도_실제로_어긋난다"/>).</item>
    ///   <item>대장 줄은 <b>양쪽 실측값을 박제</b>한다. 몸이 다시 움직이면 그 줄이 다시 물어본다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 판정 — 22건 중 무엇이 결함이고 무엇이 정당한 단순화인가 (2026-09-01 실측)
    /// ============================================================================
    /// <b>결함 9건 — 보조색 조각을 쪼갰다.</b> 규칙 3-2가 "정확히 1개"라고 못 박은 축이라 단순화로
    /// 볼 수 없다. 폴백이 뜨는 순간 <b>다른 아이템</b>이 그려진다.
    ///
    /// <b>결함 3건 — 폴백이 몸보다 <i>복잡</i>하다.</b> 단순화의 정의상 있을 수 없는 방향이다.
    /// 셋 다 HAIR이고, 특히 곱슬머리는 몸 5점 → 폴백 16점으로 <b>몸에 없는 곱슬</b>을 그린다.
    ///
    /// <para>★ 처음엔 이 칸이 6건이었다. 나비넥타이·펜던트·반다나 세 건은 <b>유령</b>이었다 —
    /// 닫힌 폴백이 첫 점을 되풀이하는 규약을 정규화하지 않아 1점씩 더 많아 보였을 뿐,
    /// 실제로는 몸과 정확히 일치한다. 반대로 <b>포니테일</b>은 정규화 후에야 6↔5로 드러났다.
    /// 세는 규약이 다르면 <b>없는 결함을 만들고 있는 결함을 가린다</b>는 사례다.</para>
    ///
    /// <b>단순화로 승인 8건 — 폴백이 몸보다 단순하다</b>(꼭짓점이 줄기만 했다). 40×40 격자에서
    /// 곡선을 펴는 것은 폴백의 목적에 부합한다. <b>다만 베레모만은 예외로 결함으로 본다</b> —
    /// 줄어든 그 한 점이 <c>innerFoot</c>(밑변의 안쪽 꺾임)이고, 그것이 베레모를 "띠 두른 정모"와
    /// 가르는 <b>정체</b>이기 때문이다(그 라운드의 결론이 도형 주석에 그대로 적혀 있다).
    /// 나머지 6건(왕관·선글라스·뿔테·짧은망토·판초·민머리)은 <b>리더 판단 대기</b>로 남긴다.
    ///
    /// <para>어느 쪽이든 <b>지금은 전부 대장에 있어 초록</b>이다. 대장은 "괜찮다"는 뜻이 아니라
    /// <b>"알고 있고, 아직 안 고쳤다"</b>는 뜻이다.</para>
    ///
    /// ============================================================================
    /// 네거티브 컨트롤 — 이번에 잡은 게 정확히 "항상 참인 단언"이다
    /// ============================================================================
    /// <see cref="네거티브_컨트롤_비교기는_폴백이_아니라_몸을_읽는다"/>가 <b>몸 쪽만</b> 합성으로
    /// 바꿔 결과가 달라지는지 본다. 비교기가 몸 인자를 무시하면(= 옛 버그) 그 테스트가 빨개진다.
    ///
    /// <para><b>플랫폼</b>: 플랫폼 중립. 이 테스트는 도형과 에셋을 <b>읽기만</b> 한다.</para>
    /// </summary>
    public sealed class AccessoryFallbackBodyParityTests
    {
        private const string LogPrefix = "[폴백-몸]";

        /// <summary>몸 도형이 있는 자리 = <see cref="AccessoryShapeBuilder"/>가 아는 자리.
        /// FX/PET은 <c>AppearanceShapeBuilder</c> 소관이라 이 검사의 대상이 아니다
        /// (<see cref="AccessoryRuleOneCoverageTests"/>와 같은 경계).</summary>
        private static readonly EquipmentSlot[] BodySlots =
        {
            EquipmentSlot.Head, EquipmentSlot.Eyes, EquipmentSlot.Neck,
            EquipmentSlot.Shoulders, EquipmentSlot.Hair,
        };

        private const int ItemsPerSlot = 6;

        internal enum Axis
        {
            /// <summary>보조색 조각 <b>수</b>가 몸과 다르다(규칙 3-2 위반이기도 하다).</summary>
            AccentPartCount,
            /// <summary>보조색 조각의 <b>꼭짓점 수</b>가 몸과 다르다(베레모 사고의 축).</summary>
            AccentVertexCount,
        }

        // ============================================================================
        // ★ 대장
        // ============================================================================

        /// <summary>
        /// 아직 몸과 갈라져 있는 폴백. <b>한 줄이 곧 하나의 빚</b>이고, 실측값 두 개를 함께 박제한다.
        /// <para><b>고칠 때 할 일</b>: 폴백 에셋을 몸에 맞춰 다시 굽고 <b>여기서 줄을 지운다</b>.
        /// 안 지우면 <see cref="대장_항목은_지금도_실제로_어긋난다"/>가 빨개진다.</para>
        /// <para><b>몸이 바뀌었을 때</b>: 박제한 몸 실측값이 안 맞으면 그 줄이 빨개진다 —
        /// "이 빚이 아직 같은 빚인지" 다시 판정하라는 뜻이다.</para>
        /// </summary>
        private static readonly Debt[] Ledger =
        {
            // ★ 2026-09-02 — <b>비었다.</b> 20줄 전부 한 번에 갚혔다.
            //
            // 방법은 좌표를 21번 손으로 고치는 것이 아니라 <b>한 번 유도</b>하는 것이었다:
            // 30종 폴백을 카드 본경로(AccessoryCardIcon.TryBuild)와 <b>같은 투영식</b>으로
            // 몸 도형에서 구워 Resources/Items/*.asset에 눕혔다. 그래서 축1(보조색 조각 수)과
            // 축2(보조색 꼭짓점 수)가 <b>정의상</b> 몸과 같아졌다 — 갈라질 자리가 없다.
            // 함께 닫힌 것: 베레모 테 3점, 펜던트 종횡비(2.2143 -> 몸과 같은 2.1333),
            // 그리고 "폴백은 속이 빈 윤곽선"이라는 근본 한계
            // (ItemIconPartKind.Polygon이 생겨 채운 면을 표현한다).
            //
            // 비어 있다는 것이 "검사가 잠들었다"는 뜻이 아니다 — 기본이 <b>검사</b>이므로
            // 새 갈라짐은 아무것도 안 적어도 아래 첫 테스트가 잡는다. 못 고칠 것이 다시 생기면
            // 실측값 두 개와 사유를 달아 여기 등재한다:
            //     new Debt(슬롯, 번호, Axis.…, 몸실측, 폴백실측, "왜 아직 못 고쳤는가")
        };

        internal readonly struct Debt
        {
            public readonly EquipmentSlot Slot;
            public readonly int Item;
            public readonly Axis Axis;
            public readonly int BodyPin;
            public readonly int FallbackPin;
            public readonly string Reason;

            public Debt(EquipmentSlot slot, int item, Axis axis, int bodyPin, int fallbackPin, string reason)
            {
                Slot = slot;
                Item = item;
                Axis = axis;
                BodyPin = bodyPin;
                FallbackPin = fallbackPin;
                Reason = reason;
            }

            public override string ToString() => $"{Slot} {Item}번 [{Axis}]";
        }

        // ============================================================================
        // ★ 비교기 — 순수 함수. 실제 데이터와 네거티브 컨트롤이 <b>같은 함수</b>를 쓴다.
        // ============================================================================

        internal readonly struct Gap
        {
            public readonly Axis Axis;
            public readonly int Body;
            public readonly int Fallback;
            public readonly string Detail;

            public Gap(Axis axis, int body, int fallback, string detail)
            {
                Axis = axis;
                Body = body;
                Fallback = fallback;
                Detail = detail;
            }
        }

        /// <summary>
        /// ★ <b>몸이 지금 뭐라고 말하는가</b>를 기준으로 폴백을 잰다.
        /// <para>두 인자는 반드시 <b>서로 다른 출처</b>여야 한다 — 몸은
        /// <see cref="AccessoryShapeBuilder"/>, 폴백은 에셋. 옛 검사는 둘 다 에셋에서 받아
        /// 자기 자신을 대조했다.</para>
        /// </summary>
        internal static List<Gap> Compare(IList<AccessoryShapeBuilder.Shape> body, ItemIconPart[] fallback)
        {
            var gaps = new List<Gap>();

            var bodyAccents = new List<AccessoryShapeBuilder.Shape>();
            for (int i = 0; i < body.Count; i++)
            {
                if (body[i].Tone == 1) bodyAccents.Add(body[i]);
            }

            var fallbackAccents = new List<ItemIconPart>();
            for (int i = 0; i < fallback.Length; i++)
            {
                if (fallback[i].Tone == 1) fallbackAccents.Add(fallback[i]);
            }

            if (bodyAccents.Count != fallbackAccents.Count)
            {
                gaps.Add(new Gap(Axis.AccentPartCount, bodyAccents.Count, fallbackAccents.Count,
                    $"보조색 조각이 몸 {bodyAccents.Count}개 / 폴백 {fallbackAccents.Count}개입니다. " +
                    "그 한 조각이 아이템의 식별 특징이라(규칙 3-2) 쪼개면 다른 아이템이 됩니다."));
                return gaps;   // 개수가 다르면 꼭짓점 비교는 의미가 없다.
            }

            if (bodyAccents.Count != 1) return gaps;

            // 폴백 보조색이 원/점이면 꼭짓점 개념이 없다 — 이 축의 대상이 아니다.
            // ★ 2026-09-02: Polygon(채운 다각형)이 생겼다. <b>이건 꼭짓점을 가진다</b> —
            //   여기서 빠뜨리면 폼폼·방울·매듭처럼 채운 보조색을 가진 아이템에서 이 축이
            //   통째로 침묵한다(대장을 닫자마자 검사가 조용해지는, 가장 위험한 형태의 거짓 초록).
            if (!fallbackAccents[0].HasPoints) return gaps;

            int bodyVerts = bodyAccents[0].Points.Length;
            int fallbackVerts = DistinctPointCount(fallbackAccents[0]);
            if (bodyVerts != fallbackVerts)
            {
                gaps.Add(new Gap(Axis.AccentVertexCount, bodyVerts, fallbackVerts,
                    $"보조색 '{bodyAccents[0].Name}'의 꼭짓점이 몸 {bodyVerts}점 / 폴백 {fallbackVerts}점입니다." +
                    (fallbackVerts > bodyVerts
                        ? " 폴백이 몸보다 <b>복잡합니다</b> — 단순화의 정의상 있을 수 없는 방향입니다."
                        : " 폴백이 더 단순합니다 — 줄어든 점이 아이템의 정체가 아닌지 확인하십시오.")));
            }

            return gaps;
        }

        /// <summary>
        /// ★ 폴백 꺾은선의 <b>서로 다른</b> 점 수. 닫힌 도형이면 마지막 점이 첫 점과 같다는 것이
        /// <see cref="ItemIconPartKind.Polyline"/>의 문서화된 규약이고, 몸의
        /// <see cref="AccessoryShapeBuilder.Shape"/>는 <c>Loop</c> 플래그로 같은 뜻을 나타내며
        /// 첫 점을 <b>되풀이하지 않는다</b>. 두 규약을 그대로 세면 닫힌 도형마다 폴백이 1점 더
        /// 많아 보인다.
        /// <para>★ 2026-09-01 실측: 이 정규화 없이 세는 바람에 <b>나비넥타이·펜던트·반다나</b> 세 건이
        /// "폴백이 몸보다 복잡하다"는 <b>유령 결함</b>으로 대장에 올라갔었다(셋 다 실제로는 정확히
        /// 일치한다). 반대로 <b>포니테일</b>은 정규화하니 비로소 6↔5로 갈라진 것이 드러났다 —
        /// 정규화가 없으면 잡아야 할 것을 놓치기도 한다.</para>
        /// </summary>
        internal static int DistinctPointCount(ItemIconPart part)
        {
            int n = part.PointCount;
            if (n < 2) return n;

            float[] v = part.Values;
            bool closed = Mathf.Approximately(v[0], v[(n - 1) * 2])
                       && Mathf.Approximately(v[1], v[(n - 1) * 2 + 1]);
            return closed ? n - 1 : n;
        }

        // ============================================================================
        // 수집
        // ============================================================================

        private static List<AccessoryShapeBuilder.Shape> BodyShapes(EquipmentSlot slot, int item)
        {
            var sink = new List<AccessoryShapeBuilder.Shape>();
            AccessoryShapeBuilder.Append(sink, slot, item, AccessorySilhouetteMetrics.Rig());
            return sink;
        }

        private static ItemIconPart[] Fallback(EquipmentSlot slot, int item)
        {
            ItemIconPart[] icon = ItemCatalog.Item(slot, item).Icon;
            Assert.IsNotNull(icon, $"{LogPrefix} {Label(slot, item)}의 폴백 아이콘이 사라졌습니다.");
            Assert.Greater(icon.Length, 0, $"{LogPrefix} {Label(slot, item)}의 폴백 아이콘이 비었습니다.");
            return icon;
        }

        private static string Label(EquipmentSlot slot, int item)
            => $"{slot} {item}번({ItemCatalog.Item(slot, item).DisplayName})";

        private static bool TryFindDebt(EquipmentSlot slot, int item, Axis axis, out Debt found)
        {
            for (int i = 0; i < Ledger.Length; i++)
            {
                if (Ledger[i].Slot == slot && Ledger[i].Item == item && Ledger[i].Axis == axis)
                {
                    found = Ledger[i];
                    return true;
                }
            }
            found = default;
            return false;
        }

        // ============================================================================
        // 1. 본 검사 — 대장에 없는 갈라짐이 하나도 없다
        // ============================================================================

        [Test]
        public void 대장에_없는_갈라짐이_하나도_없다()
        {
            var failures = new List<string>();
            int checkedItems = 0;
            int checkedAxes = 0;

            foreach (EquipmentSlot slot in BodySlots)
            {
                for (int item = 0; item < ItemsPerSlot; item++)
                {
                    checkedItems++;
                    List<Gap> gaps = Compare(BodyShapes(slot, item), Fallback(slot, item));
                    checkedAxes += gaps.Count;

                    foreach (Gap gap in gaps)
                    {
                        if (TryFindDebt(slot, item, gap.Axis, out _)) continue;
                        failures.Add($"  · {Label(slot, item)}: {gap.Detail}");
                    }
                }
            }

            Debug.Log($"{LogPrefix} {checkedItems}종 대조 — 갈라진 축 {checkedAxes}개 / 대장 {Ledger.Length}줄.");

            Assert.IsEmpty(failures,
                $"{LogPrefix} 폴백 아이콘이 몸 도형과 갈라졌는데 대장에 없는 항목이 {failures.Count}건입니다.\n" +
                string.Join("\n", failures) +
                "\n\n무엇을 하면 되는가 — 두 갈래 중 하나입니다.\n" +
                "  (가) 몸이 옳다면 → <b>폴백 에셋을 다시 구우십시오</b>(Resources/Items/*.asset).\n" +
                "       폴백은 '새 경로가 통째로 틀렸을 때 뜨는 안전망'입니다. 안전망이 틀린 그림이면\n" +
                "       안전망이 아닙니다(실제 사고: 삭제된 방울 추가 폴백에 남아 있었다).\n" +
                "  (나) 지금 못 고친다면 → 위 Ledger에 <b>실측값 두 개와 사유</b>를 적어 등재하십시오.\n" +
                "         new Debt(슬롯, 번호, Axis.…, 몸실측, 폴백실측, \"왜 아직 못 고쳤는가\")\n" +
                "       ※ 대장은 '괜찮다'가 아니라 '알고 있고 아직 안 고쳤다'는 뜻입니다.\n" +
                "       ※ 고치면 그 줄을 <b>반드시 지우십시오</b> — 안 지우면 대장이 다음 갈라짐을 덮습니다.");
        }

        // ============================================================================
        // 2. 대장이 스스로 낡지 않는다
        // ============================================================================

        [Test]
        public void 대장_항목은_지금도_실제로_어긋난다()
        {
            var stale = new List<string>();

            foreach (Debt debt in Ledger)
            {
                List<Gap> gaps = Compare(BodyShapes(debt.Slot, debt.Item), Fallback(debt.Slot, debt.Item));

                Gap? match = null;
                foreach (Gap g in gaps)
                {
                    if (g.Axis == debt.Axis) match = g;
                }

                if (match == null)
                {
                    stale.Add($"  · {debt}: 지금은 어긋나지 않습니다 — 누군가 이미 고쳤습니다. " +
                              $"대장에서 이 줄을 지우십시오. (사유였던 것: {debt.Reason})");
                    continue;
                }

                if (match.Value.Body != debt.BodyPin || match.Value.Fallback != debt.FallbackPin)
                {
                    stale.Add($"  · {debt}: 대장에 박제된 실측값(몸 {debt.BodyPin} / 폴백 {debt.FallbackPin})과 " +
                              $"지금 값(몸 {match.Value.Body} / 폴백 {match.Value.Fallback})이 다릅니다. " +
                              "★ 몸이 움직였다면 이 빚이 아직 같은 빚인지 다시 판정하고 핀을 갱신하십시오.");
                }
            }

            Assert.IsEmpty(stale,
                $"{LogPrefix} 대장이 낡았습니다 ({stale.Count}건).\n" + string.Join("\n", stale) +
                "\n대장은 '지금 살아 있는 빚'만 담습니다. 고쳐진 줄을 남겨 두면 그 아이템에서 일어나는 " +
                "<b>다음</b> 갈라짐을 조용히 덮습니다.");
        }

        [Test]
        public void 대장이_같은_축을_두_번_등재하지_않는다()
        {
            var seen = new HashSet<string>();
            foreach (Debt debt in Ledger)
            {
                Assert.IsTrue(seen.Add($"{debt.Slot}/{debt.Item}/{debt.Axis}"),
                    $"{LogPrefix} 대장에 {debt}이 두 번 있습니다 — 뒤 줄이 앞 줄을 조용히 덮습니다.");
            }
        }

        // ============================================================================
        // 3. 몸 쪽 전제 — 가정하지 않고 확인한다
        // ============================================================================

        [Test]
        public void 모든_아이템의_몸_보조색은_정확히_한_조각이다()
        {
            // 규칙 3-2. 위 비교기가 "몸이 1개"를 전제로 꼭짓점을 재므로, 그 전제를 여기서 못 박는다.
            foreach (EquipmentSlot slot in BodySlots)
            {
                for (int item = 0; item < ItemsPerSlot; item++)
                {
                    List<AccessoryShapeBuilder.Shape> body = BodyShapes(slot, item);
                    Assert.Greater(body.Count, 0, $"{LogPrefix} {Label(slot, item)}: 몸 도형이 하나도 없습니다.");

                    int accents = 0;
                    for (int i = 0; i < body.Count; i++)
                    {
                        if (body[i].Tone == 1) accents++;
                    }

                    Assert.AreEqual(1, accents,
                        $"{LogPrefix} {Label(slot, item)}의 몸 보조색 조각이 {accents}개입니다(규칙 3-2: 정확히 1개). " +
                        "이 전제가 깨지면 폴백 대조가 무엇을 재는지 알 수 없게 됩니다.");
                }
            }
        }

        // ============================================================================
        // 4. ★ 네거티브 컨트롤 — 비교기가 정말로 <b>몸</b>을 읽는가
        // ============================================================================

        private static AccessoryShapeBuilder.Shape Fake(string name, int points, byte tone)
        {
            var pts = new Vector3[points];
            for (int i = 0; i < points; i++) pts[i] = new Vector3(i, 0f, 0f);
            return new AccessoryShapeBuilder.Shape(name, pts, false, 10, tone: tone);
        }

        private static ItemIconPart FakePart(int points, byte tone)
        {
            var values = new float[points * 2];
            for (int i = 0; i < points; i++) values[i * 2] = i;
            var part = new ItemIconPart(ItemIconPartKind.Polyline, values);
            return tone == 1 ? part.AsSecondary() : part;
        }

        [Test]
        public void 네거티브_컨트롤_비교기는_폴백이_아니라_몸을_읽는다()
        {
            // ★ 옛 버그(폴백을 폴백과 대조)를 이 컨트롤이 직접 막는다.
            //   폴백을 고정한 채 <b>몸만</b> 바꿔서 결과가 달라지는지 본다.
            //   비교기가 몸 인자를 무시하면(= 옛 버그) 두 결과가 같아져 이 테스트가 빨개진다.
            ItemIconPart[] fallback = { FakePart(3, 0), FakePart(2, 1) };

            var bodyMatching = new List<AccessoryShapeBuilder.Shape> { Fake("Main", 3, 0), Fake("Accent", 2, 1) };
            var bodyDiffering = new List<AccessoryShapeBuilder.Shape> { Fake("Main", 3, 0), Fake("Accent", 3, 1) };

            Assert.IsEmpty(Compare(bodyMatching, fallback),
                "몸과 폴백이 같은데 갈라짐이 보고됐습니다 — 비교기가 과민합니다.");

            List<Gap> gaps = Compare(bodyDiffering, fallback);
            Assert.AreEqual(1, gaps.Count,
                "폴백은 그대로 두고 <b>몸만</b> 3점으로 바꿨는데 갈라짐이 안 잡혔습니다 — " +
                "비교기가 몸을 읽지 않고 있습니다(이번에 잡은 옛 버그가 바로 그것입니다).");
            Assert.AreEqual(Axis.AccentVertexCount, gaps[0].Axis);
            Assert.AreEqual(3, gaps[0].Body);
            Assert.AreEqual(2, gaps[0].Fallback);
        }

        [Test]
        public void 네거티브_컨트롤_보조색_조각을_쪼개면_잡힌다()
        {
            var body = new List<AccessoryShapeBuilder.Shape> { Fake("Main", 3, 0), Fake("Accent", 2, 1) };
            ItemIconPart[] split = { FakePart(3, 0), FakePart(2, 1), FakePart(2, 1) };

            List<Gap> gaps = Compare(body, split);
            Assert.AreEqual(1, gaps.Count);
            Assert.AreEqual(Axis.AccentPartCount, gaps[0].Axis);
            Assert.AreEqual(1, gaps[0].Body);
            Assert.AreEqual(2, gaps[0].Fallback);
        }

        [Test]
        public void 네거티브_컨트롤_원_점_보조색은_꼭짓점_축에서_빠진다()
        {
            // 털모자 폼폼(Ring) / 방울(Dot)이 이 경로다. 꼭짓점 개념이 없는 원시도형을
            // 점 수로 재면 영원히 빨간 검사가 되고, 그러면 아무도 안 본다.
            var body = new List<AccessoryShapeBuilder.Shape> { Fake("Main", 3, 0), Fake("Accent", 10, 1) };
            ItemIconPart[] ringAccent =
            {
                FakePart(3, 0),
                new ItemIconPart(ItemIconPartKind.Ring, new[] { 20f, 20f, 5f }).AsSecondary(),
            };

            Assert.IsEmpty(Compare(body, ringAccent),
                "원/점 보조색을 꼭짓점 수로 재고 있습니다 — 비교 대상이 아닙니다.");
        }

        [Test]
        public void 네거티브_컨트롤_닫힌_폴백은_첫_점_되풀이를_빼고_센다()
        {
            // ★ 이 정규화가 없으면 <b>없는 결함을 만들고</b>(나비넥타이·펜던트·반다나)
            //   <b>있는 결함을 가린다</b>(포니테일). 두 방향을 다 잠근다.
            var body = new List<AccessoryShapeBuilder.Shape> { Fake("Main", 3, 0), Fake("Accent", 4, 1) };

            // 같은 4각형을 "닫힌 꺾은선"(첫 점 되풀이)으로 적은 폴백 — 갈라진 것이 아니다.
            var closed = new ItemIconPart(ItemIconPartKind.Polyline,
                new[] { 0f, 0f, 1f, 0f, 1f, 1f, 0f, 1f, 0f, 0f });
            Assert.AreEqual(5, closed.PointCount, "이 합성 폴백은 되풀이 점을 포함해 5점이어야 합니다.");
            Assert.AreEqual(4, DistinctPointCount(closed), "되풀이 점을 빼면 4점입니다.");

            Assert.IsEmpty(Compare(body, new[] { FakePart(3, 0), closed.AsSecondary() }),
                "닫힘 규약 차이를 갈라짐으로 세고 있습니다 — 유령 결함을 만듭니다.");

            // 되풀이가 아닌 진짜 5번째 점이면 잡혀야 한다(정규화가 전부를 덮지 않는다).
            var reallyFive = new ItemIconPart(ItemIconPartKind.Polyline,
                new[] { 0f, 0f, 1f, 0f, 1f, 1f, 0f, 1f, 0.5f, 2f });
            Assert.AreEqual(5, DistinctPointCount(reallyFive));
            Assert.AreEqual(1, Compare(body, new[] { FakePart(3, 0), reallyFive.AsSecondary() }).Count,
                "정규화가 진짜 추가 점까지 삼키고 있습니다 — 그러면 이 검사는 아무것도 안 잡습니다.");
        }

        [Test]
        public void 실제_베레모_폴백_테는_이제_몸과_같은_점수다()
        {
            // ★ 원 신고 건. 옛 검사는 이 자리에 <b>2점을 상수로 박아</b> 두어 장비 담당이 폴백에
            //   innerFoot을 추가하는 것을 막고 있었고, 그 뒤에는 "3점 대 2점으로 아직 갈라져 있다"를
            //   잠갔다. 2026-09-02에 폴백을 몸에서 유도해 다시 구워 그 빚을 갚았다.
            //   기대값은 여전히 <b>몸에서 읽는다</b> — 몸이 바뀌면 기대값도 함께 바뀐다.
            List<AccessoryShapeBuilder.Shape> body = BodyShapes(EquipmentSlot.Head, AccessoryShapeBuilder.HeadBeret);
            AccessoryShapeBuilder.Shape rim = AccessorySilhouetteMetrics.Find(body, "BeretRim");

            Assert.AreEqual(1, rim.Tone, "BeretRim이 보조색이 아니게 됐습니다 — 이 검사의 전제가 깨집니다.");

            ItemIconPart[] fallback = Fallback(EquipmentSlot.Head, AccessoryShapeBuilder.HeadBeret);
            Assert.IsEmpty(Compare(body, fallback),
                "베레모 폴백이 다시 몸과 갈라졌습니다 — 유도한 값을 누가 손으로 덮어썼는지 확인하십시오.");

            ItemIconPart fallbackRim = default;
            for (int i = 0; i < fallback.Length; i++)
            {
                if (fallback[i].Tone == 1) fallbackRim = fallback[i];
            }
            Assert.AreEqual(rim.Points.Length, DistinctPointCount(fallbackRim),
                "기대값이 몸에서 오지 않았습니다 — 그게 이 파일이 처음 잡은 버그입니다.");

            Debug.Log($"{LogPrefix} 베레모 — 몸 BeretRim {rim.Points.Length}점 = 폴백 " +
                      $"{DistinctPointCount(fallbackRim)}점. innerFoot(밑변 안쪽 꺾임 = 베레모의 정체)이 살아 있다.");
        }

        /// <summary>★ 대장이 <b>비어서</b> 초록인 것과 <b>검사가 죽어서</b> 초록인 것을 가른다.
        /// <para>이 라운드에 <see cref="ItemIconPartKind.Polygon"/>이 생겼고, 비교기가 그 종류를
        /// "꼭짓점 개념이 없는 원시도형"으로 흘려보내면 채운 보조색을 가진 아이템 전부에서 축2가
        /// <b>통째로 침묵</b>한다(대장을 비운 직후가 그 사고가 가장 눈에 안 띄는 순간이다).</para></summary>
        [Test]
        public void 네거티브_컨트롤_채운_다각형_보조색도_꼭짓점_축이_산다()
        {
            var body = new List<AccessoryShapeBuilder.Shape> { Fake("Main", 3, 0), Fake("Accent", 4, 1) };

            // 같은 4각형을 닫아서 적은 채운 다각형 — 갈라진 것이 아니다.
            var samePolygon = new ItemIconPart(ItemIconPartKind.Polygon,
                new[] { 0f, 0f, 1f, 0f, 1f, 1f, 0f, 1f, 0f, 0f }).AsSecondary();
            Assert.IsEmpty(Compare(body, new[] { FakePart(3, 0), samePolygon }),
                "채운 다각형에서 닫힘 규약 차이를 갈라짐으로 세고 있습니다.");

            // 진짜로 한 점이 더 있는 채운 다각형 — 반드시 잡혀야 한다.
            var fatterPolygon = new ItemIconPart(ItemIconPartKind.Polygon,
                new[] { 0f, 0f, 1f, 0f, 1f, 1f, 0.5f, 1.5f, 0f, 1f, 0f, 0f }).AsSecondary();
            List<Gap> gaps = Compare(body, new[] { FakePart(3, 0), fatterPolygon });
            Assert.AreEqual(1, gaps.Count,
                "채운 다각형 보조색의 꼭짓점 축이 죽어 있습니다 — Polygon이 원/점 취급으로 빠지면 " +
                "이 파일의 축2가 절반 이상의 아이템에서 아무것도 재지 않습니다.");
            Assert.AreEqual(Axis.AccentVertexCount, gaps[0].Axis);
            Assert.AreEqual(4, gaps[0].Body);
            Assert.AreEqual(5, gaps[0].Fallback);
        }
    }
}
