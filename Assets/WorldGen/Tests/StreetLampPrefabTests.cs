using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class StreetLampPrefabTests
    {
        [Test]
        public void LampPrefab_HasStreetLampMarker()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorldGenPaths.PrefabsFolder + "/StreetLamp_A.prefab");
            Assert.IsNotNull(prefab, "Jalankan Build Art Assets dulu.");
            var lamp = prefab.GetComponent<StreetLamp>();
            Assert.IsNotNull(lamp, "StreetLamp_A belum punya StreetLamp.");
            Assert.IsNotNull(lamp.Light);
            Assert.AreEqual(LightType.Point, lamp.Light.type);
            Assert.AreNotSame(prefab.transform, lamp.Light.transform);
            Assert.IsTrue(lamp.Light.transform.IsChildOf(prefab.transform));
            Assert.AreSame(prefab.GetComponent<MeshRenderer>(), lamp.HeadRenderer);
            Assert.AreEqual("M_Lamp_Head", lamp.HeadRenderer.sharedMaterials[lamp.HeadMaterialIndex].name);
        }
    }
}
