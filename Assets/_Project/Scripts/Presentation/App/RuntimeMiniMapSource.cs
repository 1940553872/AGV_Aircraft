using System;
using System.Collections.Generic;
using RivetSim.Core.Config;
using RivetSim.Core.Diagnostics;
using RivetSim.Core.Events;
using RivetSim.Presentation.UI;
using RivetSim.Simulation.Inspection;
using RivetSim.Simulation.Navigation;
using RivetSim.Simulation.Robot;
using RivetSim.Simulation.World;
using Unity.Collections;
using UnityEngine;

namespace RivetSim.Presentation.App
{
    /// <summary>
    /// 主场景小地图数据源（M6）：按间隔把 OccupancyGridMap 写入贴图（复用像素缓冲，不分配），订阅 PathPlannedEvent 取规划路径，
    /// AGV 位姿取底盘真值。M8：虚拟禁行区（投影区内、接近通道外）以暗红色画进贴图；工位停靠位姿、充电位与铆钉位置取自检测区登记表与场地布局，
    /// 铆钉结果来自 <see cref="RivetInspectedEvent"/>。
    /// </summary>
    public sealed class RuntimeMiniMapSource : IMiniMapSource, IDisposable
    {
        private static readonly Color32 UnknownColor = new Color32(0x0B, 0x12, 0x1B, 0xFF);
        private static readonly Color32 FreeColor = new Color32(0x2C, 0x46, 0x63, 0xFF);
        private static readonly Color32 OccupiedColor = new Color32(0xE6, 0xEE, 0xF7, 0xFF);
        private static readonly Color32 KeepOutFreeColor = new Color32(0x5A, 0x2A, 0x33, 0xFF);
        private static readonly Color32 KeepOutUnknownColor = new Color32(0x3A, 0x1E, 0x28, 0xFF);

        private readonly OccupancyGridMap _map;
        private readonly AgvController _chassis;
        private readonly float _intervalS;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private readonly List<Vector2> _path = new List<Vector2>();
        private readonly List<PlanarPose> _stations = new List<PlanarPose>();
        private readonly List<Vector2> _rivets = new List<Vector2>();
        private readonly List<int> _results = new List<int>();
        private readonly Dictionary<Rivet, int> _rivetIndex = new Dictionary<Rivet, int>();
        private readonly bool[] _keepOut;
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

        /// <inheritdoc />
        public IReadOnlyList<PlanarPose> StationPoses => _stations;

        /// <inheritdoc />
        public PlanarPose ChargePose { get; }

        /// <inheritdoc />
        public IReadOnlyList<Vector2> RivetPointsM => _rivets;

        /// <inheritdoc />
        public IReadOnlyList<int> RivetResults => _results;

        /// <inheritdoc />
        public int MarksRevision { get; private set; }

        /// <summary>
        /// 创建；refreshIntervalS 为贴图刷新间隔（实际时间），单位 s。keepOut / registry 可为空。
        /// </summary>
        public RuntimeMiniMapSource(OccupancyGridMap map, AgvController chassis, EventBus bus, float refreshIntervalS,
            KeepOutMask keepOut, RivetRegistry registry, PlanarPose chargePose)
        {
            _map = map;
            _chassis = chassis;
            _intervalS = refreshIntervalS;
            ChargePose = chargePose;
            GridGeometry g = map.Geometry;
            GridTexture = new Texture2D(g.width, g.height, TextureFormat.RGBA32, false)
            {
                name = "T_MiniMapRuntime",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            WorldRectM = new Rect(g.originM, g.MaxM - g.originM);

            _keepOut = new bool[g.width * g.height];
            if (keepOut != null)
                for (int y = 0; y < g.height; y++)
                    for (int x = 0; x < g.width; x++)
                    {
                        Vector2 c = g.CellCenter(x, y);
                        _keepOut[g.Index(x, y)] = keepOut.IsProjected(c) && !keepOut.InChannel(c);
                    }

            if (registry != null)
                foreach (InspectionRegion region in registry.Regions)
                    foreach (InspectionStation station in region.Stations)
                    {
                        _stations.Add(station.DockingPose);
                        foreach (Rivet rivet in station.Rivets)
                        {
                            _rivetIndex[rivet] = _rivets.Count;
                            Vector3 p = rivet.PositionM;
                            _rivets.Add(new Vector2(p.x, p.z));
                            _results.Add(-1);
                        }
                    }

            _subscriptions.Add(bus.Subscribe<PathPlannedEvent>(OnPath));
            _subscriptions.Add(bus.Subscribe<RivetInspectedEvent>(OnInspected));
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
                pixels[i] = s == OccupancyState.Occupied ? OccupiedColor
                    : _keepOut[i] ? (s == OccupancyState.Free ? KeepOutFreeColor : KeepOutUnknownColor)
                    : s == OccupancyState.Free ? FreeColor : UnknownColor;
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

        private void OnInspected(RivetInspectedEvent e)
        {
            if (e.Rivet == null || e.Record == null || !_rivetIndex.TryGetValue(e.Rivet, out int index)) return;
            _results[index] = (int)e.Record.Verdict;
            MarksRevision++;
        }

        /// <summary>取消订阅并释放贴图。</summary>
        public void Dispose()
        {
            foreach (IDisposable s in _subscriptions) s.Dispose();
            _subscriptions.Clear();
            UnityEngine.Object.Destroy(GridTexture);
        }
    }
}
