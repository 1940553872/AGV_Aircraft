using System.Collections.Generic;
using System.Linq;
using System.Text;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Core.TaskMatrix;
using UnityEditor;
using UnityEngine;

namespace RivetSim.Editor.Tools
{
    /// <summary>
    /// 调试菜单：读取 SOP 任务矩阵 JSON，按“阶段 × 主体”表格打印到 Console，便于与参考图核对。
    /// </summary>
    public static class TaskMatrixDebugMenu
    {
        private const string ActiveMark = "●";
        private const string StandbyMark = "○";
        private const string ConditionalMark = "◇";
        private const string ExampleMark = "★";
        private const string LineBreakReplacement = "/";

        // 全角字符（中日韩文字与全角符号）的起始码位，按 2 个字符宽度对齐。
        private const int WideCharStart = 0x2E80;

        /// <summary>读取任务矩阵并打印到 Console。</summary>
        [MenuItem("RivetSim/Debug/Print Task Matrix")]
        public static void PrintTaskMatrix()
        {
            TextAsset json = LoadTaskMatrixJson();
            if (json == null)
            {
                SimLog.Error(LogCategory.System, $"找不到任务矩阵 JSON（CFG_SimSettings 未指定，且 {ProjectPaths.TaskMatrixJson} 不存在）。");
                return;
            }

            TaskMatrixLoadResult result = TaskMatrixLoader.LoadFromJson(json.text);
            string report = BuildReport(result);
            if (result.Success) SimLog.Info(LogCategory.System, report);
            else SimLog.Error(LogCategory.System, report);
        }

        /// <summary>取 CFG_SimSettings 指定的 JSON；未指定时取默认路径。</summary>
        public static TextAsset LoadTaskMatrixJson()
        {
            var settings = AssetDatabase.LoadAssetAtPath<SimSettings>(ProjectPaths.SimSettingsAsset);
            if (settings != null && settings.taskMatrixJson != null) return settings.taskMatrixJson;
            return AssetDatabase.LoadAssetAtPath<TextAsset>(ProjectPaths.TaskMatrixJson);
        }

        /// <summary>生成矩阵核对报告（表格 + 单元附加属性 + 回路 + 图例 + 统计 + 校验结果）。</summary>
        public static string BuildReport(TaskMatrixLoadResult result)
        {
            var sb = new StringBuilder();
            TaskMatrixData data = result.Data;

            if (data != null)
            {
                sb.AppendLine($"SOP 任务矩阵：{data.Title}（{data.Id}，{data.Schema}）");
                sb.AppendLine($"标记：{ActiveMark} active  {StandbyMark} standby  {ConditionalMark} conditional  {ExampleMark} 示例单元");
                sb.AppendLine();
                AppendTable(sb, data);
                sb.AppendLine();
                AppendCellAttributes(sb, data);
                sb.AppendLine();
                AppendLoopsAndLegend(sb, data);
                sb.AppendLine();
                AppendCounts(sb, data);
            }

            sb.AppendLine(result.Success ? "校验：通过" : $"校验：{result.Errors.Count} 个错误");
            foreach (string error in result.Errors) sb.AppendLine($"  错误：{error}");
            foreach (string warning in result.Warnings) sb.AppendLine($"  警告：{warning}");
            return sb.ToString();
        }

        private static void AppendTable(StringBuilder sb, TaskMatrixData data)
        {
            var rows = new List<string[]>();
            var header = new string[data.ColCount + 1];
            header[0] = data.Notation;
            for (int col = 1; col <= data.ColCount; col++)
            {
                ActorDef actor = data.Actors[col - 1];
                header[col] = $"{actor.Id} {actor.Code} {actor.Name}";
            }
            rows.Add(header);

            for (int row = 1; row <= data.RowCount; row++)
            {
                PhaseDef phase = data.Phases[row - 1];
                var line = new string[data.ColCount + 1];
                line[0] = $"{phase.Id} {phase.Code} {phase.Name}";
                for (int col = 1; col <= data.ColCount; col++)
                {
                    line[col] = FormatCell(data.GetCell(row, col));
                }
                rows.Add(line);
            }

            int[] widths = new int[data.ColCount + 1];
            foreach (string[] r in rows)
            {
                for (int i = 0; i < r.Length; i++) widths[i] = Mathf.Max(widths[i], DisplayWidth(r[i]));
            }

            for (int r = 0; r < rows.Count; r++)
            {
                sb.Append('|');
                for (int i = 0; i < rows[r].Length; i++)
                {
                    sb.Append(' ').Append(rows[r][i]).Append(' ', widths[i] - DisplayWidth(rows[r][i])).Append(" |");
                }
                sb.AppendLine();
                if (r == 0)
                {
                    sb.Append('|');
                    foreach (int w in widths) sb.Append('-', w + 2).Append('|');
                    sb.AppendLine();
                }
            }
        }

        private static string FormatCell(CellDef cell)
        {
            if (cell == null) return "（缺失）";
            string mark = cell.Kind switch
            {
                CellKind.Active => ActiveMark,
                CellKind.Standby => StandbyMark,
                _ => ConditionalMark,
            };
            if (cell.IsExampleCell) mark += ExampleMark;
            string label = (cell.Label ?? string.Empty).Replace("\n", LineBreakReplacement);
            return $"{mark}{cell.Id} {label}";
        }

        private static void AppendCellAttributes(StringBuilder sb, TaskMatrixData data)
        {
            sb.AppendLine("单元附加属性：");
            foreach (CellDef cell in data.Cells.Where(c => c != null))
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(cell.Trigger)) parts.Add($"trigger={cell.Trigger}");
                if (cell.OnComplete != CellCompletionAction.None) parts.Add($"onComplete={cell.OnComplete}×{cell.MaxRetries}");
                if (!string.IsNullOrEmpty(cell.RunWhen)) parts.Add($"runWhen={cell.RunWhen}");
                if (cell.ConditionRefs.Count > 0) parts.Add($"conditionRefs={string.Join(",", cell.ConditionRefs)}");
                if (cell.IsExampleCell) parts.Add("示例单元");
                if (parts.Count > 0) sb.AppendLine($"  {cell.Id} [{cell.Kind}] {cell.Executor}：{string.Join("；", parts)}");
            }
        }

        private static void AppendLoopsAndLegend(StringBuilder sb, TaskMatrixData data)
        {
            sb.AppendLine("回路：");
            foreach (LoopDef loop in data.Loops.Where(l => l != null))
            {
                string from = data.FindPhase(loop.From)?.Code ?? "?";
                string to = data.FindPhase(loop.To)?.Code ?? "?";
                sb.AppendLine($"  {loop.Id}：{loop.From} {from} → {loop.To} {to}，{loop.Style}，「{loop.Label}」，when={loop.When}");
            }
            sb.AppendLine("图例：");
            foreach (LegendItem item in data.Legend.Where(l => l != null)) sb.AppendLine($"  [{item.Style}] {item.Text}");
            sb.AppendLine($"回路说明：{data.LoopNote}");
        }

        private static void AppendCounts(StringBuilder sb, TaskMatrixData data)
        {
            int Count(CellKind kind) => data.Cells.Count(c => c != null && c.Kind == kind);
            sb.AppendLine($"统计：{data.RowCount}×{data.ColCount}，单元 {data.Cells.Count} 个（active {Count(CellKind.Active)}，standby {Count(CellKind.Standby)}，conditional {Count(CellKind.Conditional)}），回路 {data.Loops.Count} 条，条件 {data.Conditions.Items.Count} 项，执行器键名 {data.GetExecutorKeys().Count} 个。");
        }

        private static int DisplayWidth(string text)
        {
            int width = 0;
            foreach (char c in text) width += c >= WideCharStart ? 2 : 1;
            return width;
        }
    }
}
