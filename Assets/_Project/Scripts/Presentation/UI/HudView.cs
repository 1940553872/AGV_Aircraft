using System;
using System.Collections.Generic;
using RivetSim.Core.Config;
using RivetSim.Simulation.Mission;
using UnityEngine;
using UnityEngine.UIElements;

namespace RivetSim.Presentation.UI
{
    /// <summary>
    /// 屏幕 HUD（M6）：模式切换（自动 / 分步 / 遥控）、倍速（取 CFG_SimSettings.speedOptions）、暂停、下一步、
    /// 故障注入按钮（识别失败 / 力超限 / 采集无效）、机位 1～4 与自动机位、可视化开关、矩阵全屏、快捷键提示与致谢角标；
    /// t(1,8) 识别失败时弹出小地图点选提示。只发出请求事件，由入口转给 DemoModeController / ConditionBus / CameraRig。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HudView : MonoBehaviour
    {
        private const string ActiveClass = "hud-btn--active";

        private readonly List<Button> _speedButtons = new List<Button>();
        private readonly List<float> _speeds = new List<float>();
        private IDemoStatusSource _status;
        private IMiniMapSource _map;
        private UiThemeConfig _theme;
        private VisualElement _root, _main, _poseScrim;
        private Button _modeAuto, _modeStep, _modeManual, _pause, _next, _vis, _fullscreen;
        private Button _faultRecognition, _faultForce, _faultAcq;
        private readonly Button[] _cams = new Button[5];
        private Label _time, _phase, _faultStatus, _credit, _poseCountdown;
        private MiniMapElement _poseMap;
        private DemoMode _mode = DemoMode.Auto;
        private bool _built, _modeDirty = true;
        private long _shownSecond = -1;
        private float _shownSpeed = -1f;
        private bool _shownPaused;
        private bool _poseOpen, _poseAuto;
        private float _poseDelayS;
        private double _poseOpenedSimS;
        private int _poseShownSecond = -1;
        private int _cameraPreset = -1;
        private bool _cameraAuto;

        /// <summary>请求切换模式。</summary>
        public event Action<DemoMode> ModeRequested;

        /// <summary>请求倍速。</summary>
        public event Action<float> SpeedRequested;

        /// <summary>请求暂停 / 继续。</summary>
        public event Action PauseToggled;

        /// <summary>请求“下一步”（分步模式推进到下一阶段）。</summary>
        public event Action NextRequested;

        /// <summary>请求注入故障。</summary>
        public event Action<FaultKind> FaultRequested;

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

        /// <summary>绑定状态源、倍速列表与小地图。</summary>
        public void Initialize(IDemoStatusSource status, IReadOnlyList<float> speedOptions, IMiniMapSource map, UiThemeConfig theme)
        {
            _status = status;
            _map = map;
            _theme = theme;
            _speeds.Clear();
            foreach (float s in speedOptions) _speeds.Add(s);
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

        /// <summary>故障注入状态文字。</summary>
        public void SetFaultStatus(string text)
        {
            if (Bind()) _faultStatus.text = text;
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

        private bool Bind()
        {
            if (_built) return true;
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null || root.Q("hud-main") == null) return false;
            _root = root;
            _main = root.Q("hud-main");
            _poseScrim = root.Q("hud-pose-scrim");
            _modeAuto = Hook("hud-mode-auto", () => ModeRequested?.Invoke(DemoMode.Auto));
            _modeStep = Hook("hud-mode-step", () => ModeRequested?.Invoke(DemoMode.Step));
            _modeManual = Hook("hud-mode-manual", () => ModeRequested?.Invoke(DemoMode.Manual));
            _pause = Hook("hud-pause", () => PauseToggled?.Invoke());
            _next = Hook("hud-next", () => NextRequested?.Invoke());
            _vis = Hook("hud-vis", () => VisualizationToggled?.Invoke());
            _fullscreen = Hook("hud-fullscreen", () => FullscreenToggled?.Invoke());
            _faultRecognition = Hook("hud-fault-recognition", () => FaultRequested?.Invoke(FaultKind.RecognitionFailed));
            _faultForce = Hook("hud-fault-force", () => FaultRequested?.Invoke(FaultKind.ContactForceExceeded));
            _faultAcq = Hook("hud-fault-acq", () => FaultRequested?.Invoke(FaultKind.AcquisitionInvalid));
            _cams[0] = Hook("hud-cam-auto", () => CameraRequested?.Invoke(0));
            for (int i = 1; i <= 4; i++)
            {
                int preset = i;
                _cams[i] = Hook("hud-cam-" + i, () => CameraRequested?.Invoke(preset));
            }
            _time = root.Q<Label>("hud-time");
            _phase = root.Q<Label>("hud-phase");
            _faultStatus = root.Q<Label>("hud-fault-status");
            _credit = root.Q<Label>("hud-credit");
            _poseCountdown = root.Q<Label>("hud-pose-countdown");

            VisualElement speeds = root.Q("hud-speeds");
            speeds.Clear();
            _speedButtons.Clear();
            foreach (float s in _speeds)
            {
                float speed = s;
                var b = new Button(() => SpeedRequested?.Invoke(speed)) { text = $"×{s:0.##}" };
                b.AddToClassList("hud-btn");
                speeds.Add(b);
                _speedButtons.Add(b);
            }

            _poseMap = new MiniMapElement();
            _poseMap.style.flexGrow = 1f;
            root.Q("hud-pose-map").Add(_poseMap);
            if (_map != null) _poseMap.SetSource(_map, _theme != null ? _theme.miniMapRepaintThresholdPx : 0.5f);
            _poseMap.Picked += p => PosePicked?.Invoke(p);
            _built = true;
            return true;
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
                    for (int i = 0; i < _speedButtons.Count; i++) _speedButtons[i].EnableInClassList(ActiveClass, Mathf.Approximately(_speeds[i], _shownSpeed));
                    _pause.text = _shownPaused ? "继续" : "暂停";
                    _pause.EnableInClassList("hud-btn--paused", _shownPaused);
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
