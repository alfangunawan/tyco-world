using System;
using NUnit.Framework;

namespace TycoWorld.Player.Tests
{
    public class ControlLocksTests
    {
        [Test]
        public void Empty_AllowsEverything()
        {
            var locks = new ControlLocks();
            Assert.AreEqual(PlayerAbility.None, locks.Blocked);
            Assert.IsTrue(locks.Allows(PlayerAbility.All));
        }

        [Test]
        public void Acquire_BlocksUnionOfOwners()
        {
            var locks = new ControlLocks();
            object bike = new object(), cutscene = new object();
            locks.Acquire(bike, PlayerAbility.Move | PlayerAbility.Interact);
            locks.Acquire(cutscene, PlayerAbility.Look);
            Assert.IsFalse(locks.Allows(PlayerAbility.Move));
            Assert.IsFalse(locks.Allows(PlayerAbility.Look));
            Assert.IsTrue(locks.Allows(PlayerAbility.Flashlight));
            Assert.IsTrue(locks.Release(bike));
            Assert.IsTrue(locks.Allows(PlayerAbility.Move));
            Assert.IsFalse(locks.Allows(PlayerAbility.Look));
        }

        [Test]
        public void Release_ByWrongOwner_IsNoOp()
        {
            var locks = new ControlLocks();
            object bike = new object();
            locks.Acquire(bike, PlayerAbility.Move);
            Assert.IsFalse(locks.Release(new object()));
            Assert.IsFalse(locks.Release(null));
            Assert.IsFalse(locks.Allows(PlayerAbility.Move));
            Assert.IsTrue(locks.IsHeldBy(bike));
        }

        [Test]
        public void Acquire_SameOwner_ReplacesMask()
        {
            var locks = new ControlLocks();
            object owner = new object();
            locks.Acquire(owner, PlayerAbility.All);
            locks.Acquire(owner, PlayerAbility.Look);
            Assert.IsTrue(locks.Allows(PlayerAbility.Move));
            Assert.IsFalse(locks.Allows(PlayerAbility.Look));
        }

        [Test]
        public void Changed_FiresOnlyWhenBlockedChanges()
        {
            var locks = new ControlLocks();
            int fired = 0;
            locks.Changed += () => fired++;
            object a = new object(), b = new object();
            locks.Acquire(a, PlayerAbility.Move);
            locks.Acquire(b, PlayerAbility.Move);
            locks.Release(a);
            locks.Release(b);
            Assert.AreEqual(2, fired);
        }

        [Test]
        public void Acquire_NullOwner_Throws() =>
            Assert.Throws<ArgumentNullException>(() => new ControlLocks().Acquire(null, PlayerAbility.Move));
    }
}
