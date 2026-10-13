using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    public static class FeatureGroups
    {
        public const string Roads = "Roads";
        public const string Buildings = "Buildings";
        public const string Areas = "Areas";
        public const string Water = "Water";
        public const string KeyBuildings = "KeyBuildings";
    }

    public sealed class BuiltFeature
    {
        public FeatureKind Kind;
        public long OsmId;
        public OsmElementType ElementType;
        public string Name;
        public string Group;
        public Vector2Int Chunk;
        public Vector2 Anchor;
        public MeshData Mesh;
        public Material[] Materials = new Material[0];
        public bool AddCollider;
        public SurfaceType Surface;
        public KeyBuilding Key;
        public Obb Obb;
        public string Category = "";
        public IReadOnlyDictionary<string, string> Tags = OsmTags.Empty;
    }

    public sealed class BuiltWorld
    {
        public Rect Bounds;
        public float ChunkSize;
        public MeshData Ground;
        public readonly List<BuiltFeature> Features = new List<BuiltFeature>();
        public List<PropPlacement> Trees = new List<PropPlacement>();
        public List<PropPlacement> Lamps = new List<PropPlacement>();
    }

    /// <summary>Pure pipeline: classified features → meshes, materials, props, chunk hashes. Touches no scene or asset.</summary>
    public static class WorldMeshPipeline
    {
        public static Vector2Int ChunkOf(Vector2 p, float chunkSize) =>
            new Vector2Int(Mathf.FloorToInt(p.x / chunkSize), Mathf.FloorToInt(p.y / chunkSize));

        public static string ChunkName(Vector2Int chunk) =>
            "Chunk_" + chunk.x.ToString(CultureInfo.InvariantCulture) + "_" + chunk.y.ToString(CultureInfo.InvariantCulture);

        public static ClassifiedWorld Classify(string json, WorldGenSettings settings, GenerationReport report) =>
            new FeatureClassifier(settings, report).Classify(OsmParser.Parse(json));

        public static BuiltWorld Build(ClassifiedWorld world, WorldGenSettings settings, GenerationReport report)
        {
            var built = new BuiltWorld
            {
                Bounds = world.LocalBounds,
                ChunkSize = settings.chunkSize > 0f ? settings.chunkSize : 100f,
                Ground = GroundBuilder.Build(world.LocalBounds)
            };
            AddRoads(world, built, report);
            AddBuildings(world, settings, built, report);
            AddAreas(world, built, report);
            AddWater(world, settings, built, report);
            foreach (var feature in built.Features) feature.Surface = SurfaceRules.For(feature);

            var placer = new PropPlacer(world.Buildings, world.Roads);
            int treeVariants = settings.treePrefabs != null ? settings.treePrefabs.Length : 0;
            var waterShapes = world.WaterAreas.Select(w => w.Shape).ToList();
            built.Trees = placer.PlaceTrees(world.Trees, world.Areas, settings.maxTrees, treeVariants, report, waterShapes);
            built.Lamps = placer.PlaceLamps(world.Roads, settings.lampMinSpacing, settings.lampOffset, report);
            ComputeChunkHashes(built, report);
            return built;
        }

        static void AddRoads(ClassifiedWorld world, BuiltWorld built, GenerationReport report)
        {
            foreach (var road in world.Roads)
            {
                var mesh = new MeshData(1);
                foreach (var piece in road.Pieces)
                    RoadMeshBuilder.AddRibbon(mesh, 0, piece.Points, road.Width, road.Y, piece.ExtendStart, piece.ExtendEnd);
                if (mesh.IsEmpty) { report.Skip(SkipReasons.RoadEmptyMesh, road.Id); continue; }
                var anchor = MidPoint(road.Pieces);
                built.Features.Add(new BuiltFeature
                {
                    Kind = FeatureKind.Road, OsmId = road.Id, ElementType = OsmElementType.Way, Name = road.Name,
                    Group = FeatureGroups.Roads, Anchor = anchor, Chunk = ChunkOf(anchor, built.ChunkSize),
                    Mesh = mesh, Materials = new[] { road.Rule.material }, Category = road.Highway, Tags = road.Tags,
                    AddCollider = true
                });
                report.Count("Road");
            }
        }

        static void AddBuildings(ClassifiedWorld world, WorldGenSettings settings, BuiltWorld built, GenerationReport report)
        {
            var meshParams = settings.MeshParams;
            foreach (var b in world.Buildings)
            {
                var feature = new BuiltFeature
                {
                    Kind = FeatureKind.Building, OsmId = b.Id, ElementType = b.ElementType, Name = b.Name,
                    Group = b.Key != null ? FeatureGroups.KeyBuildings : FeatureGroups.Buildings,
                    Anchor = b.Centroid, Chunk = ChunkOf(b.Centroid, built.ChunkSize), Key = b.Key,
                    AddCollider = true, Category = b.Rule.name, Tags = b.Tags
                };
                if (b.Key != null && b.Key.prefab != null)
                {
                    feature.Obb = ObbCalculator.Compute(b.Spec.Footprint.Outer);
                    feature.AddCollider = false;
                    built.Features.Add(feature);
                    report.Count("KeyBuildingPrefab");
                    continue;
                }

                feature.Mesh = BuildingMeshBuilder.Build(b.Spec, meshParams, out var info);
                feature.Obb = info.Obb;
                if (info.RoofFailed) report.Skip(SkipReasons.RoofTriangulationFailed, b.Id);

                var set = settings.MaterialSet(b.Rule.materialSet);
                int upper = SplitMix64.Pick(b.Id, "upper", Length(set.upper));
                var roofs = info.Roof == RoofKind.Gable ? set.gableRoof : set.flatRoof;
                feature.Materials = new Material[BuildingMeshBuilder.SubmeshCount];
                feature.Materials[BuildingMeshBuilder.SubGround] = At(set.groundFloor, SplitMix64.Pick(b.Id, "ground", Length(set.groundFloor)));
                feature.Materials[BuildingMeshBuilder.SubUpper] = At(set.upper, upper);
                feature.Materials[BuildingMeshBuilder.SubTrim] = At(set.trim, upper);
                feature.Materials[BuildingMeshBuilder.SubRoof] = At(roofs, SplitMix64.Pick(b.Id, "roof", Length(roofs)));
                built.Features.Add(feature);
                report.Count(b.Key != null ? "KeyBuilding" : "Building");
            }
        }

        static void AddAreas(ClassifiedWorld world, BuiltWorld built, GenerationReport report)
        {
            foreach (var area in world.Areas)
            {
                var mesh = new MeshData(1);
                if (!AreaMeshBuilder.AddFlatPolygon(mesh, 0, area.Shape, area.Rule.yOffset) || mesh.IsEmpty)
                {
                    report.Skip(SkipReasons.AreaTriangulationFailed, area.Id);
                    continue;
                }
                var anchor = Polygon2D.Centroid(area.Shape.Outer);
                built.Features.Add(new BuiltFeature
                {
                    Kind = FeatureKind.Area, OsmId = area.Id, ElementType = area.ElementType, Name = area.Name,
                    Group = FeatureGroups.Areas, Anchor = anchor, Chunk = ChunkOf(anchor, built.ChunkSize),
                    Mesh = mesh, Materials = new[] { area.Rule.material }, Category = area.Rule.category.ToString(), Tags = area.Tags,
                    AddCollider = true
                });
                report.Count("Area");
            }
        }

        static void AddWater(ClassifiedWorld world, WorldGenSettings settings, BuiltWorld built, GenerationReport report)
        {
            foreach (var water in world.WaterAreas)
            {
                var mesh = new MeshData(1);
                if (!AreaMeshBuilder.AddFlatPolygon(mesh, 0, water.Shape, settings.waterAreaY) || mesh.IsEmpty)
                {
                    report.Skip(SkipReasons.AreaTriangulationFailed, water.Id);
                    continue;
                }
                var anchor = Polygon2D.Centroid(water.Shape.Outer);
                built.Features.Add(new BuiltFeature
                {
                    Kind = FeatureKind.WaterArea, OsmId = water.Id, ElementType = water.ElementType, Name = water.Name,
                    Group = FeatureGroups.Water, Anchor = anchor, Chunk = ChunkOf(anchor, built.ChunkSize),
                    Mesh = mesh, Materials = new[] { settings.waterMaterial }, Category = "Water", Tags = water.Tags,
                    AddCollider = true
                });
                report.Count("WaterArea");
            }
            foreach (var line in world.WaterLines)
            {
                var mesh = new MeshData(1);
                foreach (var piece in line.Pieces) RoadMeshBuilder.AddRibbon(mesh, 0, piece.Points, line.Width, line.Y, false, false);
                if (mesh.IsEmpty) { report.Skip(SkipReasons.WaterEmptyMesh, line.Id); continue; }
                var anchor = MidPoint(line.Pieces);
                built.Features.Add(new BuiltFeature
                {
                    Kind = FeatureKind.WaterLine, OsmId = line.Id, ElementType = OsmElementType.Way, Name = line.Name,
                    Group = FeatureGroups.Water, Anchor = anchor, Chunk = ChunkOf(anchor, built.ChunkSize),
                    Mesh = mesh, Materials = new[] { settings.waterMaterial }, Category = "Water", Tags = line.Tags,
                    AddCollider = true
                });
                report.Count("WaterLine");
            }
        }

        static int Length(Material[] list) => list != null ? list.Length : 0;

        static Material At(Material[] list, int index) =>
            list == null || list.Length == 0 || index < 0 ? null : list[index % list.Length];

        static Vector2 MidPoint(List<LinePiece> pieces)
        {
            var pts = pieces[0].Points;
            float total = 0f;
            for (int i = 0; i + 1 < pts.Count; i++) total += Vector2.Distance(pts[i], pts[i + 1]);
            float half = total * 0.5f, travelled = 0f;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                float length = Vector2.Distance(pts[i], pts[i + 1]);
                if (length > 0f && travelled + length >= half) return Vector2.Lerp(pts[i], pts[i + 1], (half - travelled) / length);
                travelled += length;
            }
            return pts[0];
        }

        static void ComputeChunkHashes(BuiltWorld built, GenerationReport report)
        {
            var hashes = new SortedDictionary<string, ulong>(StringComparer.Ordinal);
            void Mix(Vector2Int chunk, ulong value)
            {
                string key = ChunkName(chunk);
                if (!hashes.TryGetValue(key, out ulong hash)) hash = 14695981039346656037UL;
                unchecked
                {
                    hash ^= value;
                    hash *= 1099511628211UL;
                }
                hashes[key] = hash;
            }

            foreach (var f in built.Features.OrderBy(f => f.Kind).ThenBy(f => f.OsmId))
                Mix(f.Chunk, f.Mesh != null ? f.Mesh.ContentHash() : unchecked((ulong)f.OsmId));
            foreach (var t in built.Trees) Mix(ChunkOf(t.Position, built.ChunkSize), Quantize(t));
            foreach (var l in built.Lamps) Mix(ChunkOf(l.Position, built.ChunkSize), Quantize(l));

            report.ChunkHashes.Clear();
            foreach (var kv in hashes) report.ChunkHashes[kv.Key] = kv.Value.ToString("x16", CultureInfo.InvariantCulture);
        }

        static ulong Quantize(PropPlacement p) => unchecked(
            ((ulong)(uint)Mathf.RoundToInt(p.Position.x * 100f) << 32) | (uint)Mathf.RoundToInt(p.Position.y * 100f));
    }
}
