namespace RivetSim.Core.Mission
{
    /// <summary>
    /// 任务矩阵单元的运行状态（Docs/Architecture.md §5.2）。
    /// </summary>
    public enum CellState
    {
        /// <summary>未到该阶段。</summary>
        Idle,
        /// <summary>待命（standby 单元）。</summary>
        Standby,
        /// <summary>条件单元已布防。</summary>
        Armed,
        /// <summary>正在执行。</summary>
        Running,
        /// <summary>本轮已完成。</summary>
        Done,
        /// <summary>条件不满足而跳过。</summary>
        Skipped,
        /// <summary>条件单元被激活（随后转 Running）。</summary>
        Triggered,
        /// <summary>失败。</summary>
        Failed
    }
}
