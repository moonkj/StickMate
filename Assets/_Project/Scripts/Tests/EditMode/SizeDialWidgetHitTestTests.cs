using NUnit.Framework;
using UnityEngine;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// 2026-08-31 통합검증 R3 Major 2 — 리더가 M1(빈 구역 탭이 다이얼을 끝값으로 튕기는 버그, R2)을
    /// 고치면서 회귀 테스트를 안 남겼다는 지적. <see cref="SizeDialWidget.IsInRing"/>은 순수 함수라
    /// 씬 없이 EditMode로 잠글 수 있다.
    /// </summary>
    public sealed class SizeDialWidgetHitTestTests
    {
        private GameObject _parent;
        private SizeDialWidget _dial;

        [SetUp]
        public void SetUp()
        {
            _parent = new GameObject("DialTestParent", typeof(RectTransform));
            _dial = new SizeDialWidget(_parent.transform, 1.0f);
            _dial.CenterScreen = new Vector2(500f, 500f);
            _dial.PixelsPerPoint = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            if (_parent != null) Object.DestroyImmediate(_parent);
        }

        /// <summary>양성 대조 — 눈금이 실제로 있는 6시(정면) 방향, 원환 안은 여전히 "원환 안"이어야 한다.</summary>
        [Test]
        public void 눈금이_있는_방향은_원환_안이다()
        {
            Vector2 sixOClock = _dial.CenterScreen + new Vector2(0f, -60f); // AngleOf 기준 θ=0
            Assert.IsTrue(_dial.IsInRing(sixOClock),
                "눈금이 실제로 있는 6시 방향(반지름 60pt)이 원환 밖으로 판정됐습니다.");
        }

        /// <summary>
        /// ★ 핵심 회귀 — 12시 쪽 빈 구역(눈금이 하나도 없는 자리)은 반지름이 맞아도 "원환 밖"이어야
        /// 한다. 이게 실패하면 R2가 고친 버그(빈 곳 탭 → 끝값 점프)가 되살아난다.
        /// <para>빈 구역의 크기는 상한에서 <b>파생</b>된다(2026-08-31 상한 1.5 → 눈금 24칸 → 스윕 184°,
        /// 빈 구역 176°). 그래서 이 테스트는 각도를 세지 않고 "정수리는 언제나 빈 곳"만 쓴다 —
        /// 스윕이 어떻게 바뀌어도 12시가 눈금으로 덮이려면 스윕이 360°를 넘어야 하기 때문이다.</para>
        /// </summary>
        [Test]
        public void 눈금이_없는_12시_빈구역은_반지름이_맞아도_원환_밖이다()
        {
            // θ=180°(정확히 정수리) — 스윕은 6시 대칭이므로 눈금 수와 무관하게 여기는 빈 곳이다.
            Vector2 twelveOClock = _dial.CenterScreen + new Vector2(0f, 60f);
            Assert.IsFalse(_dial.IsInRing(twelveOClock),
                "빈 12시 구역이 '원환 안'으로 판정됐습니다 — 여기를 탭하면 다이얼이 끝값으로 튑니다.");
        }

        /// <summary>반지름 자체가 틀리면(너무 가깝거나 너무 멀면) 각도와 무관하게 원환 밖이다 — 기존 동작 보존.</summary>
        [Test]
        public void 반지름이_범위_밖이면_각도와_무관하게_원환_밖이다()
        {
            Vector2 tooClose = _dial.CenterScreen + new Vector2(5f, 0f);
            Vector2 tooFar = _dial.CenterScreen + new Vector2(200f, 0f);
            Assert.IsFalse(_dial.IsInRing(tooClose), "허브에 가까운 점이 원환 안으로 판정됐습니다.");
            Assert.IsFalse(_dial.IsInRing(tooFar), "원환 훨씬 밖의 점이 원환 안으로 판정됐습니다.");
        }
    }
}
