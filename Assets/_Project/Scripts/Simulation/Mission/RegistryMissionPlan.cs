using RivetSim.Simulation.World;

namespace RivetSim.Simulation.Mission
{
    /// <summary>
    /// 由检测区登记表提供的任务规模（区域 → 工位 → 铆钉）。
    /// </summary>
    public sealed class RegistryMissionPlan : IMissionPlan
    {
        private readonly RivetRegistry _registry;

        /// <summary>创建。</summary>
        public RegistryMissionPlan(RivetRegistry registry) => _registry = registry;

        /// <inheritdoc />
        public int RegionCount => _registry != null ? _registry.Regions.Count : 0;

        /// <inheritdoc />
        public int StationCount(int regionIndex) => _registry.Regions[regionIndex].Stations.Count;

        /// <inheritdoc />
        public int RivetCount(int regionIndex, int stationIndex) => _registry.Regions[regionIndex].Stations[stationIndex].Rivets.Count;
    }
}
