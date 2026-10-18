using System.Text;
using System.Threading;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Presentation.UI;
using RivetSim.Presentation.Visualization;
using RivetSim.Simulation.Inspection;
using RivetSim.Simulation.Robot;
using RivetSim.Simulation.World;
using UnityEngine;

namespace RivetSim.Presentation.App
{
    /// <summary>
    /// M4 单工位检测流程（沙盒与主场景共用）：零速锁定 → 升降柱升到 LiftHeightPlanner 高度 → T3 基准注册 → 逐钉 T4 对中 / T5 贴合采集 / T6 回退 →
    /// 收臂 → 升降柱降到最低 → 解除零速锁定，最后输出节拍、检出 / 误报、逆解与连杆间隙汇总。M5 由 MissionOrchestrator 按 SOP 任务矩阵取代。
    /// </summary>
    public sealed class StationInspectionRoutine
    {
        private readonly RobotRuntime _runtime;
        private readonly AgvController _chassis;
        private readonly LiftColumnController _lift;
        private readonly AgvConfig _agv;
        private readonly ArmConfig _arm;
        private readonly NavOverlayView _navOverlay;
        private readonly InspectionOverlayView _overlay;

        /// <summary>创建流程。</summary>
        public StationInspectionRoutine(RobotRuntime runtime, AgvController chassis, LiftColumnController lift, AgvConfig agv, ArmConfig arm,
            NavOverlayView navOverlay, InspectionOverlayView overlay)
        {
            _runtime = runtime;
            _chassis = chassis;
            _lift = lift;
            _agv = agv;
            _arm = arm;
            _navOverlay = navOverlay;
            _overlay = overlay;
        }

        /// <summary>检测一个工位；任一步失败时停止并返回 false。汇总追加到 summary。</summary>
        public async Awaitable<bool> RunAsync(InspectionStation station, StringBuilder summary, CancellationToken ct)
        {
            RivetInspectionService inspection = _runtime.Inspection;
            ArmController arm = _runtime.Arm;
            arm.IkStats.Reset();
            arm.ResetClearanceStats();
            double start = _runtime.Clock.TimeS;

            LiftHeightPlan plan = LiftHeightPlanner.Plan(station.GetRivetHeightsM(), _agv, _arm);
            Phase($"{station.StationId}：零速锁定 → 升降柱升到安装面 {plan.MountHeightM:0.00} m");
            if (!_chassis.IsZeroSpeedLocked && !_chassis.ZeroSpeedLock()) return Fail("零速锁定失败");
            if (!await _lift.MoveToHeightAsync(plan.MountHeightM, ct)) return Fail("升降柱未到位");
            double liftDone = _runtime.Clock.TimeS;

            Phase($"{station.StationId}：T3 基准注册");
            if (_overlay != null) _overlay.SetProgress(station.StationId, 0, station.Rivets.Count);
            ArmMotionResult registered = await inspection.RegisterStationAsync(station, ct);
            if (!registered.Success) return Fail(registered.FailureReason);
            double registeredAt = _runtime.Clock.TimeS;
            float datumErrorM = inspection.Registration.DatumErrorM;

            for (int i = 0; i < station.Rivets.Count; i++)
            {
                Rivet rivet = station.Rivets[i];
                Phase($"{station.StationId}：铆钉 {i + 1}/{station.Rivets.Count}（{rivet.RivetId}）T4 → T5 → T6");
                if (_overlay != null) _overlay.SetProgress(station.StationId, i + 1, station.Rivets.Count);
                ArmMotionResult result = await inspection.InspectRivetAsync(rivet, i == 0, ct);
                if (!result.Success) return Fail($"{rivet.RivetId}：{result.FailureReason}");
            }
            double rivetsDone = _runtime.Clock.TimeS;

            Phase($"{station.StationId}：收臂 → 升降柱降到最低 → 解除零速锁定");
            ArmMotionResult stow = await inspection.StowAsync(ct);
            if (!stow.Success) return Fail(stow.FailureReason);
            if (!await _lift.LowerToBottomAsync(ct)) return Fail("升降柱未降到最低");
            if (!_chassis.ReleaseZeroSpeedLock()) return Fail("解除零速锁定被拒绝");
            double end = _runtime.Clock.TimeS;

            summary.AppendLine($"  {station.StationId} 检测：升柱 {liftDone - start:0.0} s、T3 注册 {registeredAt - liftDone:0.0} s、" +
                               $"{station.Rivets.Count} 颗铆钉 {rivetsDone - registeredAt:0.0} s、收臂与降柱 {end - rivetsDone:0.0} s，工位合计 {end - start:0.0} s（仿真）");
            summary.AppendLine($"  {inspection.Store.Summarize(station.StationId)}");
            summary.AppendLine($"  {arm.IkStats}");
            summary.AppendLine($"  已接受目标解与关节路径的最小间隙 {arm.MinAcceptedClearanceM * 1000f:0} mm（{arm.MinAcceptedClearanceLink}）；" +
                               $"基准注册误差 {datumErrorM * 1000f:0.00} mm");
            Phase($"{station.StationId}：检测完成");
            return true;
        }

        private void Phase(string text)
        {
            SimLog.Info(LogCategory.Mission, $"[M4 检测] {text}");
            if (_navOverlay != null) _navOverlay.SetPhase(text);
            if (_overlay != null) _overlay.SetPhase(text);
        }

        private bool Fail(string reason)
        {
            SimLog.Error(LogCategory.Mission, $"[M4 检测] 未完成：{reason}");
            Phase($"检测未完成：{reason}");
            return false;
        }
    }
}
