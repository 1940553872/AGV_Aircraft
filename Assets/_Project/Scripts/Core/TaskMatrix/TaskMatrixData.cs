using System.Collections.Generic;
using Newtonsoft.Json;

namespace RivetSim.Core.TaskMatrix
{
    /// <summary>
    /// SOP 任务矩阵数据模型（SOP = (P, A, T)），由 <see cref="TaskMatrixLoader"/> 从 JSON 读取。
    /// 单元文字、类型、回路一律以 JSON 为准，代码不写死流程。
    /// </summary>
    public sealed class TaskMatrixData
    {
        [JsonProperty("actors")] private List<ActorDef> _actors = new List<ActorDef>();
        [JsonProperty("phases")] private List<PhaseDef> _phases = new List<PhaseDef>();
        [JsonProperty("cells")] private List<CellDef> _cells = new List<CellDef>();
        [JsonProperty("loops")] private List<LoopDef> _loops = new List<LoopDef>();
        [JsonProperty("legend")] private List<LegendItem> _legend = new List<LegendItem>();
        [JsonProperty("rules")] private Dictionary<string, string> _rules = new Dictionary<string, string>();

        private Dictionary<(int Row, int Col), CellDef> _cellByPosition;
        private Dictionary<string, CellDef> _cellById;

        /// <summary>数据格式标识，如 "RivetSim.TaskMatrix/1"。</summary>
        [JsonProperty("schema")] public string Schema { get; private set; }

        /// <summary>矩阵 id。</summary>
        [JsonProperty("id")] public string Id { get; private set; }

        /// <summary>标题。</summary>
        [JsonProperty("title")] public string Title { get; private set; }

        /// <summary>记号说明，如 "SOP = (P, A, T)"。</summary>
        [JsonProperty("notation")] public string Notation { get; private set; }

        /// <summary>参考图路径。</summary>
        [JsonProperty("source")] public string Source { get; private set; }

        /// <summary>回路说明文字（图例下方）。</summary>
        [JsonProperty("loopNote")] public string LoopNote { get; private set; }

        /// <summary>判定条件 C1～C12。</summary>
        [JsonProperty("conditions")] public ConditionSet Conditions { get; private set; } = new ConditionSet();

        /// <summary>主体（列），按 a1～a8 顺序。</summary>
        public IReadOnlyList<ActorDef> Actors => _actors;

        /// <summary>阶段（行），按 p1～p6 顺序。</summary>
        public IReadOnlyList<PhaseDef> Phases => _phases;

        /// <summary>全部单元。</summary>
        public IReadOnlyList<CellDef> Cells => _cells;

        /// <summary>回路。</summary>
        public IReadOnlyList<LoopDef> Loops => _loops;

        /// <summary>图例。</summary>
        public IReadOnlyList<LegendItem> Legend => _legend;

        /// <summary>执行语义说明文字（键 → 文字）。</summary>
        public IReadOnlyDictionary<string, string> Rules => _rules;

        /// <summary>行数（阶段数）。</summary>
        public int RowCount => _phases.Count;

        /// <summary>列数（主体数）。</summary>
        public int ColCount => _actors.Count;

        /// <summary>按行列取单元（从 1 开始），不存在时返回 null。</summary>
        public CellDef GetCell(int row, int col)
        {
            EnsureIndex();
            return _cellByPosition.TryGetValue((row, col), out CellDef cell) ? cell : null;
        }

        /// <summary>按 id 取单元，如 "t(5,4)"；不存在时返回 null。</summary>
        public CellDef FindCell(string id)
        {
            EnsureIndex();
            return id != null && _cellById.TryGetValue(id, out CellDef cell) ? cell : null;
        }

        /// <summary>按 id 取阶段，如 "p4"；不存在时返回 null。</summary>
        public PhaseDef FindPhase(string id) => _phases.Find(p => p != null && p.Id == id);

        /// <summary>按 id 取主体，如 "a4"；不存在时返回 null。</summary>
        public ActorDef FindActor(string id) => _actors.Find(a => a != null && a.Id == id);

        /// <summary>按 id 取条件，如 "C3"；不存在时返回 null。</summary>
        public ConditionItem FindCondition(string id)
        {
            foreach (ConditionItem item in Conditions.Items)
            {
                if (item != null && item.Id == id) return item;
            }
            return null;
        }

        /// <summary>阶段 id 对应的行号（从 1 开始），不存在时返回 0。</summary>
        public int RowOfPhase(string phaseId) => _phases.FindIndex(p => p != null && p.Id == phaseId) + 1;

        /// <summary>JSON 中出现的全部执行器键名（去重，按首次出现顺序）。</summary>
        public IReadOnlyList<string> GetExecutorKeys()
        {
            var keys = new List<string>();
            var seen = new HashSet<string>();
            foreach (CellDef cell in _cells)
            {
                if (cell != null && !string.IsNullOrEmpty(cell.Executor) && seen.Add(cell.Executor)) keys.Add(cell.Executor);
            }
            return keys;
        }

        private void EnsureIndex()
        {
            if (_cellByPosition != null) return;
            _cellByPosition = new Dictionary<(int, int), CellDef>();
            _cellById = new Dictionary<string, CellDef>();
            foreach (CellDef cell in _cells)
            {
                if (cell == null) continue;
                _cellByPosition.TryAdd((cell.Row, cell.Col), cell);
                if (cell.Id != null) _cellById.TryAdd(cell.Id, cell);
            }
        }
    }
}
