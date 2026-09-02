using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-02 — <c>UiChrome.CategoryTint</c>의 슬롯 → 색 매핑 잠금.
    ///
    /// <para>고친 결함: 옛 코드는 <c>_categoryTints[(int)slot &amp; 3]</c>이었고 <b>태어날 때부터
    /// 틀렸다</b>(회전이 아니다 — 핸드오프의 8칸 표를 7칸 enum에 옮겨 적은 것이고, FACE 자리는 커밋된
    /// 어느 판본에도 존재한 적이 없다). 그 결과 <b>HEAD와 HAIR가 같은 색</b>이었다: 몸 위에서 가장 크게
    /// 겹치는 쌍(design-art 잉크 봉투 IoU 0.371)이자 <c>hidesHair</c>로 기능까지 얽힌 쌍이다.</para>
    ///
    /// <para><b>이 파일이 재는 것은 색 값이 아니라 「같음/다름」의 구조다.</b> hex를 여기 베껴 오면
    /// 팔레트를 조정하는 날 이 테스트가 팔레트를 붙잡고 늘어지고(그건 design-art 소관이다), 무엇보다
    /// 기준과 대상이 갈라져도 아무도 모르게 된다(CLAUDE.md 협업 프로토콜). 기대값은 전부
    /// <c>CategoryTint</c> <b>출력끼리의 관계</b>에서 나온다.</para>
    ///
    /// <para>★ 슬롯이 늘어나는 날 컴파일은 <b>통과한다</b> — 그래서 테스트가 잡아야 한다.
    /// ①<see cref="모든_슬롯이_색을_돌려준다"/>가 인덱스 예외로, ②<see cref="매핑표_길이가_슬롯_수에_잠겨_있다"/>가
    /// 길이로 각각 빨개진다.</para>
    /// </summary>
    public sealed class CategoryTintMappingTests
    {
        private static EquipmentSlot[] AllSlots()
            => (EquipmentSlot[])Enum.GetValues(typeof(EquipmentSlot));

        /// <summary>채널 단위 정확 비교. 같은 배열 원소를 돌려받는 구조라 근사 비교가 필요 없고,
        /// 근사 비교는 "거의 같은 두 색"을 다르다고 통과시켜 이 검사를 무디게 만든다.</summary>
        private static bool Same(Color a, Color b)
            => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        private static string Hex(Color c)
            => $"#{Mathf.RoundToInt(c.r * 255f):X2}{Mathf.RoundToInt(c.g * 255f):X2}{Mathf.RoundToInt(c.b * 255f):X2}";

        // ============================================================================
        // ① 양성 대조 — 이 검사가 「같다」와 「다르다」를 실제로 구별하는가
        // ============================================================================

        /// <summary>
        /// ★ 먼저 자를 교정한다. <see cref="Same"/>가 늘 false를 돌려주면 아래의 "다르다" 단언들이
        /// <b>어떤 팔레트를 넣어도</b> 통과한다(이 저장소가 실제로 겪은 "빈 목록이라 foreach가 아무것도
        /// 안 재고 초록"과 같은 형태).
        /// </summary>
        [Test]
        public void 비교자가_같음과_다름을_구별한다()
        {
            Color head = UiChrome.CategoryTint(EquipmentSlot.Head);

            Assert.IsTrue(Same(head, UiChrome.CategoryTint(EquipmentSlot.Head)),
                "같은 슬롯을 두 번 물었는데 다른 색이라고 답했습니다 — 비교자가 고장났습니다.");
            Assert.IsFalse(Same(head, UiChrome.CategoryTint(EquipmentSlot.Eyes)),
                "HEAD와 EYES가 같은 색이라고 답했습니다. 팔레트가 무너졌거나 비교자가 고장났습니다 — " +
                "어느 쪽이든 아래 검사들의 결과를 믿을 수 없습니다.");
        }

        // ============================================================================
        // ② 전 슬롯 — 하나도 빠짐없이 색이 나오는가
        // ============================================================================

        /// <summary>
        /// 슬롯이 늘면 매핑표가 짧아져 여기서 <b>예외</b>로 터진다. 옛 <c>&amp; 3</c>은 어떤 슬롯을
        /// 넣어도 조용히 답을 내놨다 — 그것이 이 결함이 7슬롯 시대 내내 발견되지 않은 이유다.
        /// </summary>
        [Test]
        public void 모든_슬롯이_색을_돌려준다()
        {
            foreach (EquipmentSlot slot in AllSlots())
            {
                EquipmentSlot captured = slot;
                Color c = default;
                Assert.DoesNotThrow(() => c = UiChrome.CategoryTint(captured),
                    $"슬롯 {captured}에 대응하는 틴트 자리가 없습니다 — 슬롯을 추가했다면 " +
                    "UiChrome의 매핑표도 같은 라운드에 고치십시오.");
                Assert.Greater(c.a, 0f, $"슬롯 {captured}의 틴트가 투명합니다.");
            }
        }

        /// <summary>쓰이는 색이 정확히 4가지인가. 5번째 색이 생기면 카드/아이콘/상세가 나눠 쓰는
        /// 색 체계가 조용히 늘어난 것이고, 그건 design-art 판정을 거쳐야 한다.</summary>
        [Test]
        public void 쓰이는_색은_정확히_네_가지다()
        {
            var distinct = new List<Color>();
            foreach (EquipmentSlot slot in AllSlots())
            {
                Color c = UiChrome.CategoryTint(slot);
                if (!distinct.Exists(x => Same(x, c))) distinct.Add(c);
            }

            Assert.AreEqual(4, distinct.Count,
                "카테고리 틴트가 4색이 아닙니다: " + string.Join(", ", distinct.ConvertAll(Hex)));
        }

        // ============================================================================
        // ③ 짝 — design-art 확정 배정(최대 겹침 0.371 → 0.015)
        // ============================================================================

        /// <summary>
        /// ★ 이 라운드가 고친 바로 그 결함. HEAD와 HAIR는 몸 위에서 가장 크게 겹치는 쌍이고
        /// <c>hidesHair</c>로 기능까지 얽혀 있어, 같은 색이면 카드에서 "이 둘은 한 몸"으로 읽힌다.
        /// </summary>
        [Test]
        public void 가장_크게_겹치는_HEAD와_HAIR는_다른_색이다()
        {
            Color head = UiChrome.CategoryTint(EquipmentSlot.Head);
            Color hair = UiChrome.CategoryTint(EquipmentSlot.Hair);

            Assert.IsFalse(Same(head, hair),
                $"HEAD와 HAIR가 같은 색입니다({Hex(head)}) — 매핑이 옛 산술로 되돌아간 증상입니다. " +
                "슬롯 → 색은 산술이 아니라 UiChrome의 명시 표가 정합니다.");
        }

        /// <summary>확정된 짝만 색을 공유한다: HEAD 단독 / EYES+PET / NECK+HAIR / BACK+FX.</summary>
        [Test]
        public void 확정된_짝만_같은_색을_쓴다()
        {
            var pairs = new[]
            {
                new[] { EquipmentSlot.Eyes, EquipmentSlot.Pet },
                new[] { EquipmentSlot.Neck, EquipmentSlot.Hair },
                new[] { EquipmentSlot.Shoulders, EquipmentSlot.Fx },   // Shoulders = 핸드오프의 BACK
            };

            foreach (EquipmentSlot[] pair in pairs)
            {
                Assert.IsTrue(Same(UiChrome.CategoryTint(pair[0]), UiChrome.CategoryTint(pair[1])),
                    $"{pair[0]}와 {pair[1]}는 같은 색이어야 합니다(design-art 확정 배정). " +
                    $"지금 {Hex(UiChrome.CategoryTint(pair[0]))} vs {Hex(UiChrome.CategoryTint(pair[1]))}.");
            }

            foreach (EquipmentSlot other in AllSlots())
            {
                if (other == EquipmentSlot.Head) continue;
                Assert.IsFalse(Same(UiChrome.CategoryTint(EquipmentSlot.Head), UiChrome.CategoryTint(other)),
                    $"HEAD가 {other}와 색을 공유합니다 — 확정 배정에서 HEAD는 단독입니다.");
            }
        }

        /// <summary>
        /// ★ 짝의 <b>근거</b>가 여전히 참인가. 색이 아니라 <b>정렬층</b>을 잰다 — 근거가 무너졌는데
        /// 색만 남아 있으면 다음 사람이 "왜 이 짝이지"에 답할 수 없다.
        /// <para>라벤더의 짝이 PET이 아니라 FX인 이유가 이것이다: 몸 뒤(음수 층)에 그려지는 것은
        /// BACK 액세서리와 FX 발자국 둘뿐이고 PET은 앞이다. 숫자를 여기 베끼지 않고 <b>렌더러가 실제로
        /// 선언한 상수</b>의 부호만 본다.</para>
        /// </summary>
        [Test]
        public void 라벤더_짝의_근거인_정렬층이_그대로다()
        {
            // internal — InternalsVisibleTo(StickMate.Tests.EditMode) 덕분에 직접 볼 수 있다.
            Assert.Less(AccessoryShapeBuilder.SortBack, 0,
                "BACK 액세서리가 더 이상 몸 뒤에 그려지지 않습니다 — 짝의 근거가 바뀌었습니다.");

            Assert.Less(PrivateIntConst(typeof(CharacterFxRenderer), "SortFootprint"), 0,
                "FX 발자국이 더 이상 몸 뒤에 그려지지 않습니다 — 짝의 근거가 바뀌었습니다.");

            Assert.Greater(PrivateIntConst(typeof(CharacterPetRenderer), "SortDefault"), 0,
                "PET이 몸 뒤로 내려갔습니다 — 그렇다면 라벤더의 짝을 FX가 아니라 PET으로 다시 판정해야 " +
                "합니다(design-art). 색만 그대로 두지 마십시오.");
        }

        // ============================================================================
        // ④ 길이 잠금 — 슬롯이 늘면 컴파일이 아니라 여기서 걸린다
        // ============================================================================

        /// <summary>
        /// 매핑표 길이를 <c>EquipmentSlot</c> 개수에 못박는다. 숫자 7을 여기 적지 않는다 —
        /// enum이 정본이고 표가 따라간다.
        /// </summary>
        [Test]
        public void 매핑표_길이가_슬롯_수에_잠겨_있다()
        {
            var index = (int[])PrivateStatic(typeof(UiChrome), "_categoryTintIndex");
            var tints = (Color[])PrivateStatic(typeof(UiChrome), "_categoryTints");

            Assert.AreEqual(AllSlots().Length, index.Length,
                "슬롯 수와 틴트 매핑표 길이가 어긋났습니다 — 슬롯을 추가/삭제했다면 매핑표도 " +
                "같은 라운드에 고치십시오.");

            var used = new HashSet<int>();
            foreach (int i in index)
            {
                Assert.IsTrue(i >= 0 && i < tints.Length, $"매핑표에 범위 밖 값 {i}이 있습니다.");
                used.Add(i);
            }
            Assert.AreEqual(tints.Length, used.Count,
                "쓰이지 않는 틴트 색이 있습니다 — 색 하나가 죽었거나 짝 배정이 어긋났습니다.");
        }

        // ---- private 상수/필드 읽기. 이름이 바뀌면 조용히 통과하지 않고 여기서 빨개진다. ----

        private static object PrivateStatic(Type type, string name)
        {
            FieldInfo f = type.GetField(name,
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(f,
                $"{type.Name}.{name} 필드를 찾지 못했습니다. 이름이 바뀌었다면 이 테스트도 함께 " +
                "고치십시오 — 못 찾은 것을 '문제 없음'으로 넘기면 이 잠금 전체가 공허해집니다.");
            object v = f.GetValue(null);
            Assert.IsNotNull(v, $"{type.Name}.{name}이 null입니다.");
            return v;
        }

        private static int PrivateIntConst(Type type, string name)
            => (int)PrivateStatic(type, name);
    }
}
