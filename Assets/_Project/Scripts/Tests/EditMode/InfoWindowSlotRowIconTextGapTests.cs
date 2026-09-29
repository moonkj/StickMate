using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <b>착용 슬롯 행에서 아이콘과 글자가 겹치지 않는가</b> — 2026-09-29 사용자 신고로 신설.
    ///
    /// ============================================================================
    /// 무엇이 실제로 깨져 있었나
    /// ============================================================================
    /// 신고 원문(빌드 <c>windows-preview-20260929b</c>): <i>"폰트가 훨씬 깔끔해졌어. 다만 이미지와
    /// 폰트가 겹치는 문제 있음"</i>. 캡처에서 겹친 것은 <b>[안경 · EYES] 행</b> 하나였다 —
    /// 선글라스 아이콘이 「선글라스」 글자에 붙어 잘려 보였고, 나머지 세 행은 「비어 있음」(아이콘 없음)
    /// 이라 증상이 보이지 않았다.
    ///
    /// <para><b>원인은 폰트가 아니다.</b> 아이콘 칸의 <see cref="RectTransform"/>이 pivot을
    /// <b>왼쪽 변</b>(0, 0.5)으로 두고도 <c>anchoredPosition.x</c>에 <b>중심</b> 공식
    /// (<c>12 + SlotIconSize * 0.5</c>)을 받아, 칸이 인계본 선언값 <c>[12, 36]</c>이 아니라
    /// <c>[24, 48]</c>에 앉아 있었다. 글자 칸은 처음부터 <b>46</b>에서 시작했으므로(인계본 DOM
    /// <c>padding 0 12</c> + <c>gap 10</c> + 아이콘 24) <b>겹침 2pt가 구조적으로 보장</b>돼 있었다.
    /// 커밋 <c>0229f52</c>부터 한 글자도 안 바뀐 자리다 — 즉 <b>폰트 교체(7481c72)가 만든 회귀가
    /// 아니라 드러낸 자리</b>다.</para>
    ///
    /// <para><b>그럼 폰트는 무엇을 바꿨나 — 좌표는 한 점도 안 바꿨다(실측).</b> 글자 칸은 왼쪽 정렬
    /// (<see cref="TextAnchor.MiddleLeft"/>) + <c>HorizontalWrapMode.Overflow</c>라 <b>글자 시작 x가
    /// 글자 폭에 좌우되지 않고</b>(그래서 「폭이 전부 좁아졌다」는 직전 라운드 실측은 이 자리를 옮기지
    /// 않았다), 첫 글자의 <b>좌측 여백</b>도 두 글꼴이 같았다 — 아래
    /// <see cref="글자_잉크의_좌측_여백이_아이콘_칸까지_넘어오지_않는다"/>가 같은 자로 둘을 재서
    /// <c>Apple SD Gothic Neo</c> −1.00pt · <c>LegacyRuntime.ttf</c> −1.00pt를 로그로 남긴다(분해능 1pt).
    /// ⇒ <b>겹침은 폰트 교체 전에도 같은 크기로 있었다.</b> 바뀐 것은 <b>보이는 정도</b>다: 직전까지
    /// 이 창의 굵은 글자는 합성 볼드로 번져 있었고(같은 사용자의 이전 신고 *"두꺼운 폰트들이 깔끔하게
    /// 안보이고 번져보임"*), 획이 선명해지자 같은 겹침이 이제 <b>겹침으로 읽힌다</b>.
    /// ★ 그래서 이 파일이 잠그는 것은 「폰트 회귀」가 아니라 <b>칸의 기하</b>다 — 폰트를 또 바꿔도
    /// 아래 두 자는 같은 값을 내야 한다.</para>
    ///
    /// <para><b>실측표</b>(2026-09-29, EditMode 배치모드. 「옛 배치」는 같은 그림을 12pt 오른쪽에 둔
    /// 출하 좌표다. 글자 잉크는 45.0에서 시작한다):
    /// <list type="bullet">
    ///   <item>아이콘 <b>칸</b>: 고침 뒤 [12.0, 36.0] · 옛 배치 [24.0, 48.0] ⇒ 글자 칸(46.0)을 <b>2.0pt</b> 덮었다.</item>
    ///   <item>실제 <b>그림</b>의 오른쪽 끝(옛 배치 환산): HEAD 49.1 · EYES 48.8 · NECK 46.2 ·
    ///     BACK 46.9 · HAIR 49.3 · FX 47.7 · PET 47.8 ⇒ 잉크(45.0)와 최대 <b>4.3pt</b> 겹쳤다.</item>
    ///   <item>고침 뒤 가장 오른쪽까지 가는 그림은 HAIR 「곱슬」 37.3이고 글자 칸까지 <b>8.71pt</b> 남는다.</item>
    /// </list></para>
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 세 가지
    /// ============================================================================
    /// <list type="number">
    /// <item><b>칸이 겹치지 않는다</b> — 아이콘 칸을 프로덕션 함수
    ///   (<see cref="CharacterInfoWindow.PlaceSlotIconBox"/>)로 실제로 앉혀 놓고 잰다. 상수 산술로는
    ///   이 실패가 안 잡힌다(pivot과 x의 <b>뜻</b>이 어긋난 것이라 <see cref="RectTransform"/>이
    ///   앉은 자리를 재야 보인다). <b>음성 대조</b>로 옛 배치를 손으로 재현해, 이 자가 그것을
    ///   실제로 빨갛게 만드는지 같은 테스트 안에서 증명한다.</item>
    /// <item><b>그림이 칸을 넘지 않는다</b> — 아이템 그림은 칸의 <b>중심</b>에 굽히므로, 도형 좌표가
    ///   넓어지면 칸 밖으로 자라 같은 증상이 돌아온다. 선글라스가 하필 안경 4종 중 상자를 가장 꽉
    ///   채우는 폭이라 그 행에서 먼저 보인 것이다. 그래서 <b>모든 카테고리 × 모든 아이템</b>을
    ///   프로덕션 갈래(<see cref="CharacterInfoWindow.BuildSlotIconArt"/>) 그대로 구워 폭을 잰다.</item>
    /// <item><b>폰트가 또 바뀌어도 안 깨진다</b> — 잉크의 좌측 여백이 홈(10pt)을 먹지 않는지 잰다.</item>
    /// </list>
    ///
    /// <para><b>한계(정직하게)</b>: 배치모드는 <c>-nographics</c>라 OS 페이스가 안 올라오면 내장
    /// 폰트로 폴백한다(<c>UiFontResolutionAuditTests</c>가 같은 한계를 적어 두었다). 그래서 세 번째
    /// 항목은 <b>확정 페이스 이름을 로그로 함께</b> 남긴다 — 로그가 내장 폰트를 말하면 그 실행은
    /// 출하 페이스를 잰 것이 아니다. 최종 판정은 실기 캡처다.</para>
    /// </summary>
    public sealed class InfoWindowSlotRowIconTextGapTests
    {
        private const string LogPrefix = "[슬롯행겹침-TEST]";

        /// <summary>잉크 여백을 재는 표본. <b>프로덕션 문안이 아니다</b> — 아이템 이름이 바뀌어도
        /// 이 자는 안 흔들려야 한다. 신고된 행의 첫 글자와 같은 계열(한글)이면 충분하다.</summary>
        private const string InkSample = "가선글빔";

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null) UnityEngine.Object.DestroyImmediate(_spawned[i]);
            }
            _spawned.Clear();
        }

        // ============================================================================
        // 자 — 행 왼쪽 변을 x=0으로 두고 재는 통로
        // ============================================================================

        /// <summary>착용 슬롯 행 하나. <see cref="UiChrome.PlaceTopLeft"/>로 앉히므로 pivot이
        /// 좌상단이고, 이 행의 로컬 x가 곧 <b>행 왼쪽 변에서의 거리</b>가 된다 — 프로덕션 상수
        /// (<c>SlotRowPadX</c>·<c>SlotTextX</c>)가 쓰는 좌표계와 같다.</summary>
        private RectTransform NewRow()
        {
            var host = new GameObject("슬롯행겹침측정", typeof(RectTransform));
            _spawned.Add(host);
            var rowGo = new GameObject("SlotRow", typeof(RectTransform));
            rowGo.transform.SetParent(host.transform, false);
            var row = rowGo.GetComponent<RectTransform>();
            UiChrome.PlaceTopLeft(row, 0f, 0f,
                CharacterInfoWindow.Col1ContentWidth, CharacterInfoWindow.SlotRowHeight);
            return row;
        }

        private static RectTransform NewIconBox(RectTransform row)
        {
            var go = new GameObject("SlotIcon", typeof(RectTransform));
            go.transform.SetParent(row, false);
            var irt = go.GetComponent<RectTransform>();
            CharacterInfoWindow.PlaceSlotIconBox(irt);
            return irt;
        }

        /// <summary><paramref name="target"/>이 <paramref name="reference"/>의 로컬 좌표계에서
        /// 차지하는 사각형. 회전(획 선분은 회전한 사각형이다)까지 포함하므로 모서리를 통째로 옮긴다.</summary>
        private static Rect LocalRectOf(RectTransform reference, RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 p = reference.InverseTransformPoint(corners[i]);
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        /// <summary><paramref name="artRoot"/> 밑에 실제로 그려진 것들의 <b>합집합</b> 사각형
        /// (<paramref name="reference"/> 로컬). <paramref name="drawn"/>은 센 조각 수 —
        /// 0이면 아무것도 안 재고 통과한 것이므로 부르는 쪽이 <b>양성 대조</b>로 쓴다.
        ///
        /// <para>★ 채움 면(<c>AccessoryFillGraphic</c>)은 <c>sizeDelta</c>가 0이라 사각형이 점 하나다 —
        /// 그래서 그 조각만 <b>정점 사각형</b>을 따로 물어본다(그 창구가 없으면 윤곽선 없는 채움
        /// 조각이 이 자에서 조용히 0으로 세어진다).</para></summary>
        private static Rect MeasuredArtRect(RectTransform reference, RectTransform artRoot, out int drawn)
        {
            drawn = 0;
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            Graphic[] graphics = artRoot.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic g = graphics[i];
                if (g == null) continue;
                var grt = g.rectTransform;
                Rect box = LocalRectOf(reference, grt);

                var fill = g as AccessoryFillGraphic;
                if (fill != null && fill.TryGetPolygonBounds(out Rect poly))
                {
                    // 정점은 조각의 로컬 좌표다 — 같은 변환을 거쳐야 행 좌표가 된다.
                    Vector3 lo = reference.InverseTransformPoint(grt.TransformPoint(new Vector3(poly.xMin, poly.yMin, 0f)));
                    Vector3 hi = reference.InverseTransformPoint(grt.TransformPoint(new Vector3(poly.xMax, poly.yMax, 0f)));
                    box = Rect.MinMaxRect(Mathf.Min(lo.x, hi.x), Mathf.Min(lo.y, hi.y),
                        Mathf.Max(lo.x, hi.x), Mathf.Max(lo.y, hi.y));
                }

                if (box.xMin < minX) minX = box.xMin;
                if (box.xMax > maxX) maxX = box.xMax;
                if (box.yMin < minY) minY = box.yMin;
                if (box.yMax > maxY) maxY = box.yMax;
                drawn++;
            }
            return drawn == 0 ? new Rect(0f, 0f, 0f, 0f) : Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        // ============================================================================
        // 1. 칸이 겹치지 않는가 (+ 음성 대조)
        // ============================================================================

        [Test]
        public void 아이콘_칸은_글자_칸의_왼쪽에서_끝난다()
        {
            RectTransform row = NewRow();
            RectTransform icon = NewIconBox(row);
            Rect box = LocalRectOf(row, icon);

            Assert.AreEqual(CharacterInfoWindow.SlotRowPadX, box.xMin, 0.01f,
                $"{LogPrefix} 아이콘 칸의 왼쪽 x가 행 안쪽 여백 {CharacterInfoWindow.SlotRowPadX}과 다릅니다 " +
                $"(실측 {box.xMin:F2}). 인계본 DOM은 행 패딩 0 12를 선언합니다(UI_SURFACE_SPEC §16.1-b).");
            Assert.AreEqual(CharacterInfoWindow.SlotIconSize, box.width, 0.01f,
                $"{LogPrefix} 아이콘 칸의 폭이 {CharacterInfoWindow.SlotIconSize}이 아닙니다(실측 {box.width:F2}).");

            float gap = CharacterInfoWindow.SlotTextX - box.xMax;
            Assert.GreaterOrEqual(gap, 0f,
                $"{LogPrefix} ★ 아이콘 칸이 글자 칸을 {-gap:F2}pt 덮습니다 — 사용자 신고 " +
                "「이미지와 폰트가 겹치는 문제」(2026-09-29)가 바로 이 형태입니다. " +
                $"칸 [{box.xMin:F1}, {box.xMax:F1}] · 글자 칸 시작 {CharacterInfoWindow.SlotTextX:F1}. " +
                "pivot이 왼쪽 변(0, 0.5)이므로 anchoredPosition.x는 칸의 중심이 아니라 왼쪽 x입니다.");
            Assert.AreEqual(CharacterInfoWindow.SlotIconTextGap, gap, 0.01f,
                $"{LogPrefix} 아이콘 칸과 글자 칸 사이 홈이 선언값 {CharacterInfoWindow.SlotIconTextGap}과 " +
                $"다릅니다(실측 {gap:F2}) — 인계본 DOM gap 10.");

            // ★ 음성 대조 — 이 자가 <b>옛 배치</b>를 실제로 빨갛게 만드는가. 이게 없으면 위 초록이
            //   "재긴 쟀는데 무엇도 거를 수 없는 자"일 수 있다(거짓 통과 4번: 모든 없음 판정에 대조).
            icon.anchoredPosition = new Vector2(
                CharacterInfoWindow.SlotRowPadX + CharacterInfoWindow.SlotIconSize * 0.5f, 0f);
            Rect regressed = LocalRectOf(row, icon);
            Assert.Greater(regressed.xMax, CharacterInfoWindow.SlotTextX,
                $"{LogPrefix} 음성 대조 실패 — 2026-09-29까지 출하돼 있던 배치(중심 공식)를 다시 앉혔는데도 " +
                $"이 자가 겹침을 말하지 못합니다(칸 [{regressed.xMin:F1}, {regressed.xMax:F1}]). " +
                "자가 죽었으므로 위의 초록도 무효입니다.");

            Debug.Log($"{LogPrefix} 아이콘 칸 [{box.xMin:F1}, {box.xMax:F1}] · 홈 {gap:F1} · " +
                      $"글자 칸 시작 {CharacterInfoWindow.SlotTextX:F1} / " +
                      $"옛 배치는 [{regressed.xMin:F1}, {regressed.xMax:F1}]로 글자 칸을 " +
                      $"{regressed.xMax - CharacterInfoWindow.SlotTextX:F1}pt 덮었다.");
        }

        // ============================================================================
        // 2. 그림이 칸을 넘지 않는가 — 모든 카테고리 × 모든 아이템
        // ============================================================================

        [Test]
        public void 착용_슬롯_아이콘_그림은_글자_칸을_넘지_않는다()
        {
            // 슬롯 행에 뜰 수 있는 카테고리 목록을 <b>베끼지 않는다</b> — enum 전체를 돌면 상위집합이고,
            // 새 카테고리가 행에 들어오는 날 자동으로 대상이 된다.
            var slots = (EquipmentSlot[])Enum.GetValues(typeof(EquipmentSlot));

            float worstRight = float.MinValue;
            string worstName = null;
            int measured = 0, empty = 0;
            var report = new StringBuilder();
            var perSlot = new StringBuilder();
            var table = new StringBuilder();

            for (int s = 0; s < slots.Length; s++)
            {
                EquipmentSlot slot = slots[s];
                int count = ItemCatalog.ItemCountIn(slot);
                float slotWorstRight = float.MinValue;
                string slotWorstName = null;
                for (int item = 0; item < count; item++)
                {
                    ItemCatalogEntry entry = ItemCatalog.Item(slot, item);
                    if (entry == null) continue;

                    RectTransform row = NewRow();
                    RectTransform icon = NewIconBox(row);
                    CharacterInfoWindow.BuildSlotIconArt(icon, slot, item, entry);
                    Rect art = MeasuredArtRect(row, icon, out int drawn);
                    if (drawn == 0)
                    {
                        empty++;
                        report.Append($"· {EquipmentModel.SlotCode(slot)}/{item} {entry.DisplayName}: 조각 0개\n");
                        continue;
                    }

                    measured++;
                    if (art.xMax > worstRight)
                    {
                        worstRight = art.xMax;
                        worstName = $"{EquipmentModel.SlotCode(slot)}/{item} {entry.DisplayName} " +
                                    $"[{art.xMin:F1}, {art.xMax:F1}] 조각 {drawn}개";
                    }
                    if (art.xMax > slotWorstRight)
                    {
                        slotWorstRight = art.xMax;
                        slotWorstName = $"{entry.DisplayName} [{art.xMin:F1}, {art.xMax:F1}]";
                    }
                    // 전수 표 — 신고가 <b>아이템 하나</b>에서 오면 그 한 줄을 다시 재지 않고 읽을 수
                    //   있어야 한다(2026-09-29 신고 대상은 EYES 「선글라스」였다).
                    table.Append($"{EquipmentModel.SlotCode(slot)}/{item} {entry.DisplayName} " +
                                 $"{art.xMax:F1}(옛 {art.xMax + CharacterInfoWindow.SlotIconSize * 0.5f:F1}) · ");
                }

                // 카테고리별 최댓값도 남긴다 — 신고는 <b>행 하나</b>(안경)에서 왔고, 어느 카테고리가
                // 칸을 꽉 채우는지가 다음 회귀의 위험 순위다.
                if (slotWorstName != null)
                {
                    perSlot.Append($"{EquipmentModel.SlotCode(slot)} {slotWorstName} " +
                                   $"(옛 배치라면 {slotWorstRight + CharacterInfoWindow.SlotIconSize * 0.5f:F1}) / ");
                }
            }

            // 양성 대조 — 무엇이라도 쟀는가. 0개를 재고 "안 넘었다"고 말하는 것이 이 저장소의 거짓 통과다.
            Assert.Greater(measured, 0,
                $"{LogPrefix} 아이템 그림을 한 장도 못 쟀습니다 — 이 실행의 폭 판정은 전부 무효입니다. " +
                $"(조각 0개로 끝난 아이템 {empty}개)\n{report}");
            Assert.AreEqual(0, empty,
                $"{LogPrefix} 그림이 한 조각도 안 나온 아이템이 {empty}개입니다 — 그 자리는 이 자에서 " +
                $"조용히 0pt로 세어지므로 감사에 구멍이 됩니다.\n{report}");

            Assert.LessOrEqual(worstRight, CharacterInfoWindow.SlotTextX,
                $"{LogPrefix} ★ 아이템 그림이 글자 칸(x ≥ {CharacterInfoWindow.SlotTextX:F1})까지 자랐습니다 — " +
                $"가장 넓은 자리 {worstName}. 칸은 [{CharacterInfoWindow.SlotRowPadX:F0}, " +
                $"{CharacterInfoWindow.SlotRowPadX + CharacterInfoWindow.SlotIconSize:F0}]이고 그림은 그 " +
                "중심에 굽습니다(도형 좌표가 넓어지면 칸 밖으로 자랍니다).");

            Debug.Log($"{LogPrefix} 아이템 {measured}종 측정 · 가장 오른쪽까지 간 그림 {worstName} · " +
                      $"글자 칸 시작 {CharacterInfoWindow.SlotTextX:F1} · " +
                      $"여유 {CharacterInfoWindow.SlotTextX - worstRight:F2}pt");
            Debug.Log($"{LogPrefix} 카테고리별 최대 오른쪽: {perSlot}");
            Debug.Log($"{LogPrefix} 전수 표(그림 오른쪽 끝, 괄호는 2026-09-29 이전 배치 환산): {table}");
        }

        // ============================================================================
        // 3. 폰트가 또 바뀌어도 안 깨지는가 — 잉크의 좌측 여백
        // ============================================================================

        /// <summary>
        /// ★★ <b>2026-09-29 정정 — 이 자의 모형은 이제 실제보다 1pt 비관적이다(단언 방향은 안전).</b>
        /// <para>같은 날 다른 신고(「상자 안 정렬이 안 되어 있다」)를 고치면서
        /// <see cref="UiChrome.AddText"/>가 <see cref="UnityEngine.UI.Text.alignByGeometry"/>를 켰다.
        /// 그러면 <b>왼쪽 정렬 라벨의 잉크가 상자 왼쪽 변에 딱 붙는다</b> — 즉 이 테스트가 더하는
        /// 좌측 베어링(출하 페이스에서 <c>−1.00pt</c>)만큼 <b>왼쪽으로 나가지 않는다</b>.
        /// 실제 잉크 시작은 <c>SlotTextX + 0</c>이고 이 자가 계산하는 값은 <c>SlotTextX + minX</c>다.</para>
        /// <para><b>그래도 고치지 않는다</b>: 단언은 「잉크가 아이콘 칸을 넘어오지 않는다」이고
        /// 이 모형은 잉크를 <b>더 왼쪽에</b> 두므로 <b>통과 조건이 더 엄격</b>하다(거짓 통과 방향이
        /// 아니다). 그리고 이 자의 값어치는 <b>페이스별 좌측 베어링을 숫자로 남기는 것</b>이라
        /// 정렬 방식과 무관하게 유효하다. 세로 중앙 정렬의 폰트 의존성은
        /// <c>UiCenteredTextInkAlignmentTests</c>가 따로 잠근다.</para>
        /// </summary>
        [Test]
        public void 글자_잉크의_좌측_여백이_아이콘_칸까지_넘어오지_않는다()
        {
            Font font = UiChrome.Font;
            Assert.IsNotNull(font, $"{LogPrefix} UiChrome.Font가 null입니다 — 잴 것이 없습니다.");

            float mostLeft = LeftmostInk(font, out char mostLeftGlyph, out int known);

            // 양성 대조: 글리프를 하나라도 알아냈는가(0이면 이 실행은 아무것도 재지 않았다).
            Assert.Greater(known, 0,
                $"{LogPrefix} 표본 {InkSample.Length}자 중 글리프 수치를 얻은 것이 0개입니다 — " +
                "이 실행의 잉크 여백 판정은 무효입니다(배치모드 -nographics에서 폰트가 안 올라온 경우).");

            float inkStart = CharacterInfoWindow.SlotTextX + mostLeft;
            float boxRight = CharacterInfoWindow.SlotRowPadX + CharacterInfoWindow.SlotIconSize;
            Assert.Greater(inkStart, boxRight,
                $"{LogPrefix} 글자 잉크가 x={inkStart:F2}에서 시작해 아이콘 칸의 오른쪽 끝 {boxRight:F1}을 " +
                $"넘어왔습니다(가장 왼쪽 글리프 '{mostLeftGlyph}' 여백 {mostLeft:F2}pt, 홈 " +
                $"{CharacterInfoWindow.SlotIconTextGap:F0}pt). 이 폰트에서는 홈을 넓혀야 합니다.");

            // ★ 「폰트 교체 전에는 왜 안 겹쳤나」를 숫자로 남긴다 — 옛 글꼴(내장 폰트)의 같은 표본을
            //   같은 자로 재서 나란히 찍는다. 단언하지 않는다(출하되는 것이 아니므로) — 이 줄의
            //   값어치는 「무엇이 달라졌는지」를 다음 사람이 다시 조사하지 않게 하는 것이다.
            Font builtin = Resources.GetBuiltinResource<Font>(UiChrome.BuiltinFontResource);
            string before = "(내장 폰트를 못 불렀다)";
            if (builtin != null)
            {
                float builtinLeft = LeftmostInk(builtin, out char builtinGlyph, out int builtinKnown);
                before = builtinKnown > 0
                    ? $"'{UiChrome.BuiltinFontResource}' 여백 {builtinLeft:F2}pt('{builtinGlyph}')"
                    : $"'{UiChrome.BuiltinFontResource}' 글리프 0자(이 모드에서 못 그린다)";
            }

            Debug.Log($"{LogPrefix} 확정 페이스='{UiChrome.ResolvedFontName}' · 표본 {known}/{InkSample.Length}자 · " +
                      $"가장 왼쪽 여백 {mostLeft:F2}pt · 잉크 시작 {inkStart:F2} · 아이콘 칸 끝 {boxRight:F1} · " +
                      $"남은 홈 {inkStart - boxRight:F2}pt · 옛 글꼴 대조 {before} " +
                      "(★ 확정 페이스가 내장 폰트면 이 실행은 출하 페이스를 잰 것이 아니다).");
        }

        /// <summary>표본에서 <b>가장 왼쪽으로 나온 잉크</b>의 x(글자 원점 기준, 음수면 원점보다 왼쪽).
        /// <paramref name="known"/>은 수치를 얻은 글자 수 — 0이면 아무것도 재지 못한 것이다.</summary>
        private static float LeftmostInk(Font font, out char glyph, out int known)
        {
            glyph = '\0';
            known = 0;
            float mostLeft = 0f;
            font.RequestCharactersInTexture(InkSample, UiChrome.FontBody, FontStyle.Normal);
            for (int i = 0; i < InkSample.Length; i++)
            {
                if (!font.GetCharacterInfo(InkSample[i], out CharacterInfo info, UiChrome.FontBody, FontStyle.Normal))
                {
                    continue;
                }
                known++;
                if (info.minX >= mostLeft) continue;
                mostLeft = info.minX;
                glyph = InkSample[i];
            }
            return mostLeft;
        }
    }
}
