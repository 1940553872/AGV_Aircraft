using NUnit.Framework;
using RivetSim.Core.Config;
using RivetSim.Simulation.Navigation;
using RivetSim.Simulation.Robot;
using UnityEngine;

namespace RivetSim.Tests.EditMode
{
    /// <summary>
    /// 差速运动学测试：直线、原地转向、四分之一圆弧终点、轮速互换、制动曲线。
    /// </summary>
    public sealed class DiffDriveKinematicsTests
    {
        private const float Tol = 1e-4f;

        [Test]
        public void Straight_MovesAlongHeading()
        {
            PlanarPose p = DiffDriveKinematics.Integrate(new PlanarPose(Vector2.zero, 90f), 0.6f, 0f, 2f);
            Assert.AreEqual(1.2f, p.positionM.x, Tol);
            Assert.AreEqual(0f, p.positionM.y, Tol);
            Assert.AreEqual(90f, p.yawDeg, Tol);
        }

        [Test]
        public void InPlaceRotation_KeepsPosition()
        {
            PlanarPose p = DiffDriveKinematics.Integrate(new PlanarPose(new Vector2(1f, 2f), 0f), 0f, 60f, 1.5f);
            Assert.AreEqual(1f, p.positionM.x, Tol);
            Assert.AreEqual(2f, p.positionM.y, Tol);
            Assert.AreEqual(90f, p.yawDeg, Tol);
        }

        [Test]
        public void QuarterArc_EndsAtExpectedPoint()
        {
            // 车头朝 +Z，以半径 1 m 向右（顺时针）转 90°：终点 (1, 1)，车头朝 +X。
            float omegaDeg = 30f;
            float v = omegaDeg * Mathf.Deg2Rad * 1f;
            var pose = new PlanarPose(Vector2.zero, 0f);
            const int steps = 300;
            for (int i = 0; i < steps; i++) pose = DiffDriveKinematics.Integrate(pose, v, omegaDeg, 3f / steps);
            Assert.AreEqual(1f, pose.positionM.x, 1e-3f);
            Assert.AreEqual(1f, pose.positionM.y, 1e-3f);
            Assert.AreEqual(90f, pose.yawDeg, 1e-2f);
        }

        [Test]
        public void TinyAngularSpeed_StillAdvances()
        {
            // 角速度极小时圆弧公式若用 cos 相减会在 float 下抵消为 0（M3 沙盒中车停住的原因），弦长形式须仍能前进。
            var pose = new PlanarPose(new Vector2(-8f, 0f), 270f);
            PlanarPose p = DiffDriveKinematics.Integrate(pose, 0.03f, 0.001f, 0.01f);
            Assert.AreEqual(-8.0003f, p.positionM.x, 1e-6f);
        }

        [Test]
        public void WheelSpeeds_RoundTrip()
        {
            Vector2 wheels = DiffDriveKinematics.ToWheelSpeeds(0.5f, 30f, 0.66f);
            Assert.Greater(wheels.x, wheels.y, "向右转时左轮更快");
            Vector2 body = DiffDriveKinematics.FromWheelSpeeds(wheels.x, wheels.y, 0.66f);
            Assert.AreEqual(0.5f, body.x, Tol);
            Assert.AreEqual(30f, body.y, 1e-3f);
        }

        [Test]
        public void BrakingSpeed_StopsExactlyAtTarget()
        {
            Assert.AreEqual(0f, MotionProfile.BrakingSpeed(0f, 0.6f, 0.5f));
            Assert.AreEqual(0.6f, MotionProfile.BrakingSpeed(10f, 0.6f, 0.5f), Tol);
            // v² = 2·a·d：0.1 m 处为 sqrt(0.1) m/s。
            Assert.AreEqual(Mathf.Sqrt(0.1f), MotionProfile.BrakingSpeed(0.1f, 0.6f, 0.5f), Tol);
            Assert.Less(MotionProfile.TurnRate(-5f, 60f, 120f), 0f);
        }
    }
}
