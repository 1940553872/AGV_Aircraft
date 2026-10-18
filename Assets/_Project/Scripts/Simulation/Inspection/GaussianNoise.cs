using System;

namespace RivetSim.Simulation.Inspection
{
    /// <summary>
    /// 高斯噪声（Box–Muller），随机源由调用方持有（固定种子，结果可复现）。
    /// </summary>
    public static class GaussianNoise
    {
        /// <summary>均值 0、标准差 sigma 的一个样本。</summary>
        public static float Next(Random random, float sigma)
        {
            if (sigma <= 0f) return 0f;
            double u1 = 1.0 - random.NextDouble();
            double u2 = random.NextDouble();
            return (float)(sigma * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
        }
    }
}
