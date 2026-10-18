using Newtonsoft.Json;

namespace RivetSim.Core.TaskMatrix
{
    /// <summary>
    /// 回路定义（如 T6→T4“下一颗”、T6→T2 / T1“换站”）。
    /// </summary>
    public sealed class LoopDef
    {
        /// <summary>回路 id，如 "nextRivet"。</summary>
        [JsonProperty("id")] public string Id { get; private set; }

        /// <summary>箭头线型。</summary>
        [JsonProperty("style")] public LoopStyle Style { get; private set; }

        /// <summary>箭头文字。</summary>
        [JsonProperty("label")] public string Label { get; private set; }

        /// <summary>起点阶段 id。</summary>
        [JsonProperty("from")] public string From { get; private set; }

        /// <summary>终点阶段 id。</summary>
        [JsonProperty("to")] public string To { get; private set; }

        /// <summary>走该回路的条件键名，如 "station.hasNextRivet"。</summary>
        [JsonProperty("when")] public string When { get; private set; }

        /// <summary>说明文字。</summary>
        [JsonProperty("explain")] public string Explain { get; private set; }
    }
}
