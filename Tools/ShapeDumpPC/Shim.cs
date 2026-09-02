// 오프라인 좌표 덤프 전용 셰이딩 스텁. 프로덕션 파일은 한 줄도 고치지 않는다.
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
}
