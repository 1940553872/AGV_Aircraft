using System;
using System.Collections.Generic;
using RivetSim.Core.Config;
using UnityEngine;

namespace RivetSim.Simulation.World
{
    /// <summary>
    /// 检测工位：AGV 停靠位姿（世界坐标）、本工位的铆钉排、停靠标记板与蒙皮上的基准标记。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InspectionStation : MonoBehaviour
    {
        [SerializeField, Tooltip("工位 ID，如 R1-S1")]
        private string stationId;

        [SerializeField, Tooltip("区域序号（从 0 起）")]
        private int regionIndex;

        [SerializeField, Tooltip("工位序号（区域内，从 0 起）")]
        private int stationIndex;

        [SerializeField, Tooltip("AGV 停靠位姿（世界坐标，车头方向为偏航角）")]
        private PlanarPose dockingPose;

        [SerializeField, Tooltip("铆钉，沿机头方向排列")]
        private Rivet[] rivets = Array.Empty<Rivet>();

        [SerializeField, Tooltip("停靠标记板")]
        private RegionMarker dockingMarker;

        [SerializeField, Tooltip("基准标记（蒙皮上）")]
        private RegionMarker datumMarker;

        /// <summary>工位 ID。</summary>
        public string StationId => stationId;

        /// <summary>区域序号。</summary>
        public int RegionIndex => regionIndex;

        /// <summary>工位序号。</summary>
        public int StationIndex => stationIndex;

        /// <summary>AGV 停靠位姿（世界坐标）。</summary>
        public PlanarPose DockingPose => dockingPose;

        /// <summary>铆钉。</summary>
        public IReadOnlyList<Rivet> Rivets => rivets;

        /// <summary>停靠标记板。</summary>
        public RegionMarker DockingMarker => dockingMarker;

        /// <summary>基准标记。</summary>
        public RegionMarker DatumMarker => datumMarker;

        /// <summary>编辑器生成器写入参数。</summary>
        public void Configure(string id, int region, int station, PlanarPose pose, Rivet[] stationRivets,
            RegionMarker docking, RegionMarker datum)
        {
            stationId = id;
            regionIndex = region;
            stationIndex = station;
            dockingPose = pose;
            rivets = stationRivets ?? Array.Empty<Rivet>();
            dockingMarker = docking;
            datumMarker = datum;
        }

        /// <summary>本工位铆钉离地高度（供 LiftHeightPlanner 使用），单位 m。</summary>
        public float[] GetRivetHeightsM()
        {
            var heights = new float[rivets.Length];
            for (int i = 0; i < rivets.Length; i++) heights[i] = rivets[i].PositionM.y;
            return heights;
        }
    }
}
