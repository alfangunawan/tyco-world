using NUnit.Framework;
using UnityEngine.UIElements;

namespace TycoWorld.GameSettings.Tests
{
    public class PauseMenuViewTests
    {
        VisualElement root;
        PauseMenuView view;

        [SetUp]
        public void SetUp()
        {
            root = new VisualElement();
            view = new PauseMenuView(root);
        }

        static DisplayStyle Display(VisualElement e) => e.style.display.value;

        [Test]
        public void Builds_NamedElementsWithIndonesianText()
        {
            Assert.AreEqual("Lanjutkan", root.Q<Button>(PauseMenuView.ResumeName).text);
            Assert.AreEqual("Pengaturan", root.Q<Button>(PauseMenuView.SettingsName).text);
            Assert.AreEqual("Keluar", root.Q<Button>(PauseMenuView.QuitName).text);
            Assert.AreEqual("Ya, keluar", root.Q<Button>(PauseMenuView.QuitYesName).text);
            Assert.AreEqual("Batal", root.Q<Button>(PauseMenuView.QuitNoName).text);
            Assert.IsNotNull(root.Q(PauseMenuView.SettingsHostName));
            Assert.AreSame(root.Q(PauseMenuView.SettingsHostName), view.SettingsHost);
        }

        [Test]
        public void StartsClosed()
        {
            Assert.AreEqual(MenuPage.Closed, view.Page);
            Assert.AreEqual(DisplayStyle.None, Display(root.Q(PauseMenuView.OverlayName)));
        }

        [TestCase(MenuPage.Main, PauseMenuView.PanelName)]
        [TestCase(MenuPage.Settings, PauseMenuView.SettingsHostName)]
        [TestCase(MenuPage.ConfirmQuit, PauseMenuView.ConfirmName)]
        public void Show_DisplaysExactlyOneSection(MenuPage page, string visible)
        {
            view.Show(page);
            Assert.AreEqual(DisplayStyle.Flex, Display(root.Q(PauseMenuView.OverlayName)));
            foreach (var name in new[] { PauseMenuView.PanelName, PauseMenuView.SettingsHostName, PauseMenuView.ConfirmName })
                Assert.AreEqual(name == visible ? DisplayStyle.Flex : DisplayStyle.None, Display(root.Q(name)), name);
        }
    }
}
