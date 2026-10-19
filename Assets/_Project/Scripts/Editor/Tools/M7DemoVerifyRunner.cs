using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RivetSim.Core.Config;
using RivetSim.Core.Diagnostics;
using RivetSim.Core.Logging;
using RivetSim.Core.Mission;
using RivetSim.Editor.Generators;
using RivetSim.Presentation.App;
using RivetSim.Presentation.CameraControl;
using RivetSim.Presentation.UI;
using RivetSim.Simulation.Mission;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace RivetSim.Editor.Tools
{
    /// <summary>
    /// M7 联调验证（RivetSim/Verify/Run M7 Demo）：
    /// ① 主场景按默认倍速（2 倍）自动演示完整流程：开始时检查演示屏与 HUD 在 1920×1080、2560×1440 下无溢出；
    ///    四个观察窗口（总览建图、跟随导航、末端特写、演示台）按真实 2 倍速运行并统计帧率与每帧 GC，同时截取宣传截图；
    ///    其余时段快进（相机不切换），时间线按仿真时间换算为真实 2 倍速时长。激光、栅格、小地图用 GcProbe 统计分配。
    /// ② 主场景分步演示完整流程（快进）：每次进入新阶段暂停，自动按“下一步”继续；含一次 t(1,8) 识别失败，确认模拟操作员给出初值。
    /// 结果写入 Docs/Reports/M7.md（保留报告末尾人工整理部分），截图写入 Docs/Screenshots/M7。
    /// </summary>
    [InitializeOnLoad]
    public static class M7DemoVerifyRunner
    {
        private const string StageKey = "RivetSim.M7Verify.Stage";
        private const string AutoSectionKey = "RivetSim.M7Verify.AutoSection";
        private const string ResolutionKey = "RivetSim.M7Verify.Resolution";
        private const string ReportPath = "Docs/Reports/M7.md";
        private const string ShotFolder = ProjectPaths.ScreenshotsFolder + "/M7";
        private const string ManualMarker = "<!-- 以下为人工整理，验证工具重写报告时保留 -->";
        private const int LayoutSettleFrames = 4;
        private const int WarmupFrames = 30;
        private const int StepNextDelayFrames = 3;
        private const double WindowMaxRealS = 22.0;
        private const double WindowAfterShotS = 10.0;
        private static readonly Vector2Int PerfResolution = new Vector2Int(1920, 1080);
        private static readonly Vector2Int ShotSizePx = new Vector2Int(1280, 720);
        private static readonly Vector2Int[] LayoutResolutions = { new Vector2Int(1920, 1080), new Vector2Int(2560, 1440) };

        // —— 观察窗口 ——
        private sealed class Window
        {
            public string Name;
            public int Preset;
            public string Shot;
            public double StartReal;
            public double ShotAt = double.NaN;
            public bool ShotDone;
            public double ShotSimS;
            public double StartSimS;
            public int Frames;
            public double SumDt;
            public double MaxDt;
            public double SumMainMs;
            public long SumGcBytes;
            public long SumGcCount;
            public int FramesWithGc;
            public readonly List<double> Dts = new List<double>();
            public Vector2Int Screen;
        }

        private sealed class StationTimes
        {
            public int Region, Station;
            public double T1 = double.NaN, T2 = double.NaN, T3 = double.NaN, FirstT4 = double.NaN, LastT6 = double.NaN;
            public int T3Entries, Rivets;
        }

        private static string Stage => SessionState.GetString(StageKey, "");

        private static RobotRuntime _runtime;
        private static MissionRoutine _mission;
        private static double _t0Sim, _realStart;
        private static float _ffSimPerFrame;
        private static string _resolutionInfo = "";
        private static int _wait;
        private static int _layoutIndex, _layoutStep;
        private static readonly List<string> LayoutRows = new List<string>();
        private static int _layoutIssues;
        private static Window _window;
        private static readonly List<Window> Windows = new List<Window>();
        private static bool _overviewDone, _navDone, _closeupDone, _deskDone;
        private static int _lastFrame = -1;
        private static ProfilerRecorder _gcBytes, _gcCount, _mainThread;
        private static readonly SortedDictionary<int, StationTimes> Stations = new SortedDictionary<int, StationTimes>();
        private static int _nextLoops, _changeLoops, _triggered, _phaseEntries;
        private static readonly List<string> Notes = new List<string>();

        // 分步演示
        private static int _stepPauses, _stepPauseLeaks, _stepPhaseEntries, _pauseFrames;
        private static bool _pauseSeen, _stepPoseProvided, _stepPoseOpened;
        private static double _pauseSim;

        static M7DemoVerifyRunner()
        {
            if (string.IsNullOrEmpty(Stage)) return;
            Hook();
            if (EditorApplication.isPlayingOrWillChangePlaymode && (Stage == "auto" || Stage == "step")) ConfigureLaunch(Stage == "step");
            if (!EditorApplication.isPlayingOrWillChangePlaymode && Stage == "to_step") EditorApplication.delayCall += LaunchStep;
        }

        /// <summary>启动 M7 验证（先自动演示，再分步演示）。</summary>
        [MenuItem("RivetSim/Verify/Run M7 Demo", priority = 102)]
        public static void Run()
        {
            if (EditorApplication.isPlaying) return;
            if (!MainSceneBuilder.EnsureNoUnsavedScenes()) return;
            EditorSceneManager.OpenScene(ProjectPaths.MainScene, OpenSceneMode.Single);
            GameViewResolution.TrySet(PerfResolution.x, PerfResolution.y, out _resolutionInfo);
            SessionState.SetString(ResolutionKey, _resolutionInfo);
            SimLog.Info(LogCategory.System, $"M7 验证：{_resolutionInfo}");
            SessionState.SetString(StageKey, "auto");
            SessionState.EraseString(AutoSectionKey);
            Hook();
            ConfigureLaunch(false);
            SimLog.Info(LogCategory.System, "M7 验证：主场景自动演示进入 Play 模式");
            EditorApplication.EnterPlaymode();
        }

        /// <summary>中止验证。</summary>
        public static void Abort()
        {
            SessionState.EraseString(StageKey);
            GcProbe.Enabled = false;
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        }

        /// <summary>第二阶段：分步演示（也可在中断后手动调用）。</summary>
        public static void LaunchStep()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || (Stage != "to_step" && Stage != "step")) return;
            SessionState.SetString(StageKey, "step");
            EditorSceneManager.OpenScene(ProjectPaths.MainScene, OpenSceneMode.Single);
            ConfigureLaunch(true);
            SimLog.Info(LogCategory.System, "M7 验证：主场景分步演示进入 Play 模式");
            EditorApplication.EnterPlaymode();
        }

        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        private static void ConfigureLaunch(bool step)
        {
            MissionLaunchOptions.Clear();
            MissionLaunchOptions.FastForward = step;
            if (step)
            {
                MissionLaunchOptions.Faults.Add(new FaultInjectionPoint(FaultKind.RecognitionFailed, "T1", 1, -1, -1));
                MissionLaunchOptions.Started = OnStepStarted;
                MissionLaunchOptions.Completed = OnStepCompleted;
            }
            else
            {
                MissionLaunchOptions.Started = OnAutoStarted;
                MissionLaunchOptions.Completed = OnAutoCompleted;
            }
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
            GcProbe.Enabled = false;
            DisposeRecorders();
            if (Stage == "to_step")
            {
                EditorApplication.delayCall += LaunchStep;
                return;
            }
            SessionState.EraseString(StageKey);
            EditorApplication.update -= OnUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private static void OnUpdate()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused || _runtime == null || AppBootstrap.Active == null) return;
            if (Stage == "auto") UpdateAuto();
            else if (Stage == "step") UpdateStep();
        }

        // ======================= ① 自动演示 =======================

        private static void OnAutoStarted(RobotRuntime runtime, MissionRoutine mission)
        {
            _runtime = runtime;
            _mission = mission;
            _t0Sim = runtime.Clock.TimeS;
            _realStart = EditorApplication.timeSinceStartup;
            _resolutionInfo = SessionState.GetString(ResolutionKey, "");
            var settings = AssetDatabase.LoadAssetAtPath<SimSettings>(ProjectPaths.SimSettingsAsset);
            _ffSimPerFrame = settings != null ? settings.fastVerifySimSecondsPerFrame : 0.25f;
            _layoutIndex = _layoutStep = 0;
            _layoutIssues = 0;
            _wait = 0;
            LayoutRows.Clear();
            Windows.Clear();
            _window = null;
            _overviewDone = _navDone = _closeupDone = _deskDone = false;
            Stations.Clear();
            Notes.Clear();
            _nextLoops = _changeLoops = _triggered = _phaseEntries = 0;
            _lastFrame = -1;
            GcProbe.Reset();
            GcProbe.Enabled = true;
            UiPerfProbe.Reset();
            DisposeRecorders();
            _gcBytes = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _gcCount = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocation In Frame Count");
            _mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
            runtime.Bus.Subscribe<PhaseEnteredEvent>(OnPhase);
            runtime.Bus.Subscribe<LoopTakenEvent>(OnLoop);
            runtime.Bus.Subscribe<CellStateChangedEvent>(OnCell);
            Directory.CreateDirectory(ShotFolder);
            SimLog.Info(LogCategory.System, $"M7 验证：自动演示开始，倍速 {runtime.Clock.SpeedMultiplier:0.#} 倍");
        }

        private static double Rel(double simS) => simS - _t0Sim;

        private static void OnPhase(PhaseEnteredEvent e)
        {
            _phaseEntries++;
            double t = Rel(_runtime.Clock.TimeS);
            if (!Stations.TryGetValue(e.StationGlobalIndex, out StationTimes s))
                Stations[e.StationGlobalIndex] = s = new StationTimes { Region = e.RegionIndex, Station = e.StationIndex };
            switch (e.PhaseCode)
            {
                case "T1": if (double.IsNaN(s.T1)) s.T1 = t; break;
                case "T2": if (double.IsNaN(s.T2)) s.T2 = t; break;
                case "T3":
                    if (double.IsNaN(s.T3)) s.T3 = t;
                    s.T3Entries++;
                    break;
                case "T4":
                    if (double.IsNaN(s.FirstT4)) s.FirstT4 = t;
                    s.Rivets = Math.Max(s.Rivets, e.RivetIndex + 1);
                    break;
                case "T6": s.LastT6 = t; break;
            }
            // 窗口 2：第一次全局导航（自动机位跟随 AGV）。
            if (e.PhaseCode == "T1" && !_navDone && CanOpen())
            {
                _navDone = true;
                OpenWindow("T1 全局导航 · 跟随 AGV（自动机位 2）", 0, "M7_02_FollowNav.png", 7.0);
            }
        }

        private static void OnLoop(LoopTakenEvent e)
        {
            if (e.Label.Contains("换站")) _changeLoops++;
            else _nextLoops++;
        }

        private static void OnCell(CellStateChangedEvent e)
        {
            if (e.State == CellState.Triggered) _triggered++;
            if (e.State != CellState.Running) return;
            MissionCursor c = _mission.Orchestrator.Cursor;
            // 窗口 3：第 1 工位第 2 颗起的 T5 接触采集（自动机位末端特写）。
            if (!_closeupDone && e.CellId == "t(5,4)" && c.StationGlobalIndex == 0 && c.RivetIndex >= 1 && CanOpen())
            {
                _closeupDone = true;
                OpenWindow("T5 贴合采集 · 末端特写（自动机位 3）", 0, "M7_03_Closeup.png", 1.5);
            }
            // 窗口 4：第 2 工位起第 1 颗 T4 切到演示台，T5 接触采集开始后截图（演示屏高亮 t(5,4)）。
            if (!_deskDone && e.CellId == "t(4,1)" && c.StationGlobalIndex >= 1 && c.RivetIndex == 0 && CanOpen())
            {
                _deskDone = true;
                OpenWindow("T4～T5 · 演示台与大屏（机位 4）", 4, "M7_04_DemoStation.png", double.NaN);
            }
            if (_window != null && _window.Preset == 4 && !_window.ShotDone && double.IsNaN(_window.ShotAt) && e.CellId == "t(5,4)")
                _window.ShotAt = Math.Max(_window.StartReal + 3.0, EditorApplication.timeSinceStartup + 1.0);
        }

        // 新窗口可打开：没有窗口，或当前窗口已截图（提前结束）。布局检查完成前不开窗口。
        private static bool CanOpen() => _layoutIndex >= LayoutResolutions.Length * 2 && (_window == null || _window.ShotDone);

        private static void OpenWindow(string name, int preset, string shot, double shotDelayS)
        {
            if (_window != null) CloseWindow();
            double now = EditorApplication.timeSinceStartup;
            _window = new Window
            {
                Name = name, Preset = preset, Shot = shot, StartReal = now, StartSimS = Rel(_runtime.Clock.TimeS),
                ShotAt = double.IsNaN(shotDelayS) ? double.NaN : now + shotDelayS,
            };
            _runtime.FastForwardSimSecondsPerFrame = 0f;
            CameraRig rig = AppBootstrap.Active.Rig;
            if (rig != null)
            {
                if (preset > 0) rig.Select(preset);
                else rig.SetAuto(true);
            }
            SimLog.Info(LogCategory.System, $"M7 验证：观察窗口「{name}」开始（真实 2 倍速）");
        }

        private static void CloseWindow()
        {
            Windows.Add(_window);
            SimLog.Info(LogCategory.System, $"M7 验证：观察窗口「{_window.Name}」结束，{_window.Frames} 帧");
            _window = null;
            AppBootstrap.Active.Rig?.SetAuto(true);
            _runtime.FastForwardSimSecondsPerFrame = _ffSimPerFrame;
        }

        private static void UpdateAuto()
        {
            if (_wait > 0)
            {
                _wait--;
                return;
            }
            if (_layoutIndex < LayoutResolutions.Length * 2)
            {
                UpdateLayout();
                return;
            }
            if (!_overviewDone)
            {
                _overviewDone = true;
                OpenWindow("建图 · 总览（机位 1）", 1, "M7_01_Overview.png", 6.0);
            }
            if (_window == null)
            {
                if (_runtime.FastForwardSimSecondsPerFrame <= 0f) _runtime.FastForwardSimSecondsPerFrame = _ffSimPerFrame;
                return;
            }
            SampleFrame(_window);
            double now = EditorApplication.timeSinceStartup;
            if (!_window.ShotDone && !double.IsNaN(_window.ShotAt) && now >= _window.ShotAt)
            {
                CaptureShot(_window.Shot);
                _window.ShotDone = true;
                _window.ShotSimS = Rel(_runtime.Clock.TimeS);
            }
            // 截图后再测 WindowAfterShotS，或窗口已满 WindowMaxRealS 且已截图时结束；未截图最多等 3 倍时长。
            bool doneAfterShot = _window.ShotDone && now - _window.ShotAt >= WindowAfterShotS;
            bool full = _window.ShotDone && now - _window.StartReal >= WindowMaxRealS;
            if (doneAfterShot || full) CloseWindow();
            else if (now - _window.StartReal >= WindowMaxRealS * 3)
            {
                Notes.Add($"窗口「{_window.Name}」超时未截图");
                CloseWindow();
            }
        }

        // 布局检查：每个分辨率依次检查演示屏（全屏面板）与 HUD（窗口模式）。
        private static void UpdateLayout()
        {
            Vector2Int size = LayoutResolutions[_layoutIndex / 2];
            bool hud = _layoutIndex % 2 == 1;
            DemoUiHub hub = AppBootstrap.Active.Hub;
            switch (_layoutStep)
            {
                case 0:
                    hub?.SetFullscreen(!hud);
                    _layoutStep = 1;
                    break;
                case 1:
                    UiLayoutCheck.Begin(PanelOf(hud), size);
                    _layoutStep = 2;
                    break;
                default:
                    VisualElement root = hud ? AppBootstrap.Active.Hud.Root : AppBootstrap.Active.DemoConsole.Root;
                    LayoutRows.Add(UiLayoutCheck.Check(root, size, hud ? "HUD" : "演示屏", out int issues));
                    _layoutIssues += issues;
                    UiLayoutCheck.End();
                    hub?.SetFullscreen(false);
                    _layoutStep = 0;
                    _layoutIndex++;
                    break;
            }
            _wait = LayoutSettleFrames;
        }

        private static PanelSettings PanelOf(bool hud) =>
            (hud ? AppBootstrap.Active.Hud.GetComponent<UIDocument>() : AppBootstrap.Active.DemoConsole.GetComponent<UIDocument>()).panelSettings;

        private static void SampleFrame(Window w)
        {
            int frame = Time.frameCount;
            if (frame == _lastFrame) return;
            _lastFrame = frame;
            if (w.Dts.Count < WarmupFrames)
            {
                w.Dts.Add(-1d);
                return;
            }
            double dt = Time.unscaledDeltaTime;
            w.Frames++;
            w.SumDt += dt;
            if (dt > w.MaxDt) w.MaxDt = dt;
            w.Dts.Add(dt);
            w.SumMainMs += _mainThread.Valid ? _mainThread.LastValue * 1e-6 : 0d;
            long bytes = _gcBytes.Valid ? _gcBytes.LastValue : 0;
            w.SumGcBytes += bytes;
            w.SumGcCount += _gcCount.Valid ? _gcCount.LastValue : 0;
            if (bytes > 0) w.FramesWithGc++;
            w.Screen = new Vector2Int(Screen.width, Screen.height);
        }

        private static void CaptureShot(string fileName)
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            var rt = new RenderTexture(ShotSizePx.x, ShotSizePx.y, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previousTarget = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = previousTarget;
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var image = new Texture2D(ShotSizePx.x, ShotSizePx.y, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, ShotSizePx.x, ShotSizePx.y), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes($"{ShotFolder}/{fileName}", image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            SimLog.Info(LogCategory.System, $"M7 验证：截图 {fileName}");
        }

        private static void OnAutoCompleted(MissionRunReport report)
        {
            if (_window != null) CloseWindow();
            double real = EditorApplication.timeSinceStartup - _realStart;
            float speed = _runtime.Clock.SpeedMultiplier;
            var sb = new StringBuilder();
            sb.AppendLine("## 1 自动演示（默认倍速，完整流程）");
            sb.AppendLine();
            sb.AppendLine($"- 结果：{(report != null && report.Success ? "完成" : "未完成 " + report?.FailureReason)}；倍速 {speed:0.#} 倍；{_resolutionInfo}");
            if (report != null)
            {
                sb.AppendLine($"- 仿真用时：建图 {report.MappingS:0.0} s，任务矩阵 {report.MatrixS:0.0} s，返回 {report.ReturnS:0.0} s，合计 {report.TotalS:0.0} s");
                sb.AppendLine($"- **真实 {speed:0.#} 倍速下总时长估算：{Clock(report.TotalS / speed)}**（仿真合计 ÷ 倍速；演示台停留按实际时间但不暂停仿真，不增加时长）");
                sb.AppendLine($"- 检测铆钉 {report.Records.Count} 颗；条件检查 {report.ConditionChecks} 次（✗ {report.ConditionFailures}）；“下一颗”回路 {_nextLoops} 次，“换站”回路 {_changeLoops} 次；条件触发单元 {_triggered} 次；阶段进入 {_phaseEntries} 次");
            }
            sb.AppendLine($"- 本次验证实际用时 {real:0} s（观察窗口按真实 {speed:0.#} 倍速，其余时段每帧快进 {_ffSimPerFrame:0.##} s 仿真）");
            sb.AppendLine();
            AppendTimeline(sb, report, speed);
            AppendLayout(sb);
            AppendPerf(sb);
            AppendGc(sb);
            if (Notes.Count > 0)
            {
                sb.AppendLine("### 备注");
                foreach (string n in Notes) sb.AppendLine($"- {n}");
                sb.AppendLine();
            }
            SessionState.SetString(AutoSectionKey, sb.ToString());
            WriteReport(sb.ToString(), "（分步演示验证进行中）");
            GcProbe.Enabled = false;
            SessionState.SetString(StageKey, "to_step");
            EditorApplication.delayCall += () => { if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode(); };
        }

        private static string Clock(double s)
        {
            int total = (int)Math.Round(s);
            return $"{total / 60:00}:{total % 60:00}";
        }

        private static void AppendTimeline(StringBuilder sb, MissionRunReport report, float speed)
        {
            sb.AppendLine($"### 1.1 演示时间线（真实 {speed:0.#} 倍速，mm:ss；括号内为仿真时间 s）");
            sb.AppendLine();
            sb.AppendLine("| 时刻 | 事件 |");
            sb.AppendLine("|---|---|");
            var rows = new List<(double t, int order, string text)>();
            void Add(double sim, string text)
            {
                if (!double.IsNaN(sim)) rows.Add((sim, rows.Count, text));
            }
            Add(0d, "工序开始：AGV 在充电位，激光建图（总览机位，演示屏显示建图准备与小地图栅格增长）");
            if (report != null) Add(report.MappingS, "建图完成，进入 SOP 任务矩阵");
            foreach (StationTimes s in Stations.Values)
            {
                string where = $"区域 {s.Region + 1} 工位 {s.Station + 1}";
                Add(s.T1, $"{where}：T1 全局导航出发（跟随机位）");
                Add(s.T2, $"{where}：T2 精确停靠{(double.IsNaN(s.T1) ? "（同一区域换站，直接转 T2）" : "")}");
                Add(s.T3, $"{where}：T3 注册（进入 {s.T3Entries} 次{(s.T3Entries > 1 ? "，含重试" : "")}）");
                Add(s.FirstT4, $"{where}：第 1 颗 T4 对中 → T5 贴合采集 → T6 释放（末端特写机位，逐钉“下一颗”）");
                Add(s.LastT6, $"{where}：第 {s.Rivets} 颗（末颗）T6 释放退出，倒车退出换站");
            }
            if (report != null)
            {
                Add(report.MappingS + report.MatrixS, "任务矩阵完成，返回充电位");
                Add(report.TotalS, "回到充电位，导出 CSV，演示结束");
            }
            foreach (Window w in Windows)
                if (w.ShotDone) Add(w.ShotSimS, $"📷 截图 {w.Shot}（{w.Name}）");
            foreach ((double t, int _, string text) in rows.OrderBy(r => r.t).ThenBy(r => r.order))
                sb.AppendLine($"| {Clock(t / speed)}（{t:0}） | {text} |");
            sb.AppendLine();
        }

        private static void AppendLayout(StringBuilder sb)
        {
            sb.AppendLine($"### 1.2 界面布局检查（主场景，问题合计 {_layoutIssues}）");
            sb.AppendLine();
            sb.AppendLine(UiLayoutCheck.TableHeader);
            foreach (string row in LayoutRows) sb.AppendLine(row);
            sb.AppendLine();
        }

        private static void AppendPerf(StringBuilder sb)
        {
            sb.AppendLine("### 1.3 帧率与每帧 GC（观察窗口，真实倍速；剔除每个窗口前 30 帧）");
            sb.AppendLine();
            sb.AppendLine("> Editor Play 模式测量（含编辑器开销），独立程序通常更高。GC 列为 Profiler 计数器 GC Allocated In Frame（主线程全部托管分配，含编辑器自身）。");
            sb.AppendLine();
            sb.AppendLine("| 窗口 | 渲染分辨率 | 帧数 | 平均 FPS | 最低 FPS | 1% 低 FPS | 主线程平均 ms | 平均 GC B/帧 | 平均 GC 次/帧 | 有 GC 的帧 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            int frames = 0;
            double sumDt = 0d, maxDt = 0d;
            var all = new List<double>();
            foreach (Window w in Windows)
            {
                if (w.Frames == 0) continue;
                List<double> dts = w.Dts.Where(d => d >= 0d).ToList();
                all.AddRange(dts);
                frames += w.Frames;
                sumDt += w.SumDt;
                maxDt = Math.Max(maxDt, w.MaxDt);
                sb.AppendLine($"| {w.Name} | {w.Screen.x}×{w.Screen.y} | {w.Frames} | {w.Frames / w.SumDt:0.0} | {1d / w.MaxDt:0.0} | {OnePercentLow(dts):0.0} | " +
                              $"{w.SumMainMs / w.Frames:0.00} | {(double)w.SumGcBytes / w.Frames:0} | {(double)w.SumGcCount / w.Frames:0.00} | {w.FramesWithGc} |");
            }
            if (frames > 0)
                sb.AppendLine($"| **合计** | | {frames} | **{frames / sumDt:0.0}** | **{1d / maxDt:0.0}** | {OnePercentLow(all):0.0} | | | | |");
            sb.AppendLine();
        }

        private static double OnePercentLow(List<double> dts)
        {
            if (dts.Count == 0) return 0d;
            List<double> sorted = dts.OrderByDescending(d => d).ToList();
            int n = Math.Max(1, sorted.Count / 100);
            return n / sorted.Take(n).Sum();
        }

        private static void AppendGc(StringBuilder sb)
        {
            sb.AppendLine("### 1.4 模块 GC 探针（整个流程，含快进时段；GC.Alloc 标记，仅主线程）");
            sb.AppendLine();
            sb.AppendLine("| 模块 | 调用次数 | GC.Alloc 次数 | 有分配的调用 | 平均 ms | 最大 ms |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (ProbeModule m in Enum.GetValues(typeof(ProbeModule)))
                sb.AppendLine($"| {m} | {GcProbe.CallCount(m)} | {GcProbe.AllocCount(m)} | {GcProbe.CallsWithAllocCount(m)} | {GcProbe.AverageMs(m):0.000} | {GcProbe.MaxDurationMs(m):0.000} |");
            sb.AppendLine($"| 演示屏 + HUD 刷新（UiPerfProbe） | {UiPerfProbe.FrameCount} | {UiPerfProbe.FrameAllocCount} | {UiPerfProbe.FramesWithAlloc} | | |");
            sb.AppendLine();
        }

        private static void DisposeRecorders()
        {
            if (_gcBytes.Valid) _gcBytes.Dispose();
            if (_gcCount.Valid) _gcCount.Dispose();
            if (_mainThread.Valid) _mainThread.Dispose();
        }

        // ======================= ② 分步演示 =======================

        private static void OnStepStarted(RobotRuntime runtime, MissionRoutine mission)
        {
            _runtime = runtime;
            _mission = mission;
            _realStart = EditorApplication.timeSinceStartup;
            _stepPauses = _stepPauseLeaks = _stepPhaseEntries = _pauseFrames = 0;
            _pauseSeen = _stepPoseProvided = _stepPoseOpened = false;
            AppBootstrap.Active.Hub?.Mode.SetMode(DemoMode.Step);
            runtime.Bus.Subscribe<PhaseEnteredEvent>(_ => _stepPhaseEntries++);
            mission.OperatorPose.Opened += _ => _stepPoseOpened = true;
            SimLog.EntryLogged -= OnStepLog;
            SimLog.EntryLogged += OnStepLog;
        }

        private static void OnStepLog(SimLogEntry e)
        {
            if (e.Message.Contains("[模拟操作员] 初值")) _stepPoseProvided = true;
        }

        private static void UpdateStep()
        {
            DemoModeController mode = AppBootstrap.Active.Hub?.Mode;
            if (mode == null || !mode.IsPaused)
            {
                _pauseSeen = false;
                return;
            }
            if (!_pauseSeen)
            {
                _pauseSeen = true;
                _stepPauses++;
                _pauseFrames = 0;
                _pauseSim = _runtime.Clock.TimeS;
                return;
            }
            if (++_pauseFrames < StepNextDelayFrames) return;
            if (Math.Abs(_runtime.Clock.TimeS - _pauseSim) > 1e-9) _stepPauseLeaks++;
            mode.Next();
        }

        private static void OnStepCompleted(MissionRunReport report)
        {
            SimLog.EntryLogged -= OnStepLog;
            double real = EditorApplication.timeSinceStartup - _realStart;
            bool ok = report != null && report.Success && _stepPauses == _stepPhaseEntries && _stepPauseLeaks == 0;
            var sb = new StringBuilder();
            sb.AppendLine("## 2 分步演示（完整流程，快进）");
            sb.AppendLine();
            sb.AppendLine($"- 结论：**{(ok ? "通过" : "未通过")}**；结果 {(report != null && report.Success ? "完成" : "未完成 " + report?.FailureReason)}，仿真合计 {report?.TotalS:0.0} s，实际 {real:0} s");
            sb.AppendLine($"- 阶段进入 {_stepPhaseEntries} 次，分步暂停 {_stepPauses} 次（应相等），暂停期间仿真时间推进 {_stepPauseLeaks} 次（应为 0）；每次暂停后由验证工具按“下一步”");
            sb.AppendLine($"- t(1,8) 识别失败注入（区域 2 首次 T1）：初值通道打开 {(_stepPoseOpened ? "是" : "否")}，模拟操作员给出初值 {(_stepPoseProvided ? "是" : "否")}（分步模式仍允许超时自动给初值）");
            sb.AppendLine();
            WriteReport(SessionState.GetString(AutoSectionKey, "（自动演示结果缺失）\n"), sb.ToString());
            SessionState.EraseString(StageKey);
            EditorApplication.delayCall += () => { if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode(); };
        }

        private static void WriteReport(string autoSection, string stepSection)
        {
            string manual = "";
            if (File.Exists(ReportPath))
            {
                string old = File.ReadAllText(ReportPath);
                int i = old.IndexOf(ManualMarker, StringComparison.Ordinal);
                if (i >= 0) manual = old.Substring(i);
            }
            var sb = new StringBuilder();
            sb.AppendLine("# M7 联调验证报告");
            sb.AppendLine();
            sb.AppendLine($"生成：{DateTime.Now:yyyy-MM-dd HH:mm}（RivetSim/Verify/Run M7 Demo，种子见 CFG_SimSettings）");
            sb.AppendLine();
            sb.Append(autoSection);
            sb.Append(stepSection);
            sb.AppendLine();
            sb.Append(string.IsNullOrEmpty(manual) ? ManualMarker + "\n" : manual);
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, sb.ToString(), new UTF8Encoding(false));
            SimLog.Info(LogCategory.System, $"M7 验证：报告已写入 {ReportPath}");
        }
    }
}
