using System.Threading;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Simulation.Inspection;
using RivetSim.Simulation.World;
using UnityEngine;

namespace RivetSim.Simulation.Mission.Executors
{
    /// <summary>
    /// S8 数据执行器（data.*）：操作员初值（t(1,8)，M5 由模拟操作员给出）、记录轨迹、保存注册 / 图像位姿 / 超声结果、提交记录。
    /// </summary>
    public static class DataExecutors
    {
        /// <summary>注册本主体的执行器。</summary>
        public static void Register(ExecutorRegistry registry, MissionServices s)
        {
            registry.Register("data.manualInitialPose", new DelegateExecutor((cell, cursor, ct) => ManualInitialPoseAsync(s, ct)));
            s.RegisterLogic(registry, "data.recordTrajectory", a =>
                ExecutorResult.Done($"里程 {s.Chassis.OdometerM:0.0} m；对接误差 {s.Board.LastDock.Error}"));
            s.RegisterLogic(registry, "data.saveRegistration", a =>
            {
                InspectionStation station = s.Station(a.Cursor);
                StationRegistration reg = s.Inspection.Registration;
                return reg != null ? ExecutorResult.Done($"{station.StationId} 注册结果已保存（误差 {reg.DatumErrorM * 1000f:0.00} mm）") : ExecutorResult.Fail("没有注册结果");
            });
            s.RegisterLogic(registry, "data.saveImageAndPose", a => ExecutorResult.Done($"{s.Rivet(a.Cursor).RivetId} 钉心图像与位姿已保存"));
            s.RegisterLogic(registry, "data.saveUtResult", a => SaveUtResult(s, a.Cursor));
            s.RegisterLogic(registry, "data.submitRecord", a =>
            {
                Rivet rivet = s.Rivet(a.Cursor);
                InspectionRecord r = s.Board.CommittedRecord;
                if (r == null) return ExecutorResult.Skip("本颗没有记录");
                r.CycleTimeS = (float)(s.TimeS() - s.Board.RivetStartS);
                return ExecutorResult.Done($"{rivet.RivetId} 记录已提交（{RivetInspectionService.Label(r.Verdict)}，单钉节拍 {r.CycleTimeS:0.0} s）");
            });
        }

        // 模拟操作员（M6 改为小地图点选）：等待后给出真值位姿 + 小噪声。
        private static async Awaitable<ExecutorResult> ManualInitialPoseAsync(MissionServices s, CancellationToken ct)
        {
            SimLog.Info(LogCategory.Mission, $"[模拟操作员] 请在小地图上点选 AGV 初值……（{s.Settings.simulatedOperatorDelayS:0} s 后自动给出）");
            await s.WaitSimAsync(s.Settings.simulatedOperatorDelayS, ct);
            PlanarPose truth = s.Chassis.Pose;
            var noise = new Vector2(Gaussian(s.Random), Gaussian(s.Random)) * s.Settings.simulatedOperatorPositionNoiseM;
            var pose = new PlanarPose(truth.positionM + noise, truth.yawDeg + Gaussian(s.Random) * s.Settings.simulatedOperatorYawNoiseDeg);
            s.Board.InitialPose = pose;
            s.Board.InitialPoseProvided = true;
            string text = $"初值 ({pose.positionM.x:0.00}, {pose.positionM.y:0.00}) m、{pose.yawDeg:0.0}°，与真值偏差 {noise.magnitude * 1000f:0} mm / {Mathf.DeltaAngle(truth.yawDeg, pose.yawDeg):0.0}°";
            SimLog.Info(LogCategory.Mission, $"[模拟操作员] {text}");
            return ExecutorResult.Done(text);
        }

        private static ExecutorResult SaveUtResult(MissionServices s, MissionCursor cursor)
        {
            Rivet rivet = s.Rivet(cursor);
            MissionBlackboard b = s.Board;
            if (b.InvalidRetryPending) return ExecutorResult.Skip("采集无效，等待重贴后重采");
            InspectionRecord record = b.PendingRecord;
            if (record == null) return ExecutorResult.Fail("没有待保存的超声结果");
            s.Inspection.CommitRecord(rivet, record);
            b.RecordSaved = true;
            b.CommittedRecord = record;
            SimLog.Info(LogCategory.UT, $"{rivet.RivetId}：{RivetInspectionService.Label(record.Verdict)}（{record.Reason}）；接触力 {record.ContactForceN:0.00} N、" +
                                        $"耦合 {record.Coupling:0.00}、对中误差 {record.CenteringErrorM * 1000f:0.00} mm（真值 {rivet.Defect}）");
            return ExecutorResult.Done($"{rivet.RivetId} 超声结果已保存（{RivetInspectionService.Label(record.Verdict)}）");
        }

        // Box-Muller 标准正态。
        private static float Gaussian(System.Random random)
        {
            double u1 = 1.0 - random.NextDouble();
            double u2 = random.NextDouble();
            return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2));
        }
    }
}
