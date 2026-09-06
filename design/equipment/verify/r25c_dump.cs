using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

// R25c 진단 전용 덤프 (design-equipment · 2026-09-06) — r25c_build.sh 가 이 파일을 컴파일한다.
//
// Tools/ShapeDump/Dump.cs 와 하는 일이 같되 <b>조각마다 IsHandoff 를 함께 찍는다</b>.
// AppearanceShapeBudgetTests.최단_실제_변_검사를_액세서리_30종으로_확장한다 가
// `if (sink[i].IsHandoff) continue;` 로 인계본 조각을 건너뛰므로, 그 플래그 없이는
// 그 검사의 숫자를 오프라인에서 재현할 수 없다.
//
// ★ 검사 로직은 여기 한 줄도 없다. 좌표만 뽑고 판정은 r25c_shortedge.py 가 한다 —
//   자를 두 벌 만들면 두 자가 갈라지고, 그 순간 어느 쪽이 옳은지 아무도 모른다.
// ★ 아이템 순서는 AccessoryStrokeBudgetTests.BudgetedItems 와 같다(HAIR -> HEAD -> NECK -> EYES -> BACK).
//   러너 메시지의 «첫 줄»이 무엇인지가 오진을 갈랐던 라운드가 있어서 순서를 맞춰 둔다.
public static class Dump2
{
    static AccessoryShapeBuilder.Rig MakeRig()
    {
        const float H = StickConfig.BaselineCharacterTotalHeight;
        const float R = AccessoryShapeBuilder.BaselineHeadVisualRadius;
        return new AccessoryShapeBuilder.Rig(R, H - R,
            AccessoryShapeBuilder.BaselineShoulderLocalY,
            AccessoryShapeBuilder.BaselineHipLocalY, 1f);
    }

    public static void Main()
    {
        var rig = MakeRig();
        float R = rig.HeadRadius, HC = rig.HeadCenterY;
        var inv = CultureInfo.InvariantCulture;

        // AccessoryStrokeBudgetTests.BudgetedItems 와 같은 순서/집합
        var keys = new List<(string cat, EquipmentSlot slot, int item)>();
        for (int i = 0; i < 6; i++) keys.Add(("HAIR", EquipmentSlot.Hair, i));
        for (int i = 0; i < 6; i++) keys.Add(("HEAD", EquipmentSlot.Head, i));
        for (int i = 0; i < 6; i++) keys.Add(("NECK", EquipmentSlot.Neck, i));
        for (int i = 0; i < 6; i++) keys.Add(("EYES", EquipmentSlot.Eyes, i));
        for (int i = 0; i < 6; i++) keys.Add(("BACK", EquipmentSlot.Shoulders, i));

        var sink = new List<AccessoryShapeBuilder.Shape>();
        foreach (var k in keys)
        {
            sink.Clear();
            AccessoryShapeBuilder.Append(sink, k.slot, k.item, rig);
            Console.WriteLine($"@ITEM\t{k.cat}\t{k.item}\t{ItemCatalog.Item(k.slot, k.item).DisplayName}\t{sink.Count}");
            foreach (var s in sink)
            {
                Console.Write($"@SHAPE\t{s.Name}\t{(s.Loop ? 1 : 0)}\t{(s.IsHandoff ? 1 : 0)}\t{(s.Filled ? 1 : 0)}");
                foreach (var p in s.Points)
                {
                    Console.Write("\t" + ((p.x / R)).ToString("R", inv) + "," + (((p.y - HC) / R)).ToString("R", inv));
                }
                Console.WriteLine();
            }
        }
        for (int i = 0; i < 6; i++)
        {
            float y = AccessoryShapeBuilder.HatCoverLocalY(i, rig);
            string v = float.IsPositiveInfinity(y) ? "inf" : ((y - HC) / R).ToString("R", inv);
            Console.WriteLine($"@COVER\t{i}\t{v}");
        }
        Console.WriteLine("@W\t" + AccessoryShapeBuilder.ShippingStrokeBudgetInHeadRadii.ToString("R", inv));
        Console.WriteLine($"@LOG\t{Debug.ErrorCount}\t{Debug.WarningCount}");
    }
}
