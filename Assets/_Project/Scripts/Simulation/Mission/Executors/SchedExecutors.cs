using System.Threading;
using RivetSim.Core.Logging;
using RivetSim.Simulation.Robot;
using RivetSim.Simulation.World;
using UnityEngine;

namespace RivetSim.Simulation.Mission.Executors
{
    /// <summary>
    /// S7 调度执行器（sched.*，MissionOrchestrator 内部逻辑的可视化单元）：检查条件下发任务、切换阶段、分解铆钉任务、
    /// 分配资源锁并判定 C1、采集无效后的重贴调度（t(5,7)）、逐层核验提交。
    /// </summary>
    public static class SchedExecutors
    {
        /// <summary>注册本主体的执行器。</summary>
        public static void Register(ExecutorRegistry registry, MissionServices s)
        {
            s.RegisterLogic(registry, "sched.checkAndDispatch", a =>
            {
                InspectionRegion region = s.Region(a.Cursor);
                s.Station(a.Cursor);
                return ExecutorResult.Done($"下发任务：{region.RegionId}，{region.Stations.Count} 个工位");
            });
            s.RegisterLogic(registry, "sched.switchPhase", a => ExecutorResult.Done($"切换到 {s.Station(a.Cursor).StationId} 精确停靠"));
            s.RegisterLogic(registry, "sched.decomposeRivets", a =>
            {
                InspectionStation station = s.Station(a.Cursor);
                return ExecutorResult.Done($"{station.StationId}：{station.Rivets.Count} 颗铆钉，生成逐钉单元 T4～T6");
            });
            s.RegisterLogic(registry, "sched.allocateResourceLock", a =>
            {
                Rivet rivet = s.Rivet(a.Cursor);
                s.Board.ResourceLockHeld = true;
                int passed = 0;
                foreach (string id in a.Cell.ConditionRefs)
                {
                    bool ok = id == "C1" ? s.Board.ResourceLockHeld : false;
                    if (s.Evaluator.Report(a.Cell.Id, id, ok, ok ? 1f : 0f, ok ? $"{rivet.RivetId}：机械臂 + 超声通道" : "无判定实现")) passed++;
                }
                return ExecutorResult.Done($"{rivet.RivetId} 资源锁已分配；满足 {passed}/{a.Cell.ConditionRefs.Count} 项");
            });
            registry.Register("sched.reattach", new DelegateExecutor((cell, cursor, ct) => ReattachAsync(s, cursor, ct)));
            s.RegisterLogic(registry, "sched.verifyAndSubmit", a =>
            {
                Rivet rivet = s.Rivet(a.Cursor);
                MissionBlackboard b = s.Board;
                return ExecutorResult.Done($"{rivet.RivetId} 逐层核验：释放条件 {b.ReleaseConditionsPassed}/{b.ReleaseConditionsChecked}，提交");
            });
        }

        // 采集无效：退回贴合起点 → 重新注入耦合剂（清除耦合故障）→ 由 retryPhase 重试 T5。
        private static async Awaitable<ExecutorResult> ReattachAsync(MissionServices s, MissionCursor cursor, CancellationToken ct)
        {
            Rivet rivet = s.Rivet(cursor);
            await s.WaitUntilAsync(() => !s.Arm.IsMoving, ct);
            ArmMotionResult r = await s.Inspection.RetreatToDistanceAsync(rivet, s.ArmConfig.preContactStandoffM, "重贴：退回", ct);
            if (!r.Success) return ExecutorResult.Fail(r.FailureReason);
            s.Contact.CouplingFaultFactor = 1f;
            await s.WaitSimAsync(s.Settings.logicCellNominalDurationS, ct);
            SimLog.Info(LogCategory.UT, $"{rivet.RivetId} 重贴调度：已退回贴合起点并重新注入耦合剂");
            return ExecutorResult.Done("退回贴合起点，重新注入耦合剂");
        }
    }
}
