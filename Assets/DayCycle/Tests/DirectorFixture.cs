using System;
using System.Collections.Generic;
using TycoWorld.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace TycoWorld.DayCycle.Tests
{
    /// <summary>A code-built director with sun, moon, three volumes, a skybox, ambience and (optionally) a player.</summary>
    internal sealed class DirectorFixture : IDisposable
    {
        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        readonly RenderSettingsSnapshot snapshot = new RenderSettingsSnapshot();

        public readonly DayCycleProfile Profile;
        public readonly Light Sun, Moon;
        public readonly Volume Day, Night, Horror;
        public readonly Material Sky;
        public readonly GameObject Root;
        public readonly AmbienceAudio Ambience;
        public readonly DayCycleDirector Director;
        public FirstPersonPlayer Player { get; private set; }
        public int Started, Ended;

        public DirectorFixture()
        {
            Profile = Track(DayCycleProfile.CreateDefault());
            Sun = NewLight("Sun");
            Moon = NewLight("Moon");
            Night = NewVolume("Volume_Night", 0f);
            Day = NewVolume("Volume_Day", 1f);
            Horror = NewVolume("Volume_Horror", 2f);
            Sky = Track(new Material(Shader.Find("Skybox/Procedural")));
            Sky.SetFloat("_Exposure", 9f);
            Root = Track(new GameObject("DayCycle"));
            Ambience = Root.AddComponent<AmbienceAudio>();
            Director = Root.AddComponent<DayCycleDirector>();
            Wire(null);
            Director.FindWorldRoot = () => null;
            Director.HorrorStarted += () => Started++;
            Director.HorrorEnded += () => Ended++;
        }

        public T Track<T>(T obj) where T : UnityEngine.Object
        {
            created.Add(obj);
            return obj;
        }

        public void Wire(FirstPersonPlayer player) =>
            Director.Configure(Profile, Sun, Moon, Day, Night, Horror, Sky, Ambience, player);

        /// <summary>An initialized player far outside the world, wired into the director (for HUD checks).</summary>
        public FirstPersonPlayer AddPlayer()
        {
            var tuning = Track(ScriptableObject.CreateInstance<PlayerTuning>());
            var go = Track(new GameObject("Player"));
            go.transform.position = new Vector3(5000f, 1000f, 5000f);
            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(go.transform, false);
            var view = new GameObject("Camera").transform;
            view.SetParent(pivot, false);
            Player = go.AddComponent<FirstPersonPlayer>();
            Player.Configure(tuning, pivot, view);
            Player.Initialize();
            Wire(Player);
            return Player;
        }

        Light NewLight(string name)
        {
            var light = Track(new GameObject(name)).AddComponent<Light>();
            light.type = LightType.Directional;
            return light;
        }

        Volume NewVolume(string name, float priority)
        {
            var volume = Track(new GameObject(name)).AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;
            return volume;
        }

        public void Dispose()
        {
            Director.Shutdown();
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
            created.Clear();
            snapshot.Restore();
        }
    }
}
