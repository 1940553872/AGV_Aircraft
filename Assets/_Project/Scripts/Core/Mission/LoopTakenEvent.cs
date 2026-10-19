using RivetSim.Core.Events;

namespace RivetSim.Core.Mission
{
    /// <summary>
    /// 走过任务矩阵回路事件（M5）：“下一颗”（T6→T4）或“换站”（→T2 / →T1）；演示屏据此高亮回路箭头（M6）。
    /// </summary>
    public readonly struct LoopTakenEvent : ISimEvent
    {
        /// <summary>回路 id（JSON loops[].id）。</summary>
        public readonly string LoopId;

        /// <summary>回路文字（“下一颗” / “换站”）。</summary>
        public readonly string Label;

        /// <summary>起点阶段 id。</summary>
        public readonly string FromPhaseId;

        /// <summary>目标阶段 id。</summary>
        public readonly string ToPhaseId;

        /// <summary>创建事件。</summary>
        public LoopTakenEvent(string loopId, string label, string fromPhaseId, string toPhaseId)
        {
            LoopId = loopId;
            Label = label;
            FromPhaseId = fromPhaseId;
            ToPhaseId = toPhaseId;
        }
    }
}
