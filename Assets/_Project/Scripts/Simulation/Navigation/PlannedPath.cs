using System.Collections.Generic;
using UnityEngine;

namespace RivetSim.Simulation.Navigation
{
    /// <summary>
    /// 规划路径：平面折线（x = 世界 X，y = 世界 Z）与总长度。
    /// </summary>
    public sealed class PlannedPath
    {
        private readonly List<Vector2> _points;

        /// <summary>折线顶点，单位 m。</summary>
        public IReadOnlyList<Vector2> Points => _points;

        /// <summary>总长度，单位 m。</summary>
        public float LengthM { get; }

        /// <summary>由顶点创建路径（复制一份）。</summary>
        public PlannedPath(IEnumerable<Vector2> points)
        {
            _points = new List<Vector2>(points);
            float length = 0f;
            for (int i = 1; i < _points.Count; i++) length += Vector2.Distance(_points[i - 1], _points[i]);
            LengthM = length;
        }
    }
}
