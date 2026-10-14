using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TycoWorld.Player
{
    /// <summary>
    /// The single per-frame entry point of the player: intent → look → crouch → move → stamina → steps/bob →
    /// ground probe → HUD model → modules (by Order) → fall check. Spec #2 §4.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class FirstPersonPlayer : MonoBehaviour
    {
        [SerializeField] PlayerTuning tuning;
        [SerializeField] Transform cameraPivot;
        [SerializeField] Transform cameraView;

        readonly List<IPlayerModule> modules = new List<IPlayerModule>();
        CharacterController body;
        MovementModel movement;
        CrouchModel crouch;
        StepCycle steps;
        IPlayerIntentSource scriptedSource;
        PlayerInputReader reader;
        Vector2 yawPitch;
        float bobWeight;

        public event Action FellOutOfWorld;

        public PlayerTuning Tuning => tuning;
        public Transform CameraPivot => cameraPivot;
        public Transform View => cameraView;
        public bool IsInitialized { get; private set; }
        public ControlLocks Locks { get; private set; }
        public StaminaModel Stamina { get; private set; }
        public HudModel Hud { get; private set; }
        public PlayerIntent Intent { get; private set; }
        public GroundInfo Ground { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsCrouched => crouch != null && crouch.Crouched;
        public bool IsSprinting { get; private set; }
        public int StepsThisFrame { get; private set; }
        public int StepCount => steps != null ? steps.StepCount : 0;
        public bool JumpedThisFrame { get; private set; }
        public bool LandedThisFrame { get; private set; }
        public float LandingImpact { get; private set; }
        public Vector3 Velocity => movement != null ? movement.Velocity : Vector3.zero;
        public float Yaw => yawPitch.x;
        public float Pitch => yawPitch.y;
        public IReadOnlyList<IPlayerModule> Modules => modules;

        /// <summary>Settings multiplier on mouse look; only the keyboard/mouse reader is scaled, scripted sources are not.</summary>
        public float LookSensitivity { get; set; } = 1f;

        /// <summary>Settings: invert vertical mouse look (keyboard/mouse reader only).</summary>
        public bool InvertY { get; set; }

        /// <summary>False while the pause menu owns Esc (spec #4 §6); true keeps Esc = release the cursor.</summary>
        public bool EscapeUnlocksCursor { get; set; } = true;

        public void Configure(PlayerTuning playerTuning, Transform pivot, Transform view)
        {
            tuning = playerTuning;
            cameraPivot = pivot;
            cameraView = view;
        }

        void Awake() => Initialize();

        void OnEnable()
        {
            if (reader != null) reader.Enable();
        }

        void OnDisable()
        {
            if (reader != null) reader.Disable();
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            HandleCursor();
            Tick(Time.deltaTime);
        }

        public void Initialize()
        {
            if (IsInitialized) return;
            if (tuning == null)
            {
                Debug.LogWarning("[Player] PlayerTuning kosong; memakai nilai default.");
                tuning = ScriptableObject.CreateInstance<PlayerTuning>();
                tuning.hideFlags = HideFlags.DontSave;
            }
            body = GetComponent<CharacterController>();
            var b = tuning.body;
            body.radius = b.radius;
            body.stepOffset = b.stepOffset;
            body.skinWidth = b.skinWidth;
            body.slopeLimit = b.slopeLimit;
            body.minMoveDistance = 0f;

            Locks = new ControlLocks();
            Locks.Changed += OnLocksChanged;
            Stamina = new StaminaModel(tuning.stamina);
            Hud = new HudModel(tuning.hud);
            movement = new MovementModel(tuning.movement);
            crouch = new CrouchModel(tuning.crouch, b.height);
            steps = new StepCycle(tuning.bob.landingMinSpeed);
            yawPitch = new Vector2(Mathf.Repeat(transform.eulerAngles.y, 360f), 0f);
            ApplyCrouchHeight();

            modules.Clear();
            modules.AddRange(GetComponentsInChildren<IPlayerModule>(true));
            modules.Sort((x, y) => x.Order.CompareTo(y.Order));
            IsInitialized = true;
            foreach (var module in modules) module.Initialize(this);
        }

        /// <summary>null restores the real keyboard/mouse reader.</summary>
        public void SetIntentSource(IPlayerIntentSource source) => scriptedSource = source;

        public void Tick(float dt)
        {
            if (!IsInitialized) Initialize();
            if (dt <= 0f) return;
            var source = ActiveSource;
            var raw = source != null ? source.Read(dt) : default;
            if (reader != null && ReferenceEquals(source, reader))
                raw.LookDegrees = PlayerInputReader.ApplyLookSettings(raw.LookDegrees, LookSensitivity, InvertY);
            Intent = Filter(raw);

            yawPitch = LookModel.Apply(yawPitch, Intent.LookDegrees, tuning.look.pitchLimit);
            transform.rotation = Quaternion.Euler(0f, yawPitch.x, 0f);
            if (cameraPivot != null) cameraPivot.localRotation = LookModel.PitchRotation(yawPitch.y);

            if (Intent.CrouchToggle) crouch.RequestToggle(HasHeadroomToStand());
            crouch.Tick(dt);
            ApplyCrouchHeight();

            StepsThisFrame = 0;
            JumpedThisFrame = false;
            LandedThisFrame = false;
            LandingImpact = 0f;
            float horizontalSpeed = 0f;
            if (body.enabled)
            {
                bool grounded = body.isGrounded;
                IsSprinting = Intent.Sprint && !crouch.Crouched && Intent.Move.sqrMagnitude > 0.01f && Stamina.CanSprint;
                bool jump = Intent.Jump && movement.CanJump(grounded, crouch.Crouched) && Stamina.TrySpend(tuning.stamina.jumpCost);
                var input = new MoveInput
                {
                    Move = Intent.Move, Forward = transform.forward, Right = transform.right, Grounded = grounded,
                    Sprinting = IsSprinting, Crouched = crouch.Crouched, JumpRequested = jump
                };
                var before = transform.position;
                var flags = body.Move(movement.Step(input, dt));
                if ((flags & CollisionFlags.Above) != 0) movement.HitCeiling();
                JumpedThisFrame = movement.JumpedThisStep;
                IsGrounded = body.isGrounded;
                // Actual displacement, not CharacterController.velocity (which depends on Time.deltaTime, not dt).
                var planar = transform.position - before;
                planar.y = 0f;
                horizontalSpeed = planar.magnitude / dt;
                StepsThisFrame = steps.Advance(horizontalSpeed, IsGrounded, Stride(), dt);
                LandedThisFrame = steps.UpdateLanding(IsGrounded, movement.Velocity.y, out float impact);
                LandingImpact = impact;
            }
            else
            {
                IsSprinting = false;
                IsGrounded = false;
            }
            Stamina.Tick(IsSprinting, dt);
            ApplyBob(horizontalSpeed, dt);
            Ground = GroundProbe.Probe(transform.position + Vector3.up * 0.5f, 1.5f, out var ground) ? ground : default;
            Hud.Tick(dt, Stamina.Normalized);
            foreach (var module in modules) module.Tick(this, dt);
            if (transform.position.y < tuning.respawnBelowY) FellOutOfWorld?.Invoke();
        }

        /// <summary>The only way to move the player instantly (autoSyncTransforms is off).</summary>
        public void Teleport(Vector3 feet, float yawDegrees)
        {
            if (!IsInitialized) Initialize();
            body.enabled = false;
            transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, yawDegrees, 0f));
            yawPitch = new Vector2(Mathf.Repeat(yawDegrees, 360f), 0f);
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.identity;
            movement.ResetMotion();
            Physics.SyncTransforms();
            body.enabled = Locks.Allows(PlayerAbility.Move);
        }

        public void LockCursor()
        {
            if (!Application.isPlaying) return;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void UnlockCursor()
        {
            if (!Application.isPlaying) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        IPlayerIntentSource ActiveSource
        {
            get
            {
                if (scriptedSource != null) return scriptedSource;
                if (reader == null && Application.isPlaying && InputSystem.actions != null)
                {
                    reader = new PlayerInputReader(InputSystem.actions, tuning.look);
                    reader.Enable();
                }
                return reader;
            }
        }

        void HandleCursor()
        {
            if (scriptedSource != null || ActiveSource == null) return;
            if (EscapeUnlocksCursor && reader.ReleaseCursorPressed) UnlockCursor();
            else if (reader.AttackPressed && Cursor.lockState != CursorLockMode.Locked && Locks.Allows(PlayerAbility.Look)) LockCursor();
            reader.CursorLocked = Cursor.lockState == CursorLockMode.Locked;
        }

        PlayerIntent Filter(PlayerIntent intent)
        {
            if (!Locks.Allows(PlayerAbility.Move))
            {
                intent.Move = Vector2.zero;
                intent.Sprint = false;
                intent.Jump = false;
                intent.CrouchToggle = false;
            }
            if (!Locks.Allows(PlayerAbility.Look)) intent.LookDegrees = Vector2.zero;
            if (!Locks.Allows(PlayerAbility.Interact)) intent.Interact = false;
            if (!Locks.Allows(PlayerAbility.Flashlight)) intent.Flashlight = false;
            return intent;
        }

        void OnLocksChanged()
        {
            bool canMove = Locks.Allows(PlayerAbility.Move);
            if (body.enabled == canMove) return;
            body.enabled = canMove;
            movement.ResetMotion();
        }

        bool HasHeadroomToStand()
        {
            float r = tuning.body.radius;
            var feet = transform.position;
            return !Physics.CheckCapsule(feet + Vector3.up * (r + 0.05f), feet + Vector3.up * (tuning.body.height - r), r * 0.95f,
                PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore);
        }

        void ApplyCrouchHeight()
        {
            body.height = crouch.Height;
            body.center = new Vector3(0f, crouch.Height * 0.5f, 0f);
            if (cameraPivot != null) cameraPivot.localPosition = new Vector3(0f, crouch.EyeHeight, 0f);
        }

        float Stride() => crouch.Crouched ? tuning.bob.crouchStride : IsSprinting ? tuning.bob.sprintStride : tuning.bob.walkStride;

        void ApplyBob(float horizontalSpeed, float dt)
        {
            bool moving = IsGrounded && horizontalSpeed > 0.2f;
            bobWeight = Mathf.MoveTowards(bobWeight, moving ? 1f : 0f, tuning.bob.fadeSpeed * dt);
            float amplitude = crouch.Crouched ? tuning.bob.crouchAmplitude : IsSprinting ? tuning.bob.sprintAmplitude : tuning.bob.walkAmplitude;
            if (cameraView != null)
                cameraView.localPosition = HeadBob.Offset(steps.CyclePhase01, amplitude, tuning.bob.lateralRatio) * bobWeight;
        }
    }
}
