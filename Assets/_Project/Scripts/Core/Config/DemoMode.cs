namespace RivetSim.Core.Config
{
    /// <summary>
    /// 演示模式。
    /// </summary>
    public enum DemoMode
    {
        /// <summary>自动演示（可倍速）。</summary>
        Auto,
        /// <summary>分步演示：每个阶段开始前暂停，点击“下一步”继续。</summary>
        Step,
        /// <summary>手动遥控。</summary>
        Manual
    }
}
