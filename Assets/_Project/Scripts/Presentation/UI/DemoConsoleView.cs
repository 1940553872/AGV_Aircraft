using System;
using System.Collections.Generic;
using RivetSim.Core.Config;
using RivetSim.Core.Events;
using RivetSim.Core.Logging;
using RivetSim.Core.Mission;
using RivetSim.Core.TaskMatrix;
using RivetSim.Simulation.Inspection;
using UnityEngine;
using UnityEngine.UIElements;

namespace RivetSim.Presentation.UI
{
    /// <summary>
    /// 演示电脑屏幕（M6，Architecture §7）：顶栏、6×8 任务矩阵（文字、类型、回路一律取自 JSON）、回路箭头、图例、
    /// 当前阶段卡片、当前执行情况说明（含 C2～C8 / C9～C12 逐条 ✓ / ✗）、小地图、A 扫波形与底栏日志。
    /// 只订阅 M5 事件（CellStateChanged / ConditionChecked / PhaseEntered / LoopTaken / RivetInspected）与 SimLog，
    /// 事件处理只改模型与脏标记，LateUpdate 按脏标记刷新，空闲帧不分配内存。
    /// 平时渲染到 RenderTexture（场景内显示器与大屏，不接收输入）；按 Tab 切到屏幕全屏，此时可点击单元查看说明卡。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class DemoConsoleView : MonoBehaviour
    {
        private const int MaxConditionLines = 12;
        private static readonly string[] StateNames = { "未开始", "待命", "已布防", "正在执行", "已完成", "已跳过", "已触发", "失败" };
        private static readonly Vector2 NoPanelPosition = new Vector2(float.NaN, float.NaN);

        [SerializeField, Tooltip("渲染到 RenderTexture 的面板（场景内显示器）")] private PanelSettings screenTexturePanel;
        [SerializeField, Tooltip("全屏叠加面板（Tab 切换，可交互）")] private PanelSettings fullscreenPanel;

        private readonly Dictionary<string, CellState> _states = new Dictionary<string, CellState>();
        private readonly Dictionary<string, TaskCellElement> _cells = new Dictionary<string, TaskCellElement>();
        private readonly List<TaskCellElement> _cellList = new List<TaskCellElement>();
        private readonly List<VisualElement> _rowHeaders = new List<VisualElement>();
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private readonly Queue<string> _dirtyCells = new Queue<string>();

        private UIDocument _document;
        private EventBus _bus;
        private TaskMatrixData _data;
        private IDemoStatusSource _status;
        private IMiniMapSource _map;
        private InspectionConfig _inspection;
        private UiThemeConfig _theme;
        private VisualElement _root;

        private Label _mode, _speed, _time, _progress, _phaseCode, _phaseName, _phaseDetail, _condTitle, _verdict, _loopNote;
        private VisualElement _detailScrim;
        private Label _detailTitle, _detailMeta, _detailExplain, _detailExtra;
        private LoopArrowElement _loops;
        private MiniMapElement _miniMap;
        private AScanGraphElement _ascan;
        private string[] _actorTags;

        // 执行情况说明与条件行（对象池，创建一次）。
        private VisualElement[] _explainLines;
        private Label[] _explainTags, _explainTexts;
        private VisualElement[] _condLines;
        private Label[] _condStatus, _condIds, _condTexts, _condValues;

        // 模型。
        private int _phaseRow;
        private string _phaseId = "";
        private int _attempt;
        private int _region = -1, _station = -1, _rivet = -1;
        private bool _phaseDirty = true, _explainDirty = true, _progressDirty = true;
        private string _condCellId;
        private readonly ConditionItem[] _condItems = new ConditionItem[MaxConditionLines];
        private readonly sbyte[] _condResult = new sbyte[MaxConditionLines];
        private readonly string[] _condValueText = new string[MaxConditionLines];
        private int _condCount;
        private bool _condDirty = true;
        private readonly float[] _loopHighlightUntil = new float[8];
        private RivetInspectedEvent _lastInspection;
        private bool _inspectionDirty;

        // 日志环。
        private SimLogEntry[] _logEntries;
        private string[] _logTexts;
        private string[] _logClasses;
        private Label[] _logLabels;
        private int _logCount, _logRendered;

        // 顶栏缓存。
        private long _shownSecond = -1;
        private float _shownSpeed = -1f;
        private string _shownMode;
        private bool _shownPaused;

        /// <summary>是否已全屏（可交互）。</summary>
        public bool IsFullscreen { get; private set; }

        /// <summary>界面元素是否已建立。</summary>
        public bool IsBuilt => _root != null && _cellList.Count > 0;

        /// <summary>界面根元素（验证用）。</summary>
        public VisualElement Root => _root;

        /// <summary>当前条件行数（验证用）。</summary>
        public int ConditionLineCount => _condCount;

        /// <summary>当前条件所属单元（验证用）。</summary>
        public string ConditionCellId => _condCellId;

        /// <summary>编辑器生成器写入面板设置。</summary>
        public void Configure(PanelSettings texturePanel, PanelSettings fullscreen)
        {
            screenTexturePanel = texturePanel;
            fullscreenPanel = fullscreen;
        }

        /// <summary>
        /// 绑定数据。initialState 为空时所有单元按 Idle 开始（随后由事件更新）。
        /// </summary>
        public void Initialize(EventBus bus, TaskMatrixData data, Func<string, CellState> initialState, IDemoStatusSource status,
            IMiniMapSource map, InspectionConfig inspection, UiThemeConfig theme)
        {
            _document = GetComponent<UIDocument>();
            _bus = bus;
            _data = data;
            _status = status;
            _map = map;
            _inspection = inspection;
            _theme = theme;
            foreach (CellDef cell in data.Cells)
                _states[cell.Id] = initialState != null ? initialState(cell.Id) : CellState.Idle;
            int logCapacity = Mathf.Max(1, theme.logVisibleCount);
            _logEntries = new SimLogEntry[logCapacity];
            _logTexts = new string[logCapacity];
            _logClasses = new string[logCapacity];
            _actorTags = new string[data.ColCount];
            for (int c = 0; c < data.ColCount; c++) _actorTags[c] = data.Actors[c].Code + " " + data.Actors[c].Name;

            _subscriptions.Add(bus.Subscribe<CellStateChangedEvent>(OnCellState));
            _subscriptions.Add(bus.Subscribe<ConditionCheckedEvent>(OnCondition));
            _subscriptions.Add(bus.Subscribe<PhaseEnteredEvent>(OnPhase));
            _subscriptions.Add(bus.Subscribe<LoopTakenEvent>(OnLoop));
            _subscriptions.Add(bus.Subscribe<RivetInspectedEvent>(OnInspected));
            SimLog.EntryLogged += OnLog;

            if (screenTexturePanel != null)
            {
                // 渲染到贴图的面板不接收屏幕输入，避免与 HUD 抢点击。
                screenTexturePanel.SetScreenToPanelSpaceFunction(_ => NoPanelPosition);
                _document.panelSettings = screenTexturePanel;
            }
        }

        /// <summary>切换全屏（Tab）。全屏时可点击单元弹出说明卡。</summary>
        public void SetFullscreen(bool fullscreen)
        {
            if (IsFullscreen == fullscreen || _document == null) return;
            IsFullscreen = fullscreen;
            PanelSettings target = fullscreen ? fullscreenPanel : screenTexturePanel;
            if (target != null) _document.panelSettings = target;
            if (!fullscreen) HideDetail();
            ApplyFullscreenHint();
        }

        /// <summary>查找单元元素（验证用）。</summary>
        public TaskCellElement FindCell(string cellId) => _cells.TryGetValue(cellId ?? "", out TaskCellElement c) ? c : null;

        /// <summary>条件行文字（验证用）：“C2 ✓ 文本 数值”。</summary>
        public string ConditionLineText(int index) =>
            index < 0 || index >= _condCount ? "" : $"{_condIds[index].text} {_condStatus[index].text} {_condTexts[index].text} {_condValues[index].text}";

        /// <summary>显示单元说明卡（全屏点击单元，或验证时调用）。</summary>
        public void ShowDetail(string cellId)
        {
            CellDef cell = _data?.FindCell(cellId);
            if (cell == null || _detailScrim == null) return;
            ActorDef actor = cell.Col >= 1 && cell.Col <= _data.ColCount ? _data.Actors[cell.Col - 1] : null;
            PhaseDef phase = cell.Row >= 1 && cell.Row <= _data.RowCount ? _data.Phases[cell.Row - 1] : null;
            _detailTitle.text = $"{cell.Id}  {cell.Label.Replace('\n', ' ')}";
            string kind = cell.Kind == CellKind.Conditional ? "条件触发" : cell.Kind == CellKind.Standby ? "待命" : "执行";
            _detailMeta.text = $"阶段 {phase?.Code} {phase?.Name} · 主体 {actor?.Code} {actor?.Name}（{actor?.Device}）\n" +
                               $"类型 {kind} · 执行器 {cell.Executor} · 当前 {StateNames[(int)GetState(cell.Id)]}";
            _detailExplain.text = string.IsNullOrEmpty(cell.Explain) ? "—" : cell.Explain;
            var extra = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(cell.Trigger)) extra.Append("触发：").Append(cell.Trigger).Append('\n');
            if (!string.IsNullOrEmpty(cell.RunWhen)) extra.Append("执行条件：").Append(cell.RunWhen).Append('\n');
            foreach (string id in cell.ConditionRefs)
            {
                ConditionItem item = _data.FindCondition(id);
                if (item != null) extra.Append(item.Id).Append(' ').Append(item.Text).Append('\n');
            }
            if (!string.IsNullOrEmpty(cell.Note)) extra.Append("备注：").Append(cell.Note);
            _detailExtra.text = extra.ToString().TrimEnd('\n');
            _detailExtra.style.display = extra.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _detailScrim.style.display = DisplayStyle.Flex;
        }

        private void ApplyFullscreenHint()
        {
            Label hint = _root?.Q<Label>("dc-fullscreen-hint");
            if (hint != null) hint.style.display = IsFullscreen ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>关闭说明卡。</summary>
        public void HideDetail()
        {
            if (_detailScrim != null) _detailScrim.style.display = DisplayStyle.None;
        }

        private CellState GetState(string id) => _states.TryGetValue(id, out CellState s) ? s : CellState.Idle;

        // —— 事件（只改模型） ——

        private void OnCellState(CellStateChangedEvent e)
        {
            UiPerfProbe.BeginEvent();
            _states[e.CellId] = e.State;
            _dirtyCells.Enqueue(e.CellId);
            SetCursor(e.RegionIndex, e.StationIndex, e.RivetIndex);
            CellDef cell = _data.FindCell(e.CellId);
            if (cell != null)
            {
                if (cell.Row == _phaseRow) _explainDirty = true;
                if (cell.ConditionRefs.Count > 0 && (e.State == CellState.Running || e.State == CellState.Triggered) && _condCellId != cell.Id)
                    BeginConditions(cell);
            }
            UiPerfProbe.EndEvent();
        }

        private void OnCondition(ConditionCheckedEvent e)
        {
            UiPerfProbe.BeginEvent();
            if (_condCellId != e.CellId)
            {
                CellDef cell = _data.FindCell(e.CellId);
                if (cell != null) BeginConditions(cell);
            }
            for (int i = 0; i < _condCount; i++)
            {
                if (_condItems[i].Id != e.ConditionId) continue;
                _condResult[i] = (sbyte)(e.Passed ? 1 : -1);
                _condValueText[i] = e.ValueText ?? "";
                _condDirty = true;
                break;
            }
            UiPerfProbe.EndEvent();
        }

        private void OnPhase(PhaseEnteredEvent e)
        {
            UiPerfProbe.BeginEvent();
            int row = _data.RowOfPhase(e.PhaseId);
            // 回到更早的阶段（下一颗 / 换站）时清除上一轮的条件列表。
            if (_condCellId != null)
            {
                CellDef condCell = _data.FindCell(_condCellId);
                if (condCell != null && row < condCell.Row)
                {
                    _condCellId = null;
                    _condCount = 0;
                    _condDirty = true;
                }
            }
            _phaseRow = row;
            _phaseId = e.PhaseId;
            _attempt = e.Attempt;
            SetCursor(e.RegionIndex, e.StationIndex, e.RivetIndex);
            _phaseDirty = true;
            _explainDirty = true;
            UiPerfProbe.EndEvent();
        }

        private void OnLoop(LoopTakenEvent e)
        {
            UiPerfProbe.BeginEvent();
            if (_loops != null)
            {
                int index = _loops.IndexOf(e.LoopId);
                if (index >= 0 && index < _loopHighlightUntil.Length) _loopHighlightUntil[index] = Time.unscaledTime + _theme.loopHighlightS;
            }
            UiPerfProbe.EndEvent();
        }

        private void OnInspected(RivetInspectedEvent e)
        {
            UiPerfProbe.BeginEvent();
            _lastInspection = e;
            _inspectionDirty = true;
            UiPerfProbe.EndEvent();
        }

        private void OnLog(SimLogEntry entry)
        {
            if (_logEntries == null) return;
            _logEntries[_logCount % _logEntries.Length] = entry;
            _logCount++;
        }

        private void SetCursor(int region, int station, int rivet)
        {
            if (region == _region && station == _station && rivet == _rivet) return;
            _region = region;
            _station = station;
            _rivet = rivet;
            _progressDirty = true;
            _phaseDirty = true;
        }

        private void BeginConditions(CellDef cell)
        {
            _condCellId = cell.Id;
            _condCount = 0;
            foreach (string id in cell.ConditionRefs)
            {
                ConditionItem item = _data.FindCondition(id);
                if (item == null || _condCount >= MaxConditionLines) continue;
                _condItems[_condCount] = item;
                _condResult[_condCount] = 0;
                _condValueText[_condCount] = "";
                _condCount++;
            }
            _condDirty = true;
        }

        // —— 刷新 ——

        private void LateUpdate()
        {
            if (_data == null) return;
            UiPerfProbe.BeginFrame();
            if (_document.rootVisualElement != _root || _cellList.Count == 0) Build();
            if (_root != null)
            {
                float now = Time.unscaledTime;
                while (_dirtyCells.Count > 0)
                {
                    string id = _dirtyCells.Dequeue();
                    if (_cells.TryGetValue(id, out TaskCellElement cell)) cell.SetState(GetState(id), now);
                }
                for (int i = 0; i < _cellList.Count; i++)
                    if (_cellList[i].IsAnimating) _cellList[i].Tick(now);
                RenderTopBar();
                if (_phaseDirty) RenderPhase();
                if (_explainDirty) RenderExplain();
                if (_condDirty) RenderConditions();
                if (_inspectionDirty) RenderInspection();
                RenderLoops(now);
                RenderLog();
                _miniMap?.Tick();
            }
            UiPerfProbe.EndFrame();
        }

        private void RenderTopBar()
        {
            if (_status == null) return;
            if (_progressDirty)
            {
                _progressDirty = false;
                int regions = _status.RegionCount;
                int stations = _region >= 0 ? _status.StationCount(_region) : 0;
                int rivets = _status.RivetCount;
                _progress.text = _region < 0
                    ? "区域 —/— · 工位 —/— · 铆钉 —/—"
                    : $"区域 {_region + 1}/{regions} · 工位 {_station + 1}/{stations} · 铆钉 {_rivet + 1}/{rivets}";
            }
            long second = (long)_status.SimTimeS;
            if (second != _shownSecond)
            {
                _shownSecond = second;
                _time.text = $"{second / 3600:00}:{second / 60 % 60:00}:{second % 60:00}";
            }
            if (!Mathf.Approximately(_status.SpeedMultiplier, _shownSpeed))
            {
                _shownSpeed = _status.SpeedMultiplier;
                _speed.text = $"×{_shownSpeed:0.##}";
            }
            if (!ReferenceEquals(_status.ModeText, _shownMode) || _status.IsPaused != _shownPaused)
            {
                _shownMode = _status.ModeText;
                _shownPaused = _status.IsPaused;
                _mode.text = _shownPaused ? _shownMode + " · 已暂停" : _shownMode;
            }
        }

        private void RenderPhase()
        {
            _phaseDirty = false;
            PhaseDef phase = _data.FindPhase(_phaseId);
            for (int i = 0; i < _rowHeaders.Count; i++) _rowHeaders[i].EnableInClassList("mx-row-header--current", i + 1 == _phaseRow);
            if (phase == null) return;
            _phaseCode.text = phase.Code;
            _phaseName.text = phase.Name;
            int rivets = _status != null ? _status.RivetCount : 0;
            _phaseDetail.text = $"区域 {_region + 1} · 工位 {_station + 1} · 铆钉 {_rivet + 1}/{rivets}" + (_attempt > 1 ? $" · 第 {_attempt} 次" : "");
        }

        private void RenderExplain()
        {
            _explainDirty = false;
            int shown = 0;
            if (_phaseRow >= 1)
                for (int c = 1; c <= _data.ColCount && shown < _explainLines.Length; c++)
                {
                    CellDef cell = _data.GetCell(_phaseRow, c);
                    if (cell == null) continue;
                    CellState s = GetState(cell.Id);
                    if (s != CellState.Running && s != CellState.Triggered && s != CellState.Failed && s != CellState.Done) continue;
                    VisualElement line = _explainLines[shown];
                    line.style.display = DisplayStyle.Flex;
                    line.EnableInClassList("ex-line--running", s == CellState.Running);
                    line.EnableInClassList("ex-line--triggered", s == CellState.Triggered);
                    line.EnableInClassList("ex-line--done", s == CellState.Done);
                    line.EnableInClassList("ex-line--failed", s == CellState.Failed);
                    _explainTags[shown].text = _actorTags[c - 1];
                    _explainTexts[shown].text = string.IsNullOrEmpty(cell.Explain) ? cell.Label : cell.Explain;
                    shown++;
                }
            for (int i = shown; i < _explainLines.Length; i++) _explainLines[i].style.display = DisplayStyle.None;
        }

        private void RenderConditions()
        {
            _condDirty = false;
            bool any = _condCount > 0;
            _condTitle.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
            if (any) _condTitle.text = $"{_condCellId} 条件判定（{_condItems[0].Id}～{_condItems[_condCount - 1].Id}，只上报不阻断）";
            for (int i = 0; i < _condLines.Length; i++)
            {
                if (i >= _condCount)
                {
                    _condLines[i].style.display = DisplayStyle.None;
                    continue;
                }
                sbyte r = _condResult[i];
                _condLines[i].style.display = DisplayStyle.Flex;
                _condLines[i].EnableInClassList("cd-line--pass", r > 0);
                _condLines[i].EnableInClassList("cd-line--fail", r < 0);
                _condStatus[i].text = r > 0 ? "✓" : r < 0 ? "✗" : "…";
                _condIds[i].text = _condItems[i].Id;
                _condTexts[i].text = _condItems[i].Text;
                _condValues[i].text = _condValueText[i];
            }
        }

        private void RenderInspection()
        {
            _inspectionDirty = false;
            InspectionRecord r = _lastInspection.Record;
            if (r == null) return;
            if (_lastInspection.Waveform != null && _inspection != null)
                _ascan.SetData(_lastInspection.Waveform, r.Gates, _inspection.gateAThresholdRatio, _inspection.gateBThresholdRatio, _inspection.backwallDefectRatio);
            _verdict.EnableInClassList("dc-verdict--pass", r.Verdict == InspectionVerdict.Pass);
            _verdict.EnableInClassList("dc-verdict--suspect", r.Verdict == InspectionVerdict.Suspect);
            _verdict.EnableInClassList("dc-verdict--defect", r.Verdict == InspectionVerdict.Defect);
            _verdict.EnableInClassList("dc-verdict--invalid", r.Verdict == InspectionVerdict.Invalid);
            _verdict.text = $"{r.RivetId}：{RivetInspectionService.Label(r.Verdict)}——{r.Reason}";
        }

        private void RenderLoops(float now)
        {
            if (_loops == null) return;
            for (int i = 0; i < _loops.Count && i < _loopHighlightUntil.Length; i++) _loops.SetHighlight(i, now < _loopHighlightUntil[i]);
        }

        private void RenderLog()
        {
            if (_logRendered == _logCount) return;
            int capacity = _logEntries.Length;
            int first = Mathf.Max(_logRendered, _logCount - capacity);
            for (int n = first; n < _logCount; n++)
            {
                SimLogEntry e = _logEntries[n % capacity];
                _logTexts[n % capacity] = $"[{e.SimTimeS,7:0.0} s] {e.Message}";
                _logClasses[n % capacity] = e.Level == SimLogLevel.Error ? "lg-line--error"
                    : e.Level == SimLogLevel.Warning ? "lg-line--warning"
                    : e.Category == LogCategory.Mission ? "lg-line--mission"
                    : e.Category == LogCategory.Nav ? "lg-line--nav"
                    : e.Category == LogCategory.Arm ? "lg-line--arm"
                    : e.Category == LogCategory.UT ? "lg-line--ut"
                    : e.Category == LogCategory.Safety ? "lg-line--safety" : "lg-line--system";
            }
            _logRendered = _logCount;
            int visible = Mathf.Min(capacity, _logCount);
            for (int i = 0; i < _logLabels.Length; i++)
            {
                Label label = _logLabels[i];
                if (i >= visible)
                {
                    label.text = "";
                    continue;
                }
                int n = _logCount - visible + i;
                label.text = _logTexts[n % capacity];
                string cls = _logClasses[n % capacity];
                if (label.userData as string != cls)
                {
                    if (label.userData is string old) label.RemoveFromClassList(old);
                    label.AddToClassList(cls);
                    label.userData = cls;
                }
            }
        }

        // —— 建立界面（首次与面板切换后） ——

        private void Build()
        {
            VisualElement root = _document.rootVisualElement;
            if (root == null || root.Q("dc-matrix") == null) return;
            if (root == _root && _cellList.Count > 0) return;
            _root = root;
            _cells.Clear();
            _cellList.Clear();
            _rowHeaders.Clear();

            _mode = root.Q<Label>("dc-mode");
            _speed = root.Q<Label>("dc-speed");
            _time = root.Q<Label>("dc-time");
            _progress = root.Q<Label>("dc-progress");
            _phaseCode = root.Q<Label>("dc-phase-code");
            _phaseName = root.Q<Label>("dc-phase-name");
            _phaseDetail = root.Q<Label>("dc-phase-detail");
            _condTitle = root.Q<Label>("dc-cond-title");
            _verdict = root.Q<Label>("dc-verdict");
            _loopNote = root.Q<Label>("dc-loopnote");
            _detailScrim = root.Q("dc-detail-scrim");
            _detailTitle = root.Q<Label>("dc-detail-title");
            _detailMeta = root.Q<Label>("dc-detail-meta");
            _detailExplain = root.Q<Label>("dc-detail-explain");
            _detailExtra = root.Q<Label>("dc-detail-extra");
            _detailScrim.RegisterCallback<PointerDownEvent>(_ => HideDetail());
            root.Q<Label>("dc-title").text = _data.Title + " · 演示";
            _loopNote.text = _data.LoopNote;

            BuildMatrix(root.Q("dc-matrix"), root.Q("dc-loops-host"));
            BuildLegend(root.Q("dc-legend"));
            BuildExplainPools(root.Q("dc-explain"), root.Q("dc-conditions"));
            BuildLog(root.Q("dc-log"));

            VisualElement mapHost = root.Q("dc-minimap-host");
            mapHost.Clear();
            _miniMap = new MiniMapElement { pickingMode = PickingMode.Ignore };
            mapHost.Add(_miniMap);
            if (_map != null) _miniMap.SetSource(_map, _theme.miniMapRepaintThresholdPx, _theme.miniMapAgvMarkerSizePx);
            VisualElement ascanHost = root.Q("dc-ascan-host");
            ascanHost.Clear();
            _ascan = new AScanGraphElement();
            _ascan.AddToClassList("dc-ascan-graph");
            ascanHost.Add(_ascan);

            float now = Time.unscaledTime;
            foreach (TaskCellElement cell in _cellList) cell.SetState(GetState(cell.Cell.Id), now);
            _dirtyCells.Clear();
            _phaseDirty = _explainDirty = _condDirty = _progressDirty = true;
            _inspectionDirty = _lastInspection.Record != null;
            _shownSecond = -1;
            _shownSpeed = -1f;
            _shownMode = null;
            _logRendered = Mathf.Max(0, _logCount - _logEntries.Length);
            ApplyFullscreenHint();
        }

        private void BuildMatrix(VisualElement host, VisualElement loopsHost)
        {
            host.Clear();
            var header = new VisualElement { pickingMode = PickingMode.Ignore };
            header.AddToClassList("mx-row");
            header.AddToClassList("mx-header-row");
            var corner = new VisualElement { pickingMode = PickingMode.Ignore };
            corner.AddToClassList("mx-corner");
            var cornerText = new Label(_data.Notation) { pickingMode = PickingMode.Ignore };
            cornerText.AddToClassList("mx-corner-text");
            corner.Add(cornerText);
            header.Add(corner);
            foreach (ActorDef actor in _data.Actors) header.Add(HeadCell("mx-col-header", actor.Code, actor.Name));
            host.Add(header);

            for (int r = 1; r <= _data.RowCount; r++)
            {
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.AddToClassList("mx-row");
                PhaseDef phase = _data.Phases[r - 1];
                VisualElement rowHeader = HeadCell("mx-row-header", phase.Code, phase.Name);
                _rowHeaders.Add(rowHeader);
                row.Add(rowHeader);
                for (int c = 1; c <= _data.ColCount; c++)
                {
                    CellDef def = _data.GetCell(r, c);
                    if (def == null)
                    {
                        var empty = new VisualElement { pickingMode = PickingMode.Ignore };
                        empty.AddToClassList("cell");
                        row.Add(empty);
                        continue;
                    }
                    var cell = new TaskCellElement(def, _theme);
                    cell.Clicked += OnCellClicked;
                    _cells[def.Id] = cell;
                    _cellList.Add(cell);
                    row.Add(cell);
                }
                host.Add(row);
            }

            loopsHost.Clear();
            _loops = new LoopArrowElement(_data, _theme.accent, _theme.dashPatternPx);
            _loops.RowCenterY = RowCenterY;
            loopsHost.Add(_loops);
            host.RegisterCallback<GeometryChangedEvent>(_ => _loops.Relayout());
        }

        private float RowCenterY(int row)
        {
            if (row < 1 || row > _rowHeaders.Count || _loops == null) return 0f;
            return _rowHeaders[row - 1].worldBound.center.y - _loops.worldBound.y;
        }

        private static VisualElement HeadCell(string cls, string code, string name)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.AddToClassList(cls);
            var codeLabel = new Label(code) { pickingMode = PickingMode.Ignore };
            codeLabel.AddToClassList("mx-head-code");
            e.Add(codeLabel);
            var nameLabel = new Label(name) { pickingMode = PickingMode.Ignore };
            nameLabel.AddToClassList("mx-head-name");
            e.Add(nameLabel);
            return e;
        }

        private void BuildLegend(VisualElement host)
        {
            host.Clear();
            foreach (LegendItem item in _data.Legend)
            {
                var entry = new VisualElement { pickingMode = PickingMode.Ignore };
                entry.AddToClassList("lg-item");
                var swatch = new VisualElement { pickingMode = PickingMode.Ignore };
                swatch.AddToClassList("lg-swatch");
                swatch.AddToClassList("lg-swatch--" + item.Style);
                if (item.Style == "dashed")
                {
                    var dashed = new DashedBorderElement();
                    dashed.SetPattern(_theme.dashPatternPx.x, _theme.dashPatternPx.y);
                    dashed.SetStroke(_theme.textSecondary, 1.5f);
                    swatch.Add(dashed);
                }
                else if (item.Style == "grayText")
                {
                    var t = new Label("待命") { pickingMode = PickingMode.Ignore };
                    t.AddToClassList("lg-swatch-text");
                    swatch.Add(t);
                }
                entry.Add(swatch);
                var text = new Label(item.Text) { pickingMode = PickingMode.Ignore };
                text.AddToClassList("lg-text");
                entry.Add(text);
                host.Add(entry);
            }
        }

        private void BuildExplainPools(VisualElement explainHost, VisualElement condHost)
        {
            explainHost.Clear();
            int n = _data.ColCount;
            _explainLines = new VisualElement[n];
            _explainTags = new Label[n];
            _explainTexts = new Label[n];
            for (int i = 0; i < n; i++)
            {
                var line = new VisualElement { pickingMode = PickingMode.Ignore };
                line.AddToClassList("ex-line");
                _explainTags[i] = new Label { pickingMode = PickingMode.Ignore };
                _explainTags[i].AddToClassList("ex-tag");
                _explainTexts[i] = new Label { pickingMode = PickingMode.Ignore };
                _explainTexts[i].AddToClassList("ex-text");
                line.Add(_explainTags[i]);
                line.Add(_explainTexts[i]);
                line.style.display = DisplayStyle.None;
                _explainLines[i] = line;
                explainHost.Add(line);
            }

            condHost.Clear();
            _condLines = new VisualElement[MaxConditionLines];
            _condStatus = new Label[MaxConditionLines];
            _condIds = new Label[MaxConditionLines];
            _condTexts = new Label[MaxConditionLines];
            _condValues = new Label[MaxConditionLines];
            for (int i = 0; i < MaxConditionLines; i++)
            {
                var line = new VisualElement { pickingMode = PickingMode.Ignore };
                line.AddToClassList("cd-line");
                _condStatus[i] = PoolLabel("cd-status", line);
                _condIds[i] = PoolLabel("cd-id", line);
                _condTexts[i] = PoolLabel("cd-text", line);
                _condValues[i] = PoolLabel("cd-value", line);
                line.style.display = DisplayStyle.None;
                _condLines[i] = line;
                condHost.Add(line);
            }
        }

        private void BuildLog(VisualElement host)
        {
            host.Clear();
            _logLabels = new Label[_logEntries.Length];
            for (int i = 0; i < _logLabels.Length; i++) _logLabels[i] = PoolLabel("lg-line", host);
        }

        private static Label PoolLabel(string cls, VisualElement parent)
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList(cls);
            parent.Add(label);
            return label;
        }

        private void OnCellClicked(TaskCellElement cell)
        {
            if (IsFullscreen) ShowDetail(cell.Cell.Id);
        }

        private void OnDestroy()
        {
            foreach (IDisposable s in _subscriptions) s.Dispose();
            _subscriptions.Clear();
            SimLog.EntryLogged -= OnLog;
        }
    }
}
