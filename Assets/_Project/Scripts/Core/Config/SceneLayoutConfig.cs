using UnityEngine;

namespace RivetSim.Core.Config
{
    /// <summary>
    /// 场景布局配置（CFG_SceneLayout）：大厅尺寸、三区矩形、障碍物、充电位、巡游建图路线、模型机外形与演示区尺寸。
    /// 世界坐标：1 单位 = 1 m，Y 轴向上，原点在大厅地面中心，模型机机头朝 +Z。
    /// </summary>
    [CreateAssetMenu(fileName = "CFG_SceneLayout", menuName = "RivetSim/Config/Scene Layout")]
    public sealed class SceneLayoutConfig : ScriptableObject
    {
        [TextArea(2, 6)]
        [Tooltip("参数来源与修改说明")]
        public string notes = "v1 预估值，见 Docs/Architecture.md §4、§6.1。三区矩形、障碍物、充电位、起始位与巡游路线在 M1 确定。";

        [Header("大厅")]
        [Tooltip("大厅尺寸（长 × 宽 × 高），单位 m")]
        public Vector3 hallSizeM = new Vector3(40f, 24f, 10f);

        [Tooltip("作业通道：模型机外轮廓到障碍物的最小距离，单位 m")]
        public float aisleClearanceM = 2.5f;

        [Header("三区（M1 确定）")]
        [Tooltip("作业区矩形（模型机 + 检测区铆钉）")]
        public ZoneRect workZone;

        [Tooltip("移动区矩形（AGV 建图 / 规划 / 移动）")]
        public ZoneRect mobilityZone;

        [Tooltip("演示区矩形（演示台 + 电脑屏幕）")]
        public ZoneRect demoZone;

        [Header("移动区设施（M1 确定）")]
        [Tooltip("障碍物占地矩形列表")]
        public ZoneRect[] obstacles = new ZoneRect[0];

        [Tooltip("充电位位姿")]
        public PlanarPose chargingDockPose;

        [Tooltip("AGV 起始位姿")]
        public PlanarPose startPose;

        [Tooltip("巡游建图路线途经点（x = 世界 X，y = 世界 Z），单位 m")]
        public Vector2[] mappingRouteM = new Vector2[0];

        [Header("模型机（A320-200 外形）")]
        [Tooltip("模型机相对真机的缩比（1:5 = 0.2）")]
        public float aircraftScale = 0.2f;

        [Tooltip("机长，单位 m")]
        public float aircraftLengthM = 7.5f;

        [Tooltip("翼展，单位 m")]
        public float aircraftWingspanM = 7.2f;

        [Tooltip("机高，单位 m")]
        public float aircraftHeightM = 2.35f;

        [Tooltip("机身直径，单位 m")]
        public float fuselageDiameterM = 0.79f;

        [Tooltip("机身下缘离地高度，单位 m")]
        public float fuselageBottomHeightM = 0.40f;

        [Tooltip("机身中线离地高度，单位 m")]
        public float fuselageCenterlineHeightM = 0.80f;

        [Header("演示区")]
        [Tooltip("演示台尺寸（长 × 宽 × 高），单位 m")]
        public Vector3 demoDeskSizeM = new Vector3(1.60f, 0.80f, 0.75f);

        [Tooltip("显示器数量（左屏任务矩阵，右屏三维总览）")]
        public int monitorCount = 2;

        [Tooltip("显示器对角线尺寸，单位英寸")]
        public float monitorDiagonalInch = 27f;

        [Tooltip("显示器宽高比")]
        public Vector2 monitorAspect = new Vector2(16f, 9f);

        [Tooltip("显示器屏幕中心离地高度，单位 m")]
        public float monitorCenterHeightM = 1.10f;

        [Tooltip("是否放置大屏（可选）")]
        public bool bigScreenEnabled = true;

        [Tooltip("大屏可视尺寸（宽 × 高），单位 m，约 86 英寸")]
        public Vector2 bigScreenSizeM = new Vector2(1.90f, 1.07f);

        [Tooltip("大屏底边离地高度，单位 m")]
        public float bigScreenBottomHeightM = 1.00f;
    }
}
