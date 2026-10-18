namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 升降柱状态（只读），供 <see cref="SafetyMonitor"/> 判定联锁。
    /// </summary>
    public interface ILiftState
    {
        /// <summary>当前伸出量，单位 m（0 = 最低位）。</summary>
        float ExtensionM { get; }

        /// <summary>是否正在运动。</summary>
        bool IsMoving { get; }

        /// <summary>是否停在最低位（伸出量在到位精度内且未运动）。</summary>
        bool IsAtBottom { get; }
    }
}
