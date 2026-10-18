using System;
using UnityEngine;

namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 升降柱一维运动规划（教学简化模型）：匀速段 + 起停缓冲（加速度限幅），剩余距离进入到位精度后停在目标位。
    /// 纯计算类，由 <see cref="LiftColumnController"/> 每帧推进，单位 m、s。
    /// </summary>
    public sealed class LiftMotionProfile
    {
        private readonly float _strokeM;
        private readonly float _speedMps;
        private readonly float _accelerationMps2;
        private readonly float _toleranceM;

        /// <summary>当前伸出量，单位 m。</summary>
        public float PositionM { get; private set; }

        /// <summary>当前速度（伸出为正），单位 m/s。</summary>
        public float VelocityMps { get; private set; }

        /// <summary>目标伸出量，单位 m。</summary>
        public float TargetM { get; private set; }

        /// <summary>是否正在运动。</summary>
        public bool IsMoving { get; private set; }

        /// <summary>行程，单位 m。</summary>
        public float StrokeM => _strokeM;

        /// <summary>到位精度，单位 m。</summary>
        public float ToleranceM => _toleranceM;

        /// <summary>创建运动规划。</summary>
        public LiftMotionProfile(float strokeM, float speedMps, float accelerationMps2, float toleranceM, float initialPositionM = 0f)
        {
            if (strokeM <= 0f) throw new ArgumentOutOfRangeException(nameof(strokeM));
            if (speedMps <= 0f) throw new ArgumentOutOfRangeException(nameof(speedMps));
            if (accelerationMps2 <= 0f) throw new ArgumentOutOfRangeException(nameof(accelerationMps2));
            if (toleranceM <= 0f) throw new ArgumentOutOfRangeException(nameof(toleranceM));
            _strokeM = strokeM;
            _speedMps = speedMps;
            _accelerationMps2 = accelerationMps2;
            _toleranceM = toleranceM;
            PositionM = TargetM = Mathf.Clamp(initialPositionM, 0f, strokeM);
        }

        /// <summary>把伸出量限制在行程 [0, 行程] 内。</summary>
        public float ClampToStroke(float extensionM) => Mathf.Clamp(extensionM, 0f, _strokeM);

        /// <summary>设置目标（自动限制在行程内），返回实际目标。</summary>
        public float SetTarget(float extensionM)
        {
            TargetM = ClampToStroke(extensionM);
            IsMoving = Mathf.Abs(TargetM - PositionM) > _toleranceM;
            if (!IsMoving)
            {
                PositionM = TargetM;
                VelocityMps = 0f;
            }
            return TargetM;
        }

        /// <summary>立即停在当前位置（取消运动）。</summary>
        public void Halt()
        {
            VelocityMps = 0f;
            TargetM = PositionM;
            IsMoving = false;
        }

        /// <summary>立即设置位置（不经运动过程，供编辑器预览与初始化）。</summary>
        public void SetPositionImmediate(float extensionM)
        {
            PositionM = TargetM = ClampToStroke(extensionM);
            VelocityMps = 0f;
            IsMoving = false;
        }

        /// <summary>推进 dtS 秒；本步到位时返回 true。</summary>
        public bool Step(float dtS)
        {
            if (!IsMoving || dtS <= 0f) return false;

            float remaining = TargetM - PositionM;
            float direction = Mathf.Sign(remaining);
            // 期望速度：匀速上限与“刚好能在目标处停下”的速度取小。
            float desired = direction * Mathf.Min(_speedMps, Mathf.Sqrt(2f * _accelerationMps2 * Mathf.Abs(remaining)));
            VelocityMps = Mathf.MoveTowards(VelocityMps, desired, _accelerationMps2 * dtS);
            PositionM += VelocityMps * dtS;

            float after = TargetM - PositionM;
            if (Mathf.Abs(after) > _toleranceM && Mathf.Sign(after) == direction) return false;
            PositionM = TargetM;
            VelocityMps = 0f;
            IsMoving = false;
            return true;
        }

        /// <summary>从静止走完 distanceM 的理论时长（梯形或三角形速度曲线），单位 s。</summary>
        public static float EstimateDurationS(float distanceM, float speedMps, float accelerationMps2)
        {
            float d = Mathf.Abs(distanceM);
            float rampDistanceM = speedMps * speedMps / accelerationMps2;
            return d >= rampDistanceM
                ? d / speedMps + speedMps / accelerationMps2
                : 2f * Mathf.Sqrt(d / accelerationMps2);
        }
    }
}
