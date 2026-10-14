using System;
using System.Collections.Generic;
using TycoWorld.DayCycle;
using TycoWorld.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace TycoWorld.GameSettings.Tests
{
    /// <summary>Code-built player, day-cycle director and scratch objects for settings tests; restores the globals they touch.</summary>
    internal sealed class GameFixture : IDisposable
    {
        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        readonly List<DayCycleDirector> directors = new List<DayCycleDirector>();
        readonly float listenerVolume = AudioListener.volume;
        readonly bool fog = RenderSettings.fog;
        readonly FogMode fogMode = RenderSettings.fogMode;
        readonly float fogDensity = RenderSettings.fogDensity;
        readonly Color fogColor = RenderSettings.fogColor;
        readonly AmbientMode ambientMode = RenderSettings.ambientMode;
        readonly Color ambientSky = RenderSettings.ambientSkyColor;
        readonly Color ambientEquator = RenderSettings.ambientEquatorColor;
        readonly Color ambientGround = RenderSettings.ambientGroundColor;
        readonly Material skybox = RenderSettings.skybox;
        readonly Light sun = RenderSettings.sun;

        public T Track<T>(T obj) where T : UnityEngine.Object
        {
            created.Add(obj);
            return obj;
        }

        /// <summary>An initialized first-person player far outside the world, with a real Camera on its view.</summary>
        public FirstPersonPlayer CreatePlayer()
        {
            var tuning = Track(ScriptableObject.CreateInstance<PlayerTuning>());
            var go = Track(new GameObject("Player"));
            go.transform.position = new Vector3(5000f, 1000f, 5000f);
            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(go.transform, false);
            var view = new GameObject("Camera");
            view.transform.SetParent(pivot, false);
            view.AddComponent<Camera>();
            var player = go.AddComponent<FirstPersonPlayer>();
            player.Configure(tuning, pivot, view.transform);
            player.Initialize();
            return player;
        }

        /// <summary>An initialized director at 12:00 with its own sun, moon and ambience (no volumes, no skybox, no world).</summary>
        public DayCycleDirector CreateDirector(FirstPersonPlayer player)
        {
            var profile = Track(DayCycleProfile.CreateDefault());
            var sunLight = NewLight("Sun");
            var moonLight = NewLight("Moon");
            var root = Track(new GameObject("DayCycle"));
            var ambience = root.AddComponent<AmbienceAudio>();
            var director = root.AddComponent<DayCycleDirector>();
            director.Configure(profile, sunLight, moonLight, null, null, null, null, ambience, player);
            director.FindWorldRoot = () => null;
            director.FindViewer = () => null;
            director.Initialize(12f);
            directors.Add(director);
            return director;
        }

        Light NewLight(string name)
        {
            var light = Track(new GameObject(name)).AddComponent<Light>();
            light.type = LightType.Directional;
            return light;
        }

        public void Dispose()
        {
            foreach (var director in directors)
                if (director != null) director.Shutdown();
            directors.Clear();
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
            created.Clear();
            AudioListener.volume = listenerVolume;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
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
