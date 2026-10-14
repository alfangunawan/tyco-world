using UnityEngine;

namespace TycoWorld.DayCycle
{
    /// <summary>Three looping 2D ambience layers (day, night, horror drone); their volumes come from the day cycle.</summary>
    public sealed class AmbienceAudio : MonoBehaviour
    {
        public const string DayName = "Ambience_Day";
        public const string NightName = "Ambience_Night";
        public const string DroneName = "Ambience_Drone";

        DayCycleProfile profile;
        float level = 1f;

        /// <summary>Settings multiplier (0–1) on top of the day cycle's layer volumes (spec #4 §5.5).</summary>
        public float Level
        {
            get => level;
            set => level = Mathf.Clamp01(value);
        }

        public AudioSource Day { get; private set; }
        public AudioSource Night { get; private set; }
        public AudioSource Drone { get; private set; }

        public void Initialize(DayCycleProfile cycleProfile)
        {
            profile = cycleProfile;
            Day = Layer(DayName, profile.dayClip);
            Night = Layer(NightName, profile.nightClip);
            Drone = Layer(DroneName, profile.horrorDroneClip);
        }

        public void Apply(float dayWeight, float nightWeight, float droneWeight)
        {
            if (profile == null) return;
            Set(Day, dayWeight * profile.dayVolume * level);
            Set(Night, nightWeight * profile.nightVolume * level);
            Set(Drone, droneWeight * profile.droneVolume * level);
        }

        AudioSource Layer(string layerName, AudioClip clip)
        {
            var child = transform.Find(layerName);
            if (child == null)
            {
                child = new GameObject(layerName).transform;
                child.SetParent(transform, false);
            }
            var source = child.GetComponent<AudioSource>();
            if (source == null) source = child.gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }

        static void Set(AudioSource source, float volume)
        {
            source.volume = volume;
            if (!Application.isPlaying || source.clip == null) return;
            bool audible = volume > 0.001f;
            if (audible && !source.isPlaying) source.Play();
            else if (!audible && source.isPlaying) source.Stop();
        }
    }
}
