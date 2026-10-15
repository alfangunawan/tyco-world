using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>Titipan Belanja (spec #6 §5): a kos mate asks for 2-3 things from the warung; buy them and hand them over.</summary>
    public sealed class TitipanMission : Mission
    {
        public const long Tip = 10000;
        public const string HandPrompt = "[E] Serahkan titipan";
        static readonly string[] Wishlist = { "mi-instan", "roti", "kopi-sachet", "teh-kemasan", "air-mineral", "gorengan" };

        readonly List<ItemDefinition> items = new List<ItemDefinition>();
        GameObject friend;
        bool handed;

        public override string Id => "titipan";
        public override string Title => "Titipan Belanja";
        public override string Detail => "Teman kos titip belanjaan dari warung. Uangnya diganti, plus tip.";
        public override string RewardText => "Ganti uang + " + RupiahFormat.Format(Tip);
        public override float StartHour => 8f;
        public override float EndHour => 20f;
        public IReadOnlyList<ItemDefinition> Items => items;
        public GameObject Friend => friend;

        protected override bool Build(MissionContext c)
        {
            handed = false;
            items.Clear();
            var kos = c.NearestLandmark(SpotKind.Kos, c.PlayerPosition);
            if (kos == null) return false;
            var pool = Wishlist.Select(id => c.Catalog.Find(id)).Where(i => i != null).ToList();
            int count = c.Range(2, 4);
            while (items.Count < count && pool.Count > 0)
            {
                int k = c.Range(0, pool.Count);
                items.Add(pool[k]);
                pool.RemoveAt(k);
            }
            if (items.Count == 0) return false;
            Reward = items.Sum(i => i.price) + Tip;
            var at = c.OnGround(kos.position);
            var look = PersonLook.From((ulong)c.Range(1, 1 << 20));
            friend = Own(c.Person("TemanKos", at, Vector3.forward, look, () => HandPrompt, HandOver, HasAll));
            string list = string.Join(", ", items.Select(i => i.displayName));
            Add(new MissionStep
            {
                Objective = "Beli titipan: " + list,
                Target = () =>
                {
                    var shop = c.NearestShop(c.PlayerPosition, s => s.type == ShopType.Warung || s.type == ShopType.Minimarket);
                    return shop != null ? c.OnGround(shop.counter) : (Vector3?)null;
                },
                Enter = () => c.Toast("Teman kos: \"Titip " + list.ToLowerInvariant() + " ya. Nanti kuganti.\""),
                Done = HasAll
            });
            Add(new MissionStep { Objective = "Antar titipan ke teman di depan kos", Target = () => friend != null ? friend.transform.position : at, Done = () => handed });
            return true;
        }

        bool HasAll() => items.All(i => Context.State.Bag.Has(i.id));

        void HandOver()
        {
            if (!HasAll()) return;
            foreach (var i in items) Context.State.Bag.Remove(i.id);
            handed = true;
            Context.Toast("Teman kos: \"Mantap. Nih uangnya, lebihnya buat kamu.\"");
        }
    }
}
