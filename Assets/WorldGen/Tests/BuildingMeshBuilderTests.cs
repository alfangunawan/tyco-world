using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class BuildingMeshBuilderTests
    {
        static readonly BuildingMeshParams P = BuildingMeshParams.Default;

        static Polygon Rect(float w, float d) => new Polygon(new List<Vector2>
            { new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, d), new Vector2(0, d) });

        static Polygon LShape() => new Polygon(new List<Vector2>
            { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 4), new Vector2(4, 4), new Vector2(4, 10), new Vector2(0, 10) });

        static BuildingSpec Spec(Polygon footprint, float wall, int levels, bool allowGable = false) => new BuildingSpec
        {
            Id = 1, Footprint = footprint, WallHeight = wall, Levels = levels, LevelHeight = 3.2f, AllowGable = allowGable
        };

        static float MaxV(MeshData m, int sub)
        {
            float max = float.MinValue;
            foreach (int i in m.Indices(sub)) max = Mathf.Max(max, m.Uvs[i].y);
            return max;
        }

        static float MaxY(MeshData m)
        {
            float max = float.MinValue;
            foreach (var v in m.Vertices) max = Mathf.Max(max, v.y);
            return max;
        }

        [Test]
        public void FlatBox_HeightsAndUvRanges()
        {
            var m = BuildingMeshBuilder.Build(Spec(Rect(10, 10), 9.6f, 3), P, out var info);
            Assert.AreEqual(RoofKind.Flat, info.Roof);
            Assert.IsFalse(info.RoofFailed);
            Assert.AreEqual(10.2f, MaxY(m), 1e-4f);
            Assert.AreEqual(1f, MaxV(m, BuildingMeshBuilder.SubGround), 1e-4f);
            Assert.AreEqual(2f, MaxV(m, BuildingMeshBuilder.SubUpper), 1e-4f);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Walls_FaceOutward(bool clockwiseInput)
        {
            var footprint = Rect(10, 10);
            if (clockwiseInput) footprint.Outer.Reverse();
            var m = BuildingMeshBuilder.Build(Spec(footprint, 9.6f, 3), P, out _);
            var center = new Vector3(5, 0, 5);
            var idx = m.Indices(BuildingMeshBuilder.SubGround);
            Assert.Greater(idx.Count, 0);
            for (int t = 0; t < idx.Count; t += 3)
            {
                Vector3 a = m.Vertices[idx[t]], b = m.Vertices[idx[t + 1]], c = m.Vertices[idx[t + 2]];
                var normal = Vector3.Cross(b - a, c - a);
                var toFace = (a + b + c) / 3f - center;
                toFace.y = 0f;
                Assert.Greater(Vector3.Dot(normal, toFace), 0f);
            }
        }

        [Test]
        public void FlatRoof_FacesUp()
        {
            var m = BuildingMeshBuilder.Build(Spec(Rect(10, 10), 6.4f, 2), P, out _);
            foreach (int i in m.Indices(BuildingMeshBuilder.SubRoof)) Assert.Greater(m.Normals[i].y, 0.99f);
        }

        [Test]
        public void FacadeBays_AreWholeNumbers()
        {
            var m = BuildingMeshBuilder.Build(Spec(Rect(10, 10), 3.2f, 1), P, out _);
            float maxU = float.MinValue;
            foreach (int i in m.Indices(BuildingMeshBuilder.SubGround)) maxU = Mathf.Max(maxU, m.Uvs[i].x);
            Assert.AreEqual(3f, maxU, 1e-4f);
        }

        [Test]
        public void SmallRectangle_GetsGableAlongLongSide()
        {
            var m = BuildingMeshBuilder.Build(Spec(Rect(8, 12), 3.2f, 1, allowGable: true), P, out var info);
            Assert.AreEqual(RoofKind.Gable, info.Roof);
            float top = MaxY(m);
            Assert.AreEqual(5.2f, top, 1e-3f);
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var v in m.Vertices)
            {
                if (Mathf.Abs(v.y - top) > 1e-3f) continue;
                Assert.AreEqual(4f, v.x, 1e-3f);
                minZ = Mathf.Min(minZ, v.z);
                maxZ = Mathf.Max(maxZ, v.z);
            }
            Assert.GreaterOrEqual(maxZ - minZ, 12f);
        }

        [Test]
        public void GableOverride_WithHole_FallsBackToFlat()
        {
            var footprint = Rect(10, 10);
            footprint.Holes.Add(new List<Vector2> { new Vector2(3, 3), new Vector2(3, 7), new Vector2(7, 7), new Vector2(7, 3) });
            var spec = Spec(footprint, 3.2f, 1, allowGable: true);
            spec.RoofOverride = RoofOverride.Gable;
            BuildingMeshBuilder.Build(spec, P, out var info);
            Assert.AreEqual(RoofKind.Flat, info.Roof);
        }

        [Test]
        public void LShape_GetsFlatRoofWithTwoSidedParapet()
        {
            var m = BuildingMeshBuilder.Build(Spec(LShape(), 3.2f, 1, allowGable: true), P, out var info);
            Assert.AreEqual(RoofKind.Flat, info.Roof);
            Assert.AreEqual(24, m.Indices(BuildingMeshBuilder.SubTrim).Count / 3);
        }

        [TestCase(4, 10f, 10f, true)]
        [TestCase(1, 25f, 10f, true)]
        [TestCase(1, 8f, 12f, false)]
        public void ChooseRoof_FallsBackToFlat(int levels, float w, float d, bool allowGable)
        {
            var spec = Spec(Rect(w, d), levels * 3.2f, levels, allowGable);
            var obb = ObbCalculator.Compute(spec.Footprint.Outer);
            Assert.AreEqual(RoofKind.Flat, BuildingMeshBuilder.ChooseRoof(spec, P, obb));
        }

        [Test]
        public void RoofOnly_HasNoWalls()
        {
            var spec = Spec(Rect(6, 4), 3f, 1);
            spec.RoofOnly = true;
            var m = BuildingMeshBuilder.Build(spec, P, out var info);
            Assert.AreEqual(RoofKind.RoofOnly, info.Roof);
            Assert.AreEqual(0, m.Indices(BuildingMeshBuilder.SubGround).Count);
            Assert.AreEqual(0, m.Indices(BuildingMeshBuilder.SubUpper).Count);
            Assert.AreEqual(BuildingMeshBuilder.RoofOnlyHeight + BuildingMeshBuilder.RoofOnlyThickness, MaxY(m), 1e-4f);
        }

        [Test]
        public void LowWall_ClampsGroundUv()
        {
            var m = BuildingMeshBuilder.Build(Spec(Rect(10, 10), 2.5f, 1), P, out _);
            Assert.AreEqual(2.5f / 3.2f, MaxV(m, BuildingMeshBuilder.SubGround), 1e-4f);
            Assert.AreEqual(0, m.Indices(BuildingMeshBuilder.SubUpper).Count);
        }

        [Test]
        public void Build_IsDeterministic()
        {
            var a = BuildingMeshBuilder.Build(Spec(LShape(), 6.4f, 2), P, out _);
            var b = BuildingMeshBuilder.Build(Spec(LShape(), 6.4f, 2), P, out _);
            Assert.AreEqual(a.ContentHash(), b.ContentHash());
        }
    }
}
