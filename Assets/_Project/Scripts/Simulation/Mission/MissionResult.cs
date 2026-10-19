namespace RivetSim.Simulation.Mission
{
    /// <summary>
    /// 任务矩阵流程的运行结果。
    /// </summary>
    public readonly struct MissionResult
    {
        /// <summary>是否跑完全部区域。</summary>
        public readonly bool Success;

        /// <summary>失败原因。</summary>
        public readonly string FailureReason;

        /// <summary>流程用时（仿真时间），单位 s。</summary>
        public readonly double DurationS;

        /// <summary>执行的阶段次数（含重试）。</summary>
        public readonly int PhaseRuns;

        /// <summary>未恢复而记为无效的铆钉数。</summary>
        public readonly int UnrecoveredRivets;

        /// <summary>创建结果。</summary>
        public MissionResult(bool success, string failureReason, double durationS, int phaseRuns, int unrecoveredRivets)
        {
            Success = success;
            FailureReason = failureReason ?? "";
            DurationS = durationS;
            PhaseRuns = phaseRuns;
            UnrecoveredRivets = unrecoveredRivets;
        }
    }
}
