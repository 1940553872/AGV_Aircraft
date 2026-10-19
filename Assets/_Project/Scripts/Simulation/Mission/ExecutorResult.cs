using UnityEngine;

namespace RivetSim.Simulation.Mission
{
    /// <summary>
    /// 单元执行结果。
    /// </summary>
    public readonly struct ExecutorResult
    {
        /// <summary>结果类别。</summary>
        public readonly ExecutorOutcome Outcome;

        /// <summary>说明（失败原因、跳过原因或完成摘要，可为空）。</summary>
        public readonly string Message;

        /// <summary>创建结果。</summary>
        public ExecutorResult(ExecutorOutcome outcome, string message)
        {
            Outcome = outcome;
            Message = message ?? "";
        }

        /// <summary>完成。</summary>
        public static ExecutorResult Done(string message = "") => new ExecutorResult(ExecutorOutcome.Done, message);

        /// <summary>跳过。</summary>
        public static ExecutorResult Skip(string message) => new ExecutorResult(ExecutorOutcome.Skipped, message);

        /// <summary>失败。</summary>
        public static ExecutorResult Fail(string message) => new ExecutorResult(ExecutorOutcome.Failed, message);

        /// <summary>包装为已完成的 Awaitable（同步执行器与测试用假执行器）。</summary>
        public Awaitable<ExecutorResult> AsCompleted()
        {
            var source = new AwaitableCompletionSource<ExecutorResult>();
            source.SetResult(this);
            return source.Awaitable;
        }
    }
}
