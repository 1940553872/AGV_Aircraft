using System;
using System.Collections.Generic;
using RivetSim.Core.Config;
using RivetSim.Core.Diagnostics;
using RivetSim.Core.Events;
using RivetSim.Presentation.UI;
using RivetSim.Simulation.Navigation;
using RivetSim.Simulation.Robot;
using Unity.Collections;
using UnityEngine;

namespace RivetSim.Presentation.App
{
    /// <summary>
    /// 主场景小地图数据源（M6）：按间隔把 OccupancyGridMap 写入贴图（复用像素缓冲，不分配），订阅 PathPlannedEvent 取规划路径，
    /// AGV 位姿取底盘真值。
    /// </summary>
    public sealed class RuntimeMiniMapSource : IMiniMapSource, IDisposable
    {
        private static readonly Color32 UnknownColor = new Color32(0x15, 0x22, 0x32, 0xFF);
        private static readonly Color32 FreeColor = new Color32(0x26, 0x3A, 0x52, 0xFF);
        private static readonly Color32 OccupiedColor = new Color32(0xE6, 0xEE, 0xF7, 0xFF);

        private readonly OccupancyGridMap _map;
        private readonly AgvController _chassis;
        private readonly float _intervalS;
        private readonly IDisposable _subscription;
        private readonly List<Vector2> _path = new List<Vector2>();
        private float _sinceRefreshS = float.MaxValue;
        private int _shownRevision = -1;

        /// <inheritdoc />
        public Texture2D GridTexture { get; }

        /// <inheritdoc />
        public Rect WorldRectM { get; }

        /// <inheritdoc />
        public PlanarPose AgvPose => _chassis.Pose;

        /// <inheritdoc />
        public IReadOnlyList<Vector2> PathPoints => _path;

        /// <inheritdoc />
        public int PathRevision { get; private set; }

        /// <summary>创建；refreshIntervalS 为贴图刷新间隔（实际时间），单位 s。</summary>
        public RuntimeMiniMapSource(OccupancyGridMap map, AgvController chassis, EventBus bus, float refreshIntervalS)
        {
            _map = map;
            _chassis = chassis;
            _intervalS = refreshIntervalS;
            GridGeometry g = map.Geometry;
            GridTexture = new Texture2D(g.width, g.height, TextureFormat.RGBA32, false)
            {
                name = "T_MiniMapRuntime",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            WorldRectM = new Rect(g.originM, g.MaxM - g.originM);
            _subscription = bus.Subscribe<PathPlannedEvent>(OnPath);
            Refresh(float.MaxValue);
        }

        /// <inheritdoc />
        public void Refresh(float realDeltaS)
        {
            _sinceRefreshS += realDeltaS;
            if (_sinceRefreshS < _intervalS || _map.Revision == _shownRevision) return;
            _sinceRefreshS = 0f;
            _shownRevision = _map.Revision;
            GcProbe.Begin(ProbeModule.MiniMapSource);
            // 直接写贴图像素缓冲（托管数组经 SetPixels32 / SetPixelData 上传会产生分配，M7 GC 探针实测）。
            NativeArray<Color32> pixels = GridTexture.GetPixelData<Color32>(0);
            for (int i = 0; i < pixels.Length; i++)
            {
                OccupancyState s = _map.GetState(i);
                pixels[i] = s == OccupancyState.Occupied ? OccupiedColor : s == OccupancyState.Free ? FreeColor : UnknownColor;
            }
            GridTexture.Apply(false);
            GcProbe.End(ProbeModule.MiniMapSource);
        }

        private void OnPath(PathPlannedEvent e)
        {
            _path.Clear();
            if (e.Path != null) _path.AddRange(e.Path.Points);
            PathRevision++;
        }

        /// <summary>取消订阅并释放贴图。</summary>
        public void Dispose()
        {
            _subscription.Dispose();
            UnityEngine.Object.Destroy(GridTexture);
        }
    }
}
