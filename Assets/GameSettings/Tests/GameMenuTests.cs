using NUnit.Framework;
using TycoWorld.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace TycoWorld.GameSettings.Tests
{
    public class GameMenuTests
    {
        GameFixture fx;
        FirstPersonPlayer player;
        SettingsApplier applier;
        GameMenu menu;
        FakePauseEffects effects;
        MemorySettingsBackend backend;
        VisualElement root;
        int quits;
        RenderPipelineAsset pipelineBefore;
        int frameRateBefore, vSyncBefore;

        [SetUp]
        public void SetUp()
        {
            pipelineBefore = QualitySettings.renderPipeline;
            frameRateBefore = Application.targetFrameRate;
            vSyncBefore = QualitySettings.vSyncCount;
            fx = new GameFixture();
            player = fx.CreatePlayer();
            var go = fx.Track(new GameObject("GameSystems"));
            applier = go.AddComponent<SettingsApplier>();
            backend = new MemorySettingsBackend();
            applier.Backend = backend;
            applier.FindPlayer = () => player;
            applier.FindDirector = () => null;
            menu = go.AddComponent<GameMenu>();
            effects = new FakePauseEffects();
            menu.PauseEffects = effects;
            menu.FindPlayer = () => player;
            menu.ListResolutions = () => new[] { new Vector2Int(1920, 1080) };
            quits = 0;
            menu.QuitAction = () => quits++;
            applier.Initialize();
            menu.Initialize();
            root = new VisualElement();
            menu.Build(root);
        }

        [TearDown]
        public void TearDown()
        {
            menu.Shutdown();
            applier.Shutdown();
            fx.Dispose();
            if (QualitySettings.renderPipeline != pipelineBefore) QualitySettings.renderPipeline = pipelineBefore;
            if (QualitySettings.vSyncCount != vSyncBefore) QualitySettings.vSyncCount = vSyncBefore;
            Application.targetFrameRate = frameRateBefore;
        }

        static DisplayStyle Display(VisualElement e) => e.style.display.value;

        [Test]
        public void Build_StartsClosed_FpsHidden()
        {
            Assert.IsTrue(menu.IsBuilt);
            Assert.AreEqual(MenuPage.Closed, menu.Navigator.Page);
            Assert.AreEqual(DisplayStyle.None, Display(root.Q(PauseMenuView.OverlayName)));
            menu.Tick(0.5f);
            Assert.AreEqual(DisplayStyle.None, Display(root.Q(FpsCounterView.LabelName)));
        }

        [Test]
        public void Escape_OpensPauseAndFreezes()
        {
            menu.Escape();
            Assert.AreEqual(MenuPage.Main, menu.Navigator.Page);
            Assert.IsTrue(effects.Paused);
            Assert.AreEqual(DisplayStyle.Flex, Display(root.Q(PauseMenuView.OverlayName)));
            Assert.IsFalse(player.Locks.Allows(PlayerAbility.Move));
            Assert.IsFalse(player.EscapeUnlocksCursor);
        }

        [Test]
        public void Escape_Twice_Resumes()
        {
            menu.Escape();
            menu.Escape();
            Assert.AreEqual(MenuPage.Closed, menu.Navigator.Page);
            Assert.IsFalse(effects.Paused);
            Assert.IsTrue(player.Locks.Allows(PlayerAbility.Move));
        }

        [Test]
        public void SettingsEdits_ApplyLive_AndSaveWhenThePageCloses()
        {
            menu.Escape();
            menu.OpenSettings();
            Assert.AreEqual(MenuPage.Settings, menu.Navigator.Page);
            menu.Presenter.Edit(s => s.fieldOfView = 90);
            Assert.AreEqual(90, applier.Current.fieldOfView, "langsung diterapkan");
            Assert.AreEqual(90f, player.View.GetComponent<Camera>().fieldOfView, 1e-3f);
            Assert.AreEqual(0, backend.Writes, "belum disimpan");
            menu.Escape();
            Assert.AreEqual(MenuPage.Main, menu.Navigator.Page);
            Assert.AreEqual(1, backend.Writes);
            StringAssert.Contains("\"fieldOfView\":90", backend.Json);
        }

        [Test]
        public void OpeningSettings_ShowsTheAppliedValues()
        {
            applier.Apply(new SettingsData { viewDistance = 500 });
            menu.Escape();
            menu.OpenSettings();
            Assert.AreEqual(500, root.Q<SliderInt>(SettingsView.ViewDistanceName).value);
        }

        [Test]
        public void Quit_NeedsConfirmation()
        {
            menu.Escape();
            menu.RequestQuit();
            Assert.AreEqual(MenuPage.ConfirmQuit, menu.Navigator.Page);
            Assert.AreEqual(0, quits);
            menu.CancelQuit();
            Assert.AreEqual(MenuPage.Main, menu.Navigator.Page);
            menu.RequestQuit();
            menu.ConfirmQuit();
            Assert.AreEqual(1, quits);
        }

        [Test]
        public void FpsCounter_FollowsTheSetting()
        {
            applier.Apply(new SettingsData { showFps = true });
            for (int i = 0; i < 4; i++) menu.Tick(0.125f);
            var label = root.Q<Label>(FpsCounterView.LabelName);
            Assert.AreEqual(DisplayStyle.Flex, Display(label));
            Assert.AreEqual("8 FPS", label.text);
        }

        [Test]
        public void Shutdown_WhilePaused_Resumes()
        {
            menu.Escape();
            menu.Shutdown();
            Assert.IsFalse(effects.Paused);
            Assert.IsTrue(player.Locks.Allows(PlayerAbility.Move));
        }
    }
}
