using TycoWorld.Player;
using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>"[E] Belanja di Warung Bu Tini" on an open shop's counter; a closed one says when it opens (spec #6 §2).</summary>
    public sealed class ShopCounterInteractable : Interactable
    {
        public const string BuyPrefix = "[E] Belanja di ";
        public const string ClosedSuffix = " (tutup)";

        [SerializeField] KampungDirector director;
        ShopStall stall;

        public ShopStall Stall => stall;

        public void Configure(KampungDirector owner, ShopStall shop)
        {
            director = owner;
            stall = shop;
        }

        bool Open => director != null && stall != null && director.IsShopOpen(stall.Site);

        public override string GetPrompt(in InteractionContext context)
        {
            if (stall == null) return "";
            return Open ? BuyPrefix + stall.Site.name : "[E] " + stall.Site.name + ClosedSuffix;
        }

        public override bool CanInteract(in InteractionContext context) => director != null && stall != null && !director.IsModalOpen;

        protected override void OnInteract(in InteractionContext context)
        {
            if (Open) director.OpenShop(stall);
            else director.Toast(stall.Site.name + " sedang tutup. " + OpeningHours.Describe(stall.Site.type) + ".");
        }
    }
}
