namespace RivetSim.Simulation.Navigation
{
    /// <summary>
    /// 一次导航（或折线行驶、直线行驶）的结果：是否成功、失败原因、规划路径长度、实际里程与仿真用时。
    /// </summary>
    public readonly struct NavigationResult
    {
        /// <summary>是否成功。</summary>
        public readonly bool Success;

        /// <summary>失败原因（成功时为空）。</summary>
        public readonly string FailureReason;

        /// <summary>规划（或给定）路径长度，单位 m。</summary>
        public readonly float PathLengthM;

        /// <summary>实际行驶里程，单位 m。</summary>
        public readonly float TraveledM;

        /// <summary>仿真用时，单位 s。</summary>
        public readonly float DurationS;

        /// <summary>创建结果。</summary>
        public NavigationResult(bool success, string failureReason, float pathLengthM, float traveledM, float durationS)
        {
            Success = success;
            FailureReason = failureReason ?? "";
            PathLengthM = pathLengthM;
            TraveledM = traveledM;
            DurationS = durationS;
        }

        /// <summary>失败结果。</summary>
        public static NavigationResult Failed(string reason) => new NavigationResult(false, reason, 0f, 0f, 0f);

        /// <inheritdoc />
        public override string ToString() => Success
            ? $"路径长度 {PathLengthM:0.00} m、实际里程 {TraveledM:0.00} m、用时 {DurationS:0.0} s（仿真时间）"
            : $"失败：{FailureReason}";
    }
}
