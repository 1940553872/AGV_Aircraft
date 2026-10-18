namespace RivetSim.Core.Logging
{
    /// <summary>
    /// 日志类别，决定演示屏日志的着色与筛选。
    /// </summary>
    public enum LogCategory
    {
        /// <summary>任务编排。</summary>
        Mission,
        /// <summary>导航与底盘。</summary>
        Nav,
        /// <summary>机械臂。</summary>
        Arm,
        /// <summary>超声检测。</summary>
        UT,
        /// <summary>安全监控。</summary>
        Safety,
        /// <summary>系统与工具。</summary>
        System
    }
}
