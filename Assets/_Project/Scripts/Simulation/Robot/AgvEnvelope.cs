using UnityEngine;

namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// AGV 外廓量测结果（车体坐标系，x 右、y 上、z 前，单位 m）：整机与机械臂的范围、扫掠半径（水平面内到车体中心的最大距离）。
    /// </summary>
    public readonly struct AgvEnvelope
    {
        /// <summary>整机范围下角。</summary>
        public readonly Vector3 Min;

        /// <summary>整机范围上角。</summary>
        public readonly Vector3 Max;

        /// <summary>机械臂范围下角。</summary>
        public readonly Vector3 ArmMin;

        /// <summary>机械臂范围上角。</summary>
        public readonly Vector3 ArmMax;

        /// <summary>扫掠半径。</summary>
        public readonly float SweptRadiusM;

        /// <summary>创建结果。</summary>
        public AgvEnvelope(Vector3 min, Vector3 max, Vector3 armMin, Vector3 armMax, float sweptRadiusM)
        {
            Min = min;
            Max = max;
            ArmMin = armMin;
            ArmMax = armMax;
            SweptRadiusM = sweptRadiusM;
        }

        /// <summary>整机最高点离地。</summary>
        public float TopM => Max.y;

        /// <summary>车头前伸量（本地 +Z 最远点）。</summary>
        public float FrontReachM => Max.z;

        /// <summary>机械臂水平投影是否在车体投影（宽 × 长）内。</summary>
        public bool ArmInsideBody(float bodyWidthM, float bodyLengthM) =>
            ArmMin.x >= -0.5f * bodyWidthM && ArmMax.x <= 0.5f * bodyWidthM && ArmMin.z >= -0.5f * bodyLengthM && ArmMax.z <= 0.5f * bodyLengthM;

        /// <inheritdoc />
        public override string ToString() =>
            $"机械臂横向 {ArmMin.x:+0.000;-0.000}～{ArmMax.x:+0.000;-0.000} m、纵向 {ArmMin.z:+0.000;-0.000}～{ArmMax.z:+0.000;-0.000} m、" +
            $"高 {ArmMin.y:0.000}～{ArmMax.y:0.000} m；整机最高点 {TopM:0.000} m、扫掠半径 {SweptRadiusM:0.000} m、车头前伸 {FrontReachM:0.000} m";
    }
}
