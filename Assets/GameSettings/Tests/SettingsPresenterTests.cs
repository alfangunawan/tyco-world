using NUnit.Framework;

namespace TycoWorld.GameSettings.Tests
{
    public class SettingsPresenterTests
    {
        SettingsPresenter presenter;

        [SetUp]
        public void SetUp() => presenter = new SettingsPresenter(new SettingsData());

        [Test]
        public void SelectPreset_FillsGraphicsAndNotifiesACopy()
        {
            SettingsData received = null;
            int calls = 0;
            presenter.Changed += s =>
            {
                received = s;
                calls++;
            };
            presenter.SelectPreset(QualityPreset.High);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(QualityPreset.High, received.preset);
            Assert.AreEqual(500, received.viewDistance);
            received.viewDistance = 100;
            Assert.AreEqual(500, presenter.Working.viewDistance, "pendengar menerima salinan");
        }

        [Test]
        public void Edit_GraphicsValue_SwitchesToCustom()
        {
            presenter.Edit(s => s.viewDistance = 450);
            Assert.AreEqual(450, presenter.Working.viewDistance);
            Assert.AreEqual(QualityPreset.Custom, presenter.Working.preset);
        }

        [Test]
        public void Edit_BackToPresetValues_DetectsThePreset()
        {
            presenter.Edit(s => s.viewDistance = 450);
            presenter.Edit(s => s.viewDistance = 300);
            Assert.AreEqual(QualityPreset.Medium, presenter.Working.preset);
        }

        [Test]
        public void Edit_SnapsAndClamps()
        {
            presenter.Edit(s => s.viewDistance = 333);
            Assert.AreEqual(350, presenter.Working.viewDistance);
            presenter.Edit(s => s.fieldOfView = 200);
            Assert.AreEqual(100, presenter.Working.fieldOfView);
        }

        [Test]
        public void Edit_NonGraphics_KeepsThePreset()
        {
            presenter.SelectPreset(QualityPreset.High);
            presenter.Edit(s => s.fieldOfView = 90);
            Assert.AreEqual(QualityPreset.High, presenter.Working.preset);
            Assert.AreEqual(90, presenter.Working.fieldOfView);
        }

        [Test]
        public void SelectCustom_KeepsTheValues()
        {
            presenter.SelectPreset(QualityPreset.High);
            presenter.SelectPreset(QualityPreset.Custom);
            Assert.AreEqual(500, presenter.Working.viewDistance);
            Assert.AreEqual(QualityPreset.High, presenter.Working.preset, "Kustom hanya tampil kalau nilainya memang tidak cocok preset");
        }

        [Test]
        public void ResetDefaults_RestoresMedium()
        {
            presenter.SelectPreset(QualityPreset.High);
            presenter.Edit(s => s.masterVolume = 10);
            presenter.ResetDefaults();
            Assert.AreEqual(QualityPreset.Medium, presenter.Working.preset);
            Assert.AreEqual(300, presenter.Working.viewDistance);
            Assert.AreEqual(100, presenter.Working.masterVolume);
        }

        [Test]
        public void Load_ReplacesWorkingWithoutNotifying()
        {
            int calls = 0;
            presenter.Changed += _ => calls++;
            var loaded = new SettingsData { viewDistance = 500 };
            presenter.Load(loaded);
            Assert.AreEqual(500, presenter.Working.viewDistance);
            Assert.AreEqual(0, calls);
            loaded.viewDistance = 100;
            Assert.AreEqual(500, presenter.Working.viewDistance);
        }
    }
}
