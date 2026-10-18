namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 占位臂姿态状态：占位臂固定为收拢姿态，始终视为运输姿态。M4 由 ArmController 实现 <see cref="IArmPoseState"/> 后删除。
    /// </summary>
    public sealed class PlaceholderArmPoseState : IArmPoseState
    {
        /// <inheritdoc />
        public bool IsInTransportPose => true;
    }
}
