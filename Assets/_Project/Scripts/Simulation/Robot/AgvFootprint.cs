using UnityEngine;

namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// AGV 外廓量测：按全部渲染器的本地包围盒角点计算绕车体中心旋转时的扫掠半径与车头前伸量（含机械臂）。
    /// M4 定义运输姿态 Stow 前，UR5e 零位向前伸出车头约 0.43 m，原地转向与对接直线段按此半径检查（ADR-015）。
    /// </summary>
    public static class AgvFootprint
    {
        /// <summary>扫掠半径（水平面内到根节点的最大距离），单位 m。</summary>
        public static float MeasureSweptRadiusM(Transform agvRoot) => Measure(agvRoot).x;

        /// <summary>车头最前端到根节点的距离（本地 +Z），单位 m。</summary>
        public static float MeasureFrontReachM(Transform agvRoot) => Measure(agvRoot).y;

        private static Vector2 Measure(Transform agvRoot)
        {
            float radius = 0f;
            float front = 0f;
            Matrix4x4 toRoot = agvRoot.worldToLocalMatrix;
            foreach (Renderer renderer in agvRoot.GetComponentsInChildren<Renderer>())
            {
                Bounds b = renderer.localBounds;
                Matrix4x4 m = toRoot * renderer.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    radius = Mathf.Max(radius, new Vector2(p.x, p.z).magnitude);
                    front = Mathf.Max(front, p.z);
                }
            }
            return new Vector2(radius, front);
        }
    }
}
