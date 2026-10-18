namespace RivetSim.Core.TaskMatrix
{
    /// <summary>
    /// 任务矩阵单元类型（对应 JSON 的 <c>kind</c>）。
    /// </summary>
    public enum CellKind
    {
        /// <summary>主动执行（实线）。</summary>
        Active,
        /// <summary>待命（灰字），不执行。</summary>
        Standby,
        /// <summary>条件触发（虚线），trigger 发生时激活。</summary>
        Conditional
    }
}
