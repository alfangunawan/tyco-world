using System.Collections.Generic;
using UnityEngine;

namespace TycoWorld.WorldGen.Authoring
{
    /// <summary>Uniform hash grid for rectangle-overlap queries.</summary>
    public sealed class SpatialGrid<T>
    {
        readonly float cellSize;
        readonly Dictionary<long, List<T>> cells = new Dictionary<long, List<T>>();

        public SpatialGrid(float cellSize) { this.cellSize = cellSize; }

        static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;

        public void Insert(Rect area, T item)
        {
            int x0 = Mathf.FloorToInt(area.xMin / cellSize), x1 = Mathf.FloorToInt(area.xMax / cellSize);
            int y0 = Mathf.FloorToInt(area.yMin / cellSize), y1 = Mathf.FloorToInt(area.yMax / cellSize);
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                {
                    long key = Key(x, y);
                    if (!cells.TryGetValue(key, out var list))
                    {
                        list = new List<T>();
                        cells[key] = list;
                    }
                    list.Add(item);
                }
        }

        public void Query(Rect area, HashSet<T> results)
        {
            int x0 = Mathf.FloorToInt(area.xMin / cellSize), x1 = Mathf.FloorToInt(area.xMax / cellSize);
            int y0 = Mathf.FloorToInt(area.yMin / cellSize), y1 = Mathf.FloorToInt(area.yMax / cellSize);
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    if (cells.TryGetValue(Key(x, y), out var list))
                        foreach (var item in list) results.Add(item);
        }
    }
}
