// 팔다리 폴리라인 덤프 — Unity를 띄우지 않고 <b>프로덕션 States/LimbCurveRenderer.cs 를 그대로</b> 실행한다.
//
// 왜: 「마디 병합이 조형을 바꾸지 않는다」는 주장은 <b>좌표가 같다</b>는 뜻이고, 그것은 말이 아니라
// 숫자로만 증명된다. 이 도구는 같은 굽힘각에서 (1) 병합 이후 렌더러의 9점, (2) 병합 이전 경로가
// 만들던 5+5점, (3) 초상화 경로, (4) 펫 경로를 전부 뽑아 stdout 으로 흘린다. 대조는 compare.py 가 한다.
//
// 입력(stdin, 탭 구분):
//   CASE<TAB>이름<TAB>Lu<TAB>Ll<TAB>jx<TAB>jy<TAB>각도<TAB>획<TAB>리그모드<TAB>구워진점(x,y;… 없으면 -)
//     리그모드: bake(선 2점 + 콜라이더) / merged(선 9점) / legacy(위 5점 + 아래 5점, 구워진점에 위9|아래5 아님 —
//               legacy 는 "위5;아래5" 를 세미콜론 목록 하나로 주고 앞 5개가 위, 뒤 5개가 아래다)
using System;
using UnityEngine;
using StickMate.States;

internal static class LimbDump
{
    private static string P(Vector3 v) => $"{v.x:R},{v.y:R}";
    private static string Join(Vector3[] a, int from, int count)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = from; i < from + count; i++) { if (i > from) sb.Append(';'); sb.Append(P(a[i])); }
        return sb.ToString();
    }

    private static Vector3[] ParsePoints(string s)
    {
        if (string.IsNullOrEmpty(s) || s == "-") return new Vector3[0];
        string[] parts = s.Split(';');
        var r = new Vector3[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            string[] xy = parts[i].Split(',');
            r[i] = new Vector3(float.Parse(xy[0]), float.Parse(xy[1]), 0f);
        }
        return r;
    }

    private static int Main()
    {
        string line;
        while ((line = Console.In.ReadLine()) != null)
        {
            if (line.Length == 0 || !line.StartsWith("CASE")) continue;
            string[] f = line.Split('\t');
            string name = f[1];
            float lu = float.Parse(f[2]), ll = float.Parse(f[3]);
            float jx = float.Parse(f[4]), jy = float.Parse(f[5]);
            float ang = float.Parse(f[6]), w = float.Parse(f[7]);
            string mode = f[8];
            Vector3[] baked = ParsePoints(f.Length > 9 ? f[9] : "-");

            // ── 리그 조립 ────────────────────────────────────────────────
            var rootGo = new GameObject("Stickman");
            var upperGo = new GameObject("LeftLeg");
            var lowerGo = new GameObject("LeftLegLower");
            rootGo.transform.AddChild(upperGo.transform);
            upperGo.transform.AddChild(lowerGo.transform);
            lowerGo.transform.localPosition = new Vector3(jx, jy, 0f);
            lowerGo.transform.localRotation = Quaternion.EulerZ(ang);

            var upperLine = upperGo.transform.Add(new LineRenderer());
            upperLine.startWidth = w; upperLine.endWidth = w;
            var box = lowerGo.transform.Add(new BoxCollider2D());
            box.size = new Vector2(w, ll);

            if (mode == "bake")
            {
                upperLine.positionCount = 2;
                upperLine.SetPosition(0, Vector3.zero);
                upperLine.SetPosition(1, new Vector3(0f, -lu, 0f));
            }
            else if (mode == "merged")
            {
                upperLine.positionCount = baked.Length;
                for (int i = 0; i < baked.Length; i++) upperLine.SetPosition(i, baked[i]);
            }
            else // legacy — 위 5점 + 아래 마디 선 5점
            {
                upperLine.positionCount = 5;
                for (int i = 0; i < 5; i++) upperLine.SetPosition(i, baked[i]);
                var lowerLine = lowerGo.transform.Add(new LineRenderer());
                lowerLine.startWidth = w; lowerLine.endWidth = w;
                lowerLine.positionCount = 5;
                for (int i = 0; i < 5; i++) lowerLine.SetPosition(i, baked[5 + i]);
            }

            // ★ 순서가 중요하다 — 병합 <b>이전</b> 코드가 읽던 길이는 <b>굽기 전</b> 선에서 나온다.
            //   구운 뒤에 읽으면 자기가 방금 쓴 값을 되읽어 대조가 무의미해진다(하니스 자체 함정).
            float luRead = Math.Abs(upperLine.positionCount >= 2
                ? upperLine.GetPosition(Math.Min(upperLine.positionCount - 1, LimbCurveRenderer.PointsPerSegment - 1)).y
                : 0f);
            var preLower = lowerGo.transform.Get<LineRenderer>();
            float llRead = preLower != null
                ? Math.Abs(preLower.GetPosition(Math.Min(preLower.positionCount - 1, LimbCurveRenderer.PointsPerSegment - 1)).y)
                : ll;

            var renderer = rootGo.transform.Add(new LimbCurveRenderer());
            renderer.BakeEditorPreview();

            var got = new Vector3[upperLine.positionCount];
            for (int i = 0; i < got.Length; i++) got[i] = upperLine.GetPosition(i);
            Console.WriteLine($"NEW\t{name}\t{mode}\t{Join(got, 0, got.Length)}");

            var legacyLine = lowerGo.transform.Get<LineRenderer>();
            Console.WriteLine($"LEGACYLINE_ENABLED\t{name}\t{mode}\t" +
                (legacyLine == null ? "none" : legacyLine.enabled.ToString()));

            // ── 병합 <b>이전</b> 경로가 만들던 것 — 같은 프로덕션 static 두 개로 재구성 ──
            //    (옛 BuildCurve 가 문자 그대로 이 두 줄이었다)
            float t = LimbCurveRenderer.SolveFilletLength(luRead, llRead, ang, w);
            var up = new Vector3[LimbCurveRenderer.PointsPerSegment];
            var lo = new Vector3[LimbCurveRenderer.PointsPerSegment];
            LimbCurveRenderer.FillArcs(luRead, llRead, ang, t, up, 0, lo, 0);
            Console.WriteLine($"OLDUPPER\t{name}\t{mode}\t{Join(up, 0, up.Length)}");
            Console.WriteLine($"OLDLOWER\t{name}\t{mode}\t{Join(lo, 0, lo.Length)}");
            Console.WriteLine($"READ\t{name}\t{mode}\t{luRead:R}\t{llRead:R}\t{t:R}");

            // ── 초상화 경로(같은 Lu/Ll/각도로) ──────────────────────────
            var portrait = new Vector3[LimbCurveRenderer.PolylinePointCount];
            int n = LimbCurveRenderer.BuildLimbPolyline(luRead, llRead, ang, w, portrait);
            Console.WriteLine($"PORTRAIT\t{name}\t{mode}\t{Join(portrait, 0, n)}");

            // ── 펫 경로(뿌리→끝점 고정) ────────────────────────────────
            var pet = new Vector3[LimbCurveRenderer.PolylinePointCount];
            Vector3 tip = got[got.Length - 1];
            int m = LimbCurveRenderer.BuildLimbPolylineBetween(Vector3.zero, tip, ang,
                luRead / (luRead + llRead), w, pet);
            Console.WriteLine($"PET\t{name}\t{mode}\t{Join(pet, 0, m)}");
        }
        return 0;
    }
}
