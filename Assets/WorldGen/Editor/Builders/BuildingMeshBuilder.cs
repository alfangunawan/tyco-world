using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Builds building meshes: per-floor facade walls, flat roof + parapet, gable roof, or roof-only shelters.</summary>
    public static class BuildingMeshBuilder
    {
        public const int SubGround = 0, SubUpper = 1, SubTrim = 2, SubRoof = 3, SubmeshCount = 4;
        public const float RoofOnlyHeight = 3f, RoofOnlyThickness = 0.15f, PillarSize = 0.2f;

        public static RoofKind ChooseRoof(BuildingSpec spec, BuildingMeshParams p, Obb obb)
        {
            if (spec.RoofOnly) return RoofKind.RoofOnly;
            bool hasHoles = spec.Footprint.Holes.Count > 0;
            if (spec.RoofOverride == RoofOverride.Flat) return RoofKind.Flat;
            if (spec.RoofOverride == RoofOverride.Gable) return hasHoles ? RoofKind.Flat : RoofKind.Gable;
            if (!spec.AllowGable || hasHoles || spec.Levels > 3) return RoofKind.Flat;
            float area = spec.Footprint.Area;
            if (area > p.GableMaxArea || obb.Area <= 1e-4f) return RoofKind.Flat;
            return area / obb.Area >= p.GableMinRectangularity ? RoofKind.Gable : RoofKind.Flat;
        }

        public static MeshData Build(BuildingSpec spec, BuildingMeshParams p, out BuildingBuildInfo info)
        {
            var footprint = Normalized(spec.Footprint);
            info = new BuildingBuildInfo { Obb = ObbCalculator.Compute(footprint.Outer) };
            info.Roof = ChooseRoof(spec, p, info.Obb);
            var mesh = new MeshData(SubmeshCount);
            switch (info.Roof)
            {
                case RoofKind.RoofOnly:
                    info.RoofFailed = !AddRoofOnly(mesh, footprint, info.Obb, spec, p);
                    break;
                case RoofKind.Gable:
                    AddWalls(mesh, footprint, spec, p);
                    AddGableRoof(mesh, info.Obb, spec, p);
                    break;
                default:
                    AddWalls(mesh, footprint, spec, p);
                    info.RoofFailed = !AddFlatRoof(mesh, footprint, spec, p);
                    break;
            }
            return mesh;
        }

        static Polygon Normalized(Polygon source)
        {
            var outer = new List<Vector2>(source.Outer);
            Polygon2D.EnsureWinding(outer, true);
            var holes = new List<List<Vector2>>();
            foreach (var h in source.Holes)
            {
                var hole = new List<Vector2>(h);
                Polygon2D.EnsureWinding(hole, false);
                holes.Add(hole);
            }
            return new Polygon(outer, holes);
        }

        static IEnumerable<List<Vector2>> Rings(Polygon polygon)
        {
            yield return polygon.Outer;
            foreach (var hole in polygon.Holes) yield return hole;
        }

        /// <summary>Outward normal for an edge of a CCW outer ring or a CW hole ring.</summary>
        static Vector3 Outward(Vector2 p0, Vector2 p1)
        {
            var d = (p1 - p0).normalized;
            return new Vector3(d.y, 0f, -d.x);
        }

        static void AddBand(MeshData m, int sub, Vector2 p0, Vector2 p1, float y0, float y1, float u1, float v1, Vector3 normal) =>
            m.AddQuad(sub, Xz.At(p0, y0), Xz.At(p1, y0), Xz.At(p1, y1), Xz.At(p0, y1),
                new Vector2(0f, 0f), new Vector2(u1, 0f), new Vector2(u1, v1), new Vector2(0f, v1), normal);

        static void AddWalls(MeshData m, Polygon footprint, BuildingSpec spec, BuildingMeshParams p)
        {
            float lh = spec.LevelHeight;
            float groundTop = Mathf.Min(lh, spec.WallHeight);
            foreach (var ring in Rings(footprint))
                for (int i = 0; i < ring.Count; i++)
                {
                    Vector2 p0 = ring[i], p1 = ring[(i + 1) % ring.Count];
                    float length = Vector2.Distance(p0, p1);
                    if (length < 1e-3f) continue;
                    float bays = Mathf.Max(1f, Mathf.Round(length / p.FacadeTileWidth));
                    var normal = Outward(p0, p1);
                    AddBand(m, SubGround, p0, p1, 0f, groundTop, bays, groundTop / lh, normal);
                    if (spec.WallHeight > lh + 1e-3f)
                        AddBand(m, SubUpper, p0, p1, lh, spec.WallHeight, bays, (spec.WallHeight - lh) / lh, normal);
                }
        }

        static bool AddFlatRoof(MeshData m, Polygon footprint, BuildingSpec spec, BuildingMeshParams p)
        {
            bool ok = AreaMeshBuilder.AddFlatPolygon(m, SubRoof, footprint, spec.WallHeight);
            float top = spec.WallHeight + p.ParapetHeight;
            float v1 = p.ParapetHeight / spec.LevelHeight;
            foreach (var ring in Rings(footprint))
                for (int i = 0; i < ring.Count; i++)
                {
                    Vector2 p0 = ring[i], p1 = ring[(i + 1) % ring.Count];
                    float length = Vector2.Distance(p0, p1);
                    if (length < 1e-3f) continue;
                    var normal = Outward(p0, p1);
                    float u1 = length / p.FacadeTileWidth;
                    AddBand(m, SubTrim, p0, p1, spec.WallHeight, top, u1, v1, normal);
                    AddBand(m, SubTrim, p0, p1, spec.WallHeight, top, u1, v1, -normal);
                }
            return ok;
        }

        static void AddGableRoof(MeshData m, Obb o, BuildingSpec spec, BuildingMeshParams p)
        {
            float h0 = spec.WallHeight;
            float rise = p.GablePitchRatio * 2f * o.HalfV;
            float h1 = h0 + rise;
            float slope = rise / Mathf.Max(o.HalfV, 1e-3f);
            float a = o.HalfU + p.GableOverhang, b = o.HalfV + p.GableOverhang;
            float eave = h0 - slope * p.GableOverhang;
            Vector2 c = o.Center, u = o.AxisU, v = o.AxisV;

            Vector3 r0 = Xz.At(c - u * a, h1), r1 = Xz.At(c + u * a, h1);
            Vector3 e0Minus = Xz.At(c - u * a - v * b, eave), e1Minus = Xz.At(c + u * a - v * b, eave);
            Vector3 e0Plus = Xz.At(c - u * a + v * b, eave), e1Plus = Xz.At(c + u * a + v * b, eave);
            float slopeLength = Mathf.Sqrt(b * b + (h1 - eave) * (h1 - eave));
            Vector3 v3 = Xz.Dir(v);
            Vector3 nMinus = (Vector3.up * b - v3 * (h1 - eave)).normalized;
            Vector3 nPlus = (Vector3.up * b + v3 * (h1 - eave)).normalized;
            Vector2 uv0 = new Vector2(-a, 0f), uv1 = new Vector2(a, 0f), uv2 = new Vector2(a, slopeLength), uv3 = new Vector2(-a, slopeLength);

            m.AddQuad(SubRoof, e0Minus, e1Minus, r1, r0, uv0, uv1, uv2, uv3, nMinus);
            m.AddQuad(SubRoof, e0Minus, e1Minus, r1, r0, uv0, uv1, uv2, uv3, -nMinus);
            m.AddQuad(SubRoof, e0Plus, e1Plus, r1, r0, uv0, uv1, uv2, uv3, nPlus);
            m.AddQuad(SubRoof, e0Plus, e1Plus, r1, r0, uv0, uv1, uv2, uv3, -nPlus);

            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 end = c + u * (o.HalfU * s);
                Vector3 g0 = Xz.At(end - v * o.HalfV, h0), g1 = Xz.At(end + v * o.HalfV, h0), g2 = Xz.At(end, h1);
                m.AddTriangle(SubTrim, g0, g1, g2,
                    Vector2.zero, new Vector2(2f * o.HalfV / p.FacadeTileWidth, 0f), new Vector2(o.HalfV / p.FacadeTileWidth, rise / spec.LevelHeight),
                    Xz.Dir(u * s));
            }
        }

        static bool AddRoofOnly(MeshData m, Polygon footprint, Obb o, BuildingSpec spec, BuildingMeshParams p)
        {
            float bottom = RoofOnlyHeight, top = RoofOnlyHeight + RoofOnlyThickness;
            bool ok = AreaMeshBuilder.AddFlatPolygon(m, SubRoof, footprint, top);
            ok &= AreaMeshBuilder.AddFlatPolygon(m, SubRoof, footprint, bottom, faceDown: true);
            float v1 = RoofOnlyThickness / spec.LevelHeight;
            foreach (var ring in Rings(footprint))
                for (int i = 0; i < ring.Count; i++)
                {
                    Vector2 p0 = ring[i], p1 = ring[(i + 1) % ring.Count];
                    float length = Vector2.Distance(p0, p1);
                    if (length < 1e-3f) continue;
                    AddBand(m, SubTrim, p0, p1, bottom, top, length / p.FacadeTileWidth, v1, Outward(p0, p1));
                }
            for (int su = -1; su <= 1; su += 2)
                for (int sv = -1; sv <= 1; sv += 2)
                {
                    Vector2 corner = o.Center + o.AxisU * ((o.HalfU - PillarSize) * su) + o.AxisV * ((o.HalfV - PillarSize) * sv);
                    AddPillar(m, corner, o.AxisU, o.AxisV, bottom, spec.LevelHeight);
                }
            return ok;
        }

        static void AddPillar(MeshData m, Vector2 center, Vector2 u, Vector2 v, float height, float levelHeight)
        {
            float h = PillarSize * 0.5f;
            var ring = new[] { center + (-u - v) * h, center + (u - v) * h, center + (u + v) * h, center + (-u + v) * h };
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = ring[i], b = ring[(i + 1) % 4];
                AddBand(m, SubTrim, a, b, 0f, height, 1f, height / levelHeight, Outward(a, b));
            }
        }
    }
}
