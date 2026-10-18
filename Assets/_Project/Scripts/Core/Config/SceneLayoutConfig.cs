using UnityEngine;

namespace RivetSim.Core.Config
{
    /// <summary>
    /// 场景布局配置（CFG_SceneLayout）：大厅尺寸、三区矩形、障碍物、充电位、巡游建图路线、模型机外形与演示区尺寸。
    /// 世界坐标：1 单位 = 1 m，Y 轴向上，原点在大厅地面中心，模型机机头朝 +Z；大厅长边沿世界 X，演示区在 −X 一端。
    /// 尺寸向量按（长, 宽, 高）存放，与 Unity 轴的对应见 <see cref="SizeAxes"/>（ADR-008）。
    /// </summary>
    [CreateAssetMenu(fileName = "CFG_SceneLayout", menuName = "RivetSim/Config/Scene Layout")]
    public sealed class SceneLayoutConfig : ScriptableObject
    {
        [TextArea(2, 6)]
        [Tooltip("参数来源与修改说明")]
        public string notes = "v1 预估值，见 Docs/Architecture.md §4、§6.1；三区矩形、障碍物、充电位、起始位、巡游路线与演示区摆放于 M1（2026-10-18）经用户确认。";

        [Header("大厅")]
        [Tooltip("大厅尺寸（长 × 宽 × 高），单位 m；长沿世界 X，宽沿世界 Z")]
        public Vector3 hallSizeM = new Vector3(40f, 24f, 10f);

        [Tooltip("作业通道：模型机外轮廓到障碍物的最小距离，单位 m")]
        public float aisleClearanceM = 2.5f;

        [Header("三区")]
        [Tooltip("作业区矩形（模型机 + 检测区铆钉）")]
        public ZoneRect workZone = new ZoneRect(new Vector2(3.5f, 0f), new Vector2(14f, 14f));

        [Tooltip("移动区矩形（AGV 建图 / 规划 / 移动；包含作业区）")]
        public ZoneRect mobilityZone = new ZoneRect(new Vector2(3.5f, 0f), new Vector2(33f, 24f));

        [Tooltip("演示区矩形（演示台 + 电脑屏幕）")]
        public ZoneRect demoZone = new ZoneRect(new Vector2(-16.5f, 0f), new Vector2(7f, 24f));

        [Header("移动区设施")]
        [Tooltip("障碍物列表；O1、O2 之间形成窄通道")]
        public ObstacleSpec[] obstacles =
        {
            new ObstacleSpec("O1 货箱堆", ObstacleKind.CrateStack, new ZoneRect(new Vector2(-6.0f, -2.8f), new Vector2(1.0f, 4.0f)), 1.0f),
            new ObstacleSpec("O2 货箱堆", ObstacleKind.CrateStack, new ZoneRect(new Vector2(-6.0f, 2.8f), new Vector2(1.0f, 4.0f)), 1.0f),
            new ObstacleSpec("O3 工具车", ObstacleKind.ToolCart, new ZoneRect(new Vector2(13.5f, 5.0f), new Vector2(1.2f, 0.6f)), 0.95f),
            new ObstacleSpec("O4 工具车", ObstacleKind.ToolCart, new ZoneRect(new Vector2(0.0f, 9.5f), new Vector2(1.2f, 0.6f)), 0.95f),
            new ObstacleSpec("O5 锥桶", ObstacleKind.Cone, new ZoneRect(new Vector2(12.5f, -8.5f), new Vector2(0.4f, 0.4f)), 0.7f),
        };

        [Tooltip("充电位：AGV 充电时的位姿（车尾对充电桩）")]
        public PlanarPose chargingDockPose = new PlanarPose(new Vector2(-11.4f, -6.0f), 90f);

        [Tooltip("AGV 起始位姿")]
        public PlanarPose startPose = new PlanarPose(new Vector2(-10.0f, 0.0f), 90f);

        [Tooltip("巡游建图路线途经点（x = 世界 X，y = 世界 Z），单位 m")]
        public Vector2[] mappingRouteM =
        {
            new Vector2(-10f, 0f), new Vector2(-6f, 0f), new Vector2(-2f, 0f),
            new Vector2(-2f, -5.5f), new Vector2(9f, -5.5f), new Vector2(9f, 5.5f),
            new Vector2(-2f, 5.5f), new Vector2(-2f, 0f), new Vector2(-6f, 0f), new Vector2(-10f, 0f),
        };

        [Header("模型机（A320-200 外形）")]
        [Tooltip("模型机包围盒中心的地面投影（x = 世界 X，y = 世界 Z），单位 m；机身沿世界 Z，机头朝 +Z")]
        public Vector2 aircraftCenterM = new Vector2(3.5f, 0f);

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
        [Tooltip("演示台位姿：偏航角为显示器朝向（90° = 显示器朝 +X，面向大厅）")]
        public PlanarPose demoDeskPose = new PlanarPose(new Vector2(-15.0f, 0f), 90f);

        [Tooltip("大屏位姿（屏幕朝向），挂在西墙")]
        public PlanarPose bigScreenPose = new PlanarPose(new Vector2(-19.8f, 0f), 90f);

        [Tooltip("入口门中心的世界 X（门在南墙 Z = −宽/2 上），单位 m")]
        public float entranceDoorCenterXM = -16.5f;

        [Tooltip("入口门宽度，单位 m")]
        public float entranceDoorWidthM = 3.0f;

        [Tooltip("入口门高度，单位 m")]
        public float entranceDoorHeightM = 3.5f;

        [Tooltip("演示台尺寸（长 × 宽 × 高），单位 m；长沿桌面左右，宽为进深")]
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
