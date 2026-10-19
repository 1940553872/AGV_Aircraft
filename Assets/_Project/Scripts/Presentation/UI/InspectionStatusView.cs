using System.Collections.Generic;
using RivetSim.Core.Config;
using RivetSim.Simulation.Inspection;
using RivetSim.Simulation.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace RivetSim.Presentation.UI
{
    /// <summary>
    /// “检测结果与设备状态”视图（M8，演示屏 UXML 中的 dc-status-page，由 <see cref="DemoConsoleView"/> 驱动）：
    /// 左侧为不合格铆钉列表（疑似 / 缺陷 / 无效，最新在上）与合格 / 不合格 / 无效统计，数据只来自 <see cref="RivetInspectedEvent"/>；
    /// 右侧为 AGV 设备状态（位姿、速度、当前任务单元、升降柱、机械臂、联锁、电量、通讯延迟），只读 <see cref="IDeviceStatusSource"/>。
    /// 事件只改模型，按 CFG_UiTheme.statusRefreshIntervalS（实际时间）在可见时刷新。
    /// </summary>
    public sealed class InspectionStatusView
    {
        private const string BadgeOnClass = "st-badge--on";
        private const string BadgeOffClass = "st-badge--off";
        private static readonly string[] DeviceKeys = { "位姿", "速度", "当前任务单元", "升降柱伸出", "机械臂", "安全联锁", "电量", "通讯延迟" };
        private const int RowInterlock = 5;
        private const int RowBattery = 6;

        private readonly UiThemeConfig _theme;
        private readonly List<InspectionRecord> _records = new List<InspectionRecord>();
        private readonly List<string> _places = new List<string>();
        private IDeviceStatusSource _device;
        private int _pass, _nonPass, _invalid;
        private string _currentCell = "—";
        private bool _resultsDirty = true;
        private float _nextRefresh;

        private Label _countTotal, _countPass, _countFail, _countInvalid, _empty;
        private VisualElement[] _rows;
        private Label[][] _cells;
        private Label[] _deviceValues;
        private Label[] _badges;
        private VisualElement _batteryFill;

        /// <summary>创建。</summary>
        public InspectionStatusView(UiThemeConfig theme) => _theme = theme;

        /// <summary>界面是否已建立。</summary>
        public bool IsBuilt => _rows != null;

        /// <summary>已检测铆钉数。</summary>
        public int InspectedCount => _pass + _nonPass + _invalid;

        /// <summary>不合格（疑似 + 缺陷）数。</summary>
        public int NonPassCount => _nonPass;

        /// <summary>无效数。</summary>
        public int InvalidCount => _invalid;

        /// <summary>列表当前显示的行数（验证用）。</summary>
        public int ShownRowCount => Mathf.Min(_records.Count, _rows?.Length ?? 0);

        /// <summary>设备状态数据源（可为空：显示“—”）。</summary>
        public void SetDeviceSource(IDeviceStatusSource device) => _device = device;

        /// <summary>收到一颗铆钉的检测结果。</summary>
        public void OnInspected(RivetInspectedEvent e)
        {
            InspectionRecord r = e.Record;
            if (r == null) return;
            if (r.Verdict == InspectionVerdict.Pass)
            {
                _pass++;
                _resultsDirty = true;
                return;
            }
            if (r.Verdict == InspectionVerdict.Invalid) _invalid++;
            else _nonPass++;
            int capacity = Mathf.Max(1, _theme.statusListRows);
            _records.Insert(0, r);
            _places.Insert(0, e.Rivet != null ? $"区域 {e.Rivet.RegionIndex + 1} / 工位 {e.Rivet.StationIndex + 1}" : r.StationId);
            if (_records.Count > capacity)
            {
                _records.RemoveAt(_records.Count - 1);
                _places.RemoveAt(_places.Count - 1);
            }
            _resultsDirty = true;
        }

        /// <summary>当前正在执行的任务单元（单元 id 与单行文字）。</summary>
        public void SetCurrentCell(string cellId, string label) => _currentCell = $"{cellId} {label}";

        /// <summary>在页面根元素下建立表格与设备状态行（面板切换后重新调用）。</summary>
        public void Build(VisualElement page)
        {
            _countTotal = page.Q<Label>("st-count-total");
            _countPass = page.Q<Label>("st-count-pass");
            _countFail = page.Q<Label>("st-count-fail");
            _countInvalid = page.Q<Label>("st-count-invalid");
            _empty = page.Q<Label>("st-empty");

            VisualElement table = page.Q("st-table");
            table.Clear();
            string[] headers = { "铆钉 ID", "区域 / 工位", "结论", "缺陷类型", "接触力", "耦合质量", "时间" };
            VisualElement head = Row(table, "st-row st-row--head");
            for (int c = 0; c < headers.Length; c++) Cell(head, headers[c], "st-cell st-col-" + c);
            int n = Mathf.Max(1, _theme.statusListRows);
            _rows = new VisualElement[n];
            _cells = new Label[n][];
            for (int i = 0; i < n; i++)
            {
                _rows[i] = Row(table, "st-row");
                _cells[i] = new Label[headers.Length];
                for (int c = 0; c < headers.Length; c++) _cells[i][c] = Cell(_rows[i], "", "st-cell st-col-" + c);
                _rows[i].style.display = DisplayStyle.None;
            }

            VisualElement device = page.Q("st-device");
            device.Clear();
            _deviceValues = new Label[DeviceKeys.Length];
            for (int i = 0; i < DeviceKeys.Length; i++)
            {
                VisualElement line = Row(device, "st-kv");
                Cell(line, DeviceKeys[i], "st-key");
                if (i == RowInterlock)
                {
                    var badges = new VisualElement { pickingMode = PickingMode.Ignore };
                    badges.AddToClassList("st-badges");
                    line.Add(badges);
                    _badges = new[] { Cell(badges, "", "st-badge"), Cell(badges, "", "st-badge"), Cell(badges, "", "st-badge"), Cell(badges, "", "st-badge") };
                }
                else if (i == RowBattery)
                {
                    var bar = new VisualElement { pickingMode = PickingMode.Ignore };
                    bar.AddToClassList("st-battery");
                    _batteryFill = new VisualElement { pickingMode = PickingMode.Ignore };
                    _batteryFill.AddToClassList("st-battery-fill");
                    bar.Add(_batteryFill);
                    line.Add(bar);
                    _deviceValues[i] = Cell(line, "", "st-value st-value--battery");
                }
                else _deviceValues[i] = Cell(line, "", "st-value");
            }
            _resultsDirty = true;
            _nextRefresh = 0f;
        }

        /// <summary>按间隔刷新（仅在可见时由演示屏调用）。now 为实际时间，单位 s。</summary>
        public void Tick(float now)
        {
            if (!IsBuilt || now < _nextRefresh) return;
            _nextRefresh = now + _theme.statusRefreshIntervalS;
            if (_resultsDirty) RenderResults();
            RenderDevice();
        }

        private void RenderResults()
        {
            _resultsDirty = false;
            _countTotal.text = $"已检 {InspectedCount}";
            _countPass.text = $"合格 {_pass}";
            _countFail.text = $"不合格 {_nonPass}";
            _countInvalid.text = $"无效 {_invalid}";
            _empty.style.display = _records.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            for (int i = 0; i < _rows.Length; i++)
            {
                if (i >= _records.Count)
                {
                    _rows[i].style.display = DisplayStyle.None;
                    continue;
                }
                InspectionRecord r = _records[i];
                _rows[i].style.display = DisplayStyle.Flex;
                _rows[i].EnableInClassList("st-row--suspect", r.Verdict == InspectionVerdict.Suspect);
                _rows[i].EnableInClassList("st-row--defect", r.Verdict == InspectionVerdict.Defect);
                _rows[i].EnableInClassList("st-row--invalid", r.Verdict == InspectionVerdict.Invalid);
                Label[] c = _cells[i];
                c[0].text = r.RivetId;
                c[1].text = _places[i];
                c[2].text = RivetInspectionService.Label(r.Verdict);
                c[3].text = r.Verdict == InspectionVerdict.Invalid ? "—（信号无效）" : DefectLabel(r.Truth);
                c[4].text = $"{r.ContactForceN:0.0} N";
                c[5].text = $"{r.Coupling:0.00}";
                long t = (long)r.TimeS;
                c[6].text = $"{t / 3600:00}:{t / 60 % 60:00}:{t % 60:00}";
            }
        }

        private void RenderDevice()
        {
            if (_device == null)
            {
                for (int i = 0; i < _deviceValues.Length; i++)
                    if (_deviceValues[i] != null) _deviceValues[i].text = i == 2 ? _currentCell : "—";
                foreach (Label b in _badges) b.text = "";
                return;
            }
            PlanarPose pose = _device.Pose;
            _deviceValues[0].text = $"x {pose.positionM.x:0.00} m · z {pose.positionM.y:0.00} m · 航向 {pose.yawDeg:0.0}°";
            _deviceValues[1].text = $"{_device.LinearSpeedMps:0.00} m/s · {_device.AngularSpeedDegPerS:0.0} °/s";
            _deviceValues[2].text = _currentCell;
            _deviceValues[3].text = $"{_device.LiftExtensionM * 1000f:0} mm";
            _deviceValues[4].text = _device.ArmStateText;
            Badge(0, _device.ChassisZeroSpeedLocked, "底盘 零速锁定", "底盘 未锁定");
            Badge(1, _device.ChassisMotionPermitted, "底盘运动 允许", "底盘运动 禁止");
            Badge(2, _device.LiftMotionPermitted, "升降柱 允许", "升降柱 禁止");
            Badge(3, _device.ArmMotionPermitted, "机械臂 允许", "机械臂 禁止");
            float soc = _device.BatterySocPct;
            _batteryFill.style.width = Length.Percent(soc);
            _deviceValues[RowBattery].text = $"{soc:0.0} %";
            _deviceValues[7].text = $"{_device.CommLatencyS * 1000f:0} ms";
        }

        private void Badge(int index, bool on, string onText, string offText)
        {
            Label b = _badges[index];
            b.text = on ? onText : offText;
            b.EnableInClassList(BadgeOnClass, on);
            b.EnableInClassList(BadgeOffClass, !on);
        }

        private static string DefectLabel(DefectType type) => type switch
        {
            DefectType.EdgeCrack => "边缘裂纹",
            DefectType.CorrosionThinning => "腐蚀减薄",
            DefectType.LooseRivet => "铆钉松动",
            _ => "—",
        };

        private static VisualElement Row(VisualElement parent, string classes)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            foreach (string c in classes.Split(' ')) row.AddToClassList(c);
            parent.Add(row);
            return row;
        }

        private static Label Cell(VisualElement parent, string text, string classes)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            foreach (string c in classes.Split(' ')) label.AddToClassList(c);
            parent.Add(label);
            return label;
        }
    }
}
