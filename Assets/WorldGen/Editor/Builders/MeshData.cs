using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Flat-shaded mesh builder. Winding follows a desired normal, so callers never order indices by hand.</summary>
    public sealed class MeshData
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<Vector2> Uvs = new List<Vector2>();
        readonly List<int>[] submeshes;

        public MeshData(int submeshCount = 1)
        {
            submeshes = new List<int>[submeshCount];
            for (int i = 0; i < submeshCount; i++) submeshes[i] = new List<int>();
        }

        public int SubmeshCount => submeshes.Length;
        public bool IsEmpty => Vertices.Count == 0;
        public IReadOnlyList<int> Indices(int submesh) => submeshes[submesh];

        public int TriangleCount
        {
            get
            {
                int count = 0;
                foreach (var s in submeshes) count += s.Count / 3;
                return count;
            }
        }

        public void AddTriangle(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector3 desiredNormal)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) return;
            if (Vector3.Dot(n, desiredNormal) < 0f)
            {
                (b, c) = (c, b);
                (uvB, uvC) = (uvC, uvB);
                n = -n;
            }
            n.Normalize();
            int start = Vertices.Count;
            Vertices.Add(a); Vertices.Add(b); Vertices.Add(c);
            Normals.Add(n); Normals.Add(n); Normals.Add(n);
            Uvs.Add(uvA); Uvs.Add(uvB); Uvs.Add(uvC);
            var list = submeshes[submesh];
            list.Add(start); list.Add(start + 1); list.Add(start + 2);
        }

        public void AddQuad(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD, Vector3 desiredNormal)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) n = Vector3.Cross(c - a, d - a);
            if (n.sqrMagnitude < 1e-12f) return;
            if (Vector3.Dot(n, desiredNormal) < 0f)
            {
                (b, d) = (d, b);
                (uvB, uvD) = (uvD, uvB);
                n = -n;
            }
            n.Normalize();
            int start = Vertices.Count;
            Vertices.Add(a); Vertices.Add(b); Vertices.Add(c); Vertices.Add(d);
            for (int i = 0; i < 4; i++) Normals.Add(n);
            Uvs.Add(uvA); Uvs.Add(uvB); Uvs.Add(uvC); Uvs.Add(uvD);
            var list = submeshes[submesh];
            list.Add(start); list.Add(start + 1); list.Add(start + 2);
            list.Add(start); list.Add(start + 2); list.Add(start + 3);
        }

        /// <summary>Writes into an existing mesh. Empty submeshes are dropped; usedSubmeshes maps mesh submesh k to MeshData submesh.</summary>
        public void WriteTo(Mesh mesh, out int[] usedSubmeshes)
        {
            var used = new List<int>();
            for (int s = 0; s < submeshes.Length; s++)
                if (submeshes[s].Count > 0) used.Add(s);
            usedSubmeshes = used.ToArray();
            mesh.Clear();
            mesh.indexFormat = Vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(Vertices);
            mesh.SetNormals(Normals);
            mesh.SetUVs(0, Uvs);
            mesh.subMeshCount = used.Count;
            for (int k = 0; k < used.Count; k++) mesh.SetTriangles(submeshes[used[k]], k, false);
            mesh.RecalculateBounds();
        }

        public Mesh ToMesh(string name, out int[] usedSubmeshes)
        {
            var mesh = new Mesh { name = name };
            WriteTo(mesh, out usedSubmeshes);
            return mesh;
        }

        public ulong ContentHash()
        {
            ulong hash = 14695981039346656037UL;
            void Mix(int value)
            {
                unchecked
                {
                    hash ^= (uint)value;
                    hash *= 1099511628211UL;
                }
            }
            foreach (var v in Vertices)
            {
                Mix(Mathf.RoundToInt(v.x * 1000f));
                Mix(Mathf.RoundToInt(v.y * 1000f));
                Mix(Mathf.RoundToInt(v.z * 1000f));
            }
            foreach (var uv in Uvs)
            {
                Mix(Mathf.RoundToInt(uv.x * 1000f));
                Mix(Mathf.RoundToInt(uv.y * 1000f));
            }
            for (int s = 0; s < submeshes.Length; s++)
            {
                Mix(-1 - s);
                foreach (int i in submeshes[s]) Mix(i);
            }
            return hash;
        }

        public bool HasInvalidValues()
        {
            foreach (var v in Vertices) if (!Finite(v.x) || !Finite(v.y) || !Finite(v.z)) return true;
            foreach (var n in Normals) if (!Finite(n.x) || !Finite(n.y) || !Finite(n.z)) return true;
            foreach (var uv in Uvs) if (!Finite(uv.x) || !Finite(uv.y)) return true;
            return false;
        }

        static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
    }
}
