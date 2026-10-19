using RivetSim.Core.Events;

namespace RivetSim.Core.Mission
{
    /// <summary>
    /// 进入任务矩阵阶段事件（M5）：每次进入阶段（含重试）发布一次，故障注入计划与演示界面订阅。
    /// </summary>
    public readonly struct PhaseEnteredEvent : ISimEvent
    {
        /// <summary>阶段 id（p1～p6）。</summary>
        public readonly string PhaseId;

        /// <summary>阶段代号（T1～T6）。</summary>
        public readonly string PhaseCode;

        /// <summary>区域序号（从 0 起）。</summary>
        public readonly int RegionIndex;

        /// <summary>工位序号（区域内，从 0 起）。</summary>
        public readonly int StationIndex;

        /// <summary>工位全局序号（跨区域累计，从 0 起）。</summary>
        public readonly int StationGlobalIndex;

        /// <summary>铆钉序号（工位内，从 0 起）。</summary>
        public readonly int RivetIndex;

        /// <summary>本阶段第几次执行（从 1 起，重试时递增）。</summary>
        public readonly int Attempt;

        /// <summary>创建事件。</summary>
        public PhaseEnteredEvent(string phaseId, string phaseCode, int regionIndex, int stationIndex, int stationGlobalIndex, int rivetIndex, int attempt)
        {
            PhaseId = phaseId;
            PhaseCode = phaseCode;
            RegionIndex = regionIndex;
            StationIndex = stationIndex;
            StationGlobalIndex = stationGlobalIndex;
            RivetIndex = rivetIndex;
            Attempt = attempt;
        }
    }
}
