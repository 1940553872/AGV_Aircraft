namespace RivetSim.Simulation.Navigation
{
    /// <summary>
    /// 占据栅格单元状态。
    /// </summary>
    public enum OccupancyState : byte
    {
        /// <summary>未观测。</summary>
        Unknown,

        /// <summary>空闲（激光射线经过）。</summary>
        Free,

        /// <summary>占据（激光命中）。</summary>
        Occupied
    }
}
