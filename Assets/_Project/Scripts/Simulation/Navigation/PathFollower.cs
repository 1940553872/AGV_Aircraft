using System.Collections.Generic;
using RivetSim.Core.Config;
using RivetSim.Simulation.Robot;
using UnityEngine;

namespace RivetSim.Simulation.Navigation
{
    /// <summary>
    /// 路径跟踪：起步原地对准 → 纯追踪行驶（曲率限速、终点按制动曲线停住）→ 终点原地转到目标朝向。
    /// 原地转向前检查含机械臂扫掠半径内没有激光障碍（M4 运输姿态前的临时措施，ADR-015）。
    /// </summary>
    public sealed class PathFollower : IMotionTask
    {
        private enum Phase { AlignStart, Follow, AlignFinal, Settle, Done, Failed }

        // 原地转向收尾：误差小于容差的此比例或越过目标时下发停车，随后等待停稳。
        private const float TurnFinishFraction = 0.1f;

        private readonly AgvController _chassis;
        private readonly AgvConfig _agv;
        private readonly NavigationConfig _nav;
        private readonly OccupancyGridMap _map;
        private readonly float _sweptRadiusM;
        private readonly PurePursuitTracker _tracker = new PurePursuitTracker();

        private Phase _phase;
        private Phase _afterSettle;
        private float _maxSpeedMps;
        private float? _finalYawDeg;
        private float _turnTargetDeg;
        private float _turnStartSign;
        private Vector2 _goalM;

        /// <inheritdoc />
        public string FailureReason { get; private set; } = "";

        /// <summary>沿路径的完成比例（0～1）。</summary>
        public float ProgressFraction => _tracker.TotalLengthM > 0f ? Mathf.Clamp01(_tracker.ProgressM / _tracker.TotalLengthM) : 0f;

        /// <summary>跟踪的折线总长，单位 m。</summary>
        public float PathLengthM => _tracker.TotalLengthM;

        /// <summary>创建路径跟踪器。</summary>
        public PathFollower(AgvController chassis, AgvConfig agv, NavigationConfig nav, OccupancyGridMap map, float sweptRadiusM)
        {
            _chassis = chassis;
            _agv = agv;
            _nav = nav;
            _map = map;
            _sweptRadiusM = sweptRadiusM;
        }

        /// <summary>开始跟踪 points（首点为当前位置）；finalYawDeg 非空时到点后转到该朝向。</summary>
        public void Start(IReadOnlyList<Vector2> points, float maxSpeedMps, float? finalYawDeg)
        {
            _tracker.Reset(points);
            _maxSpeedMps = maxSpeedMps;
            _finalYawDeg = finalYawDeg;
            _goalM = points[points.Count - 1];
            FailureReason = "";
            PlanarPose pose = _chassis.Pose;
            Vector2 target = _tracker.Compute(pose.positionM, pose.yawDeg, _nav.lookaheadM, out float error, out _);
            if (_tracker.TotalLengthM < _nav.goalPositionToleranceM || (target - pose.positionM).sqrMagnitude < 1e-6f)
                BeginFinalAlign();
            else if (Mathf.Abs(error) > _nav.goalYawToleranceDeg)
                BeginTurn(DiffDriveKinematics.BearingDeg(pose.positionM, target), Phase.Follow);
            else
                _phase = Phase.Follow;
        }

        /// <inheritdoc />
        public MotionStatus Step(float dtS)
        {
            switch (_phase)
            {
                case Phase.AlignStart:
                case Phase.AlignFinal:
                    StepTurn();
                    break;
                case Phase.Follow:
                    StepFollow(dtS);
                    break;
                case Phase.Settle:
                    if (_chassis.IsStationary) _phase = _afterSettle;
                    if (_phase == Phase.AlignFinal) BeginFinalAlign();
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

        private void StepFollow(float dtS)
        {
            PlanarPose pose = _chassis.Pose;
            _tracker.Compute(pose.positionM, pose.yawDeg, _nav.lookaheadM, out float error, out float curvature);
            float remaining = _tracker.RemainingM;
            if (remaining < _nav.lookaheadM)
            {
                // 终点段：直接瞄准终点（而不是延长线），沿车头方向的投影到 0 即停，横向偏差不留到终点；
                // 进入到点容差圈后锁定转向直线停车，避免贴近终点时方位角突变。
                Vector2 toGoal = _goalM - pose.positionM;
                remaining = Vector2.Dot(toGoal, DiffDriveKinematics.Heading(pose.yawDeg));
                float distance = toGoal.magnitude;
                if (distance < _nav.goalPositionToleranceM)
                {
                    error = 0f;
                    curvature = 0f;
                }
                else
                {
                    error = Mathf.DeltaAngle(pose.yawDeg, Mathf.Atan2(toGoal.x, toGoal.y) * Mathf.Rad2Deg);
                    curvature = 2f * Mathf.Sin(error * Mathf.Deg2Rad) / distance;
                }
            }
            if (remaining <= 0f)
            {
                _chassis.Stop();
                Settle(Phase.AlignFinal);
                return;
            }

            if (Mathf.Abs(error) > 90f)
            {
                // 前视点在车后方：停车后原地转向。
                _chassis.Stop();
                if (_chassis.IsStationary) BeginTurn(pose.yawDeg + error, Phase.Follow);
                return;
            }

            float cap = remaining < _nav.slowdownDistanceM ? Mathf.Min(_maxSpeedMps, _agv.approachSpeedMps) : _maxSpeedMps;
            float v = MotionProfile.BrakingSpeed(remaining, cap, _agv.maxAccelerationMps2);
            float omegaDeg = v * curvature * Mathf.Rad2Deg;
            if (Mathf.Abs(omegaDeg) > _agv.maxAngularSpeedDegPerS)
            {
                v *= _agv.maxAngularSpeedDegPerS / Mathf.Abs(omegaDeg);
                omegaDeg = Mathf.Sign(omegaDeg) * _agv.maxAngularSpeedDegPerS;
            }
            if (!_chassis.SetVelocity(v, omegaDeg)) Fail("联锁拒绝底盘行驶");
        }

        private void BeginFinalAlign()
        {
            if (_finalYawDeg.HasValue && Mathf.Abs(Mathf.DeltaAngle(_chassis.Pose.yawDeg, _finalYawDeg.Value)) > _nav.goalYawToleranceDeg)
                BeginTurn(_finalYawDeg.Value, Phase.Done);
            else
                _phase = Phase.Done;
        }

        private void BeginTurn(float targetYawDeg, Phase next)
        {
            PlanarPose pose = _chassis.Pose;
            if (_map != null && _map.AnyOccupiedWithin(pose.positionM, _sweptRadiusM))
            {
                Fail($"原地转向扫掠半径 {_sweptRadiusM:0.00} m 内有障碍物");
                return;
            }
            _turnTargetDeg = targetYawDeg;
            _turnStartSign = Mathf.Sign(Mathf.DeltaAngle(pose.yawDeg, targetYawDeg));
            _afterSettle = next;
            _phase = next == Phase.Follow ? Phase.AlignStart : Phase.AlignFinal;
        }

        private void StepTurn()
        {
            float error = Mathf.DeltaAngle(_chassis.Pose.yawDeg, _turnTargetDeg);
            if (Mathf.Abs(error) <= _nav.goalYawToleranceDeg * TurnFinishFraction || Mathf.Sign(error) != _turnStartSign)
            {
                _chassis.Stop();
                Settle(_afterSettle == Phase.Follow ? Phase.Follow : Phase.Done);
                return;
            }
            float omega = MotionProfile.TurnRate(error, _agv.maxAngularSpeedDegPerS, _agv.maxAngularAccelerationDegPerS2);
            if (!_chassis.SetVelocity(0f, omega)) Fail("联锁拒绝底盘转向");
        }

        private void Settle(Phase next)
        {
            _afterSettle = next;
            _phase = Phase.Settle;
        }

        private void Fail(string reason)
        {
            FailureReason = reason;
            _phase = Phase.Failed;
        }
    }
}
