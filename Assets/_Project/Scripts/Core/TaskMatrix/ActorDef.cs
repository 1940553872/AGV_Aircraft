using Newtonsoft.Json;

namespace RivetSim.Core.TaskMatrix
{
    /// <summary>
    /// 任务主体（矩阵的列 a1～a8，如 S1 导航）。
    /// </summary>
    public sealed class ActorDef
    {
        /// <summary>列 id，如 "a1"。</summary>
        [JsonProperty("id")] public string Id { get; private set; }

        /// <summary>主体代号，如 "S1"。</summary>
        [JsonProperty("code")] public string Code { get; private set; }

        /// <summary>主体名称，如 "导航"。</summary>
        [JsonProperty("name")] public string Name { get; private set; }

        /// <summary>对应设备说明。</summary>
        [JsonProperty("device")] public string Device { get; private set; }
    }
}
