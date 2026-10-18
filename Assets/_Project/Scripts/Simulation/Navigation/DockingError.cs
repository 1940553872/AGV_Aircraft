using RivetSim.Core.Config;
using UnityEngine;

namespace RivetSim.Simulation.Navigation
{
    /// <summary>
    /// 停靠误差：实际位姿相对目标停靠位姿，在目标坐标系中分解为纵向（车头方向为正）、横向（右侧为正）与偏航（顺时针为正）。
    /// </summary>
    public readonly struct DockingError
    {
        /// <summary>纵向误差，单位 m（正 = 停过了）。</summary>
        public readonly float LongitudinalM;

        /// <summary>横向误差，单位 m（正 = 偏右）。</summary>
        public readonly float LateralM;

        /// <summary>偏航误差，单位 °（正 = 顺时针偏）。</summary>
        public readonly float YawDeg;

        /// <summary>创建误差。</summary>
        public DockingError(float longitudinalM, float lateralM, float yawDeg)
        {
            LongitudinalM = longitudinalM;
            LateralM = lateralM;
            YawDeg = yawDeg;
        }

        /// <summary>计算 actual 相对 target 的误差。</summary>
        public static DockingError Between(PlanarPose target, PlanarPose actual)
        {
            float yaw = target.yawDeg * Mathf.Deg2Rad;
            var heading = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
            var right = new Vector2(heading.y, -heading.x);
            Vector2 d = actual.positionM - target.positionM;
            return new DockingError(Vector2.Dot(d, heading), Vector2.Dot(d, right), Mathf.DeltaAngle(target.yawDeg, actual.yawDeg));
        }

        /// <summary>是否在容差内（纵向与横向分别不超过 positionToleranceM，偏航不超过 yawToleranceDeg）。</summary>
        public bool IsWithin(float positionToleranceM, float yawToleranceDeg) =>
            Mathf.Abs(LongitudinalM) <= positionToleranceM && Mathf.Abs(LateralM) <= positionToleranceM && Mathf.Abs(YawDeg) <= yawToleranceDeg;

        /// <inheritdoc />
        public override string ToString() => $"纵向 {LongitudinalM * 1000f:+0.0;-0.0;0.0} mm、横向 {LateralM * 1000f:+0.0;-0.0;0.0} mm、偏航 {YawDeg:+0.00;-0.00;0.00}°";
    }
}
