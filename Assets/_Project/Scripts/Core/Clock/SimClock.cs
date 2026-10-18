using System;
using System.Collections.Generic;

namespace RivetSim.Core.Clock
{
    /// <summary>
    /// 仿真时钟：累计仿真时间，支持倍速与暂停。业务代码一律使用本时钟的时间，不直接读取 UnityEngine.Time。
    /// 由启动入口每帧调用 <see cref="Advance"/> 推进。
    /// </summary>
    public sealed class SimClock
    {
        // 倍速比较时的相对容差，避免浮点误差导致合法倍速被拒绝。
        private const float SpeedMatchTolerance = 1e-4f;

        private readonly float[] _speedOptions;

        /// <summary>当前倍速变化时触发，参数为新倍速。</summary>
        public event Action<float> SpeedChanged;

        /// <summary>暂停状态变化时触发，参数为是否暂停。</summary>
        public event Action<bool> PausedChanged;

        /// <summary>累计仿真时间（秒）。</summary>
        public double TimeS { get; private set; }

        /// <summary>最近一次推进的仿真步长（秒），暂停时为 0。</summary>
        public float DeltaS { get; private set; }

        /// <summary>当前倍速。</summary>
        public float SpeedMultiplier { get; private set; }

        /// <summary>是否暂停。</summary>
        public bool IsPaused { get; private set; }

        /// <summary>可选倍速列表（升序）。</summary>
        public IReadOnlyList<float> SpeedOptions => _speedOptions;

        /// <summary>
        /// 创建时钟。
        /// </summary>
        /// <param name="speedOptions">可选倍速，须为正数且至少一项。</param>
        /// <param name="initialSpeed">初始倍速，须在可选列表内。</param>
        public SimClock(IReadOnlyList<float> speedOptions, float initialSpeed)
        {
            if (speedOptions == null || speedOptions.Count == 0)
                throw new ArgumentException("倍速列表不能为空。", nameof(speedOptions));

            _speedOptions = new float[speedOptions.Count];
            for (int i = 0; i < speedOptions.Count; i++)
            {
                if (speedOptions[i] <= 0f)
                    throw new ArgumentException($"倍速必须为正数：{speedOptions[i]}", nameof(speedOptions));
                _speedOptions[i] = speedOptions[i];
            }
            Array.Sort(_speedOptions);

            int index = IndexOfSpeed(initialSpeed);
            if (index < 0)
                throw new ArgumentException($"初始倍速 {initialSpeed} 不在可选列表内。", nameof(initialSpeed));
            SpeedMultiplier = _speedOptions[index];
        }

        /// <summary>
        /// 按真实时间步长推进仿真时间，返回本次仿真步长（秒）。
        /// </summary>
        /// <param name="realDeltaS">真实时间步长（秒），负值按 0 处理。</param>
        public float Advance(float realDeltaS)
        {
            DeltaS = IsPaused || realDeltaS <= 0f ? 0f : realDeltaS * SpeedMultiplier;
            TimeS += DeltaS;
            return DeltaS;
        }

        /// <summary>设置倍速；不在可选列表内时返回 false 且不改变。</summary>
        public bool TrySetSpeed(float speed)
        {
            int index = IndexOfSpeed(speed);
            if (index < 0) return false;
            ApplySpeed(_speedOptions[index]);
            return true;
        }

        /// <summary>按可选列表切换到更快（正数）或更慢（负数）的倍速，到达两端时保持不变。</summary>
        public void StepSpeed(int direction)
        {
            int current = IndexOfSpeed(SpeedMultiplier);
            int next = Math.Clamp(current + Math.Sign(direction), 0, _speedOptions.Length - 1);
            ApplySpeed(_speedOptions[next]);
        }

        /// <summary>设置暂停状态。</summary>
        public void SetPaused(bool paused)
        {
            if (IsPaused == paused) return;
            IsPaused = paused;
            PausedChanged?.Invoke(paused);
        }

        /// <summary>切换暂停状态。</summary>
        public void TogglePause() => SetPaused(!IsPaused);

        /// <summary>仿真时间归零（倍速与暂停状态不变）。</summary>
        public void ResetTime()
        {
            TimeS = 0d;
            DeltaS = 0f;
        }

        private void ApplySpeed(float speed)
        {
            if (speed == SpeedMultiplier) return;
            SpeedMultiplier = speed;
            SpeedChanged?.Invoke(speed);
        }

        private int IndexOfSpeed(float speed)
        {
            for (int i = 0; i < _speedOptions.Length; i++)
            {
                if (Math.Abs(_speedOptions[i] - speed) <= SpeedMatchTolerance * _speedOptions[i])
                    return i;
            }
            return -1;
        }
    }
}
