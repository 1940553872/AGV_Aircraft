using RivetSim.Core.Events;

namespace RivetSim.Simulation.Navigation
{
    /// <summary>
    /// 导航完成事件（成功或失败）。
    /// </summary>
    public readonly struct NavigationCompletedEvent : ISimEvent
    {
        /// <summary>导航用途说明（如“充电位 → R1-S1”）。</summary>
        public readonly string Label;

        /// <summary>导航结果。</summary>
        public readonly NavigationResult Result;

        /// <summary>创建事件。</summary>
        public NavigationCompletedEvent(string label, NavigationResult result)
        {
            Label = label;
            Result = result;
        }
    }
}
