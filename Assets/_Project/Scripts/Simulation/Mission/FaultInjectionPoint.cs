namespace RivetSim.Simulation.Mission
{
    /// <summary>
    /// 计划注入点：进入指定阶段（首次执行）且位置匹配时布设一次故障；序号为 −1 表示不限。
    /// </summary>
    public readonly struct FaultInjectionPoint
    {
        /// <summary>故障类别。</summary>
        public readonly FaultKind Kind;

        /// <summary>阶段代号（T1～T6）。</summary>
        public readonly string PhaseCode;

        /// <summary>区域序号（从 0 起，−1 不限）。</summary>
        public readonly int RegionIndex;

        /// <summary>工位全局序号（从 0 起，−1 不限）。</summary>
        public readonly int StationGlobalIndex;

        /// <summary>工位内铆钉序号（从 0 起，−1 不限）。</summary>
        public readonly int RivetIndex;

        /// <summary>创建注入点。</summary>
        public FaultInjectionPoint(FaultKind kind, string phaseCode, int regionIndex, int stationGlobalIndex, int rivetIndex)
        {
            Kind = kind;
            PhaseCode = phaseCode;
            RegionIndex = regionIndex;
            StationGlobalIndex = stationGlobalIndex;
            RivetIndex = rivetIndex;
        }

        /// <inheritdoc />
        public override string ToString() =>
            $"{ConditionBus.Describe(Kind)}（{PhaseCode}{(RegionIndex >= 0 ? $"，区域 {RegionIndex + 1}" : "")}" +
            $"{(StationGlobalIndex >= 0 ? $"，第 {StationGlobalIndex + 1} 工位" : "")}{(RivetIndex >= 0 ? $"，第 {RivetIndex + 1} 颗" : "")}）";
    }
}
