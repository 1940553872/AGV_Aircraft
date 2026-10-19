using System;
using RivetSim.Core.Config;
using RivetSim.Core.Mission;
using RivetSim.Core.TaskMatrix;
using UnityEngine;
using UnityEngine.UIElements;

namespace RivetSim.Presentation.UI
{
    /// <summary>
    /// 任务矩阵单元（M6，Architecture §5.2）：显示 t(i,j) 与任务文字（取自 JSON），按状态切换 USS 类
    /// （cell--idle / standby / armed / running / done / skipped / triggered / failed）。
    /// 条件单元用 <see cref="DashedBorderElement"/> 画虚线边框；Running 粗实线 + 呼吸光 + 左上角循环进度条；
    /// Triggered 虚线变琥珀色闪烁后按实际状态显示，并保留“已触发”角标直到本行重置。
    /// </summary>
    public sealed class TaskCellElement : VisualElement
    {
        /// <summary>状态 USS 类前缀。</summary>
        public const string StateClassPrefix = "cell--";

        private static readonly string[] StateClasses = BuildStateClasses();

        private readonly DashedBorderElement _dashed;
        private readonly VisualElement _glow;
        private readonly VisualElement _progress;
        private readonly Label _triggerBadge;
        private readonly UiThemeConfig _theme;
        private float _flashStartS = float.NegativeInfinity;
        private float _animStartS;
        private CellState _shownState = (CellState)(-1);

        /// <summary>单元定义（JSON）。</summary>
        public CellDef Cell { get; }

        /// <summary>仿真上报的实际状态。</summary>
        public CellState State { get; private set; } = CellState.Idle;

        /// <summary>当前显示的状态（Triggered 闪烁期间仍显示 Triggered）。</summary>
        public CellState DisplayState { get; private set; } = CellState.Idle;

        /// <summary>本轮是否被条件触发过（行重置时清除）。</summary>
        public bool WasTriggered { get; private set; }

        /// <summary>是否需要逐帧刷新（Running 呼吸光或 Triggered 闪烁）。</summary>
        public bool IsAnimating => DisplayState == CellState.Running || DisplayState == CellState.Triggered;

        /// <summary>单元被点击（全屏交互时弹出说明卡）。</summary>
        public event Action<TaskCellElement> Clicked;

        /// <summary>按 JSON 单元定义创建。</summary>
        public TaskCellElement(CellDef cell, UiThemeConfig theme)
        {
            Cell = cell;
            _theme = theme;
            name = cell.Id;
            AddToClassList("cell");
            AddToClassList("cell--kind-" + cell.Kind.ToString().ToLowerInvariant());
            if (cell.IsExampleCell) AddToClassList("cell--example");

            _glow = new VisualElement { pickingMode = PickingMode.Ignore };
            _glow.AddToClassList("cell-glow");
            Add(_glow);
            _progress = new VisualElement { pickingMode = PickingMode.Ignore };
            _progress.AddToClassList("cell-progress");
            Add(_progress);

            var id = new Label(cell.Id) { pickingMode = PickingMode.Ignore };
            id.AddToClassList("cell-id");
            Add(id);
            var text = new Label(cell.Label) { pickingMode = PickingMode.Ignore };
            text.AddToClassList("cell-text");
            Add(text);

            var done = new Label("✓") { pickingMode = PickingMode.Ignore };
            done.AddToClassList("cell-done-badge");
            Add(done);
            _triggerBadge = new Label("已触发") { pickingMode = PickingMode.Ignore };
            _triggerBadge.AddToClassList("cell-trigger-badge");
            Add(_triggerBadge);

            _dashed = new DashedBorderElement();
            _dashed.SetPattern(theme.dashPatternPx.x, theme.dashPatternPx.y);
            Add(_dashed);

            RegisterCallback<PointerDownEvent>(_ => Clicked?.Invoke(this));
            ApplyDisplay(CellState.Idle, 0f);
        }

        /// <summary>写入仿真上报的新状态。nowS 为实际时间（动画计时）。</summary>
        public void SetState(CellState state, float nowS)
        {
            CellState previous = State;
            State = state;
            if (state == CellState.Triggered)
            {
                WasTriggered = true;
                _flashStartS = nowS;
            }
            // 行重置：回到初始状态时清除“已触发”角标。
            if ((state == CellState.Idle || state == CellState.Standby || state == CellState.Armed) && previous != CellState.Triggered)
                WasTriggered = false;
            Refresh(nowS);
        }

        /// <summary>逐帧刷新动效（只对 <see cref="IsAnimating"/> 的单元调用，不分配内存）。</summary>
        public void Tick(float nowS)
        {
            if (DisplayState == CellState.Triggered)
            {
                if (nowS - _flashStartS >= _theme.triggeredFlashDurationS)
                {
                    Refresh(nowS);
                    return;
                }
                float phase = (nowS - _flashStartS) / _theme.triggeredFlashDurationS * Mathf.Max(1, _theme.triggeredFlashCount);
                bool on = phase - Mathf.Floor(phase) < 0.5f;
                _dashed.SetStroke(on ? _theme.conditionTriggered : Transparent(_theme.conditionTriggered), DashWidthTriggeredPx);
                return;
            }
            if (DisplayState != CellState.Running) return;
            float t = nowS - _animStartS;
            float breath = 0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * t / Mathf.Max(0.1f, _theme.runningBreathPeriodS));
            float alpha = Mathf.Lerp(_theme.runningBreathMinAlpha, 1f, breath);
            Color c = _theme.running;
            c.a = alpha;
            style.borderTopColor = c;
            style.borderRightColor = c;
            style.borderBottomColor = c;
            style.borderLeftColor = c;
            _glow.style.opacity = breath;
            float p = Mathf.PingPong(t / Mathf.Max(0.1f, _theme.runningProgressPeriodS), 1f);
            _progress.style.width = Length.Percent(100f * p);
        }

        private const float DashWidthPx = 1.5f;
        private const float DashWidthTriggeredPx = 2.5f;

        private void Refresh(float nowS)
        {
            bool flashing = WasTriggered && nowS - _flashStartS < _theme.triggeredFlashDurationS;
            ApplyDisplay(flashing ? CellState.Triggered : State, nowS);
        }

        private void ApplyDisplay(CellState shown, float nowS)
        {
            DisplayState = shown;
            _triggerBadge.style.display = WasTriggered && shown != CellState.Triggered ? DisplayStyle.Flex : DisplayStyle.None;
            if (shown == _shownState) return;
            _shownState = shown;
            _animStartS = nowS;
            for (int i = 0; i < StateClasses.Length; i++) EnableInClassList(StateClasses[i], i == (int)shown);

            bool running = shown == CellState.Running;
            if (!running)
            {
                // 清除呼吸光写入的内联颜色，交还 USS。
                style.borderTopColor = StyleKeyword.Null;
                style.borderRightColor = StyleKeyword.Null;
                style.borderBottomColor = StyleKeyword.Null;
                style.borderLeftColor = StyleKeyword.Null;
                _glow.style.opacity = 0f;
            }
            bool dashed = Cell.Kind == CellKind.Conditional &&
                          (shown == CellState.Idle || shown == CellState.Armed || shown == CellState.Triggered || shown == CellState.Skipped);
            Color dashColor = shown == CellState.Triggered ? _theme.conditionTriggered
                : shown == CellState.Armed ? _theme.textSecondary : _theme.standbyGray;
            _dashed.style.display = dashed ? DisplayStyle.Flex : DisplayStyle.None;
            _dashed.SetStroke(dashColor, shown == CellState.Triggered ? DashWidthTriggeredPx : DashWidthPx);
        }

        private static Color Transparent(Color c)
        {
            c.a = 0f;
            return c;
        }

        private static string[] BuildStateClasses()
        {
            Array values = Enum.GetValues(typeof(CellState));
            var classes = new string[values.Length];
            foreach (CellState s in values) classes[(int)s] = StateClassPrefix + s.ToString().ToLowerInvariant();
            return classes;
        }
    }
}
