using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-05 진단 로그 (a) — 근접 미스 클릭 로그의 <b>규칙과 포맷</b>을 잠근다
    /// (docs/DEBUG_WINDOWS_THROW_LANDING_STALL.md §6). 발생 조건(실제 히트박스 경로)은 PlayMode
    /// <c>ThrowLandingDiag_ClickHitboxNearMissTests</c>가 잠그고, 여기는 순수 규칙만 실행한다.
    /// 시간은 전부 벽시계 초를 인자로 넘긴다 — 기다리지 않는다.
    /// </summary>
    public sealed class ThrowLandingDiag_NearMissPolicyTests
    {
        // ★ 태그는 리더 지시("[히트판정] 계열")가 준 **바깥의 자**다 — 프로덕션 상수를 베낀 것이 아니라
        //   프로덕션이 이 요구를 만족하는지를 잰다. 관례(대괄호 한글 한 단어)도 같이 잠근다.
        private const string RequiredTag = "[히트판정]";

        [Test]
        public void 태그는_리더가_지정한_히트판정이고_대괄호_한글_한단어_관례를_따른다()
        {
            Assert.AreEqual(RequiredTag, ClickHitboxNearMissPolicy.LogTag);
            Assert.IsTrue(ClickHitboxNearMissPolicy.LogTag.StartsWith("[") && ClickHitboxNearMissPolicy.LogTag.EndsWith("]"));
            Assert.IsFalse(ClickHitboxNearMissPolicy.LogTag.Contains(" "), "태그 안에 공백이 있으면 StallAttribution의 태그 집계가 갈라진다.");
        }

        [Test]
        public void 근접_판정은_몸중심에서_신장_배수_반경이고_경계는_포함이다()
        {
            float h = 2.0f;
            float radius = h * ClickHitboxNearMissPolicy.NearRadiusHeights;   // 3.0
            Assert.IsTrue(ClickHitboxNearMissPolicy.IsNear(0f, h));
            Assert.IsTrue(ClickHitboxNearMissPolicy.IsNear(radius - 0.01f, h));
            Assert.IsTrue(ClickHitboxNearMissPolicy.IsNear(radius, h), "경계값은 근접으로 친다(놓치는 쪽보다 남기는 쪽).");
            Assert.IsFalse(ClickHitboxNearMissPolicy.IsNear(radius + 0.01f, h));
        }

        [Test]
        public void 신장을_모르거나_거리가_NaN이면_근접이_아니다()
        {
            Assert.IsFalse(ClickHitboxNearMissPolicy.IsNear(0.1f, 0f));
            Assert.IsFalse(ClickHitboxNearMissPolicy.IsNear(0.1f, -1f));
            Assert.IsFalse(ClickHitboxNearMissPolicy.IsNear(0.1f, float.NaN));
            Assert.IsFalse(ClickHitboxNearMissPolicy.IsNear(float.NaN, 2f));
        }

        [Test]
        public void 레이트리밋은_벽시계_초_기준이고_첫_호출은_항상_통과한다()
        {
            float min = ClickHitboxNearMissPolicy.LogMinIntervalSeconds;
            Assert.IsTrue(ClickHitboxNearMissPolicy.ShouldLog(100f, float.NegativeInfinity), "첫 호출(직전 로그 없음).");
            Assert.IsFalse(ClickHitboxNearMissPolicy.ShouldLog(100f, 100f - min + 0.01f), "간격 미만이면 억제.");
            Assert.IsTrue(ClickHitboxNearMissPolicy.ShouldLog(100f, 100f - min), "정확히 간격이면 통과.");
            Assert.IsTrue(ClickHitboxNearMissPolicy.ShouldLog(100f, 100f - min - 1f));
            Assert.IsTrue(ClickHitboxNearMissPolicy.ShouldLog(float.NaN, 50f), "모르면 남기는 쪽(진단 로그).");
        }

        [Test]
        public void 근접_미스_한줄에는_태그_거리_신장비_콜라이더외접_상태_프레임이_전부_들어간다()
        {
            // 기대값은 테스트가 스스로 계산한다(프로덕션 함수로 만들지 않는다): 거리 1.2 / 신장 2.0 = 0.60신장.
            var envelope = new Bounds(new Vector3(3f, -10f, 0f), new Vector3(0.5f, 1.7f, 0f));
            string line = ClickHitboxNearMissPolicy.FormatNearMiss("전역폴링",
                cursorWorld: new Vector2(4.5f, -9.5f), bodyCenterWorld: new Vector2(3f, -10f), footWorld: new Vector2(3f, -10.85f),
                characterHeightWorld: 2.0f, distanceWorld: 1.2f, hasEnvelope: true, envelope: envelope,
                activeColliders: 11, totalColliders: 11, state: StickmanStateId.Idle, frame: 77);

            StringAssert.StartsWith(RequiredTag + " ", line);
            StringAssert.Contains("(전역폴링)", line);
            StringAssert.Contains("거리=1.20유닛(0.60신장", line);
            StringAssert.Contains("커서 월드=(4.50, -9.50)", line);
            StringAssert.Contains("몸 중심=(3.00, -10.00)", line);
            StringAssert.Contains("콜라이더 외접=(2.75, -10.85)~(3.25, -9.15)", line);
            StringAssert.Contains("활성 11/11개", line);
            StringAssert.Contains("상태=Idle", line);
            StringAssert.Contains("프레임#77", line);
            Assert.IsFalse(line.Contains("\n"), "Player.log 한 줄 = 사건 하나(여러 줄이면 grep 한 번에 안 잡힌다).");
        }

        [Test]
        public void 활성_콜라이더가_없으면_외접_대신_그_사실을_적는다()
        {
            string line = ClickHitboxNearMissPolicy.FormatNearMiss("전역폴링", Vector2.zero, Vector2.zero, Vector2.zero,
                2.0f, 0.5f, hasEnvelope: false, envelope: default, activeColliders: 0, totalColliders: 11,
                state: StickmanStateId.LandingCrouch, frame: 3);
            StringAssert.Contains("(활성 콜라이더 없음)", line);
            StringAssert.Contains("활성 0/11개", line);
            StringAssert.Contains("상태=LandingCrouch", line);
        }

        [Test]
        public void 커서_조회_실패_한줄도_같은_태그로_남고_상태와_프레임을_적는다()
        {
            string line = ClickHitboxNearMissPolicy.FormatCursorUnavailable("전역폴링", StickmanStateId.Idle, 1234);
            StringAssert.StartsWith(RequiredTag + " ", line);
            StringAssert.Contains("커서 좌표를 읽지 못함(전역폴링)", line);
            StringAssert.Contains("상태=Idle", line);
            StringAssert.Contains("프레임#1234", line);
        }
    }
}
