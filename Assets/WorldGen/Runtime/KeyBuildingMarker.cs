using UnityEngine;

namespace TycoWorld.WorldGen
{
    /// <summary>Marks a key building that may later be replaced by a hand-made prefab with an interior.</summary>
    [DisallowMultipleComponent]
    public sealed class KeyBuildingMarker : MonoBehaviour
    {
        [SerializeField] long osmId;
        [SerializeField] string label = "";

        public long OsmId => osmId;
        public string Label => label;

        public void Initialize(long id, string buildingLabel)
        {
            osmId = id;
            label = buildingLabel ?? "";
        }
    }
}
