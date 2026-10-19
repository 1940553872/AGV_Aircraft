using System;
using RivetSim.Core.Clock;
using RivetSim.Core.Config;
using RivetSim.Core.Events;
using RivetSim.Core.Logging;
using RivetSim.Core.Mission;
using RivetSim.Presentation.UI;
using RivetSim.Simulation.Mission;

namespace RivetSim.Presentation.App
{
    /// <summary>
    /// 演示模式控制（M6，Architecture §3.3）：自动演示 / 分步演示（每次进入新阶段后暂停仿真时钟，点“下一步”继续）/ 手动遥控
    /// （仅在工序未运行时可切换）。倍速取 CFG_SimSettings.speedOptions；自动演示时 t(1,8) 允许模拟操作员超时给出初值。
    /// 同时作为顶栏与 HUD 的 <see cref="IDemoStatusSource"/>。
    /// </summary>
    public sealed class DemoModeController : IDemoStatusSource, IDisposable
    {
        private const string AutoText = "自动演示";
        private const string StepText = "分步演示";
        private const string ManualText = "手动遥控";

        private readonly SimClock _clock;
        private readonly IDisposable _phaseSubscription;
        private OperatorPoseChannel _operatorPose;

        /// <summary>当前模式。</summary>
        public DemoMode Mode { get; private set; }

        /// <summary>模式变化。</summary>
        public event Action<DemoMode> ModeChanged;

        /// <summary>是否允许切换到手动遥控（主场景：工序未运行时）。</summary>
        public Func<bool> CanEnterManual { get; set; } = () => true;

        /// <summary>遥控开关（主场景写入 AgvTeleopInput.TeleopEnabled）。</summary>
        public Action<bool> SetTeleop { get; set; }

        /// <summary>区域总数来源。</summary>
        public Func<int> RegionCountSource { get; set; } = () => 0;

        /// <summary>区域内工位总数来源。</summary>
        public Func<int, int> StationCountSource { get; set; } = _ => 0;

        /// <summary>当前工位铆钉总数来源。</summary>
        public Func<int> RivetCountSource { get; set; } = () => 0;

        /// <summary>创建并订阅阶段事件。</summary>
        public DemoModeController(SimClock clock, EventBus bus, DemoMode initial)
        {
            _clock = clock;
            _phaseSubscription = bus.Subscribe<PhaseEnteredEvent>(OnPhaseEntered);
            Mode = initial == DemoMode.Manual ? DemoMode.Auto : initial;
        }

        /// <summary>绑定 t(1,8) 操作员初值通道（自动与分步演示时允许超时由模拟操作员给出，M7）。</summary>
        public void BindOperatorPose(OperatorPoseChannel channel)
        {
            _operatorPose = channel;
            if (_operatorPose != null) _operatorPose.AutoProvide = Mode != DemoMode.Manual;
        }

        /// <summary>切换模式；返回是否切换成功。</summary>
        public bool SetMode(DemoMode mode)
        {
            if (mode == Mode) return true;
            if (mode == DemoMode.Manual && !CanEnterManual())
            {
                SimLog.Warning(LogCategory.System, "工序运行中不能切换到手动遥控，请等待工序结束");
                return false;
            }
            DemoMode previous = Mode;
            Mode = mode;
            SetTeleop?.Invoke(mode == DemoMode.Manual);
            if (previous == DemoMode.Step && _clock.IsPaused) _clock.SetPaused(false);
            if (_operatorPose != null) _operatorPose.AutoProvide = mode != DemoMode.Manual;
            SimLog.Info(LogCategory.System, $"演示模式：{ModeText}");
            ModeChanged?.Invoke(mode);
            return true;
        }

        /// <summary>“下一步”：分步模式下继续运行到下一个阶段开始。</summary>
        public void Next()
        {
            if (Mode == DemoMode.Step && _clock.IsPaused) _clock.SetPaused(false);
        }

        /// <summary>暂停 / 继续。</summary>
        public void TogglePause() => _clock.TogglePause();

        /// <summary>设置倍速（须在倍速列表内）。</summary>
        public void SetSpeed(float speed) => _clock.TrySetSpeed(speed);

        private void OnPhaseEntered(PhaseEnteredEvent e)
        {
            if (Mode != DemoMode.Step) return;
            _clock.SetPaused(true);
            SimLog.Info(LogCategory.System, $"分步演示：进入 {e.PhaseCode}，已暂停，点“下一步”继续");
        }

        /// <inheritdoc />
        public string ModeText => Mode == DemoMode.Step ? StepText : Mode == DemoMode.Manual ? ManualText : AutoText;

        /// <inheritdoc />
        public double SimTimeS => _clock.TimeS;

        /// <inheritdoc />
        public float SpeedMultiplier => _clock.SpeedMultiplier;

        /// <inheritdoc />
        public bool IsPaused => _clock.IsPaused;

        /// <inheritdoc />
        public int RegionCount => RegionCountSource();

        /// <inheritdoc />
        public int StationCount(int regionIndex) => StationCountSource(regionIndex);

        /// <inheritdoc />
        public int RivetCount => RivetCountSource();

        /// <summary>取消订阅。</summary>
        public void Dispose() => _phaseSubscription.Dispose();
    }
}
