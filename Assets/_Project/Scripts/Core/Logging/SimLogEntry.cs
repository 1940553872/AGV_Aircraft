namespace RivetSim.Core.Logging
{
    /// <summary>
    /// 一条仿真日志，供演示屏日志区显示。
    /// </summary>
    public readonly struct SimLogEntry
    {
        /// <summary>记录时的仿真时间（秒）；未接入仿真时钟时为 0。</summary>
        public readonly double SimTimeS;

        /// <summary>类别。</summary>
        public readonly LogCategory Category;

        /// <summary>级别。</summary>
        public readonly SimLogLevel Level;

        /// <summary>正文。</summary>
        public readonly string Message;

        /// <summary>创建日志条目。</summary>
        public SimLogEntry(double simTimeS, LogCategory category, SimLogLevel level, string message)
        {
            SimTimeS = simTimeS;
            Category = category;
            Level = level;
            Message = message;
        }
    }
}
