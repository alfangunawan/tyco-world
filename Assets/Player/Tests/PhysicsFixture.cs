using System;
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.Player.Tests
{
    /// <summary>Tracks test GameObjects far outside the generated world and destroys them on Dispose.</summary>
    internal sealed class PhysicsFixture : IDisposable
    {
        public static readonly Vector3 Origin = new Vector3(5000f, 1000f, 5000f);
        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        public T Track<T>(T obj) where T : UnityEngine.Object
        {
            created.Add(obj);
            return obj;
        }

        /// <summary>Box collider centred at Origin + localCenter.</summary>
        public GameObject Box(string name, Vector3 localCenter, Vector3 size)
        {
            var go = Track(new GameObject(name));
            go.transform.position = Origin + localCenter;
            go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        /// <summary>1 m thick square floor whose top face is at Origin.y + topY.</summary>
        public GameObject Floor(float topY = 0f, float size = 100f) =>
            Box("Floor", new Vector3(0f, topY - 0.5f, 0f), new Vector3(size, 1f, size));

        public void Sync() => Physics.SyncTransforms();

        public void Dispose()
        {
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
            created.Clear();
        }
    }
}
