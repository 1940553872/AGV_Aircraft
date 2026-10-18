using NUnit.Framework;
using RivetSim.Core.Config;
using UnityEngine;

namespace RivetSim.Tests.EditMode
{
    /// <summary>
    /// SizeAxes 测试：（长, 宽, 高）到 Unity 缩放的两种映射（ADR-008）。
    /// </summary>
    public sealed class SizeAxesTests
    {
        [Test]
        public void LengthAlongX_MapsHallToWorldAxes()
        {
            Assert.That(SizeAxes.LengthAlongX(new Vector3(40f, 24f, 10f)), Is.EqualTo(new Vector3(40f, 10f, 24f)));
        }

        [Test]
        public void LengthAlongZ_MapsVehicleLengthToForward()
        {
            Assert.That(SizeAxes.LengthAlongZ(new Vector3(1.00f, 0.70f, 0.45f)), Is.EqualTo(new Vector3(0.70f, 0.45f, 1.00f)));
        }
    }
}
