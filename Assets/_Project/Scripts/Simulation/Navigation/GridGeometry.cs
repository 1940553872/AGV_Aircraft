using System;
using UnityEngine;

namespace RivetSim.Simulation.Navigation
{
    /// <summary>
    /// 平面栅格几何：原点（最小角，x = 世界 X、y = 世界 Z）、分辨率与格数；提供世界坐标与格坐标互换。
    /// </summary>
    [Serializable]
    public struct GridGeometry
    {
        /// <summary>栅格最小角的平面坐标，单位 m。</summary>
        public Vector2 originM;

        /// <summary>分辨率（格边长），单位 m。</summary>
        public float resolutionM;

        /// <summary>X 方向格数。</summary>
        public int width;

        /// <summary>Z 方向格数。</summary>
        public int height;

        /// <summary>创建栅格几何。</summary>
        public GridGeometry(Vector2 originM, float resolutionM, int width, int height)
        {
            if (resolutionM <= 0f) throw new ArgumentOutOfRangeException(nameof(resolutionM));
            this.originM = originM;
            this.resolutionM = resolutionM;
            this.width = width;
            this.height = height;
        }

        /// <summary>覆盖以 centerM 为中心、尺寸 sizeM 的矩形（格数向上取整）。</summary>
        public static GridGeometry Covering(Vector2 centerM, Vector2 sizeM, float resolutionM)
        {
            int w = Mathf.CeilToInt(sizeM.x / resolutionM - 1e-4f);
            int h = Mathf.CeilToInt(sizeM.y / resolutionM - 1e-4f);
            return new GridGeometry(centerM - 0.5f * new Vector2(w, h) * resolutionM, resolutionM, w, h);
        }

        /// <summary>格子总数。</summary>
        public int CellCount => width * height;

        /// <summary>栅格最大角的平面坐标，单位 m。</summary>
        public Vector2 MaxM => originM + new Vector2(width, height) * resolutionM;

        /// <summary>格坐标是否在栅格内。</summary>
        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;

        /// <summary>格坐标是否在栅格内。</summary>
        public bool InBounds(Vector2Int c) => InBounds(c.x, c.y);

        /// <summary>格坐标 → 一维下标。</summary>
        public int Index(int x, int y) => y * width + x;

        /// <summary>格坐标 → 一维下标。</summary>
        public int Index(Vector2Int c) => c.y * width + c.x;

        /// <summary>一维下标 → 格坐标。</summary>
        public Vector2Int CellOf(int index) => new Vector2Int(index % width, index / width);

        /// <summary>平面坐标所在的格（可能在栅格外）。</summary>
        public Vector2Int WorldToCell(Vector2 p) =>
            new Vector2Int(Mathf.FloorToInt((p.x - originM.x) / resolutionM), Mathf.FloorToInt((p.y - originM.y) / resolutionM));

        /// <summary>格中心的平面坐标，单位 m。</summary>
        public Vector2 CellCenter(int x, int y) => originM + new Vector2(x + 0.5f, y + 0.5f) * resolutionM;

        /// <summary>格中心的平面坐标，单位 m。</summary>
        public Vector2 CellCenter(Vector2Int c) => CellCenter(c.x, c.y);
    }
}
