using System;

namespace TycoWorld.Gameplay
{
    /// <summary>One thing a shop sells. Effects apply when it is eaten, drunk or used (spec #6 §2).</summary>
    [Serializable]
    public sealed class ItemDefinition
    {
        public string id = "";
        public string displayName = "";
        public ItemCategory category;
        public long price;
        /// <summary>Hunger restored (0..100 scale).</summary>
        public float lapar;
        /// <summary>Energy restored (0..100 scale).</summary>
        public float energi;
        /// <summary>Eaten at the counter instead of going into the bag (warung makan).</summary>
        public bool consumeOnPurchase;
        /// <summary>Clothing worn once bought (one of each); see OutfitEffects.</summary>
        public bool wearable;

        public ItemDefinition() { }

        public ItemDefinition(string id, string displayName, ItemCategory category, long price, float lapar = 0f, float energi = 0f,
            bool consumeOnPurchase = false, bool wearable = false)
        {
            this.id = id;
            this.displayName = displayName;
            this.category = category;
            this.price = price;
            this.lapar = lapar;
            this.energi = energi;
            this.consumeOnPurchase = consumeOnPurchase;
            this.wearable = wearable;
        }

        public bool IsConsumable => lapar > 0f || energi > 0f;
    }
}
