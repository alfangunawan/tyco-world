using System;
using System.Collections.Generic;
using TycoWorld.Player;
using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// Passers-by on the walking lanes around the player (spec #6 §2): a pool that fills to the hour's target count
    /// (busy days, empty nights), walks lanes and turns at nodes, stops for the player, talks, flees, chases a thief,
    /// and at deep night lets one pale figure stand by the road until you come close.
    /// </summary>
    public sealed class PedestrianSystem : IKampungSystem
    {
        public const float SpawnMin = 24f, SpawnMax = 70f, DespawnBeyond = 88f;
        public const float SosokMin = 28f, SosokMax = 48f, SosokVanish = 9f, SosokLifetime = 30f;
        public const float TalkSeconds = 3.5f;
        public const float WitnessRadius = 18f;
        public const float CatchDistance = 1.3f;
        const int PoolSize = 30;
        const float PersonalSpace = 1.1f;

        static readonly string[] DayLines =
        {
            "Punten, a.", "Mau ke kampus? Hati-hati, Jalan Sukabirus rame motor.", "Warung depan gorengannya masih anget.",
            "Lagi cari kerja sampingan? Coba aplikasi Kerjaan di HP.", "Distro deket sini lagi diskon jaket, katanya.",
            "Kos Raflesia? Masuk gang yang itu.", "Panas pisan hari ini.", "Jangan lupa makan, a. Kurus gitu."
        };
        static readonly string[] EveningLines =
        {
            "Udah magrib, a. Jangan lama-lama di luar.", "Nanti malam ronda. Katanya ada yang kehilangan motor minggu lalu.",
            "Minimarket yang 24 jam masih buka kok.", "Lampu jalan di ujung gang suka mati sendiri."
        };
        static readonly string[] NightLines =
        {
            "...", "Kamu denger suara dari arah kuburan?", "Pulang, a. Malam di sini nggak enak.", "Jangan lihat ke belakang.",
            "Tadi ada yang berdiri di pinggir jalan. Pas dideketin... nggak ada."
        };

        KampungDirector director;
        LaneGraph graph;
        Transform root;
        readonly List<Pedestrian> pool = new List<Pedestrian>();
        Pedestrian sosok;
        readonly List<int> lanesNear = new List<int>();
        readonly HashSet<Collider> people = new HashSet<Collider>();
        readonly RaycastHit[] hits = new RaycastHit[8];
        SeededRandom rng = new SeededRandom(0x5EED5EEDUL);
        float spawnTimer;
        int serial;

        public int Order => 10;
        /// <summary>Height of the flat world the lanes lie on (0 in the generated world; tests move it).</summary>
        public float GroundLevel { get; set; }
        public LaneGraph Graph => graph;
        public IReadOnlyList<Pedestrian> Pool => pool;
        public Pedestrian Sosok => sosok;
        /// <summary>Raised when a chasing warga reaches the player.</summary>
        public event Action<Pedestrian> Caught;

        public int ActiveCount
        {
            get
            {
                int n = 0;
                foreach (var p in pool)
                    if (p.Active) n++;
                return n;
            }
        }

        public void Begin(KampungDirector owner)
        {
            director = owner;
            graph = new LaneGraph(owner.Data.walkLanes, owner.Data.walkNodes);
            root = new GameObject("Pedestrians").transform;
            root.SetParent(owner.ObjectsRoot, false);
            for (int i = 0; i < PoolSize; i++)
                pool.Add(new Pedestrian(root, i, PersonLook.From(SeededRandom.Mix(0xFACEUL, (ulong)i)), owner.Palette, false, this));
            sosok = new Pedestrian(root, PoolSize, PersonLook.Sosok, owner.Palette, true, this);
            foreach (var p in pool) people.Add(p.Root.GetComponent<Collider>());
            people.Add(sosok.Root.GetComponent<Collider>());
        }

        public void End()
        {
            if (root != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(root.gameObject);
                else UnityEngine.Object.DestroyImmediate(root.gameObject);
            }
            pool.Clear();
            people.Clear();
            sosok = null;
        }

        Vector3 Viewer => director.Player != null ? director.Player.transform.position : Vector3.zero;

        Vector3 ViewForward
        {
            get
            {
                var view = director.Player != null ? director.Player.View : null;
                var f = view != null ? view.forward : Vector3.forward;
                f.y = 0f;
                return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
            }
        }

        public void Tick(float dt)
        {
            if (graph == null || graph.LaneCount == 0) return;
            var viewer = Viewer;
            int target = PedestrianSchedule.TargetCount(director.Hours);
            int active = 0;
            foreach (var p in pool)
            {
                if (!p.Active) continue;
                if (Flat(p.Position - viewer) > DespawnBeyond && p.State != PedestrianState.Chase) Despawn(p);
                else active++;
            }
            // Over the target (dusk falls): let people go home once they are out of sight.
            if (active > target)
                foreach (var p in pool)
                {
                    if (active <= target) break;
                    if (!p.Active || p.State == PedestrianState.Chase || Visible(p.Position, 35f)) continue;
                    Despawn(p);
                    active--;
                }
            spawnTimer -= dt;
            if (spawnTimer <= 0f && active < target)
            {
                spawnTimer = 0.15f;
                foreach (var p in pool)
                {
                    if (p.Active) continue;
                    if (TrySpawn(p, viewer)) active++;
                    break;
                }
            }
            foreach (var p in pool)
                if (p.Active) Step(p, dt, viewer);
            TickSosok(dt, viewer);
        }

        static float Flat(Vector3 v)
        {
            v.y = 0f;
            return v.magnitude;
        }

        bool Visible(Vector3 position, float within)
        {
            var d = position - Viewer;
            d.y = 0f;
            float distance = d.magnitude;
            return distance < within && Vector3.Dot(d / Mathf.Max(distance, 1e-3f), ViewForward) > 0.45f;
        }

        bool TrySpawn(Pedestrian p, Vector3 viewer)
        {
            var at = new Vector2(viewer.x, viewer.z);
            graph.Near(at, SpawnMax, lanesNear);
            if (lanesNear.Count == 0) return false;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                int lane = lanesNear[rng.Range(0, lanesNear.Count)];
                float length = graph.Length(lane);
                if (length < 1f || graph.Lane(lane).width < 0) continue;
                float s = rng.Range(0f, length);
                var point = graph.PointAt(lane, s, out _);
                float d = Vector2.Distance(point, at);
                if (d < SpawnMin || d > SpawnMax) continue;
                var world = new Vector3(point.x, 0f, point.y);
                if (Visible(world, 45f)) continue;
                p.Lane = lane;
                p.S = s;
                p.Dir = rng.Next01() < 0.5f ? 1 : -1;
                p.Speed = rng.Range(Pedestrian.WalkSpeedMin, Pedestrian.WalkSpeedMax);
                p.Lateral = rng.Range(-0.3f, 0.3f);
                p.State = PedestrianState.Walk;
                p.Timer = 0f;
                p.GroundTimer = 0f;
                p.Serial = ++serial;
                p.Random = new SeededRandom(SeededRandom.Mix((ulong)p.Serial, 0xBEEFUL));
                p.Root.SetActive(true);
                Place(p, 0f);
                return true;
            }
            return false;
        }

        void Despawn(Pedestrian p)
        {
            p.Root.SetActive(false);
            p.State = PedestrianState.Walk;
            p.Lane = -1;
        }

        void Step(Pedestrian p, float dt, Vector3 viewer)
        {
            switch (p.State)
            {
                case PedestrianState.Walk: Walk(p, dt, viewer); break;
                case PedestrianState.Wait:
                    p.Timer -= dt;
                    p.CurrentSpeed = 0f;
                    if (p.Timer <= 0f)
                    {
                        // Still in the way: turn back rather than nudging into the player forever.
                        if (Blocked(p, viewer))
                        {
                            p.Dir = -p.Dir;
                            Place(p, 0f);
                        }
                        p.State = PedestrianState.Walk;
                    }
                    break;
                case PedestrianState.Talk:
                    p.Timer -= dt;
                    p.CurrentSpeed = 0f;
                    Face(p, viewer);
                    if (p.Timer <= 0f) p.State = PedestrianState.Walk;
                    break;
                case PedestrianState.Flee:
                    p.Timer -= dt;
                    p.Speed = Pedestrian.RunSpeed;
                    Walk(p, dt, viewer, ignorePlayer: true);
                    if (p.Timer <= 0f)
                    {
                        p.State = PedestrianState.Walk;
                        p.Speed = rng.Range(Pedestrian.WalkSpeedMin, Pedestrian.WalkSpeedMax);
                    }
                    break;
                case PedestrianState.Chase: Chase(p, dt, viewer); break;
            }
            p.Figure.Animate(p.CurrentSpeed, dt);
        }

        static bool Blocked(Pedestrian p, Vector3 viewer)
        {
            var toPlayer = viewer - p.Position;
            toPlayer.y = 0f;
            float d = toPlayer.magnitude;
            return d < PersonalSpace && (d < 1e-3f || Vector3.Dot(toPlayer / d, p.Root.transform.forward) > 0.5f);
        }

        void Walk(Pedestrian p, float dt, Vector3 viewer, bool ignorePlayer = false)
        {
            if (!ignorePlayer && Blocked(p, viewer))
            {
                p.State = PedestrianState.Wait;
                p.Timer = 1.2f;
                p.CurrentSpeed = 0f;
                return;
            }
            p.S += p.Dir * p.Speed * dt;
            float length = graph.Length(p.Lane);
            if (p.S < 0f || p.S > length)
            {
                var lane = graph.Lane(p.Lane);
                int node = p.S > length ? lane.endNode : lane.startNode;
                float over = p.S > length ? p.S - length : -p.S;
                int next = graph.NextLane(node, p.Lane, ref p.Random);
                var nextLane = graph.Lane(next);
                if (next == p.Lane)
                {
                    p.Dir = -p.Dir;
                    p.S = p.S > length ? length - over : over;
                }
                else if (nextLane.startNode == node)
                {
                    p.Lane = next;
                    p.Dir = 1;
                    p.S = Mathf.Min(over, graph.Length(next));
                }
                else
                {
                    p.Lane = next;
                    p.Dir = -1;
                    p.S = Mathf.Max(0f, graph.Length(next) - over);
                }
            }
            p.CurrentSpeed = p.Speed;
            Place(p, dt);
        }

        void Place(Pedestrian p, float dt)
        {
            var point = graph.PointAt(p.Lane, p.S, out var dir);
            var side = new Vector2(dir.y, -dir.x) * p.Dir;
            var flat = point + side * p.Lateral;
            p.GroundTimer -= dt;
            if (p.GroundTimer <= 0f)
            {
                p.GroundTimer = 0.3f;
                p.GroundY = GroundAt(new Vector3(flat.x, 0f, flat.y));
            }
            p.Place(new Vector3(flat.x, p.GroundY, flat.y), new Vector3(dir.x, 0f, dir.y) * p.Dir);
        }

        /// <summary>
        /// Walking height at p: the highest ground hit below knee height, skipping people (their capsules would lift a
        /// walker onto someone's head). Kerbs and slabs are at most 0.21 m, so knee height never meets a counter top.
        /// </summary>
        float GroundAt(Vector3 p)
        {
            int n = Physics.RaycastNonAlloc(new Vector3(p.x, GroundLevel + 0.5f, p.z), Vector3.down, hits, 1.5f, PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            for (int i = 0; i < n; i++)
            {
                if (people.Contains(hits[i].collider)) continue;
                if (hits[i].point.y > best) best = hits[i].point.y;
            }
            return float.IsNegativeInfinity(best) ? GroundLevel : best;
        }

        static void Face(Pedestrian p, Vector3 target)
        {
            var d = target - p.Position;
            d.y = 0f;
            if (d.sqrMagnitude > 1e-4f) p.Root.transform.rotation = Quaternion.Slerp(p.Root.transform.rotation, Quaternion.LookRotation(d), 0.25f);
        }

        void Chase(Pedestrian p, float dt, Vector3 viewer)
        {
            var d = viewer - p.Position;
            d.y = 0f;
            float distance = d.magnitude;
            p.CurrentSpeed = Pedestrian.ChaseSpeed;
            if (distance < CatchDistance)
            {
                p.CurrentSpeed = 0f;
                Caught?.Invoke(p);
                return;
            }
            var dir = d / distance;
            var origin = p.Position + Vector3.up * 0.6f;
            // Around obstacles: try straight, then 45° and 90° to either side.
            foreach (float turn in new[] { 0f, 45f, -45f, 90f, -90f })
            {
                var candidate = Quaternion.Euler(0f, turn, 0f) * dir;
                if (Physics.SphereCast(origin, 0.25f, candidate, out var hit, 0.8f, PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore) &&
                    hit.collider.gameObject != p.Root)
                    continue;
                var next = p.Position + candidate * (Pedestrian.ChaseSpeed * dt);
                next.y = GroundAt(next);
                p.Place(next, candidate);
                return;
            }
            p.CurrentSpeed = 0f;
        }

        void TickSosok(float dt, Vector3 viewer)
        {
            if (sosok == null) return;
            int want = PedestrianSchedule.SosokCount(director.Hours);
            if (!sosok.Active)
            {
                if (want == 0) return;
                sosok.Timer -= dt;
                if (sosok.Timer > 0f) return;
                sosok.Timer = 6f;
                TrySpawnSosok(viewer);
                return;
            }
            sosok.Timer -= dt;
            Face(sosok, viewer);
            sosok.CurrentSpeed = 0f;
            float distance = Flat(sosok.Position - viewer);
            if (want == 0 || distance < SosokVanish || distance > DespawnBeyond || sosok.Timer <= 0f)
            {
                bool seen = distance < SosokVanish;
                sosok.Root.SetActive(false);
                sosok.Timer = seen ? 40f : 8f;
                if (seen) Vanished?.Invoke();
            }
        }

        /// <summary>The figure stood its ground until the player came close, then was gone.</summary>
        public event Action Vanished;

        bool TrySpawnSosok(Vector3 viewer)
        {
            var at = new Vector2(viewer.x, viewer.z);
            graph.Near(at, SosokMax, lanesNear);
            var forward = ViewForward;
            for (int attempt = 0; attempt < 12 && lanesNear.Count > 0; attempt++)
            {
                int lane = lanesNear[rng.Range(0, lanesNear.Count)];
                float s = rng.Range(0f, graph.Length(lane));
                var point = graph.PointAt(lane, s, out _);
                float d = Vector2.Distance(point, at);
                if (d < SosokMin || d > SosokMax) continue;
                var world = new Vector3(point.x, 0f, point.y);
                var dir = (world - viewer);
                dir.y = 0f;
                if (Vector3.Dot(dir.normalized, forward) < 0.6f) continue;
                world.y = GroundAt(world);
                if (!InSight(world + Vector3.up * 1.5f)) continue;
                sosok.Root.SetActive(true);
                sosok.Place(world, -dir);
                sosok.State = PedestrianState.Stare;
                sosok.Timer = SosokLifetime;
                return true;
            }
            return false;
        }

        /// <summary>A clear line from the player's eyes to the point (the figure must be seen to be felt).</summary>
        bool InSight(Vector3 point)
        {
            var view = director.Player != null ? director.Player.View : null;
            var eye = view != null ? view.position : Viewer + Vector3.up * 1.6f;
            var to = point - eye;
            float d = to.magnitude;
            return d < 1e-3f || !Physics.Raycast(eye, to / d, d - 0.4f, PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Spawns the figure at once (tests, MCP) on a lane ahead of the player.</summary>
        public bool ForceSosok() => TrySpawnSosok(Viewer);

        // ---------- talking ----------

        public void TalkTo(Pedestrian p)
        {
            if (p == null || !p.Active) return;
            p.State = PedestrianState.Talk;
            p.Timer = TalkSeconds;
            float h = director.Hours;
            var lines = h >= 21f || h < 5f ? NightLines : h >= 17.5f ? EveningLines : DayLines;
            director.Toast("Warga: \"" + lines[p.Random.Range(0, lines.Length)] + "\"");
        }

        // ---------- witnesses and the angry crowd ----------

        /// <summary>Active (non-chasing) people within <paramref name="radius"/> of <paramref name="point"/> who can see it.</summary>
        public List<Pedestrian> Witnesses(Vector3 point, float radius)
        {
            var result = new List<Pedestrian>();
            foreach (var p in pool)
            {
                if (!p.Active || p.State == PedestrianState.Chase) continue;
                var eye = p.Position + Vector3.up * 1.6f;
                var to = point + Vector3.up * 1f - eye;
                float d = to.magnitude;
                if (d > radius) continue;
                if (Physics.Raycast(eye, to / d, out var hit, d - 0.6f, PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore) &&
                    hit.collider.gameObject != p.Root) continue;
                result.Add(p);
            }
            return result;
        }

        /// <summary>A thief was seen: the nearest <paramref name="chasers"/> witnesses give chase, the rest run off. Returns the chasers.</summary>
        public int Alarm(Vector3 point, List<Pedestrian> witnesses, int chasers)
        {
            witnesses.Sort((a, b) => Flat(a.Position - point).CompareTo(Flat(b.Position - point)));
            int chasing = 0;
            foreach (var p in witnesses)
            {
                if (chasing < chasers)
                {
                    p.State = PedestrianState.Chase;
                    chasing++;
                }
                else
                {
                    p.State = PedestrianState.Flee;
                    p.Timer = 4f;
                    var away = p.Position - point;
                    var forward = p.Root.transform.forward;
                    if (Vector3.Dot(away, forward) < 0f) p.Dir = -p.Dir;
                }
            }
            return chasing;
        }

        public int ChaserCount
        {
            get
            {
                int n = 0;
                foreach (var p in pool)
                    if (p.Active && p.State == PedestrianState.Chase) n++;
                return n;
            }
        }

        /// <summary>The crowd gives up: chasers walk off (and despawn when out of range).</summary>
        public void CalmDown()
        {
            foreach (var p in pool)
            {
                if (!p.Active || p.State != PedestrianState.Chase) continue;
                if (graph.LaneCount == 0)
                {
                    Despawn(p);
                    continue;
                }
                // Back onto the nearest lane.
                var at = new Vector2(p.Position.x, p.Position.z);
                graph.Near(at, 15f, lanesNear);
                int best = -1;
                float bestDistance = float.PositiveInfinity, bestS = 0f;
                foreach (int lane in lanesNear)
                {
                    float s = graph.Project(lane, at, out float d);
                    if (d < bestDistance)
                    {
                        best = lane;
                        bestDistance = d;
                        bestS = s;
                    }
                }
                if (best < 0)
                {
                    Despawn(p);
                    continue;
                }
                p.Lane = best;
                p.S = bestS;
                p.State = PedestrianState.Walk;
                p.Speed = rng.Range(Pedestrian.WalkSpeedMin, Pedestrian.WalkSpeedMax);
            }
        }

        /// <summary>Puts a walker at a lane position (tests).</summary>
        public Pedestrian SpawnAt(int lane, float s, int dir)
        {
            foreach (var p in pool)
            {
                if (p.Active) continue;
                p.Lane = lane;
                p.S = s;
                p.Dir = dir;
                p.Speed = 1.3f;
                p.Lateral = 0f;
                p.State = PedestrianState.Walk;
                p.Serial = ++serial;
                p.Random = new SeededRandom(SeededRandom.Mix((ulong)p.Serial, 0xBEEFUL));
                p.Root.SetActive(true);
                Place(p, 0f);
                return p;
            }
            return null;
        }
    }
}
