using System.Threading;
using RivetSim.Core.Config;
using RivetSim.Simulation.Inspection;
using RivetSim.Simulation.World;
using UnityEngine;

namespace RivetSim.Simulation.Mission.Executors
{
    /// <summary>
    /// S5 超声接口执行器（ut.*）：载入配方、接收采集并判定有效性（无效时上报“采集无效”）、接收结论（闸门判读为教学简化模型）。
    /// </summary>
    public static class UtExecutors
    {
        /// <summary>注册本主体的执行器。</summary>
        public static void Register(ExecutorRegistry registry, MissionServices s)
        {
            s.RegisterLogic(registry, "ut.loadRecipe", a =>
            {
                s.Station(a.Cursor);
                s.Board.RecipeLoaded = true;
                InspectionConfig c = s.InspectionConfig;
                return ExecutorResult.Done($"{c.probeFrequencyMHz:0} MHz、声速 {c.longitudinalVelocityMps:0} m/s、闸门 A {c.gateAThresholdRatio:P0} / B {c.gateBThresholdRatio:P0}");
            });
            registry.Register("ut.receiveValidity", new DelegateExecutor((cell, cursor, ct) => ReceiveValidityAsync(s, cursor, ct)));
            s.RegisterLogic(registry, "ut.receiveConclusion", a =>
            {
                s.Rivet(a.Cursor);
                InspectionRecord r = s.Board.CommittedRecord;
                s.Board.ConclusionReady = r != null;
                return r != null ? ExecutorResult.Done($"结论：{RivetInspectionService.Label(r.Verdict)}") : ExecutorResult.Skip("本颗没有采集结论");
            });
        }

        private static async Awaitable<ExecutorResult> ReceiveValidityAsync(MissionServices s, MissionCursor cursor, CancellationToken ct)
        {
            Rivet rivet = s.Rivet(cursor);
            await s.WaitSimAsync(s.Settings.logicCellNominalDurationS, ct);
            InspectionRecord record = s.Inspection.JudgeLastAcquisition(rivet);
            if (record == null) return ExecutorResult.Fail("没有可判读的采集数据");
            s.Board.PendingRecord = record;
            if (record.Verdict != InspectionVerdict.Invalid) return ExecutorResult.Done($"有效：{RivetInspectionService.Label(record.Verdict)}（{record.Reason}）");
            s.Board.InvalidRetryPending = s.Conditions.Raise(ConditionBus.AcquisitionInvalidTrigger, record.Reason);
            return ExecutorResult.Done($"无效：{record.Reason}{(s.Board.InvalidRetryPending ? "，转重贴" : "，重贴次数已用尽，记为无效")}");
        }
    }
}
