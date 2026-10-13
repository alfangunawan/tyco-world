using System;
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen
{
    /// <summary>OSM metadata for one generated feature. Registers itself for runtime lookup.</summary>
    [DisallowMultipleComponent]
    public sealed class OsmFeature : MonoBehaviour
    {
        [SerializeField] long osmId;
        [SerializeField] OsmElementType elementType;
        [SerializeField] FeatureKind kind;
        [SerializeField] string displayName = "";
        [SerializeField] string[] tagKeys = Array.Empty<string>();
        [SerializeField] string[] tagValues = Array.Empty<string>();

        public long OsmId => osmId;
        public OsmElementType ElementType => elementType;
        public FeatureKind Kind => kind;
        public string DisplayName => displayName;
        public int TagCount => tagKeys.Length;

        public void Initialize(long id, OsmElementType type, FeatureKind featureKind, string name,
            IEnumerable<KeyValuePair<string, string>> tags)
        {
            osmId = id;
            elementType = type;
            kind = featureKind;
            displayName = name ?? "";
            var pairs = new List<KeyValuePair<string, string>>();
            if (tags != null) pairs.AddRange(tags);
            pairs.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            tagKeys = new string[pairs.Count];
            tagValues = new string[pairs.Count];
            for (int i = 0; i < pairs.Count; i++)
            {
                tagKeys[i] = pairs[i].Key;
                tagValues[i] = pairs[i].Value;
            }
        }

        public bool TryGetTag(string key, out string value)
        {
            for (int i = 0; i < tagKeys.Length; i++)
            {
                if (tagKeys[i] != key) continue;
                value = tagValues[i];
                return true;
            }
            value = null;
            return false;
        }

        void OnEnable() => OsmFeatureRegistry.Register(this);

        void OnDisable() => OsmFeatureRegistry.Unregister(this);
    }
}
