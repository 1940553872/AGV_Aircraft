using RivetSim.Core.Events;

namespace RivetSim.Simulation.Navigation
{
    /// <summary>
    /// 对接停靠完成事件（成功或失败），含停靠误差。
    /// </summary>
    public readonly struct DockingCompletedEvent : ISimEvent
    {
        /// <summary>对接结果。</summary>
        public readonly DockingResult Result;

        /// <summary>创建事件。</summary>
        public DockingCompletedEvent(DockingResult result)
        {
            Result = result;
        }
    }
}
