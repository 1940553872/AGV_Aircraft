using NUnit.Framework;
using RivetSim.Core.Config;
using RivetSim.Simulation.World;
using UnityEngine;

namespace RivetSim.Tests.EditMode
{
    /// <summary>
    /// SceneLayoutValidator 测试：M2 默认布局（模型机 1:1.5、厂房 64 × 44 × 12 m）合法，关键间距与计划一致；违规布局能被发现。
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
            Assert.That(report.NarrowestPassageM, Is.EqualTo(1.8f).Within(1e-4f));
            Assert.That(report.RouteToFuselageM, Is.EqualTo(6.375f).Within(1e-3f));
            Assert.That(report.RouteToAircraftBoundsM, Is.EqualTo(6.375f).Within(1e-3f));
            Assert.That(report.RouteToObstacleM, Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(report.AircraftToObstacleM, Is.EqualTo(4.675f).Within(1e-3f));
            Assert.That(report.AircraftToWorkZoneEdgeM, Is.EqualTo(4.075f).Within(1e-3f));
            Assert.That(report.RouteLengthM, Is.EqualTo(169.2f).Within(1e-2f));
            Assert.That(report.ChargingDockToWorkZoneM, Is.EqualTo(10.4f).Within(1e-3f));
            Assert.That(report.DemoAudienceDepthM, Is.EqualTo(5.1f).Within(1e-3f));
        }

        [Test]
        public void ObstacleInsideWorkZone_IsReported()
        {
            _layout.obstacles[2].footprint.centerM = new Vector2(22f, 0f);
            SceneLayoutReport report = SceneLayoutValidator.Validate(_layout, _agv);
            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Errors, Has.Some.Contains("侵入作业区"));
        }

        [Test]
        public void PassageNarrowerThanAgv_IsReported()
        {
            _layout.obstacles[0].footprint.centerM = new Vector2(-15.0f, -2.7f);
            _layout.obstacles[1].footprint.centerM = new Vector2(-15.0f, 2.7f);
            SceneLayoutReport report = SceneLayoutValidator.Validate(_layout, _agv);
            Assert.That(report.NarrowestPassageM, Is.EqualTo(0.4f).Within(1e-4f));
            Assert.That(report.Errors, Has.Some.Contains("小于 AGV 车宽"));
            Assert.That(report.Errors, Has.Some.Contains("碰撞"));
        }

        [Test]
        public void ChargingDockTooCloseToWorkZone_IsReported()
        {
            _layout.chargingDockPose.positionM = new Vector2(-12.0f, -12.0f);
            SceneLayoutReport report = SceneLayoutValidator.Validate(_layout, _agv);
            Assert.That(report.ChargingDockToWorkZoneM, Is.EqualTo(4.0f).Within(1e-4f));
            Assert.That(report.Errors, Has.Some.Contains("充电位到作业区"));
        }

        [Test]
        public void DemoAudienceTooShallow_IsReported()
        {
            _layout.demoDeskPose.positionM = new Vector2(-25.0f, 0f);
            SceneLayoutReport report = SceneLayoutValidator.Validate(_layout, _agv);
            Assert.That(report.DemoAudienceDepthM, Is.EqualTo(1.6f).Within(1e-4f));
            Assert.That(report.Errors, Has.Some.Contains("观众区进深"));
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
