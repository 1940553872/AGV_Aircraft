using RivetSim.Core.Clock;
using RivetSim.Core.Config;
using RivetSim.Core.Events;
using RivetSim.Core.Logging;
using RivetSim.Simulation.Inspection;
using RivetSim.Simulation.Navigation;
using RivetSim.Simulation.Robot;

namespace RivetSim.Presentation.App
{
    /// <summary>
    /// 机器人运行时组装（AppBootstrap、SandboxNavDemo 与 SandboxInspectionDemo 共用；M3 的 NavigationRuntime 在 M4 扩展并改名）：
    /// 仿真时钟、事件总线、安全监视、占据栅格、激光雷达、导航服务与建图流程，机械臂控制器、接触力模型与单钉检测服务。
    /// 每帧按仿真时间以固定子步推进（导航控制 → 底盘 → 升降柱 → 机械臂 → 激光），控制精度与倍速无关。
    /// 启动时机械臂置于运输姿态，并校核运输姿态外廓（车体投影内、整机高度 ≤ 上限、扫掠半径相对膨胀半径）。
    /// </summary>
    public sealed class RobotRuntime
    {
        private readonly AgvController _chassis;
        private readonly LiftColumnController _lift;
        private readonly ArmController _arm;
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

        /// <summary>机械臂控制器（S3）。</summary>
        public ArmController Arm => _arm;

        /// <summary>接触力模型（S4，教学简化模型）。</summary>
        public ContactModel Contact { get; }

        /// <summary>单钉检测服务（S2 / S3 / S4 / S5）。</summary>
        public RivetInspectionService Inspection { get; }

        /// <summary>启动时量测的运输姿态外廓。</summary>
        public AgvEnvelope TransportEnvelope { get; }

        /// <summary>组装运行时。</summary>
        /// <param name="stepS">仿真子步长（取项目 Fixed Timestep），单位 s。</param>
        public RobotRuntime(SimSettings settings, AgvConfig agv, NavigationConfig nav, ArmConfig armConfig, InspectionConfig inspection,
            AgvController chassis, LiftColumnController lift, ArmController arm, Lidar2D lidar, GridGeometry mapGeometry, KeepOutMask keepOut,
            float speedMultiplier, float stepS)
        {
            _chassis = chassis;
            _lift = lift;
            _arm = arm;
            _lidar = lidar;
            _stepS = stepS;

            Clock = new SimClock(settings.speedOptions, settings.defaultSpeedMultiplier);
            if (!Clock.TrySetSpeed(speedMultiplier))
                SimLog.Warning(LogCategory.System, $"倍速 {speedMultiplier} 不在倍速列表内，使用默认倍速 {Clock.SpeedMultiplier}");
            SimLog.SimTimeProvider = () => Clock.TimeS;

            Bus = new EventBus();
            Safety = new SafetyMonitor(chassis, lift, arm);
            chassis.Initialize(Safety);
            lift.Initialize(Safety, Bus);
            Contact = new ContactModel(inspection, armConfig.toolFloatTravelM);
            arm.Initialize(Safety, Bus, Contact, settings.randomSeed + 2, 1 << SimLayers.AircraftIndex);
            arm.SetJointsImmediate(arm.NamedPose(ArmConfig.StowPoseName));
            TransportEnvelope = CheckTransportEnvelope(agv, nav);

            Map = new OccupancyGridMap(mapGeometry);
            lidar.Initialize(chassis, Map, settings.randomSeed);
            Navigation = new NavigationService(chassis, Safety, Map, keepOut, agv, nav, Bus, () => Clock.TimeS, settings.randomSeed + 1);
            Mapping = new MappingRoutine(Navigation, Map, Bus, agv.cruiseSpeedMps);

            var vision = new WristVision(armConfig, new System.Random(settings.randomSeed + 3));
            var synthesizer = new AScanSynthesizer(inspection, new System.Random(settings.randomSeed + 4));
            Inspection = new RivetInspectionService(arm, Contact, vision, synthesizer, new GateJudge(inspection), new InspectionRecordStore(),
                armConfig, inspection, Bus, () => Clock.TimeS);
            SimLog.Info(LogCategory.Nav,
                $"导航初始化：地图 {mapGeometry.width} × {mapGeometry.height} 格（{mapGeometry.resolutionM:0.00} m）；膨胀半径 {agv.BodyCircumscribedRadiusM:0.000} m（车体外接圆）；" +
                $"虚拟禁行区 {(keepOut != null ? $"{keepOut.Channels.Count} 条接近通道" : "无")}");
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
                _arm.Tick(_stepS);
                _lidar.Tick(_stepS);
            }
        }

        /// <summary>释放：解除日志时间来源。</summary>
        public void Dispose() => SimLog.SimTimeProvider = null;

        // 运输姿态外廓（渲染器包围盒，偏保守）：机械臂在车体投影内、整机高度不超过上限；扫掠半径超出膨胀半径一个栅格以上时警告。
        private AgvEnvelope CheckTransportEnvelope(AgvConfig agv, NavigationConfig nav)
        {
            AgvEnvelope e = AgvFootprint.Measure(_chassis.transform, _arm.transform, false);
            bool inside = e.ArmInsideBody(agv.bodySizeM.y, agv.bodySizeM.x);
            bool low = e.TopM <= agv.transportHeightMaxM;
            string text = $"运输姿态外廓（包围盒）：{e}；车体投影 {agv.bodySizeM.x:0.00} × {agv.bodySizeM.y:0.00} m，高度上限 {agv.transportHeightMaxM:0.00} m，膨胀半径 {agv.BodyCircumscribedRadiusM:0.000} m";
            if (!inside || !low) SimLog.Error(LogCategory.Safety, $"{text}——机械臂超出车体投影或整机过高");
            else if (e.SweptRadiusM > agv.BodyCircumscribedRadiusM + nav.gridResolutionM) SimLog.Warning(LogCategory.Nav, $"{text}——扫掠半径超出膨胀半径一个栅格以上");
            else SimLog.Info(LogCategory.Arm, text);
            return e;
        }
    }
}
