using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// Everything for sale and which shops stock it (spec #6 §2). The field initializers are the defaults; names are
    /// generic Indonesian goods, never real brands.
    /// </summary>
    [CreateAssetMenu(menuName = "Tyco World/Item Catalog", fileName = "ItemCatalog")]
    public sealed class ItemCatalog : ScriptableObject
    {
        public const string Galon = "galon";
        public const string PaketData = "paket-data";
        public const string CatUlang = "cat-ulang";
        public const string Jaket = "jaket-parka";
        public const string Hoodie = "hoodie-hitam";

        public ItemDefinition[] items = DefaultItems();

        public static ItemDefinition[] DefaultItems() => new[]
        {
            new ItemDefinition("air-mineral", "Air mineral", ItemCategory.Drink, 4000, lapar: 2f, energi: 4f),
            new ItemDefinition("teh-kemasan", "Teh kemasan", ItemCategory.Drink, 5000, lapar: 3f, energi: 6f),
            new ItemDefinition("kopi-sachet", "Kopi sachet", ItemCategory.Drink, 3000, energi: 15f),
            new ItemDefinition("roti", "Roti", ItemCategory.Snack, 6000, lapar: 15f),
            new ItemDefinition("mi-instan", "Mi instan", ItemCategory.Food, 4000, lapar: 25f),
            new ItemDefinition("gorengan", "Gorengan", ItemCategory.Snack, 2000, lapar: 6f),
            new ItemDefinition(Galon, "Galon isi ulang", ItemCategory.Errand, 20000),
            new ItemDefinition("nasi-telur", "Nasi telur", ItemCategory.Food, 12000, lapar: 55f, consumeOnPurchase: true),
            new ItemDefinition("nasi-goreng", "Nasi goreng", ItemCategory.Food, 15000, lapar: 65f, consumeOnPurchase: true),
            new ItemDefinition("mi-ayam", "Mi ayam", ItemCategory.Food, 13000, lapar: 50f, consumeOnPurchase: true),
            new ItemDefinition("es-teh", "Es teh", ItemCategory.Drink, 4000, lapar: 3f, energi: 5f, consumeOnPurchase: true),
            new ItemDefinition("kopi-hitam", "Kopi hitam", ItemCategory.Drink, 5000, energi: 20f, consumeOnPurchase: true),
            new ItemDefinition("kaos-polos", "Kaos polos", ItemCategory.Clothing, 45000, wearable: true),
            new ItemDefinition("kemeja-flanel", "Kemeja flanel", ItemCategory.Clothing, 85000, wearable: true),
            new ItemDefinition(Jaket, "Jaket parka", ItemCategory.Clothing, 150000, wearable: true),
            new ItemDefinition(Hoodie, "Hoodie hitam", ItemCategory.Clothing, 120000, wearable: true),
            new ItemDefinition("sarung", "Sarung", ItemCategory.Clothing, 40000, wearable: true),
            new ItemDefinition(PaketData, "Paket data harian", ItemCategory.Data, 10000),
            new ItemDefinition("vitamin-c", "Vitamin C", ItemCategory.Medicine, 8000, energi: 20f),
            new ItemDefinition("jamu-anget", "Jamu anget", ItemCategory.Medicine, 6000, energi: 25f),
            new ItemDefinition("minyak-kayu-putih", "Minyak kayu putih", ItemCategory.Medicine, 12000, energi: 5f),
            new ItemDefinition(CatUlang, "Cat ulang motor", ItemCategory.Service, 50000),
            new ItemDefinition("kopi-kaleng", "Kopi kaleng", ItemCategory.Drink, 9000, energi: 25f),
            new ItemDefinition("nasi-kepal", "Nasi kepal", ItemCategory.Food, 10000, lapar: 35f),
        };

        /// <summary>Item ids each kind of shop stocks, in display order.</summary>
        public static string[] Stock(ShopType type)
        {
            switch (type)
            {
                case ShopType.Warung: return new[] { "air-mineral", "teh-kemasan", "kopi-sachet", "roti", "mi-instan", "gorengan", Galon };
                case ShopType.WarungMakan: return new[] { "nasi-telur", "nasi-goreng", "mi-ayam", "es-teh", "kopi-hitam" };
                case ShopType.Distro: return new[] { "kaos-polos", "kemeja-flanel", Jaket, Hoodie, "sarung" };
                case ShopType.Konter: return new[] { PaketData };
                case ShopType.Apotek: return new[] { "vitamin-c", "jamu-anget", "minyak-kayu-putih", "air-mineral" };
                case ShopType.Bengkel: return new[] { CatUlang };
                default: return new[] { "air-mineral", "teh-kemasan", "kopi-kaleng", "roti", "nasi-kepal", "mi-instan" };
            }
        }

        Dictionary<string, ItemDefinition> byId;

        public ItemDefinition Find(string id)
        {
            if (byId == null || byId.Count != (items != null ? items.Length : 0))
            {
                byId = new Dictionary<string, ItemDefinition>();
                if (items != null)
                    foreach (var item in items)
                        if (item != null && !string.IsNullOrEmpty(item.id)) byId[item.id] = item;
            }
            return id != null && byId.TryGetValue(id, out var found) ? found : null;
        }

        /// <summary>A catalog with the default items, not saved as an asset (tests, missing reference).</summary>
        public static ItemCatalog CreateDefault()
        {
            var catalog = CreateInstance<ItemCatalog>();
            catalog.hideFlags = HideFlags.DontSave;
            return catalog;
        }
    }
}
