using RivetSim.Core.Config;
using RivetSim.Simulation.Robot;
using UnityEngine;

namespace RivetSim.Simulation.Navigation
{
    /// <summary>
    /// 直线行驶：沿当前车头方向前进（距离为正）或倒车（距离为负），按制动曲线停在目标距离处；用于倒车退出与进出充电位。
    /// </summary>
    public sealed class StraightDrive : IMotionTask
    {
        private enum Phase { Drive, Settle, Done, Failed }

        private readonly AgvController _chassis;
        private readonly AgvConfig _agv;
        private Phase _phase;
        private Vector2 _startM;
        private Vector2 _directionM;
        private float _distanceM;
        private float _speedMps;

        /// <inheritdoc />
        public string FailureReason { get; private set; } = "";

        /// <summary>创建直线行驶任务。</summary>
        public StraightDrive(AgvController chassis, AgvConfig agv)
        {
            _chassis = chassis;
            _agv = agv;
        }

        /// <summary>开始行驶 signedDistanceM（负值倒车），速度上限 speedMps。</summary>
        public void Start(float signedDistanceM, float speedMps)
        {
            PlanarPose pose = _chassis.Pose;
            _startM = pose.positionM;
            _directionM = DiffDriveKinematics.Heading(pose.yawDeg) * Mathf.Sign(signedDistanceM);
            _distanceM = Mathf.Abs(signedDistanceM);
            _speedMps = speedMps;
            FailureReason = "";
            _phase = Phase.Drive;
        }

        /// <inheritdoc />
        public MotionStatus Step(float dtS)
        {
            switch (_phase)
            {
                case Phase.Drive:
                    float remaining = _distanceM - Vector2.Dot(_chassis.Pose.positionM - _startM, _directionM);
                    if (remaining <= 0f)
                    {
                        _chassis.Stop();
                        _phase = Phase.Settle;
                        break;
                    }
                    float v = MotionProfile.BrakingSpeed(remaining, _speedMps, _agv.maxAccelerationMps2);
                    float sign = Vector2.Dot(_directionM, DiffDriveKinematics.Heading(_chassis.Pose.yawDeg)) >= 0f ? 1f : -1f;
                    if (!_chassis.SetVelocity(sign * v, 0f))
                    {
                        FailureReason = "联锁拒绝底盘行驶";
                        _phase = Phase.Failed;
                    }
                    break;
                case Phase.Settle:
                    if (_chassis.IsStationary) _phase = Phase.Done;
                    break;
            }
            return _phase == Phase.Done ? MotionStatus.Succeeded : _phase == Phase.Failed ? MotionStatus.Failed : MotionStatus.Running;
        }

        /// <inheritdoc />
        public void Abort(string reason)
        {
            _chassis.Stop();
            FailureReason = reason;
            _phase = Phase.Failed;
        }
    }
}
