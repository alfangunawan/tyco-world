using System.Linq;
using NUnit.Framework;
using TycoWorld.Player;
using UnityEngine;

namespace TycoWorld.Gameplay.Tests
{
    public class KampungDirectorTests
    {
        KampungFixture fx;

        [SetUp]
        public void SetUp()
        {
            Interactable.ResetStatics();
            fx = new KampungFixture(d => d.shops = new[] { KampungFixture.Site("shop-0", ShopType.Warung, new Vector3(0f, 0f, 2f), 180f) });
        }

        [TearDown]
        public void TearDown() => fx.Dispose();

        [Test]
        public void Ready_BuildsTheShop_AndShowsTheMoney()
        {
            Assert.IsTrue(fx.Director.IsReady);
            Assert.AreEqual(1, fx.Director.Stalls.Count);
            var stall = fx.Director.Stalls[0];
            Assert.IsNotNull(stall.Counter);
            Assert.IsNotNull(stall.Counter.GetComponent<Collider>(), "the counter is an E target");
            Assert.AreEqual(RupiahFormat.Format(SaveData.StartingMoney), fx.Director.Hud.MoneyText);
            Assert.IsTrue(fx.Director.Hud.Toasts.Any(t => t.Contains("Selamat datang")));
        }

        [Test]
        public void Counter_OpensTheShop_BuyingSpendsMoney_ClosingFreesThePlayer()
        {
            var counter = fx.Director.Stalls[0].Counter;
            StringAssert.StartsWith(ShopCounterInteractable.BuyPrefix, counter.GetPrompt(fx.Context));
            Assert.IsTrue(counter.Interact(fx.Context));
            Assert.IsTrue(fx.Director.Shop.IsOpen);
            Assert.IsFalse(fx.Player.Locks.Allows(PlayerAbility.Move), "the shop window holds the player");
            Assert.IsTrue(fx.Director.Shop.ClickBuy("roti"));
            Assert.AreEqual(SaveData.StartingMoney - 6000, fx.Director.State.Wallet.Balance);
            Assert.AreEqual(1, fx.Director.State.Bag.Count("roti"));
            StringAssert.Contains("Roti", fx.Director.Shop.MessageText);
            Assert.IsFalse(fx.Director.Shop.ClickBuy("nasi-telur"), "a warung does not sell meals");
            fx.Director.CloseShop();
            Assert.IsFalse(fx.Director.Shop.IsOpen);
            Assert.IsTrue(fx.Player.Locks.Allows(PlayerAbility.All));
        }

        [Test]
        public void AtNight_TheWarungIsClosed_AndSaysWhenItOpens()
        {
            fx.Director.JumpClock(23f);
            var counter = fx.Director.Stalls[0].Counter;
            StringAssert.Contains("tutup", counter.GetPrompt(fx.Context));
            Assert.IsTrue(counter.Interact(fx.Context));
            Assert.IsFalse(fx.Director.Shop.IsOpen);
            Assert.IsTrue(fx.Director.Hud.Toasts.Any(t => t.Contains("sedang tutup")));
        }

        [Test]
        public void SleepingAtNight_WakesNextMorning_WithTheAllowance()
        {
            fx.Director.JumpClock(22f);
            fx.Director.State.Needs.Set(50f, 20f);
            long before = fx.Director.State.Wallet.Balance;
            fx.Director.Sleep();
            Assert.IsTrue(fx.Director.IsFading);
            fx.Director.FinishFade();
            Assert.AreEqual(KampungDirector.WakeHours, fx.Director.Hours, 1e-3f);
            Assert.AreEqual(2, fx.Director.State.Day);
            Assert.AreEqual(Needs.Max, fx.Director.State.Needs.Energi);
            Assert.AreEqual(30f, fx.Director.State.Needs.Lapar, 1e-3f);
            Assert.AreEqual(before + GameState.DailyAllowance, fx.Director.State.Wallet.Balance);
            Assert.IsTrue(fx.Save.Json.Length > 0, "sleeping saves");
        }

        [Test]
        public void NotSleepyAtNoon_SaysSo()
        {
            fx.Director.JumpClock(12f);
            fx.Director.State.Needs.Set(80f, 95f);
            fx.Director.Sleep();
            Assert.IsFalse(fx.Director.IsFading);
            Assert.IsTrue(fx.Director.Hud.Toasts.Contains(KampungDirector.NotSleepyToast));
        }

        [Test]
        public void Starving_BlocksSprinting_UntilYouEat()
        {
            fx.Director.State.Needs.Set(10f, 80f);
            fx.Director.Tick(1f / 60f);
            Assert.IsFalse(fx.Player.Locks.Allows(PlayerAbility.Sprint));
            Assert.IsTrue(fx.Player.Locks.Allows(PlayerAbility.Move));
            fx.Director.State.Needs.Set(60f, 80f);
            fx.Director.Tick(1f / 60f);
            Assert.IsTrue(fx.Player.Locks.Allows(PlayerAbility.Sprint));
        }

        [Test]
        public void Exhausted_Faints_PaysTheWayHome()
        {
            fx.Director.State.Needs.Set(50f, 0f);
            fx.Director.Tick(1f / 60f);
            Assert.IsTrue(fx.Director.IsFading);
            fx.Director.FinishFade();
            Assert.AreEqual(SaveData.StartingMoney - GameState.FaintCost, fx.Director.State.Wallet.Balance);
            Assert.AreEqual(60f, fx.Director.State.Needs.Energi);
        }

        [Test]
        public void NeedsDrainWithTheClock()
        {
            fx.Director.State.Needs.Set(80f, 80f);
            fx.Run(60f, 0.5f);
            Assert.AreEqual(80f - Needs.HungerPerHour, fx.Director.State.Needs.Lapar, 0.2f, "one game hour per real minute");
        }

        [Test]
        public void Phone_ShowsTheBag_AndEatingFromItRestoresHunger()
        {
            fx.Director.State.Buy(fx.Director.Catalog.Find("roti"));
            fx.Director.State.Needs.Set(40f, 80f);
            fx.Director.TogglePhone();
            Assert.IsTrue(fx.Director.Phone.IsOpen);
            fx.Director.Phone.Select(PhoneTab.Tas);
            fx.Director.Tick(1f / 60f);
            Assert.IsTrue(fx.Director.Phone.Click(PhoneView.UsePrefix + "roti"));
            Assert.AreEqual(55f, fx.Director.State.Needs.Lapar, 0.1f);
            Assert.AreEqual(0, fx.Director.State.Bag.Count("roti"));
            fx.Director.TogglePhone();
            Assert.IsTrue(fx.Player.Locks.Allows(PlayerAbility.All));
        }

        [Test]
        public void Save_ThenANewDirector_ContinuesTheGame()
        {
            fx.Director.State.Wallet.Earn(12345);
            fx.Director.JumpClock(15.5f);
            fx.Director.SaveNow();
            var loaded = new SaveStore(fx.Save).Load();
            Assert.AreEqual(SaveData.StartingMoney + 12345, loaded.money);
            Assert.AreEqual(15.5f, loaded.hours, 1e-3f);
        }
    }
}
