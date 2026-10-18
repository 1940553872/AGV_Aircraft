using RivetSim.Simulation.Inspection;
using UnityEngine;
using UnityEngine.UIElements;

namespace RivetSim.Presentation.UI
{
    /// <summary>
    /// A 扫波形显示（UI Toolkit 自定义元素，Painter2D 绘制）：网格、射频波形、闸门 A / B 与底面闸门（横线位于阈值高度、覆盖闸门时间范围）。
    /// 纵轴为满屏高度比例（−1～1），横轴为显示时间窗。M6 演示屏可直接复用。
    /// </summary>
    public sealed class AScanGraphElement : VisualElement
    {
        // 配色取 Architecture §7。
        private static readonly Color GridColor = new Color32(0x26, 0x3A, 0x52, 0xFF);
        private static readonly Color WaveColor = new Color32(0x2E, 0xC4, 0xE6, 0xFF);
        private static readonly Color GateAColor = new Color32(0x8F, 0xA3, 0xB8, 0xFF);
        private static readonly Color GateBColor = new Color32(0xFF, 0x5D, 0x5D, 0xFF);
        private static readonly Color BackwallColor = new Color32(0x3D, 0xD6, 0x8C, 0xFF);
        private const int GridDivisionsX = 10;
        private const int GridDivisionsY = 4;
        private const float WaveWidthPx = 1.5f;
        private const float GateWidthPx = 3f;

        private AScanWaveform _waveform;
        private GateResult _gates;
        private float _gateAThreshold, _gateBThreshold, _backwallThreshold;

        /// <summary>创建元素。</summary>
        public AScanGraphElement()
        {
            generateVisualContent += Draw;
        }

        /// <summary>设置波形、闸门结果与阈值（满屏高度比例）并重绘。</summary>
        public void SetData(AScanWaveform waveform, in GateResult gates, float gateAThreshold, float gateBThreshold, float backwallThreshold)
        {
            _waveform = waveform;
            _gates = gates;
            _gateAThreshold = gateAThreshold;
            _gateBThreshold = gateBThreshold;
            _backwallThreshold = backwallThreshold;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            Rect r = contentRect;
            if (r.width <= 0f || r.height <= 0f) return;
            Painter2D p = context.painter2D;
            p.lineWidth = 1f;
            p.strokeColor = GridColor;
            p.BeginPath();
            for (int i = 0; i <= GridDivisionsX; i++)
            {
                float x = r.xMin + r.width * i / GridDivisionsX;
                p.MoveTo(new Vector2(x, r.yMin));
                p.LineTo(new Vector2(x, r.yMax));
            }
            for (int j = 0; j <= GridDivisionsY; j++)
            {
                float y = r.yMin + r.height * j / GridDivisionsY;
                p.MoveTo(new Vector2(r.xMin, y));
                p.LineTo(new Vector2(r.xMax, y));
            }
            p.Stroke();
            if (_waveform == null || _waveform.Samples.Length < 2) return;

            float window = _waveform.WindowUs;
            Vector2 ToPx(float tUs, float amp) => new Vector2(r.xMin + r.width * tUs / window, r.center.y - 0.5f * r.height * Mathf.Clamp(amp, -1f, 1f));

            p.lineWidth = WaveWidthPx;
            p.strokeColor = WaveColor;
            p.BeginPath();
            float[] s = _waveform.Samples;
            p.MoveTo(ToPx(0f, s[0]));
            for (int i = 1; i < s.Length; i++) p.LineTo(ToPx(_waveform.TimeUs(i), s[i]));
            p.Stroke();

            DrawGate(p, ToPx, _gates.GateAStartUs, _gates.GateAEndUs, _gateAThreshold, GateAColor);
            DrawGate(p, ToPx, _gates.GateBStartUs, _gates.GateBEndUs, _gateBThreshold, GateBColor);
            DrawGate(p, ToPx, _gates.BackwallStartUs, _gates.BackwallEndUs, _backwallThreshold, BackwallColor);
        }

        private static void DrawGate(Painter2D p, System.Func<float, float, Vector2> toPx, float startUs, float endUs, float threshold, Color color)
        {
            p.lineWidth = GateWidthPx;
            p.strokeColor = color;
            p.BeginPath();
            p.MoveTo(toPx(startUs, threshold));
            p.LineTo(toPx(endUs, threshold));
            p.Stroke();
        }
    }
}
