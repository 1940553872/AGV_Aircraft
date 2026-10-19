using System;
using System.Collections.Generic;
using System.Threading;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Presentation.CameraControl;
using RivetSim.Presentation.Input;
using RivetSim.Presentation.UI;
using RivetSim.Presentation.Visualization;
using RivetSim.Simulation.Mission;
using RivetSim.Simulation.Navigation;
using RivetSim.Simulation.Robot;
using RivetSim.Simulation.World;
using UnityEngine;

namespace RivetSim.Presentation.App
{
    /// <summary>
    /// 主场景唯一入口（M3 建立，ADR-015；M4 加入机械臂与检测，ADR-016）：加载配置、在充电位运行时生成 AGV（ADR-009）、
    /// 组装仿真时钟 / 安全监视 / 地图 / 导航服务 / 机械臂与检测服务（<see cref="RobotRuntime"/>），初始化可视化与遥控，
    /// 并按 CFG_SimSettings.missionEntry 运行：Mission（默认，M5）为 SOP 完整工序 <see cref="MissionRoutine"/>；
    /// SubsystemDemo 为 M3 / M4 的子系统演示 <see cref="NavigationDemoRoutine"/>（仅调试，不再维护）。
    /// 场景引用由 MainSceneBuilder 写入；设备指示灯（<see cref="DeviceIndicator"/>）在启动时查找并订阅。
    /// M6：演示屏（<see cref="DemoConsoleView"/>，渲染到演示台显示器与大屏）、HUD、CameraRig 与演示模式由 <see cref="DemoUiHub"/> 连线，
    /// 取代 M3 / M4 的 NavOverlayView 与 InspectionOverlayView（类保留，主场景不再挂载）。
    /// </summary>
    public sealed class AppBootstrap : MonoBehaviour
    {
        [Header("配置")]
        [SerializeField] private SimSettings simSettings;
        [SerializeField] private SceneLayoutConfig sceneLayout;
        [SerializeField] private AgvConfig agvConfig;
        [SerializeField] private ArmConfig armConfig;
        [SerializeField] private NavigationConfig navigationConfig;
        [SerializeField] private InspectionConfig inspectionConfig;

        [Header("场景")]
        [SerializeField, Tooltip("AGV 预制体（运行时生成）")] private GameObject agvPrefab;
        [SerializeField, Tooltip("Robot 根节点")] private Transform robotRoot;
        [SerializeField, Tooltip("虚拟禁行区")] private VirtualKeepOutZone keepOutZone;
        [SerializeField, Tooltip("检测区登记表")] private RivetRegistry rivetRegistry;

        [Header("可视化与输入")]
        [SerializeField] private OccupancyGridOverlay mapOverlay;
        [SerializeField] private KeepOutOverlay keepOutOverlay;
        [SerializeField] private LidarPointCloudView lidarView;
        [SerializeField] private PathView pathView;
        [SerializeField] private NavOverlayView overlayView;
        [SerializeField] private AgvTeleopInput teleopInput;
        [SerializeField] private InspectionOverlayView inspectionOverlay;
        [SerializeField] private RivetResultRingView resultRings;
        [SerializeField] private WristCameraFrustumView cameraFrustum;
        [SerializeField] private ProbeContactView contactView;

        [Header("演示界面（M6）")]
        [SerializeField] private UiThemeConfig uiTheme;
        [SerializeField] private DemoConsoleView demoConsole;
        [SerializeField] private HudView hud;
        [SerializeField] private CameraRig cameraRig;
        [SerializeField] private VisualizationToggleInput visualizationToggle;

        [Header("演示屏（M8：右屏状态视图与大屏视图切换）")]
        [SerializeField, Tooltip("右屏“检测结果与设备状态”演示屏")] private DemoConsoleView statusConsole;
        [SerializeField, Tooltip("大屏屏幕网格渲染器")] private Renderer bigScreenRenderer;
        [SerializeField, Tooltip("大屏显示任务矩阵时的材质（M_DemoScreen）")] private Material bigScreenMatrixMaterial;
        [SerializeField, Tooltip("大屏显示检测结果与设备状态时的材质（M_DemoScreenStatus）")] private Material bigScreenStatusMaterial;

        [Header("演示（倍速取 CFG_SimSettings.defaultSpeedMultiplier；目标工位仅用于 SubsystemDemo）")]
        [SerializeField, Tooltip("进入 Play 模式后自动运行演示")] private bool autoRunDemo = true;
        [SerializeField, Tooltip("演示目标区域序号（从 0 起）")] private int demoRegionIndex;
        [SerializeField, Tooltip("演示目标工位序号（区域内，从 0 起）")] private int demoStationIndex;

        private RobotRuntime _runtime;
        private AgvController _chassis;
        private LiftColumnController _lift;
        private CancellationTokenSource _cancellation;
        private MissionRoutine _mission;
        private DemoModeController _demoMode;
        private RuntimeMiniMapSource _miniMap;
        private DemoUiHub _hub;
        private SpeedGuard _speedGuard;
        private KeepOutMask _keepOutMask;
        private bool _missionRunning;

        /// <summary>当前主场景入口（验证工具读取）。</summary>
        public static AppBootstrap Active { get; private set; }

        /// <summary>演示屏。</summary>
        public DemoConsoleView DemoConsole => demoConsole;

        /// <summary>HUD。</summary>
        public HudView Hud => hud;

        /// <summary>相机机位。</summary>
        public CameraRig Rig => cameraRig;

        /// <summary>界面连线（工序开始后可用）。</summary>
        public DemoUiHub Hub => _hub;

        /// <summary>右屏状态演示屏（M8）。</summary>
        public DemoConsoleView StatusConsole => statusConsole;

        /// <summary>大屏渲染器（M8，验证用）。</summary>
        public Renderer BigScreenRenderer => bigScreenRenderer;

        /// <summary>倍速保护（M8，工序开始后可用）。</summary>
        public SpeedGuard Guard => _speedGuard;

        /// <summary>本轮随机故障注入点（M8；Verify 给出固定注入点或关闭随机故障时为空）。</summary>
        public IReadOnlyList<FaultInjectionPoint> RandomFaultPlan { get; private set; } = Array.Empty<FaultInjectionPoint>();

        /// <summary>仿真运行时（验证用）。</summary>
        public RobotRuntime Runtime => _runtime;

        /// <summary>编辑器生成器写入演示界面引用（M6）。</summary>
        public void ConfigureDemoUi(UiThemeConfig theme, DemoConsoleView console, HudView hudView, CameraRig rig, VisualizationToggleInput visualization)
        {
            uiTheme = theme;
            demoConsole = console;
            hud = hudView;
            cameraRig = rig;
            visualizationToggle = visualization;
        }

        /// <summary>编辑器生成器写入演示屏引用（M8：右屏状态演示屏、大屏渲染器与两种材质）。</summary>
        public void ConfigureDemoScreens(DemoConsoleView status, Renderer bigScreen, Material matrixMaterial, Material statusMaterial)
        {
            statusConsole = status;
            bigScreenRenderer = bigScreen;
            bigScreenMatrixMaterial = matrixMaterial;
            bigScreenStatusMaterial = statusMaterial;
        }

        /// <summary>编辑器生成器写入配置。</summary>
        public void ConfigureConfigs(SimSettings settings, SceneLayoutConfig layout, AgvConfig agv, ArmConfig arm, NavigationConfig nav, InspectionConfig inspection)
        {
            simSettings = settings;
            sceneLayout = layout;
            agvConfig = agv;
            armConfig = arm;
            navigationConfig = nav;
            inspectionConfig = inspection;
        }

        /// <summary>编辑器生成器写入场景引用。</summary>
        public void ConfigureScene(GameObject agv, Transform robot, VirtualKeepOutZone keepOut, RivetRegistry registry)
        {
            agvPrefab = agv;
            robotRoot = robot;
            keepOutZone = keepOut;
            rivetRegistry = registry;
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

        /// <summary>编辑器生成器写入检测可视化引用（M4）。</summary>
        public void ConfigureInspectionViews(InspectionOverlayView overlay, RivetResultRingView rings, WristCameraFrustumView frustum, ProbeContactView contact)
        {
            inspectionOverlay = overlay;
            resultRings = rings;
            cameraFrustum = frustum;
            contactView = contact;
        }

        private void Awake()
        {
            Active = this;
            PlanarPose spawn = sceneLayout.chargingDockPose;
            GameObject agv = Instantiate(agvPrefab, spawn.WorldPosition, spawn.WorldRotation, robotRoot);
            agv.name = "AGV";
            _chassis = agv.GetComponent<AgvController>();
            _lift = agv.GetComponent<LiftColumnController>();
            Lidar2D lidar = agv.GetComponentInChildren<Lidar2D>();

            Vector2 hall = new Vector2(sceneLayout.hallSizeM.x, sceneLayout.hallSizeM.y);
            GridGeometry mapGeometry = GridGeometry.Covering(Vector2.zero, hall, navigationConfig.gridResolutionM);
            KeepOutMask keepOut = keepOutZone != null ? keepOutZone.ToMask() : null;
            _keepOutMask = keepOut;
            _runtime = new RobotRuntime(simSettings, agvConfig, navigationConfig, armConfig, inspectionConfig, _chassis, _lift,
                agv.GetComponentInChildren<ArmController>(), lidar, mapGeometry, keepOut, simSettings.defaultSpeedMultiplier, Time.fixedDeltaTime);

            // 序列化引用未赋值时是 Unity 的“假空”对象，须用 != null 判断，不能用 ?.。
            if (mapOverlay != null) mapOverlay.Initialize(_runtime.Map);
            if (keepOut != null && keepOutOverlay != null) keepOutOverlay.Initialize(keepOut, _runtime.Map.Geometry);
            if (lidarView != null) lidarView.Initialize(lidar);
            if (pathView != null) pathView.Initialize(_runtime.Bus);
            if (overlayView != null)
            {
                overlayView.Initialize(_runtime.Clock, _runtime.Bus);
                overlayView.SetHint("WASD 遥控（演示结束后）· 空格急停 · V 显示 / 隐藏可视化");
            }
            if (teleopInput != null) teleopInput.Initialize(_chassis, _runtime.Navigation);
            InspectionSceneViews.Initialize(_runtime, armConfig, inspectionConfig, inspectionOverlay, resultRings, cameraFrustum, contactView);
            _cancellation = new CancellationTokenSource();
            SimLog.Info(LogCategory.System, $"AppBootstrap：AGV 已在充电位生成 ({spawn.positionM.x:0.0}, {spawn.positionM.y:0.0})，偏航 {spawn.yawDeg:0}°");
        }

        private async Awaitable Start()
        {
            if (!autoRunDemo)
            {
                if (teleopInput != null) teleopInput.TeleopEnabled = true;
                if (overlayView != null) overlayView.SetPhase("遥控模式");
                return;
            }
            if (simSettings.missionEntry == MissionEntry.Mission)
            {
                await RunMissionAsync();
                return;
            }
            InspectionStation station = FindStation();
            if (station == null) return;
            try
            {
                var demo = new NavigationDemoRoutine(_runtime, _chassis, _lift, sceneLayout, agvConfig, armConfig, navigationConfig, overlayView, inspectionOverlay);
                bool ok = await demo.RunAsync(station, _cancellation.Token);
                if (!ok)
                {
                    SimLog.Error(LogCategory.Mission, "演示未完成，详见上方日志");
                    if (overlayView != null) overlayView.SetPhase("演示未完成（见日志），已切换到遥控");
                }
            }
            catch (OperationCanceledException)
            {
                SimLog.Info(LogCategory.System, "演示已中止");
            }
            if (teleopInput != null) teleopInput.TeleopEnabled = true;
        }

        private async Awaitable RunMissionAsync()
        {
            _mission = new MissionRoutine(_runtime, rivetRegistry, simSettings, sceneLayout, agvConfig, armConfig, navigationConfig, inspectionConfig,
                overlayView, inspectionOverlay);
            foreach (DeviceIndicator indicator in FindObjectsByType<DeviceIndicator>(FindObjectsSortMode.None)) indicator.Initialize(_runtime.Bus);
            if (overlayView != null) overlayView.SetHint("故障由系统随机注入并自动处理 · 空格急停 · V 显示 / 隐藏可视化");
            SetUpDemoUi();
            if (MissionLaunchOptions.FastForward) _runtime.FastForwardSimSecondsPerFrame = simSettings.fastVerifySimSecondsPerFrame;
            if (MissionLaunchOptions.Faults.Count > 0) _mission.ScheduleFaults(MissionLaunchOptions.Faults);
            else if (simSettings.randomFaultsEnabled) ScheduleRandomFaults();
            MissionLaunchOptions.Started?.Invoke(_runtime, _mission);
            MissionRunReport report = null;
            _missionRunning = true;
            try
            {
                report = await _mission.RunAsync(_cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                SimLog.Info(LogCategory.System, "工序已中止");
                return;
            }
            finally
            {
                _missionRunning = false;
            }
            MissionLaunchOptions.Completed?.Invoke(report);
            if (teleopInput != null) teleopInput.TeleopEnabled = true;
        }

        // M8：按种子生成本轮随机故障（每类至多一次），由 ScheduledFaultInjector 在进入匹配阶段时注入。
        private void ScheduleRandomFaults()
        {
            List<FaultInjectionPoint> plan = RandomFaultScheduler.CreatePlan(new RegistryMissionPlan(rivetRegistry),
                simSettings.randomSeed + simSettings.randomFaultSeedOffset, simSettings.randomFaultProbability);
            RandomFaultPlan = plan;
            _mission.ScheduleFaults(plan);
            SimLog.Info(LogCategory.Mission, $"随机故障（种子 {simSettings.randomSeed + simSettings.randomFaultSeedOffset}）：" +
                (plan.Count > 0 ? string.Join("；", plan) : "本轮无"));
        }

        // M6 演示界面：演示模式、小地图数据源、演示屏、HUD、相机与快捷键。
        private void SetUpDemoUi()
        {
            if (_mission.Orchestrator == null || uiTheme == null) return;
            var matrix = _mission.Orchestrator.Matrix;
            RivetRegistry registry = _mission.Registry;
            _demoMode = new DemoModeController(_runtime.Clock, _runtime.Bus, simSettings.defaultDemoMode)
            {
                CanEnterManual = () => !_missionRunning,
                SetTeleop = enabled => { if (teleopInput != null) teleopInput.TeleopEnabled = enabled; },
                RegionCountSource = () => registry != null ? registry.Regions.Count : 0,
                StationCountSource = r => registry != null && r >= 0 && r < registry.Regions.Count ? registry.Regions[r].Stations.Count : 0,
                RivetCountSource = () => _mission.Orchestrator.Cursor.RivetCount,
            };
            _demoMode.BindOperatorPose(_mission.OperatorPose);
            _miniMap = new RuntimeMiniMapSource(_runtime.Map, _chassis, _runtime.Bus, uiTheme.miniMapRefreshIntervalS, _keepOutMask, registry,
                sceneLayout.chargingDockPose);
            var device = new RuntimeDeviceStatusSource(_runtime, agvConfig, simSettings.randomSeed);
            if (demoConsole != null)
                demoConsole.Initialize(_runtime.Bus, matrix.Data, matrix.GetState, _demoMode, _miniMap, inspectionConfig, uiTheme, device);
            if (statusConsole != null)
                statusConsole.Initialize(_runtime.Bus, matrix.Data, matrix.GetState, _demoMode, _miniMap, inspectionConfig, uiTheme, device);
            if (cameraRig != null)
            {
                cameraRig.Initialize(uiTheme, _runtime.Bus, matrix.Data, _chassis.transform, _runtime.Arm.Rig.Tcp, sceneLayout.startPose);
                cameraRig.ConfigureRivetView(new MissionRivetFocusSource(_mission), RivetViewOccluders(), ~(1 << SimLayers.VisualizationIndex));
            }
            _speedGuard = new SpeedGuard(_runtime.Clock, simSettings);
            if (hud == null || demoConsole == null) return;
            hud.Initialize(_demoMode, _runtime.Clock.SpeedOptions, _miniMap, uiTheme);
            var hotkeys = gameObject.AddComponent<DemoHotkeys>();
            _hub = new DemoUiHub(_demoMode, _runtime.Bus, matrix.Data, hud, demoConsole, cameraRig, _mission.Conditions, _mission.OperatorPose,
                visualizationToggle, hotkeys, _miniMap, uiTheme);
            if (bigScreenRenderer != null && bigScreenMatrixMaterial != null && bigScreenStatusMaterial != null)
                _hub.BigScreenViewHandler = status => bigScreenRenderer.sharedMaterial = status ? bigScreenStatusMaterial : bigScreenMatrixMaterial;
            _speedGuard.Triggered += _hub.ShowSpeedGuardNotice;
        }

        // 机位遮挡检查用的车体与机械臂渲染器（探头末端是取景目标，不计入；可视化层不计入）。
        private Renderer[] RivetViewOccluders()
        {
            Transform endEffector = _runtime.Arm.Rig.Probe != null ? _runtime.Arm.Rig.Probe.transform : null;
            var list = new List<Renderer>();
            foreach (Renderer r in _chassis.GetComponentsInChildren<Renderer>(true))
            {
                if (r.gameObject.layer == SimLayers.VisualizationIndex || !(r is MeshRenderer)) continue;
                if (endEffector != null && r.transform.IsChildOf(endEffector)) continue;
                list.Add(r);
            }
            return list.ToArray();
        }

        private void Update()
        {
            _runtime?.Update(Time.deltaTime);
            _miniMap?.Refresh(Time.unscaledDeltaTime);
            if (_runtime != null) _speedGuard?.Tick(Time.unscaledDeltaTime, _runtime.LastFrameSubsteps, _runtime.FastForwardSimSecondsPerFrame > 0f);
        }

        private void OnDestroy()
        {
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;
            _hub?.Dispose();
            _demoMode?.Dispose();
            _miniMap?.Dispose();
            if (Active == this) Active = null;
            _mission?.Dispose();
            _mission = null;
            _runtime?.Dispose();
        }

        private InspectionStation FindStation()
        {
            if (rivetRegistry == null || demoRegionIndex >= rivetRegistry.Regions.Count ||
                demoStationIndex >= rivetRegistry.Regions[demoRegionIndex].Stations.Count)
            {
                SimLog.Error(LogCategory.System, $"找不到演示工位（区域 {demoRegionIndex}，工位 {demoStationIndex}）");
                return null;
            }
            return rivetRegistry.Regions[demoRegionIndex].Stations[demoStationIndex];
        }
    }
}
