using System;
using System.Collections.Generic;
using System.Threading;
using RivetSim.Core.Config;
using RivetSim.Core.Events;
using RivetSim.Core.Logging;
using RivetSim.Simulation.Robot;
using RivetSim.Simulation.World;
using UnityEngine;

namespace RivetSim.Simulation.Navigation
{
    /// <summary>
    /// 导航服务（主体 S1）：在代价地图上规划（A* + 平滑）、路径跟踪、对接停靠与直线行驶。
    /// 每个动作开始前经 <see cref="SafetyMonitor"/> 规则 2 判定（升降柱最低位且机械臂运输姿态），被拒绝时返回失败结果。
    /// 运动控制在 <see cref="Tick"/> 中按仿真子步执行，异步方法只等待任务结束。同一时刻只执行一个运动任务。
    /// </summary>
    public sealed class NavigationService
    {
        private readonly AgvController _chassis;
        private readonly SafetyMonitor _safety;
        private readonly OccupancyGridMap _map;
        private readonly KeepOutMask _keepOut;
        private readonly AgvConfig _agv;
        private readonly NavigationConfig _nav;
        private readonly EventBus _bus;
        private readonly Func<double> _timeS;
        private readonly GridAStar _astar = new GridAStar();
        private readonly List<Vector2Int> _cells = new List<Vector2Int>();
        private readonly PathFollower _follower;
        private readonly DockingController _docking;
        private readonly StraightDrive _straight;

        private IMotionTask _active;
        private MotionStatus _activeStatus;

        /// <summary>代价地图（每次规划前重建）。</summary>
        public CostMap CostMap { get; }

        /// <summary>虚拟禁行区（可为空）。</summary>
        public KeepOutMask KeepOut => _keepOut;

        /// <summary>最近一次规划的路径。</summary>
        public PlannedPath LastPath { get; private set; }

        /// <summary>对接控制器（供可视化读取标记可见性与估计位姿）。</summary>
        public DockingController Docking => _docking;

        /// <summary>路径跟踪器（供显示进度）。</summary>
        public PathFollower Follower => _follower;

        /// <summary>是否有运动任务在执行。</summary>
        public bool IsBusy => _active != null;

        /// <summary>含机械臂的扫掠半径，单位 m。</summary>
        public float SweptRadiusM { get; }

        /// <summary>创建导航服务。</summary>
        /// <param name="sweptRadiusM">含机械臂的扫掠半径（原地转向检查用）。</param>
        /// <param name="frontReachM">车头最前端（含机械臂）到车体中心的距离（对接车道检查用）。</param>
        public NavigationService(AgvController chassis, SafetyMonitor safety, OccupancyGridMap map, KeepOutMask keepOut,
            AgvConfig agv, NavigationConfig nav, EventBus bus, Func<double> timeS, float sweptRadiusM, float frontReachM, int seed)
        {
            _chassis = chassis ?? throw new ArgumentNullException(nameof(chassis));
            _safety = safety ?? throw new ArgumentNullException(nameof(safety));
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _keepOut = keepOut;
            _agv = agv;
            _nav = nav;
            _bus = bus;
            _timeS = timeS;
            SweptRadiusM = sweptRadiusM;
            CostMap = new CostMap(map.Geometry);
            _follower = new PathFollower(chassis, agv, nav, map, sweptRadiusM);
            _docking = new DockingController(chassis, agv, nav, map, frontReachM, new System.Random(seed));
            _straight = new StraightDrive(chassis, agv);
        }

        /// <summary>按仿真子步推进当前运动任务（须在底盘 Tick 之前调用）。</summary>
        public void Tick(float dtS)
        {
            if (_active == null || _activeStatus != MotionStatus.Running) return;
            _activeStatus = _active.Step(dtS);
        }

        /// <summary>中止当前运动任务（急停、切换遥控时调用）。</summary>
        public void Abort(string reason)
        {
            if (_active == null || _activeStatus != MotionStatus.Running) return;
            _active.Abort(reason);
            _activeStatus = MotionStatus.Failed;
        }

        /// <summary>停靠位姿 dock 对应的预停靠位姿（沿车头反方向后退预停靠距离）。</summary>
        public PlanarPose PreDockPose(PlanarPose dock) =>
            new PlanarPose(dock.positionM - DiffDriveKinematics.Heading(dock.yawDeg) * _nav.dockApproachDistanceM, dock.yawDeg);

        /// <summary>重建代价地图（激光层 + 虚拟禁行区，按车体外接圆膨胀）。</summary>
        public void RebuildCostMap() =>
            CostMap.Rebuild(_map, _keepOut, _agv.BodyCircumscribedRadiusM, _agv.fuselageSafetyClearanceM, _nav.unknownCellExtraCost);

        /// <summary>从当前位置规划到 goal；失败时给出原因。</summary>
        public bool TryPlan(PlanarPose goal, out PlannedPath path, out string failure)
        {
            path = null;
            RebuildCostMap();
            GridGeometry g = CostMap.Geometry;
            Vector2 start = _chassis.Pose.positionM;
            // 引入点：终点沿目标朝向后退前视距离。可通行且到终点直线无遮挡时先规划到引入点，再沿目标朝向直线进入终点，
            // 横向偏差在到点前收敛；离终点已很近时不加。
            Vector2 leadIn = goal.positionM - DiffDriveKinematics.Heading(goal.yawDeg) * _nav.lookaheadM;
            bool useLeadIn = (start - goal.positionM).sqrMagnitude > _nav.lookaheadM * _nav.lookaheadM &&
                             !CostMap.IsBlocked(g.WorldToCell(leadIn)) && PathSmoother.LineOfSight(CostMap, leadIn, goal.positionM);
            Vector2 target = useLeadIn ? leadIn : goal.positionM;
            if (!_astar.TryPlan(CostMap, g.WorldToCell(start), g.WorldToCell(target), _cells, out failure)) return false;
            List<Vector2> points = PathSmoother.Smooth(CostMap, _cells, start, target);
            if (useLeadIn) points.Add(goal.positionM);
            path = new PlannedPath(points);
            return true;
        }

        /// <summary>规划并行驶到 goal（含终点朝向）；label 用于日志。</summary>
        public async Awaitable<NavigationResult> NavigateToAsync(PlanarPose goal, string label, CancellationToken ct)
        {
            if (!CheckReady(label, out string refusal)) return Finish(label, NavigationResult.Failed(refusal));
            if (!TryPlan(goal, out PlannedPath path, out string failure))
                return Finish(label, NavigationResult.Failed($"规划失败：{failure}"));

            LastPath = path;
            SimLog.Info(LogCategory.Nav, $"{label}：规划路径 {path.LengthM:0.00} m（{path.Points.Count} 个顶点，A* 展开 {_astar.ExpandedCount} 格）");
            _bus?.Publish(new PathPlannedEvent(path, goal, false));
            return Finish(label, await RunFollowAsync(path.Points, _agv.cruiseSpeedMps, goal.yawDeg, path.LengthM, null, ct));
        }

        /// <summary>按给定折线行驶（不规划，如巡游建图路线），progress 回调完成比例；首点之前自动补上当前位置。</summary>
        public async Awaitable<NavigationResult> FollowPathAsync(IReadOnlyList<Vector2> waypoints, float maxSpeedMps, string label,
            Action<float> progress, CancellationToken ct)
        {
            if (!CheckReady(label, out string refusal)) return Finish(label, NavigationResult.Failed(refusal));
            var points = new List<Vector2> { _chassis.Pose.positionM };
            foreach (Vector2 p in waypoints)
                if ((p - points[points.Count - 1]).sqrMagnitude > 1e-6f) points.Add(p);
            var path = new PlannedPath(points);
            LastPath = path;
            Vector2 last = points[points.Count - 1], prev = points[Mathf.Max(0, points.Count - 2)];
            _bus?.Publish(new PathPlannedEvent(path, new PlanarPose(last, DiffDriveKinematics.BearingDeg(prev, last)), false));
            return Finish(label, await RunFollowAsync(points, maxSpeedMps, null, path.LengthM, progress, ct));
        }

        /// <summary>
        /// 对接停靠到工位：不在预停靠位姿时先导航过去，再识别停靠标记并低速直线对接，记录停靠误差。
        /// </summary>
        public async Awaitable<DockingResult> DockAsync(InspectionStation station, CancellationToken ct)
        {
            PlanarPose dock = station.DockingPose;
            string label = $"对接 {station.StationId}";
            PlanarPose pre = PreDockPose(dock);
            PlanarPose pose = _chassis.Pose;
            bool atPre = (pose.positionM - pre.positionM).magnitude <= _nav.goalPositionToleranceM &&
                         Mathf.Abs(Mathf.DeltaAngle(pose.yawDeg, pre.yawDeg)) <= _nav.goalYawToleranceDeg;
            if (!atPre)
            {
                NavigationResult toPre = await NavigateToAsync(pre, $"导航到 {station.StationId} 预停靠位姿", ct);
                if (!toPre.Success) return FinishDock(DockingResult.Failed(station.StationId, dock, toPre.FailureReason));
            }
            if (!CheckReady(label, out string refusal)) return FinishDock(DockingResult.Failed(station.StationId, dock, refusal));

            if (!_docking.Start(dock, station.DockingMarker))
                return FinishDock(DockingResult.Failed(station.StationId, dock, _docking.FailureReason));
            SimLog.Info(LogCategory.Nav, $"{label}：识别到停靠标记 ID {station.DockingMarker.TagId}，开始低速对接");
            _bus?.Publish(new PathPlannedEvent(new PlannedPath(new[] { _chassis.Pose.positionM, dock.positionM }), dock, true));

            double start = _timeS();
            float odometer = _chassis.OdometerM;
            MotionStatus status = await RunTaskAsync(_docking, ct);
            if (status != MotionStatus.Succeeded)
                return FinishDock(DockingResult.Failed(station.StationId, dock, _docking.FailureReason));

            DockingError error = DockingError.Between(dock, _chassis.Pose);
            bool within = error.IsWithin(_nav.dockingPositionToleranceM, _nav.dockingYawToleranceDeg);
            return FinishDock(new DockingResult(station.StationId, true, "", dock, error, within,
                (float)(_timeS() - start), _chassis.OdometerM - odometer));
        }

        /// <summary>沿车头反方向倒车 distanceM（接近工位速度）。</summary>
        public Awaitable<NavigationResult> ReverseExitAsync(float distanceM, CancellationToken ct) =>
            DriveStraightAsync(-Mathf.Abs(distanceM), _agv.approachSpeedMps, $"倒车退出 {distanceM:0.00} m", ct);

        /// <summary>直线行驶 signedDistanceM（负值倒车），速度上限 speedMps。</summary>
        public async Awaitable<NavigationResult> DriveStraightAsync(float signedDistanceM, float speedMps, string label, CancellationToken ct)
        {
            if (!CheckReady(label, out string refusal)) return Finish(label, NavigationResult.Failed(refusal));
            double start = _timeS();
            float odometer = _chassis.OdometerM;
            _straight.Start(signedDistanceM, speedMps);
            MotionStatus status = await RunTaskAsync(_straight, ct);
            return Finish(label, status == MotionStatus.Succeeded
                ? new NavigationResult(true, "", Mathf.Abs(signedDistanceM), _chassis.OdometerM - odometer, (float)(_timeS() - start))
                : NavigationResult.Failed(_straight.FailureReason));
        }

        private async Awaitable<NavigationResult> RunFollowAsync(IReadOnlyList<Vector2> points, float maxSpeedMps, float? finalYawDeg,
            float pathLengthM, Action<float> progress, CancellationToken ct)
        {
            double start = _timeS();
            float odometer = _chassis.OdometerM;
            _follower.Start(points, maxSpeedMps, finalYawDeg);
            MotionStatus status = await RunTaskAsync(_follower, ct, progress == null ? null : () => progress(_follower.ProgressFraction));
            return status == MotionStatus.Succeeded
                ? new NavigationResult(true, "", pathLengthM, _chassis.OdometerM - odometer, (float)(_timeS() - start))
                : NavigationResult.Failed(_follower.FailureReason);
        }

        private async Awaitable<MotionStatus> RunTaskAsync(IMotionTask task, CancellationToken ct, Action onFrame = null)
        {
            _active = task;
            _activeStatus = MotionStatus.Running;
            try
            {
                while (_activeStatus == MotionStatus.Running)
                {
                    await Awaitable.NextFrameAsync(ct);
                    onFrame?.Invoke();
                }
                return _activeStatus;
            }
            catch (OperationCanceledException)
            {
                task.Abort("已取消");
                throw;
            }
            finally
            {
                _active = null;
            }
        }

        private bool CheckReady(string label, out string refusal)
        {
            refusal = "";
            if (_active != null)
            {
                refusal = "已有运动任务在执行";
            }
            else if (_chassis.IsZeroSpeedLocked)
            {
                refusal = "底盘零速锁定中";
                SimLog.Warning(LogCategory.Safety, $"联锁拒绝：{label}（{refusal}）");
            }
            else if (!_safety.TryAuthorizeChassisMotion(label))
            {
                _safety.CheckChassisMotion(out string reason);
                refusal = $"联锁拒绝：{reason}";
            }
            return refusal.Length == 0;
        }

        private NavigationResult Finish(string label, NavigationResult result)
        {
            if (result.Success) SimLog.Info(LogCategory.Nav, $"{label} 完成：{result}");
            else SimLog.Warning(LogCategory.Nav, $"{label} 未完成：{result.FailureReason}");
            _bus?.Publish(new NavigationCompletedEvent(label, result));
            return result;
        }

        private DockingResult FinishDock(DockingResult result)
        {
            if (result.Success && result.WithinTolerance) SimLog.Info(LogCategory.Nav, result.ToString());
            else SimLog.Warning(LogCategory.Nav, result.ToString());
            _bus?.Publish(new DockingCompletedEvent(result));
            return result;
        }
    }
}
