using TycoWorld.Player;
using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>Antar Galon (spec #6 §5): fetch a full galon from a warung (already paid) and carry it to a house; too heavy to run.</summary>
    public sealed class GalonMission : Mission
    {
        public const long Pay = 15000;
        public const float ShopRange = 450f, DoorMin = 70f, DoorMax = 320f, CounterRadius = 3.2f;
        public const string HandPrompt = "[E] Serahkan galon";

        readonly object sprintLock = new object();
        ShopSite shop;
        Spot door;
        GameObject customer;
        bool delivered, locked;
        FirstPersonPlayer lockedPlayer;

        public override string Id => "galon";
        public override string Title => "Antar Galon";
        public override string Detail => "Ambil galon isi ulang di warung (sudah dibayar), antar ke rumah pemesan.";
        public override string RewardText => RupiahFormat.Format(Pay);
        public override float StartHour => 7f;
        public override float EndHour => 18f;
        public ShopSite Shop => shop;
        public Spot Door => door;
        public GameObject Customer => customer;

        protected override bool Build(MissionContext c)
        {
            delivered = false;
            shop = c.NearestShop(c.PlayerPosition, s => s.type == ShopType.Warung || s.type == ShopType.Minimarket, ShopRange);
            if (shop == null) return false;
            door = c.Door(shop.counter, DoorMin, DoorMax);
            if (door == null) return false;
            Reward = Pay;
            var at = c.InFrontOf(door);
            var face = Quaternion.Euler(0f, door.yaw, 0f) * Vector3.forward;
            customer = Own(c.Person("Pemesan", at, face, PersonLook.From((ulong)c.Range(1, 1 << 20)), () => HandPrompt, HandOver,
                () => c.State.Bag.Has(ItemCatalog.Galon)));
            Add(new MissionStep
            {
                Objective = "Ambil galon di " + shop.name,
                Target = () => c.OnGround(shop.counter),
                Done = () => c.Near(shop.counter, CounterRadius)
            });
            Add(new MissionStep
            {
                Objective = "Antar galon ke rumah pemesan",
                Target = () => at,
                Enter = () =>
                {
                    c.State.Bag.Add(ItemCatalog.Galon);
                    Lock(c, true);
                    c.Toast("Galon diangkat. Berat! Tidak bisa lari.");
                },
                Done = () => delivered
            });
            return true;
        }

        void HandOver()
        {
            if (!Context.State.Bag.Remove(ItemCatalog.Galon)) return;
            delivered = true;
            Lock(Context, false);
            Context.Toast("Pemesan: \"Makasih, a. Pas banget galonnya habis.\"");
        }

        void Lock(MissionContext c, bool on)
        {
            var player = c.Director.Player;
            if (on && !locked && player != null)
            {
                player.Locks.Acquire(sprintLock, PlayerAbility.Sprint);
                lockedPlayer = player;
                locked = true;
            }
            else if (!on && locked)
            {
                if (lockedPlayer != null) lockedPlayer.Locks.Release(sprintLock);
                lockedPlayer = null;
                locked = false;
            }
        }

        protected override void OnFailed()
        {
            // The galon goes back to the warung.
            if (!delivered) Context.State.Bag.Remove(ItemCatalog.Galon);
        }

        protected override void OnCleanup() => Lock(Context, false);
    }
}
