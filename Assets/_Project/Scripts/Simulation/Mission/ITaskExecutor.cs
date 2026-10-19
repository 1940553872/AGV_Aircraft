using System.Threading;
using RivetSim.Core.TaskMatrix;
using UnityEngine;

namespace RivetSim.Simulation.Mission
{
    /// <summary>
    /// 任务矩阵单元执行器：JSON 中的 executor 键名经 <see cref="ExecutorRegistry"/> 映射到实现。
    /// </summary>
    public interface ITaskExecutor
    {
        /// <summary>执行单元；取消时抛出 OperationCanceledException。</summary>
        Awaitable<ExecutorResult> ExecuteAsync(CellDef cell, MissionCursor cursor, CancellationToken ct);
    }
}
