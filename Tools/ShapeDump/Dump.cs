using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;

public static class Dump
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
        var cats = new (string cat, EquipmentSlot slot, string[] names)[]
        {
            ("HEAD", EquipmentSlot.Head, new[]{"야구모자","털모자","중절모","왕관","베레모","밀짚모자"}),
            ("EYES", EquipmentSlot.Eyes, new[]{"선글라스","동그란안경","고글","외알안경","뿔테안경","안대"}),
            ("NECK", EquipmentSlot.Neck, new[]{"나비넥타이","줄무늬타이","목도리","방울목걸이","펜던트","반다나"}),
            ("BACK", EquipmentSlot.Shoulders, new[]{"짧은망토","긴망토","날개","배낭","판초","요정날개"}),
            ("HAIR", EquipmentSlot.Hair, new[]{"삐친머리","단정한머리","곱슬머리","민머리","바가지머리","포니테일"}),
        };

        var inv = CultureInfo.InvariantCulture;
        var sink = new List<AccessoryShapeBuilder.Shape>();
        foreach (var c in cats)
        {
            for (int i = 0; i < c.names.Length; i++)
            {
                sink.Clear();
                AccessoryShapeBuilder.Append(sink, c.slot, i, rig);
                Console.WriteLine($"@ITEM\t{c.cat}\t{c.names[i]}");
                foreach (var s in sink)
                {
                    Console.Write($"@SHAPE\t{s.Name}\t{(s.Loop ? 1 : 0)}\t{(s.Filled ? 1 : 0)}\t{s.Tone}\t{s.SortingOrder}");
                    foreach (var p in s.Points)
                    {
                        // 머리 중심 원점 · R 배수로 되돌린다(진행 방향 +x, facing=+1).
                        Console.Write("\t" + ((p.x / R)).ToString("R", inv) + "," + (((p.y - HC) / R)).ToString("R", inv));
                    }
                    Console.WriteLine();
                }
            }
        }
        // 모자 커버선(HatCoverLocalY)도 같은 단위로.
        for (int i = 0; i < 6; i++)
        {
            float y = AccessoryShapeBuilder.HatCoverLocalY(i, rig);
            string v = float.IsPositiveInfinity(y) ? "inf" : ((y - HC) / R).ToString("R", inv);
            Console.WriteLine($"@COVER\t{i}\t{v}");
        }
        Console.WriteLine("@W\t" + AccessoryShapeBuilder.ShippingStrokeBudgetInHeadRadii.ToString("R", inv));

        // ====================================================================
        // ★ 등급 전수 덤프 (2026-09-02) — ItemCatalog.Rarity 를 <b>프로덕션 그대로</b> 부른다.
        //   기대값을 여기서 다시 계산하지 않는다(계산기를 두 벌 만들면 둘이 같이 틀린다).
        //   코호트 배선 같은 변경의 전/후를 이 줄들의 diff 로 판정한다 — 0줄이면 등급이 안 움직였다.
        // ====================================================================
        for (int s = 0; s < EquipmentModel.SlotCount; s++)
        {
            var slot = (EquipmentSlot)s;
            int n = ItemCatalog.ItemCountIn(slot);
            for (int i = 0; i < n; i++)
            {
                ItemCatalogEntry e = ItemCatalog.Item(slot, i);
                if (e == null)
                {
                    Console.WriteLine($"@RARITY\t{EquipmentModel.SlotCode(slot)}\t{i}\t(빈자리)\t-\t-");
                    continue;
                }
                Console.WriteLine($"@RARITY\t{EquipmentModel.SlotCode(slot)}\t{i}\t{e.Id}\t" +
                    $"Lv{e.RequiredLevel}\t{ItemCatalog.RarityName(ItemCatalog.Rarity(slot, i))}");
            }
        }

        // 로그는 버리지 않는다 — 이 하니스가 반쪽만 재고 있는지를 이 두 숫자가 알린다.
        Console.WriteLine($"@LOG\t{Debug.ErrorCount}\t{Debug.WarningCount}");

        // 모자 6 × 머리 6 = 36조합. 프로덕션의 실제 클립 코드를 그대로 돈다.
        float strokeHalf = AccessoryShapeBuilder.BaselineStrokeWidth * 0.5f;
        string[] hats = {"야구모자","털모자","중절모","왕관","베레모","밀짚모자"};
        string[] hairs = {"삐친머리","단정한머리","곱슬머리","민머리","바가지머리","포니테일"};
        for (int h = 0; h < 6; h++)
        {
            float cover = AccessoryShapeBuilder.HatCoverLocalY(h, rig);
            for (int k = 0; k < 6; k++)
            {
                sink.Clear();
                AccessoryShapeBuilder.Append(sink, EquipmentSlot.Hair, k, rig, cover, strokeHalf);
                float top = float.NegativeInfinity;
                foreach (var s in sink) foreach (var p in s.Points) if (p.y > top) top = p.y;
                string topR = sink.Count == 0 ? "-" : ((top - HC) / R).ToString("F3", inv);
                Console.WriteLine($"@CLIP\t{hats[h]}\t{hairs[k]}\t{sink.Count}\t{topR}");
            }
        }
    }
}
