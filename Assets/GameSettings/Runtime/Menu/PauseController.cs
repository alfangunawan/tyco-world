using TycoWorld.Player;

namespace TycoWorld.GameSettings
{
    /// <summary>
    /// Pausing (spec #4 §3/§6): freezes game time and audio, locks every player ability under its own owner and frees
    /// the cursor. Resume undoes exactly that, so locks held by others (the spawner while the world loads) stay.
    /// </summary>
    public sealed class PauseController
    {
        readonly IPauseEffects effects;
        readonly object lockOwner = new object();
        FirstPersonPlayer player;

        public PauseController(IPauseEffects effects) => this.effects = effects;

        public bool IsPaused { get; private set; }

        /// <summary>Takes Esc away from the player (it now opens the menu). Null is allowed (scenes without a player).</summary>
        public void Attach(FirstPersonPlayer firstPerson)
        {
            player = firstPerson;
            if (player != null) player.EscapeUnlocksCursor = false;
        }

        public void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;
            effects.SetPaused(true);
            if (player != null && player.IsInitialized)
            {
                player.Locks.Acquire(lockOwner, PlayerAbility.All);
                player.UnlockCursor();
            }
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            effects.SetPaused(false);
            if (player != null && player.IsInitialized)
            {
                player.Locks.Release(lockOwner);
                if (player.Locks.Allows(PlayerAbility.Look)) player.LockCursor();
            }
        }

        /// <summary>The menu is going away (Play stopped, scene unloaded): never leave the game frozen.</summary>
        public void Shutdown() => Resume();
    }
}
