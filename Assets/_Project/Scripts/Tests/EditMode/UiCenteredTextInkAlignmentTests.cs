using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using StickMate.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ <b>「상자 안에 가운데 놓인 글자」가 진짜로 가운데 있는가</b> — 2026-09-29 사용자 신고로 신설.
    ///
    /// ============================================================================
    /// 무엇이 깨져 있었나
    /// ============================================================================
    /// 신고 원문(빌드 <c>windows-preview-20260929b</c>, 정보창 「장비 외형 보관함」 탭바 캡처):
    /// <i>"상자 안 정렬이 안 되어 있다"</i>. 활성 탭 「장비」만 주황 알약(<see cref="UiChrome.Accent"/>)을
    /// 두르고 있어서 <b>글자와 상자의 위아래 여백이 다르다는 사실이 그 칸에서만 눈에 보인다.</b>
    ///
    /// <para><b>원인은 폰트 교체(<c>7481c72</c>)다 — 다만 「굵기」가 아니라 「세로 수치」쪽이다.</b>
    /// uGUI <see cref="Text"/>의 중앙 정렬은 <b>폰트 수치</b>(ascent / lineHeight)로 줄 상자를 세워
    /// rect에 맞춘다. 그 수치와 한글 글리프의 <b>실제 잉크</b>가 어긋나는 양은 페이스마다 다르므로,
    /// 글꼴을 갈아타면 <b>「상자 안 가운데 글자」가 전부 통째로 밀린다.</b> 실측(아래 테스트가 매번
    /// 다시 찍는다): 탭 칩 52×28pt · 「장비」 · 14pt에서 잉크 세로 중심이
    /// <c>LegacyRuntime +0.50pt</c> → <c>Apple SD Gothic Neo −1.50pt</c>로 <b>2.0pt 내려앉았다</b>
    /// (위 여백 8.0 대 아래 여백 5.0).
    ///
    /// <para>고친 방법은 <see cref="Text.alignByGeometry"/>를 <see cref="UiChrome.AddText"/>에서
    /// 켜는 것이다(이 앱에서 UI 글자가 태어나는 문은 그 하나뿐이다 — 말풍선만 예외).
    /// 정렬 기준이 <b>폰트 수치에서 글리프 기하로</b> 바뀌어 두 글꼴이 <b>같은 값</b>(−0.50pt)을 낸다.
    /// 남는 0.50pt는 잉크 높이가 15pt(홀수)인데 글리프가 정수 pt에만 앉아서 생기는
    /// <b>구조적 하한</b>이다.</para>
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 세 가지
    /// ============================================================================
    /// <list type="number">
    /// <item><b>문이 열려 있다</b> — <see cref="UiChrome.AddText"/>가 만든 글자는 글리프 기하로
    ///   정렬된다. <b>음성 대조</b>로 맨 <see cref="CrispText"/>는 그 값이 꺼져 있음을 같은
    ///   테스트에서 보여, 이것이 Unity 기본값이 아니라 <b>우리가 켠 것</b>임을 증명한다.</item>
    /// <item><b>실제 표면이 가운데다</b> — 프로덕션 빌더 <c>CharacterInfoWindow.BuildTabs</c>를
    ///   그대로 돌려 칩 안의 잉크 중심을 잰다. 상수 산술로는 이 실패가 안 잡힌다(폰트가 정하는
    ///   값이다). <b>살아있음 대조</b>로 스위치를 껐을 때 같은 자가 잉크를 실제로 움직여 보인다.</item>
    /// <item><b>폰트가 또 바뀌어도 안 깨진다</b> — 같은 라벨을 <b>옛 내장 글꼴</b>로도 재서 두 값이
    ///   같은지 확인한다. 이 항이 이 파일의 본체다: 잠그는 것은 「이 폰트에서 가운데」가 아니라
    ///   <b>「폰트에 의존하지 않는다」</b>다.</item>
    /// </list>
    ///
    /// <para><b>한계(정직하게)</b>: 배치모드는 <c>-nographics</c>라 OS 페이스가 안 올라오면 내장
    /// 글꼴로 폴백한다. 그때 세 번째 항은 <b>같은 글꼴을 두 번 재는 공허한 비교</b>가 되므로,
    /// 확정 페이스 이름과 「두 글꼴의 스위치 끈 값 차이」를 로그로 함께 남긴다 — 그 차이가 0이면
    /// 그 실행은 출하 페이스를 잰 것이 아니다. 최종 판정은 실기 캡처다.</para>
    /// </summary>
    public sealed class UiCenteredTextInkAlignmentTests
    {
        private const string LogPrefix = "[상자중앙정렬-TEST]";

        /// <summary>잉크 중심이 상자 중심에서 벗어나도 되는 폭(pt).
        /// <para>유도: 글리프는 <b>정수 pt</b>에만 앉고 한글 잉크 높이는 이 크기에서 <b>15pt(홀수)</b>다 —
        /// 그러면 완전 중앙(±7.5)이 불가능하고 |중심|의 하한이 <b>0.5pt</b>다. 문자열마다 잉크 높이의
        /// 홀짝이 달라지므로 그 하한을 위아래로 한 칸 열어 <b>1.0pt</b>로 둔다. 「지금 통과하는 값」을
        /// 얼린 것이 아니라 격자 해상도에서 유도한 값이다.</para></summary>
        private const float InkCenterTolerancePoints = 1.0f;

        /// <summary>스위치가 <b>실제로 잉크를 움직였는가</b>의 하한(pt). 격자 해상도 1pt의 절반 —
        /// 이보다 작으면 「스위치가 아무 일도 안 했다」와 구분되지 않는다.</summary>
        private const float SwitchEffectFloorPoints = 0.5f;

        /// <summary>두 글꼴이 <b>같은 값</b>이라고 말할 수 있는 폭(pt). 글리프 격자가 1pt이므로
        /// 그 절반보다 크면 「같다」가 아니다.</summary>
        private const float FontIndependenceTolerancePoints = 0.5f;

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            }
            _spawned.Clear();
        }

        // ============================================================================
        // 자 — 라벨이 실제로 그릴 글리프 사각형의 합집합
        // ============================================================================

        /// <summary><paramref name="label"/>이 <b>지금 설정으로</b> 그릴 글리프 정점의 합집합
        /// (라벨 rect 로컬. pivot 0.5라 (0,0)이 곧 상자 중심이다).
        /// <paramref name="glyphs"/>는 센 글리프 수 — 0이면 아무것도 재지 못한 것이다.
        ///
        /// <para><see cref="Text.cachedTextGenerator"/>가 아니라 새 <see cref="TextGenerator"/>를 쓰는
        /// 이유: 캐시는 캔버스 갱신 타이밍에 묶여 있고 EditMode에는 그 갱신이 없다. 설정은
        /// <see cref="Text.GetGenerationSettings"/>로 <b>프로덕션 컴포넌트에게 직접 물어본다</b> —
        /// 정렬·글꼴·크기·wrap을 테스트가 다시 조립하면 그 순간 프로덕션과 갈라진다.</para>
        ///
        /// <para>퇴화 사각형(폭이나 높이가 0)은 건너뛴다. <see cref="TextGenerator"/>는 마지막에
        /// 패딩 정점 4개를 붙이는데, 그것을 글리프로 세면 잉크 상자가 한 글자만큼 좁게 나온다
        /// (이 파일을 짜는 동안 실제로 그렇게 나왔다 — 「장비」가 「장」으로 재어졌다).</para></summary>
        private static Rect InkBoxOf(Text label, out int glyphs)
        {
            TextGenerationSettings settings = label.GetGenerationSettings(label.rectTransform.rect.size);
            var generator = new TextGenerator();
            generator.Populate(label.text, settings);
            IList<UIVertex> verts = generator.verts;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            glyphs = 0;
            for (int q = 0; q + 3 < verts.Count; q += 4)
            {
                Vector3 a = verts[q].position, c = verts[q + 2].position;
                if (Mathf.Approximately(a.x, c.x) || Mathf.Approximately(a.y, c.y)) continue;
                for (int k = 0; k < 4; k++)
                {
                    Vector3 p = verts[q + k].position;
                    if (p.x < minX) minX = p.x;
                    if (p.x > maxX) maxX = p.x;
                    if (p.y < minY) minY = p.y;
                    if (p.y > maxY) maxY = p.y;
                }
                glyphs++;
            }
            return glyphs == 0 ? new Rect(0f, 0f, 0f, 0f) : Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        /// <summary>같은 라벨을 <paramref name="geometry"/> 상태로 재고 <b>원래 값으로 되돌린다</b>.</summary>
        private static Rect InkBoxWith(Text label, bool geometry, out int glyphs)
        {
            bool keep = label.alignByGeometry;
            label.alignByGeometry = geometry;
            Rect box = InkBoxOf(label, out glyphs);
            label.alignByGeometry = keep;
            return box;
        }

        /// <summary>프로덕션 빌더로 정보창 <b>탭 스트립</b>을 굽는다 — 탭 정의·폭 측정·칩 배치가
        /// 전부 출하 경로 그대로다(테스트가 좌표를 다시 조립하지 않는다).</summary>
        private RectTransform BuildTabStrip()
        {
            // 호스트 크기는 일부러 정하지 않는다 — 스트립은 <c>UiChrome.PlaceTopLeft</c>(점 앵커)로,
            // 칩도 같은 함수로 앉고, 라벨은 칩을 <c>Stretch</c>한다. 즉 이 세 사각형은 <b>부모 크기와
            // 무관</b>하다. 여기서 헤더 치수를 베끼면 그 상수가 움직일 때 기준만 낡는다.
            var host = new GameObject("탭정렬측정", typeof(RectTransform));
            _spawned.Add(host);

            var window = host.AddComponent<CharacterInfoWindow>();
            MethodInfo build = typeof(CharacterInfoWindow)
                .GetMethod("BuildTabs", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(build,
                $"{LogPrefix} CharacterInfoWindow.BuildTabs를 못 찾았습니다 — 이름이 바뀌었다면 이 파일도 " +
                "함께 고쳐야 합니다. 그 전까지 아래 모든 숫자는 대상 없이 돌고, 그 상태의 초록은 " +
                "아무것도 증명하지 않습니다.");
            build.Invoke(window, new object[] { host.transform, 0f });

            Transform strip = host.transform.Find("TabStrip");
            Assert.NotNull(strip, $"{LogPrefix} TabStrip이 만들어지지 않았습니다.");
            return (RectTransform)strip;
        }

        /// <summary>스트립 밑의 탭 칩들(라벨을 가진 자식만). 테두리 겹은 라벨이 없어서 빠진다.</summary>
        private static List<Text> TabLabels(RectTransform strip)
        {
            var found = new List<Text>(4);
            foreach (Transform child in strip)
            {
                Transform labelT = child.Find("Label");
                if (labelT == null) continue;
                Text label = labelT.GetComponent<Text>();
                if (label != null) found.Add(label);
            }
            return found;
        }

        // ============================================================================
        // 1. 문이 열려 있는가 (+ 음성 대조)
        // ============================================================================

        [Test]
        public void UiChrome가_만든_글자는_글리프_기하로_정렬된다()
        {
            var host = new GameObject("문검사", typeof(RectTransform));
            _spawned.Add(host);

            Text made = UiChrome.AddText(host.transform, "Label", UiChrome.FontTitle,
                TextAnchor.MiddleCenter, UiChrome.TextPrimary);
            Assert.IsTrue(made.alignByGeometry,
                $"{LogPrefix} ★ UiChrome.AddText가 만든 글자의 alignByGeometry가 꺼져 있습니다 — " +
                "중앙 정렬이 다시 <폰트 수치>에 매달립니다. 그러면 글꼴을 갈아타는 라운드마다 " +
                "「상자 안 정렬이 안 되어 있다」(2026-09-29 사용자 신고)가 되돌아옵니다.");

            // ★ 음성 대조 — 이것이 Unity 기본값이 아니라 <b>우리가 켠 것</b>인가.
            //   기본값이 이미 true라면 위 단언은 무엇도 지키지 않는 장식이다.
            var bareGo = new GameObject("맨CrispText", typeof(RectTransform), typeof(CrispText));
            _spawned.Add(bareGo);
            var bare = bareGo.GetComponent<CrispText>();
            Assert.IsFalse(bare.alignByGeometry,
                $"{LogPrefix} 음성 대조 실패 — 맨 CrispText의 alignByGeometry가 이미 켜져 있습니다. " +
                "그러면 위 단언은 우리 코드가 아니라 Unity 기본값을 재고 있는 것이고, " +
                "AddText에서 그 줄을 지워도 초록으로 남습니다.");

            Debug.Log($"{LogPrefix} 문 확인 — AddText 산출물 alignByGeometry=True / " +
                      $"맨 CrispText={bare.alignByGeometry}(= Unity 기본값).");
        }

        // ============================================================================
        // 2. 실제 표면 — 정보창 탭 칩 안의 잉크 (+ 살아있음 대조)
        // ============================================================================

        [Test]
        public void 정보창_탭_라벨_잉크는_칩_가운데에_앉는다()
        {
            RectTransform strip = BuildTabStrip();
            List<Text> labels = TabLabels(strip);
            Assert.Greater(labels.Count, 1,
                $"{LogPrefix} 탭 라벨을 {labels.Count}개만 찾았습니다 — 이 검사가 공허합니다.");

            var report = new StringBuilder();
            report.Append(LogPrefix).Append(" 확정 페이스='").Append(UiChrome.ResolvedFontName)
                  .Append("' · 탭 ").Append(labels.Count).Append("칸\n");

            float worstAbsY = 0f, worstSwitch = float.MaxValue;
            string worstName = null;
            for (int i = 0; i < labels.Count; i++)
            {
                Text label = labels[i];
                var chip = (RectTransform)label.transform.parent;

                Rect on = InkBoxWith(label, true, out int glyphsOn);
                Rect off = InkBoxWith(label, false, out int glyphsOff);

                // 양성 대조 — 무엇이라도 쟀는가. 0글리프에서 "가운데다"라고 말하는 것이
                // 이 저장소의 거짓 통과 형태다.
                Assert.Greater(glyphsOn, 0,
                    $"{LogPrefix} 탭 «{label.text}»의 글리프를 0개 셌습니다 — 이 실행의 정렬 판정은 " +
                    "전부 무효입니다(배치모드 -nographics에서 글꼴이 안 올라온 경우).");
                Assert.AreEqual(label.text.Length, glyphsOn,
                    $"{LogPrefix} 탭 «{label.text}»({label.text.Length}자)에서 글리프 {glyphsOn}개만 " +
                    "셌습니다 — 잉크 상자가 글자 일부만 담고 있어 중심값이 거짓입니다.");

                report.Append("    «").Append(label.text).Append("» 칩 ")
                      .Append(chip.sizeDelta.x.ToString("F1")).Append('×')
                      .Append(chip.sizeDelta.y.ToString("F1"))
                      .Append("  켬 y=[").Append(on.yMin.ToString("F2")).Append(", ")
                      .Append(on.yMax.ToString("F2")).Append("] 중심 ")
                      .Append(on.center.y.ToString("+0.00;-0.00"))
                      .Append("  / 끔 중심 ").Append(off.center.y.ToString("+0.00;-0.00"))
                      .Append("  가로 중심 ").Append(on.center.x.ToString("+0.00;-0.00")).Append('\n');

                if (Mathf.Abs(on.center.y) > worstAbsY)
                {
                    worstAbsY = Mathf.Abs(on.center.y);
                    worstName = label.text;
                }
                worstSwitch = Mathf.Min(worstSwitch, Mathf.Abs(on.center.y - off.center.y));

                Assert.LessOrEqual(Mathf.Abs(on.center.y), InkCenterTolerancePoints,
                    $"{LogPrefix} ★ 탭 «{label.text}»의 글자가 칩 세로 중심에서 " +
                    $"{on.center.y:+0.00;-0.00}pt 벗어났습니다(위 여백 " +
                    $"{chip.sizeDelta.y * 0.5f - on.yMax:F2} 대 아래 여백 " +
                    $"{on.yMin + chip.sizeDelta.y * 0.5f:F2}). 2026-09-29 사용자 신고 " +
                    "「상자 안 정렬이 안 되어 있다」가 바로 이 형태입니다.");
                Assert.LessOrEqual(Mathf.Abs(on.center.x), InkCenterTolerancePoints,
                    $"{LogPrefix} 탭 «{label.text}»의 글자가 칩 가로 중심에서 " +
                    $"{on.center.x:+0.00;-0.00}pt 벗어났습니다.");
            }

            // ★ 살아있음 대조 — 이 자가 <b>정렬 변화를 실제로 본다</b>는 것을 같은 실행에서 보인다.
            //   스위치를 끄면 잉크가 움직여야 한다. 안 움직이면 위 초록은 「재긴 쟀는데 무엇도
            //   거를 수 없는 자」의 초록이다.
            Assert.GreaterOrEqual(worstSwitch, SwitchEffectFloorPoints,
                $"{LogPrefix} 살아있음 대조 실패 — alignByGeometry를 껐는데 잉크 세로 중심이 " +
                $"{worstSwitch:F2}pt밖에 움직이지 않았습니다(하한 {SwitchEffectFloorPoints:F2}). " +
                "이 자는 정렬 변화를 못 보고 있으므로 위의 초록도 무효입니다.");

            report.Append("    최악 |세로 중심| ").Append(worstAbsY.ToString("F2"))
                  .Append("pt(«").Append(worstName).Append("») · 허용 ")
                  .Append(InkCenterTolerancePoints.ToString("F2"))
                  .Append("pt · 스위치 효과 최소 ").Append(worstSwitch.ToString("F2")).Append("pt");
            Debug.Log(report.ToString());
        }

        // ============================================================================
        // 3. 폰트가 또 바뀌어도 안 깨지는가 — 이 파일의 본체
        // ============================================================================

        [Test]
        public void 탭_라벨_세로_중심은_글꼴에_의존하지_않는다()
        {
            RectTransform strip = BuildTabStrip();
            List<Text> labels = TabLabels(strip);
            Assert.Greater(labels.Count, 1,
                $"{LogPrefix} 탭 라벨을 {labels.Count}개만 찾았습니다 — 이 검사가 공허합니다.");

            Font builtin = Resources.GetBuiltinResource<Font>(UiChrome.BuiltinFontResource);
            Assert.NotNull(builtin,
                $"{LogPrefix} 내장 글꼴 '{UiChrome.BuiltinFontResource}'을 못 불렀습니다 — 대조할 짝이 없습니다.");

            var report = new StringBuilder();
            report.Append(LogPrefix).Append(" 글꼴 의존성 — 출하 '").Append(UiChrome.ResolvedFontName)
                  .Append("' 대 내장 '").Append(UiChrome.BuiltinFontResource).Append("'\n");

            float worstOnGap = 0f;
            float worstOffGap = 0f;
            string worstName = null;
            for (int i = 0; i < labels.Count; i++)
            {
                Text label = labels[i];
                Font shipped = label.font;

                Rect shippedOn = InkBoxWith(label, true, out int gShippedOn);
                Rect shippedOff = InkBoxWith(label, false, out _);
                Assert.Greater(gShippedOn, 0,
                    $"{LogPrefix} 탭 «{label.text}»의 출하 글꼴 글리프를 0개 셌습니다 — 이 실행은 무효입니다.");

                label.font = builtin;
                Rect builtinOn = InkBoxWith(label, true, out int gBuiltinOn);
                Rect builtinOff = InkBoxWith(label, false, out _);
                label.font = shipped;
                Assert.Greater(gBuiltinOn, 0,
                    $"{LogPrefix} 탭 «{label.text}»의 내장 글꼴 글리프를 0개 셌습니다 — 대조가 공허합니다.");

                float onGap = Mathf.Abs(shippedOn.center.y - builtinOn.center.y);
                float offGap = Mathf.Abs(shippedOff.center.y - builtinOff.center.y);
                if (onGap > worstOnGap) { worstOnGap = onGap; worstName = label.text; }
                worstOffGap = Mathf.Max(worstOffGap, offGap);

                report.Append("    «").Append(label.text)
                      .Append("» 켬: 출하 ").Append(shippedOn.center.y.ToString("+0.00;-0.00"))
                      .Append(" 내장 ").Append(builtinOn.center.y.ToString("+0.00;-0.00"))
                      .Append(" 차 ").Append(onGap.ToString("F2"))
                      .Append("  / 끔: 출하 ").Append(shippedOff.center.y.ToString("+0.00;-0.00"))
                      .Append(" 내장 ").Append(builtinOff.center.y.ToString("+0.00;-0.00"))
                      .Append(" 차 ").Append(offGap.ToString("F2")).Append('\n');

                Assert.LessOrEqual(onGap, FontIndependenceTolerancePoints,
                    $"{LogPrefix} ★ 탭 «{label.text}»의 세로 중심이 글꼴에 따라 {onGap:F2}pt " +
                    $"달라집니다(출하 {shippedOn.center.y:+0.00;-0.00} / 내장 " +
                    $"{builtinOn.center.y:+0.00;-0.00}). 이 파일이 잠그는 것은 「이 폰트에서 " +
                    "가운데」가 아니라 「폰트에 의존하지 않는다」입니다.");
            }

            // 이 실행이 <b>서로 다른 두 글꼴</b>을 실제로 쟀는가. 배치모드에서 OS 페이스가 안 올라와
            // 내장 글꼴로 폴백했다면 위 비교는 같은 글꼴을 두 번 잰 공허한 값이다 — 단언하지 않고
            // (출하 페이스가 폴백이라도 프로덕션은 정상이므로) 사실만 남긴다.
            string witness = worstOffGap >= SwitchEffectFloorPoints
                ? $"두 글꼴은 스위치를 끄면 최대 {worstOffGap:F2}pt 갈라진다(= 신고된 결함의 크기)"
                : "★ 두 글꼴의 끈 값이 같다 — 이 실행은 출하 페이스를 잰 것이 아닐 수 있다(폴백 의심)";
            report.Append("    최악 켬 차 ").Append(worstOnGap.ToString("F2")).Append("pt(«")
                  .Append(worstName).Append("») · 허용 ")
                  .Append(FontIndependenceTolerancePoints.ToString("F2")).Append("pt · ")
                  .Append(witness);
            Debug.Log(report.ToString());
        }

        // ============================================================================
        // 4. 칩 자체의 기하 — 폰트와 무관한 불변식 (+ 음성 대조)
        // ============================================================================

        [Test]
        public void 탭_칩은_스트립_안에서_상하_대칭으로_앉는다()
        {
            RectTransform strip = BuildTabStrip();
            List<Text> labels = TabLabels(strip);
            Assert.Greater(labels.Count, 1,
                $"{LogPrefix} 탭 라벨을 {labels.Count}개만 찾았습니다 — 이 검사가 공허합니다.");

            float stripHeight = strip.sizeDelta.y;
            var report = new StringBuilder();
            report.Append(LogPrefix).Append(" 칩 기하 — 스트립 ")
                  .Append(strip.sizeDelta.x.ToString("F1")).Append('×')
                  .Append(stripHeight.ToString("F1")).Append('\n');

            RectTransform firstChip = null;
            for (int i = 0; i < labels.Count; i++)
            {
                var chip = (RectTransform)labels[i].transform.parent;
                if (firstChip == null) firstChip = chip;

                // PlaceTopLeft 규약: pivot이 좌상단이므로 anchoredPosition.y가 곧 위 변이다.
                float topPad = -chip.anchoredPosition.y;
                float bottomPad = stripHeight - topPad - chip.sizeDelta.y;

                report.Append("    «").Append(labels[i].text).Append("» 칩 ")
                      .Append(chip.sizeDelta.y.ToString("F1")).Append("pt · 위여백 ")
                      .Append(topPad.ToString("F2")).Append(" · 아래여백 ")
                      .Append(bottomPad.ToString("F2")).Append('\n');

                Assert.AreEqual(topPad, bottomPad, 0.01f,
                    $"{LogPrefix} 탭 «{labels[i].text}» 칩의 위여백 {topPad:F2}과 아래여백 " +
                    $"{bottomPad:F2}이 다릅니다 — 스트립 높이와 칩 높이·여백이 서로 어긋났습니다" +
                    "(HeaderTabStripHeight == HeaderTabHeight + HeaderTabStripPad × 2가 깨졌습니다).");

                // 라벨은 칩을 꽉 채워야 한다 — 안 그러면 「가운데」의 기준 상자가 칩이 아니게 된다.
                Rect labelRect = labels[i].rectTransform.rect;
                Assert.AreEqual(chip.sizeDelta.x, labelRect.width, 0.01f,
                    $"{LogPrefix} 탭 «{labels[i].text}»의 라벨 상자 폭 {labelRect.width:F2}이 칩 폭 " +
                    $"{chip.sizeDelta.x:F2}과 다릅니다 — 가로 중앙의 기준이 칩이 아닙니다.");
                Assert.AreEqual(chip.sizeDelta.y, labelRect.height, 0.01f,
                    $"{LogPrefix} 탭 «{labels[i].text}»의 라벨 상자 높이 {labelRect.height:F2}이 칩 높이 " +
                    $"{chip.sizeDelta.y:F2}과 다릅니다 — 세로 중앙의 기준이 칩이 아닙니다.");
            }

            // ★ 음성 대조 — 칩을 1pt 내려 앉히면 이 자가 실제로 빨개지는가.
            Assert.NotNull(firstChip, $"{LogPrefix} 칩을 하나도 못 찾았습니다.");
            float shifted = -(firstChip.anchoredPosition.y - 1f);
            float shiftedBottom = stripHeight - shifted - firstChip.sizeDelta.y;
            Assert.AreNotEqual(shifted, shiftedBottom,
                $"{LogPrefix} 음성 대조 실패 — 칩을 1pt 옮겼는데도 위·아래 여백이 같다고 나옵니다" +
                $"({shifted:F2} 대 {shiftedBottom:F2}). 이 자가 죽었으므로 위의 초록도 무효입니다.");

            report.Append("    음성 대조: 1pt 내리면 위 ").Append(shifted.ToString("F2"))
                  .Append(" 대 아래 ").Append(shiftedBottom.ToString("F2")).Append(" (검출됨)");
            Debug.Log(report.ToString());
        }
    }
}
