using System;

namespace TycoWorld.Gameplay
{
    /// <summary>The player's money in rupiah; never negative (spec #6 §2).</summary>
    public sealed class Wallet
    {
        public long Balance { get; private set; }

        public event Action Changed;

        public Wallet(long balance = 0) => Balance = Math.Max(0, balance);

        public bool CanAfford(long amount) => amount <= Balance;

        public bool TrySpend(long amount)
        {
            if (amount < 0 || amount > Balance) return false;
            if (amount == 0) return true;
            Balance -= amount;
            Changed?.Invoke();
            return true;
        }

        public void Earn(long amount)
        {
            if (amount <= 0) return;
            Balance += amount;
            Changed?.Invoke();
        }

        /// <summary>Takes up to <paramref name="amount"/> and returns what was actually taken (fines when the player is short).</summary>
        public long TakeUpTo(long amount)
        {
            long taken = Math.Min(Math.Max(0, amount), Balance);
            if (taken > 0)
            {
                Balance -= taken;
                Changed?.Invoke();
            }
            return taken;
        }

        public void Set(long balance)
        {
            Balance = Math.Max(0, balance);
            Changed?.Invoke();
        }
    }
}
