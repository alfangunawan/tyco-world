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

        /// <summary>Who is out: active count, states, nearest distance, the figure.</summary>
        public static string People()
        {
            var d = Director;
            var people = d != null ? d.GetSystem<PedestrianSystem>() : null;
            if (people == null) return "no pedestrian system";
            var player = d.Player.transform.position;
            float nearest = float.PositiveInfinity;
            var states = new System.Collections.Generic.Dictionary<PedestrianState, int>();
            foreach (var p in people.Pool)
            {
                if (!p.Active) continue;
                nearest = Mathf.Min(nearest, Vector3.Distance(p.Position, player));
                states.TryGetValue(p.State, out int n);
                states[p.State] = n + 1;
            }
            var text = new System.Text.StringBuilder();
            text.Append("time=").Append(d.ClockText).Append(" active=").Append(people.ActiveCount)
                .Append(" target=").Append(PedestrianSchedule.TargetCount(d.Hours))
                .Append(" nearest=").Append(nearest.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Append(" sosok=").Append(people.Sosok != null && people.Sosok.Active ? people.Sosok.Position.ToString("F1") : "-");
            foreach (var kv in states) text.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
            return text.ToString();
        }

        static MissionSystem Jobs => Director != null ? Director.GetSystem<MissionSystem>() : null;

        /// <summary>The Kerjaan list and the running job: objective, target distance, compass, time left.</summary>
        public static string Mission()
        {
            var jobs = Jobs;
            if (jobs == null) return "no mission system";
            var text = new System.Text.StringBuilder();
            foreach (var l in jobs.Listings())
                text.Append(l.Id).Append(l.Active ? "[active]" : l.Available ? "[ok]" : "[-]").Append(' ');
            var m = jobs.Active;
            if (m == null) return text.Append("| idle hud='").Append(Director.Hud.ObjectiveText).Append("'").ToString();
            var target = m.Target;
            text.Append("| ").Append(m.Id).Append(" step ").Append(m.StepIndex + 1).Append('/').Append(m.StepCount)
                .Append(" '").Append(m.Objective).Append("' reward=").Append(m.Reward)
                .Append(" target=").Append(target.HasValue ? target.Value.ToString("F1") : "-")
                .Append(" dist=").Append(target.HasValue ? jobs.Context.Distance(target.Value).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) : "-")
                .Append(" left=").Append(m.TimeLeft.ToString("F0", System.Globalization.CultureInfo.InvariantCulture))
                .Append(" compass='").Append(Director.Hud.CompassText).Append("' beacon=").Append(jobs.Beacon != null && jobs.Beacon.activeSelf);
            return text.ToString();
        }

        public static string StartJob(string id)
        {
            var jobs = Jobs;
            if (jobs == null) return "no mission system";
            var m = jobs.Find(id);
            return m != null && jobs.StartJob(m) ? "started " + id + ": " + m.Objective : "could not start " + id;
        }

        /// <summary>Walks (teleports) the player to <paramref name="metres"/> short of the current target, facing it.</summary>
        public static string GoToTarget(float metres = 2f)
        {
            var jobs = Jobs;
            var m = jobs != null ? jobs.Active : null;
            var target = m != null ? m.Target : null;
            if (!target.HasValue) return "no target";
            var player = Director.Player.transform.position;
            var dir = target.Value - player;
            dir.y = 0f;
            var at = target.Value - (dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward) * metres;
            at.y = jobs.Context.Ground(at) + 0.05f;
            Director.Player.Teleport(at, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg);
            return "at " + at.ToString("F1") + " facing target";
        }

        /// <summary>Uses the nearest mission E target within 4 m (as if E was pressed while looking at it).</summary>
        public static string UseNearest()
        {
            var d = Director;
            if (d == null) return "no director";
            var player = d.Player.transform.position;
            ActionInteractable best = null;
            foreach (var a in d.ObjectsRoot.GetComponentsInChildren<ActionInteractable>())
                if (best == null || (a.transform.position - player).sqrMagnitude < (best.transform.position - player).sqrMagnitude) best = a;
            if (best == null || Vector3.Distance(best.transform.position, player) > 4f) return "nothing to use nearby";
            var context = new TycoWorld.Player.InteractionContext(d.Player, d.Player.Hud, d.Player.View.position);
            string prompt = best.GetPrompt(context);
            return best.Interact(context) ? "used " + best.name + " (" + prompt + ")" : "cannot use " + best.name + " (" + prompt + ")";
        }

        public static string ForceSosok()
        {
            var people = Director != null ? Director.GetSystem<PedestrianSystem>() : null;
            return people != null && people.ForceSosok() ? "sosok at " + people.Sosok.Position.ToString("F1") : "no spot ahead";
        }

        /// <summary>Faces the player toward the nearest active passer-by (for screenshots and greeting).</summary>
        public static string FaceNearestPerson()
        {
            var d = Director;
            var people = d != null ? d.GetSystem<PedestrianSystem>() : null;
            if (people == null) return "no pedestrian system";
            var player = d.Player.transform.position;
            Pedestrian best = null;
            foreach (var p in people.Pool)
                if (p.Active && (best == null || (p.Position - player).sqrMagnitude < (best.Position - player).sqrMagnitude)) best = p;
            if (best == null) return "nobody out";
            var to = best.Position - player;
            d.Player.Teleport(player, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
            return "facing " + best.Root.name + " at " + to.magnitude.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " m";
        }

        public static string ResetSave()
        {
            PlayerPrefs.DeleteKey(PlayerPrefsSaveBackend.DefaultKey);
            PlayerPrefs.Save();
            return "save cleared";
        }
    }
}
