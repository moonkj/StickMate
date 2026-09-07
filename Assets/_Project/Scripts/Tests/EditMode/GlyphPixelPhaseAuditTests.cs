using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <b>글리프 「위상」 축</b> — 사용자 신고 "전체적으로 글자가 흐리고 일부는 번져 보임"(2026-09-07).
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 것은 <see cref="UiGlyphExactnessAuditTests"/>와 <b>다른 축</b>이다
    /// ============================================================================
    /// 저쪽은 <b>크기</b>를 잠근다(<c>pt × canvasScale</c>가 정수인가).
    /// 이쪽은 <b>위치</b>를 잠근다(그 비트맵이 정수 픽셀 <b>자리</b>에 얹히는가).
    /// <c>정수_크기여도_위상_잔차는_남는다_두_축은_독립이다</c>가 그 독립성을 수로 못박는다 —
    /// 이 테스트가 없으면 다음 사람이 <b>"크기 감사가 초록이니 글자는 선명하다"</b>고 읽는다.
    /// 실제로 그렇게 읽혀서 한 라운드를 잃었다.
    ///
    /// ============================================================================
    /// 교정(calibration) — 계산기를 만들면 알려진 값으로 먼저 맞춘다(TEAM.md §4)
    /// ============================================================================
    /// <see cref="GlyphPixelSnapPolicy.PeakCoverage"/>는 <b>독립적으로 손으로 풀 수 있는</b> 세 점을
    /// 갖는다: 잔차 0 → 1.0 / 잔차 ±0.5 → 0.5 / 균등 위상의 기댓값 → 0.75.
    /// <b>이 교정이 깨지면 그 뒤 숫자를 전부 폐기한다.</b>
    /// </summary>
    public sealed class GlyphPixelPhaseAuditTests
    {
        private const string LogPrefix = "[글리프위상-TEST]";

        private static string ScriptsRoot => Path.Combine(Application.dataPath, "_Project", "Scripts");

        // ============================================================================
        // (1) 교정 — 손으로 푼 세 점
        // ============================================================================

        /// <summary>
        /// 모형: 텍셀 폭 1의 획이 offset <c>f</c>만큼 밀려 얹히면 이웃 두 픽셀이 <c>1-f</c>와 <c>f</c>를
        /// 나눠 받는다. 최대 밝기 <c>= max(f, 1-f) = 1 - |잔차|</c>.
        /// <para>★ 기대값을 <see cref="GlyphPixelSnapPolicy.PeakCoverage"/>로 만들지 <b>않는다</b> —
        /// 그러면 함수가 틀어질 때 기대값도 같이 틀어져 아무것도 못 잰다(TEAM.md "생성기와 검사기가
        /// 같이 틀린다"). 여기 적힌 1.0 / 0.5 / 0.75는 <b>모형에서 손으로 나온 수</b>다.</para>
        /// </summary>
        [Test]
        public void 교정_피크밝기가_알려진_세_점에서_맞는다()
        {
            Assert.AreEqual(1.0f, GlyphPixelSnapPolicy.PeakCoverage(0f), 1e-6f,
                $"{LogPrefix} 잔차 0(픽셀 정합)에서 밝기 손실이 없어야 합니다.");
            Assert.AreEqual(0.5f, GlyphPixelSnapPolicy.PeakCoverage(0.5f), 1e-6f,
                $"{LogPrefix} 잔차 +0.5(최악)에서 획이 두 픽셀에 반반 나뉩니다.");
            Assert.AreEqual(0.5f, GlyphPixelSnapPolicy.PeakCoverage(-0.5f), 1e-6f,
                $"{LogPrefix} 잔차의 부호는 밝기 손실과 무관합니다.");
        }

        /// <summary>
        /// 균등 분포한 위상에서 최대 밝기의 기댓값은 <c>∫₀¹ max(f, 1-f) df = 3/4</c>다.
        /// <b>이 0.75가 「지금 이 앱의 글자가 평균적으로 얼마나 흐린가」의 상한 추정</b>이고,
        /// 신고를 인상이 아니라 수로 바꾼 값이다.
        /// </summary>
        [Test]
        public void 교정_균등_위상에서_평균_피크밝기는_0점75다()
        {
            const int samples = 10000;
            double sum = 0.0;
            for (int i = 0; i < samples; i++)
            {
                float f = (i + 0.5f) / samples;                       // (0,1) 균등
                sum += GlyphPixelSnapPolicy.PeakCoverage(GlyphPixelSnapPolicy.Residual(f));
            }
            double mean = sum / samples;
            Assert.AreEqual(0.75, mean, 1e-3,
                $"{LogPrefix} 균등 위상 평균 밝기가 {mean:F4}입니다(해석해 0.75). " +
                "교정이 깨졌으므로 이 파일의 다른 수치는 전부 무효로 보세요.");
        }

        // ============================================================================
        // (2) 잔차·스냅의 성질
        // ============================================================================

        [Test]
        public void 잔차는_항상_반_픽셀_이내다()
        {
            for (int i = -2000; i <= 2000; i++)
            {
                float c = i * 0.037f;                                  // 격자와 무관한 보폭.
                float r = GlyphPixelSnapPolicy.Residual(c);
                Assert.LessOrEqual(Mathf.Abs(r), 0.5f + 1e-5f,
                    $"{LogPrefix} 좌표 {c}에서 잔차 {r} — |잔차| ≤ 0.5가 깨졌습니다.");
            }
        }

        /// <summary>
        /// ★ <b>이것이 「무한 재빌드 없음」의 증명이다.</b> <c>CrispText.ReSnapIfDrifted</c>는
        /// "격자에서 벗어났으면 메시를 다시 만든다"인데, 스냅이 <b>한 번에</b> 격자에 도달하지 못하면
        /// 매 프레임 재빌드가 돈다 — 24시간 상주 앱에서 조용한 부하다.
        /// </summary>
        [Test]
        public void 스냅은_한_번에_수렴한다_무한_재빌드가_없다()
        {
            for (int i = 0; i < 4000; i++)
            {
                float x = i * 0.0173f - 30f;
                float y = i * 0.0291f + 7f;
                float sx = x + GlyphPixelSnapPolicy.SnapDelta(x);
                float sy = y + GlyphPixelSnapPolicy.SnapDelta(y);
                Assert.IsTrue(GlyphPixelSnapPolicy.IsPixelAligned(sx, sy),
                    $"{LogPrefix} ({x}, {y})에 스냅을 한 번 적용했는데 여전히 격자 밖입니다 " +
                    $"({sx}, {sy}) — ReSnapIfDrifted가 매 프레임 다시 돌게 됩니다.");
            }
        }

        [Test]
        public void 스냅은_멱등이며_이미_정합인_좌표를_바꾸지_않는다()
        {
            foreach (float c in new[] { -100f, -1f, 0f, 1f, 42f, 1920f, 3024f })
            {
                Assert.AreEqual(0f, GlyphPixelSnapPolicy.SnapDelta(c), 1e-6f,
                    $"{LogPrefix} 이미 정수인 {c}를 움직였습니다 — 정지 화면에서 헛된 재빌드가 됩니다.");
                Assert.IsTrue(GlyphPixelSnapPolicy.IsPixelAligned(c, c));
            }
        }

        [Test]
        public void 못_잰_값은_결함이_아니라_무해로_접는다()
        {
            foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Assert.AreEqual(0f, GlyphPixelSnapPolicy.Residual(bad), 1e-6f,
                    $"{LogPrefix} 못 잰 좌표({bad})를 잔차로 바꾸면 오탐이 됩니다.");
                Assert.AreEqual(1f, GlyphPixelSnapPolicy.PeakCoverage(bad), 1e-6f);
            }
        }

        // ============================================================================
        // (3) 축 정렬 — 회전된 글자는 손대지 않는다
        // ============================================================================

        [Test]
        public void 축_정렬_판정이_회전과_뒤집힘과_불량값을_거른다()
        {
            // 정상: 배율만 걸린 축 정렬 행렬.
            Assert.IsTrue(GlyphPixelSnapPolicy.IsAxisAligned(1f, 0f, 0f, 1f), "단위 행렬이 거부됐습니다.");
            Assert.IsTrue(GlyphPixelSnapPolicy.IsAxisAligned(1.5f, 0f, 0f, 1.5f), "균등 배율이 거부됐습니다.");
            Assert.IsTrue(GlyphPixelSnapPolicy.IsAxisAligned(2f, 0f, 0f, 0.5f), "비균등 배율도 축은 정렬입니다.");

            // 회전 45°(이 저장소가 UI에 실제로 거는 각도 — CharacterInfoWindow의 셰브론).
            float c45 = Mathf.Cos(45f * Mathf.Deg2Rad), s45 = Mathf.Sin(45f * Mathf.Deg2Rad);
            Assert.IsFalse(GlyphPixelSnapPolicy.IsAxisAligned(c45, s45, -s45, c45),
                "45° 회전을 축 정렬로 봤습니다 — 회전 상태에서 화면 좌표를 반올림하면 글자를 " +
                "비스듬히 밀어 위치만 틀어집니다.");

            // 아주 작은 회전도 거른다(0.057°가 임계).
            Assert.IsFalse(GlyphPixelSnapPolicy.IsAxisAligned(1f, 0.01f, 0f, 1f), "미세 회전이 통과했습니다.");

            // 뒤집힘/영/불량.
            Assert.IsFalse(GlyphPixelSnapPolicy.IsAxisAligned(-1f, 0f, 0f, 1f), "x축 뒤집힘이 통과했습니다.");
            Assert.IsFalse(GlyphPixelSnapPolicy.IsAxisAligned(1f, 0f, 0f, 0f), "y 배율 0이 통과했습니다.");
            Assert.IsFalse(GlyphPixelSnapPolicy.IsAxisAligned(float.NaN, 0f, 0f, 1f), "NaN이 통과했습니다.");
        }

        // ============================================================================
        // (4) ★ 두 축의 독립성 — 이 파일의 존재 이유
        // ============================================================================

        /// <summary>
        /// <b>크기 감사가 완전히 초록인 상태에서도 위상 잔차는 최악치까지 갈 수 있다.</b>
        ///
        /// <para>배율은 <see cref="UiGlyphScalePolicy.ReferenceCanvasScale"/>(=사용자 실기),
        /// pt는 <see cref="Interaction.UiChrome.FontBody"/>(=지금 출하되는 본문 크기)를 그대로 쓴다.
        /// 숫자를 베끼지 않으므로 어느 한쪽이 바뀌면 이 테스트가 따라간다.</para>
        /// </summary>
        [Test]
        public void 정수_크기여도_위상_잔차는_남는다_두_축은_독립이다()
        {
            int pt = Interaction.UiChrome.FontBody;
            float scale = UiGlyphScalePolicy.ReferenceCanvasScale;

            // (가) 크기 축은 이미 완벽하다 — 이 라운드 이전에 해결된 상태.
            Assert.IsTrue(UiGlyphScalePolicy.IsResampleFree(pt, scale),
                $"{LogPrefix} 전제가 깨졌습니다: {pt}pt × {scale}에서 크기 잔차가 이미 있습니다. " +
                "그렇다면 이 라운드의 진단(「크기는 이미 해결됐고 위상만 남았다」)부터 다시 세워야 합니다.");

            // (나) 그런데도 위상은 최악까지 간다 — 두 축은 서로를 못 본다.
            float worstOrigin = 100.5f;                                   // 반 픽셀 어긋난 원점.
            Assert.IsFalse(GlyphPixelSnapPolicy.IsPixelAligned(worstOrigin, 0f),
                $"{LogPrefix} 반 픽셀 어긋난 원점을 «정합»으로 봤습니다.");
            Assert.AreEqual(0.5f, GlyphPixelSnapPolicy.PeakCoverage(
                    GlyphPixelSnapPolicy.Residual(worstOrigin)), 1e-6f,
                $"{LogPrefix} 크기 잔차 0인 {pt}pt 글자도 위상 잔차 0.5에서는 밝기가 절반으로 떨어집니다 — " +
                "이것이 크기 감사가 구조적으로 볼 수 없는 축입니다.");
            Assert.AreEqual(2, GlyphPixelSnapPolicy.SpreadPixels(0.5f),
                $"{LogPrefix} 1px 획이 두 픽셀에 걸치는 것이 사용자가 말한 «번짐»입니다.");
        }

        // ============================================================================
        // (5) 소스 감사 — 프로덕션에서 맨 Text를 만들면 그 글자만 조용히 흐려진다
        // ============================================================================

        /// <summary><b>구성</b>(생성)만 금지한다. <c>GetComponent&lt;Text&gt;()</c> 같은 <b>조회</b>는
        /// 그대로 허용된다 — 그쪽은 이미 만들어진 <see cref="Interaction.CrispText"/>를 돌려준다.</summary>
        private static readonly string[] BannedConstructions =
        {
            "typeof(Text)",
            "AddComponent<Text>",
        };

        /// <summary>주석 <b>줄</b>을 지운다(<c>///</c>·<c>//</c>·<c>*</c>). 이 저장소의 문서 주석은
        /// 옛 호출부를 그대로 인용하므로 이 단계가 없으면 감사가 주석을 코드로 착각한다.</summary>
        private static string StripCommentLines(string source)
        {
            var sb = new StringBuilder(source.Length);
            foreach (string line in source.Split('\n'))
            {
                string t = line.TrimStart();
                bool comment = t.StartsWith("//", StringComparison.Ordinal)
                            || t.StartsWith("*", StringComparison.Ordinal)
                            || t.StartsWith("/*", StringComparison.Ordinal);
                sb.Append(comment ? string.Empty : line).Append('\n');
            }
            return sb.ToString();
        }

        private static List<string> ProductionFiles()
        {
            string testsRoot = (Path.Combine(ScriptsRoot, "Tests") + Path.DirectorySeparatorChar).Replace('\\', '/');
            var files = new List<string>(Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories));
            files.RemoveAll(p => p.Replace('\\', '/').StartsWith(testsRoot, StringComparison.Ordinal));
            Assert.GreaterOrEqual(files.Count, 40,
                $"{LogPrefix} 스캔 대상이 비정상적으로 적습니다({files.Count}) — 경로 계산 오류로 허위 통과할 위험.");
            return files;
        }

        private static List<string> Offenders(string source, string label)
        {
            var hits = new List<string>();
            string code = StripCommentLines(source);
            string[] lines = code.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                foreach (string banned in BannedConstructions)
                {
                    if (lines[i].Contains(banned, StringComparison.Ordinal))
                        hits.Add($"{label}:{i + 1}  {banned}  →  {lines[i].Trim()}");
                }
            }
            return hits;
        }

        /// <summary>
        /// <b>부재 단언</b>이다 — 썩으면 조용히 초록이 된다(CLAUDE.md의 경고).
        /// 그래서 바로 아래 <see cref="양성_대조_스캐너는_맨_Text_생성을_실제로_잡는다"/>와
        /// 그 아래 <see cref="존재_단언_알려진_두_생성처가_CrispText다"/>를 <b>같이</b> 둔다.
        /// 셋 중 하나라도 없으면 이 감사는 아무것도 증명하지 못한다.
        /// </summary>
        [Test]
        public void 프로덕션에서_맨_Text를_새로_만드는_곳이_없다()
        {
            var all = new List<string>();
            foreach (string file in ProductionFiles())
                all.AddRange(Offenders(File.ReadAllText(file), Path.GetFileName(file)));

            Assert.IsEmpty(all,
                $"{LogPrefix} 맨 UnityEngine.UI.Text를 새로 만드는 곳이 있습니다({all.Count}곳).\n" +
                $"{string.Join("\n", all)}\n" +
                "그 글자만 픽셀 격자에 안 맞아 <혼자 흐리게> 나옵니다. StickMate.Interaction.CrispText를 " +
                "쓰거나(권장: UiChrome.AddText), 여기 면제 사유를 남기세요.");
        }

        /// <summary>★ 위 부재 단언의 <b>양성 대조</b>. 스캐너가 눈이 멀어도 위 테스트는 초록이다.</summary>
        [Test]
        public void 양성_대조_스캐너는_맨_Text_생성을_실제로_잡는다()
        {
            const string poisoned =
                "class X {\n" +
                "  void M() {\n" +
                "    var go = new GameObject(\"L\", typeof(RectTransform), typeof(Text));\n" +
                "    var t = go.AddComponent<Text>();\n" +
                "  }\n" +
                "}\n";
            Assert.AreEqual(2, Offenders(poisoned, "합성").Count,
                $"{LogPrefix} 스캐너가 고의로 심은 두 생성처를 못 잡았습니다 — " +
                "위 부재 단언의 초록은 아무것도 증명하지 않습니다.");

            // 그리고 <조회>와 <CrispText 생성>은 잡으면 안 된다(오탐 대조).
            const string innocent =
                "class Y {\n" +
                "  void M() {\n" +
                "    var t = go.GetComponent<Text>();\n" +
                "    var g = new GameObject(\"L\", typeof(RectTransform), typeof(CrispText));\n" +
                "    var h = go.AddComponent<CrispText>();\n" +
                "  }\n" +
                "}\n";
            Assert.IsEmpty(Offenders(innocent, "합성"),
                $"{LogPrefix} 조회/CrispText 생성을 위반으로 잡았습니다 — 오탐이 한 번 나면 " +
                "다음 사람이 이 감사를 꺼 버립니다.");

            // 주석 줄은 넘긴다(이 저장소의 문서가 옛 호출부를 그대로 인용하므로).
            Assert.IsEmpty(Offenders("  /// <c>typeof(Text)</c>를 쓰던 시절\n", "합성"),
                $"{LogPrefix} 주석 안의 인용을 코드로 착각했습니다.");
        }

        /// <summary>★ <b>존재 단언</b>. 부재 단언만 두면 «CrispText가 통째로 사라져도» 초록이다.
        /// 알려진 두 생성처가 실제로 <see cref="Interaction.CrispText"/>를 쓰는지 못박는다.</summary>
        [Test]
        public void 존재_단언_알려진_두_생성처가_CrispText다()
        {
            (string relative, string why)[] sites =
            {
                (Path.Combine("Interaction", "UiChrome.cs"),
                 "AddText — 이 앱의 창/팝오버/부채꼴/포스트잇 글자 전부가 여기서 태어난다"),
                (Path.Combine("Dialogue", "DialogueBubbleRenderer.cs"),
                 "말풍선 라벨 — 매 프레임 움직여서 위상 잔차가 가장 큰 표면이다"),
            };

            foreach ((string relative, string why) in sites)
            {
                string path = Path.Combine(ScriptsRoot, relative);
                Assert.IsTrue(File.Exists(path),
                    $"{LogPrefix} {relative}를 찾지 못했습니다 — 경로가 바뀌었다면 이 목록도 갱신하세요. " +
                    "그대로 두면 이 존재 단언이 <파일이 없어서> 영원히 실패하거나, 더 나쁘게는 " +
                    "다음 사람이 목록에서 지워 버립니다.");

                string code = StripCommentLines(File.ReadAllText(path));
                StringAssert.Contains(nameof(Interaction.CrispText), code,
                    $"{LogPrefix} {relative}가 더 이상 CrispText를 쓰지 않습니다({why}). " +
                    "누군가 되돌렸다면 그 표면의 글자는 다시 픽셀 격자를 벗어납니다.");
            }
        }

        // ============================================================================
        // (5.5) 계측 훅 — 이 머신에서 배율 1.5를 만드는 유일한 통로
        // ============================================================================

        /// <summary>
        /// <see cref="UiDensityOverride.TryParseDensity"/>는 <b>순수 함수</b>다 — 프로세스 환경을
        /// 건드리지 않고 전수 검증할 수 있게 일부러 그렇게 갈라 놓았다.
        ///
        /// <para>★ <b>로캘 함정을 못박는 것이 이 테스트의 핵심</b>: 소수점 기호가 쉼표인 로캘에서
        /// <c>float.TryParse("1.5")</c>는 <b>조용히 15</b>가 되거나 실패한다. 그러면 계측 실행이
        /// «배율 15»로 돌면서 UI가 화면 밖으로 날아가고, 원인을 찾는 데 한 라운드가 든다.</para>
        /// </summary>
        [Test]
        public void 계측_배율_파싱이_로캘과_오타를_모두_거른다()
        {
            Assert.IsTrue(UiDensityOverride.TryParseDensity("1.5", out float ok), "«1.5»를 거부했습니다.");
            Assert.AreEqual(UiGlyphScalePolicy.ReferenceCanvasScale, ok, 1e-6f,
                $"{LogPrefix} «1.5»가 기준 배율로 해석되지 않았습니다 — 로캘 소수점 함정입니다.");

            Assert.IsTrue(UiDensityOverride.TryParseDensity(" 1.25 ", out float trimmed), "공백을 못 다듬었습니다.");
            Assert.AreEqual(1.25f, trimmed, 1e-6f);

            // 거부해야 하는 것들. ★ 150은 «150%»를 그대로 넣은 흔한 오타다 — 조용히 먹으면 안 된다.
            foreach (string bad in new[] { "", "   ", "abc", "150", "0", "-1.5", "1,5", "NaN", "Infinity" })
            {
                Assert.IsFalse(UiDensityOverride.TryParseDensity(bad, out float _),
                    $"{LogPrefix} «{bad}»를 배율로 받아들였습니다 — 계측 실행이 조용히 망가집니다.");
            }

            // 경계: 상·하한은 포함, 그 바로 밖은 거부.
            Assert.IsTrue(UiDensityOverride.TryParseDensity(
                UiDensityOverride.MinAcceptedDensity.ToString(CultureInfo.InvariantCulture), out float _),
                "하한값 자신을 거부했습니다.");
            Assert.IsTrue(UiDensityOverride.TryParseDensity(
                UiDensityOverride.MaxAcceptedDensity.ToString(CultureInfo.InvariantCulture), out float _),
                "상한값 자신을 거부했습니다.");
            Assert.IsFalse(UiDensityOverride.TryParseDensity(
                (UiDensityOverride.MaxAcceptedDensity + 0.5f).ToString(CultureInfo.InvariantCulture), out float _),
                "상한을 넘긴 값을 받아들였습니다.");
        }

        /// <summary>
        /// 환경변수를 <b>주지 않으면</b> 이 훅은 아무 일도 하지 않는다 — 제품 동작 영향 0의 근거.
        /// <para>러너는 환경변수 없이 도는 것이 정상이므로 여기서 <c>ForcedByEnvironment</c>가
        /// true라면 <b>누군가 계측 모드로 러너를 돌린 것</b>이고, 그 회차의 배율 관련 수치는 전부
        /// 폐기해야 한다. 그래서 «내 코드 검사»이자 동시에 «측정 위생 검사»다.</para>
        /// </summary>
        [Test]
        public void 계측_훅은_환경변수가_없으면_아무것도_하지_않는다()
        {
            string raw = System.Environment.GetEnvironmentVariable(
                UiDensityOverride.EnvironmentVariableName);

            // ★ 여기서는 <건너뛰기>를 쓰지 않는다 — 두 갈래 모두 <실단언>으로 잰다.
            //   건너뛰면 «계측 모드로 돌았다»와 «훅이 고장났다»가 러너에서 똑같이 생긴다.
            if (UiDensityOverride.TryParseDensity(raw, out float requested))
            {
                Assert.IsTrue(UiDensityOverride.ForcedByEnvironment,
                    $"{LogPrefix} {UiDensityOverride.EnvironmentVariableName}=\"{raw}\"를 줬는데 " +
                    "강제 지정이 켜지지 않았습니다 — 계측 훅이 동작하지 않습니다.");
                Assert.AreEqual(requested, UiDensityOverride.ForcedDensity, 1e-6f,
                    $"{LogPrefix} 요청한 배율과 적용된 배율이 다릅니다.");
                Assert.AreEqual(requested,
                    Platform.ScreenCoordinateConverter.ResolveCanvasScaleFactor(null), 1e-6f,
                    $"{LogPrefix} 캔버스 배율이 강제값을 따라가지 않았습니다 — 훅이 " +
                    "ReportUiDensityScale까지 도달하지 못했습니다.");
                return;
            }

            // 평상시 갈래: 환경변수가 없거나(정상) 해석 불가(무시돼야 한다).
            Assert.IsFalse(UiDensityOverride.ForcedByEnvironment,
                $"{LogPrefix} 유효한 환경변수가 없는데 강제 지정이 켜져 있습니다 — 계측 훅이 " +
                "제품 경로로 새어 들어갔습니다.");
            Assert.AreEqual(0f, UiDensityOverride.ForcedDensity, 1e-6f,
                $"{LogPrefix} 강제 배율이 0이 아닙니다({UiDensityOverride.ForcedDensity}).");
        }

        // ============================================================================
        // (6) 남은 갭 — 러너에 계속 보이게 둔다
        // ============================================================================

        /// <summary>
        /// ★ <b>미해결</b>: 위상 축에는 <b>실기 계기판이 없다</b>. 크기 축은
        /// <c>[GLYPH-SCALE]</c> 줄이 Windows 실기에서 잰 값을 찍지만, 위상 축은 어느 플랫폼에서도
        /// 로그에 나오지 않는다 — <b>고쳤는지 실기에서 확인할 방법이 아직 없다</b>.
        /// </summary>
        [Test]
        public void 미해결_위상_축에는_실기_계기판이_없다()
        {
            // ★ 자동 승격 — <b>「이름을 언급했는가」가 아니라 「태그를 찍는가」로 잰다.</b>
            //   2026-09-07 실측 사고: 처음에는 "Platform/ 안에 GlyphPixelSnapPolicy를 언급하는
            //   파일이 있는가"로 판정했는데, <b>같은 라운드가 추가한 UiDensityOverride.cs의 주석
            //   한 줄</b>이 그것을 만족시켜 이 항목이 <b>Passed로 조용히 닫혔다</b>(러너 실측).
            //   갭은 그대로였다. «죽은 프로브의 출력이 성공한 프로브와 똑같이 생겼다»의 재현이다.
            string platformRoot = Path.Combine(ScriptsRoot, "Platform");
            Assert.IsTrue(Directory.Exists(platformRoot),
                $"{LogPrefix} {platformRoot}를 찾지 못했습니다 — 경로가 바뀌었다면 여기도 갱신하세요. " +
                "그대로 두면 이 항목은 '고쳐져도 영원히 건너뜀'이 됩니다.");
            foreach (string file in Directory.GetFiles(platformRoot, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                if (name == nameof(GlyphPixelSnapPolicy) + ".cs") continue;   // 태그를 정의한 파일 자신.
                if (EmitsPhaseTag(StripCommentLines(File.ReadAllText(file))))
                {
                    Assert.Pass($"위상 축 계기판이 생겼습니다({name}이 " +
                        $"<{GlyphPixelSnapPolicy.DiagnosticTag}>를 찍습니다) — 이 항목을 정식 검사로 " +
                        "승격하고, 그 프로브가 표본 표면의 이름을 함께 찍는지" +
                        "(«표본 하나에 대한 판정»을 «모든 글자에 대한 판정»으로 오독하지 않게) 확인하세요.");
                }
            }

            Assert.Ignore("【미해결 · 고침은 착지했고 <실기 계측>이 없다】 신설 2026-09-07 (dev-platform)\n" +
                "항목: 글리프 위상(sub-pixel placement) 축 — 규칙과 적용은 양 플랫폼 공통으로 들어갔고, " +
                "실기에서 그것을 재는 로그가 없다.\n" +
                "\n" +
                "· 착지한 것(플랫폼 중립, 양쪽 동시): Platform/GlyphPixelSnapPolicy(순수 규칙) + " +
                "Interaction/CrispText(Text 파생, 메시 정점만 격자에 스냅). UiChrome.AddText와 " +
                "DialogueBubbleRenderer가 이 컴포넌트를 쓴다. #if 분기가 한 줄도 없으므로 " +
                "macOS/Windows가 <같은 코드>를 돈다.\n" +
                "\n" +
                "· 갭: OverlayCompositionSnapshot에 <위상> 필드가 없다. 크기 축은 SampleFontSizePoints/" +
                "SampleTransformScale이 있어 [GLYPH-SCALE] 줄로 실기 값이 찍히지만, 위상은 " +
                "«글자 원점의 월드 xy 소수부»라 <살아 있는 오브젝트를 재야> 한다. " +
                "WindowsCompositionProbe는 지금 상수 하나만 표집하는 구조라(그 사각지대는 " +
                "PlatformParityAuditTests.미해결_글리프_리샘플_진단...이 이미 띄우고 있다) " +
                "여기에 위상을 얹으면 같은 «표본 구조 재설계»가 선행이다.\n" +
                "\n" +
                "· 그리고 macOS에는 합성 프로브 자체가 아직 없다 — 그쪽 갭은 같은 파일의 기존 항목 소관.\n" +
                "\n" +
                "· 다음 사람이 할 일: (1) OverlayCompositionSnapshot에 SampleGlyphOriginX/Y(스크린 픽셀) " +
                "추가, (2) 프로브가 <표면 이름과 함께> 살아 있는 CrispText 하나를 표집, " +
                "(3) OverlayCompositionVerdict에 [GLYPH-PHASE] 줄 추가 — 문구는 이미 " +
                "GlyphPixelSnapPolicy.Describe에 있으니 사본을 만들지 말 것.");
        }

        /// <summary>그 소스가 <b>실제로 위상 태그를 찍는가</b>(주석은 이미 제거된 상태로 들어온다).
        /// 리터럴과 상수 참조를 <b>둘 다</b> 인정한다 — 프로브가 어느 쪽으로 쓰든 계기판은 계기판이다.</summary>
        private static bool EmitsPhaseTag(string codeWithoutComments)
            => codeWithoutComments.Contains(GlyphPixelSnapPolicy.DiagnosticTag, StringComparison.Ordinal)
               || codeWithoutComments.Contains(nameof(GlyphPixelSnapPolicy.DiagnosticTag), StringComparison.Ordinal);

        /// <summary>★ 위 래칫의 <b>양성·음성 대조</b>. 이것이 없으면 래칫이 눈이 멀어도(항상 false)
        /// 위 항목은 «건너뜀»으로 조용히 남고, 갭이 <b>실제로 닫힌 날에도 아무도 모른다</b>.</summary>
        [Test]
        public void 양성음성_대조_위상_계기판_래칫이_이름_언급과_실제_계측을_구분한다()
        {
            // 음성: 이름만 언급하는 주석/코드는 계기판이 아니다.
            Assert.IsFalse(EmitsPhaseTag(StripCommentLines(
                    "/// <see cref=\"GlyphPixelSnapPolicy\"/>의 위상 축\nclass A { void M() { } }\n")),
                $"{LogPrefix} 이름 언급을 계기판으로 셌습니다 — 2026-09-07에 실제로 이 형태로 " +
                "열린 갭이 조용히 초록이 됐습니다.");
            Assert.IsFalse(EmitsPhaseTag(StripCommentLines(
                    "class B { int x = UiGlyphScalePolicy.ExactPointStep(1.5f); }\n")),
                $"{LogPrefix} 크기 축(GLYPH-SCALE) 호출을 위상 계기판으로 셌습니다 — 축이 다릅니다.");

            // 양성: 태그를 리터럴로 찍는 코드, 그리고 상수로 참조하는 코드 둘 다 잡아야 한다.
            Assert.IsTrue(EmitsPhaseTag("class C { string t = \"" + GlyphPixelSnapPolicy.DiagnosticTag + "\"; }"),
                $"{LogPrefix} 태그 리터럴을 못 잡았습니다 — 갭이 닫혀도 이 항목이 안 열립니다.");
            Assert.IsTrue(EmitsPhaseTag(
                    "class D { void M() { Add(GlyphPixelSnapPolicy." + nameof(GlyphPixelSnapPolicy.DiagnosticTag) + "); } }"),
                $"{LogPrefix} 태그 상수 참조를 못 잡았습니다.");

            // 그리고 <지금은 실제로 아무도 안 찍는다>는 사실을 못박는다 — 이것이 참이어야
            // 위 항목의 Assert.Ignore가 정직하다.
            string platformRoot = Path.Combine(ScriptsRoot, "Platform");
            var emitters = new List<string>();
            foreach (string file in Directory.GetFiles(platformRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file) == nameof(GlyphPixelSnapPolicy) + ".cs") continue;
                if (EmitsPhaseTag(StripCommentLines(File.ReadAllText(file)))) emitters.Add(Path.GetFileName(file));
            }
            Assert.IsEmpty(emitters,
                $"{LogPrefix} 위상 태그를 찍는 파일이 생겼습니다({string.Join(", ", emitters)}) — " +
                "그렇다면 미해결_위상_축에는_실기_계기판이_없다가 Assert.Pass로 열렸을 것이고, " +
                "이 대조는 그 사실을 <두 번째 방법으로> 확인합니다. 항목을 정식 검사로 승격하세요.");
        }
    }
}
