using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Interaction;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ DLC 6팩 팔레트 <b>게이트</b> — 카탈로그가 움직이면 <b>여기서</b> 빨간불이 난다.
    ///
    /// ============================================================================
    /// 왜 이 검사가 필요한가 — 실존하는 위험이다
    /// ============================================================================
    /// design-art 지적: *"카탈로그 색이 움직이면 출하된 DLC 팩 색이 조용히 하한 아래로 내려가는데,
    /// 지금 그걸 잡는 검사가 없다."*
    /// 그리고 <b>2026-09-02 하루에만 카탈로그 색을 두 번 옮겼다</b>(25색 자립 대역 이행 →
    /// W=0.10 채택으로 6색 교체). 가정이 아니라 이미 두 번 일어난 일이다.
    ///
    /// 팩 색은 <b>동결값</b>으로 출하된다(PALETTE_SPEC §14-3): 규칙으로 매번 다시 유도하면
    /// 카탈로그가 바뀔 때마다 <b>이미 유저가 산 팩의 색이 조용히 달라진다</b>. 그건 유저 자산이
    /// 바뀌는 것이다(원칙 3의 정신). 그래서 <b>값은 동결하고 규칙은 게이트로 남긴다</b> —
    /// 이 파일이 그 게이트다.
    ///
    /// ============================================================================
    /// 잠그는 것 넷 (PALETTE_SPEC §14-3)
    /// ============================================================================
    ///  ① 팩 12색이 <b>자립 대역</b> 안에 있는가 (휘도 대역 소속으로 판정)
    ///  ② <c>ItemCatalog.WornColor</c> <b>항등</b>인가 (카드 색 = 몸 색, 바이트 단위)
    ///  ③ 배경 4종에서 <see cref="UiChrome.MinNonTextContrast"/> 이상인가 (대비로 직접 판정)
    ///  ④ ★ <b>카탈로그 전 색과 ΔE ≥ 8.0</b> — 이게 §13의 핵심이고, 카탈로그가 움직이면 여기서 난다
    ///
    /// ①과 ③은 <b>같은 성질을 다른 방법으로</b> 잰다(대역 소속 vs 대비 직접 측정). 둘이 갈라지면
    /// 대역 유도 자체가 틀린 것이므로 그것도 함께 잡는다.
    ///
    /// ============================================================================
    /// 거짓 통과 방지 (TEAM.md §4)
    /// ============================================================================
    ///  · <b>계산기를 먼저 교정한다</b> — 대비(흰/검 21.0 · 동일색 1.0 · #767676/흰 4.5422)와
    ///    ΔE(LAB 흰 L*=100 · 검 L*=0 · 순수 빨강 53.24/80.09/67.20 · dE 동일색 0 · 흰/검 100).
    ///    <b>교정이 깨지면 이 파일의 모든 숫자를 폐기한다.</b>
    ///    (참조 구현: <c>design/art/verify/colorlab.py</c> — 16건 교정을 통과한 도구다.)
    ///  · <b>모든 "없음" 판정에 양성 대조</b>. 처방 C 이전의 <b>실제 충돌값</b>을 그대로 넣어
    ///    같은 판정 함수가 빨개지는지 본다.
    ///  · ★ <b>폴백 탐지</b>: 하한을 올렸는데 미달 건수가 <b>줄어드는</b> 경로가 없는지 훑는다.
    ///    design-art가 자기 유도기에서 겪은 함정이 정확히 그 모양이었다(여유 9.0에서 결과가
    ///    8.06 → 6.21로 나빠졌고, 원인은 해가 없을 때 무제약 값으로 돌아가는 폴백이었다).
    ///  · 카탈로그 쪽은 <b>애셋에서 직접</b> 읽는다(<c>ItemCatalog</c> 순회). 문서를 베끼지 않는다.
    ///
    /// ============================================================================
    /// ★ 이 파일이 hex를 들고 있는 이유 (숨기지 않고 적는다)
    /// ============================================================================
    /// 팩 12색은 <b>아직 프로덕션에 없다</b>. 매니페스트(<c>ScriptableObject</c>)는 DLC 조형 라운드의
    /// 산출물이고, 이번 라운드의 파일 경계 밖이다. 그래서 <see cref="FrozenPacks"/>는
    /// "프로덕션 상수의 사본"이 <b>아니라</b> design-art가 확정한 <b>동결 대장</b> 그 자체다
    /// (PALETTE_SPEC §13-3 처방 C). 매니페스트가 생기면 이 표는 <b>거기서 읽도록 바뀌어야 하고</b>,
    /// 그때 이 게이트는 대장이 아니라 매니페스트를 잰다.
    /// </summary>
    public sealed class PackPaletteGateTests
    {
        private const string LogPrefix = "[팩팔레트]";

        /// <summary>
        /// ★ 카탈로그 색과 팩 색 사이에 지키기로 한 <b>여유</b>(PALETTE_SPEC §13-3 처방 C).
        /// <para>왜 8.0인가(§13-4): 8.2가 측정값으로는 더 좋지만(8.30 &gt; 8.06) 해가 존재하는 상한이
        /// <b>8.2~8.4 사이</b>라 벼랑 끝이다. 8.0은 변별 하한보다 2.6% 위이면서 벼랑에서 두 칸 앞이다.
        /// ΔE 0.24를 주고 규칙의 성립 여유를 산다.</para>
        /// </summary>
        private const float PackCatalogDeltaEFloor = 8.0f;

        /// <summary>"나란히 놓았을 때 다른 색으로 보이는가"의 하한(PALETTE_SPEC §3-3).
        /// 팩 12색 <b>내부</b>는 이 자를 쓴다 — 팩끼리는 카탈로그만큼 벌릴 필요가 없다.</summary>
        private const float DiscriminationFloor = 7.8f;

        /// <summary>동결 대장 — 색상각 하나에서 유도된 주색/보조색. 값은 PALETTE_SPEC §13-3 표.
        /// 색상각을 함께 들고 있는 것은 <b>대장 자체의 오타를 잡기 위해서다</b>(hex를 잘못 옮기면
        /// 색상각이 어긋난다).</summary>
        private static readonly (string Name, float Hue, int Primary, int Secondary)[] FrozenPacks =
        {
            ("오피스 워커", 222f, 0x456ECC, 0x6080CC),
            ("사이버 아포칼립스", 172f, 0x009682, 0x518C84),
            ("네온 낙서", 312f, 0xCC1BA9, 0x9C5A8E),
            ("스포츠", 8f, 0xCC3F29, 0x9E655C),   // 표시명 정본: PACK_THEME_SPEC.md §1-1(「스포츠 이펙트」 드리프트 정정, R-3′)
            ("컬러 잉크", 268f, 0x9768CC, 0x8563AB),
            ("밀리터리", 80f, 0x639400, 0x798C51),
        };

        // ============================================================================
        // 0. 계산기 교정 — 이게 깨지면 아래 숫자는 전부 무효다
        // ============================================================================

        [Test]
        public void 계산기가_알려진_값을_낸다()
        {
            AssertCalculatorsCalibrated();
        }

        private static void AssertCalculatorsCalibrated()
        {
            // (a) 대비 — 프로덕션 함수를 그대로 쓴다.
            Assert.AreEqual(21.0f, UiChrome.ContrastRatio(Color.white, Color.black), 0.0005f,
                $"{LogPrefix} 흰/검 대비가 21.0이 아닙니다 — 이 파일의 모든 판정을 폐기하십시오.");
            Assert.AreEqual(1.0f, UiChrome.ContrastRatio(Color.white, Color.white), 0.0005f,
                $"{LogPrefix} 동일색 대비가 1.0이 아닙니다.");
            Assert.AreEqual(4.5422f, UiChrome.ContrastRatio(Hex(0x767676), Color.white), 0.0005f,
                $"{LogPrefix} #767676 / 흰색 대비가 WCAG 기준값 4.5422가 아닙니다.");

            // (b) ΔE — 이 파일이 들고 있는 계산기. 프로덕션에 CIELAB이 없으므로 여기서 만들고,
            //     외부에서 검증 가능한 값으로만 교정한다(colorlab.py와 같은 교정표).
            Vector3 white = Lab(Color.white), black = Lab(Color.black), red = Lab(Hex(0xFF0000));
            Assert.AreEqual(100f, white.x, 0.01f, $"{LogPrefix} LAB 흰 L*가 100이 아닙니다.");
            Assert.AreEqual(0f, white.y, 0.01f, $"{LogPrefix} LAB 흰 a*가 0이 아닙니다.");
            Assert.AreEqual(0f, white.z, 0.01f, $"{LogPrefix} LAB 흰 b*가 0이 아닙니다.");
            Assert.AreEqual(0f, black.x, 0.01f, $"{LogPrefix} LAB 검 L*가 0이 아닙니다.");
            Assert.AreEqual(53.24f, red.x, 0.02f, $"{LogPrefix} LAB 순수 빨강 L*가 53.24가 아닙니다.");
            Assert.AreEqual(80.09f, red.y, 0.05f, $"{LogPrefix} LAB 순수 빨강 a*가 80.09가 아닙니다.");
            Assert.AreEqual(67.20f, red.z, 0.05f, $"{LogPrefix} LAB 순수 빨강 b*가 67.20이 아닙니다.");
            Assert.AreEqual(0f, DeltaE(Color.white, Color.white), 1e-4f, $"{LogPrefix} 동일색 ΔE가 0이 아닙니다.");
            Assert.AreEqual(100f, DeltaE(Color.white, Color.black), 0.01f, $"{LogPrefix} 흰/검 ΔE가 100이 아닙니다.");
        }

        // ============================================================================
        // 1. 동결 대장 자체가 정직한가 — 오타 하나가 아래 전부를 무의미하게 만든다
        // ============================================================================

        /// <summary>hex를 잘못 옮기면 색상각이 어긋난다. 대장이 스스로를 검산한다.</summary>
        [Test]
        public void 동결_대장의_색이_선언된_색상각에_있다()
        {
            var failures = new List<string>();
            int judged = 0;

            foreach ((string name, float hue, int primary, int secondary) in FrozenPacks)
            {
                foreach ((string role, int hex) in new[] { ("주색", primary), ("보조색", secondary) })
                {
                    judged++;
                    Color.RGBToHSV(Hex(hex), out float h, out float s, out _);
                    if (s <= 0f)
                    {
                        failures.Add($"  {name} {role} {Show(Hex(hex))}가 무채색입니다 — 색상각이 없습니다.");
                        continue;
                    }

                    float degrees = h * 360f;
                    float delta = Mathf.Abs(Mathf.DeltaAngle(degrees, hue));
                    if (delta <= 1f) continue;
                    failures.Add($"  {name} {role} {Show(Hex(hex))} 색상각 {degrees:F2}° " +
                                 $"(대장 선언 {hue:F0}°, 차이 {delta:F2}°)");
                }
            }

            Assert.AreEqual(FrozenPacks.Length * 2, judged, $"{LogPrefix} 잰 색이 {judged}개뿐입니다.");
            Assert.IsEmpty(failures,
                $"{LogPrefix} 동결 대장이 스스로와 어긋납니다({failures.Count}건) — hex를 옮겨 적다 " +
                "틀렸을 수 있습니다. 이 파일의 다른 판정도 전부 의심하십시오.\n" + string.Join("\n", failures));
        }

        /// <summary>여섯 팩이 "한 게임"인가 — <b>밝기로는 구분되지 않아야</b> 한다(PALETTE_SPEC §13-8).
        /// 팩끼리는 색상각만 다르고 밝기는 한 대역이다. 새 팩이 이 대역 밖으로 나가면 나란히 놓았을 때
        /// 한쪽이 튄다.</summary>
        [Test]
        public void 여섯_팩이_밝기로는_구분되지_않는다_한_대역이다()
        {
            AssertCalculatorsCalibrated();

            Color brightest = default, darkest = default;
            float hi = -1f, lo = float.MaxValue;
            foreach ((string packName, Color c) in PackColors())
            {
                float l = UiChrome.RelativeLuminance(c);
                if (l > hi) { hi = l; brightest = c; }
                if (l < lo) { lo = l; darkest = c; }
            }

            float spread = UiChrome.ContrastRatio(brightest, darkest);
            Assert.Less(spread, UiChrome.MinNonTextContrast,
                $"{LogPrefix} 가장 밝은 팩색 {Show(brightest)}과 가장 어두운 {Show(darkest)}의 대비가 " +
                $"{spread:F2}:1입니다 — 비텍스트 하한을 넘으면 두 팩이 <b>밝기로</b> 구분되기 시작하고, " +
                "그 순간 '한 상자, 여섯 각도'라는 통일 축이 깨집니다.");
            Debug.Log($"{LogPrefix} 팩 밝기 폭 {spread:F4}:1 (L {lo:F4}~{hi:F4}).");
        }

        // ============================================================================
        // 2. 본안 ④ — 카탈로그가 움직이면 여기서 난다
        // ============================================================================

        /// <summary>
        /// ★ 이 파일의 존재 이유. <b>카탈로그 전 색</b>(애셋에서 읽는다)과 팩 12색의 ΔE가
        /// <see cref="PackCatalogDeltaEFloor"/> 이상인가.
        /// <para>잉크 표식 두 색도 <b>면제하지 않는다</b> — 면제는 그 자체로 구멍이고, 실측상
        /// 최소 ΔE가 면제 유무로 바뀌지 않는다(잉크 표식은 팩색에서 22.6·39.3만큼 멀다).</para>
        /// </summary>
        [Test]
        public void 팩_12색이_카탈로그_전_색과_충분히_떨어져_있다()
        {
            AssertCalculatorsCalibrated();

            List<string> failures = JudgeAgainstCatalog(PackColors(), PackCatalogDeltaEFloor, out int pairs, out float worst);

            Assert.Greater(pairs, 0, $"{LogPrefix} 잰 쌍이 0건입니다 — 카탈로그 열거가 비었습니다.");
            Assert.IsEmpty(failures,
                $"{LogPrefix} 카탈로그와 ΔE {PackCatalogDeltaEFloor:F1} 미만인 쌍이 {failures.Count}건입니다. " +
                "★ 카탈로그 색을 옮겼다면 팩 색을 <b>다시 유도해야</b> 합니다(PALETTE_SPEC §13-6 마지막 문단). " +
                "출하된 팩이 있다면 그 색을 조용히 바꾸는 것은 유저 자산을 바꾸는 것이므로 리더 판단이 필요합니다.\n" +
                string.Join("\n", failures));

            Debug.Log($"{LogPrefix} 카탈로그↔팩 {pairs}쌍 최소 ΔE {worst:F4} (하한 {PackCatalogDeltaEFloor:F1}).");
        }

        /// <summary>팩 12색 <b>내부</b>는 변별 하한을 넘는가 — 두 팩이 같은 색으로 읽히면 안 된다.</summary>
        [Test]
        public void 팩_12색이_서로_변별된다()
        {
            AssertCalculatorsCalibrated();

            List<(string Name, Color Color)> colors = PackColors();
            var failures = new List<string>();
            float worst = float.MaxValue;
            int pairs = 0;

            for (int i = 0; i < colors.Count; i++)
            {
                for (int j = i + 1; j < colors.Count; j++)
                {
                    pairs++;
                    float d = DeltaE(colors[i].Color, colors[j].Color);
                    worst = Mathf.Min(worst, d);
                    if (d >= DiscriminationFloor) continue;
                    failures.Add($"  {colors[i].Name} {Show(colors[i].Color)} ↔ {colors[j].Name} " +
                                 $"{Show(colors[j].Color)} = ΔE {d:F2} (하한 {DiscriminationFloor:F1})");
                }
            }

            Assert.AreEqual(colors.Count * (colors.Count - 1) / 2, pairs, $"{LogPrefix} 쌍 수가 어긋납니다.");
            Assert.IsEmpty(failures, $"{LogPrefix} 변별 미달 {failures.Count}쌍.\n" + string.Join("\n", failures));
            Debug.Log($"{LogPrefix} 팩 내부 {pairs}쌍 최소 ΔE {worst:F4}.");
        }

        /// <summary>★ 등급 램프 4색과도 안 섞이는가. 카드 안에서 <b>리본과 팩색이 같은 화면</b>에
        /// 놓인다 — 둘이 붙으면 유저가 등급을 팩 색으로 오해한다.</summary>
        [Test]
        public void 팩_12색이_등급_램프와도_섞이지_않는다()
        {
            AssertCalculatorsCalibrated();

            var failures = new List<string>();
            float worst = float.MaxValue;
            int pairs = 0;

            foreach (ItemRarity r in System.Enum.GetValues(typeof(ItemRarity)))
            {
                Color ramp = UiChrome.RarityColor(r);
                foreach ((string name, Color pack) in PackColors())
                {
                    pairs++;
                    float d = DeltaE(ramp, pack);
                    worst = Mathf.Min(worst, d);
                    if (d >= DiscriminationFloor) continue;
                    failures.Add($"  등급 {ItemCatalog.RarityName(r)} {Show(ramp)} ↔ {name} " +
                                 $"{Show(pack)} = ΔE {d:F2}");
                }
            }

            Assert.Greater(pairs, 0, $"{LogPrefix} 잰 쌍이 0건입니다.");
            Assert.IsEmpty(failures, $"{LogPrefix} 등급 램프와 섞이는 팩색 {failures.Count}건.\n" +
                                     string.Join("\n", failures));
            Debug.Log($"{LogPrefix} 등급↔팩 {pairs}쌍 최소 ΔE {worst:F4}.");
        }

        // ============================================================================
        // 3. 본안 ①②③ — 팩 색이 몸 위에서도 사는가
        // ============================================================================

        /// <summary>③ 배경 4종에서 비텍스트 하한을 넘는가(대비로 직접 판정).</summary>
        [Test]
        public void 팩_12색이_배경_넷에서_보인다()
        {
            AssertCalculatorsCalibrated();

            var failures = new List<string>();
            int judged = 0;
            foreach ((string name, Color c) in PackColors())
            {
                foreach ((string bgName, Color bg) in Backdrops())
                {
                    judged++;
                    float cr = UiChrome.ContrastRatio(c, bg);
                    if (cr >= UiChrome.MinNonTextContrast) continue;
                    failures.Add($"  {name} {Show(c)} vs {bgName} {Show(bg)} = {cr:F2}:1 " +
                                 $"(하한 {UiChrome.MinNonTextContrast:F1})");
                }
            }

            Assert.AreEqual(FrozenPacks.Length * 2 * Backdrops().Length, judged,
                $"{LogPrefix} 잰 조합이 {judged}건입니다 — 열거가 샙니다.");
            Assert.IsEmpty(failures, $"{LogPrefix} 배경 미달 {failures.Count}건.\n" + string.Join("\n", failures));
        }

        /// <summary>① 같은 성질을 <b>다른 방법으로</b> 잰다 — 색마다 대비를 재는 것이 아니라
        /// 배경 넷에서 <b>대역 하나</b>를 유도하고 그 안에 12색이 전부 들어가는지 본다.
        /// ③과 갈라지면 대역 유도식이 틀린 것이므로 그것도 함께 잡는다.</summary>
        [Test]
        public void 팩_12색이_자립_대역_안에_있다_대비와_같은_답이_나온다()
        {
            AssertCalculatorsCalibrated();

            DeriveSelfStandingBand(out float floor, out float ceil, out string floorSource, out string ceilSource);

            // ★ design-art가 1차 유도에서 놓쳤던 기하학을 여기서 잠근다 — 대역을 막는 것은
            //   흑백 <b>극단</b>이 아니라 <b>중간 밝기 무대</b>다(대비는 자기 휘도에 가까운 배경에서 0으로 간다).
            Assert.AreNotEqual("어두운 바탕화면(극단 검은색)", floorSource,
                $"{LogPrefix} 대역의 아래를 막는 것이 무대가 아니라 검은 바탕화면입니다 — " +
                "누군가 배경을 흑백 둘로 줄였을 수 있습니다.");
            Assert.AreNotEqual("밝은 바탕화면(극단 흰색)", ceilSource,
                $"{LogPrefix} 대역의 위를 막는 것이 무대가 아니라 흰 바탕화면입니다.");

            var outside = new List<string>();
            foreach ((string name, Color c) in PackColors())
            {
                float l = UiChrome.RelativeLuminance(c);
                if (l >= floor && l <= ceil) continue;
                outside.Add($"  {name} {Show(c)} L={l:F4} (대역 [{floor:F4}, {ceil:F4}])");
            }

            // ★ 두 방법이 같은 답을 내야 한다. 갈라지면 한쪽이 거짓말을 하고 있다.
            var byContrast = new List<string>();
            foreach ((string name, Color c) in PackColors())
            {
                foreach ((string bgName, Color bg) in Backdrops())
                {
                    if (UiChrome.ContrastRatio(c, bg) >= UiChrome.MinNonTextContrast) continue;
                    byContrast.Add(name);
                    break;
                }
            }

            Assert.AreEqual(byContrast.Count, outside.Count,
                $"{LogPrefix} 대역 소속으로는 {outside.Count}건, 대비 직접 측정으로는 {byContrast.Count}건이 " +
                "미달입니다 — 두 방법이 갈라졌다면 대역 유도식이 틀렸습니다.");
            Assert.IsEmpty(outside, $"{LogPrefix} 자립 대역 밖 {outside.Count}건.\n" + string.Join("\n", outside));
            Debug.Log($"{LogPrefix} 자립 대역 L ∈ [{floor:F4}({floorSource}), {ceil:F4}({ceilSource})], 팩 12색 전부 소속.");
        }

        /// <summary>
        /// 배경 넷에서 <b>자립 대역</b>(어느 배경 위에서도 비텍스트 하한을 넘는 휘도 구간)을 유도한다.
        ///
        /// <para>어떤 배경 위에서든 하한을 넘으려면 그 배경보다 <b>충분히 밝거나</b>(<see cref="BandFloor"/>)
        /// <b>충분히 어두워야</b>(<see cref="BandCeil"/>) 한다. 그래서 배경을 휘도순으로 세우고
        /// "여기까지는 내가 더 밝다 / 여기부터는 내가 더 어둡다"의 <b>분할점</b>을 전부 훑는다.
        /// 색은 [0, 1] 안에 있으므로 그 밖으로 나가는 구간은 실재하지 않는다.</para>
        ///
        /// <para>★ <b>유효한 분할이 하나뿐임을 단언한다.</b> 둘 이상이면 "자립 대역"이라는 말 자체가
        /// 애매해지고, 하나도 없으면 배경 넷을 동시에 만족하는 색이 존재하지 않는다는 뜻이다.</para>
        /// </summary>
        private static void DeriveSelfStandingBand(out float floor, out float ceil,
            out string floorSource, out string ceilSource)
        {
            var sorted = new List<(string Name, Color Color)>(Backdrops());
            sorted.Sort((a, b) => UiChrome.RelativeLuminance(a.Color).CompareTo(UiChrome.RelativeLuminance(b.Color)));

            floor = 0f; ceil = 1f; floorSource = null; ceilSource = null;
            int valid = 0;

            for (int split = 0; split <= sorted.Count; split++)
            {
                float lo = 0f, hi = 1f;
                string loSource = "휘도 하한(0)", hiSource = "휘도 상한(1)";

                for (int i = 0; i < split; i++)
                {
                    float f = BandFloor(sorted[i].Color);
                    if (f <= lo) continue;
                    lo = f;
                    loSource = sorted[i].Name;
                }
                for (int i = split; i < sorted.Count; i++)
                {
                    float c = BandCeil(sorted[i].Color);
                    if (c >= hi) continue;
                    hi = c;
                    hiSource = sorted[i].Name;
                }

                if (lo >= hi) continue;
                valid++;
                floor = lo; ceil = hi; floorSource = loSource; ceilSource = hiSource;
            }

            Assert.AreEqual(1, valid,
                $"{LogPrefix} 자립 대역 후보가 {valid}개입니다(기대 1). 0개면 배경 넷을 동시에 만족하는 " +
                "휘도가 없다는 뜻이고, 2개 이상이면 '대역'이 하나로 정해지지 않습니다.");
        }

        /// <summary>② <c>WornColor</c> 항등 — 카드에서 본 색과 몸에 칠해진 색이 <b>바이트 단위로</b> 같다.
        /// 이게 서면 "카드엔 색이 있는데 착용하면 다른 색"이라는 결함군이 구조적으로 사라진다.</summary>
        [Test]
        public void 팩_12색이_WornColor_항등이다()
        {
            var failures = new List<string>();
            int judged = 0;
            foreach ((string name, Color c) in PackColors())
            {
                judged++;
                Color worn = ItemCatalog.WornColor(c, Color.white);
                if (SameByte(worn, c)) continue;
                failures.Add($"  {name} 카드 {Show(c)} -> 몸 {Show(worn)}");
            }

            Assert.AreEqual(FrozenPacks.Length * 2, judged, $"{LogPrefix} 잰 색이 {judged}개뿐입니다.");
            Assert.IsEmpty(failures,
                $"{LogPrefix} WornColor가 팩색을 바꾸는 경우 {failures.Count}건 — 카드와 몸이 갈라집니다.\n" +
                string.Join("\n", failures));
        }

        // ============================================================================
        // 4. ★ 양성 대조 — 처방 C 이전의 <b>실제</b> 충돌값으로 판정을 시험한다
        // ============================================================================

        /// <summary>
        /// 이 세 값은 발명한 것이 아니라 <b>처방 C 이전에 실제로 트리에 있던 충돌</b>이다
        /// (PALETTE_SPEC §13-1). 같은 판정 함수에 넣어 빨간불이 나는지 본다 —
        /// 대조용 판정기를 따로 짜면 그건 대조가 아니다.
        /// </summary>
        [TestCase(0x8D56CC, "「컬러 잉크」 옛 주색 — 카탈로그 #955CCC(요정날개·날개)와 ΔE 4.26")]
        [TestCase(0x5C709E, "「오피스 워커」 옛 보조색 — 카탈로그 #587398과 6.21 · #5577AE와 6.33")]
        [TestCase(0xCC3D3D, "「컬러 잉크」를 0°로 돌렸을 때의 주색 — 카탈로그 #CC3C3C(망토)와 ΔE 0.54")]
        public void 양성_대조_옛_충돌색을_게이트가_잡는다(int hex, string why)
        {
            AssertCalculatorsCalibrated();

            var probe = new List<(string, Color)> { ("대조", Hex(hex)) };
            List<string> failures = JudgeAgainstCatalog(probe, PackCatalogDeltaEFloor, out int pairs, out float worst);

            Assert.Greater(pairs, 0, $"{LogPrefix} 대조가 카탈로그와 한 쌍도 비교하지 못했습니다.");
            Assert.IsNotEmpty(failures,
                $"{LogPrefix} ★대조 실패 — {Show(Hex(hex))}({why})를 게이트가 놓쳤습니다(최소 ΔE {worst:F2}). " +
                "이 파일이 낸 모든 '위반 0건'을 폐기하십시오.");
            Debug.Log($"{LogPrefix} 대조 통과 — {Show(Hex(hex))} 최소 ΔE {worst:F2}:{failures[0]}");
        }

        /// <summary>대조가 <b>무엇이든</b> 빨개지는 상태가 아님을 보인다. 카탈로그에서 멀찍이 떨어진
        /// 값(순수 자홍)은 통과해야 한다 — 통과하지 않으면 위 대조들의 빨간불은 의미가 없다.</summary>
        [Test]
        public void 양성_대조_멀리_떨어진_색은_통과한다()
        {
            AssertCalculatorsCalibrated();

            var probe = new List<(string, Color)> { ("먼 색", Hex(0xFF00FF)) };
            List<string> failures = JudgeAgainstCatalog(probe, PackCatalogDeltaEFloor, out _, out float worst);

            Assert.IsEmpty(failures,
                $"{LogPrefix} ★대조 실패 — 카탈로그에서 먼 색(#FF00FF, 최소 ΔE {worst:F2})까지 잡았습니다. " +
                "게이트가 무엇이든 빨갛게 만드는 상태입니다.");
            Debug.Log($"{LogPrefix} 대조 통과 — #FF00FF 최소 ΔE {worst:F2}로 통과.");
        }

        /// <summary>배경/항등 판정에도 대조를 건다. ΔE만 잠그고 나머지가 죽어 있으면
        /// "넷을 잠갔다"가 거짓말이 된다.</summary>
        [Test]
        public void 양성_대조_배경과_항등_판정도_실제로_문다()
        {
            AssertCalculatorsCalibrated();

            // (a) 배경 — 종이 무대에서 사라지는 값(흑백 극단만 보면 통과한다. ItemPaletteBandGateTests와 같은 함정).
            Color trap = Hex(0x7690CC);
            Assert.GreaterOrEqual(UiChrome.ContrastRatio(trap, Color.white), UiChrome.MinNonTextContrast,
                $"{LogPrefix} 함정 색이 흰 바탕에서 이미 미달입니다 — 대조가 성립하지 않습니다.");
            Assert.Less(UiChrome.ContrastRatio(trap, Backdrops()[2].Color), UiChrome.MinNonTextContrast,
                $"{LogPrefix} ★대조 실패 — {Show(trap)}이 종이 무대에서도 통과했습니다.");

            // (b) 항등 — 채도 하한 미달 색은 WornColor가 반드시 바꾼다.
            Color grey = Hex(0x6E7176);
            Assert.IsFalse(SameByte(ItemCatalog.WornColor(grey, Color.white), grey),
                $"{LogPrefix} ★대조 실패 — 채도 하한 미달 색 {Show(grey)}를 WornColor가 그대로 뒀습니다.");

            // (c) 대역 — 자립 대역 밖 값이 실제로 밖으로 판정되는가.
            DeriveSelfStandingBand(out float floor, out float ceil, out _, out _);
            Assert.Less(floor, ceil, $"{LogPrefix} 자립 대역이 비었습니다.");

            foreach ((string name, Color c) in new[]
                     {
                         ("흰색", Color.white),
                         ("검정", Color.black),
                         ("함정 #7690CC", trap),
                     })
            {
                float l = UiChrome.RelativeLuminance(c);
                Assert.IsFalse(l >= floor && l <= ceil,
                    $"{LogPrefix} ★대조 실패 — {name}(L={l:F4})이 자립 대역 " +
                    $"[{floor:F4}, {ceil:F4}] 안으로 판정됐습니다.");
            }
            Debug.Log($"{LogPrefix} 대조 통과 — 자립 대역 [{floor:F4}, {ceil:F4}]이 흰색·검정·함정색을 전부 밖으로 판정.");
        }

        // ============================================================================
        // 5. ★ 폴백 탐지 — "조건을 강화했는데 결과가 좋아지는" 경로가 없는가
        // ============================================================================

        /// <summary>
        /// design-art가 자기 유도기에서 겪은 함정(PALETTE_SPEC §13-4): 여유를 9.0으로 <b>올리자</b>
        /// 결과가 8.06 → 6.21로 <b>나빠졌다</b>. 해가 없을 때 조용히 무제약 값으로 되돌아가는 폴백 때문이었다.
        /// <para>같은 형태가 이 게이트 안에 없는지 본다 — 하한을 올리면 미달 건수는 <b>절대 줄어들 수 없다</b>.
        /// 줄어드는 지점이 있으면 판정 어딘가에 조건 분기(=폴백)가 숨어 있다.</para>
        /// <para>그리고 <b>측정값 자체는 하한과 무관</b>해야 한다. 하한을 바꿨는데 최소 ΔE가 달라지면
        /// 그건 판정이 값을 고르고 있다는 뜻이다 — 바로 그 함정의 정의다.</para>
        /// </summary>
        [Test]
        public void 하한을_올리면_미달이_줄어드는_경로가_없다()
        {
            AssertCalculatorsCalibrated();

            List<(string, Color)> packs = PackColors();
            int previous = -1;
            float firstWorst = float.NaN;
            var trace = new List<string>();

            for (float floor = 2f; floor <= 40f + 1e-4f; floor += 0.5f)
            {
                List<string> failures = JudgeAgainstCatalog(packs, floor, out _, out float worst);
                trace.Add($"{floor:F1}->{failures.Count}");

                Assert.GreaterOrEqual(failures.Count, previous,
                    $"{LogPrefix} 하한을 {floor:F1}로 올렸더니 미달이 {previous} -> {failures.Count}로 줄었습니다. " +
                    "제약을 강화했는데 결과가 좋아졌다면 판정 안에 폴백이 숨어 있습니다(§13-4).\n" +
                    string.Join(" ", trace));
                previous = failures.Count;

                if (float.IsNaN(firstWorst)) firstWorst = worst;
                Assert.AreEqual(firstWorst, worst, 1e-4f,
                    $"{LogPrefix} 하한을 {floor:F1}로 바꿨더니 측정된 최소 ΔE가 {firstWorst:F4} -> {worst:F4}로 " +
                    "달라졌습니다 — 판정이 하한에 따라 값을 고르고 있습니다.");
            }

            Assert.Greater(previous, 0,
                $"{LogPrefix} 하한을 40까지 올려도 미달이 0건입니다 — 판정이 아무것도 재지 않고 있습니다.");
            Debug.Log($"{LogPrefix} 하한 스윕 단조 확인(최소 ΔE {firstWorst:F4} 불변) — {string.Join(" ", trace)}");
        }

        // ============================================================================
        // 판정 — 본안과 대조가 같은 함수를 쓴다
        // ============================================================================

        /// <summary>후보 색들을 <b>카탈로그 전 색</b>과 대조한다. 하한을 인자로 받는 것은 폴백 스윕이
        /// 같은 함수를 타야 하기 때문이다. <b>측정(최소 ΔE)은 하한과 무관하게 계산된다.</b></summary>
        private static List<string> JudgeAgainstCatalog(List<(string Name, Color Color)> candidates,
            float floor, out int pairs, out float worst)
        {
            var failures = new List<string>();
            pairs = 0;
            worst = float.MaxValue;

            foreach ((string name, Color candidate) in candidates)
            {
                foreach ((string owner, Color catalog) in CatalogColors())
                {
                    pairs++;
                    float d = DeltaE(candidate, catalog);
                    if (d < worst) worst = d;
                    if (d >= floor) continue;
                    failures.Add($"  {name} {Show(candidate)} ↔ 카탈로그 {Show(catalog)}({owner}) = " +
                                 $"ΔE {d:F2} (하한 {floor:F1})");
                }
            }
            return failures;
        }

        // ============================================================================
        // 열거 — 카탈로그는 애셋에서, 팩은 동결 대장에서
        // ============================================================================

        private static List<(string Name, Color Color)> _catalogColors;

        /// <summary>카탈로그가 실제로 쓰는 <b>모든</b> 조각 색(중복 제거). 문서를 베끼지 않고
        /// <see cref="ItemCatalog"/>를 순회한다 — 애셋이 바뀌면 이 목록이 따라 바뀌고, 그게 이 게이트의 요점이다.</summary>
        private static List<(string Name, Color Color)> CatalogColors()
        {
            if (_catalogColors != null) return _catalogColors;

            var list = new List<(string, Color)>();
            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                ItemCatalogEntry e = ItemCatalog.At(i);
                if (e?.Icon == null) continue;
                for (int p = 0; p < e.Icon.Length; p++)
                {
                    Color c = e.Icon[p].Color;
                    bool duplicate = false;
                    foreach ((string seenOwner, Color seen) in list)
                    {
                        if (!SameByte(seen, c)) continue;
                        duplicate = true;
                        break;
                    }
                    if (!duplicate) list.Add((e.Id, c));
                }
            }
            _catalogColors = list;
            return _catalogColors;
        }

        private static List<(string Name, Color Color)> PackColors()
        {
            var list = new List<(string, Color)>();
            foreach ((string name, float hue, int primary, int secondary) in FrozenPacks)
            {
                list.Add(($"{name} 주색", Hex(primary)));
                list.Add(($"{name} 보조색", Hex(secondary)));
            }
            return list;
        }

        private static (string Name, Color Color)[] _backdrops;

        /// <summary>장비/팩 색이 실제로 놓이는 배경 넷. <see cref="ItemPaletteBandGateTests"/>와 같은
        /// 정의이고, 두 무대 색은 프로덕션(<see cref="CharacterPortraitStage.ResolveBackdropColor"/>)이 정한다.</summary>
        private static (string Name, Color Color)[] Backdrops()
        {
            if (_backdrops != null) return _backdrops;

            var blackInk = ScriptableObject.CreateInstance<StickConfig>();
            blackInk.SetRuntimeInkColor(StickmanInkColor.Black);
            var whiteInk = ScriptableObject.CreateInstance<StickConfig>();
            whiteInk.SetRuntimeInkColor(StickmanInkColor.White);
            try
            {
                _backdrops = new[]
                {
                    ("밝은 바탕화면(극단 흰색)", Color.white),
                    ("어두운 바탕화면(극단 검은색)", Color.black),
                    ("종이 무대", CharacterPortraitStage.ResolveBackdropColor(blackInk)),
                    ("목탄 무대", CharacterPortraitStage.ResolveBackdropColor(whiteInk)),
                };
            }
            finally
            {
                Object.DestroyImmediate(blackInk);
                Object.DestroyImmediate(whiteInk);
            }
            return _backdrops;
        }

        [Test]
        public void 배경_넷이_서로_다르다()
        {
            (string Name, Color Color)[] bg = Backdrops();
            Assert.AreEqual(4, bg.Length);
            Assert.Greater(UiChrome.ContrastRatio(bg[2].Color, bg[3].Color), 2f,
                $"{LogPrefix} 종이 무대와 목탄 무대가 사실상 같은 색입니다 — 배경 셋만 재게 됩니다.");
        }

        [Test]
        public void 카탈로그_열거가_비지_않는다()
        {
            List<(string Name, Color Color)> colors = CatalogColors();
            Assert.Greater(colors.Count, 0, $"{LogPrefix} 카탈로그 색이 0개입니다 — 게이트가 아무것도 안 잽니다.");
            Assert.GreaterOrEqual(ItemCatalog.EquipmentCount, 1, $"{LogPrefix} 장비가 0종입니다.");
            Debug.Log($"{LogPrefix} 카탈로그 고유색 {colors.Count}종 / 장비 {ItemCatalog.EquipmentCount}종.");
        }

        // ============================================================================
        // 계산기 — CIELAB(D65) + CIE76 ΔE*ab
        // ============================================================================
        //
        // CIEDE2000이 아니라 CIE76인 이유: 이 환경에서 CIEDE2000의 <b>검증용 기준표를 구할 수 없다</b>.
        // 검증 못 하는 계산기는 이 저장소에서 쓸 수 없다(colorlab.py와 같은 판단).
        // CIE76은 흰/검 = 100, 동일색 = 0 처럼 외부에서 확인 가능한 값으로 교정된다.

        private static readonly Vector3 D65White = new Vector3(0.95047f, 1.00000f, 1.08883f);

        private static Vector3 Lab(Color c)
        {
            float r = Linear(c.r), g = Linear(c.g), b = Linear(c.b);
            float x = 0.4124564f * r + 0.3575761f * g + 0.1804375f * b;
            float y = 0.2126729f * r + 0.7151522f * g + 0.0721750f * b;
            float z = 0.0193339f * r + 0.1191920f * g + 0.9503041f * b;

            float fx = LabF(x / D65White.x), fy = LabF(y / D65White.y), fz = LabF(z / D65White.z);
            return new Vector3(116f * fy - 16f, 500f * (fx - fy), 200f * (fy - fz));
        }

        private static float LabF(float t)
            => t > 216f / 24389f ? Mathf.Pow(t, 1f / 3f) : 841f / 108f * t + 4f / 29f;

        private static float Linear(float srgb)
        {
            srgb = Mathf.Clamp01(srgb);
            return srgb <= 0.04045f ? srgb / 12.92f : Mathf.Pow((srgb + 0.055f) / 1.055f, 2.4f);
        }

        private static float DeltaE(Color a, Color b) => Vector3.Distance(Lab(a), Lab(b));

        // ============================================================================
        // 유틸
        // ============================================================================

        /// <summary>이 배경 위에서 하한을 넘으려면 색의 상대휘도가 <b>이보다 커야</b> 하는 값.</summary>
        private static float BandFloor(Color backdrop)
            => UiChrome.MinNonTextContrast * (UiChrome.RelativeLuminance(backdrop) + 0.05f) - 0.05f;

        /// <summary>이 배경 위에서 하한을 넘으려면 색의 상대휘도가 <b>이보다 작아야</b> 하는 값.</summary>
        private static float BandCeil(Color backdrop)
            => (UiChrome.RelativeLuminance(backdrop) + 0.05f) / UiChrome.MinNonTextContrast - 0.05f;

        private static bool SameByte(Color a, Color b)
            => Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f
               && Mathf.Abs(a.b - b.b) < 0.004f;

        private static Color Hex(int hex)
            => new Color(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f, 1f);

        private static string Show(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    }
}
