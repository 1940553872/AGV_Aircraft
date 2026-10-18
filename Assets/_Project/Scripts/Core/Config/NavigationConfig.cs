using UnityEngine;

namespace RivetSim.Core.Config
{
    /// <summary>
    /// 导航配置（CFG_Navigation）：占据栅格、代价地图、路径跟踪、对接停靠与工位接近通道（M3，取值经用户确认）。
    /// 膨胀半径由 CFG_Agv 车体外接圆推算，虚拟禁行区外扩量取 CFG_Agv 的机身安全距离，不在此重复存放。
    /// </summary>
    [CreateAssetMenu(fileName = "CFG_Navigation", menuName = "RivetSim/Config/Navigation")]
    public sealed class NavigationConfig : ScriptableObject
    {
        [TextArea(2, 6)]
        [Tooltip("参数来源与修改说明")]
        public string notes = "M3（2026-10-18）经用户确认的 v1 取值：栅格 0.1 m（PromptTemplates M3）、未知格附加代价 1.0、纯追踪前视 0.8 m、减速距离 1.0 m、到点容差 0.05 m / 1.0°、预停靠距离 1.5 m、接近通道（车头前 0.20 m、车尾后 0.30 m、内侧 0.60 m、外侧 1.80 m，开口宽 2.40 m）、对接低速段 0.30 m、标记位姿估计噪声 σ 3 mm / 0.2°、停靠合格容差 ±10 mm / ±0.5°。";

        [Header("占据栅格（教学简化模型：真值位姿）")]
        [Tooltip("栅格分辨率，单位 m")]
        public float gridResolutionM = 0.1f;

        [Header("代价地图与规划")]
        [Tooltip("未知格的附加代价（相对一格步长的倍数），未知格可通行")]
        public float unknownCellExtraCost = 1.0f;

        [Header("路径跟踪（纯追踪）")]
        [Tooltip("纯追踪前视距离，单位 m")]
        public float lookaheadM = 0.8f;

        [Tooltip("距终点小于此距离时降到接近工位速度，单位 m")]
        public float slowdownDistanceM = 1.0f;

        [Tooltip("到点位置容差，单位 m")]
        public float goalPositionToleranceM = 0.05f;

        [Tooltip("到点偏航容差，单位 °")]
        public float goalYawToleranceDeg = 1.0f;

        [Header("对接停靠")]
        [Tooltip("预停靠位姿在停靠位姿后方的距离，单位 m（回充电位时复用）")]
        public float dockApproachDistanceM = 1.5f;

        [Tooltip("距停靠位姿小于此距离时切到对接速度，单位 m")]
        public float dockingSlowDistanceM = 0.30f;

        [Tooltip("标记位姿估计噪声（位置标准差），单位 m（教学简化模型）")]
        public float markerPositionNoiseSigmaM = 0.003f;

        [Tooltip("标记位姿估计噪声（偏航标准差），单位 °（教学简化模型）")]
        public float markerYawNoiseSigmaDeg = 0.2f;

        [Tooltip("停靠合格容差（纵向 / 横向），单位 m")]
        public float dockingPositionToleranceM = 0.010f;

        [Tooltip("停靠合格容差（偏航），单位 °")]
        public float dockingYawToleranceDeg = 0.5f;

        [Header("工位接近通道（停靠坐标系，通道内不叠加虚拟禁行区）")]
        [Tooltip("通道前端超出停靠位姿车头的距离，单位 m")]
        public float channelFrontMarginM = 0.20f;

        [Tooltip("通道后端超出预停靠位姿车尾的距离，单位 m")]
        public float channelRearMarginM = 0.30f;

        [Tooltip("通道内侧（朝机身）到车道中心线的距离，单位 m")]
        public float channelInnerHalfWidthM = 0.60f;

        [Tooltip("通道外侧（远离机身）到车道中心线的距离，单位 m")]
        public float channelOuterHalfWidthM = 1.80f;
    }
}
