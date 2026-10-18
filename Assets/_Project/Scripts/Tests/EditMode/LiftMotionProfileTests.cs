using NUnit.Framework;
using RivetSim.Core.Config;
using RivetSim.Simulation.Robot;
using UnityEngine;

namespace RivetSim.Tests.EditMode
{
    /// <summary>
    /// LiftMotionProfile 测试：运动时间与行程一致（梯形 / 三角形速度曲线）、限位截断、到位精度、速度上限。
    /// 参数取 CFG_Agv 默认值（行程 1.40 m、0.08 m/s、0.16 m/s²、±1 mm）。
    /// </summary>
    public sealed class LiftMotionProfileTests
    {
        private const float StepS = 0.01f;
        private const float MaxSimulatedS = 60f;
        private AgvConfig _agv;

        [SetUp]
        public void SetUp() => _agv = ScriptableObject.CreateInstance<AgvConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_agv);

        // 剩余距离进入到位精度即判定到位，省去减速末段走完精度带的时间 √(2·精度 / 加速度)（约 0.11 s）。
        private float ToleranceSavingS => Mathf.Sqrt(2f * _agv.liftPositionToleranceM / _agv.liftAccelerationMps2);

        private LiftMotionProfile CreateProfile(float initialM = 0f) =>
            new LiftMotionProfile(_agv.liftStrokeM, _agv.liftSpeedMps, _agv.liftAccelerationMps2, _agv.liftPositionToleranceM, initialM);

        // 以固定步长推进到到位，返回用时；同时检查速度从不超过上限。
        private float RunToArrival(LiftMotionProfile profile)
        {
            float t = 0f;
            while (profile.IsMoving && t < MaxSimulatedS)
            {
                profile.Step(StepS);
                t += StepS;
                Assert.That(Mathf.Abs(profile.VelocityMps), Is.LessThanOrEqualTo(_agv.liftSpeedMps + 1e-5f));
            }
            Assert.That(profile.IsMoving, Is.False, "超时未到位");
            return t;
        }

        [Test]
        public void FullStroke_DurationMatchesTrapezoid()
        {
            float expected = LiftMotionProfile.EstimateDurationS(_agv.liftStrokeM, _agv.liftSpeedMps, _agv.liftAccelerationMps2);
            Assert.That(expected, Is.EqualTo(18.0f).Within(1e-3f));

            LiftMotionProfile profile = CreateProfile();
            profile.SetTarget(_agv.liftStrokeM);
            float duration = RunToArrival(profile);
            Assert.That(duration, Is.EqualTo(expected - ToleranceSavingS).Within(0.03f));
            Assert.That(profile.PositionM, Is.EqualTo(_agv.liftStrokeM).Within(_agv.liftPositionToleranceM));
        }

        [Test]
        public void DemoHeight_DurationMatchesTrapezoid()
        {
            float extension = _agv.ExtensionForMountHeightM(2.0f);
            Assert.That(extension, Is.EqualTo(1.20f).Within(1e-4f));
            LiftMotionProfile profile = CreateProfile();
            profile.SetTarget(extension);
            Assert.That(RunToArrival(profile), Is.EqualTo(15.5f - ToleranceSavingS).Within(0.03f));
        }

        [Test]
        public void ShortMove_UsesTriangleProfile()
        {
            const float distanceM = 0.02f;
            float expected = LiftMotionProfile.EstimateDurationS(distanceM, _agv.liftSpeedMps, _agv.liftAccelerationMps2);
            Assert.That(expected, Is.LessThan(distanceM / _agv.liftSpeedMps + _agv.liftSpeedMps / _agv.liftAccelerationMps2));

            LiftMotionProfile profile = CreateProfile(0.5f);
            profile.SetTarget(0.5f - distanceM);
            Assert.That(RunToArrival(profile), Is.EqualTo(expected - ToleranceSavingS).Within(0.03f));
        }

        [Test]
        public void TargetOutsideStroke_IsClamped()
        {
            LiftMotionProfile profile = CreateProfile();
            Assert.That(profile.SetTarget(2.0f), Is.EqualTo(_agv.liftStrokeM));
            Assert.That(profile.SetTarget(-0.5f), Is.EqualTo(0f));
            Assert.That(profile.IsMoving, Is.False, "从最低位指向低于 0 的目标不应运动");
        }

        [Test]
        public void TargetWithinTolerance_DoesNotMove()
        {
            LiftMotionProfile profile = CreateProfile(1.0f);
            profile.SetTarget(1.0f + _agv.liftPositionToleranceM * 0.5f);
            Assert.That(profile.IsMoving, Is.False);
        }

        [Test]
        public void MountHeight_LowestAndHighestMatchSpec()
        {
            Assert.That(_agv.ArmMountHeightM(0f), Is.EqualTo(0.80f).Within(1e-4f));
            Assert.That(_agv.ArmMountHeightM(_agv.liftStrokeM), Is.EqualTo(2.20f).Within(1e-4f));
            Assert.That(_agv.ArmMountHeightM(5f), Is.EqualTo(2.20f).Within(1e-4f));
        }
    }
}
