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
        public string notes = "v1 预估值，见 Docs/Architecture.md §4、§6.1。闸门阈值在 M4 确定。";

        [Header("区域 / 工位 / 铆钉排布")]
        [Tooltip("检测区域数（机身左、右侧各 1 个）")]
        public int regionCount = 2;

        [Tooltip("每个区域的工位数")]
        public int stationsPerRegion = 2;

        [Tooltip("工位间距，单位 m")]
        public float stationSpacingM = 1.2f;

        [Tooltip("每个工位的铆钉排数")]
        public int rivetRowsPerStation = 1;

        [Tooltip("每个工位的铆钉数（12 颗可触发一次“每 10 颗基准复测”）")]
        public int rivetsPerStation = 12;

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

        [Header("采集与 A 扫（教学简化模型）")]
        [Tooltip("单钉采集时长，单位 s")]
        public float acquisitionDurationS = 0.5f;

        [Tooltip("A 扫采样率，单位 MHz")]
        public float aScanSampleRateMHz = 100f;

        [Tooltip("A 扫显示时间窗，单位 µs")]
        public float aScanWindowUs = 2f;

        [Tooltip("闸门 A 阈值（满屏高度比例 0～1，M4 确定）")]
        [Range(0f, 1f)]
        public float gateAThresholdRatio;

        [Tooltip("闸门 B 阈值（满屏高度比例 0～1，M4 确定）")]
        [Range(0f, 1f)]
        public float gateBThresholdRatio;

        [Header("真值缺陷")]
        [Tooltip("真值缺陷比例（类型：孔边裂纹、腐蚀减薄、铆钉松动）")]
        [Range(0f, 1f)]
        public float defectRatio = 0.10f;
    }
}
