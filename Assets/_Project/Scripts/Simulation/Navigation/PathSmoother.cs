using System.Collections.Generic;
using UnityEngine;

namespace RivetSim.Simulation.Navigation
{
    /// <summary>
    /// 路径平滑（纯计算）：对 A* 格路径做视线捷径——从当前顶点出发向前延伸，取直线保持可通行的最远格作为下一个顶点；
    /// 起点逃离膨胀区的那一段保持原样。输出首尾替换为实际起点与目标点。
    /// </summary>
    public static class PathSmoother
    {
        /// <summary>平滑格路径，返回平面折线。</summary>
        public static List<Vector2> Smooth(CostMap costMap, IReadOnlyList<Vector2Int> cells, Vector2 startM, Vector2 goalM)
        {
            var result = new List<Vector2> { startM };
            if (cells.Count == 0)
            {
                result.Add(goalM);
                return result;
            }
            GridGeometry g = costMap.Geometry;

            // 逃离段：起点所在膨胀区内的格逐个保留。
            int anchor = 0;
            while (anchor < cells.Count - 1 && costMap.IsBlocked(g.Index(cells[anchor])))
            {
                if (anchor > 0) result.Add(g.CellCenter(cells[anchor]));
                anchor++;
            }

            while (anchor < cells.Count - 1)
            {
                int next = anchor + 1;
                // 向前延伸到第一次被遮挡为止（复杂度与路径长度成线性关系）。
                for (int k = anchor + 2; k < cells.Count; k++)
                {
                    if (!LineOfSight(costMap, g.CellCenter(cells[anchor]), g.CellCenter(cells[k]))) break;
                    next = k;
                }
                if (next < cells.Count - 1) result.Add(g.CellCenter(cells[next]));
                anchor = next;
            }
            result.Add(goalM);
            return result;
        }

        /// <summary>两点之间的直线是否只经过可通行格（按半格步长采样）。</summary>
        public static bool LineOfSight(CostMap costMap, Vector2 a, Vector2 b)
        {
            GridGeometry g = costMap.Geometry;
            float step = 0.5f * g.resolutionM;
            int samples = Mathf.CeilToInt(Vector2.Distance(a, b) / step);
            for (int i = 0; i <= samples; i++)
            {
                Vector2 p = samples == 0 ? a : Vector2.Lerp(a, b, (float)i / samples);
                if (costMap.IsBlocked(g.WorldToCell(p))) return false;
            }
            return true;
        }
    }
}
