using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-02 — 사용자 지시 <b>"캐릭터창 둘레로도 그림자들이 있는데 다 없애줘 깔끔하게"</b>의
    /// 회귀 잠금. 여기 있던 <c>UiChromeSoftShadowTests</c>(그림자 스프라이트의 램프/캐시/기하를 재던
    /// 8건)는 잴 대상이 사라져 통째로 삭제됐고, 이 파일이 그 자리를 <b>반대 방향</b>으로 대신한다.
    ///
    /// <para><b>세 겹으로 잠근다</b> — 하나만으로는 전부 빠져나간다:</para>
    /// <list type="number">
    ///   <item>API 부재(리플렉션) — 함수를 되살려 놓고 호출만 안 하는 상태를 잡는다.</item>
    ///   <item>소스 감사 — 함수가 다른 이름으로 부활해도 <b>호출 문자열</b>로 잡는다.</item>
    ///   <item>씬 트리 — <b>이름이 아니라 생김새</b>(거의 검은 반투명 겹)로 센다. 이름만 바꾼 잔재를
    ///         이름 검사로는 못 잡기 때문이다. 형제 <b>개수</b>도 함께 고정한다.</item>
    /// </list>
    ///
    /// <para>★ 세 번째 그물이 실제로 무는지는 <see cref="검은_반투명_겹을_넣으면_같은_검사가_빨개진다"/>가
    /// <b>같은 판정 함수</b>로 증명한다(거짓 초록 방지).</para>
    /// </summary>
    public sealed class UiChromeNoShadowTests
    {
        private const string LogPrefix = "[그림자없음-TEST]";

        /// <summary>그림자의 <b>정의</b>: 거의 검은 색 + 반투명. 실제 그림자는 예외 없이 이 모양이고,
        /// 이름을 무엇으로 바꾸든 이 조건을 벗어나면 그림자로 보이지 않는다.</summary>
        private static bool LooksLikeShadow(Graphic g)
        {
            Color c = g.color;
            if (c.a <= 0.02f) return false;      // 안 보인다(투명 히트 영역 등)
            if (c.a >= 0.999f) return false;     // 불투명 면은 그림자가 아니다
            return UiChrome.RelativeLuminance(c) <= 0.05f;
        }

        private static List<string> ShadowLikePieces(Transform root)
        {
            var hits = new List<string>();
            foreach (Graphic g in root.GetComponentsInChildren<Graphic>(true))
            {
                if (LooksLikeShadow(g)) hits.Add($"{g.name}(α={g.color.a:F2}, L={UiChrome.RelativeLuminance(g.color):F3})");
            }
            return hits;
        }

        // ============================================================================
        // ① API 부재 — 되살려 놓고 안 쓰는 상태도 실패다
        // ============================================================================

        [Test]
        public void UiChrome에는_그림자라는_이름의_멤버가_하나도_없다()
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

            var offenders = new List<string>();
            foreach (MemberInfo m in typeof(UiChrome).GetMembers(all))
            {
                if (m.Name.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    offenders.Add($"{m.MemberType} {m.Name}");
            }

            // 그물이 비어 있지 않다는 확인 — 경로가 틀려 0개를 훑고 통과하는 것을 막는다.
            Assert.Greater(typeof(UiChrome).GetMembers(all).Length, 50,
                $"{LogPrefix} UiChrome 멤버가 비정상적으로 적게 잡혔습니다 — 리플렉션 플래그를 의심하세요.");
            Assert.IsEmpty(offenders,
                $"{LogPrefix} 그림자 API가 되살아났습니다: {string.Join(", ", offenders)}. " +
                "2026-09-02 사용자 지시로 UI 그림자는 전부 없앴습니다 — 되살리려면 그 결정을 먼저 뒤집으세요.");
        }

        // ============================================================================
        // ② 소스 감사 — 다른 이름으로 부활해도 호출 문자열이 남는다
        // ============================================================================

        [Test]
        public void 제품_소스에_그림자_생성_호출이_한_줄도_없다()
        {
            string scriptsRoot = Path.Combine(Application.dataPath, "_Project", "Scripts");
            string testsRoot = (Path.Combine(scriptsRoot, "Tests") + Path.DirectorySeparatorChar).Replace('\\', '/');

            var files = new List<string>(Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories));
            files.RemoveAll(p => p.Replace('\\', '/').StartsWith(testsRoot, System.StringComparison.Ordinal));
            Assert.GreaterOrEqual(files.Count, 40,
                $"{LogPrefix} 스캔 대상이 {files.Count}개뿐입니다 — 경로 계산 오류로 허위 통과할 위험.");

            string[] needles =
            {
                "AddShadow(", "AddShadowLayer(", "AddSoftShadowCircle(",
                "SoftShadowFill(", "SoftShadowCircle(", "PanelShadow",
            };

            var hits = new List<string>();
            int scannedLines = 0;
            foreach (string file in files)
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string t = lines[i].TrimStart();
                    // 주석은 건너뛴다 — UiChrome의 제거 노트가 옛 이름을 <b>일부러</b> 인용하고 있고,
                    // 그 기록이 있어야 다음 사람이 "왜 안 나오지"로 헤매지 않는다.
                    if (t.StartsWith("//") || t.StartsWith("*") || t.StartsWith("/*")) continue;
                    scannedLines++;
                    foreach (string needle in needles)
                    {
                        if (t.IndexOf(needle, System.StringComparison.Ordinal) >= 0)
                            hits.Add($"{Path.GetFileName(file)}:{i + 1}: {t.Trim()}");
                    }
                }
            }

            Assert.Greater(scannedLines, 5000,
                $"{LogPrefix} 검사한 코드 줄이 {scannedLines}줄뿐입니다 — 주석 판정이 과하게 먹었을 수 있습니다.");
            Assert.IsEmpty(hits, $"{LogPrefix} 제품 코드에 그림자 생성 호출이 남아 있습니다:\n{string.Join("\n", hits)}");
            Debug.Log($"{LogPrefix} 제품 소스 {files.Count}파일 / {scannedLines}줄에서 그림자 호출 0건.");
        }

        // ============================================================================
        // ③ 씬 트리 — 큰 창(캐릭터창/설정창/팝오버/포스트잇이 전부 이 한 함수를 쓴다)
        // ============================================================================

        [Test]
        public void 불투명_패널은_본체와_보더_두_겹뿐이다()
        {
            var host = new GameObject("OpaquePanelProbe", typeof(RectTransform));
            try
            {
                RectTransform panel = UiChrome.AddOpaquePanel(host.transform, "Panel",
                    UiChrome.RadiusPanel, out Image body);

                Assert.AreEqual(2, panel.childCount,
                    $"{LogPrefix} 패널 자식이 {panel.childCount}개입니다(기대 2 = 본체 + 보더). " +
                    "겹이 늘었다면 그림자가 다시 들어온 것은 아닌지 보세요.");
                Assert.AreEqual(1f, body.color.a, 0.0001f, $"{LogPrefix} 본체가 α=1이 아닙니다.");
                Assert.IsEmpty(ShadowLikePieces(panel),
                    $"{LogPrefix} 캐릭터창/설정창/팝오버의 바탕에 그림자로 보이는 겹이 있습니다: " +
                    $"{string.Join(", ", ShadowLikePieces(panel))} — 사용자 지시는 '다 없애줘 깔끔하게'입니다.");
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void 유리_패널도_그림자_없이_세_겹뿐이다()
        {
            var host = new GameObject("GlassPanelProbe", typeof(RectTransform));
            try
            {
                RectTransform panel = UiChrome.AddGlassPanel(host.transform, "Glass", UiChrome.RadiusPanel, out _);

                Assert.AreEqual(3, panel.childCount,
                    $"{LogPrefix} 유리 패널 자식이 {panel.childCount}개입니다(기대 3 = 본체 + 보더 + 하이라이트).");
                Assert.IsEmpty(ShadowLikePieces(panel), $"{LogPrefix} 유리 패널에 그림자로 보이는 겹이 있습니다.");
            }
            finally { Object.DestroyImmediate(host); }
        }

        // ============================================================================
        // ④ 부채꼴 원버튼 — 직전 신고("나머지 3메뉴가 이상한 그림자로 남겨있음")의 현장
        // ============================================================================

        /// <summary>
        /// 그 신고의 원인은 <b>그림자가 접힘(알파 페이드) 대상에서 빠져 있던 것</b>이었다. 조립부가
        /// <c>AddSoftShadowCircle</c>의 반환값을 받지 않아 <c>ButtonView</c>에 참조가 없었고, 스케일은
        /// 0.72까지만 줄어서 알파 1.0짜리 검은 원 셋이 그대로 남았다. 그림자를 없앴으므로 남을 것도 없다.
        /// </summary>
        [Test]
        public void 부채꼴_원버튼에는_그림자_겹이_없다()
        {
            var host = new GameObject("GearButtonProbe");
            var spawned = new List<GameObject>();
            try
            {
                var widget = host.AddComponent<GearRadialMenuWidget>();
                MethodInfo build = typeof(GearRadialMenuWidget)
                    .GetMethod("BuildButton", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(build, $"{LogPrefix} BuildButton을 못 찾았습니다 — 이 테스트가 허위 통과합니다.");

                for (int i = 0; i < GearRadialMenuWidget.ButtonCount; i++)
                {
                    object view = build.Invoke(widget, new object[] { i });
                    var group = (RectTransform)view.GetType().GetField("Group").GetValue(view);
                    var root = (RectTransform)view.GetType().GetField("Root").GetValue(view);
                    spawned.Add(group.gameObject);

                    // 이름과 무관한 <b>개수</b> 고정: [Surface, Border, Flash, Symbol] (+ 오늘 할일은 Badge).
                    int expected = i == (int)GearMenuButton.Todo ? 5 : 4;
                    Assert.AreEqual(expected, root.childCount,
                        $"{LogPrefix} 버튼 {i}의 원 자식이 {root.childCount}개입니다(기대 {expected}). " +
                        "겹이 하나 늘었다면 그림자가 다시 들어왔는지 먼저 보세요.");

                    List<string> shadowLike = ShadowLikePieces(group);
                    Assert.IsEmpty(shadowLike,
                        $"{LogPrefix} 버튼 {i}에 그림자로 보이는 겹이 있습니다: {string.Join(", ", shadowLike)}");
                }
            }
            finally
            {
                foreach (GameObject go in spawned) if (go != null) Object.DestroyImmediate(go);
                FieldInfo canvas = typeof(GearRadialMenuWidget)
                    .GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic);
                var w = host.GetComponent<GearRadialMenuWidget>();
                if (canvas != null && w != null) canvas.SetValue(w, null);   // OnDestroy가 에디트 모드에서 Destroy를 부르지 않게
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// ★ 잔상 신고의 <b>재발 방지</b> — 그림자를 없애 원인은 사라졌지만, 같은 결함은 "접힘 알파
        /// 목록에 새 조각을 빠뜨린다"로 언제든 되돌아온다. 그래서 <b>이름을 세지 않고</b> 버튼 서브트리의
        /// 보이는 모든 <see cref="Graphic"/>을 훑어 "알파 0이면 전부 0"을 요구한다.
        /// <para>★ 뒤쪽 절반이 <b>같은 판정</b>으로 거짓 초록을 막는다: 조각 하나를 도로 불투명하게
        /// 되돌리면 이 검사가 실제로 잡아야 한다.</para>
        /// </summary>
        [Test]
        public void 접힌_버튼은_보이는_조각을_하나도_남기지_않는다()
        {
            var host = new GameObject("GearFadeProbe");
            var spawned = new List<GameObject>();
            try
            {
                var widget = host.AddComponent<GearRadialMenuWidget>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                MethodInfo build = typeof(GearRadialMenuWidget).GetMethod("BuildButton", flags);
                MethodInfo apply = typeof(GearRadialMenuWidget).GetMethod("ApplyButtonStyle", flags);
                Assert.NotNull(build, $"{LogPrefix} BuildButton을 못 찾았습니다.");
                Assert.NotNull(apply, $"{LogPrefix} ApplyButtonStyle을 못 찾았습니다 — 검사가 허위 통과합니다.");

                Image firstPiece = null;
                for (int i = 0; i < GearRadialMenuWidget.ButtonCount; i++)
                {
                    object view = build.Invoke(widget, new object[] { i });
                    var group = (RectTransform)view.GetType().GetField("Group").GetValue(view);
                    spawned.Add(group.gameObject);

                    // 실제로 켜질 수 있는 가지까지 재현한다(미완료 배지 / 잔여 시간 호).
                    var badge = (RectTransform)view.GetType().GetField("Badge").GetValue(view);
                    if (badge != null) badge.gameObject.SetActive(true);
                    var ringFill = (Image)view.GetType().GetField("RingFill").GetValue(view);
                    if (ringFill != null) ringFill.gameObject.SetActive(true);

                    apply.Invoke(widget, new object[] { view, i, 0f });

                    var leftovers = new List<string>();
                    int counted = 0;
                    foreach (Graphic g in group.GetComponentsInChildren<Graphic>(true))
                    {
                        if (!g.gameObject.activeInHierarchy) continue;   // 꺼진 조각은 그려지지 않는다
                        counted++;
                        if (firstPiece == null) firstPiece = g as Image;
                        if (g.color.a > 0.002f) leftovers.Add($"{g.name}(α={g.color.a:F3})");
                    }

                    Assert.GreaterOrEqual(counted, 5,
                        $"{LogPrefix} 버튼 {i}에서 센 조각이 {counted}개뿐입니다 — 그물이 비었습니다.");
                    Assert.IsEmpty(leftovers,
                        $"{LogPrefix} 버튼 {i}가 완전히 접혔는데 남은 잉크: {string.Join(", ", leftovers)} — " +
                        "사용자 신고 '나머지 3메뉴가 이상한 그림자로 남겨있음'이 그대로 재발합니다.");
                }

                // ★ 네거티브 컨트롤 — 조각 하나를 페이드에서 빠뜨린 상태를 만들면 위 판정이 잡아야 한다.
                Assert.NotNull(firstPiece, $"{LogPrefix} 네거티브 컨트롤 대상 조각을 못 잡았습니다.");
                firstPiece.color = new Color(firstPiece.color.r, firstPiece.color.g, firstPiece.color.b, 1f);
                bool caught = false;
                foreach (Graphic g in spawned[0].GetComponentsInChildren<Graphic>(true))
                {
                    if (g.gameObject.activeInHierarchy && g.color.a > 0.002f) caught = true;
                }
                Assert.IsTrue(caught,
                    $"{LogPrefix} 페이드에서 빠진 조각을 재현했는데도 검출되지 않았습니다 — 위 초록은 " +
                    "아무것도 증명하지 못합니다.");
            }
            finally
            {
                foreach (GameObject go in spawned) if (go != null) Object.DestroyImmediate(go);
                FieldInfo canvas = typeof(GearRadialMenuWidget)
                    .GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic);
                var w = host.GetComponent<GearRadialMenuWidget>();
                if (canvas != null && w != null) canvas.SetValue(w, null);
                Object.DestroyImmediate(host);
            }
        }

        // ============================================================================
        // ⑤ 네거티브 컨트롤 — ③④의 그물이 진짜로 무는지 증명한다
        // ============================================================================

        [Test]
        public void 검은_반투명_겹을_넣으면_같은_검사가_빨개진다()
        {
            var host = new GameObject("NegativeControlProbe", typeof(RectTransform));
            try
            {
                RectTransform panel = UiChrome.AddOpaquePanel(host.transform, "Panel",
                    UiChrome.RadiusPanel, out _);
                Assert.IsEmpty(ShadowLikePieces(panel), $"{LogPrefix} 주입 전인데 이미 검출됐습니다.");

                // 삭제된 그림자와 <b>같은 색</b>(검정 α0.55)을 이름만 바꿔 심는다.
                var fake = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
                fake.transform.SetParent(panel, false);
                fake.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

                List<string> hits = ShadowLikePieces(panel);
                Assert.AreEqual(1, hits.Count,
                    $"{LogPrefix} 이름을 바꾼 그림자를 검출하지 못했습니다(검출 {hits.Count}건) — " +
                    "③④의 초록은 아무것도 증명하지 못합니다.");
                StringAssert.StartsWith("Backdrop", hits[0], $"{LogPrefix} 엉뚱한 겹을 잡았습니다.");
                Assert.AreEqual(3, panel.childCount, $"{LogPrefix} 개수 검사도 함께 깨져야 합니다.");
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
