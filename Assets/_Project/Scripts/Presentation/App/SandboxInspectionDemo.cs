using System;
using System.Text;
using System.Threading;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Presentation.UI;
using RivetSim.Presentation.Visualization;
using RivetSim.Simulation.Navigation;
using RivetSim.Simulation.Robot;
using RivetSim.Simulation.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RivetSim.Presentation.App
{
    /// <summary>
    /// Sandbox_Robot 的 M4 开发入口（默认启用）：AGV 已停在 A320 机身段旁的工位停靠位姿，进入 Play 模式后对该工位执行
    /// 单工位检测（<see cref="StationInspectionRoutine"/>）。结束后可按 I 重新检测（先清除结果）。引用由 SandboxSceneBuilder 写入。
    /// </summary>
    public sealed class SandboxInspectionDemo : MonoBehaviour
    {
        [Header("配置")]
        [SerializeField] private SimSettings simSettings;
        [SerializeField] private AgvConfig agvConfig;
        [SerializeField] private NavigationConfig navigationConfig;
        [SerializeField] private ArmConfig armConfig;
        [SerializeField] private InspectionConfig inspectionConfig;

        [Header("场景")]
        [SerializeField] private AgvController chassis;
        [SerializeField] private InspectionStation station;
        [SerializeField] private VirtualKeepOutZone keepOutZone;
        [SerializeField, Tooltip("地图中心（x = 世界 X，y = 世界 Z），单位 m")] private Vector2 mapCenterM;
        [SerializeField, Tooltip("地图尺寸，单位 m")] private Vector2 mapSizeM = new Vector2(20f, 14f);

        [Header("可视化")]
        [SerializeField] private NavOverlayView overlayView;
        [SerializeField] private InspectionOverlayView inspectionOverlay;
        [SerializeField] private RivetResultRingView resultRings;
        [SerializeField] private WristCameraFrustumView cameraFrustum;
        [SerializeField] private ProbeContactView contactView;

        [Header("演示")]
        [SerializeField, Tooltip("进入 Play 模式后自动执行一遍")] private bool autoRun = true;
        [SerializeField, Tooltip("演示倍速（须在 CFG_SimSettings 的倍速列表内）")] private float demoSpeedMultiplier = 2f;

        private RobotRuntime _runtime;
        private CancellationTokenSource _cancellation;
        private bool _running;

        /// <summary>运行时（截图与调试用）。</summary>
        public RobotRuntime Runtime => _runtime;

        /// <summary>编辑器生成器写入配置与场景引用。</summary>
        public void Configure(SimSettings settings, AgvConfig agv, NavigationConfig nav, ArmConfig arm, InspectionConfig inspection,
            AgvController agvController, InspectionStation testStation, VirtualKeepOutZone keepOut, Vector2 centerM, Vector2 sizeM)
        {
            simSettings = settings;
            agvConfig = agv;
            navigationConfig = nav;
            armConfig = arm;
            inspectionConfig = inspection;
            chassis = agvController;
            station = testStation;
            keepOutZone = keepOut;
            mapCenterM = centerM;
            mapSizeM = sizeM;
        }

        /// <summary>编辑器生成器写入可视化引用。</summary>
        public void ConfigureViews(NavOverlayView overlay, InspectionOverlayView inspection, RivetResultRingView rings, WristCameraFrustumView frustum,
            ProbeContactView contact)
        {
            overlayView = overlay;
            inspectionOverlay = inspection;
            resultRings = rings;
            cameraFrustum = frustum;
            contactView = contact;
        }

        private void Awake()
        {
            Lidar2D lidar = chassis.GetComponentInChildren<Lidar2D>();
            KeepOutMask keepOut = keepOutZone != null ? keepOutZone.ToMask() : null;
            GridGeometry geometry = GridGeometry.Covering(mapCenterM, mapSizeM, navigationConfig.gridResolutionM);
            _runtime = new RobotRuntime(simSettings, agvConfig, navigationConfig, armConfig, inspectionConfig, chassis,
                chassis.GetComponent<LiftColumnController>(), chassis.GetComponentInChildren<ArmController>(), lidar, geometry, keepOut,
                demoSpeedMultiplier, Time.fixedDeltaTime);
            if (overlayView != null)
            {
                overlayView.Initialize(_runtime.Clock, _runtime.Bus);
                overlayView.SetHint("I 重新检测 · V 显示 / 隐藏可视化");
            }
            InspectionSceneViews.Initialize(_runtime, armConfig, inspectionConfig, inspectionOverlay, resultRings, cameraFrustum, contactView);
            _cancellation = new CancellationTokenSource();
        }

        private void Start()
        {
            if (autoRun) Run();
        }

        private void Update()
        {
            _runtime?.Update(Time.deltaTime);
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && !_running && keyboard.iKey.wasPressedThisFrame) Run();
        }

        private void OnDestroy()
        {
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;
            _runtime?.Dispose();
        }

        private async void Run()
        {
            _running = true;
            try
            {
                _runtime.Inspection.Store.Clear();
                var summary = new StringBuilder($"沙盒 M4 检测汇总（{_runtime.Clock.SpeedMultiplier:0.#}× 倍速）：\n");
                var routine = new StationInspectionRoutine(_runtime, chassis, chassis.GetComponent<LiftColumnController>(), agvConfig, armConfig,
                    overlayView, inspectionOverlay);
                bool ok = await routine.RunAsync(station, summary, _cancellation.Token);
                if (ok) SimLog.Info(LogCategory.Mission, summary.ToString());
            }
            catch (OperationCanceledException)
            {
                SimLog.Info(LogCategory.System, "沙盒检测已中止");
            }
            catch (Exception ex)
            {
                // async void 中未捕获的异常不会回到调用方，这里记录后结束。
                SimLog.Error(LogCategory.System, $"沙盒检测出错：{ex}");
            }
            finally
            {
                _running = false;
            }
        }
    }
}
