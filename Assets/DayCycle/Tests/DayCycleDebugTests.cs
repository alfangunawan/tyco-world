using NUnit.Framework;
using TycoWorld.Player;

namespace TycoWorld.DayCycle.Tests
{
    public class DayCycleDebugTests
    {
        static readonly DebugKeys T = new DebugKeys { JumpTime = true };

        DirectorFixture fx;
        FirstPersonPlayer player;
        DayCycleDebug debug;

        [SetUp]
        public void SetUp()
        {
            fx = new DirectorFixture();
            player = fx.AddPlayer();
            debug = fx.Root.AddComponent<DayCycleDebug>();
        }

        [TearDown]
        public void TearDown() => fx.Dispose();

        [Test]
        public void Allowed_InEditor() => Assert.IsTrue(DayCycleDebug.Allowed);

        [Test]
        public void T_JumpsToNight_ThenHorrorStartsOnNextTick()
        {
            fx.Director.Initialize(12f);
            Assert.IsTrue(debug.ApplyKeys(T));
            Assert.AreEqual(19.5f, fx.Director.Clock.Hours, 1e-4f);
            Assert.AreEqual("Lompat ke 19:30", player.Hud.Message);
            fx.Director.Tick(0.016f);
            Assert.IsTrue(fx.Director.IsHorror);
            Assert.AreEqual(1, fx.Started);
            Assert.AreEqual(DayCycleDirector.HorrorStartMessage, player.Hud.Message);
            debug.ApplyKeys(T);
            fx.Director.Tick(0.016f);
            Assert.AreEqual(6.5f, fx.Director.Clock.Hours, 1e-3f);
            Assert.AreEqual(1, fx.Ended);
        }

        [Test]
        public void Brackets_ChangeSpeed()
        {
            fx.Director.Initialize(12f);
            debug.ApplyKeys(new DebugKeys { FasterSpeed = true });
            Assert.AreEqual(10f, fx.Director.Clock.SpeedMultiplier);
            Assert.AreEqual("Kecepatan waktu x10", player.Hud.Message);
            fx.Director.Tick(6f);
            Assert.AreEqual(13f, fx.Director.Clock.Hours, 1e-3f);
            debug.ApplyKeys(new DebugKeys { SlowerSpeed = true });
            Assert.AreEqual(1f, fx.Director.Clock.SpeedMultiplier);
        }

        [Test]
        public void F3_TogglesHudClock()
        {
            fx.Director.Initialize(12f);
            debug.ApplyKeys(new DebugKeys { ToggleClock = true });
            Assert.IsTrue(fx.Director.ShowClock);
            fx.Director.Tick(0.016f);
            Assert.IsTrue(player.Hud.ClockVisible);
            Assert.AreEqual("12:00", player.Hud.Clock);
        }

        [Test]
        public void WorksWhilePlayerControlsAreLocked()
        {
            fx.Director.Initialize(12f);
            player.Locks.Acquire(this, PlayerAbility.All);
            Assert.IsTrue(debug.ApplyKeys(T));
            Assert.AreEqual(19.5f, fx.Director.Clock.Hours, 1e-4f);
        }

        [Test]
        public void BeforeDirectorInitialized_DoesNothing()
        {
            Assert.IsFalse(debug.ApplyKeys(T));
            Assert.IsFalse(fx.Director.IsInitialized);
        }

        [Test]
        public void NoKeys_ReturnsFalse()
        {
            fx.Director.Initialize(12f);
            Assert.IsFalse(debug.ApplyKeys(default));
            Assert.AreEqual(12f, fx.Director.Clock.Hours, 1e-5f);
        }
    }
}
