using System.Text;
using System.Threading;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Presentation.UI;
using RivetSim.Presentation.Visualization;
using RivetSim.Simulation.Navigation;
using RivetSim.Simulation.Robot;
using RivetSim.Simulation.World;
using UnityEngine;

namespace RivetSim.Presentation.App
{
    /// <summary>
    /// 主场景演示流程（M3 建立，M4 扩展）：驶离充电位 → 巡游建图 → 返回充电位 → 从充电位导航到工位预停靠位姿 → 识别标记对接停车 →
    /// 联锁演示 1（零速锁定、升降柱升起时解锁被拒绝）→ 单工位检测（<see cref="StationInspectionRoutine"/>，结束时收臂、降柱、解锁）→
    /// 联锁演示 2（机械臂离开运输姿态时解锁 / 倒车被拒绝）→ 倒车退出，最后输出路径、停靠误差与检测汇总。
    /// M5 由 MissionOrchestrator 按 SOP 任务矩阵取代。
    /// </summary>
    public sealed class NavigationDemoRoutine
    {
        // 联锁演示 2：腕 3 转过的角度，单位 °（使机械臂离开运输姿态，外廓仍在车体投影内）。
        private const float InterlockDemoWristDeg = 30f;

        private readonly RobotRuntime _runtime;
        private readonly AgvController _chassis;
        private readonly LiftColumnController _lift;
        private readonly SceneLayoutConfig _layout;
        private readonly AgvConfig _agv;
        private readonly ArmConfig _arm;
        private readonly NavigationConfig _nav;
        private readonly NavOverlayView _overlay;
        private readonly InspectionOverlayView _inspectionOverlay;

        /// <summary>创建演示流程。</summary>
        public NavigationDemoRoutine(RobotRuntime runtime, AgvController chassis, LiftColumnController lift, SceneLayoutConfig layout,
            AgvConfig agv, ArmConfig arm, NavigationConfig nav, NavOverlayView overlay, InspectionOverlayView inspectionOverlay)
        {
            _runtime = runtime;
            _chassis = chassis;
            _lift = lift;
            _layout = layout;
            _agv = agv;
            _arm = arm;
            _nav = nav;
            _overlay = overlay;
            _inspectionOverlay = inspectionOverlay;
        }

        /// <summary>对目标工位执行完整演示；任一步失败时停止并返回 false。</summary>
        public async Awaitable<bool> RunAsync(InspectionStation station, CancellationToken ct)
        {
            NavigationService nav = _runtime.Navigation;
            var summary = new StringBuilder("主场景演示汇总（M3 导航 + M4 单工位检测）：\n");
            PlanarPose charger = _layout.chargingDockPose;
            var chargerFront = new PlanarPose(charger.positionM + DiffDriveKinematics.Heading(charger.yawDeg) * _nav.dockApproachDistanceM, charger.yawDeg);

            Phase("准备：环境建图（驶离充电位 → 巡游路线）");
            NavigationResult leave = await nav.DriveStraightAsync(_nav.dockApproachDistanceM, _agv.approachSpeedMps, "驶离充电位", ct);
            if (!leave.Success) return false;
            NavigationResult mapping = await _runtime.Mapping.RunAsync(_layout.mappingRouteM, ct);
            if (!mapping.Success) return false;
            summary.AppendLine($"  建图：驶离充电位 {leave.TraveledM:0.00} m + 巡游路线 {mapping.PathLengthM:0.00} m，里程 {mapping.TraveledM:0.00} m，" +
                               $"用时 {leave.DurationS + mapping.DurationS:0.0} s（仿真），已观测 {_runtime.Map.KnownCellCount:N0} 格");

            Phase("返回充电位");
            NavigationResult back = await nav.NavigateToAsync(chargerFront, "返回充电位前方", ct);
            if (!back.Success) return false;
            NavigationResult dockIn = await nav.DriveStraightAsync(-_nav.dockApproachDistanceM, _agv.approachSpeedMps, "倒车进入充电位", ct);
            if (!dockIn.Success) return false;

            Phase($"导航：充电位 → {station.StationId}");
            NavigationResult exit = await nav.DriveStraightAsync(_nav.dockApproachDistanceM, _agv.approachSpeedMps, "驶离充电位", ct);
            if (!exit.Success) return false;
            NavigationResult go = await nav.NavigateToAsync(nav.PreDockPose(station.DockingPose), $"充电位 → {station.StationId} 预停靠位姿", ct);
            if (!go.Success) return false;
            summary.AppendLine($"  导航（充电位 → {station.StationId} 预停靠位姿）：驶离充电位 {exit.TraveledM:0.00} m + 规划路径 {go.PathLengthM:0.00} m，" +
                               $"实际里程 {exit.TraveledM + go.TraveledM:0.00} m，用时 {exit.DurationS + go.DurationS:0.0} s（仿真）");

            Phase($"对接停靠：{station.StationId}（识别停靠标记 ID {station.DockingMarker.TagId}，低速直线对接）");
            DockingResult docked = await nav.DockAsync(station, ct);
            if (!docked.Success) return false;
            summary.AppendLine($"  对接：{docked}");

            Phase("联锁演示 1：零速锁定 → 升降柱升到工位高度 → 尝试解锁（应被拒绝）");
            _chassis.ZeroSpeedLock();
            LiftHeightPlan plan = LiftHeightPlanner.Plan(station.GetRivetHeightsM(), _agv, _arm);
            if (!await _lift.MoveToHeightAsync(plan.MountHeightM, ct)) return false;
            bool releasedWhileRaised = _chassis.ReleaseZeroSpeedLock();
            NavigationResult refused = await nav.ReverseExitAsync(_agv.reverseExitDistanceM, ct);
            summary.AppendLine($"  联锁 1：升降柱安装面 {_lift.MountHeightM:0.000} m 时解锁{(releasedWhileRaised ? "未被拒绝（异常）" : "被拒绝")}，" +
                               $"倒车指令{(refused.Success ? "未被拒绝（异常）" : $"被拒绝（{refused.FailureReason}）")}");

            var inspection = new StationInspectionRoutine(_runtime, _chassis, _lift, _agv, _arm, _overlay, _inspectionOverlay);
            if (!await inspection.RunAsync(station, summary, ct)) return false;

            Phase($"联锁演示 2：零速锁定 → 机械臂离开运输姿态（腕 3 转 {InterlockDemoWristDeg:0}°）→ 尝试解锁（应被拒绝）→ 收回");
            ArmController arm = _runtime.Arm;
            _chassis.ZeroSpeedLock();
            float[] q = arm.NamedPose(ArmConfig.StowPoseName);
            q[q.Length - 1] += InterlockDemoWristDeg;
            if (!(await arm.MoveToJointsAsync(q, "离开运输姿态（联锁演示）", ct)).Success) return false;
            bool releasedOutOfStow = _chassis.ReleaseZeroSpeedLock();
            summary.AppendLine($"  联锁 2：机械臂不在运输姿态时解锁{(releasedOutOfStow ? "未被拒绝（异常）" : "被拒绝")}");
            if (!(await arm.MoveToNamedPoseAsync(ArmConfig.StowPoseName, false, ct)).Success) return false;
            if (!_chassis.ReleaseZeroSpeedLock()) return false;

            Phase($"倒车退出 {_agv.reverseExitDistanceM:0.00} m");
            NavigationResult reverse = await nav.ReverseExitAsync(_agv.reverseExitDistanceM, ct);
            if (!reverse.Success) return false;
            summary.AppendLine($"  倒车退出：{reverse.TraveledM:0.000} m，用时 {reverse.DurationS:0.0} s");
            summary.Append($"  总仿真时间 {_runtime.Clock.TimeS:0.0} s");

            SimLog.Info(LogCategory.Mission, summary.ToString());
            Phase("演示完成：可用 WASD 遥控，空格急停，V 切换可视化");
            return true;
        }

        private void Phase(string text)
        {
            SimLog.Info(LogCategory.Mission, $"[演示] {text}");
            if (_overlay != null) _overlay.SetPhase(text);
        }
    }
}
