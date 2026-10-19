using System;
using System.Collections.Generic;
using RivetSim.Core.Events;
using RivetSim.Core.Mission;

namespace RivetSim.Simulation.Mission
{
    /// <summary>
    /// 按计划自动注入故障（Verify 菜单用）：订阅 <see cref="PhaseEnteredEvent"/>，首次进入匹配的阶段时调用 <see cref="ConditionBus.Inject"/>，每个注入点只触发一次。
    /// </summary>
    public sealed class ScheduledFaultInjector : IDisposable
    {
        private readonly List<FaultInjectionPoint> _remaining;
        private readonly ConditionBus _conditions;
        private readonly IDisposable _subscription;

        /// <summary>尚未触发的注入点数。</summary>
        public int RemainingCount => _remaining.Count;

        /// <summary>创建并订阅。</summary>
        public ScheduledFaultInjector(EventBus bus, ConditionBus conditions, IEnumerable<FaultInjectionPoint> points)
        {
            _conditions = conditions;
            _remaining = new List<FaultInjectionPoint>(points);
            _subscription = bus.Subscribe<PhaseEnteredEvent>(OnPhaseEntered);
        }

        /// <summary>取消订阅。</summary>
        public void Dispose() => _subscription?.Dispose();

        private void OnPhaseEntered(PhaseEnteredEvent e)
        {
            if (e.Attempt != 1) return;
            for (int i = _remaining.Count - 1; i >= 0; i--)
            {
                FaultInjectionPoint p = _remaining[i];
                if (p.PhaseCode != e.PhaseCode) continue;
                if (p.RegionIndex >= 0 && p.RegionIndex != e.RegionIndex) continue;
                if (p.StationGlobalIndex >= 0 && p.StationGlobalIndex != e.StationGlobalIndex) continue;
                if (p.RivetIndex >= 0 && p.RivetIndex != e.RivetIndex) continue;
                _remaining.RemoveAt(i);
                _conditions.Inject(p.Kind);
            }
        }
    }
}
