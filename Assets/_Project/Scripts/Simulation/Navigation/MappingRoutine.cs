using System.Collections.Generic;
using System.Threading;
using RivetSim.Core.Events;
using RivetSim.Core.Logging;
using UnityEngine;

namespace RivetSim.Simulation.Navigation
{
    /// <summary>
    /// 巡游建图（教学简化模型）：沿预设巡游路线以巡航速度行驶，激光雷达按真值位姿把每圈扫描写入占据栅格，地图随行驶逐步“长出来”。
    /// 不做扫描匹配与回环；路线来自 CFG_SceneLayout（主场景）或沙盒入口。
    /// </summary>
    public sealed class MappingRoutine
    {
        private readonly NavigationService _navigation;
        private readonly OccupancyGridMap _map;
        private readonly EventBus _bus;
        private readonly float _speedMps;

        /// <summary>创建建图流程。</summary>
        public MappingRoutine(NavigationService navigation, OccupancyGridMap map, EventBus bus, float speedMps)
        {
            _navigation = navigation;
            _map = map;
            _bus = bus;
            _speedMps = speedMps;
        }

        /// <summary>从当前位置出发依次经过 route 的各点完成建图。</summary>
        public async Awaitable<NavigationResult> RunAsync(IReadOnlyList<Vector2> route, CancellationToken ct)
        {
            SimLog.Info(LogCategory.Nav, $"开始巡游建图：{route.Count} 个途经点，栅格 {_map.Geometry.width} × {_map.Geometry.height}（{_map.Geometry.resolutionM:0.00} m）");
            float lastReported = -1f;
            NavigationResult result = await _navigation.FollowPathAsync(route, _speedMps, "巡游建图", progress =>
            {
                // 每完成 1% 发布一次进度，避免每帧发布。
                if (progress - lastReported < 0.01f && progress < 1f) return;
                lastReported = progress;
                _bus?.Publish(new MappingProgressEvent(progress, _map.KnownCellCount, progress * (_navigation.LastPath?.LengthM ?? 0f)));
            }, ct);
            _bus?.Publish(new MappingProgressEvent(result.Success ? 1f : lastReported, _map.KnownCellCount, result.TraveledM));
            float knownRatio = (float)_map.KnownCellCount / _map.Geometry.CellCount;
            SimLog.Info(LogCategory.Nav, $"巡游建图结束：已观测 {_map.KnownCellCount} 格（占地图 {knownRatio:P1}）");
            return result;
        }
    }
}
