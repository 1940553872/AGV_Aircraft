using NUnit.Framework;
using RivetSim.Simulation.Robot;

namespace RivetSim.Tests.EditMode
{
    /// <summary>
    /// SafetyMonitor 联锁测试：规则 1（升降柱只在底盘零速锁定后运动）、规则 2（底盘只在升降柱最低位且机械臂运输姿态时运动）。
    /// </summary>
    public sealed class SafetyMonitorTests
    {
        private sealed class FakeChassis : IChassisState
        {
            public bool IsZeroSpeedLocked { get; set; }
            public bool IsStationary { get; set; } = true;
        }

        private sealed class FakeLift : ILiftState
        {
            public float ExtensionM { get; set; }
            public bool IsMoving { get; set; }
            public bool IsAtBottom => !IsMoving && ExtensionM <= 0.001f;
        }

        private sealed class FakeArm : IArmPoseState
        {
            public bool IsInTransportPose { get; set; } = true;
        }

        private FakeChassis _chassis;
        private FakeLift _lift;
        private FakeArm _arm;
        private SafetyMonitor _monitor;

        [SetUp]
        public void SetUp()
        {
            _chassis = new FakeChassis();
            _lift = new FakeLift();
            _arm = new FakeArm();
            _monitor = new SafetyMonitor(_chassis, _lift, _arm);
        }

        [Test]
        public void LiftMotion_RequiresZeroSpeedLock()
        {
            Assert.That(_monitor.CheckLiftMotion(out string reason), Is.False);
            Assert.That(reason, Does.Contain("零速锁定"));

            _chassis.IsZeroSpeedLocked = true;
            Assert.That(_monitor.CheckLiftMotion(out _), Is.True);
        }

        [Test]
        public void ChassisMotion_RequiresLiftAtBottom()
        {
            _lift.ExtensionM = 1.2f;
            Assert.That(_monitor.CheckChassisMotion(out string reason), Is.False);
            Assert.That(reason, Does.Contain("最低位"));

            _lift.ExtensionM = 0.0005f;
            _lift.IsMoving = true;
            Assert.That(_monitor.CheckChassisMotion(out reason), Is.False);
            Assert.That(reason, Does.Contain("正在运动"));

            _lift.IsMoving = false;
            Assert.That(_monitor.CheckChassisMotion(out _), Is.True);
        }

        [Test]
        public void ChassisMotion_RequiresArmTransportPose()
        {
            _arm.IsInTransportPose = false;
            Assert.That(_monitor.CheckChassisMotion(out string reason), Is.False);
            Assert.That(reason, Does.Contain("运输姿态"));
        }

        [Test]
        public void TryAuthorize_MatchesCheck()
        {
            Assert.That(_monitor.TryAuthorizeLiftMotion("测试升降"), Is.False);
            _chassis.IsZeroSpeedLocked = true;
            Assert.That(_monitor.TryAuthorizeLiftMotion("测试升降"), Is.True);
            Assert.That(_monitor.TryAuthorizeChassisMotion("测试行驶"), Is.True);
        }
    }
}
