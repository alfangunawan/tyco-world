using UnityEngine;

namespace TycoWorld.Vehicles
{
    /// <summary>Wheels, handlebar, lean, headlight, tail light and the rider model of the bike (spec #5 §3); no logic of its own.</summary>
    [DisallowMultipleComponent]
    public sealed class MotorbikeVisuals : MonoBehaviour
    {
        const float WheelRadius = 0.3f;
        const float TailDim = 0.35f;

        [SerializeField] Transform leanPivot;
        [SerializeField] Transform handlebar;
        [SerializeField] Transform frontWheel;
        [SerializeField] Transform rearWheel;
        [SerializeField] Transform rider;
        [SerializeField] Light headlight;
        [SerializeField] Renderer tailLight;
        [SerializeField] Renderer headlightLens;

        RuntimeEmission tail, lens;
        float wheelAngle;
        bool initialized;

        public Transform LeanPivot => leanPivot;
        public Transform Handlebar => handlebar;
        public Transform FrontWheel => frontWheel;
        public Transform RearWheel => rearWheel;
        public Transform Rider => rider;
        public Light Headlight => headlight;
        public Renderer TailLight => tailLight;
        public Renderer HeadlightLens => headlightLens;
        public bool TailBright => tail != null && tail.IsHigh;
        public bool LensOn => lens != null && lens.IsHigh;

        public void Configure(Transform lean, Transform bar, Transform front, Transform rear, Transform riderModel, Light light,
            Renderer tailRenderer, Renderer lensRenderer)
        {
            leanPivot = lean;
            handlebar = bar;
            frontWheel = front;
            rearWheel = rear;
            rider = riderModel;
            headlight = light;
            tailLight = tailRenderer;
            headlightLens = lensRenderer;
        }

        void OnDestroy() => Shutdown();

        public void Initialize()
        {
            if (initialized) return;
            tail = new RuntimeEmission(tailLight, 0, TailDim, 1f);
            lens = new RuntimeEmission(headlightLens, 0, 0f, 1f);
            initialized = true;
            Apply(0f, 0f, 0f, false, false, false, 0f);
        }

        /// <param name="speed">m/s, signed.</param>
        /// <param name="steerAngle">Handlebar yaw in degrees.</param>
        /// <param name="lean">Degrees, positive leans right.</param>
        public void Apply(float speed, float steerAngle, float lean, bool braking, bool headlightOn, bool riderVisible, float dt)
        {
            if (!initialized) Initialize();
            wheelAngle = Mathf.Repeat(wheelAngle + speed / WheelRadius * Mathf.Rad2Deg * dt, 360f);
            var spin = Quaternion.Euler(wheelAngle, 0f, 0f);
            if (frontWheel != null) frontWheel.localRotation = spin;
            if (rearWheel != null) rearWheel.localRotation = spin;
            if (handlebar != null) handlebar.localRotation = Quaternion.Euler(0f, steerAngle, 0f);
            if (leanPivot != null) leanPivot.localRotation = Quaternion.Euler(0f, 0f, -lean);
            if (headlight != null && headlight.enabled != headlightOn) headlight.enabled = headlightOn;
            if (rider != null && rider.gameObject.activeSelf != riderVisible) rider.gameObject.SetActive(riderVisible);
            if (tail != null) tail.Set(braking);
            if (lens != null) lens.Set(headlightOn);
        }

        public void Shutdown()
        {
            if (tail != null) tail.Dispose();
            if (lens != null) lens.Dispose();
            tail = null;
            lens = null;
            initialized = false;
        }
    }
}
