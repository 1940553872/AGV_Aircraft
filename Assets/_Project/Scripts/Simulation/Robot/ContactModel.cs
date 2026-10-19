using RivetSim.Core.Config;
using UnityEngine;

namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 接触力模型（教学简化模型，ADR-002 / ADR-016）：把蒙皮在铆钉处近似为平面，探头名义端面越过平面的深度即浮动夹持的压缩量 δ；
    /// 行程内 F = k·δ，行程用尽后改用硬限位刚度。耦合质量随接触力变化：达到允许下限时为 1，降到 couplingZeroForceN 时为 0。
    /// 不模拟真实力控与蒙皮变形。<see cref="SurfaceOffsetFaultM"/> 供故障注入（表面比预期更近）。
    /// </summary>
    public sealed class ContactModel
    {
        private readonly InspectionConfig _inspection;
        private readonly float _travelM;
        private Vector3 _surfacePoint;
        private Vector3 _surfaceNormal;

        /// <summary>是否已设置接触表面。</summary>
        public bool HasSurface { get; private set; }

        /// <summary>故障注入：表面沿法向向外偏移的距离，单位 m（0 为无故障）。</summary>
        public float SurfaceOffsetFaultM { get; set; }

        /// <summary>耦合故障系数（M5 故障注入“采集无效”：模拟耦合剂不足），耦合质量乘以该值；默认 1。</summary>
        public float CouplingFaultFactor { get; set; } = 1f;

        /// <summary>浮动夹持压缩量（含越过行程的部分），单位 m。</summary>
        public float CompressionM { get; private set; }

        /// <summary>接触力，单位 N。</summary>
        public float ForceN { get; private set; }

        /// <summary>是否接触。</summary>
        public bool InContact => ForceN > 0f;

        /// <summary>接触力是否在允许范围内。</summary>
        public bool IsWithinAllowedRange => ForceN >= _inspection.contactForceMinN && ForceN <= _inspection.contactForceMaxN;

        /// <summary>接触力是否超过超限阈值。</summary>
        public bool IsOverLimit => ForceN > _inspection.contactForceLimitN;

        /// <summary>检测配置（目标力、允许范围、超限阈值）。</summary>
        public InspectionConfig Config => _inspection;

        /// <summary>创建模型；floatTravelM 为浮动夹持行程（CFG_Arm）。</summary>
        public ContactModel(InspectionConfig inspection, float floatTravelM)
        {
            _inspection = inspection;
            _travelM = floatTravelM;
        }

        /// <summary>设置接触表面（点与外法向，世界坐标）。</summary>
        public void SetSurface(Vector3 pointM, Vector3 normal)
        {
            _surfacePoint = pointM;
            _surfaceNormal = normal.normalized;
            HasSurface = true;
        }

        /// <summary>清除接触表面（接触力归零）。</summary>
        public void ClearSurface()
        {
            HasSurface = false;
            CompressionM = 0f;
            ForceN = 0f;
        }

        /// <summary>按探头名义端面位置（世界坐标）更新压缩量与接触力。</summary>
        public void Update(Vector3 tipM)
        {
            if (!HasSurface)
            {
                CompressionM = 0f;
                ForceN = 0f;
                return;
            }
            Vector3 surface = _surfacePoint + _surfaceNormal * SurfaceOffsetFaultM;
            CompressionM = Mathf.Max(0f, Vector3.Dot(surface - tipM, _surfaceNormal));
            ForceN = ForceFromCompression(CompressionM, _travelM, _inspection.floatingSpringStiffnessNpm, _inspection.hardStopStiffnessNpm);
        }

        /// <summary>当前接触力对应的耦合质量（0～1）。</summary>
        public float Coupling => CouplingFromForce(ForceN, _inspection.contactForceMinN, _inspection.couplingZeroForceN) * Mathf.Clamp01(CouplingFaultFactor);

        /// <summary>压缩量 → 接触力：行程内线性，行程用尽后硬限位刚度，单位 N。</summary>
        public static float ForceFromCompression(float compressionM, float travelM, float springNpm, float hardStopNpm)
        {
            if (compressionM <= 0f) return 0f;
            if (compressionM <= travelM) return springNpm * compressionM;
            return springNpm * travelM + hardStopNpm * (compressionM - travelM);
        }

        /// <summary>接触力 → 耦合质量：≥ fullForceN 为 1，≤ zeroForceN 为 0，其间线性。</summary>
        public static float CouplingFromForce(float forceN, float fullForceN, float zeroForceN) =>
            Mathf.Clamp01((forceN - zeroForceN) / Mathf.Max(1e-6f, fullForceN - zeroForceN));
    }
}
