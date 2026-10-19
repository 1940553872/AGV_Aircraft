using RivetSim.Core.TaskMatrix;

namespace RivetSim.Simulation.Mission
{
    /// <summary>
    /// 纯逻辑执行器的调用参数：单元定义与当前位置。
    /// </summary>
    public readonly struct CellDefCursor
    {
        /// <summary>单元定义。</summary>
        public readonly CellDef Cell;

        /// <summary>当前位置。</summary>
        public readonly MissionCursor Cursor;

        /// <summary>创建参数。</summary>
        public CellDefCursor(CellDef cell, MissionCursor cursor)
        {
            Cell = cell;
            Cursor = cursor;
        }
    }
}
