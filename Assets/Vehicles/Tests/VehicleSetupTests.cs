using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.Player;
using TycoWorld.Vehicles.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TycoWorld.Vehicles.Tests
{
    public class VehicleSetupTests
    {
        readonly HashSet<GameObject> rootsBefore = new HashSet<GameObject>();
        Scene scratch;
        GameObject prefab;

        /// <summary>The untitled scene the test runner opens stands in for World.unity; every root a test adds is destroyed in TearDown.</summary>
        [SetUp]
        public void SetUp()
        {
            scratch = SceneManager.GetActiveScene();
            Assert.IsTrue(string.IsNullOrEmpty(scratch.path), "Tes setup hanya boleh memakai scene untitled, bukan " + scratch.path);
            rootsBefore.Clear();
            rootsBefore.UnionWith(scratch.GetRootGameObjects());
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VehiclePaths.MotorbikePrefab);
            Assert.IsNotNull(prefab, "Prefab belum ada (Task 10)");
        }

        [TearDown]
        public void TearDown()
        {
            if (scratch.IsValid() && scratch.isLoaded)
                foreach (var go in scratch.GetRootGameObjects())
                    if (!rootsBefore.Contains(go)) Object.DestroyImmediate(go);
            rootsBefore.Clear();
        }

        /// <summary>A KosFrontyard stand-in rotated 180° with the placeholder motor as a child, like World.unity.</summary>
        static Transform Placeholder(Vector3 yardPosition, Vector3 local, float localYaw)
        {
            var yard = GameObject.Find("KosFrontyard");
            if (yard == null)
            {
                yard = new GameObject("KosFrontyard");
                yard.transform.SetPositionAndRotation(yardPosition, Quaternion.Euler(0f, 180f, 0f));
            }
            var motor = new GameObject("Motor_Placeholder");
            motor.transform.SetParent(yard.transform, false);
            motor.transform.localPosition = local;
            motor.transform.localRotation = Quaternion.Euler(0f, localYaw, 0f);
            motor.AddComponent<PlaceholderVehicleInteractable>();
            return motor.transform;
        }

        int Count<T>() where T : Component
        {
            int n = 0;
            foreach (var go in scratch.GetRootGameObjects()) n += go.GetComponentsInChildren<T>(true).Length;
            return n;
        }

        static void AssertPose(Vector3 position, float yaw, Transform actual)
        {
            Assert.Less(Vector3.Distance(position, actual.position), 1e-3f, "posisi");
            Assert.AreEqual(0f, Mathf.DeltaAngle(yaw, actual.eulerAngles.y), 1e-2f, "yaw");
            Assert.AreEqual(0f, Mathf.DeltaAngle(0f, actual.eulerAngles.x), 1e-2f, "tegak");
            Assert.AreEqual(0f, Mathf.DeltaAngle(0f, actual.eulerAngles.z), 1e-2f, "tegak");
        }

        [Test]
        public void Wire_ReplacesThePlaceholder_AtItsWorldPose()
        {
            var placeholder = Placeholder(new Vector3(10f, 0f, 20f), new Vector3(4.7f, 0.12f, 2.7f), -90f);
            var pose = placeholder.position;
            float yaw = placeholder.eulerAngles.y;

            var bike = VehicleSceneSetup.Wire(scratch, prefab, out string error);

            Assert.IsNull(error);
            Assert.IsNotNull(bike);
            Assert.AreEqual(MotorbikeBuilder.RootName, bike.name);
            Assert.IsNull(bike.transform.parent, "akar scene");
            Assert.AreEqual(scratch, bike.scene);
            Assert.AreSame(prefab, PrefabUtility.GetCorrespondingObjectFromSource(bike));
            AssertPose(pose, yaw, bike.transform);
            Assert.AreEqual(0, Count<PlaceholderVehicleInteractable>());
            Assert.IsNotNull(GameObject.Find("KosFrontyard"), "halaman kos tetap ada");
        }

        [Test]
        public void Wire_Rerun_DoesNotDuplicate()
        {
            Placeholder(Vector3.zero, new Vector3(1f, 0.12f, 2f), 0f);
            var first = VehicleSceneSetup.Wire(scratch, prefab, out _);
            var again = VehicleSceneSetup.Wire(scratch, prefab, out string error);
            Assert.IsNull(error);
            Assert.AreSame(first, again);
            Assert.AreEqual(1, Count<Motorbike>());
        }

        [Test]
        public void Wire_WithoutAPlaceholder_KeepsAMovedBike()
        {
            Placeholder(Vector3.zero, new Vector3(1f, 0.12f, 2f), 0f);
            var bike = VehicleSceneSetup.Wire(scratch, prefab, out _);
            bike.transform.SetPositionAndRotation(new Vector3(30f, 0f, -5f), Quaternion.Euler(0f, 45f, 0f));
            VehicleSceneSetup.Wire(scratch, prefab, out _);
            AssertPose(new Vector3(30f, 0f, -5f), 45f, bike.transform);
        }

        [Test]
        public void Wire_AfterTheYardWasRebuilt_MovesTheBike_AndRemovesTheNewPlaceholder()
        {
            Placeholder(Vector3.zero, new Vector3(1f, 0.12f, 2f), 0f);
            var bike = VehicleSceneSetup.Wire(scratch, prefab, out _);
            var rebuilt = Placeholder(Vector3.zero, new Vector3(-3f, 0.12f, 4f), 90f);
            var pose = rebuilt.position;
            float yaw = rebuilt.eulerAngles.y;
            var again = VehicleSceneSetup.Wire(scratch, prefab, out _);
            Assert.AreSame(bike, again);
            AssertPose(pose, yaw, bike.transform);
            Assert.AreEqual(0, Count<PlaceholderVehicleInteractable>());
        }

        [Test]
        public void Wire_WithNothingToReplace_Fails()
        {
            var bike = VehicleSceneSetup.Wire(scratch, prefab, out string error);
            Assert.IsNull(bike);
            Assert.AreEqual(VehicleSceneSetup.MissingPlaceholderError, error);
            Assert.AreEqual(0, Count<Motorbike>());
        }
    }
}
