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
        public const string RoadEdges = "RoadEdges";
        public const string Frontage = "Frontage";
        public const string KeyBuildings = "KeyBuildings";
        public const string Barriers = "Barriers";
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
        /// <summary>Separate collision mesh (solid boxes for walls); null = collide with Mesh.</summary>
        public MeshData ColliderMesh;
        /// <summary>False for generated chunk objects with no OSM element behind them (edges, walls): they get no OsmFeature.</summary>
        public bool HasOsmIdentity = true;
    }

    public sealed class BuiltWorld
    {
        public Rect Bounds;
        public float ChunkSize;
        public MeshData Ground;
        public readonly List<BuiltFeature> Features = new List<BuiltFeature>();
        public List<PropPlacement> Trees = new List<PropPlacement>();
        public List<PropPlacement> Lamps = new List<PropPlacement>();
        /// <summary>Pots, concrete blocks and road-closed signs at dead ends (spec road-clearance §3).</summary>
        public List<PropPlacement> Barriers = new List<PropPlacement>();
        public readonly List<InfillHouse> Infill = new List<InfillHouse>();
    }

    /// <summary>Pure pipeline: classified features → meshes, materials, props, chunk hashes. Touches no scene or asset.</summary>
    public static class WorldMeshPipeline
    {
        /// <summary>Longest triangle edge allowed in walkable (collider) meshes; PhysX warns above 500 m.</summary>
        public const float MaxColliderEdge = 100f;
        /// <summary>Prop containers under the generated root; WorldChunkIndex culls by these names.</summary>
        public const string TreesGroup = "Props/Trees";
        public const string LampsGroup = "Props/Lamps";
        public const string BarrierPropsGroup = "Props/Barriers";
        static readonly IReadOnlyDictionary<string, string> InfillTags = new Dictionary<string, string> { { "generated", "infill" } };

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
            var clock = System.Diagnostics.Stopwatch.StartNew();
            void Lap(string name)
            {
                report.Stage(name, clock.Elapsed.TotalMilliseconds);
                clock.Restart();
            }
            var network = new RoadNetwork(world.Roads, CampusShapes(world), settings.roadEdges);
            AddRoads(world, built, network, settings, report);
            AddBuildings(world, settings, built, report);
            AddAreas(world, built, report);
            AddWater(world, settings, built, report);
            Lap("meshes.base");
            var street = StreetContext.From(world, settings);
            Lap("meshes.street");
            AddEdges(built, network, street, settings, report);
            Lap("meshes.edges");
            var plan = FrontagePlanner.Plan(network, street, settings);
            Lap("meshes.frontage1");
            var houses = InfillPlanner.Plan(plan, network, street, settings, report);
            AddInfill(built, houses, settings, report);
            Lap("meshes.infill");
            var walls = AddFrontage(built, FrontagePlanner.Plan(network, street, settings), settings, report);
            Lap("meshes.frontage2");
            var deadEnds = DeadEndPlanner.Plan(network, world, street, settings.deadEnds, settings.frontage.kosExclusionRadius, report);
            var barriers = BarrierBuilder.Build(deadEnds, settings.deadEnds, settings.frontage, built.ChunkSize);
            AddChunkFeatures(built, barriers.Render, barriers.Collider, FeatureKind.Barrier, FeatureGroups.Barriers, BarrierBuilder.Name,
                new[] { settings.frontage.wallAtlas, settings.roadEdges.concrete, settings.deadEnds.stripe }, report);
            built.Barriers = barriers.Props;
            Lap("meshes.barriers");
            foreach (var feature in built.Features) feature.Surface = SurfaceRules.For(feature);

            var placer = new PropPlacer(world.Buildings, world.Roads, network, settings.roadEdges);
            placer.AddWalls(walls);
            placer.AddWalls(barriers.Blockers);
            placer.AddBuildings(houses.Select(h => h.AsBuildingFeature()));
            int treeVariants = settings.treePrefabs != null ? settings.treePrefabs.Length : 0;
            var waterShapes = world.WaterAreas.Select(w => w.Shape).ToList();
            built.Trees = placer.PlaceTrees(world.Trees, world.Areas, settings.maxTrees, treeVariants, report, waterShapes);
            built.Lamps = placer.PlaceLamps(world.Roads, settings.lampMinSpacing, settings.lampOffset, report);
            ComputeChunkHashes(built, report);
            return built;
        }

        static void AddRoads(ClassifiedWorld world, BuiltWorld built, RoadNetwork network, WorldGenSettings settings, GenerationReport report)
        {
            var meshes = new Dictionary<long, MeshData>();
            foreach (var road in world.Roads)
            {
                var mesh = new MeshData(2);
                foreach (var piece in road.Pieces)
                    RoadMeshBuilder.AddRibbon(mesh, 0, piece.Points, road.Width, road.Y, piece.ExtendStart, piece.ExtendEnd);
                meshes[road.Id] = mesh;
            }
            report.Count("MarkingDashes", RoadMarkingBuilder.Build(network, settings.roadEdges, meshes));
            foreach (var road in world.Roads)
            {
                var mesh = meshes[road.Id];
                if (mesh.IsEmpty) { report.Skip(SkipReasons.RoadEmptyMesh, road.Id); continue; }
                mesh = mesh.SplitLongTriangles(MaxColliderEdge);
                var anchor = MidPoint(road.Pieces);
                built.Features.Add(new BuiltFeature
                {
                    Kind = FeatureKind.Road, OsmId = road.Id, ElementType = OsmElementType.Way, Name = road.Name,
                    Group = FeatureGroups.Roads, Anchor = anchor, Chunk = ChunkOf(anchor, built.ChunkSize),
                    Mesh = mesh, Materials = new[] { road.Rule.material, settings.roadEdges.marking }, Category = road.Highway, Tags = road.Tags,
                    AddCollider = true
                });
                report.Count("Road");
            }
        }

        static List<Polygon> CampusShapes(ClassifiedWorld world) =>
            world.Areas.Where(a => a.Rule != null && a.Rule.category == AreaCategory.Campus && a.Shape != null).Select(a => a.Shape).ToList();

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

                feature.Materials = BuildingMaterials(settings.MaterialSet(b.Rule.materialSet), b.Id, info.Roof);
                built.Features.Add(feature);
                report.Count(b.Key != null ? "KeyBuilding" : "Building");
            }
        }

        static Material[] BuildingMaterials(BuildingMaterialSet set, long id, RoofKind roof)
        {
            int upper = SplitMix64.Pick(id, "upper", Length(set.upper));
            var roofs = roof == RoofKind.Gable ? set.gableRoof : set.flatRoof;
            var materials = new Material[BuildingMeshBuilder.SubmeshCount];
            materials[BuildingMeshBuilder.SubGround] = At(set.groundFloor, SplitMix64.Pick(id, "ground", Length(set.groundFloor)));
            materials[BuildingMeshBuilder.SubUpper] = At(set.upper, upper);
            materials[BuildingMeshBuilder.SubTrim] = At(set.trim, upper);
            materials[BuildingMeshBuilder.SubRoof] = At(roofs, SplitMix64.Pick(id, "roof", Length(roofs)));
            return materials;
        }

        static void AddInfill(BuiltWorld built, List<InfillHouse> houses, WorldGenSettings settings, GenerationReport report)
        {
            built.Infill.AddRange(houses);
            var byChunk = new Dictionary<Vector2Int, List<InfillHouse>>();
            foreach (var h in houses)
            {
                var chunk = ChunkOf(h.Centroid, built.ChunkSize);
                if (!byChunk.TryGetValue(chunk, out var list)) byChunk[chunk] = list = new List<InfillHouse>();
                list.Add(h);
            }
            var chunks = new List<Vector2Int>(byChunk.Keys);
            chunks.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var meshParams = settings.MeshParams;
            foreach (var chunk in chunks)
            {
                var list = byChunk[chunk];
                list.Sort((a, b) => a.Id.CompareTo(b.Id));
                var materials = new List<Material>();
                var parts = new List<(MeshData mesh, int[] map)>();
                foreach (var h in list)
                {
                    var mesh = BuildingMeshBuilder.Build(h.Spec, meshParams, out var info);
                    if (info.RoofFailed) report.Skip(SkipReasons.RoofTriangulationFailed, h.Id);
                    var picked = BuildingMaterials(settings.residentialMaterials, h.Id, info.Roof);
                    var map = new int[BuildingMeshBuilder.SubmeshCount];
                    for (int s = 0; s < map.Length; s++)
                    {
                        int index = materials.IndexOf(picked[s]);
                        if (index < 0)
                        {
                            index = materials.Count;
                            materials.Add(picked[s]);
                        }
                        map[s] = index;
                    }
                    parts.Add((mesh, map));
                }
                var merged = new MeshData(Mathf.Max(1, materials.Count));
                foreach (var (mesh, map) in parts) merged.Append(mesh, map);
                if (materials.Count == 0) materials.Add(null);
                built.Features.Add(new BuiltFeature
                {
                    Kind = FeatureKind.Building, OsmId = ChunkFeatureId(FeatureGroups.Buildings, "Infill", chunk), ElementType = OsmElementType.Way,
                    Name = "Infill", Group = FeatureGroups.Buildings, Chunk = chunk,
                    Anchor = new Vector2((chunk.x + 0.5f) * built.ChunkSize, (chunk.y + 0.5f) * built.ChunkSize),
                    Mesh = merged, Materials = materials.ToArray(), AddCollider = true, Category = "Infill", Tags = InfillTags
                });
                report.Count("Infill", list.Count);
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
                mesh = mesh.SplitLongTriangles(MaxColliderEdge);
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
                mesh = mesh.SplitLongTriangles(MaxColliderEdge);
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
                mesh = mesh.SplitLongTriangles(MaxColliderEdge);
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

        /// <summary>Stable negative id of a per-chunk generated object (never collides with OSM ids, which are positive).</summary>
        public static long ChunkFeatureId(string group, string name, Vector2Int chunk) =>
            -(long)(SplitMix64.Hash(((long)chunk.x << 32) ^ (uint)chunk.y, group + "/" + name) & 0x3FFF_FFFF_FFFF_FFFFUL) - 1;

        static void AddChunkFeatures(BuiltWorld built, IReadOnlyDictionary<Vector2Int, MeshData> meshes, IReadOnlyDictionary<Vector2Int, MeshData> colliders,
            FeatureKind kind, string group, string name, Material[] materials, GenerationReport report)
        {
            var chunks = new List<Vector2Int>(meshes.Keys);
            chunks.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            foreach (var chunk in chunks)
            {
                var mesh = meshes[chunk];
                if (mesh.IsEmpty) continue;
                MeshData collider = null;
                if (colliders != null && colliders.TryGetValue(chunk, out var c) && !c.IsEmpty) collider = c.SplitLongTriangles(MaxColliderEdge);
                built.Features.Add(new BuiltFeature
                {
                    Kind = kind, OsmId = ChunkFeatureId(group, name, chunk), ElementType = OsmElementType.Way, Name = name,
                    Group = group, Chunk = chunk,
                    Anchor = new Vector2((chunk.x + 0.5f) * built.ChunkSize, (chunk.y + 0.5f) * built.ChunkSize),
                    Mesh = mesh.SplitLongTriangles(MaxColliderEdge), ColliderMesh = collider,
                    Materials = materials, AddCollider = true, HasOsmIdentity = false, Category = name
                });
                report.Count(name);
            }
        }

        static List<WallSegment> AddFrontage(BuiltWorld built, IReadOnlyList<FrontageInterval> plan, WorldGenSettings settings, GenerationReport report)
        {
            var f = settings.frontage;
            var meshes = FrontageMeshBuilder.Build(FrontagePlanner.Walls(plan, f), settings.roadEdges, f, built.ChunkSize);
            AddChunkFeatures(built, meshes.Render, meshes.Collider, FeatureKind.Frontage, FeatureGroups.Frontage, FrontageMeshBuilder.WallsName,
                new[] { f.wallAtlas, settings.roadEdges.concrete, f.fence, f.gateMetal }, report);
            report.Count("WallMeters", Mathf.RoundToInt(meshes.WallMeters));
            foreach (var kv in meshes.MetersByStyle) report.Count("WallMeters." + kv.Key, Mathf.RoundToInt(kv.Value));
            report.Count("Gates", meshes.Gates);
            report.Count("Doors", meshes.Doors);
            report.Count("GutterSlabs", meshes.Slabs);
            return meshes.Blockers;
        }

        static void AddEdges(BuiltWorld built, RoadNetwork network, StreetContext street, WorldGenSettings settings, GenerationReport report)
        {
            var e = settings.roadEdges;
            var edges = RoadEdgeBuilder.Build(network, street, e, built.ChunkSize);
            AddChunkFeatures(built, edges.Gutter, null, FeatureKind.RoadEdge, FeatureGroups.RoadEdges, RoadEdgeBuilder.GutterName,
                new[] { e.concrete, e.gutter }, report);
            AddChunkFeatures(built, edges.Sidewalk, null, FeatureKind.RoadEdge, FeatureGroups.RoadEdges, RoadEdgeBuilder.SidewalkName,
                new[] { e.sidewalk, e.kerb, e.concrete }, report);
            report.Count("EdgeMeters.Gutter", Mathf.RoundToInt(edges.GutterMeters));
            report.Count("EdgeMeters.Sidewalk", Mathf.RoundToInt(edges.SidewalkMeters));
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
            {
                Mix(f.Chunk, f.Mesh != null ? f.Mesh.ContentHash() : unchecked((ulong)f.OsmId));
                if (f.ColliderMesh != null) Mix(f.Chunk, f.ColliderMesh.ContentHash());
            }
            foreach (var t in built.Trees) Mix(ChunkOf(t.Position, built.ChunkSize), Quantize(t));
            foreach (var l in built.Lamps) Mix(ChunkOf(l.Position, built.ChunkSize), Quantize(l));
            foreach (var p in built.Barriers) Mix(ChunkOf(p.Position, built.ChunkSize), Quantize(p));

            report.ChunkHashes.Clear();
            foreach (var kv in hashes) report.ChunkHashes[kv.Key] = kv.Value.ToString("x16", CultureInfo.InvariantCulture);
        }

        static ulong Quantize(PropPlacement p) => unchecked(
            ((ulong)(uint)Mathf.RoundToInt(p.Position.x * 100f) << 32) | (uint)Mathf.RoundToInt(p.Position.y * 100f));
    }
}
