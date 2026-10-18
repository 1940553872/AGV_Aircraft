using System;
using UnityEngine;

namespace RivetSim.Simulation.Navigation
{
    /// <summary>
    /// 虚拟禁行区（场景组件）：2D 激光（离地 0.30 m）扫不到机身、机翼、平尾与发动机，由编辑器生成器把模型机可见网格三角形的地面投影
    /// 保守栅格化后写入本组件，并在各工位开放接近通道（ADR-010 / ADR-015）。运行时只读。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VirtualKeepOutZone : MonoBehaviour
    {
        [SerializeField, Tooltip("掩膜栅格几何")]
        private GridGeometry geometry;

        [SerializeField, HideInInspector, Tooltip("掩膜（按位压缩，1 = 投影内）")]
        private byte[] packedMask = Array.Empty<byte>();

        [SerializeField, Tooltip("工位接近通道")]
        private KeepOutChannel[] channels = Array.Empty<KeepOutChannel>();

        [SerializeField, Tooltip("投影内的格数（生成时统计）")]
        private int projectedCellCount;

        /// <summary>掩膜栅格几何。</summary>
        public GridGeometry Geometry => geometry;

        /// <summary>投影内的格数。</summary>
        public int ProjectedCellCount => projectedCellCount;

        /// <summary>编辑器生成器写入掩膜与通道。</summary>
        public void Configure(GridGeometry maskGeometry, bool[] blocked, KeepOutChannel[] stationChannels)
        {
            if (blocked == null || blocked.Length != maskGeometry.CellCount)
                throw new ArgumentException("掩膜长度与栅格格数不一致。", nameof(blocked));
            geometry = maskGeometry;
            packedMask = new byte[(blocked.Length + 7) / 8];
            projectedCellCount = 0;
            for (int i = 0; i < blocked.Length; i++)
            {
                if (!blocked[i]) continue;
                packedMask[i >> 3] |= (byte)(1 << (i & 7));
                projectedCellCount++;
            }
            channels = stationChannels ?? Array.Empty<KeepOutChannel>();
        }

        /// <summary>解压为运行时掩膜。</summary>
        public KeepOutMask ToMask()
        {
            var blocked = new bool[geometry.CellCount];
            for (int i = 0; i < blocked.Length && (i >> 3) < packedMask.Length; i++)
                blocked[i] = (packedMask[i >> 3] & (1 << (i & 7))) != 0;
            return new KeepOutMask(geometry, blocked, channels);
        }
    }
}
