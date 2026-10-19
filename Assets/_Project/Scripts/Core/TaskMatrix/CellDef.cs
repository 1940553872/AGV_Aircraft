using System.Collections.Generic;
using Newtonsoft.Json;

namespace RivetSim.Core.TaskMatrix
{
    /// <summary>
    /// 任务矩阵单元 t(i,j)：第 i 阶段、第 j 主体的任务定义。
    /// </summary>
    public sealed class CellDef
    {
        [JsonProperty("conditionRefs")] private List<string> _conditionRefs = new List<string>();

        /// <summary>单元 id，如 "t(5,4)"。</summary>
        [JsonProperty("id")] public string Id { get; private set; }

        /// <summary>行号（阶段序号，从 1 开始）。</summary>
        [JsonProperty("row")] public int Row { get; private set; }

        /// <summary>列号（主体序号，从 1 开始）。</summary>
        [JsonProperty("col")] public int Col { get; private set; }

        /// <summary>单元类型。</summary>
        [JsonProperty("kind")] public CellKind Kind { get; private set; }

        /// <summary>显示文字（可含换行），以用户给定为准，不得擅自改写（v1.1 起按 M8_label_update.json 优化）。</summary>
        [JsonProperty("label")] public string Label { get; private set; }

        /// <summary>v1 原始显示文字（v1.1 起显示文字优化，原文保留可追溯）；未改写时为空。</summary>
        [JsonProperty("labelOriginal")] public string LabelOriginal { get; private set; }

        /// <summary>执行器键名，如 "nav.globalNavigate"；standby 单元为空。</summary>
        [JsonProperty("executor")] public string Executor { get; private set; }

        /// <summary>“当前执行情况说明”使用的解释文字。</summary>
        [JsonProperty("explain")] public string Explain { get; private set; }

        /// <summary>条件单元的触发源键名，如 "fault.contactForceExceeded"。</summary>
        [JsonProperty("trigger")] public string Trigger { get; private set; }

        /// <summary>条件单元完成后的动作。</summary>
        [JsonProperty("onComplete")] public CellCompletionAction OnComplete { get; private set; }

        /// <summary>重试阶段的最大次数（仅 <see cref="CellCompletionAction.RetryPhase"/> 有效）。</summary>
        [JsonProperty("maxRetries")] public int MaxRetries { get; private set; }

        /// <summary>active 单元的执行条件键名，如 "lastRivetInStation"；为空表示总是执行。</summary>
        [JsonProperty("runWhen")] public string RunWhen { get; private set; }

        /// <summary>备注。</summary>
        [JsonProperty("note")] public string Note { get; private set; }

        /// <summary>是否为参考图中的示例单元（粗实线）。</summary>
        [JsonProperty("exampleCell")] public bool IsExampleCell { get; private set; }

        /// <summary>需逐项判定的条件 id 列表，如 C2～C8。</summary>
        public IReadOnlyList<string> ConditionRefs => _conditionRefs;
    }
}
