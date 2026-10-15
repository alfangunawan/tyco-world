using UnityEngine;

namespace TycoWorld.Gameplay
{
    public enum PedestrianState
    {
        Walk,
        Wait,
        Talk,
        Flee,
        Chase,
        Stare
    }

    /// <summary>One pooled person (spec #6 §2): a primitive figure with a capsule collider and a talk interactable, walking a lane.</summary>
    public sealed class Pedestrian
    {
        public const float WalkSpeedMin = 1.05f, WalkSpeedMax = 1.5f;
        /// <summary>Chasers run a touch slower than the player's sprint (5.5): you can get away while your stamina lasts.</summary>
        public const float RunSpeed = 4.2f, ChaseSpeed = 5.3f;

        public readonly GameObject Root;
        /// <summary>The primitive figure (when no character library is set up), else null.</summary>
        public readonly PersonFigure Figure;
        /// <summary>The animated downloaded body, else null.</summary>
        public readonly CharacterRig Rig;
        public readonly bool IsSosok;
        public readonly TalkInteractable Talk;
        public int Lane = -1;
        public float S;
        public int Dir = 1;
        public float Speed = 1.2f;
        public float Lateral;
        public PedestrianState State = PedestrianState.Walk;
        public float Timer;
        public float GroundY;
        public float GroundTimer;
        public float CurrentSpeed;
        /// <summary>Time not yet stepped (far people step every few frames).</summary>
        public float PendingDt;
        public SeededRandom Random;
        public int Serial;
        readonly Renderer[] bodyParts;
        bool detailSet;

        public Pedestrian(Transform parent, int index, PersonLook look, Palette palette, bool sosok, PedestrianSystem system)
        {
            IsSosok = sosok;
            Root = new GameObject(sosok ? "Sosok" : "Pedestrian_" + index);
            Root.transform.SetParent(parent, false);
            var library = system.Director != null ? system.Director.Characters : null;
            ulong seed = SeededRandom.Mix(0xFACEUL, (ulong)index);
            Rig = CharacterFactory.Spawn(library, Root.transform, "Body", seed, palette, woman: look.Hijab || (seed & 2UL) != 0UL, ghost: sosok);
            if (Rig == null) Figure = PersonFigure.Build(Root.transform, "Figure", look, palette, sosok);
            else bodyParts = Rig.GetComponentsInChildren<Renderer>(true);
            // Torso, head and legs make the shadow; arms and hair would only add shadow casters.
            foreach (var r in Root.GetComponentsInChildren<MeshRenderer>(true))
                if (r.name.StartsWith("Arm", System.StringComparison.Ordinal) || r.name == "Hair" || r.name == "Hijab" || r.name == "Drape")
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var capsule = Root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.height = 1.8f;
            capsule.radius = 0.28f;
            if (!sosok)
            {
                Talk = Root.AddComponent<TalkInteractable>();
                Talk.Configure(system, this);
                Talk.SetInteractionId("warga");
            }
            Root.SetActive(false);
            Random = new SeededRandom(SeededRandom.Mix(0xC0FFEEUL, (ulong)index));
        }

        public bool Active => Root.activeSelf;

        /// <summary>Plays walking at <paramref name="speed"/> (and talking) on whichever body this person has.</summary>
        public void Animate(float speed, float dt)
        {
            if (Rig != null)
            {
                Rig.Move(speed);
                Rig.Talk(State == PedestrianState.Talk);
            }
            else Figure.Animate(speed, dt);
        }

        /// <summary>
        /// Far people keep their pose and cast no shadow: the animator and the skinned meshes' shadow pass only run near
        /// the player (or for a chase).
        /// </summary>
        public void SetDetail(bool near)
        {
            if (Rig == null || Rig.Animator.enabled == near && detailSet) return;
            detailSet = true;
            Rig.Animator.enabled = near;
            var shadows = near ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            foreach (var r in bodyParts) r.shadowCastingMode = shadows;
        }
        public Vector3 Position => Root.transform.position;

        public void Place(Vector3 position, Vector3 forward)
        {
            forward.y = 0f;
            Root.transform.SetPositionAndRotation(position, forward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(forward) : Root.transform.rotation);
        }
    }
}
