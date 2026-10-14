using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace TycoWorld.GameSettings.Tests
{
    public class SettingsViewTests
    {
        SettingsPresenter presenter;
        VisualElement host;
        SettingsView view;

        [SetUp]
        public void SetUp()
        {
            presenter = new SettingsPresenter(new SettingsData());
            host = new VisualElement();
            view = new SettingsView(host, presenter, new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) });
        }

        T Q<T>(string name) where T : VisualElement
        {
            var element = host.Q<T>(name);
            Assert.IsNotNull(element, name);
            return element;
        }

        string ValueText(string name) => Q<Label>(name + SettingsView.ValueSuffix).text;

        [Test]
        public void Builds_EveryNamedControl()
        {
            foreach (var name in new[]
                     {
                         SettingsView.PresetName, SettingsView.ViewDistanceName, SettingsView.RenderScaleName, SettingsView.ShadowsName,
                         SettingsView.SsaoName, SettingsView.GpuOcclusionName, SettingsView.LampLightsName, SettingsView.FpsLimitName,
                         SettingsView.VSyncName, SettingsView.ScreenModeName, SettingsView.ResolutionName, SettingsView.SensitivityName,
                         SettingsView.InvertYName, SettingsView.FovName, SettingsView.MasterName, SettingsView.EffectsName,
                         SettingsView.AmbienceName, SettingsView.ShowFpsName, SettingsView.ShowClockName, SettingsView.ResetName,
                         SettingsView.BackName
                     })
                Assert.IsNotNull(host.Q(name), name);
            foreach (SettingsTab tab in System.Enum.GetValues(typeof(SettingsTab)))
            {
                Assert.IsNotNull(host.Q(SettingsView.TabName(tab)), SettingsView.TabName(tab));
                Assert.IsNotNull(host.Q(SettingsView.PageName(tab)), SettingsView.PageName(tab));
            }
        }

        [Test]
        public void Refresh_ShowsTheDefaults()
        {
            Assert.AreEqual("Sedang", Q<DropdownField>(SettingsView.PresetName).value);
            Assert.AreEqual(300, Q<SliderInt>(SettingsView.ViewDistanceName).value);
            Assert.AreEqual("300 m", ValueText(SettingsView.ViewDistanceName));
            Assert.AreEqual("85 %", ValueText(SettingsView.RenderScaleName));
            Assert.AreEqual("Sedang", Q<DropdownField>(SettingsView.ShadowsName).value);
            Assert.AreEqual("16", Q<DropdownField>(SettingsView.LampLightsName).value);
            Assert.AreEqual("60 FPS", Q<DropdownField>(SettingsView.FpsLimitName).value);
            Assert.AreEqual("Layar penuh", Q<DropdownField>(SettingsView.ScreenModeName).value);
            Assert.AreEqual("Saat ini", Q<DropdownField>(SettingsView.ResolutionName).value);
            Assert.AreEqual("1,0×", ValueText(SettingsView.SensitivityName));
            Assert.AreEqual("70°", ValueText(SettingsView.FovName));
            Assert.AreEqual("100 %", ValueText(SettingsView.MasterName));
            Assert.IsTrue(Q<Toggle>(SettingsView.GpuOcclusionName).value);
            Assert.IsFalse(Q<Toggle>(SettingsView.ShowFpsName).value);
        }

        [Test]
        public void PresenterChanges_AreShown()
        {
            presenter.SelectPreset(QualityPreset.High);
            Assert.AreEqual("Tinggi", Q<DropdownField>(SettingsView.PresetName).value);
            Assert.AreEqual(500, Q<SliderInt>(SettingsView.ViewDistanceName).value);
            Assert.AreEqual("500 m", ValueText(SettingsView.ViewDistanceName));
            Assert.AreEqual("Tinggi", Q<DropdownField>(SettingsView.ShadowsName).value);
            Assert.IsTrue(Q<Toggle>(SettingsView.SsaoName).value);
            Assert.AreEqual("32", Q<DropdownField>(SettingsView.LampLightsName).value);
        }

        [Test]
        public void CustomValues_ShowKustom()
        {
            presenter.Edit(s => s.viewDistance = 450);
            Assert.AreEqual("Kustom", Q<DropdownField>(SettingsView.PresetName).value);
        }

        [Test]
        public void Resolution_ChoicesStartWithCurrent()
        {
            CollectionAssert.AreEqual(new[] { "Saat ini", "1920 × 1080", "1280 × 720" }, Q<DropdownField>(SettingsView.ResolutionName).choices);
            presenter.Edit(s =>
            {
                s.resolutionWidth = 1280;
                s.resolutionHeight = 720;
            });
            Assert.AreEqual("1280 × 720", Q<DropdownField>(SettingsView.ResolutionName).value);
        }

        [Test]
        public void StartsOnGraphics_ShowTabShowsOnePage()
        {
            Assert.AreEqual(SettingsTab.Graphics, view.Tab);
            view.ShowTab(SettingsTab.Audio);
            Assert.AreEqual(SettingsTab.Audio, view.Tab);
            foreach (SettingsTab tab in System.Enum.GetValues(typeof(SettingsTab)))
                Assert.AreEqual(tab == SettingsTab.Audio ? DisplayStyle.Flex : DisplayStyle.None,
                    host.Q(SettingsView.PageName(tab)).style.display.value, tab.ToString());
        }
    }
}
