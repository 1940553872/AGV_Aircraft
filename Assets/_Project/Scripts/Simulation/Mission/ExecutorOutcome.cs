namespace RivetSim.Simulation.Mission
{
    /// <summary>
    /// 执行器结果类别：完成、跳过（条件不满足，不视为失败）、失败。
    /// </summary>
    public enum ExecutorOutcome
    {
        /// <summary>完成。</summary>
        Done,
        /// <summary>跳过（不视为失败，后续单元照常执行）。</summary>
        Skipped,
        /// <summary>失败。</summary>
        Failed
    }
}
