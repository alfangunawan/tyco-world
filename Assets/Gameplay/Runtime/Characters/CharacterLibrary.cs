using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// The downloaded character art the game uses (spec #7): the Kampung animator controller, the humanoid body models
    /// people are made from, and the galon and water dispenser props. Filled by Tyco World > Gameplay Setup.
    /// </summary>
    [CreateAssetMenu(menuName = "Tyco World/Character Library", fileName = "CharacterLibrary")]
    public sealed class CharacterLibrary : ScriptableObject
    {
        public RuntimeAnimatorController controller;
        [Tooltip("Humanoid model roots (FBX prefabs) for men; people pick one from their seed.")]
        public GameObject[] bodies = new GameObject[0];
        [Tooltip("Bodies for women; empty falls back to the men's.")]
        public GameObject[] womenBodies = new GameObject[0];
        public GameObject galon;
        public Material galonMaterial;
        public GameObject dispenser;
        [Tooltip("People are scaled to about this height (metres).")]
        public float targetHeight = 1.66f;

        public bool HasPeople => controller != null && bodies != null && bodies.Length > 0;
    }
}
