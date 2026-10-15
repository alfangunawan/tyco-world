using TycoWorld.Vehicles;
using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// The galon of Antar Galon (spec #7 §4), as a real model: lifted onto the player's right shoulder in the first-person
    /// view (a half-second lift), strapped to the pillion while riding, and handed to the customer, who holds it with
    /// both hands. Without the downloaded art there is nothing to show and every call is a no-op.
    /// </summary>
    public sealed class CarriedGalon
    {
        public const float LiftSeconds = 0.5f;
        /// <summary>On the right shoulder, seen from the first-person camera.</summary>
        public static readonly Vector3 ShoulderLocal = new Vector3(0.5f, -0.42f, 0.42f);
        /// <summary>Lying on the shoulder, neck forward, as galons are carried.</summary>
        public static readonly Vector3 ShoulderEuler = new Vector3(78f, -8f, 0f);
        static readonly Vector3 BelowView = new Vector3(0.5f, -1.3f, 0.42f);

        readonly MissionContext context;
        readonly GameObject galon;
        readonly Renderer[] renderers;
        float lift;
        Transform holder;

        public CarriedGalon(MissionContext owner)
        {
            context = owner;
            var library = owner.Director.Characters;
            if (library == null || library.galon == null) return;
            galon = Object.Instantiate(library.galon, owner.Root, false);
            galon.name = "GalonDibawa";
            renderers = galon.GetComponentsInChildren<Renderer>(true);
            if (library.galonMaterial != null)
                foreach (var r in renderers)
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = library.galonMaterial;
                    r.sharedMaterials = mats;
                }
        }

        public GameObject Model => galon;
        public bool Exists => galon != null;
        /// <summary>Where it is: "view", "bike", "held" (by the customer) or "" before the first tick.</summary>
        public string Place { get; private set; } = "";

        /// <summary>Keeps the galon on the shoulder, or on the pillion while riding.</summary>
        public void Tick(float dt)
        {
            if (galon == null || Place == "held") return;
            var bike = context.RiddenBike;
            if (bike != null)
            {
                var visuals = bike.Visuals;
                var anchor = visuals != null && visuals.LeanPivot != null ? visuals.LeanPivot : bike.transform;
                if (holder != anchor)
                {
                    holder = anchor;
                    galon.transform.SetParent(anchor, false);
                    var seat = bike.Seat != null ? anchor.InverseTransformPoint(bike.Seat.position) : new Vector3(0f, 0.86f, -0.2f);
                    galon.transform.localPosition = seat + new Vector3(0f, 0.02f, -PassengerRide.PillionBack - 0.05f);
                    galon.transform.localRotation = Quaternion.identity;
                    Shadows(true);
                    Place = "bike";
                }
                return;
            }
            var view = context.Director.Player != null ? context.Director.Player.View : null;
            if (view == null) return;
            if (holder != view)
            {
                holder = view;
                galon.transform.SetParent(view, false);
                galon.transform.localRotation = Quaternion.Euler(ShoulderEuler);
                lift = 0f;
                Shadows(false);
                Place = "view";
            }
            lift = Mathf.Min(1f, lift + dt / LiftSeconds);
            float k = 1f - (1f - lift) * (1f - lift);
            galon.transform.localPosition = Vector3.Lerp(BelowView, ShoulderLocal, k);
        }

        /// <summary>The customer takes it: in front of their chest, both hands on it.</summary>
        public void GiveTo(GameObject person)
        {
            if (galon == null || person == null) return;
            holder = person.transform;
            galon.transform.SetParent(person.transform, false);
            galon.transform.localPosition = new Vector3(0f, 0.72f, 0.32f);
            galon.transform.localRotation = Quaternion.identity;
            Shadows(true);
            var rig = MissionContext.RigOf(person);
            if (rig != null) rig.Hold(galon.transform, new Vector3(-0.15f, 0.3f, 0f), new Vector3(0.15f, 0.3f, 0f));
            Place = "held";
        }

        void Shadows(bool on)
        {
            foreach (var r in renderers)
                r.shadowCastingMode = on ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void Destroy()
        {
            if (galon == null) return;
            if (Application.isPlaying) Object.Destroy(galon);
            else Object.DestroyImmediate(galon);
        }
    }
}
