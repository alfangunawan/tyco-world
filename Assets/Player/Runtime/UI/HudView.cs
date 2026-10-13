using UnityEngine;
using UnityEngine.UIElements;

namespace TycoWorld.Player
{
    /// <summary>Code-built UI Toolkit HUD: crosshair dot, interaction prompt, message line, stamina bar.</summary>
    public sealed class HudView
    {
        public const string CrosshairName = "crosshair";
        public const string PromptName = "prompt";
        public const string MessageName = "message";
        public const string StaminaName = "stamina";
        public const string StaminaFillName = "stamina-fill";

        readonly VisualElement crosshair, staminaBar, staminaFill;
        readonly Label prompt, message;

        public HudView(VisualElement root)
        {
            root.style.flexGrow = 1f;
            root.pickingMode = PickingMode.Ignore;

            crosshair = new VisualElement { name = CrosshairName, pickingMode = PickingMode.Ignore };
            Absolute(crosshair);
            crosshair.style.left = Length.Percent(50f);
            crosshair.style.top = Length.Percent(50f);
            crosshair.style.width = 4f;
            crosshair.style.height = 4f;
            crosshair.style.marginLeft = -2f;
            crosshair.style.marginTop = -2f;
            crosshair.style.backgroundColor = new Color(1f, 1f, 1f, 0.8f);
            crosshair.style.borderTopLeftRadius = 2f;
            crosshair.style.borderTopRightRadius = 2f;
            crosshair.style.borderBottomLeftRadius = 2f;
            crosshair.style.borderBottomRightRadius = 2f;

            prompt = CenteredLabel(PromptName, 22f);
            prompt.style.top = Length.Percent(56f);

            message = CenteredLabel(MessageName, 24f);
            message.style.bottom = Length.Percent(12f);

            staminaBar = new VisualElement { name = StaminaName, pickingMode = PickingMode.Ignore };
            Absolute(staminaBar);
            staminaBar.style.left = Length.Percent(50f);
            staminaBar.style.bottom = Length.Percent(6f);
            staminaBar.style.width = 240f;
            staminaBar.style.height = 6f;
            staminaBar.style.marginLeft = -120f;
            staminaBar.style.backgroundColor = new Color(1f, 1f, 1f, 0.15f);

            staminaFill = new VisualElement { name = StaminaFillName, pickingMode = PickingMode.Ignore };
            staminaFill.style.height = Length.Percent(100f);
            staminaFill.style.width = Length.Percent(100f);
            staminaFill.style.backgroundColor = new Color(0.86f, 0.86f, 0.78f, 0.8f);
            staminaBar.Add(staminaFill);

            root.Add(crosshair);
            root.Add(prompt);
            root.Add(message);
            root.Add(staminaBar);
        }

        public void Render(HudModel model)
        {
            crosshair.style.display = model.CrosshairVisible ? DisplayStyle.Flex : DisplayStyle.None;
            prompt.text = model.Prompt;
            prompt.style.display = model.Prompt.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            message.text = model.Message;
            message.style.display = model.Message.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            staminaBar.style.display = model.StaminaVisible ? DisplayStyle.Flex : DisplayStyle.None;
            staminaFill.style.width = Length.Percent(model.StaminaFill * 100f);
        }

        static void Absolute(VisualElement e) => e.style.position = Position.Absolute;

        static Label CenteredLabel(string name, float size)
        {
            var label = new Label { name = name, pickingMode = PickingMode.Ignore };
            Absolute(label);
            label.style.left = 0f;
            label.style.right = 0f;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.fontSize = size;
            label.style.color = new Color(0.92f, 0.92f, 0.88f, 1f);
            label.style.display = DisplayStyle.None;
            return label;
        }
    }
}
