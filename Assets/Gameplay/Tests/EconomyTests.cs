using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TycoWorld.Gameplay.Tests
{
    public class EconomyTests
    {
        [TestCase(0, "Rp 0")]
        [TestCase(500, "Rp 500")]
        [TestCase(4000, "Rp 4.000")]
        [TestCase(12500, "Rp 12.500")]
        [TestCase(150000, "Rp 150.000")]
        [TestCase(1250000, "Rp 1.250.000")]
        [TestCase(-20000, "-Rp 20.000")]
        public void Rupiah_UsesIndonesianThousandsDots(long amount, string expected) => Assert.AreEqual(expected, RupiahFormat.Format(amount));

        [Test]
        public void Wallet_NeverGoesNegative()
        {
            var w = new Wallet(10000);
            int changes = 0;
            w.Changed += () => changes++;
            Assert.IsFalse(w.TrySpend(12000));
            Assert.AreEqual(10000, w.Balance);
            Assert.IsTrue(w.TrySpend(4000));
            Assert.AreEqual(6000, w.Balance);
            w.Earn(15000);
            Assert.AreEqual(21000, w.Balance);
            Assert.AreEqual(21000, w.TakeUpTo(100000), "a fine takes what is there");
            Assert.AreEqual(0, w.Balance);
            Assert.AreEqual(3, changes);
            Assert.IsFalse(w.TrySpend(-5));
        }

        [Test]
        public void Inventory_StacksAndRemovesInIdOrder()
        {
            var bag = new Inventory();
            bag.Add("roti");
            bag.Add("air-mineral", 2);
            bag.Add("roti");
            Assert.AreEqual(2, bag.Count("roti"));
            CollectionAssert.AreEqual(new[] { "air-mineral", "roti" }, bag.Stacks.Select(s => s.Key).ToArray());
            Assert.IsFalse(bag.Remove("roti", 3));
            Assert.IsTrue(bag.Remove("air-mineral", 2));
            Assert.AreEqual(1, bag.StackCount);
            Assert.IsFalse(bag.Has("air-mineral"));
        }

        [Test]
        public void Needs_DrainWithGameTime_AndNightChillsWithoutAJacket()
        {
            var day = new Needs();
            day.Set(80f, 90f);
            day.Tick(2f, night: false, warm: false, sprinting: false);
            Assert.AreEqual(80f - 2f * Needs.HungerPerHour, day.Lapar, 1e-4f);
            Assert.AreEqual(90f - 2f * Needs.EnergyPerHour, day.Energi, 1e-4f);
            var cold = new Needs();
            cold.Set(80f, 90f);
            cold.Tick(2f, night: true, warm: false, sprinting: false);
            var warm = new Needs();
            warm.Set(80f, 90f);
            warm.Tick(2f, night: true, warm: true, sprinting: false);
            Assert.Less(cold.Energi, warm.Energi);
            var starving = new Needs();
            starving.Set(10f, 0f);
            Assert.IsTrue(starving.IsStarving);
            Assert.IsTrue(starving.IsExhausted);
            starving.Restore(200f, 50f);
            Assert.AreEqual(Needs.Max, starving.Lapar);
        }

        [TestCase(ShopType.Warung, 5.5f, false)]
        [TestCase(ShopType.Warung, 6f, true)]
        [TestCase(ShopType.Warung, 21.9f, true)]
        [TestCase(ShopType.Warung, 22f, false)]
        [TestCase(ShopType.Bengkel, 17.5f, false)]
        [TestCase(ShopType.Minimarket, 3f, true)]
        [TestCase(ShopType.Minimarket, 23.99f, true)]
        public void OpeningHours_ClosedAtNightExceptTheMinimarket(ShopType type, float hours, bool open) =>
            Assert.AreEqual(open, OpeningHours.IsOpen(type, hours));

        [Test]
        public void Catalog_EveryStockedItemExists_AndEveryShopSellsSomething()
        {
            var catalog = ItemCatalog.CreateDefault();
            try
            {
                foreach (ShopType type in System.Enum.GetValues(typeof(ShopType)))
                {
                    var stock = ItemCatalog.Stock(type);
                    Assert.Greater(stock.Length, 0, type.ToString());
                    foreach (var id in stock)
                    {
                        var item = catalog.Find(id);
                        Assert.IsNotNull(item, type + " sells unknown " + id);
                        Assert.Greater(item.price, 0, id);
                        Assert.IsFalse(string.IsNullOrEmpty(item.displayName), id);
                    }
                }
                Assert.AreEqual(catalog.items.Length, catalog.items.Select(i => i.id).Distinct().Count(), "ids are unique");
                Assert.IsTrue(catalog.Find(ItemCatalog.Jaket).wearable);
                Assert.IsTrue(catalog.Find("nasi-telur").consumeOnPurchase);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void Save_RoundTrips()
        {
            var backend = new MemorySaveBackend();
            var store = new SaveStore(backend);
            Assert.IsFalse(store.HasSave);
            var wallet = new Wallet(73000);
            var needs = new Needs();
            needs.Set(40f, 55f);
            var bag = new Inventory();
            bag.Add("roti", 2);
            bag.Add(ItemCatalog.Galon);
            store.Save(SaveData.FromState(wallet, needs, bag, new[] { ItemCatalog.Jaket }, ItemCatalog.Jaket, 3, 21.25f, 3, new[] { "galon-1" }, 2));
            Assert.IsTrue(store.HasSave);
            var loaded = store.Load();
            Assert.AreEqual(73000, loaded.money);
            Assert.AreEqual(40f, loaded.lapar, 1e-4f);
            Assert.AreEqual(3, loaded.day);
            Assert.AreEqual(21.25f, loaded.hours, 1e-4f);
            CollectionAssert.AreEqual(new[] { ItemCatalog.Galon, "roti" }, loaded.itemIds);
            CollectionAssert.AreEqual(new[] { 1, 2 }, loaded.itemCounts);
            Assert.AreEqual(ItemCatalog.Jaket, loaded.wearing);
            CollectionAssert.AreEqual(new[] { "galon-1" }, loaded.missionsDone);
        }

        [Test]
        public void Save_BadDataOrAnotherVersion_StartsANewGame()
        {
            var backend = new MemorySaveBackend { Json = "{not json" };
            LogAssert.Expect(LogType.Warning, SaveStore.UnreadableWarning);
            var fresh = new SaveStore(backend).Load();
            Assert.AreEqual(SaveData.StartingMoney, fresh.money);
            backend.Json = "{\"version\":99,\"money\":5}";
            LogAssert.Expect(LogType.Warning, SaveStore.UnknownVersionWarningPrefix + "99; mulai baru.");
            Assert.AreEqual(SaveData.StartingMoney, new SaveStore(backend).Load().money);
            backend.Json = "{\"version\":1,\"money\":-5,\"lapar\":400,\"day\":0,\"itemIds\":[\"a\"],\"itemCounts\":[]}";
            var clamped = new SaveStore(backend).Load();
            Assert.AreEqual(0, clamped.money);
            Assert.AreEqual(Needs.Max, clamped.lapar);
            Assert.AreEqual(1, clamped.day);
            Assert.AreEqual(0, clamped.itemIds.Length, "mismatched bag arrays are dropped");
        }
    }
}
