using System.Collections.Generic;

namespace RivetSim.Core.TaskMatrix
{
    /// <summary>
    /// 任务矩阵读取结果：数据 + 校验错误 / 警告。存在错误时不应使用该数据启动流程。
    /// </summary>
    public sealed class TaskMatrixLoadResult
    {
        /// <summary>创建读取结果。</summary>
        public TaskMatrixLoadResult(TaskMatrixData data, IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
        {
            Data = data;
            Errors = errors;
            Warnings = warnings;
        }

        /// <summary>解析得到的数据；JSON 无法解析时为 null。</summary>
        public TaskMatrixData Data { get; }

        /// <summary>校验错误。</summary>
        public IReadOnlyList<string> Errors { get; }

        /// <summary>校验警告（不阻止使用）。</summary>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>是否读取成功（有数据且无错误）。</summary>
        public bool Success => Data != null && Errors.Count == 0;
    }
}
