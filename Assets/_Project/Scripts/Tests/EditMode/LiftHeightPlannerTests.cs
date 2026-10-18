using System;
using NUnit.Framework;
using RivetSim.Core.Config;
using RivetSim.Simulation.Robot;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RivetSim.Tests.EditMode
{
    /// <summary>
    /// LiftHeightPlanner 测试：安装面 = 铆钉平均高度 − 舒适偏移（0.30 m），伸出量限制在行程 0～1.40 m 内。
    /// </summary>
    public sealed class LiftHeightPlannerTests
    {
        private AgvConfig _agv;
        private ArmConfig _arm;

        [SetUp]
        public void SetUp()
        {
            _agv = ScriptableObject.CreateInstance<AgvConfig>();
            _arm = ScriptableObject.CreateInstance<ArmConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_agv);
            Object.DestroyImmediate(_arm);
        }

        [Test]
        public void TypicalRivetRow_PlansWithinStroke()
        {
            LiftHeightPlan plan = LiftHeightPlanner.Plan(new[] { 2.0f, 2.15f, 2.3f }, _agv, _arm);
            Assert.That(plan.MeanRivetHeightM, Is.EqualTo(2.15f).Within(1e-4f));
            Assert.That(plan.MountHeightM, Is.EqualTo(1.85f).Within(1e-4f));
            Assert.That(plan.ExtensionM, Is.EqualTo(1.05f).Within(1e-4f));
            Assert.That(plan.WasClamped, Is.False);
        }

        [Test]
        public void TooHigh_IsClampedToTop()
        {
            LiftHeightPlan plan = LiftHeightPlanner.Plan(new[] { 3.0f }, _agv, _arm);
            Assert.That(plan.ExtensionM, Is.EqualTo(_agv.liftStrokeM).Within(1e-4f));
            Assert.That(plan.MountHeightM, Is.EqualTo(2.20f).Within(1e-4f));
            Assert.That(plan.WasClamped, Is.True);
        }

        [Test]
        public void TooLow_IsClampedToBottom()
        {
            LiftHeightPlan plan = LiftHeightPlanner.Plan(new[] { 0.6f }, _agv, _arm);
            Assert.That(plan.ExtensionM, Is.EqualTo(0f));
            Assert.That(plan.MountHeightM, Is.EqualTo(0.80f).Within(1e-4f));
            Assert.That(plan.WasClamped, Is.True);
        }

        [Test]
        public void EmptyList_Throws()
        {
            Assert.Throws<ArgumentException>(() => LiftHeightPlanner.Plan(Array.Empty<float>(), _agv, _arm));
        }
    }
}
