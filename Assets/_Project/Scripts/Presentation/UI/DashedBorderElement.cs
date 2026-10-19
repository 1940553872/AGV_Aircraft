using UnityEngine;
using UnityEngine.UIElements;

namespace RivetSim.Presentation.UI
{
    /// <summary>
    /// 虚线边框（M6，Architecture §7）：USS 不支持虚线边框，铺满父元素后在 generateVisualContent 中用 Painter2D 分段绘制。
    /// 只在颜色、线宽或尺寸变化时重绘。
    /// </summary>
    public sealed class DashedBorderElement : VisualElement
    {
        private Color _color = Color.gray;
        private float _widthPx = 2f;
        private float _dashPx = 8f;
        private float _gapPx = 5f;

        /// <summary>当前线条颜色。</summary>
        public Color StrokeColor => _color;

        /// <summary>创建铺满父元素、不参与拾取的虚线边框。</summary>
        public DashedBorderElement()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0f;
            style.top = 0f;
            style.right = 0f;
            style.bottom = 0f;
            generateVisualContent += Draw;
        }

        /// <summary>设置虚线样式（线段长 / 间隔，单位 px）。</summary>
        public void SetPattern(float dashPx, float gapPx)
        {
            _dashPx = Mathf.Max(1f, dashPx);
            _gapPx = Mathf.Max(1f, gapPx);
            MarkDirtyRepaint();
        }

        /// <summary>设置颜色与线宽（单位 px），无变化时不重绘。</summary>
        public void SetStroke(Color color, float widthPx)
        {
            if (color == _color && Mathf.Approximately(widthPx, _widthPx)) return;
            _color = color;
            _widthPx = widthPx;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            Rect r = contentRect;
            if (r.width <= 0f || r.height <= 0f || _color.a <= 0f) return;
            Painter2D p = context.painter2D;
            p.lineWidth = _widthPx;
            p.strokeColor = _color;
            p.lineCap = LineCap.Butt;
            float inset = _widthPx * 0.5f;
            var a = new Vector2(r.xMin + inset, r.yMin + inset);
            var b = new Vector2(r.xMax - inset, r.yMin + inset);
            var c = new Vector2(r.xMax - inset, r.yMax - inset);
            var d = new Vector2(r.xMin + inset, r.yMax - inset);
            p.BeginPath();
            Dash(p, a, b);
            Dash(p, b, c);
            Dash(p, c, d);
            Dash(p, d, a);
            p.Stroke();
        }

        private void Dash(Painter2D p, Vector2 from, Vector2 to)
        {
            float length = Vector2.Distance(from, to);
            if (length <= 0f) return;
            Vector2 dir = (to - from) / length;
            for (float s = 0f; s < length; s += _dashPx + _gapPx)
            {
                p.MoveTo(from + dir * s);
                p.LineTo(from + dir * Mathf.Min(length, s + _dashPx));
            }
        }
    }
}
