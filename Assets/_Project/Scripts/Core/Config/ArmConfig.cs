using UnityEngine;

namespace RivetSim.Core.Config
{
    /// <summary>
    /// 机械臂配置（CFG_Arm）：运动学参数来源、关节限位与速度、末端速度、探头工具长度、命名姿态、腕部相机。
    /// </summary>
    [CreateAssetMenu(fileName = "CFG_Arm", menuName = "RivetSim/Config/Arm")]
    public sealed class ArmConfig : ScriptableObject
    {
        [TextArea(2, 6)]
        [Tooltip("参数来源与修改说明")]
        public string notes = "UR5e；关节限位与速度取自 Universal_Robots_ROS2_Description config/ur5e/joint_limits.yaml；其余为 Docs/Architecture.md §6.1 预估值。命名姿态在 M4 确定。";

        [Header("型号与运动学")]
        [Tooltip("机械臂型号")]
        public string modelName = "UR5e";

        [Tooltip("运动学参数 yaml（default_kinematics.yaml，M2 导入后指定）")]
        public TextAsset kinematicsYaml;

        [Tooltip("臂展，单位 m")]
        public float reachM = 0.85f;

        [Tooltip("额定负载，单位 kg")]
        public float payloadKg = 5f;

        [Tooltip("关节限位与最大角速度（顺序：基座 → 腕 3）")]
        public JointLimit[] jointLimits =
        {
            new JointLimit("shoulder_pan_joint", -360f, 360f, 180f),
            new JointLimit("shoulder_lift_joint", -360f, 360f, 180f),
            new JointLimit("elbow_joint", -180f, 180f, 180f),
            new JointLimit("wrist_1_joint", -360f, 360f, 180f),
            new JointLimit("wrist_2_joint", -360f, 360f, 180f),
            new JointLimit("wrist_3_joint", -360f, 360f, 180f),
        };

        [Header("末端速度")]
        [Tooltip("常规末端速度，单位 m/s")]
        public float endEffectorSpeedMps = 0.25f;

        [Tooltip("近距离限速区内末端速度，单位 m/s")]
        public float nearFieldSpeedMps = 0.05f;

        [Tooltip("近距离限速区范围（距表面），单位 m")]
        public float nearFieldDistanceM = 0.100f;

        [Header("探头工具")]
        [Tooltip("法兰到探头端面长度，单位 m")]
        public float toolLengthM = 0.15f;

        [Tooltip("浮动夹持行程（±），单位 m")]
        public float toolFloatTravelM = 0.003f;

        [Header("命名姿态（M4 确定）")]
        [Tooltip("命名姿态列表，如 Stow（运输）、Observe（观察）")]
        public NamedJointPose[] namedPoses = new NamedJointPose[0];

        [Header("腕部相机（装在 wrist_3 法兰侧面）")]
        [Tooltip("视场（水平 × 垂直），单位 °")]
        public Vector2 wristCameraFovDeg = new Vector2(60f, 45f);

        [Tooltip("工作距离（最近 ～ 最远），单位 m")]
        public Vector2 wristCameraWorkingDistanceM = new Vector2(0.15f, 0.40f);
    }
}
