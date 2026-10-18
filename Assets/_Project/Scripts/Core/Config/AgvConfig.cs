using UnityEngine;

namespace RivetSim.Core.Config
{
    /// <summary>
    /// AGV 配置（CFG_Agv）：车体尺寸、差速驱动参数、速度限制、雷达安装位姿、升降柱（机械臂装在其顶部法兰上）、车载设备盒、电池与通讯（占位）。
    /// 安装位姿以底盘地面投影中心为原点，x 右、y 上、z 前。
    /// </summary>
    [CreateAssetMenu(fileName = "CFG_Agv", menuName = "RivetSim/Config/AGV")]
    public sealed class AgvConfig : ScriptableObject
    {
        [TextArea(2, 6)]
        [Tooltip("参数来源与修改说明")]
        public string notes = "v1 预估值，见 Docs/Architecture.md §6.1；M2 第一部分（2026-10-18）按 ADR-010 将车体改为 1.20 × 0.80 × 0.45 m、轮距 0.66 m、质量 180 kg，雷达随车头前移；M2 第二部分（2026-10-18）加入升降柱（行程 1.40 m、0.08 m/s、±1 mm，机械臂安装面 0.80～2.20 m），起停缓冲加速度 0.16 m/s² 经用户确认；原 armMount 字段由升降柱推算替代。后续按实际选型修改。";

        [Header("车体")]
        [Tooltip("车体尺寸（长 × 宽 × 高），单位 m；高度即上装平台面高")]
        public Vector3 bodySizeM = new Vector3(1.20f, 0.80f, 0.45f);

        [Tooltip("整机质量（含升降柱、机械臂与上装设备），单位 kg")]
        public float massKg = 180f;

        [Tooltip("移动时整机高度上限（升降柱最低位、机械臂运输姿态），单位 m（Architecture §6.1，M4 校核 Stow 外廓用）")]
        public float transportHeightMaxM = 1.35f;

        /// <summary>车体外接圆半径（绕车体中心 = 驱动轮轴中点），单位 m；路径规划的膨胀半径。</summary>
        public float BodyCircumscribedRadiusM => 0.5f * Mathf.Sqrt(bodySizeM.x * bodySizeM.x + bodySizeM.y * bodySizeM.y);

        [Header("差速驱动")]
        [Tooltip("驱动轮轮距，单位 m")]
        public float wheelTrackM = 0.66f;

        [Tooltip("驱动轮直径，单位 m")]
        public float driveWheelDiameterM = 0.20f;

        [Tooltip("万向轮数量（前后各 1 个）")]
        public int casterCount = 2;

        [Header("速度与加速度")]
        [Tooltip("巡航速度，单位 m/s")]
        public float cruiseSpeedMps = 0.6f;

        [Tooltip("最大线速度，单位 m/s")]
        public float maxSpeedMps = 1.0f;

        [Tooltip("接近工位速度，单位 m/s")]
        public float approachSpeedMps = 0.15f;

        [Tooltip("对接速度，单位 m/s")]
        public float dockingSpeedMps = 0.05f;

        [Tooltip("最大线加速度，单位 m/s²")]
        public float maxAccelerationMps2 = 0.5f;

        [Tooltip("最大角速度，单位 °/s")]
        public float maxAngularSpeedDegPerS = 60f;

        [Tooltip("最大角加速度，单位 °/s²（M3 经用户确认）")]
        public float maxAngularAccelerationDegPerS2 = 120f;

        [Tooltip("倒车退出距离，单位 m")]
        public float reverseExitDistanceM = 0.6f;

        [Tooltip("底盘外轮廓距机身的最小安全距离，单位 m")]
        public float fuselageSafetyClearanceM = 0.30f;

        [Header("激光雷达")]
        [Tooltip("雷达安装位姿：车头中心，离地 0.30 m")]
        public MountPose lidarMount = new MountPose(new Vector3(0f, 0.30f, 0.60f), Vector3.zero);

        [Tooltip("每圈射线数")]
        public int lidarBeamCount = 360;

        [Tooltip("扫描频率，单位 Hz")]
        public float lidarRateHz = 10f;

        [Tooltip("最大量程，单位 m")]
        public float lidarRangeM = 20f;

        [Tooltip("测距噪声幅值（±），单位 m")]
        public float lidarRangeNoiseM = 0.02f;

        [Header("车头对接相机（教学简化模型，归 S2 视觉；M3 经用户确认）")]
        [Tooltip("对接相机安装位姿：车头中心，离地 0.40 m，朝前")]
        public MountPose dockingCameraMount = new MountPose(new Vector3(0f, 0.40f, 0.60f), Vector3.zero);

        [Tooltip("对接相机视场（水平 × 垂直），单位 °")]
        public Vector2 dockingCameraFovDeg = new Vector2(90f, 60f);

        [Tooltip("对接相机识别距离下限，单位 m")]
        public float dockingCameraMinRangeM = 0.3f;

        [Tooltip("对接相机识别距离上限，单位 m")]
        public float dockingCameraMaxRangeM = 4.0f;

        [Header("升降柱（机械臂装在顶部法兰上）")]
        [Tooltip("升降柱底座安装位姿：平台中心前移 0.15 m，位于上装平台面（离地 0.45 m）")]
        public MountPose liftMount = new MountPose(new Vector3(0f, 0.45f, 0.15f), Vector3.zero);

        [Tooltip("升降柱固定段截面（宽 × 深），单位 m")]
        public Vector2 liftSectionSizeM = new Vector2(0.25f, 0.25f);

        [Tooltip("升降柱固定段高度（平台面到最低位法兰顶面），单位 m")]
        public float liftFixedHeightM = 0.35f;

        [Tooltip("升降柱行程，单位 m")]
        public float liftStrokeM = 1.40f;

        [Tooltip("升降柱匀速段速度，单位 m/s")]
        public float liftSpeedMps = 0.08f;

        [Tooltip("升降柱起停缓冲加速度，单位 m/s²（M2 第二部分经用户确认：缓冲 0.5 s、每端 20 mm）")]
        public float liftAccelerationMps2 = 0.16f;

        [Tooltip("升降柱到位精度（±），单位 m")]
        public float liftPositionToleranceM = 0.001f;

        /// <summary>升降柱伸出量为 extensionM 时机械臂安装面离地高度，单位 m（伸出量限制在行程内）。</summary>
        public float ArmMountHeightM(float extensionM) =>
            liftMount.positionM.y + liftFixedHeightM + Mathf.Clamp(extensionM, 0f, liftStrokeM);

        /// <summary>机械臂安装面离地高度为 mountHeightM 时所需的升降柱伸出量，单位 m（未限制在行程内）。</summary>
        public float ExtensionForMountHeightM(float mountHeightM) => mountHeightM - liftMount.positionM.y - liftFixedHeightM;

        [Header("车载设备盒（长 × 宽 × 高，单位 m）")]
        [Tooltip("超声仪（S5）")]
        public Vector3 utInstrumentBoxSizeM = new Vector3(0.30f, 0.20f, 0.10f);

        [Tooltip("安全控制盒（S6）")]
        public Vector3 safetyBoxSizeM = new Vector3(0.20f, 0.15f, 0.10f);

        [Tooltip("工控机（S7）")]
        public Vector3 ipcBoxSizeM = new Vector3(0.25f, 0.20f, 0.08f);

        [Header("电池（占位，v1 线性放电显示）")]
        [Tooltip("电池电压，单位 V")]
        public float batteryVoltageV = 48f;

        [Tooltip("电池容量，单位 Ah")]
        public float batteryCapacityAh = 40f;

        [Header("通讯（占位，v1 只显示状态）")]
        [Tooltip("通讯方式")]
        public CommLinkType commLink = CommLinkType.Wifi6;

        [Tooltip("显示用固定延迟，单位 s")]
        public float commLatencyS = 0.020f;
    }
}
