using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using RivetSim.Core.TaskMatrix;
using UnityEditor;
using UnityEngine;

namespace RivetSim.Tests.EditMode
{
    /// <summary>
    /// TaskMatrixLoader 测试：读取 SOP_TaskMatrix_v1.json 的结构与计数，以及校验规则的反例。
    /// </summary>
    public sealed class TaskMatrixLoaderTests
    {
        private const string JsonPath = "Assets/_Project/Config/TaskMatrix/SOP_TaskMatrix_v1.json";

        private string _json;
        private TaskMatrixLoadResult _result;

        [OneTimeSetUp]
        public void LoadOnce()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(JsonPath);
            Assert.That(asset, Is.Not.Null, $"找不到 {JsonPath}");
            _json = asset.text;
            _result = TaskMatrixLoader.LoadFromJson(_json);
        }

        [Test]
        public void Load_SucceedsWithoutErrors()
        {
            Assert.That(_result.Errors, Is.Empty, string.Join("\n", _result.Errors));
            Assert.That(_result.Success, Is.True);
        }

        [Test]
        public void Matrix_Has6PhasesAnd8Actors()
        {
            Assert.That(_result.Data.RowCount, Is.EqualTo(6));
            Assert.That(_result.Data.ColCount, Is.EqualTo(8));
            Assert.That(_result.Data.Phases.Select(p => p.Code), Is.EqualTo(new[] { "T1", "T2", "T3", "T4", "T5", "T6" }));
            Assert.That(_result.Data.Actors.Select(a => a.Code), Is.EqualTo(new[] { "S1", "S2", "S3", "S4", "S5", "S6", "S7", "S8" }));
        }

        [Test]
        public void Matrix_Has48CellsCoveringEveryPosition()
        {
            TaskMatrixData data = _result.Data;
            Assert.That(data.Cells.Count, Is.EqualTo(48));
            for (int row = 1; row <= 6; row++)
            {
                for (int col = 1; col <= 8; col++)
                {
                    CellDef cell = data.GetCell(row, col);
                    Assert.That(cell, Is.Not.Null, $"缺少 t({row},{col})");
                    Assert.That(cell.Id, Is.EqualTo($"t({row},{col})"));
                }
            }
        }

        [Test]
        public void Matrix_Has3Loops()
        {
            TaskMatrixData data = _result.Data;
            Assert.That(data.Loops.Count, Is.EqualTo(3));

            LoopDef next = data.Loops.Single(l => l.Id == "nextRivet");
            Assert.That((next.From, next.To, next.Style), Is.EqualTo(("p6", "p4", LoopStyle.Solid)));

            LoopDef sameRegion = data.Loops.Single(l => l.Id == "nextStationSameRegion");
            Assert.That((sameRegion.From, sameRegion.To, sameRegion.Style), Is.EqualTo(("p6", "p2", LoopStyle.Dashed)));

            LoopDef nextRegion = data.Loops.Single(l => l.Id == "nextRegion");
            Assert.That((nextRegion.From, nextRegion.To, nextRegion.Style), Is.EqualTo(("p6", "p1", LoopStyle.Dashed)));
        }

        [Test]
        public void ExampleCell_IsT54Only()
        {
            string[] examples = _result.Data.Cells.Where(c => c.IsExampleCell).Select(c => c.Id).ToArray();
            Assert.That(examples, Is.EqualTo(new[] { "t(5,4)" }));
        }

        [Test]
        public void ConditionalCells_AreT18_T56_T57_T62()
        {
            string[] conditional = _result.Data.Cells.Where(c => c.Kind == CellKind.Conditional).Select(c => c.Id).ToArray();
            Assert.That(conditional, Is.EquivalentTo(new[] { "t(1,8)", "t(5,6)", "t(5,7)", "t(6,2)" }));
            Assert.That(_result.Data.Cells.Where(c => c.Kind == CellKind.Conditional).All(c => !string.IsNullOrEmpty(c.Trigger)), Is.True);
        }

        [Test]
        public void CellKindCounts_Standby9_Active35()
        {
            Assert.That(_result.Data.Cells.Count(c => c.Kind == CellKind.Standby), Is.EqualTo(9));
            Assert.That(_result.Data.Cells.Count(c => c.Kind == CellKind.Active), Is.EqualTo(35));
        }

        [Test]
        public void ConditionRefs_MatchConditionChecks()
        {
            TaskMatrixData data = _result.Data;
            Assert.That(data.FindCell("t(4,7)").ConditionRefs, Is.EqualTo(new[] { "C1" }));
            Assert.That(data.FindCell("t(5,4)").ConditionRefs, Is.EqualTo(new[] { "C2", "C3", "C4", "C5", "C6", "C7", "C8" }));
            Assert.That(data.FindCell("t(6,4)").ConditionRefs, Is.EqualTo(new[] { "C9", "C10", "C11", "C12" }));
            Assert.That(data.Conditions.Items.Count, Is.EqualTo(12));
        }

        [Test]
        public void T61_RunsOnlyOnLastRivetInStation()
        {
            CellDef cell = _result.Data.FindCell("t(6,1)");
            Assert.That(cell.Kind, Is.EqualTo(CellKind.Active));
            Assert.That(cell.RunWhen, Is.EqualTo("lastRivetInStation"));
        }

        [Test]
        public void RetryCells_HaveRetryPhaseAndMaxRetries()
        {
            CellDef forceExceeded = _result.Data.FindCell("t(5,6)");
            Assert.That((forceExceeded.OnComplete, forceExceeded.MaxRetries), Is.EqualTo((CellCompletionAction.RetryPhase, 1)));
            CellDef reattach = _result.Data.FindCell("t(5,7)");
            Assert.That((reattach.OnComplete, reattach.MaxRetries), Is.EqualTo((CellCompletionAction.RetryPhase, 2)));
        }

        [Test]
        public void ExecutingCells_HaveExecutorKeys_StandbyCellsDoNot()
        {
            foreach (CellDef cell in _result.Data.Cells)
            {
                if (cell.Kind == CellKind.Standby) Assert.That(cell.Executor, Is.Null.Or.Empty, cell.Id);
                else Assert.That(cell.Executor, Is.Not.Null.And.Not.Empty, cell.Id);
            }
            Assert.That(_result.Data.GetExecutorKeys(), Is.Unique);
        }

        [Test]
        public void Validation_ReportsMissingCell()
        {
            JObject root = JObject.Parse(_json);
            ((JArray)root["cells"]).RemoveAt(0);
            TaskMatrixLoadResult result = TaskMatrixLoader.LoadFromJson(root.ToString());
            Assert.That(result.Success, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("t(1,1)"));
        }

        [Test]
        public void Validation_ReportsDuplicateId()
        {
            JObject root = JObject.Parse(_json);
            root["cells"][1]["id"] = "t(1,1)";
            TaskMatrixLoadResult result = TaskMatrixLoader.LoadFromJson(root.ToString());
            Assert.That(result.Success, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("重复"));
        }

        [Test]
        public void Validation_ReportsLoopToMissingPhase()
        {
            JObject root = JObject.Parse(_json);
            root["loops"][0]["to"] = "p9";
            TaskMatrixLoadResult result = TaskMatrixLoader.LoadFromJson(root.ToString());
            Assert.That(result.Success, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("p9"));
        }

        [Test]
        public void Validation_ReportsActiveCellWithoutExecutor()
        {
            JObject root = JObject.Parse(_json);
            root["cells"][0]["executor"] = null;
            TaskMatrixLoadResult result = TaskMatrixLoader.LoadFromJson(root.ToString());
            Assert.That(result.Success, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("执行器"));
        }

        [Test]
        public void Validation_ReportsInvalidJsonWithoutThrowing()
        {
            TaskMatrixLoadResult result = null;
            Assert.DoesNotThrow(() => result = TaskMatrixLoader.LoadFromJson("{ not json"));
            Assert.That(result.Success, Is.False);
            Assert.That(result.Data, Is.Null);
        }
    }
}
