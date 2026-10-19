using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using RivetSim.Core.Events;
using RivetSim.Core.Logging;
using RivetSim.Core.Mission;
using RivetSim.Core.TaskMatrix;

namespace RivetSim.Simulation.Mission
{
    /// <summary>
    /// 条件 C1～C12 的阈值与上报：阈值从 JSON conditions[].check 中按出现顺序解析出的数字读取（教学示例阈值），
    /// 判定量由执行器从仿真状态计算；每项结果发布 <see cref="ConditionCheckedEvent"/> 并写日志。
    /// </summary>
    public sealed class ConditionEvaluator
    {
        private static readonly Regex Number = new Regex(@"-?\d+(\.\d+)?", RegexOptions.CultureInvariant);

        private readonly Dictionary<string, ConditionItem> _items = new Dictionary<string, ConditionItem>(StringComparer.Ordinal);
        private readonly Dictionary<string, float[]> _numbers = new Dictionary<string, float[]>(StringComparer.Ordinal);
        private readonly EventBus _bus;

        /// <summary>本次运行的判定次数。</summary>
        public int CheckCount { get; private set; }

        /// <summary>本次运行未满足的判定次数。</summary>
        public int FailedCount { get; private set; }

        /// <summary>从 JSON conditions 创建。</summary>
        public ConditionEvaluator(ConditionSet conditions, EventBus bus)
        {
            _bus = bus;
            if (conditions == null) return;
            foreach (ConditionItem item in conditions.Items)
            {
                if (item == null || string.IsNullOrEmpty(item.Id)) continue;
                _items[item.Id] = item;
                _numbers[item.Id] = ParseNumbers(item.Check);
            }
        }

        /// <summary>条件文字（未找到时为空）。</summary>
        public string TextOf(string conditionId) => _items.TryGetValue(conditionId, out ConditionItem item) ? item.Text : "";

        /// <summary>check 表达式中第 index 个数字（从 0 起）；缺失时记错误并返回 NaN。</summary>
        public float Threshold(string conditionId, int index)
        {
            if (_numbers.TryGetValue(conditionId, out float[] list) && index >= 0 && index < list.Length) return list[index];
            SimLog.Error(LogCategory.Mission, $"条件 {conditionId} 的 check 表达式缺少第 {index + 1} 个阈值");
            return float.NaN;
        }

        /// <summary>上报一项判定，返回 passed。</summary>
        public bool Report(string cellId, string conditionId, bool passed, float value, string valueText)
        {
            CheckCount++;
            if (!passed) FailedCount++;
            _bus?.Publish(new ConditionCheckedEvent(cellId, conditionId, passed, value, valueText ?? ""));
            string line = $"[条件] {cellId} {conditionId} {TextOf(conditionId)}：{(passed ? "✓" : "✗")}（{valueText}）";
            if (passed) SimLog.Info(LogCategory.Mission, line);
            else SimLog.Warning(LogCategory.Mission, line);
            return passed;
        }

        /// <summary>按出现顺序解析表达式中的数字。</summary>
        public static float[] ParseNumbers(string check)
        {
            if (string.IsNullOrEmpty(check)) return Array.Empty<float>();
            MatchCollection matches = Number.Matches(check);
            var values = new float[matches.Count];
            for (int i = 0; i < matches.Count; i++)
                values[i] = float.Parse(matches[i].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
            return values;
        }
    }
}
