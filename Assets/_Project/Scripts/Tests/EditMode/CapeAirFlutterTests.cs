using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 낙하 중 망토 펄럭임의 <b>식(式) 회귀</b> — 2026-08-31 사용자 신고
    /// "떨어지거나 할때 망토도 펄럭여야하는데 고정되어있음".
    ///
    /// ============================================================================
    /// 무엇이 고장나 있었나 (증상이 아니라 원인)
    /// ============================================================================
    /// 흔들림의 구동원이 <c>CharacterAccessoryRenderer.ResolveWalkSpeed01()</c>
    /// = <c>|Body.linearVelocity.x| / 보행속도</c> <b>하나뿐</b>이었다. 수직 낙하는 x 속도가 0이므로
    /// 진폭이 정확히 0이 되고, 그 경로는 0일 때 갱신 호출 자체를 건너뛴다. 즉 "낙하 중에는 자락이
    /// 한 점도 움직이지 않는다"가 버그가 아니라 <b>설계상 보장</b>돼 있었다. 사용자의 "고정"은 정확한 관찰이다.
    ///
    /// ============================================================================
    /// 왜 EditMode인가 — 그리고 이 파일이 <b>못</b> 잡는 것
    /// ============================================================================
    /// 여기서 잠그는 것은 <see cref="AccessoryShapeBuilder.HemAirOffset"/>라는 <b>순수 함수</b>의 성질뿐이다.
    /// 씬도 물리도 필요 없어 결정론적이고 빠르다. 다만 이 파일만으로는
    /// "실제로 낙하 상태에서 그 함수가 <b>불리는가</b>"를 증명할 수 없다 — 그건 렌더러의 상태 판정
    /// (<c>ResolveAirFlow01</c>)이고, <c>Tests/PlayMode/CapeFallFlutterTests.cs</c>가 실행으로 잡는다.
    /// 두 파일이 짝이며, 한쪽만 있으면 이 프로젝트가 여섯 번 반복한
    /// "로직은 있는데 아무도 안 부른다" 실패를 그대로 다시 낸다.
    /// </summary>
    public sealed class CapeAirFlutterTests
    {
        /// <summary>배율 1.0 프리팹 실측 리그(도형 계산 전용 — GameObject를 만들지 않는다).</summary>
        private static AccessoryShapeBuilder.Rig Rig(float facing = 1f)
        {
            const float H = StickConfig.BaselineCharacterTotalHeight;
            const float R = 0.22f;
            return new AccessoryShapeBuilder.Rig(R, H - R, 1.7646944f, 0.9346944f, facing);
        }

        private const float R = 0.22f;

        /// <summary>수직 낙하에서 기류가 가는 방향 — 아래로 떨어지므로 바람은 <b>위로</b> 훑고 지나간다.</summary>
        private static readonly Vector2 FallWind = Vector2.up;

        // ============================================================================
        // (1) 경계 — 속도가 0에 가까우면 펄럭임도 0이다
        // ============================================================================

        /// <summary>
        /// 리더 지시의 경계 케이스. <b>if 분기로 막는 것이 아니라 식 자체가 0을 낸다</b>는 것이 요점이다 —
        /// 분기로 막으면 임계값 바로 위에서 진폭이 계단처럼 튀어 "천이 딸깍 하고 젖혀지는" 그림이 된다.
        /// </summary>
        [TestCase(0f)]
        [TestCase(-1f)]      // 음수(있을 수 없는 입력)도 0으로 접힌다 — Clamp01.
        public void 기류가_0이면_오프셋이_정확히_0이다(float air01)
        {
            for (int idx = 0; idx < 8; idx++)
            {
                Vector2 o = AccessoryShapeBuilder.HemAirOffset(R, FallWind, air01, 12.34f, idx);
                Assert.AreEqual(0f, o.magnitude, 1e-6f,
                    $"기류 {air01}에서 {idx}번 점이 {o}만큼 움직였습니다 — 멈춘 망토가 펄럭입니다.");
            }
        }

        /// <summary>진폭은 기류에 <b>연속</b>으로 붙어 있다. 아주 작은 기류에서는 화면상 보이지 않을 만큼
        /// 작아야 한다(획 두께 0.048의 1/10 미만).</summary>
        [Test]
        public void 기류가_아주_작으면_펄럭임도_눈에_안_보일_만큼_작다()
        {
            float max = 0f;
            for (int idx = 0; idx < 8; idx++)
            {
                max = Mathf.Max(max, AccessoryShapeBuilder.HemAirOffset(R, FallWind, 0.01f, 3.3f, idx).magnitude);
            }
            Debug.Log($"[망토펄럭] 기류 0.01에서 최대 오프셋 = {max:F6} 월드유닛(획 두께 0.048 기준 {max / 0.048f:P1}).");
            Assert.Less(max, 0.048f * 0.1f,
                $"기류 1%에서 이미 {max:F5}유닛이 움직입니다 — 정지에 가까운 순간에 천이 튑니다.");
        }

        [Test]
        public void 바람_방향이_0이면_오프셋도_0이다()
        {
            // 속도가 정확히 0인 프레임(포물선 정점)에서 방향이 NaN으로 새는 것을 막는 경로.
            Vector2 o = AccessoryShapeBuilder.HemAirOffset(R, Vector2.zero, 1f, 1f, 2);
            Assert.AreEqual(0f, o.magnitude, 1e-6f, $"방향 없는 기류가 {o}만큼 밀었습니다.");
            Assert.IsFalse(float.IsNaN(o.x) || float.IsNaN(o.y), "오프셋에 NaN이 샜습니다.");
        }

        // ============================================================================
        // (2) 낙하 속도가 커지면 자락이 실제로 더 젖혀진다 — 신고의 핵심
        // ============================================================================

        [Test]
        public void 기류가_셀수록_자락이_더_젖혀진다()
        {
            float prev = -1f;
            foreach (float air in new[] { 0.1f, 0.3f, 0.6f, 1.0f })
            {
                // 젖힘(정적 성분)만 본다 — 물결은 사인파라 순간값으로 단조성을 논할 수 없다.
                float push = Vector2.Dot(AccessoryShapeBuilder.HemAirOffset(R, FallWind, air, 0f, 2), FallWind);
                Debug.Log($"[망토펄럭] 기류 {air:F1} -> 바람 방향 젖힘 {push:F5}유닛 (= {push / R:F2}R).");
                Assert.Greater(push, prev,
                    $"기류가 {air}로 커졌는데 젖힘이 늘지 않았습니다({push:F5} <= {prev:F5}).");
                prev = push;
            }
        }

        /// <summary>최대 기류에서의 젖힘이 <b>눈에 보이는 크기</b>인가. 획 두께 정도만 움직이면
        /// "펄럭인다"가 아니라 "떨린다"로 보이고, 사용자는 여전히 고정이라고 느낀다.</summary>
        [Test]
        public void 최대_기류의_젖힘은_머리_반경만큼_크다()
        {
            float push = Vector2.Dot(AccessoryShapeBuilder.HemAirOffset(R, FallWind, 1f, 0f, 2), FallWind);
            Assert.Greater(push, R * 0.5f,
                $"최대 기류의 젖힘이 {push / R:F2}R뿐입니다 — 화면에서 '고정'과 구분되지 않습니다.");
        }

        // ============================================================================
        // (3) 방향 — 바람이 가는 쪽으로 밀리고, 물결은 그 수직으로 떤다
        // ============================================================================

        [Test]
        public void 젖힘은_바람이_가는_쪽이고_물결은_그_수직이다()
        {
            var wind = new Vector2(0.6f, 0.8f); // 정규화 안 된 입력 — 함수가 스스로 정규화해야 한다.
            Vector2 unit = wind.normalized;
            var perp = new Vector2(-unit.y, unit.x);

            float pushSum = 0f, perpAbsMax = 0f;
            const int samples = 64;
            for (int i = 0; i < samples; i++)
            {
                float t = i * 0.01f;
                Vector2 o = AccessoryShapeBuilder.HemAirOffset(R, wind, 1f, t, 2);
                pushSum += Vector2.Dot(o, unit);
                perpAbsMax = Mathf.Max(perpAbsMax, Mathf.Abs(Vector2.Dot(o, perp)));
            }

            // 젖힘 성분은 항상 같은 부호라 평균이 그대로 남는다(물결과 달리 상쇄되지 않는다).
            Assert.Greater(pushSum / samples, R * AccessoryShapeBuilder.HemAirPushRatio * 0.9f,
                "젖힘이 바람 방향으로 나오지 않습니다 — 정규화를 빠뜨렸거나 축이 뒤바뀌었습니다.");
            Assert.Greater(perpAbsMax, R * 0.05f,
                "바람에 수직인 물결이 없습니다 — 젖히기만 하면 '펄럭임'이 아니라 '기울임'입니다.");
        }

        [Test]
        public void 정규화_안_된_바람을_넣어도_진폭이_배가_되지_않는다()
        {
            Vector2 unit = AccessoryShapeBuilder.HemAirOffset(R, Vector2.up, 1f, 0.7f, 3);
            Vector2 long5 = AccessoryShapeBuilder.HemAirOffset(R, Vector2.up * 5f, 1f, 0.7f, 3);
            Assert.AreEqual(unit.x, long5.x, 1e-5f, "바람 벡터 길이가 진폭에 샜습니다(x).");
            Assert.AreEqual(unit.y, long5.y, 1e-5f, "바람 벡터 길이가 진폭에 샜습니다(y).");
        }

        // ============================================================================
        // (4) 시간이 지나면 물결이 실제로 움직인다 (= 정적 오프셋이 아니다)
        // ============================================================================

        [Test]
        public void 같은_기류라도_시간이_지나면_점이_움직인다()
        {
            // 한 주기(1/HemAirRippleHz초)를 잘게 훑어 진동 폭을 잰다.
            float period = 1f / AccessoryShapeBuilder.HemAirRippleHz;
            float min = float.MaxValue, max = float.MinValue;
            var perp = new Vector2(-FallWind.y, FallWind.x);
            for (int i = 0; i <= 40; i++)
            {
                float t = period * i / 40f;
                float w = Vector2.Dot(AccessoryShapeBuilder.HemAirOffset(R, FallWind, 1f, t, 2), perp);
                min = Mathf.Min(min, w);
                max = Mathf.Max(max, w);
            }
            float swing = max - min;
            Debug.Log($"[망토펄럭] 한 주기({period:F3}초) 동안 물결 진폭 = {swing:F5}유닛 (= {swing / R:F2}R).");
            Assert.Greater(swing, R * AccessoryShapeBuilder.HemAirRippleRatio,
                "한 주기를 훑었는데도 점이 거의 안 움직입니다 — 사실상 정적 오프셋입니다.");
        }

        // ============================================================================
        // (5) 천이 찢어지지 않는다 — 밑단은 통째로 같은 만큼 젖혀진다
        // ============================================================================

        /// <summary>망토는 옷깃에 매달린 <b>천 한 장</b>이다. 밑단 점마다 젖힘이 다르면 천이 늘어나
        /// 찢어진 것처럼 보인다. 점마다 달라도 되는 것은 물결(위상)뿐이다.</summary>
        [Test]
        public void 밑단_점들의_젖힘은_전부_같다()
        {
            float first = float.NaN;
            for (int idx = 2; idx <= 6; idx++)
            {
                float push = Vector2.Dot(AccessoryShapeBuilder.HemAirOffset(R, FallWind, 0.8f, 1.1f, idx), FallWind);
                if (float.IsNaN(first)) first = push;
                Assert.AreEqual(first, push, 1e-5f,
                    $"{idx}번 밑단 점의 젖힘이 다른 점과 다릅니다 — 천이 늘어납니다.");
            }
        }

        // ============================================================================
        // (6) 배율 비례 — 월드유닛 절대 상수가 새지 않았다
        // ============================================================================

        [Test]
        public void 오프셋은_머리_반경에_정확히_비례한다()
        {
            Vector2 big = AccessoryShapeBuilder.HemAirOffset(0.22f, FallWind, 0.7f, 2.5f, 4);
            Vector2 small = AccessoryShapeBuilder.HemAirOffset(0.11f, FallWind, 0.7f, 2.5f, 4);
            Assert.AreEqual(big.x * 0.5f, small.x, 1e-6f, "x가 머리 반경에 비례하지 않습니다.");
            Assert.AreEqual(big.y * 0.5f, small.y, 1e-6f, "y가 머리 반경에 비례하지 않습니다.");
        }

        // ============================================================================
        // (7) 그늘(주름)도 천을 따라간다 — 도형 쪽 계약
        // ============================================================================

        /// <summary>
        /// 주름 두 줄은 천에 진 <b>그늘</b>이다(AccessoryShapeBuilder.Shade 문서). 천이 젖혀지는데
        /// 그늘만 제자리에 남으면 2026-08-30 첫 시안에서 이미 겪은 "천 위에 붙은 끈"으로 되돌아간다.
        /// 그래서 <b>끝점만</b>(옷깃 쪽 시작점은 어깨 고정) 흔들 구간에 들어 있어야 한다.
        /// </summary>
        [TestCase(AccessoryShapeBuilder.BackCape)]
        [TestCase(AccessoryShapeBuilder.BackLongCape)]
        public void 망토_주름의_끝점도_흔들_구간에_들어_있다(int item)
        {
            var sink = new List<AccessoryShapeBuilder.Shape>();
            AccessoryShapeBuilder.Append(sink, EquipmentSlot.Shoulders, item, Rig());

            int folds = 0;
            for (int i = 0; i < sink.Count; i++)
            {
                AccessoryShapeBuilder.Shape s = sink[i];
                if (!s.Name.StartsWith("CapeFold")) continue;
                folds++;
                Assert.IsTrue(s.HasSway, $"'{s.Name}'이 흔들 구간을 선언하지 않습니다 — 천만 젖혀지고 그늘은 남습니다.");
                Assert.AreEqual(s.Points.Length - 1, s.SwayStart,
                    $"'{s.Name}'의 흔들 구간이 끝점이 아닙니다(옷깃 쪽이 움직이면 어깨에서 떨어져 나옵니다).");
                Assert.AreEqual(1, s.SwayCount, $"'{s.Name}'의 흔들 점이 1개가 아닙니다.");
            }
            Assert.AreEqual(2, folds, "망토 주름이 2줄이 아닙니다 — 도형이 바뀌었다면 이 테스트도 함께 갱신해야 합니다.");
        }

        /// <summary>망토 윤곽선의 흔들 구간은 밑단 5점 그대로여야 한다(재설계 이전 값으로 되돌아가는 것을 막는다).</summary>
        [Test]
        public void 망토_윤곽선은_밑단_5점을_흔든다()
        {
            var sink = new List<AccessoryShapeBuilder.Shape>();
            AccessoryShapeBuilder.Append(sink, EquipmentSlot.Shoulders, AccessoryShapeBuilder.BackCape, Rig());

            bool found = false;
            for (int i = 0; i < sink.Count; i++)
            {
                if (sink[i].Name != "CapeOutline") continue;
                found = true;
                Assert.AreEqual(2, sink[i].SwayStart, "밑단 시작 인덱스가 2가 아닙니다.");
                Assert.AreEqual(5, sink[i].SwayCount, "흔들리는 밑단 점이 5개가 아닙니다.");
                Assert.IsTrue(sink[i].Filled, "망토 윤곽선이 채움을 선언하지 않습니다.");
            }
            Assert.IsTrue(found, "CapeOutline 도형을 찾지 못했습니다.");
        }

        // ============================================================================
        // (8) 색 — 2026-08-31 사용자 요청 "망토 색도 빨간색으로"
        // ============================================================================

        /// <summary>
        /// 카드 색과 <b>몸에 칠하는 색</b>이 둘 다 빨강인지 본다. 이 둘이 갈라지는 사고는 이미 한 번 났다
        /// (2026-08-30 "카드엔 색이 있는데 착용하면 색이 없다"). <see cref="ItemCatalog.WornColor"/>의
        /// 채도 하한·명도 창을 통과한 뒤에도 빨강으로 남는지가 핵심이다 — 이미 채도 0.75/명도 0.80이라
        /// 두 보정 모두 이 색을 건드리지 않아야 한다.
        /// </summary>
        [TestCase(AccessoryShapeBuilder.BackCape, "equip.shoulders.cape")]
        [TestCase(AccessoryShapeBuilder.BackLongCape, "equip.shoulders.long_cape")]
        public void 망토는_카드에서도_몸에서도_빨강이다(int item, string expectedId)
        {
            ItemCatalogEntry cape = ItemCatalog.Item(EquipmentSlot.Shoulders, item);
            Assert.IsNotNull(cape, $"{expectedId} 항목을 찾지 못했습니다.");
            Assert.AreEqual(expectedId, cape.Id, $"{item}번 자리가 {expectedId}가 아닙니다.");

            AssertRed(cape.PrimaryColor, $"{cape.DisplayName} 카드 주색");

            // 흰 잉크 / 검은 잉크 양쪽에서 확인한다 — 잉크색은 사용자가 바꿀 수 있다.
            foreach (Color ink in new[] { Color.white, Color.black })
            {
                ItemCatalog.ResolveWornPalette(EquipmentSlot.Shoulders, item, ink,
                    out Color primary, out Color _);
                AssertRed(primary, $"{cape.DisplayName} 몸에 칠하는 주색(잉크 {ink})");
            }
        }

        /// <summary>두 망토가 <b>같은</b> 빨강이어야 한다(리더 결정 2026-09-01). 하나만 고치면
        /// 보관함에서 나란히 놓였을 때 "왜 얘만 다른 빨강이지"가 된다.</summary>
        [Test]
        public void 짧은망토와_긴망토의_빨강은_완전히_같은_값이다()
        {
            Color a = ItemCatalog.Item(EquipmentSlot.Shoulders, AccessoryShapeBuilder.BackCape).PrimaryColor;
            Color b = ItemCatalog.Item(EquipmentSlot.Shoulders, AccessoryShapeBuilder.BackLongCape).PrimaryColor;
            Assert.AreEqual(a.r, b.r, 1e-5f, "두 망토의 빨강 채널이 다릅니다.");
            Assert.AreEqual(a.g, b.g, 1e-5f, "두 망토의 초록 채널이 다릅니다.");
            Assert.AreEqual(a.b, b.b, 1e-5f, "두 망토의 파랑 채널이 다릅니다.");
        }

        private static void AssertRed(Color c, string what)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            float hueDegrees = h * 360f;
            Debug.Log($"[망토색] {what} = RGB({c.r:F3},{c.g:F3},{c.b:F3}) HSV(h={hueDegrees:F1}도, s={s:F2}, v={v:F2}).");
            Assert.IsTrue(hueDegrees < 20f || hueDegrees > 340f,
                $"{what}의 색상각이 {hueDegrees:F1}도입니다 — 빨강 대역(±20도)이 아닙니다.");
            Assert.Greater(s, 0.5f, $"{what}의 채도가 {s:F2}뿐입니다 — 흐린 분홍/회색으로 보입니다.");
            Assert.Greater(c.r, c.g * 2f, $"{what}에서 빨강이 초록의 2배 미만입니다.");
            Assert.Greater(c.r, c.b * 2f, $"{what}에서 빨강이 파랑의 2배 미만입니다.");
        }
    }
}
