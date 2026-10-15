using System;
using System.Collections.Generic;

namespace TycoWorld.Player
{
    [Flags]
    public enum PlayerAbility
    {
        None = 0,
        Move = 1,
        Look = 2,
        Interact = 4,
        Flashlight = 8,
        /// <summary>Running (gameplay needs block it when the player is starving).</summary>
        Sprint = 16,
        All = Move | Look | Interact | Flashlight | Sprint
    }

    /// <summary>Owner-keyed ability locks: a vehicle, cutscene or spawner can only release its own lock.</summary>
    public sealed class ControlLocks
    {
        readonly Dictionary<object, PlayerAbility> locks = new Dictionary<object, PlayerAbility>();

        public event Action Changed;

        public PlayerAbility Blocked { get; private set; }

        public void Acquire(object owner, PlayerAbility blocked)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            locks[owner] = blocked;
            Recompute();
        }

        public bool Release(object owner)
        {
            if (owner == null || !locks.Remove(owner)) return false;
            Recompute();
            return true;
        }

        public bool IsHeldBy(object owner) => owner != null && locks.ContainsKey(owner);

        public bool Allows(PlayerAbility ability) => (Blocked & ability) == 0;

        void Recompute()
        {
            var blocked = PlayerAbility.None;
            foreach (var mask in locks.Values) blocked |= mask;
            if (blocked == Blocked) return;
            Blocked = blocked;
            Changed?.Invoke();
        }
    }
}
