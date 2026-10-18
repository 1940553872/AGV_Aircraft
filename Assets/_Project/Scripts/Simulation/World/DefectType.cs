namespace RivetSim.Simulation.World
{
    /// <summary>
    /// 铆钉真值缺陷类型（教学简化模型，比例见 CFG_Inspection.defectRatio）。
    /// </summary>
    public enum DefectType
    {
        /// <summary>无缺陷。</summary>
        None,
        /// <summary>孔边裂纹。</summary>
        EdgeCrack,
        /// <summary>腐蚀减薄。</summary>
        CorrosionThinning,
        /// <summary>铆钉松动。</summary>
        LooseRivet,
    }
}
