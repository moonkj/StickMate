using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Interaction;
using UnityEditor;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ DLC 이행 A단계(하드코딩 28종 → <see cref="AccessoryDefSO"/> 에셋) <b>회귀 잠금</b>.
    /// 2026-09-01 카테고리당 +2종으로 <b>42종(7×6)</b>이 됐고, 골든도 그 라운드에서 의도적으로 갱신했다.
    /// 근거: docs/ARCHITECTURE.md 5-3-3 "A: … 값 동일하게 생성 / 회귀 잠금: 에셋과 하드코딩이 비트 동일".
    ///
    /// ============================================================================
    /// 무엇을 잡으려는가 — 이 마이그레이션의 고유 실패 유형
    /// ============================================================================
    /// 값 수천 개(아이콘 좌표 1,000여 개 + 색 채널 200여 개 + 문자열 84개)를 코드에서 에셋으로
    /// 옮기는 작업의 무서운 점은 <b>대부분의 사고가 조용하다</b>는 것이다. 왕관 좌표 한 점이
    /// 틀려도 컴파일되고, 기존 테스트도 전부 초록이다(아무도 좌표값 자체를 단언하지 않았다).
    /// 그래서 전환 <b>직전</b>의 카탈로그 전문을 골든 텍스트로 굳혀 두고 지금과 완전 대조한다.
    ///
    ///  (1) <b>골든 대조</b>: 전환 전/후 카탈로그가 한 글자도 다르지 않다.
    ///  (2) <b>표 모양</b>: 7카테고리 × 4자리, (카테고리, 자리) 중복 없음, 구멍 없음, 아이디 중복 없음.
    ///  (3) <b>에셋 ↔ 런타임 1:1</b>: 에셋 파일이 든 값과 카탈로그가 내주는 값이 필드 단위로 같다.
    ///      (1)이 이미 전체를 덮지만, 실패했을 때 <b>어느 아이템의 어느 필드</b>인지 바로 말해 준다.
    ///  (4) <b>hidesHair가 거짓말하지 않는다</b>: 새로 생긴 필드가 렌더러의 실제 동작
    ///      (<c>AccessoryShapeBuilder.HatCoverLocalY</c>)과 일치한다. A단계에서 이 필드를 읽는 코드는
    ///      아직 없지만, <b>지금 값을 못 박아 두지 않으면</b> 훗날 렌더러를 이 필드로 갈아탈 때
    ///      그 라운드가 "전환"이 아니라 "동작 변경"이 된다.
    ///  (5) <b>에셋 오염 금지</b>: 런타임이 들고 다니는 좌표 배열이 임포트된 에셋의 배열과 같은
    ///      인스턴스면, 누가 한 칸만 써도 에디터에서 .asset 파일이 조용히 더러워진다.
    ///  (6) <b>팩 소속·등급 선언</b>(2026-09-05 security S-13): <c>cohortId</c>/<c>declaredRarity</c>가
    ///      에셋 → 카탈로그 → 골든까지 같은 값으로 도착하는가. 이 셋 중 어디가 끊겨도
    ///      <b>기본 42종은 전부 기본값</b>이라 아무 초록도 갈라지지 않는다 — 팩이 와야 증상이 난다.
    ///
    /// ============================================================================
    /// 네거티브 컨트롤
    /// ============================================================================
    ///  · <c>골든_비교기가_진짜로_차이를_잡는다</c> — 비교기가 죽어도 초록이 뜨는 일을 막는다.
    ///  · 에셋 하나를 지우면 (2)가, 좌표 한 칸을 고치면 (1)과 (3)이 함께 빨개진다.
    /// </summary>
    public sealed class ItemCatalogAssetParityTests
    {
        private const string ItemFolder = "Assets/_Project/Resources/Items";
        // 28종(7×4) -> 2026-09-01 카테고리당 +2종으로 42종(7×6).
        private const int ExpectedItemsPerSlot = 6;
        private const int ExpectedEquipmentCount = ExpectedItemsPerSlot * EquipmentModel.SlotCount;   // 42

        /// <summary>왕관의 자리(모자 3번). <c>AccessoryShapeBuilder.HeadCrown</c>과 같은 값이며,
        /// 아래 hidesHair 테스트는 그 상수를 직접 읽어 이 주석이 뒤처지는 것까지 막는다.</summary>
        private const int CrownIndex = 3;

        private static AccessoryDefSO[] LoadDefs()
        {
            string[] guids = AssetDatabase.FindAssets("t:AccessoryDefSO", new[] { ItemFolder });
            var defs = new AccessoryDefSO[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                defs[i] = AssetDatabase.LoadAssetAtPath<AccessoryDefSO>(AssetDatabase.GUIDToAssetPath(guids[i]));
            }
            return defs;
        }

        // ==================== (1) 골든 대조 ====================

        [Test]
        public void 전환_전_골든_스냅샷과_지금_카탈로그가_한_글자도_다르지_않다()
        {
            string goldenPath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, ItemCatalogDigest.GoldenAssetPath);

            Assert.IsTrue(File.Exists(goldenPath),
                $"골든 스냅샷이 없습니다: {ItemCatalogDigest.GoldenAssetPath}\n" +
                "이 파일이 없으면 A단계 전환이 값을 보존했는지 증명할 방법이 사라집니다 " +
                "(메뉴: StickMate/DLC 이행 A/1).");

            string golden = File.ReadAllText(goldenPath).Replace("\r\n", "\n");
            string now = ItemCatalogDigest.Build().Replace("\r\n", "\n");

            Assert.Greater(golden.Length, 10000,
                "골든 스냅샷이 너무 짧습니다 — 빈 카탈로그를 골든이라고 굳혀 두면 이 테스트가 " +
                "아무것도 지키지 않게 됩니다.");

            string diff = ItemCatalogDigest.FirstDifference(golden, now);
            Assert.IsNull(diff,
                "에셋 전환으로 카탈로그 값이 바뀌었습니다(A단계는 값 보존이 전제입니다).\n" + diff);
        }

        [Test]
        public void 골든_비교기가_진짜로_차이를_잡는다()
        {
            // 양성 대조 — FirstDifference가 항상 null을 뱉게 되어도 위 테스트는 초록이다.
            Assert.IsNull(ItemCatalogDigest.FirstDifference("a\nb", "a\nb"));
            StringAssert.Contains("2번째 줄", ItemCatalogDigest.FirstDifference("a\nb", "a\nc"));
            StringAssert.Contains("줄 수가 다릅니다", ItemCatalogDigest.FirstDifference("a\nb", "a"));
        }

        // ==================== (2) 표 모양 ====================

        [Test]
        public void 아이템_에셋은_카테고리당_같은_개수이고_자리가_겹치지도_비지도_않는다()
        {
            AccessoryDefSO[] defs = LoadDefs();
            Assert.AreEqual(ExpectedEquipmentCount, defs.Length,
                $"{ItemFolder} 아래 아이템 에셋이 {defs.Length}개입니다({ExpectedEquipmentCount}종이어야 합니다). " +
                "하나라도 빠지면 그 자리가 보관함에서 빈 칸이 되고, 저장 파일이 그 아이디를 가리키면 " +
                "복원이 실패합니다.");

            var seenIds = new HashSet<string>();
            var occupied = new Dictionary<(EquipmentSlot, int), string>();

            foreach (AccessoryDefSO def in defs)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(def.itemId),
                    $"에셋 '{def.name}'에 itemId가 없습니다 — 저장 파일이 적을 값입니다.");
                Assert.IsTrue(seenIds.Add(def.itemId), $"itemId가 중복됩니다: {def.itemId}");

                Assert.That((int)def.slot, Is.InRange(0, EquipmentModel.SlotCount - 1),
                    $"'{def.itemId}'의 카테고리가 범위를 벗어납니다.");
                Assert.That(def.itemIndex, Is.InRange(0, ExpectedItemsPerSlot - 1),
                    $"'{def.itemId}'의 자리 번호가 0~{ExpectedItemsPerSlot - 1}이 아닙니다.");

                var key = (def.slot, def.itemIndex);
                Assert.IsFalse(occupied.ContainsKey(key),
                    $"{def.slot} {def.itemIndex}번 자리를 '{(occupied.TryGetValue(key, out string other) ? other : "?")}'와 " +
                    $"'{def.itemId}'가 함께 차지합니다 — 자리 번호는 AccessoryShapeBuilder가 그림을 고르는 값입니다.");
                occupied[key] = def.itemId;
            }

            for (int s = 0; s < EquipmentModel.SlotCount; s++)
            {
                for (int i = 0; i < ExpectedItemsPerSlot; i++)
                {
                    Assert.IsTrue(occupied.ContainsKey(((EquipmentSlot)s, i)),
                        $"{(EquipmentSlot)s} {i}번 자리의 에셋이 없습니다(표에 구멍).");
                }
            }
        }

        // ==================== (3) 에셋 ↔ 런타임 1:1 ====================

        [Test]
        public void 에셋이_든_값과_카탈로그가_내주는_값이_필드_단위로_같다()
        {
            foreach (AccessoryDefSO def in LoadDefs())
            {
                ItemCatalogEntry entry = ItemCatalog.Item(def.slot, def.itemIndex);
                Assert.IsNotNull(entry, $"'{def.itemId}' 에셋이 카탈로그에 실리지 않았습니다.");

                string where = $"[{def.itemId}]";
                Assert.AreEqual(def.itemId, entry.Id, $"{where} 아이디");
                Assert.AreEqual(def.displayName, entry.DisplayName, $"{where} 표시 이름");
                Assert.AreEqual(def.description, entry.Description, $"{where} 설명");
                Assert.AreEqual(def.requiredLevel, entry.RequiredLevel, $"{where} 요구 레벨");
                Assert.AreEqual(def.slot, entry.Slot, $"{where} 카테고리");
                Assert.AreEqual(def.itemIndex, entry.ItemIndex, $"{where} 자리 번호");
                Assert.AreEqual(ItemCategory.Equipment, entry.Category, $"{where} 항목 종류");

                // ★ 2026-09-05 security S-13 — 이 두 줄이 없던 동안 팩 소속·등급 선언은
                //   에셋에서 카탈로그로 넘어가지 않아도 어떤 초록도 갈라지지 않았다.
                Assert.AreEqual(def.cohortId, entry.CohortId,
                    $"{where} 코호트 — 에셋이 적은 모집단과 카탈로그가 실은 모집단이 다릅니다. " +
                    "이게 어긋나면 팩을 산 사람이 아니라 안 산 사람의 등급이 미끄러집니다.");
                Assert.AreEqual(def.declaredRarity, entry.Declared,
                    $"{where} 등급 선언 — 에셋의 선언과 카탈로그가 실은 선언이 다릅니다.");

                Assert.IsNotNull(entry.Icon, $"{where} 아이콘이 비었습니다.");
                Assert.AreEqual(def.icon.Length, entry.Icon.Length, $"{where} 아이콘 조각 수");

                for (int p = 0; p < def.icon.Length; p++)
                {
                    AccessoryIconPartData src = def.icon[p];
                    ItemIconPart got = entry.Icon[p];

                    Assert.AreEqual(src.kind, got.Kind, $"{where} p{p} 종류");
                    Assert.AreEqual(src.tone, got.Tone, $"{where} p{p} 색 역할");
                    AssertColor(src.color, got.Color, $"{where} p{p} 색");

                    Assert.AreEqual(src.values.Length, got.Values.Length, $"{where} p{p} 좌표 개수");
                    for (int v = 0; v < src.values.Length; v++)
                    {
                        Assert.AreEqual(src.values[v], got.Values[v], 1e-4f, $"{where} p{p} 좌표 {v}");
                    }
                }
            }
        }

        [Test]
        public void 주색과_보조색은_아이콘_조각에서_그대로_파생된다()
        {
            // 카드 색과 몸에 칠하는 색이 갈라지는 것을 막는 기존 규약(2026-08-30 사용자 신고)이
            // 에셋 전환 뒤에도 그대로인지 본다 — 첫 tone=0 조각이 주색, 첫 tone=1 조각이 보조색.
            foreach (AccessoryDefSO def in LoadDefs())
            {
                ItemCatalogEntry entry = ItemCatalog.Item(def.slot, def.itemIndex);

                Color primary = ItemCatalog.InkTone, secondary = ItemCatalog.InkTone;
                bool gotPrimary = false, gotSecondary = false;
                foreach (AccessoryIconPartData part in def.icon)
                {
                    if (part.tone == 0 && !gotPrimary) { primary = part.color; gotPrimary = true; }
                    else if (part.tone != 0 && !gotSecondary) { secondary = part.color; gotSecondary = true; }
                }

                AssertColor(primary, entry.PrimaryColor, $"[{def.itemId}] 주색");
                AssertColor(gotSecondary ? secondary : primary, entry.SecondaryColor, $"[{def.itemId}] 보조색");
            }
        }

        // ==================== (4) hidesHair ====================

        [Test]
        public void hidesHair는_렌더러가_지금_실제로_하는_일과_같은_말을_한다()
        {
            // A단계에서 이 필드를 읽는 코드는 아직 없다. 그래서 값이 틀려도 화면은 멀쩡하다 —
            // 즉 <b>지금</b> 못 박아 두지 않으면 훗날 렌더러를 이 필드로 갈아타는 라운드가
            // "전환"이 아니라 "동작 변경"이 되어 버린다(Major 4의 근본 해법이 되려면 그러면 안 된다).
            AccessoryShapeBuilder.Rig rig = MakeRig();

            foreach (AccessoryDefSO def in LoadDefs())
            {
                if (def.slot != EquipmentSlot.Head)
                {
                    Assert.IsFalse(def.hidesHair,
                        $"[{def.itemId}]는 모자가 아닌데 머리카락을 가린다고 적혀 있습니다 — " +
                        "머리카락에 관여하는 것은 HEAD 카테고리뿐입니다.");
                    continue;
                }

                bool rendererHides = !float.IsPositiveInfinity(
                    AccessoryShapeBuilder.HatCoverLocalY(def.itemIndex, rig));

                Assert.AreEqual(rendererHides, def.hidesHair,
                    $"[{def.itemId}] hidesHair={def.hidesHair}인데 AccessoryShapeBuilder.HatCoverLocalY는 " +
                    $"{(rendererHides ? "가린다" : "가리지 않는다")}고 말합니다. 에셋이 렌더러보다 먼저 " +
                    "거짓말을 시작하면, 훗날 렌더러가 이 필드를 읽는 순간 원인 모를 그림 변화가 됩니다.");
            }
        }

        [Test]
        public void 왕관만_머리카락을_남긴다()
        {
            // 위 테스트는 "에셋과 렌더러가 같은 말을 한다"만 본다 — 둘 다 동시에 뒤집히면 통과한다.
            // 그래서 <b>내용</b> 자체를 한 번 못 박는다: 왕관은 얹는 것이라 밑이 뚫려 있다.
            Assert.AreEqual(CrownIndex, AccessoryShapeBuilder.HeadCrown,
                "왕관의 자리 번호가 바뀌었습니다 — 이 테스트의 전제가 깨졌습니다.");

            foreach (AccessoryDefSO def in LoadDefs())
            {
                if (def.slot != EquipmentSlot.Head) continue;
                Assert.AreEqual(def.itemIndex != CrownIndex, def.hidesHair,
                    $"[{def.itemId}] 모자 중 왕관만 머리카락을 남기고 나머지 셋은 덮습니다.");
            }
        }

        // ==================== (5) 에셋 오염 금지 ====================

        [Test]
        public void 런타임_좌표_배열은_에셋의_배열과_다른_인스턴스다()
        {
            foreach (AccessoryDefSO def in LoadDefs())
            {
                ItemCatalogEntry entry = ItemCatalog.Item(def.slot, def.itemIndex);
                for (int p = 0; p < def.icon.Length; p++)
                {
                    Assert.AreNotSame(def.icon[p].values, entry.Icon[p].Values,
                        $"[{def.itemId}] p{p}의 좌표 배열을 에셋과 <b>공유</b>하고 있습니다 — " +
                        "런타임이 한 칸만 써도 에디터에서 .asset 파일이 조용히 더러워집니다.");
                }
            }
        }

        // ==================== (6) S-13 — 골든이 코호트·등급을 본다 ====================

        /// <summary>
        /// ★ security 발견 S-13(2026-09-05): 골든과 파리티 감사가 <c>cohortId</c>·<c>declaredRarity</c>를
        /// <b>한 번도 보지 않았다</b>. 그래서 "팩 아이템을 기본 코호트로 실어 버렸다" 같은 오기입이
        /// 골든을 <b>한 글자도</b> 흔들지 않았다.
        ///
        /// <para><b>기대값의 출처가 다르다</b>는 것이 이 테스트의 전부다 —
        /// 기대값은 <c>.asset</c> 필드에서, 실측값은 <b>디스크의 골든 비트</b>에서 온다.
        /// <see cref="ItemCatalogDigest.Build"/>를 양쪽에 쓰면 그 함수가 틀어질 때 둘이 함께 틀어져
        /// <b>아무것도 못 잰다</b>(TEAM.md "생성기와 검사기가 같이 틀린다").</para>
        /// </summary>
        [Test]
        public void 골든이_적은_코호트와_등급이_에셋이_말하는_값과_같다()
        {
            Dictionary<string, string> byId = ReadGoldenItemLines();

            foreach (AccessoryDefSO def in LoadDefs())
            {
                Assert.IsTrue(byId.TryGetValue(def.itemId, out string line),
                    $"골든에 '{def.itemId}' 줄이 없습니다 — 골든이 낡았습니다(재생성 필요).");

                Assert.AreEqual(def.cohortId.ToString(CultureInfo.InvariantCulture),
                    ReadToken(line, ItemCatalogDigest.CohortToken),
                    $"[{def.itemId}] 골든의 코호트가 에셋과 다릅니다.\n  골든 줄: {line}");
                Assert.AreEqual(def.declaredRarity.ToString(),
                    ReadToken(line, ItemCatalogDigest.RarityToken),
                    $"[{def.itemId}] 골든의 등급 선언이 에셋과 다릅니다.\n  골든 줄: {line}");
            }
        }

        /// <summary>양성/음성 대조 — 위 테스트의 <b>계측기</b>가 살아 있는가.
        /// 토큰 판독기가 늘 같은 값을 뱉거나 늘 null을 뱉으면 위 테스트는 영원히 초록이다.</summary>
        [Test]
        public void 토큰_판독기가_값이_다르면_다르게_읽는다()
        {
            const string head = "  item id=x cat=Equipment slot=Head idx=0 lv=1 invocable=1";
            string basic = head + ItemCatalogDigest.CohortToken + "0" + ItemCatalogDigest.RarityToken + "Derived";
            string pack = head + ItemCatalogDigest.CohortToken + "3" + ItemCatalogDigest.RarityToken + "Rare";

            // 양성 — 서로 다른 두 줄을 실제로 다르게 읽는다.
            Assert.AreEqual("0", ReadToken(basic, ItemCatalogDigest.CohortToken));
            Assert.AreEqual("Derived", ReadToken(basic, ItemCatalogDigest.RarityToken));
            Assert.AreEqual("3", ReadToken(pack, ItemCatalogDigest.CohortToken));
            Assert.AreEqual("Rare", ReadToken(pack, ItemCatalogDigest.RarityToken));

            // 음성 — 토큰이 없는 줄에서는 null이다(있는데 못 읽는 것과 구분된다).
            Assert.IsNull(ReadToken(head, ItemCatalogDigest.CohortToken));
            Assert.IsNull(ReadToken(head, ItemCatalogDigest.RarityToken));

            // 골든 비교기도 이 토큰의 차이에 실제로 반응하는가 — 이게 아니면 골든에 토큰만 있고
            // 아무도 안 보는 상태가 된다.
            Assert.IsNotNull(ItemCatalogDigest.FirstDifference(basic, pack),
                "골든 비교기가 코호트/등급 차이를 못 봅니다 — 토큰을 적어도 소용이 없습니다.");
        }

        /// <summary>다이제스트가 <b>모든 항목</b>에 두 토큰을 적는가. 하나라도 빠지면 그 항목은
        /// 골든 대조에서 통째로 사각지대다.</summary>
        [Test]
        public void 다이제스트가_두_토큰을_모든_항목에_적는다()
        {
            string digest = ItemCatalogDigest.Build();
            int expected = ItemCatalog.EquipmentCount + ItemCatalog.ActionCount;

            Assert.AreEqual(expected, CountOccurrences(digest, ItemCatalogDigest.CohortToken),
                "코호트 토큰 개수가 항목 수와 다릅니다 — 일부 항목의 팩 소속이 골든에서 빠집니다.");
            Assert.AreEqual(expected, CountOccurrences(digest, ItemCatalogDigest.RarityToken),
                "등급 토큰 개수가 항목 수와 다릅니다.");

            // 계측기 교정 — 이 세는 함수가 "무엇이든 항목 수만큼 센다"면 위 두 줄은 의미가 없다.
            Assert.AreEqual(0, CountOccurrences(digest, " 이토큰은_다이제스트에_없다="),
                "존재하지 않는 토큰이 0건이 아닙니다 — 개수 세는 함수를 신뢰할 수 없습니다.");
            Assert.Greater(expected, 0, "항목이 0개면 위 단언은 0==0으로 아무것도 지키지 않습니다.");
        }

        // ==================== 도구 ====================

        /// <summary>골든에서 <c>item id=</c> 줄만 <c>id -&gt; 줄</c>로. <b>디스크의 비트</b>가 출처다.</summary>
        private static Dictionary<string, string> ReadGoldenItemLines()
        {
            string goldenPath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, ItemCatalogDigest.GoldenAssetPath);
            Assert.IsTrue(File.Exists(goldenPath), $"골든 스냅샷이 없습니다: {ItemCatalogDigest.GoldenAssetPath}");

            var byId = new Dictionary<string, string>();
            foreach (string raw in File.ReadAllText(goldenPath).Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.TrimEnd();
                string id = ReadToken(line, IdToken);
                if (id != null) byId[id] = line;
            }

            Assert.Greater(byId.Count, 0, "골든에서 항목 줄을 한 줄도 못 찾았습니다 — 판독기가 죽었습니다.");
            return byId;
        }

        /// <summary>다이제스트 항목 줄의 첫 토큰. <see cref="ItemCatalogDigest.CohortToken"/>들과 달리
        /// 프로덕션 상수가 없어 여기서만 쓰는 값이고, 위 <c>Assert.Greater</c>가 이 니들이 죽으면
        /// <b>시끄럽게</b> 빨개지게 한다.</summary>
        private const string IdToken = "item id=";

        private static string ReadToken(string line, string token)
        {
            if (line == null) return null;
            int at = line.IndexOf(token, StringComparison.Ordinal);
            if (at < 0) return null;
            int from = at + token.Length;
            int to = line.IndexOf(' ', from);
            return to < 0 ? line.Substring(from) : line.Substring(from, to - from);
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int n = 0, at = 0;
            while ((at = haystack.IndexOf(needle, at, StringComparison.Ordinal)) >= 0) { n++; at += needle.Length; }
            return n;
        }


        /// <summary>AccessoryShapeCatalogTests와 같은 표준 리그(실제 캐릭터 비율에서 뽑은 값).
        /// hidesHair 판정은 리그 크기와 무관하지만(무한대인지 아닌지만 본다), 같은 리그를 써서
        /// 두 테스트가 다른 전제 위에 서는 일을 없앤다.</summary>
        private static AccessoryShapeBuilder.Rig MakeRig()
        {
            const float R = 0.32f;
            const float H = 2.4f;
            return new AccessoryShapeBuilder.Rig(R, H - R, 1.7646944f, 0.9346944f, 1f);
        }

        private static void AssertColor(Color expected, Color actual, string where)
        {
            Assert.AreEqual(expected.r, actual.r, 1e-5f, $"{where} R");
            Assert.AreEqual(expected.g, actual.g, 1e-5f, $"{where} G");
            Assert.AreEqual(expected.b, actual.b, 1e-5f, $"{where} B");
            Assert.AreEqual(expected.a, actual.a, 1e-5f, $"{where} A");
        }
    }
}
