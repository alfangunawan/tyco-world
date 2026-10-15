using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    public class RideCameraTests
    {
        const float Dt = 0.02f;
        BikeTuning tuning;
        VehicleFixture fx;
        RideCamera cam;

        static Vector3 O => VehicleFixture.Origin;

        [SetUp]
        public void SetUp()
        {
            tuning = ScriptableObject.CreateInstance<BikeTuning>();
            fx = new VehicleFixture();
            cam = new RideCamera(tuning);
            cam.Reset(0f);
        }

        [TearDown]
        public void TearDown()
        {
            fx.Dispose();
            Object.DestroyImmediate(tuning);
        }

        static void AssertNear(Vector3 expected, Vector3 actual, float tolerance = 1e-3f) =>
            Assert.Less(Vector3.Distance(expected, actual), tolerance, actual.ToString("F3"));

        void Idle(float seconds, float speed)
        {
            for (int i = 0; i < Mathf.RoundToInt(seconds / Dt); i++) cam.Update(Vector2.zero, 0f, speed, Dt);
        }

        float Elevation()
        {
            cam.PoseThirdPerson(O, out var p, out _);
            var pivot = RideCameraMath.ChasePivot(O, tuning);
            return Mathf.Asin((p.y - pivot.y) / Vector3.Distance(p, pivot)) * Mathf.Rad2Deg;
        }

        [Test]
        public void DefaultChase_IsBehindAndAbove_LookingAtThePivot()
        {
            cam.PoseThirdPerson(O, out var p, out var r);
            AssertNear(O + new Vector3(0f, 1.9f, -4.5f), p);
            var pivot = O + Vector3.up * 1.2f;
            Assert.Less(Vector3.Angle(r * Vector3.forward, pivot - p), 0.01f);
        }

        [Test]
        public void Chase_FollowsTheBikeYawSmoothly()
        {
            cam.Update(Vector2.zero, 90f, 10f, Dt);
            Assert.That(cam.FollowYaw, Is.InRange(0.1f, 89.9f));
            for (int i = 0; i < 300; i++) cam.Update(Vector2.zero, 90f, 10f, Dt);
            Assert.AreEqual(90f, cam.FollowYaw, 0.01f);
        }

        [Test]
        public void Orbit_IsClamped()
        {
            cam.Update(new Vector2(500f, 0f), 0f, 0f, Dt);
            Assert.AreEqual(120f, cam.OrbitYaw, 1e-3f);
            cam.Update(new Vector2(0f, -500f), 0f, 0f, Dt);
            Assert.AreEqual(35f, Elevation(), 0.01f);
            cam.Update(new Vector2(0f, 1000f), 0f, 0f, Dt);
            Assert.AreEqual(-10f, Elevation(), 0.01f);
        }

        [Test]
        public void Orbit_ReturnsBehind_AfterIdlingWhileMoving()
        {
            cam.Update(new Vector2(60f, 0f), 0f, 5f, Dt);
            Idle(1f, 5f);
            Assert.AreEqual(60f, cam.OrbitYaw, 1e-3f, "belum 1,5 s");
            Idle(2f, 5f);
            Assert.Less(Mathf.Abs(cam.OrbitYaw), 5f);
        }

        [Test]
        public void Orbit_Stays_WhenTheBikeIsSlow()
        {
            cam.Update(new Vector2(60f, 0f), 0f, 1f, Dt);
            Idle(3f, 1f);
            Assert.AreEqual(60f, cam.OrbitYaw, 1e-3f);
        }

        [Test]
        public void Toggle_SwitchesModeAndResetsOffsets()
        {
            cam.Update(new Vector2(60f, 10f), 0f, 0f, Dt);
            Assert.AreEqual(RideCameraMode.ThirdPerson, cam.Mode);
            cam.Toggle();
            Assert.AreEqual(RideCameraMode.FirstPerson, cam.Mode);
            Assert.AreEqual(0f, cam.OrbitYaw);
            cam.Update(new Vector2(20f, 5f), 0f, 0f, Dt);
            Assert.AreEqual(20f, cam.LookYaw, 1e-3f);
            cam.Toggle();
            Assert.AreEqual(RideCameraMode.ThirdPerson, cam.Mode);
            Assert.AreEqual(0f, cam.LookYaw);
        }

        [Test]
        public void FirstPerson_HeadSitsAboveTheSeat_LookIsClamped()
        {
            cam.Toggle();
            cam.Update(new Vector2(500f, 500f), 0f, 0f, Dt);
            Assert.AreEqual(100f, cam.LookYaw, 1e-3f);
            Assert.AreEqual(60f, cam.LookPitch, 1e-3f);
            var seat = O + new Vector3(0f, 0.85f, 0f);
            cam.PoseFirstPerson(seat, 0f, 0f, out var p, out var r);
            AssertNear(seat + new Vector3(0f, 0.75f, -0.1f), p);
            var forward = r * Vector3.forward;
            Assert.Greater(forward.y, 0.8f, "menengadah 60°");
            Assert.Greater(forward.x, 0f, "menoleh ke kanan");
        }

        [Test]
        public void FirstPerson_RollsWithHalfTheLean()
        {
            cam.Toggle();
            cam.PoseFirstPerson(O, 0f, 20f, out _, out var r);
            Assert.AreEqual(-10f, Vector3.SignedAngle(Vector3.up, r * Vector3.up, Vector3.forward), 0.01f);
        }

        [Test]
        public void PullIn_StopsInFrontOfAWall()
        {
            fx.Box("Wall", new Vector3(0f, 1.5f, -2.5f), new Vector3(4f, 3f, 0.2f));
            fx.Sync();
            cam.PoseThirdPerson(O, out var p, out _);
            Assert.Greater(p.z, O.z - 2.4f, "di depan permukaan tembok");
            Assert.Less(p.z, O.z);
        }

        [Test]
        public void PullIn_WithoutObstacles_KeepsTheDesiredPoint()
        {
            var pivot = O + Vector3.up;
            var desired = pivot + new Vector3(0f, 0f, -5f);
            AssertNear(desired, RideCamera.PullIn(pivot, desired, 0.25f));
        }

        [Test]
        public void Car_StaysInThirdPerson()
        {
            // The PSX car models have no interior (spec vehicles-3a §5).
            tuning.isCar = true;
            cam.Toggle();
            Assert.AreEqual(RideCameraMode.ThirdPerson, cam.Mode);
        }
    }
}
