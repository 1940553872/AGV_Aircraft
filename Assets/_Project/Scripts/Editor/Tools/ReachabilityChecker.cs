using System.Collections.Generic;
using System.IO;
using System.Text;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Editor.Generators;
using RivetSim.Simulation.Inspection;
using RivetSim.Simulation.Robot;
using RivetSim.Simulation.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RivetSim.Editor.Tools
{
    /// <summary>
    /// 工位可达性与连杆间隙验证（M2 第三部分 3B 建立；M4 改用运行时逆解 <see cref="ArmIkSolver"/> 并增加连杆间隙检查，ADR-016）：
    /// 在每个工位的停靠位姿临时放一台 PF_AGV，升降柱取 LiftHeightPlanner 的伸出量，按运行时检测流程的几何依次检查——
    /// 运输姿态、Stow → Observe 关节路径、基准观察位姿（含腕部相机能否看到基准标记）、Observe → 基准观察路径，
    /// 以及每颗铆钉的对中位姿（含钉心可见性）、接触位姿与首颗铆钉的关节进近路径；记录逆解误差与连杆 / 末端到机身外壳的最小间隙。
    /// 另以 500 个随机关节构型（种子取 CFG_SimSettings）测试逆解收敛率与精度（Architecture §8）。结果写入 Docs/Reference/M4_ArmReach.md。
    /// </summary>
    public static class ReachabilityChecker
    {
        private const string ReportPath = "Docs/Reference/M4_ArmReach.md";
        private const int RandomPoseCount = 500;
        // 随机关节构型的取值范围（°）；肘关节限制在 ±160° 以避开前臂与上臂重合的退化构型。
        private const float RandomJointRangeDeg = 180f;
        private const float RandomElbowRangeDeg = 160f;
        private const int ElbowJointIndex = 2;
        private const float ClearanceSearchMaxM = 1.0f;
        private const float ClearanceSearchResolutionM = 0.005f;
        private static readonly float[] RollCandidatesDeg = { 0f, 90f, -90f, 180f };

        private sealed class StationReport
        {
            public string StationId;
            public float ExtensionM;
            public int Rivets;
            public int ReachedPreContact;
            public int ReachedContact;
            public int RivetVisible;
            public bool DatumReached;
            public bool DatumVisible;
            public float MaxPositionErrorM;
            public float MaxAngleErrorDeg;
            public float MinLinkM = float.MaxValue;
            public float MinEndEffectorM = float.MaxValue;
            public string NearestLink = "—";
            public string WorstStep = "—";
            public float StowLinkM;
            public float ChassisClearanceM;
            public float ColumnClearanceM;
            public readonly List<string> Failures = new List<string>();

            public void Note(in ClearanceResult r, string step)
            {
                if (r.MinM < Mathf.Min(MinLinkM, MinEndEffectorM))
                {
                    NearestLink = r.NearestLink;
                    WorstStep = step;
                }
                MinLinkM = Mathf.Min(MinLinkM, r.MinLinkM);
                MinEndEffectorM = Mathf.Min(MinEndEffectorM, r.MinEndEffectorM);
                if (!r.Acceptable) Failures.Add($"{step}：{r}");
            }

            public void Note(in IkResult r)
            {
                if (!r.Converged) return;
                MaxPositionErrorM = Mathf.Max(MaxPositionErrorM, r.PositionErrorM);
                MaxAngleErrorDeg = Mathf.Max(MaxAngleErrorDeg, r.AngleErrorDeg);
            }
        }

        /// <summary>对主场景全部工位执行验证（需打开主场景并已生成检测区）。</summary>
        [MenuItem("RivetSim/Debug/Check Station Reachability")]
        public static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform work = GeneratorUtility.FindRoot(scene, "Zone_Work");
            RivetRegistry registry = work != null ? work.Find(RivetZoneGenerator.RootName)?.GetComponent<RivetRegistry>() : null;
            var agvConfig = GeneratorUtility.LoadConfig<AgvConfig>(ProjectPaths.AgvAsset);
            var armConfig = GeneratorUtility.LoadConfig<ArmConfig>(ProjectPaths.ArmAsset);
            var sim = GeneratorUtility.LoadConfig<SimSettings>(ProjectPaths.SimSettingsAsset);
            GameObject agvPrefab = GeneratorUtility.LoadPrefab(AgvGenerator.AgvPrefabPath);
            if (registry == null || agvConfig == null || armConfig == null || sim == null || agvPrefab == null)
            {
                SimLog.Error(LogCategory.System, "可达性验证失败：请先打开主场景并生成检测区。");
                return;
            }

            var reports = new List<StationReport>();
            IkStatistics random;
            var agv = (GameObject)Object.Instantiate(agvPrefab);
            agv.hideFlags = HideFlags.DontSave;
            try
            {
                ArmController arm = agv.GetComponentInChildren<ArmController>();
                arm.InitializeKinematics(sim.randomSeed, 1 << SimLayers.AircraftIndex);
                var vision = new WristVision(armConfig, new System.Random(sim.randomSeed));
                foreach (InspectionRegion region in registry.Regions)
                foreach (InspectionStation station in region.Stations)
                    reports.Add(CheckStation(station, agv, arm, vision, agvConfig, armConfig));
                random = RandomPoseTest(arm, sim.randomSeed);
            }
            finally
            {
                Object.DestroyImmediate(agv);
            }
            WriteReport(reports, random, agvConfig, armConfig);
        }

        /// <summary>
        /// 把 AGV 实例放到工位停靠位姿，升降柱取 LiftHeightPlanner 的伸出量，并把探头逆解到指定铆钉（端面压在铆钉上，供截图使用）；逆解失败时返回 false。
        /// </summary>
        public static bool PoseAtRivet(GameObject agv, InspectionStation station, Rivet rivet, AgvConfig agvConfig, ArmConfig armConfig)
        {
            agv.transform.SetPositionAndRotation(station.DockingPose.WorldPosition, station.DockingPose.WorldRotation);
            LiftHeightPlan plan = LiftHeightPlanner.Plan(station.GetRivetHeightsM(), agvConfig, armConfig);
            agv.GetComponent<LiftColumnController>().SetExtensionImmediate(plan.ExtensionM);
            ArmController arm = agv.GetComponentInChildren<ArmController>();
            arm.InitializeKinematics(0, 0);
            var q = new float[arm.Rig.JointCount];
            foreach (float roll in RollCandidatesDeg)
            {
                if (!arm.SolveWorld(RivetInspectionService.ToolPoseFacing(rivet.PositionM, rivet.Normal, 0f, roll), q, false).Converged) continue;
                arm.SetJointsImmediate(q);
                return true;
            }
            return false;
        }

        private static StationReport CheckStation(InspectionStation station, GameObject agv, ArmController arm, WristVision vision,
            AgvConfig agvConfig, ArmConfig armConfig)
        {
            var report = new StationReport { StationId = station.StationId, Rivets = station.Rivets.Count };
            agv.transform.SetPositionAndRotation(station.DockingPose.WorldPosition, station.DockingPose.WorldRotation);
            LiftHeightPlan plan = LiftHeightPlanner.Plan(station.GetRivetHeightsM(), agvConfig, armConfig);
            agv.GetComponent<LiftColumnController>().SetExtensionImmediate(plan.ExtensionM);
            report.ExtensionM = plan.ExtensionM;
            float[] stow = arm.NamedPose(ArmConfig.StowPoseName);
            arm.SetJointsImmediate(stow);
            Physics.SyncTransforms();
            report.ChassisClearanceM = BoxClearance(ChassisBox(agv.transform, agvConfig));
            report.ColumnClearanceM = BoxClearance(ColumnBox(agv.transform, agvConfig, plan.ExtensionM));
            ArmClearance clearance = arm.Clearance;
            ClearanceResult stowClear = clearance.Measure();
            report.StowLinkM = stowClear.MinM;
            report.Note(stowClear, "运输姿态");

            bool mirrored = RivetInspectionService.IsLeftSide(arm, station);
            float[] ready = arm.NamedPose(ArmConfig.ReadyPoseName);
            float[] observe = arm.NamedPose(ArmConfig.ObservePoseName, mirrored);
            report.Note(clearance.MeasurePath(stow, ready, armConfig.clearancePathSamples), "Stow → Ready 路径");
            report.Note(clearance.MeasurePath(ready, observe, armConfig.clearancePathSamples), "Ready → Observe 路径");
            arm.SetJointsImmediate(observe);

            var q = new float[arm.Rig.JointCount];
            foreach (float roll in RollCandidatesDeg)
            {
                Pose tcp = RivetInspectionService.TcpForCamera(arm.Rig, RivetInspectionService.DatumViewCameraPose(station.DatumMarker, armConfig.observeStandoffM, roll));
                IkResult ik = arm.SolveWorld(tcp, q, true);
                if (!ik.Converged) continue;
                report.DatumReached = true;
                report.Note(ik);
                report.Note(clearance.MeasurePath(observe, q, armConfig.clearancePathSamples), "Observe ↔ 基准观察路径");
                arm.SetJointsImmediate(q);
                report.DatumVisible = vision.CanSee(arm.Rig.WristCamera, station.DatumMarker.TagCenterWorld, station.DatumMarker.NormalWorld);
                break;
            }
            if (!report.DatumReached) report.Failures.Add($"基准观察位姿逆解失败（{arm.LastClearance}）");
            float[] datumView = arm.CurrentJointsDeg();

            float rollDeg = 0f;
            bool first = true;
            float approachM = Mathf.Max(0f, armConfig.nearFieldDistanceM - armConfig.preContactStandoffM);
            foreach (Rivet rivet in station.Rivets)
            {
                bool solved = false;
                foreach (float roll in first ? RollCandidatesDeg : new[] { rollDeg })
                {
                    if (first)
                    {
                        // 首颗：关节插值到对中位姿外 approachM 处，再直线进入。
                        Pose far = RivetInspectionService.ToolPoseFacing(rivet.PositionM, rivet.Normal, armConfig.preContactStandoffM + approachM, roll);
                        if (!arm.SolveWorld(far, q, true).Converged) continue;
                        report.Note(clearance.MeasurePath(datumView, q, armConfig.clearancePathSamples), $"基准观察 → {rivet.RivetId} 进近路径");
                        arm.SetJointsImmediate(q);
                    }
                    IkResult ik = arm.SolveWorld(RivetInspectionService.ToolPoseFacing(rivet.PositionM, rivet.Normal, armConfig.preContactStandoffM, roll), q, true);
                    if (!ik.Converged) continue;
                    rollDeg = roll;
                    solved = true;
                    report.Note(ik);
                    report.Note(arm.LastClearance, $"{rivet.RivetId} 对中位姿");
                    arm.SetJointsImmediate(q);
                    break;
                }
                first = false;
                if (!solved)
                {
                    report.Failures.Add($"{rivet.RivetId} 对中位姿逆解失败（{arm.LastClearance}）");
                    continue;
                }
                report.ReachedPreContact++;
                if (vision.CanSee(arm.Rig.WristCamera, rivet.PositionM, rivet.Normal)) report.RivetVisible++;
                else report.Failures.Add($"{rivet.RivetId} 对中位姿时腕部相机看不到钉心");
                float[] pre = arm.CurrentJointsDeg();
                IkResult contact = arm.SolveWorld(RivetInspectionService.ToolPoseFacing(rivet.PositionM, rivet.Normal, 0f, rollDeg), q, true);
                if (contact.Converged)
                {
                    report.ReachedContact++;
                    report.Note(contact);
                    report.Note(arm.LastClearance, $"{rivet.RivetId} 接触位姿");
                }
                else report.Failures.Add($"{rivet.RivetId} 接触位姿逆解失败（{arm.LastClearance}）");
                arm.SetJointsImmediate(pre);
            }
            // 收臂：末颗对中位姿 → Observe（与展开路径相反）。
            report.Note(clearance.MeasurePath(arm.CurrentJointsDeg(), observe, armConfig.clearancePathSamples), "末颗 → Observe 收臂路径");
            return report;
        }

        // 随机关节构型 → 正解得到目标 → 从 Observe 初值逆解（不检查间隙），统计收敛率与误差。
        private static IkStatistics RandomPoseTest(ArmController arm, int seed)
        {
            var rng = new System.Random(seed);
            var stats = new IkStatistics();
            var q = new float[arm.Rig.JointCount];
            var solution = new float[q.Length];
            var seeds = new List<float[]> { arm.NamedPose(ArmConfig.ObservePoseName) };
            for (int k = 0; k < RandomPoseCount; k++)
            {
                for (int i = 0; i < q.Length; i++)
                {
                    float range = i == ElbowJointIndex ? RandomElbowRangeDeg : RandomJointRangeDeg;
                    q[i] = (float)((rng.NextDouble() * 2.0 - 1.0) * range);
                }
                Pose target = arm.Kinematics.Forward(q);
                stats.Record(arm.Ik.Solve(target, seeds, null, solution));
            }
            return stats;
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
        private static float BoxClearance((Vector3 Center, Vector3 HalfExtents, Quaternion Rotation) box)
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

        private static void WriteReport(List<StationReport> reports, IkStatistics random, AgvConfig agv, ArmConfig arm)
        {
            var sb = new StringBuilder();
            sb.Append("# M4 工位可达性与连杆间隙验证（自动生成，RivetSim/Debug/Check Station Reachability）\n\n");
            sb.Append("方法：运行时逆解 ArmIkSolver（阻尼最小二乘，位置 < ").Append($"{arm.ikPositionToleranceM * 1000f:0.0} mm、姿态 < {arm.ikOrientationToleranceDeg:0.0}°）")
              .Append("，工具轴沿 −法向、腕部相机优先朝上（失败时绕工具轴转 90° / −90° / 180°）；升降柱取 LiftHeightPlanner 伸出量。")
              .Append($"间隙为连杆表面采样点到机身外壳（Aircraft 层碰撞体）的最近距离：UR5e 连杆要求 ≥ {arm.linkClearanceMinM * 1000f:0} mm，")
              .Append($"探头末端零件（不计探头前端 {arm.probeTipExclusionM * 1000f:0} mm）要求 ≥ {arm.endEffectorClearanceMinM * 1000f:0} mm；关节插值路径按 {arm.clearancePathSamples} 点采样。")
              .Append($"对中位姿为探头端面距蒙皮 {arm.preContactStandoffM * 1000f:0} mm，接触位姿为端面在蒙皮上。\n\n");
            sb.Append("| 工位 | 伸出量 | 对中可达 | 接触可达 | 钉心可见 | 基准观察 | 最大位置误差 | 最大姿态误差 | 连杆最小间隙 | 末端最小间隙 | 最近处 | 运输姿态间隙 | 车体 / 升降柱间隙 |\n");
            sb.Append("|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
            foreach (StationReport r in reports)
            {
                sb.Append($"| {r.StationId} | {r.ExtensionM:0.000} m | {r.ReachedPreContact}/{r.Rivets} | {r.ReachedContact}/{r.Rivets} | {r.RivetVisible}/{r.Rivets} | ")
                  .Append($"{(r.DatumReached ? "可达" : "不可达")}，{(r.DatumVisible ? "可见" : "不可见")} | {r.MaxPositionErrorM * 1000f:0.000} mm | {r.MaxAngleErrorDeg:0.000}° | ")
                  .Append($"{r.MinLinkM * 1000f:0} mm | {r.MinEndEffectorM * 1000f:0} mm | {r.NearestLink}（{r.WorstStep}） | {r.StowLinkM * 1000f:0} mm | ")
                  .Append($"{r.ChassisClearanceM:0.000} / {r.ColumnClearanceM:0.000} m |\n");
            }
            sb.Append("\n间隙达到搜索上限（").Append($"{arm.clearanceSearchMaxM * 1000f:0} mm）时按上限记。\n\n");
            sb.Append($"## 随机位姿逆解测试\n\n{RandomPoseCount} 个随机关节构型（肘关节 ±{RandomElbowRangeDeg:0}°，其余 ±{RandomJointRangeDeg:0}°）经正解得到目标，自 Observe 初值逆解（不检查间隙）：{random}\n\n");
            sb.Append("## 问题\n\n");
            bool any = false;
            foreach (StationReport r in reports)
            foreach (string f in r.Failures)
            {
                sb.Append($"- {r.StationId}：{f}\n");
                any = true;
            }
            if (!any) sb.Append("无。\n");
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? ".");
            File.WriteAllText(ReportPath, sb.ToString(), new UTF8Encoding(false));
            SimLog.Write(LogCategory.System, any ? SimLogLevel.Warning : SimLogLevel.Info, $"可达性与连杆间隙验证完成，结果写入 {ReportPath}\n{sb}");
        }
    }
}
