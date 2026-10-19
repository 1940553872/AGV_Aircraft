using System;
using System.Collections.Generic;

namespace RivetSim.Simulation.Mission
{
    /// <summary>
    /// 执行器注册表：executor 键名 → 实现。启动时用 <see cref="FindMissing"/> 与 TaskMatrixData.GetExecutorKeys() 核对。
    /// </summary>
    public sealed class ExecutorRegistry
    {
        private readonly Dictionary<string, ITaskExecutor> _executors = new Dictionary<string, ITaskExecutor>(StringComparer.Ordinal);

        /// <summary>已注册的键名数。</summary>
        public int Count => _executors.Count;

        /// <summary>已注册的键名。</summary>
        public IEnumerable<string> Keys => _executors.Keys;

        /// <summary>注册（同名覆盖）。</summary>
        public void Register(string key, ITaskExecutor executor)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("执行器键名为空", nameof(key));
            _executors[key] = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        /// <summary>按键名查找。</summary>
        public bool TryGet(string key, out ITaskExecutor executor) => _executors.TryGetValue(key ?? "", out executor);

        /// <summary>返回 keys 中未注册的键名。</summary>
        public List<string> FindMissing(IEnumerable<string> keys)
        {
            var missing = new List<string>();
            foreach (string key in keys)
                if (!_executors.ContainsKey(key)) missing.Add(key);
            return missing;
        }
    }
}
