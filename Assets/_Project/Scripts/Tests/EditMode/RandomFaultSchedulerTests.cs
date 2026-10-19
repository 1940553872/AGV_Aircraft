using System.Linq;
using NUnit.Framework;
using RivetSim.Simulation.Mission;

namespace RivetSim.Tests.EditMode
{
    /// <summary>
    /// 随机故障调度（M8）：同一种子结果相同；每类故障每轮一次；力超限与采集无效落在不同工位；位置不越界。
    /// </summary>
    public sealed class RandomFaultSchedulerTests
    {
        private sealed class Plan : IMissionPlan
        {
            private readonly int[][] _rivets;
            public Plan(params int[][] rivets) => _rivets = rivets;
            public int RegionCount => _rivets.Length;
            public int StationCount(int regionIndex) => _rivets[regionIndex].Length;
            public int RivetCount(int regionIndex, int stationIndex) => _rivets[regionIndex][stationIndex];
        }

        private static readonly Plan TwoRegions = new Plan(new[] { 12, 12 }, new[] { 12, 12 });

        [Test]
        public void SameSeed_GivesSamePlan()
        {
            var a = RandomFaultScheduler.CreatePlan(TwoRegions, 50, 1f);
            var b = RandomFaultScheduler.CreatePlan(TwoRegions, 50, 1f);
            Assert.That(a.Select(p => p.ToString()), Is.EqualTo(b.Select(p => p.ToString())));
        }

        [Test]
        public void ProbabilityOne_EachKindOnce_DifferentStations()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var plan = RandomFaultScheduler.CreatePlan(TwoRegions, seed, 1f);
                Assert.That(plan.Count(p => p.Kind == FaultKind.RecognitionFailed), Is.EqualTo(1));
                Assert.That(plan.Count(p => p.Kind == FaultKind.ContactForceExceeded), Is.EqualTo(1));
                Assert.That(plan.Count(p => p.Kind == FaultKind.AcquisitionInvalid), Is.EqualTo(1));
                FaultInjectionPoint force = plan.Single(p => p.Kind == FaultKind.ContactForceExceeded);
                FaultInjectionPoint acq = plan.Single(p => p.Kind == FaultKind.AcquisitionInvalid);
                Assert.That(force.StationGlobalIndex, Is.Not.EqualTo(acq.StationGlobalIndex));
                Assert.That(force.RivetIndex, Is.InRange(0, 11));
                Assert.That(acq.RivetIndex, Is.InRange(0, 11));
                Assert.That(plan.Single(p => p.Kind == FaultKind.RecognitionFailed).RegionIndex, Is.InRange(0, 1));
            }
        }

        [Test]
        public void SingleStation_FaultsOnDifferentRivets()
        {
            var plan = RandomFaultScheduler.CreatePlan(new Plan(new[] { 5 }), 7, 1f);
            FaultInjectionPoint force = plan.Single(p => p.Kind == FaultKind.ContactForceExceeded);
            FaultInjectionPoint acq = plan.Single(p => p.Kind == FaultKind.AcquisitionInvalid);
            Assert.That(force.StationGlobalIndex, Is.EqualTo(acq.StationGlobalIndex));
            Assert.That(force.RivetIndex, Is.Not.EqualTo(acq.RivetIndex));
        }

        [Test]
        public void ProbabilityZero_NoFaults()
        {
            Assert.That(RandomFaultScheduler.CreatePlan(TwoRegions, 50, 0f), Is.Empty);
        }
    }
}
