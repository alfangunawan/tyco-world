using UnityEngine;
using UnityEngine.SceneManagement;

namespace TycoWorld.WorldGen
{
    /// <summary>Loads the generated world scene additively when the main world scene starts.</summary>
    public sealed class WorldSceneLoader : MonoBehaviour
    {
        public const string DefaultScenePath = "Assets/WorldGen/Generated/World_Generated.unity";

        [SerializeField] string generatedScenePath = DefaultScenePath;

        /// <summary>True when the generated scene cannot be loaded (never generated, or missing from Build Settings).</summary>
        public bool LoadFailed { get; private set; }

        public void Configure(string scenePath) => generatedScenePath = scenePath;

        void Awake() => TryLoad();

        public bool TryLoad()
        {
            if (SceneManager.GetSceneByPath(generatedScenePath).isLoaded) return true;
            int buildIndex = SceneUtility.GetBuildIndexByScenePath(generatedScenePath);
            bool missing = buildIndex < 0;
#if UNITY_EDITOR
            // The generated folder is git-ignored: on a fresh clone Build Settings still list the scene but the file does not exist.
            missing |= !System.IO.File.Exists(System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, generatedScenePath));
#endif
            if (missing)
            {
                LoadFailed = true;
                Debug.LogError($"[WorldGen] '{generatedScenePath}' belum di-generate atau tidak ada di Build Settings. Jalankan Tyco World > World Generator > Generate.");
                return false;
            }
            SceneManager.LoadScene(buildIndex, LoadSceneMode.Additive);
            return true;
        }
    }
}
