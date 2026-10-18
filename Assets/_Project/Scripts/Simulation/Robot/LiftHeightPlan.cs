namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 升降柱目标高度计算结果，单位 m。
    /// </summary>
    public readonly struct LiftHeightPlan
    {
        /// <summary>工位铆钉平均离地高度。</summary>
        public readonly float MeanRivetHeightM;

        /// <summary>机械臂安装面目标离地高度（已限制在行程内）。</summary>
        public readonly float MountHeightM;

        /// <summary>升降柱目标伸出量（已限制在行程内）。</summary>
        public readonly float ExtensionM;

        /// <summary>理想伸出量超出行程、被截断时为 true。</summary>
        public readonly bool WasClamped;

        /// <summary>创建结果。</summary>
        public LiftHeightPlan(float meanRivetHeightM, float mountHeightM, float extensionM, bool wasClamped)
        {
            MeanRivetHeightM = meanRivetHeightM;
            MountHeightM = mountHeightM;
            ExtensionM = extensionM;
            WasClamped = wasClamped;
        }
    }
}
