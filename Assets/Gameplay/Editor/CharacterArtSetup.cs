using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TycoWorld.Gameplay.Authoring
{
    /// <summary>
    /// Builds the character art the game reads (spec #7): the Kampung AnimatorController from the Universal Animation
    /// Library clips, a transparent blue galon material, and the CharacterLibrary asset listing the body models (the
    /// Modular Men/Women FBX files when present, else the library's own mannequin). Idempotent; run by Gameplay Setup.
    /// </summary>
    public static class CharacterArtSetup
    {
        public const string ArtFolder = GameplayPaths.Root + "/Art/Characters";
        public const string ControllerAsset = ArtFolder + "/Kampung.controller";
        public const string GalonMaterialAsset = ArtFolder + "/Galon_Blue.mat";
        public const string LibraryAsset = GameplayPaths.DataFolder + "/CharacterLibrary.asset";
        public const string AnimationModel = CharacterImportRules.AnimationFolder + "UAL_Standard.fbx";
        public const string BodiesFolder = CharacterImportRules.QuaterniusFolder + "Characters";
        public const string GalonModel = "Assets/Gameplay/Art/ThirdParty/PolyPizza/Galon.obj";
        public const string DispenserModel = "Assets/Gameplay/Art/ThirdParty/PolyPizza/WaterDispenser.obj";

        /// <summary>Speeds (m/s) at which the locomotion blend reaches each clip.</summary>
        public const float WalkSpeed = 1.3f, JogSpeed = 2.6f, SprintSpeed = 5.2f;

        [MenuItem("Tyco World/Gameplay/Build Character Art")]
        static void Menu()
        {
            var library = Run(out string error);
            if (library == null) EditorUtility.DisplayDialog("Character Art gagal", error, "OK");
            else Debug.Log("[Gameplay] Character art: " + library.bodies.Length + " tubuh, controller " + ControllerAsset);
        }

        public static CharacterLibrary Run(out string error)
        {
            error = "";
            var clips = AssetDatabase.LoadAllAssetsAtPath(AnimationModel).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview", System.StringComparison.Ordinal)).ToDictionary(c => c.name);
            if (clips.Count == 0)
            {
                error = "Animasi tidak ditemukan di " + AnimationModel + ".";
                return null;
            }
            Directory.CreateDirectory(ArtFolder);
            var controller = BuildController(clips, out error);
            if (controller == null) return null;
            var library = AssetDatabase.LoadAssetAtPath<CharacterLibrary>(LibraryAsset);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<CharacterLibrary>();
                AssetDatabase.CreateAsset(library, LibraryAsset);
            }
            library.controller = controller;
            var bodies = FindBodies(out var women);
            library.bodies = bodies.ToArray();
            library.womenBodies = women.ToArray();
            library.galon = AssetDatabase.LoadAssetAtPath<GameObject>(GalonModel);
            library.dispenser = AssetDatabase.LoadAssetAtPath<GameObject>(DispenserModel);
            library.galonMaterial = EnsureGalonMaterial();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return library;
        }

        /// <summary>
        /// The Modular Men bodies and, apart, the Modular Women ones (by file name prefix); the animation library's
        /// mannequin until any exist.
        /// </summary>
        static List<GameObject> FindBodies(out List<GameObject> women)
        {
            var men = new List<GameObject>();
            women = new List<GameObject>();
            if (AssetDatabase.IsValidFolder(BodiesFolder))
                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { BodiesFolder }).OrderBy(g => AssetDatabase.GUIDToAssetPath(g)))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (model == null) continue;
                    if (Path.GetFileName(path).StartsWith("Women", System.StringComparison.OrdinalIgnoreCase)) women.Add(model);
                    else men.Add(model);
                }
            if (men.Count == 0) men.AddRange(women);
            if (men.Count == 0)
            {
                var mannequin = AssetDatabase.LoadAssetAtPath<GameObject>(AnimationModel);
                if (mannequin != null) men.Add(mannequin);
            }
            return men;
        }

        static Material EnsureGalonMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(GalonMaterialAsset);
            bool created = m == null;
            if (created) m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Galon_Blue" };
            // Clear blue plastic, as refill galons are.
            m.SetColor("_BaseColor", new Color(0.2f, 0.45f, 0.82f, 0.42f));
            m.SetFloat("_Smoothness", 0.55f);
            // URP Lit, transparent alpha blend.
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (created) AssetDatabase.CreateAsset(m, GalonMaterialAsset);
            else EditorUtility.SetDirty(m);
            return m;
        }

        static AnimatorController BuildController(Dictionary<string, AnimationClip> clips, out string error)
        {
            error = "";
            string[] needed = { "Idle_Loop", "Walk_Loop", "Jog_Fwd_Loop", "Sprint_Loop", "Idle_Talking_Loop", "Sitting_Enter", "Sitting_Idle_Loop", "Sitting_Exit", "Driving_Loop", "PickUp_Table" };
            foreach (var n in needed)
                if (!clips.ContainsKey(n))
                {
                    error = "Klip animasi " + n + " tidak ada di " + AnimationModel + ".";
                    return null;
                }
            // Rebuilt from scratch each time (the asset keeps its path; the library re-points to it).
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerAsset) != null) AssetDatabase.DeleteAsset(ControllerAsset);
            var c = AnimatorController.CreateAnimatorControllerAtPath(ControllerAsset);
            c.AddParameter("Speed", AnimatorControllerParameterType.Float);
            c.AddParameter("Sit", AnimatorControllerParameterType.Bool);
            c.AddParameter("Drive", AnimatorControllerParameterType.Bool);
            c.AddParameter("Talk", AnimatorControllerParameterType.Bool);
            c.AddParameter("PickUp", AnimatorControllerParameterType.Trigger);
            var layers = c.layers;
            layers[0].iKPass = true;
            c.layers = layers;
            var sm = c.layers[0].stateMachine;

            var loco = c.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(clips["Idle_Loop"], 0f);
            tree.AddChild(clips["Walk_Loop"], WalkSpeed);
            tree.AddChild(clips["Jog_Fwd_Loop"], JogSpeed);
            tree.AddChild(clips["Sprint_Loop"], SprintSpeed);
            sm.defaultState = loco;

            var talk = sm.AddState("Talk");
            talk.motion = clips["Idle_Talking_Loop"];
            var sitEnter = sm.AddState("SitEnter");
            sitEnter.motion = clips["Sitting_Enter"];
            var sitIdle = sm.AddState("SitIdle");
            sitIdle.motion = clips["Sitting_Idle_Loop"];
            var sitExit = sm.AddState("SitExit");
            sitExit.motion = clips["Sitting_Exit"];
            var drive = sm.AddState("Drive");
            drive.motion = clips["Driving_Loop"];
            var pickUp = sm.AddState("PickUp");
            pickUp.motion = clips["PickUp_Table"];

            AnimatorStateTransition T(AnimatorState from, AnimatorState to, float duration, bool exitTime = false)
            {
                var t = from.AddTransition(to);
                t.duration = duration;
                t.hasExitTime = exitTime;
                if (exitTime) t.exitTime = 0.95f;
                return t;
            }

            T(loco, talk, 0.2f).AddCondition(AnimatorConditionMode.If, 0f, "Talk");
            T(talk, loco, 0.2f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Talk");
            T(loco, sitEnter, 0.15f).AddCondition(AnimatorConditionMode.If, 0f, "Sit");
            T(talk, sitEnter, 0.15f).AddCondition(AnimatorConditionMode.If, 0f, "Sit");
            T(sitEnter, sitIdle, 0.1f, exitTime: true);
            T(sitEnter, sitExit, 0.15f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Sit");
            T(sitIdle, sitExit, 0.15f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Sit");
            T(sitExit, loco, 0.2f, exitTime: true);
            T(loco, pickUp, 0.1f).AddCondition(AnimatorConditionMode.If, 0f, "PickUp");
            T(talk, pickUp, 0.1f).AddCondition(AnimatorConditionMode.If, 0f, "PickUp");
            T(pickUp, loco, 0.2f, exitTime: true);
            // Riding wins from anywhere; leaving it goes back to walking.
            var toDrive = sm.AddAnyStateTransition(drive);
            toDrive.duration = 0.15f;
            toDrive.canTransitionToSelf = false;
            toDrive.AddCondition(AnimatorConditionMode.If, 0f, "Drive");
            T(drive, loco, 0.2f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Drive");
            EditorUtility.SetDirty(c);
            return c;
        }
    }
}
