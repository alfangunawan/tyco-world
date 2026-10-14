using NUnit.Framework;
using TycoWorld.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace TycoWorld.Vehicles.Tests
{
    public class MotorbikeTests
    {
        const float Dt = 0.02f;
        VehicleFixture fx;
        BikeTuning tuning;
        FirstPersonPlayer player;
        Motorbike bike;
        ScriptedVehicleIntentSource input;

        static Vector3 O => VehicleFixture.Origin;
        static VehicleIntent Gas => new VehicleIntent { Throttle = 1f };
        static VehicleIntent ExitPress => new VehicleIntent { Exit = true };

        [SetUp]
        public void SetUp()
        {
            Interactable.ResetStatics();
            fx = new VehicleFixture();
            fx.Floor(800f);
            tuning = fx.Track(ScriptableObject.CreateInstance<BikeTuning>());
            player = fx.CreatePlayer(new Vector3(-3f, 0f, 0f));
            bike = fx.CreateBike(Vector3.zero, 0f, tuning);
            input = new ScriptedVehicleIntentSource();
            bike.SetIntentSource(input);
            fx.Sync();
        }

        [TearDown]
        public void TearDown()
        {
            bike.Shutdown();
            fx.Dispose();
            Interactable.ResetStatics();
        }

        void Run(float seconds, float dt = Dt)
        {
            int frames = Mathf.Max(1, Mathf.RoundToInt(seconds / dt));
            for (int i = 0; i < frames; i++)
            {
                bike.Tick(dt);
                player.Tick(dt);
                bike.LateTick();
            }
        }

        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        [Test]
        public void Mount_SeatsThePlayer_LocksEverythingButLook()
        {
            Assert.IsTrue(bike.Mount(player));
            Assert.IsTrue(bike.IsMounted);
            Assert.AreSame(player, bike.Rider);
            Assert.IsTrue(player.IsSeated);
            Assert.AreSame(bike.Seat, player.CurrentSeat);
            Assert.IsFalse(player.Locks.Allows(PlayerAbility.Move));
            Assert.IsFalse(player.Locks.Allows(PlayerAbility.Interact));
            Assert.IsFalse(player.Locks.Allows(PlayerAbility.Flashlight));
            Assert.IsTrue(player.Locks.Allows(PlayerAbility.Look), "Look tidak dikunci supaya kursor bisa dikunci lagi setelah pause");
            Assert.IsFalse(player.Hud.CrosshairVisible);
            Assert.IsTrue(player.Hud.VehicleVisible);
        }

        [Test]
        public void Mount_Twice_OrWithoutAPlayer_Fails()
        {
            Assert.IsFalse(bike.Mount(null));
            var seated = fx.CreatePlayer(new Vector3(0f, 0f, 6f));
            var otherSeat = fx.Track(new GameObject("OtherSeat")).transform;
            otherSeat.position = O + new Vector3(0f, 0.86f, 6f);
            seated.Seat(otherSeat);
            Assert.IsFalse(bike.Mount(seated), "pemain yang sudah duduk di kendaraan lain tidak boleh naik");
            Assert.IsFalse(bike.IsMounted);
            Assert.AreSame(otherSeat, seated.CurrentSeat);
            Assert.IsTrue(bike.Mount(player));
            var other = fx.CreatePlayer(new Vector3(3f, 0f, 0f));
            Assert.IsFalse(bike.Mount(other));
            Assert.AreSame(player, bike.Rider);
        }

        [Test]
        public void Mount_TurnsTheFlashlightOff()
        {
            var go = fx.Track(new GameObject("Flashlight"));
            go.transform.SetParent(player.View, false);
            var flashlight = go.AddComponent<Flashlight>();
            flashlight.Initialize(player);
            flashlight.Toggle();
            Assert.IsTrue(flashlight.IsOn);
            bike.Mount(player);
            Assert.IsFalse(flashlight.IsOn);
        }

        [Test]
        public void FullThrottle_ReachesTopSpeed_AndDrivesStraight()
        {
            bike.Mount(player);
            input.Hold(Gas, 10f);
            Run(10f);
            Assert.AreEqual(60f, bike.SpeedKmh, 0.5f);
            Assert.Greater(bike.transform.position.z - O.z, 100f);
            Assert.AreEqual(O.x, bike.transform.position.x, 0.01f);
            Assert.AreEqual(60, player.Hud.VehicleSpeedKmh, 1);
            Assert.Less(Flat(bike.Seat.position, player.transform.position), 0.01f, "pemain ikut kursi");
        }

        [Test]
        public void Steering_TurnsTheBike()
        {
            bike.Mount(player);
            input.Hold(new VehicleIntent { Throttle = 1f, Steer = 1f }, 2f);
            Run(2f);
            Assert.Greater(bike.Yaw, 30f);
        }

        [Test]
        public void WallAhead_StopsWithoutPenetrating_EvenWithLongFrames()
        {
            fx.Box("Wall", new Vector3(0f, 1.5f, 30f), new Vector3(10f, 3f, 0.5f));
            fx.Sync();
            bike.Mount(player);
            input.Hold(Gas, 8f);
            Run(8f, 0.1f);
            float front = bike.transform.position.z + tuning.blockHalfExtents.z;
            Assert.LessOrEqual(front, O.z + 29.75f + 0.01f);
            Assert.AreEqual(0f, bike.Speed, 1e-3f);
            Assert.GreaterOrEqual(bike.Bumps, 1);
        }

        [Test]
        public void GlancingWall_SlidesAlong()
        {
            fx.Box("Wall", new Vector3(3f, 1.5f, 15f), new Vector3(0.5f, 3f, 40f), -20f);
            fx.Sync();
            bike.Mount(player);
            input.Hold(Gas, 6f);
            Run(6f);
            Assert.Greater(bike.Speed, 2f, "menyerong tidak berhenti");
            Assert.AreEqual(0, bike.Bumps);
        }

        [Test]
        public void ExitPressedRightAfterMounting_IsIgnored()
        {
            bike.Mount(player);
            input.Press(ExitPress);
            Run(Dt);
            Assert.IsTrue(bike.IsMounted, "E yang sama tidak boleh langsung menurunkan");
            Run(0.4f);
            input.Press(ExitPress);
            Run(Dt);
            Assert.IsFalse(bike.IsMounted);
        }

        [Test]
        public void Dismount_WhenFast_ShowsAMessageAndStaysSeated()
        {
            bike.Mount(player);
            input.Hold(Gas, 2f);
            Run(2f);
            Assert.IsFalse(player.Hud.VehicleCanExit);
            input.Press(ExitPress);
            Run(Dt);
            Assert.IsTrue(bike.IsMounted);
            Assert.AreEqual(Motorbike.SlowDownMessage, player.Hud.Message);
        }

        [Test]
        public void Dismount_WhenStopped_PutsThePlayerOnTheLeft_AndFreesEverything()
        {
            bike.Mount(player);
            Run(0.5f);
            Assert.IsTrue(player.Hud.VehicleCanExit);
            Assert.IsTrue(bike.Dismount());
            Assert.IsFalse(bike.IsMounted);
            Assert.IsFalse(player.IsSeated);
            Assert.Less(Flat(bike.transform.position - bike.transform.right * 1.1f, player.transform.position), 0.05f);
            Assert.IsTrue(player.Locks.Allows(PlayerAbility.All));
            Assert.IsTrue(player.GetComponent<CharacterController>().enabled);
            Assert.IsFalse(player.Hud.VehicleVisible);
            Assert.IsTrue(player.Hud.CrosshairVisible);
            Assert.IsFalse(bike.HeadlightOn);
        }

        [Test]
        public void Dismount_NoRoom_ShowsAMessageAndStaysSeated()
        {
            fx.Box("WallLeft", new Vector3(-1.1f, 1f, 0f), new Vector3(0.4f, 2f, 3f));
            fx.Box("WallRight", new Vector3(1.1f, 1f, 0f), new Vector3(0.4f, 2f, 3f));
            fx.Sync();
            bike.Mount(player);
            Run(0.5f);
            Assert.IsFalse(bike.Dismount());
            Assert.IsTrue(bike.IsMounted);
            Assert.AreEqual(Motorbike.NoRoomMessage, player.Hud.Message);
        }

        [Test]
        public void RiderTeleported_ReleasesTheBike()
        {
            bike.Mount(player);
            player.Teleport(O + new Vector3(10f, 0f, 0f), 0f);
            Assert.IsFalse(bike.IsMounted);
            Assert.IsTrue(player.Locks.Allows(PlayerAbility.Move));
            Assert.IsFalse(player.Hud.VehicleVisible);
        }

        [Test]
        public void Shutdown_WhileRidden_FreesThePlayer()
        {
            bike.Mount(player);
            bike.Shutdown();
            Assert.IsFalse(player.IsSeated);
            Assert.IsFalse(bike.IsMounted);
            Assert.IsTrue(player.Locks.Allows(PlayerAbility.All));
        }

        [Test]
        public void ZeroDt_DoesNothing()
        {
            bike.Mount(player);
            input.Hold(Gas, 10f);
            Run(2f);
            var before = bike.transform.position;
            float speed = bike.Speed;
            bool headlight = bike.HeadlightOn;
            Assert.Greater(speed, 2f, "motor harus sudah melaju sebelum pause");
            input.Press(new VehicleIntent { ToggleHeadlight = true });
            bike.Tick(0f);
            Assert.AreEqual(before, bike.transform.position);
            Assert.AreEqual(speed, bike.Speed);
            Assert.AreEqual(headlight, bike.HeadlightOn);
            bike.Tick(Dt);
            Assert.AreNotEqual(headlight, bike.HeadlightOn, "tekanan tombol tetap tertunda selama pause dan diproses pada tick berikutnya");
        }

        [Test]
        public void Motorbike_TicksAfterThePlayer()
        {
            Assert.Greater(ExecutionOrder(typeof(Motorbike)), ExecutionOrder(typeof(FirstPersonPlayer)),
                "pada frame turun, pemain (Interact masih terkunci) harus tick dulu supaya E tidak ikut memicu Interactor");
        }

        static int ExecutionOrder(System.Type type)
        {
            var attributes = type.GetCustomAttributes(typeof(DefaultExecutionOrder), true);
            return attributes.Length > 0 ? ((DefaultExecutionOrder)attributes[0]).order : 0;
        }

        [Test]
        public void Ramp_WithinSlopeLimit_IsClimbed()
        {
            // 32° ramp (below the 35° slopeLimit, above the 30° where sin θ passes headOnDot 0.5) rising toward +z;
            // the near edge of its top face sits at floor level 3 m ahead of the bike root (2.05 m ahead of its nose).
            const float angle = 32f;
            var rotation = Quaternion.Euler(-angle, 0f, 0f);
            var size = new Vector3(6f, 1f, 30f);
            var nearTopEdge = new Vector3(0f, -0.01f, 3f);
            var center = nearTopEdge - rotation * Vector3.up * (size.y * 0.5f) + rotation * Vector3.forward * (size.z * 0.5f);
            fx.Box("Ramp", center, size).transform.rotation = rotation;
            fx.Sync();
            bike.Mount(player);
            input.Hold(Gas, 3f);
            Run(3f);
            Assert.Greater(bike.transform.position.y - O.y, 3f, "motor naik tanjakan");
            Assert.AreEqual(0, bike.Bumps);
        }

        [Test]
        public void Body_RestsOnTheGround()
        {
            bike.Mount(player);
            Run(0.5f);
            Assert.AreEqual(O.y, bike.transform.position.y, 0.01f, "akar motor di tanah, tidak melayang setebal skin width");
        }

        [Test]
        public void Shutdown_AfterTheVisualsShutDown_DoesNotRecreateMaterialCopies()
        {
            var tail = new GameObject("TailLight").AddComponent<MeshRenderer>();
            tail.transform.SetParent(bike.transform, false);
            var material = fx.Track(new Material(Shader.Find("Universal Render Pipeline/Simple Lit")));
            tail.sharedMaterial = material;
            var visuals = bike.gameObject.AddComponent<MotorbikeVisuals>();
            visuals.Configure(null, null, null, null, null, null, tail, null);
            bike.Configure(tuning, bike.Seat, visuals, null);
            visuals.Initialize();
            try
            {
                bike.Mount(player);
                Run(0.1f);
                Assert.AreNotSame(material, tail.sharedMaterial, "selama dinaiki, lampu belakang memakai salinan runtime");
                visuals.Shutdown();
                Assert.AreSame(material, tail.sharedMaterial);
                bike.Shutdown();
                Assert.IsFalse(bike.IsMounted);
                Assert.AreSame(material, tail.sharedMaterial, "Shutdown motor tidak boleh membuat salinan material baru");
            }
            finally
            {
                visuals.Shutdown();
            }
        }

        [Test]
        public void Buttons_ToggleTheHeadlightAndTheCamera()
        {
            bike.Mount(player);
            Run(0.4f);
            input.Press(new VehicleIntent { ToggleHeadlight = true, ToggleCamera = true });
            Run(Dt);
            Assert.IsTrue(bike.HeadlightOn);
            Assert.IsTrue(player.Hud.VehicleHeadlight);
            Assert.AreEqual(RideCameraMode.FirstPerson, bike.CameraState.Mode);
        }

        [Test]
        public void LateTick_PutsTheCameraBehindAndAboveTheBike()
        {
            bike.Mount(player);
            Run(0.1f);
            var cam = player.View.position;
            Assert.Less(cam.z, bike.transform.position.z - 4f);
            Assert.Greater(cam.y, bike.transform.position.y + 1.5f);
        }

        [Test]
        public void FellBelowTheWorld_ReturnsHome_AndDropsTheRider()
        {
            tuning.respawnBelowY = O.y - 20f;
            bike.Mount(player);
            bike.Teleport(O + new Vector3(500f, -30f, 0f), 0f);
            bike.Tick(Dt);
            Assert.Less(Flat(O, bike.transform.position), 0.01f);
            Assert.IsFalse(bike.IsMounted);
            Assert.IsFalse(player.IsSeated);
        }

        [Test]
        public void Interactable_MountsAndIsUnavailableWhileRidden()
        {
            var interactable = bike.GetComponent<MotorbikeInteractable>();
            var context = new InteractionContext(player, player.Hud, player.View.position);
            Assert.AreEqual("[E] Naik motor", interactable.GetPrompt(context));
            Assert.IsTrue(interactable.CanInteract(context));
            Assert.IsTrue(interactable.Interact(context));
            Assert.IsTrue(bike.IsMounted);
            Assert.IsFalse(interactable.CanInteract(context));
        }

        [Test]
        public void MissingTuning_UsesDefaultsWithOneWarning()
        {
            var root = fx.Track(new GameObject("BareBike"));
            root.transform.position = O + new Vector3(20f, 0f, 0f);
            root.AddComponent<CharacterController>();
            var bare = root.AddComponent<Motorbike>();
            LogAssert.Expect(LogType.Warning, Motorbike.MissingTuningWarning);
            bare.Initialize();
            Assert.IsNotNull(bare.Tuning);
            Assert.DoesNotThrow(() => bare.Tick(Dt));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
