using System;
using System.Collections.Generic;
using RivetSim.Core.Events;
using RivetSim.Core.Mission;
using RivetSim.Core.TaskMatrix;

namespace RivetSim.Simulation.Mission
{
    /// <summary>
    /// 任务矩阵运行状态（Docs/Architecture.md §5.2）：记录每个单元的 <see cref="CellState"/>，状态变化时发布 <see cref="CellStateChangedEvent"/>。
    /// 进入阶段时 standby → Standby、conditional → Armed、active → Idle；回路回到某阶段时，该阶段及其后各行重置。
    /// </summary>
    public sealed class TaskMatrixRuntime
    {
        private readonly Dictionary<string, CellState> _states = new Dictionary<string, CellState>(StringComparer.Ordinal);
        private readonly List<CellDef>[] _rows;
        private readonly EventBus _bus;
        private readonly Func<double> _timeS;

        /// <summary>任务矩阵数据。</summary>
        public TaskMatrixData Data { get; }

        /// <summary>当前执行位置（写入事件）。</summary>
        public MissionCursor Cursor { get; }

        /// <summary>创建运行状态，全部单元置为初始状态。</summary>
        public TaskMatrixRuntime(TaskMatrixData data, MissionCursor cursor, EventBus bus, Func<double> timeS)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Cursor = cursor ?? throw new ArgumentNullException(nameof(cursor));
            _bus = bus;
            _timeS = timeS ?? (() => 0d);
            _rows = new List<CellDef>[data.RowCount + 1];
            for (int r = 0; r <= data.RowCount; r++) _rows[r] = new List<CellDef>();
            foreach (CellDef cell in data.Cells)
            {
                if (cell == null || cell.Row < 1 || cell.Row > data.RowCount) continue;
                _rows[cell.Row].Add(cell);
                _states[cell.Id] = InitialState(cell);
            }
            foreach (List<CellDef> row in _rows) row.Sort((a, b) => a.Col.CompareTo(b.Col));
        }

        /// <summary>某一行（阶段，从 1 起）的单元，按列排序。</summary>
        public IReadOnlyList<CellDef> CellsInRow(int row) => row >= 1 && row < _rows.Length ? _rows[row] : (IReadOnlyList<CellDef>)Array.Empty<CellDef>();

        /// <summary>单元当前状态。</summary>
        public CellState GetState(string cellId) => _states.TryGetValue(cellId ?? "", out CellState s) ? s : CellState.Idle;

        /// <summary>单元所属主体 id。</summary>
        public string ActorIdOf(CellDef cell) => cell.Col >= 1 && cell.Col <= Data.ColCount ? Data.Actors[cell.Col - 1].Id : "";

        /// <summary>单元所属主体代号（S1～S8）。</summary>
        public string ActorCodeOf(CellDef cell) => cell.Col >= 1 && cell.Col <= Data.ColCount ? Data.Actors[cell.Col - 1].Code : "";

        /// <summary>单元所属阶段 id。</summary>
        public string PhaseIdOf(CellDef cell) => cell.Row >= 1 && cell.Row <= Data.RowCount ? Data.Phases[cell.Row - 1].Id : "";

        /// <summary>单元所属阶段代号（T1～T6）。</summary>
        public string PhaseCodeOf(CellDef cell) => cell.Row >= 1 && cell.Row <= Data.RowCount ? Data.Phases[cell.Row - 1].Code : "";

        /// <summary>设置状态；与原状态相同时不发布事件。</summary>
        public void SetState(CellDef cell, CellState state, string note = "")
        {
            CellState previous = GetState(cell.Id);
            if (previous == state) return;
            _states[cell.Id] = state;
            _bus?.Publish(new CellStateChangedEvent(cell.Id, ActorIdOf(cell), PhaseIdOf(cell), state, previous,
                Cursor.RegionIndex, Cursor.StationIndex, Cursor.RivetIndex, _timeS(), note ?? ""));
        }

        /// <summary>进入阶段：standby → Standby、conditional → Armed、active → Idle。</summary>
        public void EnterPhase(int row)
        {
            foreach (CellDef cell in CellsInRow(row))
                SetState(cell, cell.Kind == CellKind.Conditional ? CellState.Armed : InitialState(cell));
        }

        /// <summary>把 fromRow 及其后各行重置为初始状态（逐钉循环、换站时调用）。</summary>
        public void ResetRows(int fromRow)
        {
            for (int r = Math.Max(1, fromRow); r < _rows.Length; r++)
                foreach (CellDef cell in _rows[r]) SetState(cell, InitialState(cell));
        }

        private static CellState InitialState(CellDef cell) => cell.Kind == CellKind.Standby ? CellState.Standby : CellState.Idle;
    }
}
