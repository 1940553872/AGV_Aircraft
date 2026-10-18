using System;
using System.Collections.Generic;
using UnityEngine;

namespace RivetSim.Simulation.Navigation
{
    /// <summary>
    /// 栅格 A*（纯计算）：8 邻域、禁止斜向切角、八向距离启发；步长代价 = 几何距离 ×（1 + 目标格附加代价）。
    /// 起点落在膨胀区内时（如停在充电桩前、贴近障碍物），允许先在膨胀区内移动，直到离开膨胀区；致命区始终不可进入。
    /// 内部数组按栅格大小复用，单次规划不产生与路径长度无关的分配。
    /// </summary>
    public sealed class GridAStar
    {
        private static readonly Vector2Int[] Neighbors =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1),
        };

        private const float Sqrt2 = 1.41421356f;

        private float[] _g = Array.Empty<float>();
        private int[] _parent = Array.Empty<int>();
        private int[] _stamp = Array.Empty<int>();
        private bool[] _closed = Array.Empty<bool>();
        private bool[] _escaping = Array.Empty<bool>();
        private int _generation;
        private readonly MinHeap _open = new MinHeap();

        /// <summary>最近一次规划展开的节点数。</summary>
        public int ExpandedCount { get; private set; }

        /// <summary>
        /// 规划从 start 到 goal 的格路径（含两端）；不可达时返回 false 并给出原因。
        /// </summary>
        public bool TryPlan(CostMap costMap, Vector2Int start, Vector2Int goal, List<Vector2Int> path, out string failure)
        {
            path.Clear();
            GridGeometry g = costMap.Geometry;
            failure = "";
            if (!g.InBounds(start) || !g.InBounds(goal))
            {
                failure = "起点或终点在地图外";
                return false;
            }
            int startIndex = g.Index(start), goalIndex = g.Index(goal);
            if (costMap.IsLethal(startIndex))
            {
                failure = "起点在障碍物内";
                return false;
            }
            if (costMap.IsBlocked(goalIndex))
            {
                failure = "终点在障碍物或其膨胀区内";
                return false;
            }

            EnsureCapacity(g.CellCount);
            _generation++;
            _open.Clear();
            ExpandedCount = 0;
            Open(startIndex, 0f, -1, costMap.IsBlocked(startIndex), Heuristic(start, goal));

            while (_open.Count > 0)
            {
                int current = _open.Pop();
                if (_closed[current]) continue;
                _closed[current] = true;
                ExpandedCount++;
                if (current == goalIndex)
                {
                    BuildPath(g, goalIndex, path);
                    return true;
                }

                Vector2Int c = g.CellOf(current);
                for (int k = 0; k < Neighbors.Length; k++)
                {
                    Vector2Int d = Neighbors[k];
                    Vector2Int nc = c + d;
                    if (!g.InBounds(nc)) continue;
                    int ni = g.Index(nc);
                    if (IsVisited(ni) && _closed[ni]) continue;
                    if (!CanEnter(costMap, current, ni, out bool escaping)) continue;
                    // 斜向时两侧正交格都须可进入，避免擦过障碍物角。
                    if (d.x != 0 && d.y != 0 &&
                        (!CanEnter(costMap, current, g.Index(c.x + d.x, c.y), out _) || !CanEnter(costMap, current, g.Index(c.x, c.y + d.y), out _)))
                        continue;

                    float step = (d.x != 0 && d.y != 0 ? Sqrt2 : 1f) * (1f + costMap.ExtraCost(ni));
                    float tentative = _g[current] + step;
                    if (IsVisited(ni) && tentative >= _g[ni]) continue;
                    Open(ni, tentative, current, escaping, tentative + Heuristic(nc, goal));
                }
            }
            failure = "终点不可达";
            return false;
        }

        // 致命区不可进入；膨胀区只在“仍处于起点所在膨胀区内”（逃离阶段）时可进入。
        private bool CanEnter(CostMap costMap, int from, int to, out bool escaping)
        {
            escaping = false;
            if (costMap.IsLethal(to)) return false;
            if (!costMap.IsBlocked(to)) return true;
            escaping = _escaping[from];
            return escaping;
        }

        private void Open(int index, float g, int parent, bool escaping, float f)
        {
            _stamp[index] = _generation;
            _closed[index] = false;
            _g[index] = g;
            _parent[index] = parent;
            _escaping[index] = escaping;
            _open.Push(index, f);
        }

        private bool IsVisited(int index) => _stamp[index] == _generation;

        private static float Heuristic(Vector2Int a, Vector2Int b)
        {
            int dx = Math.Abs(a.x - b.x), dy = Math.Abs(a.y - b.y);
            return Math.Max(dx, dy) + (Sqrt2 - 1f) * Math.Min(dx, dy);
        }

        private void BuildPath(GridGeometry g, int goalIndex, List<Vector2Int> path)
        {
            for (int i = goalIndex; i >= 0; i = _parent[i]) path.Add(g.CellOf(i));
            path.Reverse();
        }

        private void EnsureCapacity(int n)
        {
            if (_g.Length == n) return;
            _g = new float[n];
            _parent = new int[n];
            _stamp = new int[n];
            _closed = new bool[n];
            _escaping = new bool[n];
            _generation = 0;
        }

        // 二叉最小堆（允许重复入堆，出堆时跳过已关闭的节点）。
        private sealed class MinHeap
        {
            private readonly List<int> _items = new List<int>();
            private readonly List<float> _keys = new List<float>();

            public int Count => _items.Count;

            public void Clear()
            {
                _items.Clear();
                _keys.Clear();
            }

            public void Push(int item, float key)
            {
                _items.Add(item);
                _keys.Add(key);
                int i = _items.Count - 1;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (_keys[p] <= _keys[i]) break;
                    Swap(i, p);
                    i = p;
                }
            }

            public int Pop()
            {
                int top = _items[0];
                int last = _items.Count - 1;
                Swap(0, last);
                _items.RemoveAt(last);
                _keys.RemoveAt(last);
                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1, r = l + 1, m = i;
                    if (l < _items.Count && _keys[l] < _keys[m]) m = l;
                    if (r < _items.Count && _keys[r] < _keys[m]) m = r;
                    if (m == i) break;
                    Swap(i, m);
                    i = m;
                }
                return top;
            }

            private void Swap(int a, int b)
            {
                (_items[a], _items[b]) = (_items[b], _items[a]);
                (_keys[a], _keys[b]) = (_keys[b], _keys[a]);
            }
        }
    }
}
