using RivetSim.Core.Config;
using UnityEngine;

namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// AGV 外廓量测（M3 建立，M4 改为运输姿态校核）：在车体坐标系（根节点，前进方向 +Z）中统计全部渲染器的范围、
    /// 机械臂部分的范围、整机最高点、绕车体中心旋转的扫掠半径与车头前伸量。useVertices 为 true 时用网格顶点（编辑器校核，精确），
    /// 否则用渲染器本地包围盒角点（运行时，偏保守）。
    /// </summary>
    public static class AgvFootprint
    {
        /// <summary>量测外廓；armRoot 为机械臂根节点（其下的渲染器计入机械臂范围）。</summary>
        public static AgvEnvelope Measure(Transform agvRoot, Transform armRoot, bool useVertices)
        {
            Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
            Vector3 armMin = min, armMax = max;
            float radius = 0f;
            Matrix4x4 toRoot = agvRoot.worldToLocalMatrix;
            var vertices = new System.Collections.Generic.List<Vector3>();
            foreach (Renderer renderer in agvRoot.GetComponentsInChildren<Renderer>())
            {
                if (renderer.name.StartsWith(SimLayers.VisualOnlyPrefix, System.StringComparison.Ordinal)) continue;
                bool isArm = armRoot != null && renderer.transform.IsChildOf(armRoot);
                Matrix4x4 m = toRoot * renderer.transform.localToWorldMatrix;
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (useVertices && filter != null && filter.sharedMesh != null)
                {
                    filter.sharedMesh.GetVertices(vertices);
                }
                else
                {
                    vertices.Clear();
                    Bounds b = renderer.localBounds;
                    for (int i = 0; i < 8; i++)
                        vertices.Add(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f)));
                }
                foreach (Vector3 v in vertices)
                {
                    Vector3 p = m.MultiplyPoint3x4(v);
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                    radius = Mathf.Max(radius, new Vector2(p.x, p.z).magnitude);
                    if (!isArm) continue;
                    armMin = Vector3.Min(armMin, p);
                    armMax = Vector3.Max(armMax, p);
                }
            }
            return new AgvEnvelope(min, max, armMin, armMax, radius);
        }
    }
}
