using System;
using NUnit.Framework;
using RivetSim.Core.Clock;

namespace RivetSim.Tests.EditMode
{
    /// <summary>
    /// SimClock 测试：倍速推进、暂停、倍速切换。
    /// </summary>
    public sealed class SimClockTests
    {
        private static readonly float[] Speeds = { 0.5f, 1f, 2f, 4f };

        [Test]
        public void Advance_ScalesBySpeed()
        {
            var clock = new SimClock(Speeds, 2f);
            clock.Advance(0.1f);
            clock.Advance(0.1f);
            Assert.That(clock.TimeS, Is.EqualTo(0.4).Within(1e-6));
            Assert.That(clock.DeltaS, Is.EqualTo(0.2f).Within(1e-6f));
        }

        [Test]
        public void Advance_WhilePaused_DoesNotAccumulate()
        {
            var clock = new SimClock(Speeds, 1f);
            clock.SetPaused(true);
            Assert.That(clock.Advance(0.5f), Is.EqualTo(0f));
            Assert.That(clock.TimeS, Is.EqualTo(0d));
        }

        [Test]
        public void TrySetSpeed_RejectsValueNotInOptions()
        {
            var clock = new SimClock(Speeds, 1f);
            Assert.That(clock.TrySetSpeed(3f), Is.False);
            Assert.That(clock.SpeedMultiplier, Is.EqualTo(1f));
            Assert.That(clock.TrySetSpeed(4f), Is.True);
            Assert.That(clock.SpeedMultiplier, Is.EqualTo(4f));
        }

        [Test]
        public void StepSpeed_ClampsAtEnds()
        {
            var clock = new SimClock(Speeds, 4f);
            clock.StepSpeed(+1);
            Assert.That(clock.SpeedMultiplier, Is.EqualTo(4f));
            clock.StepSpeed(-1);
            clock.StepSpeed(-1);
            clock.StepSpeed(-1);
            clock.StepSpeed(-1);
            Assert.That(clock.SpeedMultiplier, Is.EqualTo(0.5f));
        }

        [Test]
        public void Constructor_RejectsInitialSpeedNotInOptions()
        {
            Assert.Throws<ArgumentException>(() => new SimClock(Speeds, 3f));
        }
    }
}
