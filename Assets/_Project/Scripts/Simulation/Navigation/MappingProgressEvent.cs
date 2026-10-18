using RivetSim.Core.Events;

namespace RivetSim.Simulation.Navigation
{
    /// <summary>
    /// 建图进度事件：沿巡游路线的完成比例与已观测格数。
    /// </summary>
    public readonly struct MappingProgressEvent : ISimEvent
    {
        /// <summary>完成比例（0～1）。</summary>
        public readonly float Progress;

        /// <summary>已观测格数。</summary>
        public readonly int KnownCellCount;

        /// <summary>已行驶里程，单位 m。</summary>
        public readonly float TraveledM;

        /// <summary>创建事件。</summary>
        public MappingProgressEvent(float progress, int knownCellCount, float traveledM)
        {
            Progress = progress;
            KnownCellCount = knownCellCount;
            TraveledM = traveledM;
        }
    }
}
