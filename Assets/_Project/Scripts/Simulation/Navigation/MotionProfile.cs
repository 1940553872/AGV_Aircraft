using UnityEngine;

namespace RivetSim.Simulation.Navigation
{
    /// <summary>
    /// 运动指令的制动曲线（纯计算）：按“剩余量 = 速度² / (2 × 加速度)”给出不超过上限、能在终点刚好停住的速度。
    /// </summary>
    public static class MotionProfile
    {
        /// <summary>距离 remaining 处允许的最大速度（非负），不超过 maxSpeed。</summary>
        public static float BrakingSpeed(float remaining, float maxSpeed, float acceleration)
        {
            if (remaining <= 0f) return 0f;
            return Mathf.Min(maxSpeed, Mathf.Sqrt(2f * acceleration * remaining));
        }

        /// <summary>原地转向的角速度指令（°/s，符号与误差相同）。</summary>
        public static float TurnRate(float errorDeg, float maxRateDegPerS, float accelerationDegPerS2) =>
            Mathf.Sign(errorDeg) * BrakingSpeed(Mathf.Abs(errorDeg), maxRateDegPerS, accelerationDegPerS2);
    }
}
