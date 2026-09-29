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
    ///
    /// ============================================================================
    /// ★★ 2026-09-30 — 사용자가 신고 대상을 정정해서 범위가 <b>탭바에서 부채꼴까지</b> 넓어졌다
    /// ============================================================================
    /// 같은 신고의 다른 화면(캐릭터 우클릭 부채꼴)에 대해 사용자가 정정했다:
    /// <i>"아이콘 말고 아이콘 설명 텍스트를 말하는 거야."</i> 가리킨 것은 <b>호버 이름표</b>
    /// (버튼 원 옆에 뜨는 「오늘 할일」 알약)다. 부채꼴에서 글자가 사는 표면은 <b>정확히 셋</b>이고
    /// (<c>GearRadialMenuWidget</c>의 <c>UiChrome.AddText</c> 호출 3곳 — 음성 대조로 다른 Text 생성
    /// 경로 0건) <b>셋 다 같은 스위치에 매달려 있다</b>. 아래 §5가 그 셋을 프로덕션 빌더로 구워 잰다.
    ///
    /// <para><b>실측 결론</b>: 세 표면 모두 <c>3933533</c>(탭바 수리)으로 <b>이미 함께 고쳐졌다</b> —
    /// 부채꼴에 별도 결함은 없었다. 고장 상태와의 차이는 아래와 같다(pt).
    /// <code>
    ///                          끔(신고 상태)  켬(현행)   상자 안 위/아래 여백
    ///  호버 이름표 18pt 알약       −1.00        0.00      4/2  →  3/3
    ///  최초 1회 안내 18pt 알약     −1.00        0.00      4/2  →  3/3
    ///  오늘 할일 배지 16pt 원      −1.50       −0.50      4/1  →  3/2
    /// </code>
    /// 배지에 남는 0.50pt는 잉크 높이 11pt(홀수)가 16pt(짝수) 상자에 앉는 <b>구조적 하한</b>이다.</para>
    ///
    /// <para>★★ <b>그리고 이 확장이 자기 함정을 하나 잡았다</b>: 위 탭 칩용 허용폭
    /// <see cref="InkCenterTolerancePoints"/>(1.00pt)를 부채꼴에 그대로 쓰면 <b>고장 상태
    /// (−1.00pt)가 통과한다</b> — 1차 초안이 그 상태로 초록을 냈다. 같은 상수가 탭 칩(끔 −1.50)에서는
    /// 물고 이름표에서는 죽는다. 그래서 §5는 <see cref="FanInkCenterTolerancePoints"/>를 따로
    /// 유도하고, <b>허용폭이 고장값보다 좁다는 것 자체를 같은 실행에서 단언</b>한다.</para>
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

        // ====================================================================================
        // 5. 부채꼴 메뉴의 글자 3종 — 호버 이름표 · 최초 1회 안내 · 오늘 할일 배지
        // ====================================================================================
        // ★★ 왜 여기에 붙였나 — 사용자가 2026-09-29 신고를 정정했다.
        //    원문: "메뉴들에서도 상자들 안 정렬이 안 됨" → 정정 "아이콘 말고 아이콘 설명 텍스트를
        //    말하는 거야." 가리킨 것은 캡처에 떠 있던 <b>호버 이름표</b>(체크리스트 원 옆의 「오늘
        //    할일」 알약)다. 즉 탭 칩과 <b>같은 병</b>이고 같은 문(UiChrome.AddText)에서 태어난다 —
        //    그래서 같은 파일에 둔다. 파일을 쪼개면 이 두 표면이 같은 스위치에 매달려 있다는 사실이
        //    두 군데로 흩어진다.
        //
        //    부채꼴 글자는 탭 칩과 세 가지가 다르고 그 셋이 전부 <b>새 실패 모드</b>다:
        //      ⑴ 상자 높이가 18pt(탭 28pt) · 글자가 FontCaption 10pt(탭 14pt) — 잉크 높이의
        //         홀짝이 달라 구조적 하한이 다시 계산돼야 한다.
        //      ⑵ 알약 폭이 <b>글자에서 파생</b>된다(preferredWidth + HoverLabelPaddingPoints).
        //         탭 칩처럼 고정 상수가 아니므로 가로 중심이 「좌우 여백 7/7」로 자동 보장되지 않는다.
        //      ⑶ 배지 글자는 <b>숫자</b>다("3" / "9+"). alignByGeometry의 대가(「내용이 바뀌면
        //         정렬도 다시 잡힌다」, UiChrome.AddText 문서)가 실제로 문제가 되는 유일한 자리다 —
        //         한글이 아니라 받침 없는 글리프라 폰트 수치 기준과 가장 크게 갈라진다.
        //
        // ★★★ 그리고 ⑴이 실제로 물었다 — <b>이 절은 허용폭 1.00pt를 쓸 수 없다.</b>
        //    위 탭 칩 검사의 <see cref="InkCenterTolerancePoints"/>는 1.00pt인데, 부채꼴 이름표·안내의
        //    <b>고장 상태(스위치 끔) 세로 중심이 정확히 −1.00pt</b>다(아래 실측). 즉 그 허용폭을
        //    그대로 베끼면 <b>고장 상태가 게이트를 통과한다</b> — alignByGeometry를 지워도 초록인
        //    자가 된다. 탭 칩(끔 −1.50)과 배지(끔 −1.50)에서는 1.00pt가 물기 때문에 <b>같은 상수가
        //    어떤 표면에서는 살아 있고 어떤 표면에서는 죽는다.</b> 이 저장소가 반복해 당한
        //    「죽은 프로브가 산 프로브와 똑같이 생겼다」의 새 형태이고, 이 파일을 짜는 동안 실제로
        //    1차 초안이 그 상태로 초록을 냈다. 그래서 아래 <see cref="FanInkCenterTolerancePoints"/>를
        //    따로 유도하고, <b>게이트가 고장 상태를 실제로 거부하는지를 같은 테스트가 단언</b>한다.

        /// <summary>부채꼴 글자의 허용폭(pt) — 탭 칩용 <see cref="InkCenterTolerancePoints"/>보다
        /// <b>좁다</b>. 유도:
        /// <list type="bullet">
        /// <item><b>아래</b>: 글리프는 정수 pt에만 앉으므로, 상자 높이와 잉크 높이의 <b>홀짝이 다르면</b>
        ///   완전 중앙이 불가능하고 <c>|중심|</c>의 구조적 하한이 <b>0.50pt</b>다(배지 16pt 상자 ·
        ///   잉크 11pt가 실제로 그 경우다). 이보다 좁게 잡으면 고칠 방법이 없는 빨강이 된다.</item>
        /// <item><b>위</b>: 고장 상태(스위치 끔)의 <c>|중심|</c> 최솟값이 <b>1.00pt</b>다. 허용폭이
        ///   1.00pt 이상이면 <b>고장 상태가 통과</b>하므로 게이트가 죽는다.</item>
        /// </list>
        /// ⇒ 열린 구간 (0.50, 1.00)의 <b>가운데</b>인 <c>0.75</c>를 쓴다 — 양쪽으로 0.25pt씩 여유가
        /// 같고, 어느 쪽 사정이 한 격자 절반만큼 움직여도 판정이 뒤집히지 않는다. 「지금 통과하는 값」을
        /// 얼린 것이 아니라 <b>하한과 고장값 사이</b>에서 유도한 값이다.
        /// <para>★ 이 상수가 고장값(1.00)에 닿거나 넘으면 아래
        /// <c>게이트가_고장_상태를_거부한다</c> 단언이 먼저 빨개진다 — 상수만 슬쩍 넓혀 초록을
        /// 만드는 길을 막아 둔 것이다.</para></summary>
        private const float FanInkCenterTolerancePoints = 0.75f;

        /// <summary>부채꼴 글자에서 <b>두 글꼴이 같은 값</b>이라고 말할 수 있는 폭(pt).
        /// 탭 칩쪽(<see cref="FontIndependenceTolerancePoints"/> 0.50)보다 <b>넓다</b>: 두 글꼴의 잉크
        /// 높이 홀짝이 서로 다르면 각자의 구조적 하한이 <b>반대 방향으로 0.50pt씩</b> 잡힐 수 있어
        /// 차이의 상한이 <b>1.00pt</b>가 된다(탭 칩은 14pt 한글로 양쪽 잉크 높이가 같았다).
        /// 그보다 크게 갈라지면 정렬이 다시 <b>폰트 수치</b>에 매달려 있다는 뜻이다 — 실측된 고장
        /// 상태의 글꼴 간 차이는 2.00pt였으므로 이 폭으로도 그 회귀는 잡힌다.</summary>
        private const float FanFontIndependenceTolerancePoints = 1.0f;

        /// <summary>한 표면 · 한 문안의 실측 한 줄.</summary>
        private struct InkProbe
        {
            public string Surface;      // 「호버 이름표」 등 — 실패 메시지에 그대로 쓴다.
            public string Content;
            public Vector2 BoxSize;     // 상자(알약/배지)의 sizeDelta.
            public Vector2 LabelSize;   // 라벨 rect — 상자와 같아야 「가운데」의 기준이 상자다.
            public Rect On;             // alignByGeometry 켬.
            public Rect Off;            // 끔(= 신고된 상태의 기준).
            public int GlyphsOn;
            public int GlyphsExpected;

            // 글꼴 대조 테스트가 <b>같은 자리를 다시 재려면</b> 이 둘이 필요하다. 호버 이름표는
            // 문안 다섯이 <b>Text 하나</b>를 돌려 쓰고 알약 폭만 바뀌므로, 다시 잴 때도 문안과 폭을
            // <b>한 쌍으로</b> 되돌려야 한다(<see cref="Restore"/>).
            public RectTransform Box;
            public Text Label;

            /// <summary>이 줄을 쟀던 그 상태로 상자와 글자를 되돌린다.</summary>
            public void Restore()
            {
                Box.sizeDelta = BoxSize;
                Label.text = Content;
            }
        }

        private static object PrivateField(object target, string name)
        {
            FieldInfo f = target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(f, $"{LogPrefix} {target.GetType().Name}.{name} 필드를 못 찾았습니다 — " +
                "이름이 바뀌었다면 이 파일도 함께 고쳐야 합니다. 그 전까지 아래 숫자는 대상 없이 " +
                "돌고, 그 상태의 초록은 아무것도 증명하지 않습니다.");
            return f.GetValue(target);
        }

        private static object InvokePrivate(object target, string name, params object[] args)
        {
            MethodInfo m = target.GetType()
                .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(m, $"{LogPrefix} {target.GetType().Name}.{name}()을 못 찾았습니다 — " +
                "프로덕션 빌더가 사라졌거나 이름이 바뀌었습니다. 이 파일도 함께 고쳐야 합니다.");
            return m.Invoke(target, args);
        }

        /// <summary>부채꼴 위젯 껍데기. <c>BuildUi</c>는 부르지 않는다 — 캔버스가 필요한 것은
        /// 스크린 좌표 계산뿐이고, 이 파일이 재는 것은 <b>상자 안에서의 상대 위치</b>라 캔버스와
        /// 무관하다(<c>_canvas</c>가 null이므로 <c>OnDestroy</c>도 아무것도 파괴하지 않는다).</summary>
        private GearRadialMenuWidget SpawnFanWidget()
        {
            var host = new GameObject("부채꼴글자측정");
            _spawned.Add(host);
            return host.AddComponent<GearRadialMenuWidget>();
        }

        /// <summary>공백은 잉크가 없다 — <c>TextGenerator</c>가 퇴화 사각형을 내므로
        /// <see cref="InkBoxOf"/>가 건너뛴다. 기대 글리프 수는 그래서 「공백 아닌 글자 수」다.</summary>
        private static int InkyGlyphCount(string s)
        {
            int n = 0;
            for (int i = 0; i < s.Length; i++) if (s[i] != ' ') n++;
            return n;
        }

        private static InkProbe Measure(string surface, RectTransform box, Text label, string content)
        {
            label.text = content;
            var probe = new InkProbe
            {
                Surface = surface,
                Content = content,
                BoxSize = box.sizeDelta,
                LabelSize = label.rectTransform.rect.size,
                GlyphsExpected = InkyGlyphCount(content),
                Box = box,
                Label = label,
            };
            probe.On = InkBoxWith(label, true, out int glyphsOn);
            probe.Off = InkBoxWith(label, false, out _);
            probe.GlyphsOn = glyphsOn;
            return probe;
        }

        /// <summary>호버 이름표 — 프로덕션 빌더 <c>BuildHoverLabel</c>을 그대로 돌리고, 문안별 알약
        /// 폭도 프로덕션이 그 안에서 재어 둔 <c>_nameWidths</c>·<c>_quitArmedLabelWidth</c>를 쓴다
        /// (테스트가 <c>preferredWidth + 여백</c>을 다시 조립하면 그 순간 프로덕션과 갈라진다).</summary>
        private List<InkProbe> ProbeHoverLabel()
        {
            GearRadialMenuWidget widget = SpawnFanWidget();
            InvokePrivate(widget, "BuildHoverLabel");

            var pill = (RectTransform)PrivateField(widget, "_hoverLabel");
            var text = (Text)PrivateField(widget, "_hoverLabelText");
            var widths = (float[])PrivateField(widget, "_nameWidths");
            var quitWidth = (float)PrivateField(widget, "_quitArmedLabelWidth");
            Assert.NotNull(pill, $"{LogPrefix} 호버 이름표 알약이 만들어지지 않았습니다.");
            _spawned.Add(pill.gameObject);

            var probes = new List<InkProbe>(GearRadialMenuWidget.ButtonCount + 1);
            for (int i = 0; i < GearRadialMenuWidget.ButtonCount; i++)
            {
                // ApplyHoverLabel이 하는 것과 같은 두 줄 — 글자와 알약 폭은 한 쌍으로 바뀐다.
                pill.sizeDelta = new Vector2(widths[i], GearRadialMenuWidget.HoverLabelHeightPoints);
                probes.Add(Measure("호버 이름표", pill, text, GearRadialMenuWidget.NameOf(i)));
            }
            pill.sizeDelta = new Vector2(quitWidth, GearRadialMenuWidget.HoverLabelHeightPoints);
            probes.Add(Measure("호버 이름표(무장)", pill, text, GearRadialMenuWidget.QuitArmedLabel));
            return probes;
        }

        /// <summary>최초 1회 안내 알약 — 빌더가 문안·폭을 스스로 정하므로 그대로 읽는다.</summary>
        private InkProbe ProbeOnboardingHint()
        {
            GearRadialMenuWidget widget = SpawnFanWidget();
            InvokePrivate(widget, "BuildOnboardingHint");

            var pill = (RectTransform)PrivateField(widget, "_onboardingHint");
            var text = (Text)PrivateField(widget, "_onboardingHintText");
            Assert.NotNull(pill, $"{LogPrefix} 최초 1회 안내 알약이 만들어지지 않았습니다.");
            _spawned.Add(pill.gameObject);
            pill.gameObject.SetActive(true);   // 빌더가 꺼 둔다(알파 0으로 시작) — 재는 데는 켜 둔다.

            return Measure("최초 1회 안내", pill, text, text.text);
        }

        /// <summary>오늘 할일 배지 — 프로덕션 빌더 <c>BuildButton</c>을 [오늘 할일] 슬롯으로 돌린다.
        /// 문안은 <c>RefreshDynamicContent</c>가 실제로 만드는 두 형태(한 자리 수 · <c>"9+"</c>)다.</summary>
        private List<InkProbe> ProbeTodoBadge()
        {
            GearRadialMenuWidget widget = SpawnFanWidget();
            object view = InvokePrivate(widget, "BuildButton", (int)GearMenuButton.Todo);
            Assert.NotNull(view, $"{LogPrefix} [오늘 할일] 버튼 뷰가 만들어지지 않았습니다.");

            var group = (RectTransform)view.GetType().GetField("Group").GetValue(view);
            _spawned.Add(group.gameObject);
            var badge = (RectTransform)view.GetType().GetField("Badge").GetValue(view);
            var text = (Text)view.GetType().GetField("BadgeText").GetValue(view);
            Assert.NotNull(badge, $"{LogPrefix} [오늘 할일] 배지가 만들어지지 않았습니다 — " +
                "BuildButton의 Todo 분기가 사라졌다면 이 검사는 대상이 없습니다.");
            badge.gameObject.SetActive(true);

            return new List<InkProbe>
            {
                Measure("오늘 할일 배지", badge, text, "3"),
                Measure("오늘 할일 배지", badge, text, "9+"),
            };
        }

        /// <summary>실측 줄 하나를 사람이 읽는 형태로. 숫자를 전부 남겨 다음 라운드가 재지 않게 한다.</summary>
        private static void AppendRow(StringBuilder sb, InkProbe p)
        {
            sb.Append("    ").Append(p.Surface).Append(" «").Append(p.Content).Append("» 상자 ")
              .Append(p.BoxSize.x.ToString("F2")).Append('×').Append(p.BoxSize.y.ToString("F2"))
              .Append("  잉크 y=[").Append(p.On.yMin.ToString("F2")).Append(", ")
              .Append(p.On.yMax.ToString("F2")).Append("] x=[").Append(p.On.xMin.ToString("F2"))
              .Append(", ").Append(p.On.xMax.ToString("F2")).Append(']')
              .Append("  중심 (").Append(p.On.center.x.ToString("+0.00;-0.00")).Append(", ")
              .Append(p.On.center.y.ToString("+0.00;-0.00")).Append(')')
              .Append("  / 끔 중심 (").Append(p.Off.center.x.ToString("+0.00;-0.00")).Append(", ")
              .Append(p.Off.center.y.ToString("+0.00;-0.00")).Append(')')
              .Append("  글리프 ").Append(p.GlyphsOn).Append('/').Append(p.GlyphsExpected)
              .Append('\n');
        }

        /// <summary>한 표면 묶음을 전부 단언한다 — 양성 대조(글리프 수)부터 걸고, 그 다음에만
        /// 중심을 판정한다. 0글리프에서 「가운데다」라고 말하는 것이 이 저장소의 거짓 통과 형태다.
        ///
        /// <para>★★ 마지막 단언이 이 함수의 핵심이다: <b>이 게이트가 고장 상태를 실제로 거부하는가.</b>
        /// 허용폭이 스위치 끔 상태의 <c>|중심|</c>보다 넓으면 <c>alignByGeometry</c>를 지워도 초록이
        /// 남는다 — 1차 초안이 정확히 그 상태였다(부채꼴 이름표 끔 −1.00pt 대 허용 1.00pt).
        /// 그래서 <b>허용폭과 고장값의 대소를 같은 실행에서 못박는다.</b></para></summary>
        private static void AssertCentered(List<InkProbe> probes, StringBuilder sb, out float worstSwitch)
        {
            worstSwitch = float.MaxValue;
            float worstOffAbs = 0f;         // 고장 상태의 |세로 중심| 최댓값 — 게이트 교정용.
            string worstOffName = null;
            for (int i = 0; i < probes.Count; i++)
            {
                InkProbe p = probes[i];
                AppendRow(sb, p);

                Assert.Greater(p.GlyphsOn, 0,
                    $"{LogPrefix} {p.Surface} «{p.Content}»의 글리프를 0개 셌습니다 — 이 실행의 정렬 " +
                    "판정은 전부 무효입니다(배치모드 -nographics에서 글꼴이 안 올라온 경우).");
                Assert.AreEqual(p.GlyphsExpected, p.GlyphsOn,
                    $"{LogPrefix} {p.Surface} «{p.Content}»(잉크 있는 글자 {p.GlyphsExpected}자)에서 " +
                    $"글리프 {p.GlyphsOn}개를 셌습니다 — 잉크 상자가 글자 일부만 담고 있어 " +
                    "중심값이 거짓입니다.");

                // 「가운데」의 기준 상자가 알약/배지 자신인가. 라벨이 Stretch로 상자를 꽉 채우지
                // 않으면 아래 중심값은 <b>다른 사각형</b>을 기준으로 잰 것이 된다.
                Assert.AreEqual(p.BoxSize.x, p.LabelSize.x, 0.01f,
                    $"{LogPrefix} {p.Surface} «{p.Content}»의 라벨 폭 {p.LabelSize.x:F2}이 상자 폭 " +
                    $"{p.BoxSize.x:F2}과 다릅니다 — 가로 중앙의 기준이 상자가 아닙니다.");
                Assert.AreEqual(p.BoxSize.y, p.LabelSize.y, 0.01f,
                    $"{LogPrefix} {p.Surface} «{p.Content}»의 라벨 높이 {p.LabelSize.y:F2}이 상자 높이 " +
                    $"{p.BoxSize.y:F2}과 다릅니다 — 세로 중앙의 기준이 상자가 아닙니다.");

                Assert.LessOrEqual(Mathf.Abs(p.On.center.y), FanInkCenterTolerancePoints,
                    $"{LogPrefix} ★ {p.Surface} «{p.Content}»의 글자가 상자 세로 중심에서 " +
                    $"{p.On.center.y:+0.00;-0.00}pt 벗어났습니다(위 여백 " +
                    $"{p.BoxSize.y * 0.5f - p.On.yMax:F2} 대 아래 여백 " +
                    $"{p.On.yMin + p.BoxSize.y * 0.5f:F2}, 허용 " +
                    $"{FanInkCenterTolerancePoints:F2}). 2026-09-29 사용자 신고 " +
                    "「메뉴들에서도 상자들 안 정렬이 안 됨」이 바로 이 형태입니다.");
                Assert.LessOrEqual(Mathf.Abs(p.On.center.x), FanInkCenterTolerancePoints,
                    $"{LogPrefix} ★ {p.Surface} «{p.Content}»의 글자가 상자 가로 중심에서 " +
                    $"{p.On.center.x:+0.00;-0.00}pt 벗어났습니다(왼 여백 " +
                    $"{p.On.xMin + p.BoxSize.x * 0.5f:F2} 대 오른 여백 " +
                    $"{p.BoxSize.x * 0.5f - p.On.xMax:F2}, 허용 " +
                    $"{FanInkCenterTolerancePoints:F2}).");

                worstSwitch = Mathf.Min(worstSwitch,
                    Mathf.Abs(p.On.center.y - p.Off.center.y));
                if (Mathf.Abs(p.Off.center.y) > worstOffAbs)
                {
                    worstOffAbs = Mathf.Abs(p.Off.center.y);
                    worstOffName = p.Content;
                }
            }

            // ★★ 게이트 교정 — 허용폭이 <b>고장 상태를 거부할 만큼 좁은가.</b>
            //    이 단언이 없으면 「상수를 넓혀 초록을 만든다」와 「표면이 실제로 가운데다」가
            //    똑같이 생긴다. 이 저장소의 공통 처방("계산기는 알려진 값으로 먼저 교정한다")의
            //    정렬판이고, 여기서 쓰는 알려진 값은 <b>고쳐지기 전의 그 화면</b>이다.
            Assert.Greater(worstOffAbs, FanInkCenterTolerancePoints,
                $"{LogPrefix} ★ 게이트 교정 실패 — 허용폭 {FanInkCenterTolerancePoints:F2}pt가 " +
                $"고장 상태(alignByGeometry 끔)의 최악 |세로 중심| {worstOffAbs:F2}pt" +
                (worstOffName == null ? "" : $"(«{worstOffName}»)") + "보다 좁지 않습니다. " +
                "즉 이 자는 신고된 그 화면을 통과시킵니다 — 위의 초록은 아무것도 증명하지 않습니다. " +
                "허용폭을 넓혔다면 되돌리고, 글꼴이 바뀌어 고장값 자체가 작아졌다면 " +
                "FanInkCenterTolerancePoints 문서의 유도(하한 0.50 · 고장값 1.00)를 다시 하십시오.");

            sb.Append("    게이트 교정: 허용 ").Append(FanInkCenterTolerancePoints.ToString("F2"))
              .Append("pt < 고장상태 최악 |중심| ").Append(worstOffAbs.ToString("F2"))
              .Append("pt («").Append(worstOffName).Append("») — 고장 상태를 거부한다\n");
        }

        /// <summary>같은 자리를 <b>옛 내장 글꼴</b>로 다시 재서, 세로 중심이 글꼴에 의존하지 않음을
        /// 확인한다. 이 파일 본체(클래스 문서 3항)의 부채꼴판이다.</summary>
        private static void AssertFontIndependent(List<InkProbe> probes, StringBuilder sb)
        {
            Font builtin = Resources.GetBuiltinResource<Font>(UiChrome.BuiltinFontResource);
            Assert.NotNull(builtin,
                $"{LogPrefix} 내장 글꼴 '{UiChrome.BuiltinFontResource}'을 못 불렀습니다 — 대조할 짝이 없습니다.");

            float worstOnGap = 0f, worstOffGap = 0f;
            string worstName = null;
            for (int i = 0; i < probes.Count; i++)
            {
                InkProbe p = probes[i];
                p.Restore();                       // 호버 이름표는 Text 하나를 돌려 쓴다 — 폭·문안을 짝으로.
                Font shipped = p.Label.font;

                p.Label.font = builtin;
                Rect builtinOn = InkBoxWith(p.Label, true, out int gBuiltinOn);
                Rect builtinOff = InkBoxWith(p.Label, false, out _);
                p.Label.font = shipped;

                Assert.Greater(gBuiltinOn, 0,
                    $"{LogPrefix} {p.Surface} «{p.Content}»의 내장 글꼴 글리프를 0개 셌습니다 — " +
                    "대조가 공허합니다.");

                float onGap = Mathf.Abs(p.On.center.y - builtinOn.center.y);
                float offGap = Mathf.Abs(p.Off.center.y - builtinOff.center.y);
                if (onGap > worstOnGap) { worstOnGap = onGap; worstName = p.Content; }
                worstOffGap = Mathf.Max(worstOffGap, offGap);

                sb.Append("    ").Append(p.Surface).Append(" «").Append(p.Content)
                  .Append("» 켬: 출하 ").Append(p.On.center.y.ToString("+0.00;-0.00"))
                  .Append(" 내장 ").Append(builtinOn.center.y.ToString("+0.00;-0.00"))
                  .Append(" 차 ").Append(onGap.ToString("F2"))
                  .Append("  / 끔: 출하 ").Append(p.Off.center.y.ToString("+0.00;-0.00"))
                  .Append(" 내장 ").Append(builtinOff.center.y.ToString("+0.00;-0.00"))
                  .Append(" 차 ").Append(offGap.ToString("F2")).Append('\n');

                Assert.LessOrEqual(onGap, FanFontIndependenceTolerancePoints,
                    $"{LogPrefix} ★ {p.Surface} «{p.Content}»의 세로 중심이 글꼴에 따라 {onGap:F2}pt " +
                    $"달라집니다(출하 {p.On.center.y:+0.00;-0.00} / 내장 " +
                    $"{builtinOn.center.y:+0.00;-0.00}, 허용 " +
                    $"{FanFontIndependenceTolerancePoints:F2}). 잠그는 것은 「이 폰트에서 가운데」가 " +
                    "아니라 「폰트에 의존하지 않는다」입니다.");
            }

            // ★ 이 실행이 <b>서로 다른 두 글꼴</b>을 실제로 쟀는가 — 단언하지 않고 사실만 남긴다
            //   (배치모드 -nographics에서 OS 페이스가 안 올라오면 폴백이라 비교가 공허해지는데,
            //    그때도 프로덕션은 정상이므로 빨강을 낼 근거가 되지 않는다).
            sb.Append("    최악 켬 차 ").Append(worstOnGap.ToString("F2")).Append("pt(«")
              .Append(worstName).Append("») · 허용 ")
              .Append(FanFontIndependenceTolerancePoints.ToString("F2")).Append("pt · ")
              .Append(worstOffGap >= SwitchEffectFloorPoints
                  ? $"두 글꼴은 스위치를 끄면 최대 {worstOffGap:F2}pt 갈라진다(= 신고된 결함의 크기)"
                  : "★ 두 글꼴의 끈 값이 같다 — 이 실행은 출하 페이스를 잰 것이 아닐 수 있다(폴백 의심)")
              .Append('\n');
        }

        [Test]
        public void 부채꼴_호버_이름표_글자는_알약_가운데에_앉는다()
        {
            List<InkProbe> probes = ProbeHoverLabel();
            Assert.AreEqual(GearRadialMenuWidget.ButtonCount + 1, probes.Count,
                $"{LogPrefix} 이름표 문안을 {probes.Count}개만 쟀습니다 — 버튼 " +
                $"{GearRadialMenuWidget.ButtonCount}개 + 무장 문구 1개가 기대값입니다.");

            var sb = new StringBuilder();
            sb.Append(LogPrefix).Append(" 부채꼴 호버 이름표 — 확정 페이스='")
              .Append(UiChrome.ResolvedFontName).Append("' · 알약 높이 ")
              .Append(GearRadialMenuWidget.HoverLabelHeightPoints.ToString("F1"))
              .Append("pt · 글자 ").Append(UiChrome.FontCaption).Append("pt\n");

            AssertCentered(probes, sb, out float worstSwitch);

            // ★ 살아있음 대조 — 스위치를 껐을 때 잉크가 실제로 움직이는가.
            //   여기서 움직임이 없으면 위 초록은 「무엇도 거를 수 없는 자」의 초록이다.
            Assert.GreaterOrEqual(worstSwitch, SwitchEffectFloorPoints,
                $"{LogPrefix} 살아있음 대조 실패 — alignByGeometry를 껐는데 어떤 문안의 잉크 세로 " +
                $"중심도 {worstSwitch:F2}pt 이상 움직이지 않았습니다(하한 " +
                $"{SwitchEffectFloorPoints:F2}). 이 자는 정렬 변화를 못 보고 있으므로 위의 초록도 " +
                "무효입니다.");

            sb.Append("    스위치 효과 최소 ").Append(worstSwitch.ToString("F2")).Append("pt");
            Debug.Log(sb.ToString());
        }

        [Test]
        public void 부채꼴_최초_안내_글자는_알약_가운데에_앉는다()
        {
            var probes = new List<InkProbe> { ProbeOnboardingHint() };

            var sb = new StringBuilder();
            sb.Append(LogPrefix).Append(" 부채꼴 최초 1회 안내 — 확정 페이스='")
              .Append(UiChrome.ResolvedFontName).Append("'\n");

            AssertCentered(probes, sb, out float worstSwitch);
            Assert.GreaterOrEqual(worstSwitch, SwitchEffectFloorPoints,
                $"{LogPrefix} 살아있음 대조 실패 — 안내 문안에서 alignByGeometry를 껐는데 잉크 세로 " +
                $"중심이 {worstSwitch:F2}pt밖에 움직이지 않았습니다(하한 " +
                $"{SwitchEffectFloorPoints:F2}). 이 실행의 초록은 무효입니다.");

            sb.Append("    스위치 효과 ").Append(worstSwitch.ToString("F2")).Append("pt");
            Debug.Log(sb.ToString());
        }

        [Test]
        public void 오늘_할일_배지_숫자는_원_가운데에_앉는다()
        {
            List<InkProbe> probes = ProbeTodoBadge();

            var sb = new StringBuilder();
            sb.Append(LogPrefix).Append(" 오늘 할일 배지 — 확정 페이스='")
              .Append(UiChrome.ResolvedFontName).Append("'\n");

            // ★ 배지만 살아있음 대조를 <b>단언하지 않는다</b>: 숫자·기호는 한글과 달리 폰트 수치와
            //   잉크가 우연히 맞아떨어질 수 있고(디센더가 없다), 그러면 스위치 효과가 0이 된다.
            //   그것은 「자가 죽었다」가 아니라 「이 문안에서는 애초에 병이 없었다」는 뜻이다.
            //   ★ 그래도 <b>게이트 교정</b>은 AssertCentered 안에서 그대로 걸린다 — 그쪽이 재는 것은
            //     「스위치가 움직였는가」가 아니라 「이 허용폭이 고장 상태를 거부하는가」로, 배지의
            //     고장값(−1.50pt)은 이 표면에서도 실재한다.
            AssertCentered(probes, sb, out float worstSwitch);

            sb.Append("    스위치 효과 최소 ").Append(worstSwitch.ToString("F2"))
              .Append("pt (0이면 이 문안은 스위치 이전에도 정상이었다는 뜻)");
            Debug.Log(sb.ToString());
        }

        /// <summary>★ 이 절의 본체 — 폰트를 또 갈아타도 부채꼴 글자가 안 밀리는가.
        /// <para>세 표면을 <b>한 테스트에</b> 담는 이유: 사용자가 「메뉴<b>들</b>에서도」(복수)라고
        /// 신고했고, 이 셋은 같은 문(<see cref="UiChrome.AddText"/>)에 <b>함께</b> 매달려 있다.
        /// 한 곳만 잠그면 다음 글꼴 교체 때 나머지 둘이 조용히 밀린다.</para></summary>
        [Test]
        public void 부채꼴_글자_세로_중심은_글꼴에_의존하지_않는다()
        {
            var probes = new List<InkProbe>();
            probes.AddRange(ProbeHoverLabel());
            probes.Add(ProbeOnboardingHint());
            probes.AddRange(ProbeTodoBadge());
            Assert.AreEqual(GearRadialMenuWidget.ButtonCount + 4, probes.Count,
                $"{LogPrefix} 부채꼴 문안을 {probes.Count}개만 쟀습니다 — 이름표 " +
                $"{GearRadialMenuWidget.ButtonCount}개 + 무장 1 + 안내 1 + 배지 2가 기대값입니다. " +
                "표면이 하나 빠졌다면 그 표면은 이 검사가 보지 않습니다.");

            var sb = new StringBuilder();
            sb.Append(LogPrefix).Append(" 부채꼴 글꼴 의존성 — 출하 '").Append(UiChrome.ResolvedFontName)
              .Append("' 대 내장 '").Append(UiChrome.BuiltinFontResource).Append("'\n");

            AssertFontIndependent(probes, sb);
            Debug.Log(sb.ToString());
        }
    }
}
