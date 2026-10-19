using RivetSim.Core.Events;

namespace RivetSim.Core.Mission
{
    /// <summary>
    /// 条件判定事件（M5）：t(4,7)、t(5,4)、t(6,4) 按 JSON conditionRefs 逐项判定 C1～C12 后发布；演示屏逐条显示 ✓ / ✗（M6）。
    /// </summary>
    public readonly struct ConditionCheckedEvent : ISimEvent
    {
        /// <summary>判定所在单元 id。</summary>
        public readonly string CellId;

        /// <summary>条件 id（C1～C12）。</summary>
        public readonly string ConditionId;

        /// <summary>是否满足。</summary>
        public readonly bool Passed;

        /// <summary>判定用的仿真量（无数值的布尔条件为 1 / 0）。</summary>
        public readonly float Value;

        /// <summary>带单位的数值说明，如“0.12 mm”。</summary>
        public readonly string ValueText;

        /// <summary>创建事件。</summary>
        public ConditionCheckedEvent(string cellId, string conditionId, bool passed, float value, string valueText)
        {
            CellId = cellId;
            ConditionId = conditionId;
            Passed = passed;
            Value = value;
            ValueText = valueText;
        }
    }
}
