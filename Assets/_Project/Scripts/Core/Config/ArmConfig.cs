using UnityEngine;

namespace RivetSim.Core.Config
{
    /// <summary>
    /// 机械臂配置（CFG_Arm）：运动学参数来源、关节限位与速度、末端速度、探头工具长度、命名姿态、关节运动与逆解、
    /// 检测动作距离与速度、连杆间隙、腕部相机与简化视觉噪声（M4 新增参数经用户确认，见 ADR-016）。
    /// </summary>
    [CreateAssetMenu(fileName = "CFG_Arm", menuName = "RivetSim/Config/Arm")]
    public sealed class ArmConfig : ScriptableObject
    {
        /// <summary>运输姿态名称。</summary>
        public const string StowPoseName = "Stow";

        /// <summary>观察姿态名称（面向车体右侧；左侧工位基座角转半圈，见 ArmController.NamedPose）。</summary>
        public const string ObservePoseName = "Observe";

        /// <summary>直立过渡姿态名称（运输姿态与观察姿态之间的中间点，避免关节插值路径扫到机身）。</summary>
        public const string ReadyPoseName = "Ready";

        [TextArea(2, 6)]
        [Tooltip("参数来源与修改说明")]
        public string notes = "UR5e；关节限位与速度取自 Universal_Robots_ROS2_Description config/ur5e/joint_limits.yaml；其余为 Docs/Architecture.md §6.1 预估值。M4（2026-10-18）经用户确认：Stow / Observe 命名姿态、关节运动、逆解、检测动作、连杆间隙与简化视觉噪声参数（ADR-016）。";

        [Header("型号与运动学")]
        [Tooltip("机械臂型号")]
        public string modelName = "UR5e";

        [Tooltip("运动学参数 yaml（default_kinematics.yaml，M2 导入后指定）")]
        public TextAsset kinematicsYaml;

        [Tooltip("臂展，单位 m")]
        public float reachM = 0.85f;

        [Tooltip("额定负载，单位 kg")]
        public float payloadKg = 5f;

        [Tooltip("舒适工作区：目标（工位铆钉平均高度）比机械臂安装面高出的距离，单位 m；升降柱目标高度据此计算（M2 第二部分经用户确认）")]
        public float comfortTargetAboveMountM = 0.30f;

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

        [Header("命名姿态（M4 确定，ADR-016）")]
        [Tooltip("命名姿态列表：Stow（运输，收臂后外廓在车体投影内）、Ready（直立过渡）、Observe（观察，面向车体右侧，腕部相机光轴水平）")]
        public NamedJointPose[] namedPoses =
        {
            new NamedJointPose(StowPoseName, new[] { 0f, -180f, -120f, 45f, -90f, 0f }),
            new NamedJointPose(ReadyPoseName, new[] { 0f, -90f, 90f, -90f, -90f, 0f }),
            new NamedJointPose(ObservePoseName, new[] { -61f, -82f, 146f, -82f, -243f, -98f }),
        };

        [Tooltip("判定处于命名姿态（如运输姿态）的关节角容差，单位 °")]
        public float namedPoseToleranceDeg = 0.5f;

        [Header("关节运动（M4）")]
        [Tooltip("关节插值运动的最大角速度，单位 °/s（演示用；yaml 的最大角速度仍作为上限）")]
        public float jointMoveSpeedDegPerS = 60f;

        [Tooltip("关节插值运动的角加速度，单位 °/s²")]
        public float jointMoveAccelerationDegPerS2 = 120f;

        [Tooltip("末端直线运动加速度，单位 m/s²")]
        public float endEffectorAccelerationMps2 = 0.5f;

        [Header("逆解（阻尼最小二乘，M4）")]
        [Tooltip("单个初值的最大迭代次数")]
        public int ikMaxIterations = 150;

        [Tooltip("位置收敛阈值，单位 m")]
        public float ikPositionToleranceM = 0.0002f;

        [Tooltip("姿态收敛阈值，单位 °")]
        public float ikOrientationToleranceDeg = 0.5f;

        [Tooltip("阻尼系数 λ")]
        public float ikDampingLambda = 0.05f;

        [Tooltip("单次迭代关节角步长上限，单位 °")]
        public float ikMaxStepDeg = 8f;

        [Tooltip("固定初值都失败后的随机初值次数")]
        public int ikRandomRestarts = 8;

        [Tooltip("姿态误差（弧度）折算为长度的权重，单位 m")]
        public float ikOrientationWeightM = 0.2f;

        [Header("检测动作（M4）")]
        [Tooltip("对中 / 回退位置：探头端面到蒙皮的距离，单位 m（在近距离限速区内）")]
        public float preContactStandoffM = 0.05f;

        [Tooltip("接触逼近速度（距预计接触点 toolFloatTravelM 以内），单位 m/s")]
        public float contactApproachSpeedMps = 0.005f;

        [Tooltip("机械臂连杆到机身外壳的最小间隙，单位 m")]
        public float linkClearanceMinM = 0.03f;

        [Tooltip("探头末端零件（夹持、相机等）到机身外壳的最小间隙，单位 m")]
        public float endEffectorClearanceMinM = 0.01f;

        [Tooltip("间隙检查不计入的探头前端长度（自端面起），单位 m")]
        public float probeTipExclusionM = 0.03f;

        [Tooltip("间隙检查的最大搜索距离，单位 m")]
        public float clearanceSearchMaxM = 0.2f;

        [Tooltip("关节插值路径的间隙采样数")]
        public int clearancePathSamples = 12;

        [Header("腕部相机（装在 wrist_3 法兰侧面）")]
        [Tooltip("视场（水平 × 垂直），单位 °")]
        public Vector2 wristCameraFovDeg = new Vector2(60f, 45f);

        [Tooltip("工作距离（最近 ～ 最远），单位 m")]
        public Vector2 wristCameraWorkingDistanceM = new Vector2(0.15f, 0.40f);

        [Header("简化视觉（视场可见性判定 + 噪声，教学简化模型，M4）")]
        [Tooltip("基准注册时腕部相机到基准标记的距离，单位 m")]
        public float observeStandoffM = 0.30f;

        [Tooltip("可识别的最大入射角（光轴与标记 / 铆钉法向夹角），单位 °")]
        public float visionMaxIncidenceDeg = 45f;

        [Tooltip("钉心复测位置噪声 σ（蒙皮切平面内每个方向），单位 m")]
        public float rivetCenterNoiseSigmaM = 0.0002f;

        [Tooltip("基准注册位置噪声 σ（每个方向），单位 m")]
        public float datumPositionNoiseSigmaM = 0.0005f;

        [Tooltip("基准注册转角噪声 σ（绕标记法向），单位 °")]
        public float datumRotationNoiseSigmaDeg = 0.2f;

        [Tooltip("单次视觉测量用时，单位 s")]
        public float visionMeasureTimeS = 0.2f;

        /// <summary>按名称查找命名姿态；找不到时返回 false。</summary>
        public bool TryGetNamedPose(string poseName, out float[] jointAnglesDeg)
        {
            foreach (NamedJointPose pose in namedPoses)
            {
                if (pose.poseName != poseName || pose.jointAnglesDeg == null) continue;
                jointAnglesDeg = (float[])pose.jointAnglesDeg.Clone();
                return true;
            }
            jointAnglesDeg = null;
            return false;
        }
    }
}
