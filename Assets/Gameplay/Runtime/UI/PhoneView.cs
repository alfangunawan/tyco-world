using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TycoWorld.Gameplay
{
    public enum PhoneTab
    {
        Dompet,
        Tas,
        Kerjaan,
        Peta
    }

    /// <summary>A job on the phone's Kerjaan app (spec #6 §5).</summary>
    public struct JobListing
    {
        public string Id, Title, Detail, Reward;
        public bool Active, Available;
    }

    /// <summary>
    /// The phone (Tab; spec #6 §2): Dompet (money, day, data package, clothes), Tas (eat or drink from the bag, change
    /// clothes), Kerjaan (side jobs) and Peta. Buttons are named for tests and MCP ("phone-tab-Tas", "use-roti").
    /// </summary>
    public sealed class PhoneView
    {
        public const string RootName = "phone-view";
        public const string TabPrefix = "phone-tab-";
        public const string UsePrefix = "use-";
        public const string WearPrefix = "wear-";
        public const string JobPrefix = "job-";

        readonly VisualElement overlay, body;
        readonly Label header;
        readonly Dictionary<PhoneTab, Button> tabs = new Dictionary<PhoneTab, Button>();
        readonly Dictionary<string, (Button button, Action action)> buttons = new Dictionary<string, (Button button, Action action)>();
        readonly MapView map = new MapView();
        int lastSignature = -1;

        public event Action<ItemDefinition> UseRequested;
        public event Action<string> WearRequested;
        public event Action<string> JobRequested;
        public event Action CloseRequested;

        public bool IsOpen { get; private set; }
        public PhoneTab Tab { get; private set; } = PhoneTab.Dompet;
        public MapView Map => map;

        public PhoneView(VisualElement parent)
        {
            overlay = new VisualElement { name = RootName };
            GameplayStyle.FullScreen(overlay);
            overlay.style.alignItems = Align.FlexEnd;
            overlay.style.justifyContent = Justify.Center;
            overlay.style.paddingRight = 60f;
            parent.Add(overlay);
            var phone = GameplayStyle.Panel("phone", 400f);
            phone.style.height = 620f;
            phone.style.backgroundColor = new Color(0.05f, 0.05f, 0.06f, 0.97f);
            phone.style.borderTopWidth = phone.style.borderBottomWidth = phone.style.borderLeftWidth = phone.style.borderRightWidth = 4f;
            phone.style.borderTopColor = phone.style.borderBottomColor = phone.style.borderLeftColor = phone.style.borderRightColor = new Color(0.25f, 0.25f, 0.28f);
            overlay.Add(phone);
            header = GameplayStyle.Text("", 15f);
            header.style.color = GameplayStyle.DimText;
            header.style.unityTextAlign = TextAnchor.MiddleCenter;
            phone.Add(header);
            var tabRow = GameplayStyle.Row();
            tabRow.style.marginTop = 8f;
            tabRow.style.marginBottom = 8f;
            foreach (PhoneTab t in Enum.GetValues(typeof(PhoneTab)))
            {
                var tab = t;
                var b = GameplayStyle.MakeButton(TabPrefix + tab, tab.ToString(), () => Select(tab), 32f, 15f);
                b.style.flexGrow = 1f;
                b.style.marginLeft = 2f;
                b.style.marginRight = 2f;
                tabRow.Add(b);
                tabs[tab] = b;
            }
            phone.Add(tabRow);
            body = new ScrollView();
            body.style.flexGrow = 1f;
            phone.Add(body);
            var close = GameplayStyle.MakeButton("phone-close", "Tutup [Tab]", () => CloseRequested?.Invoke(), 32f, 15f);
            close.style.marginTop = 8f;
            phone.Add(close);
            overlay.style.display = DisplayStyle.None;
        }

        public void Open()
        {
            IsOpen = true;
            lastSignature = -1;
            overlay.style.display = DisplayStyle.Flex;
        }

        public void Close()
        {
            IsOpen = false;
            overlay.style.display = DisplayStyle.None;
        }

        public void Select(PhoneTab tab)
        {
            Tab = tab;
            lastSignature = -1;
            foreach (var kv in tabs) kv.Value.style.backgroundColor = kv.Key == tab ? GameplayStyle.AccentColor : GameplayStyle.ButtonColor;
        }

        /// <summary>Rebuilds the open tab when its content changed (a cheap signature keeps buttons clickable between frames).</summary>
        public void Render(GameState state, ItemCatalog catalog, string clock, IReadOnlyList<JobListing> jobs, KampungData data, Vector2 player,
            float yaw, IEnumerable<MapMarker> markers)
        {
            if (!IsOpen) return;
            header.text = clock + "  ·  Hari " + state.Day + "  ·  " + (state.HasData ? "4G" : "tanpa paket data");
            if (Tab == PhoneTab.Peta)
            {
                if (lastSignature != -2)
                {
                    body.Clear();
                    buttons.Clear();
                    var zoomRow = GameplayStyle.Row();
                    var minus = GameplayStyle.MakeButton("map-zoom-out", "−", map.ZoomOut, 30f, 18f);
                    var plus = GameplayStyle.MakeButton("map-zoom-in", "+", map.ZoomIn, 30f, 18f);
                    minus.style.width = plus.style.width = 44f;
                    zoomRow.Add(minus);
                    zoomRow.Add(plus);
                    var legend = GameplayStyle.Text("  ● toko  ● kos  ● tujuan", 13f);
                    legend.style.color = GameplayStyle.DimText;
                    zoomRow.Add(legend);
                    body.Add(zoomRow);
                    map.style.height = 470f;
                    map.style.marginTop = 6f;
                    body.Add(map);
                    lastSignature = -2;
                }
                map.Show(data, player, yaw, markers);
                return;
            }
            int signature = Signature(state, jobs);
            if (signature == lastSignature) return;
            lastSignature = signature;
            body.Clear();
            buttons.Clear();
            switch (Tab)
            {
                case PhoneTab.Dompet: Wallet(state, catalog); break;
                case PhoneTab.Tas: Bag(state, catalog); break;
                case PhoneTab.Kerjaan: Jobs(state, jobs); break;
            }
        }

        int Signature(GameState state, IReadOnlyList<JobListing> jobs)
        {
            int h = (int)Tab * 7919 + (int)(state.Wallet.Balance % 100000) + state.Day * 31 + (state.HasData ? 1 : 0) + state.Wearing.GetHashCode();
            foreach (var s in state.Bag.Stacks) h = h * 31 + s.Key.GetHashCode() + s.Value;
            if (jobs != null)
                foreach (var j in jobs) h = h * 31 + j.Id.GetHashCode() + (j.Active ? 3 : 0) + (j.Available ? 5 : 0);
            return h;
        }

        void Wallet(GameState state, ItemCatalog catalog)
        {
            body.Add(Line("Saldo", RupiahFormat.Format(state.Wallet.Balance), 24f));
            body.Add(Line("Hari ke", state.Day.ToString(), 18f));
            body.Add(Line("Paket data", state.HasData ? "aktif hari ini" : "habis — beli di konter", 16f));
            var wearing = catalog != null ? catalog.Find(state.Wearing) : null;
            body.Add(Line("Baju", wearing != null ? wearing.displayName : "kaos lama", 16f));
            var tip = GameplayStyle.Text("Uang jajan Rp 25.000 masuk tiap pagi setelah tidur di kos. Cari tambahan di aplikasi Kerjaan.", 14f);
            tip.style.whiteSpace = WhiteSpace.Normal;
            tip.style.color = GameplayStyle.DimText;
            tip.style.marginTop = 14f;
            body.Add(tip);
        }

        void Bag(GameState state, ItemCatalog catalog)
        {
            bool any = false;
            foreach (var stack in state.Bag.Stacks)
            {
                var item = catalog != null ? catalog.Find(stack.Key) : null;
                if (item == null) continue;
                any = true;
                var row = GameplayStyle.Row();
                row.style.marginTop = 4f;
                var name = GameplayStyle.Text(item.displayName + " ×" + stack.Value, 17f);
                name.style.flexGrow = 1f;
                row.Add(name);
                if (item.IsConsumable)
                {
                    var use = Register(UsePrefix + item.id, item.category == ItemCategory.Drink ? "Minum" : "Makan", () => UseRequested?.Invoke(item));
                    use.style.width = 90f;
                    row.Add(use);
                }
                body.Add(row);
            }
            if (!any) body.Add(Dim("Tas kosong. Belanja di warung."));
            var wardrobeTitle = GameplayStyle.Text("Lemari baju", 18f, true);
            wardrobeTitle.style.marginTop = 16f;
            body.Add(wardrobeTitle);
            AddWear(state, "", "Kaos lama");
            foreach (var id in state.Wardrobe)
            {
                var item = catalog != null ? catalog.Find(id) : null;
                AddWear(state, id, item != null ? item.displayName : id);
            }
        }

        void AddWear(GameState state, string id, string label)
        {
            var row = GameplayStyle.Row();
            row.style.marginTop = 4f;
            var name = GameplayStyle.Text(label, 16f);
            name.style.flexGrow = 1f;
            row.Add(name);
            bool on = state.Wearing == id;
            var b = Register(WearPrefix + (id == "" ? "none" : id), on ? "Dipakai" : "Pakai", () => WearRequested?.Invoke(id));
            b.style.width = 90f;
            b.SetEnabled(!on);
            row.Add(b);
            body.Add(row);
        }

        void Jobs(GameState state, IReadOnlyList<JobListing> jobs)
        {
            if (!state.HasData)
            {
                body.Add(Dim("Butuh paket data. Beli Paket data harian di konter pulsa atau minimarket."));
                // A job already running can still be cancelled.
                var running = new List<JobListing>();
                if (jobs != null)
                    foreach (var job in jobs)
                        if (job.Active) running.Add(job);
                if (running.Count == 0) return;
                jobs = running;
            }
            if (jobs == null || jobs.Count == 0)
            {
                body.Add(Dim("Belum ada kerjaan. Coba lagi nanti."));
                return;
            }
            foreach (var job in jobs)
            {
                var card = new VisualElement();
                card.style.backgroundColor = new Color(1f, 1f, 1f, 0.05f);
                GameplayStyle.Round(card, 6f);
                GameplayStyle.Pad(card, 10f);
                card.style.marginBottom = 8f;
                card.Add(GameplayStyle.Text(job.Title, 18f, true));
                var detail = GameplayStyle.Text(job.Detail, 14f);
                detail.style.whiteSpace = WhiteSpace.Normal;
                detail.style.color = GameplayStyle.DimText;
                card.Add(detail);
                var row = GameplayStyle.Row();
                var reward = GameplayStyle.Text(job.Reward, 16f, true);
                reward.style.flexGrow = 1f;
                reward.style.color = new Color(0.7f, 0.95f, 0.6f, 1f);
                row.Add(reward);
                string jobId = job.Id;
                var b = Register(JobPrefix + jobId, job.Active ? "Batalkan" : "Ambil", () => JobRequested?.Invoke(jobId));
                b.style.width = 100f;
                b.SetEnabled(job.Active || job.Available);
                row.Add(b);
                card.Add(row);
                body.Add(card);
            }
        }

        static VisualElement Line(string label, string value, float size)
        {
            var row = GameplayStyle.Row();
            row.style.marginTop = 6f;
            var l = GameplayStyle.Text(label, 15f);
            l.style.color = GameplayStyle.DimText;
            l.style.width = 110f;
            row.Add(l);
            row.Add(GameplayStyle.Text(value, size, true));
            return row;
        }

        static Label Dim(string text)
        {
            var label = GameplayStyle.Text(text, 15f);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.color = GameplayStyle.DimText;
            label.style.marginTop = 6f;
            return label;
        }

        Button Register(string buttonName, string text, Action action)
        {
            var b = GameplayStyle.MakeButton(buttonName, text, action, 30f, 15f);
            buttons[buttonName] = (b, action);
            return b;
        }

        /// <summary>Clicks a named button of the open tab (tests and MCP).</summary>
        public bool Click(string buttonName)
        {
            if (!IsOpen || !buttons.TryGetValue(buttonName, out var entry) || !entry.button.enabledSelf) return false;
            entry.action();
            return true;
        }
    }
}
