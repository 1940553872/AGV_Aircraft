using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using RivetSim.Core.Events;
using RivetSim.Core.Mission;
using RivetSim.Core.TaskMatrix;
using RivetSim.Simulation.Mission;
using UnityEngine;
using UnityEngine.TestTools;

namespace RivetSim.Tests.EditMode
{
    /// <summary>
    /// MissionOrchestrator 测试（M5）：用假执行器按真实 SOP_TaskMatrix_v1.json 运行，验证逐钉循环、换站、runWhen 跳过、
    /// 条件单元触发与 retryPhase 重试、每 N 颗计数触发，以及启动核对与依赖表。
    /// </summary>
    public sealed class MissionOrchestratorTests
    {
        private const string JsonPath = "Assets/_Project/Config/TaskMatrix/SOP_TaskMatrix_v1.json";
        private const int FrameLimit = 500;

        private TaskMatrixData _data;
        private EventBus _bus;
        private ConditionBus _conditions;
        private ExecutorRegistry _registry;
        private Dictionary<string, int> _calls;
        private List<CellStateChangedEvent> _events;

        [SetUp]
        public void SetUp()
        {
            TaskMatrixLoadResult load = TaskMatrixLoader.LoadFromJson(File.ReadAllText(JsonPath));
            Assert.That(load.Success, Is.True, string.Join("；", load.Errors));
            _data = load.Data;
            _bus = new EventBus();
            _conditions = new ConditionBus(_bus);
            _calls = new Dictionary<string, int>();
            _events = new List<CellStateChangedEvent>();
            _bus.Subscribe<CellStateChangedEvent>(e => _events.Add(e));
            _registry = new ExecutorRegistry();
            foreach (string key in _data.GetExecutorKeys()) RegisterFake(key, ExecutorResult.Done());
        }

        [TearDown]
        public void TearDown() => _conditions.Dispose();

        [UnityTest]
        public IEnumerator Loops_VisitEveryRivetStationAndRegion()
        {
            var plan = new FakePlan(new[] { new[] { 2, 3 }, new[] { 2 } });
            var box = new Box();
            _ = Run(NewOrchestrator(plan), box);
            yield return WaitDone(box);

            Assert.That(box.Result.Success, Is.True, box.Result.FailureReason);
            Assert.That(Calls("nav.globalNavigate"), Is.EqualTo(2), "每个区域一次 T1");
            Assert.That(Calls("nav.dock"), Is.EqualTo(3), "每个工位一次 T2");
            Assert.That(Calls("vision.registerDatum"), Is.EqualTo(3), "每个工位一次 T3");
            Assert.That(Calls("arm.alignWithVisionCorrection"), Is.EqualTo(7), "每颗铆钉一次 T4");
            Assert.That(Calls("nav.reverseExitAndChangeStation"), Is.EqualTo(3), "t(6,1) 只在工位最后一颗执行");
            Assert.That(Calls("data.manualInitialPose"), Is.EqualTo(0), "未触发的条件单元不执行");
            Assert.That(_events.Count(e => e.CellId == "t(6,1)" && e.State == CellState.Skipped), Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator RivetCellSequence_FollowsStateModel()
        {
            var box = new Box();
            _ = Run(NewOrchestrator(new FakePlan(new[] { new[] { 2 } })), box);
            yield return WaitDone(box);

            Assert.That(box.Result.Success, Is.True);
            List<CellState> seq = _events.Where(e => e.CellId == "t(5,4)" && e.RivetIndex == 0).Select(e => e.State).ToList();
            CollectionAssert.AreEqual(new[] { CellState.Running, CellState.Done }, seq, "运行 → 完成");
            Assert.That(_events.Any(e => e.CellId == "t(5,4)" && e.RivetIndex == 1 && e.Previous == CellState.Done && e.State == CellState.Idle), Is.True, "下一颗开始时重置");
            List<CellState> conditional = _events.Where(e => e.CellId == "t(5,6)" && e.RivetIndex == 0).Select(e => e.State).ToList();
            Assert.That(conditional.First(), Is.EqualTo(CellState.Armed), "条件单元进入阶段时布防");
            List<CellState> standby = _events.Where(e => e.CellId == "t(5,2)").Select(e => e.State).ToList();
            Assert.That(standby, Is.Empty, "standby 单元始终为 Standby");
        }

        [UnityTest]
        public IEnumerator ForceExceeded_TriggersControlledRetreatAndRetriesPhase()
        {
            int attempts = 0;
            _registry.Register("arm.forceControlledContact", new DelegateExecutor((cell, cursor, ct) =>
            {
                Count("arm.forceControlledContact");
                if (cursor.RivetIndex == 1 && attempts++ == 0)
                {
                    Assert.That(_conditions.Raise(ConditionBus.ContactForceExceededTrigger, "测试：力超限"), Is.True);
                    return ExecutorResult.Fail("接触力超限").AsCompleted();
                }
                return ExecutorResult.Done().AsCompleted();
            }));
            var box = new Box();
            _ = Run(NewOrchestrator(new FakePlan(new[] { new[] { 3 } })), box);
            yield return WaitDone(box);

            Assert.That(box.Result.Success, Is.True);
            Assert.That(box.Result.UnrecoveredRivets, Is.EqualTo(0));
            Assert.That(Calls("arm.forceControlledContact"), Is.EqualTo(4), "3 颗 + 1 次重试");
            Assert.That(Calls("safety.controlledRetreat"), Is.EqualTo(1));
            List<CellState> retreat = _events.Where(e => e.CellId == "t(5,6)" && e.RivetIndex == 1).Select(e => e.State).ToList();
            CollectionAssert.IsSubsetOf(new[] { CellState.Triggered, CellState.Running, CellState.Done }, retreat);
            Assert.That(_events.Any(e => e.CellId == "t(5,4)" && e.RivetIndex == 1 && e.State == CellState.Skipped), Is.True, "接触失败后采集被跳过");
            Assert.That(Calls("acq.contactAcquire"), Is.EqualTo(3), "重试后正常采集");
        }

        [UnityTest]
        public IEnumerator RetriesExhausted_MarksRivetUnrecoveredAndContinues()
        {
            _registry.Register("arm.forceControlledContact", new DelegateExecutor((cell, cursor, ct) =>
            {
                Count("arm.forceControlledContact");
                if (cursor.RivetIndex != 0) return ExecutorResult.Done().AsCompleted();
                _conditions.Raise(ConditionBus.ContactForceExceededTrigger, "测试：持续力超限");
                return ExecutorResult.Fail("接触力超限").AsCompleted();
            }));
            var unrecovered = new List<string>();
            MissionOrchestrator o = NewOrchestrator(new FakePlan(new[] { new[] { 2 } }));
            o.RivetUnrecovered = (cursor, phase, reason) => unrecovered.Add($"{cursor.RivetIndex}:{phase}");
            var box = new Box();
            _ = Run(o, box);
            yield return WaitDone(box);

            Assert.That(box.Result.Success, Is.True);
            CollectionAssert.AreEqual(new[] { "0:T5" }, unrecovered);
            Assert.That(Calls("safety.controlledRetreat"), Is.EqualTo(1), "maxRetries = 1");
            Assert.That(Calls("arm.retractHold"), Is.EqualTo(2), "未恢复的铆钉仍执行 T6");
        }

        [UnityTest]
        public IEnumerator EveryTenRivets_TriggersDatumRemeasureOncePerStation()
        {
            var box = new Box();
            _ = Run(NewOrchestrator(new FakePlan(new[] { new[] { 12, 12 } })), box);
            yield return WaitDone(box);

            Assert.That(box.Result.Success, Is.True);
            Assert.That(Calls("vision.remeasureDatum"), Is.EqualTo(2), "每个工位第 10 颗后一次");
            Assert.That(_events.Where(e => e.CellId == "t(6,2)" && e.State == CellState.Triggered).Select(e => e.RivetIndex), Is.All.EqualTo(9));
        }

        [Test]
        public void Validate_ReportsMissingExecutorKeys()
        {
            Assert.That(NewOrchestrator(new FakePlan(new[] { new[] { 1 } })).Validate(), Is.Empty);
            var partial = new ExecutorRegistry();
            partial.Register("nav.dock", new DelegateExecutor((c, k, t) => ExecutorResult.Done().AsCompleted()));
            var o = new MissionOrchestrator(_data, partial, new ConditionBus(_bus), new FakePlan(new[] { new[] { 1 } }), _bus, () => 0d);
            Assert.That(o.Validate().Count, Is.EqualTo(_data.GetExecutorKeys().Count - 1));
        }

        [Test]
        public void DependencyTable_ReferencesCellsInSameRow()
        {
            foreach (string id in PhaseDependencyTable.DependentCells)
            {
                CellDef cell = _data.FindCell(id);
                Assert.That(cell, Is.Not.Null, id);
                foreach (string pred in PhaseDependencyTable.PredecessorsOf(id))
                {
                    CellDef p = _data.FindCell(pred);
                    Assert.That(p, Is.Not.Null, pred);
                    Assert.That(p.Row, Is.EqualTo(cell.Row), $"{id} 依赖 {pred} 跨阶段");
                    Assert.That(p.Kind, Is.Not.EqualTo(CellKind.Standby), $"{id} 依赖 standby 单元 {pred}");
                }
            }
        }

        [Test]
        public void ConditionThresholds_ParsedFromJsonCheck()
        {
            CollectionAssert.AreEqual(new[] { 8f, 12f, 0.3f }, ConditionEvaluator.ParseNumbers("contact.forceN in [8,12] for 0.3 s"));
            var evaluator = new ConditionEvaluator(_data.Conditions, _bus);
            Assert.That(evaluator.Threshold("C4", 0), Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(evaluator.Threshold("C12", 0), Is.EqualTo(100f));
            Assert.That(ConditionBus.TryParseEveryNRivets("counter.every10Rivets", out int n) && n == 10, Is.True);
        }

        private MissionOrchestrator NewOrchestrator(IMissionPlan plan) => new MissionOrchestrator(_data, _registry, _conditions, plan, _bus, () => 0d);

        private void RegisterFake(string key, ExecutorResult result) =>
            _registry.Register(key, new DelegateExecutor((cell, cursor, ct) =>
            {
                Count(key);
                return result.AsCompleted();
            }));

        private void Count(string key) => _calls[key] = Calls(key) + 1;

        private int Calls(string key) => _calls.TryGetValue(key, out int n) ? n : 0;

        private static async Awaitable Run(MissionOrchestrator o, Box box)
        {
            box.Result = await o.RunAsync(CancellationToken.None);
            box.Done = true;
        }

        private static IEnumerator WaitDone(Box box)
        {
            for (int i = 0; i < FrameLimit && !box.Done; i++) yield return null;
            Assert.That(box.Done, Is.True, "流程未在限定帧数内结束");
        }

        private sealed class Box
        {
            public bool Done;
            public MissionResult Result;
        }

        private sealed class FakePlan : IMissionPlan
        {
            private readonly int[][] _rivets;

            public FakePlan(int[][] rivetsPerStation) => _rivets = rivetsPerStation;

            public int RegionCount => _rivets.Length;

            public int StationCount(int regionIndex) => _rivets[regionIndex].Length;

            public int RivetCount(int regionIndex, int stationIndex) => _rivets[regionIndex][stationIndex];
        }
    }
}
