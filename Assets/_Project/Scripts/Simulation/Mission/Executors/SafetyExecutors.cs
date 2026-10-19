using System.Threading;
using RivetSim.Core.Logging;
using RivetSim.Simulation.Robot;
using RivetSim.Simulation.World;
using UnityEngine;

namespace RivetSim.Simulation.Mission.Executors
{
    /// <summary>
    /// S6 安全执行器（safety.*）：按 ADR-010 联锁规则（SafetyMonitor 规则 1～3）发放底盘 / 机械臂运动许可与退出释放许可，
    /// 近距离限速，以及力超限后的受控退出（t(5,6)）。
    /// </summary>
    public static class SafetyExecutors
    {
        /// <summary>注册本主体的执行器。</summary>
        public static void Register(ExecutorRegistry registry, MissionServices s)
        {
            s.RegisterLogic(registry, "safety.grantChassisMotion", a =>
            {
                if (s.Chassis.IsZeroSpeedLocked) return ExecutorResult.Fail("底盘仍处于零速锁定");
                return s.Safety.CheckChassisMotion(out string reason) ? ExecutorResult.Done("规则 2 满足：底盘运动许可") : ExecutorResult.Fail(reason);
            });
            s.RegisterLogic(registry, "safety.grantArmMotion", a =>
            {
                s.Station(a.Cursor);
                s.Board.ArmPermit = s.Safety.CheckArmLeaveTransport(out string reason);
                return s.Board.ArmPermit ? ExecutorResult.Done("规则 3 满足：机械臂运动许可") : ExecutorResult.Fail(reason);
            });
            s.RegisterLogic(registry, "safety.nearFieldSpeedLimit", a =>
            {
                s.Rivet(a.Cursor);
                s.Board.NearFieldLimit = true;
                return ExecutorResult.Done($"距表面 {s.ArmConfig.nearFieldDistanceM * 1000f:0} mm 内限速 {s.ArmConfig.nearFieldSpeedMps * 1000f:0} mm/s");
            });
            registry.Register("safety.controlledRetreat", new DelegateExecutor((cell, cursor, ct) => ControlledRetreatAsync(s, cursor, ct)));
            s.RegisterLogic(registry, "safety.grantExitRelease", a => GrantExitRelease(s, a.Cursor));
        }

        // 力超限：等逼近动作停止 → 复位表面偏差 → 沿工具轴退回贴合起点，随后由 retryPhase 重试 T5。
        private static async Awaitable<ExecutorResult> ControlledRetreatAsync(MissionServices s, MissionCursor cursor, CancellationToken ct)
        {
            Rivet rivet = s.Rivet(cursor);
            await s.WaitUntilAsync(() => !s.Arm.IsMoving, ct);
            float peak = s.Contact.ForceN;
            s.Contact.SurfaceOffsetFaultM = 0f;
            ArmMotionResult r = await s.Inspection.RetreatToDistanceAsync(rivet, s.ArmConfig.preContactStandoffM, "受控退出", ct);
            if (!r.Success) return ExecutorResult.Fail(r.FailureReason);
            SimLog.Info(LogCategory.Safety, $"{rivet.RivetId} 受控退出：超限时接触力 {peak:0.0} N，已退回贴合起点（距表面 {s.Inspection.SurfaceDistanceM(rivet) * 1000f:0} mm）");
            return ExecutorResult.Done($"超限 {peak:0.0} N 后退回贴合起点");
        }

        private static ExecutorResult GrantExitRelease(MissionServices s, MissionCursor cursor)
        {
            s.Rivet(cursor);
            if (s.Contact.ForceN > 0f) return ExecutorResult.Fail($"探头仍有接触力 {s.Contact.ForceN:0.0} N");
            if (!cursor.IsLastRivetInStation) return ExecutorResult.Done("探头释放许可");
            if (!s.Safety.CheckChassisMotion(out string reason)) return ExecutorResult.Fail(reason);
            return s.Chassis.ReleaseZeroSpeedLock() ? ExecutorResult.Done("规则 2 满足：解除零速锁定，允许倒车") : ExecutorResult.Fail("解除零速锁定被拒绝");
        }
    }
}
