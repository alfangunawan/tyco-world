using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// Small helpers for primitive-built props: a renderer-only part, or one that keeps its collider. Spheres, capsules
    /// and cylinders render with <see cref="LowPolyMeshes"/> (same shape, a tenth of the triangles); colliders stay analytic.
    /// </summary>
    public static class Primitives
    {
        public static GameObject Part(PrimitiveType type, Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material,
            bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var low = LowPolyMeshes.For(type);
            if (low != null) go.GetComponent<MeshFilter>().sharedMesh = low;
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
