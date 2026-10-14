using TycoWorld.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TycoWorld.Vehicles
{
    /// <summary>
    /// The rideable motorbike (spec #5 §3): mount and dismount, arcade motion with a CharacterController plus a box
    /// cast that stops the long body at walls, the ride camera, visuals, audio, headlight and the riding HUD.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class Motorbike : MonoBehaviour
    {
        public const string SlowDownMessage = "Pelankan motor dulu.";
        public const string NoRoomMessage = "Tidak ada ruang untuk turun.";
        public const string MissingTuningWarning = "[Vehicle] BikeTuning kosong; memakai nilai default.";
        const float Skin = 0.05f;
        const PlayerAbility RiderLocks = PlayerAbility.Move | PlayerAbility.Interact | PlayerAbility.Flashlight;

        [SerializeField] BikeTuning tuning;
        [SerializeField] Transform seat;
        [SerializeField] MotorbikeVisuals visuals;
        [SerializeField] MotorbikeAudio sound;

        CharacterController body;
        BikeModel model;
        RideCamera rideCamera;
        IVehicleIntentSource scriptedSource;
        VehicleInputReader reader;
        float verticalSpeed, exitGuard, steer;
        bool touchingWall;
        Vector3 homePosition;
        float homeYaw;

        public BikeTuning Tuning => tuning;
        public Transform Seat => seat;
        public MotorbikeVisuals Visuals => visuals;
        public MotorbikeAudio Sound => sound;
        public bool IsInitialized { get; private set; }
        public FirstPersonPlayer Rider { get; private set; }
        public bool IsMounted => Rider != null;
        public BikeModel Model => model;
        public RideCamera CameraState => rideCamera;
        public IVehicleIntentSource IntentSourceOverride => scriptedSource;
        public float Speed => model != null ? model.Speed : 0f;
        public float SpeedKmh => Speed * BikeTuning.KmhPerMs;
        public float Yaw => transform.eulerAngles.y;
        public bool HeadlightOn { get; private set; }
        public bool CanDismount => IsMounted && Mathf.Abs(Speed) <= tuning.dismountMaxSpeed;
        /// <summary>Head-on wall hits above bumpSpeed since Initialize.</summary>
        public int Bumps { get; private set; }
        public Vector3 HomePosition => homePosition;

        public void Configure(BikeTuning bikeTuning, Transform seatTransform, MotorbikeVisuals bikeVisuals, MotorbikeAudio bikeAudio)
        {
            tuning = bikeTuning;
            seat = seatTransform;
            visuals = bikeVisuals;
            sound = bikeAudio;
        }

        void Awake() => Initialize();

        void Update()
        {
            if (Application.isPlaying) Tick(Time.deltaTime);
        }

        void LateUpdate()
        {
            if (Application.isPlaying) LateTick();
        }

        void OnDestroy() => Shutdown();

        public void Initialize()
        {
            if (IsInitialized) return;
            if (tuning == null)
            {
                Debug.LogWarning(MissingTuningWarning);
                tuning = ScriptableObject.CreateInstance<BikeTuning>();
                tuning.hideFlags = HideFlags.DontSave;
            }
            body = GetComponent<CharacterController>();
            ConfigureBody(body, tuning);
            model = new BikeModel(tuning);
            rideCamera = new RideCamera(tuning);
            homePosition = transform.position;
            homeYaw = transform.eulerAngles.y;
            if (visuals != null) visuals.Initialize();
            IsInitialized = true;
        }

        /// <summary>CharacterController shape from the tuning (spec #5 §4); the prefab builder uses it too.</summary>
        public static void ConfigureBody(CharacterController controller, BikeTuning t)
        {
            controller.radius = t.bodyRadius;
            controller.height = t.bodyHeight;
            controller.center = new Vector3(0f, t.bodyHeight * 0.5f, 0f);
            controller.slopeLimit = t.slopeLimit;
            controller.stepOffset = t.stepOffset;
            controller.minMoveDistance = 0f;
        }

        /// <summary>Scripted input for tests and MCP verification; null restores the keyboard/gamepad reader.</summary>
        public void SetIntentSource(IVehicleIntentSource source) => scriptedSource = source;

        public bool Mount(FirstPersonPlayer player)
        {
            if (!IsInitialized) Initialize();
            if (IsMounted || player == null || !player.IsInitialized || seat == null) return false;
            Rider = player;
            player.Locks.Acquire(this, RiderLocks);
            var flashlight = player.GetComponentInChildren<Flashlight>(true);
            if (flashlight != null) flashlight.TurnOff();
            player.Seat(seat);
            player.Unseated += OnRiderUnseated;
            player.Hud.CrosshairVisible = false;
            player.Hud.SetVehicle(true, SpeedKmh, HeadlightOn, CanDismount);
            exitGuard = tuning.exitGuardSeconds;
            rideCamera.Reset(Yaw);
            if (sound != null) sound.StartEngine();
            return true;
        }

        public bool Dismount()
        {
            if (!IsMounted) return false;
            var player = Rider;
            if (!CanDismount)
            {
                player.Hud.ShowMessage(SlowDownMessage);
                return false;
            }
            var stance = player.Tuning.body;
            if (!DismountFinder.TryFind(transform, tuning.dismountSideOffset, stance.radius, stance.height, out var feet))
            {
                player.Hud.ShowMessage(NoRoomMessage);
                return false;
            }
            player.Unseat(feet, Yaw);
            return true;
        }

        /// <summary>Moves the bike instantly (autoSyncTransforms is off) and stops it.</summary>
        public void Teleport(Vector3 position, float yawDegrees)
        {
            if (!IsInitialized) Initialize();
            body.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yawDegrees, 0f));
            model.Reset();
            verticalSpeed = 0f;
            touchingWall = false;
            Physics.SyncTransforms();
            body.enabled = true;
            rideCamera.Reset(yawDegrees);
        }

        /// <summary>Back to the parking pose the bike had when the scene started; a rider is set down beside it.</summary>
        public void ResetToHome()
        {
            var player = Rider;
            Teleport(homePosition, homeYaw);
            if (player == null) return;
            var stance = player.Tuning.body;
            var feet = DismountFinder.TryFind(transform, tuning.dismountSideOffset, stance.radius, stance.height, out var spot)
                ? spot
                : transform.position - transform.right * tuning.dismountSideOffset;
            player.Unseat(feet, Yaw);
        }

        public void Tick(float dt)
        {
            if (!IsInitialized) Initialize();
            if (dt <= 0f) return;
            var intent = IsMounted ? ReadIntent(dt) : default;
            if (exitGuard > 0f) exitGuard -= dt;
            if (IsMounted) HandleButtons(intent);
            model.Step(intent, dt);
            steer = Mathf.Clamp(intent.Steer, -1f, 1f);
            float yawDelta = model.YawRate(steer) * dt;
            if (yawDelta != 0f) transform.rotation = Quaternion.Euler(0f, Yaw + yawDelta, 0f);
            Move(dt);
            bool riderVisible = IsMounted && rideCamera.Mode == RideCameraMode.ThirdPerson;
            if (visuals != null) visuals.Apply(model.Speed, steer * tuning.maxSteerAngle, model.Lean, model.Braking, HeadlightOn, riderVisible, dt);
            if (sound != null) sound.Tick(Mathf.Abs(model.Speed) / tuning.maxForwardSpeed, Mathf.Max(0f, intent.Throttle), dt, tuning);
            if (IsMounted)
            {
                rideCamera.Update(intent.LookDegrees, Yaw, model.Speed, dt);
                Rider.Hud.SetVehicle(true, SpeedKmh, HeadlightOn, CanDismount);
            }
            if (transform.position.y < tuning.respawnBelowY) ResetToHome();
        }

        /// <summary>Places the rider's camera (third-person chase or first-person head); call after every Tick of the frame.</summary>
        public void LateTick()
        {
            if (!IsMounted || Rider.View == null) return;
            Vector3 position;
            Quaternion rotation;
            if (rideCamera.Mode == RideCameraMode.ThirdPerson) rideCamera.PoseThirdPerson(transform.position, out position, out rotation);
            else rideCamera.PoseFirstPerson(seat.position, Yaw, model.Lean, out position, out rotation);
            Rider.View.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Frees the rider (bike destroyed, scene unloaded) so the player is never left locked on a missing seat.</summary>
        public void Shutdown()
        {
            if (IsInitialized && Rider != null)
            {
                var player = Rider;
                if (player.IsSeated) player.Unseat(transform.position - transform.right * tuning.dismountSideOffset, Yaw);
                else ReleaseRider();
            }
            if (reader != null) reader.Disable();
        }

        void HandleButtons(VehicleIntent intent)
        {
            if (intent.ToggleHeadlight) HeadlightOn = !HeadlightOn;
            if (intent.ToggleCamera) rideCamera.Toggle();
            if (intent.Horn && sound != null) sound.Horn(tuning);
            if (intent.Exit && exitGuard <= 0f) Dismount();
        }

        VehicleIntent ReadIntent(float dt)
        {
            if (scriptedSource != null) return scriptedSource.Read(dt);
            var input = Reader;
            if (input == null) return default;
            input.CursorLocked = Cursor.lockState == CursorLockMode.Locked;
            var intent = input.Read(dt);
            intent.LookDegrees = PlayerInputReader.ApplyLookSettings(intent.LookDegrees, Rider.LookSensitivity, Rider.InvertY);
            return intent;
        }

        VehicleInputReader Reader
        {
            get
            {
                if (reader == null && Application.isPlaying && InputSystem.actions != null && Rider != null)
                    reader = new VehicleInputReader(InputSystem.actions, Rider.Tuning.look.degreesPerPixel);
                if (reader != null && IsMounted && !reader.IsEnabled) reader.Enable();
                return reader;
            }
        }

        void Move(float dt)
        {
            var motion = transform.forward * (model.Speed * dt);
            if (motion.sqrMagnitude > 1e-10f) motion = Block(motion);
            else touchingWall = false;
            if (body.isGrounded && verticalSpeed < 0f) verticalSpeed = -1f;
            verticalSpeed -= tuning.gravity * dt;
            body.Move(motion + Vector3.up * (verticalSpeed * dt));
        }

        /// <summary>Box cast of the long body along the motion: head-on stops at the wall, glancing slides along it (speed × glanceSpeedFactor once per contact).</summary>
        Vector3 Block(Vector3 motion)
        {
            float distance = motion.magnitude;
            var direction = motion / distance;
            var center = transform.position + Vector3.up * (tuning.stepOffset + tuning.blockHalfExtents.y);
            if (!Physics.BoxCast(center, tuning.blockHalfExtents, direction, out var hit, transform.rotation, distance + Skin,
                    PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore))
            {
                touchingWall = false;
                return motion;
            }
            if (Vector3.Dot(hit.normal, -direction) > tuning.headOnDot)
            {
                float impact = Mathf.Abs(model.Speed);
                if (impact > tuning.bumpSpeed)
                {
                    Bumps++;
                    if (sound != null) sound.Bump(impact / tuning.maxForwardSpeed);
                }
                model.Stop();
                touchingWall = true;
                return direction * Mathf.Max(0f, hit.distance - Skin);
            }
            if (!touchingWall) model.Scale(tuning.glanceSpeedFactor);
            touchingWall = true;
            var slide = Vector3.ProjectOnPlane(motion, hit.normal);
            slide.y = 0f;
            return slide;
        }

        void OnRiderUnseated() => ReleaseRider();

        void ReleaseRider()
        {
            var player = Rider;
            if (player == null) return;
            player.Unseated -= OnRiderUnseated;
            Rider = null;
            player.Locks.Release(this);
            player.Hud.SetVehicle(false, 0f, false, false);
            player.Hud.CrosshairVisible = true;
            HeadlightOn = false;
            if (reader != null) reader.Disable();
            if (sound != null) sound.StopEngine();
            if (visuals != null) visuals.Apply(model.Speed, 0f, model.Lean, false, false, false, 0f);
        }
    }
}
