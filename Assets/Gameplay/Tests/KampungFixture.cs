using System;
using System.Collections.Generic;
using TycoWorld.Player;
using TycoWorld.Vehicles.Tests;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TycoWorld.Gameplay.Tests
{
    /// <summary>
    /// A ready KampungDirector over the vehicle tests' physics scratch area: a floor, a player, an in-memory save and
    /// a hand-made KampungData; no day cycle (the director's own clock), no keyboard, no default systems.
    /// </summary>
    public sealed class KampungFixture : IDisposable
    {
        public readonly VehicleFixture Fx = new VehicleFixture();
        public readonly MemorySaveBackend Save = new MemorySaveBackend();
        public readonly FirstPersonPlayer Player;
        public readonly KampungData Data;
        public readonly KampungDirector Director;

        public static Vector3 Origin => VehicleFixture.Origin;

        public KampungFixture(Action<KampungData> fill = null, bool begin = true)
        {
            Fx.Floor();
            Player = Fx.CreatePlayer(Vector3.zero);
            Data = Fx.Track(ScriptableObject.CreateInstance<KampungData>());
            Data.spots = new[] { new Spot { kind = SpotKind.Kos, position = Origin + new Vector3(0f, 0f, -20f) } };
            fill?.Invoke(Data);
            var go = Fx.Track(new GameObject("Gameplay"));
            go.SetActive(false);
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = Fx.Track(ScriptableObject.CreateInstance<PanelSettings>());
            Director = go.AddComponent<KampungDirector>();
            Director.Configure(Data, null, null);
            Director.SaveBackend = Save;
            Director.ReadKeyboard = false;
            Director.CreateDefaultSystems = false;
            Director.FindPlayer = () => Player;
            Director.FindDayCycle = () => null;
            Director.WorldReady = () => true;
            Director.GroundLevel = Origin.y;
            go.SetActive(true);
            Director.Initialize();
            if (begin) Director.Tick(1f / 60f);
            Fx.Sync();
        }

        public static ShopSite Site(string id, ShopType type, Vector3 local, float yaw = 0f, string name = "Warung Uji") => new ShopSite
        {
            id = id, type = type, name = name, counter = Origin + local, yaw = yaw, slabLength = 0.9f, buildingId = 1
        };

        public const float GridSpacing = 20f;
        public const int GridCells = 8;

        /// <summary>A 9x9 node street grid, 20 m apart, centred on the fixture origin (lanes in world x/z).</summary>
        public static void StreetGrid(KampungData d) => StreetGrid(d, GridCells, GridSpacing);

        /// <summary>A (cells+1)^2 node street grid, <paramref name="spacing"/> m apart, centred on the fixture origin.</summary>
        public static void StreetGrid(KampungData d, int cells, float spacing)
        {
            var nodes = new List<LaneNode>();
            var lanes = new List<Lane>();
            var nodeLanes = new List<List<int>>();
            float half = cells * spacing / 2f;
            int Id(int x, int z) => z * (cells + 1) + x;
            for (int z = 0; z <= cells; z++)
                for (int x = 0; x <= cells; x++)
                {
                    nodes.Add(new LaneNode { position = new Vector2(Origin.x - half + x * spacing, Origin.z - half + z * spacing) });
                    nodeLanes.Add(new List<int>());
                }
            void Link(int a, int b)
            {
                nodeLanes[a].Add(lanes.Count);
                nodeLanes[b].Add(lanes.Count);
                lanes.Add(new Lane { points = new[] { nodes[a].position, nodes[b].position }, startNode = a, endNode = b, width = 1 });
            }
            for (int z = 0; z <= cells; z++)
                for (int x = 0; x <= cells; x++)
                {
                    if (x < cells) Link(Id(x, z), Id(x + 1, z));
                    if (z < cells) Link(Id(x, z), Id(x, z + 1));
                }
            for (int i = 0; i < nodes.Count; i++) nodes[i].lanes = nodeLanes[i].ToArray();
            d.walkLanes = lanes.ToArray();
            d.walkNodes = nodes.ToArray();
        }

        public InteractionContext Context => new InteractionContext(Player, Player.Hud, Player.View.position);

        public void Run(float seconds, float dt = 1f / 60f)
        {
            for (float t = 0f; t < seconds; t += dt) Director.Tick(dt);
        }

        public void Dispose()
        {
            if (Director != null) Object.DestroyImmediate(Director.gameObject);
            Fx.Dispose();
        }
    }
}
