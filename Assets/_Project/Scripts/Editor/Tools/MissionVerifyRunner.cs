using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Core.Mission;
using RivetSim.Editor.Generators;
using RivetSim.Presentation.App;
using RivetSim.Simulation.Inspection;
using RivetSim.Simulation.Mission;
using RivetSim.Simulation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RivetSim.Editor.Tools
{
    /// <summary>
    /// M5 验证（RivetSim/Verify/Run Mission (Fast)）：在主场景以快速推进跑完整工序（建图 → T1～T6 × 2 区域 × 2 工位 × 全部铆钉 → 返回充电位），
    /// 自动注入三种故障各一次（区域 2 的 T1 识别失败、第 2 工位第 3 颗力超限、第 3 工位第 5 颗采集无效），结果写入 Docs/Reports/M5.md，
    /// CSV 复制到 Docs/Reports/M5_inspection.csv。截图时刻会暂停编辑器并把阶段写入 SessionState（RivetSim.M5Verify.Stage：shot1 / shot2），
    /// 由外部截取 Game 视图后恢复。
    /// </summary>
    [InitializeOnLoad]
    public static class MissionVerifyRunner
    {
        /// <summary>SessionState 键：当前阶段（running / shot1 / shot2 / 空）。</summary>
        public const string StageKey = "RivetSim.M5Verify.Stage";

        private const string ReportPath = "Docs/Reports/M5.md";
        private const string CsvCopyPath = "Docs/Reports/M5_inspection.csv";
        private const int CsvPreviewRows = 10;
        private const int FaultLogMaxLines = 40;
        private const int SettleFrames = 3;
        // 截图 1：第 1 工位第 6 颗（非故障颗）的 T5 力控接触开始时。
        private const int ShotStationGlobal = 0;
        private const int ShotRivet = 5;
        // 状态序列示例：第 1 工位第 2 颗。
        private const int TraceStationGlobal = 0;
        private const int TraceRivet = 1;

        private static readonly List<SimLogEntry> Logs = new List<SimLogEntry>();
        private static RobotRuntime _runtime;
        private static MissionRoutine _mission;
        private static double _realStart;
        private static int _pauseCountdown = -1;
        private static string _pauseStage = "";
        private static IDisposable _shotSubscription;

        static MissionVerifyRunner()
        {
            if (string.IsNullOrEmpty(SessionState.GetString(StageKey, ""))) return;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (EditorApplication.isPlayingOrWillChangePlaymode) Configure();
        }

        /// <summary>启动快速验证。</summary>
        [MenuItem("RivetSim/Verify/Run Mission (Fast)", priority = 100)]
        public static void Run()
        {
            if (EditorApplication.isPlaying) return;
            if (!MainSceneBuilder.EnsureNoUnsavedScenes()) return;
            if (EditorSceneManager.GetActiveScene().path != ProjectPaths.MainScene) EditorSceneManager.OpenScene(ProjectPaths.MainScene, OpenSceneMode.Single);
            SessionState.SetString(StageKey, "running");
            SimLog.Info(LogCategory.System, "M5 快速验证：进入 Play 模式");
            EditorApplication.EnterPlaymode();
        }

        /// <summary>截图完成后继续（外部调用）。</summary>
        public static void Resume()
        {
            SessionState.SetString(StageKey, "running");
            EditorApplication.isPaused = false;
        }

        /// <summary>结束验证并退出 Play 模式（外部调用）。</summary>
        public static void Finish()
        {
            SessionState.EraseString(StageKey);
            EditorApplication.isPaused = false;
            EditorApplication.ExitPlaymode();
        }

        private static void Configure()
        {
            Application.runInBackground = true;
            MissionLaunchOptions.Clear();
            MissionLaunchOptions.FastForward = true;
            MissionLaunchOptions.Faults.Add(new FaultInjectionPoint(FaultKind.RecognitionFailed, "T1", 1, -1, -1));
            MissionLaunchOptions.Faults.Add(new FaultInjectionPoint(FaultKind.ContactForceExceeded, "T5", -1, 1, 2));
            MissionLaunchOptions.Faults.Add(new FaultInjectionPoint(FaultKind.AcquisitionInvalid, "T5", -1, 2, 4));
            MissionLaunchOptions.Started = OnStarted;
            MissionLaunchOptions.Completed = OnCompleted;
            Logs.Clear();
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;
            SimLog.EntryLogged -= OnLog;
            EditorApplication.update -= OnUpdate;
            _shotSubscription?.Dispose();
            _shotSubscription = null;
            MissionLaunchOptions.Clear();
            SessionState.EraseString(StageKey);
            _runtime = null;
            _mission = null;
        }

        private static void OnLog(SimLogEntry e) => Logs.Add(e);

        private static void OnStarted(RobotRuntime runtime, MissionRoutine mission)
        {
            _runtime = runtime;
            _mission = mission;
            _realStart = EditorApplication.timeSinceStartup;
            // SimLog 在进入 Play 时（SubsystemRegistration）清空订阅，因此在流程开始时订阅。
            SimLog.EntryLogged -= OnLog;
            SimLog.EntryLogged += OnLog;
            _shotSubscription = runtime.Bus.Subscribe<CellStateChangedEvent>(OnCellChanged);
        }

        // 截图 1：目标颗进入 T4 时摆好机位，T5 力控接触开始后暂停。
        private static void OnCellChanged(CellStateChangedEvent e)
        {
            MissionCursor c = _mission.Orchestrator.Cursor;
            if (c.StationGlobalIndex != ShotStationGlobal || c.RivetIndex != ShotRivet) return;
            if (e.CellId == "t(4,3)" && e.State == CellState.Running) PlaceCloseCamera();
            if (e.CellId == "t(5,3)" && e.State == CellState.Running && _pauseCountdown < 0 && string.IsNullOrEmpty(_pauseStage))
            {
                _pauseStage = "shot1";
                _pauseCountdown = SettleFrames;
            }
        }

        private static void OnUpdate()
        {
            if (!EditorApplication.isPlaying || _pauseCountdown < 0) return;
            if (_pauseCountdown-- > 0) return;
            SessionState.SetString(StageKey, _pauseStage);
            SimLog.Info(LogCategory.System, $"M5 快速验证：暂停等待截图（{_pauseStage}）");
            _pauseStage = _pauseStage == "shot1" ? "done1" : "done2";
            EditorApplication.isPaused = true;
        }

        private static void OnCompleted(MissionRunReport report)
        {
            double real = EditorApplication.timeSinceStartup - _realStart;
            try
            {
                if (!string.IsNullOrEmpty(report.CsvPath) && File.Exists(report.CsvPath)) File.Copy(report.CsvPath, CsvCopyPath, true);
                File.WriteAllText(ReportPath, BuildReport(report, real), new UTF8Encoding(false));
                SimLog.Info(LogCategory.System, $"M5 快速验证：报告已写入 {ReportPath}");
            }
            catch (Exception ex)
            {
                SimLog.Error(LogCategory.System, $"M5 报告写入失败：{ex}");
            }
            PlaceOverviewCamera(report);
            _pauseStage = "shot2";
            _pauseCountdown = SettleFrames;
        }

        private static void PlaceCloseCamera()
        {
            Camera cam = Camera.main;
            if (cam == null || _runtime == null) return;
            Rivet rivet = RivetAt(ShotStationGlobal, ShotRivet);
            if (rivet == null) return;
            Vector3 n = rivet.Normal.normalized;
            Vector3 axis = Vector3.Cross(Vector3.up, n).normalized;
            Vector3 agv = _runtime.Chassis.transform.position + Vector3.up * 1.2f;
            Vector3 look = Vector3.Lerp(rivet.PositionM, agv, 0.45f);
            cam.transform.position = look + axis * 2.6f + n * 1.2f + Vector3.up * 0.5f;
            cam.transform.LookAt(look);
        }

        // 截图 2：对准非“合格”结论最多的工位，斜看一排铆钉结果环。
        private static void PlaceOverviewCamera(MissionRunReport report)
        {
            Camera cam = Camera.main;
            if (cam == null || _mission == null) return;
            var stations = new List<InspectionStation>();
            foreach (InspectionRegion region in _mission.Registry.Regions) stations.AddRange(region.Stations);
            int best = 0, bestCount = -1;
            for (int i = 0; i < stations.Count; i++)
            {
                string id = stations[i].StationId;
                int count = report.Records.Count(x => x.StationId == id && x.Verdict != InspectionVerdict.Pass);
                if (count > bestCount) (best, bestCount) = (i, count);
            }
            Rivet first = RivetAt(best, 0), last = RivetAt(best, int.MaxValue);
            if (first == null) return;
            Vector3 center = (first.PositionM + last.PositionM) * 0.5f;
            Vector3 n = first.Normal.normalized;
            Vector3 along = (last.PositionM - first.PositionM).normalized;
            cam.transform.position = center + n * 0.55f - along * 0.2f + Vector3.up * 0.1f;
            cam.transform.LookAt(center);
        }

        private static Rivet RivetAt(int stationGlobal, int rivetIndex)
        {
            if (_mission == null) return null;
            var stations = new List<InspectionStation>();
            RivetRegistry registry = _mission.Registry;
            if (registry == null) return null;
            foreach (InspectionRegion region in registry.Regions) stations.AddRange(region.Stations);
            if (stationGlobal >= stations.Count || stations[stationGlobal].Rivets.Count == 0) return null;
            IReadOnlyList<Rivet> rivets = stations[stationGlobal].Rivets;
            return rivets[Mathf.Min(rivetIndex, rivets.Count - 1)];
        }

        private static string BuildReport(MissionRunReport r, double realS)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# M5 报告：SOP 流程编排快速验证");
            sb.AppendLine();
            sb.AppendLine($"生成：{DateTime.Now:yyyy-MM-dd HH:mm:ss}（RivetSim/Verify/Run Mission (Fast)）；结果：**{(r.Success ? "完成" : $"未完成——{r.FailureReason}")}**");
            sb.AppendLine();
            sb.AppendLine("## 1. 用时");
            double fastSpeed = realS > 0 ? r.TotalS / realS : 0;
            sb.AppendLine($"- 仿真总用时 {r.TotalS:0.0} s（建图 {r.MappingS:0.0} s + 矩阵流程 {r.MatrixS:0.0} s + 返回充电位 {r.ReturnS:0.0} s）");
            sb.AppendLine($"- 换算 4 倍速实际用时：{r.TotalS / 4.0:0.0} s（{r.TotalS / 240.0:0.0} min）");
            sb.AppendLine($"- 本次快速推进：真实用时 {realS:0.0} s，等效 {fastSpeed:0.0} 倍速（每帧推进 {FastStepS():0.00} s 仿真时间，固定子步不变）");
            sb.AppendLine($"- 阶段执行 {r.Result.PhaseRuns} 次（含重试），未恢复铆钉 {r.Result.UnrecoveredRivets} 颗");
            sb.AppendLine();

            IReadOnlyList<InspectionRecord> records = r.Records;
            int defects = records.Count(x => x.Truth != DefectType.None);
            sb.AppendLine("## 2. 检出与误报");
            sb.AppendLine($"- 铆钉 {records.Count} 颗；真值缺陷 {defects} 颗");
            sb.AppendLine($"- 检出 {records.Count(x => x.IsDetection)}、漏检 {records.Count(x => x.IsMiss)}、误报 {records.Count(x => x.IsFalseAlarm)}、无效 {records.Count(x => x.Verdict == InspectionVerdict.Invalid)}");
            sb.AppendLine($"- 结论分布：" + string.Join("、", records.GroupBy(x => x.Verdict).OrderBy(g => g.Key).Select(g => $"{RivetInspectionService.Label(g.Key)} {g.Count()}")));
            sb.AppendLine();

            sb.AppendLine("## 3. 故障恢复日志");
            AppendFaultLog(sb, "识别失败（区域 2 的 T1）", "[故障注入] 识别失败", "进入阶段 精确停靠");
            AppendFaultLog(sb, "力超限（第 2 工位第 3 颗）", "[故障注入] 力超限", "进入阶段 释放退出");
            AppendFaultLog(sb, "采集无效（第 3 工位第 5 颗）", "[故障注入] 采集无效", "进入阶段 释放退出");

            sb.AppendLine("## 4. CSV 前 10 行");
            sb.AppendLine($"文件：`{r.CsvPath}`（副本 `{CsvCopyPath}`）");
            sb.AppendLine("```csv");
            if (File.Exists(CsvCopyPath))
                foreach (string line in File.ReadLines(CsvCopyPath).Take(CsvPreviewRows + 1)) sb.AppendLine(line.TrimStart('﻿'));
            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine("## 5. 执行器键名缺失检查");
            sb.AppendLine($"- JSON 执行器键名 {r.ExecutorKeyCount} 个；启动核对问题 {r.ValidationProblems.Count} 项{(r.ValidationProblems.Count == 0 ? "（缺失 0）" : "：")}");
            foreach (string p in r.ValidationProblems) sb.AppendLine($"  - {p}");
            sb.AppendLine();

            sb.AppendLine("## 6. 条件判定");
            sb.AppendLine($"- 共 {r.ConditionChecks} 次，未满足 {r.ConditionFailures} 次；按条件：");
            if (r.Trace != null)
                foreach (IGrouping<string, ConditionCheckedEvent> g in r.Trace.ConditionChecks.GroupBy(x => x.ConditionId).OrderBy(g => int.Parse(g.Key.Substring(1))))
                    sb.AppendLine($"  - {g.Key}：{g.Count(x => x.Passed)}/{g.Count()} ✓{(g.Any(x => !x.Passed) ? $"；✗ 示例：{g.First(x => !x.Passed).ValueText}" : "")}");
            sb.AppendLine();

            sb.AppendLine($"## 7. 单元状态序列（第 {TraceStationGlobal + 1} 工位第 {TraceRivet + 1} 颗，T4～T6）");
            sb.AppendLine("| 仿真时间 s | 单元 | 状态 | 说明 |");
            sb.AppendLine("|---|---|---|---|");
            if (r.Trace != null)
            {
                int stationRegion = 0, stationIndex = TraceStationGlobal;
                foreach (CellStateChangedEvent e in r.Trace.CellChanges)
                {
                    if (e.RegionIndex != stationRegion || e.StationIndex != stationIndex || e.RivetIndex != TraceRivet) continue;
                    if (e.PhaseId != "p4" && e.PhaseId != "p5" && e.PhaseId != "p6") continue;
                    if (e.State == CellState.Idle || e.State == CellState.Standby) continue;
                    sb.AppendLine($"| {e.TimeS:0.00} | {e.CellId} | {e.Previous} → {e.State} | {e.Note.Replace("|", "/")} |");
                }
            }
            sb.AppendLine();
            int errors = Logs.Count(x => x.Level == SimLogLevel.Error);
            int warnings = Logs.Count(x => x.Level == SimLogLevel.Warning);
            sb.AppendLine("## 8. 日志统计");
            sb.AppendLine($"- SimLog 共 {Logs.Count} 条：错误 {errors}、警告 {warnings}（警告含故障注入、条件 ✗ 与单元失败的预期日志）");
            foreach (SimLogEntry e in Logs.Where(x => x.Level == SimLogLevel.Error).Take(10)) sb.AppendLine($"  - [{e.SimTimeS:0.0}] {e.Message}");
            return sb.ToString();
        }

        private static float FastStepS()
        {
            var settings = AssetDatabase.LoadAssetAtPath<SimSettings>(ProjectPaths.SimSettingsAsset);
            return settings != null ? settings.fastVerifySimSecondsPerFrame : 0f;
        }

        private static void AppendFaultLog(StringBuilder sb, string title, string startMarker, string endMarker)
        {
            sb.AppendLine($"### {title}");
            sb.AppendLine("```text");
            int start = Logs.FindIndex(x => x.Message.StartsWith(startMarker, StringComparison.Ordinal));
            if (start < 0) sb.AppendLine("（未找到注入日志）");
            else
            {
                int lines = 0;
                for (int i = start; i < Logs.Count && lines < FaultLogMaxLines; i++)
                {
                    SimLogEntry e = Logs[i];
                    if (e.Message.StartsWith("[条件]", StringComparison.Ordinal)) continue;
                    sb.AppendLine($"[{e.SimTimeS,8:0.00}] {e.Message}");
                    lines++;
                    if (i > start && e.Message.Contains(endMarker)) break;
                }
            }
            sb.AppendLine("```");
            sb.AppendLine();
        }
    }
}
