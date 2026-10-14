using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// Light stand-ins for Unity's sphere, capsule and cylinder primitives, the same size and orientation (sphere
    /// diameter 1; capsule and cylinder 2 tall along Y with radius 0.5) at a tenth of the triangles: kampung people and
    /// props are many and small. Built on first use and kept for the session; reset when Play starts (domain reload is off).
    /// </summary>
    public static class LowPolyMeshes
    {
        /// <summary>Around the axis; a multiple of 4 so the mesh reaches the full width on both x and z.</summary>
        public const int Segments = 12;
        const int SphereRings = 6;
        const int CapRings = 3;

        static Mesh sphere, capsule, cylinder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetStatics()
        {
            Release(ref sphere);
            Release(ref capsule);
            Release(ref cylinder);
        }

        static void Release(ref Mesh mesh)
        {
            if (mesh != null) Object.DestroyImmediate(mesh);
            mesh = null;
        }

        /// <summary>The low-poly mesh for a primitive type, or null for the cube (already 12 triangles) and others.</summary>
        public static Mesh For(PrimitiveType type)
        {
            switch (type)
            {
                case PrimitiveType.Sphere:
                    if (sphere == null) sphere = Lathe("LowPolySphere", SphereProfile());
                    return sphere;
                case PrimitiveType.Capsule:
                    if (capsule == null) capsule = Lathe("LowPolyCapsule", CapsuleProfile());
                    return capsule;
                case PrimitiveType.Cylinder:
                    if (cylinder == null) cylinder = Cylinder();
                    return cylinder;
                default:
                    return null;
            }
        }

        /// <summary>Profile (radius, y, normal in the xz-radial/y plane) from the bottom pole to the top pole.</summary>
        static List<(float r, float y, Vector2 n)> SphereProfile()
        {
            var points = new List<(float, float, Vector2)>();
            for (int i = 0; i <= SphereRings; i++)
            {
                float a = Mathf.PI * i / SphereRings - Mathf.PI / 2f;
                var n = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                points.Add((0.5f * n.x, 0.5f * n.y, n));
            }
            return points;
        }

        static List<(float r, float y, Vector2 n)> CapsuleProfile()
        {
            var points = new List<(float, float, Vector2)>();
            for (int i = 0; i <= CapRings; i++)
            {
                float a = Mathf.PI / 2f * i / CapRings - Mathf.PI / 2f;
                var n = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                points.Add((0.5f * n.x, -0.5f + 0.5f * n.y, n));
            }
            for (int i = 0; i <= CapRings; i++)
            {
                float a = Mathf.PI / 2f * i / CapRings;
                var n = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                points.Add((0.5f * n.x, 0.5f + 0.5f * n.y, n));
            }
            return points;
        }

        /// <summary>Spins a profile around Y with smooth normals.</summary>
        static Mesh Lathe(string name, List<(float r, float y, Vector2 n)> profile)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            int columns = Segments + 1;
            for (int i = 0; i < profile.Count; i++)
                for (int s = 0; s <= Segments; s++)
                {
                    float a = 2f * Mathf.PI * s / Segments;
                    float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                    var (r, y, n) = profile[i];
                    vertices.Add(new Vector3(r * c, y, r * sn));
                    normals.Add(new Vector3(n.x * c, n.y, n.x * sn).normalized);
                    uv.Add(new Vector2((float)s / Segments, (float)i / (profile.Count - 1)));
                }
            for (int i = 0; i + 1 < profile.Count; i++)
                for (int s = 0; s < Segments; s++)
                {
                    int a = i * columns + s, b = a + 1, c = a + columns, d = c + 1;
                    // Skip the degenerate triangle at a pole (radius 0 rows).
                    if (profile[i].r > 1e-5f) Add(triangles, a, c, b);
                    if (profile[i + 1].r > 1e-5f) Add(triangles, b, c, d);
                }
            return Build(name, vertices, normals, uv, triangles);
        }

        static Mesh Cylinder()
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            // Side: own vertices for smooth radial normals.
            for (int s = 0; s <= Segments; s++)
            {
                float a = 2f * Mathf.PI * s / Segments;
                var n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices.Add(n * 0.5f + Vector3.down);
                vertices.Add(n * 0.5f + Vector3.up);
                normals.Add(n);
                normals.Add(n);
                uv.Add(new Vector2((float)s / Segments, 0f));
                uv.Add(new Vector2((float)s / Segments, 1f));
            }
            for (int s = 0; s < Segments; s++)
            {
                int a = s * 2, b = a + 1, c = a + 2, d = a + 3;
                Add(triangles, a, b, c);
                Add(triangles, c, b, d);
            }
            // Caps: a centre fan each, flat normals.
            foreach (float y in new[] { -1f, 1f })
            {
                int centre = vertices.Count;
                var normal = new Vector3(0f, y, 0f);
                vertices.Add(new Vector3(0f, y, 0f));
                normals.Add(normal);
                uv.Add(new Vector2(0.5f, 0.5f));
                for (int s = 0; s <= Segments; s++)
                {
                    float a = 2f * Mathf.PI * s / Segments;
                    vertices.Add(new Vector3(Mathf.Cos(a) * 0.5f, y, Mathf.Sin(a) * 0.5f));
                    normals.Add(normal);
                    uv.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
                }
                for (int s = 0; s < Segments; s++)
                {
                    if (y > 0f) Add(triangles, centre, centre + 2 + s, centre + 1 + s);
                    else Add(triangles, centre, centre + 1 + s, centre + 2 + s);
                }
            }
            return Build("LowPolyCylinder", vertices, normals, uv, triangles);
        }

        static void Add(List<int> triangles, int a, int b, int c)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        static Mesh Build(string name, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uv, List<int> triangles)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            mesh.UploadMeshData(false);
            return mesh;
        }
    }
}
