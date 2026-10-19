using System;
using System.Threading;
using RivetSim.Core.Config;
using RivetSim.Simulation.Inspection;
using RivetSim.Simulation.Navigation;
using RivetSim.Simulation.Robot;
using RivetSim.Simulation.World;
using UnityEngine;

namespace RivetSim.Simulation.Mission
{
    /// <summary>
    /// 执行器可调用的仿真对象与配置（由启动入口组装），以及按仿真时间等待的工具方法。
    /// 纯逻辑单元（安全许可、调度、数据与无物理动作的确认）按 CFG_SimSettings.logicCellNominalDurationS 执行。
    /// </summary>
    public sealed class MissionServices
    {
        /// <summary>导航服务（S1）。</summary>
        public NavigationService Navigation { get; }

        /// <summary>底盘（S1）。</summary>
        public AgvController Chassis { get; }

        /// <summary>升降柱（S3 同时负责）。</summary>
        public LiftColumnController Lift { get; }

        /// <summary>机械臂（S3）。</summary>
        public ArmController Arm { get; }

        /// <summary>检测动作（S2 / S3 / S4 / S5）。</summary>
        public RivetInspectionService Inspection { get; }

        /// <summary>接触力模型（S4，教学简化模型）。</summary>
        public ContactModel Contact { get; }

        /// <summary>安全监视（S6）。</summary>
        public SafetyMonitor Safety { get; }

        /// <summary>检测区登记表。</summary>
        public RivetRegistry Registry { get; }

        /// <summary>全局设置。</summary>
        public SimSettings Settings { get; }

        /// <summary>AGV 配置。</summary>
        public AgvConfig Agv { get; }

        /// <summary>机械臂配置。</summary>
        public ArmConfig ArmConfig { get; }

        /// <summary>检测配置。</summary>
        public InspectionConfig InspectionConfig { get; }

        /// <summary>导航配置。</summary>
        public NavigationConfig NavigationConfig { get; }

        /// <summary>条件触发源与故障注入。</summary>
        public ConditionBus Conditions { get; }

        /// <summary>条件 C1～C12 的阈值与上报。</summary>
        public ConditionEvaluator Evaluator { get; }

        /// <summary>执行器共享状态。</summary>
        public MissionBlackboard Board { get; }

        /// <summary>仿真时间来源，单位 s。</summary>
        public Func<double> TimeS { get; }

        /// <summary>模拟操作员与噪声用随机数。</summary>
        public System.Random Random { get; }

        /// <summary>组装。</summary>
        public MissionServices(NavigationService navigation, AgvController chassis, LiftColumnController lift, ArmController arm,
            RivetInspectionService inspection, ContactModel contact, SafetyMonitor safety, RivetRegistry registry,
            SimSettings settings, AgvConfig agv, ArmConfig armConfig, InspectionConfig inspectionConfig, NavigationConfig navigationConfig,
            ConditionBus conditions, ConditionEvaluator evaluator, MissionBlackboard board, Func<double> timeS, int randomSeed)
        {
            Navigation = navigation;
            Chassis = chassis;
            Lift = lift;
            Arm = arm;
            Inspection = inspection;
            Contact = contact;
            Safety = safety;
            Registry = registry;
            Settings = settings;
            Agv = agv;
            ArmConfig = armConfig;
            InspectionConfig = inspectionConfig;
            NavigationConfig = navigationConfig;
            Conditions = conditions;
            Evaluator = evaluator;
            Board = board;
            TimeS = timeS;
            Random = new System.Random(randomSeed);
        }

        /// <summary>当前区域。</summary>
        public InspectionRegion Region(MissionCursor cursor) => Registry.Regions[cursor.RegionIndex];

        /// <summary>当前工位（同时按位置清零共享状态）。</summary>
        public InspectionStation Station(MissionCursor cursor)
        {
            Board.Sync(cursor, TimeS(), false);
            return Registry.Regions[cursor.RegionIndex].Stations[cursor.StationIndex];
        }

        /// <summary>当前铆钉（同时按位置清零共享状态）。</summary>
        public Rivet Rivet(MissionCursor cursor)
        {
            Board.Sync(cursor, TimeS(), true);
            return Registry.Regions[cursor.RegionIndex].Stations[cursor.StationIndex].Rivets[cursor.RivetIndex];
        }

        /// <summary>按仿真时间等待 durationS。</summary>
        public async Awaitable WaitSimAsync(float durationS, CancellationToken ct)
        {
            double end = TimeS() + durationS;
            while (TimeS() < end) await Awaitable.NextFrameAsync(ct);
        }

        /// <summary>每帧检查，直到 condition 成立。</summary>
        public async Awaitable WaitUntilAsync(Func<bool> condition, CancellationToken ct)
        {
            while (!condition()) await Awaitable.NextFrameAsync(ct);
        }

        /// <summary>纯逻辑单元：立即求值，再按标称时长等待（便于观众看清）。</summary>
        public async Awaitable<ExecutorResult> LogicAsync(Func<ExecutorResult> evaluate, CancellationToken ct)
        {
            ExecutorResult result = evaluate();
            await WaitSimAsync(Settings.logicCellNominalDurationS, ct);
            return result;
        }

        /// <summary>纯逻辑单元：先按标称时长等待（接收数据），再求值。</summary>
        public async Awaitable<ExecutorResult> LogicAfterAsync(Func<ExecutorResult> evaluate, CancellationToken ct)
        {
            await WaitSimAsync(Settings.logicCellNominalDurationS, ct);
            return evaluate();
        }

        /// <summary>注册纯逻辑执行器（立即求值）。</summary>
        public void RegisterLogic(ExecutorRegistry registry, string key, Func<CellDefCursor, ExecutorResult> evaluate) =>
            registry.Register(key, new DelegateExecutor((cell, cursor, ct) => LogicAsync(() => evaluate(new CellDefCursor(cell, cursor)), ct)));

        /// <summary>机械臂动作结果转执行结果。</summary>
        public static ExecutorResult FromArm(ArmMotionResult r, string done) => r.Success ? ExecutorResult.Done(done) : ExecutorResult.Fail(r.FailureReason);

        /// <summary>导航结果转执行结果。</summary>
        public static ExecutorResult FromNav(NavigationResult r, string done) => r.Success ? ExecutorResult.Done(done) : ExecutorResult.Fail(r.FailureReason);
    }
}
