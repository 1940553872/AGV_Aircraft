using NUnit.Framework;
using RivetSim.Simulation.Robot;

namespace RivetSim.Tests.EditMode
{
    /// <summary>
    /// 机械臂运动曲线测试：梯形 / 三角形速度曲线、最短用时拉长，以及关节同步轨迹各关节同时到位且速度不超限。
    /// </summary>
    public sealed class ArmMotionProfileTests
    {
        [Test]
        public void Trapezoid_ReachesDistanceAtDuration()
        {
            var p = new TrapezoidProfile(90f, 60f, 120f);
            Assert.That(p.DurationS, Is.EqualTo(90f / 60f + 60f / 120f).Within(1e-4f));
            Assert.That(p.PositionAt(p.DurationS), Is.EqualTo(90f));
            Assert.That(p.PositionAt(0.5f * p.DurationS), Is.EqualTo(45f).Within(1e-3f));
        }

        [Test]
        public void Trapezoid_ShortDistance_BecomesTriangle()
        {
            var p = new TrapezoidProfile(3f, 60f, 120f);
            Assert.That(p.DurationS, Is.EqualTo(2f * UnityEngine.Mathf.Sqrt(3f / 120f)).Within(1e-4f));
        }

        [Test]
        public void Trapezoid_MinDuration_SlowsCruise()
        {
            var p = new TrapezoidProfile(10f, 60f, 120f, 2f);
            Assert.That(p.DurationS, Is.EqualTo(2f));
            Assert.That(p.PositionAt(2f), Is.EqualTo(10f));
        }

        [Test]
        public void JointTrajectory_AllJointsArriveTogether_WithinSpeed()
        {
            float[] from = { 0f, -180f, -120f, 45f, -90f, 0f };
            float[] to = { -61f, -82f, 146f, -82f, -243f, -98f };
            var t = new JointTrajectory(from, to, 60f, 120f);
            var q = new float[6];
            var previous = (float[])from.Clone();
            const float dt = 0.01f;
            for (float time = dt; time <= t.DurationS + dt; time += dt)
            {
                t.Sample(time, q);
                for (int i = 0; i < 6; i++)
                {
                    Assert.That(System.Math.Abs(q[i] - previous[i]) / dt, Is.LessThanOrEqualTo(60f + 1e-2f));
                    previous[i] = q[i];
                }
            }
            t.Sample(t.DurationS, q);
            for (int i = 0; i < 6; i++) Assert.That(q[i], Is.EqualTo(to[i]).Within(1e-3f));
        }
    }
}
