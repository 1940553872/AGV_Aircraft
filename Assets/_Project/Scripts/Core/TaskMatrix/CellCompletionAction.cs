namespace RivetSim.Core.TaskMatrix
{
    /// <summary>
    /// 条件单元完成后的动作（对应 JSON 的 <c>onComplete</c>）。
    /// </summary>
    public enum CellCompletionAction
    {
        /// <summary>无附加动作。</summary>
        None,
        /// <summary>重新执行所在阶段，最多 <c>maxRetries</c> 次。</summary>
        RetryPhase
    }
}
