using System;
using System.Threading;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Presentation.Input;
using RivetSim.Presentation.Visualization;
using RivetSim.Simulation.Navigation;
using RivetSim.Simulation.Robot;
using RivetSim.Simulation.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RivetSim.Presentation.App
{
    /// <summary>
    /// Sandbox_Robot 的导航开发入口（M3）：两堆货箱夹出 1.8 m 窄通道、一块模拟机身虚拟禁行区与一个测试工位。
    /// 进入 Play 模式后自动执行 建图 → 导航到预停靠位姿 → 对接 → 倒车退出；之后可用 WASD 遥控、空格急停，
    /// M 重新建图、N 导航到预停靠位姿、K 对接、R 倒车退出。引用由 SandboxSceneBuilder 写入。
    /// </summary>
    public sealed class SandboxNavDemo : MonoBehaviour
    {
        [Header("配置")]
        [SerializeField] private SimSettings simSettings;
        [SerializeField] private AgvConfig agvConfig;
        [SerializeField] private NavigationConfig navigationConfig;

        [Header("场景")]
        [SerializeField] private AgvController chassis;
        [SerializeField] private InspectionStation station;
        [SerializeField] private VirtualKeepOutZone keepOutZone;
        [SerializeField, Tooltip("地图中心（x = 世界 X，y = 世界 Z），单位 m")] private Vector2 mapCenterM;
        [SerializeField, Tooltip("地图尺寸，单位 m")] private Vector2 mapSizeM = new Vector2(20f, 14f);
        [SerializeField, Tooltip("沙盒巡游路线，单位 m")] private Vector2[] mappingRouteM = Array.Empty<Vector2>();

        [Header("可视化与输入")]
        [SerializeField] private OccupancyGridOverlay mapOverlay;
        [SerializeField] private KeepOutOverlay keepOutOverlay;
        [SerializeField] private LidarPointCloudView lidarView;
        [SerializeField] private PathView pathView;
        [SerializeField] private NavOverlayView overlayView;
        [SerializeField] private AgvTeleopInput teleopInput;

        [Header("演示")]
        [SerializeField, Tooltip("进入 Play 模式后自动执行一遍")] private bool autoRun = true;
        [SerializeField, Tooltip("演示倍速（须在 CFG_SimSettings 的倍速列表内）")] private float demoSpeedMultiplier = 4f;

        private NavigationRuntime _runtime;
        private CancellationTokenSource _cancellation;
        private bool _running;

        /// <summary>编辑器生成器写入配置与场景引用。</summary>
        public void Configure(SimSettings settings, AgvConfig agv, NavigationConfig nav, AgvController agvController, InspectionStation testStation,
            VirtualKeepOutZone keepOut, Vector2 centerM, Vector2 sizeM, Vector2[] route)
        {
            simSettings = settings;
            agvConfig = agv;
            navigationConfig = nav;
            chassis = agvController;
            station = testStation;
            keepOutZone = keepOut;
            mapCenterM = centerM;
            mapSizeM = sizeM;
            mappingRouteM = route;
        }

        /// <summary>编辑器生成器写入可视化与输入引用。</summary>
        public void ConfigureViews(OccupancyGridOverlay map, KeepOutOverlay keepOut, LidarPointCloudView lidar, PathView path,
            NavOverlayView overlay, AgvTeleopInput teleop)
        {
            mapOverlay = map;
            keepOutOverlay = keepOut;
            lidarView = lidar;
            pathView = path;
            overlayView = overlay;
            teleopInput = teleop;
        }

        private void Awake()
        {
            Lidar2D lidar = chassis.GetComponentInChildren<Lidar2D>();
            KeepOutMask keepOut = keepOutZone != null ? keepOutZone.ToMask() : null;
            GridGeometry geometry = GridGeometry.Covering(mapCenterM, mapSizeM, navigationConfig.gridResolutionM);
            _runtime = new NavigationRuntime(simSettings, agvConfig, navigationConfig, chassis, chassis.GetComponent<LiftColumnController>(),
                lidar, geometry, keepOut, demoSpeedMultiplier, Time.fixedDeltaTime);

            if (mapOverlay != null) mapOverlay.Initialize(_runtime.Map);
            if (keepOut != null && keepOutOverlay != null) keepOutOverlay.Initialize(keepOut, _runtime.Map.Geometry);
            if (lidarView != null) lidarView.Initialize(lidar);
            if (pathView != null) pathView.Initialize(_runtime.Bus);
            if (overlayView != null)
            {
                overlayView.Initialize(_runtime.Clock, _runtime.Bus);
                overlayView.SetHint("WASD 遥控 · 空格急停 · M 建图 · N 导航 · K 对接 · R 倒车退出 · V 可视化");
            }
            if (teleopInput != null)
            {
                teleopInput.Initialize(chassis, _runtime.Navigation);
                teleopInput.TeleopEnabled = true;
            }
            _cancellation = new CancellationTokenSource();
        }

        private void Start()
        {
            if (autoRun) Run(FullDemoAsync, "沙盒自动演示");
        }

        private void Update()
        {
            _runtime?.Update(Time.deltaTime);
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || _running) return;
            if (keyboard.mKey.wasPressedThisFrame) Run(MapAsync, "建图");
            else if (keyboard.nKey.wasPressedThisFrame) Run(NavigateAsync, "导航到预停靠位姿");
            else if (keyboard.kKey.wasPressedThisFrame) Run(DockAsync, "对接");
            else if (keyboard.rKey.wasPressedThisFrame) Run(ReverseAsync, "倒车退出");
        }

        private void OnDestroy()
        {
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;
            _runtime?.Dispose();
        }

        private async void Run(Func<CancellationToken, Awaitable<bool>> action, string label)
        {
            _running = true;
            if (overlayView != null) overlayView.SetPhase(label);
            try
            {
                bool ok = await action(_cancellation.Token);
                if (overlayView != null) overlayView.SetPhase(ok ? $"{label}：完成" : $"{label}：未完成（见日志）");
            }
            catch (OperationCanceledException)
            {
                SimLog.Info(LogCategory.System, $"{label} 已中止");
            }
            catch (Exception ex)
            {
                // async void 中未捕获的异常不会回到调用方，这里记录后结束。
                SimLog.Error(LogCategory.System, $"{label} 出错：{ex}");
            }
            finally
            {
                _running = false;
            }
        }

        private async Awaitable<bool> FullDemoAsync(CancellationToken ct)
        {
            SimLog.Info(LogCategory.System, $"沙盒导航演示开始（{_runtime.Clock.SpeedMultiplier:0.#}× 倍速）");
            if (!await MapAsync(ct) || !await NavigateAsync(ct) || !await DockAsync(ct) || !await ReverseAsync(ct)) return false;
            SimLog.Info(LogCategory.System, $"沙盒导航演示结束，总用时 {_runtime.Clock.TimeS:0.0} s（仿真时间）");
            return true;
        }

        private async Awaitable<bool> MapAsync(CancellationToken ct) => (await _runtime.Mapping.RunAsync(mappingRouteM, ct)).Success;

        private async Awaitable<bool> NavigateAsync(CancellationToken ct) =>
            (await _runtime.Navigation.NavigateToAsync(_runtime.Navigation.PreDockPose(station.DockingPose), $"导航到 {station.StationId} 预停靠位姿", ct)).Success;

        private async Awaitable<bool> DockAsync(CancellationToken ct) => (await _runtime.Navigation.DockAsync(station, ct)).Success;

        private async Awaitable<bool> ReverseAsync(CancellationToken ct) =>
            (await _runtime.Navigation.ReverseExitAsync(agvConfig.reverseExitDistanceM, ct)).Success;
    }
}
