namespace RivetSim.Simulation.Mission
{
    /// <summary>
    /// 当前执行位置：区域 / 工位 / 铆钉序号（均从 0 起），由 <see cref="MissionOrchestrator"/> 按回路推进。
    /// </summary>
    public sealed class MissionCursor
    {
        /// <summary>区域序号。</summary>
        public int RegionIndex { get; internal set; }

        /// <summary>工位序号（区域内）。</summary>
        public int StationIndex { get; internal set; }

        /// <summary>工位全局序号（跨区域累计）。</summary>
        public int StationGlobalIndex { get; internal set; }

        /// <summary>铆钉序号（工位内）。</summary>
        public int RivetIndex { get; internal set; }

        /// <summary>本工位铆钉数。</summary>
        public int RivetCount { get; internal set; }

        /// <summary>是否本工位最后一颗铆钉（t(6,1) 的 runWhen）。</summary>
        public bool IsLastRivetInStation => RivetIndex >= RivetCount - 1;

        /// <summary>回到第一个区域的第一颗铆钉。</summary>
        internal void Reset(int rivetCount)
        {
            RegionIndex = 0;
            StationIndex = 0;
            StationGlobalIndex = 0;
            RivetIndex = 0;
            RivetCount = rivetCount;
        }

        /// <inheritdoc />
        public override string ToString() => $"区域 {RegionIndex + 1} 工位 {StationIndex + 1} 铆钉 {RivetIndex + 1}/{RivetCount}";
    }
}
