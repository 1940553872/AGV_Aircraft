using System;
using UnityEngine;

namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 关节空间同步轨迹（纯计算）：各关节按同一梯形速度曲线（以变化量最大的关节为准）同时起停，关节空间内为直线。
    /// </summary>
    public sealed class JointTrajectory
    {
        private readonly float[] _from;
        private readonly float[] _delta;
        private readonly TrapezoidProfile _profile;

        /// <summary>总用时，单位 s。</summary>
        public float DurationS => _profile.DurationS;

        /// <summary>变化量最大的关节转过的角度，单位 °。</summary>
        public float MaxDeltaDeg => _profile.Distance;

        /// <summary>创建轨迹。</summary>
        public JointTrajectory(float[] fromDeg, float[] toDeg, float maxSpeedDegPerS, float accelerationDegPerS2, float minDurationS = 0f)
        {
            if (fromDeg.Length != toDeg.Length) throw new ArgumentException("起止关节角数量不一致。");
            _from = (float[])fromDeg.Clone();
            _delta = new float[fromDeg.Length];
            float max = 0f;
            for (int i = 0; i < _from.Length; i++)
            {
                _delta[i] = toDeg[i] - fromDeg[i];
                max = Mathf.Max(max, Mathf.Abs(_delta[i]));
            }
            _profile = new TrapezoidProfile(max, maxSpeedDegPerS, accelerationDegPerS2, minDurationS);
        }

        /// <summary>t 时刻的关节角写入 q，单位 °。</summary>
        public void Sample(float tS, float[] q)
        {
            float s = _profile.Distance > 0f ? _profile.PositionAt(tS) / _profile.Distance : 1f;
            SampleFraction(s, q);
        }

        /// <summary>按路程比例 s（0～1）取关节角。</summary>
        public void SampleFraction(float s, float[] q)
        {
            for (int i = 0; i < _from.Length; i++) q[i] = _from[i] + _delta[i] * s;
        }
    }
}
