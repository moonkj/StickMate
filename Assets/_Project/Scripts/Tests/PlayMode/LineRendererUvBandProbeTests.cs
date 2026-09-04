using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ perf-doc R9 — <b>역잉크 분리막을 어느 형태로 넣을 수 있는가</b>를 재는 계측기.
    ///
    /// <para><b>묻는 것은 하나다</b>: LineRenderer가 굽는 메시에서 <c>uv.y</c>가 폭 방향 0..1을
    /// 지키는가. 지키면 <b>대안 A(프래그먼트 밴드)</b> — 기존 선의 폭만 2물리픽셀 넓히고 셰이더가
    /// <c>abs(uv.y-0.5)</c>로 가장자리를 역잉크로 칠하는 방식 — 이 성립하고, 그러면 정렬층 재배치
    /// (<c>design/character/R8_INK_MEMBRANE_FORM_VERDICT.md</c> §2, <b>되돌리기 어려운 결정</b>)와
    /// 렌더러 2배가 <b>동시에</b> 불필요해진다.</para>
    ///
    /// <para><b>★★ 실측 결론(2026-09-03)</b>
    /// <list type="bullet">
    ///   <item><b>직선 몸통 — 성립.</b> 픽셀 계수 막/잉크/막이 설계값과 일치.</item>
    ///   <item><b>코너 팬(numCornerVertices) — 성립(정확).</b> 팬이 <b>안쪽 오프셋 점을 피벗</b>으로
    ///     삼고 피벗과 호가 각각 uv.y = 0 / 1 <b>양 끝값만</b> 갖는다. 중간값이 하나도 없다.</item>
    ///   <item><b>★ 캡 팬(numCapVertices) — 성립 안 함.</b> 캡의 uv.y는 폭이 아니라 <b>각도</b>다:
    ///     팬 중심 0.5, 테두리 <c>k/(numCapVertices+1)</c>로 반원을 훑는다. 그래서 밴드가 캡에서
    ///     쐐기가 되고 <b>막이 남는 캡 호 = 1 − R</b>(R = 잉크코어÷전체 반폭)만 남는다.</item>
    ///   <item><b>대안 B(2패스)도 같이 죽는다.</b> 구운 메시의 정점 속성이
    ///     <c>Position · Color · TexCoord0</c> 셋뿐 — 법선·탄젠트·uv1~7이 전부 0개라
    ///     <b>바깥 방향 벡터를 실을 곳이 없다.</b></item>
    ///   <item><b>대안 A′(캡 0 + 끝면 밴드)</b> — T7이 잰다. 캡 팬을 없애 uv.y를 전 구간 정확하게
    ///     만들지만, <b>관절에서 둥근 캡이 메우던 쐐기가 열린다</b>. 그 대가를 셰이딩과 분리해
    ///     <b>단색 잉크 대조군</b>으로 잰다.</item>
    /// </list></para>
    ///
    /// <para><b>측정식</b>: 정점의 폴리라인까지의 거리 <c>d</c>와 반폭 <c>ρ</c>에 대해 uv.y가 폭
    /// 방향이라면 <c>d/ρ == |2·uv.y − 1|</c>. 부호 무관이라 캡·코너 팬에 그대로 적용된다.</para>
    ///
    /// <para><b>★ T2의 범위(2026-09-03 정정)</b> — T2는 몸 프리팹만이 아니라 <b>장비 선까지</b>
    /// 같은 잣대로 잰다. 몸(cap=8)과 장비(cap=4)가 서로 다른 캡 개수를 쓰는 덕분에 위 공식이
    /// <b>한 점이 아니라 두 점</b>에서 확인된다. 개수는 어디에도 적지 않는다 — 몸은 <b>맨몸 상태를
    /// 직접 만들어</b>, 장비는 착용 전후의 <b>차분</b>과 도형 빌더의 <c>ItemLineCount</c>로 센다
    /// (옛 T2는 «7개»를 적어 두었다가 모자·선글라스 5개에 부딪혀 죽었다).
    /// <b>위 네 결론은 이 정정으로 바뀌지 않는다.</b></para>
    ///
    /// <para><b>★ 이 파일의 단언은 전부 「지금 참인 것」이 아니라 「이 판정의 근거」를 못박는다.</b>
    /// Unity가 캡 uv를 고치거나 법선을 싣기 시작하면 해당 단언이 <b>빨개져서</b> "A/B 판정을 다시
    /// 하라"고 말한다. 그래서 T3·T5·T7의 <see cref="Assert.Ignore"/>는 <b>자동 래칫</b>이다 —
    /// 그래픽스가 있으면 같은 메서드가 실단언으로 간다.</para>
    ///
    /// <para>PNG는 판정 근거라 <c>docs/perf/uvprobe/</c>에 남긴다. 회귀마다 덮어써지는 텍스트
    /// 덤프는 <c>Logs/uvprobe/</c>(git 무시)로 보낸다 — <c>Temp/</c>는 Unity가 종료 때 비운다 — 매 회귀에 <c>M</c>이 뜨는 파일은 다음
    /// 사람이 무시하기 시작한다.</para>
    /// </summary>
    public sealed class LineRendererUvBandProbeTests
    {
        private const string LogPrefix = "[UV프로브]";
        private const string ShaderName = "StickMate/Debug/UvProbe";
        private const int ProbeLayer = 31;

        /// <summary><b>맨몸</b> 프리팹의 LineRenderer 이름. 이 목록이 T2의 <b>몸</b> 쪽 기준이다.
        /// <para>★ 2026-09-03 <b>범위 정정</b> — 옛 T2는 이 목록의 <b>길이</b>를 «에이전트 아래 선의
        /// 총 개수»와 맞세웠다. 그건 <b>장비 선이 런타임에 생긴다는 사실을 못 본</b> 비교였고
        /// (모자·선글라스 5개), 러너에서 «7개인데 12개»로 빨개졌다. 더 나쁜 것은 그 비교가
        /// <b>저장 파일의 차림에 따라 결과가 달라진다</b>는 점이다 — 전부 벗은 저장 파일에서는
        /// 조용히 초록이 됐다. 지금은 <b>맨몸 상태를 직접 만들어서</b> 재고, 장비는 실측한 <b>차분</b>과
        /// 도형 빌더의 <c>ItemLineCount</c>로 <b>두 경로에서</b> 센다. 개수는 어디에도 안 적혀 있다.</para>
        /// <para>★ 2026-09-03 <b>11 → 7</b>(마디 병합, 리더 판정 CH-8). 팔다리 하나를
        /// 위 마디에 붙은 폴리라인 하나가 통째로 그린다(States/LimbCurveRenderer "마디 병합" 절).
        /// 이 프로브의 T5가 그 결론의 근거를 냈다 — <c>T5_singlePolyline_elbow</c>는 <c>cap = 8</c>인데
        /// 팔꿈치 잉크 노출이 0.00%였다.</para></summary>
        private static readonly string[] BodyLineNames =
        {
            "Torso", "HeadFill", "HeadOutline",
            "LeftLeg", "RightLeg", "LeftArm", "RightArm",
        };

        /// <summary>병합으로 <b>없어진</b> 선. 남아 있으면 같은 그림을 두 번 그린다는 뜻이라
        /// T2가 그 자리에서 잡아야 한다(부재 단언은 썩으면 조용히 초록이 되므로 위 목록과 <b>짝</b>으로 둔다).</summary>
        private static readonly string[] MergedAwayLineNames =
        {
            "LeftLegLower", "RightLegLower", "LeftArmLower", "RightArmLower",
        };

        private static string Dir(string sub)
        {
            string d = Path.GetFullPath(Path.Combine(Application.dataPath, "..", sub));
            Directory.CreateDirectory(d);
            return d;
        }
        private static string TxtDir => Dir(Path.Combine("Logs", "uvprobe"));
        private static string PngDir => Dir(Path.Combine("docs", "perf", "uvprobe"));

        private static void WriteTxt(string name, string text)
        {
            File.WriteAllText(Path.Combine(TxtDir, name), text, new UTF8Encoding(false));
            Debug.Log($"{LogPrefix} 덤프 {name} ({text.Length}자) -> Logs/uvprobe/");
        }

        // ================================================================== 기하 도우미

        private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-12f) return (p - a).magnitude;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return (p - (a + t * ab)).magnitude;
        }

        private static float DistToPolyline(Vector2 p, IList<Vector2> pts, bool loop)
        {
            float best = float.MaxValue;
            int n = pts.Count, segs = loop ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                float d = DistToSegment(p, pts[i], pts[(i + 1) % n]);
                if (d < best) best = d;
            }
            return best;
        }

        private sealed class Report
        {
            public string Label;
            public int VertexCount, UvCount;
            public bool HasUv;
            public float HalfWidth, MaxAbsErr, MaxErrOnQuadBody;
            public List<float> DistinctUvY = new List<float>();
            public List<float> CapCenterUvY = new List<float>();
            public List<float> CapRimUvY = new List<float>();
            public string Table;
        }

        /// <summary>메시를 굽고 정점마다 <c>d/ρ</c> 와 <c>|2uv.y−1|</c> 를 대조한다.</summary>
        private static Report Analyze(string label, LineRenderer lr, Camera cam,
                                      IList<Vector2> path, bool loop, float halfWidth, bool dumpTable)
        {
            var mesh = new Mesh();
            lr.BakeMesh(mesh, cam, false);
            Vector3[] verts = mesh.vertices;
            Vector2[] uvs = mesh.uv;

            var rep = new Report
            {
                Label = label, VertexCount = verts.Length,
                UvCount = uvs?.Length ?? 0, HasUv = uvs != null && uvs.Length == verts.Length,
                HalfWidth = halfWidth,
            };
            var sb = new StringBuilder();
            sb.AppendLine($"# {label}");
            sb.AppendLine($"# vertices={verts.Length} uv={rep.UvCount} tris={mesh.triangles.Length / 3} halfWidth={halfWidth:F6}");
            sb.AppendLine("# i\tx\ty\tz\tuv.x\tuv.y\td\td/rho\t|2v-1|\terr");
            if (!rep.HasUv) { rep.Table = sb.ToString(); UnityEngine.Object.DestroyImmediate(mesh); return rep; }

            var seen = new SortedSet<float>();
            // 캡 팬 정점은 uv.x가 정확히 0 또는 1이고 폴리라인 끝점 주변에 모인다.
            Vector2 end0 = path[0], end1 = path[path.Count - 1];
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 v = verts[i]; Vector2 uv = uvs[i];
                var p = new Vector2(v.x, v.y);
                float d = DistToPolyline(p, path, loop);
                float dn = halfWidth > 1e-9f ? d / halfWidth : 0f;
                float pred = Mathf.Abs(2f * uv.y - 1f);
                float err = Mathf.Abs(dn - pred);
                if (err > rep.MaxAbsErr) rep.MaxAbsErr = err;

                bool nearEnd = !loop &&
                    (Vector2.Distance(p, end0) <= halfWidth * 1.001f + 1e-5f ||
                     Vector2.Distance(p, end1) <= halfWidth * 1.001f + 1e-5f);
                bool capFan = nearEnd && (Mathf.Approximately(uv.x, 0f) || Mathf.Approximately(uv.x, 1f));
                if (capFan && dn < 0.15f) rep.CapCenterUvY.Add(uv.y);
                else if (capFan && dn > 0.85f) rep.CapRimUvY.Add(uv.y);
                else if (err > rep.MaxErrOnQuadBody) rep.MaxErrOnQuadBody = err;

                seen.Add((float)Math.Round(uv.y, 6));
                if (dumpTable)
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "{0}\t{1:F6}\t{2:F6}\t{3:F6}\t{4:F6}\t{5:F6}\t{6:F6}\t{7:F6}\t{8:F6}\t{9:F6}",
                        i, v.x, v.y, v.z, uv.x, uv.y, d, dn, pred, err));
            }
            rep.DistinctUvY = seen.ToList();
            rep.Table = sb.ToString();
            UnityEngine.Object.DestroyImmediate(mesh);
            return rep;
        }

        private static string Summarize(Report r) =>
            $"{r.Label,-46} verts={r.VertexCount,4} rho={r.HalfWidth:F5} " +
            $"서로다른uv.y={r.DistinctUvY.Count,2} 캡중심={r.CapCenterUvY.Count} 캡테두리={r.CapRimUvY.Count} " +
            $"maxErr={r.MaxAbsErr:F5} 쿼드본문maxErr={r.MaxErrOnQuadBody:F5}";

        private static Camera MakeCamera()
        {
            var go = new GameObject("UvProbeCamera");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = 1f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0.5f, 1f);
            cam.enabled = false;
            // ★ MSAA를 반드시 끈다. 켜져 있으면 가장자리 픽셀이 섞여 「잉크가 배경에 닿는가」를
            //   세는 계수가 조용히 어긋난다(2026-09-03 실측: 1024x1024 한 장에 중간색 778~1253개).
            //   품질설정의 MSAA가 카메라를 통해 임시 버퍼로 들어오므로 카메라 쪽에서 끊는다.
            cam.allowMSAA = false;
            cam.allowHDR = false;
            cam.allowDynamicResolution = false;
            return cam;
        }

        private static LineRenderer MakeLine(string name, Vector2[] pts, float width, bool loop, int capVerts = 8)
        {
            var go = new GameObject(name);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false; lr.loop = loop;
            lr.numCapVertices = capVerts;     // 프리팹 실측 8 (A′는 0)
            lr.numCornerVertices = 8;         // 프리팹 실측 8
            lr.alignment = LineAlignment.View;        // 프리팹 alignment: 0
            lr.textureMode = LineTextureMode.Stretch; // 프리팹 textureMode: 0
            lr.startWidth = width; lr.endWidth = width;
            lr.positionCount = pts.Length;
            for (int i = 0; i < pts.Length; i++) lr.SetPosition(i, new Vector3(pts[i].x, pts[i].y, 0f));
            return lr;
        }

        // ================================================================== 픽셀 판독 (C# 안에서)

        private enum Px { BG, MEM, INK, OTHER }

        private static Px Cls(Color32 c)
        {
            if (c.b > 80 && c.r < 60 && c.g < 60) return Px.BG;
            if (c.r > 180 && c.g > 180 && c.b > 180) return Px.MEM;
            if (c.r < 60 && c.g < 60 && c.b < 60) return Px.INK;
            return Px.OTHER;
        }

        private sealed class Shot
        {
            public Color32[] P; public int W, H;
            public Px At(int x, int y) => (x < 0 || y < 0 || x >= W || y >= H) ? Px.OTHER : Cls(P[y * W + x]);
            public int Count(Px k) { int n = 0; for (int i = 0; i < P.Length; i++) if (Cls(P[i]) == k) n++; return n; }

            /// <summary>잉크가 배경에 <b>직접</b> 닿는 윤곽 비율. 막이 존재하는 이유 그 자체를 잰다.</summary>
            public (int inkOnBg, int memOnBg, float ratio) Exposure()
            {
                int ink = 0, mem = 0;
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        Px k = At(x, y);
                        if (k != Px.INK && k != Px.MEM) continue;
                        if (At(x + 1, y) == Px.BG || At(x - 1, y) == Px.BG ||
                            At(x, y + 1) == Px.BG || At(x, y - 1) == Px.BG)
                        { if (k == Px.INK) ink++; else mem++; }
                    }
                int t = ink + mem;
                return (ink, mem, t == 0 ? 0f : (float)ink / t);
            }
        }

        private static Shot Capture(Camera cam, RenderTexture rt, string png)
        {
            cam.Render();
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            if (!string.IsNullOrEmpty(png))
            {
                File.WriteAllBytes(Path.Combine(PngDir, png), tex.EncodeToPNG());
                Debug.Log($"{LogPrefix} PNG {png}");
            }
            var s = new Shot { P = tex.GetPixels32(), W = rt.width, H = rt.height };
            UnityEngine.Object.DestroyImmediate(tex);
            return s;
        }

        private static int _savedAa = -1;

        private static (Camera cam, RenderTexture rt) MakeRig(int size)
        {
            if (_savedAa < 0) _savedAa = QualitySettings.antiAliasing;
            QualitySettings.antiAliasing = 0;   // ★ 전역이라 반드시 되돌린다(ReleaseRig)
            var cam = MakeCamera();
            cam.cullingMask = 1 << ProbeLayer;   // 씬의 다른 잉크가 섞이면 판독이 오염된다
            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            { filterMode = FilterMode.Point, antiAliasing = 1 };
            cam.targetTexture = rt;
            return (cam, rt);
        }

        /// <summary>렌더 리그를 정리하고 <b>전역 품질설정을 원복</b>한다. 안 되돌리면 다음 테스트가 오염된다.</summary>
        private static void ReleaseRig(Camera cam, RenderTexture rt, Material mat)
        {
            if (cam != null) cam.targetTexture = null;
            RenderTexture.active = null;
            if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
            if (mat != null) UnityEngine.Object.DestroyImmediate(mat);
            if (cam != null) UnityEngine.Object.DestroyImmediate(cam.gameObject);
            if (_savedAa >= 0) { QualitySettings.antiAliasing = _savedAa; _savedAa = -1; }
        }

        /// <summary>분류기 교정 — 중간색이 있으면 그 프레임의 모든 계수가 무효다.</summary>
        private static void AssertClassifierCalibrated(Shot s, string where)
        {
            int other = s.Count(Px.OTHER);
            Assert.Zero(other, $"{LogPrefix} {where}: 분류 불가 픽셀 {other}개. AA/필터가 켜졌거나 색이 " +
                               "바뀌었다 — 이 프레임에서 나온 모든 비율을 폐기해야 한다.");
            Assert.Greater(s.Count(Px.BG), 0, $"{LogPrefix} {where}: 배경 픽셀 0 — 카메라가 비었다.");
            Assert.Greater(s.Count(Px.INK), 0, $"{LogPrefix} {where}: 잉크 픽셀 0 — 선이 안 그려졌다.");
        }

        // ================================================================== T1

        [UnityTest]
        public IEnumerator T1_캡은_각도를_코너는_폭을_uv_y로_나른다()
        {
            var cam = MakeCamera();
            var all = new StringBuilder();
            float w = 0.16f, rho = 0.08f;
            float turn = Mathf.PI - 126f * Mathf.Deg2Rad;   // 무릎 내각 126도(R8 §3-3 최악 자세)

            var straight = new[] { new Vector2(-0.5f, 0f), new Vector2(0.5f, 0f) };
            var knee = new[] { new Vector2(-0.5f, 0f), new Vector2(0f, 0f),
                               new Vector2(Mathf.Cos(-turn) * 0.5f, Mathf.Sin(-turn) * 0.5f) };
            int n = 24;
            var circle = new Vector2[n];
            for (int i = 0; i < n; i++)
            { float a = i * Mathf.PI * 2f / n; circle[i] = new Vector2(Mathf.Cos(a) * 0.4f, Mathf.Sin(a) * 0.4f); }

            var open = new List<Report>();
            foreach (var c in new (string, Vector2[])[] { ("합성-직선2점", straight), ("합성-무릎126도", knee) })
            {
                var lr = MakeLine(c.Item1, c.Item2, w, false);
                yield return null;
                var r = Analyze(c.Item1, lr, cam, c.Item2, false, rho, true);
                open.Add(r); all.AppendLine(Summarize(r)); all.AppendLine(r.Table);
                Debug.Log($"{LogPrefix} {Summarize(r)}");
                UnityEngine.Object.DestroyImmediate(lr.gameObject);
            }
            var loopLr = MakeLine("합성-머리링24점loop", circle, w, true);
            yield return null;
            var loopRep = Analyze("합성-머리링24점loop", loopLr, cam, circle, true, rho, true);
            all.AppendLine(Summarize(loopRep)); all.AppendLine(loopRep.Table);
            Debug.Log($"{LogPrefix} {Summarize(loopRep)}");
            UnityEngine.Object.DestroyImmediate(loopLr.gameObject);
            UnityEngine.Object.DestroyImmediate(cam.gameObject);
            WriteTxt("T1_synthetic_uv.txt", all.ToString());

            // ---- 단언 1: 닫힌 선(코너만)의 uv.y는 양 끝값 둘뿐 = 코너 팬은 폭 방향을 지킨다
            CollectionAssert.AreEqual(new[] { 0f, 1f }, loopRep.DistinctUvY,
                $"{LogPrefix} 닫힌 선(캡 없음, 코너만)의 서로 다른 uv.y가 {{0,1}}이 아니다: " +
                $"[{string.Join(", ", loopRep.DistinctUvY)}]. 코너 팬이 폭 방향을 지킨다는 판정의 근거가 " +
                "이것이다 — 값이 바뀌면 대안 A 판정을 다시 해야 한다.");

            // ---- 단언 2: 열린 선의 캡 팬 중심은 정확히 2개이고 uv.y = 0.5
            foreach (var r in open)
            {
                Assert.AreEqual(2, r.CapCenterUvY.Count,
                    $"{LogPrefix} {r.Label}: 캡 팬 중심 정점이 2개가 아니다({r.CapCenterUvY.Count}).");
                foreach (float v in r.CapCenterUvY)
                    Assert.AreEqual(0.5f, v, 1e-5f, $"{LogPrefix} {r.Label}: 캡 팬 중심 uv.y가 0.5가 아니다.");
            }

            // ---- 단언 3: ★ 캡 테두리 uv.y = k/(cap+1) — 각도다. 이게 A가 캡에서 깨지는 이유다
            int cap = 8;
            var expected = Enumerable.Range(0, cap + 2).Select(k => (float)Math.Round(k / (float)(cap + 1), 6)).ToArray();
            foreach (var r in open)
            {
                var got = r.CapRimUvY.Select(v => (float)Math.Round(v, 6)).Distinct().OrderBy(v => v).ToArray();
                CollectionAssert.AreEqual(expected, got,
                    $"{LogPrefix} {r.Label}: 캡 테두리 uv.y가 k/(numCapVertices+1)이 아니다. " +
                    $"기대 [{string.Join(", ", expected)}] 실측 [{string.Join(", ", got)}]. " +
                    "★ 이 단언이 빨개지면 Unity가 캡 uv 매핑을 바꾼 것이고, 대안 A의 캡 결함 판정을 " +
                    "처음부터 다시 재야 한다(docs/perf/uvprobe).");
            }

            // ---- 단언 4: 쿼드 본문(캡 밖)에서는 d/rho == |2uv.y-1| 가 성립한다
            foreach (var r in open.Concat(new[] { loopRep }))
                Assert.Less(r.MaxErrOnQuadBody, 0.15f,
                    $"{LogPrefix} {r.Label}: 캡 밖에서 폭 매핑이 깨졌다(maxErr={r.MaxErrOnQuadBody:F4}). " +
                    "코너 팬은 호가 조인트에서 rho보다 안쪽으로 들어오므로 약간의 차는 정상이지만 " +
                    "0.15를 넘으면 그건 다른 사건이다.");

            // ---- 단언 5: 그럼에도 전체 최대 오차는 캡에서 크게 벌어진다 (양성 대조 — 결함이 실재)
            foreach (var r in open)
                Assert.Greater(r.MaxAbsErr, 0.5f,
                    $"{LogPrefix} {r.Label}: 캡 결함이 사라졌다(maxErr={r.MaxAbsErr:F4}). " +
                    "축하할 실패다 — 대안 A를 다시 판정하라.");
            Assert.Less(loopRep.MaxAbsErr, 0.5f,
                $"{LogPrefix} 닫힌 선에는 캡이 없으므로 큰 오차가 있으면 안 된다(실측 {loopRep.MaxAbsErr:F4}).");
        }

        // ================================================================== T2

        /// <summary>착용 상태를 <b>기본 차림</b>으로 되돌린다 — T2가 착용을 바꿔 가며 재기 때문이다.
        /// <para>디스크는 건드리지 않는다: 저장은 <c>CharacterProgressionDirector.IsAnythingDirty</c>의
        /// 더티 플래그로만 일어나고 <b>착용은 그 목록에 없다</b>(진행도/기록/UI배치/외형/설정뿐).
        /// 그래서 이 프로브는 사용자의 저장 파일을 쓰지 않는다.</para></summary>
        [UnityTearDown]
        public IEnumerator 착용상태를_기본차림으로_되돌린다()
        {
            StickMate.Core.EquipmentModel.ResetForTesting();
            // ★ ResetForTesting은 <b>조용하다</b>(이벤트를 안 흘린다). 그것만 부르면 모델은 기본
            //   차림인데 계층에는 직전 차림의 선이 그대로 남는다 — 다음 사람이 «벗겼는데 선이 있다»를
            //   보게 되는 자리다. 씬을 다시 로드하는 테스트는 우연히 무사하지만 그 우연에 기대지 않는다.
            StickMate.Core.StickmanEventBus.RaiseCharacterEquipmentChanged();
            yield return null;
        }

        /// <summary>에이전트 아래 <see cref="LineRenderer"/>를 이름으로 훑는다.
        /// 이름이 겹치면 그 자리에서 실패시킨다 — 겹치면 아래의 「차분으로 장비를 센다」가
        /// <b>조용히 적게 세기</b> 때문이다(사전이 하나로 합쳐진다).</summary>
        private static Dictionary<string, LineRenderer> NamedLines(
            StickMate.Core.StickmanAgent agent, string where)
        {
            var dict = new Dictionary<string, LineRenderer>(StringComparer.Ordinal);
            foreach (LineRenderer lr in agent.GetComponentsInChildren<LineRenderer>(true))
            {
                if (lr == null) continue;
                string n = lr.gameObject.name;
                Assert.IsFalse(dict.ContainsKey(n),
                    $"{LogPrefix} {where}: 이름이 겹치는 선 '{n}'이 둘 이상이다. 이 프로브는 " +
                    "이름 사전의 <b>차분</b>으로 장비 선을 세므로, 이름이 겹치면 장비 개수가 " +
                    "조용히 줄어든 채 초록이 된다.");
                dict.Add(n, lr);
            }
            return dict;
        }

        /// <summary>두 이름 집합이 같은가. ★ <b>실단언과 음성 대조가 같은 비교기를 쓰게</b> 하려고
        /// 함수로 뺐다 — 대조가 다른 비교기를 쓰면 그 대조는 실단언을 지켜 주지 못한다.</summary>
        private static bool SameNameSet(IEnumerable<string> a, IEnumerable<string> b)
            => new SortedSet<string>(a, StringComparer.Ordinal)
               .SetEquals(new SortedSet<string>(b, StringComparer.Ordinal));

        /// <summary>열린 선의 uv.y가 가질 수 있는 값 전체 — 캡 테두리 <c>k/(cap+1)</c>(k=0..cap+1,
        /// 양 끝 0·1 포함)에 팬 중심 <c>0.5</c>를 더한 것. 쿼드 본문과 코너 팬은 0·1만 내므로
        /// 여기에 이미 들어 있다.
        /// <para>★ T1이 <b>합성</b> 선에서 이 공식을 확인했고, T2는 <b>실제 리그</b>가 같은 공식을
        /// 따르는지를 <b>cap 값 두 개</b>로 확인한다 — 몸 8, 장비 4(둘 다 프로덕션에서 읽는다).
        /// 값을 하나만 쓰면 공식이 아니라 우연을 재게 된다.</para></summary>
        private static float[] OpenLineUvYTheory(int cap)
        {
            var s = new SortedSet<float>();
            for (int k = 0; k <= cap + 1; k++) s.Add((float)Math.Round(k / (float)(cap + 1), 6));
            s.Add(0.5f);
            return s.ToArray();
        }

        private static readonly float[] LoopLineUvYTheory = { 0f, 1f };

        private static void Wear(StickMate.Core.EquipmentSlot slot, int index, StickMate.Core.StickConfig config)
        {
            StickMate.Core.EquipmentModel.TryWear(slot, index, config);
            Assert.AreEqual(index, StickMate.Core.EquipmentModel.WornIndex(slot),
                $"{LogPrefix} {slot} {index}번을 걸치지 못했다(지금 " +
                $"{StickMate.Core.EquipmentModel.WornIndex(slot)}번). 기본 차림의 아이템인데도 " +
                "못 걸쳤다면 소유/잠금 판정이 바뀐 것이다.");
        }

        private static void StripAll(StickMate.Core.StickConfig config)
        {
            for (int i = 0; i < StickMate.Core.EquipmentModel.SlotCount; i++)
                StickMate.Core.EquipmentModel.TryWear((StickMate.Core.EquipmentSlot)i,
                    StickMate.Core.EquipmentModel.NotWorn, config);
        }

        /// <summary>착용이 바뀌면 렌더러가 컨테이너를 통째로 다시 만든다(이벤트 -> Rebuild).
        /// 프레임 수로 기다리는 이유: 여기서 재는 것은 <b>시간 연출이 아니라 계층 재구성</b>이고,
        /// 그건 프레임 단위로 일어난다(CLAUDE.md의 벽시계 규칙은 시간 기반 <b>연출</b>이 대상이다).</summary>
        private static IEnumerator SettleAccessories()
        {
            for (int i = 0; i < 8; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator T2_실제_리그의_몸선과_장비선이_같은_uv_규칙을_따른다()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null; yield return null; yield return null;

            var cam = Camera.main != null ? Camera.main : UnityEngine.Object.FindFirstObjectByType<Camera>();
            Assert.IsNotNull(cam, $"{LogPrefix} 씬 카메라를 찾지 못했다.");
            var agent = UnityEngine.Object.FindFirstObjectByType<StickMate.Core.StickmanAgent>();
            Assert.IsNotNull(agent, $"{LogPrefix} StickmanAgent를 찾지 못했다.");
            var acc = UnityEngine.Object.FindFirstObjectByType<StickMate.Interaction.CharacterAccessoryRenderer>();
            Assert.IsNotNull(acc, $"{LogPrefix} CharacterAccessoryRenderer를 찾지 못했다 — 장비 선을 셀 수 없다.");
            StickMate.Core.StickConfig config = agent.Config;

            // ---- 아이템 <b>번호</b>를 이 파일에 적지 않는다. 「지금 레벨에서 걸칠 수 있는 첫 아이템」을
            //      모델에게 묻는다(<c>FirstOwnedItemIndex</c> — 카테고리 토글이 쓰는 바로 그 경로다).
            //      ★ <b>레벨을 올리지 않는다</b>: 진행도는 더티 플래그를 세우는 <b>디스크 저장 대상</b>이라
            //        계측기가 사용자의 저장 파일을 건드리게 된다. 착용은 그 목록에 없다.
            //      ★ <b>ResetForTesting으로 번호를 알아내지 않는다</b>: 그것은 이벤트를 안 흘리므로
            //        모델만 바뀌고 계층은 저장 파일 차림 그대로 남는다. 그 상태에서 «기본 차림을
            //        걸친다»고 하면 TryWear가 «이미 그 번호»라며 false를 돌려주고 재구성이 안 일어난다 —
            //        맨몸을 잰다고 믿으면서 남의 차림을 재게 된다.
            var headSlot = StickMate.Core.EquipmentSlot.Head;
            var eyesSlot = StickMate.Core.EquipmentSlot.Eyes;

            // ---- 3단계 실측: 맨몸 -> +모자 -> +안경. 개수를 적는 대신 <b>차분</b>으로 얻는다.
            StripAll(config);
            // 모델과 계층이 어긋난 채로 들어왔을 수 있으므로(위 문단) 재구성을 <b>한 번 강제</b>한다.
            StickMate.Core.StickmanEventBus.RaiseCharacterEquipmentChanged();
            yield return SettleAccessories();
            var bare = NamedLines(agent, "맨몸");

            int hatItem = StickMate.Core.EquipmentModel.FirstOwnedItemIndex(headSlot);
            int eyeItem = StickMate.Core.EquipmentModel.FirstOwnedItemIndex(eyesSlot);
            Assert.GreaterOrEqual(hatItem, 0,
                $"{LogPrefix} 이 레벨에서 걸칠 수 있는 모자가 없다 — 이 프로브의 양성 대조(장비를 " +
                "걸치면 선이 늘어난다)가 성립하지 않는다. 요구 레벨 표가 바뀌었으면 여기서 쓸 자리를 " +
                "다시 골라라(레벨을 올려서 뚫지 마라 — 저장 파일이 더러워진다).");
            Assert.GreaterOrEqual(eyeItem, 0, $"{LogPrefix} 걸칠 수 있는 안경이 없다(위와 같은 사정).");

            Wear(headSlot, hatItem, config);
            yield return SettleAccessories();
            var withHat = NamedLines(agent, "모자만");

            Wear(eyesSlot, eyeItem, config);
            yield return SettleAccessories();
            var worn = NamedLines(agent, "모자+안경");

            string[] hatLines = withHat.Keys.Except(bare.Keys, StringComparer.Ordinal).ToArray();
            string[] eyeLines = worn.Keys.Except(withHat.Keys, StringComparer.Ordinal).ToArray();
            string[] equipLines = worn.Keys.Except(bare.Keys, StringComparer.Ordinal).ToArray();

            Debug.Log($"{LogPrefix} T2 실측 — 맨몸 {bare.Count}개 [{string.Join(", ", bare.Keys)}] / " +
                      $"모자 +{hatLines.Length} [{string.Join(", ", hatLines)}] / " +
                      $"안경 +{eyeLines.Length} [{string.Join(", ", eyeLines)}] / 합계 {worn.Count}개.");

            // ============================================================ (A) 몸 — 이름 계약
            foreach (string name in BodyLineNames)
                Assert.IsTrue(bare.ContainsKey(name),
                    $"{LogPrefix} 맨몸 프리팹에 본체 선 '{name}'이 없다. 실측: " +
                    $"[{string.Join(", ", bare.Keys)}]");

            // ★ 짝이 되는 부재 단언 — 병합으로 없어진 선이 되살아나면 같은 그림을 두 번 그린다.
            //   <b>장비까지 포함한</b> 최종 집합에 대고 본다(존재 단언은 맨몸 집합에 대고 봤다).
            //   사전이 통째로 비면 위 존재 단언이 먼저 빨개지므로, 이 부재 단언이 조용히 초록이
            //   되는 경로가 없다(CLAUDE.md 부재 단언 규칙).
            foreach (string gone in MergedAwayLineNames)
                Assert.IsFalse(worn.ContainsKey(gone),
                    $"{LogPrefix} 병합으로 없어졌어야 할 선 '{gone}'이 아직 있다 — 프리팹이 마디 병합 " +
                    "이전이거나(다시 굽기 필요) 합치고 나서 아래를 안 지운 중간 상태다.");

            Assert.IsTrue(SameNameSet(BodyLineNames, bare.Keys),
                $"{LogPrefix} <b>맨몸</b> 선 집합이 기준 목록과 다르다. " +
                $"기준 [{string.Join(", ", BodyLineNames)}] / 실측 [{string.Join(", ", bare.Keys)}]. " +
                "★ 여기서 재는 것은 <b>전부 벗은 상태</b>다 — 장비 선이 여기 섞였다면 착용 해제가 " +
                "렌더러에 도달하지 않은 것이고, 그건 그 자체로 버그다.");

            // ---- 음성 대조: 위 단언이 <b>정말로 개수 차이를 잡는가</b>. 같은 비교기로 확인한다.
            string[] oneShort = bare.Keys.Take(bare.Count - 1).ToArray();
            Assert.IsFalse(SameNameSet(BodyLineNames, oneShort),
                $"{LogPrefix} 음성 대조 실패 — 몸 선 하나를 <b>뺐는데도</b> 비교기가 «같다»고 답했다. " +
                "그렇다면 바로 위의 초록은 아무것도 증명하지 않는다.");
            string[] oneExtra = bare.Keys.Concat(new[] { "존재하지않는가짜선" }).ToArray();
            Assert.IsFalse(SameNameSet(BodyLineNames, oneExtra),
                $"{LogPrefix} 음성 대조 실패 — 없는 선을 <b>하나 더했는데도</b> «같다»고 답했다. " +
                "옛 T2가 죽은 방식이 정확히 이것이다(장비 5개가 더해진 것을 못 봤다).");

            // ============================================================ (B) 장비 — 양성 대조
            Assert.Greater(hatLines.Length, 0,
                $"{LogPrefix} 모자를 걸쳤는데 <b>새 선이 하나도 안 생겼다</b>. 장비 선이 잡히지 " +
                "않으면 이 프로브는 몸만 재는 것이고, 「장비도 같은 규칙을 따르는가」에 답하지 못한다.");
            Assert.Greater(eyeLines.Length, 0,
                $"{LogPrefix} 안경을 <b>추가로</b> 걸쳤는데 새 선이 안 생겼다(양성 대조).");
            CollectionAssert.IsSubsetOf(bare.Keys, worn.Keys,
                $"{LogPrefix} 장비를 걸치자 <b>몸 선이 사라졌다</b>. 장비는 몸 위에 얹히는 것이지 " +
                "몸을 대체하지 않는다 — 사라진 선: " +
                $"[{string.Join(", ", bare.Keys.Except(worn.Keys, StringComparer.Ordinal))}]");
            CollectionAssert.IsSubsetOf(hatLines, equipLines,
                $"{LogPrefix} 모자를 걸쳐 생긴 선이 안경까지 걸친 뒤 사라졌다.");

            // ---- 개수는 <b>다른 경로</b>로 다시 잰다: 도형 빌더가 이 아이템에 대해 몇 개를 굽는가.
            //      계층 실측(차분)과 빌더 실측이 같아야 한다. 어느 쪽에도 숫자를 적지 않는다.
            //      ★ HEAD/EYES만 쓰는 이유: ItemLineCount는 커버선 +∞ · 월요일 false로 굽는데,
            //        커버선은 HAIR만, 월요일 상태는 NECK만 바꾼다. 이 두 자리에서는 두 경로가
            //        같은 입력을 본다.
            int hatExpected = acc.ItemLineCount(headSlot, hatItem);
            int eyeExpected = acc.ItemLineCount(eyesSlot, eyeItem);
            Assert.AreEqual(hatExpected, hatLines.Length,
                $"{LogPrefix} 모자 선 개수가 어긋난다 — 도형 빌더 {hatExpected}개 vs 계층 실측 " +
                $"{hatLines.Length}개 [{string.Join(", ", hatLines)}]. 빌더가 굽는 도형과 실제로 " +
                "만들어진 LineRenderer가 1:1이 아니라는 뜻이다(도형 하나가 조용히 안 그려졌거나 " +
                "이름이 겹쳤다).");
            Assert.AreEqual(eyeExpected, eyeLines.Length,
                $"{LogPrefix} 안경 선 개수가 어긋난다 — 도형 빌더 {eyeExpected}개 vs 계층 실측 " +
                $"{eyeLines.Length}개 [{string.Join(", ", eyeLines)}].");
            Assert.AreEqual(bare.Count + hatExpected + eyeExpected, worn.Count,
                $"{LogPrefix} 합계가 안 맞는다 — 몸(맨몸 실측 {bare.Count}) + 장비(빌더 실측 " +
                $"{hatExpected}+{eyeExpected}) != 전체 {worn.Count}. 실측: " +
                $"[{string.Join(", ", worn.Keys)}]. ★ 어느 숫자도 이 파일에 적혀 있지 않다 — " +
                "장비가 42종으로 늘어도 이 단언은 갱신할 필요가 없다.");

            // ============================================================ (C) 본론 — uv.y 규칙
            //  몸이든 장비든 <b>가르지 않고</b> 같은 잣대를 댄다. 분기 기준은 «몸/장비»가 아니라
            //  렌더러가 스스로 들고 있는 두 값(loop, numCapVertices)뿐이다.
            var all = new StringBuilder();
            var reports = new Dictionary<string, Report>(StringComparer.Ordinal);
            var openCaps = new SortedSet<int>();
            foreach (var kv in worn.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                LineRenderer lr = kv.Value;
                bool isBody = bare.ContainsKey(kv.Key);
                int n = lr.positionCount;
                Assert.GreaterOrEqual(n, 2,
                    $"{LogPrefix} {kv.Key}: 점이 {n}개다 — 선이 될 수 없다.");
                var buf = new Vector3[n]; lr.GetPositions(buf);
                var pts = new Vector2[n];
                for (int i = 0; i < n; i++) pts[i] = new Vector2(buf[i].x, buf[i].y);

                var r = Analyze($"{(isBody ? "몸  " : "장비")} {kv.Key}(loop={(lr.loop ? 1 : 0)}," +
                                $"pts={n},cap={lr.numCapVertices},cor={lr.numCornerVertices})",
                                lr, cam, pts, lr.loop, lr.startWidth * 0.5f, true);
                reports[kv.Key] = r;
                all.AppendLine(Summarize(r)); all.AppendLine(r.Table);

                Assert.Greater(r.VertexCount, 0, $"{LogPrefix} {kv.Key}: 구운 메시에 정점이 없다.");
                Assert.IsTrue(r.HasUv,
                    $"{LogPrefix} {kv.Key}: 정점 {r.VertexCount}개인데 uv가 {r.UvCount}개다. " +
                    "★ uv가 없으면 대안 A(프래그먼트 밴드)는 이 선에서 성립할 수 없다.");

                float[] expect = lr.loop ? LoopLineUvYTheory : OpenLineUvYTheory(lr.numCapVertices);
                if (!lr.loop) openCaps.Add(lr.numCapVertices);
                CollectionAssert.AreEqual(expect, reports[kv.Key].DistinctUvY,
                    $"{LogPrefix} {(isBody ? "몸" : "장비")} 선 {kv.Key}" +
                    $"(loop={lr.loop}, cap={lr.numCapVertices})의 uv.y 값 집합이 이론과 다르다.\n" +
                    $"  기대 [{string.Join(", ", expect)}]\n  실측 [{string.Join(", ", reports[kv.Key].DistinctUvY)}]\n" +
                    "★ 이론은 T1이 합성 선에서 확인한 것 그대로다: 닫힌 선은 코너 팬뿐이라 {0,1}, " +
                    "열린 선은 캡 테두리 k/(cap+1) + 팬 중심 0.5. <b>실측이 이론 밖의 값을 냈다면</b> " +
                    "Unity가 uv 매핑을 바꾼 것이고 대안 A/A′ 판정을 처음부터 다시 재야 한다. " +
                    "<b>값이 빠졌다면</b> 그 도형의 마디가 획 폭에 비해 너무 짧아 축퇴한 것이니 " +
                    "도형 쪽을 먼저 보라(Logs/uvprobe/T2_prefab_uv.txt에 전 정점이 있다).");
            }

            // ---- 몸 프리팹의 전제(캡·코너 8)는 T1/T3/T5/T7의 합성 선이 흉내 내는 값이다.
            //      여기가 어긋나면 그 네 테스트가 <b>프리팹과 다른 물건</b>을 재고 있는 것이다.
            foreach (string name in BodyLineNames)
            {
                Assert.AreEqual(8, worn[name].numCapVertices,
                    $"{LogPrefix} 몸 {name}: numCapVertices가 8이 아니다 — MakeLine의 기본값(8)이 " +
                    "더 이상 프리팹을 흉내 내지 않는다.");
                Assert.AreEqual(8, worn[name].numCornerVertices,
                    $"{LogPrefix} 몸 {name}: numCornerVertices가 8이 아니다(위와 같은 사정).");
            }

            // ---- 두 갈래가 <b>둘 다 실제로 돌았는가</b>. 위 루프는 loop 플래그로 갈라지는데,
            //      한쪽으로만 흐르면 이론의 절반은 <b>검사되지 않은 채</b> 초록이 된다
            //      (닫힌 선만 있으면 캡 공식이, 열린 선만 있으면 {0,1}이 미검증으로 남는다).
            int loopCount = worn.Values.Count(l => l.loop);
            int openCount = worn.Count - loopCount;
            Assert.Greater(loopCount, 0,
                $"{LogPrefix} 닫힌 선이 하나도 없다 — 「코너 팬은 폭 방향을 지킨다({{0,1}})」 쪽 " +
                "단언이 이번 실행에서 <b>한 번도 실행되지 않았다</b>. 머리 링 2개가 열린 선이 " +
                "됐는지 확인하라.");
            Assert.Greater(openCount, 0,
                $"{LogPrefix} 열린 선이 하나도 없다 — 「캡의 uv.y는 각도다(k/(cap+1))」 쪽 단언이 " +
                "이번 실행에서 <b>한 번도 실행되지 않았다</b>. 이 프로브의 결론 전체가 그 사실 " +
                "위에 서 있다.");
            Debug.Log($"{LogPrefix} T2 갈래 — 닫힌 선 {loopCount}개 / 열린 선 {openCount}개 " +
                      "(두 갈래 모두 실행됨).");

            // ---- ★ 대조의 힘: k/(cap+1) 공식을 <b>서로 다른 cap 값 두 개</b>에서 확인했는가.
            //      몸은 8, 장비는 4다(둘 다 프로덕션이 정한 값을 읽었을 뿐 여기 적지 않았다).
            //      값이 하나뿐이면 이 프로브는 공식이 아니라 그 한 점을 외운 것이 된다.
            Assert.GreaterOrEqual(openCaps.Count, 2,
                $"{LogPrefix} 열린 선의 numCapVertices가 [{string.Join(", ", openCaps)}] 한 종류뿐이다. " +
                "몸과 장비가 서로 다른 캡 개수를 쓰는 덕분에 이 프로브가 k/(cap+1)을 <b>두 점에서</b> " +
                "확인해 왔는데, 그 대조가 사라졌다. 장비 선의 캡 개수가 몸과 같아졌거나(그러면 " +
                "이 검사를 다른 방식으로 다시 세워라) 장비에 열린 선이 하나도 없다.");

            WriteTxt("T2_prefab_uv.txt", all.ToString());
            Debug.Log($"{LogPrefix} T2: 몸 {bare.Count}개 + 장비 {equipLines.Length}개 = {worn.Count}개 " +
                      $"전수 확인. 열린 선의 cap 값 [{string.Join(", ", openCaps)}] — " +
                      "두 값 모두에서 k/(cap+1)이 성립했다.");
        }

        // ================================================================== T3

        [UnityTest]
        public IEnumerator T3_실제_GPU에서_막이_캡에서만_사라진다()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore($"{LogPrefix} 그래픽스 디바이스가 Null이다(-nographics) — 실제 렌더는 " +
                              "못 봤다. 계산으로 메우지 않는다. ★ 자동 래칫: 그래픽스가 있는 러너에서는 " +
                              "이 메서드가 그대로 아래 실단언들로 간다(Ignore 뒤에 Assert가 8~11개 있다).");
            var shader = Shader.Find(ShaderName);
            Assert.IsNotNull(shader, $"{LogPrefix} 셰이더를 못 찾았다: {ShaderName}");

            var (cam, rt) = MakeRig(1024);
            var mat = new Material(shader);
            mat.SetColor("_Ink", Color.black); mat.SetColor("_Membrane", Color.white);

            float turn = Mathf.PI - 126f * Mathf.Deg2Rad;
            const float halfW = 0.13f;
            var knee = new[] { new Vector2(-0.45f, 0.20f), new Vector2(0f, 0.20f),
                               new Vector2(Mathf.Cos(-turn) * 0.45f, 0.20f + Mathf.Sin(-turn) * 0.45f) };
            var lr = MakeLine("UvProbeLine", knee, halfW * 2f, false);
            lr.gameObject.layer = ProbeLayer; lr.sharedMaterial = mat;
            yield return null;

            var log = new StringBuilder();
            log.AppendLine($"# T3 device={SystemInfo.graphicsDeviceType}");

            // (a) uv 그대로 / v 회색조 — 사람이 보는 그림
            cam.transform.position = new Vector3(0f, 0f, -10f); cam.orthographicSize = 0.70f;
            mat.SetFloat("_Mode", 0f); Capture(cam, rt, "T3_full_mode0uv.png");
            mat.SetFloat("_Mode", 1f); Capture(cam, rt, "T3_full_mode1v.png");

            // (b) 대안 A — 세 실제 구성. R = 잉크코어 / 전체 반폭
            var cfg = new (string tag, float R)[]
            {
                ("win075", 3.528f / 5.528f),   // Win100% s=0.75  (잉크 3.528px + 1px x2)
                ("mac075", 7.055f / 9.055f),   // macOS Retina s=0.75
                ("win035", 2.000f / 4.000f),   // Win100% s=0.35  (하한 2.00pt = 2.00px)
            };
            foreach (var c in cfg)
            {
                mat.SetFloat("_Mode", 2f); mat.SetFloat("_CoreHalf", c.R / 2f);
                cam.transform.position = new Vector3(0f, 0f, -10f); cam.orthographicSize = 0.70f;
                Capture(cam, rt, $"T3_full_mode2_{c.tag}.png");

                // 코너 확대 — 막이 끊기지 않아야 한다
                cam.transform.position = new Vector3(0.02f, 0.10f, -10f); cam.orthographicSize = 0.20f;
                var corner = Capture(cam, rt, $"T3_corner_mode2_{c.tag}.png");
                AssertClassifierCalibrated(corner, $"코너/{c.tag}");
                var ce = corner.Exposure();
                log.AppendLine($"코너 {c.tag}: 잉크노출 {ce.inkOnBg} / 막 {ce.memOnBg} = {ce.ratio:P1}");
                Assert.Less(ce.ratio, 0.01f,
                    $"{LogPrefix} 코너({c.tag})에서 잉크가 배경에 닿는다({ce.ratio:P1}). " +
                    "코너 팬은 uv.y를 폭 방향으로 나르므로 0이어야 한다 — 이 판정의 기둥이 무너진 것이다.");

                // 캡 확대 — 막이 남는 호 비율 = 1 - R
                cam.transform.position = new Vector3(-0.42f, 0.20f, -10f); cam.orthographicSize = 0.20f;
                var capShot = Capture(cam, rt, $"T3_cap_mode2_{c.tag}.png");
                AssertClassifierCalibrated(capShot, $"캡/{c.tag}");
                float ppu = rt.width / (2f * cam.orthographicSize);
                float cx = (-0.45f - cam.transform.position.x) * ppu + rt.width * 0.5f;
                float cy = rt.height * 0.5f + (0.20f - cam.transform.position.y) * ppu; // ReadPixels 는 아래가 y=0
                float rpx = halfW * ppu;
                int with = 0, tot = 0;
                for (int phi = 0; phi <= 180; phi += 2)
                {
                    float a = phi * Mathf.Deg2Rad;
                    float dx = -Mathf.Sin(a), dy = Mathf.Cos(a);
                    bool mem = false;
                    for (int k = 1; k <= (int)rpx + 1; k++)
                        if (capShot.At(Mathf.RoundToInt(cx + dx * k), Mathf.RoundToInt(cy + dy * k)) == Px.MEM)
                        { mem = true; break; }
                    tot++; if (mem) with++;
                }
                float frac = (float)with / tot;
                var xe = capShot.Exposure();
                log.AppendLine($"캡 {c.tag}: 막이 남는 호 {frac:F3} (이론 1-R = {1f - c.R:F3}), 잉크노출 {xe.ratio:P1}");
                Assert.AreEqual(1f - c.R, frac, 0.06f,
                    $"{LogPrefix} 캡({c.tag}): 막이 남는 호 비율 {frac:F3}이 이론 1-R={1f - c.R:F3}과 다르다. " +
                    "★ 이 식이 「캡의 uv.y는 각도다」의 정량 증거다. 어긋나면 캡 매핑이 바뀐 것이다.");
                Assert.Greater(xe.ratio, 0.2f,
                    $"{LogPrefix} 캡({c.tag})에서 잉크 노출이 사라졌다({xe.ratio:P1}) — 축하할 실패다. " +
                    "대안 A를 다시 판정하라.");
            }

            // (c) 직선 몸통 단면 — 막/잉크/막 픽셀 수가 설계와 맞는가
            {
                float R = cfg[0].R;
                mat.SetFloat("_Mode", 2f); mat.SetFloat("_CoreHalf", R / 2f);
                cam.transform.position = new Vector3(0f, 0f, -10f); cam.orthographicSize = 0.70f;
                var s = Capture(cam, rt, null);
                AssertClassifierCalibrated(s, "직선단면");
                float ppu = rt.width / (2f * cam.orthographicSize);
                int col = Mathf.RoundToInt((-0.30f + 0.70f) * ppu);
                int mem1 = 0, ink = 0, mem2 = 0, phase = 0;
                for (int y = rt.height - 1; y >= 0; y--)
                {
                    var k = s.At(col, y);
                    if (k == Px.BG) { if (phase == 3) break; continue; }
                    if (k == Px.MEM && phase <= 1) { phase = 1; mem1++; }
                    else if (k == Px.INK) { phase = 2; ink++; }
                    else if (k == Px.MEM && phase == 2) { phase = 3; mem2++; }
                    else if (k == Px.MEM && phase == 3) mem2++;
                }
                float total = 2f * halfW * ppu;
                float expInk = total * R, expMem = total * (1f - R) * 0.5f;
                log.AppendLine($"직선 단면: 막 {mem1} / 잉크 {ink} / 막 {mem2}  (설계 {expMem:F1}/{expInk:F1}/{expMem:F1})");
                Assert.AreEqual(expInk, ink, 3f, $"{LogPrefix} 직선 몸통의 잉크 코어 폭이 설계와 다르다.");
                Assert.AreEqual(expMem, mem1, 3f, $"{LogPrefix} 직선 몸통 위쪽 막 두께가 설계와 다르다.");
                Assert.AreEqual(expMem, mem2, 3f, $"{LogPrefix} 직선 몸통 아래쪽 막 두께가 설계와 다르다.");
            }

            WriteTxt("T3_render.txt", log.ToString());
            Debug.Log($"{LogPrefix} T3\n{log}");
            UnityEngine.Object.DestroyImmediate(lr.gameObject);
            ReleaseRig(cam, rt, mat);
        }

        // ================================================================== T4

        [UnityTest]
        public IEnumerator T4_라인메시에는_바깥_방향을_실을_곳이_없다()
        {
            var cam = MakeCamera();
            float turn = Mathf.PI - 126f * Mathf.Deg2Rad;
            var knee = new[] { new Vector2(-0.5f, 0f), new Vector2(0f, 0f),
                               new Vector2(Mathf.Cos(-turn) * 0.5f, Mathf.Sin(-turn) * 0.5f) };
            var lr = MakeLine("AttrProbe", knee, 0.16f, false);
            yield return null;
            var mesh = new Mesh();
            lr.BakeMesh(mesh, cam, false);

            var sb = new StringBuilder();
            sb.AppendLine($"# T4 정점 속성 census  vertexCount={mesh.vertexCount}");
            sb.AppendLine($"normals={mesh.normals.Length} tangents={mesh.tangents.Length} colors32={mesh.colors32.Length}");
            foreach (var d in mesh.GetVertexAttributes())
                sb.AppendLine($"  {d.attribute} format={d.format} dim={d.dimension} stream={d.stream}");
            var uvCounts = new int[8];
            for (int ch = 0; ch < 8; ch++)
            { var l = new List<Vector4>(); mesh.GetUVs(ch, l); uvCounts[ch] = l.Count; sb.AppendLine($"uv{ch}={l.Count}"); }
            WriteTxt("T4_vertex_attributes.txt", sb.ToString());
            Debug.Log($"{LogPrefix} T4\n{sb}");

            var attrs = mesh.GetVertexAttributes().Select(a => a.attribute).OrderBy(a => (int)a).ToArray();
            int nNormals = mesh.normals.Length, nTangents = mesh.tangents.Length;
            UnityEngine.Object.DestroyImmediate(mesh);
            UnityEngine.Object.DestroyImmediate(lr.gameObject);
            UnityEngine.Object.DestroyImmediate(cam.gameObject);

            CollectionAssert.AreEqual(
                new[] { VertexAttribute.Position, VertexAttribute.Color, VertexAttribute.TexCoord0 }
                    .OrderBy(a => (int)a).ToArray(), attrs,
                $"{LogPrefix} 라인 메시의 정점 속성 집합이 바뀌었다: [{string.Join(", ", attrs)}]. " +
                "★ 대안 B(2패스)를 기각한 근거가 바로 이 셋뿐이라는 사실이다 — 속성이 늘었으면 " +
                "바깥 방향 벡터를 실을 수 있다는 뜻이고, B를 다시 재야 한다.");
            Assert.Zero(nNormals, $"{LogPrefix} 법선이 생겼다 — 대안 B를 다시 재라.");
            Assert.Zero(nTangents, $"{LogPrefix} 탄젠트가 생겼다 — 대안 B를 다시 재라.");
            for (int ch = 1; ch < 8; ch++)
                Assert.Zero(uvCounts[ch], $"{LogPrefix} uv{ch}가 생겼다 — 대안 B를 다시 재라.");
        }

        // ================================================================== T5

        [UnityTest]
        public IEnumerator T5_실제_리그에서_막이_관절과_종단에서_뚫린다()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore($"{LogPrefix} 그래픽스 디바이스가 Null이다(-nographics) — 실제 렌더는 " +
                              "못 봤다. 계산으로 메우지 않는다. ★ 자동 래칫: 그래픽스가 있는 러너에서는 " +
                              "이 메서드가 그대로 아래 실단언들로 간다(Ignore 뒤에 Assert가 8~11개 있다).");
            var shader = Shader.Find(ShaderName);
            Assert.IsNotNull(shader);

            var (cam, rt) = MakeRig(1024);
            var mat = new Material(shader);
            mat.SetFloat("_Mode", 2f); mat.SetFloat("_CoreHalf", (3.528f / 5.528f) / 2f);   // Win100% s=0.75
            mat.SetColor("_Ink", Color.black); mat.SetColor("_Membrane", Color.white);

            // 프리팹 실측 종횡비: 팔 마디 길이 0.2775 / 폭 0.078375 = 3.54 : 1
            float turn = Mathf.PI - 126f * Mathf.Deg2Rad;
            var upper = new[] { new Vector2(-0.35f, 0.25f), new Vector2(0f, 0.25f) };
            var lower = new[] { new Vector2(0f, 0.25f),
                                new Vector2(Mathf.Cos(-turn) * 0.35f, 0.25f + Mathf.Sin(-turn) * 0.35f) };
            var u = MakeLine("Upper", upper, 0.10f, false); u.gameObject.layer = ProbeLayer; u.sharedMaterial = mat;
            var d = MakeLine("Lower", lower, 0.10f, false); d.gameObject.layer = ProbeLayer; d.sharedMaterial = mat;
            yield return null;

            cam.transform.position = new Vector3(0f, 0.10f, -10f); cam.orthographicSize = 0.42f;
            var full = Capture(cam, rt, "T5_twoSegmentArm_full.png");
            cam.transform.position = new Vector3(0f, 0.25f, -10f); cam.orthographicSize = 0.13f;
            var elbow = Capture(cam, rt, "T5_twoSegmentArm_elbow.png");
            cam.transform.position = new Vector3(lower[1].x, lower[1].y, -10f); cam.orthographicSize = 0.11f;
            var hand = Capture(cam, rt, "T5_twoSegmentArm_hand.png");
            UnityEngine.Object.DestroyImmediate(u.gameObject);
            UnityEngine.Object.DestroyImmediate(d.gameObject);

            // ★ 대조: 같은 팔꿈치를 「하나의 폴리라인」으로 만들면 코너 팬이 되어 막이 온전해진다
            var single = new[] { upper[0], new Vector2(0f, 0.25f), lower[1] };
            var s1 = MakeLine("SinglePolyline", single, 0.10f, false);
            s1.gameObject.layer = ProbeLayer; s1.sharedMaterial = mat;
            yield return null;
            cam.transform.position = new Vector3(0f, 0.25f, -10f); cam.orthographicSize = 0.13f;
            var elbowSingle = Capture(cam, rt, "T5_singlePolyline_elbow.png");
            UnityEngine.Object.DestroyImmediate(s1.gameObject);

            foreach (var (name, s) in new[] { ("full", full), ("elbow", elbow), ("hand", hand), ("elbowSingle", elbowSingle) })
                AssertClassifierCalibrated(s, $"T5/{name}");

            var ef = full.Exposure(); var ee = elbow.Exposure();
            var eh = hand.Exposure(); var es = elbowSingle.Exposure();
            var log = new StringBuilder();
            log.AppendLine($"두마디팔 전체 : 잉크노출 {ef.ratio:P1} ({ef.inkOnBg}/{ef.inkOnBg + ef.memOnBg})");
            log.AppendLine($"팔꿈치(렌더러 2개) : {ee.ratio:P1}");
            log.AppendLine($"손끝           : {eh.ratio:P1}");
            log.AppendLine($"팔꿈치(단일 폴리라인 = 코너 팬) : {es.ratio:P1}   ← 대조군");
            WriteTxt("T5_render.txt", log.ToString());
            Debug.Log($"{LogPrefix} T5\n{log}");

            // 문턱 근거: 청정 실측 12.8%(2026-09-03, MSAA 끈 뒤). ★ 같은 장면을 MSAA 켠 채 재면
            //   27.2%가 나왔는데 그건 거짓이다 — 혼색된 배경 픽셀이 잉크로 분류됐다. 8%는 그 실측에
            //   여유를 둔 값이고, 결함이 사라지면(0에 가까워지면) 아래 메시지대로 빨개진다.
            Assert.Greater(ef.ratio, 0.08f,
                $"{LogPrefix} 두 마디 팔의 잉크 노출이 {ef.ratio:P1}로 떨어졌다 — 축하할 실패다. " +
                "대안 A의 캡 결함이 사라졌다는 뜻이니 다시 판정하라.");
            Assert.Greater(eh.ratio, 0.25f, $"{LogPrefix} 손끝(캡) 노출이 사라졌다 — 다시 판정하라.");
            Assert.Less(es.ratio, 0.02f,
                $"{LogPrefix} ★ 팔꿈치를 단일 폴리라인으로 만들어도 잉크가 노출된다({es.ratio:P1}). " +
                "「캡 결함은 리그가 마디마다 렌더러를 가른 결과다」라는 판정의 근거가 이 대조군이다.");
            Assert.Greater(ee.ratio, es.ratio + 0.02f,
                $"{LogPrefix} 렌더러 2개 팔꿈치({ee.ratio:P1})가 단일 폴리라인({es.ratio:P1})보다 " +
                "나쁘지 않다 — 대조가 성립하지 않는다.");

            ReleaseRig(cam, rt, mat);
        }

        // ================================================================== T6

        [UnityTest]
        public IEnumerator T6_하한을_전체폭에_걸면_Win100_최저배율에서_잉크가_0이_된다()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null; yield return null; yield return null;

            var agent = UnityEngine.Object.FindFirstObjectByType<StickMate.Core.StickmanAgent>();
            Assert.IsNotNull(agent);
            var cam = Camera.main != null ? Camera.main : UnityEngine.Object.FindFirstObjectByType<Camera>();
            Assert.IsNotNull(cam);

            // ★ 상수를 숫자로 베끼지 않는다 — 전부 프로덕션 상수를 참조해서 유도한다.
            const float MembranePhysicalPx = 1f;                 // design-art §22-3 (OutlineRingMinPhysicalPixels 재사용)
            const float PtPerPxAtWin100 = 1f;                    // Windows 표시배율 100%
            const float PtPerPxAtRetina = 0.5f;                  // macOS Retina / Windows 200%

            float floorLine = StickMate.Core.StickConfig.MinStrokeScreenPoints;
            float floorFill = StickMate.Core.StickConfig.MinFillOutlineScreenPoints;

            var sb = new StringBuilder();
            sb.AppendLine($"MinStrokeScreenPoints={floorLine}  MinFillOutlineScreenPoints={floorFill}");
            sb.AppendLine($"MinCharacterScale={StickMate.Core.StickConfig.MinCharacterScale}");
            sb.AppendLine($"BakedCharacterScale={agent.BakedCharacterScale} Current={agent.CurrentCharacterScale}");
            sb.AppendLine($"MinStrokeWorldWidth={agent.MinStrokeWorldWidth}");
            sb.AppendLine($"Screen {Screen.width}x{Screen.height} ortho={cam.orthographicSize}");
            foreach (var lr in agent.GetComponentsInChildren<LineRenderer>(true))
                sb.AppendLine($"  {lr.gameObject.name}\t{lr.startWidth:F6}");

            // (1) 하한을 「전체 폭」에 걸면 잉크 코어가 무엇이 되는가 — 하한에 눌린 배율에서
            float coreIfFloorOnTotal_Win = floorLine / PtPerPxAtWin100 - 2f * MembranePhysicalPx;
            float coreIfFloorOnTotal_Mac = floorLine / PtPerPxAtRetina - 2f * MembranePhysicalPx;
            float ringCoreOnTotal_Win = floorFill / PtPerPxAtWin100 - 1f * MembranePhysicalPx; // 머리 막은 바깥 한 겹
            sb.AppendLine($"\n하한을 전체폭에 걸 때 잉크코어(px): Win100%={coreIfFloorOnTotal_Win} " +
                          $"Retina={coreIfFloorOnTotal_Mac} 머리링Win100%={ringCoreOnTotal_Win}");
            WriteTxt("T6_floor.txt", sb.ToString());
            Debug.Log($"{LogPrefix} T6\n{sb}");

            Assert.LessOrEqual(coreIfFloorOnTotal_Win, 0f,
                $"{LogPrefix} ★ 하한을 전체 폭에 걸면 Windows 100% 최저 배율에서 잉크 코어가 " +
                $"{coreIfFloorOnTotal_Win}px다. 0 이하라는 것이 「하한 계약을 잉크 코어 기준으로 다시 써야 " +
                "한다」의 근거다. 이 값이 양수가 되었다면 하한 상수가 바뀐 것이니 그 판정을 다시 하라.");
            Assert.LessOrEqual(ringCoreOnTotal_Win, 0f,
                $"{LogPrefix} 머리 링도 같은 함정이다(코어 {ringCoreOnTotal_Win}px).");
            Assert.Greater(coreIfFloorOnTotal_Mac, 0f,
                $"{LogPrefix} macOS Retina에서는 잉크가 남아야 한다(코어 {coreIfFloorOnTotal_Mac}px) — " +
                "이 함정이 Windows 전용임을 못박는 대조다.");
            Assert.Less(coreIfFloorOnTotal_Mac, floorLine / PtPerPxAtRetina,
                $"{LogPrefix} Retina에서도 코어가 하한이 뜻한 두께보다 얇아져야 한다(대조).");

            // (2) ★ 게이트가 「전체 폭」을 읽는다는 것을 실행해서 증명한다.
            //     A를 넣으면 이 게이트는 잉크가 0px여도 「하한 지킴」을 찍는다.
            var rep = StickMate.Platform.StrokeWidthDiagnostics.Measure(cam, agent.Config);
            float ppu = rep.PixelsPerWorldUnit;
            Assert.Greater(ppu, 0f, $"{LogPrefix} 픽셀/유닛이 0 — 카메라가 직교가 아니다.");
            float minStartWidth = UnityEngine.Object
                .FindObjectsByType<LineRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(l => l != null && l.startWidth * ppu > 0f)
                .Min(l => l.startWidth);
            Assert.AreEqual(minStartWidth * ppu, rep.MinPixels, 1e-3f,
                $"{LogPrefix} ★ StrokeWidthDiagnostics.MinPixels 가 렌더러의 startWidth(= <b>그려진</b> 폭)를 " +
                "그대로 읽는다는 증명이다. " +
                "★★ 2026-09-03 갱신 — 이 프로브가 요구한 수정이 <b>들어갔다</b>: 계측기는 이제 " +
                "MinLineInkCorePoints/MinFillOutlineInkCorePoints 로 <b>막을 되뺀 잉크 코어</b>를 따로 재고 " +
                "FloorHonored 도 그 값으로 판정한다(Core/InkMembraneStroke). MinPixels 는 " +
                "「화면에 실제로 찍히는 폭」이라는 원래 뜻 그대로 남겨 둔 것이라 이 단언은 계속 유효하다. " +
                "이 단언이 깨졌다면 MinPixels 의 뜻이 바뀐 것이니 그때 위 문장을 다시 갱신하라.");
            Debug.Log($"{LogPrefix} [렌더품질] {StickMate.Platform.StrokeWidthDiagnostics.Describe(rep)}");
        }

        // ================================================================== T7 — 대안 A′

        [UnityTest]
        public IEnumerator T7_대안Aprime은_캡을_없애서_막을_닫지만_관절에_쐐기를_연다()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore($"{LogPrefix} 그래픽스 디바이스가 Null이다(-nographics) — 실제 렌더는 " +
                              "못 봤다. 계산으로 메우지 않는다. ★ 자동 래칫: 그래픽스가 있는 러너에서는 " +
                              "이 메서드가 그대로 아래 실단언들로 간다(Ignore 뒤에 Assert가 8~11개 있다).");
            var shader = Shader.Find(ShaderName);
            Assert.IsNotNull(shader);

            var (cam, rt) = MakeRig(1024);
            var mat = new Material(shader);
            mat.SetColor("_Ink", Color.black); mat.SetColor("_Membrane", Color.white);
            float R = 3.528f / 5.528f;                 // Win100% s=0.75
            mat.SetFloat("_CoreHalf", R / 2f);

            var log = new StringBuilder();
            float turn = Mathf.PI - 126f * Mathf.Deg2Rad;
            const float width = 0.10f, half = 0.05f;
            var upper = new[] { new Vector2(-0.35f, 0.25f), new Vector2(0f, 0.25f) };
            var lower = new[] { new Vector2(0f, 0.25f),
                                new Vector2(Mathf.Cos(-turn) * 0.35f, 0.25f + Mathf.Sin(-turn) * 0.35f) };

            // ---- (1) 종단: A(캡8) vs A′(캡0). A′는 끝면에도 막이 있어야 한다
            var results = new Dictionary<string, float>();
            foreach (var (tag, capV, mode) in new[] { ("A_cap8", 8, 2f), ("Aprime_cap0", 0, 3f) })
            {
                var one = MakeLine("EndProbe", upper, width, false, capV);
                one.gameObject.layer = ProbeLayer; one.sharedMaterial = mat;
                mat.SetFloat("_Mode", mode);
                yield return null;
                cam.transform.position = new Vector3(-0.35f, 0.25f, -10f); cam.orthographicSize = 0.10f;
                var s = Capture(cam, rt, $"T7_end_{tag}.png");
                AssertClassifierCalibrated(s, $"T7 종단/{tag}");
                var e = s.Exposure();
                results["end_" + tag] = e.ratio;
                log.AppendLine($"종단 {tag,-12}: 잉크노출 {e.ratio:P1}");
                UnityEngine.Object.DestroyImmediate(one.gameObject);
            }

            // ---- (2) 관절: 단색 잉크 대조군으로 「조형 대가」를 셰이딩과 분리해 잰다
            mat.SetFloat("_Mode", 4f);   // 단색 잉크, 막 없음
            foreach (var (tag, capV) in new[] { ("cap8", 8), ("cap0", 0) })
            {
                var u = MakeLine("U", upper, width, false, capV); u.gameObject.layer = ProbeLayer; u.sharedMaterial = mat;
                var d = MakeLine("D", lower, width, false, capV); d.gameObject.layer = ProbeLayer; d.sharedMaterial = mat;
                yield return null;
                cam.transform.position = new Vector3(0f, 0.25f, -10f); cam.orthographicSize = 0.10f;
                var s = Capture(cam, rt, $"T7_jointSolid_{tag}.png");
                Assert.Zero(s.Count(Px.OTHER), $"{LogPrefix} T7 관절/{tag}: 분류 불가 픽셀이 있다.");

                // 조인트 중심 반경 rho 원 안에 배경이 얼마나 침입하는가
                float ppu = rt.width / (2f * cam.orthographicSize);
                float cx = rt.width * 0.5f, cy = rt.height * 0.5f;
                float rpx = half * ppu;
                int hole = 0, inside = 0; float deepest = rpx;
                for (int y = 0; y < rt.height; y++)
                    for (int x = 0; x < rt.width; x++)
                    {
                        float dx = x - cx, dy = y - cy, rr = Mathf.Sqrt(dx * dx + dy * dy);
                        if (rr > rpx) continue;
                        inside++;
                        if (s.At(x, y) == Px.BG) { hole++; if (rr < deepest) deepest = rr; }
                    }
                float frac = (float)hole / inside;
                results["joint_" + tag] = frac;
                results["jointDepth_" + tag] = deepest / rpx;   // 0 = 구멍이 조인트 중심까지 닿는다
                log.AppendLine($"관절 단색 {tag,-6}: 조인트 원({rpx:F0}px) 안 배경 {frac:P1} " +
                               $"(구멍 {hole}/{inside}), 가장 깊은 구멍의 조인트까지 거리 {deepest:F1}px " +
                               $"= 반폭의 {deepest / rpx:P0}");
                UnityEngine.Object.DestroyImmediate(u.gameObject);
                UnityEngine.Object.DestroyImmediate(d.gameObject);
            }

            // ---- (3) 관절: A′ 셰이딩으로도 한 장 (design-character 판정용 그림)
            mat.SetFloat("_Mode", 3f);
            {
                var u = MakeLine("U", upper, width, false, 0); u.gameObject.layer = ProbeLayer; u.sharedMaterial = mat;
                var d = MakeLine("D", lower, width, false, 0); d.gameObject.layer = ProbeLayer; d.sharedMaterial = mat;
                yield return null;
                cam.transform.position = new Vector3(0f, 0.10f, -10f); cam.orthographicSize = 0.42f;
                Capture(cam, rt, "T7_Aprime_arm_full.png");
                cam.transform.position = new Vector3(0f, 0.25f, -10f); cam.orthographicSize = 0.10f;
                var s = Capture(cam, rt, "T7_Aprime_arm_elbow.png");
                var e = s.Exposure();
                results["elbow_Aprime"] = e.ratio;
                log.AppendLine($"관절 A′ 셰이딩: 잉크노출 {e.ratio:P1}");
                UnityEngine.Object.DestroyImmediate(u.gameObject);
                UnityEngine.Object.DestroyImmediate(d.gameObject);
            }

            // ---- (4) ★ 구제안: 마디를 하나의 폴리라인으로 합치면 관절이 코너 팬이 된다
            var single = new[] { upper[0], new Vector2(0f, 0.25f), lower[1] };
            {
                mat.SetFloat("_Mode", 4f);                       // 단색: 관절 구멍이 있는가
                var one = MakeLine("Single", single, width, false, 0);
                one.gameObject.layer = ProbeLayer; one.sharedMaterial = mat;
                yield return null;
                cam.transform.position = new Vector3(0f, 0.25f, -10f); cam.orthographicSize = 0.10f;
                var solid = Capture(cam, rt, "T7_jointSolid_single_cap0.png");
                float ppu = rt.width / (2f * cam.orthographicSize);
                float cx = rt.width * 0.5f, cy = rt.height * 0.5f, rpx = half * ppu;
                int hole = 0, inside = 0; float deepest = rpx;
                for (int y = 0; y < rt.height; y++)
                    for (int x = 0; x < rt.width; x++)
                    {
                        float dx = x - cx, dy = y - cy, rr = Mathf.Sqrt(dx * dx + dy * dy);
                        if (rr > rpx) continue;
                        inside++;
                        if (solid.At(x, y) == Px.BG) { hole++; if (rr < deepest) deepest = rr; }
                    }
                results["jointSingle_cap0"] = (float)hole / inside;
                results["jointSingleDepth_cap0"] = deepest / rpx;
                log.AppendLine($"관절 단색 단일폴리라인+캡0 : 조인트 원 안 배경 {(float)hole / inside:P2}, " +
                               $"가장 깊은 구멍 거리 {deepest / rpx:P0} ← 구제안");

                mat.SetFloat("_Mode", 3f);                       // A′ 셰이딩
                yield return null;
                var sh = Capture(cam, rt, "T7_Aprime_single_elbow.png");
                AssertClassifierCalibrated(sh, "T7 단일폴리라인 A′");
                var e2 = sh.Exposure();
                results["elbowSingle_Aprime"] = e2.ratio;
                log.AppendLine($"관절 A′ + 단일 폴리라인 : 잉크노출 {e2.ratio:P1} ← 구제안");
                cam.transform.position = new Vector3(0f, 0.10f, -10f); cam.orthographicSize = 0.42f;
                Capture(cam, rt, "T7_Aprime_single_full.png");
                UnityEngine.Object.DestroyImmediate(one.gameObject);
            }

            WriteTxt("T7_aprime.txt", log.ToString());
            Debug.Log($"{LogPrefix} T7 — 대안 A′\n{log}");

            // ---- 단언
            // 문턱 근거는 전부 2026-09-03 청정 실측(MSAA 끔). 괄호 안이 실측값.
            Assert.Greater(results["end_A_cap8"], 0.15f,
                $"{LogPrefix} 대조가 성립하지 않는다 — A(캡8)의 종단 노출이 {results["end_A_cap8"]:P1}다(실측 25.8%). " +
                "A′가 무엇을 고쳤는지 말하려면 A가 먼저 뚫려 있어야 한다.");
            Assert.Less(results["end_Aprime_cap0"], 0.02f,
                $"{LogPrefix} ★ A′가 종단을 못 막았다({results["end_Aprime_cap0"]:P1}, 실측 0.0%). " +
                "numCapVertices=0 + uv.x 끝면 밴드(fwidth 자기교정)가 끝면에도 막을 만들어야 한다.");

            // ★ 관절 — 조형 대가를 셰이딩과 분리해 재는 단색 대조군.
            //   cap8(현행): 구멍이 있어도 <b>바깥 테두리 한 겹</b>뿐이어야 한다(깊이 = 반폭의 98%).
            //   cap0(A′)  : 구멍이 <b>조인트 중심까지</b> 닿는다(깊이 0%). 그리고 넓이는 꺾임각/360도.
            Assert.Less(results["joint_cap8"], 0.01f,
                $"{LogPrefix} 둥근 캡(현행)인데 조인트 원 안 구멍이 {results["joint_cap8"]:P2}다(실측 0.3%). " +
                "이 대조군이 거의 0이어야 A′의 쐐기가 캡 제거 탓임을 말할 수 있다.");
            Assert.Greater(results["jointDepth_cap8"], 0.90f,
                $"{LogPrefix} 둥근 캡의 잔여 구멍이 안쪽까지 들어왔다(깊이 반폭의 {results["jointDepth_cap8"]:P0}). " +
                "현행에서는 그 구멍이 바깥 테두리 이산화 오차여야 한다.");

            float turnDeg = 180f - 126f;                       // 무릎 내각 126도 -> 꺾임 54도
            Assert.AreEqual(turnDeg / 360f, results["joint_cap0"], 0.02f,
                $"{LogPrefix} ★ A′(캡0)의 관절 쐐기 넓이 {results["joint_cap0"]:P1}가 해석해 " +
                $"(꺾임각/360도 = {turnDeg / 360f:P1})와 다르다. 이 일치가 " +
                "「둥근 캡이 메우던 자리가 그대로 열린다」의 정량 근거다(실측 15.1% vs 이론 15.0%).");
            Assert.Less(results["jointDepth_cap0"], 0.05f,
                $"{LogPrefix} ★ A′의 쐐기가 조인트 중심까지 닿지 않는다(깊이 반폭의 {results["jointDepth_cap0"]:P0}). " +
                "닿는다는 것이 이 대가의 무게다 — 실루엣이 관절에서 중심선까지 파인다.");

            // ★ 구제안: 마디를 <b>하나의 폴리라인</b>으로 합치면 관절이 코너 팬이 되어 쐐기가 사라진다.
            // ★ 자기 정정(2026-09-03): 나는 "단일 폴리라인이면 구멍 0"이라고 예측했는데 실측은 3.6%였다.
            //   그러나 그건 <b>쐐기가 아니다</b> — 깊이를 재 보면 조인트에서 반폭의 ~88%지 0이 아니다.
            //   원인: Unity의 둥근 조인은 <b>안쪽 오프셋 점을 중심으로 반지름 2ρ</b>인 팬이라
            //   바깥 호가 조인트 기준 원보다 살짝 안쪽(≈0.88ρ)으로 들어온다. 조인이 납작한 것이지
            //   실루엣이 파인 것이 아니다. 두 렌더러 캡0(깊이 0%)과는 <b>종류가 다르다</b>.
            Assert.Less(results["jointSingle_cap0"], 0.06f,
                $"{LogPrefix} 단일 폴리라인 + 캡0의 조인트 원 안 배경이 {results["jointSingle_cap0"]:P2}다(실측 3.6%).");
            Assert.Greater(results["jointSingleDepth_cap0"], 0.80f,
                $"{LogPrefix} ★ 단일 폴리라인의 구멍이 안쪽까지 파였다(깊이 반폭의 {results["jointSingleDepth_cap0"]:P0}). " +
                "0.80을 넘어야 「납작한 조인」이지 「쐐기」가 아니라고 말할 수 있다 — " +
                $"두 렌더러 캡0은 같은 자리에서 깊이 {results["jointDepth_cap0"]:P0}다.");
            Assert.Less(results["elbowSingle_Aprime"], 0.02f,
                $"{LogPrefix} 단일 폴리라인 + A′에서 잉크가 노출된다({results["elbowSingle_Aprime"]:P1}). " +
                "이 조합이 0이어야 「A′ + 마디 병합 = 결함 0」이라고 말할 수 있다.");

            Debug.Log($"{LogPrefix} T7 요약: 종단 A={results["end_A_cap8"]:P1} -> A′={results["end_Aprime_cap0"]:P1} / " +
                      $"관절 구멍 캡8={results["joint_cap8"]:P2} -> 캡0={results["joint_cap0"]:P2}");

            ReleaseRig(cam, rt, mat);
        }
    }
}
