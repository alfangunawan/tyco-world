using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TycoWorld.Gameplay
{
    /// <summary>Inline styles for the gameplay UI (code-built like the HUD and menus; same colours as MenuStyle).</summary>
    public static class GameplayStyle
    {
        public static readonly Color TextColor = new Color(0.92f, 0.92f, 0.88f, 1f);
        public static readonly Color DimText = new Color(0.7f, 0.7f, 0.66f, 1f);
        public static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.11f, 0.95f);
        public static readonly Color ButtonColor = new Color(0.18f, 0.19f, 0.22f, 1f);
        public static readonly Color AccentColor = new Color(0.55f, 0.42f, 0.16f, 1f);
        public static readonly Color GoodColor = new Color(0.45f, 0.75f, 0.4f, 1f);
        public static readonly Color WarnColor = new Color(0.9f, 0.6f, 0.2f, 1f);
        public static readonly Color BadColor = new Color(0.85f, 0.25f, 0.2f, 1f);
        public static readonly Color ShadowText = new Color(0f, 0f, 0f, 0.75f);

        public static void FullScreen(VisualElement e)
        {
            e.style.position = Position.Absolute;
            e.style.left = 0f;
            e.style.right = 0f;
            e.style.top = 0f;
            e.style.bottom = 0f;
        }

        public static VisualElement Panel(string name, float width)
        {
            var panel = new VisualElement { name = name };
            panel.style.width = width;
            panel.style.backgroundColor = PanelColor;
            Round(panel, 10f);
            Pad(panel, 18f);
            return panel;
        }

        public static Label Text(string text, float size, bool bold = false, string name = null)
        {
            var label = new Label(text) { name = name };
            label.style.fontSize = size;
            label.style.color = TextColor;
            if (bold) label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.pickingMode = PickingMode.Ignore;
            return label;
        }

        /// <summary>HUD text over the 3D world: a soft dark outline keeps it readable on bright sky and on night fog.</summary>
        public static Label HudText(string text, float size, bool bold = false, string name = null)
        {
            var label = Text(text, size, bold, name);
            label.style.unityTextOutlineColor = ShadowText;
            label.style.unityTextOutlineWidth = 1.2f;
            return label;
        }

        public static Button MakeButton(string name, string text, Action onClick, float height = 38f, float fontSize = 18f)
        {
            var button = new Button(onClick) { name = name, text = text };
            button.style.height = height;
            button.style.fontSize = fontSize;
            button.style.color = TextColor;
            button.style.backgroundColor = ButtonColor;
            button.style.borderTopWidth = 0f;
            button.style.borderBottomWidth = 0f;
            button.style.borderLeftWidth = 0f;
            button.style.borderRightWidth = 0f;
            Round(button, 6f);
            return button;
        }

        public static VisualElement Row()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            return row;
        }

        public static void Round(VisualElement e, float radius)
        {
            e.style.borderTopLeftRadius = radius;
            e.style.borderTopRightRadius = radius;
            e.style.borderBottomLeftRadius = radius;
            e.style.borderBottomRightRadius = radius;
        }

        public static void Pad(VisualElement e, float padding)
        {
            e.style.paddingLeft = padding;
            e.style.paddingRight = padding;
            e.style.paddingTop = padding;
            e.style.paddingBottom = padding;
        }

        /// <summary>A thin horizontal bar: track plus fill; returns the fill so callers can set its width.</summary>
        public static VisualElement Bar(VisualElement parent, float width, float height, out VisualElement fill)
        {
            var track = new VisualElement();
            track.style.width = width;
            track.style.height = height;
            track.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
            Round(track, height * 0.5f);
            track.pickingMode = PickingMode.Ignore;
            fill = new VisualElement();
            fill.style.height = height;
            fill.style.width = Length.Percent(100f);
            Round(fill, height * 0.5f);
            fill.pickingMode = PickingMode.Ignore;
            track.Add(fill);
            parent.Add(track);
            return track;
        }

        public static Color NeedColor(float value) => value < 15f ? BadColor : value < 35f ? WarnColor : GoodColor;
    }
}
