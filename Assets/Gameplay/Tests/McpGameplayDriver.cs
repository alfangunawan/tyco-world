using System.Globalization;
using System.Linq;
using System.Text;
using TycoWorld.Player;
using TycoWorld.Player.Tests;
using UnityEngine;

namespace TycoWorld.Gameplay.Tests
{
    /// <summary>
    /// Dev helper for Unity MCP snippets in Play mode (not a test): read the gameplay state, walk to a shop, buy, use the
    /// phone, set the clock and take screenshots with the UI (ScreenCapture) or without it (McpRender).
    /// </summary>
    public static class McpGameplayDriver
    {
        static KampungDirector Director => Object.FindAnyObjectByType<KampungDirector>();

        public static string Status()
        {
            var d = Director;
            if (d == null) return "NO DIRECTOR";
            if (!d.IsReady) return "NOT READY";
            var inv = CultureInfo.InvariantCulture;
            var s = d.State;
            var sb = new StringBuilder();
            sb.Append("day=").Append(s.Day).Append(" time=").Append(d.ClockText)
              .Append(" money=").Append(s.Wallet.Balance)
              .Append(" lapar=").Append(s.Needs.Lapar.ToString("F1", inv))
              .Append(" energi=").Append(s.Needs.Energi.ToString("F1", inv))
              .Append(" wearing=").Append(s.Wearing)
              .Append(" bag=").Append(string.Join(",", s.Bag.Stacks.Select(k => k.Key + "x" + k.Value)))
              .Append(" shops=").Append(d.Stalls.Count)
              .Append(" shopOpen=").Append(d.Shop.IsOpen)
              .Append(" phone=").Append(d.Phone.IsOpen ? d.Phone.Tab.ToString() : "closed")
              .Append(" fading=").Append(d.IsFading);
            if (d.Player != null) sb.Append(" pos=").Append(d.Player.transform.position.ToString("F1")).Append(" prompt='").Append(d.Player.Hud.Prompt).Append('\'');
            sb.Append(" toasts=[").Append(string.Join(" | ", d.Hud.Toasts)).Append(']');
            return sb.ToString();
        }

        /// <summary>Stands the player 1.8 m in front of a shop counter, facing it; index -1 = the shop nearest the kos.</summary>
        public static string GoToShop(int index)
        {
            var d = Director;
            if (d == null || !d.IsReady) return "NOT READY";
            ShopStall stall;
            if (index < 0)
            {
                var kos = d.Data.spots.FirstOrDefault(s => s.kind == SpotKind.Kos);
                var at = kos != null ? kos.position : Vector3.zero;
                stall = d.Stalls.OrderBy(x => (x.transform.position - at).sqrMagnitude).First();
            }
            else stall = d.Stalls[Mathf.Clamp(index, 0, d.Stalls.Count - 1)];
            var site = stall.Site;
            var forward = Quaternion.Euler(0f, site.yaw, 0f) * Vector3.forward;
            var feet = site.counter + forward * (site.slabLength + 0.6f);
            feet.y = GroundY(feet);
            d.Player.Teleport(feet, site.yaw + 180f);
            McpPlayerDriver.Install();
            McpPlayerDriver.Face(site.counter.x, site.counter.y + 0.9f, site.counter.z);
            return "at " + site.name + " (" + site.type + ") " + feet.ToString("F1");
        }

        static float GroundY(Vector3 p)
        {
            float best = 0f;
            foreach (var hit in Physics.RaycastAll(p + Vector3.up * 50f, Vector3.down, 60f))
                if (hit.point.y < 3f && hit.point.y > best) best = hit.point.y;
            return best;
        }

        public static string Buy(string itemId)
        {
            var d = Director;
            if (d == null || !d.Shop.IsOpen) return "SHOP NOT OPEN";
            bool ok = d.Shop.ClickBuy(itemId);
            return (ok ? "clicked " : "no button ") + itemId + ": " + d.Shop.MessageText + " money=" + d.State.Wallet.Balance;
        }

        public static string CloseShop()
        {
            var d = Director;
            if (d == null) return "NO DIRECTOR";
            d.CloseShop();
            return "closed";
        }

        public static string Phone(string tab)
        {
            var d = Director;
            if (d == null || !d.IsReady) return "NOT READY";
            if (string.IsNullOrEmpty(tab))
            {
                if (d.Phone.IsOpen) d.TogglePhone();
                return "phone closed";
            }
            if (!d.Phone.IsOpen) d.TogglePhone();
            d.Phone.Select((PhoneTab)System.Enum.Parse(typeof(PhoneTab), tab));
            return "phone " + d.Phone.Tab;
        }

        public static string PhoneClick(string button)
        {
            var d = Director;
            return d != null && d.Phone.Click(button) ? "clicked " + button : "no button " + button;
        }

        public static string SetTime(float hours)
        {
            var d = Director;
            if (d == null || !d.IsReady) return "NOT READY";
            d.JumpClock(hours);
            return "time " + d.ClockText + " day " + d.State.Day;
        }

        public static string SetNeeds(float lapar, float energi)
        {
            var d = Director;
            if (d == null) return "NO DIRECTOR";
            d.State.Needs.Set(lapar, energi);
            return "needs set";
        }

        /// <summary>The Game view with the UI, written at the end of the frame.</summary>
        public static string Screen(string path)
        {
            ScreenCapture.CaptureScreenshot(path);
            return "capturing " + path;
        }

        /// <summary>The main camera only (no UI), rendered at once.</summary>
        public static string World(string path) => McpRender.SavePng(Camera.main, path, 1280, 720);

        public static string ResetSave()
        {
            PlayerPrefs.DeleteKey(PlayerPrefsSaveBackend.DefaultKey);
            PlayerPrefs.Save();
            return "save cleared";
        }
    }
}
