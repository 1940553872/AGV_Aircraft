namespace RivetSim.Simulation.Navigation
{
    /// <summary>
    /// 底盘运动任务：由 <see cref="NavigationService"/> 在每个仿真子步调用 <see cref="Step"/>，任务内部向底盘下发速度指令。
    /// </summary>
    public interface IMotionTask
    {
        /// <summary>失败原因（未失败时为空）。</summary>
        string FailureReason { get; }

        /// <summary>按仿真步长推进一次控制，返回当前状态。</summary>
        MotionStatus Step(float dtS);

        /// <summary>中止任务（底盘停车）。</summary>
        void Abort(string reason);
    }
}
