using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>Small helpers for primitive-built props: a renderer-only part, or one that keeps its collider.</summary>
    public static class Primitives
    {
        public static GameObject Part(PrimitiveType type, Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material,
            bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!keepCollider) DestroyCollider(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        public static void DestroyCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider == null) return;
            if (Application.isPlaying) Object.Destroy(collider);
            else Object.DestroyImmediate(collider);
        }
    }
}
