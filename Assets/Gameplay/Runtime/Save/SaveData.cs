using System;
using System.Collections.Generic;

namespace TycoWorld.Gameplay
{
    /// <summary>Everything that survives quitting (spec #6 §2): JSON via JsonUtility, so only fields and arrays.</summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;
        public const long StartingMoney = 50000;

        public int version = CurrentVersion;
        public long money = StartingMoney;
        public float lapar = 80f;
        public float energi = 90f;
        public int day = 1;
        public float hours = 6.5f;
        public string[] itemIds = new string[0];
        public int[] itemCounts = new int[0];
        /// <summary>Clothing owned; the first entry of <see cref="wearing"/> is the outfit on.</summary>
        public string[] wardrobe = new string[0];
        public string wearing = "";
        /// <summary>Day number through which the mobile data package lasts (0 = none).</summary>
        public int dataUntilDay;
        public string[] missionsDone = new string[0];
        public int sleeps;

        public static SaveData FromState(Wallet wallet, Needs needs, Inventory bag, IReadOnlyCollection<string> wardrobe, string wearing,
            int day, float hours, int dataUntilDay, IReadOnlyCollection<string> missionsDone, int sleeps)
        {
            var data = new SaveData
            {
                money = wallet.Balance, lapar = needs.Lapar, energi = needs.Energi, day = day, hours = hours, wearing = wearing ?? "",
                dataUntilDay = dataUntilDay, sleeps = sleeps
            };
            var ids = new List<string>();
            var counts = new List<int>();
            foreach (var stack in bag.Stacks)
            {
                ids.Add(stack.Key);
                counts.Add(stack.Value);
            }
            data.itemIds = ids.ToArray();
            data.itemCounts = counts.ToArray();
            data.wardrobe = new List<string>(wardrobe).ToArray();
            data.missionsDone = new List<string>(missionsDone).ToArray();
            return data;
        }
    }
}
