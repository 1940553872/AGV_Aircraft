using System;
using System.Collections.Generic;
using UnityEngine;

namespace RivetSim.Simulation.Navigation
{
    /// <summary>
    /// 代价地图（纯计算）：激光层（占据栅格）叠加虚拟禁行区层（模型机投影外扩机身安全距离，工位接近通道内不叠加），
    /// 两层再按车体外接圆半径膨胀（ADR-015）。格分三级：可通行、膨胀区（车体中心不可进入）、致命区（障碍本身）；
    /// 未知格可通行但带附加代价。
    /// </summary>
    public sealed class CostMap
    {
        private const byte FreeCell = 0;
        private const byte InflatedCell = 1;
        private const byte LethalCell = 2;

        private readonly byte[] _state;
        private readonly bool[] _unknown;
        private readonly bool[] _channel;
        private readonly bool[] _virtualCore;
        private readonly bool[] _lidarLethal;
        private float _unknownExtraCost;

        /// <summary>栅格几何。</summary>
        public GridGeometry Geometry { get; }

        /// <summary>最近一次重建使用的膨胀半径，单位 m。</summary>
        public float InflationRadiusM { get; private set; }

        /// <summary>创建全部可通行的代价地图。</summary>
        public CostMap(GridGeometry geometry)
        {
            Geometry = geometry;
            int n = geometry.CellCount;
            _state = new byte[n];
            _unknown = new bool[n];
            _channel = new bool[n];
            _virtualCore = new bool[n];
            _lidarLethal = new bool[n];
        }

        /// <summary>车体中心是否不可进入（膨胀区或致命区）。</summary>
        public bool IsBlocked(int index) => _state[index] != FreeCell;

        /// <summary>是否为致命区（障碍本身或外扩后的虚拟禁行区）。</summary>
        public bool IsLethal(int index) => _state[index] == LethalCell;

        /// <summary>格坐标是否不可进入；栅格外视为不可进入。</summary>
        public bool IsBlocked(Vector2Int c) => !Geometry.InBounds(c) || IsBlocked(Geometry.Index(c));

        /// <summary>进入该格的附加代价（未知格）。</summary>
        public float ExtraCost(int index) => _unknown[index] ? _unknownExtraCost : 0f;

        /// <summary>该格是否在工位接近通道内。</summary>
        public bool InChannel(int index) => _channel[index];

        /// <summary>该格是否在模型机投影内（通道外部分）。</summary>
        public bool IsVirtualCore(int index) => _virtualCore[index];

        /// <summary>
        /// 重建代价地图。map 为空时激光层视为全部已知空闲；keepOut 为空时不叠加虚拟层。
        /// </summary>
        public void Rebuild(OccupancyGridMap map, KeepOutMask keepOut, float inflationRadiusM, float virtualDilationM, float unknownExtraCost)
        {
            GridGeometry g = Geometry;
            int n = g.CellCount;
            InflationRadiusM = inflationRadiusM;
            _unknownExtraCost = unknownExtraCost;
            Array.Clear(_state, 0, n);
            Array.Clear(_channel, 0, n);
            Array.Clear(_virtualCore, 0, n);

            for (int i = 0; i < n; i++)
            {
                OccupancyState s = map != null && map.Geometry.CellCount == n ? map.GetState(i) : OccupancyState.Free;
                _unknown[i] = s == OccupancyState.Unknown;
                _lidarLethal[i] = s == OccupancyState.Occupied;
            }

            if (keepOut != null)
            {
                MarkChannels(keepOut.Channels);
                for (int y = 0; y < g.height; y++)
                for (int x = 0; x < g.width; x++)
                {
                    int i = g.Index(x, y);
                    if (!_channel[i] && keepOut.IsProjected(g.CellCenter(x, y))) _virtualCore[i] = true;
                }
            }

            Stamp(_lidarLethal, 0f, inflationRadiusM, false);
            if (keepOut != null) Stamp(_virtualCore, virtualDilationM, virtualDilationM + inflationRadiusM, true);
        }

        // 从源格的边界格向外盖印：距离 ≤ lethalRadius 为致命区，≤ inflatedRadius 为膨胀区；skipChannel 时通道内的格不受影响。
        private void Stamp(bool[] source, float lethalRadiusM, float inflatedRadiusM, bool skipChannel)
        {
            GridGeometry g = Geometry;
            List<Vector3Int> offsets = DiscOffsets(inflatedRadiusM, lethalRadiusM);
            for (int y = 0; y < g.height; y++)
            for (int x = 0; x < g.width; x++)
            {
                int i = g.Index(x, y);
                if (!source[i]) continue;
                if (!skipChannel || !_channel[i]) _state[i] = LethalCell;
                if (!IsBoundary(source, x, y)) continue;
                foreach (Vector3Int o in offsets)
                {
                    int nx = x + o.x, ny = y + o.y;
                    if (!g.InBounds(nx, ny)) continue;
                    int j = g.Index(nx, ny);
                    if (skipChannel && _channel[j]) continue;
                    byte level = (byte)o.z;
                    if (level > _state[j]) _state[j] = level;
                }
            }
        }

        private bool IsBoundary(bool[] source, int x, int y)
        {
            GridGeometry g = Geometry;
            return !Get(source, x - 1, y) || !Get(source, x + 1, y) || !Get(source, x, y - 1) || !Get(source, x, y + 1);

            bool Get(bool[] s, int cx, int cy) => g.InBounds(cx, cy) && s[g.Index(cx, cy)];
        }

        // 圆盘偏移（z = 该偏移处的等级）：按格中心距离判定。
        private List<Vector3Int> DiscOffsets(float inflatedRadiusM, float lethalRadiusM)
        {
            float res = Geometry.resolutionM;
            int r = Mathf.CeilToInt(inflatedRadiusM / res);
            float inflated2 = inflatedRadiusM * inflatedRadiusM;
            float lethal2 = lethalRadiusM * lethalRadiusM;
            var list = new List<Vector3Int>();
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                float d2 = (dx * dx + dy * dy) * res * res;
                if (d2 > inflated2) continue;
                list.Add(new Vector3Int(dx, dy, d2 <= lethal2 && lethalRadiusM > 0f ? LethalCell : InflatedCell));
            }
            return list;
        }

        private void MarkChannels(IReadOnlyList<KeepOutChannel> channels)
        {
            GridGeometry g = Geometry;
            foreach (KeepOutChannel channel in channels)
            {
                Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
                foreach (Vector2 c in channel.Corners())
                {
                    min = Vector2.Min(min, c);
                    max = Vector2.Max(max, c);
                }
                Vector2Int a = g.WorldToCell(min), b = g.WorldToCell(max);
                for (int y = Math.Max(0, a.y); y <= Math.Min(g.height - 1, b.y); y++)
                for (int x = Math.Max(0, a.x); x <= Math.Min(g.width - 1, b.x); x++)
                    if (channel.Contains(g.CellCenter(x, y))) _channel[g.Index(x, y)] = true;
            }
        }
    }
}
