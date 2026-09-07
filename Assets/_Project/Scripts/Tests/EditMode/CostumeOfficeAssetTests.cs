using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <c>costume.office</c> <b>실물 에셋</b>이 실제로 실리는가 —
    /// 좌표·박자의 정본은 <c>docs/UX_MOTION_COSTUME_FOCUS.md</c> 5-2절이고, 이 파일은 그 표를
    /// <b>디스크의 에셋에서 되읽어</b> 대조한다.
    ///
    /// ============================================================================
    /// 이 파일이 있는 이유 — 「에셋이 폴더에 있다」는 「앱이 싣는다」가 아니다
    /// ============================================================================
    /// <see cref="CostumeCatalog"/>가 훑는 폴더는 <b><c>Resources/Items</c></b>이고
    /// (<c>ItemCatalog.ItemResourceFolder</c>를 그대로 쓴다) 다른 폴더에 두면 <b>조용히 안 실린다</b> —
    /// 그 화면은 「기능 미구현」과 완전히 같아 보인다. 이 테스트가 그 침묵을 깬다.
    ///
    /// <para>그리고 <b>이 코스튬이 실리는 순간부터 L-1~L-5를 잴 수 있다</b> — 그 전까지는
    /// 계기가 0을 내는데 「절감됐다」인지 「경로가 안 돈다」인지 구별되지 않았다.</para>
    /// </summary>
    public sealed class CostumeOfficeAssetTests
    {
        private const string LogPrefix = "[코스튬오피스]";

        /// <summary>세이브에 앉는 키. 바꾸면 사용자의 누적 시간이 다른 코스튬 것이 되므로
        /// <b>여기 적힌 것이 곧 계약</b>이다(에셋과 이 문자열이 갈라지면 아래 ①이 빨개진다).</summary>
        private const string OfficeCostumeKey = "costume.office";

        // ---- 설계 5-2절에서 손으로 옮긴 골든 ----
        private const int GoldenShapeCountS1 = 10;
        private const int GoldenShapeCountS0 = 4;
        private const float GoldenNearEdgeH = 0.26f;
        private const float GoldenFarEdgeH = 1.00f;
        private const float GoldenTopH = 1.02f;
        private const int GoldenKeyCount = 3;

        /// <summary>(A θu, A θl, B θu, B θl, lean, dY) — 설계 5-2 「서류 넘기기」 3키.
        /// A = 앞팔(<c>NeutralSign &gt; 0</c> = right), B = 뒷팔(left).</summary>
        private static readonly float[,] GoldenKeys =
        {
            { 46.0f, 66.0f, -26.0f, 22.0f, -2.0f,  0.000f },
            { 47.5f, 27.5f, -26.0f, 22.0f,  2.5f, -0.018f },
            { 40.0f, 78.0f, -22.0f, 26.0f,  1.0f,  0.000f },
        };

        private static CostumeDescriptor Office()
        {
            CostumeCatalog.ResetForTesting();   // 폴더를 실제로 다시 훑게 한다.
            CostumeDescriptor office = CostumeCatalog.Find(OfficeCostumeKey);
            Assert.IsNotNull(office,
                $"{LogPrefix} '{OfficeCostumeKey}'가 실리지 않았습니다. CostumeCatalog가 훑는 폴더는 " +
                "Resources/Items 하나이고(ItemCatalog.ItemResourceFolder), 다른 폴더에 두면 " +
                "조용히 안 실립니다 — 그 화면은 「기능 미구현」과 구별되지 않습니다. " +
                "결함이 있으면 로드가 LogError로 사유를 남겼을 것입니다.");
            return office;
        }

        [Test]
        public void 오피스_코스튬이_실제로_실린다()
        {
            CostumeDescriptor office = Office();

            Assert.AreEqual(CostumeSourceKind.BaseTheme, office.SourceKind,
                $"{LogPrefix} 오피스는 기본 코호트 테마에서 나온다(무료 — 엔타이틀먼트를 «안 묻는다»).");
            Assert.AreEqual(ItemCatalog.ThemeOffice, office.SourceId,
                $"{LogPrefix} sourceId는 ItemCatalog의 테마 키와 글자까지 같아야 한다 — " +
                "갈라지면 해석기가 «어떤 차림으로도 성립하지 않는» 코스튬을 싣는다.");
            Assert.LessOrEqual(office.ManifestSchemaVersion, CostumeManifestSO.SchemaVersion,
                $"{LogPrefix} 이 앱이 모르는 스키마를 요구하면 반쯤 읽힌다.");
            Assert.AreEqual(CostumeManifestSO.SchemaVersion, office.ManifestSchemaVersion,
                $"{LogPrefix} keyposes를 쓰는 코스튬은 현재 스키마를 요구해야 한다 — " +
                "낮게 적으면 구버전 앱이 전용 모션만 조용히 빼고 싣는다.");

            // ★ 음성 대조 — 같은 조회기가 없는 키에 대해 실제로 null을 낸다(계기 생존 확인).
            Assert.IsNull(CostumeCatalog.Find("costume.no_such_costume_zz"),
                $"{LogPrefix} 없는 키가 null이 아니면 위 ①의 «찾았다»는 아무것도 증명하지 못한다.");

            Debug.Log($"{LogPrefix} ① 통과 — 실린 코스튬 {CostumeCatalog.Count}종, " +
                      $"'{office.CostumeKey}' 스키마 v{office.ManifestSchemaVersion}.");
        }

        [Test]
        public void 프롭_조각이_설계_기하와_같다()
        {
            CostumeDescriptor office = Office();
            IReadOnlyList<AccessoryWornShapeData> shapes = office.PropShapes;

            Assert.AreEqual(GoldenShapeCountS1, shapes.Count,
                $"{LogPrefix} 설계 5-2의 소환 오브젝트는 S1 기준 {GoldenShapeCountS1}선이다.");

            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
            for (int i = 0; i < shapes.Count; i++)
            {
                Assert.IsTrue(AccessoryWornShapeReader.Validate(shapes[i], out string error),
                    $"{LogPrefix} 조각 '{shapes[i].name}'의 좌표 스트림이 문법에 안 맞는다: {error}");

                // H = 1인 프레임이라 좌표가 곧 H 배수다.
                Assert.IsTrue(AccessoryWornShapeReader.TryBuild(shapes[i], AccessoryWornFrame.Unit,
                    false, out Vector3[] pts, out error), $"{LogPrefix} '{shapes[i].name}' 해석 실패: {error}");
                for (int p = 0; p < pts.Length; p++)
                {
                    minX = Mathf.Min(minX, pts[p].x);
                    maxX = Mathf.Max(maxX, pts[p].x);
                    maxY = Mathf.Max(maxY, pts[p].y);
                }
            }

            Assert.AreEqual(GoldenNearEdgeH, minX, 0.0005f, $"{LogPrefix} 근단이 설계와 다르다.");
            Assert.AreEqual(GoldenFarEdgeH, maxX, 0.0005f, $"{LogPrefix} 원단이 설계와 다르다.");
            Assert.AreEqual(GoldenTopH, maxY, 0.0005f, $"{LogPrefix} 높이가 설계와 다르다.");
            Assert.AreEqual(0f, office.PropAnchorOffsetXInH, 0.0001f,
                $"{LogPrefix} 좌표가 이미 캐릭터 루트 기준이라 앵커 오프셋은 0이다 " +
                "(0이 아니면 필요폭이 설계값 1.10 H와 어긋난다).");

            // 단계 0은 스탠드 4 + 칸막이 홈 2를 뺀 4선이다(설계 문장에서 그대로 유도).
            IReadOnlyList<CostumeStageOverride> stages = office.StageShapes;
            bool sawStage0 = false;
            for (int i = 0; i < stages.Count; i++)
            {
                if (stages[i].stage != 0) continue;
                sawStage0 = true;
                Assert.AreEqual(GoldenShapeCountS0, stages[i].shapes.Length,
                    $"{LogPrefix} 단계 0은 {GoldenShapeCountS0}선이다.");
            }
            Assert.IsTrue(sawStage0, $"{LogPrefix} 단계 0 조형이 없다 — 진화 첫 칸이 S1과 같아진다.");

            Debug.Log($"{LogPrefix} ② 통과 — 조각 {shapes.Count}개, 근단 {minX:F2} / 원단 {maxX:F2} / " +
                      $"높이 {maxY:F2} H, 필요폭 {maxX + 0.10f:F2} H.");
        }

        [Test]
        public void 키포즈_표가_설계_각도표와_같다()
        {
            CostumeDescriptor office = Office();
            CostumeKeyposeTableSO table = office.Keyposes;

            Assert.IsNotNull(table,
                $"{LogPrefix} 전용 모션 표가 매니페스트에 연결돼 있지 않다 — 프롭만 서고 캐릭터는 " +
                "기존 관망 자세로 남는다(그 화면은 「모션 미구현」과 구별되지 않는다).");
            Assert.IsTrue(table.IsUsable(out string error), $"{LogPrefix} 표가 거부됐다: {error}");
            Assert.AreEqual(CostumeFocusRhythm.RestingStepsPerSecond, table.stepsPerSecond,
                $"{LogPrefix} 오피스는 «쉬는» 스텝레이트로 돈다(절정 소구간만 빠른 값으로 올라간다).");
            Assert.AreEqual(GoldenKeyCount, table.keyposes.Length,
                $"{LogPrefix} 설계 5-2의 「서류 넘기기」는 {GoldenKeyCount}키다(리더 판정 12-1: 무료도 전용 모션).");

            for (int k = 0; k < GoldenKeyCount; k++)
            {
                CostumeKeypose key = table.keyposes[k];
                Assert.AreEqual(GoldenKeys[k, 0], key.rightUpperArm, 0.001f, $"{LogPrefix} K{k} A 어깨각.");
                Assert.AreEqual(GoldenKeys[k, 1], key.rightLowerArm, 0.001f, $"{LogPrefix} K{k} A 팔꿈치.");
                Assert.AreEqual(GoldenKeys[k, 2], key.leftUpperArm, 0.001f, $"{LogPrefix} K{k} B 어깨각.");
                Assert.AreEqual(GoldenKeys[k, 3], key.leftLowerArm, 0.001f, $"{LogPrefix} K{k} B 팔꿈치.");
                Assert.AreEqual(GoldenKeys[k, 4], key.leanDegrees, 0.001f, $"{LogPrefix} K{k} 기울임.");
                Assert.AreEqual(GoldenKeys[k, 5], key.bodyOffsetY, 0.0001f, $"{LogPrefix} K{k} dY(H 배수).");
                Assert.AreEqual(0, key.propFrame,
                    $"{LogPrefix} K{k}의 propFrame이 0이 아니다 — 오피스 프롭은 움직이는 부분이 없어 " +
                    "합격선 P-1(SetPositions 정확히 0)이 문자 그대로 참이어야 한다.");
            }
            Debug.Log($"{LogPrefix} ③ 통과 — {GoldenKeyCount}키 × 6값 대조 + propFrame 전부 0.");
        }

        /// <summary>
        /// 해석기가 <b>성립할 수 있는가</b> — 오피스 4부위가 스탯 4슬롯에 실재해야 한다.
        /// 없으면 코스튬이 영원히 안 뜨고, 그 화면은 「기능 미구현」과 같아 보인다.
        /// </summary>
        [Test]
        public void 오피스_4부위가_스탯_4슬롯에_실재한다()
        {
            EquipmentSlot[] statSlots =
            {
                EquipmentSlot.Head, EquipmentSlot.Eyes, EquipmentSlot.Neck, EquipmentSlot.Shoulders,
            };

            var found = new List<string>();
            foreach (EquipmentSlot slot in statSlots)
            {
                bool any = false;
                int count = ItemCatalog.ItemCountIn(slot);
                for (int i = 0; i < count; i++)
                {
                    ItemCatalogEntry entry = ItemCatalog.Item(slot, i);
                    if (entry == null || entry.Theme != ItemCatalog.ThemeOffice) continue;
                    any = true;
                    found.Add($"{slot}:{entry.Id}");
                }
                Assert.IsTrue(any,
                    $"{LogPrefix} {slot} 자리에 office 테마 아이템이 없다 — 4부위가 다 있어야 " +
                    "CostumeResolver가 이 코스튬을 돌려줄 수 있다(신규 아이템 0개가 이 라운드의 전제였다).");
            }
            Assert.AreEqual(statSlots.Length, found.Count,
                $"{LogPrefix} 자리마다 정확히 하나여야 한다(둘이면 어느 쪽을 입어도 세트가 완성돼 " +
                $"의도가 흐려진다). 실측: {string.Join(", ", found)}");
            Debug.Log($"{LogPrefix} ④ 통과 — {string.Join(" / ", found)}");
        }
    }
}
