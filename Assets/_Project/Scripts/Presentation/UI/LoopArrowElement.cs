using System;
using System.Collections.Generic;
using RivetSim.Core.TaskMatrix;
using UnityEngine;
using UnityEngine.UIElements;

namespace RivetSim.Presentation.UI
{
    /// <summary>
    /// 任务矩阵右侧回路箭头（M6）：按 JSON loops 绘制，“下一颗”为实线、“换站”为虚线；
    /// 每条回路占一条竖向车道，从起点阶段行引出、回到目标阶段行并带箭头；被走到时高亮。
    /// 行位置由 <see cref="RowCenterY"/> 提供（本元素局部坐标）。
    /// </summary>
    public sealed class LoopArrowElement : VisualElement
    {
        private const float DimAlpha = 0.45f;
        private const float LineWidthPx = 2f;
        private const float HighlightWidthPx = 3.5f;
        private const float ArrowSizePx = 9f;
        private const float LabelOffsetPx = 4f;

        private readonly List<LoopDef> _loops = new List<LoopDef>();
        private readonly List<Label> _labels = new List<Label>();
        private readonly List<int> _fromRows = new List<int>();
        private readonly List<int> _toRows = new List<int>();
        private readonly bool[] _highlight;
        private readonly Color _accent;
        private readonly Vector2 _dash;

        /// <summary>阶段行号（从 1 起）→ 行中心的局部 y 坐标，单位 px。</summary>
        public Func<int, float> RowCenterY { get; set; }

        /// <summary>按 JSON 回路创建。</summary>
        public LoopArrowElement(TaskMatrixData data, Color accent, Vector2 dashPatternPx)
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("loop-arrows");
            _accent = accent;
            _dash = dashPatternPx;
            foreach (LoopDef loop in data.Loops)
            {
                _loops.Add(loop);
                _fromRows.Add(data.RowOfPhase(loop.From));
                _toRows.Add(data.RowOfPhase(loop.To));
                var label = new Label(loop.Label) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("loop-label");
                label.AddToClassList(loop.Style == LoopStyle.Dashed ? "loop-label--dashed" : "loop-label--solid");
                _labels.Add(label);
                Add(label);
            }
            _highlight = new bool[_loops.Count];
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => PlaceLabels());
        }

        /// <summary>回路条数。</summary>
        public int Count => _loops.Count;

        /// <summary>回路是否高亮。</summary>
        public bool IsHighlighted(int index) => _highlight[index];

        /// <summary>按回路 id 查序号，找不到返回 -1。</summary>
        public int IndexOf(string loopId)
        {
            for (int i = 0; i < _loops.Count; i++)
                if (_loops[i].Id == loopId) return i;
            return -1;
        }

        /// <summary>设置高亮，有变化时重绘。</summary>
        public void SetHighlight(int index, bool on)
        {
            if (index < 0 || index >= _highlight.Length || _highlight[index] == on) return;
            _highlight[index] = on;
            _labels[index].EnableInClassList("loop-label--active", on);
            MarkDirtyRepaint();
        }

        /// <summary>行布局变化后重新定位（由矩阵视图在几何变化时调用）。</summary>
        public void Relayout()
        {
            PlaceLabels();
            MarkDirtyRepaint();
        }

        private float LaneX(int index) => contentRect.width * (index + 1f) / (_loops.Count + 1f);

        private void PlaceLabels()
        {
            if (RowCenterY == null) return;
            for (int i = 0; i < _loops.Count; i++)
            {
                float yMid = 0.5f * (RowCenterY(_fromRows[i]) + RowCenterY(_toRows[i]));
                Label label = _labels[i];
                label.style.left = LaneX(i) + LabelOffsetPx;
                label.style.top = yMid;
            }
        }

        private void Draw(MeshGenerationContext context)
        {
            if (RowCenterY == null || contentRect.width <= 0f) return;
            Painter2D p = context.painter2D;
            p.lineCap = LineCap.Butt;
            p.lineJoin = LineJoin.Miter;
            for (int i = 0; i < _loops.Count; i++)
            {
                float x = LaneX(i);
                float y0 = RowCenterY(_fromRows[i]);
                float y1 = RowCenterY(_toRows[i]);
                Color color = _accent;
                color.a = _highlight[i] ? 1f : DimAlpha;
                p.strokeColor = color;
                p.fillColor = color;
                p.lineWidth = _highlight[i] ? HighlightWidthPx : LineWidthPx;
                var a = new Vector2(0f, y0);
                var b = new Vector2(x, y0);
                var c = new Vector2(x, y1);
                var d = new Vector2(ArrowSizePx, y1);
                p.BeginPath();
                bool dashed = _loops[i].Style == LoopStyle.Dashed;
                Segment(p, a, b, dashed);
                Segment(p, b, c, dashed);
                Segment(p, c, d, dashed);
                p.Stroke();
                p.BeginPath();
                p.MoveTo(new Vector2(0f, y1));
                p.LineTo(new Vector2(ArrowSizePx, y1 - ArrowSizePx * 0.6f));
                p.LineTo(new Vector2(ArrowSizePx, y1 + ArrowSizePx * 0.6f));
                p.ClosePath();
                p.Fill();
            }
        }

        private void Segment(Painter2D p, Vector2 from, Vector2 to, bool dashed)
        {
            if (!dashed)
            {
                p.MoveTo(from);
                p.LineTo(to);
                return;
            }
            float length = Vector2.Distance(from, to);
            if (length <= 0f) return;
            Vector2 dir = (to - from) / length;
            for (float s = 0f; s < length; s += _dash.x + _dash.y)
            {
                p.MoveTo(from + dir * s);
                p.LineTo(from + dir * Mathf.Min(length, s + _dash.x));
            }
        }
    }
}
