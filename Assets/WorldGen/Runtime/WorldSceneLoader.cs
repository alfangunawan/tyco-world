using UnityEngine;
using UnityEngine.SceneManagement;

namespace TycoWorld.WorldGen
{
    /// <summary>Loads the generated world scene additively when the main world scene starts.</summary>
    public sealed class WorldSceneLoader : MonoBehaviour
    {
        public const string DefaultScenePath = "Assets/WorldGen/Generated/World_Generated.unity";

        [SerializeField] string generatedScenePath = DefaultScenePath;

        void Awake()
        {
            if (SceneManager.GetSceneByPath(generatedScenePath).isLoaded) return;
            int buildIndex = SceneUtility.GetBuildIndexByScenePath(generatedScenePath);
            if (buildIndex < 0)
            {
                Debug.LogError($"[WorldGen] '{generatedScenePath}' tidak ada di Build Settings. Jalankan Generate dulu.");
                return;
            }
            SceneManager.LoadScene(buildIndex, LoadSceneMode.Additive);
        }
    }
}
