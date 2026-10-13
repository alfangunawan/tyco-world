using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Turns raw OSM elements into typed, projected, cleaned features (spec section 6 and 7).</summary>
    public sealed class FeatureClassifier
    {
        readonly WorldGenSettings settings;
        readonly GenerationReport report;
        readonly GeoProjection projection;
        readonly Rect bounds;
        readonly Dictionary<string, RoadRule> roadRules = new Dictionary<string, RoadRule>();
        readonly Dictionary<string, float> waterwayWidths = new Dictionary<string, float>();
        readonly Dictionary<long, KeyBuilding> keyBuildings = new Dictionary<long, KeyBuilding>();
        readonly AreaRule pavingRule;
        readonly Dictionary<long, Vector2> localCache = new Dictionary<long, Vector2>();
        readonly List<Polygon> campusPolygons = new List<Polygon>();
        OsmData data;

        public FeatureClassifier(WorldGenSettings settings, GenerationReport report)
        {
            this.settings = settings;
            this.report = report;
            projection = settings.CreateProjection();
            bounds = projection.ToLocalRect(settings.Bounds);
            foreach (var rule in settings.roadRules)
                if (rule != null && !string.IsNullOrEmpty(rule.highway)) roadRules[rule.highway] = rule;
            foreach (var w in settings.waterwayWidths)
                if (w != null && !string.IsNullOrEmpty(w.waterway)) waterwayWidths[w.waterway] = w.width;
            foreach (var key in settings.keyBuildings)
                if (key != null && key.osmId != 0) keyBuildings[key.osmId] = key;
            pavingRule = settings.areaRules.FirstOrDefault(a => a != null && a.category == AreaCategory.Paving);
        }

        public ClassifiedWorld Classify(OsmData osm)
        {
            data = osm ?? throw new ArgumentNullException(nameof(osm));
            localCache.Clear();
            campusPolygons.Clear();
            var world = new ClassifiedWorld(bounds);
            CollectCampusPolygons();

            var roadWays = new List<OsmWay>();
            foreach (var way in data.Ways.Values.OrderBy(w => w.Id))
            {
                var tags = way.Tags;
                if (IsBuilding(tags)) { ClassifyBuildingWay(way, world); continue; }

                string highway = tags.Tag("highway");
                if (highway != null && roadRules.ContainsKey(highway))
                {
                    if (tags.Tag("area") != "yes") roadWays.Add(way);
                    else if (pavingRule != null) ClassifyAreaWay(way, pavingRule, world);
                    else report.Ignored++;
                    continue;
                }

                string waterway = tags.Tag("waterway");
                if (waterway != null && waterwayWidths.TryGetValue(waterway, out float waterWidth))
                {
                    ClassifyWaterLine(way, waterWidth, world);
                    continue;
                }
                if (IsWaterArea(tags)) { ClassifyWaterAreaWay(way, world); continue; }

                var rule = MatchArea(tags);
                if (rule != null) { ClassifyAreaWay(way, rule, world); continue; }
                report.Ignored++;
            }
            ClassifyRoads(roadWays, world);

            foreach (var relation in data.Relations.Values.OrderBy(r => r.Id)) ClassifyRelation(relation, world);

            foreach (var node in data.Nodes.Values.OrderBy(n => n.Id))
            {
                if (node.Tags.Tag("natural") != "tree") continue;
                var p = LocalOf(node);
                if (bounds.Contains(p)) world.Trees.Add(new TreeFeature(node.Id, p));
            }

            var classifiedIds = new HashSet<long>(world.Buildings.Select(b => b.Id));
            foreach (var key in keyBuildings.Values.OrderBy(k => k.osmId))
                if (!classifiedIds.Contains(key.osmId))
                    report.Warn("Gedung kunci '" + key.label + "' (OSM id " + key.osmId.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                                ") tidak ditemukan atau dilewati; KeyBuildingMarker tidak dibuat.");
            return world;
        }

        static bool IsBuilding(IReadOnlyDictionary<string, string> tags)
        {
            string value = tags.Tag("building");
            return value != null && value != "no";
        }

        static bool IsWaterArea(IReadOnlyDictionary<string, string> tags)
        {
            if (tags.Tag("natural") == "water") return true;
            string landuse = tags.Tag("landuse");
            if (landuse == "reservoir" || landuse == "basin") return true;
            return tags.ContainsKey("water");
        }

        AreaRule MatchArea(IReadOnlyDictionary<string, string> tags)
        {
            foreach (var rule in settings.areaRules)
                if (rule != null && rule.Matches(tags)) return rule;
            return null;
        }

        Vector2 LocalOf(OsmNode node)
        {
            if (!localCache.TryGetValue(node.Id, out var p))
            {
                p = projection.ToLocal(node.Lat, node.Lon);
                localCache[node.Id] = p;
            }
            return p;
        }

        bool TryResolve(IReadOnlyList<long> nodeIds, out List<Vector2> points)
        {
            points = new List<Vector2>(nodeIds.Count);
            foreach (long id in nodeIds)
            {
                if (!data.Nodes.TryGetValue(id, out var node)) return false;
                points.Add(LocalOf(node));
            }
            return true;
        }

        void CollectCampusPolygons()
        {
            foreach (var way in data.Ways.Values.OrderBy(w => w.Id))
                if (way.Tags.Tag("amenity") == "university" && way.IsClosed && TryResolve(way.NodeIds, out var pts))
                    campusPolygons.Add(new Polygon(Polygon2D.Clean(pts)));
            foreach (var relation in data.Relations.Values.OrderBy(r => r.Id))
                if (relation.Tags.Tag("amenity") == "university" && TryBuildRelationPolygons(relation, out var polygons))
                    campusPolygons.AddRange(polygons);
        }

        bool IsCampus(Vector2 p)
        {
            foreach (var campus in campusPolygons)
                if (campus.Contains(p)) return true;
            return false;
        }

        // ---------- buildings ----------

        void ClassifyBuildingWay(OsmWay way, ClassifiedWorld world)
        {
            if (!way.IsClosed) { report.Skip(SkipReasons.BuildingUnclosed, way.Id); return; }
            if (!TryResolve(way.NodeIds, out var pts)) { report.Skip(SkipReasons.BuildingMissingNodes, way.Id); return; }
            AddBuilding(way.Id, OsmElementType.Way, way.Tags, new Polygon(pts), world);
        }

        void AddBuilding(long id, OsmElementType type, IReadOnlyDictionary<string, string> tags, Polygon raw, ClassifiedWorld world)
        {
            var outer = Polygon2D.Clean(raw.Outer);
            if (outer.Count < 3) { report.Skip(SkipReasons.BuildingTooFewPoints, id); return; }
            Polygon2D.EnsureWinding(outer, true);
            var holes = new List<List<Vector2>>();
            foreach (var h in raw.Holes)
            {
                var hole = Polygon2D.Clean(h);
                if (hole.Count < 3 || Mathf.Abs(Polygon2D.SignedArea(hole)) < 1f) continue;
                Polygon2D.EnsureWinding(hole, false);
                holes.Add(hole);
            }
            if (Polygon2D.IsSelfIntersecting(outer)) { report.Skip(SkipReasons.BuildingSelfIntersecting, id); return; }
            var polygon = new Polygon(outer, holes);
            if (polygon.Area < 4f) { report.Skip(SkipReasons.BuildingTooSmall, id); return; }
            var centroid = Polygon2D.Centroid(outer);
            if (!bounds.Contains(centroid)) { report.Cull(CullReasons.BuildingOutside); return; }

            bool campus = IsCampus(centroid);
            var rule = FindBuildingRule(tags.Tag("building") ?? "yes", campus);
            keyBuildings.TryGetValue(id, out var key);
            var spec = new BuildingSpec
            {
                Id = id,
                Footprint = polygon,
                AllowGable = rule.allowGable,
                RoofOnly = rule.roofOnly,
                RoofOverride = key != null ? key.overrideRoof : RoofOverride.Auto
            };
            ResolveHeight(id, tags, rule, key, spec);
            world.Buildings.Add(new BuildingFeature
            {
                Id = id, ElementType = type, Name = tags.Tag("name"), Spec = spec, Rule = rule,
                IsCampus = campus, Key = key, Centroid = centroid, Tags = tags
            });
        }

        BuildingRule FindBuildingRule(string buildingValue, bool campus)
        {
            foreach (var rule in settings.buildingRules)
                if (rule != null && rule.Matches(buildingValue, campus)) return rule;
            report.Warn("Tidak ada aturan bangunan untuk '" + buildingValue + "'; memakai default 1 lantai.");
            return new BuildingRule { name = "Fallback" };
        }

        void ResolveHeight(long id, IReadOnlyDictionary<string, string> tags, BuildingRule rule, KeyBuilding key, BuildingSpec spec)
        {
            float levelHeight = rule.levelHeightOverride > 0f ? rule.levelHeightOverride : settings.levelHeight;
            spec.LevelHeight = levelHeight;
            int levels;
            float wall;
            if (key != null && key.overrideLevels > 0)
            {
                levels = key.overrideLevels;
                wall = levels * levelHeight;
            }
            else if (TagParsing.TryParseLeadingFloat(tags.Tag("height"), out float height))
            {
                wall = height;
                levels = Mathf.Max(1, (int)Math.Floor(height / levelHeight + 0.5f));
            }
            else if (TagParsing.TryParseLevels(tags.Tag("building:levels"), out int tagged))
            {
                levels = tagged;
                wall = tagged * levelHeight;
            }
            else if (rule.roofOnly)
            {
                levels = 1;
                wall = BuildingMeshBuilder.RoofOnlyHeight;
            }
            else
            {
                levels = rule.minLevels >= rule.maxLevels
                    ? rule.minLevels
                    : SplitMix64.For(id, "levels").RangeInclusive(rule.minLevels, rule.maxLevels);
                wall = levels * levelHeight;
            }
            spec.Levels = Mathf.Max(1, levels);
            spec.WallHeight = Mathf.Max(wall, settings.minWallHeight);
        }

        // ---------- roads and water lines ----------

        void ClassifyRoads(List<OsmWay> ways, ClassifiedWorld world)
        {
            var resolved = new List<(OsmWay way, List<Vector2> points)>();
            foreach (var way in ways)
            {
                if (way.NodeIds.Count < 2 || !TryResolve(way.NodeIds, out var pts))
                {
                    report.Skip(SkipReasons.RoadMissingNodes, way.Id);
                    continue;
                }
                resolved.Add((way, pts));
            }

            var usage = new Dictionary<long, int>();
            foreach (var (way, _) in resolved)
                foreach (long id in new HashSet<long>(way.NodeIds))
                    usage[id] = usage.TryGetValue(id, out int count) ? count + 1 : 1;

            foreach (var (way, points) in resolved)
            {
                var rule = roadRules[way.Tags.Tag("highway")];
                var road = new RoadFeature
                {
                    Id = way.Id,
                    Name = way.Tags.Tag("name"),
                    Highway = rule.highway,
                    Rule = rule,
                    Width = ResolveWidth(way.Tags, rule.defaultWidth),
                    Y = settings.roadBaseY + rule.rank * settings.roadRankStep,
                    Tags = way.Tags
                };
                bool startJunction = usage[way.NodeIds[0]] > 1;
                bool endJunction = usage[way.NodeIds[way.NodeIds.Count - 1]] > 1;
                foreach (var piece in Clipping.ClipPolyline(points, bounds))
                    road.Pieces.Add(new LinePiece(piece.Points, startJunction && piece.StartsAtOriginalStart, endJunction && piece.EndsAtOriginalEnd));
                if (road.Pieces.Count == 0) { report.Cull(CullReasons.RoadOutside); continue; }
                world.Roads.Add(road);
            }
        }

        float ResolveWidth(IReadOnlyDictionary<string, string> tags, float fallback)
        {
            if (TagParsing.TryParseLeadingFloat(tags.Tag("width"), out float width)) return width;
            if (TagParsing.TryParseCount(tags.Tag("lanes"), 20, out int lanes)) return lanes * settings.lanesWidth;
            return fallback;
        }

        void ClassifyWaterLine(OsmWay way, float defaultWidth, ClassifiedWorld world)
        {
            if (way.NodeIds.Count < 2 || !TryResolve(way.NodeIds, out var pts))
            {
                report.Skip(SkipReasons.WaterMissingNodes, way.Id);
                return;
            }
            var line = new WaterLineFeature
            {
                Id = way.Id,
                Name = way.Tags.Tag("name"),
                Width = TagParsing.TryParseLeadingFloat(way.Tags.Tag("width"), out float w) ? w : defaultWidth,
                Y = settings.waterLineY,
                Tags = way.Tags
            };
            foreach (var piece in Clipping.ClipPolyline(pts, bounds)) line.Pieces.Add(new LinePiece(piece.Points, false, false));
            if (line.Pieces.Count == 0) { report.Cull(CullReasons.WaterOutside); return; }
            world.WaterLines.Add(line);
        }

        // ---------- areas ----------

        void ClassifyAreaWay(OsmWay way, AreaRule rule, ClassifiedWorld world)
        {
            if (!way.IsClosed) { report.Skip(SkipReasons.AreaUnclosed, way.Id); return; }
            if (!TryResolve(way.NodeIds, out var pts)) { report.Skip(SkipReasons.AreaMissingNodes, way.Id); return; }
            AddArea(way.Id, OsmElementType.Way, way.Tags, rule, new Polygon(pts), world);
        }

        void ClassifyWaterAreaWay(OsmWay way, ClassifiedWorld world)
        {
            if (!way.IsClosed) { report.Skip(SkipReasons.AreaUnclosed, way.Id); return; }
            if (!TryResolve(way.NodeIds, out var pts)) { report.Skip(SkipReasons.AreaMissingNodes, way.Id); return; }
            AddWaterArea(way.Id, OsmElementType.Way, way.Tags, new Polygon(pts), world);
        }

        void AddArea(long id, OsmElementType type, IReadOnlyDictionary<string, string> tags, AreaRule rule, Polygon raw, ClassifiedWorld world)
        {
            var shape = ClipAndClean(raw);
            if (shape == null) { report.Skip(SkipReasons.AreaDegenerate, id); return; }
            world.Areas.Add(new AreaFeature { Id = id, ElementType = type, Name = tags.Tag("name"), Rule = rule, Shape = shape, Tags = tags });
        }

        void AddWaterArea(long id, OsmElementType type, IReadOnlyDictionary<string, string> tags, Polygon raw, ClassifiedWorld world)
        {
            var shape = ClipAndClean(raw);
            if (shape == null) { report.Skip(SkipReasons.AreaDegenerate, id); return; }
            world.WaterAreas.Add(new WaterAreaFeature { Id = id, ElementType = type, Name = tags.Tag("name"), Shape = shape, Tags = tags });
        }

        Polygon ClipAndClean(Polygon raw)
        {
            var outer = Polygon2D.Clean(Clipping.ClipPolygon(Polygon2D.Clean(raw.Outer), bounds));
            if (outer.Count < 3 || Mathf.Abs(Polygon2D.SignedArea(outer)) < 1f) return null;
            Polygon2D.EnsureWinding(outer, true);
            var holes = new List<List<Vector2>>();
            foreach (var h in raw.Holes)
            {
                var hole = Polygon2D.Clean(Clipping.ClipPolygon(Polygon2D.Clean(h), bounds));
                if (hole.Count < 3 || Mathf.Abs(Polygon2D.SignedArea(hole)) < 1f) continue;
                Polygon2D.EnsureWinding(hole, false);
                holes.Add(hole);
            }
            return new Polygon(outer, holes);
        }

        // ---------- relations ----------

        void ClassifyRelation(OsmRelation relation, ClassifiedWorld world)
        {
            var tags = relation.Tags;
            if (tags.Tag("type") != "multipolygon") { report.Ignored++; return; }
            bool building = IsBuilding(tags);
            bool water = !building && IsWaterArea(tags);
            AreaRule rule = building || water ? null : MatchArea(tags);
            if (!building && !water && rule == null) { report.Ignored++; return; }
            if (!TryBuildRelationPolygons(relation, out var polygons))
            {
                report.Skip(SkipReasons.RelationUnclosedRing, relation.Id);
                return;
            }
            foreach (var polygon in polygons)
            {
                if (building) AddBuilding(relation.Id, OsmElementType.Relation, tags, polygon, world);
                else if (water) AddWaterArea(relation.Id, OsmElementType.Relation, tags, polygon, world);
                else AddArea(relation.Id, OsmElementType.Relation, tags, rule, polygon, world);
            }
        }

        bool TryBuildRelationPolygons(OsmRelation relation, out List<Polygon> polygons)
        {
            polygons = new List<Polygon>();
            var outerWays = new List<IReadOnlyList<long>>();
            var innerWays = new List<IReadOnlyList<long>>();
            foreach (var member in relation.Members)
            {
                if (member.Type != OsmElementType.Way) continue;
                if (!data.Ways.TryGetValue(member.Ref, out var way)) return false;
                if (member.Role == "inner") innerWays.Add(way.NodeIds);
                else outerWays.Add(way.NodeIds);
            }
            if (outerWays.Count == 0) return false;
            if (!RingAssembler.TryAssemble(outerWays, out var outerRings)) return false;
            if (!RingAssembler.TryAssemble(innerWays, out var innerRings)) return false;

            var holes = new List<List<Vector2>>();
            foreach (var ring in innerRings)
            {
                if (!TryResolve(ring, out var pts)) return false;
                holes.Add(pts);
            }
            foreach (var ring in outerRings)
            {
                if (!TryResolve(ring, out var outer)) return false;
                var own = new List<List<Vector2>>();
                foreach (var hole in holes)
                    if (hole.Count > 0 && Polygon2D.ContainsPoint(outer, hole[0])) own.Add(hole);
                polygons.Add(new Polygon(outer, own));
            }
            return true;
        }
    }
}
