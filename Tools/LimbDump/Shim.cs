// UnityEngine 최소 대역 — States/LimbCurveRenderer.cs 를 <b>한 줄도 고치지 않고</b> 컴파일하기 위한 것.
// Tools/ShapeDump/Shim.cs 와 같은 원리이고 같은 규칙을 따른다: 흉내내는 것은 UnityEngine 뿐이고,
// 기하 수식은 프로덕션 파일 그대로 돈다.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0f, 0f, 0f);
        public static Vector3 one => new Vector3(1f, 1f, 1f);
        public override string ToString() => $"({x:R}, {y:R}, {z:R})";
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => new Quaternion(0f, 0f, 0f, 1f);
        /// <summary>Z축만 쓰는 리그이므로 z 회전만 만든다(프리팹이 그렇게 구워진다).</summary>
        public static Quaternion EulerZ(float degrees)
        {
            float h = degrees * Mathf.Deg2Rad * 0.5f;
            return new Quaternion(0f, 0f, (float)Math.Sin(h), (float)Math.Cos(h));
        }
    }

    public static class Mathf
    {
        public const float Deg2Rad = 0.0174532924f;
        public const float Rad2Deg = 57.29578f;
        public static float Abs(float v) => Math.Abs(v);
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Sin(float v) => (float)Math.Sin(v);
        public static float Cos(float v) => (float)Math.Cos(v);
        public static float Tan(float v) => (float)Math.Tan(v);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
        public static bool Approximately(float a, float b)
            => Math.Abs(b - a) < Math.Max(1E-06f * Math.Max(Math.Abs(a), Math.Abs(b)), 1.121039E-44f);
        public static float DeltaAngle(float current, float target)
        {
            float d = Repeat(target - current, 360f);
            if (d > 180f) d -= 360f;
            return d;
        }
        private static float Repeat(float t, float length)
            => Clamp(t - (float)Math.Floor(t / length) * length, 0f, length);
    }

    public static class Debug
    {
        public static void Log(object m) => Console.Error.WriteLine("[Log] " + m);
        public static void LogWarning(object m) => Console.Error.WriteLine("[Warn] " + m);
        public static void LogError(object m) => Console.Error.WriteLine("[Error] " + m);
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DisallowMultipleComponentAttribute : Attribute { }

    public class GameObject
    {
        public string name;
        public readonly Transform transform;
        public GameObject(string name) { this.name = name; transform = new Transform(this); }
    }

    public class Component
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T : Component => gameObject.transform.Get<T>();
        public bool TryGetComponent<T>(out T value) where T : Component
        {
            value = gameObject.transform.Get<T>();
            return value != null;
        }
    }

    public class Behaviour : Component { public bool enabled = true; }
    public class MonoBehaviour : Behaviour { }

    public sealed class Transform : Component
    {
        private readonly List<Transform> _children = new List<Transform>();
        private readonly List<Component> _components = new List<Component>();
        public Transform parent;
        public Vector3 localPosition;
        public Quaternion localRotation = Quaternion.identity;
        public Vector3 localScale = Vector3.one;

        public Transform(GameObject go) { gameObject = go; }
        public string name => gameObject.name;
        public int childCount => _children.Count;
        public Transform GetChild(int i) => _children[i];
        public void AddChild(Transform t) { t.parent = this; _children.Add(t); }
        public T Add<T>(T c) where T : Component { c.gameObject = gameObject; _components.Add(c); return c; }
        public T Get<T>() where T : Component
        {
            for (int i = 0; i < _components.Count; i++) if (_components[i] is T t) return t;
            return null;
        }
        public Vector3 lossyScale
        {
            get
            {
                float sx = localScale.x, sy = localScale.y, sz = localScale.z;
                for (Transform p = parent; p != null; p = p.parent)
                { sx *= p.localScale.x; sy *= p.localScale.y; sz *= p.localScale.z; }
                return new Vector3(sx, sy, sz);
            }
        }
    }

    public sealed class BoxCollider2D : Component { public Vector2 size; public Vector2 offset; }

    public sealed class LineRenderer : Behaviour
    {
        private Vector3[] _positions = new Vector3[0];
        public float startWidth, endWidth;
        public bool useWorldSpace;
        public int positionCount
        {
            get => _positions.Length;
            set { if (_positions.Length != value) Array.Resize(ref _positions, value); }
        }
        public Vector3 GetPosition(int i) => _positions[i];
        public void SetPosition(int i, Vector3 p) { _positions[i] = p; }
        public void SetPositions(Vector3[] p) { for (int i = 0; i < _positions.Length && i < p.Length; i++) _positions[i] = p[i]; }
    }
}
