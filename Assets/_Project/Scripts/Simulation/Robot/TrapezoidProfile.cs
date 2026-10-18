using UnityEngine;

namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 一维梯形速度曲线（纯计算）：从 0 运动到距离 D，速度上限 v、加速度 a，可指定最短用时（拉长时降低巡航速度）。
    /// 距离不足以达到巡航速度时退化为三角形曲线。
    /// </summary>
    public sealed class TrapezoidProfile
    {
        private readonly float _distance;
        private readonly float _speed;
        private readonly float _acceleration;
        private readonly float _accelTimeS;

        /// <summary>总用时，单位 s。</summary>
        public float DurationS { get; }

        /// <summary>总距离（单位由调用方决定，如 m 或 °）。</summary>
        public float Distance => _distance;

        /// <summary>创建曲线；distance ≥ 0，maxSpeed 与 acceleration &gt; 0。</summary>
        public TrapezoidProfile(float distance, float maxSpeed, float acceleration, float minDurationS = 0f)
        {
            _distance = Mathf.Max(0f, distance);
            _acceleration = Mathf.Max(1e-6f, acceleration);
            float v = Mathf.Max(1e-6f, maxSpeed);
            if (_distance <= 0f)
            {
                _speed = v;
                _accelTimeS = 0f;
                DurationS = Mathf.Max(0f, minDurationS);
                return;
            }
            // 三角形曲线时的峰值速度。
            v = Mathf.Min(v, Mathf.Sqrt(_distance * _acceleration));
            float duration = _distance / v + v / _acceleration;
            if (duration < minDurationS)
            {
                // 给定用时 T 求巡航速度：T = D / v + v / a  →  v = (aT − √(a²T² − 4aD)) / 2。
                float aT = _acceleration * minDurationS;
                v = 0.5f * (aT - Mathf.Sqrt(Mathf.Max(0f, aT * aT - 4f * _acceleration * _distance)));
                duration = minDurationS;
            }
            _speed = v;
            _accelTimeS = v / _acceleration;
            DurationS = duration;
        }

        /// <summary>t 时刻已走过的距离（t 超出用时时为总距离）。</summary>
        public float PositionAt(float tS)
        {
            if (_distance <= 0f) return 0f;
            if (tS <= 0f) return 0f;
            if (tS >= DurationS) return _distance;
            float decelStart = DurationS - _accelTimeS;
            if (tS < _accelTimeS) return 0.5f * _acceleration * tS * tS;
            if (tS <= decelStart) return 0.5f * _acceleration * _accelTimeS * _accelTimeS + _speed * (tS - _accelTimeS);
            float remaining = DurationS - tS;
            return _distance - 0.5f * _acceleration * remaining * remaining;
        }
    }
}
