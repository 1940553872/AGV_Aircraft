using RivetSim.Core.Config;
using RivetSim.Presentation.UI;
using RivetSim.Simulation.Robot;

namespace RivetSim.Presentation.App
{
    /// <summary>
    /// 主场景设备状态数据源（M8）：只读 RobotRuntime 的底盘、升降柱、机械臂与安全联锁；电量与通讯延迟取 <see cref="TeachingPowerCommModel"/>（教学简化模型）。
    /// </summary>
    public sealed class RuntimeDeviceStatusSource : IDeviceStatusSource
    {
        private readonly RobotRuntime _runtime;
        private readonly TeachingPowerCommModel _power;

        /// <summary>创建。</summary>
        public RuntimeDeviceStatusSource(RobotRuntime runtime, AgvConfig agv, int seed)
        {
            _runtime = runtime;
            _power = new TeachingPowerCommModel(agv, seed);
        }

        /// <inheritdoc />
        public PlanarPose Pose => _runtime.Chassis.Pose;

        /// <inheritdoc />
        public float LinearSpeedMps => _runtime.Chassis.LinearSpeedMps;

        /// <inheritdoc />
        public float AngularSpeedDegPerS => _runtime.Chassis.AngularSpeedDegPerS;

        /// <inheritdoc />
        public float LiftExtensionM => _runtime.Lift.ExtensionM;

        /// <inheritdoc />
        public string ArmStateText
        {
            get
            {
                ArmController arm = _runtime.Arm;
                if (arm.IsMoving) return "运动中：" + arm.CurrentMotionLabel;
                return arm.IsInTransportPose ? "运输姿态（收拢）" : "作业姿态保持";
            }
        }

        /// <inheritdoc />
        public bool ChassisZeroSpeedLocked => _runtime.Chassis.IsZeroSpeedLocked;

        /// <inheritdoc />
        public bool ChassisMotionPermitted => _runtime.Safety.CheckChassisMotion(out _);

        /// <inheritdoc />
        public bool LiftMotionPermitted => _runtime.Safety.CheckLiftMotion(out _);

        /// <inheritdoc />
        public bool ArmMotionPermitted => _runtime.Safety.CheckArmLeaveTransport(out _);

        /// <inheritdoc />
        public float BatterySocPct => _power.BatterySocPct(_runtime.Clock.TimeS, _runtime.Chassis.OdometerM);

        /// <inheritdoc />
        public float CommLatencyS => _power.CommLatencyS(_runtime.Clock.TimeS);
    }
}
