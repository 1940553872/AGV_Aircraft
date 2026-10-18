namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 机械臂动作结果：是否成功、失败原因、用时（仿真时间）与动作前检查的最小间隙。
    /// </summary>
    public readonly struct ArmMotionResult
    {
        /// <summary>是否成功。</summary>
        public readonly bool Success;

        /// <summary>失败原因。</summary>
        public readonly string FailureReason;

        /// <summary>用时，单位 s。</summary>
        public readonly float DurationS;

        /// <summary>动作前检查的最小间隙，单位 m（未检查为 NaN）。</summary>
        public readonly float MinClearanceM;

        /// <summary>创建结果。</summary>
        public ArmMotionResult(bool success, string failureReason, float durationS, float minClearanceM)
        {
            Success = success;
            FailureReason = failureReason ?? "";
            DurationS = durationS;
            MinClearanceM = minClearanceM;
        }

        /// <summary>失败结果。</summary>
        public static ArmMotionResult Failed(string reason) => new ArmMotionResult(false, reason, 0f, float.NaN);
    }
}
