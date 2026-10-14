using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace TycoWorld.Player.Tests
{
    public class HudViewTests
    {
        static (VisualElement Root, HudView View, HudModel Model) Make()
        {
            var root = new VisualElement();
            return (root, new HudView(root), new HudModel(new HudTuning()));
        }

        [Test]
        public void Build_CreatesNamedElements()
        {
            var (root, _, _) = Make();
            foreach (var name in new[] { HudView.CrosshairName, HudView.PromptName, HudView.MessageName, HudView.StaminaName, HudView.StaminaFillName, HudView.ClockName })
                Assert.IsNotNull(root.Q(name), name);
        }

        [Test]
        public void Render_ShowsPrompt_AndHidesWhenEmpty()
        {
            var (root, view, model) = Make();
            model.SetPrompt("[E] Buka pagar");
            view.Render(model);
            var prompt = root.Q<Label>(HudView.PromptName);
            Assert.AreEqual("[E] Buka pagar", prompt.text);
            Assert.AreEqual(DisplayStyle.Flex, prompt.style.display.value);
            model.SetPrompt("");
            view.Render(model);
            Assert.AreEqual(DisplayStyle.None, prompt.style.display.value);
        }

        [Test]
        public void Render_MessageAndStamina()
        {
            var (root, view, model) = Make();
            model.ShowMessage("Kos Raflesia 3");
            model.Tick(0.1f, 0.5f);
            view.Render(model);
            Assert.AreEqual("Kos Raflesia 3", root.Q<Label>(HudView.MessageName).text);
            Assert.AreEqual(DisplayStyle.Flex, root.Q(HudView.StaminaName).style.display.value);
            var width = root.Q(HudView.StaminaFillName).style.width.value;
            Assert.AreEqual(LengthUnit.Percent, width.unit);
            Assert.AreEqual(50f, width.value, 1e-3f);
        }

        [Test]
        public void Render_HidesCrosshairWhenModelSaysSo()
        {
            var (root, view, model) = Make();
            model.CrosshairVisible = false;
            view.Render(model);
            Assert.AreEqual(DisplayStyle.None, root.Q(HudView.CrosshairName).style.display.value);
        }

        [Test]
        public void Render_ClockTopRight_OnlyWhenVisible()
        {
            var (root, view, model) = Make();
            view.Render(model);
            var clock = root.Q<Label>(HudView.ClockName);
            Assert.AreEqual(DisplayStyle.None, clock.style.display.value);
            model.SetClock("06:30", true);
            view.Render(model);
            Assert.AreEqual("06:30", clock.text);
            Assert.AreEqual(DisplayStyle.Flex, clock.style.display.value);
            Assert.AreEqual(TextAnchor.MiddleRight, clock.style.unityTextAlign.value);
            model.SetClock("06:30", false);
            view.Render(model);
            Assert.AreEqual(DisplayStyle.None, clock.style.display.value);
        }

        [Test]
        public void VehiclePanel_IsBuiltHidden_WithIndonesianTexts()
        {
            var (root, _, _) = Make();
            foreach (var name in new[] { HudView.VehicleName, HudView.SpeedName, HudView.UnitName, HudView.LampName, HudView.ExitName })
                Assert.IsNotNull(root.Q(name), name);
            Assert.AreEqual(DisplayStyle.None, root.Q(HudView.VehicleName).style.display.value);
            Assert.AreEqual("km/j", root.Q<Label>(HudView.UnitName).text);
            Assert.AreEqual("LAMPU", root.Q<Label>(HudView.LampName).text);
            Assert.AreEqual("[E] Turun", root.Q<Label>(HudView.ExitName).text);
        }

        [Test]
        public void Render_ShowsSpeedAndExitHint_HidesStamina()
        {
            var (root, view, model) = Make();
            model.Tick(0.1f, 0.5f);
            Assert.IsTrue(model.StaminaVisible);
            model.SetVehicle(true, 42.4f, false, true);
            view.Render(model);
            Assert.AreEqual(DisplayStyle.Flex, root.Q(HudView.VehicleName).style.display.value);
            Assert.AreEqual("42", root.Q<Label>(HudView.SpeedName).text);
            Assert.AreEqual(DisplayStyle.Flex, root.Q(HudView.ExitName).style.display.value);
            Assert.AreEqual(DisplayStyle.None, root.Q(HudView.StaminaName).style.display.value);
            model.SetVehicle(true, 30f, true, false);
            view.Render(model);
            Assert.AreEqual("30", root.Q<Label>(HudView.SpeedName).text);
            Assert.AreEqual(DisplayStyle.None, root.Q(HudView.ExitName).style.display.value);
            model.SetVehicle(false, 0f, false, false);
            view.Render(model);
            Assert.AreEqual(DisplayStyle.None, root.Q(HudView.VehicleName).style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, root.Q(HudView.StaminaName).style.display.value);
        }

        [Test]
        public void Render_HeadlightOn_BrightensTheLampLabel()
        {
            var (root, view, model) = Make();
            model.SetVehicle(true, 10f, false, false);
            view.Render(model);
            float off = root.Q<Label>(HudView.LampName).style.color.value.a;
            model.SetVehicle(true, 10f, true, false);
            view.Render(model);
            float on = root.Q<Label>(HudView.LampName).style.color.value.a;
            Assert.Greater(on, off);
        }

        [Test]
        public void PlayerHud_WithoutPanel_DoesNotThrow()
        {
            using (var fx = new PhysicsFixture())
            {
                var rig = TestRig.Create(fx, Vector3.zero);
                rig.Root.AddComponent<PlayerHud>();
                rig.Init();
                Assert.DoesNotThrow(() => rig.Player.Tick(1f / 60f));
            }
        }
    }
}
