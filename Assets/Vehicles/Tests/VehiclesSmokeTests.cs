using NUnit.Framework;
using TycoWorld.Vehicles.Authoring;

namespace TycoWorld.Vehicles.Tests
{
    public class VehiclesSmokeTests
    {
        [Test]
        public void Assemblies_Load()
        {
            Assert.AreEqual("TycoWorld.Vehicles", typeof(VehicleIntent).Assembly.GetName().Name);
            Assert.AreEqual("TycoWorld.Vehicles.Editor", typeof(VehiclePaths).Assembly.GetName().Name);
        }
    }
}
