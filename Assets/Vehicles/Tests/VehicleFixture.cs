using System;
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.Vehicles.Tests
{
    /// <summary>Physics scratch objects far outside the world (and away from the player tests' fixture), destroyed on Dispose.</summary>
    public sealed partial class VehicleFixture : IDisposable
    {
        public static readonly Vector3 Origin = new Vector3(7000f, 1000f, 7000f);
        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        public T Track<T>(T obj) where T : UnityEngine.Object
        {
            created.Add(obj);
            return obj;
        }

        /// <summary>Box collider centred at Origin + local, rotated by <paramref name="yaw"/>.</summary>
        public GameObject Box(string name, Vector3 local, Vector3 size, float yaw = 0f)
        {
            var go = Track(new GameObject(name));
            go.transform.SetPositionAndRotation(Origin + local, Quaternion.Euler(0f, yaw, 0f));
            go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        /// <summary>1 m thick square floor whose top face is at Origin.y + topY.</summary>
        public GameObject Floor(float size = 600f, float topY = 0f) =>
            Box("Floor", new Vector3(0f, topY - 0.5f, 0f), new Vector3(size, 1f, size));

        public void Sync() => Physics.SyncTransforms();

        public void Dispose()
        {
            DisposeExtras();
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        partial void DisposeExtras();
    }
}
