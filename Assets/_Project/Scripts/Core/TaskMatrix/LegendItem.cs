using Newtonsoft.Json;

namespace RivetSim.Core.TaskMatrix
{
    /// <summary>
    /// 图例条目（矩阵下方的样式说明）。
    /// </summary>
    public sealed class LegendItem
    {
        /// <summary>样式键，如 "exampleBold"、"dashed"、"grayText"。</summary>
        [JsonProperty("style")] public string Style { get; private set; }

        /// <summary>图例文字。</summary>
        [JsonProperty("text")] public string Text { get; private set; }
    }
}
