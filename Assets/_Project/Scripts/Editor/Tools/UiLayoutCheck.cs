using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace RivetSim.Editor.Tools
{
    /// <summary>
    /// 界面布局检查（M6 提取，M7 复用）：把面板临时渲染到指定分辨率的 RenderTexture，再核对元素是否超出屏幕、被裁切或文字溢出。
    /// 用法：Begin → 等布局稳定数帧 → Check → End。
    /// </summary>
    public static class UiLayoutCheck
    {
        private const float Tolerance = 1.5f;
        private static readonly string[] ClipClasses = { "dc-body", "dc-left", "dc-right", "dc-explain-card", "dc-log", "cell" };
        private static RenderTexture _rt;
        private static PanelSettings _panel;
        private static RenderTexture _panelOriginal;

        /// <summary>报告表头（Markdown）。</summary>
        public const string TableHeader = "| 分辨率 | 界面 | 检查元素 | 问题 | 省略号截断（允许） | 结果 |\n|---|---|---|---|---|---|";

        /// <summary>是否已开始（尚未 End）。</summary>
        public static bool IsActive => _rt != null;

        /// <summary>把面板改为渲染到指定分辨率。</summary>
        public static void Begin(PanelSettings panel, Vector2Int size)
        {
            _rt = new RenderTexture(size.x, size.y, 24);
            _panel = panel;
            _panelOriginal = panel.targetTexture;
            panel.targetTexture = _rt;
        }

        /// <summary>还原面板的渲染目标并释放临时贴图。</summary>
        public static void End()
        {
            if (_panel != null) _panel.targetTexture = _panelOriginal;
            _panel = null;
            _panelOriginal = null;
            if (_rt == null) return;
            _rt.Release();
            Object.DestroyImmediate(_rt);
            _rt = null;
        }

        /// <summary>检查 root 下全部可见元素，返回报告表格的一行。</summary>
        public static string Check(VisualElement root, Vector2Int size, string name, out int issueCount)
        {
            var issues = new List<string>();
            int checkedCount = 0, truncated = 0;
            Rect rootRect = root.worldBound;
            foreach (VisualElement e in root.Query<VisualElement>().ToList())
            {
                if (!IsShown(e, root)) continue;
                checkedCount++;
                Rect b = e.worldBound;
                if (b.width > 0f && (b.xMin < rootRect.xMin - Tolerance || b.yMin < rootRect.yMin - Tolerance ||
                                     b.xMax > rootRect.xMax + Tolerance || b.yMax > rootRect.yMax + Tolerance))
                    issues.Add($"{Describe(e)} 超出屏幕 {b}");
                VisualElement clip = ClippingAncestor(e, root);
                if (clip != null && b.height > 0f && b.yMax > clip.worldBound.yMax + Tolerance)
                    issues.Add($"{Describe(e)} 被 {Describe(clip)} 裁切（底 {b.yMax:0} > {clip.worldBound.yMax:0}）");
                if (e is Label label && !string.IsNullOrEmpty(label.text))
                {
                    bool ellipsis = label.resolvedStyle.whiteSpace == WhiteSpace.NoWrap && label.resolvedStyle.textOverflow == TextOverflow.Ellipsis;
                    Rect c = label.contentRect;
                    Vector2 need = label.resolvedStyle.whiteSpace == WhiteSpace.NoWrap
                        ? label.MeasureTextSize(label.text, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined)
                        : label.MeasureTextSize(label.text, c.width, VisualElement.MeasureMode.Exactly, 0f, VisualElement.MeasureMode.Undefined);
                    bool overflow = need.x > c.width + Tolerance || need.y > c.height + Tolerance;
                    if (overflow && ellipsis) truncated++;
                    else if (overflow) issues.Add($"{Describe(e)} 文字溢出（需 {need.x:0}×{need.y:0}，实 {c.width:0}×{c.height:0}）「{Short(label.text)}」");
                }
            }
            issueCount = issues.Count;
            return $"| {size.x}×{size.y} | {name} | {checkedCount} | {issues.Count} | {truncated} | {(issues.Count == 0 ? "无溢出" : string.Join("<br>", issues.Take(6)))} |";
        }

        private static bool IsShown(VisualElement e, VisualElement root)
        {
            for (VisualElement p = e; p != null; p = p.parent)
            {
                if (p.resolvedStyle.display == DisplayStyle.None || p.resolvedStyle.visibility == Visibility.Hidden) return false;
                if (p == root) return true;
            }
            return true;
        }

        private static VisualElement ClippingAncestor(VisualElement e, VisualElement root)
        {
            for (VisualElement p = e.parent; p != null && p != root; p = p.parent)
                if (ClipClasses.Any(p.ClassListContains)) return p;
            return null;
        }

        private static string Describe(VisualElement e) => string.IsNullOrEmpty(e.name) ? $"{e.GetType().Name}.{e.GetClasses().FirstOrDefault()}" : e.name;

        private static string Short(string s) => s.Length > 24 ? s.Substring(0, 24).Replace('\n', ' ') + "…" : s.Replace('\n', ' ');
    }
}
