using System.Threading;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Core.TaskMatrix;
using RivetSim.Simulation.Navigation;
using RivetSim.Simulation.World;
using UnityEngine;

namespace RivetSim.Simulation.Mission.Executors
{
    /// <summary>
    /// S1 导航执行器（nav.*、chassis.*）：全局导航（含整机识别）、对接停车、倒车退出换站、零速锁定。
    /// 整机识别为教学简化：不做轮廓匹配，只在注入“识别失败”时上报 t(1,8) 并等待操作员初值。
    /// </summary>
    public static class NavExecutors
    {
        /// <summary>注册本主体的执行器。</summary>
        public static void Register(ExecutorRegistry registry, MissionServices s)
        {
            registry.Register("nav.globalNavigate", new DelegateExecutor((cell, cursor, ct) => GlobalNavigateAsync(s, cursor, ct)));
            registry.Register("nav.dock", new DelegateExecutor((cell, cursor, ct) => DockAsync(s, cursor, ct)));
            registry.Register("nav.reverseExitAndChangeStation", new DelegateExecutor((cell, cursor, ct) => ReverseExitAsync(s, cursor, ct)));
            s.RegisterLogic(registry, "chassis.zeroSpeedLock", a =>
                s.Chassis.IsZeroSpeedLocked || s.Chassis.ZeroSpeedLock() ? ExecutorResult.Done("底盘零速锁定") : ExecutorResult.Fail("零速锁定被拒绝"));
            s.RegisterLogic(registry, "chassis.holdZeroSpeed", a =>
                s.Chassis.IsZeroSpeedLocked ? ExecutorResult.Done("零速锁定保持") : ExecutorResult.Fail("底盘未零速锁定"));
        }

        private static async Awaitable<ExecutorResult> GlobalNavigateAsync(MissionServices s, MissionCursor cursor, CancellationToken ct)
        {
            InspectionStation station = s.Station(cursor);
            NavigationService nav = s.Navigation;
            if (s.Conditions.Consume(FaultKind.RecognitionFailed))
            {
                SimLog.Warning(LogCategory.Nav, $"整机识别失败：激光轮廓与模型机不匹配（故障注入），无法确定 {s.Region(cursor).RegionId} 的全局位姿");
                if (!s.Conditions.Raise(ConditionBus.RecognitionFailedTrigger, "整机识别失败"))
                    return ExecutorResult.Fail("整机识别失败，且没有可处理的条件单元");
                await s.WaitUntilAsync(() => s.Board.InitialPoseProvided, ct);
                PlanarPose p = s.Board.InitialPose;
                SimLog.Info(LogCategory.Nav, $"按操作员初值 ({p.positionM.x:0.00}, {p.positionM.y:0.00}) m、{p.yawDeg:0.0}° 重新定位，整机识别成功");
            }
            else
            {
                SimLog.Info(LogCategory.Nav, $"整机识别：模型机轮廓匹配，目标 {s.Region(cursor).RegionId}（教学简化模型）");
            }
            NavigationResult r = await nav.NavigateToAsync(nav.PreDockPose(station.DockingPose), $"全局导航 → {station.StationId} 预停靠位姿", ct);
            return MissionServices.FromNav(r, $"路径 {r.PathLengthM:0.0} m，用时 {r.DurationS:0.0} s");
        }

        private static async Awaitable<ExecutorResult> DockAsync(MissionServices s, MissionCursor cursor, CancellationToken ct)
        {
            InspectionStation station = s.Station(cursor);
            NavigationService nav = s.Navigation;
            try
            {
                PlanarPose pre = nav.PreDockPose(station.DockingPose);
                if (Vector2.Distance(s.Chassis.Pose.positionM, pre.positionM) > s.NavigationConfig.goalPositionToleranceM)
                {
                    NavigationResult go = await nav.NavigateToAsync(pre, $"→ {station.StationId} 预停靠位姿", ct);
                    if (!go.Success) return ExecutorResult.Fail(go.FailureReason);
                }
                DockingResult docked = await nav.DockAsync(station, ct);
                s.Board.LastDock = docked;
                return docked.Success ? ExecutorResult.Done(docked.ToString()) : ExecutorResult.Fail(docked.FailureReason);
            }
            finally
            {
                s.Board.DockFinished = true;
            }
        }

        private static async Awaitable<ExecutorResult> ReverseExitAsync(MissionServices s, MissionCursor cursor, CancellationToken ct)
        {
            InspectionStation station = s.Station(cursor);
            await s.WaitUntilAsync(() => s.Lift.IsAtBottom, ct);
            NavigationResult reverse = await s.Navigation.ReverseExitAsync(s.Agv.reverseExitDistanceM, ct);
            if (!reverse.Success) return ExecutorResult.Fail(reverse.FailureReason);
            InspectionRegion region = s.Region(cursor);
            if (cursor.StationIndex + 1 >= region.Stations.Count)
                return ExecutorResult.Done($"倒车 {reverse.TraveledM:0.00} m；本区域工位已完成");
            InspectionStation next = region.Stations[cursor.StationIndex + 1];
            NavigationResult go = await s.Navigation.NavigateToAsync(s.Navigation.PreDockPose(next.DockingPose), $"换站：{station.StationId} → {next.StationId} 预停靠位姿", ct);
            return MissionServices.FromNav(go, $"倒车 {reverse.TraveledM:0.00} m，换站 → {next.StationId}（{go.PathLengthM:0.0} m）");
        }
    }
}
