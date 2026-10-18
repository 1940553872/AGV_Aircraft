using RivetSim.Simulation.Inspection;
using UnityEngine;

namespace RivetSim.Simulation.World
{
    /// <summary>
    /// 单颗铆钉：ID、所属区域与工位、表面法向（世界坐标，指向机身外）与真值缺陷。位置取本节点的世界坐标。
    /// 检测结果由检测流程（RivetInspectionService，M4）在运行时写入，不序列化。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Rivet : MonoBehaviour
    {
        [SerializeField, Tooltip("铆钉 ID，如 R1-S1-01")]
        private string rivetId;

        [SerializeField, Tooltip("区域序号（从 0 起）")]
        private int regionIndex;

        [SerializeField, Tooltip("工位序号（区域内，从 0 起）")]
        private int stationIndex;

        [SerializeField, Tooltip("工位内序号（从 0 起，沿机头方向递增）")]
        private int indexInStation;

        [SerializeField, Tooltip("表面法向（世界坐标，指向机身外）")]
        private Vector3 normal = Vector3.left;

        [SerializeField, Tooltip("真值缺陷")]
        private DefectType defect;

        /// <summary>铆钉 ID。</summary>
        public string RivetId => rivetId;

        /// <summary>区域序号。</summary>
        public int RegionIndex => regionIndex;

        /// <summary>工位序号。</summary>
        public int StationIndex => stationIndex;

        /// <summary>工位内序号。</summary>
        public int IndexInStation => indexInStation;

        /// <summary>表面法向（世界坐标）。</summary>
        public Vector3 Normal => normal;

        /// <summary>真值缺陷。</summary>
        public DefectType Defect => defect;

        /// <summary>位置（世界坐标），单位 m。</summary>
        public Vector3 PositionM => transform.position;

        /// <summary>是否已有检测结果（运行时）。</summary>
        public bool HasResult { get; private set; }

        /// <summary>最近一次检测结论（运行时）。</summary>
        public InspectionVerdict LastVerdict { get; private set; }

        /// <summary>写入检测结论（运行时）。</summary>
        public void SetResult(InspectionVerdict verdict)
        {
            LastVerdict = verdict;
            HasResult = true;
        }

        /// <summary>编辑器生成器写入参数。</summary>
        public void Configure(string id, int region, int station, int index, Vector3 surfaceNormal, DefectType truth)
        {
            rivetId = id;
            regionIndex = region;
            stationIndex = station;
            indexInStation = index;
            normal = surfaceNormal.normalized;
            defect = truth;
        }
    }
}
