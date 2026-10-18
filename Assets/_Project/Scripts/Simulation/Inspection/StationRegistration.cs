using System.Collections.Generic;
using RivetSim.Simulation.World;
using UnityEngine;

namespace RivetSim.Simulation.Inspection
{
    /// <summary>
    /// 工位基准注册结果（T3，教学简化模型）：铆钉在基准标记坐标系中的位置与法向视为“图纸坐标”（取自真值），
    /// 用注册得到的标记位姿估计把它们换算到世界坐标，作为对中前的预计钉位；预计误差即注册噪声的传播。
    /// </summary>
    public sealed class StationRegistration
    {
        private readonly Dictionary<Rivet, (Vector3 PositionM, Vector3 Normal)> _predicted = new Dictionary<Rivet, (Vector3, Vector3)>();

        /// <summary>工位。</summary>
        public InspectionStation Station { get; }

        /// <summary>基准标记位姿估计（世界坐标）。</summary>
        public Pose DatumEstimate { get; }

        /// <summary>基准标记位置估计误差，单位 m。</summary>
        public float DatumErrorM { get; }

        /// <summary>按标记位姿估计推算本工位全部铆钉的预计位置与法向。</summary>
        public StationRegistration(InspectionStation station, Pose datumEstimate)
        {
            Station = station;
            DatumEstimate = datumEstimate;
            RegionMarker datum = station.DatumMarker;
            Vector3 truthCenter = datum.TagCenterWorld;
            Quaternion truthInverse = Quaternion.Inverse(datum.transform.rotation);
            DatumErrorM = Vector3.Distance(truthCenter, datumEstimate.position);
            foreach (Rivet rivet in station.Rivets)
            {
                Vector3 local = truthInverse * (rivet.PositionM - truthCenter);
                Vector3 localNormal = truthInverse * rivet.Normal;
                _predicted[rivet] = (datumEstimate.position + datumEstimate.rotation * local, (datumEstimate.rotation * localNormal).normalized);
            }
        }

        /// <summary>预计钉位与法向（世界坐标）；不属于本工位时返回 false。</summary>
        public bool TryGetPredicted(Rivet rivet, out Vector3 positionM, out Vector3 normal)
        {
            if (_predicted.TryGetValue(rivet, out var p))
            {
                positionM = p.PositionM;
                normal = p.Normal;
                return true;
            }
            positionM = rivet.PositionM;
            normal = rivet.Normal;
            return false;
        }
    }
}
