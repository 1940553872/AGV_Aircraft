using NUnit.Framework;
using RivetSim.Core.Config;
using RivetSim.Simulation.World;
using UnityEngine;

namespace RivetSim.Tests.EditMode
{
    /// <summary>
    /// SceneLayoutValidator 测试：M1 默认布局合法，关键间距与计划一致；违规布局能被发现。
    /// </summary>
    public sealed class SceneLayoutValidatorTests
    {
        private SceneLayoutConfig _layout;
        private AgvConfig _agv;

        [SetUp]
        public void SetUp()
        {
            _layout = ScriptableObject.CreateInstance<SceneLayoutConfig>();
            _agv = ScriptableObject.CreateInstance<AgvConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_layout);
            Object.DestroyImmediate(_agv);
        }

        [Test]
        public void DefaultLayout_IsValid()
        {
            SceneLayoutReport report = SceneLayoutValidator.Validate(_layout, _agv);
            Assert.That(report.IsValid, Is.True, report.ToText());
        }

        [Test]
        public void DefaultLayout_KeyDistancesMatchPlan()
        {
            SceneLayoutReport report = SceneLayoutValidator.Validate(_layout, _agv);
            Assert.That(report.NarrowestPassageM, Is.EqualTo(1.6f).Within(1e-4f));
            Assert.That(report.RouteToFuselageM, Is.EqualTo(1.40f).Within(1e-4f));
            Assert.That(report.RouteToAircraftBoundsM, Is.EqualTo(1.40f).Within(1e-4f));
            Assert.That(report.AircraftToObstacleM, Is.EqualTo(5.4f).Within(1e-4f));
            Assert.That(report.AircraftToWorkZoneEdgeM, Is.EqualTo(3.25f).Within(1e-4f));
            Assert.That(report.RouteLengthM, Is.EqualTo(60f).Within(1e-3f));
        }

        [Test]
        public void ObstacleInsideWorkZone_IsReported()
        {
            _layout.obstacles[2].footprint.centerM = new Vector2(9f, 0f);
            SceneLayoutReport report = SceneLayoutValidator.Validate(_layout, _agv);
            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Errors, Has.Some.Contains("侵入作业区"));
        }

        [Test]
        public void PassageNarrowerThanAgv_IsReported()
        {
            _layout.obstacles[0].footprint.centerM = new Vector2(-6.0f, -2.2f);
            _layout.obstacles[1].footprint.centerM = new Vector2(-6.0f, 2.2f);
            SceneLayoutReport report = SceneLayoutValidator.Validate(_layout, _agv);
            Assert.That(report.NarrowestPassageM, Is.EqualTo(0.4f).Within(1e-4f));
            Assert.That(report.Errors, Has.Some.Contains("小于 AGV 车宽"));
            Assert.That(report.Errors, Has.Some.Contains("碰撞"));
        }

        [Test]
        public void SegmentRectDistance_HandlesCrossingAndParallel()
        {
            var rect = new Rect(0f, 0f, 1f, 1f);
            Assert.That(SceneLayoutValidator.SegmentRectDistance(new Vector2(-1f, 0.5f), new Vector2(2f, 0.5f), rect), Is.EqualTo(0f));
            Assert.That(SceneLayoutValidator.SegmentRectDistance(new Vector2(-1f, 2f), new Vector2(2f, 2f), rect), Is.EqualTo(1f).Within(1e-5f));
        }
    }
}
