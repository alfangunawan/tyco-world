using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TycoWorld.WorldGen;

namespace TycoWorld.Player.Authoring
{
    public sealed class FootstepSet
    {
        public SurfaceType Surface;
        public string[] Files;
        public float Volume, PitchMin, PitchMax;
    }

    /// <summary>Which Kenney CC0 clip plays for which surface or interaction (spec #2 §7).</summary>
    public static class PlayerAudioMap
    {
        public const string GateUnlatch = "metalLatch.ogg";
        public const string GateCreak = "creak1.ogg";
        public const string GateLatch = "metalLatch.ogg";
        public const string DoorRattle = "metalLatch.ogg";
        public const string FlashlightClick = "metalClick.ogg";
        public const string VehicleKeys = "handleCoins.ogg";

        public static readonly string[] InteractionFiles = { "metalLatch.ogg", "creak1.ogg", "metalClick.ogg", "handleCoins.ogg" };

        static readonly string[] Concrete = Seq("footstep_concrete_{0:000}.ogg", 5);

        public static readonly FootstepSet[] Footsteps =
        {
            Set(SurfaceType.Default, Concrete, 0.45f, 0.95f, 1.05f),
            Set(SurfaceType.Asphalt, Concrete, 0.45f, 0.88f, 0.98f),
            Set(SurfaceType.Concrete, Concrete, 0.45f, 0.98f, 1.08f),
            Set(SurfaceType.Paving, Concrete, 0.45f, 1.05f, 1.15f),
            Set(SurfaceType.Grass, Seq("footstep_grass_{0:000}.ogg", 5), 0.5f, 0.95f, 1.05f),
            Set(SurfaceType.Dirt, Seq("footstep{0:00}.ogg", 10), 0.5f, 0.9f, 1.0f),
            Set(SurfaceType.Water, Seq("footstep_snow_{0:000}.ogg", 5), 0.5f, 0.7f, 0.8f)
        };

        public static IEnumerable<string> AllFootstepFiles => Footsteps.SelectMany(f => f.Files).Distinct();

        public static string FootstepPath(string file) => PlayerPaths.FootstepAudioFolder + "/" + file;

        public static string InteractionPath(string file) => PlayerPaths.InteractionAudioFolder + "/" + file;

        static FootstepSet Set(SurfaceType surface, string[] files, float volume, float pitchMin, float pitchMax) =>
            new FootstepSet { Surface = surface, Files = files, Volume = volume, PitchMin = pitchMin, PitchMax = pitchMax };

        static string[] Seq(string format, int count)
        {
            var result = new string[count];
            for (int i = 0; i < count; i++) result[i] = string.Format(CultureInfo.InvariantCulture, format, i);
            return result;
        }
    }
}
