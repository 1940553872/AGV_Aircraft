namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 逆解结果：是否收敛、最终位置 / 姿态误差、累计迭代次数、尝试的初值个数与被拒绝（如连杆间隙不足）的收敛解个数。
    /// </summary>
    public readonly struct IkResult
    {
        /// <summary>是否收敛且被接受。</summary>
        public readonly bool Converged;

        /// <summary>位置误差，单位 m。</summary>
        public readonly float PositionErrorM;

        /// <summary>姿态误差，单位 °。</summary>
        public readonly float AngleErrorDeg;

        /// <summary>累计迭代次数。</summary>
        public readonly int Iterations;

        /// <summary>尝试的初值个数。</summary>
        public readonly int SeedsTried;

        /// <summary>收敛但被拒绝的解个数。</summary>
        public readonly int Rejected;

        /// <summary>创建结果。</summary>
        public IkResult(bool converged, float positionErrorM, float angleErrorDeg, int iterations, int seedsTried, int rejected)
        {
            Converged = converged;
            PositionErrorM = positionErrorM;
            AngleErrorDeg = angleErrorDeg;
            Iterations = iterations;
            SeedsTried = seedsTried;
            Rejected = rejected;
        }

        /// <inheritdoc />
        public override string ToString() =>
            $"{(Converged ? "收敛" : "未收敛")}：位置误差 {PositionErrorM * 1000f:0.000} mm、姿态误差 {AngleErrorDeg:0.000}°，" +
            $"迭代 {Iterations} 次、初值 {SeedsTried} 个{(Rejected > 0 ? $"、拒绝 {Rejected} 个" : "")}";
    }
}
