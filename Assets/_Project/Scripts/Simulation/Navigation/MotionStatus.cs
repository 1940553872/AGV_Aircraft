namespace RivetSim.Simulation.Navigation
{
    /// <summary>
    /// 底盘运动任务的状态。
    /// </summary>
    public enum MotionStatus
    {
        /// <summary>执行中。</summary>
        Running,

        /// <summary>已完成。</summary>
        Succeeded,

        /// <summary>失败（联锁拒绝、急停、外廓检查不通过等）。</summary>
        Failed
    }
}
