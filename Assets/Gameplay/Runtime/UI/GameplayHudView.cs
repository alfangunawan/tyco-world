using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// The gameplay HUD layer (spec #6 §4): money and needs at the top left, a queue of toasts at the bottom left (the
    /// player HUD has one message slot), the mission objective at the top, a hold-progress bar, wanted stars and a fade.
    /// </summary>
    public sealed class GameplayHudView
    {
        public const string MoneyName = "gameplay-money";
        public const string ObjectiveName = "gameplay-objective";
        public const string ToastsName = "gameplay-toasts";
        public const float ToastSeconds = 4f;
        const int MaxToasts = 4;

        readonly VisualElement root, toastStack, progressTrack, progressFill, fade, laparFill, energiFill;
        readonly Label money, objective, progressLabel, stars, laparLabel, energiLabel;
        readonly List<(Label label, float until)> toasts = new List<(Label label, float until)>();
        float clock;

        public GameplayHudView(VisualElement parent)
        {
            root = new VisualElement { name = "gameplay-hud", pickingMode = PickingMode.Ignore };
            GameplayStyle.FullScreen(root);
            parent.Add(root);

            // Top right under the clock (the FPS counter owns the top left).
            var corner = new VisualElement { pickingMode = PickingMode.Ignore };
            corner.style.position = Position.Absolute;
            corner.style.right = 24f;
            corner.style.top = 48f;
            corner.style.alignItems = Align.FlexEnd;
            root.Add(corner);
            money = GameplayStyle.HudText(RupiahFormat.Format(0), 26f, true, MoneyName);
            money.style.color = new Color(0.7f, 0.95f, 0.6f, 1f);
            corner.Add(money);
            laparLabel = NeedRow(corner, "Lapar", out laparFill);
            energiLabel = NeedRow(corner, "Energi", out energiFill);
            stars = GameplayStyle.HudText("", 24f, true, "gameplay-stars");
            stars.style.color = GameplayStyle.BadColor;
            stars.style.marginTop = 6f;
            corner.Add(stars);

            objective = GameplayStyle.HudText("", 20f, false, ObjectiveName);
            objective.style.position = Position.Absolute;
            objective.style.top = 22f;
            objective.style.left = 0f;
            objective.style.right = 0f;
            objective.style.unityTextAlign = TextAnchor.UpperCenter;
            root.Add(objective);

            toastStack = new VisualElement { name = ToastsName, pickingMode = PickingMode.Ignore };
            toastStack.style.position = Position.Absolute;
            toastStack.style.left = 24f;
            toastStack.style.bottom = 120f;
            toastStack.style.flexDirection = FlexDirection.ColumnReverse;
            root.Add(toastStack);

            var progressBox = new VisualElement { pickingMode = PickingMode.Ignore };
            progressBox.style.position = Position.Absolute;
            progressBox.style.left = Length.Percent(50f);
            progressBox.style.top = Length.Percent(62f);
            progressBox.style.translate = new Translate(Length.Percent(-50f), 0f);
            progressBox.style.alignItems = Align.Center;
            root.Add(progressBox);
            progressLabel = GameplayStyle.HudText("", 18f);
            progressBox.Add(progressLabel);
            progressTrack = GameplayStyle.Bar(progressBox, 260f, 10f, out progressFill);
            progressFill.style.backgroundColor = GameplayStyle.AccentColor;
            progressTrack.style.marginTop = 6f;
            progressBox.style.display = DisplayStyle.None;
            progressTrack.userData = progressBox;

            fade = new VisualElement { name = "gameplay-fade", pickingMode = PickingMode.Ignore };
            GameplayStyle.FullScreen(fade);
            fade.style.backgroundColor = Color.black;
            fade.style.opacity = 0f;
            root.Add(fade);
        }

        static Label NeedRow(VisualElement parent, string name, out VisualElement fill)
        {
            var row = GameplayStyle.Row();
            row.style.marginTop = 6f;
            row.pickingMode = PickingMode.Ignore;
            var label = GameplayStyle.HudText(name, 15f);
            label.style.width = 56f;
            row.Add(label);
            GameplayStyle.Bar(row, 130f, 8f, out fill);
            parent.Add(row);
            return label;
        }

        public bool Visible
        {
            get => root.style.display != DisplayStyle.None;
            set => root.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public IReadOnlyList<string> Toasts
        {
            get
            {
                var list = new List<string>();
                foreach (var t in toasts) list.Add(t.label.text);
                return list;
            }
        }

        public string MoneyText => money.text;
        public string ObjectiveText => objective.text;

        public void Toast(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var label = GameplayStyle.HudText(text, 18f);
            label.style.marginTop = 4f;
            label.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
            GameplayStyle.Pad(label, 6f);
            GameplayStyle.Round(label, 4f);
            toastStack.Add(label);
            toasts.Add((label, clock + ToastSeconds));
            while (toasts.Count > MaxToasts)
            {
                toasts[0].label.RemoveFromHierarchy();
                toasts.RemoveAt(0);
            }
        }

        public void SetObjective(string text) => objective.text = text ?? "";

        public void SetProgress(string text, float amount01)
        {
            var box = (VisualElement)progressTrack.userData;
            bool show = amount01 >= 0f;
            box.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;
            progressLabel.text = text ?? "";
            progressFill.style.width = Length.Percent(Mathf.Clamp01(amount01) * 100f);
        }

        /// <summary>Heat 1..3 as "MALING! [**-]"; ASCII so every font has the glyphs.</summary>
        public void SetStars(int count) => stars.text = count > 0 ? "MALING! [" + new string('*', count) + new string('-', Mathf.Max(0, 3 - count)) + "]" : "";

        public string StarsText => stars.text;

        public void SetFade(float alpha) => fade.style.opacity = Mathf.Clamp01(alpha);

        public void Render(GameState state, float dt)
        {
            clock += dt;
            money.text = RupiahFormat.Format(state.Wallet.Balance);
            float lapar = state.Needs.Lapar, energi = state.Needs.Energi;
            laparFill.style.width = Length.Percent(lapar);
            laparFill.style.backgroundColor = GameplayStyle.NeedColor(lapar);
            energiFill.style.width = Length.Percent(energi);
            energiFill.style.backgroundColor = GameplayStyle.NeedColor(energi);
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                float left = toasts[i].until - clock;
                if (left <= 0f)
                {
                    toasts[i].label.RemoveFromHierarchy();
                    toasts.RemoveAt(i);
                }
                else toasts[i].label.style.opacity = Mathf.Clamp01(left / 0.6f);
            }
        }
    }
}
