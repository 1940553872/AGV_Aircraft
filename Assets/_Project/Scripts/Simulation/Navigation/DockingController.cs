using System;
using RivetSim.Core.Config;
using RivetSim.Simulation.Robot;
using RivetSim.Simulation.World;
using UnityEngine;

namespace RivetSim.Simulation.Navigation
{
    /// <summary>
    /// 对接控制：从预停靠位姿沿车道中心线低速直线对接（纯追踪跟踪直线，可修正横向偏差）。
    /// 车头对接相机按视场判定停靠标记可见时，每个子步用“真值 + 高斯噪声”更新目标停靠位姿估计；标记移出视场后沿用最后一次估计
    /// （教学简化模型）。按制动曲线停在估计位姿处，停车后以真值位姿计算停靠误差。
    /// </summary>
    public sealed class DockingController : IMotionTask
    {
        private enum Phase { Approach, Settle, Done, Failed }

        private readonly AgvController _chassis;
        private readonly AgvConfig _agv;
        private readonly NavigationConfig _nav;
        private readonly OccupancyGridMap _map;
        private readonly float _frontReachM;
        private readonly System.Random _random;

        private Phase _phase;
        private PlanarPose _truePose;
        private PlanarPose _estimate;
        private RegionMarker _marker;

        /// <inheritdoc />
        public string FailureReason { get; private set; } = "";

        /// <summary>当前帧标记是否可见。</summary>
        public bool MarkerVisible { get; private set; }

        /// <summary>累计识别到标记的子步数。</summary>
        public int DetectionCount { get; private set; }

        /// <summary>当前的目标位姿估计。</summary>
        public PlanarPose Estimate => _estimate;

        /// <summary>创建对接控制器。frontReachM 为车头最前端（含机械臂）到车体中心的距离。</summary>
        public DockingController(AgvController chassis, AgvConfig agv, NavigationConfig nav, OccupancyGridMap map, float frontReachM, System.Random random)
        {
            _chassis = chassis;
            _agv = agv;
            _nav = nav;
            _map = map;
            _frontReachM = frontReachM;
            _random = random;
        }

        /// <summary>开始对接；起点看不到标记或车道上有障碍物时返回 false（FailureReason 给出原因）。</summary>
        public bool Start(PlanarPose dockingPose, RegionMarker marker)
        {
            _truePose = dockingPose;
            _marker = marker;
            DetectionCount = 0;
            FailureReason = "";
            if (marker == null)
            {
                Fail("工位没有停靠标记");
                return false;
            }
            if (!Detect())
            {
                Fail($"车头对接相机未识别到停靠标记 ID {marker.TagId}");
                return false;
            }

            PlanarPose pose = _chassis.Pose;
            float toTarget = Vector2.Dot(_truePose.positionM - pose.positionM, DiffDriveKinematics.Heading(_truePose.yawDeg));
            float halfWidth = 0.5f * _agv.bodySizeM.y;
            if (_map != null && _map.AnyOccupiedInRect(pose.positionM, _truePose.yawDeg, halfWidth, 0f, toTarget + _frontReachM))
            {
                Fail("对接车道（含机械臂前伸）上有障碍物");
                return false;
            }
            _phase = Phase.Approach;
            return true;
        }

        /// <inheritdoc />
        public MotionStatus Step(float dtS)
        {
            switch (_phase)
            {
                case Phase.Approach:
                    StepApproach();
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
            Fail(reason);
        }

        private void StepApproach()
        {
            Detect();
            PlanarPose pose = _chassis.Pose;
            Vector2 heading = DiffDriveKinematics.Heading(_estimate.yawDeg);
            float remaining = Vector2.Dot(_estimate.positionM - pose.positionM, heading);
            if (remaining <= 0f)
            {
                _chassis.Stop();
                _phase = Phase.Settle;
                return;
            }

            // 前视点取在估计车道中心线上、投影点前方前视距离处（可越过停靠位姿），接近终点时转向保持平稳。
            Vector2 projection = _estimate.positionM - heading * remaining;
            Vector2 target = projection + heading * _nav.lookaheadM;
            Vector2 d = target - pose.positionM;
            float error = Mathf.DeltaAngle(pose.yawDeg, Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg);
            float curvature = 2f * Mathf.Sin(error * Mathf.Deg2Rad) / d.magnitude;

            float cap = remaining < _nav.dockingSlowDistanceM ? _agv.dockingSpeedMps : _agv.approachSpeedMps;
            float v = MotionProfile.BrakingSpeed(remaining, cap, _agv.maxAccelerationMps2);
            float omega = Mathf.Clamp(v * curvature * Mathf.Rad2Deg, -_agv.maxAngularSpeedDegPerS, _agv.maxAngularSpeedDegPerS);
            if (!_chassis.SetVelocity(v, omega)) Fail("联锁拒绝底盘行驶");
        }

        // 标记可见时用真值加噪声更新估计。
        private bool Detect()
        {
            PlanarPose pose = _chassis.Pose;
            MountPose mount = _agv.dockingCameraMount;
            Vector3 cameraPosition = pose.WorldPosition + pose.WorldRotation * mount.positionM;
            Quaternion cameraRotation = pose.WorldRotation * Quaternion.Euler(mount.eulerDeg);
            MarkerVisible = MarkerVisibility.IsVisible(cameraPosition, cameraRotation, _agv.dockingCameraFovDeg,
                _agv.dockingCameraMinRangeM, _agv.dockingCameraMaxRangeM, _marker.TagCenterWorld, _marker.NormalWorld);
            if (!MarkerVisible) return false;

            Vector2 heading = DiffDriveKinematics.Heading(_truePose.yawDeg);
            Vector2 right = DiffDriveKinematics.Right(_truePose.yawDeg);
            Vector2 noise = heading * Gaussian(_nav.markerPositionNoiseSigmaM) + right * Gaussian(_nav.markerPositionNoiseSigmaM);
            _estimate = new PlanarPose(_truePose.positionM + noise, _truePose.yawDeg + Gaussian(_nav.markerYawNoiseSigmaDeg));
            DetectionCount++;
            return true;
        }

        private float Gaussian(float sigma)
        {
            // Box–Muller 变换。
            double u1 = 1.0 - _random.NextDouble();
            double u2 = _random.NextDouble();
            return (float)(sigma * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
        }

        private void Fail(string reason)
        {
            FailureReason = reason;
            _phase = Phase.Failed;
        }
    }
}
