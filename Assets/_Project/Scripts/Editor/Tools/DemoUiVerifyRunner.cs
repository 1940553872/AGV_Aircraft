using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RivetSim.Core.Logging;
using RivetSim.Core.Mission;
using RivetSim.Core.TaskMatrix;
using RivetSim.Editor.Generators;
using RivetSim.Presentation.App;
using RivetSim.Presentation.UI;
using RivetSim.Simulation.Inspection;
using RivetSim.Simulation.Mission;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace RivetSim.Editor.Tools
{
    /// <summary>
    /// M6 演示界面验证（RivetSim/Verify/Run Demo UI）：
    /// ① Sandbox_UI 用假数据逐步走完全部 8 种单元状态，核对各状态的单元样式，检查 1920×1080 与 2560×1440 下演示屏与 HUD 无溢出，
    /// 统计界面逐帧 GC 分配与事件处理用时；② 主场景跑一次带 1 次故障注入（T1 识别失败）的快速流程，核对演示屏单元状态与仿真一致、
    /// t(1,8) 提示与初值写回、单钉节拍扣除收臂降柱。结果写入 Docs/Reports/M6.md。
    /// 截图时刻暂停编辑器并写 SessionState（RivetSim.M6Verify.Stage = shot_matrix / shot_pose / shot_hud / shot_monitor），
    /// 外部截图后 eval <see cref="Resume"/> 继续。
    /// </summary>
    [InitializeOnLoad]
    public static class DemoUiVerifyRunner
    {
        /// <summary>SessionState 键：阶段。</summary>
        public const string StageKey = "RivetSim.M6Verify.Stage";

        private const string SandboxSectionKey = "RivetSim.M6Verify.Sandbox";
        private const string ReportPath = "Docs/Reports/M6.md";
        private const int StepSettleFrames = 3;
        private const int LayoutSettleFrames = 4;
        private const double CameraSettleS = 1.5;
        private const int HudShotPreset = 3;
        private const int IdleMeasureFrames = 120;
        private const float MatrixShotDelayS = 0.35f;
        private const float Tolerance = 1.5f;
        private const string MatrixShotMarker = "T5 条件判定 C2～C8（含";
        private const string PoseMarker = "t(1,8) 等待操作员";
        // USS 中设了 overflow: hidden 的容器类（IResolvedStyle 不提供 overflow）。
        private static readonly string[] ClipClasses = { "dc-body", "dc-left", "dc-right", "dc-explain-card", "dc-log", "cell" };
        private static readonly Vector2Int[] Resolutions = { new Vector2Int(1920, 1080), new Vector2Int(2560, 1440) };
        private static readonly Color Border = new Color32(0x26, 0x3A, 0x52, 0xFF);
        private static readonly Color StandbyGray = new Color32(0x56, 0x65, 0x77, 0xFF);
        private static readonly Color Secondary = new Color32(0x8F, 0xA3, 0xB8, 0xFF);
        private static readonly Color Running = new Color32(0xFF, 0x9F, 0x1C, 0xFF);
        private static readonly Color Done = new Color32(0x3D, 0xD6, 0x8C, 0xFF);
        private static readonly Color Amber = new Color32(0xFF, 0xC8, 0x57, 0xFF);
        private static readonly Color Failed = new Color32(0xFF, 0x5D, 0x5D, 0xFF);

        // —— 运行状态（Play 内，域重载后由 SessionState 恢复阶段） ——
        private static int _wait;
        private static string _phase = "";
        private static float _shotAt;
        private static readonly Dictionary<CellState, string> StyleRows = new Dictionary<CellState, string>();
        private static readonly Dictionary<CellState, bool> StylePass = new Dictionary<CellState, bool>();
        private static readonly List<string> LayoutRows = new List<string>();
        private static int _layoutIndex;
        private static RenderTexture _layoutRt;
        private static PanelSettings _layoutPanel;
        private static RenderTexture _layoutPanelOriginal;
        private static string _condSnapshot = "";
        private static string _condSnapshotTitle = "";
        private static bool _posePromptSeen, _posePickAccepted;
        private static double _sandboxFrameAvg, _sandboxEventAvgMs, _sandboxEventMaxMs, _idleFrameAvg;
        private static int _sandboxFrames, _sandboxFramesAlloc, _sandboxEvents, _idleFramesAlloc, _idleFrames;

        // 主场景。
        private static MissionRoutine _mission;
        private static RobotRuntime _runtime;
        private static bool _clockFrozen;
        private static float _savedFastForward;
        private static readonly List<(string id, CellState expected)> PendingMismatch = new List<(string, CellState)>();
        private static int _consistencySamples, _persistentMismatches;
        private static string _mismatchExample = "";
        private static bool _mainPoseOpened, _mainPoseClosed, _initialPoseLogged, _hudShotDone, _monitorShotDone, _poseShotDone, _monitorArmed;
        private static int _mainTriggered;
        private static double _realStart;
        private static readonly List<string> MainLog = new List<string>();

        static DemoUiVerifyRunner()
        {
            if (string.IsNullOrEmpty(SessionState.GetString(StageKey, ""))) return;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
            if (EditorApplication.isPlayingOrWillChangePlaymode && Stage == "main") ConfigureMain();
            // 退出 Play 时可能发生域重载，排队的 delayCall 会丢失：在新域里补发主场景启动。
            if (!EditorApplication.isPlayingOrWillChangePlaymode && Stage == "to_main") EditorApplication.delayCall += LaunchMain;
        }

        /// <summary>打开主场景并进入 Play（第二阶段；也可在中断后手动调用）。</summary>
        public static void LaunchMain()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || (Stage != "to_main" && Stage != "main")) return;
            SessionState.SetString(StageKey, "main");
            EditorSceneManager.OpenScene(ProjectPaths.MainScene, OpenSceneMode.Single);
            ConfigureMain();
            SimLog.Info(LogCategory.System, "M6 验证：主场景进入 Play 模式");
            EditorApplication.EnterPlaymode();
        }

        private static string Stage => SessionState.GetString(StageKey, "");

        /// <summary>启动验证（先 Sandbox_UI，再主场景）。</summary>
        [MenuItem("RivetSim/Verify/Run Demo UI", priority = 101)]
        public static void Run()
        {
            if (EditorApplication.isPlaying) return;
            if (!MainSceneBuilder.EnsureNoUnsavedScenes()) return;
            EditorSceneManager.OpenScene(ProjectPaths.SandboxUiScene, OpenSceneMode.Single);
            SessionState.SetString(StageKey, "sandbox");
            SessionState.EraseString(SandboxSectionKey);
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
            SimLog.Info(LogCategory.System, "M6 验证：Sandbox_UI 进入 Play 模式");
            EditorApplication.EnterPlaymode();
        }

        /// <summary>截图后继续（外部调用）。</summary>
        public static void Resume()
        {
            string stage = Stage;
            if (!stage.StartsWith("shot_")) return;
            SessionState.SetString(StageKey, stage == "shot_matrix" ? "sandbox" : "main");
            if (_clockFrozen && _runtime != null) _runtime.FastForwardSimSecondsPerFrame = _savedFastForward;
            if (stage == "shot_monitor") SelectCamera(0);
            _clockFrozen = false;
            EditorApplication.isPaused = false;
        }

        /// <summary>中止验证。</summary>
        public static void Abort()
        {
            SessionState.EraseString(StageKey);
            EditorApplication.isPaused = false;
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        }

        private static void Pause(string shot)
        {
            SessionState.SetString(StageKey, shot);
            SimLog.Info(LogCategory.System, $"M6 验证：暂停等待截图（{shot}）");
            EditorApplication.isPaused = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                Application.runInBackground = true;
                _phase = "";
                _wait = 0;
                return;
            }
            if (change != PlayModeStateChange.EnteredEditMode) return;
            MissionLaunchOptions.Clear();
            if (Stage == "to_main")
            {
                EditorApplication.delayCall += LaunchMain;
                return;
            }
            SessionState.EraseString(StageKey);
            EditorApplication.update -= OnUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private static void OnUpdate()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
            string stage = Stage;
            if (stage == "sandbox") UpdateSandbox();
            else if (stage == "main") UpdateMain();
        }

        // ======================= ① Sandbox_UI =======================

        private static void UpdateSandbox()
        {
            SandboxUiBootstrap box = SandboxUiBootstrap.Active;
            if (box == null || box.Feed == null || !box.Console.IsBuilt) return;
            if (_wait-- > 0) return;
            FakeMissionFeed feed = box.Feed;
            switch (_phase)
            {
                case "":
                    UiPerfProbe.Reset();
                    StyleRows.Clear();
                    StylePass.Clear();
                    LayoutRows.Clear();
                    _phase = "steps";
                    break;
                case "steps":
                    RecordStyles(box.Console);
                    if (feed.IsFinished)
                    {
                        CaptureSandboxPerf();
                        _phase = "idle";
                        _wait = IdleMeasureFrames;
                        UiPerfProbe.Reset();
                        break;
                    }
                    feed.Step();
                    _wait = StepSettleFrames;
                    if (feed.LastTitle.Contains(PoseMarker)) _phase = "pose";
                    else if (feed.LastTitle.Contains(MatrixShotMarker))
                    {
                        _phase = "matrixShot";
                        _shotAt = Time.unscaledTime + MatrixShotDelayS;
                    }
                    break;
                case "pose":
                    RecordStyles(box.Console);
                    _posePromptSeen = box.Hud.IsPosePromptOpen;
                    // 与点击同一路径：小地图点选 → 通道 → 提示关闭。
                    _posePickAccepted = box.Hud.PoseMap != null && box.Hud.PoseMap.PickAt(new Vector2(-6f, 4f)) && box.Feed.OperatorPose.HasSubmission;
                    _phase = "steps";
                    break;
                case "matrixShot":
                    RecordStyles(box.Console);
                    if (Time.unscaledTime < _shotAt) break;
                    _condSnapshotTitle = feed.LastTitle;
                    _condSnapshot = string.Join("；", Enumerable.Range(0, box.Console.ConditionLineCount).Select(box.Console.ConditionLineText));
                    _phase = "layout";
                    _layoutIndex = 0;
                    Pause("shot_matrix");
                    break;
                case "layout":
                    if (_layoutIndex < Resolutions.Length * 2)
                    {
                        if (_layoutRt == null) BeginLayout(box, Resolutions[_layoutIndex / 2], _layoutIndex % 2 == 1);
                        else
                        {
                            CheckLayout(box, Resolutions[_layoutIndex / 2], _layoutIndex % 2 == 1);
                            EndLayout(box);
                            _layoutIndex++;
                        }
                        _wait = LayoutSettleFrames;
                        break;
                    }
                    _phase = "steps";
                    break;
                case "idle":
                    _idleFrames = UiPerfProbe.FrameCount;
                    _idleFramesAlloc = UiPerfProbe.FramesWithAlloc;
                    _idleFrameAvg = _idleFrames > 0 ? (double)UiPerfProbe.FrameAllocCount / _idleFrames : 0d;
                    SessionState.SetString(SandboxSectionKey, BuildSandboxSection(feed));
                    SessionState.SetString(StageKey, "to_main");
                    SimLog.Info(LogCategory.System, "M6 验证：Sandbox_UI 完成，转主场景");
                    EditorApplication.ExitPlaymode();
                    break;
            }
        }

        private static void CaptureSandboxPerf()
        {
            _sandboxFrames = UiPerfProbe.FrameCount;
            _sandboxFramesAlloc = UiPerfProbe.FramesWithAlloc;
            _sandboxFrameAvg = _sandboxFrames > 0 ? (double)UiPerfProbe.FrameAllocCount / _sandboxFrames : 0d;
            _sandboxEvents = UiPerfProbe.EventCount;
            _sandboxEventAvgMs = _sandboxEvents > 0 ? UiPerfProbe.EventTotalMs / _sandboxEvents : 0d;
            _sandboxEventMaxMs = UiPerfProbe.EventMaxMs;
        }

        private static void RecordStyles(DemoConsoleView console)
        {
            foreach (CellState s in Enum.GetValues(typeof(CellState)))
            {
                if (StyleRows.ContainsKey(s)) continue;
                TaskCellElement cell = FindDisplayed(console, s);
                if (cell == null) continue;
                (bool pass, string detail) = CheckStyle(cell, s);
                StylePass[s] = pass;
                StyleRows[s] = $"| {s} | {cell.Cell.Id}（{cell.Cell.Kind}） | {(cell.ClassListContains(TaskCellElement.StateClassPrefix + s.ToString().ToLowerInvariant()) ? "有" : "无")} | {detail} | {(pass ? "✓" : "✗")} |";
            }
        }

        private static TaskCellElement FindDisplayed(DemoConsoleView console, CellState state)
        {
            TaskCellElement fallback = null;
            foreach (string id in AllCellIds(console))
            {
                TaskCellElement c = console.FindCell(id);
                if (c == null || c.DisplayState != state) continue;
                // Idle 优先取执行单元（普通边框），其余取第一个。
                if (state != CellState.Idle || c.Cell.Kind == CellKind.Active) return c;
                fallback ??= c;
            }
            return fallback;
        }

        private static IEnumerable<string> AllCellIds(DemoConsoleView console) =>
            console.Root.Query<TaskCellElement>().ToList().Select(c => c.Cell.Id);

        private static (bool, string) CheckStyle(TaskCellElement cell, CellState s)
        {
            IResolvedStyle r = cell.resolvedStyle;
            DashedBorderElement dashed = cell.Q<DashedBorderElement>();
            bool dashVisible = dashed != null && dashed.resolvedStyle.display == DisplayStyle.Flex;
            Color dashColor = dashed != null ? dashed.StrokeColor : Color.clear;
            Label text = cell.Q<Label>(className: "cell-text");
            Label badge = cell.Q<Label>(className: "cell-done-badge");
            VisualElement progress = cell.Q(className: "cell-progress");
            string border = $"边框 {r.borderTopWidth:0.#} px {Hex(r.borderTopColor)}";
            switch (s)
            {
                case CellState.Idle:
                    return cell.Cell.Kind == CellKind.Conditional
                        ? (dashVisible && Near(dashColor, StandbyGray), $"虚线 {Hex(dashColor)}")
                        : (Mathf.Abs(r.borderTopWidth - 1f) < 0.1f && Near(r.borderTopColor, Border) && r.opacity > 0.99f, border + "，正文色");
                case CellState.Standby:
                    return (Near(text.resolvedStyle.color, StandbyGray), $"文字 {Hex(text.resolvedStyle.color)}（灰字）");
                case CellState.Armed:
                    return (dashVisible && Near(dashColor, Secondary), $"虚线可见 {dashVisible}，{Hex(dashColor)}");
                case CellState.Running:
                    return (r.borderTopWidth >= 3.9f && NearRgb(r.borderTopColor, Running) && progress.resolvedStyle.display == DisplayStyle.Flex,
                        border + $"（呼吸光 α={r.borderTopColor.a:0.00}），进度条 {progress.resolvedStyle.display}");
                case CellState.Done:
                    return (NearRgb(r.borderTopColor, Done) && badge.resolvedStyle.display == DisplayStyle.Flex, border + $"，✓ 角标 {badge.resolvedStyle.display}");
                case CellState.Skipped:
                    return (r.opacity < 0.5f, $"不透明度 {r.opacity:0.00}");
                case CellState.Triggered:
                    return (dashVisible && NearRgb(dashColor, Amber), $"虚线 {Hex(dashColor)}（琥珀色闪烁），文字 {Hex(text.resolvedStyle.color)}");
                case CellState.Failed:
                    return (NearRgb(r.borderTopColor, Failed) && r.borderTopWidth >= 1.9f, border);
            }
            return (false, "");
        }

        private static bool Near(Color a, Color b) => NearRgb(a, b) && Mathf.Abs(a.a - b.a) < 0.05f;

        private static bool NearRgb(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.03f && Mathf.Abs(a.g - b.g) < 0.03f && Mathf.Abs(a.b - b.b) < 0.03f;

        private static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        // —— 布局检查：把面板临时渲染到指定分辨率的 RenderTexture，再核对元素边界与文字是否被截断 ——

        private static void BeginLayout(SandboxUiBootstrap box, Vector2Int size, bool hud)
        {
            _layoutRt = new RenderTexture(size.x, size.y, 24);
            if (hud) box.Hub.SetFullscreen(false);
            _layoutPanel = PanelOf(box, hud);
            _layoutPanelOriginal = _layoutPanel.targetTexture;
            _layoutPanel.targetTexture = _layoutRt;
        }

        private static void EndLayout(SandboxUiBootstrap box)
        {
            // 只还原本次改过的面板（演示屏贴图面板须保持指向 T_DemoConsoleRT）。
            _layoutPanel.targetTexture = _layoutPanelOriginal;
            _layoutPanel = null;
            box.Hub.SetFullscreen(true);
            _layoutRt.Release();
            UnityEngine.Object.DestroyImmediate(_layoutRt);
            _layoutRt = null;
        }

        private static PanelSettings PanelOf(SandboxUiBootstrap box, bool hud) =>
            (hud ? box.Hud.GetComponent<UIDocument>() : box.Console.GetComponent<UIDocument>()).panelSettings;

        private static void CheckLayout(SandboxUiBootstrap box, Vector2Int size, bool hud)
        {
            VisualElement root = hud ? box.Hud.Root : box.Console.Root;
            var issues = new List<string>();
            int checkedCount = 0, truncated = 0;
            Rect rootRect = root.worldBound;
            foreach (VisualElement e in root.Query<VisualElement>().ToList())
            {
                if (!IsShown(e, root)) continue;
                checkedCount++;
                Rect b = e.worldBound;
                if (b.width > 0f && (b.xMin < rootRect.xMin - Tolerance || b.yMin < rootRect.yMin - Tolerance ||
                                     b.xMax > rootRect.xMax + Tolerance || b.yMax > rootRect.yMax + Tolerance))
                    issues.Add($"{Describe(e)} 超出屏幕 {b}");
                VisualElement clip = ClippingAncestor(e, root);
                if (clip != null && b.height > 0f && b.yMax > clip.worldBound.yMax + Tolerance)
                    issues.Add($"{Describe(e)} 被 {Describe(clip)} 裁切（底 {b.yMax:0} > {clip.worldBound.yMax:0}）");
                if (e is Label label && !string.IsNullOrEmpty(label.text))
                {
                    bool ellipsis = label.resolvedStyle.whiteSpace == WhiteSpace.NoWrap && label.resolvedStyle.textOverflow == TextOverflow.Ellipsis;
                    Rect c = label.contentRect;
                    Vector2 need = label.resolvedStyle.whiteSpace == WhiteSpace.NoWrap
                        ? label.MeasureTextSize(label.text, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined)
                        : label.MeasureTextSize(label.text, c.width, VisualElement.MeasureMode.Exactly, 0f, VisualElement.MeasureMode.Undefined);
                    bool overflow = need.x > c.width + Tolerance || need.y > c.height + Tolerance;
                    if (overflow && ellipsis) truncated++;
                    else if (overflow) issues.Add($"{Describe(e)} 文字溢出（需 {need.x:0}×{need.y:0}，实 {c.width:0}×{c.height:0}）「{Short(label.text)}」");
                }
            }
            string name = hud ? "HUD" : "演示屏";
            LayoutRows.Add($"| {size.x}×{size.y} | {name} | {checkedCount} | {issues.Count} | {truncated} | {(issues.Count == 0 ? "无溢出" : string.Join("<br>", issues.Take(6)))} |");
        }

        private static bool IsShown(VisualElement e, VisualElement root)
        {
            for (VisualElement p = e; p != null; p = p.parent)
            {
                if (p.resolvedStyle.display == DisplayStyle.None || p.resolvedStyle.visibility == Visibility.Hidden) return false;
                if (p == root) return true;
            }
            return true;
        }

        private static VisualElement ClippingAncestor(VisualElement e, VisualElement root)
        {
            for (VisualElement p = e.parent; p != null && p != root; p = p.parent)
                if (ClipClasses.Any(p.ClassListContains)) return p;
            return null;
        }

        private static string Describe(VisualElement e) => string.IsNullOrEmpty(e.name) ? $"{e.GetType().Name}.{e.GetClasses().FirstOrDefault()}" : e.name;

        private static string Short(string s) => s.Length > 24 ? s.Substring(0, 24).Replace('\n', ' ') + "…" : s.Replace('\n', ' ');

        private static string BuildSandboxSection(FakeMissionFeed feed)
        {
            var sb = new StringBuilder();
            sb.AppendLine("## 1. Sandbox_UI 假数据（FakeMissionFeed）");
            sb.AppendLine();
            int covered = StylePass.Count;
            int passed = StylePass.Count(p => p.Value);
            sb.AppendLine($"- 假数据 {feed.StepCount} 步全部推进；发布过的状态 {feed.PublishedStates.Count}/8，回路 {string.Join("、", feed.PublishedLoops)}。");
            sb.AppendLine($"- 单元样式核对：显示过 {covered}/8 种状态，通过 {passed}/{covered}。");
            sb.AppendLine($"- t(1,8) 小地图点选：提示显示 {(_posePromptSeen ? "是" : "否")}，点选写回通道 {(_posePickAccepted ? "是" : "否")}。");
            sb.AppendLine($"- 截图时条件列表（{_condSnapshotTitle}）：{_condSnapshot}");
            sb.AppendLine();
            sb.AppendLine("### 1.1 各状态样式核对（Architecture §5.2）");
            sb.AppendLine();
            sb.AppendLine("| 状态 | 取样单元 | USS 类 | 实测样式 | 结论 |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (CellState s in Enum.GetValues(typeof(CellState)))
                sb.AppendLine(StyleRows.TryGetValue(s, out string row) ? row : $"| {s} | 未显示 | — | — | ✗ |");
            sb.AppendLine();
            sb.AppendLine("### 1.2 分辨率布局检查");
            sb.AppendLine();
            sb.AppendLine("面板临时渲染到对应分辨率的 RenderTexture（ScaleWithScreenSize，参考 1920×1080）后核对：元素不超出屏幕、不被 overflow:hidden 容器裁切、文字不溢出；单行省略号（ellipsis）计为“截断”，不算溢出。");
            sb.AppendLine("演示屏取 T5 条件判定时刻（C2～C8 七行条件 + 执行情况说明最满）；HUD 取常规状态。");
            sb.AppendLine();
            sb.AppendLine("| 分辨率 | 面板 | 检查元素 | 溢出 | 省略号截断 | 结论 |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (string row in LayoutRows) sb.AppendLine(row);
            sb.AppendLine();
            sb.AppendLine("### 1.3 界面开销（演示屏 + HUD 的 LateUpdate 与事件处理；GC.Alloc 标记计数，仅主线程界面代码）");
            sb.AppendLine();
            sb.AppendLine($"- 逐步推进期间：{_sandboxFrames} 帧，平均 {_sandboxFrameAvg:0.00} 次分配/帧，有分配的帧 {_sandboxFramesAlloc}（事件后的刷新帧：文字格式化）。");
            sb.AppendLine($"- 空闲（无事件）{_idleFrames} 帧：平均 {_idleFrameAvg:0.00} 次分配/帧，有分配的帧 {_idleFramesAlloc}。");
            sb.AppendLine($"- 事件处理：{_sandboxEvents} 次，平均 {_sandboxEventAvgMs * 1000d:0.0} µs，最长 {_sandboxEventMaxMs * 1000d:0.0} µs（只改模型与脏标记）。");
            sb.AppendLine();
            return sb.ToString();
        }

        // ======================= ② 主场景快速流程 =======================

        private static void ConfigureMain()
        {
            MissionLaunchOptions.Clear();
            MissionLaunchOptions.FastForward = true;
            MissionLaunchOptions.Faults.Add(new FaultInjectionPoint(FaultKind.RecognitionFailed, "T1", 1, -1, -1));
            MissionLaunchOptions.Started = OnMainStarted;
            MissionLaunchOptions.Completed = OnMainCompleted;
            _consistencySamples = 0;
            _persistentMismatches = 0;
            _mismatchExample = "";
            _mainPoseOpened = _mainPoseClosed = _initialPoseLogged = false;
            _hudShotDone = _monitorShotDone = _poseShotDone = _monitorArmed = false;
            _mainTriggered = 0;
            PendingMismatch.Clear();
            MainLog.Clear();
        }

        private static void OnMainStarted(RobotRuntime runtime, MissionRoutine mission)
        {
            _mission = mission;
            _runtime = runtime;
            _realStart = EditorApplication.timeSinceStartup;
            UiPerfProbe.Reset();
            SimLog.EntryLogged -= OnMainLog;
            SimLog.EntryLogged += OnMainLog;
            mission.OperatorPose.Opened += _ => _mainPoseOpened = true;
            mission.OperatorPose.Closed += _ => _mainPoseClosed = true;
            runtime.Bus.Subscribe<CellStateChangedEvent>(OnMainCell);
        }

        private static void OnMainLog(SimLogEntry e)
        {
            if (e.Message.Contains("[模拟操作员] 初值") || e.Message.Contains("[操作员] 初值")) _initialPoseLogged = true;
            if (e.Message.Contains("t(1,8)") || e.Message.Contains("操作员")) MainLog.Add($"[{e.SimTimeS:0.0} s] {e.Message}");
        }

        private static void OnMainCell(CellStateChangedEvent e)
        {
            if (e.State == CellState.Triggered) _mainTriggered++;
            MissionCursor c = _mission.Orchestrator.Cursor;
            // 截图 3（场景内显示器）：第 1 工位第 3 颗进入 T4 时切到演示台机位，T5 接触采集开始时暂停。
            if (!_monitorShotDone && c.StationGlobalIndex == 0 && c.RivetIndex == 2)
            {
                if (e.CellId == "t(4,1)" && e.State == CellState.Running && !_monitorArmed)
                {
                    _monitorArmed = true;
                    AppBootstrap.Active.Hub?.SetFullscreen(false);
                    SelectCamera(4);
                }
                if (_monitorArmed && e.CellId == "t(5,4)" && e.State == CellState.Running)
                {
                    _monitorShotDone = true;
                    _pendingShot = "shot_monitor";
                }
            }
            // 截图 2（HUD）：第 2 工位第 2 颗 T5 接触采集开始（显示器截图之后），自动机位（末端特写）。
            if (!_hudShotDone && _monitorShotDone && c.StationGlobalIndex == 1 && c.RivetIndex == 1 && e.CellId == "t(5,4)" && e.State == CellState.Running)
            {
                _hudShotDone = true;
                _pendingShot = "shot_hud";
            }
        }

        private static string _pendingShot;
        private static int _shotFrames;
        private static double _shotReadyAt;

        private static void SelectCamera(int preset)
        {
            var rig = AppBootstrap.Active != null ? AppBootstrap.Active.Rig : null;
            if (rig == null) return;
            if (preset == 0) rig.SetAuto(true);
            else rig.Select(preset);
        }

        private static void UpdateMain()
        {
            if (_mission == null || _mission.Orchestrator == null || AppBootstrap.Active == null) return;
            DemoConsoleView console = AppBootstrap.Active.DemoConsole;
            CheckConsistency(console);

            // 截图 4（t(1,8) 小地图点选提示）：通道打开后等提示稳定再暂停。
            if (!_poseShotDone && _mission.OperatorPose.IsOpen && AppBootstrap.Active.Hud.IsPosePromptOpen)
            {
                if (_shotFrames++ < StepSettleFrames) return;
                _poseShotDone = true;
                _shotFrames = 0;
                Pause("shot_pose");
                return;
            }
            if (_pendingShot == null) return;
            // 截图时刻先退出快进（快速流程每帧推进 0.25 s 仿真）回到正常倍速，等相机按实际时间过渡到位后再暂停编辑器。
            if (!_clockFrozen)
            {
                _savedFastForward = _runtime.FastForwardSimSecondsPerFrame;
                _runtime.FastForwardSimSecondsPerFrame = 0f;
                _clockFrozen = true;
            }
            // HUD 截图要求自动机位已结束演示台停留（实际时间 2 s；快进时阶段切换密集）。
            var rig = AppBootstrap.Active.Rig;
            if (_pendingShot == "shot_hud" && rig != null && !(rig.IsAuto && rig.Preset == HudShotPreset)) return;
            // 快进时帧率很高，相机过渡按实际时间等待。
            if (_shotReadyAt <= 0d) _shotReadyAt = EditorApplication.timeSinceStartup + CameraSettleS;
            if (EditorApplication.timeSinceStartup < _shotReadyAt) return;
            _shotReadyAt = 0d;
            string shot = _pendingShot;
            _pendingShot = null;
            _shotFrames = 0;
            Pause(shot);
        }

        private static void CheckConsistency(DemoConsoleView console)
        {
            if (!console.IsBuilt) return;
            TaskMatrixRuntime matrix = _mission.Orchestrator.Matrix;
            // 第二次取样：上一帧不一致且仿真状态未变的单元记为持续不一致（界面在 LateUpdate 刷新，同帧可能滞后一帧）。
            foreach ((string id, CellState expected) in PendingMismatch)
            {
                TaskCellElement cell = console.FindCell(id);
                if (matrix.GetState(id) == expected && cell != null && cell.State != expected)
                {
                    _persistentMismatches++;
                    if (_mismatchExample.Length == 0) _mismatchExample = $"{id} 仿真 {expected} / 界面 {cell.State}";
                }
            }
            PendingMismatch.Clear();
            foreach (CellDef def in matrix.Data.Cells)
            {
                TaskCellElement cell = console.FindCell(def.Id);
                CellState expected = matrix.GetState(def.Id);
                if (cell != null && cell.State != expected) PendingMismatch.Add((def.Id, expected));
            }
            _consistencySamples++;
        }

        private static void OnMainCompleted(MissionRunReport report)
        {
            double real = EditorApplication.timeSinceStartup - _realStart;
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# M6 演示界面验证报告");
                sb.AppendLine();
                sb.AppendLine($"生成：{DateTime.Now:yyyy-MM-dd HH:mm}（RivetSim/Verify/Run Demo UI）。");
                sb.AppendLine();
                sb.Append(SessionState.GetString(SandboxSectionKey, "## 1. Sandbox_UI\n\n（缺失）\n\n"));
                sb.AppendLine("## 2. 主场景快速流程（1 次故障注入：T1 识别失败）");
                sb.AppendLine();
                sb.AppendLine($"- 结果：{(report.Success ? "完成" : "未完成：" + report.FailureReason)}；仿真 {report.TotalS:0.0} s（建图 {report.MappingS:0.0} + 矩阵 {report.MatrixS:0.0} + 返回 {report.ReturnS:0.0}），实际 {real:0} s。");
                sb.AppendLine($"- 演示屏与仿真单元状态一致性：取样 {_consistencySamples} 帧 × 48 单元，持续不一致 {_persistentMismatches} 次{(_mismatchExample.Length > 0 ? "（例：" + _mismatchExample + "）" : "")}。");
                sb.AppendLine($"- 条件单元触发 {_mainTriggered} 次；t(1,8) 初值通道打开 {(_mainPoseOpened ? "是" : "否")} / 关闭 {(_mainPoseClosed ? "是" : "否")}，初值写入日志 {(_initialPoseLogged ? "是" : "否")}（自动演示：截图暂停后由模拟操作员 3 s 给出）。");
                foreach (string line in MainLog.Take(8)) sb.AppendLine($"  - `{line}`");
                AppendCycleTimes(sb, report.Records);
                int frames = UiPerfProbe.FrameCount;
                sb.AppendLine($"- 界面开销（快速流程，每帧推进 0.25 s 仿真，事件密集）：{frames} 帧，平均 {(frames > 0 ? (double)UiPerfProbe.FrameAllocCount / frames : 0):0.00} 次分配/帧，有分配的帧 {UiPerfProbe.FramesWithAlloc}（{(frames > 0 ? 100.0 * UiPerfProbe.FramesWithAlloc / frames : 0):0.0}%）；" +
                              $"事件处理 {UiPerfProbe.EventCount} 次，平均 {(UiPerfProbe.EventCount > 0 ? UiPerfProbe.EventTotalMs / UiPerfProbe.EventCount * 1000d : 0):0.0} µs，最长 {UiPerfProbe.EventMaxMs * 1000d:0.0} µs。");
                sb.AppendLine();
                sb.AppendLine("## 3. 截图");
                sb.AppendLine();
                sb.AppendLine("见 Docs/Screenshots/M6/：1_matrix_fullscreen（全屏矩阵）、2_scene_monitor（场景内显示器）、3_hud（HUD + 末端特写）、4_pose_prompt（t(1,8) 小地图点选提示）。");
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllText(ReportPath, sb.ToString(), new UTF8Encoding(false));
                SimLog.Info(LogCategory.System, $"M6 验证：报告已写入 {ReportPath}");
            }
            catch (Exception ex)
            {
                SimLog.Error(LogCategory.System, $"M6 验证：写报告失败：{ex.Message}");
            }
            SimLog.EntryLogged -= OnMainLog;
            SessionState.SetString(StageKey, "done");
            EditorApplication.delayCall += () => EditorApplication.ExitPlaymode();
        }

        private static void AppendCycleTimes(StringBuilder sb, IReadOnlyList<InspectionRecord> records)
        {
            if (records == null || records.Count == 0) return;
            var byStation = records.GroupBy(r => r.StationId).ToList();
            var last = byStation.Select(g => g.Last().CycleTimeS).ToList();
            var others = byStation.SelectMany(g => g.Take(g.Count() - 1)).Select(r => r.CycleTimeS).ToList();
            sb.AppendLine($"- 单钉节拍（M5 遗留，已扣除工位最后一颗的收臂与降柱）：非末颗平均 {(others.Count > 0 ? others.Average() : 0):0.0} s；" +
                          $"各工位末颗 {string.Join(" / ", last.Select(v => v.ToString("0.0")))} s。");
        }
    }
}
