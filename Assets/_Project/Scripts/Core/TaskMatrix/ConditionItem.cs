using Newtonsoft.Json;

namespace RivetSim.Core.TaskMatrix
{
    /// <summary>
    /// 判定条件 C1～C12（v1 为固定文字与教学示例阈值）。
    /// </summary>
    public sealed class ConditionItem
    {
        /// <summary>条件 id，如 "C3"。</summary>
        [JsonProperty("id")] public string Id { get; private set; }

        /// <summary>所属阶段说明，如 "T5 贴合采集"。</summary>
        [JsonProperty("stage")] public string Stage { get; private set; }

        /// <summary>显示文字。</summary>
        [JsonProperty("text")] public string Text { get; private set; }

        /// <summary>判定表达式说明（供执行器实现参考）。</summary>
        [JsonProperty("check")] public string Check { get; private set; }
    }
}
