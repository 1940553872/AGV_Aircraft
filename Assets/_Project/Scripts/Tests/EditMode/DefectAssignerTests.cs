using System.Linq;
using NUnit.Framework;
using RivetSim.Simulation.World;

namespace RivetSim.Tests.EditMode
{
    /// <summary>
    /// DefectAssigner 测试：48 颗铆钉 × 10% = 5 颗缺陷，类型均为三种缺陷之一；同一种子结果可复现，不同种子结果不同。
    /// </summary>
    public sealed class DefectAssignerTests
    {
        private const int RivetCount = 48;
        private const float Ratio = 0.10f;
        private const int Seed = 42;

        [Test]
        public void Count_IsRoundedRatio()
        {
            DefectType[] defects = DefectAssigner.Assign(RivetCount, Ratio, Seed);
            Assert.That(defects.Length, Is.EqualTo(RivetCount));
            Assert.That(defects.Count(d => d != DefectType.None), Is.EqualTo(5));
            Assert.That(DefectAssigner.DefectCount(RivetCount, Ratio), Is.EqualTo(5));
        }

        [Test]
        public void SameSeed_IsReproducible()
        {
            Assert.That(DefectAssigner.Assign(RivetCount, Ratio, Seed), Is.EqualTo(DefectAssigner.Assign(RivetCount, Ratio, Seed)));
        }

        [Test]
        public void DifferentSeed_Differs()
        {
            Assert.That(DefectAssigner.Assign(RivetCount, Ratio, Seed), Is.Not.EqualTo(DefectAssigner.Assign(RivetCount, Ratio, Seed + 1)));
        }

        [Test]
        public void EdgeCases_AreClamped()
        {
            Assert.That(DefectAssigner.Assign(0, Ratio, Seed), Is.Empty);
            Assert.That(DefectAssigner.Assign(10, 1.5f, Seed).All(d => d != DefectType.None), Is.True);
            Assert.That(DefectAssigner.Assign(10, 0f, Seed).All(d => d == DefectType.None), Is.True);
        }
    }
}
