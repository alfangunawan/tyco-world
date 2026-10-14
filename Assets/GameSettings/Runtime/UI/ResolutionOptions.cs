using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.GameSettings
{
    /// <summary>The resolution dropdown: "Saat ini" (keep the current size) followed by distinct sizes, largest first.</summary>
    public static class ResolutionOptions
    {
        public static List<Vector2Int> Distinct(IEnumerable<Vector2Int> sizes)
        {
            var result = new List<Vector2Int>();
            if (sizes == null) return result;
            foreach (var size in sizes)
                if (size.x > 0 && size.y > 0 && !result.Contains(size)) result.Add(size);
            result.Sort((a, b) => a.x != b.x ? b.x.CompareTo(a.x) : b.y.CompareTo(a.y));
            return result;
        }

        public static List<string> Labels(IReadOnlyList<Vector2Int> sizes)
        {
            var labels = new List<string> { SettingsText.CurrentResolution };
            if (sizes != null)
                foreach (var size in sizes) labels.Add(SettingsText.Resolution(size));
            return labels;
        }

        /// <summary>Dropdown index of a stored resolution: 0 ("Saat ini") for 0 × 0 or a size not in the list.</summary>
        public static int IndexOf(IReadOnlyList<Vector2Int> sizes, int width, int height)
        {
            for (int i = 0; i < sizes.Count; i++)
                if (sizes[i].x == width && sizes[i].y == height) return i + 1;
            return 0;
        }

        /// <summary>The size a dropdown index stands for; index 0 or out of range → 0 × 0 (keep current).</summary>
        public static Vector2Int At(IReadOnlyList<Vector2Int> sizes, int index) =>
            index >= 1 && index <= sizes.Count ? sizes[index - 1] : Vector2Int.zero;
    }
}
