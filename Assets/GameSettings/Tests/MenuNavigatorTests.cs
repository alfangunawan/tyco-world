using System.Collections.Generic;
using NUnit.Framework;

namespace TycoWorld.GameSettings.Tests
{
    public class MenuNavigatorTests
    {
        MenuNavigator nav;
        readonly List<string> changes = new List<string>();

        [SetUp]
        public void SetUp()
        {
            nav = new MenuNavigator();
            changes.Clear();
            nav.PageChanged += (from, to) => changes.Add(from + ">" + to);
        }

        [Test]
        public void StartsClosed() => Assert.AreEqual(MenuPage.Closed, nav.Page);

        [Test]
        public void Escape_OpensAndClosesTheMainPage()
        {
            nav.Escape();
            Assert.AreEqual(MenuPage.Main, nav.Page);
            nav.Escape();
            Assert.AreEqual(MenuPage.Closed, nav.Page);
            CollectionAssert.AreEqual(new[] { "Closed>Main", "Main>Closed" }, changes);
        }

        [TestCase(MenuPage.Settings)]
        [TestCase(MenuPage.ConfirmQuit)]
        public void Escape_FromASubPage_ReturnsToMain(MenuPage page)
        {
            nav.Go(MenuPage.Main);
            nav.Go(page);
            nav.Escape();
            Assert.AreEqual(MenuPage.Main, nav.Page);
        }

        [Test]
        public void Go_SamePage_RaisesNothing()
        {
            nav.Go(MenuPage.Closed);
            Assert.AreEqual(0, changes.Count);
        }

        [Test]
        public void Escape_PressedRepeatedly_StaysConsistent()
        {
            for (int i = 0; i < 5; i++) nav.Escape();
            Assert.AreEqual(MenuPage.Main, nav.Page);
            Assert.AreEqual(5, changes.Count);
            Assert.AreEqual("Closed>Main", changes[4]);
        }
    }
}
