using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>Where the player appears (feet position + facing). Manual content in World.unity.</summary>
    public sealed class PlayerSpawnPoint : MonoBehaviour
    {
        [SerializeField] string id = "kos";
        [SerializeField] string arrivalMessage = "Kos Raflesia 3";

        public string Id => id;
        public string ArrivalMessage => arrivalMessage;
        public Vector3 Feet => transform.position;
        public float Yaw => transform.eulerAngles.y;

        public void Configure(string spawnId, string message)
        {
            id = spawnId ?? "";
            arrivalMessage = message ?? "";
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.35f);
            Gizmos.DrawLine(transform.position + Vector3.up * 1.6f, transform.position + Vector3.up * 1.6f + transform.forward);
        }
    }
}
