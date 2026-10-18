using RivetSim.Core.Clock;
using RivetSim.Core.Config;
using RivetSim.Core.Events;
using RivetSim.Core.Logging;
using RivetSim.Simulation.Navigation;
using RivetSim.Simulation.Robot;

namespace RivetSim.Presentation.App
{
    /// <summary>
    /// 导航运行时组装（供 AppBootstrap 与 SandboxNavDemo 共用）：仿真时钟、事件总线、安全监视、占据栅格、激光雷达、导航服务与建图流程；
    /// 每帧按仿真时间以固定子步推进（导航控制 → 底盘 → 升降柱 → 激光），控制精度与倍速无关。
    /// </summary>
    public sealed class NavigationRuntime
    {
        private readonly AgvController _chassis;
        private readonly LiftColumnController _lift;
        private readonly Lidar2D _lidar;
        private readonly float _stepS;
        private float _accumulatorS;

        /// <summary>仿真时钟。</summary>
        public SimClock Clock { get; }

        /// <summary>事件总线。</summary>
        public EventBus Bus { get; }

        /// <summary>安全监视（S6）。</summary>
        public SafetyMonitor Safety { get; }

        /// <summary>占据栅格地图。</summary>
        public OccupancyGridMap Map { get; }

        /// <summary>导航服务（S1）。</summary>
        public NavigationService Navigation { get; }

        /// <summary>巡游建图流程。</summary>
        public MappingRoutine Mapping { get; }

        /// <summary>组装运行时。</summary>
        /// <param name="stepS">仿真子步长（取项目 Fixed Timestep），单位 s。</param>
        public NavigationRuntime(SimSettings settings, AgvConfig agv, NavigationConfig nav, AgvController chassis, LiftColumnController lift,
            Lidar2D lidar, GridGeometry mapGeometry, KeepOutMask keepOut, float speedMultiplier, float stepS)
        {
            _chassis = chassis;
            _lift = lift;
            _lidar = lidar;
            _stepS = stepS;

            Clock = new SimClock(settings.speedOptions, settings.defaultSpeedMultiplier);
            if (!Clock.TrySetSpeed(speedMultiplier))
                SimLog.Warning(LogCategory.System, $"倍速 {speedMultiplier} 不在倍速列表内，使用默认倍速 {Clock.SpeedMultiplier}");
            SimLog.SimTimeProvider = () => Clock.TimeS;

            Bus = new EventBus();
            Safety = new SafetyMonitor(chassis, lift, new PlaceholderArmPoseState());
            chassis.Initialize(Safety);
            lift.Initialize(Safety, Bus);

            Map = new OccupancyGridMap(mapGeometry);
            lidar.Initialize(chassis, Map, settings.randomSeed);
            float swept = AgvFootprint.MeasureSweptRadiusM(chassis.transform);
            float front = AgvFootprint.MeasureFrontReachM(chassis.transform);
            Navigation = new NavigationService(chassis, Safety, Map, keepOut, agv, nav, Bus, () => Clock.TimeS, swept, front, settings.randomSeed + 1);
            Mapping = new MappingRoutine(Navigation, Map, Bus, agv.cruiseSpeedMps);
            SimLog.Info(LogCategory.Nav,
                $"导航初始化：地图 {mapGeometry.width} × {mapGeometry.height} 格（{mapGeometry.resolutionM:0.00} m）；膨胀半径 {agv.BodyCircumscribedRadiusM:0.000} m（车体外接圆）；" +
                $"含机械臂扫掠半径 {swept:0.000} m、车头前伸 {front:0.000} m；虚拟禁行区 {(keepOut != null ? $"{keepOut.Channels.Count} 条接近通道" : "无")}");
        }

        /// <summary>按真实帧间隔推进仿真时间与全部控制器。</summary>
        public void Update(float realDeltaS)
        {
            _accumulatorS += Clock.Advance(realDeltaS);
            while (_accumulatorS >= _stepS)
            {
                _accumulatorS -= _stepS;
                Navigation.Tick(_stepS);
                _chassis.Tick(_stepS);
                _lift.Tick(_stepS);
                _lidar.Tick(_stepS);
            }
        }

        /// <summary>释放：解除日志时间来源。</summary>
        public void Dispose() => SimLog.SimTimeProvider = null;
    }
}
