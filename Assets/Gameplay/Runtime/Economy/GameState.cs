using System;
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.Gameplay
{
    public enum PurchaseResult
    {
        Bought,
        Eaten,
        Worn,
        DataActive,
        Serviced,
        AlreadyOwned,
        NotEnoughMoney,
        Unknown,
        /// <summary>The data package bought earlier today still runs; nothing charged.</summary>
        StillActive
    }

    /// <summary>
    /// The player's life outside the world objects (spec #6 §2): money, needs, bag, clothes, the game day, the data
    /// package and finished missions. Pure; the director applies it to the scene.
    /// </summary>
    public sealed class GameState
    {
        public const long DailyAllowance = 25000;
        public const long FaintCost = 10000;

        readonly SortedSet<string> wardrobe = new SortedSet<string>(StringComparer.Ordinal);
        readonly SortedSet<string> missionsDone = new SortedSet<string>(StringComparer.Ordinal);

        public Wallet Wallet { get; } = new Wallet(SaveData.StartingMoney);
        public Needs Needs { get; } = new Needs();
        public Inventory Bag { get; } = new Inventory();
        public string Wearing { get; private set; } = "";
        public int Day { get; private set; } = 1;
        public int DataUntilDay { get; private set; }
        public int Sleeps { get; private set; }
        public int LastAllowanceDay { get; private set; } = 1;

        public IReadOnlyCollection<string> Wardrobe => wardrobe;
        public IReadOnlyCollection<string> MissionsDone => missionsDone;
        public bool HasData => DataUntilDay >= Day;
        public bool Owns(string id) => id != null && wardrobe.Contains(id);
        /// <summary>A jacket keeps the night chill off (Needs).</summary>
        public bool IsWarm => Wearing == ItemCatalog.Jaket;
        /// <summary>A black hoodie makes witnesses notice a theft from closer (spec #6 §2).</summary>
        public bool IsHooded => Wearing == ItemCatalog.Hoodie;

        public event Action Changed;

        public PurchaseResult Buy(ItemDefinition item)
        {
            if (item == null) return PurchaseResult.Unknown;
            if (item.wearable && wardrobe.Contains(item.id))
            {
                Wear(item.id);
                return PurchaseResult.AlreadyOwned;
            }
            // Data is for the day: a second package the same day would buy nothing.
            if (item.category == ItemCategory.Data && HasData) return PurchaseResult.StillActive;
            if (!Wallet.TrySpend(item.price)) return PurchaseResult.NotEnoughMoney;
            PurchaseResult result;
            if (item.wearable)
            {
                wardrobe.Add(item.id);
                Wearing = item.id;
                result = PurchaseResult.Worn;
            }
            else if (item.category == ItemCategory.Data)
            {
                DataUntilDay = Day;
                result = PurchaseResult.DataActive;
            }
            else if (item.category == ItemCategory.Service)
            {
                result = PurchaseResult.Serviced;
            }
            else if (item.consumeOnPurchase)
            {
                Needs.Restore(item.lapar, item.energi);
                result = PurchaseResult.Eaten;
            }
            else
            {
                Bag.Add(item.id);
                result = PurchaseResult.Bought;
            }
            Changed?.Invoke();
            return result;
        }

        /// <summary>Eats or drinks one of <paramref name="item"/> from the bag.</summary>
        public bool Use(ItemDefinition item)
        {
            if (item == null || !item.IsConsumable || !Bag.Remove(item.id)) return false;
            Needs.Restore(item.lapar, item.energi);
            Changed?.Invoke();
            return true;
        }

        public bool Wear(string id)
        {
            if (string.IsNullOrEmpty(id)) Wearing = "";
            else if (wardrobe.Contains(id)) Wearing = id;
            else return false;
            Changed?.Invoke();
            return true;
        }

        public void CompleteMission(string id)
        {
            if (!string.IsNullOrEmpty(id) && missionsDone.Add(id)) Changed?.Invoke();
        }

        public bool IsMissionDone(string id) => id != null && missionsDone.Contains(id);

        /// <summary>The game clock passed midnight.</summary>
        /// <summary>A data package for today without paying (the first day's welcome).</summary>
        public void GrantData()
        {
            if (DataUntilDay >= Day) return;
            DataUntilDay = Day;
            Changed?.Invoke();
        }

        public void NextDay()
        {
            Day++;
            Changed?.Invoke();
        }

        /// <summary>Waking up in the kos: the parents' daily allowance arrives once per day. Returns what arrived.</summary>
        public long WakeUp()
        {
            Sleeps++;
            long paid = 0;
            if (LastAllowanceDay < Day)
            {
                LastAllowanceDay = Day;
                Wallet.Earn(DailyAllowance);
                paid = DailyAllowance;
            }
            Changed?.Invoke();
            return paid;
        }

        /// <summary>Collapsing from exhaustion: someone takes you home for a fee; you wake weak and hungry.</summary>
        public long Faint()
        {
            long taken = Wallet.TakeUpTo(FaintCost);
            Needs.Set(Mathf.Max(0f, Needs.Lapar - 15f), 60f);
            Changed?.Invoke();
            return taken;
        }

        public void Load(SaveData data)
        {
            Wallet.Set(data.money);
            Needs.Set(data.lapar, data.energi);
            Bag.Clear();
            for (int i = 0; i < data.itemIds.Length; i++) Bag.Add(data.itemIds[i], data.itemCounts[i]);
            wardrobe.Clear();
            foreach (var id in data.wardrobe)
                if (!string.IsNullOrEmpty(id)) wardrobe.Add(id);
            Wearing = wardrobe.Contains(data.wearing) ? data.wearing : "";
            Day = Math.Max(1, data.day);
            // Older saves have no allowance day: count today's as paid.
            LastAllowanceDay = data.lastAllowanceDay > 0 ? Math.Min(data.lastAllowanceDay, Day) : Day;
            DataUntilDay = data.dataUntilDay;
            missionsDone.Clear();
            foreach (var id in data.missionsDone)
                if (!string.IsNullOrEmpty(id)) missionsDone.Add(id);
            Sleeps = data.sleeps;
            Changed?.Invoke();
        }

        public SaveData ToSave(float hours) =>
            SaveData.FromState(Wallet, Needs, Bag, wardrobe, Wearing, Day, hours, DataUntilDay, missionsDone, Sleeps, LastAllowanceDay);
    }
}
