namespace RivetSim.Core.TaskMatrix
{
    /// <summary>
    /// 回路箭头线型（对应 JSON 的 <c>loops[].style</c>）。
    /// </summary>
    public enum LoopStyle
    {
        /// <summary>实线（如“下一颗”）。</summary>
        Solid,
        /// <summary>虚线（如“换站”）。</summary>
        Dashed
    }
}
