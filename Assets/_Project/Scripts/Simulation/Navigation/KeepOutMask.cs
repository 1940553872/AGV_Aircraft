using System;
using System.Collections.Generic;
using UnityEngine;

namespace RivetSim.Simulation.Navigation
{
    /// <summary>
    /// 虚拟禁行区数据（纯计算）：模型机网格地面投影的栅格掩膜 + 工位接近通道。由 <see cref="VirtualKeepOutZone"/> 提供，供代价地图使用。
    /// </summary>
    public sealed class KeepOutMask
    {
        private readonly bool[] _blocked;
        private readonly KeepOutChannel[] _channels;

        /// <summary>掩膜栅格几何。</summary>
        public GridGeometry Geometry { get; }

        /// <summary>接近通道。</summary>
        public IReadOnlyList<KeepOutChannel> Channels => _channels;

        /// <summary>创建掩膜；blocked 长度须等于格数。</summary>
        public KeepOutMask(GridGeometry geometry, bool[] blocked, KeepOutChannel[] channels)
        {
            if (blocked == null || blocked.Length != geometry.CellCount)
                throw new ArgumentException("掩膜长度与栅格格数不一致。", nameof(blocked));
            Geometry = geometry;
            _blocked = blocked;
            _channels = channels ?? Array.Empty<KeepOutChannel>();
        }

        /// <summary>平面点是否落在模型机投影内（不考虑通道）。</summary>
        public bool IsProjected(Vector2 p)
        {
            Vector2Int c = Geometry.WorldToCell(p);
            return Geometry.InBounds(c) && _blocked[Geometry.Index(c)];
        }

        /// <summary>平面点是否在某条接近通道内。</summary>
        public bool InChannel(Vector2 p)
        {
            foreach (KeepOutChannel channel in _channels)
                if (channel.Contains(p)) return true;
            return false;
        }
    }
}
