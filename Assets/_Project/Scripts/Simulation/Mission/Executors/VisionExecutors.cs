using System.Threading;
using RivetSim.Core.Logging;
using RivetSim.Simulation.Inspection;
using RivetSim.Simulation.Robot;
using RivetSim.Simulation.World;
using UnityEngine;

namespace RivetSim.Simulation.Mission.Executors
{
    /// <summary>
    /// S2 视觉执行器（vision.*）：区域标记识别、基准注册、钉心复测、每 N 颗基准复测（简化视觉：视场可见性判定 + 噪声，教学简化模型）。
    /// </summary>
    public static class VisionExecutors
    {
        /// <summary>注册本主体的执行器。</summary>
        public static void Register(ExecutorRegistry registry, MissionServices s)
        {
            registry.Register("vision.detectRegionMarker", new DelegateExecutor((cell, cursor, ct) => DetectRegionMarkerAsync(s, cursor, ct)));
            registry.Register("vision.registerDatum", new DelegateExecutor((cell, cursor, ct) => RegisterDatumAsync(s, cursor, ct)));
            registry.Register("vision.remeasureRivetCenter", new DelegateExecutor((cell, cursor, ct) => RemeasureRivetCenterAsync(s, cursor, ct)));
            registry.Register("vision.remeasureDatum", new DelegateExecutor((cell, cursor, ct) => RemeasureDatumAsync(s, cursor, ct)));
        }

        // 停靠标记由对接相机在对接过程中识别：等对接结束后报告识别帧数。
        private static async Awaitable<ExecutorResult> DetectRegionMarkerAsync(MissionServices s, MissionCursor cursor, CancellationToken ct)
        {
            InspectionStation station = s.Station(cursor);
            await s.WaitUntilAsync(() => s.Board.DockFinished, ct);
            int frames = s.Navigation.Docking.DetectionCount;
            return frames > 0
                ? ExecutorResult.Done($"停靠标记 ID {station.DockingMarker.TagId}，识别 {frames} 帧")
                : ExecutorResult.Fail($"未识别到停靠标记 ID {station.DockingMarker.TagId}");
        }

        private static async Awaitable<ExecutorResult> RegisterDatumAsync(MissionServices s, MissionCursor cursor, CancellationToken ct)
        {
            InspectionStation station = s.Station(cursor);
            ArmMotionResult r = await s.Inspection.RegisterDatumAsync(station, ct);
            if (!r.Success) return ExecutorResult.Fail(r.FailureReason);
            s.Board.JointApproachNext = true;
            return ExecutorResult.Done($"基准标记 ID {station.DatumMarker.TagId}，位置误差 {s.Inspection.Registration.DatumErrorM * 1000f:0.00} mm");
        }

        // 与 arm.alignWithVisionCorrection 衔接：机械臂到预计钉位后测量，测完通知机械臂做视觉修正。
        private static async Awaitable<ExecutorResult> RemeasureRivetCenterAsync(MissionServices s, MissionCursor cursor, CancellationToken ct)
        {
            Rivet rivet = s.Rivet(cursor);
            MissionBlackboard b = s.Board;
            await s.WaitUntilAsync(() => b.ArmAtPredicted || b.ArmAlignFailed, ct);
            if (b.ArmAlignFailed) return ExecutorResult.Skip("机械臂未到预计钉位");
            ArmMotionResult r = await s.Inspection.MeasureRivetCenterAsync(rivet, ct);
            if (!r.Success)
            {
                b.CenterMeasureFailed = true;
                return ExecutorResult.Fail(r.FailureReason);
            }
            b.CenterMeasured = true;
            Vector3 offset = Vector3.ProjectOnPlane(s.Inspection.MeasuredCenter - rivet.PositionM, rivet.Normal);
            return ExecutorResult.Done($"{rivet.RivetId} 钉心复测误差 {offset.magnitude * 1000f:0.00} mm");
        }

        private static async Awaitable<ExecutorResult> RemeasureDatumAsync(MissionServices s, MissionCursor cursor, CancellationToken ct)
        {
            InspectionStation station = s.Station(cursor);
            await s.WaitUntilAsync(() => !s.Arm.IsMoving, ct);
            ArmMotionResult r = await s.Inspection.RemeasureDatumAsync(station, ct);
            if (!r.Success) return ExecutorResult.Fail(r.FailureReason);
            s.Board.JointApproachNext = true;
            SimLog.Info(LogCategory.Arm, $"{station.StationId} 基准复测完成（工位内第 {cursor.RivetIndex + 1} 颗后）");
            return ExecutorResult.Done($"基准复测误差 {s.Inspection.Registration.DatumErrorM * 1000f:0.00} mm");
        }
    }
}
