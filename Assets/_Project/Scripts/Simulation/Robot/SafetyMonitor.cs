using System;
using RivetSim.Core.Logging;

namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 安全监视（主体 S6）：升降柱与底盘的联锁规则（ADR-010）。
    /// 规则 1：升降柱只在底盘零速锁定后运动。
    /// 规则 2：底盘只在升降柱降到最低位且机械臂处于运输姿态时运动（解除零速锁定同此条件）。
    /// 规则 3（M4，用户确认）：机械臂离开运输姿态只在底盘零速锁定后允许。
    /// <c>Check*</c> 只判定不记录；<c>TryAuthorize*</c> 判定失败时通过 SimLog 记录拒绝原因。
    /// </summary>
    public sealed class SafetyMonitor
    {
        private readonly IChassisState _chassis;
        private readonly ILiftState _lift;
        private readonly IArmPoseState _arm;

        /// <summary>创建安全监视。</summary>
        public SafetyMonitor(IChassisState chassis, ILiftState lift, IArmPoseState arm)
        {
            _chassis = chassis ?? throw new ArgumentNullException(nameof(chassis));
            _lift = lift ?? throw new ArgumentNullException(nameof(lift));
            _arm = arm ?? throw new ArgumentNullException(nameof(arm));
        }

        /// <summary>规则 1：升降柱是否允许运动。</summary>
        public bool CheckLiftMotion(out string reason)
        {
            if (!_chassis.IsZeroSpeedLocked)
            {
                reason = "底盘未零速锁定";
                return false;
            }
            reason = "";
            return true;
        }

        /// <summary>规则 2：底盘是否允许运动或解除零速锁定。</summary>
        public bool CheckChassisMotion(out string reason)
        {
            if (_lift.IsMoving)
            {
                reason = "升降柱正在运动";
                return false;
            }
            if (!_lift.IsAtBottom)
            {
                reason = $"升降柱未降到最低位（伸出 {_lift.ExtensionM:0.000} m）";
                return false;
            }
            if (!_arm.IsInTransportPose)
            {
                reason = "机械臂不在运输姿态";
                return false;
            }
            reason = "";
            return true;
        }

        /// <summary>规则 3：机械臂是否允许离开运输姿态。</summary>
        public bool CheckArmLeaveTransport(out string reason)
        {
            if (!_chassis.IsZeroSpeedLocked)
            {
                reason = "底盘未零速锁定";
                return false;
            }
            reason = "";
            return true;
        }

        /// <summary>判定规则 3；拒绝时记录 command 与原因。</summary>
        public bool TryAuthorizeArmLeaveTransport(string command) => Authorize(CheckArmLeaveTransport(out string reason), command, reason);

        /// <summary>判定规则 1；拒绝时记录 command 与原因。</summary>
        public bool TryAuthorizeLiftMotion(string command) => Authorize(CheckLiftMotion(out string reason), command, reason);

        /// <summary>判定规则 2；拒绝时记录 command 与原因。</summary>
        public bool TryAuthorizeChassisMotion(string command) => Authorize(CheckChassisMotion(out string reason), command, reason);

        private static bool Authorize(bool allowed, string command, string reason)
        {
            if (!allowed) SimLog.Warning(LogCategory.Safety, $"联锁拒绝：{command}（{reason}）");
            return allowed;
        }
    }
}
