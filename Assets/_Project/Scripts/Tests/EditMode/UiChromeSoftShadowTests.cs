using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-01 — 사용자 신고 <b>"설정창이 두 겹으로 보인다 / 원 메뉴도 겹쳐 보인다"</b>의 회귀 잠금.
    ///
    /// <para><b>무엇이 문제였나</b>: 옛 <c>UiChrome.AddShadowLayer</c>는 <see cref="UiChrome.RoundedFill"/>
    /// (모서리만 둥근 <b>균일 알파 채움</b>)을 그림자로 썼다. 그래서 화면에 나온 것은 그림자가 아니라
    /// "패널보다 <c>spread</c>만큼 큰 반투명 판"이었고, 그게 2겹이라 창 아래에 딱딱한 사각형이 둘 더
    /// 생겼다. 이 앱은 <b>투명 오버레이</b>라 그림자가 드리울 바닥이 없어서 그 판이 유저의 데스크톱
    /// 위에 그대로 합성된다 — 불투명 UI였다면 "좀 진한 그림자"로 넘어갔을 것이 여기서는
    /// <b>두 번째 창</b>으로 읽힌다.</para>
    ///
    /// <para><b>왜 EditMode 텍셀 검사인가</b>: "두 겹으로 보인다"는 최종적으로 사람 눈의 판정이지만,
    /// 그 판정을 만드는 물리량은 <b>알파가 가장자리에서 절벽인가 램프인가</b> 하나다. 스프라이트는
    /// 순수 함수로 구워지므로 씬 없이 텍셀을 직접 읽어 잠글 수 있다(이 저장소가 이미 쓰는 오프라인
    /// 래스터 검산과 같은 관례). PlayMode 스크린샷 비교보다 훨씬 빠르고 훨씬 덜 흔들린다.</para>
    ///
    /// <para>★ CLAUDE.md: <b>프로덕션 상수를 숫자로 베끼지 않는다</b> — 번짐 상한/클램프/부채꼴 기하는
    /// 전부 해당 상수를 참조해 검증한다.</para>
    /// </summary>
    public sealed class UiChromeSoftShadowTests
    {
        /// <summary>대표 검사값 — 큰 창(설정/정보창)이 실제로 쓰는 반지름과 번짐대.</summary>
        private const int Radius = UiChrome.RadiusPanel;
        private const int Feather = 22;

        private static Color32[] Texels(Sprite sprite, out int size)
        {
            var tex = sprite.texture;
            size = tex.width;
            Assert.AreEqual(tex.width, tex.height, "그림자 스프라이트가 정사각이 아닙니다.");
            return tex.GetPixels32();
        }

        // ============================================================================
        // (1) 가장자리 알파가 실제로 0으로 떨어진다 — 이 라운드의 본론
        // ============================================================================

        [Test]
        public void 그림자_스프라이트의_가장_바깥_알파는_정확히_0이다()
        {
            Sprite s = UiChrome.SoftShadowFill(Radius, Feather);
            Color32[] px = Texels(s, out int n);

            int worst = 0;
            for (int i = 0; i < n; i++)
            {
                worst = Mathf.Max(worst, px[i].a);                       // 아래 변
                worst = Mathf.Max(worst, px[(n - 1) * n + i].a);         // 위 변
                worst = Mathf.Max(worst, px[i * n].a);                   // 왼 변
                worst = Mathf.Max(worst, px[i * n + (n - 1)].a);         // 오른 변
            }

            Assert.AreEqual(0, worst,
                $"그림자 스프라이트({s.name})의 테두리 텍셀 알파가 {worst}/255입니다. 0이 아니면 " +
                "그 값이 9-슬라이스로 늘어나 화면 끝단에 '판의 가장자리'로 남습니다 — 사용자가 신고한 " +
                "'창이 두 겹으로 보인다'가 바로 그 선입니다.");

            Assert.AreEqual(255, px[(n / 2) * n + (n / 2)].a,
                "그림자 한가운데가 불투명하지 않습니다 — 램프만 있고 코어가 없으면 그림자가 통째로 옅어집니다.");
        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — 옛 스프라이트(<see cref="UiChrome.RoundedFill"/>)를 <b>같은 잣대로</b>
        /// 재면 실제로 "절벽"이 잡힌다. 이게 없으면 위 단언이 무엇이든 통과하는 잣대일 수 있다.
        ///
        /// <para><b>어디를 재는가</b>: 균일 알파 채움은 텍스처 안쪽이 전부 255라 <b>텍스처 안에는 절벽이
        /// 없다</b> — 절벽은 스프라이트가 <b>끝나는 지점</b>에 있다(255에서 그리지 않음으로 한 번에
        /// 떨어진다). 그래서 재야 하는 값은 "가로 중앙선의 가장 바깥 텍셀 알파"와 "그 값이 255에
        /// 도달하기까지의 텍셀 수(= 램프 폭)"다. 옛것은 (255, 0텍셀), 새것은 (0, feather텍셀)이다.</para>
        /// </summary>
        [Test]
        public void 옛_균일채움은_끝이_잘려_있고_새_그림자는_램프로_사라진다()
        {
            Sprite old = UiChrome.RoundedFill(Radius + Feather);
            Sprite soft = UiChrome.SoftShadowFill(Radius, Feather);

            int oldEdge = EdgeAlphaOnCenterRow(old, out int oldRamp);
            int newEdge = EdgeAlphaOnCenterRow(soft, out int newRamp);

            Debug.Log($"[그림자] 가로 중앙선 — 옛 균일채움: 끝 알파 {oldEdge}/255, 램프 {oldRamp}텍셀 / " +
                $"새 감쇠: 끝 알파 {newEdge}/255, 램프 {newRamp}텍셀(설계 {Feather}).");

            Assert.AreEqual(255, oldEdge,
                "네거티브 컨트롤이 통과해 버렸습니다 — 옛 RoundedFill의 끝이 255가 아니면 이 측정 방식 " +
                "자체를 먼저 의심해야 합니다.");
            Assert.AreEqual(0, oldRamp, "옛 RoundedFill에 램프가 있다고 나옵니다 — 측정이 잘못됐습니다.");

            Assert.AreEqual(0, newEdge, "새 그림자의 끝이 0이 아닙니다.");
            // 바이트 반올림 때문에 마지막 1텍셀은 255로 올라붙을 수 있다 — 그 오차만 허용한다.
            Assert.GreaterOrEqual(newRamp, Feather - 1,
                $"램프가 {newRamp}텍셀뿐입니다 — 설계 폭({Feather}) 미만이면 그만큼 경계가 다시 날카로워집니다.");
        }

        /// <summary>가로 중앙선의 <b>가장 바깥</b> 텍셀 알파를 돌려주고, 거기서 안쪽으로 몇 텍셀 만에
        /// 255에 도달하는지(= 램프 폭)를 함께 낸다.</summary>
        private static int EdgeAlphaOnCenterRow(Sprite sprite, out int rampTexels)
        {
            Color32[] px = Texels(sprite, out int n);
            int row = n / 2;
            rampTexels = 0;
            while (rampTexels < n / 2 && px[row * n + rampTexels].a < 255) rampTexels++;
            return px[row * n].a;
        }

        [Test]
        public void 알파는_안에서_밖으로_단조감소한다()
        {
            Color32[] px = Texels(UiChrome.SoftShadowFill(Radius, Feather), out int n);
            int row = n / 2;
            for (int x = 1; x <= n / 2; x++)
            {
                Assert.GreaterOrEqual(px[row * n + x].a, px[row * n + x - 1].a,
                    $"가로 {x}번째 텍셀에서 알파가 안쪽으로 가는데 오히려 낮아졌습니다 — 램프가 뒤집혔거나 " +
                    "코어 인셋 계산이 틀렸습니다.");
            }
        }

        // ============================================================================
        // (2) 9-슬라이스로 늘려도 램프 폭이 유지된다
        // ============================================================================
        //
        // 이 파일(Capsule / VerticalGradientFill)이 이미 두 번 다룬 함정이다: 가운데가 늘어나면
        // 램프까지 함께 늘어나 "폭 약속"이 깨진다. 보더 안에 램프 전체가 들어가면 그 일이 없다.

        [Test]
        public void 램프_전체가_늘어나지_않는_9슬라이스_보더_안에_있다()
        {
            Sprite s = UiChrome.SoftShadowFill(Radius, Feather);
            Vector4 b = s.border;

            Assert.AreEqual(b.x, b.y, 0.001f, "보더가 4방향 대칭이 아닙니다.");
            Assert.AreEqual(b.x, b.z, 0.001f, "보더가 4방향 대칭이 아닙니다.");
            Assert.AreEqual(b.x, b.w, 0.001f, "보더가 4방향 대칭이 아닙니다.");

            Assert.GreaterOrEqual(b.x, (float)(Feather + Radius),
                $"보더가 {b.x}텍셀뿐입니다 — 램프({Feather}) + 코너({Radius})보다 좁으면 늘어나는 " +
                "가운데 슬라이스에 램프가 걸쳐 폭이 크기마다 달라집니다.");

            // 늘어나는 가운데는 알파 1이어야 한다(늘려도 왜곡될 것이 없다는 뜻).
            Color32[] px = Texels(s, out int n);
            int inner = Mathf.CeilToInt(b.x);
            for (int y = inner; y < n - inner; y++)
                for (int x = inner; x < n - inner; x++)
                    Assert.AreEqual(255, px[y * n + x].a,
                        $"늘어나는 가운데 슬라이스({x},{y})의 알파가 255가 아닙니다.");
        }

        // ============================================================================
        // (3) 캐시 키가 유계다 — 상한 없는 캐시는 이 앱에 실제 사고 이력이 있다
        // ============================================================================

        [Test]
        public void 캐시_키는_클램프되어_유계다()
        {
            // 상한을 넘겨 부르면 상한값과 <b>같은 인스턴스</b>가 나와야 한다(= 새 텍스처를 굽지 않는다).
            Sprite atMax = UiChrome.SoftShadowFill(UiChrome.MaxShadowRadius, UiChrome.MaxShadowFeatherPoints);
            Sprite overMax = UiChrome.SoftShadowFill(UiChrome.MaxShadowRadius + 4096,
                UiChrome.MaxShadowFeatherPoints + 4096);
            Assert.AreSame(atMax, overMax,
                "번짐/반지름 상한을 넘겨 불렀더니 새 스프라이트가 구워졌습니다 — 키가 유계가 아닙니다. " +
                "24시간 상주 앱에서 키가 무한하면 텍스처가 계속 쌓입니다.");

            Sprite atMin = UiChrome.SoftShadowFill(0, 1);
            Sprite underMin = UiChrome.SoftShadowFill(-9999, -9999);
            Assert.AreSame(atMin, underMin, "하한 쪽 클램프가 없습니다.");

            // 같은 인자를 다시 부르면 캐시 적중(= 매번 굽지 않는다).
            Assert.AreSame(UiChrome.SoftShadowFill(Radius, Feather), UiChrome.SoftShadowFill(Radius, Feather),
                "같은 인자인데 스프라이트가 새로 구워졌습니다 — 캐시가 동작하지 않습니다.");
        }

        [Test]
        public void 원형_그림자도_가장자리_알파가_0이고_캐시가_유계다()
        {
            const float diameter = GearRadialMenuWidget.ButtonDiameterPoints;
            const float feather = GearRadialMenuWidget.ButtonShadowFeatherPoints;

            var parent = new GameObject("SoftCircleProbe", typeof(RectTransform));
            try
            {
                Image img = UiChrome.AddSoftShadowCircle(parent.transform, "Shadow", diameter, feather,
                    UiChrome.PanelShadow);

                Assert.AreEqual(diameter + feather * 2f, img.rectTransform.sizeDelta.x, 0.001f,
                    "원형 그림자 사각형이 '코어 + 양쪽 램프'가 아닙니다 — 램프가 잘리거나 코어가 커집니다.");

                Color32[] px = Texels(img.sprite, out int n);
                int mid = n / 2;
                Assert.AreEqual(0, px[mid * n].a,
                    "원형 그림자의 가로 끝 텍셀 알파가 0이 아닙니다 — 딱딱한 테가 남아 이웃 버튼과의 " +
                    "틈을 먹습니다(사용자 신고 '원 메뉴도 겹쳐 보임').");
                Assert.AreEqual(0, px[mid].a, "원형 그림자의 세로 끝 텍셀 알파가 0이 아닙니다.");
                Assert.AreEqual(255, px[mid * n + mid].a, "원형 그림자의 코어가 불투명하지 않습니다.");

                // 극단값을 넣어도 키가 클램프된다.
                Image tiny = UiChrome.AddSoftShadowCircle(parent.transform, "Tiny", 1f, 9999f, UiChrome.PanelShadow);
                Image huge = UiChrome.AddSoftShadowCircle(parent.transform, "Huge", 1f, 99999f, UiChrome.PanelShadow);
                Assert.AreSame(tiny.sprite, huge.sprite, "원형 그림자 캐시 키가 유계가 아닙니다.");
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        // ============================================================================
        // (4) AddShadow가 실제로 감쇠 스프라이트를 쓰고, 2겹 관계가 유지된다
        // ============================================================================

        [Test]
        public void 두_겹_모두_감쇠_스프라이트를_쓰고_앰비언트가_더_넓고_옅다()
        {
            const float spread = 22f;
            var offset = new Vector2(0f, -7f);

            var container = new GameObject("ShadowProbe", typeof(RectTransform));
            try
            {
                Image key = UiChrome.AddShadow(container.transform, "Shadow", Radius, spread, offset);

                Assert.AreEqual(2, container.transform.childCount,
                    "그림자가 2겹이 아닙니다 — 앰비언트가 사라지면 어두운 바탕에서 '떠 있음'이 통째로 없어집니다.");

                var ambient = container.transform.GetChild(0).GetComponent<Image>();
                Assert.AreSame(key.gameObject, container.transform.GetChild(1).gameObject,
                    "앰비언트가 키보다 뒤(위)에 있습니다 — uGUI 형제 순서상 넓고 옅은 겹이 먼저 깔려야 합니다.");

                Assert.AreSame(UiChrome.SoftShadowFill(Radius, Mathf.RoundToInt(spread)), key.sprite,
                    "키 그림자가 감쇠 스프라이트를 쓰지 않습니다 — 균일 알파 채움으로 되돌아갔습니다.");
                Assert.AreEqual(0, EdgeAlphaOnCenterRow(ambient.sprite, out _),
                    "앰비언트의 끝 알파가 0이 아닙니다 — 옛 균일 알파 채움으로 되돌아갔습니다. " +
                    "사용자 스크린샷에서 '아래로 크게 삐져나온 판'이 바로 이 겹이었습니다.");

                Assert.Less(ambient.color.a, key.color.a,
                    "앰비언트가 키보다 진합니다 — 넓은 겹이 진하면 그게 곧 '두 번째 판'입니다.");

                float keyReach = -key.rectTransform.offsetMin.x;
                float ambientReach = -ambient.rectTransform.offsetMin.x;
                Assert.Greater(ambientReach, keyReach, "앰비언트가 키보다 넓게 퍼지지 않습니다.");

                // 코어가 실루엣 밖으로 드러나지 않는다 — 그것이 이번 회귀의 두 번째 원인이었다.
                Assert.Less(Mathf.Abs(offset.y), spread * UiChrome.MaxShadowOffsetToSpreadRatio,
                    "검사에 쓴 오프셋 자체가 경고 구간입니다 — 이 테스트가 무엇을 재는지 다시 보세요.");
            }
            finally
            {
                Object.DestroyImmediate(container);
            }
        }

        /// <summary>
        /// 옛 값(<c>spread 18 / offset -18</c>)을 다시 넣으면 <b>경고가 뜬다</b>. 알파 램프를 아무리 잘
        /// 구워도 오프셋이 번짐만큼 크면 알파 1짜리 코어가 실루엣 밖으로 통째로 드러난다.
        /// </summary>
        [Test]
        public void 오프셋이_번짐에_육박하면_경고한다()
        {
            var container = new GameObject("ShadowOffsetProbe", typeof(RectTransform));
            try
            {
                LogAssert.Expect(LogType.Warning,
                    new System.Text.RegularExpressions.Regex("코어\\(알파 1\\)가 본체 실루엣"));
                UiChrome.AddShadow(container.transform, "TooFar", Radius, 18f, new Vector2(0f, -18f));

                // 양성 대조 — 지금 쓰는 값에서는 아무 소리도 나지 않아야 한다(오탐 방지).
                UiChrome.AddShadow(container.transform, "Fine", Radius, 22f, new Vector2(0f, -7f));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(container);
            }
        }

        // ============================================================================
        // (5) 부채꼴 — 그림자가 이웃 버튼과의 틈을 먹지 않는다
        // ============================================================================

        [Test]
        public void 원버튼_그림자는_이웃과의_틈_안에_들어간다()
        {
            var gear = new Vector2(1000f, 500f);
            float spacing = Vector2.Distance(
                GearRadialMenuWidget.SlotCenterPoints(gear, 225f, 0),
                GearRadialMenuWidget.SlotCenterPoints(gear, 225f, 1));

            float footprint = GearRadialMenuWidget.ButtonDiameterPoints
                + GearRadialMenuWidget.ButtonShadowFeatherPoints * 2f;

            Debug.Log($"[부채꼴] 이웃 중심 거리 {spacing:F1}pt, 그림자 포함 지름 {footprint:F1}pt, " +
                $"남는 틈 {spacing - footprint:F1}pt.");

            Assert.Greater(spacing, footprint,
                $"그림자를 포함한 버튼 지름({footprint:F1}pt)이 이웃 중심 거리({spacing:F1}pt) 이상입니다 — " +
                "두 버튼의 그림자가 맞닿아 '붙어 보인다'가 됩니다(사용자 신고). 번짐을 줄이거나 " +
                "GearRadialMenuWidget.ButtonAngleStepDegrees/OrbitRadiusPoints를 키워야 합니다.");

            // 그림자가 시각 여백의 <b>절반</b>을 넘게 먹으면, 남는 틈보다 램프가 넓어져 두 버튼이
            // "이어진 회색 띠"로 읽힌다. 옛 하드 링(사방 2pt)이 이미 그 경계에 있었다.
            float visualGap = spacing - GearRadialMenuWidget.ButtonDiameterPoints;
            Assert.LessOrEqual(GearRadialMenuWidget.ButtonShadowFeatherPoints * 2f, visualGap * 0.5f,
                $"그림자 램프 합({GearRadialMenuWidget.ButtonShadowFeatherPoints * 2f:F1}pt)이 시각 여백 " +
                $"{visualGap:F1}pt의 절반을 넘습니다 — 틈이 램프로 메워져 버튼들이 붙어 보입니다.");
        }
    }
}
