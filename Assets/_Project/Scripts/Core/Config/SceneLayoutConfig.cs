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
        public string notes = "v1 预估值，见 Docs/Architecture.md §4、§6.1 与 ADR-010；M2 第一部分（2026-10-18）按模型机 1:1.5、厂房 64 × 44 × 12 m 重排三区、障碍物、充电位、起始位、巡游路线与演示区，经用户确认；M2 第三部分（2026-10-18）按导入的 A320 模型实测更新翼展 22.73 m、机高 7.92 m、机身直径 2.63 m、中线离地 2.63 m、下缘离地 1.25 m（机长 25.05 m 为缩放基准）。";

        [Header("大厅")]
        [Tooltip("大厅尺寸（长 × 宽 × 高），单位 m；长沿世界 X，宽沿世界 Z")]
        public Vector3 hallSizeM = new Vector3(64f, 44f, 12f);

        [Tooltip("作业通道：模型机外轮廓到障碍物的最小距离，单位 m")]
        public float aisleClearanceM = 4.0f;

        [Header("三区")]
        [Tooltip("作业区矩形（模型机 + 检测区铆钉）")]
        public ZoneRect workZone = new ZoneRect(new Vector2(8f, 0f), new Vector2(32f, 33.2f));

        [Tooltip("移动区矩形（AGV 建图 / 规划 / 移动；包含作业区）")]
        public ZoneRect mobilityZone = new ZoneRect(new Vector2(6f, 0f), new Vector2(52f, 44f));

        [Tooltip("演示区矩形（演示台 + 电脑屏幕）")]
        public ZoneRect demoZone = new ZoneRect(new Vector2(-27.5f, 0f), new Vector2(9f, 44f));

        [Header("移动区设施")]
        [Tooltip("障碍物列表；O1、O2 之间形成窄通道")]
        public ObstacleSpec[] obstacles =
        {
            new ObstacleSpec("O1 货箱堆", ObstacleKind.CrateStack, new ZoneRect(new Vector2(-15.0f, -3.4f), new Vector2(1.0f, 5.0f)), 1.0f),
            new ObstacleSpec("O2 货箱堆", ObstacleKind.CrateStack, new ZoneRect(new Vector2(-15.0f, 3.4f), new Vector2(1.0f, 5.0f)), 1.0f),
            new ObstacleSpec("O3 工具车", ObstacleKind.ToolCart, new ZoneRect(new Vector2(25.5f, 6.0f), new Vector2(1.2f, 0.6f)), 0.95f),
            new ObstacleSpec("O4 工具车", ObstacleKind.ToolCart, new ZoneRect(new Vector2(0.0f, 17.5f), new Vector2(1.2f, 0.6f)), 0.95f),
            new ObstacleSpec("O5 锥桶", ObstacleKind.Cone, new ZoneRect(new Vector2(16.0f, -17.5f), new Vector2(0.4f, 0.4f)), 0.7f),
        };

        [Tooltip("充电位：AGV 充电时的位姿（车尾对充电桩）")]
        public PlanarPose chargingDockPose = new PlanarPose(new Vector2(-18.4f, -12.0f), 90f);

        [Tooltip("充电位到作业区边界的最小距离，单位 m（ADR-010 / PromptTemplates M2 1-2）")]
        public float chargingDockMinToWorkZoneM = 6.0f;

        [Tooltip("AGV 起始位姿")]
        public PlanarPose startPose = new PlanarPose(new Vector2(-18.0f, 0.0f), 90f);

        [Tooltip("巡游建图路线途经点（x = 世界 X，y = 世界 Z），单位 m；穿过窄通道后绕作业区外环一圈")]
        public Vector2[] mappingRouteM =
        {
            new Vector2(-18f, 0f), new Vector2(-10.5f, 0f), new Vector2(-10.5f, -19.3f),
            new Vector2(28f, -19.3f), new Vector2(28f, 19.3f), new Vector2(-10.5f, 19.3f),
            new Vector2(-10.5f, 0f), new Vector2(-18f, 0f),
        };

        [Header("模型机（A320-200 外形）")]
        [Tooltip("模型机包围盒中心的地面投影（x = 世界 X，y = 世界 Z），单位 m；机身沿世界 Z，机头朝 +Z")]
        public Vector2 aircraftCenterM = new Vector2(8f, 0f);

        [Tooltip("模型机相对真机的缩比（1:1.5 ≈ 0.6667）")]
        public float aircraftScale = 1f / 1.5f;

        [Tooltip("机长，单位 m")]
        public float aircraftLengthM = 25.05f;

        [Tooltip("翼展，单位 m")]
        public float aircraftWingspanM = 22.73f;

        [Tooltip("机高，单位 m")]
        public float aircraftHeightM = 7.92f;

        [Tooltip("机身直径，单位 m")]
        public float fuselageDiameterM = 2.63f;

        [Tooltip("机身下缘离地高度，单位 m（A320 模型实测）")]
        public float fuselageBottomHeightM = 1.25f;

        [Tooltip("机身中线离地高度，单位 m（A320 模型实测）")]
        public float fuselageCenterlineHeightM = 2.63f;

        [Header("演示区")]
        [Tooltip("演示台位姿：偏航角为显示器朝向（90° = 显示器朝 +X，面向大厅）")]
        public PlanarPose demoDeskPose = new PlanarPose(new Vector2(-28.5f, 0f), 90f);

        [Tooltip("大屏位姿（屏幕朝向），挂在西墙")]
        public PlanarPose bigScreenPose = new PlanarPose(new Vector2(-31.8f, 0f), 90f);

        [Tooltip("入口门中心的世界 X（门在南墙 Z = −宽/2 上），单位 m")]
        public float entranceDoorCenterXM = -27.5f;

        [Tooltip("入口门宽度，单位 m")]
        public float entranceDoorWidthM = 3.0f;

        [Tooltip("入口门高度，单位 m")]
        public float entranceDoorHeightM = 3.5f;

        [Tooltip("演示台前缘（操作侧）到演示区边界的观众站位最小进深，单位 m（PromptTemplates M2 1-2）")]
        public float demoAudienceDepthMinM = 4.0f;

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
