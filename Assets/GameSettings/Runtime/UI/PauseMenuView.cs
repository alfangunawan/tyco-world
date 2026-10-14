using System;
using UnityEngine.UIElements;

namespace TycoWorld.GameSettings
{
    /// <summary>Code-built pause overlay: main panel (Lanjutkan / Pengaturan / Keluar), quit confirmation and the settings host (spec #4 §6).</summary>
    public sealed class PauseMenuView
    {
        public const string OverlayName = "pause-overlay";
        public const string PanelName = "pause-panel";
        public const string ResumeName = "resume";
        public const string SettingsName = "open-settings";
        public const string QuitName = "quit";
        public const string ConfirmName = "quit-confirm";
        public const string QuitYesName = "quit-yes";
        public const string QuitNoName = "quit-no";
        public const string SettingsHostName = "settings-host";

        readonly VisualElement overlay, panel, confirm;

        public event Action ResumeClicked;
        public event Action SettingsClicked;
        public event Action QuitClicked;
        public event Action QuitConfirmed;
        public event Action QuitCancelled;

        public PauseMenuView(VisualElement root)
        {
            overlay = new VisualElement { name = OverlayName };
            MenuStyle.FullScreen(overlay);
            overlay.style.backgroundColor = MenuStyle.DimColor;
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;

            panel = MenuStyle.Panel(PanelName, 420f);
            panel.Add(MenuStyle.Title(SettingsText.PauseTitle));
            panel.Add(MenuStyle.MakeButton(ResumeName, SettingsText.Resume, () => ResumeClicked?.Invoke()));
            panel.Add(MenuStyle.MakeButton(SettingsName, SettingsText.OpenSettings, () => SettingsClicked?.Invoke()));
            panel.Add(MenuStyle.MakeButton(QuitName, SettingsText.Quit, () => QuitClicked?.Invoke()));

            confirm = MenuStyle.Panel(ConfirmName, 420f);
            confirm.Add(MenuStyle.Title(SettingsText.QuitQuestion));
            var row = MenuStyle.Row();
            var yes = MenuStyle.MakeButton(QuitYesName, SettingsText.QuitYes, () => QuitConfirmed?.Invoke());
            var no = MenuStyle.MakeButton(QuitNoName, SettingsText.QuitNo, () => QuitCancelled?.Invoke());
            yes.style.flexGrow = 1f;
            yes.style.marginRight = 8f;
            no.style.flexGrow = 1f;
            row.Add(yes);
            row.Add(no);
            confirm.Add(row);

            SettingsHost = new VisualElement { name = SettingsHostName };

            overlay.Add(panel);
            overlay.Add(confirm);
            overlay.Add(SettingsHost);
            root.Add(overlay);
            Show(MenuPage.Closed);
        }

        public VisualElement Overlay => overlay;
        public VisualElement SettingsHost { get; }
        public MenuPage Page { get; private set; }

        public void Show(MenuPage page)
        {
            Page = page;
            overlay.style.display = page == MenuPage.Closed ? DisplayStyle.None : DisplayStyle.Flex;
            panel.style.display = page == MenuPage.Main ? DisplayStyle.Flex : DisplayStyle.None;
            confirm.style.display = page == MenuPage.ConfirmQuit ? DisplayStyle.Flex : DisplayStyle.None;
            SettingsHost.style.display = page == MenuPage.Settings ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
