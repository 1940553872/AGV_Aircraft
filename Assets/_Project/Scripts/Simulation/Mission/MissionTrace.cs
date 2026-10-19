using System;
using System.Collections.Generic;
using RivetSim.Core.Events;
using RivetSim.Core.Mission;

namespace RivetSim.Simulation.Mission
{
    /// <summary>
    /// 运行记录（报告与验收用）：单元状态变化、条件判定与回路事件按发生顺序保存。
    /// </summary>
    public sealed class MissionTrace : IDisposable
    {
        private readonly List<CellStateChangedEvent> _cells = new List<CellStateChangedEvent>();
        private readonly List<ConditionCheckedEvent> _conditions = new List<ConditionCheckedEvent>();
        private readonly List<LoopTakenEvent> _loops = new List<LoopTakenEvent>();
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();

        /// <summary>单元状态变化。</summary>
        public IReadOnlyList<CellStateChangedEvent> CellChanges => _cells;

        /// <summary>条件判定。</summary>
        public IReadOnlyList<ConditionCheckedEvent> ConditionChecks => _conditions;

        /// <summary>回路事件。</summary>
        public IReadOnlyList<LoopTakenEvent> Loops => _loops;

        /// <summary>订阅事件总线。</summary>
        public MissionTrace(EventBus bus)
        {
            _subscriptions.Add(bus.Subscribe<CellStateChangedEvent>(e => _cells.Add(e)));
            _subscriptions.Add(bus.Subscribe<ConditionCheckedEvent>(e => _conditions.Add(e)));
            _subscriptions.Add(bus.Subscribe<LoopTakenEvent>(e => _loops.Add(e)));
        }

        /// <summary>取消订阅。</summary>
        public void Dispose()
        {
            foreach (IDisposable s in _subscriptions) s.Dispose();
            _subscriptions.Clear();
        }
    }
}
