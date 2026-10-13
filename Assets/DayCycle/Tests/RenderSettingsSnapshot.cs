using UnityEngine;
using UnityEngine.Rendering;

namespace TycoWorld.DayCycle.Tests
{
    /// <summary>Saves the RenderSettings a day cycle test touches and puts them back in TearDown.</summary>
    internal sealed class RenderSettingsSnapshot
    {
        readonly AmbientMode ambientMode;
        readonly Color sky, equator, ground, fogColor;
        readonly bool fog;
        readonly FogMode fogMode;
        readonly float fogDensity;
        readonly Material skybox;
        readonly Light sun;

        public RenderSettingsSnapshot()
        {
            ambientMode = RenderSettings.ambientMode;
            sky = RenderSettings.ambientSkyColor;
            equator = RenderSettings.ambientEquatorColor;
            ground = RenderSettings.ambientGroundColor;
            fog = RenderSettings.fog;
            fogMode = RenderSettings.fogMode;
            fogDensity = RenderSettings.fogDensity;
            fogColor = RenderSettings.fogColor;
            skybox = RenderSettings.skybox;
            sun = RenderSettings.sun;
        }

        public void Restore()
        {
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.fog = fog;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogColor = fogColor;
            RenderSettings.skybox = skybox;
            RenderSettings.sun = sun;
        }
    }
}
