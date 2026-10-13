using UnityEngine;

namespace TycoWorld.Player.Tests
{
    /// <summary>A code-built player for physics tests; add modules after Create and before Init.</summary>
    internal sealed class TestRig
    {
        public readonly GameObject Root;
        public readonly FirstPersonPlayer Player;
        public readonly Transform Pivot, View;
        public readonly PlayerTuning Tuning;
        public readonly ScriptedIntentSource Intents = new ScriptedIntentSource();

        TestRig(GameObject root, FirstPersonPlayer player, Transform pivot, Transform view, PlayerTuning tuning)
        {
            Root = root;
            Player = player;
            Pivot = pivot;
            View = view;
            Tuning = tuning;
        }

        public Vector3 Local => Root.transform.position - PhysicsFixture.Origin;

        public static TestRig Create(PhysicsFixture fx, Vector3 localFeet, float yaw = 0f)
        {
            var tuning = fx.Track(ScriptableObject.CreateInstance<PlayerTuning>());
            var root = fx.Track(new GameObject("Player"));
            root.layer = PlayerLayers.Player;
            root.transform.SetPositionAndRotation(PhysicsFixture.Origin + localFeet, Quaternion.Euler(0f, yaw, 0f));
            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(0f, 1.6f, 0f);
            var view = new GameObject("Camera").transform;
            view.SetParent(pivot, false);
            var player = root.AddComponent<FirstPersonPlayer>();
            player.Configure(tuning, pivot, view);
            return new TestRig(root, player, pivot, view, tuning);
        }

        public TestRig Init()
        {
            Player.Initialize();
            Player.SetIntentSource(Intents);
            Player.Teleport(Root.transform.position, Root.transform.eulerAngles.y);
            return this;
        }

        public void Run(float seconds, float dt = 1f / 60f)
        {
            int frames = Mathf.Max(1, Mathf.RoundToInt(seconds / dt));
            for (int i = 0; i < frames; i++) Player.Tick(dt);
        }
    }
}
