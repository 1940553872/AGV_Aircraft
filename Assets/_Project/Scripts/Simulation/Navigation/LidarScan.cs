using UnityEngine;

namespace RivetSim.Simulation.Navigation
{
    /// <summary>
    /// 一圈激光扫描：雷达位置与每条射线的终点（命中点或量程末端，x = 世界 X，y = 世界 Z）。数组在构造时分配并复用。
    /// </summary>
    public sealed class LidarScan
    {
        /// <summary>射线终点，单位 m。</summary>
        public readonly Vector2[] EndpointsM;

        /// <summary>射线是否命中。</summary>
        public readonly bool[] Hits;

        /// <summary>雷达位置（世界坐标）。</summary>
        public Vector3 OriginM { get; internal set; }

        /// <summary>本圈命中数。</summary>
        public int HitCount { get; internal set; }

        /// <summary>扫描序号（从 1 起）。</summary>
        public int Sequence { get; internal set; }

        /// <summary>创建 beamCount 条射线的扫描缓冲。</summary>
        public LidarScan(int beamCount)
        {
            EndpointsM = new Vector2[beamCount];
            Hits = new bool[beamCount];
        }
    }
}
