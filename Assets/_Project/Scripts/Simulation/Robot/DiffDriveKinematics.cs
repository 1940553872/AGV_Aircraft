using RivetSim.Core.Config;
using UnityEngine;

namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 差速驱动运动学（纯计算）：位姿积分（按圆弧精确积分）、车体速度与左右轮速互换。
    /// 坐标约定见 ADR-008：平面位置 x = 世界 X、y = 世界 Z；偏航 0° 指向 +Z、顺时针为正；角速度顺时针为正（向右转）。
    /// </summary>
    public static class DiffDriveKinematics
    {
        // 角速度低于此值（rad/s）时按直线积分，避免圆弧公式除以接近 0 的数。
        private const float StraightLineOmegaRadPerS = 1e-6f;

        /// <summary>以线速度 linearMps、角速度 angularDegPerS 匀速运动 dtS 后的位姿。</summary>
        public static PlanarPose Integrate(PlanarPose pose, float linearMps, float angularDegPerS, float dtS)
        {
            float yaw0 = pose.yawDeg * Mathf.Deg2Rad;
            float omega = angularDegPerS * Mathf.Deg2Rad;
            float turn = omega * dtS;
            float yaw1 = yaw0 + turn;
            // 圆弧的弦长 = 2·(v/ω)·sin(Δθ/2)，方向为中点偏航角；该形式在 Δθ 很小时数值稳定（不做 cos 相减）。
            float chord = Mathf.Abs(omega) < StraightLineOmegaRadPerS
                ? linearMps * dtS
                : 2f * linearMps / omega * Mathf.Sin(0.5f * turn);
            float mid = yaw0 + 0.5f * turn;
            Vector2 delta = new Vector2(Mathf.Sin(mid), Mathf.Cos(mid)) * chord;
            return new PlanarPose(pose.positionM + delta, Mathf.Repeat(yaw1 * Mathf.Rad2Deg, 360f));
        }

        /// <summary>车体速度 → 左、右轮线速度，单位 m/s（向右转时左轮更快）。</summary>
        public static Vector2 ToWheelSpeeds(float linearMps, float angularDegPerS, float wheelTrackM)
        {
            float half = 0.5f * wheelTrackM * angularDegPerS * Mathf.Deg2Rad;
            return new Vector2(linearMps + half, linearMps - half);
        }

        /// <summary>左、右轮线速度 → 车体线速度（x，m/s）与角速度（y，°/s）。</summary>
        public static Vector2 FromWheelSpeeds(float leftMps, float rightMps, float wheelTrackM)
        {
            return new Vector2(0.5f * (leftMps + rightMps), (leftMps - rightMps) / wheelTrackM * Mathf.Rad2Deg);
        }

        /// <summary>车头方向单位向量（x = 世界 X，y = 世界 Z）。</summary>
        public static Vector2 Heading(float yawDeg)
        {
            float yaw = yawDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
        }

        /// <summary>车体右侧方向单位向量（x = 世界 X，y = 世界 Z）。</summary>
        public static Vector2 Right(float yawDeg)
        {
            float yaw = yawDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw));
        }

        /// <summary>从 from 指向 to 的方位角，单位 °（0 指向 +Z、顺时针为正，范围 0～360）。</summary>
        public static float BearingDeg(Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            return Mathf.Repeat(Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, 360f);
        }
    }
}
