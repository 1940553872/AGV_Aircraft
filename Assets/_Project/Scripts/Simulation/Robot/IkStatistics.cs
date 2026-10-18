namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 逆解统计（供验收报告与叠加信息）：求解次数、收敛次数、平均 / 最大位置误差与最大姿态误差。
    /// </summary>
    public sealed class IkStatistics
    {
        private double _positionSumM;

        /// <summary>求解次数。</summary>
        public int Attempts { get; private set; }

        /// <summary>收敛次数。</summary>
        public int Converged { get; private set; }

        /// <summary>收敛解的最大位置误差，单位 m。</summary>
        public float MaxPositionErrorM { get; private set; }

        /// <summary>收敛解的最大姿态误差，单位 °。</summary>
        public float MaxAngleErrorDeg { get; private set; }

        /// <summary>收敛解的平均位置误差，单位 m。</summary>
        public float MeanPositionErrorM => Converged > 0 ? (float)(_positionSumM / Converged) : 0f;

        /// <summary>收敛率（0～1）。</summary>
        public float ConvergenceRate => Attempts > 0 ? (float)Converged / Attempts : 0f;

        /// <summary>记录一次求解。</summary>
        public void Record(in IkResult result)
        {
            Attempts++;
            if (!result.Converged) return;
            Converged++;
            _positionSumM += result.PositionErrorM;
            if (result.PositionErrorM > MaxPositionErrorM) MaxPositionErrorM = result.PositionErrorM;
            if (result.AngleErrorDeg > MaxAngleErrorDeg) MaxAngleErrorDeg = result.AngleErrorDeg;
        }

        /// <summary>清零。</summary>
        public void Reset()
        {
            Attempts = 0;
            Converged = 0;
            _positionSumM = 0;
            MaxPositionErrorM = 0f;
            MaxAngleErrorDeg = 0f;
        }

        /// <inheritdoc />
        public override string ToString() =>
            $"逆解 {Converged}/{Attempts} 收敛（{ConvergenceRate:P1}），位置误差 平均 {MeanPositionErrorM * 1000f:0.000} mm / 最大 {MaxPositionErrorM * 1000f:0.000} mm，" +
            $"最大姿态误差 {MaxAngleErrorDeg:0.000}°";
    }
}
