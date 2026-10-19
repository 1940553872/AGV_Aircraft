using RivetSim.Core.Config;

namespace RivetSim.Presentation.UI
{
    /// <summary>
    /// 设备状态只读数据源（M8，“检测结果与设备状态”视图用）：AGV 位姿与速度、升降柱、机械臂、安全联锁、电量与通讯延迟。
    /// 主场景取自 RobotRuntime；电量与通讯延迟为教学简化模型。
    /// </summary>
    public interface IDeviceStatusSource
    {
        /// <summary>AGV 位姿（底盘真值）。</summary>
        PlanarPose Pose { get; }

        /// <summary>线速度，单位 m/s。</summary>
        float LinearSpeedMps { get; }

        /// <summary>角速度，单位 °/s。</summary>
        float AngularSpeedDegPerS { get; }

        /// <summary>升降柱伸出量，单位 m。</summary>
        float LiftExtensionM { get; }

        /// <summary>机械臂状态文字（运输姿态 / 运动中：动作名 / 作业姿态保持）。</summary>
        string ArmStateText { get; }

        /// <summary>底盘是否零速锁定。</summary>
        bool ChassisZeroSpeedLocked { get; }

        /// <summary>联锁规则 2：底盘是否允许运动。</summary>
        bool ChassisMotionPermitted { get; }

        /// <summary>联锁规则 1：升降柱是否允许运动。</summary>
        bool LiftMotionPermitted { get; }

        /// <summary>联锁规则 3：机械臂是否允许离开运输姿态。</summary>
        bool ArmMotionPermitted { get; }

        /// <summary>电量，单位 %（教学简化模型）。</summary>
        float BatterySocPct { get; }

        /// <summary>通讯延迟，单位 s（教学简化模型）。</summary>
        float CommLatencyS { get; }
    }
}
