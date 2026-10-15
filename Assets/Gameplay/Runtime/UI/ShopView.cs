using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// The shop window (spec #6 §2): name and hours, your money, one row per item with its price, effect and a Beli
    /// button, a message line and Tutup. Buttons are named so tests and MCP can click them ("buy-roti", "shop-close").
    /// </summary>
    public sealed class ShopView
    {
        public const string RootName = "shop-view";
        public const string CloseName = "shop-close";
        public const string BuyPrefix = "buy-";
        public const string CloseText = "Tutup [E]";

        readonly VisualElement overlay, list;
        readonly Label title, subtitle, money, message;
        readonly Dictionary<string, Button> buyButtons = new Dictionary<string, Button>();
        readonly List<ItemDefinition> stock = new List<ItemDefinition>();

        public event Action<ItemDefinition> BuyRequested;
        public event Action CloseRequested;

        public bool IsOpen { get; private set; }
        public ShopSite Site { get; private set; }
        public string MessageText => message.text;
        public IReadOnlyList<ItemDefinition> Stock => stock;

        public ShopView(VisualElement parent)
        {
            overlay = new VisualElement { name = RootName };
            GameplayStyle.FullScreen(overlay);
            overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.35f);
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;
            parent.Add(overlay);
            var panel = GameplayStyle.Panel("shop-panel", 560f);
            overlay.Add(panel);
            title = GameplayStyle.Text("", 28f, true);
            panel.Add(title);
            subtitle = GameplayStyle.Text("", 16f);
            subtitle.style.color = GameplayStyle.DimText;
            panel.Add(subtitle);
            money = GameplayStyle.Text("", 20f, true);
            money.style.color = new Color(0.7f, 0.95f, 0.6f, 1f);
            money.style.marginTop = 8f;
            money.style.marginBottom = 8f;
            panel.Add(money);
            list = new ScrollView();
            list.style.maxHeight = 360f;
            panel.Add(list);
            message = GameplayStyle.Text("", 17f);
            message.style.marginTop = 10f;
            message.style.minHeight = 24f;
            message.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(message);
            var close = GameplayStyle.MakeButton(CloseName, CloseText, () => CloseRequested?.Invoke());
            close.style.marginTop = 8f;
            panel.Add(close);
            overlay.style.display = DisplayStyle.None;
        }

        public void Open(ShopSite site, IEnumerable<ItemDefinition> items, GameState state)
        {
            Site = site;
            title.text = site.name;
            subtitle.text = TypeName(site.type) + " · " + OpeningHours.Describe(site.type);
            stock.Clear();
            stock.AddRange(items);
            list.Clear();
            buyButtons.Clear();
            foreach (var item in stock) list.Add(Row(item));
            message.text = "";
            IsOpen = true;
            overlay.style.display = DisplayStyle.Flex;
            Refresh(state);
        }

        VisualElement Row(ItemDefinition item)
        {
            var row = GameplayStyle.Row();
            row.style.marginTop = 4f;
            row.style.marginBottom = 4f;
            var text = new VisualElement();
            text.style.flexGrow = 1f;
            text.Add(GameplayStyle.Text(item.displayName, 19f));
            var effect = GameplayStyle.Text(Effect(item), 14f);
            effect.style.color = GameplayStyle.DimText;
            text.Add(effect);
            row.Add(text);
            var price = GameplayStyle.Text(RupiahFormat.Format(item.price), 18f, true);
            price.style.marginRight = 12f;
            row.Add(price);
            var buy = GameplayStyle.MakeButton(BuyPrefix + item.id, "Beli", () => BuyRequested?.Invoke(item), 34f, 16f);
            buy.style.width = 96f;
            row.Add(buy);
            buyButtons[item.id] = buy;
            return row;
        }

        public void Refresh(GameState state)
        {
            money.text = "Uangmu: " + RupiahFormat.Format(state.Wallet.Balance);
            foreach (var item in stock)
            {
                if (!buyButtons.TryGetValue(item.id, out var button)) continue;
                bool owned = item.wearable && state.Owns(item.id);
                button.text = owned ? (state.Wearing == item.id ? "Dipakai" : "Pakai") : "Beli";
                button.SetEnabled(owned ? state.Wearing != item.id : state.Wallet.CanAfford(item.price));
            }
        }

        public void SetMessage(string text, bool good)
        {
            message.text = text ?? "";
            message.style.color = good ? GameplayStyle.GoodColor : GameplayStyle.WarnColor;
        }

        public void Close()
        {
            IsOpen = false;
            Site = null;
            overlay.style.display = DisplayStyle.None;
        }

        /// <summary>Clicks a Beli button by item id (tests and MCP); false when there is no such enabled button.</summary>
        public bool ClickBuy(string id)
        {
            if (!IsOpen || !buyButtons.TryGetValue(id, out var button) || !button.enabledSelf) return false;
            foreach (var item in stock)
                if (item.id == id)
                {
                    BuyRequested?.Invoke(item);
                    return true;
                }
            return false;
        }

        static string Effect(ItemDefinition item)
        {
            var parts = new List<string>();
            if (item.lapar > 0f) parts.Add("Lapar +" + item.lapar.ToString("0", CultureInfo.InvariantCulture));
            if (item.energi > 0f) parts.Add("Energi +" + item.energi.ToString("0", CultureInfo.InvariantCulture));
            if (item.consumeOnPurchase) parts.Add("makan di tempat");
            switch (item.category)
            {
                case ItemCategory.Clothing:
                    parts.Add(item.id == ItemCatalog.Jaket ? "hangat di malam hari" : item.id == ItemCatalog.Hoodie ? "susah dikenali warga" : "baju");
                    break;
                case ItemCategory.Data: parts.Add("aplikasi Kerjaan aktif sampai tengah malam"); break;
                case ItemCategory.Service: parts.Add("motor/mobil yang kamu naiki ganti warna, warga lupa"); break;
                case ItemCategory.Errand: parts.Add("untuk diantar"); break;
            }
            return string.Join(" · ", parts);
        }

        public static string TypeName(ShopType type)
        {
            switch (type)
            {
                case ShopType.Warung: return "Toko kelontong";
                case ShopType.WarungMakan: return "Warung makan";
                case ShopType.Distro: return "Toko baju";
                case ShopType.Konter: return "Konter pulsa";
                case ShopType.Apotek: return "Apotek";
                case ShopType.Bengkel: return "Bengkel";
                default: return "Minimarket";
            }
        }
    }
}
