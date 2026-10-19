using System;
using System.Threading;
using RivetSim.Core.TaskMatrix;
using UnityEngine;

namespace RivetSim.Simulation.Mission
{
    /// <summary>
    /// 以委托实现的执行器（各主体的执行器注册类用它把键名映射到具体动作）。
    /// </summary>
    public sealed class DelegateExecutor : ITaskExecutor
    {
        private readonly Func<CellDef, MissionCursor, CancellationToken, Awaitable<ExecutorResult>> _run;

        /// <summary>创建执行器。</summary>
        public DelegateExecutor(Func<CellDef, MissionCursor, CancellationToken, Awaitable<ExecutorResult>> run) =>
            _run = run ?? throw new ArgumentNullException(nameof(run));

        /// <inheritdoc />
        public Awaitable<ExecutorResult> ExecuteAsync(CellDef cell, MissionCursor cursor, CancellationToken ct) => _run(cell, cursor, ct);
    }
}
