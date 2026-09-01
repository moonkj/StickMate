// 임시 조사 스크립트 — 셰이더 AA 라운드 갈림길 실험. 결과 확인 후 삭제한다.
using System.Text;
using UnityEditor;
using UnityEngine;

namespace StickMate.EditorTools
{
    public static class TempLineUvProbe
    {
        private const string Tag = "[UV조사]";

        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{Tag} ===== LineRenderer BakeMesh UV 덤프 =====");

            var camGo = new GameObject("ProbeCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;

            // 케이스 1: 2점 직선 + 둥근 캡 8
            Dump(sb, "직선2점 cap8 corner8", cam, lr =>
            {
                lr.positionCount = 2;
                lr.SetPosition(0, new Vector3(0f, 0f, 0f));
                lr.SetPosition(1, new Vector3(1f, 0f, 0f));
                lr.numCapVertices = 8;
                lr.numCornerVertices = 8;
            });

            // 케이스 2: 꺾인 3점 (코너 팬 확인)
            Dump(sb, "꺾임3점 cap8 corner8", cam, lr =>
            {
                lr.positionCount = 3;
                lr.SetPosition(0, new Vector3(0f, 0f, 0f));
                lr.SetPosition(1, new Vector3(1f, 0f, 0f));
                lr.SetPosition(2, new Vector3(1f, 1f, 0f));
                lr.numCapVertices = 8;
                lr.numCornerVertices = 8;
            });

            // 케이스 3: cap0 (대조군 — 순수 리본이면 uv.y가 0/1만 나와야 한다)
            Dump(sb, "직선2점 cap0 corner0", cam, lr =>
            {
                lr.positionCount = 2;
                lr.SetPosition(0, new Vector3(0f, 0f, 0f));
                lr.SetPosition(1, new Vector3(1f, 0f, 0f));
                lr.numCapVertices = 0;
                lr.numCornerVertices = 0;
            });

            // 케이스 4: 프로젝트 실제값 — loop 링(머리)
            Dump(sb, "링 loop cap8 corner8", cam, lr =>
            {
                int n = 24;
                lr.positionCount = n;
                for (int i = 0; i < n; i++)
                {
                    float a = (i / (float)n) * Mathf.PI * 2f;
                    lr.SetPosition(i, new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, 0f));
                }
                lr.loop = true;
                lr.numCapVertices = 8;
                lr.numCornerVertices = 8;
            });

            Object.DestroyImmediate(camGo);
            Debug.Log(sb.ToString());
            System.IO.File.WriteAllText("/tmp/stickmate_uvprobe.txt", sb.ToString());
        }

        private static void Dump(StringBuilder sb, string label, Camera cam, System.Action<LineRenderer> setup)
        {
            var go = new GameObject("ProbeLine");
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.startWidth = 0.2f;
            lr.endWidth = 0.2f;
            lr.alignment = LineAlignment.View;
            setup(lr);

            var mesh = new Mesh();
            lr.BakeMesh(mesh, cam, false);

            Vector3[] v = mesh.vertices;
            Vector2[] uv = mesh.uv;
            sb.AppendLine($"{Tag} --- {label} : 정점={v.Length} uv={uv.Length} 삼각형={mesh.triangles.Length / 3} " +
                $"halfWidth={lr.startWidth * 0.5f:F4} textureMode={lr.textureMode} alignment={lr.alignment}");

            // 캡 중심 후보: 선의 두 끝점
            Vector3 endA = lr.GetPosition(0);
            Vector3 endB = lr.GetPosition(lr.positionCount - 1);

            float half = lr.startWidth * 0.5f;
            int shown = 0;
            for (int i = 0; i < v.Length && shown < 80; i++, shown++)
            {
                Vector2 t = i < uv.Length ? uv[i] : Vector2.zero;
                float d = Mathf.Abs(t.y - 0.5f) * 2f;
                float rA = Vector3.Distance(v[i], endA) / half;
                float rB = Vector3.Distance(v[i], endB) / half;
                sb.AppendLine($"{Tag}   [{i,3}] pos=({v[i].x,8:F4},{v[i].y,8:F4}) uv=({t.x,7:F4},{t.y,7:F4}) " +
                    $"d=|uvy-.5|*2={d,6:F4}  r/half(끝A)={rA,7:F4} r/half(끝B)={rB,7:F4}");
            }
            if (v.Length > 80) sb.AppendLine($"{Tag}   ... (총 {v.Length}개 중 80개만 표시)");

            // uv.y 고유값 집계
            var set = new System.Collections.Generic.SortedSet<float>();
            for (int i = 0; i < uv.Length; i++) set.Add(Mathf.Round(uv[i].y * 10000f) / 10000f);
            sb.Append($"{Tag}   uv.y 고유값({set.Count}): ");
            int c = 0;
            foreach (float f in set) { sb.Append(f.ToString("F4")).Append(' '); if (++c > 24) { sb.Append("..."); break; } }
            sb.AppendLine();

            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(go);
        }
    }
}
