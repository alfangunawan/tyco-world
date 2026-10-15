using System;
using System.Collections.Generic;

namespace TycoWorld.Gameplay
{
    /// <summary>The bag: stacks of item ids, iterated in id order so saves and lists are stable (spec #6 §2).</summary>
    public sealed class Inventory
    {
        readonly SortedDictionary<string, int> counts = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public event Action Changed;

        public int Count(string id) => id != null && counts.TryGetValue(id, out int n) ? n : 0;

        public bool Has(string id, int amount = 1) => Count(id) >= amount;

        public IEnumerable<KeyValuePair<string, int>> Stacks => counts;

        public int StackCount => counts.Count;

        public void Add(string id, int amount = 1)
        {
            if (string.IsNullOrEmpty(id) || amount <= 0) return;
            counts[id] = Count(id) + amount;
            Changed?.Invoke();
        }

        public bool Remove(string id, int amount = 1)
        {
            if (amount <= 0 || Count(id) < amount) return false;
            int left = counts[id] - amount;
            if (left == 0) counts.Remove(id);
            else counts[id] = left;
            Changed?.Invoke();
            return true;
        }

        public void Clear()
        {
            if (counts.Count == 0) return;
            counts.Clear();
            Changed?.Invoke();
        }
    }
}
