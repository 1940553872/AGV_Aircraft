using UnityEngine;

namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 机械臂单个转动关节：零位本地旋转（URDF 关节原点）× 绕本地轴的关节角。
    /// 关节角按 ROS 右手定则记（与 UR 控制器一致），Unity 中按左手定则取反后施加。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArmJoint : MonoBehaviour
    {
        [SerializeField, Tooltip("关节名，与运动学 yaml 一致")]
        private string jointName;

        [SerializeField, Tooltip("转轴（本地坐标，Unity）")]
        private Vector3 axisLocal = Vector3.up;

        [SerializeField, Tooltip("零位本地旋转（URDF 关节原点）")]
        private Quaternion zeroLocalRotation = Quaternion.identity;

        [SerializeField, Tooltip("最小角度，单位 °")]
        private float minDeg = -360f;

        [SerializeField, Tooltip("最大角度，单位 °")]
        private float maxDeg = 360f;

        [SerializeField, Tooltip("最大角速度，单位 °/s")]
        private float maxSpeedDegPerS = 180f;

        [SerializeField, Tooltip("当前关节角，单位 °")]
        private float angleDeg;

        /// <summary>关节名。</summary>
        public string JointName => jointName;

        /// <summary>转轴（本地坐标，Unity）。</summary>
        public Vector3 AxisLocal => axisLocal;

        /// <summary>最小角度，单位 °。</summary>
        public float MinDeg => minDeg;

        /// <summary>最大角度，单位 °。</summary>
        public float MaxDeg => maxDeg;

        /// <summary>最大角速度，单位 °/s。</summary>
        public float MaxSpeedDegPerS => maxSpeedDegPerS;

        /// <summary>当前关节角，单位 °。</summary>
        public float AngleDeg => angleDeg;

        /// <summary>零位本地旋转（URDF 关节原点）。</summary>
        public Quaternion ZeroLocalRotation => zeroLocalRotation;

        /// <summary>编辑器生成器写入关节参数（零位取当前本地旋转）。</summary>
        public void Configure(string name, Vector3 axis, float min, float max, float maxSpeed)
        {
            jointName = name;
            axisLocal = axis.normalized;
            minDeg = min;
            maxDeg = max;
            maxSpeedDegPerS = maxSpeed;
            zeroLocalRotation = transform.localRotation;
            angleDeg = 0f;
        }

        /// <summary>设置关节角（限制在限位内），返回实际角度。</summary>
        public float SetAngleDeg(float degrees)
        {
            angleDeg = Mathf.Clamp(degrees, minDeg, maxDeg);
            transform.localRotation = zeroLocalRotation * Quaternion.AngleAxis(-angleDeg, axisLocal);
            return angleDeg;
        }
    }
}
