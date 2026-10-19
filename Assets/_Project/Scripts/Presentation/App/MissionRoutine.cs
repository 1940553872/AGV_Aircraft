using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Core.Mission;
using RivetSim.Core.TaskMatrix;
using RivetSim.Presentation.UI;
using RivetSim.Presentation.Visualization;
using RivetSim.Simulation.Inspection;
using RivetSim.Simulation.Mission;
using RivetSim.Simulation.Mission.Executors;
using RivetSim.Simulation.Navigation;
using RivetSim.Simulation.Robot;
using RivetSim.Simulation.World;
using UnityEngine;

namespace RivetSim.Presentation.App
{
    /// <summary>
    /// 主场景完整工序（M5，取代 NavigationDemoRoutine）：建图准备（不在矩阵内）→ SOP 任务矩阵 T1～T6（<see cref="MissionOrchestrator"/>，
    /// 含逐钉循环、换站与条件单元）→ 返回充电位（导航到充电位前方 + 直线倒车）→ 导出 CSV 检测记录并输出汇总。
    /// </summary>
    public sealed class MissionRoutine : IDisposable
    {
        /// <summary>CSV 子目录（persistentDataPath 下）。</summary>
        public const string ReportsFolder = "Reports";

        private readonly RobotRuntime _runtime;
        private readonly RivetRegistry _registry;
        private readonly SceneLayoutConfig _layout;
        private readonly AgvConfig _agv;
        private readonly NavigationConfig _nav;
        private readonly NavOverlayView _overlay;
        private readonly InspectionOverlayView _inspectionOverlay;
        private readonly MissionBlackboard _board;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private readonly int _executorKeyCount;
        private readonly List<string> _problems = new List<string>();
        private ScheduledFaultInjector _injector;

        /// <summary>条件触发源与故障注入接口（热键与 M6 HUD 调用）。</summary>
        public ConditionBus Conditions { get; }

        /// <summary>编排器（任务矩阵未能加载时为空）。</summary>
        public MissionOrchestrator Orchestrator { get; }

        /// <summary>运行记录。</summary>
        public MissionTrace Trace { get; }

        /// <summary>条件判定。</summary>
        public ConditionEvaluator Evaluator { get; }

        /// <summary>检测区登记表。</summary>
        public RivetRegistry Registry => _registry;

        /// <summary>组装：加载任务矩阵 JSON、注册 S1～S8 执行器并核对键名。</summary>
        public MissionRoutine(RobotRuntime runtime, RivetRegistry registry, SimSettings settings, SceneLayoutConfig layout, AgvConfig agv,
            ArmConfig arm, NavigationConfig nav, InspectionConfig inspection, NavOverlayView overlay, InspectionOverlayView inspectionOverlay)
        {
            _runtime = runtime;
            _registry = registry;
            _layout = layout;
            _agv = agv;
            _nav = nav;
            _overlay = overlay;
            _inspectionOverlay = inspectionOverlay;
            Conditions = new ConditionBus(runtime.Bus);
            Trace = new MissionTrace(runtime.Bus);
            _board = new MissionBlackboard(runtime.Bus);

            TaskMatrixLoadResult load = settings.taskMatrixJson != null
                ? TaskMatrixLoader.LoadFromJson(settings.taskMatrixJson.text)
                : null;
            if (load == null || !load.Success)
            {
                _problems.Add(load == null ? "CFG_SimSettings 未指定任务矩阵 JSON" : $"任务矩阵加载失败：{string.Join("；", load.Errors)}");
                SimLog.Error(LogCategory.Mission, _problems[0]);
                return;
            }
            TaskMatrixData data = load.Data;
            _executorKeyCount = data.GetExecutorKeys().Count;
            Evaluator = new ConditionEvaluator(data.Conditions, runtime.Bus);
            var services = new MissionServices(runtime.Navigation, runtime.Chassis, runtime.Lift, runtime.Arm,
                runtime.Inspection, runtime.Contact, runtime.Safety, registry, settings, agv, arm, inspection, nav,
                Conditions, Evaluator, _board, () => runtime.Clock.TimeS, settings.randomSeed + 5);
            var executors = new ExecutorRegistry();
            MissionExecutorCatalog.RegisterAll(executors, services);
            Orchestrator = new MissionOrchestrator(data, executors, Conditions, new RegistryMissionPlan(registry), runtime.Bus, () => runtime.Clock.TimeS)
            {
                RivetUnrecovered = (cursor, phase, reason) => MissionExecutorCatalog.HandleUnrecovered(services, cursor, phase, reason),
            };
            _problems.AddRange(Orchestrator.Validate());
            if (_problems.Count == 0)
                SimLog.Info(LogCategory.Mission, $"任务矩阵：{data.Cells.Count} 个单元、{_executorKeyCount} 个执行器键名全部已注册，回路 {data.Loops.Count} 条，条件 {data.Conditions.Items.Count} 项");
            else
                foreach (string p in _problems) SimLog.Error(LogCategory.Mission, $"启动核对：{p}");

            _subscriptions.Add(runtime.Bus.Subscribe<PhaseEnteredEvent>(OnPhaseEntered));
        }

        /// <summary>按计划自动注入故障（Verify 菜单）。</summary>
        public void ScheduleFaults(IEnumerable<FaultInjectionPoint> points)
        {
            _injector?.Dispose();
            _injector = new ScheduledFaultInjector(_runtime.Bus, Conditions, points);
        }

        /// <summary>运行完整工序；取消时抛出 OperationCanceledException。</summary>
        public async Awaitable<MissionRunReport> RunAsync(CancellationToken ct)
        {
            var report = new MissionRunReport
            {
                ExecutorKeyCount = _executorKeyCount,
                ValidationProblems = new List<string>(_problems),
                Trace = Trace,
                Records = _runtime.Inspection.Store.Records,
            };
            if (Orchestrator == null || _problems.Count > 0) return Fail(report, "启动核对未通过");

            NavigationService nav = _runtime.Navigation;
            double t0 = _runtime.Clock.TimeS;
            Phase("准备：环境建图（驶离充电位 → 巡游路线）");
            NavigationResult leave = await nav.DriveStraightAsync(_nav.dockApproachDistanceM, _agv.approachSpeedMps, "驶离充电位", ct);
            if (!leave.Success) return Fail(report, $"驶离充电位失败：{leave.FailureReason}");
            NavigationResult mapping = await _runtime.Mapping.RunAsync(_layout.mappingRouteM, ct);
            if (!mapping.Success) return Fail(report, $"建图失败：{mapping.FailureReason}");
            double t1 = _runtime.Clock.TimeS;
            report.MappingS = t1 - t0;

            MissionResult result = await Orchestrator.RunAsync(ct);
            report.Result = result;
            double t2 = _runtime.Clock.TimeS;
            report.MatrixS = t2 - t1;
            if (!result.Success) return Fail(report, result.FailureReason);

            Phase("检测完成：返回充电位");
            PlanarPose charger = _layout.chargingDockPose;
            var chargerFront = new PlanarPose(charger.positionM + DiffDriveKinematics.Heading(charger.yawDeg) * _nav.dockApproachDistanceM, charger.yawDeg);
            NavigationResult back = await nav.NavigateToAsync(chargerFront, "返回充电位前方", ct);
            if (!back.Success) return Fail(report, $"返回充电位失败：{back.FailureReason}");
            NavigationResult dockIn = await nav.DriveStraightAsync(-_nav.dockApproachDistanceM, _agv.approachSpeedMps, "倒车进入充电位", ct);
            if (!dockIn.Success) return Fail(report, $"倒车进入充电位失败：{dockIn.FailureReason}");
            DockingError parkError = DockingError.Between(charger, _runtime.Chassis.Pose);
            bool parked = parkError.IsWithin(_nav.goalPositionToleranceM, _nav.goalYawToleranceDeg);
            SimLog.Write(LogCategory.Nav, parked ? SimLogLevel.Info : SimLogLevel.Warning,
                $"返回充电位：停车误差（相对充电位）{parkError}，{(parked ? "在" : "超出")}到点容差 {_nav.goalPositionToleranceM * 1000f:0} mm / {_nav.goalYawToleranceDeg:0.0}° 内");
            double t3 = _runtime.Clock.TimeS;
            report.ReturnS = t3 - t2;
            report.TotalS = t3 - t0;

            string file = $"inspection_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            report.CsvPath = InspectionCsvExporter.Write(Path.Combine(Application.persistentDataPath, ReportsFolder, file), _runtime.Inspection.Store.Records, _registry);
            report.ConditionChecks = Evaluator.CheckCount;
            report.ConditionFailures = Evaluator.FailedCount;
            report.Success = true;
            Summarize(report);
            Phase("工序完成：已返回充电位，检测记录已导出");
            return report;
        }

        /// <summary>取消订阅。</summary>
        public void Dispose()
        {
            foreach (IDisposable s in _subscriptions) s.Dispose();
            _subscriptions.Clear();
            _injector?.Dispose();
            Trace.Dispose();
            _board.Dispose();
            Conditions.Dispose();
        }

        private void Summarize(MissionRunReport report)
        {
            IReadOnlyList<InspectionRecord> records = report.Records;
            int detections = 0, falseAlarms = 0, misses = 0, invalid = 0, defects = 0;
            foreach (InspectionRecord r in records)
            {
                if (r.Truth != DefectType.None) defects++;
                if (r.IsDetection) detections++;
                if (r.IsFalseAlarm) falseAlarms++;
                if (r.IsMiss) misses++;
                if (r.Verdict == InspectionVerdict.Invalid) invalid++;
            }
            SimLog.Info(LogCategory.Mission,
                $"[汇总] {records.Count} 颗铆钉：真值缺陷 {defects}、检出 {detections}、漏检 {misses}、误报 {falseAlarms}、无效 {invalid}；" +
                $"建图 {report.MappingS:0.0} s + 矩阵流程 {report.MatrixS:0.0} s + 返回 {report.ReturnS:0.0} s = {report.TotalS:0.0} s（仿真）；" +
                $"条件判定 {report.ConditionChecks} 次、未满足 {report.ConditionFailures} 次；CSV：{report.CsvPath}");
        }

        private MissionRunReport Fail(MissionRunReport report, string reason)
        {
            report.Success = false;
            report.FailureReason = reason;
            SimLog.Error(LogCategory.Mission, $"[工序] 未完成：{reason}");
            Phase($"工序未完成：{reason}");
            return report;
        }

        private void OnPhaseEntered(PhaseEnteredEvent e)
        {
            MissionCursor c = Orchestrator.Cursor;
            PhaseDef phase = Orchestrator.Matrix.Data.FindPhase(e.PhaseId);
            InspectionStation station = _registry.Regions[c.RegionIndex].Stations[c.StationIndex];
            Phase($"{e.PhaseCode} {phase?.Name}（{station.StationId}，铆钉 {c.RivetIndex + 1}/{c.RivetCount}）{(e.Attempt > 1 ? $" 重试 {e.Attempt - 1}" : "")}");
            if (_inspectionOverlay != null) _inspectionOverlay.SetProgress(station.StationId, c.RivetIndex + 1, c.RivetCount);
        }

        private void Phase(string text)
        {
            if (_overlay != null) _overlay.SetPhase(text);
            if (_inspectionOverlay != null) _inspectionOverlay.SetPhase(text);
        }
    }
}
