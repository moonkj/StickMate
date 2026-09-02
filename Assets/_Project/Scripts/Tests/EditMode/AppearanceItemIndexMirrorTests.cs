using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StickMate.Interaction;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-02 qa-regression — <b>PlayMode 테스트가 베껴 적은 아이템 번호</b>가 프로덕션과
    /// 갈라지지 않게 잠근다.
    ///
    /// <para><b>왜 이런 이상한 검사가 필요한가.</b>
    /// <c>Tests/PlayMode/AppearanceNewItemsRenderTests.cs</c>는 <c>FxBubble = 4</c> 같은 값을
    /// 손으로 적어 두고, 주석에 "어긋나면 착용 단언이 즉시 빨개진다"고 <b>거짓 안전 주장</b>을
    /// 달고 있었다. 실제로는 빨개지지 않는다 — 그 단언은
    /// <c>Assert.IsTrue(Wear(slot, index))</c>이고 <c>Wear</c>는 <b>범위 밖 번호</b>에만 false를
    /// 준다. 프로덕션이 번호를 재배치해 4번이 다른 아이템이 되면 <c>Wear</c>는 여전히 true이고,
    /// 그 PlayMode 테스트는 <b>엉뚱한 아이템을 착용한 채 초록</b>이 된다. 사본이 어긋나는 바로 그
    /// 경우가 그 단언의 사각지대다. (<c>Wear</c>는 <b>거부</b> — 범위 밖·미해금 — 은 잡는다.
    /// 못 잡는 것은 <b>번호가 다른 아이템을 가리키게 된 것</b>이다.)</para>
    ///
    /// <para><b>왜 사본을 그냥 없애지 않았나.</b> 없앨 수 없다. <c>Scripts/AssemblyInfo.cs</c>의
    /// <c>InternalsVisibleTo</c>는 <b><c>StickMate.Tests.EditMode</c> 하나뿐</b>이고,
    /// <c>AppearanceShapeBuilder</c>의 번호 상수는 전부 <c>internal</c>이다. 즉 PlayMode 어셈블리는
    /// 그 상수를 <b>물리적으로 참조할 수 없다</b>. (프로덕션 <c>.cs</c>를 고쳐
    /// <c>InternalsVisibleTo("StickMate.Tests.PlayMode")</c>를 추가하면 사본 자체가 사라지지만,
    /// 그것은 프로덕션 변경이라 리더 배정이 필요하다 — 그때까지의 다리가 이 파일이다.)</para>
    ///
    /// <para>그래서 EditMode(= internal이 보이는 쪽)에서 <b>그 파일의 소스 텍스트를 읽어</b>
    /// 프로덕션 상수와 대조한다. 어긋나면 여기가 빨개진다.</para>
    ///
    /// <para><b>양성 대조 필수.</b> 이 검사의 결론은 "어긋난 것이 없다"라는 <b>부재 판정</b>이고,
    /// 이 저장소가 부재 판정으로 당한 거짓 통과가 한둘이 아니다(정규식이 아무것도 못 찾으면
    /// <c>foreach</c>가 0바퀴 돌고 초록이 된다). 그래서 대장이 비지 않았는지,
    /// 파서가 실제로 값을 뽑았는지, 그리고 <b>일부러 틀린 값</b>이 실제로 걸리는지를
    /// 같은 파일에서 증명한다.</para>
    /// </summary>
    public sealed class AppearanceItemIndexMirrorTests
    {
        private const string LogPrefix = "[번호거울]";

        private static string MirrorSourcePath => Path.Combine(
            Application.dataPath, "_Project", "Scripts", "Tests", "PlayMode",
            "AppearanceNewItemsRenderTests.cs");

        /// <summary>PlayMode 테스트가 베낀 이름 → 프로덕션의 진짜 값.
        /// <b>비면 안 된다</b> — 비는 순간 아래 <c>foreach</c>가 아무것도 재지 않는다.</summary>
        private static IReadOnlyDictionary<string, int> MirroredIndices => new Dictionary<string, int>
        {
            { "FxNone", AppearanceShapeBuilder.FxNone },
            { "FxBubble", AppearanceShapeBuilder.FxBubble },
            { "FxLeaf", AppearanceShapeBuilder.FxLeaf },
            { "PetBalloon", AppearanceShapeBuilder.PetBalloon },
            { "PetSnail", AppearanceShapeBuilder.PetSnail },
        };

        /// <summary><c>private const int NAME = N;</c>에서 N을 뽑는다. 못 찾으면 false —
        /// 못 찾은 것을 "일치"로 세지 않는 것이 이 파서의 유일한 임무다.</summary>
        private static bool TryReadIntConst(string source, string name, out int value)
        {
            Match m = Regex.Match(source,
                @"const\s+int\s+" + Regex.Escape(name) + @"\s*=\s*(-?\d+)\s*;");
            if (!m.Success) { value = 0; return false; }
            return int.TryParse(m.Groups[1].Value, out value);
        }

        [Test]
        public void PlayMode가_베낀_아이템_번호가_프로덕션과_같다()
        {
            Assert.IsTrue(File.Exists(MirrorSourcePath),
                $"{LogPrefix} 거울 대상 소스를 찾지 못했습니다: {MirrorSourcePath}. " +
                "파일이 옮겨졌다면 이 검사의 경로를 함께 옮기세요 — 경로가 썩으면 이 검사는 " +
                "아무것도 재지 않으면서 초록이 될 수 있습니다.");

            string src = File.ReadAllText(MirrorSourcePath);
            IReadOnlyDictionary<string, int> expected = MirroredIndices;

            // 대장이 비면 아래 루프가 0바퀴 돈다. 빈 대장을 기대값으로 명시한다(거짓 통과 유형 5).
            Assert.Greater(expected.Count, 0,
                $"{LogPrefix} 거울 대장이 비었습니다 — 검사가 아무것도 재지 않습니다.");

            var mismatched = new List<string>();
            var missing = new List<string>();
            int compared = 0;

            foreach (KeyValuePair<string, int> kv in expected)
            {
                if (!TryReadIntConst(src, kv.Key, out int mirrored))
                {
                    missing.Add(kv.Key);
                    continue;
                }

                compared++;
                if (mirrored != kv.Value) mismatched.Add($"{kv.Key}: 사본={mirrored} 프로덕션={kv.Value}");
            }

            Assert.IsEmpty(missing,
                $"{LogPrefix} 사본에서 상수를 찾지 못했습니다: {string.Join(", ", missing)}. " +
                "이름이 바뀌었거나 지워졌습니다. 지워졌다면(= InternalsVisibleTo로 직접 참조하게 " +
                "바뀌었다면) 이 검사의 대장에서도 그 항목을 빼세요.");

            Assert.AreEqual(expected.Count, compared,
                $"{LogPrefix} 대장 {expected.Count}건 중 {compared}건만 실제로 비교됐습니다.");

            Assert.IsEmpty(mismatched,
                $"{LogPrefix} PlayMode 테스트가 베낀 번호가 프로덕션과 갈라졌습니다:\n  " +
                string.Join("\n  ", mismatched) + "\n" +
                "그 파일의 착용 단언(Assert.IsTrue(Wear(...)))은 이 어긋남을 <b>잡지 못합니다</b> — " +
                "Wear는 범위 밖 번호에만 false를 주므로, 재배치된 번호로는 <b>엉뚱한 아이템을 " +
                "착용한 채 초록</b>이 됩니다. 사본 값을 프로덕션에 맞추세요.");
        }

        /// <summary>★ 양성 대조 — 파서가 실제로 값을 뽑는가, 그리고 <b>틀린 값을 틀렸다고 하는가</b>.
        /// 이것이 빨간불이면 위 검사의 초록은 "일치"가 아니라 "아무것도 못 읽음"이다.</summary>
        [Test]
        public void 양성_대조_파서가_값을_읽고_틀린_값을_잡아낸다()
        {
            string src = File.ReadAllText(MirrorSourcePath);

            Assert.IsTrue(TryReadIntConst(src, "FxBubble", out int bubble),
                $"{LogPrefix} 파서가 FxBubble을 읽지 못했습니다 — 위 검사는 비교를 한 적이 없습니다.");
            Assert.AreEqual(AppearanceShapeBuilder.FxBubble, bubble,
                $"{LogPrefix} 양성 대조 자체가 어긋났습니다.");

            // 음성 대조 — 존재하지 않는 이름은 "일치"가 아니라 "못 찾음"으로 떨어져야 한다.
            Assert.IsFalse(TryReadIntConst(src, "존재하지않는상수이름12345", out _),
                $"{LogPrefix} 없는 상수를 찾았다고 보고했습니다 — 파서가 아무 숫자나 집어오고 있습니다.");

            // 음성 대조 — 대조 로직이 실제로 불일치를 구분하는가(같은 자를 뒤집어 확인).
            Assert.AreNotEqual(AppearanceShapeBuilder.FxBubble, AppearanceShapeBuilder.FxLeaf,
                $"{LogPrefix} 두 아이템 번호가 같습니다 — 번호로 아이템을 가르는 전제가 무너집니다.");
        }

        /// <summary>번호 상수들이 <b>서로 다른가</b>. 프로덕션이 재배치하다 둘을 같은 값으로 만들면
        /// 위 거울 검사는 통과하면서(사본도 같이 고치면) 두 PlayMode 테스트가 같은 아이템을 잰다.</summary>
        [Test]
        public void 신규_4종의_번호가_카테고리_안에서_서로_다르다()
        {
            Assert.AreNotEqual(AppearanceShapeBuilder.FxBubble, AppearanceShapeBuilder.FxLeaf,
                $"{LogPrefix} 물방울과 나뭇잎의 FX 번호가 같습니다.");
            Assert.AreNotEqual(AppearanceShapeBuilder.PetBalloon, AppearanceShapeBuilder.PetSnail,
                $"{LogPrefix} 풍선과 달팽이의 PET 번호가 같습니다.");
            Assert.AreNotEqual(AppearanceShapeBuilder.FxNone, AppearanceShapeBuilder.FxBubble,
                $"{LogPrefix} FX '없음'과 물방울의 번호가 같습니다 — " +
                "그러면 네거티브 컨트롤(FX 없음 = 조각 0개)이 물방울을 재게 됩니다.");
        }
    }
}
