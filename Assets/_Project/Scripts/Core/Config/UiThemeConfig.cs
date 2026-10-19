using UnityEngine;

namespace RivetSim.Core.Config
{
    /// <summary>
    /// 演示界面主题（CFG_UiTheme）：配色、字号、屏幕分辨率与布局比例（Architecture §7，深色工业控制台风格）。
    /// </summary>
    [CreateAssetMenu(fileName = "CFG_UiTheme", menuName = "RivetSim/Config/UI Theme")]
    public sealed class UiThemeConfig : ScriptableObject
    {
        [TextArea(2, 6)]
        [Tooltip("参数来源与修改说明")]
        public string notes = "见 Docs/Architecture.md §7 演示界面视觉规范。";

        [Header("配色")]
        [Tooltip("背景 #0E1621")]
        public Color background = new Color32(0x0E, 0x16, 0x21, 0xFF);

        [Tooltip("面板 #152232")]
        public Color panel = new Color32(0x15, 0x22, 0x32, 0xFF);

        [Tooltip("分隔线 #263A52")]
        public Color divider = new Color32(0x26, 0x3A, 0x52, 0xFF);

        [Tooltip("正文 #E6EEF7")]
        public Color textPrimary = new Color32(0xE6, 0xEE, 0xF7, 0xFF);

        [Tooltip("次要文字 #8FA3B8")]
        public Color textSecondary = new Color32(0x8F, 0xA3, 0xB8, 0xFF);

        [Tooltip("待命灰 #566577")]
        public Color standbyGray = new Color32(0x56, 0x65, 0x77, 0xFF);

        [Tooltip("强调（标题、回路箭头）#2EC4E6")]
        public Color accent = new Color32(0x2E, 0xC4, 0xE6, 0xFF);

        [Tooltip("正在执行（粗边框）#FF9F1C")]
        public Color running = new Color32(0xFF, 0x9F, 0x1C, 0xFF);

        [Tooltip("完成 #3DD68C")]
        public Color done = new Color32(0x3D, 0xD6, 0x8C, 0xFF);

        [Tooltip("条件激活 #FFC857")]
        public Color conditionTriggered = new Color32(0xFF, 0xC8, 0x57, 0xFF);

        [Tooltip("失败 #FF5D5D")]
        public Color failed = new Color32(0xFF, 0x5D, 0x5D, 0xFF);

        [Header("字号（基于 1920×1080，单位 px）")]
        [Tooltip("标题")]
        public int titleFontSize = 28;

        [Tooltip("表头")]
        public int headerFontSize = 18;

        [Tooltip("矩阵单元")]
        public int cellFontSize = 16;

        [Tooltip("执行情况说明")]
        public int explainFontSize = 18;

        [Tooltip("事件日志")]
        public int logFontSize = 14;

        [Header("屏幕与布局")]
        [Tooltip("演示屏渲染分辨率，单位 px")]
        public Vector2Int screenResolutionPx = new Vector2Int(1920, 1080);

        [Tooltip("任务矩阵区域占屏幕宽度的比例")]
        [Range(0f, 1f)]
        public float matrixWidthRatio = 0.62f;

        [Tooltip("底栏日志显示条数")]
        public int logVisibleCount = 6;

        [Header("单元动效（M6）")]
        [Tooltip("Running 单元呼吸光周期，单位 s")]
        public float runningBreathPeriodS = 1.6f;

        [Tooltip("Running 单元呼吸光最低不透明度")]
        [Range(0f, 1f)]
        public float runningBreathMinAlpha = 0.45f;

        [Tooltip("Running 单元左上角进度条往返周期，单位 s（单元无预计时长，显示为循环进度）")]
        public float runningProgressPeriodS = 1.5f;

        [Tooltip("Triggered 单元闪烁次数")]
        public int triggeredFlashCount = 2;

        [Tooltip("Triggered 单元闪烁总时长，单位 s（之后按实际状态显示）")]
        public float triggeredFlashDurationS = 1.2f;

        [Tooltip("回路箭头被走到后的高亮时长，单位 s（实际时间）")]
        public float loopHighlightS = 3f;

        [Tooltip("虚线边框的线段长与间隔，单位 px")]
        public Vector2 dashPatternPx = new Vector2(8f, 5f);

        [Header("小地图（M6）")]
        [Tooltip("栅格贴图刷新间隔，单位 s（实际时间）")]
        public float miniMapRefreshIntervalS = 0.5f;

        [Tooltip("AGV 位姿变化超过该像素才重绘小地图叠加层，单位 px")]
        public float miniMapRepaintThresholdPx = 0.5f;

        [Tooltip("小地图 AGV 标记尺寸（长 × 宽），单位 px（M7 放大）")]
        public Vector2 miniMapAgvMarkerSizePx = new Vector2(22f, 14f);

        [Header("相机（M6）")]
        [Tooltip("机位切换平滑时长，单位 s（实际时间）")]
        public float cameraBlendS = 0.8f;

        [Tooltip("自动演示阶段切换时显示演示台的时长，单位 s（实际时间）")]
        public float demoStationHoldS = 2f;

        [Tooltip("只在阶段切换导致机位变化时显示演示台（避免逐钉循环 T4～T6 频繁切到演示台）")]
        public bool demoStationOnPresetChangeOnly = true;

        [Tooltip("末端特写：相机到探头 TCP 的水平距离（位于 AGV 一侧），单位 m")]
        public float closeupDistanceM = 1.4f;

        [Tooltip("末端特写：相机高于探头 TCP 的高度，单位 m")]
        public float closeupHeightM = 0.5f;

        [Tooltip("末端特写：相机方位相对“探头 → AGV”方向的偏转，单位 °（斜侧视角，避免机械臂挡住探头）")]
        public float closeupYawOffsetDeg = 35f;

        [Tooltip("跟随 AGV：注视点高于 AGV 原点的高度，单位 m")]
        public float followLookHeightM = 0.6f;

        [Tooltip("CamAnchor_4 演示台机位：相机相对演示台位姿的位置（x 右、y 上、z 朝观众），单位 m（M7；大屏放大 3 倍后后移，使大屏与桌面显示器同时入画）")]
        public Vector3 demoCameraOffsetM = new Vector3(0.5f, 1.7f, 2.6f);

        [Tooltip("CamAnchor_4 演示台机位：注视点相对演示台位姿的位置，单位 m（对准大屏中部偏上）")]
        public Vector3 demoCameraLookOffsetM = new Vector3(0f, 2.2f, -3.3f);

        [Tooltip("自动演示时各阶段（p1～p6 顺序）使用的机位编号：1 总览、2 跟随 AGV、3 末端特写、4 演示台")]
        public int[] phaseCameraPresets = { 2, 2, 3, 3, 3, 3 };

        [Header("检测机位防遮挡（M8）")]
        [Tooltip("从该阶段行号起（含）“2 跟随”“3 特写”改为以当前铆钉法向为参照取景（3 = T3）")]
        public int rivetViewFromRow = 3;

        [Tooltip("“3 特写”相机到接触点的距离，单位 m")]
        [Min(0.2f)]
        public float rivetCloseupDistanceM = 1.3f;

        [Tooltip("“2 跟随”在检测作业时到接触点的距离，单位 m")]
        [Min(0.5f)]
        public float rivetFollowDistanceM = 3.4f;

        [Tooltip("相机相对铆钉排的高度偏移（与铆钉排同高时取小值），单位 m")]
        public float rivetViewElevationM = 0.15f;

        [Tooltip("候选取景角：绕竖直轴相对铆钉外法向（水平投影）向机身切向偏转的角度，按优先顺序，单位 °")]
        public float[] rivetViewYawCandidatesDeg = { 35f, -35f, 45f, -45f, 25f, -25f, 60f, -60f };

        [Tooltip("候选高度偏移（所有偏航角都被遮挡时依次尝试），单位 m")]
        public float[] rivetViewElevationCandidatesM = { 0.15f, 0.6f, -0.4f };

        [Tooltip("法向水平分量低于该值时（机腹铆钉）改用 AGV 指向接触点的反方向作为外侧方向")]
        [Range(0f, 1f)]
        public float rivetViewMinHorizontalNormal = 0.35f;

        [Tooltip("AGV 到当前铆钉的水平距离超过该值时（如返回充电位）恢复原跟随 / 特写机位，单位 m")]
        [Min(0.5f)]
        public float rivetViewMaxAgvDistanceM = 6f;

        [Tooltip("遮挡检查周期（实际时间），单位 s")]
        [Min(0.02f)]
        public float occlusionCheckIntervalS = 0.2f;

        [Tooltip("遮挡检查 SphereCast 半径，单位 m")]
        [Min(0f)]
        public float occlusionProbeRadiusM = 0.06f;

        [Tooltip("接触点附近不计遮挡的距离（探头与蒙皮本身），单位 m")]
        [Min(0f)]
        public float occlusionIgnoreNearTargetM = 0.25f;

        [Header("演示提示与状态视图（M8）")]
        [Tooltip("异常提示（“系统检测到异常：…，正在自动处理”）显示时长（实际时间），单位 s")]
        [Min(0.5f)]
        public float anomalyToastS = 3f;

        [Tooltip("倍速保护提示显示时长（实际时间），单位 s")]
        [Min(0.5f)]
        public float speedGuardToastS = 4f;

        [Tooltip("检测结果与设备状态视图的刷新间隔（实际时间），单位 s")]
        [Min(0.05f)]
        public float statusRefreshIntervalS = 0.25f;

        [Tooltip("不合格铆钉列表最多显示的行数（最新在上）")]
        [Min(1)]
        public int statusListRows = 12;

        [Tooltip("小地图铆钉结果点半径，单位 px")]
        [Min(0.5f)]
        public float miniMapRivetDotRadiusPx = 2.5f;
    }
}
