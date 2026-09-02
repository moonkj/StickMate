using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ============================================================================
    /// 표시 모니터 선택 — 사용자 확정 규칙의 실행 검증 (2026-09-02)
    /// ============================================================================
    /// 사용자 원문: <i>"멀티모니터일때 무조건 주모니터에서 실행하도록"</i> →
    /// <i>"이게 기본이고 사용자가 설정할수있게 기능 넣어줘 다만 멀티모니터 인식이 됐을때만 활성화"</i> →
    /// <i>"그럼 왼쪽 오른쪽 선택할수 있게 <b>기본은 왼쪽</b>"</i>.
    ///
    /// <para><b>이 파일이 잡는 가장 중요한 것</b>: 기본값이 <b>"인덱스 0"도 "주 모니터"도 아니라
    /// "x가 가장 작은 화면"</b>이라는 것. 셋은 흔한 배치에서 <b>우연히 같아서</b>, 구분하지 않는
    /// 구현도 대부분의 테스트를 통과한다. 그래서 아래 표본은 셋이 <b>서로 갈리도록</b> 일부러 구성했다:
    /// 목록 순서는 오른쪽 먼저, 주 모니터 플래그는 오른쪽에 붙어 있다.</para>
    ///
    /// <para>P/Invoke도 UnityEngine 런타임도 타지 않는 순수 규칙이라 이 개발 머신(디스플레이 1대)에서
    /// <b>실행으로</b> 검증된다 — 그것이 정책을 <c>Platform/</c> 중립에 둔 이유다.</para>
    /// </summary>
    public class OverlayMonitorChoiceTests
    {
        private static OsMonitorFact M(float x, float y, float w, float h, bool primary)
            => new OsMonitorFact(new Rect(x, y, w, h), new Rect(x, y, w, h), primary, "");

        /// <summary>주 모니터가 <b>오른쪽</b>이고 목록 순서도 오른쪽이 먼저인 배치.
        /// "인덱스 0" / "주 모니터" / "가장 왼쪽"이 <b>전부 다른 답</b>을 내는 표본이다.</summary>
        private static List<OsMonitorFact> RightIsPrimary() => new List<OsMonitorFact>
        {
            M(1920f, 0f, 2560f, 1440f, true),
            M(0f, 0f, 1920f, 1080f, false),
        };

        [Test]
        public void 기본값은_인덱스0이_아니라_가장_왼쪽이다()
        {
            var monitors = RightIsPrimary();

            Assert.AreEqual(1, OverlayMonitorChoicePolicy.IndexOfLeftmost(monitors),
                "OS 열거 순서를 그대로 믿었습니다 — EnumDisplayMonitors/CGGetActiveDisplayList는 " +
                "정렬을 보장하지 않습니다(정렬하는 것은 라이브러리 쪽입니다).");

            OverlayMonitorChoice choice = OverlayMonitorChoicePolicy.Resolve(monitors, null);
            Assert.AreEqual(OverlayMonitorChoiceSource.StartSlotDefault, choice.Source);
            Assert.AreEqual(1, choice.Index,
                "기본값이 가장 왼쪽이 아닙니다. 사용자 확정 규칙은 '기본은 왼쪽'입니다.");
        }

        [Test]
        public void 기본값은_주모니터가_아니다()
        {
            var monitors = RightIsPrimary();
            Assert.AreEqual(0, OverlayMonitorChoicePolicy.IndexOfPrimary(monitors),
                "주 모니터는 OS 플래그로만 알 수 있습니다(좌표 (0,0) 추정 금지).");
            Assert.AreNotEqual(OverlayMonitorChoicePolicy.IndexOfPrimary(monitors),
                OverlayMonitorChoicePolicy.Resolve(monitors, null).Index,
                "기본값이 주 모니터를 따라갔습니다 — 사용자가 기본값을 '왼쪽'으로 바꿨습니다(2026-09-02).");
        }

        [Test]
        public void 주모니터_왼쪽에_보조가_있으면_0번은_주모니터가_아니다()
        {
            // MacOverlayStateEnforcer의 `isPrimary = i == 0`이 거짓이 되던 바로 그 구성.
            var monitors = new List<OsMonitorFact>
            {
                M(-1920f, 0f, 1920f, 1080f, false),
                M(0f, 0f, 2560f, 1440f, true),
            };
            Assert.AreEqual(0, OverlayMonitorChoicePolicy.IndexOfLeftmost(monitors));
            Assert.AreEqual(1, OverlayMonitorChoicePolicy.IndexOfPrimary(monitors),
                "이 배치에서 0번을 주 모니터로 보면 macOS가 '화면 전체 덮기'를 엉뚱한 화면 크기로 합니다.");
        }

        [Test]
        public void 사용자_선택이_기본값을_이긴다()
        {
            var monitors = RightIsPrimary();
            string slot = OverlayMonitorChoicePolicy.SlotSaveName(OverlayMonitorSlot.End);

            OverlayMonitorChoice choice = OverlayMonitorChoicePolicy.Resolve(monitors, slot);
            Assert.AreEqual(OverlayMonitorChoiceSource.UserPreferred, choice.Source);
            Assert.AreEqual(0, choice.Index);
        }

        [Test]
        public void 해상도만_바뀌면_선택이_살아남는다()
        {
            string slot = OverlayMonitorChoicePolicy.SlotSaveName(OverlayMonitorSlot.End);
            var after = new List<OsMonitorFact>
            {
                M(1920f, 0f, 3840f, 2160f, true),
                M(0f, 0f, 1920f, 1080f, false),
            };
            Assert.AreEqual(OverlayMonitorChoiceSource.UserPreferred,
                OverlayMonitorChoicePolicy.Resolve(after, slot).Source,
                "해상도를 바꿨다고 사용자의 선택이 사라지면 안 됩니다.");
            Assert.AreEqual(0, OverlayMonitorChoicePolicy.Resolve(after, slot).Index);
        }

        /// <summary>
        /// 배치가 <b>미러링으로</b> 바뀌면 자리 개념이 사라진다 — 기본값으로 떨어지되
        /// <b>그 사실이 값으로</b> 남아야 한다(조용히 폴백하면 "설정이 안 먹는다"가 된다).
        /// </summary>
        [Test]
        public void 축이_사라지면_기본값으로_떨어지고_그_사실이_값으로_남는다()
        {
            string slot = OverlayMonitorChoicePolicy.SlotSaveName(OverlayMonitorSlot.End);
            var mirrored = new List<OsMonitorFact>
            {
                M(0f, 0f, 1920f, 1080f, true),
                M(0f, 0f, 1920f, 1080f, false),
            };
            OverlayMonitorChoice choice = OverlayMonitorChoicePolicy.Resolve(mirrored, slot);
            Assert.AreEqual(OverlayMonitorChoiceSource.UserPreferredMissing, choice.Source,
                "폴백이 '정상 기본값'과 구분되지 않습니다 — 로그가 조용해집니다.");
            Assert.IsTrue(choice.HasIndex, "그래도 창 목표는 결정적으로 정해져야 합니다.");
        }

        [Test]
        public void 모르는_것을_0번으로_위장하지_않는다()
        {
            OverlayMonitorChoice empty = OverlayMonitorChoicePolicy.Resolve(new List<OsMonitorFact>(), null);
            Assert.IsFalse(empty.HasIndex);
            Assert.AreEqual(OverlayMonitorChoiceSource.NoMonitors, empty.Source);

            Assert.IsFalse(OverlayMonitorChoicePolicy.TryParseOrigin("garbage", out _),
                "깨진 키를 (0,0)으로 파싱하면 '주 모니터를 골랐다'는 거짓 성공이 됩니다.");
            Assert.IsFalse(OverlayMonitorChoicePolicy.TryParseOrigin(null, out _));
        }

        [Test]
        public void 진단용_좌표_문자열은_여전히_왕복한다()
        {
            // MakeKey는 저장에서 물러났지만 로그가 "그 화면이 어디였는지"를 말하는 데 쓴다.
            string key = OverlayMonitorChoicePolicy.MakeKey(new Rect(-1920f, 120f, 1920f, 1080f));
            Assert.IsTrue(OverlayMonitorChoicePolicy.TryParseOrigin(key, out Vector2 origin));
            Assert.AreEqual(new Vector2(-1920f, 120f), origin);
        }

        [Test]
        public void 설정_행은_2대_이상일_때만_산다()
        {
            Assert.IsFalse(OverlayMonitorChoicePolicy.IsMultiMonitor(0));
            Assert.IsFalse(OverlayMonitorChoicePolicy.IsMultiMonitor(1),
                "1대인데 행이 살아 있으면 '골랐는데 아무 일도 안 일어난다'가 됩니다(사용자 확정 조건).");
            Assert.IsTrue(OverlayMonitorChoicePolicy.IsMultiMonitor(2));
            Assert.IsTrue(OverlayMonitorChoicePolicy.IsMultiMonitor(3));

            // ★ 게이트 입력은 IsMultiMonitor가 아니라 CanChoose다(ux-designer §49).
            Assert.IsTrue(OverlayMonitorChoicePolicy.CanChoose(RightIsPrimary()));
            Assert.IsFalse(OverlayMonitorChoicePolicy.CanChoose(
                new List<OsMonitorFact> { M(0f, 0f, 1920f, 1080f, true) }));
        }

        [Test]
        public void 축은_가로_세로_불가_3값이다()
        {
            Assert.AreEqual(MonitorArrangementAxis.Horizontal,
                OverlayMonitorChoicePolicy.ResolveAxis(RightIsPrimary()));

            var stacked = new List<OsMonitorFact>
            {
                M(0f, 0f, 1920f, 1080f, true),
                M(0f, 1080f, 1920f, 1080f, false),
            };
            Assert.AreEqual(MonitorArrangementAxis.Vertical,
                OverlayMonitorChoicePolicy.ResolveAxis(stacked));

            Assert.AreEqual(MonitorArrangementAxis.Indistinct,
                OverlayMonitorChoicePolicy.ResolveAxis(
                    new List<OsMonitorFact> { M(0f, 0f, 1920f, 1080f, true) }),
                "1대는 축이 설 수 없습니다.");
        }

        /// <summary>
        /// ★ <b>미러링을 "세로"라고 답하면 원칙 1 위반</b> — 두 화면이 같은 픽셀인데 칩 두 개가
        /// 서로 다른 척한다. ux-designer가 <b>자기 초안도 이 함정에 빠졌다</b>고 자백한 지점이며,
        /// 처방은 <b>중심 일치 검사를 첫 줄에</b> 두는 것이다(겹침 비교를 먼저 하면
        /// <c>overlapX &gt; overlapY</c>로 "세로"에 떨어진다).
        /// </summary>
        [Test]
        public void 미러링은_세로가_아니라_불가다()
        {
            var mirrored = new List<OsMonitorFact>
            {
                M(0f, 0f, 1920f, 1080f, true),
                M(0f, 0f, 1920f, 1080f, false),   // 같은 픽셀을 가리킨다
            };
            Assert.AreEqual(MonitorArrangementAxis.Indistinct,
                OverlayMonitorChoicePolicy.ResolveAxis(mirrored),
                "미러링을 '세로 배치'로 읽었습니다 — UI가 위쪽/아래쪽 칩 두 개를 그리는데 " +
                "두 화면은 같은 화면입니다(절대 불변 원칙 1 위반).");

            Assert.IsFalse(OverlayMonitorChoicePolicy.CanChoose(mirrored),
                "미러링에서 설정 행이 살아 있으면 '고를 것이 둘인데 화면은 하나'가 됩니다.");
            Assert.IsTrue(OverlayMonitorChoicePolicy.IsMultiMonitor(mirrored.Count),
                "표본 전제: 개수로만 보면 2대다 — 그래서 게이트 입력이 IsMultiMonitor가 아니라 " +
                "CanChoose여야 한다는 것이 ux-designer의 지적이다.");

            // 축이 안 서도 창은 어딘가에 떠야 한다 — 결정적인 답을 준다.
            OverlayMonitorChoice choice = OverlayMonitorChoicePolicy.Resolve(mirrored, null);
            Assert.IsTrue(choice.HasIndex, "미러링이라고 창 목표를 포기하면 안 됩니다.");
        }

        [Test]
        public void 세로_배치에서는_x정렬이_아니라_y정렬로_자리를_정한다()
        {
            // 네이티브는 x만 보고 정렬한다 — 세로 배치에서 그 목록을 그대로 믿으면 위/아래가 뒤바뀐다.
            var stacked = new List<OsMonitorFact>
            {
                M(0f, 1080f, 1920f, 1080f, false),   // 아래쪽이 목록 앞
                M(0f, 0f, 1920f, 1080f, true),       // 위쪽
            };
            MonitorArrangementAxis axis = OverlayMonitorChoicePolicy.ResolveAxis(stacked);
            Assert.AreEqual(MonitorArrangementAxis.Vertical, axis);

            Assert.AreEqual(1, OverlayMonitorChoicePolicy.IndexOfSlot(stacked, axis, OverlayMonitorSlot.Start),
                "세로 배치의 시작(위쪽)을 x 정렬로 골랐습니다 — 두 화면의 x가 같아 순서가 정해지지 않습니다.");
            Assert.AreEqual(0, OverlayMonitorChoicePolicy.IndexOfSlot(stacked, axis, OverlayMonitorSlot.End));
        }

        [Test]
        public void 양_끝_두_자리를_고를_수_있다()
        {
            var monitors = RightIsPrimary();
            MonitorArrangementAxis axis = OverlayMonitorChoicePolicy.ResolveAxis(monitors);

            Assert.AreEqual(1, OverlayMonitorChoicePolicy.IndexOfSlot(monitors, axis, OverlayMonitorSlot.Start));
            Assert.AreEqual(0, OverlayMonitorChoicePolicy.IndexOfSlot(monitors, axis, OverlayMonitorSlot.End),
                "오른쪽 끝(End)을 고를 수 없습니다 — 칩 2개 설계가 성립하지 않습니다.");
        }

        /// <summary>
        /// ★ <b>주 화면을 바꿔도 선택이 살아남는다</b> — 좌표 키를 버리고 자리를 저장한 이유 그 자체.
        /// macOS는 주 화면이 언제나 (0,0)이라 주 화면을 오른쪽으로 바꾸면 왼쪽 화면이 음수로 밀린다.
        /// </summary>
        [Test]
        public void 주화면을_바꿔도_고른_자리가_증발하지_않는다()
        {
            string saved = OverlayMonitorChoicePolicy.SlotSaveName(OverlayMonitorSlot.End);

            // 주 화면이 왼쪽일 때의 좌표계
            var before = new List<OsMonitorFact> { M(0f, 0f, 1920f, 1080f, true), M(1920f, 0f, 2560f, 1440f, false) };
            Assert.AreEqual(1, OverlayMonitorChoicePolicy.Resolve(before, saved).Index);

            // 사용자가 주 화면을 오른쪽으로 바꿨다 -> 모든 원점이 재계산된다
            var after = new List<OsMonitorFact> { M(-1920f, 0f, 1920f, 1080f, false), M(0f, 0f, 2560f, 1440f, true) };
            OverlayMonitorChoice choice = OverlayMonitorChoicePolicy.Resolve(after, saved);

            Assert.AreEqual(OverlayMonitorChoiceSource.UserPreferred, choice.Source,
                "주 화면을 바꿨더니 선택이 증발했습니다 — 좌표 키를 저장하면 정확히 이렇게 됩니다.");
            Assert.AreEqual(1, choice.Index, "여전히 '오른쪽 끝'이어야 합니다.");
        }

        [Test]
        public void 모르는_저장값은_고른_적_없음으로_떨어진다()
        {
            Assert.IsFalse(OverlayMonitorChoicePolicy.TryParseSlot("1920,0@2560x1440", out _),
                "중간 빌드가 남긴 옛 좌표 키가 '고른 값'으로 되살아나면 안 됩니다.");
            Assert.IsFalse(OverlayMonitorChoicePolicy.TryParseSlot("Middle", out _));
            Assert.IsTrue(OverlayMonitorChoicePolicy.TryParseSlot("End", out OverlayMonitorSlot slot));
            Assert.AreEqual(OverlayMonitorSlot.End, slot);
        }

        [Test]
        public void 정책은_모니터_3대_이상에도_열려_있다()
        {
            var three = new List<OsMonitorFact>
            {
                M(2560f, 0f, 1920f, 1080f, false),
                M(0f, 0f, 2560f, 1440f, true),
                M(-1920f, 0f, 1920f, 1080f, false),
            };
            Assert.AreEqual(2, OverlayMonitorChoicePolicy.IndexOfLeftmost(three));

            MonitorArrangementAxis axis3 = OverlayMonitorChoicePolicy.ResolveAxis(three);
            Assert.AreEqual(2, OverlayMonitorChoicePolicy.IndexOfSlot(three, axis3, OverlayMonitorSlot.Start));
            Assert.AreEqual(0, OverlayMonitorChoicePolicy.IndexOfSlot(three, axis3, OverlayMonitorSlot.End),
                "3대에서도 양 끝은 정해진다 — 가운데를 못 고르는 것은 UI 고지가 사용자에게 직접 말한다.");
        }

        // ====================================================================
        // ★ 발판 클리핑 == 오버레이가 덮는 화면 (2026-09-02 회귀 잠금)
        // ====================================================================

        [TearDown]
        public void ResetDirectory() => OverlayMonitorDirectory.ResetForTesting();

        /// <summary>
        /// <b>"기본은 왼쪽"을 넣으면서 우리가 만든 회귀 위험</b>을 값으로 잠근다.
        ///
        /// <para>발판 열거가 자르는 사각형과 오버레이가 덮는 사각형이 <b>다른 화면</b>이면
        /// 실제 창 발판이 <b>0개</b>가 되고 합성 안전망만 남는다 — 캐릭터가 남의 창 위에 서지 못한다.
        /// 지금까지 둘이 맞았던 것은 <b>우연</b>이었다(오버레이도 사실상 주 디스플레이에 떴다).</para>
        ///
        /// <para>★ 표본은 <b>주 모니터 ≠ 가장 왼쪽</b>으로 잡는다. 둘이 같은 배치에서는
        /// "주 디스플레이로 자르는" 틀린 구현도 그대로 통과한다 — 이 파일이 반복해서 피하는 함정이다.</para>
        /// </summary>
        [Test]
        public void 발판_클리핑_사각형은_오버레이가_덮는_화면과_같다()
        {
            var monitors = RightIsPrimary();       // 주 모니터 = 오른쪽(1920,0), 가장 왼쪽 = (0,0)
            OverlayMonitorDirectory.ResetForTesting();
            OverlayMonitorDirectory.Publish(monitors);

            Assert.AreNotEqual(OverlayMonitorDirectory.PrimaryIndex, OverlayMonitorDirectory.LeftmostIndex,
                "표본이 잘못됐습니다 — 주 모니터와 가장 왼쪽이 같으면 이 테스트는 아무것도 잡지 못합니다.");

            OverlayMonitorChoice choice = OverlayMonitorDirectory.Resolve();
            Assert.IsTrue(OverlayMonitorDirectory.TryGetOverlayScreenOsRect(out Rect clip),
                "발판 클리핑 사각형을 얻지 못했습니다 — 그러면 양 플랫폼이 옛 폴백(주 디스플레이 / 가상 화면)으로 " +
                "돌아가고 이 회귀가 그대로 살아납니다.");

            Assert.AreEqual(monitors[choice.Index].FullOsRect, clip,
                "발판 클리핑 사각형이 오버레이가 고른 화면과 다릅니다 — 캐릭터가 설 수 있는 창이 " +
                "0개가 되는 경로입니다.");
            Assert.AreEqual(monitors[OverlayMonitorDirectory.LeftmostIndex].FullOsRect, clip,
                "클리핑이 기본값(가장 왼쪽)을 따르지 않았습니다.");
            Assert.AreNotEqual(monitors[OverlayMonitorDirectory.PrimaryIndex].FullOsRect, clip,
                "클리핑이 여전히 <b>주 디스플레이</b>를 보고 있습니다 — 이것이 고치려던 그 결함입니다.");
        }

        /// <summary>
        /// 사용자가 화면을 고르면 <b>발판 클리핑도 함께 따라간다</b>. 창만 옮기고 발판은 안 옮기면
        /// 옮긴 화면에서 캐릭터가 아무 창에도 못 선다.
        /// </summary>
        [Test]
        public void 사용자가_화면을_고르면_발판_클리핑도_따라간다()
        {
            var monitors = RightIsPrimary();
            OverlayMonitorDirectory.ResetForTesting();
            OverlayMonitorDirectory.Publish(monitors);

            Core.AppSettingsModel.SetPreferredOverlayMonitor(
                OverlayMonitorChoicePolicy.SlotSaveName(OverlayMonitorSlot.End));   // 오른쪽 끝을 고른다
            try
            {
                Assert.IsTrue(OverlayMonitorDirectory.TryGetOverlayScreenOsRect(out Rect clip));
                Assert.AreEqual(monitors[0].FullOsRect, clip,
                    "사용자가 고른 화면으로 창은 옮겼는데 발판 클리핑은 기본값에 남았습니다.");
            }
            finally
            {
                Core.AppSettingsModel.SetPreferredOverlayMonitor(null);
            }
        }

        /// <summary>
        /// <b>원점 위생 검사용 사각형은 전체 데스크톱</b>이어야 한다 — 한 화면을 넘기면
        /// 오버레이가 보조 화면에 있을 때 <b>우리가 우리 자신의 원점을 거부</b>한다.
        /// (macOS가 <c>CGDisplayBounds(주 디스플레이)</c>를 넘기고 있던 것이 이번에 드러난 두 번째 회귀다.)
        /// </summary>
        [Test]
        public void 원점_위생_검사용_사각형은_전체_데스크톱이다()
        {
            var monitors = RightIsPrimary();
            OverlayMonitorDirectory.ResetForTesting();
            OverlayMonitorDirectory.Publish(monitors);

            Assert.IsTrue(OverlayMonitorDirectory.TryGetDesktopUnionOsRect(out Rect union));
            Assert.AreEqual(new Rect(0f, 0f, 4480f, 1440f), union,
                "전체 데스크톱 외접 사각형이 아닙니다(0,0~1920x1080 과 1920,0~2560x1440 의 합).");

            Assert.IsTrue(OverlayMonitorDirectory.TryGetOverlayScreenOsRect(out Rect clip));
            Assert.AreNotEqual(union, clip,
                "위생 검사용(전체)과 발판 클리핑용(한 화면)이 같은 값입니다 — 둘은 서로 다른 질문에 " +
                "답하므로 섞이면 한쪽이 반드시 틀립니다.");

            // 오버레이가 보조 화면에 있어도 전체 데스크톱 안이다(= 원점이 거부되지 않는다).
            Assert.IsTrue(union.Overlaps(clip));
        }

        [Test]
        public void 목록이_비면_클리핑은_폴백에_맡긴다()
        {
            OverlayMonitorDirectory.ResetForTesting();
            Assert.IsFalse(OverlayMonitorDirectory.TryGetOverlayScreenOsRect(out _),
                "빈 목록에서 사각형을 지어내면 안 됩니다 — 호출자가 기존 폴백(주 디스플레이 / 가상 화면)을 " +
                "쓰게 두어야 '조회 실패로 멀쩡한 창을 지우지 않는다'는 계약이 유지됩니다.");
            Assert.IsFalse(OverlayMonitorDirectory.TryGetDesktopUnionOsRect(out _));
        }

        [Test]
        public void OS_인덱스를_라이브러리_인덱스로_옮긴다()
        {
            // 라이브러리 목록은 네이티브가 왼쪽부터 정렬해 준다(Phase 0에서 원문 확정).
            var lib = new List<Rect>
            {
                new Rect(-1920f, 0f, 1920f, 1080f),
                new Rect(0f, 0f, 2560f, 1440f),
                new Rect(2560f, 0f, 1920f, 1080f),
            };
            var os = new List<OsMonitorFact>
            {
                M(2560f, 0f, 1920f, 1080f, false),
                M(0f, 0f, 2560f, 1440f, true),
                M(-1920f, 0f, 1920f, 1080f, false),
            };

            Assert.AreEqual(0, OverlayMonitorChoicePolicy.LibraryIndexForOsMonitor(lib, os, 2),
                "두 목록의 순서가 다른데 인덱스를 그대로 썼습니다 — 창이 엉뚱한 화면에 뜹니다.");
            Assert.AreEqual(2, OverlayMonitorChoicePolicy.LibraryIndexForOsMonitor(lib, os, 0));
            Assert.AreEqual(-1, OverlayMonitorChoicePolicy.LibraryIndexForOsMonitor(lib, os, 99));
        }
    }
}
