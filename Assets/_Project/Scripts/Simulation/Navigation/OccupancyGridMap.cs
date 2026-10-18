using System;
using UnityEngine;

namespace RivetSim.Simulation.Navigation
{
    /// <summary>
    /// 占据栅格地图（教学简化模型）：使用真值位姿，激光命中格加分、射线经过格减分，分数为正判为占据、为负判为空闲；
    /// 不做扫描匹配与回环。记录脏矩形与版本号，供地图叠加层增量刷新。
    /// </summary>
    public sealed class OccupancyGridMap
    {
        // 命中 / 经过的计分与上下限：命中权重大于经过，测距噪声擦过障碍物边缘时不会把已确认的障碍清掉。
        private const sbyte HitScore = 2;
        private const sbyte MissScore = -1;
        private const sbyte MaxScore = 6;
        private const sbyte MinScore = -4;

        private readonly sbyte[] _scores;
        private RectInt _dirty;
        private bool _hasDirty;

        /// <summary>栅格几何。</summary>
        public GridGeometry Geometry { get; }

        /// <summary>每次写入后递增的版本号。</summary>
        public int Revision { get; private set; }

        /// <summary>已观测（空闲或占据）的格数。</summary>
        public int KnownCellCount { get; private set; }

        /// <summary>创建全部为未观测的地图。</summary>
        public OccupancyGridMap(GridGeometry geometry)
        {
            Geometry = geometry;
            _scores = new sbyte[geometry.CellCount];
        }

        /// <summary>按下标读取状态。</summary>
        public OccupancyState GetState(int index)
        {
            sbyte s = _scores[index];
            return s > 0 ? OccupancyState.Occupied : s < 0 ? OccupancyState.Free : OccupancyState.Unknown;
        }

        /// <summary>按格坐标读取状态；栅格外视为未观测。</summary>
        public OccupancyState GetState(int x, int y) => Geometry.InBounds(x, y) ? GetState(Geometry.Index(x, y)) : OccupancyState.Unknown;

        /// <summary>直接把一格设为占据（供测试与预置地图）。</summary>
        public void MarkOccupied(int x, int y) => Apply(x, y, MaxScore, true);

        /// <summary>直接把一格设为空闲（供测试与预置地图）。</summary>
        public void MarkFree(int x, int y) => Apply(x, y, MinScore, true);

        /// <summary>
        /// 写入一条激光射线：起点到终点经过的格计为空闲；hit 为 true 时终点格计为占据。
        /// </summary>
        public void IntegrateRay(Vector2 originM, Vector2 endM, bool hit)
        {
            Vector2Int a = Geometry.WorldToCell(originM);
            Vector2Int b = Geometry.WorldToCell(endM);
            int dx = Math.Abs(b.x - a.x), sx = a.x < b.x ? 1 : -1;
            int dy = -Math.Abs(b.y - a.y), sy = a.y < b.y ? 1 : -1;
            int err = dx + dy;
            int x = a.x, y = a.y;
            while (x != b.x || y != b.y)
            {
                Apply(x, y, MissScore, false);
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x += sx; }
                if (e2 <= dx) { err += dx; y += sy; }
            }
            Apply(b.x, b.y, hit ? HitScore : MissScore, false);
        }

        /// <summary>一次扫描写入结束：版本号加一。</summary>
        public void CommitScan() => Revision++;

        /// <summary>取出自上次调用以来改变过的格的外接矩形；没有改变时返回 false。</summary>
        public bool ConsumeDirtyRect(out RectInt rect)
        {
            rect = _dirty;
            bool had = _hasDirty;
            _hasDirty = false;
            return had;
        }

        /// <summary>以 centerM 为圆心、半径 radiusM 的圆内是否有占据格。</summary>
        public bool AnyOccupiedWithin(Vector2 centerM, float radiusM)
        {
            Vector2Int min = Geometry.WorldToCell(centerM - Vector2.one * radiusM);
            Vector2Int max = Geometry.WorldToCell(centerM + Vector2.one * radiusM);
            float r2 = radiusM * radiusM;
            for (int y = Math.Max(0, min.y); y <= Math.Min(Geometry.height - 1, max.y); y++)
            for (int x = Math.Max(0, min.x); x <= Math.Min(Geometry.width - 1, max.x); x++)
            {
                if (GetState(Geometry.Index(x, y)) != OccupancyState.Occupied) continue;
                if ((Geometry.CellCenter(x, y) - centerM).sqrMagnitude <= r2) return true;
            }
            return false;
        }

        /// <summary>
        /// 有向矩形（中心 centerM、车头方向偏航 yawDeg、横向半宽 halfWidthM、纵向从 rearM 到 frontM）内是否有占据格。
        /// </summary>
        public bool AnyOccupiedInRect(Vector2 centerM, float yawDeg, float halfWidthM, float rearM, float frontM)
        {
            float yaw = yawDeg * Mathf.Deg2Rad;
            var heading = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
            var right = new Vector2(heading.y, -heading.x);
            float reach = Mathf.Max(Mathf.Abs(rearM), Mathf.Abs(frontM)) + halfWidthM;
            Vector2Int min = Geometry.WorldToCell(centerM - Vector2.one * reach);
            Vector2Int max = Geometry.WorldToCell(centerM + Vector2.one * reach);
            for (int y = Math.Max(0, min.y); y <= Math.Min(Geometry.height - 1, max.y); y++)
            for (int x = Math.Max(0, min.x); x <= Math.Min(Geometry.width - 1, max.x); x++)
            {
                if (GetState(Geometry.Index(x, y)) != OccupancyState.Occupied) continue;
                Vector2 d = Geometry.CellCenter(x, y) - centerM;
                float lon = Vector2.Dot(d, heading);
                if (lon >= rearM && lon <= frontM && Mathf.Abs(Vector2.Dot(d, right)) <= halfWidthM) return true;
            }
            return false;
        }

        private void Apply(int x, int y, sbyte delta, bool absolute)
        {
            if (!Geometry.InBounds(x, y)) return;
            int i = Geometry.Index(x, y);
            sbyte before = _scores[i];
            sbyte after = absolute ? delta : (sbyte)Math.Clamp(before + delta, MinScore, MaxScore);
            if (after == before) return;
            _scores[i] = after;
            if (before == 0) KnownCellCount++;
            else if (after == 0) KnownCellCount--;
            if (Math.Sign(before) == Math.Sign(after)) return;
            if (!_hasDirty)
            {
                _dirty = new RectInt(x, y, 1, 1);
                _hasDirty = true;
                return;
            }
            int xMin = Math.Min(_dirty.xMin, x), yMin = Math.Min(_dirty.yMin, y);
            int xMax = Math.Max(_dirty.xMax, x + 1), yMax = Math.Max(_dirty.yMax, y + 1);
            _dirty = new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        }
    }
}
