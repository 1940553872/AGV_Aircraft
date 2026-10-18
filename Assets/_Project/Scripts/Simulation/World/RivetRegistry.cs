using System;
using System.Collections.Generic;
using UnityEngine;

namespace RivetSim.Simulation.World
{
    /// <summary>
    /// 检测区登记表：全部区域、工位与铆钉的入口，挂在主场景 Zone_Work/InspectionRegions 根节点上。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RivetRegistry : MonoBehaviour
    {
        [SerializeField, Tooltip("检测区域")]
        private InspectionRegion[] regions = Array.Empty<InspectionRegion>();

        /// <summary>检测区域。</summary>
        public IReadOnlyList<InspectionRegion> Regions => regions;

        /// <summary>编辑器生成器写入区域引用。</summary>
        public void Configure(InspectionRegion[] allRegions) => regions = allRegions ?? Array.Empty<InspectionRegion>();

        /// <summary>按区域、工位顺序列出全部铆钉。</summary>
        public List<Rivet> GetAllRivets()
        {
            var all = new List<Rivet>();
            foreach (InspectionRegion region in regions)
            {
                foreach (InspectionStation station in region.Stations) all.AddRange(station.Rivets);
            }
            return all;
        }

        /// <summary>按 ID 查找铆钉，找不到时返回 null。</summary>
        public Rivet FindRivet(string rivetId)
        {
            foreach (Rivet rivet in GetAllRivets())
            {
                if (rivet.RivetId == rivetId) return rivet;
            }
            return null;
        }
    }
}
