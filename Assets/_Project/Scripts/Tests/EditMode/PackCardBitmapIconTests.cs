using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Interaction;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <b>팩 카드 비트맵 아이콘</b>(2026-09-08) — 이 저장소 최초의 래스터 자산이 카드에 도달하는가,
    /// 그리고 <b>나머지 전부가 그대로인가</b>.
    ///
    /// ============================================================================
    /// ★★ 이 파일의 무게중심은 <b>네거티브 컨트롤</b>이다
    /// ============================================================================
    /// 새 갈래를 하나 넣었다(<c>CardSprite != null</c>이면 비트맵). 이 갈래가 <b>넓게 새면</b>
    /// 출하 42종의 카드 그림이 통째로 바뀌는데, 증상은 «카드가 좀 달라 보인다»뿐이라
    /// 아무도 신고하지 않는다. 그래서 여기서 두 방향을 <b>같은 파일에서</b> 잠근다:
    /// <list type="number">
    ///   <item><b>팩 12종은 비트맵을 탄다</b>(존재 단언).</item>
    ///   <item><b>선언 = 폴더의 PNG</b>(양방향 단언) — 그리고 «비트맵이 없는 아이템»이 0종이 아님을
    ///     같은 실행 안에서 증명한다. CLAUDE.md: 부재 단언은 대조가 있어야 뜻을 갖는다.
    ///     <br/>★ <b>2026-09-09에 형태가 바뀌었다.</b> 옛 판은 «팩이 아니면 비트맵이 없다»였는데,
    ///     왕관 파일럿(출하 42종인데 카드 비트맵을 갖는다)이 그 문장을 <b>참이 아니게</b> 만들었다.
    ///     참이 아닌 단언을 남겨 두면 다음 사람이 그것을 지우면서 <b>대조까지</b> 지운다.</item>
    ///   <item><b>착용 비트맵을 선언했으면 카드도 선언한다</b> — 이 라운드의 신설.
    ///     사용자가 R2·R3·R4에서 반려한 «장비창이랑 착용이 다르다»의 필요조건을 형태로 잠근다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// 숫자·문자열을 베끼지 않는다
    /// ============================================================================
    /// <b>12</b>도 <b>72</b>도 <b>256</b>도 기대값으로 적지 않는다.
    ///  · 개수는 <see cref="PackRegistry"/>가 «팩 소속」이라고 답한 것을 <b>센다</b>.
    ///  · 크기는 <see cref="CharacterInfoWindow.BitmapIconSizeForTests"/>에서 <b>읽는다</b>.
    ///  · 임포트 값은 <see cref="TextureImporter"/>에게 <b>묻는다</b>.
    ///  · 그림 경로는 <see cref="CharacterInfoWindow.BuildCardArtForTests"/>로 <b>실제 함수를 부른다</b>
    ///    (리플렉션 문자열 니들 0개 — 이름이 바뀌면 컴파일이 깨진다).
    ///
    /// <para><b>이 파일이 검증하지 못하는 것</b>: 눈으로 본 «이중 테두리 없음»과 실제 픽셀이다.
    /// 그건 실기 캡처의 몫이고, 여기서 잠그는 것은 <b>배선과 값</b>이다.</para>
    /// </summary>
    public sealed class PackCardBitmapIconTests
    {
        private const string LogPrefix = "[팩카드-TEST]";
        private const string IconFolder = "Assets/_Project/Resources/Items/Icons";

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp() => EquipmentDebugUnlock.SetTestOverride(false);

        [TearDown]
        public void TearDown()
        {
            EquipmentDebugUnlock.SetTestOverride(false);
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            }
            _spawned.Clear();
        }

        private RectTransform NewRoot()
        {
            var go = new GameObject("CardArtRoot", typeof(RectTransform));
            _spawned.Add(go);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(CharacterInfoWindow.VectorIconSizeForTests,
                CharacterInfoWindow.VectorIconSizeForTests);
            return rt;
        }

        /// <summary>카탈로그에 실재하는 <b>팩 소속</b> 장비 전량. 화면에게 묻지 않는다 —
        /// 화면이 틀리면 기대값도 함께 틀어진다(<c>DlcShowcaseTests</c>와 같은 규약).</summary>
        private static List<ItemCatalogEntry> PackItems()
        {
            var list = new List<ItemCatalogEntry>();
            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                ItemCatalogEntry e = ItemCatalog.At(i);
                if (e == null || e.Category != ItemCategory.Equipment) continue;
                if (!e.Slot.HasValue || e.ItemIndex < 0) continue;
                if (!ItemCatalog.IsListed(e)) continue;
                if (!PackRegistry.TryFindPackOfItem(e, out _)) continue;
                list.Add(e);
            }
            return list;
        }

        /// <summary>팩이 <b>아닌</b> 장비 전량(= 출하 42종 쪽).</summary>
        private static List<ItemCatalogEntry> NonPackItems()
        {
            var list = new List<ItemCatalogEntry>();
            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                ItemCatalogEntry e = ItemCatalog.At(i);
                if (e == null || e.Category != ItemCategory.Equipment) continue;
                if (!e.Slot.HasValue || e.ItemIndex < 0) continue;
                if (PackRegistry.TryFindPackOfItem(e, out _)) continue;
                list.Add(e);
            }
            return list;
        }

        /// <summary>카드 비트맵을 <b>실제로 선언한</b> 장비 전량.
        ///
        /// <para>★ 2026-09-09 — 이 파일이 처음 쓰였을 때는 「팩 = 비트맵」이 참이라 코호트를
        /// <see cref="PackRegistry"/>로 골랐다. 왕관 파일럿이 그 전제를 깼다(팩이 아닌 출하
        /// 아이템도 비트맵을 갖는다). 그래서 코호트를 <b>이름이 아니라 사실</b>로 고른다 —
        /// 아래 단언들은 「팩이라서」가 아니라 「비트맵을 선언해서」 걸리는 것이 맞다.</para></summary>
        private static List<ItemCatalogEntry> CardBitmapItems()
        {
            var list = new List<ItemCatalogEntry>();
            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                ItemCatalogEntry e = ItemCatalog.At(i);
                if (e == null || e.Category != ItemCategory.Equipment) continue;
                if (!e.Slot.HasValue || e.ItemIndex < 0) continue;
                if (ItemCatalog.CardSprite(e.Slot.Value, e.ItemIndex) == null) continue;
                list.Add(e);
            }
            return list;
        }

        /// <summary><b>몸</b>에 래스터가 붙는 장비 전량(착용 비트맵 선언). 앞층만 본다 —
        /// 뒤층만 있는 자리는 <c>WornBitmapAttachmentTests</c>가 이미 결함으로 잡는다.</summary>
        private static List<ItemCatalogEntry> WornBitmapItems()
        {
            var list = new List<ItemCatalogEntry>();
            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                ItemCatalogEntry e = ItemCatalog.At(i);
                if (e == null || e.Category != ItemCategory.Equipment) continue;
                if (!e.Slot.HasValue || e.ItemIndex < 0) continue;
                if (ItemCatalog.WornSprite(e.Slot.Value, e.ItemIndex) == null) continue;
                list.Add(e);
            }
            return list;
        }

        /// <summary>아이콘 폴더에 실제로 있는 PNG 이름(확장자 제거). <b>디스크가 말하는 것</b>이다.</summary>
        private static HashSet<string> IconPngNames()
        {
            var names = new HashSet<string>(System.StringComparer.Ordinal);
            if (!Directory.Exists(IconFolder)) return names;
            string[] files = Directory.GetFiles(IconFolder, "*.png", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++) names.Add(Path.GetFileNameWithoutExtension(files[i]));
            return names;
        }

        // ============================================================================
        // 1. 배선 — 팩 전량이 비트맵을 선언했는가
        // ============================================================================

        [Test]
        public void 팩_아이템_전량이_카드_비트맵을_선언한다()
        {
            List<ItemCatalogEntry> packs = PackItems();
            Assert.Greater(packs.Count, 0,
                $"{LogPrefix} 팩 아이템이 0종입니다 — 이 파일의 모든 단언이 공허해집니다(빈 목록 순회).");

            var missing = new List<string>();
            for (int i = 0; i < packs.Count; i++)
            {
                ItemCatalogEntry e = packs[i];
                if (ItemCatalog.CardSprite(e.Slot.Value, e.ItemIndex) == null) missing.Add(e.Id);
            }

            Assert.IsEmpty(missing,
                $"{LogPrefix} 팩 {packs.Count}종 중 {missing.Count}종이 cardIconOverride 를 비웠습니다: " +
                $"{string.Join(", ", missing)}. 그 카드는 <b>조용히</b> 옛 벡터 아이콘으로 돌아가고, " +
                "증상은 «이 아이템만 그림이 다르다»뿐이라 신고되지 않습니다. " +
                "StickMate/팩 카드 아이콘/1 을 다시 돌리십시오.");
        }

        /// <summary>
        /// ★★ <b>2026-09-09에 형태가 바뀐 부재 단언이다.</b>
        ///
        /// <para>옛 판은 «팩이 아니면 카드 비트맵이 <b>없다</b>»였고, 그것은 왕관 파일럿이 착지하는
        /// 순간 <b>참이 아니게</b> 된다(왕관은 출하 42종인데 카드 비트맵을 갖는다). 참이 아닌 단언을
        /// 그대로 두면 다음 사람이 그것을 <b>지우면서 대조까지 함께</b> 지운다 —
        /// <c>WornBitmapAttachmentTests</c>가 P0→P1에서 똑같은 자리를 지나갔고, 그 파일이 남긴
        /// 처방(«0 을 지우지 말고 «파일과 정확히 일치»로 바꿔라»)을 여기에도 그대로 적용한다.</para>
        ///
        /// <para>새 불변식은 <b>양방향</b>이다 — <c>cardIconOverride</c>가 채워져 있다 ⟺
        /// 아이콘 폴더에 같은 이름의 PNG가 있다. 이쪽이 옛 판보다 <b>더 많이</b> 잡는다:</para>
        /// <list type="number">
        ///   <item><b>새는 쪽</b> — PNG도 없는데 비트맵을 물었다(엉뚱한 그림이 카드에 뜬다).</item>
        ///   <item><b>빠진 쪽</b> — PNG를 넣었는데 배선을 안 했다(그림을 넣었는데 화면이 그대로다,
        ///     그 증상은 «아직 안 넣었다»와 구분이 안 된다).</item>
        /// </list>
        /// </summary>
        [Test]
        public void 카드_비트맵_선언이_아이콘_폴더의_PNG와_정확히_일치한다()
        {
            HashSet<string> pngs = IconPngNames();
            Assert.Greater(pngs.Count, 0,
                $"{LogPrefix} ★대조 실패 — {IconFolder} 에 PNG가 0장입니다. " +
                "폴더가 옮겨졌다면 아래 목록은 «비어 있다»와 «못 봤다»를 구분하지 못합니다.");

            var missing = new List<string>();   // PNG는 있는데 배선이 없다
            var leaked = new List<string>();    // 배선은 있는데 PNG가 없다
            var plain = new List<string>();     // 비트맵도 PNG도 없는 옛 벡터 아이템
            int scanned = 0;
            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                ItemCatalogEntry e = ItemCatalog.At(i);
                if (e == null || e.Category != ItemCategory.Equipment) continue;
                if (!e.Slot.HasValue || e.ItemIndex < 0) continue;
                scanned++;

                string defPath = DefAssetPathOf(e);
                if (string.IsNullOrEmpty(defPath)) continue;   // 카탈로그에만 있는 자리(짝짓기 판정 불가)
                bool hasPng = pngs.Contains(Path.GetFileNameWithoutExtension(defPath));
                bool wired = ItemCatalog.CardSprite(e.Slot.Value, e.ItemIndex) != null;

                if (hasPng && !wired) missing.Add(e.Id);
                else if (!hasPng && wired) leaked.Add(e.Id);
                else if (!hasPng) plain.Add(e.Id);
            }

            Assert.Greater(scanned, 0, $"{LogPrefix} 장비를 한 종도 못 셌습니다 — 순회가 공허합니다.");

            // ★ 대조 둘. 이 두 줄이 없으면 위 «0건»들이 「아무도 안 탄다」와 구분되지 않는다.
            List<ItemCatalogEntry> declared = CardBitmapItems();
            Assert.Greater(declared.Count, 0,
                $"{LogPrefix} 카드 비트맵을 선언한 아이템이 0종입니다 — " +
                "CardSprite 배선이 통째로 죽어도 아래 «누출 0건»은 아무것도 증명하지 않습니다.");
            Assert.Greater(plain.Count, 0,
                $"{LogPrefix} 비트맵이 <b>없는</b> 아이템이 0종입니다 — 갈래가 전량으로 새면 " +
                "«누락 0건»이 그 사고를 통과시킵니다(출하 대다수는 아직 벡터여야 합니다).");

            Assert.IsEmpty(missing,
                $"{LogPrefix} PNG는 있는데 cardIconOverride 가 빈 아이템 {missing.Count}종: " +
                $"{string.Join(", ", missing)}. 그 카드는 <b>조용히</b> 옛 벡터로 남고, 증상은 " +
                "«그림을 넣었는데 아무것도 안 바뀐다»뿐입니다. StickMate/팩 카드 아이콘/1 을 다시 돌리십시오.");
            Assert.IsEmpty(leaked,
                $"{LogPrefix} PNG가 없는데 비트맵을 물고 있는 아이템 {leaked.Count}종: " +
                $"{string.Join(", ", leaked)}. 유령 참조이거나, 남의 그림을 쓰고 있습니다.");
        }

        /// <summary>★ 2026-09-09 — 코호트가 «팩»에서 «카드 비트맵을 선언한 전량»으로 넓어졌다.
        /// 왕관은 팩이 아니므로 옛 코호트로는 <b>짝짓기 검사를 한 번도 받지 않았을</b> 것이다.</summary>
        [Test]
        public void 비트맵은_같은_이름의_PNG에서_오고_서로_겹치지_않는다()
        {
            List<ItemCatalogEntry> packs = CardBitmapItems();
            Assert.Greater(packs.Count, 0, $"{LogPrefix} 카드 비트맵을 선언한 아이템이 0종 — 순회가 공허합니다.");

            var seen = new Dictionary<string, string>();
            var faults = new List<string>();
            for (int i = 0; i < packs.Count; i++)
            {
                ItemCatalogEntry e = packs[i];
                Sprite sprite = ItemCatalog.CardSprite(e.Slot.Value, e.ItemIndex);
                if (sprite == null) { faults.Add($"{e.Id}: 비트맵 없음"); continue; }

                string spritePath = AssetDatabase.GetAssetPath(sprite);
                // 아이템 에셋 파일명 = PNG 파일명. 이 등식이 이 라운드의 <b>유일한</b> 짝짓기 규칙이고
                // 도구(PackCardIconImport)도 같은 규칙을 쓴다. 규칙을 여기 다시 «적는» 것이 아니라
                // 실제 두 경로를 비교한다 — 짝이 어긋나면 «모자 카드에 망토 그림»이 되는데 화면상으로는
                // 그냥 «그런 아이템»으로 보인다.
                // ★ 에셋 파일명은 <b>아이디에서 파생되지 않는다</b>(팩은 pack_&lt;팩&gt;_&lt;슬롯&gt;_&lt;이름&gt;.asset 이고
                //   아이디는 equip.head.… 다). 그래서 파일명을 지어내지 않고 <b>실물을 찾아</b> 읽는다.
                string defPath = DefAssetPathOf(e);
                if (string.IsNullOrEmpty(defPath))
                {
                    faults.Add($"{e.Id}: 대응하는 .asset 을 찾지 못했습니다(짝짓기 판정 불가).");
                    continue;
                }
                string expected = $"{IconFolder}/{Path.GetFileNameWithoutExtension(defPath)}.png";
                if (!string.Equals(spritePath, expected, System.StringComparison.Ordinal))
                {
                    faults.Add($"{e.Id}: 스프라이트={spritePath} 기대={expected}");
                }

                if (seen.TryGetValue(spritePath, out string owner))
                {
                    faults.Add($"{e.Id}: 같은 PNG를 '{owner}'와 함께 씁니다({spritePath}).");
                }
                else
                {
                    seen[spritePath] = e.Id;
                }
            }

            Assert.IsEmpty(faults, $"{LogPrefix} 짝짓기 결함 {faults.Count}건\n  " + string.Join("\n  ", faults));
            Assert.AreEqual(packs.Count, seen.Count,
                $"{LogPrefix} 서로 다른 PNG가 {seen.Count}장인데 팩은 {packs.Count}종입니다 — 한 장이 여러 칸에 쓰였습니다.");
        }

        /// <summary>이 항목을 실어 온 <c>.asset</c>의 경로. <b>파일명을 지어내지 않는다</b> —
        /// 팩 에셋의 파일명은 아이디와 규칙이 다르고(<c>pack_cyber_head_patched_hood.asset</c> ↔
        /// <c>equip.head.patchedhood</c>), 지어낸 이름으로 <c>Resources.Load</c>를 하면 <b>null</b>이
        /// 돌아오는데 그 null 은 「짝이 틀렸다」와 출력이 똑같이 생겼다.</summary>
        private static string DefAssetPathOf(ItemCatalogEntry e)
        {
            AccessoryDefSO[] defs = Resources.LoadAll<AccessoryDefSO>("Items");
            for (int i = 0; i < defs.Length; i++)
            {
                AccessoryDefSO d = defs[i];
                if (d != null && d.slot == e.Slot.Value && d.itemIndex == e.ItemIndex
                    && string.Equals(d.itemId, e.Id, System.StringComparison.Ordinal))
                {
                    return AssetDatabase.GetAssetPath(d);
                }
            }
            return null;
        }

        // ============================================================================
        // 2. 임포트 설정 — Sprite / Bilinear / 무압축 / 플랫폼 오버라이드 없음
        // ============================================================================

        [Test]
        public void 임포트_설정이_카드용으로_못박혀_있다()
        {
            List<ItemCatalogEntry> packs = CardBitmapItems();
            Assert.Greater(packs.Count, 0, $"{LogPrefix} 카드 비트맵을 선언한 아이템이 0종 — 순회가 공허합니다.");

            var faults = new List<string>();
            for (int i = 0; i < packs.Count; i++)
            {
                Sprite sprite = ItemCatalog.CardSprite(packs[i].Slot.Value, packs[i].ItemIndex);
                if (sprite == null) { faults.Add($"{packs[i].Id}: 비트맵 없음"); continue; }

                string path = AssetDatabase.GetAssetPath(sprite);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) { faults.Add($"{path}: TextureImporter 아님"); continue; }

                if (ti.textureType != TextureImporterType.Sprite)
                    faults.Add($"{path}: textureType={ti.textureType} (Sprite 여야 Image.sprite 에 넣을 것이 생긴다)");
                // ★ 사용자 지시 — 그라데이션이라 Point 는 안 된다. 「Bilinear 다」와 「Point 가 아니다」를
                //   둘 다 적는다: 나중에 Trilinear 가 들어와도 앞 단언이 잡고, 열거형이 바뀌어도 뒤가 잡는다.
                if (ti.filterMode != FilterMode.Bilinear)
                    faults.Add($"{path}: filterMode={ti.filterMode} (Bilinear 여야 한다)");
                if (ti.filterMode == FilterMode.Point)
                    faults.Add($"{path}: filterMode 가 Point 다 — 그라데이션이 계단으로 깨진다.");
                if (ti.textureCompression != TextureImporterCompression.Uncompressed)
                    faults.Add($"{path}: 압축={ti.textureCompression} (무손실이어야 두 플랫폼이 같은 비트다)");
                if (ti.crunchedCompression)
                    faults.Add($"{path}: crunched 압축이 켜져 있다.");
                if (!ti.mipmapEnabled)
                    faults.Add($"{path}: 밉맵이 꺼져 있다 — ×1 DPI에서 3.5배 축소라 윤곽선이 끊긴다.");
                if (!ti.alphaIsTransparency)
                    faults.Add($"{path}: alphaIsTransparency 가 꺼져 있다 — 가장자리에 검은 테가 돈다.");

                // ★ 플랫폼 패리티 — Standalone 오버라이드가 <b>켜져 있으면</b> Windows 와 macOS 가
                //   서로 다른 포맷으로 굳고, 그 차이는 그 플랫폼에서 빌드해야만 보인다.
                TextureImporterPlatformSettings standalone = ti.GetPlatformTextureSettings("Standalone");
                if (standalone != null && standalone.overridden)
                {
                    faults.Add($"{path}: Standalone 플랫폼 오버라이드가 켜져 있다 " +
                               $"(maxTextureSize={standalone.maxTextureSize} 압축={standalone.textureCompression}).");
                }

                // 화면에 그려지는 최대 물리 픽셀보다 작으면 흐려진다. 기대값을 숫자로 적지 않고
                // <b>프로덕션 상수에서 파생</b>시킨다(카드 크기 × Retina 2배).
                int minimumEdge = Mathf.CeilToInt(CharacterInfoWindow.BitmapIconSizeForTests * 2f);
                int edge = ti.maxTextureSize;
                if (edge < minimumEdge)
                    faults.Add($"{path}: maxTextureSize={edge} < 화면 최대 물리픽셀 {minimumEdge} — Retina 에서 흐려진다.");
            }

            Assert.IsEmpty(faults, $"{LogPrefix} 임포트 결함 {faults.Count}건\n  " + string.Join("\n  ", faults));
        }

        // ============================================================================
        // 3. 그리기 경로 — 실제 함수를 부른다
        // ============================================================================

        [Test]
        public void 비트맵이_있으면_카드는_그_스프라이트_한_장을_그린다()
        {
            List<ItemCatalogEntry> packs = CardBitmapItems();
            Assert.Greater(packs.Count, 0, $"{LogPrefix} 카드 비트맵을 선언한 아이템이 0종 — 순회가 공허합니다.");

            float expectedEdge = CharacterInfoWindow.BitmapIconSizeForTests;
            var faults = new List<string>();
            for (int i = 0; i < packs.Count; i++)
            {
                ItemCatalogEntry e = packs[i];
                RectTransform root = NewRoot();
                CharacterInfoWindow.BuildCardArtForTests(root, e.Slot.Value, e.ItemIndex, e);

                Image[] images = root.GetComponentsInChildren<Image>(true);
                if (images.Length != 1)
                {
                    faults.Add($"{e.Id}: Image 가 {images.Length}개다(1이어야 한다 — 배경/테두리를 겹쳐 그리면 이중 테두리).");
                    continue;
                }

                Image img = images[0];
                Sprite expected = ItemCatalog.CardSprite(e.Slot.Value, e.ItemIndex);
                if (img.sprite != expected) faults.Add($"{e.Id}: 그려진 스프라이트가 카탈로그 값과 다르다.");
                if (!img.preserveAspect) faults.Add($"{e.Id}: preserveAspect 가 꺼져 있다 — 정사각이 아닌 그림이 늘어난다.");
                if (img.color != Color.white)
                    faults.Add($"{e.Id}: 기본색이 흰색이 아니다({img.color}) — 잠김/해금 복원이 원본색을 잃는다.");
                if (img.raycastTarget) faults.Add($"{e.Id}: raycastTarget 이 켜져 있다 — 썸네일 가운데가 클릭을 먹는다.");

                Vector2 size = img.rectTransform.sizeDelta;
                if (!Mathf.Approximately(size.x, expectedEdge) || !Mathf.Approximately(size.y, expectedEdge))
                    faults.Add($"{e.Id}: 크기 {size} != {expectedEdge}정사각.");
            }

            Assert.IsEmpty(faults, $"{LogPrefix} 그리기 결함 {faults.Count}건\n  " + string.Join("\n  ", faults));
        }

        /// <summary>★ 2026-09-09 — 옛 판은 «팩이 아니면 벡터»였다. 왕관이 그 전제를 깼으므로
        /// 코호트를 <b>비트맵을 선언하지 않은 아이템</b>으로 좁힌다. 좁히기만 하면 «건너뛰기»가
        /// 조용히 늘어나 아무것도 안 재게 되므로, <b>건너뛴 쪽을 같은 테스트에서 양성으로</b>
        /// 되받는다(선언한 아이템은 실제로 그 비트맵 한 장을 그려야 한다).</summary>
        [Test]
        public void 비트맵이_없으면_카드는_옛_벡터_경로를_그대로_탄다()
        {
            var others = new List<ItemCatalogEntry>();
            List<ItemCatalogEntry> all = NonPackItems();
            for (int i = 0; i < all.Count; i++)
            {
                if (ItemCatalog.CardSprite(all[i].Slot.Value, all[i].ItemIndex) == null) others.Add(all[i]);
            }
            Assert.Greater(others.Count, 0,
                $"{LogPrefix} 비트맵을 선언하지 않은 아이템이 0종 — 순회가 공허합니다(전량이 비트맵이 됐습니까?).");

            // 비트맵 코호트가 실제로 «한 장»을 그린다는 사실을 대조군으로 먼저 세운다. 그래야 아래의
            // «두 장 이상» 판정이 「비트맵 경로가 죽어서 전부 벡터가 됐다」와 구분된다.
            List<ItemCatalogEntry> packs = CardBitmapItems();
            Assert.Greater(packs.Count, 0, $"{LogPrefix} 대조군(비트맵 선언)이 0종입니다.");
            for (int i = 0; i < packs.Count; i++)
            {
                RectTransform packRoot = NewRoot();
                CharacterInfoWindow.BuildCardArtForTests(packRoot, packs[i].Slot.Value, packs[i].ItemIndex, packs[i]);
                Assert.AreEqual(1, packRoot.GetComponentsInChildren<Image>(true).Length,
                    $"{LogPrefix} 대조군 '{packs[i].Id}' 가 비트맵 한 장을 그리지 않았습니다 — 아래 판정이 뜻을 잃습니다.");
            }

            var faults = new List<string>();
            for (int i = 0; i < others.Count; i++)
            {
                ItemCatalogEntry e = others[i];
                if (!ItemCatalog.IsListed(e)) continue;   // 은퇴한 자리는 화면에 없다

                RectTransform root = NewRoot();
                CharacterInfoWindow.BuildCardArtForTests(root, e.Slot.Value, e.ItemIndex, e);
                Image[] images = root.GetComponentsInChildren<Image>(true);

                if (images.Length == 0)
                {
                    faults.Add($"{e.Id}: 아무것도 안 그렸다 — 벡터 폴백까지 죽었다.");
                    continue;
                }

                for (int g = 0; g < images.Length; g++)
                {
                    if (images[g] != null && images[g].preserveAspect && images[g].sprite != null
                        && AssetDatabase.GetAssetPath(images[g].sprite).StartsWith(IconFolder, System.StringComparison.Ordinal))
                    {
                        faults.Add($"{e.Id}: 카드 비트맵 폴더의 스프라이트를 그렸다(이 아이템은 선언한 적이 없다).");
                        break;
                    }
                }
            }

            Assert.IsEmpty(faults, $"{LogPrefix} 벡터 경로 회귀 {faults.Count}건\n  " + string.Join("\n  ", faults));
        }

        // ============================================================================
        // 3-b. ★ 2026-09-09 신설 — <b>한 아이템은 모든 표면에서 같은 화법이어야 한다</b>
        // ============================================================================

        /// <summary>
        /// ★★ <b>이 라운드가 닫으려는 결함을 그대로 단언으로 옮긴 것이다.</b>
        ///
        /// <para>사용자가 하루에 세 번(R2·R3·R4) 반려한 문장은 하나였다 —
        /// <i>«장비창이랑 착용이 다르다»</i>. 그리고 2026-09-09 왕관 파일럿이 착지한 직후,
        /// <b>방향만 반대인 같은 결함</b>이 다시 났다: 착용은 새 금색 비트맵인데 카드·상세는
        /// 옛 벡터 라인아트였다(리더 육안 확인, <c>live_crown_0.75.png</c>).</para>
        ///
        /// <para>그 결함은 «둘 중 하나만 채운다»는 <b>배선의 형태</b>에서 나온다. 그림이 예쁜지는
        /// 여기서 못 재지만 <b>둘 다 채웠는지</b>는 잴 수 있고, 그것이 이 결함의 필요조건이다.
        /// 몸에 래스터를 붙였으면 카드에도 래스터가 있어야 한다 — 아니면 그 아이템은
        /// <b>구조적으로</b> 표면마다 다른 화법이 된다.</para>
        ///
        /// <para><b>역방향은 단언하지 않는다</b>(카드 비트맵 ⇒ 착용 비트맵 아님). 팩 12종이
        /// 정확히 그 상태이고(카드는 래스터, 몸은 벡터 <c>wornShapes</c>), 그것은 2026-09-08
        /// 리더 판단이라 결함이 아니다. 방향을 하나만 잠그는 이유가 이것이다.</para>
        /// </summary>
        [Test]
        public void 착용_비트맵을_선언한_아이템은_카드_비트맵도_선언한다()
        {
            List<ItemCatalogEntry> worn = WornBitmapItems();
            Assert.Greater(worn.Count, 0,
                $"{LogPrefix} 착용 비트맵을 선언한 아이템이 0종입니다 — 아래 단언이 공허합니다. " +
                "(P1 파일럿이 통째로 사라졌거나 WornSprite 배선이 죽었습니다.)");

            var half = new List<string>();
            for (int i = 0; i < worn.Count; i++)
            {
                ItemCatalogEntry e = worn[i];
                if (ItemCatalog.CardSprite(e.Slot.Value, e.ItemIndex) == null) half.Add(e.Id);
            }

            Assert.IsEmpty(half,
                $"{LogPrefix} 몸에는 비트맵을 붙였는데 카드는 옛 벡터인 아이템 {half.Count}종: " +
                $"{string.Join(", ", half)}.\n" +
                "  ★ 이것이 사용자가 R2·R3·R4에서 반려한 «장비창이랑 착용이 다르다»의 형태입니다. " +
                "한 아이템이 표면마다 다른 화법으로 보입니다 — 카드 파생본을 굽고 " +
                "cardIconOverride 를 채우십시오(design/equipment/verify/bitmap_authoring_pipeline_r1.py).");
        }

        /// <summary>
        /// ★ <b>카드 파생본이 옛 벡터와 같은 배율·같은 자리에 있는가</b>(규격서 §7-2 이행 검산).
        ///
        /// <para>왜 이것이 필요한가: 카드 그림을 «적당한 여백»으로 구우면 왕관만 옆 카드의
        /// 벡터 모자들보다 크거나 작게, 혹은 위아래로 밀려 보인다. 그 증상은 «원래 그런 아이콘»으로
        /// 읽혀 <b>아무도 신고하지 않는다</b> — 이 파일이 <c>preserveAspect</c>에 적어 둔 것과 같은 병이다.</para>
        ///
        /// <para><b>기대값을 손으로 적지 않는다.</b> 두 개의 <b>서로 다른</b> 프로덕션 사실에서 유도해
        /// 세 번째 사실(PNG 픽셀)과 대조한다:</para>
        /// <list type="number">
        ///   <item><see cref="ItemCatalog.WornSpriteInkBoxInR"/> — <c>WornSpriteImport</c>가
        ///     <b>착용</b> PNG 알파에서 구워 넣은 잉크 박스(R 단위).</item>
        ///   <item><see cref="AccessoryCardIcon.Frame"/> + <see cref="CharacterInfoWindow"/>의 두 상자 —
        ///     옛 벡터 카드가 쓰던 pt/R 배율과 상자 중심.</item>
        ///   <item>그리고 <b>카드 PNG 파일 자체</b>를 읽어 실제 잉크 박스를 잰다.</item>
        /// </list>
        /// <para>셋이 어긋나면 실패한다. 굽는 스크립트가 상수를 베껴 갔으므로, 그 사본이 썩는 것을
        /// 막는 것은 주석이 아니라 이 대조다.</para>
        ///
        /// <para><b>배경이 불투명이어도 잰다</b> — 알파가 있으면 α로, 전면 불투명이면 모서리 색과의
        /// 차이로 잉크를 가른다. 배경 처리를 바꾸는 판단이 이 계기를 죽이지 않도록.</para>
        /// </summary>
        [Test]
        public void 카드_파생본이_옛_벡터_카드와_같은_배율_같은_자리에_있다()
        {
            List<ItemCatalogEntry> worn = WornBitmapItems();
            Assert.Greater(worn.Count, 0, $"{LogPrefix} 착용 비트맵 아이템이 0종 — 순회가 공허합니다.");

            var faults = new List<string>();
            int measured = 0;
            for (int i = 0; i < worn.Count; i++)
            {
                ItemCatalogEntry e = worn[i];
                Sprite card = ItemCatalog.CardSprite(e.Slot.Value, e.ItemIndex);
                if (card == null) continue;   // 앞 테스트가 이미 결함으로 잡는다

                Vector4 ink = ItemCatalog.WornSpriteInkBoxInR(e.Slot.Value, e.ItemIndex);
                if (!WornSpritePlacement.IsInkBoxBaked(ink))
                {
                    faults.Add($"{e.Id}: 착용 잉크 박스를 안 구웠습니다 — 기대값을 만들 수 없습니다.");
                    continue;
                }
                if (!AccessoryCardIcon.Frame.TryGetCardFrame(e.Slot.Value, out float unitsPerR,
                        out float centerYInR))
                {
                    faults.Add($"{e.Id}: 이 슬롯({e.Slot.Value})에는 카드 프레임이 없습니다.");
                    continue;
                }

                // 옛 벡터 카드의 pt/R (AccessoryCardIcon.BuildFramed 와 같은 식, shrink 는 아래에서)
                float pxPerR = unitsPerR * (CharacterInfoWindow.VectorIconSizeForTests
                                            / AccessoryCardIcon.Frame.IconViewBox);
                float spanR = CharacterInfoWindow.BitmapIconSizeForTests / pxPerR;
                float inkW = ink.z - ink.x, inkH = ink.w - ink.y;
                // BoxFit.Shrink 와 같은 처방 — 넘칠 때만 창을 넓혀(=배율을 낮춰) 담는다.
                spanR = Mathf.Max(spanR, Mathf.Max(inkW, inkH));

                float expMinX = ink.x / spanR + 0.5f;
                float expMaxX = ink.z / spanR + 0.5f;
                float expMinY = (ink.y - (centerYInR - spanR * 0.5f)) / spanR;
                float expMaxY = (ink.w - (centerYInR - spanR * 0.5f)) / spanR;

                string path = AssetDatabase.GetAssetPath(card);
                if (!TryMeasureInkFraction(path, out Vector4 got, out int edge))
                {
                    faults.Add($"{e.Id}: 카드 PNG({path})를 읽지 못했습니다.");
                    continue;
                }
                measured++;

                // 허용치는 <b>텍셀에서 유도한다</b>: 축소 리샘플의 가장자리 반투명 띠가 1텍셀 안팎이고
                // 잉크 판정이 절반 지점에서 갈리므로, 양쪽 3텍셀이면 넉넉하다. 숫자를 지어내지 않는다.
                float tol = 3f / edge;
                Check(faults, e.Id, "좌", got.x, expMinX, tol);
                Check(faults, e.Id, "우", got.z, expMaxX, tol);
                Check(faults, e.Id, "하", got.y, expMinY, tol);
                Check(faults, e.Id, "상", got.w, expMaxY, tol);
            }

            Assert.Greater(measured, 0,
                $"{LogPrefix} 한 장도 재지 못했습니다 — «결함 0건»이 「검사할 것이 없었다」와 같아집니다.");
            Assert.IsEmpty(faults, $"{LogPrefix} 카드 프레이밍 결함 {faults.Count}건\n  " + string.Join("\n  ", faults));
        }

        private static void Check(List<string> faults, string id, string edgeName, float got, float want, float tol)
        {
            if (Mathf.Abs(got - want) > tol)
            {
                faults.Add($"{id}: {edgeName}끝 프레임비 {got:F4} != 기대 {want:F4} (허용 ±{tol:F4}). " +
                           "카드의 왕관이 옆 벡터 카드들과 다른 크기·자리로 보입니다.");
            }
        }

        /// <summary>PNG 파일에서 잉크 박스를 <b>프레임 비율</b>로 잰다 — (minX, minY, maxX, maxY),
        /// y 는 <b>아래에서 위로</b>(Unity 텍스처 좌표계).
        /// <para>임포트된 스프라이트가 아니라 <b>파일</b>을 읽는다: 임포터의 축소·압축이 낀 값은
        /// «우리가 구운 그림»이 아니라 «Unity가 만든 그림»이고, 여기서 판정하려는 것은 전자다.</para></summary>
        private static bool TryMeasureInkFraction(string assetPath, out Vector4 frac, out int edge)
        {
            frac = default;
            edge = 0;
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath)) return false;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(assetPath))) return false;
                int w = tex.width, h = tex.height;
                edge = Mathf.Min(w, h);
                Color32[] px = tex.GetPixels32();

                // 알파가 실제로 쓰이는가(가장자리 한 화소로 판정하지 않는다 — 전량을 센다).
                int transparent = 0;
                for (int i = 0; i < px.Length; i++)
                {
                    if (px[i].a < 128) transparent++;
                }
                Color32 corner = px[0];

                int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        Color32 c = px[y * w + x];
                        bool isInk = transparent > 0
                            ? c.a >= 128
                            : Mathf.Abs(c.r - corner.r) + Mathf.Abs(c.g - corner.g)
                              + Mathf.Abs(c.b - corner.b) > 24;
                        if (!isInk) continue;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
                if (maxX < 0) return false;
                frac = new Vector4(minX / (float)w, minY / (float)h, (maxX + 1) / (float)w, (maxY + 1) / (float)h);
                return true;
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        // ============================================================================
        // 4. ★ 범위 잠금 — 몸에 붙는 그림은 한 점도 안 바뀐다
        // ============================================================================

        [Test]
        public void 팩의_착용_형상은_비트맵과_무관하게_그대로다()
        {
            List<ItemCatalogEntry> packs = PackItems();
            Assert.Greater(packs.Count, 0, $"{LogPrefix} 팩 아이템이 0종 — 순회가 공허합니다.");

            var faults = new List<string>();
            for (int i = 0; i < packs.Count; i++)
            {
                ItemCatalogEntry e = packs[i];
                AccessoryWornShapeData[] worn = ItemCatalog.WornShapes(e.Slot.Value, e.ItemIndex);
                if (worn == null || worn.Length == 0)
                {
                    faults.Add($"{e.Id}: wornShapes 가 비었다 — 몸에 아무것도 안 붙는다.");
                }
            }

            Assert.IsEmpty(faults,
                $"{LogPrefix} 착용 형상 {faults.Count}건이 사라졌습니다\n  " + string.Join("\n  ", faults) +
                "\n  ★ 카드 비트맵은 <b>카드 표면 전용</b>입니다. 몸은 여전히 벡터 wornShapes 하나이고, " +
                "그 선은 2026-09-08 리더 판단입니다.");
        }
    }
}
