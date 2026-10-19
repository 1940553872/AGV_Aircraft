using System;
using System.Collections.Generic;
using RivetSim.Core.Config;
using RivetSim.Core.Events;
using RivetSim.Core.Mission;
using RivetSim.Core.TaskMatrix;
using RivetSim.Presentation.CameraControl;
using RivetSim.Presentation.Input;
using RivetSim.Presentation.UI;
using RivetSim.Simulation.Mission;
using UnityEngine;

namespace RivetSim.Presentation.App
{
    /// <summary>
    /// 演示界面连线（M6，主场景与 Sandbox_UI 共用）：HUD / 快捷键的请求转给 DemoModeController、ConditionBus、CameraRig、
    /// 可视化开关与演示屏全屏；t(1,8) 操作员初值通道打开时弹出小地图点选提示，点选结果写回通道（再由执行器写入 MissionBlackboard.InitialPose）。
    /// </summary>
    public sealed class DemoUiHub : IDisposable
    {
        private readonly DemoModeController _mode;
        private readonly HudView _hud;
        private readonly DemoConsoleView _console;
        private readonly CameraRig _rig;
        private readonly ConditionBus _conditions;
        private readonly OperatorPoseChannel _operatorPose;
        private readonly VisualizationToggleInput _visualization;
        private readonly DemoHotkeys _hotkeys;
        private readonly IMiniMapSource _map;
        private readonly TaskMatrixData _data;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();

        /// <summary>演示模式控制器（验证工具用）。</summary>
        public DemoModeController Mode => _mode;

        /// <summary>“下一步”的处理（默认 DemoModeController.Next；Sandbox_UI 改为推进假数据）。</summary>
        public Action NextHandler { get; set; }

        /// <summary>连线。rig / conditions / operatorPose / visualization 可为空。</summary>
        public DemoUiHub(DemoModeController mode, EventBus bus, TaskMatrixData data, HudView hud, DemoConsoleView console, CameraRig rig,
            ConditionBus conditions, OperatorPoseChannel operatorPose, VisualizationToggleInput visualization, DemoHotkeys hotkeys, IMiniMapSource map)
        {
            _mode = mode;
            _data = data;
            _hud = hud;
            _console = console;
            _rig = rig;
            _conditions = conditions;
            _operatorPose = operatorPose;
            _visualization = visualization;
            _hotkeys = hotkeys;
            _map = map;
            NextHandler = mode.Next;

            hud.ModeRequested += OnModeRequested;
            hud.SpeedRequested += mode.SetSpeed;
            hud.PauseToggled += mode.TogglePause;
            hud.NextRequested += OnNext;
            hud.FaultRequested += OnFault;
            hud.CameraRequested += OnCamera;
            hud.VisualizationToggled += OnVisualization;
            hud.FullscreenToggled += ToggleFullscreen;
            hud.PosePicked += OnPosePicked;
            hud.SetMode(mode.Mode);
            mode.ModeChanged += hud.SetMode;
            if (hotkeys != null)
            {
                hotkeys.CameraPressed += OnCamera;
                hotkeys.FullscreenPressed += ToggleFullscreen;
                hotkeys.PausePressed += mode.TogglePause;
                hotkeys.NextPressed += OnNext;
            }
            if (rig != null)
            {
                rig.Changed += hud.SetCamera;
                hud.SetCamera(rig.Preset, rig.IsAuto);
            }
            else hud.SetCamera(1, true);
            if (conditions != null) conditions.FaultInjected += OnFaultInjected;
            if (operatorPose != null)
            {
                operatorPose.Opened += OnPoseOpened;
                operatorPose.Closed += OnPoseClosed;
            }
            _subscriptions.Add(bus.Subscribe<PhaseEnteredEvent>(OnPhase));
            _subscriptions.Add(bus.Subscribe<CellStateChangedEvent>(OnCell));
        }

        /// <summary>切换演示屏全屏（Tab / HUD 按钮）。</summary>
        public void ToggleFullscreen() => SetFullscreen(!_console.IsFullscreen);

        /// <summary>设置演示屏全屏。</summary>
        public void SetFullscreen(bool fullscreen)
        {
            _console.SetFullscreen(fullscreen);
            _hud.SetConsoleFullscreen(fullscreen);
        }

        private void OnModeRequested(DemoMode mode) => _mode.SetMode(mode);

        private void OnNext() => NextHandler?.Invoke();

        private void OnFault(FaultKind kind)
        {
            if (_conditions != null) _conditions.Inject(kind);
            else _hud.SetFaultStatus($"{ConditionBus.Describe(kind)}：本场景未接入仿真");
        }

        private void OnFaultInjected(FaultKind kind) => _hud.SetFaultStatus($"已布设：{ConditionBus.Describe(kind)}（真实触发取决于仿真结果）");

        private void OnCell(CellStateChangedEvent e)
        {
            // 布设的故障触发了条件单元后清除提示。
            if (e.State == CellState.Triggered) _hud.SetFaultStatus($"{e.CellId} 已触发：{e.Note}");
        }

        private void OnCamera(int preset)
        {
            if (_rig == null)
            {
                _hud.SetCamera(preset == 0 ? 1 : preset, preset == 0);
                return;
            }
            if (preset == 0) _rig.SetAuto(true);
            else _rig.Select(preset);
        }

        private void OnVisualization()
        {
            if (_visualization != null) _visualization.Toggle();
        }

        private void OnPhase(PhaseEnteredEvent e)
        {
            PhaseDef phase = _data.FindPhase(e.PhaseId);
            _hud.SetPhaseText($"{e.PhaseCode} {phase?.Name} · 区域 {e.RegionIndex + 1} 工位 {e.StationIndex + 1} 铆钉 {e.RivetIndex + 1}" +
                              (e.Attempt > 1 ? $" · 第 {e.Attempt} 次" : ""));
        }

        private void OnPoseOpened(float delayS) => _hud.ShowPosePrompt(_operatorPose.AutoProvide, delayS);

        private void OnPoseClosed(bool usedPick) => _hud.HidePosePrompt();

        private void OnPosePicked(Vector2 worldM)
        {
            if (_operatorPose == null) return;
            // 教学简化：点选只给位置，朝向沿用当前估计。
            float yaw = _map != null ? _map.AgvPose.yawDeg : 0f;
            if (_operatorPose.Submit(new PlanarPose(worldM, yaw))) _hud.HidePosePrompt();
        }

        /// <summary>解除连线。</summary>
        public void Dispose()
        {
            foreach (IDisposable s in _subscriptions) s.Dispose();
            _subscriptions.Clear();
            if (_conditions != null) _conditions.FaultInjected -= OnFaultInjected;
            if (_operatorPose != null)
            {
                _operatorPose.Opened -= OnPoseOpened;
                _operatorPose.Closed -= OnPoseClosed;
            }
            _mode.ModeChanged -= _hud.SetMode;
            if (_rig != null) _rig.Changed -= _hud.SetCamera;
        }
    }
}
