using System;
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen
{
    /// <summary>
    /// Hides the renderers of whole chunk groups beyond the view distance (props beyond the prop distance), every 0.25 s
    /// or at once after a jump over 50 m, with 10 m hysteresis. Colliders stay. Infinite distances (default) cull nothing
    /// (spec #4 §5.1).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DistanceCuller : MonoBehaviour
    {
        public const float IntervalSeconds = 0.25f;
        public const float HysteresisMeters = 10f;
        public const float JumpMeters = 50f;

        static readonly IReadOnlyList<ChunkGroup> NoGroups = Array.Empty<ChunkGroup>();

        float viewDistance = float.PositiveInfinity;
        float propDistance = float.PositiveInfinity;
        List<ChunkGroup> groups;
        GeneratedRoot root;
        float timer, searchTimer;
        Vector3 lastViewer;
        bool hasViewer, dirty;

        public float ViewDistance
        {
            get => viewDistance;
            set
            {
                if (value == viewDistance) return;
                viewDistance = value;
                dirty = true;
            }
        }

        public float PropDistance
        {
            get => propDistance;
            set
            {
                if (value == propDistance) return;
                propDistance = value;
                dirty = true;
            }
        }

        /// <summary>Finds the generated world (another scene, so it cannot be serialized); tests replace it.</summary>
        public Func<GeneratedRoot> FindWorldRoot { get; set; } = () => UnityEngine.Object.FindAnyObjectByType<GeneratedRoot>();

        /// <summary>Where distances are measured from (the active camera); tests replace it.</summary>
        public Func<Vector3?> FindViewer { get; set; } = MainCameraPosition;

        public bool HasWorld => groups != null;
        public IReadOnlyList<ChunkGroup> Groups => groups != null ? (IReadOnlyList<ChunkGroup>)groups : NoGroups;
        public int HiddenCount { get; private set; }

        void Update()
        {
            if (!Application.isPlaying) return;
            Tick(Time.unscaledDeltaTime);
        }

        void OnDisable() => ShowAll();

        public void Tick(float dt)
        {
            if (groups != null && root == null)
            {
                // The world scene was unloaded: its renderers are gone with it.
                groups = null;
                hasViewer = false;
                HiddenCount = 0;
            }
            if (groups == null)
            {
                searchTimer -= dt;
                if (searchTimer > 0f) return;
                searchTimer = IntervalSeconds;
                root = FindWorldRoot != null ? FindWorldRoot() : null;
                if (root == null) return;
                groups = WorldChunkIndex.Build(root.transform);
                hasViewer = false;
            }
            var viewer = FindViewer != null ? FindViewer() : null;
            if (!viewer.HasValue) return;
            timer += dt;
            bool jumped = !hasViewer || (viewer.Value - lastViewer).sqrMagnitude > JumpMeters * JumpMeters;
            if (!dirty && !jumped && timer < IntervalSeconds) return;
            Evaluate(viewer.Value);
        }

        public void Evaluate(Vector3 viewer)
        {
            if (groups == null) return;
            timer = 0f;
            dirty = false;
            lastViewer = viewer;
            hasViewer = true;
            int hidden = 0;
            foreach (var g in groups)
            {
                float limit = g.Category == CullCategory.Prop ? propDistance : viewDistance;
                float distance = g.DistanceTo(viewer);
                bool show = g.Visible ? distance <= limit + HysteresisMeters : distance <= limit - HysteresisMeters;
                g.SetVisible(show);
                if (!show) hidden++;
            }
            HiddenCount = hidden;
        }

        public void ShowAll()
        {
            if (groups == null) return;
            foreach (var g in groups) g.SetVisible(true);
            HiddenCount = 0;
        }

        static Vector3? MainCameraPosition()
        {
            var cam = Camera.main;
            return cam != null ? cam.transform.position : (Vector3?)null;
        }
    }
}
