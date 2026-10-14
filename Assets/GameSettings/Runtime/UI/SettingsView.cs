using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TycoWorld.GameSettings
{
    public enum SettingsTab
    {
        Graphics,
        Controls,
        Audio,
        Display
    }

    /// <summary>
    /// Code-built settings page with four tabs (spec #4 §4/§6). Every control writes through the SettingsPresenter;
    /// Refresh reads the presenter's values back into the controls without raising change events.
    /// </summary>
    public sealed class SettingsView
    {
        public const string RootName = "settings-panel";
        public const string PresetName = "preset";
        public const string ViewDistanceName = "view-distance";
        public const string RenderScaleName = "render-scale";
        public const string ShadowsName = "shadows";
        public const string SsaoName = "ssao";
        public const string GpuOcclusionName = "gpu-occlusion";
        public const string LampLightsName = "lamp-lights";
        public const string FpsLimitName = "fps-limit";
        public const string VSyncName = "vsync";
        public const string ScreenModeName = "screen-mode";
        public const string ResolutionName = "resolution";
        public const string SensitivityName = "mouse-sensitivity";
        public const string InvertYName = "invert-y";
        public const string FovName = "fov";
        public const string MasterName = "master-volume";
        public const string EffectsName = "effects-volume";
        public const string AmbienceName = "ambience-volume";
        public const string ShowFpsName = "show-fps";
        public const string ShowClockName = "show-clock";
        public const string ResetName = "reset-defaults";
        public const string BackName = "back";
        public const string ValueSuffix = "-value";

        readonly SettingsPresenter presenter;
        readonly List<Vector2Int> resolutions = new List<Vector2Int>();
        readonly Dictionary<SettingsTab, VisualElement> pages = new Dictionary<SettingsTab, VisualElement>();
        readonly Dictionary<SettingsTab, Button> tabs = new Dictionary<SettingsTab, Button>();
        readonly Dictionary<string, Label> values = new Dictionary<string, Label>();
        readonly DropdownField preset, shadows, lampLights, fpsLimit, screenMode, resolution;
        readonly SliderInt viewDistance, renderScale, fieldOfView, masterVolume, effectsVolume, ambienceVolume;
        readonly Slider sensitivity;
        readonly Toggle ssao, gpuOcclusion, vSync, invertY, showFps, showClock;

        public event Action BackClicked;

        public SettingsView(VisualElement host, SettingsPresenter settingsPresenter, IReadOnlyList<Vector2Int> resolutionOptions)
        {
            presenter = settingsPresenter;
            if (resolutionOptions != null) resolutions.AddRange(resolutionOptions);

            Root = MenuStyle.Panel(RootName, 760f);
            Root.Add(MenuStyle.Title(SettingsText.SettingsTitle));
            var tabRow = MenuStyle.Row();
            tabRow.style.marginBottom = 12f;
            Root.Add(tabRow);
            foreach (SettingsTab tab in Enum.GetValues(typeof(SettingsTab)))
            {
                var shown = tab;
                var button = MenuStyle.MakeButton(TabName(tab), TabTitle(tab), () => ShowTab(shown));
                button.style.flexGrow = 1f;
                tabRow.Add(button);
                tabs[tab] = button;
                var page = new VisualElement { name = PageName(tab) };
                Root.Add(page);
                pages[tab] = page;
            }

            var g = pages[SettingsTab.Graphics];
            preset = MakeDropdown(g, PresetName, SettingsText.PresetLabel, SettingsText.PresetChoices(), i => presenter.SelectPreset((QualityPreset)i));
            viewDistance = MakeIntSlider(g, ViewDistanceName, SettingsText.ViewDistanceLabel, ViewDistanceRules.Min, ViewDistanceRules.Max,
                v => presenter.Edit(s => s.viewDistance = v));
            renderScale = MakeIntSlider(g, RenderScaleName, SettingsText.RenderScaleLabel, SettingsValidator.RenderScaleMin,
                SettingsValidator.RenderScaleMax, v => presenter.Edit(s => s.renderScale = v));
            shadows = MakeDropdown(g, ShadowsName, SettingsText.ShadowsLabel, SettingsText.ShadowChoices(),
                i => presenter.Edit(s => s.shadows = (ShadowQuality)i));
            ssao = MakeToggle(g, SsaoName, SettingsText.SsaoLabel, v => presenter.Edit(s => s.ssao = v));
            gpuOcclusion = MakeToggle(g, GpuOcclusionName, SettingsText.GpuOcclusionLabel, v => presenter.Edit(s => s.gpuOcclusion = v));
            lampLights = MakeDropdown(g, LampLightsName, SettingsText.LampLightsLabel, SettingsText.LampLightChoices(),
                i => presenter.Edit(s => s.lampLights = SettingsValidator.LampLightChoices()[i]));
            fpsLimit = MakeDropdown(g, FpsLimitName, SettingsText.FpsLimitLabel, SettingsText.FpsLimitChoices(),
                i => presenter.Edit(s => s.fpsLimit = SettingsValidator.FpsLimitChoices()[i]));
            vSync = MakeToggle(g, VSyncName, SettingsText.VSyncLabel, v => presenter.Edit(s => s.vSync = v));
            screenMode = MakeDropdown(g, ScreenModeName, SettingsText.ScreenModeLabel, SettingsText.ScreenModeChoices(),
                i => presenter.Edit(s => s.fullscreen = i == 0));
            resolution = MakeDropdown(g, ResolutionName, SettingsText.ResolutionLabel, ResolutionOptions.Labels(resolutions),
                i => presenter.Edit(s => SetResolution(s, i)));

            var c = pages[SettingsTab.Controls];
            sensitivity = MakeFloatSlider(c, SensitivityName, SettingsText.SensitivityLabel, SettingsValidator.SensitivityMin,
                SettingsValidator.SensitivityMax, v => presenter.Edit(s => s.mouseSensitivity = Mathf.Round(v * 10f) / 10f));
            invertY = MakeToggle(c, InvertYName, SettingsText.InvertYLabel, v => presenter.Edit(s => s.invertY = v));
            fieldOfView = MakeIntSlider(c, FovName, SettingsText.FovLabel, SettingsValidator.FovMin, SettingsValidator.FovMax,
                v => presenter.Edit(s => s.fieldOfView = v));

            var a = pages[SettingsTab.Audio];
            masterVolume = MakeIntSlider(a, MasterName, SettingsText.MasterLabel, 0, 100, v => presenter.Edit(s => s.masterVolume = v));
            effectsVolume = MakeIntSlider(a, EffectsName, SettingsText.EffectsLabel, 0, 100, v => presenter.Edit(s => s.effectsVolume = v));
            ambienceVolume = MakeIntSlider(a, AmbienceName, SettingsText.AmbienceLabel, 0, 100, v => presenter.Edit(s => s.ambienceVolume = v));

            var d = pages[SettingsTab.Display];
            showFps = MakeToggle(d, ShowFpsName, SettingsText.ShowFpsLabel, v => presenter.Edit(s => s.showFps = v));
            showClock = MakeToggle(d, ShowClockName, SettingsText.ShowClockLabel, v => presenter.Edit(s => s.showClock = v));

            var footer = MenuStyle.Row();
            footer.style.marginTop = 16f;
            var reset = MenuStyle.MakeButton(ResetName, SettingsText.ResetDefaults, () => presenter.ResetDefaults());
            var back = MenuStyle.MakeButton(BackName, SettingsText.Back, () => BackClicked?.Invoke());
            reset.style.flexGrow = 1f;
            reset.style.marginRight = 8f;
            back.style.flexGrow = 1f;
            footer.Add(reset);
            footer.Add(back);
            Root.Add(footer);

            host.Add(Root);
            presenter.Changed += _ => Refresh();
            Refresh();
            ShowTab(SettingsTab.Graphics);
        }

        public VisualElement Root { get; }
        public SettingsTab Tab { get; private set; }

        public static string TabName(SettingsTab tab) => "tab-" + Key(tab);

        public static string PageName(SettingsTab tab) => "page-" + Key(tab);

        public void ShowTab(SettingsTab tab)
        {
            Tab = tab;
            foreach (var pair in pages) pair.Value.style.display = pair.Key == tab ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var pair in tabs) pair.Value.style.backgroundColor = pair.Key == tab ? MenuStyle.SelectedColor : MenuStyle.ButtonColor;
        }

        public void Refresh()
        {
            var s = presenter.Working;
            preset.SetValueWithoutNotify(SettingsText.PresetName(s.preset));
            SetSlider(viewDistance, ViewDistanceName, s.viewDistance, SettingsText.Meters(s.viewDistance));
            SetSlider(renderScale, RenderScaleName, s.renderScale, SettingsText.Percent(s.renderScale));
            shadows.SetValueWithoutNotify(SettingsText.ShadowName(s.shadows));
            ssao.SetValueWithoutNotify(s.ssao);
            gpuOcclusion.SetValueWithoutNotify(s.gpuOcclusion);
            lampLights.SetValueWithoutNotify(SettingsText.LampLights(s.lampLights));
            fpsLimit.SetValueWithoutNotify(SettingsText.FpsLimit(s.fpsLimit));
            vSync.SetValueWithoutNotify(s.vSync);
            screenMode.SetValueWithoutNotify(SettingsText.ScreenMode(s.fullscreen));
            resolution.SetValueWithoutNotify(resolution.choices[ResolutionOptions.IndexOf(resolutions, s.resolutionWidth, s.resolutionHeight)]);
            sensitivity.SetValueWithoutNotify(s.mouseSensitivity);
            values[SensitivityName].text = SettingsText.Multiplier(s.mouseSensitivity);
            invertY.SetValueWithoutNotify(s.invertY);
            SetSlider(fieldOfView, FovName, s.fieldOfView, SettingsText.Degrees(s.fieldOfView));
            SetSlider(masterVolume, MasterName, s.masterVolume, SettingsText.Percent(s.masterVolume));
            SetSlider(effectsVolume, EffectsName, s.effectsVolume, SettingsText.Percent(s.effectsVolume));
            SetSlider(ambienceVolume, AmbienceName, s.ambienceVolume, SettingsText.Percent(s.ambienceVolume));
            showFps.SetValueWithoutNotify(s.showFps);
            showClock.SetValueWithoutNotify(s.showClock);
        }

        void SetResolution(SettingsData s, int index)
        {
            var size = ResolutionOptions.At(resolutions, index);
            s.resolutionWidth = size.x;
            s.resolutionHeight = size.y;
        }

        void SetSlider(SliderInt slider, string name, int value, string text)
        {
            slider.SetValueWithoutNotify(value);
            values[name].text = text;
        }

        DropdownField MakeDropdown(VisualElement parent, string name, string label, List<string> choices, Action<int> onIndex)
        {
            var field = new DropdownField(label, choices, 0) { name = name };
            field.RegisterValueChangedCallback(e =>
            {
                int index = field.choices.IndexOf(e.newValue);
                if (index >= 0) onIndex(index);
            });
            MenuStyle.Field(field);
            parent.Add(field);
            return field;
        }

        Toggle MakeToggle(VisualElement parent, string name, string label, Action<bool> onChange)
        {
            var field = new Toggle(label) { name = name };
            field.RegisterValueChangedCallback(e => onChange(e.newValue));
            MenuStyle.Field(field);
            parent.Add(field);
            return field;
        }

        SliderInt MakeIntSlider(VisualElement parent, string name, string label, int low, int high, Action<int> onChange)
        {
            var field = new SliderInt(label, low, high) { name = name };
            field.RegisterValueChangedCallback(e => onChange(e.newValue));
            MenuStyle.Field(field);
            AddWithValue(parent, field, name);
            return field;
        }

        Slider MakeFloatSlider(VisualElement parent, string name, string label, float low, float high, Action<float> onChange)
        {
            var field = new Slider(label, low, high) { name = name };
            field.RegisterValueChangedCallback(e => onChange(e.newValue));
            MenuStyle.Field(field);
            AddWithValue(parent, field, name);
            return field;
        }

        void AddWithValue(VisualElement parent, VisualElement field, string name)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            field.style.flexGrow = 1f;
            row.Add(field);
            var value = new Label { name = name + ValueSuffix };
            value.style.width = 90f;
            value.style.unityTextAlign = TextAnchor.MiddleRight;
            value.style.color = MenuStyle.TextColor;
            value.style.fontSize = 18f;
            row.Add(value);
            values[name] = value;
            parent.Add(row);
        }

        static string Key(SettingsTab tab)
        {
            switch (tab)
            {
                case SettingsTab.Controls: return "controls";
                case SettingsTab.Audio: return "audio";
                case SettingsTab.Display: return "display";
                default: return "graphics";
            }
        }

        static string TabTitle(SettingsTab tab)
        {
            switch (tab)
            {
                case SettingsTab.Controls: return SettingsText.TabControls;
                case SettingsTab.Audio: return SettingsText.TabAudio;
                case SettingsTab.Display: return SettingsText.TabDisplay;
                default: return SettingsText.TabGraphics;
            }
        }
    }
}
