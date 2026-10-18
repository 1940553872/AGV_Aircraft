namespace RivetSim.Simulation.World
{
    /// <summary>
    /// 检测区域所在的机身侧（机头朝 +Z 时，左侧在 −X）。
    /// </summary>
    public enum RegionSide
    {
        /// <summary>机身左侧（−X）。</summary>
        Left,
        /// <summary>机身右侧（+X）。</summary>
        Right,
    }
}
