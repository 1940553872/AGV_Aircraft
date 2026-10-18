using System.Collections.Generic;
using Newtonsoft.Json;

namespace RivetSim.Core.TaskMatrix
{
    /// <summary>
    /// 条件集合（对应 JSON 的 <c>conditions</c>）。
    /// </summary>
    public sealed class ConditionSet
    {
        [JsonProperty("items")] private List<ConditionItem> _items = new List<ConditionItem>();

        /// <summary>版本说明。</summary>
        [JsonProperty("version")] public string Version { get; private set; }

        /// <summary>条件列表。</summary>
        public IReadOnlyList<ConditionItem> Items => _items;
    }
}
