namespace TycoWorld.Player.Authoring
{
    /// <summary>Project-relative paths of the player sub-project.</summary>
    public static class PlayerPaths
    {
        public const string Root = "Assets/Player";
        public const string ConfigFolder = Root + "/Config";
        public const string TuningAsset = ConfigFolder + "/PlayerTuning.asset";
        public const string FootstepBankAsset = ConfigFolder + "/FootstepBank.asset";
        public const string AudioFolder = Root + "/Audio";
        public const string FootstepAudioFolder = AudioFolder + "/Footsteps";
        public const string InteractionAudioFolder = AudioFolder + "/Interactions";
        public const string AudioSourcesFile = AudioFolder + "/SOURCES.md";
        public const string UiFolder = Root + "/UI";
        public const string PanelSettingsAsset = UiFolder + "/PlayerHudPanel.asset";
        public const string ThemeAsset = UiFolder + "/PlayerHudTheme.tss";
        public const string MaterialsFolder = Root + "/Materials";
        public const string PrefabsFolder = Root + "/Prefabs";
        public const string MeshesFolder = PrefabsFolder + "/Meshes";
        public const string PlayerPrefab = PrefabsFolder + "/Player.prefab";
        public const string GatePrefab = PrefabsFolder + "/Gate.prefab";
        public const string DoorPrefab = PrefabsFolder + "/KosDoor.prefab";
        public const string MotorPrefab = PrefabsFolder + "/Motor_Placeholder.prefab";
        public const string InputActionsAsset = "Assets/InputSystem_Actions.inputactions";
    }
}
