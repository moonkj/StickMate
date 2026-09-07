// 오프라인 좌표 덤프 전용 셰이딩 스텁. 프로덕션 파일은 한 줄도 고치지 않는다.
//
// ★ 규약 (2026-09-02): 여기 있는 것은 **UnityEngine 흉내**뿐이다. StickMate.Core/Interaction 타입은
//   가능하면 프로덕션 파일을 그대로 컴파일한다(build.sh 목록). 흉내낸 StickMate 타입은
//   CoreShim.cs 한 곳에만 두고 shimdrift.py 의 허용 목록에 이유와 함께 등록한다 —
//   흉내가 늘어날수록 이 하니스가 재는 것이 프로덕션에서 멀어진다.
using System;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float magnitude => (float)Math.Sqrt(x * x + y * y);
        public float sqrMagnitude => x * x + y * y;
        public static Vector2 zero => new Vector2(0f, 0f);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator *(Vector2 a, float s) => new Vector2(a.x * s, a.y * s);
        public static Vector2 operator /(Vector2 a, float s) => new Vector2(a.x / s, a.y / s);
        public static Vector2 Min(Vector2 a, Vector2 b) => new Vector2(Math.Min(a.x, b.x), Math.Min(a.y, b.y));
        public static Vector2 Max(Vector2 a, Vector2 b) => new Vector2(Math.Max(a.x, b.x), Math.Max(a.y, b.y));
        public static float Angle(Vector2 a, Vector2 b)
        {
            float d = (float)Math.Sqrt(a.sqrMagnitude * (double)b.sqrMagnitude);
            if (d < 1e-15f) return 0f;
            float c = Math.Max(-1f, Math.Min(1f, (a.x * b.x + a.y * b.y) / d));
            return (float)Math.Acos(c) * Mathf.Rad2Deg;
        }
        public override string ToString() => $"({x},{y})";
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        // ★ 2026-09-08 coder-systems — <b>흉내가 늘어난 것이 아니라 대역이 모자랐다.</b>
        //   AccessoryShapeBuilder.FxPetCard.cs(생성 파일)가 AppearanceShapeBuilder 상수를 부르면서
        //   그 파일이 컴파일 목록에 딸려 들어왔고, 아래 셋이 없어 하니스가 통째로 죽어 있었다
        //   (README 가 경고한 «게이트가 빨간 상태가 아니라 안 도는 상태»). 셋 다 UnityEngine 원문 그대로다.
        public static Vector3 zero => new Vector3(0f, 0f, 0f);
        public static Vector3 operator *(float s, Vector3 v) => new Vector3(s * v.x, s * v.y, s * v.z);
        public static Vector3 operator *(Vector3 v, float s) => new Vector3(v.x * s, v.y * s, v.z * s);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static float Distance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dy = a.y - b.y, dz = a.z - b.z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1f, 1f, 1f, 1f);

        // ★ UnityEngine.Color 의 HSV 변환 그대로. 착용 색(ItemCatalog.WornColor)이 부르는 자리라
        //   흉내가 아니라 <b>같은 식</b>이어야 한다(Unity 소스의 RGBToHSV/HSVToRGB 알고리즘).
        public static void RGBToHSV(Color rgb, out float h, out float s, out float v)
        {
            float max = Math.Max(rgb.r, Math.Max(rgb.g, rgb.b));
            float min = Math.Min(rgb.r, Math.Min(rgb.g, rgb.b));
            v = max;
            float d = max - min;
            if (max <= 0f) { h = 0f; s = 0f; return; }
            s = d / max;
            if (d <= 0f) { h = 0f; return; }
            if (max == rgb.r) h = (rgb.g - rgb.b) / d % 6f;
            else if (max == rgb.g) h = (rgb.b - rgb.r) / d + 2f;
            else h = (rgb.r - rgb.g) / d + 4f;
            h /= 6f;
            if (h < 0f) h += 1f;
        }

        public static Color HSVToRGB(float h, float s, float v)
        {
            if (s <= 0f) return new Color(v, v, v, 1f);
            h = h - (float)Math.Floor(h);
            float sector = h * 6f;
            int i = (int)Math.Floor(sector);
            float f = sector - i;
            float p = v * (1f - s);
            float q = v * (1f - s * f);
            float t = v * (1f - s * (1f - f));
            switch (i)
            {
                case 0: return new Color(v, t, p, 1f);
                case 1: return new Color(q, v, p, 1f);
                case 2: return new Color(p, v, t, 1f);
                case 3: return new Color(p, q, v, 1f);
                case 4: return new Color(t, p, v, 1f);
                default: return new Color(v, p, q, 1f);
            }
        }

        /// <summary>UnityEngine.Color.Lerp 원문 이식 — 성분별 선형보간, t 는 Clamp01.
        /// (2026-09-06: R20 유리 재질이 카드 바탕 위에 M2 를 사전 합성하면서 프로덕션이 쓰기 시작했다.
        ///  <c>AccessoryShapeBuilder.cs</c> AccessoryTone.Glass.)</summary>
        public static Color Lerp(Color a, Color b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t,
                             a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
        }
    }

    public static class Mathf
    {
        public const float Deg2Rad = (float)(Math.PI * 2.0 / 360.0);
        public const float Rad2Deg = (float)(360.0 / (Math.PI * 2.0));
        public const float PI = (float)Math.PI;
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Abs(float a) => Math.Abs(a);
        public static float Sin(float a) => (float)Math.Sin(a);
        public static float Cos(float a) => (float)Math.Cos(a);
        public static float Asin(float a) => (float)Math.Asin(a);
        public static float Sqrt(float a) => (float)Math.Sqrt(a);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        // ↓ 아래 둘은 <b>오늘의 컴파일 목록에서는 안 불린다</b>(2026-09-06 실측). 그래도 둔 이유:
        //   · Clamp(int,int,int) 가 없으면 int 인자가 <b>조용히 float 판</b>에 붙는다. 결과를 int 로
        //     받으면 CS0266 으로 시끄럽지만, float 로 받는 자리였다면 아무 말 없이 통과한다.
        //   · FloorToInt 를 (int) 캐스트로 때우면 음수에서 조용히 1 차이가 난다(-0.5 -> 0 vs -1).
        //   둘 다 UnityEngine 원문 그대로라 「흉내가 늘었다」에 해당하지 않는다.
        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
        public static int FloorToInt(float f) => (int)Math.Floor(f);
        // UnityEngine.Mathf.Sign: 0 은 <b>+1</b> 이다(수학의 sgn 과 다르다). 여기를 0으로 두면
        // AccessoryShapeBuilder 의 좌우 밀기가 중앙선 위 점에서만 조용히 달라진다.
        public static float Sign(float f) => f >= 0f ? 1f : -1f;
        // UnityEngine.Mathf.InverseLerp 원문 이식: a==b면 0, 그 밖에는 Clamp01((v-a)/(b-a)).
        public static float InverseLerp(float a, float b, float v)
            => a != b ? Clamp01((v - a) / (b - a)) : 0f;
    }

    public enum HideFlags { None = 0, DontSave = 52 }

    public class Mesh
    {
        public string name;
        public HideFlags hideFlags;
        public Vector3[] vertices;
        public Color[] colors;
        public int[] triangles;
        public void RecalculateBounds() { }
    }

    // ---- 에셋을 읽는 프로덕션 코드(ItemCatalog/AccessoryDefSO)를 그대로 컴파일하기 위한 최소 대역 ----

    public class Object
    {
        public string name;
        public HideFlags hideFlags;
    }

    public class ScriptableObject : Object
    {
    }

    /// <summary>★ 로그는 <b>버리지 않는다</b>. 이 하니스가 조용히 반쪽만 재는 것을 막는 유일한 신호다.
    /// stdout 은 좌표 스트림이라 오염시키면 안 되므로 전부 stderr 로 보낸다.</summary>
    public static class Debug
    {
        public static int ErrorCount { get; private set; }
        public static int WarningCount { get; private set; }

        public static void Log(object message) => Console.Error.WriteLine("[log] " + message);
        public static void LogWarning(object message)
        {
            WarningCount++;
            Console.Error.WriteLine("[warn] " + message);
        }
        public static void LogError(object message)
        {
            ErrorCount++;
            Console.Error.WriteLine("!! [error] " + message);
        }
    }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string header) { } }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string tooltip) { } }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public sealed class TextAreaAttribute : Attribute
    {
        public TextAreaAttribute() { }
        public TextAreaAttribute(int minLines, int maxLines) { }
    }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public sealed class SerializeFieldAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public sealed class RangeAttribute : Attribute { public RangeAttribute(float min, float max) { } }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class CreateAssetMenuAttribute : Attribute
    {
        public string fileName;
        public string menuName;
        public int order;
    }
}
