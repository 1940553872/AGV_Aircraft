namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 底盘运动状态（只读），供 <see cref="SafetyMonitor"/> 判定联锁。
    /// </summary>
    public interface IChassisState
    {
        /// <summary>是否处于零速锁定。</summary>
        bool IsZeroSpeedLocked { get; }

        /// <summary>线速度与角速度是否都为 0。</summary>
        bool IsStationary { get; }
    }
}
