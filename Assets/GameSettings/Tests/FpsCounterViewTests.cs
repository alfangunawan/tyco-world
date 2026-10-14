using NUnit.Framework;
using UnityEngine.UIElements;

namespace TycoWorld.GameSettings.Tests
{
    public class FpsCounterViewTests
    {
        [Test]
        public void Render_ShowsRoundedFps()
        {
            var root = new VisualElement();
            var view = new FpsCounterView(root);
            view.Render(true, 59.6f);
            var label = root.Q<Label>(FpsCounterView.LabelName);
            Assert.AreEqual("60 FPS", label.text);
            Assert.AreEqual(DisplayStyle.Flex, label.style.display.value);
        }

        [Test]
        public void Render_Hidden()
        {
            var root = new VisualElement();
            var view = new FpsCounterView(root);
            view.Render(false, 60f);
            Assert.AreEqual(DisplayStyle.None, root.Q<Label>(FpsCounterView.LabelName).style.display.value);
        }
    }
}
