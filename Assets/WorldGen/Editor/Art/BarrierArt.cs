using UnityEditor;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>
    /// Prefabs for the dead-end props (spec road-clearance §4), in <see cref="BarrierBuilder"/>'s variant order: the potted
    /// plant, the striped concrete block and the road-closed barricade with its "JALAN DITUTUP" board. Each comes from a
    /// downloaded CC0 model (Assets/WorldGen/Art/ThirdParty/Barriers, see SOURCES.md), scaled to size, standing on the
    /// ground, with a box collider. Idempotent: rebuilding overwrites the same prefab assets.
    /// </summary>
    public static class BarrierArt
    {
        public const string ModelsFolder = "Assets/WorldGen/Art/ThirdParty/Barriers/";
        public const string PrefabFolder = "Assets/WorldGen/Prefabs/Barriers";
        public const string SignText = "JALAN DITUTUP";
        public const float PotHeight = 0.7f, ConcreteHeight = 0.8f, BarricadeHeight = 0.9f;

        /// <param name="boardMaterial">The board's face behind the text; a plain white one when null.</param>
        public static GameObject[] BuildPrefabs(Material boardMaterial)
        {
            AssetFolders.Ensure(PrefabFolder);
            return new[]
            {
                Prefab("Barrier_Pot", "Kenney_PottedPlant.obj", PotHeight, null),
                Prefab("Barrier_Concrete", "Quaternius_BarrierSingle.obj", ConcreteHeight, null),
                Prefab("Barrier_RoadClosed", "Quaternius_TrafficBarrier.obj", BarricadeHeight, root => AddSign(root, boardMaterial))
            };
        }

        static GameObject Prefab(string name, string model, float height, System.Action<GameObject> extra)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsFolder + model);
            if (source == null)
            {
                Debug.LogWarning("[WorldGen] Model pembatas tidak ada: " + ModelsFolder + model);
                return null;
            }
            var root = new GameObject(name);
            try
            {
                var body = Object.Instantiate(source, root.transform, false);
                body.name = "Model";
                var b = BoundsOf(body);
                if (b.size.y > 1e-4f) body.transform.localScale *= height / b.size.y;
                b = BoundsOf(body);
                body.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
                extra?.Invoke(root);
                b = BoundsOf(root);
                var box = root.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = b.size;
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/" + name + ".prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// A white board above the barricade, red letters on its +Z face (BarrierBuilder turns +Z toward the road). One
        /// line only: the text shader draws both sides, so a second line on the back would show through mirrored.
        /// </summary>
        static void AddSign(GameObject root, Material boardMaterial)
        {
            float top = BoundsOf(root).max.y;
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Board";
            Object.DestroyImmediate(board.GetComponent<Collider>());
            board.transform.SetParent(root.transform, false);
            board.transform.localPosition = new Vector3(0f, top + 0.24f, 0f);
            board.transform.localScale = new Vector3(1.3f, 0.36f, 0.04f);
            var renderer = board.GetComponent<MeshRenderer>();
            if (boardMaterial != null) renderer.sharedMaterial = boardMaterial;
            var text = new GameObject("Text").AddComponent<TextMesh>();
            text.transform.SetParent(root.transform, false);
            // A TextMesh reads from behind its forward: forward -Z reads from +Z.
            text.transform.localPosition = new Vector3(0f, top + 0.24f, 0.025f);
            text.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            text.text = SignText;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 48;
            text.characterSize = 0.022f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(0.78f, 0.06f, 0.06f);
            text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
        }

        static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
