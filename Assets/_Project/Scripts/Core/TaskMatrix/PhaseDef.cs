using Newtonsoft.Json;

namespace RivetSim.Core.TaskMatrix
{
    /// <summary>
    /// 工序阶段（矩阵的行 p1～p6，如 T1 全局导航）。
    /// </summary>
    public sealed class PhaseDef
    {
        /// <summary>行 id，如 "p1"。</summary>
        [JsonProperty("id")] public string Id { get; private set; }

        /// <summary>阶段代号，如 "T1"。</summary>
        [JsonProperty("code")] public string Code { get; private set; }

        /// <summary>阶段名称，如 "全局导航"。</summary>
        [JsonProperty("name")] public string Name { get; private set; }

        /// <summary>v1 原始名称（v1.1 起显示文字优化，原文保留可追溯）；未改名时为空。</summary>
        [JsonProperty("nameOriginal")] public string NameOriginal { get; private set; }
    }
}
