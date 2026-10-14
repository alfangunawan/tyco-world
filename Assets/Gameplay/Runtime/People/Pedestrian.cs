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
        public const float RunSpeed = 4.2f, ChaseSpeed = 5.6f;

        public readonly GameObject Root;
        public readonly PersonFigure Figure;
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
        public SeededRandom Random;
        public int Serial;

        public Pedestrian(Transform parent, int index, PersonLook look, Palette palette, bool sosok, PedestrianSystem system)
        {
            IsSosok = sosok;
            Root = new GameObject(sosok ? "Sosok" : "Pedestrian_" + index);
            Root.transform.SetParent(parent, false);
            Figure = PersonFigure.Build(Root.transform, "Figure", look, palette, sosok);
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
        public Vector3 Position => Root.transform.position;

        public void Place(Vector3 position, Vector3 forward)
        {
            forward.y = 0f;
            Root.transform.SetPositionAndRotation(position, forward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(forward) : Root.transform.rotation);
        }
    }
}
