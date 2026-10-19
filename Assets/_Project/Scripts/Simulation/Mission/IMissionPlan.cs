namespace RivetSim.Simulation.Mission
{
    /// <summary>
    /// 任务规模：区域数、各区域工位数、各工位铆钉数（主场景由检测区登记表提供，测试用假数据）。
    /// </summary>
    public interface IMissionPlan
    {
        /// <summary>区域数。</summary>
        int RegionCount { get; }

        /// <summary>区域内工位数。</summary>
        int StationCount(int regionIndex);

        /// <summary>工位内铆钉数。</summary>
        int RivetCount(int regionIndex, int stationIndex);
    }
}
