using NUnit.Framework;
using UnityEngine;

namespace TycoWorld.Gameplay.Tests
{
    public class GameStateTests
    {
        ItemCatalog catalog;

        [SetUp]
        public void SetUp() => catalog = ItemCatalog.CreateDefault();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(catalog);

        [Test]
        public void Buy_SnackGoesInTheBag_MealIsEatenAtOnce()
        {
            var state = new GameState();
            state.Needs.Set(40f, 50f);
            Assert.AreEqual(PurchaseResult.Bought, state.Buy(catalog.Find("roti")));
            Assert.AreEqual(1, state.Bag.Count("roti"));
            Assert.AreEqual(SaveData.StartingMoney - 6000, state.Wallet.Balance);
            Assert.AreEqual(PurchaseResult.Eaten, state.Buy(catalog.Find("nasi-telur")));
            Assert.AreEqual(95f, state.Needs.Lapar, 1e-4f);
            Assert.AreEqual(0, state.Bag.Count("nasi-telur"));
            Assert.IsTrue(state.Use(catalog.Find("roti")));
            Assert.AreEqual(Needs.Max, state.Needs.Lapar);
            Assert.IsFalse(state.Use(catalog.Find("roti")), "none left");
        }

        [Test]
        public void Buy_TooExpensive_ChangesNothing()
        {
            var state = new GameState();
            Assert.AreEqual(PurchaseResult.NotEnoughMoney, state.Buy(catalog.Find(ItemCatalog.Jaket)));
            Assert.AreEqual(SaveData.StartingMoney, state.Wallet.Balance);
            Assert.AreEqual("", state.Wearing);
        }

        [Test]
        public void Clothes_AreBoughtOnce_ThenWornForFree()
        {
            var state = new GameState();
            state.Wallet.Earn(300000);
            Assert.AreEqual(PurchaseResult.Worn, state.Buy(catalog.Find(ItemCatalog.Jaket)));
            Assert.IsTrue(state.IsWarm);
            long after = state.Wallet.Balance;
            Assert.AreEqual(PurchaseResult.Worn, state.Buy(catalog.Find(ItemCatalog.Hoodie)));
            Assert.IsTrue(state.IsHooded);
            Assert.AreEqual(PurchaseResult.AlreadyOwned, state.Buy(catalog.Find(ItemCatalog.Jaket)));
            Assert.AreEqual(after - 120000, state.Wallet.Balance, "the jacket again costs nothing");
            Assert.IsTrue(state.IsWarm);
        }

        [Test]
        public void DataPackage_LastsTheDayItWasBought()
        {
            var state = new GameState();
            Assert.IsFalse(state.HasData);
            Assert.AreEqual(PurchaseResult.DataActive, state.Buy(catalog.Find(ItemCatalog.PaketData)));
            Assert.IsTrue(state.HasData);
            state.NextDay();
            Assert.IsFalse(state.HasData);
        }

        [Test]
        public void Allowance_ArrivesOncePerDay_AndFaintingCostsMoney()
        {
            var state = new GameState();
            Assert.AreEqual(0, state.WakeUp(), "day 1: no allowance yet");
            state.NextDay();
            Assert.AreEqual(GameState.DailyAllowance, state.WakeUp());
            Assert.AreEqual(0, state.WakeUp(), "second nap the same day");
            state.Needs.Set(50f, 0f);
            Assert.AreEqual(GameState.FaintCost, state.Faint());
            Assert.AreEqual(60f, state.Needs.Energi);
            Assert.AreEqual(35f, state.Needs.Lapar);
        }

        [Test]
        public void SaveAndLoad_KeepEverything()
        {
            var state = new GameState();
            state.Wallet.Earn(200000);
            state.Buy(catalog.Find(ItemCatalog.Hoodie));
            state.Buy(catalog.Find("roti"));
            state.Buy(catalog.Find(ItemCatalog.PaketData));
            state.CompleteMission("galon-1");
            state.NextDay();
            var copy = new GameState();
            copy.Load(state.ToSave(20.5f));
            Assert.AreEqual(state.Wallet.Balance, copy.Wallet.Balance);
            Assert.AreEqual(ItemCatalog.Hoodie, copy.Wearing);
            Assert.AreEqual(1, copy.Bag.Count("roti"));
            Assert.AreEqual(2, copy.Day);
            Assert.IsFalse(copy.HasData);
            Assert.IsTrue(copy.IsMissionDone("galon-1"));
            Assert.AreEqual(0, copy.WakeUp(), "the loaded day already had its allowance");
        }
    }
}
