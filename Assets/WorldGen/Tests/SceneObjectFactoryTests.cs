using System.Collections.Generic;
using NUnit.Framework;
using TycoWorld.WorldGen.Authoring;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine;

namespace TycoWorld.WorldGen.Tests
{
    public class SceneObjectFactoryTests
    {
        readonly List<GameObject> created = new List<GameObject>();
        readonly List<Object> createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
            created.Clear();
            foreach (var o in createdObjects) if (o != null) Object.DestroyImmediate(o);
            createdObjects.Clear();
        }

        GameObject Make(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }

        [Test]
        public void AddEdgeWalls_CreatesFourStaticInvisibleBoxes()
        {
            var parent = Make("WorldEdge").transform;
            SceneObjectFactory.AddEdgeWalls(parent, new Rect(0f, 0f, 100f, 50f));
            Assert.AreEqual(4, parent.childCount);
            CollectionAssert.AreEquivalent(new[] { "Edge_West", "Edge_East", "Edge_South", "Edge_North" },
                new[] { parent.GetChild(0).name, parent.GetChild(1).name, parent.GetChild(2).name, parent.GetChild(3).name });
            foreach (Transform child in parent)
            {
                Assert.IsNotNull(child.GetComponent<BoxCollider>());
                Assert.IsNull(child.GetComponent<MeshRenderer>());
                Assert.IsTrue(GameObjectUtility.AreStaticEditorFlagsSet(child.gameObject, StaticEditorFlags.OccludeeStatic));
            }
            var west = parent.Find("Edge_West").GetComponent<BoxCollider>();
            Assert.AreEqual(0f, west.center.x + west.size.x * 0.5f, 1e-4f);
        }

        [Test]
        public void ApplySurface_DefaultAddsNothing()
        {
            var go = Make("building");
            SceneObjectFactory.ApplySurface(go, SurfaceType.Default);
            Assert.IsNull(go.GetComponent<GroundSurface>());
        }

        [Test]
        public void ApplySurface_AddsOrUpdatesComponent()
        {
            var go = Make("road");
            SceneObjectFactory.ApplySurface(go, SurfaceType.Asphalt);
            Assert.AreEqual(SurfaceType.Asphalt, go.GetComponent<GroundSurface>().Type);
            SceneObjectFactory.ApplySurface(go, SurfaceType.Paving);
            Assert.AreEqual(1, go.GetComponents<GroundSurface>().Length);
            Assert.AreEqual(SurfaceType.Paving, go.GetComponent<GroundSurface>().Type);
        }

        static MeshData Quad(float y)
        {
            var m = new MeshData(1);
            m.AddQuad(0, new Vector3(0f, y, 0f), new Vector3(1f, y, 0f), new Vector3(1f, y, 1f), new Vector3(0f, y, 1f),
                Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector3.up);
            return m;
        }

        static BuiltFeature Feature(FeatureKind kind, string group, long id, MeshData mesh, MeshData collider)
        {
            return new BuiltFeature
            {
                Kind = kind, Group = group, OsmId = id, Name = group, Chunk = Vector2Int.zero, Mesh = mesh, ColliderMesh = collider,
                Materials = new Material[1], AddCollider = true, HasOsmIdentity = false
            };
        }

        GameObject Populated(out Dictionary<BuiltFeature, MeshAsset> assets, out BuiltFeature edge, out BuiltFeature wall, out Mesh wallRender, out Mesh wallCollider)
        {
            var settings = DefaultSettingsFactory.CreateInMemory();
            createdObjects.Add(settings);
            edge = Feature(FeatureKind.RoadEdge, FeatureGroups.RoadEdges, -1, Quad(0.1f), null);
            wall = Feature(FeatureKind.Frontage, FeatureGroups.Frontage, -2, Quad(1f), Quad(2f));
            var edgeMesh = edge.Mesh.ToMesh("edge", out var edgeUsed);
            wallRender = wall.Mesh.ToMesh("wall", out var wallUsed);
            wallCollider = wall.ColliderMesh.ToMesh("wallCollider", out _);
            assets = new Dictionary<BuiltFeature, MeshAsset>
            {
                { edge, new MeshAsset(edgeMesh, edgeUsed) },
                { wall, new MeshAsset(wallRender, wallUsed, wallCollider) }
            };
            var world = new BuiltWorld { Bounds = new Rect(0f, 0f, 100f, 100f), ChunkSize = 100f, Ground = new MeshData(1) };
            world.Features.Add(edge);
            world.Features.Add(wall);
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var groundMesh = new Mesh();
            createdObjects.Add(groundMesh);
            var root = SceneObjectFactory.Populate(scene, world, assets, groundMesh, settings, new GenerationReport(), null);
            created.Add(root);
            return root;
        }

        static MeshRenderer RendererOf(GameObject root, string group) => root.transform.Find(group + "/" + WorldMeshPipeline.ChunkName(Vector2Int.zero)).GetChild(0).GetComponent<MeshRenderer>();

        [Test]
        public void RoadEdgeRenderers_CastNoShadows_WallsStillDo()
        {
            var root = Populated(out _, out _, out _, out _, out _);
            Assert.AreEqual(ShadowCastingMode.Off, RendererOf(root, FeatureGroups.RoadEdges).shadowCastingMode);
            Assert.AreEqual(ShadowCastingMode.On, RendererOf(root, FeatureGroups.Frontage).shadowCastingMode);
        }

        [Test]
        public void ColliderMesh_GoesToTheMeshColliderAndRenderMeshToTheFilter()
        {
            var root = Populated(out var assets, out var edge, out var wall, out var wallRender, out var wallCollider);
            var go = RendererOf(root, FeatureGroups.Frontage).gameObject;
            Assert.AreSame(wallRender, go.GetComponent<MeshFilter>().sharedMesh);
            Assert.AreSame(wallCollider, go.GetComponent<MeshCollider>().sharedMesh);
            var edgeGo = RendererOf(root, FeatureGroups.RoadEdges).gameObject;
            Assert.AreSame(edgeGo.GetComponent<MeshFilter>().sharedMesh, edgeGo.GetComponent<MeshCollider>().sharedMesh, "no collider mesh: collide with the render mesh");
        }

        [Test]
        public void ColliderOnlyFeature_IsWritten_AndBecomesAnInvisibleCollider()
        {
            const string folder = "Assets/WorldGen/Tests/_TmpChunkWriterColliderOnly";
            try
            {
                var settings = DefaultSettingsFactory.CreateInMemory();
                createdObjects.Add(settings);
                var block = Feature(FeatureKind.Barrier, FeatureGroups.Barriers, -3, new MeshData(1), Quad(1f));
                var world = new BuiltWorld { Bounds = new Rect(0f, 0f, 100f, 100f), ChunkSize = 100f, Ground = Quad(0f) };
                world.Features.Add(block);
                var assets = ChunkAssetWriter.Write(world, folder, out var groundMesh);
                Assert.IsTrue(assets.ContainsKey(block), "written although it draws nothing");
                Assert.IsNotNull(assets[block].Collider);
                var root = SceneObjectFactory.Populate(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), world, assets, groundMesh, settings,
                    new GenerationReport(), null);
                created.Add(root);
                var go = root.transform.Find(FeatureGroups.Barriers + "/" + WorldMeshPipeline.ChunkName(Vector2Int.zero)).GetChild(0).gameObject;
                Assert.AreSame(assets[block].Collider, go.GetComponent<MeshCollider>().sharedMesh);
                Assert.IsNull(go.GetComponent<MeshRenderer>());
                Assert.IsNull(go.GetComponent<MeshFilter>());
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void ChunkAssetWriter_WritesTheColliderAsASecondSubAsset()
        {
            const string folder = "Assets/WorldGen/Tests/_TmpChunkWriter";
            try
            {
                var wall = Feature(FeatureKind.Frontage, FeatureGroups.Frontage, -2, Quad(1f), Quad(2f));
                var plain = Feature(FeatureKind.RoadEdge, FeatureGroups.RoadEdges, -1, Quad(0.1f), null);
                var world = new BuiltWorld { Bounds = new Rect(0f, 0f, 100f, 100f), ChunkSize = 100f, Ground = Quad(0f) };
                world.Features.Add(plain);
                world.Features.Add(wall);
                var assets = ChunkAssetWriter.Write(world, folder, out _);
                Assert.IsNotNull(assets[wall].Collider);
                Assert.IsNull(assets[plain].Collider);
                Assert.AreNotSame(assets[wall].Mesh, assets[wall].Collider);
                int meshes = 0;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(folder + "/" + WorldMeshPipeline.ChunkName(Vector2Int.zero) + ".asset")) if (o is Mesh) meshes++;
                Assert.AreEqual(3, meshes, "edge mesh + wall mesh + wall collider");
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
