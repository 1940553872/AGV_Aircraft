namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 机械臂姿态状态（只读），供 <see cref="SafetyMonitor"/> 判定联锁。
    /// </summary>
    public interface IArmPoseState
    {
        /// <summary>是否处于运输姿态（Stow）。</summary>
        bool IsInTransportPose { get; }
    }
}
