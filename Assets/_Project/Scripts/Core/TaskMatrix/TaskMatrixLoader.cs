using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace RivetSim.Core.TaskMatrix
{
    /// <summary>
    /// 用 Newtonsoft Json 读取 SOP 任务矩阵 JSON 并校验：
    /// 单元按“阶段数 × 主体数”齐全、id 不重复、回路阶段存在、active 和 conditional 单元的执行器键名非空。
    /// 本类只解析字符串，不涉及文件路径；运行时由配置中的 TextAsset 提供 JSON。
    /// </summary>
    public static class TaskMatrixLoader
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            Converters = { new StringEnumConverter { AllowIntegerValues = false } }
        };

        /// <summary>解析并校验 JSON 文本。不会抛出异常，问题记录在结果的 Errors 中。</summary>
        public static TaskMatrixLoadResult LoadFromJson(string json)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (string.IsNullOrWhiteSpace(json))
            {
                errors.Add("JSON 文本为空。");
                return new TaskMatrixLoadResult(null, errors, warnings);
            }

            TaskMatrixData data;
            try
            {
                data = JsonConvert.DeserializeObject<TaskMatrixData>(json, Settings);
            }
            catch (Exception ex) when (ex is JsonException || ex is ArgumentException)
            {
                errors.Add($"JSON 解析失败：{ex.Message}");
                return new TaskMatrixLoadResult(null, errors, warnings);
            }

            if (data == null)
            {
                errors.Add("JSON 解析结果为空。");
                return new TaskMatrixLoadResult(null, errors, warnings);
            }

            Validate(data, errors, warnings);
            return new TaskMatrixLoadResult(data, errors, warnings);
        }

        private static void Validate(TaskMatrixData data, List<string> errors, List<string> warnings)
        {
            if (data.RowCount == 0) errors.Add("phases 为空。");
            if (data.ColCount == 0) errors.Add("actors 为空。");
            CheckUniqueIds(data.Phases, p => p?.Id, "阶段", errors);
            CheckUniqueIds(data.Actors, a => a?.Id, "主体", errors);
            CheckUniqueIds(data.Loops, l => l?.Id, "回路", errors);
            CheckUniqueIds(data.Conditions.Items, c => c?.Id, "条件", errors);

            ValidateCells(data, errors, warnings);
            ValidateLoops(data, errors);
        }

        private static void ValidateCells(TaskMatrixData data, List<string> errors, List<string> warnings)
        {
            var ids = new HashSet<string>();
            var positions = new HashSet<(int, int)>();

            foreach (CellDef cell in data.Cells)
            {
                if (cell == null)
                {
                    errors.Add("cells 中存在空项。");
                    continue;
                }

                string name = string.IsNullOrEmpty(cell.Id) ? $"[{cell.Row},{cell.Col}]" : cell.Id;

                if (string.IsNullOrEmpty(cell.Id)) errors.Add($"单元 {name} 缺少 id。");
                else if (!ids.Add(cell.Id)) errors.Add($"单元 id 重复：{cell.Id}");

                if (cell.Row < 1 || cell.Row > data.RowCount || cell.Col < 1 || cell.Col > data.ColCount)
                    errors.Add($"单元 {name} 的行列 ({cell.Row},{cell.Col}) 超出 {data.RowCount}×{data.ColCount} 范围。");
                else if (!positions.Add((cell.Row, cell.Col)))
                    errors.Add($"单元 {name} 的位置 ({cell.Row},{cell.Col}) 重复。");

                string expectedId = $"t({cell.Row},{cell.Col})";
                if (!string.IsNullOrEmpty(cell.Id) && cell.Id != expectedId)
                    warnings.Add($"单元 {cell.Id} 的 id 与行列不一致（应为 {expectedId}）。");

                bool executes = cell.Kind == CellKind.Active || cell.Kind == CellKind.Conditional;
                if (executes && string.IsNullOrWhiteSpace(cell.Executor))
                    errors.Add($"单元 {name}（{cell.Kind}）缺少执行器键名。");
                if (cell.Kind == CellKind.Standby && !string.IsNullOrWhiteSpace(cell.Executor))
                    warnings.Add($"standby 单元 {name} 配置了执行器 {cell.Executor}，将被忽略。");

                if (cell.Kind == CellKind.Conditional && string.IsNullOrWhiteSpace(cell.Trigger))
                    errors.Add($"条件单元 {name} 缺少 trigger。");
                if (cell.OnComplete == CellCompletionAction.RetryPhase && cell.MaxRetries <= 0)
                    warnings.Add($"单元 {name} 的 onComplete 为 retryPhase，但 maxRetries ≤ 0，不会重试。");

                foreach (string conditionId in cell.ConditionRefs)
                {
                    if (data.FindCondition(conditionId) == null)
                        errors.Add($"单元 {name} 引用了不存在的条件 {conditionId}。");
                }
            }

            for (int row = 1; row <= data.RowCount; row++)
            {
                for (int col = 1; col <= data.ColCount; col++)
                {
                    if (!positions.Contains((row, col))) errors.Add($"缺少单元 t({row},{col})。");
                }
            }
        }

        private static void ValidateLoops(TaskMatrixData data, List<string> errors)
        {
            foreach (LoopDef loop in data.Loops)
            {
                if (loop == null)
                {
                    errors.Add("loops 中存在空项。");
                    continue;
                }
                if (data.FindPhase(loop.From) == null) errors.Add($"回路 {loop.Id} 的起点阶段 {loop.From} 不存在。");
                if (data.FindPhase(loop.To) == null) errors.Add($"回路 {loop.Id} 的终点阶段 {loop.To} 不存在。");
            }
        }

        private static void CheckUniqueIds<T>(IReadOnlyList<T> items, Func<T, string> getId, string what, List<string> errors)
        {
            var seen = new HashSet<string>();
            foreach (T item in items)
            {
                string id = getId(item);
                if (string.IsNullOrEmpty(id)) errors.Add($"{what}缺少 id。");
                else if (!seen.Add(id)) errors.Add($"{what} id 重复：{id}");
            }
        }
    }
}
