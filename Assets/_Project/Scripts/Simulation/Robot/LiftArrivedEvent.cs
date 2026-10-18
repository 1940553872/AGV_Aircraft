using RivetSim.Core.Events;

namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 升降柱到位事件：伸出量与对应的机械臂安装面离地高度，单位 m。
    /// </summary>
    public readonly struct LiftArrivedEvent : ISimEvent
    {
        /// <summary>到位后的伸出量，单位 m。</summary>
        public readonly float ExtensionM;

        /// <summary>到位后机械臂安装面离地高度，单位 m。</summary>
        public readonly float MountHeightM;

        /// <summary>创建事件。</summary>
        public LiftArrivedEvent(float extensionM, float mountHeightM)
        {
            ExtensionM = extensionM;
            MountHeightM = mountHeightM;
        }
    }
}
