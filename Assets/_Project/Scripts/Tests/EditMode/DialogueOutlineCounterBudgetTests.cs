using NUnit.Framework;
using StickMate.Dialogue;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 말풍선 글자 뭉갬 회귀 — 사용자 신고(2026-09-01) <b>"텍스트들도 선명하지 않고"</b>.
    ///
    /// ============================================================================
    /// 원인은 회전이 아니라 <b>외곽선</b>이었다 (오프라인 A/B로 확정)
    /// ============================================================================
    /// 후보가 둘이었다: (a) 9.1도 기울기의 회전 리샘플, (b) 0.84pt 외곽선.
    /// uGUI Text + Outline + 회전 파이프라인을 그대로 복제해 각 변수를 <b>단독으로</b> 스윕한 결과
    /// (복제의 유효성은 프로덕션 파라미터에서 실측 1.82px / 복제 1.79px 일치로 확인):
    /// <code>
    ///   기울기 9.1도 -> 0도   : 전이 픽셀 -9%,   속공간 열림 0.000 -> 0.000  (변화 없음)
    ///   외곽선 0.84 -> 0.4pt  : 전이 픽셀 -42%,  속공간 열림 0.000 -> 0.186
    /// </code>
    /// 기울기를 0으로 만들어도 사용자가 본 뭉갬은 <b>한 픽셀도</b> 나아지지 않는다.
    /// 그래서 손글씨 기울기 연출은 유지하고 외곽선만 고쳤다.
    ///
    /// ============================================================================
    /// 이 테스트가 잠그는 불변식
    /// ============================================================================
    /// uGUI <c>Outline</c>은 메시를 네 대각선(±t, ±t)에 복제하므로 글자가 사방으로 t만큼 팽창하고,
    /// <b>속공간은 양쪽에서 좁아져 2t만큼 줄어든다.</b> 그러므로
    /// <code>2 x 외곽선em비율 &lt; 한글 속공간 최소 em비율</code>
    /// 이 깨지는 순간 ㅇ/ㅁ의 속이 <b>원리적으로 반드시</b> 메워진다 — 폰트/화면/배율과 무관하다.
    /// 고치기 전 값(0.06)은 2t = 0.12 > 0.113이라 이 단언에서 <b>빨갛게 떴을 것</b>이다.
    ///
    /// ★ 숫자를 베끼지 않는다(CLAUDE.md): 전부 프로덕션 상수를 참조한다. 폰트를 바꿔
    ///   <see cref="DialogueBubbleRenderer.NarrowestHangulCounterEmRatio"/>를 다시 실측하면
    ///   이 테스트가 그 새 값을 자동으로 지킨다.
    /// </summary>
    public sealed class DialogueOutlineCounterBudgetTests
    {
        private const string LogPrefix = "[말풍선외곽선-TEST]";

        /// <summary>★ 핵심 불변식. 외곽선이 속공간을 통째로 삼키면 안 된다.</summary>
        [Test]
        public void OutlineNeverConsumesTheWholeNarrowestHangulCounter()
        {
            float consumed = DialogueBubbleRenderer.TextOutlineEmRatio * 2f;
            float available = DialogueBubbleRenderer.NarrowestHangulCounterEmRatio;

            Assert.Less(consumed, available,
                $"{LogPrefix} 외곽선이 속공간을 {consumed:F4}em 잡아먹는데 가장 좁은 속공간은 " +
                $"{available:F4}em입니다 — ㅇ/ㅁ의 속이 <b>원리적으로</b> 메워집니다. " +
                "uGUI Outline은 사방으로 t만큼 팽창하므로 속공간은 2t만큼 줄어듭니다. " +
                "고치기 전 값(0.06em)이 정확히 이 상태였습니다(사용자 신고 \"텍스트들도 선명하지 않고\").");
        }

        /// <summary>예산이 1을 넘으면 위 불변식이 자동으로 깨진다 — 유도식의 전제를 못 박는다.</summary>
        [Test]
        public void TheCounterBudgetLeavesAVisibleHole()
        {
            Assert.Less(DialogueBubbleRenderer.CounterClosureBudget, 1f,
                $"{LogPrefix} 속공간 예산이 {DialogueBubbleRenderer.CounterClosureBudget:F2}입니다 — " +
                "1.0은 속공간이 정확히 0이 되는 지점입니다.");
            Assert.Greater(DialogueBubbleRenderer.CounterClosureBudget, 0f,
                $"{LogPrefix} 예산이 0 이하면 외곽선이 사라집니다 — 이 선은 글자와 바탕화면 사이의 " +
                "유일한 분리막입니다(어두운 바탕화면 + 검은 잉크에서 글자가 그대로 사라집니다).");
        }

        /// <summary>
        /// 두께가 <b>유도식에서</b> 나오는가. 누군가 리터럴로 되돌리면(예전이 그랬다) 여기서 걸린다 —
        /// 리터럴이 되는 순간 "왜 그 값인지"가 사라지고, 실제로 그렇게 사라진 채 예산을 넘겨 있었다.
        /// </summary>
        [Test]
        public void OutlineThicknessIsDerivedFromTheMeasurementAndTheBudget()
        {
            float expected = DialogueBubbleRenderer.NarrowestHangulCounterEmRatio
                             * DialogueBubbleRenderer.CounterClosureBudget * 0.5f;
            Assert.AreEqual(expected, DialogueBubbleRenderer.TextOutlineEmRatio, 1e-6f,
                $"{LogPrefix} 외곽선 em비율이 실측(속공간)과 예산에서 유도되지 않고 있습니다 — " +
                "직접 적은 숫자로 되돌아갔다면 그 값이 왜 그 값인지가 다시 사라집니다.");
        }
    }
}
