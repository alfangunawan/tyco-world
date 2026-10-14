using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TycoWorld.GameSettings
{
    /// <summary>Inline styles shared by the code-built menus (no USS, like the HUD).</summary>
    public static class MenuStyle
    {
        public static readonly Color TextColor = new Color(0.92f, 0.92f, 0.88f, 1f);
        public static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f);
        public static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.11f, 0.96f);
        public static readonly Color ButtonColor = new Color(0.18f, 0.19f, 0.22f, 1f);
        public static readonly Color SelectedColor = new Color(0.55f, 0.42f, 0.16f, 1f);

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
            Round(panel, 8f);
            Pad(panel, 24f);
            return panel;
        }

        public static Label Title(string text)
        {
            var label = new Label(text);
            label.style.fontSize = 30f;
            label.style.color = TextColor;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.marginBottom = 16f;
            return label;
        }

        public static Button MakeButton(string name, string text, Action onClick)
        {
            var button = new Button(onClick) { name = name, text = text };
            button.style.height = 44f;
            button.style.fontSize = 20f;
            button.style.marginTop = 6f;
            button.style.marginBottom = 6f;
            button.style.color = TextColor;
            button.style.backgroundColor = ButtonColor;
            Round(button, 6f);
            return button;
        }

        public static VisualElement Row()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.Center;
            return row;
        }

        /// <summary>Spacing and a readable, aligned label for a settings field.</summary>
        public static void Field<T>(BaseField<T> field)
        {
            field.style.marginTop = 4f;
            field.style.marginBottom = 4f;
            field.labelElement.style.color = TextColor;
            field.labelElement.style.minWidth = 260f;
            field.labelElement.style.fontSize = 18f;
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
    }
}
