using UnityEngine;
using UnityEngine.UIElements;

namespace TycoWorld.GameSettings
{
    /// <summary>Small FPS label in the top-left corner (Tampilan → Penghitung FPS).</summary>
    public sealed class FpsCounterView
    {
        public const string LabelName = "fps";

        readonly Label label;

        public FpsCounterView(VisualElement root)
        {
            label = new Label { name = LabelName, pickingMode = PickingMode.Ignore };
            label.style.position = Position.Absolute;
            label.style.left = 16f;
            label.style.top = 12f;
            label.style.fontSize = 18f;
            label.style.color = MenuStyle.TextColor;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.display = DisplayStyle.None;
            root.Add(label);
        }

        public void Render(bool visible, float fps)
        {
            label.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible) label.text = SettingsText.Fps(fps);
        }
    }
}
