using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TycoWorld.WorldGen.Authoring;
using UnityEngine;

namespace TycoWorld.Gameplay.Authoring
{
    /// <summary>
    /// Bakes KampungData from the same steps as Generate (spec #6 §3): classify, road network, edges, frontage pass 1,
    /// infill, frontage pass 2. Pure and deterministic: the same world and settings give the same data.
    /// </summary>
    public static class KampungBaker
    {
        public const float MinShopSpacing = 45f;
        public const int MaxShops = 70;
        public const float MinShopFront = 4f;
        public const float NodeMergeRadius = 6f;
        /// <summary>A dead-end lane end looks this far for another node to cross to (the far side of a wide road, a corner).</summary>
        public const float CrossingRadius = 11f;
        public const float KosNeighbourhood = 220f;
        public const int MaxBikes = 120;
        public const int MaxCars = 30;
        public const float CarRoadMinWidth = 6f;
        const float SimplifyTolerance = 0.25f;

        static readonly string[] WalkableWithoutEdge = { "footway", "path", "pedestrian", "steps", "track", "cycleway", "service", "living_street" };

        public static KampungData Bake(ClassifiedWorld world, WorldGenSettings settings, GenerationReport report)
        {
            var e = settings.roadEdges;
            var f = settings.frontage;
            var campus = world.Areas.Where(a => a.Rule != null && a.Rule.category == AreaCategory.Campus).Select(a => a.Shape).ToList();
            var net = new RoadNetwork(world.Roads, campus, e);
            var street = StreetContext.From(world, settings);
            var edges = RoadEdgeBuilder.Build(net, street, e, settings.chunkSize > 0f ? settings.chunkSize : 100f);
            var pass1 = FrontagePlanner.Plan(net, street, settings);
            InfillPlanner.Plan(pass1, net, street, settings, report);
            var plan = FrontagePlanner.Plan(net, street, settings);
            var walls = FrontagePlanner.Walls(plan, f);

            var data = ScriptableObject.CreateInstance<KampungData>();
            data.bounds = world.LocalBounds;
            WalkLanes(net, edges, e, out data.walkLanes, out data.walkNodes);
            data.mapRoads = MapRoads(net);
            Vector2 kos = KosCentre(world, f);
            data.shops = Shops(plan, street, e, f, kos);
            var spots = new List<Spot>();
            Doors(walls, street, e, f, spots);
            Parking(data.shops, spots, net, street, e, f);
            Landmarks(world, f, kos, data.walkLanes, data.walkNodes, campus, spots);
            data.spots = spots.ToArray();
            data.summary = string.Format(CultureInfo.InvariantCulture, "shops={0} spots={1} walkLanes={2} walkNodes={3} mapRoads={4}",
                data.shops.Length, data.spots.Length, data.walkLanes.Length, data.walkNodes.Length, data.mapRoads.Length);
            return data;
        }

        // ---------- walking lanes ----------

        static void WalkLanes(RoadNetwork net, RoadEdgeResult edges, RoadEdgeSettings e, out Lane[] lanes, out LaneNode[] nodes)
        {
            var list = new List<Lane>();
            var along = new List<float>();
            foreach (var run in edges.Runs)
            {
                var chain = run.Chain;
                float offset = chain.HalfWidth + (run.Profile == EdgeProfile.Sidewalk
                    ? e.kerbWidth + (e.sidewalkWidth - e.kerbWidth) * 0.5f
                    : e.shoulderWidth * 0.5f);
                var line = Polyline2D.Offset(Polyline2D.Slice(chain.Points, chain.Cum, run.S0, run.S1, along), along, run.Side * offset, new List<float>());
                AddLane(list, line, WidthClass(chain));
            }
            foreach (var chain in net.Chains)
            {
                if (chain.BaseProfile != EdgeProfile.None || chain.Points.Count < 2 || !RoadNetwork.Contains(WalkableWithoutEdge, chain.Highway)) continue;
                AddLane(list, new List<Vector2>(chain.Points), WidthClass(chain));
            }
            nodes = Connect(list);
            lanes = list.ToArray();
        }

        static void AddLane(List<Lane> list, List<Vector2> line, int width)
        {
            var clean = Simplify(Dedup(line), SimplifyTolerance);
            if (clean.Count < 2) return;
            var lane = new Lane { points = clean.ToArray(), width = width };
            if (lane.Length < 2f) return;
            list.Add(lane);
        }

        /// <summary>
        /// Lane ends within NodeMergeRadius share a node (corners, narrow crossings, dead ends); first come, first served. Then
        /// every dead end gets a straight crossing lane to the nearest other node within CrossingRadius (the far side of a
        /// wide road, the corner across a junction mouth), so pedestrians walk on instead of turning back.
        /// </summary>
        static LaneNode[] Connect(List<Lane> lanes)
        {
            var positions = new List<Vector2>();
            var members = new List<List<int>>();
            int NodeFor(Vector2 p)
            {
                int best = -1;
                float bestDistance = NodeMergeRadius;
                for (int i = 0; i < positions.Count; i++)
                {
                    float d = Vector2.Distance(positions[i], p);
                    if (d < bestDistance)
                    {
                        best = i;
                        bestDistance = d;
                    }
                }
                if (best >= 0) return best;
                positions.Add(p);
                members.Add(new List<int>());
                return positions.Count - 1;
            }
            void Link(int laneIndex)
            {
                var lane = lanes[laneIndex];
                members[lane.startNode].Add(laneIndex);
                if (lane.endNode != lane.startNode) members[lane.endNode].Add(laneIndex);
            }
            for (int i = 0; i < lanes.Count; i++)
            {
                var lane = lanes[i];
                lane.startNode = NodeFor(lane.points[0]);
                lane.endNode = NodeFor(lane.points[lane.points.Length - 1]);
                Link(i);
            }
            int original = positions.Count;
            for (int n = 0; n < original; n++)
            {
                if (members[n].Count != 1) continue;
                var own = lanes[members[n][0]];
                int other = own.startNode == n ? own.endNode : own.startNode;
                int best = -1;
                float bestDistance = CrossingRadius;
                for (int m = 0; m < original; m++)
                {
                    if (m == n || m == other) continue;
                    float d = Vector2.Distance(positions[m], positions[n]);
                    if (d < bestDistance)
                    {
                        best = m;
                        bestDistance = d;
                    }
                }
                if (best < 0 || members[best].Any(l => lanes[l].startNode == n || lanes[l].endNode == n)) continue;
                lanes.Add(new Lane { points = new[] { positions[n], positions[best] }, startNode = n, endNode = best, width = -1 });
                Link(lanes.Count - 1);
            }
            var nodes = new LaneNode[positions.Count];
            for (int i = 0; i < nodes.Length; i++) nodes[i] = new LaneNode { position = positions[i], lanes = members[i].ToArray() };
            return nodes;
        }

        static int WidthClass(RoadChain chain)
        {
            switch (chain.Highway)
            {
                case "primary":
                case "secondary":
                case "tertiary": return 2;
                case "residential":
                case "unclassified":
                case "living_street":
                case "service": return 1;
                default: return 0;
            }
        }

        static Lane[] MapRoads(RoadNetwork net)
        {
            var list = new List<Lane>();
            foreach (var chain in net.Chains)
            {
                if (chain.Points.Count < 2) continue;
                var clean = Simplify(Dedup(chain.Points), 0.6f);
                if (clean.Count >= 2) list.Add(new Lane { points = clean.ToArray(), width = WidthClass(chain) });
            }
            return list.ToArray();
        }

        // ---------- shops ----------

        sealed class Candidate
        {
            public RoadChain Chain;
            public int Side;
            public float S;
            public Vector2 Counter, Outward;
            public float SlabLength;
            public long Building;
            public ulong Hash;
        }

        static ShopSite[] Shops(List<FrontageInterval> plan, StreetContext street, RoadEdgeSettings e, FrontageSettings f, Vector2 kos)
        {
            var candidates = new List<Candidate>();
            foreach (var x in plan)
            {
                if (x.Kind != FrontageKind.Flush || x.Length < MinShopFront || x.Profile == EdgeProfile.None) continue;
                float s = 0.5f * (x.S0 + x.S1);
                var c = x.Chain.PointAt(s, out var t);
                var outward = Polyline2D.LeftNormal(t) * x.Side;
                float lineOffset = FrontagePlanner.LineOffset(x.Chain, x.Profile, e, f);
                var counter = c + outward * lineOffset;
                var building = street.Raycast(counter, outward, 3f, out _);
                if (building == null || building.Excluded || street.DistanceToKos(counter) < f.kosExclusionRadius) continue;
                if (street.InsideBuilding(counter - outward * 0.3f)) continue;
                candidates.Add(new Candidate
                {
                    Chain = x.Chain, Side = x.Side, S = s, Counter = counter, Outward = outward,
                    SlabLength = x.Profile == EdgeProfile.Gutter ? lineOffset - x.Chain.HalfWidth - e.shoulderWidth : 0f,
                    Building = building.Id,
                    Hash = SplitMix64.Hash(x.Chain.Key, "shop:" + x.Side.ToString(CultureInfo.InvariantCulture) + ":" + Mathf.RoundToInt(s * 10f).ToString(CultureInfo.InvariantCulture))
                });
            }
            // Near the kos first, so the start of the game always has shops within reach; then by hash for an even spread.
            candidates.Sort((a, b) =>
            {
                bool na = Vector2.Distance(a.Counter, kos) < KosNeighbourhood, nb = Vector2.Distance(b.Counter, kos) < KosNeighbourhood;
                if (na != nb) return na ? -1 : 1;
                return a.Hash.CompareTo(b.Hash);
            });
            var chosen = new List<Candidate>();
            foreach (var c in candidates)
            {
                if (chosen.Count >= MaxShops) break;
                bool clear = true;
                foreach (var o in chosen)
                    if (Vector2.Distance(o.Counter, c.Counter) < MinShopSpacing)
                    {
                        clear = false;
                        break;
                    }
                if (clear) chosen.Add(c);
            }
            var types = new ShopType[chosen.Count];
            // A second hash for the type: candidates were picked in Hash order, so Hash itself is skewed low.
            for (int i = 0; i < chosen.Count; i++) types[i] = WeightedType(new SplitMix64(chosen[i].Hash ^ 0x9E3779B97F4A7C15UL).NextULong());
            EnsureNearKos(chosen, types, kos);
            var used = new Dictionary<string, int>();
            var sites = new ShopSite[chosen.Count];
            for (int i = 0; i < chosen.Count; i++)
            {
                var c = chosen[i];
                var face = -c.Outward;
                sites[i] = new ShopSite
                {
                    id = "shop-" + i.ToString(CultureInfo.InvariantCulture),
                    type = types[i],
                    name = Name(types[i], c.Hash, used),
                    counter = new Vector3(c.Counter.x, 0f, c.Counter.y),
                    yaw = Mathf.Atan2(face.x, face.y) * Mathf.Rad2Deg,
                    slabLength = c.SlabLength,
                    buildingId = c.Building
                };
            }
            return sites;
        }

        static ShopType WeightedType(ulong hash)
        {
            double r = (hash >> 11) * (1.0 / (1UL << 53));
            if (r < 0.30) return ShopType.Warung;
            if (r < 0.52) return ShopType.WarungMakan;
            if (r < 0.62) return ShopType.Distro;
            if (r < 0.72) return ShopType.Konter;
            if (r < 0.80) return ShopType.Apotek;
            if (r < 0.90) return ShopType.Bengkel;
            return ShopType.Minimarket;
        }

        /// <summary>Every kind of shop the first day needs must exist near the kos: the nearest unclaimed shops take them.</summary>
        static void EnsureNearKos(List<Candidate> chosen, ShopType[] types, Vector2 kos)
        {
            var order = Enumerable.Range(0, chosen.Count).OrderBy(i => Vector2.Distance(chosen[i].Counter, kos)).ThenBy(i => i).ToList();
            var claimed = new HashSet<int>();
            foreach (var need in new[] { ShopType.Warung, ShopType.WarungMakan, ShopType.Konter, ShopType.Distro, ShopType.Bengkel, ShopType.Minimarket })
            {
                float radius = KosNeighbourhood * (need == ShopType.Minimarket || need == ShopType.Bengkel ? 2f : 1f);
                int found = -1;
                foreach (int i in order)
                {
                    if (Vector2.Distance(chosen[i].Counter, kos) >= radius) break;
                    if (claimed.Contains(i) || types[i] != need) continue;
                    found = i;
                    break;
                }
                if (found < 0)
                    foreach (int i in order)
                    {
                        if (claimed.Contains(i)) continue;
                        types[i] = need;
                        found = i;
                        break;
                    }
                if (found >= 0) claimed.Add(found);
            }
        }

        static readonly Dictionary<ShopType, string[]> Names = new Dictionary<ShopType, string[]>
        {
            { ShopType.Warung, new[] { "Warung Bu Tini", "Toko Berkah", "Warung Mak Ijah", "Toko Sumber Rejeki", "Warung Pak Dadang", "Toko Barokah", "Warung Teh Euis", "Toko Lancar Jaya" } },
            { ShopType.WarungMakan, new[] { "Warung Nasi Bu Ani", "Warteg Sederhana", "Warung Nasi Teh Imas", "Rumah Makan Sunda Asih", "Mi Ayam Pak Kumis", "Nasi Goreng Mang Ujang" } },
            { ShopType.Distro, new[] { "Distro Kampus", "Distro Sukabirus", "Toko Baju Rizky", "Distro Anak Teknik" } },
            { ShopType.Konter, new[] { "Konter Jaya Cell", "Konter Barokah Cell", "Konter Sinar Cell" } },
            { ShopType.Apotek, new[] { "Apotek Sehat Sentosa", "Apotek Bojong Farma", "Apotek Rakyat" } },
            { ShopType.Bengkel, new[] { "Bengkel Jaya Motor", "Bengkel Mang Asep", "Bengkel Kencana Motor" } },
            { ShopType.Minimarket, new[] { "Kampus Mart 24 Jam", "Sukabirus Mart 24 Jam", "Bojong Mart 24 Jam" } },
        };

        static string Name(ShopType type, ulong hash, Dictionary<string, int> used)
        {
            var pool = Names[type];
            string name = pool[(int)(hash % (ulong)pool.Length)];
            for (int k = 0; k < pool.Length && used.ContainsKey(name); k++) name = pool[(int)((hash + (ulong)k + 1UL) % (ulong)pool.Length)];
            used.TryGetValue(name, out int n);
            used[name] = n + 1;
            return n == 0 ? name : name + " " + (n + 1).ToString(CultureInfo.InvariantCulture);
        }

        // ---------- doors and parking ----------

        static void Doors(List<WallRun> walls, StreetContext street, RoadEdgeSettings e, FrontageSettings f, List<Spot> spots)
        {
            foreach (var run in walls)
                foreach (var o in run.Openings)
                {
                    var c = run.Chain.PointAt(o.S, out var t);
                    var outward = Polyline2D.LeftNormal(t) * run.Side;
                    var p = c + outward * FrontagePlanner.LineOffset(run.Chain, run.Profile, e, f);
                    var building = street.Raycast(p, outward, f.corridorDepth, out _);
                    var face = -outward;
                    spots.Add(new Spot
                    {
                        kind = SpotKind.Door, position = new Vector3(p.x, 0f, p.y), yaw = Mathf.Atan2(face.x, face.y) * Mathf.Rad2Deg,
                        refId = building != null ? building.Id : 0
                    });
                }
        }

        static void Parking(ShopSite[] shops, List<Spot> spots, RoadNetwork net, StreetContext street, RoadEdgeSettings e, FrontageSettings f)
        {
            var near = new Dictionary<RoadChain, float>();
            int bikes = 0, cars = 0;
            var taken = new List<Vector2>();
            bool Free(Vector2 p, float clearance)
            {
                if (!street.InBounds(p) || street.InsideBuilding(p)) return false;
                foreach (var q in taken)
                    if (Vector2.Distance(p, q) < clearance) return false;
                return true;
            }
            void Add(SpotKind kind, Vector2 p, Vector2 along, long refId)
            {
                taken.Add(p);
                spots.Add(new Spot { kind = kind, position = new Vector3(p.x, 0f, p.y), yaw = Mathf.Atan2(along.x, along.y) * Mathf.Rad2Deg, refId = refId });
            }
            // Every shop gets a bike beside its front, on the asphalt edge.
            foreach (var shop in shops)
            {
                if (bikes >= MaxBikes) break;
                var counter = new Vector2(shop.counter.x, shop.counter.z);
                var face = new Vector2(Mathf.Sin(shop.yaw * Mathf.Deg2Rad), Mathf.Cos(shop.yaw * Mathf.Deg2Rad));
                var along = new Vector2(face.y, -face.x);
                var p = counter + face * (shop.slabLength + e.shoulderWidth + 0.45f) + along * 3.2f;
                if (!Free(p, 2.5f)) continue;
                Add(SpotKind.ParkingBike, p, along, shop.buildingId);
                bikes++;
            }
            // Some house gates: a bike in front (1 in 4), a car on wide roads (1 in 6), never near a junction.
            foreach (var door in spots.Where(s => s.kind == SpotKind.Door).ToList())
            {
                var gate = new Vector2(door.position.x, door.position.z);
                var face = new Vector2(Mathf.Sin(door.yaw * Mathf.Deg2Rad), Mathf.Cos(door.yaw * Mathf.Deg2Rad));
                var along = new Vector2(face.y, -face.x);
                ulong h = SplitMix64.Hash(door.refId, "park:" + Mathf.RoundToInt(gate.x * 10f).ToString(CultureInfo.InvariantCulture) + ":" + Mathf.RoundToInt(gate.y * 10f).ToString(CultureInfo.InvariantCulture));
                var road = ClosestChain(net, gate, near, out float hw);
                if (road == null) continue;
                if (cars < MaxCars && road.Width >= CarRoadMinWidth && h % 6UL == 0UL)
                {
                    var p = SnapToLane(road, gate, hw - 1.05f) + along * 3.5f;
                    if (Free(p, 5.5f) && !NearOtherRoad(net, road, p, near, 9f))
                    {
                        Add(SpotKind.ParkingCar, p, along, door.refId);
                        cars++;
                        continue;
                    }
                }
                if (bikes < MaxBikes && h % 4UL == 1UL)
                {
                    var p = SnapToLane(road, gate, hw - 0.4f) + along * 2.6f;
                    if (Free(p, 2.5f) && !NearOtherRoad(net, road, p, near, 6f))
                    {
                        Add(SpotKind.ParkingBike, p, along, door.refId);
                        bikes++;
                    }
                }
            }
        }

        static RoadChain ClosestChain(RoadNetwork net, Vector2 p, Dictionary<RoadChain, float> near, out float halfWidth)
        {
            net.Nearby(p, net.MaxReach + 3f, null, near);
            RoadChain best = null;
            float bestDistance = float.PositiveInfinity;
            foreach (var kv in near)
                if (kv.Value < bestDistance || (Mathf.Approximately(kv.Value, bestDistance) && best != null && kv.Key.Index < best.Index))
                {
                    best = kv.Key;
                    bestDistance = kv.Value;
                }
            halfWidth = best != null ? best.HalfWidth : 0f;
            return best;
        }

        /// <summary>The point at <paramref name="offset"/> from the road's centreline, on the side of <paramref name="toward"/>.</summary>
        static Vector2 SnapToLane(RoadChain road, Vector2 toward, float offset)
        {
            float s = Polyline2D.Project(road.Points, road.Cum, toward, out _);
            var c = road.PointAt(s, out var t);
            var n = Polyline2D.LeftNormal(t);
            return c + n * (Vector2.Dot(toward - c, n) >= 0f ? offset : -offset);
        }

        static bool NearOtherRoad(RoadNetwork net, RoadChain road, Vector2 p, Dictionary<RoadChain, float> near, float radius)
        {
            net.Nearby(p, radius + net.MaxHalfWidth, road, near);
            foreach (var kv in near)
                if (kv.Value < radius + kv.Key.HalfWidth) return true;
            return false;
        }

        // ---------- landmarks ----------

        static Vector2 KosCentre(ClassifiedWorld world, FrontageSettings f)
        {
            foreach (var b in world.Buildings)
                if (b.Id == f.kosBuildingId) return b.Centroid;
            return world.LocalBounds.center;
        }

        static void Landmarks(ClassifiedWorld world, FrontageSettings f, Vector2 kos, Lane[] lanes, LaneNode[] nodes, List<Polygon> campus, List<Spot> spots)
        {
            if (lanes.Length == 0) return;
            void AddNear(SpotKind kind, Vector2 target, long refId)
            {
                var p = ClosestLanePoint(lanes, target, out var along);
                spots.Add(new Spot { kind = kind, position = new Vector3(p.x, 0f, p.y), yaw = Mathf.Atan2(along.x, along.y) * Mathf.Rad2Deg, refId = refId });
            }
            AddNear(SpotKind.Kos, kos, f.kosBuildingId);
            foreach (var b in world.Buildings.OrderBy(b => b.Id))
                if (b.Tags.TryGetValue("amenity", out var amenity) && amenity == "place_of_worship") AddNear(SpotKind.Mosque, b.Centroid, b.Id);
            foreach (var a in world.Areas.OrderBy(a => a.Id))
            {
                if (a.Rule == null) continue;
                if (a.Rule.category == AreaCategory.Cemetery) AddNear(SpotKind.Cemetery, Polygon2D.Centroid(a.Shape.Outer), a.Id);
                else if (a.Tags.TryGetValue("amenity", out var amenity) && amenity == "place_of_worship") AddNear(SpotKind.Mosque, Polygon2D.Centroid(a.Shape.Outer), a.Id);
            }
            // The campus gate: where the campus boundary comes closest to the kos.
            Vector2 bestGate = default;
            float bestGateDistance = float.PositiveInfinity;
            foreach (var shape in campus)
                foreach (var v in shape.Outer)
                {
                    float d = Vector2.Distance(v, kos);
                    if (d < bestGateDistance)
                    {
                        bestGateDistance = d;
                        bestGate = v;
                    }
                }
            if (!float.IsPositiveInfinity(bestGateDistance)) AddNear(SpotKind.CampusGate, bestGate, 0);
            // Pos ronda: three junctions (3+ lanes) 80-320 m from the kos, spread around it.
            var junctions = Enumerable.Range(0, nodes.Length)
                .Where(i => nodes[i].lanes.Length >= 3)
                .Select(i => (i, d: Vector2.Distance(nodes[i].position, kos)))
                .Where(x => x.d >= 80f && x.d <= 320f)
                .OrderBy(x => x.d).ThenBy(x => x.i)
                .ToList();
            var picked = new List<Vector2>();
            foreach (var (i, _) in junctions)
            {
                var p = nodes[i].position;
                if (picked.Any(q => Vector2.Distance(q, p) < 90f)) continue;
                picked.Add(p);
                spots.Add(new Spot { kind = SpotKind.PosRonda, position = new Vector3(p.x, 0f, p.y), refId = i });
                if (picked.Count == 3) break;
            }
        }

        static Vector2 ClosestLanePoint(Lane[] lanes, Vector2 target, out Vector2 along)
        {
            Vector2 best = target;
            along = Vector2.up;
            float bestDistance = float.PositiveInfinity;
            foreach (var lane in lanes)
                for (int i = 0; i + 1 < lane.points.Length; i++)
                {
                    Vector2 a = lane.points[i], b = lane.points[i + 1], ab = b - a;
                    float len2 = ab.sqrMagnitude;
                    float t = len2 > 1e-8f ? Mathf.Clamp01(Vector2.Dot(target - a, ab) / len2) : 0f;
                    var q = a + ab * t;
                    float d = Vector2.Distance(q, target);
                    if (d >= bestDistance) continue;
                    bestDistance = d;
                    best = q;
                    along = len2 > 1e-8f ? ab.normalized : Vector2.up;
                }
            return best;
        }

        // ---------- polyline helpers ----------

        static List<Vector2> Dedup(IReadOnlyList<Vector2> line)
        {
            var result = new List<Vector2>(line.Count);
            foreach (var p in line)
                if (result.Count == 0 || (p - result[result.Count - 1]).sqrMagnitude > 1e-4f) result.Add(p);
            return result;
        }

        /// <summary>Ramer-Douglas-Peucker: drops points within <paramref name="tolerance"/> of the line between kept neighbours.</summary>
        public static List<Vector2> Simplify(List<Vector2> line, float tolerance)
        {
            if (line.Count < 3) return line;
            var keep = new bool[line.Count];
            keep[0] = keep[line.Count - 1] = true;
            var stack = new Stack<(int a, int b)>();
            stack.Push((0, line.Count - 1));
            while (stack.Count > 0)
            {
                var (a, b) = stack.Pop();
                int index = -1;
                float worst = tolerance;
                for (int i = a + 1; i < b; i++)
                {
                    float d = Polygon2D.DistanceToSegment(line[i], line[a], line[b]);
                    if (d > worst)
                    {
                        worst = d;
                        index = i;
                    }
                }
                if (index < 0) continue;
                keep[index] = true;
                stack.Push((a, index));
                stack.Push((index, b));
            }
            var result = new List<Vector2>();
            for (int i = 0; i < line.Count; i++)
                if (keep[i]) result.Add(line[i]);
            return result;
        }
    }
}
