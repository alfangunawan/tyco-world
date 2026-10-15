using System;
using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.Vehicles
{
    /// <summary>
    /// The built vehicle models (spec vehicles-3a §4, §6): id → prefab, class and how its body may be painted. Written by
    /// VehicleModelAssets; read by gameplay to put warga vehicles on the street.
    /// </summary>
    public sealed class VehicleCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string id = "";
            public VehicleClass vehicleClass;
            public GameObject prefab;
            /// <summary>Names of the materials a repaint tints; empty = fixed livery (police, angkot).</summary>
            public string[] paintMaterials = new string[0];
            /// <summary>1 tints a body-only material fully; below 1 keeps an atlas's glass and lights readable.</summary>
            public float paintStrength = 1f;
        }

        public Entry[] entries = new Entry[0];

        public IReadOnlyList<Entry> Entries => entries;

        public Entry Get(string id)
        {
            foreach (var e in entries)
                if (e != null && e.id == id) return e;
            return null;
        }
    }
}
