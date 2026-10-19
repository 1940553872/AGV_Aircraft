using System;
using System.Collections.Generic;
using RivetSim.Core.Config;
using RivetSim.Core.Diagnostics;
using UnityEngine;
using UnityEngine.UIElements;

namespace RivetSim.Presentation.UI
{
    /// <summary>
    /// 小地图（M6）：栅格贴图按世界范围等比铺放，Painter2D 叠加规划路径、AGV 位姿与点选标记；
    /// M8 增加铆钉结果点（未检测灰、合格绿、疑似琥珀、缺陷红、无效蓝灰）、工位停靠点（青色方框）与充电位（绿色菱形）。
    /// <see cref="PickEnabled"/> 时点击（UI Toolkit 面板拾取，不用 OnMouseDown）换算为世界坐标并触发 <see cref="Picked"/>（t(1,8) 操作员初值）。
    /// 只在 AGV 位姿、路径或贴图变化时重绘。
    /// </summary>
    public sealed class MiniMapElement : VisualElement
    {
        private static readonly Color PathColor = new Color32(0x2E, 0xC4, 0xE6, 0xFF);
        private static readonly Color AgvColor = new Color32(0xFF, 0x9F, 0x1C, 0xFF);
        private static readonly Color PickColor = new Color32(0xFF, 0xC8, 0x57, 0xFF);
        private static readonly Color StationColor = new Color32(0x2E, 0xC4, 0xE6, 0xFF);
        private static readonly Color ChargeColor = new Color32(0x3D, 0xD6, 0x8C, 0xFF);
        // 铆钉结果点颜色，按 InspectionVerdict 整数值（合格、疑似、缺陷、无效）；未检测另取。
        private static readonly Color RivetPendingColor = new Color32(0x56, 0x65, 0x77, 0xFF);
        private static readonly Color[] RivetVerdictColors =
        {
            new Color32(0x3D, 0xD6, 0x8C, 0xFF), new Color32(0xFF, 0xC8, 0x57, 0xFF), new Color32(0xFF, 0x5D, 0x5D, 0xFF), new Color32(0x8F, 0xA3, 0xB8, 0xFF),
        };
        private const float StationHalfPx = 4.5f;
        private const float ChargeHalfPx = 6f;
        private const float PathWidthPx = 2f;
        private const float PickRadiusPx = 7f;

        private readonly VisualElement _image;
        private readonly VisualElement _overlay;
        private IMiniMapSource _source;
        private Texture2D _texture;
        private Rect _fit;
        private Vector2 _lastAgvPx = new Vector2(float.NaN, float.NaN);
        private float _lastAgvYawDeg = float.NaN;
        private int _lastPathRevision = -1;
        private int _lastMarksRevision = -1;
        private float _rivetDotRadiusPx = 2.5f;
        private bool _hasPick;
        private Vector2 _pickWorldM;
        private float _repaintThresholdPx = 0.5f;
        private Vector2 _agvMarkerSizePx = new Vector2(22f, 14f);

        /// <summary>是否允许点选。</summary>
        public bool PickEnabled { get; set; }

        /// <summary>点选的世界水平面坐标，单位 m。</summary>
        public event Action<Vector2> Picked;

        /// <summary>创建。</summary>
        public MiniMapElement()
        {
            AddToClassList("minimap");
            _image = new VisualElement { pickingMode = PickingMode.Ignore };
            _image.AddToClassList("minimap-image");
            _image.style.position = Position.Absolute;
            Add(_image);
            // 叠加层放在贴图之后（子元素绘制在父元素内容之上）。
            _overlay = new VisualElement { pickingMode = PickingMode.Ignore };
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0f;
            _overlay.style.top = 0f;
            _overlay.style.right = 0f;
            _overlay.style.bottom = 0f;
            _overlay.generateVisualContent += Draw;
            Add(_overlay);
            RegisterCallback<GeometryChangedEvent>(_ => Relayout());
            RegisterCallback<PointerDownEvent>(OnPointerDown);
        }

        /// <summary>绑定数据源；repaintThresholdPx 为 AGV 位移重绘阈值，agvMarkerSizePx 为 AGV 标记长 × 宽，rivetDotRadiusPx 为铆钉结果点半径，单位 px。</summary>
        public void SetSource(IMiniMapSource source, float repaintThresholdPx, Vector2 agvMarkerSizePx, float rivetDotRadiusPx = 2.5f)
        {
            _rivetDotRadiusPx = rivetDotRadiusPx;
            _source = source;
            _repaintThresholdPx = repaintThresholdPx;
            _agvMarkerSizePx = agvMarkerSizePx;
            Relayout();
        }

        /// <summary>清除点选标记。</summary>
        public void ClearPick()
        {
            if (!_hasPick) return;
            _hasPick = false;
            _overlay.MarkDirtyRepaint();
        }

        /// <summary>逐帧调用：检查变化后决定是否重绘（不分配内存）。</summary>
        public void Tick()
        {
            if (_source == null) return;
            if (_source.GridTexture != _texture) Relayout();
            Vector2 agvPx = WorldToLocal(_source.AgvPose.positionM);
            bool moved = float.IsNaN(_lastAgvPx.x) || (agvPx - _lastAgvPx).sqrMagnitude > _repaintThresholdPx * _repaintThresholdPx ||
                         Mathf.Abs(Mathf.DeltaAngle(_lastAgvYawDeg, _source.AgvPose.yawDeg)) > 1f;
            if (moved || _source.PathRevision != _lastPathRevision || _source.MarksRevision != _lastMarksRevision)
            {
                _lastMarksRevision = _source.MarksRevision;
                _lastAgvPx = agvPx;
                _lastAgvYawDeg = _source.AgvPose.yawDeg;
                _lastPathRevision = _source.PathRevision;
                _overlay.MarkDirtyRepaint();
            }
        }

        /// <summary>世界坐标 → 本元素局部坐标（单位 px）。</summary>
        public Vector2 WorldToLocal(Vector2 worldM)
        {
            if (_source == null || _fit.width <= 0f) return Vector2.zero;
            Rect w = _source.WorldRectM;
            float u = (worldM.x - w.xMin) / w.width;
            float v = (worldM.y - w.yMin) / w.height;
            return new Vector2(_fit.xMin + u * _fit.width, _fit.yMax - v * _fit.height);
        }

        /// <summary>本元素局部坐标 → 世界坐标（单位 m）。</summary>
        public Vector2 LocalToWorld(Vector2 localPx)
        {
            Rect w = _source.WorldRectM;
            float u = (localPx.x - _fit.xMin) / _fit.width;
            float v = (_fit.yMax - localPx.y) / _fit.height;
            return new Vector2(w.xMin + u * w.width, w.yMin + v * w.height);
        }

        private void Relayout()
        {
            Rect r = contentRect;
            if (_source == null || r.width <= 0f || r.height <= 0f) return;
            _texture = _source.GridTexture;
            Rect w = _source.WorldRectM;
            float scale = Mathf.Min(r.width / w.width, r.height / w.height);
            var size = new Vector2(w.width * scale, w.height * scale);
            _fit = new Rect(r.xMin + (r.width - size.x) * 0.5f, r.yMin + (r.height - size.y) * 0.5f, size.x, size.y);
            _image.style.left = _fit.xMin;
            _image.style.top = _fit.yMin;
            _image.style.width = _fit.width;
            _image.style.height = _fit.height;
            _image.style.backgroundImage = _texture != null ? Background.FromTexture2D(_texture) : new StyleBackground(StyleKeyword.None);
            _lastAgvPx = new Vector2(float.NaN, float.NaN);
            _overlay.MarkDirtyRepaint();
        }

        /// <summary>按世界坐标点选（与点击同一路径；验证工具用）。返回是否接受。</summary>
        public bool PickAt(Vector2 worldM)
        {
            if (!PickEnabled || _source == null) return false;
            _pickWorldM = worldM;
            _hasPick = true;
            _overlay.MarkDirtyRepaint();
            Picked?.Invoke(_pickWorldM);
            return true;
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (!PickEnabled || _source == null || !_fit.Contains(evt.localPosition)) return;
            if (PickAt(LocalToWorld(evt.localPosition))) evt.StopPropagation();
        }

        private void Draw(MeshGenerationContext context)
        {
            GcProbe.Begin(ProbeModule.MiniMapDraw);
            DrawContent(context);
            GcProbe.End(ProbeModule.MiniMapDraw);
        }

        private void DrawMarks(Painter2D p)
        {
            IReadOnlyList<Vector2> rivets = _source.RivetPointsM;
            IReadOnlyList<int> results = _source.RivetResults;
            if (rivets != null && results != null)
                for (int i = 0; i < rivets.Count && i < results.Count; i++)
                {
                    int r = results[i];
                    p.fillColor = r >= 0 && r < RivetVerdictColors.Length ? RivetVerdictColors[r] : RivetPendingColor;
                    p.BeginPath();
                    p.Arc(WorldToLocal(rivets[i]), _rivetDotRadiusPx, 0f, 360f);
                    p.Fill();
                }

            IReadOnlyList<PlanarPose> stations = _source.StationPoses;
            if (stations != null)
            {
                p.strokeColor = StationColor;
                p.lineWidth = 1.5f;
                for (int i = 0; i < stations.Count; i++)
                {
                    Vector2 c = WorldToLocal(stations[i].positionM);
                    p.BeginPath();
                    p.MoveTo(c + new Vector2(-StationHalfPx, -StationHalfPx));
                    p.LineTo(c + new Vector2(StationHalfPx, -StationHalfPx));
                    p.LineTo(c + new Vector2(StationHalfPx, StationHalfPx));
                    p.LineTo(c + new Vector2(-StationHalfPx, StationHalfPx));
                    p.ClosePath();
                    p.Stroke();
                }
            }

            Vector2 charge = WorldToLocal(_source.ChargePose.positionM);
            p.fillColor = ChargeColor;
            p.BeginPath();
            p.MoveTo(charge + new Vector2(0f, -ChargeHalfPx));
            p.LineTo(charge + new Vector2(ChargeHalfPx, 0f));
            p.LineTo(charge + new Vector2(0f, ChargeHalfPx));
            p.LineTo(charge + new Vector2(-ChargeHalfPx, 0f));
            p.ClosePath();
            p.Fill();
        }

        private void DrawContent(MeshGenerationContext context)
        {
            if (_source == null || _fit.width <= 0f) return;
            Painter2D p = context.painter2D;
            DrawMarks(p);
            IReadOnlyList<Vector2> path = _source.PathPoints;
            if (path != null && path.Count > 1)
            {
                p.lineWidth = PathWidthPx;
                p.strokeColor = PathColor;
                p.lineJoin = LineJoin.Round;
                p.BeginPath();
                p.MoveTo(WorldToLocal(path[0]));
                for (int i = 1; i < path.Count; i++) p.LineTo(WorldToLocal(path[i]));
                p.Stroke();
            }

            PlanarPose agv = _source.AgvPose;
            Vector2 c = WorldToLocal(agv.positionM);
            float yaw = agv.yawDeg * Mathf.Deg2Rad;
            // 世界航向 (sin, cos) 在小地图上 y 轴向下。
            var fwd = new Vector2(Mathf.Sin(yaw), -Mathf.Cos(yaw));
            var side = new Vector2(-fwd.y, fwd.x);
            p.fillColor = AgvColor;
            p.BeginPath();
            p.MoveTo(c + fwd * _agvMarkerSizePx.x * 0.6f);
            p.LineTo(c - fwd * _agvMarkerSizePx.x * 0.4f + side * _agvMarkerSizePx.y * 0.5f);
            p.LineTo(c - fwd * _agvMarkerSizePx.x * 0.4f - side * _agvMarkerSizePx.y * 0.5f);
            p.ClosePath();
            p.Fill();

            if (_hasPick)
            {
                Vector2 m = WorldToLocal(_pickWorldM);
                p.strokeColor = PickColor;
                p.lineWidth = 2f;
                p.BeginPath();
                p.Arc(m, PickRadiusPx, 0f, 360f);
                p.MoveTo(m + new Vector2(-PickRadiusPx * 1.6f, 0f));
                p.LineTo(m + new Vector2(PickRadiusPx * 1.6f, 0f));
                p.MoveTo(m + new Vector2(0f, -PickRadiusPx * 1.6f));
                p.LineTo(m + new Vector2(0f, PickRadiusPx * 1.6f));
                p.Stroke();
            }
        }
    }
}
