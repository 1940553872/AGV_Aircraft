using RivetSim.Core.Events;

namespace RivetSim.Core.Mission
{
    /// <summary>
    /// 任务矩阵单元状态变化事件（M5，MissionOrchestrator 发布；演示屏、设备指示灯与报告订阅）。
    /// </summary>
    public readonly struct CellStateChangedEvent : ISimEvent
    {
        /// <summary>单元 id，如 t(5,4)。</summary>
        public readonly string CellId;

        /// <summary>所属主体 id（a1～a8）。</summary>
        public readonly string ActorId;

        /// <summary>所属阶段 id（p1～p6）。</summary>
        public readonly string PhaseId;

        /// <summary>新状态。</summary>
        public readonly CellState State;

        /// <summary>原状态。</summary>
        public readonly CellState Previous;

        /// <summary>当前区域序号（从 0 起）。</summary>
        public readonly int RegionIndex;

        /// <summary>当前工位序号（区域内，从 0 起）。</summary>
        public readonly int StationIndex;

        /// <summary>当前铆钉序号（工位内，从 0 起）。</summary>
        public readonly int RivetIndex;

        /// <summary>仿真时间，单位 s。</summary>
        public readonly double TimeS;

        /// <summary>说明（失败原因、跳过原因等，可为空）。</summary>
        public readonly string Note;

        /// <summary>创建事件。</summary>
        public CellStateChangedEvent(string cellId, string actorId, string phaseId, CellState state, CellState previous,
            int regionIndex, int stationIndex, int rivetIndex, double timeS, string note)
        {
            CellId = cellId;
            ActorId = actorId;
            PhaseId = phaseId;
            State = state;
            Previous = previous;
            RegionIndex = regionIndex;
            StationIndex = stationIndex;
            RivetIndex = rivetIndex;
            TimeS = timeS;
            Note = note;
        }
    }
}
