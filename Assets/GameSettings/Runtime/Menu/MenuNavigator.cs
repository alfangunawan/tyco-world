using System;

namespace TycoWorld.GameSettings
{
    /// <summary>
    /// Which menu page is open (spec #4 §6). Esc: closed → main, main → closed, settings/confirm → main.
    /// PageChanged reports (from, to) so the menu can pause, resume and save at the right moments.
    /// </summary>
    public sealed class MenuNavigator
    {
        public event Action<MenuPage, MenuPage> PageChanged;

        public MenuPage Page { get; private set; } = MenuPage.Closed;

        public void Escape()
        {
            switch (Page)
            {
                case MenuPage.Closed:
                    Go(MenuPage.Main);
                    break;
                case MenuPage.Main:
                    Go(MenuPage.Closed);
                    break;
                default:
                    Go(MenuPage.Main);
                    break;
            }
        }

        public void Go(MenuPage page)
        {
            if (page == Page) return;
            var from = Page;
            Page = page;
            PageChanged?.Invoke(from, page);
        }
    }
}
