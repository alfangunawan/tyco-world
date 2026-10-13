using TycoWorld.WorldGen;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.Player.Authoring
{
    /// <summary>Turns a KosSitePlan into the front-yard hierarchy in the active scene (spec #2 §9).</summary>
    public static class KosFrontyardBuilder
    {
        public static GameObject Build(KosSitePlan plan, GameObject gate, GameObject door, GameObject motor, Material yard, Material wall, UnityEngine.Mesh unitBox)
        {
            float slab = PlaceholderPropFactory.YardThickness;
            var outward = Xz.At(plan.Outward, 0f);
            var along = Xz.At(plan.Along, 0f);
            var root = new GameObject("KosFrontyard");
            root.transform.SetPositionAndRotation(Xz.At(plan.Door, 0f), Quaternion.Euler(0f, plan.SpawnYaw, 0f));

            Vector3 a = Xz.At(plan.WallA, 0f), b = Xz.At(plan.WallB, 0f);
            Vector3 aOut = a + outward * plan.YardDepth, bOut = b + outward * plan.YardDepth;

            var yardGo = PlaceholderPropFactory.CreateBlock("Yard", root.transform, (a + b) * 0.5f, (aOut + bOut) * 0.5f, slab, Vector3.Distance(a, b), yard, unitBox);
            yardGo.AddComponent<GroundSurface>().Initialize(SurfaceType.Concrete);

            float h = PlaceholderPropFactory.WallHeight, t = PlaceholderPropFactory.WallThickness;
            var gateCenter = Xz.At(plan.Gate, 0f);
            float openingHalf = PlaceholderPropFactory.GateWidth * 0.5f + 0.12f;
            bool aIsLeft = Vector3.Dot(a - gateCenter, along) < 0f;
            Vector3 leftOut = aIsLeft ? aOut : bOut, rightOut = aIsLeft ? bOut : aOut;
            Vector3 leftIn = aIsLeft ? a : b, rightIn = aIsLeft ? b : a;
            Vector3 leftDir = (leftOut - gateCenter).normalized;
            Vector3 rightDir = (rightOut - gateCenter).normalized;
            PlaceholderPropFactory.CreateBlock("Wall_Left", root.transform, leftIn, leftOut, h, t, wall, unitBox);
            PlaceholderPropFactory.CreateBlock("Wall_Right", root.transform, rightIn, rightOut, h, t, wall, unitBox);
            PlaceholderPropFactory.CreateBlock("Wall_FrontLeft", root.transform, leftOut, gateCenter + leftDir * openingHalf, h, t, wall, unitBox);
            PlaceholderPropFactory.CreateBlock("Wall_FrontRight", root.transform, gateCenter + rightDir * openingHalf, rightOut, h, t, wall, unitBox);

            Place(gate, root.transform, gateCenter, plan.SpawnYaw);
            Place(door, root.transform, Xz.At(plan.Door, slab), plan.SpawnYaw);
            Place(motor, root.transform, Xz.At(plan.Motor, slab), plan.MotorYaw);

            var spawn = new GameObject("PlayerSpawn_Kos");
            spawn.transform.SetParent(root.transform, true);
            spawn.transform.SetPositionAndRotation(Xz.At(plan.Spawn, slab), Quaternion.Euler(0f, plan.SpawnYaw, 0f));
            spawn.AddComponent<PlayerSpawnPoint>().Configure("kos", "Kos Raflesia 3");
            return root;
        }

        static void Place(GameObject source, Transform parent, Vector3 position, float yaw)
        {
            if (source == null) return;
            var go = PrefabUtility.IsPartOfPrefabAsset(source) ? (GameObject)PrefabUtility.InstantiatePrefab(source) : Object.Instantiate(source);
            go.name = source.name;
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        }
    }
}
