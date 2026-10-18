using System;
using System.Collections.Generic;
using RivetSim.Core.Clock;
using RivetSim.Core.Events;
using RivetSim.Simulation.Navigation;
using UnityEngine;
using UnityEngine.UIElements;

namespace RivetSim.Presentation.Visualization
{
    /// <summary>
    /// M3 导航演示叠加信息（UI Toolkit）：当前阶段、仿真时间与倍速、建图进度、导航结果（路径长度 / 里程 / 用时）、停靠误差，
    /// 并在停靠位姿上方标注停靠误差。M6 的演示界面就绪后由 HUD 取代。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class NavOverlayView : MonoBehaviour
    {
        // 停靠误差标注在停靠位姿上方的高度，单位 m（外观常量）。
        private const float DockLabelHeightM = 1.6f;
        private const string FailClass = "dock-label--fail";

        [SerializeField, Tooltip("用于把世界坐标换算到屏幕的相机")]
        private Camera worldCamera;

        private SimClock _clock;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private Label _phase, _clockLabel, _mapping, _navigation, _docking, _hint, _dockLabel;
        private Vector3? _dockAnchor;

        /// <summary>编辑器生成器写入引用。</summary>
        public void Configure(Camera camera) => worldCamera = camera;

        /// <summary>绑定时钟并订阅导航事件。</summary>
        public void Initialize(SimClock clock, EventBus bus)
        {
            _clock = clock;
            _subscriptions.Add(bus.Subscribe<MappingProgressEvent>(OnMapping));
            _subscriptions.Add(bus.Subscribe<NavigationCompletedEvent>(OnNavigation));
            _subscriptions.Add(bus.Subscribe<DockingCompletedEvent>(OnDocking));
            _subscriptions.Add(bus.Subscribe<PathPlannedEvent>(_ => HideDockLabel()));
        }

        /// <summary>设置当前阶段文字。</summary>
        public void SetPhase(string text)
        {
            if (Bind()) _phase.text = text;
        }

        /// <summary>设置底部按键提示。</summary>
        public void SetHint(string text)
        {
            if (Bind()) _hint.text = text;
        }

        // UIDocument 在自身 OnEnable 时才建立元素树，入口的 Awake 可能更早，因此首次使用时再查询元素。
        private bool Bind()
        {
            if (_phase != null) return true;
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null || root.Q<Label>("phase") == null) return false;
            _phase = root.Q<Label>("phase");
            _clockLabel = root.Q<Label>("clock");
            _mapping = root.Q<Label>("mapping");
            _navigation = root.Q<Label>("navigation");
            _docking = root.Q<Label>("docking");
            _hint = root.Q<Label>("hint");
            _dockLabel = root.Q<Label>("dock-label");
            return true;
        }

        private void OnMapping(MappingProgressEvent e)
        {
            if (!Bind()) return;
            _mapping.text = $"建图：{e.Progress:P0}，已行驶 {e.TraveledM:0.0} m，已观测 {e.KnownCellCount:N0} 格";
        }

        private void OnNavigation(NavigationCompletedEvent e)
        {
            if (!Bind()) return;
            NavigationResult r = e.Result;
            _navigation.text = r.Success
                ? $"导航（{e.Label}）：路径 {r.PathLengthM:0.00} m · 里程 {r.TraveledM:0.00} m · 用时 {r.DurationS:0.0} s"
                : $"导航（{e.Label}）：未完成——{r.FailureReason}";
        }

        private void OnDocking(DockingCompletedEvent e)
        {
            if (!Bind()) return;
            DockingResult r = e.Result;
            if (!r.Success)
            {
                _docking.text = $"停靠（{r.StationId}）：失败——{r.FailureReason}";
                return;
            }
            _docking.text = $"停靠（{r.StationId}）：{r.Error}（{(r.WithinTolerance ? "合格" : "超出容差")}）";
            _dockLabel.text = $"{r.StationId} 停靠误差\n纵向 {r.Error.LongitudinalM * 1000f:+0.0;-0.0;0.0} mm  横向 {r.Error.LateralM * 1000f:+0.0;-0.0;0.0} mm\n偏航 {r.Error.YawDeg:+0.00;-0.00;0.00}°";
            _dockLabel.EnableInClassList(FailClass, !r.WithinTolerance);
            _dockAnchor = r.TargetPose.WorldPosition + Vector3.up * DockLabelHeightM;
        }

        private void HideDockLabel()
        {
            _dockAnchor = null;
            if (_dockLabel != null) _dockLabel.style.display = DisplayStyle.None;
        }

        private void LateUpdate()
        {
            if (_clock == null || !Bind()) return;
            TimeSpan t = TimeSpan.FromSeconds(_clock.TimeS);
            _clockLabel.text = $"仿真时间 {(int)t.TotalMinutes:00}:{t.Seconds:00} · 倍速 {_clock.SpeedMultiplier:0.#}×";

            if (!_dockAnchor.HasValue || worldCamera == null || _dockLabel.panel == null) return;
            Vector3 viewport = worldCamera.WorldToViewportPoint(_dockAnchor.Value);
            if (viewport.z <= 0f)
            {
                _dockLabel.style.display = DisplayStyle.None;
                return;
            }
            Vector2 panel = RuntimePanelUtils.CameraTransformWorldToPanel(_dockLabel.panel, _dockAnchor.Value, worldCamera);
            _dockLabel.style.display = DisplayStyle.Flex;
            float w = _dockLabel.resolvedStyle.width, h = _dockLabel.resolvedStyle.height;
            _dockLabel.style.left = panel.x - 0.5f * (float.IsNaN(w) ? 0f : w);
            _dockLabel.style.top = panel.y - (float.IsNaN(h) ? 0f : h);
        }

        private void OnDestroy()
        {
            foreach (IDisposable s in _subscriptions) s.Dispose();
            _subscriptions.Clear();
        }
    }
}
