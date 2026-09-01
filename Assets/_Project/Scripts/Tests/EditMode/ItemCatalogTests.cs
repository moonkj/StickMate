using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 보관함 카탈로그(Core/ItemCatalog.cs) 회귀 테스트 — 2026-08-30 정보창 리디자인 라운드.
    ///
    /// ============================================================================
    /// 무엇을 잡으려는가
    /// ============================================================================
    ///  (1) <b>이중 정의 금지</b>: 장비 전종의 이름/자리/요구레벨을 <see cref="EquipmentModel"/> 경유로
    ///      읽은 값과 대조한다 — 한 글자라도 달라지면 실패한다. 이 프로젝트가 이미 두 번 겪은
    ///      "같은 사실이 두 곳에 적혀 조용히 어긋나는" 실패 유형의 직접 잠금이다.
    ///      (2026-08-30 32종 확장에서 표의 주인이 EquipmentModel -> ItemCatalog로 바뀌었지만,
    ///       "주인이 하나뿐"이라는 이 테스트의 의도는 그대로다.)
    ///  (2) <b>거짓말 금지</b>: 설명 문구에 이 앱에 존재하지 않는 수치("방어력 +2")를 넣으면 실패한다.
    ///  (3) <b>탈출구 명시</b>: 방해가 될 수 있는 행동(로데오 커서)의 설명에는 빠져나오는 방법이
    ///      문장 안에 있어야 한다(불변 원칙 계열 — 디자이너가 코드로 확인한 규칙).
    ///  (4) 행동 항목은 <b>레벨과 무관하게 항상 보유</b>다(이미 단축키/메뉴로 쓸 수 있으므로).
    /// </summary>
    public class ItemCatalogTests
    {
        private const string DefaultConfigPath = "Assets/_Project/Data/DefaultStickConfig.asset";

        private static StickConfig LoadDefaultConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<StickConfig>(DefaultConfigPath);
            Assert.IsNotNull(config, $"기본 설정 자산을 찾지 못했습니다: {DefaultConfigPath}");
            return config;
        }

        [SetUp]
        public void ResetModels()
        {
            CharacterProgressionModel.ResetForTesting();
            EquipmentModel.ResetForTesting();
        }

        /// <summary>카테고리당 아이템 수. 7×4=28 -> <b>7×6=42</b>(2026-09-01 카테고리당 +2종).
        /// 숫자를 여기 <b>한 곳에만</b> 적는다 — 아래 두 단언이 이 상수에서 파생된다.</summary>
        private const int ItemsPerSlot = 6;

        private const int EquipmentTotal = ItemsPerSlot * EquipmentModel.SlotCount;   // 42

        [Test]
        public void 장비_항목은_7카테고리_고정개수이고_EquipmentModel과_같은_사실을_말한다()
        {
            StickConfig config = LoadDefaultConfig();
            int found = 0;

            for (int s = 0; s < EquipmentModel.SlotCount; s++)
            {
                var slot = (EquipmentSlot)s;
                Assert.AreEqual(ItemsPerSlot, EquipmentModel.ItemCount(slot),
                    $"[{EquipmentModel.SlotName(slot)}] 카테고리의 아이템이 {ItemsPerSlot}개가 아닙니다 " +
                    "(2026-08-30 표정 삭제로 7×4 -> 2026-09-01 카테고리당 +2종으로 7×6=42).");

                for (int i = 0; i < EquipmentModel.ItemCount(slot); i++)
                {
                    ItemCatalogEntry entry = ItemCatalog.Item(slot, i);
                    Assert.IsNotNull(entry, $"[{EquipmentModel.SlotName(slot)}] {i}번 아이템이 카탈로그에 없습니다.");
                    Assert.AreEqual(ItemCategory.Equipment, entry.Category);
                    Assert.AreEqual(slot, entry.Slot.Value, "아이템이 다른 카테고리를 가리킵니다.");
                    Assert.AreEqual(i, entry.ItemIndex, "아이템의 자리 번호가 표의 순서와 다릅니다.");

                    Assert.AreEqual(EquipmentModel.ItemName(slot, i), entry.DisplayName,
                        "아이템 이름이 두 곳에 따로 적혀 있습니다 — 카탈로그 표 하나에서만 나와야 합니다.");
                    Assert.AreEqual(EquipmentModel.SlotName(slot), entry.CategoryLabel,
                        "카테고리 이름이 두 곳에 따로 적혀 있습니다.");
                    Assert.AreEqual(EquipmentModel.RequiredLevel(slot, i), entry.ResolveUnlockLevel(config),
                        "요구 레벨이 두 곳에 따로 적혀 있습니다.");
                    Assert.AreEqual(i, ItemCatalog.IndexOfItemId(slot, entry.Id),
                        "아이디 -> 자리 역방향 조회가 어긋납니다 — 저장 파일 복원이 엉뚱한 아이템을 걸치게 됩니다.");
                    found++;
                }
            }

            Assert.AreEqual(EquipmentTotal, found, $"장비 아이템이 {EquipmentTotal}종이 아닙니다.");
            Assert.AreEqual(EquipmentTotal, ItemCatalog.EquipmentCount);
        }

        [Test]
        public void 카테고리마다_첫_아이템은_처음부터_보유이고_요구_레벨은_오름차순이다()
        {
            // 하나도 고를 수 없는 카테고리가 있으면 그 칸은 Lv.1 사용자에게 <b>빈 자리</b>로 보인다.
            // 그리고 카테고리 안에서 요구 레벨이 뒤죽박죽이면 "위에서 아래로 열린다"는 목록의 약속이 깨진다.
            for (int s = 0; s < EquipmentModel.SlotCount; s++)
            {
                var slot = (EquipmentSlot)s;
                Assert.AreEqual(1, EquipmentModel.RequiredLevel(slot, 0),
                    $"[{EquipmentModel.SlotName(slot)}]의 첫 아이템이 Lv.1 보유가 아닙니다 — Lv.1 사용자에게 빈 카테고리가 됩니다.");

                int prev = 0;
                for (int i = 0; i < EquipmentModel.ItemCount(slot); i++)
                {
                    int need = EquipmentModel.RequiredLevel(slot, i);
                    Assert.GreaterOrEqual(need, prev,
                        $"[{EquipmentModel.SlotName(slot)}]의 {i}번({EquipmentModel.ItemName(slot, i)}) 요구 레벨 {need}이 " +
                        $"앞 아이템({prev})보다 낮습니다 — 목록이 열리는 순서와 보이는 순서가 어긋납니다.");
                    prev = need;
                }
            }
        }

        [Test]
        public void 행동_항목은_레벨과_무관하게_항상_보유다()
        {
            StickConfig config = LoadDefaultConfig();
            Assert.AreEqual(1, CharacterProgressionModel.Level, "전제: Lv.1에서 시작.");

            int actions = 0;
            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                ItemCatalogEntry entry = ItemCatalog.At(i);
                if (entry.Category != ItemCategory.Action) continue;
                actions++;

                Assert.IsTrue(entry.IsOwned(config),
                    $"[{entry.DisplayName}]이 Lv.1에서 잠겨 있습니다 — 행동은 이미 단축키/메뉴로 쓸 수 있으므로 " +
                    "잠긴 척하면 그것이 거짓말입니다.");
                Assert.IsNull(entry.ResolveUnlockLevel(config), "행동에는 해제 레벨이 없어야 합니다.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.ActionStatus),
                    $"[{entry.DisplayName}]의 상태 슬롯 문구가 비어 있습니다(목록 오른쪽 칸이 빈칸이 됩니다).");
            }

            Assert.AreEqual(ItemCatalog.ActionCount, actions);
            Assert.GreaterOrEqual(actions, 13,
                "행동 항목이 13개보다 적습니다 — 보관함이 빈 화면이 될 수 있어 디자이너가 13개 이상을 요구했습니다.");
        }

        [Test]
        public void 설명에_이_앱에_없는_전투_수치를_적지_않는다()
        {
            // "방어력 +2" 같은 문구는 이 앱에 존재하지 않는 시스템을 있는 것처럼 말한다.
            var banned = new Regex("(공격력|방어력|명중률\\s*\\+|회피|체력|HP|데미지|스탯\\s*\\+|\\+\\s*\\d+\\s*(포인트|pt))");

            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                ItemCatalogEntry entry = ItemCatalog.At(i);
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.Description),
                    $"[{entry.DisplayName}]의 설명이 비어 있습니다.");
                Assert.IsFalse(banned.IsMatch(entry.Description),
                    $"[{entry.DisplayName}]의 설명이 이 앱에 없는 전투 수치를 언급합니다: \"{entry.Description}\"");
            }
        }

        [Test]
        public void 설명에_이_앱에_없는_소리를_주장하지_않는다()
        {
            // 근거: 이 프로젝트에는 오디오가 <b>하나도 없다</b>(AudioSource/AudioClip/PlayOneShot 전수
            // 검색 0건, 2026-08-30 ux-designer 확인). 방울 목걸이 원문 "움직일 때 소리가 난다"가 실제로
            // 이 규칙에 걸렸고 리더 승인으로 교체됐다 — 같은 실수가 다시 들어오면 여기서 잡는다.
            var banned = BannedSoundWords();

            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                ItemCatalogEntry entry = ItemCatalog.At(i);
                Assert.IsFalse(banned.IsMatch(entry.Description),
                    $"[{entry.DisplayName}]의 설명이 들리지 않는 소리를 주장합니다: \"{entry.Description}\" " +
                    "— 이 앱에는 오디오 시스템이 없습니다.");
            }
        }

        /// <summary>
        /// 소리 금지어 패턴. `울린다`는 <b>`어울린다`를 제외</b>하고 잡는다 — 졸린눈 설명
        /// "오후 3시 이후에 잘 어울린다."가 정상 문구인데 오탐으로 걸렸다(2026-08-30 R2 M1).
        /// .NET 정규식은 가변길이 후방탐색을 지원하므로 `(?&lt;!어)`로 한 글자만 배제한다.
        /// </summary>
        private static Regex BannedSoundWords()
            => new Regex("(소리|딸랑|짤랑|삐-|효과음|(?<!어)울린다)");

        [Test]
        public void 소리_금지어_패턴_자체가_진짜_소리는_잡고_어울린다는_통과시킨다()
        {
            // 양성 대조 — 이 가드가 죽어도 초록이 뜨는 일을 막는다.
            var banned = BannedSoundWords();

            Assert.IsTrue(banned.IsMatch("움직일 때 소리가 난다."), "정상 금지어 `소리`를 놓쳤습니다.");
            Assert.IsTrue(banned.IsMatch("걸을 때마다 방울이 울린다."), "정상 금지어 `울린다`를 놓쳤습니다.");
            Assert.IsTrue(banned.IsMatch("딸랑거린다."), "정상 금지어 `딸랑`을 놓쳤습니다.");

            // 음성 대조 — R2 M1의 오탐 재현 방지.
            Assert.IsFalse(banned.IsMatch("오후 3시 이후에 잘 어울린다."), "`어울린다`를 오탐했습니다.");
            Assert.IsFalse(banned.IsMatch("걸으면 자락이 흔들린다."), "`흔들린다`를 오탐했습니다.");
        }

        [Test]
        public void 하드웨어_반응_설명은_얼굴이_아니라_아이콘을_말한다()
        {
            // 근거: Interaction/HardwareReactionRenderer.cs가 그리는 것은 머리 주변 이모트 아이콘이고,
            // 이 앱에 상태별 표정 시스템은 존재하지 않는다. 원문 "표정만 바뀌고"는 없는 기능을 말했다.
            ItemCatalogEntry hardware = FindById("action.hardware_reaction");
            StringAssert.DoesNotContain("표정", hardware.Description,
                "하드웨어 반응은 얼굴을 바꾸지 않습니다 — 머리 주변 아이콘을 띄웁니다.");
            StringAssert.Contains("아이콘", hardware.Description,
                "실제로 하는 일(아이콘 표시)이 설명에 없습니다.");
        }

        [Test]
        public void 장비는_전부_아이콘을_갖고_행동은_아이콘이_없다()
        {
            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                ItemCatalogEntry entry = ItemCatalog.At(i);
                if (entry.Category == ItemCategory.Equipment)
                {
                    Assert.IsNotNull(entry.Icon, $"[{entry.DisplayName}]에 카드 아이콘이 없습니다.");
                    Assert.Greater(entry.Icon.Length, 0, $"[{entry.DisplayName}]의 아이콘이 비어 있습니다.");
                }
                else
                {
                    // ★ 네거티브 컨트롤 — 행동은 카드가 아니라 목록 한 줄이라 아이콘이 <b>없어야</b> 한다.
                    // 이 단언이 없으면 "전부 null이어도 통과"가 되어 위 검사가 의미를 잃는다.
                    Assert.IsNull(entry.Icon,
                        $"[{entry.DisplayName}]는 행동인데 카드 아이콘을 갖고 있습니다(카드로 그려지지 않습니다).");
                }
            }
        }

        [Test]
        public void 아이콘_좌표는_40x40_뷰박스_안에_있고_선은_점_두_개_이상이다()
        {
            // 스펙(icon-paths.json)의 viewBox가 40×40이다. 벗어난 좌표는 카드 썸네일 밖으로 삐져나간다.
            const float Slack = 1f;   // 곡선 샘플링 반올림 여유.

            for (int s = 0; s < EquipmentModel.SlotCount; s++)
            {
                var slot = (EquipmentSlot)s;
                for (int i = 0; i < EquipmentModel.ItemCount(slot); i++)
                {
                    ItemCatalogEntry entry = ItemCatalog.Item(slot, i);
                    foreach (ItemIconPart part in entry.Icon)
                    {
                        Assert.IsNotNull(part.Values, $"[{entry.DisplayName}] 아이콘 파츠에 좌표가 없습니다.");

                        // ★ Polygon(채운 다각형)도 좌표가 <b>점 목록</b>이다 — Polyline과 같은 규약.
                        //   여기서 종류를 하나하나 나열하면 새 종류가 생길 때마다 '원'으로 오분류된다.
                        if (part.HasPoints)
                        {
                            Assert.AreEqual(0, part.Values.Length % 2,
                                $"[{entry.DisplayName}] 꺾은선의 좌표 개수가 홀수입니다.");
                            Assert.GreaterOrEqual(part.PointCount, 2,
                                $"[{entry.DisplayName}] 점이 하나뿐인 꺾은선은 그려지지 않습니다.");
                        }
                        else
                        {
                            Assert.AreEqual(3, part.Values.Length,
                                $"[{entry.DisplayName}] 원 파츠는 cx,cy,r 세 값이어야 합니다.");
                            Assert.Greater(part.Values[2], 0f, $"[{entry.DisplayName}] 원의 반지름이 0입니다.");
                        }

                        // ★ 채운 다각형은 <b>닫혀</b> 있어야 한다(마지막 점 = 첫 점). 안 닫히면
                        //   삼각분할이 마지막 변을 임의로 이어 붙여 몸과 다른 덩어리가 된다.
                        if (part.Kind == ItemIconPartKind.Polygon)
                        {
                            int n = part.PointCount;
                            Assert.GreaterOrEqual(n, 4,
                                $"[{entry.DisplayName}] 채운 다각형이 {n}점입니다 — 닫는 점을 포함해 4점 이상이어야 면이 생깁니다.");
                            Assert.AreEqual(part.Values[0], part.Values[(n - 1) * 2], 1e-4f,
                                $"[{entry.DisplayName}] 채운 다각형의 마지막 점이 첫 점과 다릅니다(x).");
                            Assert.AreEqual(part.Values[1], part.Values[(n - 1) * 2 + 1], 1e-4f,
                                $"[{entry.DisplayName}] 채운 다각형의 마지막 점이 첫 점과 다릅니다(y).");
                        }

                        int coords = part.HasPoints ? part.Values.Length : 2;
                        for (int v = 0; v < coords; v++)
                        {
                            Assert.That(part.Values[v], Is.InRange(-Slack, 40f + Slack),
                                $"[{entry.DisplayName}] 아이콘 좌표가 40×40 뷰박스를 벗어납니다: {part.Values[v]}");
                        }
                    }
                }
            }
        }

        [Test]
        public void 방해가_될_수_있는_행동에는_탈출구가_문장에_들어_있다()
        {
            // 근거: Interaction/RodeoCursorWatcher.cs — 하차 후 커서가 다시 멈춰야만 재발동한다
            //       (즉 사용자가 커서를 움직이면 빠져나올 수 있다). 문구가 그 사실을 말해야 한다.
            ItemCatalogEntry rodeo = FindById("action.rodeo_cursor");
            StringAssert.Contains("떨어진다", rodeo.Description,
                "로데오 커서 설명에 빠져나오는 방법이 없습니다 — 탈출구를 암시하지 않는 문구는 쓰지 않습니다.");

            ItemCatalogEntry runaway = FindById("action.runaway");
            StringAssert.Contains("돌아온다", runaway.Description,
                "가출 설명에 돌아오게 하는 방법이 없습니다.");
        }

        [Test]
        public void 항목_아이디는_중복되지_않고_비어_있지_않다()
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                string id = ItemCatalog.At(i).Id;
                Assert.IsFalse(string.IsNullOrWhiteSpace(id), "빈 Id가 있습니다 — 훗날 상점 SKU가 될 값입니다.");
                Assert.IsTrue(seen.Add(id), $"Id가 중복됩니다: {id}");
            }
        }

        [Test]
        public void 상태_슬롯_문구는_장비와_행동이_같은_자리를_쓴다()
        {
            StickConfig config = LoadDefaultConfig();

            // 왕관(모자 4번, req20)은 Lv.1에서 확실히 잠겨 있다. 천모자(0번)는 이제 처음부터 보유라
            // 잠금 문구의 표본이 될 수 없다.
            ItemCatalogEntry crown = ItemCatalog.Item(EquipmentSlot.Head, 3);
            StringAssert.Contains("Lv.", crown.ResolveStatusSlot(config),
                "Lv.1에서 잠긴 장비의 상태 슬롯은 '몇 레벨에 열리는지'를 보여줘야 합니다.");

            ItemCatalogEntry cap = ItemCatalog.Item(EquipmentSlot.Head, 0);
            Assert.AreEqual("착용 중", cap.ResolveStatusSlot(config),
                "새 캐릭터는 천모자를 걸치고 시작합니다(핸드오프 확정 기본 차림).");

            // ★ 2026-09-01 — 여기 있던 "⌃⌥⌘A" 하드코딩을 지웠다. 그 리터럴은 <b>macOS 표기</b>라,
            // 테스트가 그것을 잠그는 동안 Windows 사용자에게는 존재하지 않는 조합이 안내되고 있었다
            // (Windows 패리티 감사 C3). 이제 표기를 만드는 곳은 Core/ShortcutLabel 하나뿐이고,
            // 여기서 확인할 사실은 "카탈로그가 <b>그 단일 소스를</b> 쓰는가"다.
            // 표기 자체가 플랫폼별로 옳은지는 ShortcutLabelParityTests가 따로 잠근다 —
            // 이 파일이 문자열을 다시 적으면 단일 소스가 둘이 된다.
            ItemCatalogEntry archery = FindById("action.archery");
            Assert.AreEqual(ShortcutLabel.Chord("A"), archery.ResolveStatusSlot(config),
                "직접 부를 수 있는 행동의 상태 슬롯에는 단축키가 나와야 합니다.");
            StringAssert.EndsWith("A", archery.ResolveStatusSlot(config),
                "단축키 문구가 동작키(A)로 끝나지 않습니다 — 조합키만 남고 키가 빠졌습니다.");

            ItemCatalogEntry tidy = FindById("action.desktop_tidy");
            Assert.AreEqual(ItemCatalogEntry.AutoOnlyStatus, tidy.ResolveStatusSlot(config),
                "자율 발동 전용 행동의 상태 슬롯 문구가 다릅니다.");
        }

        [Test]
        public void 해제된_아이템을_착용하면_상태_슬롯이_착용_중으로_바뀐다()
        {
            StickConfig config = LoadDefaultConfig();

            // 털모자(모자 1번, req5)로 검증한다 — 0번은 처음부터 보유라 "열리는 순간"이 없다.
            const int Fur = 1;
            int need = EquipmentModel.RequiredLevel(EquipmentSlot.Head, Fur);
            ItemCatalogEntry fur = ItemCatalog.Item(EquipmentSlot.Head, Fur);
            Assert.AreEqual("Lv.5에 열림", fur.ResolveStatusSlot(config), "Lv.1에서는 잠겨 있어야 합니다.");

            // 요구 레벨까지 올린다(연속 레벨업 이월 경로를 그대로 쓴다).
            float bulk = 0f;
            for (int lv = 1; lv < need; lv++) bulk += CharacterProgressionModel.XpToNextLevel(lv, config);
            CharacterProgressionModel.AddXp(bulk + 1f, config);

            Assert.AreEqual("보유", fur.ResolveStatusSlot(config));

            Assert.IsTrue(EquipmentModel.TryWear(EquipmentSlot.Head, Fur, config));
            Assert.AreEqual("착용 중", fur.ResolveStatusSlot(config));

            // ★ 카테고리당 하나만 — 털모자를 쓰면 천모자는 "보유"로 돌아가야 한다.
            ItemCatalogEntry cap = ItemCatalog.Item(EquipmentSlot.Head, 0);
            Assert.AreEqual("보유", cap.ResolveStatusSlot(config),
                "같은 카테고리의 아이템 두 개가 동시에 '착용 중'입니다 — 카테고리당 하나 규칙이 깨졌습니다.");

            // 보관함 헤더 분자("걸치는 것 n/32")가 카테고리별 보유 수의 합과 같아야 한다.
            int owned = 0;
            for (int s = 0; s < EquipmentModel.SlotCount; s++) owned += EquipmentModel.OwnedItemCount((EquipmentSlot)s);
            Assert.AreEqual(owned, ItemCatalog.UnlockedEquipmentCount(config),
                "보관함 헤더의 보유 수가 카테고리별 보유 수 합과 어긋납니다.");
        }

        private static ItemCatalogEntry FindById(string id)
        {
            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                if (ItemCatalog.At(i).Id == id) return ItemCatalog.At(i);
            }
            Assert.Fail($"카탈로그에 {id} 항목이 없습니다.");
            return null;
        }
    }
}
