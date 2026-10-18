using RivetSim.Core.Config;
using RivetSim.Core.Events;

namespace RivetSim.Simulation.Navigation
{
    /// <summary>
    /// 规划路径事件：新规划（或对接车道）的路径与目标位姿，供路径可视化显示。
    /// </summary>
    public readonly struct PathPlannedEvent : ISimEvent
    {
        /// <summary>路径。</summary>
        public readonly PlannedPath Path;

        /// <summary>目标位姿。</summary>
        public readonly PlanarPose Goal;

        /// <summary>是否为对接车道（预停靠位姿 → 停靠位姿）。</summary>
        public readonly bool IsDockingLane;

        /// <summary>创建事件。</summary>
        public PathPlannedEvent(PlannedPath path, PlanarPose goal, bool isDockingLane)
        {
            Path = path;
            Goal = goal;
            IsDockingLane = isDockingLane;
        }
    }
}
