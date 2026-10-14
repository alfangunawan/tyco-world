using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace TycoWorld.Player.Authoring
{
    /// <summary>Builds the unsaved Player hierarchy (spec #2 §11).</summary>
    public static class PlayerRigBuilder
    {
        public static GameObject CreateRig(PlayerTuning tuning, FootstepBank bank, PanelSettings panel, AudioClip flashlightClick)
        {
            var root = new GameObject("Player") { layer = PlayerLayers.Player };
            var cc = root.AddComponent<CharacterController>();
            cc.height = tuning.body.height;
            cc.radius = tuning.body.radius;
            cc.center = new Vector3(0f, tuning.body.height * 0.5f, 0f);
            cc.stepOffset = tuning.body.stepOffset;
            cc.skinWidth = tuning.body.skinWidth;
            cc.slopeLimit = tuning.body.slopeLimit;
            cc.minMoveDistance = 0f;
            var stepSource = Add2DSource(root);

            var pivot = new GameObject("CameraPivot") { layer = PlayerLayers.Player };
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, tuning.crouch.standingEyeHeight, 0f);

            var camGo = new GameObject("Camera") { layer = PlayerLayers.Player, tag = "MainCamera" };
            camGo.transform.SetParent(pivot.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = tuning.look.fieldOfView;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = new Color32(0x0E, 0x12, 0x18, 0xFF);
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<Interactor>();

            var flashGo = new GameObject("Flashlight") { layer = PlayerLayers.Player };
            flashGo.transform.SetParent(camGo.transform, false);
            flashGo.transform.localPosition = new Vector3(0.18f, -0.15f, 0.1f);
            var light = flashGo.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = tuning.flashlight.outerAngle;
            light.innerSpotAngle = tuning.flashlight.innerAngle;
            light.range = tuning.flashlight.range;
            light.intensity = tuning.flashlight.intensity;
            light.color = tuning.flashlight.color;
            light.shadows = LightShadows.Soft;
            light.enabled = tuning.flashlight.startOn;
            flashGo.AddComponent<Flashlight>().Configure(Add2DSource(flashGo), flashlightClick);

            var player = root.AddComponent<FirstPersonPlayer>();
            player.Configure(tuning, pivot.transform, camGo.transform);
            root.AddComponent<FootstepAudio>().Configure(bank, stepSource);
            root.AddComponent<UIDocument>().panelSettings = panel;
            root.AddComponent<PlayerHud>();
            root.AddComponent<PlayerSpawner>().Configure(player, null);
            return root;
        }

        static AudioSource Add2DSource(GameObject go)
        {
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }
    }
}
