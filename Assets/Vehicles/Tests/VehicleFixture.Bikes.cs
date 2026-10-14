using TycoWorld.Player;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    internal sealed partial class VehicleFixture
    {
        /// <summary>An initialized first-person player on the Ignore Raycast layer, at Origin + local.</summary>
        public FirstPersonPlayer CreatePlayer(Vector3 local, float yaw = 0f)
        {
            var tuning = Track(ScriptableObject.CreateInstance<PlayerTuning>());
            var root = Track(new GameObject("Player"));
            root.layer = PlayerLayers.Player;
            root.transform.SetPositionAndRotation(Origin + local, Quaternion.Euler(0f, yaw, 0f));
            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(0f, 1.6f, 0f);
            var view = new GameObject("Camera").transform;
            view.SetParent(pivot, false);
            var player = root.AddComponent<FirstPersonPlayer>();
            player.Configure(tuning, pivot, view);
            player.Initialize();
            player.SetIntentSource(new ScriptedIntentSource());
            player.Teleport(Origin + local, yaw);
            return player;
        }

        /// <summary>A code-built bike (CharacterController, seat, interactable; no visuals or audio) at Origin + local.</summary>
        public Motorbike CreateBike(Vector3 local, float yaw, BikeTuning tuning)
        {
            var root = Track(new GameObject("Motorbike"));
            root.transform.SetPositionAndRotation(Origin + local, Quaternion.Euler(0f, yaw, 0f));
            root.AddComponent<CharacterController>();
            var seat = new GameObject("Seat").transform;
            seat.SetParent(root.transform, false);
            seat.localPosition = new Vector3(0f, 0.86f, -0.2f);
            var bike = root.AddComponent<Motorbike>();
            bike.Configure(tuning, seat, null, null);
            root.AddComponent<MotorbikeInteractable>();
            bike.Initialize();
            return bike;
        }
    }
}
