using RivetSim.Presentation.CameraControl;
using RivetSim.Simulation.Mission;
using RivetSim.Simulation.World;
using UnityEngine;

namespace RivetSim.Presentation.App
{
    /// <summary>
    /// 检测机位取景目标（M8）：按任务编排器的游标（区域 / 工位 / 铆钉）从检测区登记表取当前铆钉的位置与外法向。
    /// </summary>
    public sealed class MissionRivetFocusSource : IRivetFocusSource
    {
        private readonly MissionRoutine _mission;

        /// <summary>创建。</summary>
        public MissionRivetFocusSource(MissionRoutine mission) => _mission = mission;

        /// <inheritdoc />
        public bool TryGetFocus(out Vector3 pointM, out Vector3 normal)
        {
            pointM = default;
            normal = Vector3.up;
            RivetRegistry registry = _mission?.Registry;
            MissionCursor cursor = _mission?.Orchestrator?.Cursor;
            if (registry == null || cursor == null) return false;
            if (cursor.RegionIndex < 0 || cursor.RegionIndex >= registry.Regions.Count) return false;
            InspectionRegion region = registry.Regions[cursor.RegionIndex];
            if (cursor.StationIndex < 0 || cursor.StationIndex >= region.Stations.Count) return false;
            InspectionStation station = region.Stations[cursor.StationIndex];
            if (cursor.RivetIndex < 0 || cursor.RivetIndex >= station.Rivets.Count) return false;
            Rivet rivet = station.Rivets[cursor.RivetIndex];
            if (rivet == null) return false;
            pointM = rivet.PositionM;
            normal = rivet.Normal.sqrMagnitude > 1e-6f ? rivet.Normal.normalized : Vector3.up;
            return true;
        }
    }
}
