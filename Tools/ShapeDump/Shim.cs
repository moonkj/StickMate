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
    }

    public static class Mathf
    {
        public const float Deg2Rad = (float)(Math.PI * 2.0 / 360.0);
        public const float Rad2Deg = (float)(360.0 / (Math.PI * 2.0));
        public const float PI = (float)Math.PI;
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Abs(float a) => Math.Abs(a);
        public static float Sin(float a) => (float)Math.Sin(a);
        public static float Cos(float a) => (float)Math.Cos(a);
        public static float Asin(float a) => (float)Math.Asin(a);
        public static float Sqrt(float a) => (float)Math.Sqrt(a);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
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
