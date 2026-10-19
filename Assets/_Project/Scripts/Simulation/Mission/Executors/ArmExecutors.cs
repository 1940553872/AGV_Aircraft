using System.Threading;
using RivetSim.Core.Logging;
using RivetSim.Simulation.Inspection;
using RivetSim.Simulation.Robot;
using RivetSim.Simulation.World;
using UnityEngine;

namespace RivetSim.Simulation.Mission.Executors
{
    /// <summary>
    /// S3 机械臂执行器（arm.*），同时负责升降柱（M5 模板第 5 条）：t(1,3) / t(2,3) 保持运输姿态与升降柱最低位；
    /// t(3,3) 升到工位作业高度后进入观察位姿；t(4,3) 规划与视觉修正；t(5,3) 力控接触；t(6,3) 回退离开近距离限速区，工位最后一颗时收臂并降到最低。
    /// </summary>
    public static class ArmExecutors
    {
        /// <summary>注册本主体的执行器。</summary>
        public static void Register(ExecutorRegistry registry, MissionServices s)
        {
            registry.Register("arm.stow", new DelegateExecutor((cell, cursor, ct) => EnsureStowAsync(s, ct)));
            s.RegisterLogic(registry, "arm.holdStow", a => s.Arm.IsInTransportPose && s.Lift.IsAtBottom
                ? ExecutorResult.Done("运输姿态，升降柱最低位")
                : ExecutorResult.Fail(s.Arm.IsInTransportPose ? "升降柱不在最低位" : "机械臂不在运输姿态"));
            registry.Register("arm.toObservePose", new DelegateExecutor((cell, cursor, ct) => ToObserveAsync(s, cursor, ct)));
            registry.Register("arm.alignWithVisionCorrection", new DelegateExecutor((cell, cursor, ct) => AlignAsync(s, cursor, ct)));
            registry.Register("arm.forceControlledContact", new DelegateExecutor((cell, cursor, ct) => ContactAsync(s, cursor, ct)));
            registry.Register("arm.retractHold", new DelegateExecutor((cell, cursor, ct) => RetractAsync(s, cursor, ct)));
        }

        private static async Awaitable<ExecutorResult> EnsureStowAsync(MissionServices s, CancellationToken ct)
        {
            if (!s.Arm.IsInTransportPose)
            {
                ArmMotionResult stow = await s.Inspection.StowAsync(ct);
                if (!stow.Success) return ExecutorResult.Fail(stow.FailureReason);
            }
            if (!s.Lift.IsAtBottom && !await s.Lift.LowerToBottomAsync(ct)) return ExecutorResult.Fail("升降柱未降到最低");
            await s.WaitSimAsync(s.Settings.logicCellNominalDurationS, ct);
            return ExecutorResult.Done("运输姿态，升降柱最低位");
        }

        private static async Awaitable<ExecutorResult> ToObserveAsync(MissionServices s, MissionCursor cursor, CancellationToken ct)
        {
            InspectionStation station = s.Station(cursor);
            LiftHeightPlan plan = LiftHeightPlanner.Plan(station.GetRivetHeightsM(), s.Agv, s.ArmConfig);
            if (!await s.Lift.MoveToHeightAsync(plan.MountHeightM, ct)) return ExecutorResult.Fail("升降柱未到工位作业高度");
            ArmMotionResult observe = await s.Inspection.MoveToObserveAsync(station, ct);
            return MissionServices.FromArm(observe, $"升降柱安装面 {s.Lift.MountHeightM:0.000} m，观察位姿");
        }

        // 规划到预计钉位 → 通知视觉复测 → 等复测结果 → 视觉修正。
        private static async Awaitable<ExecutorResult> AlignAsync(MissionServices s, MissionCursor cursor, CancellationToken ct)
        {
            Rivet rivet = s.Rivet(cursor);
            MissionBlackboard b = s.Board;
            bool joint = b.JointApproachNext;
            ArmMotionResult move = await s.Inspection.MoveToPredictedAsync(rivet, joint, ct);
            if (!move.Success)
            {
                b.ArmAlignFailed = true;
                return ExecutorResult.Fail(move.FailureReason);
            }
            b.ArmAtPredicted = true;
            await s.WaitUntilAsync(() => b.CenterMeasured || b.CenterMeasureFailed, ct);
            if (b.CenterMeasureFailed) return ExecutorResult.Fail("钉心复测失败，无法视觉修正");
            ArmMotionResult correct = await s.Inspection.CorrectToMeasuredAsync(rivet, ct);
            if (!correct.Success) return ExecutorResult.Fail(correct.FailureReason);
            b.JointApproachNext = false;
            return ExecutorResult.Done($"{(joint ? "关节插值" : "直线横移")}到 {rivet.RivetId}，视觉修正完成");
        }

        private static async Awaitable<ExecutorResult> ContactAsync(MissionServices s, MissionCursor cursor, CancellationToken ct)
        {
            Rivet rivet = s.Rivet(cursor);
            if (s.Conditions.Consume(FaultKind.ContactForceExceeded))
            {
                s.Contact.SurfaceOffsetFaultM = s.ArmConfig.preContactStandoffM + s.Settings.faultForceCompressionM;
                SimLog.Warning(LogCategory.Arm, $"[故障注入] 力超限：{rivet.RivetId} 实际表面比注册预测近 {s.Contact.SurfaceOffsetFaultM * 1000f:0} mm");
            }
            ArmMotionResult r = await s.Inspection.ApproachContactAsync(rivet, ct);
            return MissionServices.FromArm(r, $"{rivet.RivetId} 接触力 {s.Contact.ForceN:0.0} N");
        }

        private static async Awaitable<ExecutorResult> RetractAsync(MissionServices s, MissionCursor cursor, CancellationToken ct)
        {
            Rivet rivet = s.Rivet(cursor);
            RivetInspectionService inspection = s.Inspection;
            await s.WaitUntilAsync(() => !s.Arm.IsMoving, ct);
            float before = inspection.SurfaceDistanceM(rivet);
            // 退到近距离限速区外，并留浮动行程的余量，保证 C12（≥ 限速区距离）判定不受末端跟踪误差影响。
            float target = s.ArmConfig.nearFieldDistanceM + s.ArmConfig.toolFloatTravelM;
            ArmMotionResult retreat = await inspection.RetreatToDistanceAsync(rivet, target, "T6 回退", ct);
            if (!retreat.Success) return ExecutorResult.Fail(retreat.FailureReason);
            float after = inspection.SurfaceDistanceM(rivet);
            s.Board.RetreatM = after - before;
            s.Board.SurfaceDistanceAfterRetreatM = after;
            if (!cursor.IsLastRivetInStation)
                return ExecutorResult.Done($"回退 {(after - before) * 1000f:0} mm，距表面 {after * 1000f:0} mm");

            ArmMotionResult stow = await inspection.StowAsync(ct);
            if (!stow.Success) return ExecutorResult.Fail(stow.FailureReason);
            if (!await s.Lift.LowerToBottomAsync(ct)) return ExecutorResult.Fail("升降柱未降到最低");
            return ExecutorResult.Done($"回退 {(after - before) * 1000f:0} mm；工位最后一颗：收臂，升降柱降到最低");
        }
    }
}
