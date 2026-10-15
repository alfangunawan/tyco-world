using System.Linq;
using NUnit.Framework;
using TycoWorld.Vehicles.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    /// <summary>GLB + spec → standard vehicle (spec vehicles-3a §4).</summary>
    public class VehicleModelBuilderTests
    {
        GameObject root;
        BikeTuning tuning;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("VehicleUnderTest");
            tuning = ScriptableObject.CreateInstance<BikeTuning>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(tuning);
        }

        Bounds BuildModel(string id)
        {
            var spec = VehicleModelCatalog.Get(id);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(VehicleModelFiles.Path(id));
            return VehicleModelBuilder.Build(root, spec, source, tuning, VehicleMaterials.LoadOrCreate(), new VehicleClipSet());
        }

        /// <summary>
        /// Tight bounds of the drawn model from its vertices (Renderer.bounds is a loose box around a node turned at an odd angle,
        /// like the Vario's handlebar).
        /// </summary>
        static Bounds RendererBounds(GameObject go)
        {
            var lean = go.transform.Find(VehicleModelBuilder.LeanName);
            var filters = lean.GetComponentsInChildren<MeshFilter>()
                .Where(f => f.name != VehicleModelBuilder.TailName && f.name != VehicleModelBuilder.LensName).ToArray();
            var points = filters.SelectMany(f => f.sharedMesh.vertices.Select(v => f.transform.TransformPoint(v))).ToList();
            var b = new Bounds(points[0], Vector3.zero);
            foreach (var p in points) b.Encapsulate(p);
            return b;
        }

        [Test]
        public void EveryModel_IsScaledGroundedAndFacingForward()
        {
            foreach (var spec in VehicleModelCatalog.All)
            {
                foreach (Transform child in root.transform.Cast<Transform>().ToList()) Object.DestroyImmediate(child.gameObject);
                var b = BuildModel(spec.Id);
                var drawn = RendererBounds(root);
                Assert.AreEqual(spec.Length, drawn.size.z, spec.Length * 0.02f, spec.Id + " length");
                Assert.AreEqual(0f, drawn.min.y, 0.02f, spec.Id + " stands on the ground");
                Assert.AreEqual(0f, drawn.center.x, 0.05f, spec.Id + " centred");
                Assert.Less(drawn.size.y, drawn.size.z, spec.Id + " upright: lower than long (the GLB root keeps its Y-up turn)");
                Assert.Less(drawn.size.x, drawn.size.z, spec.Id + " narrower than long");
                foreach (var f in root.GetComponentsInChildren<MeshFilter>(true))
                    for (int i = 0; i < f.sharedMesh.subMeshCount; i++)
                        Assert.AreEqual(MeshTopology.Triangles, f.sharedMesh.GetTopology(i), spec.Id + " " + f.name + " (line meshes are dropped)");
                // Under the lean (and the bike's steer), so the lamps lean and turn with the body.
                var lights = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == VehicleModelBuilder.LightsName);
                Assert.Greater(root.GetComponent<MotorbikeVisuals>().Headlight.transform.position.z, 0f, spec.Id + " headlight at the front");
                Assert.IsNotNull(lights, spec.Id);
                Assert.AreEqual(drawn.size.z, b.size.z, 0.01f);
            }
        }

        /// <summary>Centre of each lamp box (a car's lamp mesh holds a left and a right box, a bike's one).</summary>
        static Vector3[] BoxCentres(Renderer lamp)
        {
            var toWorld = lamp.transform.localToWorldMatrix;
            var points = lamp.GetComponent<MeshFilter>().sharedMesh.vertices.Select(v => toWorld.MultiplyPoint3x4(v)).ToList();
            float mid = lamp.bounds.center.x;
            var groups = lamp.bounds.size.x > 0.5f ? new[] { points.Where(p => p.x < mid), points.Where(p => p.x >= mid) } : new[] { points.AsEnumerable() };
            return groups.Select(g =>
            {
                var list = g.ToList();
                var b = new Bounds(list[0], Vector3.zero);
                foreach (var p in list) b.Encapsulate(p);
                return b.center;
            }).ToArray();
        }

        [Test]
        public void LampQuads_SitOnTheBody_NotInFrontOfIt()
        {
            foreach (var spec in VehicleModelCatalog.All)
            {
                foreach (Transform child in root.transform.Cast<Transform>().ToList()) Object.DestroyImmediate(child.gameObject);
                BuildModel(spec.Id);
                var model = root.GetComponentsInChildren<Transform>(true).First(t => t.name == VehicleModelBuilder.ModelName);
                // Only the model's own colliders are tested, never the world around the origin.
                var colliders = model.GetComponentsInChildren<MeshFilter>(true).Select(f => f.gameObject.AddComponent<MeshCollider>()).ToList();
                var visuals = root.GetComponent<MotorbikeVisuals>();
                foreach (var (lamp, inward) in new[] { (visuals.HeadlightLens, Vector3.back), (visuals.TailLight, Vector3.forward) })
                {
                    foreach (var centre in BoxCentres(lamp))
                    {
                        bool onBody = colliders.Any(c => c.Raycast(new Ray(centre, inward), out _, 0.08f));
                        Assert.IsTrue(onBody, spec.Id + " " + lamp.name + " at " + centre.ToString("F2") + ": the body is within 8 cm behind it");
                    }
                }
            }
        }

        [Test]
        public void Car_GetsAHullOverItsBounds_AndAllWheelsSpin()
        {
            var b = BuildModel("sedan");
            var hull = root.transform.Find(VehicleModelBuilder.HullName).GetComponent<BoxCollider>();
            Assert.IsNotNull(hull);
            Assert.AreEqual(b.size.x, hull.size.x, 0.02f);
            Assert.AreEqual(b.size.z, hull.size.z, 0.02f);
            var visuals = root.GetComponent<MotorbikeVisuals>();
            var spec = VehicleModelCatalog.Get("sedan");
            if (!spec.WheelsMerged) Assert.AreEqual(spec.FrontWheels.Length + spec.RearWheels.Length, visuals.Wheels.Count);
            Assert.Greater(visuals.WheelRadius, 0.15f);
            Assert.Less(visuals.WheelRadius, 0.6f);
            Assert.AreEqual(spec.FrontWheels.Length, root.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Steer_")));
        }

        [Test]
        public void Supra_ForkTurnsWithTheHandlebar()
        {
            BuildModel("supra");
            var steer = root.GetComponentsInChildren<Transform>(true).First(t => t.name == VehicleModelBuilder.SteerName);
            foreach (var n in new[] { "handlebars", "forks_front", "wheel_front" })
                Assert.IsTrue(root.GetComponentsInChildren<Transform>(true).First(t => t.name == n).IsChildOf(steer), n);
            Assert.IsFalse(root.GetComponentsInChildren<Transform>(true).First(t => t.name == "wheel_rear").IsChildOf(steer));
        }

        [Test]
        public void Angkot_AxlesSpin_ButTheFrontDoesNotSteer()
        {
            BuildModel("angkot");
            var visuals = root.GetComponent<MotorbikeVisuals>();
            Assert.AreEqual(2, visuals.Wheels.Count);
            Assert.IsFalse(root.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("Steer")));
        }

        [Test]
        public void Build_TwiceOnTheSameRoot_LeavesOneModel()
        {
            BuildModel("sedan");
            BuildModel("sedan");
            Assert.AreEqual(1, root.GetComponentsInChildren<Transform>(true).Count(t => t.name == VehicleModelBuilder.ModelName));
            Assert.AreEqual(1, root.GetComponents<Motorbike>().Length);
            Assert.AreEqual(1, root.transform.Cast<Transform>().Count(t => t.name == VehicleModelBuilder.HullName));
        }

        [Test]
        public void Police_KeepsOnlyTheSedan()
        {
            var b = BuildModel("police");
            Assert.AreEqual(4.45f, b.size.z, 0.09f);
            Assert.Less(b.size.x, 2.2f, "one car wide, not two side by side");
            Assert.Greater(b.size.x, 1.4f, "the whole sedan, not a sliver");
        }

        // Over the 3-material budget of spec §8 (ledger, Task 1 ruling): flat-colour materials of the source files. Their draw
        // cost is judged by the perf check of Task 9; triangles are still held to the budget.
        static readonly string[] MaterialExempt = { "angkot", "elf", "l300", "lmpv2", "supra" };

        static int Triangles(Mesh m)
        {
            int n = 0;
            for (int i = 0; i < m.subMeshCount; i++)
                if (m.GetTopology(i) == MeshTopology.Triangles) n += (int)m.GetIndexCount(i) / 3;
            return n;
        }

        [Test]
        public void EveryModel_StaysInBudget()
        {
            foreach (var spec in VehicleModelCatalog.All)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(VehicleModelFiles.Path(spec.Id));
                int tris = source.GetComponentsInChildren<MeshFilter>(true).Sum(f => Triangles(f.sharedMesh));
                var materials = source.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Distinct().Count();
                Assert.LessOrEqual(tris, 13000, spec.Id);
                if (System.Array.IndexOf(MaterialExempt, spec.Id) < 0) Assert.LessOrEqual(materials, 3, spec.Id);
            }
        }
    }
}
