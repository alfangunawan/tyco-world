using UnityEngine;

namespace TycoWorld.WorldGen
{
    /// <summary>Marks a street lamp for the day cycle: its point light and the renderer slot of the glowing head.</summary>
    [DisallowMultipleComponent]
    public sealed class StreetLamp : MonoBehaviour
    {
        [SerializeField] Light lampLight;
        [SerializeField] Renderer headRenderer;
        [SerializeField] int headMaterialIndex;

        public Light Light => lampLight;
        public Renderer HeadRenderer => headRenderer;
        public int HeadMaterialIndex => headMaterialIndex;

        public void Initialize(Light light, Renderer renderer, int materialIndex)
        {
            lampLight = light;
            headRenderer = renderer;
            headMaterialIndex = materialIndex;
        }
    }
}
