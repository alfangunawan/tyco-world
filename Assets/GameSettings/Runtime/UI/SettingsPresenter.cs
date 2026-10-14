using System;

namespace TycoWorld.GameSettings
{
    /// <summary>
    /// The settings page's working copy: every edit is validated, the preset is re-detected (any changed graphics value
    /// makes it Custom), and listeners receive a copy so they cannot change the page behind its back (spec #4 §4).
    /// </summary>
    public sealed class SettingsPresenter
    {
        SettingsData working;

        public SettingsPresenter(SettingsData initial) => working = initial != null ? initial.Clone() : new SettingsData();

        public event Action<SettingsData> Changed;

        public SettingsData Working => working;

        /// <summary>Shows other settings (the applied ones when the page opens) without raising Changed.</summary>
        public void Load(SettingsData settings) => working = settings != null ? settings.Clone() : new SettingsData();

        public void SelectPreset(QualityPreset preset)
        {
            if (preset != QualityPreset.Custom) QualityPresets.Apply(preset, working);
            working.preset = QualityPresets.Detect(working);
            Notify();
        }

        public void Edit(Action<SettingsData> edit)
        {
            edit(working);
            SettingsValidator.Sanitize(working);
            Notify();
        }

        public void ResetDefaults()
        {
            working = new SettingsData();
            Notify();
        }

        void Notify() => Changed?.Invoke(working.Clone());
    }
}
