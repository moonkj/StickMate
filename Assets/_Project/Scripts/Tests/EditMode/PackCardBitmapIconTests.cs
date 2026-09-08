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
    ///   <item><b>기본 코호트는 한 종도 안 탄다</b>(부재 단언) — 그리고 그 0이 «아무도 안 탄다»가
    ///     아님을 (1)이 같은 실행 안에서 증명한다. CLAUDE.md: 부재 단언은 대조가 있어야 뜻을 갖는다.</item>
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

        [Test]
        public void 기본_코호트는_카드_비트맵을_선언하지_않는다()
        {
            List<ItemCatalogEntry> others = NonPackItems();
            Assert.Greater(others.Count, 0, $"{LogPrefix} 비팩 아이템이 0종 — 순회가 공허합니다.");

            var leaked = new List<string>();
            for (int i = 0; i < others.Count; i++)
            {
                ItemCatalogEntry e = others[i];
                if (ItemCatalog.CardSprite(e.Slot.Value, e.ItemIndex) != null) leaked.Add(e.Id);
            }

            // ★ 대조 — 위 «0건»이 「아무도 안 탄다」가 아님을 <b>같은 테스트 안에서</b> 증명한다.
            //   이 줄이 없으면 CardSprite 가 통째로 null 을 돌려주게 되는 날 이 테스트가 조용히 초록이 된다.
            List<ItemCatalogEntry> packs = PackItems();
            Assert.Greater(packs.Count, 0, $"{LogPrefix} 대조군(팩)이 0종 — 부재 단언이 뜻을 잃습니다.");
            Assert.IsNotNull(ItemCatalog.CardSprite(packs[0].Slot.Value, packs[0].ItemIndex),
                $"{LogPrefix} 대조군 '{packs[0].Id}' 조차 비트맵이 null 입니다 — " +
                "CardSprite 배선이 통째로 죽었을 때 아래 «누출 0건»은 아무것도 증명하지 않습니다.");

            Assert.IsEmpty(leaked,
                $"{LogPrefix} 팩이 아닌 아이템 {leaked.Count}종이 비트맵을 물고 있습니다: " +
                $"{string.Join(", ", leaked)}. 출하 42종의 카드 그림이 바뀌는 것은 이 라운드의 범위가 아닙니다.");
        }

        [Test]
        public void 비트맵은_같은_이름의_PNG에서_오고_서로_겹치지_않는다()
        {
            List<ItemCatalogEntry> packs = PackItems();
            Assert.Greater(packs.Count, 0, $"{LogPrefix} 팩 아이템이 0종 — 순회가 공허합니다.");

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
            List<ItemCatalogEntry> packs = PackItems();
            Assert.Greater(packs.Count, 0, $"{LogPrefix} 팩 아이템이 0종 — 순회가 공허합니다.");

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
            List<ItemCatalogEntry> packs = PackItems();
            Assert.Greater(packs.Count, 0, $"{LogPrefix} 팩 아이템이 0종 — 순회가 공허합니다.");

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

        [Test]
        public void 비트맵이_없으면_카드는_옛_벡터_경로를_그대로_탄다()
        {
            List<ItemCatalogEntry> others = NonPackItems();
            Assert.Greater(others.Count, 0, $"{LogPrefix} 비팩 아이템이 0종 — 순회가 공허합니다.");

            // 팩이 실제로 «한 장»을 그린다는 사실을 대조군으로 먼저 세운다. 그래야 아래의
            // «두 장 이상» 판정이 「비트맵 경로가 죽어서 전부 벡터가 됐다」와 구분된다.
            List<ItemCatalogEntry> packs = PackItems();
            Assert.Greater(packs.Count, 0, $"{LogPrefix} 대조군(팩)이 0종입니다.");
            RectTransform packRoot = NewRoot();
            CharacterInfoWindow.BuildCardArtForTests(packRoot, packs[0].Slot.Value, packs[0].ItemIndex, packs[0]);
            Assert.AreEqual(1, packRoot.GetComponentsInChildren<Image>(true).Length,
                $"{LogPrefix} 대조군 '{packs[0].Id}' 가 비트맵 한 장을 그리지 않았습니다 — 아래 판정이 뜻을 잃습니다.");

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
                        faults.Add($"{e.Id}: 팩 전용 비트맵 폴더의 스프라이트를 그렸다.");
                        break;
                    }
                }
            }

            Assert.IsEmpty(faults, $"{LogPrefix} 벡터 경로 회귀 {faults.Count}건\n  " + string.Join("\n  ", faults));
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
