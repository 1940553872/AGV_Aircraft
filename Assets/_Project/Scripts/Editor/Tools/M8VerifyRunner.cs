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
using RivetSim.Presentation.CameraControl;
using RivetSim.Presentation.UI;
using RivetSim.Simulation.Inspection;
using RivetSim.Simulation.Mission;
using RivetSim.Simulation.Robot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace RivetSim.Editor.Tools
{
    /// <summary>
    /// M8 验证（RivetSim/Verify/Run M8）：主场景快进跑完整流程，随机故障开启（不给固定注入点）。
    /// 开始时检查两种屏幕视图（任务矩阵 / 检测结果与设备状态）、说明卡与 HUD 在 1920×1080 下的布局；随后截总览图、按真实 ×8 倍速运行一个窗口
    /// （帧率、每帧子步数、倍速保护）；全程逐帧测量 AGV 整机（车体 + 机械臂，探头前端除外）到机身的最小三维距离，统计检测机位遮挡检查；
    /// 三类故障是否各发生一次并自动恢复；途中截特写、演示台全景与演示屏（小地图）。结果写入 Docs/Reports/M8.md，截图写入 Docs/Screenshots/M8（≤ 4 张，1280×720）。
    /// </summary>
    [InitializeOnLoad]
    public static class M8VerifyRunner
    {
        private const string StageKey = "RivetSim.M8Verify.Stage";
        private const string ResolutionKey = "RivetSim.M8Verify.Resolution";
        // 右屏状态视图自检图（不计入验收截图，写在 Unity 的 Temp 目录）。
        private const string StatusCheckPath = "Temp/M8_StatusCheck.png";
        private const string ReportPath = "Docs/Reports/M8.md";
        private const string ShotFolder = ProjectPaths.ScreenshotsFolder + "/M8";
        private const string ManualMarker = "<!-- 以下为人工整理，验证工具重写报告时保留 -->";
        private const int LayoutSettleFrames = 4;
        private const double OverviewSettleS = 2.5;
        private const double SpeedWindowS = 20.0;
        private const double ShotSettleS = 2.5;
        private const float ClearanceSearchM = 1.0f;
        private const float FastSpeed = 8f;
        private static readonly Vector2Int LayoutSize = new Vector2Int(1920, 1080);
        private static readonly Vector2Int ShotSizePx = new Vector2Int(1280, 720);

        private sealed class Anomaly
        {
            public FaultKind Kind;
            public string Reason;
            public double SimS;
            public int Station, Rivet, Region;
            public string Alert;
            public bool Recovered;
            public double RecoveredSimS = double.NaN;
        }

        private static string Stage => SessionState.GetString(StageKey, "");

        private static RobotRuntime _runtime;
        private static MissionRoutine _mission;
        private static AppBootstrap _app;
        private static float _ffSimPerFrame;
        private static string _resolutionInfo = "";
        private static double _realStart, _t0Sim;
        private static int _step, _wait, _layoutStep;
        private static double _stepUntil;
        private static readonly List<string> LayoutRows = new List<string>();
        private static int _layoutIssues;
        private static readonly List<Anomaly> Anomalies = new List<Anomaly>();
        private static readonly List<IDisposable> Subscriptions = new List<IDisposable>();
        private static readonly List<string> Notes = new List<string>();
        private static readonly List<string> Shots = new List<string>();

        // ×8 窗口。
        private static int _fastFrames, _fastMaxSubsteps;
        private static double _fastRealS, _fastSimS, _fastMaxDt;
        private static float _fastGuardFps;
        private static int _fastGuardWindows, _fastGuardTriggers;
        private static string _fallbackCheck = "未检查";

        // 间隙。
        private static VehicleClearanceMeter _vehicleMeter;
        private static float _minVehicleM = float.MaxValue, _minArmM = float.MaxValue, _minEeM = float.MaxValue;
        private static string _minVehiclePart = "—", _minArmLink = "—";
        private static double _minVehicleAtS, _minArmAtS;
        private static int _clearanceSamples, _penetrations;

        // 机位。
        private static int _rivetViewFrames, _rivetViewBlockedFrames;

        // 截图时机。
        private static bool _closeupDone, _deskDone;
        private static int _nonPass;
        private static string _bigScreenCheck = "未检查";
        private static string _minimapCheck = "未检查";

        static M8VerifyRunner()
        {
            if (string.IsNullOrEmpty(Stage)) return;
            Hook();
            if (EditorApplication.isPlayingOrWillChangePlaymode && Stage == "run") ConfigureLaunch();
        }

        /// <summary>启动 M8 验证。</summary>
        [MenuItem("RivetSim/Verify/Run M8", priority = 103)]
        public static void Run()
        {
            if (EditorApplication.isPlaying) return;
            if (!MainSceneBuilder.EnsureNoUnsavedScenes()) return;
            EditorSceneManager.OpenScene(ProjectPaths.MainScene, OpenSceneMode.Single);
            GameViewResolution.TrySet(LayoutSize.x, LayoutSize.y, out _resolutionInfo);
            SessionState.SetString(ResolutionKey, _resolutionInfo);
            SessionState.SetString(StageKey, "run");
            Hook();
            ConfigureLaunch();
            SimLog.Info(LogCategory.System, $"M8 验证：主场景快进运行（随机故障开启），{_resolutionInfo}");
            EditorApplication.EnterPlaymode();
        }

        /// <summary>中止验证。</summary>
        public static void Abort()
        {
            SessionState.EraseString(StageKey);
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        }

        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        private static void ConfigureLaunch()
        {
            MissionLaunchOptions.Clear();
            MissionLaunchOptions.FastForward = true;
            MissionLaunchOptions.Started = OnStarted;
            MissionLaunchOptions.Completed = OnCompleted;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                Application.runInBackground = true;
                return;
            }
            if (change != PlayModeStateChange.EnteredEditMode) return;
            MissionLaunchOptions.Clear();
            foreach (IDisposable s in Subscriptions) s.Dispose();
            Subscriptions.Clear();
            SessionState.EraseString(StageKey);
            EditorApplication.update -= OnUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        // ======================= 开始 =======================

        private static void OnStarted(RobotRuntime runtime, MissionRoutine mission)
        {
            _runtime = runtime;
            _mission = mission;
            _app = AppBootstrap.Active;
            _realStart = EditorApplication.timeSinceStartup;
            _t0Sim = runtime.Clock.TimeS;
            _resolutionInfo = SessionState.GetString(ResolutionKey, "");
            var settings = AssetDatabase.LoadAssetAtPath<SimSettings>(ProjectPaths.SimSettingsAsset);
            _ffSimPerFrame = settings != null ? settings.fastVerifySimSecondsPerFrame : 0.25f;
            _step = _wait = _layoutStep = 0;
            LayoutRows.Clear();
            _layoutIssues = 0;
            Anomalies.Clear();
            Notes.Clear();
            Shots.Clear();
            _fastFrames = _fastMaxSubsteps = _fastGuardWindows = _fastGuardTriggers = 0;
            _fastRealS = _fastSimS = _fastMaxDt = 0d;
            _fastGuardFps = 0f;
            _minVehicleM = _minArmM = _minEeM = float.MaxValue;
            _minVehiclePart = _minArmLink = "—";
            _clearanceSamples = _penetrations = 0;
            _rivetViewFrames = _rivetViewBlockedFrames = 0;
            _closeupDone = _deskDone = false;
            _nonPass = 0;
            _bigScreenCheck = _minimapCheck = _fallbackCheck = "未检查";
            _vehicleMeter = new VehicleClearanceMeter(runtime.Chassis.transform, runtime.Arm.transform, 1 << SimLayers.AircraftIndex, ClearanceSearchM);
            // 布局检查与总览截图期间先不快进。
            runtime.FastForwardSimSecondsPerFrame = 0f;

            foreach (IDisposable s in Subscriptions) s.Dispose();
            Subscriptions.Clear();
            Subscriptions.Add(runtime.Bus.Subscribe<PhaseEnteredEvent>(OnPhase));
            Subscriptions.Add(runtime.Bus.Subscribe<CellStateChangedEvent>(OnCell));
            Subscriptions.Add(runtime.Bus.Subscribe<RivetInspectedEvent>(OnInspected));
            mission.Conditions.AnomalyRaised += OnAnomaly;
            Directory.CreateDirectory(ShotFolder);
            SimLog.Info(LogCategory.System, $"M8 验证：开始；随机故障计划 {string.Join("；", _app.RandomFaultPlan)}");
        }

        private static double Rel(double simS) => simS - _t0Sim;

        private static void OnAnomaly(FaultKind kind, string reason)
        {
            MissionCursor c = _mission.Orchestrator.Cursor;
            Anomalies.Add(new Anomaly
            {
                Kind = kind, Reason = reason, SimS = Rel(_runtime.Clock.TimeS), Region = c.RegionIndex, Station = c.StationGlobalIndex, Rivet = c.RivetIndex,
                Alert = _app.Hud != null ? _app.Hud.AlertText : "",
            });
        }

        private static void OnPhase(PhaseEnteredEvent e)
        {
            foreach (Anomaly a in Anomalies)
            {
                if (a.Recovered) continue;
                bool ok = a.Kind == FaultKind.RecognitionFailed
                    ? e.PhaseCode == "T2" && e.StationGlobalIndex >= a.Station
                    : e.PhaseCode == "T6" && e.StationGlobalIndex == a.Station && e.RivetIndex == a.Rivet;
                if (!ok) continue;
                a.Recovered = true;
                a.RecoveredSimS = Rel(_runtime.Clock.TimeS);
            }
        }

        private static void OnInspected(RivetInspectedEvent e)
        {
            if (e.Record != null && e.Record.Verdict != InspectionVerdict.Pass) _nonPass++;
        }

        private static void OnCell(CellStateChangedEvent e)
        {
            if (e.State != CellState.Running || _step < 3) return;
            MissionCursor c = _mission.Orchestrator.Cursor;
            // 特写：第 2 个工位第 2 颗起的 T5 超声采集。
            if (!_closeupDone && e.CellId == "t(5,4)" && c.StationGlobalIndex >= 1 && c.RivetIndex >= 1 && _step == 3)
            {
                _closeupDone = true;
                BeginShot(3, 4);
            }
            // 演示台全景：已有不合格记录后，下一个工位第 1 颗 T4 开始时。
            else if (!_deskDone && _closeupDone && e.CellId == "t(4,1)" && c.RivetIndex == 0 && _nonPass > 0 && _step == 3)
            {
                _deskDone = true;
                BeginShot(4, 5);
            }
        }

        private static void BeginShot(int preset, int step)
        {
            _runtime.FastForwardSimSecondsPerFrame = 0f;
            _app.Rig?.Select(preset);
            _step = step;
            _stepUntil = EditorApplication.timeSinceStartup + ShotSettleS;
        }

        private static void EndShot()
        {
            _app.Rig?.SetAuto(true);
            _runtime.FastForwardSimSecondsPerFrame = _ffSimPerFrame;
            _step = 3;
        }

        // ======================= 逐帧 =======================

        private static void OnUpdate()
        {
            if (Stage != "run" || !EditorApplication.isPlaying || EditorApplication.isPaused || _runtime == null || _app == null) return;
            if (_step >= 3) SampleFrame();
            if (_wait > 0)
            {
                _wait--;
                return;
            }
            double now = EditorApplication.timeSinceStartup;
            switch (_step)
            {
                case 0:
                    UpdateLayout();
                    break;
                case 1:
                    if (now < _stepUntil) break;
                    CaptureCamera("M8_01_Overview.png");
                    // 真实 ×8 倍速窗口。
                    _app.Hub.Mode.SetSpeed(FastSpeed);
                    _step = 2;
                    _stepUntil = now + SpeedWindowS;
                    break;
                case 2:
                    SampleFast();
                    if (now < _stepUntil && _runtime.Clock.SpeedMultiplier >= FastSpeed - 0.01f) break;
                    FinishFast();
                    _app.Rig?.SetAuto(true);
                    _runtime.FastForwardSimSecondsPerFrame = _ffSimPerFrame;
                    _step = 3;
                    break;
                case 4:
                    if (now < _stepUntil) break;
                    CaptureCamera("M8_03_Closeup.png");
                    EndShot();
                    break;
                case 5:
                    if (now < _stepUntil) break;
                    CheckBigScreen();
                    CaptureCamera("M8_02_DemoStation.png");
                    CaptureConsole(DemoUiGenerator.RenderTexturePath, $"{ShotFolder}/M8_04_MiniMap.png");
                    Shots.Add("M8_04_MiniMap.png");
                    CaptureConsole(DemoUiGenerator.StatusRenderTexturePath, StatusCheckPath);
                    CheckMinimap();
                    EndShot();
                    break;
            }
        }

        // 布局：任务矩阵全屏、状态视图全屏、t(4,3) 说明卡、HUD、右屏状态演示屏（RenderTexture 面板）。
        private static void UpdateLayout()
        {
            DemoConsoleView console = _app.DemoConsole;
            DemoConsoleView status = _app.StatusConsole;
            string[] names = { "演示屏 · 任务矩阵（全屏）", "演示屏 · 检测结果与设备状态（全屏）", "演示屏 · 说明卡 t(4,3)", "HUD", "右屏 · 检测结果与设备状态（RenderTexture）" };
            int item = _layoutStep / 3;
            int phase = _layoutStep % 3;
            if (item >= names.Length)
            {
                _app.Hub.SetFullscreen(false);
                _app.Rig?.Select(1);
                _step = 1;
                _stepUntil = EditorApplication.timeSinceStartup + OverviewSettleS;
                return;
            }
            bool hud = item == 3;
            DemoConsoleView target = item == 4 ? status : console;
            switch (phase)
            {
                case 0:
                    _app.Hub.SetFullscreen(item <= 2);
                    if (item <= 2) console.SetStatusView(item == 1);
                    if (item == 2) console.ShowDetail("t(4,3)");
                    break;
                case 1:
                    UiLayoutCheck.Begin((hud ? _app.Hud.GetComponent<UIDocument>() : target.GetComponent<UIDocument>()).panelSettings, LayoutSize);
                    break;
                default:
                    VisualElement root = hud ? _app.Hud.Root : target.Root;
                    LayoutRows.Add(UiLayoutCheck.Check(root, LayoutSize, names[item], out int issues));
                    _layoutIssues += issues;
                    UiLayoutCheck.End();
                    if (item == 2) console.HideDetail();
                    break;
            }
            _layoutStep++;
            _wait = LayoutSettleFrames;
        }

        private static void SampleFast()
        {
            double dt = Time.unscaledDeltaTime;
            _fastFrames++;
            _fastRealS += dt;
            _fastSimS += _runtime.Clock.DeltaS;
            if (dt > _fastMaxDt) _fastMaxDt = dt;
            if (_runtime.LastFrameSubsteps > _fastMaxSubsteps) _fastMaxSubsteps = _runtime.LastFrameSubsteps;
        }

        private static void FinishFast()
        {
            SpeedGuard guard = _app.Guard;
            if (guard != null)
            {
                _fastGuardFps = guard.LastWindowFps;
                _fastGuardWindows = guard.WindowCount;
                _fastGuardTriggers = guard.TriggerCount;
            }
            var settings = AssetDatabase.LoadAssetAtPath<SimSettings>(ProjectPaths.SimSettingsAsset);
            CheckFallback(settings);
            _app.Hub.Mode.SetSpeed(settings != null ? settings.defaultSpeedMultiplier : 2f);
        }

        // 降速路径检查：用阈值极高的配置副本（不改资产）在 ×8 下统计一个窗口，确认降到回退倍速并在 HUD 提示。
        private static void CheckFallback(SimSettings settings)
        {
            if (settings == null) return;
            SimSettings probe = UnityEngine.Object.Instantiate(settings);
            probe.speedGuardMinFps = 1e6f;
            probe.speedGuardWindowS = 0.01f;
            var guard = new SpeedGuard(_runtime.Clock, probe);
            string text = null;
            guard.Triggered += t =>
            {
                text = t;
                _app.Hub.ShowSpeedGuardNotice(t);
            };
            float before = _runtime.Clock.SpeedMultiplier;
            guard.Tick(0.02f, _runtime.LastFrameSubsteps, false);
            float after = _runtime.Clock.SpeedMultiplier;
            string alert = _app.Hud != null ? _app.Hud.AlertText : "";
            bool ok = text != null && Mathf.Approximately(after, settings.speedGuardFallbackMultiplier) && alert == text;
            _fallbackCheck = $"{(ok ? "通过" : "未通过")}：×{before:0.##} → ×{after:0.##}，HUD 提示“{alert}”";
            UnityEngine.Object.DestroyImmediate(probe);
        }

        private static void SampleFrame()
        {
            // 整机到机身的最小距离：车体（底盘、升降柱、设备）与机械臂分别测量。
            float vehicle = _vehicleMeter.Measure(out string part);
            ClearanceResult arm = _runtime.Arm.Clearance.Measure();
            _clearanceSamples++;
            double t = Rel(_runtime.Clock.TimeS);
            if (vehicle < _minVehicleM)
            {
                _minVehicleM = vehicle;
                _minVehiclePart = part;
                _minVehicleAtS = t;
            }
            if (arm.MinLinkM < _minArmM)
            {
                _minArmM = arm.MinLinkM;
                _minArmLink = arm.NearestLink;
                _minArmAtS = t;
            }
            if (arm.MinEndEffectorM < _minEeM) _minEeM = arm.MinEndEffectorM;
            if (vehicle <= 0f || arm.MinLinkM <= 0f) _penetrations++;

            CameraRig rig = _app.Rig;
            if (rig != null && rig.IsRivetView)
            {
                _rivetViewFrames++;
                if (rig.IsViewBlocked) _rivetViewBlockedFrames++;
            }
        }

        private static void CheckBigScreen()
        {
            Renderer big = _app.BigScreenRenderer;
            if (big == null)
            {
                _bigScreenCheck = "未找到大屏渲染器";
                return;
            }
            Material before = big.sharedMaterial;
            _app.Hub.ToggleBigScreenView();
            Material status = big.sharedMaterial;
            _app.Hub.ToggleBigScreenView();
            Material after = big.sharedMaterial;
            bool ok = before != status && after == before && status != null && status.name.Contains("Status");
            _bigScreenCheck = $"{(ok ? "通过" : "未通过")}：{before?.name} → {status?.name} → {after?.name}";
        }

        private static void CheckMinimap()
        {
            VisualElement root = _app.DemoConsole.Root;
            VisualElement host = root?.Q("dc-minimap-host");
            MiniMapElement map = host?.Q<MiniMapElement>();
            if (map == null)
            {
                _minimapCheck = "未找到小地图元素";
                return;
            }
            VisualElement image = map.childCount > 0 ? map[0] : null;
            Texture tex = image != null ? image.resolvedStyle.backgroundImage.texture : null;
            _minimapCheck = $"小地图元素 {map.layout.width:0} × {map.layout.height:0} px，栅格贴图 {(tex != null ? tex.name + $" {tex.width}×{tex.height}" : "无")}";
        }

        // ======================= 截图 =======================

        private static void CaptureCamera(string fileName)
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            var rt = new RenderTexture(ShotSizePx.x, ShotSizePx.y, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previousTarget = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = previousTarget;
            Save(rt, $"{ShotFolder}/{fileName}");
            Shots.Add(fileName);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }

        private static void CaptureConsole(string texturePath, string filePath)
        {
            var source = AssetDatabase.LoadAssetAtPath<RenderTexture>(texturePath);
            if (source == null) return;
            var rt = new RenderTexture(ShotSizePx.x, ShotSizePx.y, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, rt);
            Save(rt, filePath);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }

        private static void Save(RenderTexture rt, string filePath)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var image = new Texture2D(ShotSizePx.x, ShotSizePx.y, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, ShotSizePx.x, ShotSizePx.y), 0, 0);
            image.Apply();
            // Graphics.Blit 会把 active 设为 rt：恢复时不能留在待释放的 rt 上。
            RenderTexture.active = previous == rt ? null : previous;
            File.WriteAllBytes(filePath, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
            SimLog.Info(LogCategory.System, $"M8 验证：截图 {filePath}");
        }

        // ======================= 报告 =======================

        private static void OnCompleted(MissionRunReport report)
        {
            _mission.Conditions.AnomalyRaised -= OnAnomaly;
            double real = EditorApplication.timeSinceStartup - _realStart;
            var sb = new StringBuilder();
            sb.AppendLine("# M8 验证报告（v1.1 小更新）");
            sb.AppendLine();
            sb.AppendLine($"生成：{DateTime.Now:yyyy-MM-dd HH:mm}；菜单 RivetSim/Verify/Run M8；{_resolutionInfo}；本次验证实际用时 {real:0} s（快进每帧 {_ffSimPerFrame:0.##} s 仿真）。");
            sb.AppendLine();
            sb.AppendLine("## 1 完整流程（快进，随机故障开启）");
            sb.AppendLine();
            if (report != null)
            {
                sb.AppendLine($"- 结果：{(report.Success ? "完成" : "未完成 " + report.FailureReason)}");
                sb.AppendLine($"- 仿真用时：建图 {report.MappingS:0.0} s，任务矩阵 {report.MatrixS:0.0} s，返回 {report.ReturnS:0.0} s，合计 {report.TotalS:0.0} s");
                int pass = report.Records.Count(r => r.Verdict == InspectionVerdict.Pass);
                int invalid = report.Records.Count(r => r.Verdict == InspectionVerdict.Invalid);
                sb.AppendLine($"- 检测铆钉 {report.Records.Count} 颗：合格 {pass}，不合格（疑似 + 缺陷）{report.Records.Count - pass - invalid}，无效 {invalid}；条件检查 {report.ConditionChecks} 次（✗ {report.ConditionFailures}）");
                InspectionStatusView sv = _app.DemoConsole.StatusView;
                InspectionStatusView rv = _app.StatusConsole != null ? _app.StatusConsole.StatusView : null;
                if (rv != null)
                    sb.AppendLine($"- 右屏状态视图统计：已检 {rv.InspectedCount}、不合格 {rv.NonPassCount}、无效 {rv.InvalidCount}，列表 {rv.ShownRowCount} 行——与任务报告{(rv.InspectedCount == report.Records.Count ? "一致" : "**不一致**")}");
                if (sv != null && rv != null && sv.InspectedCount != rv.InspectedCount) Notes.Add("大屏与右屏状态视图统计不一致");
            }
            sb.AppendLine();

            AppendFaults(sb);
            AppendSpeed(sb);
            AppendClearance(sb);
            AppendLayout(sb);
            AppendCamera(sb);
            sb.AppendLine("## 7 小地图与大屏视图切换");
            sb.AppendLine();
            sb.AppendLine($"- 小地图：{_minimapCheck}；未显示原因：M6 栅格“未知”色 #152232 与卡片底色相同、“空闲”色 #263A52 与分隔线同色，贴图几乎融入背景。M8 改为未知 #0B121B、空闲 #2C4663，并画出禁行区（暗红）、工位（青框）、充电位（绿菱形）与铆钉结果点。");
            sb.AppendLine($"- 大屏视图切换（B / HUD 按钮，替换大屏材质）：{_bigScreenCheck}");
            sb.AppendLine();
            sb.AppendLine("## 8 场景明度参数调整前后");
            sb.AppendLine();
            sb.AppendLine(M8ConfigMigration.ChangeTable());
            sb.AppendLine("## 9 截图（Docs/Screenshots/M8，1280×720）");
            sb.AppendLine();
            foreach (string s in Shots) sb.AppendLine($"- {s}：{ShotCaption(s)}");
            sb.AppendLine();
            if (Notes.Count > 0)
            {
                sb.AppendLine("## 备注");
                sb.AppendLine();
                foreach (string n in Notes) sb.AppendLine($"- {n}");
                sb.AppendLine();
            }
            WriteReport(sb.ToString());
            SimLog.Info(LogCategory.System, $"M8 验证：完成，报告 {ReportPath}");
            SessionState.EraseString(StageKey);
            EditorApplication.delayCall += () => { if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode(); };
        }

        private static string ShotCaption(string file) => file switch
        {
            "M8_01_Overview.png" => "总览机位（建图阶段，体现新墙面与屋顶内侧明度）",
            "M8_02_DemoStation.png" => "演示台全景（放大 1.4 倍的显示器、木纹桌面；左屏任务矩阵、右屏检测结果与设备状态，大屏任务矩阵）",
            "M8_03_Closeup.png" => "特写机位检测中（以铆钉法向为参照、机身外侧取景）",
            "M8_04_MiniMap.png" => "演示屏画面（执行说明区下方的小地图：栅格、禁行区、工位、充电位、路径、AGV、铆钉结果点）",
            _ => "",
        };

        private static void AppendFaults(StringBuilder sb)
        {
            sb.AppendLine("## 2 随机故障（每类每轮一次，种子决定位置）");
            sb.AppendLine();
            sb.AppendLine($"- 计划：{(_app.RandomFaultPlan.Count > 0 ? string.Join("；", _app.RandomFaultPlan) : "无")}");
            sb.AppendLine();
            sb.AppendLine("| 故障 | 发生（仿真 s） | 位置 | 原因 | 屏幕提示 | 自动恢复 |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (Anomaly a in Anomalies)
                sb.AppendLine($"| {ConditionBus.Describe(a.Kind)} | {a.SimS:0.0} | 区域 {a.Region + 1} · 第 {a.Station + 1} 工位 · 第 {a.Rivet + 1} 颗 | {a.Reason} | {(string.IsNullOrEmpty(a.Alert) ? "**无**" : a.Alert)} | " +
                              $"{(a.Recovered ? $"是（{a.RecoveredSimS:0.0} s 进入 {(a.Kind == FaultKind.RecognitionFailed ? "T2" : "T6")}）" : "**否**")} |");
            sb.AppendLine();
            foreach (FaultKind kind in new[] { FaultKind.RecognitionFailed, FaultKind.ContactForceExceeded, FaultKind.AcquisitionInvalid })
            {
                int n = Anomalies.Count(a => a.Kind == kind);
                bool recovered = Anomalies.Where(a => a.Kind == kind).All(a => a.Recovered);
                sb.AppendLine($"- {ConditionBus.Describe(kind)}：发生 {n} 次，{(n > 0 && recovered ? "全部自动恢复" : n == 0 ? "**未发生**" : "**有未恢复**")}" +
                              (kind == FaultKind.AcquisitionInvalid && n > 1 ? "（含仿真中自然出现的采集无效）" : ""));
            }
            sb.AppendLine();
        }

        private static void AppendSpeed(StringBuilder sb)
        {
            sb.AppendLine("## 3 ×8 倍速稳定性");
            sb.AppendLine();
            double fps = _fastRealS > 0 ? _fastFrames / _fastRealS : 0;
            double rate = _fastRealS > 0 ? _fastSimS / _fastRealS : 0;
            sb.AppendLine($"- 真实 ×8 窗口 {_fastRealS:0.0} s（建图阶段）：{_fastFrames} 帧，平均 {fps:0} FPS，最长帧 {_fastMaxDt * 1000:0} ms；每帧子步最多 {_fastMaxSubsteps} 个（子步长 {_runtime.StepS * 1000:0.#} ms）；仿真推进 {_fastSimS:0.0} s（{rate:0.00} 倍于真实时间）");
            sb.AppendLine($"- 倍速保护：统计窗口 {_fastGuardWindows} 个，最近窗口 {_fastGuardFps:0} FPS，触发降速 {_fastGuardTriggers} 次（阈值见 CFG_SimSettings.speedGuardMinFps）");
            sb.AppendLine($"- 降速路径检查（配置副本把帧率阈值调到极高，强制触发）：{_fallbackCheck}");
            sb.AppendLine("- 结论依据：仿真按固定子步推进（RobotRuntime），倍速只改变每帧子步数，单步动力学与 ×1 完全相同；×8 的风险只在帧率，由倍速保护兜底（帧率不足时自动降到 ×4 并提示）。");
            sb.AppendLine();
        }

        private static void AppendClearance(StringBuilder sb)
        {
            sb.AppendLine("## 4 AGV 整机到机身的最小三维距离（全程逐帧，含升降柱与机械臂）");
            sb.AppendLine();
            sb.AppendLine($"- 采样 {_clearanceSamples} 帧；穿模（距离 ≤ 0）{_penetrations} 帧");
            sb.AppendLine($"- 车体（底盘、升降柱、设备盒，包围盒表面 26 点 × {_vehicleMeter.PartCount} 个零件，偏保守）：最小 {Fmt(_minVehicleM)}（{_minVehiclePart}，仿真 {_minVehicleAtS:0.0} s）");
            sb.AppendLine($"- 机械臂连杆（ArmClearance 表面采样点）：最小 {Fmt(_minArmM)}（{_minArmLink}，仿真 {_minArmAtS:0.0} s）；探头末端（排除探头前端接触区）最小 {Fmt(_minEeM)}");
            float whole = Mathf.Min(_minVehicleM, _minArmM);
            sb.AppendLine($"- **整机最小距离 {Fmt(whole)}**，{(whole > 0f && _penetrations == 0 ? "无穿模" : "**存在穿模**")}（搜索上限 {ClearanceSearchM:0.0} m）");
            sb.AppendLine();
        }

        private static string Fmt(float m) => m >= ClearanceSearchM ? $"≥ {ClearanceSearchM:0.0} m" : $"{m * 1000f:0} mm";

        private static void AppendLayout(StringBuilder sb)
        {
            sb.AppendLine("## 5 屏幕布局检查（1920×1080）");
            sb.AppendLine();
            sb.AppendLine(UiLayoutCheck.TableHeader);
            foreach (string row in LayoutRows) sb.AppendLine(row);
            sb.AppendLine();
            sb.AppendLine($"- 问题合计 {_layoutIssues} 个");
            sb.AppendLine();
        }

        private static void AppendCamera(StringBuilder sb)
        {
            CameraRig rig = _app.Rig;
            sb.AppendLine("## 6 检测机位防遮挡");
            sb.AppendLine();
            if (rig == null)
            {
                sb.AppendLine("- 未找到 CameraRig");
                sb.AppendLine();
                return;
            }
            sb.AppendLine($"- 按铆钉法向取景的帧 {_rivetViewFrames}，其中所有候选均被挡 {_rivetViewBlockedFrames} 帧（{(_rivetViewFrames > 0 ? 100.0 * _rivetViewBlockedFrames / _rivetViewFrames : 0):0.0}%）");
            sb.AppendLine($"- 遮挡检查 {rig.OcclusionCheckCount} 次（每 0.2 s 实际时间），当前角度被挡 {rig.OcclusionBlockedCount} 次，切换候选角度 {rig.OcclusionSwitchCount} 次；结束时偏航 {rig.CurrentYawDeg:0}°");
            sb.AppendLine();
        }

        private static void WriteReport(string body)
        {
            string manual = "";
            if (File.Exists(ReportPath))
            {
                string old = File.ReadAllText(ReportPath);
                int i = old.IndexOf(ManualMarker, StringComparison.Ordinal);
                if (i >= 0) manual = old.Substring(i);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? ".");
            File.WriteAllText(ReportPath, body + (manual.Length > 0 ? "\n" + manual : ""));
        }
    }
}
