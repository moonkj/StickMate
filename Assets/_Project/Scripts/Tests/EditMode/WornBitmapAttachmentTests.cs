using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using StickMate.Core;
using StickMate.Interaction;
using UnityEditor;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ <b>몸에 붙는 비트맵 — P0 교정 관문</b>(2026-09-09).
    /// 설계 정본: <c>docs/GAME_ARCHITECTURE_REVIEW.md</c> §19-3 「P0가 그림 없이 가능한 이유」.
    ///
    /// ============================================================================
    /// 이 파일이 존재하는 이유 — <b>그림을 한 장도 부탁하기 전에 통과해야 하는 여섯 가지</b>
    /// ============================================================================
    /// 오늘 이전에 12종짜리 라운드가 <b>세 번 연속</b> 실패했고, 세 번의 공통 손실은
    /// «12종을 다 만든 뒤에야 판정을 받았다»였다. 그래서 P0는 «예쁜가»를 묻지 않는다 —
    /// 절차적 교정 스프라이트(<see cref="WornSpriteCalibration"/>) 한 장으로
    /// <b>부착 · 배율 · z-order · 좌우반전 · 잉크박스 · 숨김</b> 여섯 가지를 <b>숫자로</b> 잰다.
    ///
    /// ============================================================================
    /// ★★ 여섯 중 <b>가장 무거운 것은 여섯째(숨김)</b>다 — 절대 불변 원칙 2
    /// ============================================================================
    /// 이 앱의 «캐릭터가 지금 보이는가» 판정은 <c>GetComponentsInChildren&lt;LineRenderer&gt;</c>로
    /// 이뤄지는 자리가 <b>40곳</b>(프로덕션 1 + 테스트 39)이고, <see cref="SpriteRenderer"/>는
    /// <see cref="LineRenderer"/>가 <b>아니다</b>. 배선을 빠뜨리면 전체화면 게임 위에
    /// «몸 없는 비트맵 모자»만 남는데 <b>그 40개 프로브는 전부 초록</b>이다 —
    /// 「죽은 프로브가 산 프로브와 똑같이 생겼다」의 정확히 같은 형태다(§19-8-1).
    ///
    /// ============================================================================
    /// 공허해지지 않게 하는 장치 — <b>출하 42종은 이 칸이 전부 비어 있다</b>
    /// ============================================================================
    /// 그것이 <b>P0의</b> 합격 조건이었으므로(§19-9-a: 「E1 착지 직후 전량 회귀는 0건 변화여야
    /// 한다」), 아무것도 심지 않으면 이 파일의 모든 단언이 <b>빈 목록 순회</b>가 된다.
    /// ★ <b>2026-09-09(P1) 부터는 왕관 1종이 실제로 칸을 채웠다</b> — 그래서 그 관문은
    /// 「전량 0종」이 아니라 <b>「폴더의 PNG와 정확히 일치」</b>로 바뀌었다(그 테스트 문단 참고).
    /// 그래서 <see cref="ItemCatalog.TrySetWornSpriteForTests"/>로 <b>실제 병렬 표에</b> 심고,
    /// 심는 데 실패하면 그 자체를 실패로 단언한다. 그리고
    /// <see cref="착용_비트맵_선언이_폴더의_PNG와_정확히_일치한다"/>가 <b>같은 파일에서</b> 그 계수기가
    /// 살아 있음을 양성 대조로 증명한다.
    /// </summary>
    public sealed class WornBitmapAttachmentTests
    {
        private const string LogPrefix = "[착용비트맵-TEST]";
        private const float Tol = 1e-4f;

        // 배율 1.0 프리팹의 실측 치수(Editor/SceneBootstrapper.cs가 굽는 값 그대로 —
        // Tests/PlayMode/CharacterAccessoryScaleTests가 쓰는 것과 같은 사본이다).
        private const float BaseHeight = StickConfig.BaselineCharacterTotalHeight;
        private const float BaseHeadRadius = 0.22f;
        private const float BaseShoulderY = 1.7646944f;
        private const float BaseHipY = 0.9346944f;
        private const float BaseHeadCenterY = BaseHeight - BaseHeadRadius;

        /// <summary>교정용 배치 사각형 — <b>일부러 비대칭</b>이다(x 오프셋 0.30, y 오프셋 0.85,
        /// 폭 2.4 × 높이 1.6). 정사각·원점이면 「반전을 빼먹었는데 대칭이라 통과」와
        /// 「가로세로를 바꿔 넣었는데 정사각이라 통과」가 둘 다 가능해진다.</summary>
        private static readonly Rect CalibrationRect = new Rect(0.30f, 0.85f, 2.4f, 1.6f);

        private readonly List<GameObject> _rigs = new List<GameObject>(4);
        private readonly List<Object> _assets = new List<Object>(4);

        [SetUp]
        public void SetUp()
        {
            EquipmentModel.ResetForTesting();
            EquipmentDebugUnlock.SetTestOverride(true);   // 요구 레벨을 우회한다(잠김은 이 파일의 주제가 아니다).
        }

        [TearDown]
        public void TearDown()
        {
            ItemCatalog.ClearWornSpriteTestPatches();
            EquipmentModel.ResetForTesting();
            EquipmentDebugUnlock.SetTestOverride(false);
            for (int i = 0; i < _rigs.Count; i++)
            {
                if (_rigs[i] != null) Object.DestroyImmediate(_rigs[i]);
            }
            _rigs.Clear();
            for (int i = 0; i < _assets.Count; i++)
            {
                if (_assets[i] != null) Object.DestroyImmediate(_assets[i]);
            }
            _assets.Clear();
        }

        // ============================================================================
        // 리그 / 헬퍼
        // ============================================================================

        /// <summary><see cref="StickmanMetrics"/>가 실측하는 소스만 갖춘 최소 리그.
        /// <c>Tests/PlayMode/CharacterAccessoryScaleTests.Renderer</c>와 <b>같은 구성</b>이다 —
        /// 씬/프리팹은 배율 하나로만 구워지므로 한 실행에 세 배율을 볼 수 없다.</summary>
        private CharacterAccessoryRenderer Rig(float scale, float facing, float rootScale = 1f)
        {
            var root = new GameObject($"WornSpriteRig_{scale:F2}_{facing:F0}");
            root.transform.position = Vector3.zero;
            _rigs.Add(root);

            float height = BaseHeight * scale;
            var capsule = root.AddComponent<CapsuleCollider2D>();
            capsule.size = new Vector2(0.4f * scale, height);
            capsule.offset = new Vector2(0f, height * 0.5f);

            var head = new GameObject("Head");
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, BaseHeadCenterY * scale, 0f);
            var outline = new GameObject(StickmanMetrics.HeadRingObjectName);
            outline.transform.SetParent(head.transform, false);
            var lr = outline.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 1;
            lr.SetPosition(0, new Vector3(BaseHeadRadius * scale, 0f, 0f));

            var arm = new GameObject("LeftArm");
            arm.transform.SetParent(root.transform, false);
            arm.transform.localPosition = new Vector3(0f, BaseShoulderY * scale, 0f);

            var leg = new GameObject("LeftLeg");
            leg.transform.SetParent(root.transform, false);
            leg.transform.localPosition = new Vector3(0f, BaseHipY * scale, 0f);

            root.AddComponent<StickmanMetrics>();
            // ★ 루트 배율은 <b>컴포넌트를 붙이기 전</b>에 세운다 — Rebuild 가 SyncContainerScale()로
            //   이 값을 상쇄하므로, 나중에 바꾸면 컨테이너가 옛 배율로 굳는다.
            if (!Mathf.Approximately(rootScale, 1f))
            {
                root.transform.localScale = new Vector3(rootScale, rootScale, 1f);
            }
            var renderer = root.AddComponent<CharacterAccessoryRenderer>();
            renderer.SetFacingForTests(facing);
            return renderer;
        }

        /// <summary>교정 스프라이트 한 장(텍스처와 함께 정리 목록에 등록한다).</summary>
        private Sprite Calibration()
        {
            Sprite sprite = WornSpriteCalibration.BuildSprite(out Texture2D tex);
            _assets.Add(sprite);
            _assets.Add(tex);
            return sprite;
        }

        /// <summary>이 자리에 교정 스프라이트를 <b>실제 병렬 표에</b> 심고 걸친다.
        /// 심기와 걸치기 둘 다 절대 조건으로 단언한다 — 하나라도 조용히 실패하면 이 파일의
        /// 나머지 단언이 «스프라이트가 0개라 아무것도 안 재는» 초록이 된다.</summary>
        private Sprite Wear(EquipmentSlot slot, int itemIndex, Sprite back = null)
        {
            UnequipOthers(slot);
            Sprite front = Calibration();
            Vector4 inkBox = WornSpriteCalibration.ExpectedInkBoxInR(CalibrationRect);
            Assert.IsTrue(ItemCatalog.TrySetWornSpriteForTests(slot, itemIndex, front, back, CalibrationRect, inkBox),
                $"{LogPrefix} {slot} {itemIndex}번 자리에 교정 스프라이트를 심지 못했습니다 — " +
                "ItemCatalog 의 착용 비트맵 병렬 표가 채워지지 않았습니다(EnsureLoaded 의 대입 누락).");
            Equip(slot, itemIndex);
            return front;
        }

        /// <summary>
        /// 이 자리를 걸친 상태로 만든다.
        /// <para>★ <see cref="EquipmentModel.TryWear"/>의 <b>반환값을 단언하지 않는다</b>:
        /// 새 캐릭터의 시작 차림이 <b>모자 0번 + 안경 0번</b>이라(<c>CreateDefaultWorn</c>) 그 둘은
        /// 이미 걸쳐져 있고, 그때 <c>TryWear</c>는 «바뀐 것이 없다»는 뜻으로 <c>false</c>를 돌려준다.
        /// 그 <c>false</c>를 실패로 읽으면 <b>기본 차림인 자리만</b> 빨개진다 — 실제로 이 파일의
        /// 첫 실행에서 Head/Eyes 케이스 14건이 그 이유로 죽었다. 단언은 <b>결과 상태</b>로 한다.</para>
        /// </summary>
        /// <summary>이 자리만 남기고 <b>전부 벗긴다</b>.
        /// <para>새 캐릭터의 기본 차림이 모자 0번 + 안경 0번이라(<c>CreateDefaultWorn</c>), 안 벗기면
        /// 이 파일의 잉크 측정에 <b>선글라스의 벡터 잉크</b>가 섞인다 — 실제로 첫 실행에서
        /// 「잉크 최저 Y」 두 건이 비트맵이 아니라 그 선글라스 값(1.9984)을 재고 있었다.
        /// 이 파일이 재는 것은 «그 비트맵 하나»의 기하다.</para></summary>
        private static void UnequipOthers(EquipmentSlot slot)
        {
            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                var other = (EquipmentSlot)i;
                if (other == slot) continue;
                EquipmentModel.TryWear(other, EquipmentModel.NotWorn, null);
            }
        }

        private static void Equip(EquipmentSlot slot, int itemIndex)
        {
            if (!EquipmentModel.IsEquipped(slot, itemIndex)) EquipmentModel.TryWear(slot, itemIndex, null);
            Assert.IsTrue(EquipmentModel.IsEquipped(slot, itemIndex),
                $"{LogPrefix} {slot} {itemIndex}번을 걸치지 못했습니다 — 이 테스트의 전제가 성립하지 않습니다.");
        }

        private static SpriteRenderer Only(CharacterAccessoryRenderer r)
        {
            IReadOnlyList<SpriteRenderer> sprites = r.WornSpritesForTests;
            Assert.AreEqual(1, sprites.Count,
                $"{LogPrefix} 착용 비트맵이 {sprites.Count}장입니다(1장이어야 합니다). " +
                "0장이면 갈래를 안 탔고(= 벡터로 그려졌고), 2장 이상이면 뒤층이 딸려 왔습니다.");
            Assert.IsNotNull(sprites[0], $"{LogPrefix} 스프라이트 렌더러가 null 입니다.");
            return sprites[0];
        }

        // ============================================================================
        // (0) 전제 — 출하 전량이 비어 있다 + 그 「0」이 죽은 계수기가 아니다
        // ============================================================================

        /// <summary>
        /// ★ <b>선언한 아이템 = 폴더에 PNG가 있는 아이템.</b> 양쪽 어느 방향으로 어긋나도 실패한다.
        ///
        /// <para><b>2026-09-09 P1(왕관 파일럿)에서 바뀐 테스트다.</b> P0 판이 스스로 그렇게 적어
        /// 두었다 — *"P1이라면 이 0을 「PNG 파일 수와 같다」로 바꾸는 것이 맞다. 그냥 지우면
        /// 다음 사고를 못 잡는다."* 지금 그 지시를 이행한다. P0 판의 문장(«전량 0종»)은
        /// 그림이 한 장 들어오는 순간 <b>참이 아니게</b> 되고, 참이 아닌 단언을 남겨 두면
        /// 다음 사람이 그것을 지우면서 <b>대조까지 함께</b> 지운다.</para>
        ///
        /// <para>이 형태가 잡는 것이 P0 판보다 <b>많다</b>:</para>
        /// <list type="number">
        ///   <item><b>PNG는 있는데 배선이 안 됐다</b> — 임포트 도구를 안 돌렸거나 실패했다.
        ///     증상은 «그림을 넣었는데 아무것도 안 바뀐다»이고, 그건 «아직 안 넣었다»와 화면이 같다.</item>
        ///   <item><b>배선은 됐는데 PNG가 없다</b> — 파일을 지웠는데 에셋이 유령 참조를 들고 있다.</item>
        ///   <item><b>뒤층 PNG가 있는데 뒤 칸이 비었다</b>(그 반대도) — 왕관이 정확히 이 형태를 쓴다.</item>
        ///   <item><b>잉크박스를 안 구웠다</b> — 렌더러가 캔버스 전체를 잉크로 보고 캐릭터를
        ///     공중에 띄운다(§19-8-3). 조용한 실패라 눈으로 못 잡는다.</item>
        /// </list>
        ///
        /// <para>★ 폴더/접미사/접두사 문자열은 <b>프로덕션 상수에서 리플렉션으로 읽는다</b> —
        /// 베끼면 도구가 폴더를 옮기는 날 이 테스트가 <b>조용히 0건을 세고 초록이 된다</b>
        /// (CLAUDE.md: 부재 단언은 썩어도 안 빨개진다). 타입을 못 찾으면 <b>그 자리에서 실패</b>한다.</para>
        /// </summary>
        [Test]
        public void 착용_비트맵_선언이_폴더의_PNG와_정확히_일치한다()
        {
            // ---- (a) 프로덕션 상수를 리플렉션으로 — 문자열을 베끼지 않는다 -------------------
            System.Type tool = FindImportToolType();
            Assert.IsNotNull(tool,
                $"{LogPrefix} ★대조 실패 — 착용 비트맵 임포트 도구 타입({ImportToolTypeName})을 못 찾았습니다. " +
                "이 테스트가 폴더 위치를 모르므로 아래 목록은 «비어 있다»와 «못 봤다»를 구분하지 못합니다.");
            string spriteFolder = Const(tool, "SpriteFolder");
            string itemFolder = Const(tool, "ItemFolder");
            string backSuffix = Const(tool, "BackSuffix");
            string probePrefix = Const(tool, "ProbePrefix");

            // ---- (b) 폴더가 말하는 것 ---------------------------------------------------------
            var frontPngs = new HashSet<string>();   // 에셋 이름(확장자·접미사 제거)
            var backPngs = new HashSet<string>();
            string[] files = Directory.Exists(spriteFolder)
                ? Directory.GetFiles(spriteFolder, "*.png", SearchOption.TopDirectoryOnly)
                : new string[0];
            foreach (string f in files)
            {
                string name = Path.GetFileNameWithoutExtension(f);
                if (name.StartsWith(probePrefix, System.StringComparison.Ordinal)) continue;   // 규격 확인용 고정물
                if (name.EndsWith(backSuffix, System.StringComparison.Ordinal))
                    backPngs.Add(name.Substring(0, name.Length - backSuffix.Length));
                else frontPngs.Add(name);
            }

            // 파일 이름 -> 카탈로그 자리. 짝이 없으면 그것부터 결함이다.
            var expectedFront = new Dictionary<string, string>();   // itemId -> PNG 파일 이름
            var missingDefs = new List<string>();
            foreach (string name in frontPngs)
            {
                var def = AssetDatabase.LoadAssetAtPath<AccessoryDefSO>($"{itemFolder}/{name}.asset");
                if (def == null || string.IsNullOrEmpty(def.itemId)) { missingDefs.Add(name); continue; }
                expectedFront[def.itemId] = name;
            }
            Assert.IsEmpty(missingDefs,
                $"{LogPrefix} PNG는 있는데 같은 이름의 아이템 에셋이 없습니다: {string.Join(", ", missingDefs)}");

            // ---- (c) 카탈로그가 말하는 것 -----------------------------------------------------
            var wired = new List<string>();
            var faults = new List<string>();
            int scanned = 0;
            for (int i = 0; i < ItemCatalog.Count; i++)
            {
                ItemCatalogEntry e = ItemCatalog.At(i);
                if (e == null || e.Category != ItemCategory.Equipment) continue;
                if (!e.Slot.HasValue || e.ItemIndex < 0) continue;
                scanned++;

                Sprite front = ItemCatalog.WornSprite(e.Slot.Value, e.ItemIndex);
                Sprite back = ItemCatalog.WornSpriteBack(e.Slot.Value, e.ItemIndex);
                Rect rect = ItemCatalog.WornSpriteRectInR(e.Slot.Value, e.ItemIndex);
                Vector4 ink = ItemCatalog.WornSpriteInkBoxInR(e.Slot.Value, e.ItemIndex);
                bool declares = front != null || back != null || rect != default(Rect) || ink != Vector4.zero;
                if (!declares) continue;
                wired.Add(e.Id);

                if (front == null) faults.Add($"{e.Id}: 앞층이 비었습니다(앞장이 주인이라 뒤층만으로는 벡터로 되돌아갑니다).");
                if (rect.width <= 0f || rect.height <= 0f) faults.Add($"{e.Id}: wornSpriteRectInR 이 비었습니다(렌더러가 머리 지름으로 되메웁니다).");
                if (!WornSpritePlacement.IsInkBoxBaked(ink)) faults.Add($"{e.Id}: 잉크박스를 안 구웠습니다 — 캔버스 여백까지 잉크로 읽혀 캐릭터가 뜹니다(§19-8-3).");

                if (!expectedFront.TryGetValue(e.Id, out string pngName))
                {
                    faults.Add($"{e.Id}: 배선은 있는데 {spriteFolder} 에 PNG가 없습니다(유령 참조).");
                }
                else
                {
                    bool wantBack = backPngs.Contains(pngName);
                    if (wantBack && back == null) faults.Add($"{e.Id}: 뒤층 PNG는 있는데 wornSpriteBackOverride 가 비었습니다.");
                    if (!wantBack && back != null) faults.Add($"{e.Id}: 뒤층 PNG가 없는데 wornSpriteBackOverride 가 채워져 있습니다.");
                }
            }

            Assert.Greater(scanned, 0, $"{LogPrefix} 장비 아이템이 0종입니다 — 순회가 공허합니다.");

            // 양쪽 집합이 같은가.
            var onlyInFolder = new List<string>();
            foreach (KeyValuePair<string, string> kv in expectedFront)
                if (!wired.Contains(kv.Key)) onlyInFolder.Add(kv.Key);
            Assert.IsEmpty(onlyInFolder,
                $"{LogPrefix} PNG는 폴더에 있는데 배선이 안 된 아이템: {string.Join(", ", onlyInFolder)}\n" +
                "임포트 도구를 돌리십시오: -executeMethod StickMate.EditorTools.WornSpriteImport.ApplyBatch");
            Assert.IsEmpty(faults, $"{LogPrefix} 착용 비트맵 배선 결함 {faults.Count}건:\n  " + string.Join("\n  ", faults));
            Assert.AreEqual(expectedFront.Count, wired.Count,
                $"{LogPrefix} 폴더의 앞층 PNG {expectedFront.Count}장과 배선된 아이템 {wired.Count}종이 다릅니다 " +
                $"(폴더: {string.Join(", ", expectedFront.Keys)} / 배선: {string.Join(", ", wired)}).");

            // ★ 양성 대조 — 위 계수가 「표가 통째로 죽어서」 맞아떨어진 것이 아님을 증명한다.
            //   (선언 0종일 때도 이 대조 하나로 목록이 살아 있음이 남는다 — P0 판의 그 장치를 그대로 옮겼다.)
            Sprite probe = Calibration();
            Assert.IsTrue(ItemCatalog.TrySetWornSpriteForTests(EquipmentSlot.Neck, 0, probe, null,
                CalibrationRect, WornSpriteCalibration.ExpectedInkBoxInR(CalibrationRect)),
                $"{LogPrefix} 대조군을 심지 못했습니다 — 위 집합 비교가 아무것도 증명하지 않습니다.");
            Assert.IsNotNull(ItemCatalog.WornSprite(EquipmentSlot.Neck, 0),
                $"{LogPrefix} 심은 뒤에도 WornSprite 가 null 입니다 — 위 계수는 표가 통째로 죽은 결과입니다.");

            ItemCatalog.ClearWornSpriteTestPatches();
            Assert.IsNull(ItemCatalog.WornSprite(EquipmentSlot.Neck, 0),
                $"{LogPrefix} 테스트가 심은 값이 되돌아가지 않았습니다 — 뒤따르는 테스트가 오염됩니다.");

            Debug.Log($"{LogPrefix} 장비 {scanned}종 스캔 — 폴더 앞층 PNG {expectedFront.Count}장 · 뒤층 {backPngs.Count}장 · " +
                      $"배선 {wired.Count}종 (일치). 배선 목록: {(wired.Count == 0 ? "(없음)" : string.Join(", ", wired))}");
        }

        /// <summary>임포트 도구의 <b>타입 이름</b>. 이 어셈블리는 <c>Assembly-CSharp-Editor</c> 를
        /// 참조하지 않으므로(asmdef) 리플렉션이 유일한 통로다. 못 찾으면 위에서 <b>실패</b>한다 —
        /// 존재 단언이라 썩으면 시끄럽게 빨개진다.</summary>
        private const string ImportToolTypeName = "StickMate.EditorTools.WornSpriteImport";

        private static System.Type FindImportToolType()
        {
            foreach (System.Reflection.Assembly a in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type t = a.GetType(ImportToolTypeName, false);
                if (t != null) return t;
            }
            return null;
        }

        private static string Const(System.Type t, string field)
        {
            System.Reflection.FieldInfo fi = t.GetField(field,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(fi, $"{LogPrefix} {t.FullName}.{field} 상수를 못 찾았습니다.");
            var v = fi.GetValue(null) as string;
            Assert.IsFalse(string.IsNullOrEmpty(v), $"{LogPrefix} {t.FullName}.{field} 가 비어 있습니다.");
            return v;
        }

        // ============================================================================
        // (1) 부착 — 뼈를 따라가는가
        // ============================================================================

        /// <summary>
        /// 앵커가 <b>자리에서 유도</b>되는가, 그리고 <b>머리에 붙는 것만</b> 머리 추종 그룹에 들어가는가.
        ///
        /// <para>후자가 이 테스트의 무게중심이다: 목/어깨 아이템이 머리 그룹에 들어가면
        /// 유휴 앰비언트 «주위 살피기»가 머리를 옆으로 밀 때 넥타이가 따라가 <b>목이 늘어난 것처럼</b>
        /// 보인다(2026-08-30 사용자 신고의 거울상). 계층 깊이로 재는 이유는 이름 문자열을
        /// 니들로 베끼지 않기 위해서다(CLAUDE.md).</para>
        /// </summary>
        [TestCase(EquipmentSlot.Head, 0, true)]
        [TestCase(EquipmentSlot.Eyes, 0, true)]
        [TestCase(EquipmentSlot.Neck, 0, false)]
        [TestCase(EquipmentSlot.Shoulders, 0, false)]
        public void 부착_앵커와_추종_그룹이_자리에서_유도된다(EquipmentSlot slot, int item, bool headAttached)
        {
            Wear(slot, item);
            CharacterAccessoryRenderer r = Rig(1f, +1f);
            r.RebuildForTests();

            SpriteRenderer sr = Only(r);
            var metrics = r.GetComponent<StickmanMetrics>();
            Assert.IsTrue(metrics.MeasuredFromHierarchy,
                $"{LogPrefix} 리그가 폴백 비율로 되메워졌습니다 — 아래 앵커 비교가 뜻을 잃습니다.");

            float rr = metrics.HeadRadius;
            float expectedAnchorY = headAttached ? metrics.HeadCenterLocalY : metrics.ShoulderLocalY;
            float expectedCenterY = expectedAnchorY + CalibrationRect.y * rr;
            float expectedCenterX = CalibrationRect.x * rr;   // facing +1

            Assert.AreEqual(expectedCenterX, sr.transform.position.x, Tol,
                $"{LogPrefix} {slot}: 비트맵 중심 x 가 앵커에서 유도되지 않았습니다.");
            Assert.AreEqual(expectedCenterY, sr.transform.position.y, Tol,
                $"{LogPrefix} {slot}: 비트맵 중심 y 가 {(headAttached ? "머리 중심" : "어깨선")}에서 " +
                "유도되지 않았습니다 — 자리마다 앵커가 다르다는 규칙이 깨졌습니다.");

            // 계층 깊이 — 머리 추종 그룹은 컨테이너의 자식이므로 한 단계 더 깊다.
            int depth = 0;
            for (Transform t = sr.transform.parent; t != null && t != r.transform; t = t.parent) depth++;
            Assert.AreEqual(headAttached ? 2 : 1, depth,
                $"{LogPrefix} {slot}: 부모 계층 깊이가 {depth}입니다(기대 {(headAttached ? 2 : 1)}). " +
                "머리에 붙는 자리만 머리 추종 그룹에 들어가야 합니다.");

            // ★ 「따라간다」를 직접 잰다 — 추종 그룹을 밀면 비트맵이 <b>같은 만큼</b> 따라와야 한다.
            Transform group = sr.transform.parent;
            Vector3 before = sr.transform.position;
            group.localPosition += new Vector3(0.137f, 0f, 0f);
            Assert.AreEqual(before.x + 0.137f, sr.transform.position.x, Tol,
                $"{LogPrefix} {slot}: 부착 그룹을 밀었는데 비트맵이 따라오지 않았습니다.");
        }

        // ============================================================================
        // (2) 배율 — 0.35 / 0.75 / 1.00
        // ============================================================================

        /// <summary>
        /// 크기와 자리가 배율에 <b>정확히 비례</b>하는가. 두 가지 배율 경로를 <b>둘 다</b> 본다:
        /// <list type="number">
        ///   <item>리그 치수 자체가 작게 구워진 경우(씬이 배율 s로 구워진 상태).</item>
        ///   <item>루트 <c>localScale</c>이 s인 경우(크기 다이얼) — 컨테이너가 그 배율을
        ///     <b>상쇄</b>하므로(이 렌더러의 「이중 스케일」 문단) 두 번 곱해지면 s² 로 커진다.
        ///     실제로 그 사고가 있었다(루트 2.6667에서 모자가 2.839배로 떠올랐다).</item>
        /// </list>
        /// 하한 0.35를 반드시 포함한다 — 거기서 몸 위 모자 폭이 1080p에서 9px이고, 벡터를 지켜 주던
        /// 획 하한(<see cref="StickConfig.MinAccessoryStrokeScreenPoints"/>)이 비트맵에는 <b>없다</b>(§19-1).
        /// </summary>
        [TestCase(0.35f)]
        [TestCase(0.75f)]
        [TestCase(1.00f)]
        public void 크기와_자리가_배율에_정확히_비례한다(float scale)
        {
            Wear(EquipmentSlot.Head, 0);

            CharacterAccessoryRenderer full = Rig(1f, +1f);
            full.RebuildForTests();
            Bounds b1 = Only(full).bounds;
            Vector3 p1 = Only(full).transform.position;

            CharacterAccessoryRenderer bakedSmall = Rig(scale, +1f);
            bakedSmall.RebuildForTests();
            Bounds b2 = Only(bakedSmall).bounds;
            Vector3 p2 = Only(bakedSmall).transform.position;

            CharacterAccessoryRenderer dialSmall = Rig(1f, +1f, rootScale: scale);
            dialSmall.RebuildForTests();
            Bounds b3 = Only(dialSmall).bounds;
            Vector3 p3 = Only(dialSmall).transform.position;

            AssertScaled(b1.size.x, scale, b2.size.x, $"배율 {scale:F2}(리그)", "비트맵 월드 폭");
            AssertScaled(b1.size.y, scale, b2.size.y, $"배율 {scale:F2}(리그)", "비트맵 월드 높이");
            AssertScaled(p1.y, scale, p2.y, $"배율 {scale:F2}(리그)", "비트맵 중심 y");

            AssertScaled(b1.size.x, scale, b3.size.x, $"배율 {scale:F2}(다이얼)", "비트맵 월드 폭");
            AssertScaled(b1.size.y, scale, b3.size.y, $"배율 {scale:F2}(다이얼)", "비트맵 월드 높이");
            AssertScaled(p1.y, scale, p3.y, $"배율 {scale:F2}(다이얼)", "비트맵 중심 y");
        }

        /// <summary>배율 1.0에서의 <b>절대</b> 크기 — 위 비례 검사가 「둘 다 0이라 비례한다」로
        /// 통과하지 못하게 하는 대조다. 기대값은 <c>rect × 머리 반경</c> 정의 그대로다.</summary>
        [Test]
        public void 배율_1에서_월드_크기가_배치_사각형_정의와_같다()
        {
            Wear(EquipmentSlot.Head, 0);
            CharacterAccessoryRenderer r = Rig(1f, +1f);
            r.RebuildForTests();

            float rr = r.GetComponent<StickmanMetrics>().HeadRadius;
            Bounds b = Only(r).bounds;
            Assert.Greater(b.size.x, 0f, $"{LogPrefix} 비트맵 폭이 0입니다 — 위 비례 검사가 공허해집니다.");
            Assert.AreEqual(CalibrationRect.width * rr, b.size.x, Tol, $"{LogPrefix} 월드 폭");
            Assert.AreEqual(CalibrationRect.height * rr, b.size.y, Tol, $"{LogPrefix} 월드 높이");
        }

        // ============================================================================
        // (3) z-order — 슬롯 규칙 그대로
        // ============================================================================

        /// <summary>
        /// <see cref="Renderer.sortingOrder"/>가 <b>자리 표</b>와 같은가. 값을 숫자로 베끼지 않고
        /// <c>AccessoryShapeBuilder</c>의 같은 함수에 <b>물어본다</b> — 표가 움직이면 벡터와 비트맵이
        /// 함께 움직여야 하고, 여기에 사본을 두면 그 둘이 갈라진다(§19-7-b).
        /// </summary>
        [TestCase(EquipmentSlot.Head, 0)]
        [TestCase(EquipmentSlot.Eyes, 0)]
        [TestCase(EquipmentSlot.Neck, 0)]
        [TestCase(EquipmentSlot.Shoulders, 0)]
        public void z_order가_자리_표를_그대로_따른다(EquipmentSlot slot, int item)
        {
            Wear(slot, item);
            CharacterAccessoryRenderer r = Rig(1f, +1f);
            r.RebuildForTests();

            Assert.IsTrue(AccessoryShapeBuilder.TryWornAssetSlotOrder(slot, out int expected),
                $"{LogPrefix} {slot}: 자리 표가 이 자리를 모릅니다 — 기대값의 출처가 없습니다.");
            Assert.AreEqual(expected, Only(r).sortingOrder,
                $"{LogPrefix} {slot}: sortingOrder 가 자리 표와 다릅니다.");
        }

        /// <summary>
        /// 아이템 하나가 <b>층 두 개</b>를 쓸 때 뒤층이 몸 뒤로 간다.
        /// <para>실측상 인계본 12종 중 <b>6종</b>이 서로 다른 <c>sortingOrder</c> 두 개를 쓴다
        /// (모자 3 + 망토·배낭 3, §19-7-c). 이게 틀리면 <b>왕관 뒷테가 머리 앞에</b> 그려지고,
        /// 그것이 P1 파일럿을 왕관으로 고른 이유다.</para>
        /// </summary>
        [Test]
        public void 뒤층은_몸_뒤로_앞층은_자리_표대로_간다()
        {
            Sprite back = Calibration();
            Wear(EquipmentSlot.Head, 0, back);
            CharacterAccessoryRenderer r = Rig(1f, +1f);
            r.RebuildForTests();

            IReadOnlyList<SpriteRenderer> sprites = r.WornSpritesForTests;
            Assert.AreEqual(2, sprites.Count,
                $"{LogPrefix} 뒤층을 선언했는데 스프라이트가 {sprites.Count}장입니다(2장이어야 합니다).");

            Assert.IsTrue(AccessoryShapeBuilder.TryWornAssetSlotOrder(EquipmentSlot.Head, out int frontOrder));
            Assert.AreEqual(AccessoryShapeBuilder.SortBack, sprites[0].sortingOrder,
                $"{LogPrefix} 뒤층이 몸 뒤(SortBack)로 가지 않았습니다.");
            Assert.AreEqual(frontOrder, sprites[1].sortingOrder,
                $"{LogPrefix} 앞층이 자리 표를 따르지 않았습니다.");
            Assert.Less(sprites[0].sortingOrder, sprites[1].sortingOrder,
                $"{LogPrefix} 뒤층이 앞층보다 앞에 있습니다 — 왕관 뒷테가 머리 앞에 그려집니다.");
        }

        /// <summary>★ 앞층이 비면 뒤층만 있어도 <b>아무 일도 일어나지 않는다</b>(앞장이 주인이다).
        /// 이 갈래가 없으면 「뒤만 있는」 오타가 <b>몸 뒤에 그림 한 장</b>으로 조용히 나타난다.</summary>
        [Test]
        public void 앞층이_비면_뒤층만_있어도_벡터로_돌아간다()
        {
            Sprite back = Calibration();
            Assert.IsTrue(ItemCatalog.TrySetWornSpriteForTests(EquipmentSlot.Head, 0, null, back,
                CalibrationRect, default));
            Equip(EquipmentSlot.Head, 0);

            CharacterAccessoryRenderer r = Rig(1f, +1f);
            r.RebuildForTests();
            Assert.AreEqual(0, r.WornSpritesForTests.Count,
                $"{LogPrefix} 앞층이 비어 있는데 스프라이트가 그려졌습니다.");
        }

        // ============================================================================
        // (4) 좌우 반전
        // ============================================================================

        /// <summary>
        /// 진행 방향이 바뀌면 <c>flipX</c>와 <b>중심 오프셋 부호</b>가 함께 뒤집힌다.
        /// <para>둘 중 하나만 뒤집히는 것이 실제로 위험한 상태다 — 그림은 뒤집혔는데 자리가 그대로면
        /// 왼쪽으로 걸을 때 챙이 뒤통수에서 튀어나온다(이 저장소가 벡터에서 두 번 겪은 사고).</para>
        /// </summary>
        [Test]
        public void 좌우_반전이_그림과_자리를_함께_뒤집는다()
        {
            Wear(EquipmentSlot.Head, 0);

            CharacterAccessoryRenderer right = Rig(1f, +1f);
            right.RebuildForTests();
            SpriteRenderer sr = Only(right);
            float rr = right.GetComponent<StickmanMetrics>().HeadRadius;

            Assert.IsFalse(sr.flipX, $"{LogPrefix} 오른쪽을 볼 때 flipX 가 켜져 있습니다.");
            Assert.AreEqual(CalibrationRect.x * rr, sr.transform.position.x, Tol,
                $"{LogPrefix} 오른쪽을 볼 때 중심 x 가 기대와 다릅니다.");

            CharacterAccessoryRenderer left = Rig(1f, -1f);
            left.RebuildForTests();
            SpriteRenderer flipped = Only(left);

            Assert.IsTrue(flipped.flipX,
                $"{LogPrefix} 왼쪽을 볼 때 flipX 가 꺼져 있습니다 — 비대칭 그림이 안 뒤집힙니다.");
            Assert.AreEqual(-CalibrationRect.x * rr, flipped.transform.position.x, Tol,
                $"{LogPrefix} 왼쪽을 볼 때 중심 x 가 안 뒤집혔습니다 — 그림만 뒤집히고 자리는 그대로입니다.");
            Assert.AreEqual(-sr.transform.position.x, flipped.transform.position.x, Tol,
                $"{LogPrefix} 두 방향의 중심 x 가 서로 반대 부호가 아닙니다.");
        }

        /// <summary>★ 네거티브 컨트롤 — 「반전을 빼먹었다면」 위 단언이 <b>실제로</b> 깨진다.
        /// <see cref="CalibrationRect"/>의 x 오프셋이 0이면 두 방향의 x가 같아져 위 검사가
        /// 아무것도 못 잡는다. 그 사실을 같은 파일에서 못 박는다.</summary>
        [Test]
        public void 반전_검사는_x_오프셋이_0이_아니어야_뜻을_갖는다()
        {
            Assert.AreNotEqual(0f, CalibrationRect.x,
                $"{LogPrefix} 교정 사각형의 x 오프셋이 0입니다 — 위 반전 검사가 통째로 공허해집니다.");
            Assert.AreNotEqual(CalibrationRect.width, CalibrationRect.height,
                $"{LogPrefix} 교정 사각형이 정사각입니다 — 가로세로를 바꿔 넣어도 통과합니다.");
        }

        // ============================================================================
        // (5) 잉크 타이트 박스 — 투명 여백이 안 섞이는가
        // ============================================================================

        /// <summary>
        /// ★★ <c>TryGetLowestInkWorldY</c>(GETUP 바닥 클리어런스)가 <b>알파 박스</b>를 쓰는가.
        ///
        /// <para>교정 그림은 사방 <see cref="WornSpriteCalibration.MarginFraction"/>(=25%)가
        /// 투명이다. 캔버스 사각형을 그대로 쓰면 최저 Y가 <b>캔버스 아래 모서리</b>에서 나오고,
        /// 그 차이만큼 GETUP이 캐릭터를 <b>공중에 띄운다</b>(§19-8-3). 두 값을 <b>같은 실행에서</b>
        /// 계산해 대조하므로 「우연히 같아서 통과」가 없다.</para>
        /// </summary>
        [Test]
        public void 잉크_최저Y가_캔버스가_아니라_알파_박스에서_나온다()
        {
            Wear(EquipmentSlot.Head, 0);
            CharacterAccessoryRenderer r = Rig(1f, +1f);
            r.RebuildForTests();
            r.ApplyGlobalAlphaForTests(1f);

            SpriteRenderer sr = Only(r);
            float rr = r.GetComponent<StickmanMetrics>().HeadRadius;
            float anchorY = r.GetComponent<StickmanMetrics>().HeadCenterLocalY;

            Assert.IsTrue(r.TryGetLowestInkWorldY(out float lowest),
                $"{LogPrefix} 잉크 최저 Y를 못 냈습니다 — 비트맵이 이 계산에 아예 안 들어갔습니다.");

            Vector4 expectedBox = WornSpriteCalibration.ExpectedInkBoxInR(CalibrationRect);
            float expectedInkBottom = anchorY + expectedBox.y * rr;
            float canvasBottom = anchorY + (CalibrationRect.y - CalibrationRect.height * 0.5f) * rr;

            Assert.AreEqual(expectedInkBottom, lowest, Tol,
                $"{LogPrefix} 잉크 최저 Y가 알파 박스 아래변과 다릅니다.");
            Assert.Greater(lowest, canvasBottom + Tol,
                $"{LogPrefix} 잉크 최저 Y가 캔버스 아래변({canvasBottom:F4})까지 내려갔습니다 — " +
                "투명 여백을 잉크로 세고 있습니다(캐릭터가 그만큼 공중에 뜹니다).");

            // 대조 — 여백이 실제로 존재하는 그림인지 같은 실행에서 확인한다.
            Assert.AreEqual(CalibrationRect.height * rr * WornSpriteCalibration.MarginFraction,
                lowest - canvasBottom, Tol,
                $"{LogPrefix} 잉크 아래변과 캔버스 아래변의 간격이 교정 여백과 다릅니다 — " +
                "위 «크다» 단언이 우연일 수 있습니다.");

            // 스프라이트의 bounds 는 여전히 캔버스 전체다(그래서 bounds 를 안 쓴다).
            Assert.LessOrEqual(sr.bounds.min.y, lowest + Tol,
                $"{LogPrefix} 전제 확인 실패 — 스프라이트 bounds 가 잉크 박스보다 위에 있습니다.");
        }

        /// <summary>알파 박스를 <b>안 구웠으면</b> 사각형 전체를 잉크로 본다 — 「모른다」일 때
        /// 안전한 방향(여유를 더 주는 쪽)으로 틀린다. 반대로 했다면 안 구운 아이템이 바닥을 뚫는다.</summary>
        [Test]
        public void 알파_박스를_안_구웠으면_사각형_전체를_잉크로_본다()
        {
            Sprite front = Calibration();
            Assert.IsTrue(ItemCatalog.TrySetWornSpriteForTests(EquipmentSlot.Head, 0, front, null,
                CalibrationRect, default));
            UnequipOthers(EquipmentSlot.Head);
            Equip(EquipmentSlot.Head, 0);

            CharacterAccessoryRenderer r = Rig(1f, +1f);
            r.RebuildForTests();
            r.ApplyGlobalAlphaForTests(1f);

            var metrics = r.GetComponent<StickmanMetrics>();
            Assert.IsTrue(r.TryGetLowestInkWorldY(out float lowest));
            float canvasBottom = metrics.HeadCenterLocalY
                + (CalibrationRect.y - CalibrationRect.height * 0.5f) * metrics.HeadRadius;
            Assert.AreEqual(canvasBottom, lowest, Tol,
                $"{LogPrefix} 안 구운 박스에서 사각형 전체를 잉크로 보지 않았습니다.");
        }

        // ============================================================================
        // (6) ★★ 숨김 — 절대 불변 원칙 2
        // ============================================================================

        /// <summary>
        /// ★★ 비트맵이 <b>「캐릭터가 지금 그리는 것」의 단일 창구</b>에 신고되는가.
        ///
        /// <para>이 창구(<see cref="CharacterVisualRegistry"/>)가
        /// <c>StickmanAgent.SetRenderersEnabled(false)</c> — 전체화면 게임 자동 숨김과 가출 은신이
        /// 함께 쓰는 그 함수 — 가 <b>몸 바깥의 잉크에 닿는 유일한 통로</b>다. 여기 안 들어가면
        /// 전체화면 게임 위에 비트맵 모자만 남고 기존 <see cref="LineRenderer"/> 프로브 40개는
        /// 전부 초록이다(§19-8-1).</para>
        ///
        /// <para><b>양성 대조</b>: 스프라이트를 안 심은 상태에서 같은 계수기가 0을 내는지 먼저 재고,
        /// 심은 뒤 1이 되는지 잰다. 그 두 값이 다르지 않으면 이 테스트는 아무것도 증명하지 않는다.</para>
        /// </summary>
        [Test]
        public void 착용_비트맵이_숨김_창구에_신고된다()
        {
            // (가) 대조군 — 비트맵 없이 같은 아이템(벡터)으로 구웠을 때 스프라이트는 0개다.
            Equip(EquipmentSlot.Head, 0);
            CharacterAccessoryRenderer vector = Rig(1f, +1f);
            vector.RebuildForTests();
            Assert.AreEqual(0, CountSprites(vector),
                $"{LogPrefix} 벡터 경로인데 창구에 스프라이트가 신고됐습니다 — 대조가 성립하지 않습니다.");
            Assert.Greater(CountAll(vector), 0,
                $"{LogPrefix} 벡터 경로가 아무것도 신고하지 않았습니다 — 대조군이 죽었습니다.");

            // (나) 실험군 — 같은 자리에 비트맵을 심으면 정확히 1장이 신고된다.
            EquipmentModel.ResetForTesting();
            Wear(EquipmentSlot.Head, 0);
            CharacterAccessoryRenderer bitmap = Rig(1f, +1f);
            bitmap.RebuildForTests();
            Assert.AreEqual(1, CountSprites(bitmap),
                $"{LogPrefix} 착용 비트맵이 숨김 창구에 신고되지 않았습니다 — " +
                "전체화면 게임 위에 몸 없는 모자만 남습니다(절대 불변 원칙 2 위반).");
        }

        /// <summary>
        /// ★★ 숨김 요청이 실제로 <c>SpriteRenderer.enabled == false</c>를 만드는가.
        ///
        /// <para><c>SetVisualsEnabledForTests</c>는 프로덕션의 <c>SetLinesEnabled</c>를 그대로 부른다 —
        /// 알파가 0으로 내려간 프레임(랙돌 · 전체화면 억제)에 <see cref="CharacterAccessoryRenderer"/>가
        /// 스스로 부르는 바로 그 함수다. 그리고 <b>전역 페이드</b>도 함께 잰다: 둘 중 하나만 걸리면
        /// 「꺼졌는데 색이 남는다」 또는 「투명한데 켜져 있다」가 되고, 두 증상은 다르다.</para>
        /// </summary>
        [Test]
        public void 숨기면_비트맵의_enabled와_알파가_함께_내려간다()
        {
            Wear(EquipmentSlot.Head, 0);
            CharacterAccessoryRenderer r = Rig(1f, +1f);
            r.RebuildForTests();
            r.ApplyGlobalAlphaForTests(1f);

            SpriteRenderer sr = Only(r);
            Assert.IsTrue(sr.enabled,
                $"{LogPrefix} 굽자마자 꺼져 있습니다 — 아래 «꺼진다» 단언이 공허해집니다.");
            Assert.AreEqual(1f, sr.color.a, Tol, $"{LogPrefix} 굽자마자 투명합니다 — 대조가 성립하지 않습니다.");

            r.SetVisualsEnabledForTests(false);
            Assert.IsFalse(sr.enabled,
                $"{LogPrefix} ★ 숨김 요청 뒤에도 SpriteRenderer.enabled 가 true 입니다 — " +
                "전체화면 게임 위에 비트맵이 남습니다(절대 불변 원칙 2 위반).");

            r.ApplyGlobalAlphaForTests(0f);
            Assert.AreEqual(0f, sr.color.a, Tol,
                $"{LogPrefix} 전역 페이드가 비트맵에 걸리지 않았습니다 — 랙돌 페이드에서 모자만 남습니다.");

            r.SetVisualsEnabledForTests(true);
            r.ApplyGlobalAlphaForTests(1f);
            Assert.IsTrue(sr.enabled, $"{LogPrefix} 다시 켜지지 않았습니다 — 돌아오는 문이 없습니다.");
            Assert.AreEqual(1f, sr.color.a, Tol, $"{LogPrefix} 알파가 되돌아오지 않았습니다.");
        }

        /// <summary>다 벗으면 비트맵 목록도 함께 비워지는가 — 안 비우면 파괴된 스프라이트가
        /// 목록에 남아 계층을 훑는 계측기에게 <b>모델과 다른 사실</b>을 말한다.</summary>
        [Test]
        public void 다_벗으면_비트맵_목록이_비워진다()
        {
            Wear(EquipmentSlot.Head, 0);
            CharacterAccessoryRenderer r = Rig(1f, +1f);
            r.RebuildForTests();
            Assert.AreEqual(1, r.WornSpritesForTests.Count, $"{LogPrefix} 전제가 성립하지 않습니다.");

            EquipmentModel.TryWear(EquipmentSlot.Head, EquipmentModel.NotWorn, null);
            r.RebuildForTests();
            Assert.AreEqual(0, r.WornSpritesForTests.Count,
                $"{LogPrefix} 다 벗었는데 비트맵 목록에 {r.WornSpritesForTests.Count}장이 남아 있습니다.");
        }

        /// <summary>
        /// ★ <b>【미해결 · 교차 레이어 · P1 착수 전에 닫아야 함】</b>
        /// 잉크 <b>최저 Y</b>는 알파 박스를 쓰는데(위 테스트), <b>시각 반폭</b>은 아직
        /// <c>Renderer.bounds</c>를 그대로 쓴다.
        ///
        /// <para>소비자가 이 렌더러 바깥에 있다 — <c>Core/StickmanAgent</c>의 화면 여백 계산이
        /// <c>_dynamicVisuals</c>를 훑으면서 <c>Line == null</c>인 항목은 <c>r.bounds</c>로 잰다.
        /// 채움 면(MeshRenderer)의 bounds 는 실제 정점에서 나오므로 그 규칙이 옳았는데,
        /// <b>스프라이트는 투명 여백까지 포함</b>한다. 증상은 「캐릭터가 화면 끝에서 헛되이 밀려난다」이고
        /// <b>그림이 실제로 들어오는 P1부터</b> 나타난다(지금은 42종이 전부 벡터라 발생 경로가 없다).</para>
        ///
        /// <para>이 라운드에서 안 고친 이유: game-architect가 지정한 배선 5곳(§19-8-1)은 전부 이
        /// 렌더러 안이고, <c>StickmanAgent</c>를 고치는 것은 <b>다른 레이어</b>다 — 동시 진행 라운드가
        /// 그 파일을 잡고 있을 수 있어 리더를 거쳐야 한다(CLAUDE.md 동시 진행 규칙).
        /// <c>Assert.Fail</c>이 아니라 <c>Assert.Ignore</c>인 이유도 같다: 지금 빨간 것은 없고,
        /// <b>잊히지 않게</b> 러너에 계속 「건너뜀」으로 보이게 두는 것이 목적이다.</para>
        /// </summary>
        [Test]
        public void 시각_반폭이_아직_알파박스를_안_본다_미해결()
        {
            Wear(EquipmentSlot.Head, 0);
            CharacterAccessoryRenderer r = Rig(1f, +1f);
            r.RebuildForTests();
            r.ApplyGlobalAlphaForTests(1f);

            SpriteRenderer sr = Only(r);
            float rr = r.GetComponent<StickmanMetrics>().HeadRadius;
            Vector4 box = WornSpriteCalibration.ExpectedInkBoxInR(CalibrationRect);

            float boundsHalfWidth = Mathf.Max(Mathf.Abs(sr.bounds.max.x), Mathf.Abs(sr.bounds.min.x));
            float inkHalfWidth = Mathf.Max(Mathf.Abs(box.z * rr), Mathf.Abs(box.x * rr));
            float overshoot = boundsHalfWidth / Mathf.Max(1e-6f, inkHalfWidth);

            // ==== 역방향 장치 ①: 잉크 범위 계약이 넓어졌는가 = 처방된 고침이 착지했는가 ====
            //   먼저 <b>양성 대조</b> — 이 계수기가 실제로 계약을 보고 있는지 확인한다.
            //   니들은 nameof 라 멤버 이름이 바뀌면 문자열이 썩는 대신 <b>컴파일이 깨진다</b>.
            var contractMethods = typeof(ICharacterInkExtentProvider).GetMethods();
            bool sawKnownMember = false;
            for (int i = 0; i < contractMethods.Length; i++)
            {
                if (contractMethods[i].Name == nameof(ICharacterInkExtentProvider.TryGetLowestInkWorldY))
                {
                    sawKnownMember = true;
                }
            }
            Assert.IsTrue(sawKnownMember,
                $"{LogPrefix} 잉크 범위 계약에서 알려진 멤버를 못 찾았습니다 — 아래 «넓어졌는가» 판정이 " +
                "계수기가 죽은 결과일 수 있습니다.");
            Assert.AreEqual(1, contractMethods.Length,
                $"{LogPrefix} ★ 축하할 실패 — 잉크 범위 계약이 {contractMethods.Length}개로 넓어졌습니다. " +
                "처방된 고침(반폭을 이 계약으로 나르기)이 착지했다는 뜻이므로 " +
                "이 테스트의 Assert.Ignore 를 걷고 실단언으로 승격하고, " +
                "TestClaimExpiryAuditTests 의 Ignore 명부에서도 이 항목을 지우세요.");

            // ==== 역방향 장치 ②: bounds 자체가 타이트해졌는가 ====
            //   (Unity 가 Tight 메시의 Sprite.bounds 를 실제로 줄이게 되는 날 — §19-12-2 미확인 항목)
            Assert.Greater(overshoot, 1.05f,
                $"{LogPrefix} ★ 축하할 실패 — 스프라이트 bounds 가 잉크 박스와 거의 같아졌습니다" +
                $"({overshoot:F3}배). 갭이 닫혔으니 이 Ignore 를 걷고 실단언으로 승격하세요.");

            Assert.Ignore(
                "【미해결 · 교차 레이어】 시각 반폭(Core/StickmanAgent 의 화면 여백 계산)이 " +
                "착용 비트맵을 bounds 로 잰다 — 투명 여백이 섞인다.\n" +
                $"  교정 그림(사방 여백 {WornSpriteCalibration.MarginFraction:P0})에서 실측: " +
                $"bounds 반폭 {boundsHalfWidth:F4} vs 잉크 반폭 {inkHalfWidth:F4} = " +
                $"{overshoot:F2}배 과대.\n" +
                "  지금은 42종이 전부 벡터라 발생 경로가 없다. 그림이 들어오는 P1(왕관) 전에 닫아야 한다.\n" +
                "  고치는 자리: StickmanAgent 의 _dynamicVisuals 순회에서 스프라이트 항목만 " +
                "CharacterAccessoryRenderer 가 신고하는 잉크 박스를 읽게 한다(ICharacterInkExtentProvider 확장).");
        }

        // ============================================================================
        // 배치 규약 순수 함수 — 리그 없이 재는 자리
        // ============================================================================

        [Test]
        public void 크기를_안_적으면_머리_지름_폭으로_되메운다()
        {
            Rect resolved = WornSpritePlacement.ResolveRectInR(new Rect(0.4f, -0.2f, 0f, 0f), 0.5f);
            Assert.AreEqual(WornSpritePlacement.DefaultWidthInR, resolved.width, Tol);
            Assert.AreEqual(WornSpritePlacement.DefaultWidthInR * 0.5f, resolved.height, Tol,
                "그림 비율(높이/너비)이 유지되지 않았습니다.");
            Assert.AreEqual(0.4f, resolved.x, Tol, "중심 오프셋까지 덮어쓰면 안 됩니다.");

            Rect declared = new Rect(0f, 0f, 3f, 1f);
            Assert.AreEqual(declared, WornSpritePlacement.ResolveRectInR(declared, 9f),
                "적어 둔 크기를 되메움이 덮어썼습니다.");
        }

        [Test]
        public void 잉크박스_변환이_정규화_좌표를_사각형_좌표로_옮긴다()
        {
            Vector4 box = WornSpritePlacement.InkBoxFromNormalized(CalibrationRect, 0.25f, 0.25f, 0.75f, 0.75f);
            Assert.AreEqual(CalibrationRect.x - CalibrationRect.width * 0.25f, box.x, Tol);
            Assert.AreEqual(CalibrationRect.y - CalibrationRect.height * 0.25f, box.y, Tol);
            Assert.AreEqual(CalibrationRect.x + CalibrationRect.width * 0.25f, box.z, Tol);
            Assert.AreEqual(CalibrationRect.y + CalibrationRect.height * 0.25f, box.w, Tol);

            Assert.IsTrue(WornSpritePlacement.IsInkBoxBaked(box));
            Assert.IsFalse(WornSpritePlacement.IsInkBoxBaked(default),
                "기본값을 «구운 박스»로 읽으면 안 구운 아이템의 잉크가 0이 됩니다.");
            Assert.IsFalse(WornSpritePlacement.IsInkBoxBaked(new Vector4(1f, 1f, 0f, 0f)),
                "뒤집힌 박스를 «구웠다»로 읽으면 잉크 범위가 음수가 됩니다.");
        }

        /// <summary>교정 그림의 알파가 <b>실제로</b> 이진이고 정중앙 50%에만 있는가.
        /// 이 그림이 규약을 어기면 위 (5)의 기대값이 통째로 거짓말이 된다.</summary>
        [Test]
        public void 교정_그림의_알파가_이진이고_사방_25퍼센트가_투명하다()
        {
            Texture2D tex = WornSpriteCalibration.BuildTexture();
            _assets.Add(tex);

            Color32[] px = tex.GetPixels32();
            int w = tex.width;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            int partial = 0;
            for (int y = 0; y < tex.height; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    byte a = px[y * w + x].a;
                    if (a == 0) continue;
                    if (a != 255) partial++;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            Assert.AreEqual(0, partial, "교정 그림에 반투명 픽셀이 있습니다 — 저작 규격(α는 0 또는 255)과 어긋납니다.");
            Assert.AreEqual(WornSpriteCalibration.InkMinNormalized, minX / (float)w, Tol, "잉크 좌측");
            Assert.AreEqual(WornSpriteCalibration.InkMinNormalized, minY / (float)tex.height, Tol, "잉크 하단");
            Assert.AreEqual(WornSpriteCalibration.InkMaxNormalized, (maxX + 1) / (float)w, Tol, "잉크 우측");
            Assert.AreEqual(WornSpriteCalibration.InkMaxNormalized, (maxY + 1) / (float)tex.height, Tol, "잉크 상단");
        }

        // ============================================================================
        // 임포트 규격 — <b>.meta 를 직접 읽는다</b>
        // ============================================================================

        /// <summary>
        /// ★★ 규격이 «코드에 적혀 있다»가 아니라 «<c>.meta</c>에 실제로 걸렸다»를 잰다.
        ///
        /// <para>어제 라운드에서 정확히 이 부류의 결함이 났다 — <c>platformSettings</c>에
        /// <c>maxTextureSize: 256</c>이 <b>적혀 있는데</b> <c>overridden: 0</c>이라 최상위 2048이
        /// 지배하고 있었다. 임포터 API에 물으면 「요청」이 나오고, <c>.meta</c>를 읽으면 「결과」가 나온다.
        /// <b>둘 다</b> 본다.</para>
        ///
        /// <para>확인용 PNG를 그 자리에서 만들고 지우는 이유: 이 라운드의 정상 상태가 <b>그림 0장</b>이라
        /// 기존 파일을 읽는 방식은 «검사할 것이 없어 초록»이 된다. 그리고 이 방식은
        /// <b>후처리기가 자동으로 도는지</b>까지 함께 증명한다 — 사람이 메뉴를 눌러야 하는 구조였다면
        /// 「누르는 것을 잊는」 경로가 영원히 남았을 것이다.</para>
        /// </summary>
        [Test]
        public void 착용_비트맵_임포트_규격이_meta에_실제로_걸린다()
        {
            const string folder = "Assets/_Project/Art/WornSprites";
            string assetPath = folder + "/__probe_editmode_wornsprite.png";
            string metaPath = assetPath + ".meta";

            Directory.CreateDirectory(folder);
            Texture2D tex = WornSpriteCalibration.BuildTexture();
            try
            {
                File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }

            try
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

                var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                Assert.IsNotNull(ti,
                    $"{LogPrefix} TextureImporter 를 못 얻었습니다 — 확인용 PNG 가 임포트되지 않았습니다.");

                // ---- (가) 「요청」 — 임포터에게 묻는다 -----------------------------------
                Assert.AreEqual(TextureImporterType.Sprite, ti.textureType,
                    $"{LogPrefix} textureType 이 Sprite 가 아닙니다 — 스프라이트 하위 자산이 생기지 않습니다.");
                Assert.AreEqual(FilterMode.Bilinear, ti.filterMode, $"{LogPrefix} filterMode");
                Assert.IsTrue(ti.mipmapEnabled,
                    $"{LogPrefix} 밉맵이 꺼져 있습니다 — 몸에서 5~13배 축소라 선이 끊깁니다.");
                Assert.IsTrue(ti.alphaIsTransparency,
                    $"{LogPrefix} alphaIsTransparency 가 꺼져 있습니다 — 가장자리에 검은 테가 돕니다.");
                Assert.AreEqual(SpriteImportMode.Single, ti.spriteImportMode, $"{LogPrefix} spriteMode");
                Assert.AreEqual(SpriteMeshType.Tight, GetMeshType(ti), $"{LogPrefix} spriteMeshType");
                Assert.AreEqual(256, ti.maxTextureSize,
                    $"{LogPrefix} maxTextureSize 가 256이 아닙니다(§19-8-4의 상주 메모리 근거).");

                TextureImporterPlatformSettings sa = ti.GetPlatformTextureSettings("Standalone");
                TextureImporterPlatformSettings ios = ti.GetPlatformTextureSettings("iPhone");
                Assert.IsTrue(sa.overridden,
                    $"{LogPrefix} Standalone 오버라이드가 꺼져 있습니다 — BC7 이 안 걸립니다.");
                Assert.IsTrue(ios.overridden,
                    $"{LogPrefix} iOS 오버라이드가 꺼져 있습니다 — ASTC 가 안 걸립니다(모바일 상주 메모리).");
                Assert.AreEqual(TextureImporterFormat.BC7, sa.format, $"{LogPrefix} Standalone 포맷");
                Assert.AreEqual(TextureImporterFormat.ASTC_6x6, ios.format, $"{LogPrefix} iOS 포맷");

                // ---- (나) 「결과」 — .meta 텍스트를 직접 읽는다 --------------------------
                Assert.IsTrue(File.Exists(metaPath), $"{LogPrefix} .meta 파일이 없습니다: {metaPath}");
                string meta = File.ReadAllText(metaPath);

                AssertPlatformOverridden(meta, "Standalone");
                // .meta 의 직렬화 이름은 iOS, API 이름은 iPhone 이다 — 둘 다 받아들인다.
                AssertPlatformOverridden(meta, "iOS", "iPhone");

                StringAssert.Contains("enableMipMap: 1", meta, $"{LogPrefix} .meta 에 밉맵이 안 걸렸습니다.");
                StringAssert.Contains("alphaIsTransparency: 1", meta,
                    $"{LogPrefix} .meta 에 alphaIsTransparency 가 안 걸렸습니다.");
                StringAssert.Contains("spriteMeshType: 1", meta,
                    $"{LogPrefix} .meta 의 spriteMeshType 이 Tight(1)가 아닙니다.");
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.Refresh();
                Assert.IsFalse(File.Exists(assetPath),
                    $"{LogPrefix} 확인용 PNG 를 지우지 못했습니다 — 저장소에 찌꺼기가 남습니다: {assetPath}");
            }
        }

        private static SpriteMeshType GetMeshType(TextureImporter importer)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            return settings.spriteMeshType;
        }

        /// <summary><c>.meta</c>의 <c>platformSettings</c> 블록 중 이 플랫폼의 것을 찾아
        /// <c>overridden: 1</c>과 <c>maxTextureSize: 256</c>이 <b>같은 블록 안에</b> 있는지 본다.
        /// 파일 전체에서 문자열을 찾으면 <b>다른 플랫폼 블록의 값</b>이 대신 잡힌다.</summary>
        private static void AssertPlatformOverridden(string meta, params string[] buildTargetNames)
        {
            int at = -1;
            string used = null;
            for (int i = 0; i < buildTargetNames.Length && at < 0; i++)
            {
                used = "buildTarget: " + buildTargetNames[i];
                at = meta.IndexOf(used, System.StringComparison.Ordinal);
            }
            Assert.Greater(at, -1,
                $"{LogPrefix} .meta 에 {string.Join("/", buildTargetNames)} 플랫폼 블록이 없습니다.");

            int end = meta.IndexOf("  - serializedVersion", at, System.StringComparison.Ordinal);
            if (end < 0) end = meta.IndexOf("  spriteSheet:", at, System.StringComparison.Ordinal);
            if (end < 0) end = meta.Length;
            string block = meta.Substring(at, end - at);

            StringAssert.Contains("overridden: 1", block,
                $"{LogPrefix} ★ {used} 블록이 overridden: 0 입니다 — 값이 적혀 있어도 적용되지 않습니다. " +
                "어제 라운드에서 실제로 난 결함과 같은 형태입니다.\n블록:\n" + block);
            StringAssert.Contains("maxTextureSize: 256", block,
                $"{LogPrefix} {used} 블록의 maxTextureSize 가 256이 아닙니다.\n블록:\n" + block);
        }

        // ============================================================================
        // 유틸
        // ============================================================================

        private static int CountSprites(CharacterAccessoryRenderer r)
        {
            var registry = new CharacterVisualRegistry();
            registry.BindSources(new ICharacterVisualSource[] { r });
            registry.Refresh();
            int n = 0;
            for (int i = 0; i < registry.Count; i++)
            {
                if (registry[i].Renderer is SpriteRenderer) n++;
            }
            return n;
        }

        private static int CountAll(CharacterAccessoryRenderer r)
        {
            var registry = new CharacterVisualRegistry();
            registry.BindSources(new ICharacterVisualSource[] { r });
            registry.Refresh();
            return registry.Count;
        }

        private static void AssertScaled(float baseValue, float scale, float actual, string label, string what)
        {
            Assert.AreEqual(baseValue * scale, actual, Mathf.Max(Tol, Mathf.Abs(baseValue * scale) * 1e-4f),
                $"{LogPrefix} {label}: {what} 가 배율에 비례하지 않습니다.");
        }
    }
}
