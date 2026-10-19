namespace RivetSim.Core.Config
{
    /// <summary>
    /// 主场景启动入口（M5）：Mission 按 SOP 任务矩阵运行完整工序；SubsystemDemo 为 M3 / M4 的子系统演示（仅调试用，不再维护）。
    /// </summary>
    public enum MissionEntry
    {
        /// <summary>SOP 任务矩阵完整工序（默认）。</summary>
        Mission,
        /// <summary>M3 / M4 子系统演示（NavigationDemoRoutine）。</summary>
        SubsystemDemo
    }
}
