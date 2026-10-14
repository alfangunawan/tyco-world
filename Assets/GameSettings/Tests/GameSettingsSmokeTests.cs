using NUnit.Framework;
using TycoWorld.GameSettings.Authoring;

namespace TycoWorld.GameSettings.Tests
{
    public class GameSettingsSmokeTests
    {
        [Test]
        public void Assemblies_Load()
        {
            Assert.AreEqual("TycoWorld.GameSettings", typeof(FrameStats).Assembly.GetName().Name);
            Assert.AreEqual("TycoWorld.GameSettings.Editor", typeof(GameSettingsPaths).Assembly.GetName().Name);
        }
    }
}
