using System;
using System.Collections.Generic;
using TycoWorld.Player;
using TycoWorld.Vehicles;
using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// What a mission may ask of the world (spec #6 §4): the player, the baked places, random picks from a seeded
    /// stream, people and props under the mission root, money and toasts. Owned by the MissionSystem.
    /// </summary>
    public sealed class MissionContext
    {
        readonly KampungDirector director;
        readonly Transform root;
        readonly List<int> near = new List<int>();
        SeededRandom rng;

        public MissionContext(KampungDirector owner, Transform objectsRoot, ulong seed)
        {
            director = owner;
            root = objectsRoot;
            rng = new SeededRandom(seed);
        }

        public KampungDirector Director => director;
        public GameState State => director.State;
        public KampungData Data => director.Data;
        public ItemCatalog Catalog => director.Catalog;
        public Palette Palette => director.Palette;
        public Transform Root => root;
        public LaneGraph Lanes => director.Lanes;
        public float Hours => director.Hours;
        public PedestrianSystem People => director.GetSystem<PedestrianSystem>();

        public void Reseed(ulong seed) => rng = new SeededRandom(seed);
        public float Random01() => rng.Next01();
        public int Range(int min, int max) => rng.Range(min, max);
        public float Range(float min, float max) => rng.Range(min, max);
        public T Pick<T>(IReadOnlyList<T> list) => list[rng.Range(0, list.Count)];

        public Vector3 PlayerPosition => director.Player != null ? director.Player.transform.position : Vector3.zero;

        public Vector3 ViewForward
        {
            get
            {
                var view = director.Player != null ? director.Player.View : null;
                var f = view != null ? view.forward : Vector3.forward;
                f.y = 0f;
                return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
            }
        }

        /// <summary>The bike the player is riding, or null on foot.</summary>
        public Motorbike RiddenBike
        {
            get
            {
                var player = director.Player;
                if (player == null || !player.IsSeated || player.CurrentSeat == null) return null;
                return player.CurrentSeat.GetComponentInParent<Motorbike>();
            }
        }

        public bool IsRiding => RiddenBike != null;

        public static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        public float Distance(Vector3 target) => Flat(PlayerPosition, target);

        public bool Near(Vector3 target, float radius) => Distance(target) <= radius;

        public void Toast(string text) => director.Toast(text);

        public void Pay(long amount)
        {
            if (amount > 0) State.Wallet.Earn(amount);
        }

        /// <summary>Walking height at p (the flat world's ground or what stands on it below knee height).</summary>
        public float Ground(Vector3 p)
        {
            float level = director.GroundLevel;
            return Physics.Raycast(new Vector3(p.x, level + 0.5f, p.z), Vector3.down, out var hit, 1.5f, PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore)
                ? hit.point.y
                : level;
        }

        public Vector3 OnGround(Vector3 p)
        {
            p.y = Ground(p);
            return p;
        }

        /// <summary>A random point on a walking lane between min and max metres from <paramref name="around"/>; null when none.</summary>
        public Vector3? LanePoint(Vector3 around, float min, float max)
        {
            var lanes = Lanes;
            if (lanes == null || lanes.LaneCount == 0) return null;
            var at = new Vector2(around.x, around.z);
            lanes.Near(at, max, near);
            if (near.Count == 0) return null;
            for (int attempt = 0; attempt < 48; attempt++)
            {
                int lane = near[rng.Range(0, near.Count)];
                if (lanes.Lane(lane).width < 0) continue;
                var p = lanes.PointAt(lane, rng.Range(0f, lanes.Length(lane)), out _);
                float d = Vector2.Distance(p, at);
                if (d < min || d > max) continue;
                return OnGround(new Vector3(p.x, 0f, p.y));
            }
            return null;
        }

        /// <summary>A random house gate between min and max metres away; null when none.</summary>
        public Spot Door(Vector3 around, float min, float max)
        {
            var doors = new List<Spot>();
            foreach (var s in Data.spots)
            {
                if (s.kind != SpotKind.Door) continue;
                float d = Flat(s.position, around);
                if (d >= min && d <= max) doors.Add(s);
            }
            return doors.Count > 0 ? doors[rng.Range(0, doors.Count)] : null;
        }

        /// <summary>The nearest open shop the filter accepts within <paramref name="max"/> metres; null when none.</summary>
        public ShopSite NearestShop(Vector3 around, Func<ShopSite, bool> accept, float max = float.PositiveInfinity)
        {
            ShopSite best = null;
            float bestDistance = max;
            foreach (var site in Data.shops)
            {
                if (!director.IsShopOpen(site) || (accept != null && !accept(site))) continue;
                float d = Flat(site.counter, around);
                if (d > bestDistance) continue;
                bestDistance = d;
                best = site;
            }
            return best;
        }

        public List<Spot> Landmarks(SpotKind kind)
        {
            var list = new List<Spot>();
            foreach (var s in Data.spots)
                if (s.kind == kind) list.Add(s);
            return list;
        }

        public Spot NearestLandmark(SpotKind kind, Vector3 around)
        {
            Spot best = null;
            float bestDistance = float.PositiveInfinity;
            foreach (var s in Data.spots)
            {
                if (s.kind != kind) continue;
                float d = Flat(s.position, around);
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = s;
            }
            return best;
        }

        /// <summary>Where someone waits at a gate: a step out toward the road, facing it.</summary>
        public Vector3 InFrontOf(Spot door, float step = 0.9f)
        {
            var face = Quaternion.Euler(0f, door.yaw, 0f) * Vector3.forward;
            return OnGround(door.position + face * step);
        }

        /// <summary>A standing person with a capsule; with a prompt and action they are an E target.</summary>
        public GameObject Person(string name, Vector3 at, Vector3 facing, PersonLook look, Func<string> prompt = null, Action action = null,
            Func<bool> can = null, bool ghost = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            facing.y = 0f;
            go.transform.SetPositionAndRotation(at, facing.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(facing) : Quaternion.identity);
            PersonFigure.Build(go.transform, "Figure", look, Palette, ghost);
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.height = 1.8f;
            capsule.radius = 0.28f;
            if (action != null)
            {
                var interactable = go.AddComponent<ActionInteractable>();
                interactable.Configure(prompt, action, can);
                interactable.SetInteractionId("mission:" + name);
            }
            return go;
        }

        /// <summary>Puts an E target on a prop: a box collider of the given size around its middle.</summary>
        public ActionInteractable MakeTarget(GameObject prop, Vector3 center, Vector3 size, Func<string> prompt, Action action, Func<bool> can = null)
        {
            var box = prop.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
            var interactable = prop.AddComponent<ActionInteractable>();
            interactable.Configure(prompt, action, can);
            interactable.SetInteractionId("mission:" + prop.name);
            return interactable;
        }

        public Vector3 FacePlayer(Vector3 from)
        {
            var d = PlayerPosition - from;
            d.y = 0f;
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
        }
    }
}
