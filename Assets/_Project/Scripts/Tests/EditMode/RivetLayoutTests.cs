using NUnit.Framework;
using RivetSim.Core.Config;
using RivetSim.Simulation.World;
using UnityEngine;

namespace RivetSim.Tests.EditMode
{
    /// <summary>
    /// RivetLayout 测试：按 CFG_Inspection 默认值，每区 2 个工位间距 1.5 m、每工位 12 颗铆钉钉距 25 mm，居中排列；ID 命名。
    /// </summary>
    public sealed class RivetLayoutTests
    {
        private InspectionConfig _cfg;

        [SetUp]
        public void SetUp() => _cfg = ScriptableObject.CreateInstance<InspectionConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_cfg);

        [Test]
        public void Stations_AreSpacedAndCentered()
        {
            float[] offsets = RivetLayout.StationOffsetsM(_cfg.stationsPerRegion, _cfg.stationSpacingM);
            Assert.That(offsets.Length, Is.EqualTo(2));
            Assert.That(offsets[0] - offsets[1], Is.EqualTo(1.5f).Within(1e-5f));
            Assert.That(offsets[0] + offsets[1], Is.EqualTo(0f).Within(1e-5f));
            Assert.That(offsets[0], Is.GreaterThan(0f), "工位 1 在机头方向");
        }

        [Test]
        public void Rivets_UsePitchAndCount()
        {
            float[] offsets = RivetLayout.RivetOffsetsM(_cfg.rivetsPerStation, _cfg.rivetPitchMm * 0.001f);
            Assert.That(offsets.Length, Is.EqualTo(12));
            for (int i = 1; i < offsets.Length; i++) Assert.That(offsets[i] - offsets[i - 1], Is.EqualTo(0.025f).Within(1e-6f));
            Assert.That(offsets[0] + offsets[offsets.Length - 1], Is.EqualTo(0f).Within(1e-6f));
            Assert.That(offsets[offsets.Length - 1] - offsets[0], Is.EqualTo(0.275f).Within(1e-5f));
        }

        [Test]
        public void Ids_FollowNamingRule()
        {
            Assert.That(RivetLayout.RivetId(0, 1, 0), Is.EqualTo("R1-S2-01"));
            Assert.That(RivetLayout.RivetId(1, 0, 11), Is.EqualTo("R2-S1-12"));
        }

        [Test]
        public void Region_SitsBetweenWingAndStabilizer()
        {
            // A320 实测（主起落架为原点）：机翼后缘约 z = −3.3，平尾前缘约 z = −8.9。
            float[] stations = RivetLayout.StationOffsetsM(_cfg.stationsPerRegion, _cfg.stationSpacingM);
            float half = (_cfg.rivetsPerStation - 1) * _cfg.rivetPitchMm * 0.001f * 0.5f;
            float front = -_cfg.regionCenterAftOfMainGearM + stations[0] + half;
            float back = -_cfg.regionCenterAftOfMainGearM + stations[stations.Length - 1] - half;
            Assert.That(front, Is.LessThan(-3.3f));
            Assert.That(back, Is.GreaterThan(-8.9f));
        }
    }
}
