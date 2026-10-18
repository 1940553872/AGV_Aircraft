using System;
using System.Collections.Generic;
using UnityEngine;

namespace RivetSim.Simulation.World
{
    /// <summary>
    /// 检测区域：机身一侧后机身侧壁（机翼后缘与平尾前缘之间）上的若干工位。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InspectionRegion : MonoBehaviour
    {
        [SerializeField, Tooltip("区域 ID，如 R1")]
        private string regionId;

        [SerializeField, Tooltip("区域序号（从 0 起）")]
        private int regionIndex;

        [SerializeField, Tooltip("所在机身侧")]
        private RegionSide side;

        [SerializeField, Tooltip("工位，按机头方向在前的顺序")]
        private InspectionStation[] stations = Array.Empty<InspectionStation>();

        /// <summary>区域 ID。</summary>
        public string RegionId => regionId;

        /// <summary>区域序号。</summary>
        public int RegionIndex => regionIndex;

        /// <summary>所在机身侧。</summary>
        public RegionSide Side => side;

        /// <summary>工位。</summary>
        public IReadOnlyList<InspectionStation> Stations => stations;

        /// <summary>编辑器生成器写入参数。</summary>
        public void Configure(string id, int index, RegionSide regionSide, InspectionStation[] regionStations)
        {
            regionId = id;
            regionIndex = index;
            side = regionSide;
            stations = regionStations ?? Array.Empty<InspectionStation>();
        }
    }
}
