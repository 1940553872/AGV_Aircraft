using System;
using System.Collections.Generic;
using RivetSim.Core.Config;
using UnityEngine;

namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 车体（底盘、升降柱各节、设备盒等，机械臂除外）到机身外壳的最小三维距离（M8 验证用）：
    /// 每个网格渲染器取本地包围盒的 26 个表面采样点（8 角、12 棱中点、6 面中心），先用 <c>Physics.CheckSphere</c>（搜索上限半径）筛选，
    /// 命中的点再二分半径求到外壳碰撞体的距离。机械臂与探头末端由 <see cref="ArmClearance"/> 负责（含探头前端排除）。
    /// 采样点在包围盒上，结果偏保守（不大于真实最小距离）。
    /// </summary>
    public sealed class VehicleClearanceMeter
    {
        private const int BisectionSteps = 10;

        private readonly List<(Transform t, Vector3[] points, string name)> _parts = new List<(Transform, Vector3[], string)>();
        private readonly int _layerMask;
        private readonly float _searchMaxM;

        /// <summary>参与测量的渲染器数。</summary>
        public int PartCount => _parts.Count;

        /// <summary>创建；vehicleRoot 为本车根节点，excludeRoot（机械臂）下的渲染器不计入；layerMask 为机身外壳所在层。</summary>
        public VehicleClearanceMeter(Transform vehicleRoot, Transform excludeRoot, int layerMask, float searchMaxM)
        {
            _layerMask = layerMask;
            _searchMaxM = searchMaxM;
            foreach (MeshFilter mf in vehicleRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || (excludeRoot != null && mf.transform.IsChildOf(excludeRoot))) continue;
                if (mf.gameObject.layer == SimLayers.VisualizationIndex || mf.name.StartsWith(SimLayers.VisualOnlyPrefix, StringComparison.Ordinal)) continue;
                _parts.Add((mf.transform, SurfacePoints(mf.sharedMesh.bounds), mf.name));
            }
        }

        /// <summary>测量当前姿态下的最小距离，单位 m（搜索上限内无外壳时返回上限）；nearest 为最近的零件名。</summary>
        public float Measure(out string nearest)
        {
            float best = _searchMaxM;
            nearest = "—";
            foreach ((Transform t, Vector3[] points, string name) in _parts)
            {
                Matrix4x4 m = t.localToWorldMatrix;
                foreach (Vector3 local in points)
                {
                    Vector3 p = m.MultiplyPoint3x4(local);
                    if (!Physics.CheckSphere(p, best, _layerMask, QueryTriggerInteraction.Ignore)) continue;
                    if (Physics.CheckSphere(p, 0f, _layerMask, QueryTriggerInteraction.Ignore))
                    {
                        nearest = name;
                        return 0f;
                    }
                    float lo = 0f, hi = best;
                    for (int i = 0; i < BisectionSteps; i++)
                    {
                        float mid = (lo + hi) * 0.5f;
                        if (Physics.CheckSphere(p, mid, _layerMask, QueryTriggerInteraction.Ignore)) hi = mid;
                        else lo = mid;
                    }
                    if (hi < best)
                    {
                        best = hi;
                        nearest = name;
                    }
                }
            }
            return best;
        }

        private static Vector3[] SurfacePoints(Bounds b)
        {
            var points = new List<Vector3>(26);
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    for (int z = -1; z <= 1; z++)
                        if (x != 0 || y != 0 || z != 0)
                            points.Add(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z)));
            return points.ToArray();
        }
    }
}
