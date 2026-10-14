using NUnit.Framework;
using TycoWorld.Player;

namespace TycoWorld.GameSettings.Tests
{
    public class PauseControllerTests
    {
        GameFixture fx;
        FakePauseEffects effects;
        PauseController pause;
        FirstPersonPlayer player;

        [SetUp]
        public void SetUp()
        {
            fx = new GameFixture();
            effects = new FakePauseEffects();
            pause = new PauseController(effects);
            player = fx.CreatePlayer();
        }

        [TearDown]
        public void TearDown() => fx.Dispose();

        [Test]
        public void Attach_TakesEscapeAwayFromThePlayer()
        {
            pause.Attach(player);
            Assert.IsFalse(player.EscapeUnlocksCursor);
        }

        [Test]
        public void Pause_FreezesTimeAudioAndControls()
        {
            pause.Attach(player);
            pause.Pause();
            Assert.IsTrue(pause.IsPaused);
            Assert.IsTrue(effects.Paused);
            foreach (var ability in new[] { PlayerAbility.Move, PlayerAbility.Look, PlayerAbility.Interact, PlayerAbility.Flashlight })
                Assert.IsFalse(player.Locks.Allows(ability), ability.ToString());
        }

        [Test]
        public void Resume_RestoresEverything()
        {
            pause.Attach(player);
            pause.Pause();
            pause.Resume();
            Assert.IsFalse(pause.IsPaused);
            Assert.IsFalse(effects.Paused);
            Assert.IsTrue(player.Locks.Allows(PlayerAbility.All));
        }

        [Test]
        public void Resume_KeepsOtherOwnersLocks()
        {
            var spawner = new object();
            player.Locks.Acquire(spawner, PlayerAbility.All);
            pause.Attach(player);
            pause.Pause();
            pause.Resume();
            Assert.IsTrue(player.Locks.IsHeldBy(spawner));
            Assert.IsFalse(player.Locks.Allows(PlayerAbility.Move));
        }

        [Test]
        public void PauseAndResume_AreIdempotent()
        {
            pause.Attach(player);
            pause.Pause();
            pause.Pause();
            Assert.AreEqual(1, effects.Calls);
            pause.Resume();
            pause.Resume();
            Assert.AreEqual(2, effects.Calls);
        }

        [Test]
        public void Shutdown_WhilePaused_RestoresTimeAndAudio()
        {
            pause.Attach(player);
            pause.Pause();
            pause.Shutdown();
            Assert.IsFalse(pause.IsPaused);
            Assert.IsFalse(effects.Paused);
            Assert.IsTrue(player.Locks.Allows(PlayerAbility.Move));
        }

        [Test]
        public void WithoutPlayer_StillPausesTime()
        {
            pause.Attach(null);
            pause.Pause();
            Assert.IsTrue(effects.Paused);
            pause.Resume();
            Assert.IsFalse(effects.Paused);
        }
    }
}
