using System;
using RivetSim.Core.Clock;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;

namespace RivetSim.Presentation.App
{
    /// <summary>
    /// 高倍速保护（M8）：仿真按固定子步推进（RobotRuntime），倍速只增加每帧子步数，单步动力学与 ×1 相同；
    /// 风险在于每帧子步过多导致实际帧率过低（演示卡顿、输入迟滞）。倍速 ≥ CFG_SimSettings.speedGuardFromMultiplier 时按窗口统计实际帧率，
    /// 低于 speedGuardMinFps 则自动降到 speedGuardFallbackMultiplier 并发出提示。快进验证与暂停时不统计。
    /// </summary>
    public sealed class SpeedGuard
    {
        private readonly SimClock _clock;
        private readonly SimSettings _settings;
        private float _windowS;
        private int _frames;
        private int _maxSubsteps;

        /// <summary>降速时触发（参数为提示文字）。</summary>
        public event Action<string> Triggered;

        /// <summary>触发次数。</summary>
        public int TriggerCount { get; private set; }

        /// <summary>最近一个完整窗口的平均帧率，单位 FPS（未统计时为 0）。</summary>
        public float LastWindowFps { get; private set; }

        /// <summary>最近一个完整窗口的最大每帧子步数。</summary>
        public int LastWindowMaxSubsteps { get; private set; }

        /// <summary>已统计的完整窗口数。</summary>
        public int WindowCount { get; private set; }

        /// <summary>创建。</summary>
        public SpeedGuard(SimClock clock, SimSettings settings)
        {
            _clock = clock;
            _settings = settings;
        }

        /// <summary>每帧调用；substeps 为本帧推进的仿真子步数，fastForward 为快进验证中（不统计）。</summary>
        public void Tick(float realDeltaS, int substeps, bool fastForward)
        {
            if (fastForward || _clock.IsPaused || _clock.SpeedMultiplier < _settings.speedGuardFromMultiplier - 1e-4f)
            {
                _windowS = 0f;
                _frames = 0;
                _maxSubsteps = 0;
                return;
            }
            _windowS += realDeltaS;
            _frames++;
            if (substeps > _maxSubsteps) _maxSubsteps = substeps;
            if (_windowS < _settings.speedGuardWindowS) return;

            LastWindowFps = _frames / _windowS;
            LastWindowMaxSubsteps = _maxSubsteps;
            WindowCount++;
            _windowS = 0f;
            _frames = 0;
            _maxSubsteps = 0;
            if (LastWindowFps >= _settings.speedGuardMinFps) return;

            float from = _clock.SpeedMultiplier;
            if (!_clock.TrySetSpeed(_settings.speedGuardFallbackMultiplier)) return;
            TriggerCount++;
            string text = $"×{from:0.##} 下实际帧率 {LastWindowFps:0} FPS 低于 {_settings.speedGuardMinFps:0} FPS，已自动降到 ×{_settings.speedGuardFallbackMultiplier:0.##}";
            SimLog.Warning(LogCategory.System, "倍速保护：" + text);
            Triggered?.Invoke(text);
        }
    }
}
