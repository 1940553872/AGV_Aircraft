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
    }
}
