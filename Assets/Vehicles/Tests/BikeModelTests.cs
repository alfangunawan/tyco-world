using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    public class BikeModelTests
    {
        const float Dt = 0.02f;
        BikeTuning tuning;
        BikeModel model;

        static VehicleIntent Gas => new VehicleIntent { Throttle = 1f };
        static VehicleIntent Back => new VehicleIntent { Throttle = -1f };

        [SetUp]
        public void SetUp()
        {
            tuning = ScriptableObject.CreateInstance<BikeTuning>();
            model = new BikeModel(tuning);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(tuning);

        void Run(BikeModel m, VehicleIntent intent, float seconds)
        {
            int frames = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < frames; i++) m.Step(intent, Dt);
        }

        float TimeToStop(BikeModel m, VehicleIntent intent)
        {
            float time = 0f;
            while (m.Speed > tuning.reverseThreshold && time < 10f)
            {
                m.Step(intent, Dt);
                time += Dt;
            }
            return time;
        }

        [Test]
        public void Defaults_MatchTheSpec()
        {
            Assert.AreEqual(16.67f, tuning.maxForwardSpeed, 1e-3f);
            Assert.AreEqual(2.5f, tuning.maxReverseSpeed, 1e-3f);
            Assert.AreEqual(4f, tuning.accelAtRest);
            Assert.AreEqual(1.5f, tuning.accelAtMax);
            Assert.AreEqual(7f, tuning.brakeDecel);
            Assert.AreEqual(10f, tuning.hardBrakeDecel);
            Assert.AreEqual(0.8f, tuning.coastDecel, 1e-5f);
            Assert.AreEqual(90f, tuning.yawRateLow);
            Assert.AreEqual(35f, tuning.yawRateHigh);
            Assert.AreEqual(25f, tuning.maxLean);
            Assert.AreEqual(1.4f, tuning.dismountMaxSpeed, 1e-5f);
            Assert.AreEqual(4.5f, tuning.chaseDistance, 1e-5f);
            Assert.AreEqual(30f, tuning.headlightRange);
            Assert.AreEqual(60f, tuning.headlightAngle);
            Assert.AreEqual(60f, tuning.headlightIntensity, "dinaikkan dari 4 setelah cek Play malam");
            Assert.AreEqual(0.8f, tuning.idlePitch, 1e-5f);
            Assert.AreEqual(2f, tuning.maxPitch);
        }

        [Test]
        public void TopSpeed_IsCappedAt60Kmh()
        {
            Run(model, Gas, 20f);
            Assert.AreEqual(60f, model.SpeedKmh, 0.1f);
        }

        [Test]
        public void ZeroTo40_TakesAbout4Seconds()
        {
            float time = 0f;
            while (model.SpeedKmh < 40f && time < 10f)
            {
                model.Step(Gas, Dt);
                time += Dt;
            }
            Assert.That(time, Is.InRange(3.5f, 4.5f));
        }

        [Test]
        public void S_From60_BrakesToAStopWithin2Point5Seconds()
        {
            Run(model, Gas, 20f);
            model.Step(Back, Dt);
            Assert.IsTrue(model.Braking);
            Assert.Less(TimeToStop(model, Back), 2.5f);
        }

        [Test]
        public void Space_BrakesHarderThanS()
        {
            var other = new BikeModel(tuning);
            Run(model, Gas, 20f);
            Run(other, Gas, 20f);
            float withS = TimeToStop(model, Back);
            float withSpace = TimeToStop(other, new VehicleIntent { Brake = true });
            Assert.Less(withSpace, withS);
        }

        [Test]
        public void Coasting_SlowsDownGently()
        {
            Run(model, Gas, 20f);
            float before = model.Speed;
            Run(model, default, 1f);
            Assert.AreEqual(before - 0.8f, model.Speed, 0.02f);
            Assert.IsFalse(model.Braking);
        }

        [Test]
        public void S_AtStandstill_ReversesUpTo9Kmh()
        {
            Run(model, Back, 5f);
            Assert.AreEqual(-9f, model.SpeedKmh, 0.1f);
            Assert.IsFalse(model.Braking);
        }

        [Test]
        public void Gas_WhileReversing_BrakesFirst()
        {
            Run(model, Back, 3f);
            model.Step(Gas, Dt);
            Assert.IsTrue(model.Braking);
            Assert.Less(model.Speed, 0f);
        }

        [Test]
        public void YawRate_IsZeroWhenStopped() => Assert.AreEqual(0f, model.YawRate(1f));

        [Test]
        public void YawRate_FollowsSpeed()
        {
            Run(model, Gas, 1f);
            float v = model.Speed;
            Assert.Greater(v, tuning.turnInSpeed);
            Assert.AreEqual(Mathf.Lerp(90f, 35f, v / tuning.maxForwardSpeed), model.YawRate(1f), 0.01f);
            Run(model, Gas, 20f);
            Assert.AreEqual(35f, model.YawRate(1f), 0.01f);
            Assert.AreEqual(-35f, model.YawRate(-1f), 0.01f);
        }

        [Test]
        public void YawRate_ScalesInBelowTurnInSpeed()
        {
            Run(model, Gas, 0.2f);
            float v = model.Speed;
            Assert.Less(v, tuning.turnInSpeed);
            float expected = Mathf.Lerp(90f, 35f, v / tuning.maxForwardSpeed) * (v / tuning.turnInSpeed);
            Assert.AreEqual(expected, model.YawRate(1f), 0.01f);
        }

        [Test]
        public void YawRate_FlipsWhenReversing()
        {
            Run(model, Back, 3f);
            Assert.Less(model.YawRate(1f), 0f);
        }

        [Test]
        public void Lean_FollowsSteerAtSpeed_ButNotAtStandstill()
        {
            Run(model, new VehicleIntent { Throttle = 1f, Steer = 1f }, 4f);
            Assert.AreEqual(25f, model.Lean, 0.5f);
            model.Reset();
            Run(model, new VehicleIntent { Steer = 1f }, 1f);
            Assert.AreEqual(0f, model.Lean, 1e-3f);
        }

        [Test]
        public void StopAndScale_AdjustSpeed()
        {
            Run(model, Gas, 3f);
            float v = model.Speed;
            model.Scale(0.5f);
            Assert.AreEqual(v * 0.5f, model.Speed, 1e-4f);
            model.Stop();
            Assert.AreEqual(0f, model.Speed);
        }

        [Test]
        public void ZeroDt_ChangesNothing()
        {
            Run(model, Gas, 1f);
            float v = model.Speed;
            model.Step(Gas, 0f);
            Assert.AreEqual(v, model.Speed);
        }
    }
}
