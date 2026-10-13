using UnityEngine;

namespace TycoWorld.WorldGen
{
    /// <summary>Tells footsteps (and later vehicles) what a walkable collider is made of.</summary>
    [DisallowMultipleComponent]
    public sealed class GroundSurface : MonoBehaviour
    {
        [SerializeField] SurfaceType type;

        public SurfaceType Type => type;

        public void Initialize(SurfaceType surface) => type = surface;
    }
}
