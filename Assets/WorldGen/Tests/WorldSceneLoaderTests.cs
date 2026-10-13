using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TycoWorld.WorldGen.Tests
{
    public class WorldSceneLoaderTests
    {
        [Test]
        public void MissingGeneratedScene_FailsWithOneClearError()
        {
            var go = new GameObject("loader");
            try
            {
                var loader = go.AddComponent<WorldSceneLoader>();
                loader.Configure("Assets/WorldGen/Generated/DoesNotExist.unity");
                LogAssert.Expect(LogType.Error, new Regex("belum di-generate"));
                Assert.IsFalse(loader.TryLoad());
                Assert.IsTrue(loader.LoadFailed);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
