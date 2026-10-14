using NUnit.Framework;
using TycoWorld.DayCycle.Authoring;

namespace TycoWorld.DayCycle.Tests
{
    public class DayCycleSmokeTests
    {
        [Test]
        public void DayTimes_MatchSpec()
        {
            Assert.AreEqual(6.5f, DayTimes.StartHour);
            Assert.AreEqual(5f, DayTimes.DawnStart);
            Assert.AreEqual(6.5f, DayTimes.DayStart);
            Assert.AreEqual(17.5f, DayTimes.DuskStart);
            Assert.AreEqual(19f, DayTimes.NightStart);
            Assert.AreEqual(19f, DayTimes.HorrorStart);
            Assert.AreEqual(5f, DayTimes.HorrorEnd);
            Assert.AreEqual(6.5f, DayTimes.DebugMorning);
            Assert.AreEqual(19.5f, DayTimes.DebugNight);
            Assert.AreEqual(60f, DayTimes.RealSecondsPerGameHour);
        }

        [Test]
        public void Paths_AreUnderDayCycleRoot()
        {
            Assert.AreEqual("Assets/DayCycle", DayCyclePaths.Root);
            StringAssert.StartsWith(DayCyclePaths.Root + "/", DayCyclePaths.ProfileAsset);
            StringAssert.StartsWith(DayCyclePaths.Root + "/", DayCyclePaths.NightAmbienceClip);
        }

        [Test]
        public void Urp_And_InputSystem_AreReferenced()
        {
            Assert.IsNotNull(typeof(UnityEngine.Rendering.Volume));
            Assert.IsNotNull(typeof(UnityEngine.InputSystem.Keyboard));
        }
    }
}
