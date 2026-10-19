using System;
using System.Collections.Generic;
using RivetSim.Core.Config;
using UnityEngine;
using UnityEngine.UIElements;

namespace RivetSim.Presentation.UI
{
    /// <summary>
    /// 屏幕 HUD（M6）：模式切换（自动 / 分步 / 遥控）、倍速下拉框（M8，取 CFG_SimSettings.speedOptions）、暂停、下一步、
    /// 机位 1～4 与自动机位、可视化开关、矩阵全屏、大屏视图切换（M8）、快捷键提示与致谢角标；
    /// 异常提示条（M8：“系统检测到异常：…，正在自动处理”，按实际时间自动隐藏）；t(1,8) 识别失败时弹出小地图点选提示。
    /// 只发出请求事件，由入口转给 DemoModeController / CameraRig 等。M8 起不再提供故障注入按钮。
    /// v1.1：右上角“Esc 退出”按钮与退出确认框（确认 / 取消只发事件，由 DemoUiHub 暂停仿真并退出程序）。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HudView : MonoBehaviour
    {
        private const string ActiveClass = "hud-btn--active";

        private const string AlertInfoClass = "hud-alert--info";

        private readonly List<float> _speeds = new List<float>();
        private readonly List<string> _speedChoices = new List<string>();
        private IDemoStatusSource _status;
        private IMiniMapSource _map;
        private UiThemeConfig _theme;
        private VisualElement _root, _main, _poseScrim, _alert, _exitScrim;
        private Button _modeAuto, _modeStep, _modeManual, _pause, _next, _vis, _fullscreen, _bigScreen;
        private DropdownField _speed;
        private float _alertUntil;
        private readonly Button[] _cams = new Button[5];
        private Label _time, _phase, _alertText, _credit, _poseCountdown;
        private MiniMapElement _poseMap;
        private DemoMode _mode = DemoMode.Auto;
        private bool _built, _modeDirty = true;
        private long _shownSecond = -1;
        private float _shownSpeed = -1f;
        private bool _shownPaused;
        private bool _poseOpen, _poseAuto, _exitOpen;
        private float _poseDelayS;
        private double _poseOpenedSimS;
        private int _poseShownSecond = -1;
        private int _cameraPreset = -1;
        private bool _cameraAuto;

        /// <summary>点击右上角“退出”按钮。</summary>
        public event Action ExitRequested;

        /// <summary>退出确认框中点击“退出”。</summary>
        public event Action ExitConfirmed;

        /// <summary>退出确认框中点击“取消”。</summary>
        public event Action ExitCancelled;

        /// <summary>退出确认框是否打开。</summary>
        public bool IsExitConfirmOpen => _exitOpen;

        /// <summary>请求切换模式。</summary>
        public event Action<DemoMode> ModeRequested;

        /// <summary>请求倍速。</summary>
        public event Action<float> SpeedRequested;

        /// <summary>请求暂停 / 继续。</summary>
        public event Action PauseToggled;

        /// <summary>请求“下一步”（分步模式推进到下一阶段）。</summary>
        public event Action NextRequested;

        /// <summary>请求切换大屏视图（任务矩阵 / 检测结果与设备状态）。</summary>
        public event Action BigScreenViewToggled;

        /// <summary>请求机位（0 自动，1～4 预设）。</summary>
        public event Action<int> CameraRequested;

        /// <summary>请求切换可视化。</summary>
        public event Action VisualizationToggled;

        /// <summary>请求切换矩阵全屏。</summary>
        public event Action FullscreenToggled;

        /// <summary>小地图点选的世界坐标（t(1,8) 初值）。</summary>
        public event Action<Vector2> PosePicked;

        /// <summary>初值点选提示是否显示。</summary>
        public bool IsPosePromptOpen => _poseOpen;

        /// <summary>界面根元素（验证用）。</summary>
        public VisualElement Root => _root;

        /// <summary>提示中的小地图（验证用）。</summary>
        public MiniMapElement PoseMap => _poseMap;

        /// <summary>当前提示条文字（未显示时为空；验证用）。</summary>
        public string AlertText => _alertUntil > Time.unscaledTime && _alertText != null ? _alertText.text : "";

        /// <summary>倍速下拉框（验证用）。</summary>
        public DropdownField SpeedDropdown => _speed;

        /// <summary>绑定状态源、倍速列表与小地图。</summary>
        public void Initialize(IDemoStatusSource status, IReadOnlyList<float> speedOptions, IMiniMapSource map, UiThemeConfig theme)
        {
            _status = status;
            _map = map;
            _theme = theme;
            _speeds.Clear();
            _speedChoices.Clear();
            foreach (float s in speedOptions)
            {
                _speeds.Add(s);
                _speedChoices.Add($"×{s:0.##}");
            }
        }

        /// <summary>当前模式（高亮按钮，分步模式才启用“下一步”）。</summary>
        public void SetMode(DemoMode mode)
        {
            _mode = mode;
            _modeDirty = true;
        }

        /// <summary>当前机位高亮。</summary>
        public void SetCamera(int preset, bool auto)
        {
            _cameraPreset = preset;
            _cameraAuto = auto;
            _modeDirty = true;
        }

        /// <summary>阶段文字（显示在时间下方）。</summary>
        public void SetPhaseText(string text)
        {
            if (Bind()) _phase.text = text;
        }

        /// <summary>显示提示条 durationS 秒（实际时间）；info 为普通提示（青色），否则为异常提示（琥珀色）。</summary>
        public void ShowAlert(string text, float durationS, bool info = false)
        {
            if (!Bind()) return;
            _alertText.text = text;
            _alert.EnableInClassList(AlertInfoClass, info);
            _alert.style.display = DisplayStyle.Flex;
            _alertUntil = Time.unscaledTime + durationS;
        }

        /// <summary>大屏当前视图（按钮文字）。</summary>
        public void SetBigScreenView(bool status)
        {
            if (Bind()) _bigScreen.text = status ? "B 大屏：检测与设备" : "B 大屏：任务矩阵";
        }

        /// <summary>矩阵全屏时隐藏 HUD 主体与致谢角标（全屏时不显示模型；返回提示在演示屏顶栏）。</summary>
        public void SetConsoleFullscreen(bool fullscreen)
        {
            if (!Bind()) return;
            _main.style.display = fullscreen ? DisplayStyle.None : DisplayStyle.Flex;
            _credit.style.display = fullscreen ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>显示 t(1,8) 小地图点选提示；autoProvide 时显示模拟操作员倒计时（delayS 为仿真时间，单位 s）。</summary>
        public void ShowPosePrompt(bool autoProvide, float delayS)
        {
            if (!Bind()) return;
            _poseOpen = true;
            _poseAuto = autoProvide;
            _poseDelayS = delayS;
            _poseOpenedSimS = _status != null ? _status.SimTimeS : 0d;
            _poseShownSecond = -1;
            _poseMap.ClearPick();
            _poseMap.PickEnabled = true;
            _poseScrim.style.display = DisplayStyle.Flex;
            _poseCountdown.text = autoProvide ? "" : "等待操作员点选（点选后继续）";
        }

        /// <summary>关闭点选提示。</summary>
        public void HidePosePrompt()
        {
            _poseOpen = false;
            if (!Bind()) return;
            _poseMap.PickEnabled = false;
            _poseScrim.style.display = DisplayStyle.None;
        }

        /// <summary>显示退出确认框（矩阵全屏时也显示）。</summary>
        public void ShowExitConfirm()
        {
            if (!Bind()) return;
            _exitOpen = true;
            _exitScrim.style.display = DisplayStyle.Flex;
        }

        /// <summary>关闭退出确认框。</summary>
        public void HideExitConfirm()
        {
            _exitOpen = false;
            if (!Bind()) return;
            _exitScrim.style.display = DisplayStyle.None;
        }

        private bool Bind()
        {
            if (_built) return true;
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null || root.Q("hud-main") == null) return false;
            _root = root;
            _main = root.Q("hud-main");
            _poseScrim = root.Q("hud-pose-scrim");
            _exitScrim = root.Q("hud-exit-scrim");
            Hook("hud-exit", () => ExitRequested?.Invoke());
            Hook("hud-exit-confirm", () => ExitConfirmed?.Invoke());
            Hook("hud-exit-cancel", () => ExitCancelled?.Invoke());
            _modeAuto = Hook("hud-mode-auto", () => ModeRequested?.Invoke(DemoMode.Auto));
            _modeStep = Hook("hud-mode-step", () => ModeRequested?.Invoke(DemoMode.Step));
            _modeManual = Hook("hud-mode-manual", () => ModeRequested?.Invoke(DemoMode.Manual));
            _pause = Hook("hud-pause", () => PauseToggled?.Invoke());
            _next = Hook("hud-next", () => NextRequested?.Invoke());
            _vis = Hook("hud-vis", () => VisualizationToggled?.Invoke());
            _fullscreen = Hook("hud-fullscreen", () => FullscreenToggled?.Invoke());
            _bigScreen = Hook("hud-bigscreen", () => BigScreenViewToggled?.Invoke());
            _cams[0] = Hook("hud-cam-auto", () => CameraRequested?.Invoke(0));
            for (int i = 1; i <= 4; i++)
            {
                int preset = i;
                _cams[i] = Hook("hud-cam-" + i, () => CameraRequested?.Invoke(preset));
            }
            _time = root.Q<Label>("hud-time");
            _phase = root.Q<Label>("hud-phase");
            _alert = root.Q("hud-alert");
            _alertText = root.Q<Label>("hud-alert-text");
            _credit = root.Q<Label>("hud-credit");
            _poseCountdown = root.Q<Label>("hud-pose-countdown");

            _speed = root.Q<DropdownField>("hud-speed");
            _speed.choices = _speedChoices;
            _speed.focusable = false;
            _speed.RegisterValueChangedCallback(OnSpeedChosen);

            _poseMap = new MiniMapElement();
            _poseMap.style.flexGrow = 1f;
            root.Q("hud-pose-map").Add(_poseMap);
            if (_map != null) _poseMap.SetSource(_map, _theme != null ? _theme.miniMapRepaintThresholdPx : 0.5f,
                _theme != null ? _theme.miniMapAgvMarkerSizePx : new Vector2(22f, 14f));
            _poseMap.Picked += p => PosePicked?.Invoke(p);
            _built = true;
            return true;
        }

        private void OnSpeedChosen(ChangeEvent<string> evt)
        {
            int index = _speedChoices.IndexOf(evt.newValue);
            if (index >= 0) SpeedRequested?.Invoke(_speeds[index]);
        }

        private Button Hook(string name, Action click)
        {
            var b = _root.Q<Button>(name);
            b.clicked += click;
            b.focusable = false;
            return b;
        }

        private void LateUpdate()
        {
            if (_status == null) return;
            UiPerfProbe.BeginFrame();
            if (Bind())
            {
                if (_modeDirty) RenderButtons();
                long second = (long)_status.SimTimeS;
                if (second != _shownSecond)
                {
                    _shownSecond = second;
                    _time.text = $"仿真 {second / 3600:00}:{second / 60 % 60:00}:{second % 60:00}";
                }
                if (!Mathf.Approximately(_status.SpeedMultiplier, _shownSpeed) || _status.IsPaused != _shownPaused)
                {
                    _shownSpeed = _status.SpeedMultiplier;
                    _shownPaused = _status.IsPaused;
                    for (int i = 0; i < _speeds.Count; i++)
                        if (Mathf.Approximately(_speeds[i], _shownSpeed)) _speed.SetValueWithoutNotify(_speedChoices[i]);
                    _pause.text = _shownPaused ? "继续" : "暂停";
                    _pause.EnableInClassList("hud-btn--paused", _shownPaused);
                }
                if (_alertUntil > 0f && Time.unscaledTime >= _alertUntil)
                {
                    _alertUntil = 0f;
                    _alert.style.display = DisplayStyle.None;
                }
                if (_poseOpen)
                {
                    _poseMap.Tick();
                    if (_poseAuto)
                    {
                        // 模拟操作员按仿真时间计时（暂停时倒计时停止）。
                        float remain = _poseDelayS - (float)(_status.SimTimeS - _poseOpenedSimS);
                        int s = Mathf.Max(0, Mathf.CeilToInt(remain));
                        if (s != _poseShownSecond)
                        {
                            _poseShownSecond = s;
                            _poseCountdown.text = $"自动演示：{s} s 后由模拟操作员给出初值";
                        }
                    }
                }
            }
            UiPerfProbe.EndFrame();
        }

        private void RenderButtons()
        {
            _modeDirty = false;
            _modeAuto.EnableInClassList(ActiveClass, _mode == DemoMode.Auto);
            _modeStep.EnableInClassList(ActiveClass, _mode == DemoMode.Step);
            _modeManual.EnableInClassList(ActiveClass, _mode == DemoMode.Manual);
            _next.SetEnabled(_mode == DemoMode.Step);
            for (int i = 0; i < _cams.Length; i++)
                _cams[i].EnableInClassList(ActiveClass, i == 0 ? _cameraAuto : !_cameraAuto && i == _cameraPreset);
        }
    }
}
