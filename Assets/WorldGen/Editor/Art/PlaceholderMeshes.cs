using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Flat-shaded low-poly primitives for placeholder props.</summary>
    public static class PlaceholderMeshes
    {
        public static void AddCylinder(MeshData m, int sub, Vector3 baseCenter, float radius, float height, int sides)
        {
            var top = baseCenter + Vector3.up * height;
            for (int i = 0; i < sides; i++)
            {
                float a0 = 2f * Mathf.PI * i / sides, a1 = 2f * Mathf.PI * (i + 1) / sides;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                var outward = ((d0 + d1) * 0.5f).normalized;
                m.AddQuad(sub, baseCenter + d0, baseCenter + d1, top + d1, top + d0,
                    new Vector2(i, 0f), new Vector2(i + 1, 0f), new Vector2(i + 1, 1f), new Vector2(i, 1f), outward);
                m.AddTriangle(sub, top, top + d0, top + d1, Vector2.zero, Vector2.right, Vector2.up, Vector3.up);
                m.AddTriangle(sub, baseCenter, baseCenter + d0, baseCenter + d1, Vector2.zero, Vector2.right, Vector2.up, Vector3.down);
            }
        }

        public static void AddCone(MeshData m, int sub, Vector3 baseCenter, float radius, float height, int sides)
        {
            var apex = baseCenter + Vector3.up * height;
            for (int i = 0; i < sides; i++)
            {
                float a0 = 2f * Mathf.PI * i / sides, a1 = 2f * Mathf.PI * (i + 1) / sides;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                var mid = ((d0 + d1) * 0.5f).normalized;
                m.AddTriangle(sub, baseCenter + d0, baseCenter + d1, apex,
                    new Vector2(i, 0f), new Vector2(i + 1, 0f), new Vector2(i + 0.5f, 1f), (mid * height + Vector3.up * radius).normalized);
                m.AddTriangle(sub, baseCenter, baseCenter + d0, baseCenter + d1, Vector2.zero, Vector2.right, Vector2.up, Vector3.down);
            }
        }

        public static void AddIcosahedron(MeshData m, int sub, Vector3 center, float radius)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            for (int i = 0; i < v.Length; i++) v[i] = center + v[i].normalized * radius;
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
            for (int i = 0; i < f.Length; i += 3)
            {
                Vector3 a = v[f[i]], b = v[f[i + 1]], c = v[f[i + 2]];
                m.AddTriangle(sub, a, b, c, Vector2.zero, Vector2.right, Vector2.up, (a + b + c) / 3f - center);
            }
        }

        public static void AddBox(MeshData m, int sub, Vector3 center, Vector3 size)
        {
            Vector3 half = size * 0.5f;
            foreach (var n in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
            {
                Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 w = Vector3.Cross(n, u);
                Vector3 c = center + Vector3.Scale(n, half);
                Vector3 du = Vector3.Scale(u, half), dw = Vector3.Scale(w, half);
                m.AddQuad(sub, c - du - dw, c + du - dw, c + du + dw, c - du + dw, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, n);
            }
        }
    }
}
