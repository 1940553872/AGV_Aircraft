using System.Collections.Generic;
using System.IO;
using System.Text;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Editor.Generators;
using RivetSim.Simulation.Robot;
using RivetSim.Simulation.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RivetSim.Editor.Tools
{
    /// <summary>
    /// 工位可达性验证（M2 第三部分 3B，采样验证；正式逆解 ArmIkSolver 在 M4）：在每个工位的停靠位姿临时放一台 PF_AGV，
    /// 升降柱伸出量按步长扫描，对每颗铆钉用阻尼最小二乘数值逆解求“探头端面压在铆钉上、工具轴沿 −法向”的关节角；
    /// 同时用 Physics.CheckBox 检查车体与升降柱到机身外壳的三维间隙（用户确认按三维间隙判定）。
    /// 结果写入 Docs/Reference/M2_Reachability.md。机械臂连杆与机身的碰撞不检查（M4）。
    /// </summary>
    public static class ReachabilityChecker
    {
        private const string ReportPath = "Docs/Reference/M2_Reachability.md";
        private const float ExtensionStepM = 0.05f;
        private const int MaxIterations = 150;
        private const float PositionToleranceM = 0.001f;
        private const float AngleToleranceDeg = 1f;
        private const float JacobianStepDeg = 0.05f;
        private const float DampingLambda = 0.05f;
        private const float MaxStepDeg = 8f;
        // 姿态误差（弧度）折算为长度的权重，使位置与姿态误差量级相当。
        private const float OrientationWeightM = 0.1f;
        private const float ClearanceSearchMaxM = 1.0f;
        private const float ClearanceSearchResolutionM = 0.005f;

        // 逆解初值（°）：先用上一颗的解（热启动），失败时依次尝试这些姿态。
        private static readonly float[][] Seeds =
        {
            new[] { 0f, -90f, 90f, -90f, -90f, 0f },
            new[] { 90f, -90f, 90f, -90f, -90f, 0f },
            new[] { -90f, -90f, 90f, -90f, -90f, 0f },
            new[] { 0f, -60f, -90f, -120f, 90f, 0f },
            new[] { 180f, -90f, 90f, -90f, -90f, 0f },
        };

        private readonly struct StationResult
        {
            public readonly string StationId;
            public readonly float MinExtensionM;
            public readonly float MaxExtensionM;
            public readonly int ReachableAtPlan;
            public readonly float PlanExtensionM;
            public readonly int RivetCount;
            public readonly float ChassisClearanceM;
            public readonly float ColumnClearanceAtPlanM;

            public StationResult(string id, float min, float max, int atPlan, float plan, int count, float chassis, float column)
            {
                StationId = id;
                MinExtensionM = min;
                MaxExtensionM = max;
                ReachableAtPlan = atPlan;
                PlanExtensionM = plan;
                RivetCount = count;
                ChassisClearanceM = chassis;
                ColumnClearanceAtPlanM = column;
            }
        }

        /// <summary>对主场景全部工位执行可达性验证（需打开主场景并已生成检测区）。</summary>
        [MenuItem("RivetSim/Debug/Check Station Reachability")]
        public static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform work = GeneratorUtility.FindRoot(scene, "Zone_Work");
            RivetRegistry registry = work != null ? work.Find(RivetZoneGenerator.RootName)?.GetComponent<RivetRegistry>() : null;
            var agvConfig = GeneratorUtility.LoadConfig<AgvConfig>(ProjectPaths.AgvAsset);
            var armConfig = GeneratorUtility.LoadConfig<ArmConfig>(ProjectPaths.ArmAsset);
            GameObject agvPrefab = GeneratorUtility.LoadPrefab(AgvGenerator.AgvPrefabPath);
            if (registry == null || agvConfig == null || armConfig == null || agvPrefab == null)
            {
                SimLog.Error(LogCategory.System, "可达性验证失败：请先打开主场景并生成检测区。");
                return;
            }

            var results = new List<StationResult>();
            var agv = (GameObject)Object.Instantiate(agvPrefab);
            agv.hideFlags = HideFlags.DontSave;
            try
            {
                LiftColumnController lift = agv.GetComponent<LiftColumnController>();
                ArmRig rig = agv.GetComponentInChildren<ArmRig>();
                Transform tcp = FindDeep(agv.transform, EndEffectorGenerator.TcpName);
                foreach (InspectionRegion region in registry.Regions)
                {
                    foreach (InspectionStation station in region.Stations)
                        results.Add(CheckStation(station, agv, lift, rig, tcp, agvConfig, armConfig));
                }
            }
            finally
            {
                Object.DestroyImmediate(agv);
            }
            WriteReport(results, agvConfig);
        }

        /// <summary>
        /// 把 AGV 实例放到工位停靠位姿，升降柱取 LiftHeightPlanner 的伸出量，并把探头逆解到指定铆钉（供截图使用）；逆解失败时返回 false。
        /// </summary>
        public static bool PoseAtRivet(GameObject agv, InspectionStation station, Rivet rivet, AgvConfig agvConfig, ArmConfig armConfig)
        {
            agv.transform.SetPositionAndRotation(station.DockingPose.WorldPosition, station.DockingPose.WorldRotation);
            LiftHeightPlan plan = LiftHeightPlanner.Plan(station.GetRivetHeightsM(), agvConfig, armConfig);
            agv.GetComponent<LiftColumnController>().SetExtensionImmediate(plan.ExtensionM);
            float[] warm = Seeds[0];
            return Solve(agv.GetComponentInChildren<ArmRig>(), FindDeep(agv.transform, EndEffectorGenerator.TcpName),
                rivet.PositionM, -rivet.Normal, ref warm);
        }

        private static StationResult CheckStation(InspectionStation station, GameObject agv, LiftColumnController lift, ArmRig rig,
            Transform tcp, AgvConfig agvConfig, ArmConfig armConfig)
        {
            agv.transform.SetPositionAndRotation(station.DockingPose.WorldPosition, station.DockingPose.WorldRotation);
            LiftHeightPlan plan = LiftHeightPlanner.Plan(station.GetRivetHeightsM(), agvConfig, armConfig);
            float min = float.NaN, max = float.NaN;
            int atPlan = 0;
            float[] warm = Seeds[0];
            int steps = Mathf.RoundToInt(agvConfig.liftStrokeM / ExtensionStepM);
            for (int k = 0; k <= steps + 1; k++)
            {
                // 最后一轮单独检查 LiftHeightPlanner 给出的伸出量。
                bool planRound = k == steps + 1;
                float extension = planRound ? plan.ExtensionM : k * ExtensionStepM;
                lift.SetExtensionImmediate(extension);
                int reachable = 0;
                foreach (Rivet rivet in station.Rivets)
                {
                    if (Solve(rig, tcp, rivet.PositionM, -rivet.Normal, ref warm)) reachable++;
                }
                if (planRound)
                {
                    atPlan = reachable;
                    break;
                }
                if (reachable != station.Rivets.Count) continue;
                if (float.IsNaN(min)) min = extension;
                max = extension;
            }

            lift.SetExtensionImmediate(plan.ExtensionM);
            Physics.SyncTransforms();
            float chassis = Clearance(ChassisBox(agv.transform, agvConfig));
            float column = Clearance(ColumnBox(agv.transform, agvConfig, plan.ExtensionM));
            return new StationResult(station.StationId, min, max, atPlan, plan.ExtensionM, station.Rivets.Count, chassis, column);
        }

        // 阻尼最小二乘：误差 = [位置差; 姿态差（工具轴叉积）× 权重]，数值雅可比。
        private static bool Solve(ArmRig rig, Transform tcp, Vector3 target, Vector3 toolAxis, ref float[] warm)
        {
            var tried = new List<float[]> { warm };
            tried.AddRange(Seeds);
            foreach (float[] seed in tried)
            {
                float[] q = (float[])seed.Clone();
                rig.SetJointAnglesDeg(q);
                for (int it = 0; it < MaxIterations; it++)
                {
                    float[] e = Error(tcp, target, toolAxis);
                    if (Converged(tcp, target, toolAxis))
                    {
                        warm = rig.GetJointAnglesDeg();
                        return true;
                    }
                    float[,] j = Jacobian(rig, tcp, target, toolAxis, q, e);
                    float[] dq = DampedStep(j, e);
                    for (int i = 0; i < q.Length; i++) q[i] += Mathf.Clamp(dq[i] * Mathf.Rad2Deg, -MaxStepDeg, MaxStepDeg);
                    rig.SetJointAnglesDeg(q);
                    q = rig.GetJointAnglesDeg();
                }
                if (Converged(tcp, target, toolAxis))
                {
                    warm = rig.GetJointAnglesDeg();
                    return true;
                }
            }
            return false;
        }

        private static bool Converged(Transform tcp, Vector3 target, Vector3 toolAxis) =>
            Vector3.Distance(tcp.position, target) < PositionToleranceM && Vector3.Angle(tcp.forward, toolAxis) < AngleToleranceDeg;

        private static float[] Error(Transform tcp, Vector3 target, Vector3 toolAxis)
        {
            Vector3 p = target - tcp.position;
            Vector3 r = Vector3.Cross(tcp.forward, toolAxis.normalized) * OrientationWeightM;
            return new[] { p.x, p.y, p.z, r.x, r.y, r.z };
        }

        private static float[,] Jacobian(ArmRig rig, Transform tcp, Vector3 target, Vector3 toolAxis, float[] q, float[] e0)
        {
            var j = new float[6, q.Length];
            float stepRad = JacobianStepDeg * Mathf.Deg2Rad;
            for (int c = 0; c < q.Length; c++)
            {
                float[] qp = (float[])q.Clone();
                qp[c] += JacobianStepDeg;
                rig.SetJointAnglesDeg(qp);
                float[] e1 = Error(tcp, target, toolAxis);
                // 误差对关节角的导数取负即末端对关节角的雅可比。
                for (int r = 0; r < 6; r++) j[r, c] = -(e1[r] - e0[r]) / stepRad;
            }
            rig.SetJointAnglesDeg(q);
            return j;
        }

        // Δq = Jᵀ (J Jᵀ + λ² I)⁻¹ e
        private static float[] DampedStep(float[,] j, float[] e)
        {
            int n = j.GetLength(1);
            var a = new double[6, 7];
            for (int r = 0; r < 6; r++)
            {
                for (int c = 0; c < 6; c++)
                {
                    double sum = 0;
                    for (int k = 0; k < n; k++) sum += j[r, k] * j[c, k];
                    a[r, c] = sum + (r == c ? DampingLambda * DampingLambda : 0);
                }
                a[r, 6] = e[r];
            }
            double[] y = SolveLinear(a);
            var dq = new float[n];
            for (int k = 0; k < n; k++)
            {
                double sum = 0;
                for (int r = 0; r < 6; r++) sum += j[r, k] * y[r];
                dq[k] = (float)sum;
            }
            return dq;
        }

        private static double[] SolveLinear(double[,] a)
        {
            const int n = 6;
            for (int col = 0; col < n; col++)
            {
                int pivot = col;
                for (int r = col + 1; r < n; r++)
                {
                    if (System.Math.Abs(a[r, col]) > System.Math.Abs(a[pivot, col])) pivot = r;
                }
                for (int c = 0; c <= n; c++) (a[col, c], a[pivot, c]) = (a[pivot, c], a[col, c]);
                for (int r = 0; r < n; r++)
                {
                    if (r == col) continue;
                    double f = a[r, col] / a[col, col];
                    for (int c = col; c <= n; c++) a[r, c] -= f * a[col, c];
                }
            }
            var x = new double[n];
            for (int i = 0; i < n; i++) x[i] = a[i, n] / a[i, i];
            return x;
        }

        private static (Vector3 Center, Vector3 HalfExtents, Quaternion Rotation) ChassisBox(Transform agv, AgvConfig cfg)
        {
            Vector3 size = SizeAxes.LengthAlongZ(cfg.bodySizeM);
            return (agv.TransformPoint(new Vector3(0f, size.y * 0.5f, 0f)), size * 0.5f, agv.rotation);
        }

        private static (Vector3 Center, Vector3 HalfExtents, Quaternion Rotation) ColumnBox(Transform agv, AgvConfig cfg, float extensionM)
        {
            float bottom = cfg.liftMount.positionM.y;
            float top = cfg.ArmMountHeightM(extensionM);
            Vector3 local = new Vector3(cfg.liftMount.positionM.x, (bottom + top) * 0.5f, cfg.liftMount.positionM.z);
            var half = new Vector3(cfg.liftSectionSizeM.x * 0.5f, (top - bottom) * 0.5f, cfg.liftSectionSizeM.y * 0.5f);
            return (agv.TransformPoint(local), half, agv.rotation);
        }

        // 三维间隙：盒子向外膨胀到刚好碰到机身外壳（Aircraft 层）时的膨胀量（二分查找）。
        private static float Clearance((Vector3 Center, Vector3 HalfExtents, Quaternion Rotation) box)
        {
            int mask = 1 << SimLayers.AircraftIndex;
            if (Physics.CheckBox(box.Center, box.HalfExtents, box.Rotation, mask)) return 0f;
            float lo = 0f, hi = ClearanceSearchMaxM;
            if (!Physics.CheckBox(box.Center, box.HalfExtents + Vector3.one * hi, box.Rotation, mask)) return hi;
            while (hi - lo > ClearanceSearchResolutionM)
            {
                float mid = (lo + hi) * 0.5f;
                if (Physics.CheckBox(box.Center, box.HalfExtents + Vector3.one * mid, box.Rotation, mask)) hi = mid;
                else lo = mid;
            }
            return lo;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            return null;
        }

        private static void WriteReport(List<StationResult> results, AgvConfig agv)
        {
            var sb = new StringBuilder();
            sb.Append("# M2 工位可达性验证（自动生成，RivetSim/Debug/Check Station Reachability）\n\n");
            sb.Append($"方法：阻尼最小二乘数值逆解（采样验证，正式逆解在 M4）；目标为探头端面压在铆钉上、工具轴沿 −法向，位置 < {PositionToleranceM * 1000f:0} mm、角度 < {AngleToleranceDeg:0}°；");
            sb.Append($"升降柱伸出量按 {ExtensionStepM:0.00} m 步长扫描 0～{agv.liftStrokeM:0.00} m。间隙为车体 / 升降柱外形到机身外壳的三维最近距离（要求 ≥ {agv.fuselageSafetyClearanceM:0.00} m）。机械臂连杆与机身的碰撞未检查。\n\n");
            sb.Append("| 工位 | 铆钉数 | 12 颗全可达的伸出量范围 | LiftHeightPlanner 伸出量 | 该伸出量下可达 | 车体间隙 | 升降柱间隙 |\n|---|---|---|---|---|---|---|\n");
            foreach (StationResult r in results)
            {
                string range = float.IsNaN(r.MinExtensionM) ? "无" : $"{r.MinExtensionM:0.00}～{r.MaxExtensionM:0.00} m（安装面 {agv.ArmMountHeightM(r.MinExtensionM):0.00}～{agv.ArmMountHeightM(r.MaxExtensionM):0.00} m）";
                sb.Append($"| {r.StationId} | {r.RivetCount} | {range} | {r.PlanExtensionM:0.000} m | {r.ReachableAtPlan}/{r.RivetCount} | {r.ChassisClearanceM:0.000} m | {r.ColumnClearanceAtPlanM:0.000} m |\n");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? ".");
            File.WriteAllText(ReportPath, sb.ToString(), new UTF8Encoding(false));
            SimLog.Info(LogCategory.System, $"可达性验证完成，结果写入 {ReportPath}\n{sb}");
        }
    }
}
