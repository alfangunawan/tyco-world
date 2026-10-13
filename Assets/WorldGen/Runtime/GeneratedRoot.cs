using UnityEngine;

namespace TycoWorld.WorldGen
{
    /// <summary>Marks the root GameObject owned by the world generator.</summary>
    [DisallowMultipleComponent]
    public sealed class GeneratedRoot : MonoBehaviour
    {
        [SerializeField] string generatedAtUtc = "";
        [SerializeField, TextArea(3, 30)] string summary = "";

        public string GeneratedAtUtc => generatedAtUtc;
        public string Summary => summary;

        public void Initialize(string atUtc, string reportSummary)
        {
            generatedAtUtc = atUtc ?? "";
            summary = reportSummary ?? "";
        }
    }
}
