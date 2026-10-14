using System;
using System.Collections.Generic;
using TycoWorld.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TycoWorld.GameSettings
{
    /// <summary>
    /// The pause menu (spec #4 §6): Esc opens and closes it, it pauses the game, hosts the settings page (changes apply
    /// live and are saved when the page closes), shows the FPS counter, and never leaves the game paused when destroyed.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public sealed class GameMenu : MonoBehaviour
    {
        readonly MenuNavigator navigator = new MenuNavigator();
        readonly FrameStats fpsStats = new FrameStats(0.5f);
        SettingsApplier applier;
        PauseController pause;
        SettingsPresenter presenter;
        PauseMenuView pauseView;
        SettingsView settingsView;
        FpsCounterView fpsView;

        /// <summary>Pause side effects; null (default) = the real Time.timeScale and AudioListener. Tests set a fake.</summary>
        public IPauseEffects PauseEffects { get; set; }
        public Func<FirstPersonPlayer> FindPlayer { get; set; } = () => UnityEngine.Object.FindAnyObjectByType<FirstPersonPlayer>();
        public Func<IReadOnlyList<Vector2Int>> ListResolutions { get; set; } = ScreenResolutions;
        /// <summary>What "Ya, keluar" does; the default stops Play in the editor and quits a build.</summary>
        public Action QuitAction { get; set; } = QuitGame;

        public MenuNavigator Navigator => navigator;
        public PauseController Pause => pause;
        public SettingsPresenter Presenter => presenter;
        public PauseMenuView PauseView => pauseView;
        public SettingsView SettingsView => settingsView;
        public bool IsBuilt => pauseView != null;

        void Start()
        {
            if (Application.isPlaying) Initialize();
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            if (pauseView == null)
            {
                var document = GetComponent<UIDocument>();
                if (document != null && document.rootVisualElement != null) Build(document.rootVisualElement);
            }
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Escape();
            Tick(Time.unscaledDeltaTime);
        }

        void OnDestroy() => Shutdown();

        public void Initialize()
        {
            if (pause != null) return;
            applier = GetComponent<SettingsApplier>();
            pause = new PauseController(PauseEffects != null ? PauseEffects : new UnityPauseEffects());
            pause.Attach(FindPlayer != null ? FindPlayer() : null);
            navigator.PageChanged += OnPageChanged;
        }

        public void Build(VisualElement root)
        {
            if (pauseView != null) return;
            Initialize();
            root.style.flexGrow = 1f;
            root.pickingMode = PickingMode.Ignore;
            fpsView = new FpsCounterView(root);
            pauseView = new PauseMenuView(root);
            presenter = new SettingsPresenter(CurrentSettings());
            presenter.Changed += OnSettingsEdited;
            settingsView = new SettingsView(pauseView.SettingsHost, presenter, ListResolutions != null ? ListResolutions() : null);
            pauseView.ResumeClicked += Resume;
            pauseView.SettingsClicked += OpenSettings;
            pauseView.QuitClicked += RequestQuit;
            pauseView.QuitCancelled += CancelQuit;
            pauseView.QuitConfirmed += ConfirmQuit;
            settingsView.BackClicked += BackFromSettings;
            pauseView.Show(navigator.Page);
        }

        public void Escape()
        {
            Initialize();
            navigator.Escape();
        }

        public void Resume() => navigator.Go(MenuPage.Closed);

        public void OpenSettings() => navigator.Go(MenuPage.Settings);

        public void RequestQuit() => navigator.Go(MenuPage.ConfirmQuit);

        public void CancelQuit() => navigator.Go(MenuPage.Main);

        public void BackFromSettings() => navigator.Go(MenuPage.Main);

        public void ConfirmQuit()
        {
            if (QuitAction != null) QuitAction();
        }

        public void Tick(float unscaledDeltaTime)
        {
            fpsStats.Sample(unscaledDeltaTime);
            if (fpsView != null) fpsView.Render(applier != null && applier.Current.showFps, fpsStats.HasResult ? fpsStats.AverageFps : 0f);
        }

        /// <summary>Unpauses when the menu goes away (Play stopped, scene unloaded).</summary>
        public void Shutdown()
        {
            if (pause != null) pause.Shutdown();
        }

        void OnPageChanged(MenuPage from, MenuPage to)
        {
            if (from == MenuPage.Closed) pause.Pause();
            if (to == MenuPage.Closed) pause.Resume();
            if (from == MenuPage.Settings && applier != null) applier.Save();
            if (to == MenuPage.Settings && presenter != null)
            {
                presenter.Load(CurrentSettings());
                settingsView.Refresh();
            }
            if (pauseView != null) pauseView.Show(to);
        }

        void OnSettingsEdited(SettingsData settings)
        {
            if (applier != null) applier.Apply(settings);
        }

        SettingsData CurrentSettings() => applier != null ? applier.Current : new SettingsData();

        static IReadOnlyList<Vector2Int> ScreenResolutions()
        {
            var sizes = new List<Vector2Int>();
            foreach (var resolution in Screen.resolutions) sizes.Add(new Vector2Int(resolution.width, resolution.height));
            return ResolutionOptions.Distinct(sizes);
        }

        static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
