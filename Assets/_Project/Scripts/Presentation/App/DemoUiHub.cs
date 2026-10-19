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
    /// 演示界面连线（M6，主场景与 Sandbox_UI 共用）：HUD / 快捷键的请求转给 DemoModeController、CameraRig、
    /// 可视化开关与演示屏全屏；t(1,8) 操作员初值通道打开时弹出小地图点选提示，点选结果写回通道（再由执行器写入 MissionBlackboard.InitialPose）。
    /// M8：故障类条件单元被激活时（ConditionBus.AnomalyRaised）弹出 3 s 提示“系统检测到异常：…，正在自动处理”；
    /// B 键 / HUD 按钮切换大屏视图（全屏时切换全屏视图）；倍速保护降速时提示。不再提供手动故障注入。
    /// v1.1：Esc / HUD“退出”按钮打开退出确认框（打开期间暂停仿真，取消后恢复），确认后经 <see cref="AppExit"/> 退出程序。
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
        private readonly UiThemeConfig _theme;
        private bool _bigScreenStatus;
        private bool _pausedForExit;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();

        /// <summary>演示模式控制器（验证工具用）。</summary>
        public DemoModeController Mode => _mode;

        /// <summary>“下一步”的处理（默认 DemoModeController.Next；Sandbox_UI 改为推进假数据）。</summary>
        public Action NextHandler { get; set; }

        /// <summary>大屏视图切换的处理（参数为是否显示“检测结果与设备状态”；主场景替换大屏材质，可为空）。</summary>
        public Action<bool> BigScreenViewHandler { get; set; }

        /// <summary>大屏当前是否显示“检测结果与设备状态”。</summary>
        public bool IsBigScreenStatus => _bigScreenStatus;

        /// <summary>连线。rig / conditions / operatorPose / visualization 可为空。</summary>
        public DemoUiHub(DemoModeController mode, EventBus bus, TaskMatrixData data, HudView hud, DemoConsoleView console, CameraRig rig,
            ConditionBus conditions, OperatorPoseChannel operatorPose, VisualizationToggleInput visualization, DemoHotkeys hotkeys, IMiniMapSource map,
            UiThemeConfig theme)
        {
            _mode = mode;
            _data = data;
            _theme = theme;
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
            hud.BigScreenViewToggled += ToggleBigScreenView;
            hud.CameraRequested += OnCamera;
            hud.VisualizationToggled += OnVisualization;
            hud.FullscreenToggled += ToggleFullscreen;
            hud.PosePicked += OnPosePicked;
            hud.ExitRequested += OpenExitConfirm;
            hud.ExitConfirmed += AppExit.Quit;
            hud.ExitCancelled += CloseExitConfirm;
            hud.SetMode(mode.Mode);
            mode.ModeChanged += hud.SetMode;
            if (hotkeys != null)
            {
                hotkeys.CameraPressed += OnCamera;
                hotkeys.FullscreenPressed += ToggleFullscreen;
                hotkeys.PausePressed += mode.TogglePause;
                hotkeys.NextPressed += OnNext;
                hotkeys.ViewPressed += ToggleBigScreenView;
                hotkeys.EscapePressed += OnEscape;
                hotkeys.ConfirmPressed += OnConfirm;
            }
            if (rig != null)
            {
                rig.Changed += hud.SetCamera;
                hud.SetCamera(rig.Preset, rig.IsAuto);
            }
            else hud.SetCamera(1, true);
            if (conditions != null) conditions.AnomalyRaised += OnAnomaly;
            hud.SetBigScreenView(false);
            if (operatorPose != null)
            {
                operatorPose.Opened += OnPoseOpened;
                operatorPose.Closed += OnPoseClosed;
            }
            _subscriptions.Add(bus.Subscribe<PhaseEnteredEvent>(OnPhase));
        }

        /// <summary>打开退出确认框，并暂停仿真（原本已暂停则不动）。</summary>
        public void OpenExitConfirm()
        {
            if (_hud.IsExitConfirmOpen) return;
            _pausedForExit = !_mode.IsPaused;
            if (_pausedForExit) _mode.TogglePause();
            _hud.ShowExitConfirm();
        }

        /// <summary>关闭退出确认框，恢复打开前的暂停状态。</summary>
        public void CloseExitConfirm()
        {
            _hud.HideExitConfirm();
            if (_pausedForExit && _mode.IsPaused) _mode.TogglePause();
            _pausedForExit = false;
        }

        private void OnEscape()
        {
            if (_hud.IsExitConfirmOpen) CloseExitConfirm();
            else OpenExitConfirm();
        }

        private void OnConfirm()
        {
            if (_hud.IsExitConfirmOpen) AppExit.Quit();
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

        /// <summary>切换大屏视图（B 键 / HUD 按钮）；演示屏全屏时改为切换全屏视图。</summary>
        public void ToggleBigScreenView()
        {
            if (_console.IsFullscreen)
            {
                _console.ToggleView();
                return;
            }
            _bigScreenStatus = !_bigScreenStatus;
            BigScreenViewHandler?.Invoke(_bigScreenStatus);
            _hud.SetBigScreenView(_bigScreenStatus);
        }

        /// <summary>显示倍速保护提示。</summary>
        public void ShowSpeedGuardNotice(string text) => _hud.ShowAlert(text, _theme != null ? _theme.speedGuardToastS : 4f, true);

        private void OnAnomaly(FaultKind kind, string reason) =>
            _hud.ShowAlert($"系统检测到异常：{ConditionBus.Describe(kind)}（{reason}），正在自动处理", _theme != null ? _theme.anomalyToastS : 3f);

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
            if (_conditions != null) _conditions.AnomalyRaised -= OnAnomaly;
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
