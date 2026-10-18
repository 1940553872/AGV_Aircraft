using UnityEngine;

namespace RivetSim.Core.Config
{
    /// <summary>
    /// 检测配置（CFG_Inspection）：区域 / 工位 / 铆钉排布、钉距、蒙皮与声速、探头、接触力、采集与缺陷比例。
    /// A 扫与接触力为教学简化模型。
    /// </summary>
    [CreateAssetMenu(fileName = "CFG_Inspection", menuName = "RivetSim/Config/Inspection")]
    public sealed class InspectionConfig : ScriptableObject
    {
        [TextArea(2, 6)]
        [Tooltip("参数来源与修改说明")]
        public string notes = "v1 预估值，见 Docs/Architecture.md §4、§6.1。M4（2026-10-18）经用户确认：虚拟弹簧刚度、耦合质量、A 扫合成、缺陷回波模型与闸门参数（ADR-016）。M2 第三部分 3B（2026-10-18）经用户确认：工位间距 1.2 → 1.5 m，新增区域位置、铆钉排高度、停靠距离与 AprilTag 标记参数。";

        [Header("区域 / 工位 / 铆钉排布")]
        [Tooltip("检测区域数（机身左、右侧各 1 个）")]
        public int regionCount = 2;

        [Tooltip("每个区域的工位数")]
        public int stationsPerRegion = 2;

        [Tooltip("工位间距，单位 m（Architecture §6.1）")]
        public float stationSpacingM = 1.5f;

        [Tooltip("检测区域中心在主起落架后方的距离（沿机身轴线），单位 m；位于机翼后缘（约 3.3 m）与平尾前缘（约 8.9 m）之间的机身直段。" +
                 "由 5.0 调为 4.85，避开右侧机身 4.30～4.33 m 处的竖缝（用户确认）")]
        public float regionCenterAftOfMainGearM = 4.85f;

        [Tooltip("铆钉排离地高度，单位 m（§6.1 的 2.0～2.3 m 取中值）")]
        public float rivetRowHeightM = 2.15f;

        [Tooltip("每个工位的铆钉排数")]
        public int rivetRowsPerStation = 1;

        [Tooltip("每个工位的铆钉数（12 颗可触发一次“每 10 颗基准复测”）")]
        public int rivetsPerStation = 12;

        [Header("停靠与标记（AprilTag tag36h11，ID 分配见 ThirdParty/AprilTag/tag36h11/SOURCE.md）")]
        [Tooltip("机械臂基座轴线到蒙皮的水平距离（停靠设计），单位 m")]
        public float dockingStandoffM = 0.55f;

        [Tooltip("停靠标记板在停靠位姿前方的距离，单位 m")]
        public float dockingMarkerAheadM = 1.0f;

        [Tooltip("停靠标记板向远离机身一侧的偏移，单位 m（避免落入相邻工位的停车区）")]
        public float dockingMarkerLateralM = 0.8f;

        [Tooltip("停靠标记 ID：区域 1 工位 1、2，区域 2 工位 1、2")]
        public int[] dockingTagIds = { 0, 1, 2, 3 };

        [Tooltip("基准标记 ID（贴在蒙皮上，基准注册 / 每 10 颗复测），顺序同上")]
        public int[] datumTagIds = { 4, 5, 6, 7 };

        [Tooltip("充电位对接标记 ID")]
        public int chargerTagId = 8;

        [Tooltip("停靠标记（含充电位）边长，单位 m")]
        public float dockingTagSizeM = 0.20f;

        [Tooltip("基准标记边长，单位 m")]
        public float datumTagSizeM = 0.10f;

        [Header("铆钉与蒙皮（按实物尺寸，不随模型机缩放）")]
        [Tooltip("沉头铆钉钉头直径，单位 mm")]
        public float rivetHeadDiameterMm = 8f;

        [Tooltip("钉距，单位 mm")]
        public float rivetPitchMm = 25f;

        [Tooltip("铝合金蒙皮厚度，单位 mm")]
        public float skinThicknessMm = 2f;

        [Tooltip("蒙皮纵波声速，单位 m/s")]
        public float longitudinalVelocityMps = 6320f;

        [Header("探头")]
        [Tooltip("探头中心频率，单位 MHz（单晶接触式）")]
        public float probeFrequencyMHz = 10f;

        [Tooltip("晶片直径，单位 mm")]
        public float crystalDiameterMm = 6f;

        [Tooltip("是否带延迟块")]
        public bool hasDelayLine = true;

        [Header("接触力（虚拟弹簧，教学简化模型）")]
        [Tooltip("目标接触力，单位 N")]
        public float targetContactForceN = 10f;

        [Tooltip("允许接触力下限，单位 N")]
        public float contactForceMinN = 8f;

        [Tooltip("允许接触力上限，单位 N")]
        public float contactForceMaxN = 12f;

        [Tooltip("接触力超限阈值，单位 N")]
        public float contactForceLimitN = 20f;

        [Tooltip("浮动夹持弹簧刚度，单位 N/m（5 N/mm：10 N 时压缩 2 mm）")]
        public float floatingSpringStiffnessNpm = 5000f;

        [Tooltip("浮动行程用尽后的硬限位刚度，单位 N/m")]
        public float hardStopStiffnessNpm = 50000f;

        [Tooltip("耦合质量降为 0 的接触力，单位 N（接触力 ≥ 允许下限时耦合质量为 1，其间线性）")]
        public float couplingZeroForceN = 2f;

        [Tooltip("耦合质量低于此值判为无效")]
        [Range(0f, 1f)]
        public float couplingInvalidThreshold = 0.5f;

        [Header("采集与 A 扫（教学简化模型）")]
        [Tooltip("单钉采集时长，单位 s")]
        public float acquisitionDurationS = 0.5f;

        [Tooltip("A 扫采样率，单位 MHz")]
        public float aScanSampleRateMHz = 100f;

        [Tooltip("A 扫显示时间窗，单位 µs")]
        public float aScanWindowUs = 2f;

        [Tooltip("界面回波（延迟块 / 蒙皮界面）在显示时间窗内的时刻，单位 µs")]
        public float interfaceEchoTimeUs = 0.2f;

        [Tooltip("脉冲相对带宽（−6 dB）")]
        public float pulseFractionalBandwidth = 0.6f;

        [Tooltip("界面回波幅值（满屏高度比例）")]
        public float interfaceEchoAmplitude = 0.9f;

        [Tooltip("一次底面回波幅值（满屏高度比例）")]
        public float backwallEchoAmplitude = 0.8f;

        [Tooltip("多次底面回波每次的幅值比例")]
        public float multipleEchoDecay = 0.55f;

        [Tooltip("噪声 σ（满屏高度比例）")]
        public float noiseSigmaFs = 0.02f;

        [Header("缺陷回波模型（教学简化模型，M4）")]
        [Tooltip("腐蚀减薄：剩余厚度比例")]
        public float thinningRemainingRatio = 0.7f;

        [Tooltip("孔边裂纹：缺陷回波深度，单位 mm")]
        public float crackDepthMm = 1.0f;

        [Tooltip("孔边裂纹：缺陷回波幅值（满屏高度比例）")]
        public float crackEchoAmplitude = 0.45f;

        [Tooltip("孔边裂纹：底面回波幅值系数")]
        public float crackBackwallFactor = 0.6f;

        [Tooltip("铆钉松动：底面回波幅值系数")]
        public float looseBackwallFactor = 0.45f;

        [Tooltip("铆钉松动：多次底面回波每次的幅值比例")]
        public float looseMultipleEchoDecay = 0.3f;

        [Header("闸门判读（教学简化模型，M4）")]
        [Tooltip("闸门 A（界面 / 耦合）阈值：界面回波低于此值判为无效（满屏高度比例）")]
        [Range(0f, 1f)]
        public float gateAThresholdRatio = 0.30f;

        [Tooltip("闸门 B（缺陷，界面与底面之间）阈值：出现不低于此值的回波判为缺陷（满屏高度比例）")]
        [Range(0f, 1f)]
        public float gateBThresholdRatio = 0.25f;

        [Tooltip("闸门 B 起点：界面回波之后的盲区，单位 µs")]
        public float flawGateDeadZoneUs = 0.15f;

        [Tooltip("底面闸门半宽：名义声时的比例")]
        public float backwallGateHalfWidthRatio = 0.10f;

        [Tooltip("底面回波低于此值判为缺陷（满屏高度比例）")]
        public float backwallDefectRatio = 0.40f;

        [Tooltip("底面回波低于此值判为疑似（满屏高度比例）")]
        public float backwallSuspectRatio = 0.55f;

        [Header("真值缺陷")]
        [Tooltip("真值缺陷比例（类型：孔边裂纹、腐蚀减薄、铆钉松动）")]
        [Range(0f, 1f)]
        public float defectRatio = 0.10f;
    }
}
