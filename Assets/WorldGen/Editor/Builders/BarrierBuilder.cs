using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>What <see cref="BarrierBuilder"/> makes: per-chunk render and collider meshes, the prop placements and the blockers.</summary>
    public sealed class BarrierBuildResult
    {
        public readonly Dictionary<Vector2Int, MeshData> Render = new Dictionary<Vector2Int, MeshData>();
        public readonly Dictionary<Vector2Int, MeshData> Collider = new Dictionary<Vector2Int, MeshData>();
        public readonly List<PropPlacement> Props = new List<PropPlacement>();
        public readonly List<WallSegment> Blockers = new List<WallSegment>();
    }

    /// <summary>
    /// The barriers at dead ends (spec road-clearance §3–§5): a low plastered wall with two pots in front, a yellow and
    /// black portal on two posts, or a line of striped concrete blocks with the road-closed barricade at the map edge.
    /// Walls and portals are built here, merged per chunk; the downloaded models are placed as props. Every barrier also
    /// gets a solid box collider across the road, so it stops people and vehicles even before the prefabs are set.
    /// </summary>
    public static class BarrierBuilder
    {
        public const string Name = "Barriers";
        public const int SubWall = 0, SubConcrete = 1, SubStripe = 2, SubmeshCount = 3;
        public const int PotVariant = 0, ConcreteVariant = 1, RoadClosedVariant = 2;
        /// <summary>The road-closed line is about this tall.</summary>
        public const float BoundaryHeight = 1.0f;
        const float Coping = 0.06f;
        /// <summary>Open width a line of blocks may leave in all; less than a person needs.</summary>
        public const float MaxBlockGap = 0.6f;

        public static BarrierBuildResult Build(IReadOnlyList<DeadEnd> ends, DeadEndSettings s, FrontageSettings f, float chunkSize)
        {
            var r = new BarrierBuildResult();
            foreach (var end in ends)
            {
                var chunk = WorldMeshPipeline.ChunkOf(end.Position, chunkSize);
                var across = end.Across;
                var a = end.Position - across * (end.Width * 0.5f);
                var b = end.Position + across * (end.Width * 0.5f);
                float yaw = Mathf.Atan2(end.Outward.x, end.Outward.y) * Mathf.Rad2Deg;
                float top;
                switch (end.Kind)
                {
                    case DeadEndKind.Wall:
                    {
                        top = s.wallHeight;
                        var m = MeshIn(r.Render, chunk, SubmeshCount);
                        SolidBox.Add(m, SubWall, a, b, 0f, top - Coping, s.wallThickness,
                            BoxUv.AtlasBand(s.wallBand, f.atlasBands, f.atlasBandMeters, f.atlasTileMeters), BoxEnds.Both, false);
                        SolidBox.Add(m, SubConcrete, a, b, top - Coping, top, s.wallThickness + 0.04f, BoxUv.Meters, BoxEnds.Both, true);
                        // Two pots on the road side, in from each end.
                        foreach (var at in new[] { a + across * 0.45f, b - across * 0.45f })
                            r.Props.Add(new PropPlacement
                            {
                                Position = at - end.Outward * (s.wallThickness * 0.5f + 0.35f), YawDegrees = yaw, Scale = 1f, Variant = PotVariant
                            });
                        break;
                    }
                    case DeadEndKind.Portal:
                    {
                        top = s.portalHeight;
                        var m = MeshIn(r.Render, chunk, SubmeshCount);
                        SolidBox.AddPost(m, SubStripe, a, across, s.portalPost, 0f, top, BoxUv.Meters);
                        SolidBox.AddPost(m, SubStripe, b, across, s.portalPost, 0f, top, BoxUv.Meters);
                        SolidBox.Add(m, SubStripe, a, b, top - 0.12f, top, 0.08f, BoxUv.Meters, BoxEnds.Both, true);
                        break;
                    }
                    default:
                    {
                        top = BoundaryHeight;
                        // Enough blocks that the gaps left are narrower than a person (they may overlap a little instead).
                        int blocks = Mathf.Max(1, Mathf.CeilToInt((end.Width - MaxBlockGap) / s.blockSpacing));
                        for (int i = 0; i < blocks; i++)
                            r.Props.Add(new PropPlacement
                            {
                                // The block runs along its local X (across the road) with its striped face on +Z: turned to face
                                // back down the road.
                                Position = Vector2.Lerp(a, b, (i + 0.5f) / blocks), YawDegrees = yaw + 180f, Scale = 1f, Variant = ConcreteVariant
                            });
                        r.Props.Add(new PropPlacement
                        {
                            Position = end.Position - end.Outward * 1.0f, YawDegrees = yaw + 180f, Scale = 1f, Variant = RoadClosedVariant
                        });
                        break;
                    }
                }
                SolidBox.Add(MeshIn(r.Collider, chunk, 1), 0, a, b, 0f, top, Mathf.Max(s.wallThickness, 0.3f), BoxUv.Meters, BoxEnds.Both, true);
                r.Blockers.Add(new WallSegment(a, b));
            }
            return r;
        }

        static MeshData MeshIn(Dictionary<Vector2Int, MeshData> meshes, Vector2Int chunk, int submeshes)
        {
            if (!meshes.TryGetValue(chunk, out var m)) meshes[chunk] = m = new MeshData(submeshes);
            return m;
        }
    }
}
