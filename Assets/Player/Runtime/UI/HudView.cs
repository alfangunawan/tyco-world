using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace TycoWorld.Player
{
    /// <summary>Code-built UI Toolkit HUD: crosshair dot, interaction prompt, message line, stamina bar, debug clock.</summary>
    public sealed class HudView
    {
        public const string CrosshairName = "crosshair";
        public const string PromptName = "prompt";
        public const string MessageName = "message";
        public const string StaminaName = "stamina";
        public const string StaminaFillName = "stamina-fill";
        public const string ClockName = "clock";
        public const string VehicleName = "vehicle";
        public const string SpeedName = "vehicle-speed";
        public const string UnitName = "vehicle-unit";
        public const string LampName = "vehicle-lamp";
        public const string ExitName = "vehicle-exit";
        public const string UnitText = "km/j";
        public const string LampText = "LAMPU";
        public const string ExitText = "[E] Turun";

        static readonly Color LampOnColor = new Color(1f, 0.92f, 0.55f, 1f);
        static readonly Color LampOffColor = new Color(0.92f, 0.92f, 0.88f, 0.3f);

        readonly VisualElement crosshair, staminaBar, staminaFill, vehicle;
        int shownSpeed = -1;
        readonly Label prompt, message, clock, speed, lamp, exit;

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

            clock = new Label { name = ClockName, pickingMode = PickingMode.Ignore };
            Absolute(clock);
            clock.style.top = 16f;
            clock.style.right = 24f;
            clock.style.unityTextAlign = TextAnchor.MiddleRight;
            clock.style.fontSize = 22f;
            clock.style.color = new Color(0.92f, 0.92f, 0.88f, 0.9f);
            clock.style.display = DisplayStyle.None;

            vehicle = new VisualElement { name = VehicleName, pickingMode = PickingMode.Ignore };
            Absolute(vehicle);
            vehicle.style.right = 36f;
            vehicle.style.bottom = 40f;
            vehicle.style.alignItems = Align.FlexEnd;
            vehicle.style.display = DisplayStyle.None;
            speed = VehicleLabel(SpeedName, "0", 56f);
            speed.style.unityFontStyleAndWeight = FontStyle.Bold;
            lamp = VehicleLabel(LampName, LampText, 18f);
            exit = VehicleLabel(ExitName, ExitText, 20f);
            vehicle.Add(speed);
            vehicle.Add(VehicleLabel(UnitName, UnitText, 20f));
            vehicle.Add(lamp);
            vehicle.Add(exit);

            root.Add(crosshair);
            root.Add(prompt);
            root.Add(message);
            root.Add(staminaBar);
            root.Add(clock);
            root.Add(vehicle);
        }

        public void Render(HudModel model)
        {
            crosshair.style.display = model.CrosshairVisible ? DisplayStyle.Flex : DisplayStyle.None;
            prompt.text = model.Prompt;
            prompt.style.display = model.Prompt.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            message.text = model.Message;
            message.style.display = model.Message.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            staminaBar.style.display = model.StaminaVisible && !model.VehicleVisible ? DisplayStyle.Flex : DisplayStyle.None;
            vehicle.style.display = model.VehicleVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (model.VehicleVisible)
            {
                if (shownSpeed != model.VehicleSpeedKmh)
                {
                    shownSpeed = model.VehicleSpeedKmh;
                    speed.text = shownSpeed.ToString(CultureInfo.InvariantCulture);
                }
                lamp.style.color = model.VehicleHeadlight ? LampOnColor : LampOffColor;
                exit.style.display = model.VehicleCanExit ? DisplayStyle.Flex : DisplayStyle.None;
            }
            staminaFill.style.width = Length.Percent(model.StaminaFill * 100f);
            clock.text = model.Clock;
            clock.style.display = model.ClockVisible && model.Clock.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static Label VehicleLabel(string name, string text, float size)
        {
            var label = new Label(text) { name = name, pickingMode = PickingMode.Ignore };
            label.style.fontSize = size;
            label.style.color = new Color(0.92f, 0.92f, 0.88f, 1f);
            label.style.unityTextAlign = TextAnchor.MiddleRight;
            return label;
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
