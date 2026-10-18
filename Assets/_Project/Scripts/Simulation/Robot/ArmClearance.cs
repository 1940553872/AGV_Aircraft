using System.Collections.Generic;
using RivetSim.Core.Config;
using UnityEngine;

namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 机械臂连杆到机身外壳（Aircraft 层碰撞体）的间隙检查（M4，ADR-016）：对每个连杆的表面采样点（<see cref="ArmLinkSamples"/>）
    /// 先用 <c>Physics.CheckSphere</c>（搜索上限半径）筛选，命中的点再二分半径求到外壳的最近距离。
    /// 同时检查到本车车体（底盘、升降柱、设备盒等非机械臂渲染器的世界包围盒，基座与肩部连杆除外）的距离。
    /// UR5e 连杆要求 ≥ linkClearanceMinM；探头末端零件（排除探头前端 probeTipExclusionM）要求 ≥ endEffectorClearanceMinM。
    /// 另提供连杆自干涉（非相邻连杆）距离，供运输姿态校核。只检查不规划（v1 不做碰撞规避，Architecture §1）。
    /// </summary>
    public sealed class ArmClearance
    {
        private const int BisectionSteps = 8;
        // 自干涉检查时肘关节附近（上臂与前臂相邻处）不计入的半径，单位 m。
        private const float ElbowExclusionRadiusM = 0.10f;
        // 腕部 1～3 与探头末端视为一个紧凑整体，彼此不做自干涉检查（采样点序号见 UR5eArmGenerator）。
        private const int WristClusterFirstIndex = 4;
        // 基座与肩部连杆装在升降柱顶部法兰上，不做车体间隙检查（采样点序号 0、1）。
        private const int FirstVehicleCheckedLink = 2;

        private readonly ArmRig _rig;
        private readonly ArmConfig _config;
        private readonly int _layerMask;
        private readonly float[] _saved;
        private readonly Renderer[] _vehicleRenderers;
        private readonly List<Bounds> _vehicleBoxes = new List<Bounds>();

        /// <summary>创建检查器；layerMask 为机身外壳所在层，vehicleRoot 为本车根节点（为空时不检查车体间隙）。</summary>
        public ArmClearance(ArmRig rig, ArmConfig config, int layerMask, Transform vehicleRoot)
        {
            _rig = rig;
            _config = config;
            _layerMask = layerMask;
            _saved = new float[rig.JointCount];
            var renderers = new List<Renderer>();
            if (vehicleRoot != null)
            {
                foreach (Renderer r in vehicleRoot.GetComponentsInChildren<Renderer>(true))
                    if (!r.transform.IsChildOf(rig.transform) && r is MeshRenderer) renderers.Add(r);
            }
            _vehicleRenderers = renderers.ToArray();
        }

        /// <summary>按当前关节角检查。</summary>
        public ClearanceResult Measure()
        {
            float searchMax = _config.clearanceSearchMaxM;
            float minLink = searchMax, minEe = searchMax, minVehicleLink = searchMax, minVehicleEe = searchMax;
            string nearest = "—", nearestVehicle = "—";
            Transform tcp = _rig.Tcp;
            _vehicleBoxes.Clear();
            foreach (Renderer r in _vehicleRenderers) _vehicleBoxes.Add(r.bounds);
            IReadOnlyList<ArmLinkSamples> links = _rig.LinkSamples;
            for (int li = 0; li < links.Count; li++)
            {
                ArmLinkSamples link = links[li];
                if (link?.link == null || link.pointsLocal == null) continue;
                Matrix4x4 m = link.link.localToWorldMatrix;
                Vector3 center = m.MultiplyPoint3x4(link.boundsCenterLocal);
                bool nearAircraft = Physics.CheckSphere(center, link.boundsRadiusM + searchMax, _layerMask, QueryTriggerInteraction.Ignore);
                bool checkVehicle = li >= FirstVehicleCheckedLink && NearVehicle(center, link.boundsRadiusM + searchMax);
                if (!nearAircraft && !checkVehicle) continue;
                float linkMin = searchMax, vehicleMin = searchMax;
                foreach (Vector3 local in link.pointsLocal)
                {
                    Vector3 p = m.MultiplyPoint3x4(local);
                    if (link.isEndEffector && tcp != null && Vector3.Dot(tcp.position - p, tcp.forward) < _config.probeTipExclusionM) continue;
                    if (nearAircraft) linkMin = Mathf.Min(linkMin, PointDistance(p, linkMin));
                    if (checkVehicle) vehicleMin = Mathf.Min(vehicleMin, VehicleDistance(p));
                }
                if (link.isEndEffector)
                {
                    minEe = Mathf.Min(minEe, linkMin);
                    minVehicleEe = Mathf.Min(minVehicleEe, vehicleMin);
                }
                else
                {
                    minLink = Mathf.Min(minLink, linkMin);
                    minVehicleLink = Mathf.Min(minVehicleLink, vehicleMin);
                }
                if (linkMin <= Mathf.Min(minLink, minEe)) nearest = link.linkName;
                if (vehicleMin <= Mathf.Min(minVehicleLink, minVehicleEe)) nearestVehicle = link.linkName;
            }
            bool ok = minLink >= _config.linkClearanceMinM && minEe >= _config.endEffectorClearanceMinM &&
                      minVehicleLink >= _config.linkClearanceMinM && minVehicleEe >= _config.endEffectorClearanceMinM;
            return new ClearanceResult(minLink, minEe, nearest, Mathf.Min(minVehicleLink, minVehicleEe), nearestVehicle, ok);
        }

        /// <summary>临时设置关节角 anglesDeg 检查，检查后恢复原关节角。</summary>
        public ClearanceResult MeasureAt(float[] anglesDeg)
        {
            _rig.GetJointAnglesDeg(_saved);
            _rig.SetJointAnglesDeg(anglesDeg);
            try
            {
                return Measure();
            }
            finally
            {
                _rig.SetJointAnglesDeg(_saved);
            }
        }

        /// <summary>沿关节空间直线（from → to）均匀采样检查，返回最差的结果。</summary>
        public ClearanceResult MeasurePath(float[] fromDeg, float[] toDeg, int samples)
        {
            var q = new float[fromDeg.Length];
            ClearanceResult worst = default;
            bool first = true;
            for (int k = 1; k <= samples; k++)
            {
                float s = (float)k / samples;
                for (int i = 0; i < q.Length; i++) q[i] = fromDeg[i] + (toDeg[i] - fromDeg[i]) * s;
                ClearanceResult r = MeasureAt(q);
                if (first || (!r.Acceptable && worst.Acceptable) || (r.Acceptable == worst.Acceptable && r.MinM < worst.MinM))
                {
                    worst = r;
                    first = false;
                }
            }
            return worst;
        }

        private bool NearVehicle(Vector3 center, float radius)
        {
            float r2 = radius * radius;
            foreach (Bounds b in _vehicleBoxes)
                if (b.SqrDistance(center) <= r2) return true;
            return false;
        }

        private float VehicleDistance(Vector3 p)
        {
            float best = float.MaxValue;
            foreach (Bounds b in _vehicleBoxes) best = Mathf.Min(best, b.SqrDistance(p));
            return Mathf.Sqrt(best);
        }

        // 到外壳的最近距离：upper 以内未命中即返回 upper；否则二分。
        private float PointDistance(Vector3 p, float upper)
        {
            if (!Physics.CheckSphere(p, upper, _layerMask, QueryTriggerInteraction.Ignore)) return upper;
            float lo = 0f, hi = upper;
            for (int i = 0; i < BisectionSteps; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (Physics.CheckSphere(p, mid, _layerMask, QueryTriggerInteraction.Ignore)) hi = mid;
                else lo = mid;
            }
            return lo;
        }

        /// <summary>
        /// 连杆自干涉：非相邻连杆（序号差 ≥ 2，腕部 1～3 与末端之间除外）采样点的最近距离，以及上臂与前臂在肘关节附近以外的最近距离，单位 m。
        /// </summary>
        public static float MinSelfDistanceM(ArmRig rig, out string pair)
        {
            IReadOnlyList<ArmLinkSamples> links = rig.LinkSamples;
            var world = new List<Vector3[]>();
            foreach (ArmLinkSamples link in links)
            {
                var pts = new Vector3[link.pointsLocal.Length];
                Matrix4x4 m = link.link.localToWorldMatrix;
                for (int i = 0; i < pts.Length; i++) pts[i] = m.MultiplyPoint3x4(link.pointsLocal[i]);
                world.Add(pts);
            }
            float best = float.MaxValue;
            pair = "—";
            int elbow = 3;
            Vector3 elbowCenter = elbow - 1 < rig.JointCount ? rig.GetJoint(elbow - 1).transform.position : Vector3.zero;
            for (int a = 0; a < world.Count; a++)
            for (int b = a + 1; b < world.Count; b++)
            {
                bool adjacent = b - a < 2;
                bool wristCluster = a >= WristClusterFirstIndex && b >= WristClusterFirstIndex;
                bool upperForearm = a == 2 && b == 3;
                if (wristCluster || (adjacent && !upperForearm)) continue;
                float d = MinDistance(world[a], world[b], upperForearm ? elbowCenter : (Vector3?)null);
                if (d < best)
                {
                    best = d;
                    pair = $"{links[a].linkName} / {links[b].linkName}";
                }
            }
            return best;
        }

        private static float MinDistance(Vector3[] a, Vector3[] b, Vector3? exclusionCenter)
        {
            float best = float.MaxValue;
            float ex2 = ElbowExclusionRadiusM * ElbowExclusionRadiusM;
            foreach (Vector3 p in a)
            {
                if (exclusionCenter.HasValue && (p - exclusionCenter.Value).sqrMagnitude < ex2) continue;
                foreach (Vector3 q in b)
                {
                    if (exclusionCenter.HasValue && (q - exclusionCenter.Value).sqrMagnitude < ex2) continue;
                    float d = (p - q).sqrMagnitude;
                    if (d < best) best = d;
                }
            }
            return Mathf.Sqrt(best);
        }
    }
}
